using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The on-screen controls: a movement stick on the left, actions on the right.
    ///
    /// The action buttons are drawn on EVERY device, not only touch - one HUD, so a player who
    /// moves between a phone and a desk finds the same picture. Off touch they are READOUTS: no
    /// stick, nothing claims input, each carries the key (or pad button) that fires it, and it
    /// lights while that key is held. The cooldown rings mean the same thing either way. Modals
    /// keep their BACK button to touch, where it is the only way out; a keyboard has Escape and
    /// every screen already says so.
    ///
    /// Runs at a negative execution order so the state it writes into <see cref="Controls"/> is
    /// already correct before any Update reads it. Order between runtime-built MonoBehaviours is
    /// otherwise undefined, and this project has been bitten by that before (see the note on
    /// ConfirmDialog's opening frame) - here it would show up as a one-frame lag on every touch,
    /// which is exactly the kind of thing that reads as "the controls feel bad" rather than as a
    /// bug with a cause.
    ///
    /// FINGERS ARE CLAIMED BY ID, and that is the whole reason this is not four independent
    /// buttons. A twin-stick arena has to let you steer and swing at the same time, so the stick
    /// owns the finger that started on it for as long as it is down - even when that finger
    /// wanders out of the zone - and every other finger is free to press something else. Anything
    /// nobody claims is published as a free tap, which is what the UI screens hit-test against, so
    /// driving the character can never also press whatever the HUD has underneath the stick.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class TouchControls : MonoBehaviour
    {
        Controls.Context _mode = Controls.Context.Hidden;

        // ---------------------------------------------------------------- layout
        //
        // In the canvas's 1920x1080 reference space, so these are proportions of the screen rather
        // than pixels - the CanvasScaler handles the rest. Everything is anchored to a CORNER, not
        // to the centre, because a phone's aspect ratio is not 16:9 and centre-anchored controls
        // drift away from the thumbs that are always at the edges.

        const float StickHome = 300f;      // from the bottom-left corner, both axes
        const float StickRadius = 150f;    // travel at full deflection
        const float StickKnob = 78f;
        const float StickZoneWidth = 0.46f;// fraction of the screen the stick may start in
        const float DeadZone = 0.16f;      // of StickRadius, before the character moves at all

        // Three sizes, and the gap between them is the whole hierarchy. ATTACK is the button the
        // thumb returns to between every other decision, so it is not merely first among equals -
        // it is large enough to be found without looking, which is the only way it can be pressed
        // while the eyes are on the crowd. The two ABILITIES sit a clear step down (a deliberate
        // choice per press, not a rhythm), and everything that opens a screen a step below that.
        const float ActionR = 132f;        // ATTACK, and whatever stands in for it per context
        const float AbilityR = 88f;        // the elemental release and the defensive ability
        const float SmallR = 74f;          // screens, and the second ability

        /// <summary>
        /// How far OUTSIDE the button's own disc the ring sits. Outside rather than on the rim,
        /// because the ring is the one part that changes every frame and the fill underneath is
        /// the part the thumb is aiming at - drawn on top of each other, the moving thing reads
        /// as the button's edge flickering rather than as a readout in its own right.
        /// </summary>
        const float RingPad = 12f;

        RectTransform _root, _stickBase, _stickKnob, _stickZone;
        readonly List<Btn> _buttons = new();

        class Btn
        {
            public RectTransform Rect;
            public Image Ring, Fill;
            /// <summary>The lit part of the ring - a radial-filled copy of <see cref="Ring"/>
            /// sitting exactly on top of it. Disabled outright on a button with no
            /// <see cref="Progress"/>, so those keep the plain outline they always had.</summary>
            public Image Arc;
            public Text Label;
            /// <summary>The key that fires this, under the label - shown only off touch.</summary>
            public Text Key;
            public string Keyboard;
            public System.Func<string> Pad;
            /// <summary>Whether the physical input behind this button is down, so a readout
            /// lights as its key is pressed. Null for buttons with no such input.</summary>
            public System.Func<bool> Held;
            public Controls.Context Show;
            public System.Action<bool> Set;
            public System.Func<bool> Enabled;
            /// <summary>
            /// How close this action is to being available, 0 to 1, filling toward ready. Null
            /// for a button that is always pressable - a screen, a cancel - which is what turns
            /// the ring back into an ordinary outline rather than a meter permanently at 100%.
            /// </summary>
            public System.Func<float> Progress;
            /// <summary>Whether the button exists at all this run. Null means always - most
            /// buttons are fixtures of their context. Only a second ability, which not every
            /// element has built, needs to disappear entirely rather than sit permanently dim.</summary>
            public System.Func<bool> Visible;
            public int Finger = -1;
        }

        // ---------------------------------------------------------------- build

        public static TouchControls Build(Transform canvas)
        {
            var go = new GameObject("touch");
            go.transform.SetParent(canvas, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tc = go.AddComponent<TouchControls>();
            tc._root = rt;
            tc.Construct();
            return tc;
        }

        void Construct()
        {
            // The zone the stick may be summoned in. Invisible, and deliberately the whole height
            // of the left side rather than a disc around the home position: a thumb reaching for a
            // stick on a phone lands anywhere in that quadrant, and a stick that only answers
            // inside its own graphic is a stick you have to look at.
            _stickZone = UiKit.Rect(_root, "stickzone", Vector2.zero, new Vector2(StickZoneWidth, 1f),
                                    Vector2.zero, Vector2.zero);

            // ThinRing, not Ring: Ring is the soft fat band every impact burst in the game
            // shares, and at this size it reads as a smudge rather than as an edge.
            _stickBase = Disc(_root, "stick.base", StickRadius, Spr.ThinRing, new Color(1f, 1f, 1f, 0.22f));
            _stickKnob = Disc(_root, "stick.knob", StickKnob, Spr.Circle, new Color(1f, 1f, 1f, 0.22f));
            HomeStick();

            // ---- actions ----
            //
            // The three combat buttons stand off each other by more than the sum of their radii
            // plus RingPad, so no ring ever overlaps a neighbour's disc. That is a layout rule
            // rather than a taste one: two rings crossing read as a single ring at a wrong
            // radius, and a player with a thumb over half of it cannot tell which is which.
            Button("ATTACK", ActionR, BottomRight, new Vector2(-250f, 250f), Controls.Context.Arena,
                   v => Controls.VirtualAttack = v, null,
                   progress: () => Controls.AttackProgress01?.Invoke() ?? 1f,
                   key: "LMB", pad: () => GamepadGlyphs.Attack, held: () => Controls.AttackHeld);
            Button("ABILITY", AbilityR, BottomRight, new Vector2(-490f, 420f), Controls.Context.Arena,
                   v => Controls.VirtualRelease = v, () => Controls.ReleaseReady != null && Controls.ReleaseReady(),
                   progress: () => Controls.ReleaseProgress01?.Invoke() ?? 0f,
                   key: "RMB", pad: () => GamepadGlyphs.Release, held: () => Controls.ReleaseHeld);
            // The chest's defensive ability. Below ABILITY and clear of ATTACK's own radius -
            // shares Alt with the hub's crate-edit toggle, since the two contexts never overlap.
            Button("GUARD", AbilityR, BottomRight, new Vector2(-490f, 120f), Controls.Context.Arena,
                   v => Controls.VirtualAlt = v, () => Controls.GuardReady != null && Controls.GuardReady(),
                   progress: () => Controls.GuardProgress01?.Invoke() ?? 1f,
                   key: "Q", pad: () => GamepadGlyphs.Guard, held: () => Controls.GuardHeld);
            Button("GEAR", SmallR, TopRight, new Vector2(-120f, -120f), Controls.Context.Arena,
                   v => Controls.VirtualLoadout = v, null,
                   key: "C", pad: () => GamepadGlyphs.Menu, held: () => Controls.LoadoutHeld);
            Button("MENU", SmallR, TopRight, new Vector2(-290f, -120f), Controls.Context.Arena,
                   v => Controls.VirtualCancel = v, null,
                   key: "ESC", pad: () => GamepadGlyphs.Menu, held: () => Controls.CancelHeld);

            Button("USE", ActionR, BottomRight, new Vector2(-250f, 250f), Controls.Context.Hub,
                   v => Controls.VirtualInteract = v, null,
                   key: "E", pad: () => GamepadGlyphs.Interact, held: () => Controls.InteractHeld);
            // A showcase Frame's "look closer", on the VirtualSecondAbility input - the arena no
            // longer claims it (a chosen second ability fires from ABILITY instead). Doing
            // nothing when nothing with art is focused, same as USE does when nothing is.
            Button("VIEW", SmallR, BottomRight, new Vector2(-470f, 250f), Controls.Context.Hub,
                   v => Controls.VirtualSecondAbility = v, null,
                   key: "R", pad: () => GamepadGlyphs.SecondAbility, held: () => Controls.SecondAbilityHeld);
            Button("GEAR", SmallR, TopRight, new Vector2(-120f, -120f), Controls.Context.Hub,
                   v => Controls.VirtualLoadout = v, null,
                   key: "C", pad: () => GamepadGlyphs.Loadout, held: () => Controls.LoadoutHeld);

            // Placing a trophy is its own set, because it is the one state with THREE answers -
            // put it down, send it back to the crate, put it back where it was. Two of those
            // cannot share a button without one of them becoming undiscoverable.
            Button("PLACE", ActionR, BottomRight, new Vector2(-250f, 250f), Controls.Context.Placing,
                   v => Controls.VirtualInteract = v, null,
                   key: "E", pad: () => GamepadGlyphs.Interact, held: () => Controls.InteractHeld);
            Button("CRATE", SmallR, BottomRight, new Vector2(-470f, 400f), Controls.Context.Placing,
                   v => Controls.VirtualAlt = v, null,
                   key: "Q", pad: () => GamepadGlyphs.Alt, held: () => Controls.AltHeld);
            Button("CANCEL", SmallR, TopRight, new Vector2(-120f, -120f), Controls.Context.Placing,
                   v => Controls.VirtualCancel = v, null,
                   key: "ESC", pad: () => GamepadGlyphs.Cancel, held: () => Controls.CancelHeld);

            // ONE back button for every modal, and it does exactly what Escape does.
            //
            // The alternative was a close button drawn into each of the seven screens, which is
            // seven chances for one of them to disagree with the key that already closes it. This
            // cannot drift: it sets the same virtual Cancel every screen is already reading.
            //
            // It is not shown for a screen that must be answered - a floor reward mid-choice, an
            // offer that cannot be refused - because a button that does nothing is worse than no
            // button. See FloorRewardScreen.CanDismiss.
            Button("BACK", SmallR, TopRight, new Vector2(-120f, -120f), Controls.Context.Modal,
                   v => Controls.VirtualCancel = v, null);

            _mode = Controls.Screen;
            Relayout();
        }

        static readonly Vector2 BottomRight = new(1f, 0f);
        static readonly Vector2 TopRight = new(1f, 1f);

        static RectTransform Disc(Transform parent, string name, float radius, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.sizeDelta = new Vector2(radius * 2f, radius * 2f);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        void Button(string label, float radius, Vector2 anchor, Vector2 offset,
                    Controls.Context show,
                    System.Action<bool> set, System.Func<bool> enabled,
                    System.Func<bool> visible = null,
                    System.Func<float> progress = null,
                    string key = null, System.Func<string> pad = null,
                    System.Func<bool> held = null)
        {
            var go = new GameObject("btn." + label.ToLower(), typeof(Image));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(radius * 2f, radius * 2f);

            var fill = go.GetComponent<Image>();
            fill.sprite = Spr.Circle;
            fill.color = new Color(1f, 1f, 1f, 0.10f);
            fill.raycastTarget = false;

            // Two copies of the same ring, same geometry, drawn in order: a dim TRACK showing
            // where the light will go, and the lit ARC on top showing how far it has got. Drawn
            // as a pair rather than one ring changing length, because a bare arc with no track
            // leaves the button's edge vanishing and returning, and an edge that disappears
            // reads as the button itself being gone.
            var ringRt = Ring(rt, "ring.track", radius, new Color(1f, 1f, 1f, 0.30f));
            var arcRt = Ring(rt, "ring.arc", radius, new Color(1f, 1f, 1f, 0f));

            var arc = arcRt.GetComponent<Image>();
            arc.type = Image.Type.Filled;
            arc.fillMethod = Image.FillMethod.Radial360;
            // From 12 o'clock, clockwise - the direction a clock face already taught everybody,
            // and the one every ring in this overlay must share. Mixed directions across four
            // buttons is four separate things to learn instead of one.
            arc.fillOrigin = (int)Image.Origin360.Top;
            arc.fillClockwise = true;
            arc.fillAmount = 0f;
            // No meter to draw: the ring stays an ordinary outline and this copy never draws.
            if (progress == null) arc.enabled = false;

            var text = UiKit.Label(rt, label, radius > 90f ? 30 : 22,
                                   new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            // The key, in the disc's lower third - below the label without moving it, so the
            // name sits in the same place on every device.
            Text keyText = null;
            if (key != null)
            {
                keyText = UiKit.Label(rt, key, radius > 90f ? 20 : 15,
                                      new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleCenter);
                var krt = (RectTransform)keyText.transform;
                krt.anchorMin = new Vector2(0f, 0.12f); krt.anchorMax = new Vector2(1f, 0.36f);
                krt.offsetMin = Vector2.zero; krt.offsetMax = Vector2.zero;
            }

            _buttons.Add(new Btn
            {
                Rect = rt, Ring = ringRt.GetComponent<Image>(), Arc = arc, Fill = fill, Label = text,
                Key = keyText, Keyboard = key, Pad = pad, Held = held,
                Show = show, Set = set, Enabled = enabled, Visible = visible, Progress = progress,
            });
        }

        /// <summary>
        /// A ring centred on its parent button and standing <see cref="RingPad"/> outside it.
        /// Not <see cref="Disc"/>: that one anchors bottom-left for the stick's benefit, and is
        /// sized to the radius it is given rather than past it.
        /// </summary>
        static RectTransform Ring(Transform parent, string name, float radius, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            // ThinRing's band sits at 0.92 of the half-size, so the drawn light lands just
            // outside the disc - deriving the rect from the pad rather than the other way round
            // would put it a few units in from where RingPad says it is.
            float half = (radius + RingPad) / 0.92f;
            rt.sizeDelta = new Vector2(half * 2f, half * 2f);
            var img = go.GetComponent<Image>();
            img.sprite = Spr.ThinRing;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        void HomeStick()
        {
            _stickBase.anchoredPosition = new Vector2(StickHome, StickHome);
            _stickKnob.anchoredPosition = _stickBase.anchoredPosition;
        }

        void Relayout()
        {
            bool touch = Controls.TouchMode;
            // Off touch the buttons are readouts, and a modal's BACK reads nothing a screen's
            // own "[ESC] to close" does not already say.
            bool on = _mode != Controls.Context.Hidden && (touch || _mode != Controls.Context.Modal);

            // No stick over a modal: there is nothing to steer, and it would sit on top of the
            // screen's own controls. Its finger is freed with it, so a drag on the mastery board
            // is a drag on the board. No stick off touch at all - WASD has nothing to show.
            bool stick = on && touch && _mode != Controls.Context.Modal;
            _stickBase.gameObject.SetActive(stick);
            _stickKnob.gameObject.SetActive(stick);
            foreach (var b in _buttons)
            {
                b.Rect.gameObject.SetActive(on && b.Show == _mode && (b.Visible == null || b.Visible()));
                if (b.Key != null) b.Key.gameObject.SetActive(!touch);
            }
        }

        // ---------------------------------------------------------------- tick

        int _stickFinger = -1;
        int _freeFinger = -1;
        bool _lastTouchMode;

        void Update()
        {
            if (_lastTouchMode != Controls.TouchMode || _mode != Controls.Screen)
            {
                _lastTouchMode = Controls.TouchMode;
                _mode = Controls.Screen;
                Relayout();
            }

            Controls.FreeTaps.Clear();
            Controls.FreePointer = null;
            Controls.ClaimedPrimary = false;

            var screen = Touchscreen.current;
            if (screen == null || _mode == Controls.Context.Hidden || !Controls.TouchMode)
            {
                if (screen != null) Pinch(screen.touches);
                if (screen != null) CollectFreeTaps(screen, claimNothing: true);
                ReleaseAll();
                if (_mode != Controls.Context.Hidden && !Controls.TouchMode) PaintReadouts();
                return;
            }

            var touches = screen.touches;
            bool stickStillDown = false;

            // Pinch first, since it needs two fingers and must win over anything either of them
            // would otherwise have pressed. Only meaningful on a screen that asks for it; the
            // arena and the hub never do, so this costs a distance check.
            Pinch(touches);

            foreach (var t in touches)
            {
                var phase = t.phase.ReadValue();
                bool down = phase == UnityEngine.InputSystem.TouchPhase.Began;
                bool active = phase is UnityEngine.InputSystem.TouchPhase.Began
                                    or UnityEngine.InputSystem.TouchPhase.Moved
                                    or UnityEngine.InputSystem.TouchPhase.Stationary;
                if (!active) continue;

                int id = t.touchId.ReadValue();
                var pos = t.position.ReadValue();

                // ---- the stick keeps the finger it started with, wherever it goes ----
                if (id == _stickFinger)
                {
                    DriveStick(pos);
                    stickStillDown = true;
                    if (t == screen.primaryTouch) Controls.ClaimedPrimary = true;
                    continue;
                }

                var held = _buttons.Find(b => b.Finger == id);
                if (held != null)
                {
                    if (t == screen.primaryTouch) Controls.ClaimedPrimary = true;
                    continue;   // a button holds until the finger lifts, even if it slides off
                }

                // A finger that was free when it went down STAYS free, and is published for as
                // long as it is down - that is what a screen drags with. Without this only the
                // frame it landed on was ever visible to anything outside this file.
                if (!down)
                {
                    if (id == _freeFinger) Controls.FreePointer = pos;
                    continue;
                }

                var pressed = _buttons.Find(b => b.Rect.gameObject.activeSelf && Inside(b.Rect, pos));
                if (pressed != null)
                {
                    pressed.Finger = id;
                    if (t == screen.primaryTouch) Controls.ClaimedPrimary = true;
                    continue;
                }

                if (_stickFinger < 0 && _mode != Controls.Context.Modal && Inside(_stickZone, pos))
                {
                    _stickFinger = id;
                    SummonStick(pos);
                    DriveStick(pos);
                    stickStillDown = true;
                    if (t == screen.primaryTouch) Controls.ClaimedPrimary = true;
                    continue;
                }

                // Claimed by nobody: the HUD, a screen, or empty air.
                _freeFinger = id;
                Controls.FreePointer = pos;
                Controls.FreeTaps.Add(pos);
            }

            if (!stickStillDown) DropStick();

            foreach (var b in _buttons)
            {
                bool down = b.Finger >= 0 && StillDown(touches, b.Finger);
                if (!down) b.Finger = -1;
                b.Set(down);
                Paint(b, down);
            }
        }

        /// <summary>
        /// Free taps still have to be published when the overlay is off, or a touch device with no
        /// controls up - every modal screen - would have no pointer at all.
        /// </summary>
        void CollectFreeTaps(Touchscreen screen, bool claimNothing)
        {
            foreach (var t in screen.touches)
            {
                var p = t.phase.ReadValue();
                if (p == UnityEngine.InputSystem.TouchPhase.Began)
                    Controls.FreeTaps.Add(t.position.ReadValue());
                if (p is UnityEngine.InputSystem.TouchPhase.Began
                       or UnityEngine.InputSystem.TouchPhase.Moved
                       or UnityEngine.InputSystem.TouchPhase.Stationary)
                    Controls.FreePointer = t.position.ReadValue();
            }
        }

        static bool StillDown(UnityEngine.InputSystem.Utilities.ReadOnlyArray<TouchControl> touches, int id)
        {
            foreach (var t in touches)
            {
                if (t.touchId.ReadValue() != id) continue;
                var p = t.phase.ReadValue();
                return p is UnityEngine.InputSystem.TouchPhase.Began
                         or UnityEngine.InputSystem.TouchPhase.Moved
                         or UnityEngine.InputSystem.TouchPhase.Stationary;
            }
            return false;
        }

        void ReleaseAll()
        {
            DropStick();
            foreach (var b in _buttons) { b.Finger = -1; b.Set(false); Paint(b, false); }
        }

        /// <summary>
        /// Off touch: each shown button lit while its key is held, labelled with the key for the
        /// device in hand. Read every frame rather than on a mode change, because a pad button
        /// can be rebound from the controls screen mid-session. After <see cref="ReleaseAll"/>,
        /// so the virtual inputs it just cleared are not read back as held.
        /// </summary>
        void PaintReadouts()
        {
            bool pad = Controls.GamepadMode;
            foreach (var b in _buttons)
            {
                if (!b.Rect.gameObject.activeSelf) continue;
                Paint(b, b.Held != null && b.Held());
                if (b.Key == null) continue;
                string k = pad && b.Pad != null ? b.Pad() : b.Keyboard;
                if (b.Key.text != k) b.Key.text = k;
            }
        }

        /// <summary>
        /// Dim while filling, bright when the press would land. WHITE throughout, and that is a
        /// rule rather than a default: every other cue on the character is its element, so a
        /// tinted ring would be filed with those - and this one says the same thing on a Fire
        /// character as on a Water one. It is also what a click looks like, which is what these
        /// want to be once there is sound.
        /// </summary>
        const float RingFilling = 0.22f;
        const float RingReady = 0.85f;
        const float RingTrack = 0.12f;

        void Paint(Btn b, bool down)
        {
            bool live = b.Enabled == null || b.Enabled();
            float a = down ? 0.34f : live ? 0.10f : 0.05f;
            b.Fill.color = new Color(1f, 1f, 1f, a);
            b.Label.color = new Color(1f, 1f, 1f, down ? 0.95f : live ? 0.75f : 0.28f);

            if (b.Arc == null || !b.Arc.enabled)
            {
                b.Ring.color = new Color(1f, 1f, 1f, down ? 0.85f : live ? 0.30f : 0.14f);
                return;
            }

            float p = Mathf.Clamp01(b.Progress());
            b.Arc.fillAmount = p;

            // WHAT COUNTS AS READY IS THE Enabled FLAG, NOT A FULL RING, wherever there is one.
            // The two genuinely differ: Earth releases from a fifth of a charge and Water from
            // its first tier, so a ring that only lit at 100% would sit dim over a button that
            // has been pressable for seconds. ATTACK is the case with no flag - its gate IS its
            // meter - and there a full ring is exactly the right test, since AttackCooldown01
            // reaches 1 at the moment the gate opens.
            bool ready = b.Enabled != null ? live : p >= 0.999f;
            b.Arc.color = new Color(1f, 1f, 1f, down ? 0.95f : ready ? RingReady : RingFilling);
            b.Ring.color = new Color(1f, 1f, 1f, RingTrack);
        }

        // ---------------------------------------------------------------- the stick

        void SummonStick(Vector2 screenPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root, screenPoint, null, out var local)) return;

            // The base is anchored bottom-left, so shift out of the rect's centre-origin space.
            var origin = _root.rect.size * 0.5f;
            _stickBase.anchoredPosition = local + origin;
            _stickKnob.anchoredPosition = _stickBase.anchoredPosition;
        }

        void DriveStick(Vector2 screenPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _root, screenPoint, null, out var local)) return;

            var origin = _root.rect.size * 0.5f;
            var delta = (local + origin) - _stickBase.anchoredPosition;
            var clamped = Vector2.ClampMagnitude(delta, StickRadius);
            _stickKnob.anchoredPosition = _stickBase.anchoredPosition + clamped;

            // Below the dead zone the character does not move AT ALL, rather than crawling.
            // PlayerController treats any input above 0.01 as movement, and air's momentum now
            // reads that same flag - so a thumb resting on the glass would quietly count as
            // running. The dead zone is what stops a stationary finger being an input.
            float mag = clamped.magnitude / StickRadius;
            Controls.VirtualMove = mag < DeadZone ? Vector2.zero : clamped.normalized * Mathf.InverseLerp(DeadZone, 1f, mag);
        }

        void DropStick()
        {
            _stickFinger = -1;
            Controls.VirtualMove = Vector2.zero;
            HomeStick();
        }

        // ---------------------------------------------------------------- pinch

        float _pinchPrev = -1f;

        void Pinch(UnityEngine.InputSystem.Utilities.ReadOnlyArray<TouchControl> touches)
        {
            Vector2 a = default, b = default;
            int n = 0;
            foreach (var t in touches)
            {
                var p = t.phase.ReadValue();
                if (p is not (UnityEngine.InputSystem.TouchPhase.Began
                            or UnityEngine.InputSystem.TouchPhase.Moved
                            or UnityEngine.InputSystem.TouchPhase.Stationary)) continue;
                if (n == 0) a = t.position.ReadValue();
                else if (n == 1) b = t.position.ReadValue();
                n++;
            }

            // Controls owns the Pinching flag - see the note there. This only measures.
            if (n != 2) { _pinchPrev = -1f; return; }

            float d = Vector2.Distance(a, b);
            // The LOG OF THE RATIO the fingers moved apart - see Controls.PinchDelta. Guarded
            // against a zero separation, which is two fingers landing on the same texel.
            if (_pinchPrev > 1f && d > 1f) Controls.PinchDelta += Mathf.Log(d / _pinchPrev);
            _pinchPrev = d;
        }

        static bool Inside(RectTransform rt, Vector2 screenPoint)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, null);
    }
}
