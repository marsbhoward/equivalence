# Chain connector

The TypeScript half of the browser build. Unity calls `window.CoalescenceChain`; this
implements it.

    Unity C#                     jslib                    this connector
    ChainProfileStore  ──Call──▶ CoalescenceChainCall ──▶ window.CoalescenceChain[method](args)
                       ◀─Task─── OnChainResult        ◀── Promise resolve / reject

## Build it into the template

    npx esbuild coalescence-chain.ts --bundle --format=iife \
      --outfile=../../Assets/WebGLTemplates/Coalescence/connector/coalescence-chain.js

Unity copies the template folder to the build output, so the path the page loads is
`connector/coalescence-chain.js` relative to `index.html`.

## What is already done, and what is not

`connect` / `listWallets` / `disconnect` are complete — that is the CIP-30 half, and it is the
part that ports directly from donada.

Everything reaching the chain is a **seam**, marked in the source. Transaction building is left
to a service (`/tx/<kind>` then `/tx/submit`) rather than done in the browser, for two reasons:
the service owns the protocol parameters, the script address and the datum schema in one place,
and Unity never has to do anything but pass strings.

## Rules the C# side depends on

- **A write resolves with a transaction hash.** An empty string is recorded as a FAILED
  checkpoint in the in-game terminal, because a confirmed row with no hash claims a write that
  did not happen.
- **Reject, do not resolve with a sentinel.** A rejection becomes a `ChainException` carrying
  your message, which is what the terminal shows and what a player reads.
- **`listProfiles` stays wrapped** as `{"items":[...]}`. Unity's `JsonUtility` cannot read a
  top-level array.
- **`listWallets` returns a comma-separated string**, not an array.
- Anything that never settles is failed C#-side after 90 seconds.

## Datum shape

`datum` is a serialised `CharacterProfile` — the same JSON `LocalJsonProfileStore` writes to
disk today, so you can take a local save file as a fixture. The store mutates the profile
*before* serialising, so the datum you receive is already the post-run state; do not recompute
progression on the service.
