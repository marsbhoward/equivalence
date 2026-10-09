import express from 'express';
import { mintDesign } from './routes/mintDesign.js';
import { devMintCharacter } from './routes/devCharacter.js';
import { splitFunds } from './routes/splitFunds.js';
import { components } from './routes/components.js';
import { getArt, postArt } from './routes/art.js';
import { networkInfo } from './blockfrost.js';
import { cors } from './cors.js';
import { requireWriteToken } from './writeAuth.js';
import { serviceAddress } from './chain.js';
import { challenge, prove, requireSession } from './session.js';
import { beginRun, boxes, commitRun } from './routes/player.js';
import { openBoxes, submitOpen } from './routes/openBoxes.js';

const app = express();
app.use(cors);
// Two authorities, two prefixes:
//   /op/*     the OPERATOR - WRITE_TOKEN (writeAuth.ts). Never reachable from the browser build.
//   /write/*  the PLAYER   - a session from /auth/prove (session.ts). What the connector calls.
// The operator guard runs BEFORE any body is parsed, so an unauthorised caller can't make the
// service read a 16 MB upload just to refuse it.
app.use('/op', requireWriteToken);
// Art uploads carry a 3200x3200 PNG as base64 - mounted with its own limit ahead of the default
// 100 kB parser (which then sees the body already parsed and steps aside). OPERATOR only: the
// art index is shared, so one tampered upload would be every later mint's picture.
app.post('/op/art', express.json({ limit: '16mb' }), postArt);
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

app.post('/op/mint-design', mintDesign);
app.post('/op/split-funds', splitFunds);
// Testnet only (404 on mainnet) - a stand-in character for the PFP gate. See devCharacter.ts.
app.post('/op/dev/mint-character', devMintCharacter);

// Player auth: a one-time challenge, signed with CIP-30 signData (no fee), traded for a session.
app.get('/auth/challenge', challenge);
app.post('/auth/prove', prove);

// The connector's /write/<kind> convention (equivalence-chain.ts `write()`).
app.use('/write', requireSession);
app.post('/write/begin-run', beginRun);
app.post('/write/commit-run', commitRun);
app.post('/write/open-boxes', openBoxes);
app.post('/write/open-boxes/submit', submitOpen);
app.get('/boxes/:profileId', requireSession, boxes);
app.get('/art/:artKey', getArt);

const port = Number(process.env.PORT ?? 8787);
app.listen(port, () => {
  console.log(`[equivalence-service] listening on :${port}`);
});
