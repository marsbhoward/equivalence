using System.Collections;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.Elements.Earth;
using R = Convergence.Core.Tuning.Releases;

namespace Convergence.Player
{
    /// <summary>
    /// EARTH - Stillness Meter (slow / durable).
    /// The meter builds only while standing still and bleeds off while moving. Releasing
    /// fires an earthquake; at higher charge it produces aftershocks.
    ///
    /// DESIGN DECISIONS (both were open in the handoff):
    ///  - The earthquake is ACTIVATED, not passive. Earth already pays tempo to stand still;
    ///    auto-firing on a full meter would remove agency on top of that cost.
    ///  - Aftershock count SCALES with charge duration (1 per ~1.7s held) instead of being
    ///    binary at max charge, so partial charges are a real tactical choice.
    /// Passively: high poise (resists knockback) and damage reduction while planted.
    /// </summary>
    public class EarthResource : ElementalResource
    {
        public float ChargeSeconds = T.ChargeSeconds;     // seconds of stillness for a full meter
        public float Charge;                 // 0..1
        public float Radius = T.Radius;

        /// <summary>
        /// Seconds of MOVEMENT to drain a full meter. Was ChargeSeconds * 2 (10s), which barely
        /// punished moving at all - a full charge survived crossing the arena and back, so
        /// "planted" was a suggestion rather than a commitment.
        ///
        /// At 2s it is faster to lose than to build (5s), which is the point: earth's payoff is
        /// bought with standing still under pressure, and stepping out of danger has to actually
        /// cost the charge or there is no pressure to stand under.
        /// </summary>
        public float MovingDecaySeconds = T.MovingDecaySeconds;
        /// <summary>Seconds of held charge per aftershock: 2s stillness = 1, a full 5s = 3.</summary>
        public float AftershockPerSeconds = T.AftershockPerSeconds;

        bool _still;
        bool _quaking;
        bool _wasFull;
        float _chargeCue;

        public override ElementType Element => ElementType.Earth;
        // Heavy, slow swings - as head starts (Tuning.Elements): Earth reaches the damage cap
        // sooner and the attack-speed cap later, never a different cap.
        public override float DamagePoints => Grown(T.DamagePoints);
        public override float AttackSpeedPoints => Grown(T.AttackSpeedPoints);   // a penalty: never grown
        public override float PoiseBonus => _still ? T.StillPoise : T.MovingPoise;
        /// <summary>Planted - or just stepped off under Bedrock, which holds the planted reduction
        /// a moment after a move.</summary>
        public override float DamageReduction
            => _still || _movingFor < (Ledger != null ? Ledger.PlantedHoldSeconds : 0f)
                ? T.StillDamageReduction : T.MovingDamageReduction;

        /// <summary>Seconds into the current move - Deep Roots and Bedrock hold for the first of them.</summary>
        float _movingFor;
        float _lastCharged;

        public override void OnMoved(bool moving) => _still = !moving;

        void Update()
        {
            float dt = Time.deltaTime;
            _movingFor = _still ? 0f : _movingFor + dt;
            if (_still && !_quaking)
                Charge = Mathf.Clamp01(Charge + dt * Gain / ChargeSeconds);
            else if (!_still)
            {
                // Deep Roots: the charge holds a moment after a step before it bleeds.
                if (_movingFor >= (Ledger != null ? Ledger.ChargeHoldSeconds : 0f))
                    Charge = Mathf.Max(0f, Charge - dt * Decay / Mathf.Max(0.1f, MovingDecaySeconds));
            }

            ChargeFeedback(dt);

            if (_tremorRemaining > 0f && (_tremorRemaining -= dt) <= 0f)
            {
                _tremorRemaining = 0f;
                TexelFx.Ring(ReleaseCanvas.Feet(transform), TexelFx.Stone, 0.55f, 0.05f, 0.4f, 1.2f);   // closing
            }
        }

