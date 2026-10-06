using UnityEngine;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Combat
{
    /// <summary>
    /// A single arrow, fired at whatever the player is currently locked onto.
    ///
    /// Unlike <see cref="ThrownDisc"/> this never bounces and never returns - the bow itself
    /// stays in the character's hand the whole time, so there is no weapon to hand back and no
    /// reason to hide it. It is also why this needs its own small projectile sprite rather than
    /// borrowing the held weapon's own art the way a thrown disc does.
    /// </summary>
    public class ThrownArrow : MonoBehaviour
    {
        public float Speed = 15f;

        GameObject _owner;
        Player.PlayerController _player;
        float _damage;
        ElementType _element;
        float _nearFraction;
        Vector2 _origin;
        float _range;
        Health _target;

        /// <summary>Whether this shot came off a finisher rather than a basic - see
        /// DamageInfo.IsFinisher.</summary>
        bool _isFinisher;
        bool _crit;

        /// <summary>Gear's Splash and (bow-only) Pierce, as fractions of this arrow's hit.</summary>
        public float SplashFraction, PierceFraction;

        /// <summary>Set once and kept for the rest of the flight - a Red force field crossed on
        /// the way in doubles whatever this arrow eventually lands for.</summary>
        float _damageMul = 1f;

        public static ThrownArrow Fire(GameObject owner, Health target, Vector2 aim, float damage,
                                       ElementType element, float range, float nearFraction,
                                       bool isFinisher = false, bool crit = false)
        {
            var go = new GameObject("thrown-arrow");
            go.transform.position = owner.transform.position;

            var a = go.AddComponent<ThrownArrow>();
            a._owner = owner;
            a._player = owner.GetComponent<Player.PlayerController>();
            a._damage = damage;
            a._element = element;
            a._nearFraction = Mathf.Clamp01(nearFraction);
            a._isFinisher = isFinisher;
            a._crit = crit;
            a._origin = owner.transform.position;
            a._range = Mathf.Max(0.01f, range);
            a._target = target;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Capsule;
            sr.color = ElementInfo.Tint(element);
            sr.sortingOrder = SortingOrders.Fx - 1;
            go.transform.localScale = new Vector3(0.5f, 0.11f, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f,
                Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);

            return a;
        }

        void Update()
        {
            if (_owner == null || _target == null || _target.IsDead) { Destroy(gameObject); return; }

            Vector2 to = (Vector2)_target.transform.position - (Vector2)transform.position;
            float dist = to.magnitude;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);

            float step = Speed * Time.deltaTime;
            Vector2 prevPos = transform.position;
            Vector2 nextPos = dist <= step ? (Vector2)_target.transform.position : prevPos + to / dist * step;

            switch (HazardQuery.CheckCrossing(prevPos, nextPos))
            {
                // A Blue force field nulls the shot outright - it vanishes here, dealing nothing.
                case SightResult.Nulled: Destroy(gameObject); return;
                case SightResult.Blocked: Destroy(gameObject); return;
                case SightResult.Amplified: _damageMul = 2f; break;
            }
            // Charnel (the exchange): a corpse in the way stops the arrow.
            if (Corpse.StopsShot(prevPos, nextPos, out _)) { Destroy(gameObject); return; }

            if (dist <= step) { Hit(); return; }
            transform.position = nextPos;
        }

        void Hit()
        {
            // Measured against the ORIGIN (where the shot was released), not the player's live
            // position - the same rule ThrownDisc's own FarFraction/NearFraction curves use, so
            // a recoil or a dash mid-flight cannot cheapen or inflate a shot already in the air.
            float t = Mathf.Clamp01(Vector2.Distance(_target.transform.position, _origin) / _range);
            float near = _nearFraction >= 1f ? 1f : Mathf.Lerp(_nearFraction, 1f, t);

            float dmg = _damage * near * _damageMul;
            if (_player != null) dmg = _player.ScaleThrownHit(_target, dmg, _isFinisher, _crit, t);
            var info = new DamageInfo(dmg, _element, _owner)
                { Thrown = true, IsFinisher = _isFinisher, Crit = _crit };
            var where = _target.transform.position;
            _target.Take(info);

            // Direction of travel, not the aim at release: the arrow homes, so the line it
            // carries on along is the one it was actually flying when it struck.
            CrowdHits.Splash(_target, info.Amount, SplashFraction, _element, _owner);
            CrowdHits.Pierce(_target, (Vector2)_target.transform.position - _origin, info.Amount,
                             PierceFraction, _element, _owner);

            _player?.Resource?.OnHitLanded(_target, info);
            _player?.NotifyThrownHit(_target, info, t, true);

            Spr.Flash(where, 0.4f, ElementInfo.Tint(_element), 0.14f, false);
            Destroy(gameObject);
        }
    }
}
