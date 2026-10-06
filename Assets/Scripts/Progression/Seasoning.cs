using UnityEngine;
using Convergence.Core;

namespace Convergence.Progression
{
    /// <summary>
    /// Sulfur's mark, carried by the ENEMY rather than by a table on the player.
    ///
    /// A component, not a Dictionary keyed by Health, and that is the documented reason rather
    /// than taste: a Dictionary field on a MonoBehaviour comes back EMPTY after a domain reload
    /// while every object it referenced survives, so a script edit mid-run would silently
    /// de-season a field full of marked enemies with nothing on screen changing. A component dies
    /// with the body it is attached to and needs no sweeping.
    ///
    /// It draws its own tell. A mark the player cannot see is a mark they cannot play around, and
    /// the whole chain is about choosing WHEN to release.
    /// </summary>
    public class Seasoning : MonoBehaviour
    {
        public int Stacks { get; private set; }
        float _expires;
        SpriteRenderer _tell;

        public static Seasoning Get(GameObject go)
            => go.GetComponent<Seasoning>() ?? go.AddComponent<Seasoning>();

        /// <summary>
        /// Refreshes the clock on every application, so sustained pressure keeps a target seasoned
        /// - which is the behaviour Sulfur is rewarding. A per-stack clock would make the mark
        /// decay from underneath a player who is doing exactly the right thing.
        /// </summary>
        public void Add()
        {
            Stacks = Mathf.Min(Stacks + 1, Tuning.Principle.SeasonMaxStacks);
            _expires = Time.time + Tuning.Principle.SeasonSeconds;
        }

        /// <summary>Takes the marks off and reports how many there were.</summary>
        public int Consume()
        {
            int n = Stacks;
            Stacks = 0;
            return n;
        }

        void Update()
        {
            if (Stacks > 0 && Time.time >= _expires) Stacks = 0;
            Draw();
        }

        void Draw()
        {
            if (Stacks <= 0)
            {
                if (_tell != null) _tell.enabled = false;
                return;
            }

            if (_tell == null)
            {
                var go = new GameObject("seasoned");
                go.transform.SetParent(transform, false);
                _tell = go.AddComponent<SpriteRenderer>();
                _tell.sprite = Spr.Ring;
                // An overlay, so it is never re-based by DepthSorted - a mark that vanished behind
                // the floor at certain depths would be worse than no mark at all.
                _tell.sortingOrder = SortingOrders.StatusOverlay + 2;
            }

            _tell.enabled = true;
            float k = Stacks / (float)Tuning.Principle.SeasonMaxStacks;
            _tell.transform.localScale = Vector3.one * (1.1f + k * 0.45f);
            _tell.color = new Color(1f, 0.55f + 0.25f * k, 0.15f, 0.25f + 0.4f * k);
        }
    }
}
