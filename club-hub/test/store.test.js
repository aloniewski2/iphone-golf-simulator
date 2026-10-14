import assert from 'node:assert/strict';
import test from 'node:test';
import { MemoryStore, PostgresStore, createStore } from '../src/store.js';
import { CODE, quietLog, sleep, startHub } from './helpers/harness.js';

test('memory store: blocks are directional, isBlocked is either way, push records round-trip', async () => {
  const s = new MemoryStore();
  await s.block('a', 'b');
  assert.equal(s.hasBlocked('a', 'b'), true);
  assert.equal(s.hasBlocked('b', 'a'), false);
  assert.equal(s.isBlocked('b', 'a'), true);
  await s.unblock('a', 'b');
  assert.equal(s.isBlocked('a', 'b'), false);
  await s.setPush('a', { token: 't', env: 'sandbox', name: 'A', friends: ['b'] });
  assert.equal(s.getPush('a').token, 't');
  await s.setPush('a', null);
  assert.equal(s.getPush('a'), null);
});

test('blocks live in the store, so a new hub on the same store still enforces them', async (t) => {
  const first = await startHub();
  const a = await first.join('A', { friends: ['B'] });
  await a.say({ t: 'block', id: 'B' });
  await sleep(50);
  const store = first.store;
  await first.stop();

  const second = await startHub({ store });
  t.after(() => second.stop());
  const A = await second.join('A', { friends: ['B'] });
  const B = await second.join('B', { friends: ['A'] });
  await A.none('presence');
  await B.none('presence');
  await B.say({ t: 'invite', to: 'A', sport: 'tennis', lobbyCode: CODE, group: 1, capacity: 2 });
  assert.equal((await B.next('error')).code, 'not_friends');
});

test('createStore without DATABASE_URL gives the in-memory store', async () => {
  const s = await createStore({ databaseUrl: '', log: quietLog });
  assert.ok(s instanceof MemoryStore);
});

/** A tiny stand-in for pg.Pool: remembers statements and serves canned rows. Verifies the glue, not real SQL. */
function fakePool(rows = {}) {
  const calls = [];
  return {
    calls,
    async query(text, params) {
      calls.push({ text: text.replace(/\s+/g, ' ').trim(), params });
      if (text.includes('FROM hub_blocks')) return { rows: rows.blocks ?? [] };
      if (text.includes('FROM hub_push')) return { rows: rows.push ?? [] };
      return { rows: [] };
    },
    async end() { calls.push({ text: 'END' }); },
  };
}

test('postgres store: creates tables, loads rows, writes through', async () => {
  const pool = fakePool({
    blocks: [{ blocker: 'a', target: 'b' }],
    push: [{ id: 'c', token: 'tok', env: 'production', name: 'C', friends: ['a'] }],
  });
  const s = new PostgresStore(pool);
  await s.init();
  assert.ok(pool.calls.some((c) => c.text.startsWith('CREATE TABLE IF NOT EXISTS hub_blocks')));
  assert.ok(pool.calls.some((c) => c.text.startsWith('CREATE TABLE IF NOT EXISTS hub_push')));
  assert.equal(s.isBlocked('b', 'a'), true);
  assert.deepEqual(s.getPush('c'), { token: 'tok', env: 'production', name: 'C', friends: ['a'] });

  await s.block('x', 'y');
  await s.unblock('a', 'b');
  await s.setPush('d', { token: 't2', env: 'sandbox', name: 'D', friends: ['x'] });
  await s.setPush('d', null);
  await s.close();
  const texts = pool.calls.map((c) => c.text);
  assert.ok(texts.some((x) => x.startsWith('INSERT INTO hub_blocks')));
  assert.ok(texts.some((x) => x.startsWith('DELETE FROM hub_blocks')));
  assert.ok(texts.some((x) => x.startsWith('INSERT INTO hub_push')));
  assert.ok(texts.some((x) => x.startsWith('DELETE FROM hub_push')));
  assert.equal(texts.at(-1), 'END');
  const upsert = pool.calls.find((c) => c.text.startsWith('INSERT INTO hub_push'));
  assert.deepEqual(upsert.params, ['d', 't2', 'sandbox', 'D', '["x"]']);
  assert.equal(s.isBlocked('a', 'b'), false);
});
