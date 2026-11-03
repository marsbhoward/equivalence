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

/** The clipboard, or the old select-and-copy where the async clipboard is refused (an iframe, an
 *  older Safari). Rejects when both are. */
async function copyText(text: string): Promise<void> {
  try { await navigator.clipboard.writeText(text); return; } catch { /* fall through */ }
  const area = document.createElement('textarea');
  area.value = text;
  area.setAttribute('readonly', '');
  area.style.position = 'fixed';
  area.style.opacity = '0';
  document.body.appendChild(area);
  area.select();
  const ok = document.execCommand('copy');
  area.remove();
  if (!ok) throw new Error('The browser would not copy.');
}

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

// ---- a wallet in another browser (device links, web/service/src/link.ts) ---------------------------
//
// On a phone the game runs in Safari or from the Home Screen, where no wallet can inject itself. The
// WALLET APP choice signs through our /link page opened in the wallet's own browser instead: the
// sign-in there earns a session bound to THIS browser's key, and each transaction is posted to the
// service, signed on that page, and its witness collected here. Everything else in this file is
// unchanged by it - only signData at sign-in and signTx go elsewhere.
//
// TODO (the user's note, 2026-11-01): a DESKTOP-TO-MOBILE route - the game on a computer, the
// wallet on a phone: a QR code of the link on this panel, scanned by the wallet app. The page and
// the codes already serve it; offering WALLET APP on desktop and drawing the QR are what is left.

/** The key listWallets offers for it, and ChainWallet.AppWallet names on the C# side. */
const APP = 'wallet-app';

const mobile = () => /iPhone|iPad|iPod|Android/i.test(navigator.userAgent) ||
  (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);

/** CIP-158: opens a page inside a wallet app's browser (Eternl, VESPR). */
const inWalletApp = (url: string) => `web+cardano://browse/v1?uri=${encodeURIComponent(url)}`;

const linkUrl = (code: string) => new URL(`${SERVICE}/link/?c=${code}`, location.href).href;

const POLL_MS = 2000;

/** The panel over the game while a link is open - the game cannot draw a link the browser will
 *  follow from a tap, so this does. Resolves when `wait` settles; Cancel rejects. */
