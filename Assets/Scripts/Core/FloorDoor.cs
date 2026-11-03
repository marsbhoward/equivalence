using UnityEngine;

namespace Convergence.Core
{
    /// <summary>
    /// The one deliberate crack in "everything is locked mid-run": after a floor's reward is
    /// taken, this appears instead of the next floor spawning on a timer, and reordering the
    /// finisher wheel opens in the character sheet until the player reaches it. Passing through
    /// is what actually advances the floor - a spatial beat with real weight, the same reasoning
    /// behind every other threshold in this project, rather than one more modal stacked on the
    /// reward screen.
    ///
    /// Cosmetically this is the hub's own sigil door (Hub/SigilDoor.cs) - two stone leaves with
    /// an element mark carved across the seam - carried over rather than invented fresh, since
    /// this project already has a fixture whose whole job is "the way out of this room, marked
    /// with an element." Two things differ from the hub original, both deliberate:
    ///
    /// ALREADY OPEN. The hub door shuts and parts because the player walks up and chooses to use
    /// it, which is worth watching happen. Here there is nothing to choose - the door exists only
    /// because the floor is already cleared - so it stands parted from the moment it is spawned
    /// rather than animating open for nobody.
    ///
    /// LIGHT THROUGH THE GAP (the user's pick, group 4 - 2026-10-26; it had none): the hub door's
    /// blinding white, and only that - no glare, halo or floor spill. Opening (a puzzle solved)
    /// the seam glows white first, then the leaves part on the light and dust sifts down from the
    /// lintel. A solved puzzle's door uses this and nothing else - no second flash. Sealing, the
    /// mark dies and grey dust falls down the seam.
    ///
    /// Proximity is checked directly rather than through a trigger collider, matching how every
    /// other hit-radius check in this project already works.
    /// </summary>
    public class FloorDoor : MonoBehaviour
    {
        public const float TriggerRadius = 1.0f;

        // Matches Tuning.Hub.DoorHalfWidth / DoorHeight - the same fixture, not a re-proportioned
        // copy of it - and it opens the same way: FRENCH DOORS (SigilDoor.SwingHinge).
        const float HalfWidth = 0.45f;
        const float Height = 1.95f;
        const float SeamGap = 0.15f;

        static readonly Color Stone = new(0.30f, 0.31f, 0.36f);
        static readonly Color StoneDeep = new(0.19f, 0.20f, 0.24f);
        static readonly Color Frame = new(0.13f, 0.135f, 0.17f);

        // The two tones an INCISION is made of - see SigilDoor's own note. A mark drawn as a
        // single flat shape sits on the surface; what makes it read as cut is a shaded groove
        // with the opposite wall of that groove catching the light, offset by a hair. There is no
        // "unlit" tone here the way the hub door has one for its unselected marks - this door
        // only ever carves the one element the run is being played as, so the groove is always
        // the tinted, lit colour.
        static readonly Color CutLip = new(0.46f, 0.47f, 0.53f);
        const float CutDepth = 0.010f;

        Transform _player;
        System.Action _onPassed;
        bool _done;

        /// <summary>
        /// A puzzle floor's door: SHUT, leaves together and the mark whole, until the puzzle is
        /// solved (<see cref="Open"/>) - or SEALED for good on a wrong answer (<see cref="Seal"/>).
        /// The hub door's rule carries over: shut, the carved mark reads as one; parting tears it.
        /// </summary>
        public bool Shut { get; private set; }
        Transform _doorL, _doorR;
        /// <summary>Each leaf's hinge chain on its outer edge, left then right (SigilDoor.HingeChain).</summary>
        Transform[] _hinge = new Transform[6];
        /// <summary>The leaves' edges and tops (the hub door's slabs, Hub.DoorSlabs).</summary>
        [SerializeField] Hub.DoorSlabs _slabs;
        SpriteRenderer _gap;
        float _openT = -1f;
        System.Collections.Generic.List<SpriteRenderer> _grooves = new();

