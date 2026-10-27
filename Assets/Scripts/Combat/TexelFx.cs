using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The fight's small one-shot pictures, texel-drawn at body density (the user's group-1 picks
    /// off https://claude.ai/artifact/6HbcifzixP9yEjm777UaDP, 2026-10-26): chips off a hit, a
    /// spark star, a ring opening or closing on the floor, a dotted ripple, a puff of dust, a
    /// shockwave, a soul wisp, a rising plus. They replaced <c>Spr.Flash</c>/<c>Spr.Pulse</c> for
    /// those moments. Each is one canvas (ReleaseCanvas) that lives as long as its picture.
    ///
    /// Where it sorts is the caller's: <see cref="Air"/> over bodies, <see cref="Ground"/> on the
    /// floor (and clipped to standing room), or one under a body (ReleaseCanvas.UnderBody) for
    /// anything at the player - nothing is drawn over the player.
    /// </summary>
    public class TexelFx : ReleaseCanvas
    {
        public const float Body = Enemies.TexelRig.TexelsPerUnit;
        public static int Air => SortingOrders.Fx - 1;
        public static int Ground => SortingOrders.GroundFx;

        enum Kind { Chips, Star, Ring, Puff, Wisp, Plus, Shock, Dust, Strip, Hop, Swirl, Cut, Cone, Bolts, Arc, FlameRing, Spray, Wings, Rise, Crest, Mote, Rubble, FaintRing, Splash, Glint, Motes, BeamDrop, Sift, Flicks }

        [SerializeField] Kind _kind;
        [SerializeField] Color32[] _ramp;
        [SerializeField] float _life, _a, _b, _c, _d;   // per kind - see each Play
        [SerializeField] int _n, _seed;
        [SerializeField] bool _flag;
        [SerializeField] Vector2 _dir;

        static int _serial;

        static TexelFx Make(Kind kind, Vector2 at, int half, int below, int above, int order, Color32[] ramp, float life, bool clip)
        {
            var f = Make<TexelFx>("fx." + kind, at, half, below, above, order, Body, clip);
            f._kind = kind; f._ramp = ramp; f._life = life; f._seed = (_serial++ * 7919) & 0x7fff;
            return f;
        }

        // ---- the pictures ----

        /// <summary>A few single texels thrown off a hit, along <paramref name="dir"/> (spread
        /// either side, radians), falling as they go.</summary>
        public static void Chips(Vector2 at, Color32[] ramp, Vector2 dir, float spread = 1.6f, int n = 7,
                                 float speed = 30f, float life = 0.25f, int order = int.MinValue)
        {
            int half = Mathf.CeilToInt(speed * life) + 3;
            var f = Make(Kind.Chips, at, half, half + 6, half, order == int.MinValue ? Air : order, ramp, life, false);
            f._n = n; f._a = speed; f._b = spread; f._dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up;
        }

        /// <summary>A four-pointed spark of <paramref name="size"/> texels a ray.</summary>
        public static void Star(Vector2 at, Color32[] ramp, int size = 3, float life = 0.1f, int order = int.MinValue)
            => Make(Kind.Star, at, size + 1, size + 1, size + 1, order == int.MinValue ? Air : order, ramp, life, false)._a = size;

        /// <summary>A ring on the floor from <paramref name="from"/> to <paramref name="to"/> world
        /// units over <paramref name="life"/> (shrinking when to is smaller), fading; dotted if asked.</summary>
        public static void Ring(Vector2 at, Color32[] ramp, float from, float to, float life,
                                float width = 1.2f, bool dotted = false, int order = int.MinValue)
        {
            int r = Mathf.CeilToInt(Mathf.Max(from, to) * Body) + 2;
            var f = Make(Kind.Ring, at, r, r, r, order == int.MinValue ? Ground : order, ramp, life, true);
            f._a = from * Body; f._b = to * Body; f._c = width; f._flag = dotted;
        }

        /// <summary>A shockwave: a hard ring to <paramref name="radius"/> with grit thrown up behind
        /// it - a blast's edge, drawn exactly.</summary>
        public static void Shock(Vector2 at, Color32[] ramp, Color32[] grit, float radius, float life = 0.4f, bool dotted = false)
        {
            int r = Mathf.CeilToInt(radius * Body) + 2;
            var f = Make(Kind.Shock, at, r, r, r + 10, Ground, ramp, life, true);
            f._a = radius * Body; f._dir = Vector2.zero;
            f._gritRamp = grit; f._flag = dotted;
        }
        [SerializeField] Color32[] _gritRamp;

        /// <summary>Dust and grit thrown out to <paramref name="radius"/> and settling there.</summary>
        public static void Dust(Vector2 at, Color32[] ramp, float radius, int spokes = 26)
        {
            int r = Mathf.CeilToInt(radius * Body) + 4;
            var f = Make(Kind.Dust, at, r, r, r + 10, Ground, ramp, 0.85f, true);
            f._a = radius * Body; f._n = spokes;
        }

        /// <summary>A puff of dust rising.</summary>
        public static void Puff(Vector2 at, Color32[] ramp, int n = 12, float spread = 7f, float life = 0.5f, int order = int.MinValue)
        {
            int h = Mathf.CeilToInt(spread * 2f) + 4;
            var f = Make(Kind.Puff, at, h, 3, h + 10, order == int.MinValue ? Air : order, ramp, life, false);
            f._n = n; f._a = spread;
        }

        /// <summary>A footfall's two specks of grit flicked back off the heel along
        /// <paramref name="back"/>, arcing and landing (group 7's pick).</summary>
        public static void Flicks(Vector2 at, Color32[] ramp, Vector2 back, int order)
        {
            var f = Make(Kind.Flicks, at, 8, 3, 6, order, ramp, 0.3f, false);
            f._dir = back.sqrMagnitude > 1e-6f ? back.normalized : Vector2.left;
        }

        /// <summary>A pale wisp leaving a body and rising, swaying.</summary>
        public static void Wisp(Vector2 at, Color32[] ramp)
            => Make(Kind.Wisp, at, 4, 2, 44, Air, ramp, 1.25f, false);

        /// <summary>A small plus rising over a head and fading.</summary>
        public static void Plus(Vector2 at, Color32[] ramp, int order)
            => Make(Kind.Plus, at, 3, 3, 14, order, ramp, 0.6f, false);

        /// <summary>A dotted strip running out <paramref name="length"/> along <paramref name="dir"/>,
        /// <paramref name="halfWidth"/> either side - pierce's reach behind a target.</summary>
        public static void Strip(Vector2 at, Color32[] ramp, Vector2 dir, float length, float halfWidth)
        {
            int r = Mathf.CeilToInt((length + halfWidth) * Body) + 2;
            var f = Make(Kind.Strip, at, r, r, r, Ground, ramp, 0.35f, true);
            f._dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right; f._a = length * Body; f._b = halfWidth * Body;
        }

        /// <summary>A hit's chips in the element's colour, thrown on along <paramref name="dir"/>
        /// (the user's pick, 2026-10-26 - "chips"): thrown discs, arrows and blades.</summary>
        public static void Hit(Vector2 at, ElementType element, Vector2 dir)
            => Chips(at, RampOf(ElementInfo.Tint(element)), dir, 1.6f, 7, 30f, 0.25f);

        /// <summary>Small stones hopping once round <paramref name="at"/> - Earth charging.</summary>
        public static void Hop(Vector2 at, Color32[] ramp, float radius, int stones, int order)
        {
            int r = Mathf.CeilToInt(radius * Body) + 3;
            var f = Make(Kind.Hop, at, r, r, r + 6, order, ramp, 0.35f, false);
            f._a = radius * Body; f._n = stones;
        }

        /// <summary>A dashed ring turning round <paramref name="at"/> - Air's streak.</summary>
        public static void Swirl(Vector2 at, Color32[] ramp, float radius, float life, int order)
        {
            int r = Mathf.CeilToInt(radius * Body) + 2;
            Make(Kind.Swirl, at, r, r, r, order, ramp, life, false)._a = radius * Body;
        }

        /// <summary>A cut hanging in the air (the user's pick, group 2 - "cut lines"): a straight
        /// one-texel line <paramref name="halfLength"/> world units either side along
        /// <paramref name="dir"/>, white then the blade's colour, splitting from the middle as it fades.</summary>
        public static void Cut(Vector2 at, Color32[] ramp, Vector2 dir, float halfLength)
        {
            int r = Mathf.CeilToInt(halfLength * Body) + 2;
            var f = Make(Kind.Cut, at, r, r, r, Air, ramp, 0.3f, false);
            f._dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right; f._a = halfLength * Body;
        }

        /// <summary>A cone on the floor (Quintessence - "cone sweep"): wide at the caster, its point
        /// <paramref name="length"/> out along <paramref name="dir"/>, outlined and filled outward.</summary>
        public static void Cone(Vector2 at, Color32[] ramp, Vector2 dir, float length, float baseHalf)
        {
            int r = Mathf.CeilToInt(Mathf.Max(length, baseHalf) * Body) + 2;
            var f = Make(Kind.Cone, at, r, r, r, Ground, ramp, 0.35f, true);
            f._dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right; f._a = length * Body; f._b = baseHalf * Body;
        }

        /// <summary>Short forked bolts jumping off a point for a few frames - the Secret Fire's
        /// moments in the blade lightning's voice (the user's pick, group 2).</summary>
        public static void Bolts(Vector2 at, Color32[] ramp, int bolts = 3, int order = int.MinValue)
            => Make(Kind.Bolts, at, 12, 12, 12, order == int.MinValue ? Air : order, ramp, 0.16f, false)._n = bolts;

        /// <summary>An arc swept round <paramref name="at"/> toward <paramref name="facing"/> -
        /// Shadow's echo following its swing a beat behind.</summary>
        public static void Arc(Vector2 at, Color32[] ramp, Vector2 facing, float radius, int order, float halfSpan = 1.2f)
        {
            int r = Mathf.CeilToInt(radius * Body) + 3;
            var f = Make(Kind.Arc, at, r, r, r, order, ramp, 0.25f, false);
            f._dir = facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector2.right; f._a = radius * Body; f._b = halfSpan;
        }

        /// <summary>A ring of fire running out to <paramref name="radius"/> along the floor with
        /// flames standing on it (Emberline's ground fire, Erupt's ring at the blade).</summary>
        public static void FlameRing(Vector2 at, Color32[] ramp, float radius)
        {
            int r = Mathf.CeilToInt(radius * Body) + 2;
            Make(Kind.FlameRing, at, r, r, r + 8, Ground, ramp, 0.45f, true)._a = radius * Body;
        }

        // ---- group 3: the exchange and the board (the user's picks, 2026-10-26) ----

        /// <summary>What a thing is made of, thrown out round <paramref name="at"/> to
        /// <paramref name="radius"/> ("its own stuff", "status bursts off", "plates fly off"):
        /// single drops, or two-texel SHARDS, falling if <paramref name="falls"/>.</summary>
        public static void Spray(Vector2 at, Color32[] ramp, float radius, int n = 18, float life = 0.45f,
                                 bool falls = true, bool shards = false, int order = int.MinValue)
        {
            int r = Mathf.CeilToInt(radius * Body) + 3;
            var f = Make(Kind.Spray, at, r, r + 12, r, order == int.MinValue ? Air : order, ramp, life, false);
            f._a = radius * Body; f._n = n; f._flag = shards; f._b = falls ? 1f : 0f;
        }

        /// <summary>Two wings spreading from behind the shoulders, beating once and folding
        /// (Second Wind in gold, the Phoenix in flame). Sorted under the body.</summary>
        public static void Wings(Vector2 shoulders, Color32[] ramp, int order)
            => Make(Kind.Wings, shoulders, 24, 4, 22, order, ramp, 0.8f, false);

        /// <summary>One spark rising from the feet to over the head and popping - "something
        /// readied". Sorted under the body, so it passes behind it.</summary>
        public static void Rise(Vector2 feet, Color32[] ramp, float height, int order)
            => Make(Kind.Rise, feet, 4, 2, Mathf.CeilToInt(height * Body) + 5, order, ramp, 0.45f, false)._a = height * Body;

        /// <summary>A crest running <paramref name="length"/> along <paramref name="dir"/> on the
        /// floor and breaking at its end, foam behind it ("running crest" - Riptide, Spindrift,
        /// Felicity, Lightfoot's cut).</summary>
        public static void Crest(Vector2 at, Color32[] ramp, Vector2 dir, float length, float life = 0.35f)
        {
            int r = Mathf.CeilToInt(length * Body) + 8;
            var f = Make(Kind.Crest, at, r, r, r, Ground, ramp, life, true);
            f._dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right; f._a = length * Body;
        }

        // ---- group 4: the room (the user's picks, 2026-10-26) ----

        /// <summary>A mote of light flying from <paramref name="from"/> to <paramref name="to"/> on
        /// a low arc over <paramref name="life"/>, then <paramref name="arrive"/> - a spire's boon
        /// going into the player ("to you").</summary>
        public static void Mote(Vector2 from, Vector2 to, Color32[] ramp, float life, System.Action arrive, int order)
        {
            var d = (to - from) * Body;
            int half = Mathf.CeilToInt(Mathf.Abs(d.x)) + 4, below = Mathf.CeilToInt(Mathf.Max(0f, -d.y)) + 4, above = Mathf.CeilToInt(Mathf.Max(0f, d.y)) + 18;
            var f = Make(Kind.Mote, from, half, below, above, order, ramp, life, false);
            f._dir = d; f._arrive = arrive;
        }
        System.Action _arrive;

        /// <summary>Stone breaking (the user's pick, group 4 - "chunks fly"): blocks thrown out from
        /// up to <paramref name="height"/> world units above <paramref name="foot"/>, landing and
        /// rolling, a low puff of dust; sorted like a thing standing at the foot. The caller names
        /// the material (<see cref="RubbleOf"/>).</summary>
        public static void Rubble(Vector2 foot, Color32[] ramp, float height, int n = 10, float reach = 1f)
        {
            int up = Mathf.CeilToInt(height * Body) + 10, half = Mathf.CeilToInt(26f * reach) + 6;
            var f = Make(Kind.Rubble, foot, half, 12, up, SortingOrders.ForDepth(foot.y) + SortingOrders.DepthStride - 1, ramp, 1.3f, false);
            f._a = height * Body; f._n = n; f._b = reach;
        }

        /// <summary>The rubble's ramp in the floor's biome: the room's stone, the forest's wood, salt
        /// on the flats, marble in the sky ruin - "made of the place". Cracked stone is lighter and warmer.</summary>
        public static Color32[] RubbleOf(bool cracked = true)
        {
            if (Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest))
                return new[] { C(196, 168, 120), C(150, 118, 78), C(112, 84, 52), C(78, 60, 36), C(92, 130, 56), C(40, 30, 18) };
            if (Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SaltFlats))
                return new[] { C(255, 255, 255), C(244, 242, 236), C(222, 218, 210), C(190, 184, 178), C(150, 144, 140), C(100, 96, 94) };
            if (Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SkyRuin))
                return new[] { C(250, 248, 242), C(226, 222, 212), C(198, 192, 180), C(160, 154, 142), C(118, 112, 104), C(70, 66, 60) };
            return cracked ? new[] { C(232, 220, 196), C(200, 184, 156), C(164, 148, 120), C(124, 110, 88), C(84, 74, 60), C(46, 40, 32) }
                           : new[] { C(184, 178, 168), C(143, 137, 127), C(108, 102, 94), C(76, 72, 66), C(51, 48, 44), C(28, 26, 24) };
        }

        /// <summary>A thin, faint ring spreading on water - a step in a water pit or a lake. Broken,
        /// so it never reads as Water's tier ripple, which is one crisp ring.</summary>
        public static void FaintRing(Vector2 at, Color32[] ramp, float from, float to, float life = 0.9f)
        {
            int r = Mathf.CeilToInt(to * Body) + 2;
            var f = Make(Kind.FaintRing, at, r, r, r, Ground, ramp, life, false);
            f._a = from * Body; f._b = to * Body;
        }

        /// <summary>A body going into water: a crown of drops thrown up and falling back, and a broken
        /// ring of foam left on the surface.</summary>
        public static void Splash(Vector2 at, Color32[] ramp, float size = 1f, int order = int.MinValue)
        {
            int r = Mathf.CeilToInt(12f * size) + 4;
            var f = Make(Kind.Splash, at, r, r, r + 16, order == int.MinValue ? SortingOrders.ForDepth(at.y) + SortingOrders.DepthStride - 1 : order, ramp, 1f, false);
            f._a = size;
        }

        /// <summary>An X-shaped glint (the user's pick, group 4 - "cross glint"): two blades meeting.</summary>
        public static void Glint(Vector2 at, Color32[] ramp, int size = 5, float life = 0.15f)
            => Make(Kind.Glint, at, size + 1, size + 1, size + 1, Air, ramp, life, false)._a = size;

        /// <summary>Motes rising from the floor round <paramref name="feet"/> to <paramref name="height"/>
        /// world units over a second - a heal taken. Sorted by the caller (under the body).</summary>
        public static void Motes(Vector2 feet, Color32[] ramp, float height, int order, int n = 24)
        {
            int up = Mathf.CeilToInt(height * Body) + 4;
            var f = Make(Kind.Motes, feet, 12, 6, up, order, ramp, 1f, false);
            f._a = height * Body; f._n = n;
        }

        /// <summary>A pickup's beam (Rifts.LootBeam) falling back into it, then a pop of sparks -
        /// the thing taken.</summary>
        public static void BeamDrop(Vector2 foot, Color32[] ramp, float height, int order)
        {
            int up = Mathf.CeilToInt(height * Body) + 4;
            Make(Kind.BeamDrop, foot, 10, 10, up, order, ramp, 0.6f, false)._a = height * Body;
        }

        /// <summary>Dust sifting straight down from a line <paramref name="halfWidth"/> world units either
        /// side of <paramref name="from"/>, falling <paramref name="fall"/> - from a door's lintel as it
        /// parts, down its seam as it seals.</summary>
        public static void Sift(Vector2 from, Color32[] ramp, float halfWidth, float fall, int n = 12, float life = 0.9f, int order = int.MinValue)
        {
            int hw = Mathf.CeilToInt(halfWidth * Body) + 2, down = Mathf.CeilToInt(fall * Body) + 2;
            var f = Make(Kind.Sift, from, hw, down, 2, order == int.MinValue ? Air : order, ramp, life, false);
            f._a = halfWidth * Body; f._b = fall * Body; f._n = n;
        }

        /// <summary>The Secret Fire's three tones (core, vein, ember) for the element shown.</summary>
        public static Color32[] SecretTones() => SecretTones(Art.Gear.SecretFire.Shown);

        public static Color32[] SecretTones(ElementType e)
        {
            return new Color32[] { Art.Gear.SecretFire.Tone(e, 0), Art.Gear.SecretFire.Tone(e, 1), Art.Gear.SecretFire.Tone(e, 2) };
        }

        static System.Collections.Generic.Dictionary<int, Sprite> _ringSprites = new();

        /// <summary>A crisp white ring at <paramref name="radius"/> world units, one texel and a bit
        /// thick, at body density - for a ring that stays (a landing mark, Arrow Rain's area);
        /// tint it with the renderer. Cached per size.</summary>
        public static Sprite RingSprite(float radius)
        {
            int rT = Mathf.Max(2, Mathf.RoundToInt(radius * Body));
            if (_ringSprites.TryGetValue(rT, out var hit) && hit != null) return hit;
            int n = rT * 2 + 3;
            var px = new Color32[n * n];
            float c = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= rT && d >= rT - 1.2f) px[y * n + x] = new Color32(255, 255, 255, 255);
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();
            return _ringSprites[rT] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), Body, 0, SpriteMeshType.FullRect);
        }

        static System.Collections.Generic.Dictionary<int, Sprite> _faintRings = new();

        /// <summary>A thin BROKEN ring of <paramref name="radius"/> world units at body density, white
        /// (tint it with the renderer) - water's ripples (the user's pick, group 4 - "faint rings"),
        /// broken so they never read as Water's tier ripple, one crisp ring. Cached per size.</summary>
        public static Sprite FaintRingSprite(float radius)
        {
            int rT = Mathf.Clamp(Mathf.RoundToInt(radius * Body), 1, 200);
            if (_faintRings.TryGetValue(rT, out var hit) && hit != null) return hit;
            int n = rT * 2 + 3;
            var px = new Color32[n * n];
            float c = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > rT || d < rT - 0.9f) continue;
                    if (rT > 2 && (Mathf.FloorToInt((Mathf.Atan2(dy, dx) + Mathf.PI) * 6f / Mathf.PI) & 1) == 0) continue;
                    px[y * n + x] = new Color32(255, 255, 255, 255);
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();
            return _faintRings[rT] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), Body, 0, SpriteMeshType.FullRect);
        }

        // ---- drawing ----

        protected override bool Draw()
        {
            if (Age > _life || _ramp == null || _ramp.Length == 0) { _arrive?.Invoke(); _arrive = null; return false; }
            float u = Age / _life;
            switch (_kind)
            {
                case Kind.Chips:
                    for (int k = 0; k < _n; k++)
                    {
                        float a = Mathf.Atan2(_dir.y, _dir.x) + (FireArt.Hash(k, _seed, 1) - 0.5f) * _b;
                        float v = _a * (0.6f + FireArt.Hash(k, _seed, 2) * 0.6f);
                        float x = Mathf.Cos(a) * v * Age, y = Mathf.Sin(a) * v * Age - 30f * Age * Age;
                        Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y), At(_ramp, 0.1f + u * 0.6f));
                    }
                    return true;

                case Kind.Star:
                {
                    int r = u < 0.4f ? Mathf.RoundToInt(_a) : Mathf.Max(1, Mathf.RoundToInt(_a * 0.6f));
                    Put(0, 0, _ramp[0]);
                    for (int i = 1; i <= r; i++)
                    {
                        var c = At(_ramp, i / (float)(r + 1) * 0.8f);
                        Put(i, 0, c); Put(-i, 0, c); Put(0, i, c); Put(0, -i, c);
                    }
                    if (r >= 3) { var c = At(_ramp, 0.5f); Put(1, 1, c); Put(-1, -1, c); Put(1, -1, c); Put(-1, 1, c); }
                    return true;
                }

                case Kind.Ring:
                {
                    float r = Mathf.Lerp(_a, _b, u);
                    var col = At(_ramp, 0.1f + u * 0.5f);
                    float fade = 1f - u * 0.6f;
                    Ring(0, 0, r - _c, r, (x, y, d, ang) =>
                    {
                        if (_flag && Mathf.Repeat((ang + Mathf.PI) * r / 3f, 2f) > 1f) return;
                        Over(x, y, col, fade);
                    });
                    return true;
                }

                case Kind.Shock:
                {
                    float k = Mathf.Clamp01(Age / 0.22f), r = _a * k;
                    if (Age < 0.35f) Ring(0, 0, r - 2.5f, r, (x, y, d, ang) =>
                    {
                        if (_flag && Mathf.Repeat((ang + Mathf.PI) * r / 3f, 2f) > 1f) return;   // dotted: the player's own
                        Over(x, y, At(_ramp, 0.1f + k * 0.4f), 1f - Mathf.Clamp01((Age - 0.22f) / 0.13f));
                    });
                    var grit = _gritRamp != null && _gritRamp.Length > 0 ? _gritRamp : _ramp;
                    int spokes = Mathf.Clamp(Mathf.RoundToInt(_a * 0.6f), 12, 40);
                    for (int s = 0; s < spokes; s++)
                    {
                        float ang = s / (float)spokes * Mathf.PI * 2f, rr = Mathf.Max(0f, r - 3f);
                        PuffAt(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr, Age - 0.05f, 2, s, grit, 0.4f, 3f);
                    }
                    return true;
                }

                case Kind.Dust:
                {
                    float k = Mathf.Clamp01(Age / 0.2f);
                    for (int s = 0; s < _n; s++)
                    {
                        float ang = s / (float)_n * Mathf.PI * 2f + FireArt.Hash(s, _seed) * 0.2f;
                        float x = Mathf.Cos(ang) * _a * k, y = Mathf.Sin(ang) * _a * k;
                        PuffAt(x, y, Age - 0.15f, 2, s, _ramp, 0.6f, 3f);
                        if (Age < 0.2f) Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y) + 1, _ramp[1]);
                    }
                    return true;
                }

                case Kind.Puff:
                    PuffAt(0f, 0f, Age, _n, _seed, _ramp, _life, _a);
                    return true;

                case Kind.Flicks:
                    for (int k = 0; k < 2; k++)
                    {
                        float v = 10f + k * 6f, lift = Mathf.Max(0f, (12f * Age - 40f * Age * Age) * (1f + k * 0.4f));
                        Over(Mathf.RoundToInt(_dir.x * v * Age), Mathf.RoundToInt(_dir.y * v * Age * 0.6f + lift), At(_ramp, 0.2f + k * 0.2f));
                    }
                    return true;

                case Kind.Wisp:
                    for (int i = 0; i < 8; i++)
                    {
                        float y = 8f + Age * 26f + i, x = Mathf.Sin(Age * 5f + i * 0.4f) * 2f;
                        Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y), At(_ramp, 0.05f + i / 8f * 0.5f), 1f - u);
                    }
                    return true;

                case Kind.Plus:
                {
                    int y = Mathf.RoundToInt(Age * 16f);
                    var c = At(_ramp, 0.1f + u * 0.6f);
                    for (int i = -2; i <= 2; i++) { Over(i, y, c, 1f - u * u); Over(0, y + i, c, 1f - u * u); }
                    return true;
                }

                case Kind.Hop:
                    for (int k = 0; k < _n; k++)
                    {
                        float ang = k / (float)_n * Mathf.PI * 2f + _seed * 0.01f;
                        float hop = Mathf.Sin(Mathf.Clamp01(u * 1.2f - FireArt.Hash(k, _seed) * 0.2f) * Mathf.PI) * 3f;
                        int x = Mathf.RoundToInt(Mathf.Cos(ang) * _a), y = Mathf.RoundToInt(Mathf.Sin(ang) * _a * 0.5f + hop);
                        Over(x, y, _ramp[2]); Over(x + 1, y, _ramp[3]);
                    }
                    return true;

                case Kind.Swirl:
                    Ring(0, 0, _a - 1.5f, _a, (x, y, d, ang) =>
                    {
                        if (Mathf.Repeat((ang + Age * 6f) * 8f / (Mathf.PI * 2f), 1f) < 0.45f) Over(x, y, _ramp[0], 1f - u * u);
                    });
                    return true;

                case Kind.Cut:
                {
                    int len = Mathf.RoundToInt(_a);
                    var col = u < 0.3f ? _ramp[0] : At(_ramp, 0.4f);
                    float gap = u > 0.5f ? (u - 0.5f) * 0.6f * len : -1f;
                    for (int i = -len; i <= len; i++)
                    {
                        if (Mathf.Abs(i) < gap) continue;
                        Over(Mathf.RoundToInt(_dir.x * i), Mathf.RoundToInt(_dir.y * i), col, 1f - u * 0.6f);
                    }
                    return true;
                }

                case Kind.Cone:
                {
                    float reach = _a * Mathf.Clamp01(Age / 0.18f), fade = 1f - Mathf.Clamp01((Age - 0.18f) / 0.17f);
                    var n = new Vector2(-_dir.y, _dir.x);
                    for (int r = 1; r <= reach; r++)
                    {
                        float half = _b * (1f - r / Mathf.Max(1f, _a));
                        for (float side = -half; side <= half; side += 1f)
                        {
                            var p = _dir * r + n * side;
                            int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y);
                            bool edge = Mathf.Abs(side) > half - 1f || r > reach - 1.5f;
                            if (!edge && FireArt.Hash(x, y, _seed) > 0.45f * (1f - r / _a)) continue;
                            Over(x, y, edge ? _ramp[0] : At(_ramp, 0.4f), fade);
                        }
                    }
                    return true;
                }

                case Kind.Bolts:
                {
                    int f = Mathf.FloorToInt(Time.time * 30f);
                    for (int k = 0; k < _n; k++)
                    {
                        float x = 0f, y = 0f, ang = FireArt.Hash(k, f, _seed) * Mathf.PI * 2f;
                        for (int st = 0; st < 9; st++)
                        {
                            ang += (FireArt.Hash(k, st, f) - 0.5f) * 1.6f;
                            x += Mathf.Cos(ang); y += Mathf.Sin(ang);
                            Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y), _ramp[Mathf.Min(_ramp.Length - 1, st / 3)]);
                        }
                    }
                    return true;
                }

                case Kind.Arc:
                {
                    float face = Mathf.Atan2(_dir.y, _dir.x), sweep = Mathf.Min(1f, u * 2f);
                    for (int i = 0; i <= 40; i++)
                    {
                        float a = face - _b + 2f * _b * (i / 40f) * sweep;
                        for (int w = 0; w < 3; w++)
                        {
                            float rr = _a - w;
                            Over(Mathf.RoundToInt(Mathf.Cos(a) * rr), Mathf.RoundToInt(Mathf.Sin(a) * rr * 0.6f), _ramp[Mathf.Min(_ramp.Length - 1, 1 + w)], 1f - u * 0.7f);
                        }
                    }
                    return true;
                }

                case Kind.FlameRing:
                {
                    float r = _a * Mathf.Clamp01(Age / 0.3f);
                    int frame = Mathf.FloorToInt(Time.time * 14f);
                    Ring(0, 0, r - 3f, r, (x, y, d, ang) =>
                    {
                        if (FireArt.Hash(x, y, frame) < 0.25f) return;
                        Over(x, y, At(_ramp, 0.15f + u * 0.5f));
                        if (d < r - 0.8f) return;
                        int h = 2 + Mathf.FloorToInt(FireArt.Noise(x * 0.4f, Time.time * 8f) * 5f);
                        for (int i = 1; i < h; i++)
                            if (FireArt.Hash(x, i, frame) > 0.35f) Over(x, y + i, At(_ramp, 0.1f + i / (float)h * 0.6f));
                    });
                    return true;
                }

                case Kind.Spray:
                    for (int k = 0; k < _n; k++)
                    {
                        float a = k / (float)_n * Mathf.PI * 2f + FireArt.Hash(k, _seed) * 0.35f;
                        float v = _a / (_life * 0.7f) * (0.75f + FireArt.Hash(k, _seed, 2) * 0.4f);
                        float x = Mathf.Cos(a) * v * Age, y = Mathf.Sin(a) * v * Age * 0.6f + 2f - _b * 60f * Age * Age;
                        if (u > 0.7f && FireArt.Hash(k, _seed, Mathf.FloorToInt(Time.time * 12f)) > 0.5f) continue;
                        var c = At(_ramp, 0.12f + u * 0.7f);
                        Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y), c);
                        if (_flag) Over(Mathf.RoundToInt(x + Mathf.Cos(a)), Mathf.RoundToInt(y + Mathf.Sin(a) * 0.6f), At(_ramp, 0.05f));
                    }
                    return true;

                case Kind.Wings:
                {
                    float open = Mathf.Sin(u * Mathf.PI);
                    for (int side = -1; side <= 1; side += 2)
                        for (int i = 0; i < 18; i++)
                        {
                            int len = 3 + Mathf.RoundToInt(9f * open * Mathf.Sin(i / 18f * Mathf.PI));
                            for (int j = 0; j < len; j++)
                            {
                                if (j == len - 1 && FireArt.Hash(i, side, Mathf.FloorToInt(Time.time * 14f)) > 0.6f) continue;
                                Over(side * (3 + i), Mathf.RoundToInt(j + i * 0.45f), At(_ramp, j / (float)len * 0.9f));
                            }
                        }
                    return true;
                }

                case Kind.Rise:
                    if (u < 0.75f) { int y = Mathf.RoundToInt(_a * u / 0.75f); Put(0, y, _ramp[1]); Over(0, y - 1, _ramp[2], 0.6f); }
                    else
                    {
                        int y = Mathf.RoundToInt(_a), r = u < 0.87f ? 2 : 1;
                        Put(0, y, _ramp[0]);
                        for (int i = 1; i <= r; i++) { var c = At(_ramp, i * 0.3f); Put(i, y, c); Put(-i, y, c); Put(0, y + i, c); Put(0, y - i, c); }
                    }
                    return true;

                case Kind.Crest:
                {
                    float head = _a * Mathf.Clamp01(Age / (_life * 0.85f));
                    var n = new Vector2(-_dir.y, _dir.x);
                    float fade = 1f - Mathf.Clamp01((Age - _life * 0.7f) / (_life * 0.3f));
                    for (int dy = -6; dy <= 6; dy++)
                    {
                        float bend = dy * dy / 9f;
                        for (int w = 0; w < 2; w++)
                        {
                            var p = _dir * (head - bend - w) + n * dy;
                            Over(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), w == 0 ? _ramp[1] : _ramp[3], fade);
                        }
                    }
                    for (int k = 0; k < 14; k++)
                    {
                        float back = 4f + k * 2.2f + FireArt.Hash(k, _seed) * 3f;
                        if (back > head) continue;
                        var p = _dir * (head - back) + n * ((FireArt.Hash(k, _seed, 1) - 0.5f) * 9f);
                        Over(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), _ramp[2], 0.7f * fade);
                    }
                    return true;
                }

                case Kind.Mote:
                {
                    float x = _dir.x * u, y = _dir.y * u + Mathf.Sin(u * Mathf.PI) * 14f;
                    for (int i = 4; i >= 0; i--)
                    {
                        float uu = Mathf.Max(0f, u - i * 0.04f);
                        int tx = Mathf.RoundToInt(_dir.x * uu), ty = Mathf.RoundToInt(_dir.y * uu + Mathf.Sin(uu * Mathf.PI) * 14f);
                        Over(tx, ty, At(_ramp, 0.15f + i * 0.15f), 1f - i * 0.18f);
                    }
                    int mx = Mathf.RoundToInt(x), my = Mathf.RoundToInt(y);
                    Put(mx, my, _ramp[0]); Put(mx + 1, my, _ramp[1]); Put(mx - 1, my, _ramp[1]); Put(mx, my + 1, _ramp[1]); Put(mx, my - 1, _ramp[1]);
                    return true;
                }

                case Kind.Rubble:
                {
                    for (int k = 0; k < _n; k++)
                    {
                        float side = FireArt.Hash(k, _seed, 1) < 0.5f ? -1f : 1f;
                        float v = (14f + FireArt.Hash(k, _seed, 2) * 16f) * _b, up = 10f + FireArt.Hash(k, _seed, 3) * 18f;
                        float y0 = _a * (0.2f + 0.8f * FireArt.Hash(k, _seed, 4)), ground = (FireArt.Hash(k, _seed, 5) - 0.6f) * 6f;
                        // Flight under gravity; once down it rolls a little further and rests.
                        float tLand = (up + Mathf.Sqrt(up * up + 2f * 90f * (y0 - ground))) / 90f;
                        float t = Mathf.Min(Age, tLand), x = side * v * t, y = y0 + up * t - 45f * t * t;
                        if (Age > tLand) { x += side * v * 0.25f * Mathf.Min(Age - tLand, 0.25f); y = ground; }
                        float fade = 1f - Mathf.Clamp01((u - 0.7f) / 0.3f);
                        // A block three texels wide: lit top, dark underside - it must read on a floor
                        // of its own stone - and a shadow under it once it rests.
                        int ix = Mathf.RoundToInt(x), iy = Mathf.RoundToInt(y);
                        if (Age > tLand) { Over(ix, iy - 2, _ramp[5], 0.5f * fade); Over(ix + 1, iy - 2, _ramp[5], 0.5f * fade); Over(ix + 2, iy - 2, _ramp[5], 0.5f * fade); }
                        Over(ix, iy, _ramp[1], fade); Over(ix + 1, iy, _ramp[1], fade); Over(ix + 2, iy, _ramp[2], fade);
                        Over(ix, iy - 1, _ramp[3], fade); Over(ix + 1, iy - 1, _ramp[4], fade); Over(ix + 2, iy - 1, _ramp[4], fade);
                    }
                    PuffAt(0f, 0f, Age, Mathf.RoundToInt(16 * _b), _seed, _ramp, 0.7f, 12f * _b);
                    return true;
                }

                case Kind.FaintRing:
                {
                    float r = Mathf.Lerp(_a, _b, u);
                    var col = At(_ramp, 0.1f);
                    Ring(0, 0, r - 0.8f, r, (x, y, d, ang) =>
                    {
                        if ((Mathf.FloorToInt((ang + Mathf.PI) * 6f / Mathf.PI) & 1) == 0) return;
                        Over(x, y, col, 0.75f * (1f - u));
                    });
                    return true;
                }

                case Kind.Splash:
                {
                    if (Age < 0.7f)
                        for (int k = 0; k < 10; k++)
                        {
                            float a = k / 10f * Mathf.PI * 2f, v = 9f * _a, t = Age;
                            float x = Mathf.Cos(a) * v * t, y = Mathf.Sin(a) * v * t * 0.5f + (18f * t - 40f * t * t) * _a * 1.6f;
                            Put(Mathf.RoundToInt(x), Mathf.RoundToInt(y), (k & 1) == 0 ? _ramp[0] : _ramp[1]);
                        }
                    if (Age > 0.2f)
                    {
                        float s = Age - 0.2f, r = (5f + s * 6f) * _a;
                        Ring(0, 0, r - 1.2f, r, (x, y, d, ang) =>
                        {
                            if (FireArt.Hash(x, y, _seed) < 0.45f) return;
                            Over(x, y, _ramp[0], 0.8f * (1f - Mathf.Clamp01(s / 0.8f)));
                        });
                    }
                    return true;
                }

                case Kind.Glint:
                {
                    int r = Mathf.RoundToInt(_a * (u < 0.5f ? 1f : 0.6f));
                    Put(0, 0, _ramp[0]);
                    for (int i = 1; i <= r; i++)
                    {
                        var c = At(_ramp, i / (float)(r + 1) * 0.8f);
                        Put(i, i, c); Put(-i, -i, c); Put(i, -i, c); Put(-i, i, c);
                    }
                    return true;
                }

                case Kind.Motes:
                    for (int k = 0; k < _n; k++)
                    {
                        float age = u * 1.3f - FireArt.Hash(k, _seed, 1) * 0.4f;
                        if (age < 0f || age > 0.8f) continue;
                        float a = FireArt.Hash(k, _seed, 2) * Mathf.PI * 2f;
                        Over(Mathf.RoundToInt(Mathf.Cos(a) * 9f), Mathf.RoundToInt(Mathf.Sin(a) * 3f + age / 0.8f * _a),
                             At(_ramp, 0.1f + age), 1f - age / 0.8f);
                    }
                    return true;

                case Kind.BeamDrop:
                {
                    float fall = Mathf.Clamp01(Age / 0.22f), top = _a * (1f - fall);
                    for (int y = 0; y < top; y++) Over(0, y, (y & 3) < 2 ? _ramp[1] : _ramp[3], 0.9f * (1f - y / Mathf.Max(1f, _a)));
                    float pop = Age - 0.22f;
                    if (pop >= 0f)
                    {
                        if (pop < 0.08f) { Put(0, 2, _ramp[0]); for (int i = 1; i <= 3; i++) { var c = At(_ramp, i * 0.2f); Put(i, 2, c); Put(-i, 2, c); Put(0, 2 + i, c); Put(0, 2 - i, c); } }
                        for (int k = 0; k < 10; k++)
                        {
                            float a = FireArt.Hash(k, _seed, 1) * Mathf.PI * 2f, v = 18f + FireArt.Hash(k, _seed, 2) * 14f;
                            Put(Mathf.RoundToInt(Mathf.Cos(a) * v * pop), Mathf.RoundToInt(2f + Mathf.Sin(a) * v * pop - 40f * pop * pop), At(_ramp, 0.1f + pop * 1.5f));
                        }
                    }
                    return true;
                }

                case Kind.Sift:
                    for (int k = 0; k < _n; k++)
                    {
                        float start = FireArt.Hash(k, _seed, 1) * 0.5f, t = Age - start;
                        if (t < 0f) continue;
                        float y = -(t * 18f + 30f * t * t);
                        if (-y > _b) continue;
                        int x = Mathf.RoundToInt((FireArt.Hash(k, _seed, 2) * 2f - 1f) * _a);
                        Over(x, Mathf.RoundToInt(y), At(_ramp, 0.2f + FireArt.Hash(k, _seed, 3) * 0.5f), 1f - Mathf.Clamp01(-y / _b));
                    }
                    return true;

                case Kind.Strip:
                {
                    float k = Mathf.Clamp01(Age / 0.2f), len = _a * k;
                    var n = new Vector2(-_dir.y, _dir.x);
                    var col = At(_ramp, 0.2f + u * 0.6f);
                    for (int s = 0; s < len; s += 3)
                        foreach (int side in new[] { -1, 1 })
                        {
                            var p = _dir * s + n * (_b * side);
                            Over(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), col, 1f - u);
                        }
                    return true;
                }
            }
            return false;
        }

        void PuffAt(float x0, float y0, float since, int n, int seed, Color32[] ramp, float life, float spread)
        {
            for (int k = 0; k < n; k++)
            {
                float l = life * (0.7f + FireArt.Hash(k, seed, 1) * 0.5f);
                if (since < 0f || since > l) continue;
                float u = since / l;
                if (u > 0.7f && FireArt.Hash(k, seed, Mathf.FloorToInt(Time.time * 12f)) > 0.5f) continue;
                float x = x0 + (FireArt.Hash(k, seed, 2) - 0.5f) * spread * 2f * (0.4f + u);
                float y = y0 + u * (3f + FireArt.Hash(k, seed, 3) * 6f);
                Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y), At(ramp, 0.2f + u * 0.6f), 0.85f);
            }
        }

        // ---- ramps the group shares ----

        static Color32 C(byte r, byte g, byte b) => new(r, g, b, 255);
        public static readonly Color32[] Fire = Tuning.FireFx.Ramp;
        public static readonly Color32[] Steel = { C(240, 243, 247), C(196, 202, 211), C(142, 151, 166), C(93, 101, 115), C(55, 60, 70), C(28, 31, 37) };
        public static readonly Color32[] Gold = { C(255, 248, 224), C(255, 227, 154), C(242, 194, 90), C(184, 134, 42), C(110, 78, 20), C(56, 40, 8) };
        public static readonly Color32[] Heal = { C(234, 255, 234), C(157, 255, 157), C(79, 212, 106), C(42, 154, 72), C(22, 96, 44), C(11, 51, 24) };
        public static readonly Color32[] Bone = { C(244, 239, 226), C(216, 207, 184), C(176, 166, 140), C(133, 124, 102), C(90, 83, 68), C(46, 42, 34) };
        public static readonly Color32[] Stone = Tuning.ReleaseFx.Stone;
        public static readonly Color32[] Grey = { C(232, 230, 226), C(189, 185, 178), C(143, 138, 130), C(102, 98, 92), C(64, 61, 57), C(34, 32, 30) };
        /// <summary>What hurts the player: red outside a pale-hot core (damage lines, the tornado's
        /// ring, cinder ground's edge, an enemy blast).</summary>
        public static readonly Color32[] Danger = { C(255, 255, 255), C(255, 240, 224), C(255, 176, 144), C(255, 64, 48), C(160, 24, 16), C(64, 8, 8) };
        /// <summary>A Rift's light: white-cyan to deep teal.</summary>
        public static readonly Color32[] Rift = { C(255, 255, 255), C(216, 251, 255), C(127, 240, 255), C(44, 196, 224), C(26, 122, 154), C(12, 58, 76) };
        /// <summary>A barrier's blue charge (force fields, the spire's dome).</summary>
        public static readonly Color32[] Charge = { C(255, 255, 255), C(216, 232, 255), C(138, 184, 255), C(74, 122, 224), C(42, 66, 144), C(20, 30, 72) };
        public static readonly Color32[] Violet = { C(246, 238, 255), C(210, 184, 255), C(160, 127, 242), C(108, 80, 208), C(62, 43, 138), C(30, 20, 80) };
        /// <summary>A groove cut in the floor, and the lit north wall inside it.</summary>
        public static readonly Color32 Groove = C(18, 16, 21), GrooveLip = C(87, 82, 94);

        /// <summary>A ramp from one colour: white-hot, the colour, then darker - for an element's tint.</summary>
        public static Color32[] RampOf(Color c)
        {
            Color32 W(float t) => Color.Lerp(Color.white, c, t);
            Color32 D(float t) => Color.Lerp(c, Color.black, t);
            return new[] { W(0.15f), W(0.55f), (Color32)c, D(0.3f), D(0.55f), D(0.75f) };
        }
    }
}