function linkPanel(opts: { title: string; lead: string; code: string },
                   wait: (cancelled: () => boolean, status: (s: string) => void) => Promise<string>): Promise<string> {
  const style = document.createElement('style');
  style.textContent = `
    #eq-link { position: fixed; inset: 0; z-index: 20; display: flex; align-items: center; justify-content: center;
      background: rgba(5,5,8,.82); font: 15px/1.45 system-ui, -apple-system, sans-serif; color: #cfc6b4;
      padding: env(safe-area-inset-top) env(safe-area-inset-right) env(safe-area-inset-bottom) env(safe-area-inset-left); }
    #eq-link .p { width: min(560px, calc(100% - 32px)); max-height: 100%; overflow: auto; background: #0b0b10;
      border: 1px solid #3a3226; padding: 18px 20px; text-align: center; }
    #eq-link h2 { margin: 0 0 6px; font-size: 18px; font-weight: 600; letter-spacing: .06em; color: #e2c487; }
    #eq-link p { margin: 0 0 10px; }
    #eq-link .code { font: 600 28px/1.2 ui-monospace, Menlo, monospace; letter-spacing: .2em; color: #e2c487;
      border: 1px solid #3a3226; padding: 6px; margin: 6px auto 12px; max-width: 260px; }
    #eq-link .row { display: flex; gap: 8px; flex-wrap: wrap; justify-content: center; margin-bottom: 10px; }
    #eq-link a, #eq-link button { flex: 1 1 150px; font: inherit; letter-spacing: .05em; text-decoration: none;
      color: #cfc6b4; background: rgba(255,255,255,.03); border: 1px solid #3a3226; padding: 10px 12px; cursor: pointer; }
    #eq-link a.go { color: #0b0b10; background: #e2c487; border-color: #e2c487; font-weight: 600; }
    #eq-link .small { color: #8a8aa4; font-size: 13px; }
    #eq-link .status { min-height: 1.4em; color: #8a8aa4; font-size: 13px; margin: 0; }`;
  const root = document.createElement('div');
  root.id = 'eq-link';
  const url = linkUrl(opts.code);
  root.innerHTML = `<div class="p" role="dialog" aria-modal="true">
      <h2></h2><p class="lead"></p><div class="code"></div>
      <div class="row"><a class="go">Open wallet app</a><button type="button" class="copy">Copy link</button></div>
      <p class="small"></p>
      <p class="status"></p>
      <div class="row"><button type="button" class="cancel">Cancel</button></div></div>`;
  const q = (sel: string) => root.querySelector(sel) as HTMLElement;
  q('h2').textContent = opts.title;
  q('.lead').textContent = opts.lead;
  q('.code').textContent = `${opts.code.slice(0, 3)}-${opts.code.slice(3)}`;
  (q('.go') as HTMLAnchorElement).href = inWalletApp(url);
  q('.small').textContent = `Opens in Eternl or VESPR. Another wallet: copy the link into its browser, ` +
    `or go to ${new URL(url).host}/link there and type the code.`;
  q('.copy').addEventListener('click', () => {
    navigator.clipboard?.writeText(url).then(() => { q('.copy').textContent = 'Link copied'; }, () => { /* no clipboard */ });
  });
  // Keys and taps stay on the panel, not the game underneath.
  for (const t of ['keydown', 'keyup', 'pointerdown', 'touchstart', 'wheel']) root.addEventListener(t, e => e.stopPropagation());
  document.head.appendChild(style);
  document.body.appendChild(root);

  let cancelled = false;
  return new Promise<string>((resolve, reject) => {
    q('.cancel').addEventListener('click', () => { cancelled = true; reject(new Error('cancelled')); });
    wait(() => cancelled, s => { q('.status').textContent = s; }).then(resolve, reject);
  }).finally(() => { root.remove(); style.remove(); });
}

/** Asks `ask` every two seconds, and at once when the page comes back from the wallet app, until it
 *  answers something other than undefined. */
function pollUntil<T>(cancelled: () => boolean, expiresAt: number, ask: () => Promise<T | undefined>): Promise<T> {
  return new Promise<T>((resolve, reject) => {
    let timer = 0;
    let asking = false;
    const stop = () => { clearTimeout(timer); document.removeEventListener('visibilitychange', now); };
    const tick = async () => {
      if (cancelled()) { stop(); return; }
      if (Date.now() > expiresAt) { stop(); reject(new Error('the code expired - try again')); return; }
      if (asking) return;
      asking = true;
      try {
        const r = await ask();
        if (r !== undefined) { stop(); resolve(r); return; }
      } catch (e) {
        stop(); reject(e); return;
      } finally {
        asking = false;
      }
      timer = window.setTimeout(tick, POLL_MS);
    };
    const now = () => { if (document.visibilityState === 'visible') { clearTimeout(timer); void tick(); } };
    document.addEventListener('visibilitychange', now);
    void tick();
  });
}

/** WALLET APP sign-in: a code for this browser's key; the session arrives when the page has signed. */
async function linkSignIn(): Promise<{ address: string; expiresAt: number }> {
  const started = await (await service('/link/start', {
    method: 'POST', body: JSON.stringify({ key: await publicName() }),
  })).json();
  const done = JSON.parse(await linkPanel({
    title: 'Connect your wallet app',
    lead: 'Open the wallet app and sign in there - it checks this code. Signing is free and is not a transaction.',
    code: started.code,
  }, (cancelled, status) => {
    status('Waiting for the wallet...');
    return pollUntil(cancelled, started.expiresAt, async () => {
      const r = await (await service(`/link/poll?code=${started.code}&poll=${encodeURIComponent(started.poll)}`)).json();
      if (r.state === 'expired') throw new Error('the code expired - try again');
      return r.state === 'done' ? JSON.stringify(r) : undefined;
    });
  }));
  session = done.session;
  if (!session) throw new Error('the service issued no session');
  return { address: done.address, expiresAt: Number(done.expiresAt) || Date.now() };
}

