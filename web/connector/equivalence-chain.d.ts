/**
 * The contract Unity calls. The page assigns an implementation to `window.EquivalenceChain`.
 *
 * THE WALLET'S HALF ONLY. Saves - characters and the hub room - are plain HTTP from C# to the
 * service (Assets/Scripts/Chain/Service), guest or wallet; they never come through here
 * (2026-10-13). This contract is what needs the browser: connecting, proving, what the wallet
 * holds, opening boxes and the PFP.
 *
 * THE AUTHORITY SPLIT, which is the design this contract exists to express:
 *
 *   the PLAYER's wallet   holds the component art, its boxes and the assembled PFP. Signs a
 *                         message to prove the address (`proveOwnership`, no fee, moves
 *                         nothing) and transactions moving its own tokens (`openBoxes`, the PFP).
 *
 *   the SERVICE           writes progression and room layout. No player signature, because
 *                         these are things the GAME asserts - and a player who could sign
 *                         their own progression datum could write whatever they liked into it.
 *
 * SHAPE RULES, enforced by the jslib rather than by types at runtime:
 *
 *  - Every method takes ONE object. Unity sends every field of ChainArgs each time; unused
 *    fields arrive as "".
 *  - Return a value or a Promise of one. A string passes through; any other object is
 *    JSON.stringify'd; null/undefined becomes "".
 *  - A REJECTED promise becomes a C# ChainException carrying the message. That is how a
 *    declined dialog or a failed submit should be reported - never a sentinel string.
 *  - Anything that never settles is failed C#-side after 90s.
 */
export interface EquivalenceChain {

  // ---- identity -------------------------------------------------------------------------

  /** COMMA-SEPARATED wallet keys visible to the page - "eternl,lace". A string, not an array. */
  listWallets(args: {}): string | Promise<string>;

  /** Authorise and resolve the bech32 address. Reject if the player declines. */
  connect(args: { wallet: string }): Promise<string>;

  /**
   * Prove control of the address by signing a MESSAGE - CIP-30 `signData`, never `signTx`.
   * The message and its one-time nonce come from the service (`GET /auth/challenge`); the
   * signature is traded at `POST /auth/prove` for a SESSION that every player write carries.
   * Costs no fee and spends no UTxO. `datum` is unused (kept for the bridge's argument shape).
   */
  proveOwnership(args: { accountId: string; datum?: string }): Promise<string>;

  disconnect(args: {}): void | Promise<void>;

  /** A quiet reconnect at boot - `{"wallet","address"}` JSON when a remembered session still holds
   *  and the wallet lets the site in without a popup, else ''. Never prompts. */
  resume(args: {}): Promise<string>;

  /** Reloads the page - the service said this build is too old (426). */
  reload(args: {}): void;

  /** 'true' when this browser holds a session for the address that still holds - proveOwnership
   *  will then resolve without the wallet. */
  remembered(args: { accountId: string }): Promise<string>;

    /** Ends every session of the connected address at the service, then disconnects here. */
  signOutEverywhere(args: {}): Promise<void>;

  /** A service request signed with the session key - the game's saves once a wallet is signed in.
   *  Resolves `{"status","body"}` JSON whatever the status; status 0 when nothing came back. */
  request(args: { method: string; path: string; body?: string }): Promise<string>;

  /**
   * The service SESSION `proveOwnership` obtained, or "" before a proof. C# hands it to the save
   * store (ServiceIdentity) so the guest's saves can graduate onto the wallet and every save after
   * carries the wallet's authority.
   */
  session(args: {}): string;

  // ---- what the wallet owns -------------------------------------------------------------

  /**
   * What this wallet holds, POLICY FIRST, as the service returns it: {"items":[], "held":[unit...],
   * "policies":[{ Policy, Name, Count }], truncated} - pass it through. No details here: those come
   * a policy page at a time from listComponentItems.
   */
  listComponents(args: { accountId: string }): Promise<{ items: unknown[] } | string>;

  /**
   * One policy's page of pieces (`policy` + `page`), or named units (`units`, comma-separated, at
   * most 24): {"items":[{ Key, Title, Collection, Slot, ImageUrl }], page, pages}. `Slot` names the
   * rig slot a component can fill or is empty for display-only art. `ImageUrl` is fetched lazily by
   * the game and capped - do not inline image data here.
   */
  listComponentItems(args: { accountId: string; policy?: string; page?: string; units?: string }): Promise<{ items: unknown[] } | string>;

