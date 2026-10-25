import type { Request, Response } from 'express';
import { isMainnet } from '../blockfrost.js';
import { internal } from '../errors.js';
import { tx } from '../db.js';
import { AvatarDecision, rollAvatar } from '../avatarBoxes.js';
import { currentRunAt, runFormatOf } from '../runProgress.js';
import { isRolledAvatar } from '../runFormat.js';
import { AVATAR_RARE_DYE_CHANCE, chance, credit, posesOwned, rollPose, rollRareDye } from '../cosmetics.js';

/**
 * An avatar's boxes (avatarBoxes.ts has the rules), for every browser player - guest or wallet,
 * the owner from the identity like every save:
 *
 *   POST /saves/avatar-box   { profileId, runId, floor }  -> { floor, diamond, blackDiamond, repeat }
 *
 * Asked by the game at an avatar's clear (GameBootstrap.ClaimAvatarBoxes). Only the character's
 * CURRENT run, once its snapshots show it on that floor and the server's clock allows it
 * (runProgress.currentRunAt). One decision per run per floor, recorded before it is answered: a
 * repeat answers the stored one, so asking again - a retry, a resume, the run's end - never rerolls.
 */
const ID = /^[A-Za-z0-9_.:-]{1,96}$/;

function fail(res: Response, status: number, error: string, extra: Record<string, unknown> = {}) {
  res.status(status).json({ error, ...extra });
}

const answer = (d: AvatarDecision, repeat: boolean) =>
  ({ floor: d.floor, diamond: d.diamond, blackDiamond: d.black_diamond,
     dye: d.dye ?? '', pose: d.pose ?? '', poseGold: d.pose_gold ?? 0, repeat });

export async function claimAvatarBoxes(req: Request, res: Response): Promise<void> {
  const owner = res.locals.owner as string;
  const profileId = String(req.body?.profileId ?? '');
  const runId = String(req.body?.runId ?? '');
  const floor = Number(req.body?.floor);
  if (!ID.test(profileId) || !ID.test(runId)) return fail(res, 400, 'profileId and runId are required');
  if (!Number.isInteger(floor) || floor <= 0) return fail(res, 400, 'floor must be a floor number');

  try {
    const out = await tx(async (c) => {
      // The character's row is the lock: two claims for one character are decided one at a time.
      const ch = await c.query('SELECT 1 FROM characters WHERE owner = $1 AND profile_id = $2 FOR UPDATE',
        [owner, profileId]);
      if (!ch.rowCount) return { status: 404, error: 'not_found' } as const;
      // The run's format names its avatars - Classic's 25 / 50 / 75, the Short Descent's 25.
      const format = await runFormatOf(c, owner, profileId, runId);
      if (!format) return { status: 409, error: 'not_current_run' } as const;
      if (!isRolledAvatar(format, floor))
        return { status: 400, error: 'not_a_rolled_avatar', extra: { format: format.id, avatars: format.rolledAvatars } } as const;

      const prior = await c.query<AvatarDecision>(
        `SELECT floor, diamond, black_diamond, dye, pose, pose_gold FROM avatar_boxes
          WHERE owner = $1 AND profile_id = $2 AND run_id = $3 AND floor = $4`,
        [owner, profileId, runId, floor]);
      if (prior.rowCount) return { status: 200, decision: prior.rows[0], repeat: true } as const;

      const run = await currentRunAt(c, owner, profileId, runId, floor);
      if (!run.ok) return { status: run.status, error: run.error, extra: run.extra } as const;

      const decision = rollAvatar(run.format, floor);
      // The cosmetics (cosmetics.ts), decided with the boxes and CREDITED here: database first, so
      // a rare dye or a pose is the character's from this moment, whatever becomes of the run.
      decision.dye = chance(AVATAR_RARE_DYE_CHANCE) ? rollRareDye() : null;
      const pose = rollPose(await posesOwned(c, owner, profileId));
      decision.pose = pose.pose;
      decision.pose_gold = pose.gold;
      await c.query(
        `INSERT INTO avatar_boxes (owner, profile_id, run_id, floor, diamond, black_diamond, dye, pose, pose_gold)
         VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)`,
        [owner, profileId, runId, floor, decision.diamond, decision.black_diamond, decision.dye, decision.pose, decision.pose_gold]);
      if (decision.dye) await credit(c, owner, profileId, 'dye', decision.dye, 1, `avatar:${runId}:${floor}`);
      if (decision.pose) await credit(c, owner, profileId, 'pose', decision.pose, 1, `avatar:${runId}:${floor}`);
      return { status: 200, decision, repeat: false } as const;
    });

    if (out.status !== 200) return fail(res, out.status, out.error, out.extra ?? {});
    if (!out.repeat && (out.decision.diamond || out.decision.black_diamond || out.decision.dye || out.decision.pose))
      console.log(`[avatar-box] ${profileId} run ${runId} floor ${floor}: ` +
        `${out.decision.diamond} Diamond, ${out.decision.black_diamond} Black Diamond` +
        (out.decision.dye ? `, dye ${out.decision.dye}` : '') + (out.decision.pose ? `, pose ${out.decision.pose}` : ''));
    res.json(answer(out.decision, out.repeat));
  } catch (e) {
    internal(res, e, 'avatar-box');
  }
}

/**
 * TESTNET ONLY (404 on mainnet), OPERATOR (`/op`, WRITE_TOKEN): records an avatar decision
 * directly - a lucky roll on demand, so scripts/smoke-player.mjs has boxes to mint and open
 * without waiting on a 2% roll. Overwrites any decision already recorded for that floor.
 *
 *   POST /op/dev/avatar-box { owner, profileId, runId, floor, diamond, blackDiamond }
 */
export async function devDecideAvatarBoxes(req: Request, res: Response): Promise<void> {
  if (isMainnet()) return void res.status(404).end();
  const { owner, profileId, runId } = (req.body ?? {}) as Record<string, string>;
  const floor = Number(req.body?.floor);
  const diamond = Math.max(0, Math.floor(Number(req.body?.diamond ?? 0)));
  const black = Math.max(0, Math.floor(Number(req.body?.blackDiamond ?? 0)));
  if (!owner || !ID.test(profileId ?? '') || !ID.test(runId ?? '')) return fail(res, 400, 'owner, profileId and runId are required');
  try {
    const refused = await tx(async (c) => {
      const format = await runFormatOf(c, owner, profileId, runId);
      if (!format) return 'unknown run';
      if (!isRolledAvatar(format, floor)) return `not one of ${format.id}'s rolled avatars (${format.rolledAvatars.join(', ')})`;
      await c.query(
        `INSERT INTO avatar_boxes (owner, profile_id, run_id, floor, diamond, black_diamond)
         VALUES ($1, $2, $3, $4, $5, $6)
         ON CONFLICT (owner, profile_id, run_id, floor) DO UPDATE SET diamond = $5, black_diamond = $6, decided_at = now()`,
        [owner, profileId, runId, floor, diamond, black]);
      return null;
    });
    if (refused) return fail(res, 400, refused);
    res.json({ floor, diamond, blackDiamond: black });
  } catch (e) {
    internal(res, e, 'avatar-box');
  }
}
