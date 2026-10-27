using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// The elite Tempest Eye's mire wind-up (group 7, the user's pick "sparks drawn in"): sparks in
    /// its accent are drawn in to the lamp from all round it, faster as the wind-up ends, and the
    /// lamp brightens in three hard steps. It replaced a soft glow swelling on the turret.
    /// </summary>
    public class MireWindupFx : TexelPicture
    {
        const int Reach = 16, Sparks = 9;
        [SerializeField] Color32[] _ramp;
        /// <summary>0 as the wind-up starts, 1 as the shell leaves.</summary>
        [SerializeField] public float K;

        public static MireWindupFx On(Transform turret, Color[] accent)
        {
            var f = Attach<MireWindupFx>(turret, "turret.mire", Vector2.zero, Reach + 2, Reach + 2, Reach + 2, SortingOrders.Telegraph);
            // Accent runs dark to light; the ramp here is hottest first.
            f._ramp = new Color32[accent.Length];
            for (int i = 0; i < accent.Length; i++) f._ramp[i] = accent[accent.Length - 1 - i];
            return f;
        }

        protected override bool Draw()
        {
            if (_ramp == null || _ramp.Length == 0) return false;
            float k = Mathf.Clamp01(K);
            // The lamp: brighter by thirds, never a smooth swell.
            float step = Mathf.Ceil(k * 3f) / 3f;
            var lamp = At(_ramp, 0.75f - step * 0.75f);
            Ring(0, 0, 0f, 3f, (x, y, d, a) => Put(x, y, lamp));
            // The sparks: each falls in from the edge on its own clock, the clock quickening.
            float period = Mathf.Lerp(0.3f, 0.15f, k);
            for (int i = 0; i < Sparks; i++)
            {
                float u = Mathf.Repeat(Age / period + FireArt.Hash(i, 4, 0), 1f);
                float ang = FireArt.Hash(i, 5, 0) * Mathf.PI * 2f, r = Reach * (1f - u);
                if (r < 3.5f) continue;
                Put(Mathf.RoundToInt(Mathf.Cos(ang) * r), Mathf.RoundToInt(Mathf.Sin(ang) * r * 0.8f), u > 0.7f ? _ramp[0] : At(_ramp, 0.4f));
            }
            return true;
        }
    }
}
