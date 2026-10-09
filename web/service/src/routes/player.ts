import type { Request, Response } from 'express';
import { CML } from '@lucid-evolution/lucid';
import { bf } from '../blockfrost.js';
import { fundingInputs, getLucid, serialized, serviceAddress, signAndSubmit } from '../chain.js';
import { characterUnits } from '../characters.js';
import { referenceDatum } from '../cip68.js';
import { BOX_INFO, BOX_TIERS, BoxTier, boxPolicy, boxUnits, boxesHeld } from '../boxes.js';
import { CharacterLedger, RunRecord, fresh, snapshot, transact } from '../ledger.js';

type Claim = { status: 'unknown' | 'busy' | 'claimed' } | { status: 'repeat'; run: RunRecord };

/**
 * The PLAYER's writes - called by the browser build through the connector, authorised by a
 * session (session.ts), never by WRITE_TOKEN. The proven address is `res.locals.address`.
 *
 *   POST /write/begin-run    registers a run id (the only runs a commit may mint boxes for)
 *   POST /write/commit-run   EXTRACTION: mints the run's secured Diamond / Black Diamond boxes
 *                            to the wallet as tokens, once per run - "extract it, check your
 *                            wallet, it's there" (the user's goal)
 *   GET  /boxes/:profileId   the boxes the WALLET holds - a box is a token, the wallet is the balance
 *
 * Opening boxes is routes/openBoxes.ts. begin-run answers `{ hash: "ledger:..." }` - not empty
 * (the connector records "" as a FAILED checkpoint) and not pretending to be a transaction.
 */

/**
 * The most of each tier one run can plausibly SECURE: one per avatar boss reached (floors 25, 50,
 * 75, 100). Each avatar can drop a Diamond box (2%) or a Black Diamond box (0.5%) on top of its
 * floor's loot, and floor 100's own drop is a Diamond box - or, 1% of the time, a Black Diamond
 * one instead (RunLoot). Nothing below floor 25 can hold either. A claim past the cap is clamped,
 * and the clamp is reported.
 *
 * Boxes are claimed whether the run ended in extraction or death: a box secured at a Rift, or by
 * clearing floor 100, is safe from that moment, so a death after it still mints it.
 */
const AVATAR_INTERVAL = 25;
const MAX_AVATARS = 4;
const perRunCap = (floors: number) => Math.min(MAX_AVATARS, Math.floor(floors / AVATAR_INTERVAL));

const ID = /^[A-Za-z0-9_.:-]{1,96}$/;

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

/** Whether `address` may act for this character: the ledger's owner, or the holder of its PFP. */
export async function owns(c: CharacterLedger | undefined, address: string, profileId: string): Promise<boolean> {
  if (!c || c.owner === address) return true;
  const { user } = await characterUnits(profileId);
  const held = await bf<unknown[]>(`/addresses/${address}/utxos/${user}`);
  return !!held && held.length > 0;
}

export async function beginRun(req: Request, res: Response): Promise<void> {
  const address = res.locals.address as string;
  const { profileId, runId } = (req.body ?? {}) as Record<string, string>;
  if (!ID.test(profileId ?? '') || !ID.test(runId ?? '')) return fail(res, 400, 'profileId and runId are required');
  try {
    if (!(await owns((await snapshot()).characters[profileId], address, profileId)))
      return fail(res, 403, 'this character belongs to another address');
    await transact((f) => {
      const c = (f.characters[profileId] ??= fresh(address));
      c.owner = address;   // owns() said yes - a sold character's ledger follows its token
      c.runs[runId] ??= { begunAt: new Date().toISOString() };
      return { result: null };
    });
    res.json({ hash: `ledger:run-start:${runId}` });
  } catch (e) {
    fail(res, 500, (e as Error).message);
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

  let s: Record<string, unknown>;
  try {
    s = JSON.parse(summary ?? '');
  } catch {
    return fail(res, 400, 'summary must be the RunSummary JSON');
  }
  const runId = String(s.RunId ?? '');
  const floors = Number(s.Floors ?? 0);
  const claimed: Record<BoxTier, number> = {
    Diamond: Math.max(0, Math.floor(Number(s.SecuredDiamondBoxes ?? 0))),
    BlackDiamond: Math.max(0, Math.floor(Number(s.SecuredBlackDiamondBoxes ?? 0))),
  };

  try {
    if (!(await owns((await snapshot()).characters[profileId], address, profileId)))
      return fail(res, 403, 'this character belongs to another address');

    // CLAIM the run first (marking it in flight), so two commits racing can never mint twice.
    const claim = await transact<Claim>((f) => {
      const run = f.characters[profileId]?.runs[runId];
      // Only a run the server saw START can mint, and only once.
      if (!run) return { result: { status: 'unknown' }, write: false };
      if (run.committedAt) return { result: { status: 'repeat', run }, write: false };
      if (run.minting) return { result: { status: 'busy' }, write: false };
      run.minting = true;
      return { result: { status: 'claimed' } };
    });
    if (claim.status === 'unknown') return fail(res, 409, 'unknown_run', { runId });
    if (claim.status === 'busy') return fail(res, 409, 'commit_in_flight', { runId });
    if (claim.status === 'repeat')
      return void res.json({ hash: claim.run.hash ?? `ledger:run-end:${runId}`, minted: claim.run.minted ?? {}, repeat: true });

    const minted: Partial<Record<BoxTier, number>> = {};
    const clamped: Partial<Record<BoxTier, number>> = {};
    for (const tier of BOX_TIERS) {
      const allowed = Math.min(claimed[tier], perRunCap(floors));
      if (allowed > 0) minted[tier] = allowed;
      if (allowed < claimed[tier]) clamped[tier] = claimed[tier] - allowed;
    }
    if (Object.keys(clamped).length)
      console.warn(`[commit-run] ${profileId} run ${runId} claimed past the cap:`, clamped, `floor ${floors}`);

    let hash = `ledger:run-end:${runId}`;
    let deposit = 0;
    if (Object.keys(minted).length) {
      try {
        ({ hash, deposit } = await mintBoxes(address, minted));
      } catch (e) {
        await transact((f) => {
          delete f.characters[profileId].runs[runId].minting;
          return { result: null };
        });
        throw e;
      }
    }

    const total = Object.values(minted).reduce((a, b) => a + (b ?? 0), 0);
    await transact((f) => {
      const run = f.characters[profileId].runs[runId];
      delete run.minting;
      run.committedAt = new Date().toISOString();
      run.minted = minted;
      run.hash = hash;
      // The deposit is reclaimed per box when they are opened - split across tiers by count.
      for (const tier of BOX_TIERS) {
        const n = minted[tier] ?? 0;
        if (!n) continue;
        const pool = ((f.deposits[address] ??= {})[tier] ??= { lovelace: 0, boxes: 0 });
        pool.lovelace += Math.floor((deposit * n) / total);
        pool.boxes += n;
      }
      return { result: null };
    });
    res.json({ hash, minted, clamped, repeat: false });
  } catch (e) {
    fail(res, 500, (e as Error).message);
  }
}

export async function boxes(req: Request, res: Response): Promise<void> {
  const address = res.locals.address as string;
  const profileId = req.params.profileId;
  try {
    if (!(await owns((await snapshot()).characters[profileId], address, profileId)))
      return fail(res, 403, 'this character belongs to another address');
    const [Diamond, BlackDiamond] = await Promise.all(BOX_TIERS.map((t) => boxesHeld(address, t)));
    res.json({ profileId, boxes: { Diamond, BlackDiamond } });
  } catch (e) {
    fail(res, 500, (e as Error).message);
  }
}
