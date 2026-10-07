using System;
using System.Collections.Generic;

namespace Convergence.Combat
{
    /// <summary>
    /// How a finisher is WEIGHTED - what it costs in commitment and what it buys in control.
    /// Damage and lock length are the visible half; the half that matters in a fight is which
    /// tiers can interrupt an enemy and which can move one.
    ///
    ///     Light    3.0 basics   1.0x lock   no flinch, no knockback
    ///     Medium   5.0 basics   1.5x lock   flinches the unarmoured only
    ///     Heavy    6.5 basics   2.5x lock   flinches THROUGH armour, and knocks back
    ///
    /// Medium and Heavy deliberately do NOT deal the same damage. Under parity Heavy was only
    /// ever worth taking when you specifically wanted the control, and Medium strictly dominated
    /// it otherwise - same damage, faster. See Tuning.Finisher for the numbers themselves and
    /// CLAUDE.md for why the locks were picked to shape DPS rather than equalise it.
    /// </summary>
    public enum FinisherWeight { Light, Medium, Heavy }

    /// <summary>One swing in a chain.</summary>
    [Serializable]
    public class AttackStep
    {
        public string Name = "Swing";

        /// <summary>A field-for-field copy - for a move that resolves VARIANTS of its own step
        /// (Separatio's three figures each swing it with their own motion).</summary>
        public AttackStep Clone() => (AttackStep)MemberwiseClone();

        /// <summary>
        /// Weight class. Read for two things beyond flavour: only <see cref="FinisherWeight.Heavy"/>
        /// sets <see cref="DamageInfo.Displaces"/> (so only Heavy can move an enemy - everything
        /// else joins basics in leaving them planted), and the tier decides whether a hit flinches
        /// an armoured target, an unarmoured one, or neither.
        ///
        /// Ignored on basic steps, which never flinch and never displace whatever this says.
        /// </summary>
        public FinisherWeight Weight = FinisherWeight.Medium;

        /// <summary>Multiplies the player's base damage.</summary>
        public float DamageMultiplier = 1f;

        /// <summary>Multiplies the base attack interval. Above 1 = slower.</summary>
        public float IntervalMultiplier = 1f;

        public float RangeBonus;

        /// <summary>Swing arc. Lower = wider; 1 = dead ahead only.</summary>
        public float ArcDot = 0.25f;

        /// <summary>
        /// Outward shove, in impulse units.
        ///
        /// INERT against enemies unless the step also sets <see cref="PullsIn"/>. Enemies are
        /// Health.Immovable - they plant instead of sliding - so on every other move this number
        /// currently does nothing. Left in place rather than deleted because it is still the
        /// magnitude the PULL uses, and because turning shove back on is one flag
        /// (Tuning.Enemy.TakesKnockback) rather than re-tuning fourteen movesets.
        /// </summary>
        /// <summary>
        /// Disc finishers only. How many discs this throws, and how many extra ricochets each
        /// one gets beyond the class baseline. Zero throws means the step is resolved as a normal
        /// melee arc, which is what every greatsword move does.
        /// </summary>
        public int DiscThrows;
        public int DiscRicochetBonus;

        /// <summary>
        /// Fan the throws across this many degrees, centred on the facing. Zero sends them all
        /// straight ahead to find their own targets; 360 is a full ring.
        /// </summary>
        public float DiscSpreadDegrees;

        /// <summary>
        /// Damage at the far edge of the throw as a fraction of damage at the character. 1 is
        /// flat. Below 1 makes a throw hit hardest in your face, which is what turns a cone into
        /// a shotgun rather than a spray.
        /// </summary>
        public float DiscFarFraction = 1f;

        /// <summary>
        /// Every enemy a thrown disc touches is MARKED: the next hit on that body lands
        /// amplified by this much. 1 (or below) is off.
        /// </summary>
        public float DiscMarkMultiplier = 1f;

        /// <summary>Seconds a mark survives unspent.</summary>
        public float DiscMarkSeconds = 6f;

        /// <summary>
        /// The thrown discs STOP and hang in the air instead of returning, detonating when the
        /// player's next finisher executes. <see cref="DiscThrows"/> sets how many.
        /// </summary>
        public bool DiscSuspends;

        /// <summary>How far out the suspended discs plant themselves.</summary>
        public float DiscSuspendDistance = 2.4f;

        /// <summary>
        /// Non-zero drives the discs in a ring around the player for this many sweeps instead of
        /// throwing them outward. Each enemy is struck once per sweep.
        /// </summary>
        public int DiscOrbitSweeps;

        public float DiscOrbitRadius = 2.2f;

        /// <summary>How far the character hops backward as the move fires. Zero for none.</summary>
        public float RecoilDistance;

        public float Knockback = 3.2f;

        /// <summary>
        /// Invert the knockback so the hit drags enemies toward the player instead of away, and
        /// gather the WHOLE visible arena rather than just what the arc reaches - the pull-in
        /// finisher is the fight's reset button, not another way to hit a crowd.
        ///
        /// The impulse is full strength within about twice the reach ring (see
        /// <c>PlayerController.UndertowFullPullRangeMul</c>) and eases off with distance past it,
        /// down to <c>UndertowEdgePullFraction</c> at the edge of the screen. Everything still
        /// comes; the far crowd just arrives loose instead of being flung through the player.
        /// </summary>
        public bool PullsIn;

        /// <summary>Finishers hit everything in the arc; basics stay single-ish target.</summary>
        public bool CleavesAll;

        /// <summary>The shape the rig animates. Damage numbers are not what sells a move.</summary>
        public AttackMotion Motion = AttackMotion.Chop;

        /// <summary>
        /// Play this step as the motion's ALT variant rather than its canonical one.
        ///
        /// What "alt" means belongs to the motion: Chop stages the same downward stroke from the
        /// other shoulder, while Sweep really does run its arc the other way. It was called
        /// ReverseArc, which stopped being true once Chop's alt became a mirrored stance instead
        /// of a rewound one.
        ///
        /// Distinct from the chain's alternation, which flips the second basic so a chain reads
        /// as back-and-forth. The two compose: an alt-swing still alternates, just either side of
        /// whatever is set here.
        /// </summary>
        public bool AltVariant;

        /// <summary>
        /// World units the figure hops off the ground as this step winds up, 0 for none.
        ///
        /// Purely cosmetic - unlike <see cref="LeapSeconds"/> there is no invulnerability, no
        /// airborne state and no change to where the hit lands. The rise fills the wind-up and
        /// the drop lands with the strike, so the body's weight is falling into the blow rather
        /// than the arms swinging on their own. Reserved for the heavy committed steps; on a
        /// quick basic it would just look like the character is bouncing.
        /// </summary>
        public float HopHeight;

        /// <summary>
        /// Sends the weapon out as a <see cref="ThrownBlade"/> instead of resolving an arc here.
        /// The step's damage and knockback travel with it and apply per body it passes through.
        /// </summary>
        public bool ThrowsWeapon;

        /// <summary>
        /// How far a thrown weapon travels before the throw is spent - its point of no return.
        /// It no longer loops home on its own past this; a recall is what brings it back for a
        /// second pass, and left alone the blade returns to the hand cold. See
        /// <see cref="ThrownBlade"/>, whose <c>OutSpeed</c> is paired with this so the outbound
        /// trip - the recall window - is one second.
        /// </summary>
        public float ThrowDistance = 7.5f;

        /// <summary>
        /// Purely cosmetic: the weapon leaves the hand and a <see cref="Combat.WhirlingBlade"/>
        /// carries it in a circle around the player instead of the rig swinging it. The step still
        /// resolves its hit exactly as any other <see cref="AttackMotion.Spin"/> AoE would -
        /// nothing about GatherTargets, damage or knockback changes, only what the swing looks
        /// like. Distinct from <see cref="ThrowsWeapon"/>, which sends the weapon flying out in a
        /// line and lets the player recall or blink to it; this one never leaves the player's own
        /// reach and the hit is already resolved before it finishes playing.
        /// </summary>
        public bool OrbitsWeapon;

        /// <summary>
        /// Seconds spent winding up before this step resolves. The player keeps moving and keeps
        /// auto-aiming throughout; the cost is the tempo given up, not immobility. The strike at
        /// the end commits like any other finisher.
        /// </summary>
        public float ChargeSeconds;

        /// <summary>
        /// Seconds spent airborne before this step resolves. The character leaves the screen and
        /// takes no damage at all for the duration, while the player keeps full movement - so the
        /// landing zone is steered, not merely waited out.
        ///
        /// Distinct from <see cref="ChargeSeconds"/> because the two are opposites: a charge is a
        /// vulnerable, readable wind-up, whereas this is a window of total safety. Sharing one
        /// field would have made every existing charged move invulnerable.
        /// </summary>
        public float LeapSeconds;

