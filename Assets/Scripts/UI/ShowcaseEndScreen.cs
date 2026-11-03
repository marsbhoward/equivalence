using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// THE SHOWCASE's closing card (SHOWCASE.md, the user's calls 2026-10-31): shown when the player
    /// extracts through floor 10's Blue Rift. Thanks, the run clock, a card to join the Discord, a few
    /// tips for the next run, and what the full beta holds - every number in that breakdown COUNTED from the game's own catalogues, so it is never out of date.
    ///
    /// The Discord link is Tuning.Showcase's. The support card moved to the hub's thank-you
    /// (UI/SupportCard), opened as the player comes home from this ending.
    /// </summary>
    public class ShowcaseEndScreen : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        GameObject _root;
        Action _done;
        int _openedFrame;
        readonly List<(RectTransform Rect, Action Act)> _hits = new();
        readonly List<RectTransform> _focus = new();

        static readonly Color Ink = new(0.93f, 0.92f, 0.89f);
        static readonly Color Dim = new(0.6f, 0.62f, 0.68f);
        static readonly Color Gold = new(0.95f, 0.8f, 0.42f);
        static readonly Color CardBg = new(0.09f, 0.095f, 0.12f, 1f);

        public void Show(Transform canvas, float runClockSeconds, Action onDone)
        {
            Close();
            _done = onDone;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            GamePause.Hold(this);

            _root = new GameObject("ShowcaseEnd", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;
            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.02f, 0.035f, 0.96f));

            var mid = new Vector2(0.5f, 0.5f);
            int s = Mathf.FloorToInt(runClockSeconds);
            UiKit.Label(UiKit.Rect(full, "t", mid, mid, new Vector2(-900, 400), new Vector2(900, 470)),
                "THROUGH THE RIFT", 48, Ink, TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(full, "s", mid, mid, new Vector2(-900, 356), new Vector2(900, 398)),
                $"You reached floor {RunFormat.EndFloor} of {RunFormat.FinalFloor} in {s / 60}:{s % 60:00}. Thank you for playing the showcase - the tower goes much higher.",
                20, Dim, TextAnchor.MiddleCenter);

            // ---- the Discord. Support moved to the hub's thank-you (UI/SupportCard, the user's call
            // 2026-11-03), which opens as the player comes home from this ending.
            if (!string.IsNullOrEmpty(Tuning.Showcase.DiscordUrl))
            {
                const float lw = 560f, lx = -lw / 2f;
                var card = UiKit.Panel(full, mid, mid, new Vector2(lx, 180), new Vector2(lx + lw, 336), new Color(0.16f, 0.13f, 0.07f, 1f));
                UiKit.Label(UiKit.Rect(card, "t", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -56), new Vector2(-24, -16)), "JOIN THE DISCORD", 24, Gold);
                var b = UiKit.Label(UiKit.Rect(card, "b", Vector2.zero, Vector2.one, new Vector2(24, 14), new Vector2(-24, -60)),
                    "Tell us what worked and what didn't, and hear first when the full beta opens.", 17, Ink);
                b.horizontalOverflow = HorizontalWrapMode.Wrap;
                var url = Tuning.Showcase.DiscordUrl;
                _hits.Add((card, () => { Chain.Service.Telemetry.Card("discord"); Application.OpenURL(url); }));
            }

            // ---- tips for the next run ----
            var tips = new[]
            {
                ("THE TIMING BAR", "Land a weapon art's finisher in the bright band for more damage - a perfect streak builds critical chance."),
                ("HOLD TO AIM", "Hold an art's button to aim it before you let go. Released at once, it goes where you face."),
                ("SECURE AT A RIFT", "Gear you carry is lost if you fall. A Rift banks it - step through, or secure and push on."),
            };
            float tw = 380f, tgap = 24f, tx = -(tips.Length * tw + (tips.Length - 1) * tgap) / 2f;
            UiKit.Label(UiKit.Rect(full, "th", mid, mid, new Vector2(-900, 136), new Vector2(900, 170)),
                "FOR YOUR NEXT CLIMB", 18, Dim, TextAnchor.MiddleCenter);
            foreach (var (title, body) in tips)
            {
                var card = UiKit.Panel(full, mid, mid, new Vector2(tx, -10), new Vector2(tx + tw, 126), CardBg);
                UiKit.Label(UiKit.Rect(card, "t", new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -46), new Vector2(-20, -12)), title, 19, Ink);
                var b = UiKit.Label(UiKit.Rect(card, "b", Vector2.zero, Vector2.one, new Vector2(20, 12), new Vector2(-20, -50)), body, 15, Dim);
                b.horizontalOverflow = HorizontalWrapMode.Wrap;
                tx += tw + tgap;
            }

            // ---- what the full beta holds - counted, never typed ----
            UiKit.Label(UiKit.Rect(full, "bh", mid, mid, new Vector2(-900, -64), new Vector2(900, -30)),
                "THE FULL BETA", 18, Dim, TextAnchor.MiddleCenter);
            var facts = Breakdown();
            int cols = 5;
            float fw = 300f, fh = 96f, fgap = 16f;
            for (int i = 0; i < facts.Count; i++)
            {
                int col = i % cols, row = i / cols;
                int inRow = Mathf.Min(cols, facts.Count - row * cols);
                float x0 = -(inRow * fw + (inRow - 1) * fgap) / 2f + col * (fw + fgap);
                float y1 = -80f - row * (fh + fgap);
                var cell = UiKit.Panel(full, mid, mid, new Vector2(x0, y1 - fh), new Vector2(x0 + fw, y1), CardBg);
                UiKit.Label(UiKit.Rect(cell, "n", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -54), new Vector2(0, -8)),
                    facts[i].Number, 36, Gold, TextAnchor.MiddleCenter);
                UiKit.Label(UiKit.Rect(cell, "l", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 10), new Vector2(0, 40)),
                    facts[i].Label, 15, Ink, TextAnchor.MiddleCenter);
            }

            var cont = UiKit.Panel(full, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-150, 40), new Vector2(150, 96), new Color(0.2f, 0.2f, 0.26f));
            UiKit.Label(cont, "CONTINUE", 22, Ink, TextAnchor.MiddleCenter);
            _hits.Add((cont, Finish));
        }

        /// <summary>The full game's content, counted from the catalogues it runs on.</summary>
        static List<(string Number, string Label)> Breakdown()
        {
            int Count<T>() where T : Enum => Enum.GetValues(typeof(T)).Length;
            int uniqueWeapons = Art.Gear.GearCatalog.All.Values
                .Count(g => g != null && g.Slot == Art.Gear.GearSlot.Weapon && g.IsCosmeticTier
                            && g.ItemId != null && !g.ItemId.Contains("-"));
            int boons = 0, costs = 0;
            foreach (var e in Exchange.ExchangeCatalog.All)
                if (e.Kind == Exchange.ExchangeKind.Boon) boons++; else costs++;
            return new List<(string, string)>
            {
                (RunFormat.FinalFloor.ToString(), "floors to climb"),
                (Count<Bosses.BossKind>().ToString(), "mini-bosses"),
                (Count<Enemies.EnemyKind>().ToString(), "enemy kinds"),
                ((Count<Enemies.EliteAffix>() - 1).ToString(), "elite modifiers"),
                (Count<Art.Biomes.BiomeKind>().ToString(), "biomes"),
                (uniqueWeapons.ToString(), "unique weapons"),
                (boons.ToString(), "boons in the exchange"),
                (costs.ToString(), "costs to bargain with"),
                (Count<Puzzles.PuzzleKind>().ToString(), "kinds of puzzle"),
                (Progression.MasteryBoard.All.Count.ToString(), "nodes on the mastery grid"),
            };
        }

        void Finish()
        {
            if (_done != null) Chain.Service.Telemetry.Card("continue");
            var done = _done;
            _done = null;
            Close();
            done?.Invoke();
        }

        public void Close()
        {
            if (_root) Destroy(_root);
            _root = null;
            _hits.Clear();
            if (IsOpen) GamePause.Release(this);
            IsOpen = false;
        }

        void Update()
        {
            if (!IsOpen) return;
            _focus.Clear();
            foreach (var (rect, _) in _hits) _focus.Add(rect);
            Controls.SetFocusCandidates(_focus);
            if (Time.frameCount == _openedFrame) return;

            if (Controls.Tapped(out var at))
                foreach (var (rect, act) in _hits)
                    if (rect != null && RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) { act(); return; }
            if (Controls.CancelTapped) Finish();
        }

        void OnDisable() => Close();
    }
}
