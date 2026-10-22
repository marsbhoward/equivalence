using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using Convergence.Player;

namespace Convergence.UI
{
    /// <summary>
    /// The element's meter as a SEAL: the element's alchemical triangle in a round seal whose ring
    /// is the meter. Stands at the bottom of the screen, the health and armour crescents
    /// (<see cref="HealthCrescents"/>) hugging its right side.
    ///
    ///   - A PipCount element (Fire's five rungs, Water's three tiers) splits the ring into that many
    ///     segments, each lit once reached; Water's next tier fills in a darker shade, since
    ///     a part-filled tier is not yet anything the button can fire.
    ///   - Every other element (Earth, Air) fills one continuous ring clockwise from the top.
    ///   - <see cref="ElementalResource.PipGlyphs"/> hang as badges on the rim at each segment's
    ///     middle (Water's soak, surge, burst); the highest reached is gilded - it is what a press
    ///     fires.
    ///   - <see cref="ElementalResource.RimPips"/> run along the top of the rim (Earth's
    ///     aftershocks, Air's streak).
    ///   - FULL: the ring flashes white, then a halo pulses and a dashed ring turns round the seal
    ///     until it is spent.
    ///
    /// EACH ELEMENT BUILDS ITS OWN WAY, drawn behind the seal and growing with the meter:
    ///   - Fire, KINDLING: embers rise off the rim, more and faster each stack; from the third,
    ///     flames lick round the seal, taller each rung; at five the seal wavers in its own heat.
    ///   - Water, RISING TIDE: water fills the seal's face to the meter's level, waves running
    ///     across it; a ripple runs out from the seal for each tier reached; full, drops circle it.
    ///   - Earth, TREMOR: the seal shakes harder as charge builds, cracks run across its face and
    ///     stones lift round it; full, the cracks glow and dust kicks up at its foot.
    ///   - Air, GALE: a dashed wind ring circles the seal, faster with momentum, and more currents
    ///     join it; full, it is a gale.
    ///
    /// The release's name sits above it (lit while a press would do something), a short live note
    /// above that (<see cref="ElementalResource.SealTag"/>), and the ability key at its foot.
    ///
    /// Drawn TEXEL BY TEXEL into one point-filtered texture, as the deal screen's scales are
    /// (<see cref="ExchangeScale"/>), at <see cref="UnitsPerTexel"/> canvas units a texel: two
    /// screen pixels a texel at 1080p, four at 2160p. Twice the texels it was first drawn at, with
    /// frames, gaps and outlines kept ONE texel - so curves step half as far and lines are half as
    /// heavy, while every fill keeps its size (mocked against the old density first:
    /// https://claude.ai/artifact/3ZjApsHx8k1dEvc9QAy9sp). Redrawn every frame - something is always moving - on
    /// unscaled time, so the animations keep going under a pause screen as the HUD's other
    /// readouts do.
    ///
    /// Domain reload: the texture, image, labels and resource are [SerializeField] Object
    /// references and survive; the pixel buffer is a plain, non-readonly array rebuilt on demand.
    /// </summary>
    public class ElementSeal : MonoBehaviour
    {
        /// <summary>The texture's side, in texels. The seal is at its centre; the room around it
        /// holds the effects and, on its own texture over this one, the crescents.</summary>
        public const int Size = 144;
        public const float UnitsPerTexel = 2f;
        public const float Centre = Size * 0.5f;

        /// <summary>The seal's outer edge - everything else is laid out from it.</summary>
        public const float RimR = 38f;
        const float InnerR = 27f, RingR = 35f, BadgeR = 40f, PipR = 43f, SpinR = 45f, HaloR = 48f;
        const float GapTexels = 1.2f;       // between segments, measured along the ring
        const float SigilStroke = 1.6f;

        const float FlashSeconds = 0.25f;
        const float PulseSpeed = 4f;
        const float SpinDegreesPerSecond = 40f;

