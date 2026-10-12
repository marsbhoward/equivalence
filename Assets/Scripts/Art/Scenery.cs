using System.Collections.Generic;
using Convergence.Core;
using UnityEngine;

namespace Convergence.Art
{
    /// <summary>
    /// The arena's SCENERY, rolled per floor: the floor's masonry and stone, the boundary walls'
    /// stone, and - past floor 25 - a chance of WINDOWS in the north wall whose light tells the
    /// climb's height (Tuning.Scenery): a dark evening glow, dawn, high noon, and floor 100's
    /// crimson.
    ///
    /// FloorArt's rules hold for every floor it can make: POINT filtered, one texel per body art
    /// pixel (37.5 per unit), and a MID-VALUE, LOW-CONTRAST floor so what stands on it reads. Every
    /// pattern is described only as a map of STONE IDS; mortar is wherever the id changes, and the
    /// bevel (lit top/left, shaded bottom/right) is read off the mortar - so a new pattern is one
    /// id function, and every pattern shades alike.
    ///
    /// The boundary is drawn rather than a flat quad: the walls' TOPS round the room, and on the
    /// north the wall's FACE, which the 3/4 camera sees - the only place a window can go. Beyond
    /// the tops is dark stone, not more floor (the brick floor used to run on past the walls).
    ///
    /// Layers, all under everything that stands: floor -10, the dark beyond -9, wall tops and the
    /// face -8, the time of day's tint over floor and walls -7, windows and the light they throw
    /// on the floor -6, the beams in the air -5. Purely visual - nothing in the game reads it.
    /// </summary>
    public static class Scenery
    {
        public const float Ppu = FloorArt.PixelsPerUnit;

        public enum Masonry { RunningBond, Basketweave, Herringbone, Ashlar, Cobble, Checker }
        public enum Daylight { None, Evening, Dawn, Noon, Crimson }
        public enum WindowStyle { Arched, Lancet, Oculus, Grid }

        /// <summary>One floor's look. Pure data - <see cref="Roll"/> makes it, <see cref="Dress"/> draws it.</summary>
        public struct Theme
        {
            public Masonry Floor;
            public int Stone, Accent, WallStone;
            public float Mix;
            public Daylight Light;
            public WindowStyle Style;
            public int WindowPairs;
            public int Seed;

            public override string ToString() =>
                $"{Floor} {Stones[Stone].Name}" + (Mix > 0f ? $" + {Stones[Accent].Name} {Mix:0%}" : "") +
                $", walls {Stones[WallStone].Name}" +
                (Light == Daylight.None || WindowPairs == 0 ? "" : $", {WindowPairs * 2} {Style} windows at {Light}");
        }

        /// <summary>The look the arena is built with before any floor - the old brick floor.</summary>
        public static Theme Default => new() { Floor = Masonry.RunningBond, Stone = 0, Accent = 0, WallStone = 0 };

        // ------------------------------------------------------------------ stones

        struct Stone
        {
            public string Name;
            public Color Face, Mortar, Wall;
            public Stone(string n, Color f, Color m, Color w) { Name = n; Face = f; Mortar = m; Wall = w; }
        }

        // Floor stones stay MID-VALUE (FloorArt's rule); their wall stone is the dark one that
        // stands round them. Basalt is the darkest floor allowed - any darker and dark gear
        // vanishes on it again.
        static readonly Stone[] Stones =
        {
            new("sandstone",  new(0.55f, 0.45f, 0.31f), new(0.31f, 0.25f, 0.18f), new(0.27f, 0.22f, 0.17f)),
            new("terracotta", new(0.57f, 0.37f, 0.27f), new(0.33f, 0.22f, 0.17f), new(0.28f, 0.19f, 0.15f)),
            new("slate",      new(0.41f, 0.44f, 0.48f), new(0.23f, 0.25f, 0.28f), new(0.19f, 0.20f, 0.23f)),
            new("limestone",  new(0.61f, 0.58f, 0.51f), new(0.37f, 0.34f, 0.29f), new(0.30f, 0.28f, 0.25f)),
            new("mossrock",   new(0.45f, 0.48f, 0.38f), new(0.25f, 0.28f, 0.21f), new(0.21f, 0.23f, 0.18f)),
            new("rose granite", new(0.56f, 0.45f, 0.44f), new(0.32f, 0.25f, 0.25f), new(0.26f, 0.21f, 0.21f)),
            new("basalt",     new(0.41f, 0.39f, 0.41f), new(0.22f, 0.21f, 0.23f), new(0.16f, 0.155f, 0.17f)),
            new("ochre",      new(0.60f, 0.50f, 0.31f), new(0.35f, 0.28f, 0.17f), new(0.29f, 0.23f, 0.15f)),
        };

        // ------------------------------------------------------------------ daylight

        struct Sky
        {
            public Color Low, Mid, High;     // the window's sky, bottom to top
            public Color Light;              // what it throws on the floor
            public float PatchAlpha, BeamAlpha;
            public Color Tint; public float TintAlpha;   // over floor and walls
            public float Reach;              // floor distance per unit of window height (sun elevation)
            public float Slant;              // sideways drift per unit of that distance
        }

