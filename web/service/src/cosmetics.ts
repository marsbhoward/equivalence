import { randomInt } from 'node:crypto';
import type pg from 'pg';
import { designs, DyeRarity } from './designs.js';
import type { FormatId } from './runFormat.js';

/**
 * THE COSMETIC ECONOMY HELD PER CHARACTER (the user's calls, 2026-10-25; MONETIZATION.md "The plan
 * as of 2026-10-25") - decided and held HERE, never in the save (a save is the client's to write):
 *
 *   dyes       earned colours, a COUNT per swatch - a token only when withdrawn (routes/dyes.ts)
 *   charges    a Diamond box breaks into CHARGES_PER_BOX; one recolours one piece
 *   dyed       what each Diamond armour design wears on this character, per channel
 *   poses      portrait poses - UNTRADEABLE, never duplicated, never a token
 *   themes     hub THEMES ("hub.study") - a whole room, floor, wall and every hub item; untradeable like
 *              poses and on the SAME roll (the user's call, group 7: one roll shared with the poses)
 *   milestones once per character, by the floor first cleared
 *
 * Tables `character_items` (kind dye / charge / pose), `dyed_pieces`, `milestones` (db.ts 9). Every
 * row is keyed (owner, profile id) like every save, and moves with a graduating guest; it travels
 * with the character because the character is what it is keyed by.
 *
 * The game's copy of every number is Tuning.Cosmetics and Chain/CharacterItems.cs (Recolour) -
 * keep the two in step. The colours, the dyeable designs and the poses come from designs.json,
 * exported from the game itself.
 */
export const CHARGES_PER_BOX = 7;
export const AVATAR_RARE_DYE_CHANCE = 0.03;
export const FINAL_LATER_RARE_DYE_CHANCE = 0.04;
const RARE_WEIGHT = 3;
const VERY_RARE_WEIGHT = 1;
export const POSE_CHANCE = 0.01;
const POSE_GOLD_MIN = 2;
const POSE_GOLD_MAX = 3;

/**
 * MILESTONES by format - the first clear of each floor, once per character. The Short Descent's
 * first floor-25 clear (its avatar) pays a Diamond box and a dye, 30 / 40 / 50 a dye each; Classic
 * is at the same DEPTH (its floors count once, the Short Descent's twice); the showcase has none. The dye's rarity climbs
 * with the milestone.
 */
export const MILESTONES: Record<FormatId, number[]> = { short: [25, 30, 40, 50], classic: [50, 60, 80, 100], showcase: [] };
const MILESTONE_RARITY: DyeRarity[] = ['Uncommon', 'Uncommon', 'Rare', 'VeryRare'];
/** The milestone (index) that also pays the Diamond box. */
const MILESTONE_BOX_INDEX = 0;

/** A uniform draw in [0, 1) from the server's own randomness. */
const draw = () => randomInt(1_000_000) / 1_000_000;

/** True with probability p, from the server's own randomness. */
export const chance = (p: number) => draw() < p;

export const swatches = () => designs().swatches ?? [];
export const isSwatch = (id: string) => swatches().some((s) => s.id === id);
export const poses = () => designs().poses ?? [];
export const themes = () => designs().themes ?? [];
/** What a collectible id is: a hub theme ("hub.*") or a pose. */
export const collectibleKind = (id: string): 'theme' | 'pose' => (id.startsWith('hub.') ? 'theme' : 'pose');
export const dyeChannels = (piece: string): number => designs().dyeable?.[piece] ?? 0;

function pickWeighted<T>(items: T[], weight: (t: T) => number): T | null {
  const total = items.reduce((a, t) => a + weight(t), 0);
  if (total <= 0) return null;
  let r = draw() * total;
  for (const t of items) {
    r -= weight(t);
    if (r < 0) return t;
  }
  return items[items.length - 1];
}

/** A RARE dye: a Rare or Very Rare swatch, Rare three times as likely. */
export function rollRareDye(): string | null {
  const s = pickWeighted(swatches(), (x) => (x.rarity === 'Rare' ? RARE_WEIGHT : x.rarity === 'VeryRare' ? VERY_RARE_WEIGHT : 0));
  return s?.id ?? null;
}

function dyeOfRarity(rarity: DyeRarity): string | null {
  const s = pickWeighted(swatches(), (x) => (x.rarity === rarity ? 1 : 0));
  return s?.id ?? null;
}

export interface PoseRoll { pose: string | null; gold: number }

/**
 * A COLLECTIBLE roll (POSE_CHANCE) from the poses AND hub themes the character LACKS - one roll shared by
 * both (the user's call, group 7); with every one owned, Gold. The id answered is a pose's or a theme's
 * (collectibleKind tells them apart) - it rides the decision's `pose` field either way.
 */
export function rollPose(owned: Set<string>): PoseRoll {
  if (draw() >= POSE_CHANCE) return { pose: null, gold: 0 };
  const lacking = [...poses(), ...themes()].filter((p) => !owned.has(p.id));
  if (lacking.length === 0) return { pose: null, gold: POSE_GOLD_MIN + randomInt(POSE_GOLD_MAX - POSE_GOLD_MIN + 1) };
  return { pose: lacking[randomInt(lacking.length)].id, gold: 0 };
}

export interface MilestoneRoll { milestone: string; diamond: number; dye: string | null }

