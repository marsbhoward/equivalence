using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// LONGSHOT's picture (Tuning.Longshot; PlayerController.LongshotStrike lands the hits as it
    /// passes): the bow's arrow flies straight down the line at <see cref="Tuning.Longshot.Speed"/>
    /// with a fine pale streak drawn out behind it from the bow, then sticks quivering where the line
    /// ends - in the wall - while the streak fades from the bow's end first. Scaled time.
    /// </summary>
    public class LongshotStreak : MonoBehaviour
    {
        const float Ppu = 37.5f;
        static Sprite _pixel;

        SpriteRenderer _arrow, _streak;
        Vector2 _from, _to, _dir;
        float _length, _clock, _arrowLen;

        public static LongshotStreak Fly(Vector2 from, Vector2 to, Sprite arrow)
        {
            if (_pixel == null)
            {
                var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            }
            var go = new GameObject("bow.longshot");
            var s = go.AddComponent<LongshotStreak>();
            s._from = from;
            s._to = to;
            var d = to - from;
            s._length = d.magnitude;
            s._dir = s._length > 1e-4f ? d / s._length : Vector2.right;
            float angle = Mathf.Atan2(s._dir.y, s._dir.x) * Mathf.Rad2Deg;

            var st = new GameObject("streak");
            st.transform.SetParent(go.transform, false);
            s._streak = st.AddComponent<SpriteRenderer>();
            s._streak.sprite = _pixel;
            s._streak.sortingOrder = SortingOrders.Fx - 3;
            st.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var ar = new GameObject("arrow");
            ar.transform.SetParent(go.transform, false);
            s._arrow = ar.AddComponent<SpriteRenderer>();
            s._arrow.sprite = arrow;
            s._arrow.sortingOrder = SortingOrders.Fx - 2;
            float sc = Tuning.Player.ArenaVisualScale;
            ar.transform.localScale = new Vector3(sc, sc, 1f);
            ar.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            s._arrowLen = arrow != null ? arrow.bounds.size.x * sc : 0.4f;
            s.Place();
            return s;
        }

        void Update()
        {
            _clock += Time.deltaTime;
            Place();
            float flight = _length / Tuning.Longshot.Speed;
            if (_clock > flight + Mathf.Max(Tuning.Longshot.StuckSeconds, Tuning.Longshot.StreakFadeSeconds))
                Destroy(gameObject);
        }

        void Place()
        {
            float flight = _length / Tuning.Longshot.Speed;
            float head = Mathf.Min(_length, _clock * Tuning.Longshot.Speed);
            // Stuck: the head a quarter into the wall, a brief quiver.
            float after = _clock - flight;
            float quiver = after > 0f ? Mathf.Sin(after * 60f) * 2.5f * Mathf.Exp(-after * 8f) : 0f;
            var headAt = _from + _dir * (head + (after > 0f ? _arrowLen * 0.25f : 0f));
            _arrow.transform.position = headAt - _dir * (_arrowLen * 0.5f);
            _arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg + quiver);
            float stuckFade = after > 0f ? 1f - Mathf.Clamp01((after - Tuning.Longshot.StuckSeconds * 0.6f) / (Tuning.Longshot.StuckSeconds * 0.4f)) : 1f;
            _arrow.color = new Color(1f, 1f, 1f, stuckFade);

            // The streak: from the bow to the arrow's tail, its bow end fading first.
            float tail = Mathf.Max(0f, head - _arrowLen);
            float fade = after > 0f ? Mathf.Clamp01(after / Tuning.Longshot.StreakFadeSeconds) : 0f;
            float start = tail * fade;
            var t = _streak.transform;
            t.position = _from + _dir * start;
            t.localScale = new Vector3(Mathf.Max(0f, tail - start), 1.5f / Ppu, 1f);
            _streak.color = new Color(1f, 0.97f, 0.88f, 0.75f * (1f - fade));
        }
    }
}
