using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.Elements.Water;

namespace Convergence.Player
{
    /// <summary>
    /// WATER - Tiered Meter (pressure / payoff).
    /// Landing hits builds a meter across three tiers:
    ///   1/3 -> debuff enemies    2/3 -> surge (attack speed)    3/3 -> both, plus a big hit.
    ///
    /// DESIGN DECISIONS (both were open in the handoff):
    ///  - Release is OPTIONAL at any reached tier. That makes water the decision-maker
    ///    element: bail early for utility, or gamble on holding for the burst.
    ///  - The meter EMPTIES COMPLETELY on release regardless of tier cashed, so an early
    ///    release has a real opportunity cost instead of being free utility on the way up.
    /// </summary>
    public class WaterResource : ElementalResource
    {
        public float Meter;                 // 0..1

        /// <summary>
        /// Meter per connecting SWING (once per swing or throw since 2026-11-05 - it was per body
        /// caught; Tuning.Elements.MeterPerSwing, the same as Fire's). 0.09 is ~11 to a full meter.
        ///
        /// Slowed because the tier-3 burst is the strongest single button water has and it was
        /// arriving inside one ordinary fight. Water is meant to be the element that asks you to
        /// carry something dangerous for a while; at 8 hits there was barely a carry.
        /// </summary>
        public float GainPerHit = T.GainPerHit;

        /// <summary>This swing's meter so far - a later body in it that is soaked tops it up to double.</summary>
        float _swingGain;
        float _missAt = -99f;
        public float Radius = T.Radius;

        /// <summary>
        /// The tier-3 burst on each body it catches, in HIT UNITS (multiples of the player's own basic
        /// hit, Tuning.Elements), split by <see cref="BurstSplitFalloff"/>. It was a pool SHARED between
        /// everything caught, so one button was never both a nuke and a screen clear; since 2026-11-05
        /// that line runs between the kinds instead - Burst is the crowd's, the combo the lone
        /// enemy's (RELEASES.md "Single target and the crowd") - and nothing is shared.
        /// </summary>
        public float BurstHitUnits = T.BurstHitUnits;

        /// <summary>
        /// How the burst splits between what it catches: each body takes it over n^falloff. 0 (since
        /// 2026-11-05) hands every body the whole - Burst is the crowd's kind; 1 would be a straight
        /// even divide.
        /// </summary>
        [Range(0f, 1f)] public float BurstSplitFalloff = T.BurstSplitFalloff;

        public int Tier => Meter >= 0.999f ? 3 : Meter >= 0.66f ? 2 : Meter >= 0.33f ? 1 : 0;

        // ---- SURGE: the timed haste a release grants, in place of the old heal ----
        //
        // Healing was pulled out because it made water the safe pick rather than the pressure
        // pick: the meter you were supposed to gamble with doubled as a heal button, so holding
        // it was never actually risky. Haste keeps the tier-2 cash-out worth taking while
        // pointing it at OFFENCE - and it compounds with the tier-3 burst you gave up to take
        // it, which is the trade the tier design wants you to feel.

        /// <summary>Attack speed points while a surge is running - a head start like every
        /// element's (Tuning.Elements), so it helps reach the attack-speed cap, never past it.</summary>
        public float SurgePoints = T.SurgePoints;

        public float SurgeSecondsTier2 = T.SurgeSecondsTier2;
        public float SurgeSecondsTier3 = T.SurgeSecondsTier3;

        float _surgeRemaining;

        public bool Surging => _surgeRemaining > 0f;

        /// <summary>The surge ends now (Ebb, Low Water's Nigredo: a hit taken ends it).</summary>
        public void EndSurge() => _surgeRemaining = 0f;
        public float SurgeRemaining => _surgeRemaining;

        public override ElementType Element => ElementType.Water;
        public override float DamagePoints => Grown(T.DamagePoints);
        public override float AttackSpeedPoints
            => Surging ? Grown(Mathf.Max(0f, SurgePoints - (Ledger != null ? Ledger.SurgePointsLost : 0f))) : 0f;

        [SerializeField] UndertowRibbons _undertow;

