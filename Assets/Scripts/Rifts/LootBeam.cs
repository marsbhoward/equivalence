using UnityEngine;
using Convergence.Combat;

namespace Convergence.Rifts
{
    /// <summary>
    /// A thin column of light standing up from something lying on the floor to be taken (the
    /// user's pick, group 4 - "beam"): the Rift Box in cyan, a gear drop in its tier's colour,
    /// dashes rising up it, brightest at the foot. Findable from across a crowded room. The
    /// owner sets <see cref="Tint"/> and <see cref="Strength"/> each frame; taken, it calls
    /// TexelFx.BeamDrop so the beam falls back into it and pops.
    /// </summary>
    public class LootBeam : TexelPicture
    {
        public const float Height = 2.2f;

        [SerializeField] public Color Tint = Color.white;
        [SerializeField] public float Strength = 1f;

        public static LootBeam Attach(Transform owner, float foot, int order)
        {
            int up = Mathf.CeilToInt(Height * TexelFx.Body) + 2;
            return Attach<LootBeam>(owner, "beam", new Vector2(0f, foot), 2, 1, up, order);
        }

        protected override bool Draw()
        {
            var ramp = TexelFx.RampOf(Tint);
            int top = Mathf.CeilToInt(Height * Ppu);
            for (int y = 0; y < top; y++)
            {
                float k = 1f - y / (float)top;                      // strongest at the foot
                bool dash = Mathf.Repeat(y - Age * 30f, 6f) < 3f;
                Over(0, y, dash ? ramp[1] : ramp[3], 0.9f * k * Strength);
                if (y < top * 0.3f) { Over(-1, y, ramp[3], 0.3f * k * Strength); Over(1, y, ramp[3], 0.3f * k * Strength); }
            }
            return true;
        }
    }
}
