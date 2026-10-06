using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The element selector, mounted on the wall to the right of the door. Four gold rings in a
    /// diamond, genuinely INTERLOCKING - their rims cross, the way the sketch draws them - with
    /// an elemental mark cut into each. Pressing it cycles the selection; <see cref="SigilDoor"/>
    /// is told the result, it never reads this directly.
    ///
    /// The interlock is derived, not eyeballed. Four circles at distance <c>d</c> from the
    /// centre on the compass points sit <c>d*sqrt(2)</c> apart from each neighbour, so they
    /// overlap exactly when <c>r > d/sqrt(2)</c>, i.e. r &gt; 0.707d. The original layout had
    /// 0.34 rings on a 0.8-tall diamond - measurably NOT touching, a 0.06 gap between the top
    /// ring and its neighbours, which is why it read as four separate buttons rather than the one
    /// interlocked device that was drawn.
    ///
    /// OPPOSITE rings still must not meet: they sit <c>2d</c> apart, so the pair stays clear
    /// while <c>r &lt; d</c>. At the current 0.24 / 0.94 they clear by 0.029 - the four stay four
    /// rather than fusing into a single blob with lobes.
    ///
    /// There is no plate and no recess: the sketch is four rings on bare wall, and the wall is
    /// already stone.
    /// </summary>
    public class WallButton : MonoBehaviour
    {
        public ElementType Element { get; private set; }
        public Vector2 Anchor { get; private set; }
        public bool Focused { get; private set; }

        ElementType[] _order;
        System.Action<ElementType> _onChanged;

        // NOT readonly - see SigilDoor and every other array on a MonoBehaviour in this project.
        SpriteRenderer[] _rings = new SpriteRenderer[4];
        SpriteRenderer[] _glyphs = new SpriteRenderer[4];
        float _lit;

        /// <summary>Distance from the cluster's centre to each ring's centre.</summary>
        const float Spread = 0.24f;

        /// <summary>
        /// Ring radius as a fraction of <see cref="Spread"/>. Must exceed 0.707 or the rings stop
        /// touching at all - see the class note - but merely clearing it is not the same as
        /// reading as interlocked. At 0.75 the rims crossed by 0.026 of a unit, which is a
        /// hairline: technically overlapping, visually four circles kissing. Pulling the centres
        /// in to 0.24 while holding the ring size gives 0.112 of overlap - the same rings, wound
        /// tighter into each other.
        /// </summary>
        const float RingRatio = 0.94f;

        static readonly Color PureGold = new(0.91f, 0.73f, 0.24f);
        static readonly Color MarkDark = new(0.09f, 0.09f, 0.11f);

        /// <summary>
        /// Compass positions, ordered to match the sketch against the project's own element
        /// order (Fire, Water, Earth, Air): fire on top, water below it, earth left, air right.
        /// </summary>
        static readonly Vector2[] Layout =
        {
            new(0f, Spread), new(0f, -Spread), new(-Spread, 0f), new(Spread, 0f),
        };

        /// <param name="x">Position along the north wall.</param>
        /// <param name="wallY">The wall line - the cluster sits up the face from here.</param>
        public static WallButton Build(Transform parent, ElementType[] order, ElementType initial,
                                       System.Action<ElementType> onChanged, float x, float wallY)
        {
            var go = new GameObject("wall-button");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, wallY + Tuning.Hub.DoorHeight * 0.5f, 0f);

            var b = go.AddComponent<WallButton>();
            b._order = order;
            b.Element = initial;
            b._onChanged = onChanged;
            b.Anchor = new Vector2(x, wallY - 1.0f);

            float ringSize = Spread * RingRatio * 2f;   // localScale is a DIAMETER here

            // Orders are based on GroundDecal, not FloorDetail: FloorDetail + 4 IS GroundDecal
            // (-4), so the marks used to share an order with every ring painted on the floor.
            //
            // The mark is HALF the ring rather than 0.55 of it, and that is set by the interlock
            // rather than by taste: a neighbouring ring's near rim sits 0.1134 from this ring's
            // centre, so at 0.55 a mark's own half-width of 0.124 crossed it. Wound this tightly,
            // the marks have to give way or every one of them cuts through the ring beside it.

            for (int i = 0; i < order.Length && i < 4; i++)
            {
                var ringGo = new GameObject($"ring-{order[i]}");
                ringGo.transform.SetParent(go.transform, false);
                ringGo.transform.localPosition = Layout[i];
                ringGo.transform.localScale = Vector3.one * ringSize;
                b._rings[i] = ringGo.AddComponent<SpriteRenderer>();
                b._rings[i].sprite = Spr.ThinRing;
                b._rings[i].sortingOrder = SortingOrders.GroundDecal + 1;

                var glyphGo = new GameObject($"glyph-{order[i]}");
                glyphGo.transform.SetParent(go.transform, false);
                glyphGo.transform.localPosition = Layout[i];
                glyphGo.transform.localScale = Vector3.one * (ringSize * 0.50f);
                b._glyphs[i] = glyphGo.AddComponent<SpriteRenderer>();
                b._glyphs[i].sprite = Combat.Glyphs.Element(order[i]);
                b._glyphs[i].sortingOrder = SortingOrders.GroundDecal + 2;
            }

            b.Apply(0f);
            return b;
        }

        public void SetFocus(bool on) => Focused = on;

        /// <summary>Advance to the next element and tell whoever is listening.</summary>
        public void Cycle()
        {
            int i = System.Array.IndexOf(_order, Element);
            Element = _order[(i + 1) % _order.Length];
            Apply(_lit);
            _onChanged?.Invoke(Element);
        }

        void Update()
        {
            if (_rings == null || _rings.Length == 0 || _rings[0] == null) return;
            float target = Focused ? 1f : 0f;
            _lit = Mathf.MoveTowards(_lit, target, Time.unscaledDeltaTime * 5f);
            Apply(_lit);
        }

        void Apply(float lit)
        {
            for (int i = 0; i < _order.Length && i < 4; i++)
            {
                if (_rings[i] == null) continue;
                bool active = _order[i] == Element;

                // The rings stay pure gold whatever is selected - dimming the RING would read as
                // "three switched off", so the button always shows four real options and it is
                // the MARK inside that says which one is chosen.
                _rings[i].color = Color.Lerp(PureGold, Color.Lerp(PureGold, Color.white, 0.35f), lit);

                _glyphs[i].color = active
                    ? Color.Lerp(PureGold, Color.white, Mathf.Lerp(0f, 0.45f, lit))
                    : new Color(MarkDark.r, MarkDark.g, MarkDark.b, 0.95f);
            }
        }
    }
}
