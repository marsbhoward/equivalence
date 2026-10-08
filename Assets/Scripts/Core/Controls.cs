using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Convergence.Core
{
    /// <summary>
    /// Every input the game reads, in one place, with the device it came from made irrelevant.
    ///
    /// This exists because the project was written against <c>Keyboard.current</c> and
    /// <c>Mouse.current</c> directly, in eighteen places, and A PHONE HAS NEITHER. Two of those
    /// eighteen were not merely unresponsive on a touch device, they were fatal:
    /// <c>GameBootstrap.Update</c> and <c>PlayerController.Update</c> both opened with
    /// <c>if (kb == null) return;</c>, so with no keyboard attached the whole game loop - floor
    /// clearing, run start, camera tracking, every attack - simply never ran. Nothing about that
    /// looks like an input problem from the outside; the game just sits there.
    ///
    /// So the rule is: NOTHING OUTSIDE THIS FILE TOUCHES A DEVICE. A caller asks for an action,
    /// not for a key, and a new input device is one edit here rather than eighteen.
    ///
    /// **Edges are computed here, not read off the device.** The Input System's
    /// <c>wasPressedThisFrame</c> only exists for real controls, and half the actions below can
    /// now come from an on-screen button that has no such thing. Taking the edge of the UNION in
    /// one place is what lets a virtual button and a physical key be the same action rather than
    /// two branches at every call site.
    ///
    /// <see cref="Sync"/> is lazy and idempotent per frame, so this needs no component, no
    /// execution order and no initialisation - the first getter to be called each frame does the
    /// work and the rest read the same snapshot.
    /// </summary>
    public static class Controls
    {
        // ---------------------------------------------------------------- virtual input
        //
        // Written by UI/TouchControls, which runs at a negative execution order so its state is
        // already up to date by the time anything asks. LEVEL state only - never an edge - because
        // the edge is this file's job and a virtual button that set its own would fire twice or
        // not at all depending on which Update happened to run first.

        public static Vector2 VirtualMove;
        public static bool VirtualAttack;
        public static bool VirtualRelease;
        public static bool VirtualSecondAbility;
        public static bool VirtualInteract;
        public static bool VirtualCancel;
        public static bool VirtualLoadout;
        public static bool VirtualAlt;

        /// <summary>
        /// Which set of on-screen buttons is up.
        ///
        /// Data on this static rather than a method on the overlay, so anything that changes what
        /// the player can do - the room while carrying a trophy, the bootstrap between hub and run
        /// - just says what state it is in and never needs a reference to a UI object it does not
        /// own. The overlay reads it; nothing reads the overlay.
        /// </summary>
        public enum Context { Hidden, Hub, Placing, Arena, Modal }

        public static Context Screen = Context.Hidden;

        /// <summary>
        /// Whether the ability button should read as live. Set by whoever owns the run so the
        /// button can be dim exactly when pressing it would do nothing - the one thing a touch
        /// player cannot discover by hovering.
        /// </summary>
        public static System.Func<bool> ReleaseReady;

        /// <summary>Same as <see cref="ReleaseReady"/>, for the chest's defensive ability (GUARD)
        /// - dim exactly while it's on cooldown, which a touch player has no other way to see.</summary>
        public static System.Func<bool> GuardReady;

        // ------------------------------------------------------------ the button rings
        //
        // Each of the Ready flags above answers "would pressing this do anything" - a
        // yes/no, which is all a dim button ever needed. The rings answer the question that
        // follows it, and which a touch player previously had no way to ask at all: HOW LONG.
        //
        // Deliberately separate Funcs rather than one struct: they come from three unrelated
        // owners (the attack gate, the defensive cooldown, the elemental meter) and only the
        // overlay ever wants them together. Same lambda-not-field reasoning as the Ready flags -
        // the player is rebuilt every run, so nothing may hold a reference to it.
        //
        // All three are 0..1 filling TOWARD ready, never counting down. A ring that sometimes
        // empties and sometimes fills is a ring nobody can read at a glance.

        /// <summary>How ready the next swing is - see PlayerController.AttackCooldown01.</summary>
        public static System.Func<float> AttackProgress01;

        /// <summary>The elemental meter, 0 to 1. Its FULLNESS, not its spendability -
        /// <see cref="ReleaseReady"/> owns the latter, and for two elements they differ.</summary>
        public static System.Func<float> ReleaseProgress01;

        /// <summary>The defensive cooldown - see PlayerController.DefenseCooldown01.</summary>
        public static System.Func<float> GuardProgress01;

        /// <summary>
        /// Screen points touched this frame that the on-screen controls did NOT claim.
        ///
        /// The stick and the buttons own their own fingers, so a UI hit test must not also see
        /// them - otherwise steering the character would press whatever the HUD happens to have
        /// under the stick. Everything else is free, which is what lets a second finger tap a
        /// button while the first one is still driving.
        /// </summary>
        public static readonly System.Collections.Generic.List<Vector2> FreeTaps = new();

        /// <summary>
        /// Where an unclaimed finger currently IS, or null when none is down.
        ///
        /// Separate from <see cref="FreeTaps"/>, which only ever holds fingers that went down
        /// THIS frame. A drag needs the finger tracked for its whole life, and the release has to
        /// be the moment it stops being tracked - reading Touchscreen.primaryTouch instead looked
        /// equivalent and is not: "primary" is the first finger down, which after a stick drag or
        /// a held button is somebody else's, so a tap by a second finger never reported a release
        /// and every press-then-release on a screen was swallowed.
        /// </summary>
        public static Vector2? FreePointer;

        // ---------------------------------------------------------------- device mode

        static bool _touchMode;
        static bool _gamepadMode;

        /// <summary>
        /// Whether to draw on-screen controls.
        ///
        /// LAST DEVICE WINS rather than a platform check: a touch turns it on, a mouse click or a
        /// key turns it off. That keeps a touchscreen laptop and the Editor's device simulator
        /// working without a build flag, and it means the controls do not sit over the game on a
        /// desktop that happens to have a digitiser attached. Mobile platforms start with it on,
        /// because the first frame should already be playable.
        ///
        /// A gamepad turns this OFF, same as a mouse or a key - the on-screen stick and buttons
        /// are for a finger that has nothing else to press, and a gamepad already has real ones.
        /// </summary>
        public static bool TouchMode { get { DetectDevice(); return _touchMode; } }

        /// <summary>
        /// Whether a gamepad is the last device the player touched. Drives the virtual cursor's
        /// visibility (<see cref="GamepadCursorPosition"/>) the same "last device wins" way
        /// <see cref="TouchMode"/> already drives the on-screen stick.
        /// </summary>
        public static bool GamepadMode { get { DetectDevice(); return _gamepadMode; } }

        /// <summary>Force the overlay on for testing on a desktop. Null returns to auto.</summary>
        public static bool? TouchModeOverride;

        static bool _started;

        static void Start()
        {
            _started = true;
            _touchMode = Application.isMobilePlatform;
        }

        // ---------------------------------------------------------------- per-frame snapshot

        static int _frame = -1;

        static bool _attack, _attackPrev;
        static bool _release, _releasePrev;
        static bool _secondAbility, _secondAbilityPrev;
        static bool _interact, _interactPrev;
        static bool _cancel, _cancelPrev;
        static bool _loadout, _loadoutPrev;
        static bool _confirm, _confirmPrev;
        static bool _mastery, _masteryPrev;
        static bool _alt, _altPrev;
        static bool _settingsMenu, _settingsMenuPrev;
        static Vector2 _move;

        static bool _pointerDown, _pointerHeld, _pointerUp;
        static Vector2 _pointer, _pointerPrevPos, _pointerDelta;
        static float _zoom;

        /// <summary>Deadzone applied to both sticks, as a fraction of full deflection - the same
        /// "a thumb resting on the glass must not read as input" reasoning the touch stick's own
        /// DeadZone exists for, just for an analog stick instead of a screen zone.</summary>
        const float StickDeadzone = 0.2f;

        /// <summary>Radial deadzone remap: below it, nothing; above it, rescaled so the stick still
        /// reaches full magnitude at full deflection instead of topping out early.</summary>
        static Vector2 ApplyDeadzone(Vector2 v, float deadzone)
        {
            float mag = v.magnitude;
            if (mag < deadzone) return Vector2.zero;
            return v / mag * Mathf.InverseLerp(deadzone, 1f, mag);
        }

        static Vector2 _gamepadCursor;

        /// <summary>Fraction of the screen HEIGHT the cursor crosses per second at full stick
        /// deflection - a fraction rather than a pixel count so it feels the same on any
        /// resolution, the same reasoning the pinch-to-zoom conversion already uses.</summary>
        const float GamepadCursorSpeed = 1.1f;

        /// <summary>
        /// Where the gamepad's virtual cursor currently is, in real screen pixels - the same space
        /// <c>Mouse.current.position</c> already reports in. Read by the one thing that draws it;
        /// every screen that cares where the pointer IS already reads <see cref="PointerPosition"/>
        /// instead, which only updates while something is actually pressed/held.
        /// </summary>
        public static Vector2 GamepadCursorPosition { get { Sync(); return _gamepadCursor; } }

        // ---------------------------------------------------------------- D-pad menu navigation
        //
        // Every screen already hit-tests its OWN clickable rects against the pointer position
        // rather than going through Unity's Button/EventSystem, so there is no generic "list of
        // buttons" this file could discover by itself - a screen REGISTERS its own candidates,
        // once a frame while it's open, and this file does the rest: which one is focused, what a
        // D-pad press moves it to, and relocating the shared gamepad cursor there so a South-
        // button press clicks it through the exact same pointer pipeline every screen already
        // reads. A screen needs no new interaction code of its own at all.
        //
        // Stale entries left behind by a screen that closed without registering an empty list are
        // harmless rather than dangerous: every lookup below already skips a null (destroyed)
        // RectTransform, the same overloaded check the rest of this project relies on everywhere
        // else a MonoBehaviour reference might outlive the thing it pointed at.

        static readonly List<RectTransform> _focusCandidates = new();
        static RectTransform _focused, _focusedBefore;
        static int _candidatesFrame = -10;

        /// <summary>True once the right stick has moved the free cursor since the last D-pad
        /// press. While set, nothing is focused and nothing is auto-focused - the stick is the
        /// player saying "I am pointing myself".</summary>
        static bool _freeAim;

        /// <summary>Called by whichever screen is open, every frame, with its own current list of
        /// clickable rects. Replaces the previous list outright - only one screen is ever open at
        /// a time in this project's modal model, so there is nothing to merge.
        ///
        /// ORDER MATTERS: the first live entry is what a freshly opened screen focuses, so a
        /// screen lists its main choice first (and a destructive dialog its safe answer).</summary>
        public static void SetFocusCandidates(List<RectTransform> candidates)
        {
            _focusCandidates.Clear();
            if (candidates != null) _focusCandidates.AddRange(candidates);
            _candidatesFrame = Time.frameCount;
        }

        /// <summary>The rect the D-pad has selected, or null while a gamepad isn't the device in
        /// use, the stick is free-aiming, or no screen has registered candidates in the last
        /// frame (a closed screen's list goes stale rather than lingering as a phantom
        /// highlight). Read by <see cref="UI.GamepadCursor"/> to draw the highlight, and by the
        /// scrolling screens to keep it in view.</summary>
        public static RectTransform Focused
        {
            get
            {
                Sync();
                return _gamepadMode && CandidatesLive && Valid(_focused) ? _focused : null;
            }
        }

        // Screens register from their own Update, which can run before or after the first Sync of
        // a frame - so last frame's list still counts.
        static bool CandidatesLive => Time.frameCount - _candidatesFrame <= 1;

        static bool Valid(RectTransform rt)
            => rt != null && rt.gameObject.activeInHierarchy && _focusCandidates.Contains(rt);

        static RectTransform FirstValid()
        {
            foreach (var c in _focusCandidates) if (c != null && c.gameObject.activeInHierarchy) return c;
            return null;
        }

        static void SyncFocusNavigation(Gamepad pad)
        {
            if (pad == null) return;
            // No menu open: the right stick is aiming in play, which must not leave the next menu
            // opened believing the player is free-aiming it.
            if (!CandidatesLive) { _freeAim = false; return; }
            if (_focusCandidates.Count == 0) return;

            Vector2 dir = Vector2.zero;
            if (pad.dpad.up.wasPressedThisFrame) dir = Vector2.up;
            else if (pad.dpad.down.wasPressedThisFrame) dir = Vector2.down;
            else if (pad.dpad.left.wasPressedThisFrame) dir = Vector2.left;
            else if (pad.dpad.right.wasPressedThisFrame) dir = Vector2.right;

            if (dir != Vector2.zero)
            {
                // A D-pad press is unambiguous gamepad activity, and it ends free aim.
                _touchMode = false;
                _gamepadMode = true;
                _freeAim = false;
            }
            if (!_gamepadMode || _freeAim) return;

            // AUTO-FOCUS. A menu opened on a gamepad has something selected from its first frame -
            // otherwise the first press of A clicks wherever the free cursor was left, and the
            // first D-pad press has nothing to move FROM. When the focused rect goes away (a
            // picker closing, a screen rebuilding) the one focused before it is tried first, so
            // coming back from the gear grid lands on the slot that opened it.
            if (!Valid(_focused))
            {
                var fallback = Valid(_focusedBefore) ? _focusedBefore : FirstValid();
                if (_focused != null && _focused != fallback) _focusedBefore = _focused;
                _focused = fallback;
                if (_focused == null) return;
                if (dir != Vector2.zero) return;   // the press that woke the menu only selects
            }

            if (dir != Vector2.zero)
            {
                var next = NearestInDirection(_focused, dir, _focusCandidates);
                if (next != null) _focused = next;
            }
        }

        static readonly Vector3[] _corners = new Vector3[4];

        static Vector2 RectCenter(RectTransform rt)
        {
            rt.GetWorldCorners(_corners);
            return (Vector2)(_corners[0] + _corners[2]) * 0.5f;
        }

        /// <summary>
        /// The candidate whose center sits furthest along <paramref name="dir"/> from
        /// <paramref name="from"/> for the least sideways drift - a standard console-menu spatial
        /// nav: filter to the correct half-plane, then prefer whatever is most directly in line
        /// over whatever is merely closest, so navigating a row doesn't jump diagonally into the
        /// next one.
        /// </summary>
        static RectTransform NearestInDirection(RectTransform from, Vector2 dir, List<RectTransform> candidates)
        {
            Vector2 fromPos = RectCenter(from);
            RectTransform best = null;
            float bestScore = float.MaxValue;
            foreach (var c in candidates)
            {
                if (c == null || c == from || !c.gameObject.activeInHierarchy) continue;
                Vector2 delta = RectCenter(c) - fromPos;
                float along = Vector2.Dot(delta, dir);
                if (along <= 1f) continue;   // must be meaningfully IN that direction, not beside it
                float perp = (delta - dir * along).magnitude;
                float score = along + perp * 2.5f;   // in-line beats merely-closer-but-off-axis
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        static int _deviceFrame = -1;

        /// <summary>
        /// Which device the player is actually using, resolved SEPARATELY from the rest of the
        /// frame's snapshot and deliberately so.
        ///
        /// TouchControls runs at a negative execution order and has to ask this before it decides
        /// what to draw. If that question ran the full Sync, the snapshot would be taken BEFORE
        /// the overlay had written this frame's stick and button state - so every touch would be
        /// read one frame late, which is not a crash, it is the controls feeling slightly wrong
        /// forever. This half looks only at hardware, so asking it early costs nothing.
        /// </summary>
        static void DetectDevice()
        {
            if (_deviceFrame == Time.frameCount) return;
            _deviceFrame = Time.frameCount;
            if (!_started) Start();

            if (TouchModeOverride.HasValue) { _touchMode = TouchModeOverride.Value; return; }

            var touch = Touchscreen.current;
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            var pad = Gamepad.current;

            // LAST DEVICE WINS. A touch turns the overlay on; a mouse, a key or a gamepad turn it
            // off (and a gamepad additionally turns ITS OWN mode on, for the virtual cursor).
            //
            // HELD state, not an edge, and that is not a detail: an edge is true for exactly one
            // frame, so missing that frame for any reason leaves the overlay stuck on the wrong
            // device until the player presses something again. A hold is true for as long as the
            // finger, the key or the stick is held, so this cannot be missed - and it is the same
            // answer to the same question, since nobody presses a key for less than a frame.
            if (touch != null && touch.primaryTouch.press.isPressed) { _touchMode = true; _gamepadMode = false; }
            else if (pad != null && IsPadActive(pad)) { _touchMode = false; _gamepadMode = true; }
            else if ((mouse != null && (mouse.leftButton.isPressed
                                        || mouse.delta.ReadValue().sqrMagnitude > 4f))
                     || (kb != null && kb.anyKey.isPressed)) { _touchMode = false; _gamepadMode = false; }
        }

        /// <summary>Any bound button held or either stick pushed past a deadzone - used only to
        /// decide which device last spoke, so a small deadzone is fine even though the deadzone
        /// that actually shapes movement is applied separately in Sync. Checks the controls this
        /// file actually binds rather than every control the device exposes, so it stays cheap
        /// and so an incidental sensor/touchpad control some pads expose can't count as input.</summary>
        static bool IsPadActive(Gamepad pad)
        {
            return pad.leftStick.ReadValue().sqrMagnitude > 0.04f
                || pad.rightStick.ReadValue().sqrMagnitude > 0.04f
                || pad.buttonSouth.isPressed || pad.buttonNorth.isPressed
                || pad.buttonEast.isPressed || pad.buttonWest.isPressed
                || pad.leftShoulder.isPressed || pad.rightShoulder.isPressed
                || pad.leftTrigger.isPressed || pad.rightTrigger.isPressed
                || pad.startButton.isPressed
                || pad.dpad.up.isPressed || pad.dpad.down.isPressed
                || pad.dpad.left.isPressed || pad.dpad.right.isPressed;
        }

        static void Sync()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            if (!_started) Start();

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var touch = Touchscreen.current;
            var pad = Gamepad.current;

            DetectDevice();

            Pinching = ActiveTouches(touch) >= 2;

            // ---- movement ----
            Vector2 keys = Vector2.zero;
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) keys.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) keys.y -= 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keys.x -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keys.x += 1;
            }
            if (pad != null) keys += ApplyDeadzone(pad.leftStick.ReadValue(), StickDeadzone);
            _move = Vector2.ClampMagnitude(keys + VirtualMove, 1f);

            // ---- actions ----
            // Every one of these reads its gamepad button through GamepadBindings rather than
            // naming one directly, so a player who remaps a button in the controller
            // configuration screen changes what THIS reads without either of them knowing about
            // the other - see GamepadBindings' own header for why the lookup lives there.
            var attackBtn = GamepadBindings.Control(pad, GamepadBindings.Get(GamepadAction.Attack));
            var releaseBtn = GamepadBindings.Control(pad, GamepadBindings.Get(GamepadAction.ReleaseInteract));
            var secondBtn = GamepadBindings.Control(pad, GamepadBindings.Get(GamepadAction.SecondAbility));
            var cancelBtn = GamepadBindings.Control(pad, GamepadBindings.Get(GamepadAction.Cancel));
            var loadoutBtn = GamepadBindings.Control(pad, GamepadBindings.Get(GamepadAction.Loadout));
            var masteryBtn = GamepadBindings.Control(pad, GamepadBindings.Get(GamepadAction.Mastery));
            var altBtn = GamepadBindings.Control(pad, GamepadBindings.Get(GamepadAction.Alt));

            // Interact and Release already share E on keyboard - the contextual action, combat or
            // hub, whichever applies - so West does the same double duty here rather than needing
            // a fourth face button neither context actually wants. They also share one rebindable
            // slot (ReleaseInteract) for the same reason.
            Edge(ref _attack, ref _attackPrev,
                 (mouse != null && mouse.leftButton.isPressed) || (kb != null && kb.spaceKey.isPressed)
                 || (attackBtn != null && attackBtn.isPressed)
                 || VirtualAttack);

            Edge(ref _release, ref _releasePrev,
                 (mouse != null && mouse.rightButton.isPressed)
                 || (kb != null && (kb.eKey.isPressed || kb.leftShiftKey.isPressed))
                 || (releaseBtn != null && releaseBtn.isPressed)
                 || VirtualRelease);

            // R on keyboard and, by default, the right trigger on a pad. Named for the second
            // ability it once fired; that now rides the release button (chosen on the mastery
            // board), leaving this to the hub's "look closer" and AnyDismiss.
            Edge(ref _secondAbility, ref _secondAbilityPrev,
                 (kb != null && kb.rKey.isPressed)
                 || (secondBtn != null && secondBtn.isPressed)
                 || VirtualSecondAbility);

            Edge(ref _interact, ref _interactPrev,
                 (kb != null && (kb.eKey.isPressed || kb.enterKey.isPressed))
                 || (releaseBtn != null && releaseBtn.isPressed)
                 || VirtualInteract);

            Edge(ref _cancel, ref _cancelPrev,
                 (kb != null && kb.escapeKey.isPressed) || (cancelBtn != null && cancelBtn.isPressed)
                 || VirtualCancel);

            Edge(ref _loadout, ref _loadoutPrev,
                 (kb != null && (kb.cKey.isPressed || kb.tabKey.isPressed))
                 || (loadoutBtn != null && loadoutBtn.isPressed)
                 || VirtualLoadout);

            // Keyboard only. A gamepad's A used to be Confirm too, and that is what made two-answer
            // dialogs unanswerable on a pad: A confirmed the destructive answer whichever button
            // the D-pad had selected. On a pad, A is a CLICK on the focused rect (the pointer
            // pipeline below), which picks whichever answer is selected.
            Edge(ref _confirm, ref _confirmPrev,
                 kb != null && (kb.enterKey.isPressed || kb.numpadEnterKey.isPressed));

            Edge(ref _mastery, ref _masteryPrev,
                 (kb != null && kb.mKey.isPressed) || (masteryBtn != null && masteryBtn.isPressed));

            Edge(ref _alt, ref _altPrev,
                 (kb != null && kb.qKey.isPressed) || (altBtn != null && altBtn.isPressed)
                 || VirtualAlt);

            // Opens the hub's settings menu. Escape specifically, not the Cancel action (which
            // also fires on the gamepad's East/B button and on touch's virtual cancel) - a
            // settings menu is summoned on its own key, never backed into by the button that
            // closes everything else. Start is fixed rather than rebindable for the same reason
            // the D-pad and sticks are: a menu button has to work even before the player has
            // pressed anything else, including before they have looked at the rebind screen.
            Edge(ref _settingsMenu, ref _settingsMenuPrev,
                 (kb != null && kb.escapeKey.isPressed) || (pad != null && pad.startButton.isPressed));

            // ---- gamepad virtual cursor ----
            //
            // The right stick integrates into a persistent screen position exactly the way a mouse
            // already reports one directly and a finger's own contact point already IS one - so it
            // slots into the SAME _pointer pipeline every screen already reads, rather than every
            // screen needing to learn a fourth way to be pointed at.
            if (pad != null)
            {
                if (_gamepadCursor == Vector2.zero) _gamepadCursor = new Vector2(UnityEngine.Screen.width, UnityEngine.Screen.height) * 0.5f;
                var stick = ApplyDeadzone(pad.rightStick.ReadValue(), StickDeadzone);
                if (stick != Vector2.zero)
                {
                    // The stick takes over from the D-pad: start from where the highlight was.
                    if (!_freeAim && _focused != null && _gamepadMode) _gamepadCursor = RectCenter(_focused);
                    _freeAim = true;
                    _gamepadCursor += stick * (UnityEngine.Screen.height * GamepadCursorSpeed * Time.unscaledDeltaTime);
                    _gamepadCursor.x = Mathf.Clamp(_gamepadCursor.x, 0f, UnityEngine.Screen.width);
                    _gamepadCursor.y = Mathf.Clamp(_gamepadCursor.y, 0f, UnityEngine.Screen.height);
                }
            }

            // Focus is resolved BEFORE the pointer, so the press of A on the frame a D-pad press
            // lands clicks the rect it landed on. While something is focused the pad's pointer
            // sits on its CURRENT centre - read every frame, not copied once at the D-pad press,
            // because the mastery board pans and the lists scroll the focused rect under it.
            SyncFocusNavigation(pad);
            if (pad != null && _gamepadMode && !_freeAim && CandidatesLive && Valid(_focused))
                _gamepadCursor = RectCenter(_focused);

            // ---- pointer: a mouse click, a finger tap and a gamepad's cursor click are the same event ----
            bool mouseDown = mouse != null && mouse.leftButton.wasPressedThisFrame;
            bool mouseHeld = mouse != null && mouse.leftButton.isPressed;
            bool mouseUp = mouse != null && mouse.leftButton.wasReleasedThisFrame;

            bool fingerDown = FreeTaps.Count > 0;
            bool fingerHeld = FreePointer.HasValue;

            bool padDown = attackBtn != null && attackBtn.wasPressedThisFrame;
            bool padHeld = attackBtn != null && attackBtn.isPressed;

            _pointerDown = mouseDown || fingerDown || padDown;

            bool heldPrev = _pointerHeld;
            _pointerHeld = mouseHeld || fingerHeld || padHeld;

            // The release is the EDGE of the held state, computed here like every other edge in
            // this file, rather than read off a device control that only one of the two input
            // paths has.
            _pointerUp = mouseUp || (heldPrev && !_pointerHeld);

            // A MOUSE HAS A POSITION WITHOUT A BUTTON; A FINGER DOES NOT. That asymmetry is the
            // whole reason for the last two branches, and leaving the hover case out was a real
            // bug rather than an omission: with nothing pressed the pointer stayed wherever it was
            // last SET, which on a freshly loaded scene is (0, 0). Every screen that asks "is the
            // cursor over this rectangle" before acting on a scroll - the mastery board and the
            // character preview both - therefore answered no forever, and their zoom did nothing
            // at all until something had been clicked inside them first. It reads as scroll-to-zoom
            // being unimplemented, not as a stale coordinate.
            //
            // Held fingers and the pad cursor still win, so touch is unaffected: a phone has no
            // Mouse.current for this to fall through to, and on a touchscreen laptop a finger down
            // is matched two branches earlier.
            var prev = _pointer;
            _pointer = fingerDown ? FreeTaps[0]
                     : fingerHeld ? FreePointer.Value
                     : mouseHeld || mouseDown ? mouse.position.ReadValue()
                     : padHeld || padDown ? _gamepadCursor
                     // On a pad the HOVER is the pad's cursor too. Left to the resting mouse, the
                     // release of A read wherever the mouse happened to be - so every screen that
                     // commits on release (the lists, the mastery board) took the press for a
                     // drag - and a mouse parked over the loadout preview held that screen's
                     // gesture forever.
                     : _gamepadMode ? _gamepadCursor
                     : mouse != null ? mouse.position.ReadValue()
                     : prev;

            // Zeroed on the frame the pointer goes down, or a drag begins with a jump from
            // wherever the last one ended - which on touch is anywhere on the screen.
            _pointerDelta = _pointerDown ? Vector2.zero : _pointer - prev;

            // ZOOM IS IN NOTCHES, and every device is converted into that one unit here rather
            // than each screen guessing. A pinch is measured as a fraction of screen height, and
            // spreading by an eighth of the screen is worth one notch. Without a shared unit the
            // same code either crawled on the wheel or ran away on a pinch - the raw pinch delta
            // was being multiplied to 12% PER FRAME, which over a half-second gesture is a
            // thirty-fold zoom. The triggers have neither a wheel's continuous roll nor a pinch's
            // continuous spread, so they just step one notch per press rather than trying to
            // approximate either - and they leave the D-pad free for menu navigation below, which
            // needs all four directions.
            float padZoom = 0f;
            if (pad != null)
            {
                if (pad.leftTrigger.wasPressedThisFrame) padZoom -= 1f;
                if (pad.rightTrigger.wasPressedThisFrame) padZoom += 1f;
            }
            _zoom = WheelNotches(mouse)
                  + PinchDelta / Mathf.Log(NotchRatio)
                  + padZoom;
            PinchDelta = 0f;
        }

        /// <summary>
        /// This frame's wheel travel, in notches.
        ///
        /// THE DIVISOR IS A SETTING, NOT A CONSTANT, and getting that wrong is why zooming looked
        /// dead rather than slow. 120 is WHEEL_DELTA - the raw units Windows reports - but the
        /// Input System's `scrollDeltaBehavior` defaults to UniformAcrossAllPlatforms, which
        /// normalises every platform into [-1, 1] BEFORE anything here sees it. Dividing an
        /// already-normalised 1.0 by 120 gives 0.0083 notches, and a screen stepping 12% per notch
        /// then moves by 1.0009x - a tenth of a percent per scroll event, on every platform, not
        /// just this one. Nothing errored and the gesture genuinely arrived; the number was simply
        /// two orders of magnitude too small to see.
        ///
        /// A MAC TRACKPAD CANNOT SEND A PINCH. macOS delivers magnification as an NSEvent gesture
        /// that Unity does not surface at all, and a trackpad populates no Touchscreen - so
        /// <see cref="PinchDelta"/> is touchscreen-only and always will be. What a trackpad DOES
        /// send is two-finger scroll, as ordinary wheel events with fractional deltas, which is
        /// what this reads. That makes two-finger scroll the trackpad's zoom, and it arrives
        /// continuously rather than in steps - which is the better gesture of the two anyway.
        /// </summary>
        static float WheelNotches(Mouse mouse)
        {
            if (mouse == null) return 0f;
            float raw = mouse.scroll.ReadValue().y;
            return InputSystem.settings.scrollDeltaBehavior
                       == InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange
                 ? raw / WindowsWheelDelta
                 : raw;
        }

        /// <summary>Windows' WHEEL_DELTA. Only reachable if the project opts out of normalisation.</summary>
        const float WindowsWheelDelta = 120f;

        static void Edge(ref bool now, ref bool prev, bool state)
        {
            prev = now;
            now = state;
        }

        /// <summary>
        /// True while the on-screen controls own the primary finger. Set by TouchControls, and the
        /// reason a UI drag does not fight the movement stick.
        /// </summary>
        public static bool ClaimedPrimary;

        /// <summary>
        /// What one notch is worth as a zoom factor. Shared so the wheel and the pinch cannot
        /// drift apart: the pinch reports the LOG of how much the fingers spread, and dividing by
        /// the log of this converts it into the same notches a wheel step produces.
        /// </summary>
        public const float NotchRatio = 1.12f;

        /// <summary>
        /// This frame's pinch as a natural log of the RATIO the fingers moved apart. Consumed by
        /// Sync.
        ///
        /// A ratio, not a distance, because that is what a pinch physically means: spread your
        /// fingers to twice the separation and the thing under them should be twice the size,
        /// whatever the screen or the starting gap. Measured as a fraction of screen height
        /// instead, the same gesture did nothing at one zoom and ran away at another - closing
        /// 30% of the screen was worth a 25% zoom change, so getting from the opening zoom to the
        /// overview took a gesture one and a half screens long.
        /// </summary>
        public static float PinchDelta;

        /// <summary>
        /// True while two or more fingers are down.
        ///
        /// Anything that DRAGS has to stand down while this is set. The mastery board panned from
        /// the primary finger and pinched from both, so a pinch also dragged: the board zoomed and
        /// bolted sideways at once. A pinch is one gesture, not a pan with a zoom on top.
        ///
        /// DERIVED here every frame rather than latched by whoever noticed the second finger.
        /// As a field the overlay wrote, it stuck on - and a stuck "pinching" is a board that can
        /// never be panned again, which is a far worse failure than the one it was added to fix.
        /// One owner, recomputed, no way to go stale.
        /// </summary>
        public static bool Pinching { get; private set; }

        static int ActiveTouches(Touchscreen touch)
        {
            if (touch == null) return 0;
            int n = 0;
            foreach (var t in touch.touches)
            {
                var p = t.phase.ReadValue();
                if (p is UnityEngine.InputSystem.TouchPhase.Began
                       or UnityEngine.InputSystem.TouchPhase.Moved
                       or UnityEngine.InputSystem.TouchPhase.Stationary) n++;
            }
            return n;
        }

        // ---------------------------------------------------------------- the public verbs

        public static Vector2 Move { get { Sync(); return _move; } }

        public static bool AttackHeld { get { Sync(); return _attack; } }
        public static bool AttackTapped { get { Sync(); return _attack && !_attackPrev; } }
        public static bool ReleaseTapped { get { Sync(); return _release && !_releasePrev; } }
        public static bool SecondAbilityTapped { get { Sync(); return _secondAbility && !_secondAbilityPrev; } }
        public static bool InteractTapped { get { Sync(); return _interact && !_interactPrev; } }
        public static bool CancelTapped { get { Sync(); return _cancel && !_cancelPrev; } }
        public static bool LoadoutTapped { get { Sync(); return _loadout && !_loadoutPrev; } }
        public static bool ConfirmTapped { get { Sync(); return _confirm && !_confirmPrev; } }
        public static bool MasteryTapped { get { Sync(); return _mastery && !_masteryPrev; } }

        // Held states, for the readout buttons TouchControls draws off-touch - a button shown
        // pressed while its key is down. Nothing gameplay should key off these; it wants the edges.
        public static bool ReleaseHeld { get { Sync(); return _release; } }
        public static bool SecondAbilityHeld { get { Sync(); return _secondAbility; } }
        public static bool InteractHeld { get { Sync(); return _interact; } }
        public static bool CancelHeld { get { Sync(); return _cancel; } }
        public static bool LoadoutHeld { get { Sync(); return _loadout; } }
        public static bool AltHeld { get { Sync(); return _alt; } }

        /// <summary>
        /// Spend this frame's Cancel press: <see cref="CancelTapped"/> reads false for the rest of
        /// the frame. For a screen stacked over another (the gear grid over the loadout, the
        /// armour stand, the circle) - nothing here consumes input, so without this whichever
        /// Update ran second saw the same press and closed the screen underneath as well.
        /// </summary>
        public static void ConsumeCancel() { Sync(); _cancelPrev = _cancel; }

        /// <summary>The third option, where one exists. Only trophy placement has one.</summary>
        public static bool AltTapped { get { Sync(); return _alt && !_altPrev; } }

        /// <summary>Opens the hub's settings menu - Escape (keyboard) or Start (gamepad) only.
        /// See the field this reads for why it is not folded into <see cref="CancelTapped"/>.</summary>
        public static bool SettingsTapped { get { Sync(); return _settingsMenu && !_settingsMenuPrev; } }

        /// <summary>A click or a tap this frame, and where it landed.</summary>
        public static bool Tapped(out Vector2 position)
        {
            Sync();
            position = _pointer;
            return _pointerDown;
        }

        public static bool PointerHeld(out Vector2 position)
        {
            Sync();
            position = _pointer;
            return _pointerHeld;
        }

        public static bool PointerReleased { get { Sync(); return _pointerUp; } }
        public static Vector2 PointerPosition { get { Sync(); return _pointer; } }
        public static Vector2 PointerDelta { get { Sync(); return _pointerDelta; } }

        /// <summary>
        /// Mouse wheel or pinch this frame, in NOTCHES - one wheel step, or spreading the fingers
        /// by an eighth of the screen. Positive zooms in. A caller decides what a notch is worth.
        /// </summary>
        public static float Zoom { get { Sync(); return _zoom; } }

        /// <summary>
        /// Digit shortcuts (element doors, exchange slates). Keyboard only and deliberately so -
        /// these are the shortcut PAST a thing the player can already touch, not the only way to
        /// reach it, so there is nothing to replace on a phone.
        /// </summary>
        public static bool DigitTapped(int index)
        {
            var kb = Keyboard.current;
            return kb != null && index >= 0 && index < 9
                   && kb[(Key)((int)Key.Digit1 + index)].wasPressedThisFrame;
        }

        /// <summary>
        /// Polls for a fresh press of any rebindable gamepad button, for the controller
        /// configuration screen. This is the one caller allowed to ask "which button was that",
        /// rather than "is action X held" - remapping which physical button an action reads is a
        /// question about the device itself, not about any action this file already exposes, and
        /// it still goes through <see cref="GamepadBindings.Control"/> rather than naming a button
        /// itself.
        /// </summary>
        public static bool TryCaptureGamepadButton(out GamepadButtonId button)
        {
            Sync();
            button = default;
            var pad = Gamepad.current;
            if (pad == null) return false;
            foreach (var id in GamepadBindings.AllButtons)
            {
                var ctrl = GamepadBindings.Control(pad, id);
                if (ctrl != null && ctrl.wasPressedThisFrame) { button = id; return true; }
            }
            return false;
        }

        /// <summary>
        /// Any dismiss press at all - the run-over screen doesn't care which. Reads the same
        /// Sync()-computed edges every other action uses (R/second-ability, Space/attack) rather
        /// than polling <c>Keyboard.current</c> directly, which is what this used to do and which
        /// is exactly the pattern this file's header bans: a raw <c>wasPressedThisFrame</c> read
        /// sees only a physical keyboard, so a gamepad or touch player had no way to dismiss this
        /// screen short of Stopping the Editor.
        /// </summary>
        public static bool AnyDismiss
        {
            get
            {
                Sync();
                return SecondAbilityTapped || AttackTapped || _pointerDown;
            }
        }
    }
}
