// What survives a restart: blocks and push tokens. Everything live (sockets, presence, pending invites) is in memory in hub.js.
//
// Reads are synchronous on purpose: the stores keep a full in-memory copy (loaded once in init()) and write through to
// Postgres. That keeps hub.js simple (no await in the middle of a visibility check) and is fine for this service's size.
// Push records also carry the player's last-known name + friend list, so an invite to an OFFLINE friend can still be
// checked for mutual friendship (the friend list is otherwise only known while a player is connected).

export class MemoryStore {
  constructor() {
    this.blocks = new Map(); // blocker -> Set(target)
    this.push = new Map();   // id -> { token, env, name, friends: string[] }
  }
  async init() {}
  async close() {}

  /** Did `blocker` block `target`? */
  hasBlocked(blocker, target) { return this.blocks.get(blocker)?.has(target) ?? false; }
  /** Blocked in either direction. */
  isBlocked(a, b) { return this.hasBlocked(a, b) || this.hasBlocked(b, a); }
  async block(blocker, target) {
    if (!this.blocks.has(blocker)) this.blocks.set(blocker, new Set());
    this.blocks.get(blocker).add(target);
    await this._persistBlock(blocker, target, true);
  }
  async unblock(blocker, target) {
    this.blocks.get(blocker)?.delete(target);
    await this._persistBlock(blocker, target, false);
  }

  getPush(id) { return this.push.get(id) ?? null; }
  async setPush(id, record) {
    if (record) this.push.set(id, record); else this.push.delete(id);
    await this._persistPush(id, record);
  }

  async _persistBlock() {}
  async _persistPush() {}
}

/**
 * Postgres-backed variant. `pool` is a pg Pool (anything with .query(text, params)); createStore() builds the real one.
 * Two tables, created on init. Write failures are thrown to the caller (hub logs them) but the in-memory copy stays updated.
 */
export class PostgresStore extends MemoryStore {
  constructor(pool) {
    super();
    this.pool = pool;
  }

  async init() {
    await this.pool.query(`CREATE TABLE IF NOT EXISTS hub_blocks (
      blocker TEXT NOT NULL, target TEXT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
      PRIMARY KEY (blocker, target))`);
    await this.pool.query(`CREATE TABLE IF NOT EXISTS hub_push (
      id TEXT PRIMARY KEY, token TEXT NOT NULL, env TEXT NOT NULL, name TEXT NOT NULL DEFAULT '',
      friends JSONB NOT NULL DEFAULT '[]', updated_at TIMESTAMPTZ NOT NULL DEFAULT now())`);
    for (const r of (await this.pool.query('SELECT blocker, target FROM hub_blocks')).rows) {
      if (!this.blocks.has(r.blocker)) this.blocks.set(r.blocker, new Set());
      this.blocks.get(r.blocker).add(r.target);
    }
    for (const r of (await this.pool.query('SELECT id, token, env, name, friends FROM hub_push')).rows) {
      this.push.set(r.id, { token: r.token, env: r.env, name: r.name, friends: r.friends ?? [] });
    }
  }

  async close() { await this.pool.end?.(); }

  async _persistBlock(blocker, target, on) {
    if (on) await this.pool.query('INSERT INTO hub_blocks (blocker, target) VALUES ($1, $2) ON CONFLICT DO NOTHING', [blocker, target]);
    else await this.pool.query('DELETE FROM hub_blocks WHERE blocker = $1 AND target = $2', [blocker, target]);
  }

  async _persistPush(id, rec) {
    if (!rec) { await this.pool.query('DELETE FROM hub_push WHERE id = $1', [id]); return; }
    await this.pool.query(
      `INSERT INTO hub_push (id, token, env, name, friends, updated_at) VALUES ($1, $2, $3, $4, $5::jsonb, now())
       ON CONFLICT (id) DO UPDATE SET token = $2, env = $3, name = $4, friends = $5::jsonb, updated_at = now()`,
      [id, rec.token, rec.env, rec.name ?? '', JSON.stringify(rec.friends ?? [])],
    );
  }
}

/** DATABASE_URL set => Postgres (pg is an optional dependency, loaded only then); otherwise memory. */
export async function createStore({ databaseUrl, log = console } = {}) {
  if (!databaseUrl) {
    log.warn?.('DATABASE_URL is not set: blocks and push tokens are kept in memory (lost on restart).');
    return new MemoryStore();
  }
  const { default: pg } = await import('pg');
  const store = new PostgresStore(new pg.Pool({ connectionString: databaseUrl }));
  await store.init();
  return store;
}