        static Sky SkyOf(Daylight d) => d switch
        {
            // Sunset gone: an ember band low, indigo above, stars. The room darkens; the light
            // that comes in is low, long and amber, from the west.
            Daylight.Evening => new Sky
            {
                Low = new(0.96f, 0.50f, 0.22f), Mid = new(0.52f, 0.22f, 0.36f), High = new(0.11f, 0.09f, 0.25f),
                Light = new(1f, 0.60f, 0.30f), PatchAlpha = 0.22f, BeamAlpha = 0.10f,
                Tint = new(0.05f, 0.04f, 0.15f), TintAlpha = 0.30f, Reach = 2.6f, Slant = 0.45f,
            },
            // First light: peach at the horizon, rose, a pale blue going up; long and soft, from
            // the east - the beams lean the other way.
            Daylight.Dawn => new Sky
            {
                Low = new(1f, 0.82f, 0.56f), Mid = new(0.95f, 0.62f, 0.62f), High = new(0.56f, 0.63f, 0.86f),
                Light = new(1f, 0.84f, 0.72f), PatchAlpha = 0.24f, BeamAlpha = 0.085f,
                Tint = new(0.45f, 0.36f, 0.56f), TintAlpha = 0.08f, Reach = 2.2f, Slant = -0.45f,
            },
            // The sun overhead: a hard blue sky, short bright patches tucked under the wall, and
            // no beams to speak of - the light falls almost straight down.
            Daylight.Noon => new Sky
            {
                Low = new(0.78f, 0.91f, 1f), Mid = new(0.52f, 0.76f, 0.98f), High = new(0.30f, 0.56f, 0.93f),
                Light = new(1f, 0.97f, 0.86f), PatchAlpha = 0.42f, BeamAlpha = 0f,
                Tint = new(1f, 0.96f, 0.85f), TintAlpha = 0.05f, Reach = 0.8f, Slant = 0f,
            },
            // Floor 100. A red sky, blood light straight in.
            Daylight.Crimson => new Sky
            {
                Low = new(1f, 0.27f, 0.12f), Mid = new(0.62f, 0.06f, 0.08f), High = new(0.18f, 0.02f, 0.04f),
                Light = new(1f, 0.20f, 0.13f), PatchAlpha = 0.30f, BeamAlpha = 0.11f,
                Tint = new(0.30f, 0.02f, 0.05f), TintAlpha = 0.28f, Reach = 1.9f, Slant = 0f,
            },
            _ => default,
        };

        /// <summary>The time of day a floor's windows show (Tuning.Scenery's bands).</summary>
        public static Daylight LightFor(int floor)
        {
            if (floor >= Tuning.Scenery.CrimsonFloor) return Daylight.Crimson;
            if (floor >= Tuning.Scenery.NoonFrom) return Daylight.Noon;
            if (floor >= Tuning.Scenery.DawnFrom) return Daylight.Dawn;
            if (floor >= Tuning.Scenery.EveningFrom) return Daylight.Evening;
            return Daylight.None;
        }

        /// <summary>Testing: the next <see cref="Roll"/> has windows (if its floor has a daylight
        /// band at all), then this clears. One-shot, like the other DevForce switches.</summary>
        public static bool DevForceWindows;

        /// <summary>Testing: the next <see cref="Roll"/> uses this daylight whatever the floor
        /// (and has windows). One-shot.</summary>
        public static Daylight? DevForceLight;

        /// <summary>A floor's look, from its own seed (FloorPlanner.SceneSeed) - the same room on a restart.</summary>
        public static Theme Roll(int floor, int seed)
        {
            var rng = new System.Random(seed);
            var t = new Theme
            {
                Seed = seed,
                Floor = (Masonry)rng.Next(6),
                Stone = rng.Next(Stones.Length),
            };
            t.Accent = (t.Stone + 1 + rng.Next(Stones.Length - 1)) % Stones.Length;
            if (t.Floor == Masonry.Checker) t.Mix = 0.5f;
            else if (rng.NextDouble() < Tuning.Scenery.AccentChance)
                t.Mix = Mathf.Lerp(Tuning.Scenery.AccentMin, Tuning.Scenery.AccentMax, (float)rng.NextDouble());
            t.WallStone = rng.NextDouble() < Tuning.Scenery.WallMatchesFloorChance ? t.Stone : rng.Next(Stones.Length);

            t.Light = LightFor(floor);
            bool windows = t.Light == Daylight.Crimson || rng.NextDouble() < Tuning.Scenery.WindowChance;
            t.Style = (WindowStyle)rng.Next(4);
            int pairs = 1 + rng.Next(3);
            if (DevForceLight.HasValue) { t.Light = DevForceLight.Value; windows = true; DevForceLight = null; }
            if (DevForceWindows) { windows = true; DevForceWindows = false; }
            t.WindowPairs = windows && t.Light != Daylight.None ? pairs : 0;
            return t;
        }

        // ------------------------------------------------------------------ dressing the arena

        static Transform _lastRoot;
        static Vector2 _lastHalf;
        static float _lastMargin;

        /// <summary>The theme the live arena is wearing.</summary>
        public static Theme Current { get; private set; } = Default;

        /// <summary>
        /// Dress the arena under <paramref name="arenaRoot"/>: swaps the procedural floor's tile
        /// (an authored floor is left alone) and rebuilds the boundary and the light. Last floor's
        /// dressing and every texture it made are destroyed.
        /// </summary>
        public static void Dress(Transform arenaRoot, Theme theme, Vector2 half, float margin)
        {
            if (arenaRoot == null) return;
            _lastRoot = arenaRoot; _lastHalf = half; _lastMargin = margin;
            Current = theme;

            var art = GameArt.I;
            var floor = arenaRoot.Find("Floor");
            if (art.FloorSprite == null && floor != null && floor.TryGetComponent<SpriteRenderer>(out var fsr))
            {
                var size = fsr.size;
                fsr.sprite = FloorTile(theme.Floor, theme.Stone, theme.Accent, theme.Mix);
                fsr.size = size;
            }

            var old = arenaRoot.Find("Scenery");
            if (old != null) { old.name = "Scenery (old)"; Object.Destroy(old.gameObject); }

            var go = new GameObject("Scenery");
            go.transform.SetParent(arenaRoot, false);
            var owned = go.AddComponent<SceneryOwned>();
            new Builder(go.transform, owned, theme, half, margin, art.WallSprite == null).Build();
        }

        /// <summary>Testing: re-dress the live arena as floor <paramref name="floor"/> with seed
        /// <paramref name="seed"/> and say what it rolled. Combine with DevForceLight.</summary>
        public static string Preview(int floor, int seed)
        {
            if (_lastRoot == null)
            {
                var a = GameObject.Find("Arena");
                if (a == null) return "no arena";
                _lastRoot = a.transform;
                _lastHalf = new Vector2(Tuning.Arena.HalfWidth, Tuning.Arena.HalfHeight);
                _lastMargin = Tuning.Arena.FloorMargin;
            }
            var t = Roll(floor, seed);
            Dress(_lastRoot, t, _lastHalf, _lastMargin);
            return t.ToString();
        }

