/**
 * The browser connector - the WALLET's half of the game (equivalence-chain.d.ts). Saves never
 * come through here: C# writes them to the service over HTTP (Assets/Scripts/Chain/Service).
 *
 * WHO SIGNS WHAT. Ordinary play never prompts the player: progression and room layout are
 * written by the SERVICE, because they are things the game asserts rather than things the
 * player authorises - and a player who could sign their own progression could write whatever
 * they liked into it. The player's wallet signs a message to prove the address, and the
 * transactions that move its own tokens: opening boxes, and the PFP.
 */

import type { EquivalenceChain } from './equivalence-chain.d';   // the CONTRACT - the bare name is this file

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
/** From /auth/prove - what every player write carries. Never the operator's WRITE_TOKEN, which
 *  must never reach a browser. */
let session = '';

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
    headers: {
      'content-type': 'application/json',
      ...(session ? { authorization: `Bearer ${session}` } : {}),
      ...(init?.headers ?? {}),
    },
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

  async proveOwnership({ accountId }) {
    if (!api) throw new Error('no wallet connected');
    // The NONCE comes from the service, never from here - a nonce the client chose would let a
    // captured signature be replayed. One nonce, one attempt, five minutes.
    const ch = await service(`/auth/challenge?address=${encodeURIComponent(accountId)}`);
    const { nonce, message } = await ch.json();

    // signData, NOT signTx. No fee, no UTxO, nothing to approve beyond "yes this is my address".
    const sig = await api.signData(accountId, hex(message));

    // The service is the one that has to believe it: it verifies the signature and hands back a
    // session, which every player write then carries. A signature the client merely holds
    // proves nothing.
    const proved = await service('/auth/prove', {
      method: 'POST',
      body: JSON.stringify({ address: accountId, nonce, ...sig }),
    });
    session = (await proved.json()).session ?? '';
    if (!session) throw new Error('the service issued no session');
    return sig.signature;
  },

  disconnect() {
    api = null;
    address = '';
    session = '';
  },

  session() {
    return session;
  },

  // ---- what the wallet owns ---------------------------------------------------------------

  async listComponents({ accountId }) {
    if (!accountId) return { items: [] };
    // SEAM: every component token this address holds, resolved to its metadata. Return the image
    // URL rather than the image - the game fetches lazily and caps how many it keeps resident.
    // The service already wraps the list ({"items":[...], truncated, held}) - JsonUtility cannot
    // read a bare array. Wrapping it again handed Unity {"items":{"items":[...]}} and an empty list.
    const r = await service(`/components?address=${encodeURIComponent(accountId)}`);
    return r.json();
  },

  // ---- reads ------------------------------------------------------------------------------

  async currentPfp({ profileId }) {
    // Rejects on a transport failure rather than resolving "" - "" means "no portrait yet" and
    // would send the caller down the MINT path for a character that already has one.
    const r = await service(`/pfp/${encodeURIComponent(profileId)}`);
    const body = await r.text();
    return body === 'null' || body === '' ? '' : body;   // the PfpRecipe datum, verbatim
  },

  // ---- design boxes (tokens in the wallet; opening them is the one other thing a player signs) -

  async openBoxes({ profileId, tier, count }) {
    if (!api) throw new Error('no wallet connected');
    // The SERVER rolls the designs (committed before this signature - declining and opening again
    // shows the same ones), builds the transaction, pays its fee and keeps its own witness. The
    // boxes are tokens in this wallet, so the player approves; partial: true keeps the service's
    // half. The game plays its opening animation meanwhile - the reveal waits on the submit.
    const built = await (await service('/write/open-boxes', {
      method: 'POST',
      body: JSON.stringify({
        profileId, tier, count: Number(count) || 1,
      }),
    })).json();
    const witness = await api.signTx(built.cbor, true);
    const done = await service('/write/open-boxes/submit', {
      method: 'POST',
      body: JSON.stringify({ openId: built.openId, witness }),
    });
    return done.text();
  },

  async designBoxes({ profileId }) {
    const r = await service(`/boxes/${encodeURIComponent(profileId)}`);
    return r.text();
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

// The game reads window.EquivalenceChain lazily, but StoreFactory decides ONCE - assign before
// the Unity instance boots if you want the chain-backed stores chosen.
window.EquivalenceChain = connector;

export { LABEL_REFERENCE, LABEL_USER };
