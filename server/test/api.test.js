import assert from 'node:assert/strict';
import { once } from 'node:events';
import test from 'node:test';
import WebSocket from 'ws';
import { createServer } from '../src/server.js';
import { MemoryStore } from '../src/store/memory.js';

async function start() {
  const { server } = createServer({ store: new MemoryStore(), log: { error() {} } });
  server.listen(0);
  await once(server, 'listening');
  const base = `http://127.0.0.1:${server.address().port}`;
  const call = async (method, path, { token, body } = {}) => {
    const res = await fetch(base + path, {
      method,
      headers: { 'content-type': 'application/json', ...(token ? { authorization: `Bearer ${token}` } : {}) },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    return { status: res.status, json: await res.json() };
  };
  return { server, base, call };
}

/** A socket that queues what the server says so a test can wait for a message type. */
function connect(base) {
  const ws = new WebSocket(base.replace('http', 'ws') + '/v1/play');
  const queue = [];
  const waiters = [];
  ws.on('message', (data) => {
    const m = JSON.parse(data.toString());
    const i = waiters.findIndex((w) => w.type === m.type && w.match(m));
    if (i >= 0) waiters.splice(i, 1)[0].resolve(m); else queue.push(m);
  });
  ws.next = (type, match = () => true) => {
    const i = queue.findIndex((m) => m.type === type && match(m));
    if (i >= 0) return Promise.resolve(queue.splice(i, 1)[0]);
    return new Promise((resolve) => waiters.push({ type, match, resolve }));
  };
  ws.say = (m) => ws.send(JSON.stringify(m));
  return ws;
}

test('sign up, read and edit the profile, record a round, see the leaderboard', async (t) => {
  const { server, call } = await start();
  t.after(() => server.close());

  const bad = await call('POST', '/v1/players', { body: { name: '  ', body: 0, kit: 0, shirt: 0 } });
  assert.equal(bad.status, 400);

  const signup = await call('POST', '/v1/players', { body: { name: 'Alex', body: 1, kit: 3, shirt: 2 } });
  assert.equal(signup.status, 201);
  const { token, player } = signup.json;
  assert.ok(token.length >= 40);
  assert.deepEqual([player.name, player.body, player.kit, player.shirt], ['Alex', 1, 3, 2]);

  assert.equal((await call('GET', '/v1/me')).status, 401);
  assert.equal((await call('GET', '/v1/me', { token: 'nope' })).status, 401);

  const edited = await call('PATCH', '/v1/me', { token, body: { name: 'Alexandra', shirt: 5 } });
  assert.deepEqual([edited.json.player.name, edited.json.player.body, edited.json.player.kit, edited.json.player.shirt], ['Alexandra', 1, 3, 5]);
  assert.equal((await call('PATCH', '/v1/me', { token, body: { kit: 9 } })).status, 400);

  assert.equal((await call('POST', '/v1/rounds', { token, body: { course: 'cliffside-7', strokes: [0] } })).status, 400);
  const round = await call('POST', '/v1/rounds', { token, body: { course: 'cliffside-7', strokes: [3] } });
  assert.equal(round.status, 201);
  assert.equal(round.json.stats.birdies, 1);
  assert.equal(round.json.stats.bestToPar, -1);
  assert.equal(round.json.stats.hasBest, true);
  assert.equal(signup.json.player.stats.hasBest, false, 'no best before a round, and no null for the app');
  assert.equal(signup.json.player.stats.bestToPar, 0);

  const me = await call('GET', '/v1/me', { token });
  assert.equal(me.json.recentRounds.length, 1);
  const board = await call('GET', '/v1/leaderboard?course=cliffside-7');
  assert.deepEqual(board.json.entries, [{ playerId: player.id, name: 'Alexandra', bestToPar: -1 }]);
  const pub = await call('GET', `/v1/players/${player.id}`);
  assert.equal(pub.json.player.name, 'Alexandra');
  assert.equal(pub.json.player.token, undefined);
  assert.equal((await call('GET', '/v1/nothing')).status, 404);
});

test('two phones play an online round and both get it on their record', async (t) => {
  const { server, base, call } = await start();
  const sockets = [];
  t.after(() => { for (const s of sockets) s.terminate(); server.close(); });

  const a = (await call('POST', '/v1/players', { body: { name: 'Alex', body: 0, kit: 0, shirt: 0 } })).json;
  const b = (await call('POST', '/v1/players', { body: { name: 'Sam', body: 1, kit: 2, shirt: 1 } })).json;

  const wa = connect(base); sockets.push(wa);
  const wb = connect(base); sockets.push(wb);
  await Promise.all([once(wa, 'open'), once(wb, 'open')]);
  wa.say({ type: 'hello', token: a.token });
  wb.say({ type: 'hello', token: b.token });
  assert.equal((await wa.next('welcome')).playerId, a.player.id);
  await wb.next('welcome');

  wa.say({ type: 'create', course: 'cliffside-7' });
  const lobby = await wa.next('room');
  wb.say({ type: 'join', code: lobby.code });
  await wa.next('room', (m) => m.players.length === 2);
  wa.say({ type: 'start' });
  const [sa, sb] = await Promise.all([wa.next('start'), wb.next('start')]);
  assert.equal(sa.seed, sb.seed);

  wa.say({ type: 'hole', hole: 0, strokes: 3 });
  assert.deepEqual(await wb.next('hole'), { type: 'hole', playerId: a.player.id, hole: 0, strokes: 3 });
  wb.say({ type: 'hole', hole: 0, strokes: 5 });
  await wa.next('room', (m) => m.finished);

  // Recording runs just after the final broadcast.
  await new Promise((r) => setTimeout(r, 50));
  const meA = await call('GET', '/v1/me', { token: a.token });
  const meB = await call('GET', '/v1/me', { token: b.token });
  assert.equal(meA.json.player.stats.matchesWon, 1);
  assert.equal(meB.json.player.stats.matchesLost, 1);
  assert.equal(meA.json.recentRounds[0].result, 'won');
});

test('a socket that does not sign in is closed', async (t) => {
  const { server, base } = await start();
  t.after(() => server.close());
  const ws = connect(base);
  await once(ws, 'open');
  ws.say({ type: 'hello', token: 'wrong' });
  const [code] = await once(ws, 'close');
  assert.equal(code, 4003);
});
