using System.Collections.Generic;
using Convergence.Core;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// THE SECRET FIRE - the alchemists' ignis secretus, the one hidden fire that drives the whole
    /// work. Here it is what burns in the marks cut into a KINDLED piece (<see cref="GearItem.Kindled"/>):
    /// the Aether set's curse marks and its greatsword's fuller, lit in the wearer's element
    /// (<see cref="Attunement"/>) and going out to black, all of them together.
    ///
    /// THE MARKS ARE TEXELS OF THE PIECE'S OWN SPRITE, painted in three reserved near-black colours
    /// (<see cref="Kindle"/>). That is the UNLIT picture - what an NFT image, the 1x PNG and the bottom
    /// of every pulse show. Lit, an overlay is drawn over them (<see cref="KindledMarks"/> in the world,
    /// UI.KindledImage on a card): the same texels in the element's own three tones, everything else
    /// clear, faded by the one brightness this class answers for the whole game.
    ///
    /// WHY TEXELS AND NOT A SEPARATE MASK. Every transform the rig and the displays put a sprite
    /// through - the elbow, wrist and knee cuts, a lopsided mirror, the hood's enclosure, a dye,
    /// derived menu art (PixelDetail shades only the three ramps) - copies texels, so the reserved
    /// colours ride through all of them for free and the overlay is derived from whatever sprite is
    /// ACTUALLY on screen. The two transforms that should put the fire out do: a hit flash
    /// (PixelSprite.Silhouette) and Medusa's stone (PixelSprite.Stone) leave no reserved colour
    /// behind, so a flash is white all over and a statue's marks are cold.
    ///
    /// ONE CLOCK. The brightness is a function of unscaled time alone - no per-instance phase, no
    /// per-material animation - so every mark on every piece glows and fades on the same beat, on
    /// the character, the armour stand, the rack, the armoury wall and every card. Unscaled because
    /// every screen that shows a card pauses the game.
    ///
    /// AN ELEMENT CHANGE NEVER SNAPS. The marks go out first, the colour changes in the dark, and
    /// they come back in the new one (<see cref="Tuning.SecretFire.SwapSeconds"/>). The change time is
    /// global (<see cref="Attunement.ChangedAt"/>), so every piece does it together without any of
    /// them keeping state of its own.
    /// </summary>
    public static class SecretFire
    {
        // ------------------------------------------------------------------ the reserved marks
        //
        // Grid letters outside the three ramps, so the derived menu art leaves them alone, in three
        // colours nothing else in the project paints: near-black with a WARM cast (red above blue),
        // where every dark ramp here is cool. Black to the eye - the marks' resting colour, burnt
        // out - and exact to the byte, which is how the overlay finds them again.

        /// <summary>The hottest texels: a seal's heart, where tendrils meet.</summary>
        public const char Core = '@';

        /// <summary>The body of a mark - most of every line.</summary>
        public const char Vein = '*';

        /// <summary>The dimmest: a tendril's last texels, where it dies out into the plate or cloth.</summary>
        public const char Ember = '+';

        static readonly Color32[] Unlit =
        {
            new(6, 1, 2, 255),     // Core
            new(9, 2, 3, 255),     // Vein
            new(12, 3, 4, 255),    // Ember
        };

        /// <summary>
        /// Add the three mark letters to a palette, in their unlit colours. Call it LAST, after any
        /// scaling for a far limb - the marks are matched to the byte, and a scaled mark is no longer
        /// one. A far-side mark glows as brightly as a near one: it is light, not a lit surface.
        /// </summary>
        public static Dictionary<char, Color> Kindle(Dictionary<char, Color> palette)
        {
            palette[Core] = Unlit[0];
            palette[Vein] = Unlit[1];
            palette[Ember] = Unlit[2];
            return palette;
        }

        /// <summary>Which mark tone a texel is (0 core, 1 vein, 2 ember), or -1 for anything else.
        /// Within a step a channel, Recolour's tolerance: a colour goes in as a float.</summary>
        static int ToneOf(Color32 c)
        {
            if (c.a == 0) return -1;
            for (int k = 0; k < Unlit.Length; k++)
            {
                var u = Unlit[k];
                if (Mathf.Abs(c.r - u.r) <= 1 && Mathf.Abs(c.g - u.g) <= 1 && Mathf.Abs(c.b - u.b) <= 1)
                    return k;
            }
            return -1;
        }

        // ------------------------------------------------------------------ the element's colours

        /// <summary>
        /// The three tones a mark burns in for <paramref name="element"/>: [0] core, [1] vein,
        /// [2] ember. The VEIN is the element's own tint (ElementInfo.Tint, the colour every other
        /// piece of the game already says that element in), the core runs hot toward that element's
        /// own light, and the ember is the tint deepened rather than greyed - a dying mark keeps its
        /// colour. Fire's core goes toward yellow, never white: a lit red-orange lifted to white
        /// reads pink. Air's tint is nearly white, so its ember carries the colour instead.
        /// </summary>
        public static Color Tone(ElementType element, int tone)
        {
            var t = Tones(element);
            return t[Mathf.Clamp(tone, 0, t.Length - 1)];
        }

        static Color[] Tones(ElementType element) => element switch
        {
            ElementType.Fire  => new[] { new Color(1.00f, 0.86f, 0.46f), ElementInfo.Tint(element), new Color(0.74f, 0.13f, 0.06f) },
            ElementType.Water => new[] { new Color(0.80f, 0.96f, 1.00f), ElementInfo.Tint(element), new Color(0.10f, 0.28f, 0.74f) },
            ElementType.Earth => new[] { new Color(0.90f, 1.00f, 0.64f), ElementInfo.Tint(element), new Color(0.24f, 0.48f, 0.14f) },
            _                 => new[] { new Color(1.00f, 1.00f, 1.00f), ElementInfo.Tint(element), new Color(0.50f, 0.48f, 0.82f) },
        };

        // ------------------------------------------------------------------ the one clock

        /// <summary>
        /// The bare beat at unscaled time <paramref name="t"/>, 0..1: rise, hold lit, fade to black,
        /// hold dark. Eased at both ends of each ramp so a mark never jolts on or off.
        /// </summary>
        public static float Pulse(float t)
        {
            const float rise = Tuning.SecretFire.Rise, hold = Tuning.SecretFire.Hold, fade = Tuning.SecretFire.Fade;
            float p = t / Tuning.SecretFire.PeriodSeconds;
            p -= Mathf.Floor(p);
            if (p < rise) return Mathf.SmoothStep(0f, 1f, p / rise);
            p -= rise;
            if (p < hold) return 1f;
            p -= hold;
            if (p < fade) return 1f - Mathf.SmoothStep(0f, 1f, p / fade);
            return 0f;
        }

        /// <summary>How far through an element change the marks are: 1 outside one, falling to 0
        /// at its middle (the dark the colour changes in) and back.</summary>
        static float Swap(float t)
        {
            float since = t - Attunement.ChangedAt, all = Tuning.SecretFire.SwapSeconds;
            if (since < 0f || since >= all) return 1f;
            float half = all * 0.5f;
            return Mathf.Abs(since - half) / half;
        }

        /// <summary>Every kindled mark's brightness right now, 0 (black) to 1 (lit).</summary>
        public static float Brightness
            => DevBrightness.HasValue ? Mathf.Clamp01(DevBrightness.Value) : BrightnessAt(Time.unscaledTime);

        /// <summary>The brightness at unscaled time <paramref name="t"/>: the beat, dimmed through
        /// any element change.</summary>
        public static float BrightnessAt(float t) => Pulse(t) * Swap(t);

        /// <summary>Testing: holds every mark at this brightness instead of the beat (null = live),
        /// so a still - GearSheet, a screenshot - can be taken lit or dark on purpose.</summary>
        public static float? DevBrightness;

        /// <summary>
        /// The overlay's alpha for <see cref="Brightness"/>. The project blends in linear space, so a
        /// straight alpha over black keeps a mark looking near full until the very end of its fade
        /// and then drops it; raised to a gamma, the fade reads as even all the way down.
        /// </summary>
        public static float Alpha => Mathf.Pow(Brightness, Tuning.SecretFire.Gamma);

        /// <summary>The element whose colours burn right now - the OLD one until the middle of a
        /// change, while the marks are dark, then the new.</summary>
        public static ElementType Shown => ShownAt(Time.unscaledTime);

        public static ElementType ShownAt(float t)
            => t < Attunement.ChangedAt + Tuning.SecretFire.SwapSeconds * 0.5f
                ? Attunement.Previous : Attunement.Current;

        // ------------------------------------------------------------------ the overlay

        static readonly Dictionary<(Sprite, ElementType), Sprite> _overlays = new();
        static readonly HashSet<Sprite> _unmarked = new();

        /// <summary>Whether <paramref name="src"/> carries any reserved mark texel.</summary>
        public static bool HasMarks(Sprite src) => Overlay(src, ElementType.Fire) != null;

        /// <summary><paramref name="src"/>'s marks lit in the colours burning now, or null for a
        /// sprite with none.</summary>
        public static Sprite Overlay(Sprite src) => Overlay(src, Shown);

        /// <summary>
        /// <paramref name="src"/>'s marks in <paramref name="element"/>'s tones, every other texel
        /// clear - drawn at the source's own transform, the same size, pivot and density, so it lands
        /// exactly on the marks. Null for a sprite with no marks (a flash, a statue, any unkindled
        /// art) or one whose texture is not readable. Cached per (source, element); the no-marks
        /// answer is cached too, since the world asks every frame.
        /// </summary>
        public static Sprite Overlay(Sprite src, ElementType element)
        {
            if (src == null || _unmarked.Contains(src)) return null;
            if (_overlays.TryGetValue((src, element), out var cached) && cached != null) return cached;

            var st = src.texture;
            if (st == null || !st.isReadable) { _unmarked.Add(src); return null; }

            var r = src.rect;
            int w = (int)r.width, h = (int)r.height;
            var px = st.GetPixels((int)r.x, (int)r.y, w, h);
            var tones = Tones(element);
            var outPx = new Color[w * h];
            bool any = false;
            for (int i = 0; i < px.Length; i++)
            {
                int tone = ToneOf(px[i]);
                if (tone < 0) continue;
                outPx[i] = tones[tone];
                any = true;
            }
            if (!any) { _unmarked.Add(src); return null; }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(outPx);
            tex.Apply();

            var pivot01 = new Vector2(src.pivot.x / w, src.pivot.y / h);
            var made = Sprite.Create(tex, new Rect(0, 0, w, h), pivot01, src.pixelsPerUnit);
            made.name = src.name + ".fire." + element;
            _overlays[(src, element)] = made;
            return made;
        }
    }
}