        /// <summary>One floor's construction - kept in a class so the dozen numbers every piece
        /// shares are fields rather than parameters.</summary>
        class Builder
        {
            readonly Transform _root;
            readonly SceneryOwned _owned;
            readonly Theme _t;
            readonly Stone _wall;
            readonly Sky _sky;
            readonly bool _drawWalls;
            readonly float _w, _h, _m;      // inner faces' half extents, how far the ground runs
            readonly int _faceW, _faceH;    // the north face, texels (whole, the width even)
            readonly float _faceLeft, _faceTop, _capTop;

            public Builder(Transform root, SceneryOwned owned, Theme t, Vector2 half, float margin, bool drawWalls)
            {
                _root = root; _owned = owned; _t = t;
                _wall = Stones[t.WallStone];
                _sky = SkyOf(t.Light);
                _drawWalls = drawWalls;
                _w = half.x - Tuning.Scenery.WallInset;
                _h = half.y - Tuning.Scenery.WallInset;
                _m = margin + 1f;
                // The face is drawn at scale 1 so its texels and the window layer's line up exactly.
                _faceW = Mathf.RoundToInt(_w * Ppu) * 2;
                _faceH = Mathf.RoundToInt(Tuning.Scenery.FaceHeight * Ppu);
                _faceLeft = -_faceW * 0.5f / Ppu;
                _faceTop = _h + _faceH / Ppu;
                _capTop = _faceTop + Tuning.Scenery.CapHeight;
            }

            bool Lit => _t.Light != Daylight.None && _t.WindowPairs > 0;

            public void Build()
            {
                var windows = Lit ? PlaceWindows() : new List<Window>();
                if (_drawWalls) BuildBoundary(windows);
                if (!Lit) return;

                // The time of day over floor and walls - never over a body (it sorts under them).
                if (_sky.TintAlpha > 0f)
                {
                    var c = _sky.Tint; c.a = _sky.TintAlpha;
                    Quad("tint", Spr.Square, new Vector2(0f, 0f), new Vector2((_w + _m) * 2f, (_capTop + _m) * 2f), c, -7);
                }
                foreach (var win in windows)
                {
                    BuildGlass(win);
                    BuildPatch(win);
                    if (_sky.BeamAlpha > 0f) BuildBeam(win);
                }
            }

            // -------------------------------------------------------------- the walls

            void BuildBoundary(List<Window> windows)
            {
                float band = Tuning.Scenery.TopBand;
                var top = WallTile(_t.WallStone, false);
                var dark = WallTile(_t.WallStone, true);

                // The dark beyond, then the tops over it.
                float far = _capTop + _m;
                Tiled("beyond.n", dark, new Vector2(-_w - _m, _capTop), new Vector2(_w + _m, far), -9);
                Tiled("beyond.s", dark, new Vector2(-_w - _m, -_h - _m), new Vector2(_w + _m, -_h - band), -9);
                Tiled("beyond.w", dark, new Vector2(-_w - _m, -_h - _m), new Vector2(-_w - band, far), -9);
                Tiled("beyond.e", dark, new Vector2(_w + band, -_h - _m), new Vector2(_w + _m, far), -9);

                Tiled("top.s", top, new Vector2(-_w - band, -_h - band), new Vector2(_w + band, -_h), -8);
                Tiled("top.w", top, new Vector2(-_w - band, -_h - band), new Vector2(-_w, _capTop), -8);
                Tiled("top.e", top, new Vector2(_w, -_h - band), new Vector2(_w + band, _capTop), -8);
                Tiled("cap.n", top, new Vector2(-_w - band, _faceTop), new Vector2(_w + band, _capTop), -8);

                // The far edge of each top falls away into the dark: a shadowed lip.
                var lip = Mul(_wall.Wall, 0.55f);
                Edge(new Vector2(-_w - band, _capTop), new Vector2(_w + band, _capTop + 2f / Ppu), lip);
                Edge(new Vector2(-_w - band, -_h - band - 2f / Ppu), new Vector2(_w + band, -_h - band), lip);
                Edge(new Vector2(-_w - band - 2f / Ppu, -_h - band), new Vector2(-_w - band, _capTop), lip);
                Edge(new Vector2(_w + band, -_h - band), new Vector2(_w + band + 2f / Ppu, _capTop), lip);

                // Where a top meets the room: a dark line, the lit arris above it.
                var line = Mul(_wall.Wall, 0.35f);
                var arris = Shade(_wall.Wall, 0.10f);
                float px = 1f / Ppu;
                Edge(new Vector2(-_w, -_h - px), new Vector2(_w, -_h), line);
                Edge(new Vector2(-_w, -_h - 3f * px), new Vector2(_w, -_h - px), arris);
                Edge(new Vector2(-_w, -_h), new Vector2(-_w + px, _faceTop), line);
                Edge(new Vector2(-_w - 2f * px, -_h - 3f * px), new Vector2(-_w, _faceTop), arris);
                Edge(new Vector2(_w - px, -_h), new Vector2(_w, _faceTop), line);
                Edge(new Vector2(_w, -_h - 3f * px), new Vector2(_w + 2f * px, _faceTop), arris);

                BuildFace(windows);

                // Contact shadow along the face's foot and a thinner one down each side wall:
                // what stands the walls ON the floor.
                Quad("shadow.n", Spr.Square, new Vector2(0f, _h - 0.1f), new Vector2(_w * 2f, 0.2f), new Color(0f, 0f, 0f, 0.28f), -7);
                Quad("shadow.n2", Spr.Square, new Vector2(0f, _h - 0.27f), new Vector2(_w * 2f, 0.14f), new Color(0f, 0f, 0f, 0.12f), -7);
                Quad("shadow.w", Spr.Square, new Vector2(-_w + 0.06f, 0f), new Vector2(0.12f, _h * 2f), new Color(0f, 0f, 0f, 0.18f), -7);
                Quad("shadow.e", Spr.Square, new Vector2(_w - 0.06f, 0f), new Vector2(0.12f, _h * 2f), new Color(0f, 0f, 0f, 0.18f), -7);
            }

