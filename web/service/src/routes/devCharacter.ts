import type { Request, Response } from 'express';
import { fundingInputs, getLucid, serialized, serviceAddress, signAndSubmit } from '../chain.js';
import { characterPolicy } from '../characters.js';
import { REF_LABEL, USER_LABEL, nameBody, referenceDatum, unitOf } from '../cip68.js';
import { bf } from '../blockfrost.js';

/**
 * TESTNET ONLY: mints a STAND-IN character - the CIP-68 pair a real character will be - so the
 * gear mint's PFP gate has something real to check before the character datum is designed.
 *
 * Under characters.ts's stand-in policy. The real character policy (and its datum, PFP recipe
 * and all) replaces this; nothing here is that design.
 *
 * Datum: just the PFP's `name` (what gear's `mintedBy` resolves to) and `stub: 1`, at schema 0, so
 * nothing will ever mistake it for a real character.
 */
const NAME = /^[A-Za-z0-9]+([ -][A-Za-z0-9]+)*$/;

/** POST /write/dev/mint-character { address, profileId, name } */
export async function devMintCharacter(req: Request, res: Response): Promise<void> {
  if ((process.env.NETWORK ?? '').toLowerCase() === 'mainnet') {
    res.status(404).end();
    return;
  }
  const { address, profileId, name } = (req.body ?? {}) as Record<string, string>;
  if (!address?.startsWith('addr_test') || !profileId || !name || name.length < 3 || name.length > 16 || !NAME.test(name)) {
    res.status(400).json({ error: 'address (testnet), profileId, and a 3-16 char name are required' });
    return;
  }

  try {
    const { policyId, script } = await characterPolicy();
    const body = nameBody(`character:${profileId}`);
    const refUnit = unitOf(policyId, body, REF_LABEL);
    const userUnit = unitOf(policyId, body, USER_LABEL);
    if (await bf(`/assets/${userUnit}`)) {
      res.status(409).json({ error: 'already_minted', unit: userUnit });
      return;
    }

    const { hash } = await serialized(async () => {
      const lucid = await getLucid();
      const tx = await lucid
        .newTx()
        .mintAssets({ [refUnit]: 1n, [userUnit]: 1n })
        .attach.MintingPolicy(script)
        .validFrom(Date.now() - 3_600_000) // the `after` clause needs a validity START; an hour back, so a chain tip lagging real time never sees it in the future
        .pay.ToAddressWithData(
          await serviceAddress(),
          { kind: 'inline', value: referenceDatum({ name, profile: profileId, stub: 1 }, 0) },
          { [refUnit]: 1n },
        )
        .pay.ToAddress(address, { [userUnit]: 1n })
        .complete({ presetWalletInputs: await fundingInputs() });
      const hash = await signAndSubmit(tx);
      return { hash };
    });
    res.json({ hash, character: userUnit, reference: refUnit, policyId });
  } catch (err) {
    res.status(500).json({ error: err instanceof Error ? err.message : String(err) });
  }
}
