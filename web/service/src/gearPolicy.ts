import { mintingPolicyToId, scriptFromNative } from '@lucid-evolution/lucid';
import { serviceKeyHash } from './chain.js';

/**
 * The gear-minting policy: a plain Cardano NATIVE script requiring the service's own signature,
 * nothing else. No Plutus, no Aiken, no redeemer, no on-chain price/quantity/uniqueness checks -
 * unlike donada-mint's mint_nft.ak, which has to survive an adversarial player building their own
 * transaction. Every call to this policy comes from this one trusted backend; the player never
 * builds or signs anything here. That is the same trust boundary beginRun/commitRun already rely
 * on, just applied to a minting policy instead of a datum write.
 *
 * If a future feature needs genuine one-of-one scarcity (a specific named legendary item that must
 * exist exactly once), that is exactly when donada's slot-token/burn pattern becomes the right
 * tool - not needed for ordinary, uncapped gear drops.
 */
let cached: ReturnType<typeof buildPolicy> | null = null;

async function buildPolicy() {
  const keyHash = await serviceKeyHash();
  const script = scriptFromNative({ type: 'sig', keyHash });
  const policyId = mintingPolicyToId(script);
  return { policyId, script };
}

export function getGearPolicy() {
  if (!cached) cached = buildPolicy();
  return cached;
}
