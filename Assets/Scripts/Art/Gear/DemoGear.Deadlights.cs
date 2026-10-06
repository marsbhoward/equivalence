using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== Deadlights: the mouth, and what is in it
    //
    // After the user's reference: Pennywise's jaws opened into a tall oval, and deep in the
    // throat three small lights in a triangle with the glow pouring off them. Brief on top of
    // that: an orange-yellow disc, CRIMSON lights, and a blood-red handle SPLIT into two rails to
    // leave room for them. The reference's rings of fangs are deliberately LEFT OUT (the user's
    // call - an echo of it, not a copy); the glow and the lights carry it.
    //
    // Outside in: the RUFF (orange-yellow, the rim scalloped into pleats - the collar under the
    // jaws, and the silhouette that tells this disc from the plain one), a LIP in the rails'
    // blood red, and the THROAT - opaque, dark at the lip and brightening to gold-white round the
    // lights. The rails cross in front of all of it, the lights between them.
    //
    // The PIVOT is on the UPPER rail. The near hand's fist is painted over the grip at the pivot
    // (SetWeaponSplit's note); centred, it would sit on the lights.
    //
    // ONE field (DeadTexel), sampled per cell for the arena and per QUARTER cell for the menu.
    // Mirror-safe - everything angular reads |x|, and at rest the lights are a symmetric
    // triangle - so the unmirrored off-hand copy is the same picture. The lights TURN in the arena
    // (DeadFrames), both discs the same way: SetWeaponSprite now carries each frame to the
    // off-hand copy too, which otherwise froze on frame 0.
    public static partial class DemoGear
    {
        /// <summary>The disc standard's height (DiscHeightCells), a little narrower - a mouth
        /// is taller than wide. Everything below is placed relative to the centre, so the radius
        /// can follow the standard.</summary>
        const int DeadW = 2 * ((int)DeadR + 1), DeadH = DiscHeightCells;   // width follows the radius
        const float DeadCx = DeadW / 2f, DeadCy = DeadH / 2f;
        /// <summary>Outer radius across, at a pleat's crest, in cells: whatever makes the oval
        /// exactly the disc standard tall.</summary>
        const float DeadR = DiscHeightCells / (2f * DeadOval);
        /// <summary>A mouth is taller than wide.</summary>
        const float DeadOval = 1.08f;
        /// <summary>Pleats round the rim - even, one centred at the top, so it mirrors.</summary>
        const int DeadPleats = 18;
        /// <summary>How far the rim dips between pleats, in radii.</summary>
        const float DeadPleatDepth = 0.10f;

        // The bands, as fractions of the (oval) radius.
        const float DeadRuffInner = 0.80f;   // ruff outside this
        const float DeadGumInner = 0.73f;    // the lip, between this and the ruff; throat inside

        /// <summary>The two rails as [top, bottom) rows: two rows each - together the plain disc's
        /// four-row bar, split - with the light cluster and its glow between them.</summary>
        const float DeadRailTopY = DeadCy - 6f, DeadRailBottomY = DeadCy + 5f, DeadRailRows = 2f;

        /// <summary>
        /// The three lights: an equilateral triangle round the glow's centre, at rest a pair above
        /// and one below (angles counted UP from the right, so increasing is counter-clockwise on
        /// screen). They TURN: <see cref="DeadFrames"/> rotates them counter-clockwise, a third
        /// of a turn per loop - the triangle is the same again after 120 degrees.
        ///
        /// In the ARENA each centre is snapped to the nearest cell BOUNDARY, so a light is always a
        /// clean 2x2 stepping round the circle; left continuous, a light that small changed shape
        /// every frame (one cell, a lopsided three, a 2x2) and read as flicker, not motion. At rest
        /// the snap lands exactly on the static layout.
        /// </summary>
        static readonly float[] DeadLightRestDegrees = { 150f, 30f, 270f };
        const float DeadLightOrbit = 2.2f;
        const float DeadLightRadius = 0.95f;
        static readonly Vector2 DeadGlowCentre = new(DeadCx, DeadCy);

        /// <summary>One full turn of the lights, in seconds - slow, a drift rather than a spin.</summary>
        const float DeadTurnSeconds = 10.8f;
        /// <summary>Frames per third of a turn (the loop).</summary>
        const int DeadTurnFrames = 36;

        static Vector2[] DeadLightsAt(float turnDegrees, bool snap)
        {
            var at = new Vector2[DeadLightRestDegrees.Length];
            for (int i = 0; i < at.Length; i++)
            {
                float a = (DeadLightRestDegrees[i] + turnDegrees) * Mathf.Deg2Rad;
                var p = new Vector2(DeadGlowCentre.x + Mathf.Cos(a) * DeadLightOrbit,
                                    DeadGlowCentre.y - Mathf.Sin(a) * DeadLightOrbit);  // rows run DOWN
                at[i] = snap ? new Vector2(Mathf.Round(p.x), Mathf.Round(p.y)) : p;
            }
            return at;
        }

        const int DeadDetailScale = 4;
        static readonly Vector2Int DeadGrip = new((int)DeadCx, (int)(DeadRailTopY + DeadRailRows / 2f));
        static readonly Vector2Int DeadDetailGrip = new(DeadGrip.x * DeadDetailScale, DeadGrip.y * DeadDetailScale);

        static float DeadBayer(float x, float y, int scale)
        {
            int ix = Mathf.FloorToInt(x * scale), iy = Mathf.FloorToInt(y * scale);
            return ((ix & 1) * 2 + (iy & 1)) switch { 0 => 0.125f, 1 => 0.625f, 2 => 0.875f, _ => 0.375f };
        }

        /// <summary>
        /// The whole disc at (x, y) in arena cells, rows DOWN, at <paramref name="scale"/> texels
        /// per cell. Menu-only detail: pleat folds on the ruff, a dithered glow in the throat,
        /// white-hot cores and dithered halos on the lights, and
        /// their glow on the rails' inner faces. Thresholds are in texels (<c>px</c>).
        /// </summary>
        static char DeadTexel(float x, float y, int scale, Vector2[] lights)
        {
            bool menu = scale > 1;
            float px = 1f / scale;
            float dx = x - DeadCx, dy = y - DeadCy;
            float rho = Mathf.Sqrt(dx * dx + (dy / DeadOval) * (dy / DeadOval)) / DeadR;
            float phi = Mathf.Atan2(-dy / DeadOval, Mathf.Abs(dx));      // mirror-safe

            // ---- the ruff
            float pleat = (phi - Mathf.PI / 2f) * DeadPleats / (2f * Mathf.PI);
            float pf = pleat - Mathf.Round(pleat);
            float rim = 1f - DeadPleatDepth + DeadPleatDepth * Mathf.Sqrt(Mathf.Max(0f, 1f - 4f * pf * pf));
            if (rho > rim) return '.';
            if (rho >= DeadRuffInner)
            {
                float val = -(dx + dy) / (2f * DeadR) * 1.6f;            // lit from the upper left
                float across = (rho - DeadRuffInner) / (1f - DeadRuffInner);
                float fold = Mathf.Cos(pf * 2f * Mathf.PI);
                if (menu) val += 0.55f * fold * Mathf.Lerp(0.4f, 1f, across) - 0.1f;
                else if (across > 0.45f && fold < -0.2f) val -= 0.45f;
                if (menu && val > 0.7f) return 'h';
                return val > 0.3f ? 'l' : val > -0.1f ? 'b' : val > -0.5f ? 'd' : 's';
            }

            // ---- the split handle: two blood-red rails in front of the mouth
            bool upper = y >= DeadRailTopY && y < DeadRailTopY + DeadRailRows;
            bool lower = y >= DeadRailBottomY && y < DeadRailBottomY + DeadRailRows;
            if (upper || lower)
            {
                float into = y - (upper ? DeadRailTopY : DeadRailBottomY);
                if (!menu) return into < 1f ? 'B' : 'D';
                // the face turned toward the lights catches their glow
                bool facesLights = upper ? into > DeadRailRows - 2f * px : into < 2f * px;
                if (facesLights && Mathf.Abs(dx) < 3.5f) return Mathf.Abs(dx) < 2f ? 'H' : 'L';
                if (into < px) return 'L';
                if (into > DeadRailRows - px) return 'S';
                return into < DeadRailRows * 0.55f ? 'B' : 'D';
            }

            // ---- the lip
            if (rho >= DeadGumInner)
            {
                if (!menu) return 'D';
                float g = (rho - DeadGumInner) / (DeadRuffInner - DeadGumInner);
                return g < 0.3f ? 'S' : g > 0.8f ? 'K' : (dx + dy < 0f ? 'B' : 'D');
            }

            // ---- the lights, with their halos
            float nearest = float.MaxValue;
            foreach (var c in lights)
                nearest = Mathf.Min(nearest, Vector2.Distance(new Vector2(x, y), c));
            // Small: points of crimson ON the glow. Haloed wide, the three merged into one crimson
            // clover and covered the gold that makes the throat read as lit.
            // full crimson in the arena - the lighter tone lost its contrast against the gold
            if (!menu && nearest < DeadLightRadius) return '4';
            if (menu)
            {
                if (nearest < DeadLightRadius * 0.45f) return '6';
                if (nearest < DeadLightRadius) return nearest > DeadLightRadius - 1.1f * px ? '4' : '5';
                if (nearest < DeadLightRadius + 0.4f && DeadBayer(x, y, scale) < 0.5f) return '4';
            }

            // ---- the throat: dark at the lip, gold-white round the lights
            float dist = Vector2.Distance(new Vector2(x, y), DeadGlowCentre);
            float glow = Mathf.Exp(-dist / 4.5f) * 5.6f;                 // 0..5, one per step
            if (menu) glow += DeadBayer(x, y, scale) - 0.5f;               // dither between steps
            int step = Mathf.Clamp(Mathf.FloorToInt(glow), 0, 5);
            return "mnopqr"[step];
        }

        static string[] BuildDeadlights(int scale, float turnDegrees = 0f)
        {
            var lights = DeadLightsAt(turnDegrees, snap: scale == 1);
            int w = DeadW * scale, h = DeadH * scale;
            var rows = new string[h];
            var sb = new System.Text.StringBuilder(w);
            for (int row = 0; row < h; row++)
            {
                sb.Clear();
                for (int col = 0; col < w; col++)
                    sb.Append(DeadTexel((col + 0.5f) / scale, (row + 0.5f) / scale, scale, lights));
                rows[row] = sb.ToString();
            }
            return scale == 1 ? StripSpecks(rows) : RoseDropIslands(rows);
        }

        static string[] _deadRows, _deadDetail;
        static string[] DeadRows => _deadRows ??= BuildDeadlights(1);
        static string[] DeadDetail => _deadDetail ??= BuildDeadlights(DeadDetailScale);

        /// <summary>
        /// Orange-yellow ruff; blood-red rails and gums, highlighted toward a brighter RED (lifted
        /// toward white, blood came out pink); crimson lights lifted far toward white, the one
        /// emissive thing on it. The THROAT's glow goes past the three-ramp Palette.Of, on spare
        /// letters darkest to brightest m n o p q r - a gradient, not a ramp, so its steps are
        /// placed by hand.
        /// </summary>
        static Dictionary<char, Color> DeadPal()
        {
            var pal = Palette.Of(new Palette.Ramp(new Color(0.96f, 0.62f, 0.12f), lift: 0.30f),
                                 new Palette.Ramp(new Color(0.46f, 0.03f, 0.05f), lift: 0.30f)
                                     .WithHighlightsToward(new Color(0.95f, 0.22f, 0.16f), 0.45f),
                                 new Palette.Ramp(new Color(0.92f, 0.07f, 0.16f), lift: 0.42f));
            pal['m'] = new Color(0.20f, 0.07f, 0.04f);
            pal['n'] = new Color(0.40f, 0.15f, 0.05f);
            pal['o'] = new Color(0.68f, 0.32f, 0.08f);
            pal['p'] = new Color(0.90f, 0.56f, 0.16f);
            pal['q'] = new Color(1.00f, 0.80f, 0.40f);
            pal['r'] = new Color(1.00f, 0.95f, 0.78f);
            return pal;
        }

        /// <summary>The lights turning counter-clockwise: a third of a turn, which loops. Arena art
        /// only (GearItem.IdleFrames) - the character screen keeps the still menu picture.</summary>
        static Sprite[] DeadFrames()
        {
            var pal = DeadPal();
            var frames = new Sprite[DeadTurnFrames];
            for (int i = 0; i < DeadTurnFrames; i++)
                frames[i] = StageSprite($"gear.weapon.disc.deadlights.turn{i}",
                                        BuildDeadlights(1, 120f * i / DeadTurnFrames), pal, DeadGrip);
            return frames;
        }

        static GearItem Deadlights()
        {
            var item = Disc(WithMenu(Make("deadlights_discs", "Deadlights", GearSlot.Weapon, LootTier.Diamond, 0f,
                    Pixels(RigLayer.Weapon, "gear.weapon.disc.deadlights", DeadRows, DeadPal(),
                           DiscHandX, DiscHandY, pivotTexel: DeadGrip, ppu: FinePpu, upscale2x: true)),
                    Pixels(RigLayer.Weapon, "gear.weapon.disc.deadlights.menu", DeadDetail, DeadPal(),
                           DiscHandX, DiscHandY, pivotTexel: DeadDetailGrip, ppu: MenuPpu)));
            item.IdleFrames = DeadFrames();
            item.IdleFrameSeconds = DeadTurnSeconds / 3f / DeadTurnFrames;
            return item;
        }
    }
}
