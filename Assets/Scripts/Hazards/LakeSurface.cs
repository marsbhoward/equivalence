using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A chasm drawn as a LAKE (RoomShape.Lake): one connected body of '~' cells, painted as a
    /// single texture so it can know what a lone cell can't - where its shore is. By every rule it
    /// is the drop's void (Arena's chasm mask is the authority; nothing here is read by physics).
    ///
    /// What makes it read as deep water rather than as the water PIT's shallow basin, or as the
    /// drop's dark hole:
    ///   - the SHORE is rounded at every convex corner (a square-cornered pool is a tank);
    ///   - the far bank (ground to the NORTH) shows a short earth face down to the waterline - a
    ///     few texels, not the drop's long rock face, because the water stands high in it;
    ///   - foam breaks along the waterline, and a shadow lies on the water under the far bank;
    ///   - DEPTH runs out from the shore: shallows near the edge, darkening toward the middle in
    ///     dithered steps (pixel-art bands, not a smooth airbrush), darker at its heart than the
    ///     pit ever gets;
    ///   - glints shimmer (two hashed fields cross-fading) and rings spread on open water.
    ///
    /// Point-filtered at 75 texels a cell, the drop's own density. A falling body splashes
    /// (<see cref="Splash"/>, from EnemyController's fall and the player's).
    /// </summary>
    public class LakeSurface : MonoBehaviour
    {
        const int N = 75;
        /// <summary>Rounding of a convex shore corner, in texels.</summary>
        const float CornerRadius = 24f;
        const float LipTexels = 2f, BankTexels = 11f, FoamTexels = 2.5f;
        /// <summary>How far the shoreline wanders INTO the void, in texels - inside the 0.12 the
        /// player's walk already stops short of the edge (Tuning.Chasm.EdgeMargin is 9 texels),
        /// so the drawn ground never promises footing the mask refuses.</summary>
        const float ShoreWander = 7f;
        /// <summary>The depth field: the land mask sampled this many times a cell and blurred
        /// (three box passes of this radius, in samples). Depth taken as a distance to the nearest
        /// edge creased along every diagonal and stepped to "deep" a cell in - an upturned
        /// pyramid, a pool; blurred land gives round contours that keep deepening wherever the
        /// water is wide, and stays shallow down a narrow channel.</summary>
        const int DepthSamples = 8, DepthBlur = 6, DepthMargin = 3;

        /// <summary>How far the room's outer wall reaches in over the arena's edge (it is 0.6
        /// thick, centred on it - GameBootstrap.BuildArena). It sorts under the pit band, so water
        /// drawn there would lie over the wall's own rim.</summary>
        const float OuterWallInset = 0.3f;

        // The edge is WET stone, darker than the floor - a pale lip round straight sides read as
        // a swimming pool's coping.
        // The FOREST's lake (Art.Biomes) is a dark, still pool: a muddy reed bank, green-black water.
        static bool Forest => Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest);
        // The SALT's is a brine sinkhole: a crystal-white rim, the shallows mirroring a pale sky, turquoise deep.
        static bool Salt => Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SaltFlats);
        static Color Lip => Salt ? new(0.92f, 0.90f, 0.88f) : Forest ? new(0.13f, 0.11f, 0.07f) : new(0.24f, 0.20f, 0.16f);
        static Color BankTop => Salt ? new(0.82f, 0.74f, 0.80f) : Forest ? new(0.17f, 0.21f, 0.10f) : new(0.30f, 0.25f, 0.20f);
        static Color BankLow => Salt ? new(0.46f, 0.40f, 0.52f) : Forest ? new(0.05f, 0.08f, 0.05f) : new(0.11f, 0.10f, 0.10f);
        static Color Foam => Salt ? new(1f, 1f, 1f) : Forest ? new(0.58f, 0.70f, 0.56f) : new(0.70f, 0.84f, 0.88f);
        static Color Shallow => Salt ? new(0.64f, 0.70f, 0.84f) : Forest ? new(0.12f, 0.27f, 0.21f) : new(0.17f, 0.40f, 0.47f);
        static Color Mid => Salt ? new(0.16f, 0.50f, 0.54f) : Forest ? new(0.05f, 0.16f, 0.14f) : new(0.08f, 0.23f, 0.34f);
        static Color Deep => Salt ? new(0.06f, 0.26f, 0.32f) : Forest ? new(0.02f, 0.065f, 0.06f) : new(0.03f, 0.09f, 0.17f);
        static Color RippleColor => Salt ? new(0.96f, 0.96f, 1f) : Forest ? new(0.58f, 0.76f, 0.64f) : new(0.72f, 0.88f, 0.95f);

        // Serialized and not readonly - a domain reload would otherwise leave rings frozen
        // mid-spread (WaterPit's note).
        [SerializeField] SpriteRenderer _body, _glintA, _glintB;
        [SerializeField] List<Vector2> _openWater = new();
        [SerializeField] List<float> _openRoom = new();
        [SerializeField] List<Ripple> _ripples = new();
        [SerializeField] Rect _bounds;
        [SerializeField] float _clock, _nextRipple;
        [SerializeField] int _nextSlot;

        [System.Serializable]
        struct Ripple
        {
            public Transform T;
            public SpriteRenderer Sr;
            public float Age, Size, Alpha;
            public bool Live;
        }

        const int RipplePool = 12;

        // ------------------------------------------------------------------ building

        /// <summary>One LakeSurface per 4-connected body of water.</summary>
        /// <param name="water">Is cell (x, y) a chasm cell of this room.</param>
        /// <param name="voidAt">Water for the shore test - also true OUTSIDE the room, so water
        /// meeting the outer wall has no bank drawn against it (the drop's own rule).</param>
        internal static void BuildAll(Transform parent, int cols, int rows, Vector2 cell,
                                      System.Func<int, int, Vector2> corner,
                                      System.Func<int, int, bool> water, System.Func<int, int, bool> voidAt)
        {
            var taken = new bool[cols * rows];
            int index = 0;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < cols; x++)
            {
                if (!water(x, y) || taken[y * cols + x]) continue;
                var cells = new List<Vector2Int>();
                var stack = new Stack<Vector2Int>();
                stack.Push(new Vector2Int(x, y));
                taken[y * cols + x] = true;
                while (stack.Count > 0)
                {
                    var p = stack.Pop();
                    cells.Add(p);
                    foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        var n = p + d;
                        if (n.x < 0 || n.y < 0 || n.x >= cols || n.y >= rows) continue;
                        if (!water(n.x, n.y) || taken[n.y * cols + n.x]) continue;
                        taken[n.y * cols + n.x] = true;
                        stack.Push(n);
                    }
                }
                var go = new GameObject($"lake.{index++}");
                go.transform.SetParent(parent, false);
                go.AddComponent<LakeSurface>().Build(cells, cell, corner, voidAt);
            }
        }

        void Build(List<Vector2Int> cells, Vector2 cell, System.Func<int, int, Vector2> corner,
                   System.Func<int, int, bool> voidAt)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var c in cells)
            {
                x0 = Mathf.Min(x0, c.x); y0 = Mathf.Min(y0, c.y);
                x1 = Mathf.Max(x1, c.x); y1 = Mathf.Max(y1, c.y);
            }
            int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
            var member = new bool[bw, bh];
            foreach (var c in cells) member[c.x - x0, c.y - y0] = true;

            int W = bw * N, H = bh * N;
            var body = new Color32[W * H];
            var glA = new Color32[W * H];
            var glB = new Color32[W * H];
            int seed = x0 * 7919 + y0 * 104729 + 13;
            var depth = DepthField(x0, y0, bw, bh, voidAt, out int gw);

            var origin = corner(x0, y0);
            var half = Arena.HalfExtents;
            float wallX = half.x - OuterWallInset, wallY = half.y - OuterWallInset;

            for (int ty = 0; ty < H; ty++)
            for (int tx = 0; tx < W; tx++)
            {
                int i = ty * W + tx;
                int lx = tx / N, ly = ty / N;
                if (!member[lx, ly]) continue;
                float worldX = origin.x + (tx + 0.5f) / N * cell.x, worldY = origin.y + (ty + 0.5f) / N * cell.y;
                if (Mathf.Abs(worldX) > wallX || Mathf.Abs(worldY) > wallY) continue;
                int cx = x0 + lx, cy = y0 + ly;
                float u = tx % N + 0.5f, v = ty % N + 0.5f;
                float d = Shore(cx, cy, u, v, voidAt, out float facing);
                // World texel coordinates, so noise and glints run on across cell seams.
                int wx = cx * N + tx % N, wy = cy * N + ty % N;
                // An irregular shoreline: never a ruled edge, only ever eating INTO the water.
                float wander = PitArt.ValueNoise(wx / (float)(N * 64), wy / (float)(N * 64), 64 * 2, seed + 7) * ShoreWander;
                d -= wander;
                if (d < 0f) continue;   // the rounded corner and the wandering shore: floor shows
                Color c;
                float bank = BankTexels * facing;
                if (d < LipTexels) c = Lip;
                else if (d < LipTexels + bank)
                {
                    float k = (d - LipTexels) / bank;
                    c = Color.Lerp(BankTop, BankLow, k);
                    if ((Mathf.FloorToInt(d) + (wx / 11) % 2) % 4 == 0) c = Color.Lerp(c, BankLow, 0.45f);
                }
                else
                {
                    float f = d - LipTexels - bank;
                    if (f < FoamTexels && PitArt.ValueNoise(wx / (float)(N * 64), wy / (float)(N * 64), 64 * 6, seed + 3) > 0.45f) c = Foam;
                    else
                    {
                        // Depth: how far from the shore, wobbled by a slow mottle, then stepped
                        // into bands with an ordered dither between them.
                        float t = SampleDepth(depth, gw, tx, ty);
                        t += (PitArt.ValueNoise(wx / (float)(N * 32), wy / (float)(N * 32), 20, seed) - 0.5f) * 0.3f;
                        t = Mathf.Clamp01(t);
                        const int Bands = 5;
                        float stepped = Mathf.Floor(t * Bands + Bayer(wx, wy)) / Bands;
                        stepped = Mathf.Clamp01(stepped);
                        c = stepped < 0.5f ? Color.Lerp(Shallow, Mid, stepped * 2f)
                                           : Color.Lerp(Mid, Deep, (stepped - 0.5f) * 2f);
                        // The far bank's shadow lies on the water just under it.
                        if (facing > 0f && f < 9f) c = Color.Lerp(c, Deep, 0.45f * facing * (1f - f / 9f));

                        // Glints only on open water - a glint in the foam reads as foam.
                        if (t > 0.18f)
                        {
                            if (Dash(wx, wy, seed + 101)) glA[i] = new Color32(255, 255, 255, 255);
                            if (Dash(wx, wy, seed + 202)) glB[i] = new Color32(255, 255, 255, 255);
                        }
                    }
                }
                body[i] = c;
            }

            transform.position = new Vector3(origin.x, origin.y, 0f);
            _body = Layer("water", body, W, H, cell, SortingOrders.PitBase);
            _glintA = Layer("glints.a", glA, W, H, cell, SortingOrders.PitBase + 1);
            _glintB = Layer("glints.b", glB, W, H, cell, SortingOrders.PitBase + 1);
            _bounds = new Rect(origin, new Vector2(bw * cell.x, bh * cell.y));

            // Open water for the surface's own rings: every cell centre, each with the room a
            // ring may grow to there before it would spill over the shore.
            foreach (var c in cells)
            {
                float d = Shore(c.x, c.y, N * 0.5f, N * 0.5f, voidAt, out float facing) - ShoreWander;
                float room = (d - LipTexels - BankTexels * facing) / N * 2f * Mathf.Min(cell.x, cell.y);
                var centre = corner(c.x, c.y) + cell * 0.5f;
                room = Mathf.Min(room, 2f * (wallX - Mathf.Abs(centre.x)), 2f * (wallY - Mathf.Abs(centre.y)));
                if (room < 0.3f) continue;
                _openWater.Add(centre);
                _openRoom.Add(room);
            }

            for (int i = 0; i < RipplePool; i++)
            {
                var go = new GameObject("ripple");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Spr.Ring;
                sr.color = Color.clear;
                sr.sortingOrder = SortingOrders.PitBase + 2;
                _ripples.Add(new Ripple { T = go.transform, Sr = sr });
            }
            _clock = Random.value * 10f;
            _nextRipple = Random.Range(0f, Tuning.Chasm.LakeRippleIntervalMax);
        }

        /// <summary>
        /// Texels from (u, v) in cell (cx, cy) to the nearest standing ground (negative = the
        /// rounded corner, which is ground), and how much that shore FACES the camera's far side
        /// (1 = ground due north, so its bank face shows; 0 = beside or below).
        /// </summary>
        static float Shore(int cx, int cy, float u, float v, System.Func<int, int, bool> voidAt, out float facing)
        {
            float best = float.MaxValue;
            facing = 0f;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if ((dx == 0 && dy == 0) || voidAt(cx + dx, cy + dy)) continue;
                // Nearest point of that neighbour's rectangle.
                float qx = dx > 0 ? N : dx < 0 ? 0f : u;
                float qy = dy > 0 ? N : dy < 0 ? 0f : v;
                float ex = u - qx, ey = v - qy;
                float d = Mathf.Sqrt(ex * ex + ey * ey);
                if (d < best) { best = d; facing = d > 0f ? Mathf.Max(0f, -ey / d) : (dy > 0 ? 1f : 0f); }
            }

            // Convex corners - ground on both sides of a corner - are rounded off.
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                if (voidAt(cx + sx, cy) || voidAt(cx, cy + sy)) continue;
                float ccx = sx < 0 ? CornerRadius : N - CornerRadius;
                float ccy = sy < 0 ? CornerRadius : N - CornerRadius;
                bool inSquare = (sx < 0 ? u < ccx : u > ccx) && (sy < 0 ? v < ccy : v > ccy);
                if (!inSquare) continue;
                float ex = ccx - u, ey = ccy - v;
                float dist = Mathf.Sqrt(ex * ex + ey * ey);
                float d = CornerRadius - dist;
                if (d < best)
                {
                    best = d;
                    // From the shore toward the water is toward the circle's centre.
                    facing = dist > 0f ? Mathf.Max(0f, -ey / dist) : 0f;
                }
            }
            return best;
        }

        /// <summary>The blurred land mask over the lake's bounds plus a margin, row-major,
        /// <paramref name="gw"/> wide: 1 on land, 0 on open water, between near a shore.</summary>
        static float[] DepthField(int x0, int y0, int bw, int bh, System.Func<int, int, bool> voidAt, out int gw)
        {
            const int S = DepthSamples, M = DepthMargin;
            gw = (bw + 2 * M) * S;
            int gh = (bh + 2 * M) * S;
            var g = new float[gw * gh];
            for (int y = 0; y < gh; y++)
            for (int x = 0; x < gw; x++)
                g[y * gw + x] = voidAt(x0 - M + x / S, y0 - M + y / S) ? 0f : 1f;

            var tmp = new float[g.Length];
            for (int pass = 0; pass < 3; pass++)
            {
                BoxBlur(g, tmp, gw, gh, 1, gw);   // down each column
                BoxBlur(tmp, g, gh, gw, gw, 1);   // along each row
            }
            return g;
        }

        /// <summary>One box-blur pass of radius DepthBlur, edges clamped. <paramref name="step"/>
        /// walks along a line, <paramref name="lineStep"/> between lines.</summary>
        static void BoxBlur(float[] src, float[] dst, int lines, int length, int lineStep, int step)
        {
            // (lines, length, lineStep, step) named for the row pass; the column pass swaps them.
            const int R = DepthBlur;
            for (int l = 0; l < lines; l++)
            {
                int b = l * lineStep;
                float sum = 0f;
                for (int k = -R; k <= R; k++) sum += src[b + Mathf.Clamp(k, 0, length - 1) * step];
                for (int i = 0; i < length; i++)
                {
                    dst[b + i * step] = sum / (2 * R + 1);
                    sum += src[b + Mathf.Min(i + R + 1, length - 1) * step]
                         - src[b + Mathf.Max(i - R, 0) * step];
                }
            }
        }

        /// <summary>Depth 0 (at the shore) to 1 (no land anywhere near) at texel (tx, ty) of the
        /// lake's texture, read bilinearly off the blurred land.</summary>
        static float SampleDepth(float[] g, int gw, int tx, int ty)
        {
            float k = DepthSamples / (float)N;
            float fx = (tx + 0.5f) * k + DepthMargin * DepthSamples - 0.5f;
            float fy = (ty + 0.5f) * k + DepthMargin * DepthSamples - 0.5f;
            int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
            float ax = fx - ix, ay = fy - iy;
            float a = g[iy * gw + ix], b = g[iy * gw + ix + 1];
            float c = g[(iy + 1) * gw + ix], d = g[(iy + 1) * gw + ix + 1];
            float land = Mathf.Lerp(Mathf.Lerp(a, b, ax), Mathf.Lerp(c, d, ax), ay);
            // Half land is the shore itself; no land within the blur is as deep as it gets.
            return Mathf.Clamp01((0.5f - land) / 0.45f);
        }

        /// <summary>A 4x4 ordered-dither threshold in [0, 1).</summary>
        static float Bayer(int x, int y)
        {
            int m = ((x & 3) * 4 + (y & 3));
            int[] b = BayerTable;
            return (b[m] + 0.5f) / 16f;
        }
        static readonly int[] BayerTable = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        /// <summary>A glint dash: lit 2-3 texels right of a texel that hashes high (PitArt.Glints'
        /// shape - a lone texel reads as grain, a dash as light lying on a surface).</summary>
        static bool Dash(int x, int y, int seed)
        {
            for (int k = 0; k < 4; k++)
            {
                float h = PitArt.Hash(x - k, y, seed);
                if (h > 0.9992f && (k < 3 || h > 0.9996f)) return true;
            }
            return false;
        }

        SpriteRenderer Layer(string name, Color32[] px, int w, int h, Vector2 cell, int order)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, w, h), Vector2.zero, N, 0, SpriteMeshType.FullRect);
            s.hideFlags = HideFlags.HideAndDontSave;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(cell.x, cell.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s;
            sr.sortingOrder = order;
            return sr;
        }

        void OnDestroy()
        {
            foreach (var sr in new[] { _body, _glintA, _glintB })
            {
                if (sr == null || sr.sprite == null) continue;
                var tex = sr.sprite.texture;
                Destroy(sr.sprite);
                if (tex != null) Destroy(tex);
            }
        }

        // ------------------------------------------------------------------ the surface

        void Update()
        {
            float dt = Time.deltaTime;
            _clock += dt;

            // Two glint fields cross-fading: light moving on the water rather than one sheet of
            // dots pulsing.
            float a = 0.5f + 0.5f * Mathf.Sin(_clock * 1.7f);
            if (_glintA != null) _glintA.color = new Color(1f, 1f, 1f, 0.55f * a);
            if (_glintB != null) _glintB.color = new Color(1f, 1f, 1f, 0.55f * (1f - a));

            _nextRipple -= dt;
            if (_nextRipple <= 0f && _openWater.Count > 0)
            {
                _nextRipple = Random.Range(Tuning.Chasm.LakeRippleIntervalMin, Tuning.Chasm.LakeRippleIntervalMax);
                int k = Random.Range(0, _openWater.Count);
                float room = _openRoom[k];
                float size = Mathf.Min(Tuning.Chasm.LakeRippleSize, room);
                // Anywhere inside the cell the ring still fits.
                float slack = Mathf.Max(0f, (room - size) * 0.5f);
                var p = _openWater[k] + Random.insideUnitCircle * Mathf.Min(slack, 0.4f);
                Emit(p, size, 0.5f, 0f);
            }

            float life = Tuning.Chasm.LakeRippleSeconds;
            for (int i = 0; i < _ripples.Count; i++)
            {
                var r = _ripples[i];
                if (!r.Live || r.T == null) continue;
                r.Age += dt;
                if (r.Age < 0f) { _ripples[i] = r; continue; }   // a splash's later ring, not yet
                if (r.Age >= life)
                {
                    r.Live = false;
                    if (r.Sr != null) r.Sr.color = Color.clear;
                    _ripples[i] = r;
                    continue;
                }
                float k = r.Age / life;
                float grow = 1f - (1f - k) * (1f - k);
                r.T.localScale = Vector3.one * Mathf.Lerp(0.12f, r.Size, grow);
                var col = RippleColor;
                col.a = r.Alpha * (1f - k);
                if (r.Sr != null) r.Sr.color = col;
                _ripples[i] = r;
            }
        }

        void Emit(Vector2 world, float size, float alpha, float delay)
        {
            if (_ripples == null || _ripples.Count == 0 || size < 0.1f) return;
            int i = _nextSlot % _ripples.Count;
            _nextSlot = (i + 1) % _ripples.Count;
            var r = _ripples[i];
            if (r.T == null) return;
            r.T.position = new Vector3(world.x, world.y, 0f);
            r.T.localScale = Vector3.zero;
            if (r.Sr != null) r.Sr.color = Color.clear;
            r.Age = -delay;
            r.Size = size;
            r.Alpha = alpha;
            r.Live = true;
            _ripples[i] = r;
        }

        /// <summary>
        /// Something went into the water at <paramref name="p"/>: a white burst and rings
        /// spreading from it, each kept as big as the water around it allows (asked of Arena, the
        /// authority) so none spills onto the floor - a body goes in near the lip.
        /// </summary>
        public static void Splash(Vector2 p)
        {
            Spr.Flash(p, 0.75f, Foam, 0.22f);
            var lake = At(p);
            if (lake == null) return;
            float room = Room(p, 1.2f);
            lake.Emit(p, room * 0.55f, 0.9f, 0f);
            lake.Emit(p, room * 0.8f, 0.75f, 0.12f);
            lake.Emit(p, room, 0.6f, 0.26f);
        }

        /// <summary>One small ring at <paramref name="p"/> - the waterline round a body going
        /// under.</summary>
        public static void Ring(Vector2 p, float size, float alpha = 0.7f)
        {
            var lake = At(p);
            if (lake != null) lake.Emit(p, Mathf.Min(size, Room(p, size)), alpha, 0f);
        }

        /// <summary>The last of a body: a few small rings breaking the surface over it, one after
        /// another, a little apart - air coming up after it has gone under.</summary>
        public static void Bubbles(Vector2 p)
        {
            var lake = At(p);
            if (lake == null) return;
            for (int i = 0; i < 4; i++)
            {
                var q = p + new Vector2(Random.Range(-0.14f, 0.14f), Random.Range(-0.06f, 0.1f));
                lake.Emit(q, Mathf.Min(Random.Range(0.14f, 0.24f), Room(q, 0.24f)), 0.85f,
                          0.08f + i * Random.Range(0.14f, 0.22f));
            }
        }

        static LakeSurface At(Vector2 p)
        {
            foreach (var lake in FindObjectsByType<LakeSurface>())
                if (lake._bounds.Contains(p)) return lake;
            return null;
        }

        /// <summary>The widest ring, up to <paramref name="max"/>, that stays on the water.</summary>
        static float Room(Vector2 p, float max)
        {
            for (float r = max; r > 0.12f; r *= 0.75f)
                if (Arena.DeepInChasm(p, r * 0.5f)) return r;
            return 0.12f;
        }
    }
}
