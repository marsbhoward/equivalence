using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Discs thrown out and driven in a circle around the player - a circular saw at arm's
    /// length, striking a fixed number of times before returning.
    ///
    /// The ranged counterpart to Carousel. Carousel is one spin that catches everything inside
    /// melee reach at once; Orrery sweeps a wider ring repeatedly, so it trades the burst for
    /// coverage and duration. Standing still through it is the cost - the discs only cover
    /// ground the player stays near.
    ///
    /// Each enemy can be hit once per SWEEP, not once per move: a body that stays in the ring
    /// eats every pass, and one that steps out after the first is only clipped once. That is
    /// what makes the radius worth reading rather than a number attached to a button.
    /// </summary>
    public class OrbitingDiscs : MonoBehaviour
    {
        public float Radius = 2.2f;

        /// <summary>How many full sweeps land before the discs come home.</summary>
        public int Sweeps = 5;

        /// <summary>Seconds per sweep.</summary>
        public float SweepSeconds = 0.26f;

        /// <summary>How wide a band around the ring counts as a hit.</summary>
        public float Band = 0.85f;

        readonly List<Transform> _discs = new();
        readonly HashSet<Health> _hitThisSweep = new();

        GameObject _owner;
        Player.PlayerController _player;
        Art.Gear.ICharacterRig _rig;
        float _damage;
        ElementType _element;

        float _angle;
        float _elapsed;
        int _sweepsDone;

        public static OrbitingDiscs Launch(GameObject owner, Player.PlayerController player,
                                           Art.Gear.ICharacterRig rig, int count, float damage,
                                           ElementType element, float radius, int sweeps)
        {
            var go = new GameObject("orbiting-discs");
            go.transform.position = owner.transform.position;

            var o = go.AddComponent<OrbitingDiscs>();
            o._owner = owner;
            o._player = player;
            o._rig = rig;
            o._damage = damage;
            o._element = element;
            o.Radius = radius;
            o.Sweeps = Mathf.Max(1, sweeps);

            Sprite sprite = null; Color tint = Color.white; Vector2 size = Vector2.one;
            bool hasWeapon = rig != null && rig.TryGetWeaponVisual(out sprite, out tint, out size);
            // A split pair (the Armillary) alternates its halves round the set: disc i is the
            // main hand's half on even i, the off hand's on odd.
            Sprite offSprite = null;
            if (hasWeapon) rig.TryGetDiscVisual(1, out offSprite, out _, out _);

            for (int i = 0; i < count; i++)
            {
                var d = new GameObject($"orbit-{i}");
                d.transform.SetParent(go.transform, false);

                var sr = DiscVisual.Add(d);   // spins about its centre, not its grip
                if (hasWeapon)
                {
                    sr.sprite = i % 2 == 1 && offSprite != null ? offSprite : sprite;
                    sr.color = tint;
                    var native = sprite.bounds.size;
                    sr.transform.localScale = new Vector3(
                        native.x > 0.0001f ? size.x / native.x : size.x,
                        native.y > 0.0001f ? size.y / native.y : size.y, 1f);
                }
                else
                {
                    sr.sprite = Spr.Circle;
                    sr.color = ElementInfo.Tint(element);
                    sr.transform.localScale = Vector3.one * 0.4f;
                }
                sr.sortingOrder = SortingOrders.Fx - 1;
                DiscVisual.Centre(sr);
                // the Rift Disc's light-cycle wall, if the hand it left carried one
                LightCycleTrail.Follow(rig, d.transform, sr, SortingOrders.Fx - 2);
                // and Rai's arcs, likewise
                LightningArcs.Follow(rig, d.transform, sr, SortingOrders.Fx - 2);
                // and a weapon that casts light keeps lighting in flight
                Art.Gear.HeldGlow.Follow(rig, d.transform, sr);
                o._discs.Add(d.transform);
            }

            // Both discs leave the hands for the duration.
            rig?.SetWeaponVisible(false);
            return o;
        }

        void Update()
        {
            // Follows the player rather than sitting where it was cast, so walking while it runs
            // steers the saw. That is the move's only input once it is out, and it is what makes
            // it feel driven instead of watched.
            if (_owner != null) transform.position = _owner.transform.position;

            float dt = Time.deltaTime;
            _elapsed += dt;
            _angle += 360f / Mathf.Max(0.01f, SweepSeconds) * dt;

            int n = _discs.Count;
            for (int i = 0; i < n; i++)
            {
                var d = _discs[i];
                if (d == null) continue;
                float a = (_angle + i * (360f / Mathf.Max(1, n))) * Mathf.Deg2Rad;
                d.localPosition = new Vector3(Mathf.Cos(a) * Radius, Mathf.Sin(a) * Radius, 0f);
                d.Rotate(0f, 0f, 540f * dt);
            }

            Sweep();

            if (_elapsed >= SweepSeconds * (_sweepsDone + 1))
            {
                _sweepsDone++;
                _hitThisSweep.Clear();       // a new pass may hit the same bodies again
                if (_sweepsDone >= Sweeps) Finish();
            }
        }

        void Sweep()
        {
            if (_owner == null) return;

            var filter = new ContactFilter2D { useTriggers = true };
            var found = new List<Collider2D>();
            Physics2D.OverlapCircle(transform.position, Radius + Band, filter, found);

            foreach (var col in found)
            {
                if (col == null || col.gameObject == _owner) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead || hp.gameObject == _owner) continue;
                if (_hitThisSweep.Contains(hp)) continue;

                // Inside the BAND, not inside the circle: the discs are a ring, and something
                // stood right next to the player is in the hole in the middle of it.
                float dist = Vector2.Distance(transform.position, hp.transform.position);
                if (dist < Radius - Band) continue;

                _hitThisSweep.Add(hp);

                // Orrery: only ever launched from ThrowVolley, a finisher-only method - see
                // DamageInfo.IsFinisher.
                var info = new DamageInfo(_damage, _element, _owner) { IsFinisher = true };
                hp.Take(info);

                _player?.Resource?.OnHitLanded(hp, info);
                _player?.NotifyThrownHit(hp, info, 0.5f, false);
            }
        }

        void Finish()
        {
            _rig?.SetWeaponVisible(true);
            foreach (var d in _discs)
                if (d != null) Destroy(d.gameObject);
            _discs.Clear();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // If anything destroys this early - a floor ending, the player dying - the hands must
            // still come back. SetWeaponVisible is reference counted, so the pairing has to hold
            // on every exit path or the character is left permanently empty-handed.
            if (_discs.Count > 0) _rig?.SetWeaponVisible(true);
        }
    }
}
