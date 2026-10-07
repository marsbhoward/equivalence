using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Aether: black plate, a curse burning in it
    //
    // After the user's reference (a corrupted knight-queen in black plate veined with red, a long
    // dark dress open at the front, a visor) crossed with a CURSE MARK: a seal of three hooked
    // tongues on the sternum, flame-shaped marks spreading out of it across the plate. Named Aether by
    // the user (built as "Corvus", for the raven of the nigredo).
    //
    //   aether_helm        Head       a full helm after the user's Magneto references: a smooth dome,
    //                                 the face in an M, a burning trim round it and the foot (SealsHead)
    //   aether_pauldrons   Shoulders  one swept plate rising to a point over each shoulder, two
    //                                 lames under it
    //   aether_cuirass     Torso      the dress's high collar; a fitted breastplate coming to a V,
    //                                 THE SEAL on its sternum; two lames to the waist
    //   aether_gauntlets   Gloves     lamed upper arm, a spiked couter, a long vambrace, clawed fist
    //   aether_faulds      Belt       a plate band, a pointed front plate, a long hip plate a side
    //   aether_skirt       Legs       the dress: floor-length, OPEN at the front, the hem torn
    //   aether_greaves     Boots      a kite knee cop, a ridged greave, a pointed sabaton
    //
    // THE MARKS ARE THE SECRET FIRE'S (SecretFire): texels in its three reserved letters - Core,
    // Vein, Ember - drawn black on the piece itself and lit by the overlay in the wearer's element,
    // every piece on one beat. Kindle() is called LAST on each palette, after the far limb's
    // scaling: the marks are matched to the byte.
    //
    // Fields in each joint's own frame (DemoGear.Fields.cs), lit from +X like every piece here.
    // Diamond, power 0, like every reference-built cosmetic set; the Aether Greatsword, its
    // sword, is at the bottom of this file.
    public static partial class DemoGear
    {
        const string CorPlate = "ksdblh", CorCloth = "KSDBLH", CorDark = "123456";
        const char CorCore = SecretFire.Core, CorVein = SecretFire.Vein, CorEmber = SecretFire.Ember;

        static void AddAether(List<GearItem> items)
        {
            // BLACKENED STEEL - a near-neutral dark grey, its sheen a low grey rather than a bright
            // one. The first pass lit a blue-black toward a cold steel blue and read as BLACK
            // DIAMOND (the user's note): crystal is a texture this density cannot carry, and a
            // bright, saturated glow on every edge is what said crystal. Shadows below LIGHT
            // (Palette.Undersuit's reason: on a dark base, lerping toward black collapses the
            // bottom of the ramp).
            var plate = new Palette.Ramp(new Color(0.17f, 0.17f, 0.19f), lift: 0.26f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.34f, 0.22f)
                .WithHighlightsToward(new Color(0.50f, 0.51f, 0.54f), 0.30f);
            // The dress: a matte navy-black (the silk recipe) - never 'H'.
            var cloth = new Palette.Ramp(new Color(0.12f, 0.12f, 0.19f), lift: 0.16f, shade: 0.34f, line: 0.80f)
                .WithShadowsBelowLight(0.40f, 0.26f);
            // Straps, the flanks, the gaps: a near-black a step off the undersuit.
            var dark = new Palette.Ramp(new Color(0.10f, 0.10f, 0.13f), lift: 0.24f, shade: 0.36f, line: 0.85f)
                .WithShadowsBelowLight(0.46f, 0.30f);
            const float farF = 0.85f;

            var pal = SecretFire.Kindle(Palette.Of(plate, cloth, dark));
            var far = SecretFire.Kindle(Palette.Of(plate.Scaled(farF), cloth.Scaled(farF), dark.Scaled(farF)));

            var helm = Kindle(Make("aether_helm", "Aether Helm", GearSlot.Head, LootTier.Diamond, 0f,
                Pixels(RigLayer.HeadArmor, "gear.head.aether", CorHelmRows, pal,
                       FieldCentreCells(CorHelmMinX, CorHelmMaxX), FieldCentreCells(CorHelmBottom, CorHelmTop),
                       ppu: BodyPpu, outline: false)));
            // Enclosed - the guards wrap the jaw and no hair escapes (seraph_helm's reason).
            helm.SealsHead = true;
            items.Add(helm);

            items.Add(Kindle(Make("aether_pauldrons", "Aether Pauldrons", GearSlot.Shoulders, LootTier.Diamond, 0f,
                Pixels(RigLayer.Shoulders, "gear.pauldron.aether", CorPauldronNear, pal,
                       -ShoulderX - CorPauldronCentreCells, CorPauldronY, ppu: BodyPpu),
                Pixels(RigLayer.ShouldersBack, "gear.pauldron.aether.dark", CorPauldronFar, far,
                       ShoulderX + CorPauldronCentreCells, CorPauldronY, ppu: BodyPpu))));

            items.Add(Kindle(Defends(Make("aether_cuirass", "Aether Cuirass", GearSlot.Torso, LootTier.Diamond, 0f,
                Pixels(RigLayer.TorsoArmor, "gear.torso.aether", CorCuirassRows, pal,
                       0f, FieldCentreCells(CorCuirassBottom, CorCuirassTop), ppu: BodyPpu)),
                // A duellist who meets the blow rather than leaving its path.
                DefensiveAbility.ParryStance)));

            items.Add(Kindle(Make("aether_gauntlets", "Aether Gauntlets", GearSlot.Gloves, LootTier.Diamond, 0f,
                Pixels(RigLayer.GlovesFront, "gear.gloves.aether", CorGauntletNear, pal,
                       0f, FieldCentreCells(CorGauntletBottom, CorGauntletTop), ppu: BodyPpu),
                Pixels(RigLayer.GlovesBack, "gear.gloves.aether.dark", CorGauntletFar, far,
                       0f, FieldCentreCells(CorGauntletBottom, CorGauntletTop), ppu: BodyPpu))));

            items.Add(Kindle(Make("aether_faulds", "Aether Faulds", GearSlot.Belt, LootTier.Diamond, 0f,
                Pixels(RigLayer.Belt, "gear.belt.aether", CorFauldRows, pal,
                       0f, FieldCentreCells(CorFauldBottom, CorFauldTop), ppu: BodyPpu))));

            items.Add(Kindle(Make("aether_skirt", "Aether Skirt", GearSlot.Legs, LootTier.Diamond, 0f,
                Pixels(RigLayer.Tasset, "gear.legs.aether", CorSkirtRows, pal,
                       0f, FieldCentreCells(CorSkirtBottom, CorSkirtTop), ppu: BodyPpu))));

            items.Add(Kindle(Make("aether_greaves", "Aether Greaves", GearSlot.Boots, LootTier.Diamond, 0f,
                Pixels(RigLayer.BootsFront, "gear.boots.aether", CorGreaveRows, pal,
                       FieldCentreCells(CorGreaveMinX, CorGreaveMaxX), FieldCentreCells(CorGreaveBottom, CorGreaveTop),
                       ppu: BodyPpu),
                Pixels(RigLayer.BootsBack, "gear.boots.aether.dark", CorGreaveRows, far,
                       FieldCentreCells(CorGreaveMinX, CorGreaveMaxX), FieldCentreCells(CorGreaveBottom, CorGreaveTop),
                       ppu: BodyPpu))));

            // The plate is the set's mass, the dress its other material. The marks never dye -
            // they burn in the element, whatever the piece is coloured.
            Dyeable(items, "aether_", DyeChannel.Of("Plate", DyeMaterial.Metal, plate, farF),
                    DyeChannel.Of("Cloth", DyeMaterial.Cloth, cloth, farF));

            AddAetherGreatsword(items);
            AddPrimaMateria(items);         // the set's bow - DemoGear.PrimaMateria.cs
        }

        /// <summary>Stamp a piece as carrying marks the Secret Fire lights (GearItem.Kindled).</summary>
        static GearItem Kindle(GearItem item)
        {
            item.Kindled = true;
            return item;
        }

        // ------------------------------------------------------------------ the marks
        //
        // Every mark is a STROKE: a tendril along a polyline, its half-width tapering from root to
        // tip. The curse mark's tongues are wide at the root and come to a point; the reference's
        // veins are one texel all the way. The last stretch of each dies out as Ember, so a tendril
        // fades into the plate rather than stopping on a lit end.

        /// <summary>Distance from (px, py) to a polyline, and how far along it (0 root .. 1 tip)
        /// the nearest point lies.</summary>
        static float CorAlong(float px, float py, Vector2[] pts, out float t)
        {
            float total = 0f;
            for (int i = 1; i < pts.Length; i++) total += Vector2.Distance(pts[i - 1], pts[i]);
            var p = new Vector2(px, py);
            float best = float.MaxValue, run = 0f;
            t = 0f;
            for (int i = 1; i < pts.Length; i++)
            {
                Vector2 a = pts[i - 1], ab = pts[i] - a;
                float len = ab.magnitude;
                float u = len > 1e-4f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / (len * len)) : 0f;
                float d = Vector2.Distance(p, a + ab * u);
                if (d < best)
                {
                    best = d;
                    t = total > 0f ? (run + u * len) / total : 0f;
                }
                run += len;
            }
            return best;
        }

        /// <summary>
        /// One stroke's letter at (px, py), or '\0' where it misses: half-width <paramref name="w0"/>
        /// at the root to <paramref name="w1"/> at the tip, Ember past <paramref name="emberFrom"/>
        /// of the way along, Core before <paramref name="coreTo"/>.
        /// </summary>
        static char CorStroke(float px, float py, Vector2[] pts, float w0, float w1,
                              float emberFrom = 0.72f, float coreTo = 0f)
        {
            float d = CorAlong(px, py, pts, out float t);
            if (d > Mathf.Lerp(w0, w1, t)) return '\0';
            if (t < coreTo) return CorCore;
            return t >= emberFrom ? CorEmber : CorVein;
        }

        /// <summary>The first stroke of <paramref name="strokes"/> that (px, py) falls in.</summary>
        static char CorStrokes(float px, float py, (Vector2[] Pts, float W0, float W1)[] strokes,
                               float emberFrom = 0.72f)
        {
            foreach (var s in strokes)
            {
                char c = CorStroke(px, py, s.Pts, s.W0, s.W1, emberFrom);
                if (c != '\0') return c;
            }
            return '\0';
        }

        /// <summary>
        /// THE SEAL: a burning heart, and three hooked tongues on a ring round it, each sweeping
        /// clockwise - the curse mark's three tomoe drawn as flames. Centred at (cx, cy); s scales it.
        /// </summary>
        static char CorSeal(float px, float py, float cx, float cy, float s)
        {
            float dx = px - cx, dy = py - cy;
            if (dx * dx + dy * dy < 0.8f * 0.8f * s * s) return CorCore;
            for (int k = 0; k < 3; k++)
            {
                float a0 = 90f + 120f * k;
                var pts = new Vector2[5];
                for (int i = 0; i < pts.Length; i++)
                {
                    float a = (a0 - i * 22f) * Mathf.Deg2Rad, r = (2.6f + i * 0.25f) * s;
                    pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
                }
                char c = CorStroke(px, py, pts, 1.05f * s, 0.32f * s, emberFrom: 0.8f);
                if (c != '\0') return c;
            }
            return '\0';
        }

        // ------------------------------------------------------------------ the helm (Head)
        //
        // HEAD-local (revenant_mask's frame): the chin at 0, the eyes' rows 7..11, the hairline
        // 14.5, the hair topping out near 28; the face turned toward +X, its middle near x 2.75 -
        // the near eye at x -6..-2, the far at 8..11 (BodyLook.Gaze). After the user's Magneto
        // references (a comic panel, then a black prop helm with silver trim):
        //
        //   DOME      a smooth black shell over the skull, round across the top.
        //   OPENING   the face in an M: a rounded ARCH over each eye, the two meeting at a POINT on
        //             the nose; below, the cheek guards close in toward the mouth.
        //   TRIM      the prop's silver band, here the SECRET FIRE: one even thickness round the
        //             whole opening and along the whole foot of the helm, burning in the wearer's
        //             element. No flares off it (the prop's horns at the brow and barbs on the
        //             guards) - the user's call: the band follows the curves and nothing else.
        //
        // Its own edge is drawn (no outline): an auto-stroke round the opening would eat the eyes,
        // and the trim, not a black line, is what borders the face.

        const int CorHelmMinX = -20, CorHelmMaxX = 20, CorHelmBottom = -6, CorHelmTop = 36;
        const float CorFaceMid = 2.75f;
        /// <summary>The trim's thickness in texels, measured from the opening (and from the foot,
        /// inside the helm's own one-texel edge).</summary>
        const float CorTrimWidth = 2f;

        static string[] _corHelmRows;
        static string[] CorHelmRows => _corHelmRows ??=
            PaintField(CorHelmMinX, CorHelmMaxX, CorHelmBottom, CorHelmTop, CorHelmTexel);

        /// <summary>The dome's half-width at a height: an ellipse over the skull from y 14 up,
        /// the sides plumb below it.</summary>
        static float CorHelmHalfAt(float y)
        {
            const float Spring = 14f, Rise = 20.5f, Half = 18f;
            if (y <= Spring) return Half;
            float t = (y - Spring) / Rise;
            return t >= 1f ? -1f : Half * Mathf.Sqrt(1f - t * t);
        }

        /// <summary>The foot of the helm: LEVEL, down on the top of the chestplate (the user's
        /// call - at the jaw it left a strip of neck between helm and collar), so the guards run
        /// past the chin as the prop's do and the trim along it never breaks. (A foot slanting up
        /// under the guards stepped the band and left the guards' tips as lumps.)</summary>
        static float CorHelmFootAt(float x) => -5f;

        // Each eye hole: an arch whose OUTER end comes to a sharp POINT past the guard's edge
        // (the user's call - a round corner falling into the guard read soft), the point's lower
        // side running back in to meet the guard. The two arches still meet at a point on the nose.
        const float CorBrowBase = 7.6f;
        // (arch centre, peak, outer point x, point y, where the point's lower side meets the guard)
        static readonly (float Cx, float Peak, float TipX, float TipY, float GuardY) CorNearEye = (-3.85f, 13f, -10.8f, 10.4f, 8.6f);
        static readonly (float Cx, float Peak, float TipX, float TipY, float GuardY) CorFarEye = (8.5f, 12.8f, 13.4f, 10.4f, 9.4f);
        const float CorGuardBackX = -8.45f, CorGuardFrontX = 12.25f;

        /// <summary>The top of an eye hole at x: from the point on the nose up round the arch, then
        /// down to the outer point with a slope, so the corner comes out sharp.</summary>
        static float CorBrowAt(float x)
        {
            var e = x < CorFaceMid ? CorNearEye : CorFarEye;
            float rise = e.Peak - CorBrowBase;
            if ((x - e.Cx) * (CorFaceMid - e.Cx) > 0f)
            {
                // Inner half: the round arch down to the point on the nose.
                float u = (x - e.Cx) / (CorFaceMid - e.Cx);
                return CorBrowBase + rise * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
            }
            float t = (x - e.Cx) / (e.TipX - e.Cx);
            return t > 1f ? float.NegativeInfinity : e.TipY + (e.Peak - e.TipY) * (1f - t * t);
        }

        /// <summary>The underside of the outer point, past the guard's edge: a straight line from
        /// where it leaves the guard out to the tip.</summary>
        static float CorPointUnderAt(float x)
        {
            var e = x < CorFaceMid ? CorNearEye : CorFarEye;
            float gx = x < CorFaceMid ? CorGuardBackX : CorGuardFrontX;
            return e.GuardY + (e.TipY - e.GuardY) * (x - gx) / (e.TipX - gx);
        }

        /// <summary>The opening's sides: each guard plumb down past the eyes, then closing in
        /// toward the mouth.</summary>
        static float CorOpenBackAt(float y) => CorGuardBackX + Mathf.Max(0f, CorBrowBase - y) * 0.72f;
        static float CorOpenFrontAt(float y) => CorGuardFrontX - Mathf.Max(0f, CorBrowBase - y) * 0.5f;

        /// <summary>
        /// The band's MITRE at each outer point: past the guard, between the point's two edges
        /// carried on outward and pushed out by the band's width, so the band comes to a point as
        /// the opening does. A disc of neighbours alone rounds the outside of a point into a nub.
        /// Stopped a texel short of the helm's side, or it would run out to the edge as a horn.
        /// </summary>
        static bool CorTrimMitre(float x, float y)
        {
            bool nearSide = x < CorFaceMid;
            var e = nearSide ? CorNearEye : CorFarEye;
            float gx = nearSide ? CorGuardBackX : CorGuardFrontX;
            if (nearSide ? x > gx : x < gx) return false;
            float half = CorHelmHalfAt(y);
            if (Mathf.Abs(x) > half - 2f) return false;
            float up = -2f * (e.Peak - e.TipY) / (e.TipX - e.Cx);                 // the arch's slope at the point
            float under = (e.TipY - e.GuardY) / (e.TipX - gx);                    // the underside's
            float dx = x - e.TipX;
            float top = e.TipY + up * dx + CorTrimWidth * Mathf.Sqrt(1f + up * up);
            float bottom = e.TipY + under * dx - CorTrimWidth * Mathf.Sqrt(1f + under * under);
            return y < top && y > bottom;
        }

        static bool CorFaceOpen(float x, float y)
        {
            if (x > CorOpenBackAt(y) && x < CorOpenFrontAt(y) && (y < CorBrowBase || y < CorBrowAt(x)))
                return true;
            // The outer points, out past each guard's edge.
            bool past = x <= CorGuardBackX || x >= CorGuardFrontX;
            return past && y < CorBrowAt(x) && y > CorPointUnderAt(x);
        }

        /// <summary>Under the helm's foot (outside it, below), as opposed to off its sides or top.</summary>
        static bool CorUnderFoot(float x, float y)
            => y < CorHelmFootAt(x) && !CorFaceOpen(x, y);

        static bool CorHelmIn(float x, float y)
        {
            float half = CorHelmHalfAt(y);
            if (half < 0f || x <= -half || x >= half) return false;
            if (y < CorHelmFootAt(x)) return false;
            return !CorFaceOpen(x, y);
        }

        /// <summary>True when a texel lies within <paramref name="r"/> of a texel that passes
        /// <paramref name="test"/> - a disc of neighbours, so the band keeps its thickness round
        /// curves and slants.</summary>
        static bool CorNear(float x, float y, float r, System.Func<float, float, bool> test)
        {
            int k = Mathf.CeilToInt(r);
            for (int dy = -k; dy <= k; dy++)
            for (int dx = -k; dx <= k; dx++)
                if (dx * dx + dy * dy <= r * r + 0.01f && test(x + dx, y + dy)) return true;
            return false;
        }

        /// <summary>Outside the helm, and not in the face opening.</summary>
        static bool CorOuterEdge(float x, float y) => !CorHelmIn(x, y) && !CorFaceOpen(x, y);

        static char CorHelmTexel(int ix, int iy)
        {
            const string P = CorPlate;
            float x = ix + 0.5f, y = iy + 0.5f;
            if (!CorHelmIn(x, y)) return '.';

            // The helm's own edge first - one texel round the OUTSIDE (not the opening, which the
            // band borders directly) - or the band pushes through it at the far guard and at each
            // guard's tip.
            if (CorOuterEdge(x - 1f, y) || CorOuterEdge(x + 1f, y)
                || CorOuterEdge(x, y - 1f) || CorOuterEdge(x, y + 1f)
                || y < CorHelmFootAt(x) + 1f)                                     // the foot's row, under the opening too
                return RampChar(P, 0);

            // ---- the trim: the Secret Fire, one thickness round the opening ----
            if (CorNear(x, y, CorTrimWidth, CorFaceOpen) || CorTrimMitre(x, y)) return CorVein;

            // ---- and along the whole foot, inside that edge ----
            if (CorNear(x, y, CorTrimWidth + 1f, CorUnderFoot)) return CorVein;

            // ---- the dome: smooth, lit toward +X, the crown's rim catching the light ----
            float half = CorHelmHalfAt(y);
            float n = x / half;                                                   // -1 back .. +1 front
            if (y > 14f && CorHelmHalfAt(y + 1.6f) < Mathf.Abs(x) + 0.5f)
                return RampChar(P, n > -0.2f ? 4 : 3);                            // the crown's rim
            if (n > 0.55f) return RampChar(P, 3);
            return RampChar(P, n < -0.6f ? 1 : 2);
        }

        // ------------------------------------------------------------------ the pauldrons (Shoulders)
        //
        // (Outward, height above the chin) texels from the joint, the Herald's frame. ONE swept
        // plate over the shoulder, its top edge climbing outward to a POINT that stands above the
        // shoulder line, its outer edge falling almost straight from the point, its lower edge a
        // shallow curve back in; under it two lames, each stepping out a little. On the FAR
        // shoulder - the curse's side - a tongue of flame climbs the plate toward its point with a
        // second beside it; both shoulders carry a vein along the first lame.

        const int CorPauldronMin = -6, CorPauldronMax = 16, CorPauldronRowCount = 22, CorPauldronRise = 8;

        static float CorPauldronCentreCells
            => PrimitiveCharacterRig.Proportions.Cells((CorPauldronMin + CorPauldronMax) * 0.5f);
        static float CorPauldronY
            => PlateY(CorPauldronNear) + PrimitiveCharacterRig.Proportions.Cells(CorPauldronRise);

        static string[] _corPauldronNear, _corPauldronFar;
        static string[] CorPauldronNear => _corPauldronNear ??= CorPauldron(outwardIsPlusX: false);
        static string[] CorPauldronFar => _corPauldronFar ??= CorPauldron(outwardIsPlusX: true);

        static string[] CorPauldron(bool outwardIsPlusX)
            => PaintField(CorPauldronMin, CorPauldronMax, 0, CorPauldronRowCount, (ix, iy) =>
            {
                int o = outwardIsPlusX ? ix : CorPauldronMin + CorPauldronMax - 1 - ix;
                int r = CorPauldronRowCount - 1 - iy;
                return CorPauldronTexel(o + 0.5f, CorPauldronRise - r - 0.5f, outwardIsPlusX ? 1 : -1);
            });

        static float CorPlateTop(float o) => 0.5f + (o + 3f) * 0.36f;
        static float CorPlateOuter(float h) => 11.5f - (5.5f - h) * 0.11f;
        static float CorPlateLower(float o) => -3f - (o + 2f) * 0.12f;

        static bool CorPlateIn(float o, float h)
            => o >= -3f && o <= CorPlateOuter(h) && h < CorPlateTop(o) && h >= CorPlateLower(o);

        static readonly Vector2[] CorPauldronFlame = { new(-1.6f, -2.4f), new(1.4f, -1.8f), new(4.4f, -0.2f), new(6.6f, 2.2f), new(8.4f, 4.2f), new(10.6f, 5.2f) };
        static readonly Vector2[] CorPauldronTongue = { new(0.8f, -2.2f), new(2.2f, 0.2f), new(2.4f, 2.4f), new(3.6f, 3.6f) };
        static readonly Vector2[] CorPauldronBarb = { new(5.4f, 0.6f), new(6.8f, -0.8f) };
        static readonly Vector2[] CorPauldronRim = { new(-0.5f, -6.8f), new(5f, -7.4f), new(10.4f, -7.9f) };

        static char CorPauldronTexel(float o, float h, int outward)
        {
            const string P = CorPlate;
            // Light from +X: on the far side the outer face is lit; on the near, the inner.
            int side = (outward > 0 ? o > 7f : o < 2f) ? 1 : (outward > 0 ? o < -1f : o > 11f) ? -1 : 0;

            if (CorPlateIn(o, h))
            {
                // The curse climbs the FAR shoulder, from the chest's tongue under its inner edge.
                if (outward > 0)
                {
                    char m = CorStroke(o, h, CorPauldronFlame, 1.15f, 0.3f);
                    if (m == '\0') m = CorStroke(o, h, CorPauldronTongue, 0.8f, 0.28f);
                    if (m == '\0') m = CorStroke(o, h, CorPauldronBarb, 0.55f, 0.28f);
                    if (m != '\0') return m;
                }

                if (!CorPlateIn(o, h + 1f)) return RampChar(P, 5);                    // the lit top edge
                if (!CorPlateIn(o, h + 2f)) return RampChar(P, 4);
                if (!CorPlateIn(o + 1f, h)) return RampChar(P, outward > 0 ? 4 : 2);   // the outer edge
                if (!CorPlateIn(o, h - 1f)) return RampChar(P, 1);                    // the rim under it
                return RampChar(P, 3 + side);
            }

            // ---- two lames below, each a little further out ----
            for (int k = 0; k < 2; k++)
            {
                float lTop = CorPlateLower(o) - 1f - 3.6f * k, lBottom = lTop - 3.6f;
                float inner = -1.5f + k, outer = 10.8f + 0.6f * k;
                if (h >= lTop || h < lBottom || o < inner || o > outer) continue;
                if (o > outer - 1.2f && h < lBottom + 1.2f) return '.';                // the rounded corner
                if (h >= lTop - 1f) return RampChar(P, 0);                             // the gap under the plate above
                char lm = CorStroke(o, h, CorPauldronRim, 0.5f, 0.45f);
                if (lm != '\0') return lm;
                if (h >= lTop - 2f) return RampChar(P, 4 + Mathf.Max(side, 0));
                if (h < lBottom + 1f) return RampChar(P, 1);
                return RampChar(P, 3 + side);
            }
            return '.';
        }

        // ------------------------------------------------------------------ the cuirass (Torso)
        //
        // Torso-local, the waist at 0, the chin at 40. The dress's HIGH COLLAR in cloth under the
        // chin; a fitted BREASTPLATE from it, two shallow domes either side of a lit sternum ridge,
        // its lower edge coming to a V at the middle; two LAMES under it to the waist; the flanks in
        // dark leather, which the arms cover nearly all of.
        //
        // THE SEAL sits under the collar, a little toward the far side - the curse mark's place on
        // the neck - and the marks spread from it the way the curse does, ONE WAY: a long tongue
        // sweeping down across the sternum to the near ribs, one down the far side to the plate's
        // foot, one up the collar toward the neck, and on down the lames as the reference's
        // cracks. Symmetric, round the sternum, it read as an emblem (a spider, then a sun). Kept
        // inside x -6..6 above y 26: both pauldrons draw over the chest's upper corners. The far
        // side stops at the body's edge (TalBodyEdge), so the far arm hangs clear of it.

        const int CorCuirassTop = 44, CorCuirassBottom = 4, CorCuirassHalf = 16;
        static readonly Vector2 CorSealAt = new(2.5f, 31f);

        static string[] _corCuirassRows;
        static string[] CorCuirassRows => _corCuirassRows ??=
            PaintField(-CorCuirassHalf, CorCuirassHalf, CorCuirassBottom, CorCuirassTop, CorCuirassTexel);

        static float CorCollarBottom(float ax) => 33.5f + ax * 0.12f;
        static float CorPlateBottom(float ax) => 10.5f + ax * 0.5f;

        static readonly (Vector2[] Pts, float W0, float W1)[] CorChestMarks =
        {
            // the long tongue, down across the sternum to the near ribs
            (new Vector2[] { new(0.2f, 28.6f), new(-2.4f, 25.6f), new(-5.2f, 22.8f), new(-8.4f, 20.8f), new(-11f, 21.2f) }, 1.05f, 0.32f),
            // down the far side to the plate's foot
            (new Vector2[] { new(4.6f, 27.8f), new(6f, 24.4f), new(5.6f, 20.6f), new(7f, 17f) }, 0.95f, 0.38f),
            // under the far pauldron, where it carries on
            (new Vector2[] { new(6f, 31.6f), new(8.6f, 33f) }, 0.75f, 0.35f),
            // up the collar toward the neck - where the curse began
            (new Vector2[] { new(1.8f, 34.4f), new(1.2f, 36.8f), new(1.8f, 39f) }, 0.8f, 0.3f),
            // the barbs - the curse's flames are never smooth
            (new Vector2[] { new(-3.4f, 24.6f), new(-4.4f, 22.2f) }, 0.6f, 0.28f),
            (new Vector2[] { new(-7f, 21.8f), new(-7.6f, 24.2f) }, 0.55f, 0.28f),
            (new Vector2[] { new(5.8f, 22.6f), new(7.6f, 21.6f) }, 0.55f, 0.28f),
            // and on as the reference's veins: down the lames on the far side, and from the long
            // tongue's end down the near flank
            (new Vector2[] { new(7f, 17f), new(6.4f, 13.4f), new(6.6f, 9.4f), new(5.2f, 6f) }, 0.55f, 0.45f),
            (new Vector2[] { new(-11f, 21.2f), new(-10.4f, 17.6f), new(-8f, 14.8f) }, 0.55f, 0.45f),
        };

        static char CorCuirassTexel(int ix, int iy)
        {
            const string P = CorPlate, C = CorCloth, D = CorDark;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 4f, -5f);

            float top = 40.5f - Mathf.Max(0f, ax - 5f) * 0.5f;
            float half = x < 0f ? (iy >= 26 ? 13.5f : 13f) : TalBodyEdge(y);
            if (y >= top || ax > half) return '.';

            // ---- the collar: the dress's, standing up under the chin ----
            float collar = CorCollarBottom(ax);
            if (y >= collar)
            {
                if (ax < 1.2f && y >= collar + 2f) return RampChar(C, 1);             // the fold at the throat
                if (y < collar + 1f) return RampChar(C, 2);                            // its lower fold
                return RampChar(C, (y >= top - 1.5f ? 4 : 3) + side);
            }

            float plateBottom = CorPlateBottom(ax);
            bool onPlate = y >= plateBottom;

            // ---- the marks, wherever plate is ----
            float abHalf = 8.5f + (y - 4f) * 0.32f;
            if (onPlate || ax < abHalf)
            {
                char m = CorSeal(x, y, CorSealAt.x, CorSealAt.y, 1f);
                if (m == '\0') m = CorStrokes(x, y, CorChestMarks);
                if (m != '\0') return m;
            }

            // ---- the breastplate ----
            if (onPlate)
            {
                if (y >= collar - 1f) return RampChar(P, 4 + Mathf.Max(side, 0));    // its rolled top edge
                if (y < plateBottom + 1f) return RampChar(P, 2);                       // the lower rim
                if (ix == 0) return RampChar(P, 4);                                    // the sternum ridge
                if (ix == -1) return RampChar(P, 2);
                if (ax > 11f) return RampChar(P, 2);                                   // turning under the arm
                // A shallow dome either side, lit up and toward +X.
                float cx = x > 0f ? 5.5f : -5.5f;
                float nx = (x - cx) / 6f, ny = (y - 24f) / 6.5f;
                float lit = nx * 0.55f + ny * 0.83f;
                int tone = lit > 0.25f ? 4 : lit < -0.55f ? 2 : 3;
                return RampChar(P, Mathf.Clamp(tone + (x < -2f ? -1 : 0), 1, 5));
            }
            if (y >= plateBottom - 1f && ax < abHalf + 1f) return RampChar(P, 0);     // its shadow

            // ---- the lames, to the waist ----
            if (ax < abHalf)
            {
                int row = y >= 7.5f ? 0 : 1;
                float lTop = row == 0 ? plateBottom - 1f : 7.5f;
                if (y >= lTop - 1f) return RampChar(P, 4 + Mathf.Max(side, 0));
                if (y < (row == 0 ? 8.5f : 5f)) return RampChar(P, 1);
                if (ax > abHalf - 1f) return RampChar(P, 2);
                return RampChar(P, 3 + side);
            }

            // ---- the flanks ----
            return RampChar(D, 3 + side);
        }

        // ------------------------------------------------------------------ the gauntlets (Gloves)
        //
        // ARM-local: 0 at the shoulder, the elbow cut at -14, the wrist at -22, the hand below. Row
        // -12 is repeated by the rig to lengthen the upper arm, so only plain plate sits there.
        // Three lames down the upper arm; a COUTER with a spike swept back and out (so each arm has
        // its own grid - the spike points OUTWARD on both); a long vambrace with a ridge; a flared
        // cuff; a plated fist whose fingers end in CLAWS. A vein runs from the couter down the
        // vambrace and splits over the back of the hand toward the claws; on the FAR arm - the
        // curse's side - a tongue of flame runs down the upper arm as well.

        const int CorGauntletTop = 0, CorGauntletBottom = -38, CorGauntletHalf = 8;

        /// <summary>The far arm's: down the outside of the upper arm from under the pauldron.</summary>
        static readonly Vector2[] CorUpperArmTongue = { new(1.6f, -1f), new(2.8f, -5f), new(2.2f, -9f), new(3f, -12.6f) };

        static string[] _corGauntletNear, _corGauntletFar;
        static string[] CorGauntletNear => _corGauntletNear ??=
            PaintField(-CorGauntletHalf, CorGauntletHalf, CorGauntletBottom, CorGauntletTop, (ix, iy) => CorGauntletTexel(ix, iy, -1));
        static string[] CorGauntletFar => _corGauntletFar ??=
            PaintField(-CorGauntletHalf, CorGauntletHalf, CorGauntletBottom, CorGauntletTop, (ix, iy) => CorGauntletTexel(ix, iy, 1));

        static char CorGauntletTexel(int ix, int iy, int outward)
        {
            const string P = CorPlate, D = CorDark;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            float o = x * outward;                                                       // + is outward
            int side = LitSide(x, 1.5f, -2f);

            // The vein: from the couter down the vambrace, splitting over the hand.
            var vein = new Vector2[] { new(1.6f * outward, -18.6f), new(2.2f * outward, -22.5f), new(1.4f * outward, -26.5f), new(0.6f * outward, -29.5f) };
            var toClawIn = new Vector2[] { new(0.6f * outward, -29.5f), new(-1.2f * outward, -32f), new(-2.2f * outward, -33.6f) };
            var toClawOut = new Vector2[] { new(0.6f * outward, -29.5f), new(2.2f * outward, -32f), new(2.6f * outward, -33.6f) };

            // ---- the rerebrace: three lames ----
            if (iy >= -13)
            {
                if (ax > 4.7f) return '.';
                if (iy == -4 || iy == -9) return RampChar(P, 1);                         // each lame's lower edge
                if (iy == -5 || iy == -10) return RampChar(P, 4 + Mathf.Max(side, 0));
                if (outward > 0)
                {
                    char m = CorStroke(x, y, CorUpperArmTongue, 0.85f, 0.3f);
                    if (m != '\0') return m;
                }
                return RampChar(P, 3 + side);
            }

            // ---- the couter, its spike swept back and out ----
            {
                float dx = (x - 0.3f * outward) / 4.6f, dy = (y + 16.6f) / 3.3f;
                float r2 = dx * dx + dy * dy;
                // The spike: a wedge from the cop's outer side, out and down.
                float sAlong = (o - 2.5f) / 5.5f;
                bool spike = sAlong > 0f && sAlong < 1f
                             && Mathf.Abs(y - (-16.2f - sAlong * 3.6f)) < 1.6f * (1f - sAlong) + 0.2f;
                if (r2 < 1f || spike)
                {
                    if (spike && r2 >= 1f) return RampChar(P, sAlong > 0.6f ? 2 : outward > 0 ? 4 : 3);
                    char m = CorStroke(x, y, vein, 0.55f, 0.5f, emberFrom: 2f);
                    if (m != '\0') return m;
                    if (dy < -0.62f) return RampChar(P, 1);
                    if (dx * outward > -0.1f && dx * outward < 0.45f && dy > 0f && dy < 0.65f) return RampChar(P, 5);
                    return RampChar(P, 3 + side);
                }
            }

            // ---- the vambrace ----
            if (iy >= -27)
            {
                float half = 4.6f - (-20f - y) * 0.05f;
                if (ax > half) return '.';
                char m = CorStroke(x, y, vein, 0.55f, 0.5f, emberFrom: 2f);
                if (m != '\0') return m;
                if (ix == 0) return RampChar(P, 5);                                     // the ridge
                if (ix == -1) return RampChar(P, 2);
                return RampChar(P, 3 + side);
            }

            // ---- the cuff, flared ----
            if (iy >= -29)
            {
                if (ax > 5.4f) return '.';
                char m = CorStroke(x, y, vein, 0.55f, 0.5f, emberFrom: 2f);
                if (m != '\0') return m;
                return RampChar(P, iy == -28 ? 4 + Mathf.Max(side, 0) : 1);
            }

            // ---- the fist ----
            if (iy >= -34)
            {
                float half = iy < -32 ? 3.9f : 4.6f;
                if (ax > half) return '.';
                char m = CorStroke(x, y, toClawIn, 0.55f, 0.45f, emberFrom: 0.5f);
                if (m == '\0') m = CorStroke(x, y, toClawOut, 0.55f, 0.45f, emberFrom: 0.5f);
                if (m != '\0') return m;
                if (iy == -30) return RampChar(P, 4 + Mathf.Max(side, 0));             // the knuckle plate
                if (ix == -2 || ix == 0 || ix == 2) return RampChar(D, 1);
                return RampChar(D, 3 + Mathf.Max(side, 0));
            }

            // ---- the claws: three, tapering to points ----
            float t = (-34f - y) / 3.4f;                                                // 0 at the knuckle, 1 at the point
            if (t >= 1f) return '.';
            foreach (float cx in new[] { -2.6f, 0f, 2.6f })
                if (Mathf.Abs(x - cx - t * 0.6f * outward) < 1.1f * (1f - t) + 0.15f)
                    return RampChar(P, t < 0.4f ? 3 : 2);
            return '.';
        }

        // ------------------------------------------------------------------ the faulds (Belt)
        //
        // Torso-local, on the belt's band. A plate BAND at the waist, and under it the armoured
        // skirt SPLIT down the front - the dress's front panel shows in the gap - and running down
        // each side as THREE PLATES, the reference's: a long one over the front of the thigh, a
        // shorter one over the hip, a shorter one again turning away behind, each overlapping the
        // one behind it and cut to a shallow point at its foot. Each carries a vein down its front
        // edge that turns along its foot and dies out at the outer corner - the reference's lines
        // tracing its plates. A first pass (one short plate a side and a pointed plate over the
        // middle) had the ratio wrong: the reference's plates are the long part, the band the
        // short one, and nothing closes the front.
        //
        // THE FAR SIDE STOPS AT THE BODY'S EDGE above where the far fist hangs (it draws under this
        // layer - the Herald's finding) and flares only below it; its third plate is round the far
        // hip, out of sight, and is not drawn.

        const int CorFauldTop = 10, CorFauldBottom = -20, CorFauldHalf = 20;

        static string[] _corFauldRows;
        static string[] CorFauldRows => _corFauldRows ??=
            PaintField(-CorFauldHalf, CorFauldHalf, CorFauldBottom, CorFauldTop, CorFauldTexel);

        /// <summary>Each side's plates, front first: inner and outer |x| at the band, how fast
        /// the outer edge flares toward the foot, and the foot's height at the plate's middle.</summary>
        static readonly (float In, float Out, float Flare, float Foot)[] CorFauldPlates =
        {
            (2.6f, 8.4f, 0.12f, -17f),      // the front of the thigh
            (8.0f, 13.0f, 0.18f, -14f),     // the hip
            (12.6f, 16.4f, 0.22f, -11f),    // turning away behind
        };

        /// <summary>Whether (x, y) is on plate <paramref name="k"/> of its side, and where across it
        /// (0 its inner edge, 1 its outer) and how far above its foot.</summary>
        static bool CorFauldPlateIn(float x, float y, int k, out float u, out float aboveFoot)
        {
            var (inner, outer, flare, foot) = CorFauldPlates[k];
            float ax = Mathf.Abs(x), drop = Mathf.Max(0f, 5f - y);
            float i0 = inner + drop * flare * 0.25f, o0 = outer + drop * flare;
            u = (ax - i0) / (o0 - i0);
            float hem = foot + Mathf.Abs(u - 0.5f) * 3f;          // a shallow point at the middle
            aboveFoot = y - hem;
            if (y >= 5f || u < 0f || u > 1f || aboveFoot < 0f) return false;
            // The far side: inside the body's edge until the fist is passed, then free.
            if (x > 0f && ax > TalBodyEdge(y) + 0.5f + Mathf.Max(0f, -2f - y) * 0.9f) return false;
            return true;
        }

        static char CorFauldTexel(int ix, int iy)
        {
            const string P = CorPlate;
            float x = ix + 0.5f, y = iy + 0.5f;
            bool near = x < 0f;
            int side = LitSide(x, 6f, -7f);
            float farEdge = TalBodyEdge(y) + 0.5f;

            // ---- the band ----
            if (iy >= 5 && iy < 9 && x >= -14.5f && x <= farEdge)
            {
                if (iy == 8) return RampChar(P, 4);
                if (iy == 5) return RampChar(P, 1);
                if (ix == -1 || ix == 0) return RampChar(P, ix == 0 ? 4 : 2);       // the clasp at the split
                return RampChar(P, 3 + side);
            }

            // ---- the plates: front over hip over behind ----
            for (int k = 0; k < CorFauldPlates.Length; k++)
            {
                if (!near && k == 2) break;
                if (!CorFauldPlateIn(x, y, k, out float u, out float aboveFoot)) continue;
                float w = (CorFauldPlates[k].Out - CorFauldPlates[k].In) + Mathf.Max(0f, 5f - y) * CorFauldPlates[k].Flare * 0.75f;
                float fromInner = u * w;

                // The vein: down the FRONT plate's edge from the split, then along each plate's
                // foot, dying out on the last. Traced down every plate's front edge, the row of
                // U-shapes read as tubes.
                if (k == 0 && y < 3.5f && fromInner >= 1f && fromInner < 2f && aboveFoot >= 2f) return CorVein;
                if (aboveFoot >= 1f && aboveFoot < 2f && fromInner >= (k == 0 ? 1f : 0.5f) && u < 0.9f)
                    return k == 2 || (!near && k == 1) || u > 0.7f ? CorEmber : CorVein;

                if (y >= 4f) return RampChar(P, 0);                                     // the band's shadow on it
                if (y >= 3f) return RampChar(P, 4);                                     // its lit top
                if (aboveFoot < 1f) return RampChar(P, 1);                              // the foot's rim
                // Each plate a separate curve: a lit leading edge, a dark seam where it overlaps the
                // plate behind. Shaded only a step darker plate by plate, the three ran together.
                if (u > 0.88f) return RampChar(P, 0);
                if (u < 0.14f) return RampChar(P, near ? 4 : 3);
                int face = k == 0 ? 3 + side : near ? (k == 1 ? 3 : 2) : 4;
                return RampChar(P, Mathf.Clamp(face - (u > 0.66f ? 1 : 0), 1, 4));
            }
            return '.';
        }

        // ------------------------------------------------------------------ the skirt (Legs)
        //
        // TORSO-local on RigLayer.Tasset, hung from the belt: the reference's DRESS, floor length
        // and flaring, CLOSED - a FRONT PANEL of the same cloth hangs from under the faulds' split,
        // widening to the legs' own span, to a hem cut at mid-shin, so the greaves and sabatons show
        // beneath it as the legs stride (narrower, it left both legs under the side panels). The first pass left the front open from the crotch down and the user read
        // it as a piece of fabric missing (it is there in the reference). The sides' hem is TORN
        // in big teeth (the Hellspawn rule: small tears close under the outline).
        //
        // THE FAR SIDE holds to the body's edge down past the fist, then flares - the far arm
        // draws under this layer, and the Herald buried its hand with a panel out past the hip.
        //
        // The marks: a vein down each edge of the front panel from the faulds' split, forking out
        // onto the side, dying out before the hem; tongues of flame licking up from the side hems.

        const int CorSkirtTop = 6, CorSkirtBottom = -48, CorSkirtHalf = 26;

        static string[] _corSkirtRows;
        static string[] CorSkirtRows => _corSkirtRows ??=
            PaintField(-CorSkirtHalf, CorSkirtHalf, CorSkirtBottom, CorSkirtTop, CorSkirtTexel);

        static float CorSkirtOuter(float y, bool nearSide)
            => nearSide ? 12.5f + Mathf.Max(0f, 3f - y) * 0.22f
                        : y >= -2.5f ? 8.5f : Mathf.Min(23f, 8.5f + (-2.5f - y) * 0.36f);

        /// <summary>The front panel's half-width at y: the faulds' split at the top, widening a
        /// little to the hem.</summary>
        static float CorFrontEdge(float y) => 4.2f + Mathf.Clamp01((-2f - y) / 32f) * 5.4f;

        /// <summary>The front panel's hem: higher than the sides', a shallow point at its middle
        /// and a nick either side of it.</summary>
        static float CorFrontHem(float x)
        {
            float ax = Mathf.Abs(x);
            float hem = -35.5f - Mathf.Max(0f, 3f - ax) * 0.8f;
            float nick = 1f - Mathf.Abs(ax - 5.4f) / 1.3f;
            if (nick > 0f) hem += nick * 2.4f;
            return hem;
        }

        /// <summary>The sides' torn hem: teeth of different depths, and a deep V a side.</summary>
        static float CorSkirtHem(float x)
        {
            float ax = Mathf.Abs(x);
            float saw = Mathf.Abs(WrapMod(Mathf.FloorToInt(x * 0.5f + 40f), 3) - 1f);   // 0..1, every six texels
            float hem = -43.5f + saw * 2.6f;
            float v = 1f - Mathf.Abs(ax - 18.5f) / 2.6f;                                 // the deep V
            if (v > 0f) hem += v * 6f;
            return hem;
        }

        static Vector2[] CorPanelVein(int s) => new Vector2[]
        {
            new(s * (CorFrontEdge(-3f) - 1.4f), -3f), new(s * (CorFrontEdge(-12f) - 1.4f), -12f),
            new(s * (CorFrontEdge(-22f) - 1.4f), -22f), new(s * (CorFrontEdge(-28f) - 1.6f), -28f),
            new(s * (CorFrontEdge(-31f) - 2.4f), -31.4f),
        };

        static Vector2[] CorPanelFork(int s) => new Vector2[]
        {
            new(s * (CorFrontEdge(-15f) - 1.2f), -15f), new(s * (CorFrontEdge(-15f) + 2.6f), -19.5f),
            new(s * (CorFrontEdge(-15f) + 4.2f), -25f), new(s * (CorFrontEdge(-15f) + 5.4f), -28.5f),
        };

        static char CorSkirtTexel(int ix, int iy)
        {
            const string C = CorCloth;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            bool nearSide = x < 0f;
            int s = nearSide ? -1 : 1;
            if (y >= 4f) return '.';

            float outer = CorSkirtOuter(y, nearSide), front = CorFrontEdge(y);
            if (ax > outer) return '.';
            bool onFront = ax < front;
            if (y < (onFront ? CorFrontHem(x) : CorSkirtHem(x))) return '.';

            // ---- the marks ----
            char m = CorStroke(x, y, CorPanelVein(s), 0.55f, 0.45f, emberFrom: 0.84f);
            if (m == '\0') m = CorStroke(x, y, CorPanelFork(s), 0.5f, 0.42f, emberFrom: 0.62f);
            if (m == '\0' && !onFront && y < CorSkirtHem(x) + 9f)
            {
                // Tongues licking up from the side hems, one in every other tooth.
                float cell = Mathf.Floor((x + 40f) / 6f);
                if (((int)cell & 1) == 0)
                {
                    float cx = cell * 6f - 40f + 3f, baseY = CorSkirtHem(cx);
                    var tongue = new Vector2[] { new(cx, baseY + 0.5f), new(cx + 0.6f, baseY + 3.5f), new(cx - 0.2f, baseY + 6.5f), new(cx + 0.4f, baseY + 8.2f) };
                    if (Mathf.Abs(cx) >= CorFrontEdge(baseY) + 3f && Mathf.Abs(cx) <= CorSkirtOuter(baseY, cx < 0f) - 2f)
                        m = CorStroke(x, y, tongue, 1.0f, 0.3f, emberFrom: 0.62f);
                }
            }
            if (m != '\0') return m;

            if (y >= 1f) return RampChar(C, 1);                                        // under the faulds

            // ---- the front panel ----
            if (onFront)
            {
                if (y < CorFrontHem(x) + 1f) return RampChar(C, 1);                     // its hem
                if (ax > front - 1f) return RampChar(C, nearSide ? 2 : 4);              // its edges, the far one lit
                if (ix == 0) return RampChar(C, 2);                                     // the fold down its middle
                return RampChar(C, x > 0f ? 4 : 3);
            }

            // ---- the sides ----
            if (ax < front + 1f) return RampChar(C, 1);                                 // the shadow under the panel's edge
            if (y < CorSkirtHem(x) + 1f) return RampChar(C, 1);                         // the frayed hem
            if (ax > outer - 1f) return RampChar(C, nearSide ? 2 : 4);                  // the outer edges
            // Folds widening toward the hem.
            float fold = Mathf.Sin(ax * 0.85f + 0.7f) * Mathf.Clamp01((-4f - y) / 26f);
            int tone = 3 + (fold > 0.5f ? 1 : fold < -0.55f ? -1 : 0);
            return RampChar(C, Mathf.Clamp(tone, 1, 4));
        }

        // ------------------------------------------------------------------ the greaves (Boots)
        //
        // LEG-local, the sole at -48, the knee at -24. A KITE knee cop pointing down the shin, a
        // greave with a lit ridge, and a SABATON drawn out to a point in front (+X, the way the
        // character faces - both legs, one grid). The marks climb from the HEEL up the back of the
        // greave and branch toward the knee - cracks rising through stone.

        const int CorGreaveMinX = -6, CorGreaveMaxX = 12, CorGreaveTop = -14, CorGreaveBottom = -48;

        static string[] _corGreaveRows;
        static string[] CorGreaveRows => _corGreaveRows ??=
            PaintField(CorGreaveMinX, CorGreaveMaxX, CorGreaveBottom, CorGreaveTop, CorGreaveTexel);

        static bool CorKneeIn(float x, float y)
        {
            // A kite: widest a little above the knee, pointed at both ends.
            float w = y >= -21.5f ? (y - -15.5f) / -6f : (y - -31.5f) / 10f;
            return y < -15.5f && y > -31.5f && Mathf.Abs(x) < 5.6f * Mathf.Clamp01(w);
        }

        static readonly Vector2[] CorGreaveCrack =
            { new(-4.2f, -46.2f), new(-3.4f, -41.5f), new(-1.6f, -37.2f), new(-2.4f, -33.4f), new(-0.8f, -30.2f) };
        static readonly Vector2[] CorGreaveBranchA = { new(-3.4f, -41.5f), new(-4.4f, -37.6f), new(-3.8f, -35f) };
        static readonly Vector2[] CorGreaveBranchB = { new(-1.6f, -37.2f), new(1.2f, -35.6f), new(1.8f, -32.6f) };

        static char CorGreaveTexel(int ix, int iy)
        {
            const string P = CorPlate, D = CorDark;
            float x = ix + 0.5f, y = iy + 0.5f, ax = Mathf.Abs(x);
            int side = LitSide(x, 1.5f, -2f);

            char Mark()
            {
                char m = CorStroke(x, y, CorGreaveCrack, 0.6f, 0.45f, emberFrom: 0.86f);
                if (m == '\0') m = CorStroke(x, y, CorGreaveBranchA, 0.5f, 0.42f, emberFrom: 0.55f);
                if (m == '\0') m = CorStroke(x, y, CorGreaveBranchB, 0.5f, 0.42f, emberFrom: 0.55f);
                return m;
            }

            // ---- the knee cop ----
            if (CorKneeIn(x, y))
            {
                char m = Mark();
                if (m != '\0') return m;
                if (!CorKneeIn(x, y + 1f)) return RampChar(P, x >= 0f ? 5 : 4);
                if (!CorKneeIn(x - 1f, y) || !CorKneeIn(x + 1f, y)) return RampChar(P, x > 0f ? 4 : 1);
                if (!CorKneeIn(x, y - 1f)) return RampChar(P, 1);
                if (ix == 0) return RampChar(P, 5);                                      // the ridge
                if (ix == -1) return RampChar(P, 2);
                return RampChar(P, 3 + side);
            }
            if (y >= -26f && y < -14f) return ax < 4.6f && y < -17f ? RampChar(P, 0) : '.';   // behind the cop

            // ---- the sabaton: the foot, drawn out to a point in front ----
            if (iy < -42)
            {
                float top = x < 3f ? -42f : -42f - (x - 3f) * 0.62f;
                if (x < -5f || x > 10.2f || y >= top) return '.';
                char m = Mark();
                if (m != '\0') return m;
                if (iy == -48) return RampChar(D, 0);                                    // the sole
                if (y >= top - 1f) return RampChar(P, x > 2f ? 5 : 4);
                if (iy == -45 && x < 3.5f) return RampChar(P, 1);                       // a lame across the instep
                return RampChar(P, 3 + side);
            }

            // ---- the greave ----
            float half = 4.6f - (-32f - y) * 0.04f;
            if (ax > half) return '.';
            {
                char m = Mark();
                if (m != '\0') return m;
            }
            if (ix == 1) return RampChar(P, 5);                                         // the shin ridge
            if (ix == 0) return RampChar(P, 2);
            return RampChar(P, 3 + side);
        }

        // ====================================================== the Aether Greatsword
        //
        // The set's sword, Black Diamond, power 0 - bespoke art and nothing else (the cosmetic
        // weapons' rule). After the reference's black sword: a long black blade with a straight
        // edge and a long point, a FULLER LINE burning down its middle from a row of RINGS above the
        // guard to an ARROWHEAD short of the point, a cross-guard whose own line crosses the blade's
        // at a hot heart, a black wrapped grip and a kite pommel with a spark in it. Every line is
        // the Secret Fire's, so the sword burns with the armour, on its beat, in its element.
        //
        // Built as ONE field over authored cells (x across the 28-cell canvas, y down from the
        // point) sampled at the arena's density and at the menu's (Prism's way), so the two can
        // never disagree. The family's height: BladeRowsFor works out the blade from everything
        // else, so the finished sprite stands exactly SwordHeightTexels.

        const int AetherTipRows = 14, AetherGuardRows = 6, AetherPommelRows = 4;
        static int AetherBladeRows => BladeRowsFor(AetherTipRows + AetherGuardRows + SwordGripRows + AetherPommelRows);
        static int AetherGuardTop => AetherTipRows + AetherBladeRows;
        static int AetherGripTop => AetherGuardTop + AetherGuardRows;
        static int AetherPommelTop => AetherGripTop + SwordGripRows;
        static int AetherHeightRows => AetherPommelTop + AetherPommelRows;

        static Vector2Int AetherGrip => new(13, GripCentre(AetherGripTop, SwordGripRows));
        static Vector2Int AetherDetailGrip => new(AetherGrip.x * EmberDetailScale, AetherGrip.y * EmberDetailScale);

        static string[] _corviRows, _corviDetail;
        static string[] AetherRows => _corviRows ??= AetherSample(1);
        static string[] AetherDetail => _corviDetail ??= AetherSample(EmberDetailScale);

        static void AddAetherGreatsword(List<GearItem> items)
        {
            // The armour's blackened steel (a bright, cold sheen read as black diamond there); the
            // guard and pommel the same metal a touch warmer, so the hilt reads as its own forging;
            // a black wrap on the grip.
            var blade = new Palette.Ramp(new Color(0.14f, 0.14f, 0.16f), lift: 0.30f, shade: 0.34f, line: 0.82f)
                .WithShadowsBelowLight(0.34f, 0.22f)
                .WithHighlightsToward(new Color(0.54f, 0.55f, 0.58f), 0.34f);
            var hilt = new Palette.Ramp(new Color(0.17f, 0.16f, 0.17f), lift: 0.28f, shade: 0.34f, line: 0.82f)
                .WithShadowsBelowLight(0.34f, 0.22f)
                .WithHighlightsToward(new Color(0.56f, 0.55f, 0.56f), 0.30f);
            var wrap = new Palette.Ramp(new Color(0.09f, 0.09f, 0.11f), lift: 0.26f, shade: 0.40f, line: 0.85f)
                .WithShadowsBelowLight(0.46f, 0.30f);
            var pal = SecretFire.Kindle(Palette.Of(blade, hilt, wrap));

            var sword = WithMenu(Make("aether_greatsword", "Aether Greatsword", GearSlot.Weapon, LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.aether_greatsword", AetherRows, pal, 0f, -10f,
                       pivotTexel: AetherGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.aether_greatsword.menu", AetherDetail, pal, 0f, -10f,
                       pivotTexel: AetherDetailGrip, ppu: MenuPpu));
            items.Add(Kindle(sword));
        }

        /// <summary>The whole sword sampled at <paramref name="k"/> texels per authored cell.</summary>
        static string[] AetherSample(int k)
        {
            var rows = new string[AetherHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[SwordCanvas * k];
                float y = (Y + 0.5f) / k;
                for (int X = 0; X < line.Length; X++)
                    line[X] = AetherTexel((X + 0.5f) / k, y, k > 1);
                rows[Y] = new string(line);
            }
            return rows;
        }

        /// <summary>The blade's half-width at y (cells down from the point): a long point, then
        /// straight to the guard.</summary>
        static float AetherHalf(float y)
        {
            float full = SwordBladeWidth * 0.5f;
            if (y < AetherTipRows) return full * Mathf.Pow(Mathf.Clamp01(y / AetherTipRows), 0.9f);
            return full;
        }

        /// <summary>Where the rings sit (their centres, cells down from the point) - a row of them
        /// just above the guard, threaded on the fuller's line.</summary>
        static float[] AetherRingCentres
        {
            get
            {
                const int n = 4;
                var c = new float[n];
                for (int i = 0; i < n; i++) c[i] = AetherGuardTop - 3.4f - i * 3.9f;
                return c;
            }
        }

        static char AetherTexel(float x, float y, bool fine)
        {
            float dx = x - SwordAxis, adx = Mathf.Abs(dx);
            float lineHalf = fine ? 0.8f : 1f;                    // the fuller's line: two cells

            // ---- the blade ----
            if (y < AetherGuardTop)
            {
                float half = AetherHalf(y);
                if (adx >= half) return '.';

                // The rings, threaded on the line just above the guard.
                foreach (float rc in AetherRingCentres)
                {
                    float rx = dx, ry = y - rc, rr = Mathf.Sqrt(rx * rx + ry * ry);
                    if (rr < 2.05f)
                        return rr > 0.95f ? (rr > 1.7f ? CorEmber : CorVein) : AetherSteel(dx, y, half, fine);
                }
                float ringsTop = AetherRingCentres[AetherRingCentres.Length - 1] - 2f;
                float arrowY = AetherTipRows + 1.5f;               // the arrowhead's point

                // The arrowhead: a chevron pointing at the tip, its point hot.
                float back = y - arrowY;
                if (back >= -0.5f && back < 4.2f && Mathf.Abs(adx - back * 0.62f) < (fine ? 0.55f : 0.75f))
                    return back < 1.2f ? CorCore : back > 3f ? CorEmber : CorVein;

                // The fuller's line, from the rings to the arrowhead.
                if (adx < lineHalf && y > arrowY && y < ringsTop + 0.5f) return CorVein;

                return AetherSteel(dx, y, half, fine);
            }

            // ---- the guard: a cross whose arms taper to points, its line crossing the blade's ----
            float gy = y - (AetherGuardTop + 2.5f);
            float armHalf = Mathf.Lerp(1.6f, 0.5f, Mathf.Clamp01((adx - 3f) / 5f));
            float reach = SwordGuardWidth * 0.5f;
            bool arm = adx < reach && Mathf.Abs(gy) < armHalf;
            bool boss = adx / 3f + Mathf.Abs(gy) / 3.2f < 1f;
            if (y < AetherGripTop && (arm || boss))
            {
                if (Mathf.Abs(gy) < (fine ? 0.35f : 0.5f) && adx < reach - 0.6f)
                    return adx < 1.2f ? CorCore : adx > reach - 2.6f ? CorEmber : CorVein;
                if (adx < lineHalf && boss) return CorVein;        // the blade's line, down through the boss
                return AetherHilt(dx, gy, fine, boss ? 3.2f : armHalf);
            }

            // ---- the grip: black wrap, grooves round it ----
            if (y >= AetherGripTop && y < AetherPommelTop)
            {
                if (adx >= SwordGripWidth * 0.5f) return '.';
                float wrapPhase = (y + dx * 0.3f) / (fine ? 0.85f : 1.25f);
                if (wrapPhase - Mathf.Floor(wrapPhase) < (fine ? 0.22f : 0.34f)) return '2';
                return dx < -0.6f ? '5' : dx > 0.6f ? '3' : '4';
            }

            // ---- the pommel: a kite pointing down, a spark at its heart ----
            if (y >= AetherPommelTop && y < AetherHeightRows)
            {
                float py = y - AetherPommelTop;                    // 0..PommelRows
                float w = py < 1.4f ? 1.6f + py * 1.1f : 3.1f * (1f - (py - 1.4f) / (AetherPommelRows - 1.4f));
                if (adx >= w) return '.';
                float sx = dx, sy = py - 1.6f;
                if (sx * sx + sy * sy < (fine ? 0.45f : 0.6f)) return CorCore;
                return AetherHilt(dx, py - 1.6f, fine, w);
            }
            return '.';
        }

        /// <summary>The blade's steel: a lit bevel down the -X edge, a shaded one down the +X, the
        /// black flat between them, a glint near the point.</summary>
        static char AetherSteel(float dx, float y, float half, bool fine)
        {
            float bevel = fine ? 1.25f : 1.5f;
            float fromLeft = dx + half, fromRight = half - dx;
            if (fromLeft < bevel) return y < AetherTipRows + 5f && fromLeft < bevel * 0.6f ? 'h' : 'l';
            if (fromRight < bevel) return 'd';
            if (fine && fromLeft < bevel + 0.3f) return 'b';
            return Mathf.Abs(dx) < 2.2f ? 's' : 'b';
        }

        /// <summary>The guard's and pommel's metal, lit from the upper left like the blade's bevel.</summary>
        static char AetherHilt(float dx, float dy, bool fine, float halfH)
        {
            if (dy < -halfH + (fine ? 0.45f : 0.75f)) return 'L';                   // its top face catches the light
            if (dy > halfH - (fine ? 0.45f : 0.75f)) return 'S';                    // its underside
            return dx < 0f ? 'B' : 'D';
        }
    }
}
