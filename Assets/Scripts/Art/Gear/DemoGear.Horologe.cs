using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Horologe: a clock's gear train as a disc
    //
    // The user's brief: a Diamond dual disc of clock gears, brass, ANIMATED so the gears actually
    // move in sync - and then, of the first build, that the inner cogs be SMALL, MEDIUM and LARGE.
    //
    //     the RING         teeth inside (driving the train) and outside (the cog silhouette)
    //     a LARGE wheel    meshing the ring's inside, four crossings, like a clock's centre wheel
    //     a MEDIUM wheel   driven by the large one
    //     a SMALL pinion   driven by the medium one
    //     the BRIDGE       blued steel from the hub to every axle, a ruby jewel at each - FIXED,
    //                      and what the hand holds; everything else turns round it
    //
    // A CHAIN, not the planetary set the first build was: any gear meshing both the ring and a
    // centre gear is forced to (ring - sun) / 2, so every planet was the same size by necessity.
    // And a chain with no LOOPS - a gear meshing the ring AND another gear that meshes the ring
    // locks solid (an inside mesh keeps the sense of turn, an outside one reverses it) - so only
    // the large wheel touches the ring, and the medium and small ones keep clear of it and of each
    // other's drivers (checked by the overlap test in CLAUDE.md, which also proves the mesh).
    //
    // THE MESH IS REAL. One module for the whole train (teeth the same size where they meet);
    // each wheel sits exactly its pitch radius plus its driver's from the driver
    // (HorologeCentres, solved, not typed); the starting angles put a tooth in a gap at every
    // contact (HorologeAngles); and the train turns by the ratios - the ring d, each wheel
    // d Zr / Z, alternating sense down the chain - which keeps every contact meshed.
    //
    // It TICKS, as a clock does: a hold, then a quick step (HorologeTickCurve), one ring tooth a
    // tick, so every wheel advances exactly one of its OWN teeth a tick. The loop closes when the
    // outer cog's teeth and the large wheel's crossings have both come round
    // (HorologeTicksPerLoop, derived). The OFF HAND turns the other way on the same tick
    // (GearItem.OffhandIdleFrames, the Armillary's mechanism), as if the pair were meshed.
    //
    // STUB teeth and wheels a shade BRIGHTER than the ring - see HorologeAdd and Gear() below.
    //
    // Clock BRASS - yellow, polished - not the Armillary's copper; blued steel for the bridge, as
    // a movement's screws and bridges are; ruby jewel bearings. The disc standard tall, outlined,
    // the grip at the centre. ONE field (HorologeTexel), per cell for the arena and per quarter
    // cell for the menu, so the menu's teeth are the arena's teeth.
    public static partial class DemoGear
    {
        const int HorologeW = DiscHeightCells, HorologeH = DiscHeightCells;
        const float HorologeCx = HorologeW / 2f, HorologeCy = HorologeH / 2f;

        /// <summary>The module, in cells: pitch radius is m Z / 2 for every gear in the train.
        /// Between the first build's two: at 0.62 ten-tooth wheels had single-cell teeth (pale
        /// snowflakes in the arena), and 0.92 left no room for three sizes inside the ring.</summary>
        const float HorologeM = 0.75f;
        /// <summary>
        /// STUB teeth, in modules: how far a tooth stands past the pitch circle and how deep its
        /// gap goes below. Standard (1 and 1.25) on a six-leaf pinion made teeth longer than the
        /// gear's own body - a six-pointed star, not a gear. A tip still clears the gap it meets
        /// by the difference.
        /// </summary>
        const float HorologeAdd = 0.6f, HorologeDed = 0.8f;
        /// <summary>The ring's inner teeth.</summary>
        const int HorologeZr = 24;
        const float HorologeRr = HorologeM * HorologeZr / 2f;
        static float HorologePitch(int z) => HorologeM * z / 2f;

        /// <summary>
        /// The train, driver first: LARGE (meshes the ring), MEDIUM (meshes the large), SMALL
        /// (meshes the medium). Teeth, crossings (0 for a plain wheel), and how far from the
        /// disc's centre each wheel after the first sits - it lands where that circle crosses its
        /// mesh distance from its driver, clockwise round the fist.
        ///
        /// 14 is the largest wheel the ring allows: past it, the medium wheel cannot reach it and
        /// stay clear of the ring. The reaches are the most each wheel's ring clearance allows, so
        /// the chain spreads as far round the fist as it can - at 12 teeth and reaches of 4.6 and
        /// 4.5 it spanned half the ring and left the other half empty.
        /// </summary>
        static readonly (int Z, int Spokes, float Reach)[] HorologeTrain =
        {
            (14, 4, 0f),
            (8, 0, 4.75f),
            (6, 0, 5.4f),
        };
        /// <summary>The large wheel's direction from the centre, rows DOWN - to the left, so the
        /// chain runs over the top to the right and the open quarter is at the bottom.</summary>
        const float HorologeLargeAt = 163f * Mathf.Deg2Rad;

        /// <summary>The ring's OUTSIDE: a cog of big teeth, the silhouette.</summary>
        const int HorologeZe = 12;
        const float HorologeTip = 13.8f, HorologeRoot = 11.6f;

        const int HorologeDetailScale = 4;
        static readonly Vector2Int HorologeGrip = new(HorologeW / 2, HorologeH / 2);
        static readonly Vector2Int HorologeDetailGrip = new(HorologeGrip.x * HorologeDetailScale,
                                                            HorologeGrip.y * HorologeDetailScale);

        /// <summary>The tick: the hold, then a quick step - how far through one tooth each of the
        /// tick's LAST frames lands (the step's end is the next tick's hold). The escapement.
        /// Hold first, so a loop's frame 0 is the still.</summary>
        static readonly float[] HorologeTickCurve = { 0.45f, 0.9f };
        const int HorologeTickFrames = 10;
        const float HorologeFrameSeconds = 0.05f;

        /// <summary>
        /// Ticks until everything painted on a turning part has come round: the outer cog (the
        /// ring turns Zr / gcd(Zr, Ze) inner teeth per outer tooth) and each wheel's crossings
        /// (Z / gcd(Z, crossings) teeth). A plain wheel repeats every tooth. Derived, so a change
        /// of tooth count cannot leave a loop that jumps.
        /// </summary>
        static int HorologeTicksPerLoop
        {
            get
            {
                static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
                int loop = HorologeZr / Gcd(HorologeZr, HorologeZe);
                foreach (var w in HorologeTrain)
                {
                    int period = w.Spokes > 0 ? w.Z / Gcd(w.Z, w.Spokes) : 1;
                    loop = loop / Gcd(loop, period) * period;
                }
                return loop;
            }
        }

        static Vector2[] _horologeCentres;
        /// <summary>Each wheel's centre, in cells off the disc's centre, rows DOWN - solved, see
        /// HorologeTrain.</summary>
        static Vector2[] HorologeCentres => _horologeCentres ??= SolveHorologeCentres();

        static Vector2[] SolveHorologeCentres()
        {
            var c = new Vector2[HorologeTrain.Length];
            // the large wheel meshes the ring's inside: its pitch circle touches the ring's
            c[0] = new Vector2(Mathf.Cos(HorologeLargeAt), Mathf.Sin(HorologeLargeAt))
                 * (HorologeRr - HorologePitch(HorologeTrain[0].Z));
            for (int i = 1; i < c.Length; i++)
            {
                // where the circle of radius Reach round the centre crosses the circle of the mesh
                // distance round the driver - the crossing clockwise on screen
                var p = c[i - 1];
                float mesh = HorologePitch(HorologeTrain[i - 1].Z) + HorologePitch(HorologeTrain[i].Z);
                float reach = HorologeTrain[i].Reach, far = p.magnitude;
                float a = (reach * reach - mesh * mesh + far * far) / (2f * far);
                float h = Mathf.Sqrt(Mathf.Max(0f, reach * reach - a * a));
                var u = p / far;
                c[i] = u * a + new Vector2(-u.y, u.x) * h;
            }
            return c;
        }

        /// <summary>
        /// The ring's angle and every wheel's when the ring has turned <paramref name="d"/>
        /// radians. Solved so each contact is a tooth in a gap: the large wheel against the ring
        /// (an inside mesh - the ring's tooth in the wheel's gap where they touch), then each
        /// wheel against its driver (outside - facing phases sum to a half). Then the ratios.
        /// </summary>
        static (float Ring, float[] Wheels) HorologeAngles(float d)
        {
            const float tau = Mathf.PI * 2f;
            // phase: how far through a tooth pitch direction `dir` falls on a gear at angle `theta`
            float Phase(float dir, float theta, int z) => Mathf.Repeat((dir - theta) * z / tau, 1f);

            var c = HorologeCentres;
            var wheels = new float[c.Length];
            float ring0 = 0f;

            float at = Mathf.Atan2(c[0].y, c[0].x);
            int z0 = HorologeTrain[0].Z;
            wheels[0] = at - Mathf.Repeat(Phase(at, ring0, HorologeZr) - 0.5f, 1f) * tau / z0;
            for (int i = 1; i < c.Length; i++)
            {
                var toward = c[i] - c[i - 1];
                float b = Mathf.Atan2(toward.y, toward.x);
                float facing = Mathf.Repeat(0.5f - Phase(b, wheels[i - 1], HorologeTrain[i - 1].Z), 1f);
                wheels[i] = b + Mathf.PI - facing * tau / HorologeTrain[i].Z;
            }

            // turning: the large wheel with the ring, then alternating down the chain
            float sense = 1f;
            for (int i = 0; i < c.Length; i++)
            {
                wheels[i] += sense * d * HorologeZr / HorologeTrain[i].Z;
                sense = -sense;
            }
            return (ring0 + d, wheels);
        }

        /// <summary>
        /// A tooth profile: the edge's radius at <paramref name="dir"/> on a gear of
        /// <paramref name="z"/> teeth at <paramref name="theta"/>, between the root and the tip -
        /// a trapezoid, flat on top, which is what a clock wheel's teeth look like face on. For
        /// the ring's inside, whose teeth point IN, the "tip" is the smaller radius.
        /// </summary>
        static float HorologeEdge(float dir, float theta, int z, float rootR, float tipR)
        {
            float t = Mathf.Repeat((dir - theta) * z / (Mathf.PI * 2f) + 0.5f, 1f) - 0.5f;
            float a = Mathf.Abs(t);                                   // 0 tooth centre, 0.5 gap
            const float top = 0.17f, bottom = 0.31f;
            float u = Mathf.Clamp01((a - top) / (bottom - top));      // 0 on the tip, 1 in the root
            return Mathf.Lerp(tipR, rootR, u);
        }

        /// <summary>
        /// The disc at (x, y) in grid cells, rows DOWN, at <paramref name="scale"/> texels per
        /// cell, the ring turned <paramref name="d"/> radians. Front to back: the jewels, the
        /// bridge, the wheels, the ring.
        /// </summary>
        static char HorologeTexel(float x, float y, int scale, float d)
        {
            bool menu = scale > 1;
            float px = 1f / scale;
            var (ringA, wheelA) = HorologeAngles(d);
            var centres = HorologeCentres;
            var p = new Vector2(x - HorologeCx, y - HorologeCy);
            float r = p.magnitude;
            float dir = Mathf.Atan2(p.y, p.x);
            var light = new Vector2(-0.7071f, -0.7071f);             // from the upper left, rows down
            float band = menu ? 0.4f : 0.9f;                          // the bevel round every edge

            // ---- the bridge: a ruby at every axle in a steel setting, the hub, an arm to each
            foreach (var axle in centres)
            {
                float toJewel = Vector2.Distance(p, axle);
                if (toJewel < 0.75f)
                {
                    if (!menu) return 'r';
                    var off = (p - axle) / 0.75f;
                    if (Vector2.Distance(off, new Vector2(-0.35f, -0.35f)) < 0.3f) return 'q';
                    return Vector2.Dot(off, light) < -0.45f ? 'o' : 'r';
                }
                if (toJewel < (menu ? 1.0f : 1.1f)) return 'L';       // the jewel's steel setting
            }
            if (r < 1.9f) return r > 1.9f - px * 1.5f && Vector2.Dot(p / Mathf.Max(r, 1e-4f), light) > 0.2f ? 'L' : 'B';
            foreach (var axle in centres)
            {
                float length = axle.magnitude;
                var along = axle / length;
                float s = Vector2.Dot(p, along);
                if (s < 0f || s > length) continue;
                float side = p.x * -along.y + p.y * along.x;          // signed distance off the arm
                if (Mathf.Abs(side) < 0.7f)
                {
                    // lit on whichever side faces the light
                    var normal = new Vector2(-along.y, along.x) * Mathf.Sign(side);
                    bool edge = Mathf.Abs(side) > 0.7f - (menu ? 0.3f : 0.5f);
                    if (!edge) return 'B';
                    return Vector2.Dot(normal, light) > 0f ? 'L' : 'D';
                }
            }

            // ---- a wheel: a shade BRIGHTER than the ring, so meshing teeth part by tone - one
            // brass throughout ran the train into one jumble, and darker sank the wheels into the
            // dark gaps round them. The menu rings every wheel in a one-texel contour; the arena
            // has no room for a bevel on a wheel this small (it came out as speckle) and lights
            // only the edge facing the light. Crossings: a rim, a hub, spokes turning with the
            // wheel, and the windows between them in deep shade - a recess, not a hole, which the
            // outline would have closed.
            char Wheel(Vector2 local, float theta, int z, int spokes)
            {
                float pitchR = HorologePitch(z);
                float lr = local.magnitude;
                float ldir = Mathf.Atan2(local.y, local.x);
                float root = pitchR - HorologeDed * HorologeM;
                float edge = HorologeEdge(ldir, theta, z, root, pitchR + HorologeAdd * HorologeM);
                if (lr >= edge) return '\0';
                var n = local / Mathf.Max(lr, 1e-4f);
                if (menu && edge - lr < px) return 's';                       // the contour
                float lit = Vector2.Dot(n, light);
                if (edge - lr < band && lit > 0.35f) return 'h';
                if (spokes > 0)
                {
                    float rimIn = root - 1.1f, hub = 1.3f;
                    if (lr > hub && lr < rimIn)
                    {
                        float step = Mathf.PI * 2f / spokes;
                        float off = Mathf.Repeat(ldir - theta + step / 2f, step) - step / 2f;
                        float across = Mathf.Abs(Mathf.Sin(off)) * lr;       // distance off the spoke
                        if (across > 0.55f) return menu && lr > rimIn - px ? 'd' : 's';
                        if (menu && across > 0.55f - px) return 'd';
                    }
                    else if (menu && (Mathf.Abs(lr - rimIn) < px || Mathf.Abs(lr - hub) < px)) return 'b';
                }
                if (menu && Mathf.Repeat(lr / 0.5f, 1f) < 0.22f) return 'b';   // circular graining
                return 'l';
            }

            for (int i = 0; i < centres.Length; i++)
            {
                char g = Wheel(p - centres[i], wheelA[i], HorologeTrain[i].Z, HorologeTrain[i].Spokes);
                if (g != '\0') return g;
            }

            // ---- the ring: teeth in, teeth out
            float inner = HorologeEdge(dir, ringA, HorologeZr, HorologeRr + HorologeDed * HorologeM, HorologeRr - HorologeAdd * HorologeM);
            float outer = HorologeEdge(dir, ringA, HorologeZe, HorologeRoot, HorologeTip);
            if (r <= inner || r >= outer) return '.';
            var rn = p / Mathf.Max(r, 1e-4f);
            if (menu && (outer - r < px || r - inner < px)) return 's';        // the contour
            if (outer - r < band)
            {
                float lit = Vector2.Dot(rn, light);
                return lit > 0.35f ? (menu && lit > 0.8f ? 'h' : 'l') : lit < -0.35f ? 'd' : 'b';
            }
            // the inside is left plain: lit, it matched the planets' brighter brass and the mesh
            // ran together
            // one engraved line round the middle of the band - round, so it survives the turning.
            // Menu only: the arena band is two cells, and a line took half of it.
            float mid = (HorologeRr + HorologeDed * HorologeM + HorologeRoot) / 2f;
            if (menu && Mathf.Abs(r - mid) < px * 0.75f) return 's';
            if (menu && Mathf.Repeat(r / 0.5f, 1f) < 0.22f) return 'l';
            return 'b';
        }

        static string[] BuildHorologeAt(int scale, float d)
        {
            int w = HorologeW * scale, h = HorologeH * scale;
            var rows = new string[h];
            var sb = new System.Text.StringBuilder(w);
            for (int row = 0; row < h; row++)
            {
                sb.Clear();
                for (int col = 0; col < w; col++)
                    sb.Append(HorologeTexel((col + 0.5f) / scale, (row + 0.5f) / scale, scale, d));
                rows[row] = sb.ToString();
            }
            return scale == 1 ? rows : RoseDropIslands(rows);
        }

        /// <summary>Clock brass (lowercase), blued steel (uppercase), and the rubies: r body,
        /// o shadow, q glint.</summary>
        static Dictionary<char, Color> HorologePal()
        {
            var pal = Palette.Of(new Palette.Ramp(new Color(0.86f, 0.68f, 0.30f), lift: 0.42f, shade: 0.40f),
                                 new Palette.Ramp(new Color(0.20f, 0.30f, 0.66f), lift: 0.40f, shade: 0.35f));
            pal['r'] = new Color(0.80f, 0.08f, 0.18f);
            pal['o'] = new Color(0.46f, 0.03f, 0.10f);
            pal['q'] = new Color(1.00f, 0.62f, 0.66f);
            return pal;
        }

        /// <summary>
        /// One hand's loop: HorologeTicksPerLoop ticks, each a quick step and a hold. The ring
        /// turns one of its inner teeth a tick, <paramref name="sense"/> 1 or -1 (the off hand turns
        /// back). Frames at the same point in the turn share one sprite, and frame 0 is the still.
        /// </summary>
        static Sprite[] HorologeFrames(string key, float sense, Sprite still)
        {
            float pitch = Mathf.PI * 2f / HorologeZr;
            var pal = HorologePal();
            var made = new Dictionary<int, Sprite> { [0] = still };
            var frames = new Sprite[HorologeTickFrames * HorologeTicksPerLoop];
            for (int f = 0; f < frames.Length; f++)
            {
                int tick = f / HorologeTickFrames, within = f % HorologeTickFrames;
                int stepping = within - (HorologeTickFrames - HorologeTickCurve.Length);
                float step = stepping >= 0 ? HorologeTickCurve[stepping] : 0f;
                // where the ring is, in teeth, wrapped to the loop: the last tick's end is the start
                float teeth = Mathf.Repeat(tick + step, HorologeTicksPerLoop);
                int id = Mathf.RoundToInt(teeth * 100f);
                if (!made.TryGetValue(id, out var sprite))
                {
                    sprite = StageSprite($"{key}.t{id}", BuildHorologeAt(1, sense * teeth * pitch), pal, HorologeGrip);
                    made[id] = sprite;
                }
                frames[f] = sprite;
            }
            return frames;
        }

        static GearItem Horologe()
        {
            const string key = "gear.weapon.disc.horologe";
            var arena = Pixels(RigLayer.Weapon, key, BuildHorologeAt(1, 0f), HorologePal(),
                               DiscHandX, DiscHandY, pivotTexel: HorologeGrip, ppu: FinePpu, upscale2x: true);
            var menu = Pixels(RigLayer.Weapon, key + ".menu", BuildHorologeAt(HorologeDetailScale, 0f), HorologePal(),
                              DiscHandX, DiscHandY, pivotTexel: HorologeDetailGrip, ppu: MenuPpu);
            var item = Disc(WithMenu(Make("horologe_discs", "Horologe", GearSlot.Weapon, LootTier.Diamond, 0f, arena), menu));

            // The off hand holds the same still and turns the other way on the same tick.
            item.OffhandLayer = arena;
            item.OffhandMenuLayer = menu;
            item.IdleFrames = HorologeFrames(key, 1f, arena.Sprite);
            item.OffhandIdleFrames = HorologeFrames(key + ".back", -1f, arena.Sprite);
            item.IdleFrameSeconds = HorologeFrameSeconds;
            return item;
        }
    }
}
