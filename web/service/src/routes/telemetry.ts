import type { Request, Response } from 'express';
import { internal } from '../errors.js';
import { hasDatabase, q } from '../db.js';

/**
 * TELEMETRY - what a public showcase cannot ask its players (SHOWCASE.md "Telemetry"). The game
 * batches small events (UI-less, Chain/Service/Telemetry.cs) and posts them once a floor and at
 * the run's end:
 *
 *   POST /telemetry       { run, who, build, format, events: [{ t: "floor", f: 3, ... }, ...] }   -> 204
 *   GET  /op/telemetry    ?days=7   the summary (operator only - scripts/telemetry.mjs prints it)
 *
 * ANONYMOUS: `run` is the run's own id, `who` a hash the game makes of the character id - never a
 * wallet address, a session or an IP (the rate limit reads the IP in memory and keeps nothing).
 * No session is needed, so anyone can post here: the limits (index.ts) and the shape checks below
 * keep junk small, and the numbers are read as approximate. Stored in the instance's own schema,
 * so it goes with the schema at the wipe.
 *
 * `TELEMETRY=off` answers 204 and stores nothing.
 */
const RUN = /^[A-Za-z0-9_.:-]{1,96}$/;
const WHO = /^[a-f0-9]{8,64}$/;
const WORD = /^[A-Za-z0-9_.:-]{1,40}$/;
const KIND = /^[a-z_]{1,24}$/;

/** A batch's ceilings - far past what one floor sends (a few events and a deal). */
export const TELEMETRY_LIMITS = { events: 60, keys: 24, string: 120, eventBytes: 1024 };

const off = () => (process.env.TELEMETRY ?? '').trim().toLowerCase() === 'off';

/** One event, kept to flat primitives: a string (trimmed), a finite number, a bool. Null if unusable. */
export function cleanEvent(e: unknown): Record<string, string | number | boolean> | null {
  if (!e || typeof e !== 'object' || Array.isArray(e)) return null;
  const src = e as Record<string, unknown>;
  if (typeof src.t !== 'string' || !KIND.test(src.t)) return null;
  const out: Record<string, string | number | boolean> = {};
  let n = 0;
  for (const [k, v] of Object.entries(src)) {
    if (n >= TELEMETRY_LIMITS.keys) break;
    if (!/^[a-z_][a-z0-9_]{0,23}$/.test(k)) continue;
    if (typeof v === 'string') out[k] = v.slice(0, TELEMETRY_LIMITS.string);
    else if (typeof v === 'number' && Number.isFinite(v)) out[k] = Math.round(v * 1000) / 1000;
    else if (typeof v === 'boolean') out[k] = v;
    else continue;
    n++;
  }
  return JSON.stringify(out).length <= TELEMETRY_LIMITS.eventBytes ? out : null;
}

export async function postTelemetry(req: Request, res: Response) {
  if (off() || !hasDatabase()) return res.sendStatus(204);
  const b = (req.body ?? {}) as Record<string, unknown>;
  const run = String(b.run ?? ''), who = String(b.who ?? '');
  const build = String(b.build ?? ''), format = String(b.format ?? '');
  if (!RUN.test(run) || !WHO.test(who) || (build && !WORD.test(build)) || (format && !WORD.test(format))
      || !Array.isArray(b.events) || b.events.length === 0 || b.events.length > TELEMETRY_LIMITS.events)
    return res.status(400).json({ error: 'bad_batch' });

  const events = b.events.map(cleanEvent).filter((e): e is NonNullable<typeof e> => e !== null);
  if (events.length === 0) return res.sendStatus(204);
  try {
    await q(
      `INSERT INTO telemetry (run, who, build, format, kind, data)
       SELECT $1, $2, $3, $4, e->>'t', e FROM jsonb_array_elements($5::jsonb) AS e`,
      [run, who, build, format, JSON.stringify(events)]);
    res.sendStatus(204);
  } catch (e) {
    internal(res, e, 'telemetry', 503, 'unavailable');
  }
}

