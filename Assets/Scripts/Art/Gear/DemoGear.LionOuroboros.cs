using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the Aether Dual Discs: a red lion and a white lioness
    //
    // Named "Aether Dual Discs" by the user (built as "Lion Ouroboros", then "King and Queen" - now the
    // name of its weapon art; the code keeps the Lion names).
    // A Black Diamond disc pair, power 0 (the cosmetic weapons' rule), Kindled. The user's brief:
    // an ouroboros with a LION instead of a snake, one disc red and one white, cracked and
    // imperfect, showing the reactive energy inside - the Secret Fire, like the Aether set and
    // the Aether Longbow. Iterated as browser mock-ups first; every call below is the user's.
    //
    //   THE LION      a ring of its own body, head at the top facing clockwise, jaws CLOSED on its
    //                 own tail-tuft. A CAT's body, not a serpent's: deep chest, waist, haunch, then
    //                 a sudden drop to a thin rope tail - an even taper read as a dragon. The
    //                 muzzle is the long one (the user kept it over a short broad one). Paws tucked
    //                 under the chest and haunch.
    //   THE MANES     the red lion's: rounded LOCKS in two layers swept back (points read as a
    //                 dragon's frill), lean and ragged like Scar's. The WHITE is a LIONESS (the
    //                 user's call, after the pair was built): no mane - her head reads by its own
    //                 shape (slimmer muzzle, almond eye with a lash, both round ears, a neck) - and
    //                 a BUSHIER tuft to make up for it. Colours CROSSED: the red lion wears a white
    //                 mane, the lioness a red tuft. The mane is allowed past the ring.
    //   THE BLADE     GOLD along the lion's back (the ring's outer edge), from behind the mane to
    //                 the haunch, ONE width and ONE shading the whole way (dark root, gold, bright
    //                 honed edge), thinning to a point as it follows the haunch into the tail -
    //                 and on the lioness behind her head too, where no mane covers its end. Rim
    //                 chips nick the body, never the blade.
    //   THE STRUT     thin gold, across the hollow - kept so the fist holds the disc at its centre
    //                 like every other disc and no animation changes.
    //   THE FIRE      cracks, the lost segments (chunks broken out of the body) and the eyes are
    //                 mark texels: black at rest, lit in the attuned element on the one beat.
    //                 Networks of cracks run out of each lost segment along the body and to both
    //                 rims. The SCALE pattern the third mock-up had was removed - it read as a snake
    //                 wearing a mane.
    //
    // TWO POSES. In the hands the strut is level and the heads sit at 135 deg (red, main hand) and
    // 45 deg (white, off hand). Hung up to be looked at - the rack, the armoury wall, a gear card -
    // both turn (red 65, white 30 deg), so on the rack (the white behind, down and to the right)
    // the red lion looks down at the lioness, she looks back up, and her tuft shows between them
    // (GearItem.DisplayLayer).
    //
    // SIZE. The ring is 36 cells tall - the size the user moved discs to - against the disc
    // standard's 28, and the mane breaks past it; listed in DiscHeightExceptions until the
    // standard itself moves.
    //
    // ONE FIELD (LionAt), in its own units - the ring's outer edge at 14, so 28 across - turned and
    // mirrored per picture and sampled per cell for the arena and per quarter cell for the menu, so
    // no picture can drift from another. The white lion is the red mirrored, with its own cracks.
    public static partial class DemoGear
    {
        // ------------------------------------------------------------------ scale and poses

        const int LionRingCells = 36, LionCanvasCells = 40, LionMenuScale = 4;
        const float LionRingField = 28f;
        static float LionFieldPerCell => LionRingField / LionRingCells;

        const float LionHandRed = 135f, LionHandWhite = 45f;

        /// <summary>The heads' angles HUNG UP (the rack, the wall, cards). Facing each other at 50/50
        /// the lioness's tuft hid behind the red lion's head; turned to 65/30 it shows between
        /// them and the two still look at each other.</summary>
        const float LionDisplayRed = 65f, LionDisplayWhite = 30f;

        static Vector2Int LionGrip => new(LionCanvasCells / 2, LionCanvasCells / 2);
        static Vector2Int LionMenuGrip => new(LionGrip.x * LionMenuScale, LionGrip.y * LionMenuScale);

        // ------------------------------------------------------------------ the body's geometry

        /// <summary>The body's centre line, and the angles (deg, the LION frame: head at 90) where it
        /// starts behind the head and ends in the jaws.</summary>
        const float LionRc = 9.6f, LionBodyFrom = 116f, LionBodyTo = 410f, LionStrutAngle = -45f;

        /// <summary>The blade along the back, as shares of the body (0 behind the head .. 1 the
        /// tuft), its width, and how long its tail-end taper runs.</summary>
        const float LionBladeFrom = 0.03f, LionBladeTo = 0.68f, LionBladeW = 1.7f, LionBladeTail = 0.13f;

        static float LionSmooth(float a, float b, float t)
        {
            t = Mathf.Clamp01(t);
            return a + (b - a) * t * t * (3f - 2f * t);
        }

        /// <summary>The body's half-width along its length: chest, waist, haunch, rope tail.</summary>
        static float LionWidth(float s)
            => s < 0.08f ? LionSmooth(3.0f, 3.45f, s / 0.08f)
             : s < 0.24f ? 3.45f
             : s < 0.40f ? LionSmooth(3.45f, 2.3f, (s - 0.24f) / 0.16f)
             : s < 0.50f ? LionSmooth(2.3f, 3.0f, (s - 0.40f) / 0.10f)
             : s < 0.60f ? 3.0f
             : s < 0.68f ? LionSmooth(3.0f, 0.62f, (s - 0.60f) / 0.08f)
             : 0.62f;

        /// <summary>The chest and belly hang a little toward the hollow; the tail runs on the line.</summary>
        static float LionCentre(float s)
            => s < 0.6f ? LionRc - 0.35f * Mathf.Sin(Mathf.Min(1f, s / 0.6f) * Mathf.PI) : LionRc;

        // ------------------------------------------------------------------ shape primitives
        //
        // Each answers whether (x, y) is inside it and, if so, the surface normal there - the
        // shading is the light against the normal, so every part is lit from one place.

        static readonly Vector3 LionLight = new Vector3(-0.5f, 0.62f, 0.6f).normalized;

        static Vector2 LionP(float deg, float r)
            => new(Mathf.Cos(deg * Mathf.Deg2Rad) * r, Mathf.Sin(deg * Mathf.Deg2Rad) * r);

        static void LionPolar(float x, float y, out float r, out float a)
        {
            r = Mathf.Sqrt(x * x + y * y);
            a = Mathf.Atan2(y, x) * Mathf.Rad2Deg;
            if (a < 0f) a += 360f;
        }

        /// <summary>0..4, darkest to lightest, after <paramref name="shift"/> steps.</summary>
        static int LionTone(Vector3 n, int shift)
        {
            float d = Vector3.Dot(n, LionLight);
            int t = d < 0.12f ? 0 : d < 0.42f ? 1 : d < 0.7f ? 2 : d < 0.9f ? 3 : 4;
            return Mathf.Clamp(t + shift, 0, 4);
        }

        struct LionBand { public float S, U, A, W; }

        static bool LionInBody(float x, float y, out Vector3 n, out LionBand b)
        {
            n = default; b = default;
            LionPolar(x, y, out float r, out float a);
            while (a < LionBodyFrom) a += 360f;
            if (a > LionBodyTo) return false;
            float s = (a - LionBodyFrom) / (LionBodyTo - LionBodyFrom), w = LionWidth(s), u = (r - LionCentre(s)) / w;
            if (Mathf.Abs(u) > 1f) return false;
            float c = Mathf.Cos(a * Mathf.Deg2Rad), sn = Mathf.Sin(a * Mathf.Deg2Rad);
            n = new Vector3(c * u, sn * u, Mathf.Sqrt(1f - u * u)).normalized;
            b = new LionBand { S = s, U = u, A = a, W = w };
            return true;
        }

        static bool LionEll(float x, float y, float cx, float cy, float rx, float ry, float rotDeg, out Vector3 n)
        {
            n = default;
            float rot = rotDeg * Mathf.Deg2Rad, c = Mathf.Cos(rot), s = Mathf.Sin(rot);
            float dx = x - cx, dy = y - cy, lx = dx * c + dy * s, ly = -dx * s + dy * c;
            float q = lx * lx / (rx * rx) + ly * ly / (ry * ry);
            if (q > 1f) return false;
            float nx = lx / rx, ny = ly / ry;
            n = new Vector3(nx * c - ny * s, nx * s + ny * c, Mathf.Sqrt(1f - q) * 1.2f).normalized;
            return true;
        }

        static bool LionEll(float x, float y, float cx, float cy, float rx, float ry, float rotDeg = 0f)
            => LionEll(x, y, cx, cy, rx, ry, rotDeg, out _);

        static float LionSegDist(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            var ab = b - a;
            float l2 = ab.sqrMagnitude;
            t = l2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        static bool LionCapsule(float x, float y, Vector2 a, Vector2 b, float hw, out Vector3 n)
        {
            n = default;
            var p = new Vector2(x, y);
            if (LionSegDist(p, a, b, out _) > hw) return false;
            var ab = b - a;
            var perp = new Vector2(-ab.y, ab.x).normalized;
            float side = Mathf.Clamp(Vector2.Dot(p - a, perp) / hw, -1f, 1f);
            n = new Vector3(perp.x * side, perp.y * side, Mathf.Sqrt(1f - side * side)).normalized;
            return true;
        }

        // ------------------------------------------------------------------ what a point is

        enum LionKind : byte { None, Body, Mane, Detail, Mark, Blade, Gold }

        struct LionHit
        {
            public LionKind Kind;
            public Vector3 N;
            public int Shift, Mark;
            public bool InBody;
            public LionBand Band;
            public float BladeR, BladeW;
        }

        static LionHit LionMat(Vector3 n, int shift = 0) => new() { Kind = LionKind.Body, N = n, Shift = shift };

        /// <summary>A mane: how many locks, how long, how full, which way they sweep, and how
        /// RAGGED (how much each lock's length varies) - the red's lean and ragged, the white's
        /// full and even.</summary>
        struct LionMane { public int Locks; public float Amp, R0, Phase, Curl, Cx, Cy, Rag; }

        static readonly LionMane LionRedMane = new()
            { Locks = 10, Amp = 3.2f, R0 = 4.2f, Phase = 0.18f, Curl = -0.07f, Cx = -1.2f, Cy = 10.4f, Rag = 0.6f };
        static readonly LionMane LionWhiteMane = new()
            { Locks = 13, Amp = 1.9f, R0 = 5.4f, Phase = 0f, Curl = -0.02f, Cx = -0.9f, Cy = 10.3f, Rag = 0.15f };

        static float LionHash(int i)
        {
            double v = System.Math.Sin(i * 127.1 + 311.7) * 43758.5453;
            return (float)(v - System.Math.Floor(v));
        }

        /// <summary>
        /// ROUNDED LOCKS: each a round-tipped clump (a semicircle profile, never a point), in two
        /// layers offset by half a lock, the back one a step darker and a dark seam between locks,
        /// all swept back by the curl.
        /// </summary>
        static bool LionLocks(float x, float y, LionMane m, out Vector3 n, out int shift)
        {
            n = default; shift = 0;
            float dx = x - m.Cx, dy = y - m.Cy, d = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx) / (2f * Mathf.PI);

            bool Layer(float r0, float amp, float phase, out float f, out float rad, out bool seam)
            {
                float ph = ang * m.Locks + phase + m.Curl * 1.15f * Mathf.Max(0f, d - r0 * 0.6f);
                int k = Mathf.FloorToInt(ph);
                f = ph - k;
                float bump = Mathf.Sqrt(Mathf.Max(0f, 1f - (2f * f - 1f) * (2f * f - 1f)));
                float a = amp * (1f - m.Rag + m.Rag * LionHash(((k % m.Locks) + m.Locks) % m.Locks));
                float R = r0 + a * bump;
                rad = d / R;
                seam = Mathf.Min(f, 1f - f) < 0.13f && d > r0 * 0.75f;
                return d <= R;
            }

            float lf, lrad; bool lseam;
            if (!Layer(m.R0 * 0.86f, m.Amp * 0.8f, m.Phase + 0.5f, out lf, out lrad, out lseam))
            {
                if (!Layer(m.R0, m.Amp, m.Phase, out lf, out lrad, out lseam)) return false;
                shift = -1;
            }
            if (lseam) shift -= 1;
            float rx = d > 0f ? dx / d : 0f, ry = d > 0f ? dy / d : 0f, ridge = (lf - 0.5f) * 1.3f;
            n = new Vector3(rx * lrad * 0.6f - ry * ridge, ry * lrad * 0.6f + rx * ridge, 1f - lrad * 0.35f).normalized;
            return true;
        }

        /// <summary>The head, jaws closed on the tuft: eye (a mark - it burns), nose, mouth line,
        /// the tuft in the mane's colour, muzzle, jaw, face, ear.</summary>
        static LionHit LionHead(float x, float y)
        {
            Vector3 n;
            if (LionEll(x, y, 2.25f, 11.15f, 0.82f, 0.55f, -12f)) return new LionHit { Kind = LionKind.Mark, Mark = 0 };
            if (LionEll(x, y, 6.35f, 10.0f, 0.62f, 0.52f)) return new LionHit { Kind = LionKind.Detail };
            if (LionCapsule(x, y, new Vector2(3.4f, 8.3f), new Vector2(6.6f, 8.5f), 0.3f, out _))
                return new LionHit { Kind = LionKind.Detail };
            if (LionEll(x, y, 7.6f, 7.9f, 1.95f, 1.45f, -35f, out n)) return new LionHit { Kind = LionKind.Mane, N = n };
            if (LionEll(x, y, 4.35f, 9.3f, 2.45f, 1.75f, 4f, out n)) return LionMat(n, 1);
            if (LionEll(x, y, 3.6f, 7.75f, 1.9f, 0.8f, -8f, out n)) return LionMat(n);
            if (LionEll(x, y, 1.05f, 10.3f, 3.4f, 3.1f, 0f, out n)) return LionMat(n);
            if (LionEll(x, y, -0.6f, 13.6f, 1.25f, 1.4f, 20f, out n)) return LionMat(n);
            return default;
        }

        /// <summary>
        /// The tuft a lioness carries in place of a mane: BUSHIER - a pom of rounded clumps (the
        /// mane's own lock shape, small and swept back along the tail) - still clenched in her jaws
        /// and still in the other lion's colour.
        /// </summary>
        static readonly LionMane LionessTuft = new()
            { Locks = 8, Amp = 1.3f, R0 = 1.6f, Phase = 0.1f, Curl = -0.12f, Cx = 7.7f, Cy = 7.9f, Rag = 0.3f };

        /// <summary>
        /// The LIONESS's head (the white, by the user's call): no mane, so the head reads by its own
        /// shape - ONE long head, the brow running straight down the nose (separate face and muzzle
        /// shapes each caught a highlight and stacked like a snowman), an almond eye with a flick of
        /// lash at its outer corner, both ears showing, set back on the head (the far one a step
        /// darker; big round ears on top read as a bear). LionessNeck carries the body up under it
        /// where a mane would have covered the join.
        /// </summary>
        static LionHit LionessHead(float x, float y)
        {
            Vector3 n;
            if (LionEll(x, y, 2.75f, 10.95f, 0.92f, 0.48f, -14f)) return new LionHit { Kind = LionKind.Mark, Mark = 0 };
            if (LionCapsule(x, y, new Vector2(1.9f, 11.3f), new Vector2(1.2f, 11.85f), 0.2f, out _))
                return new LionHit { Kind = LionKind.Detail };                                   // the lash
            if (LionEll(x, y, 6.75f, 10.0f, 0.55f, 0.45f)) return new LionHit { Kind = LionKind.Detail };
            if (LionCapsule(x, y, new Vector2(4.3f, 8.75f), new Vector2(6.75f, 8.95f), 0.26f, out _))
                return new LionHit { Kind = LionKind.Detail };
            if (LionLocks(x, y, LionessTuft, out n, out int tuftShift))
                return new LionHit { Kind = LionKind.Mane, N = n, Shift = tuftShift };
            if (LionEll(x, y, 4.4f, 8.25f, 2.1f, 0.62f, -6f, out n)) return LionMat(n);         // jaw
            if (LionEll(x, y, 2.6f, 10.0f, 4.4f, 2.3f, -8f, out n)) return LionMat(n);
            if (LionEll(x, y, -0.6f, 12.45f, 0.4f, 0.62f, 30f, out n)) return LionMat(n, -2);   // near ear's hollow
            if (LionEll(x, y, -0.65f, 12.55f, 0.88f, 1.2f, 30f, out n)) return LionMat(n);
            if (LionEll(x, y, 0.5f, 12.7f, 0.68f, 0.98f, 10f, out n)) return LionMat(n, -1);   // far ear
            return default;
        }

        /// <summary>The lioness's NECK: the body's own curve carried on up under the head,
        /// narrowing - shaded as the body is, so it flows into the chest instead of bulging.</summary>
        static bool LionessNeck(float x, float y, out Vector3 n)
        {
            n = default;
            LionPolar(x, y, out float r, out float a);
            if (a < 88f || a > 118f) return false;
            float t = (a - 88f) / 30f, w = Mathf.Lerp(1.9f, 3.0f, t * t), u = (r - LionCentre(0f)) / w;
            if (Mathf.Abs(u) > 1f) return false;
            float c = Mathf.Cos(a * Mathf.Deg2Rad), sn = Mathf.Sin(a * Mathf.Deg2Rad);
            n = new Vector3(c * u, sn * u, Mathf.Sqrt(1f - u * u)).normalized;
            return true;
        }

        /// <summary>A paw: a pad with three toe lines across its front.</summary>
        static LionHit LionPaw(float x, float y, Vector2 at, float rotDeg, int shift)
        {
            float c = Mathf.Cos(rotDeg * Mathf.Deg2Rad), s = Mathf.Sin(rotDeg * Mathf.Deg2Rad);
            foreach (float t in new[] { -0.45f, 0.05f, 0.55f })
            {
                var a = new Vector2(at.x + c * 0.75f - s * t * 1.3f, at.y + s * 0.75f + c * t * 1.3f);
                if (LionCapsule(x, y, a, a + new Vector2(c, s) * 0.7f, 0.13f, out _)) return new LionHit { Kind = LionKind.Detail };
            }
            return LionEll(x, y, at.x, at.y, 1.5f, 1.2f, rotDeg, out var n) ? LionMat(n, shift + 1) : default;
        }

        static LionHit LionLeg(float x, float y, Vector2 from, Vector2 to, float hw, int shift = 0)
            => LionCapsule(x, y, from, to, hw, out var n) ? LionMat(n, shift) : default;

        /// <summary>Forepaws folded under the chest (the far one a step darker), a hind paw under
        /// the haunch. Drawn under the body, so each leg's root hides in it.</summary>
        static LionHit LionPaws(float x, float y)
        {
            LionHit h;
            if ((h = LionPaw(x, y, LionP(170f, 5.4f), 80f, 0)).Kind != LionKind.None) return h;
            if ((h = LionLeg(x, y, LionP(150f, 8.2f), LionP(166f, 5.8f), 1.25f)).Kind != LionKind.None) return h;
            if ((h = LionPaw(x, y, LionP(150f, 4.9f), 60f, -1)).Kind != LionKind.None) return h;
            if ((h = LionLeg(x, y, LionP(136f, 8.0f), LionP(147f, 5.4f), 1.15f, -1)).Kind != LionKind.None) return h;
            if ((h = LionPaw(x, y, LionP(282f, 5.8f), 192f, 0)).Kind != LionKind.None) return h;
            return LionLeg(x, y, LionP(258f, 8.2f), LionP(277f, 6.2f), 1.35f);
        }

        /// <summary>The blade standing off the back - see the header.</summary>
        static LionHit LionBladeAt(float x, float y, bool taperFront = false)
        {
            LionPolar(x, y, out float r, out float a);
            while (a < LionBodyFrom) a += 360f;
            if (a > LionBodyTo) return default;
            float s = (a - LionBodyFrom) / (LionBodyTo - LionBodyFrom);
            if (s < LionBladeFrom || s > LionBladeTo) return default;
            // Thinning to a point down the haunch - and, on the lioness, behind the head too: with
            // no mane over it the square front end stood exposed above her neck.
            float taper = Mathf.Min(1f, (LionBladeTo - s) / LionBladeTail);
            if (taperFront) taper = Mathf.Min(taper, (s - LionBladeFrom) / LionBladeTail);
            float baseR = LionCentre(s) + LionWidth(s) - 0.35f, bw = LionBladeW * taper;
            float v = (r - baseR) / Mathf.Max(bw, 0.01f);
            if (v < 0f || v > 1f || bw < 0.12f) return default;
            return new LionHit { Kind = LionKind.Blade, BladeR = r - baseR, BladeW = bw };
        }

        /// <summary>The whole lion at (x, y) in the lion frame, front to back.</summary>
        static LionHit LionAt(float x, float y, LionSide side)
        {
            LionHit h;
            Vector3 n;
            if (side.Lioness)
            {
                if ((h = LionessHead(x, y)).Kind != LionKind.None) return h;
                if (LionessNeck(x, y, out n)) return LionMat(n);
            }
            else
            {
                if ((h = LionHead(x, y)).Kind != LionKind.None) return h;
                if (LionLocks(x, y, side.Mane, out n, out int shift)) return new LionHit { Kind = LionKind.Mane, N = n, Shift = shift };
            }
            if (LionInBody(x, y, out n, out var band)) return new LionHit { Kind = LionKind.Body, N = n, InBody = true, Band = band };
            if ((h = LionPaws(x, y)).Kind != LionKind.None) return h;
            if ((h = LionBladeAt(x, y, side.Lioness)).Kind != LionKind.None) return h;
            var strut = LionP(LionStrutAngle, 9.0f);
            if (LionCapsule(x, y, -strut, strut, 0.62f, out n)) return new LionHit { Kind = LionKind.Gold, N = n };
            return default;
        }

        // ------------------------------------------------------------------ lost segments, cracks

        /// <summary>A chunk broken out of the body: centred at angle A (deg, lion frame), Len long,
        /// on the outer (Side 1) or inner (-1) half.</summary>
        struct LionLost { public float A, Len; public int Side; }

        /// <summary>The mark letter a lost segment paints here, or '\0' outside every one: a dark
        /// lip round a pocket of the fire, hottest in its middle, its edge broken by a wobble.</summary>
        static char LionLostAt(LionBand b, LionLost[] lost, float step)
        {
            foreach (var L in lost)
            {
                float da = Mathf.DeltaAngle(L.A, b.A);
                float along = Mathf.Abs(da) * Mathf.Deg2Rad * LionRc, half = L.Len / 2f;
                float u0 = L.Side > 0 ? -0.05f : -1f, u1 = L.Side > 0 ? 1f : 0.05f;
                float wob = (Mathf.Sin(b.A * 0.9f + L.A) * 0.5f + Mathf.Sin(b.A * 2.3f + b.U * 4f + L.A) * 0.35f) * 0.55f;
                float edge = Mathf.Min(half + wob - along, Mathf.Min(b.U - u0, u1 - b.U) * b.W + wob * 0.6f);
                if (edge < 0f || b.W < 1.2f) continue;
                if (edge < step * 0.65f) return 'z';
                if (edge > step * 1.8f && along < half * 0.45f) return SecretFire.Core;
                return edge < step * 1.4f ? SecretFire.Ember : SecretFire.Vein;
            }
            return '\0';
        }

        /// <summary>A crack: a jagged polyline, hot and wide at its root, a hairline at its tip.</summary>
        sealed class LionCrack
        {
            public Vector2[] P;
            public float[] Cum;
            public float Len, Hw0, Hw1;
            public Rect Bounds;
        }

        /// <summary>The mock-ups' seeded generator (mulberry32), so the cracks fall where they did.</summary>
        static System.Func<float> LionRng(int seed)
        {
            uint s = unchecked((uint)seed);
            return () =>
            {
                unchecked
                {
                    s += 0x6D2B79F5u;
                    uint t = (s ^ (s >> 15)) * (1u | s);
                    t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                    return (float)((t ^ (t >> 14)) / 4294967296.0);
                }
            };
        }

        static LionCrack LionCrackOf(Vector2[] pts, int seed, float hw0, float hw1, float jag)
        {
            var R = LionRng(seed);
            var p = new List<Vector2> { pts[0] };
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 a = pts[i - 1], b = pts[i];
                float L = Vector2.Distance(a, b);
                int k = Mathf.Max(1, Mathf.RoundToInt(L / 1.3f));
                var perp = new Vector2(-(b.y - a.y), b.x - a.x) / Mathf.Max(L, 1e-5f);
                for (int j = 1; j <= k; j++)
                {
                    float t = j / (float)k, o = j < k ? (R() * 2f - 1f) * jag : 0f;
                    p.Add(Vector2.Lerp(a, b, t) + perp * o);
                }
            }
            var cum = new float[p.Count];
            float minX = p[0].x, maxX = p[0].x, minY = p[0].y, maxY = p[0].y;
            for (int i = 1; i < p.Count; i++)
            {
                cum[i] = cum[i - 1] + Vector2.Distance(p[i - 1], p[i]);
                minX = Mathf.Min(minX, p[i].x); maxX = Mathf.Max(maxX, p[i].x);
                minY = Mathf.Min(minY, p[i].y); maxY = Mathf.Max(maxY, p[i].y);
            }
            float pad = Mathf.Max(hw0, hw1) + 0.5f;
            return new LionCrack
            {
                P = p.ToArray(), Cum = cum, Len = cum[^1], Hw0 = hw0, Hw1 = hw1,
                Bounds = Rect.MinMaxRect(minX - pad, minY - pad, maxX + pad, maxY + pad),
            };
        }

        /// <summary>0 core, 1 vein, 2 ember - or -1 off the crack. Never thinner than one texel.</summary>
        static int LionCrackAt(Vector2 q, LionCrack c, float step)
        {
            if (!c.Bounds.Contains(q)) return -1;
            float best = float.MaxValue, bt = 0f;
            for (int i = 1; i < c.P.Length; i++)
            {
                float d = LionSegDist(q, c.P[i - 1], c.P[i], out float t);
                if (d < best) { best = d; bt = (c.Cum[i - 1] + t * (c.Cum[i] - c.Cum[i - 1])) / c.Len; }
            }
            float hw = Mathf.Max(Mathf.Lerp(c.Hw0, c.Hw1, bt), step * 0.5f);
            if (best > hw) return -1;
            return bt < 0.16f ? 0 : bt > 0.7f ? 2 : 1;
        }

        static Vector2[] LionPolarPts(params (float A, float R)[] pts)
        {
            var o = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) o[i] = LionP(pts[i].A, pts[i].R);
            return o;
        }

        /// <summary>A NETWORK from one hot root: a main crack along the body each way, a branch to
        /// each rim, a fork or two.</summary>
        static IEnumerable<LionCrack> LionNetwork(float a, float r, int seed, float scale = 1f)
        {
            var R = LionRng(seed);
            float J() => R() * 2f - 1f;
            yield return LionCrackOf(LionPolarPts((a, r), (a + 8 * scale, r + J() * 0.9f), (a + 16 * scale, r + J() * 1.0f), (a + 24 * scale, r + J() * 0.9f)), seed + 1, 0.4f, 0.18f, 0.5f);
            yield return LionCrackOf(LionPolarPts((a, r), (a - 7 * scale, r + J() * 0.9f), (a - 15 * scale, r + J() * 0.8f)), seed + 2, 0.36f, 0.18f, 0.45f);
            yield return LionCrackOf(LionPolarPts((a + 4, r), (a + 6 + J() * 3, (r + 14.2f) / 2), (a + 5 + J() * 4, 14.2f)), seed + 3, 0.32f, 0.18f, 0.35f);
            yield return LionCrackOf(LionPolarPts((a - 3, r), (a - 4 + J() * 3, (r + 7.0f) / 2), (a - 2 + J() * 4, 7.0f)), seed + 4, 0.32f, 0.18f, 0.35f);
            yield return LionCrackOf(LionPolarPts((a + 13 * scale, r), (a + 17 * scale, r + 1.6f), (a + 20 * scale, r + 2.6f)), seed + 5, 0.28f, 0.18f, 0.3f);
            yield return LionCrackOf(LionPolarPts((a - 9 * scale, r), (a - 12 * scale, r - 1.5f)), seed + 6, 0.28f, 0.18f, 0.25f);
        }

        sealed class LionSide
        {
            public LionMane Mane;
            /// <summary>The white is a LIONESS: no mane, her own head, a bushier tuft.</summary>
            public bool Lioness;
            public LionLost[] Lost;
            public LionCrack[] Cracks;
            public Vector2[] Chips;
        }

        // Lazy: static data in the partial files must not depend on another file's statics being
        // initialised first (Tria Prima's note).
        static LionSide _lionRed, _lionWhite;

        static LionSide LionRedSide => _lionRed ??= new LionSide
        {
            Mane = LionRedMane,
            Lost = new[] { new LionLost { A = 175f, Len = 3.4f, Side = 1 }, new LionLost { A = 268f, Len = 2.8f, Side = -1 } },
            Cracks = LionConcat(LionNetwork(175f, 11.5f, 300), LionNetwork(268f, 9.9f, 310), LionNetwork(140f, 10.8f, 330, 0.6f),
                            LionNetwork(232f, 10.4f, 320, 0.5f),
                            new[] { LionCrackOf(new[] { new Vector2(-2.2f, 14.6f), new Vector2(-0.2f, 12.4f), new Vector2(2.3f, 11.0f) }, 14, 0.36f, 0.3f, 0.3f) }),
            Chips = new[] { LionP(158f, 14.4f), LionP(232f, 14.4f) },
        };

        static LionSide LionWhiteSide => _lionWhite ??= new LionSide
        {
            Mane = LionWhiteMane,
            Lioness = true,
            Lost = new[] { new LionLost { A = 214f, Len = 3.2f, Side = -1 }, new LionLost { A = 280f, Len = 2.6f, Side = 1 } },
            Cracks = LionConcat(LionNetwork(214f, 9.8f, 400), LionNetwork(280f, 11.4f, 410, 0.8f), LionNetwork(160f, 10.9f, 420, 0.7f),
                            LionNetwork(248f, 10.6f, 430, 0.5f),
                            new[] { LionCrackOf(new[] { new Vector2(3.1f, 6.6f), new Vector2(1.9f, 8.6f), new Vector2(2.3f, 11.0f) }, 24, 0.36f, 0.3f, 0.3f) }),
            Chips = new[] { LionP(200f, 14.4f), LionP(165f, 14.5f) },
        };

        static LionCrack[] LionConcat(params IEnumerable<LionCrack>[] parts)
        {
            var all = new List<LionCrack>();
            foreach (var p in parts) all.AddRange(p);
            return all.ToArray();
        }

        // ------------------------------------------------------------------ sampling

        /// <summary>
        /// One picture: the <paramref name="white"/> or red lion, its head turned to
        /// <paramref name="headAngle"/> (deg, the DISC frame; the white mirrored), at
        /// <paramref name="scale"/> texels per cell. Letters: a-e the body ramp, f-j the mane's,
        /// k-o gold, z the dark detail, and the Secret Fire's three marks.
        /// </summary>
        static string[] LionSample(bool white, int scale, float headAngle, bool spirit = false)
        {
            var side = white ? LionWhiteSide : LionRedSide;
            float step = LionFieldPerCell / scale, half = LionCanvasCells * LionFieldPerCell / 2f;
            float turn = (white ? 180f - headAngle : headAngle) - 90f;
            float c = Mathf.Cos(-turn * Mathf.Deg2Rad), s = Mathf.Sin(-turn * Mathf.Deg2Rad);
            float cb = Mathf.Cos(turn * Mathf.Deg2Rad), sb = Mathf.Sin(turn * Mathf.Deg2Rad);
            int n = LionCanvasCells * scale;
            var rows = new string[n];
            var line = new char[n];

            Vector3 Back(Vector3 v)
            {
                var o = new Vector3(v.x * cb - v.y * sb, v.x * sb + v.y * cb, v.z);
                if (white) o.x = -o.x;
                return o;
            }

            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    float dx = -half + (i + 0.5f) * step, dy = half - (j + 0.5f) * step;
                    float mx = white ? -dx : dx;
                    float lx = mx * c - dy * s, ly = mx * s + dy * c;
                    var q = new Vector2(lx, ly);

                    if (spirit) { line[i] = LionSpiritAt(lx, ly, q, side, step, Back); continue; }

                    bool chipped = false;
                    foreach (var chip in side.Chips) if (Vector2.Distance(q, chip) < 1.45f) { chipped = true; break; }
                    // Chips nick the body, never the blade: the gold edge runs unbroken.
                    var h = chipped ? LionBladeAt(lx, ly, side.Lioness) : LionAt(lx, ly, side);
                    line[i] = LionLetter(h, q, side, step, Back);
                }
                rows[j] = new string(line);
            }
            return rows;
        }

        /// <summary>
        /// The SPIRIT (GearSpirit): the stone body gone, the head - and the red lion's mane - left
        /// standing on a RING OF ENERGY where the body's centre line ran. The ring is the Secret
        /// Fire's marks (core along its middle, vein, ember at its edges, its width breathing round
        /// the circle), so it burns in the attuned element like every other mark on the pair.
        /// </summary>
        static char LionSpiritAt(float x, float y, Vector2 q, LionSide side, float step, System.Func<Vector3, Vector3> back)
        {
            LionHit h;
            if (side.Lioness)
            {
                if ((h = LionessHead(x, y)).Kind != LionKind.None) return LionLetter(h, q, side, step, back);
            }
            else
            {
                if ((h = LionHead(x, y)).Kind != LionKind.None) return LionLetter(h, q, side, step, back);
                if (LionLocks(x, y, side.Mane, out var n, out int shift))
                    return LionLetter(new LionHit { Kind = LionKind.Mane, N = n, Shift = shift }, q, side, step, back);
            }
            float a = Mathf.Atan2(y, x);
            float d = Mathf.Abs(Mathf.Sqrt(x * x + y * y) - LionRc);
            float w = 1f + 0.22f * Mathf.Sin(a * 5f) + 0.12f * Mathf.Sin(a * 13f + 1.3f);
            if (d < 0.35f * w) return SecretFire.Core;
            if (d < 0.8f * w) return SecretFire.Vein;
            if (d < 1.2f * w) return SecretFire.Ember;
            return '.';
        }

        static char LionLetter(LionHit h, Vector2 q, LionSide side, float step, System.Func<Vector3, Vector3> back)
        {
            switch (h.Kind)
            {
                case LionKind.None: return '.';
                case LionKind.Detail: return 'z';
                case LionKind.Mark: return LionMarkLetter(h.Mark);
                case LionKind.Blade:
                    // UNIFORM: a dark root on the back, the gold, the honed edge bright - the same
                    // three bands the whole way, so it reads as one even band.
                    return h.BladeW - h.BladeR < step * 0.95f ? 'o' : h.BladeR < step * 0.9f ? 'l' : 'n';
                case LionKind.Gold: return (char)('k' + LionTone(back(h.N), 0));
            }

            if (h.InBody)
            {
                char lost = LionLostAt(h.Band, side.Lost, step);
                if (lost != '\0') return lost;
            }
            if (h.Kind != LionKind.Mane)
            {
                int mk = -1;
                foreach (var k in side.Cracks)
                {
                    int m = LionCrackAt(q, k, step);
                    if (m >= 0 && (mk < 0 || m < mk)) mk = m;
                }
                if (mk >= 0) return LionMarkLetter(mk);
            }
            int tone = LionTone(back(h.N), h.Shift);
            return (char)((h.Kind == LionKind.Mane ? 'f' : 'a') + tone);
        }

        static char LionMarkLetter(int tone) => tone switch
        {
            0 => SecretFire.Core,
            1 => SecretFire.Vein,
            _ => SecretFire.Ember,
        };

        // ------------------------------------------------------------------ colour

        static Color LionHex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static readonly string[] LionRedRamp = { "#3a0a10", "#6a1119", "#9b1d1f", "#c73a2b", "#ec6c46" };
        static readonly string[] LionWhiteRamp = { "#5b5664", "#8c8692", "#bbb4b6", "#e0dad0", "#fbf6ea" };
        static readonly string[] LionGoldRamp = { "#4a2a06", "#80500e", "#b98420", "#e6b947", "#fff0a6" };

        /// <summary>The body's ramp, the mane's (CROSSED - the other lion's body), gold, the detail
        /// (the outline's colour) and the Secret Fire's marks, kindled last.</summary>
        static Dictionary<char, Color> LionPalette(bool white)
        {
            var body = white ? LionWhiteRamp : LionRedRamp;
            var mane = white ? LionRedRamp : LionWhiteRamp;
            var pal = new Dictionary<char, Color>();
            for (int i = 0; i < 5; i++)
            {
                pal[(char)('a' + i)] = LionHex(body[i]);
                pal[(char)('f' + i)] = LionHex(mane[i]);
                pal[(char)('k' + i)] = LionHex(LionGoldRamp[i]);
            }
            pal['z'] = LionOutline(white);
            return SecretFire.Kindle(pal);
        }

        static Color LionOutline(bool white) => LionHex(white ? "#1b1820" : "#170306");

        // ------------------------------------------------------------------ the item

        static GearItem LionOuroboros()
        {
            const string key = "gear.weapon.disc.king_and_queen";

            LayerSprite Arena(string k, bool white, float angle, bool spirit = false)
                => Pixels(RigLayer.Weapon, k, LionSample(white, 1, angle, spirit), LionPalette(white), DiscHandX, DiscHandY,
                          pivotTexel: LionGrip, ppu: FinePpu, outlineColor: LionOutline(white), upscale2x: true);
            LayerSprite Menu(string k, bool white, float angle)
                => Pixels(RigLayer.Weapon, k, LionSample(white, LionMenuScale, angle), LionPalette(white), DiscHandX, DiscHandY,
                          pivotTexel: LionMenuGrip, ppu: MenuPpu, outlineColor: LionOutline(white));

            // "Aether Dual Discs" by the user's call (built as "Lion Ouroboros", then "King and Queen", the name
            // its weapon art now carries) - the red king and the
            // white queen of the alchemists' wedding.
            var item = Disc(WithMenu(Make("king_and_queen_discs", "Aether Dual Discs", GearSlot.Weapon, LootTier.BlackDiamond, 0f,
                                          Arena(key, false, LionHandRed)),
                                     Menu(key + ".menu", false, LionHandRed)));

            // The off hand holds the WHITE lion - its own picture (the Armillary's mechanism).
            item.OffhandLayer = Arena(key + ".off", true, LionHandWhite);
            item.OffhandMenuLayer = Menu(key + ".off.menu", true, LionHandWhite);

            // Hung up, both turn so the heads face each other across the pair - see the header.
            item.DisplayLayer = Arena(key + ".display", false, LionDisplayRed);
            item.DisplayMenuLayer = Menu(key + ".display.menu", false, LionDisplayRed);
            item.OffhandDisplayLayer = Arena(key + ".off.display", true, LionDisplayWhite);
            item.OffhandDisplayMenuLayer = Menu(key + ".off.display.menu", true, LionDisplayWhite);

            item.SignatureFinisher = KingAndQueenArtId;

            // Each hand's SPIRIT - the heads on their ring of energy, what is left in the art once
            // the stone bodies fall away (GearSpirit, the user's call).
            GearSpirit.Register(item.Layers[0].Sprite, Arena(key + ".spirit", false, LionHandRed, spirit: true).Sprite);
            GearSpirit.Register(item.OffhandLayer.Sprite, Arena(key + ".off.spirit", true, LionHandWhite, spirit: true).Sprite);
            return Kindle(item);
        }

        // ------------------------------------------------------------------ the stone
        //
        // The Aether Dual Discs' RELIC: a Philosopher's Stone, the GEODE (the user's pick for the discs,
        // 2026-10-07 - the cracked shard is the Aether Greatsword's, the liquid the Aether Longbow's).
        // A red rind broken open on crystals; the CRYSTALS are the glow (the Secret Fire's liquid
        // tones, so they ebb but never go dark - SecretFire.LiquidFloor), the rind stays red, and a
        // DEEP red band runs between them (the user's change from pale). "Geode Stone", not
        // "Geode": the Diamond armour set already has that name. It carries the CONIUNCTIO.

        public const string KingAndQueenArtId = "king_and_queen_art";
        public const string GeodeStoneId = "geode_stone";

        // 8 x 14, the user's belt grid: 1-4 the rind dark to light, r the band, a/b/c the crystals
        // (shade, body, light) and s a sparkle (their highlight).
        static readonly string[] GeodeStoneRows =
        {
            "...cs3..",
            "..cbr43.",
            ".scbr432",
            ".cbbar32",
            "csbbar32",
            "cbcbar32",
            "bcbsar22",
            "cbbaar22",
            ".bbaar21",
            ".baarr21",
            ".rrr3221",
            "..33221.",
            "..2211..",
            "...11...",
        };

        static void AddGeodeStone(List<GearItem> items)
        {
            static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
            var pal = SecretFire.KindleLiquid(new Dictionary<char, Color>
            {
                ['1'] = Hex("#3a0c12"), ['2'] = Hex("#6e1420"), ['3'] = Hex("#a8202e"),
                ['4'] = Hex("#d63a44"), ['r'] = Hex("#4e0414"),
            }, 's', 'c', 'b', 'a', 'd');
            var stone = RelicGrants(Make(GeodeStoneId, "Geode Stone", GearSlot.Relic, LootTier.BlackDiamond, 0f,
                                         HipRelic("gear.trinket.geode_stone", GeodeStoneRows, pal)),
                                    KingAndQueenArtId, WeaponClass.Disc, twoHanded: false);

            // Its SPIRIT: the rind fallen away, the crystals bare (GearSpirit).
            var bare = new string[GeodeStoneRows.Length];
            for (int i = 0; i < bare.Length; i++)
            {
                var row = GeodeStoneRows[i].ToCharArray();
                for (int j = 0; j < row.Length; j++)
                    if ("abcs".IndexOf(row[j]) < 0) row[j] = '.';
                bare[i] = new string(row);
            }
            GearSpirit.Register(stone.Layers[0].Sprite, HipRelic("gear.trinket.geode_stone.spirit", bare, pal).Sprite);
            items.Add(Kindle(stone));
        }
    }
}