        /// <summary>
        /// Earth pays tempo to stand still, and standing still is exactly the state with nothing
        /// happening on screen to show for it. A slow ring that quickens as the meter fills makes
        /// the wait legible; a single bright ring marks the moment it is worth spending.
        /// </summary>
        void ChargeFeedback(float dt)
        {
            if (_quaking) return;

            if (Charge >= 0.999f && !_wasFull)
            {
                _wasFull = true;
                // Full: stones hop all round the feet and dust kicks up (the user's pick - on the floor).
                TexelFx.Hop(ReleaseCanvas.Feet(transform), TexelFx.Stone, 0.42f, 10, TexelFx.Ground);
                TexelFx.Puff(ReleaseCanvas.Feet(transform), TexelFx.Stone, 10, 10f, 0.5f, TexelFx.Ground);
            }
            else if (Charge < 0.98f)
            {
                _wasFull = false;
            }

            if (!_still || Charge <= 0.05f || Charge >= 0.999f) return;

            _chargeCue -= dt;
            if (_chargeCue > 0f) return;
            _chargeCue = Mathf.Lerp(0.9f, 0.28f, Charge);
            // Small stones hopping at the feet, more of them as it charges.
            TexelFx.Hop(ReleaseCanvas.Feet(transform), TexelFx.Stone, 0.32f, 3 + Mathf.RoundToInt(Charge * 5f), TexelFx.Ground);
        }

        public override bool CanRelease => Charge > 0.2f && !_quaking;

        /// <summary>The arsenal spends a FULL meter on a Blast (RELEASES.md) - the partial quake went
        /// with it; spending part of the meter is the combo's.</summary>
        protected override bool CanFireOwn(ReleaseKind kind) => Charge >= 0.999f && !_quaking && !Tremoring;

        protected override void FireOwn(ReleaseKind kind)
        {
            UseSecondAbility = kind == ReleaseKind.Buff;
            if (UseSecondAbility) TremorStance();
            else Release();
        }

        // ---- TREMOR STANCE, Earth's Buff (RELEASES.md): for a window every swing sends a crack along
        // the floor that strikes what it reaches. Spends the full charge; standing still still builds.

        float _tremorRemaining;
        public bool Tremoring => _tremorRemaining > 0f;

        void TremorStance()
        {
            Charge = 0f;
            _tremorRemaining = R.TremorSeconds;
            var feet = ReleaseCanvas.Feet(transform);
            TexelFx.Shock(feet, TexelFx.Stone, TexelFx.RubbleOf(), 0.9f, 0.35f);
            TexelFx.Hop(feet, TexelFx.Stone, 0.6f, 12, TexelFx.Ground);
        }

        /// <summary>A swing in the stance: the crack leaves a beat later, along the facing the strike
        /// took (the body faces freely until the strike - RIG.md).</summary>
        public override void OnAttackStarted()
        {
            if (Tremoring && Player != null) StartCoroutine(CrackAfter(0.12f));
        }

        IEnumerator CrackAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (Player == null) yield break;
            // A bow's crack starts where its arrow lands - a step short of the nearest, as its Blast does.
            Vector2? start = null;
            if (Player.Weapon == Art.Gear.WeaponClass.Bow) start = BlastCentre(out _);
            ReleaseArsenal.Crack(Player, Player.Facing, R.TremorCrackLength * AreaScale, R.TremorCrackHalfWidth,
                                 R.TremorCrackHitUnits * Scale, start);
        }

        [SerializeField] FaultSigil _fault;

        public override void Release()
        {
            if (!CanRelease) return;

            float charged = Charge;
            _lastCharged = charged;
            // Aftershocks scale with how long the meter was held, not a binary max-charge check.
            int aftershocks = Mathf.FloorToInt(charged * ChargeSeconds / AftershockPerSeconds);
            Charge = 0f;
            StartCoroutine(Quake(charged, aftershocks, ReleaseAreaScale));
        }

        IEnumerator Quake(float charged, int aftershocks, float area)
        {
            _quaking = true;
            // With a bow the quake lands a step short of the enemy it aims at (BlastCentre), and its aftershocks stay there;
            // otherwise they follow the player, as ever.
            Vector2 centre = BlastCentre(out var feet);
            Vector2? at = (centre - (Vector2)transform.position).sqrMagnitude > 1e-4f ? centre : null;
            Erupt(charged, 1f, area, at, feet);

            for (int i = 0; i < aftershocks; i++)
            {
                yield return new WaitForSeconds(0.45f);
                Erupt(charged, 0.55f - i * 0.08f, area, at, feet);
            }
            _quaking = false;
        }

