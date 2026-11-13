using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The portrait booth (the user's call, 2026-11-02): every player - guest or wallet - stands
    /// the character in a pose and SAVES A PICTURE of it (PictureSaver). The poses are the open ones
    /// everyone has (Tuning.Cosmetics.OpenPoses) and any the character earned. A wallet that can
    /// write also takes or updates its portrait NFT here, in the pose chosen - not on the showcase,
    /// whose instance never signs (SHOWCASE.md).
    ///
    /// THE STUDIO LAYOUT (the user's pick, mock "Booth A", 2026-11-02): the figure framed on the
    /// left with BODY / BUST / FACE on its frame; on the right a row of pose cards, each showing the
    /// character in that pose (rendered once on open by a second, small preview - see
    /// <see cref="RenderThumbnails"/>), the gold SAVE A PICTURE, and the portrait NFT's strip.
    /// The figure is the character preview the circle uses, zoomed and panned the same way, so the
    /// picture is exactly what is framed.
    /// </summary>
    public class BoothScreen : MonoBehaviour
    {
        public bool IsOpen { get; private set; }

        Func<CharacterProfile> _profile;
        /// <summary>Takes or updates the portrait in the pose given ("" standing) - GameBootstrap's
        /// confirm-and-sign flow. Called after the booth closes.</summary>
        Action<string> _onPortrait;
        Action _onConnect;

        public void Init(Func<CharacterProfile> profile, Action<string> onPortrait, Action onConnect)
        {
            _profile = profile;
            _onPortrait = onPortrait;
            _onConnect = onConnect;
        }

        readonly CharacterPreview _preview = new();
        /// <summary>The pose cards' painter: a small second preview, shifted off the main one's spot.</summary>
        readonly CharacterPreview _thumbs = new();
        GameObject _root;
        RectTransform _full, _previewRect;
        Canvas _canvas;
        int _openedFrame;
        ElementType _element;

        // Not readonly, and rebuilt if found null - see the domain reload rules.
        List<(RectTransform Rect, Action OnClick)> _hits = new();
        List<RectTransform> _focusRects = new();
        List<PoseCard> _poseCards = new();
        List<(Image Fill, Text Label, CharacterPreview.View View)> _viewButtons = new();

        class PoseCard
        {
            public string Pose;
            public Image Fill, Edge;
            public Text Label;
            public RawImage Picture;
            public RenderTexture Texture;
        }

        string _pose = "";
        Text _note, _portraitLabel, _portraitLine;
        /// <summary>The portrait's state, read once on open: null while asking (or with no wallet).</summary>
        Chain.Web.ChainPfp.Status _status;
        bool _statusAsked;
        Coroutine _thumbWork;

        static readonly Vector2 Centre = new(0.5f, 0.5f);
        static readonly Color Gold = new(0.85f, 0.71f, 0.35f, 0.95f);
        static readonly Color GoldBright = new(0.94f, 0.84f, 0.54f, 1f);
        static readonly Color GoldDim = new(0.85f, 0.71f, 0.35f, 0.35f);
        static readonly Color Glass = new(0.07f, 0.06f, 0.09f, 0.96f);
        static readonly Color GlassLit = new(0.36f, 0.27f, 0.09f, 0.96f);
        static readonly Color Ink = new(0.93f, 0.90f, 0.84f);
        static readonly Color InkOnGold = new(0.10f, 0.08f, 0.03f);
        static readonly Color LitInk = new(0.97f, 0.89f, 0.66f);
        static readonly Color Muted = new(0.65f, 0.63f, 0.71f);

        // The mock's sizes at the canvas's 1920x1080.
        const float PreviewW = 600f, PreviewH = 800f, Gap = 60f, ColumnW = 860f;
        /// <summary>A pose card's picture, in pixels - small: it is one figure in a thumbnail.</summary>
        const int ThumbW = 150, ThumbH = 190;

        /// <summary>A wallet portrait is on offer here: the page reaches a wallet, and the instance
        /// writes (the showcase's never does).</summary>
        static bool PortraitsHere => StoreFactory.UsingChain && !RunFormat.Showcase;

        public void Open(Transform canvas, ElementType element)
        {
            if (IsOpen || _profile == null || _profile() == null) return;
            IsOpen = true;
            _openedFrame = Time.frameCount;
            _element = element;
            GamePause.Hold(this);
            _hits ??= new(); _focusRects ??= new(); _poseCards ??= new(); _viewButtons ??= new();
            _hits.Clear(); _poseCards.Clear(); _viewButtons.Clear();
            _status = null;
            _statusAsked = false;

            // Opens on the pose the portrait was last taken in, if the character may still stand in it.
            var p = _profile();
            _pose = Tuning.Cosmetics.MayPose(p, p.Pose) ? p.Pose : "";

            _preview.Build(element, (int)PreviewW, (int)PreviewH);
            Build(canvas);
            _preview.Refresh(p);
            _preview.Rig?.HoldPose(PoseCatalog.Get(_pose));
            _refitFrame = Time.frameCount + RefitFrames;
            _thumbWork = StartCoroutine(RenderThumbnails());
            AskStatus();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            GamePause.Release(this);
            if (_thumbWork != null) StopCoroutine(_thumbWork);
            _thumbWork = null;
            _thumbs.Dispose();
            _preview.Rig?.HoldPose(null);
            _preview.Dispose();
            foreach (var card in _poseCards)
                if (card.Texture != null) { card.Texture.Release(); Destroy(card.Texture); }
            CloseReplace();
            if (_root) Destroy(_root);
            _root = null;
            _hits.Clear();
            _poseCards.Clear();
            _viewButtons.Clear();
        }

        // ------------------------------------------------------------------ build

        void Build(Transform canvas)
        {
            _root = new GameObject("BoothScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            _full = (RectTransform)_root.transform;
            _full.anchorMin = Vector2.zero; _full.anchorMax = Vector2.one;
            _full.offsetMin = Vector2.zero; _full.offsetMax = Vector2.zero;
            _canvas = _full.GetComponentInParent<Canvas>();

            UiKit.Panel(_full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.03f, 0.03f, 0.05f, 0.97f));
            UiKit.Label(UiKit.Rect(_full, "t", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -96), new Vector2(0, -40)),
                "PORTRAIT BOOTH", 42, Gold, TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(_full, "s", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -128), new Vector2(0, -96)),
                "choose a pose, frame it, keep the picture", 20, Muted, TextAnchor.MiddleCenter);

            float left = -(PreviewW + Gap + ColumnW) * 0.5f;
            float top = 330f;

            // ---- the figure, on the left, with its three framings ----
            var frame = Framed(new Vector2(left, top - PreviewH), new Vector2(left + PreviewW, top), out _);
            var imgGo = new GameObject("preview", typeof(RawImage));
            imgGo.transform.SetParent(frame, false);
            var irt = (RectTransform)imgGo.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(10, 10); irt.offsetMax = new Vector2(-10, -10);
            imgGo.GetComponent<RawImage>().raycastTarget = false;
            _previewRect = irt;
            _preview.MatchTo(imgGo.GetComponent<RawImage>(), _canvas);

            var views = new[] { ("BODY", CharacterPreview.View.Body), ("BUST", CharacterPreview.View.Bust), ("FACE", CharacterPreview.View.Face) };
            for (int i = 0; i < views.Length; i++)
            {
                float x = left + 22f + i * 112f, vy = top - PreviewH + 22f;
                var b = UiKit.Panel(_full, Centre, Centre, new Vector2(x, vy), new Vector2(x + 100f, vy + 56f), GoldDim);
                var fill = UiKit.Panel(b, Vector2.zero, Vector2.one, new Vector2(1, 1), new Vector2(-1, -1), Glass).GetComponent<Image>();
                var label = UiKit.Label(UiKit.Rect(b, "l", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero),
                    views[i].Item1, 16, Muted, TextAnchor.MiddleCenter);
                var view = views[i].Item2;
                _viewButtons.Add((fill, label, view));
                _hits.Add((b, () => { _preview.Frame(view); PaintViews(); }));
            }
            PaintViews();

            // ---- the column on the right ----
            float cx = left + PreviewW + Gap;
            UiKit.Label(UiKit.Rect(_full, "ph", Centre, Centre, new Vector2(cx, top - 34f), new Vector2(cx + ColumnW, top)),
                "POSE", 22, Gold, TextAnchor.MiddleLeft);

            // Standing, the open poses, then any the character earned - five to a row.
            var poses = new List<(string Id, string Name)> { ("", "Standing") };
            foreach (var id in Tuning.Cosmetics.OpenPoses)
            {
                var def = PoseCatalog.Get(id);
                if (def != null) poses.Add((def.Id, def.Name));
            }
            var items = _profile().Items;
            foreach (var def in PoseCatalog.All)
                if (Array.IndexOf(Tuning.Cosmetics.OpenPoses, def.Id) < 0 && items != null && items.HasPose(def.Id))
                    poses.Add((def.Id, def.Name));

            const int perRow = 5;
            const float gap = 14f;
            int rows = (poses.Count + perRow - 1) / perRow;
            float cw = (ColumnW - (perRow - 1) * gap) / perRow;
            float ch = rows == 1 ? 228f : rows == 2 ? 170f : 122f;
            float y0 = top - 48f;
            for (int i = 0; i < poses.Count; i++)
            {
                float x = cx + (i % perRow) * (cw + gap);
                float cy = y0 - (i / perRow) * (ch + gap);
                var card = Framed(new Vector2(x, cy - ch), new Vector2(x + cw, cy), out var fill);

                // The character in this pose, painted by RenderThumbnails a moment after the screen opens.
                var picGo = new GameObject("pose", typeof(RawImage));
                picGo.transform.SetParent(card, false);
                var prt = (RectTransform)picGo.transform;
                prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
                prt.offsetMin = new Vector2(10, 40); prt.offsetMax = new Vector2(-10, -10);
                var pic = picGo.GetComponent<RawImage>();
                pic.raycastTarget = false;
                pic.color = Color.clear;
                var aspect = picGo.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = ThumbW / (float)ThumbH;

                var label = UiKit.Label(UiKit.Rect(card, "n", new Vector2(0, 0), new Vector2(1, 0), new Vector2(4, 8), new Vector2(-4, 38)),
                    poses[i].Name.ToUpperInvariant(), 16, Ink, TextAnchor.MiddleCenter);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 11;
                label.resizeTextMaxSize = 16;

                string id = poses[i].Id;
                _poseCards.Add(new PoseCard { Pose = id, Fill = fill, Edge = card.GetComponent<Image>(), Label = label, Picture = pic });
                _hits.Add((card, () => ChoosePose(id)));
            }
            PaintPoses();

            float y = y0 - rows * (ch + gap) + gap - 8f;
            UiKit.Label(UiKit.Rect(_full, "earned", Centre, Centre, new Vector2(cx, y - 30f), new Vector2(cx + ColumnW, y)),
                "Earned poses join this row when an avatar drops one.", 17, Muted, TextAnchor.MiddleLeft);
            y -= 52f;

            // ---- SAVE A PICTURE: the gold button; KEEP FOR A FRAME beside it ----
            const float keepW = 300f;
            float saveR = cx + ColumnW - keepW - 16f;
            var save = UiKit.Panel(_full, Centre, Centre, new Vector2(cx, y - 120f), new Vector2(saveR, y), GoldBright);
            UiKit.Panel(save, Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3), Gold);
            CameraIcon(save, new Vector2(-190f, 0f), InkOnGold);
            UiKit.Label(UiKit.Rect(save, "l", Vector2.zero, Vector2.one, new Vector2(60, 0), Vector2.zero),
                "SAVE A PICTURE", 30, InkOnGold, TextAnchor.MiddleCenter);
            _hits.Add((save, SavePicture));

            // The moment as taken, kept with the character to hang in the hub's frames (Chain.BoothShots).
            var keep = Framed(new Vector2(saveR + 16f, y - 120f), new Vector2(cx + ColumnW, y), out _);
            UiKit.Label(UiKit.Rect(keep, "l", new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(12, -6), new Vector2(-12, -14)),
                "KEEP FOR A FRAME", 22, GoldBright, TextAnchor.MiddleCenter);
            _keepCount = UiKit.Label(UiKit.Rect(keep, "n", new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(12, 14), new Vector2(-12, 6)),
                "", 16, Muted, TextAnchor.MiddleCenter);
            PaintKeepCount();
            _hits.Add((keep, KeepForFrame));
            y -= 120f;

            _note = UiKit.Label(UiKit.Rect(_full, "note", Centre, Centre, new Vector2(cx, y - 34f), new Vector2(cx + ColumnW, y - 4f)),
                "", 17, Muted, TextAnchor.MiddleCenter);
            y -= 46f;

            // ---- the portrait NFT's strip ----
            if (PortraitsHere || RunFormat.Showcase)
            {
                var strip = Framed(new Vector2(cx, y - 110f), new Vector2(cx + ColumnW, y), out _);
                UiKit.Label(UiKit.Rect(strip, "t", new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(26, 0), new Vector2(-26, -12)),
                    "PORTRAIT NFT", 20, Ink, TextAnchor.LowerLeft);
                _portraitLine = UiKit.Label(UiKit.Rect(strip, "s", new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(26, 12), new Vector2(-26, -4)),
                    "", 16, Muted, TextAnchor.UpperLeft);
                if (PortraitsHere)
                {
                    var b = UiKit.Panel(strip, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-400f, -34f), new Vector2(-22f, 34f), Gold);
                    UiKit.Panel(b, Vector2.zero, Vector2.one, new Vector2(1, 1), new Vector2(-1, -1), Glass);
                    _portraitLabel = UiKit.Label(UiKit.Rect(b, "l", Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0)),
                        "", 16, GoldBright, TextAnchor.MiddleCenter);
                    _hits.Add((b, PressPortrait));
                }
                PaintPortrait();
            }

            UiKit.Hint(UiKit.Rect(_full, "h", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 22), new Vector2(0, 58)),
                "scroll or drag the figure to frame it    -    [ESC] leave the booth",
                "pinch or drag the figure to frame it    -    BACK to leave the booth",
                18, new Color(0.47f, 0.45f, 0.54f));
        }

        /// <summary>A gold edge round dark glass, with a fainter lining inside - the stake screen's
        /// cards. <paramref name="fill"/> is the glass, lit for the chosen pose.</summary>
        RectTransform Framed(Vector2 min, Vector2 max, out Image fill)
        {
            var edge = UiKit.Panel(_full, Centre, Centre, min, max, Gold);
            UiKit.Panel(edge, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f), Glass);
            var lining = UiKit.Panel(edge, Vector2.zero, Vector2.one, new Vector2(7f, 7f), new Vector2(-7f, -7f), GoldDim);
            // The glass inside the lining is what lights for the chosen pose.
            fill = UiKit.Panel(lining, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f), Glass).GetComponent<Image>();
            return edge;
        }

        /// <summary>The mock's camera mark, in blocks: a body, a lens and the viewfinder's hump.</summary>
        static void CameraIcon(RectTransform parent, Vector2 at, Color ink)
        {
            void Block(float x0, float y0, float x1, float y1)
                => UiKit.Panel(parent, Centre, Centre, at + new Vector2(x0, y0), at + new Vector2(x1, y1), ink);
            Block(-24, -16, 24, -12); Block(-24, 12, 24, 16);      // body, top and bottom
            Block(-24, -16, -20, 16); Block(20, -16, 24, 16);      // body, sides
            Block(-9, 16, 9, 22);                                  // the hump
            Block(-7, -7, 7, -4); Block(-7, 4, 7, 7);              // the lens
            Block(-7, -7, -4, 7); Block(4, -7, 7, 7);
        }

        void PaintPoses()
        {
            foreach (var card in _poseCards)
            {
                bool on = card.Pose == _pose;
                card.Fill.color = on ? GlassLit : Glass;
                card.Edge.color = on ? GoldBright : Gold;
                card.Label.color = on ? LitInk : Ink;
            }
        }

        void PaintViews()
        {
            var current = _preview.CurrentView;
            foreach (var (fill, label, view) in _viewButtons)
            {
                bool on = current == view;
                fill.color = on ? GlassLit : Glass;
                label.color = on ? LitInk : Muted;
            }
        }

        // ------------------------------------------------------------------ the pose cards' pictures

        /// <summary>
        /// Paints every pose card with the character standing in its pose: a second, small preview
        /// (shifted off the main one's spot, so neither camera sees the other's rig) holds each pose
        /// in turn, is fitted once the rig has drawn it, and its picture is copied to the card. A few
        /// frames a card while the screen is already up - nothing waits for it.
        /// </summary>
        IEnumerator RenderThumbnails()
        {
            _thumbs.Build(_element, ThumbW, ThumbH, shift: 40f);
            _thumbs.Background = Glass;   // the card's own glass, so the figure stands on the card
            _thumbs.Refresh(_profile());
            foreach (var card in _poseCards)
            {
                _thumbs.Rig?.HoldPose(PoseCatalog.Get(card.Pose));
                yield return null;
                yield return null;
                _thumbs.Fit();
                yield return null;
                yield return null;
                if (!IsOpen || _thumbs.Texture == null) yield break;
                card.Texture = new RenderTexture(ThumbW, ThumbH, 0) { filterMode = FilterMode.Point, name = "booth-pose" };
                Graphics.Blit(_thumbs.Texture, card.Texture);
                card.Picture.texture = card.Texture;
                card.Picture.color = Color.white;
            }
            _thumbs.Dispose();
            _thumbWork = null;
        }

        // ------------------------------------------------------------------ actions

        void ChoosePose(string pose)
        {
            _pose = pose;
            _preview.Rig?.HoldPose(PoseCatalog.Get(pose));
            PaintPoses();
            _refitFrame = Time.frameCount + RefitFrames;
        }

        /// <summary>
        /// A pose reaches further than standing - a blade overhead, out level, through a leap - and
        /// the frame was fitted to the figure standing, so it is fitted again once the rig has drawn
        /// the pose (it poses on its own next ticks; CharacterPreview.Fit measures what is drawn).
        /// A zoom the player chose is kept.
        /// </summary>
        int _refitFrame = -1;
        const int RefitFrames = 2;

        void SavePicture()
        {
            var p = _profile();
            string who = string.IsNullOrEmpty(p.DisplayName) ? "portrait" : p.DisplayName;
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) who = who.Replace(c, '-');
            string name = $"equivalence-{who.Replace(' ', '-').ToLowerInvariant()}-{DateTime.Now:yyyyMMdd-HHmmss}.png";
            Note(PictureSaver.Save(PictureSaver.Png(_preview.Texture), name));
        }

        async void AskStatus()
        {
            if (!PortraitsHere || !Chain.Web.ChainWallet.Connected) return;
            _statusAsked = true;
            PaintPortrait();
            try { _status = await Chain.Web.ChainPfp.StatusAsync(_profile().ProfileId); }
            catch (Exception e) { Debug.LogWarning($"[Booth] portrait status: {e.Message}"); }
            _statusAsked = false;
            if (IsOpen) PaintPortrait();
        }

        void PaintPortrait()
        {
            if (_portraitLine == null) return;
            if (!PortraitsHere)
            {
                _portraitLine.text = "Portraits on the chain come with the full game.";
                return;
            }
            bool connected = Chain.Web.ChainWallet.Connected;
            string wallet = connected ? Chain.Web.ChainWallet.Short(Chain.Web.ChainWallet.Address) : "no wallet connected";
            _portraitLine.text = !connected ? wallet
                : _statusAsked ? $"{wallet}  ·  checking..."
                : _status != null && _status.taken
                    ? $"{wallet}  ·  " + (_status.lapsed ? "year ran out" : $"paid until {_status.paidUntil?.Substring(0, Math.Min(10, _status.paidUntil?.Length ?? 0))}")
                    : $"{wallet}  ·  no portrait yet";
            if (_portraitLabel == null) return;
            _portraitLabel.text = !connected ? "CONNECT A WALLET"
                : _statusAsked && _status == null ? "CHECKING..."
                : _status != null && _status.taken ? $"UPDATE IN THIS POSE  ·  {Chain.Web.PfpPrices.RetakeAda} ADA"
                : $"TAKE IN THIS POSE  ·  {Chain.Web.PfpPrices.MintAda} ADA";
        }

        void PressPortrait()
        {
            if (!Chain.Web.ChainWallet.Connected)
            {
                var connect = _onConnect;
                Close();
                connect?.Invoke();
                return;
            }
            if (_statusAsked && _status == null) return;
            var take = _onPortrait;
            string pose = _pose;
            Close();
            take?.Invoke(pose);
        }

        void Note(string text)
        {
            if (_note != null) _note.text = text;
        }

        // ------------------------------------------------------------------ booth shots

        Text _keepCount;
        bool _keeping;
        /// <summary>The picture waiting on the replace chooser, taken when KEEP was pressed.</summary>
        byte[] _pendingShot;
        GameObject _replaceRoot;
        List<(RectTransform Rect, Action OnClick)> _replaceHits = new();

        void PaintKeepCount()
        {
            if (_keepCount == null || _profile == null) return;
            int n = BoothShots.CountFor(_profile().ProfileId);
            _keepCount.text = $"{n} of {Tuning.Booth.ShotsKept} kept";
        }

        /// <summary>
        /// The picture at the art's own pixel size: the preview draws <see cref="CharacterPreview.PixelsPerTexel"/>
        /// screen pixels to a texel, so every second one of those keeps every texel and weighs a few KB.
        /// A frame zoomed far out keeps one sample a texel instead, if two would be over the cap.
        /// </summary>
        byte[] ShotPng()
        {
            var rt = _preview.Texture;
            if (rt == null) return null;
            int steps = Mathf.Max(1, _preview.PixelsPerTexel);
            byte[] png = null;
            foreach (int f in new[] { Mathf.Max(1, steps / 2), steps })
            {
                png = Downsampled(rt, f);
                if (png != null && png.Length <= Tuning.Booth.ShotMaxBytes) return png;
            }
            return png;
        }

        static byte[] Downsampled(RenderTexture rt, int f)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var full = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            full.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            full.Apply();
            RenderTexture.active = prev;

            int w = Mathf.Max(1, rt.width / f), h = Mathf.Max(1, rt.height / f);
            var src = full.GetPixels32();
            var dst = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dst[y * w + x] = src[Mathf.Min(rt.height - 1, y * f + f / 2) * rt.width + Mathf.Min(rt.width - 1, x * f + f / 2)];
            Destroy(full);
            var small = new Texture2D(w, h, TextureFormat.RGB24, false);
            small.SetPixels32(dst);
            small.Apply();
            var png = small.EncodeToPNG();
            Destroy(small);
            return png;
        }

        async void KeepForFrame()
        {
            if (_keeping) return;
            var p = _profile();
            var png = ShotPng();
            if (png == null) { Note("the picture could not be made"); return; }
            if (BoothShots.CountFor(p.ProfileId) >= Tuning.Booth.ShotsKept)
            {
                OpenReplace();
                _pendingShot = png;   // after: opening clears any chooser before it, and its picture
                return;
            }
            await Keep(png, null);
        }

        async System.Threading.Tasks.Task Keep(byte[] png, string replaces)
        {
            _keeping = true;
            Note("keeping...");
            string said;
            try { said = await BoothShots.KeepAsync(_profile(), png, replaces); }
            catch (Exception e) { Debug.LogWarning($"[Booth] keep: {e.Message}"); said = "could not keep the picture - try again"; }
            _keeping = false;
            if (!IsOpen) return;
            Note(said);
            PaintKeepCount();
        }

        /// <summary>Twelve are kept: the character's shots in a grid over the booth, the one tapped gives
        /// its place to the new picture. A frame showing it then stands empty.</summary>
        void OpenReplace()
        {
            CloseReplace();
            _replaceHits ??= new();
            _replaceRoot = new GameObject("replace", typeof(RectTransform));
            _replaceRoot.transform.SetParent(_full, false);
            var full = (RectTransform)_replaceRoot.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;
            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.02f, 0.02f, 0.04f, 0.94f));
            UiKit.Label(UiKit.Rect(full, "t", Centre, Centre, new Vector2(-700f, 330f), new Vector2(700f, 390f)),
                $"YOU KEEP {Tuning.Booth.ShotsKept}  -  REPLACE WHICH?", 34, Gold, TextAnchor.MiddleCenter);
            UiKit.Label(UiKit.Rect(full, "s", Centre, Centre, new Vector2(-700f, 290f), new Vector2(700f, 326f)),
                "The new picture takes its place. A frame showing the old one stands empty.", 19, Muted, TextAnchor.MiddleCenter);

            const int cols = 6;
            const float cw = 190f, chh = 250f, gap = 18f;
            float x0 = -(cols * cw + (cols - 1) * gap) * 0.5f, top = 260f;
            var shots = BoothShots.Of(_profile().ProfileId);
            for (int i = 0; i < shots.Count; i++)
            {
                float x = x0 + (i % cols) * (cw + gap), yTop = top - (i / cols) * (chh + gap);
                var edge = UiKit.Panel(full, Centre, Centre, new Vector2(x, yTop - chh), new Vector2(x + cw, yTop), Gold);
                UiKit.Panel(edge, Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2), Glass);
                var art = shots[i].Art;
                if (art != null)
                {
                    var picGo = new GameObject("shot", typeof(RawImage));
                    picGo.transform.SetParent(edge, false);
                    var prt = (RectTransform)picGo.transform;
                    prt.anchorMin = Vector2.zero; prt.anchorMax = Vector2.one;
                    prt.offsetMin = new Vector2(8, 8); prt.offsetMax = new Vector2(-8, -8);
                    var pic = picGo.GetComponent<RawImage>();
                    pic.texture = art.texture;
                    pic.raycastTarget = false;
                    var fit = picGo.AddComponent<AspectRatioFitter>();
                    fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                    fit.aspectRatio = art.texture.width / (float)Mathf.Max(1, art.texture.height);
                }
                string id = shots[i].ShotId;
                _replaceHits.Add((edge, () => Replace(id)));
            }

            var cancel = Framed(new Vector2(-150f, top - 2 * (chh + gap) - 70f), new Vector2(150f, top - 2 * (chh + gap) - 10f), out _);
            cancel.SetParent(full, true);
            UiKit.Label(UiKit.Rect(cancel, "l", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), "KEEP THEM ALL", 20, GoldBright, TextAnchor.MiddleCenter);
            _replaceHits.Add((cancel, () => { CloseReplace(); Note("nothing replaced"); }));
        }

        async void Replace(string shotId)
        {
            var png = _pendingShot;
            CloseReplace();
            if (png != null) await Keep(png, shotId);
        }

        void CloseReplace()
        {
            _pendingShot = null;
            _replaceHits?.Clear();
            if (_replaceRoot) Destroy(_replaceRoot);
            _replaceRoot = null;
        }

        // ------------------------------------------------------------------ input

        void Update()
        {
            if (!IsOpen) return;

            _focusRects ??= new();
            _focusRects.Clear();
            var hits = _replaceRoot ? _replaceHits : _hits;
            foreach (var (rect, _) in hits) if (rect != null) _focusRects.Add(rect);
            Controls.SetFocusCandidates(_focusRects);

            if (_refitFrame >= 0 && Time.frameCount >= _refitFrame) { _refitFrame = -1; _preview.Fit(); }

            if (Time.frameCount == _openedFrame) return;
            if (_replaceRoot)
            {
                if (Controls.CancelTapped) { CloseReplace(); Note("nothing replaced"); return; }
                if (!Controls.Tapped(out var tap)) return;
                foreach (var (rect, onClick) in _replaceHits)
                {
                    if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, tap, null)) continue;
                    onClick?.Invoke();
                    return;
                }
                return;
            }
            if (Controls.CancelTapped) { Close(); return; }

            var before = _preview.CurrentView;
            PreviewGesture();
            if (_preview.CurrentView != before) PaintViews();

            if (!Controls.Tapped(out var at)) return;
            foreach (var (rect, onClick) in _hits)
            {
                if (rect == null || !RectTransformUtility.RectangleContainsScreenPoint(rect, at, null)) continue;
                onClick?.Invoke();
                return;
            }
        }

        bool _dragging;

        /// <summary>Scroll or pinch to zoom, drag to pan - the circle's preview gesture.</summary>
        void PreviewGesture()
        {
            if (_previewRect == null) return;
            var at = Controls.PointerPosition;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(_previewRect, at, null);
            // The framing buttons sit on the frame: a press on one is a press, not a drag.
            foreach (var (fill, _, _) in _viewButtons)
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)fill.transform, at, null)) over = false;

            float notches = Controls.Zoom;
            if (over && Mathf.Abs(notches) > 0.001f)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_previewRect, at, null, out var local);
                var r = _previewRect.rect;
                _preview.Zoom(notches, new Vector2(Mathf.Clamp01((local.x - r.xMin) / Mathf.Max(1f, r.width)),
                                                   Mathf.Clamp01((local.y - r.yMin) / Mathf.Max(1f, r.height))));
            }

            if (Controls.Pinching) _dragging = false;
            else if (over && Controls.Tapped(out _)) _dragging = true;
            if (!Controls.PointerHeld(out _)) _dragging = false;
            if (_dragging)
            {
                var d = Controls.PointerDelta;
                if (d.sqrMagnitude > 0.01f) _preview.PanBy(d * (_canvas != null ? _canvas.scaleFactor : 1f));
            }
        }
    }
}
