import assert from 'node:assert/strict';
import test from 'node:test';
import { lobby, startHub } from './helpers/harness.js';

const online = (id) => (m) => m.id === id && m.online === true;
const offline = (id) => (m) => m.id === id && m.online === false;

test('presence is mutual-only: A lists B but B does not list A => nobody sees anybody', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('A', { friends: ['B'] });
  const b = await hub.join('B', { friends: [] });
  await a.none('presence');
  await b.none('presence');

  // B adds A: now mutual, both are told.
  await b.say({ t: 'friends', ids: ['A'] });
  const seenByA = await a.next('presence', online('B'));
  assert.equal(seenByA.state, 'plaza');
  assert.equal(seenByA.name, 'B');
  assert.equal((await b.next('presence', online('A'))).name, 'A');
});

test('welcome is followed by one presence per online mutual friend', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  await hub.join('B', { friends: ['A', 'C'] });
  await hub.join('C', { friends: ['A', 'B'], presence: { state: 'lobby', lobby: lobby() } });
  const order = [];
  const a = hub.open();
  a.ws.on('message', (d) => order.push(JSON.parse(d.toString())));
  await a.hello({ id: 'A', friends: ['B', 'C', 'D'] });
  await a.next('presence', online('B'));
  const c = await a.next('presence', online('C'));
  assert.equal(c.state, 'lobby');
  assert.deepEqual(c.lobby, lobby());
  assert.equal(order[0].t, 'welcome');
  assert.deepEqual(order.slice(1).map((m) => m.t), ['presence', 'presence']);
});

test('online -> state/lobby changes -> offline transitions reach mutual friends', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('A', { friends: ['B'] });
  const b = await hub.join('B', { friends: ['A'] });
  await a.next('presence', online('B'));

  await b.say({ t: 'presence', state: 'lobby', lobby: lobby({ open: true }) });
  const m = await a.next('presence', online('B'));
  assert.equal(m.state, 'lobby');
  assert.deepEqual(m.lobby, lobby({ open: true }));

  await b.say({ t: 'presence', state: 'match', lobby: null });
  const m2 = await a.next('presence', online('B'));
  assert.equal(m2.state, 'match');
  assert.equal(m2.lobby, null);

  await b.close();
  await a.next('presence', offline('B'));
});

test('a heartbeat that repeats the same presence is not re-broadcast', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('A', { friends: ['B'] });
  const b = await hub.join('B', { friends: ['A'] });
  await a.next('presence', online('B'));
  await b.say({ t: 'presence', state: 'plaza' });
  await b.say({ t: 'presence', state: 'plaza' });
  await a.none('presence');
});

test('bye shows the player offline at once and closes the socket', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('A', { friends: ['B'] });
  const b = await hub.join('B', { friends: ['A'] });
  await a.next('presence', online('B'));
  await b.say({ t: 'bye' });
  await a.next('presence', offline('B'));
  assert.equal(await b.closed, 1000);
  assert.equal(hub.hub.onlineCount, 1);
});

test('invisible: friends see the player as offline, the player still sees them', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('A', { friends: ['B'] });
  const b = await hub.join('B', { friends: ['A'] });
  await a.next('presence', online('B'));
  await b.next('presence', online('A'));

  await b.say({ t: 'presence', state: 'plaza', invisible: true });
  await a.next('presence', offline('B'));
  await b.none('presence', offline('A'));

  await b.say({ t: 'presence', state: 'plaza', invisible: false });
  await a.next('presence', online('B'));

  // joining invisible: never shown at all
  const c = await hub.join('C', { friends: ['A'], presence: { state: 'plaza', invisible: true } });
  await a.say({ t: 'friends', ids: ['B', 'C'] });
  await c.say({ t: 'friends', ids: ['A'] });
  await a.none('presence', online('C'));
});

test('friends update: removing a friend hides both sides, adding shows them', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('A', { friends: ['B'] });
  const b = await hub.join('B', { friends: ['A'] });
  await a.next('presence', online('B'));
  await b.next('presence', online('A'));

  await a.say({ t: 'friends', ids: [] });
  await a.next('presence', offline('B'));
  await b.next('presence', offline('A'));

  await a.say({ t: 'friends', ids: ['B'] });
  await a.next('presence', online('B'));
  await b.next('presence', online('A'));

  await a.say({ t: 'friends', ids: 'nope' });
  assert.equal((await a.next('error')).code, 'bad_message');
});

test('blocks hide presence in both directions and survive a reconnect; unblock restores', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('A', { friends: ['B'] });
  const b = await hub.join('B', { friends: ['A'] });
  await a.next('presence', online('B'));
  await b.next('presence', online('A'));

  await a.say({ t: 'block', id: 'B' });
  await a.next('presence', offline('B'));
  await b.next('presence', offline('A'));

  // B reconnects: still hidden (the block is stored, not tied to a socket).
  await b.close();
  const b2 = await hub.join('B', { friends: ['A'] });
  await a.none('presence', online('B'));
  await b2.none('presence', online('A'));

  await a.say({ t: 'unblock', id: 'B' });
  await a.next('presence', online('B'));
  await b2.next('presence', online('A'));

  // the other direction: B blocks A
  await b2.say({ t: 'block', id: 'A' });
  await a.next('presence', offline('B'));
  await b2.next('presence', offline('A'));

  await a.say({ t: 'block', id: 'A' });
  assert.equal((await a.next('error')).code, 'bad_message');
});

test('bad presence messages are refused and change nothing', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('A', { friends: ['B'] });
  const b = await hub.join('B', { friends: ['A'] });
  await a.next('presence', online('B'));
  for (const bad of [{ state: 'nap' }, { state: 'lobby', lobby: { code: 'abc' } }, { state: 'plaza', invisible: 'yes' }, { state: 'lobby', lobby: { ...lobby(), group: -1 } }]) {
    await b.say({ t: 'presence', ...bad });
    assert.equal((await b.next('error')).code, 'bad_message');
  }
  await a.none('presence');
});

test('names are cleaned: control characters removed, length capped, empty becomes Player', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('A', { friends: ['B', 'C'] });
  await hub.join('B', { friends: ['A'], name: '  Bo\u0000b‮  ' });
  await hub.join('C', { friends: ['A'], name: 'x'.repeat(100) });
  assert.equal((await a.next('presence', online('B'))).name, 'Bob');
  assert.equal((await a.next('presence', online('C'))).name.length, 40);
  const d = await hub.join('D', { friends: ['A'], name: '   ' });
  await a.say({ t: 'friends', ids: ['B', 'C', 'D'] });
  await d.say({ t: 'friends', ids: ['A'] });
  assert.equal((await a.next('presence', online('D'))).name, 'Player');
});
