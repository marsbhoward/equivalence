using System;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A drawn arrow flying a straight leg and nothing more - SPLIT's first leg, up to where it
    /// breaks into three (PlayerController.SplitStrike). Calls <c>onArrive</c> there, with a small
    /// flash, and is gone. Scaled time.
    /// </summary>
    public class BowFlight : MonoBehaviour
    {
        SpriteRenderer _sr;
        Vector2 _from, _to;
        float _seconds, _clock;
        Action<Vector2> _onArrive;

        public static void Fly(Vector2 from, Vector2 to, float speed, Sprite arrow, Action<Vector2> onArrive)
        {
            var go = new GameObject("bow.flight");
            var f = go.AddComponent<BowFlight>();
            f._from = from; f._to = to;
            f._seconds = Mathf.Max(0.01f, Vector2.Distance(from, to) / Mathf.Max(0.1f, speed));
            f._onArrive = onArrive;
            f._sr = go.AddComponent<SpriteRenderer>();
            f._sr.sprite = arrow;
            f._sr.sortingOrder = SortingOrders.Fx - 2;
            float s = Tuning.Player.ArenaVisualScale;
            go.transform.localScale = new Vector3(s, s, 1f);
            var d = to - from;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            go.transform.position = from;
        }

        void Update()
        {
            _clock += Time.deltaTime;
            float t = Mathf.Clamp01(_clock / _seconds);
            transform.position = Vector2.Lerp(_from, _to, t);
            if (t < 1f) return;
            TexelFx.Star(_to, TexelFx.Steel, 2, 0.1f);
            _onArrive?.Invoke(_to);
            Destroy(gameObject);
        }
    }
}
