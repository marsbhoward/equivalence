/**
 * Read-only Blockfrost access.
 *
 * SEPARATE FROM chain.ts ON PURPOSE, and the split is the point rather than tidiness. `getLucid()`
 * selects the service wallet from `SERVICE_WALLET_SEED` and therefore refuses to start without one
 * - which is correct for anything that SIGNS, and wrong for everything that only reads. Sharing it
 * would mean:
 *
 *   - you cannot test a single read until you have created and funded a service wallet, which is a
 *     lot of ceremony to stand between "does Blockfrost answer" and finding out; and
 *   - the read path would hold a funded wallet's private key it never uses, which is a secret
 *     sitting somewhere it has no business being.
 *
 * So reads need the API key and nothing else. Writes keep their own module, and the seed with it.
 */

// ipfs.io itself rate-limits anonymous requests aggressively (429, verified live rather than
// assumed) and dweb.link/nftstorage.link both either share its infrastructure or redirect
// straight back to it, so none of the three actually help. Pinata's public gateway answered
// cleanly under the same load. IPFS_GATEWAY overrides this per the project's own
// everything-from-the-environment rule, for whenever this one also starts throttling us.
const IPFS_GATEWAY = process.env.IPFS_GATEWAY || 'https://gateway.pinata.cloud/ipfs/';

function config() {
  const key = process.env.BLOCKFROST_API_KEY;
  const url = process.env.BLOCKFROST_URL;
  if (!key || !url) {
    throw new Error(
      'reads need BLOCKFROST_API_KEY and BLOCKFROST_URL - copy .env.example to .env. ' +
        'A read does NOT need SERVICE_WALLET_SEED.',
    );
  }
  return { key, url: url.replace(/\/$/, '') };
}

/** One GET against Blockfrost. 404 comes back as null - "no such thing" is an answer, not a fault. */
export async function bf<T>(path: string): Promise<T | null> {
  const { key, url } = config();
  const r = await fetch(`${url}${path}`, { headers: { project_id: key } });
  if (r.status === 404) return null;
  if (!r.ok) throw new Error(`blockfrost ${path} -> ${r.status} ${await r.text()}`);
  return (await r.json()) as T;
}

/** Which network the key is pointed at, proven by asking rather than by trusting NETWORK. */
export async function networkInfo() {
  const [health, latest] = await Promise.all([
    bf<{ is_healthy: boolean }>('/health'),
    bf<{ height: number; epoch: number; time: number }>('/blocks/latest'),
  ]);
  const { url } = config();
  return {
    url,
    healthy: health?.is_healthy ?? false,
    height: latest?.height ?? null,
    epoch: latest?.epoch ?? null,
    // Derived from the URL rather than from the NETWORK env var, so a key and a network that
    // disagree show up here instead of much later as a confusing empty read.
    network: url.includes('preprod') ? 'Preprod'
           : url.includes('preview') ? 'Preview'
           : url.includes('mainnet') ? 'MAINNET'
           : 'unknown',
  };
}

type BfAmount = { unit: string; quantity: string };
type BfAsset = {
  asset: string;
  policy_id: string;
  asset_name: string | null;
  onchain_metadata: Record<string, unknown> | null;
  metadata: Record<string, unknown> | null;
};

/** Every native token at an address, lovelace excluded. */
export async function assetsAt(address: string): Promise<BfAmount[]> {
  const acc = await bf<{ amount: BfAmount[] }>(`/addresses/${encodeURIComponent(address)}`);
  if (!acc) return [];
  return acc.amount.filter(a => a.unit !== 'lovelace');
}

export async function assetInfo(unit: string): Promise<BfAsset | null> {
  return bf<BfAsset>(`/assets/${unit}`);
}

/** ipfs://Qm... is not a URL a browser can load. Anything already http(s) is left alone. */
export function resolveImage(value: unknown): string {
  const raw = Array.isArray(value) ? value.join('') : value;   // CIP-25 allows a split string
  if (typeof raw !== 'string' || !raw) return '';
  if (raw.startsWith('ipfs://')) return IPFS_GATEWAY + raw.slice('ipfs://'.length);
  if (/^Qm[1-9A-HJ-NP-Za-km-z]{44}$/.test(raw)) return IPFS_GATEWAY + raw;
  return raw;
}

/** Hex asset name back to text, when it is text. Falls back to the hex. */
export function assetNameText(hexName: string | null): string {
  if (!hexName) return '';
  try {
    const s = Buffer.from(hexName, 'hex').toString('utf8');
    return /^[\x20-\x7E]*$/.test(s) ? s : hexName;
  } catch {
    return hexName;
  }
}
