using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>The limbs a strip of gear is swept along (Body8): hip to knee, knee to ankle, elbow to wrist, heel to toe.</summary>
    public enum Limb8 { Thigh, Shin, Fore, Foot }

    /// <summary>
    /// EIGHT DIRECTIONS, PHASE 3 (RIG.md): the pieces that ride the torso, baked per view from the approved five-view
    /// mock (Tools/tier-art/views.js -> bake8.js -> Body8Art.Bronze.cs). Each view holds its colours and a depth per
    /// texel so <see cref="Body8"/> can stack whatever is worn with the limbs it draws. Bronze only for now (the
    /// showcase's tier); the bare suit's torso is set "bare".
    /// </summary>
    public static partial class Body8Art
    {
        public const int Front = 0, FrontQuarter = 1, Side = 2, BackQuarter = 3, Back = 4;

        /// <summary>One piece in one view: its box (columns from the body's centre line, rows down from
        /// <see cref="Top"/> texels above the soles), colours (palette chars, '.' empty) and depths.</summary>
        public sealed class View
        {
            public int X0, Y0, W, H;
            public string Cols;
            public byte[] Depth;    // (z + 64) * 2, z toward the camera; 255 empty; null for a helm (outlined whole)
        }

        public sealed class Piece
        {
            public string Set, Name;
            public GearSlot Slot;
            public int OutR;
            public bool Suit;                       // drawn in the undersuit's ramp ('k'..'h'), not its own colours
            public Dictionary<char, Color> Palette;
            public View[] Views, Mirror;            // Mirror: the piece's mirror image, what it shows facing left (null = the same)

            /// <summary>A cape: trails behind the stride (Body8). The bedroll and the pack hang still, as today's do.</summary>
            public bool Trails => Slot == GearSlot.Back && Name == "cape";

            /// <summary>This piece in other colours (a dye): the same pictures, <paramref name="palette"/>.</summary>
            public Piece Recoloured(Dictionary<char, Color> palette)
            {
                var p = (Piece)MemberwiseClone();
                p.Palette = palette;
                return p;
            }

            public View For(int view, bool mirrored) => (mirrored && Mirror != null ? Mirror : Views)[view];
        }

        /// <summary>
        /// PHASE 3b: a piece of LIMB gear as a strip along the limb at rest, from <see cref="T0"/> to <see cref="T1"/> of
        /// the limb (0 the hip, knee, elbow or heel; 1 the knee, ankle, wrist or toe): per step its radius, its centre off
        /// the axis, and colours across it (lit from the left; the foot's from its top down to the sole). Front, side and
        /// back views; the left limb wears the right one's.
        /// </summary>
        public sealed class Strip
        {
            public string Set, Name;
            public GearSlot Slot;
            public Limb8 Limb;
            public float T0, T1;
            public int OutR;
            public Dictionary<char, Color> Palette;
            public StripView[] Views;

            public Strip Recoloured(Dictionary<char, Color> palette)
            {
                var p = (Strip)MemberwiseClone();
                p.Palette = palette;
                return p;
            }

            public StripView For(int view)
            {
                int i = view == Side ? 1 : view >= BackQuarter ? 2 : 0;
                return Views[i] ?? Views[1] ?? Views[0] ?? Views[2];
            }
        }

        public sealed class StripView
        {
            public int N;
            public float[] Rad, Off;     // texels; Off is across the screen (a limb) or up from the sole (the foot)
            public string Cols;          // N x StripK palette chars, '.' where the piece does not reach
        }

        public const int StripK = 7;

        static List<Piece> _all;
        static List<Strip> _strips;

        public static List<Strip> Strips
        {
            get
            {
                if (_strips == null) { var _ = All; }
                return _strips;
            }
        }

        public static IEnumerable<Strip> StripsOf(string set, GearSlot slot)
        {
            foreach (var p in Strips)
                if (p.Set == set && p.Slot == slot) yield return p;
        }

        public static List<Piece> All
        {
            get
            {
                if (_all == null) { _all = new List<Piece>(); _strips = new List<Strip>(); Bake(); }
                return _all;
            }
        }

        /// <summary>The pieces of <paramref name="set"/> in <paramref name="slot"/> (none when it has no pictures).</summary>
        public static IEnumerable<Piece> Of(string set, GearSlot slot)
        {
            foreach (var p in All)
                if (p.Set == set && p.Slot == slot) yield return p;
        }

        /// <summary>The set a worn look belongs to ("tier_hoplite_torso@dye..." -> "hoplite"), or null.</summary>
        public static string SetOf(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !itemId.StartsWith("tier_")) return null;
            int at = itemId.IndexOf('@');
            string id = at >= 0 ? itemId.Substring(0, at) : itemId;
            int end = id.IndexOf('_', 5);
            return end > 5 ? id.Substring(5, end - 5) : null;
        }

        static void Add(string set, GearSlot slot, string name, int outR, string pal, View[] views, View[] mirror)
        {
            var piece = new Piece { Set = set, Slot = slot, Name = name, OutR = outR, Views = views, Mirror = mirror, Suit = pal == "@suit" };
            if (!piece.Suit)
            {
                piece.Palette = new Dictionary<char, Color>();
                foreach (var entry in pal.Split(' '))
                    if (entry.Length == 7 && ColorUtility.TryParseHtmlString("#" + entry.Substring(1), out var c))
                        piece.Palette[entry[0]] = c;
            }
            _all.Add(piece);
        }

        static void AddStrip(string set, GearSlot slot, string name, Limb8 limb, float t0, float t1, int outR, string pal,
                             StripView front, StripView side, StripView back)
        {
            var strip = new Strip { Set = set, Slot = slot, Name = name, Limb = limb, T0 = t0, T1 = t1, OutR = outR,
                                    Views = new[] { front, side, back }, Palette = new Dictionary<char, Color>() };
            foreach (var entry in pal.Split(' '))
                if (entry.Length == 7 && ColorUtility.TryParseHtmlString("#" + entry.Substring(1), out var c))
                    strip.Palette[entry[0]] = c;
            _strips.Add(strip);
        }

        static StripView S(int n, string rad, string off, string cols)
        {
            var v = new StripView { N = n, Rad = new float[n], Off = new float[n], Cols = cols };
            for (int i = 0; i < n; i++)
            {
                v.Rad[i] = (rad[i] - 'a') * 0.5f;
                v.Off[i] = (off[i] - 'a') * 0.5f;
            }
            return v;
        }

        static View V(int x0, int y0, int w, int h, string cols, string depth)
            => new View { X0 = x0, Y0 = y0, W = w, H = h, Cols = cols, Depth = string.IsNullOrEmpty(depth) ? null : System.Convert.FromBase64String(depth) };
    }
}
