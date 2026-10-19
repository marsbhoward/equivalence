using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the Phoenix: the Rubedo, the three birds in one
    //
    // Black Diamond, power 0 - the cosmetic half (its relic, the GOLDEN CAGE, is below). The fourth of
    // the FOUR BIRDS (DemoGear.Birds.cs - the shared skeleton), forged from the other three; built
    // 2026-10-17 from the user's brief: PIECES OF THE OTHER THREE, FLAMING TAIL FEATHERS, TWO OF WHICH
    // ARE THE STRING, and the relic a golden cage. Names mine.
    //
    // What it takes from each:
    //   - the SWAN's long neck, stretched ahead in its S;
    //   - the CROW's heavy hooked bill (cast in gold) and its ember eye (burning white);
    //   - the PEACOCK's crest (its tufts flames) and its EYES, on the middle feathers, in fire, and its
    //     turquoise on the top of every red feather;
    //   - the CROW's black - the wings' FRONT ARCS (leading edges and the scales along them), soot
    //     with its green gloss (the user's calls; a smouldering smoke loop was tried and removed).
    // Down each wing: the inner feathers the swan's (pale gold), the middle the peacock's (an eye at
    // each end), the wingtip's fingers the crow's ragged shape, BURNING at the ends.
    //
    // THE TAIL IS FLAME: many wispy feathers (a fine quill, barbs sweeping back) fan back over the
    // hand, and two of the same feathers, the same size, run up and down the string from the tail -
    // their quills ARE the string, which runs on plain to the nocks; the arrow nocks at the tail.
    //
    // RUBEDO - the reddening, the Work complete: crimson, orange and gold.
    public static partial class DemoGear
    {
        public const string PhoenixBowId = "phoenix_bow", PhoenixCageId = "phoenix_cage", PhoenixArtId = "phoenix_art";
        public const string CrowBowId = "crow_bow", SwanBowId = "swan_bow", PeacockBowId = "peacock_bow";

        /// <summary>Where the tail's flames - and the two string feathers - leave the body.</summary>
        static readonly Vector2 PhoenixTailAt = new(10.2f, 41f);
        const int PhoenixSwanFeathers = 3, PhoenixPeacockFeathers = 6;   // the rest are the crow's

        static BirdStyle _phoenixStyle;
        static BirdStyle PhoenixStyle => _phoenixStyle ??= new BirdStyle
        {
            Canvas = 44,
            Fingers = 5, Coverts = 10,
            FingerSplay = 6f, AngleJitter = 5f, LengthBase = 10f, LengthJitter = 1.4f,
            Half = 2.2f, FingerHalf = 1.2f,
            FingerShorten = new[] { 0f, 0f, 0f, 0.4f, 1.2f, 2.4f },
            Ragged = true,
            TraceOutline = true,
            Untraced = "3456wgt",
            Head = PhoenixHead,
            Body = (x, u, dy, half, fine) =>
            {
                if (dy < -half * 0.45f) return u > 0.4f ? 'L' : 'h';
                if (fine && Mathf.Repeat(x * 1.3f + Mathf.Abs(dy) * 1.6f, 2.6f) < 0.35f && dy > 0f) return 'd';
                return dy > half * 0.35f ? 'd' : 'b';
            },
            Tail = (v, dy, half, fine) => 'b',   // unused - the flaming tail replaces the fan
            EdgeLine = 'k', EdgeLit = 'v', EdgeInside = 'n',   // the crow's black front arc
            Feather = PhoenixFeather,
            OverFeathers = PhoenixEyes,
            StringChar = 'g',
            StringAndTail = PhoenixTail,
        };

        /// <summary>How far down a red feather its peacock-coloured top runs.</summary>
        const float PhoenixBlueTo = 0.42f;
        const int PhoenixPlumes = 8;   // the tail's wispy feathers
        /// <summary>How long the two string feathers are - the tail's others' size - before the plain string.</summary>
        const float PhoenixStringFeather = 13f;

        static void AddPhoenix(List<GearItem> items)
        {
            const string key = "gear.weapon.phoenix";
            var pal = PhoenixPal();
            var item = WithMenu(Bow(Make(PhoenixBowId, "The Phoenix", GearSlot.Weapon, LootTier.BlackDiamond, 0f,
                    OutlinelessWeapon(key, BirdSample(PhoenixStyle, 1), pal, BirdGrip, 0f)), PhoenixArtId),
                OutlinelessMenuWeapon(key + ".menu", BirdSample(PhoenixStyle, EmberDetailScale), pal, BirdDetailGrip, 0f));
            item.ForgeOnly = true;   // forged from the three birds (GearForge.Fusions), never dropped
            items.Add(item);
            AddPhoenixCage(items);
        }

        /// <summary>
        /// Fire (lowercase - k line, s deep crimson, d crimson, b red, l orange, h gold-orange); gold
        /// (uppercase - the bill, the crest, the leading edges, the swan's feathers); flame (digits -
        /// 1 its line, 2 deep red, 3 red-orange, 4 orange, 5 yellow, 6 white-hot); w white-hot, g the
        /// string feathers' white-gold core.
        /// </summary>
        static Dictionary<char, Color> PhoenixPal()
        {
            static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
            var fire = Palette.Ramp.FromTones(glow: C(255, 180, 70), light: C(240, 110, 40), b: C(200, 52, 30),
                                              dark: C(140, 24, 28), deep: C(84, 14, 20), line: C(30, 6, 10));
            var gold = Palette.Ramp.FromTones(glow: C(255, 240, 180), light: C(250, 210, 100), b: C(222, 160, 52),
                                              dark: C(170, 104, 30), deep: C(110, 60, 18), line: C(60, 30, 8));
            var flame = Palette.Ramp.FromTones(glow: C(255, 248, 214), light: C(255, 214, 90), b: C(255, 140, 40),
                                               dark: C(226, 70, 24), deep: C(150, 30, 14), line: C(60, 10, 6));
            var pal = Palette.Of(fire, gold, flame);
            pal['w'] = C(255, 250, 236);
            pal['g'] = C(255, 236, 170);
            // The peacock's: c cobalt (an eye's heart), t turquoise (its ring, the crest's flames),
            // q teal (the scales' sheen).
            pal['c'] = C(22, 40, 120); pal['t'] = C(70, 170, 220); pal['q'] = C(40, 140, 120);
            // The crow's: n soot black, m its lit black, v the green gloss.
            pal['n'] = C(16, 16, 20); pal['m'] = C(40, 42, 50); pal['v'] = C(70, 112, 104);
            return pal;
        }

        static bool PhoenixIsSwan(in BirdQuill q) => !q.Covert && q.Index < PhoenixSwanFeathers;
        static bool PhoenixIsPeacock(in BirdQuill q)
            => !q.Covert && q.Index >= PhoenixSwanFeathers && q.Index < PhoenixSwanFeathers + PhoenixPeacockFeathers;

        static char PhoenixFeather(BirdQuill q, float b, float tt, float hw, float bb, float e, bool fine)
        {
            if (q.Covert)
            {
                // The FRONT ARC is the crow's (the user's call): soot-black scales with its green gloss.
                if (bb > hw - (fine ? e * 0.8f : 0.55f)) return 'k';
                if (!fine) return bb < 0f ? 'm' : 'n';
                if (bb < 0f && tt > 0.2f && tt < 0.7f) return 'v';
                return bb < 0f ? 'm' : 'n';
            }
            if (PhoenixIsSwan(q))                                                // the swan's: pale gold, smooth
            {
                if (bb > hw - (fine ? e : 0.55f)) return 'D';
                return fine && Mathf.Abs(b) < 0.22f && tt < 0.85f ? 'B' : bb < 0f ? 'H' : 'L';
            }
            if (bb > hw - (fine ? e : 0.55f)) return 's';
            bool crow = !PhoenixIsPeacock(q);
            if (crow && tt > 0.76f)                                              // the crow's ends - burning
                return tt > 0.9f ? '5' : q.Fray > 0.5f ? '4' : '3';
            // The PEACOCK's colour at the top of each feather, under the black arc, stopping SOLID
            // where the red starts - plumage, not a fade (the user's calls): turquoise on the lit side,
            // teal on the other.
            if (tt < PhoenixBlueTo)
                return bb < 0f ? 't' : 'q';
            if (!fine) return q.Index % 2 == 1 ? 'l' : 'b';
            if (Mathf.Abs(b) < 0.2f && tt < 0.8f) return 'L';                   // a gold quill
            return bb < 0f ? 'l' : crow ? 'd' : 'b';
        }

        /// <summary>The peacock's eyes on the middle feathers, in fire: a crimson heart, orange, gold
        /// (the user's call - they stay the Phoenix's colours).</summary>
        static char PhoenixEyes(BirdStyle st, float x, float y, bool fine)
        {
            for (int w = 0; w < 2; w++)
                foreach (var q in st.Wings[w])
                {
                    if (!PhoenixIsPeacock(q)) continue;
                    var c = q.Root + q.Dir * (q.Length * 0.82f);
                    float px = x - c.x, py = y - c.y;
                    float ea = (px * q.Dir.x + py * q.Dir.y) / 1.55f;
                    float eb = (-px * q.Dir.y + py * q.Dir.x) / 1.2f;
                    float r = Mathf.Sqrt(ea * ea + eb * eb);
                    if (r >= 1f) continue;
                    if (fine)
                    {
                        if (r < 0.32f) return 's';
                        if (r < 0.52f) return '4';
                        if (r < 0.7f) return 'B';
                        if (r < 0.86f) return 'L';
                        return 'd';
                    }
                    if (r < 0.45f) return 's';
                    return r < 0.8f ? 'L' : 'd';
                }
            return '.';
        }

        /// <summary>
        /// The flaming tail (the user's calls): MANY WISPY feathers fanned back over the hand - a fine
        /// quill, barbs sweeping back off it toward the tip, white-hot at the root to red at the end -
        /// and two of the same feathers running up and down to the nocks ARE the string.
        /// </summary>
        static char PhoenixTail(BirdStyle st, float x, float y, float e, bool fine)
        {
            var t0 = PhoenixTailAt;
            // ---- the two string feathers: the quill is the string, barbs either side ----
            for (int i = 0; i < 2; i++)
            {
                var nock = i == 0 ? st.NockTop : st.NockBottom;
                if (i == 0 ? (y < nock.y || y > t0.y) : (y > nock.y || y < t0.y)) continue;
                float t = (y - t0.y) / (nock.y - t0.y);                         // 0 at the tail, 1 at the nock
                float sx = nock.x;   // straight down the nocks' line - a bow or a lean in it showed as a step
                float dx = x - sx;
                if (Mathf.Abs(dx) < (fine ? 0.25f : 0.5f)) return 'g';
                // The feather is only as long as the tail's others (the user's call); past its
                // tip the plain string runs on to the nock.
                float along = Mathf.Abs(y - t0.y);
                if (along >= PhoenixStringFeather) continue;
                float u = along / PhoenixStringFeather;
                float reach = 1.3f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.1f))), 0.7f) + 0.05f;
                char barb = PhoenixBarb(along, Mathf.Abs(dx), reach, u, fine, 1f);
                if (barb != '.') return barb;
            }
            // ---- the plumes: wispy feathers fanned back over the hand ----
            for (int i = 0; i < PhoenixPlumes; i++)
            {
                float f = i / (float)(PhoenixPlumes - 1);                        // 0 up-back .. 1 down-back
                float ang = Mathf.Lerp(104f, 256f, f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang));
                var side = new Vector2(-dir.y, dir.x);
                float length = 9f + 6f * Mathf.Abs(f - 0.5f) * 2f + (BirdHash(i, 11) - 0.5f) * 2f;   // the outer ones longest
                float curl = (f - 0.5f) * 7f;                                    // the outer ones curl away
                float px = x - t0.x, py = y - t0.y;
                float a = px * dir.x + py * dir.y, c = px * side.x + py * side.y;
                if (a <= 1f || a >= length) continue;   // clear of the root, so each feather stands apart
                float u = a / length;
                c -= curl * u * u + 0.35f * Mathf.Sin(u * Mathf.PI * 2f + i);   // the curl, and a sway
                if (Mathf.Abs(c) < (fine ? 0.2f : 0.45f)) return u < 0.35f ? 'w' : u < 0.7f ? '5' : '4';   // the quill
                bool warm = i % 2 == 1;   // alternate feathers a shade redder, so neighbours separate
                float half = 1.3f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.1f))), 0.7f) + 0.05f;   // Max: sin(pi) is a hair below 0, and its power NaN
                char barb = PhoenixBarb(a, Mathf.Abs(c), half, warm ? Mathf.Min(1f, u + 0.3f) : u, fine, 1f);
                if (barb != '.') return barb;
            }
            return '.';
        }

        /// <summary>One wispy feather's barbs: strands leaving the quill and sweeping back toward
        /// its tip, <paramref name="spacing"/> apart, within <paramref name="half"/> of the quill;
        /// coloured by <paramref name="u"/> along the feather (white-hot root to red end). '.' between.</summary>
        static char PhoenixBarb(float along, float off, float half, float u, bool fine, float spacing)
        {
            if (!(off < half)) return '.';   // also refuses a NaN width
            // Each strand: along - off x slope is constant on it (it leans back toward the tip).
            float phase = Mathf.Repeat(along - off * 0.9f, spacing) / spacing;
            if (phase > (fine ? 0.5f : 0.7f)) return '.';
            float edge = off / Mathf.Max(0.01f, half);
            if (u < 0.3f) return edge < 0.5f ? '6' : '5';
            if (u < 0.6f) return edge < 0.5f ? '5' : '4';
            return edge < 0.5f ? '4' : '3';
        }

        /// <summary>The swan's neck, the crow's gold bill and burning eye, the peacock's crest in flame.</summary>
        static char PhoenixHead(float x, float y, float e, bool fine)
        {
            // ---- the crest: four stalks off the crown, each ending in a flame ----
            var root = new Vector2(33.8f, 36.4f);
            for (int i = 0; i < 4; i++)
            {
                float ang = (100f - i * 17f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang));
                var tip = root + dir * 4.4f;
                float tdx = x - tip.x, tdy = y - tip.y + 0.5f;                   // flames lean up
                float fr = tdx * tdx / 0.8f + tdy * tdy / 1.8f;
                if (fr < 1f) return fr < 0.35f ? '6' : '5';
                float px = x - root.x, py = y - root.y;
                float a = px * dir.x + py * dir.y, c = Mathf.Abs(-px * dir.y + py * dir.x);
                if (a > 0f && a < 4f && c < (fine ? 0.2f : 0.45f)) return 'B';
            }
            // ---- the crow's bill, in gold ----
            float bx = x - 36.4f;
            if (bx >= -0.4f && bx <= 5.6f)
            {
                float top = 37.0f + 0.10f * bx + 0.034f * bx * bx;
                float bot = 40.6f - 0.36f * bx + 0.012f * bx * bx;
                if (y >= top && y <= bot)
                {
                    if (y > bot - e * 0.8f || y < top + e * 0.8f || bx > 5.6f - e * 0.8f) return 'K';
                    if (fine && Mathf.Abs(y - (39.0f + 0.05f * bx)) < 0.22f && bx > 0.6f && bx < 4f) return 'K';   // the gape
                    return y < top + 1f ? 'H' : y < 39f ? 'L' : 'B';
                }
            }
            // ---- the head, with the crow's eye burning white ----
            float hd = BirdEllipse(x, y, 34f, 38.6f, 3.2f, 2.8f);
            if (hd < 1f)
            {
                if (Mathf.Sqrt(hd) > 1f - e / 2.8f) return 'k';
                float ed = BirdEllipse(x, y, 35f, 38f, 1f, 0.9f);
                if (ed < 1f) return fine && ed < 0.35f ? 'w' : '5';
                if (ed < 1.6f) return 'k';
                return y < 38.4f ? 'h' : 'b';
            }
            // ---- the swan's neck, its S shortened ----
            if (x >= 21f && x <= 33.5f)
            {
                float u = (x - 21f) / 12.5f;
                float cy = 40.6f - 1.8f * u + 0.4f * Mathf.Sin(u * Mathf.PI * 2f);
                float half = 2.6f - 1.2f * Mathf.Pow(u, 0.6f);
                float dy = y - cy;
                if (Mathf.Abs(dy) < half)
                {
                    if (half - Mathf.Abs(dy) < e * 0.95f) return 'k';
                    if (dy < -half * 0.3f) return 'h';
                    return dy > half * 0.35f ? 'd' : 'b';
                }
            }
            return '.';
        }

        // ------------------------------------------------------------------ the relic
        //
        // The GOLDEN CAGE (the user's call): a domed birdcage on a ring, four bars, and inside it
        // the phoenix's flame. Hung from the belt like every relic (HipRelic), at the body's density.
        // It carries the Phoenix's art, THE FOUR BIRDS (PlayerController.FormationStrike).

        // 9 x 13 (HipRelic pads it even): l lit gold, b gold, d shade; F flame, w its white-hot core,
        // e embers - the phoenix's flame seen between the bars of a domed birdcage on a ring.
        static readonly string[] PhoenixCageRows =
        {
            "....l....",
            "...l.d...",
            "....b....",
            "..lbbbd..",
            ".lbbbbbd.",
            "lbbbbbbbd",
            "l.b.b.b.d",
            "l.b.bFb.d",
            "l.bFbFb.d",
            "l.bFbwbFd",
            "lebwbwbed",
            "lbbbbbbbd",
            ".ddddddd.",
        };

        static void AddPhoenixCage(List<GearItem> items)
        {
            var pal = new Dictionary<char, Color>
            {
                ['l'] = Hex(0xfad264), ['b'] = Hex(0xdea034), ['d'] = Hex(0x9a5c1a),
                ['f'] = Hex(0xff8c28), ['F'] = Hex(0xffd65a), ['w'] = Hex(0xfffae6), ['e'] = Hex(0xe2461a),
            };
            var cage = RelicGrants(Make(PhoenixCageId, "Golden Cage", GearSlot.Relic, LootTier.BlackDiamond, 0f,
                                        HipRelic("gear.trinket.phoenix_cage", PhoenixCageRows, pal)),
                                   PhoenixArtId, WeaponClass.Bow, twoHanded: true);
            cage.ForgeOnly = true;
            items.Add(cage);
        }
    }
}
