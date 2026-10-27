using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// Medusa's gaze on the floor (the user's picks, group 5, 2026-10-26), texel by texel at body
    /// density over the whole room, painted from <see cref="PillarRing.Shadowed"/> - the call her
    /// look tests - so what is drawn is the rule:
    ///
    ///     THE RED   FILLING OUTWARD from her across the open floor, its front a bright line,
    ///               reaching the walls at the look; the shadows' borders a hard red edge from the
    ///               first frame (group 4's danger rule). The clock is how far it has come.
    ///     THE LOOK  the open floor TURNS TO STONE in a wave out from her, then fades.
    ///
    /// One texture on the danger rung, redrawn at most <see cref="Fps"/> times a second while
    /// either is showing. Which texels are in shadow is cached against the ring's standing and
    /// glass masks, so a pillar breaking mid-telegraph repaints that frame.
    /// </summary>
    public class MedusaGaze : MonoBehaviour
    {
        const float Fps = 15f, WaveSeconds = 0.5f, StoneHold = 0.25f, StoneFade = 0.6f;

        [SerializeField] SpriteRenderer _sr;
        [SerializeField] PillarRing _ring;
        [SerializeField] Vector2 _eye;
        /// <summary>The red's clock, 0..1 (1 reaches the walls); negative when no gaze is building.</summary>
        [SerializeField] public float Fill = -1f;
        [SerializeField] float _lookAt = -9f;

        int _w, _h, _key = int.MinValue;
        Texture2D _tex;
        Color32[] _px;
        bool[] _shadow, _edge;
        float[] _dist;
        float _maxDist, _drawn = -9f;
        bool _on;

        public static MedusaGaze Build(Transform parent, PillarRing ring, Vector2 eye)
        {
            var go = new GameObject("medusa.gaze");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<MedusaGaze>();
            g._sr = go.AddComponent<SpriteRenderer>();
            g._sr.sortingOrder = SortingOrders.GroundDanger;
            g._ring = ring; g._eye = eye;
            return g;
        }

        /// <summary>She looks: the open floor turns to stone from her outward.</summary>
        public void Look() { _lookAt = Time.time; Fill = -1f; }

        Vector2 World(int x, int y) => new((x - _w / 2 + 0.5f) / TexelFx.Body, (y - _h / 2 + 0.5f) / TexelFx.Body);

        void Ensure()
        {
            var inner = Arena.HalfExtents - Vector2.one * Tuning.Scenery.WallInset;
            int w = 2 * Mathf.CeilToInt(inner.x * TexelFx.Body), h = 2 * Mathf.CeilToInt(inner.y * TexelFx.Body);
            if (_tex == null || _w != w || _h != h || _px == null || _dist == null)
            {
                _w = w; _h = h;
                _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                _px = new Color32[w * h]; _shadow = new bool[w * h]; _edge = new bool[w * h]; _dist = new float[w * h];
                _maxDist = 0f;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float d = Vector2.Distance(World(x, y), _eye);
                        _dist[y * w + x] = d;
                        _maxDist = Mathf.Max(_maxDist, d);
                    }
                if (_sr.sprite != null) Destroy(_sr.sprite);
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), TexelFx.Body, 0, SpriteMeshType.FullRect);
                _sr.transform.position = Vector3.zero;
                // A new Texture2D starts OPAQUE GREY: clear it before it is ever shown.
                _tex.SetPixels32(_px); _tex.Apply(false);
                _key = int.MinValue; _on = false;
            }
            int key = _ring != null ? _ring.StandingMask | (_ring.GlassMask << 12) : 0;
            if (key == _key) return;
            _key = key;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    _shadow[y * w + x] = _ring != null && _ring.Shadowed(_eye, World(x, y));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    _edge[i] = !_shadow[i] && ((x > 0 && _shadow[i - 1]) || (x < w - 1 && _shadow[i + 1])
                                               || (y > 0 && _shadow[i - w]) || (y < h - 1 && _shadow[i + w]));
                }
            _drawn = -9f;
        }

        static Color32 A(Color32 c, float a) { c.a = (byte)(Mathf.Clamp01(a) * 255f); return c; }

        void LateUpdate()
        {
            if (_sr == null) return;
            float now = Time.time, sinceLook = now - _lookAt;
            bool look = sinceLook < WaveSeconds + StoneHold + StoneFade;
            bool busy = Fill >= 0f || look;
            if (!busy)
            {
                if (_on) { System.Array.Clear(_px, 0, _px.Length); _tex.SetPixels32(_px); _tex.Apply(false); _on = false; }
                return;
            }
            Ensure();
            if (now - _drawn < 1f / Fps) return;
            _drawn = now; _on = true;

            var red = MedusaCanvas.Red;
            var stone = MedusaCanvas.Stone;
            float front = Mathf.Clamp01(Fill) * _maxDist, band = 2f / TexelFx.Body;
            float wave = Mathf.Clamp01(sinceLook / WaveSeconds) * _maxDist;
            float fade = 1f - Mathf.Clamp01((sinceLook - WaveSeconds - StoneHold) / StoneFade);
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    int i = y * _w + x;
                    if (_shadow[i]) { _px[i] = default; continue; }
                    float d = _dist[i];
                    if (look)
                    {
                        if (d > wave) { _px[i] = default; continue; }
                        var g = stone[1 + Mathf.FloorToInt(FireArt.Hash(x >> 1, y, 4) * 2.5f)];
                        _px[i] = A(g, fade * (d > wave - 3f / TexelFx.Body ? 1f : 0.75f));
                        continue;
                    }
                    if (_edge[i]) { _px[i] = A(red[2], 0.4f + Fill); continue; }
                    if (d > front) { _px[i] = default; continue; }
                    _px[i] = d > front - band ? A(red[2], 0.9f) : A(red[3], 0.42f);
                }
            _tex.SetPixels32(_px); _tex.Apply(false);
        }
    }

    /// <summary>A stone about to land (the user's pick: FILLING INWARD - a hard red edge from the
    /// first frame filling in toward the middle, the Wreathe's danger fill), its shadow growing.
    /// Round on the floor at its real radius. Gone as it lands.</summary>
    public class DebrisWarning : TexelPicture
    {
        [SerializeField] float _radius, _warn;

        public static DebrisWarning At(Transform parent, Vector2 at, float radius, float warn)
        {
            var go = new GameObject("medusa.debris");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            int r = Mathf.CeilToInt(radius * TexelFx.Body) + 2;
            var p = Attach<DebrisWarning>(go.transform, "warning", Vector2.zero, r, r, r, SortingOrders.GroundDanger);
            p._radius = radius * TexelFx.Body; p._warn = warn;
            Destroy(go, warn + 0.05f);
            return p;
        }

        protected override bool Draw()
        {
            float k = Mathf.Clamp01(Age / _warn);
            if (k >= 1f) return false;
            System.Array.Clear(Px, 0, Px.Length);
            float r = _radius, shadow = r * (0.15f + 0.85f * k);
            Ring(0, 0, 0f, shadow, (x, y, d, a) => Over(x, y, new Color32(11, 9, 12, 255), 0.25f + 0.35f * k));
            Ring(0, 0, r * (1f - k), r - 1f, (x, y, d, a) => Over(x, y, MedusaCanvas.Red[3], 0.45f));
            Ring(0, 0, r - 1f, r, (x, y, d, a) => Over(x, y, MedusaCanvas.Red[2]));
            return true;
        }
    }

    /// <summary>The stone itself, coming down the whole time its warning fills (the user's note:
    /// "debris can also still fall while filling inward") - a rock, or for a pillar coming back, the
    /// pillar's drum. Lands as the warning closes.</summary>
    public class FallingStone : TexelPicture
    {
        const int Drop = 150;
        [SerializeField] float _warn;
        [SerializeField] bool _pillar;

        public static void At(Transform parent, Vector2 at, float warn, bool pillar)
        {
            var go = new GameObject("medusa.stone");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var p = Attach<FallingStone>(go.transform, "stone", Vector2.zero, 12, 4, Drop + 40, TexelFx.Air);
            p._warn = warn; p._pillar = pillar;
            Destroy(go, warn + 0.05f);
        }

        protected override bool Draw()
        {
            float k = Mathf.Clamp01(Age / _warn);
            if (k >= 1f) return false;
            System.Array.Clear(Px, 0, Px.Length);
            var s = MedusaCanvas.Stone;
            int h = Mathf.RoundToInt((1f - k * k) * Drop);
            int tall = _pillar ? 30 : 7, half = _pillar ? 6 : 5;
            for (int dy = 0; dy < tall; dy++)
                for (int dx = -half; dx <= half; dx++)
                {
                    if (!_pillar && Mathf.Abs(dx) + (dy < 2 ? 1 : 0) >= half + 1 - (dy > 5 ? 2 : 0)) continue;
                    bool edge = Mathf.Abs(dx) == half || dy == 0 || dy == tall - 1;
                    Put(dx, h + dy, edge ? new Color32(13, 11, 14, 255) : s[1 + (dx > 1 ? 1 : 0) + (dy < 2 ? 1 : 0)]);
                }
            return true;
        }
    }

    /// <summary>Her push or pull (the user's call: her TAIL does it): a ring of dust rolling out
    /// across the floor from her coil, or drawn in to her - twice through the wind-up.</summary>
    public class DustRing : TexelPicture
    {
        const float Reach = 7f;
        [SerializeField] float _seconds;
        [SerializeField] bool _pull;

        public static void At(Transform parent, Vector2 at, float seconds, bool pull)
        {
            var go = new GameObject("medusa.dust");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            int r = Mathf.CeilToInt(Reach * TexelFx.Body) + 2;
            var p = Attach<DustRing>(go.transform, "ring", Vector2.zero, r, r, r, SortingOrders.GroundFx);
            p._seconds = seconds; p._pull = pull;
            Destroy(go, seconds + 0.1f);
        }

        protected override bool Draw()
        {
            float k = Age / _seconds;
            if (k >= 1f) return false;
            System.Array.Clear(Px, 0, Px.Length);
            float pass = (k * 2f) % 1f, max = Reach * TexelFx.Body;
            float r = _pull ? 12f + (max - 12f) * (1f - pass) : 12f + (max - 12f) * pass;
            var dust = TexelFx.RubbleOf(true);
            int frame = Mathf.FloorToInt(Age * 12f);
            Ring(0, 0, r, r + 4f, (x, y, d, a) =>
            {
                if (FireArt.Hash(x, y, frame) < 0.35f) return;
                Over(x, y, dust[1 + (FireArt.Hash(x, y) > 0.5f ? 1 : 0)], 0.85f * (1f - Mathf.Abs(pass - 0.5f)));
            });
            return true;
        }
    }

    /// <summary>
    /// Venom from her crown (band 2, the user's pick): a glob lobbed OVER the pillars at where the
    /// player stands, a small hard red ring warning where it lands, a small hit on landing - chip
    /// pressure while the player hides. The ring is this object; the glob is its child.
    /// </summary>
    public class VenomShot : TexelPicture
    {
        [SerializeField] float _flight, _damage, _radius;
        [SerializeField] Vector2 _from;
        [SerializeField] Transform _target;
        [SerializeField] VenomGlob _glob;
        [SerializeField] bool _landed;

        public static void Lob(Transform parent, Vector2 from, Vector2 to, Transform target, float damage)
        {
            var go = new GameObject("medusa.venom");
            go.transform.SetParent(parent, false);
            go.transform.position = to;
            float rad = Tuning.Medusa.VenomRadius;
            int r = Mathf.CeilToInt(rad * TexelFx.Body) + 2;
            var p = Attach<VenomShot>(go.transform, "ring", Vector2.zero, r, r, r, SortingOrders.GroundDanger);
            p._flight = Tuning.Medusa.VenomFlightSeconds; p._damage = damage; p._radius = rad;
            p._from = from; p._target = target;
            p._glob = Attach<VenomGlob>(go.transform, "glob", from - to, 4, 4, 4, TexelFx.Air);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Spit, from);
            Destroy(go, p._flight + 0.6f);
        }

        protected override void LateUpdate()
        {
            if (_glob != null && !_landed)
            {
                float u = Mathf.Clamp01(Age / Mathf.Max(0.01f, _flight));
                Vector2 to = transform.parent != null ? (Vector2)transform.parent.position : (Vector2)transform.position;
                var p = Vector2.Lerp(_from, to, u) + Vector2.up * Mathf.Sin(u * Mathf.PI) * Tuning.Medusa.VenomArc;
                _glob.Anchor = p - to;
                if (u >= 1f) Land(to);
            }
            base.LateUpdate();
        }

        void Land(Vector2 at)
        {
            _landed = true;
            if (_glob != null) Destroy(_glob.gameObject);
            TexelFx.Puff(at, Venom, 10, 6f, 0.45f);
            if (_target == null) return;
            var feet = ReleaseCanvas.Feet(_target);
            if ((feet - at).sqrMagnitude > _radius * _radius) return;
            var hp = _target.GetComponent<Health>();
            if (hp != null && !hp.IsDead && !hp.Immune) hp.Take(new DamageInfo(_damage, ElementType.Earth, gameObject));
        }

        public static readonly Color32[] Venom = { new(234, 255, 214, 255), new(170, 236, 120, 255), new(96, 186, 72, 255), new(44, 112, 44, 255), new(20, 56, 24, 255) };

        protected override bool Draw()
        {
            if (_landed) return false;
            System.Array.Clear(Px, 0, Px.Length);
            float k = Mathf.Clamp01(Age / _flight), r = _radius * TexelFx.Body;
            Ring(0, 0, r - 1f, r, (x, y, d, a) => Over(x, y, MedusaCanvas.Red[2], 0.5f + 0.5f * k));
            return true;
        }
    }

    /// <summary>The glob in flight - a drop of her venom with a highlight and a few drips behind.</summary>
    public class VenomGlob : TexelPicture
    {
        protected override bool Draw()
        {
            System.Array.Clear(Px, 0, Px.Length);
            var v = VenomShot.Venom;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    Put(dx, dy, Mathf.Abs(dx) + Mathf.Abs(dy) == 2 ? v[3] : v[2]);
            Put(-1, 1, v[0]); Put(0, 1, v[1]);
            foreach (var (dx, dy) in new[] { (-2, 0), (2, 0), (0, -2), (0, 2), (-2, 1), (2, -1), (1, 2), (-1, -2) }) Put(dx, dy, new Color32(13, 11, 14, 255));
            if (FireArt.Hash(Mathf.FloorToInt(Age * 12f), 3) > 0.5f) Put(0, -3, v[2]);
            return true;
        }
    }

    /// <summary>Her third eye fixing a pillar (enraged): a dotted red line from the eye to it, as the
    /// pillar turns to glass.</summary>
    public class SightLine : TexelPicture
    {
        const float Seconds = 0.6f;
        [SerializeField] Vector2 _to;

        public static void Draw(Transform parent, Vector2 from, Vector2 to)
        {
            var go = new GameObject("medusa.sight");
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            var d = (to - from) * TexelFx.Body;
            int hw = Mathf.CeilToInt(Mathf.Abs(d.x)) + 2, hh = Mathf.CeilToInt(Mathf.Abs(d.y)) + 2;
            var p = Attach<SightLine>(go.transform, "line", Vector2.zero, hw, hh, hh, TexelFx.Air);
            p._to = d;
            Destroy(go, Seconds + 0.05f);
        }

        protected override bool Draw()
        {
            float k = Age / Seconds;
            if (k >= 1f) return false;
            System.Array.Clear(Px, 0, Px.Length);
            float reach = Mathf.Clamp01(k / 0.4f);
            Line(0, 0, _to.x, _to.y, (x, y, f) => { if (f <= reach && Mathf.RoundToInt(f * 60f) % 2 == 0) Over(x, y, MedusaCanvas.Red[1], 0.9f * (1f - Mathf.Clamp01((k - 0.7f) / 0.3f))); });
            return true;
        }
    }

    /// <summary>
    /// The pillar she is about to break (the user's pick: CRACKS CLIMB IT): dark cracks run up the
    /// drawn column from its foot over the warning - the clock - drawn at the column's own density,
    /// one order over it. The owner sets <see cref="Climb"/>; Column.Shake does the shudder.
    /// </summary>
    public class PillarCracks : TexelPicture
    {
        [SerializeField] public float Climb;
        [SerializeField] Hazards.Column _col;

        public static PillarCracks On(Hazards.Column col)
        {
            if (col == null || col.Art == null) return null;
            int up = Mathf.CeilToInt(col.DrawnHeight * Hazards.ColumnArt.Ppu) + 8;
            var p = Attach<PillarCracks>(col.transform, "cracks", Vector2.zero, 24, 16, up, col.Art.sortingOrder + 1, Hazards.ColumnArt.Ppu);
            p._col = col;
            return p;
        }

        protected override void LateUpdate()
        {
            if (_col != null && _col.Art != null && Sr != null) Sr.sortingOrder = _col.Art.sortingOrder + 1;
            base.LateUpdate();
        }

        protected override bool Draw()
        {
            if (_col == null || _col.Art == null || Climb <= 0f) return false;
            System.Array.Clear(Px, 0, Px.Length);
            var b = _col.Art.bounds;
            float ppu = Hazards.ColumnArt.Ppu;
            int foot = Mathf.RoundToInt((b.min.y - _col.transform.position.y) * ppu) + 2;
            int top = Mathf.RoundToInt((b.max.y - _col.transform.position.y) * ppu) - 3;
            int half = Mathf.Max(3, Mathf.RoundToInt(b.extents.x * ppu) - 4);
            int reach = Mathf.RoundToInt((top - foot) * Mathf.Clamp01(Climb));
            var dark = new Color32(13, 11, 14, 255);
            var lip = MedusaCanvas.Stone[3];
            for (int s = 0; s < 3; s++)
            {
                int x = (s - 1) * half / 2;
                for (int i = 0; i < reach; i++)
                {
                    int y = foot + i;
                    Put(x, y, dark);
                    if (FireArt.Hash(i, s, 5) > 0.7f) Put(x + 1, y, lip);
                    float h = FireArt.Hash(i, s, 6);
                    x += h > 0.75f ? 1 : h < 0.25f ? -1 : 0;
                    x = Mathf.Clamp(x, -half, half);
                    if (FireArt.Hash(i, s, 9) > 0.93f)   // a short branch
                        for (int j = 1; j <= 3; j++) Put(x + j * (s == 0 ? -1 : 1), y + j, dark);
                }
            }
            return true;
        }
    }

    /// <summary>
    /// One of her adds held as a STATUE (the user's pick: statues wake): grey stone over the
    /// creature's own frames, still, immune, until her crown hisses - then it cracks
    /// (<see cref="Crack"/>) and the shell falls away (<see cref="Wake"/>). The creature's own
    /// controller is switched off meanwhile, so nothing it would do happens.
    /// </summary>
    public class MedusaStatue : MonoBehaviour
    {
        [SerializeField] Enemies.EnemyController _enemy;
        [SerializeField] float _crack;
        [SerializeField] List<SpriteRenderer> _over = new();
        [SerializeField] List<SpriteRenderer> _body = new();

        static Dictionary<(Sprite, int), Sprite> _cracked = new();

        public static MedusaStatue Hold(Enemies.EnemyController e)
        {
            var s = e.gameObject.AddComponent<MedusaStatue>();
            s._enemy = e;
            e.enabled = false;
            var hp = e.GetComponent<Health>();
            if (hp != null) hp.Immune = true;
            var rb = e.GetComponent<Rigidbody2D>();
            if (rb != null) rb.linearVelocity = Vector2.zero;
            foreach (var r in e.GetComponentsInChildren<SpriteRenderer>())
                if (r.sprite != null && r.sortingOrder < SortingOrders.StatusOverlay && r.name != HitFlash.ChildName && r.name != "shadow")
                    s._body.Add(r);
            return s;
        }

        public void Crack(float k) => _crack = Mathf.Clamp01(k);

        public void Wake()
        {
            var at = (Vector2)transform.position;
            TexelFx.Rubble(at, MedusaCanvas.Stone, 0.7f, 10, 0.8f);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Rubble, at, 0.6f);
            foreach (var r in _body) if (r != null) r.forceRenderingOff = false;
            if (_enemy != null) _enemy.enabled = true;
            var hp = GetComponent<Health>();
            if (hp != null) hp.Immune = false;
            Destroy(this);
        }

        void OnDestroy()
        {
            if (_over == null) return;
            foreach (var o in _over) if (o != null) Destroy(o.gameObject);
        }

        void LateUpdate()
        {
            _over ??= new List<SpriteRenderer>();
            for (int i = 0; i < _body.Count; i++)
            {
                var r = _body[i];
                while (_over.Count <= i)
                {
                    var go = new GameObject("statue");
                    go.transform.SetParent(transform, false);
                    _over.Add(go.AddComponent<SpriteRenderer>());
                }
                var o = _over[i];
                if (o == null) continue;
                if (r == null || r.sprite == null) { if (o != null) o.enabled = false; continue; }
                o.sprite = Cracked(r.sprite, Mathf.CeilToInt(_crack * 3f));
                o.flipX = r.flipX; o.flipY = r.flipY;
                o.sortingLayerID = r.sortingLayerID;
                o.sortingOrder = r.sortingOrder;
                o.transform.SetPositionAndRotation(r.transform.position + new Vector3(0f, 0f, -0.001f), r.transform.rotation);
                o.transform.localScale = new Vector3(r.transform.lossyScale.x / Mathf.Max(1e-4f, transform.lossyScale.x),
                                                     r.transform.lossyScale.y / Mathf.Max(1e-4f, transform.lossyScale.y), 1f);
                o.enabled = true;
                r.forceRenderingOff = true;   // the stone is drawn in its place
            }
        }

        /// <summary>The frame in stone, with <paramref name="level"/> (0-3) cracks of black running through it.</summary>
        static Sprite Cracked(Sprite src, int level)
        {
            _cracked ??= new();
            if (_cracked.TryGetValue((src, level), out var done) && done != null) return done;
            var stone = Art.PixelSprite.Stone(src);
            if (stone == null || level <= 0) return _cracked[(src, level)] = stone;
            var rect = stone.rect;
            int w = (int)rect.width, h = (int)rect.height;
            Color32[] px;
            try { px = stone.texture.GetPixels32(); }
            catch { return _cracked[(src, level)] = stone; }
            if (px.Length != stone.texture.width * stone.texture.height) return stone;
            var outPx = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    outPx[y * w + x] = px[((int)rect.y + y) * stone.texture.width + (int)rect.x + x];
            var dark = new Color32(13, 11, 14, 255);
            for (int c = 0; c < level * 2; c++)
            {
                int x = Mathf.RoundToInt(FireArt.Hash(c, 3) * (w - 1)), y = h - 1;
                for (int i = 0; i < h; i++, y--)
                {
                    if (y < 0) break;
                    int idx = y * w + x;
                    if (outPx[idx].a > 0) outPx[idx] = dark;
                    float r = FireArt.Hash(c, i, 7);
                    x = Mathf.Clamp(x + (r > 0.7f ? 1 : r < 0.3f ? -1 : 0), 0, w - 1);
                    if (i > h * (0.35f + 0.2f * level)) break;
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels32(outPx); tex.Apply(false);
            var pivot = new Vector2(stone.pivot.x / w, stone.pivot.y / h);
            return _cracked[(src, level)] = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, stone.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }
}
