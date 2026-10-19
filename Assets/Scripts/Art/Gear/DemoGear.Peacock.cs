using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the Peacock: the Cauda Pavonis' bird
    //
    // Diamond, power 0 - bespoke art and nothing else (the cosmetic weapons' rule). The third of
    // the FOUR BIRDS (DemoGear.Birds.cs - the shared skeleton), 2026-10-17; the name is mine.
    //
    // THE WINGS ARE THE TRAIN: every flight feather ends in an EYE (an ocellus - a dark blue heart,
    // a turquoise ring, bronze, then gold), so the limbs read as the peacock's fanned train. Over
    // their roots the coverts are the green scale feathers of its back. The body and neck are
    // cobalt; the head carries a short bill, a white stripe across the face and a CREST fanned up
    // from its crown, each stalk ending in a tuft.
    //
    // CAUDA PAVONIS - the peacock's tail, the stage where every colour shows: blue, green, bronze and
    // gold together; a GOLD string. A dark line is traced round the silhouette (the Swan's way).
    public static partial class DemoGear
    {
        static BirdStyle _peacockStyle;
        static BirdStyle PeacockStyle => _peacockStyle ??= new BirdStyle
        {
            Canvas = 38,
            Fingers = 4, Coverts = 10,
            FingerSplay = 4f, AngleJitter = 2f, LengthBase = 10f, LengthJitter = 0.6f,
            Half = 2.2f, FingerHalf = 1.6f,
            FingerShorten = new[] { 0f, 0f, 0f, 0f, 0.6f },
            RoundTips = true,
            TraceOutline = true,
            Head = PeacockHead,
            Body = (x, u, dy, half, fine) =>
            {
                if (dy < -half * 0.45f) return 'L';
                if (fine && Mathf.Repeat(x * 1.4f + dy * 0.9f, 2.4f) < 0.35f) return 'D';   // a sheen of scales
                return dy > half * 0.35f ? 'D' : 'B';
            },
            Tail = (v, dy, half, fine) =>
            {
                if (fine ? Mathf.Abs(Mathf.Repeat(dy + half, 1.1f) - 0.55f) < 0.13f
                         : v > 0.3f && Mathf.Abs(dy) < 0.5f) return 'd';
                return dy > 0.4f ? 'd' : 'b';
            },
            EdgeLine = 'k', EdgeLit = 'L', EdgeInside = 'B',
            Feather = PeacockFeather,
            OverFeathers = PeacockEyes,
            StringChar = 'g',   // its own letter: a digit would be read as string by the trace
        };

        static void AddPeacock(List<GearItem> items)
        {
            const string key = "gear.weapon.peacock";
            var pal = PeacockPal();
            items.Add(WithMenu(Bow(Make(PeacockBowId, "The Peacock", GearSlot.Weapon, LootTier.Diamond, 0f,
                    OutlinelessWeapon(key, BirdSample(PeacockStyle, 1), pal, BirdGrip, 0f))),
                OutlinelessMenuWeapon(key + ".menu", BirdSample(PeacockStyle, EmberDetailScale), pal, BirdDetailGrip, 0f)));
        }

        /// <summary>
        /// Green (lowercase - the train's barbs and the scale coverts, k the traced line); cobalt
        /// (uppercase - body, neck, the eyes' hearts and turquoise rings); bronze and gold (digits -
        /// 1 its line, 2 deep bronze, 3 bronze, 4 gold, 5 light gold - the string, 6 the brightest).
        /// The face's white stripe is 'w'; the GOLD string 'g'.
        /// </summary>
        static Dictionary<char, Color> PeacockPal()
        {
            static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
            var green = Palette.Ramp.FromTones(glow: C(150, 214, 160), light: C(90, 170, 130), b: C(40, 120, 96),
                                               dark: C(26, 84, 72), deep: C(16, 52, 48), line: C(10, 20, 24));
            var cobalt = Palette.Ramp.FromTones(glow: C(160, 210, 250), light: C(80, 150, 230), b: C(40, 90, 190),
                                                dark: C(26, 52, 140), deep: C(16, 30, 90), line: C(10, 14, 40));
            var gold = Palette.Ramp.FromTones(glow: C(255, 232, 150), light: C(236, 196, 90), b: C(200, 150, 50),
                                              dark: C(150, 96, 34), deep: C(110, 66, 24), line: C(40, 24, 10));
            var pal = Palette.Of(green, cobalt, gold);
            pal['w'] = C(236, 240, 236);
            pal['g'] = C(236, 196, 90);   // the string
            return pal;
        }

        /// <summary>The EYES, one at the end of every flight feather and OVER all of them - drawn on
        /// the feather itself, each was covered by the next one but its edge.</summary>
        static char PeacockEyes(BirdStyle st, float x, float y, bool fine)
        {
            for (int w = 0; w < 2; w++)
                foreach (var q in st.Wings[w])
                {
                    if (q.Covert) continue;
                    var c = q.Root + q.Dir * (q.Length * 0.82f);
                    float px = x - c.x, py = y - c.y;
                    float ea = (px * q.Dir.x + py * q.Dir.y) / 1.55f;
                    float eb = (-px * q.Dir.y + py * q.Dir.x) / 1.2f;
                    float r = Mathf.Sqrt(ea * ea + eb * eb);
                    if (r >= 1f) continue;
                    if (fine)
                    {
                        if (r < 0.32f) return 'S';   // the dark blue heart
                        if (r < 0.52f) return 'L';   // turquoise
                        if (r < 0.7f) return '3';    // bronze
                        if (r < 0.86f) return '5';   // gold
                        return 'd';                  // its dark rim
                    }
                    if (r < 0.45f) return 'S';
                    return r < 0.8f ? '5' : 'd';
                }
            return '.';
        }

        /// <summary>A train feather: green barbs and a gold quill (its eye is PeacockEyes).</summary>
        static char PeacockFeather(BirdQuill q, float b, float tt, float hw, float bb, float e, bool fine)
        {
            if (q.Covert)
            {
                // The back's scale feathers: green, lit at the root, dark where one lies over the next.
                if (bb > hw - (fine ? e * 0.8f : 0.55f)) return 's';
                return bb < 0f ? 'h' : 'b';
            }
            if (bb > hw - (fine ? e : 0.55f)) return 'd';
            if (fine && Mathf.Abs(b) < 0.2f && tt < 0.7f) return '4';   // the gold quill
            if (!fine) return q.Index % 2 == 1 ? 'b' : 'l';
            // Loose barbs: streaks across the vane.
            return Mathf.Repeat(tt * q.Length * 1.6f + Mathf.Abs(b) * 0.8f, 1.2f) < 0.4f ? 'd' : bb < 0f ? 'l' : 'b';
        }

        /// <summary>The neck, the head with its white face stripe and short bill, and the crest.</summary>
        static char PeacockHead(float x, float y, float e, bool fine)
        {
            // ---- the crest: four stalks fanned up off the crown, a tuft on each ----
            for (int i = 0; i < 4; i++)
            {
                float ang = (98f - i * 16f) * Mathf.Deg2Rad;
                var root = new Vector2(29.6f, 36.6f);
                var dir = new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang));
                var tip = root + dir * 4.2f;
                float tdx = x - tip.x, tdy = y - tip.y;
                if (tdx * tdx + tdy * tdy < (fine ? 0.75f : 1.1f))
                    return fine && tdx * tdx + tdy * tdy >= 0.25f ? 'D' : 'L';
                float px = x - root.x, py = y - root.y;
                float a = px * dir.x + py * dir.y, c = Mathf.Abs(-px * dir.y + py * dir.x);
                if (a > 0f && a < 4.2f && c < (fine ? 0.2f : 0.45f)) return 'D';
            }
            // ---- the bill ----
            float bx = x - 31.6f;
            if (bx >= -0.4f && bx <= 3.2f)
            {
                float top = 37.9f + 0.30f * bx, bot = 39.7f - 0.22f * bx;
                if (y >= top && y <= bot)
                {
                    if (y > bot - e * 0.8f || y < top + e * 0.8f || bx > 3.2f - e * 0.8f) return '1';
                    return y < top + 0.8f ? '5' : '3';
                }
            }
            // ---- the head ----
            float hd = BirdEllipse(x, y, 29.7f, 38.7f, 2.8f, 2.4f);
            if (hd < 1f)
            {
                if (Mathf.Sqrt(hd) > 1f - e / 2.2f) return 'K';
                if (BirdEllipse(x, y, 30.7f, 38.3f, fine ? 0.7f : 0.6f, fine ? 0.6f : 0.5f) < 1f) return 'K';                     // the eye
                if (Mathf.Abs(y - (38.4f - 0.15f * (x - 30.6f))) < (fine ? 0.35f : 0.5f) && x > 28.2f) return 'w';   // the face stripe
                return y < 38f ? 'L' : 'B';
            }
            // ---- the neck: cobalt, shorter than the swan's ----
            if (x >= 21f && x <= 28.5f)
            {
                float u = (x - 21f) / 7.5f;
                float cy = 40.4f - 1.4f * u;
                float half = 2.7f - 1.1f * u;
                float dy = y - cy;
                if (Mathf.Abs(dy) < half)
                {
                    if (half - Mathf.Abs(dy) < e * 0.95f) return 'K';
                    if (dy < -half * 0.3f) return 'L';
                    return dy > half * 0.35f ? 'D' : 'B';
                }
            }
            return '.';
        }
    }
}
