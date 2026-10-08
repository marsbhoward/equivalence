using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Geode: diamond plate over stone, black glass
    //
    // After the user's references (an armoured Mewtwo, sprite and painting, plus three crops the
    // user isolated - the helm, the chest and shoulder, the hip): ONLY the armour and the visor
    // are taken. The tail and the creature are left out.
    //
    // MATERIALS, the user's mapping:
    //
    //   the METAL       -> DIAMOND (uppercase), and ONLY the metal. Faceted (GeoDiamond) on top
    //                      of the plate's own form shading - one flat pale blue reads as ice.
    //   the body        -> STONE (lowercase). Where the reference shows the creature between
    //                      plates - the belly, the upper arms, the thighs - there is a grained
    //                      warm granite (GeoRock): the armour's underlayer, the diamond worn over it.
    //   the black parts -> BLACK GLASS (digits), Shadow's ramp and alphas (GeoGlassAlphas).
    //   the seams       -> BLACK (GeoSeam, the glass's solid line tone): the lines the reference
    //                      draws between two plates.
    //
    // THE FIRST PASS HAD IT THE OTHER WAY ROUND - stone plate with diamond in every gap - and the
    // user sent it back: stone carrying the whole figure read as rubble, not armour. The stone
    // is warm on purpose: Medusa's STONE status turns the character a cool neutral grey
    // (PixelSprite.Stone), and the underlayer must never read as a limb already petrified.
    //
    //   geode_helm       Head       a tall teardrop dome, a black glass CRESCENT visor low on it,
    //                               pale prongs either side, a sickle blade off the back (SealsHead)
    //   geode_pauldrons  Shoulders  a near-sphere in two plates, a black seam between
    //   geode_cuirass    Torso      a stone gorget; a faceted breastplate with a black centre seam
    //                               and a glass vent; a glass band under it; diamond flank plates
    //                               over a stone belly
    //   geode_gauntlets  Gloves     a stone upper arm; a bulbous forearm shell under a seamed lip;
    //                               a fist ending in three round knuckles
    //   geode_belt       Belt       a glass band, a glossy glass dome at the front, and under it a
    //                               fan of bevelled shards: a long tasset to the knee, two
    //                               shorter shards splaying out either side
    //   geode_greaves    Legs       a stone thigh, knee cop and shin
    //   geode_boots      Boots      sabatons, the toe split in two by a black seam
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    // Diamond, power 0, like every reference-built cosmetic set.
    public static partial class DemoGear
    {
        const string GeoStone = "ksdblh", GeoGem = "KSDBLH", GeoGlass = "123456";

        /// <summary>
        /// Shadow's alphas (<see cref="ShadowAlphas"/>), moved onto the digit set - except the
        /// LINE, which is SOLID. The visor is drawn on it, over the face, and the project blends
        /// in linear space, so an alpha reads far clearer over a bright face than over the dark
        /// floor the sword's numbers were set against: at the sword's 0.85, and still at 0.95,
        /// the skin came through and the crescent read as a brown line. The visor's glass is
        /// carried by its highlight instead; the bands, over the black undersuit, keep the
        /// see-through middle tones.
        /// </summary>
        static readonly (char C, float A)[] GeoGlassAlphas =
        {
            ('6', 0.92f), ('5', 0.82f), ('4', 0.56f), ('3', 0.66f), ('2', 0.28f), ('1', 1.00f),
        };

        static Dictionary<char, Color> GeoGlassed(Dictionary<char, Color> map)
        {
            foreach (var (c, a) in GeoGlassAlphas)
            {
                var col = map[c];
                col.a = a;
                map[c] = col;
            }
            return map;
        }

        static void AddGeode(List<GearItem> items)
        {
            // Matte - the stone never reaches its Glow (GeoRock) - so it sits behind the diamond.
            var stone = new Palette.Ramp(new Color(0.50f, 0.46f, 0.41f), lift: 0.26f, shade: 0.36f, line: 0.72f);
            // Shaded DEEP - an all-pale ramp lost the plates' form entirely.
            var gem = new Palette.Ramp(new Color(0.56f, 0.78f, 0.93f), lift: 0.48f, shade: 0.46f, line: 0.72f);
            // Shadow's steel (ShadowPal) - not black, so the glass keeps its tones.
            var glass = new Palette.Ramp(new Color(0.17f, 0.16f, 0.24f));
            var pal = GeoGlassed(Palette.Of(stone, gem, glass));
            var far = GeoGlassed(Palette.Of(stone.Scaled(0.82f), gem.Scaled(0.85f), glass.Scaled(0.85f)));

            var helm = Make("geode_helm", "Geode Helm", GearSlot.Head, LootTier.Diamond, 0f,
                Pixels(RigLayer.HeadArmor, "gear.head.geode", GeoHelmRows, pal,
                       FieldCentreCells(-GeoHelmHalf, GeoHelmHalf), FieldCentreCells(GeoHelmBottom, GeoHelmTop),
                       ppu: BodyPpu));
            // Enclosed - the dome covers the whole face and no hair escapes (seraph_helm's reason).
            helm.SealsHead = true;
            // From behind: the same field with the visor left out (GearItem.HelmBack).
            helm.HelmBack = Pixels(RigLayer.HeadArmor, "gear.head.geode.back", GeoHelmBackRows, pal,
                                   FieldCentreCells(-GeoHelmHalf, GeoHelmHalf), FieldCentreCells(GeoHelmBottom, GeoHelmTop),
                                   ppu: BodyPpu);
            items.Add(helm);

            items.Add(Make("geode_pauldrons", "Geode Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.geode", GeoPauldronNear, pal,
                       -ShoulderX - GeoPauldronCentreCells, GeoPauldronY, ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.geode.dark", GeoPauldronFar, far,
                       ShoulderX + GeoPauldronCentreCells, GeoPauldronY, ppu: BodyPpu)));

            items.Add(Defends(Make("geode_cuirass", "Geode Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.geode", GeoCuirassRows, pal,
                       0f, FieldCentreCells(GeoCuirassBottom, GeoCuirassTop), ppu: BodyPpu)),
                // The hardest thing there is, standing its ground.
                DefensiveAbility.Bulwark));

            items.Add(Make("geode_gauntlets", "Geode Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.geode", GeoGauntletRows, pal,
                       0f, FieldCentreCells(GeoGauntletBottom, GeoGauntletTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.geode.dark", GeoGauntletRows, far,
                       0f, FieldCentreCells(GeoGauntletBottom, GeoGauntletTop), ppu: BodyPpu)));

            items.Add(Make("geode_belt", "Geode Belt", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.geode", GeoBeltRows, pal,
                       0f, FieldCentreCells(GeoBeltBottom, GeoBeltTop), ppu: BodyPpu)));

            items.Add(Make("geode_greaves", "Geode Greaves", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.LegsFront, "gear.legs.geode", GeoGreaveRows, pal,
                       0f, FieldCentreCells(GeoGreaveBottom, GeoGreaveTop), ppu: BodyPpu),
                Pixels(RigLayer.LegsBack, "gear.legs.geode.dark", GeoGreaveRows, far,
                       0f, FieldCentreCells(GeoGreaveBottom, GeoGreaveTop), ppu: BodyPpu)));

            items.Add(Make("geode_boots", "Geode Boots", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.geode", GeoBootRows, pal,
                       0f, FieldCentreCells(GeoBootBottom, GeoBootTop), ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.geode.dark", GeoBootRows, far,
                       0f, FieldCentreCells(GeoBootBottom, GeoBootTop), ppu: BodyPpu)));

            // Crystal is the set's mass; the stone is its setting.
            Dyeable(items, "geode_", DyeChannel.Of("Crystal", DyeMaterial.Gem, gem, 0.85f),
                    DyeChannel.Of("Stone", DyeMaterial.Leather, stone, 0.82f));
        }

        // ------------------------------------------------------------------ the materials

        static uint GeoHash(int a, int b, int salt)
        {
            uint h = (uint)(a * 73856093) ^ (uint)(b * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return h;
        }

        /// <summary>
        /// A diamond texel at the plate's own FORM tone (from its shading), cut into facets:
        /// rhombi along both diagonals, one in four lifted a tone, so the facets' edges are where
        /// two tones meet rather than drawn lines (drawn, every plate came out plaid). Facets only
        /// ever go LIGHTER: nudged down as well, the dark ones read as camouflage blotches and
        /// broke the plate's form. A rare texel at Glow for the sparkle.
        /// </summary>
        static char GeoDiamond(int ix, int iy, int tone)
        {
            const float F = 7f;
            if (GeoHash(ix, iy, 91) % 31 == 0) return RampChar(GeoGem, 5);
            int u = Mathf.FloorToInt((ix + iy) / F), v = Mathf.FloorToInt((ix - iy) / F);
            int nudge = GeoHash(u, v, 17) % 4 == 0 ? 1 : 0;
            return RampChar(GeoGem, Mathf.Clamp(tone + nudge, 1, 5));
        }

        /// <summary>A seam between two plates: black, the glass's solid line tone.</summary>
        static char GeoSeam() => RampChar(GeoGlass, 0);

        /// <summary>A stone texel: <paramref name="tone"/> plus a fixed, hashed grain a tone up or
        /// down, held inside 1..4 - matte, and the grain is most of what says stone at this size.</summary>
        static char GeoRock(int ix, int iy, int tone)
        {
            uint h = GeoHash(ix, iy, 5) % 11;
            return RampChar(GeoStone, Mathf.Clamp(tone + (h == 0 ? 1 : h == 1 ? -1 : 0), 1, 4));
        }

        /// <summary>True when (px, py) lies on a straight bar from a root along a unit direction.</summary>
        static bool GeoBar(float px, float py, float rx, float ry, float dx, float dy, float len, float half,
                           out float along)
        {
            float ox = px - rx, oy = py - ry;
            along = ox * dx + oy * dy;
            float perp = -ox * dy + oy * dx;
            return along >= 0f && along <= len && Mathf.Abs(perp) <= half;
        }

        // ------------------------------------------------------------------ the helm (Head)
        //
        // HEAD-local texels (revenant_mask's frame): the chin at 0, the eyes' lowest row at 7.5
        // with the face turned toward +X, the hair topping out near 28. After the user's crop:
        //
        //   DOME      a tall TEARDROP - rounded below, rising to a point well above the head, the
        //             point leaning a little back (-X) with a small barb on its back edge. It
        //             covers the whole face: there is no face in the reference, only the dome.
        //   VISOR     a black glass CRESCENT low on the dome - a U, thick under the chin, its arms
        //             thinning up both sides. Not a band across the eyes.
        //   PRONGS    a pale one off each side, angled up and out.
        //   SICKLE    off the BACK of the head (-X): a thin blade rising from the jaw, clear of the
        //             dome, curving up past the prong to a point.
        //   FIN       a small dark blade behind the front prong.

        const int GeoHelmHalf = 26, GeoHelmBottom = -6, GeoHelmTop = 52;

        /// <summary>The dome's centre line and its half-widths (back side, front side) at a height.</summary>
        static bool GeoDomeAt(float y, out float cx, out float back, out float front)
        {
            cx = 0f; back = front = -1f;
            if (y < -4f || y > 50f) return false;
            if (y <= 14f)
            {
                float t = (14f - y) / 18f;
                back = front = 17f * Mathf.Pow(1f - t * t * t * t, 0.25f);
                return true;
            }
            float u = (y - 14f) / 36f;
            cx = -5f * u * u;
            front = 17f * Mathf.Pow(1f - u, 0.75f) + 0.6f;
            // The barb: the back edge steps out just below the point, then cuts straight back.
            back = front + (u > 0.68f && u < 0.80f ? (u - 0.68f) / 0.12f * 2.5f : 0f);
            return true;
        }

        static bool GeoDomeIn(float x, float y)
            => GeoDomeAt(y, out float cx, out float back, out float front) && x >= cx - back && x <= cx + front;

        static bool GeoEllipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            return dx * dx + dy * dy < 1f;
        }

        /// <summary>The crescent: between two ellipses, the inner one raised so the U is thick at
        /// its foot and thin up its arms, which stop short of the dome's widest point.</summary>
        static bool GeoCrescentIn(float x, float y)
            => GeoEllipse(x, y, 1f, 16f, 15.5f, 15.5f) && !GeoEllipse(x, y, 1f, 21.5f, 14f, 15f)
               && y < (x < 1f ? 22f : 19f);

        static string[] _geoHelmRows;
        static string[] GeoHelmRows => _geoHelmRows ??=
            PaintField(-GeoHelmHalf, GeoHelmHalf, GeoHelmBottom, GeoHelmTop, GeoHelmTexel);

        static string[] _geoHelmBackRows;
        static string[] GeoHelmBackRows => _geoHelmBackRows ??=
            PaintField(-GeoHelmHalf, GeoHelmHalf, GeoHelmBottom, GeoHelmTop, (ix, iy) => GeoHelmTexel(ix, iy, behind: true));

        static char GeoHelmTexel(int ix, int iy) => GeoHelmTexel(ix, iy, behind: false);

        /// <summary><paramref name="behind"/>: the helm from behind - no visor, nor the lights and lip
        /// that belong to it; prongs, sickle and fin stand where they do from the front.</summary>
        static char GeoHelmTexel(int ix, int iy, bool behind)
        {
            float x = ix + 0.5f, y = iy + 0.5f;

            // ---- the prongs, pale - over the dome's edge where they root ----
            if (GeoBar(x, y, -11f, 22f, -0.45f, 0.89f, 11f, 2.3f, out float a1)
                || GeoBar(x, y, 12f, 17f, 0.55f, 0.83f, 10f, 2.3f, out a1))
                return GeoDiamond(ix, iy, a1 > 6f ? 5 : 4);

            if (GeoDomeIn(x, y))
            {
                // ---- the visor ----
                if (!behind && GeoCrescentIn(x, y))
                {
                    // The glass: a lit streak along the foot of the U, and a lit inner rim on the
                    // light side.
                    // Both on the Glow tone, the densest after the line: anything clearer lets
                    // the face through as brown flecks.
                    if (iy == 2 && x > -9f && x < -3f) return RampChar(GeoGlass, 5);
                    if (x > 4f && !GeoCrescentIn(x, y + 1f)) return RampChar(GeoGlass, 5);
                    return RampChar(GeoGlass, 0);
                }

                // ---- the dome ----
                GeoDomeAt(y, out float cx, out float back, out float front);
                float n = x >= cx ? (x - cx) / front : (x - cx) / back;
                int tone = n > 0.35f ? 4 : n < -0.6f ? 2 : 3;
                if (GeoEllipse(x, y, cx + 4f, 31f, 3.5f, 6f)) tone = 5;            // the crown's light
                else if (!behind && GeoEllipse(x, y, 2f, 10f, 2f, 3.5f)) tone = 5;   // the brow's, over the visor
                if (!behind && !GeoEllipse(x, y, 1f, 16f, 15.5f, 15.5f) && y < 16f) tone--;  // the lip under the visor
                return GeoDiamond(ix, iy, tone);
            }

            // ---- the sickle, off the back of the head ----
            if (y >= 0f && y < 34f)
            {
                float t = y / 34f;
                float sx = -17f - 4.5f * Mathf.Sqrt(t), half = 2.4f * (1f - t) + 0.7f;
                if (Mathf.Abs(x - sx) < half) return GeoDiamond(ix, iy, x > sx ? 4 : 2);
            }

            // ---- the fin, dark, behind the front prong ----
            if (y >= 10f && y < 30f)
            {
                float t = (y - 10f) / 20f;
                float fx = 15f + 3.5f * t, half = 1.8f * (1f - t) + 0.6f;
                if (Mathf.Abs(x - fx) < half) return GeoDiamond(ix, iy, 1);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // A near-sphere, round underneath as well as on top (not Vermilion's dome cut level at its
        // hem), in two plates: a cap and a slightly narrower lower plate, parted by a black seam
        // curving down across the middle - an equator seen from a little above. Outward offsets,
        // mirrored for the far side; a cap standing above the shoulder line, like Vermilion's.

        const int GeoPauldronRows = 20, GeoPauldronMin = -6, GeoPauldronMax = 16;
        const int GeoPauldronRise = 4;
        static float GeoPauldronCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((GeoPauldronMin + GeoPauldronMax) * 0.5f);
        static float GeoPauldronY
            => PlateY(GeoPauldronNear) + PrimitiveCharacterRig.Proportions.Cells(GeoPauldronRise);

        static string[] _geoPauldronNear, _geoPauldronFar;
        static string[] GeoPauldronNear => _geoPauldronNear ??= GeoPauldron(outwardIsPlusX: false);
        static string[] GeoPauldronFar => _geoPauldronFar ??= GeoPauldron(outwardIsPlusX: true);

        static string[] GeoPauldron(bool outwardIsPlusX)
            => PaintField(GeoPauldronMin, GeoPauldronMax, 0, GeoPauldronRows, (ix, iy) =>
            {
                int o = outwardIsPlusX ? ix : GeoPauldronMin + GeoPauldronMax - 1 - ix;
                return GeoPauldronTexel(o, GeoPauldronRows - 1 - iy, outwardIsPlusX);
            });

        static char GeoPauldronTexel(int o, int r, bool outwardIsPlusX)
        {
            float oc = o + 0.5f, rc = r + 0.5f;
            float towardLight = outwardIsPlusX ? oc : -oc;
            int side = towardLight > 6f ? 1 : towardLight < -6f ? -1 : 0;

            float dx = (oc - 5f) / 10.5f, dy = (rc - 10f) / 10f;
            float d = dx * dx + dy * dy;
            if (d >= 1f) return '.';
            float seam = 11.5f + 1.8f * (1f - dx * dx);
            if (rc >= seam + 0.5f)
            {
                // The lower plate, a little narrower than the cap.
                float lx = (oc - 5f) / 9.6f;
                if (lx * lx + dy * dy >= 1f) return '.';
            }
            if (rc >= seam - 0.5f && rc < seam + 0.5f) return GeoSeam();

            if (GeoEllipse(oc, rc, 4f, 4.5f, 4.5f, 3.2f)) return GeoDiamond(o, r, 5);     // the crown's light
            if (d > 0.62f && (oc > 10f || rc > 13f)) return GeoDiamond(o, r, 2);         // turning away
            return GeoDiamond(o, r, 3 + side - (rc > seam ? 1 : 0));
        }

        // ------------------------------------------------------------------ the cuirass (Torso)
        //
        // Torso-local, after the user's chest crop. A faceted gorget - two faces split down a black
        // seam, one lit, in the diamond's darker tones (the reference's is dark metal). The
        // breastplate, parted down the sternum by a black seam, with a small dark glass VENT on
        // the shaded side (the reference's slot). A band of black GLASS under it. Then diamond
        // FLANK plates down each side, and between them the STONE belly - the reference's body.

        const int GeoCuirassTop = 44, GeoCuirassBottom = 6, GeoCuirassHalf = 16;

        static string[] _geoCuirassRows;
        static string[] GeoCuirassRows => _geoCuirassRows ??=
            PaintField(-GeoCuirassHalf, GeoCuirassHalf, GeoCuirassBottom, GeoCuirassTop, GeoCuirassTexel);

        static float GeoPecBottom(float ax) => 25f + (ax / 13f) * (ax / 13f) * 3f;

        static char GeoCuirassTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 7f, -8f);

            // ---- the gorget ----
            float collar = 6f + (y - 36f) * 0.45f;
            if (y >= 36f && ax <= collar)
            {
                if (ax < 0.6f) return GeoSeam();
                if (ax > collar - 1.2f || iy >= 43) return GeoDiamond(ix, iy, x > 0f ? 4 : 2);
                return GeoDiamond(ix, iy, x > 0f ? 3 : 1);
            }

            float top = ax <= 7f ? 37f : 37f - (ax - 7f) * 0.45f;
            float half = iy >= 26 ? 14f : 13.5f;
            if (y >= top || ax > half) return '.';
            float pec = GeoPecBottom(ax);

            // ---- the breastplate ----
            if (y >= pec)
            {
                if (ax < 1f) return GeoSeam();                                       // the sternum seam
                if (ix >= -6 && ix < -3 && iy >= 31 && iy < 34)                               // the vent, clear of the near arm
                    return RampChar(GeoGlass, iy == 31 ? 3 : 0);
                if (y < pec + 1f) return GeoDiamond(ix, iy, 2);                               // its lower edge
                if (y > 31f && x > 2f && x < 9f) return GeoDiamond(ix, iy, 5);                // the lit breast
                return GeoDiamond(ix, iy, 3 + side);
            }

            // ---- the glass band ----
            if (y >= pec - 4f)
            {
                if (ax > 13f) return '.';
                if (y >= pec - 1f) return RampChar(GeoGlass, 0);                              // under the plate
                if (iy == Mathf.FloorToInt(pec - 3f) && x > 5f && x < 9f) return RampChar(GeoGlass, 4);   // a glint
                return RampChar(GeoGlass, y < pec - 3f ? 3 : 2);
            }

            // ---- the flanks; the stone belly between them ----
            if (ax <= 9.5f) return GeoRock(ix, iy, 3 + LitSide(x, 4f, -5f) - (iy >= 21 ? 1 : 0));
            if (ax < 10.5f) return GeoDiamond(ix, iy, x > 0f ? 4 : 2);                        // the inner edge
            return GeoDiamond(ix, iy, 3 + side);
        }

        // ------------------------------------------------------------------ the gauntlets (Gloves)
        //
        // ARM-local: 0 at the shoulder, the elbow cut at -14, the wrist at -22, the hand below. The
        // upper arm is STONE (the reference's arm) - its rows uniform, since the rig lengthens the
        // upper arm by repeating row -12 and only the hashed grain varies. The forearm SHELL, below the cut so
        // it bends with the forearm, bulges to its widest mid-forearm under a lip parted off by a
        // black seam; the FIST ends in three round knuckles, the reference's three fingers.
        //
        // The shell is 7 at its widest, Revenant's bracer: the near arm hangs in front of the
        // torso, and Nocturne found anything wider putting its outline over the chest.

        const int GeoGauntletTop = 0, GeoGauntletBottom = -32, GeoGauntletHalf = 8;

        static string[] _geoGauntletRows;
        static string[] GeoGauntletRows => _geoGauntletRows ??=
            PaintField(-GeoGauntletHalf, GeoGauntletHalf, GeoGauntletBottom, GeoGauntletTop, GeoGauntletTexel);

        static readonly float[] GeoKnuckles = { -3.8f, 0f, 3.8f };

        static char GeoGauntletTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 2.5f, -3f);

            // The upper arm.
            if (iy >= -14 && iy < -1)
                return ax > 5f ? '.' : GeoRock(ix, iy, 3 + side);

            // The forearm shell.
            if (iy >= -25 && iy < -14)
            {
                float t = (-14f - y) / 11f;
                float half = 5.5f + 1.5f * Mathf.Sin(Mathf.PI * t);
                if (ax > half) return '.';
                if (iy == -17) return GeoSeam();                                      // under the lip
                if (iy == -15) return GeoDiamond(ix, iy, 4 + Mathf.Max(side, 0));              // the lip, lit
                if (ax > half - 1f) return GeoDiamond(ix, iy, x > 0f ? 4 : 1);
                return GeoDiamond(ix, iy, 3 + side - (iy <= -23 ? 1 : 0));
            }

            // The fist, its lower edge three knuckles.
            if (iy >= -29 && iy < -25)
            {
                if (ax > 6f) return '.';
                return GeoDiamond(ix, iy, iy == -26 ? 4 : 3 + side);
            }
            if (iy >= GeoGauntletBottom && iy < -29)
            {
                foreach (float k in GeoKnuckles)
                {
                    float kx = x - k, ky = y + 29.5f;
                    if (kx * kx + ky * ky < 4.6f)
                        return GeoDiamond(ix, iy, ky > 0.5f && kx > -0.5f ? 4 : ky < -1f ? 2 : 3);
                }
            }
            return '.';
        }

        // ------------------------------------------------------------------ the belt
        //
        // Torso-local, after the user's hip crop. A band of black GLASS on BeltY's rows; at the
        // front a glossy glass DOME over it; and hung from under the dome a FAN of three diamond
        // SHARDS: the long centre TASSET falling to the knee, and either side a broad shard,
        // SHORTER and splayed OUT - three points, a crystal cluster. Hung from the belt rather than the legs so the fan
        // stays one piece while the legs walk under it (Vermilion's tabard does the same).
        //
        // HARD, NOT CLOTH. Vermilion's sides are panels - cloth with swallowtail hems - and the
        // user asked for structure there without borrowing it. What makes these read as cut stone
        // is the BEVEL: every shard is a straight-edged polygon with a RIDGE down its long axis,
        // one flat lit face and one flat shaded face either side of a bright edge, and a black
        // seam wherever two shards touch. No facet scatter on them (GeoDiamond's) - a flat face
        // and a sharp edge are the hardness.
        //
        // ONE SHARD A SIDE, AND BROAD. Two a side (a middle and an outer) was the first pass: at
        // this density each was about five texels across, so a ridge and two seams cut it into
        // strips and the fan read as a row of ICICLES. A face needs room to be a plane.

        const int GeoBeltTop = 12, GeoBeltBottom = -24, GeoBeltHalf = 18;

        /// <summary>The fan's centre line - the tasset's own, leaning a texel toward +X.</summary>
        const float GeoFanAxis = 1f;

        static string[] _geoBeltRows;
        static string[] GeoBeltRows => _geoBeltRows ??=
            PaintField(-GeoBeltHalf, GeoBeltHalf, GeoBeltBottom, GeoBeltTop, GeoBeltTexel);

        /// <summary>One shard: its ridge, top to point, and a convex outline (in order) where the
        /// shard is a polygon. Authored for the +X side of the axis; the -X side mirrors them about
        /// <see cref="GeoFanAxis"/>.</summary>
        readonly struct GeoShard
        {
            public readonly Vector2[] Outline;
            public readonly Vector2 RidgeTop, RidgePoint;
            public GeoShard(Vector2 ridgeTop, Vector2 ridgePoint, params Vector2[] outline)
            {
                Outline = outline; RidgeTop = ridgeTop; RidgePoint = ridgePoint;
            }
        }

        static GeoShard[] _geoShards;
        static GeoShard[] GeoShards => _geoShards ??= new[]
        {
            // 0: the centre tasset, to the knee.
            new GeoShard(new Vector2(1f, 3f), new Vector2(2.8f, -22f),
                new Vector2(-5f, 3f), new Vector2(7f, 3f), new Vector2(7.5f, -5f),
                new Vector2(2.8f, -22f), new Vector2(-5.3f, -5f)),
            // 1: the side shard, shorter, splayed out. ROUNDED by the user's call - its outline
            // is GeoSideShardIn, not this polygon; only the ridge here is used.
            new GeoShard(new Vector2(11.5f, 3f), new Vector2(14.5f, -15f)),
        };

        /// <summary>
        /// The side shard's outline (+X side): an ellipse laid along its ridge and cut flat where
        /// it hangs from the band - a rounded, splayed teardrop. Angular, beside the tasset's
        /// point, the fan read sharper than the user wanted.
        /// </summary>
        static bool GeoSideShardIn(float x, float y)
        {
            if (y > 3f) return false;
            const float cx = 12.4f, cy = -4f;
            // Along the ridge's direction (3, -18), normalised.
            const float dx = 0.164f, dy = -0.986f;
            float ox = x - cx, oy = y - cy;
            float u = (ox * dx + oy * dy) / 10f, v = (-ox * dy + oy * dx) / 5.6f;
            return u * u + v * v < 1f;
        }

        static bool GeoInConvex(Vector2[] poly, float x, float y)
        {
            bool pos = false, neg = false;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                float cross = (b.x - a.x) * (y - a.y) - (b.y - a.y) * (x - a.x);
                if (cross > 0f) pos = true; else if (cross < 0f) neg = true;
                if (pos && neg) return false;
            }
            return true;
        }

        /// <summary>Which shape holds (x, y), front to back: 1 the dome, 2 the band, 3 the
        /// tasset, 4 a side shard; 0 nothing. <paramref name="mirrored"/>
        /// says the shard is the -X side's (its local x is mirrored about the axis).</summary>
        static int GeoBeltShape(float x, float y, out bool mirrored)
        {
            mirrored = false;
            if (GeoEllipse(x, y, GeoFanAxis, 5f, 4.5f, 4.5f)) return 1;
            if (y >= 4f && y < 10f && Mathf.Abs(x) <= 15.5f) return 2;
            if (GeoInConvex(GeoShards[0].Outline, x, y)) return 3;
            float mx = 2f * GeoFanAxis - x;
            if (GeoSideShardIn(x, y)) return 4;
            if (GeoSideShardIn(mx, y)) { mirrored = true; return 4; }
            return 0;
        }

        static char GeoBeltTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f;
            int id = GeoBeltShape(x, y, out bool mirrored);
            if (id == 0) return '.';

            // The dome: glossy black glass, one highlight up toward the light.
            if (id == 1)
                return GeoEllipse(x, y, GeoFanAxis + 1.5f, 6.8f, 1.6f, 1.2f)
                    ? RampChar(GeoGlass, 4) : RampChar(GeoGlass, 0);

            // The band.
            if (id == 2)
            {
                if (iy == 9) return RampChar(GeoGlass, x > 0f ? 5 : 4);
                if (iy == 4) return RampChar(GeoGlass, 0);
                return RampChar(GeoGlass, iy == 8 ? 3 : 2);
            }

            // A shard. A black seam wherever it meets a shape in front of it.
            foreach (var (dx, dy) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
            {
                int n = GeoBeltShape(x + dx, y + dy, out _);
                if (n != 0 && n < id) return GeoSeam();
            }

            var shard = GeoShards[id - 3];
            float ry = Mathf.InverseLerp(shard.RidgeTop.y, shard.RidgePoint.y, y);
            float rx = Mathf.Lerp(shard.RidgeTop.x, shard.RidgePoint.x, ry);
            if (mirrored) rx = 2f * GeoFanAxis - rx;
            if (Mathf.Abs(x - rx) < 0.5f) return RampChar(GeoGem, 5);                          // the ridge
            if (GeoHash(ix, iy, 91) % 31 == 0) return RampChar(GeoGem, 5);                     // a sparkle
            // The faces: the one turned toward the light (+X) lit, the other in shade, each a
            // tone darker over its lower half - the shard tilting back toward its point. (One
            // tone apart was tried and read softer; the user kept the harder split.)
            bool lit = x > rx;
            return RampChar(GeoGem, (lit ? 4 : 2) - (ry > 0.55f ? 1 : 0));
        }

        // ------------------------------------------------------------------ the greaves (Legs)
        //
        // LEG-local: 0 at the hip, the knee at -24, the sole at -48. A STONE thigh (the reference's
        // leg; the tasset hangs over its front); a round knee COP over a black seam, and a shin
        // with a lit ridge running down under the boot.

        const int GeoGreaveTop = 2, GeoGreaveBottom = -40, GeoGreaveHalf = 6;

        static string[] _geoGreaveRows;
        static string[] GeoGreaveRows => _geoGreaveRows ??=
            PaintField(-GeoGreaveHalf, GeoGreaveHalf, GeoGreaveBottom, GeoGreaveTop, GeoGreaveTexel);

        static char GeoGreaveTexel(int ix, int iy)
        {
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2.5f);

            // The thigh.
            if (iy >= -21)
                return ax > 4.5f || iy >= 1 ? '.' : GeoRock(ix, iy, 3 + side);

            // The knee cop, rounded.
            if (iy >= -28)
            {
                float half = Mathf.Abs(y + 24.5f) > 2.5f ? 4f : 5f;
                if (ax > half) return '.';
                if (iy == -22) return GeoDiamond(ix, iy, 5);
                if (iy == -28) return GeoSeam();
                return GeoDiamond(ix, iy, 3 + side);
            }

            // The shin, a lit ridge down its front.
            if (ax > 4.5f) return '.';
            if (ix == 0) return GeoDiamond(ix, iy, 5);
            if (ix == 1) return GeoDiamond(ix, iy, 2);
            return GeoDiamond(ix, iy, 3 + side);
        }

        // ------------------------------------------------------------------ the boots (Boots)
        //
        // LEG-local, art bottom on the sole. A cuff over the shin, a black seam round the ankle,
        // and a heavy foot whose toe is SPLIT in two by a black seam - the reference's two toes,
        // rounded at the front.

        const int GeoBootTop = -32, GeoBootBottom = -48, GeoBootHalf = 8;

        static string[] _geoBootRows;
        static string[] GeoBootRows => _geoBootRows ??=
            PaintField(-GeoBootHalf, GeoBootHalf, GeoBootBottom, GeoBootTop, GeoBootTexel);

        static char GeoBootTexel(int ix, int iy)
        {
            float x = ix + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 2f, -3f);

            // The cuff.
            if (iy >= -38)
            {
                if (ax > 5f) return '.';
                return GeoDiamond(ix, iy, iy == -33 ? 4 : 3 + side);
            }
            // The seam round the ankle.
            if (iy == -39) return ax > 5.5f ? '.' : GeoSeam();

            // The foot, the toe split in two and rounded at the front.
            float half = iy >= -44 ? 6.5f : 7f - Mathf.Max(0f, -45.5f - iy) * 0.8f;
            if (ax > half) return '.';
            if (iy <= -44)
            {
                if (ax < 0.9f) return GeoSeam();                                      // between the toes
                if (iy == -48) return GeoDiamond(ix, iy, 1);                                    // the sole
                return GeoDiamond(ix, iy, iy == -44 ? 4 : 3 + side);
            }
            return GeoDiamond(ix, iy, iy == -40 ? 4 : 3 + side);
        }
    }
}
