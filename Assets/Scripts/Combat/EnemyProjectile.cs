using UnityEngine;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Combat
{
    /// <summary>
    /// A ranged enemy's bolt. Aimed once at launch from the frozen telegraph direction and never
    /// steers afterward - matching Wanderblade's own "rolled once, not homing" rule, since a
    /// projectile that tracks the player after leaving is not dodgeable by moving. The one
    /// exception is the Stinger's MISSILE (<see cref="Homing"/>): slow enough to outwalk, turning
    /// slowly enough to sidestep, and cut out of the air by any attack.
    ///
    /// Checked against the TARGET directly (there is only ever one - the player) rather than a
    /// physics overlap: EnemyController already resolves _target/_targetHealth this way, and a
    /// broad OverlapCircle would also catch other enemies standing in the line.
    ///
    /// UNLESS PARRIED. A perfectly-timed defensive ability reverses it along the same line it
    /// arrived on - not homed onto the shooter - and flips who it can hurt from the player to
    /// every enemy, the same OverlapCircle sweep ThrownBlade/ThrownDisc already use for the
    /// player's own thrown weapons. It genuinely might hit nothing.
    /// </summary>
    public class EnemyProjectile : MonoBehaviour
    {
        const float BoltHitRadius = 0.28f;

        /// <summary>How close to the target's centre counts as a hit - the bolt's by default.</summary>
        float HitRadius = BoltHitRadius;
        ElementType _element = ElementType.Earth;
        Sprite[] _frames;
        SpriteRenderer _sr;

        GameObject _owner;
        GameObject _playerGo;
        Transform _target;
        Health _targetHealth;
        Player.PlayerController _targetController;
        Vector2 _dir;
        float _damage;
        float _knockback;
        float _speed;
        float _life;
        bool _reflected;

        /// <summary>Still flying at the player (not turned back by a parry) - what the orbiting
        /// discs stop (DiscOrbit).</summary>
        public bool Hostile => !_reflected && !_cut;
        bool _cut;

        /// <summary>Every bolt in flight - what a player's swing or shot cuts (Combat.Clash). Not
        /// readonly, and asked with a null guard: a domain reload empties it while bolts survive,
        /// and those few just fly on uncuttable.</summary>
        public static System.Collections.Generic.List<EnemyProjectile> Live = new();

        public Vector2 Position => transform.position;
        /// <summary>Which way it is flying, and whether it wears baked frames instead of the ember dart.</summary>
        public Vector2 Direction => _dir;
        public bool Dressed => _frames != null && _frames.Length > 0;
        public float Radius => HitRadius;

        void OnEnable() { Live ??= new(); if (!Live.Contains(this)) Live.Add(this); }
        void OnDisable() => Live?.Remove(this);

        /// <summary>Cut out of the air by a player's attack (Combat.Clash) or the Revolve ring:
        /// gone, dealing nothing. A missile BURSTS where it was cut (harmless - a clash cancels
        /// both blows).</summary>
        public void Cut()
        {
            if (_cut) return;
            _cut = true;
            if (_explodes) Burst();
            else SmokePuff.At(transform.position);
            Destroy(gameObject);
        }

        // ---- homing (the Stinger's missile, 2026-10-25) ----

        bool _homing, _explodes;
        float _turnDegPerSec, _splitAfter, _splitDegrees, _flown;

        /// <summary>
        /// Makes this shot a MISSILE: it turns toward the player at <paramref name="turnDegPerSec"/>
        /// for its whole flight (its lifetime is its reach), and BURSTS - on the player, when a
        /// player's attack cuts it, a parry, at a wall, or at the end of its reach. It hurts ONLY the
        /// player, by touching them - never an enemy (the user's call).
        /// </summary>
        public EnemyProjectile Homing(float turnDegPerSec)
        {
            _homing = true;
            _explodes = true;
            _turnDegPerSec = turnDegPerSec;
            return this;
        }

        /// <summary>The missile SPLITS after <paramref name="seconds"/> of flight into two that leave
        /// <paramref name="degrees"/> either side of its heading and home on from there, with what is
        /// left of its reach (Tuning.EnemyBands.MissileSplitFromBand). The halves do not split.</summary>
        public EnemyProjectile SplitsAfter(float seconds, float degrees)
        {
            _splitAfter = seconds;
            _splitDegrees = degrees;
            return this;
        }

        void Split()
        {
            Vector2 at = transform.position;
            for (int side = -1; side <= 1; side += 2)
            {
                var dir = (Vector2)(Quaternion.Euler(0f, 0f, side * _splitDegrees) * _dir);
                var half = Launch(_owner, _target, _targetHealth, at, dir, _damage, _knockback, _speed, _life);
                half.Dress(_frames, HitRadius, _element).Homing(_turnDegPerSec);
            }
            Combat.TexelFx.Star(at, Combat.TexelFx.Steel, 2, 0.12f);   // a white spark where it became two
            _cut = true;   // gone without a burst - it became the two
            Destroy(gameObject);
        }

        /// <summary>The missile's burst (the user's pick, group 4 - "violet ring"): a small SOLID violet
        /// ring - solid, as every enemy blast is - round an orange core, and a pop. A picture and a
        /// sound; whatever damage it deals is dealt by the caller.</summary>
        void Burst()
        {
            Vector2 at = transform.position;
            Combat.TexelFx.Shock(at, Combat.TexelFx.Violet, Combat.TexelFx.Violet, 0.32f, 0.3f);
            Combat.TexelFx.Star(at, Combat.TexelFx.Fire, 2, 0.14f);
            Audio.Sfx.Play(Audio.Sound.Pop, at);
        }

        /// <summary>Gone without hurting anything - a missile bursts, a bolt just stops.</summary>
        void Expire()
        {
            if (_explodes) Burst();
            Destroy(gameObject);
        }

        /// <summary>Set once and kept for the rest of the flight - a Red force field crossed
        /// either leg doubles whatever this bolt eventually lands for.</summary>
        float _damageMul = 1f;

        public static EnemyProjectile Launch(GameObject owner, Transform target, Health targetHealth,
            Vector2 origin, Vector2 dir, float damage, float knockback, float speed, float lifetime)
        {
            var go = new GameObject("enemy.bolt");
            go.transform.position = origin;

            var p = go.AddComponent<EnemyProjectile>();
            p._owner = owner;
            p._target = target;
            p._targetHealth = targetHealth;
            p._playerGo = target != null ? target.gameObject : null;
            p._targetController = target != null ? target.GetComponent<Player.PlayerController>() : null;
            p._dir = dir.normalized;
            p._damage = damage;
            p._knockback = knockback;
            p._speed = speed;
            p._life = lifetime;

            // The ordinary bolt is an ember dart (EmberDart); Dress swaps in its own frames.
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortingOrders.Fx - 1;
            p._sr = sr;
            EmberDart.On(p, SortingOrders.Fx - 1);

            return p;
        }

        /// <summary>
        /// Dresses a launched shot as something other than the bolt: baked pixel frames (drawn at
        /// scale 1, cycled), its own hit radius and element. The Bomb's fireballs - see
        /// Enemies.BombArt.Fireball. The flight, walls, force fields, parry and reflection are the
        /// bolt's, unchanged.
        /// </summary>
        public EnemyProjectile Dress(Sprite[] frames, float hitRadius, ElementType element)
        {
            _frames = frames;
            HitRadius = hitRadius;
            _element = element;
            if (_sr != null && frames != null && frames.Length > 0)
            {
                _sr.sprite = frames[0];
                _sr.color = Color.white;
                transform.localScale = Vector3.one;
            }
            return this;
        }

        void Update()
        {
            if (_cut) return;
            if (!_reflected && _owner == null) { Expire(); return; }
            if (_frames != null && _frames.Length > 0 && _sr != null)
                _sr.sprite = _frames[Mathf.FloorToInt(Time.time * 18f) % _frames.Length];

            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f) { Expire(); return; }

            if (_homing && !_reflected && _splitAfter > 0f && (_flown += dt) >= _splitAfter) { Split(); return; }

            if (_homing && !_reflected && _target != null)
            {
                var want = (Vector2)_target.position - (Vector2)transform.position;
                if (want.sqrMagnitude > 1e-4f)
                {
                    float cur = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
                    float tgt = Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg;
                    float ang = Mathf.MoveTowardsAngle(cur, tgt, _turnDegPerSec * dt) * Mathf.Deg2Rad;
                    _dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                }
            }

            var next = (Vector2)transform.position + _dir * (_speed * dt);

            // Dies at the wall rather than clamping into it - a bolt that stops at the edge and
            // keeps existing there reads as a bug, not a miss.
            var clamped = Arena.Clamp(next, 0.1f);
            if ((clamped - next).sqrMagnitude > 0.0001f) { Expire(); return; }

            switch (HazardQuery.CheckCrossing(transform.position, next))
            {
                // A Blue force field nulls the bolt outright, whichever direction it is flying.
                case SightResult.Nulled: Expire(); return;
                // An interior wall - the same as the outer one above: it dies there.
                case SightResult.Blocked: Expire(); return;
                case SightResult.Amplified: _damageMul = 2f; break;
            }
            transform.position = next;

            if (!_reflected) UpdateOutbound();
            else UpdateReflected();
        }

        void UpdateOutbound()
        {
            if (_target == null || _targetHealth == null || _targetHealth.IsDead) return;
            if (Vector2.Distance(transform.position, _target.position) > HitRadius) return;

            if (_targetController != null && _targetController.TryParry(transform.position))
            {
                // A missile hurts only the player (the user's call): parried, it bursts, harmless.
                if (_explodes) { Burst(); Destroy(gameObject); return; }
                _reflected = true;
                _dir = -_dir;   // the parry draws its own sparks (PlayerController.ParryTell)
                return;
            }

            _targetHealth.Take(new DamageInfo(_damage * _damageMul, _element, _owner)
            {
                Knockback = _dir * _knockback,
            });
            if (_explodes) Burst();
            // Chips off the player, thrown on along the bolt's line, under the body.
            else TexelFx.Chips(transform.position, TexelFx.Fire, _dir, 2.6f, 8, 26f, 0.3f, ReleaseCanvas.UnderBody(_target));
            Destroy(gameObject);
        }

        void UpdateReflected()
        {
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, HitRadius))
            {
                if (_playerGo != null && col.gameObject == _playerGo) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                hp.Take(new DamageInfo(_damage * _damageMul, _element, _playerGo)
                {
                    Knockback = _dir * _knockback,
                    Displaces = true,
                });
                TexelFx.Chips(transform.position, TexelFx.Fire, _dir, 2.6f, 8, 26f, 0.3f);
                Destroy(gameObject);
                return;
            }
        }
    }
}
