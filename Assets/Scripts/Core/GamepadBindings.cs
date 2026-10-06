using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Convergence.Core
{
    /// <summary>
    /// Every gamepad action a player can rebind, and the physical button each one is checking
    /// today by default. Not every action Controls reads is here - the sticks, the D-pad and the
    /// zoom triggers stay fixed, because a stick has nowhere else to go and the D-pad is spoken
    /// for by menu navigation - only the face buttons and the two shoulders/triggers that stand
    /// in for a keyboard key are offered up for remapping.
    /// </summary>
    public enum GamepadAction
    {
        Attack,           // also Confirm and the gamepad cursor's click - one physical button, as today
        ReleaseInteract,  // Release ability / hub Interact - shares E's double duty on keyboard too
        SecondAbility,
        Cancel,
        Loadout,
        Mastery,
        Alt,
    }

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
            { GamepadAction.Attack,          GamepadButtonId.South },
            { GamepadAction.ReleaseInteract, GamepadButtonId.West },
            { GamepadAction.SecondAbility,   GamepadButtonId.RightTrigger },
            { GamepadAction.Cancel,          GamepadButtonId.East },
            { GamepadAction.Loadout,         GamepadButtonId.North },
            { GamepadAction.Mastery,         GamepadButtonId.LeftShoulder },
            { GamepadAction.Alt,             GamepadButtonId.RightShoulder },
        };

        static Dictionary<GamepadAction, GamepadButtonId> _map;
        const string PrefPrefix = "Coalescence.GamepadBind.";

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
        /// Binds <paramref name="action"/> to <paramref name="button"/>. If another action already
        /// reads that button, the two SWAP rather than both silently reading the same press - two
        /// actions sharing one button is never what "map buttons" is asking for, and a swap is the
        /// only outcome that leaves every action still bound to exactly one thing.
        /// </summary>
        public static void Set(GamepadAction action, GamepadButtonId button)
        {
            EnsureLoaded();
            GamepadAction? holder = null;
            foreach (var kv in _map)
            {
                if (kv.Key != action && kv.Value == button) { holder = kv.Key; break; }
            }

            var previous = _map[action];
            _map[action] = button;
            if (holder.HasValue) _map[holder.Value] = previous;

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
            GamepadAction.Attack => "Attack / Confirm",
            GamepadAction.ReleaseInteract => "Release Ability / Interact",
            GamepadAction.SecondAbility => "Look Closer (hub)",
            GamepadAction.Cancel => "Cancel",
            GamepadAction.Loadout => "Loadout",
            GamepadAction.Mastery => "Mastery",
            GamepadAction.Alt => "Alt (Guard / Pick Up)",
            _ => action.ToString(),
        };
    }
}
