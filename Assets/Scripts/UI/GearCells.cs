using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Chain;

namespace Convergence.UI
{
    /// <summary>
    /// The pieces both grid screens share - the Rift screen and the run's inventory (the user's
    /// design, 2026-11-06): a piece's picture in its cell, and the card of its roll shown on hover.
    /// </summary>
    public static class GearCells
    {
        static readonly Color Dim = new(0.55f, 0.52f, 0.65f);
        static readonly Color FreeInk = new(0.94f, 0.88f, 0.82f);
        static readonly Color PaidInk = new(0.80f, 0.72f, 1f);
        static readonly Vector3[] _corners = new Vector3[4];

        /// <summary>A piece's own picture filling a cell's inside, its stars along the foot.</summary>
        public static void Picture(RectTransform inner, MintedGearRecord gear)
        {
            var item = GearCatalog.Get(gear.InstanceId) ?? gear.ToGearItem();
            var layer = item != null ? Hub.GearDisplay.Represent(item, menu: true) : null;
            if (layer?.Sprite != null)
            {
                var art = UiKit.Rect(inner, "art", Vector2.zero, Vector2.one, new Vector2(14, 28), new Vector2(-14, -14));
                var img = art.gameObject.AddComponent<Image>();
                img.sprite = layer.Sprite;
                img.color = layer.Tint;
                img.preserveAspect = true;
                img.raycastTarget = false;
                KindledImage.On(img, item);
            }
            if (gear.UpgradeLevel > 0)
                UiKit.Stars(UiKit.Rect(inner, "stars", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                                new Vector2(-30, 6), new Vector2(30, 24)),
                            gear.UpgradeLevel, GearRoller.MaxLevel, 15f,
                            new Color(1f, 0.82f, 0.3f), new Color(1f, 1f, 1f, 0.12f));
        }

        /// <summary>
        /// A piece's roll beside its cell (<paramref name="over"/>), inside <paramref name="full"/>:
        /// name in its tier's colour, tier and kind, stars, the main stat first, every sub-stat by
        /// name, the cuirass's defence and the relic's weapon art. The caller destroys it.
        /// </summary>
        public static GameObject Tip(RectTransform full, MintedGearRecord it, RectTransform over)
        {
            var lines = new List<(string Text, Color Ink, int Size)>();
            var tierCol = CharacterScreen.TierColor(it.Tier);
            lines.Add((it.DisplayName, tierCol, 24));
            string what = (it.Tier == LootTier.BlackDiamond ? "Black Diamond" : it.Tier.ToString())
                        + "  " + (it.Slot == GearSlot.Weapon ? it.Class.ToString() : it.Slot.ToString());
            lines.Add((what, Dim, 16));
            if (it.PrimaryStat != StatKind.None)
                lines.Add(($"{GearForge.Label(it.PrimaryStat)}   {GearRoller.PointsOf(it.Grants, it.PrimaryStat):+0;-0}%", FreeInk, 20));
            foreach (StatKind k in Enum.GetValues(typeof(StatKind)))
            {
                if (k == StatKind.None || k == it.PrimaryStat) continue;
                float v = GearRoller.PointsOf(it.Grants, k);
                if (Mathf.Abs(v) > 0.01f) lines.Add(($"{GearForge.Label(k)}   {v:+0;-0}%", new Color(0.78f, 0.80f, 0.86f), 18));
            }
            if (it.Slot == GearSlot.Torso)
                lines.Add(($"Defence   {it.DefensiveAbility.Label()}", new Color(0.70f, 0.82f, 0.95f), 18));
            if (!string.IsNullOrEmpty(it.Finisher))
            {
                var mv = Combat.MovesetLibrary.ById(it.Finisher);
                if (mv != null) lines.Add(($"Weapon art   {mv.DisplayName}", PaidInk, 18));
            }

            const float w = 420f, pad = 16f;
            float h = pad * 2;
            foreach (var l in lines) h += l.Size + 10f;

            // Beside the cell, on whichever side has room; above it when the cell is low.
            over.GetWorldCorners(_corners);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(full, _corners[2], null, out var topRight);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(full, _corners[0], null, out var bottomLeft);
            var r = full.rect;
            float x = topRight.x + 10f + w <= r.xMax ? topRight.x + 10f : bottomLeft.x - 10f - w;
            float y = Mathf.Clamp(topRight.y, r.yMin + h + 8f, r.yMax - 8f);

            var tip = new GameObject("Tip", typeof(RectTransform));
            tip.transform.SetParent(full, false);
            var rt = (RectTransform)tip.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, y);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.065f, 0.10f, 0.98f);
            bg.raycastTarget = false;
            var edge = UiKit.Panel(rt, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -4), Vector2.zero, tierCol);
            edge.GetComponent<Image>().raycastTarget = false;

            float cy = -pad;
            foreach (var l in lines)
            {
                UiKit.Label(UiKit.Rect(rt, "l", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(pad, cy - l.Size - 6f), new Vector2(-pad, cy)), l.Text, l.Size, l.Ink);
                cy -= l.Size + 10f;
            }
            if (it.UpgradeLevel > 0)
                UiKit.Stars(UiKit.Rect(rt, "stars", new Vector2(1, 1), new Vector2(1, 1),
                                new Vector2(-pad - 66, -pad - 54), new Vector2(-pad, -pad - 30)),
                            it.UpgradeLevel, GearRoller.MaxLevel, 20f,
                            new Color(1f, 0.82f, 0.3f), new Color(1f, 1f, 1f, 0.12f));
            return tip;
        }
    }
}