/** The milestone a format's floor is, or null. */
export function milestoneAt(format: FormatId, floor: number): MilestoneRoll | null {
  const i = MILESTONES[format].indexOf(floor);
  if (i < 0) return null;
  return {
    // Named by its PLACE in the list, not its floor, so the same milestone in either format is
    // the same milestone - once per character, whichever format reaches it first.
    milestone: `milestone.${i + 1}`,
    diamond: i === MILESTONE_BOX_INDEX ? 1 : 0,
    dye: dyeOfRarity(MILESTONE_RARITY[i] ?? 'Rare'),
  };
}

// ---------------------------------------------------------------------------------------- store

export interface Items {
  dyes: { swatch: string; count: number }[];
  charges: number;
  dyed: { piece: string; swatches: string[] }[];
  poses: string[];
  /** The hub themes the character has earned (never the Cloister). */
  themes: string[];
  milestones: string[];
  /** Diamond boxes the service HOLDS for the character (earned without a PFP) - breakable here. */
  heldDiamond: number;
}

type Q = Pick<pg.PoolClient, 'query'>;

export async function itemsOf(c: Q, owner: string, profileId: string): Promise<Items> {
  const rows = (await c.query<{ kind: string; item: string; count: number }>(
    'SELECT kind, item, count FROM character_items WHERE owner = $1 AND profile_id = $2 AND count > 0 ORDER BY kind, item',
    [owner, profileId])).rows;
  const dyed = (await c.query<{ piece: string; swatches: string[] }>(
    'SELECT piece, swatches FROM dyed_pieces WHERE owner = $1 AND profile_id = $2 ORDER BY piece',
    [owner, profileId])).rows;
  const ms = (await c.query<{ milestone: string }>(
    'SELECT milestone FROM milestones WHERE owner = $1 AND profile_id = $2 ORDER BY milestone',
    [owner, profileId])).rows;
  const held = (await c.query<{ n: string | null }>(
    `SELECT sum(count) AS n FROM held_boxes WHERE owner = $1 AND profile_id = $2 AND tier = 'Diamond' AND released_at IS NULL`,
    [owner, profileId])).rows[0]?.n;
  return {
    heldDiamond: Number(held ?? 0),
    dyes: rows.filter((r) => r.kind === 'dye').map((r) => ({ swatch: r.item, count: r.count })),
    charges: rows.find((r) => r.kind === 'charge')?.count ?? 0,
    dyed: dyed.map((d) => ({ piece: d.piece, swatches: d.swatches })),
    poses: rows.filter((r) => r.kind === 'pose').map((r) => r.item),
    themes: rows.filter((r) => r.kind === 'theme').map((r) => r.item),
    milestones: ms.map((m) => m.milestone),
  };
}

/** Adds (or with a negative delta, takes) items, logged. Never below zero - the caller checks first. */
export async function credit(c: Q, owner: string, profileId: string, kind: 'dye' | 'charge' | 'pose' | 'theme',
                             item: string, delta: number, reason: string) {
  if (!delta) return;
  await c.query(
    `INSERT INTO character_items (owner, profile_id, kind, item, count) VALUES ($1, $2, $3, $4, GREATEST(0, $5))
     ON CONFLICT (owner, profile_id, kind, item) DO UPDATE SET count = GREATEST(0, character_items.count + $5)`,
    [owner, profileId, kind, item, delta]);
  await c.query(
    'INSERT INTO item_log (owner, profile_id, kind, item, delta, reason) VALUES ($1, $2, $3, $4, $5, $6)',
    [owner, profileId, kind, item, delta, reason]);
}

export async function countOf(c: Q, owner: string, profileId: string, kind: string, item: string): Promise<number> {
  const r = await c.query<{ count: number }>(
    'SELECT count FROM character_items WHERE owner = $1 AND profile_id = $2 AND kind = $3 AND item = $4',
    [owner, profileId, kind, item]);
  return r.rows[0]?.count ?? 0;
}

/** Every collectible the character owns - poses and hub themes, the one roll's pool. */
export async function posesOwned(c: Q, owner: string, profileId: string): Promise<Set<string>> {
  const r = await c.query<{ item: string }>(
    "SELECT item FROM character_items WHERE owner = $1 AND profile_id = $2 AND kind IN ('pose', 'theme') AND count > 0",
    [owner, profileId]);
  return new Set(r.rows.map((x) => x.item));
}

// ------------------------------------------------------------------------------------ recolour

export interface Change { piece: string; swatches: (string | null)[] }
export interface Price { charges: number; dyes: Record<string, number>; refusal?: string }

/**
 * ONE CHARGE per piece whose colours change, plus ONE DYE per channel set to a colour it was not
 * already wearing; putting a channel back as authored costs no dye. The last change to a piece is
 * what it ends as. The game's Chain.Recolour.Cost - keep them in step.
 */
export function recolourCost(current: Map<string, (string | null)[]>, changes: Change[]): Price {
  const price: Price = { charges: 0, dyes: {} };
  const seen = new Set<string>();
  for (let i = changes.length - 1; i >= 0; i--) {
    const c = changes[i];
    if (seen.has(c.piece)) continue;
    seen.add(c.piece);
    const channels = dyeChannels(c.piece);
    if (channels <= 0) return { ...price, refusal: `${c.piece} cannot be dyed` };
    const was = current.get(c.piece) ?? [];
    let changed = false;
    for (let ch = 0; ch < channels; ch++) {
      const want = c.swatches[ch] || null;
      if (want && !isSwatch(want)) return { ...price, refusal: `no dye '${want}'` };
      if (want === (was[ch] || null)) continue;
      changed = true;
      if (want) price.dyes[want] = (price.dyes[want] ?? 0) + 1;
    }
    if (changed) price.charges++;
  }
  return price;
}
