using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Ground the ledger leaves for a while (the user's group-3 picks, 2026-10-26): a release's
    /// RESIDUE in its element's own ground ("its element's ground" - smouldering coals, a rippling
    /// puddle, cracked rubble, grit drawn round inward), SALT GROUND as a ring of poured salt with
    /// grains inside ("salt circle"), and Lightfoot's ANCHOR as the element sigil on the floor
    /// ("sigil on the floor").
    ///
    /// A ReleaseCanvas at the floor's density, clipped to standing room like every ground picture,
    /// and round at the real radius. It lives as long as its OWNER (the field, the salt, the anchor),
    /// which sets <see cref="Alpha"/> for its fade - the rules stay on the owner.
    /// </summary>
    public class GroundPatch : ReleaseCanvas
    {
        public enum Kind { Fire, Water, Earth, Air, Salt, Anchor }

        [SerializeField] Kind _kind;
        [SerializeField] GameObject _owner;
        [SerializeField] float _r;
        [SerializeField] int _seed;
        /// <summary>The owner's fade, 0 to 1.</summary>
        public float Alpha = 1f;

        static int _serial;

        public static GroundPatch Spawn(GameObject owner, Vector2 at, float radius, Kind kind, int order)
        {
            int r = Mathf.CeilToInt(radius * Ppu) + 2;
            var p = Make<GroundPatch>("ground." + kind, at, r, r, r, order);
            p._owner = owner; p._kind = kind; p._r = radius * Ppu; p._seed = (_serial++ * 6151) & 0x7fff;
            return p;
        }

        /// <summary>The kind of residue an element leaves.</summary>
        public static Kind Of(ElementType e) => e switch
        {
            ElementType.Water => Kind.Water,
            ElementType.Earth => Kind.Earth,
            ElementType.Air => Kind.Air,
            _ => Kind.Fire,
        };

        static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly Color32 Char = C("#2a120a"), WaterDeep = C("#1b4f92"), WaterLine = C("#56c6f2"), WaterHi = C("#a8ecff"),
                                Rubble = C("#5e9a3c"), RubbleHi = C("#8cc75c"), RubbleGap = C("#14100a"), Grit = C("#c4cad3"), GritHi = C("#f0f3f7"),
                                Salt = C("#f4f6ee"), SaltMid = C("#dfe3d2"), SaltLow = C("#b8bca8"), Anchor = C("#b3d6ff");

        protected override bool Draw()
        {
            if (_owner == null) return false;
            float a = Mathf.Clamp01(Alpha);
            if (a <= 0f) return true;
            float t = Time.time;
            switch (_kind)
            {
                case Kind.Fire:
                    Ring(0, 0, 0, _r, (x, y, d, ang) =>
                    {
                        float n = FireArt.Noise(x * 0.3f + _seed, y * 0.3f);
                        if (d > _r - 2f && FireArt.Hash(x, y, _seed) > (_r - d) / 2f) return;
                        if (n > 0.5f) Over(x, y, FireArt.At(0.35f + FireArt.Noise(x * 0.5f, y * 0.5f + t * 4f) * 0.5f), a);
                        else if (n > 0.36f) Over(x, y, Char, 0.8f * a);
                    });
                    break;
                case Kind.Water:
                    Ring(0, 0, 0, _r, (x, y, d, ang) =>
                    {
                        if (d > _r - 2f && FireArt.Hash(x, y, _seed) > (_r - d) / 2f) return;
                        Over(x, y, WaterDeep, 0.55f * a);
                        float ripple = Mathf.Repeat(d - t * 9f, 7f);
                        if (ripple < 0.9f) Over(x, y, d < _r * 0.5f ? WaterHi : WaterLine, 0.6f * a);
                    });
                    break;
                case Kind.Earth:
                    Ring(0, 0, 0, _r, (x, y, d, ang) =>
                    {
                        float n = FireArt.Noise(x * 0.32f + _seed, y * 0.32f);
                        if (n > 0.6f) Over(x, y, FireArt.Hash(x, y) > 0.7f ? RubbleHi : Rubble, a);
                        else if (n > 0.53f) Over(x, y, RubbleGap, 0.8f * a);
                    });
                    break;
                case Kind.Air:
                    // Grit drawn round and inward along spiral arms.
                    Ring(0, 0, 0, _r, (x, y, d, ang) =>
                    {
                        float arm = Mathf.Repeat(ang * 3f / (Mathf.PI * 2f) + d / _r * 0.8f + t * 0.9f, 1f);
                        if (arm < 0.07f) Over(x, y, d < _r * 0.4f ? GritHi : Grit, 0.8f * a);
                    });
                    break;
                case Kind.Salt:
                    // A ring of poured salt, uneven, and grains scattered inside it.
                    Ring(0, 0, _r - 2.2f, _r, (x, y, d, ang) =>
                    {
                        if (FireArt.Hash(x, y, _seed) < 0.22f) return;
                        Over(x, y, FireArt.Hash(x, y, _seed + 1) > 0.5f ? Salt : SaltMid, a);
                    });
                    for (int k = 0; k < Mathf.RoundToInt(_r * 1.6f); k++)
                    {
                        float ga = FireArt.Hash(k, _seed, 3) * Mathf.PI * 2f, gr = Mathf.Sqrt(FireArt.Hash(k, _seed, 4)) * (_r - 3f);
                        int gx = Mathf.RoundToInt(Mathf.Cos(ga) * gr), gy = Mathf.RoundToInt(Mathf.Sin(ga) * gr);
                        bool glint = FireArt.Hash(k, _seed, Mathf.FloorToInt(t * 3f)) > 0.94f;
                        Over(gx, gy, glint ? Color.white : (k % 3 == 0 ? SaltLow : SaltMid), a);
                    }
                    break;
                case Kind.Anchor:
                {
                    var c = Anchor;
                    Sigil(Mathf.RoundToInt(_r), c, a * (0.65f + 0.2f * Mathf.Sin(t * 4f)));
                    break;
                }
            }
            return true;
        }
    }
}
