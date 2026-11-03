using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using TS = Convergence.Core.Tuning.Showcase;

namespace Convergence.UI
{
    /// <summary>
    /// SUPPORT (the user's picks, 2026-11-03, mock https://claude.ai/artifact/JxjD9TdFYMhkGZkaGtu47N):
    /// one card, two ways in. From the title's corner plate it is the SUPPORT panel - "made by one
    /// person", SHARE THE GAME first, then the tips (Cash App, Venmo, ADA) and the Discord. In the hub
    /// it is the THANK-YOU after an escape - "you made it out", the run's line, SHARE THIS RUN, then
    /// the tips - opened by the sealed letter on the table (a character's first escape) or on arrival
    /// (the week's first showcase ending). Sharing opens a phone's share sheet, or copies the text.
    /// It never grants anything: a tip is a gift, never a purchase.
    /// </summary>
    public class SupportCard : MonoBehaviour
    {
        public static bool IsOpen => _open != null;
        /// <summary>The frame it closed on - the screen under it ignores that frame's tap.</summary>
        public static int ClosedFrame { get; private set; } = -1;

        static SupportCard _open;

        /// <summary>An escape, for the thank-you's line and the run it shares.</summary>
        public struct Escape
        {
            public int Floor, Secured;
            public float Seconds;
            public string Element, Weapon;   // "Water", WeaponClass name
        }

        GameObject _root;
        Action _closed;
        int _openedFrame;
        Text _toast;
        float _toastUntil;
        readonly List<(RectTransform Rect, Action Act)> _hits = new();
        readonly List<RectTransform> _focus = new();

        static readonly Color Ink = new(0.93f, 0.92f, 0.89f);
        static readonly Color Dim = new(0.66f, 0.68f, 0.74f);
        static readonly Color Gold = new(0.95f, 0.8f, 0.42f);
        static readonly Color Rim = new(0.29f, 0.25f, 0.13f);
        static readonly Color Face = new(0.07f, 0.078f, 0.105f, 1f);
        static readonly Color TipFace = new(0.17f, 0.145f, 0.086f);
        static readonly Color ShareFace = new(0.09f, 0.15f, 0.23f);
        static readonly Color ShareInk = new(0.74f, 0.85f, 1f);

        /// <summary>Open the card: the SUPPORT panel with <paramref name="run"/> null, else the thank-you for it.</summary>
        public static void Open(Transform canvas, Escape? run, Action onClosed = null)
        {
            if (canvas == null) return;
            if (_open != null) _open.Close();
            var go = new GameObject("SupportCard");
            var card = go.AddComponent<SupportCard>();
            card.Build(canvas, run);
            card._closed = onClosed;
        }

        /// <summary>What a shared escape says.</summary>
        public static string RunText(Escape e)
        {
            int s = Mathf.FloorToInt(e.Seconds);
            string with = e.Weapon switch
            {
                "Disc" => $"{e.Element} discs",
                "Bow" => $"a {e.Element} bow",
                _ => $"a {e.Element} greatsword",
            };
            return $"I made it out of Equivalence on floor {e.Floor} in {s / 60}:{s % 60:00} with {with}. {TS.ShareUrl}";
        }

        void Build(Transform canvas, Escape? run)
        {
            _open = this;
            _openedFrame = Time.frameCount;
            GamePause.Hold(this);

            _root = new GameObject("Support", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;
            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.016f, 0.02f, 0.035f, 0.74f));

            // The tips that are filled in (Tuning.Showcase), one button each.
            var tips = new List<(string Label, string Value, Action Act)>();
            if (!string.IsNullOrEmpty(TS.CashAppTag))
            {
                var tag = TS.CashAppTag.TrimStart('$');
                tips.Add(("CASH APP", "$" + tag, () => { Chain.Service.Telemetry.Card("cashapp"); Application.OpenURL("https://cash.app/$" + tag); Say("CASH APP OPENED IN A NEW TAB"); }));
            }
            if (!string.IsNullOrEmpty(TS.VenmoUser))
            {
                var user = TS.VenmoUser.TrimStart('@');
                tips.Add(("VENMO", "@" + user, () => { Chain.Service.Telemetry.Card("venmo"); Application.OpenURL("https://venmo.com/u/" + user); Say("VENMO OPENED IN A NEW TAB"); }));
            }
            if (!string.IsNullOrEmpty(TS.AdaAddress))
                tips.Add(("ADA", TS.AdaAddress, () => { Chain.Service.Telemetry.Card("ada"); Copy(TS.AdaAddress, $"{TS.AdaAddress} COPIED - PASTE IT IN YOUR WALLET"); }));

            bool thanks = run.HasValue;
            bool discord = !thanks && !string.IsNullOrEmpty(TS.DiscordUrl);
            const float W = 780f, P = 36f, G = 14f;
            float h = P + 44f + (thanks ? 34f : 0f) + 58f + G + 64f + (tips.Count > 0 ? G + 76f : 0f) + (discord ? G + 56f : 0f) + 10f + 28f + P;

            var mid = new Vector2(0.5f, 0.5f);
            var rim = UiKit.Panel(full, mid, mid, new Vector2(-W * 0.5f - 3f, -h * 0.5f - 3f), new Vector2(W * 0.5f + 3f, h * 0.5f + 3f), Rim);
            var card = UiKit.Panel(rim, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f), Face);

            float y = P;
            RectTransform Row(float rh)
            {
                var r = UiKit.Rect(card, "row", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(P, -y - rh), new Vector2(-P, -y));
                y += rh;
                return r;
            }