        void Update()
        {
            if (_undertow == null) _undertow = UndertowRibbons.Attach(this);   // Surge's picture
            // No leak (2026-11-05, the user's call): Water carries its meter until a miss or a release.

            if (_surgeRemaining > 0f)
            {
                _surgeRemaining -= Time.deltaTime;

                // A buff with no tell is a buff the player cannot plan around, and this one is
                // short enough that its END matters as much as its start.
                if (_surgeRemaining <= 0f)
                {
                    _surgeRemaining = 0f;
                    TexelFx.Ring(ReleaseCanvas.Feet(transform), Tuning.ReleaseFx.Water, 0.55f, 0.05f, 0.4f, 1.2f);   // closing
                }
            }
        }

        /// <summary>
        /// A hit on a SOAKED enemy is worth double.
        ///
        /// Soak is water's own tier-1 release, so cashing out early stops being pure utility and
        /// becomes an investment: spend the meter to soak, and the next meter builds twice as
        /// fast. That turns the element's central question - utility now or burst later - into a
        /// choice with a real argument on both sides, and it is the only lever a player has on a
        /// single target, where there is no crowd to chain through.
        ///
        /// Deliberately on WATER and not on any one weapon: it is a property of soak.
        /// </summary>
        public float SoakedGainMultiplier = T.SoakedGainMultiplier;

        public override void OnAttackStarted()
        {
            _swingGain = 0f;
            if (Surging) _undertow?.Splash();   // the undertow: a swing throws water off the ribbons
        }

        public override void OnHitLanded(Health target, DamageInfo info)
        {
            int before = Tier;

            // No per-class scale: a disc's bounces are one swing (WeaponClasses.HitMeterScale is gone).
            float gain = GainPerHit * Gain;

            var status = target != null ? target.GetComponent<StatusEffects>() : null;
            if (status != null && status.Soaked && (Ledger == null || Ledger.SoakedDoubleWorks)) gain *= SoakedGainMultiplier;   // Ebb stops it

            // Once per swing: the swing's best body counts - a soaked one tops a dry one up to double.
            float add = Mathf.Max(0f, gain - _swingGain);
            _swingGain = Mathf.Max(_swingGain, gain);
            Meter = Mathf.Clamp01(Meter + add);

            // Reaching a tier is the only moment water's meter means anything - it is when a new
            // release becomes available. Brighter each step, brightest at the full burst.
            if (Tier <= before) return;
            // A ripple out across the floor at the feet (the user's pick, 2026-10-26 - "on the
            // floor"), wider each tier.
            TexelFx.Ring(ReleaseCanvas.Feet(transform), Tuning.ReleaseFx.Water, 0.12f, 0.35f + Tier * 0.12f, 0.4f, 1.2f);
        }

        /// <summary>A miss costs a quarter of a tier - once per MissEvery, so a volley is one miss.
        /// The ledger's fade scales it (Leaky Vessel more, Sealed Vessel nothing).</summary>
        public override void OnAttackMissed()
        {
            if (Meter <= 0f || Time.time - _missAt < T.MissEvery) return;
            _missAt = Time.time;
            Meter = Mathf.Max(0f, Meter - T.MissCost * Decay);
        }

        public override bool CanRelease => Tier >= 1;

        /// <summary>THE ARSENAL (RELEASES.md): the tiers fold into two releases from a full meter -
        /// BURST (the hit and the soak) is Water's Blast, SURGE (the long one) its Buff.</summary>
        protected override bool CanFireOwn(ReleaseKind kind) => Meter >= 0.999f;

        protected override void FireOwn(ReleaseKind kind)
        {
            LastTier = kind == ReleaseKind.Blast ? 3 : 2;
            Cash(LastTier, surgeOnly: kind == ReleaseKind.Buff, burstOnly: kind == ReleaseKind.Blast);
            Meter = 0f;
        }

        public override void Release()
        {
            int tier = Tier;
            if (tier < 1) return;
            LastTier = tier;
            Cash(tier);
            Meter = 0f;   // full empty regardless of tier cashed
        }

        /// <summary>The tier the last release cashed - what a repeat fires again, and what Spring
        /// Tide asks (a surge).</summary>
        public int LastTier { get; private set; }

        /// <summary>The release once more, spending nothing (Twin Spark, Wellspring): the same tier,
        /// at the repeat's strength - a surge runs on by that share of itself.</summary>
        public override void ReleaseAgain()
        {
            if (LastTier < 1) return;
            Cash(LastTier, repeat: true, surgeOnly: LastTier == 2, burstOnly: LastTier == 3);
        }

        /// <summary>Spring Tide: a surge soaks every enemy around the player.</summary>
        public void SoakAround() => SoakAll(Physics2D.OverlapCircleAll(transform.position, Radius * AreaScale));

