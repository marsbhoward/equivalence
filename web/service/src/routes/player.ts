import type { Request, Response } from 'express';
import { internal } from '../errors.js';
import { CML } from '@lucid-evolution/lucid';
import { bf } from '../blockfrost.js';
import { fundingInputs, getLucid, serialized, serviceAddress, signAndSubmit } from '../chain.js';
import { characterUnits } from '../characters.js';
import { referenceDatum } from '../cip68.js';
import { BOX_INFO, BOX_TIERS, BoxTier, boxPolicy, boxUnits, boxesHeld } from '../boxes.js';
import { transact } from '../ledger.js';
import { decisionFor } from '../finalFloor.js';
import { hasDatabase, q, tx } from '../db.js';
import { allowance } from '../runProgress.js';
import { recordedFormat } from '../runFormat.js';
import { avatarDecisions } from '../avatarBoxes.js';
import { pfpGate } from './mintDesign.js';
import { milestoneBoxes } from './items.js';
import { db } from '../db.js';

/**
 * The PLAYER's writes - called by the browser build through the connector, authorised by a
 * session (session.ts), never by WRITE_TOKEN. The proven address is `res.locals.address`.
 *
 *   POST /write/begin-run    confirms the run the service saw start (the run-start save)
 *   POST /write/commit-run   EXTRACTION: the run's secured Diamond / Black Diamond boxes - minted
 *                            to the wallet as tokens if it holds the character's PFP, HELD on the
 *                            service otherwise - once per run
 *   GET  /boxes/:profileId   the boxes the WALLET holds - a box is a token, the wallet is the balance
 *
 * A RUN IS WHAT THE SERVICE SAW (runProgress.ts): its start on the server's clock (the run-start
 * save, routes/saves.ts) and how far its snapshots got (routes/runs.ts). The run summary a commit
 * carries is only a CLAIM, cut back to that record - so a client that claims floors it never sent
 * snapshots for, or faster than the clock allows, mints nothing for them - and every box it may
 * hold was ROLLED by the service (avatarBoxes.ts, finalFloor.ts), so a claim can only lower it. Wallet saves are keyed by
 * the address, so the character must be one this address saves.
 *
 * Opening boxes is routes/openBoxes.ts. begin-run answers `{ hash: "ledger:..." }` - not empty
 * (the connector records "" as a FAILED checkpoint) and not pretending to be a transaction.
 */

const ID = /^[A-Za-z0-9_.:-]{1,96}$/;

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

/** Whether this address saves the character - the one owner the service knows (routes/saves.ts). */
async function savesCharacter(address: string, profileId: string): Promise<boolean> {
  return (await q('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2', [address, profileId])).length > 0;
}

interface ProgressRow {
  reached: number;
  cleared: number;
  elapsed: number;
  minting: boolean;
  committed_at: Date | null;
  minted: Partial<Record<BoxTier, number>> | null;
  held: Partial<Record<BoxTier, number>> | null;
  hash: string | null;
  format: string;
}

export async function beginRun(req: Request, res: Response): Promise<void> {
  const address = res.locals.address as string;
  const { profileId, runId } = (req.body ?? {}) as Record<string, string>;
  if (!ID.test(profileId ?? '') || !ID.test(runId ?? '')) return fail(res, 400, 'profileId and runId are required');
  if (!hasDatabase()) return fail(res, 503, 'runs need the database');
  try {
    // The run-start save already registered the run (runProgress.recordRunStart) - this only
    // confirms it, so a client learns at the START that its end will have nothing to mint against.
    const rows = await q('SELECT 1 FROM run_progress WHERE owner = $1 AND profile_id = $2 AND run_id = $3',
      [address, profileId, runId]);
    if (!rows.length) return fail(res, 409, 'unknown_run', { runId });
    res.json({ hash: `ledger:run-start:${runId}` });
  } catch (e) {
    internal(res, e, 'player');
  }
}