            // The way out, top right: tappable, and [ESC] / B as well.
            var close = UiKit.Rect(card, "close", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-230f, -54f), new Vector2(-18f, -12f));
            UiKit.Label(close, thanks ? "CONTINUE" : "CLOSE", 17, Dim, TextAnchor.MiddleRight);
            _hits.Add((close, Close));

            UiKit.Label(Row(44f), thanks ? "YOU MADE IT OUT" : "MADE BY ONE PERSON", 34, thanks ? Gold : Ink);
            if (thanks)
            {
                var e = run.Value;
                int s = Mathf.FloorToInt(e.Seconds);
                UiKit.Label(Row(34f), $"Floor {e.Floor}  ·  {s / 60}:{s % 60:00}" +
                    (e.Secured > 0 ? $"  ·  {e.Secured} piece{(e.Secured == 1 ? "" : "s")} secured" : ""), 21, Ink, TextAnchor.MiddleLeft);
            }
            var body = UiKit.Label(Row(58f), thanks
                ? "Thanks for playing. Equivalence is made by one person - if this run was fun, share it, or leave a tip."
                : "Equivalence is a solo indie project. If you're enjoying it, sharing it helps the most - and a tip keeps it going toward the full beta.",
                18, Dim);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            y += G;

            var shareText = thanks ? RunText(run.Value) : $"{TS.ShareLine} {TS.ShareUrl}";
            Plate(Row(64f), thanks ? "SHARE THIS RUN" : "SHARE THE GAME", null, ShareFace, ShareInk,
                () => { Chain.Service.Telemetry.Card("share"); Share(shareText); });

            if (tips.Count > 0)
            {
                y += G;
                var row = Row(76f);
                float gap = 12f, bw = (W - 2f * P - gap * (tips.Count - 1)) / tips.Count;
                for (int i = 0; i < tips.Count; i++)
                {
                    var cell = UiKit.Rect(row, "tip", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(i * (bw + gap), 0f), new Vector2(i * (bw + gap) + bw, 0f));
                    Plate(cell, tips[i].Label, tips[i].Value, TipFace, Gold, tips[i].Act);
                }
            }
            if (discord)
            {
                y += G;
                var url = TS.DiscordUrl;
                Plate(Row(56f), "JOIN THE DISCORD", null, TipFace, Gold, () => { Chain.Service.Telemetry.Card("discord"); Application.OpenURL(url); Say("THE DISCORD OPENED IN A NEW TAB"); });
            }
            y += 10f;
            _toast = UiKit.Label(Row(28f), "", 16, Gold, TextAnchor.MiddleCenter);
            _toast.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        /// <summary>A button: its face, a gold-dark rim, the label and an optional value under it.</summary>
        void Plate(RectTransform at, string label, string value, Color face, Color ink, Action act)
        {
            var rim = UiKit.Panel(at, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(face.r * 2.2f, face.g * 2.2f, face.b * 2.2f, 1f));
            UiKit.Panel(rim, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f), face);
            bool sub = !string.IsNullOrEmpty(value);
            var top = UiKit.Rect(rim, "l", new Vector2(0f, sub ? 0.45f : 0f), Vector2.one, Vector2.zero, Vector2.zero);
            UiKit.Label(top, label, 19, ink, TextAnchor.MiddleCenter);
            if (sub)
            {
                var v = UiKit.Rect(rim, "v", Vector2.zero, new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
                UiKit.Label(v, value, 16, Ink, TextAnchor.MiddleCenter);
            }
            _hits.Add((rim, act));
        }

        void Say(string text)
        {
            if (_toast == null) return;
            _toast.text = text;
            _toastUntil = Time.unscaledTime + Tuning.Showcase.SupportToastSeconds;
        }

        /// <summary>A phone's share sheet, or the text copied (the connector's <c>share</c>). In the
        /// Editor, onto the clipboard. Refused, the link is shown to read.</summary>
        async void Share(string text)
        {
            if (!Chain.Web.WebChainBridge.Available) { Copy(text, "COPIED - PASTE IT ANYWHERE"); return; }
            try
            {
                var r = await Chain.Web.WebChainBridge.Call("share", new Chain.Web.ChainArgs { text = text });
                if (r == "shared") Say("THANK YOU FOR SHARING");
                else if (r == "copied") Say("COPIED - PASTE IT ANYWHERE");
            }
            catch (Exception) { Say(TS.ShareUrl); }
        }

        async void Copy(string text, string said)
        {
            if (!Chain.Web.WebChainBridge.Available) { GUIUtility.systemCopyBuffer = text; Say(said); return; }
            try { await Chain.Web.WebChainBridge.Call("copy", new Chain.Web.ChainArgs { text = text }); Say(said); }
            catch (Exception) { Say(text); }
        }

        public void Close()
        {
            if (_open == this) _open = null;
            ClosedFrame = Time.frameCount;
            if (_root) Destroy(_root);
            _root = null;
            _hits.Clear();
            GamePause.Release(this);
            var done = _closed;
            _closed = null;
            Destroy(gameObject);
            done?.Invoke();
        }

        void Update()
        {
            if (_root == null) return;
            if (_toast != null && _toast.text.Length > 0 && Time.unscaledTime > _toastUntil) _toast.text = "";
            _focus.Clear();
            foreach (var (rect, _) in _hits) _focus.Add(rect);
            Controls.SetFocusCandidates(_focus);
            if (Time.frameCount == _openedFrame) return;

            if (Controls.Tapped(out var at))
                foreach (var (rect, act) in _hits)
                    if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) { act(); return; }
            if (Controls.CancelTapped) Close();
        }

        void OnDestroy()
        {
            if (_open == this) { _open = null; GamePause.Release(this); if (_root) Destroy(_root); }
        }
    }
}
