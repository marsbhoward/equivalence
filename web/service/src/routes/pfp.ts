import type { Request, Response } from 'express';
import { randomUUID } from 'node:crypto';
import type { Assets, TxBuilder, UTxO } from '@lucid-evolution/lucid';
import { bf } from '../blockfrost.js';
import { internal } from '../errors.js';
import { q } from '../db.js';
import { fundingInputs, getLucid, rememberSpentCbor, serialized, serviceAddress } from '../chain.js';
import { characterPolicy, characterUnits } from '../characters.js';
import { referenceDatum } from '../cip68.js';
import {
  MINT_ADA, MINT_LOVELACE, PFP_SCHEMA, RENEW_ADA, RENEW_LOVELACE, RETAKE_ADA, RETAKE_LOVELACE, YEAR_MS, portraitOf,
} from '../pfp.js';

/**
 * THE PORTRAIT (pfp.ts has the rules and prices). Reads are public - a datum is public on chain;
 * takes are the PLAYER's (session):
 *
 *   GET  /pfp/:profileId          -> the PfpRecipe JSON, or null          (the connector's currentPfp)
 *   GET  /pfp/:profileId/status   -> { taken, revision, paidUntil, prices }
 *   POST /write/pfp/mint          { profileId, recipe } -> { pfpId, cbor, price }   the FIRST take
 *   POST /write/pfp/update        { profileId, recipe } -> { pfpId, cbor, price }   a RE-TAKE
 *   POST /write/pfp/renew         { profileId }         -> { pfpId, cbor, price }   a YEAR
 *   POST /write/pfp/submit        { pfpId, witness }    -> { hash }
 *
 * THE PLAYER CHOOSES WHEN, THE SERVICE VOUCHES FOR WHAT (CLAUDE.md, "Who signs what"): the player
 * signs because they choose the moment and pay for it; the service builds, attests the depth
 * (MaxFloor - the deepest floor its own run records saw CLEARED, never the client's number), checks
 * the revision is exactly one past the chain's, and adds its witness. The reference token stays in
 * the service's wallet, so a portrait never changes without it.
 *
 * NOT YET: the portrait's IMAGE (no server-side renderer of a composed character exists - the
 * datum's `image` is PFP_IMAGE until one does), unique names, and component ownership on chain
 * (worn gear is mostly authored, not tokens).
 */
