using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.Nigredo;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// One floor's Nigredo (see <see cref="Nigredo"/>): the tears in the north boundary, the Stone's
    /// light, the crystals standing in the room and both layers of cinders. Built once per floor,
    /// animated here; everything it owns is a child, destroyed with it. Texel-drawn at the floor's
    /// density. Never <c>UnityEngine.Random</c> - all of it is hashed from the floor's seed.
    /// </summary>
    public class NigredoRoom : MonoBehaviour
    {
        const float Ppu = FloorArt.PixelsPerUnit;

        // Rift portal colours (Rifts/Rift.cs), so the tears read as the same stuff - not the same door.
        static readonly Color Violet = new(0.62f, 0.42f, 0.95f), Cyan = new(0.45f, 0.88f, 1f);

        [SerializeField] float _strength;
        [SerializeField] SpriteRenderer[] _rims, _views, _glows, _shards;
        [SerializeField] Transform[] _tears;
        [SerializeField] Vector2[] _shardHome;
        [SerializeField] Lighting.Glow[] _crystalLights;
        [SerializeField] float[] _crystalBase;
        [SerializeField] Transform[] _back, _front;
        [SerializeField] SpriteRenderer[] _backSr, _frontSr;
        [SerializeField] Vector4[] _backMove, _frontMove;   // vx, vy, phase, kind
        [SerializeField] Vector2[] _frontBase;
        [SerializeField] Vector2 _min, _max;
        [SerializeField] float _w, _h;
        float _t;

        public void Build(Vector2 half, int seed, float s)
        {
            _strength = s;
            var rng = new System.Random(seed * 31 + 7);
            float w = half.x - Tuning.Scenery.WallInset, h = half.y - Tuning.Scenery.WallInset;
            float faceTop = h + Tuning.Scenery.FaceHeight, capTop = faceTop + Tuning.Scenery.CapHeight;
            _min = new Vector2(-w - 1f, -h - 1f);
            _max = new Vector2(w + 1f, capTop);

            _w = w; _h = h;
            BuildTears(rng, w, h, capTop, s);
            BuildCinders(rng, s);
        }

        // ------------------------------------------------------------------ the tears

        void BuildTears(System.Random rng, float w, float h, float capTop, float s)
        {
            int n = Mathf.RoundToInt(Mathf.Lerp(T.TearsMin, T.TearsMax, s));
            _tears = new Transform[n]; _rims = new SpriteRenderer[n]; _views = new SpriteRenderer[n];
            var shards = new System.Collections.Generic.List<SpriteRenderer>();
            var homes = new System.Collections.Generic.List<Vector2>();

            // The largest stands off the door (north wall's middle); the rest are spread along the wall.
            float bigX = (rng.NextDouble() < 0.5 ? -1f : 1f) * Mathf.Lerp(3f, w * 0.55f, (float)rng.NextDouble());
            for (int i = 0; i < n; i++)
            {
                bool big = i == 0;
                float x = big ? bigX : Mathf.Lerp(-w + 1f, w - 1f, (i - 0.5f + (float)rng.NextDouble() * 0.6f) / Mathf.Max(1, n - 1));
                if (!big && Mathf.Abs(x) < 2.4f) x = Mathf.Sign(x == 0 ? 1 : x) * 2.4f;          // the door's clear
                if (!big && Mathf.Abs(x - bigX) < 2f) x += x < bigX ? -2f : 2f;
                x = Mathf.Clamp(x, -w + 0.8f, w - 0.8f);
                float halfW = big ? Mathf.Lerp(3f, 9f, s) : 2f + (float)rng.NextDouble() * 2f;        // texels
                float bottom = h + (big ? 0.05f : 0.3f + (float)rng.NextDouble() * 0.6f);
                float top = big ? capTop - 0.02f : Mathf.Min(capTop - 0.1f, bottom + 1.2f + (float)rng.NextDouble() * 1.4f);

                var root = new GameObject(big ? "tear.stone" : "tear");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = new Vector3(Mathf.Round(x * Ppu) / Ppu, bottom, 0f);
                _tears[i] = root.transform;

                int H = Mathf.Max(8, Mathf.RoundToInt((top - bottom) * Ppu));
                int Wd = Mathf.CeilToInt(halfW) * 2 + 10;
                var (core, rim, view) = TearSprites(Wd, H, halfW, big, rng.Next());
                Quad(root.transform, "core", core, -6);
                _rims[i] = Quad(root.transform, "rim", rim, -6);
                _rims[i].sortingOrder = -6;
                _views[i] = big ? Quad(root.transform, "stone", view, -6) : null;

                float hh = (top - bottom);
                // No halo (group 7, the user's pick "flakes off the seam"): the seam's own light pools
                // in the dark, and flakes peel off its sides below.

                // Its own cold light, wide-cored so the whole seam is lit through the dark.
                var g = Lighting.Glow.Attach(root, Mathf.Max(T.TearLightRadius, hh * 0.6f + 0.6f), T.TearLight, T.TearLightIntensity);
                g.Core = 0.55f;
                g.Wash = T.TearWash;
                g.Offset = new Vector2(0f, hh * 0.5f);
                if (big)
                {
                    var stone = new GameObject("stone.light");
                    stone.transform.SetParent(root.transform, false);
                    stone.transform.localPosition = new Vector3(0f, hh * 0.45f, 0f);
                    Lighting.Glow.Attach(stone, Mathf.Lerp(T.StoneRadiusMin, T.StoneRadiusMax, s), T.StoneColor,
                                         Mathf.Lerp(T.StoneIntensityMin, T.StoneIntensityMax, s)).Wash = T.StoneWash;
                }

                // Flakes of the same light peeling off its sides and drifting out a little way
                // before they go out - the Rift rim's flakes.
                for (int k = 0; k < 4 + Mathf.RoundToInt(halfW); k++)
                {
                    var sh = Quad(root.transform, "flake", ShardSprite, -6);
                    sh.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
                    shards.Add(sh);
                    homes.Add(new Vector2((halfW * 0.6f + 1f) / Ppu, hh));
                }
            }
            _shards = shards.ToArray();
            _shardHome = homes.ToArray();
        }

        /// <summary>A seam: void inside, a pale core, the lit rim (white, tinted each frame) and,
        /// for the largest, the Stone's red in its middle - the view a Rift shows of the hub.</summary>
        static (Sprite core, Sprite rim, Sprite view) TearSprites(int W, int H, float halfW, bool big, int seed)
        {
            var core = new Color[W * H]; var rim = new Color[W * H]; var view = new Color[W * H];
            float cx0 = W * 0.5f;
            for (int y = 0; y < H; y++)
            {
                float u = (y + 0.5f) / H;
                float hw = halfW * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * u)), 0.6f) + (Hash(y >> 1, seed) - 0.5f) * 1.6f;
                float cx = cx0 + Mathf.Sin(y * 0.21f + seed % 7) * 2.2f;
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Abs(x + 0.5f - cx);
                    if (d > hw) continue;
                    float k = d / Mathf.Max(0.5f, hw);
                    int i = y * W + x;
                    if (k > 0.78f) rim[i] = new Color(1f, 1f, 1f, 0.9f);
                    else
                    {
                        core[i] = Color.Lerp(new Color(0.086f, 0.1f, 0.19f), new Color(0.59f, 0.67f, 0.84f), Mathf.Max(0f, 0.75f - k) * 0.5f);
                        if (big && k < 0.5f) view[i] = Color.Lerp(new Color(0.47f, 0.05f, 0.08f), new Color(1f, 0.43f, 0.39f), 1f - k * 2f);
                    }
                }
            }
            return (Make(core, W, H), Make(rim, W, H), big ? Make(view, W, H) : null);
        }

        // ------------------------------------------------------------------ crystals in the room

        /// <summary>Stand the crystals - once the room's walls exist (BuildFight, beside the set
        /// dressing), so none stands inside one.</summary>
        public void PlaceCrystals(int seed)
        {
            if (_crystalLights != null && _crystalLights.Length > 0) return;
            BuildCrystals(new System.Random(seed * 17 + 3), _w, _h, _strength);
        }

        void BuildCrystals(System.Random rng, float w, float h, float s)
        {
            int n = Mathf.RoundToInt(Mathf.Lerp(T.CrystalsMin, T.CrystalsMax, s));
            var spots = new[]
            {
                new Vector2(-w * 0.62f, h - 0.4f), new Vector2(w * 0.66f, h - 0.45f), new Vector2(-w + 0.7f, -h + 0.8f),
                new Vector2(w - 0.7f, -h + 0.8f), new Vector2(w * 0.3f, h - 0.4f), new Vector2(-w + 0.6f, 0.2f), new Vector2(w - 0.6f, -0.4f),
            };
            var lights = new System.Collections.Generic.List<Lighting.Glow>();
            var bases = new System.Collections.Generic.List<float>();
            int placed = 0;
            foreach (var p0 in spots)
            {
                if (placed >= n) break;
                var p = p0 + new Vector2(((float)rng.NextDouble() - 0.5f) * 0.6f, 0f);
                if (!Arena.OnFloor(p, 0.35f)) continue;
                int size = 3 + Mathf.Min(2, Mathf.RoundToInt(s * 2f + (float)rng.NextDouble()));
                var go = new GameObject("crystal");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Cluster(size, rng.Next());
                DepthSorted.Attach(go, 0f, true, sr);
                var g = Lighting.Glow.Attach(go, T.CrystalRadius + size * 0.1f, T.CrystalColor, T.CrystalIntensity);
                g.Offset = new Vector2(0f, 0.25f);
                g.Wash = T.CrystalWash;
                lights.Add(g);
                bases.Add(T.CrystalIntensity);
                placed++;
            }
            _crystalLights = lights.ToArray();
            _crystalBase = bases.ToArray();
        }

        static readonly Color[] Cr = { new(0.16f, 0.024f, 0.047f), new(0.38f, 0.047f, 0.094f), new(0.62f, 0.094f, 0.157f), new(0.84f, 0.2f, 0.235f), new(1f, 0.59f, 0.55f) };
        static readonly Color Ink = new(0.055f, 0.047f, 0.063f);

        /// <summary>A cluster of hexagonal prisms pointing up and out, outlined, a hot core in each.</summary>
        public static Sprite Cluster(int count, int seed)
        {
            const int W = 26, H = 24;
            var px = new Color[W * H];
            void Set(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) px[y * W + x] = c; }
            int[] dx = { 0, -5, 5, -9, 8 };
            float[] lean = { 0.05f, -0.3f, 0.3f, -0.5f, 0.45f };
            for (int k = Mathf.Min(count, 5) - 1; k >= 0; k--)
            {
                int hgt = (k == 0 ? count + 9 : count + 5 - k) - (k > 0 ? (int)(Hash(k, seed) * 2f) : 0);
                Prism(Set, 13 + dx[k], 2 + (k > 0 ? 1 : 0), hgt, lean[k]);
            }
            for (int x = 5; x < 21; x++) if (Hash(x, seed + 9) < 0.6f) Set(x, 1, new Color(0.12f, 0.08f, 0.09f));   // grit at its foot
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(px); tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 1f / H), Ppu, 0, SpriteMeshType.FullRect);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        /// <summary>One crystal, bottom-up from (cx, by): a hexagonal prism, three faces and a hot core.</summary>
        public static void Prism(System.Action<int, int, Color> set, float cx, int by, int h, float lean)
        {
            for (int k = 0; k < h; k++)
            {
                int w = k > h - 3 ? Mathf.Max(0, h - 1 - k) : (h > 4 ? 2 : 1);
                int x0 = Mathf.RoundToInt(cx + lean * k);
                for (int x = -w - 1; x <= w + 1; x++)
                {
                    bool edge = x == -w - 1 || x == w + 1;
                    set(x0 + x, by + k, edge ? Ink : x < 0 ? Cr[1] : x == 0 ? (k > 1 && k < h - 2 ? Cr[4] : Cr[3]) : Cr[2]);
                }
            }
            set(Mathf.RoundToInt(cx + lean * h), by + h, Ink);
        }

        // ------------------------------------------------------------------ cinders

        void BuildCinders(System.Random rng, float s)
        {
            int nb = Mathf.Max(10, Mathf.RoundToInt(T.BackCinders * s)), nf = Mathf.Max(4, Mathf.RoundToInt(T.FrontFlakes * s));
            _back = new Transform[nb]; _backSr = new SpriteRenderer[nb]; _backMove = new Vector4[nb];
            for (int i = 0; i < nb; i++)
            {
                bool ember = rng.NextDouble() < 0.55;
                var sr = Quad(transform, "cinder", Dot, SortingOrders.DepthBase - 1);   // behind everything that stands
                sr.color = ember ? new Color(0.75f, 0.13f, 0.12f, 0.7f) : new Color(0.03f, 0.024f, 0.03f, 0.8f);
                _back[i] = sr.transform; _backSr[i] = sr;
                _back[i].localPosition = RandomIn(rng);
                _backMove[i] = new Vector4(0.06f + (float)rng.NextDouble() * 0.1f, 0.15f + (float)rng.NextDouble() * 0.3f, (float)rng.NextDouble() * 6.28f, ember ? 1f : 0f);
            }
            _front = new Transform[nf]; _frontSr = new SpriteRenderer[nf]; _frontMove = new Vector4[nf]; _frontBase = new Vector2[nf];
            for (int i = 0; i < nf; i++)
            {
                float k = (float)rng.NextDouble();
                int kind = k < 0.4f ? 1 : k < 0.75f ? 2 : 3;   // ember, ash, pale ash
                // Over the bodies and the room's dark (a glow must sit over the dark), under the readouts.
                var sr = Quad(transform, "flake", kind == 1 ? Ember : (rng.NextDouble() < 0.5 ? WispL : WispR), SortingOrders.StatusOverlay - 6);
                sr.color = kind == 1 ? Color.white : kind == 2 ? new Color(0.08f, 0.063f, 0.07f, 0.75f) : new Color(0.59f, 0.55f, 0.55f, 0.55f);
                _front[i] = sr.transform; _frontSr[i] = sr;
                _frontBase[i] = RandomIn(rng);
                _frontMove[i] = new Vector4(0.02f + (float)rng.NextDouble() * 0.05f, -(0.22f + (float)rng.NextDouble() * 0.28f), (float)rng.NextDouble() * 6.28f, kind);
            }
        }

        Vector2 RandomIn(System.Random rng) =>
            new(Mathf.Lerp(_min.x, _max.x, (float)rng.NextDouble()), Mathf.Lerp(_min.y, _max.y, (float)rng.NextDouble()));

        // ------------------------------------------------------------------ animation

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;

            // The rims shimmer white to cyan at the portal's own pace; the tears breathe a little -
            // the boundary's distortion, kept slow (the mock's ripple read as a nervous shimmer).
            float shimmer = 0.5f + 0.5f * Mathf.Sin(_t * 3.3f);
            if (_rims != null)
                for (int i = 0; i < _rims.Length; i++)
                {
                    if (_rims[i] != null) _rims[i].color = Color.Lerp(new Color(1f, 1f, 1f, 0.85f), new Color(Cyan.r, Cyan.g, Cyan.b, 0.85f), shimmer);
                    if (_tears[i] != null) _tears[i].localScale = new Vector3(1f + 0.05f * Mathf.Sin(_t * 0.7f + i * 1.9f), 1f, 1f);
                    if (_views[i] != null) _views[i].color = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(_t * 1.6f));
                }
            if (_shards != null)
                for (int i = 0; i < _shards.Length; i++)
                {
                    if (_shards[i] == null) continue;
                    // home.x: the seam's half width; home.y: its height. Each flake leaves from a
                    // fresh spot on a fresh side every cycle, drifts out 7 texels and rises 3.
                    var home = _shardHome[i];
                    float period = 1.4f + Hash(i, 3) * 0.8f, cyc = _t / period + Hash(i, 4);
                    int n = Mathf.FloorToInt(cyc);
                    float u = cyc - n, side = Hash(i, n) > 0.5f ? 1f : -1f;
                    var at = new Vector2(side * (home.x + u * 7f / Ppu), home.y * (0.15f + 0.7f * Hash(i * 7 + 2, n)) + u * 3f / Ppu);
                    _shards[i].transform.localPosition = Snap(at);
                    var c = u < 0.5f ? Cyan : Violet;
                    bool gone = u > 0.7f && Hash(i, Mathf.FloorToInt(_t * 12f)) > 0.5f;
                    _shards[i].color = new Color(c.r, c.g, c.b, gone ? 0f : 0.9f - u * 0.5f);
                }

            // The crystals: a slow, steady pulse - never a flash, so never a tell.
            if (_crystalLights != null)
                for (int i = 0; i < _crystalLights.Length; i++)
                    if (_crystalLights[i] != null)
                        _crystalLights[i].Intensity = _crystalBase[i] * (1f + T.CrystalPulse * Mathf.Sin(_t * Mathf.PI * 2f / T.CrystalPulseSeconds + i * 1.7f));

            // Behind: rising, drifting with the heat.
            if (_back != null)
                for (int i = 0; i < _back.Length; i++)
                {
                    if (_back[i] == null) continue;
                    var m = _backMove[i];
                    var p = (Vector2)_back[i].localPosition + new Vector2(m.x, m.y) * dt;
                    if (p.y > _max.y) p = new Vector2(Mathf.Lerp(_min.x, _max.x, Frac(m.z * 13.1f + _t)), _min.y);
                    if (p.x > _max.x) p.x = _min.x;
                    _back[i].localPosition = p;
                    if (m.w > 0.5f) _backSr[i].color = new Color(0.75f, 0.13f, 0.12f, 0.45f + 0.35f * Mathf.Sin(_t * 5f + m.z));
                }

            // In front: falling like snow, each swaying on its own pace - never straight down.
            if (_front != null)
                for (int i = 0; i < _front.Length; i++)
                {
                    if (_front[i] == null) continue;
                    var m = _frontMove[i];
                    var b = _frontBase[i] + new Vector2(m.x, m.y) * dt;
                    if (b.y < _min.y) b = new Vector2(Mathf.Lerp(_min.x, _max.x, Frac(m.z * 7.3f + _t * 0.37f)), _max.y);
                    if (b.x > _max.x) b.x = _min.x;
                    _frontBase[i] = b;
                    float sway = Mathf.Sin(_t * (0.6f + Frac(m.z) * 0.9f) + m.z) * (0.07f + Frac(m.z * 3f) * 0.1f);
                    _front[i].localPosition = Snap(b + new Vector2(sway, 0f));
                    if (m.w < 1.5f) _frontSr[i].color = new Color(1f, 1f, 1f, 0.55f + 0.35f * Mathf.Sin(_t * 4f + m.z));
                }
        }

        // ------------------------------------------------------------------ helpers

        static float Frac(float v) => v - Mathf.Floor(v);
        static Vector3 Snap(Vector2 p) => new(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);

        static SpriteRenderer Quad(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }


        static Sprite Make(Color[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(px); tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        static Sprite _dot, _ember, _wispL, _wispR, _shard;
        static Sprite Dot => _dot ??= Tiny(1, 1, (x, y) => Color.white);
        static Sprite ShardSprite => _shard ??= Tiny(1, 2, (x, y) => Color.white);
        static Sprite Ember => _ember ??= Tiny(3, 3, (x, y) =>
            x == 1 && y == 1 ? new Color(1f, 0.55f, 0.43f) : (x == 1 || y == 1) ? new Color(0.82f, 0.18f, 0.14f) : new Color(0.55f, 0.1f, 0.09f, 0.6f));
        static Sprite WispL => _wispL ??= Tiny(3, 2, (x, y) => y == 1 ? (x < 2 ? Color.white : Color.clear) : (x > 0 ? Color.white : Color.clear));
        static Sprite WispR => _wispR ??= Tiny(3, 2, (x, y) => y == 1 ? (x > 0 ? Color.white : Color.clear) : (x < 2 ? Color.white : Color.clear));

        static Sprite Tiny(int w, int h, System.Func<int, int, Color> f)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) px[y * w + x] = f(x, y);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(px); tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            sp.hideFlags = HideFlags.HideAndDontSave;
            return sp;
        }

        static float Hash(int x, int s)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + (s + 1) * 668265263);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
