// Test harness: a real server on an ephemeral port and real WebSocket clients.
import assert from 'node:assert/strict';
import { once } from 'node:events';
import WebSocket from 'ws';
import { createClubHub } from '../../src/index.js';
import { createPusher } from '../../src/push.js';
import { MemoryStore } from '../../src/store.js';

export const quietLog = { log() {}, warn() {}, error() {} };
export const CODE = 'ABCDEF';
export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** A fake APNs transport: records requests, answers with `reply` (can be changed per test). */
export function fakeTransport() {
  const t = { requests: [], reply: () => ({ status: 200, body: '' }) };
  t.request = async (req) => { t.requests.push(req); return t.reply(req); };
  return t;
}

export async function startHub(opts = {}) {
  const store = opts.store ?? new MemoryStore();
  const app = createClubHub({ devAuth: true, log: quietLog, store, ...opts });
  app.server.listen(0, '127.0.0.1');
  await once(app.server, 'listening');
  const port = app.server.address().port;
  const clients = [];
  const api = {
    ...app,
    store,
    port,
    base: `http://127.0.0.1:${port}`,
    /** Open a socket (no hello). */
    open: () => {
      const c = new Client(`ws://127.0.0.1:${port}/v1/hub`);
      clients.push(c);
      return c;
    },
    /** Open + hello as dev player `id`; resolves to the client once `welcome` arrived. */
    async join(id, o = {}) {
      const c = api.open();
      await c.hello({ id, ...o });
      return c;
    },
    async stop() {
      for (const c of clients) c.ws.terminate();
      await app.close();
    },
  };
  return api;
}

export class Client {
  constructor(url) {
    this.ws = new WebSocket(url);
    this.inbox = [];     // messages not yet consumed by next()
    this.waiters = [];
    this.closed = new Promise((resolve) => this.ws.on('close', (code) => { this.closeCode = code; resolve(code); }));
    this.opened = new Promise((resolve, reject) => { this.ws.on('open', resolve); this.ws.on('error', reject); });
    this.ws.on('message', (data) => {
      const m = JSON.parse(data.toString());
      const i = this.waiters.findIndex((w) => w.pred(m));
      if (i >= 0) this.waiters.splice(i, 1)[0].resolve(m); else this.inbox.push(m);
    });
  }

  async say(m) { await this.opened; this.ws.send(typeof m === 'string' ? m : JSON.stringify(m)); }

  /** Wait for (and consume) the next message of type `t` matching `match`. */
  next(t, match = () => true, ms = 2000) {
    const pred = (m) => m.t === t && match(m);
    const i = this.inbox.findIndex(pred);
    if (i >= 0) return Promise.resolve(this.inbox.splice(i, 1)[0]);
    return new Promise((resolve, reject) => {
      const w = { pred, resolve: (m) => { clearTimeout(timer); resolve(m); } };
      const timer = setTimeout(() => {
        this.waiters.splice(this.waiters.indexOf(w), 1);
        reject(new Error(`timed out waiting for ${t}; inbox: ${JSON.stringify(this.inbox)}`));
      }, ms);
      this.waiters.push(w);
    });
  }

  /** Assert that no message of type `t` (matching) arrives within `ms`. */
  async none(t, match = () => true, ms = 120) {
    await sleep(ms);
    const hit = this.inbox.find((m) => m.t === t && match(m));
    assert.equal(hit, undefined, `unexpected ${t}: ${JSON.stringify(hit)}`);
  }

  hello({ id, name = id, friends = [], presence = { state: 'plaza' }, push, auth, extra = {} }) {
    return this.say({ t: 'hello', v: 1, auth: auth ?? { dev: id }, name, friends, presence, app: { version: '1', build: '1', protocol: 1 }, ...(push ? { push } : {}), ...extra })
      .then(() => this.next('welcome'));
  }

  async close() { this.ws.close(); await this.closed; }
}

export const lobby = (o = {}) => ({ code: CODE, sport: 'tennis', size: 1, capacity: 2, open: false, group: 1234, ...o });

export function makePusher(transport, extra = {}) {
  return createPusher({ keyPem: extra.keyPem, keyId: 'KEY1', teamId: 'TEAM1', topic: 'com.example.app', transport, log: quietLog, ...extra });
}
