/**
 * Blockfrost IPFS - the same two calls donada-mint's upload_artwork.ts / mint_on_demand.ts make:
 * `POST /ipfs/add` (multipart) returns the CID, `POST /ipfs/pin/add/{cid}` keeps it. A SEPARATE
 * Blockfrost project from the chain key (IPFS keys start `ipfs`), set as `BLOCKFROST_IPFS_KEY`.
 *
 * Adding identical bytes twice returns the same CID and pinning is idempotent, so a retry after a
 * half-finished upload is harmless - the art index (artIndex.ts) is what saves the upload itself.
 */
const IPFS_URL = 'https://ipfs.blockfrost.io/api/v0';

function key(): string {
  const k = process.env.BLOCKFROST_IPFS_KEY;
  if (!k) throw new Error('BLOCKFROST_IPFS_KEY is not set - art cannot be pinned');
  return k;
}

export async function addAndPin(bytes: Buffer, filename: string, mime: string): Promise<string> {
  const form = new FormData();
  form.append('file', new Blob([bytes], { type: mime }), filename);
  const added = await fetch(`${IPFS_URL}/ipfs/add`, {
    method: 'POST',
    headers: { project_id: key() },
    body: form,
  });
  if (!added.ok) throw new Error(`IPFS add failed (${added.status}): ${await added.text()}`);
  const cid = ((await added.json()) as { ipfs_hash: string }).ipfs_hash;

  const pinned = await fetch(`${IPFS_URL}/ipfs/pin/add/${cid}`, {
    method: 'POST',
    headers: { project_id: key() },
  });
  if (!pinned.ok) throw new Error(`IPFS pin failed for ${cid} (${pinned.status}): ${await pinned.text()}`);
  return cid;
}

/**
 * Whether Blockfrost still holds the pin. `queued` counts: the CID is already fixed and the pin
 * is on its way, so a mint pointing at it is pointing at the right bytes.
 */
export async function isPinned(cid: string): Promise<boolean> {
  const res = await fetch(`${IPFS_URL}/ipfs/pin/list/${cid}`, { headers: { project_id: key() } });
  if (res.status === 404) return false;
  if (!res.ok) throw new Error(`IPFS pin check failed for ${cid} (${res.status}): ${await res.text()}`);
  const { state } = (await res.json()) as { state: string };
  return state === 'pinned' || state === 'queued';
}
