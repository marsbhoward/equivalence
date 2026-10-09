import { mkdir, readFile, rename, writeFile } from 'node:fs/promises';
import { dirname } from 'node:path';

/**
 * ART KEY -> pinned CIDs. Pieces share art, so an image is rendered and pinned ONCE per key and
 * every later mint of that key just points at it.
 *
 * The key is the DRAWN item's id as the game names it: the design id (`aether_greatsword`), or a
 * dyed copy's `design@dye.<main>.<accent>` (`GearDye.DyedCopy` - swatch ids are permanent, `-` is
 * an undyed channel). Two dyes of one set are two pictures, so two keys. The service never parses
 * it; it only has to be the same string for the same picture.
 *
 * Kept by the service because Blockfrost's pin list carries CIDs only, no labels. One JSON file -
 * on Railway a VOLUME (`ART_INDEX_PATH`, e.g. /data/art-index.json), or the container's disk
 * forgets it on every deploy and every piece is re-rendered once (harmless - identical bytes give
 * the same CID - but wasted). Written whole to a temp file and renamed, so a crash mid-write can't
 * leave half a file.
 */
export interface PinnedFile {
  cid: string;
  bytes: number;
  width: number;
  height: number;
}

export interface ArtEntry {
  /** The 3200 x 3200 render - the token's `image`. */
  image: PinnedFile;
  /** The native 1x menu art - `files[]`, the source of truth (see CLAUDE.md, NFT item images). */
  native: PinnedFile;
  pinnedAt: string;
}

interface IndexFile {
  version: 1;
  entries: Record<string, ArtEntry>;
}

const PATH = process.env.ART_INDEX_PATH || './art-index.json';

export const ART_KEY = /^[a-z0-9_]{1,64}(@[a-z0-9_.-]{1,64})?$/i;

let loaded: Promise<IndexFile> | null = null;
let writes: Promise<unknown> = Promise.resolve();

function load(): Promise<IndexFile> {
  loaded ??= readFile(PATH, 'utf8')
    .then((text) => JSON.parse(text) as IndexFile)
    .catch((e: NodeJS.ErrnoException) => {
      if (e.code === 'ENOENT') return { version: 1, entries: {} } as IndexFile;
      throw e; // a corrupt index must not silently become an empty one
    });
  return loaded;
}

export async function lookup(artKey: string): Promise<ArtEntry | undefined> {
  return (await load()).entries[artKey];
}

/** Serialised, so two pins finishing together can't each write a file missing the other. */
function save(mutate: (index: IndexFile) => void): Promise<void> {
  const next = writes.then(async () => {
    const index = await load();
    mutate(index);
    await mkdir(dirname(PATH), { recursive: true });
    const tmp = `${PATH}.tmp`;
    await writeFile(tmp, JSON.stringify(index, null, 2));
    await rename(tmp, PATH);
  });
  writes = next.catch(() => {});
  return next;
}

export const record = (artKey: string, entry: ArtEntry) =>
  save((index) => { index.entries[artKey] = entry; });

export const forget = (artKey: string) =>
  save((index) => { delete index.entries[artKey]; });

export const indexPath = () => PATH;
