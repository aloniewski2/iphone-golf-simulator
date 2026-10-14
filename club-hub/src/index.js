// HTTP + WebSocket server. `node src/index.js` runs it from the environment; tests call createClubHub() with fakes.
import { readFileSync } from 'node:fs';
import http from 'node:http';
import { pathToFileURL } from 'node:url';
import { WebSocketServer } from 'ws';
import { createAuthenticator } from './auth.js';
import { Hub } from './hub.js';
import { renderLanding } from './landing.js';
import { createPusher } from './push.js';
import { MemoryStore, createStore } from './store.js';

const WS_PATH = '/v1/hub';
const LANDING_PER_MINUTE = 60;

/** Env -> options. Kept separate so tests can check the parsing without starting anything. */
export function configFromEnv(env = process.env, log = console) {
  const num = (v, d) => (v !== undefined && v !== '' && Number.isFinite(Number(v)) ? Number(v) : d);
  let keyPem;
  if (env.HUB_APNS_KEY_FILE) {
    try { keyPem = readFileSync(env.HUB_APNS_KEY_FILE, 'utf8'); } catch (e) { log.warn?.(`HUB_APNS_KEY_FILE unreadable (${e.message}); push is off`); }
  }
  return {
    port: num(env.PORT, 8090),
    devAuth: env.HUB_DEV_AUTH === '1',
    bundleIds: env.HUB_BUNDLE_IDS ? env.HUB_BUNDLE_IDS.split(',').map((s) => s.trim()).filter(Boolean) : null,
    installUrl: env.HUB_INSTALL_URL || '',
    aasaFile: env.HUB_AASA_FILE || '',
    databaseUrl: env.DATABASE_URL || '',
    trustProxy: env.HUB_TRUST_PROXY === '1',
    inviteTtlMs: num(env.HUB_INVITE_TTL_MS, 120_000),
    offlineInviteTtlMs: num(env.HUB_OFFLINE_INVITE_TTL_MS, 15 * 60_000),
    apns: { keyPem, keyId: env.HUB_APNS_KEY_ID, teamId: env.HUB_APNS_TEAM_ID, topic: env.HUB_APNS_TOPIC },
  };
}

/**
 * Build (but do not listen) the service.
 * Tests inject: store, authenticate, pusher, now, hub timing overrides.
 */
export function createClubHub({
  store = new MemoryStore(), authenticate, pusher, installUrl = '', aasaFile = '', trustProxy = false,
  devAuth = false, bundleIds = null, apns = {}, log = console, now = Date.now, ...hubOptions
} = {}) {
  authenticate ??= createAuthenticator({ devAuth, bundleIds, now });
  pusher ??= createPusher({ ...apns, now, log });
  const hub = new Hub({ store, authenticate, pusher, log, now, ...hubOptions });

  const landingHits = new Map(); // ip -> timestamps, so /j/CODE cannot be used to guess lobby codes quickly
  const clientIp = (req) => (trustProxy && req.headers['x-forwarded-for']?.split(',')[0].trim()) || req.socket.remoteAddress || '';
  function landingLimited(ip) {
    const t = now();
    const hits = (landingHits.get(ip) ?? []).filter((x) => t - x < 60_000);
    hits.push(t);
    landingHits.set(ip, hits);
    if (landingHits.size > 5000) for (const [k, v] of landingHits) if (t - v[v.length - 1] >= 60_000) landingHits.delete(k);
    return hits.length > LANDING_PER_MINUTE;
  }

  const send = (res, status, type, body, extra = {}) => {
    res.writeHead(status, { 'content-type': type, 'cache-control': 'no-store', 'x-content-type-options': 'nosniff', ...extra });
    res.end(body);
  };

  const server = http.createServer((req, res) => {
    if (req.method !== 'GET' && req.method !== 'HEAD') return send(res, 405, 'text/plain; charset=utf-8', 'method not allowed');
    const path = new URL(req.url, 'http://x').pathname;

    if (path === '/healthz') return send(res, 200, 'application/json', JSON.stringify({ ok: true, online: hub.onlineCount, rooms: hub.roomCount }));

    if (path === '/.well-known/apple-app-site-association') {
      if (!aasaFile) return send(res, 404, 'text/plain; charset=utf-8', 'not found');
      try { return send(res, 200, 'application/json', readFileSync(aasaFile)); } catch { return send(res, 404, 'text/plain; charset=utf-8', 'not found'); }
    }

    const m = /^\/j\/([^/]+)$/.exec(path);
    if (m) {
      if (landingLimited(clientIp(req))) return send(res, 429, 'text/plain; charset=utf-8', 'slow down');
      let raw = m[1];
      try { raw = decodeURIComponent(raw); } catch { /* keep as is; it will not match a lobby */ }
      const code = raw.toUpperCase();
      const host = hub.findLobby(code);
      const html = renderLanding({
        code,
        lobby: host ? { code, sport: host.presence.lobby.sport, hostName: host.name } : null,
        installUrl,
      });
      return send(res, 200, 'text/html; charset=utf-8', html, {
        'content-security-policy': "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'",
        'referrer-policy': 'no-referrer',
      });
    }
    send(res, 404, 'text/plain; charset=utf-8', 'not found');
  });

  // (party relay frames carry a lobby's game packets, up to 60 KB each before escaping)
  const wss = new WebSocketServer({ noServer: true, maxPayload: 192 * 1024 });
  server.on('upgrade', (req, socket, head) => {
    if (new URL(req.url, 'http://x').pathname !== WS_PATH) { socket.destroy(); return; }
    wss.handleUpgrade(req, socket, head, (ws) => {
      const conn = hub.connect({
        send: (text) => { if (ws.readyState === ws.OPEN) ws.send(text); },
        close: (code, reason) => ws.close(code, reason),
        terminate: () => ws.terminate(),
      });
      ws.on('message', (data, isBinary) => {
        if (isBinary) { ws.send(JSON.stringify({ t: 'error', code: 'bad_message', message: 'text frames only' })); return; }
        conn.message(data.toString('utf8'));
      });
      ws.on('close', () => conn.close());
      ws.on('error', () => ws.terminate());
    });
  });

  async function close() {
    hub.close();
    for (const c of wss.clients) c.terminate();
    wss.close();
    pusher.close?.();
    await new Promise((r) => server.close(r));
    server.closeAllConnections?.();
    await store.close();
  }
  return { server, hub, store, close };
}

// Run directly: `node src/index.js`
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const cfg = configFromEnv();
  if (cfg.devAuth) console.warn('HUB_DEV_AUTH=1: anyone can sign in as any player id. Local use only.');
  if (!cfg.devAuth && !cfg.bundleIds) console.warn('HUB_BUNDLE_IDS is not set: any bundle id passes Apple verification. Set it in production.');
  const store = await createStore({ databaseUrl: cfg.databaseUrl });
  const app = createClubHub({ ...cfg, store });
  app.server.listen(cfg.port, () => console.log(`Club Hub on :${cfg.port} (ws path ${WS_PATH})`));
  const shutdown = async () => { setTimeout(() => process.exit(0), 5000).unref(); await app.close(); process.exit(0); };
  process.on('SIGTERM', shutdown);
  process.on('SIGINT', shutdown);
}
