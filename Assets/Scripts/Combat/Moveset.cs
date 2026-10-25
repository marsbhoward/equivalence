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
        /// The leap takes off with a full TURN through the views (AttackMotion.StarLeap) and lands
        /// in the step's Motion, started on the way down so its strike is drawn as the feet touch -
        /// Falling Star's, landing in Sunder's planted slam. Off, the leap holds the charge's
        /// raised blade and plays its Motion on landing (Phantom's hop).
        /// </summary>
        public bool SpinLeap;

        // ---- a disc BASIC thrown at range (the ranged "dance" pairs, 2026-10-15) ----

        /// <summary>The motion a disc basic plays when it is THROWN (nothing in reach) - Motion
        /// is the melee one. Released at its strike (PlayerController.ThrowDisc).</summary>
        public AttackMotion ThrowMotion = AttackMotion.Throw;

        /// <summary>The Tether art (Combat/DiscTether): one disc curves past the target and drags
        /// it in; an elite flinches instead; enemies the disc passes are tagged.</summary>
        public bool Tethers;

        /// <summary>Revolve (Combat/DiscOrbit): both discs make one revolution close round the
        /// body, destroying enemy projectiles that reach them and hitting enemies they run into;
        /// a basic meanwhile knocks one out as a throw.</summary>
        public bool Orbits;

        /// <summary>Plow (Combat/DiscPlow): both discs rolled along the ground in two lanes,
        /// cutting furrows; what they meet is struck and slid out to the side.</summary>
        public bool Plows;

        /// <summary>Kickback: the fling's discs leave at its drawn strike from both hands, each
        /// STRAIGHT along its own line of the cone (ThrownDisc.Straight - the first body it meets),
        /// and the recoil hop goes with them (PlayerController.KickbackStrike).</summary>
        public bool DiscSpray;

        /// <summary>Carousel: the arms wide and a full turn, free to move; each body in the
        /// circle struck as a disc sweeps past it (PlayerController.CarouselStrike).</summary>
        public bool DiscCarousel;

        /// <summary>Heavy Cleaver: one HEAVY disc (Tuning.Cleaver - bigger, slower, its bounces
        /// carrying less) thrown at the drawn strike of the step's chop (CleaverStrike).</summary>
        public bool DiscCleaves;

        /// <summary>Carom: the pair is COMBINED over the head and hurled two-handed as one disc
        /// at the drawn strike, which caroms off enemies, walls and pillars (ThrownDisc.Carom,
        /// PlayerController.CaromStrike).</summary>
        public bool DiscCaroms;

        /// <summary>Ripple: the volley leaves at the motion's drawn strike, the discs from BOTH
        /// hands, fanned over DiscSpreadDegrees, each then HOMING onto its own target - the locked
        /// one and the nearest others in reach, shared out in order round the fan; one enemy takes
        /// them all (ThrownDisc.Home, PlayerController.HomingVolley).</summary>
        public bool DiscHoming;

        /// <summary>Scattershot: a jump, and at its top the discs leave from both hands in an even
        /// RING, one on the facing, each straight down its own spoke; what a spoke meets is struck
        /// and the disc bounces to an enemy the volley has not reached, and a disc that meets
        /// nothing turns back in on the nearest enemy (ThrownDisc.Spoke,
        /// PlayerController.ScattershotStrike).</summary>
        public bool DiscScatter;

        /// <summary>Mark: one disc flicked out at the drawn strike at the lock, bouncing far and
        /// painting each body it strikes (DiscMarkMultiplier) - PlayerController.MarkStrike.</summary>
        public bool DiscMarks;

        /// <summary>Orrery: the pair flung onto two orbits round the player at the drawn strike
        /// (Combat.OrbitingDiscs, PlayerController.OrreryStrike); the player moves freely.</summary>
        public bool DiscOrrery;

        /// <summary>Gyre: the discs spun overhead as a rotor, a cone along the aim drawn in, then a
        /// melee cut as they come back (PlayerController.GyreStrike).</summary>
        public bool Gyres;

        /// <summary>The hit lands when the motion DRAWS its strike (ICharacterRig.StrikeDrawnSeconds),
        /// not on the press - for an art whose cock is long enough that the damage read ahead of
        /// the blow (Cross Cut). The timing bar counts the wait (StrikeDelay).</summary>
        public bool StrikesOnDraw;

        /// <summary>The swing hop the throw takes (a crow hop, a leap).</summary>
        public float ThrowHop;

        /// <summary>The throw leaves the OFF hand (the pair's second throw).</summary>
        public bool ThrowOffhand;

        /// <summary>World units the throw really carries the player along the facing before the
        /// release (Charge's run) - a dash, so walls stop it and a chasm at its end takes them.</summary>
        public float ThrowRun;

        /// <summary>Seconds before the throw's strike the disc leaves (Leap: from the top of the
        /// jump, not the landing).</summary>
        public float ThrowReleaseEarly;

        /// <summary>
        /// A DIRECTED leap: the body is carried this far along the aim over the flight (less, to
        /// land on a locked target in front), and walking steers on top - the ring on the ground
        /// marks where it will come down. 0 is a leap straight up (Phantom's hop).
        /// </summary>
        public float LeapDistance;

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
        /// PRIMA MATERIA, the Aether Longbow's weapon art: the bow thrown like a boomerang at the
        /// target (Combat.BoomerangBow); its hit starts bullet time while the stone is thrown up
        /// (Combat.RelicToss), and the caught bow looses two rapid shots at every enemy round the
        /// one it struck. Handled by <c>PlayerController.PrimaMateriaStrike</c>, ahead of the bow's
        /// own arrow path. Strikes on the throw, so the timing bar winds up before it.
        /// </summary>
        public bool Boomerang;

        /// <summary>
        /// SWARM, The Hive's weapon art (through the Hive Remote): the bow is drawn as for any
        /// shot, but on the release the whole hive leaves instead of an arrow - every arrow the
        /// bow is made of, as one swarm (Combat.HiveSwarm) that balls round the target, darting
        /// in at it and at any enemy close by, then flies home and is the bow again. Handled by
        /// <c>PlayerController.SwarmStrike</c>, ahead of the bow's own arrow path; strikes on the
        /// release, as an arrow does.
        /// </summary>
        public bool Swarm;

        /// <summary>
        /// THE FOUR BIRDS, the Phoenix's weapon art: drawn like a shot while spectral Crow, Swan and
        /// Peacock bows form up in a V either side, then all four loose -
        /// <c>PlayerController.FormationStrike</c>, Combat.BowFormation, Tuning.Formation.
        /// </summary>
        public bool Formation;

        /// <summary>
        /// A bow basic that is a MOVE, not a shot (the user's idea, 2026-10-17): the player rolls to
        /// the KNEEL connector - <c>PlayerController.BowRollStrike</c>, a real dash by the move input
        /// held as it goes (never the aim), else away from the target. No arrow, no i-frames. Its
        /// pair's shot carries both basics' damage (<see cref="BasicsWorth"/> 2).
        /// </summary>
        public bool KneelRoll;

        /// <summary>A bow basic that is a MOVE: a hop straight back from the target onto the RECOIL
        /// connector - <c>PlayerController.BowRollStrike</c>, a real dash, always away from the
        /// target (else back from the facing). No arrow, no i-frames.</summary>
        public bool RecoilHop;

        /// <summary>A bow shot LOBBED (the High pair's second basic): the arrow's peak over the
        /// ground, as a share of the distance to its target. 0: a level shot.</summary>
        public float LobArc;

        /// <summary>A bow shot of TWO arrows (the Twin nock pair's second basic): each carries half
        /// the step's damage; the second flies at the enemy nearest the target within
        /// Tuning.Bow.TwinReach of it, else at the target too.</summary>
        public bool TwinShot;

        /// <summary>HARPOON, the bow's pull: a twin shot whose first arrow trails a LINE - the
        /// enemy it sticks in and up to two near it are reeled in to a set distance in front of the
        /// archer (<c>PlayerController.HarpoonStrike</c>, Combat.BowHarpoon, Tuning.Harpoon).</summary>
        public bool Harpoon;

        /// <summary>CONCUSSION, the bow's PUSH: drawn from the RECOIL, the arrow bursts off the
        /// string and blows back everything in a cone in front of the archer, slowing the group
        /// round where it struck (<c>PlayerController.ConcussionStrike</c>, Combat.ConcussionBurst,
        /// Tuning.Concussion).</summary>
        public bool Concussion;

        /// <summary>DISENGAGE, the bow's REPOSITION: out of the RECOIL, a backflip away from the
        /// target and one arrow loosed down at it from the top (<c>PlayerController.DisengageStrike</c>,
        /// Tuning.Disengage).</summary>
        public bool Disengage;

        /// <summary>PALISADE, the bow's DEFENSIVE: out of the HIGH, arrows land on enemy shots in
        /// flight and stick there, the rest stake a line; each stake stops one more shot
        /// (<c>PlayerController.PalisadeStrike</c>, Combat.BowPalisade, Tuning.Palisade). Spares the
        /// perfect streak - it may hit nothing by design.</summary>
        public bool Palisade;

        /// <summary>PIERCING RAIN: out of the HIGH, one arrow loosed straight up comes down as a
        /// rain on the target's ground over about a second (<c>PlayerController.PiercingRainStrike</c>,
        /// Combat.ArrowRain, Tuning.PiercingRain).</summary>
        public bool ArrowRain;

        /// <summary>LONGSHOT: out of the KNEEL, a long held draw, then one arrow straight across the
        /// room through everything on its line until a wall (<c>PlayerController.LongshotStrike</c>,
        /// Combat.LongshotStreak, Tuning.Longshot).</summary>
        public bool Longshot;

        /// <summary>SPLIT: one arrow that breaks into three halfway (<c>PlayerController.SplitStrike</c>).</summary>
        public bool SplitShot;

        /// <summary>PIN: two arrows that root the target (<c>PlayerController.PinStrike</c>).</summary>
        public bool Pin;

        /// <summary>TRIPWIRE: two staked arrows and a wire that EXPLODES when first crossed
        /// (<c>PlayerController.TripwireStrike</c>, Combat.BowTripwire).</summary>
        public bool Tripwire;

        /// <summary>How many basics this step's hit counts as for the per-basic hooks (the board's
        /// OnBasicLanded, Sulfur's Season) - 2 on the shot that carries a move-basic's damage.</summary>
        public int BasicsWorth = 1;

        /// <summary>
        /// KING AND QUEEN, the Aether Dual Discs' weapon art: after the Magnum Opus's gather
        /// (<see cref="ChargeSeconds"/>), both discs thrown in a pincer that meets on the target
        /// (Combat.PincerDisc), caught at the top of a jump as they come round, and hurled down
        /// onto it. Handled by <c>PlayerController.KingAndQueenStrike</c>, ahead of the disc paths.
        /// </summary>
        public bool Pincer;

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
        /// The player LUNGES with the strike: a dash this far along the facing over
        /// Tuning.Impale.LungeSeconds, and the hit resolves where it ends. Stops short of a locked
        /// target in front (so it is inside the reach, not run past) and at the floor's edge (it
        /// never carries the player into a chasm) - unless it PiercesPath, which is a plain dash
        /// that may cross a chasm or end in one. Zero for everything but Impale.
        /// </summary>
        public float LungeDistance;

        /// <summary>
        /// The lunge goes THROUGH: no stopping short of a locked target or at a chasm's edge (it
        /// is judged where it ends, as a dash is - it may fall), and the hit lands on
        /// every body along the path from where the lunge started to the thrust's reach past
        /// where it ended (nearest first, the blade's falloff). Impale since 2026-10-14.
        /// </summary>
        public bool PiercesPath;

        /// <summary>
        /// The Grappling Hook: a hooked chain thrown along the facing instead of a swing
        /// (Combat/GrapplingHook, Tuning.Grapple). An ordinary enemy it catches is reeled in;
        /// an elite, a boss, a wall or a pillar pulls the player there instead.
        /// </summary>
        public bool Grapples;

        /// <summary>
        /// Sunder's push: every body a displacing hit catches SLIDES out past the strike's rim
        /// (PlayerController.PushOut, Tuning.Sunder) instead of taking the knockback impulse,
        /// which only moves a body half a unit.
        /// </summary>
        public bool PushesOut;

        /// <summary>
        /// Flurry: cut down, cut back up, then the hilt turned and the POMMEL driven into the
        /// target - one fixed-time sequence (Tuning.Flurry, <c>PlayerController.FlurryStrike</c>).
        /// The two cuts land while the timing bar fills and pay its provisional GOOD; the bar
        /// judges the pommel. The step's DamageMultiplier is the whole sequence (what parity and
        /// the balance model price); the coroutine splits it into the cuts' and the pommel's shares.
        /// </summary>
        public bool PommelFlurry;

        /// <summary>
        /// The wind-up is a GUARD: for the whole timing bar, every attack that asks
        /// PlayerController.TryParry is parried - negated, with the parry's tell - and the guard
        /// stays up for the rest of the bar, so a pack's hits are all turned. No counter-hit of
        /// its own: the art's strike is the answer. Riposte, the swords' only defensive art.
        /// </summary>
        public bool Guards;

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
        /// Solve et Coagula, the Gemini Blade's art: the blade splits and each half goes home to a
        /// hip, held sheathed through the timing bar; then a cross-drawn X that carries the player
        /// forward, and the halves slammed back together into one sword's chop. Run by
        /// <c>PlayerController.SplitDrawStrike</c> while the Gemini Blade is DRAWN (the relic alone,
        /// beside another sword, swings it - Crosscut's rule). DamageMultiplier is the WHOLE art,
        /// split by Tuning.SolveCoagula's shares.
        /// </summary>
        public bool SplitDraw;

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
        /// The basics, by ROLE rather than by position in the chain. A chain is TWO basics and the
        /// weapon art - the opener and the lead-in; the filler is optional and only ever plays in a
        /// chain the ledger has lengthened:
        ///
        ///     [0] OPENER    the first swing, played from rest - the Chop on most greatswords
        ///     [1] LEAD-IN   always the swing right before the finisher, and authored for it:
        ///                   it ENDS where the finisher's first frame starts, so the finisher
        ///                   flows out of it instead of the rig blending across the gap in
        ///                   0.06s (Rise for finishers that start high, Wind for ones that
        ///                   start low, Thrust for the thrusts)
        ///     [2] FILLER    OPTIONAL - only fires when the ledger lengthens the chain (Leaking),
        ///                   between the opener and the lead-in; without one, the lead-in repeats
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
            // The hop is what separates the finisher from the basics that share its
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
            Flavor = "Your standing weapon art.",
            IsDefault = true,
            FinisherGlyph = Glyph.Overhand,
            FinisherDescription = "Hops off the ground and brings the blade straight down on the enemy in front of you.",
            Basics = new[]
            {
                new AttackStep { Name = "Slash", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Chop },
                new AttackStep { Name = "Rise",  DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Rise },
                new AttackStep { Name = "Slash", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Chop },
            },
            Finisher = DefaultOverhand,
        };


        // ---- disc basics: a MELEE pair and a RANGED pair (the user's calls, 2026-10-15) ----
        //
        // Every disc moveset swings one melee pair up close and throws one ranged pair at range,
        // both ending on the CONNECTOR its art starts from: RELEASE (Throw's first frame), ORBIT
        // (low, arms wide), CROSS (arms crossed), HALO (both raised), COIL (turned away, both
        // behind the hip). The second hit of each pair is the off hand's. The chain's third basic
        // (Leaking's extra swing) repeats the first.

        public enum DiscMelee { TwinCut, StepTurn, Scissor, PunchDraw, ChopWind }
        public enum DiscRanged { Pirouette, CrowHop, Charge, Leap, Windmill }

        static AttackStep[] DiscBasics(AttackStep[] basics, DiscMelee melee, DiscRanged ranged)
        {
            (AttackMotion m, float hop)[] mm = melee switch
            {
                DiscMelee.TwinCut   => new[] { (AttackMotion.DiscTwinCut, 0f), (AttackMotion.DiscTwinCross, 0f) },
                DiscMelee.StepTurn  => new[] { (AttackMotion.DiscStepCut, 0.10f), (AttackMotion.DiscFlingTurn, 0f) },
                DiscMelee.Scissor   => new[] { (AttackMotion.DiscUppercut, 0.18f), (AttackMotion.DiscScissor, 0f) },
                DiscMelee.PunchDraw => new[] { (AttackMotion.DiscPunch, 0f), (AttackMotion.DiscBackhandCoil, 0f) },
                _                   => new[] { (AttackMotion.DiscChop, 0.12f), (AttackMotion.DiscWindUp, 0f) },
            };
            (AttackMotion m, float hop, float run, float early)[] rr = ranged switch
            {
                DiscRanged.Pirouette => new[] { (AttackMotion.DiscPirouette, 0f, 0f, 0f), (AttackMotion.DiscCounterTurn, 0f, 0f, 0f) },
                DiscRanged.CrowHop   => new[] { (AttackMotion.DiscCrowHop, 0.14f, 0f, 0f), (AttackMotion.DiscCrowSide, 0.08f, 0f, 0f) },
                DiscRanged.Charge    => new[] { (AttackMotion.DiscCharge, 0f, 1.0f, 0f), (AttackMotion.DiscBrake, 0f, 0f, 0f) },
                DiscRanged.Leap      => new[] { (AttackMotion.DiscLeap, 0.45f, 0f, 0.08f), (AttackMotion.DiscSkip, 0f, 0f, 0f) },
                _                    => new[] { (AttackMotion.DiscWindmill, 0f, 0f, 0f), (AttackMotion.DiscCounterWheel, 0f, 0f, 0f) },
            };
            for (int i = 0; i < basics.Length; i++)
            {
                int h = i % 2;
                var b = basics[i];
                b.Motion = mm[h].m;
                b.HopHeight = mm[h].hop;
                b.ThrowMotion = rr[h].m;
                b.ThrowHop = rr[h].hop;
                b.ThrowRun = rr[h].run;
                b.ThrowReleaseEarly = rr[h].early;
                b.ThrowOffhand = h == 1;
            }
            return basics;
        }

        /// <summary>
        /// The disc class's standing finisher. Every disc slot falls back to this the way every
        /// greatsword slot falls back to Overhand.
        /// </summary>
        public static readonly Moveset DiscDefault = new()
        {
            Id = "disc_default",
            DisplayName = "Cross Cut",
            Flavor = "Your standing weapon art for discs.",
            IsDefault = true,
            Class = Art.Gear.WeaponClass.Disc,
            FinisherGlyph = Glyph.Cross,
            FinisherDescription = "Swings both discs out from crossed arms in an X across the enemy in front of you. " +
                                  "The full blow lands on that enemy; anything beside it takes little.",
            Basics = DiscBasics(new[]
            {
                new AttackStep { Name = "Cut", DamageMultiplier = 1f, IntervalMultiplier = 1f },
                new AttackStep { Name = "Cut", DamageMultiplier = 1f, IntervalMultiplier = 1f },
                new AttackStep { Name = "Cut", DamageMultiplier = 1f, IntervalMultiplier = 1f },
            }, DiscMelee.TwinCut, DiscRanged.Charge),
            Finisher = new AttackStep
            {
                // A MELEE art (the disc guideline, 2026-10-15): the whole Medium on the one enemy in
                // front - narrow, and steep falloff past it. Nothing in reach: it cuts the air.
                Name = "Cross Cut", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                Motion = AttackMotion.DiscCrossCut, StrikeWidth = 0.8f, ChainFalloff = 0.35f,
                StrikesOnDraw = true,
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
            Flavor = "Your standing shot.",
            IsDefault = true,
            Class = Art.Gear.WeaponClass.Bow,
            FinisherGlyph = Glyph.Spike,
            FinisherDescription = "From one knee, draws and looses a single heavier arrow.",
            // From the KNEEL (2026-10-23): a shot worth both basics, then the roll down onto one
            // knee (by the stick, else away from the target) - the art drawn steady from there.
            Basics = new[]
            {
                new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                new AttackStep { Name = "Roll", DamageMultiplier = 0f, IntervalMultiplier = 1f, Motion = AttackMotion.BowKneelRoll, KneelRoll = true },
                new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
            },
            Finisher = new AttackStep
            {
                Name = "Broadhead", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                Weight = FinisherWeight.Medium,
                Motion = AttackMotion.KneelDraw,
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
            if (finisher.LeapSeconds > 0f || finisher.SheathDraw || finisher.SplitDraw || finisher.SplitsThreeWays) return;
            // The bow's and the discs' gathers hold their throw's own first frame, not a charge's
            // raised blade.
            if (finisher.Boomerang || finisher.Pincer) return;

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
            // Nor a disc: the warning is a BLADE sliding along itself through the hand, and a round
            // disc has no length to slide - its pairs move the fist freely (2026-10-15).
            if (m.Class == Art.Gear.WeaponClass.Bow || m.Class == Art.Gear.WeaponClass.Disc
                || m.Basics.Length == 0) return;
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
                    if (f == null || f.LeapSeconds > 0f || f.SheathDraw || f.SplitDraw || f.SplitsThreeWays || f.Pincer) continue;
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

        /// <summary>
        /// The floor reward's weapon art offers: arts the player does not hold, drawn from the class's
        /// rollable pool PLUS its standing art (Overhand) - unless every slot an offer could go
        /// into already holds the standing art (the start of a run), where offering it would change
        /// nothing. So a player who replaced Overhand can take it back. Relic rolls stay on
        /// <see cref="RollablePool"/>: a relic carrying the default would be a relic carrying
        /// nothing. Up to <paramref name="count"/> DISTINCT arts, fewer (or none) when the pool
        /// runs short - two cards offering the same art would be one choice drawn twice.
        /// </summary>
        public static List<Moveset> RandomOffers(IReadOnlyList<Moveset> slots, System.Func<int, bool> locked,
                                                 Art.Gear.WeaponClass weapon, int count)
        {
            var pool = RollablePool(weapon);
            bool allStanding = true;
            for (int i = 0; i < slots.Count; i++)
            {
                var m = slots[i];
                if (m != null && !m.IsDefault) pool.Remove(m);
                if (locked != null && locked(i)) continue;
                if (m == null || !m.IsDefault) allStanding = false;
            }

            var standing = DefaultFor(weapon);
            if (!allStanding && standing != null && standing.Class == weapon) pool.Add(standing);

            var picks = new List<Moveset>();
            while (picks.Count < count && pool.Count > 0)
            {
                int i = UnityEngine.Random.Range(0, pool.Count);
                picks.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return picks;
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
                FinisherDescription = "Throws four discs, two from each hand. They fan out, then curve onto the nearest enemies, each disc to its own while there are enough of them.",
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.ChopWind, DiscRanged.Pirouette),
                Finisher = new AttackStep
                {
                    Name = "Ripple", DamageMultiplier = 0.825f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light,
                    Motion = AttackMotion.DiscFanThrow, DiscThrows = 4, DiscRicochetBonus = 0,
                    DiscSpreadDegrees = Core.Tuning.Ripple.SpreadDegrees, DiscHoming = true,
                },
            },
            new Moveset
            {
                Id = "disc_carom", DisplayName = "Carom", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Thrown,
                Flavor = "One disc, thrown to keep going.",
                FinisherDescription = "Brings both discs together over the head and hurls them as one. It rebounds off enemies, walls and pillars five times before it comes home.",
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.ChopWind, DiscRanged.Pirouette),
                Finisher = new AttackStep
                {
                    Name = "Carom", DamageMultiplier = 1.9f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light,
                    Motion = AttackMotion.DiscCaromHurl, DiscThrows = 1, DiscRicochetBonus = 4,
                    DiscCaroms = true,
                },
            },
            new Moveset
            {
                Id = "disc_cleaver", DisplayName = "Heavy Cleaver", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Spike,
                Flavor = "A slower, heavier disc.",
                FinisherDescription = "Brings one disc down over the head like a cleaver and throws it. The big, slow disc knocks back what it hits and bounces twice before it comes home.",
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  1.00f, 1.05f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1.05f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.10f, 1.05f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.Scissor, DiscRanged.Leap),
                Finisher = new AttackStep
                {
                    Name = "Cleaver", DamageMultiplier = 5.0f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy, Knockback = 9f,
                    Motion = AttackMotion.DiscCleaverChop, DiscThrows = 1, DiscRicochetBonus = 1,
                    DiscCleaves = true,
                },
            },
            new Moveset
            {
                Id = "disc_carousel", DisplayName = "Carousel", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Whirl,
                Flavor = "Discs held out at arm's length, and a turn.",
                FinisherDescription = "Holds both discs out wide and turns two full circles, cutting each enemy within reach every time a disc sweeps past it. You can keep moving through the turns.",
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.05f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.StepTurn, DiscRanged.CrowHop),
                Finisher = new AttackStep
                {
                    // Two turns take a Heavy's length (2.5); 7 basics keeps a Medium's damage a
                    // second over the longer chain - 3.5 a body each turn. Free to move throughout.
                    Name = "Carousel", DamageMultiplier = 7f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Medium,
                    Motion = AttackMotion.DiscCarousel, DiscCarousel = true,

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
                FinisherDescription = "Unwinds and flings four discs from both hands in a fan in front of you, hopping you backward. Each disc flies straight and hits the first enemy in its path, hardest right in front of you.",
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.PunchDraw, DiscRanged.Windmill),
                Finisher = new AttackStep
                {
                    Name = "Kickback", DamageMultiplier = 1.35f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium,
                    Motion = AttackMotion.DiscKickFling, DiscSpray = true,
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
                FinisherDescription = "Jumps and, at the top, flings six discs out in a ring from both hands. Each flies straight out and strikes the first enemy in its path, then bounces to one the others missed. A disc that meets nothing turns back in onto the nearest enemy.",
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.90f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.90f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.Scissor, DiscRanged.Leap),
                Finisher = new AttackStep
                {
                    // RANGED (the disc guideline): six spokes over a crowd; a lone enemy takes all
                    // six as the empty spokes turn in on it - 3.3 basics, a Light at a good press.
                    Name = "Scattershot", DamageMultiplier = 0.55f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light,
                    Motion = AttackMotion.DiscScatterBurst, DiscThrows = 6, DiscScatter = true,
                },
            },

            new Moveset
            {
                Id = "disc_mark", DisplayName = "Mark", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Vortex,
                Flavor = "One disc, and a long chain of them left painted.",
                FinisherDescription = "Points at the target and flicks one disc out sidearm. It bounces far through the crowd and paints a sigil over every enemy it strikes. The next weapon art to hit a painted enemy breaks the sigil and deals half again.",
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.ChopWind, DiscRanged.Pirouette),
                Finisher = new AttackStep
                {
                    Name = "Mark", IntervalMultiplier = 1f, Motion = AttackMotion.DiscMarkFlick, DiscMarks = true,
                    Weight = FinisherWeight.Light,

                    // Deliberately the weakest finisher the class has. Its damage is not its
                    // output - the amplifier it leaves behind is, and if the throw itself also
                    // hit hard there would be no reason to ever follow it up.
                    DamageMultiplier = 0.55f,

                    // The long chain IS the move: four bounces means a whole clump gets painted
                    // off one throw, which is what makes the follow-up worth aiming.
                    DiscThrows = 1, DiscRicochetBonus = 4,

                    // Spent only by the NEXT WEAPON ART's hit (Health.Take), so it waits through
                    // the chain's basics: 0.55 + half the next art on one enemy - about a Light
                    // before a Medium - and half again on every painted body an area art reaches.
                    DiscMarkMultiplier = Core.Tuning.Mark.Multiplier, DiscMarkSeconds = Core.Tuning.Mark.Seconds,
                },
            },
            new Moveset
            {
                Id = "disc_tether", DisplayName = "Tether", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Thrown,
                Flavor = "Two discs crossed, and one sent round behind.",
                FinisherDescription = "Throws one disc round past the enemy, hooks it from behind and drags it in to you. " +
                                      "Anything too heavy to move flinches instead. Every other enemy the disc passes is struck as it goes.",
                // The disc class's PULL (the user's calls, 2026-10-15). LIGHT: the pull is the
                // move. The Cross pair hands into the cast (arms crossed at the chest, snapping
                // open). Grappling Hook's reel; an Anchored elite is FLINCHED, armour or not.
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.TwinCut, DiscRanged.Charge),
                Finisher = new AttackStep
                {
                    Name = "Tether", DamageMultiplier = 3f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light, Motion = AttackMotion.DiscTetherCast,
                    Tethers = true,
                },
            },
            new Moveset
            {
                Id = "disc_gyre", DisplayName = "Gyre", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Vortex,
                Flavor = "Both discs spun out in front, and the air pulled in through them.",
                FinisherDescription = "Reaches out and spins both discs in front of you like rotor blades. Everything in a cone in front is drawn in toward you, " +
                                      "cutting whatever comes close, then the discs come back to your hands in a cut across what is in front. Anything too heavy to move flinches instead.",
                // The discs' MELEE pull (the user's call, 2026-10-16: helicopter style, aimed as a
                // cone; Tether is the ranged one). LIGHT: the pull is the move, and the cut is a
                // melee hit - the whole Light on the first body, steep falloff after; nothing in
                // front, it cuts the air. Out of HALO (the Scissor and Leap pairs end both raised).
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.Scissor, DiscRanged.Leap),
                Finisher = new AttackStep
                {
                    Name = "Gyre", DamageMultiplier = 3f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light, Motion = AttackMotion.DiscGyreLift,
                    Gyres = true, StrikeWidth = 1.4f, ChainFalloff = 0.5f, CleavesAll = true,
                    RangeBonus = 0.4f,   // reaches the pile the pull leaves
                },
            },
            new Moveset
            {
                Id = "disc_revolve", DisplayName = "Revolve", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Whirl,
                Flavor = "Both discs sent round you, and you keep fighting.",
                FinisherDescription = "Sets four discs turning in a close circle round you. Shots that reach the circle burst into smoke; " +
                                      "an enemy a disc runs into is struck. Each attack knocks one disc out of the circle at your target, and the next weapon art takes the last two.",
                // The disc class's DEFENSIVE art (the user's calls, 2026-10-15; the name is mine).
                // MEDIUM, as Riposte is the swords'. One revolution, 1.5 s, straight through;
                // projectiles destroyed; the hands are empty but the basics go on unarmed, each
                // knocking a disc out of the ring. The Orbit pair hands into the cast.
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.StepTurn, DiscRanged.CrowHop),
                Finisher = new AttackStep
                {
                    Name = "Revolve", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium, Motion = AttackMotion.DiscOrbitCast,
                    Orbits = true,
                },
            },
            new Moveset
            {
                Id = "disc_plow", DisplayName = "Plow", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Fan,
                Flavor = "Both discs bowled low, and the crowd parted.",
                FinisherDescription = "Bowls both discs along the ground side by side, cutting two furrows as they roll. " +
                                      "Everything they meet is struck and shoved aside, out of their path.",
                // The disc class's PUSH (the user's calls, 2026-10-15). HEAVY, as Sunder is the
                // swords': everything struck flinches, armour or not, and is slid out to the side
                // (the slide is the push - a knockback alone moves a body ~0.6). The Cross pair
                // hands into the cast.
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.TwinCut, DiscRanged.Charge),
                Finisher = new AttackStep
                {
                    Name = "Plow", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy, Motion = AttackMotion.DiscPlowCast,
                    Knockback = 6f, Plows = true,
                },
            },
            new Moveset
            {
                Id = "disc_sublimate", DisplayName = "Sublimate", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Fan,
                Flavor = "Discs that go out and simply stop.",
                FinisherDescription = "Snaps the arms open and flings three discs out. Each slows to a stop over an enemy and follows it, trailing vapour. When your next weapon art fires, all three go off.",
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.95f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 1f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.TwinCut, DiscRanged.Charge),
                Finisher = new AttackStep
                {
                    Name = "Sublimate", IntervalMultiplier = 1f, Motion = AttackMotion.DiscSublimateOpen,
                    Weight = FinisherWeight.Light,

                    // Paid out on detonation. Each disc hangs over its OWN enemy and follows it
                    // (2026-10-16), so the blasts land: a lone enemy under all three takes 3, a
                    // Light - the price is the wait for the next art, not a miss.
                    DamageMultiplier = 1.0f,

                    DiscThrows = 3, DiscSpreadDegrees = 80f,
                    DiscSuspends = true, DiscSuspendDistance = 2.2f,
                },
            },
            new Moveset
            {
                Id = "disc_orrery", DisplayName = "Orrery", Class = Art.Gear.WeaponClass.Disc,
                FinisherGlyph = Glyph.Whirl,
                Flavor = "Both discs, out on a long circle.",
                FinisherDescription = "Sweeps both arms up and sends the discs onto two orbits around you, drawn faintly on the ground: one close, one wide and slower. Each cuts what it passes. They follow you as you move, then spiral back to your hands.",
                Basics = DiscBasics(new[]
                {
                    Basic("Cut",  0.90f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  0.90f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                    Basic("Cut",  1.00f, 0.95f, 0.10f, 0f, AttackMotion.Sweep),
                }, DiscMelee.StepTurn, DiscRanged.CrowHop),
                Finisher = new AttackStep
                {
                    Name = "Orrery", IntervalMultiplier = 3.5f, Motion = AttackMotion.DiscOrreryCast,
                    Weight = FinisherWeight.Medium, DiscOrrery = true,

                    // What a body under either path for the whole art takes (Tuning.Orrery's
                    // shares). The discs orbit for the rest of the swing after the cast, so the
                    // interval is long enough for about a second of orbit (at a Heavy's 2.5 it was
                    // 0.35 s - a blur); 9 keeps a Medium's damage a second over the longer chain:
                    // (2 + 9) / (2 + 3.5) = 2.0, Carousel's sums.
                    DamageMultiplier = 9f,

                    DiscThrows = 2,
                },
            },
            new Moveset
            {
                FinisherDescription = "Plants the blade and erupts in a ring of flame around you. The ring widens as the blade heats; at its coldest it barely clears your feet.",
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
                FinisherDescription = "No weapon art of its own. A shadow of you repeats every swing of this chain a beat behind, landing it a second time - and the swing in the weapon art slot is one more of them: a full turn that hits everything around you without stopping you in place.",
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
                FinisherDescription = "A single thrust that draws blood - the fuller stains a shade deeper every time it lands. Once it is full, the same button spins the blade instead, bursting the blood out in a wide ring and emptying the fuller.",
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
                FinisherDescription = "Splits the blade and sends each half home to a sheath at either hip, cross-draws both in an X that carries you forward, then slams the halves together and brings them down as one sword.",
                Id = "solve_et_coagula", FinisherGlyph = Glyph.Cross, DisplayName = "Solve et Coagula",

                // The Gemini Blade's, and only its - a Black Diamond ceiling privilege, the same
                // reasoning as Crosscut and Separatio. The sequence needs the Gemini Blade DRAWN;
                // the Gemini Saya alone grants the art and it swings.
                SignatureOnly = true,
                Flavor = "Dissolve, then bind.",
                Basics = new[]
                {
                    Basic("Part",   0.95f, 0.88f, 0.15f, 3.0f, AttackMotion.Chop),
                    Basic("Mirror", 0.95f, 0.88f, 0.15f, 3.0f, AttackMotion.Rise),
                },
                Finisher = new AttackStep
                {
                    // LIGHT: three basics in all, the X's share and the slam's (Tuning.SolveCoagula).
                    // The slam is this step's own swing - the rejoined sword's Chop.
                    Name = "Solve et Coagula", DamageMultiplier = 3f,
                    IntervalMultiplier = 1.0f, Weight = FinisherWeight.Light,
                    Motion = AttackMotion.Chop, SplitDraw = true,
                    ArcDot = 0.5f, StrikeWidth = 0.9f, Knockback = 2f, RangeBonus = 0.3f,
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

                Basics = DiscBasics(new[]
                {
                    Basic("Kindle", 1.0f, 0.95f, 0.15f, 0f, AttackMotion.Sweep),
                    Basic("Surge",  1.0f, 0.95f, 0.15f, 0f, AttackMotion.Sweep),
                    Basic("Gust",   1.0f, 0.90f, 0.15f, 0f, AttackMotion.Sweep),
                }, DiscMelee.Scissor, DiscRanged.Leap),
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
                Id = "bow_harpoon", DisplayName = "Harpoon", Class = Art.Gear.WeaponClass.Bow,
                FinisherGlyph = Glyph.Thrown,
                Flavor = "Two arrows on the string, and one of them on a line.",
                FinisherDescription = "Looses two arrows at once, one trailing a line from the bow. Where the line sticks, " +
                                      "that enemy and the two nearest beside it are reeled in together and stop a short way in front of you. " +
                                      "Anything too heavy to move stays where it is.",
                // The bow's PULL (the user's calls, 2026-10-17): LIGHT, from the TWIN NOCK connector -
                // a shot, then two arrows at once, and the art's second arrow carries the line.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                    new AttackStep { Name = "Twin", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.TwinDraw, TwinShot = true },
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                },
                Finisher = new AttackStep
                {
                    Name = "Harpoon", DamageMultiplier = 3f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light, Motion = AttackMotion.TwinDraw,
                    Harpoon = true,
                },
            },
            new Moveset
            {
                Id = "bow_split", DisplayName = "Split", Class = Art.Gear.WeaponClass.Bow,
                FinisherGlyph = Glyph.Fan,
                Flavor = "One becomes three.",
                FinisherDescription = "Looses a single arrow flat from the hip. Halfway to the target it breaks into three, " +
                                      "one for the target and one each for the two enemies nearest it.",
                // LIGHT, from the CANTED - a snap shot, then the flat sidearm shot.
                Basics = new[]
                {
                    new AttackStep { Name = "Snap", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.SnapDraw },
                    new AttackStep { Name = "Canted", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.CantedDraw },
                    new AttackStep { Name = "Snap", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.SnapDraw },
                },
                Finisher = new AttackStep
                {
                    Name = "Split", DamageMultiplier = 3f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light, Motion = AttackMotion.CantedDraw,
                    SplitShot = true,
                },
            },
            new Moveset
            {
                Id = "bow_pin", DisplayName = "Pin", Class = Art.Gear.WeaponClass.Bow,
                FinisherGlyph = Glyph.Spike,
                Flavor = "Stay.",
                FinisherDescription = "From one knee, looses two arrows in quick succession that nail the target's feet to the ground. " +
                                      "It cannot walk for a few seconds, though it can still strike at whatever is close.",
                // LIGHT, from the KNEEL - a shot worth both basics, then the roll onto the knee.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                    new AttackStep { Name = "Roll", DamageMultiplier = 0f, IntervalMultiplier = 1f, Motion = AttackMotion.BowKneelRoll, KneelRoll = true },
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                },
                Finisher = new AttackStep
                {
                    Name = "Pin", DamageMultiplier = 3f, IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light, Motion = AttackMotion.KneelDraw,
                    Pin = true,
                },
            },
            new Moveset
            {
                Id = "bow_tripwire", DisplayName = "Tripwire", Class = Art.Gear.WeaponClass.Bow,
                FinisherGlyph = Glyph.Cross,
                Flavor = "Mind your step.",
                FinisherDescription = "Looses two arrows together that stake into the ground on either side of your aim, " +
                                      "a wire strung tight between them. The first enemy to run into the wire sets it off: " +
                                      "it explodes, throwing back everything near and leaving them moving at half speed for a few seconds.",
                // HEAVY (the user's call, 2026-10-18 - it was Medium, cutting each crosser), from the
                // TWIN NOCK - a shot, then two arrows at once.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                    new AttackStep { Name = "Twin", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.TwinDraw, TwinShot = true },
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                },
                Finisher = new AttackStep
                {
                    Name = "Tripwire", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy, Motion = AttackMotion.TwinDraw, Knockback = 7f,
                    Tripwire = true,
                },
            },
            new Moveset
            {
                Id = "bow_longshot", DisplayName = "Longshot", Class = Art.Gear.WeaponClass.Bow,
                FinisherGlyph = Glyph.Spike,
                Flavor = "Down on one knee, and wait for it.",
                FinisherDescription = "From one knee, draws and holds the string back for a long moment, then looses an arrow " +
                                      "that flies straight across the room, through every enemy in its path, until it buries itself in a wall.",
                // HEAVY (the user's calls, 2026-10-18), from the KNEEL - a shot worth both basics,
                // then the roll down onto the knee.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                    new AttackStep { Name = "Roll", DamageMultiplier = 0f, IntervalMultiplier = 1f, Motion = AttackMotion.BowKneelRoll, KneelRoll = true },
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                },
                Finisher = new AttackStep
                {
                    Name = "Longshot", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy, Motion = AttackMotion.KneelDraw, Knockback = 7f,
                    Longshot = true,
                },
            },
            new Moveset
            {
                Id = "bow_piercing_rain", DisplayName = "Piercing Rain", Class = Art.Gear.WeaponClass.Bow,
                FinisherGlyph = Glyph.Volley,
                Flavor = "What goes up.",
                FinisherDescription = "Looses one arrow straight up out of sight. A moment later a rain of arrows falls on the ground " +
                                      "where your target stood, striking everything inside it again and again for about a second, " +
                                      "and the last of it throws them out of the circle.",
                // HEAVY (the user's calls, 2026-10-18), from the HIGH - a level shot, then the lob.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                    new AttackStep { Name = "Lob", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.LobDraw, LobArc = Core.Tuning.Bow.LobArc },
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                },
                Finisher = new AttackStep
                {
                    Name = "Piercing Rain", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy, Motion = AttackMotion.HighDraw, Knockback = 7f,
                    ArrowRain = true,
                },
            },
            new Moveset
            {
                Id = "bow_palisade", DisplayName = "Palisade", Class = Art.Gear.WeaponClass.Bow,
                FinisherGlyph = Glyph.Volley,
                Flavor = "Every shot answered.",
                FinisherDescription = "Looses five arrows high into the air. Each one comes down on an enemy shot still in flight, " +
                                      "breaks it, and sticks in the ground where they met; any left over land in a line in front of you. " +
                                      "Each standing arrow stops the next shot that reaches it and breaks, and one an enemy walks over breaks and cuts it.",
                // The bow's DEFENSIVE (the user's calls, 2026-10-18): MEDIUM, from the HIGH - a level
                // shot, then the lob with the bow raised.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                    new AttackStep { Name = "Lob", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.LobDraw, LobArc = Core.Tuning.Bow.LobArc },
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                },
                Finisher = new AttackStep
                {
                    Name = "Palisade", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium, Motion = AttackMotion.HighDraw,
                    Palisade = true,
                },
            },
            new Moveset
            {
                Id = "bow_disengage", DisplayName = "Disengage", Class = Art.Gear.WeaponClass.Bow,
                FinisherGlyph = Glyph.Comet,
                Flavor = "Never where they reach.",
                FinisherDescription = "Springs into a backflip away from the target, drawing as you turn. " +
                                      "At the top of the flip you loose one arrow down at it, then land a good distance back.",
                // The bow's REPOSITION (the user's calls, 2026-10-18): MEDIUM, from the RECOIL - a
                // shot worth both basics, the hop back, then the flip further back still.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                    new AttackStep { Name = "Recoil", DamageMultiplier = 0f, IntervalMultiplier = 1f, Motion = AttackMotion.BowRecoilHop, RecoilHop = true },
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                },
                Finisher = new AttackStep
                {
                    Name = "Disengage", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium, Motion = AttackMotion.FlipDraw,
                    Disengage = true,
                },
            },
            new Moveset
            {
                Id = "bow_concussion", DisplayName = "Concussion", Class = Art.Gear.WeaponClass.Bow,
                FinisherGlyph = Glyph.Fan,
                Flavor = "Step back, then make the room.",
                FinisherDescription = "Draws from the back foot and looses an arrow that bursts just off the string. " +
                                      "The blast throws everything in front of you back out of reach, and the ones nearest where it struck " +
                                      "move at half speed for a few seconds. Anything too heavy to move stays where it is.",
                // The bow's PUSH (the user's calls, 2026-10-18): MEDIUM, from the RECOIL connector -
                // a shot worth both basics, then the hop back with the bow pulled in.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                    new AttackStep { Name = "Recoil", DamageMultiplier = 0f, IntervalMultiplier = 1f, Motion = AttackMotion.BowRecoilHop, RecoilHop = true },
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                },
                Finisher = new AttackStep
                {
                    Name = "Concussion", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium, Motion = AttackMotion.RecoilDraw,
                    Concussion = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Gathers for one great slash while the light drains from your armour into the stone at your hip, flashes back through you and floods the blade - then cuts it loose as a crescent that flies straight out, through everything in its path. What it kills comes apart.",
                Id = Art.Gear.DemoGear.MagnumOpusId, FinisherGlyph = Glyph.MagnumOpus, DisplayName = "Magnum Opus",

                // The Aether Greatsword's, through its stone - a Black Diamond ceiling privilege,
                // same reasoning as Conflagration and the rest above.
                SignatureOnly = true,
                Flavor = "The blackening, the whitening, and then the light let go.",

                // TWO basics, as every chain has (PlayerController.BasicsPerChain): the opus's first
                // stages, then the weapon art. Ruin's shape: heavy swings into a held gather, the
                // lead-in rising toward the hold.
                Basics = new[]
                {
                    Basic("Nigredo", 1.1f, 1.0f, 0.2f, 3.6f, AttackMotion.Chop),
                    Basic("Albedo",  1.1f, 1.0f, 0.2f, 3.6f, AttackMotion.Rise),
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
                FinisherDescription = "Throws the bow like a boomerang at your target. If it strikes, the world slows: you throw the stone up as the bow curves home, catch it, and loose two rapid shots at every enemy around the one it hit. If it misses, it simply comes back.",
                Id = Art.Gear.DemoGear.PrimaMateriaArtId, FinisherGlyph = Glyph.PrimaMateria,
                DisplayName = "Prima Materia", Class = Art.Gear.WeaponClass.Bow,

                // The Aether Longbow's, through its stone - a Black Diamond ceiling privilege, same
                // reasoning as Conflagration and the rest above.
                SignatureOnly = true,
                Flavor = "What is let go comes back, and what comes back is fixed.",
                // From the RECOIL (2026-10-23): a shot worth both basics, then the hop back - the
                // weight on the back foot is the wind-up the bow is hurled from.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                    new AttackStep { Name = "Recoil", DamageMultiplier = 0f, IntervalMultiplier = 1f, Motion = AttackMotion.BowRecoilHop, RecoilHop = true },
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw, BasicsWorth = 2 },
                },
                Finisher = new AttackStep
                {
                    // HEAVY: the struck enemy takes exactly one (see Tuning.PrimaMateria's shares),
                    // and the boomerang's hit is what displaces.
                    Name = "Prima Materia", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy, Motion = AttackMotion.Throw,
                    Knockback = 6f, ChainFalloff = 0.85f,
                    // The Magnum Opus's gather first (the user's call): the light drains, the
                    // stone swells, the flash runs back through the armour into the bow - then the
                    // throw. A charge in all but pose: the bow holds the throw's cocked first frame.
                    ChargeSeconds = Core.Tuning.MagnumOpus.GatherSeconds,
                    Boomerang = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Draws the bow and lets the whole hive go: every arrow the bow is made of flies out together and swarms around your target, darting in at it and at any enemy nearby, then flies home and becomes the bow again.",
                Id = Art.Gear.DemoGear.SwarmArtId, FinisherGlyph = Glyph.Swarm,
                DisplayName = "Swarm", Class = Art.Gear.WeaponClass.Bow,

                // The Hive's, through the Hive Remote - a Black Diamond ceiling privilege, same
                // reasoning as Conflagration and the rest above.
                SignatureOnly = true,
                Flavor = "Every arrow knows its way home.",
                // From the TWIN NOCK (2026-10-23): a shot, then two arrows at once - the bow held
                // ready, and the art a plain draw from there that lets every arrow go.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                    new AttackStep { Name = "Twin", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.TwinDraw, TwinShot = true },
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                },
                Finisher = new AttackStep
                {
                    // MEDIUM (the user's call, 2026-10-11): the target takes exactly one
                    // (Tuning.Swarm's shares), and the swarm's last strike on it flinches it -
                    // unarmoured only, and nothing is displaced.
                    Name = "Swarm", DamageMultiplier = 5f, IntervalMultiplier = 1.5f,
                    Weight = FinisherWeight.Medium, Motion = AttackMotion.Draw,
                    Knockback = 0f, ChainFalloff = 0.85f,
                    Swarm = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Brings the raised bow down and draws while the Crow, the Swan and the Peacock appear as bows of light in a V either side of you, drawing with you. All four loose together: the crow's arrow flies straight through everything in its line, the swan's splits on your target into arrows at the enemies nearest it, the peacock's bursts into eyes that mark your target and those around it, and the Phoenix's arrow lands last.",
                Id = Art.Gear.DemoGear.PhoenixArtId, FinisherGlyph = Glyph.Formation,
                DisplayName = "The Four Birds", Class = Art.Gear.WeaponClass.Bow,

                // The Phoenix's, through the Golden Cage - a Black Diamond ceiling privilege.
                SignatureOnly = true,
                Flavor = "The Work complete, the birds fly as one.",
                // From the HIGH (2026-10-23): a level shot, then a lob - the bow held raised to the
                // sky while the birds gather, and drawn down from there onto the target.
                Basics = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                    new AttackStep { Name = "Lob", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.LobDraw, LobArc = Core.Tuning.Bow.LobArc },
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f, Motion = AttackMotion.Draw },
                },
                Finisher = new AttackStep
                {
                    // HEAVY (the user's call, 2026-10-17): the target takes exactly one (Tuning.Formation's
                    // shares); the Phoenix's arrow displaces and flinches through armour.
                    Name = "The Four Birds", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy, Motion = AttackMotion.HighDraw,
                    Knockback = 6f, ChainFalloff = 0.85f,
                    Formation = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Sweeps both arms apart, throwing the halves in a pincer that closes on your target. Leaps to catch them overhead as they come round, where the king and the queen join into one disc, and hurls it down onto the target in a blast that throws back everything around it.",
                Id = Art.Gear.DemoGear.KingAndQueenArtId, FinisherGlyph = Glyph.KingAndQueen,
                DisplayName = "King and Queen", Class = Art.Gear.WeaponClass.Disc,

                // The Aether Dual Discs', through the Geode Stone - a Black Diamond ceiling privilege,
                // same reasoning as Conflagration and the rest above.
                SignatureOnly = true,
                Flavor = "The king and the queen part, meet on the enemy, and come down as one.",
                // Two basics, as every chain has: the red king, the white queen - then the two as one.
                Basics = DiscBasics(new[]
                {
                    Basic("Rubeus", 1.0f, 0.95f, 0.15f, 0f, AttackMotion.Sweep),
                    Basic("Albus",  1.0f, 0.95f, 0.15f, 0f, AttackMotion.Sweep),
                }, DiscMelee.ChopWind, DiscRanged.Pirouette),
                Finisher = new AttackStep
                {
                    // HEAVY: the target takes exactly one (Tuning.KingAndQueen's shares); the blast
                    // is what displaces.
                    Name = "King and Queen", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f,
                    Weight = FinisherWeight.Heavy, Motion = AttackMotion.DiscPincerThrow,
                    Knockback = 8f, ChainFalloff = 0.85f,
                    ChargeSeconds = Core.Tuning.MagnumOpus.GatherSeconds,
                    Pincer = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Lets the blade go to circle once around you, striking every enemy it passes and shoving them back.",
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
                FinisherDescription = "Cuts down, cuts back up, then turns the hilt and drives the pommel into the enemy in front.",
                Id = "flurry", FinisherGlyph = Glyph.Volley, DisplayName = "Flurry",
                Flavor = "Quick cuts that end with the hilt.",
                // Rise is the lead-in: it ends with the blade cocked high over the shoulder, which
                // is where the Flurry's first cut starts.
                Basics = new[]
                {
                    Basic("Cut",  1.0f, 0.85f, 0.25f, 3.0f, AttackMotion.Chop),
                    Basic("Rise", 1.0f, 0.85f, 0.25f, 3.0f, AttackMotion.Rise),
                },
                Finisher = new AttackStep
                {
                    Name = "Flurry",
                    DamageMultiplier = Core.Tuning.Flurry.CutShare * 2f + Core.Tuning.Flurry.PommelShare,
                    IntervalMultiplier = 1f,
                    Weight = FinisherWeight.Light,
                    Motion = AttackMotion.Flurry, PommelFlurry = true,
                    // The cuts' shape; the pommel narrows and shortens it (Tuning.Flurry).
                    ArcDot = 0.3f, Knockback = 3f, CleavesAll = true, StrikeWidth = 1.5f,
                },
            },
            new Moveset
            {
                FinisherDescription = "Draws the blade back and thrusts it far out ahead, running one enemy through at long reach and throwing it back.",
                Id = "lunge", FinisherGlyph = Glyph.Spike, DisplayName = "Piercing Lunge",
                Flavor = "Narrow and long.",
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
                FinisherDescription = "Brings the blade down into the ground; the impact throws everything close by out past its edge.",
                Id = "hammer", FinisherGlyph = Glyph.Burst, DisplayName = "Sunder",
                Flavor = "Slow and heavy.",
                // The Ascent pair (the dimensional basics): an underhand rising cut, then a pivot
                // that ends with the blade raised OVERHEAD - where the Sunder Slam starts.
                Basics = new[]
                {
                    Basic("Smash",  1.5f, 1.3f, 0.2f, 5f, AttackMotion.AscentRise),
                    Basic("Heave",  1.5f, 1.3f, 0.2f, 5f, AttackMotion.AscentPivot),
                    Basic("Crush",  1.8f, 1.4f, 0.2f, 6f, AttackMotion.AscentRise),
                },
                Finisher = new AttackStep
                {
                    Name = "Shatter", DamageMultiplier = 6.5f, IntervalMultiplier = 2.5f, Motion = AttackMotion.SunderSlam,
                    Weight = FinisherWeight.Heavy,
                    ArcDot = -0.5f, Knockback = 12f, RangeBonus = 0.8f, CleavesAll = true,
                    // The ground carries this one, not the blade.
                    AreaOfEffect = true,
                    // The knockback above only decides a throw into a chasm; the push is a slide.
                    PushesOut = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Spins as lines of wind spiral in around you, and every enemy on screen is dragged in toward you - hard from up close, gentler from range. It deals little damage.",
                // Shown as Vortex since 2026-10-14 (the user's call); the id stays "undertow" so a
                // relic that rolled it keeps the art.
                Id = "undertow", FinisherGlyph = Glyph.Vortex, DisplayName = "Vortex",
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
                FinisherDescription = "Raises the blade overhead and holds it there while you keep moving, then drives it down in a wide cone wherever you are facing when it falls.",
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
                    // Cast: the throw out of the Wind lead-in's swung-back end (AttackMotion.Cast).
                    Knockback = 2.5f, Motion = AttackMotion.Cast,
                    ThrowsWeapon = true, ThrowDistance = 7.5f,
                },
            },
            new Moveset
            {
                FinisherDescription = "Spins up off the ground and leaps off the screen for a second toward the aim, up to five units - untouchable, and still steered by moving. " +
                                      "Then drops onto the marked circle and drives the blade into the floor: the full blow lands directly beneath you, less of it toward the rim. " +
                                      "You land winded, taking half again as much damage for two seconds.",
                Id = "meteor", FinisherGlyph = Glyph.Comet, DisplayName = "Falling Star",
                Flavor = "Turning swings that end with the ground itself.",
                // The Cyclone pair (the dimensional basics): a flat cut from behind the hip, then a
                // full turn that ends crouched LOW-FORWARD - where the takeoff (StarLeap) starts.
                Basics = new[]
                {
                    Basic("Slash",  1.0f, 0.95f, 0.25f, 3.5f, AttackMotion.CycloneCut),
                    Basic("Whirl",  1.0f, 0.95f, 0.25f, 3.5f, AttackMotion.CycloneSpin),
                    Basic("Vault",  1.3f, 1.05f, 0.20f, 4.5f, AttackMotion.CycloneCut),
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
                    Motion = AttackMotion.SunderSlam, SpinLeap = true, LeapDistance = Core.Tuning.FallingStar.LeapDistance,
                    AreaOfEffect = true, LeapSeconds = 1f, EdgeDamageFraction = 0.3f,
                    ExposedSeconds = 2f, ExposedMultiplier = 1.5f,
                },
            },
            new Moveset
            {
                FinisherDescription = "Pulls the blade back to the hip, then lunges with it - body and blade together - straight through every enemy in the way, running each one through.",
                Id = "impale", FinisherGlyph = Glyph.Impale, DisplayName = "Impale",
                Flavor = "A cut and a backhand, then the whole body behind the point.",
                // The Return pair (the dimensional basics): a chop out of a turned-away wind-up,
                // then a backhand that brings the blade down level into the GUARD - the grip
                // Impale's draw starts from.
                Basics = new[]
                {
                    Basic("Cut",      1.0f, 0.9f, 0.30f, 3.0f, AttackMotion.ReturnChop),
                    Basic("Backhand", 1.0f, 0.9f, 0.30f, 3.0f, AttackMotion.ReturnSweep),
                    Basic("Cleave",   1.2f, 1.0f, 0.30f, 3.0f, AttackMotion.ReturnChop),
                },
                // LIGHT: the wind-up is the draw (AttackMotion.Lunge animates it over the timing
                // bar), then the player dashes LungeDistance along the aim - THROUGH the crowd
                // (the user's call, 2026-10-14: 4 units, any direction) - and every body on the
                // path is run through as the lunge ends.
                Finisher = new AttackStep
                {
                    Name = "Impale", DamageMultiplier = 3f, IntervalMultiplier = 1f, Motion = AttackMotion.Lunge,
                    Weight = FinisherWeight.Light,
                    ArcDot = 0.8f, Knockback = 3f, RangeBonus = 0.3f, CleavesAll = true,
                    StrikeWidth = 0.7f,   // a thrust is a point, not a sweep
                    LungeDistance = 4f, PiercesPath = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Points the blade and throws a hooked chain ahead. The first enemy it catches is dragged back to you. If it catches an elite, a boss, a wall or a pillar, you are pulled there instead.",
                Id = "grappling_hook", FinisherGlyph = Glyph.Thrown, DisplayName = "Grappling Hook",
                Flavor = "A cut, a thrust, and a chain on the end of it.",
                Basics = new[]
                {
                    Basic("Cut",    1.0f, 0.9f, 0.30f, 3.0f, AttackMotion.Chop),
                    // The lead-in: a Thrust ends in the guard the hook's thrust starts from.
                    Basic("Thrust", 1.0f, 0.9f, 0.60f, 2.0f, AttackMotion.Thrust),
                    Basic("Skewer", 1.2f, 1.0f, 0.65f, 2.5f, AttackMotion.Thrust),
                },
                // LIGHT (the user's call, 2026-10-14): a pull and a reposition in one art, so it
                // pays a Light's damage, rolled once and dealt to whatever body the hook catches.
                // It moves bodies itself (the reel), not through DamageInfo.Displaces, so it needs
                // no exception to the weight table. A miss is spent.
                Finisher = new AttackStep
                {
                    Name = "Grappling Hook", DamageMultiplier = 3f, IntervalMultiplier = 1f,
                    Motion = AttackMotion.Thrust, Weight = FinisherWeight.Light,
                    ArcDot = 0.8f, StrikeWidth = 0.4f,
                    Grapples = true,
                },
            },
            new Moveset
            {
                FinisherDescription = "Holds the blade high behind the shoulder, back half-turned, turning aside every attack that reaches you while it is raised. " +
                                      "If anything was turned aside, steps in along the aim, turning a full circle, and cuts across in front. If nothing was, turns back without a swing.",
                // The id was the old Impale's (a Heavy thrust with no guard); a relic that rolled
                // it now carries Riposte, the move its id and basics always named.
                Id = "riposte", FinisherGlyph = Glyph.Cross, DisplayName = "Riposte",
                Flavor = "Measured openers, then a guard that answers.",
                // The Crescent pair (the dimensional basics): its backhand ends HIGH-BACK, turned
                // away - the hanging guard Riposte holds.
                Basics = new[]
                {
                    Basic("Parry",  0.6f, 0.75f, 0.5f, 1.2f, AttackMotion.CrescentCut),
                    Basic("Feint",  0.8f, 0.75f, 0.5f, 1.2f, AttackMotion.CrescentRise),
                    Basic("Counter",1.2f, 0.9f, 0.45f, 3.5f, AttackMotion.CrescentCut),
                },
                // MEDIUM, and the swords' only defensive art. The guard IS the timing bar's
                // wind-up (Guards): the counter's first frame is the hanging guard, held for the
                // Medium bar (0.5s), and everything that asks TryParry in that time is parried.
                // The answer is earned (the user's calls, 2026-10-14): a parry and the player
                // steps in along the aim turning a full circle and cuts (RiposteCounter); none,
                // and they turn back without a swing - the art deals nothing.
                Finisher = new AttackStep
                {
                    Name = "Riposte", DamageMultiplier = 5f, IntervalMultiplier = 1.5f, Motion = AttackMotion.RiposteTurnabout,
                    Weight = FinisherWeight.Medium,
                    ArcDot = 0.8f, Knockback = 3.5f, RangeBonus = 0.3f,
                    StrikeWidth = 0.7f,
                    Guards = true,
                },
            },
        };
    }
}
