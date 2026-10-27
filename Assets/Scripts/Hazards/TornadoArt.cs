using UnityEngine;
using Convergence.Art.Biomes;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A tornado's pictures (the user's pick, group 4 - "banded grit", 2026-10-26): a solid funnel
    /// of the place's own material turning in bands, stones (leaves, salt) circling it, and on the
    /// floor the hitbox ring - solid and red-edged, the danger rule - with dust drawn round and in
    /// at the foot. Heavier and greyer than Squall's small yellow whirl, which is the player's.
    /// </summary>
    static class TornadoLook
    {
        static Color32 C(float r, float g, float b) => new Color(r, g, b);

        /// <summary>The funnel's ramp, light to dark, in the floor's biome: stone grit in the Tower
        /// and the sky ruin, leaves in the forest, salt on the flats.</summary>
        public static Color32[] Body()
        {
            if (Biome.Is(BiomeKind.Forest)) return new[] { C(0.62f, 0.72f, 0.36f), C(0.45f, 0.58f, 0.28f), C(0.33f, 0.45f, 0.22f), C(0.25f, 0.33f, 0.17f), C(0.19f, 0.22f, 0.12f), C(0.11f, 0.12f, 0.07f) };
            if (Biome.Is(BiomeKind.SaltFlats)) return new[] { C(1f, 1f, 1f), C(0.95f, 0.93f, 0.92f), C(0.86f, 0.80f, 0.82f), C(0.72f, 0.66f, 0.69f), C(0.55f, 0.50f, 0.53f), C(0.36f, 0.32f, 0.35f) };
            return new[] { C(0.89f, 0.88f, 0.85f), C(0.71f, 0.69f, 0.65f), C(0.52f, 0.50f, 0.46f), C(0.36f, 0.34f, 0.31f), C(0.23f, 0.22f, 0.20f), C(0.13f, 0.12f, 0.11f) };
        }

        /// <summary>What circles it: two texels of stone, leaf or salt.</summary>
        public static Color32[] Debris()
        {
            if (Biome.Is(BiomeKind.Forest)) return new[] { C(0.55f, 0.66f, 0.30f), C(0.42f, 0.31f, 0.17f) };
            if (Biome.Is(BiomeKind.SaltFlats)) return new[] { C(1f, 1f, 1f), C(0.80f, 0.78f, 0.82f) };
            return new[] { C(0.42f, 0.40f, 0.36f), C(0.30f, 0.28f, 0.26f) };
        }
    }

    /// <summary>The ground under a funnel: its shadow, dust drawn round and in, and the HITBOX ring.</summary>
    public class TornadoGround : TexelPicture
    {
        [SerializeField] Tornado _t;
        Color32[] _dust;

        protected override bool Draw()
        {
            if (_t == null) _t = GetComponentInParent<Tornado>();
            if (_t == null) return false;
            _dust ??= TornadoLook.Body();
            float r = Tuning.Tornado.Radius * Ppu, a = _t.Alpha, t = _t.Clock;
            float shown = Mathf.Max(_t.Built, 0.4f) * a;
            Ring(0, 0, 0, r + 3f, (x, y, d, ang) => Over(x, y, new Color32(0, 0, 0, 255), 0.3f * shown));
            // Dust skimming round the foot, gathering in while it forms.
            for (int k = 0; k < 26; k++)
            {
                float ang = FireArt.Hash(k, 3) * Mathf.PI * 2f + t * (3f + FireArt.Hash(k, 4) * 2f);
                float rr = r * Mathf.Lerp(1.5f, 1f, _t.Gather) * (0.35f + Mathf.Repeat(FireArt.Hash(k, 5) + t * 0.6f, 1f) * 0.75f) * (1f + _t.Unwind);
                Over(Mathf.RoundToInt(Mathf.Cos(ang) * rr), Mathf.RoundToInt(Mathf.Sin(ang) * rr), At(_dust, 0.1f + FireArt.Hash(k, 6) * 0.4f), 0.8f * _t.Gather * a);
            }
            // The ring IS the hitbox, drawn exactly on its radius: red outside, pale inside.
            float ra = _t.RingAlpha;
            Ring(0, 0, r - 1.8f, r, (x, y, d, ang) => Over(x, y, d > r - 0.9f ? TexelFx.Danger[3] : TexelFx.Danger[1], ra));
            return true;
        }
    }

    /// <summary>The standing funnel: rows of grit turning in bands, ragged at the top, bending as it
    /// sways, with debris circling - in front of it on the near half of its orbit.</summary>
    public class TornadoFunnel : TexelPicture
    {
        [SerializeField] Tornado _t;
        Color32[] _body, _debris;

        protected override bool Draw()
        {
            if (_t == null) _t = GetComponentInParent<Tornado>();
            if (_t == null) return false;
            _body ??= TornadoLook.Body();
            _debris ??= TornadoLook.Debris();
            float t = _t.Clock, a = _t.Alpha, spread = 1f + _t.Unwind * 0.6f;
            float height = Tuning.Tornado.Height * Ppu * (1f + _t.Unwind * 0.25f);
            float foot = Tuning.Tornado.Radius * 0.55f * 0.5f * Ppu, top = Tuning.Tornado.TopWidth * 0.5f * Ppu;
            float spin = Tuning.Tornado.SpinDegreesPerSecond * Mathf.Deg2Rad;
            int built = Mathf.CeilToInt(height * _t.Built);
            if (a <= 0f || built <= 0) return false;

            Debris(false, height, foot, top, spread, t, a);
            for (int h = 0; h < built; h++)
            {
                float k = h / height, w = Mathf.Lerp(foot, top, Mathf.Pow(k, 1.5f)) * spread;
                int sway = Mathf.RoundToInt(_t.SwayAt(k) * Ppu);
                int iw = Mathf.CeilToInt(w);
                for (int x = -iw; x <= iw; x++)
                {
                    float u = x / Mathf.Max(1f, w);
                    if (Mathf.Abs(u) > 1f) continue;
                    // Ragged where it is still building, and at the very top.
                    if (h > built - 4 && FireArt.Hash(x, h, Mathf.FloorToInt(t * 10f)) > 0.6f) continue;
                    int band = (int)Mathf.Repeat(Mathf.Floor((Mathf.Asin(u) + t * spin * Mathf.Lerp(1.25f, 0.8f, k) + h * 0.18f) / (Mathf.PI / 3f)), 2f);
                    var c = Mathf.Abs(u) > 0.82f ? _body[4] : band == 1 ? _body[2] : _body[3];
                    Over(x + sway, h, c, 0.95f * a);
                }
            }
            Debris(true, height, foot, top, spread, t, a);
            return true;
        }

        void Debris(bool nearHalf, float height, float foot, float top, float spread, float t, float a)
        {
            if (_t.Built < 1f) return;
            for (int k = 0; k < 6; k++)
            {
                float ang = t * 7f + k * 1.05f;
                if ((Mathf.Sin(ang) > 0f) != nearHalf) continue;
                float hh = height * (0.17f + k * 0.13f), kk = hh / height;
                float r = Mathf.Lerp(foot, top, Mathf.Pow(kk, 1.5f)) * spread + 3f;
                int x = Mathf.RoundToInt(Mathf.Cos(ang) * r + _t.SwayAt(kk) * Ppu), y = Mathf.RoundToInt(hh - Mathf.Sin(ang) * 2f);
                Over(x, y, _debris[1], a); Over(x + 1, y, _debris[0], a);
            }
        }
    }
}
