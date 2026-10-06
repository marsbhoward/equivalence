using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// Cloth that takes the colour of the worn Back piece (<see cref="GearItem.DyedByBack"/>) -
    /// the Survivor tabard, white on its own, red under a red cape.
    ///
    /// The cloth is AUTHORED in one known ramp, <see cref="Undyed"/>, and repainted at Apply the
    /// way bare skin is (BodyLook.Reskin): every texel matching one of its six tones becomes the
    /// same tone of a ramp built round the cape's MASS colour. The menu art survives this because
    /// PixelDetail only moves texels between tones of the same ramp - nothing it makes is off the
    /// palette.
    ///
    /// The colour is MEASURED off the cape sprite rather than declared on each Back item, so every
    /// cloak in the catalogue dyes the cloth with nothing added to it, and a new one does too. The
    /// mass is the commonest INTERIOR colour (texels whose four neighbours are all opaque): the
    /// outline is one colour on every texel it has, and a large cape's border still outnumbers
    /// any single interior tone if it is counted.
    ///
    /// Nothing here is per-instance state - the caches are statics keyed by sprite, so a domain
    /// reload empties them and the next Apply measures again.
    /// </summary>
    public static class ClothDye
    {
        /// <summary>
        /// White cloth, the ramp a dyed piece is authored in. Warm-neutral so none of its tones
        /// sits on a cool steel tone (Recolour matches within one step a channel, and a match
        /// would dye the metal too); shadows placed as fractions of LIGHT so its folds stay soft
        /// grey rather than collapsing toward black.
        /// </summary>
        public static readonly Palette.Ramp Undyed =
            new Palette.Ramp(new Color(0.86f, 0.85f, 0.81f), lift: 0.50f, line: 0.62f)
                .WithShadowsBelowLight(0.80f, 0.64f);

        /// <summary>
        /// The shade a far-side layer is drawn in (the far pauldron, ShouldersBack): its cloth is
        /// <see cref="Undyed"/> scaled by this, and dyes to the dyed ramp scaled by it, so the far
        /// side stays in shade under every cape. Must equal the set's own far factor.
        /// </summary>
        public const float Far = 0.85f;

        static readonly Dictionary<Sprite, Color?> _mass = new();

        /// <summary>The colour the cloth should take under <paramref name="back"/>, or false for
        /// no Back piece (or one with no readable cape) - the cloth stays white.</summary>
        public static bool TryMassOf(GearItem back, out Color mass)
        {
            mass = default;
            var cape = CapeOf(back);
            if (cape == null) return false;
            if (!_mass.TryGetValue(cape, out var measured)) _mass[cape] = measured = Measure(cape);
            if (measured is not Color c) return false;
            mass = c;
            return true;
        }

        /// <summary>A dyed piece's sprite with its cloth in the ramp built round
        /// <paramref name="mass"/>. Cached by PixelSprite.Recolour, one entry per colour.</summary>
        public static Sprite Dye(Sprite cloth, Color mass)
        {
            Palette.Ramp from = Undyed, to = RampFor(mass);
            Palette.Ramp fromFar = from.Scaled(Far), toFar = to.Scaled(Far);
            // Near tones first: Recolour takes the first match within a step.
            return PixelSprite.Recolour(cloth, "dye." + ColorUtility.ToHtmlStringRGB(mass),
                new[] { from.Line, from.Deep, from.Dark, from.Base, from.Light, from.Glow,
                        fromFar.Line, fromFar.Deep, fromFar.Dark, fromFar.Base, fromFar.Light, fromFar.Glow },
                new[] { to.Line, to.Deep, to.Dark, to.Base, to.Light, to.Glow,
                        toFar.Line, toFar.Deep, toFar.Dark, toFar.Base, toFar.Light, toFar.Glow });
        }

        /// <summary>
        /// Six tones round a cape's mass colour, by HUE: value scaled up and down, saturation
        /// eased off toward the light. Not Palette.Ramp's lerp toward white, which turns a lit
        /// red pink (the Hellspawn Cape's note); not toward black either, which collapses a black
        /// cloak's folds. The small additive lift is for that cloak, so its lit side still reads.
        /// </summary>
        public static Palette.Ramp RampFor(Color mass)
        {
            Color.RGBToHSV(mass, out float h, out float s, out float v);
            Color Tone(float vMul, float vAdd, float sMul)
                => Color.HSVToRGB(h, Mathf.Clamp01(s * sMul), Mathf.Clamp01(v * vMul + vAdd));
            return Palette.Ramp.FromTones(
                glow: Tone(1.30f, 0.10f, 0.72f),
                light: Tone(1.14f, 0.04f, 0.88f),
                b: mass,
                dark: Tone(0.76f, 0f, 1f),
                deep: Tone(0.58f, 0f, 1f),
                line: Tone(0.36f, 0f, 1f));
        }

        /// <summary>The Back piece's cape sprite - RigLayer.Back, the cloth seen behind the body.</summary>
        static Sprite CapeOf(GearItem back)
        {
            if (back == null || back.Slot != GearSlot.Back || back.Layers == null) return null;
            foreach (var l in back.Layers)
                if (l != null && l.Layer == RigLayer.Back && l.Sprite != null) return l.Sprite;
            return null;
        }

        static Color? Measure(Sprite s)
        {
            var tex = s.texture;
            if (tex == null || !tex.isReadable) return null;

            var px = tex.GetPixels32();
            int tw = tex.width;
            var r = s.textureRect;
            int x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && px[(y0 + y) * tw + x0 + x].a > 0;

            var counts = new Dictionary<int, int>();
            int best = -1, bestKey = 0;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!Solid(x, y) || !Solid(x - 1, y) || !Solid(x + 1, y) || !Solid(x, y - 1) || !Solid(x, y + 1))
                    continue;
                var c = px[(y0 + y) * tw + x0 + x];
                int key = (c.r << 16) | (c.g << 8) | c.b;
                counts.TryGetValue(key, out int n);
                counts[key] = ++n;
                if (n > best) { best = n; bestKey = key; }
            }
            if (best < 0) return null;
            return (Color)new Color32((byte)(bestKey >> 16), (byte)(bestKey >> 8), (byte)bestKey, 255);
        }
    }
}