        static readonly Color32 Clear = new(0, 0, 0, 0);
        static readonly Color32 Gap = new(0x14, 0x12, 0x19, 255);
        static readonly Color32 Rim = new(0x3b, 0x37, 0x46, 255);
        static readonly Color32 Inner = new(0x1d, 0x1b, 0x25, 255);
        static readonly Color32 Outline = new(0x0b, 0x0a, 0x0e, 255);
        static readonly Color32 PipOff = new(0x3a, 0x38, 0x46, 255);
        static readonly Color32 Gilt = new(0xc9, 0xa2, 0x3a, 255);
        static readonly Color32 GiltLit = new(0xff, 0xd9, 0x5a, 255);
        static readonly Color Unlit = new(0.48f, 0.5f, 0.55f);

        // Effect colours that belong to the effect, not the element's tint.
        static readonly Color32 FlameBase = new(0xc2, 0x3f, 0x17, 255);
        static readonly Color32 FlameBody = new(0xff, 0x6b, 0x2e, 255);
        static readonly Color32 FlameGold = new(0xff, 0xcf, 0x5a, 255);
        static readonly Color32 FlameTip = new(0xff, 0xe9, 0xa8, 255);
        static readonly Color32 Stone = new(0x8a, 0x7a, 0x62, 255);

        [SerializeField] RawImage _image;
        [SerializeField] Texture2D _texture;
        [SerializeField] ElementalResource _resource;
        [SerializeField] Text _name, _tag;

        Color32[] _px;
        bool _wasFull;
        float _fullAt = -10f;
        string _shownName, _shownTag;
        int _sx, _sy;                       // the seal's shake/waver this frame, in texels

        struct Ramp { public Color32 Hi, Lt, Md, Dk, Sh; }

        // ---------------------------------------------------------------- building

        /// <summary>Fill <paramref name="rt"/> (<see cref="Size"/> x <see cref="UnitsPerTexel"/>
        /// square, the seal at its centre) with the seal for <paramref name="resource"/>.</summary>
        public static ElementSeal Build(RectTransform rt, ElementalResource resource)
        {
            var s = rt.gameObject.AddComponent<ElementSeal>();
            s._resource = resource;

            var art = UiKit.Rect(rt, "seal", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            s._image = art.gameObject.AddComponent<RawImage>();
            s._image.raycastTarget = false;
            s._texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "element.seal",
            };
            s._image.texture = s._texture;

            var mid = new Vector2(0.5f, 0.5f);
            float top = (RimR + 4f) * UnitsPerTexel;
            var nameRt = UiKit.Rect(rt, "name", mid, mid, new Vector2(-80, top), new Vector2(80, top + 28));
            s._name = UiKit.Label(nameRt, "", 22, Unlit, TextAnchor.MiddleCenter);
            UiKit.Legible(s._name);
            Shadowed(s._name);

            var tagRt = UiKit.Rect(rt, "tag", mid, mid, new Vector2(-80, top + 28), new Vector2(80, top + 50));
            s._tag = UiKit.Label(tagRt, "", 16, new Color(0.78f, 0.8f, 0.86f), TextAnchor.MiddleCenter);
            UiKit.Legible(s._tag);
            Shadowed(s._tag);

            // At the seal's foot, over the bottom of its rim. Nothing on touch: the overlay's own
            // ability button is the key there.
            float foot = -RimR * UnitsPerTexel;
            var keyRt = UiKit.Rect(rt, "key", mid, mid, new Vector2(-60, foot - 14), new Vector2(60, foot + 14));
            var key = UiKit.Hint(keyRt, "E", "", 20, new Color(0.93f, 0.91f, 0.86f),
                TextAnchor.MiddleCenter, GamepadGlyphs.Release);
            Shadowed(key);

            return s;
        }

