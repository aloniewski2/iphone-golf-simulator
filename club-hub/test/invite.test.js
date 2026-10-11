import assert from 'node:assert/strict';
import test from 'node:test';
import { CODE, lobby, sleep, startHub } from './helpers/harness.js';

const inv = (to, o = {}) => ({ t: 'invite', to, sport: 'tennis', lobbyCode: CODE, group: 1234, capacity: 2, ...o });
const status = (s, id) => (m) => m.status === s && (id === undefined || m.inviteID === id);

/** Mutual friends A and B, both online. */
async function pair(hub, a = {}, b = {}) {
  const A = await hub.join('A', { name: 'Sam', friends: ['B'], ...a });
  const B = await hub.join('B', { name: 'Bea', friends: ['A'], ...b });
  await A.next('presence', (m) => m.id === 'B');
  await B.next('presence', (m) => m.id === 'A');
  return [A, B];
}

test('invite is delivered to the friend and the sender is told (with the receiver id)', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  const before = Date.now();
  await A.say(inv('B'));
  const got = await B.next('invite');
  assert.match(got.inviteID, /^[0-9a-f-]{36}$/);
  assert.deepEqual(got.from, { id: 'A', name: 'Sam' });
  assert.deepEqual([got.sport, got.lobbyCode, got.group, got.capacity], ['tennis', CODE, 1234, 2]);
  assert.ok(got.expiresAt >= before + 119_000 && got.expiresAt <= Date.now() + 121_000, 'expires about 120 s out');
  const up = await A.next('inviteUpdate');
  assert.deepEqual([up.inviteID, up.to, up.status], [got.inviteID, 'B', 'delivered']);
});

test('accept and decline reach the sender; the invite is then gone', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);

  await A.say(inv('B'));
  const one = await B.next('invite');
  await A.next('inviteUpdate', status('delivered'));
  await B.say({ t: 'inviteResponse', inviteID: one.inviteID, accept: true });
  const acc = await A.next('inviteUpdate', status('accepted'));
  assert.deepEqual([acc.inviteID, acc.to], [one.inviteID, 'B']);

  // answering twice is an unknown invite
  await B.say({ t: 'inviteResponse', inviteID: one.inviteID, accept: false });
  const err = await B.next('error');
  assert.deepEqual([err.code, err.ref], ['bad_message', one.inviteID]);

  await A.say(inv('B'));
  const two = await B.next('invite');
  await B.say({ t: 'inviteResponse', inviteID: two.inviteID, accept: false });
  const dec = await A.next('inviteUpdate', status('declined'));
  assert.deepEqual([dec.inviteID, dec.to], [two.inviteID, 'B']);
});

test('only the invited player may respond, only the sender may cancel', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await A.say(inv('B'));
  const got = await B.next('invite');
  await A.say({ t: 'inviteResponse', inviteID: got.inviteID, accept: true });
  assert.equal((await A.next('error')).code, 'bad_message');
  await B.say({ t: 'inviteCancel', inviteID: got.inviteID });
  assert.equal((await B.next('error')).code, 'bad_message');
  await B.say({ t: 'inviteResponse', inviteID: got.inviteID, accept: 'yes' });
  assert.equal((await B.next('error')).code, 'bad_message');
  await B.say({ t: 'inviteResponse', inviteID: got.inviteID, accept: true });
  await A.next('inviteUpdate', status('accepted'));
});

test('an invite expires: both sides get expired, and a late answer is bad_message with ref', async (t) => {
  const hub = await startHub({ inviteTtlMs: 150 });
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await A.say(inv('B'));
  const got = await B.next('invite');
  const exp = await A.next('inviteUpdate', status('expired'));
  assert.deepEqual([exp.inviteID, exp.to], [got.inviteID, 'B']);
  assert.deepEqual(await B.next('inviteGone'), { t: 'inviteGone', inviteID: got.inviteID, status: 'expired' });
  await B.say({ t: 'inviteResponse', inviteID: got.inviteID, accept: true });
  const err = await B.next('error');
  assert.deepEqual([err.code, err.ref], ['bad_message', got.inviteID]);
  await A.none('inviteUpdate', status('accepted'));
});

test('the default lifetime is 120 s', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  assert.equal(hub.hub.inviteTtlMs, 120_000);
});

