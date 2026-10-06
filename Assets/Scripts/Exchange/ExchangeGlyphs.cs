using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;

namespace Convergence.Exchange
{
    /// <summary>
    /// Icons for the 70 boons and costs, COMPOSED rather than drawn: a silhouette for the family,
    /// a small mark inside it for the entry.
    ///
    /// 70 bespoke icons is a lot of authoring, and at ledger-strip size a lot of shapes nobody can
    /// tell apart. Twelve silhouettes and eight marks cover the whole catalogue, and the family is
    /// legible even when the mark is not - which is the read that matters most, because "another
    /// Tithe" is the thing you need to recognise at a glance.
    ///
    /// Three cost families are deliberately the boon families broken - Edge chips into Blunted,
    /// Hide cracks into Brittle, Azoth splits into Leaking. The costs are not a second vocabulary;
    /// they are what your advantages look like after the exchange has taken its half.
    /// </summary>
    public static class ExchangeGlyphs
    {
        static readonly Dictionary<int, Sprite> _cache = new();

        public static Sprite Get(ExchangeEntry entry)
            => entry == null ? null : Get(entry.Family, entry.Mark);

        public static Sprite Get(ExchangeFamily family, ExchangeMark mark)
        {
            int key = (int)family * 100 + (int)mark;
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var sprite = Glyphs.Render((x, y) => Family(family, x, y) || Mark(mark, x, y));
            _cache[key] = sprite;
            return sprite;
        }

        // ---------------------------------------------------------------- families

        static bool Family(ExchangeFamily f, float x, float y) => f switch
        {
            // ---- boons ----
            ExchangeFamily.Edge        => Blade(x, y),
            ExchangeFamily.Anvil       => Trapezoid(x, y),
            ExchangeFamily.Hide        => Shield(x, y),
            ExchangeFamily.Quicksilver => Droplet(x, y),
            ExchangeFamily.Azoth       => Glyphs.Ring(x, y, 0.70f, 0.13f),
            ExchangeFamily.Ledger      => Page(x, y),

            // ---- costs: the same shapes, damaged ----
            //
            // The bite out of the chevron, the crack down the shield and the gap in the ring are
            // the whole theme in silhouette, and they survive being drawn small far better than a
            // separate set of invented shapes would.
            ExchangeFamily.Blunted     => Blade(x, y) && !Glyphs.Disc(x, y, 0.10f, 0.74f, 0.30f),
            ExchangeFamily.Brittle     => Shield(x, y) || Crack(x, y),
            ExchangeFamily.Tithe       => Vessel(x, y),
            ExchangeFamily.Leaden      => Weight(x, y),
            ExchangeFamily.Leaking     => (Glyphs.Ring(x, y, 0.70f, 0.13f) && !(y < -0.42f && Mathf.Abs(x) < 0.34f))
                                          || Glyphs.Disc(x, y, 0f, -0.74f, 0.11f),
            _                          => Eye(x, y),
        };

        /// <summary>
        /// A blade seen end-on: a tall rhombus outline.
        ///
        /// Replaced an open chevron, which had no enclosed area - every mark landed on or around
        /// the vertex and the icon came out as scribble. A closed shape with a clear middle is a
        /// hard requirement for a family, since the mark is what identifies the entry. Rhombus
        /// rather than a triangle so it cannot be confused with the alchemical element sigils
        /// already in the game.
        /// </summary>
        static bool Blade(float x, float y)
            => RhombusFill(x, y, 1f) && !RhombusFill(x, y, 0.62f);

        static bool RhombusFill(float x, float y, float s)
            => Mathf.Abs(x) / (0.60f * s) + Mathf.Abs(y) / (0.88f * s) <= 1f;

        static bool Trapezoid(float x, float y)
            => TrapFill(x, y, 0.86f) && !TrapFill(x, y, 0.56f);

        static bool TrapFill(float x, float y, float scale)
        {
            float top = 0.30f * scale, bot = -0.46f * scale;
            if (y > top || y < bot) return false;
            float t = (top - y) / (top - bot);
            return Mathf.Abs(x) <= Mathf.Lerp(0.44f, 0.76f, t) * scale;
        }

        static bool Shield(float x, float y)
            => ShieldFill(x, y, 1f) && !ShieldFill(x, y, 0.72f);

        static bool ShieldFill(float x, float y, float s)
        {
            if (y > 0.78f * s || y < -0.86f * s) return false;
            // Straight shoulders that taper to a point: widest just under the top edge.
            float t = Mathf.InverseLerp(0.78f * s, -0.86f * s, y);
            float w = t < 0.30f ? 0.70f * s : Mathf.Lerp(0.70f * s, 0f, (t - 0.30f) / 0.70f);
            return Mathf.Abs(x) <= w;
        }

