using UnityEngine;
using Convergence.Core;
using Convergence.Enemies;

namespace Convergence.Combat
{
    /// <summary>
    /// Secondary damage a gear stat spreads from a hit that already landed - Splash around the
    /// target, Pierce through the line behind it.
    ///
    /// Both are deliberately DUMB hits: plain damage, no knockback, no on-hit hooks, no crit roll
    /// of their own. They ride a hit that already paid for all of that; if they re-ran the hooks,
    /// one swing would build meter, stack marks and roll the ledger's procs once per body the
    /// spill touched, and the stat would be worth several times its number.
    /// </summary>
    public static class CrowdHits
    {
        /// <summary>
        /// <paramref name="fraction"/> of <paramref name="hitDamage"/> to every other living enemy
        /// within Tuning.Attack.SplashRadius of <paramref name="target"/>.
        /// </summary>
        public static void Splash(Health target, float hitDamage, float fraction,
                                  ElementType element, GameObject owner)
            => Splash(target, hitDamage, fraction, element, owner, Tuning.Attack.SplashRadius);

        /// <summary>The same spill at a radius of its own - the mastery board's Precipitation.</summary>
        public static void Splash(Health target, float hitDamage, float fraction,
                                  ElementType element, GameObject owner, float radius)
        {
            if (target == null || fraction <= 0f || hitDamage <= 0f) return;

            Vector2 at = target.transform.position;
            float dmg = hitDamage * fraction;

            EnemyRegistry.Prune();
            foreach (var e in EnemyRegistry.All)
            {
                if (e == null || e.gameObject == owner) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp == target || hp.IsDead) continue;
                if (((Vector2)e.transform.position - at).sqrMagnitude > radius * radius) continue;
                hp.Take(new DamageInfo(dmg, element, owner));
            }

            Spr.Flash(at, radius, ElementInfo.Tint(element) * new Color(1f, 1f, 1f, 0.55f), 0.16f, true);
        }

        /// <summary>
        /// <paramref name="fraction"/> of <paramref name="hitDamage"/> to every living enemy in a
        /// strip Tuning.Attack.PierceLength long running on from <paramref name="target"/> along
        /// <paramref name="direction"/> - the arrow carrying on through.
        /// </summary>
        public static void Pierce(Health target, Vector2 direction, float hitDamage, float fraction,
                                  ElementType element, GameObject owner)
        {
            if (target == null || fraction <= 0f || hitDamage <= 0f) return;
            if (direction.sqrMagnitude < 0.0001f) return;

            var dir = direction.normalized;
            Vector2 from = target.transform.position;
            float length = Tuning.Attack.PierceLength, half = Tuning.Attack.PierceHalfWidth;
            float dmg = hitDamage * fraction;

            EnemyRegistry.Prune();
            foreach (var e in EnemyRegistry.All)
            {
                if (e == null || e.gameObject == owner) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp == target || hp.IsDead) continue;

                var delta = (Vector2)e.transform.position - from;
                float along = Vector2.Dot(delta, dir);
                if (along <= 0f || along > length) continue;
                if ((delta - dir * along).sqrMagnitude > half * half) continue;

                hp.Take(new DamageInfo(dmg, element, owner) { Thrown = true });
                Spr.Flash(e.transform.position, 0.3f, ElementInfo.Tint(element), 0.12f, false);
            }
        }
    }
}
