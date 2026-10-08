using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.StrikeTiming;

namespace Convergence.Combat
{
    /// <summary>
    /// The finisher timing meter: a slim arc standing beside the character, filling from the
    /// bottom up - a basketball shot meter. Four segments run up it (red lead, yellow good,
    /// green perfect, yellow good), and the strike lands as the fill reaches the top. The three
    /// judged segments last the same on every bar; the lead's length follows the finisher's
    /// weight. The meter keeps one height (about Tuning ArcRows), so a longer bar fills slower,
    /// each segment getting rows in proportion to its time - the fill moves at one speed
    /// from bottom to top.
    ///
    /// ON THE SIDE AWAY FROM THE FACING - the off hand. The strike goes the other way, so the
    /// meter never sits under the swing, the slash or the target. The side is chosen when the bar
    /// appears and HELD for its run: the facing keeps tracking through a wind-up, and a meter that
    /// jumped across the body mid-read would be unreadable.
    ///
    /// PURELY A PICTURE. PlayerController owns the one clock that both judges the press and moves
    /// the fill (<see cref="Tick"/>), so what the player sees can never disagree with what they
    /// are scored on.
    ///
    /// Drawn TEXEL BY TEXEL into one small point-filtered texture at body density (37.5 px/unit),
    /// so it sits on the character's own pixel grid - no smoothed curve, no shimmer. The fill
    /// FREEZES where the press landed and stays through the strike and a short linger: that is
    /// the learning tool, it shows exactly how early or late the press was. The segment the press
    /// hit lights in its own colour, the fill's tip included; the green perfect segment is a texel WIDER than the yellow goods, so colour is never the
    /// only channel. Drawn on an OVERLAY order (<see cref="SortingOrders.StrikeBar"/>), so a body
    /// standing in front never hides it and DepthSorted leaves it alone.
    ///
    /// THE STREAK PIPS run up the arc's outer side, two texels clear of its rim: one per
    /// consecutive perfect up to <see cref="T.StreakCap"/>, in groups of five so seven reads as
    /// "a group and two" at a glance. Mint while building, the perfect segment's green (on a slow
    /// pulse) at the cap; an empty slot is a faint dot with no outline, so a pip lighting changes
    /// SHAPE as well as colour. A new pip pops near-white; a broken streak flashes every lit pip
    /// red and drains them top to bottom over the linger. Shown only with the meter - the HUD
    /// counter carries the streak between finishers.
    ///
    /// Domain reload: the texture and renderer are [SerializeField] Object references and
    /// survive; the pixel buffer is a plain, non-readonly array rebuilt on demand (see CLAUDE.md).
    /// </summary>
    public class StrikeBar : MonoBehaviour
    {
        public const string ObjectName = "strike.bar";

        const float Ppu = 37.5f;

        // This bar's rows, set by Begin: the lead's, each judged segment's, and the total.
        int _leadRows = T.ArcRows / 4, _segRows = T.ArcRows / 4;
        float _barSeconds = T.SegmentSeconds * 4f;
        int Rows => _leadRows + 3 * _segRows;
        int Height => Rows + 2;                                          // a rim cap at each end
        static int ArcWidth => T.ArcBowTexels + T.ArcCoreTexels + 1 + 2; // bow + widest core + rims
        static int Width => ArcWidth + PipGap + PipLitWidth + 2;          // + clear gap + pip + rims

        // The streak pips. A pip is PipLitWidth x PipLitRows inside a one-texel rim; neighbours
        // in a group share a rim row, and the groups stand PipGroupGap rows apart.
        const int PipGap = 2;
        const int PipLitWidth = 2;
        const int PipLitRows = 3;
        const int PipGroup = 5;
        const int PipGroupGap = 3;
        static int PipGroupRows => PipGroup * (PipLitRows + 1) + 1;
        static int PipGroups => (T.StreakCap + PipGroup - 1) / PipGroup;
        static int PipColumnRows => PipGroups * PipGroupRows + (PipGroups - 1) * PipGroupGap;

        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] Texture2D _texture;

        Color32[] _pixels;

        enum State { Hidden, Filling, Hit, Failed }
        State _state;

        bool _live;
        int _fillRows;
        int _hitSegment = -1;
        float _flash;
        float _linger;
        float _alpha = 1f;
        bool _dirty;