        /// <param name="inWall">Cut into the north wall's face at <paramref name="pos"/>'s x (its y
        /// is ignored) and WALKED THROUGH - the user's call, 2026-11-01. False: standing on the
        /// floor at <paramref name="pos"/>, passed by coming near (the labyrinth's, in its maze).</param>
        public static FloorDoor Spawn(Vector2 pos, Transform player, Transform parent,
                                      ElementType element, System.Action onPassed, bool shut = false,
                                      bool inWall = false)
        {
            if (inWall) pos.y = Arena.NorthWallFoot;
            var go = new GameObject("FloorDoor");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var frame = Quad(go.transform, "frame", HalfWidth * 2f + 0.20f, Height + 0.16f,
                             Frame, SortingOrders.GroundDecal + 1);
            frame.transform.localPosition = new Vector3(0f, Height * 0.5f, 0f);

            // Each leaf's origin sits on the seam, exactly as the hub door's does, so a half-mark
            // pivoted on that edge hangs off the leaf and slides with it as one piece - except
            // here nothing ever slides again once it is placed.
            // The light through the gap, between the frame and the leaves; sized each frame.
            var gap = Quad(go.transform, "gap", SeamGap, Height, Color.white, SortingOrders.GroundDecal + 2);
            gap.transform.localPosition = new Vector3(0f, Height * 0.5f, 0f);

            // Hung from hinges on their OUTER edges, the hub door's French doors.
            var (lo, lm, li) = Hub.SigilDoor.HingeChain(go.transform, -1, HalfWidth);
            var (ro, rm, ri) = Hub.SigilDoor.HingeChain(go.transform, +1, HalfWidth);
            var doorL = Leaf(li, "door-l");
            doorL.localPosition = new Vector3(HalfWidth, 0f, 0f);
            var doorR = Leaf(ri, "door-r");
            doorR.localPosition = new Vector3(-HalfWidth, 0f, 0f);
            var slabL = Slab(doorL, -1, HalfWidth, Height);
            Slab(doorR, +1, HalfWidth, Height);
            var slabs = Hub.DoorSlabs.Attach(go, doorL, doorR, HalfWidth, Height, slabL.sharedMaterial);


            // The run's own element, carved across the seam. Sized and cut exactly the way the
            // hub door builds its own marks - see SigilDoor.Half/Mark - just the one mark rather
            // than four stacked, since this door has nothing else to offer a choice between.
            var whole = Combat.Glyphs.Element(element);
            var tint = ElementInfo.Tint(element);
            float my = Height * 0.5f;
            float markSize = Mathf.Min(Height * 0.5f, HalfWidth * 2f * 0.62f);
            var lit = Color.Lerp(CutLip, Color.white, 0.30f);
            var groove = new Color(tint.r, tint.g, tint.b, 1f);

            var d = go.AddComponent<FloorDoor>();
            d._inWall = inWall;
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                var leaf = left ? doorL : doorR;
                var half = Half(whole, left);
                string tag = left ? "l" : "r";

                Mark(leaf, $"mark-{tag}-lip", half, markSize,
                    new Vector3(CutDepth, my - CutDepth, 0f), SortingOrders.GroundDecal + 6, lit);
                d._grooves.Add(Mark(leaf, $"mark-{tag}", half, markSize,
                    new Vector3(0f, my, 0f), SortingOrders.GroundDecal + 7, groove));
            }

            d._player = player;
            d._onPassed = onPassed;
            d.Shut = shut;
            d._doorL = doorL;
            d._doorR = doorR;
            d._hinge = new[] { lo, lm, li, ro, rm, ri };
            d._slabs = slabs;
            d._gap = gap;
            d.Part(shut ? 0f : 1f);
            if (!shut) Convergence.Art.Lighting.Glow.Attach(go, inWall ? 2.2f : 1.4f, Color.white, inWall ? 0.75f : 0.5f);
            d.SyncGap();
            return d;
        }

        bool _inWall;

        /// <summary>Open by <paramref name="k"/> (0 shut, 1 standing open): the leaves swing on their
        /// outer hinges like the hub door's (the user's call, 2026-11-02), on the floor and in the
        /// wall alike, and the light fills the doorway behind them.</summary>
        void Part(float k)
        {
            if (_hinge != null && _hinge.Length == 6)
            {
                float deg = Tuning.Hub.DoorSwingDegrees * k;
                Hub.SigilDoor.SwingHinge(_hinge[0], _hinge[1], _hinge[2], -1f, deg);
                Hub.SigilDoor.SwingHinge(_hinge[3], _hinge[4], _hinge[5], +1f, deg);
                if (_slabs != null) _slabs.Pose(deg);
            }
            SetGap(k > 0f ? HalfWidth * 2f : 0f, 1f);
        }

        /// <summary>An open door in the wall holds a doorway in the north wall's collider
        /// (Core.NorthWall); shut, gone or on the floor, it holds none.</summary>
        void SyncGap()
        {
            if (_inWall && !Shut && !_done && isActiveAndEnabled) Arena.OpenNorthGap(GetHashCode(), transform.position.x);
            else Arena.CloseNorthGap(GetHashCode());
        }

        void OnEnable() => SyncGap();
        void OnDisable() => Arena.CloseNorthGap(GetHashCode());

        void SetGap(float width, float alpha)
        {
            if (_gap == null) return;
            _gap.transform.localScale = new Vector3(Mathf.Max(width, 0.0001f), Height, 1f);
            _gap.color = new Color(1f, 1f, 1f, width > 0f ? alpha : 0f);
        }

