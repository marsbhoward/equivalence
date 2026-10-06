using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The mid-run pause menu, drawn as a radial: a circle at the centre that RESUMES, sitting
    /// inside a triangle whose three points are CHARACTER, INVENTORY and END RUN.
    ///
    /// It is the single "I want to stop and look" gesture for a live run. The loadout sheet and
    /// the carried-loot screen were each reached by their own key before, and abandoning was a
    /// bare Esc; routing all three through one paused menu means the destructive one sits beside
    /// the other two rather than on a keystroke by itself, and there is one thing to learn instead
    /// of three.
    ///
    /// IT DOES NOT NEST. Picking a point CLOSES this menu and opens that screen; backing out of
    /// that screen calls straight back here and this menu is rebuilt fresh. Only ever one screen
    /// up at a time, which is the model every other modal in this project already keeps -
    /// GameBootstrap owns the wiring that returns here.
    /// </summary>
    public class PauseScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;

        // Esc / the loadout key opens this, and wasPressedThisFrame stays true for the whole
        // frame - so without skipping the opening frame the menu would read its own opening
        // keystroke as a resume and vanish before it was ever drawn. Same guard as ConfirmDialog.
        int _openedFrame = -1;

        System.Action _onCharacter, _onInventory, _onEndRun;
        RectTransform _circleRect, _charRect, _invRect, _endRect;
        readonly List<RectTransform> _focus = new();

        public void Open(Transform canvas, System.Action onCharacter,
                         System.Action onInventory, System.Action onEndRun)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _onCharacter = onCharacter;
            _onInventory = onInventory;
            _onEndRun = onEndRun;

            GamePause.Hold(this);
            Build(canvas);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _circleRect = _charRect = _invRect = _endRect = null;
            _onCharacter = _onInventory = _onEndRun = null;
        }

        // ------------------------------------------------------------------ ui

        void Build(Transform canvas)
        {
            _root = new GameObject("PauseScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.02f, 0.02f, 0.035f, 0.88f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -96), new Vector2(0, -44)),
                "PAUSED", 40, new Color(0.90f, 0.91f, 0.96f), TextAnchor.MiddleCenter);

            // Triangle points: up / lower-left / lower-right on a circle around the centre. The
            // radius clears the middle disc and leaves each vertex button a finger of gap from
            // the next one. cos30 = 0.8660254.
            const float R = 330f;
            Vector2 pTop = new(0f, R);
            Vector2 pLL = new(-R * 0.8660254f, -R * 0.5f);
            Vector2 pLR = new(R * 0.8660254f, -R * 0.5f);

            // Drawn first, so the vertex buttons sit on top where the edges meet them.
            Edge(full, pTop, pLL);
            Edge(full, pLL, pLR);
            Edge(full, pLR, pTop);

            _circleRect = Disc(full, "resume", 224f, "RESUME",
                new Color(0.10f, 0.12f, 0.17f, 1f), new Color(0.64f, 0.68f, 0.80f));

            _charRect = Vertex(full, pTop, "CHARACTER",
                new Color(0.10f, 0.12f, 0.17f, 1f), new Color(0.86f, 0.89f, 0.96f));
            _invRect = Vertex(full, pLL, "INVENTORY",
                new Color(0.10f, 0.12f, 0.17f, 1f), new Color(0.86f, 0.89f, 0.96f));
            _endRect = Vertex(full, pLR, "END RUN",
                new Color(0.24f, 0.13f, 0.13f, 1f), new Color(0.96f, 0.66f, 0.60f));

            UiKit.Hint(UiKit.Rect(full, "h", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 40), new Vector2(0, 82)),
                "[ESC] or the circle to resume    -    game is paused",
                "tap the circle to resume    -    game is paused",
                17, new Color(0.5f, 0.53f, 0.62f), TextAnchor.MiddleCenter,
                GamepadGlyphs.Cancel + " or the circle to resume    -    game is paused");
        }

        /// <summary>A fixed-size rect centred on <paramref name="c"/> in the full-screen root's
        /// own space, so a triangle point is a plain (x, y) offset from the middle.</summary>
        static RectTransform Centered(RectTransform parent, string name, Vector2 c, Vector2 size, Color color)
        {
            var rt = UiKit.Panel(parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(c.x - size.x * 0.5f, c.y - size.y * 0.5f),
                new Vector2(c.x + size.x * 0.5f, c.y + size.y * 0.5f), color);
            rt.name = name;
            return rt;
        }

        RectTransform Vertex(RectTransform parent, Vector2 c, string label, Color bg, Color fg)
        {
            var rt = Centered(parent, "vtx", c, new Vector2(268f, UiKit.TouchTarget), bg);
            UiKit.Label(rt, label, 22, fg, TextAnchor.MiddleCenter);
            return rt;
        }

        RectTransform Disc(RectTransform parent, string name, float d, string label, Color bg, Color fg)
        {
            var rt = Centered(parent, name, Vector2.zero, new Vector2(d, d), bg);
            rt.GetComponent<Image>().sprite = Spr.Circle;
            UiKit.Label(rt, label, 24, fg, TextAnchor.MiddleCenter);
            return rt;
        }

        /// <summary>One side of the triangle: a thin bar spun to run from a to b. Pivot is the
        /// rect centre, and the rect is centred on the edge's midpoint, so the rotation lands it
        /// exactly along the edge.</summary>
        static void Edge(RectTransform parent, Vector2 a, Vector2 b)
        {
            Vector2 mid = (a + b) * 0.5f;
            float len = (b - a).magnitude;
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            var rt = Centered(parent, "edge", mid, new Vector2(len, 4f),
                new Color(0.34f, 0.37f, 0.48f, 0.55f));
            rt.localEulerAngles = new Vector3(0f, 0f, ang);
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;
            if (Time.frameCount == _openedFrame) return;

            _focus.Clear();
            _focus.Add(_charRect); _focus.Add(_invRect); _focus.Add(_endRect); _focus.Add(_circleRect);
            Core.Controls.SetFocusCandidates(_focus);

            if (Core.Controls.CancelTapped) { Close(); return; }

            if (!Core.Controls.Tapped(out var p)) return;
            if (Hit(_circleRect, p)) { Close(); return; }
            if (Hit(_charRect, p)) { var cb = _onCharacter; cb?.Invoke(); return; }
            if (Hit(_invRect, p)) { var cb = _onInventory; cb?.Invoke(); return; }
            if (Hit(_endRect, p)) { var cb = _onEndRun; cb?.Invoke(); return; }
        }

        static bool Hit(RectTransform rt, Vector2 point)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, point, null);
    }
}
