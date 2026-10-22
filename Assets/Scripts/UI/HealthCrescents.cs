using UnityEngine;
using UnityEngine.UI;

namespace Convergence.UI
{
    /// <summary>
    /// Health and armour as two CRESCENTS hugging the right side of the element's seal
    /// (<see cref="ElementSeal"/>), the numbers written large beside them - so the seal, the meter
    /// and the body read as one piece at the bottom of the screen.
    ///
    ///   - HEALTH is the inner crescent, filling from the bottom up. A hit leaves a pale CHIP of
    ///     what was lost, which holds a moment and then drains - the size of the blow, read after
    ///     the fact. A heal flashes the fill. Under <see cref="LowFraction"/> the frame throbs red
    ///     and the number turns pink.
    ///   - ARMOUR is the outer crescent, ten PLATES of a tenth of its condition each. A plate that
    ///     breaks flashes white; an empty one shows its cracks. The condition and what it costs
    ///     (the real incoming multiplier) are written under the health number.
    ///
    /// Its own texture, the seal's size and centre, laid OVER the seal's - so the seal's effects
    /// pass behind the crescents and the numbers always read. Purely a picture: <see cref="Hud"/>
    /// hands it the numbers each frame (<see cref="Tick"/>), so nothing here holds a delegate a
    /// domain reload could null.
    /// </summary>
    public class HealthCrescents : MonoBehaviour
    {
        const int Size = ElementSeal.Size;
        const float Centre = ElementSeal.Centre;
        const float U = ElementSeal.UnitsPerTexel;

        // Interiors; a one-texel frame stands round each.
        const float HpIn = 50f, HpOut = 58f, ArmIn = 64f, ArmOut = 68f;
        // Clock degrees (0 at the top, clockwise): the seal's right side.
        const float From = 30f, To = 150f;
        const int Plates = 10;
        const float PlateGapDegrees = 0.8f;

        const float LowFraction = 0.3f;
        const float ChipHoldSeconds = 0.4f, ChipDrainPerSecond = 0.6f;
        const float HealFlashSeconds = 0.3f, ShatterSeconds = 0.3f;

        static readonly Color32 Clear = new(0, 0, 0, 0);
        static readonly Color32 Frame = new(0x0b, 0x0a, 0x0e, 255);
        static readonly Color32 Throb = new(0x9a, 0x1f, 0x33, 255);
        static readonly Color32 TrackHi = new(0x3a, 0x30, 0x40, 255);
        static readonly Color32 Track = new(0x2b, 0x24, 0x30, 255);
        static readonly Color32 TrackDk = new(0x1f, 0x1a, 0x24, 255);
        static readonly Color32 RedHi = new(0xff, 0x9a, 0xa8, 255);
        static readonly Color32 RedMd = new(0xd9, 0x40, 0x59, 255);
        static readonly Color32 RedDk = new(0x8e, 0x23, 0x38, 255);
        static readonly Color32 Chip = new(0xff, 0xe2, 0xc4, 255);
        static readonly Color32 ChipDk = new(0xd8, 0xb0, 0x92, 255);
        static readonly Color32 SteelLt = new(0xc4, 0xd2, 0xe4, 255);
        static readonly Color32 SteelMd = new(0x8d, 0x9f, 0xb8, 255);
        static readonly Color32 PlateEmpty = new(0x26, 0x2a, 0x36, 255);
        static readonly Color32 Crack = new(0x3a, 0x40, 0x50, 255);
        static readonly Color HpColour = new(0.97f, 0.95f, 0.93f);
        static readonly Color HpLowColour = new(1f, 0.7f, 0.75f);

        [SerializeField] RawImage _image;
        [SerializeField] Texture2D _texture;
        [SerializeField] Text _hpText, _armourText;

        Color32[] _px;
        float _chip = -1f, _lastFraction = -1f, _hitAt, _healAt = -10f;
        int _platesLit = -1, _shatterPlate = -1;
        float _shatterAt = -10f;
        int _shownHp = -1, _shownMax = -1, _shownPct = -1, _shownMul = -1, _shownPlates = -1, _shownPlatesFull = -1;
        bool _shownLow;

