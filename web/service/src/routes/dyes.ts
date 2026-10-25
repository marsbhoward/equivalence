import type { Request, Response } from 'express';
import { randomUUID } from 'node:crypto';
import type { Assets, Script, UTxO } from '@lucid-evolution/lucid';
import { bf } from '../blockfrost.js';
import { internal } from '../errors.js';
import { q, tx as dbTx } from '../db.js';
import { fundingInputs, getLucid, rememberSpentCbor, serialized, serviceAddress, signAndSubmit } from '../chain.js';
import { characterUnits } from '../characters.js';
import { referenceDatum } from '../cip68.js';
import { boxPolicy, boxUnits } from '../boxes.js';
import { dyeInfo, dyePolicy, dyeUnits } from '../dyes.js';
import { CHARGES_PER_BOX, countOf, credit, isSwatch } from '../cosmetics.js';
import { pfpGate } from './mintDesign.js';

/**
 * THE DYE ECONOMY'S CHAIN HALF (cosmetics.ts holds the rest, in the database), under a PLAYER
 * session (session.ts):
 *
 *   POST /write/dyes/withdraw            { profileId, swatch, count } -> { hash }
 *        earned dyes out of the character's count, minted to the wallet as dye tokens. Paid by us,
 *        unlimited (the user's call, 2026-10-25), a PFP holder only - nothing earned reaches a
 *        wallet without one. The count is taken FIRST and handed back if the mint fails.
 *   POST /write/dyes/deposit             { profileId, swatch, count } -> { burnId, cbor }
 *   POST /write/break-boxes              { profileId, count }         -> { burnId, cbor }
 *   POST /write/dyes/deposit/submit      { burnId, witness }          -> { hash }
 *   POST /write/break-boxes/submit       { burnId, witness }          -> { hash }
 *        the player's own TOKENS burned - dye tokens into the character's dye count, Diamond box
 *        tokens into charges. The service builds the transaction, pays its fee and keeps its
 *        witness; the player co-signs; the credit lands only once the burn is submitted. The built
 *        transaction stays HERE (by burnId) - the client only sends back a witness.
 *
 * A character is only ever touched by the address that saves it (routes/saves.ts).
 */
const ID = /^[A-Za-z0-9_.:-]{1,96}$/;
const MAX_WITHDRAW = 100;
const MAX_BURN = 20;
const BURN_TTL_MS = 10 * 60_000;
const DUST = 1_000_000n;

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

async function savesCharacter(address: string, profileId: string): Promise<boolean> {
  return (await q('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2', [address, profileId])).length > 0;
}

// ------------------------------------------------------------------------------------- withdraw

export async function withdrawDyes(req: Request, res: Response): Promise<void> {
  const address = res.locals.address as string;
  const profileId = String(req.body?.profileId ?? '');
  const swatch = String(req.body?.swatch ?? '');
  const count = Math.floor(Number(req.body?.count ?? 1));
  if (!ID.test(profileId)) return fail(res, 400, 'profileId is required');
  if (!isSwatch(swatch)) return fail(res, 400, 'unknown swatch');
  if (!(count >= 1 && count <= MAX_WITHDRAW)) return fail(res, 400, `count must be 1-${MAX_WITHDRAW}`);

  try {
    if (!(await savesCharacter(address, profileId))) return fail(res, 403, 'this character belongs to another address');
    const refused = await pfpGate(address, (await characterUnits(profileId)).user);
    if (refused) return fail(res, 403, refused);

    // Taken first, inside the character's lock: two withdrawals can never spend one dye twice.
    const taken = await dbTx(async (c) => {
      await c.query('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2 FOR UPDATE', [address, profileId]);
      const have = await countOf(c, address, profileId, 'dye', swatch);
      if (have < count) return have;
      await credit(c, address, profileId, 'dye', swatch, -count, 'withdraw');
      return -1;
    });
    if (taken >= 0) return fail(res, 402, 'not_enough_dye', { have: taken, need: count });

    try {
      const hash = await serialized(async () => {
        const lucid = await getLucid();
        const { script } = await dyePolicy();
        const { ft, reference } = await dyeUnits(swatch);
        const mint: Assets = { [ft]: BigInt(count) };
        let t = lucid.newTx().attach.MintingPolicy(script).validFrom(Date.now() - 3_600_000);
        // The colour's metadata, minted the first time that colour is ever withdrawn.
        if (!(await bf(`/assets/${reference}`))) {
          mint[reference] = 1n;
          t = t.pay.ToAddressWithData(
            await serviceAddress(),
            { kind: 'inline', value: referenceDatum({ ...dyeInfo(swatch), decimals: 0 }, 1) },
            { [reference]: 1n },
          );
        }
        const built = await t.mintAssets(mint).pay.ToAddress(address, { [ft]: BigInt(count) })
          .complete({ presetWalletInputs: await fundingInputs() });
        return signAndSubmit(built);
      });
      console.log(`[dyes] ${profileId} withdrew ${count} ${swatch}: ${hash}`);
      res.json({ hash, swatch, count });
    } catch (e) {
      // Nothing minted: the dyes go back to the character.
      await dbTx((c) => credit(c, address, profileId, 'dye', swatch, count, 'withdraw-refund')).catch(() => {});
      throw e;
    }
  } catch (e) {
    internal(res, e, 'dyes/withdraw');
  }
}

// ---------------------------------------------------------------------------------------- burns

interface Burn {
  address: string;
  profileId: string;
  kind: 'dye' | 'charge';
  item: string;
  /** What the character is credited once the burn is submitted. */
  credit: number;
  tokens: number;
  unsigned: string;
  serviceWitness: string;
  expires: number;
}

