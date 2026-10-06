using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The pointer a gamepad drives, in whichever of its two shapes currently applies.
    ///
    /// FREE-AIM: the right stick moves Controls.GamepadCursorPosition and this draws a small dot
    /// there. FOCUSED: a D-pad press has landed on one of a screen's own registered clickable
    /// rects (Controls.Focused), and this instead outlines that rect - so navigating a menu reads
    /// as "this card is selected" rather than "there is a dot somewhere near this card".
    ///
    /// Either way, this is the only thing that draws it. Every screen already hit-tests
    /// Controls.Tapped / PointerHeld / PointerPosition the same way for a mouse click or a finger
    /// tap, so a gamepad only had to become a THIRD source feeding that one pipeline (see
    /// Controls.Sync) - nothing here, or there, needed to learn a new input shape.
    ///
    /// Hidden whenever a gamepad isn't the last device touched, the same "last device wins" rule
    /// Controls.TouchMode already lives by - a mouse click or a key press hides this exactly the
    /// way it turns off the on-screen stick.
    /// </summary>
    public class GamepadCursor : MonoBehaviour
    {
        RectTransform _rect;
        Image _image;

        static readonly Vector2 DotSize = new(56f, 56f);
        static readonly Color DotColor = new(1f, 0.85f, 0.4f, 0.9f);
        static readonly Color HighlightColor = new(1f, 0.85f, 0.4f, 0.3f);

        public static GamepadCursor Build(Transform canvas)
        {
            var go = new GameObject("gamepad-cursor", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = DotSize;

            var img = go.AddComponent<Image>();
            img.sprite = Spr.Ring;
            img.color = DotColor;
            img.raycastTarget = false;   // it points AT things, it must never itself be what's hit

            var cursor = go.AddComponent<GamepadCursor>();
            cursor._rect = rt;
            cursor._image = img;
            go.SetActive(false);
            return cursor;
        }

        void Update()
        {
            bool on = Controls.GamepadMode;
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
            if (!on) return;

            var focused = Controls.Focused;
            if (focused != null)
            {
                _image.sprite = Spr.Square;
                _image.color = HighlightColor;
                _rect.position = focused.position;
                // Canvas units, not screen pixels - sizeDelta scales with the CanvasScaler the
                // same way every other UI element in this project already does, so this has to be
                // measured in the focused rect's OWN canvas rather than copied from world corners.
                _rect.sizeDelta = focused.rect.size;
            }
            else
            {
                _image.sprite = Spr.Ring;
                _image.color = DotColor;
                _rect.sizeDelta = DotSize;
                _rect.position = Controls.GamepadCursorPosition;
            }
        }
    }
}
