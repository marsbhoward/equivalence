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
        /// Meter per connecting hit. 0.09 is ~11 hits to a full meter, up from 8.
        ///
        /// Slowed because the tier-3 burst is the strongest single button water has and it was
        /// arriving inside one ordinary fight. Water is meant to be the element that asks you to
        /// carry something dangerous for a while; at 8 hits there was barely a carry.
        /// </summary>
        public float GainPerHit = T.GainPerHit;

        public float DecayPerSecond = T.DecayPerSecond;
        public float Radius = T.Radius;

        /// <summary>
        /// Total damage the tier-3 burst has to give, in HIT UNITS (multiples of the player's own
        /// basic hit, Tuning.Elements), SHARED between everything it catches.
        ///
        /// A flat per-target number meant the burst scaled with the size of the crowd - the same
        /// button was a single-target nuke AND a screen clear, and there was never a reason to
        /// spend it on one enemy. As a shared pool it becomes a positioning decision: catch one
        /// target and it lands whole, catch six and each takes a fraction.
        /// </summary>
        public float BurstHitUnits = T.BurstHitUnits;

        /// <summary>
        /// How the pool splits. 1 is a straight even divide; below 1 softens it so a crowd still
        /// takes more in TOTAL than a single target does - otherwise the burst would be strictly
        /// worse the more enemies it caught, and stepping AWAY from a pack to fire it would be
        /// the correct play. At 0.8: one target takes 100%, two 57% each, four 33%, eight 19%.
        /// </summary>
        [Range(0.4f, 1f)] public float BurstSplitFalloff = T.BurstSplitFalloff;

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
        public float SurgeRemaining => _surgeRemaining;

        public override ElementType Element => ElementType.Water;
        public override float DamagePoints => T.DamagePoints;
        public override float AttackSpeedPoints
            => Surging ? Mathf.Max(0f, SurgePoints - (Ledger != null ? Ledger.SurgePointsLost : 0f)) : 0f;

        void Update()
        {
            if (Meter > 0f && Tier < 3)
                Meter = Mathf.Max(0f, Meter - DecayPerSecond * Decay * Time.deltaTime);

            if (_surgeRemaining > 0f)
            {
                _surgeRemaining -= Time.deltaTime;

                // A buff with no tell is a buff the player cannot plan around, and this one is
                // short enough that its END matters as much as its start.
                if (_surgeRemaining <= 0f)
                {
                    _surgeRemaining = 0f;
                    Spr.Pulse(transform, 0.8f, new Color(0.4f, 0.6f, 0.75f, 0.6f), 0.25f, true, 0.5f);
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

        public override void OnHitLanded(Health target, DamageInfo info)
        {
            int before = Tier;

            float gain = GainPerHit * Gain * (Player != null ? Player.HitMeterScale : 1f);

            var status = target != null ? target.GetComponent<StatusEffects>() : null;
            if (status != null && status.Soaked && (Ledger == null || Ledger.SoakedDoubleWorks)) gain *= SoakedGainMultiplier;   // Ebb stops it

            Meter = Mathf.Clamp01(Meter + gain);

            // Reaching a tier is the only moment water's meter means anything - it is when a new
            // release becomes available. Brighter each step, brightest at the full burst.
            if (Tier <= before) return;
            var tint = Tier switch
            {
                1 => new Color(0.35f, 0.65f, 1f),
                2 => new Color(0.45f, 0.85f, 1f),
                _ => Color.white,
            };
            Spr.Pulse(transform, 0.6f + Tier * 0.25f, tint, 0.25f + Tier * 0.06f, true, 1.2f);
        }

        public override bool CanRelease => Tier >= 1;

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
            Cash(LastTier, repeat: true);
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

        void Cash(int tier, bool repeat = false)
        {
            float radius = Radius * AreaScale;
            var hits = Physics2D.OverlapCircleAll(transform.position, radius);

            // Tier 3 only: the big hit, as a pool shared between everything caught. Dealt BEFORE the
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
                    float share = BurstHitUnits * HitUnit * Scale /
                                  Mathf.Pow(caught.Count, Mathf.Clamp(BurstSplitFalloff, 0.4f, 1f));
                    foreach (var hp in caught)
                    {
                        var to = ((Vector2)hp.transform.position - (Vector2)transform.position).normalized;
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
            if (tier >= 2)
            {
                float seconds = (tier == 3 ? SurgeSecondsTier3 : SurgeSecondsTier2)
                              + (Ledger != null ? Ledger.SurgeExtraSeconds : 0f);
                _surgeRemaining = repeat ? Mathf.Max(0f, _surgeRemaining) + seconds * RepeatStrength : seconds;
                Spr.Pulse(transform, 1.1f, new Color(0.5f, 0.85f, 1f), 0.35f, true, 1.4f);
            }

            var c = ElementInfo.Tint(ElementType.Water);
            Spr.Flash(transform.position, radius, c, tier == 3 ? 0.5f : 0.3f);
            if (tier == 3) Spr.Flash(transform.position, radius * 0.6f, Color.white, 0.35f);
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
                if (Surging) return $"SURGE  +{SurgePoints:0}% attack speed  {_surgeRemaining:0.0}s";

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
