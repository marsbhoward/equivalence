using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    public static partial class DemoGear
    {
        // ------------------------------------------------------------------ the Wraith's Eye
        //
        // WORN it has no art at all - the item IS the character's own left eye, recoloured to a
        // fixed red and pulsing (HasGlowingEye, BodyLook.Head, GlowingEye). A helm would cover the
        // very eye it exists to show. But a card, the armoury wall and its NFT image need a
        // picture, so it gets a DISPLAY picture only (GearItem.DisplayLayer - the disc pair's
        // mechanism): never on the body, so the worn look is untouched.
        //
        // The user's pick (2026-10-09, replacing a disembodied eye in smoke): a BLANK DARK HEAD
        // with the red eye where the worn one sits. The head is the rig's own (BodyLook.
        // BareHeadDetail - no hair, no beard), flattened to one dark tone with its line a shade
        // darker; only the glowing left eye - iris, white and lash - keeps its colour. Built from
        // the same pass the worn eye is, so the picture's eye can't drift from the real one.
        //
        // AND ITS LIGHT: worn, GlowingEye lays a soft red Spr.Glow over the eye, pulsing. A
        // picture can't pulse, so the light is BAKED at the top of its pulse - same colour, same
        // size and same centre (the coarse head's eye box, measured as SyncGlowEyeSprite does),
        // blended over the texels exactly as the renderer would. Blending makes colours no
        // grid letter names, so each one gets a letter of its own past the head palette's.

        const string WraithEyeExpression = "neutral";

        /// <summary>The head grid at <paramref name="scale"/>, every texel but the left eye's
        /// turned to the silhouette's two letters ('V' body, 'U' line).</summary>
        static string[] WraithEyeSilhouette(int scale)
        {
            var head = BodyLook.BareHeadDetail(WraithEyeExpression, leftEyeGlow: true, scale);

            // The left eye: the glow letters' box, widened enough to keep its white and lash.
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            for (int y = 0; y < head.Length; y++)
            for (int x = 0; x < head[y].Length; x++)
            {
                if (!IsEyeGlow(head[y][x])) continue;
                x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x);
                y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y);
            }
            int pad = 2 * scale;

            var rows = new string[head.Length];
            for (int y = 0; y < head.Length; y++)
            {
                var line = head[y].ToCharArray();
                for (int x = 0; x < line.Length; x++)
                {
                    char c = line[x];
                    if (c == '.') continue;
                    bool inEye = x >= x0 - pad && x <= x1 + pad && y >= y0 - pad && y <= y1 + pad;
                    if (IsEyeGlow(c) || (inEye && (c == 'w' || c == 'x'))) continue;
                    line[x] = c == 'k' ? 'U' : 'V';
                }
                rows[y] = new string(line);
            }
            return rows;
        }

        /// <summary>The silhouette with GlowingEye's light blended in at <paramref name="scale"/>
        /// texels per head texel. Returns the rows and the palette they need.</summary>
        static (string[] rows, Dictionary<char, Color> pal) WraithEyeLit(int scale)
        {
            var rows = WraithEyeSilhouette(scale);
            var pal = WraithEyePalette();

            // Centred where the rig puts it: the COARSE head's eye box, in head texels.
            var coarse = BodyLook.Head(WraithEyeExpression, "none", leftEyeGlow: true);
            var box = BodyLook.FindLeftEyeBox(coarse);
            if (box == null) return (rows, pal);
            float cx = (box.Value.x0 + box.Value.x1 + 1) * 0.5f * scale;
            float cy = (box.Value.y0 + box.Value.y1 + 1) * 0.5f * scale;
            // GlowingEye's world diameter at the head's own density (BodyPpu per head texel).
            float radius = GlowingEye.WorldSize * 0.5f * BodyPpu * scale;
            var red = GlowingEye.Colour;

            var lit = new Dictionary<Color32, char>();
            char next = '\u0100';
            var outRows = new string[rows.Length];
            for (int y = 0; y < rows.Length; y++)
            {
                var line = rows[y].ToCharArray();
                for (int x = 0; x < line.Length; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float t = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) / radius);
                    float a = t * t * GlowingEye.PeakAlpha;   // Spr.Glow's falloff
                    if (a <= 0.002f) continue;
                    var under = line[x] == '.' ? new Color(red.r, red.g, red.b, 0f) : pal[line[x]];
                    // Straight alpha-over, as the SpriteRenderer draws it.
                    float outA = a + under.a * (1f - a);
                    var c = new Color(
                        (red.r * a + under.r * under.a * (1f - a)) / outA,
                        (red.g * a + under.g * under.a * (1f - a)) / outA,
                        (red.b * a + under.b * under.a * (1f - a)) / outA, outA);
                    Color32 key = c;
                    if (!lit.TryGetValue(key, out var ch)) { ch = next++; lit[key] = ch; pal[ch] = key; }
                    line[x] = ch;
                }
                outRows[y] = new string(line);
            }
            return (outRows, pal);
        }

        static bool IsEyeGlow(char c) => c is 'A' or 'E' or 'F' or 'G' or 'I' or 'J';

        static Dictionary<char, Color> WraithEyePalette()
        {
            // The head's own palette, so the eye's letters come out exactly as worn.
            var grey = new Palette.Ramp(new Color(0.5f, 0.5f, 0.5f));
            var pal = BodyLook.HeadPalette(grey, grey, grey);
            pal['V'] = new Color(0.10f, 0.08f, 0.13f);   // the silhouette
            pal['U'] = new Color(0.04f, 0.03f, 0.06f);   // its line
            return pal;
        }

        /// <summary>The Wraith's Eye's display picture (cards, the wall, its NFT image).</summary>
        static void WraithEyeDisplay(GearItem item)
        {
            var (arena, arenaPal) = WraithEyeLit(1);
            var (menu, menuPal) = WraithEyeLit(BodyLook.DetailScale);
            item.DisplayLayer = Pixels(RigLayer.HeadArmor, "gear.head.wraith_eye.display", arena, arenaPal, 0f, 0f,
                                       pivotTexel: new Vector2Int(arena[0].Length / 2, arena.Length / 2),
                                       ppu: BodyPpu, outline: false);
            item.DisplayMenuLayer = Pixels(RigLayer.HeadArmor, "gear.head.wraith_eye.display.menu", menu, menuPal, 0f, 0f,
                                           pivotTexel: new Vector2Int(menu[0].Length / 2, menu.Length / 2),
                                           ppu: BodyPpu * BodyLook.DetailScale, outline: false);
        }
    }
}
