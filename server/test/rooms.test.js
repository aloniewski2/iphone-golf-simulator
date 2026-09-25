import assert from 'node:assert/strict';
import test from 'node:test';
import { RoomManager } from '../src/rooms.js';

function harness(options = {}) {
  const inbox = new Map();
  const finished = [];
  const rooms = new RoomManager({
    send: (id, m) => { if (!inbox.has(id)) inbox.set(id, []); inbox.get(id).push(m); },
    onFinished: (room, cards) => finished.push({ room, cards }),
    ...options,
  });
  const last = (id, type) => (inbox.get(id) ?? []).filter((m) => m.type === type).at(-1);
  return { rooms, inbox, finished, last };
}

const alex = { id: 'alex', name: 'Alex', body: 0, kit: 0, shirt: 0 };
const sam = { id: 'sam', name: 'Sam', body: 1, kit: 2, shirt: 3 };
const kim = { id: 'kim', name: 'Kim', body: 1, kit: 5, shirt: 1 };

test('a room is made with a code, joined with it, and started by the host', () => {
  const { rooms, last } = harness();
  const room = rooms.create(alex, 'cliffside');
  assert.match(room.code, /^[A-HJ-NP-Z]{4}$/);
  rooms.handle(sam, { type: 'join', code: room.code.toLowerCase() });
  assert.deepEqual(last('alex', 'room').players.map((p) => p.name), ['Alex', 'Sam']);

  rooms.handle(sam, { type: 'start' });
  assert.equal(last('sam', 'error').error, 'only the host can start');
  rooms.handle(alex, { type: 'start' });
  const start = last('sam', 'start');
  assert.equal(start.seed, room.seed, 'everyone gets the same seed, so the same wind');
  assert.equal(start.course, 'cliffside');

  rooms.handle(kim, { type: 'join', code: room.code });
  assert.equal(last('kim', 'error').error, 'that round has already started');
});

test('a room needs two players to start and holds four', () => {
  const { rooms, last } = harness();
  const room = rooms.create(alex);
  rooms.handle(alex, { type: 'start' });
  assert.equal(last('alex', 'error').error, 'waiting for another player');
  for (const id of ['b', 'c', 'd']) rooms.join({ id, name: id, body: 0, kit: 0, shirt: 0 }, room.code);
  rooms.join({ id: 'e', name: 'e', body: 0, kit: 0, shirt: 0 }, room.code);
  assert.equal(last('e', 'error').error, 'that room is full');
});

test('quick match fills an open public room before making a new one', () => {
  const { rooms } = harness();
  const first = rooms.quick(alex);
  const second = rooms.quick(sam);
  assert.equal(first.code, second.code);
  const privateRoom = rooms.create(kim);
  assert.notEqual(privateRoom.code, first.code);
  assert.equal(privateRoom.isPublic, false);
});

test('scores are shared, a resend cannot change them, and a full set records the round', () => {
  const { rooms, last, finished } = harness();
  const room = rooms.create(alex, 'cliffside');
  rooms.join(sam, room.code);
  rooms.start('alex');
  rooms.handle(alex, { type: 'hole', hole: 0, strokes: 3 });
  assert.deepEqual(last('sam', 'hole'), { type: 'hole', playerId: 'alex', hole: 0, strokes: 3 });
  rooms.handle(alex, { type: 'hole', hole: 0, strokes: 1 });
  assert.equal(last('sam', 'room').players[0].strokes[0], 3, 'the first score stands');
  rooms.handle(alex, { type: 'hole', hole: 7, strokes: 3 });
  assert.equal(last('alex', 'error').error, 'no such hole');

  for (const [h, s] of [[1, 3], [2, 5], [3, 4], [4, 3]]) rooms.hole('alex', h, s);
  assert.equal(finished.length, 0, 'waits for everyone');
  for (const [h, s] of [[0, 5], [1, 3], [2, 5], [3, 4], [4, 3]]) rooms.hole('sam', h, s);
  assert.equal(finished.length, 1);
  const cards = Object.fromEntries(finished[0].cards.map((c) => [c.playerId, c]));
  assert.equal(cards.alex.result, 'won');
  assert.equal(cards.sam.result, 'lost');
  assert.equal(cards.alex.toPar, -1);
  assert.ok(last('sam', 'room').finished);
});

test('a dropped phone keeps its seat and catches up when it rejoins', () => {
  const { rooms, last } = harness();
  const room = rooms.create(alex, 'cliffside-7');
  rooms.join(sam, room.code);
  rooms.start('alex');
  rooms.hole('alex', 0, 4);
  rooms.disconnected('sam');
  assert.equal(last('alex', 'room').players[1].connected, false);
  rooms.join(sam, room.code);
  const seen = last('sam', 'room');
  assert.equal(seen.players[1].connected, true);
  assert.deepEqual(seen.players[0].strokes, [4], 'the rejoining phone sees the score it missed');
});

test('a player who never comes back does not hold up the others', async () => {
  const { rooms, finished } = harness({ dropAfterMs: 10 });
  const room = rooms.create(alex, 'cliffside-7');
  rooms.join(sam, room.code);
  rooms.start('alex');
  rooms.disconnected('sam');
  rooms.hole('alex', 0, 4);
  assert.equal(finished.length, 0);
  await new Promise((r) => setTimeout(r, 30));
  assert.equal(finished.length, 1);
  assert.deepEqual(finished[0].cards.map((c) => c.playerId), ['alex']);
  assert.equal(finished[0].cards[0].result, null, 'alone at the end is a solo round');
});

test('the host passes on when the host leaves the lobby, and an empty room closes', () => {
  const { rooms, last } = harness();
  const room = rooms.create(alex);
  rooms.join(sam, room.code);
  rooms.leave('alex');
  assert.equal(last('sam', 'room').host, 'sam');
  rooms.leave('sam');
  assert.equal(rooms.rooms.size, 0);
});
