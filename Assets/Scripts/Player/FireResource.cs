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
    /// HEAT IS FILLED BY FIGHTING (2026-11-05, the user's call). A landed swing or throw fills
    /// what it fills Water's meter (Tuning.Elements.MeterPerSwing), ranged hits included; a hit
    /// taken or parried fills the same. Whenever nothing has filled it for a moment it DRAINS, a
    /// stack a second - so kiting empties it. Five whole stacks are the ladder; every stack is a
    /// little more damage (a head start, Tuning.Elements.Fire); the rungs add:
    ///
    ///     1  FUEL   a swing landing on a BURNING enemy fills twice
    ///     2         attack speed
    ///     3  STOKE  heat drains half as fast
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

        /// <summary>Heat in stacks, 0..MaxStacks; the ladder reads its whole stacks.</summary>
        float _heat;
        bool _stackedThisSwing;
        float _igniteRemaining;
        /// <summary>Seconds since anything filled heat; past DrainAfter it drains.</summary>
        float _idle;
        float _hurtGainAt = -99f;

        public int Stacks => Mathf.Min(MaxStacks, Mathf.FloorToInt(_heat + 0.0001f));

        /// <summary>One landed swing's heat, in stacks, at the character's and the run's growth.</summary>
        float SwingHeat => Tuning.Elements.MeterPerSwing * MaxStacks * Gain;

        /// <summary>True while an Ignite window is open - every landed hit burns.</summary>
        public bool Igniting => _igniteRemaining > 0f;
        public float IgniteRemaining => _igniteRemaining;

        public override ElementType Element => ElementType.Fire;

        public override float DamagePoints => Grown(Stacks * (Ledger != null ? Ledger.HeatPoints(T.StackDamagePoints) : T.StackDamagePoints));
        public override float AttackSpeedPoints => Stacks >= 2 ? Grown(T.HastePoints) : 0f;

        // The heat's picture on the ground (scorch ring, embers, the sigil at full heat).
        [SerializeField] HeatGround _heatGround;

        void Update()
        {
            float dt = Time.deltaTime;
            if (_heatGround == null) _heatGround = HeatGround.Attach(this);

            // THE DRAIN: once nothing has filled heat for DrainAfter, it drains a stack a second.
            // STOKE, from the third rung up, halves it; the ledger's fade on top (Leaky Vessel
            // faster, Sealed Vessel not at all), and its floor under it: Banked Embers keeps that
            // many stacks from draining - a release still spends them.
            _idle += dt;
            if (_idle >= T.DrainAfter && _heat > 0f)
            {
                float drain = T.DrainStacksPerSecond * dt * Decay / (Stacks >= 3 ? T.StokeDrainDiv : 1f);
                float floor = Mathf.Min(_heat, Ledger != null ? Ledger.HeatFloor : 0);
                int before = Stacks;
                _heat = Mathf.Max(floor, _heat - drain);
                if (Stacks < before) Ledger?.OnHeatLost(before - Stacks);   // Phlogiston
            }

            if (_igniteRemaining > 0f)
            {
                _igniteRemaining -= dt;
                // The window's END matters as much as its start - the same tell Water's surge has.
                if (_igniteRemaining <= 0f)
                {
                    _igniteRemaining = 0f;
                    // A ring in its colour closing in on the floor (the user's pick, 2026-10-26).
                    TexelFx.Ring(ReleaseCanvas.Feet(transform), TexelFx.Fire, 0.55f, 0.05f, 0.4f, 1.2f);
                }
            }
        }

        /// <summary>Each swing may bank at most one batch of heat; the flag resets here.</summary>
        public override void OnAttackStarted() => _stackedThisSwing = false;

        public override void Bind(PlayerController player)
        {
            if (Player != null && Player.Health != null) Player.Health.Damaged -= OnHurt;
            base.Bind(player);
            if (Player != null && Player.Health != null) Player.Health.Damaged += OnHurt;
        }

        void OnDestroy()
        {
            if (Player != null && Player.Health != null) Player.Health.Damaged -= OnHurt;
        }

        /// <summary>A hit taken fills heat like a landed swing - not a price paid or a status tick.</summary>
        void OnHurt(DamageInfo info)
        {
            if (info.Price || info.Tick || info.Source == gameObject) return;
            FillFromFight();
        }

        /// <summary>A parry fills heat like a hit taken - defending well is still fighting.</summary>
        public override void OnParried() => FillFromFight();

        void FillFromFight()
        {
            _idle = 0f;
            if (Time.time - _hurtGainAt < T.HitTakenEvery || Gain <= 0f) return;
            _hurtGainAt = Time.time;
            AddHeat(SwingHeat);
        }

        public override void OnHitLanded(Health target, DamageInfo info)
        {
            // Heat comes from ATTACKS that connect, not from bodies caught in them. Counting per
            // target let one cleaving finisher through a crowd jump straight to full stacks.
            // Thrown hits count like any other (2026-11-05): the drain, not the range, is what
            // keeps a kiting player from holding heat. Nothing builds through Barren's seconds.
            _idle = 0f;
            if (!_stackedThisSwing && Gain > 0f)
            {
                // FUEL, from the first rung: a burning enemy - Erupt's ground, an Ignite window, any
                // burn at all - fills twice. Wet Ash puts it out.
                bool fuel = Stacks >= 1 && target != null && StatusEffects.Get(target.gameObject).Burning
                            && (Ledger == null || Ledger.FuelWorks);
                AddHeat(SwingHeat * (fuel ? T.FuelStacks : 1));
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
                TexelFx.Ring(primary.transform.position, TexelFx.Fire, 0.15f, T.SplashRadius, 0.3f, 1.2f, dotted: true);
                break;   // one splash target only
            }
        }

        void AddHeat(float stacks)
        {
            _heat = Mathf.Min(MaxStacks, _heat + stacks);

            // No ring round the body for heat gained (the user's call, 2026-10-15: a ring on a
            // landed hit reads as "damage was done", and those are gone from every attack). The
            // element seal's Kindling shows the heat, and the scorched ground under the feet
            // grows with it - a state, held, not a pulse per hit (HeatGround).

            // A release coming ready is the moment worth interrupting for: the sigil emblem burns into
            // the ground under the feet and stays until it is spent (HeatGround - it replaced a
            // ring pulsing out from the body).
        }

        // ---- the release: ERUPT, or IGNITE if chosen on the board ----

        /// <summary>
        /// Only at a full five stacks. Fire's ladder is a slow climb, so the spend sits at the very
        /// top or the climb has no destination. Not while an Ignite window is still open.
        /// </summary>
        public override bool CanRelease => Stacks >= MaxStacks && !Igniting;

        protected override bool SecondFor(ReleaseKind kind) => kind == ReleaseKind.Buff;   // Ignite

        /// <summary>
        /// Spend all five stacks. The cost is everything: the damage, the attack speed, Stoke and
        /// the splash all go at once, and the ladder restarts from nothing.
        /// </summary>
        public override void Release()
        {
            if (!CanRelease) return;
            if (UseSecondAbility) Ignite();
            else Erupt();
            _heat = 0f;
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
            // Round the player - with a bow, a step short of the enemy it aims at (BlastCentre).
            var origin = BlastCentre(out var feet);
            float baseAngle = Random.value * Mathf.PI * 2f;
            float area = ReleaseAreaScale;
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

                // Lit as the blast's ring of fire reaches it (EruptBlast).
                FirePool.Spawn(pos, radius, dps, T.PoolSeconds, gameObject, transform.parent,
                               burnScale: Player != null && Player.Board != null ? Player.Board.BurnPower : 1f,
                               delay: EruptBlast.ArrivesAt(Vector2.Distance(pos, feet), spread));
            }

            EruptBlast.Play(feet, spread);
        }

        /// <summary>
        /// IGNITE: for a window, every landed hit burns. Burn used to be Fire's passive and was too
        /// strong as one; as a release it has a window to play inside - and with Fuel, a crowd set
        /// alight banks heat twice as fast toward the next one.
        /// </summary>
        void Ignite()
        {
            _igniteRemaining = T.IgniteSeconds;
            FlameOutline.Play(transform);
        }

        // ---- HUD: discrete pips, not a bar ----
        /// <summary>A share of a full meter handed back (Mother Lode, Sealed Vessel, Overflow).</summary>
        public override void Refund(float fraction01) => AddHeat(MaxStacks * Mathf.Clamp01(fraction01));

        /// <summary>Cracked Vessel: a share of the heat spilled.</summary>
        public override void Spill(float fraction01)
        {
            int before = Stacks;
            _heat = Mathf.Max(0f, _heat - MaxStacks * Mathf.Clamp01(fraction01));
            if (Stacks < before) Ledger?.OnHeatLost(before - Stacks);   // Phlogiston
        }

        /// <summary>A share of the heat spent on a Guard, a move or a combo strike - spent, not lost,
        /// so Phlogiston does not answer it.</summary>
        public override void Spend(float share01) => _heat = Mathf.Max(0f, _heat - MaxStacks * Mathf.Clamp01(share01));

        /// <summary>Hearth: a release leaves at least this much heat.</summary>
        public void HoldAtLeast(int stacks) => _heat = Mathf.Max(_heat, Mathf.Min(stacks, MaxStacks));

        /// <summary>
        /// The release once more, spending nothing (Twin Spark, Wellspring): Erupt's pools at the
        /// repeat's strength, or Ignite's window run on by that share of itself.
        /// </summary>
        public override void ReleaseAgain()
        {
            if (UseSecondAbility)
            {
                _igniteRemaining = Mathf.Max(0f, _igniteRemaining) + T.IgniteSeconds * RepeatStrength;
                FlameOutline.Play(transform);
            }
            else Erupt();
        }

        public override float Fill01 => _heat / MaxStacks;
        /// <summary>No ring segments: Fire's meter is its triangle, three sides (ElementSeal.DrawSigil).</summary>
        public override int PipCount => 0;
        public override int PipsFilled => Stacks;

        public override string SealTag
            => Igniting ? $"IGNITE  {_igniteRemaining:0.0}s"
             : Stacks switch
               {
                   1 => "1 · FUEL",
                   2 => "2 · HASTE",
                   3 => "3 · STOKE",
                   4 => "4 · SPLASH",
                   _ => null,
               };

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
                    3 => "3 - STOKE: heat drains half as fast",
                    4 => "4 - hits splash to a 2nd target",
                    _ => UseSecondAbility ? "5 - IGNITE READY  [RMB/E]" : "5 - ERUPT READY  [RMB/E]",
                };
            }
        }
    }
}
