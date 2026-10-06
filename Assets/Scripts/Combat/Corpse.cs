using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>Who a corpse is in the way of.</summary>
    public enum CorpseMode
    {
        /// <summary>Dead Weight: it blocks the player's walk; enemies pass through.</summary>
        BlocksPlayer,

        /// <summary>Charnel (Dead Weight's Nigredo): as above, and it stops the player's thrown
        /// weapons and arrows too.</summary>
        StopsShots,

        /// <summary>Ossuary (its Albedo): it blocks ENEMIES instead, cover the player made.</summary>
        BlocksEnemies,
    }

    /// <summary>
    /// A body left lying where an enemy died, for a few seconds - the exchange's Dead Weight and
    /// what it can become. Static (a corpse the player can shove out of the way is not an
    /// obstacle) and on the decal layer.
    /// </summary>
    public class Corpse : MonoBehaviour
    {
        float _life;
        float _total;
        SpriteRenderer _sr;
        [SerializeField] CorpseMode _mode;
        [SerializeField] float _radius;

        /// <summary>Ossuary's corpse: enemies route round it.</summary>
        public bool BlocksEnemies => _mode == CorpseMode.BlocksEnemies;

        /// <summary>Corpses that stop shots, for the projectiles to ask. Non-readonly and
        /// null-guarded - a reload empties it and they simply stop blocking until they fade.</summary>
        static List<Corpse> _stoppers;

        public static Corpse Spawn(Vector3 at, float seconds, GameObject player, CorpseMode mode = CorpseMode.BlocksPlayer)
        {
            var go = new GameObject("corpse");
            go.transform.position = at;

            var c = go.AddComponent<Corpse>();
            c._life = c._total = seconds;
            c._mode = mode;
            c._radius = 0.42f;

            c._sr = go.AddComponent<SpriteRenderer>();
            c._sr.sprite = Spr.Circle;
            c._sr.color = mode == CorpseMode.BlocksEnemies
                ? new Color(0.78f, 0.74f, 0.62f, 0.8f)      // bone: the player's cover
                : new Color(0.30f, 0.16f, 0.18f, 0.75f);
            c._sr.sortingOrder = SortingOrders.GroundDecal + 1;
            go.transform.localScale = Vector3.one * 0.8f;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = c._radius;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;

            // Asked of the enemy registry rather than by scanning every Collider2D in the scene.
            // Dead Weight's corpse is in the PLAYER's way only, so a pack does not pile up on its
            // own dead (a movement cost, not a free wall); Ossuary's is the opposite way round.
            if (mode == CorpseMode.BlocksEnemies)
            {
                var own = player != null ? player.GetComponent<Collider2D>() : null;
                if (own != null) Physics2D.IgnoreCollision(col, own);
            }
            else
            {
                Enemies.EnemyRegistry.Prune();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    if (e == null) continue;
                    var other = e.GetComponent<Collider2D>();
                    if (other != null) Physics2D.IgnoreCollision(col, other);
                }
            }

            if (mode == CorpseMode.StopsShots) (_stoppers ??= new List<Corpse>()).Add(c);
            if (mode == CorpseMode.BlocksEnemies) Enemies.NavField.MarkDirty();
            return c;
        }

        /// <summary>
        /// Charnel: whether a shot travelling from <paramref name="from"/> to <paramref name="to"/>
        /// this frame meets a corpse. Returns the point it stops at.
        /// </summary>
        public static bool StopsShot(Vector2 from, Vector2 to, out Vector2 at)
        {
            at = to;
            if (_stoppers == null || _stoppers.Count == 0) return false;
            var d = to - from;
            float len2 = d.sqrMagnitude;
            for (int i = _stoppers.Count - 1; i >= 0; i--)
            {
                var c = _stoppers[i];
                if (c == null) { _stoppers.RemoveAt(i); continue; }
                Vector2 p = c.transform.position;
                // A shot that STARTS inside a corpse (a disc bouncing off the body it just killed)
                // passes out of it - only one arriving from outside is stopped.
                if ((from - p).sqrMagnitude <= c._radius * c._radius) continue;
                float t = len2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - from, d) / len2) : 0f;
                var near = from + d * t;
                if ((near - p).sqrMagnitude > c._radius * c._radius) continue;
                at = near;
                return true;
            }
            return false;
        }

        void OnDestroy()
        {
            _stoppers?.Remove(this);
            if (_mode == CorpseMode.BlocksEnemies) Enemies.NavField.MarkDirty();
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) { Destroy(gameObject); return; }

            if (_sr == null) return;
            var c = _sr.color;
            c.a = 0.75f * Mathf.Clamp01(_life / Mathf.Max(0.01f, _total * 0.5f));
            _sr.color = c;
        }
    }
}
