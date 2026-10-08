using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the Aether Longbow: a bow of the Secret Fire
    //
    // The Aether set's bow, Black Diamond, power 0 - bespoke art and nothing else (the cosmetic
    // weapons' rule). After the user's reference (an archer on a rooftop holding a bow of jagged red
    // energy): limbs that run out from the grip in LIGHTNING STEPS - a long stroke out, a hard step
    // back in, again - with a spike off each step's outer corner pointing back toward the hand, and
    // a long sweep to a fine tip. A thread of the same energy for a string.
    //
    // THE WHOLE BOW IS THE SECRET FIRE'S, all but the grip: limbs, spikes and string are mark texels
    // (Core along a hot spine near the grip, Vein for the body, Ember at the edges, the dying tips
    // and the string). So at the bottom of the set's beat it is a BLACK jagged bow, like the plate
    // it is carried with, and at the top it burns in the attuned element - on the same clock as
    // every other mark in the game, with no state of its own. The grip is the greatsword's
    // blackened steel with its black wrap: the one part of the bow that is a made thing.
    //
    // The limbs are polylines (CorAlong, the curse marks' own measure), upper and lower drawn
    // separately: mirrored, lightning reads as constructed (Cosmetic weapons' lesson). The steps
    // are SHARP - a soft zigzag read as a wobbly stick.
    //
    // One field over authored cells (x across the canvas, string at the left, the bow's back to
    // the right; y down from the upper tip) sampled for the arena and (x4) the menu, so the two
    // can never disagree. As tall as every greatsword (SwordHeightRows).
    public static partial class DemoGear
    {
        const int PrimaCanvas = 22;
        static int PrimaHeightRows => SwordHeightRows;

        /// <summary>The middle of the wrapped grip - the bow's pivot, a little below its middle like
        /// a real bow's.</summary>
        const float PrimaGripX = 11f, PrimaGripY = 40f;
        const float PrimaStringX = 1.5f;

        static Vector2Int PrimaGrip => new((int)PrimaGripX, (int)PrimaGripY);
        static Vector2Int PrimaDetailGrip => new(PrimaGrip.x * EmberDetailScale, PrimaGrip.y * EmberDetailScale);

        static string[] _primaRows, _primaDetail;
        static string[] PrimaRows => _primaRows ??= PrimaSample(1);
        static string[] PrimaDetail => _primaDetail ??= PrimaSample(EmberDetailScale);

        // Lazy: static arrays in the partial files must not depend on another file's statics
        // being initialised first (Tria Prima's note).
        static Vector2[] _primaUpper, _primaLower;
        static (Vector2[] Pts, float W0, float W1)[] _primaSpikes;

        /// <summary>The upper limb, grip to tip: out, step in, out, step in, then the long sweep.</summary>
        static Vector2[] PrimaUpper => _primaUpper ??= new Vector2[]
        {
            new(11f, 35f), new(14.2f, 27f), new(11.2f, 25.2f), new(15f, 17f), new(12f, 15.2f),
            new(12.6f, 10f), new(8f, 4.5f), new(2f, 1f),
        };

        /// <summary>The lower limb - its own steps, not the upper's mirrored.</summary>
        static Vector2[] PrimaLower => _primaLower ??= new Vector2[]
        {
            new(11f, 45f), new(14.6f, 53f), new(11.6f, 55f), new(15.4f, 63f), new(12.4f, 64.8f),
            new(9f, 71f), new(2f, 76f),
        };

        /// <summary>A spike off each step's outer corner, raking back toward the grip.</summary>
        static (Vector2[] Pts, float W0, float W1)[] PrimaSpikes => _primaSpikes ??= new[]
        {
            (new Vector2[] { new(13.6f, 27f), new(20.6f, 31.2f) }, 1.3f, 0.3f),
            (new Vector2[] { new(14.4f, 17f), new(20f, 20.2f) }, 1.1f, 0.3f),
            (new Vector2[] { new(14f, 53f), new(20.8f, 49.4f) }, 1.3f, 0.3f),
            (new Vector2[] { new(14.8f, 63f), new(19.8f, 60f) }, 1.1f, 0.3f),
        };

        static void AddPrimaMateria(List<GearItem> items)
        {
            // The Aether Greatsword's steel and wrap, so the two read as one forge.
            var steel = new Palette.Ramp(new Color(0.14f, 0.14f, 0.16f), lift: 0.30f, shade: 0.34f, line: 0.82f)
                .WithShadowsBelowLight(0.34f, 0.22f)
                .WithHighlightsToward(new Color(0.54f, 0.55f, 0.58f), 0.34f);
            var hilt = new Palette.Ramp(new Color(0.17f, 0.16f, 0.17f), lift: 0.28f, shade: 0.34f, line: 0.82f)
                .WithShadowsBelowLight(0.34f, 0.22f);
            var wrap = new Palette.Ramp(new Color(0.09f, 0.09f, 0.11f), lift: 0.26f, shade: 0.40f, line: 0.85f)
                .WithShadowsBelowLight(0.46f, 0.30f);
            var pal = SecretFire.Kindle(Palette.Of(steel, hilt, wrap));

            // Id "prima_materia_bow": "prima_materia" is already an exchange boon's id.
            var bow = WithMenu(Bow(Make("prima_materia_bow", "Aether Longbow", GearSlot.Weapon, LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.prima_materia", PrimaRows, pal, 0f, 0f,
                       pivotTexel: PrimaGrip, ppu: FinePpu, upscale2x: true))),
                Pixels(RigLayer.Weapon, "gear.weapon.prima_materia.menu", PrimaDetail, pal, 0f, 0f,
                       pivotTexel: PrimaDetailGrip, ppu: MenuPpu));
            bow.SignatureFinisher = PrimaMateriaArtId;
            items.Add(Kindle(bow));
            AddPrimaStone(items);
        }

        // ------------------------------------------------------------------ the stone
        //
        // The Aether Longbow's RELIC: a Philosopher's Stone, the LIQUID (the user's pick for the bow,
        // 2026-10-07 - the cracked shard is the Aether Greatsword's, the geode the King and
        // Queen's). A glossy drop of light, element-coloured all through, with a few droplets
        // flung off it. The whole body is the Secret Fire's LIQUID (SecretFire.KindleLiquid): it
        // swells and ebbs on the one beat but never below SecretFire.LiquidFloor - a stone loses
        // its glow, it never goes out. It carries PRIMA MATERIA, and is what the character
        // throws up while the bow is in the air.

        public const string PrimaMateriaArtId = "prima_materia_art";
        public const string PrimaStoneId = "prima_stone";

        // 8 x 13, the user's belt grid: h highlight, 4 light, 3 body, 2 shade (no deep tone is
        // used at this size), w a droplet - the highlight's light, loose.
        static readonly string[] PrimaStoneRows =
        {
            "...ww...",
            "...33...",
            "..3443..",
            ".34h432.",
            ".3h4432.",
            "w3h4432w",
            ".3h4432.",
            ".34h332.",
            ".344332.",
            ".333322.",
            "..3222..",
            "...22w..",
            "....w...",
        };

        static void AddPrimaStone(List<GearItem> items)
        {
            var pal = SecretFire.KindleLiquid(new Dictionary<char, Color>(), 'h', '4', '3', '2', '1');
            pal['w'] = pal['h'];
            var stone = RelicGrants(Make(PrimaStoneId, "Liquid Stone", GearSlot.Relic, LootTier.BlackDiamond, 0f,
                                         HipRelic("gear.trinket.prima_stone", PrimaStoneRows, pal)),
                                    PrimaMateriaArtId, WeaponClass.Bow, twoHanded: true);
            items.Add(Kindle(stone));
        }

        /// <summary>The whole bow sampled at <paramref name="k"/> texels per authored cell.</summary>
        static string[] PrimaSample(int k)
        {
            var rows = new string[PrimaHeightRows * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                var line = new char[PrimaCanvas * k];
                float y = (Y + 0.5f) / k;
                for (int X = 0; X < line.Length; X++)
                    line[X] = PrimaTexel((X + 0.5f) / k, y, k > 1);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static char PrimaTexel(float x, float y, bool fine)
        {
            // ---- the grip: blackened steel, the black wrap in the middle, chamfered ends ----
            float dx = x - PrimaGripX, adx = Mathf.Abs(dx), ry = Mathf.Abs(y - PrimaGripY);
            float riserHalf = 2f - Mathf.Max(0f, ry - 4.5f) * 0.9f;
            if (ry < 6f && adx < riserHalf)
            {
                if (ry < 3f)
                {
                    float wrapPhase = (y + dx * 0.3f) / (fine ? 0.85f : 1.25f);
                    if (wrapPhase - Mathf.Floor(wrapPhase) < (fine ? 0.22f : 0.34f)) return '2';
                    return dx < -0.6f ? '5' : dx > 0.6f ? '3' : '4';
                }
                if (dx < -riserHalf + 0.9f) return 'l';
                if (dx > riserHalf - 0.9f) return 'd';
                return 'b';
            }

            // ---- the limbs and their spikes ----
            char c = PrimaLimb(x, y, PrimaUpper, 2.7f, 0.55f, fine);
            if (c == '\0') c = PrimaLimb(x, y, PrimaLower, 2.7f, 0.55f, fine);
            if (c == '\0')
                foreach (var s in PrimaSpikes)
                    if ((c = PrimaLimb(x, y, s.Pts, s.W0, s.W1, fine)) != '\0') break;
            if (c != '\0') return c;

            // ---- the string: a thread of the dimmest tone, tip to tip ----
            if (y > 1f && y < PrimaHeightRows - 1f && Mathf.Abs(x - PrimaStringX) < (fine ? 0.3f : 0.5f))
                return CorEmber;
            return '.';
        }

        /// <summary>One energy stroke's letter, or '\0' where it misses: a hot Core spine on its
        /// first stretch, Ember at its edges and its dying tip, Vein between.</summary>
        static char PrimaLimb(float x, float y, Vector2[] pts, float w0, float w1, bool fine)
        {
            float d = CorAlong(x, y, pts, out float t);
            float w = Mathf.Lerp(w0, w1, t);
            if (d > w) return '\0';
            if (t > 0.82f || w - d < (fine ? 0.55f : 0.8f)) return CorEmber;
            if (t < 0.4f && d < (fine ? 0.5f : 0.7f)) return CorCore;
            return CorVein;
        }
    }
}