        static void Shadowed(Text t)
        {
            var o = t.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = new Color(0.04f, 0.04f, 0.06f, 1f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        // ---------------------------------------------------------------- ticking

        void Update()
        {
            var r = _resource;
            if (r == null || _texture == null) return;

            float now = Time.unscaledTime;
            bool full = r.CanRelease && r.Fill01 >= 0.999f;
            if (full && !_wasFull) _fullAt = now;
            _wasFull = full;

            Redraw(r, full, now);

            var ramp = RampOf(ElementInfo.Tint(r.Element));
            string name = r.ReleaseName;
            if (name != _shownName) { _shownName = name; UiKit.SetText(_name, name); }
            UiKit.SetColor(_name, r.CanRelease ? (Color)ramp.Lt : Unlit);

            string tag = r.SealTag ?? "";
            if (tag != _shownTag) { _shownTag = tag; UiKit.SetText(_tag, tag); }
        }

        // ---------------------------------------------------------------- drawing

        void Redraw(ElementalResource r, bool full, float now)
        {
            if (_px == null || _px.Length != Size * Size) _px = new Color32[Size * Size];
            for (int i = 0; i < _px.Length; i++) _px[i] = Clear;

            var ramp = RampOf(ElementInfo.Tint(r.Element));
            float fill = Mathf.Clamp01(r.Fill01);

            // Behind the seal: the element's build-up.
            switch (r.Element)
            {
                case ElementType.Fire: Kindling(r, ramp, full, now); break;
                case ElementType.Water: RisingTide(r, ramp, full, now); break;
                case ElementType.Earth: Tremor(fill, ramp, full, now); break;
                default: Gale(fill, ramp, full, now); break;
            }
            Shake(r.Element, fill, full, now);

            float flash = Mathf.Clamp01(1f - (now - _fullAt) / FlashSeconds);
            float pulse = 0.55f + 0.45f * Mathf.Sin(now * PulseSpeed);
            var inner = full ? Color32.Lerp(Inner, ramp.Md, 0.18f) : Inner;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x + 0.5f - Centre - _sx, dy = y + 0.5f - Centre - _sy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d >= HaloR) continue;
                float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;   // 0 at the top, clockwise
                if (a < 0f) a += 360f;

                if (d < InnerR)
                    _px[y * Size + x] = r.Element == ElementType.Water ? Water(inner, ramp, dx, dy, fill, full, now) : inner;
                else if (d < RingR) _px[y * Size + x] = RingTexel(r, ramp, d, a, full, flash);
                else if (d < RimR) _px[y * Size + x] = Rim;
                else if (full) Blend(x, y, Glow(ramp, d, a, now, pulse));
            }

            if (r.Element == ElementType.Earth) DrawCracks(fill, full, ramp);
            var sigil = full ? ramp.Hi : r.CanRelease ? ramp.Lt : ramp.Dk;
            DrawSigil(r.Element, sigil);
            if (r.PipCount > 0 && r.PipGlyphs != null) DrawBadges(r, ramp);
            DrawRimPips(r, ramp);

            _texture.SetPixels32(_px);
            _texture.Apply(false);
        }

        static Color32 RingTexel(ElementalResource r, Ramp ramp, float d, float a, bool full, float flash)
        {
            bool outer = d >= RingR - 1.5f;
            var lit = full ? (outer ? ramp.Hi : ramp.Lt) : (outer ? ramp.Lt : ramp.Md);
            if (flash > 0f) lit = Color32.Lerp(lit, Color.white, flash * 0.8f);
            float fill = Mathf.Clamp01(r.Fill01);

            int n = r.PipCount;
            if (n <= 0) return a / 360f < fill ? lit : ramp.Sh;

            // Segment 0 is centred on the top, so a badge at its middle sits upright.
            float seg = 360f / n;
            float u = Mathf.Repeat(a + seg * 0.5f, 360f);
            int i = Mathf.Min(n - 1, (int)(u / seg));
            float within = u - i * seg;
            if (Mathf.Min(within, seg - within) * Mathf.Deg2Rad * d < GapTexels * 0.5f) return Gap;

            bool reached = i < r.PipsFilled;
            float f = reached ? 1f : i == r.PipsFilled ? Mathf.Clamp01(fill * n - i) : 0f;
            if (within / seg >= f) return ramp.Sh;
            return reached ? lit : ramp.Dk;
        }

        static Color32 Glow(Ramp ramp, float d, float a, float now, float pulse)
        {
            if (Mathf.Abs(d - SpinR) < 0.6f && Mathf.Repeat(a + now * SpinDegreesPerSecond, 18f) < 7f)
                return WithAlpha(ramp.Hi, 0.85f);
            float t = Mathf.Clamp01((d - RimR) / (HaloR - RimR));
            return WithAlpha(ramp.Lt, (1f - t) * (1f - t) * 0.55f * pulse);
        }

        // ---------------------------------------------------------------- the four build-ups

