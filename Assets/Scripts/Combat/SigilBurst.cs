using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Combat
{
    /// <summary>
    /// BURST's release (the user's pick, 2026-10-25 - "sigil rings"): the sigil emblem flashes
    /// white under the player, then each of its four rings spreads outward from its own compass
    /// point, crossing the others, out to the burst's radius, the floor wet behind them. Round
    /// rings, so their outer reach is the radius that hit.
    /// </summary>
    public class SigilBurst : ReleaseCanvas
    {
        [SerializeField] float _r;
        [SerializeField] int _half;

        public static void Play(Vector2 centre, float radius)
        {
            int r = Mathf.CeilToInt(radius * Ppu) + 2;
            var b = Make<SigilBurst>("water.burst", centre, r, r, r, SortingOrders.GroundFx);
            b._r = radius * Ppu;
            b._half = Mathf.Max(6, Mathf.RoundToInt(b._r * T.BurstSigilShare));
        }

        protected override bool Draw()
        {
            float p = Age;
            float wet = Mathf.Clamp01((p - T.BurstRingsFrom) / 0.3f) * (1f - Mathf.Clamp01((p - T.RippleDryFrom) / T.RippleDrySeconds));
            float ringsEnd = T.BurstRingsFrom + T.BurstRingsSeconds + T.BurstFadeSeconds;
            if (p > ringsEnd && wet <= 0f) return false;

            // Each ring grows from its compass point until its far side reaches the radius.
            float off = _half / (1f + SigilEmblem.RingRatio);
            float grow = Mathf.Clamp01((p - T.BurstRingsFrom) / T.BurstRingsSeconds);
            float r = grow * (_r - off);
            float ringAlpha = 1f - Mathf.Clamp01((p - T.BurstRingsFrom - T.BurstRingsSeconds) / T.BurstFadeSeconds);

            if (wet > 0f) Ring(0, 0, 0f, Mathf.Min(_r, r + off + 2f), (x, y, d, a) => Over(x, y, T.Wet, T.WetAlpha * 0.8f * wet));

            if (p < T.BurstFlashSeconds) Sigil(_half, At(T.Water, 0f));
            else Sigil(_half, At(T.Water, 0.3f), 1f - Mathf.Clamp01((p - T.BurstFlashSeconds) / 0.4f));

            if (p > T.BurstRingsFrom && ringAlpha > 0f)
            {
                float sq = Tuning.FireFx.SigilSquash;
                var colour = At(T.Water, 0.05f + grow * 0.4f);
                Ring(0, off * sq, r - 2f, r, (x, y, d, a) => Over(x, y, colour, ringAlpha));
                Ring(off, 0, r - 2f, r, (x, y, d, a) => Over(x, y, colour, ringAlpha));
                Ring(0, -off * sq, r - 2f, r, (x, y, d, a) => Over(x, y, colour, ringAlpha));
                Ring(-off, 0, r - 2f, r, (x, y, d, a) => Over(x, y, colour, ringAlpha));
            }
            return true;
        }
    }
}
