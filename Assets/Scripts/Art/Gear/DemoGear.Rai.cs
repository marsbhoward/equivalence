using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Rai: lightning, in disc form
    //
    // The user's brief: a dual disc that is an EMBODIMENT of lightning - Naruto's Chidori as a
    // chakram, named Rai (thunder). There is no metal in it. The ring is a current running round on itself (four
    // jagged strands braided over a steady blue glow), short tendrils crackle off its rim, and
    // the fist closes on a white-hot knot at the centre - the palm the lightning is born in - with
    // spokes of lightning running out from it to the ring.
    //
    // The GLOW is what keeps it a disc: the strands re-jag every frame and would read as a
    // scribble, so a steady semi-transparent aura runs round under them and the silhouette holds
    // still while the lightning on it does not.
    //
    // OUTLINELESS (OutlinelessWeapon), like Lumen: a black cartoon line round lightning frames
    // every bolt as its own tube. The deep blue at each strand's edge is its outline instead.
    //
    // ANIMATED: every frame is a fresh random crackle (RaiFrames), fast - on a slow beat it
    // read as a pattern. The off hand draws its OWN crackle (GearItem.OffhandLayer, the
    // Armillary's mechanism) from a different seed, or the pair would flicker in lockstep and
    // read as one sprite copied.
    //
    // The long bolts are an EFFECT, not paint (Combat/LightningArcs): streaks leaping off the ring
    // at random, out to a greatsword's length (RaiArcReachCells), in the hand, on a display
    // and in flight. Painted, they would have had to fit inside the disc standard.
    //
    // The disc standard as DRAWN (RaiH - see there), square, the grip at the centre like the
    // plain discs, and a band as broad as theirs: it was first built at radius 8 to leave the
    // tendrils room, and drew at under half the other discs' area. ONE field (RaiTexel), per
    // cell for the arena and per quarter cell for the menu, so the menu art's silhouette is the
    // arena frame's.
    public static partial class DemoGear
    {
        /// <summary>
        /// The grid, in cells: the disc standard as DRAWN. Every other disc is
        /// <see cref="DiscHeightCells"/> of grid plus the auto-outline's canvas pad either side;
        /// this one has no outline, so it makes the pad up out of its own grid - the swords' rule
        /// (<see cref="BladeRowsForNoOutline"/>). Built at DiscHeightCells it drew four cells short.
        /// A property, not a const: the pad is derived.
        /// </summary>
        static int RaiH => DiscHeightCells + 2 * PixelSprite.OutlineCanvasPadFor(FinePpu) / FineUpscale;
        static int RaiW => RaiH;
        static float RaiCx => RaiW / 2f;
        static float RaiCy => RaiH / 2f;

        /// <summary>The middle of the current's band, in cells - where the strands run round.</summary>
        const float RaiRingR = 11.4f;
        /// <summary>The steady glow under the strands, half-widths off the band's middle in
        /// cells: dense, then faint. Sized so the band runs about as far in and out as a plain
        /// disc's ring does, drawn.</summary>
        const float RaiAuraIn = 3.2f, RaiAuraOut = 4.0f;
        /// <summary>The white-hot knot the fist closes on.</summary>
        const float RaiCoreR = 2.4f;
        /// <summary>How far a tendril may reach from the centre - the grid's edge less its own
        /// glow, or the edge would slice it flat.</summary>
        static float RaiMaxR => RaiW / 2f - 1.0f;

        const int RaiDetailScale = 4;
        static Vector2Int RaiGrip => new(RaiW / 2, RaiH / 2);
        static Vector2Int RaiDetailGrip => new(RaiGrip.x * RaiDetailScale,
                                                   RaiGrip.y * RaiDetailScale);

        const int RaiFrameCount = 16;
        const float RaiFrameSeconds = 0.06f;
        /// <summary>The seeds the two hands crackle from - see the header.</summary>
        const int RaiMainSeed = 1101, RaiOffSeed = 7307;

        /// <summary>The ring's radius in cells, for the arcs to leave from.</summary>
        public static float RaiRingCells => RaiRingR;

        /// <summary>
        /// How far an arc can reach, in the disc's own cells: a greatsword's height
        /// (<see cref="SwordHeightTexels"/>, in cells). Measured in CELLS rather than world units
        /// so it rides whatever scale the disc is drawn at - the hand, a display, a throw.
        /// </summary>
        public static float RaiArcReachCells => SwordHeightTexels / (float)FineUpscale;

        /// <summary>How tall the disc is in cells, for the arcs to measure a cell off its sprite.</summary>
        public static int RaiHeightCells => RaiH;

        /// <summary>One jagged line of the crackle, in cells: how heavy it is drawn at each end
        /// (1 is a strand of the ring; a tendril tapers toward its tip) and how BRIGHT its core
        /// is (4 white, 3 pale, 2 blue). One white strand among dimmer ones - three white strands
        /// merged into a solid white band.</summary>
        struct RaiStroke
        {
            public List<Vector2> Points;
            public float RootWeight, TipWeight;
            public int Level;
            public bool Closed;
        }

        /// <summary>
        /// One frame's crackle: the ring's strands, the tendrils off its rim and the spokes in
        /// from the knot. Seeded, so frame N of a hand is the same picture in the arena, the menu
        /// and every repaint.
        /// </summary>
        static List<RaiStroke> RaiStrokes(int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var strokes = new List<RaiStroke>();
            var c = new Vector2(RaiCx, RaiCy);

            // ---- the ring: four strands, each a closed jag round the band - white down the
            // middle, pale inside it, blue outside, and a wide blue one wandering across all
            // three. A second PALE strand speckled the arena band with light cells until the
            // white current stopped reading as one line.
            float[] bases = { 0f, -1.8f, 1.8f, 0f };
            float[] amps = { 1.4f, 0.9f, 0.9f, 2.6f };
            float[] weights = { 1f, 0.85f, 0.85f, 0.8f };
            int[] levels = { 4, 3, 2, 2 };
            for (int s = 0; s < amps.Length; s++)
            {
                int n = 16 + s * 3;
                float spin = R(0f, Mathf.PI * 2f);
                var pts = new List<Vector2>(n);
                for (int k = 0; k < n; k++)
                {
                    float a = spin + (k + R(-0.3f, 0.3f)) / n * Mathf.PI * 2f;
                    float r = RaiRingR + bases[s] + R(-amps[s], amps[s]);
                    pts.Add(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
                // and jagged again between those points, finer - straight runs between them read
                // as a polygon, not a current
                var jagged = new List<Vector2>(n * 4);
                for (int k = 0; k < n; k++)
                {
                    var run = RaiJag(pts[k], pts[(k + 1) % n], 0.35f, 1.2f, rng);
                    jagged.AddRange(run.GetRange(0, run.Count - 1));
                }
                strokes.Add(new RaiStroke { Points = jagged, RootWeight = weights[s], TipWeight = weights[s],
                                                Level = levels[s], Closed = true });
            }

            // ---- tendrils: short forked bolts crackling out of the band past its edge (the long
            // ones are LightningArcs'), and a few crackling IN across the hollow
            int outward = rng.Next(3, 6), inward = rng.Next(1, 3);
            for (int t = 0; t < outward + inward; t++)
            {
                bool outs = t < outward;
                float a = R(0f, Mathf.PI * 2f);
                var radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var from = c + radial * (RaiRingR + (outs ? R(0.5f, 2f) : -R(1f, 2.2f)));
                float dir = a + (outs ? 0f : Mathf.PI) + R(-0.6f, 0.6f);
                float len = outs ? R(2.5f, 4.5f) : R(2f, 3.5f);
                var to = RaiClamp(from + new Vector2(Mathf.Cos(dir), Mathf.Sin(dir)) * len, c);
                var pts = RaiJag(from, to, 0.45f, 1.6f, rng);
                strokes.Add(new RaiStroke { Points = pts, RootWeight = 0.9f, TipWeight = 0.45f,
                                                Level = outs ? 4 : 3 });

                if (outs && rng.NextDouble() < 0.5 && pts.Count > 2)
                {
                    var at = pts[pts.Count / 2];
                    float fdir = dir + (rng.NextDouble() < 0.5 ? -1f : 1f) * R(0.5f, 1f);
                    var fto = RaiClamp(at + new Vector2(Mathf.Cos(fdir), Mathf.Sin(fdir)) * len * R(0.35f, 0.6f), c);
                    strokes.Add(new RaiStroke { Points = RaiJag(at, fto, 0.5f, 1.4f, rng),
                                                    RootWeight = 0.6f, TipWeight = 0.4f, Level = 3 });
                }
            }

            // ---- spokes: the knot reaching out to the band through the hollow
            int spokes = rng.Next(2, 4);
            float spokeSpin = R(0f, Mathf.PI * 2f);
            float spokeTo = RaiRingR - RaiAuraIn + 0.5f;
            for (int k = 0; k < spokes; k++)
            {
                float a = spokeSpin + (k + R(-0.25f, 0.25f)) / spokes * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                strokes.Add(new RaiStroke
                {
                    Points = RaiJag(c + dir * (RaiCoreR - 0.5f), c + dir * spokeTo, 0.5f, 1.5f, rng),
                    RootWeight = 0.65f, TipWeight = 0.5f, Level = 3,
                });
            }
            return strokes;
        }

        /// <summary>Pull a point back inside <see cref="RaiMaxR"/> of the centre.</summary>
        static Vector2 RaiClamp(Vector2 p, Vector2 c)
        {
            var d = p - c;
            return d.magnitude > RaiMaxR ? c + d.normalized * RaiMaxR : p;
        }

        /// <summary>
        /// A jagged line from <paramref name="a"/> to <paramref name="b"/> by midpoint
        /// displacement: each midpoint pushed sideways by up to <paramref name="rough"/> of its
        /// segment, until segments are under <paramref name="minSeg"/> cells.
        /// </summary>
        static List<Vector2> RaiJag(Vector2 a, Vector2 b, float rough, float minSeg, System.Random rng)
        {
            var pts = new List<Vector2> { a, b };
            for (int pass = 0; pass < 6; pass++)
            {
                bool split = false;
                var next = new List<Vector2>(pts.Count * 2) { pts[0] };
                for (int i = 1; i < pts.Count; i++)
                {
                    var p = pts[i - 1];
                    var q = pts[i];
                    float len = Vector2.Distance(p, q);
                    if (len > minSeg)
                    {
                        var n = new Vector2(-(q.y - p.y), q.x - p.x) / len;
                        next.Add((p + q) * 0.5f + n * ((float)rng.NextDouble() * 2f - 1f) * rough * len);
                        split = true;
                    }
                    next.Add(q);
                }
                pts = next;
                if (!split) break;
            }
            return pts;
        }

        /// <summary>
        /// The disc at (x, y) in grid cells, rows DOWN, at <paramref name="scale"/> texels per
        /// cell. Front to back: the knot, the strokes, the steady glow.
        ///
        /// Each stroke is its core (at the stroke's own level), then blue, then a deep edge; the
        /// brightest stroke over a texel wins. The ARENA core is one whole cell (half a cell
        /// either side - under that a diagonal sampled at cell centres breaks into dots) with a
        /// single cell of glow; the MENU draws the same strokes with a finer core and a pale band
        /// round the white one. The deep edge is the same distance at both, so the silhouette is
        /// the arena's.
        /// </summary>
        static char RaiTexel(float x, float y, int scale, List<RaiStroke> strokes)
        {
            bool menu = scale > 1;
            var p = new Vector2(x, y);
            float dx = x - RaiCx, dy = y - RaiCy;
            float r = Mathf.Sqrt(dx * dx + dy * dy);

            // ---- the knot
            if (r < RaiCoreR * 0.55f) return 'W';
            if (r < RaiCoreR * 0.8f) return 'L';
            if (r < RaiCoreR) return 'B';

            // ---- the strokes
            int best = 0;
            foreach (var s in strokes)
            {
                var pts = s.Points;
                int segs = s.Closed ? pts.Count : pts.Count - 1;
                for (int i = 0; i < segs; i++)
                {
                    var a = pts[i];
                    var b = pts[(i + 1) % pts.Count];
                    var ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
                    float d = Vector2.Distance(p, a + ab * t);
                    float w = Mathf.Lerp(s.RootWeight, s.TipWeight, s.Closed ? 0f : (i + t) / segs);
                    float edge = Mathf.Max(1.5f * w, menu ? 0f : 1.05f);       // the silhouette
                    int tone;
                    if (menu)
                        tone = d < 0.3f * w ? s.Level
                             : d < 0.6f * w && s.Level == 4 ? 3
                             : d < 1.05f * w ? 2 : d < edge ? 1 : 0;
                    else
                        tone = d < 0.5f ? s.Level : d < Mathf.Max(1f * w, 0.95f) ? 2 : d < edge ? 1 : 0;
                    if (tone > best) best = tone;
                }
            }
            if (best > 0) return best switch { 4 => 'W', 3 => 'L', 2 => 'B', _ => 'D' };

            // ---- the steady glow round the ring
            float off = Mathf.Abs(r - RaiRingR);
            if (off < RaiAuraIn) return 'A';
            if (off < RaiAuraOut) return 'a';
            return '.';
        }

        static string[] BuildRaiAt(int scale, int seed)
        {
            var strokes = RaiStrokes(seed);
            int w = RaiW * scale, h = RaiH * scale;
            var rows = new string[h];
            var sb = new System.Text.StringBuilder(w);
            for (int row = 0; row < h; row++)
            {
                sb.Clear();
                for (int col = 0; col < w; col++)
                    sb.Append(RaiTexel((col + 0.5f) / scale, (row + 0.5f) / scale, scale, strokes));
                rows[row] = sb.ToString();
            }
            return rows;
        }

        /// <summary>
        /// White-hot core W, pale electric L, electric blue B, a deep blue edge D, and the steady
        /// glow round the ring in two translucent steps (A dense, a faint). Emissive, so placed by
        /// hand rather than ramped.
        /// </summary>
        static Dictionary<char, Color> RaiPal() => new()
        {
            ['W'] = new Color(0.96f, 0.99f, 1.00f),
            ['L'] = new Color(0.62f, 0.85f, 1.00f),
            ['B'] = new Color(0.24f, 0.54f, 1.00f),
            ['D'] = new Color(0.13f, 0.25f, 0.86f),
            ['A'] = new Color(0.14f, 0.32f, 0.95f, 0.55f),
            ['a'] = new Color(0.10f, 0.20f, 0.80f, 0.30f),
        };

        /// <summary>A crackle frame, as the weapon layer's own sprite: doubled and pivoted exactly
        /// as <see cref="OutlinelessWeapon"/> builds the still, so a frame swap never moves it.</summary>
        static Sprite RaiFrame(string key, string[] rows)
            => PixelSprite.From(key, Double2x(rows), RaiPal(), outline: false,
                                pivotTexel: new Vector2Int(RaiGrip.x * FineUpscale, RaiGrip.y * FineUpscale),
                                pixelsPerUnit: FinePpu);

        static int RaiSeed(int hand, int frame) => hand * 1000 + frame;

        /// <summary>One hand's crackle loop. Frame 0 is the hand's still.</summary>
        static Sprite[] RaiFrames(string key, int hand)
        {
            var frames = new Sprite[RaiFrameCount];
            for (int i = 0; i < RaiFrameCount; i++)
                frames[i] = i == 0 ? null : RaiFrame($"{key}.crackle{i}", BuildRaiAt(1, RaiSeed(hand, i)));
            return frames;
        }

        static GearItem Rai()
        {
            const string key = "gear.weapon.disc.rai", offKey = key + ".off";
            var main = OutlinelessWeapon(key, BuildRaiAt(1, RaiSeed(RaiMainSeed, 0)), RaiPal(),
                                         RaiGrip, DiscHandY, unlit: true);
            var item = Disc(WithMenu(Make("rai_discs", "Rai", GearSlot.Weapon, LootTier.Diamond, 0f, main),
                OutlinelessMenuWeapon(key + ".menu", BuildRaiAt(RaiDetailScale, RaiSeed(RaiMainSeed, 0)),
                                      RaiPal(), RaiDetailGrip, DiscHandY)));

            // The off hand's own crackle - see the header.
            item.OffhandLayer = OutlinelessWeapon(offKey, BuildRaiAt(1, RaiSeed(RaiOffSeed, 0)), RaiPal(),
                                                  RaiGrip, DiscHandY, unlit: true);
            item.OffhandMenuLayer = OutlinelessMenuWeapon(offKey + ".menu",
                BuildRaiAt(RaiDetailScale, RaiSeed(RaiOffSeed, 0)), RaiPal(),
                RaiDetailGrip, DiscHandY);

            // Frame 0 of each loop IS that hand's still, so the loop and the still agree.
            item.IdleFrames = RaiFrames(key, RaiMainSeed);
            item.IdleFrames[0] = main.Sprite;
            item.OffhandIdleFrames = RaiFrames(offKey, RaiOffSeed);
            item.OffhandIdleFrames[0] = item.OffhandLayer.Sprite;
            item.IdleFrameSeconds = RaiFrameSeconds;

            item.LightningArcs = true;
            return item;
        }
    }
}
