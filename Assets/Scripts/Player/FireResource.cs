using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.Elements.Fire;

namespace Convergence.Player
{
    /// <summary>
    /// FIRE - stacking heat (sustained aggression).
    ///
    /// Landing a swing banks a heat stack, max five, each fading on its own timer, oldest first -
    /// sustained pressure holds the ladder up and backing off lets it drain. Every stack is a little
    /// more damage (a head start, Tuning.Elements.Fire); the rungs add:
    ///
    ///     1  FUEL   a swing landing on a BURNING enemy banks two stacks instead of one
    ///     2         attack speed
    ///     3  STOKE  heat fades half as fast
    ///     4         every hit splashes onto one more nearby enemy
    ///     5         a release: ERUPT (burning ground), or IGNITE if chosen on the board
    ///
    /// BURN IS NOT PASSIVE (2026-10-05). It used to ride every hit from the first stack: merged by
    /// max and never decaying while the hits kept coming, it settled at 35% of Fire's biggest hit
    /// per second and added about 40% to Fire's damage on its own. It lives in the releases now -
    /// Erupt's pools set whatever stands in them burning, and Ignite is a window of it - and Fuel is
    /// how burn feeds back into the ladder.
    /// </summary>
    public class FireResource : ElementalResource
    {
        public const int MaxStacks = T.MaxStacks;
        public float StackLifetime = T.StackLifetime;

        readonly List<float> _stacks = new();   // remaining lifetime per stack, oldest first
        bool _stackedThisSwing;
        float _igniteRemaining;

        public int Stacks => _stacks.Count;

        /// <summary>True while an Ignite window is open - every landed hit burns.</summary>
        public bool Igniting => _igniteRemaining > 0f;
        public float IgniteRemaining => _igniteRemaining;

        public override ElementType Element => ElementType.Fire;

        public override float DamagePoints => Stacks * (Ledger != null ? Ledger.HeatPoints(T.StackDamagePoints) : T.StackDamagePoints);
        public override float AttackSpeedPoints => Stacks >= 2 ? T.HastePoints : 0f;

        void Update()
        {
            float dt = Time.deltaTime;

            // STOKE: from the third rung up, heat fades half as fast. The ledger's fade on top
            // (Leaky Vessel faster, Sealed Vessel not at all), and its floor under it: Banked
            // Embers keeps that many stacks from fading out - a release still spends them.
            float fade = (Stacks >= 3 ? dt / T.StokeLifetimeMul : dt) * Decay;
            int floor = Ledger != null ? Ledger.HeatFloor : 0;
            for (int i = _stacks.Count - 1; i >= 0; i--)
            {
                _stacks[i] -= fade;
                if (_stacks[i] > 0f) continue;
                if (_stacks.Count <= floor) { _stacks[i] = 0.01f; continue; }
                _stacks.RemoveAt(i);
            }

            if (_igniteRemaining > 0f)
            {
                _igniteRemaining -= dt;
                // The window's END matters as much as its start - the same tell Water's surge has.
                if (_igniteRemaining <= 0f)
                {
                    _igniteRemaining = 0f;
                    Spr.Pulse(transform, 0.8f, new Color(1f, 0.45f, 0.2f, 0.6f), 0.25f, true, 0.5f);
                }
            }
        }

        /// <summary>Each swing may bank at most one batch of heat; the flag resets here.</summary>
        public override void OnAttackStarted() => _stackedThisSwing = false;

        public override void OnHitLanded(Health target, DamageInfo info)
        {
            // Heat comes from ATTACKS that connect, not from bodies caught in them. Counting per
            // target let one cleaving finisher through a crowd jump straight to full stacks,
            // which is not the sustained-aggression loop fire is supposed to reward.
            // THROWN hits build no heat. Fire is the aggression element, and a weapon that can
            // attack safely from range would otherwise hand it the whole ladder for nothing. On
            // a thrown class this makes heat a decision: you have to fight at the range your
            // weapon is worst at to get it.
            // Nothing builds through Barren's seconds (the run's gain at zero).
            if (!info.Thrown && !_stackedThisSwing && Gain > 0f)
            {
                // FUEL, from the first rung: a burning enemy - Erupt's ground, an Ignite window, any
                // burn at all - is worth two stacks. Wet Ash puts it out.
                bool fuel = Stacks >= 1 && target != null && StatusEffects.Get(target.gameObject).Burning
                            && (Ledger == null || Ledger.FuelWorks);
                AddStacks(fuel ? T.FuelStacks : 1);
                _stackedThisSwing = true;
            }

            // IGNITE: inside the window every landed hit burns for a share of itself, merged by max
            // with whatever burn the target already carries (StatusEffects.ApplyBurn).
            // Through the player, so the board's Burn Power strengthens it like any burn they apply.
            if (Igniting && target != null && !target.IsDead && info.Amount > 0f)
            {
                if (Player != null) Player.Burn(target, info.Amount * T.IgniteBurnFraction * Scale, T.IgniteBurnSeconds);
                else StatusEffects.Get(target.gameObject).ApplyBurn(info.Amount * T.IgniteBurnFraction * Scale,
                                                                    T.IgniteBurnSeconds, gameObject);
            }

            // Fourth rung: the hit splashes to one more nearby enemy.
            if (Stacks >= 4 && target != null) Splash(target, info);
        }

