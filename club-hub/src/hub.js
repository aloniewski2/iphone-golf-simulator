// The hub: who is online, what they are doing, and the invites between friends. Transport-agnostic: index.js hands it a
// socket-like object ({ send(text), close(code), terminate() }) per connection and feeds it frames.
//
// Everything live is in memory. One process is the whole service (see README: scaling out needs a shared bus, not done).
import { randomUUID } from 'node:crypto';
import { AuthError } from './auth.js';

const STATES = new Set(['plaza', 'lobby', 'match', 'away', 'dnd']);
const SPORTS = new Set(['tennis', 'golf']);
const CODE_RE = /^[A-HJ-NP-Z2-9]{6}$/;      // ABCDEFGHJKLMNPQRSTUVWXYZ23456789
const TOKEN_RE = /^[0-9a-fA-F]{32,200}$/;
const MAX_ID = 128;
const MAX_FRIENDS = 2000;
const MAX_PENDING_PER_RECEIVER = 3;
const LAST_KNOWN_MAX = 10_000;
const MAX_ROOM_MEMBERS = 8;
const MAX_RELAY_CHARS = 128 * 1024;

export const CLOSE = { UNAUTHORIZED: 4401, RATE_LIMITED: 4429, REPLACED: 4409 };

const isId = (v) => typeof v === 'string' && v.length > 0 && v.length <= MAX_ID;
const isUInt = (v, max) => Number.isInteger(v) && v >= 0 && v <= max;

/** Sliding window: record a hit, return true when the window is now over its limit. */
function overLimit(hits, now, windowMs, max) {
  while (hits.length && now - hits[0] >= windowMs) hits.shift();
  hits.push(now);
  return hits.length > max;
}

function cleanName(v) {
  const s = typeof v === 'string' ? v.replace(/[\u0000-\u001f\u007f-\u009f\u2028\u2029\u202a-\u202e\u2066-\u2069]/g, '').trim() : '';
  const name = [...s].slice(0, 40).join('').trim(); // code points, so an emoji is not cut in half
  return name || 'Player';
}

function cleanLobby(v) {
  if (v === null || v === undefined) return { value: null };
  if (typeof v !== 'object' || Array.isArray(v)) return { error: 'lobby must be null or an object' };
  if (typeof v.code !== 'string' || !CODE_RE.test(v.code)) return { error: 'lobby.code is not a lobby code' };
  if (!SPORTS.has(v.sport)) return { error: 'lobby.sport' };
  if (!isUInt(v.size, 64) || !isUInt(v.capacity, 64)) return { error: 'lobby.size/capacity' };
  if (typeof v.open !== 'boolean') return { error: 'lobby.open' };
  if (!isUInt(v.group, 0xffffffff)) return { error: 'lobby.group' };
  return { value: { code: v.code, sport: v.sport, size: v.size, capacity: v.capacity, open: v.open, group: v.group } };
}

/** Validates a presence-shaped object (the hello's `presence` and the presence message). */
function cleanPresence(m) {
  if (!STATES.has(m.state)) return { error: 'state must be plaza, lobby, match, away or dnd' };
  const lobby = cleanLobby(m.lobby);
  if (lobby.error) return lobby;
  if (m.invisible !== undefined && typeof m.invisible !== 'boolean') return { error: 'invisible must be a boolean' };
  return { value: { state: m.state, lobby: lobby.value, invisible: m.invisible === true } };
}

