using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Exchange;

namespace Convergence.UI
{
    /// <summary>
    /// The choice a lit transmutation circle asks when the run holds more than one Nigredo: which
    /// cost becomes its Albedo. One per circle (the user's rule); the rest wait for the next.
    ///
    /// It PAUSES the fight (the user's call, 2026-10-05): the risk was standing in the circle long
    /// enough to light it, and a choice made against the clock would test the input rather than
    /// the decision. Modal like every other screen - in GameBootstrap.Update's early-return guard,
    /// and it must be answered (no BACK), since the circle is already lit.
    /// </summary>
    public class TransmuteScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        readonly List<(RectTransform Rect, ExchangeEntry Cost)> _rows = new();
        Action<ExchangeEntry> _chosen;
        int _openedFrame = -1;

        public void Show(Transform canvas, List<ExchangeEntry> nigredos, RunModifiers mods, Action<ExchangeEntry> chosen)
        {
            if (IsOpen || nigredos == null || nigredos.Count == 0) { chosen?.Invoke(null); return; }
            IsOpen = true;
            _chosen = chosen;
            _openedFrame = Time.frameCount;
            _rows.Clear();
            Core.GamePause.Hold(this);

            _root = new GameObject("TransmuteScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.025f, 0.04f, 0.9f));
            // The process NAMED, never explained (the user's calls, 2026-10-21): the screen is the
            // ALBEDO, each card a NIGREDO and what it becomes, in their colours (ExchangeInk).
            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, -80)),
                "ALBEDO", 40, new Color(0.86f, 0.72f, 0.38f), TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -190), new Vector2(0, -150)),
                $"{mods.TransmutedCount + 1} of {Core.Tuning.Exchange.TransmutationsPerRun}", 18,
                new Color(0.55f, 0.58f, 0.66f), TextAnchor.MiddleCenter);

            const float w = 360f, gap = 24f;
            int n = nigredos.Count;
            float x0 = -(n * w + (n - 1) * gap) / 2f;
            for (int i = 0; i < n; i++)
            {
                var cost = nigredos[i];
                var albedo = ExchangeCatalog.Get(cost.AlbedoId);
                float x = x0 + i * (w + gap);
                var card = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, -170), new Vector2(x + w, 200), new Color(0.085f, 0.09f, 0.12f, 1f));

                var stack = UiKit.Rect(card, "text", new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, 0), new Vector2(-20, -16));
                stack.pivot = new Vector2(0.5f, 1f);
                var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperLeft;
                layout.childControlWidth = true; layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                layout.spacing = 8f;
                stack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                Line(stack, $"[{i + 1}]  {cost.Name}  {ExchangeEntry.Roman(mods.StacksOf(cost))}", 21, ExchangeInk.Cost, FontStyle.Bold);
                Line(stack, cost.Effect, 16, new Color(0.7f, 0.64f, 0.62f));
                Line(stack, ExchangeInk.Capstone(cost), 15, ExchangeScale.NigredoText, FontStyle.Bold);
                Line(stack, cost.CapText, 16, ExchangeScale.NigredoText);
                IconRow(stack, cost, albedo);
                var rule = UiKit.Rect(stack, "r", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                rule.gameObject.AddComponent<LayoutElement>().preferredHeight = 2f;
                rule.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.32f, 0.38f);
                Line(stack, albedo != null ? $"ALBEDO · {albedo.Name}" : "ALBEDO", 15, ExchangeInk.White, FontStyle.Bold);
                if (albedo != null) Line(stack, albedo.Effect, 17, new Color(0.78f, 0.8f, 0.86f));

                _rows.Add((card, cost));
            }
        }

        static Text Line(RectTransform stack, string text, int size, Color color, FontStyle style = FontStyle.Normal)
        {
            var label = UiKit.Label(stack, text ?? "", size, color, TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.fontStyle = style;
            return label;
        }

        /// <summary>The Nigredo's icon, an arrow, the Albedo's - each in its own colour.</summary>
        static void IconRow(RectTransform stack, ExchangeEntry cost, ExchangeEntry albedo)
        {
            const float size = 64f;
            var row = UiKit.Rect(stack, "icons", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = size + 8f;
            void Icon(ExchangeEntry e, float cx)
            {
                if (e == null) return;
                var r = UiKit.Rect(row, "i", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(cx - size * 0.5f, -size * 0.5f), new Vector2(cx + size * 0.5f, size * 0.5f));
                var img = r.gameObject.AddComponent<RawImage>();
                img.texture = ExchangeScale.IconTexture(e);
                img.raycastTarget = false;
            }
            Icon(cost, -70f);
            UiKit.Label(UiKit.Rect(row, "to", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-24, -16), new Vector2(24, 16)),
                ">>", 22, new Color(0.56f, 0.36f, 0.18f), TextAnchor.MiddleCenter);
            Icon(albedo, 70f);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Core.GamePause.Release(this);
            if (_root) Destroy(_root);
            _root = null;
            _rows.Clear();
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == _openedFrame || _rows.Count == 0) return;

            for (int i = 0; i < _rows.Count && i < 9; i++)
                if (Core.Controls.DigitTapped(i)) { Pick(_rows[i].Cost); return; }

            var rects = new List<RectTransform>();
            foreach (var (rect, _) in _rows) rects.Add(rect);
            Core.Controls.SetFocusCandidates(rects);

            if (!Core.Controls.Tapped(out var at)) return;
            foreach (var (rect, cost) in _rows)
                if (RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) { Pick(cost); return; }
        }

        void Pick(ExchangeEntry cost)
        {
            var cb = _chosen;
            Close();
            cb?.Invoke(cost);
        }
    }
}
