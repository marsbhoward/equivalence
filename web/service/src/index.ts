import express from 'express';
import { internal } from './errors.js';
import { mintDesign } from './routes/mintDesign.js';
import { devMintCharacter } from './routes/devCharacter.js';
import { splitFunds } from './routes/splitFunds.js';
import { components } from './routes/components.js';
import { getArt, postArt } from './routes/art.js';
import { getImage } from './routes/img.js';
import { networkInfo } from './blockfrost.js';
import { cors } from './cors.js';
import { requireWriteToken } from './writeAuth.js';
import { serviceAddress } from './chain.js';
import { challenge, prove, requireSession, sendProofNonce, sessionStatus, signOutEverywhere } from './session.js';
import { beginRun, boxes, commitRun } from './routes/player.js';
import { openBoxes, submitOpen } from './routes/openBoxes.js';
import { graduate, loadAccount, loadCharacter, roster, saveAccount, saveCharacter } from './routes/saves.js';
import { newGuest, requireIdentity } from './identity.js';
import { claimFinalFloor, finalFloorStatus } from './routes/finalFloor.js';
import { claimAvatarBoxes, devDecideAvatarBoxes } from './routes/avatarBox.js';
import { breakBox, claimMilestone, loadItems, recolour } from './routes/items.js';
import { breakBoxTokens, depositDyes, submitBurn, withdrawDyes } from './routes/dyes.js';
import { mintPortrait, portraitStatus, readPortrait, renewPortrait, submitPortrait, updatePortrait } from './routes/pfp.js';
import { beatRun, claimRun, endRun, loadRun, saveRun } from './routes/runs.js';
import { hasDatabase, migrate } from './db.js';
import { playRouter } from './play.js';
import { activatePlay, putPlayFile, rollbackPlay } from './routes/playUpload.js';
import { LIMITS, overall, perIdentity, perIp } from './rateLimit.js';
import { shutDownCleanly, track } from './lifecycle.js';
import { clientBuild, requireOpen, setSwitch, status, whenReopened } from './switches.js';
import { releaseDeferred } from './routes/player.js';

const app = express();
// Behind Railway's proxy: req.ip is the player's address (the guest rate limit reads it), not the proxy's.
app.set('trust proxy', 1);
// First: in-flight requests are counted, and turned away while shutting down (lifecycle.ts).
app.use(track);
app.use(cors);
// Every response names the nonce a session proof must carry (session.ts).
app.use(sendProofNonce);

// RATE LIMITS (rateLimit.ts) - per IP here, ahead of authentication and of the body parsers, so a
// flood is refused before a signature, a database read or a 512 kB body is spent on it. The
// per-identity limits sit behind each route's authentication, below.
const publicRead = perIp('public', LIMITS.publicRead);
const blockfrostPublic = overall('blockfrost-public', LIMITS.blockfrostPublic);
// Logins are the tight bucket; checking or ending a session already holds one and costs a MAC.
const login = perIp('auth', LIMITS.auth);
const sessionIp = perIp('session-ip', LIMITS.sessionIp);
app.use('/write', perIp('write-ip', LIMITS.writeIp));
app.use('/boxes', perIp('write-ip', LIMITS.writeIp));
app.use('/saves', perIp('saves-ip', LIMITS.savesIp));
app.use('/art', perIp('art', LIMITS.art));
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
// A WebGL build, a file at a time (routes/playUpload.ts) - the wasm alone is tens of MB.
app.put('/op/play/:build/file', express.raw({ type: '*/*', limit: '256mb' }), putPlayFile);
// Saves carry a whole CharacterProfile - ~12 KB today, growing with gear and history - so they get
// room past the default 100 kB, mounted before it for the same reason.
// A session proof signs the body's exact bytes (session.ts), so the parsers keep them.
const keepRaw = (req: express.Request, _res: express.Response, buf: Buffer) => {
  (req as express.Request & { rawBody?: Buffer }).rawBody = buf;
};
app.use('/saves', express.json({ limit: '512kb', verify: keepRaw }));
app.use(express.json({ verify: keepRaw }));