/** The summary the operator reads - one query per question, over the last `days` (1..90). */
export async function telemetrySummary(req: Request, res: Response) {
  if (!hasDatabase()) return res.status(503).json({ error: 'no database' });
  const days = Math.min(90, Math.max(1, Number(req.query.days ?? 7) || 7));
  const since = `now() - make_interval(days => ${days})`;
  try {
    const [runs, ends, reach, killers, bosses, puzzles, rifts, deals, timing, devices, perf, errors, cards, hits] = await Promise.all([
      // Runs started, the characters behind them, how many of those were new.
      q(`SELECT count(DISTINCT run) AS runs, count(DISTINCT who) AS characters,
                count(DISTINCT who) FILTER (WHERE (data->>'new')::boolean) AS new_characters
           FROM telemetry WHERE kind = 'run_start' AND at > ${since}`),
      // How runs ended - a run with a start and no end closed the tab or lost the connection.
      q(`SELECT coalesce(e.data->>'how', 'no end (tab closed / disconnect / still playing)') AS how, count(*) AS runs
           FROM (SELECT DISTINCT run FROM telemetry WHERE kind = 'run_start' AND at > ${since}) s
           LEFT JOIN LATERAL (SELECT data FROM telemetry t WHERE t.run = s.run AND t.kind = 'run_end' LIMIT 1) e ON true
          GROUP BY 1 ORDER BY 2 DESC`),
      // Where runs stop: the deepest floor each run was seen on.
      q(`SELECT floor, count(*) AS runs FROM (
           SELECT run, max((data->>'f')::int) AS floor FROM telemetry
            WHERE at > ${since} AND data ? 'f' GROUP BY run) r
          GROUP BY floor ORDER BY floor`),
      // What kills: deaths by floor and by what dealt the last blow.
      q(`SELECT data->>'by' AS by, (data->>'f')::int AS floor, count(*) AS deaths FROM telemetry
          WHERE kind = 'run_end' AND data->>'how' = 'death' AND at > ${since}
          GROUP BY 1, 2 ORDER BY 3 DESC LIMIT 40`),
      q(`SELECT data->>'boss' AS boss, data->>'outcome' AS outcome, count(*) AS fights,
                round(avg((data->>'s')::numeric), 1) AS avg_seconds
           FROM telemetry WHERE kind = 'boss' AND at > ${since} GROUP BY 1, 2 ORDER BY 1, 2`),
      q(`SELECT data->>'kind' AS puzzle, data->>'solved' AS solved, count(*) AS n,
                round(avg((data->>'s')::numeric), 1) AS avg_seconds
           FROM telemetry WHERE kind = 'puzzle' AND at > ${since} GROUP BY 1, 2 ORDER BY 1, 2`),
      q(`SELECT data->>'rift' AS rift, data->>'choice' AS choice, count(*) AS n
           FROM telemetry WHERE kind = 'rift' AND at > ${since} GROUP BY 1, 2 ORDER BY 1, 2`),
      // Deals: what is taken (boon + cost) and how often a deal is refused.
      q(`SELECT coalesce(data->>'boon', '(refused)') AS boon, data->>'cost' AS cost, count(*) AS taken
           FROM telemetry WHERE kind = 'deal' AND at > ${since} GROUP BY 1, 2 ORDER BY 3 DESC LIMIT 40`),
      q(`SELECT sum((data->>'perfect')::int) AS perfect, sum((data->>'good')::int) AS good,
                sum((data->>'missed')::int) AS missed
           FROM telemetry WHERE kind = 'run_end' AND at > ${since}`),
      q(`SELECT data->>'dev' AS device, count(*) AS runs FROM telemetry
          WHERE kind = 'run_start' AND at > ${since} GROUP BY 1 ORDER BY 2 DESC`),
      // Frame times by device: the share of frames past 33 ms (under 30 fps) and the long stalls.
      q(`SELECT s.data->>'dev' AS device,
                sum((f.data->>'fr')::int) AS frames,
                round(100.0 * sum((f.data->>'f33')::int) / nullif(sum((f.data->>'fr')::int), 0), 2) AS pct_over_33ms,
                sum((f.data->>'f100')::int) AS stalls_over_100ms
           FROM telemetry f JOIN telemetry s ON s.run = f.run AND s.kind = 'run_start'
          WHERE f.kind IN ('floor', 'frames') AND f.at > ${since} AND f.data ? 'fr'
          GROUP BY 1 ORDER BY 2 DESC`),
      q(`SELECT data->>'msg' AS message, count(*) AS n, count(DISTINCT run) AS runs FROM telemetry
          WHERE kind = 'error' AND at > ${since} GROUP BY 1 ORDER BY 2 DESC LIMIT 20`),
      q(`SELECT data->>'card' AS card, count(*) AS clicks FROM telemetry
          WHERE kind = 'card' AND at > ${since} GROUP BY 1 ORDER BY 2 DESC`),
      // Hits taken per floor (BALANCE.md "The next pass"): blows, and Chaser-sized blows - the
      // Assay's unit (Assay.ShowcaseClimb) - with health left at the clear. Floors from 2026-11-03 on.
      q(`SELECT (data->>'f')::int AS floor, count(*) AS clears,
                round(avg((data->>'hits')::numeric), 1) AS avg_hits,
                max((data->>'hits')::int) AS most_hits,
                round(avg((data->>'blows')::numeric), 2) AS avg_chaser_blows,
                round(avg((data->>'dmg')::numeric), 0) AS avg_damage,
                round(100 * avg((data->>'hp')::numeric), 0) AS avg_hp_pct
           FROM telemetry WHERE kind = 'floor' AND at > ${since} AND data ? 'dmg'
          GROUP BY 1 ORDER BY 1`),
    ]);
    res.json({ days, runs: runs[0], ends, reach, killers, bosses, puzzles, rifts, deals, timing: timing[0], devices, perf, errors, cards, hits });
  } catch (e) {
    res.status(500).json({ error: e instanceof Error ? e.message : String(e) });
  }
}
