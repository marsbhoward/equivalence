using UnityEngine;
using T = Convergence.Core.Tuning.ElementArtFx;

namespace Convergence.Combat
{
    /// <summary>
    /// The soft marks the pictures are painted with: off the texel grid (the user's pick, 2026-11-22 -
    /// weapon arts only; basics stay on the grid). The canvases are bilinear at a density where one
    /// texel is about one screen pixel, and every mark is anti-aliased. Sizes are given in WORLD units
    /// (see <see cref="Px"/>), so the same picture lands the same size on the floor's coarser canvas.
    /// </summary>
    public partial class ElementArtFx
    {
        /// <summary>A size in the mock's screen pixels (1080 tall, the arena camera) as world units, at this figure's scale.</summary>
        float Px(float px) => px / T.MockPxPerUnit * _scale / Core.Tuning.Player.ArenaVisualScale;

        // Coverage is gathered as a max over a shape's parts (so a stroke's joints don't double up),
        // then laid down once in one colour. One buffer for every picture (they paint one at a time on
        // the main thread); lazily made, as a domain reload empties statics.
        static float[] _cover;
        static int _x0, _y0, _x1, _y1;

        static Vector2 ToCanvas(Sheet c, Vector2 world)
            => new(world.x * c.Ppu - c.Cx + c.Size * 0.5f, world.y * c.Ppu - c.Cy + c.Size * 0.5f);

        void Open(Sheet c)
        {
            if (_cover == null || _cover.Length < c.Size * c.Size) _cover = new float[c.Size * c.Size];
            _x0 = c.Size; _y0 = c.Size; _x1 = -1; _y1 = -1;
        }

