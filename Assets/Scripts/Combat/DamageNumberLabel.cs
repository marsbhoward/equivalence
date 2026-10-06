using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Drives one floating damage number: rises in WORLD space and is reprojected onto its
    /// Screen Space - Overlay canvas every frame, because an overlay canvas has no world
    /// position of its own to parent onto - the label has to be told where it is in screen terms
    /// every frame it moves, the same reprojection every world-anchored overlay element in this
    /// project needs. Fades out over its life and destroys itself; nothing outside DamageNumbers
    /// holds a reference, so a torn-down run simply stops spawning new ones and lets the rest
    /// finish fading (or Teardown clears the canvas outright).
    /// </summary>
    public class DamageNumberLabel : MonoBehaviour
    {
        Vector3 _worldPos;
        float _t;
        bool _crit;
        Text _text;
        RectTransform _rect;
        RectTransform _canvasRect;
        Color _base;

        public void Begin(Vector3 worldPos, bool crit = false)
        {
            _worldPos = worldPos;
            _crit = crit;
            _text = GetComponent<Text>();
            _rect = (RectTransform)transform;
            _canvasRect = (RectTransform)transform.parent;
            _base = _text.color;
            _rect.localScale = _crit ? Vector3.one * Tuning.DamageNumbers.CritPopScale : Vector3.one;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float life = Tuning.DamageNumbers.LifeSeconds;
            if (_t >= life) { Destroy(gameObject); return; }

            float k = _t / life;

            var cam = Camera.main;
            if (cam == null) return;

            var worldNow = _worldPos + Vector3.up * (Tuning.DamageNumbers.RiseDistance * k);
            var screen = cam.WorldToScreenPoint(worldNow);
            if (screen.z < 0f) { Destroy(gameObject); return; }   // behind the camera - never here, but cheap to guard

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local))
                _rect.anchoredPosition = local;

            // A crit spawns oversized and eases back to 1x - the motion is what reads as "pop"
            // rather than just "bigger font," separate from the fade timer above.
            if (_crit && _t < Tuning.DamageNumbers.CritPopSeconds)
            {
                float pk = _t / Tuning.DamageNumbers.CritPopSeconds;
                float scale = Mathf.Lerp(Tuning.DamageNumbers.CritPopScale, 1f, pk * pk);
                _rect.localScale = Vector3.one * scale;
            }
            else if (_crit)
            {
                _rect.localScale = Vector3.one;
            }

            // Holds near full opacity through most of the life and only falls away at the end,
            // rather than a linear fade - a number that starts dimming the instant it appears
            // reads as weak feedback for the hit that just landed.
            var c = _base;
            c.a = Mathf.Clamp01(1f - Mathf.Pow(k, 3f));
            _text.color = c;
        }
    }
}
