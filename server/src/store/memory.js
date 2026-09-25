import { randomUUID } from 'node:crypto';
import { addRound, emptyStats } from '../game.js';

/** Everything in process memory: for tests and for running the server locally without a
 * database. Same interface as PostgresStore. */
export class MemoryStore {
  constructor() {
    this.players = new Map();
    this.byToken = new Map();
    this.rounds = [];
  }

  async createPlayer({ name, body, kit, shirt, tokenHash }) {
    const now = new Date().toISOString();
    const player = { id: randomUUID(), name, body, kit, shirt, stats: emptyStats(), createdAt: now, updatedAt: now };
    this.players.set(player.id, player);
    this.byToken.set(tokenHash, player.id);
    return { ...player };
  }

  async playerByTokenHash(tokenHash) {
    const id = this.byToken.get(tokenHash);
    return id ? this.playerById(id) : null;
  }

  async playerById(id) {
    const p = this.players.get(id);
    return p ? { ...p, stats: { ...p.stats } } : null;
  }

  async updatePlayer(id, fields) {
    const p = this.players.get(id);
    if (!p) return null;
    for (const key of ['name', 'body', 'kit', 'shirt']) if (fields[key] !== undefined) p[key] = fields[key];
    p.updatedAt = new Date().toISOString();
    return this.playerById(id);
  }

  async recordRound({ playerId, course, strokes, toPar, result = null, roomCode = null }) {
    const p = this.players.get(playerId);
    if (!p) return null;
    p.stats = addRound(p.stats, course, strokes, result);
    this.rounds.push({ playerId, course, strokes: [...strokes], toPar, result, roomCode, createdAt: new Date().toISOString() });
    return { ...p.stats };
  }

  async recentRounds(playerId, limit) {
    return this.rounds.filter((r) => r.playerId === playerId).slice(-limit).reverse()
      .map(({ course, strokes, toPar, result, createdAt }) => ({ course, strokes, toPar, result, createdAt }));
  }

  async leaderboard(course, limit) {
    const best = new Map();
    for (const r of this.rounds) {
      if (r.course !== course) continue;
      const cur = best.get(r.playerId);
      if (!cur || r.toPar < cur.toPar) best.set(r.playerId, r);
    }
    return [...best.values()]
      .sort((a, b) => a.toPar - b.toPar || a.createdAt.localeCompare(b.createdAt))
      .slice(0, limit)
      .map((r) => ({ playerId: r.playerId, name: this.players.get(r.playerId).name, bestToPar: r.toPar }));
  }

  async close() {}
}