        /// <summary>
        /// Damage at the rim of the arc relative to its centre. 1 is flat - what every ordinary
        /// swing wants, since a sword does not care where in the arc it connects.
        ///
        /// Below 1 turns the arc into a blast: full damage under the player, tapering outward.
        /// Deliberately not 0 at the rim - an edge that does literally nothing makes the ring
        /// drawn on the ground a lie, because a player standing just inside it takes no hit.
        /// </summary>
        public float EdgeDamageFraction = 1f;

        /// <summary>
        /// Seconds after this step lands during which the player takes
        /// <see cref="ExposedMultiplier"/> times damage.
        ///
        /// This is the price of the invulnerable leap. Without it the move would be strictly
        /// better than standing still: two seconds of immunity, a screen-wide blast, and no
        /// downside. The recovery is what turns it into a decision about WHERE to land.
        /// </summary>
        public float ExposedSeconds;

        public float ExposedMultiplier = 2f;

        /// <summary>
        /// Resolve as a blast centred on the player rather than as a swing.
        ///
        /// An ordinary strike is a swept capsule laid along the facing: it reaches where the blade
        /// reaches and nothing behind you is touched. That is wrong for the handful of moves that
        /// genuinely fill the space around the character - a full-circle spin, a ground slam, a
        /// body dropped out of the sky - so those say so here and get the circle instead.
        ///
        /// The two also fall off differently, and only one applies to each. An area effect tapers
        /// with DISTANCE (<see cref="EdgeDamageFraction"/>), because that is what a blast does. A
        /// strike tapers with the NUMBER of bodies already hit (<see cref="ChainFalloff"/>),
        /// because that is what a blade losing energy through a crowd does. Running both would
        /// punish the same hit twice.
        /// </summary>
        public bool AreaOfEffect;

        /// <summary>
        /// Tria Prima's Separatio: hold the blade up, then come apart into three figures - one per
        /// principle, each holding one of the three swords the fused blade is made of - that each
        /// strike a different way for a third of this step's damage, and fold back into one.
        /// Handled by <c>PlayerController.SeparatioStrike</c>, the way <see cref="SheathDraw"/> is.
        ///
        /// The step describes the WHOLE move - its declared damage is the total, split three ways
        /// by Tuning.Separatio.FigureDamageFraction - so the Medium it is tagged as is exactly what
        /// it deals. <see cref="ChargeSeconds"/> is deliberately left at 0: the hold is
        /// Tuning.Separatio.HoldSeconds, and a charge on the step would also send every figure's
        /// borrowed hit through ResolveEcho's unpaid-wind-up discount.
        /// </summary>
        public bool SplitsThreeWays;

        /// <summary>
        /// The Armillary's Quintessence: the two halves join overhead into the whole armillary,
        /// which holds while four REVERSE CONES strike round the character - one per side,
        /// widest AT the character and narrowing to a point outward - each for a quarter of this
        /// step's damage (Tuning.Quintessence.ConeDamageFraction), then separates. Handled by
        /// <c>PlayerController.QuintessenceStrike</c>; each cone resolves through ResolveEcho, and
        /// <c>GatherTargets</c> reads this flag for the cone's shape.
        /// </summary>
        public bool ReverseCones;

        /// <summary>
        /// The MAGNUM OPUS, the reactive weapons' weapon art: gather for <see cref="ChargeSeconds"/>
        /// while the light drains out of the armour and the weapon, swells in the relic and
        /// flashes back through the armour into the blade (Art.Gear.MagnumOpusGlow), then a big
        /// slash that fires a crescent of the weapon's own light down the facing
        /// (Combat.CrescentBeam) and drains the blade. Handled by
        /// <c>PlayerController.MagnumOpusStrike</c>. The gather IS a charge - ChargeSeconds, so the
        /// timing bar and the lead-in checks read it as one - it only looks different.
        /// </summary>
        public bool MagnumOpus;

        /// <summary>
        /// This step does NOT freeze movement or facing, even though it sits in a finisher slot.
        ///
        /// Exactly one move has it - Shadow's Echo - and it is half of a matched pair with that
        /// move's damage being about 10% under its weight class. Removing one without adjusting
        /// the other makes Shadow either the best or the worst thing in its tier. See
        /// Tuning.Shadow.
        ///
        /// It is worth more to some builds than others, which is intended: a finisher lock already
        /// costs Air its own momentum meter (see PlayerController.MoveIntent), so a finisher that
        /// never locks is worth measurably more to an Air build than to anyone else.
        /// </summary>
        public bool NeverLocks;

        /// <summary>
        /// This step's reach grows with <see cref="WeaponHeat"/>, and at the top of the cycle it
        /// leaves a burn.
        ///
        /// A flag rather than the Moveset reaching for the heat itself: a moveset is a description
        /// of a swing and knows nothing about what is holding it. The controller owns the lookup,
        /// the same way it owns Mods and the elemental resource.
        /// </summary>
        public bool ScalesWithHeat;

        /// <summary>
        /// Run the katana signature's sequence instead of resolving an arc here: quick-sheathe,
        /// two crescent cuts across the target, then an unsheathing draw-cut. Handled by a
        /// coroutine (<c>PlayerController.SheathDrawStrike</c>) the same way <see cref="ThrowsWeapon"/>
        /// and <see cref="LeapSeconds"/> hand off. This flag also rides the killing blow's
        /// <c>DamageInfo.Bisects</c>, so a non-boss it finishes is cut in two.
        ///
        /// The two pre-cuts are ordinary <c>ResolveArc</c> calls on a small step the coroutine
        /// builds; only the draw-cut step carries this flag, so only the draw-cut bisects.
        /// </summary>
        public bool SheathDraw;

        /// <summary>
        /// This step's hits must not trigger <see cref="Combat.Hitstop"/>. Set on the katana
        /// sequence's two pre-cuts so the sequence produces one freeze - off the draw-cut, which
        /// carries the sequence's own weight - rather than three separate stutters roughly a
        /// swing apart.
        /// </summary>
        public bool SuppressHitstop;

        /// <summary>
        /// How wide the swept strike is, in world units. Ignored by area effects.
        ///
        /// This replaces the old arc test, which combined a circle around the player with a
        /// <see cref="ArcDot"/> cone. At the default 0.25 that cone was 150 degrees wide - so a
        /// "swing" landed on anything within reach that was not directly behind you, including
        /// enemies the animation never came close to.
        /// </summary>
        public float StrikeWidth = 1.2f;

        /// <summary>
        /// What each successive body in one strike takes, relative to the one before it.
        ///
        /// Targets are ordered by distance, so the full number always goes to the enemy the blade
        /// reaches first and a cleave through a crowd pays for its reach. Floored by
        /// <c>MinChainFraction</c> so a wide finisher does not decay into nothing by its fifth
        /// target. 1 disables the taper.
        /// </summary>
        public float ChainFalloff = 0.78f;

        /// <summary>
        /// The Blood Blade's own field: when set, PlayerController substitutes THIS step for the
        /// declared one whenever <see cref="PlayerController.Vial"/> reports full, and empties
        /// the vial in the same moment. Null on every other finisher, and null on the release
        /// step itself - there is no reason for a release to nest a second one.
        ///
        /// A full second AttackStep rather than a parallel set of "heavy" fields on this one,
        /// because the release is a completely different shape (Heavy, AoE, its own motion and
        /// knockback) and describing it as its own ordinary step is exactly what every other
        /// finisher in this file already does - no new field shape needed, just one more of the
        /// thing that already exists.
        /// </summary>
        public AttackStep ReleaseStep;
    }

    /// <summary>
    /// A moveset: basic swings and a finisher. See <see cref="Basics"/> for what each basic is for.
    ///
    /// Movesets are the roguelite layer - drawn at random from floor rewards, capped at three,
    /// and wiped at the start of every run. Gear is the opposite (permanent, account-level), so
    /// the two progression systems stay cleanly separated.
    ///
    /// With several held, each chain plays the basics of the moveset whose finisher is NEXT, so
    /// the lead-in always belongs to the finisher it leads into; a full loadout cycles
    /// A -> B -> C -> A across successive chains.
    /// </summary>
    [Serializable]
    public class Moveset
    {
        public string Id;
        public string DisplayName;
        public string Flavor;

