# Coalescence service

The one backend endpoint `web/connector/coalescence-chain.ts` doesn't have yet: minting a rolled
gear item straight into a player's wallet, service-signed, no fee, no wallet prompt.

**Scope, deliberately narrow.** This is not the chain backend for `beginRun`/`commitRun`/`unlock`/
`saveAccount` or the PFP mint - those are separate, unbuilt seams that don't depend on this and
aren't touched here. This service does exactly one thing.

## Why there's no smart contract here

The gear-minting policy is a plain Cardano **native script** - "requires the service wallet's
signature," nothing else. No Aiken, no Plutus, no redeemer/datum ceremony. That's not a shortcut;
it's the correct amount of machinery for what this actually needs to prove. A real validator (like
the Aiken policy in `donada-mint`) earns its keep when it has to survive an *adversarial* player
building their own transaction. Nobody but this backend ever calls this endpoint - the player never
opens a wallet dialog for gear at all - so there's no adversary a validator would be defending
against. See `src/gearPolicy.ts` for the two-line policy itself.

If a specific item ever needs genuine one-of-one scarcity (a named legendary that must exist
exactly once), that's when `donada-mint`'s slot-token/burn pattern
(`SLOT_TOKEN_DESIGN.md` in that repo) is the right tool to reach for - not needed for ordinary,
uncapped drops.

## Setup

```bash
npm install
cp .env.example .env
# fill in BLOCKFROST_API_KEY (free, Preview tier: https://blockfrost.io)
# fill in SERVICE_WALLET_SEED with a fresh 12/24-word seed phrase - generate one with any
# Cardano wallet, then fund its Preview address from https://docs.cardano.org/cardano-testnets/tools/faucet
npm run dev
```

`GET /health` confirms the process is up. `POST /write/redeem-gear` is the real endpoint - see
`src/routes/redeemGear.ts` for the request shape and what it trusts versus computes itself (short
version: it trusts the rolled item exactly as sent, the same way `commitRun`'s datum is already
trusted rather than recomputed server-side).

## Deploying to Railway

Point a Railway service at this repo with **`web/service`** as the root directory. Set the four
`.env.example` variables as Railway environment variables (never commit a filled-in `.env`).
Railway assigns `PORT` itself - the app already reads it from the environment.

Stay on `NETWORK=Preview` until the whole flow - mint, confirm on a Preview explorer, verify the
metadata and recipient - has actually been exercised end to end. Cut over to Mainnet only by
changing `NETWORK`, `BLOCKFROST_API_KEY`, and `BLOCKFROST_URL` together; a mismatched trio fails
loudly (Blockfrost rejects a mainnet key against a preview URL) rather than quietly submitting to
the wrong network.

## Wiring into the game later

Once this is deployed, `coalescence-chain.ts` gains one more case next to `beginRun`/`commitRun`:

```ts
redeemGear({ address, itemId, slot, tier, grants, defensiveAbility }) {
  return write('redeem-gear', { address, itemId, slot, tier, grants, defensiveAbility });
}
```

using the same `write()` helper already defined there. Not added yet - the Unity-side Forge fixture
that would call it doesn't exist either.

## Reads (implemented)

Read-only endpoints need **only a Blockfrost project key** — no service wallet, no seed phrase,
no funded address. That split is deliberate: see `src/blockfrost.ts`.

    cp .env.example .env
    # set BLOCKFROST_API_KEY / BLOCKFROST_URL for Preprod or Preview. Leave
    # SERVICE_WALLET_SEED alone - reads do not use it.
    npm run dev

    curl localhost:8787/read/health
    curl "localhost:8787/components?address=addr_test1..."

`/read/health` reports the network **derived from the URL**, not from the `NETWORK` env var, so a
key and a network that disagree show up as a wrong name on a health check rather than as a read
that mysteriously returns nothing. Anything reporting `MAINNET` on this project today is a mistake.

`/components` returns the shape Unity's `WalletInventory.ComponentList` parses:

    {"items":[{"Key","Title","Collection","Slot","ImageUrl"}], "truncated":false, "held":3}

Wrapped in `items` because `JsonUtility` cannot deserialise a top-level array. `truncated`/`held`
are extra and JsonUtility ignores them — verified.

## Deploying to Railway

Everything is read from the environment; nothing is hardcoded and `.env` is gitignored.
`railway.json` declares the build (`npm ci && npm run build`) and start (`npm start`).

Set these as Railway variables:

| variable | needed for | notes |
|---|---|---|
| `BLOCKFROST_API_KEY` | reads **and** writes | Preprod or Preview project key |
| `BLOCKFROST_URL` | reads **and** writes | e.g. `https://cardano-preprod.blockfrost.io/api/v0` |
| `NETWORK` | writes only | `Preprod` / `Preview` — Lucid needs it |
| `SERVICE_WALLET_SEED` | writes only | BIP-39 seed. **Never** commit or log it |
| `ALLOWED_ORIGINS` | browser access | comma-separated exact origins; unset = nothing cross-origin |

`PORT` is supplied by Railway and read via `process.env.PORT`.

**The service ADDRESS is not a variable.** It is derived from `SERVICE_WALLET_SEED` — ask the
running service for it with `GET /service/address`, which is what you fund with test ADA. There is
nothing to set, so the address can never drift out of sync with the key that actually signs.

**Reads need only the first two.** You can deploy and exercise `/read/health` and `/components`
with no wallet seed at all.

**`--env-file-if-exists=.env`** is on both `dev` and `start`, so a local `.env` is picked up and
Railway (which injects variables into the process, with no file present) is unaffected.

### The game also needs pointing at it

`Assets/WebGLTemplates/Coalescence/index.html` sets `window.COALESCENCE_SERVICE = "/api"`, which
only works if something proxies the service onto the game's own origin. Hosting the game on itch.io
and the service on Railway means setting that to the Railway URL **and** adding the game's origin to
`ALLOWED_ORIGINS` — both, or the browser blocks every call.