export class Hub {
  /**
   * @param {object} o
   * @param {object} o.store      see store.js
   * @param {Function} o.authenticate  see auth.js
   * @param {object} o.pusher     see push.js
   */
  constructor({
    store, authenticate, pusher, log = console, now = Date.now,
    inviteTtlMs = 120_000, offlineInviteTtlMs = 15 * 60_000, awayPushMs = 10_000, idleMs = 65_000, helloTimeoutMs = 10_000,
    roomGraceMs = 120_000, limits = {},
  }) {
    this.store = store;
    this.authenticate = authenticate;
    this.pusher = pusher;
    this.log = log;
    this.now = now;
    this.inviteTtlMs = inviteTtlMs;
    this.offlineInviteTtlMs = offlineInviteTtlMs;
    this.roomGraceMs = roomGraceMs;
    this.awayPushMs = awayPushMs;
    this.idleMs = idleMs;
    this.helloTimeoutMs = helloTimeoutMs;
    this.limits = { msgWindowMs: 10_000, msgMax: 30, inviteMax: 10, resolveMax: 20, perMinute: 60_000, relayWindowMs: 1000, relayMax: 400, ...limits };
    this.sessions = new Map();   // id -> Conn (signed in)
    this.conns = new Set();      // every open Conn, signed in or not
    this.invites = new Map();    // inviteID -> invite
    this.lastKnown = new Map();  // id -> { name, friends:Set } for players who were here recently (offline friend checks)
    this.rooms = new Map();      // party code -> { code, members:Set<id>, allowed:Set<id>, emptySince }
    this.sweeper = setInterval(() => this.#sweepIdle(), Math.min(5000, Math.max(50, idleMs / 2)));
    this.sweeper.unref();
  }

  get onlineCount() { return this.sessions.size; }

  close() {
    clearInterval(this.sweeper);
    for (const inv of this.invites.values()) clearTimeout(inv.timer);
    for (const c of this.conns) { clearTimeout(c.helloTimer); c.sock.terminate?.(); }
  }

  get roomCount() { return this.rooms.size; }

  // ---- connections ----------------------------------------------------------------------------------------------

  connect(sock) {
    const conn = {
      sock, id: null, name: '', friends: new Set(), presence: null, awaySince: null,
      seen: new Map(),            // friend id -> serialized presence we last sent about them (so we only send changes)
      msgHits: [], inviteHits: [], resolveHits: [], relayHits: [],
      lastMsgAt: this.now(), closed: false, chain: Promise.resolve(), chainIdle: false, helloTimer: null,
    };
    this.conns.add(conn);
    // A socket that never says hello would sit there forever.
    conn.helloTimer = setTimeout(() => { if (!conn.id) this.#kick(conn, CLOSE.UNAUTHORIZED, 'hello required'); }, this.helloTimeoutMs);
    conn.helloTimer.unref();
    return {
      message: (text) => this.#onFrame(conn, text),
      close: () => this.#onClose(conn),
    };
  }

  #onFrame(conn, text) {
    if (conn.closed) return;
    const now = this.now();
    conn.lastMsgAt = now;
    let msg;
    try { msg = JSON.parse(text); } catch { msg = null; }
    const ok = msg && typeof msg === 'object' && !Array.isArray(msg) && typeof msg.t === 'string';
    // Party relay frames (a lobby's game traffic) come many times a second: they have their own, much higher limit.
    const relay = ok && msg.t === 'rd' && conn.id;
    if (relay ? overLimit(conn.relayHits, now, this.limits.relayWindowMs, this.limits.relayMax)
              : overLimit(conn.msgHits, now, this.limits.msgWindowMs, this.limits.msgMax)) {
      this.#send(conn, { t: 'error', code: 'rate_limited', message: 'too many messages' });
      this.#kick(conn, CLOSE.RATE_LIMITED, 'rate limited');
      return;
    }
    if (!ok) {
      if (!conn.id) { this.#send(conn, { t: 'error', code: 'unauthorized', message: 'hello required' }); this.#kick(conn, CLOSE.UNAUTHORIZED, 'hello required'); }
      else this.#send(conn, { t: 'error', code: 'bad_message', message: 'not a JSON object with a "t"' });
      return;
    }
    // Relay frames carry no state the hub keeps, so they skip the per-connection queue (order is kept by the socket itself;
    // nothing before them can still be waiting except a hello, and relay needs a signed-in connection).
    if (relay && conn.chainIdle) { this.#relay(conn, msg); return; }
    // Strictly one message at a time per connection: hello awaits the auth check and later frames must not overtake it.
    conn.chainIdle = false;
    const done = (conn.chain = conn.chain.then(() => this.#handle(conn, msg)).catch((e) => {
      this.log.error?.('hub error', e);
      this.#send(conn, { t: 'error', code: 'bad_message', message: 'internal error', ref: msg.t });
    }));
    done.then(() => { if (conn.chain === done) conn.chainIdle = true; });
  }

  async #handle(conn, msg) {
    if (conn.closed) return;
    if (!conn.id) {
      if (msg.t !== 'hello') {
        this.#send(conn, { t: 'error', code: 'unauthorized', message: 'hello required', ref: msg.t });
        this.#kick(conn, CLOSE.UNAUTHORIZED, 'hello required');
        return;
      }
      return this.#hello(conn, msg);
    }
    switch (msg.t) {
      case 'hello': return this.#bad(conn, msg, 'already signed in');
      case 'ping': return this.#send(conn, { t: 'pong' });
      case 'presence': return this.#presence(conn, msg);
      case 'friends': return this.#friends(conn, msg);
      case 'invite': return this.#invite(conn, msg);
      case 'inviteResponse': return this.#inviteResponse(conn, msg);
      case 'inviteCancel': return this.#inviteCancel(conn, msg);
      case 'resolve': return this.#resolve(conn, msg);
      case 'block': return this.#block(conn, msg, true);
      case 'unblock': return this.#block(conn, msg, false);
      case 'push': return this.#registerPush(conn, msg);
      case 'roomOpen': return this.#roomOpen(conn, msg);
      case 'roomJoin': return this.#roomJoin(conn, msg);
      case 'roomLeave': return this.#roomLeave(conn, msg);
      case 'rd': return this.#relay(conn, msg);
      case 'bye': return this.#bye(conn);
      default: return this.#bad(conn, msg, `unknown message "${msg.t}"`);
    }
  }

  // ---- hello / bye / disconnect ---------------------------------------------------------------------------------

  async #hello(conn, msg) {
    const fail = (code, message) => { this.#send(conn, { t: 'error', code, message }); this.#kick(conn, CLOSE.UNAUTHORIZED, message); };
    if (msg.v !== 1) return fail('bad_message', 'unsupported protocol version');
    const friends = this.#cleanFriends(msg.friends);
    if (!friends) return fail('bad_message', 'friends must be an array of ids');
    const presence = msg.presence === undefined ? { value: { state: 'plaza', lobby: null, invisible: false } } : cleanPresence(msg.presence ?? {});
    if (presence.error) return fail('bad_message', `presence: ${presence.error}`);
    let push = null;
    if (msg.push !== undefined) {
      push = this.#cleanPush(msg.push);
      if (!push) return fail('bad_message', 'push must be { token, env }');
    }

    let id;
    try { id = await this.authenticate(msg.auth); } catch (e) {
      if (!(e instanceof AuthError)) this.log.error?.('auth error', e);
      return fail('unauthorized', e instanceof AuthError ? e.message : 'authentication failed');
    }
    if (conn.closed) return;

    friends.delete(id);
    clearTimeout(conn.helloTimer);
    const old = this.sessions.get(id);
    if (old && old !== conn) {
      // Same player, new socket (phone woke up before the old one timed out): the new one wins and keeps pending invites.
      old.replaced = true;
      this.sessions.delete(id);
      this.#kick(old, CLOSE.REPLACED, 'signed in elsewhere');
    }
    conn.id = id;
    conn.name = cleanName(msg.name);
    conn.friends = friends;
    conn.presence = presence.value;
    conn.awaySince = conn.presence.state === 'away' ? this.now() : null;
    this.sessions.set(id, conn);

    if (push) await this.#savePush(id, push.token, push.env, conn);
    else await this.#refreshPushProfile(conn);

    this.#send(conn, { t: 'welcome', you: id, serverTime: this.now(), v: 1 });
    this.#refreshAround(conn, old?.friends);
    this.#deliverPending(conn);
  }

  async #bye(conn) {
    this.#dropSession(conn);
    conn.closed = true;
    conn.sock.close(1000);
    this.conns.delete(conn);
  }

  #onClose(conn) {
    if (conn.closed && !this.conns.has(conn)) return;
    conn.closed = true;
    clearTimeout(conn.helloTimer);
    this.conns.delete(conn);
    if (!conn.replaced) this.#dropSession(conn);
  }

