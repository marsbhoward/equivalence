using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // THE FIST'S SHAPE (the user's pick, 2026-11-09, off the mock in Tools/tier-art/fists.js:
    // https://claude.ai/artifact/6qCUQv6jUU5skjWsGvYLvL - "thumb and knuckles"). A fist was a square box, and from
    // behind, closed on a disc, it read as a brick. Every hand sprite the wrist cut makes (PaintWristSplit) is
    // reshaped here, so the bare hand and every glove - the catalogue's too - take the same fist:
    //
    //   TAPERED  the outer side in one texel below the top two rows, the knuckle end's corners rounded
    //   THUMB    a light one-texel ridge proud of the inner side (toward the body), a dark crease behind it
    //   KNUCKLES dark lines between the fingers on the knuckle face and notches in the bottom edge - the notches
    //            only on a plated gauntlet (PlatedFists), where the lines fought the plate bands
    //
    // Only art at the body's finest density is touched (a coarser glove has too few texels to reshape). Each
    // sprite's outline colour and thickness are measured off the sprite itself, so the redrawn outline matches
    // whatever drew it - the bare hand's two-texel drawn line or a glove's four-texel auto-outline.
    public partial class PrimitiveCharacterRig
    {
        /// <summary>Gloves whose fist carries plate bands across it: notches only.</summary>
        static readonly HashSet<string> PlatedFists = new() { "tier_kettle_gloves", "tier_sentinel_gloves", "tier_sol_gloves" };

        /// <summary>Whether the worn gloves are a plated gauntlet - set as Apply paints the gloves.</summary>
        bool _platedFists;

        /// <summary>Spare texels each side of a reshaped fist, so the thumb and its outline fit.</summary>
        const int FistPad = 2;

        /// <summary>Reshaped fists by (cut hand sprite, front, plated). Rebuilt on demand after a reload.</summary>
        static Dictionary<(Sprite, bool, bool), Sprite> _fists;

        /// <summary>Testing: the old square fists, for a before-and-after (re-Apply to see it).</summary>
        public static bool DevPlainFists;

        static Sprite ShapedFist(Sprite hand, bool front, bool plated, int overlapRows)
        {
            if (DevPlainFists || hand == null || hand.texture == null || !hand.texture.isReadable) return hand;
            if (!Mathf.Approximately(hand.pixelsPerUnit, Proportions.BodyPpu)) return hand;
            _fists ??= new Dictionary<(Sprite, bool, bool), Sprite>();
            var key = (hand, front, plated);
            if (_fists.TryGetValue(key, out var hit) && hit != null) return hit;

            var r = hand.rect;
            int w = (int)r.width, h = (int)r.height, W = w + 2 * FistPad;
            var src = hand.texture.GetPixels((int)r.x, (int)r.y, w, h);

            // y DOWN from here (yd = h - 1 - y), as the mock draws. The wrist is overlapRows from the top;
            // the fist is every row below the one under it.
            int fistTop = overlapRows + 1;
            Color32 At(int x, int yd) => src[(h - 1 - yd) * w + x];

            // The outline: the colour of the lowest drawn row. The fill: everything else drawn in the fist rows.
            int lowest = -1;
            for (int yd = h - 1; yd >= 0 && lowest < 0; yd--)
                for (int x = 0; x < w; x++) if (At(x, yd).a > 0) { lowest = yd; break; }
            if (lowest < fistTop) { _fists[key] = hand; return hand; }
            var outline = At(MidDrawn(lowest), lowest);
            bool IsOutline(Color32 c) => c.a > 0 && Near(c, outline);

            var fill = new Dictionary<Vector2Int, Color32>();
            for (int yd = fistTop; yd < h; yd++)
                for (int x = 0; x < w; x++)
                {
                    var c = At(x, yd);
                    if (c.a > 0 && !IsOutline(c)) fill[new Vector2Int(x + FistPad, yd)] = c;
                }
            if (fill.Count < 12) { _fists[key] = hand; return hand; }

            // The outline's thickness: drawn texels below the fill's lowest point, at its middle column.
            int fillBot = 0, midX = 0;
            foreach (var k in fill.Keys) if (k.y > fillBot) fillBot = k.y;
            int a0 = int.MaxValue, b0 = int.MinValue;
            foreach (var k in fill.Keys) if (k.y == fillBot) { a0 = Mathf.Min(a0, k.x); b0 = Mathf.Max(b0, k.x); }
            midX = (a0 + b0) / 2 - FistPad;
            int R = 0;
            for (int yd = fillBot + 1; yd < h && At(midX, yd).a > 0 && IsOutline(At(midX, yd)); yd++) R++;
            R = Mathf.Clamp(R, 1, 4);

            int inner = front ? 1 : -1;
            fill = Knuckles(Thumb(Tapered(fill, inner), inner), plated);

            // Paint: the rows above the fist as they were; the fist rows redrawn - the outline ring, then the fill.
            var px = new Color32[W * h];
            for (int yd = 0; yd < fistTop; yd++)
                for (int x = 0; x < w; x++) px[(h - 1 - yd) * W + x + FistPad] = At(x, yd);
            foreach (var k in fill.Keys)
                for (int dy = -R; dy <= R; dy++)
                    for (int dx = -R; dx <= R; dx++)
                    {
                        if (dx * dx + dy * dy > R * R + 2) continue;
                        int x = k.x + dx, yd = k.y + dy;
                        if (x < 0 || x >= W || yd < fistTop || yd >= h) continue;
                        if (!fill.ContainsKey(new Vector2Int(x, yd))) px[(h - 1 - yd) * W + x] = outline;
                    }
            foreach (var kv in fill) if (kv.Key.x >= 0 && kv.Key.x < W) px[(h - 1 - kv.Key.y) * W + kv.Key.x] = kv.Value;

            var tex = new Texture2D(W, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();
            var made = Sprite.Create(tex, new Rect(0, 0, W, h), new Vector2((hand.pivot.x + FistPad) / W, hand.pivot.y / h),
                                     hand.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            made.name = hand.name + ".fist";
            _fists[key] = made;
            return made;

            int MidDrawn(int yd)
            {
                int lo = -1, hi = -1;
                for (int x = 0; x < w; x++) if (At(x, yd).a > 0) { if (lo < 0) lo = x; hi = x; }
                return (lo + hi) / 2;
            }
        }

        static bool Near(Color32 a, Color32 b) =>
            Mathf.Abs(a.r - b.r) <= 8 && Mathf.Abs(a.g - b.g) <= 8 && Mathf.Abs(a.b - b.b) <= 8;

        /// <summary>Each fill row's extent: y -> (first x, last x).</summary>
        static Dictionary<int, Vector2Int> FistRows(Dictionary<Vector2Int, Color32> f)
        {
            var rows = new Dictionary<int, Vector2Int>();
            foreach (var k in f.Keys)
                rows[k.y] = rows.TryGetValue(k.y, out var s) ? new Vector2Int(Mathf.Min(s.x, k.x), Mathf.Max(s.y, k.x)) : new Vector2Int(k.x, k.x);
            return rows;
        }

        static void FistSpan(Dictionary<int, Vector2Int> rows, out int top, out int bot)
        {
            top = int.MaxValue; bot = int.MinValue;
            foreach (var y in rows.Keys) { top = Mathf.Min(top, y); bot = Mathf.Max(bot, y); }
        }

        static Dictionary<Vector2Int, Color32> Tapered(Dictionary<Vector2Int, Color32> f, int inner)
        {
            var rows = FistRows(f);
            FistSpan(rows, out int top, out int bot);
            var o = new Dictionary<Vector2Int, Color32>();
            foreach (var kv in f)
            {
                int x = kv.Key.x, y = kv.Key.y, fromBot = bot - y;
                var s = rows[y];
                int outer = inner > 0 ? s.x : s.y, inn = inner > 0 ? s.y : s.x;
                if (y >= top + 2 && x == outer) continue;                                     // the outer side in one
                if (fromBot == 0 && (Mathf.Abs(x - outer) <= 2 || Mathf.Abs(x - inn) <= 1)) continue;   // the corners
                if (fromBot == 1 && (Mathf.Abs(x - outer) <= 1 || x == inn)) continue;
                if (fromBot == 2 && x == outer + inner) continue;
                o[kv.Key] = kv.Value;
            }
            return o;
        }

        static Dictionary<Vector2Int, Color32> Thumb(Dictionary<Vector2Int, Color32> f, int inner)
        {
            var rows = FistRows(f);
            FistSpan(rows, out int top, out _);
            var o = new Dictionary<Vector2Int, Color32>(f);
            for (int y = top + 2; y <= top + 5; y++)
            {
                if (!rows.TryGetValue(y, out var s)) continue;
                int edge = inner > 0 ? s.y : s.x;
                var c = f[new Vector2Int(edge, y)];
                o[new Vector2Int(edge + inner, y)] = Tint(c, 0.12f);    // the thumb's ridge, proud of the side
                o[new Vector2Int(edge, y)] = Tint(c, 0.06f);
                var behind = new Vector2Int(edge - inner, y);
                if (f.TryGetValue(behind, out var b)) o[behind] = Shade(b, 0.7f);   // the crease behind it
            }
            return o;
        }

        static Dictionary<Vector2Int, Color32> Knuckles(Dictionary<Vector2Int, Color32> f, bool notchesOnly)
        {
            var rows = FistRows(f);
            FistSpan(rows, out _, out int bot);
            if (!rows.TryGetValue(bot - 2, out var s)) return f;
            var o = new Dictionary<Vector2Int, Color32>(f);
            for (int x = s.x + 2; x < s.y; x += 2)
                for (int y = bot - 3; y <= bot; y++)
                {
                    var k = new Vector2Int(x, y);
                    if (!o.ContainsKey(k)) continue;
                    if (y == bot) o.Remove(k);                                   // the notch between two knuckles
                    else if (!notchesOnly) o[k] = Shade(f[k], 0.68f);           // the line between two fingers
                }
            return o;
        }

        static Color32 Tint(Color32 c, float t) =>
            new((byte)(c.r + (255 - c.r) * t), (byte)(c.g + (255 - c.g) * t), (byte)(c.b + (255 - c.b) * t), c.a);
        static Color32 Shade(Color32 c, float k) => new((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), c.a);
    }
}
