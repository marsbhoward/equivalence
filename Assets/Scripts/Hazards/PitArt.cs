using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// The procedural sprites and layer helpers the three floor pits are drawn from.
    ///
    /// KEPT OUT OF <see cref="Spr"/> DELIBERATELY. Everything in Spr is a general primitive used
    /// from several unrelated systems, and its own notes are full of the cost of sharing one
    /// (thinning Ring would have thinned every impact burst in the game). A grain field and a jack
    /// are used by exactly one hazard each; putting them beside Circle and Capsule would invite
    /// the next caller to reach for them and start the same argument.
    ///
    /// TWO THINGS HERE DIFFER FROM Spr AND BOTH ARE THE POINT:
    ///
    /// - <b>POINT FILTERING.</b> Spr.Build is Bilinear, which is right for soft blobs and fatal
    ///   for grain: bilinear resolves a per-texel speckle into flat mush at any size a pit is
    ///   actually drawn, so "sand coloured" is exactly what you get back. Grain has to be
    ///   point-sampled or it is not grain.
    /// - <b>TILED, NOT STRETCHED.</b> These are built at one sprite per world unit and drawn
    ///   through <c>SpriteDrawMode.Tiled</c>, so a grain on a 2.3-unit Small pit is the same SIZE
    ///   of grain as on a 5-unit Large one. Stretched, the same texture would make a big pit look
    ///   like coarse gravel and a small one like dust - the material would change with the
    ///   footprint, which is the one thing a material must not do.
    ///
    /// Seamlessness is by construction rather than by blending: the fine layer is a pure per-texel
    /// hash (no spatial correlation to break at an edge) and the coarse layer's value-noise
    /// lattice is indexed MODULO its own cell count, so the left column of cells is literally the
    /// same row of hashes as the right.
    /// </summary>
    public static class PitArt
    {
        // ---- sprites ----

        static readonly Dictionary<int, Sprite> _grain = new();
        static readonly Dictionary<int, Sprite> _crust = new();
        static readonly Dictionary<int, Sprite> _mottle = new();
        static readonly Dictionary<int, Sprite> _glints = new();
        static Sprite _flame, _jack, _edgeFade, _emberBed;

        /// <summary>
        /// Per-texel speckle - the fine half of sand. Pure hash, so every texel is independent and
        /// the field has no structure at any scale above one texel, which is what granular
        /// actually means.
        /// </summary>
        public static Sprite Grain(int seed)
        {
            if (_grain.TryGetValue(seed, out var cached) && cached != null) return cached;
            var built = Build(64, FilterMode.Point, TextureWrapMode.Repeat, (x, y) =>
            {
                float h = Hash(x, y, seed);
                // Weighted to the low end: most grains sit near the base tone and only a minority
                // catch the light. An even spread reads as television static rather than as sand.
                return h * h * h;
            });
            _grain[seed] = built;
            return built;
        }

        /// <summary>
        /// Smooth two-octave value noise - the coarse half of sand, and the dune shading that
        /// stops a speckle field from reading as a flat sheet with dots on it.
        /// </summary>
        public static Sprite Mottle(int seed)
        {
            if (_mottle.TryGetValue(seed, out var cached) && cached != null) return cached;
            var built = Build(64, FilterMode.Bilinear, TextureWrapMode.Repeat, (x, y) =>
            {
                float n = ValueNoise(x / 64f, y / 64f, 4, seed) * 0.65f
                        + ValueNoise(x / 64f, y / 64f, 8, seed + 17) * 0.35f;
                return Mathf.Clamp01((n - 0.35f) * 1.6f);
            });
            _mottle[seed] = built;
            return built;
        }

        /// <summary>
        /// Sparse lit points on a water surface - sky caught on the tops of small waves. Short
        /// horizontal dashes rather than single texels: a lone texel reads as grain (sand's job),
        /// a dash reads as light lying on a surface. Point-sampled for the same reason as grain.
        /// </summary>
        public static Sprite Glints(int seed)
        {
            if (_glints.TryGetValue(seed, out var cached) && cached != null) return cached;
            var built = Build(64, FilterMode.Point, TextureWrapMode.Repeat, (x, y) =>
            {
                // A dash is lit when the texel at its LEFT end hashes high, and runs 2-3 texels
                // right of it - wrapped, so the tile still has no seam.
                for (int k = 0; k < 3; k++)
                {
                    int sx = Wrap(x - k, 64);
                    float h = Hash(sx, y, seed);
                    if (h > 0.985f && (k < 2 || h > 0.993f)) return k == 0 ? 1f : 0.7f;
                }
                return 0f;
            });
            _glints[seed] = built;
            return built;
        }

        /// <summary>
        /// Ash PLATES with dark cracks between them, not a grey wash. A cooled fire pit is a crust
        /// that has broken as it shrank, and the cracks are what tell you there is something under
        /// it - a flat grey rectangle is a paving slab.
        /// </summary>
        public static Sprite Crust(int seed)
        {
            if (_crust.TryGetValue(seed, out var cached) && cached != null) return cached;
            var built = Build(96, FilterMode.Point, TextureWrapMode.Repeat, (x, y) =>
            {
                float n = ValueNoise(x / 96f, y / 96f, 6, seed);
                // Distance from the noise field's own mid level IS the plate: the level set
                // n == 0.5 traces a closed contour network, so thresholding how far a texel sits
                // from it gives solid plates separated by continuous cracks rather than the
                // disconnected blobs a plain threshold produces.
                float edge = Mathf.Abs(n - 0.5f);
                float plate = Mathf.Clamp01((edge - 0.045f) * 14f);
                // A little per-texel tooth so the plates themselves read as ash rather than paint.
                return plate * Mathf.Lerp(0.78f, 1f, Hash(x, y, seed + 3));
            });
            _crust[seed] = built;
            return built;
        }

        /// <summary>
        /// The bed under the flames: coals, drawn as hot cells separated by cooler gaps. Shares
        /// <see cref="Crust"/>'s contour trick INVERTED - here the network between plates is what
        /// glows, because a coal bed is lit through its own cracks.
        /// </summary>
        public static Sprite EmberBed => _emberBed ??= Build(96, FilterMode.Point, TextureWrapMode.Repeat, (x, y) =>
        {
            float n = ValueNoise(x / 96f, y / 96f, 7, 991);
            float edge = Mathf.Abs(n - 0.5f);
            return Mathf.Clamp01(1f - edge * 9f) * Mathf.Lerp(0.6f, 1f, Hash(x, y, 77));
        });

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

        /// <summary>
        /// A square whose alpha runs from solid along its BOTTOM edge to nothing at the top - one
        /// inner wall of a recess. Rotated to serve all four sides, so the pit's own lip is one
        /// sprite used four ways rather than four bespoke gradients that could disagree.
        /// </summary>
        public static Sprite EdgeFade => _edgeFade ??= Build(64, FilterMode.Bilinear, TextureWrapMode.Clamp, (x, y) =>
        {
            float t = y / 64f;
            float f = 1f - t;
            return f * f;      // squared, so the wall is dark right at the lip and gone quickly
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

        /// <summary>
        /// A material layer REPEATED across <paramref name="size"/> rather than stretched to it.
        /// Scale stays at one, because Tiled draw mode sizes off <c>sr.size</c> and a scaled tile
        /// would scale the tiling with it - which is the stretch this exists to avoid, arriving
        /// by a different route.
        /// </summary>
        public static SpriteRenderer Tiled(GameObject parent, Sprite sprite, Color color,
                                           Vector2 size, int order, string name = "material")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;
            sr.size = size;
            return sr;
        }

        // ---- noise ----

        /// <summary>Deterministic per-texel hash in [0,1]. No trig, no allocation, no state.</summary>
        static float Hash(int x, int y, int seed)
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
        static float ValueNoise(float u, float v, int cells, int seed)
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
