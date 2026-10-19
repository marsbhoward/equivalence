using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the Crow: the Nigredo's bird, stooping
    //
    // Diamond, power 0 - bespoke art and nothing else (the cosmetic weapons' rule). The first of
    // the FOUR BIRDS (DemoGear.Birds.cs - the shared skeleton: body as the riser, the head down the
    // arrow's line, the wings swept back as the limbs). Prototype, 2026-10-17; the name is mine.
    //
    // A crow's HEAVY BILL, the culmen straight off the forehead and hooked, points at the target.
    //
    // NIGREDO - the blackening: soot-black feathers with a green gloss, ragged (a bite out of
    // some vanes), the frayed ends gone to ASH, a few still holding an ember; the eye an ember, and
    // the STRING the eye's ember (the user's call).
    //
    // OUTLINELESS (Umbra's route - an auto-outline would bury the fingers and the gaps between the
    // feathers); everything draws its own dark line.
    public static partial class DemoGear
    {
        static BirdStyle _crowStyle;
        static BirdStyle CrowStyle => _crowStyle ??= new BirdStyle
        {
            Canvas = 38,
            Fingers = 6, Coverts = 9,
            FingerSplay = 7f, AngleJitter = 6f, LengthBase = 9.5f, LengthJitter = 1.6f,
            Half = 2.1f, FingerHalf = 1.05f,
            FingerShorten = new[] { 0f, 0f, 0f, 0f, 0.6f, 1.6f, 3f },
            Ragged = true,
            Head = CrowHead,
            Body = (x, u, dy, half, fine) =>
            {
                if (fine && u > 0.5f && dy > 0f && Mathf.Abs(dy) < half - 1f
                    && Mathf.Repeat(x * 1.3f + Mathf.Abs(dy) * 1.6f, 2.6f) < 0.35f) return 's';   // breast chevrons
                if (dy < -half * 0.45f) return u > 0.35f ? 'h' : 'b';
                return dy > half * 0.35f ? 'd' : 'b';
            },
            Tail = (v, dy, half, fine) =>
            {
                if (fine ? Mathf.Abs(Mathf.Repeat(dy + half, 1.1f) - 0.55f) < 0.13f
                         : v > 0.3f && Mathf.Abs(dy) < 0.5f) return 's';
                return dy > 0.4f ? 'd' : Mathf.Abs(dy) < 0.3f && !fine ? 'L' : 'b';
            },
            EdgeLine = 'k', EdgeLit = 'l', EdgeInside = 'b',
            Feather = (q, b, tt, hw, bb, e, fine) =>
            {
                if (bb > hw - (fine ? e * (q.Covert ? 0.8f : 1f) : 0.55f)) return 'k';
                bool frayed = !q.Covert && tt > 0.8f && q.Fray > 0.45f;
                if (!fine)
                {
                    if (frayed) return 'S';
                    if (q.Covert) return bb < 0f ? 'b' : 'd';
                    if (bb < -0.3f && tt < 0.75f) return 'h';
                    return q.Index % 2 == 1 ? 'b' : 'd';
                }
                if (frayed) return q.Fray > 0.85f && tt > 0.93f ? '1' : 'S';   // ash, a few still burning
                if (Mathf.Abs(b) < 0.22f && tt < 0.85f) return q.Covert ? 'b' : 'L';
                if (bb < 0f)
                {
                    if (!q.Covert && tt > 0.12f && tt < 0.7f && -bb < 0.72f) return 'h';
                    return 'd';
                }
                return 's';
            },
        };

        static void AddCrow(List<GearItem> items)
        {
            const string key = "gear.weapon.crow";
            var pal = CrowPal();
            items.Add(WithMenu(Bow(Make(CrowBowId, "The Crow", GearSlot.Weapon, LootTier.Diamond, 0f,
                    OutlinelessWeapon(key, BirdSample(CrowStyle, 1), pal, BirdGrip, 0f))),
                OutlinelessMenuWeapon(key + ".menu", BirdSample(CrowStyle, EmberDetailScale), pal, BirdDetailGrip, 0f)));
        }

        /// <summary>
        /// The feathers (lowercase - k line, s deep, d dark vane, b vane, h the green gloss, l the
        /// leading edge's lit line); the bill, the quills and the ash (uppercase - K line, D under the
        /// gape, B bill, H the bill's lit ridge, L quill, S ash); the ember and the string (digits -
        /// 1 ember, 2 its hot core, 3 the string - the eye's ember, by the user's call).
        /// </summary>
        static Dictionary<char, Color> CrowPal()
        {
            static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
            var feather = Palette.Ramp.FromTones(glow: C(84, 132, 122), light: C(104, 128, 132), b: C(58, 63, 74),
                                                 dark: C(38, 41, 50), deep: C(22, 24, 30), line: C(8, 8, 11));
            var bill = Palette.Ramp.FromTones(glow: C(110, 108, 104), light: C(96, 100, 104), b: C(74, 72, 70),
                                              dark: C(46, 44, 44), deep: C(138, 130, 120), line: C(14, 12, 12));
            var ember = Palette.Ramp.FromTones(glow: C(255, 196, 110), light: C(255, 196, 110), b: C(214, 72, 28),
                                               dark: C(214, 72, 28), deep: C(255, 196, 110), line: C(214, 72, 28));
            return Palette.Of(feather, bill, ember);
        }

        /// <summary>The crow's head: the heavy bill toward the target, the ember eye.</summary>
        static char CrowHead(float x, float y, float e, bool fine)
        {
            float bx = x - 27.4f;
            if (bx >= -0.4f && bx <= 7.4f)
            {
                float top = 37.6f + 0.10f * bx + 0.028f * bx * bx;
                float bot = 42.2f - 0.36f * bx + 0.010f * bx * bx;
                if (y >= top && y <= bot)
                {
                    if (y > bot - e || y < top + e || bx > 7.4f - e) return 'K';
                    if (Mathf.Abs(y - (40.15f + 0.05f * bx)) < (fine ? 0.22f : 0.42f) && bx > 0.6f && bx < 5.2f) return 'K';
                    return y < top + 1.1f ? 'H' : y < 40.1f ? 'B' : 'D';
                }
            }
            float hd = BirdEllipse(x, y, 24.4f, 39.7f, 4.3f, 3.9f);
            if (hd < 1f)
            {
                if (Mathf.Sqrt(hd) > 1f - e / 3.6f) return 'k';
                float ed = BirdEllipse(x, y, 25.7f, 38.7f, 1.05f, 1f);
                if (ed < 1f) return fine && ed < 0.3f ? '2' : '1';
                if (ed < 1.7f) return 'k';
                return y < 39.6f && x > 23f ? 'h' : y < 41f ? 'b' : 'd';
            }
            return '.';
        }
    }
}
