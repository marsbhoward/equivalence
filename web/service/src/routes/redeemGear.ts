import type { Request, Response } from 'express';
import { randomUUID } from 'node:crypto';
import { fromText } from '@lucid-evolution/lucid';
import { getLucid } from '../chain.js';
import { getGearPolicy } from '../gearPolicy.js';

interface RedeemGearBody {
  address: string;
  itemId: string;
  slot: string;
  tier: string;
  /** StatPercents as JSON - field name to percentage-point value, e.g. { "Armor": 8, "MaxHp": 6 }. */
  grants?: Record<string, number>;
  defensiveAbility?: string;
}

/**
 * Mints one already-rolled gear item straight to the player's wallet. Service-signed, no player
 * transaction, no fee - the same trust model beginRun/commitRun already use ("the game asserts
 * it, the service notarizes it"), just applied to an asset instead of a profile datum.
 *
 * The payload is the item exactly as GearRoller (Unity-side) already computed it. This endpoint
 * does not re-roll or re-validate the stats, matching the connector's own rule for commitRun's
 * datum: "the store mutates the profile before serialising - do not recompute progression on the
 * service." Same rule, applied to an item.
 *
 * Unlike donada-mint's mint, the newly-minted asset has to be explicitly redirected to the
 * player's address via pay.ToAddress - the SERVICE wallet is the one building and signing this
 * transaction, so without that, the mint would land back in the service's own change output
 * instead of the player's wallet.
 */
export async function redeemGear(req: Request, res: Response): Promise<void> {
  const body = req.body as Partial<RedeemGearBody>;
  if (!body.address || !body.itemId || !body.slot || !body.tier) {
    res.status(400).json({ error: 'address, itemId, slot, and tier are required' });
    return;
  }

  try {
    const lucid = await getLucid();
    const { policyId, script } = await getGearPolicy();

    // Unique per mint, never reused - ordinary gear isn't a capped collection the way donada's
    // is, so there is no shared name to guard against colliding with.
    const assetName = `GEAR-${body.itemId}-${randomUUID()}`;
    const unit = policyId + fromText(assetName);

    const metadata = {
      [policyId]: {
        [assetName]: {
          itemId: body.itemId,
          slot: body.slot,
          tier: body.tier,
          grants: body.grants ?? {},
          ...(body.defensiveAbility ? { defensiveAbility: body.defensiveAbility } : {}),
        },
      },
    };

    const tx = await lucid
      .newTx()
      .mintAssets({ [unit]: 1n })
      .attach.MintingPolicy(script)
      .attachMetadata(721, metadata)
      .pay.ToAddress(body.address, { [unit]: 1n })
      .complete();

    const signed = await tx.sign.withWallet().complete();
    const hash = await signed.submit();

    res.json({ hash, unit });
  } catch (err) {
    // Rejects with a message, never a sentinel - matches equivalence-chain.d.ts's contract
    // ("a rejected promise becomes a ChainException"; empty-hash writes are recorded as failed
    // checkpoints, never as silent successes).
    res.status(500).json({ error: err instanceof Error ? err.message : String(err) });
  }
}
