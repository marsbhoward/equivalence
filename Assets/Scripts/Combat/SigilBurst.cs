using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Combat
{
    /// <summary>
    /// BURST's release - FOUR SPRINGS (the user's pick, 2026-11-05; the sigil rings of 2026-10-25
    /// grown from it): the sigil flashes white under the player and a pillar of water rises at each
    /// of its four compass points and falls back; then a wave spreads from each point, crossing the
    /// others, out to the burst's radius, the floor wet behind them, and each body splashes as the
    /// nearest wave reaches it. Round waves, so their outer reach is the radius that hit.
    /// </summary>
    public class SigilBurst : ReleaseCanvas
    {
        [SerializeField] float _r;
        [SerializeField] int _half;
        [SerializeField] Vector2[] _bodies;   // texels from the centre

        public static void Play(Vector2 centre, float radius, IEnumerable<Vector2> bodies = null)
        {
            int r = Mathf.CeilToInt(radius * Ppu) + 3;
            var b = Make<SigilBurst>("water.burst", centre, r, r, r + Mathf.Max(SplashHeight, T.SpringsPillarTexels) + 2,
                                     SortingOrders.GroundFx);
            b._r = radius * Ppu;
            b._half = Mathf.Max(6, Mathf.RoundToInt(b._r * T.BurstSigilShare));
            var list = new List<Vector2>();
            if (bodies != null) foreach (var x in bodies) list.Add((x - centre) * Ppu);
            b._bodies = list.ToArray();
        }

        protected override bool Draw()
        {
            float p = Age;
            float wet = Mathf.Clamp01((p - T.BurstRingsFrom) / 0.3f) * (1f - Mathf.Clamp01((p - T.RippleDryFrom) / T.RippleDrySeconds));
            float ringsEnd = T.BurstRingsFrom + T.BurstRingsSeconds + T.BurstFadeSeconds;
            if (p > ringsEnd && wet <= 0f) return false;

            float sq = Tuning.FireFx.SigilSquash;
            float off = _half / (1f + SigilEmblem.RingRatio);
            var points = new[] { new Vector2(0, off * sq), new Vector2(off, 0), new Vector2(0, -off * sq), new Vector2(-off, 0) };

            // Each wave grows from its compass point until its far side reaches the radius.
            float grow = EaseOut((p - T.BurstRingsFrom) / T.BurstRingsSeconds);
            float r = grow * (_r - off);
            float ringAlpha = 1f - Mathf.Clamp01((p - T.BurstRingsFrom - T.BurstRingsSeconds) / T.BurstFadeSeconds);

            if (wet > 0f) Ring(0, 0, 0f, Mathf.Min(_r, r + off + 2f), (x, y, d, a) => Over(x, y, T.Wet, T.WetAlpha * 0.8f * wet));

            if (p < T.BurstFlashSeconds) Sigil(_half, At(T.Water, 0f));
            else Sigil(_half, At(T.Water, 0.3f), 1f - Mathf.Clamp01((p - T.BurstFlashSeconds) / 0.4f));

            // The four springs: pillars rising at the compass points, falling back as the waves leave.
            if (p < T.BurstRingsFrom + 0.06f)
            {
                float rise = Mathf.Sin(Mathf.Clamp01(p / (T.BurstRingsFrom + 0.06f)) * Mathf.PI);
                int h = Mathf.RoundToInt(rise * T.SpringsPillarTexels);
                foreach (var pt in points)
                {
                    int px = Mathf.RoundToInt(pt.x), py = Mathf.RoundToInt(pt.y);
                    for (int y = 0; y < h; y++)
                    {
                        int half = T.SpringsPillarHalfWidth - (y % 3 == 0 ? 0 : 1);
                        var c = At(T.Water, y > h - 3 ? 0f : y % 3 == 0 ? 0.15f : 0.35f);
                        for (int x = -half; x <= half; x++) Over(px + x, py + y, c);
                    }
                }
            }

            if (p > T.BurstRingsFrom && ringAlpha > 0f)
            {
                var body = At(T.Water, 0.3f + grow * 0.25f);
                var crest = At(T.Water, 0.05f);
                foreach (var pt in points)
                {
                    Ring(pt.x, pt.y, r - 3f, r - 1f, (x, y, d, a) => Over(x, y, body, ringAlpha));
                    Ring(pt.x, pt.y, r - 1f, r, (x, y, d, a) => Over(x, y, crest, ringAlpha));
                }
            }

            if (_bodies != null)
                for (int i = 0; i < _bodies.Length; i++)
                {
                    float near = float.MaxValue;
                    foreach (var pt in points) near = Mathf.Min(near, (_bodies[i] - pt).magnitude);
                    WaterSplash(_bodies[i], p - T.BurstRingsFrom - EaseOutAt(near / Mathf.Max(1f, _r - off)) * T.BurstRingsSeconds, i);
                }
            return true;
        }
    }
}
