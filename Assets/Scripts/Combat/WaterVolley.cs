using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Combat
{
    /// <summary>
    /// SOAK with a bow - TIDE VOLLEY (the user's pick, 2026-11-05): a ring of arrows flies out from
    /// the player to the soak's radius, each trailing water, and bursts at the edge; a mist of wet
    /// floor settles behind them, and each body splashes as the ring of arrows reaches its
    /// distance. A picture only - nothing is fired; the soak lands at once.
    /// </summary>
    public class WaterVolley : ReleaseCanvas
    {
        [SerializeField] float _r;
        [SerializeField] Vector2[] _bodies;   // texels from the centre

        public static void Play(Vector2 centre, float radius, IEnumerable<Vector2> bodies)
        {
            int r = Mathf.CeilToInt(radius * Ppu) + 8;
            var w = Make<WaterVolley>("water.soak.volley", centre, r, r, r + SplashHeight, SortingOrders.GroundFx);
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
            float q = Mathf.Clamp01(p / T.VolleySeconds), reach = EaseOut(q) * _r;

            Ring(0, 0, 0f, reach, (x, y, d, a) => Over(x, y, T.Wet, T.WetAlpha * 0.85f * wet));

            for (int k = 0; k < T.VolleyArrows; k++)
            {
                float a = (k + 0.5f) / T.VolleyArrows * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (q < 1f)
                {
                    // The trail of water, then the shaft over it.
                    for (int t = T.VolleyTrailTexels; t >= 0; t--)
                    {
                        float d = reach - 5f - t;
                        if (d < 4f) continue;
                        float tail = t / (float)T.VolleyTrailTexels;
                        Dab(dir.x * d, dir.y * d, 1.4f - tail * 0.6f, At(T.Water, tail < 0.2f ? 0.05f : 0.3f), 1f - tail * 0.8f);
                    }
                    for (int s = 0; s < 6; s++)
                        Dab(dir.x * (reach - s), dir.y * (reach - s), s == 0 ? 1.2f : 0.7f, At(T.Air, s == 0 ? 0f : 0.35f));
                }
                else
                {
                    // The burst at the edge: a ring of spray widening and gone.
                    float after = p - T.VolleySeconds, life = 0.3f;
                    if (after < life)
                    {
                        var at = dir * _r;
                        float rr = 1f + after / life * 6f;
                        Ring(at.x, at.y, rr - 1f, rr, (x, y, d, an) => Over(x, y, At(T.Water, 0.15f), 1f - after / life));
                    }
                }
            }

            if (_bodies != null)
                for (int i = 0; i < _bodies.Length; i++)
                    WaterSplash(_bodies[i], p - EaseOutAt(_bodies[i].magnitude / Mathf.Max(1f, _r)) * T.VolleySeconds, i);
            return true;
        }
    }
}
