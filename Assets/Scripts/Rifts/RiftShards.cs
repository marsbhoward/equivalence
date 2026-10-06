using UnityEngine;
using Convergence.Core;

namespace Convergence.Rifts
{
    /// <summary>
    /// Slivers of light drifting around something rift-touched - the same debris the Rift itself
    /// carries, made attachable so the Rift Blade can carry it too.
    ///
    /// PARENTED TO WHAT IT ORBITS, so it follows a swing for free. A blade's shards that stayed
    /// where the blade used to be would read as an effect playing near a sword rather than as
    /// something the sword is doing, and the swing is exactly when anyone is looking at it.
    ///
    /// The orbits are ELLIPSES AT DIFFERENT RATES and deliberately not a ring at a shared speed:
    /// evenly spaced shards turning together read as a mechanism, and these are supposed to be
    /// caught in something rather than driven by it.
    ///
    /// `Rifts.Rift` keeps its own inline version, tuned to the fixture's much larger scale. Worth
    /// knowing they are two implementations of one idea - if the look changes, both move.
    /// </summary>
    public class RiftShards : MonoBehaviour
    {
        static readonly Color Cyan = new(0.45f, 0.88f, 1f);

        /// <summary>World units: about two body texels wide and six long - big enough to read as
        /// a splinter at the hub's zoom, small enough not to compete with the blade.</summary>
        const float ShardWidth = 0.05f;
        const float ShardLength = 0.16f;

        Transform[] _shards;
        SpriteRenderer[] _sr;
        float _reach = 1f;
        float _t;

        /// <param name="reach">Roughly how far out the shards drift, world units.</param>
        /// <param name="count">Few. On a weapon these sit beside the character every frame of the
        /// run, and a cloud of them stops being an accent and starts being noise.</param>
        /// <param name="along">
        /// How far to slide the cluster along the parent's local +Y before it starts orbiting.
        ///
        /// NEEDED BECAUSE A WEAPON'S ANCHOR IS ITS GRIP, not its middle. Attached with no offset,
        /// the shards orbited the FIST and spilled down around the character's boots - reading as
        /// ambient particles around the player rather than as something the blade is shedding,
        /// which is the one thing they exist to say. Local +Y runs up the blade and rotates with
        /// the arm, so an offset here tracks the swing for free.
        /// </param>
        public static RiftShards Attach(Transform parent, float reach, int count = 4, int order = 0,
                                        float along = 0f)
        {
            // Reused rather than doubled if something asks twice - a weapon repainted mid-run
            // would otherwise weld a second set on, which is the same trap EnsureLayers documents
            // for the rig's own layers.
            var existing = parent.GetComponentInChildren<RiftShards>();
            if (existing != null) return existing;

            var go = new GameObject("rift.shards");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, along, 0f);

            var s = go.AddComponent<RiftShards>();
            s._reach = reach;
            s._shards = new Transform[count];
            s._sr = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var c = new GameObject($"shard{i}");
                c.transform.SetParent(go.transform, false);
                var sr = c.AddComponent<SpriteRenderer>();
                sr.sprite = Spr.Capsule;
                sr.color = Cyan;
                sr.sortingOrder = order;
                s._shards[i] = c.transform;
                s._sr[i] = sr;
            }
            return s;
        }

        /// <summary>Turned off rather than destroyed when the weapon changes - the rig repaints
        /// often and churning GameObjects on every equip is work for nothing.</summary>
        public void SetShown(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        void Update()
        {
            if (_shards == null) return;
            _t += Time.deltaTime;
            float parentScale = transform.parent != null
                ? Mathf.Max(1e-4f, Mathf.Abs(transform.parent.lossyScale.y)) : 1f;

            for (int i = 0; i < _shards.Length; i++)
            {
                var sh = _shards[i];
                if (sh == null) continue;

                float phase = _t * (0.55f + i * 0.13f) + i * 2.1f;
                float rx = _reach * (0.45f + 0.35f * Mathf.Sin(i * 1.7f));
                float ry = _reach * (0.70f + 0.30f * Mathf.Cos(i * 2.3f));
                sh.localPosition = new Vector3(Mathf.Cos(phase) * rx,
                                               Mathf.Sin(phase * 0.83f + i) * ry, 0f);
                sh.localRotation = Quaternion.Euler(0f, 0f, phase * 46f);
                // A FIXED world size, not a fraction of the orbit. Scaled off the reach they came
                // out 0.015 units wide on a sword - under one screen pixel in the hub, so the
                // effect was there and invisible. Divided by the parent's own scale so a shard is
                // the same size on a display's fit-scaled sprite as in the hand.
                sh.localScale = new Vector3(ShardWidth / parentScale, ShardLength / parentScale, 1f);

                // Each fades on its own cycle, so they do not blink together - which would be the
                // mechanism read again, in time rather than in space.
                float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(phase * 0.7f + i));
                _sr[i].color = new Color(Cyan.r, Cyan.g, Cyan.b, a);
            }
        }
    }
}
