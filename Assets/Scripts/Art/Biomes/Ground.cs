using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// THE GROUND ENDS AT A LIP (Core.Arena's open edges; the user's calls, 2026-11-01). On a floor
    /// with open edges the arena's tiled floor is switched off and the floor is drawn again as one
    /// texture of the room's own shape: the floor's tile wherever there is ground, and past every
    /// edge the place's void - a CLIFF FACE under a south-facing lip where the void is a drop (the
    /// tower's shaft, the sky), a BANK, foam and shallows deepening away where it is water (the
    /// forest's lake, the salt's brine). Behind it, one plane of the void itself (<see cref="Void"/>).
    ///
    /// SOFT OUTLINES: the cells are rounded through a blurred mask, and the drawn ground only ever
    /// gives way INSIDE the cells - a convex corner is cut back by less than a body's radius
    /// (Tuning.Ground.RoundTexels), so the barrier never stands visibly off the drawn lip.
    ///
    /// Built when the room's shape is known (Hazards.RoomWalls.Apply), behind the transition's
    /// black, under the floor's Scenery object so the next dressing takes it away. Seeded from the
    /// floor's scenery, never UnityEngine.Random.
    /// </summary>
    public static class Ground
    {
        const float Ppu = Scenery.Ppu;

        public static void Build()
        {
            Parallax.Rise = 0f;
            var root = Scenery.Root;
            if (root == null) return;
            var floor = root.Find("Floor");
            var scenery = root.Find("Scenery");
            var fsr = floor != null ? floor.GetComponent<SpriteRenderer>() : null;
            if (scenery != null)
                for (int i = scenery.childCount - 1; i >= 0; i--)
                {
                    var c = scenery.GetChild(i);
                    if (c.name == "ground" || c.name == "void" || c.name == "fog" || c.name == "far" || c.name == "foreground" || c.name == "crystals") Object.Destroy(c.gameObject);
                }
            if (!Arena.EdgesOpen || scenery == null || fsr == null || fsr.sprite == null)
            {
                if (fsr != null) fsr.enabled = true;
                Lighting.LightField.ClearEdge();
                return;
            }
            var owned = scenery.GetComponent<SceneryOwned>();
            fsr.enabled = false;
            Paint(scenery, owned, fsr.sprite, Scenery.Current.Seed);
            Void(scenery);
            FarLayers.Build(scenery, owned, Scenery.Current.Seed);
            FarSights.Build(scenery.Find("far") ?? scenery, owned, Scenery.Current.Seed);
            Foreground.Build(scenery, owned, Scenery.Current.Seed);
        }

        // ------------------------------------------------------------------ the void

        /// <summary>The place's void as one plane under the ground: the shaft's dark, the sky,
        /// deep water. Far layers (parallax) lie over it.</summary>
        static void Void(Transform parent)
        {
            var go = new GameObject("void");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = Tuning.Ground.VoidOrder;
            var half = Arena.HalfExtents + Vector2.one * Tuning.Ground.VoidMargin;
            if (Biome.Is(BiomeKind.SkyRuin))
            {
                sr.sprite = SkyArt.Sky();
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = half * 2f;
                return;
            }
            sr.sprite = Spr.Square;
            sr.color = Arena.EdgeIsWater ? Hazards.LakeSurface.Deep
                     : Biome.Is(BiomeKind.SnowyPeaks) ? new Color(0.66f, 0.74f, 0.86f)    // the valley's haze
                     : Biome.Is(BiomeKind.Caves) ? new Color(0.012f, 0.014f, 0.022f)       // the abyss
                     : Tuning.Ground.ShaftDark;
            go.transform.localScale = new Vector3(half.x * 2f, half.y * 2f, 1f);
        }

        // ------------------------------------------------------------------ the ground

        static void Paint(Transform parent, SceneryOwned owned, Sprite tile, int seed)
        {
            var inner = Arena.Inner;
            float pad = Tuning.Ground.Pad;
            float x0 = -inner.x - pad, y0 = -inner.y - pad, y1 = inner.y + Tuning.Ground.TopReach;
            int W = Mathf.RoundToInt((inner.x + pad) * Ppu) * 2;
            int H = Mathf.RoundToInt((y1 - y0) * Ppu);
            x0 = -W * 0.5f / Ppu;

            // ---- the raw mask: ground inside the walls' inner faces, wherever no edge is open
            var raw = new float[W * H];
            for (int j = 0; j < H; j++)
            {
                float wy = y0 + (j + 0.5f) / Ppu;
                for (int i = 0; i < W; i++)
                {
                    float wx = x0 + (i + 0.5f) / Ppu;
                    bool g = wx >= -inner.x && wx <= inner.x && wy >= -inner.y && wy <= inner.y
                             && !Arena.OpenEdgeAt(new Vector2(wx, wy));
                    raw[j * W + i] = g ? 1f : 0f;
                }
            }

            // ---- rounded both ways, through the blurred mask
            int r = Tuning.Ground.RoundTexels;
            var soft = Blur(Blur(raw, W, H, r), W, H, r);
            bool organic = Biome.Is(BiomeKind.Forest) || Biome.Is(BiomeKind.SaltFlats);
            var ground = new bool[W * H];
            for (int k = 0; k < ground.Length; k++)
            {
                int i = k % W, j = k / W;
                // Never past the walls' inner faces, whatever the blur says: the outer barrier
                // stands exactly there.
                float wx = x0 + (i + 0.5f) / Ppu, wy = y0 + (j + 0.5f) / Ppu;
                if (wx < -inner.x || wx > inner.x || wy < -inner.y || wy > inner.y) continue;
                float wob = (Noise(i / 9f, j / 9f, seed + 11) - 0.5f) * (organic ? 0.3f : 0.12f);
                ground[k] = soft[k] > Tuning.Ground.RoundThreshold + wob;
            }

            // ---- distances out over the void: any way (chamfer), and straight up to a lip
            var dist = Chamfer(ground, W, H);
            var up = new int[W * H];
            for (int i = 0; i < W; i++)
            {
                int last = -1;
                for (int j = H - 1; j >= 0; j--)
                {
                    if (ground[j * W + i]) { last = j; up[j * W + i] = 0; continue; }
                    up[j * W + i] = last < 0 ? 9999 : last - j;
                }
            }

            // ---- the edge's dark (Lighting.LightField) and the fog over the drops
            LayEdge(ground, dist, W, H, new Vector2(x0, y0));
            EdgeFog.Spawn(parent, dist, W, H, new Vector2(x0, y0), Ppu, seed);
            if (Biome.Is(BiomeKind.Caves)) LipCrystals(parent, owned, ground, W, H, new Vector2(x0, y0), seed);

            // ---- the tile, read once
            var tex = tile.texture;
            var rect = tile.textureRect;
            int tw = (int)rect.width, th = (int)rect.height, tx0 = (int)rect.x, ty0 = (int)rect.y;
            var tpx = tex.GetPixels32();
            int texW = tex.width;
            float tilePpu = tile.pixelsPerUnit;

            bool water = Arena.EdgeIsWater;
            var cliffTop = Color.Lerp(Scenery.WallStone, CliffTint, 0.25f) * 1.75f;
            var cliffLow = Color.Lerp(Scenery.WallStone, CliffTint, 0.6f) * 0.55f;
            cliffTop.a = cliffLow.a = 1f;
            int face = Tuning.Ground.FaceTexels;

            // ---- each texel's tier (Hazards.TierLayouts), for the shading of higher and lower ground
            sbyte[] tierOf = null;
            if (Arena.HasTiers)
            {
                tierOf = new sbyte[W * H];
                for (int j = 0; j < H; j++)
                for (int i = 0; i < W; i++)
                    tierOf[j * W + i] = (sbyte)Arena.TierAt(new Vector2(x0 + (i + 0.5f) / Ppu, y0 + (j + 0.5f) / Ppu));
            }
            int TierTexel(int i, int j) => tierOf[Mathf.Clamp(j, 0, H - 1) * W + Mathf.Clamp(i, 0, W - 1)];
            // The tile's own colour, averaged: the stone a tier's face is cut from.
            Color tpxAvg = Color.black;
            if (tierOf != null)
            {
                double rr = 0, gg = 0, bb = 0; int nn = 0;
                for (int y = ty0; y < ty0 + th; y++)
                for (int x = tx0; x < tx0 + tw; x++)
                { var q = tpx[y * texW + x]; rr += q.r; gg += q.g; bb += q.b; nn++; }
                if (nn > 0) tpxAvg = new Color((float)(rr / nn / 255.0), (float)(gg / nn / 255.0), (float)(bb / nn / 255.0), 1f);
            }

            var px = new Color32[W * H];
            for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
            {
                int k = j * W + i;
                if (ground[k])
                {
                    // The tile at this texel's world position (the tile's own density).
                    float wx = x0 + (i + 0.5f) / Ppu, wy = y0 + (j + 0.5f) / Ppu;
                    int ti = Mod(Mathf.FloorToInt(wx * tilePpu), tw), tj = Mod(Mathf.FloorToInt(wy * tilePpu), th);
                    Color c = tpx[(ty0 + tj) * texW + tx0 + ti];
                    bool lipBelow = j < 2 || !ground[k - W] || !ground[k - 2 * W];
                    bool rim = i == 0 || i == W - 1 || j == H - 1 || !ground[k - 1] || !ground[k + 1] || !ground[k + W];
                    if (lipBelow) c = water ? Color.Lerp(c, Hazards.LakeSurface.Lip, 0.55f) : Shade(c, 0.09f);
                    else if (rim) c = Shade(c, water ? -0.08f : -0.05f);
                    // A TIER above stands nearer the light, a sunken one in shade; a raised edge
                    // catches the light along its arris and casts a shade on the ground beside it.
                    if (tierOf != null)
                    {
                        int tier = tierOf[k];
                        c = Shade(c, tier > 0 ? Tuning.Tiers.ShadeUp * tier : tier < 0 ? Tuning.Tiers.ShadeDown : 0f);
                        bool arris = false, shade = false;
                        for (int n = 1; n <= 2; n++)
                        {
                            if (TierTexel(i - n, j) < tier || TierTexel(i + n, j) < tier || TierTexel(i, j + n) < tier || TierTexel(i, j - n) < tier) arris = true;
                            if (TierTexel(i - n, j) > tier || TierTexel(i + n, j) > tier || TierTexel(i, j + n) > tier) shade = true;
                        }
                        if (arris) c = Shade(c, Tuning.Tiers.EdgeLight);
                        else if (shade) c = Shade(c, Tuning.Tiers.EdgeShade);

                        // A FACE: the texels just under a raised edge, the tier's stone seen side on
                        // in courses going dark toward the foot - or a flight of STAIRS, three treads
                        // lit along their noses.
                        int faceTexels = Mathf.RoundToInt(Tuning.Tiers.Face * Ppu);
                        for (int n = 1; n <= faceTexels; n++)
                        {
                            if (TierTexel(i, j + n) <= tier) continue;
                            float f = (n - 1f) / faceTexels;
                            var stone = tpxAvg;
                            if (Arena.StepsAt(new Vector2(wx, wy)))
                            {
                                int tread = (n - 1) * 3 / faceTexels;
                                c = Color.Lerp(stone * 1.05f, stone * 0.72f, tread / 2f);
                                if ((n - 1) % Mathf.Max(1, faceTexels / 3) == 0) c = stone * 1.22f;   // the nose
                                bool sideL = !Arena.StepsAt(new Vector2(wx - 1.5f / Ppu, wy)), sideR = !Arena.StepsAt(new Vector2(wx + 1.5f / Ppu, wy));
                                if (sideL || sideR) c = stone * 0.5f;
                            }
                            else
                            {
                                c = Color.Lerp(stone * 0.78f, stone * 0.42f, f);
                                if (n == 1) c = stone * 0.9f;
                                else if (n % 5 == 0 || ((i + (n > faceTexels / 2 ? 7 : 0)) % 16 == 0)) c = Shade(c, -0.06f);
                            }
                            break;
                        }
                    }
                    c.a = 1f;
                    px[k] = c;
                    continue;
                }

                int u = up[k];
                float d = dist[k];
                if (!water)
                {
                    // A drop: the slab's thickness under a south-facing lip, strata and all,
                    // going dark; the rest is left clear for the void behind.
                    if (u <= face)
                    {
                        float f = (u - 1f) / face;
                        var c = Color.Lerp(cliffTop, cliffLow, f * f * 0.6f + f * 0.4f);
                        if (u % 4 == 0) c = Shade(c, -0.06f);
                        if (Hash(i, j, seed) < 0.08f) c = Shade(c, -0.04f);
                        if (u == face) c.a = 0.6f;
                        px[k] = c;
                    }
                    else if (d <= 1.5f) px[k] = new Color(0f, 0f, 0f, 0.35f);   // a side lip's shade
                    else px[k] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // Water: a low bank under a south-facing lip, then foam, shallows, deep.
                const int bank = 5;
                Color w;
                if (u <= bank)
                    w = Color.Lerp(Hazards.LakeSurface.BankTop, Hazards.LakeSurface.BankLow, (u - 1f) / bank);
                else if (d <= 1.2f) w = Hazards.LakeSurface.Lip;
                else if (d <= 2.6f && Noise(i / 5f, j / 5f, seed + 3) > 0.42f) w = Hazards.LakeSurface.Foam;
                else
                {
                    float t = Mathf.Clamp01((d - 2f) / Tuning.Ground.ShallowTexels);
                    // Stepped, as the lakes are: bands of depth rather than a smooth gradient.
                    t = Mathf.Floor(t * 6f) / 6f;
                    w = t < 0.5f ? Color.Lerp(Hazards.LakeSurface.Shallow, Hazards.LakeSurface.Mid, t * 2f)
                                 : Color.Lerp(Hazards.LakeSurface.Mid, Hazards.LakeSurface.Deep, (t - 0.5f) * 2f);
                    if (Hash(i >> 1, j, seed + 5) > 0.992f) w = Color.Lerp(w, Hazards.LakeSurface.RippleColor, 0.35f);
                }
                w.a = 1f;
                px[k] = w;
            }

            var tx = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tx.SetPixels32(px);
            tx.Apply();
            var spr = Sprite.Create(tx, new Rect(0, 0, W, H), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
            spr.hideFlags = HideFlags.HideAndDontSave;
            if (owned != null) { owned.Add(tx); owned.Add(spr); }

            var go = new GameObject("ground");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x0, y0, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = spr;
            sr.sortingOrder = Tuning.Ground.Order;
        }

        /// <summary>
        /// The caves' light: crystal clusters growing at the floor's lip, each a small steady light
        /// (Lighting.Glow). A picture only - set just inside the lip, where nothing stands long.
        /// </summary>
        static void LipCrystals(Transform parent, SceneryOwned owned, bool[] ground, int W, int H, Vector2 origin, int seed)
        {
            var rng = new System.Random(seed * 31 + 17);
            var holder = new GameObject("crystals");
            holder.transform.SetParent(parent, false);
            int placed = 0;
            var spots = new System.Collections.Generic.List<Vector2>();
            for (int tries = 0; tries < 4000 && placed < Tuning.Ground.CaveCrystals; tries++)
            {
                int i = rng.Next(2, W - 2), j = rng.Next(2, H - 4);
                int k = j * W + i;
                // A ground texel with the void just below it: the lip itself.
                if (!ground[k] || ground[k - W * 2]) continue;
                var at = origin + new Vector2((i + 0.5f) / Ppu, (j + 0.5f) / Ppu);
                bool near = false;
                foreach (var s in spots) if ((s - at).sqrMagnitude < 9f) near = true;
                if (near || at.y > Arena.Inner.y - 1f) continue;
                spots.Add(at);
                var go = new GameObject("crystal");
                go.transform.SetParent(holder.transform, false);
                go.transform.position = at;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = CrystalCluster(owned, rng.Next());
                sr.sortingOrder = Tuning.Ground.Order + 2;
                Lighting.Glow.Attach(go, Tuning.Ground.CrystalLight, new Color(0.45f, 0.95f, 0.88f), 0.85f);
                placed++;
            }
        }

        static Sprite CrystalCluster(SceneryOwned owned, int seed)
        {
            const int w = 22, h = 26;
            var px = new Color32[w * h];
            var rng = new System.Random(seed);
            for (int n = 0; n < 4; n++)
            {
                float bx = 4 + (float)rng.NextDouble() * 14f, lean = ((float)rng.NextDouble() - 0.5f) * 0.5f;
                float hw = 2 + rng.Next(2), len = 10 + rng.Next(14);
                for (int y = 0; y < len && y < h; y++)
                {
                    float half = hw * (1f - y / len) + 0.5f;
                    float cx = bx + lean * y;
                    for (int x = Mathf.RoundToInt(cx - half); x <= cx + half; x++)
                        if (x >= 0 && x < w)
                            px[y * w + x] = x < cx ? (Color32)CaveArt.Crystal : (Color32)CaveArt.CrystalDeep;
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.1f), Ppu, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            if (owned != null) { owned.Add(tex); owned.Add(s); }
            return s;
        }

        /// <summary>
        /// The edge map for the light field: 0 on the ground well in from the lip, rising through
        /// the last few texels of floor and out over the void to 1 a couple of units past it
        /// (Tuning.Ground.EdgeInside/EdgeOutside), sampled every few texels.
        /// </summary>
        static void LayEdge(bool[] ground, float[] dist, int W, int H, Vector2 origin)
        {
            var notGround = new bool[ground.Length];
            for (int k = 0; k < ground.Length; k++) notGround[k] = !ground[k];
            var inward = Chamfer(notGround, W, H);
            const int step = 4;
            int mw = W / step, mh = H / step;
            var map = new float[mw * mh];
            float a = -Tuning.Ground.EdgeInside, b = Tuning.Ground.EdgeOutside;
            for (int y = 0; y < mh; y++)
            for (int x = 0; x < mw; x++)
            {
                int k = (y * step + step / 2) * W + x * step + step / 2;
                float sd = ground[k] ? -inward[k] : dist[k];
                float t = Mathf.Clamp01((sd - a) / (b - a));
                map[y * mw + x] = t * t * (3f - 2f * t);
            }
            Lighting.LightField.SetEdge(map, mw, mh, origin, step / Ppu, Biome.EdgeDark, Biome.EdgeShade);
        }

        /// <summary>What a drop's cliff leans toward under the place's own light: the sky's blue
        /// haze, the tower's shaft dark.</summary>
        static Color CliffTint => Biome.Current switch
        {
            BiomeKind.SkyRuin => new Color(0.55f, 0.62f, 0.78f),
            BiomeKind.SnowyPeaks => new Color(0.42f, 0.48f, 0.62f),
            BiomeKind.Caves => new Color(0.08f, 0.10f, 0.14f),
            _ => new Color(0.10f, 0.09f, 0.12f),
        };

        // ------------------------------------------------------------------ helpers

        static float[] Blur(float[] src, int w, int h, int r)
        {
            var tmp = new float[w * h];
            var outp = new float[w * h];
            float n = 2 * r + 1;
            for (int y = 0; y < h; y++)
            {
                int o = y * w;
                float s = 0f;
                for (int x = -r; x <= r; x++) s += src[o + Mathf.Clamp(x, 0, w - 1)];
                for (int x = 0; x < w; x++)
                {
                    tmp[o + x] = s / n;
                    s += src[o + Mathf.Min(x + r + 1, w - 1)] - src[o + Mathf.Max(x - r, 0)];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float s = 0f;
                for (int y = -r; y <= r; y++) s += tmp[Mathf.Clamp(y, 0, h - 1) * w + x];
                for (int y = 0; y < h; y++)
                {
                    outp[y * w + x] = s / n;
                    s += tmp[Mathf.Min(y + r + 1, h - 1) * w + x] - tmp[Mathf.Max(y - r, 0) * w + x];
                }
            }
            return outp;
        }

        /// <summary>Distance in texels from the nearest ground texel (0 on ground) - two-pass
        /// chamfer, 1 straight and 1.41 diagonal.</summary>
        static float[] Chamfer(bool[] ground, int w, int h)
        {
            const float D = 1.41421f, Big = 1e6f;
            var d = new float[w * h];
            for (int k = 0; k < d.Length; k++) d[k] = ground[k] ? 0f : Big;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int k = y * w + x;
                float v = d[k];
                if (x > 0) v = Mathf.Min(v, d[k - 1] + 1f);
                if (y > 0)
                {
                    v = Mathf.Min(v, d[k - w] + 1f);
                    if (x > 0) v = Mathf.Min(v, d[k - w - 1] + D);
                    if (x < w - 1) v = Mathf.Min(v, d[k - w + 1] + D);
                }
                d[k] = v;
            }
            for (int y = h - 1; y >= 0; y--)
            for (int x = w - 1; x >= 0; x--)
            {
                int k = y * w + x;
                float v = d[k];
                if (x < w - 1) v = Mathf.Min(v, d[k + 1] + 1f);
                if (y < h - 1)
                {
                    v = Mathf.Min(v, d[k + w] + 1f);
                    if (x < w - 1) v = Mathf.Min(v, d[k + w + 1] + D);
                    if (x > 0) v = Mathf.Min(v, d[k + w - 1] + D);
                }
                d[k] = v;
            }
            return d;
        }

        static int Mod(int a, int n) => ((a % n) + n) % n;

        /// <summary>Smooth value noise, one lattice step per unit of (x, y).</summary>
        static float Noise(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi, s), b = Hash(xi + 1, yi, s), c = Hash(xi, yi + 1, s), d = Hash(xi + 1, yi + 1, s);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static float Hash(int a, int b, int s)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777216f;
            }
        }

        static Color Shade(Color c, float v) => new(Mathf.Clamp01(c.r + v), Mathf.Clamp01(c.g + v), Mathf.Clamp01(c.b + v), c.a);
    }
}
