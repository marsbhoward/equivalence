using System;
using System.Collections.Generic;
using Convergence.Core;

namespace Convergence.Chain
{
    /// <summary>Where in its floor a run was when its snapshot was taken.</summary>
    public enum RunStage
    {
        /// <summary>Walking into <see cref="RunSnapshot.Floor"/> - nothing of it built yet. A resume
        /// builds the floor from its start: the same room, the same wave, the same drops.</summary>
        Entering,

        /// <summary><see cref="RunSnapshot.Floor"/> is cleared: its door is open, and the deal and
        /// the reward may still be waiting (<see cref="RunSnapshot.PendingScreens"/>). A resume
        /// stands the player in the cleared room with them.</summary>
        Cleared,
    }

    /// <summary>The floor's Rift, as a cleared room is rebuilt with it.</summary>
    public enum RiftSave { None, Open, Sealed }

    /// <summary>
    /// A RUN, written down - enough to carry it on in another session (CONTINUATION.md). Taken at
    /// every floor's start and at its clear, never mid-fight: mid-fight the run is described by its
    /// last snapshot plus a <see cref="RunBeat"/> (health and armour), and a resume REPLAYS the
    /// floor from the snapshot at the lower of the two healths - a replay, never a heal.
    ///
    /// Plain [Serializable] fields only, for JsonUtility and for the domain reload. Never part of
    /// the character's datum: it lives in its own store (<see cref="IRunStore"/>), is never on
    /// chain, and is deleted the moment the run ends. Unlike a checkpoint it is written several
    /// times a floor.
    ///
    /// Whatever a REPLAY would roll again is seeded from the run (Rifts.RunRolls), so replaying a
    /// floor meets the same drops - closing the tab on a poor drop buys nothing.
    /// </summary>
    [Serializable]
    public class RunSnapshot
    {
        /// <summary>The shape of this record. A snapshot from a version the game does not know is
        /// not resumed (it is settled as the run's end instead).</summary>
        public int Version = CurrentVersion;
        public const int CurrentVersion = 1;

        public string RunId;
        public string ProfileId;

        /// <summary>Counts every snapshot of the run - the store refuses one older than it holds,
        /// and a <see cref="RunBeat"/> belongs to the snapshot of its own Seq.</summary>
        public int Seq;
        public string SavedAtUtc;

        public ElementType Element;
        public RunStage Stage;

        /// <summary>The floor being ENTERED (<see cref="RunStage.Entering"/>) or the one CLEARED.</summary>
        public int Floor;

        /// <summary>Cleared only: what is still to be shown - 2 the deal (when the floor has one)
        /// then the reward, 1 the reward, 0 nothing.</summary>
        public int PendingScreens;

        /// <summary>Curator rerolls already spent on the waiting reward - a resume shows the roll
        /// they ended on, not the first one again.</summary>
        public int RewardRerolls;

        /// <summary>A puzzle floor the player gave up on, or failed: a resume builds its adjacent
        /// room's fight rather than offering the puzzle again.</summary>
        public bool PuzzleGivenUp;

        /// <summary>The run clock STOPPED on this floor: its puzzle failed, and there is nothing to
        /// fight until the side door, whose snapshot clears it and starts the clock again. The
        /// service times the floor by it (runProgress.recordSnapshot). Never set by CaptureRun, so a
        /// resume's snapshot starts the clock.</summary>
        public bool ClockStopped;

        // ---- the floor plan, for a cleared room (Entering re-plans from the planner)

        public Rifts.FloorCategory PlanCategory;
        public Rifts.RiftKind PlanRift;
        public bool PlanForced;
        public RiftSave Rift;
        public int RiftSecured;
        public List<GroundGear> Ground = new();

        /// <summary>Rift Boxes lying uncollected in a cleared room, each with its own collapse clock.</summary>
        public List<GroundBox> GroundBoxes = new();

        /// <summary>
        /// Entering a BOSS floor left mid-fight: the eighths of the boss's health already taken
        /// (Tuning.Continuation.BossSteps), which the resumed boss starts without. Counted ROUNDED
        /// IN THE BOSS'S FAVOUR - a boss at 40% is three eighths down, back at 50% - so quitting a
        /// fight never takes more off it than fighting did (the user's call, 2026-10-14). 0 = full.
        /// </summary>
        public int BossEighthsDown;

        /// <summary>
        /// Entering a floor left with a CLOCK running - the Labyrinth's, or a Collapsing Rift's: the
        /// seconds of it already spent. A resume does not start the clock again (that would sell a
        /// fresh race for a quit) - it goes on from here, with Tuning.Continuation.ReorientSeconds
        /// to find one's feet. 0 = none spent.
        /// </summary>
        public float ClockSpent;