        float SoakVulnerability => T.SoakVulnerability + (Ledger != null ? Ledger.SoakVulnerabilityAdd : 0f);

        void SoakAll(Collider2D[] hits)
        {
            foreach (var col in hits)
            {
                if (col.gameObject == gameObject) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                // Through the player, so the board's Soak Power and Aqua Fortis reach it.
                if (Player != null) Player.Soak(hp, T.SoakSeconds, SoakVulnerability);
                else StatusEffects.Get(hp.gameObject).ApplySoak(T.SoakSeconds, SoakVulnerability);
            }
        }

        /// <param name="burstOnly">The arsenal's BURST: the hit and the soak, no surge.</param>
        /// <param name="surgeOnly">The arsenal's SURGE: the full-length surge alone.</param>
        void Cash(int tier, bool repeat = false, bool surgeOnly = false, bool burstOnly = false)
        {
            // BURST IN TWO BEATS (the user's pick, 2026-11-05): the weapon's own water comes in first -
            // WELLSPRING for a greatsword, WHIRLPOOL for discs, TIDE VOLLEY for a bow - and the burst,
            // the soak and FOUR SPRINGS land as it reaches the radius.
            if (tier == 3)
            {
                float radius0 = Radius * ReleaseAreaScale;
                // Round the player - with a bow, a step short of the enemy it aims at (BlastCentre), held there.
                var centre = BlastCentre(out var feet0);
                var none = System.Array.Empty<Vector2>();
                float arrives;
                switch (Player != null ? Player.Weapon : Art.Gear.WeaponClass.Greatsword)
                {
                    case Art.Gear.WeaponClass.Disc: WaterWhirl.Play(feet0, radius0, none); arrives = Tuning.ReleaseFx.WhirlSeconds; break;
                    case Art.Gear.WeaponClass.Bow: WaterVolley.Play(feet0, radius0, none); arrives = Tuning.ReleaseFx.VolleySeconds; break;
                    default: WaterSpring.Play(feet0, radius0, none); arrives = Tuning.ReleaseFx.SpringSeconds; break;
                }
                Vector2? at = (centre - (Vector2)transform.position).sqrMagnitude > 1e-4f ? centre : null;
                StartCoroutine(Arrive(arrives, repeat, surgeOnly, burstOnly, ReleaseAreaScale, Scale, at, feet0));
                return;
            }
            Land(tier, ReleaseAreaScale, Scale, repeat, surgeOnly, burstOnly);
        }

        /// <summary>The burst's second beat: area and strength read at the press, as a quake's aftershocks.</summary>
        System.Collections.IEnumerator Arrive(float after, bool repeat, bool surgeOnly, bool burstOnly, float area, float scale,
                                              Vector2? at, Vector2 atFeet)
        {
            yield return new WaitForSeconds(after);
            Land(3, area, scale, repeat, surgeOnly, burstOnly, at, atFeet);
        }

