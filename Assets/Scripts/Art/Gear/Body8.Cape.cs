using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // THE LIVING CAPE (the Hellspawn Cape, approved motion mock 2026-11-17): the baked cape warped texel by texel as it
    // is drawn. Standing, it BREATHES (slow waves down the cloth, the lower cape rising and falling), SURGES open on
    // its own twice a loop, and its hem WRITHES, the points reaching and drawing back each on its own beat. Moving, it
    // streams on the stride. A swing LASHES it out into points, a parry WRAPS it in round the body and lifts it, a dash
    // BILLOWS it back. Every wave is a whole number of turns of one clock, so the standing loop repeats exactly
    // (LoopFrames pictures a direction) and the moving one rides the stride's own frames; the one-shots draw uncached.
    public static partial class Body8
    {
        /// <summary>What the living cape is drawn from this picture (set by the rig before each draw).</summary>
        public struct LivingCape
        {
            public bool On, Moving;
            /// <summary>The clock: one turn a standing loop, or one a stride (radians).</summary>
            public float Angle;
            /// <summary>The one-shots, 0..1: a swing's lash, a parry's wrap, a dash's billow.</summary>
            public float Lash, Wrap, Dash;
            /// <summary>Opposite the dash, in the drawn picture (x across it, y up the screen): the way a dash shoots
            /// the cape out.</summary>
            public Vector2 DashAway;
        }

        public static LivingCape Cape;

        /// <summary>The standing loop: seconds, and pictures in it (a picture a tenth of a second).</summary>
        public const float LoopSeconds = 8.4f;
        public const int LoopFrames = 84;
        /// <summary>Rows below the baked hem the points may reach into.</summary>
        const float LiveRoom = 8f;
        /// <summary>Columns either side of the baked view a row may be drawn into: the most the cape trails (a dash
        /// on a surge on a lash, 32) with its wave.</summary>
        const int LiveReach = 38;
        /// <summary>The bottom of the cape the points are drawn out of (texels), and how far they reach.</summary>
        const float PointSpan = 9f, ReachLow = 1.5f, ReachHigh = 4.5f;
        /// <summary>A dash swings the whole cape out about its top, opposite the dash: up to this angle from hanging
        /// (degrees; 90 = straight out, level with the ground - the user's "almost perpendicular").</summary>
        const float DashSwing = 78f;
        /// <summary>The narrowest the swung cape is foreshortened to on the screen (share of its length), so a cape
        /// pointing almost into the screen still shows as cloth.</summary>
        const float DashLeast = 0.22f;
        /// <summary>How far the sideways turn must carry a texel (texels) for it to give way to the body it crosses.</summary>
        const float MovedFrom = 2f;
        /// <summary>How far down from its top the cape's line takes to turn to the dash's full swing (texels): the
        /// tether at the shoulders.</summary>
        const float TetherSpan = 14f;
        /// <summary>How much wider a dash into or out of the screen fans the cape at its hem (share): thrown up behind a
        /// figure coming at the camera, and toward it behind one going away.</summary>
        const float DashFanUp = 0.45f, DashFanDown = 0.2f;
        /// <summary>Swung out level across the screen the cape's face tips up to the camera: drawn this much thicker.</summary>
        const float DashFace = 1.3f;
        /// <summary>The top of the cape held still (share of its length): the waves and the billow begin below it, so
        /// the cloth meeting the collar never moves against it.</summary>
        const float Pinned = 0.1f;
        /// <summary>How far out from the back the living cape sits (texels of depth), steady: what its billow's swell
        /// used to give it, without the flicker.</summary>
        const float LiveDepth = 4f;

        /// <summary>Whether a dash is swinging the cape this picture (the wider canvas, the extra rows).</summary>
        public static bool CapeSwung => Cape.On && Cape.Dash > 0.001f;

        static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        /// <summary>The slow breath (0..1) and the surge (0..1, twice a loop, standing only).</summary>
        static void Breath(out float b, out float surge)
        {
            if (Cape.Moving) { b = 0f; surge = 0f; return; }
            b = 0.5f + 0.5f * Mathf.Sin(Cape.Angle);
            float sp = Mathf.Repeat(2f * Cape.Angle / (2f * Mathf.PI), 1f);
            surge = sp < 0.12f ? sp / 0.12f : Mathf.Max(0f, 1f - (sp - 0.12f) / 0.3f);
            surge = Smooth01(surge);
        }

        /// <summary>How far down the cape a height is: 0 at the shoulders, 1 at the hem.</summary>
        static float Down(float hb) => Mathf.Clamp01(((CapeTop - hb) / (CapeTop - CapeHem) - Pinned) / (1f - Pinned));

        /// <summary>A row's billow: back along the facing (the cape swelling away), and up on the screen (its lower part
        /// rising as it swells toward the camera - what a flat picture cannot otherwise show). A dash is not here: it
        /// swings the whole cape (<see cref="Swing"/>).</summary>
        static (float trail, float lift) LiveRow(float hb, float rightX)
        {
            Breath(out float b, out float surge);
            float k = Down(hb), k2 = k * k;
            float trail = (3f * b + 8f * surge + 5f * Cape.Lash) * k2;
            float lift = (4f * b + 5f * surge + 9f * Cape.Wrap) * k2;
            return (trail, lift);
        }

        /// <summary>A dash's swing (the user's calls, 2026-11-17: opposite the dash, almost level with the ground, and
        /// "still a cape": its top tethered to the shoulders). Drawn FORWARD from the baked cape's own texels: each is
        /// carried along the cape's line, which hangs straight at the top and turns to the full swing over
        /// <see cref="TetherSpan"/> - across the screen for the sideways share (the cloth thickened as its face tips up to
        /// the camera), and into or out of it for the up-and-down share (foreshortened past the pinned top: thrown up
        /// behind a figure dashing at the camera, folding back over its own top; swung toward the camera behind one
        /// dashing away). Cloth the turn carries across the body goes under it where the cape hangs behind the figure.</summary>
        static void LiveSwung(Body8Art.Piece piece, Body8Art.View v, int id, float lift, float c, float lean, Vector3 f,
                              Dictionary<char, Color> suit)
        {
            var a = Cape.DashAway;
            float w = Smooth01(Cape.Dash) * DashSwing * Mathf.Deg2Rad;
            float th = w * Mathf.Abs(a.x), ph = w * Mathf.Abs(a.y), sx = Mathf.Sign(a.x);
            float m = Mathf.Cos(ph) - Mathf.Sign(a.y) * Locomotion8.DepthY * Mathf.Sin(ph);
            if (Mathf.Abs(m) < DashLeast) m = m < 0f ? -DashLeast : DashLeast;
            float d0 = Pinned * (CapeTop - CapeHem), mid = v.X0 + v.W * 0.5f;
            bool under = f.z > -0.3f;
            // the cape's line, a texel at a time down from its top: where it is (across, down) and its turn
            int L = Mathf.CeilToInt(CapeTop - (Body8Art.Top - v.Y0 - v.H)) + 2;
            if (_line == null || _line.Length < L + 1) { _line = new Vector2[L + 64]; _turn = new float[L + 64]; }
            _line[0] = Vector2.zero; _turn[0] = 0f;
            for (int t = 1; t <= L; t++)
            {
                float tm = t - 0.5f, ang = th * Smooth01(tm / TetherSpan), step = tm <= d0 ? 1f : m;
                _turn[t] = th * Smooth01(t / TetherSpan);
                _line[t] = _line[t - 1] + new Vector2(sx * Mathf.Sin(ang), Mathf.Cos(ang)) * step;
            }
            for (float r = 0.125f; r < v.H; r += 0.25f)   // fine enough that the stretched cloth leaves no gaps
            {
                int ri = (int)r;
                float h = Body8Art.Top - (v.Y0 + r), d = CapeTop - h;
                // the line's place and turn at this depth down (above the top: straight)
                Vector2 C; float ang;
                if (d <= 0f) { C = new Vector2(0f, d); ang = 0f; }
                else
                {
                    int t0 = Mathf.Min((int)d, L - 1); float fr = Mathf.Min(d - t0, 1f);
                    C = Vector2.Lerp(_line[t0], _line[t0 + 1], fr); ang = Mathf.Lerp(_turn[t0], _turn[t0 + 1], fr);
                }
                // the cloth across the line turns with it only as far as the view is side on: there the picture's width is the
                // cape's thickness; facing more toward or away from the camera it is the cape's breadth, which stays level
                float side = f.x * f.x, ca = Mathf.Cos(ang * side), sa = Mathf.Sin(ang * side), thick = 1f + DashFace * sa;
                thick *= 1f + (a.y > 0f ? DashFanUp : DashFanDown) * Smooth01(Cape.Dash) * Mathf.Abs(a.y) * Down(h);   // into or out of the screen it fans wide
                float plain = d <= d0 ? d : d0 + (d - d0) * m;   // where it would be with no sideways turn
                float xStep = 0.34f / thick;
                for (float xf = xStep * 0.5f; xf < v.W; xf += xStep)
                {
                    int xi = (int)xf;
                    char ch = v.Cols[ri * v.W + xi];
                    if (ch == '.') continue;
                    Color col;
                    if (piece.Suit) { if (!suit.TryGetValue(ch, out col)) continue; }
                    else if (!piece.Palette.TryGetValue(ch, out col)) continue;
                    float xr = v.X0 + xf - mid;
                    float px = mid + C.x + xr * thick * ca, pd = C.y - xr * thick * sx * sa;
                    float hOut = CapeTop - pd;
                    float hm = hOut + lift + (hOut > Pelvis ? c * (hOut - Pelvis) : 0f);
                    float sh = hOut > Pelvis ? (hOut - Pelvis) * lean : 0f;
                    int X = Mathf.FloorToInt(_GX + px + f.x * sh), R = Mathf.FloorToInt(_GY - hm);
                    if (X < 0 || X >= _W || R < 0 || R >= _H) continue;
                    float z = v.Depth[ri * v.W + xi] * 0.5f - 64f + f.z * sh - f.z * LiveDepth * Mathf.Clamp01(d / (CapeTop - CapeHem) * 4f);
                    bool moved = (px - (v.X0 + xf)) * (px - (v.X0 + xf)) + (pd - plain) * (pd - plain) > MovedFrom * MovedFrom;
                    if (moved && under) PutUnder(X, R, z, col, id); else Put(X, R, z, col, id);
                }
            }
        }

        static Vector2[] _line;
        static float[] _turn;

        /// <summary>One drawn row of the living cape: each texel found back through the flare (across the body's width
        /// as the screen shows it), the waves running down the cloth, and the points drawn out of the hem.</summary>
        static void LiveSpan(Body8Art.Piece piece, Body8Art.View v, int id, int R, float hb, float hd, float trail, float liveTrail,
                             float lean, Vector3 f, float rightX, float capeBottom, Dictionary<char, Color> suit)
        {
            Breath(out float b, out float surge);
            float A = Cape.Angle, across = Mathf.Abs(rightX);
            float s = hb > Pelvis ? (hb - Pelvis) * lean : 0f;
            int dx = Mathf.RoundToInt(f.x * s - f.x * trail);
            // the billow is drawn, not moved in depth (the cloth at the collar never flips over it); instead the cape sits
            // a fixed step out from the back, full by a quarter of the way down, so the arms never poke through it
            float dz = f.z * s - f.z * (trail - liveTrail) - f.z * LiveDepth * Mathf.Clamp01((CapeTop - hb) / (CapeTop - CapeHem) * 4f);
            int reach = LiveReach;
            int x0 = _GX + v.X0 - reach, x1 = _GX + v.X0 + v.W + reach;
            for (int X = Mathf.Max(0, x0); X < Mathf.Min(_W, x1); X++)
            {
                float u0 = X - _GX - dx;
                Texel(X, u0, hb, false);
            }

            void Texel(int X, float u, float h, bool moved)
            {
                float k = Down(h), k2 = k * k;
                float spread = (0.45f * surge + 0.6f * Cape.Lash - 0.4f * Cape.Wrap) * k2 * across;
                // a wave travelling down the cloth, and the hem's quick writhe
                float wave = (Cape.Moving ? 1.6f : 1.6f + 1.2f * b) * k * Mathf.Sin(2f * A - h * 0.14f);
                wave += 1.4f * Smooth01((k - 0.7f) / 0.3f) * Mathf.Sin((Cape.Moving ? 4f : 9f) * A + h * 0.5f);
                float xs = (u - wave) / (1f + spread);
                // the points: the cape's last PointSpan texels drawn out over PointSpan + reach, each column on its beat
                float pr = ReachLow + (ReachHigh - ReachLow) * (0.5f + 0.5f * Mathf.Sin((Cape.Moving ? 2f : 3f) * A + xs * 0.45f));
                pr += 3f * Cape.Lash + 3f * Cape.Dash;
                if (h < capeBottom + PointSpan)
                {
                    if (h < capeBottom - pr) return;
                    h = capeBottom + (h - (capeBottom - pr)) * PointSpan / (PointSpan + pr);
                }
                int r = Mathf.FloorToInt(Body8Art.Top - h) - v.Y0;
                if (r < 0 || r >= v.H) return;
                int x = Mathf.RoundToInt(xs) - v.X0;
                if (x < 0 || x >= v.W) return;
                char ch = v.Cols[r * v.W + x];
                if (ch == '.') return;
                Color col;
                if (piece.Suit) { if (!suit.TryGetValue(ch, out col)) return; }
                else if (!piece.Palette.TryGetValue(ch, out col)) return;
                float z = v.Depth[r * v.W + x] * 0.5f - 64f + dz;
                // carried across the body where the cape hangs behind it (facing the camera or side on), it goes under the
                // body; from behind, where the cape is the body's cover, it stays on top
                if (moved && f.z > -0.3f) PutUnder(X, R, z, col, id); else Put(X, R, z, col, id);
            }
        }
    }
}
