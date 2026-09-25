import { createHash, randomBytes } from 'node:crypto';
import http from 'node:http';
import { WebSocketServer } from 'ws';
import { COURSES, cleanName, toPar, validCard, validLook } from './game.js';
import { RoomManager } from './rooms.js';

const MAX_BODY_BYTES = 16 * 1024;
const MAX_SOCKET_MESSAGE_BYTES = 4 * 1024;

export const hashToken = (token) => createHash('sha256').update(token).digest('hex');

class HttpError extends Error {
  constructor(status, message) { super(message); this.status = status; }
}

/** A fixed window per client address: `limit` requests per `windowMs`. */
function rateLimiter(limit, windowMs, now) {
  const hits = new Map();
  return (key) => {
    const t = now();
    let entry = hits.get(key);
    if (!entry || t - entry.start >= windowMs) {
      entry = { start: t, count: 0 };
      hits.set(key, entry);
      if (hits.size > 50_000) for (const [k, v] of hits) if (t - v.start >= windowMs) hits.delete(k);
    }
    entry.count += 1;
    return entry.count <= limit;
  };
}

/** Stats as the app reads them: JsonUtility has no nullable ints, so "no best yet" is a flag. */
const publicStats = (s) => ({ ...s, bestToPar: s.bestToPar ?? 0, hasBest: s.bestToPar !== null });
const publicPlayer = (p) => ({ id: p.id, name: p.name, body: p.body, kit: p.kit, shirt: p.shirt, stats: publicStats(p.stats) });

/**
 * The HTTP API and the /v1/play WebSocket on one port.
 *
 *   POST  /v1/players            {name, body, kit, shirt} → {player, token}  sign up (anonymous)
 *   GET   /v1/me                                          → {player, recentRounds}
 *   PATCH /v1/me                 {name?, body?, kit?, shirt?} → {player}
 *   POST  /v1/rounds             {course, strokes[], result?} → {stats}  a finished card
 *   GET   /v1/players/:id                                 → {player}          anyone's public profile
 *   GET   /v1/leaderboard?course=cliffside&limit=20       → {course, entries}
 *   GET   /healthz
 *   WS    /v1/play   first message {type:"hello", token}, then RoomManager messages
 *
 * Authenticated calls carry `Authorization: Bearer <token>`. Only the token's hash is stored.
 */
