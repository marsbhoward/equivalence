import pg from 'pg';

/**
 * The service's database - Postgres (`DATABASE_URL`; Railway's Postgres in the same project).
 *
 * WHY NOT THE VOLUME. ledger.json is one file rewritten whole on every change, which is fine for
 * runs and boxes and hopeless for every guest's saves; and Railway will not run replicas of a
 * service with a volume attached. Saves live here so the API holds no state of its own.
 *
 * Migrations are numbered SQL run once each at start-up, in order, inside one transaction apiece -
 * append a new one, never edit a shipped one.
 */
let pool: pg.Pool | null = null;

export function db(): pg.Pool {
  if (pool) return pool;
  const url = process.env.DATABASE_URL;
  if (!url) throw new Error('DATABASE_URL is not set');
  pool = new pg.Pool({
    connectionString: url,
    max: Number(process.env.DATABASE_POOL ?? 10),
    // Railway's private network needs no TLS; its public proxy and most hosts want it.
    ssl: /sslmode=require|\.rlwy\.net|\.railway\.app/.test(url) ? { rejectUnauthorized: false } : undefined,
  });
  return pool;
}

export const hasDatabase = () => !!process.env.DATABASE_URL;

/** One statement, rows back. */
export async function q<T extends pg.QueryResultRow = pg.QueryResultRow>(
  text: string,
  params: unknown[] = [],
): Promise<T[]> {
  return (await db().query<T>(text, params)).rows;
}

/** Several statements as one transaction - the callback's error rolls every one of them back. */
export async function tx<T>(work: (c: pg.PoolClient) => Promise<T>): Promise<T> {
  const c = await db().connect();
  try {
    await c.query('BEGIN');
    const out = await work(c);
    await c.query('COMMIT');
    return out;
  } catch (e) {
    await c.query('ROLLBACK').catch(() => {});
    throw e;
  } finally {
    c.release();
  }
}

const MIGRATIONS: string[] = [
  // 1 - guests, saves, accounts, and the token-metadata cache
  `
  CREATE TABLE guests (
    id            TEXT PRIMARY KEY,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_ip    TEXT,
    -- set once, when the guest's records move onto a proven wallet; the guest token is spent
    graduated_to  TEXT,
    graduated_at  TIMESTAMPTZ
  );

  -- One row per character. OWNER is a wallet address (bech32) or 'guest:<id>'. A save is the
  -- game's own CharacterProfile JSON, stored verbatim - the service records what the game owns,
  -- it does not recompute it. total_runs is pulled out of it for the guard against a save going
  -- backwards (a failed read starting a fresh character must never overwrite the real one).
  CREATE TABLE characters (
    owner         TEXT NOT NULL,
    profile_id    TEXT NOT NULL,
    save          JSONB NOT NULL,
    total_runs    INTEGER NOT NULL DEFAULT 0,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (owner, profile_id)
  );

  -- The last saves of each character, newest kept, for recovery by hand. Pruned on write.
  CREATE TABLE character_history (
    id            BIGSERIAL PRIMARY KEY,
    owner         TEXT NOT NULL,
    profile_id    TEXT NOT NULL,
    reason        TEXT NOT NULL,
    run_id        TEXT,
    save          JSONB NOT NULL,
    saved_at      TIMESTAMPTZ NOT NULL DEFAULT now()
  );
  CREATE INDEX character_history_by_character ON character_history (owner, profile_id, id DESC);

  -- The PLAYER (IAccountStore): the hub room, keyed by the same owner as the characters.
  CREATE TABLE accounts (
    owner         TEXT PRIMARY KEY,
    save          JSONB NOT NULL,
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now()
  );

  -- Blockfrost's /assets/<unit>, cached (assetCache.ts): every call the service makes comes from
  -- one IP, and Blockfrost's rate limit is per IP.
  CREATE TABLE asset_cache (
    unit          TEXT PRIMARY KEY,
    info          JSONB NOT NULL,
    fetched_at    TIMESTAMPTZ NOT NULL DEFAULT now()
  );
  `,

  // 2 - floor 100's box, decided by the service (finalFloor.ts): one row per run that claimed it.
  // GUARANTEED marks the week's guaranteed box - at most one per character per week.
  `
  CREATE TABLE final_floor (
    profile_id    TEXT NOT NULL,
    run_id        TEXT NOT NULL,
    owner         TEXT NOT NULL,
    week          INTEGER NOT NULL,
    guaranteed    BOOLEAN NOT NULL,
    tier          TEXT NOT NULL,
    count         INTEGER NOT NULL,
    decided_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (profile_id, run_id)
  );
  CREATE UNIQUE INDEX final_floor_one_guarantee_a_week ON final_floor (profile_id, week) WHERE guaranteed;
  `,

  // 3 - runs in flight (routes/runs.ts): one row per character, the run it is on. SNAPSHOT is the
  // game's RunSnapshot JSON at a floor's start or clear; BEAT the health and armour since it,
  // meaningful only while beat_seq = seq. SESSION is the one device allowed to write it.
  `
  CREATE TABLE runs (
    owner         TEXT NOT NULL,
    profile_id    TEXT NOT NULL,
    run_id        TEXT NOT NULL,
    session       TEXT NOT NULL,
    seq           INTEGER NOT NULL,
    floor         INTEGER NOT NULL,
    stage         TEXT NOT NULL,
    snapshot      JSONB NOT NULL,
    beat          JSONB,
    beat_seq      INTEGER,
    started_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    saved_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    beat_at       TIMESTAMPTZ,
    PRIMARY KEY (owner, profile_id)
  );
  `,
];

/** Applies every migration not yet applied. Called once at start-up. */
export async function migrate(): Promise<void> {
  await q(`CREATE TABLE IF NOT EXISTS schema_migrations (
             version INTEGER PRIMARY KEY, applied_at TIMESTAMPTZ NOT NULL DEFAULT now())`);
  const done = new Set((await q<{ version: number }>('SELECT version FROM schema_migrations')).map(r => r.version));
  for (let i = 0; i < MIGRATIONS.length; i++) {
    const version = i + 1;
    if (done.has(version)) continue;
    await tx(async c => {
      // Two replicas starting together: the second waits here, then finds it applied.
      await c.query('SELECT pg_advisory_xact_lock(4242)');
      const again = await c.query('SELECT 1 FROM schema_migrations WHERE version = $1', [version]);
      if (again.rowCount) return;
      await c.query(MIGRATIONS[i]);
      await c.query('INSERT INTO schema_migrations (version) VALUES ($1)', [version]);
    });
    console.log(`[db] migration ${version} applied`);
  }
}
