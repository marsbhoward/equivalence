using UnityEngine;
using Convergence.Core;
using Convergence.Enemies;

namespace Convergence.Combat
{
    /// <summary>
    /// MARKS THE FIGHT LEAVES (the user's pick, 2026-10-22): scorch where a blast or fire went off,
    /// ichor where a body fell or a heavy blow landed, cracks where the ground was struck, chips
    /// where a Watcher broke. They stay until the floor ends, so a room shows the fight it had.
    ///
    /// ONE TEXTURE over the whole room at body density, not an object per mark - a long fight
    /// cannot pile up renderers. Stamped on the CPU, uploaded once a frame at most when something
    /// changed. Only on standing room (asked per texel of <see cref="Arena.OnFloor"/>, cached), so
    /// nothing is painted on a wall top or over a chasm; a pit draws over it. A picture only.
    ///
    /// Never <c>UnityEngine.Random</c> - every mark is hashed from where it fell and a counter.
    /// Ichor is never red: the bestiary is creatures, and each kind bleeds its own colour.
    /// </summary>
    public class FightMarks : MonoBehaviour
    {
        const float Ppu = Art.FloorArt.PixelsPerUnit;

        [SerializeField] SpriteRenderer _sr;
        [SerializeField] Texture2D _tex;
        [SerializeField] int _w, _h, _count;
        [SerializeField] Vector2 _half;
        Color32[] _px;
        sbyte[] _floor;   // -1 unknown, 0 not standing room, 1 floor
        bool _dirty;

        static FightMarks _live;

        static FightMarks Live
        {
            get
            {
                if (_live == null) _live = FindAnyObjectByType<FightMarks>();
                return _live;
            }
        }

        // ------------------------------------------------------------------ lifecycle

        /// <summary>A new floor: wipe every mark and fit the canvas to the room (DressArena).</summary>
        public static void Begin(Vector2 half)
        {
            var m = Live;
            if (m == null)
            {
                var go = new GameObject("FightMarks");
                m = _live = go.AddComponent<FightMarks>();
            }
            m.Fit(half);
        }

        /// <summary>Off the arena (the hub): wipe and hide.</summary>
        public static void Hide()
        {
            var m = Live;
            if (m == null) return;
            if (m._px != null) System.Array.Clear(m._px, 0, m._px.Length);
            m._dirty = true;
            if (m._sr != null) m._sr.enabled = false;
        }

        void Fit(Vector2 half)
        {
            _half = half;
            int w = Mathf.CeilToInt(half.x * 2f * Ppu), h = Mathf.CeilToInt(half.y * 2f * Ppu);
            if (_tex == null || _w != w || _h != h)
            {
                if (_tex != null) Destroy(_tex);
                _w = w; _h = h;
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave, name = "fightmarks" };
                if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
                var s = Sprite.Create(_tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
                s.hideFlags = HideFlags.HideAndDontSave;
                _sr.sprite = s;
                _px = new Color32[w * h];
            }
            Buffer();
            System.Array.Clear(_px, 0, _px.Length);
            _floor = new sbyte[w * h];
            for (int i = 0; i < _floor.Length; i++) _floor[i] = -1;
            _count = 0;
            _sr.sortingOrder = SortingOrders.GroundDecal;
            _sr.enabled = true;
            // The canvas's left edge on the world's texel grid, so marks sit on the floor's own pixels.
            transform.position = new Vector3(Mathf.Round((-half.x + w / Ppu * 0.5f) * Ppu) / Ppu,
                                             Mathf.Round((-half.y + h / Ppu * 0.5f) * Ppu) / Ppu, 0f);
            _dirty = true;
        }

        /// <summary>The pixel buffer, re-read from the texture after a domain reload emptied it.</summary>
        Color32[] Buffer()
        {
            if (_px == null || _px.Length != _w * _h) _px = _tex != null ? _tex.GetPixels32() : new Color32[_w * _h];
            if (_floor == null || _floor.Length != _w * _h)
            {
                _floor = new sbyte[_w * _h];
                for (int i = 0; i < _floor.Length; i++) _floor[i] = -1;
            }
            return _px;
        }

        void LateUpdate()
        {
            if (!_dirty || _tex == null || _px == null) return;
            _dirty = false;
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }

        // ------------------------------------------------------------------ painting