app.get('/health', (_req, res) => {
  // The deployment answering - scripts/deploy.sh waits for it to change.
  res.json({ ok: true, deployment: process.env.RAILWAY_DEPLOYMENT_ID ?? 'local' });
});

// What is paused, and the oldest game build served (switches.ts) - the game reads it in the hub.
app.get('/status', publicRead, status);
// A game build older than the service supports is told to reload (426) - only on the game's routes.
for (const prefix of ['/saves', '/write', '/auth', '/boxes']) app.use(prefix, clientBuild);

/**
 * Proves the READ path end to end: the key works, Blockfrost answers, and the chain is moving.
 *
 * It reports the network derived from the URL rather than from the NETWORK env var, so a key and a
 * network that disagree surface HERE - as a wrong name on a health check - instead of much later as
 * a read that mysteriously returns nothing. Anything reading MAINNET on this project today is a
 * mistake, and it should be legible at a glance.
 */
app.get('/read/health', publicRead, blockfrostPublic, async (_req, res) => {
  try {
    res.json(await networkInfo());
  } catch (e) {
    internal(res, e, 'read/health', 502, 'chain_unavailable');
  }
});

// The first real read: what a wallet holds. Pure Blockfrost - no service wallet, no signature,
// nothing to approve.
app.get('/components', perIp('components', LIMITS.components), blockfrostPublic, components);

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
app.get('/service/address', publicRead, async (_req, res) => {
  try {
    res.json({ address: await serviceAddress() });
  } catch (e) {
    internal(res, e, 'service/address', 503, 'unavailable');
  }
});

app.post('/op/mint-design', mintDesign);
app.post('/op/split-funds', splitFunds);
// Pause one feature without taking the server down (switches.ts, scripts/switch.mjs).
app.post('/op/switches', setSwitch);
// Testnet only (404 on mainnet) - a stand-in character for the PFP gate. See devCharacter.ts.
app.post('/op/dev/mint-character', devMintCharacter);
// Testnet only (404 on mainnet) - an avatar's boxes set by hand, for the smoke test (routes/avatarBox.ts).
app.post('/op/dev/avatar-box', devDecideAvatarBoxes);

// Player auth: a one-time challenge, signed with CIP-30 signData (no fee), traded for a session.
app.get('/auth/challenge', login, challenge);
app.post('/auth/prove', login, prove);
app.get('/auth/session', sessionIp, requireSession, sessionStatus);
app.post('/auth/signout-everywhere', sessionIp, requireSession, signOutEverywhere);

// The connector's /write/<kind> convention (equivalence-chain.ts `write()`).
app.use('/write', requireSession, perIdentity('write', LIMITS.write));
app.post('/write/begin-run', requireOpen('runs'), beginRun);
// The two that spend the service wallet's ADA, tighter still.
app.post('/write/commit-run', perIdentity('commit', LIMITS.commit), commitRun);
// Everything that goes to the chain at a player's request stops while `minting` is paused.
// Extraction (commit-run) is not refused - it defers (routes/player.ts).
const opening = [perIdentity('open', LIMITS.open), requireOpen('minting')];
app.post('/write/open-boxes', opening, openBoxes);
app.post('/write/open-boxes/submit', opening, submitOpen);
// Dyes as tokens and box tokens into charges (routes/dyes.ts) - every one spends the service's ADA.
app.post('/write/dyes/withdraw', opening, withdrawDyes);
app.post('/write/dyes/deposit', opening, depositDyes);
app.post('/write/dyes/deposit/submit', opening, submitBurn);
app.post('/write/break-boxes', opening, breakBoxTokens);
app.post('/write/break-boxes/submit', opening, submitBurn);
app.get('/boxes/:profileId', requireSession, perIdentity('boxes', LIMITS.boxes), boxes);
app.get('/art/:artKey', getArt);
// A wallet token's picture, fetched once, shrunk and cached (routes/img.ts).
app.get('/img/:unit', perIp('img', LIMITS.img), blockfrostPublic, getImage);
// The portrait (routes/pfp.ts): reads are public, takes are the player's and pay inside the tx.
app.get('/pfp/:profileId', publicRead, blockfrostPublic, readPortrait);
app.get('/pfp/:profileId/status', publicRead, blockfrostPublic, portraitStatus);
const portrait = [perIdentity('pfp', LIMITS.open), requireOpen('minting')];
app.post('/write/pfp/mint', portrait, mintPortrait);
app.post('/write/pfp/update', portrait, updatePortrait);
app.post('/write/pfp/renew', portrait, renewPortrait);
app.post('/write/pfp/submit', portrait, submitPortrait);

