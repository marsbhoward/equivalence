using UnityEngine;

namespace Convergence.Core
{
    /// <summary>
    /// The player's choices about the on-screen controls and where the run HUD sits, kept in
    /// PlayerPrefs - a device preference, like the volumes and the pad bindings, not character
    /// state. Read every frame by the overlay (UI/TouchControls) and the HUD (UI/Hud), and set
    /// only by the HUD &amp; TOUCH screen (UI/HudSettingsScreen).
    ///
    /// The HUD LAYOUT is chosen twice, once for touch and once for keys and pad, because the
    /// right answer differs by device: on touch the rotation wheel can BE the movement stick, so
    /// the two stop fighting for the bottom-left corner, while on a keyboard the same corner is
    /// just a place to read it from. Which of the two applies follows
    /// <see cref="Controls.TouchMode"/> - the device in hand - not whether the overlay is drawn.
    /// </summary>
    public static class HudPrefs
    {
        /// <summary>When the on-screen stick and buttons are drawn. AUTO is the old behaviour:
        /// after a touch, until a key, click or pad press.</summary>
        public enum Overlay { Auto, Always, Off }

        /// <summary>
        /// Where the rotation wheel lives. CENTRED: meshed to the seal's left, the whole cluster at
        /// bottom centre between the thumbs. WHEEL: in the stick's corner - on touch it IS the
        /// stick (its rim the stick's travel, its hub the knob); on keys and pad, or with the
        /// overlay off, it just sits there.
        /// </summary>
        public enum Layout { Centred, Wheel }

        const string Prefix = "Equivalence.Hud.v1.";

        public static Overlay OnScreen
        {
            get => (Overlay)Get("overlay", (int)Overlay.Auto, 2);
            set => Set("overlay", (int)value);
        }

        public static Layout TouchLayout
        {
            get => (Layout)Get("layout.touch", (int)Layout.Wheel, 1);
            set => Set("layout.touch", (int)value);
        }

        public static Layout DeskLayout
        {
            get => (Layout)Get("layout.desk", (int)Layout.Centred, 1);
            set => Set("layout.desk", (int)value);
        }

        /// <summary>The layout for the device in hand.</summary>
        public static Layout Current => Controls.TouchMode ? TouchLayout : DeskLayout;

        /// <summary>0 small, 1 medium, 2 large.</summary>
        public static int Size
        {
            get => Get("size", 1, 2);
            set => Set("size", Mathf.Clamp(value, 0, 2));
        }

        static readonly float[] Scales = { 0.88f, 1f, 1.14f };
        public static float ButtonScale => Scales[Size];
        public static readonly string[] SizeNames = { "SMALL", "MEDIUM", "LARGE" };

        /// <summary>The stick on the right and the action buttons on the left. GEAR and MENU stay
        /// in the top-right: mirrored, they would sit on the run clock.</summary>
        public static bool LeftHanded
        {
            get => Get("left", 0, 1) == 1;
            set => Set("left", value ? 1 : 0);
        }

        /// <summary>The key (or pad button) under each button's name, off touch.</summary>
        public static bool KeyHints
        {
            get => Get("keys", 1, 1) == 1;
            set => Set("keys", value ? 1 : 0);
        }

        /// <summary>Bumped by every change, so a reader can re-lay itself out only when one
        /// lands instead of comparing every field every frame.</summary>
        public static int Version { get; private set; }

        // Cached: PlayerPrefs is a dictionary lookup at best and an IndexedDB-backed one in the
        // browser build, and the overlay and the HUD both read these every frame.
        static readonly System.Collections.Generic.Dictionary<string, int> _cache = new();

        static int Get(string key, int fallback, int max)
        {
            if (_cache.TryGetValue(key, out var v)) return v;
            try { v = PlayerPrefs.GetInt(Prefix + key, fallback); } catch { v = fallback; }
            if (v < 0 || v > max) v = fallback;
            _cache[key] = v;
            return v;
        }

        static void Set(string key, int value)
        {
            _cache[key] = value;
            Version++;
            try { PlayerPrefs.SetInt(Prefix + key, value); PlayerPrefs.Save(); } catch { }
        }
    }
}