/**
 * Mints `counts` boxes to `address` in ONE output, creating a tier's reference token (its CIP-68
 * metadata) the first time that tier is ever minted. Returns the hash and the ADA that travelled
 * with the boxes - the deposit the service reclaims when they are opened.
 */
async function mintBoxes(address: string, counts: Partial<Record<BoxTier, number>>) {
  return serialized(async () => {
    const lucid = await getLucid();
    const { script } = await boxPolicy();
    const service = await serviceAddress();
    let tx = lucid.newTx().attach.MintingPolicy(script).validFrom(Date.now() - 3_600_000);
    const mint: Record<string, bigint> = {};
    const sent: Record<string, bigint> = {};
    for (const tier of BOX_TIERS) {
      const n = counts[tier] ?? 0;
      if (n <= 0) continue;
      const { ft, reference } = await boxUnits(tier);
      mint[ft] = BigInt(n);
      sent[ft] = BigInt(n);
      if (!(await bf(`/assets/${reference}`))) {
        const info = BOX_INFO[tier];
        mint[reference] = 1n;
        tx = tx.pay.ToAddressWithData(
          service,
          { kind: 'inline', value: referenceDatum({ ...info, decimals: 0 }, 1) },
          { [reference]: 1n },
        );
      }
    }
    tx = tx.mintAssets(mint).pay.ToAddress(address, sent);
    const built = await tx.complete({ presetWalletInputs: await fundingInputs() });

    // The ADA the builder put with the boxes (the ledger's minimum for that output) - the only
    // output to the player's address.
    let deposit = 0;
    const outputs = CML.Transaction.from_cbor_hex(built.toCBOR()).body().outputs();
    for (let i = 0; i < outputs.len(); i++) {
      const o = outputs.get(i);
      if (o.address().to_bech32(undefined) === address) deposit = Number(o.amount().coin());
    }
    const hash = await signAndSubmit(built);
    return { hash, deposit };
  });
}

