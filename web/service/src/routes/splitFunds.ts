import type { Request, Response } from 'express';
import { fundingInputs, getLucid, serialized, serviceAddress, signAndSubmit } from '../chain.js';

/**
 * POST /op/split-funds { count, ada } - OPERATOR. Pays the service wallet `count` coins of `ada`
 * each, from its own plain-ADA coins.
 *
 * Every build spends only plain-ADA coins and sets aside what it just spent until Blockfrost has
 * caught up (chain.ts), so how many service transactions can go out back to back is how many such
 * coins the wallet holds. One big coin is one transaction at a time.
 */
export async function splitFunds(req: Request, res: Response): Promise<void> {
  const count = Math.floor(Number(req.body?.count ?? 10));
  const ada = Number(req.body?.ada ?? 50);
  if (!(count >= 1 && count <= 50) || !(ada >= 5 && ada <= 1000)) {
    res.status(400).json({ error: 'count 1-50, ada 5-1000' });
    return;
  }
  try {
    const result = await serialized(async () => {
      const lucid = await getLucid();
      const self = await serviceAddress();
      let tx = lucid.newTx();
      for (let i = 0; i < count; i++) tx = tx.pay.ToAddress(self, { lovelace: BigInt(Math.round(ada * 1e6)) });
      const built = await tx.complete({ presetWalletInputs: await fundingInputs() });
      return { hash: await signAndSubmit(built) };
    });
    res.json({ ...result, coins: count, ada });
  } catch (err) {
    res.status(500).json({ error: err instanceof Error ? err.message : String(err) });
  }
}
