using System.Runtime.InteropServices;
using UnityEngine;

namespace Convergence.Hub
{
    /// <summary>
    /// A DONADA piece's LIVE page laid over the canvas - the web build's close-up view, and nowhere
    /// else (the user's call, 2026-10-31: the hub itself never runs a page; its frames hold the
    /// game-drawn card, NftCard). HubRoom.OpenInspect is the only caller of <see cref="Show"/>, and
    /// its CloseInspect - which Teardown runs too - the only caller of <see cref="Hide"/>, so leaving
    /// the view or the hub always takes the page down.
    ///
    /// The iframe is sandboxed and the page is served sandboxed with no network
    /// (Plugins/WebGL/EquivalenceNftFrame.jslib, web/service/src/routes/nft.ts). Native builds and
    /// the Editor have no page to show: <see cref="Available"/> is false and the close-up shows the
    /// drawn card.
    /// </summary>
    public static class NftFrame
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void EquivalenceNftShow(string url, float x, float y, float w, float h);
        [DllImport("__Internal")] static extern void EquivalenceNftHide();
        public static bool Available => true;
#else
        static void EquivalenceNftShow(string url, float x, float y, float w, float h) { }
        static void EquivalenceNftHide() { }
        public static bool Available => false;
#endif

        public static bool Showing { get; private set; }

        /// <summary>Over a screen-space rect (pixels, bottom-left origin).</summary>
        public static void Show(string url, Rect screen)
        {
            if (!Available || string.IsNullOrEmpty(url) || Screen.width <= 0 || Screen.height <= 0) return;
            EquivalenceNftShow(url, screen.x / Screen.width, screen.y / Screen.height,
                               screen.width / Screen.width, screen.height / Screen.height);
            Showing = true;
        }

        public static void Hide()
        {
            if (!Showing) return;
            Showing = false;
            EquivalenceNftHide();
        }
    }
}
