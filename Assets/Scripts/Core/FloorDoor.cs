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
    /// NO ILLUMINATION. The hub door's white light, glare and halo all sell the idea that there is
    /// a lit room on the far side of the gap - true there, false here. This is furniture standing
    /// on open arena ground with nothing behind it, so none of that carries over: no light panel
    /// in the gap, no glare, no halo, no floor spill. What is left is the stone and the mark, and
    /// the mark still carries the colour on its own - the hub door's own rule that "the selected
    /// mark stays lit whether the doors are shut or open" is exactly what makes that legible with
    /// no light behind it at all.
    ///
    /// Proximity is checked directly rather than through a trigger collider, matching how every
    /// other hit-radius check in this project already works.
    /// </summary>
    public class FloorDoor : MonoBehaviour
    {
        public const float TriggerRadius = 1.0f;

        // Matches Tuning.Hub.DoorHalfWidth / DoorHeight and SigilDoor's own OpenGap - the same
        // fixture, not a re-proportioned copy of it.
        const float HalfWidth = 0.45f;
        const float Height = 1.95f;
        const float OpenGap = 0.15f;

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
        float _openT = -1f;
        System.Collections.Generic.List<SpriteRenderer> _grooves = new();

        public static FloorDoor Spawn(Vector2 pos, Transform player, Transform parent,
                                      ElementType element, System.Action onPassed, bool shut = false)
        {
            var go = new GameObject("FloorDoor");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var frame = Quad(go.transform, "frame", HalfWidth * 2f + 0.20f, Height + 0.16f,
                             Frame, SortingOrders.GroundDecal + 1);
            frame.transform.localPosition = new Vector3(0f, Height * 0.5f, 0f);

            // Each leaf's origin sits on the seam, exactly as the hub door's does, so a half-mark
            // pivoted on that edge hangs off the leaf and slides with it as one piece - except
            // here nothing ever slides again once it is placed.
            var doorL = Leaf(go.transform, "door-l");
            var doorR = Leaf(go.transform, "door-r");
            Slab(doorL, -1, HalfWidth, Height);
            Slab(doorR, +1, HalfWidth, Height);

            float slide = shut ? 0f : OpenGap * 0.5f;
            doorL.localPosition = new Vector3(-slide, 0f, 0f);
            doorR.localPosition = new Vector3(slide, 0f, 0f);

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
            return d;
        }

        /// <summary>The puzzle was solved: the leaves part (the mark tearing in two with them) and
        /// the door can be walked through. Ignored on a sealed door.</summary>
        public void Open()
        {
            if (!Shut || _openT >= 0f || _sealed) return;
            _openT = 0f;
            Spr.Flash(transform.position + Vector3.up * Height * 0.5f, 1.8f, new Color(0.75f, 0.9f, 1f), 0.4f);
        }

        /// <summary>A wrong answer: the mark goes dead in the stone and the door stays shut.</summary>
        public void Seal()
        {
            if (!Shut || _sealed) return;
            _sealed = true;
            foreach (var g in _grooves) if (g != null) g.color = StoneDeep;
            Spr.Flash(transform.position + Vector3.up * Height * 0.5f, 1.4f, new Color(0.35f, 0.32f, 0.36f), 0.35f);
        }
        bool _sealed;

        const float OpenSeconds = 0.7f;

        void Update()
        {
            if (_openT >= 0f && Shut)
            {
                _openT += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_openT / OpenSeconds));
                float slide = OpenGap * 0.5f * k;
                if (_doorL) _doorL.localPosition = new Vector3(-slide, 0f, 0f);
                if (_doorR) _doorR.localPosition = new Vector3(slide, 0f, 0f);
                if (k >= 1f) Shut = false;
            }
            if (Shut || _done || _player == null) return;
            if (Vector2.Distance(transform.position, _player.position) > TriggerRadius) return;

            _done = true;

            // The one flash this keeps: confirmation that stepping through actually did
            // something, distinct from the door's own (now dark) rendering. It fires once, on
            // the frame the floor changes, rather than being anything the door emits on its own.
            Spr.Flash(transform.position, 1.2f, new Color(0.75f, 0.9f, 1f), 0.3f);
            _onPassed?.Invoke();
            Destroy(gameObject);
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
        /// with a line drawn on it.</summary>
        static void Slab(Transform leaf, int side, float w, float h)
        {
            var slab = Quad(leaf, "slab", w, h, Stone, SortingOrders.GroundDecal + 3);
            slab.transform.localPosition = new Vector3(side * w * 0.5f, h * 0.5f, 0f);

            var edge = Quad(leaf, "edge", 0.032f, h, StoneDeep, SortingOrders.GroundDecal + 4);
            edge.transform.localPosition = new Vector3(side * 0.016f, h * 0.5f, 0f);
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