        void Splash(Health primary, DamageInfo info)
        {
            var near = Physics2D.OverlapCircleAll(primary.transform.position, T.SplashRadius);
            foreach (var col in near)
            {
                if (col.gameObject == primary.gameObject || col.gameObject == gameObject) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                hp.Take(new DamageInfo(info.Amount * T.SplashFraction, ElementType.Fire, gameObject));
                Spr.Flash(hp.transform.position, 0.6f, new Color(1f, 0.5f, 0.2f), 0.2f);
                break;   // one splash target only
            }
        }

        /// <summary>Base StackLifetime scaled by mastery's FireStackLife + gear's ElementGrowth.</summary>
        float EffectiveStackLifetime => StackLifetime * Mathf.Max(0.1f, Gain);

        void AddStacks(int count)
        {
            int before = _stacks.Count;
            for (int i = 0; i < count; i++)
            {
                if (_stacks.Count >= MaxStacks) _stacks.RemoveAt(0);   // refresh: drop oldest
                _stacks.Add(EffectiveStackLifetime);
            }

            // Only when heat actually CLIMBS. Refreshing at max fires on every hit, and a burst
            // on every hit stops reading as a threshold and becomes background noise.
            if (_stacks.Count <= before) return;

            // A tight, translucent ring hugging the body.
            //
            // Two failure modes to stay between. At 0.5-to-0.95 units it fired at the moment of a
            // swing, centred on the player, at roughly the size of the old circular hitbox - so it
            // read as the attack's reach being drawn on screen. Filled instead of ringed, it
            // covered the character entirely with an orange disc, which is worse: the heat cue
            // hid the thing it was describing. Small and see-through does neither.
            float t = Stacks / (float)MaxStacks;
            var heat = Color.Lerp(new Color(1f, 0.5f, 0.15f), Color.white, t * 0.45f);
            heat.a = 0.45f;
            Spr.Pulse(transform, 0.34f + t * 0.12f, heat, 0.2f + t * 0.1f, true, 0.5f);

            // A release coming ready is the moment worth interrupting for, so it gets its own ring.
            if (Stacks >= MaxStacks)
                Spr.Pulse(transform, 1.5f, new Color(1f, 0.75f, 0.35f), 0.45f, true, 1.6f);
        }

        // ---- the release: ERUPT, or IGNITE if chosen on the board ----

        /// <summary>
        /// Only at a full five stacks. Fire's ladder is a slow climb, so the spend sits at the very
        /// top or the climb has no destination. Not while an Ignite window is still open.
        /// </summary>
        public override bool CanRelease => Stacks >= MaxStacks && !Igniting;

        /// <summary>
        /// Spend all five stacks. The cost is everything: the damage, the attack speed, Stoke and
        /// the splash all go at once, and the ladder restarts from nothing.
        /// </summary>
        public override void Release()
        {
            if (!CanRelease) return;
            if (UseSecondAbility) Ignite();
            else Erupt();
            _stacks.Clear();
        }

