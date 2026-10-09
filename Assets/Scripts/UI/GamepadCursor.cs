using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The gamepad's menu highlight: an outline over the rect the D-pad has focused
    /// (Controls.Focused). Menus are D-pad only - there is no free pointer and no dot; a press of A
    /// clicks the focused rect through the ordinary pointer path (see Controls.Sync).
    ///
    /// Shown only while a gamepad is the last device touched and a screen has something focused,
    /// the same "last device wins" rule Controls.TouchMode lives by.
    /// </summary>
    public class GamepadCursor : MonoBehaviour
    {
        RectTransform _rect;
        Image _image;

        static readonly Color HighlightColor = new(1f, 0.85f, 0.4f, 0.3f);

        public static GamepadCursor Build(Transform canvas)
        {
            var go = new GameObject("gamepad-cursor", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var img = go.AddComponent<Image>();
            img.sprite = Spr.Square;
            img.color = HighlightColor;
            img.raycastTarget = false;   // it marks things, it must never itself be what's hit

            var cursor = go.AddComponent<GamepadCursor>();
            cursor._rect = rt;
            cursor._image = img;
            // The GameObject stays ACTIVE and only the image is switched. This used to
            // SetActive(false) here and switch itself back on in Update - which Unity never calls
            // on an inactive object, so the highlight was never drawn and the
            // D-pad looked dead even while it was moving focus perfectly well.
            img.enabled = false;
            return cursor;
        }

        static readonly Vector3[] Corners = new Vector3[4];

        // LateUpdate, so a screen that pans or scrolls the focused rect this frame is drawn where
        // it ended up.
        void LateUpdate()
        {
            var focused = Controls.Focused;
            bool on = focused != null;
            if (_image.enabled != on) _image.enabled = on;
            if (!on) return;

            // Measured off WORLD corners and converted into this canvas's units, not copied
            // from the rect's own size: a mastery node is drawn scaled by the board's zoom, and
            // its sizeDelta knows nothing about that.
            focused.GetWorldCorners(Corners);
            var parentScale = _rect.parent != null ? _rect.parent.lossyScale : Vector3.one;
            _rect.position = (Corners[0] + Corners[2]) * 0.5f;
            _rect.sizeDelta = new Vector2((Corners[2].x - Corners[0].x) / Mathf.Max(0.0001f, parentScale.x),
                                          (Corners[2].y - Corners[0].y) / Mathf.Max(0.0001f, parentScale.y));
        }
    }
}
