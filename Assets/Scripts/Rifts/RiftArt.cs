using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Rifts
{
    /// <summary>
    /// A Rift's tear on the texel grid (the user's picks, group 4, 2026-10-26 - "shimmering rim",
    /// "drawn, then squeezed"). The aperture the hub is glimpsed through is cut from the SAME
    /// curve the rim is drawn on (Spr.TearHalfWidth), at body density, so the hole and its edge
    /// can never disagree. No halo: the room's light pool round the tear (its Glow) is the glow.
    /// </summary>
    public static class RiftShape
    {
        public static int HalfHeight => Mathf.RoundToInt(Rift.Height * 0.5f * TexelFx.Body);

        /// <summary>Half the tear's width, in texels, at row <paramref name="y"/> from its middle.</summary>
        public static float HalfWidth(int y, float squeezeW = 1f, float squeezeH = 1f)
        {
            float hh = HalfHeight * Mathf.Max(0.01f, squeezeH);
            if (Mathf.Abs(y) > hh) return -1f;
            return Spr.TearHalfWidth(y / hh) * Rift.Width * 0.5f * TexelFx.Body * squeezeW;
        }

        static Sprite _aperture;

        /// <summary>The mask the view is seen through: the tear, filled, one texel inside the rim.</summary>
        public static Sprite Aperture
        {
            get
            {
                if (_aperture != null) return _aperture;
                int hh = HalfHeight, hw = Mathf.CeilToInt(Rift.Width * 0.5f * TexelFx.Body);
                int w = hw * 2 + 1, h = hh * 2 + 1;
                var px = new Color32[w * h];
                for (int y = -hh; y <= hh; y++)
                {
                    float half = HalfWidth(y);
                    for (int x = -hw; x <= hw; x++)
                        if (half > 0f && Mathf.Abs(x) <= half) px[(y + hh) * w + x + hw] = new Color32(255, 255, 255, 255);
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                tex.SetPixels32(px);
                tex.Apply();
                return _aperture = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), TexelFx.Body, 0, SpriteMeshType.FullRect);
            }
        }
    }

    /// <summary>
    /// The tear's lit edge: white-cyan texels shimmering one by one round the tear with a cyan line
    /// inside them, and flakes drifting up off its sides. Sealed (a Red Rift), it throbs ember and
    /// wound-red instead. When the tear OPENS (a Collapsing Rift holding, a Red Rift broken) the rim
    /// is drawn again up both sides from the foot, white.
    /// </summary>
    public class RiftRim : TexelPicture
    {
        [SerializeField] Rift _r;
        [SerializeField] float _openAt = -10f;

        static readonly Color32 Ember = new(255, 56, 41, 255), Wound = new(140, 15, 20, 255);

        /// <summary>The rim drawn up both sides from the foot, now.</summary>
        public void Open() => _openAt = Age;

        protected override bool Draw()
        {
            if (_r == null) _r = GetComponentInParent<Rift>();
            if (_r == null) return false;
            float a = _r.RimAlpha;
            if (a <= 0f) return false;
            var ramp = TexelFx.Rift;
            int hh = RiftShape.HalfHeight;
            float open = Mathf.Clamp01((Age - _openAt) / 0.6f);
            bool opening = Age - _openAt < 0.9f;
            int frame = Mathf.FloorToInt(Age * 10f);
            float throb = 0.5f + 0.5f * Mathf.Sin(Age * 1.6f);

            float prev = -1f;
            for (int y = -hh; y <= hh; y++)
            {
                float half = RiftShape.HalfWidth(y);
                if (half <= 0f) { prev = -1f; continue; }
                float s = (y + hh) / (2f * hh);                 // 0 at the foot, 1 at the top
                float tip = Mathf.Clamp01((1f - Mathf.Abs(y / (float)hh)) * 6f);
                int edge = Mathf.RoundToInt(half);
                // Join to the row below, so the curve has no gaps where it widens fast.
                int from = prev < 0f ? edge : Mathf.Min(edge, Mathf.RoundToInt(prev)), to = prev < 0f ? edge : Mathf.Max(edge, Mathf.RoundToInt(prev));
                prev = half;
                for (int side = -1; side <= 1; side += 2)
                    for (int e = from; e <= to; e++)
                    {
                        Color32 outer, inner;
                        if (_r.Sealed)
                        {
                            outer = Lerp(Wound, Ember, throb); inner = Wound;
                        }
                        else
                        {
                            outer = FireArt.Hash(e * side, y, frame) > 0.3f ? ramp[0] : ramp[2];
                            inner = ramp[2];
                            // The tracer: rows below the climbing front lit white as it passes.
                            if (opening && s < open) outer = inner = ramp[0];
                        }
                        Over(side * e, y, outer, a * tip);
                        Over(side * (e - 1), y, inner, a * tip * 0.9f);
                    }
            }

            // Flakes drifting up and out off the sides.
            for (int k = 0; k < 8; k++)
            {
                float age = Mathf.Repeat(Age * 0.5f + FireArt.Hash(k, 3), 1f);
                int side = FireArt.Hash(k, 4) > 0.5f ? 1 : -1;
                int y0 = Mathf.RoundToInt(-hh * 0.6f + FireArt.Hash(k, 5) * hh * 1.3f);
                float half = Mathf.Max(0f, RiftShape.HalfWidth(y0));
                int x = side * Mathf.RoundToInt(half + 2f + age * 6f + FireArt.Hash(k, 6) * 3f), y = y0 + Mathf.RoundToInt(age * 10f);
                Over(x, y, _r.Sealed ? Ember : At(ramp, 0.1f + age * 0.7f), a * (1f - age));
            }
            return true;
        }
    }
}
