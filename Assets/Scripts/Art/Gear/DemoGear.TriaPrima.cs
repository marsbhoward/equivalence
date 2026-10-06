using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ============================================================== Tria Prima
    //
    // Three DIAMOND swords that forge into one BLACK DIAMOND sword - the alchemist's three
    // principles, each in its own METAL:
    //
    //   SULFUR   (soul)    the Ripsaw       YELLOW metal for its CHAIN - links and cutters - running
    //                                       round a clipped-point steel bar; the MOTOR at its base
    //   SALT     (body)    the Pacemaker    MULTICOLOURED salt crystal - white, pink, red, grey,
    //                                       black and blue facets. A wide HOLLOW blade, a big
    //                                       quicksilver bead running round the opening's rim;
    //                                       an ouroboros in a figure 8 for a guard
    //   MERCURY  (spirit)  the Reactor      MIRROR silver armour plates, bands, collar and core
    //                                       ring round a slim purple light blade, the REACTOR CORE
    //                                       at its base
    //
    // All three share ONE hilt - the same black leather grip engraved with circles and the same
    // pommel, at the same rows - so when they fuse, the hilts sit exactly on top of each other.
    // All three are the family's full height on their own.
    //
    // FUSED, they NEST: the Pacemaker outermost, the Reactor in its opening, the Ripsaw over the
    // Reactor's lower half - the Reactor and Ripsaw set deeper (their points start lower), not
    // shortened. The motor and the core are the lone swords' own, in the same place.
    //
    // Everything is ONE continuous field per sword, sampled at cell centres for the arena and at
    // quarter cells for the menu art - the Gilded/Silver/Emberline approach - and the fused sword
    // samples the SAME fields in layers, so a part can never drift from its fused self.

    public static partial class DemoGear
    {
        public const string MercuryReactorId = "mercury_reactor";
        public const string SulfurRipsawId = "sulfur_ripsaw";
        public const string SaltPacemakerId = "salt_pacemaker";
        public const string TriaPrimaId = "tria_prima_blade";
        public const string TriaPrimaRelicId = "tria_prima_relic";
        public const string SeparatioId = "separatio";

        static void AddTriaPrima(List<GearItem> items)
        {
            var pal = TriaPal();

            // ---- the three parts: Diamond, power 0, cosmetic - like every Diamond weapon ----
            var reactor = WithMenu(Make(MercuryReactorId, "Mercury Reactor", GearSlot.Weapon, LootTier.Diamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.reactor", ReactorRows, pal, 0f, -10f,
                       pivotTexel: TriaHiltGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.reactor.menu", Detail(ReactorTexel, SwordCanvas),
                       pal, 0f, -10f, pivotTexel: TriaHiltGrip * EmberDetailScale, ppu: MenuPpu));
            // Light pulsing up the blade and the core throbbing - the same ticker and clock as the
            // bead and the chain.
            reactor.IdleFrames = BeadFrames("gear.weapon.reactor", TriaHiltGrip,
                                            phase => ReactorRowsAt(phase - BeadRestPhase));
            reactor.IdleFrameSeconds = Core.Tuning.Separatio.BeadFrameSeconds;
            items.Add(reactor);

            var ripsaw = WithMenu(Make(SulfurRipsawId, "Sulfur Ripsaw", GearSlot.Weapon, LootTier.Diamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.ripsaw", RipsawRows, pal, 0f, -10f,
                       pivotTexel: TriaHiltGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.ripsaw.menu", Detail(RipsawTexel, SwordCanvas),
                       pal, 0f, -10f, pivotTexel: TriaHiltGrip * EmberDetailScale, ppu: MenuPpu));
            // The chain running round the bar - the same ticker and clock as the bead.
            ripsaw.IdleFrames = BeadFrames("gear.weapon.ripsaw", TriaHiltGrip,
                                           phase => RipsawRowsAt((phase - BeadRestPhase) * SawChainPerLoop));
            ripsaw.IdleFrameSeconds = Core.Tuning.Separatio.BeadFrameSeconds;
            items.Add(ripsaw);

            var pacemaker = WithMenu(Make(SaltPacemakerId, "Salt Pacemaker", GearSlot.Weapon,
                                          LootTier.Diamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.pacemaker", PacemakerRows(BeadRestPhase), pal, 0f, -10f,
                       pivotTexel: TriaHiltGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.pacemaker.menu",
                       Detail((x, y, f) => PacemakerTexel(x, y, f, BeadRestPhase), SwordCanvas),
                       pal, 0f, -10f, pivotTexel: TriaHiltGrip * EmberDetailScale, ppu: MenuPpu));
            pacemaker.IdleFrames = BeadFrames("gear.weapon.pacemaker", TriaHiltGrip, phase => PacemakerRows(phase));
            pacemaker.IdleFrameSeconds = Core.Tuning.Separatio.BeadFrameSeconds;
            items.Add(pacemaker);

            // ---- the fused sword: Black Diamond, power 0 - the mechanic lives on the relic,
            // the same split Phantom, Blood Blade and Zanmato use. NOT dropped by any box: the
            // only way to own one is to burn the three parts at the Forge (GearForge.Fusions).
            var tria = WithMenu(Make(TriaPrimaId, "Tria Prima", GearSlot.Weapon, LootTier.BlackDiamond, 0f,
                Pixels(RigLayer.Weapon, "gear.weapon.triaprima", TriaRows(BeadRestPhase), pal, 0f, -10f,
                       pivotTexel: TriaHiltGrip, ppu: FinePpu, upscale2x: true)),
                Pixels(RigLayer.Weapon, "gear.weapon.triaprima.menu",
                       Detail((x, y, f) => TriaTexel(x, y, f, BeadRestPhase), SwordCanvas),
                       pal, 0f, -10f, pivotTexel: TriaHiltGrip * EmberDetailScale, ppu: MenuPpu));
            tria.SignatureFinisher = SeparatioId;
            tria.ForgeOnly = true;
            tria.IdleFrames = BeadFrames("gear.weapon.triaprima", TriaHiltGrip, phase => TriaRows(phase));
            tria.IdleFrameSeconds = Core.Tuning.Separatio.BeadFrameSeconds;
            // What each of Separatio's three figures holds - in Sulfur, Salt, Mercury order,
            // matching Tuning.Separatio's tints and motions. The lone swords' own arena art, so a
            // figure holds exactly the sword that part is when it is pulled out and equipped.
            tria.SplitBlades = new[]
            {
                StageSprite("gear.weapon.ripsaw", RipsawRows, pal, TriaHiltGrip),
                StageSprite("gear.weapon.pacemaker", PacemakerRows(BeadRestPhase), pal, TriaHiltGrip),
                StageSprite("gear.weapon.reactor", ReactorRows, pal, TriaHiltGrip),
            };
            items.Add(tria);

            // The RELIC: the finisher, no power, no art beyond the worn mark - see Phantom's.
            var relic = Make(TriaPrimaRelicId, "Tria Prima Seal", GearSlot.Relic, LootTier.BlackDiamond, 0f,
                HipRelic("gear.trinket.triaprima", TriaRelicRows, pal));
            relic.SignatureFinisher = SeparatioId;
            relic.ForgeOnly = true;
            items.Add(relic);
        }

        // ---------------------------------------------------------------- palette
        //
        // More materials than Palette.Of's three, so the keys are assigned by hand, six to a ramp
        // in Palette's own order (line, deep, dark, base, light, glow):
        //
        //   k s d b l h   sulfur metal   the Ripsaw's chain - links and cutters
        //   K S D B L H   black leather  the shared grip
        //   1 2 3 4 5 6   reactor light  purple
        //   t u v w x y   motor grey     the Ripsaw's motor
        //   T U V W X Y   saw steel      the Ripsaw's bar, groove and collar
        //   m n o p q r   mirror silver  the Reactor's plates - Mercury's metal
        //   M N O P Q R   quicksilver    the bead
        //   j J 0 7 8 9   dark steel     the shared pommel and the Pacemaker's ouroboros
        //
        // and the SALT colours, two tones each (lowercase lit, uppercase shaded) - the Pacemaker's
        // blade is a mosaic of every colour salt comes in:
        //
        //   a A  white (table)   c C  pink (Himalayan)   e E  red (alaea)
        //   f F  grey (sel gris) g G  black (lava)       i I  blue (Persian)

        static Dictionary<char, Color> TriaPal()
        {
            var map = new Dictionary<char, Color>();
            void Put(string keys, Palette.Ramp r)
            {
                map[keys[0]] = r.Line; map[keys[1]] = r.Deep; map[keys[2]] = r.Dark;
                map[keys[3]] = r.Base; map[keys[4]] = r.Light; map[keys[5]] = r.Glow;
            }
            void Pair(char lit, char dark, Color c)
            {
                map[lit] = c;
                map[dark] = Color.Lerp(c, new Color(0.10f, 0.08f, 0.12f), 0.32f);
            }

            // Sulfur: a warm yellow metal lit toward its own pale gold, never toward white - lit
            // toward white a yellow reads as plastic (see Ramp.WithHighlightsToward).
            Put("ksdblh", new Palette.Ramp(new Color(0.86f, 0.68f, 0.14f), lift: 0.40f, shade: 0.40f)
                .WithHighlightsToward(new Color(1.00f, 0.96f, 0.62f), 0.45f));
            Put("KSDBLH", new Palette.Ramp(new Color(0.13f, 0.12f, 0.13f), lift: 0.30f, shade: 0.30f));
            // Lifted only a little: lit much toward white, purple washes out to lavender.
            Put("123456", new Palette.Ramp(new Color(0.55f, 0.20f, 0.95f), lift: 0.32f, shade: 0.35f));
            Put("tuvwxy", new Palette.Ramp(new Color(0.60f, 0.61f, 0.64f), lift: 0.36f, shade: 0.34f));
            Put("TUVWXY", new Palette.Ramp(new Color(0.56f, 0.59f, 0.64f), lift: 0.40f, shade: 0.40f));
            // Mirror: the widest ramp in the file - near-white glints over near-black reflections.
            // What says MIRROR is the contrast, not the base colour.
            Put("mnopqr", new Palette.Ramp(new Color(0.74f, 0.77f, 0.83f), lift: 0.62f, shade: 0.50f));
            Put("MNOPQR", new Palette.Ramp(new Color(0.86f, 0.90f, 0.96f), lift: 0.70f, shade: 0.45f));
            Put("jJ0789", new Palette.Ramp(new Color(0.17f, 0.17f, 0.20f), lift: 0.34f, shade: 0.30f));

            Pair('a', 'A', new Color(0.95f, 0.94f, 0.91f));
            Pair('c', 'C', new Color(0.95f, 0.66f, 0.68f));
            Pair('e', 'E', new Color(0.80f, 0.34f, 0.26f));
            Pair('f', 'F', new Color(0.66f, 0.66f, 0.64f));
            Pair('g', 'G', new Color(0.26f, 0.25f, 0.27f));
            Pair('i', 'I', new Color(0.60f, 0.72f, 0.92f));
            return map;
        }

        // ---------------------------------------------------------------- shared sampling

        delegate char Field(float x, float y, bool fine);

        /// <summary>The arena grid: one sample per cell centre.</summary>
        static string[] Arena(Field field, int canvas)
        {
            var rows = new string[SwordHeightRows];
            var line = new char[canvas];
            for (int y = 0; y < rows.Length; y++)
            {
                for (int x = 0; x < canvas; x++) line[x] = field(x, y, false);
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>Menu art at TRUE <see cref="MenuPpu"/>, four samples per cell each way -
        /// never pass it upscale2x. Same field as the arena, so the silhouettes agree.</summary>
        static string[] Detail(Field field, int canvas)
        {
            const int k = EmberDetailScale;
            var rows = new string[SwordHeightRows * k];
            var line = new char[canvas * k];
            for (int Y = 0; Y < rows.Length; Y++)
            {
                float y = (Y + 0.5f) / k - 0.5f;
                for (int X = 0; X < line.Length; X++) line[X] = field((X + 0.5f) / k - 0.5f, y, true);
                rows[Y] = new string(line);
            }
            return rows;
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        static float TriaHash(int i, int j)
        {
            unchecked
            {
                int h = i * 374761393 + j * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xffff) / 65535f;
            }
        }

        // ================================================================ the shared hilt
        //
        //   56-60   each sword's own guard zone (the Pacemaker's ouroboros; the Reactor's collar; the
        //           Ripsaw's flare) - the only rows below the blade that differ
        //   61-67   the grip: black leather, three engraved circles
        //   68-71   the pommel: a faceted dark-steel shield holding a violet crystal
        //
        // Every row here is IDENTICAL on all three, and the pivot is the same texel, which is the
        // whole trick of the fusion: the three hilts are one hilt.

        const float TriaBladeBase = 55.5f;          // continuous top of row 56
        const float TriaGripTop = 60.5f;            // row 61
        // The grip is the family's length, and the pommel follows it; the pivot is its centre.
        static float TriaPommelTop => TriaGripTop + SwordGripRows;
        static Vector2Int TriaHiltGrip => new(13, GripCentre(Mathf.RoundToInt(TriaGripTop + 0.5f), SwordGripRows));

        static char TriaHilt(float dx, float y, bool fine)
        {
            float ax = Mathf.Abs(dx);
            if (y < TriaPommelTop)
            {
                if (ax >= SwordGripWidth / 2f) return '.';
                return Leather(dx, y, fine);
            }

            float py = y - TriaPommelTop;                         // 0..4
            if (py < 0.8f) return ax < 2.2f ? (fine && py < 0.25f ? '8' : '0') : '.';
            float half = py < 2.6f ? 2f : 2f * (1f - (py - 2.6f) / 1.5f);
            if (ax >= half) return '.';
            // The crystal - a small upright diamond in the face of the shield.
            float cy = TriaPommelTop + 2.3f;
            float gem = ax * 1.5f + Mathf.Abs(y - cy);
            if (gem < 1.05f) return gem < 0.4f ? '6' : fine && dx < 0f ? '5' : '4';
            if (py > 2.6f) return dx < 0f ? '7' : 'J';               // the lower facets
            return dx < 0f ? (fine && ax > 1.6f ? '9' : '8') : '0';
        }

        /// <summary>Black leather engraved with three circles, one over another.</summary>
        static char Leather(float dx, float y, bool fine)
        {
            const float pitch = 2.35f, r = 1.05f;
            float k = Mathf.Round((y - (TriaGripTop + 1.2f)) / pitch);
            k = Mathf.Clamp(k, 0f, 2f);
            float cy = TriaGripTop + 1.2f + k * pitch;
            float d = Mathf.Sqrt(dx * dx + (y - cy) * (y - cy));
            if (Mathf.Abs(d - r) < (fine ? 0.2f : 0.45f)) return 'K';
            // The engraving's lit lower lip, on the menu grid - cut in, not drawn on.
            if (fine && d > r + 0.2f && d < r + 0.42f && y > cy) return 'L';
            float half = SwordGripWidth / 2f;
            if (dx < -half + (fine ? 0.5f : 1f)) return 'L';
            if (dx > half - (fine ? 0.5f : 1f)) return 'D';
            return 'B';
        }

        // ================================================================ MERCURY - the Reactor
        //
        //   rows 0-3     a faceted point, the light running all the way into it
        //   to 55        the light blade: violet edges, a bright core line; couplings band it, and
        //                the light gathers in a bulb and then the REACTOR CORE at the blade's base -
        //                a ringed housing about the blade's own width, a little left of centre, four
        //                notches round it, the violet glow inside
        //   22-55        mirror-silver armour plates climbing the lower half, wrapping the core -
        //                the left one a single tall plate, the right one three, stepping outward
        //   56-60        a mirror collar narrowing into the grip, a violet node at its centre
        //
        // MERCURY is the metal: every plate, band, collar and the core's ring is mirror silver.

        const float ReactorShoulder = 3.5f;
        const float ReactorLight = 2.6f;
        static readonly float[] ReactorCouplings = { 14f, 22f, 31f, 36f, 41f };
        // Left of centre, as in the reference - which also leaves its glow showing beside the
        // Ripsaw's motor on the fused sword.
        static readonly Vector2 ReactorCoreAt = new(-1.2f, 51.5f);
        const float ReactorCoreOuter = 2.7f, ReactorCoreInner = 1.9f;

        static string[] _reactorRows;
        // Lazy, not a static initialiser: this is a second file of a partial class, and C# does
        // not order static initialisers ACROSS files - built eagerly, this could run before
        // anything in DemoGear.cs it reads exists.
        static string[] ReactorRows => _reactorRows ??= ReactorRowsAt(0f);

        static string[] ReactorRowsAt(float t)
            => Arena((x, y, f) => ReactorAt(x - SwordAxis, y, f, 0f, t), SwordCanvas);

        static char ReactorTexel(float x, float y, bool fine) => ReactorAt(x - SwordAxis, y, fine, 0f);

        // The Reactor RUNS: pulses of light travel up the blade toward the tip and the core, bulb
        // and collar node throb. Driven by the same flipbook clock as the bead and the chain -
        // <c>t</c> is one circuit, 0..1 - with whole numbers of pulses per circuit so it loops clean.
        const float ReactorPulseRows = 9f;           // spacing of the travelling pulses
        const float ReactorPulsesPerLoop = 2f;       // how far they travel per circuit, in spacings
        const float ReactorThrobsPerLoop = 2f;       // core heartbeats per circuit

        static float ReactorThrob(float t) => 0.5f + 0.5f * Mathf.Cos(t * ReactorThrobsPerLoop * Mathf.PI * 2f);

        /// <summary>
        /// The Reactor at <paramref name="dx"/> from its axis. <paramref name="tip"/> is the row its
        /// point starts on: 0 for the lone sword, lower on the fused one, where the point sits
        /// inside the Pacemaker's opening - the blade is the SAME blade, set deeper.
        /// </summary>
        static char ReactorAt(float dx, float y, bool fine, float tip, float t = 0f)
        {
            if (y >= TriaGripTop) return TriaHilt(dx, y, fine);
            if (y >= TriaBladeBase) return ReactorCollar(dx, y, fine, t);
            char core = ReactorCore(dx, y, fine, t);
            if (core != '.') return core;
            char plate = ReactorPlate(dx, y, fine);
            if (plate != '.') return plate;
            return ReactorLightAt(dx, y, fine, tip, t);
        }

        static char ReactorLightAt(float dx, float y, bool fine, float tip, float t)
        {
            float ax = Mathf.Abs(dx);
            float top = tip - 0.5f;
            if (y < top) return '.';
            float shoulder = tip + ReactorShoulder;
            float half = y < shoulder ? ReactorLight * Mathf.Clamp01((y - top) / (shoulder - top)) : ReactorLight;
            if (ax >= half) return '.';

            // Couplings: white salt bands across the light, lit along their top edge.
            foreach (var c in ReactorCouplings)
                if (c > tip + 4f && Mathf.Abs(y - c) < 0.5f)
                    return fine ? (y < c - 0.15f ? 'r' : y > c + 0.3f ? 'n' : 'p') : 'q';

            // The bulb where the light gathers above the core, brightening with each throb.
            float bulb = (dx * dx) / (1.3f * 1.3f) + (y - 46.5f) * (y - 46.5f) / (1.8f * 1.8f);
            if (bulb < 1f) return bulb < 0.2f + 0.35f * ReactorThrob(t) ? '6' : '5';

            // Faceted: the outer band of each edge is a darker facet, the core a bright line, and
            // the body the full purple - most of the blade has to BE the colour.
            float edge = half - ax;
            // A pulse travelling up toward the tip: a bright band across the light, the edge
            // facets lifting with it.
            // Two rows wide and soft - a hard one-row bright line read as one more coupling.
            float pulse = Frac((y + t * ReactorPulsesPerLoop * ReactorPulseRows) / ReactorPulseRows);
            float glow = Mathf.Min(pulse, 1f - pulse) * ReactorPulseRows;     // rows from the pulse's centre
            bool lit = glow < 1.1f;
            if (edge < (fine ? 0.6f : 1f)) return lit ? '4' : dx < 0f ? '3' : '2';
            if (lit) return fine && glow < 0.45f && ax < 1.3f ? '6' : '5';
            if (fine && ax < 0.35f) return '6';
            if (ax < (fine ? 0.9f : 1f)) return '5';
            return '4';
        }

        /// <summary>
        /// The reactor core - the part the sword is named for. A ringed housing in white salt, lit
        /// on the upper left, notched at the four compass points, holding the violet light.
        /// </summary>
        static char ReactorCore(float dx, float y, bool fine, float t = 0f)
        {
            float cx = dx - ReactorCoreAt.x, cy = y - ReactorCoreAt.y;
            float r = Mathf.Sqrt(cx * cx + cy * cy);
            if (r >= ReactorCoreOuter) return '.';
            if (r >= ReactorCoreInner)
            {
                // The four notches.
                if (Mathf.Min(Mathf.Abs(cx), Mathf.Abs(cy)) < (fine ? 0.35f : 0.5f)) return 'm';
                if (fine && r < ReactorCoreInner + 0.18f) return 'm';
                return cx + cy < 0f ? 'r' : 'o';
            }
            // The glow swells and settles with each throb.
            float throb = ReactorThrob(t);
            return r < 0.3f + 0.5f * throb ? '6' : r < 0.85f + 0.45f * throb ? '5' : r < 1.55f ? '4' : '3';
        }

        /// <summary>The salt armour plates up the lower half. Each is a strip hugging the light,
        /// its top cut on a slant (higher on the outside), its top edge lit.</summary>
        static char ReactorPlate(float dx, float y, bool fine)
        {
            float ax = Mathf.Abs(dx);
            if (ax < 1.6f) return '.';
            bool left = dx < 0f;

            float outer, top, bottom;
            if (left)
            {
                top = 22f; bottom = TriaBladeBase;
                outer = y < 43f ? 3.6f : 3.6f + Mathf.Clamp01((y - 43f) / 2f);
            }
            else if (y < 32.5f) { top = 28.5f; bottom = 32.5f; outer = 3.2f; }
            else if (y < 43.5f) { top = 33.5f; bottom = 43f; outer = 3.6f; }
            else { top = 44.5f; bottom = TriaBladeBase; outer = 4.6f; }

            if (ax >= outer || y >= bottom) return '.';
            // Slanted top: the outer edge stands 1.2 rows higher than the inner.
            float slantTop = top + (1f - (ax - 1.6f) / (outer - 1.6f)) * 1.2f;
            if (y < slantTop) return '.';
            if (y < slantTop + (fine ? 0.35f : 0.5f)) return 'r';
            if (fine && bottom - y < 0.3f) return 'm';
            return PlateMirror(ax, outer, left, y, fine);
        }

        static char ReactorCollar(float dx, float y, bool fine, float t = 0f)
        {
            float ax = Mathf.Abs(dx);
            float v = (y - TriaBladeBase) / (TriaGripTop - TriaBladeBase);
            float half = Mathf.Lerp(4.6f, 1.9f, v);
            if (ax >= half) return '.';
            char node = ReactorNode(dx, y, t);
            if (node != '.') return node;
            if (fine && v < 0.08f) return 'r';
            return PlateMirror(ax, half, dx < 0f, y, fine, 0f);
        }

        /// <summary>The violet node in the collar, throbbing with the core. Separate so the fused
        /// sword can keep it in front of the ouroboros that lies over the rest of the collar.</summary>
        static char ReactorNode(float dx, float y, float t)
        {
            float nr = Mathf.Sqrt(dx * dx + (y - 58f) * (y - 58f));
            if (nr >= 0.9f) return '.';
            return nr < 0.25f + 0.35f * ReactorThrob(t) ? '6' : '5';
        }

        /// <summary>
        /// Mirror silver on a plate: a glint on its outer bevel, a narrow dark reflection band,
        /// light body, the diagonal glint the Pacemaker used to carry. Mostly LIGHT - a mirror of a
        /// bright sky; drawn wide, the dark band read as obsidian.
        /// </summary>
        static char PlateMirror(float ax, float outer, bool left, float y, bool fine, float inner = 1.6f)
        {
            float u = (ax - inner) / Mathf.Max(0.001f, outer - inner);  // 0 inner .. 1 outer
            if (fine && Frac((y + ax * (left ? -1.3f : 1.3f)) / 11f) < 0.06f) return 'r';
            if (!fine)
            {
                if (u > 0.6f) return left ? 'r' : 'o';
                return left ? 'q' : 'p';
            }
            if (u > 0.85f) return left ? 'r' : 'o';
            if (u > 0.62f) return left ? 'q' : 'p';
            if (u > 0.48f) return 'n';                                   // the dark reflection
            return left ? 'p' : 'q';
        }

        // ================================================================ SULFUR - the Ripsaw
        //
        //   rows 0-4     a CLIPPED point, high on the right, a nose sprocket in the high corner
        //   to 58        a wide STEEL bar, a groove down it - and round its rim the CHAIN, in
        //                yellow SULFUR metal: one continuous loop up the left edge, over the clipped
        //                tip and down the right, links of plate and rivet, a small L-shaped cutter
        //                riding every third link. The loop is what says chainsaw - a chain that
        //                stopped at the tip, or teeth bigger than the links carrying them, read as
        //                a serrated sword instead
        //   45-58        the MOTOR, a grey capsule fixed OVER the bar, climbing the fuller from the
        //                hilt - on the lone sword too, so on the fused sword it is plainly the
        //                Ripsaw's own
        //   59-60        a steel collar
        //
        // The bar is WIDE so the chain sits near the rim and the cutters - small, as a chain's are -
        // still stand past the Pacemaker's edge when the three fuse. The chain MOVES: the Ripsaw's
        // flipbook (and Tria Prima's, in step with the bead) scrolls the links round the loop.

        const float SawBody = 3.2f;          // the bar, each side of the axis
        const float SawChainW = 1.1f;        // the chain band, outside the bar
        const float SawToothH = 1.4f;        // how far a cutter stands past the chain
        const float SawLink = 1f;            // one link, along the loop
        const float SawToothEvery = 3f;      // a cutter every third link
        const float SawClip = 4.5f;          // rows the clipped point falls across the bar
        // Cells the chain travels in one flipbook circuit - a whole number of cutter spacings, so
        // the loop closes without a hitch.
        const float SawChainPerLoop = 18f;
        static float SawBottom => TriaGripTop - 2f;

        static string[] _ripsawRows;
        static string[] RipsawRows => _ripsawRows ??= RipsawRowsAt(0f);

        static string[] RipsawRowsAt(float chain)
            => Arena((x, y, f) => RipsawAt(x - SwordAxis, y, f, 0f, chain), SwordCanvas);

        static char RipsawTexel(float x, float y, bool fine) => RipsawAt(x - SwordAxis, y, fine, 0f);

        /// <summary>The bar's top edge - the clipped point, falling from the right edge to the left.</summary>
        static float SawTopAt(float dx, float tip) => tip - 0.5f + (SawBody - dx) * (SawClip / (2f * SawBody));

        /// <summary>
        /// Signed distance from the bar's rim (negative inside), and <paramref name="s"/>: how far
        /// along the chain's loop the nearest rim point is, measured the way the chain runs - up
        /// the left edge, across the clipped top, down the right. Round the outside corners the
        /// nearest point is the corner itself, so the chain band turns the corner rounded.
        /// </summary>
        static float SawField(float dx, float y, float tip, out float s)
        {
            var p = new Vector2(dx, y);
            var lb = new Vector2(-SawBody, SawBottom);
            var lt = new Vector2(-SawBody, SawTopAt(-SawBody, tip));
            var rt = new Vector2(SawBody, SawTopAt(SawBody, tip));
            var rb = new Vector2(SawBody, SawBottom);
            float l1 = Vector2.Distance(lb, lt), l2 = Vector2.Distance(lt, rt);

            float d1 = SegDistance(p, lb, lt, out float t1);
            float d2 = SegDistance(p, lt, rt, out float t2);
            float d3 = SegDistance(p, rt, rb, out float t3);
            float d;
            if (d1 <= d2 && d1 <= d3) { d = d1; s = t1 * l1; }
            else if (d2 <= d3) { d = d2; s = l1 + t2 * l2; }
            else { d = d3; s = l1 + l2 + t3 * Vector2.Distance(rt, rb); }

            bool inside = dx > -SawBody && dx < SawBody && y > SawTopAt(dx, tip) && y < SawBottom;
            return inside ? -d : d;
        }

        static float SegDistance(Vector2 p, Vector2 a, Vector2 b, out float t)
        {
            var ab = b - a;
            t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>The Ripsaw. <paramref name="tip"/> as for ReactorAt: 0 alone, lower when fused,
        /// where the point starts over the Reactor's lower half. <paramref name="chain"/> is how
        /// far the chain has run round its loop, in cells - the flipbook's clock.</summary>
        static char RipsawAt(float dx, float y, bool fine, float tip, float chain = 0f, bool motor = true)
        {
            float ax = Mathf.Abs(dx);
            if (y >= TriaGripTop) return TriaHilt(dx, y, fine);

            if (motor)
            {
                char m = Motor(dx, y, fine);
                if (m != '.') return m;
            }
            if (y >= SawBottom)
                return ax < 2.4f ? (fine && y < SawBottom + 0.3f ? 'X' : ax > 1.6f ? 'U' : 'V') : '.';

            float sd = SawField(dx, y, tip, out float s);
            float run = s - chain;

            if (sd >= 0f)
            {
                if (sd < SawChainW)
                {
                    // Links: a side plate, a rivet on it, and the gap to the next.
                    float u = Frac(run / SawLink);
                    if (!fine) return u < 0.5f ? 'l' : 'd';
                    if (u < 0.1f || u > 0.9f) return 'k';
                    if (u > 0.62f) return 's';
                    if (Mathf.Abs(u - 0.36f) < 0.12f && Mathf.Abs(sd - SawChainW * 0.5f) < 0.2f) return 'h';
                    return sd < 0.2f ? 'd' : 'b';
                }

                // A cutter: an L - a post standing off the link, and a top plate running back
                // along the chain from it. Small, and every one the same.
                float o = sd - SawChainW;
                if (o >= SawToothH) return '.';
                float tu = Frac(run / SawToothEvery);
                bool post = tu < 0.3f;
                bool plate = tu < 0.62f && o > SawToothH - (fine ? 0.5f : 0.7f);
                bool foot = tu < 0.62f && o < (fine ? 0.3f : 0.5f);
                if (!(post || plate || foot)) return '.';
                if (o > SawToothH - (fine ? 0.25f : 0.7f)) return fine && tu < 0.12f ? 'h' : 'l';
                return post ? 'b' : 'd';
            }

            // The nose sprocket, in the clipped point's high corner.
            float sx = dx - (SawBody - 1.35f), sy = y - (SawTopAt(SawBody - 1.35f, tip) + 1.35f);
            float sr = Mathf.Sqrt(sx * sx + sy * sy);
            if (sr < 0.95f) return sr < 0.4f ? 'k' : fine && sx + sy < -0.2f ? 'h' : 'b';

            // The bar's rim catches the light along the top and left.
            float rim = -sd;
            if (rim < (fine ? 0.35f : 0.6f)) return dx < 1.5f && (y < SawTopAt(dx, tip) + 1f || dx < 0f) ? 'Y' : 'V';
            // The groove down the bar, left of centre.
            if (dx > -1.5f && dx < -0.85f && y > SawTopAt(dx, tip) + 1.5f)
                return fine && dx > -1.35f && dx < -1.0f ? 'T' : 'U';
            // A steel bar, lit on the left - the chain round it is the sulfur.
            float uu = (dx + SawBody) / (2f * SawBody);
            return uu < 0.28f ? 'X' : uu > 0.86f ? 'V' : 'W';
        }

        const float MotorTop = 44.5f, MotorBottom = 58.5f, MotorHalf = 2.0f;

        /// <summary>
        /// The chainsaw's motor, in the capsule it has always had (same size, same place), built
        /// from the parts that say ENGINE: a spark-plug boot on the dome, cooling fins across the
        /// cylinder, a round recoil-starter cover with its vents and centre bolt, and a muffler
        /// grille at the bottom. Outlined dark so it reads as a separate part fixed to the bar.
        /// </summary>
        static char Motor(float dx, float y, bool fine)
        {
            float ax = Mathf.Abs(dx);
            float cy = Mathf.Clamp(y, MotorTop + MotorHalf, MotorBottom - MotorHalf);
            float d = Mathf.Sqrt(dx * dx + (y - cy) * (y - cy)) - MotorHalf;
            if (d >= 0f) return '.';
            if (d > (fine ? -0.28f : -0.5f)) return 'u';

            // The spark-plug boot, a steel nub capped in black on the dome.
            if (y < MotorTop + 1.4f && ax < 0.45f) return y < MotorTop + 0.8f ? 't' : 'T';

            // Cooling fins across the cylinder: raised ribs, lit on their top edge.
            float fins = y - (MotorTop + 1.5f);
            if (fins > 0f && fins < 4.4f)
            {
                float f = Frac(fins / 1.1f);
                if (f < 0.35f) return fine && f < 0.15f ? 'y' : 'x';
                if (f > 0.75f) return 'u';
                return dx < -0.8f ? 'w' : 'v';
            }

            // The recoil starter: a round cover, a dark rim, three vent slots, a centre bolt.
            const float startY = 52.3f, startR = 1.55f;
            float sx = dx, sy = y - startY;
            float sr = Mathf.Sqrt(sx * sx + sy * sy);
            if (sr < startR)
            {
                if (sr > startR - (fine ? 0.25f : 0.45f)) return 'u';
                if (sr < 0.4f) return fine && sx + sy < 0f ? 'y' : 'x';
                float a = Mathf.Atan2(sy, sx) / (Mathf.PI * 2f / 3f);
                if (sr > 0.65f && Mathf.Abs(a - Mathf.Round(a)) < 0.12f) return 'v';
                return sx + sy < 0f ? 'x' : 'w';
            }

            // The muffler grille: rows of small dark holes.
            float gy = y - 54.6f;
            if (gy > 0f && gy < 2.6f && ax < 1.4f)
            {
                float hx = Frac((dx + 1.4f) / 0.7f), hy = Frac(gy / 0.65f);
                if (fine ? (hx < 0.45f && hy < 0.5f) : ((Mathf.RoundToInt(dx + y) & 1) == 0)) return 'u';
                return 'v';
            }

            // The housing between the parts.
            if (fine && dx > -1.4f && dx < -1.0f) return 'y';
            return dx < -0.5f ? 'x' : dx > 0.9f ? 'v' : 'w';
        }

        /// <summary>
        /// Salt crystal: a mosaic of facets, each one of the colours salt comes in. Voronoi cells
        /// on a slightly squashed lattice so the facets run long, like cleaved crystal. Each
        /// facet is lit on the side facing the light and shaded on the other, with a dark crack
        /// between facets on the menu grid.
        /// </summary>
        static char Salt(float dx, float y, bool fine)
        {
            // Small enough that a strip two cells wide still crosses several facets.
            const float cell = 1.3f;
            float px = dx / cell, py = y / (cell * 1.4f);
            int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py);
            float best = 99f, second = 99f;
            int bi = 0, bj = 0;
            Vector2 bestSite = default;
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                int ci = ix + i, cj = iy + j;
                var site = new Vector2(ci + TriaHash(ci, cj), cj + TriaHash(cj * 7 + 3, ci * 5 + 1));
                float d = (site - new Vector2(px, py)).sqrMagnitude;
                if (d < best) { second = best; best = d; bi = ci; bj = cj; bestSite = site; }
                else if (d < second) second = d;
            }

            if (fine && Mathf.Sqrt(second) - Mathf.Sqrt(best) < 0.07f) return 'G';

            // Weighted toward the COLOURS - pink, red, blue - with white, grey and black as the
            // accents between them. Weighted toward white, the blade read as a pale speckle and the
            // colour was all at the bottom of the fused sword.
            const string lit = "ccceeiiiafg", dark = "CCCEEIIIAFG";
            int pick = Mathf.FloorToInt(TriaHash(bi * 13 + 5, bj * 17 + 11) * lit.Length) % lit.Length;
            // Lit if this point sits up-left of its facet's centre - the facet tilts toward the light.
            var off = new Vector2(px, py) - bestSite;
            bool facingLight = off.x + off.y < 0f;
            return facingLight ? lit[pick] : dark[pick];
        }

        // ================================================================ SALT - the Pacemaker
        //
        //   rows 0-5     a symmetric point
        //   to 55        SALT crystal, ten cells wide, HOLLOW down the middle: a long opening,
        //                pointed at both ends, from row 4 to row 53. A bead of quicksilver runs
        //                round the opening's rim, half in the hollow, so it reads against the
        //                see-through as it moves (BeadFrames)
        //   56-60        the guard: an OUROBOROS in a figure 8 across the blade's base

        const float PaceHalf = 5f;
        const float HoleHalf = 2.6f;
        const float HoleTop = 4.5f, HoleShoulder = 8f, HoleSideEnd = 49.5f, HoleBottom = 53.5f;
        const float TrackOut = 0.3f;                 // the groove, just outside the opening's rim
        // Big enough to fill most of the rail and spill into the hollow - at arena size a bead
        // that fits inside the groove was one or two cells and vanished. About three cells across.
        const float BeadRadius = 1.45f;

        /// <summary>Where the bead sits in every STILL picture of the sword - up the right side of
        /// the opening, where it is visible on the fused sword too. The flipbook starts here.</summary>
        const float BeadRestPhase = 0.08f;

        static string[] PacemakerRows(float phase)
            => Arena((x, y, f) => PacemakerTexel(x, y, f, phase), SwordCanvas);

        static char PacemakerTexel(float x, float y, bool fine, float phase)
            => PacemakerAt(x - SwordAxis, y, fine, phase);

        static char PacemakerAt(float dx, float y, bool fine, float phase)
        {
            float ax = Mathf.Abs(dx);
            if (y >= TriaGripTop) return TriaHilt(dx, y, fine);
            if (y >= TriaBladeBase) return PaceGuard(dx, y, fine);

            // The bead first - it sits half in the hollow, so it is drawn even where the blade is not.
            char bead = Bead(dx, y, fine, phase);
            if (bead != '.') return bead;

            float half = PaceHalf * Mathf.Clamp01((y + 0.5f) / 6f);
            if (ax >= half) return '.';
            if (ax < HoleHalfAt(y)) return '.';

            float t = TrackDistance(dx, y, fine);
            if (t < (fine ? 0.3f : 0.5f)) return 'G';

            // Salt: the crystal mosaic, the colour running right out to the edges - on the fused
            // sword only the tip and a thin strip either side of the Pacemaker show, and a white rim
            // there left it with no colour at all. Only the opening's rim is lit, thinly, on the
            // menu grid, so the hollow still reads as cut.
            if (fine && ax - HoleHalfAt(y) < 0.25f && HoleHalfAt(y) > 0f) return dx < 0f ? 'F' : 'a';
            return Salt(dx, y, fine);
        }

        /// <summary>The quicksilver bead - bright on its upper left, a dark rim on the menu grid.</summary>
        static char Bead(float dx, float y, bool fine, float phase)
        {
            var bead = BeadAt(phase);
            float bx = dx - bead.x, by = y - bead.y;
            float br = Mathf.Sqrt(bx * bx + by * by);
            float radius = fine ? BeadRadius : BeadRadius + 0.2f;
            if (br >= radius) return '.';
            float l = (bx * 0.6f + by * 0.8f) / radius;                // + is away from the light
            if (fine && br > radius - 0.22f) return 'M';
            if (fine && l < -0.55f) return 'R';
            return l < -0.25f ? 'R' : l < 0.2f ? 'Q' : l < 0.55f ? 'P' : 'N';
        }

        /// <summary>The opening's half-width at a row - pointed at both ends, 0 outside it.</summary>
        static float HoleHalfAt(float y)
        {
            if (y <= HoleTop || y >= HoleBottom) return 0f;
            if (y < HoleShoulder) return HoleHalf * (y - HoleTop) / (HoleShoulder - HoleTop);
            if (y > HoleSideEnd) return HoleHalf * (HoleBottom - y) / (HoleBottom - HoleSideEnd);
            return HoleHalf;
        }

        /// <summary>
        /// The guard: an OUROBOROS lying in a figure 8 across the blade's base, over the salt
        /// collar the blade narrows into.
        /// </summary>
        static char PaceGuard(float dx, float y, bool fine)
        {
            char snake = Ouroboros(dx, y, fine);
            if (snake != '.') return snake;

            float ax = Mathf.Abs(dx);
            float t = (y - TriaBladeBase) / (TriaGripTop - TriaBladeBase);
            if (ax >= Mathf.Lerp(PaceHalf, 1.8f, t)) return '.';
            if (fine && t < 0.08f) return 'a';
            return Salt(dx, y, fine);
        }

        // ---------------------------------------------------------------- the ouroboros
        //
        // A snake in a figure 8 - a lemniscate of Gerono, x = A cos t, y = B sin t cos t - its head
        // at the right-hand end biting its own tail. The body tapers from thick behind the head to
        // a thin tail in its jaws; dark steel, lit along the top of the body, scaled, a red eye.
        // The two strands cross at the centre, over the blade's base.

        const float OuroA = 9.2f;                      // half the figure's width
        // Tall enough that each lobe keeps an open eye - squashed, the loops closed into a bar.
        const float OuroB = 4.8f;                      // the lobes' height is B/2 either side
        const float OuroY = 58f;                       // the crossing's row
        const float OuroThick = 0.72f, OuroTail = 0.26f;

        static List<(Vector2 P, float T)> _ouro;

        static List<(Vector2 P, float T)> OuroCurve
        {
            get
            {
                if (_ouro != null) return _ouro;
                var pts = new List<(Vector2, float)>();
                const int n = 480;
                for (int i = 0; i <= n; i++)
                {
                    // Start a little past the head so the tail can end INSIDE the jaws.
                    float u = i / (float)n;
                    float t = 0.18f + u * (Mathf.PI * 2f - 0.18f);
                    pts.Add((new Vector2(OuroA * Mathf.Cos(t), OuroY + OuroB * Mathf.Sin(t) * Mathf.Cos(t)), u));
                }
                _ouro = pts;
                return _ouro;
            }
        }

        static char Ouroboros(float dx, float y, bool fine)
        {
            // The head: a blunt oval at the right-hand end, jaws closed on the tail.
            var head = new Vector2(OuroA + 0.35f, OuroY - 0.15f);
            float hx = (dx - head.x) / 1.25f, hy = (y - head.y) / 0.95f;
            float hr = hx * hx + hy * hy;
            if (hr < 1f)
            {
                float ex = dx - (head.x + 0.35f), ey = y - (head.y - 0.35f);
                if (ex * ex + ey * ey < (fine ? 0.07f : 0.2f)) return fine ? 'e' : 'E';     // the eye
                if (fine && hr > 0.8f) return 'j';
                return hy < -0.2f ? '9' : '8';
            }

            // The body: nearest point on the curve, and how thick the snake is there.
            float best = float.MaxValue, bestU = 0f;
            Vector2 bestP = default;
            var p = new Vector2(dx, y);
            foreach (var (q, u) in OuroCurve)
            {
                float d = (q - p).sqrMagnitude;
                if (d < best) { best = d; bestU = u; bestP = q; }
            }
            float r = Mathf.Lerp(OuroThick, OuroTail, bestU);
            float dist = Mathf.Sqrt(best);
            if (dist >= r) return '.';

            float across = (y - bestP.y) / r;                  // -1 top of the body .. 1 underside
            // Lit steel, not black - on a dark background a black snake was only its outline.
            if (fine && dist > r - 0.15f) return '0';
            // Scales: a chevron every so often along the body, on the menu grid.
            if (fine && Frac(bestU * 60f + Mathf.Abs(across) * 0.3f) < 0.18f) return '7';
            if (across < -0.3f) return '9';
            if (across > 0.45f) return '7';
            return '8';
        }

        // ---------------------------------------------------------------- the track

        static List<Vector2> _loop;
        static List<float> _loopS;
        static float _loopLength;

        /// <summary>The groove the bead runs: the opening's outline pushed out by TrackOut, as a
        /// dense closed polyline in travel order - clockwise from the top point.</summary>
        static List<Vector2> Loop
        {
            get
            {
                if (_loop != null) return _loop;
                float o = TrackOut;
                var corners = new[]
                {
                    new Vector2(0f, HoleTop - o * 1.2f),
                    new Vector2(HoleHalf + o, HoleShoulder - o * 0.4f),
                    new Vector2(HoleHalf + o, HoleSideEnd + o * 0.4f),
                    new Vector2(0f, HoleBottom + o * 1.2f),
                    new Vector2(-HoleHalf - o, HoleSideEnd + o * 0.4f),
                    new Vector2(-HoleHalf - o, HoleShoulder - o * 0.4f),
                };
                var pts = new List<Vector2>();
                for (int i = 0; i < corners.Length; i++)
                {
                    var a = corners[i];
                    var b = corners[(i + 1) % corners.Length];
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.2f));
                    for (int k = 0; k < n; k++) pts.Add(Vector2.Lerp(a, b, k / (float)n));
                }
                _loopS = new List<float>(pts.Count) { 0f };
                for (int i = 1; i < pts.Count; i++) _loopS.Add(_loopS[i - 1] + Vector2.Distance(pts[i - 1], pts[i]));
                _loopLength = _loopS[pts.Count - 1] + Vector2.Distance(pts[pts.Count - 1], pts[0]);
                _loop = pts;
                return _loop;
            }
        }

        /// <summary>The bead's centre at <paramref name="phase"/> (0..1 round the loop).</summary>
        static Vector2 BeadAt(float phase)
        {
            var pts = Loop;
            float s = Frac(phase) * _loopLength;
            int i = _loopS.BinarySearch(s);
            if (i < 0) i = Mathf.Max(0, ~i - 1);
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            float segLen = Mathf.Max(0.0001f, Vector2.Distance(a, b));
            return Vector2.Lerp(a, b, Mathf.Clamp01((s - _loopS[i]) / segLen));
        }

        static readonly Dictionary<Vector2Int, float> _trackCache = new();

        /// <summary>Distance to the groove. Cached on the arena grid, which the bead's frames
        /// resample every frame.</summary>
        static float TrackDistance(float dx, float y, bool fine)
        {
            Vector2Int key = default;
            if (!fine)
            {
                key = new Vector2Int(Mathf.RoundToInt(dx * 2f), Mathf.RoundToInt(y * 2f));
                if (_trackCache.TryGetValue(key, out var hit)) return hit;
            }

            float best = float.MaxValue;
            var p = new Vector2(dx, y);
            foreach (var q in Loop)
            {
                if (Mathf.Abs(q.y - y) > 2f) continue;
                float d = (q - p).sqrMagnitude;
                if (d < best) best = d;
            }
            best = Mathf.Sqrt(best);
            if (!fine) _trackCache[key] = best;
            return best;
        }

        // ---------------------------------------------------------------- the bead's flipbook

        const int BeadFrameCount = 36;

        /// <summary>
        /// The bead's circuit as a flipbook, for <see cref="PhantomHaze"/> to cycle - the same
        /// "the weapon layer's sprite, swapped on a timer" seam Phantom's gas already uses.
        /// Arena art only, built through <see cref="StageSprite"/> so every frame is doubled
        /// exactly as the base layer is.
        /// </summary>
        static Sprite[] BeadFrames(string key, Vector2Int grip, System.Func<float, string[]> rows)
        {
            var pal = TriaPal();
            var frames = new Sprite[BeadFrameCount];
            for (int i = 0; i < BeadFrameCount; i++)
                frames[i] = StageSprite($"{key}.bead{i}", rows(BeadRestPhase + i / (float)BeadFrameCount),
                                        pal, grip);
            return frames;
        }

        // ================================================================ the fused sword
        //
        // Front to back, the first thing drawn at a point wins:
        //
        //   the bead       riding the opening's rim, over the Reactor's edge so it stays visible
        //   the Reactor    down the Pacemaker's opening and OVER the Ripsaw's bar - its plates
        //                  and core lie on the chainsaw, which is what keeps the core visible
        //   the Ripsaw     bar, chain and cutters, under the Reactor, over the Pacemaker
        //   the Pacemaker  outermost, its ouroboros the guard
        //
        // and the hilt below row 60, which is the same on all three. The Reactor and Ripsaw are
        // the SAME blades as the lone swords, set deeper - their points start lower (FusedTip)
        // and nothing else about them changes, so the motor and the core sit exactly where they
        // sit on the lone swords.

        // The Reactor is NOT set deeper: its point lies over the Pacemaker's own point, the fused
        // Reactor is exactly the lone one.
        const float ReactorFusedTip = 0f;
        const float RipsawFusedTip = 19f;

        static string[] TriaRows(float phase) => Arena((x, y, f) => TriaTexel(x, y, f, phase), SwordCanvas);

        static char TriaTexel(float x, float y, bool fine, float phase)
        {
            float dx = x - SwordAxis;
            if (y >= TriaGripTop) return TriaHilt(dx, y, fine);

            char c;
            if (y < TriaBladeBase)
            {
                c = Bead(dx, y, fine, phase);
                if (c != '.') return c;
            }
            else
            {
                // The guard: the Reactor's violet node, then the ouroboros OVER the Reactor's
                // collar, then the collar and the Pacemaker's own. The Ripsaw's chain and cutters
                // stop here - they end at the ouroboros rather than running down beside it.
                c = ReactorNode(dx, y, phase - BeadRestPhase);
                if (c != '.') return c;
                c = Ouroboros(dx, y, fine);
                if (c != '.') return c;
                c = ReactorAt(dx, y, fine, ReactorFusedTip, phase - BeadRestPhase);
                if (c != '.') return c;
                return PacemakerAt(dx, y, fine, phase);
            }
            c = ReactorAt(dx, y, fine, ReactorFusedTip, phase - BeadRestPhase);
            if (c != '.') return c;
            // The chain runs in step with the bead: one bead circuit, SawChainPerLoop cells of chain.
            // No motor on the fused sword - it belongs to the lone Ripsaw; fused, the bar runs on
            // under the Reactor without it.
            c = RipsawAt(dx, y, fine, RipsawFusedTip, (phase - BeadRestPhase) * SawChainPerLoop, motor: false);
            if (c != '.') return c;
            return PacemakerAt(dx, y, fine, phase);
        }

        // ================================================================ the relic
        //
        // The Tria Prima itself: sulfur at the apex, salt and mercury at the base, the Reactor's
        // light at the centre, a dark steel triangle holding them. 12 x 11.

        static readonly string[] TriaRelicRows =
        {
            "....hlll....",
            "...hllllb...",
            "....lbbd....",
            "....7..0....",
            "...7....0...",
            "...7.55.0...",
            "..7..54..0..",
            "..7......0..",
            "acce.77.QPPP",
            "cCiA7777PRQP",
            "aefc....PPPN",
        };
    }
}
