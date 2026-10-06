using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// What a dye channel is MADE of - decides how a dye colour becomes a ramp on it, so a silver
    /// plate dyed green still reads as metal and a white tabard dyed green reads as cloth.
    /// Appended only: a channel's material is part of what a dyed item's image was rendered from.
    /// </summary>
    public enum DyeMaterial { Metal, Cloth, Leather, Gem }

    public enum DyeRarity { Common, Uncommon, Rare, VeryRare }

    /// <summary>One colour a dye can be. The <see cref="Id"/> is PERMANENT - it is what a dyed
    /// item's datum names - so a swatch is never renamed or re-coloured, only appended.</summary>
    public sealed class DyeSwatch
    {
        public readonly string Id, Name;
        public readonly Color Color;
        public readonly DyeRarity Rarity;

        public DyeSwatch(string id, string name, Color color, DyeRarity rarity)
        {
            Id = id; Name = name; Color = color; Rarity = rarity;
        }
    }

    /// <summary>
    /// Every dye in the game. A small CURATED list, never a free colour picker - a handful of
    /// colours reused everywhere reads as deliberate (Palette's founding note), and a dye market
    /// needs a finite number of things to price.
    ///
    /// The game's own colours (the four elements, the tiers, the project's named materials) plus
    /// the primaries and secondaries. Black and white are the very rare pair: every dye economy
    /// settles on them as the coveted ones.
    /// </summary>
    public static class DyeCatalog
    {
        public static readonly DyeSwatch[] All =
        {
            // ---- common: the elements and the plain working materials ----
            new("dye.ember",   "Ember",   Core.ElementInfo.Tint(Core.ElementType.Fire),  DyeRarity.Common),
            new("dye.tide",    "Tide",    Core.ElementInfo.Tint(Core.ElementType.Water), DyeRarity.Common),
            new("dye.moss",    "Moss",    Core.ElementInfo.Tint(Core.ElementType.Earth), DyeRarity.Common),
            new("dye.gale",    "Gale",    Core.ElementInfo.Tint(Core.ElementType.Air),   DyeRarity.Common),
            new("dye.iron",    "Iron",    Palette.Iron.Base,                             DyeRarity.Common),
            new("dye.leather", "Leather", Palette.Leather.Base,                          DyeRarity.Common),
            new("dye.bronze",  "Bronze",  Palette.Bronze.Base,                           DyeRarity.Common),

            // ---- uncommon: primaries, secondaries, the precious metals ----
            new("dye.red",     "Crimson", new Color(0.78f, 0.10f, 0.12f),                DyeRarity.Uncommon),
            new("dye.yellow",  "Saffron", new Color(0.96f, 0.80f, 0.16f),                DyeRarity.Uncommon),
            new("dye.blue",    "Cobalt",  new Color(0.14f, 0.26f, 0.78f),                DyeRarity.Uncommon),
            new("dye.green",   "Verdant", new Color(0.12f, 0.52f, 0.24f),                DyeRarity.Uncommon),
            new("dye.purple",  "Royal",   new Color(0.46f, 0.20f, 0.68f),                DyeRarity.Uncommon),
            new("dye.silver",  "Silver",  Palette.Tier(LootTier.Silver).Base,            DyeRarity.Uncommon),
            new("dye.gold",    "Gold",    Palette.Tier(LootTier.Gold).Base,              DyeRarity.Uncommon),
            new("dye.copper",  "Copper",  new Color(0.70f, 0.38f, 0.21f),                DyeRarity.Uncommon),

            // ---- rare ----
            new("dye.diamond", "Diamond", Palette.Tier(LootTier.Diamond).Base,           DyeRarity.Rare),
            new("dye.void",    "Void",    Palette.Void.Base,                             DyeRarity.Rare),

            // ---- very rare ----
            new("dye.black",   "Jet",     new Color(0.10f, 0.10f, 0.12f),                DyeRarity.VeryRare),
            new("dye.white",   "Pearl",   new Color(0.95f, 0.95f, 0.93f),                DyeRarity.VeryRare),
        };

        static Dictionary<string, DyeSwatch> _byId;

        public static DyeSwatch Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, DyeSwatch>();
                foreach (var s in All) _byId[s.Id] = s;
            }
            return _byId.TryGetValue(id, out var swatch) ? swatch : null;
        }
    }

    /// <summary>
    /// One dyeable material on a design - MAIN or ACCENT. Identified by the RAMP it was painted
    /// in (its six tones, Line..Glow) and the exact SHADES that ramp is drawn at (1 near, the
    /// set's far factor on far-side layers): a palette group whose base is the ramp's base times
    /// one of those shades IS this channel. The shades are declared rather than inferred because
    /// neutral greys are all proportional to one another (Talon's jerkin is 0.72 of its steel).
    /// </summary>
    [Serializable]
    public class DyeChannel
    {
        public string Name;
        public DyeMaterial Material;
        /// <summary>Line, Deep, Dark, Base, Light, Glow - the order Palette.Of maps k s d b l h.</summary>
        public Color[] Tones = new Color[6];
        public float[] Shades = { 1f };

        public Color Base => Tones[3];

        public static DyeChannel Of(string name, DyeMaterial material, Palette.Ramp ramp, params float[] farShades)
        {
            var shades = new float[farShades.Length + 1];
            shades[0] = 1f;
            Array.Copy(farShades, 0, shades, 1, farShades.Length);
            return new DyeChannel
            {
                Name = name,
                Material = material,
                Tones = new[] { ramp.Line, ramp.Deep, ramp.Dark, ramp.Base, ramp.Light, ramp.Glow },
                Shades = shades,
            };
        }
    }

    /// <summary>
    /// A dye colour onto a material: a new six-tone ramp.
    ///
    /// THE PIECE KEEPS ITS LIGHTING, THE DYE BRINGS ITS COLOUR, THE MATERIAL DECIDES HOW MUCH
    /// COLOUR EACH TONE CARRIES. Worked in OKLab, where lightness is perceptual - a blue and a
    /// yellow of equal HSV value are nowhere near equally light, and a dye has to land the same on
    /// every set.
    ///
    ///   LIGHTNESS  comes from the SOURCE ramp's structure, re-anchored so its base lands at the
    ///              dye's lightness (clamped per material - black steel still has a glint, white
    ///              steel still has a shadow). Below the base each tone keeps its RATIO to it;
    ///              above, its share of the HEADROOM to white - so a plate's sharp specular stays
    ///              sharp and a cloth's soft one stays soft. A minimum gap between neighbours is
    ///              then enforced, since a near-black source ramp has almost no spacing to carry.
    ///   HUE        is the dye's, drifting a few degrees cool into the shadows and warm into the
    ///              light (the pixel-art hue shift the authored ramps already do by hand).
    ///   CHROMA     is the dye's, times a per-tone profile for the material: metal muted in the
    ///              mids and near-white at the glint, cloth full, leather dull, gem vivid. It
    ///              cannot come from the source - silver has none to scale.
    ///
    /// Deterministic and stateless: a dyed item's image is rendered from this, so the same
    /// (ramp, material, swatch) must give the same tones every time.
    /// </summary>
    public static class DyeRecipe
    {
        sealed class Profile
        {
            public float MinL, MaxL, MaxGlowL, ChromaScale;
            public float[] Chroma;          // per tone, Line..Glow
            public float[] GapBelow;        // min L gap: base->dark, dark->deep, deep->line
            public float[] GapAbove;        // min L gap: base->light, light->glow
            public float ShadowDrift, LightDrift;   // degrees of hue toward cool / warm
        }

        static readonly Profile Metal = new()
        {
            // Full chroma in the mids: the SELF-DYE test (Orichalc's own copper swatch on its
            // own copper plate) came out duller than the authored copper at 0.8, and gold and
            // bronze read as khaki - warm metal is told from wood by saturation.
            MinL = 0.34f, MaxL = 0.84f, MaxGlowL = 0.97f, ChromaScale = 1.00f,
            Chroma = new[] { 0.60f, 0.85f, 1.00f, 1.00f, 0.75f, 0.35f },
            GapBelow = new[] { 0.06f, 0.07f, 0.10f }, GapAbove = new[] { 0.06f, 0.05f },
            ShadowDrift = 8f, LightDrift = 6f,
        };
        static readonly Profile Cloth = new()
        {
            MinL = 0.24f, MaxL = 0.90f, MaxGlowL = 0.96f, ChromaScale = 1.00f,
            Chroma = new[] { 0.75f, 0.95f, 1.00f, 1.00f, 0.88f, 0.70f },
            GapBelow = new[] { 0.05f, 0.06f, 0.09f }, GapAbove = new[] { 0.05f, 0.04f },
            ShadowDrift = 10f, LightDrift = 6f,
        };
        static readonly Profile Leather = new()
        {
            MinL = 0.28f, MaxL = 0.66f, MaxGlowL = 0.82f, ChromaScale = 0.65f,
            Chroma = new[] { 0.60f, 0.80f, 0.90f, 0.90f, 0.75f, 0.55f },
            GapBelow = new[] { 0.05f, 0.06f, 0.09f }, GapAbove = new[] { 0.05f, 0.04f },
            ShadowDrift = 8f, LightDrift = 4f,
        };
        static readonly Profile Gem = new()
        {
            MinL = 0.38f, MaxL = 0.88f, MaxGlowL = 0.98f, ChromaScale = 1.15f,
            Chroma = new[] { 0.80f, 1.00f, 1.05f, 1.00f, 0.75f, 0.30f },
            GapBelow = new[] { 0.06f, 0.07f, 0.10f }, GapAbove = new[] { 0.07f, 0.06f },
            ShadowDrift = 12f, LightDrift = 8f,
        };

        static Profile For(DyeMaterial m) => m switch
        {
            DyeMaterial.Metal => Metal,
            DyeMaterial.Leather => Leather,
            DyeMaterial.Gem => Gem,
            _ => Cloth,
        };

        // OKLab hue angles the drift leans toward: a blue-violet for shadow, a warm yellow for light.
        const float CoolHue = 285f, WarmHue = 95f;

        /// <summary>The channel's six tones (Line..Glow) dyed <paramref name="dye"/>.</summary>
        public static Color[] Dye(Color[] source, DyeMaterial material, Color dye)
        {
            var p = For(material);
            var d = ToLch(dye);

            float srcBase = ToLab(source[3]).x;
            float newBase = Mathf.Clamp(d.x, p.MinL, p.MaxL);

            var L = new float[6];
            for (int i = 0; i < 6; i++)
            {
                float li = ToLab(source[i]).x;
                L[i] = li >= srcBase
                    ? newBase + (li - srcBase) * (1f - newBase) / Mathf.Max(1f - srcBase, 0.08f)
                    : newBase * li / Mathf.Max(srcBase, 1e-3f);
            }
            L[3] = newBase;
            // Spacing floor, walking outward from the base. Below: dark, deep, line.
            for (int i = 2, g = 0; i >= 0; i--, g++)
                L[i] = Mathf.Max(0.02f, Mathf.Min(L[i], L[i + 1] - p.GapBelow[g]));
            // Above: light, glow - pushed up for spacing, capped so a glint never blows out.
            for (int i = 4, g = 0; i <= 5; i++, g++)
                L[i] = Mathf.Min(Mathf.Max(L[i], L[i - 1] + p.GapAbove[g]), p.MaxGlowL);

            var outTones = new Color[6];
            for (int i = 0; i < 6; i++)
            {
                float c = d.y * p.ChromaScale * p.Chroma[i];
                float h = d.z;
                if (i < 3) h = Toward(h, CoolHue, p.ShadowDrift * (3 - i) / 3f);
                else if (i > 3) h = Toward(h, WarmHue, p.LightDrift * (i - 3) / 2f);
                var col = FromLchClipped(L[i], c, h);
                col.a = source[i].a;
                outTones[i] = col;
            }
            return outTones;
        }

        static float Toward(float h, float target, float degrees)
        {
            float delta = Mathf.DeltaAngle(h, target);
            return h + Mathf.Clamp(delta, -degrees, degrees);
        }

        // ---------------------------------------------------------------- OKLab

        static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        static float ToSrgb(float c) => c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
        static float Cbrt(float x) => x < 0f ? -Mathf.Pow(-x, 1f / 3f) : Mathf.Pow(x, 1f / 3f);

        static Vector3 ToLab(Color c)
        {
            float r = ToLinear(c.r), g = ToLinear(c.g), b = ToLinear(c.b);
            float l = Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b);
            float m = Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b);
            float s = Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b);
            return new Vector3(0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s,
                               1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s,
                               0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s);
        }

        /// <summary>(L, C, h in degrees).</summary>
        static Vector3 ToLch(Color c)
        {
            var lab = ToLab(c);
            float h = Mathf.Atan2(lab.z, lab.y) * Mathf.Rad2Deg;
            if (h < 0f) h += 360f;
            return new Vector3(lab.x, Mathf.Sqrt(lab.y * lab.y + lab.z * lab.z), h);
        }

        static bool TryLinear(float L, float a, float b, out Vector3 rgb)
        {
            float l = L + 0.3963377774f * a + 0.2158037573f * b;
            float m = L - 0.1055613458f * a - 0.0638541728f * b;
            float s = L - 0.0894841775f * a - 1.2914855480f * b;
            l = l * l * l; m = m * m * m; s = s * s * s;
            rgb = new Vector3(4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s,
                             -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s,
                             -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s);
            const float eps = 1e-4f;
            return rgb.x >= -eps && rgb.y >= -eps && rgb.z >= -eps
                && rgb.x <= 1f + eps && rgb.y <= 1f + eps && rgb.z <= 1f + eps;
        }

        /// <summary>Back to sRGB, giving up CHROMA (never lightness or hue) until it fits - a dye
        /// too vivid for a tone's lightness comes out as the most saturated version that exists.</summary>
        static Color FromLchClipped(float L, float C, float hDeg)
        {
            float h = hDeg * Mathf.Deg2Rad, ca = Mathf.Cos(h), sa = Mathf.Sin(h);
            if (!TryLinear(L, C * ca, C * sa, out var rgb))
            {
                float lo = 0f, hi = C;
                for (int i = 0; i < 18; i++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (TryLinear(L, mid * ca, mid * sa, out _)) lo = mid; else hi = mid;
                }
                TryLinear(L, lo * ca, lo * sa, out rgb);
            }
            return new Color(ToSrgb(Mathf.Clamp01(rgb.x)), ToSrgb(Mathf.Clamp01(rgb.y)), ToSrgb(Mathf.Clamp01(rgb.z)), 1f);
        }
    }

    /// <summary>
    /// Dyeing an item: its art REBUILT from the same rows with the channel's palette groups
    /// repainted (DemoGear.Repaint), never a texel-colour match on the finished sprite - so a dye
    /// cannot catch a tone that merely resembles the channel, and the derived menu art, which is
    /// built from the same rows and palette, dyes identically.
    /// </summary>
    public static class GearDye
    {
        /// <summary>The ramp groups a palette can hold, Line..Glow in Palette.Of's order.</summary>
        static readonly string[] Groups = { "ksdblh", "KSDBLH", "123456" };

        /// <summary>How close a group's base must sit to channel base x shade, per RGB channel.</summary>
        const float MatchTolerance = 0.006f;

        /// <summary>Art for <paramref name="layers"/> with each channel in its swatch (null or
        /// empty = left as authored). Layers the dye doesn't reach come back as they were.</summary>
        public static LayerSprite[] Dyed(LayerSprite[] layers, DyeChannel[] channels, string[] swatchIds)
        {
            if (layers == null || channels == null || swatchIds == null) return layers;
            var tones = DyedTones(channels, swatchIds, out string tag);
            if (tag == null) return layers;

            var result = new LayerSprite[layers.Length];
            for (int i = 0; i < layers.Length; i++)
                result[i] = DemoGear.Repaint(layers[i], tag, pal => RepaintPalette(pal, channels, tones));
            return result;
        }

        /// <summary>
        /// A copy of <paramref name="design"/> wearing <paramref name="swatchIds"/> (one per
        /// channel, null = as authored), arena and menu art both, under the id
        /// <c>design@tag</c>. The design itself when nothing is dyed or it has no channels.
        /// </summary>
        public static GearItem DyedCopy(GearItem design, string[] swatchIds)
        {
            if (design == null || design.DyeChannels is not { Length: > 0 }) return design;
            DyedTones(design.DyeChannels, swatchIds, out string tag);
            if (tag == null) return design;

            var item = UnityEngine.Object.Instantiate(design);
            item.hideFlags = HideFlags.HideAndDontSave;
            item.ItemId = design.ItemId + "@" + tag.Substring(1);
            item.Layers = Dyed(design.Layers, design.DyeChannels, swatchIds);
            item.MenuLayers = Dyed(design.MenuLayers, design.DyeChannels, swatchIds);
            return item;
        }

        /// <summary>Per channel, the dyed tones (null = undyed), and the cache tag naming the
        /// combination - null when nothing is dyed at all.</summary>
        static Color[][] DyedTones(DyeChannel[] channels, string[] swatchIds, out string tag)
        {
            var tones = new Color[channels.Length][];
            tag = null;
            var sb = new System.Text.StringBuilder(".dye");
            bool any = false;
            for (int c = 0; c < channels.Length; c++)
            {
                var swatch = c < swatchIds.Length ? DyeCatalog.Get(swatchIds[c]) : null;
                sb.Append('.').Append(swatch != null ? swatch.Id.Substring(4) : "-");
                if (swatch == null || channels[c] == null) continue;
                tones[c] = DyeRecipe.Dye(channels[c].Tones, channels[c].Material, swatch.Color);
                any = true;
            }
            if (any) tag = sb.ToString();
            return tones;
        }

        /// <summary>A copy of <paramref name="pal"/> with every group belonging to a dyed channel
        /// repainted at its own shade, or null when no group in it does.</summary>
        static Dictionary<char, Color> RepaintPalette(IReadOnlyDictionary<char, Color> pal,
                                                      DyeChannel[] channels, Color[][] tones)
        {
            Dictionary<char, Color> result = null;
            foreach (var group in Groups)
            {
                if (!pal.TryGetValue(group[3], out var groupBase)) continue;
                if (!TryMatch(groupBase, channels, out int ch, out float shade) || tones[ch] == null) continue;

                result ??= new Dictionary<char, Color>(pal.Count);
                if (result.Count == 0) foreach (var kv in pal) result[kv.Key] = kv.Value;
                for (int t = 0; t < 6; t++)
                {
                    if (!pal.TryGetValue(group[t], out var was)) continue;
                    var c = tones[ch][t];
                    result[group[t]] = new Color(c.r * shade, c.g * shade, c.b * shade, was.a);
                }
            }
            return result;
        }

        /// <summary>
        /// How many of <paramref name="item"/>'s arena layers each channel reaches. A zero is a
        /// channel the player would pay to dye that visibly does nothing on this piece - a
        /// declaration to fix (or a piece to exclude) before it ships.
        /// </summary>
        public static int[] Reach(GearItem item)
        {
            var channels = item.DyeChannels ?? Array.Empty<DyeChannel>();
            var reach = new int[channels.Length];
            foreach (var layer in item.Layers ?? Array.Empty<LayerSprite>())
            {
                var pal = DemoGear.PaletteOf(layer?.Sprite);
                if (pal == null) continue;
                var hit = new bool[channels.Length];
                foreach (var group in Groups)
                    if (pal.TryGetValue(group[3], out var b) && TryMatch(b, channels, out int ch, out _))
                        hit[ch] = true;
                for (int c = 0; c < hit.Length; c++) if (hit[c]) reach[c]++;
            }
            return reach;
        }

        /// <summary>Every dyeable design whose channel reaches nothing - one line each, or "ok".</summary>
        public static string ReachReport()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var item in GearCatalog.All.Values)
            {
                if (item.DyeChannels is not { Length: > 0 } || item.ItemId.IndexOf('@') >= 0) continue;
                var reach = Reach(item);
                for (int c = 0; c < reach.Length; c++)
                    if (reach[c] == 0)
                        sb.Append(item.ItemId).Append(": ").Append(item.DyeChannels[c].Name)
                          .Append(" reaches nothing (").Append(item.Layers.Length).Append(" layers)\n");
            }
            return sb.Length == 0 ? "ok" : sb.ToString();
        }

        /// <summary>Which channel (and at which of its declared shades) a group's base is.</summary>
        public static bool TryMatch(Color groupBase, DyeChannel[] channels, out int channel, out float shade)
        {
            channel = -1; shade = 0f;
            float best = float.MaxValue;
            for (int c = 0; c < channels.Length; c++)
            {
                var ch = channels[c];
                if (ch == null) continue;
                foreach (var s in ch.Shades)
                {
                    float err = Mathf.Max(Mathf.Abs(groupBase.r - ch.Base.r * s),
                                Mathf.Max(Mathf.Abs(groupBase.g - ch.Base.g * s),
                                          Mathf.Abs(groupBase.b - ch.Base.b * s)));
                    if (err <= MatchTolerance && err < best) { best = err; channel = c; shade = s; }
                }
            }
            return channel >= 0;
        }
    }
}