        /// <param name="area">Area at the release, Concentrate's reach included - read once, as the
        /// aftershocks come after the button is let go.</param>
        /// <param name="at">Where it lands when not round the player (a bow's BlastCentre), with
        /// <paramref name="atFeet"/> where it is drawn.</param>
        void Erupt(float charged, float scale, float area, Vector2? at = null, Vector2 atFeet = default)
        {
            Vector2 centre = at ?? (Vector2)transform.position;
            float radius = Radius * area * (0.55f + charged * 0.45f) * Mathf.Max(0.4f, scale);
            // In hit units (Tuning.Elements): the quake grows with the player's own hit.
            float damage = (T.ShockHitUnits + charged * T.ShockHitUnitsPerCharge) * HitUnit * scale * Scale;

            foreach (var col in Physics2D.OverlapCircleAll(centre, radius))
            {
                if (col.gameObject == gameObject) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                var to = ((Vector2)col.transform.position - centre).normalized;
                PlayerController.ReleaseHitFrom(gameObject, hp, new DamageInfo(damage, ElementType.Earth, gameObject) { Knockback = to * T.ShockKnockback * scale });
                // Through the player, so the board's Stagger Power, Antimony and Congelation reach it.
                if (Player != null) Player.Stagger(hp, T.StaggerSeconds);
                else StatusEffects.Get(hp.gameObject).ApplyStagger(T.StaggerSeconds);
            }

            // The fault sigil: the quake opens one, an aftershock where it stands lights it again.
            var here = at != null ? atFeet : ReleaseCanvas.Feet(transform);   // drawn on the floor under the body
            if (scale < 1f && _fault != null && Vector2.Distance(_fault.Centre, here) <= Tuning.ReleaseFx.QuakeReuseDistance)
                _fault.Shock();
            else _fault = FaultSigil.Play(here, radius);
            if (scale >= 1f) FightMarks.Crack(centre, radius * 0.6f);   // the quake, not its aftershocks
        }

        /// <summary>The quake once more, spending nothing (Twin Spark, Wellspring): the charge the
        /// last one spent, at the repeat's strength.</summary>
        public override void ReleaseAgain()
        {
            if (UseSecondAbility) { _tremorRemaining = Mathf.Max(0f, _tremorRemaining) + R.TremorSeconds * RepeatStrength; return; }
            if (_lastCharged <= 0f || _quaking) return;
            int aftershocks = Mathf.FloorToInt(_lastCharged * ChargeSeconds / AftershockPerSeconds);
            StartCoroutine(Quake(_lastCharged, aftershocks, AreaScale));
        }

        public override void Spill(float fraction01) => Charge = Mathf.Max(0f, Charge - fraction01);

        public override void Refund(float fraction01)
            => Charge = Mathf.Clamp01(Charge + fraction01);

        public override float Fill01 => Charge;

        /// <summary>The aftershocks a release right now would carry - the rule Release spends.</summary>
        int Aftershocks => Mathf.FloorToInt(Charge * ChargeSeconds / AftershockPerSeconds);

        public override string ReleaseName => TapKind == ReleaseKind.Blast && !HasHold && Aftershocks > 0 ? $"QUAKE +{Aftershocks}" : base.ReleaseName;
        public override string SealTag
            => Tremoring ? $"TREMOR  {_tremorRemaining:0.0}s" : _quaking ? "AFTERSHOCKS" : _still ? "PLANTED" : Charge > 0f ? "MOVING" : null;
        public override int RimPips => Mathf.FloorToInt(ChargeSeconds / AftershockPerSeconds + 0.001f);
        public override int RimPipsLit => _quaking ? 0 : Aftershocks;

        public override string StatusLine
        {
            get
            {
                if (_quaking) return "AFTERSHOCKS...";
                int shocks = Mathf.FloorToInt(Charge * ChargeSeconds / AftershockPerSeconds);
                if (Charge <= 0.2f) return _still ? "planting... hold still" : "moving - charge bleeding";
                return $"[RMB/E] quake  +{shocks} aftershock{(shocks == 1 ? "" : "s")}";
            }
        }
    }
}
