using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The painted sigil over a body Mark has marked (the user's call, 2026-10-16) - before it,
    /// nothing showed a mark at all. The PLAYER'S OWN element sigil - the triangle chosen at the
    /// sigil door (Glyphs.InElement's shapes, the user's call) - texel-drawn at body density in the
    /// element's colour with a dark outline, hung over the body's head with a slow pulse. When a
    /// weapon art's hit SPENDS the mark it flares white and breaks into four pieces flying apart;
    /// when the mark runs out unspent it fades.
    ///
    /// Never parented to the body (the hit that spends the mark may kill it, and the break must
    /// still play): it follows the top of the body's renderers each LateUpdate. An overlay, so a
    /// body standing in front never hides it. Driven by StatusEffects (Show / Expire); it notices a
    /// spend itself, from Health.MarkMultiplier going back to 1.
    /// </summary>
    public class MarkSigil : MonoBehaviour
    {
        const float Ppu = 37.5f;
        const int R = 10;                // the sigil's half-size in texels (21 square)
        const float FadeSeconds = 0.4f;
        const float BreakSeconds = 0.32f;
        const float FlareSeconds = 0.07f;
        const float ShardTravel = 0.35f;

        // Per element; plain statics, rebuilt after a domain reload.
        static System.Collections.Generic.Dictionary<ElementType, Sprite> _whole = new();
        static System.Collections.Generic.Dictionary<ElementType, Sprite[]> _shards = new();

        Health _body;
        SpriteRenderer[] _bodyRenderers;
        SpriteRenderer _sr;
        SpriteRenderer[] _pieces;
        Color _tint;
        ElementType _element;
        float _clock, _endAt = -1f;
        bool _breaking, _fading;

        /// <summary>Show the sigil over <paramref name="body"/> (or keep the one already there).</summary>
        public static MarkSigil Show(Health body, ElementType element, MarkSigil existing)
        {
            if (body == null) return null;
            var tint = ElementInfo.Tint(element);
            if (existing != null && !existing._breaking && !existing._fading && existing._element == element)
            {
                existing._tint = tint;
                return existing;
            }
            if (existing != null && !existing._breaking && !existing._fading) Destroy(existing.gameObject);
            var go = new GameObject("mark-sigil");
            var s = go.AddComponent<MarkSigil>();
            s._body = body;
            s._bodyRenderers = body.GetComponentsInChildren<SpriteRenderer>();
            s._tint = tint;
            s._element = element;
            s._sr = go.AddComponent<SpriteRenderer>();
            s._sr.sprite = Whole(element);
            s._sr.sortingOrder = SortingOrders.StatusOverlay + 3;
            s.Follow();
            return s;
        }

        /// <summary>The mark ran out unspent: fade.</summary>
        public void Expire()
        {
            if (_breaking || _fading) return;
            _fading = true;
            _endAt = _clock + FadeSeconds;
        }

        void LateUpdate()
        {
            _clock += Time.deltaTime;
            if (!_breaking && !_fading)
            {
                // Spent (or the body died with it on): break.
                if (_body == null || _body.IsDead || _body.MarkMultiplier <= 1f) Break();
                else Follow();
            }

            if (_breaking) { TickBreak(); return; }

            // The pulse is in BRIGHTNESS, never transparency: dipping the alpha thinned the
            // outline into the floor.
            float a = 1f;
            if (_fading)
            {
                float u = Mathf.Clamp01((_endAt - _clock) / FadeSeconds);
                a *= u;
                if (u <= 0f) { Destroy(gameObject); return; }
                if (_body != null) Follow();
            }
            var c = Color.Lerp(_tint, Color.white, 0.3f * (0.5f + 0.5f * Mathf.Sin(_clock * 6f)));
            c.a = a;
            _sr.color = c;
        }

        /// <summary>Over the top of whatever the body draws, on the texel grid.</summary>
        void Follow()
        {
            if (_body == null) return;
            // The top row over the head (OverHead): Sulfur's pips and the stagger stars are below.
            if (!OverHead.Top(_body.transform, out float top, out _)) top = _body.transform.position.y + 0.5f;
            var p = new Vector2(_body.transform.position.x, top + OverHead.SigilAt + (R + 0.5f) / Ppu);
            transform.position = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
        }

        void Break()
        {
            _breaking = true;
            _endAt = _clock + BreakSeconds;
            _sr.color = Color.white;
            var shards = Shards(_element);
            _pieces = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject("mark-shard");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = shards[i];
                sr.sortingOrder = _sr.sortingOrder;
                sr.enabled = false;
                _pieces[i] = sr;
            }
        }

        void TickBreak()
        {
            float t = BreakSeconds - (_endAt - _clock);
            if (t >= BreakSeconds) { Destroy(gameObject); return; }

            // The flare: the whole sigil white for a beat, then it splits.
            if (t < FlareSeconds) { _sr.enabled = true; _sr.color = Color.white; return; }
            _sr.enabled = false;
            float u = (t - FlareSeconds) / (BreakSeconds - FlareSeconds);
            float go = ShardTravel * (1f - (1f - u) * (1f - u));
            for (int i = 0; i < 4; i++)
            {
                var sr = _pieces[i];
                sr.enabled = true;
                var dir = new Vector2(i == 0 || i == 3 ? 1f : -1f, i < 2 ? 1f : -1f) * 0.7071f;
                var off = dir * go + Vector2.down * (u * u * 0.15f);
                sr.transform.localPosition = new Vector3(Mathf.Round(off.x * Ppu) / Ppu, Mathf.Round(off.y * Ppu) / Ppu, 0f);
                var c = Color.Lerp(Color.white, _tint, Mathf.Clamp01(u * 2f));
                c.a = 1f - u;
                sr.color = c;
            }
        }

        // ---- the picture ----
        //
        // The element's sigil sampled onto a 17-texel grid (a texel inked when enough of it lies
        // on the shape - point sampling dropped Air's and Earth's bar), white for the renderer's
        // colour to tint, with a dark one-texel outline round its outside so it reads over any
        // floor or body. The
        // shards are its four quarters.

        static bool[] Ink(ElementType element)
        {
            int n = R * 2 + 1;
            var ink = new bool[n * n];
            const int Sub = 4;
            for (int ty = 0; ty < n; ty++)
                for (int tx = 0; tx < n; tx++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < Sub; sy++)
                        for (int sx = 0; sx < Sub; sx++)
                        {
                            float x = ((tx + (sx + 0.5f) / Sub) / n) * 2f - 1f;
                            float y = ((ty + (sy + 0.5f) / Sub) / n) * 2f - 1f;
                            if (Inked(element, x, y)) hits++;
                        }
                    ink[ty * n + tx] = hits >= 6;
                }
            return ink;
        }

        /// <summary>The element's sigil (Glyphs.InElement), except that Air's and Earth's bar
        /// crosses the MIDDLE of the triangle: at the door's height it sits a texel short of the
        /// hole's tip at this size, and the gap left beyond it read as a dot.</summary>
        static bool Inked(ElementType element, float x, float y)
        {
            switch (element)
            {
                case ElementType.Air:
                    return Glyphs.InElement(ElementType.Fire, x, y)
                           || (Glyphs.Rect(x, y, 0f, BarFromMiddle, 0.40f, 0.09f)
                               && Glyphs.WedgeAt(x, y, 180f, 0.85f, -0.85f, 0.92f));
                case ElementType.Earth:
                    return Glyphs.InElement(ElementType.Water, x, y)
                           || (Glyphs.Rect(x, y, 0f, -BarFromMiddle, 0.40f, 0.09f)
                               && Glyphs.DownWedge(x, y, 0.85f, -0.85f, 0.92f));
                default:
                    return Glyphs.InElement(element, x, y);
            }
        }

        /// <summary>Where Air's bar crosses (Earth's mirrored), in the sigil's -1..1.</summary>
        const float BarFromMiddle = -0.08f;

        static Sprite Whole(ElementType element)
        {
            if (_whole.TryGetValue(element, out var s) && s != null) return s;
            s = Make(element, (x, y) => true);
            _whole[element] = s;
            return s;
        }

        static Sprite[] Shards(ElementType element)
        {
            if (_shards.TryGetValue(element, out var s) && s != null && s[0] != null) return s;
            s = new[]
            {
                Make(element, (x, y) => x >= 0 && y > 0),    // upper right
                Make(element, (x, y) => x < 0 && y >= 0),    // upper left
                Make(element, (x, y) => x <= 0 && y < 0),    // lower left
                Make(element, (x, y) => x > 0 && y <= 0),    // lower right
            };
            _shards[element] = s;
            return s;
        }

        static Sprite Make(ElementType element, System.Func<int, int, bool> keep)
        {
            int n = R * 2 + 1, w = n + 2;   // a texel of room each side for the outline
            var ink = Ink(element);
            bool At(int x, int y) => x >= 0 && y >= 0 && x < n && y < n && ink[y * n + x];
            var tex = new Texture2D(w, w, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            // The outline runs round the OUTSIDE only: drawn inside the holes too, it closed the
            // narrow gap between Air's and Earth's bar and the tip.
            var outside = new bool[w * w];
            var stack = new System.Collections.Generic.Stack<int>();
            stack.Push(0);
            while (stack.Count > 0)
            {
                int i = stack.Pop();
                if (outside[i]) continue;
                int ox = i % w, oy = i / w;
                if (At(ox - 1, oy - 1)) continue;
                outside[i] = true;
                if (ox > 0) stack.Push(i - 1);
                if (ox < w - 1) stack.Push(i + 1);
                if (oy > 0) stack.Push(i - w);
                if (oy < w - 1) stack.Push(i + w);
            }
            var px = new Color32[w * w];
            for (int y = 0; y < w; y++)
                for (int x = 0; x < w; x++)
                {
                    int gx = x - 1, gy = y - 1;
                    if (!keep(gx - R, gy - R)) continue;
                    if (At(gx, gy)) { px[y * w + x] = new Color32(255, 255, 255, 255); continue; }
                    if (!outside[y * w + x]) continue;
                    bool edge = false;
                    for (int dy = -1; dy <= 1 && !edge; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            if (At(gx + dx, gy + dy)) { edge = true; break; }
                    if (edge) px[y * w + x] = new Color32(20, 16, 18, 255);
                }
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, w, w), new Vector2(0.5f, 0.5f), Ppu);
        }
    }
}
