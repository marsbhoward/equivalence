// A wallet session the way the browser connector holds one (web/connector/equivalence-chain.ts):
// a P-256 session key, its public half named in the challenge, and every request signed with it
// (X-Session-Proof - web/service/src/session.ts). Shared by the smoke scripts.
import { webcrypto as crypto } from 'node:crypto';

const b64url = (buf) => Buffer.from(buf).toString('base64url');
const hex = (s) => Buffer.from(s, 'utf8').toString('hex');

export async function sessionClient(url) {
  const keys = await crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign', 'verify']);
  const jwk = await crypto.subtle.exportKey('jwk', keys.publicKey);
  const c = { url, session: '', nonce: '', key: `${jwk.x}.${jwk.y}`, keys };

  c.proof = async (method, path, body) => {
    const u = new URL(url + path);
    const ts = String(Date.now());
    const bodyHash = b64url(await crypto.subtle.digest('SHA-256', new TextEncoder().encode(body ?? '')));
    const signed = `${method}\n${u.pathname}${u.search}\n${ts}\n${c.nonce}\n${bodyHash}`;
    const sig = await crypto.subtle.sign({ name: 'ECDSA', hash: 'SHA-256' }, c.keys.privateKey, new TextEncoder().encode(signed));
    return `${ts}.${c.nonce}.${b64url(sig)}`;
  };

  /** One request. `auth`: a session string (signed with this client's key), `prove: false` to send it unsigned. */
  c.call = async (path, { method = 'GET', body, auth, prove = true, headers = {} } = {}) => {
    const text = body === undefined ? undefined : (typeof body === 'string' ? body : JSON.stringify(body));
    const h = { 'Content-Type': 'application/json', ...headers };
    if (auth) {
      h.Authorization = `Bearer ${auth}`;
      if (prove) h['X-Session-Proof'] = await c.proof(method, path, text);
    }
    let r = await fetch(url + path, { method, headers: h, body: text });
    if (r.headers.get('x-proof-nonce')) c.nonce = r.headers.get('x-proof-nonce');
    const raw = await r.text();
    let json;
    try { json = JSON.parse(raw); } catch { json = raw; }
    // The first signed call of a fresh client has no nonce yet - the service names one; once more.
    if (r.status === 401 && json?.code === 'proof_nonce' && auth && prove && !c._retrying) {
      c._retrying = true;
      try { return await c.call(path, { method, body, auth, prove, headers }); } finally { c._retrying = false; }
    }
    return { status: r.status, json, headers: r.headers };
  };

  /** Signs in with a Lucid wallet (signMessage = CIP-30 signData). */
  c.login = async (lucid, address) => {
    const ch = await c.call(`/auth/challenge?address=${address}&key=${c.key}`);
    if (ch.status !== 200) return ch;
    const sig = await lucid.wallet().signMessage(address, hex(ch.json.message));
    const proved = await c.call('/auth/prove', { method: 'POST', body: { address, nonce: ch.json.nonce, ...sig } });
    if (proved.status === 200) c.session = proved.json.session;
    return { ...proved, challenge: ch.json };
  };
  return c;
}