        /// <summary>Fire: embers off the rim; from the third stack, a ring of flames.</summary>
        void Kindling(ElementalResource r, Ramp ramp, bool full, float now)
        {
            int stacks = r.PipsFilled;
            int embers = stacks switch { 0 => 0, 1 => 1, 2 => 2, 3 => 3, 4 => 5, _ => 8 };
            float dur = stacks switch { <= 1 => 1.8f, 2 => 1.6f, 3 => 1.4f, 4 => 1.1f, _ => 0.8f };
            for (int j = 0; j < embers; j++)
            {
                float a = embers > 1 ? -60f + 120f * j / (embers - 1) : 0f;
                float p = Mathf.Repeat(now / dur + (float)j / embers, 1f);
                var at = At(a, RimR - 2f);
                float x = at.x + (j % 2 == 0 ? 5f : -5f) * p, y = at.y + 32f * p;
                var c = WithAlpha(j % 2 == 0 ? FlameBody : FlameGold, 1f - p);
                int s = p < 0.5f ? 4 : 2;
                for (int oy = 0; oy < s; oy++)
                for (int ox = 0; ox < s; ox++) Blend(Mathf.FloorToInt(x) + ox, Mathf.FloorToInt(y) + oy, c);
            }

            if (stacks < 3) return;
            int count = stacks switch { 3 => 5, 4 => 9, _ => 14 };
            float baseLen = stacks switch { 3 => 5f, 4 => 7f, _ => 10f };
            float step = 360f / count;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x + 0.5f - Centre, dy = y + 0.5f - Centre;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float along = d - (RimR - 1f);
                if (along < 0f || along > baseLen * 1.4f) continue;
                float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                int k = Mathf.RoundToInt(Mathf.Repeat(a, 360f) / step) % count;
                float off = Mathf.Abs(Mathf.DeltaAngle(a, k * step)) * Mathf.Deg2Rad * d;
                float len = baseLen * (Mathf.Repeat(now * 4f + k * 0.37f, 1f) < 0.5f ? 1.35f : 1f);
                if (along >= len || off >= 2.8f * (1f - along / len)) continue;
                float t = along / len;
                Put(x, y, t < 0.35f ? FlameBase : t < 0.6f ? FlameBody : t < 0.85f ? FlameGold : FlameTip);
            }
        }

        /// <summary>Water: a ripple per tier reached, and drops circling at full. The water in
        /// the seal's face is <see cref="Water"/>.</summary>
        void RisingTide(ElementalResource r, Ramp ramp, bool full, float now)
        {
            int tier = r.PipsFilled;
            float dur = tier switch { 1 => 2.6f, 2 => 2.0f, _ => 1.4f };
            if (tier > 0)
                for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = x + 0.5f - Centre, dy = y + 0.5f - Centre;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    for (int j = 0; j < tier; j++)
                    {
                        float p = Mathf.Repeat(now / dur + (float)j / tier, 1f);
                        if (Mathf.Abs(d - (RimR + 22f * p)) < 0.55f) Blend(x, y, WithAlpha(ramp.Lt, 0.75f * (1f - p)));
                    }
                }