        /// <summary>A capsule from <paramref name="a"/> to <paramref name="b"/> (world), <paramref name="width"/> across (world);
        /// <paramref name="hard"/> 1 = a crisp anti-aliased edge, 0 = soft all the way in.</summary>
        void CoverLine(Sheet c, Vector2 a, Vector2 b, float width, float hard = 1f, float squash = 1f)
        {
            Vector2 p = ToCanvas(c, a), q = ToCanvas(c, b);
            float r = width * c.Ppu * 0.5f, k = 1f;
            if (r < 0.6f) { k = r / 0.6f; r = 0.6f; }   // thinner than a texel: fainter, not thinner
            float fall = Mathf.Max(0.75f, r * (1f - hard));
            int xa = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p.x, q.x) - r - 1)), xb = Mathf.Min(c.Size - 1, Mathf.CeilToInt(Mathf.Max(p.x, q.x) + r + 1));
            float ry = r * squash;
            int ya = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(p.y, q.y) - ry - 1)), yb = Mathf.Min(c.Size - 1, Mathf.CeilToInt(Mathf.Max(p.y, q.y) + ry + 1));
            if (xa > xb || ya > yb) return;
            var d = q - p; float dd = Mathf.Max(1e-6f, d.sqrMagnitude);
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    float px = x + 0.5f - p.x, py = y + 0.5f - p.y;
                    float t = Mathf.Clamp01((px * d.x + py * d.y) / dd);
                    float ex = px - d.x * t, ey = (py - d.y * t) / squash;
                    float dist = Mathf.Sqrt(ex * ex + ey * ey);
                    float cov = Mathf.Clamp01((r - dist) / fall);
                    if (hard < 0.5f) cov *= cov;
                    cov *= k;
                    if (cov <= 0.004f) continue;
                    int i = y * c.Size + x;
                    if (cov > _cover[i]) _cover[i] = cov;
                    if (x < _x0) _x0 = x; if (x > _x1) _x1 = x; if (y < _y0) _y0 = y; if (y > _y1) _y1 = y;
                }
        }

        void CoverBlob(Sheet c, Vector2 at, float radius, float hard = 1f, float squash = 1f)
            => CoverLine(c, at, at, radius * 2f, hard, squash);

        /// <summary>Lay what was covered down in one colour: over, or added (light).</summary>
        void Flush(Sheet c, Color32 col, float alpha, bool add = false)
        {
            if (_x1 < 0) return;
            for (int y = _y0; y <= _y1; y++)
                for (int x = _x0; x <= _x1; x++)
                {
                    int i = y * c.Size + x;
                    float cov = _cover[i];
                    if (cov <= 0f) continue;
                    _cover[i] = 0f;
                    Put(c, i, col.r, col.g, col.b, cov * alpha, add);
                }
            _x0 = c.Size; _y0 = c.Size; _x1 = -1; _y1 = -1;
        }

        static void Put(Sheet c, int i, float r, float g, float b, float a, bool add)
        {
            if (a <= 0.004f) return;
            a = Mathf.Min(1f, a);
            var d = c.Px[i];
            float da = d.a / 255f, oa;
            float pr, pg, pb;
            if (add)
            {
                // light adds: premultiplied sum, back to straight alpha
                oa = Mathf.Min(1f, da + a);
                pr = d.r * da + r * a; pg = d.g * da + g * a; pb = d.b * da + b * a;
            }
            else
            {
                oa = a + da * (1f - a);
                pr = r * a + d.r * da * (1f - a); pg = g * a + d.g * da * (1f - a); pb = b * a + d.b * da * (1f - a);
            }
            float inv = 1f / Mathf.Max(1e-4f, oa);
            c.Px[i] = new Color32((byte)Mathf.Min(255f, pr * inv), (byte)Mathf.Min(255f, pg * inv), (byte)Mathf.Min(255f, pb * inv), (byte)(oa * 255f));
            c.Drawn = true;
        }

        // ------------------------------------------------------------------ marks

        void Blob(Sheet c, Vector2 at, float radius, Color32 col, float alpha, float hard = 1f, bool add = false, float squash = 1f)
        {
            if (alpha <= 0.01f) return;
            Open(c); CoverBlob(c, at, radius, hard, squash); Flush(c, col, alpha, add);
        }

        void Line(Sheet c, Vector2 a, Vector2 b, float width, Color32 col, float alpha, float hard = 1f, bool add = false)
        {
            if (alpha <= 0.01f) return;
            Open(c); CoverLine(c, a, b, width, hard); Flush(c, col, alpha, add);
        }

        /// <summary>A flame tongue standing on <paramref name="at"/>: <paramref name="w"/> wide at its root,
        /// <paramref name="h"/> tall, rising screen-up and leaning <paramref name="lean"/> at the tip - an orange
        /// body, a yellow heart and a white root, each fading to its tip. Light: it adds.</summary>
        void Tongue(Sheet c, Vector2 at, float w, float h, float lean, float alpha)
        {
            if (alpha <= 0.01f || h * c.Ppu < 1f) return;
            Vector2 b = ToCanvas(c, at);
            float W = w * c.Ppu, Hh = h * c.Ppu, L = lean * c.Ppu;
            int xa = Mathf.Max(0, Mathf.FloorToInt(b.x - W - Mathf.Abs(L) - 1)), xb = Mathf.Min(c.Size - 1, Mathf.CeilToInt(b.x + W + Mathf.Abs(L) + 1));
            int ya = Mathf.Max(0, Mathf.FloorToInt(b.y - W * 0.5f - 1)), yb = Mathf.Min(c.Size - 1, Mathf.CeilToInt(b.y + Hh + 1));
            var fire = T.FlameBody; var heart = T.FlameHeart; var root = T.FlameRoot;
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    float py = y + 0.5f - b.y;
                    float v = py / Hh;   // 0 root .. 1 tip
                    if (v > 1f) continue;
                    float centre = b.x + L * Mathf.Max(0f, v) * Mathf.Max(0f, v);
                    float ax = Mathf.Abs(x + 0.5f - centre);
                    int i = y * c.Size + x;
                    // three layers: body (full), heart (0.62 wide, 0.78 tall), root (0.3 wide, 0.46 tall)
                    Layer(c, i, ax, v, W * 0.5f, fire, 0.95f, alpha);
                    Layer(c, i, ax, v / 0.78f, W * 0.31f, heart, 1f, alpha);
                    Layer(c, i, ax, v / 0.46f, W * 0.15f, root, 1f, alpha);
                }
        }

        static void Layer(Sheet c, int i, float ax, float v, float half, Color32 col, float a0, float alpha)
        {
            if (v > 1f) return;
            // a teardrop: round under the root, widest just above it, to a point at the tip
            float hw = v < 0f ? half * Mathf.Sqrt(Mathf.Max(0f, 1f - (v / -0.45f) * (v / -0.45f)))
                              : half * Mathf.Pow(1f - v, 0.75f) * Mathf.Min(1f, 0.75f + v * 1.5f);
            if (hw <= 0f) return;
            float edge = Mathf.Clamp01((hw - ax) / Mathf.Max(0.75f, hw * 0.35f));
            if (edge <= 0f) return;
            // The game blends where the mock added light, so a tongue holds its strength further up
            // before it thins: brightest at the root, most of the way, then gone at the tip.
            float fade = v < 0f ? 1f : 1f - v * v;
            Put(c, i, col.r, col.g, col.b, edge * fade * a0 * alpha, true);
        }

        /// <summary>A stone: a six-sided lump <paramref name="r"/> across (world), lit from above with a dark rim.</summary>
        void Stone(Sheet c, Vector2 at, float r, int seed, float alpha, float rot)
        {
            if (alpha <= 0.01f) return;
            Vector2 p = ToCanvas(c, at);
            float R = r * c.Ppu, ow = Mathf.Max(1.3f, R * 0.25f);
            int xa = Mathf.Max(0, Mathf.FloorToInt(p.x - R * 1.2f - 1)), xb = Mathf.Min(c.Size - 1, Mathf.CeilToInt(p.x + R * 1.2f + 1));
            int ya = Mathf.Max(0, Mathf.FloorToInt(p.y - R * 1.1f - 1)), yb = Mathf.Min(c.Size - 1, Mathf.CeilToInt(p.y + R * 1.1f + 1));
            float tone = 0.85f + 0.3f * H(seed, 90);
            var s = Core.Tuning.ReleaseFx.Stone;
            for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    float dx = x + 0.5f - p.x, dy = (y + 0.5f - p.y) / 0.85f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float th = Mathf.Repeat(Mathf.Atan2(dy, dx) - rot, Mathf.PI * 2f) / (Mathf.PI * 2f) * 6f;
                    int k0 = Mathf.FloorToInt(th) % 6, k1 = (k0 + 1) % 6;
                    float rr = R * Mathf.Lerp(0.72f + 0.4f * H(seed, 91 + k0), 0.72f + 0.4f * H(seed, 91 + k1), th - Mathf.Floor(th));
                    float cov = Mathf.Clamp01(rr + 0.5f - d);
                    if (cov <= 0f) continue;
                    int i = y * c.Size + x;
                    if (d > rr - ow) { Put(c, i, 18f, 14f, 10f, cov * 0.95f * alpha, false); continue; }
                    // lit from above: pale at the top, dark below, a glint up and left
                    float up = Mathf.Clamp01(0.5f + dy / (2f * R));
                    var lo = s[3]; var hi = s[1];
                    float cr = Mathf.Lerp(lo.r, hi.r, up) * tone, cg = Mathf.Lerp(lo.g, hi.g, up) * tone, cb = Mathf.Lerp(lo.b, hi.b, up) * tone;
                    float gx = (dx + R * 0.2f) / (R * 0.38f), gy = (dy - R * 0.4f) / (R * 0.2f);
                    if (gx * gx + gy * gy < 1f) { cr = s[0].r; cg = s[0].g; cb = s[0].b; }
                    Put(c, i, cr, cg, cb, cov * alpha, false);
                }
        }

        /// <summary>A puff of dust on the floor, spreading as it thins.</summary>
        void Dust(Sheet c, Vector2 at, float age, float life, float size)
        {
            if (age < 0f || age > life) return;
            float u = age / life, r = size * (0.4f + u * 1.1f);
            Blob(c, at, r * 1.3f, T.DustColour, 0.45f * (1f - u), 0f, false, 0.55f);
        }

        /// <summary>Where a blade was <paramref name="f"/> of the way between two cuts: it turns round its
        /// root, so the path is sampled by angle (a quarter turn in two frames is not a straight line).</summary>
        static Vector2 Swept(Cut a, Cut b, float f, float u)
        {
            var root = Vector2.Lerp(a.Pivot, b.Pivot, f);
            if (a.Vec.sqrMagnitude < 1e-8f || b.Vec.sqrMagnitude < 1e-8f) return root + Vector2.Lerp(a.Vec, b.Vec, f) * u;
            float a0 = Mathf.Atan2(a.Vec.y, a.Vec.x), da = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, Mathf.Atan2(b.Vec.y, b.Vec.x) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float ang = a0 + da * f, len = Mathf.Lerp(a.Vec.magnitude, b.Vec.magnitude, f) * u;
            return root + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * len;
        }

        static Color32 Heat(float u)   // 0 hot .. 1 cooled: white, gold, orange, red
        {
            var s = T.SparkRamp;
            float x = Mathf.Clamp(u, 0f, 0.999f) * (s.Length - 1);
            int i = Mathf.FloorToInt(x);
            return Color32.Lerp(s[i], s[i + 1], x - i);
        }
    }
}
