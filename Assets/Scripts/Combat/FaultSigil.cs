using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Combat
{
    /// <summary>
    /// The QUAKE's release (the user's pick, 2026-10-25 - "fault sigil"): the sigil emblem
    /// cracks into the floor under the player with Earth's green light in its grooves, and cracks
    /// shoot from it out to the quake's radius. Each aftershock sends the light down the cracks
    /// again and throws up dust along them. The stone stays stone; the light is the element's
    /// colour. FightMarks still leaves its crack on the floor after.
    /// </summary>
    public class FaultSigil : ReleaseCanvas
    {
        [SerializeField] float _r;
        [SerializeField] int _half;
        [SerializeField] List<float> _shocks = new();
        [SerializeField] Vector2 _centre;
        static readonly List<Vector2> Path = new();

        public Vector2 Centre => _centre;

        public static FaultSigil Play(Vector2 centre, float radius)
        {
            int r = Mathf.CeilToInt(radius * Ppu * 1.05f) + 2;
            var f = Make<FaultSigil>("earth.quake", centre, r, r, r + 10, SortingOrders.GroundFx);
            f._r = radius * Ppu;
            f._half = Mathf.Max(6, Mathf.RoundToInt(f._r * T.QuakeSigilShare));
            f._centre = centre;
            f._shocks.Add(0f);
            return f;
        }

        /// <summary>An aftershock: the light runs down the cracks again.</summary>
        public void Shock() => _shocks.Add(Age);

        protected override bool Draw()
        {
            float p = Age, last = _shocks[_shocks.Count - 1];
            float fade = 1f - Mathf.Clamp01((p - last - T.QuakeFadeAfter) / T.QuakeFadeSeconds);
            if (fade <= 0f) return false;

            float glow = 0f;
            foreach (var s in _shocks) if (p >= s) glow = Mathf.Max(glow, 1f - Mathf.Clamp01((p - s) / T.QuakePulseSeconds));
            // Cut into the floor: a dark groove with a lit lip on its lower edge, the light in it.
            Sigil(_half, Color32.Lerp(T.Groove, At(T.Moss, 0.2f), glow), fade * (0.6f + 0.4f * glow));

            float grow = Mathf.Clamp01(p / T.QuakeGrowSeconds);
            for (int k = 0; k < T.QuakeCracks; k++)
            {
                float a = k / (float)T.QuakeCracks * Mathf.PI * 2f + FireArt.Hash(k, 2) * 0.5f;
                float len = _r * (0.75f + FireArt.Hash(k, 3) * 0.25f) * grow;
                float x = 0f, y = 0f;
                Path.Clear();
                for (int s = 0; s < len; s++)
                {
                    a += (FireArt.Hash(k, s, 4) - 0.5f) * 0.5f;
                    x += Mathf.Cos(a); y += Mathf.Sin(a);
                    int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
                    Path.Add(new Vector2(ix, iy));
                    // The light travelling outward along the crack, once for every shock.
                    float g = 0f;
                    foreach (var sh in _shocks)
                    {
                        float q = p - sh - s / Mathf.Max(1f, _r) * T.QuakeGrowSeconds;
                        if (q >= 0f) g = Mathf.Max(g, 1f - Mathf.Clamp01(q / 0.3f));
                    }
                    float taper = 1f - 0.6f * s / Mathf.Max(1f, len);
                    Over(ix, iy, Color32.Lerp(T.Groove, At(T.Moss, 0.15f), g), fade * (0.55f + 0.45f * g) * taper);
                    Over(ix, iy - 1, At(T.Stone, 0.1f), fade * 0.3f * taper * (1f - g));
                    if (g > 0.6f && FireArt.Hash(k, s, 6) > 0.5f) Over(ix, iy + 1, At(T.Moss, 0.4f), g * fade);
                }
                // Dust thrown up along it by each shock.
                if (Path.Count == 0) continue;
                for (int si = 0; si < _shocks.Count; si++)
                    for (int j = 0; j < 3; j++)
                    {
                        var at = Path[Mathf.FloorToInt(FireArt.Hash(k, si * 7 + j, 8) * Path.Count)];
                        Dust(at, p - _shocks[si] - at.magnitude / Mathf.Max(1f, _r) * T.QuakeGrowSeconds, k * 31 + si * 7 + j);
                    }
            }
            return true;
        }

        void Dust(Vector2 at, float since, int seed)
        {
            for (int k = 0; k < 3; k++)
            {
                float life = 0.5f + FireArt.Hash(k, seed, 1) * 0.4f;
                if (since < 0f || since > life) continue;
                float u = since / life;
                if (u > 0.7f && FireArt.Hash(k, seed, Mathf.FloorToInt(Time.time * 12f)) > 0.5f) continue;
                int x = Mathf.RoundToInt(at.x + (FireArt.Hash(k, seed, 2) - 0.5f) * 8f * (0.4f + u));
                int y = Mathf.RoundToInt(at.y + u * (4f + FireArt.Hash(k, seed, 3) * 6f));
                Over(x, y, At(T.Stone, 0.2f + u * 0.5f), 0.8f);
            }
        }
    }
}
