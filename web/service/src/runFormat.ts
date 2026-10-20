/**
 * RUN FORMATS - the rules a run's boxes are judged by, per format (SHORT-DESCENT.md).
 *
 *   classic   100 floors. Avatars at 25 / 50 / 75 roll their own boxes; floor 100 is the end and
 *             carries the weekly guarantee.
 *   short     THE SHORT DESCENT: 50 floors, built to be cleared in about an hour. The avatar at 25
 *             rolls its own boxes; floor 50 is the end and carries the weekly guarantee - only for
 *             a clear inside the hour (before the GATE COLLAPSE).
 *
 * A run declares its format in its run-start save (routes/saves.ts) and the service records it with
 * the run (`run_progress.format`); every check after that reads the RUN's format, so a run is judged
 * by the rules it started under. A save that names none is CLASSIC - every build before formats.
 *
 * `RUN_FORMATS` lists the formats the service ACCEPTS (comma separated, default `classic`): a run
 * starting under any other is refused. Production names only what players can play.
 *
 * The game's copies of these numbers are Tuning.GearRoll (avatar and final-floor chances),
 * Chain/FinalFloorReward and Core/RunFormat - keep them in step.
 */
export type FormatId = 'classic' | 'short';

export interface RunFormat {
  id: FormatId;
  /** The last floor - the run's end, and the floor whose box carries the weekly guarantee. */
  finalFloor: number;
  /** Avatar floors below the final one that roll their own Diamond / Black Diamond boxes. */
  rolledAvatars: number[];
  avatarDiamondChance: number;
  avatarBlackDiamondChance: number;
  /** The final floor's box: the week's guarantee is Black Diamond this often instead of Diamond. */
  guaranteedBlackDiamondChance: number;
  /** A later clear that week: Diamond / Black Diamond this often, otherwise Gold boxes. */
  laterDiamondChance: number;
  laterBlackDiamondChance: number;
  goldBoxesMin: number;
  goldBoxesMax: number;
  /**
   * The fewest seconds a floor can plausibly take, on the server's clock - the plausibility check:
   * a run must have started at least floor x this before it may be paid for a floor.
   */
  minSecondsPerFloor: number;
  /**
   * The weekly guarantee only for a final floor reached within this many seconds of RUN CLOCK
   * (runProgress.runClock - floor starts to clears, as the service saw them); 0 = no limit.
   */
  guaranteeClockSeconds: number;
  /**
   * Seconds of each floor attempt not counted on the run clock: a floor's start snapshot is
   * written as the floor begins to build, a few seconds before its first enemy spawns, and the
   * clock starts at the first spawn (SHORT-DESCENT.md). An estimate - to check in play.
   */
  walkInSeconds: number;
}

/** A test override for every format's plausibility floor (seconds a floor), e.g. 0.1 locally. */
const MIN_OVERRIDE = process.env.MIN_SECONDS_PER_FLOOR ? Number(process.env.MIN_SECONDS_PER_FLOOR) : null;

/**
 * THE SHORT DESCENT's guarantee: a final clear before the GATE COLLAPSE, 60 minutes of run clock,
 * plus 30 seconds for the network - each floor's start and clear reach the service a moment after
 * they happen.
 */
const SHORT_GUARANTEE_SECONDS = 60 * 60 + 30;

export const FORMATS: Record<FormatId, RunFormat> = {
  classic: {
    id: 'classic',
    finalFloor: 100,
    rolledAvatars: [25, 50, 75],
    avatarDiamondChance: 0.02,
    avatarBlackDiamondChance: 0.005,
    guaranteedBlackDiamondChance: 0.01,
    laterDiamondChance: 0.02,
    laterBlackDiamondChance: 0.01,
    goldBoxesMin: 2,
    goldBoxesMax: 3,
    // Well under the game's own 16-74 s floor targets: an honest fast player is never refused.
    minSecondsPerFloor: MIN_OVERRIDE ?? 20,
    guaranteeClockSeconds: 0,
    walkInSeconds: 3,
  },
  // Proposed numbers (SHORT-DESCENT.md, "Diamond and Black Diamond") - per HOUR, as Classic pays.
  short: {
    id: 'short',
    finalFloor: 50,
    rolledAvatars: [25],
    avatarDiamondChance: 0.01,
    avatarBlackDiamondChance: 0.0025,
    guaranteedBlackDiamondChance: 0.01,
    laterDiamondChance: 0.025,
    laterBlackDiamondChance: 0.0075,
    goldBoxesMin: 2,
    goldBoxesMax: 3,
    // Its floors' targets run ~15-35 s; deeper floors are priced like Classic's at twice the depth.
    minSecondsPerFloor: MIN_OVERRIDE ?? 15,
    guaranteeClockSeconds: SHORT_GUARANTEE_SECONDS,
    walkInSeconds: 3,
  },
};

/** The format a run-start names - an unknown or missing name is null; a missing one means classic. */
export function formatOf(name: unknown): RunFormat | null {
  if (name === undefined || name === null || name === '') return FORMATS.classic;
  return Object.prototype.hasOwnProperty.call(FORMATS, String(name)) ? FORMATS[String(name) as FormatId] : null;
}

/** A recorded run's format - rows from before formats are classic. */
export const recordedFormat = (id: string | null | undefined): RunFormat =>
  (id && Object.prototype.hasOwnProperty.call(FORMATS, id) ? FORMATS[id as FormatId] : FORMATS.classic);

/** The formats this service accepts new runs under (RUN_FORMATS, default classic). */
export function accepts(format: RunFormat): boolean {
  const list = (process.env.RUN_FORMATS ?? 'classic').split(',').map((s) => s.trim().toLowerCase()).filter(Boolean);
  return list.includes(format.id);
}

/** An avatar floor whose own boxes this format rolls (the final floor is its own decision). */
export const isRolledAvatar = (format: RunFormat, floor: number) =>
  Number.isInteger(floor) && format.rolledAvatars.includes(floor);
