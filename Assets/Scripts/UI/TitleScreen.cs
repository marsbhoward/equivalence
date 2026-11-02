using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TT = Convergence.Core.Tuning.Title;

namespace Convergence.UI
{
    /// <summary>
    /// THE TITLE (the user's picks, 2026-11-01, mock https://claude.ai/artifact/Pvm9axMhq7jmbQD8aAanjs:
    /// centred stack, the sigil behind the mark, the gold glow rising and falling, medium mark, ink
    /// vignette). The first thing the game shows: the Equivalence mark over the ways in. Signing in
    /// happens HERE rather than at the hub's booth - a wallet the browser signed in with before is
    /// offered to continue; each wallet extension the browser has gets a button (none when there are
    /// none); a player who does not sign in starts a new game, or continues, as this browser's guest.
    ///
    /// The mark is still; its ring makes one full turn when a choice is taken (the words hold
    /// still - the ring and the words are separate layers), and keeps turning slowly while a
    /// wallet's popup is waiting. Behind it the sigil's four interlocking rings, gold, breathing, and
    /// RIFT FLAKES rising from the bottom centre over a faint light (Tuning.Title; the rift mock,
    /// https://claude.ai/artifact/XWC3PsGB2UWYHkruYMkCsN).
    /// Laid out in the 1920x1080 reference canvas (UiKit.CreateCanvas).
    /// </summary>
    public class TitleScreen : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        public struct Option
        {
            public string Label, Detail;
            public bool Primary, Wallet;
            public Option(string label, string detail, bool primary = false, bool wallet = false)
            { Label = label; Detail = detail; Primary = primary; Wallet = wallet; }
        }

        // ---- the layout (canvas units; the mock's proportions at 1920 wide) ----
        const float MarkW = 422f, Pad = 154f, BelowMark = 135f, WaysW = 576f;
        const float PrimaryH = 100f, SecondaryH = 80f, WalletH = 74f, Gap = 18f;
        const float SigilScale = 1.55f;
        /// <summary>One turn of the ring when a choice is taken; the answer waits for it.</summary>
        const float SpinSeconds = 0.9f;
        /// <summary>The ring's turn while a wallet's popup waits, degrees a second.</summary>
        const float BusyTurn = 40f;
        /// <summary>The gold's breath: a full rise and fall.</summary>
        const float BreathSeconds = 4.5f;

        static readonly Color Gold = new(0.91f, 0.73f, 0.24f);
        static readonly Color Ink = new(0.93f, 0.91f, 0.87f);
        static readonly Color Sub = new(0.56f, 0.58f, 0.64f);
        static readonly Color Faint = new(0.36f, 0.38f, 0.44f);

        GameObject _root;
        Text _status, _foot;
        RectTransform _ring;
        RawImage _rings, _glow;
        readonly List<RectTransform> _buttons = new();
        TaskCompletionSource<int> _answer;
        int _openedFrame, _picked = -1;
        float _spinT = -1f, _angle;
        bool _busy;

        static Texture2D _vignette, _sigil, _sigilGlow, _below;

        // ---- the rift flakes: square texels on a grid ~1/320 of the screen, as a Rift draws its flakes
        struct Flake
        {
            public float X0, Y0, Reach, Speed, Life, Sway, SwayRate, Phase, Twinkle, T;
            public int Size; public bool Long; public Color32 C;
        }
        RawImage _flakesImg, _belowImg;
        Texture2D _flakeTex;
        Color32[] _fpx;
        int _fw, _fh;
        Flake[] _flakes;
        uint _seed = 7u;
        float _gust;

        /// <summary>Show (or re-show) the choices and wait for one; the answer is its index.</summary>
        public Task<int> ChooseAsync(Transform canvas, IList<Option> options, string status = "")
        {
            Build(canvas, options);
            SetStatus(status);
            _busy = false;
            _picked = -1;
            _answer = new TaskCompletionSource<int>();
            return _answer.Task;
        }