        /// <summary>The floor's Collapsing Rift had already run out - it stays run out.</summary>
        public bool RiftCollapsed;

        /// <summary>The Labyrinth: where the player stood (it has no other state), when Placed.</summary>
        public bool Placed;
        public float PosX, PosY;

        /// <summary>Floor 100's box was asked of the store and had not been credited.</summary>
        public bool FinalFloorUnsettled;

        /// <summary>An avatar's boxes were asked of the store and had not been credited: its floor,
        /// or 0.</summary>
        public int AvatarUnsettledFloor;

        // ---- the run

        /// <summary>The planner as the floor found it: before planning it (Entering), after (Cleared).</summary>
        public Rifts.FloorPlanner Planner;
        public float RunSeconds;

        /// <summary>The RUN CLOCK (Core.RunClock) - floors fought, start to clear - as it stood.</summary>
        public float RunClock;

        /// <summary>The run's FORMAT (Core.RunFormat.WireName) - a resume plays by its rules. Empty in
        /// snapshots from before formats: Classic.</summary>
        public string Format;
        public int PendingMastery;
        public int RunCalx;
        public int RunCalxTaken;
        public bool StakeGateCleared;
        public int GuaranteeWeek;

        /// <summary>The profile's boxes as the run began. A resume rebases the run's banked pools
        /// on the profile as it is NOW, keeping only what the run itself added or spent.</summary>
        public int RunStartRiftBoxes;
        public LootBoxes RunStartBoxes = new();

        public RunLootSave Loot = new();
        public LedgerSave Ledger = new();

        /// <summary>A captured spire's boon still carried (Hazards.SpireBoon as an int), or -1.</summary>
        public int FloorBoon = -1;
        public int FloorBoonFloorsLeft;

        // ---- the player

        public float Health;
        public List<DurabilityEntry> Wear = new();
        public int Kills;
        public List<string> Slots = new();
        public List<bool> SlotLocked = new();
        public bool ThirdSlotRemoved;
        public int RotationIndex;
        public int PerfectStreak;

        /// <summary>RunEffects' run-long counters (every-Nth entries): hits, arts, Vapour, releases.</summary>
        public int HitCount, Arts, VapourCount, WellspringCount;
        /// <summary>Kindling (grows): enemies that have died burning this run.</summary>
        public int BurningDeaths;
    }

    /// <summary>A Rift Box lying where it dropped, in a cleared room - with the seconds it had left.</summary>
    [Serializable]
    public class GroundBox
    {
        public float X, Y, Life;
    }

    /// <summary>A piece lying where it fell, in a cleared room.</summary>
    [Serializable]
    public class GroundGear
    {
        public MintedGearRecord Record;
        public float X, Y;
    }

    /// <summary>The run's loot (Rifts.RunLoot). Tiered pools are indexed by (int)LootTier.</summary>
    [Serializable]
    public class RunLootSave
    {
        public List<MintedGearRecord> Carried = new();
        public List<MintedGearRecord> Secured = new();
        public int FoundBoxes, BankedBoxes, SecuredRiftBoxes;
        public int[] FoundTiered = Array.Empty<int>();
        public int[] BankedTiered = Array.Empty<int>();
        public int[] SecuredTiered = Array.Empty<int>();
    }

    /// <summary>The run's ledger (Exchange.RunModifiers) - entries by id, its clocks and counters.</summary>
    [Serializable]
    public class LedgerSave
    {
        public List<string> Order = new();
        public List<int> Stacks = new();
        public Exchange.PendingOffer Pending = new();
        public int DealIndex, RefusalsUsed;
        public List<string> LastOfferedIds = new();
        public List<int> LastOfferedAt = new();
        public List<string> RestIds = new();
        public List<int> RestUntil = new();
        public List<string> NewCombos = new();
        public List<string> Opened = new();
        public List<string> Transmuted = new();
        public float Withered, Virid;
        public int Senescence;
        /// <summary>Forms and socket costs inscribed (Phase 2): id and socket (-1 off the rotation).</summary>
        public List<string> InscribedIds = new();
        public List<int> InscribedSockets = new();
        /// <summary>Forms taken back out of a stacked slot - never offered again this run.</summary>
        public List<string> Removed = new();
        /// <summary>Phase 4: floors each Nigredo has fermented, Albedos' potency, oaths' progress and
        /// the oaths fulfilled, and Tribute's floors toward a refusal.</summary>
        public List<string> FermentIds = new();
        public List<int> FermentFloors = new();
        public List<string> PotencyIds = new();
        public List<float> PotencyValues = new();
        public List<string> OathIds = new();
        public List<float> OathProgress = new();
        public List<string> Fulfilled = new();
        public int TributeFloors;
    }

