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
            _burst = null;   // cut out of the air: no arms
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

        // ---- the Brimstone Heart's cross shot (2026-11-20; Tuning.Enemy.BombCross*) ----

        /// <summary>One throw's shared hit: the fireball and every arm it bursts into land on the player
        /// once between them - more to dodge, never more damage per mistake.</summary>
        public sealed class Volley { public bool Landed; }

        /// <summary>What a cross shot bursts into: its arms' ways, damage, speed, size and how long they
        /// may fly; and whether an arm a wall stops bursts once more (band 4) into a small cross.</summary>
        public sealed class CrossBurst
        {
            public Vector2[] Arms;
            public float ArmDamage, ArmSpeed, ArmRadius, ArmScale, ArmLife;
            public bool Rebounds;
            public float ReboundDamage, ReboundSpeed, ReboundReach;
            public Volley Volley = new();
        }

        CrossBurst _burst;           // the thrown fireball's: bursts where it ends
        CrossBurst _rebound;         // an arm's: bursts once more where a wall stops it
        Volley _volley;
        Enemies.BlastTell _tell;     // the floor's cross where it will burst, filling as it flies, gone when it does
        float _lifeTotal;
        bool _unparryable;

        static readonly Vector2[] Cross4 = { Vector2.up, Vector2.down, Vector2.right, Vector2.left };

        /// <summary>This shot BURSTS where it ends - its reach run out (the spot it was thrown at), a wall,
        /// the player, an enemy once reflected - into <paramref name="burst"/>'s arms. A cut or a dead owner
        /// ends it without them. <paramref name="tell"/> (may be null) goes when it does.</summary>
        public EnemyProjectile Bursts(CrossBurst burst, Enemies.BlastTell tell)
        {
            _burst = burst;
            _lifeTotal = _life;
            _volley = burst.Volley;
            _tell = tell;
            return this;
        }

        void DropTell() { if (_tell != null) Destroy(_tell.gameObject); _tell = null; }

        void OnDestroy() => DropTell();

        /// <summary>Throws the arms from here - turned back on the enemies if this shot was parried.</summary>
        void BurstArms(CrossBurst b, Vector2[] arms, float damage, float speed, float reach, bool small)
        {
            Vector2 at = transform.position;
            Combat.TexelFx.Shock(at, Combat.TexelFx.Fire, Combat.TexelFx.Fire, small ? 0.2f : 0.36f, 0.25f);
            Audio.Sfx.Play(Audio.Sound.Pop, at);
            foreach (var d in arms)
            {
                var arm = Launch(_owner, _target, _targetHealth, at, d, damage, _knockback * 0.5f, speed, reach / Mathf.Max(0.01f, speed));
                arm.Dress(_frames, b.ArmRadius, _element);
                arm.transform.localScale = Vector3.one * b.ArmScale * (small ? 0.75f : 1f);
                arm._volley = b.Volley;
                arm._unparryable = true;
                arm._reflected = _reflected;
                if (!small && b.Rebounds) arm._rebound = b;
            }
        }

        void BurstHere()
        {
            if (_burst == null) return;
            var b = _burst;
            _burst = null;
            DropTell();
            BurstArms(b, b.Arms, b.ArmDamage, b.ArmSpeed, b.ArmSpeed * b.ArmLife, false);
        }

        /// <summary>An arm stopped by a wall (band 4): a small cross, backed off the surface a touch.</summary>
        void ReboundHere()
        {
            if (_rebound == null) return;
            var b = _rebound;
            _rebound = null;
            transform.position = (Vector2)transform.position - _dir * 0.25f;
            BurstArms(b, Cross4, b.ReboundDamage, b.ReboundSpeed, b.ReboundReach, true);
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

        /// <summary>Gone without hurting anything - a missile bursts, a bolt just stops, a cross shot
        /// throws its arms; an arm a WALL stopped (<paramref name="wall"/>) may burst once more.</summary>
        void Expire(bool wall = false)
        {
            if (_explodes) Burst();
            BurstHere();
            if (wall) ReboundHere();
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
            if (!_reflected && _owner == null) { _burst = null; _rebound = null; Expire(); return; }
            if (_frames != null && _frames.Length > 0 && _sr != null)
                _sr.sprite = _frames[Mathf.FloorToInt(Time.time * 18f) % _frames.Length];

            float dt = Time.deltaTime;
            // The last frame flies only what is left of the reach, then ends - after its hit test, so a
            // shot thrown at a spot reaches it (a slow frame used to end it a step short).
            bool last = _life <= dt;
            float fly = last ? Mathf.Max(0f, _life) : dt;
            _life -= dt;
            if (_tell != null) _tell.Progress = 1f - Mathf.Clamp01(_life / Mathf.Max(0.01f, _lifeTotal));

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

            var next = (Vector2)transform.position + _dir * (_speed * fly);

            // Dies at the wall rather than clamping into it - a bolt that stops at the edge and
            // keeps existing there reads as a bug, not a miss. An open edge is no wall: the bolt
            // flies on over the void until it is gone.
            var clamped = Arena.ShotClamp(next, 0.1f);
            if ((clamped - next).sqrMagnitude > 0.0001f || Arena.ShotGone(next)) { Expire(wall: !Arena.ShotGone(next)); return; }
            // Mudslide's bank stops an enemy's shot (Combat.MudWall); a reflected one is the player's.
            if (!_reflected && MudWall.Blocks(transform.position, next)) { TexelFx.Puff(next, ArsenalFx.Mud, 6, 6f, 0.4f, TexelFx.Air); Expire(); return; }

            switch (HazardQuery.CheckCrossing(transform.position, next))
            {
                // A Blue force field nulls the bolt outright, whichever direction it is flying.
                case SightResult.Nulled: Expire(); return;
                // An interior wall - the same as the outer one above: it dies there.
                case SightResult.Blocked: Expire(wall: true); return;
                case SightResult.Amplified: _damageMul = 2f; break;
            }
            var prev = (Vector2)transform.position;
            transform.position = next;

            bool gone = !_reflected ? UpdateOutbound(prev) : UpdateReflected();
            if (!gone && last) Expire();
        }

        /// <summary>The nearest a step from <paramref name="a"/> to <paramref name="b"/> came to <paramref name="p"/>:
        /// a fast shot on a slow frame must not step clean past the player.</summary>
        static float PassDistance(Vector2 a, Vector2 b, Vector2 p)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(a + ab * t, p);
        }

        /// <summary>True when this shot is done (destroyed).</summary>
        bool UpdateOutbound(Vector2 prev)
        {
            if (_target == null || _targetHealth == null || _targetHealth.IsDead) return false;
            if (PassDistance(prev, transform.position, _target.position) > HitRadius) return false;
            // the throw has already hit: the rest of it flies on through
            if (_volley != null && _volley.Landed) return false;

            if (!_unparryable && _targetController != null && _targetController.TryParry(transform.position, _owner))
            {
                // A missile hurts only the player (the user's call): parried, it bursts, harmless.
                if (_explodes) { Burst(); Destroy(gameObject); return true; }
                _reflected = true;
                _dir = -_dir;   // the parry draws its own sparks (PlayerController.ParryTell)
                return false;
            }

            _targetHealth.Take(new DamageInfo(_damage * _damageMul, _element, _owner)
            {
                Knockback = _dir * _knockback,
            });
            if (_volley != null) _volley.Landed = true;
            BurstHere();
            if (_explodes) Burst();
            // Chips off the player, thrown on along the bolt's line, under the body.
            else TexelFx.Chips(transform.position, TexelFx.Fire, _dir, 2.6f, 8, 26f, 0.3f, ReleaseCanvas.UnderBody(_target));
            Destroy(gameObject);
            return true;
        }

        bool UpdateReflected()
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
                BurstHere();
                Destroy(gameObject);
                return true;
            }
            return false;
        }
    }
}
