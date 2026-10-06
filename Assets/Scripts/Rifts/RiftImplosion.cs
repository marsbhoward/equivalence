using UnityEngine;
using Convergence.Core;

namespace Convergence.Rifts
{
    /// <summary>
    /// Something collapsing into itself - the death an expired Rift Box dies, and the death
    /// anything killed by the Rift Blade dies.
    ///
    /// AN IMPLOSION IS NOT A FLASH PLAYED BACKWARDS, which is what the box's expiry used to be: a
    /// single `Spr.Flash` and gone, indistinguishable from every other burst in the game. What
    /// makes a collapse read is that things move INWARD and arrive - a ring that contracts onto the
    /// point, shards drawn in rather than thrown out, and only then the small hard pop of the thing
    /// closing. The pop last, not first: an implosion that starts with its brightest moment is an
    /// explosion with the middle cut out.
    ///
    /// SHARED BY BOTH USES ON PURPOSE. The point of giving the blade this death is that a player
    /// who has watched a box run out already knows what it means - the same picture in two places
    /// is the whole idea, and two implementations would drift the first time either was tuned.
    /// </summary>
    public class RiftImplosion : MonoBehaviour
    {
        const float Seconds = 0.45f;
        const int Shards = 7;

        static readonly Color Cyan = new(0.45f, 0.88f, 1f);
        static readonly Color Violet = new(0.62f, 0.42f, 0.95f);

        Transform[] _shards;
        SpriteRenderer[] _shardSr;
        SpriteRenderer _ring, _core;
        float[] _angle, _radius;
        float _t;
        float _scale;

        /// <param name="scale">Roughly the radius things collapse from. A box is small; an enemy
        /// wants the effect at about its own size, or the collapse looks like it happened to
        /// something else standing nearby.</param>
        public static RiftImplosion At(Vector2 where, float scale = 1f)
        {
            var go = new GameObject("rift.implosion");
            go.transform.position = where;

            var e = go.AddComponent<RiftImplosion>();
            e._scale = Mathf.Max(0.2f, scale);

            // The contracting ring: the boundary arriving at the point.
            e._ring = Quad(go.transform, "ring", Spr.ThinRing, Cyan, SortingOrders.Fx - 1);

            // The core only appears at the END - see the class header on why the pop is last.
            e._core = Quad(go.transform, "core", Spr.Circle, Color.white, SortingOrders.Fx);
            e._core.color = new Color(1f, 1f, 1f, 0f);

            e._shards = new Transform[Shards];
            e._shardSr = new SpriteRenderer[Shards];
            e._angle = new float[Shards];
            e._radius = new float[Shards];
            for (int i = 0; i < Shards; i++)
            {
                var sr = Quad(go.transform, $"shard{i}", Spr.Capsule, Cyan, SortingOrders.Fx - 1);
                e._shards[i] = sr.transform;
                e._shardSr[i] = sr;
                // Spread unevenly. Evenly spaced shards read as a mechanism firing rather than as
                // debris being pulled in.
                e._angle[i] = (i / (float)Shards) * Mathf.PI * 2f + Random.Range(-0.35f, 0.35f);
                e._radius[i] = Random.Range(0.75f, 1.15f);
            }
            return e;
        }

        void Update()
        {
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Seconds);
            if (k >= 1f) { Destroy(gameObject); return; }

            // Eased so the collapse ACCELERATES - slow at the edge, fast at the throat. A linear
            // draw-in reads as things being tidied away; the acceleration is what says they are
            // falling into something.
            float pull = k * k * k;

            for (int i = 0; i < Shards; i++)
            {
                float r = _radius[i] * _scale * (1f - pull);
                // They spiral rather than falling straight in, which is what tells the eye there
                // is a throat at the middle rather than a magnet.
                float a = _angle[i] + pull * 2.6f;
                _shards[i].localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
                _shards[i].localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
                _shards[i].localScale = new Vector3(0.05f * _scale, 0.34f * _scale * (1f - pull * 0.6f), 1f);
                _shardSr[i].color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f * (1f - k * 0.5f));
            }

            float ringR = _scale * 1.25f * (1f - pull);
            _ring.transform.localScale = new Vector3(ringR * 2f, ringR * 2f, 1f);
            _ring.color = new Color(Violet.r, Violet.g, Violet.b, 0.85f * (1f - k * 0.4f));

            // The pop: nothing for most of the life, then a hard bright point as everything
            // arrives at once.
            float pop = Mathf.Clamp01((k - 0.78f) / 0.22f);
            _core.transform.localScale = Vector3.one * (_scale * 0.55f * pop);
            _core.color = new Color(1f, 1f, 1f, pop * (1f - pop) * 4f);   // in and straight out again
        }

        static SpriteRenderer Quad(Transform parent, string name, Sprite sprite, Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
