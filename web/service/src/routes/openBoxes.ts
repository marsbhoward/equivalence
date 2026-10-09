import type { Request, Response } from 'express';
import { randomUUID } from 'node:crypto';
import type { Assets, UTxO } from '@lucid-evolution/lucid';
import { fundingInputs, getLucid, rememberSpentCbor, serialized } from '../chain.js';
import { characterUnits } from '../characters.js';
import { designs, pick, pool, DesignEntry, DesignPiece } from '../designs.js';
import { BOX_TIERS, BoxTier, boxPolicy, boxUnits, boxesHeld } from '../boxes.js';
import { PendingPiece, pendingKey, snapshot, transact } from '../ledger.js';
import { getGearPolicy } from '../gearPolicy.js';
import { USER_LABEL, nameBody, unitOf } from '../cip68.js';
import { MintPiece, MintRefused, addPieces, pfpGate, preflight } from './mintDesign.js';

/**
 * OPENING BOXES at the Forge - the player's boxes are TOKENS in their wallet, so opening them is
 * a transaction the player must approve (a token cannot leave a wallet without its owner). The
 * SERVICE builds it, pays its fee and adds its own witness; the player only approves:
 *
 *   POST /write/open-boxes         { profileId, tier, count 1-10 }
 *        -> { openId, cbor, pieces, boxes, reclaim }    the tx for the wallet to sign (partial)
 *   POST /write/open-boxes/submit  { openId, witness }
 *        -> { hash, tier, pieces }                       assembled, submitted
 *
 * One transaction burns the boxes, mints each design's CIP-68 pair (a Black Diamond weapon with
 * its relic), hands the player back EXACTLY what was theirs in the coins it spent, and returns
 * each box's deposit to the SERVICE, which funded it (the user's call). 1-10 boxes per approval;
 * the game plays an opening animation regardless of chain time, the reveal waiting on submission.
 *
 * ALWAYS RANDOM: one box, one design drawn from every design of the tier (the user's call,
 * 2026-10-09 - there is no targeted open for Diamond or Black Diamond).
 *
 * THE ROLL IS COMMITTED BEFORE THE SIGNATURE. Designs rolled for an open are kept PENDING (the
 * ledger) against the address / tier / target, and reused until a submitted transaction clears
 * them - declining the dialog returns the same designs next time, so cancelling is never a free
 * reroll. A new open SUPERSEDES an unsigned one for the same boxes and reuses its roll, so only one
 * transaction minting those instances can ever be submitted (the native policy would happily mint
 * them twice).
 *
 * The built transaction stays HERE (by openId); the client only ever sends back a witness, so
 * it cannot swap the transaction it signs.
 */
const OPEN_TTL_MS = 10 * 60_000;
const MAX_PER_OPEN = 10;
/** A token-less return under this would be dust no output can hold - it goes to the reclaim. */
const DUST = 1_000_000n;

interface OpenRecord {
  address: string;
  tier: BoxTier;
  key: string;
  payouts: PendingPiece[][];
  pieces: MintPiece[];
  burned: number;
  reclaim: number;
  unsigned: string;
  serviceWitness: string;
  expires: number;
}

const opens = new Map<string, OpenRecord>();

function sweep() {
  const now = Date.now();
  for (const [id, o] of opens) if (o.expires < now) opens.delete(id);
}

/** Every design the manifest knows, by id - designs and their relics. */
function designById(id: string): DesignPiece | undefined {
  for (const entries of Object.values(designs().tiers))
    for (const d of entries) {
      if (d.id === id) return d;
      if (d.relic?.id === id) return d.relic;
    }
  return undefined;
}

/** One payout: the design, plus its relic for a Black Diamond weapon. Instance ids are the server's. */
function rollPayout(entry: DesignEntry): PendingPiece[] {
  const stamp = randomUUID().replace(/-/g, '');
  const one = (d: DesignPiece): PendingPiece => ({ instanceId: `BOX-${d.slot}-${stamp}`, design: d.id });
  return entry.relic ? [one(entry), one(entry.relic)] : [one(entry)];
}

