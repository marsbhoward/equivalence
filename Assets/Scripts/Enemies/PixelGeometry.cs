using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>
    /// Small per-texel shape tests shared by the enemy *Art builders that pose a limb rather than
    /// just recolouring or resizing a fixed silhouette (ChaserArt's swinging arm, RangedArt's
    /// aiming probe, and whatever comes after them). Started as three private methods duplicated
    /// inside ChaserArt; Ranged wanting the identical capsule-along-a-pivot technique is the point
    /// this project's own stated discipline turns a copy into a shared piece - see EnemyLooks and
    /// EnemyStageCycle for the same call made one layer up, at the SAME moment (a second real
    /// consumer), for the same reason.
    /// </summary>
    public static class PixelGeometry
    {
        public static Vector2 PointAt(Vector2 from, float angleDeg, float dist)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            return from + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * dist;
        }

        /// <summary>A fixed-width capsule along segment a-b.</summary>
        public static bool InCapsule(float x, float y, Vector2 a, Vector2 b, float halfWidth)
        {
            var d = b - a;
            float lenSq = d.sqrMagnitude;
            float t = lenSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(new Vector2(x, y) - a, d) / lenSq) : 0f;
            var p = a + d * t;
            float dx = x - p.x, dy = y - p.y;
            return dx * dx + dy * dy <= halfWidth * halfWidth;
        }

        /// <summary>A capsule whose half-width TAPERS from a to b - a blade, a needle, a spike.</summary>
        public static bool InWedge(float x, float y, Vector2 a, Vector2 b, float halfA, float halfB)
        {
            var d = b - a;
            float lenSq = d.sqrMagnitude;
            if (lenSq < 0.0001f) return false;
            float t = Vector2.Dot(new Vector2(x, y) - a, d) / lenSq;
            if (t < 0f || t > 1f) return false;
            var p = a + d * t;
            float dx = x - p.x, dy = y - p.y;
            float hw = Mathf.Lerp(halfA, halfB, t);
            return dx * dx + dy * dy <= hw * hw;
        }
    }
}
