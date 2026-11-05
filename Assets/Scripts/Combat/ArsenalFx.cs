using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// THE ARSENAL'S PICTURES (RELEASES.md): the shapes Guard, Mobility, Combo and Tremor Stance are
    /// drawn from, each in the caller's ramp, at the floor's texel scale - a fan, a spiral, a pillar,
    /// a falling slab, a band along a line (a wave, a wet sheen, a mud bank, a crack, a cord), spikes
    /// along a line or round a ring, a crescent and a cyclone that travel. Ramps run brightest first.
    ///
    /// Flat pictures sort on the floor (<see cref="SortingOrders.GroundFx"/>); standing ones take the
    /// caller's order - under the player's body when they rise where the player stands.
    /// </summary>
    public class ArsenalFx : ReleaseCanvas
    {
        public enum Band { Wave, Wet, Mud, Crack, Cord }
        enum Kind { Fan, Spiral, Pillar, Slab, Band, Spikes, SpikeRing, Crescent, Cyclone }

        [SerializeField] Kind _kind;
        [SerializeField] Band _band;
        [SerializeField] Color32[] _ramp;
        [SerializeField] Vector2 _from, _dir;   // texels from the centre; a unit direction
        [SerializeField] float _len, _half, _life, _travel, _h;
        [SerializeField] int _n, _seed;

        static int _serial;

        /// <summary>Mud, for Mudslide's bank (the mock's ramp).</summary>
        public static readonly Color32[] Mud =
        {
            new(201, 176, 122, 255), new(138, 106, 62, 255), new(107, 78, 44, 255), new(74, 52, 28, 255), new(46, 32, 16, 255),
        };

        static ArsenalFx New(Kind kind, Vector2 centre, int half, int below, int above, int order, Color32[] ramp, float life)
        {
            var f = Make<ArsenalFx>("arsenal." + kind, centre, half, below, above, order);
            f._kind = kind; f._ramp = ramp; f._life = life; f._seed = (_serial++ * 7919) & 0x7fff;
            return f;
        }

        /// <summary>One along a segment: centred on its middle, wide enough for it either way.</summary>
        static ArsenalFx Along(Kind kind, Vector2 from, Vector2 dir, float length, float halfWidth, int above, int order,
                               Color32[] ramp, float life)
        {
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            var mid = from + dir * length * 0.5f;
            int half = Mathf.CeilToInt((length * 0.5f + halfWidth) * Ppu) + 4;
            var f = New(kind, mid, half, half, half + above, order, ramp, life);
            f._from = (from - mid) * Ppu; f._dir = dir; f._len = length * Ppu; f._half = halfWidth * Ppu;
            return f;
        }

        // ---- the pictures ----

        /// <summary>A fan of the element thrown out <paramref name="length"/> along <paramref name="dir"/>,
        /// <paramref name="halfAngle"/> radians either side, flickering, fading.</summary>
        public static void Fan(Vector2 at, Color32[] ramp, Vector2 dir, float length, float halfAngle, float life = 0.4f)
        {
            int r = Mathf.CeilToInt(length * Ppu) + 2;
            var f = New(Kind.Fan, at, r, r, r, SortingOrders.GroundFx, ramp, life);
            f._dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right; f._len = length * Ppu; f._half = halfAngle;
        }

        /// <summary>Two arms spiralling out to <paramref name="radius"/>.</summary>
        public static void Spiral(Vector2 at, Color32[] ramp, float radius, float life = 0.55f)
        {
            int r = Mathf.CeilToInt(radius * Ppu) + 3;
            New(Kind.Spiral, at, r, r, r, SortingOrders.GroundFx, ramp, life)._len = radius * Ppu;
        }

        /// <summary>A column rising <paramref name="height"/> off <paramref name="foot"/> and sinking
        /// back - a pillar of fire, a geyser, a stone pillar.</summary>
        public static void Pillar(Vector2 foot, Color32[] ramp, float height, float halfWidth, int order, float life = 0.6f)
        {
            int hw = Mathf.CeilToInt(halfWidth * Ppu) + 2, up = Mathf.CeilToInt(height * Ppu) + 2;
            var f = New(Kind.Pillar, foot, hw + 2, 3, up, order, ramp, life);
            f._half = halfWidth * Ppu; f._h = height * Ppu;
        }

        /// <summary>A slab falling from <paramref name="height"/> onto <paramref name="foot"/>, landing,
        /// cracking and sinking - Monolith.</summary>
        public static void Slab(Vector2 foot, Color32[] ramp, float height, float halfWidth, int order, float life = 0.8f)
        {
            int hw = Mathf.CeilToInt(halfWidth * Ppu) + 2, up = Mathf.CeilToInt((height + halfWidth * 1.4f) * Ppu) + 2;
            var f = New(Kind.Slab, foot, hw + 2, 3, up, order, ramp, life);
            f._half = halfWidth * Ppu; f._h = height * Ppu;
        }

        /// <summary>A band running <paramref name="length"/> along <paramref name="dir"/> over
        /// <paramref name="travel"/> seconds, <paramref name="halfWidth"/> either side, held to
        /// <paramref name="life"/>: a rolling wave, a wet sheen, a mud bank, a crack, a cord.</summary>
        public static void Line(Band band, Vector2 from, Vector2 dir, float length, float halfWidth, Color32[] ramp,
                                float travel, float life, int order = int.MinValue)
        {
            int above = band == Band.Wave ? 12 : band == Band.Mud ? 16 : 2;
            var f = Along(Kind.Band, from, dir, length, halfWidth, above, order == int.MinValue ? SortingOrders.GroundFx : order, ramp, life);
            f._band = band; f._travel = travel;
        }

        /// <summary><paramref name="n"/> spikes bursting up along a line as its front passes them.</summary>
        public static void Spikes(Vector2 from, Vector2 dir, float length, int n, Color32[] ramp, float travel, int order)
        {
            var f = Along(Kind.Spikes, from, dir, length, 0.3f, 24, order, ramp, travel + 0.5f);
            f._n = n; f._travel = travel;
        }

        /// <summary><paramref name="n"/> spikes bursting up round a ring of <paramref name="radius"/> - Stone Thorns.</summary>
        public static void SpikeRing(Vector2 at, float radius, int n, Color32[] ramp, int order)
        {
            int r = Mathf.CeilToInt(radius * Ppu) + 4;
            var f = New(Kind.SpikeRing, at, r, r, r + 24, order, ramp, 0.65f);
            f._len = radius * Ppu; f._n = n;
        }

        /// <summary>A crescent of the element flying <paramref name="length"/> along <paramref name="dir"/>.</summary>
        public static void Crescent(Vector2 from, Vector2 dir, float length, float radius, Color32[] ramp, float travel)
        {
            var f = Along(Kind.Crescent, from, dir, length, radius, 4, SortingOrders.GroundFx, ramp, travel + 0.12f);
            f._travel = travel; f._h = radius * Ppu;
        }

        /// <summary>A cyclone travelling <paramref name="length"/> along <paramref name="dir"/>, turning.</summary>
        public static void Cyclone(Vector2 from, Vector2 dir, float length, Color32[] ramp, float travel, int order)
        {
            var f = Along(Kind.Cyclone, from, dir, length, 0.7f, 50, order, ramp, travel + 0.25f);
            f._travel = travel;
        }

        // ---- drawing ----

        float Fade(float over = 0.25f) => 1f - Mathf.Clamp01((Age - (_life - over)) / over);
        float Hash(int a, int b) => FireArt.Hash(a, b, _seed);

        protected override bool Draw()
        {
            if (Age >= _life) return false;
            switch (_kind)
            {
                case Kind.Fan: DrawFan(); break;
                case Kind.Spiral: DrawSpiral(); break;
                case Kind.Pillar: DrawPillar(); break;
                case Kind.Slab: DrawSlab(); break;
                case Kind.Band: DrawBand(); break;
                case Kind.Spikes: DrawSpikes(); break;
                case Kind.SpikeRing: DrawSpikeRing(); break;
                case Kind.Crescent: DrawCrescent(); break;
                case Kind.Cyclone: DrawCyclone(); break;
            }
            return true;
        }

        void DrawFan()
        {
            float grow = EaseOut(Age / (_life * 0.45f)), r = grow * _len, fade = Fade(_life * 0.5f);
            float face = Mathf.Atan2(_dir.y, _dir.x);
            Ring(0, 0, 2f, r, (x, y, d, a) =>
            {
                float off = Mathf.Abs(Mathf.DeltaAngle(a * Mathf.Rad2Deg, face * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                if (off > _half) return;
                float edge = off / _half, u = d / Mathf.Max(1f, _len);
                float flick = Hash(x / 2, y / 2 + Mathf.FloorToInt(Age * 14f));
                if (flick < 0.05f + u * 0.3f + edge * edge * 0.3f) return;
                // Hot at the root, cooling out to the tips and the edges.
                Over(x, y, At(_ramp, 0.05f + u * 0.5f + edge * 0.2f), fade * (1f - u * 0.3f));
            });
        }

        void DrawSpiral()
        {
            float grow = EaseOut(Age / (_life * 0.6f)), fade = Fade(_life * 0.4f);
            for (int arm = 0; arm < 2; arm++)
                for (float s = 2f; s <= grow * _len; s += 1.2f)
                {
                    float a = s / _len * Mathf.PI * 2.4f + arm * Mathf.PI + Age * 5f;
                    float u = s / _len;
                    Dab(Mathf.Cos(a) * s, Mathf.Sin(a) * s * 0.7f, 1.6f - u * 0.6f, At(_ramp, 0.05f + u * 0.6f), fade);
                }
        }

        void DrawPillar()
        {
            float rise = EaseOut(Age / 0.14f), sink = Mathf.Clamp01((Age - _life * 0.55f) / (_life * 0.45f));
            int top = Mathf.RoundToInt(_h * rise * (1f - sink)), hw = Mathf.RoundToInt(_half);
            for (int y = 0; y <= top; y++)
            {
                float u = y / Mathf.Max(1f, _h);
                int wob = Mathf.RoundToInt((Hash(y / 3, Mathf.FloorToInt(Age * 16f)) - 0.5f) * 2f);
                int w = Mathf.Max(1, hw - (y > top - 4 ? (y - (top - 4)) : 0));
                for (int x = -w; x <= w; x++)
                {
                    float edge = Mathf.Abs(x) / (float)Mathf.Max(1, w);
                    Over(x + wob, y, At(_ramp, 0.05f + edge * 0.55f + u * 0.2f));
                }
            }
            // Its foot on the floor, wider than the column.
            Ring(0, 0, 0f, _half * 1.6f, (x, y, d, a) => { if (Mathf.Abs(y) <= 2) Over(x, y, At(_ramp, 0.5f), 0.5f * (1f - sink)); });
        }

        void DrawSlab()
        {
            float fall = Mathf.Clamp01(Age / 0.2f), sink = Mathf.Clamp01((Age - _life * 0.6f) / (_life * 0.4f));
            int hw = Mathf.RoundToInt(_half), hh = Mathf.RoundToInt(_half * 1.4f);
            int y0 = Mathf.RoundToInt(_h * (1f - fall * fall)) - Mathf.RoundToInt(sink * hh);
            for (int y = Mathf.Max(0, y0); y <= y0 + hh; y++)
                for (int x = -hw; x <= hw; x++)
                {
                    bool outline = x == -hw || x == hw || y == y0 + hh || y == y0;
                    bool lit = y >= y0 + hh - 3 || x <= -hw + 2;   // the top face and the lit side
                    bool crack = fall >= 1f && Mathf.Abs(x - Mathf.RoundToInt(Mathf.Sin(y * 0.5f) * 2f)) < 1 && Age > 0.25f;
                    Over(x, y, At(_ramp, outline || crack ? 0.95f : lit ? 0.1f : 0.5f + (x + hw) / (float)(hw * 2) * 0.25f));
                }
            if (fall < 1f) return;
            // The landing's shadow-dark crater ring.
            Ring(0, 0, _half, _half * 1.5f, (x, y, d, a) => { if (Mathf.Abs(y) < _half * 0.5f) Over(x, y, At(_ramp, 0.85f), 0.6f * (1f - sink)); });
        }

        void DrawBand()
        {
            float front = (_travel > 0f ? EaseOut(Age / _travel) : 1f) * _len, fade = Fade(Mathf.Min(0.35f, _life * 0.4f));
            Vector2 side = new(-_dir.y, _dir.x);
            // Standing bands paint back to front (higher on screen first), or a bank running up the
            // screen lays its far crest over its near face and reads as a plank.
            bool backFirst = (_band == Band.Mud || _band == Band.Wave) && _dir.y > 0f;
            int steps = Mathf.FloorToInt(front / 0.8f);
            for (int i = 0; i <= steps; i++)
            {
                float s = (backFirst ? steps - i : i) * 0.8f;
                var c = _from + _dir * s;
                float tail = (front - s) / Mathf.Max(1f, _len);
                switch (_band)
                {
                    case Band.Wave:
                    {
                        bool crest = front - s < 10f && Age < _travel + 0.15f;
                        for (float w = -_half; w <= _half; w += 1f)
                        {
                            var p = c + side * w;
                            float bow = 1f - (w / _half) * (w / _half);
                            if (crest)
                            {
                                int lift = Mathf.RoundToInt(bow * 9f * (1f - (front - s) / 10f));
                                for (int y = 0; y <= lift; y++) Over(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y) + y, At(_ramp, y >= lift - 1 ? 0f : 0.3f + y * 0.02f), fade);
                            }
                            else Over(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), At(_ramp, 0.45f + tail * 0.3f), 0.45f * fade * (1f - tail));
                        }
                        break;
                    }
                    case Band.Wet:
                        for (float w = -_half; w <= _half; w += 1f)
                        {
                            var p = c + side * w;
                            int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                            float glint = Hash(x / 2, y + Mathf.FloorToInt(Age * 3f));
                            Over(x, y, At(_ramp, glint > 0.93f ? 0f : 0.6f), (glint > 0.93f ? 0.7f : 0.28f) * fade);
                        }
                        break;
                    case Band.Mud:
                    {
                        // A lumpy bank of wet mud: rounded across, uneven along, a pale wet crest with
                        // glints, the face darkening to a black foot - so it reads off a brown floor.
                        float along = s / Mathf.Max(1f, _len);
                        float ends = Mathf.Clamp01(Mathf.Min(along, 1f - along) * 6f);
                        float lump = 0.7f + 0.3f * Mathf.Sin(s * 0.45f + _seed) + (Hash(Mathf.FloorToInt(s / 4f), 2) - 0.5f) * 0.3f;
                        float grow = Mathf.Clamp01(Age / 0.15f);
                        for (float w = -_half; w <= _half; w += 1f)
                        {
                            var p = c + side * w;
                            float across = Mathf.Abs(w) / Mathf.Max(1f, _half * (0.65f + 0.35f * lump));
                            if (across > 1f) continue;   // an uneven edge, not a ruled plank
                            int hgt = Mathf.RoundToInt(Mathf.Sqrt(Mathf.Max(0f, 1f - across * across)) * 13f * lump * ends * grow);
                            int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                            Over(x, y - 1, At(_ramp, 0.99f), fade);
                            for (int k = 0; k <= hgt; k++)
                            {
                                float u = hgt > 0 ? k / (float)hgt : 0f;
                                bool glint = k == hgt && Hash(x, 9) > 0.8f;
                                Over(x, y + k, At(_ramp, glint ? 0f : u > 0.8f ? 0.3f : u > 0.45f ? 0.55f : u > 0.15f ? 0.75f : 0.95f), fade);
                            }
                        }
                        break;
                    }
                    case Band.Crack:
                    {
                        float wob = Mathf.Sin(s * 0.35f + _seed) * 1.6f + (Hash(Mathf.FloorToInt(s / 3f), 1) - 0.5f) * 2f;
                        var p = c + side * wob;
                        int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                        int width = Mathf.Max(1, Mathf.RoundToInt(_half * 0.25f));
                        for (int k = -width; k <= width; k++) Over(x, y + k, At(_ramp, 0.95f), fade);
                        Over(x, y + width + 1, At(_ramp, 0.2f), 0.7f * fade);
                        break;
                    }
                    case Band.Cord:
                    {
                        float wob = Mathf.Sin(s * 0.3f + Age * 20f) * 1.5f * (1f - s / Mathf.Max(1f, _len));
                        var p = c + side * wob;
                        Dab(p.x, p.y, 1f, At(_ramp, 0.2f), fade);
                        break;
                    }
                }
            }
        }

        void Spike(float x, float y, float rise, int seed)
        {
            int h = Mathf.RoundToInt((14f + Hash(seed, 7) * 7f) * rise);
            if (h <= 0) return;
            for (int k = 0; k < h; k++)
            {
                int w = Mathf.RoundToInt((1f - k / (float)h) * 4f);
                for (int dx = -w; dx <= w; dx++)
                {
                    // Lit left face, shaded right, a dark outline and a pale tip - so it stands off any floor.
                    bool rim = dx == -w || dx == w;
                    Over(Mathf.RoundToInt(x) + dx, Mathf.RoundToInt(y) + k,
                         At(_ramp, k >= h - 2 ? 0.02f : rim ? 0.95f : dx < 0 ? 0.25f : 0.6f));
                }
            }
            // Broken ground at its foot.
            for (int dx = -5; dx <= 5; dx++) Over(Mathf.RoundToInt(x) + dx, Mathf.RoundToInt(y) - 1, At(_ramp, 0.85f), 0.7f * rise);
        }

        static float Rise(float since) => since < 0f ? 0f : since < 0.08f ? since / 0.08f : since < 0.35f ? 1f : Mathf.Max(0f, 1f - (since - 0.35f) / 0.2f);

        void DrawSpikes()
        {
            for (int i = 0; i < _n; i++)
            {
                float s = (i + 0.5f) / _n * _len;
                var p = _from + _dir * s;
                Spike(p.x, p.y, Rise(Age - s / Mathf.Max(1f, _len) * _travel), i);
            }
        }

        void DrawSpikeRing()
        {
            for (int i = 0; i < _n; i++)
            {
                float a = (i + Hash(i, 3) * 0.4f) / _n * Mathf.PI * 2f;
                Spike(Mathf.Cos(a) * _len, Mathf.Sin(a) * _len * 0.7f, Rise(Age - Hash(i, 5) * 0.06f), i);
            }
        }

        void DrawCrescent()
        {
            float u = Mathf.Clamp01(Age / _travel), fade = Fade(0.12f);
            var c = _from + _dir * (EaseOut(u) * _len);
            float face = Mathf.Atan2(_dir.y, _dir.x);
            for (float a = -1.2f; a <= 1.2f; a += 0.06f)
            {
                float thick = (1f - Mathf.Abs(a) / 1.2f) * 2.2f + 0.6f;
                var p = c + new Vector2(Mathf.Cos(face + a), Mathf.Sin(face + a)) * _h - _dir * _h * 0.6f;
                Dab(p.x, p.y, thick * 0.5f, At(_ramp, Mathf.Abs(a) > 0.9f ? 0.5f : 0.05f), fade);
                // A trail behind it, thinning.
                var t = p - _dir * 5f;
                Over(Mathf.RoundToInt(t.x), Mathf.RoundToInt(t.y), At(_ramp, 0.6f), 0.4f * fade);
            }
        }

        void DrawCyclone()
        {
            float u = Mathf.Clamp01(Age / _travel), fade = Fade(0.25f);
            var c = _from + _dir * (u * _len);
            for (int k = 0; k < 9; k++)
            {
                float rx = 4f + k * 2.3f, ry = rx * 0.35f, cy = c.y + k * 5f;
                float spin = Age * 14f + k * 0.8f;
                for (float a = 0f; a < Mathf.PI * 2f; a += 0.12f)
                {
                    if (Mathf.Repeat(a + spin, 1.1f) > 0.7f) continue;   // dashed, so it reads as turning
                    float x = c.x + Mathf.Cos(a) * rx, y = cy + Mathf.Sin(a) * ry;
                    bool back = Mathf.Sin(a) > 0f;
                    Dab(x, y, back ? 0.6f : 1f, At(_ramp, back ? 0.55f : 0.05f + k * 0.04f), fade * (back ? 0.5f : 1f));
                }
            }
        }
    }
}
