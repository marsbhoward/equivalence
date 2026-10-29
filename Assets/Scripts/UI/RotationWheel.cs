using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;
using Convergence.Combat;
using Convergence.Player;
using T = Convergence.Core.Tuning.StrikeTiming;

namespace Convergence.UI
{
    /// <summary>
    /// The weapon art rotation as a geared bronze WHEEL meshed to the element seal's left side,
    /// mirroring the health crescents on its right. Its teeth run only round the half that meets
    /// the seal; the outer half of that ring carries the perfect streak instead. It replaced the top-right swing panel, the
    /// "next up" strip, the bottom-centre chain bar and the timing meter's streak pips.
    ///
    ///   - One socket per wheel slot. The art that fires next sits at the TOP under a fixed
    ///     pointer, larger; the rest ride the rim, dimmed, the one after it on the lower left.
    ///     Each socket carries its art's glyph and its weight as notches (1 Light, 2 Medium,
    ///     3 Heavy) - the weight decides how long the timing bar runs.
    ///   - The HUB's lamps are the chain's basics, lit as they land. With them all lit the art is
    ///     banked: the top socket gilds and pulses, and the art's name above turns gold.
    ///   - When an art fires the wheel CLICKS round one slot (an eased turn with a small
    ///     overshoot), bringing the next art up under the pointer.
    ///   - A Black Diamond signature slot is framed in gilt with a diamond at its corner; Shadow's
    ///     Echo, a basic in the art slot, shows no weight.
    ///   - Fog II clouds the dial over: no sockets, no lamps, no name.
    ///   - THE PERFECT STREAK runs up the wheel's outer (left) half as pips, in the ring the
    ///     teeth use on the other side - bottom to top, two groups of five up to
    ///     <see cref="T.StreakCap"/>, the crit it is worth beside the gap between them - mint while building, the perfect green pulsing at the cap. A
    ///     new pip pops; a broken streak flashes red and drains.
    ///
    /// Drawn texel by texel like the seal (<see cref="ElementSeal"/>), at the same density and
    /// with the same one-texel lines; the
    /// glyphs are Images laid over their sockets so they stay upright as the wheel turns. Reads
    /// the player each frame, so nothing pushes into it.
    ///
    /// Domain reload: every reference here is a [SerializeField] Object or a list of them, and
    /// the glyph images are found again by count, so a reload neither loses nor doubles them.
    /// </summary>
    public class RotationWheel : MonoBehaviour
    {
        public const int Size = 100;
        public const float UnitsPerTexel = ElementSeal.UnitsPerTexel;
        public const float Centre = Size * 0.5f;

        /// <summary>The wheel's outermost reach - its teeth - from the centre, in texels.</summary>
        public const float OuterR = 39f;

        const float BodyR = 34f, OrbitR = 23f, HubR = 11f;
        const int ActiveSocket = 20, OtherSocket = 16;
        // The streak's pips: centred in the teeth's ring, clock degrees up the outer half, the
        // second group of five a gap further on.
        const float PipR = 37f, PipFrom = 195f, PipTo = 340f, PipGroupGap = 10f;
        const float TurnSeconds = 0.32f;
        const float PopSeconds = 0.18f, BrokenSeconds = 0.6f;
        const string EchoId = "shadow_echo";

        static readonly Color32 Clear = new(0, 0, 0, 0);
        static readonly Color32 BronzeHi = new(0xf2, 0xcf, 0x8c, 255);
        static readonly Color32 BronzeLt = new(0xc8, 0x91, 0x4f, 255);
        static readonly Color32 BronzeMd = new(0x8f, 0x5d, 0x2e, 255);
        static readonly Color32 BronzeDk = new(0x57, 0x35, 0x19, 255);
        static readonly Color32 BronzeSh = new(0x2c, 0x1a, 0x0b, 255);
        static readonly Color32 Face = new(0x1c, 0x1a, 0x22, 255);
        static readonly Color32 Cloud = new(0x34, 0x32, 0x3c, 255);
        static readonly Color32 CloudLt = new(0x45, 0x43, 0x4e, 255);
        static readonly Color32 Well = new(0x14, 0x12, 0x19, 255);
        static readonly Color32 Frame = new(0x3b, 0x37, 0x46, 255);
        static readonly Color32 Ready = new(0xd8, 0xd2, 0xc4, 255);
        static readonly Color32 Gold = new(0xff, 0xd9, 0x5a, 255);
        static readonly Color32 Gilt = new(0xc9, 0xa2, 0x3a, 255);
        static readonly Color32 LampOn = new(0xf3, 0xea, 0xd8, 255);
        static readonly Color32 LampOff = new(0x3d, 0x30, 0x40, 255);
        static readonly Color32 Outline = new(0x0b, 0x0a, 0x0e, 255);

