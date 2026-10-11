import assert from 'node:assert/strict';
import test from 'node:test';
import { startHub } from './helpers/harness.js';

test('the first message must be hello: anything else gets unauthorized and close 4401', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  for (const first of [{ t: 'presence', state: 'plaza' }, { t: 'ping' }, 'not json', '[1,2]', { nope: 1 }]) {
    const c = hub.open();
    await c.say(first);
    assert.equal((await c.next('error')).code, 'unauthorized');
    assert.equal(await c.closed, 4401);
  }
});

test('a socket that never says hello is closed with 4401', async (t) => {
  const hub = await startHub({ helloTimeoutMs: 100 });
  t.after(() => hub.stop());
  const c = hub.open();
  assert.equal(await c.closed, 4401);
});

test('dev auth is rejected when HUB_DEV_AUTH is not set', async (t) => {
  const hub = await startHub({ devAuth: false });
  t.after(() => hub.stop());
  const c = hub.open();
  await c.say({ t: 'hello', v: 1, auth: { dev: 'alice' }, name: 'Alice', friends: [], presence: { state: 'plaza' } });
  const err = await c.next('error');
  assert.equal(err.code, 'unauthorized');
  assert.equal(await c.closed, 4401);
});

test('hello: welcome carries you, serverTime and v; bad hellos are refused', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const before = Date.now();
  const a = hub.open();
  const w = await a.hello({ id: 'alice' });
  assert.equal(w.you, 'alice');
  assert.equal(w.v, 1);
  assert.ok(w.serverTime >= before && w.serverTime <= Date.now());

  const cases = [
    { v: 2 },                                   // unsupported protocol version
    { friends: 'bob' },                         // friends not an array
    { friends: [1] },
    { presence: { state: 'sleeping' } },
    { presence: { state: 'lobby', lobby: { code: 'bad' } } },
    { push: { token: 'zz', env: 'production' } },
  ];
  for (const c of cases) {
    const x = hub.open();
    await x.say({ t: 'hello', v: 1, auth: { dev: 'x' }, name: 'X', friends: [], presence: { state: 'plaza' }, ...c });
    assert.equal((await x.next('error')).code, 'bad_message', JSON.stringify(c));
    assert.equal(await x.closed, 4401);
  }
});

test('after hello: ping gets pong; garbage gets bad_message without closing; a second hello is refused', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('alice');
  await a.say({ t: 'ping' });
  await a.next('pong');
  await a.say('{{{');
  assert.equal((await a.next('error')).code, 'bad_message');
  await a.say({ t: 'dance' });
  const e = await a.next('error');
  assert.equal(e.code, 'bad_message');
  assert.equal(e.ref, 'dance');
  await a.say({ t: 'hello', v: 1, auth: { dev: 'alice' }, name: 'A', friends: [] });
  assert.equal((await a.next('error')).code, 'bad_message');
  await a.say({ t: 'ping' });
  await a.next('pong');
});

test('a second socket for the same player replaces the first (4409) and does not make the player look offline', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const bob = await hub.join('bob', { friends: ['alice'] });
  const first = await hub.join('alice', { friends: ['bob'] });
  await bob.next('presence', (m) => m.id === 'alice' && m.online);
  const second = await hub.join('alice', { friends: ['bob'] });
  assert.equal(await first.closed, 4409);
  await bob.none('presence', (m) => m.id === 'alice' && !m.online);
  assert.equal(hub.hub.onlineCount, 2);
  await second.close();
  await bob.next('presence', (m) => m.id === 'alice' && !m.online);
});

test('a silent connection is dropped after the idle limit and friends see it go offline', async (t) => {
  const hub = await startHub({ idleMs: 200 });
  t.after(() => hub.stop());
  const bob = await hub.join('bob', { friends: ['alice'] });
  const alice = await hub.join('alice', { friends: ['bob'] });
  await bob.next('presence', (m) => m.online);
  // bob keeps talking (heartbeat), alice does not
  const beat = setInterval(() => bob.say({ t: 'ping' }), 60);
  t.after(() => clearInterval(beat));
  await alice.closed;
  await bob.next('presence', (m) => m.id === 'alice' && !m.online);
});

test('rate limit: more than 30 messages in 10 s closes with 4429', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('alice'); // hello is message 1
  for (let i = 0; i < 29; i++) await a.say({ t: 'ping' }); // up to 30 messages total: fine
  for (let i = 0; i < 29; i++) await a.next('pong');
  await a.say({ t: 'ping' }); // 31st
  assert.equal((await a.next('error')).code, 'rate_limited');
  assert.equal(await a.closed, 4429);
});

test('an oversize frame is refused by the socket layer', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const a = await hub.join('alice');
  await a.say(JSON.stringify({ t: 'ping', pad: 'x'.repeat(20_000) }));
  await a.closed;
});
