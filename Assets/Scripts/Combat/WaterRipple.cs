using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Combat
{
    /// <summary>
    /// SOAK's release (the user's pick, 2026-10-25 - "ripple"): one ring of water runs out from
    /// the player across the floor to the soak's radius - its edge is the edge of the soak -
    /// leaving the floor wet and rippling behind it, and each body caught splashes as the ring
    /// reaches it. The soak itself lands at once; the ring is the picture of it arriving.
    /// </summary>
    public class WaterRipple : ReleaseCanvas
    {
        const int SplashHeight = 12;

        [SerializeField] float _r;
        [SerializeField] Vector2[] _bodies;   // texels from the centre

        public static void Play(Vector2 centre, float radius, IEnumerable<Vector2> bodies)
        {
            int r = Mathf.CeilToInt(radius * Ppu) + 2;
            var w = Make<WaterRipple>("water.soak", centre, r, r, r + SplashHeight, SortingOrders.GroundFx);
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
            float front = Mathf.Min(1f, p / T.RippleSeconds) * _r;
            float edge = 1f - Mathf.Clamp01((p - T.RippleSeconds) / 0.25f);
            float time = Time.time;
            Ring(0, 0, 0f, front, (x, y, d, a) =>
            {
                Over(x, y, T.Wet, T.WetAlpha * wet);
                if (d > front - T.RippleFrontTexels && edge > 0f)
                    Over(x, y, At(T.Water, d > front - 1f ? 0.05f : 0.35f), edge);
                else if (d > 6f && Mathf.Repeat((front - d) * 0.5f - time * 3f, 3f) < 0.4f)
                    Over(x, y, At(T.Water, 0.5f), 0.3f * wet);
            });
            if (_bodies != null)
                for (int i = 0; i < _bodies.Length; i++)
                    Splash(_bodies[i], p - _bodies[i].magnitude / Mathf.Max(1f, _r) * T.RippleSeconds, i);
            return true;
        }

        /// <summary>Drops thrown up and out where the ring met a body, falling back.</summary>
        void Splash(Vector2 at, float since, int seed)
        {
            if (since < 0f || since > T.SplashSeconds) return;
            for (int k = 0; k < 10; k++)
            {
                float life = T.SplashSeconds * (0.75f + FireArt.Hash(k, seed, 1) * 0.25f);
                if (since > life) continue;
                float u = since / life, a = (FireArt.Hash(k, seed, 2) - 0.5f) * 2.4f;
                int x = Mathf.RoundToInt(at.x + Mathf.Sin(a) * u * 8f);
                int y = Mathf.RoundToInt(at.y + Mathf.Cos(a) * SplashHeight * 0.6f * (u * 2f - u * u * 1.6f));
                Over(x, y, At(T.Water, 0.1f + u * 0.5f));
            }
        }
    }
}