/** A transaction the service built, signed in the wallet app (partial) - its witness, as signTx gives it. */
async function linkSign(cbor: string, what: string): Promise<string> {
  const req = await (await service('/link/sign', { method: 'POST', body: JSON.stringify({ cbor, what }) })).json();
  return linkPanel({
    title: 'Approve in your wallet app',
    lead: 'The wallet shows exactly what this moves. Come back here when it is approved.',
    code: req.code,
  }, (cancelled, status) => {
    status('Waiting for the wallet...');
    return pollUntil(cancelled, req.expiresAt, async () => {
      const r = await (await service(`/link/signed?code=${req.code}`)).json();
      if (r.state === 'expired') throw new Error('the code expired - try again');
      if (r.state === 'declined') throw new Error('declined in the wallet');
      return r.state === 'signed' ? String(r.witness) : undefined;
    });
  });
}

/** Stands in for the CIP-30 api when the wallet is the WALLET APP, so "is a wallet connected" holds.
 *  Nothing calls it directly: transactions go through signTx() below, and sign-in was the link's. */
const appApi = {
  signTx: () => Promise.reject(new Error('the wallet app signs through signTx()')),
  signData: () => Promise.reject(new Error('the wallet app signed in through its link')),
} as unknown as Cip30Api;

/** The one way this file signs a transaction: the injected wallet, or the wallet app via a link. */
function signTx(cbor: string, what: string): Promise<string> {
  if (!api) throw new Error('no wallet connected');
  return walletKey === APP ? linkSign(cbor, what) : api.signTx(cbor, true);
}

/** Hex-encode for CIP-30 signData, which takes a hex payload rather than raw text. */
const hex = (s: string) =>
  Array.from(new TextEncoder().encode(s)).map(b => b.toString(16).padStart(2, '0')).join('');

export const connector: EquivalenceChain = {

  // ---- identity ---------------------------------------------------------------------------

  listWallets() {
    const c = (window as any).cardano ?? {};
    // Only things that actually look like a CIP-30 provider - the namespace collects other keys,
    // and offering one with no enable() is a button that cannot work.
    const found = Object.keys(c).filter(k => typeof c[k]?.enable === 'function');
    // A phone browser with none injected: the wallet is an app - offer it (device links). Inside a
    // wallet's own browser one IS injected, and that is used directly.
    if (!found.length && mobile()) found.push(APP);
    return found.join(',');
  },

  async connect({ wallet }) {
    if (wallet === APP) {
      const linked = await linkSignIn();
      api = appApi;
      address = linked.address;
      walletKey = APP;
      heldAt = Date.now();
      remember(APP, linked.expiresAt);
      return address;
    }
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
    if (kept.wallet === APP) {
      // The wallet app is not here to ask - the session alone says who this is.
      if (!(await sessionHolds(kept.address).catch(() => false))) return '';
      api = appApi;
      address = kept.address;
      walletKey = APP;
      return JSON.stringify({ wallet: APP, address });
    }
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

  async share({ text }) {
    // A phone opens its share sheet (messages, socials); a desktop browser's sheet is thin, so it
    // copies instead. Unity handles the tap inside the gesture's activation window.
    const coarse = typeof matchMedia === 'function' && matchMedia('(pointer: coarse)').matches;
    if (coarse && typeof navigator.share === 'function') {
      try { await navigator.share({ text }); return 'shared'; }
      catch (e) { if ((e as Error)?.name === 'AbortError') return 'cancelled'; }
    }
    await copyText(text);
    return 'copied';
  },

  async copy({ text }) {
    await copyText(text);
    return 'copied';
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
    const witness = await signTx(built.cbor, 'open-boxes');
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
  const witness = await signTx(cbor, endpoint.endsWith('/renew') ? 'pfp-renew' : 'pfp');

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
  const witness = await signTx(built.cbor, endpoint.includes('dyes') ? 'dyes' : 'break-boxes');
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
