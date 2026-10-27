using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Combat
{
    /// <summary>
    /// ERUPT's release (the user's pick, 2026-10-25 - "sigil blast"): the full-heat sigil under
    /// the feet flares white-hot and burns down into the floor, and a ring of fire bursts out of it
    /// to the spread the pools are scattered in. Each pool lights as the ring reaches it
    /// (FirePool's delay, set by FireResource from the same timing). The sigil is drawn at the
    /// heat sigil's own size, so the one the player saw at full heat is the one that goes off.
    /// </summary>
    public class EruptBlast : ReleaseCanvas
    {
        [SerializeField] int _half;
        [SerializeField] float _spread;

        /// <summary>Seconds after the release the ring reaches <paramref name="distance"/>
        /// (world units) of a spread of <paramref name="spread"/>.</summary>
        public static float ArrivesAt(float distance, float spread)
            => Mathf.Clamp01(distance / Mathf.Max(0.01f, spread)) * T.EruptFrontSeconds;

        public static void Play(Vector2 feet, float spread)
        {
            int half = Player.HeatGround.SigilHalf;
            int reach = Mathf.Max(Mathf.CeilToInt(spread * Ppu), half) + 2;
            var b = Make<EruptBlast>("fire.erupt", feet, reach, reach, reach + T.EruptFrontFlame + 1, SortingOrders.GroundFx);
            b._half = half;
            b._spread = spread * Ppu;
        }

        protected override bool Draw()
        {
            float p = Age;
            float fade = 1f - Mathf.Clamp01((p - T.EruptBurntFadeFrom) / T.EruptBurntFadeSeconds);
            float frontEnd = T.EruptFrontSeconds * 1.5f;
            if (fade <= 0f && p > frontEnd) return false;

            // The sigil: white-hot, then burning down through the ramp into the floor.
            float burnt = Mathf.Clamp01((p - T.EruptFlareSeconds) / T.EruptBurntFadeFrom);
            Sigil(_half, p < T.EruptFlareSeconds ? FireArt.At(0f) : FireArt.At(0.45f + burnt * 0.55f), fade);

            if (p >= frontEnd) return true;

            // The ring: out to the spread, then guttering where it stopped.
            float front = Mathf.Min(1f, p / T.EruptFrontSeconds) * _spread;
            float gutter = Mathf.Clamp01((p - T.EruptFrontSeconds) / (frontEnd - T.EruptFrontSeconds));
            int frame = Mathf.FloorToInt(Time.time * 14f);
            Ring(0, 0, front - T.EruptFrontTexels, front, (x, y, d, a) =>
            {
                if (FireArt.Hash(x, y, frame) > 0.75f - gutter * 0.6f) return;
                Over(x, y, FireArt.At(0.1f + (front - d) / T.EruptFrontTexels * 0.5f + gutter * 0.3f));
                if (d < front - 1f) return;
                int hgt = 2 + Mathf.FloorToInt(FireArt.Noise(a * 6f, Time.time * 8f) * (T.EruptFrontFlame - 2) * (1f - gutter));
                for (int i = 1; i < hgt; i++)
                    if (FireArt.Hash(x, i, frame) > 0.35f) Over(x, y + i, FireArt.At(0.15f + i / (float)hgt * 0.6f));
            });
            return true;
        }
    }
}
