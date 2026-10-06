using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The one door into the arena, built from the sketch: a PAIR of stone doors with the four
    /// elemental sigils carved down them, only the selected one lit.
    ///
    /// It stands on the NORTH wall because that is the only wall with a drawn FACE - the gallery
    /// band, the standard top-down cheat, since a wall seen from directly above is a line. Built
    /// first on the west wall, where there is no face, "marks on the door" had nowhere to go and
    /// became a stripe of marks running along the wall beside the opening.
    ///
    /// THE DOORS ARE SHUT until the player comes to use them, and that is what makes the marks
    /// read as carved rather than floating. Each sigil is cut ACROSS the seam - so while the
    /// doors are closed it is one whole mark on one continuous stone face, and each half is a
    /// child of its own door. Walking up parts them, and the mark tears in two with them. An
    /// earlier version left the doors permanently ajar with a hairline of light between: the
    /// marks were technically bisected and still read as whole glyphs with a stripe drawn over
    /// them, because nothing ever moved to show the split was real.
    ///
    /// They never open onto anything walkable - the wall behind stays solid and the door is used
    /// from the floor in front of it. Selecting is <see cref="WallButton"/>'s job; this class
    /// only ever draws what it is told.
    /// </summary>
    public class SigilDoor : MonoBehaviour
    {
        public ElementType Element { get; private set; }

        /// <summary>Where the character stands to use the door.</summary>
        public Vector2 Anchor { get; private set; }

        public bool Focused { get; private set; }

        Transform _doorL, _doorR;
        SpriteRenderer _light, _glare, _halo, _spill, _slabL, _slabR;

        // Two renderers per mark - the left half on the left door, the right half on the right.
        // Mark i lives at [2i] and [2i+1]. NOT readonly: a domain reload cannot write back into a
        // readonly field, and these are dereferenced every frame. See the rule in CLAUDE.md.
        SpriteRenderer[] _grooves = new SpriteRenderer[8];
        SpriteRenderer[] _lips = new SpriteRenderer[8];

        ElementType[] _order;
        Color _tint;
        float _lit, _openT;

        static readonly Color Stone = new(0.30f, 0.31f, 0.36f);
        static readonly Color StoneDeep = new(0.19f, 0.20f, 0.24f);
        static readonly Color Frame = new(0.13f, 0.135f, 0.17f);

        // The two tones an INCISION is made of. A mark drawn as a single flat shape sits on the
        // surface however dark it is tinted; what makes it read as cut is a shaded groove with the
        // opposite wall of that groove catching the light, offset by a hair.
        static readonly Color CutShade = new(0.115f, 0.12f, 0.15f);
        static readonly Color CutLip = new(0.46f, 0.47f, 0.53f);

        /// <summary>How far the lit wall of the groove is offset from the shaded one. At 0.018 on
        /// a 0.4 mark it separates into a visible second glyph and reads as a drop shadow.</summary>
        const float CutDepth = 0.010f;

        /// <summary>Total parting when fully open - each door slides half of it.</summary>
        const float OpenGap = 0.15f;

        /// <param name="x">Position along the north wall.</param>
        /// <param name="wallY">The wall line - the doors stand up the face from here.</param>
        public static SigilDoor Build(Transform parent, ElementType[] order, ElementType initial,
                                      float x, float wallY)
        {
            float halfW = Tuning.Hub.DoorHalfWidth;
            float h = Tuning.Hub.DoorHeight;

            var go = new GameObject("sigil-door");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, wallY, 0f);

            var d = go.AddComponent<SigilDoor>();
            d._order = order;
            d.Element = initial;
            d._tint = ElementInfo.Tint(initial);
            d.Anchor = new Vector2(x, wallY - 1.0f);

            // Surround, then the light that only shows once the doors part, then the doors.
            //
            // Based on GroundDecal rather than FloorDetail: the old stack put the slabs on
            // FloorDetail + 4, which IS GroundDecal (-4), so the doors shared an order with every
            // ring and mark painted on the floor and would z-fight anything that overlapped them.
            var frame = Quad(go.transform, "frame", halfW * 2f + 0.20f, h + 0.16f,
                             Frame, SortingOrders.GroundDecal + 1);
            frame.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);

            d._light = Quad(go.transform, "light", OpenGap * 1.15f, h,
                            Color.white, SortingOrders.GroundDecal + 2);
            d._light.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);

            // Heights are capped so the bloom stays INSIDE the wall band: at 1.45x the door the
            // halo reached world y 5.44 against a band that ends at 5.15, so light spilled past
            // the top of the room into the letterbox.
            //
            // Glare, over the slabs rather than behind them. Light that stops dead at the edge of
            // the gap is a lit strip between two doors; light that washes over the stone either
            // side of it is a door with something blinding behind it. Two falloffs rather than
            // one, because a single blob has to choose between a tight hot core and a wide
            // atmospheric bleed and neither alone reads as glare.
            d._glare = Glow(go.transform, "glare", OpenGap * 4.5f, h * 1.06f, h * 0.5f,
                            SortingOrders.GroundDecal + 5);
            d._halo = Glow(go.transform, "halo", OpenGap * 11f, h * 1.15f, h * 0.5f,
                           SortingOrders.GroundDecal + 5);

            // Each door's ORIGIN sits on the seam, so a half-glyph pivoted at its own seam edge
            // lands exactly on the join and the whole thing slides as one piece.
            d._doorL = Leaf(go.transform, "door-l");
            d._doorR = Leaf(go.transform, "door-r");
            d._slabL = Slab(d._doorL, -1, halfW, h);
            d._slabR = Slab(d._doorR, +1, halfW, h);

            // ---- the four marks, cut across the seam ----
            //
            // Sized to fit rather than by preference: four have to stack inside the door's own
            // height, so the spacing sets the mark and not the other way round.
            float step = h / order.Length;
            float markSize = Mathf.Min(step * 0.82f, halfW * 2f * 0.62f);

            for (int i = 0; i < order.Length && i < 4; i++)
            {
                // order[0] at the TOP, reading down, the way the sketch stacks them.
                float my = h - step * (i + 0.5f);
                var whole = Combat.Glyphs.Element(order[i]);

                for (int side = 0; side < 2; side++)
                {
                    bool left = side == 0;
                    var leaf = left ? d._doorL : d._doorR;
                    var half = Half(whole, left);
                    int slot = i * 2 + side;
                    string tag = left ? "l" : "r";

                    // The lit wall of the cut, offset down-right, then the groove itself on top
                    // at true position. Light in this room reads as coming from the upper left,
                    // so the far wall of an incision is the one that catches it. Both halves take
                    // the SAME offset - the cut runs continuously across the closed pair.
                    d._lips[slot] = Mark(leaf, $"mark{i}-{tag}-lip", half, markSize,
                                         new Vector3(CutDepth, my - CutDepth, 0f),
                                         SortingOrders.GroundDecal + 6);
                    d._grooves[slot] = Mark(leaf, $"mark{i}-{tag}", half, markSize,
                                            new Vector3(0f, my, 0f),
                                            SortingOrders.GroundDecal + 7);
                }
            }

            // ---- light spilling onto the floor at the threshold ----
            //
            // A ground decal, not part of the door, and tied to how far it is OPEN: a shut stone
            // door does not leak onto the floor in front of it.
            var spillGo = new GameObject("spill");
            spillGo.transform.SetParent(go.transform, false);
            spillGo.transform.localPosition = Vector3.zero;
            spillGo.transform.localScale = new Vector3(halfW * 2.2f, halfW * 2.2f, 1f);
            d._spill = spillGo.AddComponent<SpriteRenderer>();
            d._spill.sprite = Spr.HalfDisc;
            d._spill.sortingOrder = SortingOrders.GroundDecal;

            d.Apply(0f);
            return d;
        }

        public void SetFocus(bool on) => Focused = on;

        /// <summary>Called by the WallButton every time the selection cycles.</summary>
        public void SetElement(ElementType e)
        {
            Element = e;
            _tint = ElementInfo.Tint(e);
            Apply(_lit);
        }

        void Update()
        {
            // Guard against a mid-play script edit the same way every array note in this project
            // asks for: skip rather than throw if these came back empty.
            if (_grooves == null || _grooves.Length == 0 || _grooves[0] == null) return;
            if (_lips == null || _lips.Length < _grooves.Length) return;

            float target = Focused ? 1f : 0f;
            _lit = Mathf.MoveTowards(_lit, target, Time.unscaledDeltaTime * 5f);

            // Slower than the glow, because stone is heavy. Smoothstepped in Apply so the doors
            // settle into open rather than stopping dead.
            _openT = Mathf.MoveTowards(_openT, target, Time.unscaledDeltaTime * 3.2f);

            float breath = Focused ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.4f) : 0f;
            Apply(_lit + breath * _lit * 0.35f);
        }

        void Apply(float lit)
        {
            if (_light == null || _doorL == null || _doorR == null) return;

            float slide = OpenGap * 0.5f * Mathf.SmoothStep(0f, 1f, _openT);
            _doorL.localPosition = new Vector3(-slide, 0f, 0f);
            _doorR.localPosition = new Vector3(slide, 0f, 0f);

            // BLINDING WHITE, not the element's colour - the sketch's own label for it is the
            // "white opening". Tinting it made a fire door glow salmon, which reads as a coloured
            // panel rather than as light. The element is carried by the lit sigil instead, which
            // is also what keeps it readable while the doors are shut and there is no light at all.
            _light.color = new Color(1f, 1f, 1f, _openT);
            if (_glare != null) _glare.color = new Color(1f, 1f, 1f, _openT * 0.55f);
            if (_halo != null) _halo.color = new Color(1f, 1f, 1f, _openT * 0.26f);

            if (_spill != null)
                _spill.color = new Color(1f, 1f, 1f, _openT * Mathf.Lerp(0.22f, 0.34f, lit));

            // The slabs take a little of the light nearest the seam once it is showing.
            // Deliberately NOT tinted by the element. Warming the slabs even 10% toward the
            // tint turned a fire door muddy brown - it stopped reading as stone, which is the one
            // thing the fixture has to be. The light through the gap carries the colour instead.

            for (int i = 0; i < _order.Length && i < 4; i++)
            {
                bool active = _order[i] == Element;

                // Only the selected mark is lit - "light up sigils for each element", one at a
                // time. It stays lit whether the doors are shut or open: which element is armed
                // has to be readable from across the room, not only once you are standing at it.
                //
                // Unlit, the groove is a shadow in the stone; lit, the SAME groove fills with the
                // element's light and the lip does not move, which is what keeps a glowing mark
                // still reading as carved. The lip stays PALE rather than taking the tint - a lit
                // groove's far wall is washed by the glow, not coloured by it.
                var groove = active
                    ? new Color(_tint.r, _tint.g, _tint.b, Mathf.Lerp(0.90f, 1f, lit))
                    : CutShade;
                var lip = active ? Color.Lerp(CutLip, Color.white, 0.30f) : CutLip;

                for (int side = 0; side < 2; side++)
                {
                    int slot = i * 2 + side;
                    if (slot >= _grooves.Length || _grooves[slot] == null) continue;
                    _grooves[slot].color = groove;
                    if (_lips[slot] != null) _lips[slot].color = lip;
                }
            }
        }

        // ------------------------------------------------------------------ construction

        static Transform Leaf(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }

        /// <summary>One stone door, hung so its own inner edge sits on the leaf's origin - the
        /// seam - with a darker meeting face so the pair reads as two doors rather than one panel
        /// with a line drawn on it.</summary>
        static SpriteRenderer Slab(Transform leaf, int side, float w, float h)
        {
            var slab = Quad(leaf, "slab", w, h, Stone, SortingOrders.GroundDecal + 3);
            slab.transform.localPosition = new Vector3(side * w * 0.5f, h * 0.5f, 0f);

            var edge = Quad(leaf, "edge", 0.032f, h, StoneDeep, SortingOrders.GroundDecal + 4);
            edge.transform.localPosition = new Vector3(side * 0.016f, h * 0.5f, 0f);
            return slab;
        }

        static SpriteRenderer Mark(Transform leaf, string name, Sprite sprite, float size,
                                   Vector3 at, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(leaf, false);
            go.transform.localPosition = at;
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>
        /// Half a glyph, cut down the middle, pivoted on the CUT edge so it hangs off the seam.
        ///
        /// Built rather than cached: it is eight sprites once per room build, and a static cache
        /// would be one more thing to reason about across a domain reload for no measurable gain.
        /// The pixels-per-unit stays the FULL width, so the two halves together are exactly the
        /// size the whole glyph would have been.
        /// </summary>
        static Sprite Half(Sprite whole, bool left)
        {
            var tex = whole.texture;
            int w = tex.width, h = tex.height, halfW = w / 2;
            var rect = left ? new Rect(0f, 0f, halfW, h) : new Rect(halfW, 0f, halfW, h);
            var pivot = left ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            return Sprite.Create(tex, rect, pivot, w);
        }

        /// <summary>
        /// A soft bloom on the seam. <paramref name="centreY"/> is the DOOR's midpoint, not the
        /// bloom's own - a glow taller than the door it belongs to would otherwise centre itself
        /// on its own height and float above the opening.
        /// </summary>
        static SpriteRenderer Glow(Transform parent, string name, float w, float h, float centreY, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, centreY, 0f);
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Glow;
            sr.color = new Color(1f, 1f, 1f, 0f);
            sr.sortingOrder = order;
            return sr;
        }

        static SpriteRenderer Quad(Transform parent, string name, float w, float h, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