export function createServer({ store, now = () => Date.now(), log = console, dropAfterMs, trustProxy = false } = {}) {
  const sockets = new Map(); // playerId → socket
  const rooms = new RoomManager({
    now,
    dropAfterMs,
    send: (playerId, message) => {
      const ws = sockets.get(playerId);
      if (ws && ws.readyState === ws.OPEN) ws.send(JSON.stringify(message));
    },
    onFinished: async (room, cards) => {
      for (const c of cards) {
        await store.recordRound({ playerId: c.playerId, course: room.course, strokes: c.strokes, toPar: c.toPar, result: c.result, roomCode: room.code });
      }
    },
  });
  const sweep = setInterval(() => rooms.sweep(), 60_000);
  sweep.unref();

  const signupLimit = rateLimiter(10, 60_000, now);
  const requestLimit = rateLimiter(240, 60_000, now);

  async function authenticate(req) {
    const header = req.headers.authorization ?? '';
    const token = header.startsWith('Bearer ') ? header.slice(7).trim() : '';
    if (!token) throw new HttpError(401, 'sign in first');
    const player = await store.playerByTokenHash(hashToken(token));
    if (!player) throw new HttpError(401, 'unknown token');
    return player;
  }

  function readJson(req) {
    return new Promise((resolve, reject) => {
      let size = 0;
      const chunks = [];
      req.on('data', (c) => {
        size += c.length;
        if (size > MAX_BODY_BYTES) { reject(new HttpError(413, 'request too large')); req.destroy(); return; }
        chunks.push(c);
      });
      req.on('end', () => {
        if (chunks.length === 0) return resolve({});
        try { resolve(JSON.parse(Buffer.concat(chunks).toString('utf8'))); } catch { reject(new HttpError(400, 'body is not JSON')); }
      });
      req.on('error', reject);
    });
  }

  function profileFields(body, { partial }) {
    const fields = {};
    if (body.name !== undefined || !partial) {
      const name = cleanName(body.name);
      if (!name) throw new HttpError(400, 'a name is needed');
      fields.name = name;
    }
    const look = {};
    for (const key of ['body', 'kit', 'shirt']) look[key] = body[key] ?? (partial ? undefined : 0);
    if (Object.values(look).some((v) => v !== undefined)) {
      if (!validLook(look.body ?? 0, look.kit ?? 0, look.shirt ?? 0)) throw new HttpError(400, 'body is 0–1, kit and shirt 0–5');
      for (const key of ['body', 'kit', 'shirt']) if (look[key] !== undefined) fields[key] = look[key];
    }
    return fields;
  }

  async function route(req, url, client) {
    const path = url.pathname.replace(/\/+$/, '') || '/';
    const method = req.method;

    if (path === '/healthz' && method === 'GET') return [200, { ok: true, rooms: rooms.rooms.size }];

    if (path === '/v1/players' && method === 'POST') {
      if (!signupLimit(client)) throw new HttpError(429, 'too many sign-ups, try again in a minute');
      const fields = profileFields(await readJson(req), { partial: false });
      const token = randomBytes(32).toString('base64url');
      const player = await store.createPlayer({ ...fields, tokenHash: hashToken(token) });
      return [201, { player: publicPlayer(player), token }];
    }

    if (path === '/v1/me' && method === 'GET') {
      const player = await authenticate(req);
      return [200, { player: publicPlayer(player), recentRounds: await store.recentRounds(player.id, 10) }];
    }

    if (path === '/v1/me' && method === 'PATCH') {
      const player = await authenticate(req);
      const updated = await store.updatePlayer(player.id, profileFields(await readJson(req), { partial: true }));
      return [200, { player: publicPlayer(updated) }];
    }

    if (path === '/v1/rounds' && method === 'POST') {
      const player = await authenticate(req);
      const body = await readJson(req);
      if (!validCard(body.course, body.strokes)) throw new HttpError(400, 'a finished card: a known course and 1–30 strokes per hole');
      const result = ['won', 'tied', 'lost'].includes(body.result) ? body.result : null;
      const stats = await store.recordRound({ playerId: player.id, course: body.course, strokes: body.strokes, toPar: toPar(body.course, body.strokes), result });
      return [201, { stats: publicStats(stats) }];
    }

    const profile = path.match(/^\/v1\/players\/([0-9a-f-]{36})$/i);
    if (profile && method === 'GET') {
      const player = await store.playerById(profile[1]);
      if (!player) throw new HttpError(404, 'no such player');
      return [200, { player: publicPlayer(player) }];
    }

    if (path === '/v1/leaderboard' && method === 'GET') {
      const course = url.searchParams.get('course') ?? 'cliffside';
      if (!COURSES[course]) throw new HttpError(400, 'unknown course');
      const limit = Math.min(100, Math.max(1, Number.parseInt(url.searchParams.get('limit') ?? '20', 10) || 20));
      return [200, { course, entries: await store.leaderboard(course, limit) }];
    }

    throw new HttpError(404, 'not found');
  }

  // Behind a load balancer (Cloud Run, Fly, App Platform) the caller is in X-Forwarded-For; only
  // trust it there, or anyone could dodge the rate limits by sending one.
  const clientAddress = (req) => (trustProxy && req.headers['x-forwarded-for']?.split(',')[0].trim()) || req.socket.remoteAddress || '?';

  const server = http.createServer(async (req, res) => {
    const url = new URL(req.url, 'http://localhost');
    const client = clientAddress(req);
    let status, body;
    try {
      if (!requestLimit(client)) throw new HttpError(429, 'slow down');
      [status, body] = await route(req, url, client);
    } catch (e) {
      status = e instanceof HttpError ? e.status : 500;
      body = { error: e instanceof HttpError ? e.message : 'server error' };
      if (!(e instanceof HttpError)) log.error(e);
    }
    const text = JSON.stringify(body);
    res.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'content-length': Buffer.byteLength(text), 'cache-control': 'no-store' });
    res.end(text);
  });

  // ----- /v1/play -----

  const wss = new WebSocketServer({ noServer: true, maxPayload: MAX_SOCKET_MESSAGE_BYTES });
  server.on('upgrade', (req, socket, head) => {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname !== '/v1/play') { socket.destroy(); return; }
    wss.handleUpgrade(req, socket, head, (ws) => wss.emit('connection', ws, req));
  });

  wss.on('connection', (ws) => {
    let player = null;
    ws.isAlive = true;
    ws.on('pong', () => { ws.isAlive = true; });
    const hello = setTimeout(() => { if (!player) ws.close(4001, 'say hello'); }, 10_000);

    ws.on('message', async (data) => {
      let message;
      try { message = JSON.parse(data.toString('utf8')); } catch { ws.send(JSON.stringify({ type: 'error', error: 'not JSON' })); return; }
      if (!player) {
        if (message?.type !== 'hello' || typeof message.token !== 'string') { ws.close(4001, 'say hello'); return; }
        const found = await store.playerByTokenHash(hashToken(message.token)).catch(() => null);
        if (!found) { ws.close(4003, 'unknown token'); return; }
        clearTimeout(hello);
        player = found;
        const previous = sockets.get(player.id);
        if (previous && previous !== ws) previous.close(4000, 'signed in elsewhere');
        sockets.set(player.id, ws);
        ws.send(JSON.stringify({ type: 'welcome', playerId: player.id, name: player.name }));
        // Back in a room this player is part of (a dropped connection): show them where they are.
        const room = rooms.roomFor(player.id);
        if (room) rooms.join(player, room.code);
        return;
      }
      rooms.handle(player, message);
    });

    ws.on('close', () => {
      clearTimeout(hello);
      if (player && sockets.get(player.id) === ws) {
        sockets.delete(player.id);
        rooms.disconnected(player.id);
      }
    });
  });

  const heartbeat = setInterval(() => {
    for (const ws of wss.clients) {
      if (!ws.isAlive) { ws.terminate(); continue; }
      ws.isAlive = false;
      ws.ping();
    }
  }, 25_000);
  heartbeat.unref();

  server.on('close', () => { clearInterval(sweep); clearInterval(heartbeat); for (const ws of wss.clients) ws.terminate(); });

  return { server, rooms };
}
