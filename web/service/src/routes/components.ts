import type { Request, Response } from 'express';
import { getAddressDetails } from '@lucid-evolution/lucid';
import { internal } from '../errors.js';
import { assetNameText, walletAssets } from '../blockfrost.js';
import { imagePath } from './img.js';
import { cardOf, htmlPath } from './nft.js';
import { cachedAssetInfos } from '../assetCache.js';

/**
 * What a wallet holds, POLICY FIRST (2026-10-31).
 *
 * Every token's details is its own Blockfrost call, and every call the service makes comes from one
 * IP under one rate limit (10 a second, bursts of 500) shared by every player. Resolving a whole
 * wallet up front is what the old 60-token cap was protecting; a policy is the HEAD of a token's
 * unit, so the wallet can be grouped with no details at all, and details are fetched only for the
 * policy the player opens in the game (UI/ShowcasePicker), a page at a time. No cap on what a
 * wallet can show - a large collection is paged, not cut off.
 *
 *   GET /components?address=addr...
 *     {"items":[], "held":[unit...], "policies":[{ "Policy", "Name", "Count" }], "truncated"}
 *
 *     held      every NFT the wallet holds (quantity 1 - fungible tokens are not art). Enough for
 *               an ownership check (PfpRecipe.MissingComponents) without a single details call.
 *     policies  grouped, with a NAME (the metadata's collection) when one is cached or affordable
 *               this request - "" otherwise, and the game shows a shortened policy id.
 *     items     always empty here; kept so the shape stays an OwnedComponent list for JsonUtility.
 *
 *   GET /components/items?address=addr...&policy=<56 hex>&page=0
 *   GET /components/items?address=addr...&units=<unit>,<unit>,...
 *     {"items":[{ "Key", "Title", "Collection", "Slot", "ImageUrl", "HtmlUrl" }], "page", "pages"}
 *
 *     One policy's page (PAGE_SIZE units), or named units (a placed frame's piece, restored on load).
 *     Only units the wallet HOLDS are answered, and only those with a picture.
 *
 * WRAPPED IN `items`, NOT A BARE ARRAY. Unity's JsonUtility cannot deserialise a top-level array.
 *
 * OwnedComponent, field by field:
 *
 *     Key         the asset unit (policy + hex name) - stable identity, and the same key a
 *                 RoomLayout placement stores and a PfpRecipe names
 *     Title       CIP-25 `name`, else the asset name decoded from hex
 *     Collection  the metadata's own collection if it declares one, else the policy id
 *     Slot        `slot` from the metadata, naming a rig slot ("Head", "Weapon"); empty means
 *                 display-only art, which is the common case for arbitrary wallet contents
 *     ImageUrl    `/img/<unit>?v=..` on this service (routes/img.ts) - never the gateway itself;
 *                 the page may only talk to its own origin
 *     HtmlUrl     `/nft/<unit>?v=..` - the piece's live page, ONLY for an HTML_POLICIES collection
 *                 (routes/nft.ts); '' otherwise
 *     HasCard     true when Card is there: the colours and labels of the card the page draws, so
 *     Card        the game can draw it spinning on the wall (routes/nft.ts parseCard)
 */
export async function components(req: Request, res: Response) {
  const address = addressOf(req, res);
  if (!address) return;
  try {
    const { held, truncated } = await heldBy(address);

    const byPolicy = new Map<string, string[]>();
    for (const unit of held) {
      const policy = unit.slice(0, 56);
      const list = byPolicy.get(policy);
      if (list) list.push(unit); else byPolicy.set(policy, [unit]);
    }

    // A name per policy from its first token: free when cached, and a few fetched each request so a
    // new wallet's names fill in over a refresh or two rather than spending the shared budget at once.
    const firsts = [...byPolicy.values()].map(units => units[0]);
    const infos = await cachedAssetInfos(firsts, NAME_FETCHES);
    const policies = [...byPolicy.entries()].map(([policy, units]) => {
      const info = infos.get(units[0]);
      const m = (info?.onchain_metadata ?? info?.metadata ?? {}) as Record<string, unknown>;
      return { Policy: policy, Name: typeof m.collection === 'string' ? m.collection : '', Count: units.length };
    });

    res.json({ items: [], held, policies, truncated });
  } catch (e) {
    // 502 rather than 500: the service is fine, the thing behind it did not answer. The game
    // deliberately KEEPS its previous list on a failed refresh rather than emptying it, so the
    // status code is the only place this is visible.
    internal(res, e, 'components', 502, 'chain_unavailable');
  }
}

