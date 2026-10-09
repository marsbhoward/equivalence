using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;
using Convergence.Rifts;

namespace Convergence.UI
{
    /// <summary>
    /// A read-only look at what the run is carrying, reached from the pause menu.
    ///
    /// CARRIED loot is the run's stake - lost on death, or on leaving without reaching a Rift -
    /// and SECURED loot is already banked and cannot be lost. This screen only SHOWS that split;
    /// the decision that moves a piece across it is the Rift's (see RiftScreen). Framed the same
    /// way it is: at-risk on the left, safe on the right.
    ///
    /// Dismissible - it answers nothing, it just shows the bag - so it carries a BACK button and
    /// closes on Cancel, and its close hands control straight back to the pause menu.
    /// </summary>
    public class InventoryScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        int _openedFrame = -1;
        RunLoot _loot;
        System.Action _onClosed;
        RectTransform _backRect;
        readonly List<RectTransform> _focus = new();

        public void Open(Transform canvas, RunLoot loot, System.Action onClosed = null)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _loot = loot;
            _onClosed = onClosed;
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
            _backRect = null;

            var cb = _onClosed;
            _onClosed = null;
            cb?.Invoke();
        }

        // ------------------------------------------------------------------ ui

        void Build(Transform canvas)
        {
            _root = new GameObject("InventoryScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.04f, 0.04f, 0.055f, 0.97f));

            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -102), new Vector2(0, -44)),
                "INVENTORY", 42, new Color(0.90f, 0.91f, 0.96f), TextAnchor.MiddleCenter);

            int found = _loot?.FoundBoxes ?? 0;
            int reserve = _loot?.BankedBoxes ?? 0;
            UiKit.Label(UiKit.Rect(full, "s", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -140), new Vector2(0, -102)),
                "what this run is carrying",
                18, new Color(0.55f, 0.57f, 0.66f), TextAnchor.MiddleCenter);

            // ---- left: at risk ----
            var left = UiKit.Panel(full, new Vector2(0, 0), new Vector2(0.5f, 1),
                new Vector2(60, 190), new Vector2(-20, -170), new Color(0.13f, 0.09f, 0.09f, 1f));
            UiKit.Label(UiKit.Rect(left, "h", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(20, -50), new Vector2(-20, -12)),
                $"CARRIED - AT RISK   {_loot?.CarriedCount ?? 0}", 23, new Color(0.95f, 0.62f, 0.55f));
            UiKit.Label(UiKit.Rect(left, "w", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(20, 12), new Vector2(-20, 46)),
                "lost if you die before you reach a Rift", 15,
                new Color(0.72f, 0.45f, 0.42f), TextAnchor.MiddleCenter);
            Fill(UiKit.Rect(left, "b", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(14, 52), new Vector2(-14, -56)), _loot?.Carried);

            // ---- right: safe ----
            var right = UiKit.Panel(full, new Vector2(0.5f, 0), new Vector2(1, 1),
                new Vector2(20, 190), new Vector2(-60, -170), new Color(0.08f, 0.11f, 0.09f, 1f));
            UiKit.Label(UiKit.Rect(right, "h", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(20, -50), new Vector2(-20, -12)),
                $"SECURED - SAFE   {_loot?.SecuredCount ?? 0}", 23, new Color(0.62f, 0.92f, 0.68f));
            Fill(UiKit.Rect(right, "b", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(14, 14), new Vector2(-14, -56)), _loot?.Secured);

            // ---- back ----
            _backRect = UiKit.Panel(full, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-200, 36), new Vector2(200, 36 + UiKit.TouchTarget),
                new Color(0.12f, 0.13f, 0.17f, 1f));
            UiKit.Label(_backRect, "BACK", 22, new Color(0.82f, 0.85f, 0.92f), TextAnchor.MiddleCenter);

            // ---- the Rift Boxes held, either side of BACK: the pickup's own picture at menu
            // density, so the box picked up mid-fight is recognisably the one counted here ----
            BoxCount(full, -620f, found, "FOUND", "lost if you die", new Color(0.95f, 0.62f, 0.55f));
            BoxCount(full, 300f, reserve, "IN RESERVE", "safe - from earlier runs", new Color(0.62f, 0.92f, 0.68f));
        }

        static void BoxCount(RectTransform parent, float x, int count, string title, string note, Color ink)
        {
            var icon = BoxIcon.Add(parent, Art.BoxArt.Kind.Rift, true, new Vector2(0.5f, 0f), new Vector2(x, 40f));
            if (count == 0) icon.color = new Color(1f, 1f, 1f, 0.3f);
            UiKit.Label(UiKit.Rect(parent, "bc", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(x + 96f, 40f), new Vector2(x + 360f, 158f)),
                $"{title}  x{count}\n<size=15>{note}</size>", 22,
                count > 0 ? ink : new Color(0.5f, 0.5f, 0.55f), TextAnchor.MiddleLeft);
        }

        void Fill(RectTransform parent, IReadOnlyList<MintedGearRecord> items)
        {
            if (items == null || items.Count == 0)
            {
                UiKit.Label(UiKit.Rect(parent, "empty", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, -60), new Vector2(0, -12)),
                    "nothing yet", 17, new Color(0.5f, 0.5f, 0.55f), TextAnchor.MiddleCenter);
                return;
            }

            const float h = 72f, gap = 8f;
            for (int i = 0; i < items.Count && i < 8; i++)
            {
                var it = items[i];
                float y = -i * (h + gap);
                var card = UiKit.Panel(parent, new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, y - h), new Vector2(0, y), new Color(0.11f, 0.12f, 0.15f, 1f));

                var chip = UiKit.Rect(card, "c", new Vector2(0, 0), new Vector2(0, 1),
                    new Vector2(10, 10), new Vector2(20, -10));
                chip.gameObject.AddComponent<Image>().color = CharacterScreen.TierColor(it.Tier);

                UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(34, -34), new Vector2(-12, -6)),
                    it.DisplayName, 20, CharacterScreen.TierColor(it.Tier));

                // Drops arrive at up to three stars - shown as the gear picker shows them.
                if (it.UpgradeLevel > 0)
                    UiKit.Stars(UiKit.Rect(card, "stars", new Vector2(1, 1), new Vector2(1, 1),
                                    new Vector2(-60, -22), new Vector2(-6, -4)),
                                it.UpgradeLevel, Art.Gear.GearRoller.MaxLevel, 17f,
                                new Color(1f, 0.82f, 0.3f), new Color(1f, 1f, 1f, 0.12f));

                UiKit.Label(UiKit.Rect(card, "d", new Vector2(0, 0), new Vector2(1, 1),
                    new Vector2(34, 6), new Vector2(-12, -34)),
                    Detail(it), 14, new Color(0.62f, 0.65f, 0.72f));
            }

            if (items.Count > 8)
                UiKit.Label(UiKit.Rect(parent, "more", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(0, -8 * (h + gap) - 34), new Vector2(0, -8 * (h + gap))),
                    $"+{items.Count - 8} more", 16, new Color(0.5f, 0.5f, 0.55f), TextAnchor.MiddleCenter);
        }

        /// <summary>The rolled stats, plus a relic's finisher when it has one - the same handful
        /// of values a Rift screen shows, spelled out here because this is the screen you open to
        /// actually read what is in the bag.</summary>
        static string Detail(MintedGearRecord it)
        {
            var parts = new List<string>();
            void P(string k, float v) { if (Mathf.Abs(v) > 0.01f) parts.Add($"{k} {v:+0;-0}%"); }
            var g = it.Grants;
            if (g != null)
            {
                P("dmg", g.Damage); P("spd", g.AttackSpeed); P("move", g.MoveSpeed);
                P("range", g.Range); P("hp", g.MaxHp); P("armor", g.Armor);
                P("sturdy", g.DamageResistance); P("resil", g.Resilience);
                P("elem", g.ElementGrowth); P("cdr", g.AbilityCooldownReduction);
                P("art", g.FinisherPower); P("art kb", g.FinisherKnockback);
                P("crit", g.CritChance); P("crit dmg", g.CritDamage);
                P("graze", g.Graze); P("brace", g.Brace); P("cleave", g.Cleave);
                P("elem pow", g.ElementalEffectiveness); P("combo", g.ComboTime);
                P("area", g.AoeRadius); P("heal", g.HealReceived); P("repair", g.RepairReceived);
                P("mend", g.Mend); P("splash", g.Splash); P("pierce", g.Pierce);
                P("accuracy", g.Accuracy);
            }
            string stats = parts.Count > 0 ? string.Join("   ", parts) : "no stat bonuses";

            if (!string.IsNullOrEmpty(it.Finisher))
            {
                var mv = Combat.MovesetLibrary.ById(it.Finisher);
                if (mv != null) stats += $"      weapon art: {mv.DisplayName}";
            }
            return stats;
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;
            if (Time.frameCount == _openedFrame) return;

            _focus.Clear();
            if (_backRect) _focus.Add(_backRect);
            Core.Controls.SetFocusCandidates(_focus);

            if (Core.Controls.CancelTapped) { Close(); return; }

            if (!Core.Controls.Tapped(out var p)) return;
            if (_backRect && RectTransformUtility.RectangleContainsScreenPoint(_backRect, p, null))
                Close();
        }
    }
}