        /// <summary>
        /// Three basics, by ROLE rather than by position in the chain:
        ///
        ///     [0] OPENER    the first swing, played from rest - the Chop on most greatswords
        ///     [1] LEAD-IN   always the swing right before the finisher, and authored for it:
        ///                   it ENDS where the finisher's first frame starts, so the finisher
        ///                   flows out of it instead of the rig blending across the gap in
        ///                   0.06s (Rise for finishers that start high, Wind for ones that
        ///                   start low, Thrust for the thrusts)
        ///     [2] FILLER    only fires when the ledger lengthens the chain (Leaking), between
        ///                   the opener and the lead-in
        ///
        /// MovesetLibrary checks the lead-in against its finisher at start-up and warns when the
        /// two are more than <see cref="MovesetLibrary.LeadInWarnDegrees"/> apart.
        /// </summary>
        public AttackStep[] Basics = Array.Empty<AttackStep>();
        public AttackStep Finisher;

        /// <summary>Icon shown in the HUD when this moveset's finisher is next up.</summary>
        public Glyph FinisherGlyph = Glyph.Overhand;

        /// <summary>
        /// What the finisher actually does, in plain language. This is what the player compares
        /// when choosing - a table of multipliers says nothing about how a move plays.
        /// </summary>
        public string FinisherDescription = "";

        /// <summary>True for the starting moveset every slot holds before anything is earned.</summary>
        public bool IsDefault;

        /// <summary>
        /// Never rolled as a floor reward - this moveset arrives from ONE weapon's signature slot
        /// and nowhere else.
        ///
        /// Enforced in <see cref="MovesetLibrary.RandomExcluding"/> rather than by simply not
        /// handing it out, because the reward pool is built by walking every moveset in the
        /// library: a finisher declared here is offered by default, and "exclusive" has to be a
        /// property of the entry or it lasts exactly until someone adds the next weapon.
        /// </summary>
        public bool SignatureOnly;

        /// <summary>
        /// The weapon class this finisher belongs to. A run only ever rolls its own class's
        /// movesets - a greatsword Meteor performed with a disc would read as the wrong animation
        /// attached to the wrong weapon, which is worse than having fewer options.
        /// </summary>
        public Art.Gear.WeaponClass Class = Art.Gear.WeaponClass.Greatsword;
    }

    /// <summary>
    /// The movesets a run can roll. Defined in code for the same reason the art is procedural:
    /// the game has to be playable and tunable before any authoring pipeline exists.
    /// </summary>
    public static class MovesetLibrary
    {
        /// <summary>
        /// The swing with no moveset equipped: a slow overhand worth two basic swings.
        /// This is what every run starts on, so it has to feel deliberate rather than weak.
        /// </summary>
        public static readonly AttackStep DefaultOverhand = new()
        {
            Name = "Overhand",
            Weight = FinisherWeight.Medium,
            DamageMultiplier = 5f,      // Medium: five basic swings (Tuning.Finisher)
            IntervalMultiplier = 1.5f,  // and Medium's 1.5x lock
            Knockback = 5f,
            ArcDot = 0.15f,
            RangeBonus = 0.15f,
            Motion = AttackMotion.Chop,
            // No ReverseArc: Chop's canonical direction IS the downward stroke now (see
            // ChopFrom/ChopTo in PrimitiveCharacterRig). This briefly carried ReverseArc while
            // that was the other way round.
            //
            // The hop is what separates the finisher from the three basics that share its
            // motion: same arc, but the character leaves the ground to put their weight behind
            // it. It is the only visual difference, and at 1.5x the interval there is room for it.
            HopHeight = 0.52f,
        };

        /// <summary>
        /// What every finisher slot holds at run start. Its finisher is the overhand - slow and
        /// heavy, worth two basic swings - so an unupgraded run still has a real payoff move.
        /// </summary>
        public static readonly Moveset Default = new()
        {
            Id = "default",
            DisplayName = "Overhand",
            Flavor = "Your standing weapon art. Slow, heavy, always available.",
            IsDefault = true,
            FinisherGlyph = Glyph.Overhand,
            FinisherDescription = "A slow, heavy swing brought straight down on one enemy. Nothing fancy - it just hits hard and always works.",
            Basics = new[]
            {
                new AttackStep { Name = "Slash", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Chop },
                new AttackStep { Name = "Rise",  DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Rise },
                new AttackStep { Name = "Slash", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Chop },
            },
            Finisher = DefaultOverhand,
        };

        /// <summary>
        /// The disc class's standing finisher. Every disc slot falls back to this the way every
        /// greatsword slot falls back to Overhand.
        /// </summary>
        public static readonly Moveset DiscDefault = new()
        {
            Id = "disc_default",
            DisplayName = "Cross Cut",
            Flavor = "Your standing weapon art for discs. Close and committed.",
            IsDefault = true,
            Class = Art.Gear.WeaponClass.Disc,
            FinisherGlyph = Glyph.Cross,
            FinisherDescription = "Both discs brought across one enemy at once. Short reach, heavy hit - the disc's answer to something already on top of you.",
            Basics = new[]
            {
                new AttackStep { Name = "Cut", DamageMultiplier = 1f, IntervalMultiplier = 1f },
                new AttackStep { Name = "Cut", DamageMultiplier = 1f, IntervalMultiplier = 1f },
                new AttackStep { Name = "Cut", DamageMultiplier = 1f, IntervalMultiplier = 1f },
            },
            Finisher = new AttackStep
            {
                Name = "Cross Cut", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                Motion = AttackMotion.Sweep, StrikeWidth = 1.1f,
            },
        };

        /// <summary>
        /// The bow class's standing finisher: a placeholder in the same sense DiscDefault was
        /// before the disc had a real reward pool - real bow finishers are the next piece of
        /// work, not this one. Every step is explicitly Draw, since a bow basic never reaches
        /// the general ResolveArc path that would otherwise supply a motion for it.
        /// </summary>
        public static readonly Moveset BowDefault = new()
        {
            Id = "bow_default",
            DisplayName = "Broadhead",
            Flavor = "Your standing shot. Plain and heavy.",
            IsDefault = true,
            Class = Art.Gear.WeaponClass.Bow,
            FinisherGlyph = Glyph.Spike,
            FinisherDescription = "A single heavier arrow. No frills - just more of what the bow already does.",
            Basics = new[]
            {
                new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
            },
            Finisher = new AttackStep
            {
                Name = "Broadhead", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                Weight = FinisherWeight.Medium,
                Motion = AttackMotion.Draw,
            },
        };

        static List<Moveset> _all;

        public static IReadOnlyList<Moveset> All => _all ??= CheckLeadIns(Build());

        /// <summary>
        /// Degrees between a lead-in's last frame and its finisher's first past which start-up
        /// warns. The rig crosses that gap in a 0.06s blend: inside about a quarter turn it reads
        /// as the finisher winding up, past it as a swing with no hit in it. Rise into Whirlwind
        /// was 170 before Wind existed; Wind into the spins is 62.
        /// </summary>
        public const float LeadInWarnDegrees = 90f;

        /// <summary>
        /// The lead-in rule as a mechanism rather than a convention: every greatsword lead-in is
        /// measured against its finisher's first frame, once, when the library is first built.
        /// </summary>
        static List<Moveset> CheckLeadIns(List<Moveset> all)
        {
            foreach (var m in all)
            {
                CheckGripHandoffs(m);

                if (m.Class != Art.Gear.WeaponClass.Greatsword || m.Basics.Length < 2 || m.Finisher == null)
                    continue;

                // Staged as the alt-swing wherever it falls - see PlayerController.DoAttack.
                var lead = m.Basics[1];
                float end = Art.Gear.PrimitiveCharacterRig.EndArmAngle(lead.Motion, !lead.AltVariant);

                WarnIfGap(m, lead, end, m.Finisher);
                if (m.Finisher.ReleaseStep != null) WarnIfGap(m, lead, end, m.Finisher.ReleaseStep);
            }
            return all;
        }

        static void WarnIfGap(Moveset m, AttackStep lead, float leadEnd, AttackStep finisher)
        {
            // These start from something other than the rig's own blend: a leap leaves the screen,
            // and the katana's draw and Separatio's figures run sequences of their own.
            if (finisher.LeapSeconds > 0f || finisher.SheathDraw || finisher.SplitsThreeWays) return;

            // A charged finisher starts from its hold pose (blended into over the entry window).
            bool charged = finisher.ChargeSeconds > 0f;
            float start = charged
                ? Art.Gear.PrimitiveCharacterRig.ChargeArmSwing
                : Art.Gear.PrimitiveCharacterRig.StartArmAngle(finisher.Motion, finisher.AltVariant);

            float gap = UnityEngine.Mathf.Abs(UnityEngine.Mathf.DeltaAngle(leadEnd, start));
            if (gap > LeadInWarnDegrees)
                UnityEngine.Debug.LogWarning(
                    $"[Moveset] {m.DisplayName}: lead-in '{lead.Name}' ({lead.Motion}) ends {gap:F0} degrees " +
                    $"from where '{finisher.Name}' ({(charged ? "its charge" : finisher.Motion.ToString())}) " +
                    $"starts. Give it a lead-in that ends nearer - see Moveset.Basics.");
        }