test('sender cancel: receiver gets inviteGone cancelled, sender gets cancelled', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await A.say(inv('B'));
  const got = await B.next('invite');
  await A.next('inviteUpdate', status('delivered'));
  await A.say({ t: 'inviteCancel', inviteID: got.inviteID });
  assert.deepEqual(await B.next('inviteGone'), { t: 'inviteGone', inviteID: got.inviteID, status: 'cancelled' });
  const up = await A.next('inviteUpdate', status('cancelled'));
  assert.equal(up.to, 'B');
  await A.say({ t: 'inviteCancel', inviteID: got.inviteID });
  assert.equal((await A.next('error')).code, 'bad_message');
});

test('a newer invite from the same sender to the same receiver replaces the older one', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await A.say(inv('B'));
  const first = await B.next('invite');
  await A.next('inviteUpdate', status('delivered'));
  await A.say(inv('B', { lobbyCode: 'GHJKLM' }));
  assert.deepEqual(await B.next('inviteGone'), { t: 'inviteGone', inviteID: first.inviteID, status: 'cancelled' });
  const cancelled = await A.next('inviteUpdate', status('cancelled', first.inviteID));
  assert.equal(cancelled.to, 'B');
  const second = await B.next('invite');
  assert.notEqual(second.inviteID, first.inviteID);
  assert.equal(second.lobbyCode, 'GHJKLM');
  await A.next('inviteUpdate', status('delivered', second.inviteID));
});

test('the sender going offline cancels their pending invites (bye and dropped socket)', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await A.say(inv('B'));
  const one = await B.next('invite');
  await A.say({ t: 'bye' });
  assert.deepEqual(await B.next('inviteGone'), { t: 'inviteGone', inviteID: one.inviteID, status: 'cancelled' });

  const A2 = await hub.join('A', { name: 'Sam', friends: ['B'] });
  await B.next('presence', (m) => m.id === 'A' && m.online);
  await A2.say(inv('B'));
  const two = await B.next('invite');
  A2.ws.terminate();
  assert.deepEqual(await B.next('inviteGone', (m) => m.inviteID === two.inviteID), { t: 'inviteGone', inviteID: two.inviteID, status: 'cancelled' });
});

test('the sender changing lobby code cancels invites for the old lobby, not for the new one', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await A.say({ t: 'presence', state: 'lobby', lobby: lobby() });
  await A.say(inv('B'));
  const one = await B.next('invite');

  await A.say({ t: 'presence', state: 'lobby', lobby: lobby({ code: 'GHJKLM' }) });
  assert.deepEqual(await B.next('inviteGone'), { t: 'inviteGone', inviteID: one.inviteID, status: 'cancelled' });

  // an invite already sent for the new code survives the presence update that catches up
  await A.say(inv('B', { lobbyCode: 'NPQRST' }));
  const two = await B.next('invite');
  await A.say({ t: 'presence', state: 'lobby', lobby: lobby({ code: 'NPQRST' }) });
  await B.none('inviteGone', (m) => m.inviteID === two.inviteID);

  // closing the lobby also cancels
  await A.say({ t: 'presence', state: 'plaza', lobby: null });
  assert.equal((await B.next('inviteGone', (m) => m.inviteID === two.inviteID)).status, 'cancelled');
});

test('at most 3 pending invites to one player; a replacement does not count twice', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const B = await hub.join('B', { friends: ['S1', 'S2', 'S3', 'S4'] });
  const senders = [];
  for (const id of ['S1', 'S2', 'S3', 'S4']) senders.push(await hub.join(id, { friends: ['B'] }));
  for (const s of senders.slice(0, 3)) {
    await s.say(inv('B'));
    await s.next('inviteUpdate', status('delivered'));
  }
  await senders[3].say(inv('B'));
  const err = await senders[3].next('error');
  assert.equal(err.code, 'too_many_pending');
  // S1 re-inviting replaces its own, so it is not "a fourth"
  await senders[0].say(inv('B', { lobbyCode: 'GHJKLM' }));
  await senders[0].next('inviteUpdate', status('delivered'));
  await senders[3].none('inviteUpdate');
  // once one is answered, S4 fits
  const s2 = await B.next('invite', (m) => m.from.id === 'S2');
  await B.say({ t: 'inviteResponse', inviteID: s2.inviteID, accept: false });
  await senders[3].say(inv('B'));
  await senders[3].next('inviteUpdate', status('delivered'));
});