function toPiece(p: PendingPiece, tier: string): MintPiece {
  const d = designById(p.design);
  if (!d) throw new MintRefused(409, { error: 'design_retired', design: p.design });
  return {
    instanceId: p.instanceId,
    design: d.id,
    name: d.name,
    tier,
    slot: d.slot,
    ...(d.class && d.slot === 'Weapon' ? { class: d.class } : {}),
    ...(d.signature && d.slot === 'Relic' ? { signature: d.signature } : {}),
    artKey: d.id,
  };
}

const add = (into: Assets, unit: string, n: bigint) => {
  into[unit] = (into[unit] ?? 0n) + n;
  if (into[unit] === 0n) delete into[unit];
};

export async function openBoxes(req: Request, res: Response): Promise<void> {
  const address = res.locals.address as string;
  const { profileId, tier, slot } = (req.body ?? {}) as Record<string, string>;
  const count = Math.floor(Number(req.body?.count ?? 1));
  if (!profileId) return void res.status(400).json({ error: 'profileId is required' });
  if (!BOX_TIERS.includes(tier as BoxTier)) return void res.status(400).json({ error: 'tier must be Diamond or BlackDiamond' });
  if (slot) return void res.status(400).json({ error: 'design boxes open at random - there is no targeted open' });
  if (!(count >= 1 && count <= MAX_PER_OPEN)) return void res.status(400).json({ error: `count must be 1-${MAX_PER_OPEN}` });
  const t = tier as BoxTier;
  sweep();

  try {
    const { user: character } = await characterUnits(profileId);
    const refused = await pfpGate(address, character);
    if (refused) return void res.status(403).json({ error: refused });

    const burn = count * designs().costs.random;
    const held = await boxesHeld(address, t);
    if (held < burn) return void res.status(402).json({ error: 'not_enough_boxes', tier: t, have: held, need: burn });
    if (pool(t).length === 0) return void res.status(404).json({ error: 'no design to open into for that tier' });

    // The roll: the FIRST `count` payouts pending for these boxes, rolling only what is missing -
    // committed to the ledger BEFORE anything is shown or signed. A new open SUPERSEDES any
    // unsigned one for the same boxes (its transaction can no longer be submitted - the service
    // never hands out its own witness), so opening again shows the same designs. Without this,
    // building several opens and signing the best would be a free reroll.
    const key = pendingKey(address, t);
    for (const [id, o] of opens) if (o.address === address && o.key === key) opens.delete(id);
    const payouts = await transact((f) => {
      const queue = (f.pending[key] ??= []);
      while (queue.length < count) queue.push(rollPayout(pick(pool(t))!));
      return { result: queue.slice(0, count) };
    });

    const pieces = payouts.flat().map((p) => toPiece(p, t));
    const art = await preflight(pieces);

    // The deposit to reclaim: this address's share per box minted to it, for the boxes burned.
    const depositPool = (await snapshot()).deposits[address]?.[t];
    let reclaim = depositPool && depositPool.boxes > 0
      ? Math.floor((depositPool.lovelace * Math.min(burn, depositPool.boxes)) / depositPool.boxes)
      : 0;

    const built = await serialized(async () => {
      const lucid = await getLucid();
      const { ft } = await boxUnits(t);
      const { script } = await boxPolicy();

      // The player's coins holding boxes, fewest first that cover the burn.
      const theirs = (await lucid.utxosAt(address)).filter((u) => (u.assets[ft] ?? 0n) > 0n);
      theirs.sort((a, b) => Number((b.assets[ft] ?? 0n) - (a.assets[ft] ?? 0n)));
      const spend: UTxO[] = [];
      let covered = 0n;
      for (const u of theirs) {
        if (covered >= BigInt(burn)) break;
        spend.push(u);
        covered += u.assets[ft];
      }
      if (covered < BigInt(burn)) throw new MintRefused(409, { error: 'boxes_moved', need: burn });

      // Everything those coins held goes BACK to the player - less the burned boxes and the
      // service's deposit. Exactly theirs, nothing more, nothing less.
      const back: Assets = {};
      for (const u of spend) for (const [unit, n] of Object.entries(u.assets)) add(back, unit, n);
      add(back, ft, -BigInt(burn));
      const tokensLeft = Object.keys(back).some((u) => u !== 'lovelace');
      let lovelace = (back.lovelace ?? 0n) - BigInt(reclaim);
      if (lovelace < 0n) {
        reclaim += Number(lovelace);
        lovelace = 0n;
      }
      if (!tokensLeft && lovelace < DUST) {
        reclaim += Number(lovelace);
        lovelace = 0n;
      }
      back.lovelace = lovelace;

      let tx = lucid
        .newTx()
        .collectFrom(spend)
        .attach.MintingPolicy(script)
        .mintAssets({ [ft]: -BigInt(burn) })
        .validFrom(Date.now() - 3_600_000)
        .validTo(Date.now() + OPEN_TTL_MS);
      if (tokensLeft || lovelace > 0n) tx = tx.pay.ToAddress(address, back);
      ({ tx } = await addPieces(tx, address, character, pieces, art));

      // The service's own coins pay the fee and the new outputs' deposits; its change (and with
      // it the reclaimed deposit) comes back to the service wallet.
      const done = await tx.complete({ presetWalletInputs: await fundingInputs() });
      const serviceWitness = await done.partialSign.withWallet();
      const unsigned = done.toCBOR();
      rememberSpentCbor(unsigned);
      return { unsigned, serviceWitness };
    });

    const openId = randomUUID();
    const expires = Date.now() + OPEN_TTL_MS;
    opens.set(openId, { address, tier: t, key, payouts, pieces, burned: burn, reclaim, ...built, expires });

    res.json({
      openId,
      cbor: built.unsigned,
      tier: t,
      boxes: burn,
      reclaim,
      pieces: pieces.map((p) => ({ instanceId: p.instanceId, design: p.design, name: p.name, slot: p.slot })),
    });
  } catch (e) {
    if (e instanceof MintRefused) return void res.status(e.status).json(e.body);
    res.status(500).json({ error: (e as Error).message });
  }
}