            if (!full) return;
            for (int j = 0; j < 3; j++)
            {
                var at = At(now * 360f / 2.6f + j * 120f, 52f);
                int x0 = Mathf.FloorToInt(at.x) - 2, y0 = Mathf.FloorToInt(at.y) - 2;
                for (int y = -1; y <= 6; y++)
                for (int x = -1; x <= 4; x++) Put(x0 + x, y0 + y, Outline);
                for (int y = 0; y < 6; y++)
                for (int x = 0; x < 4; x++) Put(x0 + x, y0 + y, y >= 4 && x < 2 ? ramp.Hi : ramp.Lt);
            }
        }

        /// <summary>The seal's face filling with water to the meter's level, two waves across it.</summary>
        static Color32 Water(Color32 inner, Ramp ramp, float dx, float dy, float fill, bool full, float now)
        {
            float speed = full ? 10f : Mathf.Lerp(3f, 8f, fill);
            float level = -InnerR + 2f * InnerR * fill;
            float back = level + 1.6f + Mathf.Sin((dx - now * speed * 1.2f) * 0.15f + 1f) * 1.6f;
            float front = level + Mathf.Sin((dx + now * speed * 2f) * 0.225f) * 1.8f;
            var c = inner;
            if (dy < back) c = Color32.Lerp(c, ramp.Lt, 0.25f);
            if (dy < front) c = Color32.Lerp(c, ramp.Md, 0.45f);
            if (Mathf.Abs(dy - front) < 0.5f && fill > 0.02f) c = Color32.Lerp(c, ramp.Hi, 0.5f);
            return c;
        }

        /// <summary>Earth: stones lifting round the seal; dust at its foot when full.</summary>
        void Tremor(float fill, Ramp ramp, bool full, float now)
        {
            int stones = fill < 0.2f ? 0 : fill < 0.5f ? 1 : fill < 0.75f ? 2 : full ? 5 : 3;
            // The seal's open side: the crescents hide its right.
            float[] angles = { -50f, -82f, -118f, -20f, -150f };
            for (int j = 0; j < stones; j++)
            {
                var at = At(angles[j], 50f);
                int bob = 2 * Mathf.RoundToInt(Mathf.Sin(now * Mathf.PI * 2f / 1.6f + j * 1.6f) * 1.5f);
                int x0 = Mathf.FloorToInt(at.x) - 2, y0 = Mathf.FloorToInt(at.y) - 2 + bob;
                for (int y = -1; y <= 6; y++)
                for (int x = -1; x <= 6; x++)
                {
                    bool edge = x < 0 || y < 0 || x > 5 || y > 5;
                    Put(x0 + x, y0 + y, edge ? Outline : y >= 4 ? ramp.Lt : Stone);
                }
            }

            if (!full) return;
            float[] puffs = { 150f, 180f, 210f };
            for (int j = 0; j < puffs.Length; j++)
            {
                float p = Mathf.Repeat(now / 1.1f + j / 3f, 1f);
                var at = At(puffs[j], RimR - 2f);
                float rad = 2f + 8f * p;
                var c = WithAlpha(Stone, 0.7f * (1f - p));
                for (int y = -12; y <= 12; y++)
                for (int x = -12; x <= 12; x++)
                    if (x * x + y * y <= rad * rad)
                        Blend(Mathf.FloorToInt(at.x) + x, Mathf.FloorToInt(at.y + 4f * p) + y, c);
            }
        }

        /// <summary>Air: a dashed wind ring and currents circling, faster with momentum.</summary>
        void Gale(float fill, Ramp ramp, bool full, float now)
        {
            float period = full ? 0.8f : Mathf.Lerp(6f, 1.2f, fill);
            int currents = fill < 0.05f ? 0 : fill < 0.4f ? 1 : fill < 0.7f ? 2 : full ? 5 : 3;
            float[] radii = { 52f, 57f, 50f, 61f, 55f };
            float[] pace = { 1f, 0.8f, 1.3f, 0.7f, 1.1f };
            float ringRot = now * 360f / period;
            float ringAlpha = 0.25f + 0.45f * fill;
            float width = full ? 0.9f : 0.55f;

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x + 0.5f - Centre, dy = y + 0.5f - Centre;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < RimR || d > 64f) continue;
                float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;

                if (Mathf.Abs(d - 47f) < 0.55f && Mathf.Repeat(a + ringRot, 20f) < 6f)
                    Blend(x, y, WithAlpha(ramp.Lt, ringAlpha));

                for (int j = 0; j < currents; j++)
                {
                    if (Mathf.Abs(d - radii[j]) >= width) continue;
                    float centre = now * 360f / (period * pace[j]) + j * 72f;
                    if (Mathf.Abs(Mathf.DeltaAngle(a, centre)) < 35f)
                        Blend(x, y, WithAlpha(ramp.Lt, full ? 0.9f : 0.65f));
                }
            }
        }

        /// <summary>Earth's tremor and Fire's heat move the seal itself - by whole PAIRS of texels
        /// (the jolt it had at the old density), on a stepped clock, so it jolts rather than
        /// drifts.</summary>
        void Shake(ElementType e, float fill, bool full, float now)
        {
            int amp = 0;
            float period = 0.3f;
            if (e == ElementType.Earth)
            {
                if (full) { amp = 2; period = 0.12f; }
                else if (fill >= 0.75f) { amp = 1; period = 0.2f; }
                else if (fill >= 0.5f) { amp = 1; period = 0.3f; }
            }
            else if (e == ElementType.Fire && full) { amp = 1; period = 0.18f; }

            if (amp == 0) { _sx = _sy = 0; return; }
            int step = Mathf.FloorToInt(now / period);
            _sx = 2 * (Hash(step) % (2 * amp + 1) - amp);
            // A light shake only moves sideways.
            _sy = e == ElementType.Earth && fill < 0.75f ? 0 : 2 * (Hash(step * 7 + 3) % (2 * amp + 1) - amp);
        }

        static int Hash(int n) => (int)(Mathf.Abs(Mathf.Sin(n * 12.9898f) * 43758.5453f)) & 0x7fffffff;

        // Earth's cracks, in texels from the seal's centre (y up). The first is the one a half
        // charge shows; all four glow at full.
        static readonly Vector2Int[][] Cracks =
        {
            new[] { new Vector2Int(0, 0), new Vector2Int(-4, -6), new Vector2Int(-2, -12), new Vector2Int(-8, -18) },
            new[] { new Vector2Int(0, 0), new Vector2Int(6, 6), new Vector2Int(4, 12), new Vector2Int(10, 18) },
            new[] { new Vector2Int(0, 0), new Vector2Int(10, -6), new Vector2Int(18, -4) },
            new[] { new Vector2Int(0, 0), new Vector2Int(-10, 4), new Vector2Int(-14, 2), new Vector2Int(-20, 6) },
        };

        void DrawCracks(float fill, bool full, Ramp ramp)
        {
            int n = full ? 4 : fill >= 0.75f ? 2 : fill >= 0.5f ? 1 : 0;
            var c = full ? ramp.Hi : Outline;
            int cx = Mathf.FloorToInt(Centre), cy = Mathf.FloorToInt(Centre);
            for (int i = 0; i < n; i++)
            {
                var line = Cracks[i];
                for (int s = 1; s < line.Length; s++)
                {
                    var p = line[s - 1];
                    var q = line[s];
                    int steps = Mathf.Max(Mathf.Abs(q.x - p.x), Mathf.Abs(q.y - p.y));
                    for (int t = 0; t <= steps; t++)
                    {
                        float k = steps == 0 ? 0f : (float)t / steps;
                        int x = Mathf.RoundToInt(Mathf.Lerp(p.x, q.x, k)), y = Mathf.RoundToInt(Mathf.Lerp(p.y, q.y, k));
                        if (x * x + y * y < (InnerR - 2f) * (InnerR - 2f)) PutSeal(cx + x, cy + y, c);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- the seal's marks

        /// <summary>The element's triangle: Fire and Air point up, Water and Earth down; Air and
        /// Earth carry the bar across.</summary>
        void DrawSigil(ElementType e, Color32 col)
        {
            float s = e == ElementType.Fire || e == ElementType.Air ? 1f : -1f;
            var a = new Vector2(0f, 16f * s);
            var b = new Vector2(15f, -10f * s);
            var c = new Vector2(-15f, -10f * s);
            float bar = e == ElementType.Air ? -2f : e == ElementType.Earth ? 2f : float.NaN;
            float wind = Mathf.Sign(Cross(b - a, c - a));

            for (int y = -20; y <= 20; y++)
            for (int x = -20; x <= 20; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float inside = Mathf.Min(EdgeDistance(a, b, p, wind),
                               Mathf.Min(EdgeDistance(b, c, p, wind), EdgeDistance(c, a, p, wind)));
                if (inside < 0f) continue;
                bool onBar = !float.IsNaN(bar) && Mathf.Abs(p.y - bar) < 0.9f;
                if (inside < SigilStroke || onBar) PutSeal(Mathf.FloorToInt(Centre) + x, Mathf.FloorToInt(Centre) + y, col);
            }
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        static float EdgeDistance(Vector2 p, Vector2 q, Vector2 point, float wind)
            => Cross(q - p, point - p) / (q - p).magnitude * wind;

        // 5 x 5, top row first; drawn at two texels a cell.
        static readonly string[] Spiral = { "#####", "....#", ".##.#", ".#..#", ".####" };
        static readonly string[] Chevrons = { "..#..", ".#.#.", "..#..", ".#.#.", "#...#" };
        static readonly string[] Star = { "#.#.#", ".###.", "#####", ".###.", "#.#.#" };
        static readonly string[] Dot = { ".....", ".###.", ".###.", ".###.", "....." };

        static string[] IconOf(Combat.Glyph g) => g switch
        {
            Combat.Glyph.Vortex => Spiral,
            Combat.Glyph.Volley => Chevrons,
            Combat.Glyph.Burst => Star,
            _ => Dot,
        };

        void DrawBadges(ElementalResource r, Ramp ramp)
        {
            var glyphs = r.PipGlyphs;
            int n = r.PipCount;
            for (int i = 0; i < n; i++)
            {
                var at = At(i * 360f / n, BadgeR);
                int x0 = Mathf.RoundToInt(at.x) - 7;
                int y0 = Mathf.RoundToInt(at.y) - 7;

                bool reached = i < r.PipsFilled, firing = i == r.PipsFilled - 1;
                var border = firing ? Gilt : Rim;
                var ink = firing ? GiltLit : reached ? ramp.Hi : ramp.Sh;
                var icon = IconOf(i < glyphs.Length ? glyphs[i] : Combat.Glyph.Basic);

                // A one-texel frame, a one-texel gap inside it, then the icon's 5 x 5 cells.
                for (int y = 0; y < 14; y++)
                for (int x = 0; x < 14; x++)
                {
                    bool edge = x == 0 || y == 0 || x == 13 || y == 13;
                    var c = edge ? border : Gap;
                    int ix = (x - 2) / 2, iy = (y - 2) / 2;
                    if (x >= 2 && y >= 2 && ix < 5 && iy < 5 && icon[4 - iy][ix] == '#') c = ink;
                    PutSeal(x0 + x, y0 + y, c);
                }
            }
        }

        void DrawRimPips(ElementalResource r, Ramp ramp)
        {
            int k = r.RimPips;
            if (k <= 0) return;
            int lit = r.RimPipsLit;
            int size = k <= 4 ? 6 : 4;
            float span = Mathf.Min(140f, (k - 1) * 20f);

            for (int i = 0; i < k; i++)
            {
                float deg = k == 1 ? 0f : -span * 0.5f + i * span / (k - 1);
                var at = At(deg, PipR);
                int x0 = Mathf.RoundToInt(at.x - size * 0.5f);
                int y0 = Mathf.RoundToInt(at.y - size * 0.5f);
                var c = i < lit ? ramp.Lt : PipOff;
                for (int y = -1; y <= size; y++)
                for (int x = -1; x <= size; x++)
                    PutSeal(x0 + x, y0 + y, x < 0 || y < 0 || x == size || y == size ? Outline : c);
            }
        }

        // ---------------------------------------------------------------- plumbing

        /// <summary>A point <paramref name="radius"/> texels from the centre, at a clock angle
        /// (0 at the top, clockwise), in texture coordinates.</summary>
        static Vector2 At(float degrees, float radius)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Centre + Mathf.Sin(a) * radius, Centre + Mathf.Cos(a) * radius);
        }

        void Put(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            _px[y * Size + x] = c;
        }

        /// <summary>A texel of the seal itself, which moves with its shake.</summary>
        void PutSeal(int x, int y, Color32 c) => Put(x + _sx, y + _sy, c);

        /// <summary>Alpha-over onto what is already there.</summary>
        void Blend(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size || c.a == 0) return;
            int i = y * Size + x;
            var under = _px[i];
            float a = c.a / 255f, ua = under.a / 255f;
            float oa = a + ua * (1f - a);
            if (oa <= 0f) return;
            _px[i] = new Color32(
                (byte)((c.r * a + under.r * ua * (1f - a)) / oa),
                (byte)((c.g * a + under.g * ua * (1f - a)) / oa),
                (byte)((c.b * a + under.b * ua * (1f - a)) / oa),
                (byte)(oa * 255f));
        }

        static Color32 WithAlpha(Color32 c, float alpha)
        {
            c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);
            return c;
        }

        static Ramp RampOf(Color tint) => new()
        {
            Hi = Color.Lerp(tint, Color.white, 0.55f),
            Lt = Color.Lerp(tint, Color.white, 0.22f),
            Md = tint,
            Dk = new Color(tint.r * 0.55f, tint.g * 0.55f, tint.b * 0.55f),
            Sh = Color.Lerp(new Color(0.105f, 0.1f, 0.135f), tint, 0.18f),
        };
    }
}
