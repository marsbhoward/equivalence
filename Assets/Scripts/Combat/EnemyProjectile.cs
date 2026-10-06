using UnityEngine;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Combat
{
    /// <summary>
    /// A ranged enemy's bolt. Aimed once at launch from the frozen telegraph direction and never
    /// steers afterward - matching Wanderblade's own "rolled once, not homing" rule, since a
    /// projectile that tracks the player after leaving is not dodgeable by moving.
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
        const float HitRadius = 0.28f;

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

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Circle;
            sr.color = new Color(1f, 0.55f, 0.15f);
            sr.sortingOrder = SortingOrders.Fx - 1;
            go.transform.localScale = Vector3.one * 0.24f;

            return p;
        }

        void Update()
        {
            if (!_reflected && _owner == null) { Destroy(gameObject); return; }

            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f) { Destroy(gameObject); return; }

            var next = (Vector2)transform.position + _dir * (_speed * dt);

            // Dies at the wall rather than clamping into it - a bolt that stops at the edge and
            // keeps existing there reads as a bug, not a miss.
            var clamped = Arena.Clamp(next, 0.1f);
            if ((clamped - next).sqrMagnitude > 0.0001f) { Destroy(gameObject); return; }

            switch (HazardQuery.CheckCrossing(transform.position, next))
            {
                // A Blue force field nulls the bolt outright, whichever direction it is flying.
                case SightResult.Nulled: Destroy(gameObject); return;
                // An interior wall - the same as the outer one above: it dies there.
                case SightResult.Blocked: Destroy(gameObject); return;
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
                _reflected = true;
                _dir = -_dir;
                Spr.Flash(transform.position, 0.5f, new Color(0.6f, 0.9f, 1f), 0.2f, false);
                return;
            }

            _targetHealth.Take(new DamageInfo(_damage * _damageMul, ElementType.Earth, _owner)
            {
                Knockback = _dir * _knockback,
            });
            Spr.Flash(transform.position, 0.45f, new Color(1f, 0.55f, 0.15f), 0.18f, false);
            Destroy(gameObject);
        }

        void UpdateReflected()
        {
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, HitRadius))
            {
                if (_playerGo != null && col.gameObject == _playerGo) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                hp.Take(new DamageInfo(_damage * _damageMul, ElementType.Earth, _playerGo)
                {
                    Knockback = _dir * _knockback,
                    Displaces = true,
                });
                Spr.Flash(transform.position, 0.45f, new Color(0.6f, 0.9f, 1f), 0.18f, false);
                Destroy(gameObject);
                return;
            }
        }
    }
}