  /** The player is gone from everyone's point of view: offline presence, and their outgoing invites are withdrawn. */
  #dropSession(conn) {
    if (!conn.id || this.sessions.get(conn.id) !== conn) return;
    this.sessions.delete(conn.id);
    this.#remember(conn);
    for (const inv of [...this.invites.values()]) if (inv.from.id === conn.id) this.#endInvite(inv, 'cancelled', { notifySender: false });
    for (const room of [...this.rooms.values()]) if (room.members.has(conn.id)) this.#leaveRoom(room, conn.id);
    this.#refreshAround(conn);
    conn.seen.clear();
  }

  #kick(conn, code, reason) {
    conn.closed = true;
    clearTimeout(conn.helloTimer);
    try { conn.sock.close(code, reason); } catch { /* already closing */ }
    this.#onClose(conn);
    // close(code) finishes with a close handshake; drop the connection if the peer does not answer.
    setTimeout(() => { try { conn.sock.terminate?.(); } catch { /* gone */ } }, 1000).unref();
  }

  #sweepIdle() {
    const now = this.now();
    for (const c of this.conns) {
      if (c.id && now - c.lastMsgAt > this.idleMs) this.#kick(c, 1001, 'idle');
    }
    // A party room nobody has been in for a while is gone (its members may come back within the grace: a phone that lost Wi-Fi).
    for (const [code, room] of this.rooms) if (room.members.size === 0 && now - room.emptySince > this.roomGraceMs) this.rooms.delete(code);
  }

  #remember(conn) {
    this.lastKnown.delete(conn.id);
    this.lastKnown.set(conn.id, { name: conn.name, friends: conn.friends });
    if (this.lastKnown.size > LAST_KNOWN_MAX) this.lastKnown.delete(this.lastKnown.keys().next().value);
  }

  // ---- presence -------------------------------------------------------------------------------------------------

  #presence(conn, msg) {
    const p = cleanPresence(msg);
    if (p.error) return this.#bad(conn, msg, p.error);
    const prev = conn.presence;
    const next = p.value;

    if (prev.lobby && prev.lobby.code !== next.lobby?.code) {
      // The lobby changed or closed: invites for the old lobby point nowhere. (An invite already sent for the NEW code, before this
      // presence arrived, stays valid.)
      for (const inv of [...this.invites.values()]) {
        if (inv.from.id === conn.id && inv.lobbyCode !== next.lobby?.code) this.#endInvite(inv, 'cancelled');
      }
    }
    const wasAway = prev.state === 'away';
    conn.presence = next;
    conn.awaySince = next.state === 'away' ? (wasAway ? conn.awaySince : this.now()) : null;

    // Heartbeats repeat the same presence every 20 s; #refreshView only sends when the visible content changed.
    this.#refreshAround(conn);
    if (wasAway && next.state !== 'away') this.#deliverPending(conn);
  }

  #friends(conn, msg) {
    const ids = this.#cleanFriends(msg.ids);
    if (!ids) return this.#bad(conn, msg, 'ids must be an array of ids');
    ids.delete(conn.id);
    const before = conn.friends;
    conn.friends = ids;
    this.#refreshAround(conn, before);
    // Someone who is no longer a friend (on either list) cannot keep sending us (or receiving) invites.
    for (const inv of [...this.invites.values()]) {
      const other = inv.from.id === conn.id ? inv.to : inv.to === conn.id ? inv.from.id : null;
      if (other && !ids.has(other) && !this.sessions.get(other)?.friends.has(conn.id)) this.#endInvite(inv, 'cancelled');
    }
    return this.#refreshPushProfile(conn);
  }

  /** Candidates for a visibility change involving `conn`: its friends now and before, and everyone online who lists it. */
  #refreshAround(conn, previousFriends) {
    const ids = new Set(conn.friends);
    if (previousFriends) for (const id of previousFriends) ids.add(id);
    for (const s of this.sessions.values()) if (s.friends.has(conn.id)) ids.add(s.id);
    for (const id of ids) {
      this.#refreshView(this.sessions.get(id), conn.id);
      if (this.sessions.get(conn.id) === conn) this.#refreshView(conn, id);
    }
  }

  /** Game Center friendship is mutual, but each phone only knows the friends who allowed this game to read their list: one side
   *  listing the other is enough (otherwise a friend who never opened the friends list would be invisible to everyone). */
  #canSee(viewer, target) {
    return (viewer.friends.has(target.id) || target.friends.has(viewer.id))
      && !this.store.isBlocked(viewer.id, target.id) && !target.presence.invisible;
  }

  /** Tell `viewer` what they should now know about `targetId`: online/changed, or offline if they could see them before. */
  #refreshView(viewer, targetId) {
    if (!viewer || viewer.id === targetId) return;
    const target = this.sessions.get(targetId);
    const prev = viewer.seen.get(targetId);
    if (target && this.#canSee(viewer, target)) {
      const m = { t: 'presence', id: target.id, online: true, state: target.presence.state, lobby: target.presence.lobby, name: target.name };
      const key = JSON.stringify(m);
      if (key !== prev) { viewer.seen.set(targetId, key); this.#send(viewer, m); }
    } else if (prev !== undefined) {
      viewer.seen.delete(targetId);
      this.#send(viewer, { t: 'presence', id: targetId, online: false });
    }
  }

  // ---- invites --------------------------------------------------------------------------------------------------

  #inviteView(inv) {
    return {
      t: 'invite', inviteID: inv.id, from: inv.from, sport: inv.sport, lobbyCode: inv.lobbyCode,
      group: inv.group, capacity: inv.capacity, expiresAt: inv.expiresAt,
    };
  }

  #notifySender(inv, status) {
    const s = this.sessions.get(inv.from.id);
    if (s) this.#send(s, { t: 'inviteUpdate', inviteID: inv.id, to: inv.to, status });
  }

  /** Remove an invite and tell both sides. `expired` and `cancelled` are the two ways it can die unanswered. */
  #endInvite(inv, status, { notifySender = true } = {}) {
    if (!this.invites.delete(inv.id)) return;
    clearTimeout(inv.timer);
    if (notifySender) this.#notifySender(inv, status);
    const r = this.sessions.get(inv.to);
    if (r && inv.delivered) this.#send(r, { t: 'inviteGone', inviteID: inv.id, status });
  }

  #deliver(inv, receiver) {
    inv.delivered = true;
    this.#send(receiver, this.#inviteView(inv));
    this.#notifySender(inv, 'delivered');
  }

  /** The receiver is (again) reachable on a socket: hand over invites that were waiting (pushed, or sent while they were away). */
  #deliverPending(receiver) {
    for (const inv of this.invites.values()) {
      if (inv.to !== receiver.id || inv.delivered || inv.pushing) continue;
      // one that waited for them while they were offline gets a live invite's lifetime from now (the banner's countdown)
      if (inv.expiresAt - this.now() > this.inviteTtlMs) { clearTimeout(inv.timer); inv.expiresAt = this.now() + this.inviteTtlMs; this.#track(inv); }
      this.#deliver(inv, receiver);
    }
  }

  async #invite(conn, msg) {
    const ref = typeof msg.to === 'string' ? msg.to : 'invite';
    const bad = (m) => this.#bad(conn, msg, m, 'invite');
    if (overLimit(conn.inviteHits, this.now(), this.limits.perMinute, this.limits.inviteMax)) {
      return this.#send(conn, { t: 'error', code: 'rate_limited', message: 'too many invites', ref: 'invite' });
    }
    if (!isId(msg.to)) return bad('to must be a player id');
    if (msg.to === conn.id) return bad('cannot invite yourself');
    if (!SPORTS.has(msg.sport)) return bad('sport must be tennis or golf');
    if (typeof msg.lobbyCode !== 'string' || !CODE_RE.test(msg.lobbyCode)) return bad('lobbyCode is not a lobby code');
    if (!isUInt(msg.group, 0xffffffff)) return bad('group must be a UInt32');
    if (!isUInt(msg.capacity, 64) || msg.capacity < 1) return bad('capacity must be a small positive integer');

    const to = msg.to;
    // If I blocked them I know why it fails. If THEY blocked me, say nothing different from "not friends" (no leak).
    if (this.store.hasBlocked(conn.id, to)) return this.#send(conn, { t: 'error', code: 'blocked', message: 'you blocked this player', ref });
    const receiver = this.sessions.get(to);
    const known = receiver ? receiver.friends : (this.lastKnown.get(to)?.friends ?? this.store.getPush(to)?.friends ?? null);
    const theirFriends = known instanceof Set ? known : Array.isArray(known) ? new Set(known) : null;
    // friends on either list (see #canSee), and not blocked by them
    if ((!conn.friends.has(to) && !theirFriends?.has(conn.id)) || this.store.hasBlocked(to, conn.id)) {
      return this.#send(conn, { t: 'error', code: 'not_friends', message: 'not friends', ref });
    }

    const replaced = [...this.invites.values()].find((i) => i.from.id === conn.id && i.to === to);
    let pending = 0;
    for (const i of this.invites.values()) if (i.to === to && i !== replaced) pending++;
    if (pending >= MAX_PENDING_PER_RECEIVER) return this.#send(conn, { t: 'error', code: 'too_many_pending', message: 'that player has too many pending invites', ref });

    // do-not-disturb: tell the sender, deliver nothing, keep no invite.
    if (receiver && receiver.presence.state === 'dnd') return this.#send(conn, { t: 'inviteUpdate', inviteID: randomUUID(), to, status: 'busy' });

    if (replaced) this.#endInvite(replaced, 'cancelled');
    // the invited friend may join the sender's party room (its code is the lobby code)
    const room = this.rooms.get(msg.lobbyCode);
    if (room && room.members.has(conn.id)) room.allowed.add(to);

    const inv = {
      id: randomUUID(), from: { id: conn.id, name: conn.name }, to, sport: msg.sport, lobbyCode: msg.lobbyCode,
      group: msg.group, capacity: msg.capacity, expiresAt: this.now() + this.inviteTtlMs,
      delivered: false, pushing: false, timer: null,
    };

    // Rule 5/6: push only when there is no usable socket. "Away" longer than awayPushMs means the app is suspended.
    const rec = this.store.getPush(to);
    const socketUsable = receiver && !(receiver.presence.state === 'away' && this.now() - receiver.awaySince > this.awayPushMs);
    const canPush = !socketUsable && rec && this.pusher.configured;
    if (!socketUsable && rec && !this.pusher.configured) this.log.log?.('push skipped: APNs is not configured');

    if (!socketUsable && !canPush && !receiver) {
      // Offline with no way to push: the invite waits here (longer than a live one) and is handed over the moment they open
      // the game, while the sender's party is still on. The sender sees "queued".
      inv.expiresAt = this.now() + this.offlineInviteTtlMs;
      this.#track(inv);
      return this.#notifySender(inv, 'queued');
    }

    this.#track(inv);
    if (canPush) {
      inv.pushing = true;
      return this.#pushInvite(inv, rec);
    }
    // Live socket (also "away" but with no way to push: the socket may still be alive, so try it).
    this.#deliver(inv, receiver);
  }

  async #pushInvite(inv, rec) {
    const result = await this.pusher.send(this.#inviteView(inv), rec);
    if (!this.invites.has(inv.id)) return; // cancelled / expired meanwhile
    inv.pushing = false;
    if (result.removeToken) await this.#savePush(inv.to, '', null);
    if (!result.ok) {
      // APNs refused: nothing will arrive, so drop the invite and say so.
      this.invites.delete(inv.id);
      clearTimeout(inv.timer);
      this.#notifySender(inv, 'unavailable');
      return;
    }
    this.#notifySender(inv, 'pushed');
    // Receiver may have reconnected while APNs was answering: then they get the in-app card too.
    const r = this.sessions.get(inv.to);
    if (r && !inv.delivered && r.presence.state !== 'away') this.#deliver(inv, r);
  }

  #track(inv) {
    this.invites.set(inv.id, inv);
    inv.timer = setTimeout(() => this.#endInvite(inv, 'expired'), Math.max(0, inv.expiresAt - this.now()));
    inv.timer.unref();
  }

  #inviteResponse(conn, msg) {
    if (typeof msg.accept !== 'boolean') return this.#bad(conn, msg, 'accept must be a boolean', msg.inviteID);
    const inv = typeof msg.inviteID === 'string' ? this.invites.get(msg.inviteID) : null;
    if (!inv || inv.to !== conn.id) return this.#bad(conn, msg, 'unknown or expired invite', msg.inviteID);
    this.invites.delete(inv.id);
    clearTimeout(inv.timer);
    this.#notifySender(inv, msg.accept ? 'accepted' : 'declined');
  }

  #inviteCancel(conn, msg) {
    const inv = typeof msg.inviteID === 'string' ? this.invites.get(msg.inviteID) : null;
    if (!inv || inv.from.id !== conn.id) return this.#bad(conn, msg, 'unknown or expired invite', msg.inviteID);
    this.#endInvite(inv, 'cancelled');
  }

  // ---- resolve, block, push --------------------------------------------------------------------------------------

  #resolve(conn, msg) {
    if (overLimit(conn.resolveHits, this.now(), this.limits.perMinute, this.limits.resolveMax)) {
      return this.#send(conn, { t: 'error', code: 'rate_limited', message: 'too many lookups', ref: 'resolve' });
    }
    if (typeof msg.code !== 'string') return this.#bad(conn, msg, 'code must be a string');
    const code = msg.code.trim().toUpperCase();
    const host = this.findLobby(code);
    if (!host) return this.#send(conn, { t: 'resolved', code, found: false });
    const l = host.presence.lobby;
    // a code is an open door: whoever has it may join that party's room
    this.rooms.get(code)?.allowed.add(conn.id);
    this.#send(conn, { t: 'resolved', code, found: true, from: { id: host.id, name: host.name }, sport: l.sport, group: l.group, capacity: l.capacity });
  }

  /** The live lobby with this code, as its host's session; null if there is none. Used by resolve and the landing page. */
  findLobby(code) {
    if (typeof code !== 'string' || !CODE_RE.test(code)) return null;
    for (const s of this.sessions.values()) if (s.presence.lobby?.code === code) return s;
    return null;
  }

  async #block(conn, msg, on) {
    if (!isId(msg.id) || msg.id === conn.id) return this.#bad(conn, msg, 'id must be another player');
    if (on) await this.store.block(conn.id, msg.id); else await this.store.unblock(conn.id, msg.id);
    if (on) {
      for (const inv of [...this.invites.values()]) {
        if ((inv.from.id === conn.id && inv.to === msg.id) || (inv.to === conn.id && inv.from.id === msg.id)) this.#endInvite(inv, 'cancelled');
      }
    }
    // Visibility depends on blocks in both directions, so re-evaluate this pair both ways.
    this.#refreshView(conn, msg.id);
    this.#refreshView(this.sessions.get(msg.id), conn.id);
  }

  #cleanPush(p) {
    if (!p || typeof p !== 'object') return null;
    if (p.token === '') return { token: '', env: null };
    if (typeof p.token !== 'string' || !TOKEN_RE.test(p.token)) return null;
    if (p.env !== 'sandbox' && p.env !== 'production') return null;
    return { token: p.token, env: p.env };
  }

  async #registerPush(conn, msg) {
    const p = this.#cleanPush(msg);
    if (!p) return this.#bad(conn, msg, 'push needs a hex token and env sandbox|production (or token "")');
    await this.#savePush(conn.id, p.token, p.env, conn);
  }

  async #savePush(id, token, env, conn) {
    try {
      if (!token) { await this.store.setPush(id, null); return; }
      const c = conn ?? this.sessions.get(id);
      await this.store.setPush(id, { token, env, name: c?.name ?? '', friends: c ? [...c.friends] : [] });
    } catch (e) { this.log.error?.('could not save push token', e); }
  }

  /** Keep the stored name + friends of a token holder current, so offline invites can check mutual friendship. */
  async #refreshPushProfile(conn) {
    const rec = this.store.getPush(conn.id);
    if (!rec) return;
    await this.#savePush(conn.id, rec.token, rec.env, conn);
  }

  // ---- party rooms (the relay) ------------------------------------------------------------------------------------
  // A party's phones talk through the service: lobby state, where everyone walks in the Plaza, and the match itself. The room's code
  // is the party's lobby code. Its opener and everyone invited to it (or who resolved its code) may join; nobody else.

  #roomCode(conn, msg) {
    if (typeof msg.room === 'string' && CODE_RE.test(msg.room)) return msg.room;
    this.#bad(conn, msg, 'room must be a lobby code');
    return null;
  }

  #roomOpen(conn, msg) {
    const code = this.#roomCode(conn, msg); if (!code) return;
    let room = this.rooms.get(code);
    if (room && !room.allowed.has(conn.id)) return this.#send(conn, { t: 'error', code: 'room_taken', message: 'that party code is in use', ref: code });
    if (!room) { room = { code, members: new Set(), allowed: new Set([conn.id]), emptySince: this.now() }; this.rooms.set(code, room); }
    this.#enterRoom(room, conn);
  }

  #roomJoin(conn, msg) {
    const code = this.#roomCode(conn, msg); if (!code) return;
    const room = this.rooms.get(code);
    if (!room) return this.#send(conn, { t: 'error', code: 'room_gone', message: 'that party has ended', ref: code });
    if (!room.allowed.has(conn.id)) return this.#send(conn, { t: 'error', code: 'room_denied', message: 'not invited to that party', ref: code });
    this.#enterRoom(room, conn);
  }

  #roomLeave(conn, msg) {
    const code = this.#roomCode(conn, msg); if (!code) return;
    const room = this.rooms.get(code);
    if (room?.members.has(conn.id)) this.#leaveRoom(room, conn.id);
  }

  #enterRoom(room, conn) {
    if (!room.members.has(conn.id) && room.members.size >= MAX_ROOM_MEMBERS) {
      return this.#send(conn, { t: 'error', code: 'room_full', message: 'that party is full', ref: room.code });
    }
    room.members.add(conn.id);
    this.#roomPeers(room);
  }

  #leaveRoom(room, id) {
    room.members.delete(id);
    if (room.members.size === 0) room.emptySince = this.now();
    else this.#roomPeers(room);
    const c = this.sessions.get(id);
    if (c) this.#send(c, { t: 'roomPeers', room: room.code, peers: [] });
  }

  /** Everyone in the room learns who is in it (themselves included). */
  #roomPeers(room) {
    const peers = [...room.members].map((id) => ({ id, name: this.sessions.get(id)?.name ?? 'Player' }));
    for (const id of room.members) { const c = this.sessions.get(id); if (c) this.#send(c, { t: 'roomPeers', room: room.code, peers }); }
  }

  /** One frame of a party's traffic, to everyone else in the room (or the members named in `to`). Never stored. */
  #relay(conn, msg) {
    const room = typeof msg.room === 'string' ? this.rooms.get(msg.room) : null;
    if (!room || !room.members.has(conn.id)) return this.#send(conn, { t: 'error', code: 'room_gone', message: 'not in that party', ref: msg.room ?? 'rd' });
    if (typeof msg.d !== 'string' || msg.d.length > MAX_RELAY_CHARS) return this.#bad(conn, msg, 'd must be a string up to 128 KB');
    let to = room.members;
    if (msg.to !== undefined) {
      if (!Array.isArray(msg.to) || msg.to.length > MAX_ROOM_MEMBERS || !msg.to.every(isId)) return this.#bad(conn, msg, 'to must be an array of ids');
      to = msg.to;
    }
    const frame = JSON.stringify({ t: 'rd', room: room.code, from: conn.id, d: msg.d });
    for (const id of to) {
      if (id === conn.id || !room.members.has(id)) continue;
      const c = this.sessions.get(id);
      if (c && !c.closed) { try { c.sock.send(frame); } catch { /* gone; its close cleans up */ } }
    }
  }

  // ---- helpers --------------------------------------------------------------------------------------------------

  #cleanFriends(v) {
    if (!Array.isArray(v) || v.length > MAX_FRIENDS || !v.every(isId)) return null;
    return new Set(v);
  }

  /** `ref`: the inviteID when the message has one, otherwise the message type. */
  #bad(conn, msg, message, ref) {
    this.#send(conn, { t: 'error', code: 'bad_message', message, ref: ref ?? msg.t });
  }

  #send(conn, obj) {
    if (conn.closed && obj.t !== 'error') return;
    try { conn.sock.send(JSON.stringify(obj)); } catch { /* peer is gone; the close handler cleans up */ }
  }
}