        static readonly Color GlyphActive = new(0.94f, 0.9f, 0.82f);
        static readonly Color GlyphBanked = new(1f, 0.85f, 0.35f);
        static readonly Color GlyphSignature = new(0.95f, 0.81f, 0.55f);
        static readonly Color GlyphRest = new(0.43f, 0.44f, 0.49f);
        static readonly Color NameRest = new(0.78f, 0.79f, 0.84f);

        [SerializeField] RawImage _image;
        [SerializeField] Texture2D _texture;
        [SerializeField] PlayerController _player;
        [SerializeField] RectTransform _rt;
        [SerializeField] Text _name, _streak;
        [SerializeField] List<Image> _glyphs = new();

        Color32[] _px;
        int _shownRotation = int.MinValue, _shownSlots = -1;
        float _turnAt = -10f;
        int _lastStreak = -1, _brokenFrom;
        float _popAt = -10f, _brokenAt = -10f;
        string _shownName;
        int _shownCrit = -1;

        // ---------------------------------------------------------------- building

        /// <summary>Fill <paramref name="rt"/> (<see cref="Size"/> x <see cref="UnitsPerTexel"/>
        /// square, the wheel at its centre) with <paramref name="player"/>'s wheel.</summary>
        public static RotationWheel Build(RectTransform rt, PlayerController player)
        {
            var w = rt.gameObject.AddComponent<RotationWheel>();
            w._player = player;
            w._rt = rt;

            var art = UiKit.Rect(rt, "wheel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            w._image = art.gameObject.AddComponent<RawImage>();
            w._image.raycastTarget = false;
            w._texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "rotation.wheel",
            };
            w._image.texture = w._texture;

            var mid = new Vector2(0.5f, 0.5f);
            float top = (OuterR + 8f) * UnitsPerTexel;
            var nameRt = UiKit.Rect(rt, "name", mid, mid, new Vector2(-100, top), new Vector2(100, top + 28));
            w._name = UiKit.Label(nameRt, "", 20, NameRest, TextAnchor.MiddleCenter);
            UiKit.Legible(w._name);
            Shadowed(w._name);

            // Beside the pips, level with the gap between their two groups.
            float side = -(OuterR + 4f) * UnitsPerTexel;
            var streakRt = UiKit.Rect(rt, "streak", mid, mid, new Vector2(side - 160, -12), new Vector2(side, 12));
            w._streak = UiKit.Label(streakRt, "", 15, T.PipLit, TextAnchor.MiddleRight);
            UiKit.Legible(w._streak);
            Shadowed(w._streak);

            return w;
        }

        /// <summary>
        /// Draw the wheel <paramref name="scale"/> times its own size, its two lines of text
        /// kept at theirs - the WHEEL layout's stick (Hud), where its rim has to reach the stick's
        /// travel. An integer, so the texels stay square. The name keeps its bottom edge and the
        /// streak its right edge, so both still sit just clear of the bigger rim.
        /// </summary>
        public void SetScale(float scale)
        {
            if (_rt == null || Mathf.Approximately(_rt.localScale.x, scale)) return;
            _rt.localScale = new Vector3(scale, scale, 1f);
            KeepSize(_name, new Vector2(0.5f, 0f), scale);
            KeepSize(_streak, new Vector2(1f, 0.5f), scale);
        }

