using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Hub;

namespace Convergence.EditorTools
{
    /// <summary>
    /// A minted piece's image: its MENU art, composed, at a whole-number 10x on a square 3200
    /// canvas, plus the native 1x picture (CLAUDE.md, "NFT item images"). The two PNGs are what
    /// the service's <c>POST /write/art</c> pins under the piece's ART KEY.
    ///
    /// COMPOSED, NOT A CAMERA SHOT. Every layer is copied texel by texel onto one grid at the
    /// densest layer's ppu, so the image is exactly the authored pixels - no filtering, no
    /// sub-texel placement, nothing a render setting could change. A layer that would land off
    /// that grid is refused rather than resampled.
    ///
    ///   armour, jewellery, relics   every layer AS WORN, body hidden: each hangs from its joint
    ///                               on the armour stand's own pivot tree (ArmourStand.PivotOf -
    ///                               the rig's, never a third copy), drawn in the stand's order.
    ///   weapons                     the display picture (GearDisplay.Represent), blade UP as
    ///                               authored; a disc PAIR overlaps its second half as the rack
    ///                               hangs it (same offset and shade).
    ///
    /// The UNLIT picture throughout: Secret Fire marks are their reserved near-black texels, a
    /// gemmed weapon keeps its authored gem (not the attuned element), and nothing is dyed by a
    /// cape - an item image has no wearer.
    ///
    ///     unity command eval --code 'return Convergence.EditorTools.NftArt.Render("aether_greatsword", "/tmp/nft");'
    ///
    /// The art key is the DRAWN item's id: a design, or a dyed copy's design@dye.main.accent
    /// (GearDye.DyedCopy) - and the render checks the copy it builds has exactly that id.
    /// </summary>
    public static class NftArt
    {
        public const int Canvas = 3200;
        public const int Multiplier = 10;
        /// <summary>The widest/tallest native picture that still fits the canvas at 10x.</summary>
        public const int NativeMax = Canvas / Multiplier;

        /// <summary>WeaponRack.OffHand and its 0.72 shade - a disc pair hangs as it does there.</summary>
        static readonly Vector2 PairOffset = new(0.15f, -0.11f);
        const float PairShade = 0.72f;

        struct Piece
        {
            public Sprite Sprite;
            public Vector2 At;      // where the sprite's PIVOT lands, world units
            public int Order;
            public Color Tint;
        }

