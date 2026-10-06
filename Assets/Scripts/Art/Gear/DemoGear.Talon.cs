using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Talon: one arm armoured, one arm bare
    //
    // After the user's three references, all the same set: a champion in worn leathers with ONE
    // ARM in plate. Its silhouette is the whole point and none of it is borrowed from another set
    // here - every set so far is symmetric, and this one is built on being lopsided:
    //
    //   - on one shoulder a FAN of jagged steel blades - one spike rising past the cheek, the
    //     rest radiating out and down - and under it the arm plated to the fingertips: lames
    //     spiked on their outer corners, a spiked couter, a flared vambrace, CLAWED fingers
    //   - the other arm BARE, in the wearer's own skin (GearItem.ShowsSkin), a long leather
    //     bracer and a fingerless glove; fur over that shoulder
    //   - a thick FUR collar round the neck and across both shoulders
    //   - a grey leather jerkin, a dark V-shaped plastron under the fur, brown straps from the
    //     shoulders to a WIDE DOUBLE belt with a big square buckle and a pouch
    //   - coat skirts parting at the front and long split TAILS behind the legs
    //   - tall greaves whose knee plates rise to a POINT up the thigh, a wing off the outside
    //
    //   talon_jerkin     Torso      fur collar, plastron, straps, the jerkin's front clasps
    //   talon_pauldron   Shoulders  the blade fan (far), fur and a strap over the near shoulder
    //   talon_gauntlet   Gloves     the plated arm (far), the bare arm (near)
    //   talon_belt       Belt       two belts, a square buckle, a smaller one, a pouch
    //   talon_skirts     Legs       the coat's front skirts, open over the thighs
    //   talon_greaves    Boots      pointed knee plates, layered shins, plated feet (two grids)
    //   talon_coat       Back       a fur mantle and the coat's back to its split tails
    //
    // THE FAR ARM IS THE ARMOURED ONE - the character's LEFT, facing right (+X) - by the user's
    // call, against the references (whose plate is on the right arm). The rest carry raises the
    // NEAR arm, and with the plate there the fan swung up with it; on the far arm the bare hand
    // carries the sword and the plated arm hangs still under the fan. The rig mirrors the whole
    // figure to face left, so the plate always stays on the arm away from the camera. TURNED
    // AWAY the rig keeps the carry in the right hand by moving it to arm.back, so the pauldron
    // and gauntlet are GearItem.Lopsided: their two sides trade places, mirrored, and the plate
    // stays on the left arm from behind too.
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    // Diamond, power 0, like every reference-built cosmetic set.
    public static partial class DemoGear
    {
        // Steel fittings on pieces whose three ramps are already spent - dark, base, light, glow.
        const char TalSteelDark = 'm', TalSteelBase = 'n', TalSteelLight = 'p', TalSteelGlow = 'q';

        static void AddTalon(List<GearItem> items)
        {
            // Worn dark steel: shadows placed below LIGHT so the seams between plates survive.
            var steel = new Palette.Ramp(new Color(0.38f, 0.39f, 0.42f), lift: 0.38f, shade: 0.36f, line: 0.80f)
                .WithShadowsBelowLight(0.50f, 0.32f);
            // Brown strap leather, lit toward a warm tan so it never greys out.
            var leather = new Palette.Ramp(new Color(0.48f, 0.31f, 0.19f), shade: 0.34f)
                .WithHighlightsToward(new Color(0.84f, 0.62f, 0.42f), 0.30f);
            // The jerkin and the coat: a dark grey leather, a step above the undersuit's black so
            // the legs between the skirts read as something else.
            var jerkin = new Palette.Ramp(new Color(0.28f, 0.28f, 0.30f), lift: 0.24f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.52f, 0.34f);
            // Grey-brown fur, lit at the tips.
            var fur = new Palette.Ramp(new Color(0.47f, 0.42f, 0.37f), lift: 0.30f, shade: 0.34f, line: 0.78f);
            const float farF = 0.85f;

            items.Add(Defends(Make("talon_jerkin", "Talon Jerkin", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.talon", TalJerkinRows,
                       TalWithSteel(Palette.Of(jerkin, leather, fur), steel),
                       0f, FieldCentreCells(TalJerkinBottom, TalJerkinTop), ppu: BodyPpu)),
                // One arm in plate to turn a blade aside, the other free.
                DefensiveAbility.ParryStance));

            var pauldron = Make("talon_pauldron", "Talon Pauldron", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.talon", TalPauldronRows,
                       Palette.Of(steel.Scaled(farF), leather.Scaled(farF), fur.Scaled(farF)),
                       ShoulderX + TalPauldronCentreCells, TalPauldronY, ppu: BodyPpu),
                Pixels(RigLayer.Shoulders, "gear.pauldron.talon.fur", TalFurShoulderRows, Palette.Of(steel, leather, fur),
                       -ShoulderX - TalFurShoulderCentreCells, TalFurShoulderY, ppu: BodyPpu));
            pauldron.Lopsided = true;
            items.Add(pauldron);

            var gauntlet = Make("talon_gauntlet", "Talon Gauntlet", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesBack, "gear.gloves.talon", TalPlatedArmRows,
                       Palette.Of(steel.Scaled(farF), leather.Scaled(farF)),
                       FieldCentreCells(TalPlatedArmMin, TalPlatedArmMax),
                       FieldCentreCells(TalPlatedArmBottom, TalPlatedArmTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesFront, "gear.gloves.talon.bare", TalBareArmRows,
                       Palette.Of(leather, BodyLook.GearSkin, steel),
                       0f, FieldCentreCells(TalBareArmBottom, TalBareArmTop), ppu: BodyPpu));
            gauntlet.ShowsSkin = true;
            gauntlet.Lopsided = true;
            items.Add(gauntlet);

            items.Add(Make("talon_belt", "Talon Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.talon", TalBeltRows, Palette.Of(leather, steel, jerkin),
                       0f, FieldCentreCells(TalBeltBottom, TalBeltTop), ppu: BodyPpu)));

            items.Add(Make("talon_skirts", "Talon Skirts", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.Tasset, "gear.legs.talon", TalSkirtRows, Palette.Of(jerkin, leather, steel),
                       0f, FieldCentreCells(TalSkirtBottom, TalSkirtTop), ppu: BodyPpu)));

            items.Add(Make("talon_greaves", "Talon Greaves", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.talon", TalGreaveFront, Palette.Of(steel, leather, jerkin),
                       0f, FieldCentreCells(TalGreaveBottom, TalGreaveTop), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.talon.dark", TalGreaveBack,
                       Palette.Of(steel.Scaled(farF), leather.Scaled(farF), jerkin.Scaled(farF)),
                       0f, FieldCentreCells(TalGreaveBottom, TalGreaveTop), ppu: BodyPpu)));

            var coat = Make("talon_coat", "Talon Coat", GearSlot.Back, LootTier.Diamond, 0f,
                Pixels(RigLayer.Back, "gear.back.talon", TalCoatRows, Palette.Of(jerkin, leather, fur),
                       0f, FieldCentreCells(TalCoatBottom, TalCoatTop), ppu: BodyPpu));
            // Hinged at the neck, not the top of the fur standing up behind the head.
            coat.CapeHingeCells = PrimitiveCharacterRig.Proportions.Cells(TalCoatTop + PixelSprite.OutlineCanvasPadFor(BodyPpu))
                                  - PrimitiveCharacterRig.Proportions.NeckYCells;
            // A coat, not a cape: the tails sway, they do not stream.
            coat.CapeSwingScale = 0.30f;
            items.Add(coat);

            // The jerkin is the mass; the leather is straps, bracers and the pauldron's.
            Dyeable(items, "talon_", DyeChannel.Of("Jerkin", DyeMaterial.Cloth, jerkin, farF),
                    DyeChannel.Of("Leather", DyeMaterial.Leather, leather, farF));
        }

        static Dictionary<char, Color> TalWithSteel(Dictionary<char, Color> pal, Palette.Ramp steel)
        {
            pal[TalSteelDark] = steel.Dark;
            pal[TalSteelBase] = steel.Base;
            pal[TalSteelLight] = steel.Light;
            pal[TalSteelGlow] = steel.Glow;
            return pal;
        }

        static char TalSteel(int tone) => tone switch
        {
            <= 2 => TalSteelDark, 3 => TalSteelBase, 4 => TalSteelLight, _ => TalSteelGlow,
        };

        // ------------------------------------------------------------------ shared shapes

        /// <summary>Even-odd point-in-polygon.</summary>
        static bool TalInPoly(float x, float y, Vector2[] p)
        {
            bool inside = false;
            for (int i = 0, j = p.Length - 1; i < p.Length; j = i++)
                if ((p[i].y > y) != (p[j].y > y)
                    && x < (p[j].x - p[i].x) * (y - p[i].y) / (p[j].y - p[i].y) + p[i].x)
                    inside = !inside;
            return inside;
        }

        /// <summary>A stable 0..1 hash of two integers.</summary>
        static float TalHash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / 4294967296f;
            }
        }

        /// <summary>
        /// How far a TUFT hangs below a fur edge at <paramref name="x"/>: a row of points, each its
        /// own length. A level edge read as a towel, not fur.
        /// </summary>
        static float TalTuft(float x, int seed, float period = 3.5f)
        {
            float k = Mathf.Floor(x / period), f = x / period - k;
            float amp = 1.0f + 1.8f * TalHash((int)k, seed);
            return amp * (1f - Mathf.Abs(f * 2f - 1f));
        }

        /// <summary>
        /// Fur's tone: lit along its top, a dark root above the tufts, and short strands - two
        /// texels, staggered by column - a step either side of the base. Kept to one step: a
        /// stronger strand read as noise.
        /// </summary>
        static int TalFurTone(int ix, int iy, float fromTop, float fromBottom, int side)
        {
            int tone = 3 + side;
            if (fromTop < 1.5f) tone++;
            else if (fromBottom < 1.2f) tone--;
            float n = TalHash(ix, (iy + (ix & 1)) >> 1);
            if (n < 0.16f) tone--;
            else if (n > 0.86f) tone++;
            return Mathf.Clamp(tone, 1, 5);
        }

        // ------------------------------------------------------------------ the jerkin (Torso)
        //
        // Torso-local. The FUR collar wraps the neck and runs out over both shoulders, open at the
        // front; under it a dark V PLASTRON hangs to a point, rimmed in light; the jerkin below
        // closes down the front on steel hooks; two brown STRAPS run from under the fur to the
        // belt, a buckle on each.

        const int TalJerkinTop = 46, TalJerkinBottom = 6, TalJerkinHalf = 18;

        static string[] _talJerkinRows;
        static string[] TalJerkinRows => _talJerkinRows ??=
            PaintField(-TalJerkinHalf, TalJerkinHalf, TalJerkinBottom, TalJerkinTop, TalJerkinTexel);

        static bool TalPlastronIn(float x, float y)
        {
            float ax = Mathf.Abs(x);
            return y < 36f && y >= 19.5f && ax <= Mathf.Min(6.5f, (y - 19.5f) * 0.5f);
        }

        /// <summary>The body's own +X edge at torso height <paramref name="y"/> (the torso grid's
        /// lit side, which narrows to the waist) - where the far, plated arm starts to show.</summary>
        static float TalBodyEdge(float y) => y >= 22f ? 11.5f : y >= 16f ? 10.5f : y >= 8f ? 9.5f : 8f;

        /// <summary>The fur collar's lower edge, tufted: lowest over the shoulders. The head covers
        /// everything above the chin (40), so the collar has to hang well below it to show at all -
        /// the first pass stopped at 33 and read as a thin grey ruff.</summary>
        static float TalCollarBottom(float x) => 30.5f - Mathf.Max(0f, Mathf.Abs(x) - 7f) * 0.3f - TalTuft(x, 11);

        static char TalJerkinTexel(int ix, int iy)
        {
            const string J = "ksdblh", L = "KSDBLH", F = "123456";
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 6f, -7f);

            // ---- the fur collar ----
            float furTop = 46f - Mathf.Max(0f, ax - 11f) * 1.1f;
            float furBottom = TalCollarBottom(x);
            float opening = 2f + Mathf.Max(0f, 40f - y) * 0.42f;       // meeting at the throat, parting down
            if (ax >= opening && ax <= 17.5f && y < furTop && y >= furBottom)
                return RampChar(F, TalFurTone(ix, iy, furTop - y, y - furBottom, LitSide(x, 9f, -10f)));

            // ---- the plastron ----
            if (TalPlastronIn(x, y))
            {
                if (VerRim(TalPlastronIn, x, y)) return RampChar(J, y > 33f ? 5 : 4);
                if (ix == -1) return RampChar(J, 2);                                    // the ridge's shade
                if (ix == 0) return RampChar(J, 5);                                     // the ridge
                if ((ix == -4 || ix == 3) && iy == 35) return TalSteel(5);              // rivets
                return RampChar(J, 3 + LitSide(x, 1f, -1f));
            }

            // ---- the jerkin ----
            // On the far side it keeps to the body's own edge: the plated arm hangs half behind the
            // torso, and a jerkin a texel wider (and its outline) hid all but its spikes. (Found
            // with the BARE arm there - it was the far arm first - which kept one texel of skin.)
            float top = 40.5f - Mathf.Max(0f, ax - 6f) * 0.45f;
            float half = x < 0f ? (iy >= 26 ? 13.5f : 13f) : TalBodyEdge(y);
            if (y >= top || ax > half) return '.';

            // the straps, from the shoulders to the belt, a buckle on each at the chest
            float strap = ax - 8.5f;
            if (strap >= -1.25f && strap < 1.25f)
            {
                if (iy >= 27 && iy < 31)
                {
                    bool frame = iy == 27 || iy == 30 || strap < -0.25f;
                    if (frame) return TalSteel(iy == 30 ? 5 : 3 + Mathf.Max(side, 0));
                }
                return RampChar(L, (strap > 0.25f ? 4 : 3) + (side < 0 ? -1 : 0));
            }
            if (Mathf.Abs(strap) < 1.75f) return RampChar(J, 1);                       // its shadow

            // the closure down the front: a seam, a hook either side of it. A clasp ACROSS the seam
            // under the plastron's point made a cross on the chest (no emblems - the user's call
            // on Nocturne and Orichalc).
            if (y < 20f)
            {
                if (iy == 15 && (ix == -3 || ix == 2)) return TalSteel(ix == 2 ? 5 : 3);
                if (ix == -1) return RampChar(J, 1);
                if (ix == 0) return RampChar(J, 4);
            }

            // shaded round the body, a seam either side of the front
            if (ax > 4.5f && ax < 5.5f && y < 30f) return RampChar(J, 2);
            if (y < 9f) return RampChar(J, 2 + Mathf.Max(side, 0));                     // under the belt
            return RampChar(J, 3 + side);
        }

        // ------------------------------------------------------------------ the pauldron (Shoulders)
        //
        // A FAN of jagged steel blades along the far shoulder's ARC. Each blade rises from its own
        // point on the shoulder, from beside the neck to the outside of the arm, and each points a
        // little lower than the last: the first stands up past the cheek, the last points out and
        // down the arm. Each is a long plate folded along its length (a lit upper facet, a darker
        // lower one), its lower edge cut with a BARB short of the tip - the references' plates are
        // ragged, never clean leaves. Each overlaps the one below it, as lames do, and throws a
        // shadow on it. A plate hugging the shoulder fills in behind their roots.
        //
        // The first pass sprang every blade from one point under a round cap, and read as a
        // flower: the references' plates are spread along the shoulder, not radiating from a hub.
        //
        // In (outward, height above the chin) texels from the joint; the grid is drawn for +X, so
        // the blades point INTO the light and their shadows fall down and inward.
        // A CAP, not HangsAlongArm: it fans out from the shoulder rather than hanging down the arm.

        const int TalPauldronMin = -6, TalPauldronMax = 30, TalPauldronRowCount = 42;
        /// <summary>How far the grid's top stands above the chin - the top blade's reach.</summary>
        const int TalPauldronRise = 22;
        static float TalPauldronCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((TalPauldronMin + TalPauldronMax) * 0.5f);
        static float TalPauldronY
            => PlateY(TalPauldronRows) + PrimitiveCharacterRig.Proportions.Cells(TalPauldronRise);

        /// <summary>The blades, FRONT (top) first: (root outward, root height, angle from straight
        /// out in degrees, length, half-width at the root).</summary>
        static readonly (float o, float h, float deg, float len, float half)[] TalBlades =
        {
            (6f, 2f, 80f, 19f, 5.4f),
            (8.5f, -1f, 50f, 20f, 5.4f),
            (10f, -4.5f, 20f, 18f, 5.2f),
            (10f, -8f, -12f, 14f, 4.6f),
        };

        static Vector2[][] _talBladePolys;
        static Vector2[][] TalBladePolys => _talBladePolys ??= BuildTalBlades();

        static Vector2[][] BuildTalBlades()
        {
            var polys = new Vector2[TalBlades.Length][];
            for (int i = 0; i < TalBlades.Length; i++)
            {
                var b = TalBlades[i];
                float a = b.deg * Mathf.Deg2Rad, L = b.len, H = b.half;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var n = new Vector2(-d.y, d.x);                         // the upper side
                var origin = new Vector2(b.o, b.h);
                // A straight upper edge to the tip, then back along the lower edge: a notch, the
                // barb's point, and the root.
                var uv = new[]
                {
                    new Vector2(-4f, H), new Vector2(L, 0f),
                    new Vector2(0.72f * L, -0.28f * H), new Vector2(0.66f * L, -1.0f * H),
                    new Vector2(0.35f * L, -0.95f * H), new Vector2(-4f, -H),
                };
                var poly = new Vector2[uv.Length];
                for (int k = 0; k < uv.Length; k++) poly[k] = origin + d * uv[k].x + n * uv[k].y;
                polys[i] = poly;
            }
            return polys;
        }

        static string[] _talPauldronRows;
        static string[] TalPauldronRows => _talPauldronRows ??=
            PaintField(TalPauldronMin, TalPauldronMax, 0, TalPauldronRowCount, (ix, iy) =>
            {
                int r = TalPauldronRowCount - 1 - iy;                 // outward is +X
                return TalPauldronTexel(ix + 0.5f, TalPauldronRise - r - 0.5f);
            });

        /// <summary>The plate under the blades' roots, hugging the top of the shoulder.</summary>
        static bool TalShoulderPlateIn(float o, float h)
        {
            float dx = (o - 4f) / 9f, dy = (h + 4f) / 7.5f;
            return dx * dx + dy * dy < 1f;
        }

        /// <param name="o">Outward from the joint, texels.</param>
        /// <param name="h">Height above the chin, texels.</param>
        static char TalPauldronTexel(float o, float h)
        {
            const string S = "ksdblh";
            var polys = TalBladePolys;

            // ---- the blades, front first ----
            for (int i = 0; i < polys.Length; i++)
            {
                if (!TalInPoly(o, h, polys[i])) continue;
                var b = TalBlades[i];
                float a = b.deg * Mathf.Deg2Rad;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var n = new Vector2(-d.y, d.x);
                var p = new Vector2(o, h);

                // the shadow of the blade above, falling down and inward
                for (int j = 0; j < i; j++)
                    if (TalInPoly(o + 1f, h + 1.5f, polys[j])) return RampChar(S, 1);

                var up = p + n * 1.1f;
                var down = p - n * 1.1f;
                if (!TalInPoly(up.x, up.y, polys[i])) return RampChar(S, 5);               // the lit edge
                if (!TalInPoly(down.x, down.y, polys[i])) return RampChar(S, 1);           // the under edge
                float v = Vector2.Dot(p - new Vector2(b.o, b.h), n);
                int tone = v > 0.3f ? 4 : v > -0.8f ? 3 : 2;                                // the fold
                return RampChar(S, Mathf.Clamp(tone, 1, 5));
            }

            // ---- the plate behind their roots ----
            if (TalShoulderPlateIn(o, h))
            {
                foreach (var poly in polys)
                    if (TalInPoly(o + 1f, h + 1.5f, poly)) return RampChar(S, 1);
                if (!TalShoulderPlateIn(o, h + 1f)) return RampChar(S, 4);
                return RampChar(S, h > -6f ? 3 : 2);
            }
            return '.';
        }

        // ---- the near shoulder: fur, and a strap over it ----
        //
        // A mound of the collar's fur over the bare arm's shoulder, its lower edge tufted, and a
        // brown strap running over it with a steel buckle. (Outward, height above the chin), drawn
        // for -X, so its lit side is the inner one.

        const int TalFurShoulderMin = -6, TalFurShoulderMax = 14, TalFurShoulderRowsCount = 20;
        const int TalFurShoulderRise = 6;
        static float TalFurShoulderCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((TalFurShoulderMin + TalFurShoulderMax) * 0.5f);
        static float TalFurShoulderY
            => PlateY(TalFurShoulderRows) + PrimitiveCharacterRig.Proportions.Cells(TalFurShoulderRise);

        static string[] _talFurShoulderRows;
        static string[] TalFurShoulderRows => _talFurShoulderRows ??=
            PaintField(TalFurShoulderMin, TalFurShoulderMax, 0, TalFurShoulderRowsCount, (ix, iy) =>
            {
                int o = TalFurShoulderMin + TalFurShoulderMax - 1 - ix;    // outward is -X
                int r = TalFurShoulderRowsCount - 1 - iy;
                return TalFurShoulderTexel(ix, r, o + 0.5f, TalFurShoulderRise - r - 0.5f);
            });

        static char TalFurShoulderTexel(int ix, int r, float o, float h)
        {
            const string S = "ksdblh", L = "KSDBLH", F = "123456";
            float top = 5f - ((o - 2f) / 10f) * ((o - 2f) / 10f) * 9f;
            float bottom = -6f + Mathf.Max(0f, o - 4f) * 0.3f - TalTuft(o, 23);
            if (o < -5.5f || h >= top || h < bottom) return '.';

            // the strap, front to back over the fur, a buckle where it turns down
            if (o >= 0f && o < 2.5f)
            {
                if (h < -1f && h >= -4f)
                    return h < -3f || h >= -2f || o < 1f ? RampChar(S, h >= -2f ? 5 : 3) : RampChar(L, 2);
                return RampChar(L, o < 1f ? 4 : 3);
            }
            if (o >= -0.8f && o < 3.3f) return RampChar(F, 1);                               // its shadow in the fur
            return RampChar(F, TalFurTone(ix, -r, top - h, h - bottom, o > 7f ? -1 : o < -2f ? 1 : 0));
        }

        // ------------------------------------------------------------------ the plated arm (Gloves, far)
        //
        // ARM-local: 0 at the shoulder, the elbow cut at -14, the wrist at -22, the hand below.
        // The FAR arm, so OUTWARD is +X - but the shapes below are written with outward toward -x
        // (the arm was the near one first) and sampled MIRRORED, x = -X; only the tones that
        // answer to the light read the real X. Every plate is spiked on its OUTER side only:
        // inward the arm is behind the torso, where a spike would only be hidden.
        //
        //   the REREBRACE   two lames, each with its outer lower corner drawn out to a point, then
        //                   one plain plate to the elbow. Row -12 is repeated by the rig to
        //                   lengthen the upper arm, so nothing but plain plate may sit there
        //   the COUTER      below the cut so it bends with the forearm: a dome, and a spike off it
        //                   pointing out and down - the arm's biggest point
        //   the VAMBRACE    two lames flaring toward the elbow, each lifting a point at its outer
        //                   top corner
        //   the GAUNTLET    a flared cuff, plated knuckles, and four CLAWS, longer than fingers
        //
        // (A red quilted sleeve in the crook of the elbow, after the references, read as blood at
        // this size and was dropped.)

        /// <summary>The grid's span in REAL arm-local X - the spikes reach out toward +X.</summary>
        const int TalPlatedArmTop = 0, TalPlatedArmBottom = -38, TalPlatedArmMin = -8, TalPlatedArmMax = 14;

        static string[] _talPlatedArmRows;
        static string[] TalPlatedArmRows => _talPlatedArmRows ??=
            PaintField(TalPlatedArmMin, TalPlatedArmMax, TalPlatedArmBottom, TalPlatedArmTop, TalPlatedArmTexel);

        /// <summary>A lame's spike: off the outer side, its tip <paramref name="reach"/> past the
        /// arm, <paramref name="drop"/> below the lame's lower edge.</summary>
        static bool TalLameSpikeIn(float x, float y, float edge, float reach, float drop)
            => TalInPoly(x, y, new[]
            {
                new Vector2(-3.5f, edge + 3f), new Vector2(-3.5f, edge - 0.5f),
                new Vector2(-4.5f - reach, edge - drop),
            });

        static readonly Vector2[] TalCouterSpike =
        {
            new(-1f, -14.5f), new(-4.5f, -15f), new(-13.5f, -22.5f), new(-3f, -19.5f),
        };

        static bool TalClawIn(float x, float y, float x0, float tipY)
        {
            if (y > -28.5f || y < tipY) return false;
            float t = (-28.5f - y) / (-28.5f - tipY);
            float cx = x0 + t * t * 1.4f;                         // curling in toward the palm
            float halfW = 0.95f * (1f - t) + 0.35f;
            return Mathf.Abs(x - cx) < halfW;
        }

        static char TalPlatedArmTexel(int ix, int iy)
        {
            const string S = "ksdblh", L = "KSDBLH";
            // x: the shapes' own frame, outward toward -x. wx: real arm-local X, for the light.
            float wx = ix + 0.5f, x = -wx, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(wx, 1.5f, -2.5f);

            // ---- the rerebrace ----
            if (iy >= -14)
            {
                foreach (float edge in new[] { -4f, -8.5f })
                    if (y < edge + 3f && y > edge - 3.5f && x < -3.5f && TalLameSpikeIn(x, y, edge, 5.5f, 3f))
                        return RampChar(S, y > edge ? 4 : 2);
                if (ax > 4.5f) return '.';
                // the crook of the elbow, in shadow under the plate's hem
                if (iy == -14 && x > 0f) return RampChar(L, 1);
                foreach (float edge in new[] { -4f, -8.5f })
                {
                    if (y < edge + 1f && y > edge) return RampChar(S, 5);                  // the lame's lit hem
                    if (y < edge && y > edge - 1f) return RampChar(S, 1);                  // its shadow
                }
                return RampChar(S, 3 + side);
            }

            // ---- the couter: its spike, then the dome ----
            if (TalInPoly(x, y, TalCouterSpike))
            {
                bool lit = TalInPoly(x + 0.8f, y - 1.2f, TalCouterSpike) && !TalInPoly(x - 0.5f, y + 1.2f, TalCouterSpike);
                return RampChar(S, lit ? 4 : y < -19f && x > -6f ? 1 : 2);
            }
            {
                float dx = (x + 0.5f) / 4.5f, dy = (y + 17.5f) / 3.2f;
                float r2 = dx * dx + dy * dy;
                if (r2 < 1f)
                {
                    if (r2 > 0.62f && y < -17.5f) return RampChar(S, 1);
                    if (dx * dx * 2f + (dy + 0.4f) * (dy + 0.4f) * 2f < 0.5f) return RampChar(S, 5);
                    return RampChar(S, 3 + (wx > 0f ? 1 : 0));
                }
            }
            if (iy == -15 && ax <= 4.5f) return RampChar(L, 1);                            // the crook again

            // ---- the vambrace: two flared lames ----
            if (iy >= -24)
            {
                foreach (var (topY, tip) in new[] { (-18.5f, -9f), (-21.5f, -8f) })
                {
                    // the flare, lifting to a point at the outer top corner
                    if (y <= topY + 1.5f && y > topY - 2.5f && x < -3.5f
                        && TalInPoly(x, y, new[] { new Vector2(-3.5f, topY - 2.5f), new Vector2(-3.5f, topY),
                                                   new Vector2(tip, topY + 1.5f), new Vector2(-4.6f, topY - 2.5f) }))
                        return RampChar(S, y > topY - 0.5f ? 4 : 2);
                }
                float half = 4.5f - (-y - 17f) * 0.06f;
                if (ax > half) return '.';
                if (iy == -22) return RampChar(L, 3 + side);                                // a strap round it
                if (iy == -19 || iy == -16) return RampChar(S, 5);                          // lit lame edges
                if (iy == -20 || iy == -17) return RampChar(S, 1);
                return RampChar(S, 3 + side);
            }

            // ---- the gauntlet: the cuff ----
            if (iy >= -27)
            {
                if (x < -4f && y > -26.5f && TalInPoly(x, y, new[] { new Vector2(-4f, -26.5f), new Vector2(-4f, -24f),
                                                                       new Vector2(-8.5f, -23f) }))
                    return RampChar(S, 2);
                if (ax > 5f) return '.';
                if (iy == -25) return RampChar(S, 5);
                if (iy == -27) return RampChar(S, 1);
                return RampChar(S, 3 + side);
            }

            // ---- the knuckles ----
            if (iy >= -29)
            {
                if (ax > 4.5f) return '.';
                return RampChar(S, iy == -28 ? 4 + Mathf.Max(side, 0) : 2 + Mathf.Max(side, 0));
            }

            // ---- the claws ----
            var claws = new (float x0, float tip)[] { (-3.2f, -34f), (-1.1f, -35.5f), (1.0f, -35f), (3.0f, -33f) };
            foreach (var (x0, tip) in claws)
                if (TalClawIn(x, y, x0, tip))
                {
                    float t = (-28.5f - y) / (-28.5f - tip);
                    float cx = x0 + t * t * 1.4f;
                    return RampChar(S, x < cx ? 4 : 2);                     // lit toward +X
                }
            return '.';
        }

        // ------------------------------------------------------------------ the bare arm (Gloves, near)
        //
        // ARM-local, outward -X (the near arm). SKIN, in BodyLook.GearSkin, repainted in the
        // wearer's own tone - a bare shoulder and upper arm, the elbow, then a long brown BRACER
        // to the wrist, strapped twice, and a FINGERLESS glove: leather over the back of the
        // hand, the fingers bare. Row -12 is plain skin (the rig repeats it).

        const int TalBareArmTop = 0, TalBareArmBottom = -34, TalBareArmHalf = 8;

        static string[] _talBareArmRows;
        static string[] TalBareArmRows => _talBareArmRows ??=
            PaintField(-TalBareArmHalf, TalBareArmHalf, TalBareArmBottom, TalBareArmTop, TalBareArmTexel);

        static char TalBareArmTexel(int ix, int iy)
        {
            const string L = "ksdblh", K = "KSDBLH";
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            // ---- the upper arm and elbow, bare ----
            if (iy >= -17)
            {
                float half = iy >= -10 && iy < -3 ? 4.5f : 4f;
                if (ax > half) return '.';
                if (x > 1.5f && x < 3f && iy >= -9 && iy < -3) return RampChar(K, 4);       // the bicep
                if (x < -2.5f) return RampChar(K, 2);
                return RampChar(K, 3 + side);
            }

            // ---- the bracer ----
            if (iy >= -25)
            {
                float half = 5f - (-17f - y) * 0.06f;
                if (ax > half) return '.';
                if (iy == -18) return RampChar(L, 4 + Mathf.Max(side, 0));                 // its lit top
                if (iy == -20 || iy == -23)
                    return ix == 2 || ix == 3 ? TalSteelChar(ix == 2 ? 4 : 3) : RampChar(L, 1);   // the straps
                return RampChar(L, 3 + side);
            }

            // ---- the glove, fingerless ----
            if (iy >= -29)
            {
                if (ax > 4.5f) return '.';
                if (iy == -28) return RampChar(L, 4 + Mathf.Max(side, 0));                 // the knuckles
                return RampChar(L, 2 + Mathf.Max(side, 0));
            }
            // the fingers, bare
            {
                float half = iy < -32 ? 3.5f : 4f;
                if (ax > half) return '.';
                if (ix == -2 || ix == 0 || ix == 2) return RampChar(K, 2);                  // between them
                return RampChar(K, iy == -30 ? 4 : 3 + Mathf.Max(side, 0));
            }
        }

        /// <summary>Steel in the bare arm's third ramp (digits).</summary>
        static char TalSteelChar(int tone) => RampChar("123456", tone);

        // ------------------------------------------------------------------ the belt
        //
        // Torso-local, on BeltY's band and below it: WIDE brown leather with a big SQUARE steel
        // buckle and a diamond stud in its middle; under it a second belt slung a little lower on
        // the -X side, a smaller buckle off centre; and a POUCH hanging from it in front of the +X
        // hip. Both belts stop at the body's own edge on the far side: the clawed hand hangs right
        // there, and a belt to the usual width (and the pouch out on the hip) hid it.

        const int TalBeltTop = 14, TalBeltBottom = -8, TalBeltHalf = 18;

        static string[] _talBeltRows;
        static string[] TalBeltRows => _talBeltRows ??=
            PaintField(-TalBeltHalf, TalBeltHalf, TalBeltBottom, TalBeltTop, TalBeltTexel);

        static char TalBeltTexel(int ix, int iy)
        {
            const string L = "ksdblh", S = "KSDBLH";
            float x = ix + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 10f, -11f);

            // ---- the big buckle ----
            if (ix >= -5 && ix < 5 && iy >= 3 && iy < 13)
            {
                bool frame = ix <= -4 || ix >= 3 || iy <= 4 || iy >= 11;
                if (frame) return RampChar(S, iy >= 11 ? 5 : iy <= 4 ? 2 : ix >= 3 ? 3 : 4);
                float dd = Mathf.Abs(x) + Mathf.Abs(iy + 0.5f - 8f);
                if (dd <= 2f) return RampChar(S, dd <= 1f ? 5 : 3);                       // the diamond
                return RampChar(L, 1);
            }

            // ---- the pouch ----
            if (ix >= 4 && ix < 9 && iy >= -7 && iy < 2)
            {
                if (iy >= -1) return ix == 6 && iy == -1 ? RampChar(S, 5) : RampChar(L, iy == 1 ? 4 : 3);  // the flap, its stud
                if (iy == -2) return RampChar(L, 1);
                return RampChar(L, (ix >= 7 ? 4 : 3) - (iy == -7 ? 1 : 0));
            }
            float far = TalBodyEdge(iy + 0.5f) + 0.5f;

            // ---- the upper belt ----
            if (iy >= 5 && iy < 11 && x >= -15.5f && x <= far)
                return RampChar(L, iy switch { 10 => 4, 5 => 1, _ => 3 } + side);

            // ---- the lower belt, slung ----
            float lift = -x * 0.08f;
            float yc = iy + 0.5f + lift;
            if (yc >= 1.5f && yc < 4.5f && x >= -15f && x <= far)
            {
                if (ix >= -10 && ix < -7) return RampChar(S, ix == -10 ? 4 : 3);           // its buckle
                return RampChar(L, yc >= 3.5f ? 4 : yc < 2.5f ? 2 : 3);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the skirts (Legs)
        //
        // TORSO-local on RigLayer.Tasset: the coat's front skirts, one over the OUTSIDE of each
        // thigh, open in front so the knee plates' points show between them. Hemmed lower on the
        // outside; brown piping down the front edge and along the hem.

        const int TalSkirtTop = 4, TalSkirtBottom = -24, TalSkirtHalf = 20;

        static string[] _talSkirtRows;
        static string[] TalSkirtRows => _talSkirtRows ??=
            PaintField(-TalSkirtHalf, TalSkirtHalf, TalSkirtBottom, TalSkirtTop, TalSkirtTexel);

        static char TalSkirtTexel(int ix, int iy)
        {
            const string J = "ksdblh", L = "KSDBLH";
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 10f, -11f);

            float inner = 9f + (2f - y) * 0.05f, outer = 15.5f + (2f - y) * 0.16f;
            if (y >= 3f || ax < inner || ax > outer) return '.';
            float t = (ax - inner) / (outer - inner);
            float hem = -9f - t * 6f;
            if (y < hem) return '.';

            if (y < hem + 1f || ax < inner + 1.2f) return RampChar(L, ax < inner + 1.2f && y > hem + 1f ? 4 : 3);  // piping
            if (y < -4f && Mathf.Abs(t - 0.55f) < 0.08f) return RampChar(J, 2);           // a fold
            return RampChar(J, (t > 0.85f ? 2 : 3) + side);
        }

        // ------------------------------------------------------------------ the greaves (Boots)
        //
        // LEG-local, art bottom on the sole, the knee at -24. The KNEE PLATE is the piece: a kite
        // rising to a point halfway up the thigh, ridged down its middle, a WING off its outer
        // side lifting to a second point; under it the shin in two lames that point down at the
        // middle, a leather strap at the ankle, and a plated foot. Two grids, mirrored by which
        // way is out (BootsFront's out is +X).

        const int TalGreaveTop = -8, TalGreaveBottom = -48, TalGreaveHalf = 10;

        static string[] _talGreaveFront, _talGreaveBack;
        static string[] TalGreaveFront => _talGreaveFront ??= TalGreave(outIsPlusX: true);
        static string[] TalGreaveBack => _talGreaveBack ??= TalGreave(outIsPlusX: false);

        static string[] TalGreave(bool outIsPlusX)
            => PaintField(-TalGreaveHalf, TalGreaveHalf, TalGreaveBottom, TalGreaveTop,
                          (ix, iy) => TalGreaveTexel(ix, iy, outIsPlusX));

        static readonly Vector2[] TalKneeKite =
        {
            new(0f, -11f), new(6f, -21.5f), new(5f, -26.5f), new(0f, -30.5f), new(-5f, -26.5f), new(-6f, -21.5f),
        };

        /// <summary>The wing, in (outward, y).</summary>
        static readonly Vector2[] TalKneeWing =
        {
            new(3.5f, -21f), new(9.5f, -16.5f), new(6.5f, -24.5f), new(3.5f, -25f),
        };

        static char TalGreaveTexel(int ix, int iy, bool outIsPlusX)
        {
            const string S = "ksdblh", L = "KSDBLH";
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            float u = outIsPlusX ? x : -x;
            int side = LitSide(x, 1.5f, -2f);

            // ---- the knee plate ----
            if (TalInPoly(x, y, TalKneeKite))
            {
                bool rimTop = !TalInPoly(x, y + 1f, TalKneeKite);
                if (rimTop) return RampChar(S, x >= 0f ? 5 : 4);
                if (!TalInPoly(x, y - 1f, TalKneeKite)) return RampChar(S, 1);
                if (ix == 0) return RampChar(S, 5);                                         // the ridge, lit
                if (ix == -1) return RampChar(S, 2);                                        // its shade
                return RampChar(S, (y > -22f ? 4 : 3) + (x > 0f ? 0 : -1));
            }
            if (TalInPoly(u, y, TalKneeWing))
            {
                bool lit = !TalInPoly(u, y + 1f, TalKneeWing);
                return RampChar(S, lit ? 4 : 2);
            }
            if (y > -27f) return '.';

            // ---- the foot ----
            if (y < -42f)
            {
                if (ax > 5.5f) return '.';
                if (y < -47f) return RampChar(S, 0);                                         // the sole
                if (iy == -43 || iy == -46) return RampChar(S, 4 + Mathf.Max(side, 0));    // lame tops
                return RampChar(S, 2 + Mathf.Max(side, 0));
            }
            // ---- the ankle strap ----
            if (y < -40f) return ax > 5f ? '.' : RampChar(L, iy == -41 ? 4 : 2);

            // ---- the shin, two lames pointing down at the middle ----
            if (ax > 4.5f) return '.';
            foreach (float edge in new[] { -34f, -40f })
            {
                float e = edge - (2.5f - ax) * 0.6f;
                if (y < e + 1f && y >= e) return RampChar(S, 1);                            // the lame's lower edge
                if (y < e + 2f && y >= e + 1f) return RampChar(S, 4);
            }
            if (ix == 0) return RampChar(S, 4);
            return RampChar(S, 3 + side);
        }

        // ------------------------------------------------------------------ the coat (Back)
        //
        // TORSO-local. Behind everything: a FUR MANTLE across the shoulders, wider than them and
        // standing up behind the head; the coat's back from the mantle to the waist; then two long
        // TAILS, split up the middle, flaring a little and cut lowest at their outer edges, to
        // mid-calf. From the front only the mantle's ends and the tails past the legs show - the
        // references' fur bulk at both shoulders and the dark flaps behind the legs.

        const int TalCoatTop = 48, TalCoatBottom = -40, TalCoatHalf = 26;

        static string[] _talCoatRows;
        static string[] TalCoatRows => _talCoatRows ??=
            PaintField(-TalCoatHalf, TalCoatHalf, TalCoatBottom, TalCoatTop, TalCoatTexel);

        static char TalCoatTexel(int ix, int iy)
        {
            const string J = "ksdblh", L = "KSDBLH", F = "123456";
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 10f, -11f);

            // ---- the mantle ----
            float mTop = 47.5f - (ax / 25f) * (ax / 25f) * 14f;
            float mBottom = 31f + Mathf.Max(0f, ax - 17f) * 0.9f - TalTuft(x, 37);
            if (ax <= 25f && y < mTop && y >= mBottom)
                return RampChar(F, TalFurTone(ix, iy, mTop - y, y - mBottom, LitSide(x, 12f, -13f)));

            // ---- the coat's back ----
            if (y >= -2f)
            {
                if (y >= 34f || ax > 13.5f) return '.';
                if (ix == 0 && y < 20f) return RampChar(J, 2);                              // the centre seam
                return RampChar(J, 3 + side);
            }

            // ---- the tails ----
            float outer = 13.5f + (-2f - y) * 0.14f;
            float slit = y < -6f ? 0.6f + (-6f - y) * 0.03f : 0f;
            if (ax > outer || ax < slit) return '.';
            float t = (ax - slit) / (outer - slit);
            float hem = -33f - t * 5f;
            if (y < hem) return '.';
            if (y < hem + 1f) return RampChar(L, 3);                                       // the piping
            if (slit > 0f && ax < slit + 1f) return RampChar(J, 1);                         // the slit's shade

            float fold = Mathf.Sin(t * Mathf.PI * 2.2f + 0.6f) * Mathf.Clamp01((-4f - y) / 20f);
            int tone = 3 + side;
            if (fold > 0.55f) tone++;
            else if (fold < -0.6f) tone--;
            return RampChar(J, Mathf.Clamp(tone, 1, 4));
        }
    }
}
