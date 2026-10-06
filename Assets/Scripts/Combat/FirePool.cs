using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A patch of burning ground: ticks damage and re-applies burn to anything standing in it,
    /// then fades and cleans itself up.
    ///
    /// Built at runtime like everything else in this project, so it needs no prefab and survives
    /// the scene being regenerated from the CLI.
    ///
    /// Deliberately a LINGERING AREA rather than a burst. Fire's whole identity is sustained
    /// pressure - stacks that decay unless you keep the offence up - and a pool asks the same
    /// question of the player's positioning that stacks ask of their aggression: the damage is
    /// there if you can keep the fight standing in it.
    /// </summary>
    public class FirePool : MonoBehaviour
    {
        float _damagePerTick;
        float _burnScale = 1f;
        float _tickInterval;
        float _radius;
        float _remaining;
        float _life;
        float _tick;
        GameObject _owner;

        SpriteRenderer _sr;

        /// <summary>
        /// Drop a pool at a world position.
        ///
        /// <paramref name="owner"/> is excluded from the damage: fire's eruption goes off around
        /// the player's own feet, and a pool that cooked its caster would make the payoff for
        /// reaching max heat a self-inflicted wound.
        /// </summary>
        /// <param name="burnScale">What the burn a pool leaves on its victims is multiplied by -
        /// the player's Burn Power (the mastery board), so an Erupt pool burns like every other
        /// burn they apply. The pool's own ground damage is not a burn and is left alone.</param>
        public static FirePool Spawn(Vector2 pos, float radius, float damagePerSecond,
                                     float lifetime, GameObject owner, Transform parent = null,
                                     float burnScale = 1f)
        {
            var go = new GameObject("firepool");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var pool = go.AddComponent<FirePool>();
            pool._radius = Mathf.Max(0.1f, radius);
            pool._tickInterval = 0.35f;
            pool._damagePerTick = damagePerSecond * pool._tickInterval;
            pool._life = Mathf.Max(0.1f, lifetime);
            pool._remaining = pool._life;
            pool._owner = owner;
            pool._burnScale = burnScale;
            pool._tick = pool._tickInterval;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Circle;
            sr.color = new Color(1f, 0.45f, 0.15f, 0.42f);
            // On the ground, under every body: a pool is something fought OVER, so it must never
            // hide the enemies standing in it.
            sr.sortingOrder = SortingOrders.GroundDecal + 1;
            go.transform.localScale = Vector3.one * pool._radius * 2f;
            pool._sr = sr;

            return pool;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _remaining -= dt;

            if (_remaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            // Fade out over the last stretch so the pool announces its own expiry rather than
            // vanishing under someone who was standing in it on purpose.
            if (_sr != null)
            {
                float t = Mathf.Clamp01(_remaining / _life);
                var c = _sr.color;
                c.a = 0.42f * Mathf.Min(1f, t * 3f);
                _sr.color = c;

                // A slow flicker, scaled rather than tinted: tint is already carrying the fade,
                // and two signals on one channel read as neither.
                float flicker = 1f + Mathf.Sin(Time.time * 7f + transform.position.x) * 0.04f;
                transform.localScale = Vector3.one * _radius * 2f * flicker;
            }

            _tick -= dt;
            if (_tick > 0f) return;
            _tick = _tickInterval;

            foreach (var col in Physics2D.OverlapCircleAll(transform.position, _radius))
            {
                if (col == null || col.gameObject == _owner) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                Player.PlayerController.ReleaseHitFrom(_owner, hp, new DamageInfo(_damagePerTick, ElementType.Fire, _owner));
                StatusEffects.Get(hp.gameObject).ApplyBurn(_damagePerTick * 0.5f * _burnScale, 1.5f, _owner);
            }
        }
    }
}
