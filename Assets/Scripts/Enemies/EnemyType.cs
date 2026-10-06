using System;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// What an ELITE of this kind does on top of its ordinary attack.
    ///
    /// The tier is orthogonal to the kind - any archetype can be elite - so the extra pattern has
    /// to be declared PER KIND rather than being one bonus the tier hands out. An elite Ranged and
    /// an elite Bomb are not "the same enemy, harder"; they are each their own kit turned up.
    ///
    /// Declared here as data on the type rather than branched on inside the controller, so adding
    /// a kind means adding one row and answering the question once. There is no way to make the
    /// BEHAVIOUR pure data short of a scripting layer - the controller still implements each - but
    /// the decision about which kind gets which lives in exactly one place.
    /// </summary>
    public enum ElitePattern
    {
        /// <summary>No extra pattern. A kind may legitimately have none.</summary>
        None,

        /// <summary>Chaser: the swing comes back a second time, immediately and harder.</summary>
        Riposte,

        /// <summary>Ranged: a spread instead of a single bolt.</summary>
        Volley,

        /// <summary>Bomb: the blast leaves delayed shards where it stood.</summary>
        Cluster,

        /// <summary>Turret: the beam overcharges for a burst.</summary>
        Overcharge,

        /// <summary>Dasher: it chains straight into a second rush instead of evading.</summary>
        Redouble,

        /// <summary>
        /// Gargoyle: rather than an occasional bonus on top of the ordinary attack, this REPLACES
        /// the basic's stationary gaze outright, every cycle - see EnemyController.UpdateGargoyle
        /// and Tuning.Enemy's own Gargoyle notes. It flies to wherever it last saw the player,
        /// lands, glows, and slams an AoE root with real damage, then repeats.
        ///
        /// THE ONE PATTERN HERE NOT GATED BY ElitePatternDue/EliteEvery. Every other kind's
        /// pattern is an occasional bonus on top of an otherwise-unchanged attack; this one IS the
        /// attack, every single cycle, because an elite Gargoyle is meant to be a different fight
        /// throughout rather than a stronger version of the same one every so often.
        /// </summary>
        Descent,

        /// <summary>
        /// Mortar: three shells from one wind-up, half a second apart, each aimed at where the
        /// player is at its own moment, each deflectable on its own. Every lob, not on a
        /// cadence - like Descent, it is what an elite mortar's attack IS.
        /// </summary>
        Barrage,
    }

    /// <summary>
    /// One enemy TYPE, as a record.
    ///
    /// WHY THIS EXISTS: EnemyFactory.Spawn was nine separate `kind switch` blocks - name, size,
    /// art, tint, health, speed, damage, range, telegraph - so every per-kind property added meant
    /// a tenth, and the full description of any one enemy was smeared across all of them. Nothing
    /// was wrong with the values; they were simply impossible to read as a whole. A row here is one
    /// enemy, top to bottom, and the factory reads it.
    ///
    /// The elite pattern is the field that prompted the refactor: it is genuinely per-kind, and
    /// adding it as a tenth switch would have been the point where the shape stopped being
    /// tolerable.
    ///
    /// PLAIN FIELDS AND STATIC READONLY DATA. Not a ScriptableObject: this project builds
    /// everything from code so the scene stays CLI-rebuildable, and an asset would be one more
    /// thing to keep in sync with a clean checkout. Tuning still owns every NUMBER - this owns
    /// which number goes where.
    /// </summary>
    public class EnemyDef
    {
        public EnemyKind Kind;
        public string Name;
        public float Size;
        public Color Tint;

        /// <summary>Resolved at SPAWN time, not here - GameArt.I is a runtime singleton and a
        /// static initialiser would capture it before it exists.</summary>
        public Func<Art.ActorArt> Art;

        public float BaseHp;
        public float MoveSpeed;
        public float MoveSpeedJitterMin, MoveSpeedJitterMax;

        /// <summary>Meaning varies by kind and is documented per row - a per-event amount for
        /// most, a per-second RATE for Turret, a per-combo-hit amount for Dasher.</summary>
        public float Damage;

        /// <summary>Likewise: reach for melee, cast range for Ranged, blast radius for Bomb.</summary>
        public float AttackRange;

        public float TelegraphDuration;

        /// <summary>Ranged only - how close it will let the player get before backing off.</summary>
        public float PreferredMinRange;

        public ElitePattern Elite;

        /// <summary>How often the elite pattern replaces the ordinary attack. 2 means every other
        /// one. Ignored when <see cref="Elite"/> is None, and also ignored by
        /// <see cref="ElitePattern.Descent"/>, which is unconditional every cycle - see its own
        /// doc.</summary>
        public int EliteEvery;

        /// <summary>
        /// This kind never carries armor, at any tier or floor depth - not even the generic
        /// per-floor growth bar every other kind accrues (see EnemyFactory's own armor block).
        /// Gargoyle is the first kind to need this: EnemyController.Flinch refuses a Medium
        /// interrupt while EnemyArmor.Current is above zero, and this family's one piece of
        /// counterplay is being able to flinch the glow away - an armor bar arriving silently at
        /// depth would take that away without anything on screen explaining why.
        /// </summary>
        public bool NeverArmored;
    }

    public static class EnemyTypes
    {
        static EnemyDef[] _byKind;

        public static EnemyDef Of(EnemyKind kind)
        {
            _byKind ??= Build();
            int i = (int)kind;
            return i >= 0 && i < _byKind.Length && _byKind[i] != null ? _byKind[i] : _byKind[(int)EnemyKind.Bomb];
        }

        /// <summary>
        /// Indexed BY THE ENUM'S OWN VALUE rather than in declaration order, so reordering
        /// EnemyKind cannot silently re-point a row at the wrong enemy - the same class of bug
        /// GearSlot's int serialisation already caused once for saved loadouts.
        /// </summary>
        static EnemyDef[] Build()
        {
            var all = new[]
            {
                new EnemyDef
                {
                    Kind = EnemyKind.Bomb,
                    Name = "Bomb",
                    Size = Tuning.Enemy.BombSize,
                    // Yellow, the Bomb's colour - body, lamp and blast ring. Only the placeholder
                    // circle wears this: BombArt bakes its own colours and the renderer is reset to
                    // white once the stage art is on (EnemyFactory).
                    Tint = new Color(1f, 0.86f, 0.2f),
                    // Resolved at spawn time like every Art lambda here, and reads EnemyLooks
                    // FRESH on every call rather than capturing it once - GameBootstrap.NextFloor
                    // rolls a new look before any of that floor's bombs spawn, so every bomb born
                    // on one floor agrees, and this lambda never has to know when that happens.
                    Art = () => EnemyLooks.Of(EnemyKind.Bomb) == EnemyLook.Construct
                        ? Convergence.Art.GameArt.I.BombConstruct
                        : Convergence.Art.GameArt.I.BombAutomaton,
                    BaseHp = Tuning.Enemy.BombHp,
                    MoveSpeed = Tuning.Enemy.BombMoveSpeed,
                    MoveSpeedJitterMin = Tuning.Enemy.BombMoveSpeedJitterMin,
                    MoveSpeedJitterMax = Tuning.Enemy.BombMoveSpeedJitterMax,
                    Damage = Tuning.Enemy.BombDamage,
                    // Doubles as the blast radius: one circle triggers the telegraph and is also
                    // what takes damage.
                    AttackRange = Tuning.Enemy.BombAttackRange,
                    TelegraphDuration = Tuning.Enemy.BombTelegraphDuration,
                    Elite = ElitePattern.Cluster,
                    EliteEvery = 1,     // it only ever detonates once, so every time or never
                },

                new EnemyDef
                {
                    Kind = EnemyKind.Chaser,
                    Name = "Chaser",
                    Size = Tuning.Enemy.ChaserSize,
                    Tint = new Color(0.95f, 0.35f, 0.55f),
                    // Same shape as Bomb's and Turret's own look lambdas - see EnemyLooks.
                    Art = () => EnemyLooks.Of(EnemyKind.Chaser) == EnemyLook.Construct
                        ? Convergence.Art.GameArt.I.ChaserConstruct
                        : Convergence.Art.GameArt.I.ChaserAutomaton,
                    BaseHp = Tuning.Enemy.ChaserHp,
                    MoveSpeed = Tuning.Enemy.ChaserMoveSpeed,
                    Damage = Tuning.Enemy.ChaserDamage,
                    AttackRange = Tuning.Enemy.ChaserAttackRange,
                    TelegraphDuration = Tuning.Enemy.ChaserTelegraphDuration,
                    Elite = ElitePattern.Riposte,
                    EliteEvery = 2,
                },

                new EnemyDef
                {
                    Kind = EnemyKind.Ranged,
                    Name = "Ranged",
                    Size = Tuning.Enemy.RangedSize,
                    Tint = new Color(0.55f, 0.45f, 0.95f),
                    // Same shape as Bomb's, Chaser's and Turret's own look lambdas - see EnemyLooks.
                    Art = () => EnemyLooks.Of(EnemyKind.Ranged) == EnemyLook.Construct
                        ? Convergence.Art.GameArt.I.RangedConstruct
                        : Convergence.Art.GameArt.I.RangedAutomaton,
                    BaseHp = Tuning.Enemy.RangedHp,
                    MoveSpeed = Tuning.Enemy.RangedMoveSpeed,
                    Damage = Tuning.Enemy.RangedDamage,
                    AttackRange = Tuning.Enemy.RangedPreferredMaxRange,
                    PreferredMinRange = Tuning.Enemy.RangedPreferredMinRange,
                    TelegraphDuration = Tuning.Enemy.RangedTelegraphDuration,
                    Elite = ElitePattern.Volley,
                    EliteEvery = 2,
                },

                new EnemyDef
                {
                    Kind = EnemyKind.Turret,
                    Name = "Turret",
                    Size = Tuning.Enemy.TurretSize,
                    Tint = new Color(0.3f, 0.85f, 0.95f),
                    // Same shape as Bomb's own look lambda above - see EnemyLooks.
                    Art = () => EnemyLooks.Of(EnemyKind.Turret) == EnemyLook.Construct
                        ? Convergence.Art.GameArt.I.TurretConstruct
                        : Convergence.Art.GameArt.I.TurretAutomaton,
                    BaseHp = Tuning.Enemy.TurretHp,
                    // Zero rather than omitted - FixedUpdate branches on Kind for this too, but
                    // MoveSpeed staying honest matters for anything else that reads it without
                    // knowing about that branch.
                    MoveSpeed = 0f,
                    // A RATE (damage per second), not a per-event amount - UpdateTurret multiplies
                    // it by the tick interval itself.
                    Damage = Tuning.Enemy.TurretDamagePerSecond,
                    // Effectively unlimited: the beam is stopped by line of sight, never distance.
                    AttackRange = Tuning.Enemy.TurretRange,
                    TelegraphDuration = Tuning.Enemy.TurretChargeDuration,
                    // The elite's SECOND move, the mire lob, is read straight off the Elite bool
                    // (EnemyController.TickMire) - one declared pattern per kind, and this one's
                    // is the beam's.
                    Elite = ElitePattern.Overcharge,
                    EliteEvery = 3,
                },

                new EnemyDef
                {
                    Kind = EnemyKind.Dasher,
                    Name = "Dasher",
                    Size = Tuning.Enemy.DasherSize,
                    // Acid yellow-green, and deliberately NOT the red its own telegraph glows, so
                    // "charging" reads as a state change rather than as a new enemy.
                    Tint = new Color(0.75f, 0.95f, 0.25f),
                    // Same shape as every other kind's own look lambda - see EnemyLooks.
                    Art = () => EnemyLooks.Of(EnemyKind.Dasher) == EnemyLook.Construct
                        ? Convergence.Art.GameArt.I.DasherConstruct
                        : Convergence.Art.GameArt.I.DasherAutomaton,
                    BaseHp = Tuning.Enemy.DasherHp,
                    // Its APPROACH speed only - the charge and the retreat read Tuning.Enemy.Dasher*
                    // directly, since neither has a shared field to ride on the way this one rides
                    // MoveSpeed.
                    MoveSpeed = Tuning.Enemy.DasherApproachSpeed,
                    // Per COMBO HIT, not per combo - applied once per swing, 3-5 times a connecting
                    // charge.
                    Damage = Tuning.Enemy.DasherComboHitDamage,
                    // Doubles as the combo's melee reach once the charge closes the distance.
                    AttackRange = Tuning.Enemy.DasherComboRange,
                    TelegraphDuration = Tuning.Enemy.DasherTelegraphDuration,
                    Elite = ElitePattern.Redouble,
                    EliteEvery = 2,
                },

                new EnemyDef
                {
                    Kind = EnemyKind.Gargoyle,
                    Name = "Gargoyle",
                    Size = Tuning.Enemy.GargoyleSize,
                    // Cold stone-violet - the one kind in the roster that never reads as a
                    // damage threat by colour alone, matching the family's own "affects the
                    // battle, does not hurt you" rule (the elite's slam excepted).
                    Tint = new Color(0.55f, 0.48f, 0.62f),
                    // Same shape as every other kind's own look lambda - see EnemyLooks.
                    Art = () => EnemyLooks.Of(EnemyKind.Gargoyle) == EnemyLook.Construct
                        ? Convergence.Art.GameArt.I.GargoyleConstruct
                        : Convergence.Art.GameArt.I.GargoyleAutomaton,
                    BaseHp = Tuning.Enemy.GargoyleHp,
                    // Never used for ordinary movement - a Gargoyle is perched, like a Turret,
                    // and only the elite ever leaves its spot, on GargoyleFlySpeed rather than
                    // this field. Zero rather than omitted for the same reason Turret's own row
                    // states: honest for anything reading it without knowing about that branch.
                    MoveSpeed = 0f,
                    // Read ONLY by the elite's landing slam - see Tuning.Enemy's own note. The
                    // basic's root never calls Health.Take, so this being non-zero does not make
                    // the basic damaging.
                    Damage = Tuning.Enemy.GargoyleDamage,
                    // The basic's root/gaze range; also what the elite checks before it commits
                    // to a flight. Effectively unlimited, same reasoning as Turret's own range -
                    // a watcher's reach is bounded by what it can SEE, not by distance.
                    AttackRange = Tuning.Enemy.GargoyleSightRange,
                    TelegraphDuration = Tuning.Enemy.GargoyleTelegraphDuration,
                    // Not armored, ever - see EnemyDef.NeverArmored. Fought through raw HP by
                    // design; an armor bar would also block a flinch (EnemyController.Flinch
                    // checks EnemyArmor.Current), and being able to flinch the glow away is this
                    // family's one piece of counterplay.
                    NeverArmored = true,
                    Elite = ElitePattern.Descent,
                    // Not read as a cadence at all for this kind - see ElitePattern.Descent's
                    // own doc. 1 is set here only so the row still declares a value the way
                    // every other kind's does.
                    EliteEvery = 1,
                },

                new EnemyDef
                {
                    Kind = EnemyKind.Booster,
                    Name = "Booster",
                    Size = Tuning.Enemy.BoosterSize,
                    // Pale green - the statue's own colour, and the exact tint a buffed ally
                    // wears too (see EnemyController.ApplyBoosterBuff), so the effect visibly
                    // traces back to its source.
                    Tint = new Color(0.55f, 0.85f, 0.55f),
                    Art = () => EnemyLooks.Of(EnemyKind.Booster) == EnemyLook.Construct
                        ? Convergence.Art.GameArt.I.BoosterConstruct
                        : Convergence.Art.GameArt.I.BoosterAutomaton,
                    BaseHp = Tuning.Enemy.BoosterHp,
                    // Stationary, like Gargoyle's own basic form - and this kind never leaves its
                    // spot at all, having no elite variant to ever make it move.
                    MoveSpeed = 0f,
                    // NEVER USED. This kind does not attack the player under any circumstance -
                    // not even the one exception Gargoyle's elite carries - so Damage stays 0
                    // rather than merely unread, the same honesty Turret's own MoveSpeed=0 states.
                    Damage = 0f,
                    // Reused for the CAST range instead of a melee/blast reach - see Tuning.Enemy's
                    // own BoosterCastRange note.
                    AttackRange = Tuning.Enemy.BoosterCastRange,
                    TelegraphDuration = Tuning.Enemy.BoosterTelegraphDuration,
                    NeverArmored = true,
                    // No elite variant of its own - see Tuning.Enemy's own Booster notes for why.
                    Elite = ElitePattern.None,
                    EliteEvery = 1,
                },

                new EnemyDef
                {
                    Kind = EnemyKind.Bubbles,
                    Name = "Bubbles",
                    Size = Tuning.Enemy.BubblesSize,
                    // Pale water-blue - the shield's own colour, so a player who has learned what
                    // the tint means reads the threat before the statue does anything at all.
                    Tint = new Color(0.55f, 0.75f, 0.9f),
                    Art = () => EnemyLooks.Of(EnemyKind.Bubbles) == EnemyLook.Construct
                        ? Convergence.Art.GameArt.I.BubblesConstruct
                        : Convergence.Art.GameArt.I.BubblesAutomaton,
                    BaseHp = Tuning.Enemy.BubblesHp,
                    // Stationary, like its two siblings - no elite variant to ever make it move.
                    MoveSpeed = 0f,
                    // NEVER USED - this kind never attacks the player under any circumstance, the
                    // same honesty Booster's own Damage = 0 states.
                    Damage = 0f,
                    // Reused for the CAST range, not a melee/blast reach - see Tuning.Enemy's own
                    // BubblesCastRange note.
                    AttackRange = Tuning.Enemy.BubblesCastRange,
                    TelegraphDuration = Tuning.Enemy.BubblesTelegraphDuration,
                    NeverArmored = true,
                    // ELITE = None here does NOT mean elite-less - unlike Booster's row, where it
                    // genuinely does. Bubbles' elite grants a more resilient bubble
                    // (BubblesEliteCharges), but a basic and an elite perform the IDENTICAL
                    // channel-then-bubble action, so there is no distinct extra move to declare as
                    // a pattern - EnemyController.ResolveBubbles reads the Elite bool directly, the
                    // same shape Gargoyle's own Elite check already uses for its own fork. See
                    // WaveComposer.EliteKinds - this kind IS in it.
                    Elite = ElitePattern.None,
                    EliteEvery = 1,
                },

                new EnemyDef
                {
                    Kind = EnemyKind.Mortar,
                    Name = "Mortar",
                    Size = Tuning.Enemy.MortarSize,
                    // Gunmetal with a brown cast - a cannon, not a creature. Deliberately NOT the
                    // red its shells blink, so the warning on the ground can never be mistaken for
                    // the body that sent it.
                    Tint = new Color(0.55f, 0.5f, 0.42f),
                    // Same shape as every other kind's own look lambda - see EnemyLooks. Both
                    // slots are blank today, so either look draws the placeholder circle.
                    Art = () => EnemyLooks.Of(EnemyKind.Mortar) == EnemyLook.Construct
                        ? Convergence.Art.GameArt.I.MortarConstruct
                        : Convergence.Art.GameArt.I.MortarAutomaton,
                    BaseHp = Tuning.Enemy.MortarHp,
                    MoveSpeed = Tuning.Enemy.MortarMoveSpeed,
                    // Per SHELL - the blast's damage, dealt once by MortarShell.
                    Damage = Tuning.Enemy.MortarDamage,
                    // Its lob reach, not the blast - the blast is Tuning.Enemy.MortarBlastRadius
                    // and belongs to the shell.
                    AttackRange = Tuning.Enemy.MortarRange,
                    // Ranged's field, reused: closer than this and it runs.
                    PreferredMinRange = Tuning.Enemy.MortarFleeRange,
                    TelegraphDuration = Tuning.Enemy.MortarTelegraphDuration,
                    // Every lob, not on a cadence - see ElitePattern.Barrage.
                    Elite = ElitePattern.Barrage,
                    EliteEvery = 1,
                },
            };

            int max = 0;
            foreach (var d in all) max = Mathf.Max(max, (int)d.Kind);
            var table = new EnemyDef[max + 1];
            foreach (var d in all) table[(int)d.Kind] = d;
            return table;
        }
    }
}
