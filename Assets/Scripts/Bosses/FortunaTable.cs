using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// Fortuna's room: a casino floor of green baize, every game built into it as a dim gold inlay (the user's
    /// call) - the shell game's ring of cups, the wheel's pockets, the blackjack chips, the bluff's ring of suits.
    /// When she deals a game its inlay RISES: lit gold, its colours come in. One texture over the room at body
    /// density, redrawn at most 15 times a second while something on it moves.
    /// </summary>
    public class FortunaFloor : MonoBehaviour
    {
        public const int Shell = 0, Wheel = 1, Blackjack = 2, Bluff = 3;
        const float Fps = 15f;

        /// <summary>The game whose inlay is up, -1 none; how far up 0..1 (eased by the floor itself).</summary>
        [SerializeField] public int Active = -1;
        [SerializeField] float _rise;
        /// <summary>The wheel's pockets: true hers (gold, a win), false the house's (red, a loss). The cheat flips one.</summary>
        [SerializeField] public bool[] Pocket;
        /// <summary>The pocket a ball has settled in (lit), -1 none.</summary>
        [SerializeField] public int Settled = -1;

        SpriteRenderer _sr;
        Texture2D _tex;
        Color32[] _px, _base;
        int _w, _h;
        float _drawn = -9f, _drawnRise = -1f;
        int _drawnActive = -2, _drawnSettled = -2, _drawnMask = -1;

        static Color32 C(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        static readonly Color32 Baize0 = C(0x164a30), Baize1 = C(0x1e5c3c), Inlay = C(0x7a4a14), Lit = C(0xf2c35c), Bright = C(0xfff2c0);
        static readonly Color32 Red = C(0xa81c30), RedLit = C(0xe03a48), GoldPocket = C(0xc88a2a), Ink = C(0x0a0609);

        public static Vector2 WheelCentre => Tuning.Fortuna.TableCentre + Vector2.down * 0.8f;

        public static FortunaFloor Build(Transform parent)
        {
            var go = new GameObject("fortuna.floor");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<FortunaFloor>();
            var sr = new GameObject("baize").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(go.transform, false);
            sr.sortingOrder = SortingOrders.GroundDecal;
            f._sr = sr;
            f.Pocket = new bool[Tuning.Fortuna.Pockets];
            for (int i = 0; i < f.Pocket.Length; i++) f.Pocket[i] = (i & 1) == 0;
            return f;
        }

        Vector2 World(int x, int y) => new((x - _w / 2 + 0.5f) / TexelFx.Body, (y - _h / 2 + 0.5f) / TexelFx.Body);

        void Ensure()
        {
            var inner = Arena.HalfExtents - Vector2.one * Tuning.Scenery.WallInset;
            int w = 2 * Mathf.CeilToInt(inner.x * TexelFx.Body), h = 2 * Mathf.CeilToInt(inner.y * TexelFx.Body);
            if (_tex != null && _w == w && _h == h && _base != null) return;
            _w = w; _h = h;
            _tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            _px = new Color32[w * h]; _base = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = World(x, y);
                    float n = FireArt.Noise(x * 0.2f, y * 0.2f) * 0.8f + FireArt.Hash(x, y, 2) * 0.12f;
                    var c = Color32.Lerp(Baize0, Baize1, n);
                    float v = Mathf.Max(Mathf.Abs(p.x) / inner.x, Mathf.Abs(p.y) / inner.y);
                    c = Color32.Lerp(c, C(0x050308), Mathf.Clamp01((v - 0.75f) * 1.6f));
                    for (int g = 0; g < 4; g++) if (Shape(g, p, 0f, out _)) { c = Color32.Lerp(c, Inlay, 0.45f); break; }
                    _base[i(x, y)] = c;
                }
            if (_sr.sprite != null) Destroy(_sr.sprite);
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), TexelFx.Body, 0, SpriteMeshType.FullRect);
            _sr.transform.position = Vector3.zero;
            System.Array.Copy(_base, _px, _px.Length);
            _tex.SetPixels32(_px); _tex.Apply();   // a new texture starts opaque grey: fill it before it shows
        }

        int i(int x, int y) => y * _w + x;

        /// <summary>Whether world point <paramref name="p"/> is on game <paramref name="g"/>'s inlay; <paramref name="fill"/>
        /// its colour when risen (default: the gold line).</summary>
        bool Shape(int g, Vector2 p, float rise, out Color32 fill)
        {
            fill = default;
            switch (g)
            {
                case Shell:
                {
                    var d = p - Tuning.Fortuna.TableCentre;
                    float e = Mathf.Sqrt(d.x * d.x / (3.6f * 3.6f) + d.y * d.y / (2.2f * 2.2f));
                    return Mathf.Abs(e - 1f) * 2.2f < 0.06f;
                }
                case Wheel:
                {
                    var d = p - WheelCentre; float r = d.magnitude, R = Tuning.Fortuna.WheelRadius;
                    if (Mathf.Abs(r - R) < 0.05f || Mathf.Abs(r - (R - 0.9f)) < 0.05f || r < 0.12f) return true;
                    if (r > R - 0.9f && r < R)
                    {
                        float a = Mathf.Repeat(Mathf.Atan2(d.y, d.x), Mathf.PI * 2f) / (Mathf.PI * 2f) * Tuning.Fortuna.Pockets;
                        if (Mathf.Abs(a - Mathf.Round(a)) * (r * Mathf.PI * 2f / Tuning.Fortuna.Pockets) < 0.05f) return true;
                        if (rise > 0f)
                        {
                            int k = Mathf.FloorToInt(a) % Tuning.Fortuna.Pockets;
                            bool hers = Pocket != null && k < Pocket.Length && Pocket[k];
                            fill = k == Settled ? (hers ? Bright : RedLit) : hers ? GoldPocket : Red;
                        }
                    }
                    return false;
                }
                case Blackjack:
                    foreach (var c in Tuning.Fortuna.Chips)
                    {
                        float r = (p - c).magnitude, R = Tuning.Fortuna.ChipRadius;
                        if (Mathf.Abs(r - R) < 0.05f) return true;
                        if (r < R && rise > 0f) { float a = Mathf.Atan2(p.y - c.y, p.x - c.x); fill = r > R * 0.7f && Mathf.Repeat(a * 4f / Mathf.PI, 2f) < 1f ? C(0xffffff) : C(0xa81c30); }
                    }
                    return false;
                case Bluff:
                {
                    float r = (p - Vector2.zero).magnitude;
                    return Mathf.Abs(r - 3.0f) < 0.05f || Mathf.Abs(r - 1.0f) < 0.05f;
                }
            }
            return false;
        }

        void LateUpdate()
        {
            if (_sr == null) return;
            Ensure();
            _rise = Mathf.MoveTowards(_rise, Active >= 0 ? 1f : 0f, Time.deltaTime / 0.8f);
            int mask = 0; if (Pocket != null) for (int k = 0; k < Pocket.Length; k++) if (Pocket[k]) mask |= 1 << k;
            bool changed = Active != _drawnActive || Settled != _drawnSettled || mask != _drawnMask || Mathf.Abs(_rise - _drawnRise) > 0.01f;
            float now = Time.time;
            if (!changed && now - _drawn < 0.5f) return;
            if (now - _drawn < 1f / Fps) return;
            _drawn = now; _drawnActive = Active; _drawnSettled = Settled; _drawnMask = mask; _drawnRise = _rise;
            System.Array.Copy(_base, _px, _px.Length);
            if (Active >= 0 && _rise > 0f)
            {
                float glow = 0.75f + 0.25f * Mathf.Sin(now * 4f);
                for (int y = 0; y < _h; y++)
                    for (int x = 0; x < _w; x++)
                    {
                        var p = World(x, y);
                        bool line = Shape(Active, p, _rise, out var fill);
                        int k = i(x, y);
                        if (line) _px[k] = Color32.Lerp(_px[k], Color32.Lerp(Lit, Bright, glow * 0.4f), _rise);
                        else if (fill.a > 0) _px[k] = Color32.Lerp(_px[k], fill, _rise);
                    }
            }
            _tex.SetPixels32(_px); _tex.Apply();
        }
    }

    /// <summary>
    /// A cup of the shell game: copper, upturned on the baize. It can be lifted (the stone shows under it, the mark
    /// on it glowing) and it can shimmer (the last-second switch's honest tell). Struck by the player while the
    /// choice is open - the fight reads <see cref="Struck"/>.
    /// </summary>
    public class FortunaCup : TexelPicture
    {
        [SerializeField] public float Lift, Shimmer;
        [SerializeField] public bool Stone;
        [SerializeField] public bool Struck;
        [SerializeField] CircleCollider2D _col;
        [SerializeField] Health _hp;

        static Color32 C(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        static readonly Color32[] Copper = { C(0xf0b070), C(0xe8a070), C(0xb06a3a), C(0x7a4422), C(0x3e200e) };
        static readonly Color32 StoneC = C(0x6a5f52), StoneLit = C(0xc8bba6), Mark = C(0xff6a5a), Ink = C(0x0a0609);

        public static FortunaCup Make(Transform parent, int index)
        {
            var go = new GameObject("fortuna.cup." + index);
            go.transform.SetParent(parent, false);
            var cup = Attach<FortunaCup>(go.transform, "cup", Vector2.zero, 14, 4, 30, 0);
            cup._col = go.AddComponent<CircleCollider2D>();
            cup._col.radius = Tuning.Fortuna.CupRadius;
            cup._col.enabled = false;
            cup._hp = go.AddComponent<Health>();
            cup._hp.Configure(1f);
            cup._hp.Immovable = true;
            cup.Hook();
            DepthSorted.Attach(go, 0f, false, cup.GetComponent<SpriteRenderer>());
            return cup;
        }

        void Hook() { if (_hp != null) _hp.BlocksHit = _ => { if (_col != null && _col.enabled) Struck = true; return true; }; }

        void Update() { if (_hp != null && _hp.BlocksHit == null) Hook(); }   // a delegate: lost to a domain reload

        /// <summary>The cup's object (the picture is a child, placed by TexelPicture): move and destroy this.</summary>
        public Transform Root => transform.parent != null ? transform.parent : transform;

        public bool Choosable { set { if (_col != null) _col.enabled = value; if (value) Struck = false; } }

        protected override bool Draw()
        {
            float lift = Mathf.Clamp01(Lift) * 14f;
            if (Stone)
            {
                for (int dy = 0; dy <= 4; dy++) for (int dx = -5; dx <= 5; dx++)
                {
                    float e = dx * dx / 25f + (dy - 2) * (dy - 2) / 6f; if (e > 1f) continue;
                    Put(dx, dy, e > 0.7f ? Ink : dx + dy < 1 ? StoneLit : StoneC);
                }
                if (Lift > 0.2f) { Put(0, 3, Mark); Put(-1, 2, Mark); Put(1, 2, Mark); Put(0, 1, Mark); }
            }
            int baseY = Mathf.RoundToInt(lift);
            for (int y = 0; y <= 16; y++)
            {
                float hw = Mathf.Lerp(8f, 5f, y / 16f);
                for (int x = -Mathf.CeilToInt(hw); x <= Mathf.CeilToInt(hw); x++)
                {
                    if (Mathf.Abs(x) > hw) continue;
                    float q = x / hw;
                    var c = Copper[q < -0.5f ? 0 : q > 0.45f ? 3 : y < 2 ? 2 : 1];
                    if (Mathf.Abs(x) > hw - 1f || y == 16) c = Ink;
                    if (y == 2 && Mathf.Abs(x) < hw - 1f) c = Copper[3];
                    if (Shimmer > 0f && FireArt.Hash(x, y, Mathf.FloorToInt(Age * 20f)) > 0.8f) c = Color32.Lerp(c, C(0xffffff), Shimmer);
                    Put(x, baseY + y, c);
                }
            }
            if (lift <= 0.5f) for (int x = -9; x <= 9; x++) Over(x, -1, Ink, 0.35f);
            return true;
        }
    }

    /// <summary>The wheel's ball: ivory, rolling round the pockets. Struck, it reports which way it was knocked.</summary>
    public class FortunaBall : TexelPicture
    {
        [SerializeField] public float Kick;   // +1 / -1 for the last hit's push along the ring, read and cleared by the fight
        [SerializeField] Health _hp;

        static Color32 C(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        static readonly Color32 Ivory = C(0xfffaf0), Shade = C(0xd8ccb4), Ink = C(0x0a0609);

        public static FortunaBall Make(Transform parent, int index)
        {
            var go = new GameObject("fortuna.ball." + index);
            go.transform.SetParent(parent, false);
            var b = Attach<FortunaBall>(go.transform, "ball", Vector2.zero, 8, 2, 14, 0);
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = Tuning.Fortuna.BallRadius;
            b._hp = go.AddComponent<Health>();
            b._hp.Configure(1f);
            b._hp.Immovable = true;
            b.Hook();
            DepthSorted.Attach(go, 0f, false, b.GetComponent<SpriteRenderer>());
            return b;
        }

        void Hook()
        {
            if (_hp == null) return;
            _hp.BlocksHit = info =>
            {
                var from = info.Source != null ? (Vector2)info.Source.transform.position : (Vector2)transform.position + Vector2.down;
                var push = (Vector2)Root.position - from;
                var radial = (Vector2)Root.position - FortunaFloor.WheelCentre;
                var tangent = new Vector2(-radial.y, radial.x);   // counter-clockwise
                Kick = Vector2.Dot(push, tangent) >= 0f ? 1f : -1f;
                Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Chip, (Vector2)transform.position);
                return true;
            };
        }

        void Update() { if (_hp != null && _hp.BlocksHit == null) Hook(); }

        public Transform Root => transform.parent != null ? transform.parent : transform;

        protected override bool Draw()
        {
            for (int y = -5; y <= 5; y++) for (int x = -5; x <= 5; x++)
            {
                float d = x * x + y * y; if (d > 27f) continue;
                Put(x, y + 6, d > 18f ? Ink : x + y > 2 ? Shade : Ivory);
            }
            for (int x = -4; x <= 4; x++) Over(x, 0, Ink, 0.35f);
            return true;
        }
    }

    /// <summary>
    /// Over her head: the TAROT CARD she deals for each game (the user's call - a deck fits her theme), flipping
    /// from its back (the cat and the moon, the user's pick) to its face; and Blackjack's POT, the cards drawn and
    /// their total. At body density.
    /// </summary>
    public class FortunaDeal : TexelPicture
    {
        /// <summary>The card: -1 none, else the game (FortunaFloor.Shell..Bluff); how far into the deal 0..1.</summary>
        [SerializeField] public int Card = -1;
        [SerializeField] public float Flip;
        /// <summary>The pot: the cards drawn, the total; the house's total (-1 none).</summary>
        [SerializeField] public int[] Pot = new int[0];
        [SerializeField] public int Total, House = -1;

        static Color32 C(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        static readonly Color32 Ink = C(0x0a0609), White = C(0xffffff), Paper = C(0xece4d8), Gold = C(0xf2c35c), GoldD = C(0xc88a2a);
        static readonly Color32 Red0 = C(0xe03a48), Red1 = C(0xa81c30), Moon = C(0xfff2c0), Cup = C(0xb06a3a), Sky = C(0x3a6ae0), Grey = C(0x8a8494);

        public static FortunaDeal On(Transform boss)
            => Attach<FortunaDeal>(boss, "deal", new Vector2(0f, 2.15f), 40, 4, 34, SortingOrders.Telegraph);

        // a 3x5 digit font for the pot's total
        static readonly string[] Digits = { "111101101101111", "010110010010111", "111001111100111", "111001111001111", "101101111001001", "111100111001111", "111100111101111", "111001001001001", "111101111101111", "111101111001111" };

        void Number(int n, int x0, int y0, Color32 c)
        {
            var s = Mathf.Max(0, n).ToString();
            int x = x0 - s.Length * 2;
            foreach (var ch in s)
            {
                var d = Digits[ch - '0'];
                for (int r = 0; r < 5; r++) for (int k = 0; k < 3; k++) if (d[r * 3 + k] == '1') { Put(x + k, y0 + 4 - r, c); }
                x += 4;
            }
        }

        protected override bool Draw()
        {
            bool any = false;
            if (Card >= 0 && Flip > 0f && Flip < 1f)
            {
                any = true;
                // up 0..0.25, show the back, flip at 0.35..0.5, show the face, fade 0.9..1
                float sx = Flip < 0.35f ? 1f : Flip < 0.5f ? Mathf.Abs(Mathf.Cos((Flip - 0.35f) / 0.15f * Mathf.PI)) : 1f;
                bool face = Flip >= 0.425f;
                float a = Flip < 0.1f ? Flip / 0.1f : Flip > 0.9f ? (1f - Flip) / 0.1f : 1f;
                const int cw = 16, ch = 24;
                for (int y = 0; y < ch; y++)
                    for (int x = 0; x < cw; x++)
                    {
                        int px = Mathf.RoundToInt((x - cw / 2 + 0.5f) * sx), py = y + 6;
                        Over(px, py, CardPixel(x, y, cw, ch, face), a);
                    }
            }
            if (Pot != null && Pot.Length > 0)
            {
                any = true;
                int n = Pot.Length, x0 = -(n * 6) / 2;
                for (int k = 0; k < n; k++)
                    for (int y = 0; y < 8; y++) for (int x = 0; x < 5; x++)
                        Put(x0 + k * 6 + x, 2 + y, x == 0 || y == 0 || x == 4 || y == 7 ? Ink : (Pot[k] >= 10 ? Paper : White));
                for (int k = 0; k < n; k++) Put(x0 + k * 6 + 2, 5, Pot[k] >= 10 ? Red1 : Red0);
                Number(Total, 2, 12, Total > Tuning.Fortuna.Limit ? Red0 : Gold);
                if (House >= 0) Number(House, 2, 19, Grey);
            }
            return any;
        }

        Color32 CardPixel(int x, int y, int w, int h, bool face)
        {
            if (x == 0 || y == 0 || x == w - 1 || y == h - 1) return Ink;
            if (x == 1 || y == 1 || x == w - 2 || y == h - 2) return face ? GoldD : Gold;
            int lx = x - w / 2, ly = y - h / 2;
            if (!face)
            {
                // the back: a black cat under a gold moon, on red
                var bg = ((x / 2 + y / 2) & 1) == 0 ? Red0 : Red1;
                if (Mathf.Sqrt(lx * lx + (ly - 5) * (ly - 5)) < 3.5f && Mathf.Sqrt((lx - 1.5f) * (lx - 1.5f) + (ly - 6) * (ly - 6)) > 3f) return Moon;
                if (lx * lx / 6f + (ly + 4) * (ly + 4) / 10f < 1f || Mathf.Sqrt(lx * lx + (ly + 0.5f) * (ly + 0.5f)) < 1.6f) return Ink;
                if (ly > 0 && ly < 2 && Mathf.Abs(Mathf.Abs(lx) - 1) < 1) return Ink;
                return bg;
            }
            switch (Card)
            {
                case FortunaFloor.Shell:   // the Magician: three cups on a table
                    if (ly == -4) return Cup;
                    for (int k = -1; k <= 1; k++) if (Mathf.Abs(lx - k * 4) <= 1 && ly > -4 && ly <= -1) return Cup;
                    if (Mathf.Abs(lx) <= 1 && ly > 1 && ly < 8) return ly > 5 ? C(0xf0c8a0) : Red0;
                    return Paper;
                case FortunaFloor.Wheel:   // the Wheel of Fortune
                {
                    float r = Mathf.Sqrt(lx * lx + ly * ly);
                    if (r < 6f && r > 4.8f) return GoldD;
                    if (r < 4.8f && r > 1.5f) return (Mathf.FloorToInt((Mathf.Atan2(ly, lx) + Mathf.PI) / (Mathf.PI * 2f) * 8f) & 1) == 0 ? Red0 : Ink;
                    if (r <= 1.5f) return Gold;
                    return Paper;
                }
                case FortunaFloor.Blackjack:   // the Tower, struck
                    if (Mathf.Abs(lx) <= 2 && ly > -9 && ly < 4) return ((lx + ly) & 3) == 0 ? Grey : C(0x6a6474);
                    if (Mathf.Abs(lx) <= 3 && ly >= 4 && ly < 6) return GoldD;
                    if (lx == 4 - (ly - 2) / 2 && ly > 2 && ly < 10) return White;
                    return Paper;
                default:   // the Moon: a crescent over two towers and the water
                {
                    if (Mathf.Sqrt(lx * lx + (ly - 5) * (ly - 5)) < 3.5f && Mathf.Sqrt((lx - 1.7f) * (lx - 1.7f) + (ly - 5.5f) * (ly - 5.5f)) > 3f) return C(0xe8eaf6);
                    if (Mathf.Abs(Mathf.Abs(lx) - 5) <= 1 && ly > -7 && ly < 0) return Grey;
                    if (ly < -7 && ((lx + ly) & 1) == 0) return Sky;
                    return Paper;
                }
            }
        }
    }

    /// <summary>Her thrown card (chip, Tuning.Fortuna.Card*): a white card edged red, spinning.</summary>
    public static class FortunaCards
    {
        static Sprite[] _frames;
        public static Sprite[] Frames()
        {
            if (_frames != null && _frames.Length > 0 && _frames[0] != null) return _frames;
            var ink = new Color32(10, 6, 9, 255); var white = new Color32(255, 255, 255, 255); var red = new Color32(224, 58, 72, 255);
            _frames = new Sprite[4];
            for (int f = 0; f < 4; f++)
            {
                const int s = 11;
                var px = new Color32[s * s];
                float a = f * Mathf.PI / 4f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
                {
                    float u = (x - 5) * ca + (y - 5) * sa, v = -(x - 5) * sa + (y - 5) * ca;
                    if (Mathf.Abs(u) > 3.6f || Mathf.Abs(v) > 2.4f) continue;
                    px[y * s + x] = Mathf.Abs(u) > 2.8f || Mathf.Abs(v) > 1.7f ? ink : Mathf.Abs(u) > 2f ? red : white;
                }
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels32(px); tex.Apply();
                _frames[f] = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), TexelFx.Body);
            }
            return _frames;
        }
    }
}