        // ---- grip hand-offs ----
        //
        // The arm REACHES each motion's grip (PrimitiveCharacterRig.SolveArmReach), so a hand-off
        // has to match the grip as well as the blade angle: when one swing leaves the hands in one
        // place and the next starts them in another, the hands cross that gap in the 0.06s entry
        // blend, and if the blade barely turns meanwhile the sword SLIDES along its own length -
        // usually straight through the torso. Chop into Rise did: blades 12 degrees apart, grips
        // 0.32 apart. A large move WITH a large turn is fine - that is a wind-up (the hands
        // whipping a Chop's blade from low to the cock travel 0.29 while it turns 175).

        /// <summary>
        /// How far (rig units, ~2 body texels per 0.05) a hand-off's grip may travel while the blade
        /// turns less than <see cref="GripSlideTurnDegrees"/> before start-up warns. The good
        /// hand-offs today move it 0 to 0.07; the one that slid moved it 0.32.
        /// </summary>
        public const float GripSlideTravel = 0.12f;

        /// <summary>Below this much blade turn, grip travel reads as the blade sliding rather than
        /// swinging - see <see cref="GripSlideTravel"/>.</summary>
        public const float GripSlideTurnDegrees = 45f;

        /// <summary>
        /// Every hand-off a chain plays, staged exactly as PlayerController plays it: its
        /// NextStep order (the opener, then Basics[2] for any middle swings, then the lead-in
        /// Basics[1] right before the finisher) and its alt staging (odd swings and the lead-in),
        /// for every chain length the ledger can make (1 to 4 basics) - then the finisher, or
        /// its charge and the release, the Blood Blade's release step the same way, and the
        /// finisher back into the opener when the player keeps attacking. Each distinct hand-off
        /// is judged once. Not the bow (its hands do different jobs), not a finisher that leaves
        /// the screen or runs its own sequence (leap, katana draw, Separatio), and not back into
        /// the opener from a finisher that let go of the weapon.
        /// </summary>
        static void CheckGripHandoffs(Moveset m)
        {
            if (m.Class == Art.Gear.WeaponClass.Bow || m.Basics.Length == 0) return;
            var judged = new HashSet<string>();

            // Mirrors PlayerController.NextStep and DoAttack's alt staging.
            (AttackStep step, bool alt) Played(int c, int basics)
            {
                bool leadIn = c >= basics - 1;
                int i = leadIn ? 1 : c == 0 ? 0 : 2;
                var step = m.Basics[System.Math.Min(i, m.Basics.Length - 1)];
                return (step, step.AltVariant ^ (leadIn || (c & 1) == 1));
            }

            void Pair((AttackStep step, bool alt) a, (AttackStep step, bool alt) b)
            {
                if (judged.Add($"{a.step.Name}/{a.step.Motion}/{a.alt}>{b.step.Name}/{b.step.Motion}/{b.alt}"))
                    CheckHandoff(m, a.step, a.alt, b.step, b.alt);
            }

            for (int basics = 1; basics <= MaxChainBasics; basics++)
            {
                for (int c = 0; c + 1 < basics; c++) Pair(Played(c, basics), Played(c + 1, basics));
                if (m.Finisher == null) continue;

                var lead = Played(basics - 1, basics);
                foreach (var f in new[] { m.Finisher, m.Finisher.ReleaseStep })
                {
                    if (f == null || f.LeapSeconds > 0f || f.SheathDraw || f.SplitsThreeWays) continue;
                    Pair(lead, (f, f.AltVariant));
                    if (!f.ThrowsWeapon && !f.OrbitsWeapon && f.DiscThrows == 0)
                        Pair((f, f.AltVariant), Played(0, basics));
                }
            }
        }

        /// <summary>
        /// The longest chain the ledger can make - PlayerController.MaxChainSteps less the finisher.
        /// </summary>
        const int MaxChainBasics = Player.PlayerController.MaxChainSteps - 1;

        static void CheckHandoff(Moveset m, AttackStep from, bool fromAlt, AttackStep to, bool toAlt)
        {
            var fromGrip = Art.Gear.PrimitiveCharacterRig.EndGrip(from.Motion, fromAlt);
            float fromBlade = Art.Gear.PrimitiveCharacterRig.EndArmAngle(from.Motion, fromAlt);
            string fromName = $"'{from.Name}' ({from.Motion})";

            if (to.ChargeSeconds > 0f)
            {
                // Into the charge's hold, then the release out of it - two hand-offs.
                Judge(m, fromName, fromGrip, fromBlade, $"'{to.Name}' (its charge)",
                      Art.Gear.PrimitiveCharacterRig.ChargeGrip, Art.Gear.PrimitiveCharacterRig.ChargeArmSwing);
                fromGrip = Art.Gear.PrimitiveCharacterRig.ChargeGrip;
                fromBlade = Art.Gear.PrimitiveCharacterRig.ChargeArmSwing;
                fromName = $"'{to.Name}' (its charge)";
            }

            Judge(m, fromName, fromGrip, fromBlade, $"'{to.Name}' ({to.Motion})",
                  Art.Gear.PrimitiveCharacterRig.StartGrip(to.Motion, toAlt),
                  Art.Gear.PrimitiveCharacterRig.StartArmAngle(to.Motion, toAlt));
        }

        static void Judge(Moveset m, string fromName, UnityEngine.Vector2 fromGrip, float fromBlade,
                          string toName, UnityEngine.Vector2 toGrip, float toBlade)
        {
            float travel = UnityEngine.Vector2.Distance(fromGrip, toGrip);
            float turn = UnityEngine.Mathf.Abs(UnityEngine.Mathf.DeltaAngle(fromBlade, toBlade));
            if (travel > GripSlideTravel && turn < GripSlideTurnDegrees)
                UnityEngine.Debug.LogWarning(
                    $"[Moveset] {m.DisplayName}: {fromName} leaves the grip {travel:F2} from where " +
                    $"{toName} starts it, while the blade turns only {turn:F0} degrees - the sword " +
                    "slides along itself through the hand-off. Start the second where the first " +
                    "ends (its armReach) - see MovesetLibrary.CheckGripHandoffs.");
        }

        public static Moveset ById(string id)
        {
            foreach (var m in All) if (m.Id == id) return m;
            return null;
        }

        /// <summary>Draw one the player does not already hold. Null when they hold them all.</summary>
        /// <summary>
        /// Draw an earned moveset the player does not already hold. The default is never offered
        /// - it is what slots fall back to, not a reward. Null when they hold them all.
        /// </summary>
        /// <summary>
        /// Every finisher a class may legitimately be GIVEN: not a default (that is the fallback,
        /// not a prize) and not SignatureOnly (those belong to one Black Diamond relic each).
        ///
        /// Extracted so the floor reward and a rolled relic draw from the same list by
        /// construction. Two copies of this filter would drift the first time a moveset was
        /// added, and the failure would be silent - a move that only ever appears from one of
        /// the two sources, with nothing to say which was intended.
        /// </summary>
        public static List<Moveset> RollablePool(Art.Gear.WeaponClass weapon)
        {
            var pool = new List<Moveset>();
            foreach (var m in All)
                if (m.Class == weapon && !m.IsDefault && !m.SignatureOnly) pool.Add(m);
            return pool;
        }

        public static Moveset RandomExcluding(IEnumerable<Moveset> held,
                                              Art.Gear.WeaponClass weapon = Art.Gear.WeaponClass.Greatsword)
        {
            var pool = RollablePool(weapon);

            if (held != null)
                foreach (var h in held) pool.Remove(h);

            if (pool.Count == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Count)];
        }

        /// <summary>
        /// The moveset a fresh slot holds for this weapon. Each class needs its own, or a disc run
        /// starts every slot pointed at an overhand greatsword swing.
        /// </summary>
        public static Moveset DefaultFor(Art.Gear.WeaponClass weapon)
        {
            foreach (var m in All)
                if (m.IsDefault && m.Class == weapon) return m;
            return Default;
        }

        static AttackStep Basic(string name, float dmg, float interval, float arc = 0.25f, float kb = 3.2f,
                                AttackMotion motion = AttackMotion.Chop)
            => new() { Name = name, DamageMultiplier = dmg, IntervalMultiplier = interval, ArcDot = arc,
                       Knockback = kb, Motion = motion };

