import { bf, type BfAmount } from './blockfrost.js';

/**
 * PAYING IN NIGHT (the user's call, 2026-11-07): Midnight's NIGHT, a Cardano native asset, is
 * accepted wherever a player pays ADA (the portrait's take, re-take and year). The price is SET in
 * ADA; NIGHT is that price at the market rate, read ONCE, when the transaction is built - never
 * polled, never cached, never shown before the player asks to pay. The amount is fixed inside the
 * transaction the player signs, so their wallet shows exactly what they pay; an unsigned one expires
 * with its TTL and the next try is quoted afresh.
 *
 * THE RATE is the spot price of a DEX pool's reserves, read through Blockfrost - no new third party.
 * NIGHT_POOLS is a comma-separated list of DEX pool script ADDRESSES (Minswap V2's, say); at each, the
 * ADA/NIGHT pool is the UTxO holding NIGHT and no other token but the DEX's own (any asset under the
 * policy of a single unit it holds - Minswap V2's auth token, and the LP tokens beside it) - the
 * deepest if there are several. Minswap V2 (checked 2026-11-07): ~3.75M ADA / ~16.95M NIGHT. Unset, NIGHT is not
 * accepted.
 *
 * THE GUARDS - a quote that fails one is refused (`night_unavailable`) and the game offers ADA
 * instead; a price is never guessed:
 *   - a pool holding less than MIN_POOL_ADA is too thin to trust (cheap to move);
 *   - with two or more pools, they must agree within MAX_SPREAD (the median is used);
 *   - against the last good quote of the last MOVE_WINDOW_MS, a move past MAX_MOVE is refused
 *     (a pool pushed for one block to buy cheap); a restart forgets it, the other guards stand;
 *   - the NIGHT owed is rounded UP to a whole NIGHT, so we never take less than the ADA price.
 */
export const NIGHT_UNIT = process.env.NIGHT_UNIT
  ?? '0691b2fecca1ac4f53cb6dfb00b7013e561d1f34403b957cbb5af1fa4e49474854';   // policy + "NIGHT"; 6 decimals
const ONE_NIGHT = 1_000_000n;
const MIN_POOL_ADA = 100_000;
const MAX_SPREAD = 0.05;
const MAX_MOVE = 0.15;
const MOVE_WINDOW_MS = 6 * 3_600_000;

const pools = () => (process.env.NIGHT_POOLS ?? '').split(',').map(s => s.trim()).filter(Boolean);

/** Whether a player may pay in NIGHT at all - pools configured. Says nothing about the rate. */
export const nightAccepted = () => pools().length > 0;

export class NightUnavailable extends Error {
  status = 503;
  constructor(why: string) { super(`night_unavailable: ${why}`); }
}

/** The ADA/NIGHT pool at one DEX address: its NIGHT per ADA, from the reserves its UTxO holds. */
async function poolRate(address: string): Promise<number> {
  const name = address.slice(0, 16);
  const utxos = await bf<{ amount: BfAmount[] }[]>(`/addresses/${address}/utxos/${NIGHT_UNIT}?count=100`) ?? [];
  let best: { lovelace: number; night: number } | null = null;
  for (const u of utxos) {
    // ADA and NIGHT only, besides the DEX's own tokens: not NIGHT/USDM and the like.
    const dex = new Set(u.amount.filter(a => a.quantity === '1').map(a => a.unit.slice(0, 56)));
    if (u.amount.some(a => a.unit !== 'lovelace' && a.unit !== NIGHT_UNIT && !dex.has(a.unit.slice(0, 56)))) continue;
    const lovelace = Number(u.amount.find(a => a.unit === 'lovelace')?.quantity ?? 0);
    const night = Number(u.amount.find(a => a.unit === NIGHT_UNIT)?.quantity ?? 0);
    if (!best || lovelace > best.lovelace) best = { lovelace, night };
  }
  if (!best) throw new NightUnavailable(`no ADA/NIGHT pool at ${name}`);
  if (best.lovelace < MIN_POOL_ADA * 1e6) throw new NightUnavailable(`pool at ${name} too thin`);
  if (!(best.night > 0)) throw new NightUnavailable(`pool at ${name} holds no NIGHT`);
  return best.night / best.lovelace;   // both in base units (6 decimals each)
}

let lastGood: { rate: number; at: number } | null = null;

/** The NIGHT (base units) a price of `lovelace` costs now, and the rate used. Throws NightUnavailable. */
export async function quoteNight(lovelace: bigint): Promise<{ night: bigint; rate: number }> {
  const units = pools();
  if (!units.length) throw new NightUnavailable('not accepted');
  let rates: number[];
  try {
    rates = await Promise.all(units.map(poolRate));
  } catch (e) {
    if (e instanceof NightUnavailable) throw e;
    throw new NightUnavailable(`pool unreadable (${(e as Error).message.slice(0, 80)})`);
  }
  rates.sort((a, b) => a - b);
  const rate = rates[Math.floor(rates.length / 2)];
  if (rates.length > 1 && rates[rates.length - 1] / rates[0] - 1 > MAX_SPREAD)
    throw new NightUnavailable('pools disagree');
  if (lastGood && Date.now() - lastGood.at < MOVE_WINDOW_MS && Math.abs(rate / lastGood.rate - 1) > MAX_MOVE)
    throw new NightUnavailable('rate moved too far');
  lastGood = { rate, at: Date.now() };
  const base = BigInt(Math.ceil(Number(lovelace) * rate));
  return { night: ((base + ONE_NIGHT - 1n) / ONE_NIGHT) * ONE_NIGHT, rate };
}
