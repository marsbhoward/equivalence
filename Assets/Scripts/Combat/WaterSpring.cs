using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Combat
{
    /// <summary>
    /// SOAK with a greatsword - WELLSPRING (the user's pick, 2026-11-05): the blade is driven in and
    /// channels of water crack out from it across the floor to the soak's radius, spouting where
    /// they end, a small fountain at the hilt; each body splashes as the channels reach its
    /// distance. The soak itself lands at once; this is the picture of it arriving.
    /// </summary>
    public class WaterSpring : ReleaseCanvas
    {
        [SerializeField] float _r;
        [SerializeField] Vector2[] _bodies;   // texels from the centre

        public static void Play(Vector2 centre, float radius, IEnumerable<Vector2> bodies)
        {
            int r = Mathf.CeilToInt(radius * Ppu) + 2;
            var w = Make<WaterSpring>("water.soak.spring", centre, r, r, r + Mathf.Max(SplashHeight, T.SpringSpoutTexels) + 2,
                                      SortingOrders.GroundFx);
            w._r = radius * Ppu;
            var list = new List<Vector2>();
            foreach (var b in bodies) list.Add((b - centre) * Ppu);
            w._bodies = list.ToArray();
        }

        protected override bool Draw()
        {
            float p = Age;
            float wet = 1f - Mathf.Clamp01((p - T.RippleDryFrom) / T.RippleDrySeconds);
            if (wet <= 0f) return false;
            float grow = Mathf.Clamp01(p / T.SpringSeconds), len = grow * _r;

            for (int c = 0; c < T.SpringChannels; c++)
            {
                float a = (c + 0.25f) / T.SpringChannels * Mathf.PI * 2f;
                Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a)), side = new(-dir.y, dir.x);
                for (float k = 3f; k <= len; k += 0.7f)
                {
                    // A crack wanders: a slow wobble either side of the line.
                    float wob = Mathf.Sin(k * 0.21f + c * 1.7f) * 1.4f;
                    var at = dir * k + side * wob;
                    int x = Mathf.RoundToInt(at.x), y = Mathf.RoundToInt(at.y);
                    bool front = grow < 1f && k > len - 3f;
                    Over(x, y, At(T.Water, front ? 0.05f : 0.45f), wet);
                    Over(x + Mathf.RoundToInt(side.x), y + Mathf.RoundToInt(side.y), T.Wet, T.WetAlpha * 1.4f * wet);
                    Over(x - Mathf.RoundToInt(side.x), y - Mathf.RoundToInt(side.y), At(T.Water, 0.7f), 0.6f * wet);
                }

                // The spout where a channel ends, rising and falling back.
                float spout = p - T.SpringSeconds;
                if (spout > 0f && spout < T.SpringSpoutSeconds)
                {
                    var end = dir * _r;
                    int h = Mathf.RoundToInt(Mathf.Sin(spout / T.SpringSpoutSeconds * Mathf.PI) * T.SpringSpoutTexels);
                    int ex = Mathf.RoundToInt(end.x), ey = Mathf.RoundToInt(end.y);
                    for (int y = 0; y < h; y++) Over(ex, ey + y, At(T.Water, y > h - 3 ? 0.05f : 0.3f));
                    Over(ex - 1, ey + h - 1, At(T.Water, 0.4f)); Over(ex + 1, ey + h - 1, At(T.Water, 0.4f));
                }
            }

            // The fountain at the hilt.
            if (p < T.SpringFountainSeconds)
            {
                float f = Mathf.Sin(Mathf.Clamp01(p / T.SpringFountainSeconds) * Mathf.PI);
                int h = Mathf.RoundToInt(f * T.SpringFountainTexels);
                for (int y = 0; y < h; y++) Over(2, y, At(T.Water, y > h - 2 ? 0f : 0.25f));
                Over(1, h, At(T.Water, 0.1f), f); Over(3, h, At(T.Water, 0.1f), f);
            }

            if (_bodies != null)
                for (int i = 0; i < _bodies.Length; i++)
                    WaterSplash(_bodies[i], p - _bodies[i].magnitude / Mathf.Max(1f, _r) * T.SpringSeconds, i);
            return true;
        }
    }
}
