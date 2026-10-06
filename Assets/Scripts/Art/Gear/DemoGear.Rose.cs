using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Briar Rose: a rose stem bent into a ring
    //
    // After the user's sketch: a green stem closed into a slightly tall ring, crimson thorns all
    // round it - two broad ones meeting over the top like a bow, a tall curved horn either side
    // of them, a big wedge at each lower corner, smaller ones pointing out and a few pointing IN
    // across the hollow. The THORNS are the silhouette: the stem alone is just a thinner disc.
    //
    // ONE field (RoseTexel) for arena and menu - sampled per cell and per QUARTER cell - so a thorn
    // cannot move between the two. The thorn list is written for the LEFT half and mirrored:
    // SetWeaponSplit copies the sprite into the off hand unmirrored (see the dual blades in
    // Build), so the silhouette must be a palindrome. The lighting is one-sided, like every disc.
    //
    // Stem over thorns, so each thorn grows OUT of the stem's edge rather than sitting on it; the
    // leather grip bar every disc has runs across the hollow under the stem and over the inward
    // thorns. The thorns that carry the silhouette are drawn LARGER than the sketch's, in both
    // grids - see RoseThorn for why - and the slivers are menu detail only.
    //
    // Arrays are LAZY - see the note on Tria Prima's: C# does not order static initialisers
    // across the files of a partial class.
    public static partial class DemoGear
    {
        const int RoseW = 28, RoseH = 28;
        /// <summary>Ring centre, in arena cells - a texel BOUNDARY, so the thorns mirror exactly.</summary>
        const float RoseCx = 14f, RoseCy = 15f;
        /// <summary>The stem's outer radius across, in cells. Under a plain disc's 12: the thorns
        /// add up to half again beyond it, and past that the pair swallows the torso.</summary>
        const float RoseR = 10f;
        /// <summary>The sketch's ring is taller than wide by this much.</summary>
        const float RoseOval = 1.083f;
        /// <summary>The stem's inner edge as a fraction of the outer - about three cells of stem,
        /// thicker than the sketch's (0.83), which would be under two cells and one tone.</summary>
        const float RoseInner = 0.72f;
        /// <summary>The grip bar's half-height in arena cells - two rows in all.</summary>
        const float RoseGripHalf = 1f;

        static readonly Vector2Int RoseGrip = new((int)RoseCx, (int)RoseCy);
        static readonly Vector2Int RoseDetailGrip = new((int)RoseCx * RoseDetailScale, (int)RoseCy * RoseDetailScale);

        /// <summary>
        /// One thorn as measured off the sketch: where it leaves the stem (<c>Angle</c>, degrees,
        /// 0 = right, 90 = up), its tip in ring radii with +v UP (the sketch's ring centre, divided
        /// by its ring's half-width), and its half-width at the base in radii. <c>Bow</c> bends the
        /// axis sideways with both ends fixed - the horns. <c>MinLength</c> is how far an ARENA
        /// thorn must stand past the stem, in radii.
        ///
        /// The sketch's thorns are small against its ring, and at arena density that proportion
        /// does not survive: under about three cells of base and four of length a thorn is a bump,
        /// and a one-cell point is taken by the outline. So the thorns that carry the silhouette
        /// are pushed out to <c>MinLength</c> along the sketch's own direction and widened, and the
        /// slivers (<c>Arena</c> false) are drawn in the menu only. Lengthened in BOTH grids -
        /// the tip is resolved once - so the silhouette is the same on either screen.
        /// </summary>
        readonly struct RoseThorn
        {
            public readonly float Angle, TipU, TipV, HalfWidth, Bow, MinLength, Taper;
            public readonly bool Inward, Arena;
            public RoseThorn(float angle, float tipU, float tipV, float halfWidth, float bow = 0f,
                             bool inward = false, bool arena = true, float minLength = 0.42f,
                             float taper = 1f)
            {
                Angle = angle; TipU = tipU; TipV = tipV; HalfWidth = halfWidth; Bow = bow;
                Inward = inward; Arena = arena; MinLength = minLength; Taper = taper;
            }
        }

        static readonly RoseThorn[] RoseLeft =
        {
            // outward
            new(105f, -0.51f,  1.41f, 0.28f, taper: 0.6f),        // the bow over the top, one wing
            new(140f, -0.76f,  1.02f, 0.07f, 0.16f, minLength: 0.55f), // the tall horn beside it
            new(124f, -0.62f,  0.92f, 0.05f, arena: false),
            new(154f, -1.06f,  0.56f, 0.08f),
            new(170f, -1.02f,  0.26f, 0.04f, arena: false),
            new(183f, -1.13f, -0.08f, 0.07f),
            new(204f, -1.05f, -0.49f, 0.09f),
            new(227f, -0.92f, -1.11f, 0.24f, minLength: 0.62f),   // the big wedge, lower corner
            new(254f, -0.24f, -1.21f, 0.05f, -0.06f),
            // inward, across the hollow
            new(148f, -0.44f,  0.49f, 0.06f, inward: true),
            new(168f, -0.71f,  0.18f, 0.04f, inward: true),
            new(197f, -0.71f, -0.05f, 0.04f, inward: true),
            new(232f, -0.45f, -0.62f, 0.035f, inward: true, arena: false),
            new(240f, -0.38f, -0.69f, 0.035f, inward: true, arena: false),
            new(247f, -0.31f, -0.73f, 0.035f, inward: true, arena: false),
            new(108f, -0.24f,  0.81f, 0.04f, inward: true, arena: false),
            new(124f, -0.41f,  0.71f, 0.04f, inward: true, arena: false),
        };

        /// <summary>A thorn resolved into the shape the field tests: base on the stem's edge,
        /// the (possibly lengthened) tip, and the half-width after the arena minimum.</summary>
        readonly struct RoseSpike
        {
            public readonly Vector2 Base, Dir, Perp;
            public readonly float Length, HalfWidth, Bow, Taper;
            public readonly bool Arena;
            public RoseSpike(Vector2 b, Vector2 tip, float halfWidth, float bow, float taper, bool arena)
            {
                Base = b;
                var a = tip - b;
                Length = a.magnitude;
                Dir = a / Length;
                Perp = new Vector2(-Dir.y, Dir.x);
                HalfWidth = halfWidth; Bow = bow; Taper = taper; Arena = arena;
            }
        }

        static RoseSpike Resolve(in RoseThorn th, bool mirror)
        {
            float deg = mirror ? 180f - th.Angle : th.Angle;
            float a = deg * Mathf.Deg2Rad, edge = th.Inward ? RoseInner : 1f;
            var b = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * RoseOval) * edge;
            var tip = new Vector2(mirror ? -th.TipU : th.TipU, th.TipV);
            float len = (tip - b).magnitude;
            float k = th.Inward ? Mathf.Max(1f, 0.20f / len)
                    : Mathf.Max(1.2f, (th.Arena ? th.MinLength : 0.25f) / len);
            float hw = th.Inward ? Mathf.Max(th.HalfWidth, th.Arena ? 0.10f : 0.05f)
                                 : Mathf.Max(th.HalfWidth, th.Arena ? 0.15f : 0.05f);
            return new RoseSpike(b, b + (tip - b) * k, hw, mirror ? -th.Bow : th.Bow, th.Taper, th.Arena);
        }

        static RoseSpike[] _roseSpikes;
        static RoseSpike[] RoseSpikes
        {
            get
            {
                if (_roseSpikes != null) return _roseSpikes;
                var all = new List<RoseSpike>();
                foreach (var t in RoseLeft) all.Add(Resolve(t, false));
                foreach (var t in RoseLeft) all.Add(Resolve(t, true));
                return _roseSpikes = all.ToArray();
            }
        }

        /// <summary>Light from the upper left, as every disc is drawn, in (u, v-up) space.</summary>
        static readonly Vector2 RoseLight = new Vector2(-1f, 1f).normalized;

        static float RoseRho(float u, float v) => Mathf.Sqrt(u * u + (v / RoseOval) * (v / RoseOval));

        /// <summary>
        /// Where (u,v) sits against one thorn. <paramref name="t"/> runs 0 at the stem's edge to 1
        /// at the tip (negative is under the stem, which covers it); <paramref name="sCells"/> is the
        /// signed distance off the (bowed) axis in CELLS and <paramref name="halfCells"/> the
        /// thorn's half-width there, so edges are sized in cells whatever the thorn's length.
        /// </summary>
        static void RoseThornFrame(in RoseSpike th, float u, float v,
                                   out float t, out float sCells, out float halfCells)
        {
            var d = new Vector2(u, v) - th.Base;
            t = Vector2.Dot(d, th.Dir) / th.Length;
            float s = Vector2.Dot(d, th.Perp) - th.Bow * th.Length * 4f * t * (1f - t);
            sCells = s * RoseR;
            halfCells = th.HalfWidth * Mathf.Pow(Mathf.Clamp01(1f - t), th.Taper) * RoseR;
        }

        /// <summary>Menu art samples the field at a QUARTER cell and is drawn at true MenuPpu with
        /// no block-doubling - the swords' density (EmberDetailScale), not the plain discs' 2x.</summary>
        const int RoseDetailScale = 4;

        /// <summary>Stable 0..1 hash of an integer - the bristles' placement.</summary>
        static float RoseHash(int i)
        {
            unchecked
            {
                int h = i * 374761393 + 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        /// <summary>Is (u,v) on a thorn's body (not just its root under the stem)?</summary>
        static bool RoseOnThorn(float u, float v, bool menu)
        {
            foreach (var th in RoseSpikes)
            {
                if (!menu && !th.Arena) continue;
                RoseThornFrame(th, u, v, out float t, out float s, out float half);
                if (t >= -0.05f && t <= 1f && Mathf.Abs(s) < half) return true;
            }
            return false;
        }

        /// <summary>
        /// The whole disc as a field, sampled at <paramref name="scale"/> texels per arena cell.
        /// Scale 1 is the arena; the menu (<see cref="RoseDetailScale"/>) spends its texels on:
        ///
        ///   STEM    lengthwise grain, a specular streak down the lit side, fine bristles along
        ///           both edges (a real rose stem is covered in them), and the SHADOW each thorn
        ///           casts across it - the thorn's own shape shifted away from the light.
        ///   THORNS  crimson at the root going to a pale woody TIP through a dithered band (how a
        ///           real thorn ripens), a one-texel lit edge, a gloss streak on the lit half and
        ///           the sketch's inner line as a rib up the shaded half.
        ///   GRIP    a cross-wrapped leather binding - chevrons, symmetric so the unmirrored
        ///           off-hand copy is not a mirror-image wrap.
        ///
        /// Every threshold here is in TEXELS (<c>px</c>, cells per texel), so the menu's detail
        /// is one texel wide at its own density rather than a quarter of a cell scaled up.
        /// </summary>
        static char RoseTexel(float u, float v, int scale)
        {
            bool menu = scale > 1;
            float px = 1f / scale;
            float rho = RoseRho(u, v);
            float au = Mathf.Abs(u);                                  // mirror-safe angle
            float phi = Mathf.Atan2(v / RoseOval, au);

            // ---- bristles: short dark hairs off both stem edges, menu only
            if (menu && (rho > 1f || rho < RoseInner))
            {
                bool outer = rho > 1f;
                float gap = outer ? rho - 1f : RoseInner - rho;      // in radii
                const int Count = 90;                                 // per half turn
                float slot = (phi / Mathf.PI + 0.5f) * Count;
                int i = Mathf.FloorToInt(slot) + (outer ? 0 : 1000);
                float centre = 0.5f + (RoseHash(i) - 0.5f) * 0.5f;
                float len = (1.5f + RoseHash(i + 7) * 2f) * px / RoseR * (outer ? 1f : 0.7f);
                float across = Mathf.Abs(slot - Mathf.Floor(slot) - centre) * (Mathf.PI / Count)
                               * (outer ? 1f : RoseInner) * RoseR;    // in cells
                if (RoseHash(i + 13) < 0.55f && gap < len && across < 0.55f * px
                    && !RoseOnThorn(u, v, true))
                {
                    // don't sprout one out of a thorn's root either
                    var back = new Vector2(u, v) * (outer ? 0.9f : 1.1f);
                    if (!RoseOnThorn(back.x, back.y, true)) return gap < len * 0.5f ? 'd' : 's';
                }
            }

            // ---- stem
            if (rho <= 1f && rho >= RoseInner)
            {
                float across = (rho - RoseInner) / (1f - RoseInner);
                var n = new Vector2(u, v / RoseOval).normalized;
                float ndl = Vector2.Dot(n, RoseLight);
                float tube = 1f - Mathf.Abs(2f * across - 1f);
                float val = 0.55f * tube + 0.45f * (2f * across - 1f) * ndl - 0.12f;

                if (menu)
                {
                    // the thorn's cast shadow, and a contact line where it leaves the stem
                    var lift = RoseLight * (1.4f / RoseR);
                    bool shaded = RoseOnThorn(u + lift.x, v + lift.y, true);
                    if (shaded) val -= 0.38f;
                    else
                        foreach (var th in RoseSpikes)
                        {
                            RoseThornFrame(th, u, v, out float t, out float s, out float half);
                            if (t > -2.2f * px / (th.Length * RoseR) && t < 0f && Mathf.Abs(s) < half)
                            { val -= 0.3f; shaded = true; break; }
                        }

                    // specular streak down the lit side of the tube - decided BEFORE the grain,
                    // which otherwise breaks it into dashes
                    if (!shaded && ndl > 0.35f
                        && Mathf.Abs(across - 0.62f) < 0.9f * px / ((1f - RoseInner) * RoseR))
                        return 'h';

                    // grain: fine lines running WITH the stem, wandering a little along it. Kept
                    // faint - stronger, it read as scratches rather than as a living stem
                    float grain = Mathf.Sin(2f * Mathf.PI * (across * 4.5f + 0.18f * Mathf.Sin(phi * 7f)));
                    val += 0.055f * grain;
                }

                if (menu && val > 0.55f) return 'h';
                return val > 0.25f ? 'l' : val > -0.05f ? 'b' : val > -0.35f ? 'd' : 's';
            }

            // ---- the grip, across the hollow: RoseGripHalf either side of the centre. Half the
            // plain discs' four rows - at four, the bar was the heaviest thing inside a ring that
            // is otherwise a thin stem.
            float y = RoseCy - v * RoseR;
            if (rho < RoseInner && Mathf.Abs(y - RoseCy) < RoseGripHalf)
            {
                if (!menu) return y < RoseCy ? 'B' : 'D';
                float fromTop = y - (RoseCy - RoseGripHalf);
                float rows = RoseGripHalf * 2f;
                if (fromTop < px || fromTop > rows - px) return 'K';
                if (fromTop < 2f * px) return 'L';
                if (fromTop > rows - 2f * px) return 'S';
                // cross-wrapped: chevrons of wrap, a dark seam and a lit ridge on each
                float w = (au * RoseR + fromTop * 0.9f) / (4f * px);
                float f = w - Mathf.Floor(w);
                return f < 0.2f ? 'S' : f < 0.4f ? 'L' : fromTop < rows / 2f ? 'B' : 'D';
            }

            // ---- thorns
            foreach (var th in RoseSpikes)
            {
                if (!menu && !th.Arena) continue;
                RoseThornFrame(th, u, v, out float t, out float s, out float half);
                if (t < -0.3f || t > 1f || Mathf.Abs(s) >= half) continue;

                bool lit = Vector2.Dot(th.Perp * Mathf.Sign(s), RoseLight) > 0f;
                if (!menu)
                {
                    if (t < 0.15f) return lit ? '4' : '3';
                    return lit ? '5' : '4';
                }

                // crimson to a woody tip, through a dithered band so it is a ramp, not a seam
                float tipness = Mathf.InverseLerp(0.55f, 0.8f, t);
                int dx = Mathf.FloorToInt((u * RoseR + 100f) * scale), dy = Mathf.FloorToInt((v * RoseR + 100f) * scale);
                float bayer = ((dx & 1) * 2 + (dy & 1)) switch { 0 => 0.125f, 1 => 0.625f, 2 => 0.875f, _ => 0.375f };
                bool tip = tipness > bayer;

                bool edge = Mathf.Abs(s) > half - 1.1f * px;
                // the sketch's inner line: a rib up the SHADED half, on every thorn wide enough
                // to hold one (every outward arena thorn is)
                if (th.HalfWidth >= 0.15f && !lit && t > 0.12f && t < 0.75f
                    && Mathf.Abs(Mathf.Abs(s) - half * 0.3f) < 0.6f * px) return '2';
                if (edge) return lit ? (tip ? 'z' : '6') : (tip ? 'y' : '3');
                // gloss streak on the lit half
                if (lit && t > 0.18f && t < 0.7f && Mathf.Abs(Mathf.Abs(s) - half * 0.55f) < 0.6f * px)
                    return tip ? 'z' : '6';
                if (t < 0.12f) return lit ? '4' : '3';
                if (tip) return lit ? 'w' : 'x';
                return lit ? '5' : '4';
            }
            return '.';
        }

        static string[] BuildRose(int scale)
        {
            int w = RoseW * scale, h = RoseH * scale;
            var rows = new string[h];
            var sb = new System.Text.StringBuilder(w);
            for (int r = 0; r < h; r++)
            {
                sb.Clear();
                for (int c = 0; c < w; c++)
                {
                    float x = (c + 0.5f) / scale, y = (r + 0.5f) / scale;
                    sb.Append(RoseTexel((x - RoseCx) / RoseR, (RoseCy - y) / RoseR, scale));
                }
                rows[r] = sb.ToString();
            }
            // The arena strips specks; the menu only drops texels touching NOTHING, even at a
            // corner - a bristle is meant to be one texel wide, but a thin horn's tip sampled
            // into a lone texel would be stroked by the outline into a floating dot.
            return scale == 1 ? StripSpecks(rows) : RoseDropIslands(rows);
        }

        static string[] RoseDropIslands(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var next = new char[h][];
            for (int y = 0; y < h; y++) next[y] = rows[y].ToCharArray();
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (rows[y][x] == '.') continue;
                bool touched = false;
                for (int dy = -1; dy <= 1 && !touched; dy++)
                for (int dx = -1; dx <= 1 && !touched; dx++)
                {
                    int yy = y + dy, xx = x + dx;
                    if ((dx != 0 || dy != 0) && yy >= 0 && yy < h && xx >= 0 && xx < w
                        && rows[yy][xx] != '.') touched = true;
                }
                if (!touched) next[y][x] = '.';
            }
            for (int y = 0; y < h; y++) rows[y] = new string(next[y]);
            return rows;
        }

        static string[] _roseRows, _roseDetail;
        static string[] RoseRows => _roseRows ??= BuildRose(1);
        static string[] RoseDetail => _roseDetail ??= BuildRose(RoseDetailScale);

        /// <summary>Stem green, lifted less than the sketch's pastel so the tube keeps its tones;
        /// the leather grip every disc has; thorns in crimson, on digits; and the thorns' woody
        /// TIPS on four spare lowercase letters (w light, x base, y dark, z glow) - a fourth
        /// material the three-ramp Palette.Of has no case left for.</summary>
        static Dictionary<char, Color> RosePal()
        {
            var pal = Palette.Of(new Palette.Ramp(new Color(0.40f, 0.72f, 0.34f), lift: 0.30f),
                                 Palette.Leather,
                                 new Palette.Ramp(new Color(0.88f, 0.08f, 0.34f), lift: 0.28f));
            var wood = new Palette.Ramp(new Color(0.78f, 0.60f, 0.44f), lift: 0.30f);
            pal['z'] = wood.Glow;
            pal['w'] = wood.Light;
            pal['x'] = wood.Base;
            pal['y'] = wood.Dark;
            return pal;
        }

        static GearItem BriarRose()
            => Disc(WithMenu(Make("briar_rose_discs", "Briar Rose", GearSlot.Weapon, LootTier.Diamond, 0f,
                    Pixels(RigLayer.Weapon, "gear.weapon.disc.rose", RoseRows, RosePal(),
                           DiscHandX, DiscHandY, pivotTexel: RoseGrip, ppu: FinePpu, upscale2x: true)),
                    Pixels(RigLayer.Weapon, "gear.weapon.disc.rose.menu", RoseDetail, RosePal(),
                           DiscHandX, DiscHandY, pivotTexel: RoseDetailGrip, ppu: MenuPpu)));
    }
}
