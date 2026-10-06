using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// What each action's button is CALLED, for hint text.
    ///
    /// TEXT TOKENS, NOT SPRITES, and that is a deliberate match to what is already here: every
    /// keyboard hint in the project reads as a bracketed token - "[C]", "[TAB]", "[ ESC ]" - drawn
    /// by a plain uGUI Text. A drawn glyph would need either TMP inline sprites (which this project
    /// avoids on purpose, see the note about Essential Resources breaking clean checkouts) or
    /// splitting every hint label into interleaved text and image runs. Parenthesised tokens sit
    /// symmetrically beside the bracketed keyboard ones and cost nothing.
    ///
    /// XBOX NAMING BY DEFAULT, because the first non-browser target is EGS on desktop and that is
    /// the convention there. Unity's InputSystem names the face buttons by POSITION (South/East/
    /// West/North), which is what makes a PlayStation face-button label a change to
    /// <see cref="GamepadBindings.Label"/> rather than a hunt through call sites.
    ///
    /// THESE ARE READ LIVE OFF <see cref="GamepadBindings"/> RATHER THAN HARD-CODED, because a
    /// player can remap any of them from the controller configuration screen and every hint that
    /// still said "(A)" afterward would be showing a button that no longer does that. The defaults
    /// below match Core.Controls' own defaults; the properties themselves never go stale, because
    /// they never cache the answer.
    ///
    ///     Attack / Confirm   Attack           (A)
    ///     Interact / Release ReleaseInteract  (X)   - shares E's double duty on keyboard
    ///     Cancel             Cancel           (B)
    ///     Loadout            Loadout          (Y)
    ///     Mastery            Mastery          (LB)
    ///     Alt                Alt              (RB)
    ///     SecondAbility      SecondAbility    (RT)  - same physical button as Zoom's RT by
    ///                                                 default; the hub's "look closer" prompt is
    ///                                                 what reads this one, and the two contexts
    ///                                                 never overlap
    ///     Zoom               fixed, not rebindable   (LT) / (RT)
    ///     Settings           fixed, not rebindable   Start
    /// </summary>
    public static class GamepadGlyphs
    {
        public static string Attack => GamepadBindings.Label(GamepadBindings.Get(GamepadAction.Attack));
        public static string Confirm => Attack;
        public static string Interact => GamepadBindings.Label(GamepadBindings.Get(GamepadAction.ReleaseInteract));
        public static string Release => Interact;
        public static string Cancel => GamepadBindings.Label(GamepadBindings.Get(GamepadAction.Cancel));
        public static string Loadout => GamepadBindings.Label(GamepadBindings.Get(GamepadAction.Loadout));
        public static string Mastery => GamepadBindings.Label(GamepadBindings.Get(GamepadAction.Mastery));
        public static string Alt => GamepadBindings.Label(GamepadBindings.Get(GamepadAction.Alt));
        public static string SecondAbility => GamepadBindings.Label(GamepadBindings.Get(GamepadAction.SecondAbility));

        public const string ZoomOut = "(LT)";
        public const string ZoomIn = "(RT)";

        /// <summary>Opens the settings menu from the hub. Fixed rather than rebindable - see
        /// Controls.SettingsTapped's own note.</summary>
        public const string Settings = "(Start)";

        /// <summary>Sticks and the d-pad are named rather than glyphed - there is no short token
        /// for them that reads better than the words.</summary>
        public const string Move = "left stick";
        public const string Cursor = "right stick";
        public const string Navigate = "D-pad";
    }
}
