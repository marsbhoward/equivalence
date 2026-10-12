using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Convergence.UI
{
    /// <summary>
    /// The four interlocking rings of the sigil door's selector, as a screen's choice: one ring at
    /// each compass point (0 up, 1 down, 2 left, 3 right), the floor reward's spacing (mockup C's
    /// ratio - neighbours 1.6 radii apart), drawn texel by texel at the seal's density. Shared by
    /// the floor reward (<see cref="FloorRewardScreen"/>) and the pause menu (<see cref="PauseScreen"/>)
    /// so the two stay one picture.
    ///
    /// The lit ring is the wall button's lit state - a stroke twice as thick, toward white. A soft
    /// glow band was tried and broke into speckle at this texel size.
    /// </summary>
    public static class SigilRings
    {
        public const float U = ElementSeal.UnitsPerTexel;
        public const int R = 47, D = 53, Tex = 2 * (D + R) + 8;

        /// <summary>The whole drawing's side, in canvas units.</summary>
        public const float Size = Tex * U;

        /// <summary>Where each ring's contents sit from the middle: pushed outward, clear of the
        /// overlaps, in canvas units.</summary>
        public static readonly Vector2[] CellAt = { new(0, 286), new(0, -286), new(-264, 0), new(264, 0) };

        /// <summary>The D-pad arrow's turn for each ring, so it points the way the pad goes.</summary>
        public static readonly float[] ArrowTurn = { 0f, 180f, 90f, -90f };

        public const float CellW = 225f, CellH = 177f;

        public static readonly Color32 Gold = new(0xe8, 0xba, 0x3d, 255), Lit = new(0xf3, 0xd9, 0x8f, 255);
        public static readonly Color32 Dim = new(0x9c, 0x7d, 0x2c, 255), Off = new(0x3b, 0x37, 0x46, 255);
        public static readonly Color32 Red = new(0xb8, 0x55, 0x4c, 255), RedLit = new(0xf0, 0xa8, 0xa2, 255);
        static readonly Color32 Outline = new(0x0b, 0x0a, 0x0e, 255);
        static readonly Color32 Hub = new(0x8f, 0x5d, 0x2e, 255), HubFace = new(0x14, 0x12, 0x19, 255);

        /// <summary>The drawing's RawImage and texture, filling <paramref name="rings"/>.</summary>
        public static Texture2D Build(RectTransform rings, string name)
        {
            var art = UiKit.Rect(rings, "art", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = art.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            var tex = new Texture2D(Tex, Tex, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = name,
            };
            img.texture = tex;
            return tex;
        }

        /// <summary>One ring's contents box, centred where <see cref="CellAt"/> puts it.</summary>
        public static RectTransform Cell(RectTransform rings, int i)
            => UiKit.Rect(rings, $"ring{i}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                CellAt[i] - new Vector2(CellW * 0.5f, CellH * 0.5f), CellAt[i] + new Vector2(CellW * 0.5f, CellH * 0.5f));

        /// <summary>The centre ring's index, for <see cref="Draw"/>'s <c>lit</c>, line and litLine.</summary>
        public const int Centre = 4;

        /// <summary>
        /// Redraws all four rings. <paramref name="lit"/> is the highlighted ring (-1 for none,
        /// <see cref="Centre"/> for the centre ring); <paramref name="line"/> gives each ring's
        /// colour, <paramref name="litLine"/> its lit one. <paramref name="centreR"/> &gt; 0 draws
        /// a small FILLED ring over the middle where the four meet (the pause menu's Resume);
        /// 0 leaves the bronze hub dot.
        /// </summary>
        public static void Draw(Texture2D tex, ref Color32[] px, int lit, Func<int, Color32> line,
                                Func<int, Color32> litLine, float centreR = 0f)
        {
            if (tex == null) return;
            if (px == null || px.Length != Tex * Tex) px = new Color32[Tex * Tex];

            float c = Tex * 0.5f;
            // Texture y runs up: ring 0 (up) sits above the centre.
            var centres = new[] { new Vector2(c, c + D), new Vector2(c, c - D), new Vector2(c - D, c), new Vector2(c + D, c) };
            var colours = new Color32[4];
            for (int i = 0; i < 4; i++) colours[i] = line(i);
            var litColour = lit >= 0 ? litLine(lit) : default;
            var centreLine = centreR > 0f ? line(Centre) : default;

            for (int y = 0; y < Tex; y++)
            for (int x = 0; x < Tex; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                Color32 col = new(0, 0, 0, 0);
                // Outlines first, every ring; then each ring's line over them, the lit one last.
                for (int i = 0; i < 4; i++)
                    if (Mathf.Abs(Vector2.Distance(p, centres[i]) - R) < (i == lit ? 2.1f : 1.6f)) col = Outline;
                for (int i = 0; i < 4; i++)
                    if (i != lit && Mathf.Abs(Vector2.Distance(p, centres[i]) - R) < 0.6f) col = colours[i];
                if (lit >= 0 && lit < 4 && Mathf.Abs(Vector2.Distance(p, centres[lit]) - R) < 1.1f) col = litColour;

                float hub = Vector2.Distance(p, new Vector2(c, c));
                if (centreR > 0f)
                {
                    // Filled, so the four rings' lines stop at its edge - it sits ON the knot.
                    bool on = lit == Centre;
                    float e = hub - centreR;
                    if (e < -(on ? 1.1f : 0.6f)) col = HubFace;
                    else if (Mathf.Abs(e) < (on ? 1.1f : 0.6f)) col = on ? litColour : centreLine;
                    else if (e < (on ? 2.1f : 1.6f)) col = Outline;
                }
                else if (hub < 2.6f) col = hub < 1.4f ? HubFace : Hub;
                else if (hub < 3.4f && col.a == 0) col = Outline;
                px[y * Tex + x] = col;
            }
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        // ---- pixel sprites

        static Sprite _pad;
        public static readonly Dictionary<char, Color32> White = new() { ['X'] = new Color32(255, 255, 255, 255) };

        public static Sprite PadArrow => _pad != null ? _pad : _pad = Grid(new[] { "..X..", ".XXX.", "XXXXX" }, White);

        /// <summary>A point-filtered sprite from rows of characters, the first row at the top.
        /// <paramref name="border"/> makes it 9-sliceable one texel in from each edge.</summary>
        public static Sprite Grid(string[] rows, Dictionary<char, Color32> palette, bool border = false)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[(h - 1 - y) * w + x] = palette.TryGetValue(rows[y][x], out var c) ? c : new Color32(0, 0, 0, 0);
            tex.SetPixels32(px);
            tex.Apply(false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border ? new Vector4(1, 1, 1, 1) : Vector4.zero);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
