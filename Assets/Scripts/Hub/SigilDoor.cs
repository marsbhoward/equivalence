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
    /// HELD OPEN FOR THE DAY once the player has run today (<see cref="HoldOpen"/>, the user's call,
    /// 2026-10-14): the doors stay parted with the light through them until the daily reset at
    /// 00:00 UTC shuts them again - the room says "you have played today" without a word. Fully
    /// parted, not ajar, so the marks still read as torn rather than striped.
    ///
    /// STONE FOR THE NEW WEEK (<see cref="SetStone"/>, the user's call, 2026-10-14): after each
    /// weekly reset no mark is lit - plain carved stone - until the player uses the sigil selector,
    /// which wakes them.
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
        /// <summary>Each leaf's hinge chain, outer edge first: a rotation, a scale and a rotation -
        /// together the shear a leaf swinging toward the room draws as (see <see cref="Swing"/>).
        /// Index 0 the left leaf, 1 the right.</summary>
        Transform[] _hingeOuter, _hingeMid, _hingeInner;
        SpriteRenderer _light, _slabL, _slabR;
        [SerializeField] DoorSlabs _slabs;
        [SerializeField] Convergence.Art.Lighting.Glow _glow;
        // The light's rays fall only for the room - the stake screen's doorway is dark (Stage).
        internal float OpenShare => _staged ? 0f : _openT;

        // Two renderers per mark - the left half on the left door, the right half on the right.
        // Mark i lives at [2i] and [2i+1]. NOT readonly: a domain reload cannot write back into a
        // readonly field, and these are dereferenced every frame. See the rule in CLAUDE.md.
        SpriteRenderer[] _grooves = new SpriteRenderer[8];
        SpriteRenderer[] _lips = new SpriteRenderer[8];

        ElementType[] _order;
        Color _tint;
        float _lit, _openT;
        bool _heldOpen, _stone;

        /// <summary>The sigils stand as plain stone - a new week not yet woken at the selector.</summary>
        public bool IsStone => _stone;

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

            // The whole doorway: the leaves swing clear of it (Swing), so the light fills it.
            d._light = Quad(go.transform, "light", halfW * 2f, h,
                            Color.white, SortingOrders.GroundDecal + 2);
            d._light.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);

            // No glare or halo (the user's rule, group 6): the light through the gap pools in the dim
            // hub as a Glow, and falls out across the floor as RAYS (the user's pick).
            d._glow = Convergence.Art.Lighting.Glow.Attach(go, 3.2f, Color.white, 0f);

            // Each door's ORIGIN sits on the seam, so a half-glyph pivoted at its own seam edge
            // lands exactly on the join and the whole thing slides as one piece.
            // FRENCH DOORS: each leaf hangs from a hinge chain on its OUTER edge, the leaf's own
            // origin still on the seam, so everything cut across the seam swings with it.
            d._hingeOuter = new Transform[2]; d._hingeMid = new Transform[2]; d._hingeInner = new Transform[2];
            for (int i = 0; i < 2; i++)
                (d._hingeOuter[i], d._hingeMid[i], d._hingeInner[i]) = HingeChain(go.transform, i == 0 ? -1 : 1, halfW);
            d._doorL = Leaf(d._hingeInner[0], "door-l");
            d._doorL.localPosition = new Vector3(halfW, 0f, 0f);
            d._doorR = Leaf(d._hingeInner[1], "door-r");
            d._doorR.localPosition = new Vector3(-halfW, 0f, 0f);
            d._slabL = Slab(d._doorL, -1, halfW, h);
            d._slabR = Slab(d._doorR, +1, halfW, h);
            d._slabs = DoorSlabs.Attach(go, d._doorL, d._doorR, halfW, h, d._slabL.sharedMaterial);

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

            // ---- light falling out across the floor: rays in a fan from the gap ----
            int rays = Mathf.CeilToInt(2.4f * Combat.TexelFx.Body);
            Combat.TexelPicture.Attach<DoorRays>(go.transform, "rays", Vector2.zero, rays, rays, 1, SortingOrders.GroundDecal);

            d.Apply(0f);
            return d;
        }

        public void SetFocus(bool on) => Focused = on;

        bool _staged, _stoneBefore;

        /// <summary>
        /// The stake screen's door (UI/StakeScreen): plain stone - no mark lit, the user's call - and
        /// swung open from SHUT while the screen is up, so the screen opens on the doors parting.
        /// Off puts the marks back as they were; the doors close or stay as focus and the day say.
        /// </summary>
        public void Stage(bool on)
        {
            if (on == _staged) return;
            _staged = on;
            if (on) { _stoneBefore = _stone; _stone = true; _openT = 0f; }
            else _stone = _stoneBefore;
            Apply(_lit);
        }

        /// <summary>Shut at once - unfocused, not held, not staged - for the hub's last picture
        /// (HubRoom.PoseForGlimpse).</summary>
        public void Shut()
        {
            Focused = false;
            _heldOpen = false;
            if (_staged) { _staged = false; _stone = _stoneBefore; }
            _openT = 0f;
            Apply(_lit);
        }

        /// <summary>How far open the doors are, 0 to 1, eased as they are drawn.</summary>
        public float OpenEased => Mathf.SmoothStep(0f, 1f, _openT);

        /// <summary>Where the stake screen's camera looks: the doors' middle, in world space.</summary>
        public Vector3 Centre => transform.position + new Vector3(0f, Tuning.Hub.DoorHeight * 0.5f, 0f);

        /// <summary>No mark lit - the week's reset, until the selector wakes them.</summary>
        public void SetStone(bool on)
        {
            if (_stone == on) return;
            _stone = on;
            Apply(_lit);
        }

        /// <summary>Holds the doors open whether or not anyone stands at them (the player has run
        /// today). <paramref name="snap"/> skips the slide - for a room drawn with them already
        /// open, rather than a reset happening while the player watches.</summary>
        public void HoldOpen(bool on, bool snap = false)
        {
            _heldOpen = on;
            if (!snap) return;
            _openT = on ? 1f : 0f;
            Apply(_lit);
        }

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
            // settle into open rather than stopping dead. Held open for the day, they stay parted
            // after the player walks away; the mark's glow still follows the focus alone.
            float open = Focused || _heldOpen || _staged ? 1f : 0f;
            // Stepped at most a twentieth of a second a frame: opening the stake screen costs a
            // long frame, and an unclamped step there skipped the swing it opens on.
            _openT = Mathf.MoveTowards(_openT, open, Mathf.Min(Time.unscaledDeltaTime, 0.05f) / Tuning.Hub.DoorSwingSeconds);

            float breath = Focused ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.4f) : 0f;
            Apply(_lit + breath * _lit * 0.35f);
        }

        void Apply(float lit)
        {
            if (_light == null || _doorL == null || _doorR == null) return;

            Swing(Tuning.Hub.DoorSwingDegrees * Mathf.SmoothStep(0f, 1f, _openT));

            // BLINDING WHITE, not the element's colour - the sketch's own label for it is the
            // "white opening". Tinting it made a fire door glow salmon, which reads as a coloured
            // panel rather than as light. The element is carried by the lit sigil instead, which
            // is also what keeps it readable while the doors are shut and there is no light at all.
            // Staged for the stake screen the doorway is the screen's pale light - the void is drawn
            // in it there, and is the brightest thing on that screen.
            _light.color = _staged ? new Color(0.80f, 0.80f, 0.84f, _openT) : new Color(1f, 1f, 1f, _openT);
            if (_glow != null) _glow.Intensity = _staged ? 0f : _openT * Mathf.Lerp(0.8f, 1f, lit);

            // The slabs take a little of the light nearest the seam once it is showing.
            // Deliberately NOT tinted by the element. Warming the slabs even 10% toward the
            // tint turned a fire door muddy brown - it stopped reading as stone, which is the one
            // thing the fixture has to be. The light through the gap carries the colour instead.

            for (int i = 0; i < _order.Length && i < 4; i++)
            {
                bool active = !_stone && _order[i] == Element;

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

        /// <summary>
        /// Swings both leaves to <paramref name="degrees"/> about their outer hinges (the user's
        /// French doors, mock "Stake D"). Seen through the tilted camera a leaf turning toward the
        /// room keeps its hinge, narrows to cos t and drops its free edge by
        /// w * sin t * DoorSwingDrop - in the hinge's frame, with x running toward the seam, the
        /// matrix [[cos t, 0], [-sin t * drop, 1]]. A transform cannot shear, so the matrix is
        /// split (closed-form 2x2 SVD) into rotate * scale * rotate over the three hinge nodes;
        /// its determinant is cos t, positive below 90 degrees, so neither scale flips.
        /// </summary>
        void Swing(float degrees)
        {
            if (_hingeOuter == null || _hingeOuter.Length < 2 || _hingeOuter[0] == null) return;
            for (int i = 0; i < 2; i++)
                SwingHinge(_hingeOuter[i], _hingeMid[i], _hingeInner[i], i == 0 ? -1f : 1f, degrees);
            if (_slabs != null) _slabs.Pose(degrees);
        }

        /// <summary>A leaf's hinge chain on its OUTER edge (<paramref name="side"/> -1 left, +1
        /// right): rotate, scale, rotate - hang the leaf under the last, its seam
        /// <paramref name="halfW"/> in from the hinge. Shared with the arena's FloorDoor.</summary>
        public static (Transform Outer, Transform Mid, Transform Inner) HingeChain(Transform parent, int side, float halfW)
        {
            var outer = Leaf(parent, side < 0 ? "hinge-l" : "hinge-r");
            outer.localPosition = new Vector3(side * halfW, 0f, 0f);
            var mid = Leaf(outer, "scale");
            var inner = Leaf(mid, "turn");
            return (outer, mid, inner);
        }

        /// <summary>Poses one leaf's hinge chain at <paramref name="degrees"/> - see <see cref="Swing"/>.</summary>
        public static void SwingHinge(Transform outer, Transform mid, Transform inner, float side, float degrees)
        {
            if (outer == null || mid == null || inner == null) return;
            float t = degrees * Mathf.Deg2Rad;
            // Left leaf: x toward the seam is +x, the free edge drops as x grows. Right: mirrored.
            float a = Mathf.Cos(t), b = 0f, cc = side * Mathf.Sin(t) * Tuning.Hub.DoorSwingDrop, dd = 1f;
            float e = (a + dd) * 0.5f, f = (a - dd) * 0.5f, g = (cc + b) * 0.5f, h = (cc - b) * 0.5f;
            float q = Mathf.Sqrt(e * e + h * h), r = Mathf.Sqrt(f * f + g * g);
            float a1 = Mathf.Atan2(g, f), a2 = Mathf.Atan2(h, e);
            float theta = (a2 - a1) * 0.5f, phi = (a2 + a1) * 0.5f;
            outer.localRotation = Quaternion.Euler(0f, 0f, phi * Mathf.Rad2Deg);
            mid.localScale = new Vector3(q + r, q - r, 1f);
            inner.localRotation = Quaternion.Euler(0f, 0f, theta * Mathf.Rad2Deg);
        }

        // ------------------------------------------------------------------ construction

        internal static Transform Leaf(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }

        /// <summary>One stone door, hung so its own inner edge sits on the leaf's origin - the
        /// seam - with a darker meeting face so the pair reads as two doors rather than one panel
        /// with a line drawn on it. Lit from the room's upper left: the left face a shade lighter,
        /// the right a shade darker (<see cref="LeafStone"/>; its edge and top, DoorSlabs).</summary>
        static SpriteRenderer Slab(Transform leaf, int side, float w, float h)
        {
            var slab = Quad(leaf, "slab", w, h, LeafStone(side), SortingOrders.GroundDecal + 3);
            slab.transform.localPosition = new Vector3(side * w * 0.5f, h * 0.5f, 0f);

            var edge = Quad(leaf, "edge", 0.032f, h, StoneDeep, SortingOrders.GroundDecal + 4);
            edge.transform.localPosition = new Vector3(side * 0.016f, h * 0.5f, 0f);
            return slab;
        }

        /// <summary>A leaf's face: the stone, lit or shaded by which way the leaf turns from the room's
        /// upper-left light (the user's pick L2 / S2, 2026-11-03). Shared with the arena's FloorDoor.</summary>
        public static Color LeafStone(int side)
        {
            float k = side < 0 ? Tuning.Hub.DoorLeafLit : Tuning.Hub.DoorLeafShade;
            return new Color(Stone.r * k, Stone.g * k, Stone.b * k);
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

    /// <summary>The sigil door's light falling out across the floor as a fan of rays (the user's pick,
    /// group 6), as far open as the door is.</summary>
    public class DoorRays : Combat.TexelPicture
    {
        [SerializeField] SigilDoor _d;

        protected override bool Draw()
        {
            if (_d == null) _d = GetComponentInParent<SigilDoor>();
            float open = _d != null ? _d.OpenShare : 0f;
            if (open <= 0.01f) return false;
            int len = Cy;
            var c = new Color32(255, 248, 232, 255);
            for (int y = 1; y <= len; y++)
            {
                float spread = y * 0.9f + 2f;
                for (int x = -Mathf.CeilToInt(spread); x <= spread; x++)
                {
                    if ((Mathf.FloorToInt((Mathf.Atan2(x, y) + 3f) * 9f) & 1) != 0) continue;
                    Over(x, -y, c, 0.2f * open * (1f - y / (float)len));
                }
            }
            return true;
        }
    }
}
