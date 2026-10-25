import { fromText, mintingPolicyToId, scriptFromNative, toLabel } from '@lucid-evolution/lucid';
import { serviceKeyHash } from './chain.js';
import { FT_LABEL, REF_LABEL } from './boxes.js';
import { swatches } from './cosmetics.js';

/**
 * DYES AS TOKENS - only once WITHDRAWN (the user's call, 2026-10-25: database first; withdrawals
 * unlimited and paid by us). CIP-68 FUNGIBLE tokens (label 333), one per COLOUR, so duplicates
 * stack ("Crimson Dye x3") and trade; one reference token (label 100) per colour carries the
 * metadata wallets read. Their OWN policy - the service key AND `after slot 2`, always satisfied -
 * so dyes never share a collection with gear (bare key), characters (`after 0`) or boxes (`after 1`).
 *
 * A dye token is burned back into a character's database count to be used (routes/dyes.ts deposit).
 * The asset name body is the swatch id's text after `dye.` (permanent, as every swatch id is).
 */
export async function dyePolicy() {
  const keyHash = await serviceKeyHash();
  const script = scriptFromNative({
    type: 'all',
    scripts: [{ type: 'sig', keyHash }, { type: 'after', slot: 2 }],
  });
  return { policyId: mintingPolicyToId(script), script };
}

export async function dyeUnits(swatch: string) {
  const { policyId } = await dyePolicy();
  const body = fromText(`Dye${swatch.replace(/^dye\./, '')}`);
  return { ft: policyId + toLabel(FT_LABEL) + body, reference: policyId + toLabel(REF_LABEL) + body };
}

/** What a colour's reference token says - its name and ticker. */
export function dyeInfo(swatch: string) {
  const s = swatches().find((x) => x.id === swatch);
  const name = s?.name ?? swatch;
  return {
    name: `Equivalence ${name} Dye`,
    ticker: `EQ${swatch.replace(/^dye\./, '').slice(0, 6).toUpperCase()}`,
    description: `Recolours one channel of one Diamond armour piece. ${s?.rarity ?? ''}`.trim(),
  };
}