            /// <summary>The north wall's face: coursed ashlar over a plinth under a cornice, with
            /// every window's dressed-stone surround and sill cut into it.</summary>
            void BuildFace(List<Window> windows)
            {
                int W = _faceW, H = _faceH;
                var px = new Color[W * H];
                var rng = new System.Random(_t.Seed * 31 + 7);
                var face = Mul(_wall.Wall, 0.78f);
                var seam = Mul(_wall.Wall, 0.45f);
                const int plinth = 7, cornice = 4, course = 9;

                // Course layout: random block widths, staggered.
                int rows = (H - plinth - cornice) / course;
                var joints = new List<int>[rows + 1];
                for (int r = 0; r <= rows; r++)
                {
                    joints[r] = new List<int>();
                    int x = -rng.Next(30);
                    while (x < W) { joints[r].Add(x); x += r == 0 ? rng.Next(28, 46) : rng.Next(14, 32); }
                }

                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    Color c;
                    float n = (Hash(x * 3 + 1, y * 5 + 2) - 0.5f) * 0.025f;
                    if (y < plinth)
                    {
                        bool j = joints[0].Contains(x);
                        c = y == plinth - 1 || j ? seam : Shade(Mul(face, 0.82f), n + (y == plinth - 2 ? 0.03f : 0f));
                    }
                    else if (y >= H - cornice)
                    {
                        int cy = y - (H - cornice);
                        c = cy == 0 ? seam : Shade(_wall.Wall, cy == cornice - 1 ? 0.07f : 0.03f + n);
                    }
                    else
                    {
                        int r = Mathf.Min(rows - 1, (y - plinth) / course) + 1;
                        int cy = (y - plinth) % course;
                        int id = 0;
                        bool joint = false;
                        foreach (int jx in joints[r]) { if (jx == x) joint = true; if (jx <= x) id = jx; }
                        if (cy == course - 1 || joint) c = seam;
                        else
                        {
                            float tone = (Hash(id + 977, r) - 0.5f) * 0.06f;
                            float v = tone + n;
                            if (cy == course - 2) v += 0.035f;          // lit top of the block
                            if (cy == 0) v -= 0.03f;                     // shaded underside
                            if (Hash(x + id, y * 3) < 0.04f) v -= 0.03f;
                            c = Shade(face, v);
                        }
                    }
                    // Darker toward the foot - the floor's shadow climbing the wall.
                    float foot = Mathf.Clamp01(1f - y / (H * 0.45f));
                    c = Shade(c, -0.05f * foot);
                    // Each end in the corner's shade.
                    int e = Mathf.Min(x, W - 1 - x);
                    if (e < 4) c = Shade(c, -0.05f * (4 - e) / 4f);
                    px[y * W + x] = c;
                }

                // Window surrounds.
                var dressed = Color.Lerp(_wall.Wall, Stones[_t.WallStone].Face, 0.55f);
                var outline = Mul(_wall.Wall, 0.3f);
                foreach (var win in windows)
                {
                    int ox = win.Left, oy = win.Sill;
                    const int pad = 5;
                    var dist = Dist(win, pad, out int dw, out _);
                    for (int y = -pad; y < win.Ht + 4; y++)
                    for (int x = -pad; x < win.Wt + pad; x++)
                    {
                        int fx = ox + x, fy = oy + y;
                        if (fx < 0 || fx >= W || fy < 0 || fy >= H) continue;
                        int g = dist[(y + pad) * dw + x + pad];
                        if (g == 0) { px[fy * W + fx] = Mul(_wall.Wall, 0.2f); continue; }
                        if (g < 3) px[fy * W + fx] = Shade(dressed, (g == 1 ? 0.04f : 0f) + (Hash(fx, fy) - 0.5f) * 0.02f);
                        else if (g == 3) px[fy * W + fx] = outline;
                    }
                    // The sill: a lit ledge a little wider than the surround.
                    for (int y = -4; y < -1; y++)
                    for (int x = -5; x < win.Wt + 5; x++)
                    {
                        int fx = ox + x, fy = oy + y;
                        if (fx < 0 || fx >= W || fy < 0 || fy >= H) continue;
                        px[fy * W + fx] = y == -2 ? Shade(dressed, 0.08f) : y == -3 ? dressed : outline;
                    }
                }

                var spr = Make(px, W, H, new Vector2(0.5f, 0f));
                Renderer("face", spr, new Vector3(0f, _h, 0f), -8);
            }

            // -------------------------------------------------------------- windows

            struct Window
            {
                public float X;          // world x of its centre (on the face's texel grid)
                public int Left;         // glass bounds' left edge, face texels
                public int Wt, Ht;       // glass bounds, texels
                public int Sill;         // glass bottom, texels above the face's foot
                public WindowStyle Style;
            }

            List<Window> PlaceWindows()
            {
                var list = new List<Window>();
                int n = _t.WindowPairs;
                float from = Tuning.Scenery.DoorClearance, to = _w - 1f;
                for (int i = 0; i < n; i++)
                {
                    float x = from + (i + 0.5f) * (to - from) / n;
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var win = new Window { X = s * x, Style = _t.Style };
                        switch (_t.Style)
                        {
                            case WindowStyle.Arched: win.Wt = 22; win.Ht = 46; win.Sill = 18; break;
                            case WindowStyle.Lancet: win.Wt = 22; win.Ht = 48; win.Sill = 17; break;
                            case WindowStyle.Oculus: win.Wt = 28; win.Ht = 28; win.Sill = 32; break;
                            default:                 win.Wt = 24; win.Ht = 36; win.Sill = 22; break;
                        }
                        win.Left = Mathf.RoundToInt((win.X - _faceLeft) * Ppu) - win.Wt / 2;
                        win.X = _faceLeft + (win.Left + win.Wt * 0.5f) / Ppu;
                        list.Add(win);
                    }
                }
                return list;
            }

