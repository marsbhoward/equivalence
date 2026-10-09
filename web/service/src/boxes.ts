import { fromText, mintingPolicyToId, scriptFromNative, toLabel } from '@lucid-evolution/lucid';
import { serviceKeyHash } from './chain.js';
import { bf } from './blockfrost.js';

/**
 * Diamond / Black Diamond BOXES as tokens - what a run that reaches those tiers drops (never a
 * design: RunLoot.Roll), minted to the player's wallet at extraction, tradeable, and BURNED when
 * opened at the Forge (routes/openBoxes.ts), which is when the server rolls the design.
 *
 * CIP-68 FUNGIBLE tokens (label 333), one per tier, with one reference token (label 100) per tier
 * holding the metadata wallets read (name, ticker, decimals 0). Their OWN policy - the service key
 * AND `after slot 1`, always satisfied - so boxes never share a collection with gear (bare key)
 * or characters (`after 0`).
 */
export const FT_LABEL = 333;
export const REF_LABEL = 100;

export type BoxTier = 'Diamond' | 'BlackDiamond';
export const BOX_TIERS: BoxTier[] = ['Diamond', 'BlackDiamond'];

const BODY: Record<BoxTier, string> = { Diamond: 'DiamondBox', BlackDiamond: 'BlackDiamondBox' };
export const BOX_INFO: Record<BoxTier, { name: string; ticker: string; description: string }> = {
  Diamond: {
    name: 'Equivalence Diamond Box',
    ticker: 'EQDBOX',
    description: 'Opens into one Diamond design at the Forge.',
  },
  BlackDiamond: {
    name: 'Equivalence Black Diamond Box',
    ticker: 'EQBDBOX',
    description: 'Opens into one Black Diamond design - a weapon comes with its relic.',
  },
};

export async function boxPolicy() {
  const keyHash = await serviceKeyHash();
  const script = scriptFromNative({
    type: 'all',
    scripts: [{ type: 'sig', keyHash }, { type: 'after', slot: 1 }],
  });
  return { policyId: mintingPolicyToId(script), script };
}

export async function boxUnits(tier: BoxTier) {
  const { policyId } = await boxPolicy();
  const body = fromText(BODY[tier]);
  return { ft: policyId + toLabel(FT_LABEL) + body, reference: policyId + toLabel(REF_LABEL) + body };
}

/** How many boxes of a tier an address holds right now. */
export async function boxesHeld(address: string, tier: BoxTier): Promise<number> {
  const { ft } = await boxUnits(tier);
  const utxos = (await bf<{ amount: { unit: string; quantity: string }[] }[]>(`/addresses/${address}/utxos/${ft}`)) ?? [];
  let n = 0;
  for (const u of utxos) for (const a of u.amount) if (a.unit === ft) n += Number(a.quantity);
  return n;
}