        static List<Moveset> Build() => new()
        {
            Default,
            DiscDefault,
            BowDefault,

            // ---- discs ----
            //
            // Mostly throws, but NOT all of them. Neither range is the disc's main one - they are
            // two flavours of the same weapon at level single-target damage - so a melee finisher
            // has to exist or the finisher pool would quietly declare range the real class and
            // close quarters the thing you put up with. Carousel is that finisher.
            new Moveset
            {
                Id = "disc_ripple", DisplayName = "Ripple", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Volley,
                Flavor = "Discs skipped low and flat.",
                FinisherDescription = "Three discs thrown at once, each finding its own target. The crowd-clearer.",
                Basics = new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Ripple", DamageMultiplier = 1.1f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light,
                    Motion = AttackMotion.Throw, DiscThrows = 3, DiscRicochetBonus = 0,
                },
            },
            new Moveset
            {
                Id = "disc_carom", DisplayName = "Carom", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Thrown,
                Flavor = "One disc, thrown to keep going.",
                FinisherDescription = "A single disc that ricochets four extra times before it comes home. Rewards standing where the crowd is thickest.",
                Basics = new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Carom", DamageMultiplier = 1.9f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light,
                    Motion = AttackMotion.Throw, DiscThrows = 1, DiscRicochetBonus = 4,
                },
            },
            new Moveset
            {
                Id = "disc_cleaver", DisplayName = "Heavy Cleaver", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Spike,
                Flavor = "A slower, heavier disc.",
                FinisherDescription = "One heavy disc that hits hard and bounces once. Single-target punish for when the crowd has thinned.",
                Basics = new[]
                {
                    Basic("Cut",  1.00f, 1.05f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1.05f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.10f, 1.05f, 0.10f, 0f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Cleaver", DamageMultiplier = 3.4f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy,
                    Motion = AttackMotion.Throw, DiscThrows = 1, DiscRicochetBonus = 1,
                },
            },
            new Moveset
            {
                Id = "disc_carousel", DisplayName = "Carousel", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Whirl,
                Flavor = "Discs held out at arm's length, and a turn.",
                FinisherDescription = "Holds both discs out to your sides and spins, cutting everything within reach. The disc's answer to being surrounded - and the reason close quarters is a choice rather than a mistake.",
                Basics = new[]
                {
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.05f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Carousel", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium,
                    Motion = AttackMotion.Spin,

                    // Arms out, so it genuinely reaches further than a normal cut - and the whole
                    // circle, because the body turns all the way round. AreaOfEffect makes the
                    // reach a circle rather than the capsule a directional swing uses.
                    ArcDot = -1f, RangeBonus = 0.4f, CleavesAll = true, AreaOfEffect = true,

                    // No knockback: shoving the crowd out of reach is the opposite of what a
                    // melee finisher on a hybrid wants, since it hands the fight back to range.
                    Knockback = 0f,
                },
            },
            new Moveset
            {
                Id = "disc_kickback", DisplayName = "Kickback", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Fan,
                Flavor = "Everything at once, and a step back out of it.",
                FinisherDescription = "Sprays four discs in a cone and hops you backward out of reach. Hits hardest right in front of you, so it wants something already too close - and then it is not close any more.",
                Basics = new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Kickback", DamageMultiplier = 1.35f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium,
                    Motion = AttackMotion.Throw,
                    DiscThrows = 4, DiscSpreadDegrees = 70f, DiscRicochetBonus = 0,

                    // Point blank is where this pays. At 0.35 a body in your face takes nearly
                    // three times what one at the rim does, which is what makes the move a
                    // punish for being crowded rather than a general-purpose volley.
                    DiscFarFraction = 0.35f,

                    // Back out to roughly the edge of your own throw band, so the discs land
                    // while you are leaving and you end the move at range - where the class's
                    // fast half lives.
                    RecoilDistance = 3.2f,
                },
            },
            new Moveset
            {
                Id = "disc_scatter", DisplayName = "Scattershot", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Burst,
                Flavor = "Discs loosed in every direction.",
                FinisherDescription = "Five discs thrown outward at once, each bouncing once. No aiming required - it goes everywhere.",
                Basics = new[]
                {
                    Basic("Cut",  0.90f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.90f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Scattershot", DamageMultiplier = 0.85f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light,
                    Motion = AttackMotion.Throw, DiscThrows = 5, DiscRicochetBonus = 1,
                    DiscSpreadDegrees = 360f,
                },
            },

            new Moveset
            {
                Id = "disc_mark", DisplayName = "Mark", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Vortex,
                Flavor = "One disc, and a long chain of them left painted.",
                FinisherDescription = "Throws a single disc that bounces far through the crowd, painting everything it touches. A marked enemy takes double from the next hit that lands on it - so this is the move you throw BEFORE the one that matters.",
                Basics = new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Mark", IntervalMultiplier = 1f, Motion = AttackMotion.Throw,
                    Weight = FinisherWeight.Light,

                    // Deliberately the weakest finisher the class has. Its damage is not its
                    // output - the amplifier it leaves behind is, and if the throw itself also
                    // hit hard there would be no reason to ever follow it up.
                    DamageMultiplier = 0.55f,

                    // The long chain IS the move: four bounces means a whole clump gets painted
                    // off one throw, which is what makes the follow-up worth aiming.
                    DiscThrows = 1, DiscRicochetBonus = 4,

                    DiscMarkMultiplier = 2f, DiscMarkSeconds = 6f,
                },
            },
            new Moveset
            {
                Id = "disc_sublimate", DisplayName = "Sublimate", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Fan,
                Flavor = "Discs that go out and simply stop.",
                FinisherDescription = "Hangs three discs in the air. They do nothing until your NEXT weapon art fires, then all three go off at once. Sublimate into Mark detonates onto amplified targets; Sublimate into Carousel drops it into a crowd you have already closed on.",
                Basics = new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Sublimate", IntervalMultiplier = 1f, Motion = AttackMotion.Throw,
                    Weight = FinisherWeight.Light,

                    // Paid out on detonation, so the number looks large for a finisher that
                    // appears to do nothing when pressed. It is deliberately worth less than a
                    // straight volley of the same size: the tempo cost of spending a finisher to
                    // arm another one has to buy something, but not enough to make the ordering
                    // automatic.
                    DamageMultiplier = 1.5f,

                    DiscThrows = 3, DiscSpreadDegrees = 80f,
                    DiscSuspends = true, DiscSuspendDistance = 2.2f,
                },
            },
            new Moveset
            {
                Id = "disc_orrery", DisplayName = "Orrery", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Whirl,
                Flavor = "Both discs, out on a long circle.",
                FinisherDescription = "Sends both discs into a wide orbit for five sweeps, cutting anything they pass through on each one. The ring follows you, so walking steers it - and anything pressed right up against you sits in the hole in the middle.",
                Basics = new[]
                {
                    Basic("Cut",  0.90f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.90f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Orrery", IntervalMultiplier = 1.5f, Motion = AttackMotion.Throw,
                    Weight = FinisherWeight.Medium,

                    // Per SWEEP, and there are five of them against one target that stays in the
                    // band. Low enough that the full five is comparable to Carousel rather than
                    // strictly better, since Orrery also covers far more ground.
                    DamageMultiplier = 0.5f,

                    DiscThrows = 2, DiscOrbitSweeps = 5, DiscOrbitRadius = 2.2f,
                },
            },
            new Moveset
            {
                FinisherDescription = "Erupts in flame around you. The circle widens as the blade heats, and at its coldest it barely clears your feet.",
                Id = "conflagration", FinisherGlyph = Glyph.Burst, DisplayName = "Conflagration",

                // Emberline's, and only Emberline's. The heat cycle it turns lives on the weapon,
                // so rolling this finisher onto a sword with no cycle would hand out a blast that
                // never grows - the move would be a worse Whirlwind wearing a better name.
                SignatureOnly = true,
                Flavor = "Heavy, deliberate swings that let the blade build.",
                Basics = new[]
                {
                    Basic("Cut",     1.0f, 0.90f, 0.10f, 3.0f, AttackMotion.Chop),
                    Basic("Rise",    1.0f, 0.90f, 0.10f, 3.0f, AttackMotion.Rise),
                    Basic("Cleave",  1.1f, 0.95f, 0.10f, 3.0f, AttackMotion.Chop),
                },
                Finisher = new AttackStep
                {
                    Name = "Conflagration", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium,

                    // PLANTED, not spun. The blade goes into the ground and stays there while the
                    // fire comes up around it - a spin would be the character throwing the flame
                    // outward, which is the opposite reading.
                    Motion = AttackMotion.Plant,

                    // A circle around the player, so it is a blast and not a swing - the two taper
                    // differently and only one of them is honest about what a burst does.
                    ArcDot = -1f, CleavesAll = true, AreaOfEffect = true,

                    // The reach a RED blade has. Every stage above it adds to this rather than
                    // replacing it, so the declared number here is the floor of the cycle and not
                    // one arbitrary point in it.
                    RangeBonus = -0.35f,

                    // Deliberately low at the rim. The whole point of the cycle is that the circle
                    // GROWS, and a blast that hit as hard at its edge as under your feet would
                    // make the growth free instead of a trade for concentration.
                    EdgeDamageFraction = 0.5f,

                    // Displacement is not declared here: DamageInfo.Displaces is set from
                    // isFinisher in ResolveArc, so a finisher moves enemies by being one.
                    Knockback = 5f,

                    // The cycle turns here. See WeaponHeat.
                    ScalesWithHeat = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "No weapon art of its own. Every swing of this chain lands twice - a shadow of you strikes a beat behind with half the blow - and the swing in the weapon art slot is one more of them: a full turn that never roots you, hitting everything around you.",
                Id = "shadow_echo", FinisherGlyph = Glyph.Echoes, DisplayName = "Echo",

                // Shadow's, and only Shadow's - the same reasoning as Conflagration. The whole
                // chain is the signature: its damage is redistributed into the echoed second hit
                // on every swing (Tuning.Shadow.ChainEchoFraction), so rolled onto an ordinary
                // greatsword it would be a finisher slot that gives up its finisher for nothing.
                SignatureOnly = true,
                Flavor = "Light swings that leave something behind them.",

                // Quicker and lighter than Emberline's, which is the whole difference between the
                // two black-diamond greatswords in the hand: Emberline wants heavy, deliberate
                // swings that let the blade build, and Shadow wants a lot of them, because every
                // one of them lands twice.
                Basics = new[]
                {
                    Basic("Cut",    0.85f, 0.86f, 0.10f, 2.6f, AttackMotion.Chop),
                    Basic("Answer", 0.85f, 0.86f, 0.10f, 2.6f, AttackMotion.Wind),
                    Basic("Trail",  0.95f, 0.90f, 0.10f, 2.6f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Echo", IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light,

                    // A full turn, arms out - the shadows are thrown OFF you, which is what the
                    // motion has to say. Plant was the near miss: this move does not put anything
                    // into the ground, it lets go of something.
                    Motion = AttackMotion.Spin,

                    // BASIC DAMAGE, and that is the whole rework. This slot used to pay a
                    // finisher's damage AND carry a permanent unconditional +50% passive, which
                    // measured as a straight power increase rather than a different way to play -
                    // exactly the "buy this, win" shape the rest of this design avoids. Now the
                    // slot gives up its finisher damage entirely: the light finisher's damage is
                    // REDISTRIBUTED into the echoed second hit on every swing of the chain, this
                    // one included. (It once also summoned a ring of shadows casting the wheel's
                    // other finishers; that was removed - it put finisher damage back on top of
                    // the redistribution the rework exists for.)
                    DamageMultiplier = 1f,

                    // The one finisher-slot move in the game that never commits. See NeverLocks.
                    // Also the one finisher with NO timing bar and no wind-up - it is a basic in
                    // the finisher slot, so there is no finisher to time (see Tuning.StrikeTiming
                    // and Tuning.Shadow.ChainEchoFraction for the retune that pays for it).
                    NeverLocks = true,
                    ArcDot = -1f, CleavesAll = true, AreaOfEffect = true,
                    EdgeDamageFraction = 0.6f,
                    Knockback = 4f,
                },
            },
            new Moveset
            {
                FinisherDescription = "Vanishes straight up in a swirl of purple haze, hangs there untouchable, then drops the blade down on the marked circle with the same haze bursting out from underneath it.",
                Id = "phantom_reckoning", FinisherGlyph = Glyph.Phantom, DisplayName = "Reckoning",

                // Phantom's, and only Phantom's - a Black Diamond weapon's ceiling privilege,
                // same reasoning as Conflagration and Echo above.
                SignatureOnly = true,
                Flavor = "Swings that never quite land where they started.",

                // The chain's own flicker (Phantom poofing to a random point in reach and back)
                // is wired on the DRAWN weapon in PlayerController, not here - a moveset describes
                // a swing's shape and knows nothing about which item is equipped. These three read
                // as ordinary swings on paper; the flicker is what makes them Phantom's.
                Basics = new[]
                {
                    Basic("Flicker", 1.0f, 0.92f, 0.15f, 3.4f, AttackMotion.Chop),
                    Basic("Fade",    1.0f, 0.92f, 0.15f, 3.4f, AttackMotion.Sweep),
                    Basic("Return",  1.15f, 1.0f, 0.15f, 4.0f, AttackMotion.Chop),
                },
                Finisher = new AttackStep
                {
                    Name = "Reckoning", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy,
                    Motion = AttackMotion.Slam,
                    ArcDot = -1f, CleavesAll = true, AreaOfEffect = true,
                    Knockback = 11f, RangeBonus = 1.2f,

                    // Shorter than Meteor's flight - this is a vertical hop over the target, not
                    // a screen-clearing leap, so both the airtime and the exposure it is paid
                    // with can stay honest instead of being inflated to fill a longer trip.
                    LeapSeconds = Core.Tuning.Phantom.LeapSeconds,
                    EdgeDamageFraction = Core.Tuning.Phantom.EdgeDamageFraction,
                    ExposedSeconds = Core.Tuning.Phantom.ExposedSeconds,
                    ExposedMultiplier = Core.Tuning.Phantom.ExposedMultiplier,
                },
            },
            new Moveset
            {
                FinisherDescription = "A single precise thrust that draws blood - the fuller stains a shade deeper every time it lands. Once it's full, the same button erupts instead: a wide, heavy burst that empties the blade.",
                Id = "blood_drink", FinisherGlyph = Glyph.Blood, DisplayName = "Bloodletting",

                // Blood Blade's, and only Blood Blade's - a Black Diamond weapon's ceiling
                // privilege, same reasoning as Conflagration, Echo and Reckoning above.
                SignatureOnly = true,
                Flavor = "Patient stabs that build toward a reckoning of their own.",
                Basics = new[]
                {
                    Basic("Nick", 0.9f,  0.85f, 0.30f, 2.8f, AttackMotion.Thrust),
                    Basic("Cut",  0.9f,  0.85f, 0.30f, 2.8f, AttackMotion.Thrust),
                    Basic("Gash", 1.05f, 0.95f, 0.25f, 3.4f, AttackMotion.Chop),
                },
                Finisher = new AttackStep
                {
                    // MEDIUM, single target - the filling half. CleavesAll is left false
                    // deliberately: "single target" is the whole point while the vial is
                    // filling, so nothing here should sweep.
                    Name = "Drink", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium, Motion = AttackMotion.Thrust,
                    ArcDot = 0.8f, StrikeWidth = 0.7f, Knockback = 4f, RangeBonus = 0.3f,

                    // The vial fills on THIS step landing (PlayerController.ResolveArc, gated on
                    // ReleaseStep != null) and PlayerController.DoAttack substitutes the step
                    // below whenever BloodVial reports full - see AttackStep.ReleaseStep's own
                    // doc for why this is a full second step rather than a parallel field set.
                    ReleaseStep = new AttackStep
                    {
                        // HEAVY, AoE - the payoff half. A full circle rather than a swept arc,
                        // because emptying the vial is meant to read as the blood going OUT of
                        // the blade in every direction, not as one more directional swing.
                        Name = "Exsanguinate", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                        Weight = FinisherWeight.Heavy, Motion = AttackMotion.Spin,
                        ArcDot = -1f, CleavesAll = true, AreaOfEffect = true,
                        Knockback = Core.Tuning.Blood.ReleaseKnockback,
                        RangeBonus = Core.Tuning.Blood.ReleaseRangeBonus,
                        EdgeDamageFraction = Core.Tuning.Blood.ReleaseEdgeDamageFraction,
                    },
                },
            },
            new Moveset
            {
                FinisherDescription = "Snaps the blade home, opens the target with two crossing cuts while it is sheathed, then draws it back out on a single vertical stroke. A non-boss it kills is left in two halves.",
                Id = "crosscut", FinisherGlyph = Glyph.Katana, DisplayName = "Crosscut",

                // Zanmato's, and only Zanmato's - a Black Diamond weapon's ceiling privilege,
                // same reasoning as Conflagration, Echo, Reckoning and Bloodletting above. The
                // sheath-and-draw sequence lives on the WEAPON's signature; wielding the saya
                // relic alone still grants it, without the bespoke art.
                SignatureOnly = true,
                Flavor = "Nothing wasted. The blade is only out when it is cutting.",
                Basics = new[]
                {
                    Basic("Draw",   0.85f, 0.82f, 0.12f, 2.6f, AttackMotion.Sweep),
                    Basic("Rise",   0.85f, 0.82f, 0.12f, 2.6f, AttackMotion.Rise),
                    Basic("Return", 1.00f, 0.90f, 0.10f, 3.2f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    // HEAVY, single target. The draw-cut is the one precise stroke the whole
                    // sequence winds toward, so CleavesAll stays false and the arc stays narrow -
                    // "cut the enemy", not "cut everything". The two pre-cuts are resolved by the
                    // coroutine on a small step of their own; DamageMultiplier here is only the
                    // draw-cut's share (see Tuning.Katana - draw + two cuts sum to a Heavy chain).
                    Name = "Crosscut", DamageMultiplier = Core.Tuning.Katana.DrawCutMultiplier,
                    IntervalMultiplier = 2.5f, Weight = FinisherWeight.Heavy,
                    Motion = AttackMotion.Chop, SheathDraw = true,
                    ArcDot = 0.8f, StrikeWidth = 0.7f, Knockback = 10f, RangeBonus = 0.4f,
                },
            },
            new Moveset
            {
                FinisherDescription = "Raises the fused blade and comes apart: three figures - Sulfur, Salt and Mercury - each take one of its swords and strike a different way at once, then fold back into you and the blade is whole again.",
                Id = Art.Gear.DemoGear.SeparatioId, FinisherGlyph = Glyph.Separatio, DisplayName = "Separatio",

                // Tria Prima's, and only Tria Prima's - a Black Diamond ceiling privilege, same
                // reasoning as Conflagration, Echo, Reckoning, Bloodletting and Crosscut above.
                SignatureOnly = true,
                Flavor = "Three principles in one blade, and a chain that tests each of them.",

                // One basic per principle: Sulfur burns in (a heavy chop), Salt grinds across (a
                // sweep), Mercury darts (a thrust). The chain is the finisher's three figures
                // played one at a time, before they are played all at once.
                Basics = new[]
                {
                    Basic("Calcine", 1.0f, 0.95f, 0.20f, 3.4f, AttackMotion.Chop),
                    Basic("Grind",   1.0f, 0.95f, 0.20f, 3.4f, AttackMotion.Sweep),
                    Basic("Quicken", 1.0f, 0.90f, 0.30f, 3.0f, AttackMotion.Thrust),
                },
                Finisher = new AttackStep
                {
                    // MEDIUM, and the damage is the TOTAL: three figures at a third each come out
                    // to one Medium finisher, clean. What the move buys is three directions at
                    // once, not more damage. Each figure's own swing is a strike along its facing,
                    // so CleavesAll lets it carry through whatever is in that direction.
                    Name = "Separatio", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium, Motion = AttackMotion.Chop,
                    ArcDot = 0.55f, Knockback = 4f, RangeBonus = 0.3f, CleavesAll = true,
                    SplitsThreeWays = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Brings the two halves together overhead into the whole armillary and holds it there while the four elements strike out on all four sides - widest where you stand - then parts it again.",
                Id = Art.Gear.DemoGear.QuintessenceId, FinisherGlyph = Glyph.Quintessence,
                DisplayName = "Quintessence", Class = Art.Gear.WeaponClass.Disc,

                // The Armillary's, and only the Armillary's - the disc class's Black Diamond
                // ceiling privilege, as Separatio is the greatsword's.
                SignatureOnly = true,
                Flavor = "Four elements in two hands, and a chain that calls three of them.",

                Basics = new[]
                {
                    Basic("Kindle", 1.0f, 0.95f, 0.15f, 0f, AttackMotion.Sweep),
                    Basic("Surge",  1.0f, 0.95f, 0.15f, 0f, AttackMotion.Sweep),
                    Basic("Gust",   1.0f, 0.90f, 0.15f, 0f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    // MEDIUM, and the damage is the TOTAL: four cones at a quarter each is one
                    // Medium finisher. CleavesAll so each cone carries through everything in it.
                    Name = "Quintessence", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium, Motion = AttackMotion.Chop,
                    ArcDot = -1f, Knockback = 3f, RangeBonus = 0.5f, CleavesAll = true,
                    ReverseCones = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Gathers for one great slash while the light drains from your armour into the stone at your hip, flashes back through you and floods the blade - then cuts it loose as a crescent that flies straight out, through everything in its path. What it kills comes apart.",
                Id = Art.Gear.DemoGear.MagnumOpusId, FinisherGlyph = Glyph.MagnumOpus, DisplayName = "Magnum Opus",

                // The Aether Greatsword's, through its stone - a Black Diamond ceiling privilege,
                // same reasoning as Conflagration and the rest above.
                SignatureOnly = true,
                Flavor = "The work in four stages, and the last is the light let go.",

                // The opus's stages, the last one the weapon art. Ruin's shape: heavy swings into
                // a held gather, the lead-in rising toward the hold.
                Basics = new[]
                {
                    Basic("Nigredo",    1.1f, 1.0f, 0.2f, 3.6f, AttackMotion.Chop),
                    Basic("Albedo",     1.1f, 1.0f, 0.2f, 3.6f, AttackMotion.Rise),
                    Basic("Citrinitas", 1.2f, 1.05f, 0.2f, 4.0f, AttackMotion.Chop),
                },
                Finisher = new AttackStep
                {
                    // HEAVY: the whole damage rides the crescent, which crosses the room - a line
                    // through a crowd rather than Skyfall's cone. Each body past the first takes
                    // a swing's falloff, so the reach is not free damage.
                    Name = "Magnum Opus", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy,
                    Motion = AttackMotion.Slam, ChargeSeconds = Core.Tuning.MagnumOpus.GatherSeconds,
                    Knockback = 9f, CleavesAll = true,
                    MagnumOpus = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Spins a full circle, striking every enemy around you at once and shoving them all back out of reach.",
                Id = "cleave", FinisherGlyph = Glyph.Whirl, DisplayName = "Cleaving Arc",
                Flavor = "Wide, even swings ending in a full circle.",
                Basics = new[]
                {
                    Basic("Slash",    0.9f, 0.85f, 0.10f, 3.2f, AttackMotion.Chop),
                    Basic("Wind",     0.9f, 0.85f, 0.10f, 3.2f, AttackMotion.Wind),
                    Basic("Backhand", 1.0f, 0.90f, 0.10f, 3.2f, AttackMotion.Sweep),
                },
                Finisher = new AttackStep
                {
                    Name = "Whirlwind", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    // The sword leaves the hand and circles the player rather than the body
                    // spinning to swing it - see WhirlThrow and OrbitsWeapon. The hit is still a
                    // full-circle AoE resolved the same way every other Spin finisher is.
                    Motion = AttackMotion.WhirlThrow, OrbitsWeapon = true,
                    Weight = FinisherWeight.Medium,
                    ArcDot = -1f, Knockback = 7f, RangeBonus = 0.5f, CleavesAll = true,
                    // The reach really is a circle - now because the blade orbits it, not
                    // because the body does.
                    AreaOfEffect = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "A rapid burst of strikes that rakes across everything in front of you. Fast enough to keep the chain moving.",
                Id = "flurry", FinisherGlyph = Glyph.Volley, DisplayName = "Flurry",
                Flavor = "Fast, shallow jabs that end in a burst.",
                Basics = new[]
                {
                    Basic("Jab",    0.55f, 0.5f, 0.45f, 1.6f, AttackMotion.Jab),
                    Basic("Jab",    0.55f, 0.5f, 0.45f, 1.6f, AttackMotion.Jab),
                    Basic("Cross",  0.75f, 0.6f, 0.40f, 2.4f, AttackMotion.Jab),
                },
                Finisher = new AttackStep
                {
                    Name = "Rapid Volley", DamageMultiplier = 3f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light,
                    Motion = AttackMotion.Flurry,
                    ArcDot = 0.3f, Knockback = 4f, CleavesAll = true, StrikeWidth = 1.5f,
                },
            },
            new Moveset
            {
                FinisherDescription = "A long thrust straight ahead. Runs a single enemy through at extended reach and hurls them away.",
                Id = "lunge", FinisherGlyph = Glyph.Spike, DisplayName = "Piercing Lunge",
                Flavor = "Narrow and long. Rewards facing the right way.",
                Basics = new[]
                {
                    Basic("Thrust", 1.0f, 0.9f, 0.65f, 2.0f, AttackMotion.Thrust),
                    Basic("Thrust", 1.0f, 0.9f, 0.65f, 2.0f, AttackMotion.Thrust),
                    Basic("Skewer", 1.3f, 1.0f, 0.70f, 3.0f, AttackMotion.Thrust),
                },
                Finisher = new AttackStep
                {
                    Name = "Pierce", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f, Motion = AttackMotion.Impale,
                    Weight = FinisherWeight.Heavy,
                    ArcDot = 0.75f, Knockback = 9f, RangeBonus = 1.4f,
                    StrikeWidth = 0.7f,   // a thrust is a point, not a sweep
                },
            },
            new Moveset
            {
                FinisherDescription = "Slams the ground, flattening everything close by and launching it. The heaviest knockback available.",
                Id = "hammer", FinisherGlyph = Glyph.Burst, DisplayName = "Sunder",
                Flavor = "Slow and heavy. Every hit shoves.",
                Basics = new[]
                {
                    Basic("Smash",  1.5f, 1.3f, 0.2f, 5f, AttackMotion.Chop),
                    Basic("Heave",  1.5f, 1.3f, 0.2f, 5f, AttackMotion.Rise),
                    Basic("Crush",  1.8f, 1.4f, 0.2f, 6f, AttackMotion.Chop),
                },
                Finisher = new AttackStep
                {
                    Name = "Shatter", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f, Motion = AttackMotion.Chop,
                    Weight = FinisherWeight.Heavy,
                    ArcDot = -0.5f, Knockback = 12f, RangeBonus = 0.8f, CleavesAll = true,
                    // The ground carries this one, not the blade.
                    AreaOfEffect = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Hauls every enemy on screen in toward you and drops them at arm's reach - hard from up close, gentler from range, but nothing escapes it. Barely hurts - it exists to gather a scattered fight for whatever swings next.",
                Id = "undertow", FinisherGlyph = Glyph.Vortex, DisplayName = "Undertow",
                Flavor = "Builds speed as the chain goes on, then drags the fight inward.",
                Basics = new[]
                {
                    Basic("Step",   0.7f, 0.9f, 0.3f, 2f, AttackMotion.Sweep),
                    Basic("Turn",   0.9f, 0.7f, 0.3f, 2f, AttackMotion.Wind),
                    Basic("Spiral", 1.1f, 0.55f, 0.2f, 3f, AttackMotion.Sweep),
                },
                // The only finisher that is not trying to kill anything. It trades almost all of
                // its damage for repositioning, so it is worth a slot only alongside the moves
                // that punish a clumped crowd.
                //
                // TWO DELIBERATE EXCEPTIONS TO THE WEIGHT CLASSES, both because this move is
                // Heavy for a FUNCTIONAL reason rather than a design one - PullsIn only works
                // through DamageInfo.Displaces, which only Heavy sets, so demoting it would
                // silently kill the pull entirely (enemies are Health.Immovable).
                //
                //   DAMAGE stays 0.6 rather than Heavy's 6.5. "Barely hurts" is the entire
                //   identity; conforming it would make the reset button also the best damage
                //   finisher in the game.
                //
                //   LOCK stays 0.9 rather than Heavy's 2.5. This is the fight's panic button,
                //   and a reset that commits you for nearly three times as long as it does today
                //   is not the same tool. Heavy's lock is priced against Heavy's damage; Vortex
                //   pays neither.
                Finisher = new AttackStep
                {
                    Name = "Vortex", DamageMultiplier = 0.6f, IntervalMultiplier = 0.9f, Motion = AttackMotion.Spin,
                    Weight = FinisherWeight.Heavy,
                    ArcDot = -1f, Knockback = 9f, PullsIn = true, RangeBonus = 1.2f, CleavesAll = true,
                    // Gathers the whole ring inward - a swept blade cannot express that.
                    AreaOfEffect = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Raises the blade overhead and holds it there while you keep moving, then drives it down in a wide cone. It hits harder than anything else you have, and it lands wherever you are facing when it falls.",
                Id = "ruin", FinisherGlyph = Glyph.Slam, DisplayName = "Ruin",
                Flavor = "Slow, deliberate, and utterly committed.",
                Basics = new[]
                {
                    Basic("Hew",    1.2f, 1.1f, 0.2f, 4f, AttackMotion.Chop),
                    Basic("Raise",  1.2f, 1.1f, 0.2f, 4f, AttackMotion.Rise),
                    Basic("Cleave", 1.4f, 1.2f, 0.2f, 5f, AttackMotion.Chop),
                },
                // The largest number in the game, paid for with 1.5s of not attacking rather than
                // 1.5s of standing still. The cost is the tempo you give up and the swings you do
                // not land while it winds up - not immobility.
                Finisher = new AttackStep
                {
                    Name = "Skyfall", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy,
                    ArcDot = 0.55f, Knockback = 14f, RangeBonus = 1.0f, CleavesAll = true,
                    Motion = AttackMotion.Slam, ChargeSeconds = 1.5f,
                },
            },
            new Moveset
            {
                FinisherDescription = "Hurls your blade in a straight line, spearing everything it passes through. Attack again to call it back, raking the line on its way to your hand; use your ability instead and you appear where it is. Left alone, the throw is spent and the blade simply returns.",
                Id = "wanderblade", FinisherGlyph = Glyph.Thrown, DisplayName = "Wanderblade",
                Flavor = "Loose grip. The blade does not always stay with you.",
                Basics = new[]
                {
                    Basic("Cut",    0.85f, 0.8f,  0.3f, 2.0f, AttackMotion.Chop),
                    Basic("Wind",   0.95f, 0.8f,  0.3f, 2.0f, AttackMotion.Wind),
                    Basic("Cleave", 1.10f, 0.95f, 0.25f, 3.0f, AttackMotion.Chop),
                },
                // The only finisher whose payoff is decided AFTER it is thrown. Damage is modest
                // because the real value is the choice: a line of hits, or a place to stand.
                Finisher = new AttackStep
                {
                    Name = "Cast", DamageMultiplier = 1.6f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium,
                    Knockback = 2.5f, Motion = AttackMotion.Throw,
                    ThrowsWeapon = true, ThrowDistance = 7.5f,
                },
            },
            new Moveset
            {
                FinisherDescription = "Leaps clean off the screen for a second - untouchable, and still free to run. " +
                                      "Then drops onto the marked circle: devastating directly underneath you, weaker toward the rim. " +
                                      "You land winded, taking half again as much damage for two seconds.",
                Id = "meteor", FinisherGlyph = Glyph.Comet, DisplayName = "Falling Star",
                Flavor = "Rising swings that end with the ground itself.",
                Basics = new[]
                {
                    Basic("Slash",  1.0f, 0.95f, 0.25f, 3.5f, AttackMotion.Chop),
                    Basic("Ascend", 1.0f, 0.95f, 0.25f, 3.5f, AttackMotion.Rise),
                    Basic("Vault",  1.3f, 1.05f, 0.20f, 4.5f, AttackMotion.Chop),
                },
                // The only finisher that trades safety in BOTH directions. A second of total
                // immunity is the strongest defensive window in the game, so it is paid for twice:
                // with two seconds at half again as much damage on the way out, and with a blast whose full
                // number only lands on whatever is directly beneath the player. Committing to it
                // in the wrong place is worse than not having it.
                //
                // The recovery is deliberately LONGER than the flight now. Two seconds airborne
                // read as dead time - the move spent most of itself waiting rather than deciding -
                // and cutting it is what makes the landing the point of the move.
                //
                // Floor for LeapSeconds is 0.42: the rise and the fall in LeapStrike are fixed at
                // 0.28 and 0.14, so anything shorter has no hang between them and the character
                // snaps from launch to impact with no airborne beat at all.
                Finisher = new AttackStep
                {
                    Name = "Meteor", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy,
                    ArcDot = -1f, Knockback = 11f, RangeBonus = 1.6f, CleavesAll = true,
                    Motion = AttackMotion.Slam,
                    AreaOfEffect = true, LeapSeconds = 1f, EdgeDamageFraction = 0.3f,
                    ExposedSeconds = 2f, ExposedMultiplier = 1.5f,
                },
            },
            new Moveset
            {
                FinisherDescription = "Holds the blade level at the shoulder, then lunges forward with everything behind it. The hardest single hit in the game, but it forgives nothing.",
                Id = "riposte", FinisherGlyph = Glyph.Cross, DisplayName = "Impale",
                Flavor = "Measured openers, then a single decisive strike.",
                Basics = new[]
                {
                    Basic("Parry",  0.6f, 0.75f, 0.5f, 1.2f, AttackMotion.Thrust),
                    Basic("Feint",  0.8f, 0.75f, 0.5f, 1.2f, AttackMotion.Thrust),
                    Basic("Counter",1.2f, 0.9f, 0.45f, 3.5f, AttackMotion.Thrust),
                },
                Finisher = new AttackStep
                {
                    Name = "Impale", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f, Motion = AttackMotion.Impale,
                    Weight = FinisherWeight.Heavy,
                    ArcDot = 0.8f, Knockback = 8f, RangeBonus = 0.3f,
                    StrikeWidth = 0.7f,   // a thrust is a point, not a sweep
                },
            },
        };
    }
}
