using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Shogun: red lamellar over a black robe
    //
    // After the user's reference (a samurai warlord in red lacquer): a flowing BLACK ROBE worn
    // under RED SHINGLED ARMOUR - a lamellar do on the chest, a black obi at the waist with the
    // same red lames hanging from it to the knee (kusazuri), and sode of the same lames from the
    // shoulder to the elbow. Three pieces, named as materials: red lacquer, black silk, antique
    // gold. Diamond and power 0 throughout - the purely cosmetic tier, the same rule Black Steel,
    // Conclave and Seraph carry.
    //
    //   shogun_robe   Torso      the do AND the robe. The robe runs to the ground, over the knees
    //                            and the boots - the user's call: it is a robe, so what is under
    //                            it does not show. TorsoArmor already sorts over every leg and
    //                            boot layer in every stack, and is torso-parented like the tasset,
    //                            so the skirt hangs from the waist and the legs stride under it.
    //   shogun_sode   Shoulders  chin to elbow. Torso-parented like every pauldron, so the arm
    //                            swings under it - which is what sode actually do.
    //   shogun_obi    Belt       the sash, its gold cord, and the kusazuri to the knee. ONE Belt
    //                            sprite, not Belt + Tasset: Tasset belongs to the Legs slot's
    //                            gold_greaves, and two slots painting one layer overwrite each
    //                            other by application order (vanguard_sash made the same call).
    //
    // THE LAMES ARE THE READ. Each is four texels - lit top, base, base, a deep seam - so the
    // armour reads as rows of plates overlapping downward rather than a red block, and black
    // lacing ticks cross every seam. The do, the sode and the kusazuri all use the SAME lame
    // (ShogunLameTone), so the three pieces are one armour, not three that share a colour.
    //
    // Every grid is a FIELD (DemoGear.Fields.cs) sampled per body texel in TORSO-LOCAL texels (0 at the waist, y up,
    // +X the facing), so the positions below are the rig's own: the chin is at 40, the elbow 20
    // under the shoulder joint, the knee 24 under the hip, the sole at -48. Lit from +X, the
    // side every cuirass here is authored toward.
    //
    // The sode reach y 20, below the 12-cell envelope the cloak drapes are sized to (see
    // CLAUDE.md, "A cloak covers the pauldrons") - under the Wraithguard or Wanderer's cloak their
    // gold hem shows past the cloth.
    public static partial class DemoGear
    {
        // Ramp strings: tone 0 (line) .. 5 (glow), per material. Red lacquer is the main ramp,
        // black silk the second, antique gold the third - see Palette.Of's grid conventions.
        const string ShogunRed = "ksdblh", ShogunSilk = "KSDBLH", ShogunGold = "123456";

        // Anchors, in TORSO-LOCAL BODY TEXELS - see the header.
        const int ShogunChin = 40, ShogunNeck = 44, ShogunSole = -48, ShogunKnee = -24;
        const int ShogunLame = 4;

        static void AddShogun(List<GearItem> items)
        {
            // Lacquer lit toward a warm sheen, not white: a red lifted toward white is PINK, the
            // same trap Palette.Bronze and the Reactor's violet both document.
            var lacquer = new Palette.Ramp(new Color(0.60f, 0.12f, 0.10f), shade: 0.34f)
                .WithHighlightsToward(new Color(1.00f, 0.58f, 0.40f), 0.30f);
            // The robe is built the way Palette.Undersuit is, and for its reasons: a near-black
            // base needs its shadows placed below LIGHT or its folds collapse, and a low lift so
            // the sheen stays black. Never 'H' on it (see Undersuit's note on Glow).
            var silk = new Palette.Ramp(new Color(0.16f, 0.155f, 0.19f), lift: 0.16f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.46f, 0.30f);
            var gold = new Palette.Ramp(new Color(0.80f, 0.60f, 0.24f));
            var pal = Palette.Of(lacquer, silk, gold);
            // The far sode: the whole palette moved into shadow, spacing intact (Ramp.Scaled). Not
            // "one ramp, no trim" like the other far pauldrons - the lacing and the gold hem are
            // what make a sode a sode rather than a red slab, and the far one is half hidden anyway.
            var far = Palette.Of(lacquer.Scaled(0.78f), silk.Scaled(0.80f), gold.Scaled(0.78f));

            items.Add(Defends(Make("shogun_robe", "Shogun Robe", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.shogun", ShogunRobeRows, pal,
                       0f, FieldCentreCells(ShogunRobeBottom, ShogunNeck), ppu: BodyPpu)),
                // A duelist's parry, as the Wraithguard's - a swordsman's armour, not a shield wall.
                DefensiveAbility.ParryStance));

            // Placed off the joint like every pauldron, top edge on the chin (PlateY). The grid is
            // not centred on the joint - the plate flares OUTWARD - so its x is the joint plus the
            // grid's own centre, mirrored for the far side.
            var sode = Make("shogun_sode", "Shogun Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.shogun", ShogunSodeNear, pal,
                       -ShoulderX - ShogunSodeCentreCells, PlateY(ShogunSodeNear), ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.shogun.dark", ShogunSodeFar, far,
                       ShoulderX + ShogunSodeCentreCells, PlateY(ShogunSodeFar), ppu: BodyPpu));
            // A sode HANGS down the upper arm rather than capping the shoulder, so in the rest
            // carry it turns with the arm's whole raise - see GearItem.HangsAlongArm.
            sode.HangsAlongArm = true;
            items.Add(sode);

            items.Add(Make("shogun_obi", "Shogun Sash", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.shogun", ShogunObiRows, pal,
                       0f, FieldCentreCells(ShogunObiBottom, ShogunObiTop), ppu: BodyPpu)));

            Dyeable(items, "shogun_", DyeChannel.Of("Lacquer", DyeMaterial.Metal, lacquer, 0.78f),
                    DyeChannel.Of("Silk", DyeMaterial.Cloth, silk, 0.80f));
        }

        // ------------------------------------------------------------------ the lame

        /// <summary>
        /// One lame's tone, by row within it counted from the TOP: a lit top edge, two base rows,
        /// and a deep seam where the lame below tucks under it. <paramref name="shift"/> turns the
        /// surface toward or away from the light.
        /// </summary>
        static int ShogunLameTone(int rowInLame, int shift)
        {
            int t = rowInLame switch { 0 => 4, 1 => 3, 2 => 3, _ => 1 };
            // The seam stays a seam on the lit side - lifting it to 'd' there closed the rows up.
            return rowInLame == ShogunLame - 1 ? t + Mathf.Min(shift, 0) : t + shift;
        }

        /// <summary>
        /// True where a lacing tie crosses a seam: the seam row and the first row of the lame
        /// below, so every tie straddles the join it ties. Only those two - three rows with one
        /// left between them joined up into continuous columns, and with the seams they drew a
        /// BRICK WALL rather than shingles.
        /// </summary>
        static bool ShogunLacing(int rowInLame) => rowInLame == ShogunLame - 1 || rowInLame == 0;

        // ------------------------------------------------------------------ the robe (Torso)
        //
        // The do from the collar to the waist, and the robe from the waist to the ground. The
        // skirt FLARES (a bell, not a cone) and trails wider behind than in front, so it reads as
        // cloth with weight rather than a stiff tube; its hem curves DOWN at the middle, which is
        // what a round hem looks like from the camera's height, and is the one cue that says the
        // skirt goes all the way round the legs.

        const int ShogunRobeHalf = 28;
        const int ShogunRobeBottom = -50;   // the hem's lowest texel, at the middle - see ShogunHemY
        const int ShogunDoTop = 38, ShogunDoBottom = 8;

        static string[] _shogunRobeRows;
        static string[] ShogunRobeRows => _shogunRobeRows ??=
            PaintField(-ShogunRobeHalf, ShogunRobeHalf, ShogunRobeBottom, ShogunNeck, ShogunRobeTexel);

        /// <summary>How far the skirt has flared by height y, 0 at the waist to 1 at the sole.</summary>
        static float ShogunFlare(float y)
            => Mathf.Pow(Mathf.Clamp01((ShogunDoBottom - y) / (ShogunDoBottom - ShogunSole)), 1.25f);

        static float ShogunSkirtBack(float y) => 14f + 11f * ShogunFlare(y);
        static float ShogunSkirtFront(float y) => 14f + 8f * ShogunFlare(y);

        /// <summary>
        /// The hem: a texel above the sole at the edges, three texels lower in the middle. Covers
        /// both boots and their outline at rest (they span x -15..15, down to -52 outlined).
        /// </summary>
        static float ShogunHemY(float x)
        {
            float u = x < 0f ? x / 25f : x / 22f;
            return ShogunSole + 1f - 3f * (1f - Mathf.Clamp01(u * u));
        }

        static char ShogunRobeTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f;

            // ---- the do ----
            if (iy >= ShogunDoBottom && iy < ShogunDoTop)
            {
                // Square back edge, chest a texel proud of it; the top corners rounded under the arms.
                float back = -14f, front = 15f;
                if (iy == ShogunDoTop - 1) { back += 2f; front -= 2f; }
                else if (iy == ShogunDoTop - 2) { back += 1f; front -= 1f; }
                if (x >= back && x <= front) return ShogunDoTexel(ix, iy, x);
            }

            // ---- the collar: the robe's crossed lapels, above the do ----
            if (iy >= ShogunDoTop && iy < ShogunNeck && Mathf.Abs(x) <= 13f)
            {
                float v = (iy - (ShogunDoTop - 2)) * 1.5f;   // the V's arm at this height
                float ax = Mathf.Abs(x);
                if (ax < v) return 'S';                      // the under-kimono inside the V
                if (ax < v + 1f) return 'L';                 // the lapel's lit inner edge
                if (ax < v + 3.5f) return 'B';
                return 'D';                                  // the robe over the shoulder
            }

            // ---- the skirt ----
            if (iy < ShogunDoBottom)
            {
                float back = ShogunSkirtBack(y), front = ShogunSkirtFront(y);
                if (x < -back || x > front) return '.';
                float hem = ShogunHemY(x);
                if (y < hem) return '.';
                return ShogunSkirtTexel(x, y, x < 0f ? x / back : x / front, y - hem);
            }
            return '.';
        }

        static char ShogunDoTexel(int ix, int iy, float x)
        {
            // Turned toward the light on +X and away on the straight back edge.
            int shift = x > 9f ? 1 : x < -10f ? -1 : 0;

            // The top plate: a gold edge over a band of red, then the lames begin.
            if (iy == ShogunDoTop - 1) return RampChar(ShogunGold, 4 + shift);

            int fromTop = ShogunDoTop - 2 - iy;               // 0 = first red row under the gold
            int row = WrapMod(fromTop, ShogunLame);

            // Lacing: one tie per column, every seven - at every five they tiled the do.
            if (ShogunLacing(row) && fromTop >= ShogunLame - 1 && WrapMod(ix + 3, 7) == 0 && Mathf.Abs(x) < 13f)
                return row == ShogunLame - 1 ? 'K' : 'S';

            return RampChar(ShogunRed, ShogunLameTone(row, shift));
        }

        /// <param name="s">Across the skirt, -1 at the back edge to +1 at the front.</param>
        /// <param name="d">Height above the hem.</param>
        static char ShogunSkirtTexel(float x, float y, float s, float d)
        {
            int shift = s > 0.62f ? 1 : s < -0.80f ? -2 : s < -0.55f ? -1 : 0;

            // The hem: the robe's red lining turned over its edge, then a band of gold fans -
            // the reference's ogi pattern - sitting on a gold line.
            if (d < 2f) return RampChar(ShogunRed, 3 + shift);
            if (d < 3f) return 'S';
            if (d < 9f)
            {
                float dd = d - 3f;
                float dx = WrapMod(Mathf.FloorToInt(x) + 4, 8) - 3.5f;
                float r = Mathf.Sqrt(dx * dx + dd * dd);
                if (dd < 1f) return RampChar(ShogunGold, 3 + shift);       // the line
                if (r >= 3.2f && r < 4.4f && dd < 5f) return RampChar(ShogunGold, 4 + shift); // the arc
                if (r < 3.2f && Mathf.Abs(dx) < 0.6f) return RampChar(ShogunGold, 3 + shift);  // the rib
            }

            // Folds: long valleys fanning out from the waist, each with a lit ridge on its +X side.
            // CONTINUOUS from the waist to the hem - a dashed crease reads as scattered squares
            // (CLAUDE.md on CapeRows).
            float flare = ShogunFlare(y);
            float vw = 0.5f + 1.0f * flare;
            foreach (float f in ShogunFolds)
            {
                float fx = f * (f < 0f ? ShogunSkirtBack(y) : ShogunSkirtFront(y));
                float o = x - fx;
                if (Mathf.Abs(o) < vw) return RampChar(ShogunSilk, 1 + Mathf.Min(shift, 0));
                if (o >= vw && o < vw + 1f && flare > 0.15f) return RampChar(ShogunSilk, 4 + Mathf.Min(shift, 0));
            }
            return RampChar(ShogunSilk, 3 + shift);
        }

        static readonly float[] ShogunFolds = { -0.62f, -0.22f, 0.22f, 0.64f };

        // ------------------------------------------------------------------ the sode (Shoulders)
        //
        // Chin to elbow: a black crown plate with a gold edge, four lames, a gold hem - 20 texels,
        // so with its top on the chin (40) the hem lands on the elbow joint (42 - 20 = 22). Wider
        // at the hem than the crown, the extra all on the OUTER side, so it stands off the arm the
        // way a sode does rather than hugging it. Two grids, mirrored, because the flare has a
        // side: the near plate flares to -X and the far one to +X.

        // The inner edge sits only five texels in from the joint: at seven the two plates nearly
        // met at the sternum and the do showed as a strip between them - a cape, not armour.
        const int ShogunSodeRows = 20, ShogunSodeInner = 5, ShogunSodeOuterTop = 8, ShogunSodeOuterHem = 11;
        // The grid spans outward offsets [-6, 12): 18 wide, centred three texels outboard of the joint.
        const int ShogunSodeMin = -(ShogunSodeInner + 1), ShogunSodeMax = ShogunSodeOuterHem + 1;
        static float ShogunSodeCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((ShogunSodeMin + ShogunSodeMax) * 0.5f);

        static string[] _shogunSodeNear, _shogunSodeFar;
        static string[] ShogunSodeNear => _shogunSodeNear ??= ShogunSode(outwardIsPlusX: false);
        static string[] ShogunSodeFar => _shogunSodeFar ??= ShogunSode(outwardIsPlusX: true);

        static string[] ShogunSode(bool outwardIsPlusX)
        {
            // Painted in OUTWARD offsets (o, away from the body) and flipped into grid columns, so
            // one field serves both sides and the light still comes from torso +X.
            return PaintField(ShogunSodeMin, ShogunSodeMax, 0, ShogunSodeRows, (ix, iy) =>
            {
                int o = outwardIsPlusX ? ix : ShogunSodeMin + ShogunSodeMax - 1 - ix;
                return ShogunSodeTexel(o, ShogunSodeRows - 1 - iy, outwardIsPlusX);
            });
        }

        /// <param name="o">Outward offset from the joint's column, in texels.</param>
        /// <param name="r">Row from the top.</param>
        static char ShogunSodeTexel(int o, int r, bool outwardIsPlusX)
        {
            float oc = o + 0.5f;
            float outer = ShogunSodeOuterTop + (ShogunSodeOuterHem - ShogunSodeOuterTop) * r / (ShogunSodeRows - 1f);
            float inner = -ShogunSodeInner;
            // The crown's outer corner rounded, the inner one square against the collar.
            if (r == 0) outer -= 2f;
            else if (r == 1) outer -= 1f;
            if (oc < inner || oc > outer) return '.';

            // Toward torso +X is toward the light: the near sode's inner edge, the far sode's outer.
            float q = (oc - inner) / (outer - inner);           // 0 inner .. 1 outer
            float towardLight = outwardIsPlusX ? q : 1f - q;
            int shift = towardLight > 0.72f ? 1 : towardLight < 0.22f ? -1 : 0;

            if (r == 0) return RampChar(ShogunGold, 4 + shift);
            if (r == 1) return RampChar(ShogunSilk, 3 + Mathf.Min(shift, 0));
            if (r >= ShogunSodeRows - 2)
                return RampChar(ShogunGold, (r == ShogunSodeRows - 2 ? 4 : 3) + shift);

            int row = WrapMod(r - 2, ShogunLame);
            if (ShogunLacing(row) && r >= 2 + ShogunLame - 1 && (o == -1 || o == 5))
                return row == ShogunLame - 1 ? 'K' : 'S';
            return RampChar(ShogunRed, ShogunLameTone(row, shift));
        }

        // ------------------------------------------------------------------ the obi (Belt)
        //
        // A black sash wrapped twice, knotted in gold cord at the front, and the kusazuri hanging
        // from under it to the knee: four panels, flaring, each curving down at its middle (a
        // plate bent round the thigh) with a gold hem. The panel seams are drawn as lines, not
        // cut - a transparent gap under ~5 texels is closed by the auto-outline anyway.

        const int ShogunObiTop = 12, ShogunSashBottom = 4, ShogunObiBottom = -28, ShogunObiHalf = 22;

        static string[] _shogunObiRows;
        static string[] ShogunObiRows => _shogunObiRows ??=
            PaintField(-ShogunObiHalf, ShogunObiHalf, ShogunObiBottom, ShogunObiTop, ShogunObiTexel);

        /// <summary>Panel seams, as fractions of the kusazuri's half-width at that height.</summary>
        static readonly float[] ShogunKusazuriSeams = { -0.55f, 0.05f, 0.62f };

        static char ShogunObiTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f;

            char cord = ShogunCordTexel(x, y);
            if (cord != '.') return cord;

            // ---- the sash ----
            if (iy >= ShogunSashBottom && iy < ShogunObiTop)
            {
                float half = 16f - ((iy == ShogunObiTop - 1 || iy == ShogunSashBottom) ? 1f : 0f);
                if (Mathf.Abs(x) > half) return '.';
                int shift = x > 11f ? 1 : x < -12f ? -1 : 0;
                // Two wraps: a lit top edge on each, a fold line between them.
                int t = (ShogunObiTop - 1 - iy) switch { 0 => 4, 3 => 2, 4 => 4, 7 => 1, _ => 3 };
                return RampChar(ShogunSilk, Mathf.Min(t + shift, 4));
            }

            // ---- the kusazuri ----
            if (iy < ShogunSashBottom)
            {
                float fall = Mathf.Clamp01((ShogunSashBottom - y) / (ShogunSashBottom - ShogunKnee));
                float front = 15f + 4f * fall, back = 16f + 4f * fall;
                if (x < -back || x > front) return '.';
                float s = x < 0f ? x / back : x / front;

                // Which panel, and where across it (-1..1).
                int p = 0;
                while (p < ShogunKusazuriSeams.Length && s > ShogunKusazuriSeams[p]) p++;
                float lo = p == 0 ? -1f : ShogunKusazuriSeams[p - 1];
                float hi = p == ShogunKusazuriSeams.Length ? 1f : ShogunKusazuriSeams[p];
                float v = (s - lo) / (hi - lo) * 2f - 1f;

                // Each panel's hem bows down at its middle and lands on the knee there.
                float hem = ShogunKnee + 2.5f - 2.5f * (1f - v * v);
                if (y < hem) return '.';

                int shift = p == 0 ? -1 : p == ShogunKusazuriSeams.Length ? 1 : 0;
                if (v > 0.55f) shift++;
                if (v < -0.7f) shift--;

                // The seam between panels.
                float halfW = x < 0f ? back : front;
                foreach (float seam in ShogunKusazuriSeams)
                    if (Mathf.Abs(x - seam * halfW) < 0.5f) return 'k';

                if (y - hem < 2f) return RampChar(ShogunGold, (y - hem < 1f ? 3 : 4) + Mathf.Clamp(shift, -1, 1));

                int fromTop = ShogunSashBottom - 1 - iy;
                int row = WrapMod(fromTop, ShogunLame);
                // One tie down each panel's middle: the seams are already vertical lines, and two
                // more per panel made the kusazuri a grid.
                if (ShogunLacing(row) && fromTop >= ShogunLame - 1 && Mathf.Abs(v) < 0.11f)
                    return row == ShogunLame - 1 ? 'K' : 'S';
                return RampChar(ShogunRed, ShogunLameTone(row, Mathf.Clamp(shift, -2, 1)));
            }
            return '.';
        }

        /// <summary>
        /// The gold cord that ties the obi: a knot on the front of the sash and two tails with
        /// tassels hanging over the kusazuri. '.' where there is no cord.
        /// </summary>
        static char ShogunCordTexel(float x, float y)
        {
            const float kx = 7f, ky = 8f;
            // The knot: a bright core between two loops.
            if (Mathf.Abs(x - kx) < 1.5f && Mathf.Abs(y - ky) < 1.5f) return RampChar(ShogunGold, 5);
            for (int side = -1; side <= 1; side += 2)
            {
                float lx = (x - (kx + side * 3f)) / 2.4f, ly = (y - (ky + 0.8f)) / 1.8f;
                float e = lx * lx + ly * ly;
                if (e <= 1f) return e < 0.25f ? RampChar(ShogunGold, 2) : RampChar(ShogunGold, side > 0 ? 4 : 3);
            }

            // Two tails, each a line from under the knot to a tassel.
            if (ShogunTail(x, y, kx - 1f, kx - 3f, -7f) || ShogunTail(x, y, kx + 1f, kx + 3f, -4f))
                return RampChar(ShogunGold, 4);
            if (ShogunTassel(x, y, kx - 3f, -7f) || ShogunTassel(x, y, kx + 3f, -4f))
                return RampChar(ShogunGold, 3);
            return '.';
        }

        static bool ShogunTail(float x, float y, float x0, float x1, float yEnd)
        {
            const float yStart = 6.5f;
            if (y > yStart || y < yEnd) return false;
            float t = (yStart - y) / (yStart - yEnd);
            return Mathf.Abs(x - Mathf.Lerp(x0, x1, t)) < 0.6f;
        }

        static bool ShogunTassel(float x, float y, float cx, float top)
            => y <= top && y > top - 4f && Mathf.Abs(x - cx) < 1.6f;
    }
}
