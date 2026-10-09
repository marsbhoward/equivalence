import { mkdir, readFile, rename, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';

/**
 * The service's own records - one JSON file on the volume (`LEDGER_PATH`, /data/ledger.json on
 * Railway), written whole and renamed, every change serialised (artIndex.ts's pattern).
 *
 *   characters  per profile id: its owner, and the runs the server saw START - the only runs a
 *               run-end checkpoint may mint boxes for, and each only once.
 *   deposits    per address and box tier: the ADA the service sent along with boxes it minted
 *               there, and how many boxes that covered. Opening boxes RECLAIMS each box's share
 *               (the service funded it - the user's call). Boxes that were traded arrive at an
 *               address with no pool, and reclaim nothing.
 *   pending     rolls COMMITTED before the player signs (routes/openBoxes.ts): designs rolled for
 *               an address / tier / target that have not been opened yet. Declining the wallet
 *               dialog returns the same designs next time - otherwise cancelling is a free reroll.
 *
 * Box BALANCES are not here: a box is a token, and the wallet holding it is the balance.
 */
export type BoxTier = 'Diamond' | 'BlackDiamond';

export interface RunRecord {
  begunAt: string;
  committedAt?: string;
  minted?: Partial<Record<BoxTier, number>>;
  hash?: string;
  /** Set while its boxes are being minted, so two commits racing can never mint twice. */
  minting?: boolean;
}

export interface CharacterLedger {
  owner: string;
  runs: Record<string, RunRecord>;
}

export interface DepositPool {
  lovelace: number;
  boxes: number;
}

export interface PendingPiece {
  instanceId: string;
  design: string;
}

interface LedgerFile {
  version: 2;
  characters: Record<string, CharacterLedger>;
  deposits: Record<string, Partial<Record<BoxTier, DepositPool>>>;
  pending: Record<string, PendingPiece[][]>;
}

const PATH = process.env.LEDGER_PATH || './ledger.json';

let loaded: Promise<LedgerFile> | null = null;
let queue: Promise<unknown> = Promise.resolve();

function load(): Promise<LedgerFile> {
  loaded ??= readFile(PATH, 'utf8')
    .then((t) => {
      const f = JSON.parse(t);
      // Version 1 kept a box BALANCE per character - boxes are tokens now. Runs and owners carry over.
      for (const c of Object.values(f.characters ?? {}) as Record<string, unknown>[]) delete c.boxes;
      return { version: 2, characters: f.characters ?? {}, deposits: f.deposits ?? {}, pending: f.pending ?? {} } as LedgerFile;
    })
    .catch((e: NodeJS.ErrnoException) => {
      if (e.code === 'ENOENT') return { version: 2, characters: {}, deposits: {}, pending: {} } as LedgerFile;
      throw e; // a corrupt ledger must never silently become an empty one
    });
  return loaded;
}

/** A read-only copy of the whole file. */
export async function snapshot(): Promise<LedgerFile> {
  return structuredClone(await load());
}

/**
 * Read-modify-write, serialised. `change` gets a COPY of the file; throwing refuses and writes
 * nothing; returning `write: false` reads without writing.
 */
export function transact<T>(change: (f: LedgerFile) => { result: T; write?: boolean }): Promise<T> {
  const step = queue.then(async () => {
    const file = await load();
    const copy = structuredClone(file);
    const { result, write = true } = change(copy);
    if (write) {
      Object.assign(file, copy);
      await mkdir(dirname(PATH), { recursive: true });
      await writeFile(`${PATH}.tmp`, JSON.stringify(file, null, 2));
      await rename(`${PATH}.tmp`, PATH);
    }
    return result;
  });
  queue = step.catch(() => {});
  return step;
}

export const fresh = (owner: string): CharacterLedger => ({ owner, runs: {} });

/** The key a pending roll is kept under: who, which box, which target. */
export const pendingKey = (address: string, tier: BoxTier, slot?: string, weaponClass?: string) =>
  [address, tier, slot ?? '*', weaponClass ?? '*'].join('|');
