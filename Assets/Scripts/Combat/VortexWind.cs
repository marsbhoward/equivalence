using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The Vortex art's wind: lines of air spiralling in toward the player as the pull goes off.
    /// A picture only - the pull is ResolveArc's.
    ///
    /// Drawn texel by texel into one point-filtered texture at body density, on the world's
    /// texel grid, the way the strike bar and the aim indicator are. Each line is a stretch of a
    /// LOG SPIRAL (angle grows with the log of the radius closed), so it winds tighter as it
    /// nears the middle; its head is bright and its tail thins out in a dither. The heads close
    /// faster as they go (eased in), and every line starts a little apart in time so the wind
    /// reads as a gust, not a ring. Scaled time, so a hit-stop freezes it with the
    /// swing that made it.
    /// </summary>
    public class VortexWind : MonoBehaviour
    {
        const float Ppu = 37.5f;

        Texture2D _tex;
        Sprite _sprite;
        Color32[] _px;
        List<int> _drawn = new();   // non-readonly: a domain reload rebuilds it empty, harmlessly
        int _size;
        float _radius, _life, _age, _spin;
        int _seed;

        static readonly Color32 Head = new(236, 248, 255, 235);
        static readonly Color32 Body = new(176, 214, 232, 170);
        static readonly Color32 Tail = new(128, 170, 196, 110);

        /// <summary>Spin the wind round <paramref name="centre"/>. <paramref name="spinSign"/> +1
        /// winds anticlockwise, -1 clockwise.</summary>
        public static VortexWind Play(Vector2 centre, float radius, float seconds, float spinSign)
        {
            var go = new GameObject("vortexwind");
            // On the texel grid, so the lines sit on the same pixels as everything else.
            go.transform.position = new Vector3(Mathf.Round(centre.x * Ppu) / Ppu,
                                                Mathf.Round(centre.y * Ppu) / Ppu, 0f);
            var w = go.AddComponent<VortexWind>();
            w._radius = radius;
            w._life = Mathf.Max(0.05f, seconds);
            w._spin = spinSign >= 0f ? 1f : -1f;
            w._seed = Random.Range(0, 100000);

            w._size = Mathf.CeilToInt(radius * Ppu) * 2 + 4;
            w._tex = new Texture2D(w._size, w._size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            w._px = new Color32[w._size * w._size];
            w._tex.SetPixels32(w._px);
            w._tex.Apply(false);
            w._sprite = Sprite.Create(w._tex, new Rect(0, 0, w._size, w._size), new Vector2(0.5f, 0.5f), Ppu);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = w._sprite;
            sr.sortingOrder = SortingOrders.Fx;
            w.Redraw();
            return w;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _life || _tex == null) { Destroy(gameObject); return; }
            Redraw();
        }

        void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_tex != null) Destroy(_tex);
        }

        static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393 + b * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xffffff) / (float)0x1000000;
            }
        }

        void Redraw()
        {
            if (_px == null || _tex == null) return;
            foreach (int i in _drawn) _px[i] = default;
            _drawn.Clear();

            int n = Tuning.Undertow.WindStreaks;
            float inner = Tuning.Undertow.WindInner;
            float twist = Tuning.Undertow.WindTwist * _spin;
            float travel = _life * 0.75f;   // each line's own run in; the rest staggers the starts

            for (int s = 0; s < n; s++)
            {
                float delay = Hash(_seed, s * 3) * (_life - travel);
                float p = Mathf.Clamp01((_age - delay) / travel);
                if (p <= 0f || p >= 1f) continue;

                float r0 = _radius * Mathf.Lerp(0.6f, 1f, Hash(_seed, s * 3 + 1));
                float a0 = (s + Hash(_seed, s * 3 + 2) * 0.6f) / n * Mathf.PI * 2f;
                // Eased in: a line drifts at first, then is sucked down the middle.
                float head = Mathf.Lerp(r0, inner, p * p);
                float tail = Mathf.Min(r0, head * (1f + Tuning.Undertow.WindTail));
                // Fades in from its start and out as it reaches the middle.
                float fade = Mathf.Clamp01(p * 6f) * Mathf.Clamp01((1f - p) * 5f);

                // Walk the spiral from the head outward, about half a texel at a time.
                float r = head;
                while (r <= tail)
                {
                    float a = a0 + twist * Mathf.Log(r0 / r);
                    float t = (r - head) / Mathf.Max(0.0001f, tail - head);   // 0 head, 1 tail
                    Plot(r * Mathf.Cos(a), r * Mathf.Sin(a), t, fade);
                    // Arc length per radius step grows with the twist; keep the step on the grid.
                    float ds = 0.5f / Ppu;
                    r += ds / Mathf.Sqrt(1f + twist * twist);
                }
            }

            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void Plot(float x, float y, float t, float fade)
        {
            int px = Mathf.FloorToInt(x * Ppu) + _size / 2;
            int py = Mathf.FloorToInt(y * Ppu) + _size / 2;
            if (px < 0 || py < 0 || px >= _size || py >= _size) return;
            // The tail thins out: a dither past halfway, sparser toward the end.
            if (t > 0.5f && ((px + py) & 1) == 1) return;
            if (t > 0.8f && ((px ^ py) & 2) == 2) return;

            var c = t < 0.15f ? Head : t < 0.55f ? Body : Tail;
            c.a = (byte)(c.a * fade);
            if (c.a == 0) return;
            int i = py * _size + px;
            if (_px[i].a >= c.a) return;
            if (_px[i].a == 0) _drawn.Add(i);
            _px[i] = c;
        }
    }
}