export async function submitOpen(req: Request, res: Response): Promise<void> {
  const address = res.locals.address as string;
  const { openId, witness } = (req.body ?? {}) as Record<string, string>;
  sweep();
  const open = openId ? opens.get(openId) : undefined;
  if (!open || open.address !== address) return void res.status(404).json({ error: 'unknown or expired open - open again' });
  if (!witness) return void res.status(400).json({ error: 'witness is required' });

  try {
    const lucid = await getLucid();
    const signed = await lucid.fromTx(open.unsigned).assemble([open.serviceWitness, witness]).complete();
    const hash = await signed.submit();
    opens.delete(openId);

    // Submitted: those payouts are spent, and the deposit is home.
    const spent = new Set(open.payouts.map((p) => p[0].instanceId));
    await transact((f) => {
      f.pending[open.key] = (f.pending[open.key] ?? []).filter((p) => !spent.has(p[0].instanceId));
      if (f.pending[open.key].length === 0) delete f.pending[open.key];
      const pool = f.deposits[address]?.[open.tier];
      if (pool) {
        pool.lovelace = Math.max(0, pool.lovelace - open.reclaim);
        pool.boxes = Math.max(0, pool.boxes - open.burned);
      }
      return { result: null };
    });

    const { policyId } = await getGearPolicy();
    res.json({
      hash,
      tier: open.tier,
      pieces: open.pieces.map((p) => ({
        instanceId: p.instanceId, design: p.design, name: p.name, slot: p.slot,
        unit: unitOf(policyId, nameBody(p.instanceId), USER_LABEL),
      })),
    });
  } catch (e) {
    // Nothing was burned - the boxes and their pending roll are still there.
    res.status(409).json({ error: 'submit_failed', detail: (e as Error).message });
  }
}
