using UnityEngine;
using UnityEngine.UI;

namespace Convergence.UI
{
    /// <summary>
    /// Minimal code-built uGUI helpers. Legacy Text is used deliberately over TextMeshPro:
    /// TMP needs its Essential Resources imported into Assets before it renders, which would
    /// make the scene un-rebuildable from a clean checkout via the CLI.
    /// </summary>
    public static class UiKit
    {
        /// <summary>
        /// The smallest a tap target may be, in CANVAS units.
        ///
        /// Apple asks for 44pt and Android for 48dp; this canvas scales to a 1920x1080 reference
        /// with match 0.5, so a canvas unit is worth a different number of points on every
        /// device. Worked out for the ones that matter, in landscape:
        ///
        ///     device                scaleFactor   1 unit     44pt
        ///     iPhone 14 Pro             1.206     0.402pt    109u
        ///     iPhone SE (3rd gen)       0.695     0.347pt    127u
        ///     Pixel 8                   1.118     0.426pt    103u
        ///     iPad Pro 11               1.386     0.693pt     63u
        ///
        /// 128 covers the worst of them - the small phone, where a canvas unit buys the least -
        /// so anything sized to this is at least a 44pt target everywhere and comfortably more on
        /// a tablet. It is the SMALLEST dimension that matters: a 300x40 button is a 40-unit
        /// target no matter how wide it is.
        /// </summary>
        public const float TouchTarget = 128f;

        static Font _font;
        public static Font Font => _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static Canvas CreateCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Panel(Transform parent, Vector2 anchorMin, Vector2 anchorMax,
                                          Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject("panel", typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return rt;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var go = new GameObject("label", typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return t;
        }

        /// <summary>
        /// A row of <paramref name="max"/> star icons centred in <paramref name="parent"/>, the
        /// first <paramref name="lit"/> filled and the rest dim - the Forge's star level. Stars are
        /// sprites, not text (see Spr.Star for why).
        /// </summary>
        public static void Stars(Transform parent, int lit, int max, float size, Color on, Color off)
        {
            float total = max * size;
            for (int i = 0; i < max; i++)
            {
                var go = new GameObject("star", typeof(Image));
                go.transform.SetParent(parent, false);
                var img = go.GetComponent<Image>();
                img.sprite = Core.Spr.Star();
                img.color = i < lit ? on : off;
                img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(-total * 0.5f + size * (i + 0.5f), 0f);
            }
        }

        /// <summary>A background track plus a left-anchored fill image. Returns the fill.</summary>
        public static Image Bar(Transform parent, Color fillColor, Color trackColor)
        {
            var track = new GameObject("bar", typeof(Image));
            track.transform.SetParent(parent, false);
            var trt = (RectTransform)track.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            track.GetComponent<Image>().color = trackColor;

            var fillGo = new GameObject("fill", typeof(Image));
            fillGo.transform.SetParent(track.transform, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 0.5f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;

            var fill = fillGo.GetComponent<Image>();
            fill.color = fillColor;
            return fill;
        }

        /// <summary>
        /// THESE THREE ALL EXIST TO NOT WRITE.
        ///
        /// uGUI has no concept of an assignment that changes nothing: every write to a Graphic's
        /// text or colour, and every anchor write on a RectTransform, marks the canvas dirty and
        /// buys a rebuild of its whole batch at the end of the frame. The Hud sets all three on
        /// something like a dozen elements every frame, and the overwhelming majority of those
        /// frames set a value identical to the one already there - health does not change most
        /// frames, and the attack label changes a few times a chain.
        ///
        /// So the guard is the entire point, and it is a READ rather than a cached copy: reading
        /// .text/.color/.anchorMax is free and dirties nothing, which means no new fields, nothing
        /// to keep in sync, and nothing for a domain reload to empty. Comparing against the live
        /// value cannot go stale by construction - a cache can.
        ///
        /// This does NOT save building the string - an interpolated argument is allocated before
        /// the call. Where that matters the caller guards its INPUTS too; see Hud.Update.
        /// </summary>
        public static void SetFill(Image fill, float t)
        {
            if (fill == null) return;
            var rt = (RectTransform)fill.transform;
            var want = new Vector2(Mathf.Clamp01(t), 1f);
            if (rt.anchorMax == want && rt.offsetMax == Vector2.zero && rt.offsetMin == Vector2.zero)
                return;
            rt.anchorMax = want;
            rt.offsetMax = Vector2.zero;
            rt.offsetMin = Vector2.zero;
        }

        /// <summary>Assigns only if it would change something. See SetFill.</summary>
        public static void SetText(Text label, string value)
        {
            if (label == null || label.text == value) return;
            label.text = value;
        }

        /// <summary>Assigns only if it would change something. See SetFill.</summary>
        public static void SetColor(Graphic graphic, Color value)
        {
            if (graphic == null || graphic.color == value) return;
            graphic.color = value;
        }

        /// <summary>Assigns only if it would change something. See SetFill.</summary>
        public static void SetSprite(Image image, Sprite value)
        {
            if (image == null || image.sprite == value) return;
            image.sprite = value;
        }

        /// <summary>Toggles only if it would change something. See SetFill.</summary>
        public static void SetEnabled(Graphic graphic, bool value)
        {
            if (graphic == null || graphic.enabled == value) return;
            graphic.enabled = value;
        }

        /// <summary>
        /// A hint line that says the right thing for the device in the player's hands.
        ///
        /// Every one of these was written as "[ESC] to close" and built ONCE, at construction -
        /// which on a phone is an instruction to press a key that does not exist. Swapping at
        /// build time would not have been enough either: Controls picks its mode from the last
        /// device actually used, so a touchscreen laptop can change its mind mid-session.
        ///
        /// So the text follows the mode, and it costs one string comparison per frame per hint.
        /// </summary>
        /// <param name="gamepad">
        /// Optional third variant. Trails the signature rather than sitting beside touch so the
        /// existing positional callers keep working; null falls back to the touch string, which is
        /// already written device-neutrally. See <see cref="HintSwap.Gamepad"/>.
        /// </param>
        public static Text Hint(Transform parent, string keyboard, string touch, int size,
                                Color color, TextAnchor anchor = TextAnchor.MiddleCenter,
                                string gamepad = null)
        {
            var label = Label(parent, keyboard, size, color, anchor);
            var swap = label.gameObject.AddComponent<HintSwap>();
            swap.Keyboard = keyboard;
            swap.Touch = touch;
            swap.Gamepad = gamepad;
            return label;
        }

        public static RectTransform Rect(Transform parent, string name,
                                         Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }
    }
}
