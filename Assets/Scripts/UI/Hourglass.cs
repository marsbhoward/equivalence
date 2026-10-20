using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// THE HOURGLASS - the run clock's readout in the Short Descent (SHORT-DESCENT.md, "The
    /// hourglass"; Core.RunClock). Not a number in a corner, and not always on screen - the user's
    /// calls: it APPEARS, so the fight stays uncluttered, and what it shows is what a glass shows.
    ///
    ///     every ten-minute mark   it stands, the sand takes the new tier's colour, holds, fades
    ///     ~30 s before a mark     a brief warning - a player can race it
    ///     at every clear          laid on its SIDE with the menus (a glass on its side stops its
    ///                             sand - and so does the run clock); set upright as the next floor
    ///                             begins, then fades
    ///     the GATE COLLAPSE       at the hour the glass FLIPS and its sand turns ember; it shows
    ///                             again at every five-minute step
    ///
    /// The sand in the glass is the current tier's ten minutes: the top bulb what is left of it, the
    /// bottom what is spent. Its colour warms tier by tier, pale to ember (RunClock.TierName names
    /// them). Beneath it, the clock as m:ss and the tier's name. Texel-drawn into one point-filtered
    /// texture, the element seal's way; unscaled time, as the menus stop scaled time.
    /// </summary>
    public class Hourglass : MonoBehaviour
    {
        const int W = 24, H = 36;
        public const float UnitsPerTexel = 3f;

        const float ShowSeconds = 4f, WarnSeconds = 2.5f, FadeSeconds = 0.6f, WarnBefore = 30f;
        const float FlipSeconds = 0.9f;

        static readonly Color32 Clear = new(0, 0, 0, 0);
        static readonly Color32 Outline = new(0x0b, 0x0a, 0x0e, 255);
        static readonly Color32 Wood = new(0x6b, 0x4a, 0x2a, 255);
        static readonly Color32 WoodLit = new(0x9a, 0x6e, 0x3e, 255);
        static readonly Color32 Glass = new(0x1d, 0x22, 0x2c, 200);
        static readonly Color32 GlassRim = new(0x9a, 0xb2, 0xc4, 255);
        static readonly Color32 Shine = new(0xd8, 0xe8, 0xf2, 255);

        /// <summary>Sand by tier: Calcination's pale through Distillation's red; then ember.</summary>
        static readonly Color32[] Sand =
        {
            new(0xe8, 0xdc, 0xc0, 255), new(0xe8, 0xd0, 0x8c, 255), new(0xe8, 0xb4, 0x5c, 255),
            new(0xe8, 0x90, 0x3c, 255), new(0xd8, 0x64, 0x2c, 255), new(0xc0, 0x40, 0x2a, 255),
            new(0xff, 0x5a, 0x22, 255),
        };

        [SerializeField] RawImage _image;
        [SerializeField] Texture2D _texture;
        [SerializeField] RectTransform _glass;
        [SerializeField] CanvasGroup _group;
        [SerializeField] Text _label;

        Color32[] _px;
        int _lastTier = -1, _lastCollapseStep = -1, _warnedFor = -1;
        float _showUntil = -10f, _shownAt = -10f, _flipAt = -10f;
        bool _wasOpen;
        float _alpha;

        public static Hourglass Build(RectTransform rt)
        {
            var h = rt.gameObject.AddComponent<Hourglass>();
            h._group = rt.gameObject.AddComponent<CanvasGroup>();
            h._group.alpha = 0f;
            h._group.blocksRaycasts = false;
            h._group.interactable = false;

            h._glass = UiKit.Rect(rt, "glass", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-W * UnitsPerTexel * 0.5f, -H * UnitsPerTexel), new Vector2(W * UnitsPerTexel * 0.5f, 0f));
            h._image = h._glass.gameObject.AddComponent<RawImage>();
            h._image.raycastTarget = false;
            h._texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "run.hourglass",
            };
            h._image.texture = h._texture;

            var labelRt = UiKit.Rect(rt, "label", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-150f, -H * UnitsPerTexel - 58f), new Vector2(150f, -H * UnitsPerTexel - 6f));
            h._label = UiKit.Label(labelRt, "", 20, new Color(0.92f, 0.86f, 0.72f), TextAnchor.UpperCenter);
            var o = h._label.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.04f, 0.04f, 0.06f, 0.9f);
            o.effectDistance = new Vector2(2f, -2f);
            return h;
        }

        void Update()
        {
            var clock = RunClock.Current;
            if (clock == null || !RunFormat.Short) { _group.alpha = 0f; return; }

            float now = Time.unscaledTime;
            float seconds = clock.Seconds;
            int tier = RunClock.TierAt(seconds);
            bool collapse = RunClock.InCollapse(seconds);
            int collapseStep = collapse ? Mathf.FloorToInt((seconds - Tuning.RunClock.CollapseAtSeconds) / Tuning.RunClock.CollapseStepSeconds) : -1;

            // A new tier, or a new step of the Gate Collapse: it stands and shows. The first look
            // (the run's start, a resume) only remembers where the clock is.
            if (_lastTier < 0) { _lastTier = tier; _lastCollapseStep = collapseStep; }
            if (tier != _lastTier)
            {
                if (tier >= Tuning.RunClock.Tiers && _lastTier < Tuning.RunClock.Tiers) _flipAt = now;
                _lastTier = tier;
                Show(now, ShowSeconds);
            }
            if (collapseStep != _lastCollapseStep)
            {
                _lastCollapseStep = collapseStep;
                if (collapseStep > 0) Show(now, ShowSeconds);
            }

            // The warning: a moment before the next mark, while a floor is being fought.
            float next = collapse
                ? Tuning.RunClock.CollapseAtSeconds + (collapseStep + 1) * Tuning.RunClock.CollapseStepSeconds
                : (tier + 1) * Tuning.RunClock.TierSeconds;
            int markIndex = Mathf.RoundToInt(next);
            if (clock.AttemptOpen && next - seconds <= WarnBefore && next - seconds > WarnBefore - 1f && _warnedFor != markIndex)
            {
                _warnedFor = markIndex;
                Show(now, WarnSeconds);
            }

            // On its side while the clock is stopped (a cleared floor, its menus); upright again as
            // the next floor begins, shown a moment more.
            bool open = clock.AttemptOpen;
            if (_wasOpen && !open) Show(now, 0f);
            if (!_wasOpen && open && _showUntil < now + 1.5f) _showUntil = now + 1.5f;
            _wasOpen = open;

            bool visible = !open || now < _showUntil;
            float target = visible ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, target, Time.unscaledDeltaTime / FadeSeconds);
            _group.alpha = _alpha;
            if (_alpha <= 0f) return;

            // Lying down, standing up, and the flip into the Gate Collapse (a half turn).
            float lie = open ? 0f : 90f;
            float flipK = Mathf.Clamp01((now - _flipAt) / FlipSeconds);
            float flip = flipK < 1f ? Mathf.SmoothStep(0f, 180f, flipK) : 0f;
            float pop = 1f + 0.12f * Mathf.Max(0f, 1f - (now - _shownAt) / 0.35f);
            _glass.localRotation = Quaternion.Euler(0f, 0f, lie + flip);
            _glass.localScale = Vector3.one * pop;

            float tierSpent = collapse
                ? (seconds - Tuning.RunClock.CollapseAtSeconds) % Tuning.RunClock.CollapseStepSeconds / Tuning.RunClock.CollapseStepSeconds
                : seconds % Tuning.RunClock.TierSeconds / Tuning.RunClock.TierSeconds;
            Draw(tierSpent, Sand[Mathf.Min(tier, Sand.Length - 1)], open && flipK >= 1f, collapse, now);

            int s = Mathf.FloorToInt(seconds);
            UiKit.SetText(_label, $"{s / 60}:{s % 60:00}\n{RunClock.TierName(tier).ToUpperInvariant()}");
            UiKit.SetColor(_label, collapse ? new Color(1f, 0.5f, 0.32f) : new Color(0.92f, 0.86f, 0.72f));
        }

        void Show(float now, float seconds)
        {
            _shownAt = now;
            _showUntil = Mathf.Max(_showUntil, now + seconds);
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>Half the glass's inside width at row <paramref name="y"/> (0 = the bottom cap's top).</summary>
        static int HalfWidth(int y)
        {
            // Bulbs between the caps (rows 3..32), the neck at rows 17-18.
            const int bottom = 3, top = H - 4, neckLo = 17, neckHi = 18;
            if (y < bottom || y > top) return -1;
            if (y >= neckLo && y <= neckHi) return 1;
            float t = y < neckLo ? (neckLo - y) / (float)(neckLo - bottom) : (y - neckHi) / (float)(top - neckHi);
            // 0 at the neck, 1 at the wide end: round towards the caps, pinched at the neck.
            return 1 + Mathf.RoundToInt(8f * Mathf.Pow(Mathf.Sin(t * Mathf.PI * 0.5f), 0.7f));
        }

        void Draw(float spent, Color32 sand, bool running, bool ember, float now)
        {
            _px ??= new Color32[W * H];
            for (int i = 0; i < _px.Length; i++) _px[i] = Clear;
            int cx = W / 2;

            // Count the bulbs' insides, so the sand's AREA tracks the time, not its height.
            int topCells = 0, bottomCells = 0;
            for (int y = 0; y < H; y++)
            {
                int hw = HalfWidth(y);
                if (hw < 0 || y == 17 || y == 18) continue;
                if (y > 18) topCells += 2 * hw; else bottomCells += 2 * hw;
            }
            int topSand = Mathf.RoundToInt((1f - spent) * topCells * 0.85f);
            int bottomSand = Mathf.RoundToInt(spent * bottomCells * 0.85f);

            // The glass: dark inside, a pale rim, a shine down the left.
            for (int y = 0; y < H; y++)
            {
                int hw = HalfWidth(y);
                if (hw < 0) continue;
                for (int x = cx - hw; x < cx + hw; x++) Set(x, y, Glass);
                Set(cx - hw - 1, y, GlassRim);
                Set(cx + hw, y, GlassRim);
                if (hw > 3) Set(cx - hw + 1, y, Shine);
            }

            // The bottom bulb fills from its floor upward; the top bulb drains toward the neck.
            int laid = 0;
            for (int y = 3; y < 17 && laid < bottomSand; y++)
            {
                int hw = HalfWidth(y);
                for (int x = cx - hw; x < cx + hw && laid < bottomSand; x++, laid++) Set(x, y, Shade(sand, x, y, ember, now));
            }
            laid = 0;
            for (int y = 19; y <= H - 4 && laid < topSand; y++)
            {
                int hw = HalfWidth(y);
                for (int x = cx - hw; x < cx + hw && laid < topSand; x++, laid++) Set(x, y, Shade(sand, x, y, ember, now));
            }
            // The falling stream, only while the glass stands and runs.
            if (running && topSand > 0)
                for (int y = 4; y <= 18; y++)
                    if (((y + Mathf.FloorToInt(now * 12f)) & 3) != 0) Set(cx - 1 + (y & 1), y, sand);

            // The caps and the posts.
            for (int x = 1; x < W - 1; x++)
            {
                for (int y = 0; y < 3; y++) Set(x, y, y == 2 ? WoodLit : Wood);
                for (int y = H - 3; y < H; y++) Set(x, y, y == H - 3 ? WoodLit : Wood);
            }
            for (int y = 3; y < H - 3; y++) { Set(1, y, Wood); Set(W - 2, y, Wood); }

            // A one-texel outline round everything drawn.
            var shape = (Color32[])_px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (shape[y * W + x].a == 0 && Touches(shape, x, y)) _px[y * W + x] = Outline;

            _texture.SetPixels32(_px);
            _texture.Apply(false);
        }

        static Color32 Shade(Color32 sand, int x, int y, bool ember, float now)
        {
            // Grain: alternate texels a shade darker; ember sand flickers.
            float k = ((x + y) & 1) == 0 ? 1f : 0.86f;
            if (ember) k *= 0.8f + 0.2f * Mathf.Sin(now * 7f + x * 1.3f + y * 0.7f);
            return new Color32((byte)(sand.r * k), (byte)(sand.g * k), (byte)(sand.b * k), 255);
        }

        static bool Touches(Color32[] px, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx != 0 || dy != 0) && nx >= 0 && ny >= 0 && nx < W && ny < H && px[ny * W + nx].a > 0) return true;
                }
            return false;
        }

        void Set(int x, int y, Color32 c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) _px[y * W + x] = c;
        }
    }
}
