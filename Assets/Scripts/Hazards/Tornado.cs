using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// One funnel of a floor's storm (<see cref="TornadoStorm"/>). Three phases, and only the
    /// middle one hurts:
    ///
    ///     FORMING      dust gathering on the ground, the damage ring fading in, the funnel
    ///                  building bottom-up. Still and HARMLESS - this is the telegraph.
    ///     SPINNING     the set lifetime (Tuning.Tornado.SpinSeconds): it wanders the open floor
    ///                  and ticks the player while they stand inside the ring.
    ///     DISSIPATING  fading and unwinding. Harmless from its first frame, so a funnel the
    ///                  player sees going never takes one more tick off them.
    ///
    /// THE RING ON THE GROUND IS THE HITBOX. The funnel is tall and wide at the top, and a hitbox
    /// matched to the drawing's whole width would hurt a player the art only overhangs. The
    /// damage is measured at the FOOT, and the ring is drawn exactly on that radius - the same
    /// "what hurts is what is drawn" rule the mire and the pits keep.
    ///
    /// IT ROAMS OPEN FLOOR ONLY. Walls, columns, the risen spire (anything NavField calls solid)
    /// and every pit turn it aside, so it never sits inside a hazard the player is already reading
    /// and never parks in a wall. Slower than every enemy's walk: a funnel is something you step
    /// round, not something that runs you down. It does not chase - its path is a wander.
    ///
    /// Only ever hurts the PLAYER (as a pit does). Every cached field is [SerializeField] and the
    /// lists are non-readonly - see FloorPit's note and the domain-reload traps in CLAUDE.md.
    /// </summary>
    public class Tornado : MonoBehaviour
    {
        enum Phase { Forming, Spinning, Dissipating }

        [SerializeField] Phase _phase;
        [SerializeField] float _age;            // seconds in the current phase
        [SerializeField] float _clock;          // seconds alive, drives every animation
        [SerializeField] float _heading;        // radians
        [SerializeField] float _wanderSeed;
        [SerializeField] float _tick;
        [SerializeField] float _damagePerSecond;
        [SerializeField] float _fadeFrom = 1f;  // visibility when dissipation began
        [SerializeField] Transform _player;
        [SerializeField] Health _playerHealth;
        [SerializeField] Rigidbody2D _playerBody;
        [SerializeField] SpriteRenderer _shadow, _rim;
        [SerializeField] List<Band> _bands = new();
        [SerializeField] List<Mote> _motes = new();

        [System.Serializable]
        struct Band
        {
            public Transform T;
            public SpriteRenderer Body, Edge;
            public float Height, Width;
        }

        /// <summary>Something carried round the funnel: a streak of wind on a band, or dust at
        /// the foot (<see cref="Band"/> -1).</summary>
        [System.Serializable]
        struct Mote
        {
            public Transform T;
            public SpriteRenderer Sr;
            public int Band;
            public float Phase, Rate, Size;
        }

        // Enough bands that neighbours overlap into one column; at 9 they read as separate
        // ellipses - a slinky, not a funnel.
        const int BandCount = 13;
        const int StreaksPerBand = 2;
        const int DustMotes = 7;
        /// <summary>How flat the funnel's ellipses are - a ring seen from the 3/4 camera.</summary>
        const float Flat = 0.3f;

        // The body is a mid grey, not white: the arena floors are light stone, and a white
        // haze over them has no edge. The streaks are the white.
        static readonly Color Air = new(0.66f, 0.69f, 0.76f);
        static readonly Color Streak = new(0.97f, 0.98f, 1f);
        // Lifted grit, paler and greyer than the floor - a tan dust was the floor's own colour
        // and vanished against it.
        static readonly Color Dust = new(0.86f, 0.85f, 0.82f);
        static readonly Color RimColor = new(0.86f, 0.92f, 1f);

        // A scratch list for the physics query, shared by every funnel. Not readonly, null-guarded.
        static List<Collider2D> _hits;

        /// <summary>True only while it can hurt.</summary>
        public bool Harmful => _phase == Phase.Spinning;

        /// <summary>
        /// The player is immune to tornadoes (Sylph, the Air trap boon). Set by the run, a delegate
        /// so a funnel knows nothing about the exchange; null is no immunity.
        /// </summary>
        public static System.Func<bool> PlayerImmune;

        /// <summary>Every funnel alive, so a point can ask whether one is touching it. Rebuilt
        /// from OnEnable, so a domain reload refills it.</summary>
        static List<Tornado> _live;

        void OnEnable() => (_live ??= new List<Tornado>()).Add(this);
        void OnDisable() => _live?.Remove(this);

        /// <summary>Whether a spinning funnel's ring covers <paramref name="p"/> - Sylph's contact.</summary>
        public static bool Touching(Vector2 p)
        {
            if (_live == null) return false;
            float r = Tuning.Tornado.Radius;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var t = _live[i];
                if (t == null) { _live.RemoveAt(i); continue; }
                if (!t.Harmful) continue;
                if (((Vector2)t.transform.position - p).sqrMagnitude <= r * r) return true;
            }
            return false;
        }
        /// <summary>Dissipating - no longer counted against the storm's cap.</summary>
        public bool Ending => _phase == Phase.Dissipating;

        public static Tornado Spawn(Vector2 at, float damagePerSecond, Transform player, Transform parent)
        {
            var go = new GameObject("tornado");
            go.transform.SetParent(parent, false);
            go.transform.position = at;

            var t = go.AddComponent<Tornado>();
            t._damagePerSecond = damagePerSecond;
            t._player = player;
            t._playerHealth = player != null ? player.GetComponent<Health>() : null;
            t._playerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
            t._heading = Random.value * Mathf.PI * 2f;
            t._wanderSeed = Random.value * 100f;
            t._clock = Random.value * 10f;
            t.Build();
            t.Animate();
            return t;
        }

        /// <summary>Starts dying away now, harmless from this frame. The storm calls this on
        /// every funnel when the floor is cleared.</summary>
        public void Dissipate()
        {
            if (_phase == Phase.Dissipating) return;
            // A funnel still forming fades from however far it had got, not from full.
            _fadeFrom = _phase == Phase.Forming ? Mathf.Clamp01(_age / Tuning.Tornado.FormSeconds) : 1f;
            _phase = Phase.Dissipating;
            _age = 0f;
            _tick = 0f;
        }

        // ---- building ----

        void Build()
        {
            float r = Tuning.Tornado.Radius;

            // GROUND: a soft shadow and the ring. Under every body and over the pit band, as
            // the mire's patch - not depth sorted, because the ground does not stand anywhere.
            _shadow = Layer(transform, Spr.Glow, new Vector2(r * 3.2f, r * 1.8f), SortingOrders.Enemy - 1, "shadow");
            _rim = Layer(transform, Spr.ThinRing, Vector2.one * (r * 2f), SortingOrders.Enemy - 1, "rim");

            // The FUNNEL stands, so it depth sorts at its foot and a body behind it goes behind.
            var funnel = new GameObject("funnel").transform;
            funnel.SetParent(transform, false);

            var renderers = new List<SpriteRenderer>();
            for (int i = 0; i < BandCount; i++)
            {
                float k = i / (float)(BandCount - 1);
                var bt = new GameObject("band").transform;
                bt.SetParent(funnel, false);
                // Narrow foot, flaring top - eased so most of the widening happens up high.
                float w = Mathf.Lerp(r * 0.55f, Tuning.Tornado.TopWidth, Mathf.Pow(k, 1.5f));
                var band = new Band
                {
                    T = bt,
                    Height = k * Tuning.Tornado.Height,
                    Width = w,
                    // Ascending orders: each band over the one below, so the flare at the top
                    // is the part that reads as nearest the camera.
                    // Tall enough to meet the bands above and below, so the body is continuous.
                    Body = Layer(bt, Spr.Glow,
                                 new Vector2(w * 1.15f, Mathf.Max(w * Flat * 1.3f, Tuning.Tornado.Height / (BandCount - 1) * 2.6f)),
                                 i * 3, "body"),
                    Edge = Layer(bt, Spr.Ring, new Vector2(w, w * Flat), i * 3 + 1, "edge"),
                };
                renderers.Add(band.Body);
                renderers.Add(band.Edge);
                _bands.Add(band);

                for (int s = 0; s < StreaksPerBand; s++)
                {
                    var sr = Layer(funnel, Spr.Glow, Vector2.one, i * 3 + 2, "streak");
                    renderers.Add(sr);
                    _motes.Add(new Mote
                    {
                        T = sr.transform, Sr = sr, Band = i,
                        Phase = s * Mathf.PI + Random.Range(-0.6f, 0.6f),
                        // Faster near the foot, as a real funnel's wind is - and never two bands
                        // at one rate, or the whole column turns as one rigid object.
                        Rate = Mathf.Lerp(1.25f, 0.8f, k) * Random.Range(0.9f, 1.1f),
                        Size = Mathf.Lerp(0.26f, 0.62f, k),
                    });
                }
            }

            for (int d = 0; d < DustMotes; d++)
            {
                var sr = Layer(funnel, Spr.Glow, Vector2.one, 0, "dust");
                renderers.Add(sr);
                _motes.Add(new Mote
                {
                    T = sr.transform, Sr = sr, Band = -1,
                    Phase = d * Mathf.PI * 2f / DustMotes,
                    Rate = Random.Range(0.55f, 0.8f),
                    Size = Random.Range(0.3f, 0.5f),
                });
            }

            DepthSorted.Attach(funnel.gameObject, 0f, false, renderers.ToArray());
        }

        static SpriteRenderer Layer(Transform parent, Sprite sprite, Vector2 size, int order, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Color.clear;
            sr.sortingOrder = order;
            return sr;
        }

        // ---- the clock ----

        void Update()
        {
            float dt = Time.deltaTime;
            _clock += dt;
            _age += dt;

            switch (_phase)
            {
                case Phase.Forming:
                    if (_age >= Tuning.Tornado.FormSeconds) { _phase = Phase.Spinning; _age = 0f; }
                    break;
                case Phase.Spinning:
                    Wander(dt);
                    TickDamage(dt);
                    if (_age >= Tuning.Tornado.SpinSeconds) Dissipate();
                    break;
                case Phase.Dissipating:
                    // Coasts to a stop rather than freezing where the clock ran out.
                    Wander(dt * (1f - Mathf.Clamp01(_age / Tuning.Tornado.DissipateSeconds)));
                    if (_age >= Tuning.Tornado.DissipateSeconds) { Destroy(gameObject); return; }
                    break;
            }

            Animate();
        }

        /// <summary>
        /// The ring is the hitbox, measured to the player's PHYSICS pose (SpikePit's reason).
        /// Reset rather than frozen on stepping out, the pits' rule: a part-charged tick held
        /// across a brush past would land the instant the next one touched.
        /// </summary>
        void TickDamage(float dt)
        {
            if (_player == null || _playerHealth == null || _playerHealth.IsDead) { _tick = 0f; return; }
            var p = _playerBody != null ? _playerBody.position : (Vector2)_player.position;
            float r = Tuning.Tornado.Radius;
            if (((Vector2)transform.position - p).sqrMagnitude > r * r) { _tick = 0f; return; }

            _tick += dt;
            if (_tick < Tuning.Tornado.TickInterval) return;
            _tick = 0f;
            if (PlayerImmune != null && PlayerImmune()) return;   // Sylph
            _playerHealth.Take(new DamageInfo(_damagePerSecond * Tuning.Tornado.TickInterval,
                                              ElementType.Air, gameObject));
        }

        // ---- movement ----

        /// <summary>
        /// A drift whose heading turns at a rate made of two incommensurate sines - it curls,
        /// loops and straightens without a visible beat. When the step ahead is not open floor,
        /// the heading swings to the nearest open direction (alternating sides, widening), so it
        /// slides along a wall rather than bouncing off it at a fixed angle.
        /// </summary>
        void Wander(float dt)
        {
            if (dt <= 0f) return;
            float turn = Mathf.Sin(_clock * 0.41f + _wanderSeed) * 0.65f
                       + Mathf.Sin(_clock * 0.97f + _wanderSeed * 1.7f) * 0.35f;
            _heading += turn * Tuning.Tornado.WanderDegreesPerSecond * Mathf.Deg2Rad * dt;

            float step = Tuning.Tornado.Speed * dt;
            var pos = (Vector2)transform.position;
            for (int i = 0; i <= 12; i++)
            {
                // 0, +15, -15, +30, -30 ... +90, -90: never doubling back in one frame.
                float off = (i + 1) / 2 * 15f * Mathf.Deg2Rad * (i % 2 == 0 ? -1f : 1f);
                float h = _heading + off;
                var next = pos + new Vector2(Mathf.Cos(h), Mathf.Sin(h)) * step;
                if (!Open(next)) continue;
                _heading = h;
                transform.position = next;
                return;
            }
            // Boxed in on every forward side: turn round, and try again next frame.
            _heading += Mathf.PI;
        }

        /// <summary>
        /// Can the funnel's foot stand at <paramref name="p"/>? Standing room for the ring, no
        /// pit under any of it, nothing solid inside it. Also what the storm asks before forming
        /// one - a funnel that could not stand where it formed would never move.
        /// </summary>
        public static bool Open(Vector2 p)
        {
            float r = Tuning.Tornado.Radius;
            if (!Arena.OnFloor(p, r + 0.15f)) return false;

            var pits = FloorPits.Live;
            for (int i = 0; i < pits.Count; i++)
            {
                var pit = pits[i];
                if (pit == null) continue;
                var f = pit.Footprint;
                float dx = Mathf.Max(f.xMin - p.x, 0f, p.x - f.xMax);
                float dy = Mathf.Max(f.yMin - p.y, 0f, p.y - f.yMax);
                if (dx * dx + dy * dy < r * r) return false;
            }

            _hits ??= new List<Collider2D>();
            Physics2D.OverlapCircle(p, r, ContactFilter2D.noFilter, _hits);
            for (int i = 0; i < _hits.Count; i++)
                if (Enemies.NavField.Solid(_hits[i])) return false;
            return true;
        }

        // ---- the picture ----

        void Animate()
        {
            float r = Tuning.Tornado.Radius;
            float form = Tuning.Tornado.FormSeconds;

            // How much of the funnel stands (0..1, bottom-up while forming) and how visible all
            // of it is (fading while dissipating).
            float built, alpha, unwind = 0f;
            switch (_phase)
            {
                case Phase.Forming: built = Mathf.Clamp01(_age / form); alpha = 1f; break;
                case Phase.Spinning: built = 1f; alpha = 1f; break;
                default:
                    float k = Mathf.Clamp01(_age / Tuning.Tornado.DissipateSeconds);
                    built = _fadeFrom;
                    alpha = _fadeFrom * (1f - k);
                    unwind = k;
                    break;
            }

            // The ring: pulses while forming (the warning), steady while it can hurt, gone with
            // the funnel. Brightest while live, so "this can hurt now" is the loudest state.
            if (_rim != null)
            {
                float a = _phase switch
                {
                    Phase.Forming => Mathf.Clamp01(_age / form) * (0.35f + 0.25f * Mathf.Sin(_clock * 14f)),
                    Phase.Spinning => 0.65f,
                    _ => 0.3f * alpha,
                };
                _rim.color = WithAlpha(RimColor, a);
            }
            if (_shadow != null) _shadow.color = new Color(0f, 0f, 0f, 0.38f * Mathf.Max(built, 0.4f) * alpha);

            // The funnel snakes: its foot stays put, its top sways - two rates, so the bend wanders.
            for (int i = 0; i < _bands.Count; i++)
            {
                var b = _bands[i];
                if (b.T == null) continue;
                float k = i / (float)(_bands.Count - 1);
                // Each band appears as the build passes it, the foot first.
                float shown = Mathf.Clamp01(built * _bands.Count - i) * alpha;
                float sway = SwayAt(k);
                float spread = 1f + unwind * 0.6f;
                b.T.localPosition = new Vector3(sway, b.Height * (1f + unwind * 0.25f), 0f);
                b.T.localScale = new Vector3(spread, spread, 1f);
                if (b.Body != null) b.Body.color = WithAlpha(Air, 0.34f * shown);
                if (b.Edge != null) b.Edge.color = WithAlpha(Streak, (0.2f - 0.08f * k) * shown);
            }

            float spin = Tuning.Tornado.SpinDegreesPerSecond * Mathf.Deg2Rad;
            for (int i = 0; i < _motes.Count; i++)
            {
                var m = _motes[i];
                if (m.T == null) continue;
                float a = m.Phase + _clock * spin * m.Rate;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                // Nearer the camera on the LOWER half of the ellipse - brighter there, so the
                // eye reads the circling as going round, not side to side.
                float front = 0.5f - 0.5f * sin;

                if (m.Band < 0)
                {
                    // Dust skims the ground round the foot; it gathers first while forming.
                    float gather = _phase == Phase.Forming ? Mathf.Clamp01(_age / (form * 0.5f)) : 1f;
                    float orbit = r * Mathf.Lerp(1.6f, 0.95f, gather) * (1f + unwind);
                    m.T.localPosition = new Vector3(cos * orbit, sin * orbit * Flat * 1.4f + 0.05f, 0f);
                    m.T.localScale = new Vector3(m.Size * 1.4f, m.Size * 0.7f, 1f);
                    float da = (_phase == Phase.Dissipating ? alpha : gather) * Mathf.Lerp(0.35f, 0.75f, front);
                    if (m.Sr != null) m.Sr.color = WithAlpha(Dust, da);
                    continue;
                }

                if (m.Band >= _bands.Count) continue;
                var b = _bands[m.Band];
                float k = m.Band / (float)(_bands.Count - 1);
                float shown = Mathf.Clamp01(built * _bands.Count - m.Band) * alpha;
                float spread = 1f + unwind * 0.6f;
                float hw = b.Width * 0.5f * spread;
                m.T.localPosition = new Vector3(SwayAt(k) + cos * hw,
                                                b.Height * (1f + unwind * 0.25f) + sin * hw * Flat, 0f);
                // Laid along the ellipse's tangent, so a streak is wind going round.
                float tangent = Mathf.Atan2(cos * hw * Flat, -sin * hw) * Mathf.Rad2Deg;
                m.T.localRotation = Quaternion.Euler(0f, 0f, tangent);
                m.T.localScale = new Vector3(m.Size, m.Size * 0.24f, 1f);
                if (m.Sr != null) m.Sr.color = WithAlpha(Streak, Mathf.Lerp(0.2f, 0.95f, front) * shown);
            }
        }

        float SwayAt(float k)
        {
            float s = Mathf.Sin(_clock * 1.3f + k * 2.2f) * 0.16f + Mathf.Sin(_clock * 0.7f + _wanderSeed) * 0.1f;
            return s * k * k;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
