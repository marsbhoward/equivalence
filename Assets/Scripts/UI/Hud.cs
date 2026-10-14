using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using Convergence.Player;

namespace Convergence.UI
{
    /// <summary>
    /// In-run HUD. The element's meter is its seal (<see cref="ElementSeal"/>) - segmented for
    /// Fire's rungs and Water's tiers, a continuous ring for Earth and Air, each building its own
    /// way - so the four rhythms read as different before combat even starts. Health and armour
    /// are crescents round its right side (<see cref="HealthCrescents"/>), and the weapon art
    /// rotation a wheel meshed to its left (<see cref="RotationWheel"/>).
    /// </summary>
    public class Hud : MonoBehaviour
    {
        PlayerController _player;

        HealthCrescents _crescents;
        Text _waveText;

        System.Func<float> _armorCondition;
        System.Func<float> _armorDamageMultiplier;
        System.Func<int> _pendingMastery;

        /// <summary>
        /// Weapon condition is deliberately not a parameter any more: weapon degradation is
        /// switched off (see <c>Durability</c>) and may return as a different mechanic, so the
        /// HUD should not keep a hook for it that silently reports a constant.
        ///
        /// <paramref name="damageMultiplier"/> is the REAL, live incoming-damage multiplier
        /// (wear penalty x Resilience), read rather than recomputed here - the label used to
        /// reconstruct this number from the condition fraction alone, using a copy of the wear
        /// penalty's own formula that had drifted from the real one it was supposed to describe.
        /// </summary>
        public void SetWearSource(System.Func<float> armor, System.Func<float> damageMultiplier,
                                   System.Func<int> pendingMastery)
        {
            _armorCondition = armor;
            _armorDamageMultiplier = damageMultiplier;
            _pendingMastery = pendingMastery;
        }

        GameObject _root;

        /// <summary>
        /// Everything this HUD creates goes under one root that the component owns, so destroying
        /// the component takes the UI with it. Parenting straight onto the canvas leaked a whole
        /// HUD per run - invisible while the opaque select screen covered it, but they stacked up
        /// and drew over each other the moment anything showed through.
        /// </summary>
        void OnDestroy()
        {
            if (_root) Destroy(_root);
        }

        public void Build(PlayerController player, Transform canvasRoot)
        {
            _root = new GameObject("HudRoot", typeof(RectTransform));
            _root.transform.SetParent(canvasRoot, false);
            var rootRt = (RectTransform)_root.transform;
            rootRt.anchorMin = Vector2.zero; rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero; rootRt.offsetMax = Vector2.zero;
            var canvas = _root.transform;

            _player = player;

            // ---------- the element's seal, health and armour, bottom-left of centre ----------
            // One piece: the seal, its build-up drawn round it, and the health and armour
            // crescents hugging its right side with the numbers beside them. Low and left of centre
            // so the numbers sit where the eye already goes mid-fight; the seal's effects may run
            // off the bottom edge, which is only the screen's edge.
            float sealSize = ElementSeal.Size * ElementSeal.UnitsPerTexel;
            var sealCentre = new Vector2(-432, 150);
            var sealRt = UiKit.Rect(canvas, "seal", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                sealCentre - Vector2.one * sealSize * 0.5f, sealCentre + Vector2.one * sealSize * 0.5f);
            ElementSeal.Build(sealRt, player.Resource);
            _crescents = HealthCrescents.Build(sealRt);

            // The weapon art wheel, meshed to the seal's left as the crescents hug its right.
            // It carries everything about what the attack button does: the art that fires next,
            // the chain's basics, the rotation, and the perfect streak.
            float wheelSize = RotationWheel.Size * RotationWheel.UnitsPerTexel;
            float wheelX = sealCentre.x - (ElementSeal.RimR + RotationWheel.OuterR + 4f) * ElementSeal.UnitsPerTexel;
            var wheelCentre = new Vector2(wheelX, sealCentre.y);
            var wheelRt = UiKit.Rect(canvas, "wheel", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                wheelCentre - Vector2.one * wheelSize * 0.5f, wheelCentre + Vector2.one * wheelSize * 0.5f);
            RotationWheel.Build(wheelRt, player);

            // ---------- wave / kills, top-right ----------
            // Stepped left of GEAR and MENU, which sit in the corner on every device now.
            var right = UiKit.Rect(canvas, "wave", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-768, -80), new Vector2(-380, -26));
            _waveText = UiKit.Label(right, "", 24, new Color(0.8f, 0.83f, 0.9f), TextAnchor.UpperRight);

