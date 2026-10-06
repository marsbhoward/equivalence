using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Exchange;

namespace Convergence.UI
{
    /// <summary>
    /// The run's boons and costs as a row of icons: boons, a rule, then costs.
    ///
    /// Shared by the exchange row and the character sheet so the two cannot drift. They differ
    /// only in what they are for - mid-decision it is glanced at, so it stays iconography; on the
    /// character sheet it is read, so names are shown beside it.
    /// </summary>
    public static class LedgerStrip
    {
        public static readonly Color BoonTint = new(0.61f, 0.82f, 0.42f);
        public static readonly Color CostTint = new(0.88f, 0.44f, 0.31f);
        static readonly Color Gold = new(0.86f, 0.72f, 0.38f);

        /// <param name="highlight">Ids to light. Anything on offer that the run already holds.</param>
        /// <param name="withNames">Names under each icon. For the sheet, not the choice row.</param>
        public static void Build(RectTransform parent, RunModifiers mods,
                                 ISet<string> highlight = null, bool withNames = false,
                                 float size = 44f, float gap = 8f)
        {
            if (mods == null || mods.Held.Count == 0)
            {
                UiKit.Label(UiKit.Rect(parent, "empty", Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero),
                    "nothing taken yet", 15, new Color(0.42f, 0.45f, 0.53f), TextAnchor.MiddleLeft);
                return;
            }

            // Boons and costs are grouped rather than shown in the order they were taken. The
            // question this row answers is "what am I carrying", and a chronological mix makes
            // that a counting exercise.
            var boons = new List<ExchangeEntry>();
            var costs = new List<ExchangeEntry>();
            foreach (var e in mods.Held)
            {
                if (mods.StacksOf(e) <= 0) continue;
                (e.Kind == ExchangeKind.Boon ? boons : costs).Add(e);
            }

            float x = 0f;
            foreach (var e in boons) x = Chip(parent, mods, e, x, size, gap, highlight, withNames);

            if (boons.Count > 0 && costs.Count > 0)
            {
                var rule = UiKit.Rect(parent, "rule", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(x + 4f, -size * 0.45f), new Vector2(x + 5f, size * 0.45f));
                rule.gameObject.AddComponent<Image>().color = new Color(0.35f, 0.37f, 0.44f);
                x += 14f;
            }

            foreach (var e in costs) Chip(parent, mods, e, x, size, gap, highlight, withNames);
        }

        static float Chip(RectTransform parent, RunModifiers mods, ExchangeEntry e, float x,
                          float size, float gap, ISet<string> highlight, bool withNames)
        {
            int n = mods.StacksOf(e);
            bool lit = highlight != null && highlight.Contains(e.Id);

            var chip = UiKit.Panel(parent, new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                new Vector2(x, -size * 0.5f), new Vector2(x + size, size * 0.5f),
                lit ? new Color(0.20f, 0.17f, 0.09f) : new Color(0.10f, 0.11f, 0.14f));

            if (lit)
            {
                var ring = UiKit.Rect(chip, "lit", Vector2.zero, Vector2.one,
                    new Vector2(-2, -2), new Vector2(2, 2)).gameObject.AddComponent<Image>();
                ring.color = new Color(Gold.r, Gold.g, Gold.b, 0.32f);
                ring.raycastTarget = false;
                ring.transform.SetAsFirstSibling();
            }

            var img = UiKit.Rect(chip, "i", Vector2.zero, Vector2.one,
                new Vector2(7, 7), new Vector2(-7, -7)).gameObject.AddComponent<Image>();
            img.sprite = ExchangeGlyphs.Get(e);
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = e.Kind == ExchangeKind.Boon ? BoonTint : CostTint;

            // Repeats collapse to a count. Ten floors of picks as twenty tiles is no longer
            // something anyone can read at a glance.
            if (n > 1)
                UiKit.Label(UiKit.Rect(chip, "n", new Vector2(1, 0), new Vector2(1, 0),
                    new Vector2(-16, -6), new Vector2(4, 14)),
                    n.ToString(), 14, new Color(0.95f, 0.95f, 0.98f), TextAnchor.MiddleCenter);

            if (withNames)
                UiKit.Label(UiKit.Rect(chip, "nm", new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(-14, -22), new Vector2(14, -2)),
                    e.Name, 11, new Color(0.62f, 0.65f, 0.72f), TextAnchor.UpperCenter);

            return x + size + gap;
        }
    }
}
