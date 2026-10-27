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
            int r = Mathf.CeilToInt(radius * Combat.TexelFx.Body) + 2;
            c._art = Combat.TexelPicture.Attach<HubCircleArt>(go.transform, "circle", Vector2.zero, r, r, r, SortingOrders.GroundDecal);
            c._art.Theme = HubDress.Of(HubItem.Circle);   // the circle in the character's theme (group 8)
            c._art.Radius = radius;
            return c;
        }

        public void SetFocus(bool on) => Focused = on;

        void Update()
        {
            // Unscaled, so the circle keeps breathing under the screen it opens.
            _lit = Mathf.MoveTowards(_lit, Focused ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        }
    }

    /// <summary>The hub circle's picture: the arena's carved double ring and glyphs (Hazards.CircleArt),
    /// the grooves warming and the glyphs lighting gold in turn as the player steps up to it.</summary>
    public class HubCircleArt : Combat.TexelPicture
    {
        [SerializeField] public float Radius;
        [SerializeField] public HubTheme Theme;
        [SerializeField] TransmutationCircle _c;
        static readonly Color32 GoldLit = new(242, 194, 90, 255), Lip = Combat.TexelFx.GrooveLip, Groove = Combat.TexelFx.Groove;

        /// <summary>The groove's two tones (its dark floor and its lit lip) and the glyphs' colour in
        /// the circle's theme - the Cloister's is the arena's carved groove (the agreed rule).</summary>
        (Color32 groove, Color32 lip, Color32 glyph) Tones(int x, int y)
        {
            switch (Theme)
            {
                case HubTheme.Study: return (Pal.Brass[3], Pal.Brass[1], Pal.Brass[2]);
                case HubTheme.Observatory: return (Pal.Brass[3], Pal.Brass[1], Pal.Brass[1]);
                case HubTheme.Crypt: { var b = Combat.FireArt.Hash(x, y) > 0.55f ? Pal.Bone[1] : Pal.CryptS[4]; return (b, b, Pal.Bone[0]); }
                case HubTheme.Atrium: { var m = Combat.FireArt.Hash(x, y) > 0.3f ? Pal.Leaf[2 + (Combat.FireArt.Hash(x, y, 1) > 0.5f ? 1 : 0)] : Pal.Leaf[4]; return (m, m, Pal.Leaf[2]); }
                case HubTheme.Salt: { var c = Combat.FireArt.Hash(x, y) > 0.2f ? Pal.SaltW[0] : Pal.SaltW[2]; return (c, c, Pal.SaltW[0]); }
                case HubTheme.Terrace: return (Pal.Gold[3], Pal.Gold[1], Pal.Gold[2]);
                case HubTheme.Sanctum: return (Pal.RedX[3], Pal.Blacks[5], Pal.RedX[2]);
                case HubTheme.Smithy: return (Pal.Iron[3], Pal.Iron[1], Pal.Iron[2]);
                case HubTheme.Chapel: { var g = Mathf.RoundToInt(Mathf.Atan2(y, x) * 6f) % 5 == 0 ? Pal.Crim[1] : Pal.Gold[2]; return (g, Pal.Gold[1], Pal.Gold[1]); }
                case HubTheme.Shore: { var c = Combat.FireArt.Hash(x, y) > 0.7f ? Pal.Hex("#fff4ec") : Pal.Sand[4]; return (c, c, Pal.Sand[4]); }
                case HubTheme.Volcano: { var c = Combat.FireArt.At(Mathf.Clamp01(0.15f + Combat.FireArt.Noise(x * 0.3f, y * 0.4f - Time.unscaledTime * 2f) * 0.6f)); return (c, c, Combat.FireArt.At(0.2f)); }
                case HubTheme.Dojo: return (Pal.DarkWood[3], Pal.DarkWood[1], Pal.DarkWood[3]);
                case HubTheme.TechLab: { var c = Mathf.RoundToInt(Mathf.Atan2(y, x) * 6f) % 4 == 0 ? Pal.Hex("#e8d090") : Pal.Hex("#b88a40"); return (c, c, Pal.Hex("#d8b860")); }
                default: return (Groove, Lip, Groove);
            }
        }

        protected override bool Draw()
        {
            if (_c == null) _c = GetComponentInParent<TransmutationCircle>();
            float lit = _c != null ? _c.Lit : 0f, R = Radius * Ppu, Ri = R * 0.74f;
            float pulse = lit * (0.6f + 0.2f * Mathf.Sin(Time.unscaledTime * 2f));
            GrooveRing(R, 3, pulse); GrooveRing(Ri, 2, pulse);
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad, rr = (R - 3f + Ri) * 0.5f;
                int gx = Mathf.RoundToInt(Mathf.Cos(a) * rr), gy = Mathf.RoundToInt(Mathf.Sin(a) * rr);
                float beat = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.2f - i * 2.094f);
                var col = Lerp(Tones(gx, gy).glyph, GoldLit, lit * Mathf.Lerp(0.55f, 1f, beat));
                var g = Hazards.CircleArt.Glyph[i];
                for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 5; x++)
                        if (g[4 - y][x] == 'x')
                            for (int sy = 0; sy < 2; sy++) for (int sx = 0; sx < 2; sx++) Over(gx - 5 + x * 2 + sx, gy - 5 + y * 2 + sy, col);
            }
            // The observatory's stars between the rings.
            if (Theme == HubTheme.Observatory)
                for (int i = 0; i < 14; i++) { if (i % 5 == 0) continue; float a = i / 14f * Mathf.PI * 2f, rr = (R - 3f + Ri) * 0.5f; Over(Mathf.RoundToInt(Mathf.Cos(a) * rr), Mathf.RoundToInt(Mathf.Sin(a) * rr), Pal.Hex("#e8eeff")); }
            return true;
        }

        void GrooveRing(float r, int w, float pulse)
            => Ring(0, 0, r - w, r, (x, y, d, ang) =>
            {
                var (groove, lip, _) = Tones(x, y);
                Over(x, y, (y > 0 ? d > r - 1f : d < r - w + 1f) ? Lerp(lip, GoldLit, pulse) : groove);
            });
    }
}
