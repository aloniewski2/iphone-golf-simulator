import assert from 'node:assert/strict';
import { generateKeyPairSync, sign } from 'node:crypto';
import test from 'node:test';
import { AuthError, createAuthenticator, isAppleKeyUrl } from '../src/auth.js';
import { selfSignedCert } from './helpers/cert.js';
import { startHub } from './helpers/harness.js';

const URL_OK = 'https://static.gc.apple.com/public-key/gc-prod-4.cer';
const BUNDLE = 'com.example.motionclub';

const rsa = () => generateKeyPairSync('rsa', { modulusLength: 2048 });

/** What GameKit would hand the app: a signature over playerID + bundleID + timestamp(8 BE) + salt. */
function appleAuth(privateKey, { playerID = 'G:1001', bundleID = BUNDLE, timestamp = Date.now(), url = URL_OK } = {}) {
  const salt = Buffer.from('some-salt-bytes');
  const ts = Buffer.alloc(8);
  ts.writeBigUInt64BE(BigInt(timestamp));
  const data = Buffer.concat([Buffer.from(playerID), Buffer.from(bundleID), ts, salt]);
  return { playerID, bundleID, publicKeyURL: url, signature: sign('sha256', data, privateKey).toString('base64'), salt: salt.toString('base64'), timestamp };
}

function setup(extra = {}) {
  const { publicKey, privateKey } = rsa();
  const fetched = [];
  const cert = selfSignedCert(privateKey, publicKey);
  const authenticate = createAuthenticator({
    bundleIds: [BUNDLE],
    fetchCert: async (u) => { fetched.push(u); return cert; },
    ...extra,
  });
  return { privateKey, publicKey, authenticate, fetched };
}

test('a valid Apple signature signs the player in as playerID', async () => {
  const { privateKey, authenticate } = setup();
  assert.equal(await authenticate(appleAuth(privateKey)), 'G:1001');
});

test('a signature from a different key is rejected', async () => {
  const { authenticate } = setup();
  const other = rsa();
  await assert.rejects(authenticate(appleAuth(other.privateKey)), AuthError);
});

test('changing any signed field breaks the signature', async () => {
  const { privateKey, authenticate } = setup();
  const good = appleAuth(privateKey);
  await assert.rejects(authenticate({ ...good, playerID: 'G:2002' }), /does not verify/);
  await assert.rejects(authenticate({ ...good, salt: Buffer.from('other').toString('base64') }), /does not verify/);
  await assert.rejects(authenticate({ ...good, timestamp: good.timestamp + 1 }), /does not verify/);
});

test('stale and future timestamps (more than 5 minutes) are rejected, 4 minutes is fine', async () => {
  const { privateKey, authenticate } = setup();
  await assert.rejects(authenticate(appleAuth(privateKey, { timestamp: Date.now() - 6 * 60_000 })), /stale/);
  await assert.rejects(authenticate(appleAuth(privateKey, { timestamp: Date.now() + 6 * 60_000 })), /stale/);
  assert.equal(await authenticate(appleAuth(privateKey, { timestamp: Date.now() - 4 * 60_000 })), 'G:1001');
});

test('publicKeyURL must be https on *.apple.com', async () => {
  const { privateKey, authenticate, fetched } = setup();
  for (const url of [
    'http://static.gc.apple.com/public-key/gc-prod-4.cer',
    'https://evil.example.com/public-key/gc-prod-4.cer',
    'https://static.gc.apple.com.evil.com/x.cer',
    'https://apple.com/x.cer',                 // not a subdomain
    'https://notapple.com/x.cer',
    'https://static.gc.apple.com@evil.com/x.cer',
    'https://user:pw@static.gc.apple.com/x.cer',
    'https://static.gc.apple.com:8443/x.cer',
    'ftp://static.gc.apple.com/x.cer',
    'not a url',
  ]) {
    await assert.rejects(authenticate(appleAuth(privateKey, { url })), /publicKeyURL/, url);
  }
  assert.equal(fetched.length, 0, 'nothing is downloaded for a bad URL');
  assert.equal(isAppleKeyUrl(URL_OK), true);
});

test('bundle id must be on the allow-list; with no list any bundle id passes', async () => {
  const { privateKey, authenticate } = setup();
  await assert.rejects(authenticate(appleAuth(privateKey, { bundleID: 'com.evil.app' })), /bundle id/);

  const open = setup({ bundleIds: null });
  assert.equal(await open.authenticate(appleAuth(open.privateKey, { bundleID: 'com.anything' })), 'G:1001');
});

test('the certificate is downloaded once and cached; a failed download is not cached', async () => {
  const { privateKey, authenticate, fetched } = setup();
  await Promise.all([authenticate(appleAuth(privateKey)), authenticate(appleAuth(privateKey, { playerID: 'G:2' }))]);
  await authenticate(appleAuth(privateKey));
  assert.equal(fetched.length, 1);

  let calls = 0;
  const { publicKey, privateKey: pk2 } = rsa();
  const cert = selfSignedCert(pk2, publicKey);
  const flaky = createAuthenticator({ fetchCert: async () => { if (++calls === 1) throw new Error('network'); return cert; } });
  await assert.rejects(flaky(appleAuth(pk2)), /certificate/);
  assert.equal(await flaky(appleAuth(pk2)), 'G:1001');
});

test('an expired certificate is refused', async () => {
  const { publicKey, privateKey } = rsa();
  const old = selfSignedCert(privateKey, publicKey, { notBefore: new Date('2020-01-01'), notAfter: new Date('2021-01-01') });
  const authenticate = createAuthenticator({ fetchCert: async () => old });
  await assert.rejects(authenticate(appleAuth(privateKey)), /certificate/);
});

test('malformed auth objects are rejected, dev auth only when enabled', async () => {
  const { authenticate } = setup();
  for (const bad of [null, 'x', {}, { dev: 'a' }, { playerID: 5 }, { playerID: 'a', bundleID: BUNDLE, publicKeyURL: URL_OK, signature: 1, salt: '', timestamp: 1 }]) {
    await assert.rejects(authenticate(bad), AuthError);
  }
  const dev = createAuthenticator({ devAuth: true });
  assert.equal(await dev({ dev: 'alice' }), 'alice');
  await assert.rejects(dev({ dev: '' }), AuthError);
  await assert.rejects(dev({ dev: 'x'.repeat(200) }), AuthError);
});

test('over the wire: valid Apple hello gets a welcome, a wrong signature gets unauthorized + 4401', async (t) => {
  const { privateKey, authenticate } = setup();
  const hub = await startHub({ devAuth: false, authenticate });
  t.after(() => hub.stop());

  const good = hub.open();
  const welcome = await good.hello({ id: 'ignored', auth: appleAuth(privateKey, { playerID: 'G:777' }) });
  assert.equal(welcome.you, 'G:777');

  const bad = hub.open();
  await bad.say({ t: 'hello', v: 1, auth: appleAuth(rsa().privateKey), name: 'x', friends: [], presence: { state: 'plaza' } });
  const err = await bad.next('error');
  assert.equal(err.code, 'unauthorized');
  assert.equal(await bad.closed, 4401);
});
