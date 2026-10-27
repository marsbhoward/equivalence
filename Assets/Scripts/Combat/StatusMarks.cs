using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Soak, stagger and petrify, each as its own picture (the user's pick, 2026-10-26 - "status
    /// marks"); burn has its own (BurnEmbers). A SOAKED body drips - drops leave its silhouette
    /// and fall; a STAGGERED one has three stars circling over its head; PETRIFY - the Watcher's
    /// root, the one status put on the player - turns the legs to stone from the feet up. Two at
    /// once show both. They replaced one soft halo with the statuses' colours blended.
    ///
    /// Drips and stars are drawn like the burn's embers: one canvas on the body sprite's own texel
    /// grid, read off its per-column profile (BurnEmbers.ProfileOf), an overlay. Petrify lays a
    /// stone copy (PixelSprite.Stone) over each leg layer of a rig, cut to the rows the creep
    /// has reached - the body's own picture turning, like Medusa's statue.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class StatusMarks : MonoBehaviour
    {
        const int PadX = 8, PadBelow = 12, PadAbove = 16;
        const int Drops = 4;
        const float DropCycle = 0.55f, DropFall = 16f, StarOrbit = 7f, CreepSeconds = 0.35f;

        [SerializeField] StatusEffects _status;
        [SerializeField] SpriteRenderer _sr, _body;
        [SerializeField] int _seed;
        [SerializeField] float _petrifiedFor;
        Texture2D _tex;
        Color32[] _px;
        int _w, _h;
        List<SpriteRenderer> _stone;

        static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly Color32[] Drip = { C("#a8ecff"), C("#56c6f2"), C("#2c86cc"), C("#1b4f92") };
        static readonly Color32 StarHot = C("#fffbe0"), Star = C("#f2c230"), StarBack = C("#c08a10");

        public static StatusMarks For(StatusEffects status)
        {
            var go = new GameObject("status.marks");
            go.transform.SetParent(status.transform, false);
            var m = go.AddComponent<StatusMarks>();
            m._status = status;
            m._seed = Mathf.Abs(status.GetEntityId().GetHashCode()) % 997;
            m._sr = go.AddComponent<SpriteRenderer>();
            m._sr.sortingOrder = SortingOrders.StatusOverlay;
            m._sr.enabled = false;
            return m;
        }

        void LateUpdate()
        {
            if (_status == null) { Destroy(gameObject); return; }
            var health = _status.GetComponent<Health>();
            bool dead = health != null && health.IsDead;

            Petrify(!dead && _status.Petrified);

            bool soaked = !dead && _status.Soaked, staggered = !dead && _status.Staggered;
            if ((!soaked && !staggered) || !FindBody()) { _sr.enabled = false; return; }

            var sprite = _body.sprite;
            var p = BurnEmbers.ProfileOf(sprite);
            float ppu = sprite.pixelsPerUnit;
            bool flip = _body.flipX ^ (_body.transform.lossyScale.x < 0f);
            Ensure(p.W + PadX * 2, p.H + PadBelow + PadAbove, ppu);
            System.Array.Clear(_px, 0, _px.Length);
            float now = Time.time;

            if (soaked)
                for (int k = 0; k < Drops; k++)
                {
                    float phase = FireArt.Hash(k, _seed, 1) * DropCycle;
                    float age = (now + phase) % DropCycle;
                    int cycle = Mathf.FloorToInt((now + phase) / DropCycle);
                    int col = Mathf.FloorToInt(FireArt.Hash(k, cycle, _seed) * p.W);
                    if (p.Top[col] < 0) continue;
                    int y0 = p.Bottom[col] + Mathf.FloorToInt(FireArt.Hash(k, cycle, _seed + 3) * (p.Top[col] - p.Bottom[col] + 1));
                    int sc = flip ? p.W - 1 - col : col;
                    int y = y0 + PadBelow - Mathf.RoundToInt(age * DropFall);
                    var c = Drip[Mathf.Min(Drip.Length - 1, Mathf.FloorToInt(age / DropCycle * Drip.Length))];
                    Put(sc + PadX, y, c);
                    Put(sc + PadX, y + 1, Drip[0]);
                }

            if (staggered)
            {
                // Over the head: the middle of the columns that reach the top.
                int lo = p.W, hi = -1;
                for (int x = 0; x < p.W; x++) if (p.Top[x] >= p.Highest - 2) { lo = Mathf.Min(lo, x); hi = Mathf.Max(hi, x); }
                float cx = hi < 0 ? p.W / 2f : (lo + hi) / 2f;
                if (flip) cx = p.W - 1 - cx;
                int cy = p.Highest + PadBelow + Mathf.RoundToInt(OverHead.StarsAt * ppu);   // the second row over the head (OverHead)
                for (int k = 0; k < 3; k++)
                {
                    float a = now * 4f + k * 2.09f;
                    int x = Mathf.RoundToInt(cx + PadX + Mathf.Cos(a) * StarOrbit), y = cy + Mathf.RoundToInt(Mathf.Sin(a) * 2f);
                    bool front = Mathf.Sin(a) < 0f;   // the near side of the orbit is lower on screen
                    var arm = front ? Star : StarBack;
                    Put(x, y, front ? StarHot : Star);
                    Put(x + 1, y, arm); Put(x - 1, y, arm); Put(x, y + 1, arm); Put(x, y - 1, arm);
                }
            }

            _tex.SetPixels32(_px);
            _tex.Apply();
            var min = _body.bounds.min;
            transform.position = new Vector3(min.x - PadX / ppu, min.y - PadBelow / ppu, 0f);
            _sr.enabled = true;
        }

        /// <summary>Stone over each leg layer, cut at the creep's height and rising from the feet.</summary>
        void Petrify(bool on)
        {
            _stone ??= new List<SpriteRenderer>();
            _petrifiedFor = on ? _petrifiedFor + Time.deltaTime : 0f;
            var rig = on ? _status.GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour : null;
            int used = 0;
            if (rig != null)
            {
                // The legs: every drawn layer whose middle sits in the lower part of the figure.
                float top = float.MinValue, bottom = float.MaxValue;
                var all = rig.GetComponentsInChildren<SpriteRenderer>(false);
                foreach (var r in all)
                {
                    if (!Drawn(r)) continue;
                    top = Mathf.Max(top, r.bounds.max.y); bottom = Mathf.Min(bottom, r.bounds.min.y);
                }
                float creep = Mathf.Clamp01(_petrifiedFor / CreepSeconds);
                float line = bottom + (top - bottom) * 0.42f * creep;   // to about the hips
                foreach (var r in all)
                {
                    if (!Drawn(r) || r.bounds.center.y > bottom + (top - bottom) * 0.42f) continue;
                    var stone = Art.PixelSprite.Stone(r.sprite);
                    if (stone == null) continue;
                    var s = StoneAt(used++);
                    // Cut to the rows under the creep line, in the layer's own texels.
                    float rowsUp = (line - r.bounds.min.y) / Mathf.Max(1e-4f, r.bounds.size.y);
                    int rows = Mathf.Clamp(Mathf.CeilToInt(rowsUp * stone.rect.height), 0, (int)stone.rect.height);
                    if (rows <= 0) { s.enabled = false; continue; }
                    s.sprite = Cut(stone, rows);
                    s.flipX = r.flipX; s.flipY = r.flipY;
                    s.sortingLayerID = r.sortingLayerID;
                    s.sortingOrder = r.sortingOrder;
                    s.transform.SetPositionAndRotation(r.transform.position + new Vector3(0f, 0f, -0.001f), r.transform.rotation);
                    s.transform.localScale = r.transform.lossyScale;
                    s.enabled = true;
                }
            }
            for (int i = used; i < _stone.Count; i++) if (_stone[i] != null) _stone[i].enabled = false;
        }

        static bool Drawn(SpriteRenderer r)
            => r != null && r.enabled && !r.forceRenderingOff && r.sprite != null
               && r.sortingOrder < SortingOrders.StatusOverlay && r.name != HitFlash.ChildName;

        // The bottom rows of a stone sprite, same pivot - cached per (sprite, rows).
        static Dictionary<(Sprite, int), Sprite> _cuts = new();
        static Sprite Cut(Sprite stone, int rows)
        {
            if (_cuts.TryGetValue((stone, rows), out var hit) && hit != null) return hit;
            if (_cuts.Count > 2048) _cuts.Clear();
            var r = stone.rect;
            var made = Sprite.Create(stone.texture, new Rect(r.x, r.y, r.width, rows),
                                     new Vector2(stone.pivot.x / r.width, stone.pivot.y / rows), stone.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            return _cuts[(stone, rows)] = made;
        }

        SpriteRenderer StoneAt(int i)
        {
            while (_stone.Count <= i) _stone.Add(null);
            if (_stone[i] != null) return _stone[i];
            var go = new GameObject("status.stone");
            return _stone[i] = go.AddComponent<SpriteRenderer>();
        }

        bool FindBody()
        {
            if (_body != null && _body.enabled && _body.sprite != null && _body.gameObject.activeInHierarchy) return true;
            _body = null;
            float best = 0f;
            foreach (var r in _status.GetComponentsInChildren<SpriteRenderer>())
            {
                if (r == _sr || r.sprite == null || !r.enabled || r.sortingOrder >= SortingOrders.StatusOverlay) continue;
                if (r.name == HitFlash.ChildName || r.name.StartsWith("status.")) continue;
                float area = r.bounds.size.x * r.bounds.size.y;
                if (area > best) { best = area; _body = r; }
            }
            return _body != null;
        }

        void Ensure(int w, int h, float ppu)
        {
            if (_tex != null && _w >= w && _h >= h && Mathf.Approximately(_sr.sprite.pixelsPerUnit, ppu)) return;
            if (_sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
            _w = Mathf.Max(w, _w); _h = Mathf.Max(h, _h);
            _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            _px = new Color32[_w * _h];
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
        }

        void Put(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            _px[y * _w + x] = c;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
            if (_stone != null) foreach (var s in _stone) if (s != null) Destroy(s.gameObject);
        }
    }
}
