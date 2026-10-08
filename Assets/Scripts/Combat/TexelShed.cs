using System.Collections.Generic;
using Convergence.Core;
using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// Stone falling away from a piece: every texel a renderer shows in <c>from</c> that is gone or
    /// different in <c>to</c> breaks off at its exact world position and colour and FALLS - a
    /// little sideways spread, gravity, a fade at the end. The Aether Dual Discs' lion bodies dropping
    /// off their heads, the Geode Stone's rind off its crystals.
    ///
    /// Disintegration's method, turned downward: the texels are plotted every frame into one
    /// point-filtered canvas at the source's own texel size, so the stone stays on its grid as it
    /// falls. Self-contained and self-destroying. Scaled time.
    /// </summary>
    public class TexelShed : MonoBehaviour
    {
        const int MaxTexels = 900;
        const float Seconds = 0.6f, Gravity = 7f, Spread = 0.5f;

        struct Chip { public Vector2 From, V; public Color C; public float Delay; }

        Chip[] _chips;
        Color32[] _px;
        Texture2D _tex;
        Vector2 _origin;
        float _cell, _t;
        int _w, _h;

        public static TexelShed Drop(SpriteRenderer sr, Sprite from, Sprite to)
        {
            if (sr == null || from == null) return null;
            var ft = from.texture;
            if (ft == null || !ft.isReadable) return null;
            var fr = from.textureRect;
            int w = (int)fr.width, h = (int)fr.height;
            var a = ft.GetPixels((int)fr.x, (int)fr.y, w, h);
            Color[] b = null;
            if (to != null && to.texture != null && to.texture.isReadable)
            {
                var tr = to.textureRect;
                if ((int)tr.width == w && (int)tr.height == h)
                    b = to.texture.GetPixels((int)tr.x, (int)tr.y, w, h);
            }

            float cell = Mathf.Abs(sr.transform.lossyScale.y) / from.pixelsPerUnit;
            cell = Mathf.Max(cell, 1f / 80f);
            var chips = new List<Chip>();
            var seen = new HashSet<Vector2Int>();
            var pivot = from.pivot;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = a[y * w + x];
                if (c.a < 0.5f) continue;
                if (b != null)
                {
                    var d = b[y * w + x];
                    if (d.a >= 0.5f && Mathf.Abs(d.r - c.r) + Mathf.Abs(d.g - c.g) + Mathf.Abs(d.b - c.b) < 0.02f) continue;
                }
                float lx = (x + 0.5f - pivot.x) / from.pixelsPerUnit, ly = (y + 0.5f - pivot.y) / from.pixelsPerUnit;
                if (sr.flipX) lx = -lx;
                if (sr.flipY) ly = -ly;
                Vector2 world = sr.transform.TransformPoint(new Vector3(lx, ly, 0f));
                if (!seen.Add(new Vector2Int(Mathf.FloorToInt(world.x / cell), Mathf.FloorToInt(world.y / cell)))) continue;
                c *= sr.color; c.a = 1f;
                chips.Add(new Chip
                {
                    From = world, C = c,
                    V = new Vector2(Random.Range(-Spread, Spread), Random.Range(-0.2f, 0.6f)),
                    Delay = Random.Range(0f, 0.08f),
                });
            }
            if (chips.Count == 0) return null;
            while (chips.Count > MaxTexels)
                for (int i = chips.Count - 1; i >= 0 && chips.Count > MaxTexels; i -= 2) chips.RemoveAt(i);

            Vector2 min = chips[0].From, max = min;
            foreach (var ch in chips) { min = Vector2.Min(min, ch.From); max = Vector2.Max(max, ch.From); }
            float fall = 0.5f * Gravity * Seconds * Seconds + 0.2f;
            min -= new Vector2(Spread * Seconds + cell * 2f, fall);
            max += new Vector2(Spread * Seconds + cell * 2f, 0.4f);
            min = new Vector2(Mathf.Floor(min.x / cell) * cell, Mathf.Floor(min.y / cell) * cell);

            var go = new GameObject("texel-shed");
            go.transform.position = new Vector3(min.x, min.y, sr.transform.position.z);
            var s = go.AddComponent<TexelShed>();
            s._chips = chips.ToArray();
            s._cell = cell;
            s._origin = min;
            s._w = Mathf.Min(1024, Mathf.CeilToInt((max.x - min.x) / cell) + 1);
            s._h = Mathf.Min(1024, Mathf.CeilToInt((max.y - min.y) / cell) + 1);
            s._px = new Color32[s._w * s._h];
            s._tex = new Texture2D(s._w, s._h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            var spr = Sprite.Create(s._tex, new Rect(0, 0, s._w, s._h), Vector2.zero, 1f / cell);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = spr;
            r.sortingLayerID = sr.sortingLayerID;
            r.sortingOrder = sr.sortingOrder;
            s.Plot();
            return s;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= Seconds + 0.1f) { Destroy(gameObject); return; }
            Plot();
        }

        void Plot()
        {
            System.Array.Clear(_px, 0, _px.Length);
            foreach (var ch in _chips)
            {
                float t = Mathf.Max(0f, _t - ch.Delay);
                float k = t / Seconds;
                if (k >= 1f) continue;
                var at = ch.From + ch.V * t + new Vector2(0f, -0.5f * Gravity * t * t);
                var c = ch.C;
                c.a = 1f - Mathf.SmoothStep(0f, 1f, (k - 0.55f) / 0.45f);
                if (c.a <= 0.02f) continue;
                int x = Mathf.FloorToInt((at.x - _origin.x) / _cell), y = Mathf.FloorToInt((at.y - _origin.y) / _cell);
                if (x < 0 || y < 0 || x >= _w || y >= _h) continue;
                _px[y * _w + x] = c;
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void OnDestroy()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null) Destroy(sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
