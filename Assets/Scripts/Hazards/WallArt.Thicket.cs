using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// The Drowned Forest's interior walls: THICKET. A cell's top is a mass of leaves, lumpy
    /// where it meets open floor; its south face (where the 3/4 view shows height) is the dark
    /// under the leaves, stems and branches in it. A CRACKED (breakable) cell is deadwood - pale,
    /// dry branches in a tangle - lighter and warmer, the cracked walls' rule, so a shortcut still
    /// reads from across the room. Same cell, same pivot and density as the stone blocks; only
    /// the picture changes.
    /// </summary>
    static partial class WallArt
    {
        static readonly Color[] Leaves =
        {
            new(0.035f, 0.07f, 0.045f), new(0.06f, 0.115f, 0.065f), new(0.09f, 0.17f, 0.085f),
            new(0.13f, 0.235f, 0.105f), new(0.19f, 0.31f, 0.13f),
        };
        static readonly Color[] Dead =
        {
            new(0.22f, 0.16f, 0.10f), new(0.32f, 0.24f, 0.15f), new(0.42f, 0.32f, 0.20f),
            new(0.52f, 0.41f, 0.27f), new(0.62f, 0.50f, 0.34f),
        };
        static readonly Color Under = new(0.02f, 0.035f, 0.025f), Stem = new(0.13f, 0.09f, 0.06f),
                              Outline = new(0.015f, 0.025f, 0.02f);

        static float H(int x, int y, int s)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + s * 982451653);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xFFFF) / 65536f;
            }
        }

        static float VN(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi, u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
            float a = H(xi, yi, s), b = H(xi + 1, yi, s), c = H(xi, yi + 1, s), d = H(xi + 1, yi + 1, s);
            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        /// <summary>Drawn in the CELL's own world position, so the leaves run on from one cell into
        /// the next (per-cell noise left a seam at every cell) - cached per cell.</summary>
        static Sprite Thicket(int open, bool cracked, int variant, int cellX, int cellY)
        {
            int key = open | (cracked ? 16 : 0) | (variant << 5) | (1 << 9) | (cellX & 63) << 12 | (cellY & 63) << 18;
            int ox = cellX * N, oy = cellY * N;   // texel offset of this cell in the room
            if (_cache.TryGetValue(key, out var s) && s != null) return s;

            bool north = (open & 1) != 0, south = (open & 2) != 0, west = (open & 4) != 0, east = (open & 8) != 0;
            int face = south ? Mathf.RoundToInt(N * Tuning.Rooms.FaceFraction) : 0;
            var ramp = cracked ? Dead : Leaves;
            var px = new Color[N * N];
            var solid = new bool[N * N];

            // The top's outline: lumpy where a side meets open floor, so a thicket never reads as
            // a cut block.
            float Bulge(int i, int side) => 2f + 4f * Mathf.Abs(Mathf.Sin(i * 0.21f + side * 1.7f + variant))
                                           + 2f * H(i / 4, side, variant + 3);
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                bool inside = true;
                if (west && x < Bulge(y, 0)) inside = false;
                if (east && N - 1 - x < Bulge(y, 1)) inside = false;
                if (north && N - 1 - y < Bulge(x, 2)) inside = false;
                solid[y * N + x] = inside;
                if (!inside) { px[y * N + x] = Color.clear; continue; }

                Color c;
                if (y < face)
                {
                    // The face: the dark under the leaves, stems standing in it, the leaves'
                    // lower edge hanging over its top.
                    c = Under;
                    if (!cracked && ((x * 5 + variant * 3) % 13 == 0 || (x * 3 + 7) % 17 == 0) && y < face - 4) c = Stem;
                    if (cracked && (x + y * 2 + variant) % 9 < 2) c = Dead[1];
                    float hang = face - 3f - 3f * Mathf.Abs(Mathf.Sin(x * 0.33f + variant));
                    if (y > hang) c = ramp[1];
                }
                else if (!cracked)
                {
                    // The top: leaf clumps from a noise field, each lit from the upper left (the
                    // field sampled a little toward the light - a modulo grid read as tiles).
                    float n = VN((x + ox) / 9f, (y + oy) / 9f, 1) * 0.65f + VN((x + ox) / 4f, (y + oy) / 4f, 2) * 0.35f;
                    float toward = VN((x + ox - 2) / 9f, (y + oy + 2) / 9f, 1) * 0.65f + VN((x + ox - 2) / 4f, (y + oy + 2) / 4f, 2) * 0.35f;
                    float lit = n * 0.75f + (toward - n) * 3.2f + 0.12f;
                    if (H(x, y, 11 + variant) < 0.05f) lit += 0.12f;   // leaf glints
                    c = ramp[Mathf.Clamp(Mathf.FloorToInt(lit * ramp.Length), 0, ramp.Length - 1)];
                }
                else
                {
                    // Deadwood: a dark tangle with pale branches laid across it (drawn below).
                    float n = VN((x + ox) / 6f, (y + oy) / 6f, 40);
                    c = ramp[n > 0.55f ? 1 : 0];
                }
                px[y * N + x] = c;
            }

            if (cracked)
            {
                var rng = new System.Random(key * 31 + 7);
                for (int b = 0; b < 9; b++)
                {
                    float bx = rng.Next(N), by = face + rng.Next(Mathf.Max(1, N - face)), ang = (float)rng.NextDouble() * Mathf.PI;
                    int len = 18 + rng.Next(30);
                    for (int i = 0; i < len; i++)
                    {
                        int ix = Mathf.RoundToInt(bx + Mathf.Cos(ang) * i), iy = Mathf.RoundToInt(by + Mathf.Sin(ang) * i * 0.6f);
                        if (ix < 0 || iy < 0 || ix >= N || iy >= N || !solid[iy * N + ix]) continue;
                        px[iy * N + ix] = ramp[3 + (i % 5 == 0 ? 1 : 0)];
                        if (iy + 1 < N && solid[(iy + 1) * N + ix]) px[(iy + 1) * N + ix] = ramp[4];   // lit upper edge
                        if (iy - 1 >= 0 && solid[(iy - 1) * N + ix] && iy - 1 >= face) px[(iy - 1) * N + ix] = ramp[1];
                    }
                }
            }

            // Outline the lumpy edge two texels deep where it meets open floor.
            var outPx = (Color[])px.Clone();
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                if (!solid[y * N + x]) continue;
                bool edge = false;
                for (int dy = -2; dy <= 2 && !edge; dy++)
                for (int dx = -2; dx <= 2 && !edge; dx++)
                {
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > 2) continue;
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0) { edge = west; continue; }
                    if (nx >= N) { edge = east; continue; }
                    if (ny >= N) { edge = north; continue; }
                    if (ny < 0) { edge = south; continue; }
                    if (!solid[ny * N + nx]) edge = true;
                }
                if (edge) outPx[y * N + x] = Outline;
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(outPx);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0f), N);
            s.hideFlags = HideFlags.HideAndDontSave;
            _cache[key] = s;
            return s;
        }
    }
}
