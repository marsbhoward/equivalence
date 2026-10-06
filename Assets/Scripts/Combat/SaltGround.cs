using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// SALTED GROUND - the mastery board's Fermentation: the patch an area attack or a release
    /// leaves behind, slowing what stands in it and wearing at it for a few seconds.
    ///
    /// FirePool's shape (a lingering area asked of positioning, built at runtime, faded out at
    /// the end so it never vanishes under someone using it), but ENEMIES ONLY by registry rather
    /// than by overlap, so it can never touch the player, a column or a spire.
    /// </summary>
    public class SaltGround : MonoBehaviour
    {
        const float TickInterval = 0.5f;
        const float Alpha = 0.32f;

        float _radius, _damagePerTick, _slow, _life, _remaining, _tick;
        GameObject _owner;
        SpriteRenderer _sr;

        public static SaltGround Spawn(Vector2 at, float radius, float damagePerSecond, float slowMultiplier,
                                       float seconds, GameObject owner)
        {
            var go = new GameObject("saltground");
            go.transform.position = at;

            var g = go.AddComponent<SaltGround>();
            g._radius = Mathf.Max(0.2f, radius);
            g._damagePerTick = damagePerSecond * TickInterval;
            g._slow = slowMultiplier;
            g._life = Mathf.Max(0.1f, seconds);
            g._remaining = g._life;
            g._owner = owner;
            g._tick = 0f;   // the first tick lands at once - the ground is salted where it lands

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Circle;
            sr.color = new Color(0.92f, 0.94f, 0.86f, Alpha);
            // On the ground, under every body, like a fire pool: something fought over.
            sr.sortingOrder = SortingOrders.GroundDecal + 1;
            go.transform.localScale = Vector3.one * g._radius * 2f;
            g._sr = sr;
            return g;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _remaining -= dt;
            if (_remaining <= 0f) { Destroy(gameObject); return; }

            if (_sr != null)
            {
                var c = _sr.color;
                c.a = Alpha * Mathf.Min(1f, _remaining / _life * 3f);
                _sr.color = c;
            }

            _tick -= dt;
            if (_tick > 0f) return;
            _tick = TickInterval;

            Vector2 at = transform.position;
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                if (((Vector2)e.transform.position - at).sqrMagnitude > _radius * _radius) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                StatusEffects.Get(hp.gameObject).ApplySlow(TickInterval + 0.15f, _slow);
                if (_damagePerTick > 0f) hp.Take(new DamageInfo(_damagePerTick, ElementType.Earth, _owner));
            }
        }
    }
}
