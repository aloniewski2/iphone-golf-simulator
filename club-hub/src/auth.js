// Sign-in for the hello message.
//
// Two ways in:
//   { dev: "<id>" }  only when HUB_DEV_AUTH=1 (tests, local runs). Anyone can claim any id, so never in production.
//   Apple Game Center identity verification: the phone asks GameKit for a signature over
//   playerID + bundleID + timestamp + salt, and we check it against Apple's published public key.
//   We store no passwords; the signature proves "Apple says this device is this Game Center player".
import { X509Certificate, verify } from 'node:crypto';

export class AuthError extends Error {}

const MAX_AGE_MS = 5 * 60 * 1000;
const CERT_CACHE_MS = 24 * 60 * 60 * 1000;
const CERT_CACHE_MAX = 16;
const MAX_CERT_BYTES = 64 * 1024;

/** Only https on a *.apple.com host (no credentials, no odd port): the cert URL is attacker-supplied input. */
export function isAppleKeyUrl(raw) {
  let u;
  try { u = new URL(raw); } catch { return false; }
  return u.protocol === 'https:'
    && u.hostname.endsWith('.apple.com')
    && u.username === '' && u.password === ''
    && (u.port === '' || u.port === '443');
}

/** Default certificate download: https only, no redirects (a redirect could leave *.apple.com), small, time limited. */
export async function fetchCertFromNetwork(url) {
  const res = await fetch(url, { redirect: 'error', signal: AbortSignal.timeout(5000) });
  if (!res.ok) throw new Error(`certificate fetch failed: ${res.status}`);
  const buf = Buffer.from(await res.arrayBuffer());
  if (buf.length === 0 || buf.length > MAX_CERT_BYTES) throw new Error('certificate has a bad size');
  return buf;
}

/** Apple serves a DER X.509 certificate. Returns its public key, refusing certificates outside their validity dates. */
function publicKeyFromCert(buf, nowMs) {
  const cert = new X509Certificate(buf);
  if (nowMs < Date.parse(cert.validFrom) || nowMs > Date.parse(cert.validTo)) throw new Error('certificate not valid now');
  return cert.publicKey;
}

/**
 * @param {object} o
 * @param {boolean} o.devAuth        accept { dev: id }
 * @param {string[]|null} o.bundleIds allow-list for bundleID; null/empty = accept any
 * @param {(url:string)=>Promise<Buffer>} [o.fetchCert] injectable for tests
 * @param {()=>number} [o.now]
 * @returns {(auth:any)=>Promise<string>} resolves to the player id, or throws AuthError
 */
export function createAuthenticator({ devAuth = false, bundleIds = null, fetchCert = fetchCertFromNetwork, now = Date.now } = {}) {
  const certs = new Map(); // url -> { key, at } or { promise }
  const allowed = bundleIds && bundleIds.length ? new Set(bundleIds) : null;

  async function keyFor(url) {
    const hit = certs.get(url);
    if (hit?.promise) return hit.promise;
    if (hit && now() - hit.at < CERT_CACHE_MS) return hit.key;
    // Share one download between concurrent hellos; never cache a failure.
    const promise = (async () => publicKeyFromCert(await fetchCert(url), now()))();
    certs.set(url, { promise });
    try {
      const key = await promise;
      if (certs.size > CERT_CACHE_MAX) certs.delete(certs.keys().next().value);
      certs.set(url, { key, at: now() });
      return key;
    } catch (e) {
      certs.delete(url);
      throw e;
    }
  }

  return async function authenticate(auth) {
    if (!auth || typeof auth !== 'object') throw new AuthError('missing auth');

    if (auth.dev !== undefined) {
      if (!devAuth) throw new AuthError('dev auth is disabled');
      if (typeof auth.dev !== 'string' || auth.dev.length === 0 || auth.dev.length > 128) throw new AuthError('bad dev id');
      return auth.dev;
    }

    const { playerID, bundleID, publicKeyURL, signature, salt, timestamp } = auth;
    if (typeof playerID !== 'string' || playerID.length === 0 || playerID.length > 128) throw new AuthError('bad playerID');
    if (typeof bundleID !== 'string' || bundleID.length === 0 || bundleID.length > 256) throw new AuthError('bad bundleID');
    if (typeof publicKeyURL !== 'string' || !isAppleKeyUrl(publicKeyURL)) throw new AuthError('bad publicKeyURL');
    if (typeof signature !== 'string' || typeof salt !== 'string') throw new AuthError('bad signature');
    if (!Number.isSafeInteger(timestamp) || timestamp < 0) throw new AuthError('bad timestamp');
    if (allowed && !allowed.has(bundleID)) throw new AuthError('bundle id not allowed');
    if (Math.abs(now() - timestamp) > MAX_AGE_MS) throw new AuthError('signature is stale');

    let key;
    try { key = await keyFor(publicKeyURL); } catch { throw new AuthError('could not load Apple certificate'); }

    const ts = Buffer.alloc(8);
    ts.writeBigUInt64BE(BigInt(timestamp));
    const payload = Buffer.concat([Buffer.from(playerID, 'utf8'), Buffer.from(bundleID, 'utf8'), ts, Buffer.from(salt, 'base64')]);
    let ok = false;
    try { ok = verify('sha256', payload, key, Buffer.from(signature, 'base64')); } catch { ok = false; }
    if (!ok) throw new AuthError('signature does not verify');
    return playerID;
  };
}
