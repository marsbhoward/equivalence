import { randomInt } from 'node:crypto';
import { hasDatabase, q } from './db.js';
import type { RunFormat } from './runFormat.js';

/**
 * THE FINAL FLOOR's BOX - floor 100 in Classic, 50 in the Short Descent (runFormat.ts) - decided by
 * the SERVICE (the user's calls, 2026-10-13; MONETIZATION.md "Drops"), as are the avatars' boxes
 * (avatarBoxes.ts). It carries the weekly guarantee, and a guarantee the client could claim would
 * be claimed every clear.
 *
 *   the FIRST final clear of the week, per CHARACTER   a Diamond box (Black Diamond 1% instead)
 *   every later clear that week                        Diamond / Black Diamond at the format's
 *                                                      rates, otherwise 2-3 Gold boxes
 *
 * The week turns at SUNDAY 00:00 UTC. The game's copy of these numbers is Tuning.GearRoll (the
 * Final* constants) and Chain/FinalFloorReward.cs - keep the two in step.
 *
 * A decision is recorded per (character, run) BEFORE it is answered, so a retry gets the same
 * box and never a reroll; and commit-run mints the final floor's design box only when this table
 * says so (routes/player.ts).
 */
// The numbers are per FORMAT (runFormat.ts): Classic's final floor is 100, the Short Descent's 50.

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

/** The final floor's box under a format, given whether the guarantee is still due this week. */
export function rollFinal(f: RunFormat, guaranteeDue: boolean, week: number): FinalDecision {
  const r = draw();
  if (guaranteeDue)
    return { tier: r < f.guaranteedBlackDiamondChance ? 'BlackDiamond' : 'Diamond', count: 1, guaranteed: true, week };
  if (r < f.laterBlackDiamondChance) return { tier: 'BlackDiamond', count: 1, guaranteed: false, week };
  if (r < f.laterBlackDiamondChance + f.laterDiamondChance) return { tier: 'Diamond', count: 1, guaranteed: false, week };
  return { tier: 'Gold', count: f.goldBoxesMin + randomInt(f.goldBoxesMax - f.goldBoxesMin + 1), guaranteed: false, week };
}

/** The decision recorded for one run's final floor, or null - what commit-run may mint from it. */
export async function decisionFor(owner: string, profileId: string, runId: string): Promise<FinalDecision | null> {
  if (!hasDatabase()) return null;
  const rows = await q<FinalDecision>(
    'SELECT tier, count, guaranteed, week FROM final_floor WHERE owner = $1 AND profile_id = $2 AND run_id = $3',
    [owner, profileId, runId]);
  return rows[0] ?? null;
}
