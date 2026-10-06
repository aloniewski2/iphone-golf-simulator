import { createServer } from './server.js';
import { MemoryStore } from './store/memory.js';
import { PostgresStore } from './store/postgres.js';

const port = Number.parseInt(process.env.PORT ?? '8080', 10);
const databaseUrl = process.env.DATABASE_URL;

const store = databaseUrl ? await PostgresStore.connect(databaseUrl) : new MemoryStore();
if (!databaseUrl) console.warn('DATABASE_URL is not set: keeping players in memory (lost on restart).');

const { server } = createServer({ store, trustProxy: process.env.TRUST_PROXY === '1' });
server.listen(port, () => console.log(`Golf Arcade server on :${port}`));

const shutdown = () => {
  server.close(async () => { await store.close(); process.exit(0); });
  setTimeout(() => process.exit(0), 5000).unref();
};
process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);
