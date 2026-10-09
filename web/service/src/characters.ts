import { mintingPolicyToId, scriptFromNative } from '@lucid-evolution/lucid';
import { serviceKeyHash } from './chain.js';
import { REF_LABEL, USER_LABEL, nameBody, unitOf } from './cip68.js';

/**
 * Characters' policy and units - a STAND-IN until the real character policy and datum are
 * designed (routes/devCharacter.ts mints them on testnets).
 *
 * Its own policy, so characters never share a collection with gear: the service key AND an
 * `after slot 0` clause that is always satisfied - who can mint is unchanged; only the script's
 * hash, and so the policy id, differs from the gear policy's bare signature.
 *
 * A character's units are DERIVED from its profile id (`character:<profileId>` through
 * cip68.nameBody), so the service and the game both know a character's token from the id alone.
 */
export async function characterPolicy() {
  const keyHash = await serviceKeyHash();
  const script = scriptFromNative({
    type: 'all',
    scripts: [{ type: 'sig', keyHash }, { type: 'after', slot: 0 }],
  });
  return { policyId: mintingPolicyToId(script), script };
}

export async function characterUnits(profileId: string) {
  const { policyId } = await characterPolicy();
  const body = nameBody(`character:${profileId}`);
  return { user: unitOf(policyId, body, USER_LABEL), reference: unitOf(policyId, body, REF_LABEL) };
}
