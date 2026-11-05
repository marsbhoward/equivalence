using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Combat
{
    /// <summary>
    /// SOAK with discs - WHIRLPOOL (the user's pick, 2026-11-05): two arms of water spiral out from
    /// the player to the soak's radius, a disc's glint at each head, the floor wet behind them;
    /// each body splashes as the spiral's reach passes its distance. A picture only - the discs
    /// themselves stay in the hands; the soak lands at once.
    /// </summary>
    public class WaterWhirl : ReleaseCanvas
    {
        /// <summary>How much of the spiral's run the wake trails behind each head.</summary>
        const float TrailShare = 0.35f;

        [SerializeField] float _r;
        [SerializeField] Vector2[] _bodies;   // texels from the centre

        public static void Play(Vector2 centre, float radius, IEnumerable<Vector2> bodies)
        {
            int r = Mathf.CeilToInt(radius * Ppu) + 3;
            var w = Make<WaterWhirl>("water.soak.whirl", centre, r, r, r + SplashHeight, SortingOrders.GroundFx);
            w._r = radius * Ppu;
            var list = new List<Vector2>();
            foreach (var b in bodies) list.Add((b - centre) * Ppu);
            w._bodies = list.ToArray();
        }

        Vector2 Arm(int arm, float u)
        {
            float rr = EaseOut(u) * _r, a = arm * Mathf.PI + u * Mathf.PI * 2f * T.WhirlTurns;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
        }

        protected override bool Draw()
        {
            float p = Age;
            float wet = 1f - Mathf.Clamp01((p - T.RippleDryFrom) / T.RippleDrySeconds);
            if (wet <= 0f) return false;
            float u = Mathf.Clamp01(p / T.WhirlSeconds);
            float wake = 1f - Mathf.Clamp01((p - T.WhirlSeconds) / T.WhirlFadeSeconds);

            // The floor wet out to the spiral's reach.
            Ring(0, 0, 0f, EaseOut(u) * _r, (x, y, d, a) => Over(x, y, T.Wet, T.WetAlpha * wet));

            if (wake > 0f)
                for (int arm = 0; arm < 2; arm++)
                {
                    // The wake, sampled finely enough to stay a line at full reach: deep under, bright on top.
                    for (float back = 0f; back < TrailShare; back += 0.003f)
                    {
                        float q = u - back;
                        if (q <= 0f) break;
                        var at = Arm(arm, q);
                        float k = back / TrailShare, fade = (1f - k) * wake;
                        Dab(at.x, at.y - 1f, 1.6f, At(T.Water, 0.6f), fade * 0.7f);
                        Dab(at.x, at.y, 1f, At(T.Water, k < 0.08f ? 0f : k < 0.4f ? 0.2f : 0.4f), fade);
                    }
                    // The disc's glint at the head, while it is still travelling.
                    if (u < 1f)
                    {
                        var h = Arm(arm, u);
                        Dab(h.x, h.y, 2.2f, At(T.Air, 0.45f));
                        Dab(h.x, h.y, 1f, At(T.Water, 0f));
                    }
                }

            if (_bodies != null)
                for (int i = 0; i < _bodies.Length; i++)
                    WaterSplash(_bodies[i], p - EaseOutAt(_bodies[i].magnitude / Mathf.Max(1f, _r)) * T.WhirlSeconds, i);
            return true;
        }
    }
}
