import assert from 'node:assert/strict';
import test from 'node:test';
import { CODE, lobby, startHub } from './helpers/harness.js';

test('resolve finds a live lobby by code (case-insensitive) and returns only host, sport, group, capacity', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  await hub.join('H', { name: 'Sam', presence: { state: 'lobby', lobby: lobby({ sport: 'golf', capacity: 4, group: 99, size: 2 }) } });
  const stranger = await hub.join('S'); // not a friend: codes are the credential
  await stranger.say({ t: 'resolve', code: CODE.toLowerCase() });
  assert.deepEqual(await stranger.next('resolved'), {
    t: 'resolved', code: CODE, found: true, from: { id: 'H', name: 'Sam' }, sport: 'golf', group: 99, capacity: 4,
  });
});

test('resolve: unknown code, closed lobby and a hidden (invisible) host', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const host = await hub.join('H', { name: 'Sam', presence: { state: 'lobby', lobby: lobby(), invisible: true } });
  const s = await hub.join('S');
  await s.say({ t: 'resolve', code: 'ZZZZZZ' });
  assert.deepEqual(await s.next('resolved'), { t: 'resolved', code: 'ZZZZZZ', found: false });
  await s.say({ t: 'resolve', code: CODE });
  assert.equal((await s.next('resolved')).found, true, 'a code is a code even if the host appears offline');
  await host.say({ t: 'presence', state: 'plaza', lobby: null });
  await s.say({ t: 'resolve', code: CODE });
  assert.equal((await s.next('resolved')).found, false);
  await s.say({ t: 'resolve', code: 5 });
  assert.equal((await s.next('error')).code, 'bad_message');
});

test('resolve is rate limited (20 per minute) so codes cannot be guessed quickly', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const s = await hub.join('S');
  for (let i = 0; i < 20; i++) await s.say({ t: 'resolve', code: 'ZZZZZZ' });
  await s.say({ t: 'resolve', code: 'ZZZZZZ' });
  const e = await s.next('error');
  assert.equal(e.code, 'rate_limited');
});
