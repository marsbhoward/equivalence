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
    /// finisher do I want this one to go off underneath" - Sublimate into Mark lands the
    /// detonation's hits first and Mark's on bodies already struck; Mark into Sublimate spends
    /// the paint with the blast.
    ///
    /// Reworked 2026-10-16 (the user's calls): the discs FLY from the hands and slow to a stop
    /// (they used to appear where they hang), each over its OWN enemy - the lock and the nearest
    /// others in reach, shared out in turn, Ripple's rule - and FOLLOW it at walking pace, so the
    /// blast lands where the enemy is when the next art fires, not where it stood two basics ago.
    /// Nothing in reach: a fan in front, as before. Copies, not the pair - the hands keep their
    /// discs for the basics in between. Vapour rises off them while they wait (sublimation).
    /// The blast is a thrown hit (the board's and the ledger's rules, the crit rolled at the
    /// throw), weaker toward its edge like every area hit, with ONE hit-stop for the detonation.
    /// </summary>
    public class SuspendedDiscs : MonoBehaviour
    {
        /// <summary>Discs fade out on their own if nothing ever detonates them.</summary>
        public float LifeSeconds = 12f;

        class Hung
        {
            public Transform T;
            public SpriteRenderer Sr;
            public Health Target;
            public Vector2 From, Ground, Offset;
            public float Flight;
            public float Puff;
        }

        // Non-readonly: a domain reload brings a readonly collection back fresh.
        List<Hung> _discs = new();
        float _damage;
        bool _crit;
        ElementType _element;
        GameObject _owner;
        Player.PlayerController _player;
        float _life;

        public int Count => _discs.Count;

        public static SuspendedDiscs Place(GameObject owner, Player.PlayerController player,
                                           Art.Gear.ICharacterRig rig, Vector2 aim,
                                           List<Health> targets, int count, float spreadDegrees,
                                           float distance, float damage, bool crit,
                                           ElementType element, Vector2 mainHand, Vector2 offHand)
        {
            var go = new GameObject("suspended-discs");
            go.transform.position = owner.transform.position;

            var s = go.AddComponent<SuspendedDiscs>();
            s._owner = owner;
            s._player = player;
            s._damage = damage;
            s._crit = crit;
            s._element = element;
            s._life = s.LifeSeconds;

            // One sprite read for the whole set: every disc in the air is the same weapon. A
            // split pair alternates its halves round the set, the hand each one left.
            Sprite sprite = null; Color tint = Color.white; Vector2 size = Vector2.one;
            bool hasWeapon = rig != null && rig.TryGetWeaponVisual(out sprite, out tint, out size);
            Sprite offSprite = null;
            if (hasWeapon) rig.TryGetDiscVisual(1, out offSprite, out _, out _);

            float baseAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            Vector2 at = owner.transform.position;
            for (int i = 0; i < count; i++)
            {
                var h = new Hung { Flight = 0f };
                // Its enemy, shared out in turn round the fan - one enemy takes every disc.
                if (targets != null && targets.Count > 0) h.Target = targets[i * targets.Count / count];

                // Where over it: several on one enemy stand round it, not on top of each other.
                int sharing = 0, index = 0;
                for (int j = 0; j < count; j++)
                {
                    var tj = targets != null && targets.Count > 0 ? targets[j * targets.Count / count] : null;
                    if (tj != h.Target) continue;
                    if (j < i) index++;
                    sharing++;
                }
                if (h.Target != null && sharing > 1)
                {
                    float a = (90f + 360f * index / sharing) * Mathf.Deg2Rad;
                    h.Offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Tuning.Sublimate.Cluster;
                }

                if (h.Target != null) h.Ground = (Vector2)h.Target.transform.position + h.Offset;
                else
                {
                    float t = count <= 1 ? 0.5f : i / (float)(count - 1);
                    float angle = (baseAngle + (t - 0.5f) * spreadDegrees) * Mathf.Deg2Rad;
                    h.Ground = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                }
                h.From = i % 2 == 1 ? offHand : mainHand;

                var d = new GameObject($"suspended-{i}");
                d.transform.SetParent(go.transform, false);
                d.transform.position = h.From;
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
                DiscPictures.ShowCombined(sr, player);   // all out at once: the combined picture
                sr.sortingOrder = SortingOrders.Fx - 1;
                DiscVisual.Centre(sr);
                LightCycleTrail.Follow(rig, d.transform, sr, SortingOrders.Fx - 2);
                LightningArcs.Follow(rig, d.transform, sr, SortingOrders.Fx - 2);
                Art.Gear.HeldGlow.Follow(rig, d.transform, sr);
                h.T = d.transform;
                h.Sr = sr;
                h.Puff = i * 0.05f;
                s._discs.Add(h);
            }

            return s;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _life -= dt;
            if (_life <= 0f) { Dissolve(); return; }

            float t = Time.time;
            for (int i = 0; i < _discs.Count; i++)
            {
                var h = _discs[i];
                if (h.T == null) continue;

                // Following its enemy at walking pace; a dead one leaves it where it was.
                if (h.Target != null && !h.Target.IsDead)
                    h.Ground = Vector2.MoveTowards(h.Ground, (Vector2)h.Target.transform.position + h.Offset,
                                                   Tuning.Sublimate.FollowSpeed * dt);

                // Held, not inert: a slow spin and a shallow bob say the discs are still live.
                Vector2 hang = h.Ground + Vector2.up * (Tuning.Sublimate.HangHeight
                                                        + Mathf.Sin(t * 3f + i * 0.9f) * 0.025f);
                if (h.Flight < 1f)
                {
                    // Thrown out and slowing to a stop: eased out, so it arrives gently.
                    h.Flight = Mathf.Min(1f, h.Flight + dt / Tuning.Sublimate.FlightSeconds);
                    float u = 1f - (1f - h.Flight) * (1f - h.Flight) * (1f - h.Flight);
                    h.T.position = Vector2.Lerp(h.From, hang, u);
                    h.T.Rotate(0f, 0f, Mathf.Lerp(1080f, 220f, u) * dt);
                    continue;
                }
                h.T.position = hang;
                h.T.Rotate(0f, 0f, 220f * dt);

                // Vapour coming off it - sublimation, solid straight to air.
                h.Puff -= dt;
                if (h.Puff <= 0f)
                {
                    h.Puff = Tuning.Sublimate.WispEvery;
                    Wisp.Rise(hang + new Vector2(Random.Range(-0.12f, 0.12f), 0.05f), _element);
                }
            }
        }

        /// <summary>Go off. Called when the owner's next finisher executes.</summary>
        public void Detonate()
        {
            var filter = new ContactFilter2D { useTriggers = true };
            var found = new List<Collider2D>();
            float radius = Tuning.Sublimate.BlastRadius * (_player != null ? _player.AreaScaleNow : 1f);
            bool stopped = false;   // one hit-stop for the whole detonation

            foreach (var h in _discs)
            {
                if (h.T == null) continue;
                // The blast is on the GROUND under the disc, where its enemy stands - centred on
                // the enemy itself when discs share one (the spread round it is only so they are
                // not drawn on top of each other).
                Vector2 at = (h.Flight >= 1f ? h.Ground : (Vector2)h.T.position) - h.Offset;

                found.Clear();
                Physics2D.OverlapCircle(at, radius, filter, found);
                var struck = new HashSet<Health>();

                // Counted per disc, so overlapping blasts stack on a body caught by two of them -
                // three over one enemy is the whole art on it.
                foreach (var col in found)
                {
                    if (col == null || col.gameObject == _owner) continue;
                    var hp = col.GetComponentInParent<Health>();
                    if (hp == null || hp.IsDead || hp.gameObject == _owner || !struck.Add(hp)) continue;

                    float d = Vector2.Distance(at, hp.transform.position);
                    float edge = Mathf.Lerp(1f, Tuning.Sublimate.EdgeDamageFraction, Mathf.Clamp01(d / radius));
                    float dmg = _damage * edge;
                    if (_player != null) dmg = _player.ScaleThrownHit(hp, dmg, true, _crit, 0.5f, first: !stopped);

                    // Sublimate: placed by a finisher and detonated by the next one - always
                    // finisher-context, see DamageInfo.IsFinisher.
                    var info = new DamageInfo(dmg, _element, _owner)
                        { IsFinisher = true, Thrown = true, Crit = _crit, SuppressHitstop = stopped };
                    hp.Take(info);
                    stopped = true;

                    // Detonations are hits: they feed the element's meter and the run's hooks.
                    _player?.Resource?.OnHitLanded(hp, info);
                    _player?.NotifyThrownHit(hp, info, 0.5f, false);
                }

                Spr.Flash(at, radius, ElementInfo.Tint(_element), 0.32f);
            }

            Dissolve();
        }

        void Dissolve()
        {
            foreach (var h in _discs)
                if (h.T != null) Destroy(h.T.gameObject);
            _discs.Clear();
            Destroy(gameObject);
        }
    }
}