const ID = /^[A-Za-z0-9_.:-]{1,96}$/;
const NAME = /^[A-Za-z0-9]+([ '-][A-Za-z0-9]+)*$/;
const TTL_MS = 10 * 60_000;
const MAX_RECIPE = 4096;
const DUST = 1_000_000n;

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

interface Pending { address: string; profileId: string; kind: string; unsigned: string; serviceWitness: string; expires: number }
const takes = new Map<string, Pending>();
function sweep() {
  const now = Date.now();
  for (const [id, t] of takes) if (t.expires < now) takes.delete(id);
}

const prices = () => ({ mint: MINT_ADA, retake: RETAKE_ADA, renew: RENEW_ADA,
  mintLovelace: Number(MINT_LOVELACE), retakeLovelace: Number(RETAKE_LOVELACE), renewLovelace: Number(RENEW_LOVELACE) });

export async function readPortrait(req: Request, res: Response): Promise<void> {
  const profileId = String(req.params.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  try {
    const p = await portraitOf(profileId);
    res.type('application/json').send(p?.recipe ?? 'null');
  } catch (e) {
    // NEVER "null" on a failed read - null sends the game down the irreversible MINT path.
    internal(res, e, 'pfp', 502, 'chain_unavailable');
  }
}

export async function portraitStatus(req: Request, res: Response): Promise<void> {
  const profileId = String(req.params.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'bad profile id');
  try {
    const p = await portraitOf(profileId);
    res.json({ taken: !!p, revision: p?.revision ?? 0, paidUntil: p ? new Date(Math.min(p.paidUntil, 8.64e15)).toISOString() : null,
               lapsed: !!p && p.paidUntil <= Date.now(), prices: prices() });
  } catch (e) {
    internal(res, e, 'pfp', 502, 'chain_unavailable');
  }
}

/** The deepest floor the SERVICE saw this character clear, in any run - what the portrait attests. */
async function attestedFloor(owner: string, profileId: string): Promise<number> {
  const r = await q<{ n: number | null }>('SELECT max(cleared) AS n FROM run_progress WHERE owner = $1 AND profile_id = $2', [owner, profileId]);
  return Number(r[0]?.n ?? 0);
}

/** The player's recipe, checked and with the attested depth written over whatever it claimed. */
function checkRecipe(raw: unknown, profileId: string, revision: number, floor: number): { recipe?: string; error?: string } {
  const text = String(raw ?? '');
  if (!text || text.length > MAX_RECIPE) return { error: `recipe must be the PfpRecipe JSON (<= ${MAX_RECIPE} chars)` };
  let r: Record<string, unknown>;
  try { r = JSON.parse(text); } catch { return { error: 'recipe is not JSON' }; }
  if (r.ProfileId !== profileId) return { error: 'recipe is for another character' };
  if (Number(r.Revision) !== revision) return { error: 'stale_revision', };
  const name = String(r.Name ?? '');
  if (name.length < 3 || name.length > 24 || !NAME.test(name)) return { error: 'the name must be 3-24 letters, digits, spaces or hyphens' };
  r.MaxFloor = floor;
  r.TakenAtUtc = new Date().toISOString();
  return { recipe: JSON.stringify(r) };
}

const add = (into: Assets, unit: string, n: bigint) => {
  into[unit] = (into[unit] ?? 0n) + n;
  if (into[unit] === 0n) delete into[unit];
};

/**
 * A take the player pays `price` for: their coins in, EXACTLY what they held back less the price
 * (which stays with the service, in its change). `extend` adds the take itself; the service's own
 * coins pay the fee and every deposit.
 */
async function buildPaid(address: string, price: bigint, extend: (t: TxBuilder) => Promise<TxBuilder>) {
  return serialized(async () => {
    const lucid = await getLucid();
    const coins = (await lucid.utxosAt(address)).sort((a, b) => Number((b.assets.lovelace ?? 0n) - (a.assets.lovelace ?? 0n)));
    const spend: UTxO[] = [];
    let covered = 0n;
    for (const u of coins) {
      if (covered >= price + 2n * DUST) break;
      spend.push(u);
      covered += u.assets.lovelace ?? 0n;
    }
    if (covered < price + DUST) throw Object.assign(new Error('not_enough_ada'), { status: 402, have: Number(covered) });
    const back: Assets = {};
    for (const u of spend) for (const [k, n] of Object.entries(u.assets)) add(back, k, n);
    add(back, 'lovelace', -price);
    let t = lucid.newTx().collectFrom(spend).validFrom(Date.now() - 3_600_000).validTo(Date.now() + TTL_MS);
    t = t.pay.ToAddress(address, back);
    t = await extend(t);
    const done = await t.complete({ presetWalletInputs: await fundingInputs() });
    const serviceWitness = await done.partialSign.withWallet();
    const unsigned = done.toCBOR();
    rememberSpentCbor(unsigned);
    return { unsigned, serviceWitness };
  });
}

async function holdsCharacter(address: string, userUnit: string): Promise<boolean> {
  const u = await bf<unknown[]>(`/addresses/${address}/utxos/${userUnit}`);
  return !!u && u.length > 0;
}

async function start(req: Request, res: Response, kind: 'mint' | 'update' | 'renew') {
  const address = res.locals.address as string;
  const profileId = String(req.body?.profileId ?? '');
  if (!ID.test(profileId)) return fail(res, 400, 'profileId is required');
  sweep();
  try {
    if (!(await q('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2', [address, profileId])).length)
      return fail(res, 403, 'this character belongs to another address');
    const units = await characterUnits(profileId);
    const existing = await portraitOf(profileId);
    const service = await serviceAddress();
    let price: bigint;
    let extend: (t: TxBuilder) => Promise<TxBuilder>;

    if (kind === 'mint') {
      if (existing || (await bf(`/assets/${units.user}`))) return fail(res, 409, 'already_taken');
      const checked = checkRecipe(req.body?.recipe, profileId, 1, await attestedFloor(address, profileId));
      if (checked.error) return fail(res, 400, checked.error);
      const recipe = JSON.parse(checked.recipe!);
      price = MINT_LOVELACE;
      const { script } = await characterPolicy();
      const metadata = {
        name: recipe.Name, image: process.env.PFP_IMAGE ?? '', profile: profileId,
        recipe: checked.recipe!, maxFloor: recipe.MaxFloor, paidUntil: Date.now() + YEAR_MS,
      };
      extend = async (t) => t.mintAssets({ [units.reference]: 1n, [units.user]: 1n }).attach.MintingPolicy(script)
        .pay.ToAddressWithData(service, { kind: 'inline', value: referenceDatum(metadata, PFP_SCHEMA) }, { [units.reference]: 1n })
        .pay.ToAddress(address, { [units.user]: 1n });
    } else {
      if (!existing) return fail(res, 404, 'no_portrait');
      if (!(await holdsCharacter(address, units.user))) return fail(res, 403, 'this address does not hold the portrait');
      const meta = { ...existing.metadata } as Record<string, string | number>;
      if (kind === 'update') {
        const checked = checkRecipe(req.body?.recipe, profileId, existing.revision + 1, await attestedFloor(address, profileId));
        if (checked.error) return fail(res, checked.error === 'stale_revision' ? 409 : 400, checked.error, { onChain: existing.revision });
        const recipe = JSON.parse(checked.recipe!);
        meta.recipe = checked.recipe!;
        meta.name = recipe.Name;
        meta.maxFloor = recipe.MaxFloor;
        price = RETAKE_LOVELACE;
      } else {
        // A year from whichever is later - now, or the end of the year already paid.
        meta.paidUntil = Math.max(Date.now(), existing.paidUntil > 8.64e15 ? Date.now() : existing.paidUntil) + YEAR_MS;
        price = RENEW_LOVELACE;
      }
      // The reference token goes round with its new datum; it never leaves the service's wallet.
      const refUtxo = existing.utxo;
      extend = async (t) => t.collectFrom([refUtxo])
        .pay.ToAddressWithData(service, { kind: 'inline', value: referenceDatum(meta, PFP_SCHEMA) }, { [units.reference]: 1n });
    }

    let built;
    try {
      built = await buildPaid(address, price, extend);
    } catch (e) {
      const err = e as Error & { status?: number; have?: number };
      if (err.status === 402) return fail(res, 402, 'not_enough_ada', { have: err.have, need: Number(price) });
      throw e;
    }
    for (const [id, t] of takes) if (t.address === address && t.profileId === profileId) takes.delete(id);
    const pfpId = randomUUID();
    takes.set(pfpId, { address, profileId, kind, ...built, expires: Date.now() + TTL_MS });
    res.json({ pfpId, cbor: built.unsigned, kind, price: Number(price) });
  } catch (e) {
    internal(res, e, `pfp/${kind}`);
  }
}

export const mintPortrait = (req: Request, res: Response) => start(req, res, 'mint');
export const updatePortrait = (req: Request, res: Response) => start(req, res, 'update');
export const renewPortrait = (req: Request, res: Response) => start(req, res, 'renew');

export async function submitPortrait(req: Request, res: Response): Promise<void> {
  const address = res.locals.address as string;
  const pfpId = String(req.body?.pfpId ?? '');
  const witness = String(req.body?.witness ?? '');
  sweep();
  const t = takes.get(pfpId);
  if (!t || t.address !== address) return fail(res, 404, 'unknown or expired - take it again');
  if (!witness) return fail(res, 400, 'witness is required');
  try {
    const lucid = await getLucid();
    const signed = await lucid.fromTx(t.unsigned).assemble([t.serviceWitness, witness]).complete();
    const hash = await signed.submit();
    takes.delete(pfpId);
    console.log(`[pfp] ${t.profileId} ${t.kind}: ${hash}`);
    res.json({ hash, kind: t.kind });
  } catch (e) {
    internal(res, e, 'pfp/submit', 409, 'submit_failed');
  }
}
