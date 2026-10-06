using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Discs thrown out that STOP and hang in the air, detonating when the player's next
    /// finisher executes.
    ///
    /// This is the only move in the game whose payoff is decided by a LATER decision. Every other
    /// finisher resolves itself: you press it, it happens, the chain restarts. Sublimate splits
    /// that in two, so the question stops being "which finisher is best here" and becomes "which
    /// finisher do I want this one to go off underneath" - Hold into Mark lands the detonation on
    /// amplified targets, Hold into Carousel puts it in the middle of a crowd you just pulled in.
    ///
    /// It is deliberately WEAK on its own. The move has to be worth the tempo it costs, not worth
    /// firing on its own, or the ordering is decoration.
    /// </summary>
    public class SuspendedDiscs : MonoBehaviour
    {
        /// <summary>Radius each disc damages when it goes off.</summary>
        public float BlastRadius = 1.35f;

        /// <summary>Discs fade out on their own if nothing ever detonates them.</summary>
        public float LifeSeconds = 12f;

        readonly List<Transform> _discs = new();
        float _damage;
        ElementType _element;
        GameObject _owner;
        Player.PlayerController _player;
        float _life;

        public int Count => _discs.Count;

        public static SuspendedDiscs Place(GameObject owner, Player.PlayerController player,
                                           Art.Gear.ICharacterRig rig, Vector2 aim, int count,
                                           float spreadDegrees, float distance, float damage,
                                           ElementType element)
        {
            var go = new GameObject("suspended-discs");
            go.transform.position = owner.transform.position;

            var s = go.AddComponent<SuspendedDiscs>();
            s._owner = owner;
            s._player = player;
            s._damage = damage;
            s._element = element;
            s._life = s.LifeSeconds;

            // One sprite read for the whole set: every disc in the air is the same weapon.
            Sprite sprite = null; Color tint = Color.white; Vector2 size = Vector2.one;
            bool hasWeapon = rig != null && rig.TryGetWeaponVisual(out sprite, out tint, out size);
            // A split pair (the Armillary) alternates its halves round the set: disc i is the
            // main hand's half on even i, the off hand's on odd.
            Sprite offSprite = null;
            if (hasWeapon) rig.TryGetDiscVisual(1, out offSprite, out _, out _);

            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                // Spread evenly across the fan, centred on the aim. A single disc goes straight
                // ahead rather than to one edge, which the naive i/count would do.
                float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                float angle = baseAngle + (t - 0.5f) * spreadDegrees;
                var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));

                var d = new GameObject($"suspended-{i}");
                d.transform.SetParent(go.transform, false);
                d.transform.position = (Vector2)owner.transform.position + dir * distance;

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
                s._discs.Add(d.transform);
            }

            return s;
        }

        void Update()
        {
            _life -= Time.deltaTime;
            if (_life <= 0f) { Dissolve(); return; }

            // Held, not inert: a slow spin and a shallow bob say the discs are still live and
            // waiting. A perfectly still sprite reads as a dropped prop.
            float t = Time.time;
            for (int i = 0; i < _discs.Count; i++)
            {
                var d = _discs[i];
                if (d == null) continue;
                d.Rotate(0f, 0f, 220f * Time.deltaTime);
                var p = d.localPosition;
                p.y += Mathf.Sin(t * 3f + i * 0.9f) * 0.0016f;
                d.localPosition = p;
            }
        }

        /// <summary>Go off. Called when the owner's next finisher executes.</summary>
        public void Detonate()
        {
            var filter = new ContactFilter2D { useTriggers = true };
            var found = new List<Collider2D>();

            foreach (var d in _discs)
            {
                if (d == null) continue;
                Vector2 at = d.position;

                found.Clear();
                Physics2D.OverlapCircle(at, BlastRadius, filter, found);

                // Counted per disc, so overlapping blasts genuinely stack on a body caught by
                // two of them. That is the reward for placing the fan well.
                foreach (var col in found)
                {
                    if (col == null || col.gameObject == _owner) continue;
                    var hp = col.GetComponent<Health>();
                    if (hp == null || hp.IsDead || hp.gameObject == _owner) continue;

                    // Sublimate: placed by a finisher and detonated by the next one - always
                    // finisher-context, see DamageInfo.IsFinisher.
                    var info = new DamageInfo(_damage, _element, _owner) { IsFinisher = true };
                    hp.Take(info);

                    // Detonations are hits. Without these the move feeds no elemental meter and
                    // no exchange hook, which would make it the one finisher that opts out of
                    // the run's economy entirely.
                    _player?.Resource?.OnHitLanded(hp, info);
                    _player?.NotifyThrownHit(hp, info, 0.5f, false);
                }

                Spr.Flash(at, BlastRadius, ElementInfo.Tint(_element), 0.32f);
            }

            Dissolve();
        }

        void Dissolve()
        {
            foreach (var d in _discs)
                if (d != null) Destroy(d.gameObject);
            _discs.Clear();
            Destroy(gameObject);
        }
    }
}
