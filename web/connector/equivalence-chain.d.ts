/**
 * The contract Unity calls. The page assigns an implementation to `window.EquivalenceChain`.
 *
 * THE AUTHORITY SPLIT, which is the design this contract exists to express:
 *
 *   the PLAYER's wallet   holds the component art and the assembled PFP.
 *                         Signs exactly two things: a message to prove the address
 *                         (`proveOwnership`, no fee, moves nothing) and the PFP mint
 *                         (`assemblePfp`, an asset going TO them).
 *
 *   the SERVICE's wallet  writes progression and room layout. No player signature, because
 *                         these are things the GAME asserts - and a player who could sign
 *                         their own progression datum could write whatever they liked into it.
 *
 * So beginRun / commitRun / unlock / saveAccount must NOT prompt the player. If one of them
 * opens a wallet dialog, the split has been broken somewhere.
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
   * Resolves with the signature; the service checks it before writing on this address's behalf.
   * `datum` carries a fresh nonce. Costs no fee and spends no UTxO.
   */
  proveOwnership(args: { accountId: string; datum: string }): Promise<string>;

  disconnect(args: {}): void | Promise<void>;

  // ---- what the wallet owns -------------------------------------------------------------

  /**
   * The component art this wallet holds, wrapped: {"items":[{ Key, Title, Collection, Slot,
   * ImageUrl }]}. `Slot` names the rig slot a component can fill ("Head", "Weapon", ...) or is
   * empty for display-only art. `ImageUrl` is fetched lazily by the game and capped - do not
   * inline image data here.
   */
  listComponents(args: { accountId: string }): Promise<{ items: unknown[] } | string>;

  // ---- reads (no signature) -------------------------------------------------------------

  /** The character datum as JSON, or "" when this id has no record yet. */
  loadProfile(args: { profileId: string }): Promise<string>;

  /** Every character this wallet owns, wrapped: {"items":[ ...CharacterProfile ]}. */
  listProfiles(args: {}): Promise<{ items: unknown[] } | string>;

  loadAccount(args: { accountId: string }): Promise<string>;

  accountExists(args: { accountId: string }): Promise<boolean | string>;

  /**
   * The portrait this character has, as its PfpRecipe datum JSON, or "" if it has none.
   *
   * The game reads this to decide MINT vs UPDATE, and to show what a re-take would change. A
   * read that FAILS must reject rather than resolve "" - "" means "none yet" and sends the
   * caller to the mint path, and a duplicate token cannot be undone.
   */
  currentPfp(args: { profileId: string }): Promise<string>;

  // ---- SERVICE-signed writes: the three checkpoints, plus the room ----------------------
  //
  // These must not prompt the player. Each resolves with a transaction hash; an empty string
  // is recorded as a FAILED checkpoint in the in-game terminal.

  beginRun(args: { profileId: string; runId: string; element: string; datum: string }): Promise<string>;
  commitRun(args: { profileId: string; datum: string; summary: string }): Promise<string>;
  unlock(args: { profileId: string; datum: string }): Promise<string>;

  /** Room layout and account record. Keyed by wallet address, or "local" when unconnected. */
  saveAccount(args: { accountId: string; datum: string }): Promise<string>;

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


}

declare global {
  interface Window {
    EquivalenceChain?: EquivalenceChain;
  }
}
