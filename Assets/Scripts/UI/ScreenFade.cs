using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Convergence.UI
{
    /// <summary>
    /// A full-screen black overlay for scene-style transitions - stepping through a floor door,
    /// or through the hub's own sigil door into a run. Its own canvas, ABOVE every screen and the
    /// gamepad cursor (sort order 50 against their 8-20 - see UiKit's other callers), because a
    /// transition has to cover whatever happens to be on screen at the moment it fires, whatever
    /// that turns out to be.
    ///
    /// Not raycast-blocking. Nothing in this project's input layer goes through EventSystem -
    /// every screen and every touch control hit-tests Mouse.current/Controls itself (see the
    /// modal-input notes) - so a raycast-blocking panel here would cost a GraphicRaycaster for
    /// no actual protection. The fades that use this are timed to cover the moment they need to
    /// hide (see GameBootstrap's own callers) rather than relying on this to freeze anything.
    /// Runs on UNSCALED time deliberately - a caller covering a transition with GamePause (so
    /// enemies/physics can't act while the screen is black) would otherwise freeze the fade
    /// itself right along with them, since Time.timeScale = 0 zeroes Time.deltaTime too.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        Image _panel;

        public static ScreenFade Build(Transform parent)
        {
            var canvas = UiKit.CreateCanvas("screen-fade", 50);
            canvas.transform.SetParent(parent, false);

            var fade = canvas.gameObject.AddComponent<ScreenFade>();
            var rt = UiKit.Panel(canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                                 new Color(0f, 0f, 0f, 0f));
            fade._panel = rt.GetComponent<Image>();
            fade._panel.raycastTarget = false;
            return fade;
        }

        public IEnumerator FadeOut(float seconds) => Fade(1f, seconds);
        public IEnumerator FadeIn(float seconds) => Fade(0f, seconds);

        /// <summary>
        /// Back to fully visible THIS FRAME, with no fade.
        ///
        /// For a caller unwinding from something that went wrong, where a black screen is the
        /// failure the player is actually looking at - snapping back is ugly, and a permanent
        /// black screen is the end of the session. Never part of a normal transition, which
        /// always has a FadeIn to run.
        /// </summary>
        public void Clear()
        {
            if (_panel != null) SetAlpha(0f);
        }

        IEnumerator Fade(float target, float seconds)
        {
            if (_panel == null) yield break;
            float from = _panel.color.a;
            if (seconds <= 0f) { SetAlpha(target); yield break; }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, target, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            SetAlpha(target);
        }

        void SetAlpha(float a)
        {
            var c = _panel.color;
            c.a = a;
            _panel.color = c;
        }
    }
}
