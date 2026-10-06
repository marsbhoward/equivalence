using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The gray ring around an armored enemy - always a FULL circle while any armor remains,
    /// divided evenly into BarsRemaining arcs (see <see cref="Spr.SegmentedRing"/>). Losing a
    /// bar does not punch a hole in an otherwise-full ring; it reflows into fewer, wider arcs,
    /// so "how much is left" reads as arc width rather than a gap count. One bar left draws as
    /// a solid ring; zero hides it entirely.
    /// </summary>
    public class ArmorRing : MonoBehaviour
    {
        EnemyArmor _armor;
        SpriteRenderer _sr;
        int _shownBars = -1;

        static readonly Color RingColor = new(0.72f, 0.74f, 0.78f, 0.95f);

        public static ArmorRing Attach(GameObject go, EnemyArmor armor, float radius)
        {
            var ring = go.AddComponent<ArmorRing>();
            ring._armor = armor;

            var ringGo = new GameObject("armor-ring");
            ringGo.transform.SetParent(go.transform, false);
            ringGo.transform.localScale = Vector3.one * radius * 2f;

            ring._sr = ringGo.AddComponent<SpriteRenderer>();
            ring._sr.color = RingColor;
            ring._sr.sortingOrder = SortingOrders.Enemy + 1;

            ring.Refresh();
            return ring;
        }

        void LateUpdate() => Refresh();

        /// <summary>Fog III: armour rings are hidden. Set by the run's ledger, reset at run end.</summary>
        public static bool Hidden;

        void Refresh()
        {
            if (_armor == null || _sr == null) return;
            if (Hidden) { _sr.enabled = false; _shownBars = -1; return; }
            int bars = _armor.BarsRemaining;
            if (bars == _shownBars) return;
            _shownBars = bars;
            _sr.enabled = bars > 0;
            if (bars > 0) _sr.sprite = Spr.SegmentedRing(bars);
        }
    }
}