    /// <summary>
    /// What changes in a fight between two snapshots and must not be undone by closing the tab:
    /// health, and the armour's wear. Sent every few seconds while either moves
    /// (Tuning.Continuation.BeatSeconds). Only meaningful against the snapshot of its own Seq.
    /// </summary>
    [Serializable]
    public class RunBeat
    {
        public string RunId;
        public int Seq;
        public float Health;
        public List<DurabilityEntry> Wear = new();

        /// <summary>The boss's health taken since the snapshot, in whole eighths rounded in its
        /// favour (<see cref="RunSnapshot.BossEighthsDown"/>). 0 off a boss floor.</summary>
        public int BossEighthsDown;

        /// <summary>A running clock's seconds spent (<see cref="RunSnapshot.ClockSpent"/>); the
        /// Labyrinth's position when Placed. A beat goes every second while a clock runs.</summary>
        public float ClockSpent;
        public bool Placed;
        public float PosX, PosY;

        /// <summary>The RUN CLOCK at this beat, the floor's open attempt included - where a resume
        /// picks it up, as the service counts an abandoned floor to its last beat.</summary>
        public float RunClock;
    }

    /// <summary>A run waiting to be carried on: its last snapshot and the beat since it, if any.
    /// [Serializable] so the hub's copy survives a domain reload.</summary>
    [Serializable]
    public class SuspendedRun
    {
        public RunSnapshot Snapshot;
        public RunBeat Beat;

        /// <summary>The health a resume starts at: never more than the snapshot had, nor more than
        /// the fight since it left.</summary>
        public float Health => Beat != null && Beat.Seq == Snapshot.Seq
            ? Math.Min(Snapshot.Health, Beat.Health) : Snapshot.Health;

        /// <summary>The run clock a resume carries on from: the last beat's, when it is this snapshot's.</summary>
        public float RunClock => Beat != null && Beat.Seq == Snapshot.Seq
            ? Math.Max(Snapshot.RunClock, Beat.RunClock) : Snapshot.RunClock;

        /// <summary>The boss a resume meets: as far down as the snapshot or the fight since it got
        /// it, in whole eighths - never dead (a dead boss cleared its floor, a Cleared snapshot).</summary>
        public int BossEighthsDown
        {
            get
            {
                int down = Snapshot.BossEighthsDown;
                if (Beat != null && Beat.Seq == Snapshot.Seq) down = Math.Max(down, Beat.BossEighthsDown);
                return Math.Clamp(down, 0, Core.Tuning.Continuation.BossSteps - 1);
            }
        }

        bool BeatIsCurrent => Beat != null && Beat.Seq == Snapshot.Seq;

        /// <summary>A running clock's seconds spent: whichever of snapshot and beat had spent more.</summary>
        public float ClockSpent => BeatIsCurrent ? Math.Max(Snapshot.ClockSpent, Beat.ClockSpent) : Snapshot.ClockSpent;

        /// <summary>The Labyrinth's position: the beat's when it has one, else the snapshot's.</summary>
        public bool Placed => (BeatIsCurrent && Beat.Placed) || Snapshot.Placed;
        public float PosX => BeatIsCurrent && Beat.Placed ? Beat.PosX : Snapshot.PosX;
        public float PosY => BeatIsCurrent && Beat.Placed ? Beat.PosY : Snapshot.PosY;

        /// <summary>The armour a resume starts with, by the same rule, piece by piece.</summary>
        public List<DurabilityEntry> Wear()
        {
            var list = new List<DurabilityEntry>();
            foreach (var e in Snapshot.Wear)
                if (e != null) list.Add(new DurabilityEntry { ItemId = e.ItemId, Current = e.Current });
            if (Beat == null || Beat.Seq != Snapshot.Seq || Beat.Wear == null) return list;
            foreach (var b in Beat.Wear)
            {
                if (b == null) continue;
                var at = list.Find(e => e.ItemId == b.ItemId);
                if (at == null) list.Add(new DurabilityEntry { ItemId = b.ItemId, Current = b.Current });
                else at.Current = Math.Min(at.Current, b.Current);
            }
            return list;
        }
    }

    /// <summary>The answer to "is a run waiting?" - <see cref="Read"/> false when the store could not
    /// be asked, which is NOT the same as no run (a stake must not be forfeited on a failed read).</summary>
    public struct RunLookup
    {
        public bool Read;
        public SuspendedRun Run;
    }
}
