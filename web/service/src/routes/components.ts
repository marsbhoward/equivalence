import type { Request, Response } from 'express';
import { assetInfo, assetNameText, assetsAt, resolveImage } from '../blockfrost.js';

/**
 * GET /components?address=addr_test1...
 *
 * Every token an address holds, shaped as the game's OwnedComponent:
 *
 *     {"items":[{ "Key", "Title", "Collection", "Slot", "ImageUrl" }]}
 *
 * WRAPPED IN `items`, NOT A BARE ARRAY. Unity's JsonUtility cannot deserialise a top-level array -
 * this is the same constraint the connector's own comment already records, and returning a bare
 * array here is the kind of thing that reads as "the wallet owns nothing" rather than as an error.
 *
 * Field-by-field, so a future CIP-68 pass has something to match:
 *
 *     Key         the asset unit (policy + hex name) - stable identity, and the same key a
 *                 RoomLayout placement stores and a PfpRecipe names
 *     Title       CIP-25 `name`, else the asset name decoded from hex
 *     Collection  the metadata's own collection if it declares one, else the policy id
 *     Slot        `slot` from the metadata, naming a rig slot ("Head", "Weapon"); empty means
 *                 display-only art, which is the common case for arbitrary wallet contents
 *     ImageUrl    resolved to something a browser can actually fetch - ipfs:// is not
 *
 * QUANTITY IS IGNORED and every unit appears once. These are art, and holding two of a token does
 * not give you two of the component.
 */
export async function components(req: Request, res: Response) {
  const address = String(req.query.address ?? '');
  if (!address) {
    res.status(400).json({ error: 'address required' });
    return;
  }

  try {
    const held = await assetsAt(address);

    // Resolved in parallel but capped, because an address can hold hundreds and each one is its
    // own Blockfrost call - an unbounded fan-out here would rate-limit the key on one request.
    const units = held.slice(0, MAX_ASSETS).map(a => a.unit);
    const infos = await Promise.all(units.map(u => assetInfo(u).catch(() => null)));

    const items = infos.filter(Boolean).map(a => {
      const m = (a!.onchain_metadata ?? a!.metadata ?? {}) as Record<string, unknown>;
      return {
        Key: a!.asset,
        Title: String(m.name ?? assetNameText(a!.asset_name) ?? ''),
        Collection: String(m.collection ?? a!.policy_id),
        Slot: String(m.slot ?? ''),
        ImageUrl: resolveImage(m.image),
      };
    });

    res.json({ items, truncated: held.length > MAX_ASSETS, held: held.length });
  } catch (e) {
    // 502 rather than 500: the service is fine, the thing behind it did not answer. The game
    // deliberately KEEPS its previous list on a failed refresh rather than emptying it, so the
    // status code is the only place this is visible.
    res.status(502).json({ error: String((e as Error).message ?? e) });
  }
}

const MAX_ASSETS = 60;
