using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// One element = one resource rhythm. The design brief is that these must not be shared
    /// stats with a different skin: each subclass changes *how the meter is built and spent*,
    /// not just the numbers. Everything the player controller needs is expressed through
    /// these hooks and modifier properties.
    /// </summary>
    public abstract class ElementalResource : MonoBehaviour
    {
        protected PlayerController Player;
        public virtual void Bind(PlayerController player) => Player = player;

        public abstract ElementType Element { get; }

        // ---- combat modifiers the player controller reads every frame ----
        //
        // HEAD STARTS, in stat POINTS. They join the character's own points (gear and the board)
        // BEFORE the character curve bends them (PlayerController's *PointsNow), so an element
        // reaches a threshold sooner, never past it, and a penalty means needing more gear for the
        // same cap rather than a lower one. Read live, because most of them change mid-fight - heat
        // stacks, momentum, a surge. See Tuning.Elements.
        public virtual float DamagePoints => 0f;
        public virtual float AttackSpeedPoints => 0f;
        public virtual float MoveSpeedPoints => 0f;
        public virtual float CritDamagePoints => 0f;

        public virtual float AttackRangeBonus => 0f;

        /// <summary>Lifesteal the element adds to the one lifesteal pool (StatCurves.Lifesteal) -
        /// Air's Gust. Joining the pool rather than healing on its own keeps it under the same caps.</summary>
        public virtual float LifestealBonus => 0f;

        /// <summary>Crit chance the element adds to the one crit pool (StatCurves.Crit).</summary>
        public virtual float CritChance => 0f;
        public virtual float PoiseBonus => 0f;
        public virtual float DamageReduction => 0f;

        /// <summary>The player's own basic hit right now - what a release's damage is counted in
        /// (Tuning.Elements). Falls back to the base hit with no player bound.</summary>
        protected float HitUnit => Player != null ? Player.HitUnit : Tuning.Player.BaseDamage;

        /// <summary>Area (StatKind.AoeRadius) on a release's radii, like every other area attack.</summary>
        protected float AreaScale => Player != null && Player.Stats != null ? Player.AreaScaleNow : 1f;

        // ---- event hooks ----
        public virtual void OnAttackStarted() { }
        public virtual void OnHitLanded(Health target, DamageInfo info) { }
        public virtual void OnAttackMissed() { }
        public virtual void OnMoved(bool moving) { }

        /// <summary>True when the ability key would actually do something.</summary>
        public virtual bool CanRelease => false;
        public virtual void Release() { }

        /// <summary>
        /// Whether <see cref="Release"/> fires this element's SECOND ability instead of its first.
        /// One ability is active at a time, on the one release button: the board's Rebis (at its
        /// centre, reachable by any build, locking nothing) unlocks the option and the player picks
        /// it there, defaulting to the first.
        ///
        /// Set once in BuildPlayer from <c>BoardState.UsesSecondAbility</c> - mastery is spent in
        /// the main menu and never mid-run, so there is nothing to re-read. Each subclass branches
        /// on it inside its own CanRelease/Release, which is what lets everything that already
        /// wraps a release (Twin Spark's repeats, the ledger's gates, principle hooks) apply to
        /// whichever ability is chosen without knowing there are two.
        /// </summary>
        public bool UseSecondAbility;

        /// <summary>
        /// Scales everything a release does, supplied by the run's boons and costs. Twin Spark
        /// sets it below 1 and fires twice; nothing else touches it.
        ///
        /// A lambda rather than a field so the resource never owns the value - the same shape
        /// PlayerController uses for DamageDealtMultiplier, and it means a resource built before
        /// the ledger exists still behaves.
        /// </summary>
        public System.Func<float> ReleaseScale;
        protected float Scale => ReleaseScale?.Invoke() ?? 1f;

        /// <summary>
        /// Supplied by the game: mastery's per-element growth node composed with gear's
        /// ElementGrowth, as one multiplier on whatever field actually represents "building
        /// faster" for THIS element's own shape of resource.
        ///
        /// For water/earth/air that is a literal rate - GainPerHit, the charge-per-second implied
        /// by ChargeSeconds, MomentumPerSecond. Fire has no such rate to speed up: its heat is a
        /// deliberately discrete stack gained once per landed swing, and multi-stacking a single
        /// swing would cheapen the "sustained aggression, one stack per hit" identity that rule
        /// protects. Fire instead consumes this as a multiplier on StackLifetime (via
        /// FireStackLife, its own already-designed mastery node) - a stack that survives longer
        /// is functionally "growing faster" in the only sense that makes sense for something
        /// gained in fixed, discrete steps: it climbs to and holds five stacks more easily,
        /// exactly what a faster rate buys everyone else.
        /// </summary>
        public float GainRateMultiplier = 1f;

        /// <summary>The run's ledger, when there is one - what the exchange's element entries are
        /// asked of. Null-guarded everywhere: no ledger is the ordinary case, not an error.</summary>
        protected Exchange.RunEffects Ledger => Player != null ? Player.Effects : null;

        /// <summary>How fast the meter builds right now: the character's growth (gear and the
        /// board, <see cref="GainRateMultiplier"/>) times the run's (the ledger's Element Growth;
        /// nothing through Barren's seconds).</summary>
        protected float Gain => GainRateMultiplier * (Ledger != null ? Ledger.GainMul : 1f);

        /// <summary>How fast the meter fades or decays right now (Leaky Vessel; Sealed Vessel
        /// stops it).</summary>
        protected float Decay => Ledger != null ? Ledger.DecayMul : 1f;

        /// <summary>The share of full strength a REPEAT fires at (Twin Spark, Wellspring) - 1 for
        /// an ordinary release. Already inside <see cref="Scale"/>; read on its own only by what a
        /// repeat extends rather than scales (a window's length).</summary>
        protected float RepeatStrength => Ledger != null ? Ledger.RepeatStrength : 1f;

        /// <summary>
        /// The release once more without spending anything - Twin Spark's and Wellspring's repeat,
        /// at whatever strength the ledger has set (<see cref="Scale"/>). Each element repeats
        /// its own last release in its own terms; the default does nothing.
        /// </summary>
        public virtual void ReleaseAgain() { }

        /// <summary>Lose a share of the meter - Cracked Vessel's spill. Default does nothing.</summary>
        public virtual void Spill(float fraction01) { }

        /// <summary>
        /// Put a fraction of a spent meter back. Overflow's refund.
        ///
        /// Each element implements it against its OWN resource - stacks, meter, charge, momentum -
        /// because there is no shared representation to restore. Default does nothing, so an
        /// element that has not implemented it simply never refunds rather than misbehaving.
        /// </summary>
        public virtual void Refund(float fraction01) { }

        // ---- HUD ----
        /// <summary>Primary meter, 0..1. Drawn as a bar unless PipCount > 0.</summary>
        public abstract float Fill01 { get; }
        /// <summary>Non-zero switches the HUD to discrete pips (fire's stacking icons).</summary>
        public virtual int PipCount => 0;
        public virtual int PipsFilled => 0;
        /// <summary>Tier boundaries drawn as notches on the bar (water's 1/3, 2/3).</summary>
        public virtual float[] Notches => System.Array.Empty<float>();

        /// <summary>
        /// One icon per pip. When supplied, the HUD draws these glyphs instead of plain bars, so
        /// each step of a meter can say WHAT it unlocked rather than just that something did.
        /// </summary>
        public virtual Combat.Glyph[] PipGlyphs => null;

        /// <summary>
        /// Draw pips only once they are reached, instead of showing empty slots waiting to fill.
        ///
        /// The difference is what the meter is FOR. Fire's stacks are a quantity you are trying
        /// to grow, so the empty slots are the goal and have to be visible. Water's tiers are a
        /// menu of options that become available - an empty slot there is not progress toward
        /// anything the player can act on, it is just three boxes to decode mid-fight.
        /// </summary>
        public virtual bool PipsAppearWhenReached => false;
        public abstract string StatusLine { get; }
    }
}
