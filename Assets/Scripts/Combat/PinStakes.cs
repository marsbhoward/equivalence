using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// PIN's picture (PlayerController.PinStrike): the two arrows that struck stand nailed into the
    /// ground at the body's feet, crossed through its hem, for as long as it is rooted, then fade.
    /// Unparented (the body may die); gone with it. Scaled time.
    /// </summary>
    public class PinStakes : MonoBehaviour
    {
        Health _body;
        SpriteRenderer _a, _b;
        float _seconds, _clock;

        public static void Nail(Health body, Sprite arrow, float seconds)
        {
            if (body == null) return;
            var go = new GameObject("bow.pin");
            var p = go.AddComponent<PinStakes>();
            p._body = body;
            p._seconds = seconds;
            p._a = p.Stake(arrow, -24f, -0.16f);
            p._b = p.Stake(arrow, 24f, 0.16f);
            SmokePuff.At(body.transform.position);
        }

        SpriteRenderer Stake(Sprite arrow, float lean, float side)
        {
            var go = new GameObject("pin.stake");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = arrow;
            float s = Tuning.Player.ArenaVisualScale;
            go.transform.localScale = new Vector3(s, s, 1f);
            float len = arrow != null ? arrow.bounds.size.x * s : 0.4f;
            var ground = (Vector2)_body.transform.position + new Vector2(side, -0.05f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, -90f + lean);
            go.transform.position = (Vector3)(ground + (Vector2)(Quaternion.Euler(0f, 0f, lean) * Vector2.up) * (len * 0.25f));
            // In front of the body: its feet are nailed.
            DepthSorted.Attach(go, -len * 0.25f - 0.08f, true, sr);
            return sr;
        }

        void Update()
        {
            _clock += Time.deltaTime;
            if (_body == null || _body.IsDead || _clock >= _seconds) { Destroy(gameObject); return; }
            float fade = Mathf.Clamp01((_seconds - _clock) / 0.3f);
            var c = new Color(1f, 1f, 1f, fade);
            _a.color = c; _b.color = c;
        }
    }
}
