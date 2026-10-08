/**
 * Reference connector.
 *
 * The identity half is complete - that is the part that ports from donada. The chain half is
 * left as marked seams, because that is where your transaction building goes.
 *
 * WHO SIGNS WHAT. Ordinary play never prompts the player: progression and room layout are
 * written by the SERVICE with its own key, because they are things the game asserts rather than
 * things the player authorises - and a player who could sign their own progression could write
 * whatever they liked into it. The player's wallet signs exactly twice: once over a message to
 * prove the address, and once over the mint that puts their assembled PFP in their own wallet.
 */

import type { EquivalenceChain } from './equivalence-chain';

// CIP-68 asset name prefixes. The REFERENCE token carries the datum; the USER token is what the
// player holds. Components and PFPs are user tokens; progression lives on a reference token the
// service controls.
const LABEL_REFERENCE = '000643b0'; // (100)
const LABEL_USER      = '000de140'; // (222)

type DataSignature = { signature: string; key: string };

type Cip30Api = {
  getUsedAddresses(): Promise<string[]>;
  getUnusedAddresses(): Promise<string[]>;
  getUtxos(): Promise<string[]>;
  /** A MESSAGE signature. Cannot move funds. This is what proveOwnership uses. */
  signData(address: string, payload: string): Promise<DataSignature>;
  /** A TRANSACTION signature. Used for exactly one thing here: assemblePfp. */
  signTx(tx: string, partial?: boolean): Promise<string>;
  submitTx(tx: string): Promise<string>;
};

let api: Cip30Api | null = null;
let address = '';

/**
 * Where the service lives. Read off a global rather than import.meta, because this bundles to an
 * IIFE loaded by a plain <script> tag before the Unity instance boots - import.meta.env is empty
 * in that format and the setting would be silently ignored rather than reported.
 *
 * Set it in index.html ahead of this script:  window.EQUIVALENCE_SERVICE = 'https://...'
 */
const SERVICE = (window as any).EQUIVALENCE_SERVICE ?? '/api';

async function service(path: string, init?: RequestInit): Promise<Response> {
  const r = await fetch(`${SERVICE}${path}`, {
    ...init,
    headers: { 'content-type': 'application/json', ...(init?.headers ?? {}) },
  });
  if (!r.ok) throw new Error(`${path} -> ${r.status} ${await r.text().catch(() => r.statusText)}`);
  return r;
}

/** Hex-encode for CIP-30 signData, which takes a hex payload rather than raw text. */
const hex = (s: string) =>
  Array.from(new TextEncoder().encode(s)).map(b => b.toString(16).padStart(2, '0')).join('');