            /// <summary>0 outside the window, 1 glass, 2 a bar. (gx, gy) in texels from the glass
            /// bounds' bottom-left; fractional for the projected patch.</summary>
            static int Mask(Window w, float gx, float gy)
            {
                if (gx < 0f || gy < 0f || gx >= w.Wt || gy >= w.Ht) return 0;
                int ix = (int)gx, iy = (int)gy;
                switch (w.Style)
                {
                    case WindowStyle.Arched:
                    {
                        if (!InArch(gx, gy, 0f, w.Wt, w.Ht)) return 0;
                        if (ix == w.Wt / 2 - 1 || ix == w.Wt / 2) return 2;
                        if (iy == (int)(w.Ht * 0.48f)) return 2;
                        return 1;
                    }
                    case WindowStyle.Lancet:
                    {
                        const int light = 9;
                        float lx = gx < light ? gx : gx - (w.Wt - light);
                        if (gx >= light && gx < w.Wt - light) return 0;
                        if (!InArch(lx, gy, 0f, light, w.Ht)) return 0;
                        if (iy == (int)(w.Ht * 0.5f)) return 2;
                        return 1;
                    }
                    case WindowStyle.Oculus:
                    {
                        float r = w.Wt * 0.5f, dx = gx - r, dy = gy - r;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d >= r) return 0;
                        if (Mathf.Abs(dx) < 1f || Mathf.Abs(dy) < 1f) return 2;
                        if (Mathf.Abs(d - r * 0.5f) < 0.6f) return 2;
                        return 1;
                    }
                    default:
                    {
                        if (ix == w.Wt / 3 || ix == w.Wt * 2 / 3) return 2;
                        if (iy == w.Ht / 3 || iy == w.Ht * 2 / 3) return 2;
                        return 1;
                    }
                }
            }

            static bool InArch(float gx, float gy, float x0, float width, float height)
            {
                float r = width * 0.5f, spring = height - r;
                if (gy < spring) return true;
                float dx = gx + 0.5f - x0 - r, dy = gy + 0.5f - spring;
                return dx * dx + dy * dy < r * r;
            }

            /// <summary>Chebyshev distance to the window's opening over its bounds padded by
            /// <paramref name="pad"/> (0 inside) - two chamfer passes.</summary>
            static int[] Dist(Window w, int pad, out int W, out int H)
            {
                W = w.Wt + pad * 2; H = w.Ht + pad * 2;
                var d = new int[W * H];
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    d[y * W + x] = Mask(w, x - pad, y - pad) != 0 ? 0 : 9999;
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int v = d[y * W + x];
                    if (x > 0) v = Mathf.Min(v, d[y * W + x - 1] + 1);
                    if (y > 0)
                    {
                        v = Mathf.Min(v, d[(y - 1) * W + x] + 1);
                        if (x > 0) v = Mathf.Min(v, d[(y - 1) * W + x - 1] + 1);
                        if (x < W - 1) v = Mathf.Min(v, d[(y - 1) * W + x + 1] + 1);
                    }
                    d[y * W + x] = v;
                }
                for (int y = H - 1; y >= 0; y--)
                for (int x = W - 1; x >= 0; x--)
                {
                    int v = d[y * W + x];
                    if (x < W - 1) v = Mathf.Min(v, d[y * W + x + 1] + 1);
                    if (y < H - 1)
                    {
                        v = Mathf.Min(v, d[(y + 1) * W + x] + 1);
                        if (x > 0) v = Mathf.Min(v, d[(y + 1) * W + x - 1] + 1);
                        if (x < W - 1) v = Mathf.Min(v, d[(y + 1) * W + x + 1] + 1);
                    }
                    d[y * W + x] = v;
                }
                return d;
            }

            /// <summary>The window's sky and bars, and the light spilling round it onto the wall -
            /// a layer of its own, over the time of day's tint, so the window glows.</summary>
            void BuildGlass(Window w)
            {
                const int glow = 9;
                var dist = Dist(w, glow, out int W, out int H);
                var px = new Color[W * H];
                var bar = new Color(0.07f, 0.06f, 0.06f);
                int seedX = Mathf.RoundToInt(w.X * 10f);
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int gx = x - glow, gy = y - glow;
                    int m = Mask(w, gx, gy);
                    Color c;
                    if (m == 2) c = bar;
                    else if (m == 1) c = SkyAt(gx, gy, w.Ht, seedX);
                    else
                    {
                        int d = dist[y * W + x];
                        if (d > glow) { px[y * W + x] = Color.clear; continue; }
                        float a = Mathf.Pow(1f - (d - 1f) / glow, 2f) * 0.35f;
                        a = Mathf.Round(a / 0.05f) * 0.05f;        // in steps, as pixel art lights
                        c = _sky.Light; c.a = a;
                    }
                    px[y * W + x] = c;
                }
                var spr = Make(px, W, H, Vector2.zero);
                Renderer("window", spr,
                         new Vector3(_faceLeft + (w.Left - glow) / Ppu, _h + (w.Sill - glow) / Ppu, 0f), -6);
            }

            Color SkyAt(int gx, int gy, int ht, int seedX)
            {
                float t = (gy + 0.5f) / ht;
                var c = t < 0.5f ? Color.Lerp(_sky.Low, _sky.Mid, t * 2f) : Color.Lerp(_sky.Mid, _sky.High, (t - 0.5f) * 2f);
                // banded rather than smooth - pixel art skies step
                c = new Color(Mathf.Round(c.r * 24f) / 24f, Mathf.Round(c.g * 24f) / 24f, Mathf.Round(c.b * 24f) / 24f);
                float h = Hash(gx + seedX * 13, gy + 101);
                switch (_t.Light)
                {
                    case Daylight.Evening: if (t > 0.55f && h > 0.975f) c = new Color(1f, 0.95f, 0.84f); break;
                    case Daylight.Noon:
                        if (t > 0.35f && t < 0.6f && Hash(gx / 3 + seedX, gy / 2 + 7) > 0.72f) c = Color.Lerp(c, Color.white, 0.75f);
                        break;
                    case Daylight.Dawn: if (gy == 0 || gy == 1) c = Color.Lerp(c, new Color(1f, 0.95f, 0.8f), 0.6f); break;
                    case Daylight.Crimson: if (t > 0.4f && h > 0.985f) c = new Color(0.08f, 0.01f, 0.02f); break;
                }
                // a glint across the glass
                if ((gx + gy) % 13 == 0) c = Shade(c, 0.06f);
                c.a = 1f;
                return c;
            }

            /// <summary>Where the window's light lands on the floor: the opening projected down and
            /// out along the sun, bars and all - the top of the window lands farthest out.</summary>
            void BuildPatch(Window w)
            {
                float zs = w.Sill / Ppu, zt = (w.Sill + w.Ht) / Ppu;
                float dmin = zs * _sky.Reach, dmax = zt * _sky.Reach;
                float halfW = w.Wt * 0.5f / Ppu;
                float x0 = w.X - halfW + Mathf.Min(0f, _sky.Slant * dmax) - 2f / Ppu;
                float x1 = w.X + halfW + Mathf.Max(0f, _sky.Slant * dmax) + 2f / Ppu;
                int W = Mathf.CeilToInt((x1 - x0) * Ppu), H = Mathf.CeilToInt((dmax - dmin) * Ppu) + 2;
                var px = new Color[W * H];
                for (int y = 0; y < H; y++)          // y counts DOWN from the patch's near edge
                for (int x = 0; x < W; x++)
                {
                    float d = dmin + (y + 0.5f) / Ppu, wx = x0 + (x + 0.5f) / Ppu;
                    float vg = (d / _sky.Reach - zs) / (zt - zs);
                    float gx = (wx - w.X - _sky.Slant * d) * Ppu + w.Wt * 0.5f;
                    int m = Mask(w, gx, vg * w.Ht);
                    Color c = Color.clear;
                    if (m == 1)
                    {
                        float a = _sky.PatchAlpha * (1f - 0.4f * Mathf.Clamp01(vg));
                        c = _sky.Light; c.a = Mathf.Round(a / 0.03f) * 0.03f;
                    }
                    px[(H - 1 - y) * W + x] = c;
                }
                var spr = Make(px, W, H, new Vector2(0f, 1f));
                Renderer("patch", spr, new Vector3(x0, _h - dmin, 0f), -6);
            }

            /// <summary>The beam through the air between the window and its patch - only when the
            /// sun is low enough to show one.</summary>
            void BuildBeam(Window w)
            {
                float zs = w.Sill / Ppu, zt = (w.Sill + w.Ht) / Ppu;
                float dmin = zs * _sky.Reach, dmax = zt * _sky.Reach;
                float hw = w.Wt * 0.5f / Ppu * 0.85f;
                var pts = new List<Vector2>
                {
                    new(w.X - hw, _h + zs), new(w.X + hw, _h + zs),
                    new(w.X - hw, _h + zt), new(w.X + hw, _h + zt),
                    new(w.X - hw + _sky.Slant * dmin, _h - dmin), new(w.X + hw + _sky.Slant * dmin, _h - dmin),
                    new(w.X - hw + _sky.Slant * dmax, _h - dmax), new(w.X + hw + _sky.Slant * dmax, _h - dmax),
                };
                var hull = Hull(pts);
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (var p in hull) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
                int W = Mathf.CeilToInt((maxX - minX) * Ppu), H = Mathf.CeilToInt((maxY - minY) * Ppu);
                var px = new Color[W * H];
                float top = _h + zt, span = zt + dmax;
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var p = new Vector2(minX + (x + 0.5f) / Ppu, minY + (y + 0.5f) / Ppu);
                    if (!Inside(hull, p)) continue;
                    float t = Mathf.Clamp01((top - p.y) / span);
                    float a = t < 0.15f ? t / 0.15f : Mathf.Lerp(1f, 0.25f, (t - 0.15f) / 0.85f);
                    a *= _sky.BeamAlpha;
                    // dithered steps, so the beam reads as pixel light rather than a smooth gradient
                    float step = 0.02f, q = a / step;
                    a = (Mathf.Floor(q) + (q - Mathf.Floor(q) > Bayer(x, y) ? 1f : 0f)) * step;
                    var c = _sky.Light; c.a = a;
                    px[y * W + x] = c;
                }
                var spr = Make(px, W, H, Vector2.zero);
                Renderer("beam", spr, new Vector3(minX, minY, 0f), -5);
            }

            // -------------------------------------------------------------- pieces

            void Tiled(string name, Sprite tile, Vector2 min, Vector2 max, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(_root, false);
                go.transform.localPosition = (min + max) * 0.5f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = tile;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = max - min;
                sr.sortingOrder = order;
            }

            // Edges share the tops' order, so they sit a hair nearer the camera to draw after
            // them; the contact shadows share the tint's and sit a hair farther, to draw before it.
            void Edge(Vector2 min, Vector2 max, Color c)
            {
                Quad("edge", Spr.Square, (min + max) * 0.5f, max - min, c, -8);
                _root.GetChild(_root.childCount - 1).localPosition += new Vector3(0f, 0f, -0.01f);
            }

            void Quad(string name, Sprite s, Vector2 centre, Vector2 size, Color c, int order)
            {
                var sr = Renderer(name, s, new Vector3(centre.x, centre.y, name.StartsWith("shadow") ? 0.01f : 0f), order);
                sr.color = c;
                sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            }

            SpriteRenderer Renderer(string name, Sprite s, Vector3 pos, int order)
            {
                var go = new GameObject(name);
                go.transform.SetParent(_root, false);
                go.transform.localPosition = pos;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s;
                sr.sortingOrder = order;
                return sr;
            }

            Sprite Make(Color[] px, int w, int h, Vector2 pivot)
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                tex.SetPixels(px);
                tex.Apply();
                var s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, Ppu, 0, SpriteMeshType.FullRect);
                s.hideFlags = HideFlags.HideAndDontSave;
                _owned.Add(tex);
                _owned.Add(s);
                return s;
            }
        }

        // ------------------------------------------------------------------ tiles

        // Twice FloorArt's tile, so the repeat (and where the accent stones fall) is harder to see.
        const int TileW = 256, TileH = 128;
        static readonly Dictionary<long, Sprite> _tiles = new();

        /// <summary>A floor tile: <paramref name="m"/> laid in stone <paramref name="stone"/>, a
        /// share <paramref name="mix"/> of the stones in <paramref name="accent"/>. Cached.</summary>
        public static Sprite FloorTile(Masonry m, int stone, int accent, float mix)
        {
            long key = (long)m | ((long)stone << 4) | ((long)accent << 8) | ((long)Mathf.RoundToInt(mix * 100f) << 12);
            if (_tiles.TryGetValue(key, out var s) && s != null) return s;
            var st = Stones[stone];
            s = BuildTile(m, st.Face, st.Mortar, AccentFor(st.Face, Stones[accent].Face), mix, (int)key, 0.045f);
            _tiles[key] = s;
            return s;
        }

        /// <summary>A second stone kept within the floor's narrow value range: part way to the
        /// accent's hue, at nearly the base stone's lightness. At full strength a limestone and
        /// slate checker was a chessboard, and the floor drew the eye from what stood on it.</summary>
        static Color AccentFor(Color stone, Color accent)
        {
            var c = Color.Lerp(stone, accent, 0.6f);
            float Lum(Color k) => k.r * 0.3f + k.g * 0.59f + k.b * 0.11f;
            float gap = Lum(c) - Lum(stone), keep = Mathf.Clamp(gap, -0.04f, 0.04f);
            return Shade(c, keep - gap);
        }

        /// <summary>The wall tops' big ashlar, or (<paramref name="beyond"/>) the same, darkened,
        /// for what lies past them. Cached.</summary>
        static Sprite WallTile(int stone, bool beyond)
        {
            long key = (1L << 40) | ((long)stone << 1) | (beyond ? 1L : 0L);
            if (_tiles.TryGetValue(key, out var s) && s != null) return s;
            var w = Stones[stone].Wall;
            float k = beyond ? 0.42f : 1f;
            s = BuildTile(Masonry.Ashlar, Mul(w, k * 1.08f), Mul(w, k * 0.55f), w, 0f, (int)key, 0.03f, big: true);
            _tiles[key] = s;
            return s;
        }

        static Sprite BuildTile(Masonry m, Color stone, Color mortarCol, Color accent, float mix, int seed,
                                float toneRange, bool big = false)
        {
            var id = new int[TileW * TileH];
            var mortar = new bool[TileW * TileH];
            var rng = new System.Random(seed * 7919 + 3);
            int I(int x, int y) => Mod(y, TileH) * TileW + Mod(x, TileW);

            // ---- the pattern, as stone ids (ty counts DOWN from the top, as drawn)
            switch (m)
            {
                case Masonry.RunningBond:
                    for (int y = 0; y < TileH; y++)
                    for (int x = 0; x < TileW; x++)
                    {
                        int row = y / 8, bx = (x + (row & 1) * 8) % TileW;
                        id[I(x, y)] = row * 1000 + bx / 16;
                    }
                    break;

                case Masonry.Basketweave:
                    for (int y = 0; y < TileH; y++)
                    for (int x = 0; x < TileW; x++)
                    {
                        int cx = x / 16, cy = y / 16, lx = x % 16, ly = y % 16;
                        int sub = ((cx + cy) & 1) == 0 ? ly / 8 : 2 + lx / 8;
                        id[I(x, y)] = (cy * 100 + cx) * 4 + sub;
                    }
                    break;

                case Masonry.Herringbone:
                {
                    // Staircases of a horizontal and a vertical 2:1 brick, each stair a cell up
                    // and over from the last, stacked every four cells. Period four cells both
                    // ways, so it wraps the tile; anchors are reduced into it so a brick split by
                    // the seam is one brick on both sides.
                    const int u = 8;
                    int cw = TileW / u, ch = TileH / u;
                    for (int y = 0; y < TileH; y++)
                    for (int x = 0; x < TileW; x++)
                    {
                        int i = x / u, j = y / u, d = j - i;
                        int mm = FloorDiv(d + 1, 4), r = d - 4 * mm;
                        int ax = i, ay = j, type = 0;
                        if (r == -1) ax = i - 1;
                        else if (r == 1) type = 1;
                        else if (r == 2) { type = 1; ay = j - 1; }
                        id[I(x, y)] = (Mod(ay, ch) * cw + Mod(ax, cw)) * 2 + type;
                    }
                    break;
                }

                case Masonry.Ashlar:
                {
                    // Courses of random height, stones of random width, each course started at
                    // its own offset so no joint lines up down the tile.
                    var heights = big ? new[] { 12, 14, 16 } : new[] { 8, 10, 12 };
                    int yAt = 0, course = 0;
                    while (yAt < TileH)
                    {
                        int hgt = heights[rng.Next(heights.Length)];
                        if (TileH - (yAt + hgt) < heights[0]) hgt = TileH - yAt;
                        var cuts = new List<int>();
                        int x = 0;
                        while (x < TileW)
                        {
                            cuts.Add(x);
                            int w = big ? rng.Next(20, 44) : rng.Next(12, 30);
                            if (TileW - (x + w) < (big ? 16 : 10)) w = TileW - x;
                            x += w;
                        }
                        int off = rng.Next(TileW);
                        for (int y = yAt; y < yAt + hgt; y++)
                        for (int xx = 0; xx < TileW; xx++)
                        {
                            int lx = Mod(xx - off, TileW), k = 0;
                            for (int c = 0; c < cuts.Count; c++) if (cuts[c] <= lx) k = c;
                            id[I(xx, y)] = course * 100 + k;
                        }
                        yAt += hgt;
                        course++;
                    }
                    break;
                }

                case Masonry.Cobble:
                {
                    // Jittered cells, nearest point wins; the joint is where the two nearest
                    // points are nearly as close, so it is a gap with some width, not a hairline.
                    const int cs = 16;
                    int cw = TileW / cs, ch = TileH / cs;
                    for (int y = 0; y < TileH; y++)
                    for (int x = 0; x < TileW; x++)
                    {
                        int ci = x / cs, cj = y / cs;
                        float f1 = 1e9f, f2 = 1e9f; int best = 0;
                        for (int j = -1; j <= 1; j++)
                        for (int i = -1; i <= 1; i++)
                        {
                            int ni = ci + i, nj = cj + j;
                            int wi = Mod(ni, cw), wj = Mod(nj, ch);
                            float px = ni * cs + cs * 0.5f + (Hash(wi, wj + 300) - 0.5f) * cs * 0.6f;
                            float py = nj * cs + cs * 0.5f + (Hash(wi + 500, wj) - 0.5f) * cs * 0.6f;
                            float dx = x + 0.5f - px, dy = (y + 0.5f - py) * 1.15f;
                            float dd = Mathf.Sqrt(dx * dx + dy * dy);
                            if (dd < f1) { f2 = f1; f1 = dd; best = wj * cw + wi; }
                            else if (dd < f2) f2 = dd;
                        }
                        id[I(x, y)] = best;
                        mortar[I(x, y)] = f2 - f1 < 1.6f;
                    }
                    break;
                }

                case Masonry.Checker:
                    for (int y = 0; y < TileH; y++)
                    for (int x = 0; x < TileW; x++)
                        id[I(x, y)] = (y / 32) * 100 + x / 32;
                    break;
            }

            // ---- mortar wherever the stone changes (right or below), unless the pattern said
            if (m != Masonry.Cobble)
                for (int y = 0; y < TileH; y++)
                for (int x = 0; x < TileW; x++)
                    mortar[I(x, y)] = id[I(x, y)] != id[I(x + 1, y)] || id[I(x, y)] != id[I(x, y + 1)];

            // ---- shade
            var px2 = new Color[TileW * TileH];
            for (int y = 0; y < TileH; y++)
            for (int x = 0; x < TileW; x++)
            {
                int k = I(x, y), sid = id[k];
                Color c;
                if (mortar[k]) c = Shade(mortarCol, (Hash(x, y) - 0.5f) * 0.03f);
                else
                {
                    bool acc = m == Masonry.Checker ? (((sid / 100) + (sid % 100)) & 1) == 1 : Hash(sid, 97 + seed % 13) < mix;
                    var b = acc ? accent : stone;
                    float tone = (Hash(sid, 11) - 0.5f) * toneRange * 2f;
                    b = Shade(b, tone);
                    b.r += (Hash(sid, 23) - 0.5f) * 0.03f;

                    float v = 0f;
                    if (mortar[I(x, y - 1)]) v += 0.05f;          // lit top edge
                    if (mortar[I(x - 1, y)]) v += 0.03f;          // lit left edge
                    if (mortar[I(x, y + 1)]) v -= 0.05f;          // shaded bottom edge
                    if (mortar[I(x + 1, y)]) v -= 0.035f;         // shaded right edge

                    float speck = Hash(x * 3 + 1, y * 5 + 2);
                    if (speck < 0.05f) v -= 0.045f;
                    else if (speck > 0.97f) v += 0.03f;

                    // worn arrises on some stones
                    bool atEdge = mortar[I(x + 1, y)] || mortar[I(x, y - 1)];
                    if (atEdge && Hash(sid, 41) < 0.2f && Hash(x * 7, y * 11) < 0.45f) v -= 0.07f;
                    c = Shade(b, v);
                }
                px2[k] = c;
            }

            // ---- a few short cracks, wandering until they meet a joint
            int cracks = TileW * TileH / 900;
            for (int n = 0; n < cracks; n++)
            {
                int x = rng.Next(TileW), y = rng.Next(TileH);
                int len = rng.Next(3, 7);
                for (int s = 0; s < len; s++)
                {
                    int k = I(x, y);
                    if (mortar[k]) break;
                    px2[k] = Shade(px2[k], -0.09f);
                    x += rng.Next(-1, 2); y += 1;
                }
            }

            // texture rows start at the bottom
            var flipped = new Color[TileW * TileH];
            for (int y = 0; y < TileH; y++)
                System.Array.Copy(px2, y * TileW, flipped, (TileH - 1 - y) * TileW, TileW);

            var tex = new Texture2D(TileW, TileH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(flipped);
            tex.Apply();
            var spr = Sprite.Create(tex, new Rect(0, 0, TileW, TileH), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            spr.hideFlags = HideFlags.HideAndDontSave;
            return spr;
        }

        // ------------------------------------------------------------------ helpers

        static float Hash(int a, int b)
        {
            unchecked
            {
                int h = a * 374761393 + b * 668265263 + 1013904223;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        static readonly int[] BayerTable = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static float Bayer(int x, int y) => (BayerTable[(x & 3) + (y & 3) * 4] + 0.5f) / 16f;

        static int Mod(int a, int n) => ((a % n) + n) % n;
        static int FloorDiv(int a, int n) => a >= 0 ? a / n : -((-a + n - 1) / n);

        static Color Shade(Color c, float v) =>
            new(Mathf.Clamp01(c.r + v), Mathf.Clamp01(c.g + v), Mathf.Clamp01(c.b + v), c.a);

        static Color Mul(Color c, float k) => new(c.r * k, c.g * k, c.b * k, 1f);

        static List<Vector2> Hull(List<Vector2> pts)
        {
            pts.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var h = new List<Vector2>();
            static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
            foreach (var p in pts) { while (h.Count >= 2 && Cross(h[^2], h[^1], p) <= 0f) h.RemoveAt(h.Count - 1); h.Add(p); }
            int lower = h.Count + 1;
            for (int i = pts.Count - 2; i >= 0; i--)
            {
                var p = pts[i];
                while (h.Count >= lower && Cross(h[^2], h[^1], p) <= 0f) h.RemoveAt(h.Count - 1);
                h.Add(p);
            }
            h.RemoveAt(h.Count - 1);
            return h;
        }

        /// <summary>Inside a counter-clockwise convex polygon.</summary>
        static bool Inside(List<Vector2> hull, Vector2 p)
        {
            for (int i = 0; i < hull.Count; i++)
            {
                var a = hull[i]; var b = hull[(i + 1) % hull.Count];
                if ((b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x) < 0f) return false;
            }
            return true;
        }
    }
}