        /// <summary>Writes <c>key.1x.png</c> and <c>key.png</c> into <paramref name="outDir"/>.</summary>
        public static string Render(string artKey, string outDir)
        {
            var item = Resolve(artKey, out string why);
            if (item == null) return $"{artKey}: {why}";

            var root = new GameObject("nft-art (temporary)");
            try
            {
                var pieces = Collect(item, root.transform, out why);
                if (pieces == null) return $"{artKey}: {why}";

                var native = Compose(pieces, out int w, out int h, out float ppu, out why);
                if (native == null) return $"{artKey}: {why}";
                if (w > NativeMax || h > NativeMax)
                    return $"{artKey}: native {w}x{h} is past {NativeMax} - grow the canvas for everything, never shrink one item";

                Directory.CreateDirectory(outDir);
                string safe = artKey.Replace('@', '_');
                string small = Path.Combine(outDir, safe + ".1x.png");
                string large = Path.Combine(outDir, safe + ".png");
                File.WriteAllBytes(small, Png(native, w, h));
                File.WriteAllBytes(large, Png(Enlarge(native, w, h), Canvas, Canvas));
                return $"{artKey}: {pieces.Count} layer(s), native {w}x{h} at {ppu} ppu -> {large}";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>Several keys at once, comma-separated - one line of result each.</summary>
        public static string RenderMany(string artKeys, string outDir)
        {
            var lines = new List<string>();
            foreach (var key in artKeys.Split(','))
                if (!string.IsNullOrWhiteSpace(key)) lines.Add(Render(key.Trim(), outDir));
            return string.Join("\n", lines);
        }

        // ---------------------------------------------------------------- what is drawn

        static GearItem Resolve(string artKey, out string why)
        {
            why = null;
            int at = artKey.IndexOf('@');
            string designId = at < 0 ? artKey : artKey.Substring(0, at);
            var design = GearCatalog.Get(designId);
            if (design == null) { why = $"no design '{designId}'"; return null; }
            if (at < 0) return design;

            // design@dye.<main>.<accent> - the tag GearDye builds from permanent swatch ids.
            string tag = artKey.Substring(at + 1);
            if (!tag.StartsWith("dye.")) { why = "only dye tags follow '@'"; return null; }
            var parts = tag.Substring(4).Split('.');
            var swatches = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                swatches[i] = parts[i] == "-" ? null : "dye." + parts[i];

            var dyed = GearDye.DyedCopy(design, swatches);
            if (dyed == null || dyed.ItemId != artKey)
            {
                why = $"the dyed copy is '{dyed?.ItemId}', not the key - unknown swatch, or a channel the design lacks";
                return null;
            }
            return dyed;
        }

        static List<Piece> Collect(GearItem item, Transform root, out string why)
        {
            why = null;
            var pieces = new List<Piece>();

            // A piece with a DISPLAY picture that is not a weapon (the Wraith's Eye: worn, it has
            // no art - it recolours the wearer's own eye) is shown by that picture alone.
            var shown = item.Slot != GearSlot.Weapon ? item.DisplayFor(menu: true) : null;
            if (shown?.Sprite != null)
            {
                if (!AtOwnSize(shown, out why)) return null;
                pieces.Add(new Piece { Sprite = shown.Sprite, At = Vector2.zero, Order = 0, Tint = shown.Tint });
                return pieces;
            }

            if (item.Slot == GearSlot.Weapon)
            {
                var main = GearDisplay.Represent(item, menu: true, attuned: false);
                if (main?.Sprite == null) { why = "no weapon picture"; return null; }
                if (!AtOwnSize(main, out why)) return null;
                pieces.Add(new Piece { Sprite = main.Sprite, At = Vector2.zero, Order = 1, Tint = main.Tint });

                if (item.Class == WeaponClass.Disc)
                {
                    var off = GearDisplay.RepresentOffhand(item, menu: true) ?? main;
                    if (!AtOwnSize(off, out why)) return null;
                    var t = off.Tint;
                    pieces.Add(new Piece
                    {
                        Sprite = off.Sprite, At = PairOffset, Order = 0,
                        Tint = new Color(t.r * PairShade, t.g * PairShade, t.b * PairShade, t.a),
                    });
                }
                return pieces;
            }

            // Everything else hangs on the stand's joints. A temporary stand, so the positions
            // are the rig's own pivot tree and not a copy of it that could drift.
            var stand = ArmourStand.Build(root, Vector2.zero);
            var figure = stand.Figure;
            foreach (var l in item.LayersFor(menu: true))
            {
                if (l == null || l.Sprite == null || RigLayers.IsBody(l.Layer)) continue;
                if (!AtOwnSize(l, out why)) return null;
                var pivot = stand.PivotOf(l.Layer);
                // Figure-local, so the stand's ArenaVisualScale on the figure is divided back out.
                Vector2 joint = figure.InverseTransformPoint(pivot.position);
                pieces.Add(new Piece
                {
                    Sprite = l.Sprite,
                    At = joint + (Vector2)l.Offset,
                    Order = ArmourStand.RankOf(l.Layer),
                    Tint = l.Tint,
                });
            }
            if (pieces.Count == 0) why = "no menu layers to draw";
            return pieces.Count == 0 ? null : pieces;
        }

        /// <summary>Gear art is PPU-matched - drawn at scale 1. Anything else would need resampling.</summary>
        static bool AtOwnSize(LayerSprite l, out string why)
        {
            why = null;
            var s = GearDisplay.ScaleFor(l);
            if (Mathf.Abs(s.x - 1f) < 0.01f && Mathf.Abs(s.y - 1f) < 0.01f) return true;
            why = $"layer {l.Layer} is drawn at scale {s.x:0.###}x{s.y:0.###}, not its own size";
            return false;
        }

        // ---------------------------------------------------------------- the texels

        static Color32[] Compose(List<Piece> pieces, out int w, out int h, out float ppu, out string why)
        {
            w = h = 0;
            why = null;
            ppu = 0f;
            foreach (var p in pieces) ppu = Mathf.Max(ppu, p.Sprite.pixelsPerUnit);

            // Each piece's lower-left corner on the shared grid, and its block size there.
            var origins = new Vector2Int[pieces.Count];
            var blocks = new int[pieces.Count];
            var corners = new Vector2[pieces.Count];
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                float f = ppu / p.Sprite.pixelsPerUnit;
                if (Mathf.Abs(f - Mathf.Round(f)) > 0.001f)
                {
                    why = $"{p.Sprite.name}: {p.Sprite.pixelsPerUnit} ppu does not divide {ppu}";
                    return null;
                }
                blocks[i] = Mathf.RoundToInt(f);
                corners[i] = p.At * ppu - p.Sprite.pivot * blocks[i];
            }

            // Only where layers sit RELATIVE to each other has to be whole texels: a weapon is
            // pivoted at its grip centre, often a half-texel, and a lone picture has no grid to be
            // off. So the grid is anchored on the first layer's own corner.
            var anchor = new Vector2(corners[0].x - Mathf.Floor(corners[0].x), corners[0].y - Mathf.Floor(corners[0].y));

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                Vector2 corner = corners[i] - anchor;
                var snapped = new Vector2Int(Mathf.RoundToInt(corner.x), Mathf.RoundToInt(corner.y));
                if ((corner - snapped).sqrMagnitude > 0.01f * 0.01f * 2f)
                {
                    why = $"{p.Sprite.name} lands at {corner.x:0.###},{corner.y:0.###} - off the {ppu} ppu grid";
                    return null;
                }
                origins[i] = snapped;
                var r = p.Sprite.rect;
                minX = Math.Min(minX, snapped.x);
                minY = Math.Min(minY, snapped.y);
                maxX = Math.Max(maxX, snapped.x + (int)r.width * blocks[i]);
                maxY = Math.Max(maxY, snapped.y + (int)r.height * blocks[i]);
            }

            int cw = maxX - minX, ch = maxY - minY;
            var canvas = new Color[cw * ch];

            var order = new List<int>();
            for (int i = 0; i < pieces.Count; i++) order.Add(i);
            order.Sort((a, b) => pieces[a].Order != pieces[b].Order
                ? pieces[a].Order.CompareTo(pieces[b].Order) : a.CompareTo(b));

            foreach (int i in order)
            {
                var p = pieces[i];
                var r = p.Sprite.rect;
                int sw = (int)r.width, sh = (int)r.height, b = blocks[i];
                var src = SpritePixels(p.Sprite);
                for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    var c = src[y * sw + x] * p.Tint;
                    if (c.a <= 0f) continue;
                    for (int by = 0; by < b; by++)
                    for (int bx = 0; bx < b; bx++)
                    {
                        int cx = origins[i].x - minX + x * b + bx, cy = origins[i].y - minY + y * b + by;
                        int k = cy * cw + cx;
                        canvas[k] = Over(c, canvas[k]);
                    }
                }
            }

