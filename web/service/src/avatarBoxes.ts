import { randomInt } from 'node:crypto';
import { hasDatabase, q } from './db.js';
import type { RunFormat } from './runFormat.js';

/**
 * THE AVATARS' BOXES - decided by the SERVICE, like the final floor's (finalFloor.ts).
 *
 * An avatar boss below the final floor (Classic 25 / 50 / 75, the Short Descent 25 - runFormat.ts)
 * drops, on top of its floor's box, a Diamond box and a Black Diamond box at its format's chances,
 * rolled separately. The game used to roll these itself and claim the result at the run's end -
 * and a claim is all a modified client needs to report the cap every run. Now the service rolls
 * them at the avatar's clear, records the decision per (run, floor) BEFORE answering, and
 * commit-run mints only what it recorded (routes/player.ts).
 *
 * The game's copy of Classic's numbers is Tuning.GearRoll (AvatarDiamondBoxChance /
 * AvatarBlackDiamondBoxChance) and RunLoot.RollAvatarLocal, which a LOCAL save still rolls with -
 * keep them in step.
 */
export interface AvatarDecision {
  floor: number;
  diamond: number;
  black_diamond: number;
}

/** A uniform draw in [0, 1) from the server's own randomness. */
const draw = () => randomInt(1_000_000) / 1_000_000;

export function rollAvatar(f: RunFormat, floor: number): AvatarDecision {
  return {
    floor,
    diamond: draw() < f.avatarDiamondChance ? 1 : 0,
    black_diamond: draw() < f.avatarBlackDiamondChance ? 1 : 0,
  };
}

/** Every avatar decision recorded for one run - what commit-run may mint from. */
export async function avatarDecisions(owner: string, profileId: string, runId: string): Promise<AvatarDecision[]> {
  if (!hasDatabase()) return [];
  return q<AvatarDecision>(
    `SELECT floor, diamond, black_diamond FROM avatar_boxes
      WHERE owner = $1 AND profile_id = $2 AND run_id = $3 ORDER BY floor`,
    [owner, profileId, runId]);
}
