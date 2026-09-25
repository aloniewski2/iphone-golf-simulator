import { randomInt } from 'node:crypto';
import { COURSES, MAX_STROKES_PER_HOLE, matchResults, toPar } from './game.js';

export const MAX_PLAYERS = 4;
export const MIN_PLAYERS_TO_START = 2;
const CODE_LETTERS = 'ABCDEFGHJKLMNPQRSTUVWXYZ'; // no I or O: a code is read aloud across a room
const CODE_LENGTH = 4;
const ROOM_LIFETIME_MS = 3 * 60 * 60 * 1000;

/**
 * Online rounds. Everyone plays the same course on their own phone at the same time, with the
 * same wind (the room's seed feeds Match.WindFor in the app), and each hole's score is shared the
 * moment it is holed. The server keeps every card, so a phone that drops and rejoins catches up,
 * and when every card is complete it records the round for each player.
 *
 * Transport-free: `send(playerId, message)` delivers to a player's socket, and
 * `onFinished(room, cards)` persists the round. Messages are plain objects with a `type`.
 */
export class RoomManager {
  constructor({ send, onFinished = async () => {}, now = () => Date.now(), dropAfterMs = 120_000 }) {
    this.send = send;
    this.onFinished = onFinished;
    this.now = now;
    this.dropAfterMs = dropAfterMs;
    this.rooms = new Map(); // code → room
    this.roomOf = new Map(); // playerId → code
  }

  /** One message from a signed-in player. `player` is {id, name, body, kit, shirt}. */
  handle(player, message) {
    const type = message?.type;
    switch (type) {
      case 'create': return this.create(player, message.course, false);
      case 'quick': return this.quick(player, message.course);
      case 'join': return this.join(player, String(message.code ?? '').toUpperCase().trim());
      case 'leave': return this.leave(player.id);
      case 'start': return this.start(player.id);
      case 'hole': return this.hole(player.id, message.hole, message.strokes);
      case 'ping': return this.send(player.id, { type: 'pong' });
      default: return this.error(player.id, `unknown message ${JSON.stringify(type)}`);
    }
  }

  /** The socket closed: before the start the player leaves; after it their card is kept. */
  disconnected(playerId) {
    const room = this.roomFor(playerId);
    if (!room) return;
    if (!room.started) { this.leave(playerId); return; }
    const member = room.players.find((p) => p.id === playerId);
    member.connected = false;
    if (room.players.every((p) => !p.connected)) { this.close(room); return; }
    this.broadcast(room);
    // Gone for good if they are not back in time, so the others' round can still finish.
    const timer = setTimeout(() => {
      if (this.rooms.get(room.code) === room && !member.connected && !member.left) {
        member.left = true;
        this.broadcast(room);
        this.maybeFinish(room);
      }
    }, this.dropAfterMs);
    timer.unref?.();
  }

  create(player, course = 'cliffside', isPublic = false) {
    if (!COURSES[course]) return this.error(player.id, 'unknown course');
    this.leave(player.id);
    const room = {
      code: this.newCode(), course, isPublic, host: player.id, started: false, finished: false,
      seed: randomInt(1, 2 ** 31 - 1), createdAt: this.now(), players: [],
    };
    this.rooms.set(room.code, room);
    this.addMember(room, player);
    return room;
  }

  quick(player, course = 'cliffside') {
    if (!COURSES[course]) return this.error(player.id, 'unknown course');
    const current = this.roomFor(player.id);
    if (current && current.isPublic && !current.started) return current;
    const open = [...this.rooms.values()].find((r) => r.isPublic && !r.started && r.course === course && r.players.length < MAX_PLAYERS);
    return open ? this.join(player, open.code) : this.create(player, course, true);
  }

  join(player, code) {
    const room = this.rooms.get(code);
    if (!room) return this.error(player.id, `no room ${code || '(blank)'}`);
    const member = room.players.find((p) => p.id === player.id);
    if (member) {
      // Back after a dropped connection: same seat, same card.
      member.connected = true;
      if (!room.finished) member.left = false;
      this.leaveOthers(player.id, code);
      this.roomOf.set(player.id, code);
      this.broadcast(room);
      return room;
    }
    if (room.started) return this.error(player.id, 'that round has already started');
    if (room.players.length >= MAX_PLAYERS) return this.error(player.id, 'that room is full');
    this.leave(player.id);
    this.addMember(room, player);
    return room;
  }

