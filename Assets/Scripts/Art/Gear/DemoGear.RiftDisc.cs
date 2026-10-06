using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Rift Disc: an obsidian ring with a light cycle's trail
    //
    // After the user's sketch: an OBSIDIAN ring cut flat across the top, two LED bands running
    // round its inside - purple outside, cyan in - that turn straight UP at the top and end there
    // at the flat edge as EMITTERS either side of the grip. The sketch's trails rising off them
    // are an EFFECT now, not paint (Combat/LightCycleTrail, the user's call): a Tron light
    // cycle's wall left behind wherever the disc goes. The grip is a wrap across the top,
    // bridging the hollow between the emitters. Kills implode (GearItem.ImplodesKills), the
    // Rift Blade's own death.
    //
    // The disc standard (DiscHeightCells) from the flat top to the bottom. The pivot is the
    // grip, at the top: the hand holds the ring up by it. (A thrown disc spins about its sprite's CENTRE - Combat/DiscVisual - so
    // an off-centre grip no longer makes it orbit its own flight path.)
    //
    // ANIMATED (RiftDiscFrames, arena only): pulses of light run up both sides of the ring to the
    // emitters, looping - the path is measured from the bottom of the ring, so both sides
    // move together and the picture stays mirror-safe. Every term is a whole number of cycles per
    // loop. SetWeaponSprite carries each frame to the off-hand copy.
    //
    // Widths are in CELLS, not fractions of the ring: at arena density the sketch's cyan line is
    // a fraction of a cell, so each band is a whole number of cells (cyan 1, purple 2) and the
    // emitters sit on cell boundaries, so they come out as clean vertical lines. ONE field
    // (RiftDiscTexel), per cell for the arena and per quarter cell for the menu.
    public static partial class DemoGear
    {
        const int RiftDiscW = 30;
        const int RiftDiscH = DiscHeightCells;

        /// <summary>Outer radius, and how far above the centre the top is cut flat (the sketch's
        /// chord sits at 0.86 of the radius) - together exactly the disc standard tall.</summary>
        const float RiftDiscR = DiscHeightCells / 1.86f;
        const float RiftDiscChord = 0.86f * RiftDiscR;
        const float RiftDiscCx = RiftDiscW / 2f;
        const float RiftDiscCy = RiftDiscChord;

        /// <summary>The bands inside the ring, radii in cells: hollow, then cyan, then purple, then
        /// obsidian out to the rim.</summary>
        const float RiftDiscHollow = 9.5f, RiftDiscCyanOut = RiftDiscHollow + 1f, RiftDiscPurpleOut = RiftDiscCyanOut + 2f;

        /// <summary>Where the bands turn UP to the flat top - the EMITTERS the light-cycle wall
        /// leaves from - off the centre line, in cells: cyan inside, purple outside, the same order
        /// as the rings they turn out of. On cell boundaries.</summary>
        const float RiftDiscTrailIn = 5f, RiftDiscTrailCyanOut = 6f, RiftDiscTrailPurpleOut = 8f;

        const int RiftDiscDetailScale = 4;
        /// <summary>On the wrap, between the hollow's top and the flat top.</summary>
        static readonly Vector2Int RiftDiscGrip = new((int)RiftDiscCx, 1);
        static readonly Vector2Int RiftDiscDetailGrip = new(RiftDiscGrip.x * RiftDiscDetailScale, RiftDiscGrip.y * RiftDiscDetailScale);

        const int RiftDiscFrameCount = 20;
        const float RiftDiscFrameSeconds = 0.08f;
        /// <summary>Spacing between pulses along the path, in cells.</summary>
        const float RiftDiscPulseSpacing = 9f;

        /// <summary>Where an emitter meets the ring: the height (cells off the centre, up
        /// negative) at which the hollow's edge crosses the emitter's inner line.</summary>
        static float RiftDiscJoinDy => -Mathf.Sqrt(RiftDiscHollow * RiftDiscHollow - RiftDiscTrailIn * RiftDiscTrailIn);

        /// <summary>
        /// Distance along the LED path from the bottom of the ring, in cells - round the ring to
        /// the join, then up to the emitter. Measured per side from the bottom, so both sides pulse
        /// together.
        /// </summary>
        static float RiftDiscPathAt(float adx, float dy, bool onTrail)
        {
            const float mid = (RiftDiscHollow + RiftDiscPurpleOut) / 2f;
            float joinAngle = Mathf.Atan2(RiftDiscTrailIn, RiftDiscJoinDy);           // from straight down
            if (onTrail) return joinAngle * mid + (RiftDiscJoinDy - dy);
            return Mathf.Atan2(adx, dy) * mid;                                  // 0 at the bottom
        }

        /// <summary>
        /// The whole disc at (x, y) in grid cells, rows DOWN, at <paramref name="scale"/> texels per
        /// cell; <paramref name="phase"/> 0..1 through the pulse loop. Front to back: the LED bands
        /// and emitters, the wrap, the obsidian ring.
        /// </summary>
        static char RiftDiscTexel(float x, float y, int scale, float phase)
        {
            bool menu = scale > 1;
            float px = 1f / scale;
            float dx = x - RiftDiscCx, dy = y - RiftDiscCy, adx = Mathf.Abs(dx);
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            bool inRing = r <= RiftDiscR && dy >= -RiftDiscChord;

            // ---- the LEDs: which band, and how far along the path
            int band = 0;                      // 1 cyan, 2 purple
            bool onTrail = false;
            float across = 0f;                 // 0..1 across the band, for the menu's bright core
            bool trailZone = adx >= RiftDiscTrailIn && adx < RiftDiscTrailPurpleOut && dy < RiftDiscJoinDy;
            if (trailZone && dy >= -RiftDiscChord)
            {
                onTrail = true;
                band = adx < RiftDiscTrailCyanOut ? 1 : 2;
                across = band == 1 ? (adx - RiftDiscTrailIn) / (RiftDiscTrailCyanOut - RiftDiscTrailIn)
                                   : (adx - RiftDiscTrailCyanOut) / (RiftDiscTrailPurpleOut - RiftDiscTrailCyanOut);
            }
            else if (!trailZone && r >= RiftDiscHollow && r < RiftDiscPurpleOut
                     && !(dy < RiftDiscJoinDy && adx < RiftDiscTrailIn))                // not across the grip
            {
                band = r < RiftDiscCyanOut ? 1 : 2;
                across = band == 1 ? (r - RiftDiscHollow) / (RiftDiscCyanOut - RiftDiscHollow)
                                   : (r - RiftDiscCyanOut) / (RiftDiscPurpleOut - RiftDiscCyanOut);
            }

            if (band != 0)
            {
                float u = RiftDiscPathAt(adx, dy, onTrail);
                float p = Mathf.Repeat(u / RiftDiscPulseSpacing - phase, 1f);
                bool pulse = p > 0.84f;                                           // the head, trailing down

                // the EMITTER: the band's end at the flat top, lit hard - the wall leaves from here
                bool emitter = onTrail && dy < -RiftDiscChord + (menu ? 2.5f * px : 1f);

                // the LED's bright core: ONE texel down the band's middle - a core half the band
                // wide read lavender. Band widths in cells: cyan 1, purple 2.
                float bandCells = band == 1 ? 1f : 2f;
                bool core = menu && Mathf.Abs(across - 0.5f) * bandCells < 0.5f * px + 0.001f;
                bool bright = pulse || core || (emitter && menu);
                if (band == 1) return bright || emitter ? 'E' : 'C';
                return bright || emitter ? 'Q' : 'P';
            }

            if (!inRing) return '.';

            // ---- the wrap: across the top, between the trails, above the hollow
            if (dy < RiftDiscJoinDy && adx < RiftDiscTrailIn && r >= RiftDiscHollow)
            {
                float fromTop = dy + RiftDiscChord;                                     // 0 at the flat top
                if (!menu) return ((int)Mathf.Floor(adx)) % 2 == 0 ? 'B' : 'K';   // |x|: mirror-safe
                if (fromTop < px) return 'L';
                float band4 = Mathf.Repeat(adx / (4f * px), 1f);
                if (band4 < 0.25f) return 'K';                                      // the cord's seams
                return fromTop < 1f ? 'B' : 'D';
            }

            if (r < RiftDiscHollow) return '.';

            // ---- obsidian: glassy black, lit at the rim, a few sharp conchoidal streaks
            var n = new Vector2(dx, -dy) / Mathf.Max(r, 1e-4f);
            float val = Vector2.Dot(n, new Vector2(-0.7071f, 0.7071f));
            float toRim = RiftDiscR - r, toTop = dy + RiftDiscChord;
            if (menu)
            {
                // the LEDs' light spilling one texel onto the stone beside them
                if (r - RiftDiscPurpleOut < px && !(dy < RiftDiscJoinDy && adx < RiftDiscTrailPurpleOut + px)) return 'G';
                if (dy < RiftDiscJoinDy && adx >= RiftDiscTrailPurpleOut && adx < RiftDiscTrailPurpleOut + px) return 'G';
                if ((toRim < px || toTop < px) && val > -0.2f) return 'h';
                // conchoidal flakes: curved bright streaks on the lit side
                float streak = Mathf.Repeat((r * 0.9f + dx * 0.35f) / 1.5f, 1f);
                if (val > 0.1f && streak < 0.1f) return val > 0.5f ? 'h' : 'l';
                return val > 0.45f ? 'b' : val > -0.2f ? 'd' : 's';
            }
            if ((toRim < 0.6f || toTop < 0.6f) && val > 0.3f) return 'l';
            return val > 0.2f ? 'b' : val > -0.4f ? 'd' : 's';
        }

        static string[] BuildRiftDiscAt(int scale, float phase = 0f)
        {
            int w = RiftDiscW * scale, h = RiftDiscH * scale;
            var rows = new string[h];
            var sb = new System.Text.StringBuilder(w);
            for (int row = 0; row < h; row++)
            {
                sb.Clear();
                for (int col = 0; col < w; col++)
                    sb.Append(RiftDiscTexel((col + 0.5f) / scale, (row + 0.5f) / scale, scale, phase));
                rows[row] = sb.ToString();
            }
            return scale == 1 ? rows : RoseDropIslands(rows);
        }

        static string[] _riftDiscRows, _riftDiscDetail;
        static string[] RiftDiscRows => _riftDiscRows ??= BuildRiftDiscAt(1);
        static string[] RiftDiscDetail => _riftDiscDetail ??= BuildRiftDiscAt(RiftDiscDetailScale);

        /// <summary>
        /// Obsidian (lowercase): a violet-black glass lifted only a little - it reads by its
        /// highlights, not its body. The wrap's leather (uppercase K S D B L H). The LEDs on spare
        /// letters, emissive so placed by hand: purple P and its bright core Q; cyan C and bright E;
        /// and G, the purple light spilling onto stone.
        /// </summary>
        static Dictionary<char, Color> RiftDiscPal()
        {
            var pal = Palette.Of(new Palette.Ramp(new Color(0.17f, 0.15f, 0.23f), lift: 0.45f, shade: 0.35f),
                                 Palette.Leather);
            pal['P'] = new Color(0.64f, 0.30f, 1.00f);
            pal['Q'] = new Color(0.88f, 0.74f, 1.00f);
            pal['C'] = new Color(0.05f, 0.80f, 1.00f);
            pal['E'] = new Color(0.78f, 0.98f, 1.00f);
            pal['G'] = new Color(0.32f, 0.17f, 0.48f);
            return pal;
        }

        /// <summary>The pulses running up the LEDs to the emitters, a loop. Arena only.</summary>
        static Sprite[] RiftDiscFrames()
        {
            var pal = RiftDiscPal();
            var frames = new Sprite[RiftDiscFrameCount];
            for (int i = 0; i < RiftDiscFrameCount; i++)
                frames[i] = StageSprite($"gear.weapon.disc.rift.pulse{i}",
                                        BuildRiftDiscAt(1, i / (float)RiftDiscFrameCount), pal, RiftDiscGrip);
            return frames;
        }

        static GearItem RiftDisc()
        {
            var item = Disc(WithMenu(Make("rift_discs", "Rift Disc", GearSlot.Weapon, LootTier.Diamond, 0f,
                    Pixels(RigLayer.Weapon, "gear.weapon.disc.rift", RiftDiscRows, RiftDiscPal(),
                           DiscHandX, DiscHandY, pivotTexel: RiftDiscGrip, ppu: FinePpu, upscale2x: true)),
                    Pixels(RigLayer.Weapon, "gear.weapon.disc.rift.menu", RiftDiscDetail, RiftDiscPal(),
                           DiscHandX, DiscHandY, pivotTexel: RiftDiscDetailGrip, ppu: MenuPpu)));
            item.ImplodesKills = true;
            item.LightCycleTrail = true;
            item.IdleFrames = RiftDiscFrames();
            item.IdleFrameSeconds = RiftDiscFrameSeconds;
            return item;
        }
    }
}
