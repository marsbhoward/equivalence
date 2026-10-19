using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// PIERCING RAIN's picture (Tuning.PiercingRain; PlayerController.PiercingRainStrike lands the
    /// hits on the same clock): the loosed arrow climbs straight up out of the bow and out of sight;
    /// a faint ring marks the ground; then the bow's arrows fall into the circle at hashed points,
    /// each a short steep drop, sticking a moment and fading, a puff where it strikes. Scaled time.
    /// </summary>
    public class ArrowRain : MonoBehaviour
    {
        class Drop
        {
            public SpriteRenderer Sr;
            public Vector2 Ground;
            public float At, Lean;
        }

        Sprite _arrow;
        SpriteRenderer _up, _ring;
        Vector2 _from, _centre;
        float _radius, _clock;
        List<Drop> _drops = new();   // non-readonly - domain reload

        public static ArrowRain Play(Vector2 from, Vector2 centre, float radius, Sprite arrow)
        {
            var go = new GameObject("bow.rain");
            var r = go.AddComponent<ArrowRain>();
            r._arrow = arrow;
            r._from = from;
            r._centre = centre;
            r._radius = radius;
            float s = Tuning.Player.ArenaVisualScale;

            r._up = r.Renderer("rain.up", SortingOrders.Fx - 2);
            r._up.transform.localScale = new Vector3(s, s, 1f);
            r._up.transform.rotation = Quaternion.Euler(0f, 0f, 90f);

            r._ring = r.Renderer("rain.ring", SortingOrders.GroundDecal + 3);
            r._ring.sprite = Spr.ThinRing;
            r._ring.color = new Color(1f, 1f, 1f, 0f);
            r._ring.transform.position = centre;
            float ringSize = Spr.ThinRing != null ? Spr.ThinRing.bounds.size.x : 1f;
            r._ring.transform.localScale = Vector3.one * (radius * 2f / Mathf.Max(0.01f, ringSize));

            // The drops: spread evenly in time over the rain, at points spread over the disc
            // (a sunflower spiral, turned by a random start so no two rains match).
            int n = Tuning.PiercingRain.Arrows;
            float turn = Random.value * 360f;
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n;
                float a = (turn + i * 137.508f) * Mathf.Deg2Rad;
                var p = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * Mathf.Sqrt(u) * 0.95f;
                var sr = r.Renderer("rain.arrow", SortingOrders.Fx - 2);
                sr.enabled = false;
                sr.transform.localScale = new Vector3(s, s, 1f);
                float at = Tuning.PiercingRain.RiseSeconds + Tuning.PiercingRain.RainSeconds * Random.value;
                r._drops.Add(new Drop { Sr = sr, Ground = p, At = at, Lean = Random.Range(-12f, 12f) });
            }
            return r;
        }

        SpriteRenderer Renderer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _arrow;
            sr.sortingOrder = order;
            return sr;
        }

        void Update()
        {
            _clock += Time.deltaTime;
            var T = (Rise: Tuning.PiercingRain.RiseSeconds, Rain: Tuning.PiercingRain.RainSeconds,
                     Fall: Tuning.PiercingRain.FallSeconds, Stick: Tuning.PiercingRain.StickSeconds);

            // Up out of the bow and out of sight.
            if (_up != null)
            {
                float u = _clock / T.Rise;
                if (u >= 1f) { Destroy(_up.gameObject); _up = null; }
                else
                {
                    _up.transform.position = _from + Vector2.up * (u * u * 6f);
                    _up.color = new Color(1f, 1f, 1f, 1f - u * 0.5f);
                }
            }

            // The ring: in as the arrow climbs, held through the rain, out after.
            float end = T.Rise + T.Rain + T.Fall;
            float ringA = _clock < T.Rise ? _clock / T.Rise
                        : _clock < end ? 1f : 1f - (_clock - end) / T.Stick;
            _ring.color = new Color(1f, 0.92f, 0.8f, 0.45f * Mathf.Clamp01(ringA));

            float len = _arrow != null ? _arrow.bounds.size.x * Tuning.Player.ArenaVisualScale : 0.4f;
            foreach (var d in _drops)
            {
                if (d.Sr == null) continue;
                float t = _clock - d.At;
                if (t < 0f) continue;
                d.Sr.enabled = true;
                var rot = Quaternion.Euler(0f, 0f, -90f + d.Lean);
                var down = (Vector2)(rot * Vector2.right);   // the head's direction
                if (t < T.Fall)
                {
                    // A steep drop onto the ground point, head first.
                    float u = t / T.Fall;
                    var head = d.Ground - down * (Tuning.PiercingRain.DropHeight * (1f - u * u));
                    d.Sr.transform.SetPositionAndRotation(head - down * (len * 0.5f), rot);
                    d.Sr.color = new Color(1f, 1f, 1f, Mathf.Clamp01(u * 3f));
                    continue;
                }
                if (t - T.Fall < Time.deltaTime + 1e-4f) SmokePuff.At(d.Ground);
                // Stuck a third deep, then gone.
                d.Sr.transform.SetPositionAndRotation(d.Ground - down * (len * 0.2f), rot);
                float f = (t - T.Fall) / T.Stick;
                if (f >= 1f) { Destroy(d.Sr.gameObject); d.Sr = null; continue; }
                d.Sr.color = new Color(1f, 1f, 1f, 1f - f * f);
            }

            if (_clock > end + T.Stick) Destroy(gameObject);
        }
    }
}
