using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using Convergence.Player;

namespace Convergence.UI
{
    /// <summary>
    /// In-run HUD. The resource meter deliberately renders differently per element - discrete
    /// pips for fire's stacking icons, appear-on-reach tier icons for water, a plain bar for
    /// earth and air - so the four rhythms read as different before combat even starts.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        PlayerController _player;

        Image _healthFill;
        Text _healthText;
        Image _resourceFill;
        RectTransform _resourceRow;
        readonly List<Image> _pips = new();

        /// <summary>Appear-when-reached tier icons (water). Empty for every other element.</summary>
        readonly List<Image> _tierIcons = new();
        Text _resourceLabel, _statusLine, _waveText, _comboText;
        Image _armorFill;
        Text _armorText;
        Image _attackIcon, _attackIconRing;
        Text _attackKind, _attackName;
        Combat.Glyph _shownGlyph = (Combat.Glyph)(-1);

        // What each BUILT label was last built FROM. UiKit.SetText stops a redundant assignment
        // from dirtying the canvas, but the string has already been allocated by the time it is
        // called - these stop it being built at all on the frames nothing it says has changed,
        // which is nearly all of them.
        //
        // Every int starts at a value the real one cannot take, so the first frame always writes.
        // The moveset/step entries are compared by REFERENCE against stable stored objects
        // (ActiveMoveset indexes Slots; NextStep indexes the moveset's own arrays), never
        // rebuilt per call, so the comparison is meaningful.
        //
        // The failure direction is deliberate and one-way: if any of these is lost - a domain
        // reload empties the object references, say - the comparison fails and the label is
        // rebuilt. Costing one extra rebuild is the worst this can do; it can never leave a
        // stale label on screen, which is the only outcome that would matter.
        bool _namedFinisher;
        int _namedComboIndex = -1, _namedBasics = -1;
        Combat.Moveset _namedMoveset;

        bool _comboFinisher;
        int _comboEarned = -1, _comboSlots = -1;
        Combat.Moveset _comboMoveset;
        Combat.AttackStep _comboStep;

        int _armorPct = -1, _armorMul = -1;
        int _shownStreak = -1;
        Text _streakText;
        int _shownHp = -1, _shownHpMax = -1;

        Text _slotStrip;
        readonly List<Image> _slotIcons = new();
        RectTransform _rotPanel;

