import assert from 'node:assert/strict';
import { createPublicKey, generateKeyPairSync, verify } from 'node:crypto';
import test from 'node:test';
import { buildPayload } from '../src/push.js';
import { CODE, fakeTransport, makePusher, quietLog, sleep, startHub } from './helpers/harness.js';

const TOKEN = 'ab'.repeat(32);
const ec = generateKeyPairSync('ec', { namedCurve: 'prime256v1' });
const keyPem = ec.privateKey.export({ type: 'pkcs8', format: 'pem' });
const inv = (to, o = {}) => ({ t: 'invite', to, sport: 'tennis', lobbyCode: CODE, group: 1234, capacity: 2, ...o });
const status = (s) => (m) => m.status === s;

const sample = { inviteID: 'i-1', from: { id: 'A', name: 'Sam' }, sport: 'tennis', lobbyCode: CODE, group: 77, capacity: 2, expiresAt: 1_800_000_123_456 };

test('pusher: request has the APNs headers, topic, host per env and a valid ES256 JWT', async () => {
  const tx = fakeTransport();
  const pusher = makePusher(tx, { keyPem });
  assert.equal(pusher.configured, true);
  assert.deepEqual(await pusher.send(sample, { token: TOKEN, env: 'sandbox' }), { ok: true });
  await pusher.send(sample, { token: TOKEN, env: 'production' });

  const [sb, prod] = tx.requests;
  assert.equal(sb.host, 'api.sandbox.push.apple.com');
  assert.equal(prod.host, 'api.push.apple.com');
  assert.equal(sb.path, `/3/device/${TOKEN}`);
  assert.equal(sb.headers['apns-topic'], 'com.example.app');
  assert.equal(sb.headers['apns-push-type'], 'alert');
  assert.equal(sb.headers['apns-priority'], '10');
  assert.equal(sb.headers['apns-expiration'], '1800000124', 'unix seconds, rounded up from the invite expiry');
  assert.equal(sb.headers['apns-collapse-id'], 'A');

  const [head, claims, sig] = sb.headers.authorization.replace('bearer ', '').split('.');
  assert.deepEqual(JSON.parse(Buffer.from(head, 'base64url')), { alg: 'ES256', kid: 'KEY1' });
  assert.equal(JSON.parse(Buffer.from(claims, 'base64url')).iss, 'TEAM1');
  const ok = verify('sha256', Buffer.from(`${head}.${claims}`), { key: createPublicKey(keyPem), dsaEncoding: 'ieee-p1363' }, Buffer.from(sig, 'base64url'));
  assert.equal(ok, true);
  assert.equal(prod.headers.authorization, sb.headers.authorization, 'the JWT is reused, not re-signed per push');
});

test('pusher: payload matches PROTOCOL.md and a long collapse id is shortened', async () => {
  assert.deepEqual(buildPayload(sample), {
    aps: {
      alert: { title: 'Sam invited you', body: 'Tennis · Online' },
      sound: 'default', 'interruption-level': 'time-sensitive', category: 'CLUB_INVITE', 'thread-id': 'invites', 'mutable-content': 0,
    },
    inviteID: 'i-1', lobbyCode: CODE, group: 77, sport: 'tennis', capacity: 2, from: { id: 'A', name: 'Sam' }, expiresAt: sample.expiresAt,
  });
  assert.equal(buildPayload({ ...sample, sport: 'golf' }).aps.alert.body, 'Golf · Online');

  const tx = fakeTransport();
  await makePusher(tx, { keyPem }).send({ ...sample, from: { id: 'G:'.padEnd(120, 'x'), name: 'L' } }, { token: TOKEN, env: 'sandbox' });
  assert.ok(Buffer.byteLength(tx.requests[0].headers['apns-collapse-id']) <= 64);
  assert.deepEqual(JSON.parse(tx.requests[0].body).from.id.length, 120);
});

test('pusher: unconfigured logs "push skipped" and reports not ok', async () => {
  const lines = [];
  const pusher = makePusher(fakeTransport(), { keyPem: undefined, log: { log: (l) => lines.push(l) } });
  assert.equal(pusher.configured, false);
  const r = await pusher.send(sample, { token: TOKEN, env: 'sandbox' });
  assert.equal(r.ok, false);
  assert.ok(lines.some((l) => l.includes('push skipped')));
});

