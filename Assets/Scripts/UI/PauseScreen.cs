using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The mid-run pause menu, on the floor reward's interlocking rings (<see cref="SigilRings"/>,
    /// the sigil door selector's layout - the user's calls, 2026-10-12): SETTINGS up, CHARACTER
    /// left, INVENTORY right, LEAVE down in a red ring (suspend the run for later, or abandon it -
    /// GameBootstrap.AskLeaveRun), and RESUME a small ring on the knot in the middle. Resume is lit and focused first, so a pad opens on the safe answer and the
    /// D-pad reaches each ring by pointing at it; the destructive one sits at the bottom with its
    /// own confirmation behind it.
    ///
    /// It is the single "I want to stop and look" gesture for a live run. The loadout sheet and
    /// the carried-loot screen were each reached by their own key before, and abandoning was a
    /// bare Esc; routing all three through one paused menu means the destructive one sits beside
    /// the other two rather than on a keystroke by itself, and there is one thing to learn instead
    /// of three.
    ///
    /// IT DOES NOT NEST. Picking a ring CLOSES this menu and opens that screen; backing out of
    /// that screen calls straight back here and this menu is rebuilt fresh. Only ever one screen
    /// up at a time, which is the model every other modal in this project already keeps -
    /// GameBootstrap owns the wiring that returns here.
    /// </summary>
    public class PauseScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        const int Settings = 0, EndRun = 1, Character = 2, Inventory = 3, Resume = SigilRings.Centre;
        const float RingsY = -40f;

        /// <summary>The centre ring's radius in texels: over the knot, inside every ring's cell.</summary>
        const float CentreR = 20f;

        static readonly Color Veil = new(0.039f, 0.035f, 0.055f, 0.93f);
        static readonly Color GoldC = new(1f, 0.85f, 0.35f), Silver = new(0.90f, 0.91f, 0.94f);
        static readonly Color RedC = new(0.94f, 0.66f, 0.64f);
        static readonly Color Bright = new(0.95f, 0.92f, 0.85f), Prompt = new(0.60f, 0.63f, 0.70f);
        static readonly Color Faint = new(0.42f, 0.44f, 0.50f), BronzeLt = new(0.78f, 0.57f, 0.31f);

        GameObject _root;

        // Esc / the loadout key opens this, and wasPressedThisFrame stays true for the whole
        // frame - so without skipping the opening frame the menu would read its own opening
        // keystroke as a resume and vanish before it was ever drawn. Same guard as ConfirmDialog.
        int _openedFrame = -1;

        System.Action _onCharacter, _onInventory, _onEndRun, _onSettings;
        RectTransform _centre;
        Texture2D _ringTex;
        Color32[] _px;
        int _lit, _drawn = -2;
        List<RectTransform> _cells = new();
        List<RectTransform> _focus = new();

        public void Open(Transform canvas, System.Action onCharacter,
                         System.Action onInventory, System.Action onEndRun, System.Action onSettings)
        {
            if (IsOpen) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _onCharacter = onCharacter;
            _onInventory = onInventory;
            _onEndRun = onEndRun;
            _onSettings = onSettings;
            _lit = Resume;
            _drawn = -2;

            GamePause.Hold(this);
            Build(canvas);
            Draw();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_root) Destroy(_root);
            if (_ringTex) Destroy(_ringTex);
            _root = null;
            _ringTex = null;
            _cells?.Clear();
            _centre = null;
            _onCharacter = _onInventory = _onEndRun = _onSettings = null;
        }

        // ------------------------------------------------------------------ ui

        void Build(Transform canvas)
        {
            _root = new GameObject("PauseScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Veil);

            var top = new Vector2(0, 1);
            var one = new Vector2(1, 1);
            var tag = UiKit.Label(UiKit.Rect(full, "tag", top, one, new Vector2(0, -56), new Vector2(0, -30)),
                "T H E   R U N   W A I T S", 20, BronzeLt, TextAnchor.MiddleCenter);
            tag.fontStyle = FontStyle.Bold;
            var title = UiKit.Label(UiKit.Rect(full, "title", top, one, new Vector2(0, -128), new Vector2(0, -58)),
                "PAUSED", 60, Bright, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            var o = title.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            o.effectDistance = new Vector2(3f, -3f);
            UiKit.Label(UiKit.Rect(full, "prompt", top, one, new Vector2(0, -164), new Vector2(0, -132)),
                "Nothing moves until you come back.", 22, Prompt, TextAnchor.MiddleCenter);

            var rings = UiKit.Rect(full, "rings", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            rings.sizeDelta = new Vector2(SigilRings.Size, SigilRings.Size);
            rings.anchoredPosition = new Vector2(0, RingsY);
            _ringTex = SigilRings.Build(rings, "pause.rings");

            _cells.Clear();
            _cells.Add(Cell(rings, Settings, "OPTIONS", "Settings", CogIcon, Silver));
            _cells.Add(Cell(rings, EndRun, "SUSPEND OR END", "Leave Run", DoorIcon, RedC));
            _cells.Add(Cell(rings, Character, "LOADOUT", "Character", HelmIcon, Silver));
            _cells.Add(Cell(rings, Inventory, "CARRIED LOOT", "Inventory", BagIcon, Silver));

            // Resume: the small ring on the knot.
            float d = CentreR * 2f * SigilRings.U;
            _centre = UiKit.Rect(rings, "resume", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-d * 0.5f, -d * 0.5f), new Vector2(d * 0.5f, d * 0.5f));
            var play = Img(_centre, "icon", PlayIcon, GoldC);
            play.rectTransform.anchorMin = play.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            play.rectTransform.sizeDelta = new Vector2(24, 24);
            play.rectTransform.anchoredPosition = new Vector2(0, 26);
            var name = UiKit.Label(UiKit.Rect(_centre, "name", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, -18), new Vector2(0, 12)), "Resume", 24, Bright, TextAnchor.MiddleCenter);
            name.fontStyle = FontStyle.Bold;
            var esc = UiKit.Hint(UiKit.Rect(_centre, "key", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(0, -44), new Vector2(0, -20)), "[ESC]", "", 14, GoldC, TextAnchor.MiddleCenter,
                GamepadGlyphs.Cancel);
            esc.fontStyle = FontStyle.Bold;

            UiKit.Hint(UiKit.Rect(full, "h", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 14), new Vector2(0, 44)),
                "[ESC] RESUME    ·    [ 1 - 4 ] OR CLICK A RING",
                "TAP A RING  ·  THE CENTRE RESUMES",
                16, Faint, TextAnchor.MiddleCenter,
                GamepadGlyphs.Cancel + " RESUME    ·    D-PAD AND " + GamepadGlyphs.Confirm + " PICK A RING");
        }

        static RectTransform Cell(RectTransform rings, int i, string tag, string name, Sprite icon, Color colour)
        {
            var cell = SigilRings.Cell(rings, i);
            var top = new Vector2(0, 1);
            var one = new Vector2(1, 1);

            var t = UiKit.Label(UiKit.Rect(cell, "tag", top, one, new Vector2(0, -22), Vector2.zero),
                tag, 15, colour, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;

            var img = Img(cell, "icon", icon, colour);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            img.rectTransform.sizeDelta = new Vector2(48, 44);
            img.rectTransform.anchoredPosition = new Vector2(0, -52);

            var n = UiKit.Label(UiKit.Rect(cell, "name", top, one, new Vector2(-20, -118), new Vector2(20, -80)),
                name, 30, Bright, TextAnchor.MiddleCenter);
            n.fontStyle = FontStyle.Bold;

            var arrow = Img(cell, "pad", SigilRings.PadArrow, colour);
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            arrow.rectTransform.sizeDelta = new Vector2(15, 9);
            arrow.rectTransform.anchoredPosition = new Vector2(-12, 22);
            arrow.rectTransform.localEulerAngles = new Vector3(0, 0, SigilRings.ArrowTurn[i]);
            var key = UiKit.Label(UiKit.Rect(cell, "key", new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(2, 10), new Vector2(40, 36)), (i + 1).ToString(), 18, colour);
            key.fontStyle = FontStyle.Bold;
            return cell;
        }

        static Image Img(Transform parent, string name, Sprite sprite, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = c;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        void Draw()
        {
            if (_drawn == _lit) return;
            _drawn = _lit;
            SigilRings.Draw(_ringTex, ref _px, _lit,
                i => i == EndRun ? SigilRings.Red : SigilRings.Gold,
                i => i == EndRun ? SigilRings.RedLit : SigilRings.Lit, CentreR);
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;

            _focus.Clear();
            if (_centre != null) _focus.Add(_centre);   // first: a pad opens on Resume
            foreach (var c in _cells) _focus.Add(c);
            Core.Controls.SetFocusCandidates(_focus);

            // Hover and pad focus light a ring (touch has no hover: a tap picks outright - nothing
            // here is final, END RUN has its own confirmation).
            if (!Core.Controls.TouchMode)
            {
                int ring;
                if (Core.Controls.GamepadMode)
                {
                    var f = Core.Controls.Focused;
                    ring = f != null && f == _centre ? Resume : _cells.IndexOf(f);
                }
                else
                {
                    var at = Core.Controls.PointerPosition;
                    ring = Hit(_centre, at) ? Resume : _cells.FindIndex(c => Hit(c, at));
                }
                if (ring >= 0) _lit = ring;
            }

            if (Time.frameCount != _openedFrame)
            {
                if (Core.Controls.CancelTapped || Core.Controls.MenuTapped) { Close(); return; }
                for (int i = 0; i < _cells.Count; i++)
                    if (Core.Controls.DigitTapped(i)) { Pick(i); return; }
                if (Core.Controls.ConfirmTapped) { Pick(_lit); return; }
                if (Core.Controls.Tapped(out var p))
                {
                    if (Hit(_centre, p)) { Pick(Resume); return; }
                    for (int i = 0; i < _cells.Count; i++)
                        if (Hit(_cells[i], p)) { Pick(i); return; }
                }
            }

            Draw();
        }

        void Pick(int ring)
        {
            switch (ring)
            {
                case Resume:    Close(); break;
                case Settings:  { var cb = _onSettings; cb?.Invoke(); break; }
                case Character: { var cb = _onCharacter; cb?.Invoke(); break; }
                case Inventory: { var cb = _onInventory; cb?.Invoke(); break; }
                case EndRun:    { var cb = _onEndRun; cb?.Invoke(); break; }
            }
        }

        static bool Hit(RectTransform rt, Vector2 point)
            => rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, point, null);

        // ------------------------------------------------------------------ icons (tinted per ring)

        static Sprite _play, _helm, _bag, _door, _cog;

        static Sprite CogIcon => _cog != null ? _cog : _cog = SigilRings.Grid(new[]
        {
            "....XXX....",
            ".X.XXXXX.X.",
            "..XXXXXXX..",
            ".XXXX.XXXX.",
            "XXXX...XXXX",
            "XXX.....XXX",
            "XXXX...XXXX",
            ".XXXX.XXXX.",
            "..XXXXXXX..",
            ".X.XXXXX.X.",
            "....XXX....",
        }, SigilRings.White);

        static Sprite PlayIcon => _play != null ? _play : _play = SigilRings.Grid(new[]
        {
            "X......",
            "XXX....",
            "XXXXX..",
            "XXXXXXX",
            "XXXXX..",
            "XXX....",
            "X......",
        }, SigilRings.White);

        static Sprite HelmIcon => _helm != null ? _helm : _helm = SigilRings.Grid(new[]
        {
            "..XXXXX..",
            ".XXXXXXX.",
            ".XX.X.XX.",
            ".XXXXXXX.",
            "..XXXXX..",
            "...XXX...",
            ".XXXXXXX.",
            "XXXXXXXXX",
            "XXXXXXXXX",
        }, SigilRings.White);

        static Sprite BagIcon => _bag != null ? _bag : _bag = SigilRings.Grid(new[]
        {
            "...XXXX...",
            "..X....X..",
            "...XXXX...",
            ".XXXXXXXX.",
            "XXXXXXXXXX",
            "XXXX..XXXX",
            "XXXXXXXXXX",
            "XXXXXXXXXX",
            ".XXXXXXXX.",
        }, SigilRings.White);

        static Sprite DoorIcon => _door != null ? _door : _door = SigilRings.Grid(new[]
        {
            ".XXXXXX.",
            "XXXXXXXX",
            "XX....XX",
            "XX....XX",
            "XX....XX",
            "XX..X.XX",
            "XX....XX",
            "XX....XX",
            "XXXXXXXX",
        }, SigilRings.White);
    }
}
