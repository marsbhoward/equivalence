using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A spinning knot of wind: pulls whatever is caught in it toward its own centre and damages
    /// harder the closer a body sits to that centre, for as long as it keeps spinning.
    ///
    /// Built at runtime like everything else in this project - no prefab, so it survives the
    /// scene being regenerated from the CLI.
    ///
    /// Modelled directly on <see cref="FirePool"/>'s own shape (spawn, tick, fade, self-destroy),
    /// with the two differences that are the whole idea: damage is weighted by DISTANCE rather
    /// than flat across the radius, and every tick also pulls - a pool is something you fight
    /// standing in, a vortex is something that keeps dragging you back to the worst part of it.
    /// </summary>
    public class AirVortex : MonoBehaviour
    {
        /// <summary>Full turns per second the outer blades spin - the entire "this is a vortex,
        /// not a stain" cue, since the damage/pull shape alone would be invisible.</summary>
        public const float SpinDegreesPerSecond = 260f;

        float _radius, _pullForce, _damageNearPerTick, _damageFarPerTick, _tickInterval;
        float _tick, _remaining, _life;
        GameObject _owner;

        SpriteRenderer _blades, _core;

        /// <summary>
        /// Drop a vortex at a world position.
        ///
        /// <paramref name="owner"/> is excluded from the pull and the damage, the same reason
        /// <see cref="FirePool.Spawn"/> excludes it - these land around the player's own feet, and
        /// a vortex that sucked its caster in would make reaching the second ability a way to hurt
        /// yourself.
        /// </summary>
        public static AirVortex Spawn(Vector2 pos, float radius, float pullForce,
                                      float damageNearPerSecond, float damageFarPerSecond,
                                      float lifetime, GameObject owner, Transform parent = null)
        {
            var go = new GameObject("airvortex");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var v = go.AddComponent<AirVortex>();
            v._radius = Mathf.Max(0.1f, radius);
            v._pullForce = pullForce;
            v._tickInterval = 0.2f;
            v._damageNearPerTick = damageNearPerSecond * v._tickInterval;
            v._damageFarPerTick = damageFarPerSecond * v._tickInterval;
            v._life = Mathf.Max(0.1f, lifetime);
            v._remaining = v._life;
            v._owner = owner;
            v._tick = v._tickInterval;

            // The outer blades: a segmented ring rather than a flat disc, because a filled circle
            // spinning reads as nothing at all - a ring broken into arcs is what actually looks
            // like it is turning.
            var blades = new GameObject("blades");
            blades.transform.SetParent(go.transform, false);
            var bsr = blades.AddComponent<SpriteRenderer>();
            bsr.sprite = Spr.SegmentedRing(5);
            bsr.color = new Color(0.80f, 0.94f, 1f, 0.55f);
            bsr.sortingOrder = SortingOrders.GroundDecal + 1;
            blades.transform.localScale = Vector3.one * v._radius * 2f;
            v._blades = bsr;

            // A bright core at the centre - where the damage actually lives, drawn so the eye
            // reads "worst here" without needing a number.
            var core = new GameObject("core");
            core.transform.SetParent(go.transform, false);
            var csr = core.AddComponent<SpriteRenderer>();
            csr.sprite = Spr.Circle;
            csr.color = new Color(0.88f, 0.97f, 1f, 0.28f);
            csr.sortingOrder = SortingOrders.GroundDecal;
            core.transform.localScale = Vector3.one * v._radius * 0.85f;
            v._core = csr;

            return v;
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

            if (_blades != null) _blades.transform.Rotate(0f, 0f, SpinDegreesPerSecond * dt);

            // Fade out over the last stretch, the same tell FirePool uses - the vortex announces
            // its own expiry instead of vanishing on whoever it just dragged into the middle.
            float t = Mathf.Clamp01(_remaining / _life);
            float fade = Mathf.Min(1f, t * 3f);
            if (_blades != null) { var c = _blades.color; c.a = 0.55f * fade; _blades.color = c; }
            if (_core != null)   { var c = _core.color;   c.a = 0.28f * fade; _core.color = c; }

            _tick -= dt;
            if (_tick > 0f) return;
            _tick = _tickInterval;

            foreach (var col in Physics2D.OverlapCircleAll(transform.position, _radius))
            {
                if (col == null || col.gameObject == _owner) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;

                var delta = (Vector2)col.transform.position - (Vector2)transform.position;
                float dist = delta.magnitude;

                // 1 at the centre, 0 at the rim - damage and pull both key off this, so a body
                // dragged closer keeps paying more the whole time it is caught.
                float inner = Mathf.Clamp01(1f - dist / _radius);
                float dmg = Mathf.Lerp(_damageFarPerTick, _damageNearPerTick, inner);
                var pull = dist > 0.0001f ? -(delta / dist) : Vector2.zero;

                Player.PlayerController.ReleaseHitFrom(_owner, hp, new DamageInfo(dmg, ElementType.Air, _owner)
                {
                    Knockback = pull * _pullForce,

                    // The whole point of a vortex is to move what it catches - the same opt-in
                    // Undertow's own pull already uses, and for the same reason: an ordinary enemy
                    // is Immovable by default, and Displaces is what overrides that. An elite's
                    // Anchored flag still refuses it, exactly as it refuses Undertow.
                    Displaces = true,
                });
            }
        }
    }
}
