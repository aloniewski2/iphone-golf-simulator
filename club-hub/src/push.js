// APNs sender (token-based auth, HTTP/2). Used only when the receiver has no live socket.
//
// The transport is injectable: tests pass a fake with request({ host, path, headers, body }) -> { status, body }.
import { createHash, createPrivateKey, sign } from 'node:crypto';
import http2 from 'node:http2';

const HOSTS = { sandbox: 'api.sandbox.push.apple.com', production: 'api.push.apple.com' };
const JWT_LIFETIME_MS = 40 * 60 * 1000; // APNs wants a token refreshed between 20 and 60 minutes

const b64url = (b) => Buffer.from(b).toString('base64url');
const SPORT_LABEL = { tennis: 'Tennis', golf: 'Golf' };

/** The real transport: one cached HTTP/2 connection per APNs host, dropped when it errors or closes. */
export function http2Transport({ timeoutMs = 10_000 } = {}) {
  const sessions = new Map();
  function sessionFor(host) {
    let s = sessions.get(host);
    if (s && !s.closed && !s.destroyed) return s;
    s = http2.connect(`https://${host}`);
    const drop = () => { if (sessions.get(host) === s) sessions.delete(host); };
    s.on('error', drop);
    s.on('close', drop);
    s.on('goaway', drop);
    s.unref(); // an idle APNs connection must not keep the process alive
    sessions.set(host, s);
    return s;
  }
  return {
    request({ host, path, headers, body }) {
      return new Promise((resolve, reject) => {
        let session;
        try { session = sessionFor(host); } catch (e) { reject(e); return; }
        const req = session.request({ ':method': 'POST', ':path': path, ...headers });
        let status = 0;
        const chunks = [];
        req.setTimeout(timeoutMs, () => { req.close(http2.constants.NGHTTP2_CANCEL); reject(new Error('APNs request timed out')); });
        req.on('response', (h) => { status = h[':status']; });
        req.on('data', (c) => chunks.push(c));
        req.on('end', () => resolve({ status, body: Buffer.concat(chunks).toString('utf8') }));
        req.on('error', reject);
        req.end(body);
      });
    },
    close() { for (const s of sessions.values()) s.close(); sessions.clear(); },
  };
}

/** APNs allows 64 bytes for the collapse id; Game Center ids are usually shorter, but never trust that. */
function collapseId(id) {
  return Buffer.byteLength(id) <= 64 ? id : createHash('sha256').update(id).digest('hex').slice(0, 32);
}

/** The JSON body the phone sees (PROTOCOL.md "Push"). */
export function buildPayload(invite) {
  return {
    aps: {
      alert: { title: `${invite.from.name} invited you`, body: `${SPORT_LABEL[invite.sport] ?? 'Game'} · Online` },
      sound: 'default',
      'interruption-level': 'time-sensitive',
      category: 'CLUB_INVITE',
      'thread-id': 'invites',
      'mutable-content': 0,
    },
    inviteID: invite.inviteID,
    lobbyCode: invite.lobbyCode,
    group: invite.group,
    sport: invite.sport,
    capacity: invite.capacity,
    from: invite.from,
    expiresAt: invite.expiresAt, // the app shows the same countdown as an in-game card
  };
}

/**
 * @param {object} o
 * @param {string} [o.keyPem]  the .p8 contents (loaded by index.js from HUB_APNS_KEY_FILE)
 * @param {string} [o.keyId] @param {string} [o.teamId] @param {string} [o.topic]
 * @param {object} [o.transport] @param {()=>number} [o.now] @param {object} [o.log]
 * @returns {{ configured: boolean, send(invite, record): Promise<{ok:boolean, removeToken?:boolean, reason?:string}> }}
 */
export function createPusher({ keyPem, keyId, teamId, topic, transport, now = Date.now, log = console } = {}) {
  const configured = Boolean(keyPem && keyId && teamId && topic);
  if (!configured) return { configured: false, async send() { log.log?.('push skipped: APNs is not configured'); return { ok: false, reason: 'unconfigured' }; } };

  const key = createPrivateKey(keyPem);
  const tx = transport ?? http2Transport();
  let jwt = null;
  let jwtAt = 0;

  function bearer() {
    if (!jwt || now() - jwtAt > JWT_LIFETIME_MS) {
      const head = b64url(JSON.stringify({ alg: 'ES256', kid: keyId }));
      const claims = b64url(JSON.stringify({ iss: teamId, iat: Math.floor(now() / 1000) }));
      const sig = sign('sha256', Buffer.from(`${head}.${claims}`), { key, dsaEncoding: 'ieee-p1363' }); // JWS wants r||s, not DER
      jwt = `${head}.${claims}.${b64url(sig)}`;
      jwtAt = now();
    }
    return jwt;
  }

  return {
    configured: true,
    async send(invite, record) {
      const env = record.env === 'production' ? 'production' : 'sandbox';
      try {
        const res = await tx.request({
          host: HOSTS[env],
          path: `/3/device/${record.token}`,
          headers: {
            authorization: `bearer ${bearer()}`,
            'apns-topic': topic,
            'apns-push-type': 'alert',
            'apns-priority': '10',
            'apns-expiration': String(Math.ceil(invite.expiresAt / 1000)), // APNs wants unix SECONDS
            'apns-collapse-id': collapseId(invite.from.id),
            'content-type': 'application/json',
          },
          body: JSON.stringify(buildPayload(invite)),
        });
        if (res.status === 200) return { ok: true };
        let reason = '';
        try { reason = JSON.parse(res.body).reason ?? ''; } catch { /* body is optional */ }
        const removeToken = res.status === 410 || reason === 'BadDeviceToken' || reason === 'Unregistered';
        log.log?.(`push failed: ${res.status} ${reason}${removeToken ? ' (token removed)' : ''}`);
        return { ok: false, reason: reason || String(res.status), removeToken };
      } catch (e) {
        log.log?.(`push failed: ${e.message}`);
        return { ok: false, reason: 'transport' };
      }
    },
    close() { tx.close?.(); },
  };
}