test('not_friends: not in my list, not in theirs, or they blocked me; blocked: I blocked them', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const A = await hub.join('A', { friends: ['B', 'C'] });
  const B = await hub.join('B', { friends: [] }); // does not list A
  const C = await hub.join('C', { friends: ['A'] });

  await A.say(inv('Z'));
  assert.equal((await A.next('error')).code, 'not_friends');
  await A.say(inv('B'));
  assert.equal((await A.next('error')).code, 'not_friends');
  await B.none('invite');

  await C.say({ t: 'block', id: 'A' });
  await A.say(inv('C'));
  assert.equal((await A.next('error')).code, 'not_friends', 'being blocked looks like not-friends');
  await C.say({ t: 'unblock', id: 'A' });
  await A.say(inv('C'));
  await C.next('invite');

  await A.say({ t: 'block', id: 'C' });
  await C.next('inviteGone');
  await A.say(inv('C'));
  const e = await A.next('error');
  assert.equal(e.code, 'blocked');
});

test('an invite to a player who does not list me, even while offline, is not_friends', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const A = await hub.join('A', { friends: ['B'] });
  const B = await hub.join('B', { friends: [] });
  await B.close();
  await sleep(50);
  await A.say(inv('B'));
  assert.equal((await A.next('error')).code, 'not_friends');
});

test('malformed invites are bad_message and send nothing', async (t) => {
  const hub = await startHub({ limits: { inviteMax: 100 } });
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  for (const bad of [
    inv('B', { sport: 'chess' }), inv('B', { lobbyCode: 'abc' }), inv('B', { lobbyCode: 'AB0DEF' }), inv('B', { group: -1 }),
    inv('B', { group: 1.5 }), inv('B', { group: 2 ** 32 }), inv('B', { capacity: 0 }), inv('B', { capacity: 'two' }),
    inv(42), inv('A'), { t: 'invite' },
  ]) {
    await A.say(bad);
    assert.equal((await A.next('error')).code, 'bad_message', JSON.stringify(bad));
  }
  await B.none('invite');
});

test('rate limit: the 11th invite in a minute is rate_limited (connection stays open)', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A] = await pair(hub);
  for (let i = 0; i < 10; i++) await A.say(inv('B'));
  await A.say(inv('B'));
  const err = await A.next('error', (m) => m.code === 'rate_limited');
  assert.equal(err.code, 'rate_limited');
  await A.say({ t: 'ping' });
  await A.next('pong');
  assert.equal(A.ws.readyState, A.ws.OPEN);
});

test('do not disturb: the sender gets busy and nothing is delivered', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub, {}, { presence: { state: 'dnd' } });
  await A.say(inv('B'));
  const up = await A.next('inviteUpdate');
  assert.deepEqual([up.status, up.to], ['busy', 'B']);
  await B.none('invite');
});

test('a receiver in a match still gets the invite delivered (the app holds it); invisible receivers too', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await B.say({ t: 'presence', state: 'match' });
  await A.say(inv('B'));
  await B.next('invite');
  assert.equal((await A.next('inviteUpdate')).status, 'delivered');

  await B.say({ t: 'presence', state: 'plaza', invisible: true });
  await A.next('presence', (m) => m.online === false);
  await A.say(inv('B', { lobbyCode: 'GHJKLM' }));
  await B.next('invite', (m) => m.lobbyCode === 'GHJKLM');
});

test('receiver offline with no push token: unavailable, and nothing is kept', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const A = await hub.join('A', { friends: ['B'] });
  const B = await hub.join('B', { friends: ['A'] });
  await B.close();
  await sleep(50);
  await A.say(inv('B'));
  const up = await A.next('inviteUpdate', status('unavailable'));
  assert.equal(up.to, 'B');
  const B2 = await hub.join('B', { friends: ['A'] });
  await B2.none('invite');
});

test('"away" for less than the push delay still counts as live: invite is delivered over the socket', async (t) => {
  const hub = await startHub({ awayPushMs: 300 });
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await B.say({ t: 'presence', state: 'away' });
  await A.say(inv('B'));
  await B.next('invite');
});

test('away longer than the push delay with no way to push: still tried over the socket', async (t) => {
  const hub = await startHub({ awayPushMs: 40 });
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await B.say({ t: 'presence', state: 'away' });
  await sleep(100);
  await A.say(inv('B'));
  await B.next('invite');
  assert.equal((await A.next('inviteUpdate')).status, 'delivered');
});

test('A and B each invite the other: both invites live, independent', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await A.say(inv('B'));
  await B.say(inv('A', { lobbyCode: 'GHJKLM' }));
  const toB = await B.next('invite');
  const toA = await A.next('invite');
  assert.notEqual(toA.inviteID, toB.inviteID);
});

test('removing a friend cancels invites between the two', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const [A, B] = await pair(hub);
  await A.say(inv('B'));
  const got = await B.next('invite');
  await B.say({ t: 'friends', ids: [] });
  assert.equal((await B.next('inviteGone')).inviteID, got.inviteID);
  await A.next('inviteUpdate', status('cancelled'));
});
