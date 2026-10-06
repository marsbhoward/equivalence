using System.Collections;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.Elements.Earth;

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
        public override float DamagePoints => T.DamagePoints;
        public override float AttackSpeedPoints => T.AttackSpeedPoints;
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
                Spr.Pulse(transform, 1.6f, new Color(0.85f, 0.72f, 0.45f), 0.5f, true, 1.5f);
            }
            else if (Charge < 0.98f)
            {
                _wasFull = false;
            }

            if (!_still || Charge <= 0.05f || Charge >= 0.999f) return;

            _chargeCue -= dt;
            if (_chargeCue > 0f) return;
            _chargeCue = Mathf.Lerp(0.9f, 0.28f, Charge);
            Spr.Pulse(transform, 0.45f + Charge * 0.8f,
                      new Color(0.7f, 0.6f, 0.38f, 0.55f), 0.34f, true, 0.9f);
        }

        public override bool CanRelease => Charge > 0.2f && !_quaking;

        public override void Release()
        {
            if (!CanRelease) return;

            float charged = Charge;
            _lastCharged = charged;
            // Aftershocks scale with how long the meter was held, not a binary max-charge check.
            int aftershocks = Mathf.FloorToInt(charged * ChargeSeconds / AftershockPerSeconds);
            Charge = 0f;
            StartCoroutine(Quake(charged, aftershocks));
        }

        IEnumerator Quake(float charged, int aftershocks)
        {
            _quaking = true;
            Erupt(charged, 1f);

            for (int i = 0; i < aftershocks; i++)
            {
                yield return new WaitForSeconds(0.45f);
                Erupt(charged, 0.55f - i * 0.08f);
            }
            _quaking = false;
        }

        void Erupt(float charged, float scale)
        {
            float radius = Radius * AreaScale * (0.55f + charged * 0.45f) * Mathf.Max(0.4f, scale);
            // In hit units (Tuning.Elements): the quake grows with the player's own hit.
            float damage = (T.ShockHitUnits + charged * T.ShockHitUnitsPerCharge) * HitUnit * scale * Scale;

            foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius))
            {
                if (col.gameObject == gameObject) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                var to = ((Vector2)col.transform.position - (Vector2)transform.position).normalized;
                PlayerController.ReleaseHitFrom(gameObject, hp, new DamageInfo(damage, ElementType.Earth, gameObject) { Knockback = to * T.ShockKnockback * scale });
                // Through the player, so the board's Stagger Power, Antimony and Congelation reach it.
                if (Player != null) Player.Stagger(hp, T.StaggerSeconds);
                else StatusEffects.Get(hp.gameObject).ApplyStagger(T.StaggerSeconds);
            }

            Spr.Flash(transform.position, radius, ElementInfo.Tint(ElementType.Earth), 0.45f);
        }

        /// <summary>The quake once more, spending nothing (Twin Spark, Wellspring): the charge the
        /// last one spent, at the repeat's strength.</summary>
        public override void ReleaseAgain()
        {
            if (_lastCharged <= 0f || _quaking) return;
            int aftershocks = Mathf.FloorToInt(_lastCharged * ChargeSeconds / AftershockPerSeconds);
            StartCoroutine(Quake(_lastCharged, aftershocks));
        }

        public override void Spill(float fraction01) => Charge = Mathf.Max(0f, Charge - fraction01);

        public override void Refund(float fraction01)
            => Charge = Mathf.Clamp01(Charge + fraction01);

        public override float Fill01 => Charge;

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