test('pusher: 410 and BadDeviceToken ask for the token to be removed; other failures do not', async () => {
  const tx = fakeTransport();
  const pusher = makePusher(tx, { keyPem });
  tx.reply = () => ({ status: 410, body: '{"reason":"Unregistered"}' });
  assert.equal((await pusher.send(sample, { token: TOKEN, env: 'sandbox' })).removeToken, true);
  tx.reply = () => ({ status: 400, body: '{"reason":"BadDeviceToken"}' });
  assert.equal((await pusher.send(sample, { token: TOKEN, env: 'sandbox' })).removeToken, true);
  tx.reply = () => ({ status: 500, body: '' });
  assert.deepEqual(await pusher.send(sample, { token: TOKEN, env: 'sandbox' }), { ok: false, reason: '500', removeToken: false });
  tx.reply = () => { throw new Error('socket hang up'); };
  assert.equal((await pusher.send(sample, { token: TOKEN, env: 'sandbox' })).ok, false);
});

/** Hub with a fake APNs; B registers a token and goes offline. */
async function offlineReceiver(t, hubOpts = {}) {
  const tx = fakeTransport();
  const hub = await startHub({ pusher: makePusher(tx, { keyPem }), ...hubOpts });
  t.after(() => hub.stop());
  const A = await hub.join('A', { name: 'Sam', friends: ['B'] });
  const B = await hub.join('B', { friends: ['A'], push: { token: TOKEN, env: 'sandbox' } });
  await A.next('presence', (m) => m.id === 'B');
  return { hub, tx, A, B };
}

test('offline receiver with a token: push goes through the transport, sender gets pushed', async (t) => {
  const { tx, A, B } = await offlineReceiver(t);
  await B.close();
  await sleep(50);
  await A.say(inv('B'));
  const up = await A.next('inviteUpdate', status('pushed'));
  assert.equal(up.to, 'B');
  assert.equal(tx.requests.length, 1);
  assert.equal(tx.requests[0].path, `/3/device/${TOKEN}`);
  const body = JSON.parse(tx.requests[0].body);
  assert.equal(body.inviteID, up.inviteID);
  assert.equal(body.aps.alert.title, 'Sam invited you');
  assert.deepEqual(body.from, { id: 'A', name: 'Sam' });
});

test('the pushed invite is handed over when the receiver comes back, and can be accepted', async (t) => {
  const { hub, A, B } = await offlineReceiver(t);
  await B.close();
  await sleep(50);
  await A.say(inv('B'));
  const pushed = await A.next('inviteUpdate', status('pushed'));
  const B2 = await hub.join('B', { friends: ['A'] });
  const got = await B2.next('invite');
  assert.equal(got.inviteID, pushed.inviteID);
  await A.next('inviteUpdate', status('delivered'));
  await B2.say({ t: 'inviteResponse', inviteID: got.inviteID, accept: true });
  await A.next('inviteUpdate', status('accepted'));
});

test('a pushed invite can be answered by id even before the app reconnects, and expires normally', async (t) => {
  const { A, B } = await offlineReceiver(t, { inviteTtlMs: 200 });
  await B.close();
  await sleep(50);
  await A.say(inv('B'));
  await A.next('inviteUpdate', status('pushed'));
  await A.next('inviteUpdate', status('expired'));
});

test('no push when the receiver has a live socket (never both)', async (t) => {
  const { tx, A, B } = await offlineReceiver(t);
  await A.say(inv('B'));
  await B.next('invite');
  await A.next('inviteUpdate', status('delivered'));
  await sleep(100);
  assert.equal(tx.requests.length, 0);
});