        Vector2 Origin => (Vector2)transform.position - new Vector2(_w, _h) / (Ppu * 2f);

        Vector2Int TexelOf(Vector2 world)
        {
            var q = (world - Origin) * Ppu;
            return new Vector2Int(Mathf.FloorToInt(q.x), Mathf.FloorToInt(q.y));
        }

        bool Standing(int x, int y)
        {
            int i = y * _w + x;
            if (_floor[i] < 0)
                _floor[i] = (sbyte)(Arena.OnFloor(Origin + new Vector2(x + 0.5f, y + 0.5f) / Ppu) ? 1 : 0);
            return _floor[i] == 1;
        }

        /// <summary>Lay a colour over a texel ("over"), its alpha capped so marks never go solid.</summary>
        void Plot(int x, int y, Color c)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h || c.a <= 0f || !Standing(x, y)) return;
            int i = y * _w + x;
            Color d = _px[i];
            float a = c.a + d.a * (1f - c.a);
            Color o = a > 0f ? (c * c.a + d * d.a * (1f - c.a)) / a : c;
            o.a = Mathf.Min(a, Tuning.FightMarks.MaxAlpha);
            _px[i] = o;
            _dirty = true;
        }

        System.Random Rng(Vector2 at, int salt)
        {
            unchecked { return new System.Random(Mathf.RoundToInt(at.x * 97f) * 7919 + Mathf.RoundToInt(at.y * 89f) * 31 + salt * 104729 + _count++); }
        }

        static float Noise1(float a, int seed, int lobes)
        {
            // A smooth wobble round a circle: a few sines of random phase.
            float n = 0f;
            for (int k = 1; k <= lobes; k++)
            {
                float ph = ((seed * (k * 2654435761u)) % 1000u) / 1000f * Mathf.PI * 2f;
                n += Mathf.Sin(a * (k + 1) + ph) / k;
            }
            return n * 0.5f;
        }

        // ------------------------------------------------------------------ the marks

        /// <summary>A scorch: a sooty blot, blacker at the heart, streaked out where the blast ran
        /// further, ash flecks in it and loose soot round the edge.</summary>
        public static void Scorch(Vector2 at, float radius)
        {
            var m = Live;
            if (m == null || !m._sr.enabled) return;
            m.Buffer();
            m.DrawScorch(at, Mathf.Clamp(radius * Tuning.FightMarks.ScorchScale, 0.3f, Tuning.FightMarks.ScorchMaxRadius));
        }

        void DrawScorch(Vector2 at, float radius)
        {
            var rng = Rng(at, 1);
            int seed = rng.Next(1, 1 << 20);
            var c = TexelOf(at);
            float r = radius * Ppu;
            int ext = Mathf.CeilToInt(r * 1.5f);
            for (int dy = -ext; dy <= ext; dy++)
            for (int dx = -ext; dx <= ext; dx++)
            {
                float yy = dy / Tuning.FightMarks.GroundSquash;   // lying on the ground
                float d = Mathf.Sqrt(dx * dx + yy * yy);
                float ang = Mathf.Atan2(yy, dx);
                float edge = r * (0.82f + Noise1(ang, seed, 4) * 0.22f);
                // Streaks: a few angles where the blast ran on.
                float streak = Mathf.Pow(Mathf.Max(0f, Noise1(ang * 3f, seed + 7, 2)), 3f) * r * 0.9f;
                float t = d / edge;
                // Clumped (2x2 blocks), never lone texels - single specks read as noise on a pale floor.
                float h = Hash((c.x + dx) >> 1, (c.y + dy) >> 1, seed);
                Color col;
                if (t < 0.45f) col = Tuning.FightMarks.SootCore;
                else if (t < 0.8f) col = Tuning.FightMarks.SootMid;
                else if (t < 1f) col = h > 0.35f ? Tuning.FightMarks.SootEdge : Color.clear;
                else if (d < edge + streak) col = h > 0.55f ? Tuning.FightMarks.SootEdge : Color.clear;
                else if (t < 1.25f && h > 0.9f) col = Tuning.FightMarks.SootEdge;   // loose soot
                else continue;
                if (t < 0.8f && Hash(c.x + dx, c.y + dy, seed + 3) > 0.985f) col = Tuning.FightMarks.Ash;   // ash flecks
                Plot(c.x + dx, c.y + dy, col);
            }
        }

        /// <summary>Cracks where the ground was struck: a few jagged rays with a lit lip, forking.</summary>
        public static void Crack(Vector2 at, float size)
        {
            var m = Live;
            if (m == null || !m._sr.enabled) return;
            m.Buffer();
            m.DrawCrack(at, Mathf.Clamp(size, 0.3f, Tuning.FightMarks.CrackMaxSize));
        }

        void DrawCrack(Vector2 at, float size)
        {
            var rng = Rng(at, 2);
            var c = TexelOf(at);
            for (int y = -1; y <= 0; y++) for (int x = -1; x <= 1; x++) Plot(c.x + x, c.y + y, Tuning.FightMarks.CrackDark);
            int rays = 4 + rng.Next(3);
            float a0 = (float)rng.NextDouble() * Mathf.PI * 2f;
            int baseLen = Mathf.RoundToInt(size * Ppu);
            for (int r = 0; r < rays; r++)
            {
                float a = a0 + r * Mathf.PI * 2f / rays + ((float)rng.NextDouble() - 0.5f) * 0.7f;
                int len = baseLen / 2 + rng.Next(Mathf.Max(1, baseLen / 2));
                float x = c.x, y = c.y;
                for (int s = 0; s < len; s++)
                {
                    a += ((float)rng.NextDouble() - 0.5f) * 0.7f;
                    x += Mathf.Cos(a);
                    y += Mathf.Sin(a) * Tuning.FightMarks.GroundSquash;
                    int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
                    Plot(ix, iy + 1, Tuning.FightMarks.CrackLip);
                    Plot(ix, iy, Tuning.FightMarks.CrackDark);
                    if (s > 2 && rng.Next(6) == 0)
                    {
                        float b = a + (rng.Next(2) == 0 ? 0.9f : -0.9f);
                        float fx = x, fy = y;
                        for (int f = 0; f < 3; f++)
                        {
                            fx += Mathf.Cos(b); fy += Mathf.Sin(b) * Tuning.FightMarks.GroundSquash;
                            Plot(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy), Tuning.FightMarks.CrackDark);
                        }
                    }
                }
            }
        }

        /// <summary>Where a body fell: its kind's ichor in a pool with drops flung away from the
        /// blow (<paramref name="away"/>), or a Watcher's stone chips. A Brimstone Heart leaves
        /// its blast's scorch instead.</summary>
        public static void Death(Vector2 at, EnemyKind kind, Vector2 away, bool elite)
        {
            var m = Live;
            if (m == null || !m._sr.enabled || kind == EnemyKind.Bomb) return;
            m.Buffer();
            float s = elite ? 1.3f : 1f;
            if (kind == EnemyKind.Gargoyle) m.DrawChips(at, Tuning.FightMarks.ChipCount * s);
            else m.DrawIchor(at, IchorOf(kind), away, Tuning.FightMarks.PoolRadius * s, Tuning.FightMarks.DeathDrops);
        }

        /// <summary>A heavy blow (one that displaces): a spatter flung the way it was struck.</summary>
        public static void Spatter(Vector2 at, EnemyKind kind, Vector2 away)
        {
            var m = Live;
            if (m == null || !m._sr.enabled || kind == EnemyKind.Bomb) return;
            m.Buffer();
            if (kind == EnemyKind.Gargoyle) m.DrawChips(at, Tuning.FightMarks.ChipCount * 0.4f);
            else m.DrawIchor(at, IchorOf(kind), away, Tuning.FightMarks.SpatterRadius, Tuning.FightMarks.SpatterDrops);
        }

        void DrawIchor(Vector2 at, Color ink, Vector2 away, float radius, int drops)
        {
            var rng = Rng(at, 3);
            int seed = rng.Next(1, 1 << 20);
            var c = TexelOf(at);
            Color dark = Color.Lerp(ink, Color.black, 0.45f), wet = Color.Lerp(ink, Color.white, 0.35f);
            dark.a = ink.a; wet.a = ink.a;
            float r = radius * Ppu;
            int ext = Mathf.CeilToInt(r * 1.3f);
            for (int dy = -ext; dy <= ext; dy++)
            for (int dx = -ext; dx <= ext; dx++)
            {
                float yy = dy / Tuning.FightMarks.GroundSquash;
                float d = Mathf.Sqrt(dx * dx + yy * yy);
                float edge = r * (0.75f + Noise1(Mathf.Atan2(yy, dx), seed, 3) * 0.35f);
                if (d > edge) continue;
                Color col = d > edge - 1.2f ? dark : ink;
                // A wet sheen on the upper side of the pool, where the light lands.
                if (dy > 0 && d < edge * 0.5f && Hash(c.x + dx, c.y + dy, seed) > 0.8f) col = wet;
                Plot(c.x + dx, c.y + dy, col);
            }
            // Drops flung along the blow, smaller further out.
            Vector2 dir = away.sqrMagnitude > 1e-4f ? away.normalized : new Vector2(Mathf.Cos(seed), Mathf.Sin(seed));
            for (int i = 0; i < drops; i++)
            {
                float spread = ((float)rng.NextDouble() - 0.5f) * 1.3f;
                var dd = new Vector2(dir.x * Mathf.Cos(spread) - dir.y * Mathf.Sin(spread), dir.x * Mathf.Sin(spread) + dir.y * Mathf.Cos(spread));
                float dist = r * (1.1f + (float)rng.NextDouble() * 2.2f);
                int px = c.x + Mathf.RoundToInt(dd.x * dist), py = c.y + Mathf.RoundToInt(dd.y * dist * Tuning.FightMarks.GroundSquash);
                int size = dist < r * 2f && rng.Next(2) == 0 ? 2 : 1;
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) Plot(px + x, py - y, size > 1 && x == 0 && y == 0 ? ink : dark);
            }
        }

        void DrawChips(Vector2 at, float count)
        {
            var rng = Rng(at, 4);
            var c = TexelOf(at);
            int n = Mathf.Max(2, Mathf.RoundToInt(count));
            float spread = Tuning.FightMarks.ChipSpread * Ppu;
            for (int i = 0; i < n; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)rng.NextDouble()) * spread;
                int x0 = c.x + Mathf.RoundToInt(Mathf.Cos(a) * d), y0 = c.y + Mathf.RoundToInt(Mathf.Sin(a) * d * Tuning.FightMarks.GroundSquash);
                int w = 2 + rng.Next(2), h = 1 + rng.Next(2);
                for (int x = 0; x < w; x++) Plot(x0 + x, y0 - 1, Tuning.FightMarks.ChipShadow);   // its shadow under it
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
                    Plot(x0 + x, y0 + y, y == h - 1 ? Tuning.FightMarks.ChipLit : Tuning.FightMarks.Chip);
            }
            // Grit round them.
            for (int i = 0; i < n * 3; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = (float)rng.NextDouble() * spread * 1.3f;
                Plot(c.x + Mathf.RoundToInt(Mathf.Cos(a) * d), c.y + Mathf.RoundToInt(Mathf.Sin(a) * d * Tuning.FightMarks.GroundSquash), Tuning.FightMarks.ChipShadow);
            }
        }

        /// <summary>Each kind's ichor - none of them red.</summary>
        static Color IchorOf(EnemyKind kind) => kind switch
        {
            EnemyKind.Chaser => Tuning.FightMarks.IchorBrute,
            EnemyKind.Ranged => Tuning.FightMarks.IchorStinger,
            EnemyKind.Turret => Tuning.FightMarks.IchorEye,
            EnemyKind.Dasher => Tuning.FightMarks.IchorHound,
            EnemyKind.Booster => Tuning.FightMarks.IchorLion,
            EnemyKind.Bubbles => Tuning.FightMarks.IchorBell,
            EnemyKind.Mortar => Tuning.FightMarks.IchorSalamander,
            _ => Tuning.FightMarks.IchorBrute,
        };

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + (s + 1) * 982451653);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }

        // ------------------------------------------------------------------ hooks

        /// <summary>Wired beside the hit sounds (EnemyFactory): a heavy blow spatters.</summary>
        public static void AttachEnemy(Health hp, EnemyKind kind)
        {
            if (hp == null) return;
            hp.Damaged += info =>
            {
                if (hp == null || !info.Displaces || info.Tick || info.Price || info.Amount <= 0f) return;
                Vector2 at = hp.transform.position;
                Vector2 away = info.Knockback.sqrMagnitude > 1e-4f ? info.Knockback
                             : info.Source != null ? at - (Vector2)info.Source.transform.position : Vector2.zero;
                Spatter(at, kind, away);
            };
        }
    }
}
