using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Small cracks in the floor where a blade went in point-first (Art/Gear/GroundPierce): a dark
    /// hole and a few short jagged lines running out from it, flattened a little so they lie on the
    /// ground. Texel-drawn at body density on the world grid, on the ground-decal layer under
    /// everything that stands; held, then faded. Scaled time.
    /// </summary>
    public class GroundCracks : MonoBehaviour
    {
        const float Ppu = 37.5f;
        const int W = 60, H = 36;

        SpriteRenderer _sr;
        Texture2D _tex;
        Sprite _sprite;
        float _age;

        public static GroundCracks Spawn(Vector2 at)
        {
            var go = new GameObject("groundcracks");
            go.transform.position = new Vector3(Mathf.Round(at.x * Ppu) / Ppu, Mathf.Round(at.y * Ppu) / Ppu, 0f);
            var c = go.AddComponent<GroundCracks>();
            c.Draw(Mathf.RoundToInt(at.x * 97f) * 31 + Mathf.RoundToInt(at.y * 89f));
            return c;
        }

        void Draw(int seed)
        {
            var rng = new System.Random(seed);
            var px = new Color32[W * H];
            var crack = new Color32(34, 27, 22, 230);
            var lip = new Color32(255, 240, 215, 70);   // the broken edge catching the light

            void Plot(int x, int y, Color32 c)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x;
                if (px[i].a >= c.a && c.a < 200) return;
                px[i] = c;
            }

            int cx = W / 2, cy = H / 2;

            // The hole the point made.
            for (int y = -1; y <= 0; y++)
                for (int x = -1; x <= 1; x++) Plot(cx + x, cy + y, crack);

            // Five to six rays, spread round the hole, each a short random walk.
            int rays = 5 + rng.Next(2);
            float a0 = (float)rng.NextDouble() * Mathf.PI * 2f;
            for (int r = 0; r < rays; r++)
            {
                float a = a0 + r * Mathf.PI * 2f / rays + ((float)rng.NextDouble() - 0.5f) * 0.6f;
                // Long enough to run out past the legs that stand over the hole.
                int len = 10 + rng.Next(10);
                float x = cx, y = cy;
                for (int s = 0; s < len; s++)
                {
                    a += ((float)rng.NextDouble() - 0.5f) * 0.7f;
                    x += Mathf.Cos(a);
                    y += Mathf.Sin(a) * 0.6f;   // lying on the ground
                    int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
                    Plot(ix, iy + 1, lip);
                    Plot(ix, iy, crack);
                    // A short fork now and then.
                    if (s > 2 && rng.Next(6) == 0)
                    {
                        float b = a + (rng.Next(2) == 0 ? 0.9f : -0.9f);
                        float fx = x, fy = y;
                        for (int f = 0; f < 3; f++)
                        {
                            fx += Mathf.Cos(b); fy += Mathf.Sin(b) * 0.6f;
                            Plot(Mathf.RoundToInt(fx), Mathf.RoundToInt(fy), crack);
                        }
                    }
                }
            }

            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            _tex.SetPixels32(px);
            _tex.Apply(false);
            _sprite = Sprite.Create(_tex, new Rect(0, 0, W, H), new Vector2((cx + 0.5f) / W, (cy + 0.5f) / H), Ppu);
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = _sprite;
            _sr.sortingOrder = SortingOrders.GroundDecal + 1;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float hold = Tuning.Sunder.CrackHoldSeconds, fade = Tuning.Sunder.CrackFadeSeconds;
            if (_age >= hold + fade) { Destroy(gameObject); return; }
            if (_sr != null && _age > hold)
                _sr.color = new Color(1f, 1f, 1f, 1f - (_age - hold) / fade);
        }

        void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
