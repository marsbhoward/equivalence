using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Rifts
{
    /// <summary>
    /// Something collapsing into itself - the death an expired Rift Box dies, a Collapsing Rift
    /// running out, and the death anything killed by the Rift Blade dies.
    ///
    /// AN IMPLOSION IS NOT A FLASH PLAYED BACKWARDS: things move INWARD and arrive - shards drawn
    /// in on a spiral, accelerating into a throat - and only then the small hard pop of the thing
    /// closing, last. Then the pieces it could not hold blow out and fall.
    ///
    /// Texel-drawn (the user's pick, group 4 - "drawn, then squeezed", 2026-10-26). A Rift's own
    /// collapse (<c>tear</c>) also squeezes the tear itself to a line and then to the white point.
    /// No ring: a ring on the floor means a hazard, or the player's own.
    ///
    /// SHARED BY EVERY USE ON PURPOSE: a player who has watched a box run out already knows what
    /// the blade's death means.
    /// </summary>
    public class RiftImplosion : ReleaseCanvas
    {
        const float Pull = 0.45f, Life = 0.9f;
        const int Shards = 7;

        [SerializeField] float _scale;   // texels things collapse from
        [SerializeField] bool _tear;
        [SerializeField] int _seed;
        static int _serial;

        /// <param name="scale">Roughly the radius things collapse from (world units). A box is
        /// small; an enemy wants the effect at about its own size.</param>
        /// <param name="tear">A Rift's own collapse: the tear squeezes shut first.</param>
        public static RiftImplosion At(Vector2 where, float scale = 1f, bool tear = false)
        {
            float s = Mathf.Max(0.2f, scale) * TexelFx.Body;
            int half = Mathf.CeilToInt(Mathf.Max(s * 1.2f, tear ? RiftShape.HalfHeight : 0f)) + 18;
            var e = Make<RiftImplosion>("rift.implosion", where, half, half, half, SortingOrders.Fx - 1, TexelFx.Body, false);
            e._scale = s; e._tear = tear; e._seed = (_serial++ * 4243) & 0x7fff;
            return e;
        }

        protected override bool Draw()
        {
            if (Age > Life) return false;
            var ramp = TexelFx.Rift;
            float k = Mathf.Clamp01(Age / Pull), pull = k * k * k;   // accelerating, as falling in

            if (Age < Pull)
            {
                // The tear squeezing: narrow to a line, then the line drawn up to the point.
                if (_tear)
                {
                    float w = 1f - Mathf.Clamp01(k / 0.75f), hgt = 1f - Mathf.Clamp01((k - 0.7f) / 0.3f);
                    int hh = RiftShape.HalfHeight;
                    for (int y = -hh; y <= hh; y++)
                    {
                        float half = RiftShape.HalfWidth(y, Mathf.Max(0.06f, w), Mathf.Max(0.02f, hgt));
                        if (half < 0f) continue;
                        int e = Mathf.RoundToInt(half);
                        Put(e, y, ramp[0]); Put(-e, y, ramp[0]);
                        if (e > 1) { Over(e - 1, y, ramp[2]); Over(1 - e, y, ramp[2]); }
                    }
                }
                // Shards spiralling in to the throat, two texels each, shortening as they go.
                for (int i = 0; i < Shards; i++)
                {
                    float a0 = i / (float)Shards * Mathf.PI * 2f + (FireArt.Hash(i, _seed) - 0.5f) * 0.7f;
                    float r = _scale * (0.75f + FireArt.Hash(i, _seed, 1) * 0.4f) * (1f - pull), a = a0 + pull * 2.6f;
                    int x = Mathf.RoundToInt(Mathf.Cos(a) * r), y = Mathf.RoundToInt(Mathf.Sin(a) * r);
                    Put(x, y, ramp[1]);
                    if (pull < 0.6f) Put(x + Mathf.RoundToInt(Mathf.Cos(a + 1.57f)), y + Mathf.RoundToInt(Mathf.Sin(a + 1.57f)), ramp[3]);
                }
            }

            // The pop: a hard white point as everything arrives.
            float pop = Age - Pull * 0.85f;
            if (pop >= 0f && pop < 0.1f)
            {
                int r = pop < 0.05f ? 3 : 2;
                Put(0, 0, ramp[0]);
                for (int i = 1; i <= r; i++) { var c = At(ramp, i / (float)(r + 1) * 0.6f); Put(i, 0, c); Put(-i, 0, c); Put(0, i, c); Put(0, -i, c); }
            }

            // What it could not hold blows out and falls.
            float out_ = Age - Pull;
            if (out_ > 0f)
            {
                float u = out_ / (Life - Pull);
                int n = Mathf.Clamp(Mathf.RoundToInt(_scale * 0.4f), 6, 14);
                for (int i = 0; i < n; i++)
                {
                    float a = FireArt.Hash(i, _seed, 2) * Mathf.PI * 2f, v = 30f + FireArt.Hash(i, _seed, 3) * 30f;
                    float x = Mathf.Cos(a) * v * out_, y = Mathf.Sin(a) * v * out_ - 70f * out_ * out_;
                    if (u > 0.7f && FireArt.Hash(i, Mathf.FloorToInt(Age * 14f), _seed) > 0.5f) continue;
                    Put(Mathf.RoundToInt(x), Mathf.RoundToInt(y), At(ramp, 0.1f + u * 0.7f));
                }
            }
            return true;
        }
    }
}
