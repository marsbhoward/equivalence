using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// The procedural sprites and layer helpers the four floor pits are drawn from.
    ///
    /// KEPT OUT OF <see cref="Spr"/> DELIBERATELY. Everything in Spr is a general primitive used
    /// from several unrelated systems, and its own notes are full of the cost of sharing one
    /// (thinning Ring would have thinned every impact burst in the game). A grain field and a jack
    /// are used by exactly one hazard each; putting them beside Circle and Capsule would invite
    /// the next caller to reach for them and start the same argument.
    ///
    /// THREE THINGS HERE DIFFER FROM Spr AND ALL ARE THE POINT:
    ///
    /// - <b>POINT FILTERING.</b> Spr.Build is Bilinear, which is right for soft blobs and fatal
    ///   for grain: bilinear resolves a per-texel speckle into flat mush, so "sand coloured" is
    ///   exactly what you get back. Grain has to be point-sampled or it is not grain.
    /// - <b>WORLD-SPACE MATERIALS, NOT STRETCHED ONES.</b> A pit's materials are sampled at world
    ///   texels and units (<see cref="Pattern"/>), so a grain on a small pit is the same SIZE of
    ///   grain as on a large one - stretched, a big pit looks like gravel and a small one like
    ///   dust, and the material would change with the footprint, which a material must not do.
    /// - <b>SHAPED, BAKED ONCE.</b> A pit is any shape the pieces lay (Hazards.PitShape), so its
    ///   layers are baked to it at the floor's own density (<see cref="Shaped"/>), its recess
    ///   followed round the outline (<see cref="Recess"/>).
    /// </summary>
    public static class PitArt
    {
        // ---- sprites ----

        static Sprite _flame, _jack;

        /// <summary>
        /// One flame tongue: broad and soft at the base, tapering to a point, curling as it rises.
        ///
        /// A tongue rather than a blob, and the taper is the whole reason. Fire is drawn here as
        /// several of these at independent phases (see <see cref="FirePit"/>) rather than as one
        /// animated sheet - a sheet can only get brighter and dimmer, where a handful of tongues
        /// rising out of the bed at different rates is what actually reads as burning.
        /// </summary>
        /// <remarks>
        /// Pivoted on its BASE, not its centre, so growing a tongue lifts its tip and leaves its
        /// root in the coals. Centre-pivoted, scaling the height would sink half the flame below
        /// the bed and take the root with it - the flame would appear to rise out of nothing and
        /// then out of the floor beneath the pit.
        /// </remarks>
        public static Sprite Flame => _flame ??= Build(128, FilterMode.Bilinear, TextureWrapMode.Clamp, (x, y) =>
        {
            float u = x / 128f, t = y / 128f;          // t: 0 at the base, 1 at the tip
            // The tongue leans as it rises, and the lean accelerates - a constant lean is a
            // slanted cone, which reads as a shard of glass rather than as something rising.
            float centre = 0.5f + 0.1f * Mathf.Sin(t * 2.6f) * t * t;
            float half = 0.30f * Mathf.Pow(1f - t, 0.62f) * Mathf.Clamp01(t * 6f);
            if (half <= 0.0001f) return 0f;
            float d = Mathf.Abs(u - centre) / half;
            if (d > 1f) return 0f;
            // Soft at the rim, hottest along the core, and fading out toward the tip so the
            // tongue dissolves rather than ending on a hard point.
            float body = 1f - d * d;
            return body * Mathf.Lerp(1f, 0.25f, Mathf.Pow(t, 1.5f));
        }, pivotY: 0f);

        /// <summary>
        /// A jack: three bars crossed at 60 degrees with a knob on every end and a boss at the
        /// centre. Six points from directly above, which is what a caltrop actually looks like -
        /// a simple X reads as a cross painted on the floor, and the knobs are what make it an
        /// OBJECT lying there rather than a mark.
        /// </summary>
        public static Sprite Jack => _jack ??= Build(64, FilterMode.Bilinear, TextureWrapMode.Clamp, (x, y) =>
        {
            float nx = (x + 0.5f) / 32f - 1f, ny = (y + 0.5f) / 32f - 1f;
            float a = 0f;
            for (int i = 0; i < 3; i++)
            {
                float ang = i * Mathf.PI / 3f;
                float ca = Mathf.Cos(ang), sa = Mathf.Sin(ang);
                float along = nx * ca + ny * sa;       // -1..1 along the bar
                float across = -nx * sa + ny * ca;
                if (Mathf.Abs(along) > 0.88f) continue;
                // The bar tapers toward its ends and swells back out into the knob, so the arm
                // has a waist - a constant-width bar with a ball stuck on the end reads as two
                // shapes rather than one spike.
                float waist = Mathf.Lerp(0.115f, 0.052f, Mathf.Clamp01(Mathf.Abs(along) / 0.7f));
                float knob = Mathf.Clamp01((Mathf.Abs(along) - 0.66f) / 0.22f);
                float half = Mathf.Max(waist, knob * 0.145f);
                a = Mathf.Max(a, Mathf.Clamp01((half - Mathf.Abs(across)) * 26f));
            }
            // The centre boss, so the three bars meet in a body instead of just overlapping.
            a = Mathf.Max(a, Mathf.Clamp01((0.15f - Mathf.Sqrt(nx * nx + ny * ny)) * 22f));
            return a;
        });

        // ---- layer helpers ----

        /// <summary>
        /// A flat tinted quad filling <paramref name="size"/>, centred on the parent. Every pit
        /// layer goes through this or <see cref="Tiled"/>, so nothing in a pit sets a transform
        /// scale by hand.
        /// </summary>
        public static SpriteRenderer Quad(GameObject parent, Sprite sprite, Color color,
                                          Vector2 size, int order, string name = "layer")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return sr;
        }

        // ---- shaped layers (Hazards.PitShape) ----
        //
        // A pit laid by the pieces is any shape, so its layers cannot be tiled quads: each is
        // BAKED once, at the floor's own density (Art.Scenery.Ppu - floors stay 37.5,
        // ENGINEERING.md) and on the world's texel grid, cut to the shape. The materials are the
        // same patterns the tiles were made of, sampled in WORLD units, so grain and mottle sit
        // still across a pit and a big pit's grain is a small one's. White with the pattern in
        // alpha: the renderer's colour tints it, so the fire's paint-by-heat still works.

        /// <summary>A world-space material pattern: texel x, y on the world grid and the same
        /// point in units.</summary>
        public delegate float Pattern(int tx, int ty, float u, float v);

        public static Pattern Solid => (tx, ty, u, v) => 1f;
        public static Pattern GrainAt(int seed) => (tx, ty, u, v) => { float h = Hash(tx, ty, seed); return h * h * h; };
        public static Pattern MottleAt(int seed) => (tx, ty, u, v) =>
        {
            float n = ValueNoise(u, v, 4, seed) * 0.65f + ValueNoise(u, v, 8, seed + 17) * 0.35f;
            return Mathf.Clamp01((n - 0.35f) * 1.6f);
        };
        public static Pattern CrustAt(int seed) => (tx, ty, u, v) =>
        {
            float edge = Mathf.Abs(ValueNoise(u, v, 6, seed) - 0.5f);
            return Mathf.Clamp01((edge - 0.045f) * 14f) * Mathf.Lerp(0.78f, 1f, Hash(tx, ty, seed + 3));
        };
        public static Pattern EmberBedAt => (tx, ty, u, v) =>
        {
            float edge = Mathf.Abs(ValueNoise(u, v, 7, 991) - 0.5f);
            return Mathf.Clamp01(1f - edge * 9f) * Mathf.Lerp(0.6f, 1f, Hash(tx, ty, 77));
        };
        public static Pattern GlintsAt(int seed) => (tx, ty, u, v) =>
        {
            for (int k = 0; k < 3; k++)
            {
                float h = Hash(tx - k, ty, seed);
                if (h > 0.985f && (k < 2 || h > 0.993f)) return k == 0 ? 1f : 0.7f;
            }
            return 0f;
        };

        static float Ppu => Art.Scenery.Ppu;   // the floor's own density (37.5)

        /// <summary>The texel rect a shape's layers are baked over, on the world grid.</summary>
        static RectInt TexelRect(PitShape shape)
        {
            var r = shape.DrawRect;
            int x0 = Mathf.FloorToInt(r.xMin * Ppu) - 1, y0 = Mathf.FloorToInt(r.yMin * Ppu) - 1;
            int x1 = Mathf.CeilToInt(r.xMax * Ppu) + 1, y1 = Mathf.CeilToInt(r.yMax * Ppu) + 1;
            return new RectInt(x0, y0, x1 - x0, y1 - y0);
        }

        static Vector2 TexelCentre(int tx, int ty) => new((tx + 0.5f) / Ppu, (ty + 0.5f) / Ppu);

        /// <summary>
        /// Whether a texel is drawn as the pit: inside the field (by a hashed wobble of a fraction
        /// of a texel, so the cut edge is a hand's edge rather than a ruled curve) and inside the
        /// room - a spill past the room is the shelf's, never the pit's.
        /// </summary>
        public static bool InsideTexel(PitShape shape, int tx, int ty)
        {
            var p = TexelCentre(tx, ty);
            if (shape.PastSpill(p, out _)) return false;
            float wobble = (Hash(tx >> 1, ty >> 1, 4441) - 0.5f) * Tuning.Hazards.PitEdgeWobble;
            return shape.Sample(p) > wobble;
        }

        /// <summary>
        /// One material layer of a shaped pit, baked: the shape's texels carry
        /// <paramref name="pattern"/> in alpha, the rest are clear. Positioned in world space under
        /// <paramref name="parent"/>; the texture and sprite are added to <paramref name="owned"/>
        /// for the pit to free.
        /// </summary>
        public static SpriteRenderer Shaped(GameObject parent, PitShape shape, Pattern pattern, Color color,
                                            int order, string name, List<Object> owned)
        {
            var rect = TexelRect(shape);
            var px = new Color32[rect.width * rect.height];
            for (int j = 0; j < rect.height; j++)
            for (int i = 0; i < rect.width; i++)
            {
                int tx = rect.x + i, ty = rect.y + j;
                if (!InsideTexel(shape, tx, ty)) continue;
                var p = TexelCentre(tx, ty);
                float a = Mathf.Clamp01(pattern(tx, ty, p.x, p.y));
                px[j * rect.width + i] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            return Place(parent, rect, px, color, order, name, owned);
        }

        /// <summary>
        /// The RECESS for a shaped pit, baked as one coloured layer: the far wall (a top edge,
        /// which a top-down camera sees the inside of) in the deepest shadow, the side walls
        /// weaker, the near wall catching a little light, and a hairline of broken stone along
        /// every top edge - FloorPit's four gradient walls, followed round any outline. Edges
        /// where a spill runs on into the lake have no wall: the field never closes there.
        /// </summary>
        public static SpriteRenderer Recess(GameObject parent, PitShape shape, float depthScale, int order, List<Object> owned)
        {
            var rect = TexelRect(shape);
            int w = rect.width, h = rect.height;
            var inside = new bool[w * h];
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                // The walls are measured against the FIELD (unclipped), so a spill's mouth has none.
                var p = TexelCentre(rect.x + i, rect.y + j);
                float wobble = (Hash((rect.x + i) >> 1, (rect.y + j) >> 1, 4441) - 0.5f) * Tuning.Hazards.PitEdgeWobble;
                inside[j * w + i] = shape.Sample(p) > wobble;
            }
            bool In(int i, int j) => i < 0 || j < 0 || i >= w || j >= h ? false : inside[j * w + i];

            float depth = Tuning.Hazards.PitWallDepth * depthScale * Ppu;   // texels
            int far = Mathf.CeilToInt(depth * 1.6f), side = Mathf.CeilToInt(depth), near = Mathf.CeilToInt(depth * 0.7f);
            int Run(int i, int j, int di, int dj, int max)
            {
                for (int k = 1; k <= max; k++) if (!In(i + di * k, j + dj * k)) return k;
                return max + 1;
            }
            static float Fall(int d, int max) { if (d > max) return 0f; float t = (d - 1f) / Mathf.Max(1f, max); float f = 1f - t; return f * f; }

            var px = new Color32[w * h];
            var lip = new Color(0.72f, 0.70f, 0.66f, 0.55f);
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                if (!In(i, j) || !InsideTexel(shape, rect.x + i, rect.y + j)) continue;
                int up = Run(i, j, 0, 1, far);
                if (up == 1) { px[j * w + i] = lip; continue; }
                float shade = Mathf.Max(0.62f * Fall(up, far),
                                        0.34f * Mathf.Max(Fall(Run(i, j, -1, 0, side), side), Fall(Run(i, j, 1, 0, side), side)));
                float light = 0.10f * Fall(Run(i, j, 0, -1, near), near);
                px[j * w + i] = light > shade ? new Color(1f, 0.96f, 0.88f, light) : new Color(0f, 0f, 0f, shade);
            }
            return Place(parent, rect, px, Color.white, order, "recess", owned);
        }

        /// <summary>
        /// The SHELF where wading water meets the lake past an open side (edge water): a dark
        /// drop-off line along the room's edge, then the lake's own shallows running on under the
        /// ground's bank, so the inlet and the lake read as one water with a step down between.
        /// </summary>
        public static SpriteRenderer Shelf(GameObject parent, PitShape shape, Color drop, Color lake, int order, List<Object> owned)
        {
            var rect = TexelRect(shape);
            var px = new Color32[rect.width * rect.height];
            float line = 2f / Ppu;
            for (int j = 0; j < rect.height; j++)
            for (int i = 0; i < rect.width; i++)
            {
                var p = TexelCentre(rect.x + i, rect.y + j);
                if (!shape.PastSpill(p, out float past) || shape.Sample(p) <= 0f) continue;
                px[j * rect.width + i] = past <= line ? drop : lake;
            }
            return Place(parent, rect, px, Color.white, order, "shelf", owned);
        }

        static SpriteRenderer Place(GameObject parent, RectInt rect, Color32[] px, Color color, int order, string name, List<Object> owned)
        {
            var tex = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var spr = Sprite.Create(tex, new Rect(0, 0, rect.width, rect.height), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
            spr.hideFlags = HideFlags.HideAndDontSave;
            owned?.Add(tex);
            owned?.Add(spr);

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = new Vector3(rect.x / Ppu, rect.y / Ppu, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // ---- noise ----

        /// <summary>Deterministic per-texel hash in [0,1]. No trig, no allocation, no state.</summary>
        internal static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1274126177;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7FFFFFFF) / (float)0x7FFFFFFF;
            }
        }

        /// <summary>
        /// Value noise on a <paramref name="cells"/>-square lattice, wrapped MODULO that count so
        /// the field is toroidal and the tile has no seam. Smoothstepped between lattice points -
        /// linear interpolation leaves visible creases along every lattice line, which on a crust
        /// come out as a grid of cracks rather than an organic one.
        /// </summary>
        internal static float ValueNoise(float u, float v, int cells, int seed)
        {
            float fx = u * cells, fy = v * cells;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);

            int xa = Wrap(x0, cells), xb = Wrap(x0 + 1, cells);
            int ya = Wrap(y0, cells), yb = Wrap(y0 + 1, cells);

            float n00 = Hash(xa, ya, seed), n10 = Hash(xb, ya, seed);
            float n01 = Hash(xa, yb, seed), n11 = Hash(xb, yb, seed);

            return Mathf.Lerp(Mathf.Lerp(n00, n10, tx), Mathf.Lerp(n01, n11, tx), ty);
        }

        static int Wrap(int i, int n) => ((i % n) + n) % n;

        // ---- builder ----

        /// <summary>
        /// Spr.Build with the filter and wrap modes exposed, and <c>FullRect</c> geometry so the
        /// sprite is legal in Tiled draw mode - a tight-meshed sprite silently refuses to tile.
        /// PPU is the texture's own size, so one sprite is one world unit and the tile density is
        /// stated in world terms rather than in texels.
        /// </summary>
        static Sprite Build(int size, FilterMode filter, TextureWrapMode wrap,
                            System.Func<int, int, float> alpha, float pivotY = 0.5f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = wrap,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha(x, y));
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, pivotY),
                                 size, 0, SpriteMeshType.FullRect);
        }
    }
}
