// Party rooms: the relay a party's phones talk through (the lobby, the Plaza, the match). PROTOCOL.md "Party rooms".
import assert from 'node:assert/strict';
import test from 'node:test';
import { CODE, sleep, startHub } from './helpers/harness.js';

const inv = (to, o = {}) => ({ t: 'invite', to, sport: 'tennis', lobbyCode: CODE, group: 0, capacity: 2, ...o });
const peers = (m) => m.peers.map((p) => p.id).sort();

async function friends(hub, ids) {
  const out = {};
  for (const id of ids) out[id] = await hub.join(id, { name: id + ' name', friends: ids.filter((x) => x !== id) });
  return out;
}

test('host opens a room, the invited friend joins it, and frames go both ways (never to the sender)', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const { A, B } = await friends(hub, ['A', 'B']);
  await A.say({ t: 'roomOpen', room: CODE });
  assert.deepEqual(peers(await A.next('roomPeers')), ['A']);
  await A.say(inv('B'));
  await B.next('invite');
  await B.say({ t: 'roomJoin', room: CODE });
  const seenByB = await B.next('roomPeers');
  assert.equal(seenByB.room, CODE);
  assert.deepEqual(peers(seenByB), ['A', 'B']);
  assert.deepEqual(seenByB.peers.find((p) => p.id === 'A'), { id: 'A', name: 'A name' });
  assert.deepEqual(peers(await A.next('roomPeers')), ['A', 'B']);

  await A.say({ t: 'rd', room: CODE, d: '{"kind":"lobby"}' });
  const got = await B.next('rd');
  assert.deepEqual(got, { t: 'rd', room: CODE, from: 'A', d: '{"kind":"lobby"}' });
  await B.say({ t: 'rd', room: CODE, to: ['A'], d: 'hello' });
  assert.equal((await A.next('rd')).d, 'hello');
  await A.none('rd');
  await B.none('rd');
});

test('nobody else gets in: an uninvited player is room_denied, a missing room is room_gone, a taken code is room_taken', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const { A, C } = await friends(hub, ['A', 'C']);
  await A.say({ t: 'roomOpen', room: CODE });
  await A.next('roomPeers');
  await C.say({ t: 'roomJoin', room: CODE });
  assert.equal((await C.next('error')).code, 'room_denied');
  await C.say({ t: 'roomOpen', room: CODE });
  assert.equal((await C.next('error')).code, 'room_taken');
  await C.say({ t: 'roomJoin', room: 'ZZZZZZ' });
  assert.equal((await C.next('error')).code, 'room_gone');
  // and a non-member cannot push frames into it
  await C.say({ t: 'rd', room: CODE, d: 'x' });
  assert.equal((await C.next('error')).code, 'room_gone');
  await A.none('rd');
  await C.say({ t: 'roomJoin', room: 'bad' });
  assert.equal((await C.next('error')).code, 'bad_message');
});

test('a resolved invite code lets that player in (a share link)', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const A = await hub.join('A', { presence: { state: 'lobby', lobby: { code: CODE, sport: 'golf', size: 1, capacity: 4, open: false, group: 0 } } });
  const D = await hub.join('D');
  await A.say({ t: 'roomOpen', room: CODE });
  await A.next('roomPeers');
  await D.say({ t: 'resolve', code: CODE });
  assert.equal((await D.next('resolved')).found, true);
  await D.say({ t: 'roomJoin', room: CODE });
  assert.deepEqual(peers(await D.next('roomPeers')), ['A', 'D']);
});

test('a member who drops leaves the room (the others are told) and gets back in after reconnecting', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const { A, B } = await friends(hub, ['A', 'B']);
  await A.say({ t: 'roomOpen', room: CODE }); await A.next('roomPeers');
  await A.say(inv('B')); await B.next('invite');
  await B.say({ t: 'roomJoin', room: CODE }); await B.next('roomPeers'); await A.next('roomPeers');
  B.ws.terminate();
  assert.deepEqual(peers(await A.next('roomPeers')), ['A']);
  const B2 = await hub.join('B', { friends: ['A'] });
  await B2.say({ t: 'roomJoin', room: CODE });
  assert.deepEqual(peers(await B2.next('roomPeers')), ['A', 'B']);
  assert.deepEqual(peers(await A.next('roomPeers')), ['A', 'B']);
  await A.say({ t: 'rd', room: CODE, d: 'again' });
  assert.equal((await B2.next('rd')).d, 'again');
});

test('leaving: the leaver gets an empty list, the rest see them go; an empty room is gone after the grace', async (t) => {
  const hub = await startHub({ roomGraceMs: 60, idleMs: 2000 });   // (the sweep runs every idleMs / 2)
  t.after(() => hub.stop());
  const { A, B } = await friends(hub, ['A', 'B']);
  await A.say({ t: 'roomOpen', room: CODE }); await A.next('roomPeers');
  await A.say(inv('B')); await B.next('invite');
  await B.say({ t: 'roomJoin', room: CODE }); await B.next('roomPeers'); await A.next('roomPeers');
  await B.say({ t: 'roomLeave', room: CODE });
  assert.deepEqual((await B.next('roomPeers')).peers, []);
  assert.deepEqual(peers(await A.next('roomPeers')), ['A']);
  await A.say({ t: 'roomLeave', room: CODE });
  await A.next('roomPeers');
  assert.equal(hub.hub.roomCount, 1);
  await sleep(1300);
  assert.equal(hub.hub.roomCount, 0);
  await B.say({ t: 'roomJoin', room: CODE });
  assert.equal((await B.next('error')).code, 'room_gone');
});

test('relay frames have their own high limit: 150 in a second is fine where 31 other messages would close the socket', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const { A, B } = await friends(hub, ['A', 'B']);
  await A.say({ t: 'roomOpen', room: CODE }); await A.next('roomPeers');
  await A.say(inv('B')); await B.next('invite');
  await B.say({ t: 'roomJoin', room: CODE }); await B.next('roomPeers');
  for (let i = 0; i < 150; i++) await A.say({ t: 'rd', room: CODE, d: String(i) });
  const seen = [];
  for (let i = 0; i < 150; i++) seen.push((await B.next('rd')).d);
  assert.deepEqual(seen, Array.from({ length: 150 }, (_, i) => String(i)), 'every frame, in order');
  assert.equal(A.closeCode, undefined);
});

test('a big frame (a 60 KB game packet) goes through; over the cap is refused', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const { A, B } = await friends(hub, ['A', 'B']);
  await A.say({ t: 'roomOpen', room: CODE }); await A.next('roomPeers');
  await A.say(inv('B')); await B.next('invite');
  await B.say({ t: 'roomJoin', room: CODE }); await B.next('roomPeers');
  const big = 'x'.repeat(60_000);
  await A.say({ t: 'rd', room: CODE, d: big });
  assert.equal((await B.next('rd')).d.length, 60_000);
  await A.say({ t: 'rd', room: CODE, d: 'y'.repeat(140 * 1024) });
  assert.equal((await A.next('error')).code, 'bad_message');
});