        // ---------------------------------------------------------------- building

        /// <summary>Over the seal in <paramref name="rt"/> - call after <see cref="ElementSeal.Build"/>
        /// on the same rect, so this draws on top.</summary>
        public static HealthCrescents Build(RectTransform rt)
        {
            var h = rt.gameObject.AddComponent<HealthCrescents>();

            var art = UiKit.Rect(rt, "crescents", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            h._image = art.gameObject.AddComponent<RawImage>();
            h._image.raycastTarget = false;
            h._texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "health.crescents",
            };
            h._image.texture = h._texture;

            // The numbers stand just clear of the armour crescent, on the seal's centre line.
            var mid = new Vector2(0.5f, 0.5f);
            float x0 = (ArmOut + 5f) * U;
            var hpRt = UiKit.Rect(rt, "hp", mid, mid, new Vector2(x0, -6), new Vector2(x0 + 320, 44));
            h._hpText = UiKit.Label(hpRt, "", 36, HpColour, TextAnchor.LowerLeft);
            UiKit.Legible(h._hpText);
            h._hpText.supportRichText = true;
            Shadowed(h._hpText);

            var armRt = UiKit.Rect(rt, "armour", mid, mid, new Vector2(x0, -32), new Vector2(x0 + 520, -6));
            h._armourText = UiKit.Label(armRt, "", 16, new Color(0.70f, 0.77f, 0.87f), TextAnchor.UpperLeft);
            UiKit.Legible(h._armourText);
            Shadowed(h._armourText);

            return h;
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

        /// <param name="armour01">Armour condition, 0..1.</param>
        /// <param name="incoming">The live incoming-damage multiplier (wear x Resilience).</param>
        public void Tick(float current, float max, float armour01, float incoming,
                         float platingLeft = 0f, float platingFull = 0f)
        {
            if (_texture == null) return;
            float now = Time.unscaledTime;
            float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            armour01 = Mathf.Clamp01(armour01);

            // The chip: held at the pre-hit health, then drained down to the real one. A heal
            // past it just carries it along.
            if (_chip < 0f || fraction >= _chip) _chip = fraction;
            if (_lastFraction >= 0f)
            {
                if (fraction < _lastFraction - 0.0001f) _hitAt = now;
                else if (fraction > _lastFraction + 0.0001f) _healAt = now;
            }
            _lastFraction = fraction;
            if (_chip > fraction && now - _hitAt > ChipHoldSeconds)
                _chip = Mathf.Max(fraction, _chip - ChipDrainPerSecond * Time.unscaledDeltaTime);

            int lit = Mathf.FloorToInt(armour01 * Plates + 0.0001f);
            if (_platesLit >= 0 && lit < _platesLit) { _shatterPlate = lit; _shatterAt = now; }
            _platesLit = lit;

            bool low = fraction > 0f && fraction < LowFraction;
            Redraw(fraction, armour01, low, now);
            Labels(current, max, armour01, incoming, low, platingLeft, platingFull);
        }

        void Labels(float current, float max, float armour01, float incoming, bool low,
                    float platingLeft, float platingFull)
        {
            int cur = Mathf.CeilToInt(current), mx = Mathf.CeilToInt(max);
            if (cur != _shownHp || mx != _shownMax)
            {
                _shownHp = cur; _shownMax = mx;
                UiKit.SetText(_hpText, $"<b>{cur}</b><size=20>  /  {mx}</size>");
            }
            if (low != _shownLow)
            {
                _shownLow = low;
                UiKit.SetColor(_hpText, low ? HpLowColour : HpColour);
            }

            // Keyed on what is PRINTED, not the raw floats, so a drift in the sixth decimal does
            // not rebuild a string that reads the same.
            // PLATING: blows left this floor before the armour wears, whole blows, rounded up so a
            // part-spent plate still reads as one.
            int pct = Mathf.RoundToInt(armour01 * 100f), mul = Mathf.RoundToInt(incoming * 100f);
            // Both rounded UP: a part-plate still takes part of a blow (it read 10/9 at 9.6 a floor).
            int plates = Mathf.CeilToInt(platingLeft - 0.001f), platesFull = Mathf.CeilToInt(platingFull - 0.001f);
            if (pct != _shownPct || mul != _shownMul || plates != _shownPlates || platesFull != _shownPlatesFull)
            {
                _shownPct = pct; _shownMul = mul; _shownPlates = plates; _shownPlatesFull = platesFull;
                string plating = platesFull > 0 ? $"   ·   plating {Mathf.Max(0, plates)}/{platesFull}" : "";
                UiKit.SetText(_armourText, $"ARMOUR {pct}%{plating}   ·   dmg taken x{incoming:0.00}");
            }
        }

        // ---------------------------------------------------------------- drawing

        void Redraw(float fraction, float armour01, bool low, float now)
        {
            if (_px == null || _px.Length != Size * Size) _px = new Color32[Size * Size];

            float throb = low ? 0.5f + 0.5f * Mathf.Sin(now * 9f) : 0f;
            float heal = Mathf.Clamp01(1f - (now - _healAt) / HealFlashSeconds);
            float shatter = Mathf.Clamp01(1f - (now - _shatterAt) / ShatterSeconds);
            var hpFrame = Color32.Lerp(Frame, Throb, throb);

            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x + 0.5f - Centre, dy = y + 0.5f - Centre;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
                if (a < 0f) a += 360f;

                Color32 c = Clear;
                float edge = Mathf.Rad2Deg / Mathf.Max(d, 1f);       // one texel, in degrees here
                if (a >= From - edge && a <= To + edge)
                {
                    bool inArc = a >= From && a <= To;
                    // 0 at the bottom of the crescent, 1 at the top.
                    float u = Mathf.Clamp01((To - a) / (To - From));

                    if (d >= HpIn - 1f && d < HpOut + 1f)
                        c = d < HpIn || d >= HpOut || !inArc ? hpFrame : HealthTexel(d, u, fraction, low, throb, heal);
                    else if (d >= ArmIn - 1f && d < ArmOut + 1f)
                        c = d < ArmIn || d >= ArmOut || !inArc ? Frame : PlateTexel(d, u, a, armour01, shatter);
                }
                _px[y * Size + x] = c;
            }

            _texture.SetPixels32(_px);
            _texture.Apply(false);
        }

