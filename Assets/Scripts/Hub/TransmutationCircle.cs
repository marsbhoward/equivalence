using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The transmutation circle in the middle of the hub: sulfur, mercury and salt on a ring.
    ///
    /// It sits at the centre of the room on purpose. The doors are a decision you make once per
    /// run and then leave through; this is the thing you come back to, and putting it where the
    /// character spawns means the first thing a player is standing in is the one that says
    /// "you can change what you are".
    ///
    /// The three marks are not decoration - they name the three pages of the screen this opens.
    /// See <see cref="UI.TransmutationScreen"/>.
    /// </summary>
    public class TransmutationCircle : MonoBehaviour
    {
        public Vector2 Anchor { get; private set; }
        public float Radius { get; private set; }
        public bool Focused { get; private set; }

        [SerializeField] HubCircleArt _art;
        float _lit;
        internal float Lit => _lit;

        /// <summary>Used (the screen opened from it): the Tree of Life shows over the sigil until the
        /// player steps away, and the light gathers in from the rim once (HubCircleArt). Unscaled time.</summary>
        internal bool Used { get; private set; }
        internal float UsedAt { get; private set; } = -100f;

        public void Use() { Used = true; UsedAt = Time.unscaledTime; }

        public static TransmutationCircle Build(Transform parent, Vector2 centre, float radius)
        {
            var go = new GameObject("transmutation-circle");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var c = go.AddComponent<TransmutationCircle>();
            c.Anchor = centre;
            c.Radius = radius;

            // The arena circle's look (the agreed rule, group 6): a double ring cut in the floor with
            // the three principles' glyphs - its Sulfur, Mercury and Salt tabs - lit gold on approach.
            int r = Mathf.CeilToInt(radius * Tuning.Circle.Ppu) + 2;
            c._art = Combat.TexelPicture.Attach<HubCircleArt>(go.transform, "circle", Vector2.zero, r, r, r, SortingOrders.GroundDecal,
                Tuning.Circle.Ppu);
            c._art.Theme = HubDress.Of(HubItem.Circle);   // the circle in the character's theme (group 8)
            c._art.Radius = radius;
            return c;
        }

        public void SetFocus(bool on)
        {
            Focused = on;
            if (!on) Used = false;
        }

        void Update()
        {
            // Unscaled, so the circle keeps breathing under the screen it opens.
            _lit = Mathf.MoveTowards(_lit, Focused ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        }
    }

    /// <summary>The hub circle's picture: the arena's figure (Hazards.CircleFigureArt) in the room's
    /// theme - the grooves warming and the glyphs pulsing gold in turn as the player steps up; used,
    /// the Tree of Life over the sigil and the light gathering in from the rim once.</summary>
    public class HubCircleArt : Hazards.CircleFigureArt
    {
        [SerializeField] public float Radius;
        [SerializeField] public HubTheme Theme;
        [SerializeField] TransmutationCircle _c;
        static readonly Color32 Lip = Combat.TexelFx.GrooveLip, GrooveBed = Combat.TexelFx.Groove;

        /// <summary>The groove's two tones (its dark floor and its lit lip) and the glyphs' colour in
        /// the circle's theme - the Cloister's is the arena's carved groove (the agreed rule).</summary>
        (Color32 groove, Color32 lip, Color32 glyph) Tones(int x, int y)
        {
            // Hashed per WORLD texel of the old density, so a theme's speckle keeps its grain.
            int hx = x >> 1, hy = y >> 1;
            switch (Theme)
            {
                case HubTheme.Study: return (Pal.Brass[3], Pal.Brass[1], Pal.Brass[2]);
                case HubTheme.Observatory: return (Pal.Brass[3], Pal.Brass[1], Pal.Brass[1]);
                case HubTheme.Crypt: { var b = Combat.FireArt.Hash(hx, hy) > 0.55f ? Pal.Bone[1] : Pal.CryptS[4]; return (b, b, Pal.Bone[0]); }
                case HubTheme.Atrium: { var m = Combat.FireArt.Hash(hx, hy) > 0.3f ? Pal.Leaf[2 + (Combat.FireArt.Hash(hx, hy, 1) > 0.5f ? 1 : 0)] : Pal.Leaf[4]; return (m, m, Pal.Leaf[2]); }
                case HubTheme.Salt: { var c = Combat.FireArt.Hash(hx, hy) > 0.2f ? Pal.SaltW[0] : Pal.SaltW[2]; return (c, c, Pal.SaltW[0]); }
                case HubTheme.Terrace: return (Pal.Gold[3], Pal.Gold[1], Pal.Gold[2]);
                case HubTheme.Sanctum: return (Pal.RedX[3], Pal.Blacks[5], Pal.RedX[2]);
                case HubTheme.Smithy: return (Pal.Iron[3], Pal.Iron[1], Pal.Iron[2]);
                case HubTheme.Chapel: { var g = Mathf.RoundToInt(Mathf.Atan2(y, x) * 6f) % 5 == 0 ? Pal.Crim[1] : Pal.Gold[2]; return (g, Pal.Gold[1], Pal.Gold[1]); }
                case HubTheme.Shore: { var c = Combat.FireArt.Hash(hx, hy) > 0.7f ? Pal.Hex("#fff4ec") : Pal.Sand[4]; return (c, c, Pal.Sand[4]); }
                case HubTheme.Volcano: { var c = Combat.FireArt.At(Mathf.Clamp01(0.15f + Combat.FireArt.Noise(hx * 0.3f, hy * 0.4f - Time.unscaledTime * 2f) * 0.6f)); return (c, c, Combat.FireArt.At(0.2f)); }
                case HubTheme.Dojo: return (Pal.DarkWood[3], Pal.DarkWood[1], Pal.DarkWood[3]);
                case HubTheme.TechLab: { var c = Mathf.RoundToInt(Mathf.Atan2(y, x) * 6f) % 4 == 0 ? Pal.Hex("#e8d090") : Pal.Hex("#b88a40"); return (c, c, Pal.Hex("#d8b860")); }
                default: return (GrooveBed, Lip, GrooveBed);
            }
        }

        protected override bool Draw()
        {
            if (_c == null) _c = GetComponentInParent<TransmutationCircle>();
            float lit = _c != null ? _c.Lit : 0f, R = Outer(Radius), Ri = Inner(R), now = Time.unscaledTime;
            float pulse = lit * (0.6f + 0.2f * Mathf.Sin(now * 2f));
            bool used = _c != null && _c.Used;
            float since = _c != null ? now - _c.UsedAt : 100f;
            // The pulse: the light gathers in once when the circle is used. Unlike the arena's the
            // circle is not spent, so what the ring passes lights again behind it.
            bool gathering = used && since < Tuning.Circle.WaveSeconds;
            float waveR = gathering ? WaveR(R, since) : float.MaxValue;

            (Color32, Color32) Groove2(int x, int y)
            {
                var (groove, lip, _) = Tones(x, y);
                bool taken = gathering && Taken(R, waveR, Mathf.Sqrt(x * x + y * y));
                return (groove, taken ? lip : Lerp(lip, GoldLit, pulse));
            }
            Groove(R, Tuning.Circle.OuterGroove, Vector2.zero, Groove2, 1f);
            Groove(Ri, Tuning.Circle.InnerGroove, Vector2.zero, Groove2, 1f);
            var (sigil, sr) = SigilRings(R);
            foreach (var c in sigil) Groove(sr, Tuning.Circle.SigilGroove, c, Groove2, 1f);

            if (used)
            {
                if (gathering) { Tree(Ri, _ => 1f, _ => GoldLit, 1f); GatheringRing(R, waveR, 1f); LightUnderRing(R, waveR, 1f, true); }
                else if (since < Tuning.Circle.HubPulseSeconds) { var (c, a) = Flare(since - Tuning.Circle.WaveSeconds); Tree(Ri, _ => 1f, _ => GoldLit, 1f); Tree(Ri, _ => 1f, _ => c, a); }
                else Tree(Ri, _ => 1f, _ => GoldLit, 1f);
            }

            for (int i = 0; i < 3; i++)
            {
                var g = Polar(Band(R), GlyphAngles[i]);
                float beat = 0.5f + 0.5f * Mathf.Sin(now * Tuning.Circle.BeatSpeed - i * 2.094f);
                var col = Lerp(Tones(Mathf.RoundToInt(g.x), Mathf.RoundToInt(g.y)).glyph, GoldLit, lit * Mathf.Lerp(0.55f, 1f, beat));
                if (gathering && Taken(R, waveR, g.magnitude)) col = Tones(0, 0).glyph;
                if (gathering && OnWave(R, waveR, g.magnitude) > 0.3f) col = White;
                Glyph(i, g.x, g.y, col, 1f);
            }
            // The observatory's stars between the rings.
            if (Theme == HubTheme.Observatory)
                for (int i = 0; i < 14; i++)
                {
                    if (i % 5 == 0) continue;
                    var p = Polar(Band(R), i / 14f * 360f);
                    Over(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), Pal.Hex("#e8eeff"));
                }
            return true;
        }
    }
}