  leave(playerId) {
    const room = this.roomFor(playerId);
    if (!room) return;
    this.roomOf.delete(playerId);
    if (room.started) {
      const member = room.players.find((p) => p.id === playerId);
      member.connected = false;
      member.left = true;
    } else {
      room.players = room.players.filter((p) => p.id !== playerId);
    }
    const present = room.players.filter((p) => p.connected);
    if (present.length === 0) return this.close(room);
    if (room.host === playerId) room.host = present[0].id;
    this.broadcast(room);
    this.maybeFinish(room);
  }

  start(playerId) {
    const room = this.roomFor(playerId);
    if (!room) return this.error(playerId, 'not in a room');
    if (room.host !== playerId) return this.error(playerId, 'only the host can start');
    if (room.started) return room;
    if (room.players.length < MIN_PLAYERS_TO_START) return this.error(playerId, `waiting for another player`);
    room.started = true;
    room.startedAt = this.now();
    for (const p of room.players) if (p.connected) this.send(p.id, { type: 'start', code: room.code, seed: room.seed, course: room.course });
    this.broadcast(room);
    return room;
  }

  hole(playerId, hole, strokes) {
    const room = this.roomFor(playerId);
    if (!room || !room.started) return this.error(playerId, 'the round has not started');
    const holes = COURSES[room.course].pars.length;
    if (!Number.isInteger(hole) || hole < 0 || hole >= holes) return this.error(playerId, 'no such hole');
    if (!Number.isInteger(strokes) || strokes < 1 || strokes > MAX_STROKES_PER_HOLE) return this.error(playerId, 'bad score');
    const member = room.players.find((p) => p.id === playerId);
    if (member.strokes[hole] !== 0) return room; // a resend: the first score stands
    member.strokes[hole] = strokes;
    for (const p of room.players) if (p.connected) this.send(p.id, { type: 'hole', playerId, hole, strokes });
    this.maybeFinish(room);
    return room;
  }

  /** Closes rooms nobody has touched for a few hours. */
  sweep() {
    for (const room of [...this.rooms.values()]) if (this.now() - room.createdAt > ROOM_LIFETIME_MS) this.close(room);
  }

  roomFor(playerId) {
    const code = this.roomOf.get(playerId);
    return code ? this.rooms.get(code) : undefined;
  }

  // ----- private -----

  leaveOthers(playerId, code) {
    const current = this.roomFor(playerId);
    if (current && current.code !== code) this.leave(playerId);
  }

  addMember(room, player) {
    room.players.push({
      id: player.id, name: player.name, body: player.body, kit: player.kit, shirt: player.shirt,
      connected: true, left: false, strokes: new Array(COURSES[room.course].pars.length).fill(0),
    });
    this.roomOf.set(player.id, room.code);
    this.broadcast(room);
  }

  maybeFinish(room) {
    if (!room.started || room.finished) return;
    const finishing = room.players.filter((p) => !p.left);
    const complete = finishing.filter((p) => p.strokes.every((s) => s > 0));
    if (complete.length === 0 || complete.length < finishing.length) return;
    room.finished = true;
    const cards = complete.map((p) => ({ playerId: p.id, strokes: [...p.strokes], toPar: toPar(room.course, p.strokes) }));
    const results = matchResults(cards);
    for (const c of cards) c.result = results.get(c.playerId);
    this.broadcast(room);
    Promise.resolve(this.onFinished(room, cards)).catch((e) => console.error('recording an online round failed', e));
  }

  close(room) {
    this.rooms.delete(room.code);
    for (const p of room.players) if (this.roomOf.get(p.id) === room.code) this.roomOf.delete(p.id);
  }

  broadcast(room) {
    const message = this.describe(room);
    for (const p of room.players) if (p.connected) this.send(p.id, message);
  }

  /** The room as the app reads it (OnlineMessage in Unity: flat fields, arrays, no nulls). */
  describe(room) {
    return {
      type: 'room', code: room.code, course: room.course, host: room.host, seed: room.seed,
      started: room.started, finished: room.finished, isPublic: room.isPublic,
      players: room.players.map(({ id, name, body, kit, shirt, connected, strokes }) => ({ id, name, body, kit, shirt, connected, strokes })),
    };
  }

  error(playerId, error) {
    this.send(playerId, { type: 'error', error });
  }

  newCode() {
    for (;;) {
      let code = '';
      for (let i = 0; i < CODE_LENGTH; i++) code += CODE_LETTERS[randomInt(CODE_LETTERS.length)];
      if (!this.rooms.has(code)) return code;
    }
  }
}
