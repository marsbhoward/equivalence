using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// Rai's arcs: streaks of lightning leaping off the disc at random, out to a greatsword's
    /// length (<see cref="Art.Gear.DemoGear.RaiArcReachCells"/>). Each strike flashes, dims,
    /// often RE-STRIKES down the same channel - real lightning does, and a bolt that just blinks
    /// on and off reads as a decal - and fades through deep blue.
    ///
    /// PIXEL ART, at the disc's own cell size: bolts are rasterised into one small canvas per disc
    /// rather than drawn as a LineRenderer, whose smooth sub-pixel strips read as a different game
    /// beside the art. Measured in the disc's CELLS (its sprite's height over its cell count), so
    /// the reach rides whatever scale the disc is drawn at - the hand, a display, a throw - and a
    /// disc showing menu art gets bolts at twice the resolution, finer lines, same silhouette.
    ///
    /// The canvas is UNPARENTED and never rotates: it follows the disc's centre in world space.
    /// Parented, it would turn with every swing and alias like a rotated sprite, and forcing a
    /// world rotation under the rig's mirrored (negative-x) chain is asking Unity for a skew.
    /// Owned here and destroyed with this component.
    ///
    /// UNSCALED time: every screen that shows a held disc (character sheet, gear picker) pauses
    /// the game, and a bolt frozen mid-strike reads as a crack in the picture.
    /// </summary>
    public class LightningArcs : MonoBehaviour
    {
        /// <summary>Seconds between strikes, and the chance another follows hard behind.</summary>
        const float GapMin = 0.10f, GapMax = 0.55f, BurstChance = 0.35f;
        const float LifeMin = 0.12f, LifeMax = 0.24f;
        /// <summary>Share of strikes that go most of the way to the full reach; the rest are
        /// medium. Mostly-long would be a strobe across the room; never-long would miss the brief.</summary>
        const float LongChance = 0.4f;
        /// <summary>How far each midpoint may be pushed sideways, as a fraction of its segment -
        /// the first split is what BOWS the bolt into an arc.</summary>
        const float Rough = 0.3f;
        /// <summary>Subdivide until segments are under this many cells.</summary>
        const float MinSegCells = 3f;
        /// <summary>Cells of glow past a bolt's core, so the canvas never clips one.</summary>
        const float GlowMargin = 4f;

        static readonly Color32[] Tone =
        {
            new(0, 0, 0, 0),
            new(33, 64, 219, 205),     // 1 deep blue, the glow's edge - near opaque, or it
                                       //   vanished against the arena's light brick
            new(61, 138, 255, 240),    // 2 electric blue
            new(158, 217, 255, 255),   // 3 pale
            new(245, 252, 255, 255),   // 4 white-hot core
        };

        /// <summary>One strike: a trunk and its forks, in cells off the disc's centre, world-up.</summary>
        class Bolt
        {
            public List<Vector2> Trunk;
            public List<List<Vector2>> Forks;
            public float Age, Life;
            public bool Restrike;
            public int Stage = -1;
        }

        SpriteRenderer _source;
        SpriteRenderer _canvas;
        Texture2D _tex;
        Color32[] _px;
        byte[] _lvl;
        int _size, _res;
        int _fixedOrder = int.MinValue;
        // NOT readonly and null-guarded: a domain reload hands a list of a non-serialised class
        // back empty or null (see CLAUDE.md).
        List<Bolt> _bolts = new();
        float _next;

        /// <summary>Turn the arcs on or off under <paramref name="anchor"/>, crackling off
        /// <paramref name="source"/> - a held disc (either hand) or a display's.</summary>
        public static void SetOn(Transform anchor, SpriteRenderer source, bool on)
        {
            if (anchor == null) return;
            var a = anchor.GetComponentInChildren<LightningArcs>(true);
            if (!on)
            {
                if (a != null) a.gameObject.SetActive(false);
                return;
            }
            if (a == null)
            {
                var go = new GameObject("lightning-arcs");
                go.transform.SetParent(anchor, false);
                a = go.AddComponent<LightningArcs>();
            }
            // Re-taken every call: a repaint can replace the renderer (SaintHalo's rule).
            a._source = source;
            if (!a.gameObject.activeSelf) a.gameObject.SetActive(true);
        }

        /// <summary>
        /// Give a FLYING disc its arcs, if the hand it left carried them - thrown, orbiting or
        /// suspended. <paramref name="visual"/> is the flying picture; sorted at a fixed order,
        /// since nothing in flight re-sorts.
        /// </summary>
        public static void Follow(Art.Gear.ICharacterRig rig, Transform flying, SpriteRenderer visual,
                                  int sortingOrder)
        {
            var held = rig?.WeaponAnchor != null
                ? rig.WeaponAnchor.GetComponentInChildren<LightningArcs>(false) : null;
            if (held == null || flying == null || visual == null) return;
            var go = new GameObject("lightning-arcs");
            go.transform.SetParent(flying, false);
            var a = go.AddComponent<LightningArcs>();
            a._source = visual;
            a._fixedOrder = sortingOrder;
        }

        void OnDisable()
        {
            if (_canvas != null) _canvas.enabled = false;
            _bolts?.Clear();
        }

        void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_tex != null) Destroy(_tex);
        }

        void LateUpdate()
        {
            _bolts ??= new List<Bolt>();
            var src = _source;
            // isVisible: on no camera at all (the armoury wall's bay while the player is in the
            // hub), nothing is drawn and nothing is redrawn - the canvas is a texture upload a strike.
            bool shown = src != null && src.enabled && src.gameObject.activeInHierarchy && src.sprite != null
                         && src.isVisible;
            if (!shown)
            {
                if (_canvas != null) _canvas.enabled = false;
                _bolts.Clear();
                return;
            }

            // The disc's cell, in world units, and how many canvas texels to spend on one:
            // arena art is two texels a cell (1 here), menu art four (2 here - finer lines).
            var sprite = src.sprite;
            int cells = Art.Gear.DemoGear.RaiHeightCells;
            float cellWorld = sprite.bounds.size.y * Mathf.Abs(src.transform.lossyScale.y) / cells;
            int res = Mathf.Max(1, Mathf.RoundToInt(sprite.rect.height / cells / 2f));
            if (cellWorld <= 0f) return;
            EnsureCanvas(res);

            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            bool dirty = false;
            if (now >= _next)
            {
                _bolts.Add(Strike());
                dirty = true;
                _next = now + (Random.value < BurstChance ? Random.Range(0.02f, 0.06f)
                                                          : Random.Range(GapMin, GapMax));
            }
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                var b = _bolts[i];
                b.Age += dt;
                if (b.Age >= b.Life) { _bolts.RemoveAt(i); dirty = true; continue; }
                int stage = StageOf(b);
                if (stage != b.Stage) { b.Stage = stage; dirty = true; }
            }
            if (dirty) Redraw();

            // Follow the disc's CENTRE (its sprite's, not its grip), unrotated - see the class doc.
            var t = _canvas.transform;
            t.position = src.bounds.center;
            t.rotation = Quaternion.identity;
            t.localScale = new Vector3(cellWorld, cellWorld, 1f);
            _canvas.gameObject.layer = src.gameObject.layer;
            _canvas.sortingLayerID = src.sortingLayerID;
            _canvas.sortingOrder = _fixedOrder != int.MinValue ? _fixedOrder : src.sortingOrder - 1;
            _canvas.enabled = _bolts.Count > 0;
        }

        /// <summary>Brightness through a strike: 3 full, 2 dim, 1 fading. A re-strike flashes
        /// twice down the same channel.</summary>
        static int StageOf(Bolt b)
        {
            float t = b.Age / b.Life;
            if (b.Restrike)
                return t < 0.28f ? 3 : t < 0.42f ? 2 : t < 0.68f ? 3 : 1;
            return t < 0.55f ? 3 : t < 0.75f ? 2 : 1;
        }

        void EnsureCanvas(int res)
        {
            if (_canvas != null && _tex != null && _res == res && _px != null && _lvl != null) return;
            _res = res;
            float reach = Art.Gear.DemoGear.RaiRingCells + Art.Gear.DemoGear.RaiArcReachCells + GlowMargin;
            int half = Mathf.CeilToInt(reach * res);
            _size = half * 2;
            if (_tex != null) Destroy(_tex);
            _tex = new Texture2D(_size, _size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _px = new Color32[_size * _size];
            _lvl = new byte[_size * _size];
            _tex.SetPixels32(_px);
            _tex.Apply(false);

            if (_canvas == null)
            {
                var go = new GameObject("lightning-arcs.canvas");
                _canvas = go.AddComponent<SpriteRenderer>();
            }
            // FullRect: a Tight mesh is cut to the alpha at creation, and the canvas is created
            // empty - it would draw nothing, ever.
            _canvas.sprite = Sprite.Create(_tex, new Rect(0, 0, _size, _size), new Vector2(0.5f, 0.5f),
                                           res, 0, SpriteMeshType.FullRect);
            if (_source != null) _canvas.sharedMaterial = _source.sharedMaterial;
            _canvas.enabled = false;
        }

        /// <summary>A new strike from a random point on the ring, heading mostly outward.</summary>
        static Bolt Strike()
        {
            float ring = Art.Gear.DemoGear.RaiRingCells;
            float reach = Art.Gear.DemoGear.RaiArcReachCells;
            float a = Random.Range(0f, Mathf.PI * 2f);
            var root = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring;
            float heading = a + Random.Range(-0.55f, 0.55f);
            float len = reach * (Random.value < LongChance ? Random.Range(0.8f, 1f) : Random.Range(0.3f, 0.75f));
            var tip = root + new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)) * len;

            var b = new Bolt
            {
                Trunk = Jag(root, tip),
                Forks = new List<List<Vector2>>(),
                Life = Random.Range(LifeMin, LifeMax),
                Restrike = Random.value < 0.5f,
            };

            // Forks peel off the middle of the trunk, shorter and at an angle to it.
            int forks = Random.Range(0, 4);
            for (int f = 0; f < forks && b.Trunk.Count > 4; f++)
            {
                int i = Random.Range(b.Trunk.Count / 5, b.Trunk.Count * 4 / 5);
                var from = b.Trunk[i];
                var along = (b.Trunk[i + 1] - b.Trunk[i - 1]).normalized;
                float turn = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.35f, 0.8f);
                var dir = new Vector2(along.x * Mathf.Cos(turn) - along.y * Mathf.Sin(turn),
                                      along.x * Mathf.Sin(turn) + along.y * Mathf.Cos(turn));
                float left = len * (1f - i / (float)b.Trunk.Count);
                b.Forks.Add(Jag(from, from + dir * left * Random.Range(0.25f, 0.5f)));
            }
            return b;
        }

        /// <summary>Midpoint displacement from <paramref name="a"/> to <paramref name="b"/>.</summary>
        static List<Vector2> Jag(Vector2 a, Vector2 b)
        {
            var pts = new List<Vector2> { a, b };
            for (int pass = 0; pass < 8; pass++)
            {
                bool split = false;
                var next = new List<Vector2>(pts.Count * 2) { pts[0] };
                for (int i = 1; i < pts.Count; i++)
                {
                    var p = pts[i - 1];
                    var q = pts[i];
                    float len = Vector2.Distance(p, q);
                    if (len > MinSegCells)
                    {
                        var n = new Vector2(-(q.y - p.y), q.x - p.x) / len;
                        next.Add((p + q) * 0.5f + n * Random.Range(-Rough, Rough) * len);
                        split = true;
                    }
                    next.Add(q);
                }
                pts = next;
                if (!split) break;
            }
            return pts;
        }

        void Redraw()
        {
            System.Array.Clear(_lvl, 0, _lvl.Length);
            foreach (var b in _bolts)
            {
                int shift = 3 - b.Stage;                       // dim and fading step every tone down
                Stamp(b.Trunk, 1f, 0.45f, shift);
                foreach (var f in b.Forks) Stamp(f, 0.6f, 0.35f, shift);
            }
            for (int i = 0; i < _lvl.Length; i++) _px[i] = Tone[_lvl[i]];
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        /// <summary>
        /// Draw one path into the level buffer: a one-texel white core, then pale, blue and deep
        /// bands whose widths taper from <paramref name="rootW"/> to <paramref name="tipW"/>.
        /// <paramref name="shift"/> steps every tone down for a dim or fading stage.
        /// </summary>
        void Stamp(List<Vector2> path, float rootW, float tipW, int shift)
        {
            int half = _size / 2;
            var texels = new List<Vector2Int>(path.Count * 4);
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1] * _res;
                var c = path[i] * _res;
                Line(Mathf.FloorToInt(a.x) + half, Mathf.FloorToInt(a.y) + half,
                     Mathf.FloorToInt(c.x) + half, Mathf.FloorToInt(c.y) + half, texels, i == 1);
            }
            int total = texels.Count;
            for (int k = 0; k < total; k++)
            {
                var p = texels[k];
                float w = Mathf.Lerp(rootW, tipW, k / (float)Mathf.Max(1, total - 1)) * _res;
                float pale = Mathf.Max(0.5f, 1.1f * w);
                float blue = pale + 1.0f * w;
                float deep = blue + 0.7f * w;
                int box = Mathf.CeilToInt(deep);
                for (int oy = -box; oy <= box; oy++)
                for (int ox = -box; ox <= box; ox++)
                {
                    int x = p.x + ox, y = p.y + oy;
                    if (x < 0 || y < 0 || x >= _size || y >= _size) continue;
                    float d = Mathf.Sqrt(ox * ox + oy * oy);
                    int level = d < 0.5f ? 4 : d <= pale ? 3 : d <= blue ? 2 : d <= deep ? 1 : 0;
                    level -= shift;
                    if (level <= 0) continue;
                    int idx = y * _size + x;
                    if (_lvl[idx] < level) _lvl[idx] = (byte)level;
                }
            }
        }

        /// <summary>Bresenham from (x0, y0) to (x1, y1), appended to <paramref name="into"/>;
        /// the first point only on the first segment, so joints are not stamped twice.</summary>
        static void Line(int x0, int y0, int x1, int y1, List<Vector2Int> into, bool first)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            bool start = true;
            while (true)
            {
                if (!start || first) into.Add(new Vector2Int(x0, y0));
                start = false;
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }
    }
}