        Color32 HealthTexel(float d, float u, float fraction, bool low, float throb, float heal)
        {
            bool outer = d >= HpOut - 1f, inner = d < HpIn + 1f;
            // A notch at each quarter on the inner row, so a fraction reads without counting.
            bool notch = inner && (Near(u, 0.25f) || Near(u, 0.5f) || Near(u, 0.75f));

            Color32 c;
            if (u < fraction)
            {
                c = outer ? RedHi : inner ? RedDk : RedMd;
                if (low && !outer) c = Color32.Lerp(c, RedHi, throb * 0.4f);
                if (heal > 0f) c = Color32.Lerp(c, Color.white, heal * 0.7f);
            }
            else if (u < _chip) c = outer ? Chip : ChipDk;
            else c = outer ? TrackHi : inner ? TrackDk : Track;

            return notch ? Color32.Lerp(c, Frame, 0.45f) : c;
        }

        static bool Near(float u, float at) => Mathf.Abs(u - at) < 0.006f;

        Color32 PlateTexel(float d, float u, float a, float armour01, float shatter)
        {
            float slot = u * Plates;
            int k = Mathf.Min(Plates - 1, (int)slot);
            float within = (slot - k) * (To - From) / Plates;          // degrees into this plate
            float half = PlateGapDegrees * 0.5f;
            if (within < half || within > (To - From) / Plates - half) return Frame;

            float f = Mathf.Clamp01(armour01 * Plates - k);
            float along = (within - half) / ((To - From) / Plates - PlateGapDegrees);
            Color32 c;
            if (along < f) c = d >= ArmOut - 1f ? SteelLt : SteelMd;
            else c = f <= 0f && ((int)(a * 3.4f) + (int)d + k * 3) % 5 == 0 ? Crack : PlateEmpty;

            if (k == _shatterPlate && shatter > 0f) c = Color32.Lerp(c, Color.white, shatter);
            return c;
        }
    }
}
