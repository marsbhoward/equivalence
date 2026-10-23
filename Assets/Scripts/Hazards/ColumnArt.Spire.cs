using UnityEngine;
using Convergence.Art.Biomes;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// THE SPIRE'S STONE, per place (2026-10-23): the obelisk the boon's light runs through, drawn
    /// texel by texel at the columns' density in the place's own material - the light itself (core,
    /// grooves, channels) stays the Spire's own tinted overlay, laid over the socket and groove cut
    /// here, so the boon's colour is untouched in every place.
    ///
    ///   TOWER   dressed ashlar in the walls' dark stone, a pyramidion, a stepped plinth
    ///   FOREST  a standing stone: rounded, moss on its crown and shaded side, roots gripping its foot
    ///   SKY     pale marble, gilt bands, ivy - and the pyramidion FLOATS a hand's width over the shaft
    ///   SALT    a crystal of salt: a six-sided prism in strata, pointed, with small crystals at its foot
    ///
    /// Every one keeps the same footprint (the collider), the socket at half height and the groove's
    /// line, since the overlay is placed off <see cref="Tuning.Spire"/> alone.
    /// </summary>
    static partial class ColumnArt
    {
        /// <summary>The groove's width, texels - the overlay channels are drawn this wide.</summary>
        public const int SpireGrooveTexels = 6;
        public static float SpireGrooveWidth => SpireGrooveTexels / Ppu;

        static readonly Sprite[] _spires = new Sprite[4];
        static Sprite _spireCore;

        static readonly Color Gilt = new(0.78f, 0.62f, 0.26f), GiltLit = new(0.95f, 0.82f, 0.42f), GiltDark = new(0.50f, 0.37f, 0.14f);
        static readonly Pal MenhirPal = new()
        {
            Ramp = new[] { new Color(0.13f, 0.14f, 0.12f), new Color(0.18f, 0.19f, 0.16f), new Color(0.23f, 0.24f, 0.20f),
                           new Color(0.29f, 0.30f, 0.25f), new Color(0.35f, 0.36f, 0.30f) },
            Top = new(0.33f, 0.34f, 0.28f), TopLight = new(0.40f, 0.41f, 0.34f),
            Seam = new(0.16f, 0.17f, 0.14f), Rim = new(0.45f, 0.46f, 0.38f),
        };

        public static Sprite SpireStone(BiomeKind place)
        {
            int i = (int)place;
            if (_spires[i] != null) return _spires[i];
            var g = new Grid();
            var rng = new System.Random(4409 + i * 131);
            g.SpireStone(place, rng);
            return _spires[i] = g.ToSprite();
        }

        /// <summary>The core's disc - white texels, tinted by the Spire as the boon's light.</summary>
        public static Sprite SpireCore
        {
            get
            {
                if (_spireCore != null) return _spireCore;
                var g = new Grid();
                g.Disc(SpireCoreRadius);
                return _spireCore = g.ToSprite();
            }
        }

        /// <summary>The core's radius in texels (the socket is cut round it).</summary>
        const float SpireCoreRadius = 14.5f;

        sealed partial class Grid
        {
            public void Disc(float r)
            {
                int n = Mathf.CeilToInt(r);
                for (int x = -n; x < n; x++)
                for (int y = -n; y < n; y++)
                {
                    float dx = x + 0.5f, dy = y + 0.5f;
                    if (dx * dx + dy * dy < r * r) Put(x, y, Color.white);
                }
            }

            static float Hash(int a, int b)
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }

            public void SpireStone(BiomeKind place, System.Random rng)
            {
                float hb = Tuning.Spire.BodyRadius * 1.5f * Ppu * 0.5f;   // the shaft's half width at the foot (~23.6)
                int H = Mathf.RoundToInt(Tuning.Spire.BodyHeight * Ppu);  // the shaft's top
                int mid = H / 2;
                int groove = Mathf.RoundToInt((Tuning.Spire.BodyHeight * 0.5f - 0.12f) * Ppu);

                switch (place)
                {
                    case BiomeKind.Forest: Menhir(hb, H, rng); break;
                    case BiomeKind.SaltFlats: SaltCrystal(hb, H, rng); break;
                    default: Obelisk(hb, H, place == BiomeKind.SkyRuin, rng); break;
                }

                Outline();
                // The light's bed, cut after the outline so the edge stays whole: the groove up and
                // down from the socket, and the round socket the core sits in.
                Groove(mid - groove, mid + groove, place);
                Socket(mid, place);

                if (place == BiomeKind.SkyRuin) Vines(hb * 0.9f, H * 0.95f, rng);
            }

            // ---- the four stones ----

            void Obelisk(float hb, int H, bool sky, System.Random rng)
            {
                var pal = sky ? Marble : Stone;
                // Plinth: two steps, each a block with its top seen.
                Block(-hb - 9, hb + 9, 0, 7, pal, 3);
                Block(-hb - 4, hb + 4, 7, 13, pal, 3);
                if (sky) for (int x = Mathf.FloorToInt(-hb - 4); x < hb + 4; x++) { Put(x, 12, GiltLit); Put(x, 11, Gilt); }

                // The shaft, tapering to 0.74 of its foot, in courses of ashlar.
                int course = 17;
                for (int y = 13; y < H; y++)
                {
                    float hw = Mathf.Lerp(hb, hb * 0.74f, (y - 13f) / (H - 13f));
                    int row = (y - 13) / course;
                    bool seam = (y - 13) % course == course - 1;
                    int joint = (row & 1) == 0 ? -7 : 8;
                    for (int x = Mathf.FloorToInt(-hw); x < Mathf.CeilToInt(hw); x++)
                    {
                        float u = (x + 0.5f) / hw;
                        Color c = u < -0.62f ? pal.Ramp[4] : u > 0.66f ? pal.Ramp[1] : pal.Ramp[3];
                        if (seam || x == joint) c = u < -0.62f ? pal.Ramp[3] : pal.Ramp[2] * 0.85f;
                        if (!seam && Hash(x, y) < 0.05f) c = Color.Lerp(c, pal.Ramp[2], 0.6f);   // grain
                        Put(x, y, c);
                    }
                }

                // The pyramidion - lifted clear of the shaft in the sky ruin, as all its stone floats.
                float ht = hb * 0.74f;
                int lift = sky ? 13 : 0, capH = Mathf.RoundToInt(ht * 1.25f);
                if (sky)
                    for (int x = Mathf.FloorToInt(-ht - 1); x < Mathf.CeilToInt(ht + 1); x++)
                    { Put(x, H + lift - 2, GiltDark); Put(x, H + lift - 1, x < 0 ? GiltLit : Gilt); }
                for (int y = 0; y < capH; y++)
                {
                    float hw = ht * (1f - (float)y / capH);
                    for (int x = Mathf.FloorToInt(-hw); x < Mathf.CeilToInt(hw); x++)
                    {
                        Color c = x < 0 ? pal.TopLight : pal.Ramp[1];
                        if (sky && y > capH - 5) c = x < 0 ? GiltLit : Gilt;   // a gilt tip
                        Put(x, H + lift + y, c);
                    }
                }
            }

            void Menhir(float hb, int H, System.Random rng)
            {
                var pal = MenhirPal;
                var moss = new Color(0.21f, 0.33f, 0.14f); var mossLit = new Color(0.33f, 0.47f, 0.19f);
                var lichen = new Color(0.55f, 0.58f, 0.40f);
                float p1 = Range(rng, 0f, 6f), p2 = Range(rng, 0f, 6f);
                int crown = H + 8;
                for (int y = 0; y < crown; y++)
                {
                    // A standing stone: thickest a little above the ground, rounded over the crown.
                    float t = (float)y / crown;
                    float hw = hb * (1.05f - 0.18f * t) + 1.6f * Mathf.Sin(y * 0.11f + p1) + 0.9f * Mathf.Sin(y * 0.27f + p2);
                    float cr = crown - y;
                    if (cr < hb) hw *= Mathf.Sqrt(Mathf.Clamp01(cr / hb * (2f - cr / hb)));
                    for (int x = Mathf.FloorToInt(-hw); x < Mathf.CeilToInt(hw); x++)
                    {
                        float u = (x + 0.5f) / Mathf.Max(1f, hw);
                        float l = -0.55f * u + 0.3f * (1f - Mathf.Abs(u)) + 0.25f;
                        Color c = pal.Ramp[Mathf.Clamp(Mathf.FloorToInt(l * 5f), 0, 4)];
                        if (Hash(x, y) < 0.04f) c = lichen;
                        // Moss: over the crown, and up the shaded side in drifts.
                        bool onCrown = cr < hb * 0.5f + 3f * Mathf.Sin(x * 0.4f + p2);
                        float drift = Mathf.Sin(y * 0.09f + p1) + 0.5f * Mathf.Sin(y * 0.23f + x * 0.35f + p2);
                        bool shaded = u > 0.25f && y < H * 0.85f && drift > 1.1f - 1.2f * u;
                        if (onCrown || shaded) c = Hash(x, y) < 0.18f ? mossLit : moss;
                        Put(x, y, c);
                    }
                }
                // Roots gripping the foot: they come down the face from the soil, then run out on it.
                var root = new Color(0.27f, 0.19f, 0.11f); var rootLit = new Color(0.40f, 0.29f, 0.17f);
                for (int k = 0; k < 6; k++)
                {
                    float side = k % 2 == 0 ? -1f : 1f;
                    float x = side * Range(rng, 4f, hb * 0.9f);
                    float y = Range(rng, 10f, 22f);
                    float dx = side * Range(rng, 0.15f, 0.5f);
                    int run = 0, length = rng.Next(5, 11);
                    while (run < length)
                    {
                        int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
                        Put(ix, iy, rootLit); Put(ix + 1, iy, root); Put(ix, iy - 1, root);
                        if (y > 1f) { y -= 1f; x += dx * 0.5f; }
                        else { x += side; y -= 0.45f; run++; }   // a short way out over the ground, toward us
                    }
                }
            }

            void SaltCrystal(float hb, int H, System.Random rng)
            {
                var pal = Salt;
                var blush = new Color(0.86f, 0.74f, 0.74f);
                int tipH = Mathf.RoundToInt(hb * 1.5f);
                for (int y = 0; y < H + tipH; y++)
                {
                    float hw = y < H ? Mathf.Lerp(hb * 1.02f, hb * 0.86f, (float)y / H)
                                     : hb * 0.86f * (1f - (float)(y - H) / tipH);
                    bool stratum = y < H && ((y + 400) / 9) % 3 == 1;
                    for (int x = Mathf.FloorToInt(-hw); x < Mathf.CeilToInt(hw); x++)
                    {
                        float u = (x + 0.5f) / Mathf.Max(1f, hw);
                        // Three faces of a six-sided prism: the lit facet, the front, the shaded facet.
                        Color c = u < -0.5f ? pal.Ramp[4] : u > 0.55f ? pal.Ramp[1] : pal.Ramp[3];
                        if (Mathf.Abs(u + 0.5f) < 0.05f || Mathf.Abs(u - 0.55f) < 0.05f) c = pal.Ramp[2];   // the facets' edges
                        if (stratum) c = Color.Lerp(c, blush, 0.35f);
                        if (y >= H) c = u < 0f ? pal.TopLight : pal.Ramp[2];                                 // the point's two faces
                        if (Hash(x, y) < 0.03f) c = pal.Rim;                                                 // glints
                        Put(x, y, c);
                    }
                }
                // Small crystals at the foot.
                for (int k = 0; k < 6; k++)
                {
                    float side = k % 2 == 0 ? -1f : 1f;
                    float cx = side * Range(rng, hb * 0.55f, hb + 3f), h = Range(rng, 6f, 12f), w = Range(rng, 2.5f, 4f);
                    float lean = side * Range(rng, 0f, 0.12f);
                    for (int y = -3; y < h; y++)
                    {
                        float hw = w * Mathf.Min(1f, (h - y) / (w * 1.6f));
                        float mx = cx + lean * y;
                        for (int x = Mathf.FloorToInt(mx - hw); x < Mathf.CeilToInt(mx + hw); x++)
                            Put(x, y, x < mx ? pal.TopLight : pal.Ramp[2]);
                    }
                }
            }

            // ---- shared pieces ----

            /// <summary>A plinth block: a front face from y0 to y1 and its top, <paramref name="topRows"/> deep.</summary>
            void Block(float x0, float x1, int y0, int y1, in Pal pal, int topRows)
            {
                for (int y = y0; y < y1 + topRows; y++)
                for (int x = Mathf.FloorToInt(x0); x < Mathf.CeilToInt(x1); x++)
                {
                    float u = (x + 0.5f - (x0 + x1) * 0.5f) / ((x1 - x0) * 0.5f);
                    Color c = y >= y1 ? (y == y1 ? pal.TopLight : pal.Top)
                            : u < -0.85f ? pal.Ramp[3] : u > 0.85f ? pal.Ramp[0] : pal.Ramp[1];
                    Put(x, y, c);
                }
            }

            void Groove(int y0, int y1, BiomeKind place)
            {
                int half = SpireGrooveTexels / 2;
                for (int y = y0; y < y1; y++)
                {
                    if (!Has(-half - 1, y) || !Has(half, y)) continue;
                    for (int x = -half; x < half; x++) Put(x, y, Line);
                    // Its right wall faces the light; its left lip is in shadow.
                    Put(half, y, Lip(place));
                    Put(-half - 1, y, Color.Lerp(Line, Lip(place), 0.35f));
                }
            }

            void Socket(int mid, BiomeKind place)
            {
                float r0 = SpireCoreRadius, r1 = r0 + 2.5f;
                int n = Mathf.CeilToInt(r1);
                for (int x = -n; x < n; x++)
                for (int y = -n; y < n; y++)
                {
                    float dx = x + 0.5f, dy = y + 0.5f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= r1 || !Has(x, mid + y)) continue;
                    // The cut's inner wall: lit where it faces the upper-left light (its lower right).
                    Color c = d < r0 ? Line : (dx - dy > 0f ? Lip(place) : Color.Lerp(Line, Lip(place), 0.3f));
                    Put(x, mid + y, c);
                }
            }

            static Color Lip(BiomeKind place) => place switch
            {
                BiomeKind.SkyRuin => Gilt,
                BiomeKind.SaltFlats => new Color(0.75f, 0.68f, 0.70f),
                BiomeKind.Forest => new Color(0.38f, 0.39f, 0.32f),
                _ => new Color(0.36f, 0.30f, 0.22f),
            };
        }
    }
}