        /// <summary>
        /// ERUPT: scatter burning ground around the player. Placed RANDOMLY rather than aimed,
        /// because the pools are area denial, not a targeted hit - the player chooses WHERE to
        /// stand when they press it, and that is the whole decision. Spread across a ring rather
        /// than a disc so they do not all pile up underfoot, and jittered inward so the pattern is
        /// not a readable circle. Each pool burns what stands in it (FirePool).
        /// </summary>
        void Erupt()
        {
            var origin = (Vector2)transform.position;
            float baseAngle = Random.value * Mathf.PI * 2f;
            float area = AreaScale;
            float spread = T.EruptSpread * area, radius = T.PoolRadius * area;
            float dps = T.PoolHitUnitsPerSecond * HitUnit * Scale;

            for (int i = 0; i < T.EruptPools; i++)
            {
                // Evenly spaced around the ring, then jittered - evenly spaced alone reads as a
                // machine-drawn circle, fully random leaves bald patches and clumps.
                float angle = baseAngle + (i / (float)T.EruptPools) * Mathf.PI * 2f
                              + Random.Range(-0.35f, 0.35f);
                float dist = spread * Random.Range(0.35f, 1f);
                var pos = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                // Clamped inside the walls: a pool spawned through one is damage the player paid
                // for and can never use.
                pos = Arena.NearestFloor(Arena.Clamp(pos, radius), 0.3f);

                FirePool.Spawn(pos, radius, dps, T.PoolSeconds, gameObject, transform.parent,
                               burnScale: Player != null && Player.Board != null ? Player.Board.BurnPower : 1f);
            }

            Spr.Flash(transform.position, spread, new Color(1f, 0.55f, 0.2f), 0.5f);
            Spr.Flash(transform.position, spread * 0.5f, Color.white, 0.3f);
        }

        /// <summary>
        /// IGNITE: for a window, every landed hit burns. Burn used to be Fire's passive and was too
        /// strong as one; as a release it has a window to play inside - and with Fuel, a crowd set
        /// alight banks heat twice as fast toward the next one.
        /// </summary>
        void Ignite()
        {
            _igniteRemaining = T.IgniteSeconds;
            Spr.Pulse(transform, 1.2f, new Color(1f, 0.5f, 0.15f), 0.4f, true, 1.5f);
            Spr.Flash(transform.position, 1.2f, Color.white, 0.22f);
        }

        // ---- HUD: discrete pips, not a bar ----
        /// <summary>
        /// Returned stacks come back at FULL life rather than the life they had when spent. The
        /// alternative is remembering per-stack timers through a release, which is a lot of
        /// bookkeeping for a refund the player experiences as "some of it came back".
        /// </summary>
        public override void Refund(float fraction01)
        {
            // Carried between calls, so small refunds (a kill's Mother Lode, a hit's Sealed
            // Vessel) add up to whole stacks instead of rounding to nothing.
            _refundCarry += MaxStacks * Mathf.Clamp01(fraction01);
            while (_refundCarry >= 1f && _stacks.Count < MaxStacks)
            {
                _stacks.Add(EffectiveStackLifetime);
                _refundCarry -= 1f;
            }
            if (_stacks.Count >= MaxStacks) _refundCarry = 0f;
        }

        float _refundCarry, _spillCarry;

        /// <summary>Cracked Vessel: a share of the heat spilled, the oldest stacks first, carried
        /// between hits like a refund.</summary>
        public override void Spill(float fraction01)
        {
            _spillCarry += MaxStacks * Mathf.Clamp01(fraction01);
            while (_spillCarry >= 1f && _stacks.Count > 0)
            {
                _stacks.RemoveAt(0);
                _spillCarry -= 1f;
            }
            if (_stacks.Count == 0) _spillCarry = 0f;
        }

        /// <summary>Hearth: a release leaves at least this much heat.</summary>
        public void HoldAtLeast(int stacks)
        {
            while (_stacks.Count < Mathf.Min(stacks, MaxStacks)) _stacks.Add(EffectiveStackLifetime);
        }

        /// <summary>
        /// The release once more, spending nothing (Twin Spark, Wellspring): Erupt's pools at the
        /// repeat's strength, or Ignite's window run on by that share of itself.
        /// </summary>
        public override void ReleaseAgain()
        {
            if (UseSecondAbility)
            {
                _igniteRemaining = Mathf.Max(0f, _igniteRemaining) + T.IgniteSeconds * RepeatStrength;
                Spr.Pulse(transform, 1f, new Color(1f, 0.5f, 0.15f), 0.3f, true, 1.2f);
            }
            else Erupt();
        }

        public override float Fill01 => Stacks / (float)MaxStacks;
        public override int PipCount => MaxStacks;
        public override int PipsFilled => Stacks;

        public override string StatusLine
        {
            get
            {
                if (Igniting) return $"IGNITE  -  every hit burns  {_igniteRemaining:0.0}s";
                return Stacks switch
                {
                    0 => "no heat - land hits to stack",
                    1 => "1 - FUEL: burning enemies feed heat twice",
                    2 => "2 - attack speed",
                    3 => "3 - STOKE: heat fades half as fast",
                    4 => "4 - hits splash to a 2nd target",
                    _ => UseSecondAbility ? "5 - IGNITE READY  [RMB/E]" : "5 - ERUPT READY  [RMB/E]",
                };
            }
        }
    }
}
