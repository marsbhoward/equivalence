using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The question the gate asks before a run: put one equipped piece on the line, or go in with
    /// nothing staked. See <see cref="GearStake"/> for the rules this only shows.
    ///
    /// THE SCREEN IS THE SIGIL DOOR (the user's sketch, mock "Stake D", approved 2026-11-02:
    /// https://claude.ai/artifact/3EDpCosD7j8ok5HmRbkxup). The hub's own door, seen live through a
    /// camera of the hub's tilt, in plain stone with no mark lit, swinging open like French doors
    /// as the screen opens (SigilDoor.Stage). Only its LEFT half is filmed; the right half is the
    /// same picture mirrored, which keeps the room's element selector off the screen and the two
    /// bays either side alike. RISK stands in the left bay, REWARD in the right, and between the
    /// parted doors a VOID pulls white motes into itself - and is also where a Rift Box goes to
    /// insure the stake. Gold dashes run piece - void - piece. THE VOID IS THE LIGHT (the user's
    /// pick E1, 2026-11-03, mock https://claude.ai/artifact/6mSUyc4brwWBCrJ2cVVExe): a white core
    /// in a pale doorway, its light spilling out onto the open leaves.
    ///
    /// Opened only when the player HAS something stakeable, and never on a character's first run
    /// (GameBootstrap.EnterGate). EVERY TERM IS STATED before the choice: the floor, the payout,
    /// that a death loses the piece. Dismissible: backing out stays in the hub and starts nothing.
    /// </summary>
    public class StakeScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        GameObject _root;
        int _openedFrame = -1;
        Action<MintedGearRecord, bool> _onChoose;
        Action _onCancel;
        IReadOnlyList<MintedGearRecord> _candidates;
        MintedGearRecord _picked;
        bool _insured;
        int _riftBoxes;

        Hub.HubRoom _hub;
        Hub.SigilDoor _door;
        Camera _cam;
        RenderTexture _rt;

        // Not readonly, and rebuilt if found null - see the domain reload rules.
        List<(RectTransform Rect, Action OnClick)> _hits = new();
        List<RectTransform> _focusRects = new();
        List<(Image Fill, Image Edge, MintedGearRecord Item)> _pickCells = new();
        List<(RectTransform Rect, Image Img, float Angle, float Radius, float Period, float Phase)> _moteList = new();
        CanvasGroup _doorway, _spill;
        Image _halo, _voidRim;
        Image _riskArt, _rewardArt;
        Text _riskName, _riskStars, _rewardMark, _gateLine, _voidLine, _boxLine;
        RectTransform _boxIcon;
        Canvas _canvas;

        static readonly Vector2 Centre = new(0.5f, 0.5f);
        static readonly Color Gold = new(0.85f, 0.71f, 0.35f, 0.95f);
        static readonly Color GoldBright = new(0.94f, 0.84f, 0.54f, 1f);
        static readonly Color GoldDim = new(0.85f, 0.71f, 0.35f, 0.45f);
        static readonly Color Glass = new(0.07f, 0.06f, 0.09f, 0.96f);
        static readonly Color GlassLit = new(0.36f, 0.27f, 0.09f, 0.96f);
        static readonly Color Ink = new(0.93f, 0.90f, 0.84f);
        static readonly Color InkOnGold = new(0.10f, 0.08f, 0.03f);
        static readonly Color Muted = new(0.65f, 0.63f, 0.71f);
        static readonly Color RiskInk = new(0.94f, 0.54f, 0.48f);
        static readonly Color RewardInk = new(0.56f, 0.82f, 0.63f);
        static readonly Color BoxCyan = new(0.50f, 0.84f, 0.91f);
        static readonly Color Bay = new(0.075f, 0.067f, 0.086f, 1f);
        /// <summary>The lit doorway around the void, and the words on it.</summary>
        static readonly Color Pale = new(0.80f, 0.80f, 0.84f, 1f);
        static readonly Color InkOnPale = new(0.10f, 0.09f, 0.12f);
        static readonly Color MutedOnPale = new(0.22f, 0.21f, 0.27f);
        static readonly Color InsuredOnPale = new(0.10f, 0.40f, 0.47f);
        static readonly Color RimOnPale = new(0.20f, 0.62f, 0.72f);

        // ---- the door's picture, in the canvas's 1920x1080 units ----
        // The mock's composition: the camera films 1.357 world units either side of the door's
        // middle (the left half filmed, the right mirrored), 2.0 tall.
        const float ViewW = 1344f, ViewH = 990f, ViewTop = 480f;
        const float HalfWorld = 1.357f, OrthoHalf = 1.0f;
        static float PerUnit => ViewW / (HalfWorld * 2f);
        /// <summary>The clear doorway between the swung leaves, either side of the middle, in world units:
        /// where the open leaves' free edges stand, less their slabs' edges (Hub.DoorSlabs).</summary>
        static float ClearHalf
        {
            get
            {
                float t = Tuning.Hub.DoorSwingDegrees * Mathf.Deg2Rad;
                return Tuning.Hub.DoorHalfWidth * (1f - Mathf.Cos(t)) - Tuning.Hub.DoorSlabThickness * Mathf.Sin(t);
            }
        }
        // The bays either side of the door, as the hub's wall draws them (from the approved capture).
        const float BayX0 = 53.8f, BayX1 = 281.5f, BayTop = 121f, BayBottom = 866.5f;
        const float Slot = 174f, VoidSize = 165f;

        /// <summary>The first piece that can be staked, on screen - for the stake's first hint.</summary>
        public Vector2? HintAnchor => _hits != null && _hits.Count > 0 && _hits[0].Rect != null ? (Vector2)_hits[0].Rect.position : (Vector2?)null;

        public void Open(Transform canvas, IReadOnlyList<MintedGearRecord> candidates, int riftBoxes,
                         Hub.HubRoom hub, Action<MintedGearRecord, bool> onChoose, Action onCancel)
        {
            if (IsOpen || candidates == null || candidates.Count == 0) return;
            IsOpen = true;
            _candidates = candidates;
            _picked = candidates[0];
            _riftBoxes = riftBoxes;
            _insured = false;
            _openedFrame = Time.frameCount;
            _onChoose = onChoose;
            _onCancel = onCancel;
            _hub = hub;
            _door = hub != null ? hub.Door : null;
            GamePause.Hold(this);

            _door?.Stage(true);
            _hub?.HideAvatar(true);
            Build(canvas);
            StartCamera();
            Paint();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_door != null) _door.Stage(false);
            if (_hub != null) _hub.HideAvatar(false);
            if (_cam != null) Destroy(_cam.gameObject);
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
            _cam = null; _rt = null;
            if (_root) Destroy(_root);
            _root = null;
            _hits?.Clear();
            _pickCells?.Clear();
            _moteList?.Clear();
            _onChoose = null;
            _onCancel = null;
        }

        // ------------------------------------------------------------------ the camera

        /// <summary>A camera like the hub's - its tilt, its renderer - filming the door's LEFT half.</summary>
        void StartCamera()
        {
            var main = Camera.main;
            if (_door == null || main == null) return;
            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            int w = Mathf.Max(2, Mathf.RoundToInt(ViewW * 0.5f * scale));
            int h = Mathf.Max(2, Mathf.RoundToInt(ViewH * scale));
            _rt = new RenderTexture(w, h, 24) { filterMode = FilterMode.Point, name = "stake-door" };

            var go = new GameObject("stake-door-camera");
            _cam = go.AddComponent<Camera>();
            _cam.CopyFrom(main);
            _cam.transform.rotation = main.transform.rotation;
            var centre = _door.Centre + new Vector3(-HalfWorld * 0.5f, 0f, 0f);
            _cam.transform.position = centre - _cam.transform.forward * 10f;
            _cam.orthographic = true;
            _cam.orthographicSize = OrthoHalf;
            _cam.targetTexture = _rt;
            _cam.aspect = HalfWorld / (OrthoHalf * 2f);
            _cam.depth = main.depth - 1;
            foreach (var img in _root.GetComponentsInChildren<RawImage>()) img.texture = _rt;
        }

        // ------------------------------------------------------------------ build

        void Build(Transform canvas)
        {
            _hits ??= new(); _pickCells ??= new(); _moteList ??= new(); _focusRects ??= new();
            _hits.Clear(); _pickCells.Clear(); _moteList.Clear();
            _root = new GameObject("StakeScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;
            _canvas = full.GetComponentInParent<Canvas>();

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.03f, 0.03f, 0.05f, 0.98f));
            UiKit.Label(UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -56), new Vector2(0, -8)),
                "THE STAKE", 30, Gold, TextAnchor.MiddleCenter);

            // ---- the door: the left half filmed, the right the same picture mirrored ----
            var view = UiKit.Rect(full, "door", Centre, Centre,
                new Vector2(-ViewW * 0.5f, ViewTop - ViewH), new Vector2(ViewW * 0.5f, ViewTop));
            Picture(view, "left", 0f, 0.5f, new Rect(0f, 0f, 1f, 1f));
            Picture(view, "right", 0.5f, 1f, new Rect(1f, 0f, -1f, 1f));

            BuildDoorway(view);
            BuildBay(view, risk: true);
            BuildBay(view, risk: false);
            Dashes(view, BayX1 - 22f, ViewW * 0.5f - VoidSize * 0.5f - 10f);
            Dashes(view, ViewW * 0.5f + VoidSize * 0.5f + 10f, ViewW - BayX1 + 22f);

            // ---- the pieces, on the left ----
            float px = -ViewW * 0.5f - 270f;
            UiKit.Label(UiKit.Rect(full, "ph", Centre, Centre, new Vector2(px, 340f), new Vector2(px + 250f, 380f)),
                "CHOOSE WHAT TO RISK", 16, Muted, TextAnchor.MiddleLeft);
            for (int i = 0; i < _candidates.Count; i++)
            {
                float x = px + (i % 2) * 128f, y = 330f - (i / 2) * 128f;
                var edge = UiKit.Panel(full, Centre, Centre, new Vector2(x, y - 116f), new Vector2(x + 116f, y), GoldDim);
                var fill = UiKit.Panel(edge, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2), Glass).GetComponent<Image>();
                PieceArt(fill.rectTransform, _candidates[i], 12f);
                var item = _candidates[i];
                _pickCells.Add((fill, edge.GetComponent<Image>(), item));
                _hits.Add((edge, () => { _picked = item; Paint(); }));
            }
            float below = 330f - ((_candidates.Count + 1) / 2) * 128f - 10f;
            var note = UiKit.Label(UiKit.Rect(full, "pn", Centre, Centre, new Vector2(px, below - 110f), new Vector2(px + 250f, below)),
                "Only what you wear." + (RunFormat.Showcase ? " Gear stops at Bronze 3 stars in the showcase." : ""),
                15, new Color(0.47f, 0.45f, 0.54f), TextAnchor.UpperLeft);
            note.horizontalOverflow = HorizontalWrapMode.Wrap;

            // ---- the answers, on the right ----
            float bx = ViewW * 0.5f + 20f;
            var stake = UiKit.Panel(full, Centre, Centre, new Vector2(bx, 196f), new Vector2(bx + 250f, 340f), GoldBright);
            UiKit.Panel(stake, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3), Gold);
            UiKit.Label(UiKit.Rect(stake, "l", Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0)),
                "STAKE IT\nAND ENTER", 24, InkOnGold, TextAnchor.MiddleCenter);
            _hits.Add((stake, () => Finish(_picked)));

            var none = UiKit.Panel(full, Centre, Centre, new Vector2(bx, 64f), new Vector2(bx + 250f, 180f), Gold);
            UiKit.Panel(none, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2), Glass);
            UiKit.Label(UiKit.Rect(none, "l", Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0)),
                "ENTER WITH\nNOTHING STAKED", 19, Gold, TextAnchor.MiddleCenter);
            _hits.Add((none, () => Finish(null)));

            var boxRow = UiKit.Rect(full, "boxes", Centre, Centre, new Vector2(bx, -40f), new Vector2(bx + 250f, 30f));
            BoxIcon.Add(boxRow, Convergence.Art.BoxArt.Kind.Rift, false, new Vector2(0f, 0.5f), new Vector2(28f, -22f));
            _boxLine = UiKit.Label(UiKit.Rect(boxRow, "l", Vector2.zero, Vector2.one, new Vector2(74, 0), Vector2.zero),
                "", 16, Muted, TextAnchor.MiddleLeft);
            _boxLine.horizontalOverflow = HorizontalWrapMode.Wrap;

            UiKit.Hint(UiKit.Rect(full, "h", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 4), new Vector2(0, 34)),
                "[ ESC ] stay in the hub", "BACK to stay in the hub", 17, new Color(0.45f, 0.47f, 0.54f));
        }

        static void Picture(RectTransform view, string name, float x0, float x1, Rect uv)
        {
            var go = new GameObject(name, typeof(RawImage));
            go.transform.SetParent(view, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(x0, 0f); rt.anchorMax = new Vector2(x1, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<RawImage>();
            img.uvRect = uv;
            img.raycastTarget = false;
        }

        /// <summary>
        /// The doorway between the parted leaves: the light, the void and its motes, the words.
        /// Masked to the clear opening, and faded in only as the doors finish opening - drawn over
        /// the film, it would otherwise sit on the shut doors. The light it throws onto the open
        /// leaves (the spill) lies outside the mask, under it, and fades in with it.
        /// </summary>
        void BuildDoorway(RectTransform view)
        {
            float halfW = ClearHalf * PerUnit;
            float halfH = Tuning.Hub.DoorHeight * 0.5f * Mathf.Cos(Tuning.Hub.CameraTiltDegrees * Mathf.Deg2Rad) * PerUnit;

            var spill = UiKit.Rect(view, "spill", Centre, Centre, Vector2.zero, Vector2.zero);
            _spill = spill.gameObject.AddComponent<CanvasGroup>();
            _spill.alpha = 0f;
            _spill.blocksRaycasts = false;
            Soft(spill, Vector2.zero, new Vector2(900f, 1150f), new Color(1f, 1f, 1f, 0.35f));
            Soft(spill, Vector2.zero, new Vector2(620f, 1000f), new Color(1f, 1f, 1f, 0.35f));

            var way = UiKit.Panel(view, Centre, Centre, new Vector2(-halfW, -halfH), new Vector2(halfW, halfH), Pale);
            way.gameObject.AddComponent<RectMask2D>();
            _doorway = way.gameObject.AddComponent<CanvasGroup>();
            _doorway.alpha = 0f;
            _doorway.blocksRaycasts = false;

            // The void's light: a wide bloom and a bright middle, under the motes.
            Soft(way, Vector2.zero, 560f, Color.white);
            Soft(way, Vector2.zero, 308f, Color.white);
            Soft(way, Vector2.zero, 240f, Color.white);

            // Dark on the light: no outline (UiKit.Legible's dark edge smudges dark type).
            _gateLine = UiKit.Label(UiKit.Rect(way, "gate", new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -150), new Vector2(-14, -40)),
                "", 18, InkOnPale, TextAnchor.UpperCenter);
            _gateLine.horizontalOverflow = HorizontalWrapMode.Wrap;

            var motes = UiKit.Rect(way, "motes", Centre, Centre, Vector2.zero, Vector2.zero);
            for (int i = 0; i < 40; i++)
            {
                float size = 4f + ((i * 7) % 4) * 3f;
                var m = UiKit.Panel(motes, Centre, Centre, new Vector2(-size * 0.5f, -size * 0.5f), new Vector2(size * 0.5f, size * 0.5f), Color.white);
                // Fixed by index - hashed, never UnityEngine.Random (an effect's look).
                _moteList.Add((m, m.GetComponent<Image>(), (i * 137.5f) % 360f, 135f + (i * 29) % 75,
                               2.4f + ((i * 13) % 9) * 0.25f, (i * 0.37f) % 3f));
            }

            _halo = Soft(way, Vector2.zero, VoidSize * 1.9f, new Color(1f, 1f, 1f, 0.6f));
            _voidRim = UiKit.Panel(way, Centre, Centre, new Vector2(-VoidSize * 0.5f - 5f, -VoidSize * 0.5f - 5f),
                new Vector2(VoidSize * 0.5f + 5f, VoidSize * 0.5f + 5f), Color.clear).GetComponent<Image>();
            _voidRim.sprite = Spr.Circle;
            var v = UiKit.Panel(way, Centre, Centre, new Vector2(-VoidSize * 0.5f, -VoidSize * 0.5f),
                new Vector2(VoidSize * 0.5f, VoidSize * 0.5f), Color.white);
            v.GetComponent<Image>().sprite = Spr.Circle;
            _hits.Add((v, ToggleInsurance));
            _boxIcon = UiKit.Rect(v, "box", Centre, Centre, Vector2.zero, Vector2.zero);
            BoxIcon.Add(_boxIcon, Convergence.Art.BoxArt.Kind.Rift, false, Centre, new Vector2(0f, -26f));

            _voidLine = UiKit.Label(UiKit.Rect(way, "void", new Vector2(0, 0), new Vector2(1, 0), new Vector2(14, 60), new Vector2(-14, 190)),
                "", 16, MutedOnPale, TextAnchor.UpperCenter);
            _voidLine.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        static Image Soft(RectTransform parent, Vector2 at, float size, Color c) => Soft(parent, at, Vector2.one * size, c);

        static Image Soft(RectTransform parent, Vector2 at, Vector2 size, Color c)
        {
            var img = UiKit.Panel(parent, Centre, Centre, at - size * 0.5f, at + size * 0.5f, c).GetComponent<Image>();
            img.sprite = Spr.Glow;   // a soft pool, falling off to nothing
            return img;
        }

        /// <summary>A bay beside the door - the wall's own dark recess, drawn over the film so it
        /// stays one whatever stands in the room - holding one side of the wager.</summary>
        void BuildBay(RectTransform view, bool risk)
        {
            float x0 = risk ? BayX0 : ViewW - BayX1, x1 = risk ? BayX1 : ViewW - BayX0;
            var bay = UiKit.Panel(view, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x0, -BayBottom), new Vector2(x1, -BayTop), Bay);
            UiKit.Label(UiKit.Rect(bay, "t", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -90), new Vector2(0, -30)),
                risk ? "RISK" : "REWARD", 28, risk ? RiskInk : RewardInk, TextAnchor.MiddleCenter);

            float mid = (BayBottom - BayTop) * 0.5f;
            var slot = UiKit.Panel(bay, new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                new Vector2(-Slot * 0.5f, -mid - Slot * 0.5f), new Vector2(Slot * 0.5f, -mid + Slot * 0.5f), risk ? RiskInk : RewardInk);
            var well = UiKit.Panel(slot, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3), new Color(0.055f, 0.047f, 0.07f, 1f));
            var art = UiKit.Rect(well, "art", Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -16));
            var img = art.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            if (risk) _riskArt = img; else _rewardArt = img;

            float below = -mid - Slot * 0.5f;
            if (risk)
            {
                _riskName = UiKit.Label(UiKit.Rect(bay, "n", new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, below - 64), new Vector2(-10, below - 10)),
                    "", 16, Ink, TextAnchor.MiddleCenter);
                _riskName.horizontalOverflow = HorizontalWrapMode.Wrap;
                _riskStars = UiKit.Label(UiKit.Rect(bay, "s", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, below - 96), new Vector2(0, below - 64)),
                    "", 22, new Color(1f, 0.84f, 0.40f), TextAnchor.MiddleCenter);
                _riskStars.supportRichText = true;
                UiKit.Label(UiKit.Rect(bay, "d", new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, below - 150), new Vector2(-10, below - 104)),
                    "Die, and it is lost.", 17, new Color(0.79f, 0.64f, 0.63f), TextAnchor.MiddleCenter);
            }
            else
            {
                _rewardMark = UiKit.Label(UiKit.Rect(slot, "m", new Vector2(1, 1), new Vector2(1, 1), new Vector2(-90, -34), new Vector2(-6, -4)),
                    "", 20, new Color(1f, 0.84f, 0.40f), TextAnchor.MiddleRight);
                UiKit.Label(UiKit.Rect(bay, "n", new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, below - 64), new Vector2(-10, below - 10)),
                    "A MATCHING PIECE", 16, Ink, TextAnchor.MiddleCenter);
                UiKit.Label(UiKit.Rect(bay, "d", new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, below - 150), new Vector2(-10, below - 70)),
                    "Combine the two\nat the Forge.", 17, new Color(0.65f, 0.79f, 0.68f), TextAnchor.MiddleCenter);
            }
        }

        /// <summary>The gold dashes from a piece to the void, on the slots' line.</summary>
        static void Dashes(RectTransform view, float from, float to)
        {
            for (float x = from; x + 14f <= to; x += 24f)
                UiKit.Panel(view, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x, -2f), new Vector2(x + 14f, 2f), Gold);
        }

        /// <summary>A piece's own art in a cell, as the circle and the gear picker draw it.</summary>
        static void PieceArt(RectTransform parent, MintedGearRecord r, float inset)
        {
            var item = Convergence.Art.Gear.GearCatalog.Get(r.InstanceId);
            var layer = item != null ? Hub.GearDisplay.Represent(item) : null;
            if (layer?.Sprite == null)
            {
                UiKit.Label(UiKit.Rect(parent, "n", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                    Convergence.Art.Gear.GearSlots.Label(r.Slot).ToUpper(), 13, Muted, TextAnchor.MiddleCenter);
                return;
            }
            var art = UiKit.Rect(parent, "art", Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));
            var img = art.gameObject.AddComponent<Image>();
            img.sprite = layer.Sprite;
            img.color = layer.Tint;
            img.preserveAspect = true;
            img.raycastTarget = false;
            KindledImage.On(img, item);
        }

        // ------------------------------------------------------------------ paint

        void Paint()
        {
            foreach (var (fill, edge, item) in _pickCells)
            {
                bool on = item == _picked;
                fill.color = on ? GlassLit : Glass;
                edge.color = on ? GoldBright : GoldDim;
            }

            var art = Convergence.Art.Gear.GearCatalog.Get(_picked.InstanceId);
            var layer = art != null ? Hub.GearDisplay.Represent(art) : null;
            foreach (var img in new[] { _riskArt, _rewardArt })
            {
                img.sprite = layer?.Sprite;
                img.enabled = layer?.Sprite != null;
                img.color = layer?.Tint ?? Color.white;
            }
            _riskName.text = (art?.DisplayName ?? _picked.DisplayName).ToUpperInvariant();
            int max = Convergence.Art.Gear.GearRoller.MaxLevel;
            int lit = Mathf.Clamp(_picked.UpgradeLevel, 0, max);
            _riskStars.text = new string('★', lit) + "<color=#3a332c>" + new string('★', max - lit) + "</color>";

            bool promotes = _picked.UpgradeLevel >= max;
            _rewardMark.text = promotes ? "TIER UP" : "+1★";
            _gateLine.text = $"Extract after floor {GearStake.GateFloor(_picked.UpgradeLevel)} and it is " +
                             (promotes ? "promoted." : "upgraded.");

            bool can = _riftBoxes >= Tuning.GearRoll.StakeInsuranceRiftBoxCost;
            _voidLine.text = _insured ? "Insured: a death brings it home."
                           : can ? "Tap the void to insure it with a Rift Box."
                           : "No Rift Box to insure it with.";
            _voidLine.color = _insured ? InsuredOnPale : MutedOnPale;
            _voidRim.color = _insured ? RimOnPale : Color.clear;
            _boxIcon.gameObject.SetActive(_insured);
            _boxLine.text = _insured ? $"1 of your {_riftBoxes} Rift Boxes in the void"
                          : _riftBoxes == 1 ? "You have 1 Rift Box" : $"You have {_riftBoxes} Rift Boxes";
        }

        void ToggleInsurance()
        {
            if (!_insured && _riftBoxes < Tuning.GearRoll.StakeInsuranceRiftBoxCost) return;
            _insured = !_insured;
            Paint();
        }

        // ------------------------------------------------------------------ tick

        void Update()
        {
            if (!IsOpen) return;

            // The doorway's picture shows only once the leaves are out of its way.
            float open = _door != null ? _door.OpenEased : 1f;
            if (_doorway != null) _doorway.alpha = Mathf.InverseLerp(0.75f, 1f, open);
            if (_spill != null) _spill.alpha = _doorway != null ? _doorway.alpha : 1f;
            AnimateVoid();
            if (FirstRunHints.Blocking) return;   // the stake hint over the door: its press is not a pick

            _focusRects ??= new();
            _focusRects.Clear();
            foreach (var (rect, _) in _hits) _focusRects.Add(rect);
            Controls.SetFocusCandidates(_focusRects);

            // The gate is used with the same press that could read as a tap on this frame.
            if (Time.frameCount == _openedFrame) return;

            if (Controls.CancelTapped)
            {
                var back = _onCancel;
                Close();
                back?.Invoke();
                return;
            }

            if (!Controls.Tapped(out var at)) return;
            foreach (var (rect, onClick) in _hits)
            {
                if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                onClick?.Invoke();
                return;
            }
        }

        /// <summary>The motes spiral in and shrink as they are drawn to the middle; the halo breathes.
        /// On unscaled time - the screen holds the pause.</summary>
        void AnimateVoid()
        {
            if (_moteList == null) return;
            float now = Time.unscaledTime;
            foreach (var (rect, img, angle, radius, period, phase) in _moteList)
            {
                float k = Mathf.Repeat((now + phase) / period, 1f);
                float e = k * k * (1.6f - 0.6f * k);   // slow at the rim, quickening inward
                float a = (angle + 200f * e) * Mathf.Deg2Rad;
                rect.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * (1f - e);
                rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.15f, e);
                var c = img.color; c.a = Mathf.Clamp01(k / 0.12f); img.color = c;
            }
            if (_halo != null)
            {
                float breath = 0.5f + 0.5f * Mathf.Sin(now * Mathf.PI * 2f / 3.2f);
                _halo.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.55f, 0.7f, breath));
                _halo.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, breath);
            }
        }

        void Finish(MintedGearRecord chosen)
        {
            var go = _onChoose;
            bool insured = chosen != null && _insured;
            Close();
            go?.Invoke(chosen, insured);
        }
    }
}
