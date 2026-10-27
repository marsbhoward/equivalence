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
        [SerializeField] TornadoGround _ground;
        [SerializeField] TornadoFunnel _funnel;

        /// <summary>What the pictures read each frame (Animate sets them): how much of the funnel
        /// stands (0..1, foot first), how visible it is, how far it has unwound, the clock, and the
        /// ring's strength.</summary>
        internal float Built, Alpha, Unwind, Clock, RingAlpha, Gather;

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
            // The GROUND (the hitbox ring, dust drawn in, a shadow) on the danger rung - nothing on
            // the floor covers what hurts; the FUNNEL stands, depth sorted at its foot.
            int g = Mathf.CeilToInt(Tuning.Tornado.Radius * TexelFx.Body) + 6;
            _ground = TexelPicture.Attach<TornadoGround>(transform, "ground", Vector2.zero, g, g, g, SortingOrders.GroundDanger, clip: true);
            int top = Mathf.CeilToInt(Tuning.Tornado.TopWidth * 0.5f * TexelFx.Body * 1.6f) + 8;
            int up = Mathf.CeilToInt(Tuning.Tornado.Height * 1.25f * TexelFx.Body) + 8;
            _funnel = TexelPicture.Attach<TornadoFunnel>(transform, "funnel", Vector2.zero, top, 3, up, 0);
            DepthSorted.Attach(gameObject, 0f, false, _funnel.GetComponent<SpriteRenderer>());
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
                                              ElementType.Air, gameObject) { Tick = true });
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
            float form = Tuning.Tornado.FormSeconds;
            switch (_phase)
            {
                case Phase.Forming: Built = Mathf.Clamp01(_age / form); Alpha = 1f; Unwind = 0f; break;
                case Phase.Spinning: Built = 1f; Alpha = 1f; Unwind = 0f; break;
                default:
                    float k = Mathf.Clamp01(_age / Tuning.Tornado.DissipateSeconds);
                    Built = _fadeFrom; Alpha = _fadeFrom * (1f - k); Unwind = k;
                    break;
            }
            // The ring: pulsing in while forming (the warning), steady while it can hurt, gone with
            // the funnel - brightest while live.
            RingAlpha = _phase switch
            {
                Phase.Forming => Mathf.Clamp01(_age / form) * (0.55f + 0.3f * Mathf.Sin(_clock * 14f)),
                Phase.Spinning => 1f,
                _ => 0.5f * Alpha,
            };
            Gather = _phase == Phase.Forming ? Mathf.Clamp01(_age / (form * 0.5f)) : 1f;
            Clock = _clock;
        }

        /// <summary>The funnel's sideways bend at height share <paramref name="k"/> (world units):
        /// the foot stays put, the top sways on two rates.</summary>
        internal float SwayAt(float k)
        {
            float s = Mathf.Sin(_clock * 1.3f + k * 2.2f) * 0.16f + Mathf.Sin(_clock * 0.7f + _wanderSeed) * 0.1f;
            return s * k * k;
        }
    }
}
