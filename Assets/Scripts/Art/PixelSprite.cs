using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art
{
    /// <summary>
    /// Turns a grid of characters into a pixel-art sprite.
    ///
    /// Three things make this different from <c>Spr</c> and <c>Glyphs</c>, and all three are the
    /// point of it:
    ///
    /// 1. <b>Point filtering.</b> Those two use Bilinear, which is right for soft blobs and fatal
    ///    for pixel art - it blurs every edge into a gradient.
    /// 2. <b>Real RGB per texel.</b> Everything else in the project writes white-with-alpha and
    ///    colours it via SpriteRenderer.color. Pixel art carries its own colour, so anything
    ///    drawing it must leave that tint WHITE or it multiplies the art down.
    /// 3. <b>Fixed pixel density, not "one sprite = one unit".</b> The others pass
    ///    <c>pixelsPerUnit: textureSize</c>, making every sprite exactly 1x1 world unit. Here the
    ///    density is fixed at <see cref="LayoutUnit"/> so a grid's world size follows its real
    ///    aspect - which is what lets <see cref="SizeOf"/> defeat the non-uniform stretch in
    ///    PrimitiveCharacterRig.Apply.
    /// </summary>
    public static class PixelSprite
    {
        /// <summary>
        /// Art pixels per world unit for the BODY and the ARMOUR, and the grid the layout is
        /// counted in: pivots, attach points, gear offsets.
        ///
        /// NOW 75 - THE SAME DENSITY AS WEAPONS. Armour spent a while at 75 once before and came
        /// back to 37.5: a gear silhouette twice as fine as the body wearing it was judged the one
        /// thing a paper doll cannot afford, and the six-tone ramp that bump was really buying is a
        /// property of the palette, not of the grid (that reasoning is preserved in full on
        /// <c>DemoGear.GearPpu</c>'s own doc comment). The project later decided the match to
        /// weapons was worth it after all and moved armour and body up to meet them here, rather
        /// than moving weapons down - a weapon held away from the body and looked at directly
        /// (the disc's ring, hole and outline in particular) still needs the finer grid, and there
        /// is no equivalent case for the body needing to be coarser than what it wears. Every
        /// existing armour/body grid was block-doubled (see "More pixels is free; more DETAIL is
        /// what costs" in CLAUDE.md) rather than redrawn, so this is a resolution change only - the
        /// same silhouette-vs-paper-doll argument that sent armour back to 37.5 the first time was
        /// never about resolution, it was about DETAIL inviting itself in, and a block-double adds
        /// none.
        ///
        /// WEAPONS AND JEWELLERY stay at this same value now too (<c>DemoGear.FinePpu</c> is still
        /// a separate named constant, numerically equal to this one) - a weapon is held away from
        /// the body and looked at directly, and the disc's ring, hole and outline would not fit
        /// across twelve texels at half this density.
        ///
        /// A shoulder is the same distance from the spine whatever resolution anything is drawn
        /// at, so every offset literal in the rig and the gear is in these cells. NOTE: because
        /// <see cref="Px(float,float)"/> always divides by this single global constant regardless
        /// of what ppu a sprite's own art is drawn at, EVERY offX/offY position literal in the
        /// project - including ones attached to a FinePpu or MenuPpu piece - had to double when
        /// this did. Only each piece's own GRID and pivotTexel stayed put if its ppu didn't move.
        ///
        /// CORRECTION. This block used to claim 75 "puts a whole number of screen pixels on a texel
        /// at every common render height - 2 at 720, 3 at 1080, 6 at 2160". Those are the numbers
        /// 37.5 had: DOUBLING the density HALVES the screen pixels a texel gets, so at 75 they are
        /// 1, 1.5 and 3. The 1.5 is why 1080p could not hold the tuned camera size and silently
        /// snapped 25% tighter. Measured, not reasoned - see PixelPerfectZoom.SizeFor.
        ///
        /// Integer-ratio safety is NOT a property of this constant on its own; it is a property of
        /// the snap, which now rounds against <see cref="FinestUnit"/>. See that constant.
        /// </summary>
        public const float LayoutUnit = 75f;

        /// <summary>
        /// The FINEST density any in-game sprite is drawn at, and the one the pixel-perfect snap
        /// rounds against.
        ///
        /// Snapping against the finest density is what makes the whole ladder safe at once. The
        /// snap used to round <c>H / (2S * LayoutUnit)</c>, which guarantees an integer only for
        /// 75 - and an integer at 75 is a HALF-integer at 150, which is the alternating-column
        /// shimmer the fixed densities exist to prevent. Inverted, it is guaranteed both ways: an
        /// integer k at 150 gives exactly 2k at 75, and 4k at 37.5. Every coarser density on the
        /// ladder divides in.
        ///
        /// So this must stay the LARGEST ppu in use by anything the game camera renders. Menu art
        /// is exempt - the character screen magnifies it well past one screen pixel per texel, and
        /// nothing there moves relative to the camera for a shimmer to show up in.
        /// </summary>
        public const float FinestUnit = 150f;

        /// <summary>Layout cells to world units, for offsets and attach points.</summary>
        public static float Px(float texels) => texels / LayoutUnit;

        public static Vector2 Px(float x, float y) => new(x / LayoutUnit, y / LayoutUnit);

        /// <summary>Bounded by the fixed set of declared pieces - nothing here is ever freed.</summary>
        static readonly Dictionary<string, Sprite> _cache = new();

        /// <summary>
        /// The world size this grid renders at. Hand it straight to <c>LayerSprite.Size</c>:
        /// because the sprite's own bounds are already cols/PPU x rows/PPU, the rig's
        /// <c>Size / bounds</c> scaling becomes exactly (1,1) and the art cannot be distorted.
        /// </summary>
        /// <summary>
        /// Must be called with the SAME <paramref name="outline"/> value passed to the matching
        /// <see cref="From"/> - the padding it adds changes the real texture size, and a mismatch
        /// here is exactly what silently reintroduces the non-uniform stretch this type exists to
        /// prevent.
        /// </summary>
        public static Vector2 SizeOf(string[] rows, bool outline = false,
                                    float pixelsPerUnit = LayoutUnit)
        {
            int pad = outline ? OutlineCanvasPadFor(pixelsPerUnit) * 2 : 0;
            return new Vector2((Columns(rows) + pad) / pixelsPerUnit, (rows.Length + pad) / pixelsPerUnit);
        }

        /// <summary>
        /// Rasterise a grid. <paramref name="key"/> is the cache identity and must be unique per
        /// piece - two pieces sharing a key would silently share art.
        /// </summary>
        /// <summary>
        /// How many texels thick an auto-outline is, and therefore how much transparent border
        /// the canvas gains so the stroke has somewhere to go without clipping.
        ///
        /// DERIVED from the density rather than fixed, because an outline is a WORLD-SIZE
        /// decision, not a texel count: one texel at 37.5 ppu and two at 75 are the same line on
        /// screen. When this was the constant 2 it silently doubled the border weight of anything
        /// drawn at the coarser density, which is the loudest possible way for a piece to look
        /// wrong while every measurement says it is the right size.
        ///
        /// INTERNAL rather than private because a piece's real extent on the body is its grid PLUS
        /// this, and anything placing one plate flush against another has to know that. Gear code
        /// that measured with the grid alone put a tasset two cells under a belt that actually
        /// reached two cells further down, so the belt drew over the tasset's own lit top edge and
        /// the plate below it read as a shadow rather than as armour.
        /// </summary>
        internal static int OutlinePadFor(float pixelsPerUnit)
            => Mathf.Max(1, Mathf.RoundToInt(pixelsPerUnit / LayoutUnit));

        /// <summary>
        /// How thick the outline is actually DRAWN, and the canvas border it needs: ONE BODY ART
        /// PIXEL (1/37.5 of a unit) at arena densities, twice the <see cref="OutlinePadFor"/> line -
        /// which stays the LAYOUT contract every design is measured against (sword height, blade
        /// and guard budgets, plates sitting flush), so thickening the line moves nothing.
        ///
        /// Why: the body's own hand-drawn border is a full art pixel, gear's was half of one, and
        /// a clear pixel game draws every sprite in one weight of dark line (see the side-by-side
        /// against the reference ad). Menu art (past <see cref="FinestUnit"/>) keeps its thin line:
        /// it is magnified in the character screen, where a doubled stroke would be heavy.
        ///
        /// The extra thickness is drawn OUTSIDE only - see StrokeOutline: inside a narrow channel
        /// (a disc's hollow, a fork's gap, a tear) the stroke stays at the layout thickness, so
        /// every gap tuned to survive the outline still does.
        /// </summary>
        internal static int OutlineCanvasPadFor(float pixelsPerUnit)
            => pixelsPerUnit > FinestUnit
                ? OutlinePadFor(pixelsPerUnit)
                : Mathf.Max(1, Mathf.RoundToInt(pixelsPerUnit / (LayoutUnit / 2f)));

        /// <summary>
        /// Rasterise a grid. <paramref name="key"/> is the cache identity and must be unique per
        /// piece - two pieces sharing a key would silently share art.
        ///
        /// <paramref name="outline"/> strokes a single 1px border around the shape's own
        /// silhouette automatically, using <see cref="Palette.Outline"/>. This is what gear uses:
        /// hand-drawing a full border into every grid meant that overlapping pieces stacked
        /// border-on-border at every seam, which is most of why a fully equipped character read
        /// as noise rather than as twelve distinct pieces. Body parts keep hand-drawn borders
        /// (outline: false, the default) because their border is already sized into the outline
        /// contract the rest of the rig is tuned against - auto-outlining them too would draw a
        /// second border outside the first.
        /// </summary>
        /// <summary>
        /// The sprite already built under <paramref name="key"/>, or null. For callers whose ROWS
        /// are expensive to produce (a head or mane through the hair pass) and that repaint often
        /// - checking first skips building a grid <see cref="From"/> would only throw away.
        /// </summary>
        public static Sprite Cached(string key)
            => _cache.TryGetValue(key, out var cached) && cached != null ? cached : null;

        public static Sprite From(string key, string[] rows, IReadOnlyDictionary<char, Color> palette,
                                  bool outline = false, Vector2Int? pivotTexel = null,
                                  float pixelsPerUnit = LayoutUnit, Color? outlineColor = null)
        {
            // The null recheck matters: a destroyed sprite is not C# null, so without it a torn
            // down texture would be handed back as a live one.
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var sprite = Build(rows, palette, outline, pivotTexel, pixelsPerUnit, key, outlineColor);
            _cache[key] = sprite;
            return sprite;
        }

        static readonly Dictionary<Sprite, Sprite> _silhouettes = new();

        /// <summary>
        /// A pure-white copy of a sprite, alpha preserved.
        ///
        /// Baked colour cannot be flashed by tinting: SpriteRenderer.color MULTIPLIES, so it can
        /// only ever darken art that already carries its own RGB, and Lerp(white, white) is a
        /// no-op. Swapping the texture is the only way to whiten pixel art. Dimensions, pivot and
        /// PPU are identical, so offset, scale and sorting are all untouched by the swap.
        /// </summary>
        public static Sprite Silhouette(Sprite src)
        {
            if (src == null) return null;
            if (_silhouettes.TryGetValue(src, out var cached) && cached != null) return cached;

            var st = src.texture;
            var tex = new Texture2D(st.width, st.height, TextureFormat.RGBA32, false)
            {
                filterMode = st.filterMode,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var px = st.GetPixels();
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, px[i].a);
            tex.SetPixels(px);
            tex.Apply();

            // The SOURCE pivot, not a centred one. This line used to hardcode (0.5, 0.5) while
            // the summary above claimed the pivot was preserved, and most of the time nothing
            // showed it: a hit flash lasts a tenth of a second, and a layer jumping by the gap
            // between its real pivot and its centre for that long passes for part of the impact.
            // Anything that holds the silhouette longer - BulwarkAura draws one for a full second
            // - renders the whole figure visibly dislocated instead. See PixelSprite.Build, where
            // pivot01 is explicitly NOT centred for layers that have to rotate about a joint.
            var pivot01 = new Vector2(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height);
            var white = Sprite.Create(tex, src.rect, pivot01, src.pixelsPerUnit);
            _silhouettes[src] = white;
            return white;
        }

        static readonly Dictionary<Sprite, Sprite> _stones = new();

        static readonly Color StoneLit = new(0.80f, 0.80f, 0.77f);
        static readonly Color StoneDeep = new(0.20f, 0.21f, 0.23f);

        /// <summary>
        /// A STONE copy of a sprite - Medusa's statue. Luminance kept, colour drained: each texel's
        /// brightness picks a point on one grey stone ramp, so every edge, fold and ramp step the
        /// art has survives and only the colour goes. The ramp is compressed (never black, never
        /// white) because a statue is one material - the outline stays the darkest thing on it and
        /// a white highlight would read as paint left on the stone.
        ///
        /// Built and cached the same way <see cref="Silhouette"/> is, pivot and PPU kept, so the
        /// swap moves nothing. Art whose texture is not readable comes back unchanged.
        /// </summary>
        public static Sprite Stone(Sprite src)
        {
            if (src == null) return null;
            if (_stones.TryGetValue(src, out var cached) && cached != null) return cached;

            var st = src.texture;
            if (st == null || !st.isReadable) return src;

            var tex = new Texture2D(st.width, st.height, TextureFormat.RGBA32, false)
            {
                filterMode = st.filterMode,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var px = st.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                float l = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                // A slight S-curve so the midtones separate; flat linear greys read as fog.
                l = Mathf.SmoothStep(0f, 1f, l);
                var g = Color.Lerp(StoneDeep, StoneLit, l);
                px[i] = new Color(g.r, g.g, g.b, c.a);
            }
            tex.SetPixels(px);
            tex.Apply();

            var pivot01 = new Vector2(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height);
            var stone = Sprite.Create(tex, src.rect, pivot01, src.pixelsPerUnit);
            _stones[src] = stone;
            return stone;
        }

        static readonly Dictionary<(Sprite, int, bool), Sprite> _rims = new();

        /// <summary>
        /// A white RIM <paramref name="pad"/> texels thick hugging a sprite's silhouette from
        /// OUTSIDE, everything else clear - the locked target's highlight (Player.TargetHighlight).
        /// The canvas grows by the pad on every side and the pivot moves with it, so drawn at the
        /// source's own transform it lands exactly around the art. White, so the renderer's colour
        /// is the highlight's colour.
        ///
        /// Grown by a disc of radius <paramref name="pad"/>, not a square: at pad 1 that is the four
        /// neighbours only, which is what keeps a diagonal edge a staircase rather than a thick
        /// smear. <paramref name="dashed"/> keeps runs of two pad-sized blocks and drops the next
        /// two, counted as (x + y) - every step along a 4-connected rim moves that by exactly one,
        /// so straight and stepped edges dash at the same rhythm.
        ///
        /// Cached per (source, pad, dashed). Art whose texture is not readable gives null - the
        /// caller draws nothing rather than a box.
        /// </summary>
        public static Sprite Rim(Sprite src, int pad, bool dashed)
        {
            if (src == null) return null;
            pad = Mathf.Max(1, pad);
            var key = (src, pad, dashed);
            if (_rims.TryGetValue(key, out var cached) && cached != null) return cached;

            var st = src.texture;
            if (st == null || !st.isReadable) return null;

            var r = src.rect;
            int w = (int)r.width, h = (int)r.height;
            var px = st.GetPixels((int)r.x, (int)r.y, w, h);
            int W = w + pad * 2, H = h + pad * 2;
            var outPx = new Color[W * H];

            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a > 0.5f;

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int sx = x - pad, sy = y - pad;
                    if (Solid(sx, sy)) continue;

                    bool near = false;
                    for (int dy = -pad; dy <= pad && !near; dy++)
                        for (int dx = -pad; dx <= pad && !near; dx++)
                            if (dx * dx + dy * dy <= pad * pad && Solid(sx + dx, sy + dy)) near = true;
                    if (!near) continue;

                    if (dashed && ((x / pad + y / pad) / 2) % 2 == 1) continue;
                    outPx[y * W + x] = Color.white;
                }

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(outPx);
            tex.Apply();

            var pivot01 = new Vector2((src.pivot.x + pad) / W, (src.pivot.y + pad) / H);
            var made = Sprite.Create(tex, new Rect(0, 0, W, H), pivot01, src.pixelsPerUnit);
            made.name = src.name + ".rim";
            _rims[key] = made;
            return made;
        }

        static readonly Dictionary<(Sprite, int, int, int), Sprite> _glints = new();

        /// <summary>
        /// One frame of a glint running up a weapon - frame <paramref name="frame"/> of
        /// <paramref name="frames"/>, bottom to top. Only the band's texels are set (white), so it
        /// is an OVERLAY drawn on the weapon's own transform, never a replacement for its sprite:
        /// the rig keeps swapping that sprite (flash, stone, idle flipbooks) and an overlay just
        /// follows whatever is there.
        ///
        /// The band is a diagonal about five texels deep, and lights INTERIOR texels only - an opaque
        /// texel with all four neighbours opaque - so the dark outline stays dark and the light
        /// reads as passing over the surface rather than spilling off it. It starts
        /// <paramref name="from"/> of the way up, clear of the grip the hands close on.
        /// </summary>
        public static Sprite Glint(Sprite src, int frame, int frames, float from)
        {
            if (src == null || frames <= 0) return null;
            var key = (src, frame, frames, Mathf.RoundToInt(from * 100f));
            if (_glints.TryGetValue(key, out var cached) && cached != null) return cached;

            var st = src.texture;
            if (st == null || !st.isReadable) return null;

            var r = src.rect;
            int w = (int)r.width, h = (int)r.height;
            var px = st.GetPixels((int)r.x, (int)r.y, w, h);
            var outPx = new Color[w * h];

            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && px[y * w + x].a > 0.5f;

            // The band leans one texel up for every two across, and travels the whole height
            // plus its own lean so it enters fully below the start row and leaves fully above.
            float lean = w * 0.5f;
            float start = h * Mathf.Clamp01(from) - lean;
            float centre = Mathf.Lerp(start, h + lean, (frame + 0.5f) / frames);
            const float half = 2.5f;
            int fromRow = Mathf.FloorToInt(h * Mathf.Clamp01(from));

            for (int y = fromRow; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (Mathf.Abs(y + x * 0.5f - centre) > half) continue;
                    if (!Solid(x, y) || !Solid(x - 1, y) || !Solid(x + 1, y)
                        || !Solid(x, y - 1) || !Solid(x, y + 1)) continue;
                    outPx[y * w + x] = Color.white;
                }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(outPx);
            tex.Apply();

            var pivot01 = new Vector2(src.pivot.x / w, src.pivot.y / h);
            var made = Sprite.Create(tex, new Rect(0, 0, w, h), pivot01, src.pixelsPerUnit);
            made.name = src.name + ".glint" + frame;
            _glints[key] = made;
            return made;
        }

        static readonly Dictionary<Sprite, Sprite> _mirrors = new();

        /// <summary>
        /// A copy of a sprite flipped left to right, its pivot flipped with it - a piece worn on
        /// the other side of the body (GearItem.Lopsided). A real sprite rather than a flipX on
        /// the renderer, because the rig CUTS arm layers at the elbow and wrist, and the cut
        /// halves would not carry the flag. Cached like <see cref="Silhouette"/>; art whose
        /// texture is not readable comes back unchanged.
        /// </summary>
        public static Sprite Mirror(Sprite src)
        {
            if (src == null) return null;
            if (_mirrors.TryGetValue(src, out var cached) && cached != null) return cached;

            var st = src.texture;
            if (st == null || !st.isReadable) return src;

            var r = src.rect;
            int w = (int)r.width, h = (int)r.height;
            var px = st.GetPixels((int)r.x, (int)r.y, w, h);
            var flipped = new Color[px.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    flipped[y * w + x] = px[y * w + (w - 1 - x)];

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = st.filterMode,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(flipped);
            tex.Apply();

            var pivot01 = new Vector2(1f - src.pivot.x / r.width, src.pivot.y / r.height);
            var made = Sprite.Create(tex, new Rect(0, 0, w, h), pivot01, src.pixelsPerUnit);
            made.name = src.name + ".mirror";
            _mirrors[src] = made;
            return made;
        }

        static readonly Dictionary<(Sprite, Sprite, Vector2Int), Sprite> _enclosed = new();

        /// <summary>
        /// A copy of <paramref name="src"/> with every texel that pokes OUT of
        /// <paramref name="frame"/>'s silhouette cleared - a helmet worn under a hood
        /// (PrimitiveCharacterRig.EncloseHelmInHood), the same rule BodyLook.Mane applies to
        /// hair: nothing pokes through the crown or the cowl.
        ///
        /// "Inside" is per ROW of the frame: between its leftmost and rightmost opaque texels, so
        /// the face opening counts as inside and whatever shows through it is kept. Above the
        /// frame's top opaque row is outside. BELOW its bottom opaque row is kept - hair flows out
        /// from under a hood's hem, and so does anything else that reaches that far.
        ///
        /// <paramref name="srcToFrame"/> maps a point in src's sprite space (world units from its
        /// pivot) into frame's, so the two may be drawn at different densities. Cached per (src,
        /// frame, where src's pivot lands in frame texels). Art whose texture is not readable comes
        /// back unchanged; a src with nothing outside the frame comes back as itself.
        /// </summary>
        public static Sprite Enclosed(Sprite src, Sprite frame, Matrix4x4 srcToFrame)
        {
            if (src == null || frame == null) return src;
            var ft = frame.texture;
            var st = src.texture;
            if (st == null || !st.isReadable || ft == null || !ft.isReadable) return src;

            var fr = frame.rect;
            int fw = (int)fr.width, fh = (int)fr.height;
            float fppu = frame.pixelsPerUnit;
            Vector2 fpivot = frame.pivot;

            Vector3 o = srcToFrame.MultiplyPoint3x4(Vector3.zero);
            var key = (src, frame, new Vector2Int(Mathf.RoundToInt(o.x * fppu * 4f), Mathf.RoundToInt(o.y * fppu * 4f)));
            if (_enclosed.TryGetValue(key, out var cached) && cached != null) return cached;

            // Each frame row's opaque span, and the band of rows that have one.
            var fpx = ft.GetPixels32();
            int ftw = ft.width;
            var minX = new int[fh];
            var maxX = new int[fh];
            int bottom = -1, top = -1;
            for (int y = 0; y < fh; y++)
            {
                minX[y] = int.MaxValue; maxX[y] = int.MinValue;
                int row = ((int)fr.y + y) * ftw + (int)fr.x;
                for (int x = 0; x < fw; x++)
                {
                    if (fpx[row + x].a == 0) continue;
                    if (x < minX[y]) minX[y] = x;
                    if (x > maxX[y]) maxX[y] = x;
                }
                if (maxX[y] < 0) continue;
                if (bottom < 0) bottom = y;
                top = y;
            }
            if (top < 0) return src;

            var r = src.rect;
            int w = (int)r.width, h = (int)r.height;
            float sppu = src.pixelsPerUnit;
            Vector2 spivot = src.pivot;
            var px = st.GetPixels32();
            int stw = st.width;
            bool any = false;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = ((int)r.y + y) * stw + (int)r.x + x;
                if (px[i].a == 0) continue;

                // ALL of the texel, not its centre: sampled at its four quarter points, so a src
                // texel coarser than the frame's cannot keep the half of itself hanging past the
                // outline. At equal densities all four land in one frame texel.
                bool inside = true;
                for (int q = 0; q < 4 && inside; q++)
                {
                    var p = srcToFrame.MultiplyPoint3x4(new Vector3((x + 0.25f + 0.5f * (q & 1) - spivot.x) / sppu,
                                                                    (y + 0.25f + 0.5f * (q >> 1) - spivot.y) / sppu, 0f));
                    int fx = Mathf.FloorToInt(p.x * fppu + fpivot.x);
                    int fy = Mathf.FloorToInt(p.y * fppu + fpivot.y);
                    inside = fy < bottom                                                 // below the hem
                             || (fy <= top && fx >= minX[fy] && fx <= maxX[fy]);
                }
                if (inside) continue;
                px[i] = new Color32(0, 0, 0, 0);
                any = true;
            }

            if (!any)
            {
                _enclosed[key] = src;
                return src;
            }

            var tex = new Texture2D(st.width, st.height, TextureFormat.RGBA32, false)
            {
                filterMode = st.filterMode,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();

            var pivot01 = new Vector2(spivot.x / r.width, spivot.y / r.height);
            var made = Sprite.Create(tex, r, pivot01, sppu);
            made.name = $"{src.name}.in.{frame.name}";
            _enclosed[key] = made;
            return made;
        }

        static readonly Dictionary<(Sprite, string), Sprite> _recoloured = new();

        /// <summary>
        /// A copy of a sprite with every texel matching one of <paramref name="from"/> replaced by
        /// the colour at the same index in <paramref name="to"/> - bare skin on a piece of gear
        /// repainted in the wearer's own tone (see BodyLook.Reskin). <paramref name="tag"/> names
        /// the mapping for the cache, so one source recoloured two ways is two entries.
        ///
        /// Matched to within one step a channel, not exactly: a palette colour goes into the
        /// texture as a float and comes back as a byte. Built and cached like
        /// <see cref="Silhouette"/>, pivot and PPU kept, so the swap moves nothing. Art whose
        /// texture is not readable comes back unchanged.
        /// </summary>
        public static Sprite Recolour(Sprite src, string tag, Color[] from, Color[] to)
        {
            if (src == null) return null;
            if (_recoloured.TryGetValue((src, tag), out var cached) && cached != null) return cached;

            var st = src.texture;
            if (st == null || !st.isReadable) return src;

            var keys = new Color32[from.Length];
            for (int k = 0; k < from.Length; k++) keys[k] = from[k];

            var px = st.GetPixels32();
            bool any = false;
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                if (c.a == 0) continue;
                for (int k = 0; k < keys.Length; k++)
                {
                    var f = keys[k];
                    if (Mathf.Abs(c.r - f.r) > 1 || Mathf.Abs(c.g - f.g) > 1 || Mathf.Abs(c.b - f.b) > 1) continue;
                    Color32 t = to[k];
                    px[i] = new Color32(t.r, t.g, t.b, c.a);
                    any = true;
                    break;
                }
            }
            // Nothing to repaint (the plated half of a piece that is only partly bare): the source
            // itself, rather than a second texture identical to it.
            if (!any)
            {
                _recoloured[(src, tag)] = src;
                return src;
            }

            var tex = new Texture2D(st.width, st.height, TextureFormat.RGBA32, false)
            {
                filterMode = st.filterMode,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();

            var pivot01 = new Vector2(src.pivot.x / src.rect.width, src.pivot.y / src.rect.height);
            var made = Sprite.Create(tex, src.rect, pivot01, src.pixelsPerUnit);
            made.name = $"{src.name}.{tag}";
            _recoloured[(src, tag)] = made;
            return made;
        }

        /// <summary>
        /// Paint every transparent texel that touches an opaque one, <paramref name="thickness"/>
        /// times over so the border is that many texels thick - the passes past
        /// <paramref name="inner"/> only outside narrow channels (see OutlineCanvasPadFor). 4-connected, not 8: a
        /// diagonal-only outline reads as a thick blob at corners rather than a clean line.
        /// Thickness comes from <see cref="OutlinePadFor"/> so the line is the same WIDTH on
        /// screen at either density - each pass treats the previous ring as opaque and grows one
        /// more.
        /// </summary>
        static void StrokeOutline(Color[] px, int w, int h, int thickness, int inner, Color? color = null)
        {
            var strokeColor = color ?? Palette.Outline;

            // The art as drawn, before any stroke - what the channel test measures against.
            var art = new bool[w * h];
            for (int i = 0; i < art.Length; i++) art[i] = px[i].a > 0.001f;

            // A texel is in a CHANNEL when there is art on BOTH sides of it, left and right or
            // above and below, within reach. The passes past `inner` skip those: a hollow, a fork
            // or a tear keeps exactly the stroke it was tuned against, and only the outside of a
            // silhouette gets the heavier line.
            int reach = thickness * 2 + 1;
            bool InChannel(int x, int y)
            {
                bool Art(int ax, int ay) => ax >= 0 && ax < w && ay >= 0 && ay < h && art[ay * w + ax];
                bool left = false, right = false, down = false, up = false;
                for (int d = 1; d <= reach; d++)
                {
                    left |= Art(x - d, y); right |= Art(x + d, y);
                    down |= Art(x, y - d); up |= Art(x, y + d);
                }
                return (left && right) || (down && up);
            }

            for (int pass = 0; pass < thickness; pass++)
            {
                var stroke = new List<int>();
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (px[i].a > 0.001f) continue;
                    bool touchesOpaque =
                        (x > 0     && px[i - 1].a > 0.001f) ||
                        (x < w - 1 && px[i + 1].a > 0.001f) ||
                        (y > 0     && px[i - w].a > 0.001f) ||
                        (y < h - 1 && px[i + w].a > 0.001f);
                    if (!touchesOpaque) continue;
                    if (pass >= inner && InChannel(x, y)) continue;
                    stroke.Add(i);
                }
                foreach (var i in stroke) px[i] = strokeColor;
            }
        }

        static int Columns(string[] rows)
        {
            int w = 0;
            foreach (var r in rows) w = Mathf.Max(w, r.Length);
            return w;
        }

        /// <summary>
        /// Rotate a swung item around where the HAND holds it, not around the shape's own middle.
        ///
        /// Sprite.Create's pivot IS the point a parent transform's rotation swings the whole
        /// sprite around, which is also the point Offset places in the parent's space. A weapon
        /// with the default centre pivot has its blade extend equally above and below wherever
        /// the offset is planted, so no offset choice makes it look gripped: at rest the grip
        /// sits in mid-air near the middle of the blade, and mid-swing the whole sword orbits
        /// that empty point instead of pivoting from the fist. `pivotTexel` is the grip's cell in
        /// the grid AS WRITTEN (same column/row numbering you count the string literal in) - the
        /// bookkeeping to turn that into a bottom-left-origin, padding-aware, 0..1 sprite pivot
        /// happens here so the grid stays easy to read.
        /// </summary>
        /// <param name="key">
        /// The caller's cache key, used ONLY to name the piece in the two grid-hygiene warnings
        /// below. Both said what was wrong and never which sprite it was wrong on, which is most
        /// of why they went unchased across a catalogue of forty-odd pieces - a warning nobody can
        /// act on is noise. Optional, because Build is also reachable from callers with no key.
        /// </param>
        static Sprite Build(string[] rows, IReadOnlyDictionary<char, Color> palette, bool outline,
                            Vector2Int? pivotTexel, float pixelsPerUnit, string key = null,
                            Color? outlineColor = null)
        {
            string who = string.IsNullOrEmpty(key) ? "" : $" ({key})";
            int rawW = Mathf.Max(1, Columns(rows));
            int rawH = Mathf.Max(1, rows.Length);
            int pad = outline ? OutlineCanvasPadFor(pixelsPerUnit) : 0;
            int w = rawW + pad * 2;
            int h = rawH + pad * 2;

            // The rig mirrors by negating the ROOT's x scale. A centred pivot only lands back on
            // a texel boundary if the width is even; on an odd width the flipped figure sits half
            // a texel across and twitches every time the player turns around.
            if ((w & 1) != 0)
                Debug.LogWarning($"[PixelSprite] width {w} is odd{who} - the mirrored pose will " +
                                 "sit half a texel off. Pad the grid to an even width.");

            // A short row is padded with transparency, which silently eats the right edge of the
            // art; a stray space does the same mid-row. Both look like a drawing mistake rather
            // than a typo, so say so. Checked against the declared grid, before outline padding.
            foreach (var r in rows)
            {
                if (r.Length != rawW)
                {
                    Debug.LogWarning($"[PixelSprite] ragged grid{who}: a row is {r.Length} of " +
                                     $"{rawW} columns and will be padded with transparency.");
                    break;
                }
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var px = new Color[w * h];
            for (int y = 0; y < rawH; y++)
            {
                // rows[0] is the TOP row as written in source, but Unity textures start at the
                // bottom. Flipping here is what lets the grids read as pictures in the code.
                var row = rows[rawH - 1 - y];
                for (int x = 0; x < rawW; x++)
                {
                    char c = x < row.Length ? row[x] : '.';
                    var col = palette.TryGetValue(c, out var found) ? found : Color.clear;
                    px[(y + pad) * w + (x + pad)] = col;
                }
            }

            if (outline) StrokeOutline(px, w, h, pad, OutlinePadFor(pixelsPerUnit), outlineColor);

            tex.SetPixels(px);
            tex.Apply();

            Vector2 pivot01;
            if (pivotTexel.HasValue)
            {
                // Column is the same left-to-right count either way. Row flips because rows[0] is
                // the TOP row as written, but texture row 0 is the BOTTOM.
                int col = pivotTexel.Value.x + pad;
                int rowFromTop = pivotTexel.Value.y;
                int rowFromBottom = (rawH - 1 - rowFromTop) + pad;
                pivot01 = new Vector2((col + 0.5f) / w, (rowFromBottom + 0.5f) / h);
            }
            else
            {
                pivot01 = new Vector2(0.5f, 0.5f);
            }

            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot01, pixelsPerUnit);
        }
    }
}
