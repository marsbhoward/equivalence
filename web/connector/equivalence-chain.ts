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

// ---- the session key ------------------------------------------------------------------------------
//
// A wallet session is BOUND to a P-256 key made here and never exportable (web/service/src/session.ts):
// every request the session authorises is signed with it, so the session token alone - which is
// kept in localStorage so a reload or a later visit does not ask the wallet again - is useless
// anywhere but this browser. The key lives in IndexedDB as a CryptoKey object, which the browser
// stores without ever handing script the private half.

const KEY_DB = 'equivalence-keys';
const STORED = 'equivalence.wallet';

type Stored = { wallet: string; address: string; session: string; expiresAt: number };

function idb<T>(mode: IDBTransactionMode, run: (s: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  return new Promise((resolve, reject) => {
    const open = indexedDB.open(KEY_DB, 1);
    open.onupgradeneeded = () => open.result.createObjectStore('keys');
    open.onerror = () => reject(open.error);
    open.onsuccess = () => {
      const tx = open.result.transaction('keys', mode);
      const req = run(tx.objectStore('keys'));
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
      tx.oncomplete = () => open.result.close();
    };
  });
}

let keyPair: CryptoKeyPair | null = null;

/** This browser's session key, made on first use. */
async function sessionKey(): Promise<CryptoKeyPair> {
  if (keyPair) return keyPair;
  const kept = await idb<CryptoKeyPair | undefined>('readonly', s => s.get('session')).catch(() => undefined);
  if (kept?.privateKey && kept.publicKey) return (keyPair = kept);
  const made = await crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign', 'verify']);
  await idb('readwrite', s => s.put(made, 'session')).catch(() => undefined);
  return (keyPair = made);
}

/** A new key next sign-in - signing out drops the old one, which ends its session here for good. */
async function dropKey() {
  keyPair = null;
  await idb('readwrite', s => s.delete('session')).catch(() => undefined);
}

/** The public half as the service names it: `<x>.<y>`, base64url. */
async function publicName(): Promise<string> {
  const jwk = await crypto.subtle.exportKey('jwk', (await sessionKey()).publicKey);
  return `${jwk.x}.${jwk.y}`;
}

const b64url = (buf: ArrayBuffer) =>
  btoa(String.fromCharCode(...new Uint8Array(buf))).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

/** The nonce the service last named (every response carries X-Proof-Nonce), and its clock's offset. */
let proofNonce = '';
let clockOffset = 0;

/** X-Session-Proof for one request: METHOD \n url \n ts \n nonce \n sha256(body). */
async function proof(method: string, url: URL, body: string): Promise<string> {
  const ts = String(Date.now() + clockOffset);
  const bodyHash = b64url(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(body)));
  const signed = `${method.toUpperCase()}\n${url.pathname}${url.search}\n${ts}\n${proofNonce}\n${bodyHash}`;
  const sig = await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, (await sessionKey()).privateKey,
    new TextEncoder().encode(signed));
  return `${ts}.${proofNonce}.${b64url(sig)}`;
}

function remember(wallet: string, expiresAt: number) {
  try {
    localStorage.setItem(STORED, JSON.stringify({ wallet, address, session, expiresAt } satisfies Stored));
  } catch { /* storage blocked: this visit still works, the next one asks again */ }
}

function recalled(): Stored | null {
  try {
    const s = JSON.parse(localStorage.getItem(STORED) ?? 'null') as Stored | null;
    return s && s.session && s.address && s.expiresAt > Date.now() + 60_000 ? s : null;
  } catch {
    return null;
  }
}

function forget() {
  try { localStorage.removeItem(STORED); } catch { /* nothing to remove */ }
}

/** Which wallet is connected - remembered alongside the session, for a quiet reconnect. */
let walletKey = '';

/**
 * Where the service lives. Read off a global rather than import.meta, because this bundles to an
 * IIFE loaded by a plain <script> tag before the Unity instance boots - import.meta.env is empty
 * in that format and the setting would be silently ignored rather than reported.
 *
 * Set it in index.html ahead of this script:  window.EQUIVALENCE_SERVICE = 'https://...'
 */
const SERVICE = (window as any).EQUIVALENCE_SERVICE ?? '/api';

/** This build's place in the service's compatibility line - kept equal to ServiceClient.ClientBuild
 *  (Assets/Scripts/Chain/Service/ServiceClient.cs); bump both together. */
const CLIENT_BUILD = '1';

/** A request to the service, signed with the session key whenever there is a session. Retried
 *  once when the service says the proof's nonce or clock was stale - it has just named fresh ones. */