        int _pips;              // lit pips, 0..StreakCap
        int _popPip = -1;       // the pip that just lit, popping
        float _pipFlash;
        int _brokenPips;        // > 0 while a broken streak flashes and drains

        /// <summary>The meter beside this player, made on first use and found by NAME after
        /// that, so a domain reload re-attaches instead of welding on a second one.</summary>
        public static StrikeBar For(Transform player)
        {
            var found = player.Find(ObjectName);
            if (found != null && found.TryGetComponent(out StrikeBar existing)) return existing;

            var go = new GameObject(ObjectName);
            go.transform.SetParent(player, false);
            var bar = go.AddComponent<StrikeBar>();
            bar.Build();
            return bar;
        }

        void Build()
        {
            _renderer = gameObject.AddComponent<SpriteRenderer>();
            _renderer.sortingOrder = SortingOrders.StrikeBar;
            _renderer.enabled = false;
            Fit();
        }

        /// <summary>(Re)make the texture when this bar's height differs from the last one's.</summary>
        void Fit()
        {
            if (_texture != null && _texture.height == Height && _renderer.sprite != null) return;
            if (_texture != null) Destroy(_texture);
            if (_renderer.sprite != null) Destroy(_renderer.sprite);
            _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "strike.bar",
            };
            _renderer.sprite = Sprite.Create(_texture, new Rect(0, 0, Width, Height),
                                             new Vector2(0.5f, 0.5f), Ppu);
        }

        void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        /// <summary>
        /// Start a fresh, empty meter. <paramref name="facingX"/> is the player's facing: the
        /// meter stands on the OTHER side, and stays there until it is gone.
        /// <paramref name="barSeconds"/> is how long it is on screen before the strike.
        /// </summary>
        public void Begin(float facingX, int streak, float barSeconds)
        {
            float seg = T.SegmentSeconds;
            _barSeconds = Mathf.Max(barSeconds, seg * 4f);
            _segRows = Mathf.Max(1, Mathf.RoundToInt(T.ArcRows * seg / _barSeconds));
            _leadRows = Mathf.Max(1, Mathf.RoundToInt((_barSeconds - T.JudgedSeconds) / seg * _segRows));
            Fit();

            float side = facingX >= 0f ? -1f : 1f;
            // The texture's x = 0 is the arc's inner edge (its ends), and the bow points to +x -
            // so the arc bulges AWAY from the body, and mirroring flips it for the left side.
            transform.localPosition = new Vector3(
                side * (T.ArcSideOffset + Width * 0.5f / Ppu), T.ArcCentreY, 0f);
            transform.localScale = new Vector3(side, 1f, 1f);

            _live = true;
            _state = State.Filling;
            _fillRows = 0;
            _hitSegment = -1;
            _flash = 0f;
            _linger = 0f;
            _alpha = 1f;
            _pips = Mathf.Min(streak, T.StreakCap);
            _popPip = -1;
            _pipFlash = 0f;
            _brokenPips = 0;
            _renderer.enabled = true;
            _dirty = true;
        }

        /// <summary>A perfect connected: light the next pip. Ignored once the meter has gone -
        /// a thrown finisher can connect after it - since the HUD counter says it too.</summary>
        public void StreakGained(int streak)
        {
            if (_renderer == null || !_renderer.enabled) return;
            _brokenPips = 0;
            if (streak <= T.StreakCap)
            {
                _pips = streak;
                _popPip = streak - 1;
                _pipFlash = 1f;
            }
            _dirty = true;
        }

        /// <summary>The streak broke: every lit pip flashes red, then drains over the linger.</summary>
        public void StreakBroken(int was)
        {
            if (_renderer == null || !_renderer.enabled) return;
            _brokenPips = Mathf.Min(was, T.StreakCap);
            _pips = 0;
            _popPip = -1;
            _dirty = true;
        }

        /// <summary>Advance the fill. <paramref name="untilStrike"/> is the controller's clock,
        /// so drawing and judging agree.</summary>
        public void Tick(float untilStrike)
        {
            if (!_live || _state != State.Filling) return;
            SetFill(untilStrike);
        }

        /// <summary>
        /// The first tap: freeze the fill where it landed. A hit lights the segment it landed in;
        /// an early press turns the fill red and the zones dark.
        /// </summary>
        public void Press(float untilStrike, StrikeVerdict verdict)
        {
            if (!_live) return;
            SetFill(untilStrike);
            if (verdict == StrikeVerdict.Early || verdict == StrikeVerdict.Missed)
            {
                _state = State.Failed;
            }
            else
            {
                _state = State.Hit;
                float seg = T.SegmentSeconds;
                _hitSegment = untilStrike > seg * 2f ? 1 : untilStrike > seg ? 2 : 3;
                _flash = 1f;
            }
            _dirty = true;
        }

        /// <summary>The strike landed: hold the result for a moment, then fade.</summary>
        public void Settle(StrikeVerdict verdict)
        {
            if (!_live) return;
            _live = false;
            _linger = T.LingerSeconds;
            if (verdict == StrikeVerdict.Missed)
            {
                _fillRows = Rows;   // ran out the top with nothing pressed
                _state = State.Failed;
            }
            if (verdict == StrikeVerdict.Perfect)
                transform.localScale = new Vector3(transform.localScale.x * 1.15f, 1.12f, 1f);
            _dirty = true;
        }

        /// <summary>The finisher never reached its strike (the run ended, the player died).</summary>
        public void Cancel()
        {
            _live = false;
            _linger = 0f;
            _state = State.Hidden;
            if (_renderer != null) _renderer.enabled = false;
        }

        /// <summary>Rows filled with <paramref name="untilStrike"/> left - piecewise, lead then
        /// the judged segments, so the fill crosses each boundary exactly when the judge does
        /// (the two rates differ by under a row's worth from rounding).</summary>
        void SetFill(float untilStrike)
        {
            float judged = T.JudgedSeconds;
            float lead = _barSeconds - judged;
            float f = untilStrike > judged
                ? (1f - (untilStrike - judged) / lead) * _leadRows
                : _leadRows + (1f - untilStrike / judged) * 3 * _segRows;
            int rows = Mathf.Clamp(Mathf.CeilToInt(f), 0, Rows);
            if (rows == _fillRows) return;
            _fillRows = rows;
            _dirty = true;
        }

        /// <summary>0 lead, 1 good, 2 perfect, 3 good - bottom to top.</summary>
        int SegmentOf(int r) => r < _leadRows ? 0 : 1 + Mathf.Min(2, (r - _leadRows) / _segRows);

        static Color SegmentColor(int segment) => segment switch
        {
            0 => T.Lead,
            2 => T.Perfect,
            _ => T.Good,
        };

        /// <summary>Where row <paramref name="r"/>'s inner rim sits: a parabola through the
        /// arc's two ends, bowing ArcBowTexels out at the middle.</summary>
        int BowAt(int r)
        {
            float half = Rows * 0.5f;
            float u = (r + 0.5f - half) / half;
            return Mathf.RoundToInt(T.ArcBowTexels * (1f - u * u));
        }

        /// <summary>The first texel past row <paramref name="r"/>'s outer rim.</summary>
        int OuterAt(int r)
        {
            r = Mathf.Clamp(r, 0, Rows - 1);
            int core = T.ArcCoreTexels + (SegmentOf(r) == 2 ? 1 : 0);
            return BowAt(r) + core + 2;
        }

        Color PipColor(int i)
        {
            if (_brokenPips > 0)
            {
                // Drains top to bottom as the linger runs out.
                float left = _live ? 1f : Mathf.Clamp01(_linger / T.LingerSeconds);
                return i < Mathf.CeilToInt(_brokenPips * left) ? T.PipBroken : Color.clear;
            }
            if (i >= _pips) return Color.clear;

            Color c = _pips >= T.StreakCap
                ? Bright(T.Perfect, 1f + 0.3f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f)))
                : T.PipLit;
            return i == _popPip ? Color.Lerp(c, T.PipPop, _pipFlash) : c;
        }

        void PaintPips()
        {
            int y0 = (Height - PipColumnRows) / 2;
            for (int i = 0; i < T.StreakCap; i++)
            {
                int bottom = y0 + (i / PipGroup) * (PipGroupRows + PipGroupGap) + (i % PipGroup) * (PipLitRows + 1);
                int top = bottom + PipLitRows + 1;

                // Follow the arc's curve: clear the widest of the rows this pip stands beside.
                int x = 0;
                for (int y = bottom; y <= top; y++) x = Mathf.Max(x, OuterAt(y - 1));
                x += PipGap;

                Color lit = PipColor(i);
                bool on = lit.a > 0f;
                for (int y = bottom; y <= top; y++)
                    for (int dx = 0; dx < PipLitWidth + 2; dx++)
                    {
                        bool inner = dx >= 1 && dx <= PipLitWidth && y > bottom && y < top;
                        if (on) Put(x + dx, y, inner ? lit : T.Track);
                        else if (inner) Put(x + dx, y, T.PipEmpty);   // a faint dot, no outline
                    }
            }
        }

        void Repaint()
        {
            int w = Width, h = Height;
            if (_pixels == null || _pixels.Length != w * h) _pixels = new Color32[w * h];
            System.Array.Clear(_pixels, 0, _pixels.Length);

            Color rim = T.Track;
            for (int r = 0; r < Rows; r++)
            {
                int segment = SegmentOf(r);
                int core = T.ArcCoreTexels + (segment == 2 ? 1 : 0);
                int x0 = BowAt(r);
                int y = r + 1;

                Color lit = SegmentColor(segment);
                Color c;
                bool filled = r < _fillRows;
                if (_state == State.Failed)
                    c = filled ? T.Missed : Dim(lit, T.UnlitBrightness * 0.6f);
                else if (_state == State.Hit && segment == _hitSegment)
                    // The whole segment the press hit - the fill's tip included - lights in ITS
                    // colour, so the tip itself says good or perfect. Brightened by the flash, never
                    // washed to white: white is the fill, and a white flash read as more fill.
                    c = Bright(lit, 1f + 0.45f * _flash);
                else if (filled)
                    c = T.Fill;
                else
                    c = Dim(lit, T.UnlitBrightness);

                Put(x0, y, rim);
                for (int i = 1; i <= core; i++) Put(x0 + i, y, c);
                Put(x0 + core + 1, y, rim);

                // Caps close the two ends.
                if (r == 0 || r == Rows - 1)
                {
                    int capY = r == 0 ? 0 : h - 1;
                    for (int i = 0; i <= core + 1; i++) Put(x0 + i, capY, rim);
                }
            }

            PaintPips();

            if (_alpha < 1f)
                for (int i = 0; i < _pixels.Length; i++)
                    _pixels[i].a = (byte)(_pixels[i].a * _alpha);

            _texture.SetPixels32(_pixels);
            _texture.Apply(false);
            _dirty = false;
        }

        void Put(int x, int y, Color c)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return;
            _pixels[y * Width + x] = c;
        }

        static Color Dim(Color c, float k) => new(c.r * k, c.g * k, c.b * k, c.a);

        static Color Bright(Color c, float k)
            => new(Mathf.Min(1f, c.r * k), Mathf.Min(1f, c.g * k), Mathf.Min(1f, c.b * k), c.a);

        void Update()
        {
            if (_renderer == null || !_renderer.enabled) return;
            float dt = Time.deltaTime;

            // The segment the press hit flares bright and settles to its own colour.
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - dt / 0.16f);
                _dirty = true;
            }

            if (_pipFlash > 0f)
            {
                _pipFlash = Mathf.Max(0f, _pipFlash - dt / 0.16f);
                _dirty = true;
            }
            if (_pips >= T.StreakCap || _brokenPips > 0) _dirty = true;   // the cap's pulse, the drain

            // A perfect strike pops the meter and lets it settle back to size.
            var s = transform.localScale;
            var target = new Vector3(Mathf.Sign(s.x), 1f, 1f);
            if ((s - target).sqrMagnitude > 0.00001f)
                transform.localScale = Vector3.Lerp(s, target, 1f - Mathf.Exp(-18f * dt));

            if (!_live && _linger > 0f)
            {
                _linger -= dt;
                _alpha = Mathf.Clamp01(_linger / (T.LingerSeconds * 0.5f));
                _dirty = true;
                if (_linger <= 0f)
                {
                    _renderer.enabled = false;
                    _state = State.Hidden;
                    return;
                }
            }

            if (_dirty) Repaint();
        }
    }
}
