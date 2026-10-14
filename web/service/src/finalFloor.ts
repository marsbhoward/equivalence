import { randomInt } from 'node:crypto';
import { hasDatabase, q } from './db.js';

/**
 * FLOOR 100's BOX - the one drop the SERVICE decides (the user's calls, 2026-10-13;
 * MONETIZATION.md "Drops"). The game rolls every other drop itself; this one carries the weekly
 * guarantee, and a guarantee the client could claim would be claimed every clear.
 *
 *   the FIRST floor-100 clear of the week, per CHARACTER   a Diamond box (Black Diamond 1% instead)
 *   every later clear that week                            Diamond 2%, Black Diamond 1%,
 *                                                          otherwise 2-3 Gold boxes
 *
 * The week turns at SUNDAY 00:00 UTC. The game's copy of these numbers is Tuning.GearRoll (the
 * Final* constants) and Chain/FinalFloorReward.cs - keep the two in step.
 *
 * A decision is recorded per (character, run) BEFORE it is answered, so a retry gets the same
 * box and never a reroll; and commit-run mints a design box for floor 100 only when this table
 * says so (routes/player.ts).
 */
export const FINAL_FLOOR = 100;
export const GUARANTEED_BLACK_DIAMOND_CHANCE = 0.01;
export const LATER_DIAMOND_CHANCE = 0.02;
export const LATER_BLACK_DIAMOND_CHANCE = 0.01;
export const GOLD_BOXES_MIN = 2;
export const GOLD_BOXES_MAX = 3;

/**
 * The fewest seconds a floor can plausibly take - the plausibility check's default (agreed
 * 2026-10-13). A run must have started at least FINAL_FLOOR x this before it may claim floor 100,
 * well under the game's own 16-74 s floor targets, so an honest fast player is never refused.
 */
export const MIN_SECONDS_PER_FLOOR = Number(process.env.MIN_SECONDS_PER_FLOOR ?? 20);

export type FinalTier = 'Diamond' | 'BlackDiamond' | 'Gold';

export interface FinalDecision {
  tier: FinalTier;
  count: number;
  guaranteed: boolean;
  week: number;
}

const DAY_MS = 86_400_000;
/** 1970-01-04, the first Sunday after the epoch, is day 3. */
const FIRST_SUNDAY = 3;

/** The guarantee's week: 0 began Sunday 1970-01-04 00:00 UTC, and each turns at Sunday 00:00 UTC. */
export function weekOf(ms = Date.now()): number {
  return Math.floor((Math.floor(ms / DAY_MS) - FIRST_SUNDAY) / 7);
}

/** When `week` begins - the moment the guarantee before it resets. */
export function weekStarts(week: number): Date {
  return new Date((week * 7 + FIRST_SUNDAY) * DAY_MS);
}

/** A uniform draw in [0, 1) from the server's own randomness. */
const draw = () => randomInt(1_000_000) / 1_000_000;

/** Floor 100's box, given whether this character's guarantee is still unused this week. */
export function rollFinal(guaranteeUnused: boolean, week: number): FinalDecision {
  const r = draw();
  if (guaranteeUnused)
    return { tier: r < GUARANTEED_BLACK_DIAMOND_CHANCE ? 'BlackDiamond' : 'Diamond', count: 1, guaranteed: true, week };
  if (r < LATER_BLACK_DIAMOND_CHANCE) return { tier: 'BlackDiamond', count: 1, guaranteed: false, week };
  if (r < LATER_BLACK_DIAMOND_CHANCE + LATER_DIAMOND_CHANCE) return { tier: 'Diamond', count: 1, guaranteed: false, week };
  return { tier: 'Gold', count: GOLD_BOXES_MIN + randomInt(GOLD_BOXES_MAX - GOLD_BOXES_MIN + 1), guaranteed: false, week };
}

/** The decision recorded for one run's floor 100, or null - what commit-run may mint from it. */
export async function decisionFor(profileId: string, runId: string): Promise<FinalDecision | null> {
  if (!hasDatabase()) return null;
  const rows = await q<FinalDecision>(
    'SELECT tier, count, guaranteed, week FROM final_floor WHERE profile_id = $1 AND run_id = $2',
    [profileId, runId]);
  return rows[0] ?? null;
}
