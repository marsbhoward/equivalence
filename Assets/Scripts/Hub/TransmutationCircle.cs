using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The transmutation circle in the middle of the hub: sulfur, mercury and salt on a ring.
    ///
    /// It sits at the centre of the room on purpose. The doors are a decision you make once per
    /// run and then leave through; this is the thing you come back to, and putting it where the
    /// character spawns means the first thing a player is standing in is the one that says
    /// "you can change what you are".
    ///
    /// The three marks are not decoration - they name the three pages of the screen this opens.
    /// See <see cref="UI.TransmutationScreen"/>.
    /// </summary>
    public class TransmutationCircle : MonoBehaviour
    {
        public Vector2 Anchor { get; private set; }
        public float Radius { get; private set; }
        public bool Focused { get; private set; }

        SpriteRenderer _outer, _inner, _fill;

        /// <summary>
        /// NOT readonly, deliberately.
        ///
        /// Editing a script while play mode is running triggers a domain reload, and Unity
        /// restores a MonoBehaviour's plain private fields across it but CANNOT write to a
        /// readonly one - so a readonly array came back freshly initialised, full of nulls, while
        /// every sibling field survived. The result was not a blip: Update kept running against
        /// the empty array and threw once per frame for the rest of the session. It looked like a
        /// stale error from the reload and was not.
        /// </summary>
        SpriteRenderer[] _marks = new SpriteRenderer[3];
        float _lit;

        static readonly Color Gold = new(0.86f, 0.72f, 0.38f);

        public static TransmutationCircle Build(Transform parent, Vector2 centre, float radius)
        {
            var go = new GameObject("transmutation-circle");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var c = go.AddComponent<TransmutationCircle>();
            c.Anchor = centre;
            c.Radius = radius;

            c._fill = Disc(go.transform, "fill", radius * 2f, new Color(0.86f, 0.72f, 0.38f, 0.03f),
                           SortingOrders.Floor, Spr.Circle);
            c._outer = Disc(go.transform, "outer", radius * 2f, Gold, SortingOrders.GroundDecal, Spr.ThinRing);
            c._inner = Disc(go.transform, "inner", radius * 1.52f, Gold, SortingOrders.GroundDecal, Spr.ThinRing);

            // Marks on a triangle, point UP: the arrangement is the one thing that says this is a
            // transmutation circle rather than a decorative ring on a floor.
            var marks = new[]
            {
                Combat.Glyphs.PrimeMark.Sulfur,
                Combat.Glyphs.PrimeMark.Mercury,
                Combat.Glyphs.PrimeMark.Salt,
            };
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad;
                var at = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (radius * 0.76f);

                var m = new GameObject(marks[i].ToString());
                m.transform.SetParent(go.transform, false);
                m.transform.localPosition = at;
                m.transform.localScale = Vector3.one * (radius * 0.48f);
                var sr = m.AddComponent<SpriteRenderer>();
                sr.sprite = Combat.Glyphs.Prime(marks[i]);
                sr.sortingOrder = SortingOrders.GroundDecal + 1;
                c._marks[i] = sr;
            }

            c.Apply(0f);
            return c;
        }

        public void SetFocus(bool on) => Focused = on;

        void Update()
        {
            // A domain reload can hand this component back with its renderers gone. Rebuilding is
            // not possible from here, but a silent skip is far better than an exception per frame
            // - the room keeps working and the circle simply stops animating until it is rebuilt.
            if (_outer == null || _inner == null || _fill == null || _marks == null) return;

            // Unscaled, so the circle keeps breathing under the screen it opens rather than
            // freezing the moment the player uses it.
            _lit = Mathf.MoveTowards(_lit, Focused ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            Apply(_lit);
        }

        void Apply(float lit)
        {
            _outer.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Lerp(0.16f, 0.60f, lit));
            _inner.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Lerp(0.10f, 0.40f, lit));
            _fill.color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Lerp(0.022f, 0.075f, lit));

            // The three marks pulse in sequence rather than together. In step they read as one
            // flashing object; offset, they read as three things taking turns, which is the
            // relationship the screen behind them is built on.
            for (int i = 0; i < _marks.Length; i++)
            {
                if (_marks[i] == null) continue;
                float phase = Time.unscaledTime * 2.2f - i * (Mathf.PI * 2f / 3f);
                float beat = 0.5f + 0.5f * Mathf.Sin(phase);
                float a = Mathf.Lerp(0.22f, Mathf.Lerp(0.55f, 1f, beat), lit);
                _marks[i].color = new Color(Gold.r, Gold.g, Gold.b, Mathf.Max(0.22f, a));
            }
        }

        static SpriteRenderer Disc(Transform parent, string name, float size, Color color,
                                   int order, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
