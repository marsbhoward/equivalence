using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>Which way a Frame is currently standing. Never stored - see the class note.</summary>
    public enum FrameMode { Easel, Wall }

    /// <summary>
    /// One piece of showcase art - an EASEL standing on the floor, or a frame mounted flush on
    /// the gallery wall, depending on where the player puts it. Replaces two separate fixtures
    /// that used to exist for this (a floor "Trophy" and a wall "ShowcaseFrame"): a wall piece
    /// and a floor piece were always the same idea underneath - a showcase key and a spot in the
    /// room - wearing two different classes, two different placement systems, and two different
    /// prompts. Only one of them could be picked back up and moved.
    ///
    /// MODE IS DERIVED FROM POSITION, NEVER STORED. Set down against the gallery wall, a Frame
    /// reads as mounted; set down anywhere else on the floor, it reads as a freestanding easel.
    /// HubRoom.ModeFor is the one place that rule lives, and it runs both while a piece is being
    /// carried (so the ghost previews the shape it will actually take) and again on load (so a
    /// saved (X, Y) always means the same thing it meant when it was placed) - there is nothing
    /// here that could disagree with what gets drawn.
    ///
    /// This is also most of the fix for a room full of empty picture frames growing across the
    /// wall as a wallet's collection got bigger: a Frame only exists once a player has taken a
    /// specific piece out of the crate and set it down, exactly like an easel always did, rather
    /// than the wall pre-declaring one more slot than it currently holds.
    /// </summary>
    public class Frame : MonoBehaviour
    {
        /// <summary>The frame's own identity - the spot in the room - separate from
        /// <see cref="Key"/>, which is only what it happens to be showing. See
        /// RoomLayout.TrophyPlacement.Id.</summary>
        public string Id { get; private set; }

        public string Key { get; private set; }
        public string Title { get; private set; }
        public string Collection { get; private set; }
        public FrameMode Mode { get; private set; }
        public bool HasArt => _sprite != null;
        public Sprite Sprite => _sprite;

        /// <summary>Where the character stands to use this frame - always a floor point, in
        /// EITHER mode. Distinct from transform.position, which for a wall-mounted frame sits up
        /// on the gallery face instead of where the player is actually standing.</summary>
        public Vector2 Anchor { get; private set; }

        /// <summary>Footprint for the room editor's legality checks. One number for both modes -
        /// a wall frame and an easel take about the same floor space to approach and stand at.</summary>
        public const float Radius = 0.52f;

        /// <summary>Tallest an EASEL's art may stand, world units. Wide art gets narrower, not
        /// shorter - see FitArt.</summary>
        public const float ArtHeight = 1.15f;

        Sprite _sprite;
        SpriteRenderer _art, _empty;

        // wall-mode visuals
        SpriteRenderer _wBorder, _wMatte;

        // easel-mode visuals
        SpriteRenderer _ePlinth, _eCap, _eGlow, _eShadow;
        DepthSorted _depth;

        float _lit;
        bool _focused;


        /// <param name="visualAt">Where the fixture itself is drawn - the wall face for Wall mode,
        /// the floor spot for Easel mode.</param>
        /// <param name="anchor">Where the character stands to use it - always a floor point.</param>
        public static Frame Build(Transform parent, string id, in ShowcaseItem item, FrameMode mode,
                                  Vector2 visualAt, Vector2 anchor, bool solid)
        {
            var go = new GameObject($"frame-{id}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = visualAt;

            var f = go.AddComponent<Frame>();
            f.Id = id;
            f.Key = item.Key;
            f.Title = string.IsNullOrEmpty(item.Title) ? "untitled" : item.Title;
            f.Collection = item.Collection;
            f.Anchor = anchor;
            f.Mode = mode;
            f._sprite = item.Art;

            f.BuildVisuals(solid);
            f.FitArt();

            Showcase.Register(f);
            return f;
        }

        void OnDestroy() => Showcase.Unregister(this);

        /// <summary>
        /// Move the live ghost while it is being carried. If the mode itself changed - crossed
        /// from the wall zone onto open floor or back - the whole fixture is torn down and
        /// rebuilt as the other shape, because Easel and Wall are not one shape repositioned,
        /// they are two different pictures of what this piece is.
        /// </summary>
        public void UpdateCarry(Vector2 anchor, FrameMode mode, Vector2 visualAt)
        {
            Anchor = anchor;
            if (mode != Mode)
            {
                Mode = mode;
                TeardownVisuals();
                BuildVisuals(solid: false);
                FitArt();
            }
            transform.localPosition = visualAt;
        }

        void BuildVisuals(bool solid)
        {
            if (Mode == FrameMode.Wall) BuildWall();
            else BuildEasel(solid);
        }

        /// <summary>Hides the old shape immediately (a Destroy is deferred to end of frame, and
        /// without this both shapes would render for the one frame the mode flips) then destroys
        /// it - every child, plus the easel-only collider and DepthSorted, neither of which is a
        /// child of this transform.</summary>
        void TeardownVisuals()
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            if (_depth != null) { Destroy(_depth); _depth = null; }
            var col = GetComponent<CircleCollider2D>();
            if (col != null) Destroy(col);
            _wBorder = _wMatte = _ePlinth = _eCap = _eGlow = _eShadow = _art = _empty = null;
        }

        void BuildWall()
        {
            float inner = Tuning.Hub.FrameSize;
            float border = inner * 0.11f;

            // A gilt frame round a dark matte (the user's pick, group 6). The art inside is the
            // wallet's own - only the frame is ours.
            _ = border;
            _wBorder = Quad("border", 1f, 1f, Color.white, SortingOrders.FloorDetail + 1, Vector2.zero,
                            HubThemeArt.WallFrame(HubDress.Of(HubItem.Frames), Mathf.RoundToInt(inner * HubArt.Ppu)));
            _empty = Quad("empty", 1f, 1f, new Color(1f, 1f, 1f, 0.08f),
                          SortingOrders.FloorDetail + 3, Vector2.zero, Combat.TexelFx.RingSprite(inner * 0.17f));

            var artGo = new GameObject("art");
            artGo.transform.SetParent(transform, false);
            _art = artGo.AddComponent<SpriteRenderer>();
            _art.sortingOrder = SortingOrders.FloorDetail + 4;
        }

        void BuildEasel(bool solid)
        {
            _eShadow = Quad("shadow", Radius * 2.1f, Radius * 1.0f, new Color(0f, 0f, 0f, 0.55f),
                            0, new Vector2(0f, -0.02f), Spr.Square);
            Convergence.Art.SteppedShadow.Apply(_eShadow, Radius * 2.1f, Radius * 1.0f, HubArt.Ppu);
            // A stone plinth with a gilt band, the piece standing on it under a glass bell jar (the
            // user's pick, group 6). No glow: the jar's highlight catches the dim hub's light.
            _ePlinth = Quad("plinth", 1f, 1f, Color.white, 1, new Vector2(0f, -0.02f),
                            HubThemeArt.FrameStand(HubDress.Of(HubItem.Frames), Mathf.RoundToInt(Radius * 0.85f * HubArt.Ppu), Mathf.RoundToInt(0.3f * HubArt.Ppu)));
            _eGlow = Quad("jar", 1f, 1f, new Color(1f, 1f, 1f, 0.7f), 5, new Vector2(0f, 0.34f),
                          HubThemeArt.FrameCover(HubDress.Of(HubItem.Frames), Mathf.RoundToInt(Radius * 2.5f * HubArt.Ppu), Mathf.RoundToInt((ArtHeight + 0.12f) * HubArt.Ppu)));

            _empty = Quad("empty", 1f, 1f, new Color(1f, 1f, 1f, 0.08f), 3,
                         new Vector2(0f, 0.34f + ArtHeight * 0.5f), Combat.TexelFx.RingSprite(Radius * 0.35f));

            var artGo = new GameObject("art");
            artGo.transform.SetParent(transform, false);
            artGo.transform.localPosition = new Vector3(0f, 0.34f + ArtHeight * 0.5f, 0f);
            _art = artGo.AddComponent<SpriteRenderer>();
            _art.sortingOrder = 4;

            // Solid only when actually placed: the ghost that follows the player during placement
            // must not shove them around, and must not block the spot it is testing.
            if (solid)
            {
                var col = gameObject.AddComponent<CircleCollider2D>();
                col.radius = Radius * 0.7f;
                col.offset = new Vector2(0f, 0.16f);
            }

            // Sorted from the BASE of the plinth, which is where it touches the floor.
            _depth = DepthSorted.Attach(gameObject, 0f, isFixed: false,
                                        _eShadow, _ePlinth, _eGlow, _art, _empty);
        }

        void FitArt()
        {
            if (_art == null) return;
            _art.sprite = _sprite;
            _art.color = Color.white;
            if (_empty != null) _empty.enabled = _sprite == null;
            if (_sprite == null) return;

            var native = _sprite.bounds.size;
            if (native.x < 0.0001f || native.y < 0.0001f) return;

            if (Mode == FrameMode.Wall)
            {
                // Fit inside the frame preserving aspect. SpriteRenderer has no preserveAspect,
                // and collection art is not reliably square - stretching someone's picture to
                // fill a fixed rectangle is the one thing a gallery must not do.
                float inner = Tuning.Hub.FrameSize;
                float fit = Mathf.Min(inner / native.x, inner / native.y);
                _art.transform.localScale = Vector3.one * fit;
            }
            else
            {
                // Height-locked, so a row of easels reads as a row rather than a skyline.
                float scale = ArtHeight / native.y;
                float width = native.x * scale;
                float maxWidth = Radius * 2.4f;
                if (width > maxWidth) scale *= maxWidth / width;
                _art.transform.localScale = Vector3.one * scale;
            }
        }

        // ------------------------------------------------------------------ Showcase callbacks

        public void SetArt(Sprite sprite, string title, string collection)
        {
            Title = string.IsNullOrEmpty(title) ? "untitled" : title;
            Collection = collection;
            _sprite = sprite;
            FitArt();
        }

        public void Clear()
        {
            Title = null;
            Collection = null;
            _sprite = null;
            FitArt();
        }

        // ------------------------------------------------------------------ interaction

        public void SetFocus(bool on) => _focused = on;

        /// <summary>Ghost styling while it is being carried, plus whether the spot is legal.</summary>
        public void SetGhost(bool legal)
        {
            var tint = legal ? new Color(1f, 1f, 1f, 0.62f) : new Color(1f, 0.45f, 0.42f, 0.62f);
            if (_art != null) _art.color = tint;

            if (Mode == FrameMode.Easel)
            {
                _ePlinth.color = legal ? Color.white : new Color(1f, 0.5f, 0.5f);
                _eShadow.color = new Color(0f, 0f, 0f, legal ? 0.40f : 0.18f);
                _eGlow.color = new Color(1f, 1f, 1f, legal ? 0.5f : 0.2f);
                if (_depth != null) _depth.Apply();
            }
            else
            {
                _wBorder.color = legal ? Color.white : new Color(1f, 0.5f, 0.5f);
            }
        }

        void Update()
        {
            // Only a standing piece answers focus - its jar's glass brightens. A wall frame hangs flush.
            if (Mode != FrameMode.Easel || _eGlow == null) return;
            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            _eGlow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.6f, 1f, _lit));
        }

        // ------------------------------------------------------------------ construction

        SpriteRenderer Quad(string name, float size, Color color, int order)
            => Quad(name, size, size, color, order, Vector2.zero, Spr.Square);

        SpriteRenderer Quad(string name, float w, float h, Color color, int order, Vector2 offset, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = offset;
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
