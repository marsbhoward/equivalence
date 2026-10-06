using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the Armillary: four element discs, one pair
    //
    // The disc class's Forge set (the Tria Prima pattern, GearForge.Fusions): four Diamond discs,
    // one per element, burned together into the Black Diamond ARMILLARY. Each disc is the same
    // armillary FRAME - a COPPER outer hoop, a SILVER hub hoop and four silver spokes carrying on
    // into the element's SIGIL in shining GOLD at the centre (ArmSigil) - with ONE band filled,
    // each at its own radius:
    //
    //   EARTH  the outer rim, r 11.3-14   jagged rock in slabs on a copper hoop, the crags making
    //                                     its own outline
    //   WATER  r 10.1-11.3                a low water line and two curling waves travelling round
    //                                     it, rising past the band on the solo disc
    //   AIR    r 8.2-10.1                 clear white air, a breeze of wisps and leaves always
    //                                     running round it
    //   FIRE   r 6.4-8.2, round the hub   a ring of fire, tongues licking inward toward the sigil -
    //                                     AND the outer hoop engulfed in dancing flames (ArmFireHoopIn)
    //
    // The frame is kept LIGHT: hoops between every band (the first pass) read as a metal
    // dartboard with the elements as slivers in it. Between two filled bands the menu draws a
    // one-texel silver line; the arena has no room for one.
    //
    // Fused, the bands stack into one armillary - and the pair is SPLIT between the hands by
    // alchemy's own grouping, the triangles on the sigil door: RISING (Fire + Air, point up) in
    // the main hand, FALLING (Water + Earth, point down) in the off hand. The whole four-band
    // sphere is only seen where the two halves overlap (the rack, the armoury wall). The off hand
    // draws its own half through GearItem.OffhandLayer - the rig's off hand copied the main
    // until this set needed it not to.
    //
    // A part never shows anything its band on the fusion does not, and the fusion nothing its
    // parts do not: every picture here is ONE field (ArmTexel) with bands switched on or off.
    // Per cell for the arena, per quarter cell for the menu. The disc standard tall
    // (DiscHeightCells) - except a piece carrying FIRE, whose flames rise past the standard's edge
    // so they clear Earth's rim when the four combine (ArmFlameMax, DiscHeightExceptions). The
    // hand at the centre, closing over the sigil.
    //
    // ANIMATED on one clock (ArmFrames, arena only): the breeze's wisps and leaves run whole laps
    // per loop, the two waves half a lap each, the flames flicker on whole-number cycles. Earth
    // holds still - it is earth.
    public static partial class DemoGear
    {
        public const string IgnisBandId = "ignis_band";
        public const string AerBandId = "aer_band";
        public const string AquaBandId = "aqua_band";
        public const string TerraBandId = "terra_band";
        public const string ArmillaryId = "armillary_discs";
        public const string ArmillaryRelicId = "armillary_relic";
        public const string QuintessenceId = "quintessence";

        [System.Flags]
        enum ArmBands { None = 0, Fire = 1, Air = 2, Water = 4, Earth = 8,
                        Rising = Fire | Air, Falling = Water | Earth, All = Rising | Falling }

        /// <summary>The frame's outer radius - the disc standard's edge, where the outer hoop and
        /// Earth's rim sit.</summary>
        const float ArmOuter = DiscHeightCells / 2f;

        /// <summary>
        /// How far the engulfing flames may reach, in cells from the centre - PAST the standard's
        /// edge, on purpose: when the four combine in the finisher, Earth's rim is the outermost
        /// band, and fire that stopped at the same edge would disappear behind it. So a piece
        /// carrying fire is the one disc listed in DiscHeightExceptions, on a larger grid.
        /// </summary>
        const float ArmFlameMax = 16.6f;

        /// <summary>The grid a piece is drawn on: the disc standard, or - carrying the engulfing
        /// fire - big enough for its flames. Even, so the centre is a texel boundary.</summary>
        static int ArmSizeFor(ArmBands bands)
            => ArmFireRing(bands) ? 2 * Mathf.CeilToInt(ArmFlameMax) : DiscHeightCells;

        /// <summary>Does this piece carry the engulfing flames? Any piece with Fire - with Earth
        /// too (all four combined, the finisher's form) they burn OUTSIDE the stone rim instead of
        /// round a copper hoop, and reach only outward, since Water sits inside.</summary>
        static bool ArmFireRing(ArmBands bands) => (bands & ArmBands.Fire) != 0;

        /// <summary>The grip - the grid's centre, at whatever size the piece is.</summary>
        static Vector2Int ArmGripFor(ArmBands bands, int scale = 1)
            => new(ArmSizeFor(bands) / 2 * scale, ArmSizeFor(bands) / 2 * scale);

        // Band radii, in cells from the centre.
        // The hollow is sized for the SIGIL: at 4.2 its triangle was eight cells across and its
        // inside closed up under the outline - a brass blob in the arena. The bands gave up a
        // little width each to pay for it.
        const float ArmHollow = 5.8f;          // the sigil inside it
        const float ArmHub = 6.4f;             // the inner silver hoop, ArmHollow..ArmHub
        // Water gave Earth half a cell (the user's call) - the rock needed the depth to read as
        // rock rather than a saw-toothed fringe.
        const float ArmFireOut = 8.2f, ArmAirOut = 10.1f, ArmWaterOut = 11.3f;
        /// <summary>The outer hoop a disc without Earth is rimmed by, in cells.</summary>
        const float ArmOuterHoop = 0.9f;

        /// <summary>
        /// A disc carrying FIRE (and no Earth rim) has its outer hoop ENGULFED: the hoop moves in to
        /// here and dancing flames lick off both sides of it, out to the disc standard's edge - so
        /// the outline is flame, not a copper circle. Radial, not rising: baked-in flames that rise
        /// "up" would point sideways and down every time the disc swings or spins.
        /// </summary>
        const float ArmFireHoopIn = 12.0f, ArmFireHoopOut = 12.9f;

        /// <summary>How far the engulfing flames reach at this angle and moment - outward past the
        /// hoop and inward from it, in cells. Two sets of pointed tongues on different counts
        /// drift round the ring; every term is whole cycles per loop, so the loop closes.</summary>
        static void ArmFlameReach(float ang, float phase, out float outer, out float inner, float root = ArmFireHoopOut)
        {
            float tau = 2f * Mathf.PI;
            float a = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(18f * ang + tau * 2f * phase), 3f);
            float b = 0.8f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(11f * ang - tau * phase + 1.7f), 3f);
            float c = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(14f * ang - tau * 3f * phase + 0.6f), 3f);
            outer = root + 0.4f + (ArmFlameMax - root - 0.4f) * Mathf.Max(a, b);
            inner = ArmFireHoopIn - 0.35f - 1.25f * Mathf.Max(c, 0.7f * a);
        }

        const int ArmDetailScale = 4;

        const int ArmFrameCount = 24;
        const float ArmFrameSeconds = 0.1f;

        /// <summary>EARTH sits on a copper hoop like every other disc - the metalwork is shared -
        /// with the rock rising off it, in cells.</summary>
        const float ArmEarthHoopOut = ArmWaterOut + 0.5f;
        /// <summary>Crags round the rim, and how far the lowest dips below the standard's edge. The
        /// rock keeps a solid BODY under them (ArmRockBody) - with only crags off a copper ring
        /// (the first pass) it read as a saw-toothed fringe, not rock.</summary>
        const int ArmCrags = 17;
        const float ArmCragDepth = 1.5f;
        /// <summary>The rock's minimum depth off the copper hoop, in cells.</summary>
        const float ArmRockBody = 0.7f;

        /// <summary>
        /// The rock's JAGGED outer edge at this angle: a chain of crags, each a sharp peak of its
        /// own height, leaning its own way - never higher than the standard's edge. Mirrored left
        /// to right (it reads |angle from the top|), so the pair's unmirrored off-hand copy is
        /// the same picture.
        /// </summary>
        static float ArmRockEdge(float ang)
        {
            float fromTop = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, 90f)) / 180f;   // 0 top .. 1 bottom
            float t = fromTop * ArmCrags;
            int i = Mathf.FloorToInt(t);
            float f = t - i;
            float peakAt = 0.25f + 0.5f * EclipseHash(i * 13 + 7);
            float height = 0.35f + 0.65f * EclipseHash(i * 29 + 3);
            float rise = f < peakAt ? f / peakAt : (1f - f) / (1f - peakAt);
            float edge = ArmOuter - ArmCragDepth + ArmCragDepth * height * rise;
            return Mathf.Max(edge, ArmEarthHoopOut + ArmRockBody);
        }

        /// <summary>
        /// The rock broken into SLABS: a cellular pattern laid round the band (wrapping cleanly -
        /// a whole number of cells round the ring), returning the cell a point belongs to and how
        /// close it is to that cell's border, in cells.
        /// </summary>
        static int ArmRockCell(float r, float ang, out float border)
        {
            // Big slabs - about two cells along the ring and mostly the band's full depth. The rock
            // is under a cell and a half thick, so smaller cells (the first passes) put nearly
            // every texel beside a crevice and the rock read as all cracks.
            const int Around = 36;
            const float RowCells = 1.8f;
            float rMid = (ArmEarthHoopOut + ArmOuter) / 2f;
            float cellW = 2f * Mathf.PI * rMid / Around;
            float gx = (ang + Mathf.PI) / (2f * Mathf.PI) * Around;
            float gy = (r - ArmEarthHoopOut) / RowCells;
            int cx = Mathf.FloorToInt(gx), cy = Mathf.FloorToInt(gy);
            float best = float.MaxValue, second = float.MaxValue;
            int bestId = 0;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int ix = cx + ox, iy = cy + oy;
                int wx = ((ix % Around) + Around) % Around;
                int id = wx * 17 + iy * 131 + 5;
                float sx = ix + 0.15f + 0.7f * EclipseHash(id);
                float sy = iy + 0.15f + 0.7f * EclipseHash(id + 71);
                float ddx = (gx - sx) * cellW, ddy = (gy - sy) * RowCells;
                float d = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                if (d < best) { second = best; best = d; bestId = id; }
                else if (d < second) second = d;
            }
            border = (second - best) * 0.5f;
            return bestId;
        }

        static float ArmBayer(float x, float y, int scale)
        {
            int ix = Mathf.FloorToInt(x * scale), iy = Mathf.FloorToInt(y * scale);
            return ((ix & 1) * 2 + (iy & 1)) switch { 0 => 0.125f, 1 => 0.625f, 2 => 0.875f, _ => 0.375f };
        }

        /// <summary>
        /// One armillary at (dx, dy) in cells off its CENTRE, rows DOWN, <paramref name="scale"/> texels per cell,
        /// <paramref name="phase"/> 0..1 through the loop, with only <paramref name="bands"/> filled.
        /// Front to back: the sigil, the hoops, the bands, the spokes (seen only where a band
        /// is empty).
        /// </summary>
        static char ArmTexel(float dx, float dy, int scale, float phase, ArmBands bands)
        {
            bool menu = scale > 1;
            float px = 1f / scale;
            float x = dx, y = dy;                                             // for the water's dither
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(-dy, dx);                                 // counter-clockwise, 0 = right
            float light = Mathf.Cos(ang - 0.75f * Mathf.PI);                  // 1 facing the upper left
            bool earth = (bands & ArmBands.Earth) != 0;
            bool fireRing = ArmFireRing(bands);
            float flameOut = 0f, flameIn = 0f;
            if (fireRing) ArmFlameReach(ang, phase, out flameOut, out flameIn, earth ? ArmOuter : ArmFireHoopOut);

            // ---- all four combined: the flames burn OUTSIDE the stone rim, rooted on its edge
            if (fireRing && earth && r > ArmOuter - 0.9f)
            {
                float stoneEdge = ArmRockEdge(ang);
                if (r > flameOut) return '.';
                if (r > stoneEdge)
                {
                    float d = (r - ArmOuter) / Mathf.Max(0.05f, flameOut - ArmOuter);
                    return d < 0.3f ? 'F' : d < 0.68f ? 'G' : 'I';
                }
            }

            // ---- the outline: the engulfing flames, Earth's notched millstone rim, or the plain hoop
            float rim = fireRing && !earth ? flameOut : ArmOuter;
            if (earth)
            {
                rim = ArmRockEdge(ang);                                          // the jagged crags
            }
            if (r > rim) return '.';

            // ---- the SIGIL in the hollow: gold, grown out of the silver spokes - no grip bar any more
            if (r < ArmHollow) return ArmSigil(dx, dy, bands, light, menu, px, phase);

            // ---- the engulfed hoop: copper glowing through, flames off both sides of it
            if (fireRing && !earth && r >= flameIn)
            {
                if (r >= ArmFireHoopIn && r <= ArmFireHoopOut)
                    return menu && light < -0.3f ? 'l' : 'h';                 // copper, lit by its own fire
                float d = r > ArmFireHoopOut
                    ? (r - ArmFireHoopOut) / Mathf.Max(0.05f, flameOut - ArmFireHoopOut)
                    : (ArmFireHoopIn - r) / Mathf.Max(0.05f, ArmFireHoopIn - flameIn);
                return d < 0.3f ? 'F' : d < 0.68f ? 'G' : 'I';
            }

            // ---- metal: the silver hub hoop, and the copper outer hoop when there is no Earth (nor fire)
            if (r < ArmHub) return ArmSilver(light, menu, r);
            if (!earth && !fireRing && r > ArmOuter - ArmOuterHoop) return ArmCopper(r, light, menu);

            // ...and in the menu a one-texel silver line where two FILLED bands meet
            if (menu)
            {
                bool Has(ArmBands b) => (bands & b) != 0;
                if ((Mathf.Abs(r - ArmFireOut) < 0.5f * px && Has(ArmBands.Fire) && Has(ArmBands.Air))
                    || (Mathf.Abs(r - ArmAirOut) < 0.5f * px && Has(ArmBands.Air) && Has(ArmBands.Water))
                    || (Mathf.Abs(r - ArmWaterOut) < 0.5f * px && Has(ArmBands.Water) && earth))
                    return light > 0f ? 'L' : 'B';
            }

            // ---- the band this radius belongs to
            if (r < ArmFireOut)
                return (bands & ArmBands.Fire) != 0 ? ArmFire(r, ang, phase, menu, px) : ArmSpoke(dx, dy, r, light, menu, px);
            if (r < ArmAirOut)
                return (bands & ArmBands.Air) != 0 ? ArmAir(dx, dy, r, ang, phase, menu, px) : ArmSpoke(dx, dy, r, light, menu, px);
            if (r < ArmWaterOut)
            {
                if ((bands & ArmBands.Water) == 0) return ArmSpoke(dx, dy, r, light, menu, px);
                char c = ArmWater(x, y, r, ang, phase, light, menu, px, scale, ArmWaveTopAlone);
                return c != '.' ? c : ArmSpoke(dx, dy, r, light, menu, px);
            }
            // the waves rise PAST the water band - into Earth's empty zone on the solo disc, and
            // crashing over the copper and the rock where Earth is there
            if ((bands & ArmBands.Water) != 0 && r < ArmWaveTopAlone)
            {
                char c = ArmWater(x, y, r, ang, phase, light, menu, px, scale, ArmWaveTopAlone);
                if (c != '.') return c;
            }
            if (earth) return r < ArmEarthHoopOut ? ArmCopper(r, light, menu) : ArmEarth(r, ang, menu, px);
            // an engulfed disc's spokes stop at its (smaller) hoop
            return fireRing && !earth && r > ArmFireHoopIn ? '.' : ArmSpoke(dx, dy, r, light, menu, px);
        }

        /// <summary>
        /// The element's ALCHEMICAL SIGIL at the centre - the sigil door's own triangles - drawn as
        /// a continuation of the frame, in SHINING GOLD: the triangle's corners touch the hub hoop, and every
        /// spoke carries on inward until it meets it. Fire and Air point UP, Water and Earth DOWN;
        /// Air and Earth are crossed by a bar, laid level with the east-west spokes so it reads as
        /// one gold line straight across the disc (the canonical bar sits a little higher - the
        /// continuation matters more). A fused half shows both its elements' marks at once: Rising
        /// is up-with-bar, Falling down-with-bar, and where the two overlap on the rack they make
        /// the six-pointed star, the elements united. The hand closes over it at the centre.
        /// </summary>
        static char ArmSigil(float dx, float dy, ArmBands bands, float light, bool menu, float px, float phase)
        {
            // Rising elements draw the UP triangle, falling ones the DOWN; a piece holding both
            // (all four combined) draws both - the six-pointed star, the elements united.
            bool up = (bands & (ArmBands.Fire | ArmBands.Air)) != 0;
            bool down = (bands & (ArmBands.Water | ArmBands.Earth)) != 0;
            bool bar = (bands & (ArmBands.Air | ArmBands.Earth)) != 0;
            var p = new Vector2(dx, -dy);                                     // y up
            float circum = ArmHollow - 0.05f, inr = circum / 2f;
            float half = menu ? 0.42f : 0.5f;                                 // line half-width
            float spokeHalf = menu ? 0.45f : 0.55f;                           // matches ArmSpoke

            // distance outward through a triangle's nearest edge: the largest projection on its
            // three edge normals (pointing out through each side)
            float Reach(bool pointingUp)
            {
                float best = float.MinValue;
                for (int i = 0; i < 3; i++)
                {
                    float a = ((pointingUp ? 270f : 90f) + i * 120f) * Mathf.Deg2Rad;
                    best = Mathf.Max(best, Vector2.Dot(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a))));
                }
                return best;
            }
            float mUp = up ? Reach(true) : float.MaxValue, mDown = down ? Reach(false) : float.MaxValue;
            bool edge = (up && Mathf.Abs(mUp - inr) < half) || (down && Mathf.Abs(mDown - inr) < half);
            bool outside = mUp > inr && mDown > inr;                          // outside every triangle drawn
            bool spoke = outside && (Mathf.Abs(dx) < spokeHalf || Mathf.Abs(dy) < spokeHalf);
            bool across = bar && !outside && Mathf.Abs(dy) < half;
            if (!(edge || spoke || across)) return '.';

            // the spokes' own continuation is SILVER, like the spokes; the sigil is GOLD
            if (!(edge || across)) return ArmSilver(light, menu, 0f);

            // SHINING gold: lit toward the upper left, and a glint sweeping across the mark once a
            // loop (entering and leaving outside the hollow, so the loop's seam never shows)
            float sweep = -1.6f + 3.2f * phase;
            float g = (dx - dy) / (ArmHollow * 1.414f);
            if (Mathf.Abs(g - sweep) < (menu ? 0.07f : 0.12f)) return 'r';
            if (menu && light > 0.35f) return 'q';
            return light > 0.1f ? 'q' : light > -0.4f ? 'p' : 'o';
        }

        /// <summary>SILVER - the spokes, the hub hoop, the band separators - lit from the upper left.</summary>
        static char ArmSilver(float light, bool menu, float r)
        {
            if (menu && light > 0.45f && Mathf.Repeat(r * 4f, 1f) < 0.25f) return 'H';
            return light > 0.35f ? 'L' : light > -0.3f ? 'B' : 'D';
        }

        /// <summary>COPPER - the outer hoop - lit from the upper left; a bright line along it in the menu.</summary>
        static char ArmCopper(float r, float light, bool menu)
        {
            if (menu)
            {
                float edge = Mathf.Repeat(r * 4f, 1f);
                if (light > 0.3f && edge < 0.25f) return 'h';
            }
            return light > 0.4f ? 'l' : light > -0.3f ? 'b' : 'd';
        }

        /// <summary>The four spokes (N, E, S, W) crossing an EMPTY band - what holds a part's
        /// hoops together where its neighbours' bands are missing.</summary>
        static char ArmSpoke(float dx, float dy, float r, float light, bool menu, float px)
        {
            // The spokes straddle the centre line, which is a CELL BOUNDARY: at exactly 0.5 no
            // arena cell centre falls inside and they vanished. Just over, they are two cells wide.
            float half = menu ? 0.45f : 0.55f;
            if (Mathf.Abs(dx) < half || Mathf.Abs(dy) < half) return light > 0.2f ? 'B' : 'D';   // silver
            return '.';
        }

        /// <summary>FIRE: flames rooted at the band's outer edge licking inward toward the sigil -
        /// white-yellow at the root, red at the tips, an ember line along the root.</summary>
        static char ArmFire(float r, float ang, float phase, bool menu, float px)
        {
            float tau = 2f * Mathf.PI;
            float t = (ArmFireOut - r) / (ArmFireOut - ArmHub);             // 0 root, 1 at the hub
            // a SOLID ring of fire at the root, tongues rising off it - tongues alone read as a
            // dotted line at arena size
            const float Root = 0.4f;
            float reach = 0.78f + 0.14f * Mathf.Sin(7f * ang + tau * phase)
                                + 0.10f * Mathf.Sin(11f * ang - tau * 2f * phase + 1.3f);
            if (t > Root && t > reach) return '.';
            if (menu && t < 1.2f * px / (ArmFireOut - ArmHub)) return 'J';
            float d = t < Root ? t / Root * 0.4f : 0.4f + (t - Root) / Mathf.Max(0.05f, reach - Root) * 0.6f;
            return d < 0.3f ? 'F' : d < 0.68f ? 'G' : 'I';
        }

        /// <summary>
        /// AIR: a ring of moving air - CLEAR and faintly white, the silver spokes showing through it -
        /// with a breeze always running round it: white wisps in two layers at different speeds,
        /// each tapering at both ends and weaving a little across the band, and a few leaves (and,
        /// in the menu, specks of debris) tumbling along with it. Every wisp and leaf travels a
        /// WHOLE number of laps per loop, so the loop closes. (Turbine vanes were the first pass -
        /// the user wanted it white and clear, moving like a breeze.)
        /// </summary>
        static char ArmAir(float dx, float dy, float r, float ang, float phase, bool menu, float px)
        {
            float tau = 2f * Mathf.PI;
            float mid = (ArmFireOut + ArmAirOut) / 2f, halfW = (ArmAirOut - ArmFireOut) / 2f;
            float v = (r - mid) / halfW;                                       // -1 inner .. 1 outer

            // ---- leaves and debris, carried along
            char leaf = ArmAirLeaf(r, ang, phase, mid, halfW, menu, px);
            if (leaf != '.') return leaf;

            // ---- the breeze: two layers of wisps, both flowing counter-clockwise
            float Wisp(int count, float laps, float weave, float weaveFreq, float width, float length, float offset)
            {
                float a = ang - tau * laps * phase;
                float along = Mathf.Repeat(count * a / tau + offset, 1f);
                if (along > length) return 0f;
                float taper = Mathf.Sin(Mathf.PI * along / length);
                float centre = weave * Mathf.Sin(weaveFreq * a + offset * 5f);
                float off = (v - centre) / width;
                return taper * Mathf.Exp(-off * off);
            }
            // Three layers, wide and long, so at any moment most of the band is moving - thin
            // faint wisps (the first pass) read as a still grey tube.
            float breeze = Mathf.Max(Mathf.Max(
                                     Wisp(4, 1f, 0.40f, 3f, 0.42f, 0.70f, 0f),
                                     0.9f * Wisp(3, 2f, 0.34f, 2f, 0.34f, 0.60f, 0.37f)),
                                     0.8f * Wisp(5, 3f, 0.50f, 4f, 0.26f, 0.45f, 0.71f));
            if (breeze > (menu ? 0.58f : 0.5f)) return 'A';                   // a wisp's bright core
            if (breeze > (menu ? 0.22f : 0.25f)) return 'E';

            // ---- clear air: faint rims at its edges in the menu, spokes seen through it
            if (menu && (Mathf.Abs(v) > 1f - 1.1f * px / halfW)) return 'A';   // bright glassy rims
            char spoke = ArmSpoke(dx, dy, r, 0f, menu, px);
            return spoke != '.' ? spoke : 'C';
        }

        /// <summary>The leaves and debris the breeze carries: each circles the band at its own
        /// whole number of laps per loop, bobbing across it and tumbling as it goes.</summary>
        static char ArmAirLeaf(float r, float ang, float phase, float mid, float halfW, bool menu, float px)
        {
            float tau = 2f * Mathf.PI;
            // start angle, laps per loop, bob phase, kind (0 green leaf, 1 autumn leaf, 2 debris)
            var carried = new (float start, float laps, float bob, int kind)[]
            {
                (0.0f, 1f, 0.0f, 0), (1.9f, 1f, 1.3f, 1), (3.4f, 2f, 2.1f, 0),
                (4.8f, 1f, 0.6f, 1), (2.6f, 2f, 3.0f, 2), (5.6f, 1f, 4.2f, 2), (0.9f, 3f, 5.1f, 2),
            };
            foreach (var c in carried)
            {
                if (c.kind == 2 && !menu) continue;                             // debris is menu detail
                float at = c.start + tau * c.laps * phase;
                float lr = mid + 0.5f * halfW * Mathf.Sin(2f * at + c.bob);
                float da = Mathf.DeltaAngle(ang * Mathf.Rad2Deg, at * Mathf.Rad2Deg) * Mathf.Deg2Rad * r;
                float dr = r - lr;
                if (c.kind == 2)
                {
                    if (da * da + dr * dr < 0.09f) return 'T';
                    continue;
                }
                if (!menu)
                {
                    if (da * da + dr * dr < 0.62f * 0.62f) return c.kind == 0 ? 'P' : 'R';
                    continue;
                }
                // a small tumbling leaf: an ellipse turning as it goes, a darker vein down it
                float turn = at * 3f + c.bob;
                float u = da * Mathf.Cos(turn) + dr * Mathf.Sin(turn);
                float w = -da * Mathf.Sin(turn) + dr * Mathf.Cos(turn);
                float e = (u / 1.0f) * (u / 1.0f) + (w / 0.48f) * (w / 0.48f);
                if (e > 1f) continue;
                if (Mathf.Abs(w) < 0.6f * px && Mathf.Abs(u) < 0.8f) return c.kind == 0 ? 'Q' : 'T';
                return c.kind == 0 ? 'P' : 'R';
            }
            return '.';
        }

        /// <summary>How high a wave rises, as a radius in cells - past the water band, into Earth's
        /// zone: empty on the solo disc, and on the Falling half and the whole the wave crashes OVER
        /// the copper and the rock (capped at the band's top, the fused waves vanished).</summary>
        const float ArmWaveTopAlone = 12.9f;

        /// <summary>
        /// WATER: a low, calm water line along the inside of the band, and TWO WAVES on opposite
        /// sides travelling counter-clockwise round it - a long gentle back, a steep face, and a
        /// crest that rises and CURLS forward into a white foam lip, the barrel open under it. Half
        /// a lap per loop (the two are the same, so the loop closes). They rise well past the band -
        /// over the copper and the rock where Earth is there.
        /// Returns '.' above the water so the spokes show through the air.
        /// </summary>
        static char ArmWater(float x, float y, float r, float ang, float phase, float light, bool menu,
                             float px, int scale, float top)
        {
            float h = r - ArmAirOut;                                            // cells up off the band's floor
            float calm = 0.85f;                                                 // the water line between waves - a cell, or the arena dots it
            float peak = top - ArmAirOut - 0.05f;

            // which wave, and where along it: u > 0 is AHEAD of the crest (counter-clockwise)
            float rMid = (ArmAirOut + ArmWaterOut) / 2f;
            float best = float.MaxValue;
            for (int k = 0; k < 2; k++)
            {
                float crestAt = k * Mathf.PI + 2f * Mathf.PI * 0.5f * phase;
                float du = Mathf.DeltaAngle(crestAt * Mathf.Rad2Deg, ang * Mathf.Rad2Deg) * Mathf.Deg2Rad * rMid;
                if (Mathf.Abs(du) < Mathf.Abs(best)) best = du;
            }
            float u = best;
            // A PLUMP swell: a rounded back (cosine) and a steep, slightly hollow face - a back that
            // fell away as a power curve (the first pass) left only a thin sliver at full height,
            // and the waves read as nubs.
            const float Back = 6.5f, Front = 2.6f;
            float lift = u <= 0f ? (u < -Back ? 0f : 0.5f + 0.5f * Mathf.Cos(Mathf.PI * -u / Back))
                                 : Mathf.Pow(Mathf.Clamp01(1f - u / Front), 0.5f);
            float surface = calm + (peak - calm) * lift;

            // the CURL: a hook of foam thrown forward off the crest, the barrel open under it
            // the curl's circle sits AHEAD of the crest, so the lip hooks forward over the barrel
            float curlR = Mathf.Clamp(0.42f * (peak - calm), 0.3f, 1.0f);
            var centre = new Vector2(0.95f * curlR, peak - curlR);
            var q = new Vector2(u, h) - centre;
            float qd = q.magnitude;
            bool curlZone = q.y > -0.55f * curlR && u > 0f;
            if (curlZone && qd < curlR && qd > curlR - (menu ? 2.2f * px : 0.6f)) return 'w';
            if (curlZone && qd <= curlR - (menu ? 2.2f * px : 0.6f) && u > 0f) return '.';   // the barrel

            if (h > surface) return '.';
            float below = surface - h;
            if (below < (menu ? 1.2f * px : 0.45f) && lift > 0.25f) return 'w';   // foam on the face
            if (below < (menu ? 2.4f * px : 0.5f)) return 'x';                    // the lit skin of the water
            if (h < 0.3f) return 'z';                                            // the deep floor
            if (menu) return ArmBayer(x, y, scale) < 0.25f ? 'x' : 'y';
            return 'y';
        }

        /// <summary>
        /// EARTH: JAGGED ROCK on a copper hoop - broken into slabs, each turned its own way to the
        /// light, dark crevices between them, the crags' lit faces catching the light at the rim.
        /// (A neat faceted millstone with regular notches was the first pass.) The arena has about
        /// a cell and a half of rock: it keeps the slab shading and the outline, not the crevices.
        /// </summary>
        static char ArmEarth(float r, float ang, bool menu, float px)
        {
            int cell = ArmRockCell(r, ang, out float border);
            if (menu && border < 0.5f * px) return '1';                          // a crevice, one texel

            // the slab's own facet: a random tilt, lit from the upper left
            float tilt = (EclipseHash(cell + 311) - 0.5f) * 2.4f;
            float facing = Mathf.Cos(ang + tilt - 0.75f * Mathf.PI);
            float val = 0.55f * facing + 0.25f * (EclipseHash(cell + 97) - 0.5f);

            // the crag tips: the last stretch before the edge, lit where it faces the light
            float edge = ArmRockEdge(ang);
            if (edge - r < (menu ? 1.3f * px : 0.6f) && Mathf.Cos(ang - 0.75f * Mathf.PI) > 0f)
                return menu ? '6' : '5';
            return val > 0.4f ? '5' : val > 0.02f ? '4' : val > -0.35f ? '3' : '2';
        }

        static string[] BuildArmillary(int scale, float phase, ArmBands bands)
        {
            int size = ArmSizeFor(bands), n = size * scale;
            float c = size / 2f;
            var rows = new string[n];
            var sb = new System.Text.StringBuilder(n);
            for (int row = 0; row < n; row++)
            {
                sb.Clear();
                for (int col = 0; col < n; col++)
                    sb.Append(ArmTexel((col + 0.5f) / scale - c, (row + 0.5f) / scale - c, scale, phase, bands));
                rows[row] = sb.ToString();
            }
            return RoseDropIslands(rows);
        }

        /// <summary>
        /// Copper (lowercase), silver (uppercase), stone on digits (the third
        /// ramp), and past that on spare letters, placed by hand: WATER w foam, x light, y body,
        /// z deep (a little translucent - it is in glass); AIR A wisp core, C clear air, E wisp /
        /// rim, and what the breeze carries P green leaf, Q its vein, R autumn leaf, T debris; FIRE F root
        /// (white-yellow), G orange, I red tips, J the ember line.
        /// </summary>
        static Dictionary<char, Color> ArmPal()
        {
            // COPPER outer ring (lowercase), SILVER spokes and hub (uppercase), stone (digits)
            var pal = Palette.Of(new Palette.Ramp(new Color(0.76f, 0.42f, 0.24f), lift: 0.34f),
                                 new Palette.Ramp(new Color(0.70f, 0.72f, 0.77f), lift: 0.40f),
                                 new Palette.Ramp(new Color(0.50f, 0.46f, 0.41f), lift: 0.32f));
            // SHINING GOLD for the sigils: o shadow, p body, q lit, r the glint
            pal['o'] = new Color(0.60f, 0.42f, 0.10f);
            pal['p'] = new Color(0.90f, 0.70f, 0.20f);
            pal['q'] = new Color(1.00f, 0.87f, 0.42f);
            pal['r'] = new Color(1.00f, 0.99f, 0.86f);
            pal['w'] = new Color(0.88f, 0.97f, 1.00f, 0.96f);
            pal['x'] = new Color(0.36f, 0.68f, 0.96f, 0.92f);
            pal['y'] = new Color(0.14f, 0.42f, 0.84f, 0.90f);
            pal['z'] = new Color(0.08f, 0.22f, 0.54f, 0.92f);
            // AIR is white and CLEAR: the band itself barely there, the wisps brighter
            pal['A'] = new Color(1.00f, 1.00f, 1.00f, 0.92f);
            pal['C'] = new Color(0.94f, 0.97f, 1.00f, 0.26f);
            pal['E'] = new Color(0.96f, 0.99f, 1.00f, 0.58f);
            // what the breeze carries: a green leaf and its vein, an autumn leaf, debris
            pal['P'] = new Color(0.50f, 0.72f, 0.28f);
            pal['Q'] = new Color(0.28f, 0.46f, 0.16f);
            pal['R'] = new Color(0.88f, 0.54f, 0.18f);
            pal['T'] = new Color(0.52f, 0.40f, 0.27f);
            pal['F'] = new Color(1.00f, 0.94f, 0.58f);
            pal['G'] = new Color(1.00f, 0.60f, 0.14f);
            pal['I'] = new Color(0.88f, 0.24f, 0.08f);
            pal['J'] = new Color(0.46f, 0.10f, 0.05f);
            return pal;
        }

        /// <summary>The Seal's worn mark: a copper ring holding the six-pointed star in gold.</summary>
        static string[] ArmSealRows()
        {
            const int N = 12;
            const float C = N / 2f;
            var rows = new string[N];
            for (int y = 0; y < N; y++)
            {
                var sb = new System.Text.StringBuilder(N);
                for (int x = 0; x < N; x++)
                {
                    float dx = x + 0.5f - C, dy = y + 0.5f - C;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    char c = '.';
                    if (r <= 6f && r > 4.9f) c = dx + dy < 0f ? 'l' : 'd';
                    else if (r <= 4.9f)
                    {
                        // the star: inside either triangle, circumradius 4
                        var p = new Vector2(dx, -dy);
                        bool In(bool up)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                float a = ((up ? 270f : 90f) + i * 120f) * Mathf.Deg2Rad;
                                if (Vector2.Dot(p, new Vector2(Mathf.Cos(a), Mathf.Sin(a))) > 2f) return false;
                            }
                            return true;
                        }
                        c = In(true) || In(false) ? (dx + dy < 0f ? 'q' : 'p') : 's';
                    }
                    sb.Append(c);
                }
                rows[y] = sb.ToString();
            }
            return rows;
        }

        static Sprite[] ArmFrames(string key, ArmBands bands)
        {
            var pal = ArmPal();
            var frames = new Sprite[ArmFrameCount];
            for (int i = 0; i < ArmFrameCount; i++)
                frames[i] = StageSprite($"{key}.frame{i}", BuildArmillary(1, i / (float)ArmFrameCount, bands),
                                        pal, ArmGripFor(bands));
            return frames;
        }

        static LayerSprite ArmArena(string key, ArmBands bands)
            => Pixels(RigLayer.Weapon, key, BuildArmillary(1, 0f, bands), ArmPal(),
                      DiscHandX, DiscHandY, pivotTexel: ArmGripFor(bands), ppu: FinePpu, upscale2x: true);

        static LayerSprite ArmMenu(string key, ArmBands bands)
            => Pixels(RigLayer.Weapon, key + ".menu", BuildArmillary(ArmDetailScale, 0f, bands), ArmPal(),
                      DiscHandX, DiscHandY, pivotTexel: ArmGripFor(bands, ArmDetailScale), ppu: MenuPpu);

        static GearItem ArmPart(string id, string name, string key, ArmBands bands)
        {
            var item = Disc(WithMenu(Make(id, name, GearSlot.Weapon, LootTier.Diamond, 0f,
                                          ArmArena(key, bands)), ArmMenu(key, bands)));
            if (bands != ArmBands.Earth)                      // stone does not move
            {
                item.IdleFrames = ArmFrames(key, bands);
                item.IdleFrameSeconds = ArmFrameSeconds;
            }
            return item;
        }

        static void AddArmillary(List<GearItem> items)
        {
            // ---- the four parts: Diamond, power 0, cosmetic - each an ordinary (identical) pair
            items.Add(ArmPart(IgnisBandId, "Ignis Band", "gear.weapon.disc.arm.fire", ArmBands.Fire));
            items.Add(ArmPart(AerBandId, "Aer Band", "gear.weapon.disc.arm.air", ArmBands.Air));
            items.Add(ArmPart(AquaBandId, "Aqua Band", "gear.weapon.disc.arm.water", ArmBands.Water));
            items.Add(ArmPart(TerraBandId, "Terra Band", "gear.weapon.disc.arm.earth", ArmBands.Earth));

            // ---- the fusion: Black Diamond, power 0, Forge-only. RISING in the main hand,
            // FALLING in the off hand, each on the same clock so the two halves turn together.
            var armillary = Disc(WithMenu(Make(ArmillaryId, "Armillary", GearSlot.Weapon, LootTier.BlackDiamond, 0f,
                                               ArmArena("gear.weapon.disc.arm.rising", ArmBands.Rising)),
                                          ArmMenu("gear.weapon.disc.arm.rising", ArmBands.Rising)));
            armillary.OffhandLayer = ArmArena("gear.weapon.disc.arm.falling", ArmBands.Falling);
            armillary.OffhandMenuLayer = ArmMenu("gear.weapon.disc.arm.falling", ArmBands.Falling);
            armillary.IdleFrames = ArmFrames("gear.weapon.disc.arm.rising", ArmBands.Rising);
            armillary.OffhandIdleFrames = ArmFrames("gear.weapon.disc.arm.falling", ArmBands.Falling);
            armillary.IdleFrameSeconds = ArmFrameSeconds;
            // The whole armillary, all four bands - what the halves become overhead in the
            // finisher (Quintessence), on the same clock as the halves.
            armillary.CombinedFrames = ArmFrames("gear.weapon.disc.arm.whole", ArmBands.All);
            armillary.SignatureFinisher = QuintessenceId;
            armillary.ForgeOnly = true;
            items.Add(armillary);

            // The RELIC: the finisher, no power, a worn mark - the six-pointed star in gold in a
            // copper ring, the four elements united. Matched to a one-handed DISC, like the weapon.
            var relic = RelicGrants(Make(ArmillaryRelicId, "Armillary Seal", GearSlot.Relic, LootTier.BlackDiamond, 0f,
                                         HipRelic("gear.trinket.armillary", ArmSealRows(), ArmPal())),
                                    QuintessenceId, WeaponClass.Disc, twoHanded: false);
            relic.ForgeOnly = true;
            items.Add(relic);
        }
    }
}
