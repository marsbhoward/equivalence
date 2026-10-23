using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.Wildlife;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// BACKGROUND LIFE, per place (the user's picks, 2026-10-23): small things that live in the room
    /// and want nothing to do with the fight.
    ///
    ///   TOWER   rats darting along the foot of the north wall into its cracks, dust motes drifting
    ///           in the window light (only with windows), now and then a bat crossing overhead
    ///   FOREST  leaves falling across the room, moths circling the lights, a fish jumping in the lake
    ///   SKY     birds wheeling high, their shadows crossing the floor; feathers drifting on the wind
    ///   SALT    salt dust blowing past in streaks, glints on the brine, a lizard skittering across
    ///
    /// A picture only: no collider, no sound, never hurts, never blocks. Small, quick and SHY - every
    /// runner starts well clear of the player and bolts from one that comes close - so nothing reads
    /// as an enemy. Drawn at the creatures' texel size; the fliers and the falling things sort just
    /// under the room's darkness (so the dark takes them like everything else), above the bodies.
    /// Timing is hashed from the floor's seed, never UnityEngine.Random (seeded rolls share it).
    /// Built from DressArena beside the Nigredo layer, rebuilt each floor.
    /// </summary>
    public class Wildlife : MonoBehaviour
    {
        const float Ppu = Enemies.TexelRig.TexelsPerUnit;      // the creatures' own texel
        const float DotPpu = FloorArt.PixelsPerUnit;           // motes, streaks, glints

        /// <summary>Overhead, under the room's darkness (LightField at StatusOverlay - 10).</summary>
        const int OrderAir = SortingOrders.StatusOverlay - 12;
        const int OrderGround = SortingOrders.GroundDecal + 1;  // shadows, wind streaks
        const int OrderWater = SortingOrders.PitBase + 4;       // over the lake's surface

        enum Kind { Rat, Bat, Lizard, Fish }

        [System.Serializable]
        class Runner
        {
            public Kind Kind;
            public SpriteRenderer Sr;
            public Vector2 From, To;
            public float Start = -1f, Duration, NextAt, Pause;
            public int Legs;            // a lizard's darts left
        }

        [System.Serializable]
        class Mote
        {
            public SpriteRenderer Sr;
            public Vector2 Base, Move;
            public float Phase;
            public int Kind;            // 0 leaf, 1 feather, 2 mote, 3 streak, 4 glint, 5 moth, 6 bird
            public SpriteRenderer Shadow;
            public Vector2 Centre;
            public float Radius;
            public Lighting.Glow Light; // a moth's lamp
        }

        [SerializeField] BiomeKind _place;
        [SerializeField] Vector2 _half;
        [SerializeField] int _seed;
        [SerializeField] float _t;
        [SerializeField] List<Runner> _runners = new();
        [SerializeField] List<Mote> _motes = new();
        [SerializeField] Transform _player;
        [SerializeField] float _lookAt, _lightsAt, _glintsAt;
        [SerializeField] List<Vector2> _water = new();
        int _rolls;

        public static void Dress(Transform parent, Vector2 half, int seed)
        {
            if (parent == null) return;
            var old = parent.Find("wildlife");
            if (old != null) { old.name = "wildlife.old"; Destroy(old.gameObject); }   // Destroy is deferred
            if (!T.Enabled) return;
            var go = new GameObject("wildlife");
            go.transform.SetParent(parent, false);
            go.AddComponent<Wildlife>().Build(Biome.Current, half, seed);
        }

        void Build(BiomeKind place, Vector2 half, int seed)
        {
            _place = place; _half = half; _seed = seed;
            var rng = new System.Random(seed * 31 + 17);
            switch (place)
            {
                case BiomeKind.Tower:
                    for (int i = 0; i < T.Rats; i++) AddRunner(Kind.Rat, rng);
                    AddRunner(Kind.Bat, rng);
                    if (Scenery.Current.WindowPairs > 0 && Scenery.Current.Light != Scenery.Daylight.None)
                        for (int i = 0; i < T.DustMotes; i++) AddMote(2, rng);
                    break;
                case BiomeKind.Forest:
                    for (int i = 0; i < T.Leaves; i++) AddMote(0, rng);
                    for (int i = 0; i < T.Moths; i++) AddMote(5, rng);
                    AddRunner(Kind.Fish, rng);
                    break;
                case BiomeKind.SkyRuin:
                    for (int i = 0; i < T.Birds; i++) AddMote(6, rng);
                    for (int i = 0; i < T.Feathers; i++) AddMote(1, rng);
                    break;
                case BiomeKind.SaltFlats:
                    for (int i = 0; i < T.SaltStreaks; i++) AddMote(3, rng);
                    for (int i = 0; i < T.Glints; i++) AddMote(4, rng);
                    AddRunner(Kind.Lizard, rng);
                    break;
            }
        }

        // ------------------------------------------------------------------ building

        void AddRunner(Kind kind, System.Random rng)
        {
            var sr = Quad(kind.ToString().ToLowerInvariant(), Frames(kind)[0],
                          kind == Kind.Bat ? OrderAir : kind == Kind.Fish ? OrderWater : SortingOrders.DepthBase);
            sr.enabled = false;
            _runners.Add(new Runner
            {
                Kind = kind, Sr = sr,
                NextAt = T.FirstAfter + (float)rng.NextDouble() * Gap(kind).y,
            });
        }

        void AddMote(int kind, System.Random rng)
        {
            Sprite sp = kind switch
            {
                0 => Leaf((int)(rng.NextDouble() * 3), false),
                1 => Feather(false),
                3 => Streak,
                4 => Glint,
                5 => Moth(false),
                6 => Bird(0),
                _ => Dot,
            };
            int order = kind == 3 ? OrderGround : kind == 4 ? OrderWater : OrderAir;
            var sr = Quad(kind switch { 0 => "leaf", 1 => "feather", 2 => "mote", 3 => "streak", 4 => "glint", 5 => "moth", _ => "bird" }, sp, order);
            var m = new Mote { Sr = sr, Kind = kind, Phase = (float)rng.NextDouble() * 100f, Base = RandomIn(rng) };
            switch (kind)
            {
                case 0: m.Move = new Vector2(0.05f + (float)rng.NextDouble() * 0.08f, -(0.18f + (float)rng.NextDouble() * 0.16f)); break;
                case 1: m.Move = new Vector2(0.18f + (float)rng.NextDouble() * 0.14f, -(0.04f + (float)rng.NextDouble() * 0.05f)); sr.color = new Color(1f, 1f, 1f, 0.7f); break;
                case 2: m.Move = new Vector2((float)rng.NextDouble() * 0.06f - 0.03f, 0.02f + (float)rng.NextDouble() * 0.03f);
                        m.Base.y = Mathf.Lerp(0.1f, _half.y - 0.5f, (float)rng.NextDouble()); break;   // in the north half, where the windows' light falls
                case 3: m.Move = new Vector2(3f + (float)rng.NextDouble() * 2.5f, 0f); sr.enabled = false; break;
                case 4: sr.enabled = false; break;
                case 5: sr.enabled = false; m.Radius = 0.3f + (float)rng.NextDouble() * 0.35f; break;
                case 6:
                    m.Centre = new Vector2(((float)rng.NextDouble() - 0.5f) * _half.x, ((float)rng.NextDouble() - 0.3f) * _half.y);
                    m.Radius = 2.5f + (float)rng.NextDouble() * 3f;
                    m.Move = new Vector2((rng.NextDouble() < 0.75 ? 1f : -1f) * (0.25f + (float)rng.NextDouble() * 0.15f), 0f);
                    m.Shadow = Quad("bird.shadow", BirdShadow, OrderGround);
                    m.Shadow.color = new Color(0f, 0f, 0f, T.BirdShadowAlpha);
                    break;
            }
            _motes.Add(m);
        }

        SpriteRenderer Quad(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        Vector2 RandomIn(System.Random rng) =>
            new(((float)rng.NextDouble() * 2f - 1f) * _half.x, ((float)rng.NextDouble() * 2f - 1f) * _half.y);

        /// <summary>A roll off the floor's seed - the same room meets the same timings.</summary>
        float Roll() { _rolls++; uint h = (uint)_seed * 2654435761u + (uint)_rolls * 40503u; h ^= h >> 15; h *= 2246822519u; h ^= h >> 13; return (h & 0xffffff) / 16777216f; }

        static Vector2 Gap(Kind k) => k switch
        {
            Kind.Rat => new Vector2(T.RatGap.x, T.RatGap.y),
            Kind.Bat => new Vector2(T.BatGap.x, T.BatGap.y),
            Kind.Lizard => new Vector2(T.LizardGap.x, T.LizardGap.y),
            _ => new Vector2(T.FishGap.x, T.FishGap.y),
        };

        // ------------------------------------------------------------------ running

        void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_player == null && _t >= _lookAt)
            {
                _lookAt = _t + 1f;
                var pc = FindAnyObjectByType<Player.PlayerController>();
                if (pc != null) _player = pc.transform;
            }
            foreach (var r in _runners) if (r != null && r.Sr != null) TickRunner(r, dt);
            if (_place == BiomeKind.Forest && _t >= _lightsAt) { _lightsAt = _t + 3f; PickLamps(); }
            if (_place == BiomeKind.SaltFlats && _t >= _glintsAt) { _glintsAt = _t + 5f; FindWater(); }
            foreach (var m in _motes) if (m != null && m.Sr != null) TickMote(m, dt);
        }

        Vector2 PlayerAt => _player != null ? (Vector2)_player.position : new Vector2(9999f, 9999f);

        void TickRunner(Runner r, float dt)
        {
            if (r.Start < 0f)
            {
                if (_t < r.NextAt) return;
                if (!Launch(r)) { r.NextAt = _t + 1.5f; return; }   // nowhere to go just now - try again soon
                r.Start = _t;
                r.Sr.enabled = true;
            }
            // A rat keeps its own progress (Pause), so bolting speeds it up without a jump.
            if (r.Kind == Kind.Rat)
            {
                bool near = Vector2.Distance(PlayerAt, Vector2.Lerp(r.From, r.To, r.Pause)) < T.FleeRadius;
                r.Pause += dt / r.Duration * (near ? T.RatBolt : 1f);
            }
            float k = r.Kind == Kind.Rat ? r.Pause : (_t - r.Start) / r.Duration;
            Vector2 p = Vector2.Lerp(r.From, r.To, Mathf.Clamp01(k));

            switch (r.Kind)
            {
                case Kind.Rat:
                {
                    r.Sr.sprite = Frames(Kind.Rat)[Mathf.FloorToInt(_t / 0.07f) & 1];
                    r.Sr.flipX = r.To.x < r.From.x;
                    float a = Mathf.Clamp01(Mathf.Min(k, 1f - k) * 8f);   // out of one crack and into another
                    r.Sr.color = new Color(1f, 1f, 1f, a);
                    Place(r.Sr, p, true);
                    break;
                }
                case Kind.Bat:
                {
                    var side = new Vector2(-(r.To - r.From).y, (r.To - r.From).x).normalized;
                    p += side * Mathf.Sin(k * Mathf.PI) * 1.2f + Vector2.up * Mathf.Sin(_t * 9f) * 0.08f;   // a curve, and its flutter
                    r.Sr.sprite = Frames(Kind.Bat)[Mathf.FloorToInt(_t / 0.08f) & 1];
                    r.Sr.flipX = r.To.x < r.From.x;
                    Place(r.Sr, p, false);
                    break;
                }
                case Kind.Lizard:
                {
                    float since = _t - r.Start;
                    if (since >= r.Duration)
                    {
                        if (r.Pause > 0f) { r.Pause -= dt; Place(r.Sr, r.To, true); r.Sr.sprite = Frames(Kind.Lizard)[0]; return; }
                        if (r.Legs <= 0 || !NextDart(r)) { End(r); return; }
                        return;
                    }
                    r.Sr.sprite = Frames(Kind.Lizard)[Mathf.FloorToInt(_t / 0.06f) & 1];
                    r.Sr.flipX = r.To.x < r.From.x;
                    Place(r.Sr, p, true);
                    return;
                }
                case Kind.Fish:
                {
                    // An arc out of the water and back in; rings where it left and where it went in.
                    float h = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * T.FishJumpHeight;
                    int f = k < 0.38f ? 0 : k < 0.62f ? 1 : 2;
                    r.Sr.sprite = Frames(Kind.Fish)[f];
                    r.Sr.flipX = r.To.x < r.From.x;
                    Place(r.Sr, p + Vector2.up * h, false);
                    if (k >= 1f) Hazards.LakeSurface.RippleAt(r.To, 0.45f);
                    break;
                }
            }
            if (k >= 1f) End(r);
        }

        void End(Runner r)
        {
            r.Start = -1f;
            r.Sr.enabled = false;
            var g = Gap(r.Kind);
            r.NextAt = _t + Mathf.Lerp(g.x, g.y, Roll());
        }

        bool Launch(Runner r)
        {
            var player = PlayerAt;
            switch (r.Kind)
            {
                case Kind.Rat:
                {
                    // Along the foot of the north wall - the one face the camera sees - crack to crack.
                    float y = _half.y - T.RatWallInset;
                    float x0 = Mathf.Lerp(-_half.x + 1f, _half.x - 1f, Roll());
                    float len = Mathf.Lerp(2f, 4.5f, Roll()) * (Roll() < 0.5f ? -1f : 1f);
                    float x1 = Mathf.Clamp(x0 + len, -_half.x + 0.6f, _half.x - 0.6f);
                    var a = new Vector2(x0, y); var b = new Vector2(x1, y);
                    if (Mathf.Abs(x1 - x0) < 1.2f || !Clear(a, b, player, 0.15f)) return false;
                    r.From = a; r.To = b; r.Duration = Mathf.Abs(x1 - x0) / T.RatSpeed; r.Pause = 0f;
                    return true;
                }
                case Kind.Bat:
                {
                    // Across the room high up, from one side to the other.
                    bool left = Roll() < 0.5f;
                    r.From = new Vector2((left ? -1f : 1f) * (_half.x + 2f), Mathf.Lerp(-_half.y * 0.5f, _half.y, Roll()));
                    r.To = new Vector2(-r.From.x, Mathf.Lerp(-_half.y * 0.6f, _half.y, Roll()));
                    r.Duration = Vector2.Distance(r.From, r.To) / T.BatSpeed;
                    return true;
                }
                case Kind.Lizard:
                {
                    bool left = Roll() < 0.5f;
                    var a = new Vector2((left ? -1f : 1f) * (_half.x - 0.5f), Mathf.Lerp(-_half.y + 1f, _half.y - 1.5f, Roll()));
                    if (!Arena.OnFloor(a, 0.2f) || Vector2.Distance(a, player) < T.SpawnClearance) return false;
                    r.To = a; r.Legs = 4 + Mathf.FloorToInt(Roll() * 4f); r.Duration = 0f; r.Pause = 0f;
                    r.Start = _t;
                    return NextDart(r);
                }
                case Kind.Fish:
                {
                    if (!Arena.ChasmIsWater || !Arena.HasChasms) return false;
                    FindWater();
                    if (_water.Count == 0) return false;
                    var a = _water[Mathf.FloorToInt(Roll() * _water.Count) % _water.Count];
                    var b = a + new Vector2((Roll() < 0.5f ? -1f : 1f) * T.FishJumpLength, 0f);
                    if (!Arena.DeepInChasm(b, 0.3f)) return false;
                    r.From = a; r.To = b; r.Duration = T.FishJumpSeconds;
                    Hazards.LakeSurface.RippleAt(a, 0.35f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>The lizard's next dart: a short dash on open floor, away from the player if
        /// they are close, then a pause.</summary>
        bool NextDart(Runner r)
        {
            var from = r.To;
            var player = PlayerAt;
            Vector2 dir = Vector2.Distance(from, player) < T.FleeRadius * 1.5f
                ? (from - player).normalized
                : new Vector2(-Mathf.Sign(from.x) * 0.9f + (Roll() - 0.5f) * 0.8f, (Roll() - 0.5f) * 1.2f).normalized;
            for (int tries = 0; tries < 6; tries++)
            {
                float len = Mathf.Lerp(0.6f, 1.3f, Roll());
                var to = from + dir * len;
                if (Arena.OnFloor(to, 0.2f) && Clear(from, to, player, 0f))
                {
                    r.From = from; r.To = to; r.Duration = len / T.LizardSpeed; r.Start = _t;
                    r.Pause = Mathf.Lerp(0.25f, 0.9f, Roll()); r.Legs--;
                    r.Sr.enabled = true;
                    return true;
                }
                dir = Quaternion.Euler(0f, 0f, 55f * (tries + 1) * (tries % 2 == 0 ? 1f : -1f)) * dir;
            }
            return false;
        }

        /// <summary>Is the line from a to b standing room, and clear of the player?</summary>
        static bool Clear(Vector2 a, Vector2 b, Vector2 player, float margin)
        {
            for (int i = 0; i <= 8; i++)
            {
                var p = Vector2.Lerp(a, b, i / 8f);
                if (!Arena.OnFloor(p, margin)) return false;
                if (Vector2.Distance(p, player) < T.SpawnClearance) return false;
            }
            return true;
        }

        void FindWater()
        {
            _water.Clear();
            if (!Arena.HasChasms || !Arena.ChasmIsWater) return;
            for (float x = -_half.x + 0.5f; x < _half.x; x += 0.5f)
            for (float y = -_half.y + 0.5f; y < _half.y; y += 0.5f)
            {
                var p = new Vector2(x, y);
                if (Arena.DeepInChasm(p, 0.6f)) _water.Add(p);
            }
        }

        void PickLamps()
        {
            var lamps = new List<Lighting.Glow>();
            foreach (var g in Lighting.Glow.All)
            {
                if (g == null || g.Radius < T.MothLampRadius || g.transform.IsChildOf(transform)) continue;
                if (g.GetComponentInParent<Player.PlayerController>() != null) continue;
                if (g.GetComponentInParent<Enemies.EnemyController>() != null) continue;
                lamps.Add(g);
            }
            int n = 0;
            foreach (var m in _motes)
            {
                if (m.Kind != 5) continue;
                m.Light = lamps.Count > 0 ? lamps[(n / 2) % lamps.Count] : null;   // two to a lamp
                n++;
            }
        }

        void TickMote(Mote m, float dt)
        {
            float ph = m.Phase;
            switch (m.Kind)
            {
                case 0:   // a leaf: falling, swaying, tumbling
                case 1:   // a feather: carried along on the wind, rocking
                {
                    m.Base += m.Move * dt;
                    if (m.Base.y < -_half.y - 0.5f) m.Base = new Vector2(Mathf.Lerp(-_half.x, _half.x, Frac(ph * 7.3f + _t * 0.37f)), _half.y + 0.8f);
                    if (m.Base.x > _half.x + 0.8f) m.Base = new Vector2(-_half.x - 0.8f, Mathf.Lerp(-_half.y, _half.y, Frac(ph * 3.1f + _t * 0.21f)));
                    float sway = Mathf.Sin(_t * (0.7f + Frac(ph) * 0.8f) + ph) * (m.Kind == 0 ? 0.28f : 0.12f);
                    bool turned = Mathf.Sin(_t * (m.Kind == 0 ? 2.6f : 1.4f) + ph * 2f) > 0.3f;
                    m.Sr.sprite = m.Kind == 0 ? Leaf(Mathf.FloorToInt(Frac(ph * 5f) * 3f), turned) : Feather(turned);
                    m.Sr.flipX = Mathf.Cos(_t * 0.9f + ph) < 0f;
                    Place(m.Sr, m.Base + new Vector2(sway, 0f), false);
                    break;
                }
                case 2:   // a mote of dust, turning slowly in the light
                {
                    m.Base += (m.Move + new Vector2(Mathf.Sin(_t * 0.4f + ph) * 0.03f, Mathf.Cos(_t * 0.33f + ph) * 0.02f)) * dt;
                    if (m.Base.y > _half.y - 0.2f) m.Base.y = 0.1f;
                    if (Mathf.Abs(m.Base.x) > _half.x) m.Base.x = -Mathf.Sign(m.Base.x) * (_half.x - 0.2f);
                    m.Sr.color = new Color(1f, 0.93f, 0.78f, 0.25f + 0.3f * (0.5f + 0.5f * Mathf.Sin(_t * 1.3f + ph)));
                    PlaceDot(m.Sr, m.Base);
                    break;
                }
                case 3:   // a streak of salt dust, blown across the floor in a gust
                {
                    float cycle = 2.4f + Frac(ph) * 2.5f;
                    float local = Frac(_t / cycle + ph) * cycle;
                    float live = 1.3f;
                    if (local > live) { m.Sr.enabled = false; m.Base.x = 9999f; break; }
                    if (m.Base.x > 999f || local < dt * 1.5f)
                        m.Base = new Vector2(Mathf.Lerp(-_half.x, _half.x * 0.4f, Frac(ph * 11.7f + _t * 0.13f)), Mathf.Lerp(-_half.y + 0.4f, _half.y - 0.6f, Frac(ph * 5.9f + _t * 0.29f)));
                    m.Base += m.Move * dt;
                    m.Sr.enabled = true;
                    m.Sr.color = new Color(1f, 1f, 1f, T.SaltStreakAlpha * Mathf.Sin(local / live * Mathf.PI));
                    PlaceDot(m.Sr, m.Base);
                    break;
                }
                case 4:   // a glint of light on the brine
                {
                    float cycle = 1.6f + Frac(ph) * 2.2f;
                    float local = Frac(_t / cycle + ph);
                    if (_water.Count == 0 || local > 0.22f) { m.Sr.enabled = false; break; }
                    if (local < dt / cycle * 1.5f || m.Base.x > 999f || !m.Sr.enabled)
                        m.Base = _water[Mathf.FloorToInt(Frac(ph * 13.3f + _t * 0.071f) * _water.Count) % _water.Count]
                                 + new Vector2(Frac(ph * 3.7f) - 0.5f, Frac(ph * 9.1f) - 0.5f) * 0.4f;
                    m.Sr.enabled = true;
                    m.Sr.color = new Color(1f, 1f, 1f, Mathf.Sin(local / 0.22f * Mathf.PI));
                    PlaceDot(m.Sr, m.Base);
                    break;
                }
                case 5:   // a moth round a lamp
                {
                    if (m.Light == null || !m.Light.isActiveAndEnabled || !m.Light.Sample(out var at, out _, out var strength, out _) || strength < 0.15f)
                    { m.Sr.enabled = false; break; }
                    m.Sr.enabled = true;
                    float a = _t * (2.4f + Frac(ph) * 1.6f) + ph;
                    var off = new Vector2(Mathf.Cos(a), Mathf.Sin(a * 1.3f) * 0.7f) * m.Radius
                              + new Vector2(Mathf.Sin(_t * 7.1f + ph), Mathf.Cos(_t * 6.3f + ph)) * 0.05f;
                    m.Sr.sprite = Moth(Mathf.FloorToInt(_t / 0.05f + ph) % 2 == 0);
                    Place(m.Sr, at + off + new Vector2(0f, 0.2f), false);
                    break;
                }
                case 6:   // a bird wheeling high, its shadow crossing the floor below
                {
                    float a = _t * m.Move.x / Mathf.Max(1f, m.Radius) * 3f + ph;
                    var centre = m.Centre + new Vector2(Mathf.Sin(_t * 0.05f + ph) * 2f, Mathf.Cos(_t * 0.04f + ph) * 1f);
                    var p = centre + new Vector2(Mathf.Cos(a) * m.Radius, Mathf.Sin(a) * m.Radius * 0.55f);
                    float vx = -Mathf.Sin(a) * m.Move.x;
                    // Mostly gliding, a few beats now and then.
                    bool flapping = Mathf.Sin(_t * 0.6f + ph) > 0.55f;
                    m.Sr.sprite = Bird(flapping ? 1 + (Mathf.FloorToInt(_t / 0.1f) & 1) : 0);
                    m.Sr.flipX = vx < 0f;
                    Place(m.Sr, p + new Vector2(0f, T.BirdHeight), false);
                    if (m.Shadow != null) Place(m.Shadow, p + T.BirdShadowOffset, false);
                    break;
                }
            }
        }

        static float Frac(float v) => v - Mathf.Floor(v);

        /// <summary>On the creatures' texel grid; a runner on the ground sorts by its feet.</summary>
        static void Place(SpriteRenderer sr, Vector2 p, bool grounded)
        {
            sr.transform.localPosition = new Vector3(Mathf.Round(p.x * Ppu) / Ppu, Mathf.Round(p.y * Ppu) / Ppu, 0f);
            if (grounded) sr.sortingOrder = SortingOrders.ForDepth(p.y);
        }

        static void PlaceDot(SpriteRenderer sr, Vector2 p) =>
            sr.transform.localPosition = new Vector3(Mathf.Round(p.x * DotPpu) / DotPpu, Mathf.Round(p.y * DotPpu) / DotPpu, 0f);

        // ------------------------------------------------------------------ pictures

        static readonly Color Ink = new(0.06f, 0.05f, 0.05f);
        static readonly Dictionary<string, Sprite> _sprites = new();

        sealed class Canvas
        {
            public readonly int W, H;
            public readonly Color[] Px;
            public Canvas(int w, int h) { W = w; H = h; Px = new Color[w * h]; }
            public void Set(int x, int y, Color c) { if (x >= 0 && y >= 0 && x < W && y < H) Px[y * W + x] = c; }
            public bool Has(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && Px[y * W + x].a > 0f;
            public void Ellipse(float cx, float cy, float rx, float ry, System.Func<float, Color> shade)
            {
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy < 1f) Set(x, y, shade(dy));
                }
            }
            public void Line(float x0, float y0, float x1, float y1, Color c)
            {
                int n = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))) + 1;
                for (int i = 0; i <= n; i++) Set(Mathf.FloorToInt(Mathf.Lerp(x0, x1, i / (float)n)), Mathf.FloorToInt(Mathf.Lerp(y0, y1, i / (float)n)), c);
            }
            /// <summary>One texel of ink round everything drawn so far.</summary>
            public void Outline()
            {
                var add = new List<int>();
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (!Has(x, y) && (Has(x + 1, y) || Has(x - 1, y) || Has(x, y + 1) || Has(x, y - 1))) add.Add(y * W + x);
                foreach (int i in add) Px[i] = Ink;
            }
            public Sprite Make(float ppu, Vector2 pivot)
            {
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels(Px);
                tex.Apply();
                var s = Sprite.Create(tex, new Rect(0, 0, W, H), pivot, ppu, 0, SpriteMeshType.FullRect);
                s.hideFlags = HideFlags.HideAndDontSave;
                return s;
            }
        }

        static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (_sprites.TryGetValue(key, out var s) && s != null) return s;
            return _sprites[key] = make();
        }

        static readonly Sprite[] _none = new Sprite[0];

        static Sprite[] Frames(Kind k) => k switch
        {
            Kind.Rat => new[] { Rat(0), Rat(1) },
            Kind.Bat => new[] { Bat(true), Bat(false) },
            Kind.Lizard => new[] { Lizard(0), Lizard(1) },
            Kind.Fish => new[] { Fish(35f), Fish(0f), Fish(-35f) },
            _ => _none,
        };

        /// <summary>A rat side on, facing right, two frames of its scurry.</summary>
        public static Sprite Rat(int frame) => Cached("rat" + frame, () =>
        {
            var c = new Canvas(20, 8);
            Color hi = new(0.36f, 0.32f, 0.30f), mid = new(0.25f, 0.22f, 0.21f), lo = new(0.16f, 0.14f, 0.14f), pink = new(0.62f, 0.42f, 0.42f);
            c.Ellipse(9.5f, 3.6f, 5f, 2.3f, dy => dy > 0.3f ? hi : dy < -0.4f ? lo : mid);   // the body, hunched
            c.Ellipse(14.6f, 3.4f, 2.4f, 1.7f, dy => dy > 0.2f ? hi : mid);                   // the head
            c.Set(17, 3, mid); c.Set(17, 2, lo);                                              // the snout
            int a = frame == 0 ? 0 : 1;
            c.Set(7 - a, 1, lo); c.Set(12 + a, 1, lo);                                        // legs
            c.Outline();
            c.Set(14, 5, pink); c.Set(15, 5, pink);                                           // the ear
            c.Set(15, 4, new Color(0.85f, 0.75f, 0.6f));                                      // the eye catches the light
            c.Set(18, 2, pink);                                                               // the nose
            for (int i = 0; i < 5; i++) c.Set(3 - i, 2 - (i + frame) / 3, pink * 0.8f);       // the tail
            return c.Make(Ppu, new Vector2(0.5f, 1f / 8f));
        });

        /// <summary>A bat seen from below, wings up or down.</summary>
        public static Sprite Bat(bool up) => Cached("bat" + up, () =>
        {
            var c = new Canvas(17, 9);
            Color body = new(0.13f, 0.10f, 0.11f), wing = new(0.21f, 0.16f, 0.17f);
            for (int x = 0; x < 17; x++)
            {
                int d = Mathf.Abs(x - 8);
                if (d < 2) continue;
                float y0 = up ? 4f + d * 0.45f : 4.2f - d * 0.3f;
                int scallop = (d % 3 == 0) ? 1 : 0;
                for (int y = Mathf.FloorToInt(y0 - 1.6f + scallop); y <= Mathf.FloorToInt(y0); y++) c.Set(x, y, wing);
            }
            c.Ellipse(8.5f, 4f, 1.8f, 2.4f, _ => body);
            c.Set(7, 6, body); c.Set(9, 6, body);                                             // the ears
            c.Outline();
            return c.Make(Ppu, new Vector2(0.5f, 0.5f));
        });

        /// <summary>A lizard seen from above, splayed, facing right; its legs alternate.</summary>
        public static Sprite Lizard(int frame) => Cached("lizard" + frame, () =>
        {
            var c = new Canvas(22, 9);
            Color hi = new(0.86f, 0.76f, 0.62f), mid = new(0.72f, 0.60f, 0.46f), lo = new(0.56f, 0.45f, 0.34f);
            c.Ellipse(11f, 4.5f, 4.2f, 1.6f, dy => dy > 0.3f ? hi : dy < -0.4f ? lo : mid);   // body
            c.Ellipse(16.2f, 4.5f, 2f, 1.3f, dy => dy > 0.2f ? hi : mid);                     // head
            for (int i = 0; i < 6; i++) c.Set(6 - i, 4 + (i > 3 ? (frame == 0 ? 1 : -1) : 0), i < 3 ? mid : lo);   // the tail, flicking
            int s = frame == 0 ? 1 : -1;
            c.Line(13, 5, 14 + s, 7, lo); c.Line(13, 4, 14 - s, 1, lo);                      // forelegs
            c.Line(9, 5, 8 - s, 7, lo); c.Line(9, 4, 8 + s, 1, lo);                          // hind legs
            c.Outline();
            c.Set(17, 5, Ink); c.Set(17, 3, Ink);                                            // the eyes
            for (int x = 8; x < 14; x += 2) c.Set(x, 4, lo);                                 // the spine's spots
            return c.Make(Ppu, new Vector2(0.5f, 0.3f));
        });

        /// <summary>A small fish, silver-green, pitched up, level or down.</summary>
        public static Sprite Fish(float pitch) => Cached("fish" + pitch, () =>
        {
            var c = new Canvas(13, 11);
            Color hi = new(0.88f, 0.92f, 0.84f), mid = new(0.56f, 0.64f, 0.54f);   // a bright belly: it must read over dark water
            var d = new Vector2(Mathf.Cos(pitch * Mathf.Deg2Rad), Mathf.Sin(pitch * Mathf.Deg2Rad));
            var n = new Vector2(-d.y, d.x);
            var centre = new Vector2(6.5f, 5.5f);
            for (int y = 0; y < 11; y++)
            for (int x = 0; x < 13; x++)
            {
                var q = new Vector2(x + 0.5f, y + 0.5f) - centre;
                float t = Vector2.Dot(q, d), s = Vector2.Dot(q, n);
                float half = t > -3f && t < 3.5f ? 1.4f * Mathf.Sqrt(Mathf.Max(0f, 1f - (t / 3.5f) * (t / 3.5f))) : 0f;
                bool tail = t <= -2.6f && t > -4.8f && Mathf.Abs(s) < (-t - 2.4f) * 0.8f;
                if (Mathf.Abs(s) < half || tail) c.Set(x, y, s > 0.2f ? hi : mid);
            }
            c.Outline();
            return c.Make(Ppu, new Vector2(0.5f, 0.5f));
        });

        /// <summary>A bird far overhead, a gull's shallow M: gliding (0), wings up (1), wings down (2).</summary>
        public static Sprite Bird(int frame) => Cached("bird" + frame, () =>
        {
            var c = new Canvas(17, 9);
            Color wing = new(0.24f, 0.25f, 0.29f), body = new(0.16f, 0.17f, 0.20f);
            for (int x = 0; x < 17; x++)
            {
                int d = Mathf.Abs(x - 8);
                if (d > 8) continue;
                float y = frame switch
                {
                    1 => 3f + d * 0.65f,                                   // raised, tips highest
                    2 => 4f - d * 0.35f,                                   // the downstroke
                    _ => d <= 1 ? 3f : d <= 5 ? 4.6f : 4.6f - (d - 5) * 0.8f,   // gliding: up from the body, tips curled down
                };
                int iy = Mathf.RoundToInt(y);
                c.Set(x, iy, wing);
                if (d <= 4) c.Set(x, iy - 1, wing);                       // the inner wing is broader
            }
            c.Set(8, 2, body); c.Set(8, 3, body); c.Set(7, 3, body); c.Set(9, 3, body); c.Set(8, 1, body);   // the body and tail
            return c.Make(Ppu, new Vector2(0.5f, 0.5f));
        });

        static Sprite BirdShadow => Cached("bird.shadow", () =>
        {
            var c = new Canvas(9, 4);
            for (int y = 0; y < 4; y++)
            for (int x = 0; x < 9; x++)
            {
                float dx = (x + 0.5f - 4.5f) / 4.5f, dy = (y + 0.5f - 2f) / 2f;
                if (dx * dx + dy * dy < 1f && (dx * dx + dy * dy < 0.4f || (x + y) % 2 == 0)) c.Set(x, y, Color.white);
            }
            return c.Make(DotPpu, new Vector2(0.5f, 0.5f));
        });

        /// <summary>A leaf: three colours, face on or turned edge on as it tumbles.</summary>
        public static Sprite Leaf(int tone, bool edge) => Cached("leaf" + tone + edge, () =>
        {
            Color[] tones = { new(0.30f, 0.38f, 0.15f), new(0.45f, 0.38f, 0.14f), new(0.42f, 0.24f, 0.12f) };
            var col = tones[Mathf.Clamp(tone, 0, 2)];
            var c = new Canvas(5, 4);
            if (edge) { c.Line(0, 2, 4, 1, col); }
            else { c.Ellipse(2.5f, 2f, 2.4f, 1.4f, dy => dy > 0f ? col * 1.15f : col); c.Set(4, 1, col * 0.7f); }
            return c.Make(DotPpu, new Vector2(0.5f, 0.5f));
        });

        public static Sprite Feather(bool turned) => Cached("feather" + turned, () =>
        {
            var c = new Canvas(6, 3);
            Color pale = new(0.80f, 0.79f, 0.76f), quill = new(0.62f, 0.60f, 0.57f);   // soft grey - a white chevron read as an arrow
            if (turned) c.Line(0, 1, 5, 1, pale);
            else { c.Line(0, 1, 5, 1, quill); for (int x = 1; x < 5; x++) { c.Set(x, 2, pale); if (x > 1) c.Set(x, 0, pale); } }
            return c.Make(DotPpu, new Vector2(0.5f, 0.5f));
        });

        public static Sprite Moth(bool open) => Cached("moth" + open, () =>
        {
            var c = new Canvas(3, 2);
            Color w = new(0.86f, 0.82f, 0.70f);
            if (open) { c.Set(0, 1, w); c.Set(1, 0, w); c.Set(2, 1, w); } else c.Set(1, 0, w);
            return c.Make(DotPpu, new Vector2(0.5f, 0.5f));
        });

        static Sprite Dot => Cached("dot", () => { var c = new Canvas(1, 1); c.Set(0, 0, Color.white); return c.Make(DotPpu, new Vector2(0.5f, 0.5f)); });

        static Sprite Streak => Cached("streak", () =>
        {
            var c = new Canvas(12, 1);
            for (int x = 0; x < 12; x++) if (x > 7 || x % 2 == 0) c.Set(x, 0, new Color(1f, 1f, 1f, Mathf.Lerp(0.2f, 1f, x / 11f)));
            return c.Make(DotPpu, new Vector2(1f, 0.5f));
        });

        static Sprite Glint => Cached("glint", () =>
        {
            var c = new Canvas(3, 3);
            c.Set(1, 1, Color.white); c.Set(0, 1, new Color(0.8f, 0.95f, 1f, 0.6f)); c.Set(2, 1, new Color(0.8f, 0.95f, 1f, 0.6f));
            c.Set(1, 0, new Color(0.8f, 0.95f, 1f, 0.6f)); c.Set(1, 2, new Color(0.8f, 0.95f, 1f, 0.6f));
            return c.Make(DotPpu, new Vector2(0.5f, 0.5f));
        });
    }
}
