using UnityEngine;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Enemies
{
    /// <summary>
    /// An ELITE MODIFIER (Enemies.EliteAffix, Tuning.EliteAffixes, the user's go-ahead 2026-10-25):
    /// one more rule on top of the kind, rolled by the wave composer. Each shows itself - the
    /// Bell's bubble, rising embers, falling drips - and none is named on screen.
    /// </summary>
    public partial class EnemyController
    {
        [SerializeField] EliteAffix _affix;
        public EliteAffix Affix => _affix;

        [SerializeField] Vector2 _cinderFrom;
        [SerializeField] bool _cinderStarted;

        // Rising embers (Cindered) and yellow drips (Mired), each a ramp for BoostedMotes.
        static readonly Color[] EmberRamp = { new(0.35f, 0.08f, 0.04f), new(0.95f, 0.35f, 0.08f), new(1f, 0.72f, 0.30f) };
        static readonly Color[] DripRamp = { new(0.45f, 0.36f, 0.05f), new(0.92f, 0.70f, 0.10f), new(1f, 0.86f, 0.20f) };

        /// <summary>Set once by EnemyFactory, after the body is built. Only an elite carries one.</summary>
        public void ApplyAffix(EliteAffix affix)
        {
            if (!Elite || affix == EliteAffix.None) return;
            _affix = affix;
            switch (affix)
            {
                case EliteAffix.Warded:
                    ApplyBubble(Tuning.EliteAffixes.WardCharges, BubblesArt.AccentOf(EnemyLook.Construct), null);
                    break;
                case EliteAffix.Cindered:
                    BoostedMotes.Attach(this, EmberRamp, falling: false, name: "affix.cinders");
                    break;
                case EliteAffix.Mired:
                    BoostedMotes.Attach(this, DripRamp, falling: true, name: "affix.mire");
                    break;
            }
        }

        /// <summary>Cindered: a burning patch every CinderEvery units walked. FixedUpdate, alive and
        /// standing on the floor.</summary>
        void TickAffix(Vector2 pos)
        {
            if (_affix != EliteAffix.Cindered) return;
            if (!_cinderStarted) { _cinderStarted = true; _cinderFrom = pos; return; }
            if ((pos - _cinderFrom).sqrMagnitude < Tuning.EliteAffixes.CinderEvery * Tuning.EliteAffixes.CinderEvery) return;
            _cinderFrom = pos;
            if (!Arena.OnFloor(pos, 0.1f)) return;
            CinderGround.Spawn(pos, transform.parent, _target, gameObject, Damage * Tuning.EliteAffixes.CinderDamageMul);
        }

        /// <summary>Mired: the body falls into a mire where it dies - not one that fell into a chasm.</summary>
        void AffixDied()
        {
            if (_affix != EliteAffix.Mired || _fellIn) return;
            Vector2 at = transform.position;
            if (!Arena.OnFloor(at, 0.1f)) return;
            MireField.Spawn(at, transform.parent, turned: false,
                            radius: Tuning.Enemy.MireRadius * Tuning.EliteAffixes.MireRadiusMul);
        }
    }
}