export async function commitRun(req: Request, res: Response): Promise<void> {
  const address = res.locals.address as string;
  const { profileId, summary } = (req.body ?? {}) as Record<string, string>;
  if (!ID.test(profileId ?? '')) return fail(res, 400, 'profileId is required');
  if (!hasDatabase()) return fail(res, 503, 'runs need the database');

  let s: Record<string, unknown>;
  try {
    s = JSON.parse(summary ?? '');
  } catch {
    return fail(res, 400, 'summary must be the RunSummary JSON');
  }
  const runId = String(s.RunId ?? '');
  if (!ID.test(runId)) return fail(res, 400, 'summary.RunId is required');
  const floors = Math.max(0, Math.floor(Number(s.Floors ?? 0)));
  const claimed: Record<BoxTier, number> = {
    Diamond: Math.max(0, Math.floor(Number(s.SecuredDiamondBoxes ?? 0))),
    BlackDiamond: Math.max(0, Math.floor(Number(s.SecuredBlackDiamondBoxes ?? 0))),
  };

  try {
    // CLAIM the run first (marking it in flight), so two commits racing can never mint twice.
    const claim = await tx(async (c) => {
      const r = await c.query<ProgressRow>(
        `SELECT reached, cleared, EXTRACT(EPOCH FROM now() - started_at)::float AS elapsed,
                minting, committed_at, minted, held, hash, format
           FROM run_progress WHERE owner = $1 AND profile_id = $2 AND run_id = $3 FOR UPDATE`,
        [address, profileId, runId]);
      const row = r.rows[0];
      // Only a run the service saw START, for a character this address saves.
      if (!row) return { status: 'unknown' as const };
      if (row.committed_at) return { status: 'repeat' as const, row };
      if (row.minting) return { status: 'busy' as const };
      await c.query('UPDATE run_progress SET minting = true WHERE owner = $1 AND profile_id = $2 AND run_id = $3',
        [address, profileId, runId]);
      return { status: 'claimed' as const, row };
    });
    if (claim.status === 'unknown') return fail(res, 409, 'unknown_run', { runId });
    if (claim.status === 'busy') return fail(res, 409, 'commit_in_flight', { runId });
    if (claim.status === 'repeat')
      return void res.json({
        hash: claim.row.hash ?? `ledger:run-end:${runId}`,
        minted: claim.row.minted ?? {}, held: claim.row.held ?? {}, repeat: true,
      });

    // From here on the run is marked in flight. Any failure BEFORE a mint is submitted releases it,
    // so the game's retry can commit; after a submitted mint it stays marked - better a run stuck
    // for a look by hand than one that mints twice.
    let submitted = false;
    try {
      const seen = { reached: claim.row.reached, cleared: claim.row.cleared, elapsedSeconds: claim.row.elapsed };
      const final = await decisionFor(address, profileId, runId);
      const avatars = await avatarDecisions(address, profileId, runId);
      const milestones = await milestoneBoxes(db(), address, profileId, runId);
      const { allowed, clamped, floor, timeFloor } = allowance(floors, claimed, seen, avatars, final, recordedFormat(claim.row.format), milestones);
      if (Object.keys(clamped).length)
        console.warn(`[commit-run] ${profileId} run ${runId} claimed past what was seen and decided:`, clamped,
          `claimed floor ${floors}, snapshots ${seen.reached}/${seen.cleared} cleared, clock allows ${timeFloor}, counted ${floor}`);

      // NOTHING EARNED REACHES A WALLET WITHOUT A PFP (the user's rule, 2026-10-13): with no PFP for
      // this character, the boxes are HELD here - a record the client cannot write - not minted.
      const any = Object.keys(allowed).length > 0;
      const mintable = any && (await pfpGate(address, (await characterUnits(profileId)).user)) === null;

      let hash = `ledger:run-end:${runId}`;
      let deposit = 0;
      if (mintable) {
        ({ hash, deposit } = await mintBoxes(address, allowed));
        submitted = true;
      }
      const minted = mintable ? allowed : {};
      const held = any && !mintable ? allowed : {};

      await tx(async (c) => {
        await c.query(
          `UPDATE run_progress SET minting = false, committed_at = now(), minted = $4, held = $5, hash = $6
            WHERE owner = $1 AND profile_id = $2 AND run_id = $3`,
          [address, profileId, runId, minted, held, hash]);
        for (const [tier, n] of Object.entries(held))
          await c.query('INSERT INTO held_boxes (owner, profile_id, run_id, tier, count) VALUES ($1, $2, $3, $4, $5)',
            [address, profileId, runId, tier, n]);
      });

      // The deposit is reclaimed per box when they are opened - split across tiers by count.
      const total = Object.values(minted).reduce((a, b) => a + (b ?? 0), 0);
      if (total > 0)
        await transact((f) => {
          for (const tier of BOX_TIERS) {
            const n = minted[tier] ?? 0;
            if (!n) continue;
            const pool = ((f.deposits[address] ??= {})[tier] ??= { lovelace: 0, boxes: 0 });
            pool.lovelace += Math.floor((deposit * n) / total);
            pool.boxes += n;
          }
          return { result: null };
        });
      res.json({ hash, minted, held, clamped, repeat: false });
    } catch (e) {
      if (!submitted)
        await q('UPDATE run_progress SET minting = false WHERE owner = $1 AND profile_id = $2 AND run_id = $3',
          [address, profileId, runId]).catch(() => {});
      throw e;
    }
  } catch (e) {
    internal(res, e, 'player');
  }
}

export async function boxes(req: Request, res: Response): Promise<void> {
  const address = res.locals.address as string;
  const profileId = String(req.params.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  try {
    if (hasDatabase() && !(await savesCharacter(address, profileId)))
      return fail(res, 403, 'this character belongs to another address');
    const [Diamond, BlackDiamond] = await Promise.all(BOX_TIERS.map((t) => boxesHeld(address, t)));
    res.json({ profileId, boxes: { Diamond, BlackDiamond } });
  } catch (e) {
    internal(res, e, 'player');
  }
}