  // ---- reads (no signature) -------------------------------------------------------------

  /**
   * The portrait this character has, as its PfpRecipe datum JSON, or "" if it has none.
   *
   * The game reads this to decide MINT vs UPDATE, and to show what a re-take would change. A
   * read that FAILS must reject rather than resolve "" - "" means "none yet" and sends the
   * caller to the mint path, and a duplicate token cannot be undone.
   */
  currentPfp(args: { profileId: string }): Promise<string>;

  // ---- design boxes (tokens; the SERVER rolls, the player approves the open) --------------

  /**
   * Open 1-10 Diamond / Black Diamond BOXES - tokens in this wallet, so the player approves ONE
   * transaction (signTx, partial); the service builds it, pays its fee and adds its own witness.
   * Always a random open (there is no targeted open for these tiers). The SERVER rolls - committed
   * before the signature, so declining and opening again shows the same designs. Resolves with the
   * service's JSON: { hash, tier, pieces: [{ instanceId, design, name, slot, unit }] }. A Black
   * Diamond weapon comes with its relic. The box's deposit returns to the service.
   */
  openBoxes(args: { profileId: string; tier: string; count?: string }): Promise<string>;

  /** The boxes this WALLET holds - a box is a token: { profileId, boxes: { Diamond, BlackDiamond } }. */
  designBoxes(args: { profileId: string }): Promise<string>;

  /** The portrait's year: { taken, revision, paidUntil, lapsed, prices }. */
  pfpStatus(args: { profileId: string }): Promise<string>;

  // ---- dyes (MONETIZATION.md "The plan as of 2026-10-25") - all else about dyes is the database's -

  /** Earned dyes out of the character's database count into this wallet as dye TOKENS. Minted TO
   *  the player by the service (no signature), a PFP holder only. Resolves { hash }. */
  withdrawDyes(args: { profileId: string; swatch: string; count: string }): Promise<string>;

  /** Dye TOKENS in this wallet burned into the character's database count - the player approves
   *  (signTx, partial), the service pays. Resolves { hash }. */
  depositDyes(args: { profileId: string; swatch: string; count: string }): Promise<string>;

  /** Diamond box TOKENS burned into dye charges - the player approves (signTx, partial), the
   *  service pays. Resolves { hash }. */
  breakBoxes(args: { profileId: string; tier: string; count: string }): Promise<string>;

  // ---- the ONE player-signed write ------------------------------------------------------

  /**
   * FIRST take only - mints the portrait token into the player's wallet.
   *
   * MUST re-check that the wallet holds every component in the recipe. The game checks first so
   * it can name the missing parts in the UI, but that list is a cache and a token can be sold in
   * another tab between the check and the signature.
   *
   * `datum.MaxFloor` is SERVICE-attested - take it from your own record of the character, never
   * from what the client sent, or a player can claim any depth they like. It is the one field in
   * this datum the player must not be able to author.
   *
   * REJECT a mint for a character that already has a portrait. The game checks first, but two
   * tabs can race and a duplicate cannot be taken back.
   */
  assemblePfp(args: { profileId: string; accountId: string; datum: string }): Promise<string>;

  /**
   * EVERY re-take after the first - a CIP-68 datum update on the (100) reference token. The
   * (222) token in the player's wallet is untouched: the same trophy, saying something new.
   *
   * TWO SIGNATURES, and the split is the point. The PLAYER signs because they choose when a
   * trophy is re-taken and they pay for it. The SERVICE signs because `datum.MaxFloor` is
   * attested, not claimed - take it from your own record of the character, never from what the
   * client sent, or a player can write any depth they like into their own trophy. A reference
   * token spendable by the player alone makes the floor meaningless; one spendable by the
   * service alone makes "the player chooses when" untrue.
   *
   * Reject a `datum.Revision` that is not exactly one past what is on chain, or two takes in
   * flight silently overwrite each other.
   *
   * Same ownership re-check as the mint.
   */
  updatePfp(args: { profileId: string; accountId: string; datum: string }): Promise<string>;

  /** Another YEAR on the portrait (MONETIZATION.md: 200 ADA) - the player pays inside the transaction
   *  they sign, the service writes the new paid-until date. Resolves the hash. */
  renewPfp(args: { profileId: string; accountId: string }): Promise<string>;


}

declare global {
  interface Window {
    EquivalenceChain?: EquivalenceChain;
  }
}
