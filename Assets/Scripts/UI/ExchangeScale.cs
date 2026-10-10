using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Exchange;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.UI
{
    /// <summary>
    /// One slate's balance on the deal screen: a bronze scale of justice, held from the top, its
    /// beam tipped by the boon's weight against the cost's. The boon pan is on the LEFT and sinks
    /// on a bargain; each pan holds its entry's own icon, so the scale says the whole trade and no
    /// verdict text is needed (the user's call, 2026-10-10).
    ///
    /// Drawn texel by texel into one point-filtered texture, redrawn whenever the beam moves -
    /// the handle and fork never move, everything below the pivot is re-plotted, so the motion
    /// steps by whole texels like the rest of the game's pixel art. Unscaled time: the deal holds
    /// the pause.
    ///
    ///   - swings in from level with an overshoot, each slate a beat after the last; an even trade
    ///     wobbles in; an EMPTY boon pan (Debt) slams past the stops and bounces off them
    ///   - sways while hovered or focused (the pad's focus cue too)
    ///   - copies already carried stand dimmer behind the new icon; the stack that completes a
    ///     capstone glows - gilt-crimson for a Rubedo, black smoke and ash for a Nigredo (a dark
    ///     glow is invisible on a dark card)
    ///   - a Citrinitas lights the beam yellow
    ///   - REFUSE is the beam lifted off the fork and laid on the floor: no trade
    ///   - taking the slate freezes the beam, the icons leave, and the empty pans spring level
    /// </summary>
    public class ExchangeScale : MonoBehaviour
    {
        public const int W = 82, H = 56, IconTexels = 17;
        const int Cx = 41, Cy = 16, Arm = 30, Drop = 16;

        static readonly Color32 Clear = new(0, 0, 0, 0);
        static Color32 C(uint rgb) => new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        struct Ramp { public Color32 Hi, Lt, Md, Dk, Sh; }
        struct IconRamp { public Color32 Lt, Md, Dk, Ghost, GhostDk; }

        static readonly Ramp Bronze = new() { Hi = C(0xf2cf8c), Lt = C(0xc8914f), Md = C(0x8f5d2e), Dk = C(0x573519), Sh = C(0x2c1a0b) };
        static readonly Ramp Citrine = new() { Hi = C(0xfff3a6), Lt = C(0xecca4c), Md = C(0xb08f22), Dk = C(0x6a5410), Sh = C(0x3a2e08) };
        static readonly IconRamp BoonInk = new() { Lt = C(0xb4e384), Md = C(0x8cc25b), Dk = C(0x4c772a), Ghost = C(0x33482a), GhostDk = C(0x22301d) };
        static readonly IconRamp CostInk = new() { Lt = C(0xf2916c), Md = C(0xd8633e), Dk = C(0x86341b), Ghost = C(0x4d2a20), GhostDk = C(0x331c15) };
        static readonly Color32[] RubedoGlow = { C(0xffd27a), C(0xffa04a), C(0xe8583a), C(0xffa04a) };
        static readonly Color32 Smoke = C(0x030204), Ash = C(0xbdb2bd), AshDim = C(0x7e7480), Floor = C(0x141218);

        /// <summary>The Rubedo and Nigredo lines' text colours, matched to the glows.</summary>
        public static readonly Color RubedoText = new(1f, 0.69f, 0.44f);
        public static readonly Color NigredoText = new(0.74f, 0.70f, 0.74f);

        [SerializeField] RawImage _image;
        [SerializeField] Texture2D _texture;
        Color32[] _px;

        // Plain C# references: a domain reload nulls them, and Update then draws nothing rather
        // than throwing (the screen it sits on guards the same way).
        ExchangeEntry _boon, _cost;
        int _boonHeld, _costHeld;
        bool _refused, _boonCaps, _costCaps;
        float _shownAt, _delay;
        int _seed;

        bool _taken;
        float _takenAt, _takenAngle;

        /// <summary>Hovered or focused - sways while true.</summary>
        public bool Focused;

        float _drawnAngle = float.NaN;
        int _drawnFrame = -1;
        bool _drawnTaken;

        Vector2Int[] _pans = new Vector2Int[2];
        Vector2Int[] _iconAt = new Vector2Int[2];
        RectTransform[] _tags = new RectTransform[2];

        static float Units => T.ScaleUnitsPerTexel;

        // ---------------------------------------------------------------- building

        /// <summary>A scale whose top-left corner sits at <paramref name="topLeft"/> in
        /// <paramref name="parent"/>'s top-left-anchored space.</summary>
        public static ExchangeScale Create(RectTransform parent, Vector2 topLeft)
        {
            var rt = UiKit.Rect(parent, "scale", new Vector2(0, 1), new Vector2(0, 1),
                Vector2.zero, Vector2.zero);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = new Vector2(W * Units, H * Units);

            var s = rt.gameObject.AddComponent<ExchangeScale>();
            s._image = rt.gameObject.AddComponent<RawImage>();
            s._image.raycastTarget = false;
            s._texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            s._image.texture = s._texture;
            return s;
        }

        /// <param name="boon">Null on a Debt slate - the pan stays empty and the beam slams.</param>
        /// <param name="slot">Position in the row: staggers the swing, and picks the wobble's side.</param>
        public void Show(ExchangeEntry boon, int boonHeld, ExchangeEntry cost, int costHeld, int slot)
        {
            _boon = boon; _cost = cost;
            _boonHeld = boonHeld; _costHeld = costHeld;
            _boonCaps = Completes(boon, boonHeld);
            _costCaps = Completes(cost, costHeld);
            Begin(slot);
        }

        public void ShowRefused(int slot)
        {
            _refused = true;
            Begin(slot);
        }

        void Begin(int slot)
        {
            _seed = slot;
            _delay = slot * T.ScaleStaggerSeconds;
            _shownAt = Time.unscaledTime;
            _drawnAngle = float.NaN;
            Redraw(0f, 0);
        }

        /// <summary>Taking this slate stack would complete the entry's capstone.</summary>
        public static bool Completes(ExchangeEntry e, int held)
            => e != null && e.CapName != null && held + 1 >= e.MaxStacks;

        /// <summary>A tag (RETURNING, NEW, MERCY) to hang under one pan and follow it: 0 the boon's,
        /// 1 the cost's. Its pivot should be its top centre.</summary>
        public void Hang(RectTransform tag, int side)
        {
            tag.SetParent(transform, false);
            tag.anchorMin = tag.anchorMax = new Vector2(0, 1);
            tag.pivot = new Vector2(0.5f, 1f);
            _tags[side] = tag;
            PlaceTags();
        }

        // ---------------------------------------------------------------- taking

        /// <summary>The slate was taken: the beam holds, the pans empty, and it springs level.</summary>
        public void Take()
        {
            if (_taken) return;
            _takenAngle = Angle(Time.unscaledTime);
            _taken = true;
            _takenAt = Time.unscaledTime;
            foreach (var tag in _tags) if (tag) tag.gameObject.SetActive(false);
        }

        /// <summary>Where a pan's icon is centred, in world space - the flight starts here.</summary>
        public Vector3 IconWorldCentre(int side)
        {
            var at = _iconAt[side];
            var local = new Vector2((at.x + IconTexels * 0.5f) * Units, -(at.y + IconTexels * 0.5f) * Units);
            return transform.TransformPoint(local);
        }

        // ---------------------------------------------------------------- motion

        float StepRadians => T.ScaleTiltDegrees * Mathf.Deg2Rad;

        float Target
        {
            get
            {
                if (_boon == null) return -Mathf.Min(T.ScaleSlamDegrees, (T.ScaleMaxSteps + 0.5f) * T.ScaleTiltDegrees) * Mathf.Deg2Rad;
                int d = Mathf.Clamp(_boon.Weight - _cost.Weight, -T.ScaleMaxSteps, T.ScaleMaxSteps);
                return d * StepRadians;
            }
        }

        float Angle(float now)
        {
            if (_refused || _cost == null) return 0f;
            float t = now - _shownAt - _delay;
            float target = Target, th;

            if (_boon == null)
                // The slam: bounces off the stop, never swings back past it.
                th = t <= 0f ? 0f : target * (1f - Mathf.Exp(-t / 0.35f) * Mathf.Abs(Mathf.Cos(11f * t)));
            else
            {
                float th0 = target == 0f ? (_seed % 2 == 1 ? 1f : -1f) * 0.8f * StepRadians : 0f;
                th = t <= 0f ? th0 : target + (th0 - target) * Mathf.Exp(-t / 0.55f) * Mathf.Cos(7.5f * t);
            }
            if (Focused) th += 0.3f * StepRadians * Mathf.Sin(2.6f * now);
            return th;
        }

        void Update()
        {
            if (_texture == null || (!_refused && _cost == null)) return;
            float now = Time.unscaledTime, th;
            if (_taken)
            {
                float tp = now - _takenAt - T.TakeLiftSeconds;
                th = tp <= 0f ? _takenAngle : _takenAngle * Mathf.Exp(-tp / 0.3f) * Mathf.Cos(10f * tp);
            }
            else th = Angle(now);

            int frame = !_taken && (_boonCaps || _costCaps) ? Mathf.FloorToInt(now * 6f) : 0;
            if (Mathf.Abs(th - _drawnAngle) < 0.0005f && frame == _drawnFrame && _taken == _drawnTaken) return;
            Redraw(th, frame);
        }

        // ---------------------------------------------------------------- drawing

        void Redraw(float th, int frame)
        {
            _drawnAngle = th; _drawnFrame = frame; _drawnTaken = _taken;
            if (_px == null || _px.Length != W * H) _px = new Color32[W * H];
            for (int i = 0; i < _px.Length; i++) _px[i] = Clear;

            if (_refused) DrawRefused();
            else DrawBalance(th, frame);

            _texture.SetPixels32(_px);
            _texture.Apply(false);
            PlaceTags();
        }

        void Put(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return;
            _px[(H - 1 - y) * W + x] = c;
        }

        void Put(float x, float y, Color32 c) => Put(Mathf.RoundToInt(x), Mathf.RoundToInt(y), c);

        void Row(int x0, int x1, int y, Color32 c) { for (int x = x0; x <= x1; x++) Put(x, y, c); }

        /// <summary>A line whose texels alternate, so it reads as links of chain.</summary>
        void Chain(int x0, int y0, int x1, int y1, Color32 a, Color32 b)
        {
            int dx = Mathf.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1, dy = -Mathf.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int e = dx + dy, n = 0;
            while (true)
            {
                Put(x0, y0, n++ % 2 == 1 ? b : a);
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * e;
                if (e2 >= dy) { e += dy; x0 += sx; }
                if (e2 <= dx) { e += dx; y0 += sy; }
            }
        }

        void Dish(int hx, int py)
        {
            Row(hx - 11, hx + 10, py, Bronze.Hi);
            Row(hx - 10, hx + 9, py + 1, Bronze.Lt);
            Row(hx - 8, hx + 7, py + 2, Bronze.Md);
            Row(hx - 4, hx + 3, py + 3, Bronze.Dk);
        }

        void Handle()
        {
            Put(Cx, 0, Bronze.Hi);
            Row(Cx - 1, Cx + 1, 1, Bronze.Hi); Row(Cx - 2, Cx + 2, 2, Bronze.Lt);
            Row(Cx - 2, Cx + 2, 3, Bronze.Md); Row(Cx - 1, Cx + 1, 4, Bronze.Dk);
            for (int y = 5; y <= 8; y++) { Put(Cx - 1, y, Bronze.Lt); Put(Cx, y, Bronze.Md); Put(Cx + 1, y, Bronze.Dk); }
            Row(Cx - 2, Cx + 2, 6, Bronze.Lt);
            Row(Cx - 2, Cx + 2, 9, Bronze.Lt); Row(Cx - 4, Cx + 4, 10, Bronze.Md);
        }

        void Fork()
        {
            for (int y = 11; y <= Cy; y++) { Put(Cx - 4, y, Bronze.Lt); Put(Cx + 4, y, Bronze.Dk); }
        }

        void DrawBalance(float th, int frame)
        {
            float c = Mathf.Cos(th), s = Mathf.Sin(th);
            // u along the beam (right +), v across it (down +).
            Vector2 At(float u, float v) => new(Cx + u * c + v * s, Cy - u * s + v * c);
            void Plot(float u, float v, Color32 col) { var p = At(u, v); Put(p.x, p.y, col); }

            var beam = _boon != null && _boon.Origin == EntryOrigin.Citrinitas ? Citrine : Bronze;
            Handle();

            for (int side = 0; side < 2; side++)
            {
                int sg = side == 0 ? -1 : 1;
                var h = At(sg * Arm, 3);
                int hx = Mathf.RoundToInt(h.x), hy = Mathf.RoundToInt(h.y), py = hy + Drop;
                Plot(sg * Arm, 1, beam.Md); Plot(sg * Arm, 2, beam.Lt);
                Chain(hx, hy, hx - 11, py - 1, Bronze.Lt, Bronze.Dk);
                Chain(hx, hy, hx + 10, py - 1, Bronze.Lt, Bronze.Dk);
                Chain(hx, hy, hx, py - 1, Bronze.Md, Bronze.Sh);
                Dish(hx, py);
                _pans[side] = new Vector2Int(hx, py);

                var e = side == 0 ? _boon : _cost;
                if (e == null) continue;
                var g = ExchangeGlyphs.Texels(e, IconTexels);
                int left = hx - 8, top = py - 1 - Bottom(g);
                _iconAt[side] = new Vector2Int(left, top);
                if (_taken) continue;

                var ink = e.Kind == ExchangeKind.Boon ? BoonInk : CostInk;
                int held = side == 0 ? _boonHeld : _costHeld;
                if (held >= 1) Icon(g, left - 3, top - 2, ink, true);
                if (held >= 2) Icon(g, left + 3, top - 2, ink, true);
                if (side == 0 ? _boonCaps : _costCaps)
                {
                    if (e.Kind == ExchangeKind.Boon) Glow(g, left, top, RubedoGlow[frame % 4]);
                    else SmokeHalo(g, left, top, frame);
                }
                Icon(g, left, top, ink, false);
            }

            // Beam: tapers toward the ends, scrolled crest at the middle, a curl up at each end.
            for (float u = -Arm; u <= Arm; u += 0.5f) { Plot(u, 0, beam.Lt); if (Mathf.Abs(u) < Arm - 4) Plot(u, 1, beam.Dk); }
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Plot(sg * 3, -1, beam.Hi); Plot(sg * 4, -2, beam.Hi); Plot(sg * 5, -3, beam.Hi); Plot(sg * 6, -3, beam.Hi);
                Plot(sg * 7, -2, beam.Lt); Plot(sg * 7, -1, beam.Lt); Plot(sg * 6, -1, beam.Lt);
                Plot(sg * Arm, -1, beam.Lt); Plot(sg * (Arm - 1), -2, beam.Lt);
                Plot(sg * (Arm - 2), -2, beam.Lt); Plot(sg * (Arm - 3), -1, beam.Lt);
                Plot(sg * Arm, 0, beam.Hi);
            }
            // The needle stands up from the pivot and leans with the beam; the fork's arms frame it.
            for (int k = 1; k <= 6; k++) Plot(0, -k, k > 4 ? beam.Hi : beam.Lt);
            Fork();
            Row(Cx - 1, Cx + 1, Cy, Bronze.Md); Put(Cx, Cy, Bronze.Hi);
        }

        void DrawRefused()
        {
            Handle();
            Fork();
            // The beam lies on the floor, its curls up; the two empty pans beside it, chain slack.
            Row(Cx - 18, Cx + 18, 50, Bronze.Lt); Row(Cx - 18, Cx + 18, 51, Bronze.Dk);
            Put(Cx - 19, 49, Bronze.Lt); Put(Cx - 20, 48, Bronze.Lt); Put(Cx + 19, 49, Bronze.Lt); Put(Cx + 20, 48, Bronze.Lt);
            for (int sg = -1; sg <= 1; sg += 2)
            {
                Put(Cx + sg * 5, 49, Bronze.Hi); Put(Cx + sg * 6, 48, Bronze.Hi);
                Put(Cx + sg * 7, 47, Bronze.Hi); Put(Cx + sg * 8, 47, Bronze.Hi);
            }
            for (int k = 0; k < 7; k++) Put(Cx - 3 + k, 47 - (k % 2), Bronze.Lt);
            Row(1, 18, 52, Bronze.Hi); Row(2, 17, 53, Bronze.Lt); Row(4, 15, 54, Bronze.Md);
            Row(64, 81, 52, Bronze.Hi); Row(65, 80, 53, Bronze.Lt); Row(67, 78, 54, Bronze.Md);
            for (int k = 0; k < 5; k++)
            {
                Put(19 + k, 54 - (k % 2), k % 2 == 1 ? Bronze.Dk : Bronze.Lt);
                Put(58 + k, 54 - (k % 2), k % 2 == 1 ? Bronze.Dk : Bronze.Lt);
            }
            Row(0, W - 1, 55, Floor);
        }

        static int Bottom(bool[,] g)
        {
            int n = g.GetLength(0), bottom = 0;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                if (g[x, y]) bottom = y;
            return bottom;
        }

        static bool On(bool[,] g, int x, int y)
        {
            int n = g.GetLength(0);
            return x >= 0 && y >= 0 && x < n && y < n && g[x, y];
        }

        /// <summary>The glyph sitting in the pan: lit along its top edges, shaded on its lower and
        /// right edges, so it reads as an object rather than a flat stamp.</summary>
        void Icon(bool[,] g, int left, int top, IconRamp ink, bool dim)
        {
            int n = g.GetLength(0);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (!g[x, y]) continue;
                bool shade = !On(g, x, y + 1) || !On(g, x + 1, y);
                var col = dim ? (shade ? ink.GhostDk : ink.Ghost)
                              : shade ? ink.Dk : !On(g, x, y - 1) ? ink.Lt : ink.Md;
                Put(left + x, top + y, col);
            }
        }

        void Glow(bool[,] g, int left, int top, Color32 col)
        {
            int n = g.GetLength(0);
            for (int y = -1; y <= n; y++)
            for (int x = -1; x <= n; x++)
                if (!On(g, x, y) && (On(g, x - 1, y) || On(g, x + 1, y) || On(g, x, y - 1) || On(g, x, y + 1)))
                    Put(left + x, top + y, col);
        }

        /// <summary>The Nigredo can't glow on a dark card, so it is shown as darkness: a ring of
        /// black smoke, with flecks of pale ash flickering at its edge.</summary>
        void SmokeHalo(bool[,] g, int left, int top, int frame)
        {
            int n = g.GetLength(0);
            for (int y = -3; y <= n + 2; y++)
            for (int x = -3; x <= n + 2; x++)
            {
                if (On(g, x, y)) continue;
                int best = 9;
                for (int j = -3; j <= 3; j++)
                for (int i = -3; i <= 3; i++)
                    if (On(g, x + i, y + j)) best = Mathf.Min(best, Mathf.Max(Mathf.Abs(i), Mathf.Abs(j)));
                if (best > 3) continue;
                int h = ((x * 7 + y * 13 + frame * 5) % 11 + 11) % 11;
                if (best == 1) Put(left + x, top + y, Smoke);
                else if (best == 2) Put(left + x, top + y, h < 2 ? Ash : Smoke);
                else if (h == 0) Put(left + x, top + y, AshDim);
            }
        }

        void PlaceTags()
        {
            for (int side = 0; side < 2; side++)
            {
                var tag = _tags[side];
                if (!tag) continue;
                var pan = _pans[side];
                tag.anchoredPosition = new Vector2((pan.x + 0.5f) * Units, -(pan.y + 4) * Units);
            }
        }

        // ---------------------------------------------------------------- icons outside the pan

        static Dictionary<string, Texture2D> _iconCache = new();

        /// <summary>An entry's icon as its own little texture, shaded as it is in the pan - for
        /// the card's columns and the flight to the ledger strip, so the three always match.</summary>
        public static Texture2D IconTexture(ExchangeEntry e)
        {
            _iconCache ??= new Dictionary<string, Texture2D>();
            if (_iconCache.TryGetValue(e.Id, out var cached) && cached != null) return cached;

            var g = ExchangeGlyphs.Texels(e, IconTexels);
            var ink = e.Kind == ExchangeKind.Boon ? BoonInk : CostInk;
            var px = new Color32[IconTexels * IconTexels];
            for (int y = 0; y < IconTexels; y++)
            for (int x = 0; x < IconTexels; x++)
            {
                if (!g[x, y]) { px[(IconTexels - 1 - y) * IconTexels + x] = Clear; continue; }
                bool shade = !On(g, x, y + 1) || !On(g, x + 1, y);
                px[(IconTexels - 1 - y) * IconTexels + x] = shade ? ink.Dk : !On(g, x, y - 1) ? ink.Lt : ink.Md;
            }
            var tex = new Texture2D(IconTexels, IconTexels, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply(false);
            _iconCache[e.Id] = tex;
            return tex;
        }

        void OnDestroy()
        {
            if (_texture) Destroy(_texture);
        }
    }
}