        /// <summary>Say what is happening (a wallet's popup is waiting); the buttons stand idle and
        /// the ring turns meanwhile.</summary>
        public void SetStatus(string text, bool busy = false)
        {
            _busy = busy;
            if (_status != null) _status.text = text ?? "";
            // The footer steps aside while there is something to say - the two share the bottom.
            if (_foot != null) _foot.enabled = string.IsNullOrEmpty(text);
            foreach (var b in _buttons)
                if (b != null && b.TryGetComponent<CanvasGroup>(out var g)) g.alpha = busy ? 0.4f : 1f;
        }

        public void Close()
        {
            IsOpen = false;
            if (_root) Destroy(_root);
            _root = null;
            _buttons.Clear();
        }

        // ------------------------------------------------------------------ building

        void Build(Transform canvas, IList<Option> options)
        {
            if (_root) Destroy(_root);
            _buttons.Clear();
            IsOpen = true;
            _openedFrame = Time.frameCount;

            _root = new GameObject("TitleScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            // The ink vignette: lifted behind the mark, falling to near black at the edges.
            var bg = UiKit.Rect(full, "ground", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bgi = bg.gameObject.AddComponent<RawImage>();
            bgi.texture = Vignette();
            bgi.raycastTarget = false;

            // The mark's size and where its ring's centre sits, from the top.
            var ringTex = Resources.Load<Texture2D>("Title/logo_ring");
            var wordTex = Resources.Load<Texture2D>("Title/logo_word");
            float aspect = ringTex != null ? ringTex.height / (float)ringTex.width : 0.937f;
            float markH = MarkW * aspect, centreY = Pad + markH * 0.5f;

            // The sigil behind it: glow under the rings, both breathing (Update).
            float sigil = MarkW * SigilScale;
            _glow = Layer(full, "sigil.glow", SigilGlow(), new Vector2(sigil * 1.12f, sigil * 1.12f), centreY);
            _rings = Layer(full, "sigil", Sigil(), new Vector2(sigil, sigil), centreY);

            // The rift flakes and the light below them, behind the mark and the choices.
            BuildFlakes(full);

            // The mark: the ring (turns) over the words (still).
            if (ringTex != null) _ring = Layer(full, "mark.ring", ringTex, new Vector2(MarkW, markH), centreY).rectTransform;
            if (wordTex != null) Layer(full, "mark.word", wordTex, new Vector2(MarkW, markH), centreY);
            if (_ring != null) _ring.localEulerAngles = new Vector3(0f, 0f, _angle);

            // The ways in: the main choice and any other, stacked; the wallets in a row beneath,
            // starting below the sigil's bottom ring.
            float y = Pad + markH + BelowMark;
            var wallets = new List<int>();
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Wallet) { wallets.Add(i); continue; }
                float h = options[i].Primary ? PrimaryH : SecondaryH;
                Button(full, i, options[i], -WaysW * 0.5f, y, WaysW, h);
                y += h + Gap;
            }
            if (wallets.Count > 0)
            {
                var cap = Top(full, "or", -WaysW * 0.5f, y, WaysW, 34f);
                UiKit.Label(cap, "OR SIGN IN WITH A WALLET", 17, Sub, TextAnchor.MiddleCenter);
                y += 34f + 6f;
                float w = (WaysW - Gap * (wallets.Count - 1)) / wallets.Count;
                for (int k = 0; k < wallets.Count; k++)
                    Button(full, wallets[k], options[wallets[k]], -WaysW * 0.5f + k * (w + Gap), y, w, WalletH);
                y += WalletH + Gap;
            }
            var st = Top(full, "status", -460f, y + 4f, 920f, 72f);
            _status = UiKit.Label(st, "", 20, Sub, TextAnchor.UpperCenter);
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;

            var foot = UiKit.Rect(full, "foot", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 12f), new Vector2(-40f, 44f));
            _foot = UiKit.Label(foot, "Signed in, your characters live with your wallet. Without one, they stay with this browser.",
                16, Faint, TextAnchor.MiddleCenter);
        }

        void Button(RectTransform full, int index, Option o, float x, float y, float w, float h)
        {
            var btn = Top(full, $"way.{index}", x, y, w, h);
            // A NOTCHED PLATE (the user's pick, button style 3): cut corners, shaded top to foot, a
            // fine line inset along its edge - gold on the main choice.
            var face = btn.gameObject.AddComponent<Image>();
            face.sprite = Plate(o.Primary);
            face.type = Image.Type.Sliced;
            face.color = Color.white;
            btn.gameObject.AddComponent<CanvasGroup>();
            bool sub = !string.IsNullOrEmpty(o.Detail);
            var top = UiKit.Rect(btn, "l", new Vector2(0f, sub ? 0.42f : 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            UiKit.Label(top, o.Label, o.Primary ? 32 : o.Wallet ? 22 : 24, o.Primary ? new Color(0.95f, 0.84f, 0.58f) : Ink, TextAnchor.MiddleCenter);
            if (sub)
            {
                var d = UiKit.Rect(btn, "d", new Vector2(0f, 0f), new Vector2(1f, 0.46f), Vector2.zero, Vector2.zero);
                UiKit.Label(d, o.Detail, 17, Sub, TextAnchor.MiddleCenter);
            }
            _buttons.Add(btn);
        }

        /// <summary>A rect hung from the top centre: (x, y) its top-left, y measured down.</summary>
        static RectTransform Top(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var r = UiKit.Rect(parent, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        static RawImage Layer(RectTransform parent, string name, Texture2D tex, Vector2 size, float centreFromTop)
        {
            var r = UiKit.Rect(parent, name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            r.sizeDelta = size;
            r.anchoredPosition = new Vector2(0f, -centreFromTop);
            var img = r.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
            return img;
        }

        // ------------------------------------------------------------------ pictures

        static Sprite _plate, _platePrimary;

        /// <summary>The plate, nine-sliced: corners cut on the diagonal (<c>Notch</c> units), the face
        /// shaded from top to foot, a line inset <c>Inset</c> units following the cut. One sprite
        /// texel to a canvas unit, so the cut is the same size on every button.</summary>
        static Sprite Plate(bool primary)
        {
            var cached = primary ? _platePrimary : _plate;
            if (cached != null) return cached;
            const int S = 64, Border = 22;
            const float Notch = 17f, Inset = 7f, Line = 1.4f;
            Color top = primary ? new Color(0.227f, 0.188f, 0.114f) : new Color(0.106f, 0.118f, 0.157f);
            Color foot = primary ? new Color(0.149f, 0.122f, 0.075f) : new Color(0.075f, 0.082f, 0.114f);
            Color line = primary ? new Color(0.95f, 0.84f, 0.58f, 0.40f) : new Color(0.93f, 0.91f, 0.87f, 0.13f);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                // Signed distance inside the notched rectangle: the nearer of the four edges and the
                // four diagonal cuts.
                float edge = Mathf.Min(Mathf.Min(fx, S - fx), Mathf.Min(fy, S - fy));
                float cx = Mathf.Min(fx, S - fx), cy = Mathf.Min(fy, S - fy);
                float cut = (cx + cy - Notch) * 0.70710678f;
                float d = Mathf.Min(edge, cut);
                float a = Mathf.Clamp01(d + 0.5f);
                if (a <= 0f) { px[y * S + x] = Color.clear; continue; }
                var c = Color.Lerp(foot, top, fy / S);
                float l = Mathf.Clamp01(Line * 0.5f + 0.5f - Mathf.Abs(d - Inset));
                c = Color.Lerp(c, new Color(line.r, line.g, line.b, 1f), l * line.a);
                c.a = a;
                px[y * S + x] = c;
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(px);
            tex.Apply();
            var spr = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f, 0,
                                    SpriteMeshType.FullRect, new Vector4(Border, Border, Border, Border));
            spr.hideFlags = HideFlags.HideAndDontSave;
            if (primary) _platePrimary = spr; else _plate = spr;
            return spr;
        }

        static Texture2D Vignette()
        {
            if (_vignette != null) return _vignette;
            const int W = 192, H = 108;
            var px = new Color[W * H];
            var core = new Color(0.114f, 0.125f, 0.188f);
            var mid = new Color(0.051f, 0.055f, 0.078f);
            var edge = new Color(0.020f, 0.020f, 0.031f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // An ellipse the screen's shape, centred a little above the middle (behind the mark).
                float u = (x + 0.5f) / W - 0.5f, v = (y + 0.5f) / H - 0.62f;
                float r = Mathf.Sqrt(u * u + v * v * (H * H) / (float)(W * W) * 3.2f) * 1.6f;
                px[y * W + x] = r < 0.55f ? Color.Lerp(core, mid, r / 0.55f) : Color.Lerp(mid, edge, Mathf.Clamp01((r - 0.55f) / 0.45f));
            }
            _vignette = Make(px, W, H, FilterMode.Bilinear);
            return _vignette;
        }

        // The sigil's geometry (UI.SigilRings): four rings at the compass points, neighbours 1.6
        // radii apart - R 47 and offset 53 in a 212 square.
        const int SigilTex = 512;
        static float Scale => SigilTex / 212f;

        static float RingDistance(float x, float y)
        {
            float c = 106f * Scale, d = 53f * Scale, r = 47f * Scale;
            float best = float.MaxValue;
            foreach (var (ox, oy) in new[] { (0f, d), (0f, -d), (-d, 0f), (d, 0f) })
            {
                float dx = x - (c + ox), dy = y - (c + oy);
                best = Mathf.Min(best, Mathf.Abs(Mathf.Sqrt(dx * dx + dy * dy) - r));
            }
            return best;
        }

        static Texture2D Sigil()
        {
            if (_sigil != null) return _sigil;
            var px = new Color[SigilTex * SigilTex];
            float half = 1.4f * Scale * 0.5f;
            for (int y = 0; y < SigilTex; y++)
            for (int x = 0; x < SigilTex; x++)
            {
                float a = Mathf.Clamp01(half + 0.5f - RingDistance(x + 0.5f, y + 0.5f));   // antialiased stroke
                px[y * SigilTex + x] = new Color(Gold.r, Gold.g, Gold.b, a);
            }
            _sigil = Make(px, SigilTex, SigilTex, FilterMode.Bilinear);
            return _sigil;
        }

        static Texture2D SigilGlow()
        {
            if (_sigilGlow != null) return _sigilGlow;
            // A little larger than the rings (the glow spills past the outermost rim), same centre.
            float k = 1.12f;
            var px = new Color[SigilTex * SigilTex];
            for (int y = 0; y < SigilTex; y++)
            for (int x = 0; x < SigilTex; x++)
            {
                float sx = (x + 0.5f - SigilTex * 0.5f) * k + SigilTex * 0.5f, sy = (y + 0.5f - SigilTex * 0.5f) * k + SigilTex * 0.5f;
                float d = RingDistance(sx, sy);
                float a = Mathf.Exp(-d / (4.5f * Scale)) * 0.55f + Mathf.Exp(-d / (1.6f * Scale)) * 0.45f;
                px[y * SigilTex + x] = new Color(Gold.r, Gold.g * 0.92f, Gold.b * 0.8f, a);
            }
            _sigilGlow = Make(px, SigilTex, SigilTex, FilterMode.Bilinear);
            return _sigilGlow;
        }

        // ------------------------------------------------------------------ the rift flakes

        /// <summary>A hashed stream, never UnityEngine.Random (the project's rule for looks).</summary>
        float Rnd() { _seed = _seed * 1664525u + 1013904223u; return (_seed >> 8) / 16777216f; }
        float Gauss() => (Rnd() + Rnd() + Rnd() - 1.5f) / 1.5f;

        void BuildFlakes(RectTransform full)
        {
            _belowImg = UiKit.Rect(full, "rift.light", new Vector2(0.5f - TT.GlowWidth * 0.5f, 0f), new Vector2(0.5f + TT.GlowWidth * 0.5f, TT.GlowHeight), Vector2.zero, Vector2.zero)
                .gameObject.AddComponent<RawImage>();
            _belowImg.texture = Below();
            _belowImg.raycastTarget = false;

            var r = full.rect;
            float aspect = r.width > 1f && r.height > 1f ? r.height / r.width : 9f / 16f;
            int w = Core.Tuning.Title.FlakeTexelsAcross, h = Mathf.Max(1, Mathf.RoundToInt(w * aspect));
            if (_flakeTex == null || _fw != w || _fh != h)
            {
                if (_flakeTex != null) Destroy(_flakeTex);
                _fw = w; _fh = h;
                _flakeTex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                _fpx = new Color32[w * h];
            }
            _flakesImg = UiKit.Rect(full, "rift.flakes", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            _flakesImg.texture = _flakeTex;
            _flakesImg.raycastTarget = false;
            if (_flakes == null)
            {
                _flakes = new Flake[Core.Tuning.Title.Flakes];
                for (int i = 0; i < _flakes.Length; i++) _flakes[i] = Spawn(Rnd());   // spread over their climb
            }
        }

        /// <summary>The rift's white, cyan and deep cyan, with the sigil's gold - weighted.</summary>
        static readonly (Color32 c, float w)[] FlakeColours =
        {
            (new Color32(242, 253, 255, 255), 0.28f), (new Color32(115, 224, 255, 255), 0.34f),
            (new Color32(47, 182, 232, 255), 0.14f), (new Color32(232, 186, 61, 255), 0.24f),
        };

        /// <summary>A flake born at the foot, <paramref name="age"/> of the way up its climb.</summary>
        Flake Spawn(float age)
        {
            float pick = Rnd();
            var col = FlakeColours[0].c;
            foreach (var (c, w) in FlakeColours) { if ((pick -= w) <= 0f) { col = c; break; } }
            var f = new Flake
            {
                X0 = _fw * 0.5f + Gauss() * TT.Spread * _fw,
                Y0 = -2f,
                Reach = _fh * TT.Reach * Mathf.Lerp(TT.ReachMin, 1f, Rnd()),
                Speed = _fh * Mathf.Lerp(TT.SpeedMin, TT.SpeedMax, Rnd()),
                Sway = Mathf.Lerp(TT.SwayMin, TT.SwayMax, Rnd()),
                SwayRate = Mathf.Lerp(TT.SwayRateMin, TT.SwayRateMax, Rnd()),
                Phase = Rnd() * 6.283f,
                Twinkle = Mathf.Lerp(TT.TwinkleMin, TT.TwinkleMax, Rnd()),
                Size = Rnd() < 0.12f ? 2 : 1,
                Long = Rnd() < 0.18f,
                C = col,
            };
            f.Life = f.Reach / f.Speed;
            f.T = age * f.Life;
            return f;
        }

        void StepFlakes(float dt)
        {
            if (_flakeTex == null || _flakes == null || _fpx == null) return;
            float now = Time.unscaledTime;
            _gust = Mathf.Max(0f, _gust - dt * TT.GustDecay);
            if (_belowImg != null)
                _belowImg.color = new Color(1f, 1f, 1f, TT.GlowAlpha * (0.75f + 0.25f * Mathf.Sin(now * TT.GlowBreath)));

            System.Array.Clear(_fpx, 0, _fpx.Length);
            for (int i = 0; i < _flakes.Length; i++)
            {
                ref var f = ref _flakes[i];
                f.T += dt * (1f + _gust * TT.GustBoost);
                if (f.T >= f.Life) { f = Spawn(0f); continue; }
                float k = f.T / f.Life;
                float x = f.X0 + Mathf.Sin(f.T * f.SwayRate + f.Phase) * f.Sway;
                float y = f.Y0 + f.T * f.Speed;
                // In fast, out slow; shimmering texel by texel; now and then a white glint.
                float a = Mathf.Min(1f, k * 8f) * Mathf.Pow(1f - k, 0.7f) * (0.72f + 0.28f * Mathf.Sin(now * f.Twinkle + f.Phase));
                bool glint = Mathf.Sin(now * 2.1f + f.Phase * 7f) > 0.985f;
                var c = glint ? new Color32(255, 255, 255, 255) : f.C;
                int px = Mathf.RoundToInt(x), py = Mathf.RoundToInt(y), h = f.Long ? f.Size * 2 : f.Size;
                FlakeRect(px, py, f.Size, h, c, Mathf.Min(1f, a * 1.1f));
            }
            _flakeTex.SetPixels32(_fpx);
            _flakeTex.Apply();
        }

        /// <summary>A block of texels, keeping whichever is stronger where flakes overlap.</summary>
        void FlakeRect(int x0, int y0, int w, int h, Color32 c, float a)
        {
            byte alpha = (byte)(Mathf.Clamp01(a) * 255f);
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                {
                    if (x < 0 || y < 0 || x >= _fw || y >= _fh) continue;
                    int i = y * _fw + x;
                    if (_fpx[i].a >= alpha) continue;
                    _fpx[i] = new Color32(c.r, c.g, c.b, alpha);
                }
        }

        /// <summary>The light below: a cyan glow rising from the bottom centre, violet at its edge.</summary>
        static Texture2D Below()
        {
            if (_below != null) return _below;
            const int W = 128, H = 48;
            var px = new Color[W * H];
            var core = new Color(0.55f, 0.91f, 1f);
            var rim = new Color(0.35f, 0.43f, 1f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                // Round from its centre just under the bottom edge, falling away to nothing at the rect's edge.
                float u = ((x + 0.5f) / W - 0.5f) * 2f, v = (y + 0.5f) / H + 0.04f;
                float r = Mathf.Min(1f, Mathf.Sqrt(u * u + v * v));
                float a = (1f - r) * (1f - r);
                var c = Color.Lerp(core, rim, Mathf.Clamp01(r / 0.6f));
                px[y * W + x] = new Color(c.r, c.g, c.b, a);
            }
            _below = Make(px, W, H, FilterMode.Bilinear);
            return _below;
        }

        void OnDestroy()
        {
            if (_flakeTex != null) Destroy(_flakeTex);
        }

        static Texture2D Make(Color[] px, int w, int h, FilterMode filter)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = filter, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ the frame

        void Update()
        {
            if (!IsOpen || _root == null) return;
            float dt = Time.unscaledDeltaTime;

            // The gold breathes: a slow rise and fall, the glow coming and going with it.
            float b = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / BreathSeconds);
            if (_rings != null) _rings.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.18f, 0.46f, b));
            if (_glow != null) _glow.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.0f, 0.5f, b));
            StepFlakes(dt);

            // The ring: one turn when a choice is taken (eased), a slow turn while a wallet waits.
            if (_spinT >= 0f)
            {
                _spinT += dt / SpinSeconds;
                float k = Mathf.Clamp01(_spinT);
                float eased = k * k * (3f - 2f * k);
                if (_ring != null) _ring.localEulerAngles = new Vector3(0f, 0f, _angle - 360f * eased);
                if (k >= 1f)
                {
                    _spinT = -1f;
                    var a = _answer;
                    _answer = null;
                    a?.TrySetResult(_picked);
                }
                return;
            }
            if (_busy && _ring != null)
            {
                _angle = Mathf.Repeat(_angle - BusyTurn * dt, 360f);
                _ring.localEulerAngles = new Vector3(0f, 0f, _angle);
            }

            Core.Controls.SetFocusCandidates(_buttons);
            if (_busy || _answer == null || Time.frameCount == _openedFrame) return;

            if (Core.Controls.ConfirmTapped && _buttons.Count > 0) { Pick(0); return; }
            if (!Core.Controls.Tapped(out var p)) return;
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i] != null && RectTransformUtility.RectangleContainsScreenPoint(_buttons[i], p, null)) { Pick(Index(i)); return; }
        }

        /// <summary>Buttons are laid in screen order (stacked choices, then the wallets) which may
        /// differ from the options' order; each button's name carries its option index.</summary>
        int Index(int button)
        {
            var n = _buttons[button].name;
            return int.TryParse(n.Substring(n.IndexOf('.') + 1), out int i) ? i : button;
        }

        void Pick(int i)
        {
            if (_spinT >= 0f) return;
            _picked = i;
            _spinT = 0f;
            _gust = 1f;   // the flakes rush up as the ring turns
        }
    }
}
