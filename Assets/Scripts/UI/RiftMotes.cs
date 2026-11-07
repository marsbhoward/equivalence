using UnityEngine;
using UnityEngine.UI;

namespace Convergence.UI
{
    /// <summary>
    /// A few rift flakes drifting up behind a screen - the Rift screen's (the user's call,
    /// 2026-11-06), so the menu reads as standing at the tear rather than in a ledger.
    ///
    /// SPARSE on purpose: a handful of splinters in the tear's cyan and violet, each rising slowly,
    /// swaying, and fading in and out on its own cycle. Behind the grids, never on them - the
    /// screen is for reading.
    ///
    /// UNSCALED time, because the screen holds the pause; and HASHED, never UnityEngine.Random -
    /// looks are hashed from seeds (CLAUDE.md), so each flake's path is its index and its cycle.
    /// </summary>
    public class RiftMotes : MonoBehaviour
    {
        static readonly Color[] Inks =
        {
            new(0.45f, 0.88f, 1f),     // the tear's cyan (RiftShards)
            new(0.72f, 0.60f, 1f),     // the screen's violet
            new(0.90f, 0.96f, 1f),     // a white splinter now and then
        };

        RectTransform _area;
        RectTransform[] _motes;
        Image[] _images;
        float[] _speed;

        public static RiftMotes Add(RectTransform area, int count)
        {
            var go = new GameObject("rift.motes", typeof(RectTransform));
            go.transform.SetParent(area, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var m = go.AddComponent<RiftMotes>();
            m._area = rt;
            m._motes = new RectTransform[count];
            m._images = new Image[count];
            m._speed = new float[count];
            for (int i = 0; i < count; i++)
            {
                var c = new GameObject($"mote{i}", typeof(RectTransform), typeof(Image));
                c.transform.SetParent(rt, false);
                var r = (RectTransform)c.transform;
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                // Splinters, not dots: two texels wide, three to six long, at the menu's density.
                bool longOne = H(i, 1) > 0.5f;
                r.sizeDelta = new Vector2(6f, longOne ? 18f : 9f);
                var img = c.GetComponent<Image>();
                img.raycastTarget = false;
                img.color = Inks[H(i, 2) < 0.15f ? 2 : H(i, 3) < 0.6f ? 0 : 1];
                m._motes[i] = r;
                m._images[i] = img;
                m._speed[i] = Mathf.Lerp(18f, 42f, H(i, 4));   // canvas units a second
            }
            return m;
        }

        void Update()
        {
            if (_motes == null || _area == null) return;
            var rect = _area.rect;
            float t = Time.unscaledTime;

            for (int i = 0; i < _motes.Length; i++)
            {
                if (_motes[i] == null) continue;

                // One rise from the bottom edge to the top is a cycle; each cycle gets a fresh
                // column, so a flake never retraces its own path.
                float span = rect.height + 40f;
                float travel = t * _speed[i] + H(i, 5) * span;
                int cycle = Mathf.FloorToInt(travel / span);
                float k = (travel - cycle * span) / span;   // 0 at the bottom, 1 at the top

                float x = Mathf.Lerp(rect.xMin + 30f, rect.xMax - 30f, H(i * 31 + cycle, 6));
                x += Mathf.Sin(t * (0.4f + H(i, 7) * 0.5f) + i) * 14f;
                float y = Mathf.Lerp(rect.yMin - 20f, rect.yMax + 20f, k);
                _motes[i].anchoredPosition = new Vector2(x, y);
                _motes[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f + i * 1.3f) * 25f);

                // Faint, and faded at both ends of the rise and on its own beat in between.
                float ends = Mathf.Sin(k * Mathf.PI);
                float beat = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * (0.5f + H(i, 8) * 0.6f) + i * 2.1f));
                var c = _images[i].color;
                c.a = 0.5f * ends * beat;
                _images[i].color = c;
            }
        }

        /// <summary>A stable 0..1 from two integers.</summary>
        static float H(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