        /// <summary>
        /// The split in a Brittle shield. Kept to the LEFT shoulder rather than run down the
        /// centre: a crack through the middle collides with every mark and both stop reading.
        /// </summary>
        static bool Crack(float x, float y)
            => Glyphs.RotRect(x, y, -0.40f, 0.30f, 0.22f, 0.055f, 70f)
            || Glyphs.RotRect(x, y, -0.30f, -0.10f, 0.20f, 0.055f, -60f);

        static bool Droplet(float x, float y)
            => DropFill(x, y, 1f) && !DropFill(x, y, 0.66f);

        static bool DropFill(float x, float y, float s)
        {
            if (Glyphs.Disc(x, y, 0f, -0.24f * s, 0.62f * s)) return true;
            if (y < -0.24f * s || y > 0.84f * s) return false;
            float t = Mathf.InverseLerp(-0.24f * s, 0.84f * s, y);
            return Mathf.Abs(x) <= Mathf.Lerp(0.62f * s, 0f, t);
        }

        static bool Page(float x, float y)
            => (Glyphs.Rect(x, y, 0f, 0f, 0.56f, 0.82f) && !Glyphs.Rect(x, y, 0f, 0f, 0.42f, 0.68f))
            || Glyphs.Rect(x, y, 0f, 0.30f, 0.30f, 0.05f)
            || Glyphs.Rect(x, y, 0f, 0.02f, 0.30f, 0.05f);

        /// <summary>A cup with a drip under it - the vessel paying out.</summary>
        static bool Vessel(float x, float y)
            => (VesselFill(x, y, 1f) && !VesselFill(x, y, 0.72f))
            || Glyphs.Disc(x, y, 0f, -0.80f, 0.12f);

        static bool VesselFill(float x, float y, float s)
        {
            if (y > 0.72f * s || y < -0.46f * s) return false;
            float t = Mathf.InverseLerp(0.72f * s, -0.46f * s, y);
            return Mathf.Abs(x) <= Mathf.Lerp(0.66f * s, 0.40f * s, t * t);
        }

        static bool Weight(float x, float y)
            => (TrapFill(x, y, 0.90f) && !TrapFill(x, y, 0.60f) && y < 0.24f)
            || (Glyphs.Ring(x, y, 0.28f, 0.10f) && y > 0.20f);

        static bool Eye(float x, float y)
            => (Glyphs.Disc(x, y * 1.9f, 0f, 0f, 0.74f) && !Glyphs.Disc(x, y * 1.9f, 0f, 0f, 0.54f))
            || Glyphs.RotRect(x, y, 0f, 0f, 0.86f, 0.075f, -26f);

        // ---------------------------------------------------------------- marks

        static bool Mark(ExchangeMark m, float x, float y) => m switch
        {
            ExchangeMark.Dot1   => Glyphs.Disc(x, y, 0f, -0.02f, 0.115f),
            ExchangeMark.Dot2   => Glyphs.Disc(x, y, -0.18f, -0.02f, 0.10f)
                                || Glyphs.Disc(x, y,  0.18f, -0.02f, 0.10f),
            ExchangeMark.Dot3   => Glyphs.Disc(x, y, -0.26f, -0.02f, 0.092f)
                                || Glyphs.Disc(x, y,  0.00f, -0.02f, 0.092f)
                                || Glyphs.Disc(x, y,  0.26f, -0.02f, 0.092f),
            ExchangeMark.Bar    => Glyphs.Rect(x, y, 0f, -0.02f, 0.28f, 0.065f),
            ExchangeMark.Ring   => Glyphs.Ring(x, y, 0.24f, 0.085f),
            ExchangeMark.Cross  => Glyphs.Rect(x, y, 0f, -0.02f, 0.26f, 0.062f)
                                || Glyphs.Rect(x, y, 0f, -0.02f, 0.062f, 0.26f),
            ExchangeMark.Stroke => Glyphs.RotRect(x, y, 0f, -0.02f, 0.28f, 0.062f, 45f),
            ExchangeMark.Drop   => Glyphs.Disc(x, y, 0f, -0.10f, 0.15f)
                                || (y >= -0.10f && y <= 0.24f
                                    && Mathf.Abs(x) <= Mathf.Lerp(0.15f, 0f, (y + 0.10f) / 0.34f)),
            _ => false,
        };
    }
}
