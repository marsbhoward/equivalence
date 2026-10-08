import express from 'express';
import { redeemGear } from './routes/redeemGear.js';
import { components } from './routes/components.js';
import { networkInfo } from './blockfrost.js';
import { cors } from './cors.js';
import { serviceAddress } from './chain.js';

const app = express();
app.use(cors);
app.use(express.json());

app.get('/health', (_req, res) => {
  res.json({ ok: true });
});

/**
 * Proves the READ path end to end: the key works, Blockfrost answers, and the chain is moving.
 *
 * It reports the network derived from the URL rather than from the NETWORK env var, so a key and a
 * network that disagree surface HERE - as a wrong name on a health check - instead of much later as
 * a read that mysteriously returns nothing. Anything reading MAINNET on this project today is a
 * mistake, and it should be legible at a glance.
 */
app.get('/read/health', async (_req, res) => {
  try {
    res.json(await networkInfo());
  } catch (e) {
    res.status(502).json({ error: String((e as Error).message ?? e) });
  }
});

// The first real read: what a wallet holds. Pure Blockfrost - no service wallet, no signature,
// nothing to approve.
app.get('/components', components);

/**
 * The service wallet's own address - the one that has to hold test ADA before any write can pay a
 * fee. You cannot fund a wallet whose address you cannot see, and the address is NOT configuration:
 * it is DERIVED from SERVICE_WALLET_SEED, so there is nothing to set and nothing that can drift out
 * of sync with the key that actually signs.
 *
 * A Cardano address is public by nature - it is on chain the moment the wallet transacts - so
 * exposing it here reveals nothing the ledger will not. The SEED is the secret, and it never leaves
 * this process.
 */
app.get('/service/address', async (_req, res) => {
  try {
    res.json({ address: await serviceAddress() });
  } catch (e) {
    res.status(503).json({ error: String((e as Error).message ?? e) });
  }
});

// Named to match equivalence-chain.ts's existing /write/<kind> convention (beginRun, commitRun,
// unlock, saveAccount all go through that same shape client-side), so wiring this endpoint into
// the connector later is one more case in that file, not a new pattern.
app.post('/write/redeem-gear', redeemGear);

const port = Number(process.env.PORT ?? 8787);
app.listen(port, () => {
  console.log(`[equivalence-service] listening on :${port}`);
});
