using UnityEngine;
using Convergence.Core;

namespace Convergence.Progression
{
    /// <summary>
    /// The sphere grid, drawn small enough to sit on a sheet of paper on a table.
    ///
    /// Rendered FROM <see cref="MasteryBoard"/> rather than hand-drawn, so the paper cannot drift
    /// out of date. The board is built in code precisely because it is going to need a lot of
    /// tuning, and a hand-authored picture of it would be wrong the first time anyone moved a
    /// node - which is the worst possible outcome for a prop whose entire job is to say "this is
    /// the thing you are about to open".
    ///
    /// Deliberately illegible at world scale. It is a map on a table, not a UI: it should read as
    /// four limbs around a hub, and the moment a player wants to actually read it they press [E]
    /// and get the real screen.
    /// </summary>
    public static class GridSketch
    {
        static Sprite _sheet;

        /// <summary>Half the world the grid occupies, in grid units. Roots sit at 14.6.</summary>
        const float Extent = 16.5f;

        public static Sprite Sheet => _sheet != null ? _sheet : _sheet = Build(256);

        static Sprite Build(int size)
        {
            var px = new Color[size * size];
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            // Ink on nothing: the paper itself is a separate sprite underneath, so this only ever
            // draws marks. Tinting a baked sheet would tint the paper along with the ink.
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0f, 0f, 0f, 0f);

            var ink = new Color(0.24f, 0.19f, 0.13f);

            foreach (var node in MasteryBoard.All)
            foreach (var id in node.Neighbours)
            {
                var other = MasteryBoard.Get(id);
                // Each edge is stored on both ends; draw it once.
                if (other == null || string.CompareOrdinal(node.Id, other.Id) > 0) continue;
                Line(px, size, ToPixel(node.Position, size), ToPixel(other.Position, size),
                     ink, 0.85f, 1.15f);
            }

            foreach (var node in MasteryBoard.All)
            {
                // Tinted by PRINCIPLE, not element: the paper shows the one board design that
                // all four elements share, so an element tint would be meaningless here.
                var tint = node.Principle switch
                {
                    Principle.Sulfur  => new Color(0.95f, 0.55f, 0.28f),
                    Principle.Mercury => new Color(0.55f, 0.80f, 0.95f),
                    _                 => new Color(0.72f, 0.78f, 0.62f),
                };

                // Muted toward the ink so four saturated limbs do not read as a colour wheel;
                // the point of the sketch is the SHAPE.
                tint = Color.Lerp(ink, tint, 0.62f);

                float r = node.Keystone ? 4.2f : 2.9f;
                Dot(px, size, ToPixel(node.Position, size), r, tint);
                if (node.Keystone) Ring(px, size, ToPixel(node.Position, size), r + 2.2f, 0.9f, ink);
            }

            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Vector2 ToPixel(Vector2 gridPos, int size)
        {
            float half = size * 0.5f;
            return new Vector2(half + gridPos.x / Extent * half,
                               half + gridPos.y / Extent * half);
        }

        // ---------------------------------------------------------------- raster

        static void Line(Color[] px, int size, Vector2 a, Vector2 b, Color color,
                         float halfWidth, float feather)
        {
            // Only the segment's bounding box is visited - the whole-image scan this replaced was
            // a hundred edges times sixty thousand pixels for a picture the size of a postcard.
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - feather - 1));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + feather + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - feather - 1));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + feather + 1));

            var ab = b - a;
            float lenSq = Mathf.Max(0.0001f, ab.sqrMagnitude);

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
                float d = Vector2.Distance(p, a + ab * t);
                float alpha = 1f - Mathf.InverseLerp(halfWidth, feather, d);
                if (alpha > 0f) Blend(px, size, x, y, color, alpha);
            }
        }

        static void Dot(Color[] px, int size, Vector2 c, float r, Color color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r - 1));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.x + r + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r - 1));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.y + r + 1));

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c);
                float alpha = 1f - Mathf.InverseLerp(r - 0.8f, r + 0.4f, d);
                if (alpha > 0f) Blend(px, size, x, y, color, alpha);
            }
        }

        static void Ring(Color[] px, int size, Vector2 c, float r, float halfWidth, Color color)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(c.x - r - 2));
            int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.x + r + 2));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(c.y - r - 2));
            int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(c.y + r + 2));

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float d = Mathf.Abs(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) - r);
                float alpha = 1f - Mathf.InverseLerp(halfWidth, halfWidth + 0.9f, d);
                if (alpha > 0f) Blend(px, size, x, y, color, alpha);
            }
        }

        static void Blend(Color[] px, int size, int x, int y, Color color, float alpha)
        {
            int i = y * size + x;
            var dst = px[i];
            float a = Mathf.Clamp01(alpha);
            float outA = a + dst.a * (1f - a);
            if (outA <= 0.0001f) { px[i] = new Color(0f, 0f, 0f, 0f); return; }
            px[i] = new Color(
                (color.r * a + dst.r * dst.a * (1f - a)) / outA,
                (color.g * a + dst.g * dst.a * (1f - a)) / outA,
                (color.b * a + dst.b * dst.a * (1f - a)) / outA,
                outA);
        }
    }
}
