using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art
{
    /// <summary>
    /// The loot BOXES - every tier and the Rift Box - closed and open, at both densities.
    ///
    /// The user's brief (2026-10-09): a STONE crate in the chunky shape of the first reference
    /// (straps over the lid, chamfered corner bumpers, feet), a SILVER ARCHWAY framing the front
    /// panel with an OUTLINED emblem in the box's light (circle - Gold, Black Diamond; triangle -
    /// Silver, Rift; square - Bronze, Diamond), and LIGHT leaking from the lid seam just above the
    /// arch. OPENED, the lid swings back (a dark glass panel ringed in LEDs) and the box is a real,
    /// empty hollow with its colour LINING the rim. Every box is the same box; only the light
    /// changes - except the RIFT BOX, redesigned toward the extraction portal (RiftBox.cs): the
    /// same silhouette, but a TEAR you see home through, not an object.
    ///
    /// ONE function sampled at k texels per cell: k = 1 at the weapons' in-game density (FinePpu
    /// 150 - what the arena and the inventory show), k = 2 at menu density (300 - cards, the NFT
    /// image). The outline is drawn HERE rather than by PixelSprite's auto-outline: the light that
    /// spills past the stone must not be ringed in black.
    /// </summary>
    public static partial class BoxArt
    {
        public enum Kind { Bronze, Silver, Gold, Diamond, BlackDiamond, Rift }

        /// <summary>The shape inlaid in the arch, in the box's light (the user's pairing,
        /// 2026-10-09): circle - Gold, Black Diamond; triangle - Silver, Rift; square - Bronze,
        /// Diamond. Shape as well as colour, so two boxes never differ by colour alone.</summary>
        public enum Emblem { Circle, Triangle, Square }

        public static Emblem EmblemOf(Kind kind) => kind switch
        {
            Kind.Gold or Kind.BlackDiamond => Emblem.Circle,
            Kind.Silver or Kind.Rift => Emblem.Triangle,
            _ => Emblem.Square,
        };

        public const float ArenaPpu = 150f;
        public const float MenuPpu = 300f;

        const int W = 72, ClosedH = 54, OpenH = 74;
        const float Cx = 36f;

        public static Sprite Closed(Kind kind, bool menu) => Build(kind, open: false, menu);
        public static Sprite Open(Kind kind, bool menu) => Build(kind, open: true, menu);

        static Sprite Build(Kind kind, bool open, bool menu)
        {
            int k = menu ? 2 : 1;
            string key = $"box.{kind}.{(open ? "open" : "closed")}.{(menu ? "menu" : "arena")}";
            var cached = PixelSprite.Cached(key);
            if (cached != null) return cached;
            // The Rift box is drawn by its own path (RiftBox.cs) - a tear, not an object.
            var rows = kind == Kind.Rift ? SampleRift(open, k) : Sample(open, k, kind);
            int width = rows[0].Length;
            // Pivot at the feet, centred - a box stands on the floor.
            return PixelSprite.From(key, rows, kind == Kind.Rift ? RiftTearPalette() : Palette(kind), outline: false,
                                    pivotTexel: new Vector2Int(width / 2, 0),
                                    pixelsPerUnit: menu ? MenuPpu : ArenaPpu);
        }

        // ------------------------------------------------------------------ sampling

        /// <summary>Rows top-first, each texel a palette letter, outlined.</summary>
        public static string[] Sample(bool open, int k, Kind kind)
        {
            int h = (open ? OpenH : ClosedH) * k, w = W * k;
            var grid = new char[h, w];
            for (int Y = 0; Y < h; Y++)
            for (int X = 0; X < w; X++)
            {
                float x = (X + 0.5f) / k, y = (Y + 0.5f) / k;   // y up from the feet
                var c = open ? OpenTexel(x, y, X, Y, kind) : ClosedTexel(x, y, X, Y, kind);
                grid[Y, X] = c == '\0' ? '.' : c;   // '\0' is the parts' own "not me" - empty here
            }

            // The outline: k texels of ink round everything solid - never round the light.
            var outlined = (char[,])grid.Clone();
            for (int Y = 0; Y < h; Y++)
            for (int X = 0; X < w; X++)
            {
                // Only BLANK space takes ink - light drawn over the stone (a shard) is never inked.
                if (grid[Y, X] != '.') continue;
                bool near = false;
                for (int dy = -k; dy <= k && !near; dy++)
                for (int dx = -k; dx <= k && !near; dx++)
                {
                    int yy = Y + dy, xx = X + dx;
                    if (yy >= 0 && yy < h && xx >= 0 && xx < w && Solid(grid[yy, xx])) near = true;
                }
                if (near) outlined[Y, X] = 'o';
            }

            var rows = new string[h];
            for (int Y = 0; Y < h; Y++)
            {
                var line = new char[w];
                for (int X = 0; X < w; X++) line[X] = outlined[h - 1 - Y, X];   // top row first
                rows[Y] = new string(line);
            }
            return rows;
        }

        static bool Solid(char c) => c != '.' && c != 'g' && c != 'h' && c != 'z';

        // ------------------------------------------------------------------ the shared body

        /// <summary>
        /// The body below the lid - feet, front face, bumpers, straps, the silver arch and its
        /// panel - or '\0' outside it. Shared by both states: opening only changes what is above.
        /// </summary>
        static char Body(float x, float y, int X, int Y, Kind kind)
        {
            // Feet.
            if (y < 3f && ((x >= 8f && x < 15f) || (x >= 57f && x < 64f)))
                return y < 1.2f ? 'a' : 'b';

            // Bottom corner bumpers - chamfered on the outer lower corner.
            if (y >= 3f && y < 13f && ((x >= 4f && x < 13f) || (x >= 59f && x < 68f)))
            {
                float outer = x < Cx ? x - 4f : 68f - x;
                if (outer + (y - 3f) < 2.2f) return '\0';
                return Stone(x, y, X, Y, outer < 1.4f ? -1 : (y > 11.6f ? 1 : 0));
            }

            if (x < 6f || x >= 66f || y < 3f || y >= 34f) return '\0';

            // The silver archway: a band round a semicircle on two columns.
            const float ax = Cx, ay = 18f, rOut = 13f, rIn = 10.4f;
            float dx = x - ax, dy = y - ay;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            bool inArchBand = y >= ay ? (r >= rIn && r < rOut) : (Mathf.Abs(dx) >= rIn && Mathf.Abs(dx) < rOut && y >= 3f);
            if (inArchBand)
            {
                // Keystone: a proud block at the crown.
                if (y >= ay + rIn - 0.2f && Mathf.Abs(dx) < 1.6f) return y > ay + rOut - 1f ? 'S' : 's';
                // Lit from the upper left: the outer upper-left rim bright, the inner lower-right dark.
                float t = y >= ay ? (r - rIn) / (rOut - rIn) : (Mathf.Abs(dx) - rIn) / (rOut - rIn);
                bool left = dx < 0f;
                if (t > 0.72f) return left || y > ay + 6f ? 'S' : 's';
                if (t < 0.25f) return 'R';
                return left ? 's' : 'r';
            }
            // A plinth under each column.
            if (y < 5f && Mathf.Abs(Mathf.Abs(dx) - (rIn + rOut) / 2f) < 2.2f) return y < 4f ? 'r' : 's';

            bool insideArch = y >= ay ? r < rIn : Mathf.Abs(dx) < rIn && y >= 3f;
            if (insideArch)
            {
                // The emblem: an OUTLINE of the box's light inlaid between two carved grooves,
                // the same stone inside it as round it (the user's call).
                float inside = EmblemDepth(EmblemOf(kind), dx, y - 17.4f);
                if (inside > -0.7f && inside <= 0f) return 'a';
                if (inside > 0f && inside < 1.25f) return Light(x, dx < -0.6f && y - 17.4f > 0.4f);
                if (inside >= 1.25f && inside < 1.85f) return 'a';
                return Stone(x, y, X, Y, 0);
            }

            // Straps down the body, under the seam latches.
            if ((x >= 15f && x < 21f) || (x >= 51f && x < 57f))
            {
                if (y >= 29f) return Latch(x, y);
                bool edge = (x >= 15f && x < 16f) || (x >= 20f && x < 21f) || (x >= 51f && x < 52f) || (x >= 56f && x < 57f);
                return edge ? 'a' : Hash(X, Y, 5) < 0.2f ? 'a' : 'b';
            }

            // Carved panel lines, then the stone.
            if (Mathf.Abs(x - 10f) < 0.5f || Mathf.Abs(x - 62f) < 0.5f || Mathf.Abs(y - 7.5f) < 0.45f) return 'a';
            int shade = y > 28f ? 1 : y < 9f ? -1 : 0;
            return LitBySeam(Stone(x, y, X, Y, shade), x, y, X, Y);
        }

        /// <summary>
        /// How far inside the emblem a point is (cells; negative outside), centred on the arch's
        /// panel. Sized to sit well inside the arch with stone round it.
        /// </summary>
        static float EmblemDepth(Emblem emblem, float dx, float dy)
        {
            switch (emblem)
            {
                case Emblem.Circle:
                    return 4.6f - Mathf.Sqrt(dx * dx + dy * dy);
                case Emblem.Triangle:
                {
                    // Pointing up: base 4.6 below centre, apex 5.4 above, 5.6 either side at the base.
                    float baseY = -4.6f, apexY = 5.4f, half = 5.6f;
                    float fromBase = dy - baseY;
                    float halfAtY = half * (apexY - dy) / (apexY - baseY);
                    // The distance to the nearest edge, the slanted ones scaled to true distance.
                    float side = (halfAtY - Mathf.Abs(dx)) * 0.87f;
                    return Mathf.Min(fromBase, side);
                }
                default:
                    return 4.1f - Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
            }
        }

        /// <summary>A silver latch straddling the seam on each strap.</summary>
        static char Latch(float x, float y)
        {
            float lx = x < Cx ? x - 15f : x - 51f;   // 0..6 across the strap
            if (lx < 0.6f || lx > 5.4f) return 'R';
            if (y > 38.6f || y < 29.4f) return 'R';
            if (Mathf.Abs(lx - 3f) < 0.7f && y > 31f && y < 37f) return 'r';    // the hinge pin
            return lx < 2f || y > 37.8f ? 'S' : 's';
        }

        // ------------------------------------------------------------------ closed

        static char ClosedTexel(float x, float y, int X, int Y, Kind kind)
        {
            char body = Body(x, y, X, Y, kind);
            if (body != '\0') return body;

            // THE SEAM - the opening, light escaping. Brightest at the middle, fading out to the
            // corners, a thin gap of darkness at its very ends.
            if (y >= 33f && y < 35.6f && x >= 5f && x < 67f)
            {
                if ((x >= 15f && x < 21f) || (x >= 51f && x < 57f)) return Latch(x, y);
                float u = Mathf.Abs(x - Cx) / 31f;
                bool core = y >= 33.7f && y < 34.9f;
                if (core) return Light(x, u < 0.55f);
                return u < 0.8f ? Light(x, false) : 'T';
            }
            // ...and it slips out past the sides: a breath of translucent light, never outlined.
            if (y >= 33.3f && y < 35.3f && ((x >= 1.5f && x < 5f) || (x >= 67f && x < 70.5f)))
                return Hash(X, Y, 37) < 0.55f ? 'g' : 'h';

            // The lid's front face.
            if (y >= 35f && y < 45.5f && x >= 4f && x < 68f)
            {
                if ((x >= 15f && x < 21f) || (x >= 51f && x < 57f))
                    return y < 38.6f ? Latch(x, y) : (Hash(X, Y, 5) < 0.2f ? 'a' : 'b');
                if (x < 12f || x >= 60f)   // lid corner bumpers, chamfered top outer corner
                {
                    float outer = x < Cx ? x - 4f : 68f - x;
                    if (outer + (45.5f - y) < 2.2f) return '\0';
                    return Stone(x, y, X, Y, outer < 1.4f ? -1 : 1);
                }
                if (Mathf.Abs(y - 41f) < 0.45f && x > 24f && x < 48f) return 'a';   // a carved band
                return LitBySeam(Stone(x, y, X, Y, y > 43.5f ? 2 : 1), x, y, X, Y);
            }

            // The lid's top face, foreshortened - narrowing toward the back.
            if (y >= 45.5f && y < 51.5f)
            {
                float t = (y - 45.5f) / 6f;
                float l = Mathf.Lerp(4f, 9f, t), rr = Mathf.Lerp(68f, 63f, t);
                if (x < l || x >= rr) return '\0';
                // A chamfered front edge, then the top running back.
                if (t < 0.22f) return Stone(x, y, X, Y, 2);
                // The straps run over the top toward the back.
                float s1 = Mathf.Lerp(15f, 18f, t), s2 = Mathf.Lerp(51f, 48f, t);
                float sw = 6f - 1.5f * t;
                bool onS1 = x >= s1 && x < s1 + sw, onS2 = x >= s2 && x < s2 + sw;
                if (onS1 || onS2) return Hash(X, Y, 5) < 0.2f ? 'b' : 'c';
                // Raised chevrons on the lid, as on the reference.
                float cv = Mathf.Abs(x - Cx) * 0.3f + (y - 48.5f);
                if (Mathf.Abs(cv) < 0.4f && Mathf.Abs(x - Cx) < 9f) return 'e';
                return Stone(x, y, X, Y, 3);
            }
            return '.';
        }

        // ------------------------------------------------------------------ open

        static char OpenTexel(float x, float y, int X, int Y, Kind kind)
        {
            char body = Body(x, y, X, Y, kind);
            if (body != '\0')
            {
                // Open, the latches hang unhooked - the strap tops end at the rim.
                return body;
            }

            // The front rim of the body, lit from inside.
            if (y >= 33.2f && y < 35.2f && x >= 5f && x < 67f)
                return Hash(X, Y, 13) < 0.5f ? 'T' : 'd';

            // THE OPENING, seen from above - a real, EMPTY hollow (the user's calls): the box's own
            // colour LINING its rim, the far wall going down into it, a dark floor catching the
            // light. The Rift box holds a RIFT instead, cut to the opening.
            if (y >= 35.2f && y < 47f)
            {
                float t = (y - 35.2f) / 11.8f;
                float l = Mathf.Lerp(5f, 11f, t), rr = Mathf.Lerp(67f, 61f, t);
                if (x < l || x >= rr) return '\0';

                // The stone lip, then the light lining the rim all the way round.
                if (x < l + 1.2f || x >= rr - 1.2f || y < 36.2f || y >= 46f) return 'c';
                if (x < l + 2.2f || x >= rr - 2.2f || y < 37.1f || y >= 45.1f)
                    return Light(x, y < 37.1f && Mathf.Abs(x - Cx) < 22f);


                // The far wall, running down from the rim and lit by it, darkening with depth.
                if (y >= 42.2f)
                {
                    float depth = (45.1f - y) / 2.9f;
                    return Hash(X, Y, 53) < 0.55f - depth * 0.45f ? 'T' : depth > 0.7f ? 'K' : 'b';
                }

                // The floor, deep in shadow - the rim's light catching its edges and fading in.
                float fromRim = Mathf.Min(y - 37.1f, Mathf.Min(x - (l + 2.2f), rr - 2.2f - x) * 0.5f);
                if (fromRim < 1.6f && Hash(X, Y, 61) < 0.6f - fromRim * 0.35f) return 'T';
                return Hash(X, Y, 19) < 0.12f ? 'K' : 'k';
            }

            // THE LID, swung back on its hinge: its inside face - a dark glass panel ringed by an
            // LED strip, the way a case window is lit.
            if (y >= 47f && y < 71f)
            {
                float t = (y - 47f) / 24f;
                float l = Mathf.Lerp(7f, 11f, t), rr = Mathf.Lerp(65f, 61f, t);
                if (x < l || x >= rr) return '\0';
                if (y < 49f) return 'c';                                               // the lid's edge
                float inL = l + 4f, inR = rr - 4f;
                bool ring = (x >= inL - 1f && x < inL) || (x >= inR && x < inR + 1f) || (y >= 51f && y < 52f) || (y >= 66.5f && y < 67.5f);
                bool inside = x >= inL - 1f && x < inR + 1f && y >= 51f && y < 67.5f;
                if (ring && inside) return Light(x, true);
                if (inside)
                {
                    // Dark glass, a diagonal sheen, and the light from below warming its foot.
                    // Solid tones, not the translucent glow letters - inside the silhouette the
                    // floor would show through the lid.
                    if (Mathf.Abs((x - inL) - (y - 52f) * 0.6f - 8f) < 1.2f) return 'i';
                    if (y < 56f && Hash(X, Y, 23) < (56f - y) / 5f) return 'T';
                    return 'K';
                }
                // The lid's stone frame, tinted by the light near its foot.
                return y < 55f && Hash(X, Y, 29) < (55f - y) / 7f ? 'T' : Stone(x, y, X, Y, 0);
            }

            // Light rising out of the open box, translucent, not outlined.
            return '.';
        }

        // ------------------------------------------------------------------ materials

        /// <summary>Stone: noise between three tones, `shade` lifting (+) or dropping (-) it.</summary>
        static char Stone(float x, float y, int X, int Y, int shade)
        {
            // Patches a few cells across, so it reads as quarried stone, not grit...
            // Mostly the mid tone; a few soft patches a step either way.
            float n = Hash(Mathf.FloorToInt(x / 4f), Mathf.FloorToInt(y / 3f), 1);
            int tone = n < 0.14f ? 0 : n > 0.9f ? 2 : 1;
            tone += shade;
            // ...and a sparse fleck of the next tone down.
            if (Hash(X, Y, 2) < 0.045f) tone -= 1;
            return tone <= -1 ? 'a' : tone == 0 ? 'b' : tone == 1 ? 'c' : tone == 2 ? 'd' : 'e';
        }

        /// <summary>Stone near the seam catches the escaping light, thinning with distance.</summary>
        static char LitBySeam(char stone, float x, float y, int X, int Y)
        {
            float d = Mathf.Abs(y - 34.3f);
            float u = Mathf.Abs(x - Cx) / 31f;
            float p = (1f - d / 4.6f) * (1f - u * 0.7f);
            return p > 0f && Hash(X, Y, 31) < p * 0.85f ? 'T' : stone;
        }

        /// <summary>A light texel: the core ('L'/'J') or the colour ('l'/'j'). The second letter of
        /// each pair is the Rift's violet half, taken right of centre; for every other box the two
        /// letters are the same colour.</summary>
        static char Light(float x, bool core)
        {
            bool second = x + (Hash(Mathf.FloorToInt(x * 4f), 0, 41) - 0.5f) * 6f > Cx;
            return core ? (second ? 'J' : 'L') : (second ? 'j' : 'l');
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        // ------------------------------------------------------------------ colour

        /// <summary>The box's light. Tier colours are the game's own (CharacterScreen.TierColor);
        /// the Rift's is the tear's cyan, with its violet taken by 'g' below.</summary>
        public static Color LightOf(Kind kind) => kind switch
        {
            Kind.Bronze => new Color(0.95f, 0.55f, 0.22f),
            Kind.Silver => new Color(0.80f, 0.88f, 1.00f),
            Kind.Gold => new Color(1.00f, 0.80f, 0.30f),
            Kind.Diamond => new Color(0.55f, 0.88f, 1.00f),
            Kind.BlackDiamond => new Color(0.74f, 0.42f, 1.00f),
            _ => new Color(0.45f, 0.88f, 1.00f),
        };

        static Dictionary<char, Color> Palette(Kind kind)
        {
            static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
            static Color A(Color c, float a) => new(c.r, c.g, c.b, a);
            var light = LightOf(kind);
            // The Rift's light runs cyan into violet - its glow and tint lean to the violet.
            var second = kind == Kind.Rift ? new Color(0.62f, 0.42f, 0.95f) : light;
            var stoneMid = Hex("#6d6863");
            return new Dictionary<char, Color>
            {
                ['o'] = Hex("#141217"),
                // Stone, darkest to the lit top.
                ['a'] = Hex("#2e2b2c"), ['b'] = Hex("#4a4645"), ['c'] = stoneMid, ['d'] = Hex("#8d8780"), ['e'] = Hex("#aaa49b"),
                ['p'] = Hex("#3a3637"),
                // Silver.
                ['S'] = Hex("#f2f4f8"), ['s'] = Hex("#b9bec8"), ['r'] = Hex("#7c8290"), ['R'] = Hex("#4a4f5c"),
                // The light: a near-white core, the colour, and stone it falls on.
                ['L'] = Color.Lerp(light, Color.white, 0.55f), ['l'] = light,
                ['J'] = Color.Lerp(second, Color.white, 0.55f), ['j'] = second,
                ['T'] = Color.Lerp(stoneMid, second, 0.45f),
                ['g'] = A(second, 0.55f), ['h'] = A(Color.Lerp(light, Color.white, 0.4f), 0.35f),
                // Dark interiors: the hollow's floor, the lid's glass, and spare tones.
                ['k'] = Hex("#0f0f14"), ['K'] = Hex("#23232c"), ['f'] = Hex("#3c3c48"), ['F'] = Hex("#5a5a68"),
                ['m'] = Hex("#d8dae2"), ['i'] = Hex("#4a4d5c"),
            };
        }
    }
}
