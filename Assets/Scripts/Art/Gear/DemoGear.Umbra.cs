using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Umbra: a skull on a raven's wings
    //
    // Diamond, power 0 - bespoke art and nothing else (the cosmetic weapons' rule). Three passes,
    // all the user's calls (2026-10-10):
    //   1. After a "Ring of Shadow Dagger" (a karambit of violet-black smoke off a finger ring): a
    //      bow's silhouette in the dagger's colour - smoke limbs with flame tongues on a ring.
    //   2. That read as a RAVEN more than shadow, so the limbs became feathered wings.
    //   3. After a photograph of a raven in flight: the ring became a SKULL and the feathers
    //      LONG FLIGHT FEATHERS that make the silhouette, as the bird's wings do. A raven's
    //      skull (in profile, beak outward) was drawn first; the user asked for a human one.
    //
    //   - THE SKULL IS THE RISER: a human skull, face on, after the user's sepia sketch - a tall
    //     dome, angular sockets, no jaw - the wings springing from behind it top and bottom. The
    //     hand holds its string-side edge.
    //   - EACH LIMB IS A WING: a smooth dark LEADING EDGE on the string side (the wing's bone);
    //     fourteen FLIGHT FEATHERS fanned off it - near the skull they stand out from it, toward
    //     the tip they lie down along it and grow longer - and the last five are the wingtip's
    //     FINGERS, narrower and splayed apart, the outermost graduated shorter. A row of rounded
    //     COVERTS over their roots. The feathers' ends are the trailing edge.
    //   - What failed in pass 3: thin feathers radiating apart read as a sea urchin; twenty broad
    //     ones overlapping read as one band. Fourteen, broad enough to overlap but long enough
    //     that each one's length shows, read as a wing.
    //   - Every feather (UmbraFeatherTone): a dark edge where it lies over the one beneath, a
    //     quill, a blue-violet sheen beside the quill on the lit vane, the rest raven black.
    //   - IT MOVES: the feathers ruffle on a loop (IdleFrames, frame 0 the still); the menu art is
    //     the still. The string is a thin violet strand, nocked at the ends of the leading edges.
    //
    // OUTLINELESS (OutlinelessWeapon, Rai's route): an auto-outline would bury the fingers and the
    // gaps between them. The leading edge, the feathers and the skull draw their own dark lines.
    // The canvas is BowHeightRows plus most of the outline the other bows draw - all of it would
    // put the menu art past the 320 texels an item image has room for.
    //
    // One field over authored cells (x across the canvas, string at the left, the wings' trailing
    // edges to the right; y down from the upper tip) sampled for the arena and (x4) the menu, so
    // the two can never disagree. Mocked up texel for texel in Python first.
    public static partial class DemoGear
    {
        const int UmbraCanvas = 36;
        static int UmbraRows => BowHeightRows + 3;

        const float UmbraStringX = 6.5f;
        const int UmbraFlight = 14, UmbraFingers = 5, UmbraCoverts = 8;
        const int UmbraFrameCount = 8;
        const float UmbraFrameSeconds = 0.12f;

        /// <summary>The skull's string-side edge - the bow's pivot, a little below its middle.</summary>
        static Vector2Int UmbraGrip => new(10, 41);
        static Vector2Int UmbraDetailGrip => new(UmbraGrip.x * EmberDetailScale, UmbraGrip.y * EmberDetailScale);

        // Lazy: static arrays in the partial files must not depend on another file's statics
        // being initialised first (Tria Prima's note).
        static Vector2[] _umbraUpper, _umbraLower;

        /// <summary>Each wing's LEADING EDGE, root at the skull to the wingtip. Separate curves,
        /// never a mirror.</summary>
        static Vector2[] UmbraUpper => _umbraUpper ??= UmbraCubic(
            new Vector2(15.2f, 36.6f), new Vector2(19.5f, 28f), new Vector2(9.5f, 18.8f), new Vector2(6.5f, 12f));
        static Vector2[] UmbraLower => _umbraLower ??= UmbraCubic(
            new Vector2(15.2f, 45.4f), new Vector2(19.5f, 54f), new Vector2(9.5f, 61.2f), new Vector2(6.5f, 68f));

        struct UmbraQuill
        {
            public Vector2 Root, Dir;
            public float Length, Half;
            public bool Covert;
        }

        /// <summary>Both wings' feathers for the frame being sampled, in drawing order (later on
        /// top) - built once per sample, not per texel.</summary>
        static UmbraQuill[][] _umbraWings;

        static void AddUmbra(List<GearItem> items)
        {
            const string key = "gear.weapon.umbra";
            var pal = UmbraPal();
            var still = OutlinelessWeapon(key, UmbraSample(1, 0f), pal, UmbraGrip, 0f);
            var item = WithMenu(Bow(Make("umbra_bow", "Umbra", GearSlot.Weapon, LootTier.Diamond, 0f, still)),
                OutlinelessMenuWeapon(key + ".menu", UmbraSample(EmberDetailScale, 0f), pal, UmbraDetailGrip, 0f));

            // The feathers ruffling. Frame 0 IS the still, so the loop and the still agree.
            var frames = new Sprite[UmbraFrameCount];
            frames[0] = still.Sprite;
            for (int i = 1; i < UmbraFrameCount; i++)
                frames[i] = PixelSprite.From($"{key}.ruffle{i}", Double2x(UmbraSample(1, i / (float)UmbraFrameCount)),
                                             pal, outline: false,
                                             pivotTexel: new Vector2Int(UmbraGrip.x * FineUpscale, UmbraGrip.y * FineUpscale),
                                             pixelsPerUnit: FinePpu);
            item.IdleFrames = frames;
            item.IdleFrameSeconds = UmbraFrameSeconds;
            items.Add(item);
        }

        /// <summary>
        /// The feathers (lowercase k s d b l: edges, the shadowed vane, the black, the sheen, the
        /// quill) - raven black lit blue-violet; the skull's sepia bone (uppercase: its line, the
        /// hatching, sockets' rims and gaps between teeth, then shading up to the lit dome); the
        /// string (3).
        /// </summary>
        static Dictionary<char, Color> UmbraPal()
        {
            static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
            var feather = Palette.Ramp.FromTones(glow: C(96, 108, 168), light: C(68, 70, 118), b: C(46, 44, 80),
                                                 dark: C(30, 27, 50), deep: C(19, 16, 32), line: C(10, 8, 16));
            var bone = Palette.Ramp.FromTones(glow: C(250, 246, 236), light: C(234, 226, 208), b: C(204, 190, 164),
                                              dark: C(152, 130, 102), deep: C(98, 76, 56), line: C(40, 28, 22));
            var strand = Palette.Ramp.FromTones(glow: C(136, 130, 160), light: C(104, 96, 132), b: C(82, 74, 108),
                                                dark: C(66, 58, 92), deep: C(50, 44, 72), line: C(30, 26, 44));
            return Palette.Of(feather, bone, strand);
        }

        /// <summary>The whole bow at <paramref name="k"/> texels per authored cell, at
        /// <paramref name="t"/> (0..1) of the ruffle.</summary>
        static string[] UmbraSample(int k, float t)
        {
            _umbraWings = new[] { UmbraWing(UmbraUpper, 1f, 0f, t), UmbraWing(UmbraLower, -1f, 3.7f, t) };
            var rows = new string[UmbraRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[UmbraCanvas * k];
                float y = (Y + 0.5f) / k;
                for (int X = 0; X < line.Length; X++)
                    line[X] = UmbraTexel((X + 0.5f) / k, y, k > 1);
                rows[Y] = new string(line);
            }
            return rows;
        }

        /// <summary>
        /// One wing's feathers off its leading edge <paramref name="pts"/>: the flight feathers,
        /// then the coverts over their roots. <paramref name="sign"/> turns the edge's normal
        /// outward (away from the string) for the limb's direction of travel.
        /// </summary>
        static UmbraQuill[] UmbraWing(Vector2[] pts, float sign, float seed, float t)
        {
            var wing = new List<UmbraQuill>();
            float wave = 2f * Mathf.PI * t;
            for (int k = 0; k < UmbraFlight; k++)
            {
                float s = 0.04f + 0.94f * k / (UmbraFlight - 1);
                UmbraEdgeAt(pts, s, sign, out var root, out var tan, out var n);
                int finger = Mathf.Max(0, k - (UmbraFlight - 1 - UmbraFingers));   // 1..5 at the wingtip
                float phi = (68f - 50f * Mathf.Pow(s, 1.1f) - 3f * finger + 2f * Mathf.Sin(wave + k * 1.3f + seed))
                            * Mathf.Deg2Rad;
                int fromTip = UmbraFlight - 1 - k;
                float graduated = fromTip == 0 ? 3f : fromTip == 1 ? 1.4f : fromTip == 2 ? 0.4f : 0f;
                wing.Add(new UmbraQuill
                {
                    Root = root + n * 0.6f,
                    Dir = tan * Mathf.Cos(phi) + n * Mathf.Sin(phi),
                    Length = 11.5f + 5f * Mathf.Pow(s, 1.3f) - graduated,
                    Half = finger == 0 ? 1.8f : Mathf.Lerp(1.5f, 1f, finger / (float)UmbraFingers),
                });
            }
            for (int k = 0; k < UmbraCoverts; k++)
            {
                float s = 0.02f + 0.74f * k / UmbraCoverts;
                UmbraEdgeAt(pts, s, sign, out var root, out var tan, out var n);
                float phi = (36f - 12f * s) * Mathf.Deg2Rad;
                wing.Add(new UmbraQuill
                {
                    Root = root + n * 0.4f, Dir = tan * Mathf.Cos(phi) + n * Mathf.Sin(phi),
                    Length = 4.4f, Half = 1.6f, Covert = true,
                });
            }
            return wing.ToArray();
        }

        /// <summary>The point <paramref name="s"/> of the way along a leading edge, its tangent
        /// toward the tip, and its outward normal.</summary>
        static void UmbraEdgeAt(Vector2[] pts, float s, float sign, out Vector2 at, out Vector2 tan, out Vector2 n)
        {
            float f = s * (pts.Length - 1);
            int i = Mathf.Min(pts.Length - 2, (int)f);
            at = Vector2.Lerp(pts[i], pts[i + 1], f - i);
            tan = (pts[i + 1] - pts[i]).normalized;
            n = new Vector2(-tan.y, tan.x) * sign;
        }

        static char UmbraTexel(float x, float y, bool fine)
        {
            float edge = fine ? 0.45f : 0.81f;   // about one drawn texel's worth of cell

            char skull = UmbraSkull(x, y, edge, fine);
            if (skull != '.') return skull;

            // ---- the wings: the last feather found lies on top ----
            for (int w = 0; w < 2; w++)
            {
                bool found = false, covert = false;
                float b = 0f, tt = 0f, hw = 0f;
                foreach (var q in _umbraWings[w])
                    if (UmbraFeatherAt(x, y, q, out float qb, out float qt, out float qh))
                    {
                        found = true; covert = q.Covert;
                        b = qb; tt = qt; hw = qh;
                    }
                if (found)
                    return UmbraFeatherTone(b, tt, hw, covert ? edge * 0.8f : edge, fine, covert,
                                            rootward: w == 0 ? 1f : -1f);
            }

            // ---- the string ----
            if (y > 12.5f && y < UmbraRows - 12.5f && Mathf.Abs(x - UmbraStringX) < (fine ? 0.25f : 0.5f))
                return '3';
            return '.';
        }

        /// <summary>Whether (x, y) is on feather <paramref name="q"/>: across it (b), along it as a
        /// fraction of its length (tt), and its half-width there (hw). A flight feather keeps its
        /// width most of its length and runs to a point; a covert ends rounded.</summary>
        static bool UmbraFeatherAt(float x, float y, UmbraQuill q, out float b, out float tt, out float hw)
        {
            float px = x - q.Root.x, py = y - q.Root.y;
            float a = px * q.Dir.x + py * q.Dir.y;
            b = -px * q.Dir.y + py * q.Dir.x;
            tt = a / q.Length;
            hw = 0f;
            if (a <= 0f || a >= q.Length) return false;
            float profile = q.Covert
                ? Mathf.Min(1f, Mathf.Sqrt(tt / 0.15f)) * Mathf.Sqrt(Mathf.Max(0f, 1f - tt * tt * tt))
                : Mathf.Min(1f, Mathf.Sqrt(tt / 0.1f)) * (1f - Mathf.Pow(Mathf.Max(0f, (tt - 0.55f) / 0.45f), 1.7f));
            hw = q.Half * Mathf.Min(1f, profile);
            return Mathf.Abs(b) < hw;
        }

        /// <summary>A feather's tone: dark along the edge lying over the one beneath it (the
        /// rootward side), the quill, a sheen beside the quill on the lit vane, the rest black.</summary>
        static char UmbraFeatherTone(float b, float tt, float hw, float edge, bool fine, bool covert, float rootward)
        {
            float bb = b * rootward;
            if (bb > hw - edge) return 'k';
            float quill = fine ? 0.22f : 0.42f;
            if (Mathf.Abs(b) < quill && tt < 0.85f) return covert ? 'b' : 'l';
            if (bb < 0f)
            {
                if (!covert && tt > 0.1f && tt < 0.75f && -bb < quill + (fine ? 0.45f : 0.55f)) return 'b';
                return 'd';
            }
            return 's';
        }

        /// <summary>
        /// A human skull after the user's sketch (2026-10-10): a tall dome, squarish sockets with
        /// their outer corners drooping under a heavy brow, an inverted-heart nose, the cheekbones,
        /// one row of UPPER teeth and no jaw - lit from the right, the left in hatched shadow, in
        /// sepia bone, all inside its own dark line. '.' when off it.
        ///
        /// The arena grid is laid out by hand through the numbers: sockets two cells square with a
        /// cell of bone each side of them (touching the rim, they ran together into one dark bar),
        /// one row of teeth above a cell of rim (two rows were twice the sketch's length), and the
        /// rim a whole cell (thinner, it broke where the dome met the face). Centred on a cell
        /// column (14.5) so it comes out symmetric.
        /// </summary>
        static char UmbraSkull(float x, float y, float edge, bool fine)
        {
            const float cx = 14.5f, bottom = 46.9f;
            if (!fine) edge = 1f;
            float dx = x - cx;
            float qx = dx / 4.95f, qy = (y - 39.6f) / 5.2f;
            float rc = Mathf.Sqrt(qx * qx + qy * qy);
            bool inDome = rc <= 1f && y <= 41f;
            float half = UmbraSkullHalf(y), low = bottom;
            bool inFace = half > 0f && Mathf.Abs(dx) <= half && y <= low;
            if (inFace && y > 45.2f && fine)                                    // the teeth's ragged ends
            {
                low = bottom - 0.2f * Mathf.Abs(Mathf.Sin(Mathf.PI * dx / 0.75f));
                inFace = y <= low;
            }
            if (!inDome && !inFace) return '.';
            bool rimDome = inDome && rc > 1f - edge / 5f;
            bool rimFace = inFace && (half - Mathf.Abs(dx) < edge * 0.9f || low - y < edge * 0.9f);
            if ((rimDome && !(inFace && !rimFace)) || (rimFace && !(inDome && !rimDome))) return 'K';

            // the sockets
            for (int side = -1; side <= 1; side += 2)
            {
                float ang = (fine ? -13f : 0f) * side * Mathf.Deg2Rad;
                float lx = x - (cx + side * (fine ? 2f : 1.5f)), ly = y - 41.7f;
                float rx = lx * Mathf.Cos(ang) - ly * Mathf.Sin(ang);
                float ry = lx * Mathf.Sin(ang) + ly * Mathf.Cos(ang);
                float ax = Mathf.Abs(rx) / (fine ? 1.65f : 1f), ay = Mathf.Abs(ry) / (fine ? 1.35f : 1.25f);
                float sq = ax * ax * ax + ay * ay * ay;
                if (sq < 1f)
                {
                    if (fine && sq > 0.62f && ry > 0f && side > 0) return 'D';      // the lit lower rim inside
                    return !fine || sq < 0.62f ? 'K' : 'S';
                }
                if (fine && sq < 1.5f && ry < -0.9f) return side < 0 ? 'D' : 'L';   // the brow
            }

            // the nose: an inverted heart
            float ny = (y - 43.3f) / 1.8f;
            if (ny >= 0f && ny <= 1f)
            {
                float w = 0.2f + 0.75f * Mathf.Pow(ny, 0.7f);
                if (ny > 0.75f) w *= 1f - 0.3f * (ny - 0.75f) / 0.25f;
                if (Mathf.Abs(dx) < w) return fine && ny > 0.55f && Mathf.Abs(dx) < 0.12f ? 'D' : 'K';
            }

            // the upper teeth
            if (y > 45.2f)
            {
                if (fine && y < 45.45f) return 'D';                                // the gum line
                bool gap = fine ? ((Mathf.FloorToInt(dx * 4f) % 3) + 3) % 3 == 0     // every third texel
                                : ((Mathf.FloorToInt(x) - Mathf.FloorToInt(cx)) % 2 + 2) % 2 == 0;
                if (gap) return 'S';
                return dx > 0f ? 'L' : 'B';
            }

            if (y > 43.6f && y < 45f && Mathf.Abs(dx) > 2.4f) return 'D';          // under the cheekbones

            float lit = qx * 0.75f - qy * 0.45f;
            if (lit < -0.5f)                                                        // the left, hatched
                return fine && Mathf.Repeat(x + y, 0.75f) < 0.25f ? 'S' : 'D';
            if (lit > 0f) return fine && lit > 0.45f ? 'H' : 'L';
            return 'B';
        }

        /// <summary>The skull's half-width below the dome: temples, the cheekbones' flare, the upper
        /// jaw narrowing to the teeth. 0 above the face.</summary>
        static float UmbraSkullHalf(float y)
        {
            if (y < 40f) return 0f;
            if (y < 42.4f) return Mathf.Lerp(4.75f, 4.35f, (y - 40f) / 2.4f);
            if (y < 43.6f) return 4.6f;
            if (y < 45f) return Mathf.Lerp(4.6f, 2.75f, Mathf.Pow((y - 43.6f) / 1.4f, 0.8f));
            return Mathf.Lerp(2.75f, 2.55f, (y - 45f) / 1.9f);
        }

        static Vector2[] UmbraCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            const int n = 40;
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, v = 1f - u;
                pts[i] = v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
            }
            return pts;
        }
    }
}
