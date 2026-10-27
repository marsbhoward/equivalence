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
        /// <summary>How long a mote takes to fly from the heart to its spot.</summary>
        const float MoteFlightSeconds = 0.3f;

        float _fuse;
        float _damage;
        Transform _target;
        SpriteRenderer _ring;
        Transform _mote;
        Vector2 _from;
        float _flight;

        public static ClusterShard Spawn(Vector2 at, Transform parent, Transform target, float damage,
                                         Vector2? from = null, Sprite mote = null)
        {
            var go = new GameObject("bomb.shard");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = at;

            var s = go.AddComponent<ClusterShard>();
            s._fuse = Tuning.Enemy.ClusterDelay;
            s._damage = damage;
            s._target = target;

            // Its own small ring, the blast's edge (texel, the Heart's shockwave at a smaller size).
            s._ring = go.AddComponent<SpriteRenderer>();
            s._ring.sprite = RingSprite();
            s._ring.sortingOrder = SortingOrders.Telegraph;

            // The Brimstone Heart's elite: the shard is one of its orbiting motes, flung from the
            // heart to where it will burst, drawn at scale 1 (its own GameObject, not under the
            // ring's scale) and sorted as a body.
            if (mote != null && from.HasValue)
            {
                var m = new GameObject("bomb.mote");
                if (parent != null) m.transform.SetParent(parent, false);
                m.transform.position = from.Value;
                var msr = m.AddComponent<SpriteRenderer>();
                msr.sprite = mote;
                msr.sortingOrder = SortingOrders.Fx - 1;
                s._mote = m.transform;
                s._from = from.Value;
            }
            return s;
        }

        void Update()
        {
            _fuse -= Time.deltaTime;

            // The mote arcs out over the first part of the fuse, then sits on its spot, trembling.
            if (_mote != null)
            {
                _flight += Time.deltaTime;
                float u = Mathf.Clamp01(_flight / MoteFlightSeconds);
                var p = Vector2.Lerp(_from, transform.position, u) + Vector2.up * (Mathf.Sin(u * Mathf.PI) * 0.6f);
                if (u >= 1f) p += new Vector2(Mathf.Sin(Time.time * 40f), Mathf.Cos(Time.time * 33f)) * (1f / 28.125f);
                _mote.position = p;
            }

            if (_ring != null)
            {
                float t = 1f - Mathf.Clamp01(_fuse / Tuning.Enemy.ClusterDelay);
                _ring.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 1f, t));
            }

            if (_fuse > 0f) return;

            if (_target != null)
            {
                var hp = _target.GetComponent<Health>();
                if (hp != null && !hp.IsDead
                    && Vector2.Distance(_target.position, transform.position) <= Tuning.Enemy.ClusterRadius)
                    hp.Take(new DamageInfo(_damage, ElementType.Fire, gameObject));
            }

            TexelFx.Shock(transform.position, TexelFx.Fire, TexelFx.Grey, Tuning.Enemy.ClusterRadius, 0.3f);
            if (_mote != null) Destroy(_mote.gameObject);
            Destroy(gameObject);
        }

        static Sprite _ringSprite;

        /// <summary>The shard's ring: one texel of the fire ramp's orange at its blast's edge.</summary>
        static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            float ppu = TexelRig.TexelsPerUnit, r = Tuning.Enemy.ClusterRadius * ppu;
            int n = Mathf.CeilToInt(r) * 2 + 3;
            var px = new Color32[n * n];
            float c = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= r && d >= r - 1.2f) px[y * n + x] = Tuning.FireFx.Ramp[2];
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();
            return _ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }
    }
}
