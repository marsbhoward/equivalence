using UnityEngine;
using T2 = Convergence.Core.Tuning.Overhaul;

namespace Convergence.Combat
{
    /// <summary>
    /// THE CONTROL BUDGET (the overhaul's rule 2, 2026-11-04): the seconds of the ledger's control -
    /// a stagger, a root, a held-off attack - one body takes before it shrugs it off. Load fills with
    /// every grant and drains over ControlWindowSeconds; past ControlBudgetSeconds the body is IMMUNE
    /// for ControlImmuneSeconds, shown by a grey ring once. Diminishing returns on stuns, the way
    /// many games keep a lock from becoming permanent. Only the exchange's NEW control goes through
    /// it - the element's own staggers and the board's are as they were.
    /// </summary>
    public class ControlBudget : MonoBehaviour
    {
        [SerializeField] float _load, _immuneUntil, _last;

        /// <summary>How much of <paramref name="seconds"/> this body takes now (0 while immune).</summary>
        public static float Grant(Health body, float seconds)
        {
            if (body == null || body.IsDead || seconds <= 0f) return 0f;
            var b = body.GetComponent<ControlBudget>();   // never ?? - a missing component isn't C# null
            if (b == null) b = body.gameObject.AddComponent<ControlBudget>();
            return b.Take(seconds);
        }

        public static bool Immune(Health body)
        {
            if (body == null) return false;
            var b = body.GetComponent<ControlBudget>();
            return b != null && Time.time < b._immuneUntil;
        }

        float Take(float seconds)
        {
            float now = Time.time;
            if (now < _immuneUntil) return 0f;
            _load = Mathf.Max(0f, _load - (now - _last) * (T2.ControlBudgetSeconds / T2.ControlWindowSeconds));
            _last = now;
            float room = T2.ControlBudgetSeconds - _load;
            float granted = Mathf.Min(seconds, room);
            _load += granted;
            if (_load >= T2.ControlBudgetSeconds - 1e-3f)
            {
                _immuneUntil = now + granted + T2.ControlImmuneSeconds;
                _load = 0f;
                TexelFx.Ring(transform.position, TexelFx.Grey, 0.35f, 0.6f, 0.4f);
            }
            return Mathf.Max(0f, granted);
        }
    }
}