        static void KeepSize(Text t, Vector2 pivot, float scale)
        {
            if (t == null) return;
            var rt = (RectTransform)t.transform;
            Vector2 min = rt.offsetMin, max = rt.offsetMax;
            rt.pivot = pivot;
            rt.offsetMin = min; rt.offsetMax = max;   // a pivot change moves the rect; put it back
            rt.localScale = new Vector3(1f / scale, 1f / scale, 1f);
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

        /// <summary>One glyph Image per slot, made or trimmed to the wheel's size - it grows with
        /// Extra Sigil and shrinks when the player drops slot 3.</summary>
        void FitGlyphs(int count)
        {
            _glyphs ??= new List<Image>();
            _glyphs.RemoveAll(g => g == null);
            while (_glyphs.Count < count)
            {
                var g = UiKit.Rect(_rt, $"glyph{_glyphs.Count}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);
                var img = g.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                img.preserveAspect = true;
                _glyphs.Add(img);
            }
            while (_glyphs.Count > count)
            {
                Destroy(_glyphs[_glyphs.Count - 1].gameObject);
                _glyphs.RemoveAt(_glyphs.Count - 1);
            }
        }

        // ---------------------------------------------------------------- ticking

        void Update()
        {
            var p = _player;
            if (p == null || _texture == null || p.Slots.Count == 0) return;
            float now = Time.unscaledTime;

            int n = p.Slots.Count;
            if (n != _shownSlots) { _shownSlots = n; FitGlyphs(n); }
            if (p.RotationIndex != _shownRotation)
            {
                // The very first read is where the wheel already stands, not a turn.
                if (_shownRotation != int.MinValue) _turnAt = now;
                _shownRotation = p.RotationIndex;
            }

            int streak = p.PerfectStreak;
            if (_lastStreak >= 0)
            {
                if (streak > _lastStreak) _popAt = now;
                else if (streak < _lastStreak) { _brokenAt = now; _brokenFrom = _lastStreak; }
            }
            _lastStreak = streak;

            bool fog = p.Ledger != null && p.Ledger.HideChainHud;
            bool banked = p.FinisherNext;
            float step = 360f / n;
            float turn = Turn(now) * step;    // eases from -step to 0 as the wheel settles

            Redraw(p, n, step, turn, banked, fog, now);
            PlaceGlyphs(p, n, step, turn, banked, fog);
            Labels(p, banked, fog, streak);
        }

        /// <summary>How far the wheel still has to go, in slots: -1 at the moment an art fires,
        /// past 0 a little (the overshoot), settling at 0.</summary>
        float Turn(float now)
        {
            float t = Mathf.Clamp01((now - _turnAt) / TurnSeconds);
            if (t >= 1f) return 0f;
            // Ease out with a little overshoot (a "back" curve), so it clicks into place.
            const float s = 1.7f;
            float u = t - 1f;
            float e = 1f + (s + 1f) * u * u * u + s * u * u;
            return e - 1f;
        }

        /// <summary>The clock angle of slot <paramref name="j"/> places after the active one: the
        /// active at the top, the next on the lower left (the wheel turns clockwise to bring it up).</summary>
        static float SlotAngle(int j, float step, float turn) => -j * step + turn;

        Moveset SlotAt(PlayerController p, int j, out int index)
        {
            int n = p.Slots.Count;
            index = ((p.RotationIndex + j) % n + n) % n;
            return p.Slots[index] ?? MovesetLibrary.Default;
        }

        internal static int WeightOf(Moveset m)
        {
            if (m == null || m.Id == EchoId || m.Finisher == null) return 0;
            return m.Finisher.Weight switch
            {
                FinisherWeight.Light => 1,
                FinisherWeight.Medium => 2,
                _ => 3,
            };
        }

        void PlaceGlyphs(PlayerController p, int n, float step, float turn, bool banked, bool fog)
        {
            for (int j = 0; j < n && j < _glyphs.Count; j++)
            {
                var img = _glyphs[j];
                if (img == null) continue;
                var m = SlotAt(p, j, out int index);
                bool active = j == 0;
                if (fog) { UiKit.SetColor(img, Color.clear); continue; }

                UiKit.SetSprite(img, Combat.Glyphs.Get(m.FinisherGlyph));
                float a = SlotAngle(j, step, turn) * Mathf.Deg2Rad;
                var rt = (RectTransform)img.transform;
                float size = (active ? ActiveSocket - 8 : OtherSocket - 6) * UnitsPerTexel;
                // Two texels up from the socket's middle, leaving its bottom rows to the weight notches.
                rt.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * OrbitR * UnitsPerTexel
                                    + new Vector2(0f, 2f * UnitsPerTexel);
                rt.sizeDelta = new Vector2(size, size);
                UiKit.SetColor(img, !active ? GlyphRest
                    : banked ? GlyphBanked
                    : p.SlotLocked(index) ? GlyphSignature
                    : GlyphActive);
            }
        }

        void Labels(PlayerController p, bool banked, bool fog, int streak)
        {
            string name = fog ? "" : p.ActiveMoveset.DisplayName.ToUpperInvariant();
            if (name != _shownName) { _shownName = name; UiKit.SetText(_name, name); }
            UiKit.SetColor(_name, banked ? GlyphBanked : NameRest);

            int crit = streak > 0 ? Mathf.RoundToInt(p.StreakCritBonus * 100f) : -1;
            if (crit != _shownCrit)
            {
                _shownCrit = crit;
                UiKit.SetText(_streak, crit < 0 ? "" : $"+{crit}% CRIT");
            }
            UiKit.SetColor(_streak, streak >= T.StreakCap ? T.Perfect : T.PipLit);
        }

        // ---------------------------------------------------------------- drawing

        void Redraw(PlayerController p, int n, float step, float turn, bool banked, bool fog, float now)
        {
            if (_px == null || _px.Length != Size * Size) _px = new Color32[Size * Size];
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 6f);

            // The body and its teeth. The teeth turn with the sockets - it is one wheel - and only
            // the half facing the seal has them: a tooth is drawn whole while its middle is on
            // that side, so one crossing the top or bottom pops rather than being cut.
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x + 0.5f - Centre, dy = y + 0.5f - Centre;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                Color32 c = Clear;
                if (d < BodyR)
                {
                    if (d >= BodyR - 1f) c = dy > 0f ? BronzeLt : BronzeMd;
                    else if (d >= BodyR - 2f) c = BronzeDk;
                    else if (fog) c = (((x >> 1) * 7 + (y >> 1) * 3) % 9 < 4) ? CloudLt : Cloud;
                    else c = Face;
                }
                else if (d < OuterR && Mathf.Repeat(a - turn, 20f) < 7f && ToothFacesSeal(a, turn))
                    c = d >= OuterR - 0.8f ? BronzeSh : BronzeDk;
                _px[y * Size + x] = c;
            }

            if (!fog)
            {
                // Back to front, so the active socket draws over its neighbours mid-turn.
                for (int j = n - 1; j >= 0; j--)
                {
                    var m = SlotAt(p, j, out int index);
                    DrawSocket(j, m, p.SlotLocked(index), SlotAngle(j, step, turn), banked, pulse);
                }
                DrawHub(p);
            }
            DrawPointer();
            DrawStreak(p.PerfectStreak, now);

            _texture.SetPixels32(_px);
            _texture.Apply(false);
        }

