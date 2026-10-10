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
            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), new Vector2(0, -80)),
                "THE CIRCLE IS LIT", 40, new Color(0.86f, 0.72f, 0.38f), TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -190), new Vector2(0, -150)),
                $"choose one Nigredo to transmute - the others wait for the next circle  " +
                $"({mods.TransmutedCount + 1} of {Core.Tuning.Exchange.TransmutationsPerRun} this run)", 18,
                new Color(0.55f, 0.58f, 0.66f), TextAnchor.MiddleCenter);

            const float w = 340f, gap = 24f;
            int n = nigredos.Count;
            float x0 = -(n * w + (n - 1) * gap) / 2f;
            for (int i = 0; i < n; i++)
            {
                var cost = nigredos[i];
                var albedo = ExchangeCatalog.Get(cost.AlbedoId);
                float x = x0 + i * (w + gap);
                var card = UiKit.Panel(full, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, -170), new Vector2(x + w, 190), new Color(0.085f, 0.09f, 0.12f, 1f));

                UiKit.Label(UiKit.Rect(card, "k", new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -40), new Vector2(-18, -12)),
                    $"[{i + 1}]  {cost.Name}  {ExchangeEntry.Roman(mods.StacksOf(cost))}", 20, new Color(0.88f, 0.44f, 0.31f));
                var nig = UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -150), new Vector2(-18, -46)),
                    $"{cost.Effect}\nNigredo - {cost.CapName}: {cost.CapText}", 15, new Color(0.7f, 0.62f, 0.6f));
                nig.horizontalOverflow = HorizontalWrapMode.Wrap;

                var rule = UiKit.Rect(card, "r", new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -164), new Vector2(-18, -162));
                rule.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.32f, 0.38f);

                UiKit.Label(UiKit.Rect(card, "a", new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -200), new Vector2(-18, -172)),
                    albedo != null ? $"becomes  {albedo.Name}" : "becomes nothing", 20, new Color(0.92f, 0.92f, 0.86f));
                var ab = UiKit.Label(UiKit.Rect(card, "e", new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -340), new Vector2(-18, -206)),
                    albedo?.Effect ?? "", 16, new Color(0.78f, 0.8f, 0.86f));
                ab.horizontalOverflow = HorizontalWrapMode.Wrap;

                _rows.Add((card, cost));
            }
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
