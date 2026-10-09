using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art
{
    public static partial class BoxArt
    {
        // ------------------------------------------------------------------ the Rift box
        //
        // A FULL REDESIGN toward the extraction portal (the user's call, 2026-10-09). The portal's
        // rule (Rifts.Rift): "A RIFT IS A TEAR, NOT A DOOR" - no frame, no architecture; a ragged
        // edge with light escaping along it, a violet halo, cyan shards, and HOME seen through it.
        // So the Rift box keeps the box's exact SILHOUETTE - taken from the stone box itself, open
        // or closed - and nothing else of the object:
        //
        //   THROUGH IT   home, framed on the SIGIL DOOR as the portal frames its view - the one
        //                fixture no player can move - warmed as the portal warms it
        //   ITS EDGE     the portal's torn rim instead of an ink outline - white-cyan at the very
        //                edge into cyan, notches bitten out, sparks breaking off it
        //   FAULT LINES  only the lid seam (the brightest - where it opens) and the lid's edge; the
        //                box's triangle is the door's lit mark
        //   AROUND IT    a dithered violet halo, and the portal's cyan shards
        //   OPEN         the cavity is the tear's bright core, light pouring out
        //
        // The canvas is wider by M cells a side (and on top) so the halo has somewhere to go.

        const int M = 5;

        public static string[] SampleRift(bool open, int k)
        {
            int baseH = open ? OpenH : ClosedH;
            int w = (W + 2 * M) * k, h = (baseH + M) * k;

            // The silhouette: wherever the stone box is solid.
            var inside = new bool[h, w];
            for (int Y = 0; Y < h; Y++)
            for (int X = 0; X < w; X++)
            {
                float x = (X + 0.5f) / k - M, y = (Y + 0.5f) / k;
                if (x < 0f || x >= W || y >= baseH) continue;
                char c = open ? OpenTexel(x, y, X, Y, Kind.Bronze) : ClosedTexel(x, y, X, Y, Kind.Bronze);
                inside[Y, X] = c != '\0' && c != '.' && c != 'g' && c != 'h' && c != 'z';
            }

            // Distance (texels, Chebyshev) to the other side of the silhouette's edge, both ways.
            int reach = 5 * k;
            var dist = new int[h, w];
            for (int Y = 0; Y < h; Y++)
            for (int X = 0; X < w; X++)
            {
                bool me = inside[Y, X];
                int best = reach + 1;
                for (int r = 1; r <= reach && best > reach; r++)
                    for (int dy = -r; dy <= r && best > reach; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        int yy = Y + dy, xx = X + dx;
                        // Past the canvas is OUTSIDE the box - other only to a texel inside it.
                        bool other = yy < 0 || yy >= h || xx < 0 || xx >= w ? me : inside[yy, xx] != me;
                        if (other) { best = r; break; }
                    }
                dist[Y, X] = best;
            }

            var rows = new string[h];
            for (int Y = 0; Y < h; Y++)
            {
                var line = new char[w];
                for (int X = 0; X < w; X++)
                {
                    float x = (X + 0.5f) / k - M, y = (Y + 0.5f) / k;
                    line[X] = inside[Y, X]
                        ? RiftInside(x, y, X, Y, dist[Y, X], k, open)
                        : RiftOutside(x, y, X, Y, dist[Y, X], k);
                }
                rows[h - 1 - Y] = new string(line);   // top row first
            }
            return rows;
        }

        static char RiftInside(float x, float y, int X, int Y, int d, int k, bool open)
        {
            // THE TORN EDGE: its width wavers along the rim; a few notches bitten clean out.
            float rim = k * (1.2f + Hash(X / (2 * k), Y / (2 * k), 81) * 1.6f);
            if (d <= k && Hash(X / k, Y / k, 83) < 0.10f) return '.';
            if (d <= rim) return d <= k ? 'V' : 'L';

            // Open: the cavity is the tear's bright core, light pouring up out of it.
            if (open && y >= 35.2f && y < 47f)
            {
                float t = (y - 35.2f) / 11.8f;
                float l = Mathf.Lerp(5f, 11f, t), r = Mathf.Lerp(67f, 61f, t);
                // Hottest at the back where it pours out, streaked as it rises.
                float streak = Mathf.Abs(Mathf.Sin(x * 0.9f)) * 0.5f + Hash(Mathf.FloorToInt(x * k), 0, 85) * 0.5f;
                float heat = t * 0.7f + streak * 0.5f;
                return heat > 0.62f ? 'V' : heat > 0.32f ? 'L' : 'l';
            }

            // The straps the other boxes wear, in the Rift's cyan light (the user's call).
            char strap = Strap(x, y, open);
            if (strap != '\0') return strap;

            // FAULT LINES where the box's features were.
            char fault = Fault(x, y, open);
            if (fault != '\0') return fault;

            return Home(x, y, X, Y, open);
        }

        /// <summary>
        /// The stone boxes' two straps - down the body, over the lid front and back across the
        /// top - as bands of the Rift's CYAN light: a bright core with cyan edges. Open, only the
        /// body's run, the lid having swung away. '\0' off them.
        /// </summary>
        static char Strap(float x, float y, bool open)
        {
            float a, b;   // the band's left and right at this height
            if (y < 3f) return '\0';
            if (y < (open ? 35f : 45.5f)) { a = 0f; b = 6f; }
            else if (!open && y < 51.5f)
            {
                // Over the top, converging toward the back as the stone straps do.
                float t = (y - 45.5f) / 6f;
                a = 3f * t; b = 6f - 1.5f * t + 3f * t;
            }
            else return '\0';

            foreach (bool left in new[] { true, false })
            {
                float lo = left ? 15f + a : 51f - a + (6f - (b - a)) - 0f;
                float hi = lo + (b - a);
                if (!left) { lo = 57f - (b - a) - a; hi = 57f - a; }
                if (x < lo || x >= hi) continue;
                float edge = Mathf.Min(x - lo, hi - x);
                return edge < 1f ? 'l' : 'L';
            }
            return '\0';
        }

        /// <summary>The few features kept, as seams of cyan light across the tear.</summary>
        static char Fault(float x, float y, bool open)
        {
            const float thin = 0.42f;
            if (!open)
            {
                // The lid seam - the brightest seam, where the box would open - and the lid's edge.
                if (Mathf.Abs(y - 34.2f) < 0.7f) return Mathf.Abs(y - 34.2f) < 0.3f ? 'V' : 'L';
                if (Mathf.Abs(y - 45.5f) < thin) return 'l';
            }
            else
            {
                // The lid swung back: its panel's frame.
                if (y >= 51f && y < 67.5f)
                {
                    float t = (y - 47f) / 24f;
                    float inL = Mathf.Lerp(7f, 11f, t) + 3.5f, inR = Mathf.Lerp(65f, 61f, t) - 3.5f;
                    if ((Mathf.Abs(x - inL) < thin || Mathf.Abs(x - inR) < thin) && y < 67f) return 'l';
                    if ((Mathf.Abs(y - 51.5f) < thin || Mathf.Abs(y - 67f) < thin) && x > inL && x < inR) return 'l';
                }
                if (Mathf.Abs(y - 47.6f) < thin) return 'l';
            }
            return '\0';
        }

        /// <summary>
        /// HOME through the tear, framed on the SIGIL DOOR as the portal frames its view
        /// (HubGlimpse is aimed at the gate) - the one fixture no player can move, so every
        /// player's box shows THEIR hub, and one shared NFT image is true for all of them. Nothing
        /// movable is drawn: no circle, no couch, no forge. Warmed as the portal warms its view.
        ///
        /// The door is shut: two stone leaves at a dark seam, its element marks CUT down the
        /// seam (a groove with a lit lip, as SigilDoor cuts them), the second lit in the Rift's
        /// light - the box's own triangle. Behind it the wall with its gallery band; before it,
        /// plain floor. Open, the door rides up on the lifted lid.
        /// </summary>
        static char Home(float x, float y, int X, int Y, bool open)
        {
            float wallFoot = open ? 49.5f : 23f, doorTop = open ? 70f : 52f;
            const float doorHalf = 8f;

            if (y >= wallFoot && Mathf.Abs(x - Cx) < doorHalf && y < doorTop)
            {
                float dx = x - Cx;
                // Marks cut ACROSS the seam, as the door's own are - three, alternating up and
                // down; the middle one burns in the Rift's light, the box's own triangle.
                float span = doorTop - wallFoot;
                for (int m = 0; m < 3; m++)
                {
                    float my = doorTop - span * (0.22f + m * 0.28f);
                    bool up = m % 2 == 0;
                    float depth = Mark(dx, (y - my) * (up ? 1f : -1f));
                    if (depth > 0f && depth < 0.9f)
                        return m == 1 ? Light(x, depth > 0.45f) : (depth < 0.45f ? 'q' : 'P');
                }
                if (Mathf.Abs(dx) < 0.45f) return 'k';                                // the seam
                // The leaves: lit along their outer edges, deeper toward the seam.
                return Mathf.Abs(dx) > doorHalf - 0.9f ? 'P' : Mathf.Abs(dx) < 1.4f ? 'p' : Hash(X / 2, Y / 2, 97) < 0.15f ? 'p' : 'n';
            }
            if (y >= wallFoot)
            {
                // The wall, its gallery band a little below the top.
                float band = doorTop - 3f;
                if (Mathf.Abs(y - band) < 0.5f) return 'k';
                if (y > band) return 'w';
                return Hash(X / 2, Y / 2, 87) < 0.2f ? 'w' : 'W';
            }
            if (y >= wallFoot - 1.3f) return 'k';                                    // the wall's foot

            // The door's light on the floor before it, then plain floor, warmest in the middle.
            if (Mathf.Abs(x - Cx) < doorHalf && y > wallFoot - 6f && Hash(X, Y, 99) < 0.35f) return 'y';
            float warm = 1f - Mathf.Sqrt(((x - Cx) / 40f) * ((x - Cx) / 40f) + ((y - 20f) / 30f) * ((y - 20f) / 30f));
            return Hash(X, Y, 89) < warm * 0.5f ? 'y' : 'Y';
        }

        /// <summary>An element mark - a triangle, apex up - as depth inside it (cells; negative
        /// outside). Small enough that four stack down a leaf's seam.</summary>
        static float Mark(float dx, float dy)
        {
            const float baseY = -2.4f, apexY = 2.9f, half = 3.4f;
            float halfAtY = half * (apexY - dy) / (apexY - baseY);
            return Mathf.Min(dy - baseY, (halfAtY - Mathf.Abs(dx)) * 0.85f);
        }

        static char RiftOutside(float x, float y, int X, int Y, int d, int k)
        {
            // Sparks breaking off the rim.
            if (d <= k && Hash(X / k, Y / k, 91) < 0.10f) return 'L';
            // The shards, thrown clear (shared with the rest of the boxes' geometry).
            if (RiftShardAt(x, y)) return 'z';
            // The violet halo, thinning away from the edge.
            float fall = 1f - (d - 1f) / (3f * k);
            if (fall <= 0f) return '.';
            float n = Hash(X, Y, 93);
            if (n < fall * fall * 0.3f) return 'g';
            if (n < fall * fall * 0.55f) return 'h';
            return '.';
        }

        /// <summary>Three cyan slivers round the tear, as the portal throws them.</summary>
        static bool RiftShardAt(float x, float y)
        {
            static bool Seg(float x, float y, float ax, float ay, float bx, float by)
            {
                float dx = bx - ax, dy = by - ay;
                float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
                float px = ax + dx * t - x, py = ay + dy * t - y;
                return px * px + py * py < 0.3f;
            }
            return Seg(x, y, -3.5f, 30f, -1.5f, 35.5f) || Seg(x, y, 73.5f, 22f, 75.5f, 27f) || Seg(x, y, 60f, 57f, 63.5f, 59f);
        }

        static Dictionary<char, Color> RiftTearPalette()
        {
            static Color A(Color c, float a) => new(c.r, c.g, c.b, a);
            var cyan = new Color(0.45f, 0.88f, 1.00f);
            var violet = new Color(0.62f, 0.42f, 0.95f);
            return new Dictionary<char, Color>
            {
                // The torn edge and the seams: white-cyan core, the portal's cyan.
                ['V'] = new Color(0.86f, 0.97f, 1.00f), ['L'] = Color.Lerp(cyan, Color.white, 0.35f), ['l'] = cyan,
                ['J'] = Color.Lerp(violet, Color.white, 0.35f), ['j'] = violet,
                // Around it: the violet halo, the cyan shards.
                ['g'] = A(violet, 0.42f), ['h'] = A(violet, 0.2f), ['z'] = A(cyan, 0.85f),
                // HOME, warmed as the portal warms its view (x1.35, 1.22, 1.05).
                ['Y'] = new Color(0.23f, 0.19f, 0.19f), ['y'] = new Color(0.32f, 0.26f, 0.23f),
                ['W'] = new Color(0.22f, 0.21f, 0.23f), ['w'] = new Color(0.19f, 0.18f, 0.20f),
                ['Q'] = new Color(0.64f, 0.62f, 0.64f), ['k'] = new Color(0.08f, 0.07f, 0.08f),
                ['Z'] = new Color(0.86f, 0.66f, 0.30f),
                ['O'] = new Color(0.98f, 0.52f, 0.16f), ['o'] = new Color(1.00f, 0.80f, 0.45f),
                // The Sigil Door, its own stone warmed: Stone, StoneDeep, CutShade and CutLip.
                ['n'] = new Color(0.58f, 0.55f, 0.56f), ['p'] = new Color(0.47f, 0.44f, 0.45f),
                ['q'] = new Color(0.18f, 0.17f, 0.19f), ['P'] = new Color(0.76f, 0.72f, 0.71f),
            };
        }
    }
}