async function send(path: string, init: RequestInit = {}, retried = false): Promise<Response> {
  const url = new URL(`${SERVICE}${path}`, location.href);
  const method = (init.method ?? 'GET').toUpperCase();
  const body = typeof init.body === 'string' ? init.body : '';
  const r = await fetch(url, {
    ...init,
    headers: {
      'content-type': 'application/json',
      'x-client-build': CLIENT_BUILD,
      ...(session ? { authorization: `Bearer ${session}`, 'x-session-proof': await proof(method, url, body) } : {}),
      ...(init.headers ?? {}),
    },
  });
  const nonce = r.headers.get('x-proof-nonce');
  if (nonce) proofNonce = nonce;
  const date = Date.parse(r.headers.get('date') ?? '');
  if (!Number.isNaN(date)) clockOffset = date - Date.now();
  if (r.status === 401 && session && !retried) {
    const code = (await r.clone().json().catch(() => ({})))?.code;
    if (code === 'proof_nonce' || code === 'proof_clock' || code === 'proof_missing') return send(path, init, true);
  }
  return r;
}

async function service(path: string, init?: RequestInit): Promise<Response> {
  const r = await send(path, init);
  if (!r.ok) {
    // The rejection's message is what the player reads (ChainException) - the service's own words
    // when it gave any (a paused feature names why), the raw status otherwise.
    const text = await r.text().catch(() => r.statusText);
    let said = '';
    try { said = JSON.parse(text)?.message ?? JSON.parse(text)?.error ?? ''; } catch { /* not JSON */ }
    throw new Error(said && typeof said === 'string' ? said : `${path} -> ${r.status} ${text}`);
  }
  return r;
}

/** Whether the remembered session for this address still holds, asked of the service - and the
 *  answer trusted for half a minute, since a connect asks twice (remembered, then proveOwnership). */
let heldAt = 0;
async function sessionHolds(forAddress: string): Promise<boolean> {
  const kept = recalled();
  if (!kept || !sameAddress(kept.address, forAddress)) return false;
  session = kept.session;
  if (Date.now() - heldAt < 30_000) return true;
  const r = await send('/auth/session').catch(() => null);
  if (r?.ok) { heldAt = Date.now(); return true; }
  heldAt = 0;
  // Expired, signed out elsewhere, or made with a key this browser no longer has.
  session = '';
  if (r && r.status === 401) forget();
  return false;
}

/** CIP-30 hands out hex; the service answers bech32. Compared by asking nothing: both are stored
 *  as the wallet gave them, and a mismatch only costs one extra signature. */