// Saves (routes/saves.ts): every browser player's characters and hub room, guest or wallet.
app.post('/guest', newGuest);
app.use('/saves', requireIdentity, perIdentity('saves', LIMITS.saves));
// `saves` paused: read-only. `runs` paused: no run-start save, so no new run.
app.use('/saves', requireOpen('saves', req => req.method !== 'GET'));
app.put('/saves/characters/:profileId', requireOpen('runs', req => req.body?.reason === 'run-start'));
app.get('/saves/characters', roster);
app.get('/saves/characters/:profileId', loadCharacter);
app.put('/saves/characters/:profileId', saveCharacter);
app.get('/saves/account', loadAccount);
app.put('/saves/account', saveAccount);
app.post('/saves/graduate', graduate);
// Floor 100's box - the one drop the service decides (routes/finalFloor.ts).
app.post('/saves/final-floor', claimFinalFloor);
app.get('/saves/final-floor/:profileId', finalFloorStatus);
// An avatar's boxes - decided by the service too (routes/avatarBox.ts).
app.post('/saves/avatar-box', claimAvatarBoxes);
// What the database holds per character - dyes, charges, poses, milestones (routes/items.ts).
app.get('/saves/items/:profileId', loadItems);
app.post('/saves/items/:profileId/recolour', recolour);
app.post('/saves/items/:profileId/break-box', breakBox);
app.post('/saves/milestone', claimMilestone);
// A run in flight - snapshots, heartbeats, and one live session per run (routes/runs.ts).
app.get('/saves/runs/:profileId', loadRun);
app.put('/saves/runs/:profileId', saveRun);
app.put('/saves/runs/:profileId/beat', beatRun);
app.post('/saves/runs/:profileId/claim', claimRun);
app.post('/saves/runs/:profileId/end', endRun);

// Where the page's Content-Security-Policy reports what it blocked (play.ts) - logged, trimmed.
app.post('/csp-report', perIp('csp', LIMITS.csp),
  express.json({ type: ['application/csp-report', 'application/reports+json', 'application/json'], limit: '16kb' }),
  (req, res) => {
    const r = (req.body?.['csp-report'] ?? req.body ?? {}) as Record<string, unknown>;
    console.warn(`[csp] blocked ${String(r['blocked-uri'] ?? r.blockedURL ?? '?').slice(0, 200)} ` +
      `(${String(r['violated-directive'] ?? r.effectiveDirective ?? '?').slice(0, 60)})`);
    res.sendStatus(204);
  });

app.post('/op/play/:build/activate', activatePlay);
app.post('/op/play/rollback', rollbackPlay);

// The game (play.ts), same origin as everything above. Relative Build/ paths need the trailing
// slash; Express matches /play and /play/ alike, so the redirect checks the raw path - or /play/
// would redirect to itself.
app.get('/play', (req, res, next) => (req.path === '/play' && !req.originalUrl.startsWith('/play/')
  ? res.redirect(301, '/play/') : next()));
app.use('/play', playRouter());

const port = Number(process.env.PORT ?? 8787);
// The schema is brought up to date BEFORE the first request is served. Without DATABASE_URL the
// saves routes answer 503 and everything else runs as it always has.
(hasDatabase() ? migrate() : Promise.resolve()).then(
  () => {
    const server = app.listen(port, () => console.log(`[equivalence-service] listening on :${port}`));
    shutDownCleanly(server);
    // Extractions deferred while minting was paused mint when it reopens - and at start-up, in case
    // a restart cut a release short.
    whenReopened(name => { if (name === 'minting') void releaseDeferred().then(n => n && console.log(`[deferred] released ${n} run(s)`)); });
    void releaseDeferred().catch(e => console.error('[deferred] release at start-up failed:', e));
  },
  (e) => {
    console.error('[equivalence-service] database migration failed:', e);
    process.exit(1);
  },
);
