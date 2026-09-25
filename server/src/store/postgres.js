import { randomUUID } from 'node:crypto';
import { readFile } from 'node:fs/promises';
import pg from 'pg';
import { addRound, emptyStats } from '../game.js';

const row = (r) => r && {
  id: r.id, name: r.name, body: r.body, kit: r.kit, shirt: r.shirt,
  stats: { ...emptyStats(), ...r.stats },
  createdAt: r.created_at.toISOString(), updatedAt: r.updated_at.toISOString(),
};

/** Players and rounds in Postgres (any managed Postgres: Cloud SQL, RDS, Neon, Supabase, …). */
export class PostgresStore {
  static async connect(connectionString) {
    const ssl = /sslmode=disable/.test(connectionString) ? false : { rejectUnauthorized: false };
    const pool = new pg.Pool({ connectionString, ssl, max: 10 });
    const schema = await readFile(new URL('./schema.sql', import.meta.url), 'utf8');
    await pool.query(schema);
    return new PostgresStore(pool);
  }

  constructor(pool) { this.pool = pool; }

  async createPlayer({ name, body, kit, shirt, tokenHash }) {
    const { rows } = await this.pool.query(
      `insert into players (id, name, body, kit, shirt, token_hash, stats) values ($1, $2, $3, $4, $5, $6, $7) returning *`,
      [randomUUID(), name, body, kit, shirt, tokenHash, emptyStats()]);
    return row(rows[0]);
  }

  async playerByTokenHash(tokenHash) {
    const { rows } = await this.pool.query('select * from players where token_hash = $1', [tokenHash]);
    return row(rows[0]) ?? null;
  }

  async playerById(id) {
    const { rows } = await this.pool.query('select * from players where id = $1', [id]);
    return row(rows[0]) ?? null;
  }

  async updatePlayer(id, fields) {
    const { rows } = await this.pool.query(
      `update players set name = coalesce($2, name), body = coalesce($3, body), kit = coalesce($4, kit),
         shirt = coalesce($5, shirt), updated_at = now() where id = $1 returning *`,
      [id, fields.name ?? null, fields.body ?? null, fields.kit ?? null, fields.shirt ?? null]);
    return row(rows[0]) ?? null;
  }

  async recordRound({ playerId, course, strokes, toPar, result = null, roomCode = null }) {
    const client = await this.pool.connect();
    try {
      await client.query('begin');
      const { rows } = await client.query('select stats from players where id = $1 for update', [playerId]);
      if (!rows[0]) { await client.query('rollback'); return null; }
      const stats = addRound(rows[0].stats, course, strokes, result);
      await client.query('update players set stats = $2, updated_at = now() where id = $1', [playerId, stats]);
      await client.query(
        'insert into rounds (player_id, course, strokes, to_par, result, room_code) values ($1, $2, $3, $4, $5, $6)',
        [playerId, course, strokes, toPar, result, roomCode]);
      await client.query('commit');
      return stats;
    } catch (e) {
      await client.query('rollback');
      throw e;
    } finally {
      client.release();
    }
  }

  async recentRounds(playerId, limit) {
    const { rows } = await this.pool.query(
      `select course, strokes, to_par, result, created_at from rounds where player_id = $1
         order by created_at desc limit $2`, [playerId, limit]);
    return rows.map((r) => ({ course: r.course, strokes: r.strokes, toPar: r.to_par, result: r.result, createdAt: r.created_at.toISOString() }));
  }

  async leaderboard(course, limit) {
    const { rows } = await this.pool.query(
      `select b.player_id, p.name, b.to_par from (
         select distinct on (player_id) player_id, to_par, created_at from rounds
           where course = $1 order by player_id, to_par, created_at) b
       join players p on p.id = b.player_id
       order by b.to_par, b.created_at limit $2`, [course, limit]);
    return rows.map((r) => ({ playerId: r.player_id, name: r.name, bestToPar: r.to_par }));
  }

  async close() { await this.pool.end(); }
}
