using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using Convergence.UI;

namespace Convergence.Combat
{
    /// <summary>
    /// Floating combat text for damage the PLAYER deals - hooked onto a body's own Damaged event
    /// rather than threaded through every attack path, the same "attach a listener, keep Health
    /// ignorant" shape StatusVisuals already uses for burn/soak/stagger.
    ///
    /// Player-driven is decided by GameObject identity, not by asking a body what kind of hit
    /// this is. Every player-sourced DamageInfo in the project - basics, finishers, echoes,
    /// thrown weapons, discs, elemental releases - carries Source (or the owner field feeding
    /// it) set to the player's own GameObject, so comparing against that reference reads
    /// correctly from any damage path without this file needing to know about it. Damage the
    /// player TAKES, and enemy-on-enemy damage, are deliberately silent - this is feedback for
    /// the player's own offense, not a combat log.
    /// </summary>
    public static class DamageNumbers
    {
        static GameObject _player;

        /// <summary>Set once from BuildPlayer. Null-safe elsewhere - no player means no numbers,
        /// which is also what a torn-down run should look like.</summary>
        public static void SetPlayer(GameObject player) => _player = player;

        /// <summary>
        /// Subscribe a body to spawn a number whenever the PLAYER hits it. Call once per Health
        /// at creation - EnemyFactory and Boss.CreateBody both do.
        /// </summary>
        public static void Attach(Health hp)
        {
            hp.Damaged += info => OnDamaged(hp, info);
        }

        static void OnDamaged(Health hp, DamageInfo info)
        {
            if (info.Amount <= 0f) return;
            if (_player == null || info.Source != _player) return;
            Spawn(hp.transform.position, info.Amount, info.IsFinisher, info.Crit);
        }

        static Canvas _canvas;

        static Canvas EnsureCanvas()
        {
            // Its own canvas rather than the HUD's: numbers are gameplay feedback tracking world
            // positions every frame, not a screen-anchored panel, and giving them a dedicated
            // canvas means nothing here has to know the HUD's own child order.
            if (_canvas == null) _canvas = UiKit.CreateCanvas("damage-numbers", 9);
            return _canvas;
        }

        /// <summary>Torn down between runs so a stale canvas (and any numbers still fading on
        /// it) doesn't survive into the next one - the same discipline Hud's own root follows.</summary>
        public static void Teardown()
        {
            if (_canvas != null) Object.Destroy(_canvas.gameObject);
            _canvas = null;
            _player = null;
        }

        /// <summary>Fog III: the numbers are hidden - the fight is read by feel. Set by the run's
        /// ledger, reset when the run ends.</summary>
        public static bool Hidden;

        public static void Spawn(Vector3 worldPos, float amount, bool finisher, bool crit = false)
        {
            if (Hidden) return;
            var canvas = EnsureCanvas();

            var go = new GameObject("dmg", typeof(Text));
            go.transform.SetParent(canvas.transform, false);

            var text = go.GetComponent<Text>();
            text.font = UiKit.Font;
            text.text = Mathf.RoundToInt(amount).ToString();
            int baseSize = finisher ? Tuning.DamageNumbers.FinisherFontSize : Tuning.DamageNumbers.FontSize;
            text.fontSize = crit ? Mathf.RoundToInt(baseSize * Tuning.DamageNumbers.CritSizeMul) : baseSize;
            text.fontStyle = FontStyle.Bold;
            text.color = finisher ? Tuning.DamageNumbers.FinisherColor : Tuning.DamageNumbers.Color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            // Anchored to the canvas's own centre rather than stretched to fill it (UiKit.Label's
            // shape, right for a panel and wrong here) - DamageNumberLabel repositions it every
            // frame via anchoredPosition, which only means "offset from anchor" if the anchor is
            // a fixed point rather than a stretched rect.
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 100f);

            var jitter = new Vector3(Random.Range(-Tuning.DamageNumbers.Jitter, Tuning.DamageNumbers.Jitter), 0f);
            go.AddComponent<DamageNumberLabel>().Begin(worldPos + jitter, crit);
        }
    }
}
