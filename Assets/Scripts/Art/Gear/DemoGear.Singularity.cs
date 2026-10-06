using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Singularity: a ring of light falling in
    //
    // The user's brief: a dual disc that is a ring of PARTICLE LIGHT round a TRANSLUCENT BLACK
    // CORE - the core a singularity that draws the light in. A black hole's accretion ring, held:
    //
    //     the HORIZON        a translucent black core; the fist closes over it
    //     the PHOTON RING    a thin bright circle on the horizon's edge - the part of a black
    //                        hole's picture that reads at a glance
    //     the RING           bright white particles swirling in a band, each one in turn
    //                        spiralling IN - speeding up, stretching into a streak - and
    //                        vanishing at the horizon
    //     the HAZE           a SEMI-TRANSPARENT white glow under the band, so the silhouette
    //                        stays a disc while the particles on it move
    //
    // The drawing-in IS the animation (SingularityFrames): each particle's fall is a function of
    // one phase, and every particle falls a WHOLE number of times a loop, so the loop closes. A
    // mote ORBITS in the band for most of its phase (SingOrbitShare) and then PLUNGES, quickly -
    // which is what keeps the ring a ring and the gap under it dark. Easing one curve from band to
    // horizon (the first build) kept a third of the motes mid-fall at any moment: near the
    // horizon they crowded into a solid white donut, and once the band moved out they filled the
    // whole gap with gold. The OFF HAND has
    // its own particles and turns the other way - a pair spinning in step reads as one sprite
    // pasted twice.
    //
    // WHITE light with WARM particles, by the user's two calls: built warm white-gold (after
    // Interstellar's Gargantua) it read too saturated, turned pure white it read washed out, so the
    // particles sit between - warm white, pale gold, soft amber - in a semi-transparent WHITE ring,
    // round a white photon ring and the black horizon. And it LIGHTS its holder (GlowColor - see
    // HeldGlow). OUTLINELESS, like
    // Rai: an ink line round light frames it as a tube. Its grid makes the outline's pad up out
    // of itself, the disc standard as DRAWN (RaiH's rule). ONE field (SingularityTexel), per cell
    // for the arena and per quarter cell for the menu.
    public static partial class DemoGear
    {
        static int SingH => DiscHeightCells + 2 * PixelSprite.OutlineCanvasPadFor(FinePpu) / FineUpscale;
        static int SingW => SingH;
        static float SingC => SingH / 2f;

        /// <summary>The horizon's radius, and the photon ring's width round it, in cells.</summary>
        const float SingHorizon = 5f, SingPhotonRing = 0.9f;
        /// <summary>Where particles start their fall (the band) - a NARROW ring well out from the
        /// horizon, by the user's call (it was 9.5..15), so a dark gap opens between the light and
        /// the core for the falling streaks to cross - and how far out its glow runs:
        /// solid to within a cell of the grid's edge, then a faint fringe. The glow is the disc's
        /// SILHOUETTE, so it has to reach the disc standard - at 14.7 the solid part drew 30 x 30
        /// against every other disc's 32. The outermost cells' centres are 15.5 out, so the solid
        /// glow has to pass that.</summary>
        const float SingBandIn = 12.5f, SingBandOut = 15f, SingGlowOut = 15.7f, SingHazeOut = 16.2f;

        const int SingParticles = 80;
        const int SingDetailScale = 4;
        const int SingFrameCount = 24;
        const float SingFrameSeconds = 0.06f;
        const int SingMainSeed = 4242, SingOffSeed = 9191;

        static Vector2Int SingGrip => new(SingW / 2, SingH / 2);
        static Vector2Int SingDetailGrip => new(SingGrip.x * SingDetailScale, SingGrip.y * SingDetailScale);

        /// <summary>One particle: where round the ring it starts, where in its fall it is at the
        /// loop's start, the radius it falls from, how many times it falls a loop, and how bright
        /// it runs (0 dim .. 1 bright).</summary>
        struct SingMote { public float Angle, Phase, From; public int Falls; public float Heat; }

        static List<SingMote> SingMotes(int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var motes = new List<SingMote>(SingParticles);
            for (int i = 0; i < SingParticles; i++)
                motes.Add(new SingMote
                {
                    Angle = R(0f, Mathf.PI * 2f),
                    Phase = R(0f, 1f),
                    // weighted toward the band's middle, so the ring has a body
                    From = Mathf.Lerp(SingBandIn, SingBandOut, (R(0f, 1f) + R(0f, 1f)) * 0.5f),
                    Falls = rng.NextDouble() < 0.7 ? 1 : 2,
                    Heat = R(0f, 1f),
                });
            return motes;
        }

        /// <summary>The share of a mote's phase spent orbiting in the band before it plunges.</summary>
        const float SingOrbitShare = 0.86f;

        /// <summary>
        /// Where a mote is at fall-phase <paramref name="s"/> (0 at its start in the band, 1 at the
        /// horizon), cells off the centre, turning <paramref name="sense"/> 1 or -1. First it
        /// orbits, drifting a little inward; then it plunges, gathering speed as it drops and
        /// turning faster the closer it gets.
        /// </summary>
        static Vector2 SingAt(SingMote m, float s, float sense)
        {
            const float drift = 0.6f;
            float r, turns;
            if (s < SingOrbitShare)
            {
                float u = s / SingOrbitShare;
                r = m.From - drift * u;
                turns = 0.30f * u;
            }
            else
            {
                float u = (s - SingOrbitShare) / (1f - SingOrbitShare);
                r = Mathf.Lerp(m.From - drift, SingHorizon, Mathf.Pow(u, 1.6f));
                turns = 0.30f + 0.45f * u + 0.35f * u * u;
            }
            float a = m.Angle + sense * Mathf.PI * 2f * turns;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        /// <summary>How far back along its path a mote's streak reaches, in phase: a dot while it
        /// orbits, a fifth of the plunge once it falls.</summary>
        static float SingTrail(float s)
            => s < SingOrbitShare ? 0.02f : 0.2f * (1f - SingOrbitShare);

        /// <summary>
        /// The disc at (x, y) in grid cells, rows DOWN, at <paramref name="scale"/> texels per
        /// cell, <paramref name="t"/> through the loop (0..1). Front to back: the horizon, the
        /// photon ring, the motes, the haze.
        /// </summary>
        static char SingularityTexel(float x, float y, int scale, List<SingMote> motes, float t, float sense)
        {
            bool menu = scale > 1;
            var p = new Vector2(x - SingC, y - SingC);
            float r = p.magnitude;

            // ---- the horizon, and the photon ring on its edge
            if (r < SingHorizon) return r > SingHorizon - (menu ? 0.5f : 1f) ? 'k' : 'K';
            if (r < SingHorizon + SingPhotonRing)
                return menu && Mathf.Abs(r - SingHorizon - SingPhotonRing * 0.4f) < 0.2f ? 'W' : 'Y';

            // ---- the motes: each a streak from where it is back along where it came from,
            // longer and hotter the deeper it has fallen. The streak FOLLOWS the path, in short
            // pieces - drawn as one straight segment, a plunging mote cut across its own spiral
            // as a chord and the ring read as a polygon.
            const int pieces = 5;
            int best = 0;
            foreach (var m in motes)
            {
                float s = Mathf.Repeat(m.Phase + t * m.Falls, 1f);
                if (s > 0.97f) continue;                                    // swallowed
                float from = Mathf.Max(0f, s - SingTrail(s));
                float size = menu ? 0.22f + 0.18f * s : 0.55f;
                // a quick reject: nowhere near the streak's head or tail
                var head = SingAt(m, s, sense);
                if (Vector2.Distance(p, head) > 9f) continue;

                float d = float.MaxValue, along = 0f;
                var prev = SingAt(m, from, sense);
                for (int i = 1; i <= pieces; i++)
                {
                    var next = SingAt(m, Mathf.Lerp(from, s, i / (float)pieces), sense);
                    var ab = next - prev;
                    float k = Mathf.Clamp01(Vector2.Dot(p - prev, ab) / Mathf.Max(ab.sqrMagnitude, 1e-5f));
                    float di = Vector2.Distance(p, prev + ab * k);
                    if (di < d) { d = di; along = (i - 1 + k) / pieces; }
                    prev = next;
                }
                if (d > size) continue;

                // Swirling in the band a mote is warm white, pale gold or soft amber by its own
                // heat - the colour the ring has; falling, it burns warm white. One just born fades
                // in, and a falling streak's tail cools a step, so it reads as a comet with its
                // head at the front.
                bool falling = s >= SingOrbitShare;
                int level = falling || m.Heat > 0.6f ? 4 : m.Heat > 0.25f ? 3 : 2;
                if (s < 0.06f) level = 1;
                if (along < 0.4f && s >= SingOrbitShare) level--;
                if (level > best) best = level;
            }
            if (best > 0) return best switch { 4 => 'W', 3 => 'Y', 2 => 'O', _ => 'R' };

            // ---- the haze: the band's glow, and the faint light already drawn in
            if (r >= SingBandIn - 0.5f && r < SingGlowOut) return 'A';
            if (r < SingHazeOut && r >= SingBandIn - 0.5f) return 'a';
            if (r < SingBandIn - 0.5f) return 'i';
            return '.';
        }

        static string[] BuildSingularityAt(int scale, List<SingMote> motes, float t, float sense)
        {
            int w = SingW * scale, h = SingH * scale;
            var rows = new string[h];
            var sb = new System.Text.StringBuilder(w);
            for (int row = 0; row < h; row++)
            {
                sb.Clear();
                for (int col = 0; col < w; col++)
                    sb.Append(SingularityTexel((col + 0.5f) / scale, (row + 0.5f) / scale, scale, motes, t, sense));
                rows[row] = sb.ToString();
            }
            return rows;
        }

        /// <summary>
        /// The particles, brightest first: warm white W, pale gold Y, soft amber O, a dim ember R
        /// - halfway between the first build's saturated amber and the all-white that followed.
        /// The ring's glow SEMI-TRANSPARENT white in two steps (A the band - half alpha, so it
        /// still counts as the disc's edge - and a its fringe), the faint light already falling
        /// (i). The horizon K, black and TRANSLUCENT - the floor shows through it, darkly - with a
        /// denser rim k.
        /// </summary>
        static Dictionary<char, Color> SingularityPal() => new()
        {
            ['W'] = new Color(1.00f, 0.97f, 0.90f),
            ['Y'] = new Color(1.00f, 0.88f, 0.64f),
            ['O'] = new Color(0.98f, 0.72f, 0.46f),
            ['R'] = new Color(0.86f, 0.54f, 0.36f),
            ['A'] = new Color(0.92f, 0.94f, 1.00f, 0.50f),
            ['a'] = new Color(0.90f, 0.92f, 1.00f, 0.24f),
            ['i'] = new Color(0.90f, 0.92f, 1.00f, 0.10f),
            ['K'] = new Color(0.02f, 0.01f, 0.03f, 0.80f),
            ['k'] = new Color(0.01f, 0.00f, 0.02f, 0.92f),
        };

        static Sprite SingularityFrame(string key, string[] rows)
            => PixelSprite.From(key, Double2x(rows), SingularityPal(), outline: false,
                                pivotTexel: new Vector2Int(SingGrip.x * FineUpscale, SingGrip.y * FineUpscale),
                                pixelsPerUnit: FinePpu);

        /// <summary>One hand's loop - the same motes through one whole loop. Frame 0 is the still.</summary>
        static Sprite[] SingularityFrames(string key, List<SingMote> motes, float sense, Sprite still)
        {
            var frames = new Sprite[SingFrameCount];
            frames[0] = still;
            for (int i = 1; i < SingFrameCount; i++)
                frames[i] = SingularityFrame($"{key}.fall{i}",
                    BuildSingularityAt(1, motes, i / (float)SingFrameCount, sense));
            return frames;
        }

        static GearItem Singularity()
        {
            const string key = "gear.weapon.disc.singularity", offKey = key + ".off";
            var mainMotes = SingMotes(SingMainSeed);
            var offMotes = SingMotes(SingOffSeed);

            var main = OutlinelessWeapon(key, BuildSingularityAt(1, mainMotes, 0f, 1f), SingularityPal(),
                                         SingGrip, DiscHandY, unlit: true);
            var item = Disc(WithMenu(Make("singularity_discs", "Singularity", GearSlot.Weapon, LootTier.Diamond, 0f, main),
                OutlinelessMenuWeapon(key + ".menu", BuildSingularityAt(SingDetailScale, mainMotes, 0f, 1f),
                                      SingularityPal(), SingDetailGrip, DiscHandY)));

            // The off hand: its own motes, turning the other way - see the header.
            item.OffhandLayer = OutlinelessWeapon(offKey, BuildSingularityAt(1, offMotes, 0f, -1f),
                                                  SingularityPal(), SingGrip, DiscHandY, unlit: true);
            item.OffhandMenuLayer = OutlinelessMenuWeapon(offKey + ".menu",
                BuildSingularityAt(SingDetailScale, offMotes, 0f, -1f), SingularityPal(),
                SingDetailGrip, DiscHandY);

            item.IdleFrames = SingularityFrames(key, mainMotes, 1f, main.Sprite);
            item.OffhandIdleFrames = SingularityFrames(offKey, offMotes, -1f, item.OffhandLayer.Sprite);
            item.IdleFrameSeconds = SingFrameSeconds;

            // a soft warm glow on whoever holds it - the particles' own warm white - from the RING,
            // not the horizon: a black hole's core gives no light
            item.GlowColor = new Color(1.00f, 0.90f, 0.74f);
            item.GlowIntensity = 0.3f;
            item.GlowFromRim = true;
            return item;
        }
    }
}