            // Trimmed to what is drawn - padding is the canvas's job, not the native picture's.
            int x0 = cw, y0 = ch, x1 = -1, y1 = -1;
            for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
            {
                if (canvas[y * cw + x].a <= 0f) continue;
                x0 = Math.Min(x0, x); y0 = Math.Min(y0, y);
                x1 = Math.Max(x1, x); y1 = Math.Max(y1, y);
            }
            if (x1 < 0) { why = "every layer is empty"; return null; }

            w = x1 - x0 + 1;
            h = y1 - y0 + 1;
            var trimmed = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                trimmed[y * w + x] = canvas[(y + y0) * cw + (x + x0)];
            return trimmed;
        }

        static Color Over(Color top, Color under)
        {
            float a = top.a + under.a * (1f - top.a);
            if (a <= 0f) return Color.clear;
            var rgb = ((Vector4)top * top.a + (Vector4)under * under.a * (1f - top.a)) / a;
            return new Color(rgb.x, rgb.y, rgb.z, a);
        }

        /// <summary>The sprite's own rect, row-major from the bottom. Copied through a render
        /// texture when the texture was made non-readable.</summary>
        static Color[] SpritePixels(Sprite s)
        {
            var r = s.rect;
            var tex = s.texture;
            if (tex.isReadable)
                return tex.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);

            var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32,
                                                RenderTextureReadWrite.sRGB);
            var was = RenderTexture.active;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(r.x, r.y, r.width, r.height), 0, 0);
                var px = copy.GetPixels();
                UnityEngine.Object.DestroyImmediate(copy);
                return px;
            }
            finally
            {
                RenderTexture.active = was;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>Nearest-neighbour x10, centred on the square canvas, transparent around it.</summary>
        static Color32[] Enlarge(Color32[] native, int w, int h)
        {
            var big = new Color32[Canvas * Canvas];
            int ox = (Canvas - w * Multiplier) / 2, oy = (Canvas - h * Multiplier) / 2;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = native[y * w + x];
                if (c.a == 0) continue;
                for (int by = 0; by < Multiplier; by++)
                {
                    int row = (oy + y * Multiplier + by) * Canvas + ox + x * Multiplier;
                    for (int bx = 0; bx < Multiplier; bx++) big[row + bx] = c;
                }
            }
            return big;
        }

        static byte[] Png(Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                tex.SetPixels32(px);
                tex.Apply(false);
                return tex.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