const burns = new Map<string, Burn>();

function sweep() {
  const now = Date.now();
  for (const [id, b] of burns) if (b.expires < now) burns.delete(id);
}

const add = (into: Assets, unit: string, n: bigint) => {
  into[unit] = (into[unit] ?? 0n) + n;
  if (into[unit] === 0n) delete into[unit];
};

/**
 * Builds a burn of `count` of the player's `unit` tokens: their coins holding it go in, and
 * EXACTLY what they held comes back less the burned tokens (openBoxes' rule); the service's coins
 * pay the fee. Returns the unsigned transaction and the service's witness.
 */
async function buildBurn(address: string, unit: string, count: number, script: Script) {
  return serialized(async () => {
    const lucid = await getLucid();
    const theirs = (await lucid.utxosAt(address)).filter((u) => (u.assets[unit] ?? 0n) > 0n);
    theirs.sort((a, b) => Number((b.assets[unit] ?? 0n) - (a.assets[unit] ?? 0n)));
    const spend: UTxO[] = [];
    let covered = 0n;
    for (const u of theirs) {
      if (covered >= BigInt(count)) break;
      spend.push(u);
      covered += u.assets[unit];
    }
    if (covered < BigInt(count)) throw Object.assign(new Error('not_enough_tokens'), { status: 402, have: Number(covered) });

    const back: Assets = {};
    for (const u of spend) for (const [k, n] of Object.entries(u.assets)) add(back, k, n);
    add(back, unit, -BigInt(count));
    const tokensLeft = Object.keys(back).some((k) => k !== 'lovelace');
    let t = lucid.newTx().collectFrom(spend).attach.MintingPolicy(script)
      .mintAssets({ [unit]: -BigInt(count) })
      .validFrom(Date.now() - 3_600_000).validTo(Date.now() + BURN_TTL_MS);
    // The ADA those coins held goes back with them (dust too small for an output stays as fee help).
    if (tokensLeft || (back.lovelace ?? 0n) >= DUST) t = t.pay.ToAddress(address, back);
    const done = await t.complete({ presetWalletInputs: await fundingInputs() });
    const serviceWitness = await done.partialSign.withWallet();
    const unsigned = done.toCBOR();
    rememberSpentCbor(unsigned);
    return { unsigned, serviceWitness };
  });
}

async function startBurn(req: Request, res: Response, kind: 'dye' | 'charge') {
  const address = res.locals.address as string;
  const profileId = String(req.body?.profileId ?? '');
  const swatch = String(req.body?.swatch ?? '');
  const count = Math.floor(Number(req.body?.count ?? 1));
  if (!ID.test(profileId)) return fail(res, 400, 'profileId is required');
  if (kind === 'dye' && !isSwatch(swatch)) return fail(res, 400, 'unknown swatch');
  if (!(count >= 1 && count <= MAX_BURN)) return fail(res, 400, `count must be 1-${MAX_BURN}`);
  sweep();
  try {
    if (!(await savesCharacter(address, profileId))) return fail(res, 403, 'this character belongs to another address');
    const unit = kind === 'dye' ? (await dyeUnits(swatch)).ft : (await boxUnits('Diamond')).ft;
    const { script } = kind === 'dye' ? await dyePolicy() : await boxPolicy();
    let built;
    try {
      built = await buildBurn(address, unit, count, script);
    } catch (e) {
      const err = e as Error & { status?: number; have?: number };
      if (err.status === 402) return fail(res, 402, kind === 'dye' ? 'not_enough_dye_tokens' : 'not_enough_boxes', { have: err.have, need: count });
      throw e;
    }
    // A new burn for the same character and token supersedes an unsigned one.
    for (const [id, b] of burns) if (b.address === address && b.profileId === profileId && b.kind === kind) burns.delete(id);
    const burnId = randomUUID();
    burns.set(burnId, {
      address, profileId, kind, item: kind === 'dye' ? swatch : '',
      credit: kind === 'dye' ? count : count * CHARGES_PER_BOX, tokens: count,
      ...built, expires: Date.now() + BURN_TTL_MS,
    });
    res.json({ burnId, cbor: built.unsigned, kind, count });
  } catch (e) {
    internal(res, e, kind === 'dye' ? 'dyes/deposit' : 'break-boxes');
  }
}

export const depositDyes = (req: Request, res: Response) => startBurn(req, res, 'dye');
export const breakBoxTokens = (req: Request, res: Response) => startBurn(req, res, 'charge');

export async function submitBurn(req: Request, res: Response): Promise<void> {
  const address = res.locals.address as string;
  const burnId = String(req.body?.burnId ?? '');
  const witness = String(req.body?.witness ?? '');
  sweep();
  const b = burns.get(burnId);
  if (!b || b.address !== address) return fail(res, 404, 'unknown or expired - start again');
  if (!witness) return fail(res, 400, 'witness is required');
  try {
    const lucid = await getLucid();
    const signed = await lucid.fromTx(b.unsigned).assemble([b.serviceWitness, witness]).complete();
    const hash = await signed.submit();
    burns.delete(burnId);
    // Burned: the character is credited. Logged with the hash, so a credit can be traced to its burn.
    await dbTx((c) => credit(c, address, b.profileId, b.kind, b.item, b.credit,
      `${b.kind === 'dye' ? 'deposit' : 'break-boxes'}:${hash}`));
    console.log(`[dyes] ${b.profileId} burned ${b.tokens} ${b.kind === 'dye' ? b.item : 'Diamond box(es)'}: ${hash}`);
    res.json({ hash, credited: b.credit, kind: b.kind });
  } catch (e) {
    internal(res, e, 'burn/submit', 409, 'submit_failed');
  }
}
