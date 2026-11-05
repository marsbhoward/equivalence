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
        //
        // ELEMENTAL POWER GROWS THEM (the user's call, 2026-10-12): every BONUS is multiplied by
        // the character's Elemental Power (gear and the board - never the ledger's release scale,
        // which Twin Spark sets below 1), a PENALTY never is. Each override passes its bonuses
        // through Grown. Investing in the element is investing in all of it, not only the release.
        public virtual float DamagePoints => 0f;
        public virtual float AttackSpeedPoints => 0f;
        public virtual float MoveSpeedPoints => 0f;
        public virtual float CritDamagePoints => 0f;
        /// <summary>Accuracy points added to the stat block's, after its curve (Air's streak).</summary>
        public virtual float AccuracyPoints => 0f;

        /// <summary>The character's Elemental Power as a multiplier, supplied by the game (1 when
        /// unset) - what <see cref="Grown"/> scales a standing bonus by.</summary>
        public System.Func<float> PowerScale;

        /// <summary>A standing bonus grown by Elemental Power; a penalty passes through as it is.</summary>
        protected float Grown(float points) => points > 0f ? points * (PowerScale?.Invoke() ?? 1f) : points;

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

        /// <summary>Concentrate: set by the player round a release from a FULL meter, 1 otherwise
        /// (a repeat fires at the ordinary reach).</summary>
        [System.NonSerialized] public float ReleaseRadiusMul = 1f;

        /// <summary>Area on a release's own radii, with Concentrate's reach.</summary>
        protected float ReleaseAreaScale => AreaScale * Mathf.Max(1f, ReleaseRadiusMul);

        /// <summary>
        /// WHERE A BLAST LANDS (RELEASES.md "The bow at range"): round the player - and with a bow,
        /// round a point a step short of the nearest enemy within <see cref="Tuning.Releases.BowBlastReach"/>,
        /// where a blade would stand, so a bow never closes in for its crowd release and catches what a
        /// blade's would. Read once at the press; <paramref name="feet"/> is where its picture is drawn
        /// (on the floor, as a body's shadow is).
        /// </summary>
        protected Vector2 BlastCentre(out Vector2 feet)
        {
            if (Player != null && Player.Weapon == Art.Gear.WeaponClass.Bow)
            {
                Vector2 me = transform.position;
                Health best = null;
                float bd = float.MaxValue;
                foreach (var col in Physics2D.OverlapCircleAll(me, Tuning.Releases.BowBlastReach))
                {
                    if (col == null || col.gameObject == gameObject) continue;
                    var hp = col.GetComponent<Health>();
                    if (hp == null || hp.IsDead || hp.GetComponent<PlayerController>() != null) continue;
                    float d = ((Vector2)hp.transform.position - me).sqrMagnitude;
                    if (d < bd) { bd = d; best = hp; }
                }
                if (best != null)
                {
                    Vector2 at = Vector2.MoveTowards(best.transform.position, me, Tuning.Releases.BowBlastShort);
                    if ((at - me).sqrMagnitude > 1e-4f)
                    {
                        // Drawn on the floor as the player's own blast is: the shadow's offset from the body.
                        feet = at + (ReleaseCanvas.Feet(transform) - me);
                        return at;
                    }
                }
            }
            feet = ReleaseCanvas.Feet(transform);
            return transform.position;
        }

        // ---- event hooks ----
        public virtual void OnAttackStarted() { }
        public virtual void OnHitLanded(Health target, DamageInfo info) { }
        public virtual void OnAttackMissed() { }
        /// <summary>A parry landed - the window met a hit, or a ward turned one.</summary>
        public virtual void OnParried() { }
        public virtual void OnMoved(bool moving) { }

        /// <summary>True when the ability key would actually do something.</summary>
        public virtual bool CanRelease => false;
        public virtual void Release() { }

        // ---- THE RELEASE ARSENAL (2026-11-05, RELEASES.md) ----

        /// <summary>The run's two releases: a TAP of the button fires <see cref="TapKind"/>, a HOLD
        /// fires <see cref="HoldKind"/> when <see cref="HasHold"/>. Set in BuildPlayer from the board
        /// (BoardState.TapRelease/HoldRelease) and kept with the run (RunSnapshot).</summary>
        public ReleaseKind TapKind = ReleaseKind.Blast, HoldKind = ReleaseKind.Blast;
        public bool HasHold;

        /// <summary>The run's Guard, Mobility and Combo (Player.ReleaseArsenal), attached on first use.</summary>
        public ReleaseArsenal Arsenal
        {
            get
            {
                if (_arsenal == null) _arsenal = GetComponent<ReleaseArsenal>();
                if (_arsenal == null) _arsenal = gameObject.AddComponent<ReleaseArsenal>();
                return _arsenal;
            }
        }
        ReleaseArsenal _arsenal;

        /// <summary>The kind fired last - what a repeat (Twin Spark, Wellspring) fires again.</summary>
        public ReleaseKind LastKind { get; private set; } = ReleaseKind.Blast;

        /// <summary>Whether a kind would fire now. Blast and Buff are the element's own two releases
        /// (<see cref="CanFireOwn"/>); Guard, Mobility and Combo are the arsenal's, on the shared meter
        /// (<see cref="Fill01"/>).</summary>
        public bool CanFire(ReleaseKind kind)
        {
            if (!ReleaseInfo.Built(Element, kind)) return false;
            return kind switch
            {
                ReleaseKind.Blast or ReleaseKind.Buff => CanFireOwn(kind),
                _ => Arsenal.CanFire(this, kind),
            };
        }

        /// <summary>Blast and Buff: the element's own releases, from a full meter by default
        /// (<see cref="CanRelease"/>).</summary>
        protected virtual bool CanFireOwn(ReleaseKind kind) => CanRelease;

        /// <summary>Fire a kind. Blast and Buff set the flag the element's Release branches on, then
        /// release - so everything that already wraps a release (repeats, the ledger, principles) still
        /// applies; the rest go to the arsenal.</summary>
        public void Fire(ReleaseKind kind)
        {
            if (!CanFire(kind)) return;
            LastKind = kind;
            if (kind == ReleaseKind.Blast || kind == ReleaseKind.Buff) FireOwn(kind);
            else Arsenal.Fire(this, kind);
        }

        /// <summary>Which of the element's two built releases a kind is: true for the one that was its
        /// SECOND ability (Fire's Ignite is its Buff, Air's Squall its Blast).</summary>
        protected virtual bool SecondFor(ReleaseKind kind) => false;

        protected virtual void FireOwn(ReleaseKind kind)
        {
            UseSecondAbility = SecondFor(kind);
            Release();
        }

        /// <summary>
        /// The last release once more, spending nothing (Twin Spark, Wellspring): the element's own
        /// <see cref="ReleaseAgain"/> for a Blast or a Buff, another charge for a Guard. Only full-meter
        /// kinds reach the ledger (ReleaseInfo.FullMeter), so a combo or a move is never repeated.
        /// </summary>
        public void Repeat()
        {
            if (LastKind == ReleaseKind.Guard) Arsenal.RepeatGuard();
            else if (LastKind == ReleaseKind.Blast || LastKind == ReleaseKind.Buff) ReleaseAgain();
        }

        /// <summary>
        /// Spend a share of the meter on a Guard, a move or a combo strike. Its own resource each - the
        /// default takes it as a spill; Fire overrides it so a spend is never a loss to Phlogiston.
        /// </summary>
        public virtual void Spend(float share01) => Spill(share01);

        /// <summary>Either of the run's releases would fire now - what the seal and the touch button light.</summary>
        public bool CanFireAny => CanFire(TapKind) || (HasHold && CanFire(HoldKind));

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
        /// <summary><see cref="Scale"/> for the arsenal's own releases.</summary>
        public float ReleaseScaleNow => Scale;

        /// <summary>
        /// Supplied by the game: mastery's per-element growth node composed with gear's
        /// ElementGrowth, as one multiplier on whatever field actually represents "building
        /// faster" for THIS element's own shape of resource.
        ///
        /// A literal rate for every element: Fire's and Water's meter per swing, the
        /// charge-per-second implied by Earth's ChargeSeconds, Air's MomentumPerSecond.
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

        // ---- the seal (UI.ElementSeal) ----

        /// <summary>The run's releases, named above the seal: the tap's, then the hold's.</summary>
        public virtual string ReleaseName
            => HasHold ? $"{ReleaseInfo.Name(Element, TapKind)} / {ReleaseInfo.Name(Element, HoldKind)}"
                       : ReleaseInfo.Name(Element, TapKind);

        /// <summary>A few words of what the meter is doing - a rung reached, a buff's clock, a
        /// stance. Null for nothing to say.</summary>
        public virtual string SealTag => null;

        /// <summary>What the seal shows: a Guard's charges or a combo's next strike outrank the
        /// element's own tag while they last.</summary>
        public string Tag => _arsenal != null && _arsenal.Tag(this) is { } t ? t : SealTag;

        /// <summary>Small pips along the top of the seal's rim, for a second count beside the
        /// meter (Earth's aftershocks, Air's streak). Zero draws none.</summary>
        public virtual int RimPips => 0;
        public virtual int RimPipsLit => 0;
    }
}
