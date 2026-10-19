using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the Swan: the Albedo's bird
    //
    // Diamond, power 0 - bespoke art and nothing else (the cosmetic weapons' rule). The second of
    // the FOUR BIRDS (DemoGear.Birds.cs - the shared skeleton), 2026-10-17; the name is mine.
    //
    // A swan flies with its NECK stretched out ahead: here it runs on from the body in a gentle S,
    // down the arrow's line, to a mute swan's head - the ORANGE BILL, the black knob at its base,
    // the black nail at its tip (the last two on the menu art only - in the arena they swallowed
    // the orange), a black mask round the eye.
    //
    // ALBEDO - the whitening: white feathers shaded in cool grey, SMOOTH (no bites, no fraying),
    // their ends ROUNDED, the fingers fewer and closer than the Crow's; a SILVER string. A pale
    // bird draws no edge of its own against the floor, so a dark line is traced round the whole
    // silhouette after sampling (BirdStyle.TraceOutline); outlineless otherwise, as the Crow.
    public static partial class DemoGear
    {
        static BirdStyle _swanStyle;
        static BirdStyle SwanStyle => _swanStyle ??= new BirdStyle
        {
            Canvas = 48,
            Fingers = 4, Coverts = 10,
            FingerSplay = 4f, AngleJitter = 2f, LengthBase = 9.8f, LengthJitter = 0.6f,
            Half = 2.2f, FingerHalf = 1.5f,
            FingerShorten = new[] { 0f, 0f, 0f, 0f, 0.6f },
            RoundTips = true,
            TraceOutline = true,
            Head = SwanHead,
            Body = (x, u, dy, half, fine) =>
            {
                if (fine && u > 0.4f && dy > 0f && Mathf.Abs(dy) < half - 1f
                    && Mathf.Repeat(x * 1.3f + Mathf.Abs(dy) * 1.6f, 2.6f) < 0.3f) return 'b';   // breast chevrons
                if (dy < -half * 0.45f) return 'h';
                return dy > half * 0.35f ? 'b' : 'l';
            },
            Tail = (v, dy, half, fine) =>
            {
                if (fine ? Mathf.Abs(Mathf.Repeat(dy + half, 1.1f) - 0.55f) < 0.13f
                         : v > 0.3f && Mathf.Abs(dy) < 0.5f) return 'b';
                return dy > 0.4f ? 'b' : 'l';
            },
            EdgeLine = 'k', EdgeLit = 'l', EdgeInside = 'b',
            Feather = (q, b, tt, hw, bb, e, fine) =>
            {
                if (bb > hw - (fine ? e * (q.Covert ? 0.8f : 1f) : 0.55f)) return 'd';   // a soft grey where one lies over the next
                if (!fine)
                {
                    if (q.Covert) return bb < 0f ? 'h' : 'l';
                    return q.Index % 2 == 1 ? 'h' : 'l';
                }
                if (Mathf.Abs(b) < 0.22f && tt < 0.85f) return 'b';   // the quill
                if (tt > 0.88f) return 'b';
                return bb < 0f ? 'h' : 'l';
            },
        };

        static void AddSwan(List<GearItem> items)
        {
            const string key = "gear.weapon.swan";
            var pal = SwanPal();
            items.Add(WithMenu(Bow(Make(SwanBowId, "The Swan", GearSlot.Weapon, LootTier.Diamond, 0f,
                    OutlinelessWeapon(key, BirdSample(SwanStyle, 1), pal, BirdGrip, 0f))),
                OutlinelessMenuWeapon(key + ".menu", BirdSample(SwanStyle, EmberDetailScale), pal, BirdDetailGrip, 0f)));
        }

        /// <summary>
        /// The feathers (lowercase - white, h to k: glow, light, the quill and shade, the overlap's
        /// grey, deep, the traced line); the bill (uppercase - K black, H lit orange, B orange, D under
        /// the gape); the string (3, silver).
        /// </summary>
        static Dictionary<char, Color> SwanPal()
        {
            static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
            var feather = Palette.Ramp.FromTones(glow: C(250, 250, 246), light: C(222, 226, 232), b: C(184, 192, 206),
                                                 dark: C(138, 148, 168), deep: C(96, 104, 126), line: C(36, 40, 56));
            var bill = Palette.Ramp.FromTones(glow: C(255, 170, 90), light: C(255, 170, 90), b: C(236, 120, 40),
                                              dark: C(176, 72, 24), deep: C(176, 72, 24), line: C(24, 20, 22));
            var silver = Palette.Ramp.FromTones(glow: C(240, 244, 250), light: C(228, 234, 242), b: C(214, 220, 232),
                                                dark: C(206, 214, 228), deep: C(170, 178, 196), line: C(110, 118, 140));
            return Palette.Of(feather, bill, silver);
        }

        /// <summary>The neck stretched ahead in a gentle S, the head, the orange bill.</summary>
        static char SwanHead(float x, float y, float e, bool fine)
        {
            if (x >= 21f && x <= 36.5f)
            {
                float u = (x - 21f) / 15.5f;
                float cy = 40.6f - 1.6f * u + 0.42f * Mathf.Sin(u * Mathf.PI * 2f);
                float half = 2.6f - 1.3f * Mathf.Pow(u, 0.6f);
                float dy = y - cy;
                if (Mathf.Abs(dy) < half)
                {
                    if (half - Mathf.Abs(dy) < e * 0.95f) return 'k';
                    if (dy < -half * 0.3f) return 'h';
                    return dy > half * 0.35f ? 'b' : 'l';
                }
            }
            float bx = x - 38.8f;
            if (bx >= -0.6f && bx <= 6.4f)
            {
                float top = 37f + 0.22f * bx + 0.015f * bx * bx;
                float bot = 40.6f - 0.10f * bx - 0.035f * bx * bx;
                if (y >= top && y <= bot)
                {
                    if (y > bot - e * 0.8f || y < top + e * 0.8f || bx > 6.4f - e * 0.8f) return 'K';
                    if (bx < (fine ? 1.4f : 0.9f) && y < top + (fine ? 1.8f : 1.2f)) return 'K';   // the knob
                    if (fine && bx > 5.4f) return 'K';                                             // the nail
                    if (fine && Mathf.Abs(y - (39.5f - 0.05f * bx)) < 0.22f && bx > 1.4f) return 'D';   // the gape
                    return y < top + 1f ? 'H' : y < 39.6f ? 'B' : 'D';
                }
            }
            float hd = BirdEllipse(x, y, 36.9f, 38.8f, 3.2f, 2.6f);
            if (hd < 1f)
            {
                if (Mathf.Sqrt(hd) > 1f - e / 2.4f) return 'k';
                if (BirdEllipse(x, y, 38f, 38.7f, 1.2f, 0.85f) < 1f) return 'k';   // the black mask round the eye
                return y < 38.6f ? 'h' : 'l';
            }
            return '.';
        }
    }
}
