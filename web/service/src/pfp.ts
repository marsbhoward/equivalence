import { Constr, Data, toText } from '@lucid-evolution/lucid';
import { getLucid } from './chain.js';
import { characterUnits } from './characters.js';
import { bf, isMainnet } from './blockfrost.js';
import { referenceOf } from './cip68.js';

/**
 * THE PORTRAIT - one per character, the character's own CIP-68 pair: the USER token in the player's
 * wallet (the PFP, the access tier), the REFERENCE token with the service holding its datum - the
 * PfpRecipe the game assembles (PfpRecipe.cs), the depth the SERVICE attests, and the year it is
 * paid until. Under the stand-in character policy (characters.ts) until the real one is designed.
 *
 * WHAT IT COSTS (the user's calls, 2026-10-25; MONETIZATION.md "The plan as of 2026-10-25"):
 *
 *   the first take   MINT_ADA, year one included - we pay the mint and its deposit
 *   each re-take     RETAKE_ADA to us - we pay the transaction
 *   each later year  RENEW_ADA - while a year is unpaid, minting to the wallet PAUSES (pfpGate)
 *
 * The player pays INSIDE the transaction they sign (their coins in, the price left with us), so a
 * take is never paid without happening, nor happens unpaid. The game's copy of the prices is
 * Chain.Web.PfpPrices - keep the two in step. `PFP_PRICE_SCALE` scales all three on a TESTNET
 * (test ADA is scarce); refused on mainnet.
 */
const SCALE = (() => {
  const s = Number(process.env.PFP_PRICE_SCALE ?? 1);
  if (s !== 1 && isMainnet()) throw new Error('PFP_PRICE_SCALE is testnet only');
  return Number.isFinite(s) && s > 0 ? s : 1;
})();
const ada = (n: number) => BigInt(Math.round(n * SCALE * 1_000_000));
export const MINT_ADA = 250;
export const RETAKE_ADA = 5;
export const RENEW_ADA = 200;
export const MINT_LOVELACE = ada(MINT_ADA);
export const RETAKE_LOVELACE = ada(RETAKE_ADA);
export const RENEW_LOVELACE = ada(RENEW_ADA);
export const YEAR_MS = 365 * 86_400_000;
/** The datum's own shape version (`extra.schema`). The stand-in dev characters are 0. */
export const PFP_SCHEMA = 1;

/** Plutus data back to JSON-ish metadata - cip68.referenceDatum's inverse. */
function fromPlutus(d: unknown): unknown {
  if (typeof d === 'bigint') return Number(d);
  if (typeof d === 'string') {
    try { return toText(d); } catch { return d; }
  }
  if (Array.isArray(d)) return d.map(fromPlutus);
  if (d instanceof Map) {
    const o: Record<string, unknown> = {};
    for (const [k, v] of d) o[String(fromPlutus(k))] = fromPlutus(v);
    return o;
  }
  if (d instanceof Constr) return d.fields.map(fromPlutus);
  return d;
}

export interface Portrait {
  /** The reference token's coin, to spend on an update. */
  utxo: Awaited<ReturnType<Awaited<ReturnType<typeof getLucid>>['utxoByUnit']>>;
  metadata: Record<string, unknown>;
  schema: number;
  recipe: string | null;
  revision: number;
  paidUntil: number;
}

/** The character's portrait as the chain holds it, or null when it has none. Throws on a failed read. */
export async function portraitOf(profileId: string): Promise<Portrait | null> {
  return portraitAt((await characterUnits(profileId)).reference);
}

/** The portrait whose REFERENCE token is `reference`, or null. */
export async function portraitAt(reference: string): Promise<Portrait | null> {
  if (!(await bf(`/assets/${reference}`))) return null;
  const lucid = await getLucid();
  const utxo = await lucid.utxoByUnit(reference);
  if (!utxo?.datum) return null;
  const datum = Data.from(utxo.datum) as Constr<unknown>;
  const [metadata, , extra] = datum.fields.map(fromPlutus) as [Record<string, unknown>, number, Record<string, unknown>];
  const recipe = typeof metadata.recipe === 'string' ? metadata.recipe : null;
  let revision = 0;
  try { revision = recipe ? Number(JSON.parse(recipe).Revision ?? 0) : 0; } catch { revision = 0; }
  return {
    utxo, metadata, schema: Number(extra?.schema ?? 0), recipe, revision,
    // A stand-in dev character (schema 0) has no year on it: treated as paid.
    paidUntil: Number(metadata.paidUntil ?? (Number(extra?.schema ?? 0) === 0 ? Number.MAX_SAFE_INTEGER : 0)),
  };
}

/** Whether a character's year is paid (by its USER unit) - what keeps minting to the wallet running.
 *  A character with no portrait record at all is not this check's to refuse (the holding check is). */
export async function lapsed(userUnit: string): Promise<boolean> {
  const reference = referenceOf(userUnit);
  if (!reference) return false;
  const p = await portraitAt(reference);
  return !!p && p.paidUntil <= Date.now();
}
