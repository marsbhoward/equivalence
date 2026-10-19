using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// Each bow's QUIVER and ARROW (the user's call, 2026-10-16 - they replace the bow's belt vials):
    /// the quiver is worn on the BACK, its fletchings showing over the shoulder
    /// (PrimitiveCharacterRig.SetQuiver); the arrow is at the weapon's density, drawn nocked through
    /// the draw (the rig) and flying (Combat.ThrownArrow). Both are the bow's own - its materials and
    /// palette, both at the BOW's density (75 cells a unit). A bow with no kit of its own (a minted, stat-rolled bow) wears the Hunting Bow's;
    /// the Hive has none - it is made of arrows and looses its own (HiveLoose).
    ///
    /// Arrows lie level, head to the RIGHT, pivoted at their middle, fletching swept back from
    /// the nock (a fan tall in the middle read as a lump inside its outline); quivers stand
    /// upright, pivoted at the mouth. Shaft = the main ramp, head = the second (uppercase), fletching = the
    /// third (digits) - except where a piece says otherwise.
    /// </summary>
    public static partial class DemoGear
    {
        const float ArrowPpu = 75f;
        /// <summary>The quiver grids' rim row - their pivot.</summary>
        const int QuiverMouthRow = 10;

        static void AddQuivers(List<GearItem> items)
        {
            static Color C(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);
            foreach (var item in items)
            {
                if (item == null || item.Class != WeaponClass.Bow) continue;
                switch (item.ItemId)
                {
                    case "hunting_bow":
                    {
                        var wood = new Palette.Ramp(new Color(0.55f, 0.36f, 0.18f));
                        var iron = new Palette.Ramp(new Color(0.55f, 0.57f, 0.62f));
                        var goose = new Palette.Ramp(new Color(0.80f, 0.79f, 0.75f));
                        var dark = new Palette.Ramp(new Color(0.30f, 0.20f, 0.14f));
                        Kit(item, "hunting", HuntingArrowRows, Palette.Of(wood, iron, goose),
                            HuntingQuiverRows, Palette.Of(Palette.Leather, dark, goose));
                        break;
                    }
                    case "artemis_bow":
                    {
                        var blade = Palette.Ramp.FromTones(
                            glow: new Color(0.97f, 0.98f, 1.00f), light: new Color(0.84f, 0.87f, 0.93f),
                            b: new Color(0.70f, 0.74f, 0.82f), dark: new Color(0.58f, 0.62f, 0.71f),
                            deep: new Color(0.40f, 0.42f, 0.50f), line: new Color(0.20f, 0.21f, 0.27f));
                        var frame = new Palette.Ramp(new Color(0.44f, 0.46f, 0.53f), lift: 0.30f);
                        var white = new Palette.Ramp(new Color(0.88f, 0.89f, 0.92f), lift: 0.20f);
                        Kit(item, "artemis", ArtemisArrowRows, Palette.Of(blade, frame, white),
                            ArtemisQuiverRows, Palette.Of(frame, blade, white));
                        break;
                    }
                    case "umbra_bow":
                    {
                        var feather = Palette.Ramp.FromTones(glow: C(96, 108, 168), light: C(68, 70, 118), b: C(46, 44, 80),
                                                             dark: C(30, 27, 50), deep: C(19, 16, 32), line: C(10, 8, 16));
                        var bone = Palette.Ramp.FromTones(glow: C(250, 246, 236), light: C(234, 226, 208), b: C(204, 190, 164),
                                                          dark: C(152, 130, 102), deep: C(98, 76, 56), line: C(40, 28, 22));
                        Kit(item, "umbra", UmbraArrowRows, Palette.Of(feather, bone, feather),
                            UmbraQuiverRows, Palette.Of(feather, bone, feather));
                        break;
                    }
                    case "crow_bow":
                    {
                        // Black leather bound in the string's ember, ONE crow feather laid across it,
                        // three more hanging from the foot gone to ash at the ends; the arrow's head
                        // is the crow's bill, its fletching crow feathers, its nock an ember.
                        var feather = Palette.Ramp.FromTones(glow: C(84, 132, 122), light: C(104, 128, 132), b: C(58, 63, 74),
                                                             dark: C(38, 41, 50), deep: C(22, 24, 30), line: C(8, 8, 11));
                        var ember = Palette.Ramp.FromTones(glow: C(255, 196, 110), light: C(255, 140, 60), b: C(214, 72, 28),
                                                           dark: C(120, 34, 14), deep: C(138, 130, 120), line: C(70, 18, 8));
                        var bill = Palette.Ramp.FromTones(glow: C(196, 192, 184), light: C(150, 146, 140), b: C(104, 100, 96),
                                                          dark: C(62, 60, 58), deep: C(30, 28, 28), line: C(14, 12, 12));
                        var shaft = new Palette.Ramp(C(34, 30, 30), lift: 0.30f);
                        var arrowPal = Palette.Of(shaft, bill, feather);
                        arrowPal['e'] = C(214, 72, 28);
                        // S in the ember ramp is ASH - the frayed feather ends at the foot.
                        Kit(item, "crow", CrowArrowRows, arrowPal, CrowQuiverRows, Palette.Of(feather, ember, feather));
                        break;
                    }
                    case "swan_bow":
                    {
                        // The Crow's kit redrawn in the Swan's colours - the birds' kits are one family:
                        // pale leather, a white feather across it, black bands, the hanging feathers
                        // tipped silver; the arrow pale with a silver head, white fletching and the
                        // bill's orange at the nock.
                        var white = Palette.Ramp.FromTones(glow: C(252, 252, 250), light: C(236, 238, 244), b: C(214, 220, 230),
                                                           dark: C(170, 178, 196), deep: C(124, 132, 156), line: C(60, 66, 84));
                        // The bands are the knob's and the mask's BLACK (silver on white vanished);
                        // S - the hanging feathers' tips - stays silver.
                        var bands = Palette.Ramp.FromTones(glow: C(170, 178, 196), light: C(110, 118, 140), b: C(40, 44, 58),
                                                           dark: C(24, 26, 36), deep: C(206, 214, 228), line: C(12, 12, 18));
                        var silver = Palette.Ramp.FromTones(glow: C(250, 252, 255), light: C(214, 222, 236), b: C(146, 156, 180),
                                                            dark: C(96, 104, 128), deep: C(206, 214, 228), line: C(56, 62, 82));
                        var shaft = new Palette.Ramp(C(214, 204, 186), lift: 0.20f);
                        var arrowPal = Palette.Of(shaft, silver, white);
                        arrowPal['e'] = C(236, 120, 40);
                        Kit(item, "swan", CrowArrowRows, arrowPal, CrowQuiverRows, Palette.Of(white, bands, white));
                        break;
                    }
                    case "peacock_bow":
                    {
                        // The birds' family kit in the Peacock's colours: cobalt leather with a pale
                        // blue feather across it, GOLD bands and gold-tipped hanging feathers, green
                        // fletching; the arrow bronze with a gold head and a cobalt nock.
                        var cobalt = Palette.Ramp.FromTones(glow: C(160, 210, 250), light: C(110, 170, 240), b: C(70, 120, 210),
                                                            dark: C(32, 62, 150), deep: C(20, 36, 100), line: C(10, 14, 40));
                        var gold = Palette.Ramp.FromTones(glow: C(255, 232, 150), light: C(236, 196, 90), b: C(200, 150, 50),
                                                          dark: C(150, 96, 34), deep: C(236, 196, 90), line: C(60, 36, 12));
                        var green = Palette.Ramp.FromTones(glow: C(150, 214, 160), light: C(90, 170, 130), b: C(40, 120, 96),
                                                           dark: C(26, 84, 72), deep: C(16, 52, 48), line: C(10, 20, 24));
                        var bronze = new Palette.Ramp(C(120, 80, 34), lift: 0.30f);
                        var arrowPal = Palette.Of(bronze, gold, green);
                        arrowPal['e'] = C(40, 90, 190);
                        Kit(item, "peacock", CrowArrowRows, arrowPal, CrowQuiverRows, Palette.Of(cobalt, gold, green));
                        break;
                    }
                    case "phoenix_bow":
                    {
                        // The birds' family kit in the Rubedo: crimson leather with a gold feather across
                        // it, gold bands, the hanging feathers' tips BURNING; flame fletching. The arrow
                        // crimson with a gold head, flame fletching and a white-hot nock.
                        var fire = Palette.Ramp.FromTones(glow: C(255, 180, 70), light: C(240, 110, 40), b: C(200, 52, 30),
                                                          dark: C(140, 24, 28), deep: C(84, 14, 20), line: C(30, 6, 10));
                        var gold = Palette.Ramp.FromTones(glow: C(255, 240, 180), light: C(250, 210, 100), b: C(222, 160, 52),
                                                          dark: C(170, 104, 30), deep: C(255, 160, 50), line: C(60, 30, 8));
                        var flame = Palette.Ramp.FromTones(glow: C(255, 248, 214), light: C(255, 214, 90), b: C(255, 140, 40),
                                                           dark: C(226, 70, 24), deep: C(150, 30, 14), line: C(60, 10, 6));
                        var arrowPal = Palette.Of(fire, gold, flame);
                        arrowPal['e'] = C(255, 248, 214);
                        Kit(item, "phoenix", CrowArrowRows, arrowPal, CrowQuiverRows, Palette.Of(fire, gold, flame));
                        break;
                    }
                    case "prima_materia_bow":
                    {
                        var steel = new Palette.Ramp(new Color(0.14f, 0.14f, 0.16f), lift: 0.30f, shade: 0.34f, line: 0.82f)
                            .WithShadowsBelowLight(0.34f, 0.22f)
                            .WithHighlightsToward(new Color(0.54f, 0.55f, 0.58f), 0.34f);
                        var hilt = new Palette.Ramp(new Color(0.17f, 0.16f, 0.17f), lift: 0.28f, shade: 0.34f, line: 0.82f)
                            .WithShadowsBelowLight(0.34f, 0.22f);
                        var wrap = new Palette.Ramp(new Color(0.09f, 0.09f, 0.11f), lift: 0.26f, shade: 0.40f, line: 0.85f)
                            .WithShadowsBelowLight(0.46f, 0.30f);
                        // The Secret Fire's reserved colours, last (SecretFire.Kindle) - the marks light.
                        Kit(item, "aether", AetherArrowRows, SecretFire.Kindle(Palette.Of(steel, wrap)),
                            AetherQuiverRows, SecretFire.Kindle(Palette.Of(steel, hilt, wrap)));
                        break;
                    }
                }
            }
        }

        static void Kit(GearItem bow, string key, string[] arrowRows, Dictionary<char, Color> arrowPal,
                        string[] quiverRows, Dictionary<char, Color> quiverPal)
        {
            bow.ArrowSprite = PixelSprite.From("gear.arrow." + key, arrowRows, arrowPal, outline: true,
                                               pivotTexel: new Vector2Int(arrowRows[0].Length / 2, arrowRows.Length / 2),
                                               pixelsPerUnit: ArrowPpu);
            // At the BOW's density, as the arrow is (the user's call - drawn at the body's, each of
            // its pixels was two of the bow's beside it): authored at 75 cells a unit, so its line
            // is the same one-body-pixel weight as the bow's and the arrow's.
            bow.QuiverSprite = PixelSprite.From("gear.quiver." + key, quiverRows, quiverPal, outline: true,
                                                pivotTexel: new Vector2Int(quiverRows[0].Length / 2, QuiverMouthRow),
                                                pixelsPerUnit: ArrowPpu);
        }

        /// <summary>The quiver and arrow a bow is drawn with: its own, else the Hunting Bow's - none
        /// for a non-bow or the Hive.</summary>
        public static (Sprite quiver, Sprite arrow) BowKit(GearItem bow)
        {
            if (bow == null || bow.Class != WeaponClass.Bow || bow.ItemId == HiveBowId) return (null, null);
            if (bow.ArrowSprite != null) return (bow.QuiverSprite, bow.ArrowSprite);
            var hunting = GearCatalog.Get("hunting_bow");
            return hunting != null ? (hunting.QuiverSprite, hunting.ArrowSprite) : (null, null);
        }

        static readonly string[] HuntingArrowRows =
        {
            ".4444.............................",
            ".4444444...................BBB....",
            ".5555555555...............LLLLLLL.",
            "dbbbbbbbbbbbbbbbbbbbbbbbbbBBBBBBBL",
            ".3333333333...............DDDDDB..",
            ".4444444...................BBB....",
            ".4444.............................",
        };


        static readonly string[] ArtemisArrowRows =
        {
            ".555..............................",
            ".555555....................lll....",
            ".666666666...............hhhhhhh..",
            "DBBBBBBBBBBBBBBBBBBBBBBBbbbbbbbbbh",
            ".444444444...............ddddddd..",
            ".555555....................bbb....",
            ".555..............................",
        };


        static readonly string[] UmbraArrowRows =
        {
            ".bbbbbb...........................",
            ".blllllllb.................BB.....",
            ".bbhbbhbbhbbb.............LLLLLL..",
            "sdddddddddddddddddddddddddBBBBBBBL",
            ".dddddddddddd.............DDDDDB..",
            ".ddddddddd.................BB.....",
            ".dddddd...........................",
        };


        static readonly string[] AetherArrowRows =
        {
            ".DDDD....................b........",
            ".DDDDDDD..................bb......",
            ".DD+DD+DD+D...............lllllb..",
            "dbbbbbbbbb*b*b*b*b*b*b*b*bb@@@@@@@",
            ".SS+SS+SS+S...............bbbbbb..",
            ".SSSSSSS..................bb......",
            ".SSSS....................b........",
        };

        static readonly string[] HuntingQuiverRows =
        {
            "......455.......",
            "......445455....",
            "...455345445....",
            "...445345345....",
            "...34534534455..",
            "...34534534445..",
            "...34534534345..",
            "...34534534345..",
            "...34434434344..",
            "...33433433334..",
            ".BLLLLLLLLLLHHB.",
            ".DDDDDDDDDDDDDD.",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..sdbbLLBBblld..",
            "..DDDDBkkBDDDD..",
            "..SSSSBkkBSSSS..",
            "..sdbbBBBBblld..",
            "..sdbkbbbbblld..",
            "..sdbbbbbbblld..",
            "..sdbkbbbbblld..",
            "..sdbbbbbbblld..",
            "..sdbkbbbbbhld..",
            "..sdbbbbbbbhld..",
            "..sdbkbbbbblld..",
            "..sdbbbbbbblld..",
            "..sdbkbbbbblld..",
            "..sdbbbbbbblld..",
            "..DDDDDDDDDDDD..",
            "..SSSSSSSSSSSS..",
            "..sdbbbbbbbhld..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..dddddddddddd..",
            "...ssssssssss...",
        };

        static readonly string[] ArtemisQuiverRows =
        {
            "......566.......",
            "......556566....",
            "...566456556....",
            "...556456456....",
            "...45645645566..",
            "...45645645556..",
            "...45645645456..",
            "...45645645456..",
            "...45545545455..",
            "...44544544445..",
            ".BLLLLLLLLLLHHB.",
            ".DDDDDDDDDDDDDD.",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..LLLLLLLLLLHL..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbbhld..",
            "..sdbbbbbbbhld..",
            "..sdbbbbHHblld..",
            "..sdbbbHbbLlld..",
            "..sdbbHHbbblld..",
            "..sdbbHHbbblld..",
            "..sdbbHHbbblld..",
            "..sdbbHHbbbhld..",
            "..sdbbbHbbLhld..",
            "..sdbbbbHHblld..",
            "..sdbbbbbbblld..",
            "..sdbbbbLbblld..",
            "..sdbbbLbLblld..",
            "..sdbbbbLbblld..",
            "..sdbbbbbbbhld..",
            "..LLLLLLLLLLHL..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..dddddddddddd..",
            "...ssssssssss...",
        };

        static readonly string[] UmbraQuiverRows =
        {
            "......355.......",
            "......335355....",
            "...355235335....",
            "...335235235....",
            "...23523523355..",
            "...23523523335..",
            "...23523523235..",
            "...23523523235..",
            "...23323323233..",
            "...22322322223..",
            ".BLLLLLLLLLLHHB.",
            ".DDDDDDDDDDDDDD.",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..DDDDDDDDDDDD..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbbhld..",
            "..sdbbLLLLbhld..",
            "..sdbLHHHHLlld..",
            "..sdbBLLLLBlld..",
            "..sdbBkkBkkBld..",
            "..sdbBkkBkkBld..",
            "..sdbBBkkBBlld..",
            "..sdbbBDDBbhld..",
            "..sdbbBkBkbhld..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbbhld..",
            "..sdbbbbbbbhld..",
            "..DDDDDDDDDDDD..",
            "..sdbbbbbbblld..",
            "..dddddddddddd..",
            "...ssssssssss...",
        };

        static readonly string[] AetherQuiverRows =
        {
            "......344.......",
            "......334344....",
            "...344234334....",
            "...334234234....",
            "...23423423344..",
            "...23423423334..",
            "...23423423234..",
            "...23423423234..",
            "...23323323233..",
            "...22322322223..",
            ".BLLLLLLLLLLHHB.",
            ".DDDDDDDDDDDDDD.",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..LLLLLLLLLLLL..",
            "..sdbbbb+bblld..",
            "..sdbbbb*bbhld..",
            "..sdbbb@bbbhld..",
            "..sdbbb@bbblld..",
            "..sdbbbb*bblld..",
            "..skkkkkkkkkkd..",
            "..sdbbbbb@blld..",
            "..sdbb+bb@blld..",
            "..sdb+bb*bbhld..",
            "..sdbbbb@bbhld..",
            "..sdbbb*bbblld..",
            "..skkkkkkkkkkd..",
            "..sdbbb@b*blld..",
            "..sdbb@bbb+lld..",
            "..sdbb*bbbblld..",
            "..sdbbbbbbbhld..",
            "..LLLLLLLLLLLL..",
            "..sdbbbbbbblld..",
            "..sdbbbbbbblld..",
            "..dddddddddddd..",
            "...ssssssssss...",
        };

        static readonly string[] CrowArrowRows =
        {
            ".33.3.............................",
            ".3443434..................BB......",
            ".545454545................BLLLLL..",
            "ebbbbbbbbbbbbbbbbbbbbbbbbbBBBBBBBH",
            ".232323232................DDDDD...",
            ".2332323..................DD......",
            ".22.2.............................",
        };

        static readonly string[] CrowQuiverRows =
        {
            "......355.......",
            ".....3352355....",
            "...355235335....",
            "...335235235.5..",
            "...23523523355..",
            "...23523523335..",
            "...23523523235..",
            "...23523523235..",
            "...23323323233..",
            "...22322322223..",
            ".KBBBBBBBBBBLLK.",
            ".DDDDDDDDDDDDDD.",
            "..ksddddddsdbd..",
            "..ksddddbblkbd..",
            "..KBBBBBBBBBLK..",
            "..DDDDDDDDDDDD..",
            "..ksdddkbbhbbd..",
            "..ksdddbblhbbd..",
            "..ksddkbblbkbd..",
            "..ksddkblhbkbd..",
            "..ksdkbblkbdbd..",
            "..ksdkblhkkdbd..",
            "..ksdbblhksdbd..",
            "..kskbblbksdbd..",
            "..kskblhbksdbd..",
            "..kskblhbdsdbd..",
            "..ksklhbkdsdbd..",
            "..ksklhkddsdbd..",
            "..KBBBBBBBBBLK..",
            "..DDDDDDDDDDDD..",
            "..ksddddddsdbd..",
            "..kbhdkbhdkbhdk.",
            "..kbhdkbhdkbhd..",
            "...bd..bd..bd...",
            "...SS..dS..SS...",
            "....S...S...S...",
        };
    }
}
