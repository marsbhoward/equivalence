using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// One delayed blast left behind by an elite Bomb - see <see cref="ElitePattern.Cluster"/>.
    ///
    /// ITS OWN OBJECT RATHER THAN A SCHEDULED CALL ON THE BOMB, and that is not a style choice: the
    /// bomb spawns these from inside its own Died handler and is destroyed at the end of the frame,
    /// so an Invoke on it would be cancelled before it ever ran. The shard has to outlive the thing
    /// that made it.
    ///
    /// It telegraphs for its whole life. A delayed blast the player cannot see coming is not a
    /// pattern, it is a second explosion at a random time - and the point of the elite version is
    /// that the ground stays denied, which only reads if the denied ground is drawn.
    /// </summary>
    public class ClusterShard : MonoBehaviour
    {
        float _fuse;
        float _damage;
        Transform _target;
        SpriteRenderer _ring;

        public static ClusterShard Spawn(Vector2 at, Transform parent, Transform target, float damage)
        {
            var go = new GameObject("bomb.shard");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = at;

            var s = go.AddComponent<ClusterShard>();
            s._fuse = Tuning.Enemy.ClusterDelay;
            s._damage = damage;
            s._target = target;

            s._ring = go.AddComponent<SpriteRenderer>();
            s._ring.sprite = Spr.ThinRing;
            s._ring.sortingOrder = SortingOrders.Telegraph;
            go.transform.localScale = Vector3.one * (Tuning.Enemy.ClusterRadius * 2f);
            return s;
        }

        void Update()
        {
            _fuse -= Time.deltaTime;

            if (_ring != null)
            {
                float t = 1f - Mathf.Clamp01(_fuse / Tuning.Enemy.ClusterDelay);
                var c = BombArt.Blast;
                _ring.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0.15f, 0.85f, t));
            }

            if (_fuse > 0f) return;

            if (_target != null)
            {
                var hp = _target.GetComponent<Health>();
                if (hp != null && !hp.IsDead
                    && Vector2.Distance(_target.position, transform.position) <= Tuning.Enemy.ClusterRadius)
                    hp.Take(new DamageInfo(_damage, ElementType.Fire, gameObject));
            }

            Spr.Flash(transform.position, Tuning.Enemy.ClusterRadius, BombArt.Blast, 0.25f);
            Destroy(gameObject);
        }
    }
}