        /// <summary>Whether the tooth at clock angle <paramref name="a"/> has its middle on the
        /// seal's (right) side.</summary>
        static bool ToothFacesSeal(float a, float turn)
        {
            float middle = Mathf.Floor((a - turn) / 20f) * 20f + turn + 3.5f;
            return Mathf.Repeat(middle, 360f) < 180f;
        }

        void DrawSocket(int j, Moveset m, bool signature, float angle, bool banked, float pulse)
        {
            bool active = j == 0;
            int size = active ? ActiveSocket : OtherSocket;
            float a = angle * Mathf.Deg2Rad;
            int x0 = Mathf.RoundToInt(Centre + Mathf.Sin(a) * OrbitR - size * 0.5f);
            int y0 = Mathf.RoundToInt(Centre + Mathf.Cos(a) * OrbitR - size * 0.5f);

            Color32 border = !active ? (signature ? BronzeDk : Frame)
                : signature ? Gilt
                : banked ? Color32.Lerp(Gilt, Gold, pulse)
                : Ready;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                Put(x0 + x, y0 + y, edge ? border : Well);
            }
            if (active && banked)
                for (int i = -1; i <= size; i++)          // a halo texel round the frame
                {
                    var glow = Color32.Lerp(Clear, Gold, 0.35f + 0.4f * pulse);
                    Blend(x0 + i, y0 - 1, glow); Blend(x0 + i, y0 + size, glow);
                    Blend(x0 - 1, y0 + i, glow); Blend(x0 + size, y0 + i, glow);
                }
            if (signature)
            {
                // A diamond at the top-right corner, of two-texel blocks.
                int cx = x0 + size - 2, cy = y0 + size - 2;
                Block(cx, cy + 2, Gilt); Block(cx - 2, cy, Gilt); Block(cx + 2, cy, Gilt); Block(cx, cy - 2, Gilt);
                Block(cx, cy, BronzeHi);
            }

