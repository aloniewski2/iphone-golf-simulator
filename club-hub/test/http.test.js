import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import test from 'node:test';
import { configFromEnv } from '../src/index.js';
import { CODE, lobby, quietLog, startHub } from './helpers/harness.js';

const get = (hub, path) => fetch(hub.base + path);

test('/healthz reports ok and the number of online players', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  let r = await get(hub, '/healthz');
  assert.equal(r.status, 200);
  assert.deepEqual(await r.json(), { ok: true, online: 0 });
  await hub.join('A');
  await hub.join('B');
  r = await get(hub, '/healthz');
  assert.deepEqual(await r.json(), { ok: true, online: 2 });
});

test('/j/:code for a live lobby shows host, sport, big code, open link and install button', async (t) => {
  const hub = await startHub({ installUrl: 'https://apps.apple.com/app/id123?x=1&y=2' });
  t.after(() => hub.stop());
  await hub.join('H', { name: 'Sam', presence: { state: 'lobby', lobby: lobby({ sport: 'golf' }) } });
  for (const path of [`/j/${CODE}`, `/j/${CODE.toLowerCase()}`]) {
    const r = await get(hub, path);
    assert.equal(r.status, 200);
    assert.match(r.headers.get('content-type'), /text\/html/);
    assert.match(r.headers.get('content-security-policy'), /default-src 'none'/);
    const html = await r.text();
    assert.match(html, /Sam invited you to play Golf/);
    assert.ok(html.includes(CODE));
    assert.ok(html.includes(`href="motionclub://j/${CODE}"`));
    assert.ok(html.includes('href="https://apps.apple.com/app/id123?x=1&amp;y=2"'));
    assert.match(html, /#10243D/i);
    assert.match(html, /#D7F044/i);
    assert.match(html, /#FFF9EE/i);
    assert.ok(!html.includes('<script'));
    assert.ok(!html.includes('"G:'), 'no ids are revealed');
  }
});

test('/j/:code for a code that is not a live lobby says the invite has ended', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  const host = await hub.join('H', { name: 'Sam', presence: { state: 'lobby', lobby: lobby() } });
  await host.say({ t: 'presence', state: 'plaza', lobby: null });
  await new Promise((r) => setTimeout(r, 50));
  for (const path of [`/j/${CODE}`, '/j/ZZZZZZ', '/j/abc', '/j/%E0%A4%A']) {
    const html = await (await get(hub, path)).text();
    assert.match(html, /This invite has ended/, path);
    assert.ok(!html.includes('motionclub://'), 'no open link for a dead code');
    assert.ok(!html.includes('Sam'));
  }
});

test('/j/:code escapes the host name and the code (XSS attempt)', async (t) => {
  const hub = await startHub({ installUrl: 'javascript:alert(1)' });
  t.after(() => hub.stop());
  const evil = '"\'&<script>alert(1)</script><img src=x>'; // 39 chars, under the 40-char name cap
  await hub.join('H', { name: evil, presence: { state: 'lobby', lobby: lobby() } });
  const html = await (await get(hub, `/j/${CODE}`)).text();
  assert.ok(!html.includes('<script'));
  assert.ok(!html.includes('<img'), 'no raw img tag');
  assert.ok(html.includes('&lt;script&gt;alert(1)&lt;/script&gt;'));
  assert.ok(html.includes('&quot;'));
  assert.ok(html.includes('&#39;'));
  assert.ok(!html.includes('javascript:'), 'a non-http install url is dropped');

  const probe = await (await get(hub, '/j/%3Cscript%3Ealert(1)%3C%2Fscript%3E')).text();
  assert.ok(!probe.includes('<script'));
});

test('/j is rate limited per client address', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  let last;
  for (let i = 0; i < 61; i++) last = await get(hub, '/j/ZZZZZZ');
  assert.equal(last.status, 429);
});

test('apple-app-site-association is served from HUB_AASA_FILE, 404 when unset', async (t) => {
  const dir = mkdtempSync(join(tmpdir(), 'hub-aasa-'));
  const file = join(dir, 'aasa.json');
  writeFileSync(file, '{"applinks":{"details":[]}}');
  const withFile = await startHub({ aasaFile: file });
  const without = await startHub();
  t.after(() => Promise.all([withFile.stop(), without.stop()]));
  const r = await get(withFile, '/.well-known/apple-app-site-association');
  assert.equal(r.status, 200);
  assert.match(r.headers.get('content-type'), /application\/json/);
  assert.deepEqual(await r.json(), { applinks: { details: [] } });
  assert.equal((await get(without, '/.well-known/apple-app-site-association')).status, 404);
});

test('unknown paths are 404, non-GET is 405, a websocket on another path is refused', async (t) => {
  const hub = await startHub();
  t.after(() => hub.stop());
  assert.equal((await get(hub, '/nope')).status, 404);
  assert.equal((await fetch(hub.base + '/healthz', { method: 'POST' })).status, 405);
  const { default: WebSocket } = await import('ws');
  const ws = new WebSocket(`ws://127.0.0.1:${hub.port}/other`);
  await new Promise((resolve) => { ws.on('error', resolve); ws.on('close', resolve); });
});

test('configFromEnv reads the documented variables', () => {
  const cfg = configFromEnv({
    PORT: '9000', HUB_DEV_AUTH: '1', HUB_BUNDLE_IDS: 'a.b, c.d', HUB_INSTALL_URL: 'https://x', HUB_AASA_FILE: '/f',
    DATABASE_URL: 'postgres://x', HUB_INVITE_TTL_MS: '500', HUB_APNS_KEY_ID: 'K', HUB_APNS_TEAM_ID: 'T', HUB_APNS_TOPIC: 'b',
  }, quietLog);
  assert.equal(cfg.port, 9000);
  assert.equal(cfg.devAuth, true);
  assert.deepEqual(cfg.bundleIds, ['a.b', 'c.d']);
  assert.equal(cfg.inviteTtlMs, 500);
  assert.equal(cfg.apns.topic, 'b');
  const defaults = configFromEnv({}, quietLog);
  assert.equal(defaults.port, 8090);
  assert.equal(defaults.devAuth, false);
  assert.equal(defaults.bundleIds, null);
  assert.equal(defaults.inviteTtlMs, 120000);
});
