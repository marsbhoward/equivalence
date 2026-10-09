using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Convergence.Core
{
    /// <summary>
    /// Every gamepad action a player can rebind, and the physical button each one is checking
    /// today by default. Not every action Controls reads is here - the sticks, the D-pad, the
    /// zoom triggers and Start/Select stay fixed, because a stick has nowhere else to go, the
    /// D-pad is spoken for by menu navigation, and a menu button has to work before the player
    /// has looked at the rebind screen (see <see cref="Controls.SettingsTapped"/>).
    ///
    /// Saved BY NAME, so appending or reordering here never shifts a saved binding.
    /// </summary>
    public enum GamepadAction
    {
        Attack,
        Ability,        // the element release - its own button, never shared with Interact
        Interact,       // use a fixture, pick up, open a Rift; a weapon art's mid-move choice (the blade blink)
        Guard,          // the chest's defensive ability
        SecondAbility,  // the hub's "look closer" (VIEW)
        Mastery,
        Alt,            // the hub's edit mode / pick up a frame
        Confirm,        // a menu's click on the focused entry
        Cancel,         // backs out of a menu
    }

    /// <summary>Where an action is read. Two actions may share a button only when they never
    /// share a context - B guards in a fight, looks closer in the hub, backs out of a menu.</summary>
    [System.Flags]
    public enum GamepadContext { Arena = 1, Hub = 2, Menu = 4 }

    /// <summary>A physical button, named by position rather than by label - InputSystem's own
    /// convention, and what makes a PlayStation face-button swap a change to <see cref="GamepadBindings.Name"/>
    /// rather than a hunt through every call site that names a button.</summary>
    public enum GamepadButtonId
    {
        South, North, East, West,
        LeftShoulder, RightShoulder,
        LeftTrigger, RightTrigger,
    }

    /// <summary>
    /// Which physical button each <see cref="GamepadAction"/> reads, player-configurable and
    /// saved to <see cref="PlayerPrefs"/> - a device preference, not account or character state,
    /// so it is unaffected by which wallet is connected or which character is being played and
    /// never rides a chain checkpoint.
    ///
    /// THIS IS THE ONE OTHER FILE ALLOWED TO NAME A PHYSICAL GAMEPAD CONTROL BY IDENTITY, and
    /// that is a narrow, deliberate exception to <see cref="Controls"/>' own "nothing outside
    /// this file touches a device" rule. Binding CONFIGURATION has to live somewhere, and the
    /// enum-to-control lookup is that configuration's only reason to exist - every actual
    /// <c>isPressed</c>/<c>wasPressedThisFrame</c> read still happens inside <see cref="Controls"/>,
    /// which calls <see cref="Control"/> rather than naming a button itself for anything in
    /// <see cref="GamepadAction"/>. <see cref="GamepadGlyphs"/> mirrors this rather than the other
    /// way round, so a hint label is never stale once a player has remapped anything.
    /// </summary>
    public static class GamepadBindings
    {
        static readonly Dictionary<GamepadAction, GamepadButtonId> Defaults = new()
        {
            { GamepadAction.Attack,        GamepadButtonId.RightTrigger },
            { GamepadAction.Ability,       GamepadButtonId.LeftTrigger },
            { GamepadAction.Interact,      GamepadButtonId.South },
            { GamepadAction.Guard,         GamepadButtonId.East },
            { GamepadAction.SecondAbility, GamepadButtonId.East },
            { GamepadAction.Mastery,       GamepadButtonId.LeftShoulder },
            { GamepadAction.Alt,           GamepadButtonId.RightShoulder },
            { GamepadAction.Confirm,       GamepadButtonId.South },
            { GamepadAction.Cancel,        GamepadButtonId.East },
        };

        public static GamepadContext ContextOf(GamepadAction action) => action switch
        {
            GamepadAction.Attack or GamepadAction.Ability or GamepadAction.Guard => GamepadContext.Arena,
            GamepadAction.Interact => GamepadContext.Arena | GamepadContext.Hub,
            GamepadAction.SecondAbility or GamepadAction.Mastery or GamepadAction.Alt => GamepadContext.Hub,
            _ => GamepadContext.Menu,
        };

        static Dictionary<GamepadAction, GamepadButtonId> _map;
        // v2: Ability and Interact split (they shared one slot) and the defaults moved to the
        // triggers - a v1 save would pin Attack back on A, so it is left behind rather than read.
        const string PrefPrefix = "Equivalence.GamepadBind.v2.";

        static void EnsureLoaded()
        {
            if (_map != null) return;
            _map = new Dictionary<GamepadAction, GamepadButtonId>(Defaults);
            foreach (var action in Defaults.Keys)
            {
                string key = PrefPrefix + action;
                if (!PlayerPrefs.HasKey(key)) continue;
                int raw = PlayerPrefs.GetInt(key);
                if (System.Enum.IsDefined(typeof(GamepadButtonId), raw))
                    _map[action] = (GamepadButtonId)raw;
            }
        }

        public static GamepadButtonId Get(GamepadAction action)
        {
            EnsureLoaded();
            return _map[action];
        }

        /// <summary>
        /// Binds <paramref name="action"/> to <paramref name="button"/>. Any action read in the
        /// same context that already reads that button SWAPS to this one's old button rather than
        /// both silently reading the same press. Actions in other contexts keep it - sharing
        /// across contexts is what lets B guard in a fight and back out of a menu. Each context
        /// held the button at most once, so every holder can take the old button without a clash.
        /// </summary>
        public static void Set(GamepadAction action, GamepadButtonId button)
        {
            EnsureLoaded();
            var ctx = ContextOf(action);
            var holders = new List<GamepadAction>();
            foreach (var kv in _map)
            {
                if (kv.Key != action && kv.Value == button && (ContextOf(kv.Key) & ctx) != 0)
                    holders.Add(kv.Key);
            }

            var previous = _map[action];
            _map[action] = button;
            foreach (var h in holders) _map[h] = previous;

            Save();
        }

        public static void ResetToDefault()
        {
            _map = new Dictionary<GamepadAction, GamepadButtonId>(Defaults);
            Save();
        }

        static void Save()
        {
            foreach (var kv in _map)
                PlayerPrefs.SetInt(PrefPrefix + kv.Key, (int)kv.Value);
            PlayerPrefs.Save();
        }

        public static IReadOnlyList<GamepadAction> AllActions { get; } =
            new List<GamepadAction>(Defaults.Keys);

        /// <summary>Every button this project offers up for remapping - the pool a capture polls
        /// and the rebind screen lists as a target.</summary>
        public static readonly GamepadButtonId[] AllButtons =
        {
            GamepadButtonId.South, GamepadButtonId.North, GamepadButtonId.East, GamepadButtonId.West,
            GamepadButtonId.LeftShoulder, GamepadButtonId.RightShoulder,
            GamepadButtonId.LeftTrigger, GamepadButtonId.RightTrigger,
        };

        /// <summary>The live Unity control a button id refers to on the given pad, or null if
        /// there is no pad. The only place this project maps the enum to a real InputSystem
        /// control - see this class's own header for why that centralisation matters.</summary>
        public static ButtonControl Control(Gamepad pad, GamepadButtonId id)
        {
            if (pad == null) return null;
            return id switch
            {
                GamepadButtonId.South => pad.buttonSouth,
                GamepadButtonId.North => pad.buttonNorth,
                GamepadButtonId.East => pad.buttonEast,
                GamepadButtonId.West => pad.buttonWest,
                GamepadButtonId.LeftShoulder => pad.leftShoulder,
                GamepadButtonId.RightShoulder => pad.rightShoulder,
                GamepadButtonId.LeftTrigger => pad.leftTrigger,
                GamepadButtonId.RightTrigger => pad.rightTrigger,
                _ => null,
            };
        }

        /// <summary>Bracketed hint-text token, matching every keyboard hint's own style.</summary>
        public static string Label(GamepadButtonId id) => id switch
        {
            GamepadButtonId.South => "(A)",
            GamepadButtonId.North => "(Y)",
            GamepadButtonId.East => "(B)",
            GamepadButtonId.West => "(X)",
            GamepadButtonId.LeftShoulder => "(LB)",
            GamepadButtonId.RightShoulder => "(RB)",
            GamepadButtonId.LeftTrigger => "(LT)",
            GamepadButtonId.RightTrigger => "(RT)",
            _ => "(?)",
        };

        /// <summary>A full name for the rebind list, where a bracketed token would read as noise
        /// next to eight of them stacked in a column.</summary>
        public static string Name(GamepadButtonId id) => id switch
        {
            GamepadButtonId.South => "A / Cross",
            GamepadButtonId.North => "Y / Triangle",
            GamepadButtonId.East => "B / Circle",
            GamepadButtonId.West => "X / Square",
            GamepadButtonId.LeftShoulder => "Left Bumper",
            GamepadButtonId.RightShoulder => "Right Bumper",
            GamepadButtonId.LeftTrigger => "Left Trigger",
            GamepadButtonId.RightTrigger => "Right Trigger",
            _ => "?",
        };

        public static string ActionName(GamepadAction action) => action switch
        {
            GamepadAction.Attack => "Attack",
            GamepadAction.Ability => "Ability",
            GamepadAction.Interact => "Action / Use",
            GamepadAction.Guard => "Defensive Ability",
            GamepadAction.SecondAbility => "View (hub)",
            GamepadAction.Mastery => "Mastery (hub)",
            GamepadAction.Alt => "Edit / Pick Up (hub)",
            GamepadAction.Confirm => "Menu Select",
            GamepadAction.Cancel => "Menu Back",
            _ => action.ToString(),
        };
    }
}
