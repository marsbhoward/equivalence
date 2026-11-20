using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// The Maelstrom's water on the floor (the mock's, at the body's density): while it drains away and
    /// travels beneath the floor, RIPPLES spreading from where it is - so the player never loses it; as it
    /// erupts, the WHIRLPOOL - three arcs turning inward over the ring's ground. Its owner sets the phase
    /// every frame (<see cref="Show"/>). A child of the body, drawn on the floor under every body.
    /// </summary>
    public class MaelstromWater : TexelPicture
    {
        [SerializeField] MaelstromPhase _phase;
        [SerializeField] float _progress, _radius;

        public static MaelstromWater Attach(Transform body)
        {
            int r = Mathf.CeilToInt(Tuning.Enemy.MaelstromEliteRadius * TexelFx.Body) + 2;
            var w = Attach<MaelstromWater>(body, "maelstrom.water", Vector2.zero, r, r, r, SortingOrders.GroundFx);
            return w;
        }

        public void Show(MaelstromPhase phase, float progress, float radius)
        {
            _phase = phase;
            _progress = progress;
            _radius = radius;
        }

        protected override bool Draw()
        {
            var acc = MaelstromArt.Reserved;
            Color32 a = acc[2], hot = acc[3];
            float t = Age;
            switch (_phase)
            {
                case MaelstromPhase.Dive:
                case MaelstromPhase.Under:
                case MaelstromPhase.Tell:
                {
                    // a dark patch where it is, and two rings spreading from it (squashed onto the floor)
                    Ellipse(12f, 5f, (x, y, d) => { if (d <= 1f) Over(x, y, new Color32(0, 0, 0, 255), 0.28f); });
                    for (int s = 0; s < 2; s++)
                    {
                        float r = (t * 14f + s * 9f) % 18f + 3f, fade = 1f - r / 21f;
                        Ellipse(r + 0.6f, (r + 0.6f) * 0.45f, (x, y, d) =>
                        {
                            float rr = d * (r + 0.6f);
                            if (rr > r - 0.6f) Over(x, y, a, 0.8f * fade);
                        });
                    }
                    return true;
                }
                case MaelstromPhase.Rise:
                {
                    // the whirlpool: three arcs turning over the ring's ground, closing in as it rises
                    float R = _radius * TexelFx.Body, k = _progress;
                    for (int s = 0; s < 3; s++)
                    {
                        float rad = R * (0.3f + s * 0.3f) * (1f - 0.3f * k), a0 = t * 6f + s * 2f;
                        Color32 c = s == 0 ? hot : a;
                        float alpha = (s == 0 ? 0.85f : 0.6f) * (1f - k * 0.6f);
                        Ellipse(rad + 0.8f, (rad + 0.8f) * 0.45f, (x, y, d) =>
                        {
                            if (d * (rad + 0.8f) < rad - 0.8f) return;
                            float ang = Mathf.Repeat(Mathf.Atan2(y / 0.45f, x) - a0, Mathf.PI * 2f);
                            if (ang < 4f) Over(x, y, c, alpha);
                        });
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>Every texel inside an ellipse of half-axes (rx, ry), with its normalised distance from the centre.</summary>
        void Ellipse(float rx, float ry, System.Action<int, int, float> fn)
        {
            int ix = Mathf.CeilToInt(rx), iy = Mathf.CeilToInt(ry);
            for (int y = -iy; y <= iy; y++)
            for (int x = -ix; x <= ix; x++)
            {
                float nx = x / rx, ny = y / ry, d = Mathf.Sqrt(nx * nx + ny * ny);
                if (d <= 1f) fn(x, y, d);
            }
        }
    }
}