        /// <summary>The puzzle was solved: the leaves part (the mark tearing in two with them) and
        /// the door can be walked through. Ignored on a sealed door.</summary>
        public void Open()
        {
            if (Shut && _openT < 0f && !_sealed) Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.DoorOpen, transform.position);
            if (!Shut || _openT >= 0f || _sealed) return;
            _openT = 0f;
        }

        /// <summary>A wrong answer: the mark goes dead in the stone and the door stays shut.</summary>
        public void Seal()
        {
            if (!Shut || _sealed) return;
            _sealed = true;
            _sealT = 0f;
            foreach (var g in _grooves) if (g != null) _markLit = g.color;
            Combat.TexelFx.Sift((Vector2)transform.position + Vector2.up * (Height * 0.5f), Combat.TexelFx.Grey, 0.03f, Height * 0.5f, 10, 1f, SortingOrders.GroundDecal + 8);
        }
        bool _sealed;
        float _sealT = -1f;
        Color _markLit;
        const float SeamSeconds = 0.2f;

        /// <summary>The swing, the hub door's pace (stone is heavy).</summary>
        static float OpenSeconds => Tuning.Hub.DoorSwingSeconds;

        void Update()
        {
            // Sealing: the mark goes dark over a moment rather than at once.
            if (_sealT >= 0f && _sealT < 0.5f)
            {
                _sealT += Time.deltaTime;
                var c = Color.Lerp(_markLit, StoneDeep, Mathf.Clamp01(_sealT / 0.4f));
                foreach (var g in _grooves) if (g != null) g.color = c;
            }
            if (_openT >= 0f && Shut)
            {
                float before = _openT;
                _openT += Time.deltaTime;
                // The seam glows white first, a texel wide, then the leaves part on the light.
                if (_openT < SeamSeconds) SetGap(1f / Combat.TexelFx.Body, Mathf.Clamp01(_openT / SeamSeconds));
                else
                {
                    if (before < SeamSeconds)
                    {
                        Combat.TexelFx.Sift((Vector2)transform.position + Vector2.up * Height, Combat.TexelFx.Grey, HalfWidth, 0.7f, 14, 1f, SortingOrders.GroundDecal + 8);
                        Convergence.Art.Lighting.Glow.Attach(gameObject, 1.4f, Color.white, 0.5f);
                    }
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((_openT - SeamSeconds) / OpenSeconds));
                    Part(k);
                    if (k >= 1f) { Shut = false; SyncGap(); }
                }
            }
            if (Shut || _done || _player == null) return;
            if (_inWall)
            {
                // Walked INTO the doorway, past the face's foot - not merely up to it.
                Vector2 d = (Vector2)_player.position - (Vector2)transform.position;
                if (d.y < Tuning.Door.WalkInDepth || Mathf.Abs(d.x) > Tuning.Door.GapHalfWidth) return;
            }
            else if (Vector2.Distance(transform.position, _player.position) > TriggerRadius) return;

            _done = true;

            // Stepping through stirs the dust at the threshold - the confirmation, once, on the
            // frame the floor changes.
            Combat.TexelFx.Puff(transform.position, Combat.TexelFx.Grey, 10, 8f, 0.5f);
            // Not destroyed here: the caller takes it down once the screen is black, or it vanished
            // in plain sight a moment before the fade.
            _onPassed?.Invoke();
        }

        // ------------------------------------------------------------------ construction

        static Transform Leaf(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }

        /// <summary>One stone leaf, hung so its own inner edge sits on the parent's origin - the
        /// seam - with a darker meeting face so the pair reads as two doors rather than one panel
        /// with a line drawn on it. Lit like the hub door's (SigilDoor.LeafStone).</summary>
        static SpriteRenderer Slab(Transform leaf, int side, float w, float h)
        {
            var slab = Quad(leaf, "slab", w, h, Hub.SigilDoor.LeafStone(side), SortingOrders.GroundDecal + 3);
            slab.transform.localPosition = new Vector3(side * w * 0.5f, h * 0.5f, 0f);

            var edge = Quad(leaf, "edge", 0.032f, h, StoneDeep, SortingOrders.GroundDecal + 4);
            edge.transform.localPosition = new Vector3(side * 0.016f, h * 0.5f, 0f);
            return slab;
        }

        static SpriteRenderer Mark(Transform leaf, string name, Sprite sprite, float size,
                         Vector3 at, int order, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(leaf, false);
            go.transform.localPosition = at;
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Half a glyph, cut down the middle, pivoted on the CUT edge so it hangs off the
        /// seam - identical to SigilDoor's own Half, duplicated rather than shared across the
        /// Hub/Core boundary for one small helper.</summary>
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
}