        /// <param name="at">Where the burst lands when not round the player (a bow's BlastCentre), with
        /// <paramref name="atFeet"/> where it is drawn.</param>
        void Land(int tier, float area, float scale, bool repeat, bool surgeOnly, bool burstOnly,
                  Vector2? at = null, Vector2 atFeet = default)
        {
            float radius = Radius * area;
            Vector2 centre = at ?? (Vector2)transform.position;
            var hits = Physics2D.OverlapCircleAll(centre, radius);

            // Tier 3 only: the big hit on everything caught (BurstSplitFalloff). Dealt BEFORE the
            // soak below lands: the soak is for the hits that FOLLOW, and a burst amplified by its
            // own debuff was 35% bigger than anything priced it (Balance.Assay).
            if (tier == 3)
            {
                // Counted BEFORE dealing any damage: killing the first target would otherwise
                // shrink the divisor mid-loop and hand the survivors a bigger share than the
                // ones that died - the burst would pay more for being partly wasted.
                var caught = new List<Health>();
                foreach (var col in hits)
                {
                    if (col.gameObject == gameObject) continue;
                    var hp = col.GetComponent<Health>();
                    if (hp == null || hp.IsDead) continue;
                    caught.Add(hp);
                }

                if (caught.Count > 0)
                {
                    float share = BurstHitUnits * HitUnit * scale /
                                  Mathf.Pow(caught.Count, Mathf.Clamp01(BurstSplitFalloff));
                    foreach (var hp in caught)
                    {
                        var to = ((Vector2)hp.transform.position - centre).normalized;
                        PlayerController.ReleaseHitFrom(gameObject, hp,
                            new DamageInfo(share, ElementType.Water, gameObject) { Knockback = to * T.BurstKnockback });
                    }
                }
            }

            // Tier 1 and 3: debuff every enemy caught in the burst - after the hit, so it sets up the next.
            if (tier == 1 || tier == 3) SoakAll(hits);

            // Tier 2 and 3: SURGE. Refreshed rather than stacked - a player sitting on tier 2
            // could otherwise chain-cast it into permanent haste, which is not a decision. High
            // Tide lengthens it; a repeat runs it on by its own share.
            if (tier >= 2 && !burstOnly)
            {
                float seconds = (tier == 3 || surgeOnly ? SurgeSecondsTier3 : SurgeSecondsTier2)
                              + (Ledger != null ? Ledger.SurgeExtraSeconds : 0f);
                // The undertow round the body is its picture (UndertowRibbons).
                _surgeRemaining = repeat ? Mathf.Max(0f, _surgeRemaining) + seconds * RepeatStrength : seconds;
            }

            // The pictures (the user's picks, 2026-11-05): the burst lands as FOUR SPRINGS, after the
            // weapon's own water (Cash); the old tier-1 soak takes the weapon's form alone. Each splashes
            // every body it soaked.
            if (tier == 1 || tier == 3)
            {
                var bodies = new List<Vector2>();
                foreach (var col in hits)
                    if (col != null && col.gameObject != gameObject && col.GetComponent<Health>() is { IsDead: false } hp)
                        bodies.Add(ReleaseCanvas.Feet(hp.transform));
                var feet = at != null ? atFeet : ReleaseCanvas.Feet(transform);
                if (tier == 3) SigilBurst.Play(feet, radius, bodies);
                else switch (Player != null ? Player.Weapon : Art.Gear.WeaponClass.Greatsword)
                {
                    case Art.Gear.WeaponClass.Greatsword: WaterSpring.Play(feet, radius, bodies); break;
                    case Art.Gear.WeaponClass.Disc: WaterWhirl.Play(feet, radius, bodies); break;
                    case Art.Gear.WeaponClass.Bow: WaterVolley.Play(feet, radius, bodies); break;
                    default: WaterRipple.Play(feet, radius, bodies); break;
                }
            }
        }

        public override void Spill(float fraction01) => Meter = Mathf.Max(0f, Meter - fraction01);

        public override void Refund(float fraction01)
            => Meter = Mathf.Clamp01(Meter + fraction01);

        public override float Fill01 => Meter;

        // ---- HUD: three tier icons that APPEAR as they unlock ----
        //
        // Was a continuous bar with notches at 1/3 and 2/3. That drew the meter as one quantity
        // and left the player to work out which side of an unmarked line the fill had crossed,
        // mid-fight, to know which of three different abilities the button would fire. The tiers
        // are not really a quantity at all - they are three distinct options that switch on - so
        // they read far better as three things that either exist or do not.
        //
        // The glyphs say what each one DOES: a spiral for the soak, a volley for the attack-speed
        // surge, a burst for the payoff.
        public override int PipCount => 3;
        public override int PipsFilled => Tier;
        public override bool PipsAppearWhenReached => true;

        
        public override string SealTag => Surging ? $"SURGE  {_surgeRemaining:0.0}s" : null;
        public override Combat.Glyph[] PipGlyphs => TierGlyphs;

        static readonly Combat.Glyph[] TierGlyphs =
        {
            Combat.Glyph.Vortex,   // 1 - soak: slow and vulnerable
            Combat.Glyph.Volley,   // 2 - surge: attack speed
            Combat.Glyph.Burst,    // 3 - the burst
        };

        public override string StatusLine
        {
            get
            {
                // The surge outranks the meter while it runs: it is on a clock the player has to
                // spend, and the meter is not going anywhere.
                if (Surging) return $"SURGE  +{AttackSpeedPoints:0}% attack speed  {_surgeRemaining:0.0}s";

                return Tier switch
                {
                    0 => "building pressure...",
                    1 => "TIER 1 ready - [RMB/E] debuff",
                    2 => "TIER 2 ready - [RMB/E] surge (attack speed)",
                    _ => "TIER 3 FULL - [RMB/E] debuff + surge + burst",
                };
            }
        }
    }
}