export const connector: EquivalenceChain = {

  // ---- identity ---------------------------------------------------------------------------

  listWallets() {
    const c = (window as any).cardano;
    if (!c) return '';
    // Only things that actually look like a CIP-30 provider - the namespace collects other keys,
    // and offering one with no enable() is a button that cannot work.
    return Object.keys(c).filter(k => typeof c[k]?.enable === 'function').join(',');
  },

  async connect({ wallet }) {
    const provider = (window as any).cardano?.[wallet];
    if (!provider) throw new Error(`wallet "${wallet}" is not installed`);

    api = await provider.enable();          // rejects if the player declines - let it propagate
    const used = await api!.getUsedAddresses();
    const unused = used.length ? [] : await api!.getUnusedAddresses();
    address = used[0] ?? unused[0] ?? '';
    if (!address) throw new Error('wallet authorised but exposed no address');
    return address;
  },

  async proveOwnership({ accountId, datum }) {
    if (!api) throw new Error('no wallet connected');
    // signData, NOT signTx. No fee, no UTxO, nothing to approve beyond "yes this is my address".
    const message = `Equivalence login\naddress: ${accountId}\nnonce: ${datum}`;
    const sig = await api.signData(accountId, hex(message));

    // The service is the one that has to believe it - it stores the proof and thereafter accepts
    // progression writes for this address. A signature the client merely holds proves nothing.
    await service('/auth/prove', {
      method: 'POST',
      body: JSON.stringify({ address: accountId, nonce: datum, ...sig }),
    });
    return sig.signature;
  },

  disconnect() {
    api = null;
    address = '';
  },

  // ---- what the wallet owns ---------------------------------------------------------------

  async listComponents({ accountId }) {
    if (!accountId) return { items: [] };
    // SEAM: every component token this address holds, resolved to its metadata. Return the image
    // URL rather than the image - the game fetches lazily and caps how many it keeps resident.
    const r = await service(`/components?address=${encodeURIComponent(accountId)}`);
    return { items: await r.json() };       // MUST stay wrapped - JsonUtility cannot read a bare array
  },

  // ---- reads ------------------------------------------------------------------------------

  async loadProfile({ profileId }) {
    const r = await service(`/profile/${encodeURIComponent(profileId)}`);
    const body = await r.text();
    return body === 'null' ? '' : body;     // "" means no record yet; Unity starts a fresh character
  },

  async listProfiles() {
    if (!address) return { items: [] };
    const r = await service(`/profiles?address=${encodeURIComponent(address)}`);
    return { items: await r.json() };
  },

  async loadAccount({ accountId }) {
    const r = await service(`/account/${encodeURIComponent(accountId)}`);
    const body = await r.text();
    return body === 'null' ? '' : body;
  },

  async accountExists({ accountId }) {
    const r = await fetch(`${SERVICE}/account/${encodeURIComponent(accountId)}`, { method: 'HEAD' });
    return r.ok;
  },

  async currentPfp({ profileId }) {
    // Rejects on a transport failure rather than resolving "" - "" means "no portrait yet" and
    // would send the caller down the MINT path for a character that already has one.
    const r = await service(`/pfp/${encodeURIComponent(profileId)}`);
    const body = await r.text();
    return body === 'null' || body === '' ? '' : body;   // the PfpRecipe datum, verbatim
  },

  // ---- SERVICE-signed writes ---------------------------------------------------------------
  //
  // No signTx, no dialog, no player interaction. The service builds, signs with its own key and
  // submits; the browser only says what happened. If you ever find yourself adding a signature
  // step to one of these, the authority split has been broken.

  beginRun({ profileId, runId, element, datum }) {
    return write('begin-run', { profileId, runId, element, datum });
  },

  commitRun({ profileId, datum, summary }) {
    return write('commit-run', { profileId, datum, summary });
  },

  unlock({ profileId, datum }) {
    return write('unlock', { profileId, datum });
  },

  saveAccount({ accountId, datum }) {
    // Room layout. Very likely off-chain - how a hub room was arranged is not something a
    // transaction should pay for - but it is still service-authored and still keyed by address.
    return write('account', { accountId, datum });
  },

  // ---- the ONE player-signed write ---------------------------------------------------------

  assemblePfp({ profileId, accountId, datum }) {
    return trophy('/tx/mint-pfp', profileId, accountId, datum);
  },

  updatePfp({ profileId, accountId, datum }) {
    // A CIP-68 datum update on the (100) reference token. The (222) token in the wallet is
    // untouched - the same trophy, saying something new.
    return trophy('/tx/update-pfp', profileId, accountId, datum);
  },
};

/**
 * Taking a trophy - the ONE transaction a player is ever asked to approve, and the only one
 * carrying two signatures.
 *
 * The service builds and adds its own witness, because MaxFloor is attested rather than claimed
 * and a reference token the player could spend alone would let them write any depth into their
 * own trophy. It also re-checks that the wallet still holds every component, and that the
 * revision is exactly one past what is on chain. The player then signs because they are the one
 * choosing to re-take it, and paying for it.
 */
async function trophy(endpoint: string, profileId: string, accountId: string, datum: string) {
  if (!api) throw new Error('no wallet connected');

  const built = await service(endpoint, {
    method: 'POST',
    body: JSON.stringify({ profileId, address: accountId, recipe: datum }),
  });
  const { cbor } = await built.json();

  // partial: true - the service's witness is already on it and must not be discarded.
  const witness = await api.signTx(cbor, true);

  const done = await service('/tx/submit', {
    method: 'POST',
    body: JSON.stringify({ cbor, witness }),
  });
  const { hash } = await done.json();
  if (!hash) throw new Error('submit returned no hash');
  return hash;
}

/**
 * A service-authored write. Built, signed and submitted server-side; the player is not involved.
 * The address is sent so the service can check it against the proof from `proveOwnership` -
 * without that check anyone could post progression for anyone.
 */
async function write(kind: string, payload: Record<string, string>): Promise<string> {
  const r = await service(`/write/${kind}`, {
    method: 'POST',
    body: JSON.stringify({ ...payload, address }),
  });
  const { hash } = await r.json();
  return hash ?? '';                        // "" is recorded as a failed checkpoint, not a silent success
}

// The game reads window.EquivalenceChain lazily, but StoreFactory decides ONCE - assign before
// the Unity instance boots if you want the chain-backed stores chosen.
window.EquivalenceChain = connector;

export { LABEL_REFERENCE, LABEL_USER };
