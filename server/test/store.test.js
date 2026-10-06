import assert from 'node:assert/strict';
import test from 'node:test';
import { MemoryStore } from '../src/store/memory.js';
import { PostgresStore } from '../src/store/postgres.js';

// The same contract for both stores. Postgres runs when TEST_DATABASE_URL points at a scratch
// database (its tables are emptied first), e.g.
//   TEST_DATABASE_URL=postgres://golf@localhost:5432/golf_test?sslmode=disable npm test
const stores = [['memory', async () => new MemoryStore()]];
if (process.env.TEST_DATABASE_URL) {
  stores.push(['postgres', async () => {
    const store = await PostgresStore.connect(process.env.TEST_DATABASE_URL);
    await store.pool.query('truncate rounds, players');
    return store;
  }]);
}

for (const [name, make] of stores) {
  test(`${name}: players, rounds, stats and the leaderboard`, async (t) => {
    const store = await make();
    t.after(() => store.close());

    const alex = await store.createPlayer({ name: 'Alex', body: 0, kit: 1, shirt: 2, tokenHash: 'hash-a' });
    const sam = await store.createPlayer({ name: 'Sam', body: 1, kit: 3, shirt: 0, tokenHash: 'hash-b' });
    assert.equal((await store.playerByTokenHash('hash-a')).id, alex.id);
    assert.equal(await store.playerByTokenHash('nope'), null);
    assert.equal(alex.stats.roundsPlayed, 0);

    const renamed = await store.updatePlayer(alex.id, { name: 'Alexandra' });
    assert.deepEqual([renamed.name, renamed.body, renamed.kit, renamed.shirt], ['Alexandra', 0, 1, 2]);

    await store.recordRound({ playerId: alex.id, course: 'cliffside', strokes: [5, 3, 5, 4, 3], toPar: 1 });
    const stats = await store.recordRound({ playerId: alex.id, course: 'cliffside', strokes: [3, 3, 5, 4, 3], toPar: -1, result: 'won', roomCode: 'ABCD' });
    assert.equal(stats.roundsPlayed, 2);
    assert.equal(stats.bestToPar, -1);
    assert.equal(stats.matchesWon, 1);
    await store.recordRound({ playerId: sam.id, course: 'cliffside', strokes: [4, 3, 5, 4, 3], toPar: 0, result: 'lost' });

    const recent = await store.recentRounds(alex.id, 10);
    assert.equal(recent.length, 2);
    assert.deepEqual(recent[0].strokes, [3, 3, 5, 4, 3], 'newest first');
    assert.equal(recent[0].result, 'won');

    const board = await store.leaderboard('cliffside', 10);
    assert.deepEqual(board.map((e) => [e.name, e.bestToPar]), [['Alexandra', -1], ['Sam', 0]]);
    assert.deepEqual(await store.leaderboard('cliffside-12', 10), []);
    assert.equal((await store.playerById(alex.id)).stats.birdies, 1);
  });
}