            int weight = WeightOf(m);
            if (weight == 0) return;
            var ink = !active ? (Color32)GlyphRest : banked ? (Color32)GlyphBanked : (Color32)GlyphActive;
            int width = (weight * 2 - 1) * 2;
            int nx = x0 + (size - width) / 2;
            for (int i = 0; i < weight; i++) Block(nx + i * 4, y0 + 1, ink);
        }

        void DrawHub(PlayerController p)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x + 0.5f - Centre, dy = y + 0.5f - Centre;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < HubR) _px[y * Size + x] = d >= HubR - 1f ? BronzeMd : BronzeSh;
            }

            int lamps = Mathf.Max(1, p.BasicsPerChain);
            int lit = Mathf.Min(p.ComboIndex, lamps);
            if (p.FinisherNext) lit = lamps;
            int width = (lamps * 3 - 1) * 2;
            int x0 = Mathf.FloorToInt(Centre) - width / 2, y0 = Mathf.FloorToInt(Centre) - 2;
            for (int i = 0; i < lamps; i++)
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                Put(x0 + i * 6 + x, y0 + y, i < lit ? LampOn : LampOff);
        }

        /// <summary>The fixed pointer over the top socket: it does not turn with the wheel. Steps
        /// of two texels, outlined in one.</summary>
        void DrawPointer()
        {
            int cx = Mathf.FloorToInt(Centre);
            int top = Mathf.FloorToInt(Centre + OuterR + 5f);    // the bottom row of the top step
            for (int r = 0; r < 4; r++)
            {
                int x0 = cx - (3 - r) * 2, x1 = cx + (4 - r) * 2;   // the step's lit span, [x0, x1)
                for (int y = 0; y < 2; y++)
                {
                    int row = top - r * 2 + y;
                    Put(x0 - 1, row, BronzeSh); Put(x1, row, BronzeSh);
                    for (int x = x0; x < x1; x++) Put(x, row, r == 0 ? BronzeHi : BronzeLt);
                }
            }
            Block(cx, top - 8, BronzeSh);
        }

        /// <summary>The perfect streak up the wheel's outer half, in the ring the teeth use on the
        /// seal's side: two groups of five, clockwise from the bottom.</summary>
        void DrawStreak(int streak, float now)
        {
            int cap = T.StreakCap;
            float broken = Mathf.Clamp01(1f - (now - _brokenAt) / BrokenSeconds);
            float pop = Mathf.Clamp01(1f - (now - _popAt) / PopSeconds);
            int lit = Mathf.Min(streak, cap);
            float capPulse = 0.5f + 0.5f * Mathf.Sin(now * 5f);

            int group = Mathf.Max(1, cap / 2);
            float step = cap > 1 ? (PipTo - PipFrom - PipGroupGap) / (cap - 1) : 0f;
            for (int i = 0; i < cap; i++)
            {
                float deg = PipFrom + i * step + (i >= group ? PipGroupGap : 0f);
                float a = deg * Mathf.Deg2Rad;
                int x0 = Mathf.RoundToInt(Centre + Mathf.Sin(a) * PipR - 2f);
                int y0 = Mathf.RoundToInt(Centre + Mathf.Cos(a) * PipR - 2f);

                Color c;
                if (broken > 0f && i < Mathf.CeilToInt(Mathf.Min(_brokenFrom, cap) * broken)) c = T.PipBroken;
                else if (i < lit)
                {
                    c = lit >= cap ? Color.Lerp(T.Perfect, Color.white, 0.25f * capPulse) : T.PipLit;
                    if (i == lit - 1 && pop > 0f) c = Color.Lerp(c, T.PipPop, pop);
                }
                else
                {
                    // An empty slot is a faint dot with no outline, so lighting changes its shape.
                    for (int y = 1; y < 3; y++)
                    for (int x = 1; x < 3; x++) Blend(x0 + x, y0 + y, T.PipEmpty);
                    continue;
                }
                for (int y = -1; y <= 4; y++)
                for (int x = -1; x <= 4; x++)
                    Put(x0 + x, y0 + y, x < 0 || y < 0 || x > 3 || y > 3 ? Outline : (Color32)c);
            }
        }

        // ---------------------------------------------------------------- plumbing

        void Put(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            _px[y * Size + x] = c;
        }

        /// <summary>A two-texel square: one texel of the wheel as it was first drawn.</summary>
        void Block(int x, int y, Color32 c)
        {
            Put(x, y, c); Put(x + 1, y, c); Put(x, y + 1, c); Put(x + 1, y + 1, c);
        }

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
    }
}