const sameAddress = (a: string, b: string) => a.toLowerCase() === b.toLowerCase();

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
    walletKey = wallet;
    return address;
  },

  async resume() {
    // A QUIET reconnect at boot: only when a session is remembered, the wallet already lets this
    // site in (isEnabled - no popup), the wallet still shows the same address, and the service
    // still honours the session. Anything less resolves '' and the player connects as usual.
    const kept = recalled();
    if (!kept) return '';
    const provider = (window as any).cardano?.[kept.wallet];
    if (!provider || typeof provider.isEnabled !== 'function') return '';
    if (!(await provider.isEnabled().catch(() => false))) return '';
    try {
      api = await provider.enable();
      const used = await api!.getUsedAddresses();
      const unused = used.length ? [] : await api!.getUnusedAddresses();
      const now = used[0] ?? unused[0] ?? '';
      if (!now || !sameAddress(now, kept.address)) { api = null; return ''; }
      if (!(await sessionHolds(now))) { api = null; return ''; }
      address = now;
      walletKey = kept.wallet;
      return JSON.stringify({ wallet: kept.wallet, address });
    } catch {
      api = null;
      return '';
    }
  },

  async proveOwnership({ accountId }) {
    if (!api) throw new Error('no wallet connected');
    // A session this browser already holds for this address - no second popup.
    if (await sessionHolds(accountId)) return 'remembered';
    session = '';
    // The NONCE comes from the service, never from here - a nonce the client chose would let a
    // captured signature be replayed. One nonce, one attempt, five minutes.
    const ch = await service(`/auth/challenge?address=${encodeURIComponent(accountId)}` +
      `&key=${encodeURIComponent(await publicName())}`);
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
    const issued = await proved.json();
    session = issued.session ?? '';
    if (!session) throw new Error('the service issued no session');
    remember(walletKey, Number(issued.expiresAt) || Date.now());
    return sig.signature;
  },

  async disconnect() {
    // Signing out HERE: the remembered session and the key it is bound to both go, so the
    // session cannot be used again even if a copy of the token survives somewhere.
    api = null;
    address = '';
    session = '';
    walletKey = '';
    heldAt = 0;
    forget();
    await dropKey();
  },

  async signOutEverywhere() {
    // Every session of this address, on every device, ends at the service - then this one here,
    // whether or not the service answered (a failed call must not leave this browser signed in).
    try {
      if (session) await service('/auth/signout-everywhere', { method: 'POST', body: '{}' });
    } finally {
      await connector.disconnect({});
    }
  },

  reload() {
    // The service no longer talks to this build (426) - the player said reload.
    location.reload();
  },

  async remembered({ accountId }) {
    // Whether proveOwnership will need the wallet at all - the game only announces the signature
    // step when it is really coming.
    return (await sessionHolds(accountId)) ? 'true' : '';
  },

  async request({ method, path, body }) {
    // The game's own saves (ServiceClient) once a wallet is signed in: they must carry the session
    // key's proof, and only this side holds the key. Never rejects on an HTTP status - the game
    // reads the status itself, exactly as it does for its own requests.
    try {
      const r = await send(path, { method, ...(body ? { body } : {}) });
      // `service`: answered by the service itself, not a proxy's error page - ServiceClient retries
      // only the latter through an outage.
      return JSON.stringify({ status: r.status, body: await r.text(), service: !!r.headers.get('x-proof-nonce') });
    } catch (e) {
      return JSON.stringify({ status: 0, body: '', error: (e as Error).message });
    }
  },

  session() {
    return session;
  },

  // ---- what the wallet owns ---------------------------------------------------------------

  async listComponents({ accountId }) {
    if (!accountId) return { items: [] };
    // SEAM: what this wallet holds, grouped by policy - details come per policy page
    // (listComponentItems). The service already wraps the list ({"items":[...], truncated, held}) - JsonUtility cannot
    // read a bare array. Wrapping it again handed Unity {"items":{"items":[...]}} and an empty list.
    const r = await service(`/components?address=${encodeURIComponent(accountId)}`);
    return r.json();
  },

  async listComponentItems({ accountId, policy, page, units }) {
    if (!accountId) return { items: [] };
    const q = units
      ? `units=${encodeURIComponent(units)}`
      : `policy=${encodeURIComponent(policy ?? '')}&page=${encodeURIComponent(page || '0')}`;
    const r = await service(`/components/items?address=${encodeURIComponent(accountId)}&${q}`);
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

  async pfpStatus({ profileId }) {
    // { taken, revision, paidUntil, lapsed, prices } - the year the portrait is paid until.
    const r = await service(`/pfp/${encodeURIComponent(profileId)}/status`);
    return r.text();
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

  // ---- dyes (all else about dyes is the database's - the game reaches it over HTTP) ----------

  async withdrawDyes({ profileId, swatch, count }) {
    // Minted TO the player: the service signs alone, nothing to approve.
    const r = await service('/write/dyes/withdraw', {
      method: 'POST',
      body: JSON.stringify({ profileId, swatch, count: Number(count) || 1 }),
    });
    return r.text();
  },

  depositDyes({ profileId, swatch, count }) {
    return burn('/write/dyes/deposit', { profileId, swatch, count: Number(count) || 1 });
  },

  breakBoxes({ profileId, tier, count }) {
    return burn('/write/break-boxes', { profileId, tier, count: Number(count) || 1 });
  },

  // ---- the ONE player-signed write ---------------------------------------------------------

  assemblePfp({ profileId, accountId, datum }) {
    return trophy('/write/pfp/mint', profileId, accountId, datum);
  },

  updatePfp({ profileId, accountId, datum }) {
    // A CIP-68 datum update on the (100) reference token. The (222) token in the wallet is
    // untouched - the same trophy, saying something new.
    return trophy('/write/pfp/update', profileId, accountId, datum);
  },

  renewPfp({ profileId, accountId }) {
    // Another year on the portrait's record - minting to the wallet resumes. Paid like a take.
    return trophy('/write/pfp/renew', profileId, accountId, '');
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

  // The service builds it - the player's coins in, the price left with the service, the depth
  // attested - and keeps it; only a witness comes back.
  const built = await service(endpoint, {
    method: 'POST',
    body: JSON.stringify({ profileId, address: accountId, recipe: datum }),
  });
  const { pfpId, cbor } = await built.json();

  // partial: true - the service's witness is already on it and must not be discarded.
  const witness = await api.signTx(cbor, true);

  const done = await service('/write/pfp/submit', {
    method: 'POST',
    body: JSON.stringify({ pfpId, witness }),
  });
  const { hash } = await done.json();
  if (!hash) throw new Error('submit returned no hash');
  return hash;
}

/**
 * A burn of the player's own tokens (dyes into the database, boxes into charges): the service
 * builds the transaction, pays its fee and keeps its witness; the wallet co-signs (partial) and the
 * service submits and credits the character. Resolves the service's { hash }.
 */
async function burn(endpoint: string, body: Record<string, unknown>) {
  if (!api) throw new Error('no wallet connected');
  const built = await (await service(endpoint, { method: 'POST', body: JSON.stringify(body) })).json();
  const witness = await api.signTx(built.cbor, true);
  const done = await service(`${endpoint}/submit`, {
    method: 'POST',
    body: JSON.stringify({ burnId: built.burnId, witness }),
  });
  return done.text();
}

// The game reads window.EquivalenceChain lazily, but StoreFactory decides ONCE - assign before
// the Unity instance boots if you want the chain-backed stores chosen.
window.EquivalenceChain = connector;

export { LABEL_REFERENCE, LABEL_USER };
