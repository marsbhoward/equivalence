using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// The sigil selector's emblem (Hub.WallButton) - four rings at the compass points, each
    /// overlapping its neighbours and clear of its opposite - as a texel mask, with no element
    /// mark inside (the user's call, 2026-10-25). Laid on the floor: flattened by a squash, every
    /// ring traced one line at a time so it stays a connected texel outline at any size.
    ///
    /// Element-blind on purpose, so any element can burn it into the ground in its own colour -
    /// Fire's full heat is the first (Player.HeatGround).
    /// </summary>
    public static class SigilEmblem
    {
        /// <summary>Ring radius over the centre-to-ring spread: WallButton's own ratio.</summary>
        public const float RingRatio = 0.94f;

        /// <summary>
        /// The emblem <paramref name="halfWidth"/> texels either side of its centre, flattened to
        /// <paramref name="squash"/> of that height, each ring <paramref name="weight"/> texels
        /// thick. Returns the mask, row by row from the bottom; its size in <paramref name="w"/>
        /// and <paramref name="h"/> (both odd, the centre on a texel).
        /// </summary>
        public static bool[] Ink(int halfWidth, float squash, int weight, out int w, out int h)
        {
            float spread = halfWidth / (1f + RingRatio);
            float r = spread * RingRatio;
            int halfH = Mathf.CeilToInt(halfWidth * squash) + 1;
            w = halfWidth * 2 + 1;
            h = halfH * 2 + 1;
            var ink = new bool[w * h];

            var centres = new[] { new Vector2(0f, spread), new Vector2(spread, 0f), new Vector2(0f, -spread), new Vector2(-spread, 0f) };
            int steps = Mathf.CeilToInt(2f * Mathf.PI * r * 3f);
            foreach (var c in centres)
                for (int k = 0; k < weight; k++)
                {
                    float rr = r - k * 0.9f;
                    for (int i = 0; i < steps; i++)
                    {
                        float a = i * 2f * Mathf.PI / steps;
                        int x = halfWidth + Mathf.RoundToInt(c.x + rr * Mathf.Cos(a));
                        int y = halfH + Mathf.RoundToInt((c.y + rr * Mathf.Sin(a)) * squash);
                        if (x >= 0 && y >= 0 && x < w && y < h) ink[y * w + x] = true;
                    }
                }
            return ink;
        }
    }
}
