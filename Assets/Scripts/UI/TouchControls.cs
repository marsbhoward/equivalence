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
    /// moves between a phone and a desk finds the same picture. Off touch there is no stick, each
    /// button carries the key (or pad button) that fires it and lights while that key is held -
    /// and it is still CLICKABLE: a left click on one presses it, exactly as a finger would. A
    /// button that is drawn but ignores the pointer reads as broken, not as a readout (the user's
    /// call, 2026-10-29). The cooldown rings mean the same thing either way. Modals keep their
    /// BACK button to touch, where it is the only way out; a keyboard has Escape and every screen
    /// already says so.
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

        /// <summary>
        /// How far past its drawn ring a button still takes a press, as a multiple of the ring's
        /// radius. A thumb lands where it MEANT to, give or take its own width, and the drawn
        /// disc is smaller than the pad of a thumb on a phone - so a press just off the edge
        /// counted for nothing, and USE in the corner was a target to aim at rather than to hit.
        /// Overlaps are settled by distance (<see cref="ButtonAt"/>), so the slop never steals a
        /// press from the neighbour it was nearer.
        /// </summary>
        const float HitSlop = 1.25f;

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
            /// <summary>A tap that went down and up between two frames - pressed for exactly
            /// one frame, see <see cref="TouchControls.Update"/>.</summary>
            public bool Pulse;

            /// <summary>
            /// Whether dragging this button AIMS a weapon art (ATTACK only - the user's call,
            /// 2026-10-14). Held, it is the hold to aim; the finger's travel from where it landed
            /// is the aim (Controls.VirtualAim) once it passes AimDragFraction while an aimable art
            /// is pending; lifting fires. A tap that never drags auto-targets as ever.
            /// </summary>
            public bool DragAims;
            public Vector2 DownAt, At;
            public bool Aiming;

            /// <summary>Where it sits at medium size, right-handed - Relayout scales and mirrors
            /// from these (<see cref="HudPrefs"/>).</summary>
            public Vector2 Anchor, Offset;
            /// <summary>A rounded bar rather than a disc: the screen buttons in the top corner.</summary>
            public bool Pill;
            /// <summary>A line over the button naming what it would act on - USE's fixture
            /// (<see cref="Controls.InteractTarget"/>). Null for every other button.</summary>
            public Text Tag;
            public System.Func<string> TagText;
        }

        /// <summary>How far, in screen pixels as a fraction of the screen's height, a finger on a
        /// drag-aiming button must travel before it aims - a tap's own wobble stays a tap.</summary>
        const float AimDragFraction = 0.05f;

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
            // The combat buttons stand off each other by more than the sum of their radii plus
            // RingPad, so no ring ever overlaps a neighbour's disc. That is a layout rule rather
            // than a taste one: two rings crossing read as a single ring at a wrong radius, and a
            // player with a thumb over half of it cannot tell which is which.
            //
            // THE THUMB ARC (the user's sign-off, 2026-10-29): everything the right thumb reaches
            // for mid-fight sits on one arc around ATTACK at the same reach, so the thumb pivots
            // from ATTACK to the others instead of hunting for each - see ThumbArc.
            Button("ATTACK", ActionR, BottomRight, Home, Controls.Context.Arena,
                   v => Controls.VirtualAttack = v, null,
                   progress: () => Controls.AttackProgress01?.Invoke() ?? 1f,
                   key: "LMB", pad: () => GamepadGlyphs.Attack, held: () => Controls.AttackHeld,
                   dragAims: true);
            Button("ABILITY", AbilityR, BottomRight, ThumbArc(64f, AbilityReach), Controls.Context.Arena,
                   v => Controls.VirtualRelease = v, () => Controls.ReleaseReady != null && Controls.ReleaseReady(),
                   progress: () => Controls.ReleaseProgress01?.Invoke() ?? 0f,
                   key: "RMB", pad: () => GamepadGlyphs.Release, held: () => Controls.ReleaseHeld);
            // The chest's defensive ability. Low on the arc, clear of ATTACK's own radius -
            // shares Alt with the hub's crate-edit toggle, since the two contexts never overlap.
            Button("GUARD", AbilityR, BottomRight, ThumbArc(10f, AbilityReach), Controls.Context.Arena,
                   v => Controls.VirtualAlt = v, () => Controls.GuardReady != null && Controls.GuardReady(),
                   progress: () => Controls.GuardProgress01?.Invoke() ?? 1f,
                   key: "Q", pad: () => GamepadGlyphs.Guard, held: () => Controls.GuardHeld);
            Pill("GEAR", PillW, TopRight, new Vector2(-120f, -86f), Controls.Context.Arena,
                 v => Controls.VirtualLoadout = v,
                 key: "C", pad: () => GamepadGlyphs.Menu, held: () => Controls.LoadoutHeld);
            Pill("MENU", PillW, TopRight, new Vector2(-290f, -86f), Controls.Context.Arena,
                 v => Controls.VirtualCancel = v,
                 key: "ESC", pad: () => GamepadGlyphs.Menu, held: () => Controls.CancelHeld);

            // USE names what it would use, and dims when nothing is in reach - USE that "did
            // nothing" in the hub was, as often as not, USE with nothing in front of it.
            Button("USE", ActionR, BottomRight, Home, Controls.Context.Hub,
                   v => Controls.VirtualInteract = v, () => Controls.InteractTarget != null,
                   key: "E", pad: () => GamepadGlyphs.Interact, held: () => Controls.InteractHeld,
                   tag: () => Controls.InteractTarget);
            // A showcase Frame's "look closer", on the VirtualSecondAbility input - the arena no
            // longer claims it (a chosen second ability fires from ABILITY instead). Doing
            // nothing when nothing with art is focused, same as USE does when nothing is.
            Button("VIEW", SmallR, BottomRight, ThumbArc(38f, SmallReach), Controls.Context.Hub,
                   v => Controls.VirtualSecondAbility = v, null,
                   key: "R", pad: () => GamepadGlyphs.SecondAbility, held: () => Controls.SecondAbilityHeld);
            Pill("GEAR", PillW, TopRight, new Vector2(-120f, -86f), Controls.Context.Hub,
                 v => Controls.VirtualLoadout = v,
                 key: "C", pad: () => GamepadGlyphs.Loadout, held: () => Controls.LoadoutHeld);

            // Placing a trophy is its own set, because it is the one state with THREE answers -
            // put it down, send it back to the crate, put it back where it was. Two of those
            // cannot share a button without one of them becoming undiscoverable.
            Button("PLACE", ActionR, BottomRight, Home, Controls.Context.Placing,
                   v => Controls.VirtualInteract = v, null,
                   key: "E", pad: () => GamepadGlyphs.Interact, held: () => Controls.InteractHeld);
            Button("CRATE", SmallR, BottomRight, ThumbArc(38f, SmallReach), Controls.Context.Placing,
                   v => Controls.VirtualAlt = v, null,
                   key: "Q", pad: () => GamepadGlyphs.Alt, held: () => Controls.AltHeld);
            Pill("CANCEL", PillW + 20f, TopRight, new Vector2(-130f, -86f), Controls.Context.Placing,
                 v => Controls.VirtualCancel = v,
                 key: "ESC", pad: () => GamepadGlyphs.Cancel, held: () => Controls.CancelHeld);
            // A frame's own two choices: hang it or stand it, and which way up its art goes.
            Button("HANG", SmallR, BottomRight, ThumbArc(72f, SmallReach), Controls.Context.Placing,
                   v => Controls.VirtualHang = v, null,
                   key: "R", pad: () => "(Y)");
            Pill("< TURN", PillW, TopRight, new Vector2(-480f, -86f), Controls.Context.Placing,
                 v => Controls.VirtualTurnLeft = v, key: "Z", pad: () => "(<)");
            Pill("TURN >", PillW, TopRight, new Vector2(-310f, -86f), Controls.Context.Placing,
                 v => Controls.VirtualTurnRight = v, key: "X", pad: () => "(>)");

            // ONE back button for every modal, and it does exactly what Escape does.
            //
            // The alternative was a close button drawn into each of the seven screens, which is
            // seven chances for one of them to disagree with the key that already closes it. This
            // cannot drift: it sets the same virtual Cancel every screen is already reading.
            //
            // It is not shown for a screen that must be answered - a floor reward mid-choice, an
            // offer that cannot be refused - because a button that does nothing is worse than no
            // button. See FloorRewardScreen.CanDismiss.
            Pill("BACK", PillW, TopRight, new Vector2(-120f, -86f), Controls.Context.Modal,
                 v => Controls.VirtualCancel = v);

            _mode = Controls.Screen;
            Relayout();
        }

        static readonly Vector2 BottomRight = new(1f, 0f);
        static readonly Vector2 TopRight = new(1f, 1f);

        /// <summary>ATTACK's spot - and USE's and PLACE's, which stand in for it - from the
        /// bottom-right corner. The thumb arc is measured from here.</summary>
        static readonly Vector2 Home = new(-250f, 250f);
        const float AbilityReach = 290f, SmallReach = 270f;

        /// <summary>A point on the thumb arc round <see cref="Home"/>: 0 degrees straight left of
        /// it, 90 straight above, <paramref name="reach"/> canvas units out.</summary>
        static Vector2 ThumbArc(float degrees, float reach)
        {
            float a = degrees * Mathf.Deg2Rad;
            return Home + new Vector2(-Mathf.Cos(a), Mathf.Sin(a)) * reach;
        }

        /// <summary>The corner pills: a full thumb wide, a little under one tall, so two fit
        /// beside the wave counter (Hud) without touching it.</summary>
        const float PillW = 150f, PillH = 80f;

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
                    System.Func<bool> held = null, bool dragAims = false,
                    System.Func<string> tag = null)
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

            // The tag rides above the ring, gold like every other "this is what it will do" line
            // in the hub's prompts, and wide enough for a fixture's whole name.
            Text tagText = null;
            if (tag != null)
            {
                tagText = UiKit.Label(rt, "", 22, TagLive, TextAnchor.LowerCenter);
                var grt = (RectTransform)tagText.transform;
                grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 1f);
                grt.offsetMin = new Vector2(-180f, RingPad + 10f);
                grt.offsetMax = new Vector2(180f, RingPad + 46f);
                UiKit.Legible(tagText);
            }

            _buttons.Add(new Btn
            {
                Rect = rt, Ring = ringRt.GetComponent<Image>(), Arc = arc, Fill = fill, Label = text,
                Key = keyText, Keyboard = key, Pad = pad, Held = held,
                Show = show, Set = set, Enabled = enabled, Visible = visible, Progress = progress,
                DragAims = dragAims, Anchor = anchor, Offset = offset, Tag = tagText, TagText = tag,
            });
        }

        static readonly Color TagLive = new(0.85f, 0.71f, 0.35f, 0.95f);
        static readonly Color TagDim = new(0.85f, 0.71f, 0.35f, 0.40f);

        /// <summary>
        /// A screen button in the top corner: a rounded bar with the same fill, outline and
        /// labels as the discs, and no meter - none of them has a cooldown. The ring stands
        /// <see cref="RingPad"/> outside it like a disc's, so every button's edge reads the same.
        /// </summary>
        void Pill(string label, float width, Vector2 anchor, Vector2 offset, Controls.Context show,
                  System.Action<bool> set,
                  string key = null, System.Func<string> pad = null, System.Func<bool> held = null)
        {
            var go = new GameObject("btn." + label.ToLower(), typeof(Image));
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(width, PillH);

            var fill = go.GetComponent<Image>();
            fill.sprite = Spr.PillFill;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = Spr.PillMultiplier(PillH);
            fill.color = new Color(1f, 1f, 1f, 0.10f);
            fill.raycastTarget = false;

            // Sized as Ring sizes a disc's: ThinRing's band sits at 0.92 of the slice, so the rect
            // is grown until the band lands RingPad outside the bar.
            float h = (PillH + 2f * RingPad) / 0.92f;
            var ringGo = new GameObject("ring.track", typeof(Image));
            ringGo.transform.SetParent(rt, false);
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.anchorMin = ringRt.anchorMax = ringRt.pivot = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(width + (h - PillH), h);
            var ring = ringGo.GetComponent<Image>();
            ring.sprite = Spr.PillRing;
            ring.type = Image.Type.Sliced;
            ring.pixelsPerUnitMultiplier = Spr.PillMultiplier(h);
            ring.color = new Color(1f, 1f, 1f, 0.30f);
            ring.raycastTarget = false;

            var text = UiKit.Label(rt, label, 22, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter);
            var trt = (RectTransform)text.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;

            // Off touch the key sits to the label's right rather than under it - a pill has no
            // lower third to put it in.
            Text keyText = null;
            if (key != null)
            {
                trt.offsetMax = new Vector2(-26f, 0f);
                keyText = UiKit.Label(rt, key, 15, new Color(1f, 1f, 1f, 0.5f), TextAnchor.MiddleRight);
                var krt = (RectTransform)keyText.transform;
                krt.anchorMin = Vector2.zero; krt.anchorMax = Vector2.one;
                krt.offsetMin = new Vector2(0f, 0f); krt.offsetMax = new Vector2(-18f, 0f);
            }

            _buttons.Add(new Btn
            {
                Rect = rt, Ring = ring, Arc = null, Fill = fill, Label = text,
                Key = keyText, Keyboard = key, Pad = pad, Held = held,
                Show = show, Set = set, Anchor = anchor, Offset = offset, Pill = true,
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
            // Left-handed, the stick rests in the bottom-RIGHT corner; the base is still anchored
            // bottom-left, so that is the root's width in from it.
            float x = HudPrefs.LeftHanded ? _root.rect.width - StickHome : StickHome;
            _stickBase.anchoredPosition = new Vector2(x, StickHome);
            _stickKnob.anchoredPosition = _stickBase.anchoredPosition;
        }

        /// <summary>Whether a stick is drawn this frame - see <see cref="Controls.StickShown"/>.</summary>
        bool _stickShown;

        /// <summary>Above the screens (10) and under the gamepad cursor (20) - see Relayout.</summary>
        const int ModalSortOrder = 15;
        int? _baseSortOrder;

        /// <summary>
        /// Whether the buttons are touch controls this frame: <see cref="Controls.OverlayTouch"/>, or a modal
        /// under a finger whatever HUD &amp; TOUCH says - OFF there must not take away the only way out of a
        /// screen on a phone.
        /// </summary>
        bool Live => Controls.OverlayTouch || (Controls.Screen == Controls.Context.Modal && Controls.TouchMode);

        void Relayout()
        {
            bool touch = Live;
            // Off touch the buttons are readouts, and a modal's BACK reads nothing a screen's
            // own "[ESC] to close" does not already say. OFF in HUD & TOUCH takes them all down -
            // except a modal's BACK under a finger, which is the only way out of a screen on touch
            // (see Live).
            bool modal = _mode == Controls.Context.Modal;
            bool on = _mode != Controls.Context.Hidden
                      && (modal ? touch : HudPrefs.OnScreen != HudPrefs.Overlay.Off);

            // OVER the screens while one is up. The canvas sits under them (GameBootstrap) so a modal
            // covers the stick and the action buttons - but BACK is the one button a modal has, and a
            // screen with an opaque backdrop painted straight over it: on touch, the hub's loadout and
            // the transmutation circle had no way out at all. BACK is all a modal shows here, so
            // lifting the whole canvas lifts only it.
            var canvas = _root != null ? _root.GetComponentInParent<Canvas>() : null;
            if (canvas != null)
            {
                if (canvas.sortingOrder != ModalSortOrder) _baseSortOrder = canvas.sortingOrder;
                canvas.sortingOrder = modal ? ModalSortOrder : _baseSortOrder ?? canvas.sortingOrder;
            }

            // No stick over a modal: there is nothing to steer, and it would sit on top of the
            // screen's own controls. Its finger is freed with it, so a drag on the mastery board
            // is a drag on the board. No stick off touch at all - WASD has nothing to show.
            _stickShown = on && touch && _mode != Controls.Context.Modal;

            // In a run with the WHEEL layout the HUD's rotation wheel is drawn as the stick (Hud),
            // so this file's plain ring and knob stand down - the stick itself still works the same.
            bool wheel = _mode == Controls.Context.Arena && HudPrefs.Current == HudPrefs.Layout.Wheel;
            _stickBase.gameObject.SetActive(_stickShown && !wheel);
            _stickKnob.gameObject.SetActive(_stickShown && !wheel);

            bool left = HudPrefs.LeftHanded;
            _stickZone.anchorMin = left ? new Vector2(1f - StickZoneWidth, 0f) : Vector2.zero;
            _stickZone.anchorMax = left ? Vector2.one : new Vector2(StickZoneWidth, 1f);

            // SIZE scales each button about its own centre and its distance from the corner alike,
            // so the gaps the layout rule above depends on scale with them. LEFT-HANDED mirrors the
            // bottom corner only: GEAR and MENU stay top-right, where mirrored they would sit on
            // the run clock.
            float scale = HudPrefs.ButtonScale;
            bool hints = !touch && HudPrefs.KeyHints;
            foreach (var b in _buttons)
            {
                bool mirror = left && b.Anchor.y < 0.5f;
                b.Rect.anchorMin = b.Rect.anchorMax = mirror ? new Vector2(1f - b.Anchor.x, b.Anchor.y) : b.Anchor;
                b.Rect.anchoredPosition = new Vector2(mirror ? -b.Offset.x : b.Offset.x, b.Offset.y) * scale;
                b.Rect.localScale = new Vector3(scale, scale, 1f);
                b.Rect.gameObject.SetActive(on && b.Show == _mode && (b.Visible == null || b.Visible()));
                if (b.Key != null) b.Key.gameObject.SetActive(hints);
                // A pill's key sits beside its name, so the name makes room only while it shows.
                if (b.Pill && b.Key != null)
                    ((RectTransform)b.Label.transform).offsetMax = new Vector2(hints ? -26f : 0f, 0f);
            }
            HomeStick();
        }

        // ---------------------------------------------------------------- tick

        int _stickFinger = -1;
        int _freeFinger = -1;
        bool _lastOverlay, _lastDevice;
        int _lastPrefs = -1;

        void Update()
        {
            // Re-laid out on what it shows changing - the device, the context, a setting - and on
            // nothing else, since Relayout touches every button.
            if (_lastOverlay != Live || _lastDevice != Controls.TouchMode
                || _mode != Controls.Screen || _lastPrefs != HudPrefs.Version)
            {
                _lastOverlay = Live;
                _lastDevice = Controls.TouchMode;
                _lastPrefs = HudPrefs.Version;
                _mode = Controls.Screen;
                Relayout();
            }

            Controls.FreeTaps.Clear();
            Controls.FreePointer = null;
            Controls.ClaimedPrimary = false;
            UpdateTags();

            var screen = Touchscreen.current;
            if (screen == null || _mode == Controls.Context.Hidden || !Live)
            {
                if (screen != null) Pinch(screen.touches);
                if (screen != null) CollectFreeTaps(screen, claimNothing: true);
                ReleaseAll();
                _seen.Clear();
                if (_mode != Controls.Context.Hidden && HudPrefs.OnScreen != HudPrefs.Overlay.Off
                    && !Controls.TouchMode)
                {
                    Click();
                    PaintReadouts();
                }
                else ReleaseClick();
                PublishStick();
                return;
            }

            var touches = screen.touches;
            bool stickStillDown = false;

            // Pinch first, since it needs two fingers and must win over anything either of them
            // would otherwise have pressed. Only meaningful on a screen that asks for it; the
            // arena and the hub never do, so this costs a distance check.
            Pinch(touches);

            _seenNow.Clear();
            foreach (var t in touches)
            {
                var phase = t.phase.ReadValue();
                int id = t.touchId.ReadValue();
                bool active = phase is UnityEngine.InputSystem.TouchPhase.Began
                                    or UnityEngine.InputSystem.TouchPhase.Moved
                                    or UnityEngine.InputSystem.TouchPhase.Stationary;
                if (!active)
                {
                    if (phase == UnityEngine.InputSystem.TouchPhase.Ended) QuickTap(t, id);
                    continue;
                }
                _seenNow.Add(id);

                // A finger is NEW the first frame this file sees its id, not only on a Began
                // phase. A mobile browser does not reliably deliver one: a second finger landing
                // while the first steers can arrive already Moved (its touchstart and first
                // touchmove landing in one frame), and a finger that was never seen down was a
                // finger nothing could ever claim - the stick worked, and the button under the
                // other thumb did not.
                bool down = phase == UnityEngine.InputSystem.TouchPhase.Began || !_seen.Contains(id);
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
                    held.At = pos;   // a drag-aiming button follows its finger
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

                var pressed = ButtonAt(pos);
                if (pressed != null)
                {
                    pressed.Finger = id;
                    pressed.DownAt = pressed.At = pos;
                    pressed.Aiming = false;
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
            (_seen, _seenNow) = (_seenNow, _seen);

            foreach (var b in _buttons)
            {
                bool down = b.Finger >= 0 && StillDown(touches, b.Finger);
                if (!down) b.Finger = -1;
                down |= b.Pulse;
                b.Pulse = false;
                bool pressedNow = down;
                if (b.DragAims) pressedNow = DragAim(b, down);
                b.Set(pressedNow);
                Paint(b, down);
            }

            // ALWAYS on a machine with a touchscreen AND a mouse: the overlay is drawn as touch
            // controls, and a click on one still presses it.
            if (!Controls.TouchMode)
            {
                Click();
                if (_clicked != null) Paint(_clicked, true);
            }
            else ReleaseClick();
            PublishStick();
        }

        /// <summary>The stick's state for the HUD's wheel (<see cref="Controls.StickBase"/>).</summary>
        void PublishStick()
        {
            Controls.StickBase = _stickBase.anchoredPosition;
            Controls.StickKnob = _stickKnob.anchoredPosition;
            Controls.StickShown = _stickShown;
        }

        /// <summary>USE's line: the fixture in reach, or NOTHING IN REACH dimmed.</summary>
        void UpdateTags()
        {
            foreach (var b in _buttons)
            {
                if (b.Tag == null || !b.Rect.gameObject.activeSelf) continue;
                string t = b.TagText();
                string line = t == null ? "NOTHING IN REACH" : t.ToUpperInvariant();
                if (b.Tag.text != line) b.Tag.text = line;
                b.Tag.color = t == null ? TagDim : TagLive;
            }
        }

        HashSet<int> _seen = new(), _seenNow = new();
        readonly Dictionary<int, double> _quickTapped = new();

        /// <summary>
        /// A finger that went down AND up between two frames - a quick tap on a phone that is
        /// busy drawing. It is never seen active, so without this it pressed nothing at all. It
        /// presses for one frame instead: the button under it (its <see cref="Btn.Pulse"/>), or
        /// a free tap for the screens. Keyed on the touch's start time as well as its id, since
        /// the phase can read Ended for more than one frame and ids are reused.
        /// </summary>
        void QuickTap(TouchControl t, int id)
        {
            double start = t.startTime.ReadValue();
            if (_quickTapped.TryGetValue(id, out var was) && was == start) return;
            _quickTapped[id] = start;
            // An ordinary release - its owner already saw it down. Recorded all the same, so the
            // frame after, when its id has left _seen, does not take it for a quick tap.
            if (_seen.Contains(id)) return;

            var pos = t.position.ReadValue();
            var b = ButtonAt(pos);
            if (b != null) { b.Pulse = true; b.DownAt = b.At = pos; b.Aiming = false; }
            else Controls.FreeTaps.Add(pos);
        }

        /// <summary>
        /// The shown button a press at <paramref name="screenPoint"/> belongs to, or null: the
        /// NEAREST whose disc, grown by <see cref="HitSlop"/>, contains the point. A circle, not
        /// the button's square rect - the corners of the rect are not part of the button, and
        /// they were where a thumb aiming between two buttons landed.
        /// </summary>
        Btn ButtonAt(Vector2 screenPoint)
        {
            Btn best = null;
            float bestD = float.MaxValue;
            foreach (var b in _buttons)
            {
                if (!b.Rect.gameObject.activeInHierarchy) continue;
                b.Rect.GetWorldCorners(_corners);
                // Screen-space overlay: world corners ARE screen pixels.
                Vector2 centre = (_corners[0] + _corners[2]) * 0.5f;
                float halfH = (_corners[2].y - _corners[0].y) * 0.5f;           // screen pixels
                float pad = halfH * RingPad / (b.Rect.rect.height * 0.5f);      // RingPad is canvas units
                float reach = (halfH + pad) * HitSlop;
                // A disc is measured from its centre; a pill from the line through its middle,
                // which is a capsule - its round ends are exactly the drawn ends.
                float run = b.Pill ? (_corners[2].x - _corners[0].x) * 0.5f - halfH : 0f;
                var along = screenPoint - centre;
                along.x = Mathf.Sign(along.x) * Mathf.Max(0f, Mathf.Abs(along.x) - run);
                float d = along.magnitude;
                if (d <= reach && d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        static readonly Vector3[] _corners = new Vector3[4];

        /// <summary>
        /// A drag-aiming button's frame (see Btn.DragAims): it reports pressed exactly while the
        /// finger is down - holding is the hold to aim, lifting is the release that fires
        /// (PlayerController) - and past AimDragFraction, while an aimable art is pending, the drag
        /// is written as the aim.
        /// </summary>
        bool DragAim(Btn b, bool down)
        {
            if (down)
            {
                Vector2 drag = b.At - b.DownAt;
                float need = AimDragFraction * Screen.height;
                if (!b.Aiming && drag.sqrMagnitude > need * need && (Controls.ArtAimable?.Invoke() ?? false))
                    b.Aiming = true;
                if (b.Aiming && drag.sqrMagnitude > 1f) Controls.VirtualAim = drag.normalized;
                return true;
            }
            if (b.Aiming)
            {
                b.Aiming = false;
                Controls.VirtualAim = Vector2.zero;
            }
            return false;
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

        Btn _clicked;

        /// <summary>
        /// Off touch, the left mouse button presses whichever shown button it went down on, and
        /// holds it until it lets go - wherever the pointer has wandered, as a finger does. While
        /// it does, <see cref="Controls.MouseClaimed"/> keeps the same click from also being a
        /// swing or a press on what is underneath; that includes the frame it lets go, or the
        /// release would arrive at a screen as the end of a click it never saw begin. After
        /// <see cref="ReleaseAll"/>, which has just cleared every virtual input.
        /// </summary>
        void Click()
        {
            var mouse = Mouse.current;
            if (mouse == null || Controls.MouseIsGhost) { ReleaseClick(); return; }

            if (_clicked != null && !_clicked.Rect.gameObject.activeInHierarchy) _clicked = null;
            if (_clicked == null && mouse.leftButton.wasPressedThisFrame)
                _clicked = ButtonAt(mouse.position.ReadValue());
            if (_clicked == null) { Controls.MouseClaimed = false; return; }

            bool held = mouse.leftButton.isPressed;
            _clicked.Set(held);
            Controls.MouseClaimed = true;
            if (!held) _clicked = null;
        }

        void ReleaseClick()
        {
            _clicked = null;
            Controls.MouseClaimed = false;
        }

        void ReleaseAll()
        {
            DropStick();
            foreach (var b in _buttons) { b.Finger = -1; b.Set(false); Paint(b, false); }
        }

        /// <summary>
        /// Off touch: each shown button lit while its key is held (or it is clicked - the click is
        /// written into the same virtual input its key's union reads), labelled with the key for the
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