        /// <summary>
        /// One icon per slot, sized so the strip still fits its panel as the wheel grows. Rebuilt
        /// rather than appended to, because the icons have to shrink and re-space when a slot is
        /// added - appending would run the last one off the end of the panel.
        /// </summary>
        void RebuildSlotIcons(int count)
        {
            if (_rotPanel == null) return;

            foreach (var img in _slotIcons)
                if (img != null) Destroy(img.gameObject);
            _slotIcons.Clear();

            count = Mathf.Max(1, count);
            const float pad = 16f, avail = 296f - pad * 2f;
            float gap = count > 4 ? 8f : 12f;
            float size = Mathf.Min(34f, (avail - gap * (count - 1)) / count);

            for (int i = 0; i < count; i++)
            {
                float x = pad + i * (size + gap);
                var slotRt = UiKit.Rect(_rotPanel, $"slot{i}", new Vector2(0, 0), new Vector2(0, 0),
                    new Vector2(x, 12), new Vector2(x + size, 12 + size));
                var img = slotRt.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                _slotIcons.Add(img);
            }
        }
        readonly List<Image> _comboPips = new();
        System.Func<float> _armorCondition;
        System.Func<float> _armorDamageMultiplier;
        System.Func<int> _bonusXp;

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
                                   System.Func<int> bonusXp)
        {
            _armorCondition = armor;
            _armorDamageMultiplier = damageMultiplier;
            _bonusXp = bonusXp;
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
            var element = player.Resource.Element;
            var tint = ElementInfo.Tint(element);

            // ---------- resource block, top-left ----------
            var block = UiKit.Panel(canvas, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(32, -196), new Vector2(560, -32), new Color(0.05f, 0.06f, 0.09f, 0.82f));

            var title = UiKit.Rect(block, "title", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(20, -46), new Vector2(-20, -12));
            UiKit.Label(title, $"{element.ToString().ToUpper()}  -  {ElementInfo.ResourceName(element)}", 26, tint);

            _resourceRow = UiKit.Rect(block, "meter", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(20, -92), new Vector2(-20, -56));

            var statusRow = UiKit.Rect(block, "status", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(20, -126), new Vector2(-20, -96));
            _statusLine = UiKit.Label(statusRow, "", 19, new Color(0.85f, 0.87f, 0.92f));

            var hintRow = UiKit.Rect(block, "hint", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(20, 12), new Vector2(-20, 42));
            UiKit.Label(hintRow, ElementInfo.Tagline(element), 15, new Color(0.55f, 0.58f, 0.66f));

            BuildMeter(player.Resource, tint);

            // ---------- wave / kills, top-right ----------
            // Stepped left of GEAR and MENU, which sit in the corner on every device now.
            var right = UiKit.Rect(canvas, "wave", new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-768, -80), new Vector2(-380, -26));
            _waveText = UiKit.Label(right, "", 24, new Color(0.8f, 0.83f, 0.9f), TextAnchor.UpperRight);

            // ---------- health, bottom-centre ----------
            var hpTrack = UiKit.Rect(canvas, "health", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-300, 42), new Vector2(300, 74));
            _healthFill = UiKit.Bar(hpTrack, new Color(0.85f, 0.25f, 0.35f), new Color(0.12f, 0.10f, 0.13f, 0.9f));

            // ---------- armour, directly above health ----------
            // Same track width so the two read as one stack, but deliberately THINNER and a cool
            // steel colour: armour is the buffer in front of health, not a second health bar, and
            // it must never be mistaken for one at a glance mid-fight.
            var armTrack = UiKit.Rect(canvas, "armour", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-300, 78), new Vector2(300, 98));
            _armorFill = UiKit.Bar(armTrack, new Color(0.62f, 0.70f, 0.82f), new Color(0.11f, 0.12f, 0.16f, 0.9f));

            // Both readouts share the row above the bars. Laid OUTSIDE the armour track on
            // purpose: text inside it has to stay legible against a light fill when armour is
            // high and a dark empty track when it is low, and no single colour does both - least
            // of all at low armour, which is exactly when the number needs reading.
            var hpLabel = UiKit.Rect(canvas, "healthText", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-300, 102), new Vector2(300, 126));
            _healthText = UiKit.Label(hpLabel, "", 18, new Color(0.85f, 0.87f, 0.92f), TextAnchor.LowerCenter);

            var armLabel = UiKit.Rect(canvas, "armourText", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-300, 102), new Vector2(300, 124));
            _armorText = UiKit.Label(armLabel, "", 14, new Color(0.62f, 0.70f, 0.82f), TextAnchor.LowerRight);

            // ---------- pending swing + rotation, top-right ----------
            // Everything about WHAT THE ATTACK BUTTON DOES lives in this corner, stacked: the
            // big pending-swing readout on top, the queue of what follows underneath. They were
            // split across opposite corners, so answering "what am I about to do, and what comes
            // after" meant reading both sides of the screen at once. The left corner is now free
            // for the resource meter to own outright.
            // Wider than the old left-hand version: the moveset name sits to the right of the
            // icon and long ones ("Piercing Lunge", "Cleaving Arc") used to spill past the panel.
            // In the left corner that overflow ran harmlessly into open screen; against the right
            // edge it runs off it.
            //
            // At the TOP right, stacked below GEAR and MENU: the bottom-right corner belongs to
            // the action buttons, which TouchControls draws on every device (live buttons on
            // touch, key-labelled readouts otherwise). Measured against the overlay's layout:
            // the action buttons occupy roughly x -552..-132 up to y 482, and the two small ones
            // x -364..-46 down to y -194.
            var iconPanel = UiKit.Panel(canvas, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-320, -350), new Vector2(-24, -220), new Color(0.05f, 0.06f, 0.09f, 0.88f));

            var ringRt = UiKit.Rect(iconPanel, "ring", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(14, -110), new Vector2(110, -14));
            _attackIconRing = ringRt.gameObject.AddComponent<Image>();
            _attackIconRing.sprite = Spr.Ring;
            _attackIconRing.raycastTarget = false;

            var iconRt = UiKit.Rect(iconPanel, "icon", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(30, -94), new Vector2(94, -30));
            _attackIcon = iconRt.gameObject.AddComponent<Image>();
            _attackIcon.raycastTarget = false;
            _attackIcon.preserveAspect = true;

            var kindRt = UiKit.Rect(iconPanel, "kind", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(118, -52), new Vector2(-10, -18));
            _attackKind = UiKit.Label(kindRt, "", 17, new Color(0.5f, 0.53f, 0.6f));

            var nameRt = UiKit.Rect(iconPanel, "name", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(118, -92), new Vector2(-10, -52));
            _attackName = UiKit.Label(nameRt, "", 21, new Color(0.9f, 0.92f, 0.96f));

            // Directly beneath the pending-swing panel, same width, reading as one column.
            var rotPanel = UiKit.Panel(canvas, new Vector2(1, 1), new Vector2(1, 1),
                new Vector2(-320, -428), new Vector2(-24, -358), new Color(0.05f, 0.06f, 0.09f, 0.88f));

            _rotPanel = rotPanel;
            RebuildSlotIcons(player.Slots.Count);

            var rotLabel = UiKit.Rect(rotPanel, "rot", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -26), new Vector2(-12, -6));
            // "next up", not "rotation": the strip is now a QUEUE in firing order rather than
            // three fixed slots with one of them lit, so the name has to describe what position
            // in it MEANS - leftmost fires next, then the one after it.
            _slotStrip = UiKit.Label(rotLabel, "next up", 13, new Color(0.4f, 0.43f, 0.5f),
                TextAnchor.MiddleLeft);

            // ---------- combo chain, above the health/armour stack ----------
            // Raised to clear the armour bar and the health readout that now sit beneath it.
            var comboRow = UiKit.Rect(canvas, "combo", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-140, 134), new Vector2(140, 152));
            // One pip per step of the chain: swing, alt-swing, finisher. Derived from
            // BasicsPerChain rather than hardcoded, so shortening the chain cannot leave a fourth
            // pip on screen that nothing will ever fill.
            // Built for the LONGEST chain the run could reach, then trimmed each frame to the
            // live length. The chain is no longer fixed - Short Chain and Long Chain move it
            // mid-run - and this row is built once when the HUD is, so sizing it to the chain
            // length at spawn would strand a pip nothing can fill (or hide one that fires).
            int chainSteps = PlayerController.MaxChainSteps;
            for (int i = 0; i < chainSteps; i++)
            {
                float w = 1f / chainSteps;
                var slot = UiKit.Rect(comboRow, $"c{i}", new Vector2(i * w, 0), new Vector2((i + 1) * w, 1),
                    new Vector2(i == 0 ? 0 : 4, 0), new Vector2(i == chainSteps - 1 ? 0 : -4, 0));
                // The last pip is the finisher, so it reads differently.
                _comboPips.Add(UiKit.Bar(slot,
                    i == chainSteps - 1 ? new Color(1f, 0.85f, 0.35f) : new Color(0.8f, 0.83f, 0.9f),
                    new Color(0.13f, 0.14f, 0.18f, 0.95f)));
            }

            // The perfect streak, beside the chain it is earned on - the meter's pips show it
            // only while a finisher winds up; this carries it between. Empty at zero.
            var streakRt = UiKit.Rect(canvas, "streak", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(150, 130), new Vector2(340, 154));
            _streakText = UiKit.Label(streakRt, "", 17, Tuning.StrikeTiming.PipLit, TextAnchor.MiddleLeft);

            var comboPanel = UiKit.Panel(canvas, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-260, 156), new Vector2(260, 186), new Color(0.05f, 0.06f, 0.09f, 0.82f));
            var comboLabel = UiKit.Rect(comboPanel, "comboLabel", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(10, 0), new Vector2(-10, 0));
            _comboText = UiKit.Label(comboLabel, "", 17, new Color(0.78f, 0.81f, 0.88f), TextAnchor.MiddleCenter);

            // The gear-condition text block that used to sit bottom-right is gone: armour is now
            // the bar above health, and weapon wear is switched off entirely (see Durability).

            var controls = UiKit.Rect(canvas, "controls", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-420, 10), new Vector2(420, 36));
            UiKit.Hint(controls,
                "WASD move    -    LMB / SPACE attack    -    RMB / E element ability    -    [Q] guard    -    [C] loadout    -    ESC abandon run",
                "stick to move    -    ATTACK    -    ABILITY    -    GUARD    -    GEAR for your loadout    -    MENU to leave the run",
                15, new Color(0.45f, 0.48f, 0.56f), TextAnchor.LowerCenter,
                UI.GamepadGlyphs.Move + " move    -    " + UI.GamepadGlyphs.Attack + " attack    -    "
                + UI.GamepadGlyphs.Release + " element ability    -    " + UI.GamepadGlyphs.Alt + " guard    -    "
                + UI.GamepadGlyphs.Loadout + " loadout    -    " + UI.GamepadGlyphs.Cancel + " abandon run");
        }

        void BuildMeter(ElementalResource resource, Color tint)
        {
            var track = new Color(0.13f, 0.14f, 0.18f, 0.95f);

            var glyphs = resource.PipGlyphs;

            if (resource.PipCount > 0 && glyphs != null && resource.PipsAppearWhenReached)
            {
                // Water: one icon per tier, each hidden until that tier is actually reached.
                //
                // Laid out at a fixed size from the left rather than stretched across the row:
                // these appear one at a time, and slots that divide the full width would leave
                // a lone icon stranded in the middle of an empty strip.
                const float size = 34f, gap = 12f;
                for (int i = 0; i < resource.PipCount; i++)
                {
                    float x = i * (size + gap);
                    var slot = UiKit.Rect(_resourceRow, $"tier{i}", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                        new Vector2(x, -size * 0.5f), new Vector2(x + size, size * 0.5f));

                    var img = slot.gameObject.AddComponent<Image>();
                    img.sprite = Combat.Glyphs.Get(i < glyphs.Length ? glyphs[i] : Combat.Glyph.Basic);
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    img.enabled = false;          // nothing reached yet
                    _tierIcons.Add(img);
                }
            }
            else if (resource.PipCount > 0)
            {
                // Fire: discrete stacking icons.
                int n = resource.PipCount;
                float gap = 8f;
                for (int i = 0; i < n; i++)
                {
                    float w = 1f / n;
                    var slot = UiKit.Rect(_resourceRow, $"pip{i}", new Vector2(i * w, 0), new Vector2((i + 1) * w, 1),
                        new Vector2(i == 0 ? 0 : gap * 0.5f, 0), new Vector2(i == n - 1 ? 0 : -gap * 0.5f, 0));
                    var img = UiKit.Bar(slot, tint, track);
                    _pips.Add(img);
                }
            }
            else
            {
                _resourceFill = UiKit.Bar(_resourceRow, tint, track);

                // Water: tier notches drawn over the bar.
                foreach (var n in resource.Notches)
                {
                    var notch = UiKit.Rect(_resourceRow, "notch", new Vector2(n, 0), new Vector2(n, 1),
                        new Vector2(-1.5f, 2), new Vector2(1.5f, -2));
                    var img = notch.gameObject.AddComponent<Image>();
                    img.color = new Color(0.02f, 0.02f, 0.03f, 0.9f);
                    img.raycastTarget = false;
                }
            }
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

        public void SetFloorInfo(int floor, int alive, int kills)
        {
            if (!_waveText) return;
            // Only shown once some has actually been banked.
            int xp = _bonusXp != null ? _bonusXp() : 0;
            string xpLine = xp > 0 ? $"\n+{xp} XP banked" : "";
            _waveText.text = $"FLOOR {floor}\nenemies {alive}    kills {kills}{xpLine}";
        }

        void Update()
        {
            // Ticked BEFORE the early return: a banner fires on a kill, and the player being dead
            // or mid-transition is exactly when the return below trips - so a flash left behind
            // that way would hang on screen until something else happened to clear it.
            TickFlash();

            if (_player == null || _player.Resource == null) return;
            var r = _player.Resource;

            if (_resourceFill) UiKit.SetFill(_resourceFill, r.Fill01);

            for (int i = 0; i < _pips.Count; i++)
                UiKit.SetFill(_pips[i], i < r.PipsFilled ? 1f : 0f);

            // Tier icons are present or absent, not filled or empty - an unreached tier is not
            // a thing the player can do anything with, so it is not on screen at all.
            for (int i = 0; i < _tierIcons.Count; i++)
            {
                bool reached = i < r.PipsFilled;
                UiKit.SetEnabled(_tierIcons[i], reached);

                // The HIGHEST reached tier is the one the button will actually fire, so it is
                // the only one lit; the ones below it are still spendable but are not what a
                // press does right now.
                if (reached)
                    UiKit.SetColor(_tierIcons[i], i == r.PipsFilled - 1
                        ? new Color(1f, 0.85f, 0.35f)
                        : new Color(0.62f, 0.72f, 0.85f, 0.75f));
            }

            UiKit.SetText(_statusLine, r.StatusLine);

            // Pending-swing icon. The sprite is only swapped when the glyph actually changes -
            // Image.sprite assignment dirties the canvas, and this runs every frame.
            var glyph = _player.NextGlyph;
            if (glyph != _shownGlyph)
            {
                _shownGlyph = glyph;
                _attackIcon.sprite = Combat.Glyphs.Get(glyph);
            }

            bool finisherNext = _player.FinisherNext;
            var iconTint = finisherNext ? new Color(1f, 0.85f, 0.35f) : new Color(0.82f, 0.85f, 0.92f);
            UiKit.SetColor(_attackIcon, iconTint);
            UiKit.SetColor(_attackIconRing, finisherNext
                ? new Color(1f, 0.85f, 0.35f, 0.85f)
                : new Color(0.4f, 0.43f, 0.5f, 0.5f));

            UiKit.SetText(_attackKind, finisherNext ? "WEAPON ART" : "BASIC");
            UiKit.SetColor(_attackKind, iconTint * 0.85f);

            // Guarded on its INPUTS, not on the finished string. UiKit.SetText already stops the
            // canvas rebuild, but it cannot stop the interpolation below from allocating a string
            // first - and this label is rebuilt from scratch every frame to say a thing that
            // changes a handful of times per chain.
            if (_attackName && (finisherNext != _namedFinisher
                                || _player.ComboIndex != _namedComboIndex
                                || _player.BasicsPerChain != _namedBasics
                                || !ReferenceEquals(_player.ActiveMoveset, _namedMoveset)))
            {
                _namedFinisher = finisherNext;
                _namedComboIndex = _player.ComboIndex;
                _namedBasics = _player.BasicsPerChain;
                _namedMoveset = _player.ActiveMoveset;

                UiKit.SetText(_attackName, finisherNext
                    ? _player.ActiveMoveset.DisplayName
                    // The lead-in is its own move, picked to set up this finisher, so it is named
                    // (Rise, Wind, ...). Every other basic is the opener or a repeat of it.
                    : $"{(_player.LeadInNext ? _player.NextStep.Name : "Swing")}  " +
                      $"{_player.ComboIndex + 1}/{_player.BasicsPerChain}");
            }

            // The strip is a QUEUE in firing order, not three fixed slots. Position 0 is what the
            // NEXT press produces - a basic while the chain is still building, this chain's
            // finisher once it is banked - and each position after it is the finisher of the
            // chain after that.
            //
            // It used to light whichever slot equalled RotationIndex, in fixed positions. That is
            // a true statement about the rotation and a useless one at the moment of pressing:
            // mid-chain it lit a finisher that was still swings away, so the highlight and
            // the big icon above it disagreed about what the button was about to do.
            int slotCount = _player.Slots.Count;
            // The strip is built at run start, and the wheel can grow after it - Extra Sigil
            // arrives between floors. Without this the extra slot simply never got an icon.
            if (_slotIcons.Count != slotCount) RebuildSlotIcons(slotCount);

            // Fog II (the exchange) hides the chain and the rotation: the player counts their
            // own swings and remembers their own wheel.
            bool silent = _player.Ledger.HideChainHud;
            if (silent && _attackName) UiKit.SetText(_attackName, "");

            for (int i = 0; i < _slotIcons.Count; i++)
            {
                // A pending basic occupies position 0 and pushes the finishers along one.
                bool basicHere = !finisherNext && i == 0;
                int rotOffset = finisherNext ? i : i - 1;
                var slot = _player.Slots[((_player.RotationIndex + rotOffset) % slotCount + slotCount) % slotCount];

                UiKit.SetSprite(_slotIcons[i], Combat.Glyphs.Get(
                    basicHere ? Combat.Glyph.Basic : slot.FinisherGlyph));

                bool active = i == 0;
                UiKit.SetColor(_slotIcons[i], silent ? Color.clear
                    : active
                    ? iconTint                                        // matches the big icon exactly
                    : slot.IsDefault ? new Color(0.4f, 0.43f, 0.5f, 0.6f)
                                     : new Color(0.62f, 0.65f, 0.72f, 0.85f));
            }

            // Chain: filled pips show swings already landed in this chain.
            var ms = _player.ActiveMoveset;
            // Trim to the chain's current length: the last pip is always the finisher, so the
            // ones between the live basics and it are the ones that get hidden.
            int live = _player.BasicsPerChain + 1;
            for (int i = 0; i < _comboPips.Count; i++)
            {
                var pip = _comboPips[i];
                bool used = i < live && !silent;
                var track = pip.transform.parent.gameObject;
                if (track.activeSelf != used) track.SetActive(used);
                if (used) UiKit.SetFill(pip, i < _player.ComboIndex ? 1f : 0f);
            }

            if (_streakText && _player.PerfectStreak != _shownStreak)
            {
                _shownStreak = _player.PerfectStreak;
                // "x" not a multiplication sign - the legacy font has no reliable glyph for it.
                UiKit.SetText(_streakText, _shownStreak == 0 ? "" :
                    $"PERFECT x{_shownStreak}   +{Mathf.RoundToInt(_player.StreakCritBonus * 100f)}% crit");
                UiKit.SetColor(_streakText, _shownStreak >= Tuning.StrikeTiming.StreakCap
                    ? Tuning.StrikeTiming.Perfect : Tuning.StrikeTiming.PipLit);
            }

            // Input-guarded for the same reason as _attackName above.
            if (_comboText && (finisherNext != _comboFinisher
                               || _player.EarnedCount != _comboEarned
                               || slotCount != _comboSlots
                               || !ReferenceEquals(ms, _comboMoveset)
                               || !ReferenceEquals(_player.NextStep, _comboStep)))
            {
                _comboFinisher = finisherNext;
                _comboEarned = _player.EarnedCount;
                _comboSlots = slotCount;
                _comboMoveset = ms;
                _comboStep = _player.NextStep;

                UiKit.SetText(_comboText,
                    $"{ms.DisplayName}   -   earned {_player.EarnedCount}/{_player.Slots.Count}" +
                    $"   -   next: {_player.NextStep.Name}{(finisherNext ? "  <- WEAPON ART" : "")}");
            }

            if (_armorFill != null && _armorCondition != null)
            {
                float a = _armorCondition();
                UiKit.SetFill(_armorFill, a);

                // The penalty stays on the label because the bar alone only says "how worn" -
                // it cannot say what that costs, and the cost is the reason to care. Read from
                // the real multiplier rather than reconstructed from `a` here, so this can never
                // again drift from what Health actually applies.
                float mul = _armorDamageMultiplier != null ? _armorDamageMultiplier() : 1f;

                // Keyed on what the label actually PRINTS, not on the raw floats: both are
                // rounded on the way out, so a bar drifting in the sixth decimal would otherwise
                // rebuild a string that reads identically.
                int shownPct = Mathf.RoundToInt(a * 100f);
                int shownMul = Mathf.RoundToInt(mul * 100f);
                if (_armorText && (shownPct != _armorPct || shownMul != _armorMul))
                {
                    _armorPct = shownPct;
                    _armorMul = shownMul;
                    UiKit.SetText(_armorText, $"ARMOUR {a * 100f:0}%   dmg taken x{mul:0.00}");
                }
            }

            var hp = _player.Health;
            if (hp)
            {
                UiKit.SetFill(_healthFill, hp.Current / hp.Max);

                // Likewise keyed on the printed integers - health is a float that moves on most
                // frames of a fight and reads the same for most of them.
                int cur = Mathf.CeilToInt(hp.Current), max = Mathf.CeilToInt(hp.Max);
                if (cur != _shownHp || max != _shownHpMax)
                {
                    _shownHp = cur;
                    _shownHpMax = max;
                    UiKit.SetText(_healthText, $"{cur} / {max}");
                }
            }
        }
    }
}
