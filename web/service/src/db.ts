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

/** Closes the pool - the last step of a clean shutdown (lifecycle.ts). */
export async function closeDb(): Promise<void> {
  const p = pool;
  pool = null;
  if (p) await p.end();
}

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

  // 4 - what the SERVICE saw of each run (runProgress.ts): its start on the server's clock, the
  // deepest floor its snapshots reached and cleared - the authority a run's end is minted against -
  // and the commit itself, so a run mints once. Kept after the run ends.
  // HELD_BOXES: Diamond / Black Diamond boxes a run earned for a character whose owner holds no
  // PFP. They are kept HERE, never in the save (a save is the client's to write), until the day a
  // PFP moves them on chain (MONETIZATION.md). RELEASED_AT marks that move.
  `
  CREATE TABLE run_progress (
    owner         TEXT NOT NULL,
    profile_id    TEXT NOT NULL,
    run_id        TEXT NOT NULL,
    started_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    reached       INTEGER NOT NULL DEFAULT 0,
    cleared       INTEGER NOT NULL DEFAULT 0,
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    minting       BOOLEAN NOT NULL DEFAULT false,
    committed_at  TIMESTAMPTZ,
    minted        JSONB,
    held          JSONB,
    hash          TEXT,
    PRIMARY KEY (owner, profile_id, run_id)
  );

  CREATE TABLE held_boxes (
    id            BIGSERIAL PRIMARY KEY,
    owner         TEXT NOT NULL,
    profile_id    TEXT NOT NULL,
    run_id        TEXT NOT NULL,
    tier          TEXT NOT NULL,
    count         INTEGER NOT NULL,
    held_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    released_at   TIMESTAMPTZ
  );
  CREATE INDEX held_boxes_by_character ON held_boxes (owner, profile_id) WHERE released_at IS NULL;
  `,

  // 5 - the avatars' boxes, decided by the service (avatarBoxes.ts): one row per run per avatar
  // floor that claimed them, recorded BEFORE it is answered - a retry gets the same boxes.
  `
  CREATE TABLE avatar_boxes (
    owner          TEXT NOT NULL,
    profile_id     TEXT NOT NULL,
    run_id         TEXT NOT NULL,
    floor          INTEGER NOT NULL,
    diamond        INTEGER NOT NULL,
    black_diamond  INTEGER NOT NULL,
    decided_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (owner, profile_id, run_id, floor)
  );
  `,

  // 6 - floor 100's decisions keyed by the CHARACTER, which is (owner, profile id) like every save:
  // keyed by the id alone, another owner using the same id could spend someone's weekly guarantee.
  // A graduating guest's rows move with it (routes/saves.ts).
  `
  DROP INDEX final_floor_one_guarantee_a_week;
  ALTER TABLE final_floor DROP CONSTRAINT final_floor_pkey;
  ALTER TABLE final_floor ADD PRIMARY KEY (owner, profile_id, run_id);
  CREATE UNIQUE INDEX final_floor_one_guarantee_a_week ON final_floor (owner, profile_id, week) WHERE guaranteed;
  `,

  // 7 - the FORMAT a run started under (runFormat.ts): Classic's 100 floors or the Short Descent's
  // 50. Every run recorded before formats was Classic.
  `
  ALTER TABLE run_progress ADD COLUMN format TEXT NOT NULL DEFAULT 'classic';
  `,

  // 8 - the RUN CLOCK, measured by the service (runProgress.recordSnapshot): the seconds of every
  // floor attempt closed so far, the attempt open now (its floor, start, session), and the last
  // time the service heard from the run.
  `
  ALTER TABLE run_progress
    ADD COLUMN clock_seconds   DOUBLE PRECISION NOT NULL DEFAULT 0,
    ADD COLUMN attempt_floor   INTEGER,
    ADD COLUMN attempt_at      TIMESTAMPTZ,
    ADD COLUMN attempt_session TEXT,
    ADD COLUMN last_seen       TIMESTAMPTZ;
  `,

  // 9 - THE COSMETIC ECONOMY held per character (cosmetics.ts): earned dyes, dye charges and poses
  // as counts (kind dye / charge / pose), what each design is dyed, the once-per-character
  // milestones (a milestone's Diamond box is a run decision like an avatar's - commit-run mints it),
  // and a log of every change. The avatars' and the final floor's decisions gain their rare dye
  // and pose (and the Gold a pose pays once every pose is owned).
  `
  CREATE TABLE character_items (
    owner         TEXT NOT NULL,
    profile_id    TEXT NOT NULL,
    kind          TEXT NOT NULL,
    item          TEXT NOT NULL,
    count         INTEGER NOT NULL CHECK (count >= 0),
    PRIMARY KEY (owner, profile_id, kind, item)
  );

  CREATE TABLE dyed_pieces (
    owner         TEXT NOT NULL,
    profile_id    TEXT NOT NULL,
    piece         TEXT NOT NULL,
    swatches      JSONB NOT NULL,
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (owner, profile_id, piece)
  );

  CREATE TABLE milestones (
    owner         TEXT NOT NULL,
    profile_id    TEXT NOT NULL,
    milestone     TEXT NOT NULL,
    run_id        TEXT NOT NULL,
    floor         INTEGER NOT NULL,
    diamond       INTEGER NOT NULL DEFAULT 0,
    dye           TEXT,
    decided_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (owner, profile_id, milestone)
  );

  CREATE TABLE item_log (
    id            BIGSERIAL PRIMARY KEY,
    owner         TEXT NOT NULL,
    profile_id    TEXT NOT NULL,
    kind          TEXT NOT NULL,
    item          TEXT NOT NULL,
    delta         INTEGER NOT NULL,
    reason        TEXT NOT NULL,
    at            TIMESTAMPTZ NOT NULL DEFAULT now()
  );
  CREATE INDEX item_log_by_character ON item_log (owner, profile_id, id);

  ALTER TABLE avatar_boxes
    ADD COLUMN dye       TEXT,
    ADD COLUMN pose      TEXT,
    ADD COLUMN pose_gold INTEGER NOT NULL DEFAULT 0;
  ALTER TABLE final_floor
    ADD COLUMN dye       TEXT,
    ADD COLUMN pose      TEXT,
    ADD COLUMN pose_gold INTEGER NOT NULL DEFAULT 0;
  `,
  // 10 - signing out everywhere (session.ts): a session carries its address's epoch, and bumping
  // the epoch ends every session issued before it.
  `
  CREATE TABLE session_epochs (
    address       TEXT PRIMARY KEY,
    epoch         INTEGER NOT NULL,
    bumped_at     TIMESTAMPTZ NOT NULL DEFAULT now()
  );
  `,
  // 11 - pause switches (switches.ts), and an extraction's boxes DEFERRED while minting is paused:
  // judged and recorded at the commit, minted when minting resumes.
  `
  CREATE TABLE switches (
    name          TEXT PRIMARY KEY,
    paused        BOOLEAN NOT NULL,
    message       TEXT NOT NULL DEFAULT '',
    changed_at    TIMESTAMPTZ NOT NULL DEFAULT now()
  );
  ALTER TABLE run_progress ADD COLUMN deferred JSONB;
  `,

  // 12 - the run clock STOPPED mid-floor (runProgress.recordSnapshot): a failed puzzle's room, with
  // nothing to fight until the side door. When it stopped, so a floor cleared without the clock
  // ever going again is charged the whole stop.
  `
  ALTER TABLE run_progress ADD COLUMN stopped_at TIMESTAMPTZ;
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
