/**
 * WARMING A WALLET (2026-10-31): the slow work done in the background as soon as a wallet is read, so
 * the art chooser opens on names and pictures that are already here.
 *
 * What is slow is the first fetch of a picture - an IPFS gateway, then a shrink (routes/img.ts) - and
 * a policy's name, which is its first token's metadata. Right after /components answers, this:
 *   1. fetches the first token of every policy whose name is not cached yet (the game asks
 *      /components once more a few seconds later and finds them - WalletInventory);
 *   2. fetches the metadata and the shrunk picture of the first FIRST_PAGE pieces of each policy -
 *      exactly what the chooser shows when a policy is opened.
 *
 * Bounded every way, since it spends the shared Blockfrost budget and gateway time for nobody yet:
 * one queue for the whole service, CONCURRENCY at a time; at most MAX_PER_WALLET pictures a wallet; a
 * wallet warmed once per AGAIN_MS; the queue capped (a flood is dropped, not queued). Everything it
 * fetches lands in the same caches the routes read (asset_cache, the image directory), so a real
 * request that arrives first simply does the work itself - nothing waits on this.
 */
import { cachedAssetInfo, cachedAssetInfos } from './assetCache.js';
import { warmPicture } from './routes/img.js';

const CONCURRENCY = 3;
/** The chooser's first page (UI/ShowcasePicker: 4 x 2). */
const FIRST_PAGE = 8;
const MAX_PER_WALLET = 96;
const AGAIN_MS = 10 * 60_000;
const MAX_QUEUE = 2000;

type Task = () => Promise<unknown>;
const queue: Task[] = [];
let running = 0;
const warmedAt = new Map<string, number>();

function pump() {
  while (running < CONCURRENCY && queue.length) {
    const task = queue.shift()!;
    running++;
    task().catch(() => {}).finally(() => { running--; pump(); });
  }
}

function enqueue(tasks: Task[]) {
  const room = Math.max(0, MAX_QUEUE - queue.length);
  queue.push(...tasks.slice(0, room));
  pump();
}

/** Warms a wallet just read: names first (they are what the chooser lists), then first pages. */
export async function warmWallet(address: string, byPolicy: Map<string, string[]>): Promise<void> {
  const now = Date.now();
  if ((warmedAt.get(address) ?? 0) > now - AGAIN_MS) return;
  warmedAt.set(address, now);
  if (warmedAt.size > 5000) for (const [a, t] of warmedAt) if (t < now - AGAIN_MS) warmedAt.delete(a);

  // Names: the first token of each policy not cached already (a zero budget reads the cache only).
  const firsts = [...byPolicy.values()].map(units => units[0]);
  const cached = await cachedAssetInfos(firsts, 0).catch(() => new Map());
  enqueue(firsts.filter(u => !cached.has(u)).map(u => () => cachedAssetInfo(u)));

  // Pictures: each policy's first page, until the wallet's allowance runs out.
  let budget = MAX_PER_WALLET;
  const pictures: Task[] = [];
  for (const units of byPolicy.values()) {
    for (const unit of units.slice(0, FIRST_PAGE)) {
      if (budget-- <= 0) break;
      pictures.push(async () => {
        const info = await cachedAssetInfo(unit);
        if (info) await warmPicture(unit, (info.onchain_metadata ?? info.metadata ?? {}) as Record<string, unknown>);
      });
    }
    if (budget <= 0) break;
  }
  enqueue(pictures);
}