            // The gear-condition text block that used to sit bottom-right is gone: armour is now
            // the bar above health, and weapon wear is switched off entirely (see Durability).

            var controls = UiKit.Rect(canvas, "controls", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-420, 10), new Vector2(420, 36));
            UiKit.Hint(controls,
                "WASD move    -    LMB / SPACE attack    -    RMB / E element ability    -    [Q] guard    -    [C] loadout    -    ESC pause / leave",
                "stick to move    -    ATTACK    -    ABILITY    -    GUARD    -    GEAR for your loadout    -    MENU to leave the run",
                15, new Color(0.45f, 0.48f, 0.56f), TextAnchor.LowerCenter,
                UI.GamepadGlyphs.Move + " move    -    " + UI.GamepadGlyphs.Attack + " attack    -    "
                + UI.GamepadGlyphs.Release + " element ability    -    " + UI.GamepadGlyphs.Guard + " guard    -    "
                + UI.GamepadGlyphs.Interact + " use    -    " + UI.GamepadGlyphs.Menu + " menu    -    " + UI.GamepadGlyphs.Inventory + " loot");
        }

        /// <summary>
        /// The boss bar, and it says TWO things rather than one.
        ///
        /// Health is the obvious half. The other half is whether the boss can currently be HIT,
        /// which for a fight built around one narrow window is the more urgent question - a player
        /// swinging at an immune boss is not making a mistake about damage, they are making one
        /// about timing, and nothing else on screen tells them. The bar dims and names the
        /// movement while it is shut.
        ///
        /// Pass a negative fraction to hide it. Built lazily so an ordinary floor pays nothing.
        /// </summary>
        public void SetBoss(float fraction01, string bossName, string movement, bool enraged, bool immune)
        {
            if (fraction01 < 0f)
            {
                if (_bossRoot != null) _bossRoot.gameObject.SetActive(false);
                return;
            }

            if (_bossRoot == null) BuildBossBar();
            _bossRoot.gameObject.SetActive(true);

            UiKit.SetFill(_bossFill, Mathf.Clamp01(fraction01));
            var tint = enraged ? new Color(1f, 0.35f, 0.30f) : new Color(0.55f, 0.65f, 1f);
            _bossFill.color = immune ? tint * 0.55f : new Color(1f, 0.92f, 0.55f);

            _bossLabel.text = immune
                ? (enraged ? $"{bossName}   -   ENRAGED   -   {movement}" : $"{bossName}   -   {movement}")
                : $"{bossName}   -   OPEN";
            _bossLabel.color = immune ? new Color(0.62f, 0.66f, 0.74f) : new Color(1f, 0.92f, 0.55f);
        }

        RectTransform _bossRoot;
        Image _bossFill;
        Text _bossLabel;

        void BuildBossBar()
        {
            _bossRoot = UiKit.Rect(_root.transform as RectTransform, "boss",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-360f, -96f), new Vector2(360f, -40f));

            var barRt = UiKit.Rect(_bossRoot, "bar", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 0), new Vector2(0, 18));
            _bossFill = UiKit.Bar(barRt, new Color(0.55f, 0.65f, 1f), new Color(0.10f, 0.10f, 0.14f, 0.9f));

            var lblRt = UiKit.Rect(_bossRoot, "lbl", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(0, 20), new Vector2(0, 0));
            _bossLabel = UiKit.Label(lblRt, "", 20, new Color(0.9f, 0.92f, 0.96f), TextAnchor.MiddleCenter);
        }

        /// <summary>
        /// A short banner for something the player just gained. Fades on its own.
        ///
        /// Deliberately not a queue: two of these in the same second would be a notification
        /// system, and there is exactly one thing in the game that uses it.
        /// </summary>
        public void Flash(string text)
        {
            if (_flashLabel == null)
            {
                var rt = UiKit.Rect(_root.transform as RectTransform, "flash",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-320f, 168f), new Vector2(320f, 216f));
                _flashLabel = UiKit.Label(rt, "", 26, new Color(0.80f, 0.72f, 1f), TextAnchor.MiddleCenter);
            }
            _flashLabel.gameObject.SetActive(true);
            _flashLabel.text = text;
            _flashUntil = Time.unscaledTime + 2.2f;
        }

        Text _flashLabel;
        float _flashUntil;

        void TickFlash()
        {
            if (_flashLabel == null || !_flashLabel.gameObject.activeSelf) return;
            float left = _flashUntil - Time.unscaledTime;
            if (left <= 0f) { _flashLabel.gameObject.SetActive(false); return; }
            var c = _flashLabel.color;
            _flashLabel.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(left / 0.6f));
        }

        /// <summary>The "a Rift is open" call to action. Built lazily; hidden by default.</summary>
        public void SetRiftPrompt(bool on)
        {
            if (!on)
            {
                if (_riftPrompt != null) _riftPrompt.gameObject.SetActive(false);
                return;
            }
            if (_riftPrompt == null)
            {
                var rt = UiKit.Rect(_root.transform as RectTransform, "rift",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-320f, 96f), new Vector2(320f, 148f));
                _riftPrompt = UiKit.Hint(rt, "A RIFT HAS OPENED   -   [ E ] at the tear to leave",
                    "A RIFT HAS OPENED   -   INTERACT at the tear to leave", 24,
                    new Color(0.80f, 0.72f, 1f));
            }
            _riftPrompt.gameObject.SetActive(true);
        }

        Text _riftPrompt;

        /// <summary>
        /// A dormant Red Rift's call to action: its seal is broken only when the player chooses to.
        /// Shares the open Rift prompt's spot - the two never show at once, since this one is only
        /// up while the tear is shut and its guard has not been called.
        /// </summary>
        public void SetRedRiftPrompt(bool on)
        {
            if (!on)
            {
                if (_redRiftPrompt != null) _redRiftPrompt.gameObject.SetActive(false);
                return;
            }
            if (_redRiftPrompt == null)
            {
                var rt = UiKit.Rect(_root.transform as RectTransform, "redrift",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-320f, 96f), new Vector2(320f, 148f));
                _redRiftPrompt = UiKit.Hint(rt, "A RED RIFT   -   [ E ] at the tear to break its seal",
                    "A RED RIFT   -   INTERACT at the tear to break its seal", 24,
                    new Color(1f, 0.55f, 0.42f));
            }
            _redRiftPrompt.gameObject.SetActive(true);
        }

        Text _redRiftPrompt;

        /// <summary>
        /// The Collapsing Rift's countdown, in exact seconds - the player is owed the real number
        /// for a check this tight. Negative hides it. Shares the prompt's spot: the two never show
        /// at once, since the prompt only appears once the Rift has stabilised.
        /// </summary>
        public void SetRiftTimer(float seconds)
        {
            if (seconds < 0f)
            {
                if (_riftTimer != null) _riftTimer.gameObject.SetActive(false);
                return;
            }
            if (_riftTimer == null)
            {
                var rt = UiKit.Rect(_root.transform as RectTransform, "rift-timer",
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-360f, 96f), new Vector2(360f, 148f));
                _riftTimer = UiKit.Label(rt, "", 26, new Color(0.80f, 0.72f, 1f), TextAnchor.MiddleCenter);
            }
            if (!_riftTimer.gameObject.activeSelf) _riftTimer.gameObject.SetActive(true);
            bool urgent = seconds < Core.Tuning.CollapsingRift.UrgentSeconds;
            UiKit.SetText(_riftTimer, $"THE RIFT IS COLLAPSING   {seconds:0.0}s   -   clear the floor");
            UiKit.SetColor(_riftTimer, urgent ? new Color(1f, 0.52f, 0.42f) : new Color(0.80f, 0.72f, 1f));
        }

        Text _riftTimer;

        /// <summary>
        /// A puzzle's clock (the labyrinth's), as M:SS in the puzzle panel's lower half - never
        /// below it, where the screen-centred player's head is. Large, since the
        /// close view leaves the player nothing else to judge time by. Turns red and grows under
        /// Tuning.Puzzle.MazeUrgentSeconds. Negative hides it. Rewritten only when the shown
        /// second changes.
        /// </summary>
        public void SetCountdown(float seconds)
        {
            if (seconds < 0f)
            {
                if (_countdown != null) _countdown.gameObject.SetActive(false);
                _countdownShown = -1;
                return;
            }
            if (_countdown == null)
            {
                var rt = UiKit.Rect(_root.transform as RectTransform, "puzzle-countdown",
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-200f, -322f), new Vector2(200f, -236f));
                _countdown = UiKit.Label(rt, "", 64, new Color(0.95f, 0.85f, 0.60f), TextAnchor.MiddleCenter);
            }
            if (!_countdown.gameObject.activeSelf) _countdown.gameObject.SetActive(true);
            int s = Mathf.CeilToInt(seconds);
            if (s == _countdownShown) return;
            _countdownShown = s;
            bool urgent = seconds < Core.Tuning.Puzzle.MazeUrgentSeconds;
            UiKit.SetText(_countdown, $"{s / 60}:{s % 60:00}");
            UiKit.SetColor(_countdown, urgent ? new Color(1f, 0.42f, 0.34f) : new Color(0.95f, 0.85f, 0.60f));
            _countdown.fontSize = urgent ? 76 : 64;
        }

        Text _countdown;
        int _countdownShown = -1;

        /// <summary>
        /// A puzzle floor's instructions, top centre: the puzzle's name and its live state
        /// (clues, steps left, progress). Null hides it. Text is only rewritten when it changes,
        /// since the puzzle is polled every frame.
        /// </summary>
        public void SetPuzzle(string title, string body)
        {
            if (title == null)
            {
                if (_puzzlePanel != null) _puzzlePanel.gameObject.SetActive(false);
                return;
            }
            if (_puzzlePanel == null)
            {
                _puzzlePanel = UiKit.Panel(_root.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-440f, -330f), new Vector2(440f, -96f), new Color(0.04f, 0.04f, 0.07f, 0.72f));
                _puzzleTitle = UiKit.Label(UiKit.Rect(_puzzlePanel, "t", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(20f, -46f), new Vector2(-20f, -10f)),
                    "", 26, new Color(0.95f, 0.85f, 0.60f), TextAnchor.MiddleCenter);
                _puzzleBody = UiKit.Label(UiKit.Rect(_puzzlePanel, "b", Vector2.zero, Vector2.one,
                    new Vector2(24f, 12f), new Vector2(-24f, -50f)),
                    "", 19, new Color(0.82f, 0.82f, 0.88f), TextAnchor.UpperCenter);
                _puzzleBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
            if (!_puzzlePanel.gameObject.activeSelf) _puzzlePanel.gameObject.SetActive(true);
            if (_puzzleTitle.text != title) UiKit.SetText(_puzzleTitle, title);
            if (_puzzleBody.text != body) UiKit.SetText(_puzzleBody, body);
        }

        RectTransform _puzzlePanel;
        Text _puzzleTitle, _puzzleBody;

        public void SetFloorInfo(int floor, int alive, int kills, int gearOnFloor = 0)
        {
            if (!_waveText) return;
            // Element mastery earned this run (floors + the reward card), banked at the run's end.
            int xp = _pendingMastery != null ? _pendingMastery() : 0;
            string xpLine = xp > 0 ? $"\n+{xp} mastery XP" : "";
            // Dropped gear never times out, but the door leaves it behind - so it is counted here.
            string gearLine = gearOnFloor > 0 ? $"\n{gearOnFloor} gear on the floor" : "";
            _waveText.text = $"FLOOR {floor}\nenemies {alive}    kills {kills}{xpLine}{gearLine}";
        }

        void Update()
        {
            // Ticked BEFORE the early return: a banner fires on a kill, and the player being dead
            // or mid-transition is exactly when the return below trips - so a flash left behind
            // that way would hang on screen until something else happened to clear it.
            TickFlash();

            if (_player == null || _player.Resource == null) return;

            // The crescents. The armour cost is read from the real multiplier rather than
            // reconstructed from the condition, so it can never drift from what Health applies.
            var hp = _player.Health;
            if (hp && _crescents != null)
                _crescents.Tick(hp.Current, hp.Max,
                    _armorCondition != null ? _armorCondition() : 1f,
                    _armorDamageMultiplier != null ? _armorDamageMultiplier() : 1f);
        }
    }
}