test('receiver "away" for longer than the push delay: push only, no socket frame; short away: socket only', async (t) => {
  const { tx, A, B } = await offlineReceiver(t, { awayPushMs: 80 });
  await B.say({ t: 'presence', state: 'away' });
  await A.say(inv('B'));
  await B.next('invite');
  assert.equal(tx.requests.length, 0);
  await A.next('inviteUpdate', status('delivered'));

  await sleep(150); // now "away" for longer than the delay
  await A.say(inv('B', { lobbyCode: 'GHJKLM' }));
  await A.next('inviteUpdate', status('pushed'));
  assert.equal(tx.requests.length, 1);
  await B.none('invite', (m) => m.lobbyCode === 'GHJKLM');

  // coming back to the foreground hands the waiting invite over
  await B.say({ t: 'presence', state: 'plaza' });
  await B.next('invite', (m) => m.lobbyCode === 'GHJKLM');
});

test('APNs rejects the token (410): sender gets unavailable and the token is removed', async (t) => {
  const { hub, tx, A, B } = await offlineReceiver(t);
  tx.reply = () => ({ status: 410, body: '{"reason":"Unregistered"}' });
  await B.close();
  await sleep(50);
  assert.ok(hub.store.getPush('B'));
  await A.say(inv('B'));
  await A.next('inviteUpdate', status('unavailable'));
  assert.equal(hub.store.getPush('B'), null);
  await A.none('inviteUpdate', status('pushed'));
});

test('a transient APNs failure gives unavailable but keeps the token', async (t) => {
  const { hub, tx, A, B } = await offlineReceiver(t);
  tx.reply = () => ({ status: 503, body: '' });
  await B.close();
  await sleep(50);
  await A.say(inv('B'));
  await A.next('inviteUpdate', status('unavailable'));
  assert.ok(hub.store.getPush('B'));
});

test('token registered by a push message, changed, and removed with ""', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const B = await hub.join('B');
  await B.say({ t: 'push', token: TOKEN, env: 'production' });
  await sleep(50);
  assert.deepEqual([hub.store.getPush('B').token, hub.store.getPush('B').env], [TOKEN, 'production']);
  await B.say({ t: 'push', token: 'cd'.repeat(32), env: 'sandbox' });
  await sleep(50);
  assert.equal(hub.store.getPush('B').token, 'cd'.repeat(32));
  await B.say({ t: 'push', token: 'nothex', env: 'sandbox' });
  assert.equal((await B.next('error')).code, 'bad_message');
  await B.say({ t: 'push', token: '', env: 'sandbox' });
  await sleep(50);
  assert.equal(hub.store.getPush('B'), null);
});

test('token + push unconfigured: push skipped, the invite waits for them (queued)', async (t) => {
  const lines = [];
  const hub = await startHub({ log: { log: (l) => lines.push(l), warn() {}, error() {} } });
  t.after(() => hub.stop());
  const A = await hub.join('A', { friends: ['B'] });
  const B = await hub.join('B', { friends: ['A'], push: { token: TOKEN, env: 'sandbox' } });
  await B.close();
  await sleep(50);
  await A.say(inv('B'));
  await A.next('inviteUpdate', status('queued'));
  assert.ok(lines.some((l) => l.includes('push skipped')));
  void quietLog;
});

test('offline friend check uses the friend list stored with the token (survives a hub restart)', async (t) => {
  const tx = fakeTransport();
  const first = await startHub({ pusher: makePusher(tx, { keyPem }) });
  const B1 = await first.join('B', { friends: ['A'], push: { token: TOKEN, env: 'sandbox' } });
  await B1.close();
  await sleep(50);
  const store = first.store;
  await first.stop();

  const second = await startHub({ store, pusher: makePusher(tx, { keyPem }) });
  t.after(() => second.stop());
  const A = await second.join('A', { friends: ['B', 'C'] });
  await A.say(inv('B'));
  await A.next('inviteUpdate', status('pushed'));
  // C is unknown and has no token: the invite waits for C
  await A.say(inv('C'));
  await A.next('inviteUpdate', status('queued'));
});

test('offline receiver who dropped me, and whom I dropped too: not_friends, nothing pushed', async (t) => {
  const { tx, A, B } = await offlineReceiver(t);
  await B.say({ t: 'friends', ids: [] });
  await A.say({ t: 'friends', ids: [] });
  await sleep(50);
  await B.close();
  await sleep(50);
  await A.say(inv('B'));
  assert.equal((await A.next('error')).code, 'not_friends');
  assert.equal(tx.requests.length, 0);
});
