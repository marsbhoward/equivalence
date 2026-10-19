using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A furrow cut in the floor by a rolling disc (Plow - the user's call, 2026-10-15): a dark
    /// groove with a lit lip on its far side and dirt thrown out beside it, revealed behind the
    /// disc as it rolls (<see cref="Reveal"/>). Each texel holds for FurrowHoldSeconds from the
    /// moment it was CUT, then fades, so the line goes in the order it was made - from the
    /// thrower's end, trailing the disc (the user's call). Texel-drawn at body density in
    /// ONE unrotated canvas, so the line stays on the pixel grid at any angle (GroundCracks' way),
    /// on the ground-decal layer under everything that stands. Scaled time.
    /// </summary>
    public class GroundFurrow : MonoBehaviour
    {
        const float Ppu = 37.5f;
        const int Pad = 4;

        struct Mark { public int I; public float At; public Color32 C; }

        SpriteRenderer _sr;
        Texture2D _tex;
        Sprite _sprite;
        Color32[] _px, _cut;          // what is shown, and each texel's colour as cut
        float[] _cutAt;               // when each texel was cut (scaled clock); NaN = not yet
        List<Mark> _marks = new();   // non-readonly - domain reload
        int _w, _h;
        float _length, _clock, _lastCut;
        bool _done;

        public static GroundFurrow Cut(Vector2 from, Vector2 to, int seed)
        {
            var go = new GameObject("groundfurrow");
            var f = go.AddComponent<GroundFurrow>();
            f.Build(from, to, seed);
            return f;
        }

        void Build(Vector2 from, Vector2 to, int seed)
        {
            var rng = new System.Random(seed);
            // Texel coordinates of both ends, and the canvas round them.
            Vector2Int a = new(Mathf.RoundToInt(from.x * Ppu), Mathf.RoundToInt(from.y * Ppu));
            Vector2Int b = new(Mathf.RoundToInt(to.x * Ppu), Mathf.RoundToInt(to.y * Ppu));
            int minX = Mathf.Min(a.x, b.x) - Pad, minY = Mathf.Min(a.y, b.y) - Pad;
            _w = Mathf.Abs(a.x - b.x) + Pad * 2 + 1;
            _h = Mathf.Abs(a.y - b.y) + Pad * 2 + 1;
            _px = new Color32[_w * _h];
            _cut = new Color32[_w * _h];
            _cutAt = new float[_w * _h];
            for (int i = 0; i < _cutAt.Length; i++) _cutAt[i] = float.NaN;
            transform.position = new Vector3(minX / Ppu, minY / Ppu, 0f);

            var groove = new Color32(30, 24, 20, 225);
            var deep = new Color32(18, 14, 12, 235);
            var lip = new Color32(255, 238, 210, 80);
            var dirt = new Color32(70, 56, 44, 170);

            Vector2 d = b - a;
            _length = Mathf.Max(1f, d.magnitude);
            var dir = d / _length;
            var perp = new Vector2(-dir.y, dir.x);
            if (perp.y < 0f) perp = -perp;   // the lip on the side away from the camera

            void Put(Vector2 p, float at, Color32 c)
            {
                int x = Mathf.RoundToInt(p.x) - minX, y = Mathf.RoundToInt(p.y) - minY;
                if (x < 0 || y < 0 || x >= _w || y >= _h) return;
                _marks.Add(new Mark { I = y * _w + x, At = at, C = c });
            }

            float wobble = 0f;
            for (float s = 0f; s <= _length; s += 0.5f)
            {
                // A slight drift across the groove, so it reads as cut by a wheel, not ruled.
                if (rng.Next(5) == 0) wobble = Mathf.Clamp(wobble + (rng.Next(3) - 1) * 0.5f, -0.5f, 0.5f);
                var c = (Vector2)a + dir * s + perp * wobble;
                float at = s / _length;
                Put(c, at, deep);
                Put(c - perp, at, groove);
                Put(c + perp, at, groove);
                Put(c + perp * 2f, at, lip);
                // Dirt thrown out to either side now and then.
                if (rng.Next(4) == 0)
                {
                    float side = rng.Next(2) == 0 ? -1f : 1f;
                    Put(c + perp * side * (2.5f + rng.Next(3)), at, dirt);
                }
            }

            _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            _tex.SetPixels32(_px);
            _tex.Apply(false);
            _sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), Vector2.zero, Ppu);
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = _sprite;
            _sr.sortingOrder = SortingOrders.GroundDecal + 1;
        }

        /// <summary>Cut the furrow up to <paramref name="fraction"/> of its length (the disc's
        /// progress). <paramref name="done"/>: the disc has stopped - nothing more will be cut, and
        /// the furrow goes once its last texel has faded.</summary>
        public void Reveal(float fraction, bool done = false)
        {
            if (_px == null) return;
            for (int i = 0; i < _marks.Count; i++)
            {
                var m = _marks[i];
                if (m.At > fraction || m.At < 0f) continue;
                if (_cut[m.I].a < m.C.a || m.C.a >= 200) _cut[m.I] = m.C;
                if (float.IsNaN(_cutAt[m.I])) _cutAt[m.I] = _clock;
                _lastCut = _clock;
                m.At = -1f;
                _marks[i] = m;
            }
            if (done) _done = true;
            Repaint();
        }

        void Update()
        {
            if (_px == null) return;
            _clock += Time.deltaTime;
            Repaint();
            if (_done && _clock >= _lastCut + Tuning.Plow.FurrowHoldSeconds + Tuning.Plow.FurrowFadeSeconds)
                Destroy(gameObject);
        }

        /// <summary>Every cut texel at its own age: whole through the hold, then fading.</summary>
        void Repaint()
        {
            float hold = Tuning.Plow.FurrowHoldSeconds, fade = Tuning.Plow.FurrowFadeSeconds;
            bool changed = false;
            for (int i = 0; i < _px.Length; i++)
            {
                if (float.IsNaN(_cutAt[i])) continue;
                float age = _clock - _cutAt[i];
                var c = _cut[i];
                if (age > hold) c.a = (byte)Mathf.RoundToInt(c.a * Mathf.Clamp01(1f - (age - hold) / fade));
                if (c.a != _px[i].a || c.r != _px[i].r)
                {
                    _px[i] = c;
                    changed = true;
                }
            }
            if (!changed) return;
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