export async function componentItems(req: Request, res: Response) {
  const address = addressOf(req, res);
  if (!address) return;

  const policy = String(req.query.policy ?? '');
  const named = String(req.query.units ?? '').split(',').filter(Boolean);
  if (!/^[0-9a-f]{56}$/i.test(policy) && named.length === 0) {
    res.status(400).json({ error: 'policy or units required' });
    return;
  }
  if (named.length > PAGE_SIZE) {
    res.status(400).json({ error: `at most ${PAGE_SIZE} units` });
    return;
  }

  try {
    const { held } = await heldBy(address);
    let units: string[];
    let page = 0, pages = 1;
    if (named.length > 0) {
      const mine = new Set(held);
      units = named.filter(u => mine.has(u));
    } else {
      const all = held.filter(u => u.startsWith(policy.toLowerCase()));
      pages = Math.max(1, Math.ceil(all.length / PAGE_SIZE));
      page = Math.min(Math.max(0, Number(req.query.page ?? 0) | 0), pages - 1);
      units = all.slice(page * PAGE_SIZE, (page + 1) * PAGE_SIZE);
    }

    const infos = await cachedAssetInfos(units, PAGE_SIZE);
    // Our own collections' cards (routes/nft.ts), read in parallel - each is its page, fetched once.
    const cards = new Map(await Promise.all(units.map(async u => {
      const a = infos.get(u);
      const m = (a?.onchain_metadata ?? a?.metadata ?? {}) as Record<string, unknown>;
      return [u, a ? await cardOf(u, m).catch(() => null) : null] as const;
    })));
    const items = [];
    for (const unit of units) {
      const a = infos.get(unit);
      if (!a) continue;
      const m = (a.onchain_metadata ?? a.metadata ?? {}) as Record<string, unknown>;
      // Our own /img route, never the gateway itself (routes/img.ts) - the page may only talk to us.
      const image = imagePath(a.asset, m);
      if (!image) continue;   // a token with no picture is not art for a frame
      items.push({
        Key: a.asset,
        Title: String(m.name ?? assetNameText(a.asset_name) ?? ''),
        Collection: String(m.collection ?? a.policy_id),
        Slot: String(m.slot ?? ''),
        ImageUrl: image,
        // The live page, for our own collections only (routes/nft.ts) - '' for everything else.
        HtmlUrl: htmlPath(a.asset, a.policy_id, m),
        // The card the game draws on the wall for an HTML piece (Hub/NftCard); absent otherwise.
        ...(cards.get(unit) ? { HasCard: true, Card: cards.get(unit) } : {}),
      });
    }
    res.json({ items, page, pages });
  } catch (e) {
    internal(res, e, 'components/items', 502, 'chain_unavailable');
  }
}

/** Units per details page - and so the most Blockfrost calls one request can make. */
const PAGE_SIZE = 24;
/** Uncached policy names fetched per /components request. */
const NAME_FETCHES = 12;
/** Stake-account pages (100 tokens each) read per wallet - a 2,000-token wallet is read whole. */
const MAX_PAGES = 20;

/**
 * The wallet's NFTs, remembered for a minute: a picker paging through one policy asks for the
 * holdings on every page, and each read is up to MAX_PAGES Blockfrost calls.
 */
const recent = new Map<string, { at: number; held: string[]; truncated: boolean }>();
const RECENT_MS = 60_000;
const RECENT_MAX = 500;

async function heldBy(address: string) {
  const hit = recent.get(address);
  if (hit && Date.now() - hit.at < RECENT_MS) return hit;

  const { amounts, truncated } = await walletAssets(address, MAX_PAGES);
  // Quantity 1 only: a fungible token is not a piece of art, and used to fill the cap before any art.
  const held = amounts.filter(a => a.unit !== 'lovelace' && a.quantity === '1').map(a => a.unit);
  const entry = { at: Date.now(), held, truncated };
  recent.delete(address);
  recent.set(address, entry);
  if (recent.size > RECENT_MAX) recent.delete(recent.keys().next().value!);
  return entry;
}

function addressOf(req: Request, res: Response): string {
  const given = String(req.query.address ?? '');
  if (!given) {
    res.status(400).json({ error: 'address required' });
    return '';
  }
  // A CIP-30 wallet hands the game its address in HEX; Blockfrost only takes bech32, and a hex
  // one came back as a 502 the game reads as "keep the old list" - an empty crate.
  const address = bech32Of(given);
  if (!address) res.status(400).json({ error: 'bad address' });
  return address;
}

function bech32Of(address: string): string {
  try {
    return getAddressDetails(address).address.bech32;
  } catch {
    return '';
  }
}
