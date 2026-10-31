import type { NextFunction, Request, Response } from 'express';

/**
 * READ-ONLY CHAIN (SHOWCASE.md step 2): `CHAIN_WRITES=off` on an instance that must never sign - the
 * showcase's mainnet service (the user's call, 2026-10-31: no chain write on the showcase). Held at
 * three places, so no one route forgetting can let a write through:
 *
 *   - getLucid() refuses (chain.ts) - nothing can sign, whatever calls it;
 *   - every route that mints, burns or signs answers 403 before doing any work (index.ts);
 *   - nothing is DEFERRED to mint later: commit-run mints nothing and queues nothing, and the
 *     start-up release of deferred mints does not run (routes/player.ts, index.ts).
 *
 * Reads, sign-in (CIP-30 signData - no transaction), saves and runs work as ever. Run it with no
 * SERVICE_WALLET_SEED at all: there is then no key on the instance to misuse.
 */
export const chainWritesOff = (): boolean => (process.env.CHAIN_WRITES ?? 'on').trim().toLowerCase() === 'off';

export const READ_ONLY_MESSAGE =
  'This is the showcase - it writes nothing to the chain. Minting, portraits and dyes come with the full beta.';

/** A route that signs: refused outright on a read-only instance. */
export function noChainWrites(_req: Request, res: Response, next: NextFunction): void {
  if (!chainWritesOff()) { next(); return; }
  res.status(403).json({ error: READ_ONLY_MESSAGE, code: 'read_only', message: READ_ONLY_MESSAGE });
}
