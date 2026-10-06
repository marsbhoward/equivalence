using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>Builds enemies from code - no prefab assets, so the scene stays CLI-rebuildable.</summary>
    public static class EnemyFactory
    {
        /// <param name="elite">
        /// Promote this spawn to the ELITE TIER. Layers on top of <paramref name="kind"/> rather
        /// than replacing it, so any archetype can be elite. The stat half of the tier is folded
        /// into the ordinary fields here; the behaviour half (displacement resistance, the extra
        /// attack pattern) is carried by EnemyController.Elite.
        /// </param>
        public static EnemyController Spawn(Vector2 pos, EnemyKind kind, int wave, Transform target, Transform parent,
                                            bool elite = false)
        {
            // ONE LOOKUP, NOT NINE SWITCHES. Every per-kind value lives on the type record -
            // see EnemyTypes - so an enemy can be read as a whole and adding a property means
            // adding a field there rather than another switch here.
            var def = EnemyTypes.Of(kind);

            var go = new GameObject(def.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            // Root stays at scale 1 so world sizes stay honest; the visual child carries the size.
            float size = def.Size * (elite ? Tuning.Enemy.EliteSizeMul : 1f);

            var art = def.Art();
            var visual = Art.ArtBinder.AttachVisual(go, art, Spr.Circle, def.Tint, size, 5);

            // ArtBinder's Sprite/Prefab paths size to art.WorldHeight (or the prefab's own
            // authored scale) - neither knows about the elite multiplier already folded into
            // `size` for the placeholder path just above. Without this an elite with real art
            // renders at the same size as a basic and, since the fallback elite ring below only
            // draws while there is NO real art, loses the one tell that told the two apart.
            if (elite && art.HasArt) visual.transform.localScale *= Tuning.Enemy.EliteSizeMul;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = size * 0.5f;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = Tuning.Enemy.BodyDamping;
            rb.freezeRotation = true;

            // Interpolated for the same reason as the player (see GameBootstrap.BuildPlayer), and
            // it is not merely cosmetic on an enemy: a telegraph is read off where the thing IS,
            // and a body stepping 20ms at a time is hardest to read exactly when it is closing.
            // The floor curve scales speed with depth, so the size of each jump grows with the
            // floors on which reading the approach matters most.
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // The floor curve, from the one place that owns it - see Enemies.FloorDifficulty for
            // what each axis is for and why speed is on the list at all.
            float maxHp = MaxHpFor(def, wave, elite);
            var hp = go.AddComponent<Health>();
            hp.Configure(maxHp);
            hp.Immovable = !Tuning.Enemy.TakesKnockback;
            DamageNumbers.Attach(hp);
            Hitstop.Attach(hp);
            CameraKick.Attach(hp);
            FinisherHits.Attach(hp);

            // ELITES DO NOT TRAVEL, and this is the only thing that makes that true. It
            // deliberately does NOT touch flinch: denying an elite's attack works exactly as it
            // does on anything else, and only the shove is refused.
            hp.Anchored = elite;

            // ---- per-kind physicality ----
            //
            // Decided HERE rather than baked into Health or the controller, because the answers
            // differ by what the enemy is. Basics, elites and ranged are walk-through and cannot
            // shove the player; a BOSS will want both reversed, and that is a change to these two
            // lines rather than to the combat code underneath them.
            bool solid = Tuning.Enemy.BlocksPlayer;

            // Walk-through is done per collider pair rather than with layers because the project
            // sets none up, and this is the only pairing that needs it - enemy-to-enemy collision
            // stays on so a pack spreads instead of stacking into one point.
            if (!solid && target != null)
            {
                var playerCol = target.GetComponent<Collider2D>();
                if (playerCol != null) Physics2D.IgnoreCollision(col, playerCol);
            }

            var ec = go.AddComponent<EnemyController>();
            ec.Kind = kind;
            ec.Elite = elite;
            // SPEED SCALES WITH DEPTH NOW. It never did, and that was the whole reason a
            // seasoned player felt no tension anywhere from floor 1 to floor 70: nothing could
            // ever catch them, so the only thing depth bought was a longer fight.
            ec.MoveSpeed = (def.MoveSpeed + Random.Range(def.MoveSpeedJitterMin, def.MoveSpeedJitterMax))
                         * FloorDifficulty.Speed(wave);
            ec.Damage = def.Damage * FloorDifficulty.Damage(wave) * (elite ? Tuning.Enemy.EliteDamageMul : 1f);
            ec.AttackRange = def.AttackRange;
            ec.PreferredMinRange = def.PreferredMinRange;

            // Deep telegraphs are shorter and deep attacks come faster - the two axes that turn a
            // familiar enemy into one that demands a quicker read rather than a longer grind.
            ec.TelegraphDuration = Mathf.Max(Tuning.Enemy.TelegraphMinSeconds,
                                             def.TelegraphDuration * FloorDifficulty.Telegraph(wave));
            // Mortar lobs on its own, slower clock - the only kind whose cadence differs from the
            // shared one AND runs through AttackInterval, so it still speeds up with depth.
            if (kind == EnemyKind.Mortar) ec.AttackInterval = Tuning.Enemy.MortarAttackInterval;
            ec.AttackInterval *= FloorDifficulty.AttackInterval(wave);

            // The tier's extra move, declared on the type rather than branched on here - an elite
            // Ranged and an elite Bomb are each their own kit turned up, not one shared bonus.
            ec.ElitePattern = elite ? def.Elite : ElitePattern.None;
            ec.ElitePatternEvery = Mathf.Max(1, def.EliteEvery);

            ec.SetTarget(target);

            // The body agrees with its own telegraph/recover beat rather than leaving the ground
            // ring or charge ring to carry the whole read alone - see EnemyStageCycle. Each kind
            // hands in the progress signal that ACTUALLY drives its own attack: Bomb never touches
            // _turretCharge and Turret never touches _telegraphTimer, so this is two different
            // delegates, not one shared getter. Harmless no-op for a future Prefab-based look,
            // which would animate itself instead of being driven here.
            if (kind == EnemyKind.Bomb)
            {
                // BombArt bakes its own yellow, so the fallback Tint must not multiply over it
                // (pixel art is drawn at white - see PixelSprite). Only once the stage art is on:
                // a missing stage cycle leaves the tinted placeholder circle, which needs it.
                if (EnemyStageCycle.Attach(visual, BombArt.For(EnemyLooks.Of(kind)), size, ec, kind) != null)
                    visual.GetComponent<SpriteRenderer>().color = Color.white;
            }
            else if (kind == EnemyKind.Turret)
                TurretArt.Attach(go, visual, ec, kind, EnemyLooks.Of(kind), size);
            else if (kind == EnemyKind.Chaser)
                EnemyStageCycle.Attach(visual, ChaserArt.For(EnemyLooks.Of(kind)), size, ec, kind);
            else if (kind == EnemyKind.Ranged)
                EnemyStageCycle.Attach(visual, RangedArt.For(EnemyLooks.Of(kind)), size, ec, kind);
            else if (kind == EnemyKind.Dasher)
                EnemyStageCycle.Attach(visual, DasherArt.For(EnemyLooks.Of(kind)), size, ec, kind);

            // Placeholder elite marker. Once elite art exists it carries its own read, so the
            // ring is only drawn while the elite is still using the fallback shape.
            //
            // Gated on ELITE, not on Chaser. It used to check `kind == EnemyKind.Chaser`, which
            // was correct back when Elite WAS a kind and Chaser was its name - so after Elite
            // became a tier the ring drew on every common chaser and on no other elite at all,
            // marking exactly the wrong bodies.
            if (elite && !art.HasArt)
            {
                var ring = new GameObject("elite-ring");
                ring.transform.SetParent(go.transform, false);
                ring.transform.localScale = Vector3.one * size * 1.5f;
                var rsr = ring.AddComponent<SpriteRenderer>();
                rsr.sprite = Spr.Ring;
                rsr.color = new Color(1f, 0.8f, 0.3f, 0.85f);
                rsr.sortingOrder = SortingOrders.Enemy;
            }

            // ---- armor: a shield-style absorb pool in front of HP ----
            //
            // BASIC (the default state of any kind - see EnemyKind's own note) starts with
            // nothing and gains one bar, worth half its own max HP, every ArmorGrowthFloors
            // floors. ELITE starts with a full extra HP's worth from floor 1 and grows at the
            // same per-floor rate on top of that. BaseArmorFor is the seam for a future enemy
            // TYPE to carry armor by default (additive with the tier's own amount) - nothing
            // does yet, so it is 0 for everything today.
            float barSize = maxHp * Tuning.Enemy.ArmorBarFraction;
            float armorMax = ArmorFor(def, maxHp, wave, elite);

            if (armorMax > 0f)
            {
                var armor = EnemyArmor.Attach(go, armorMax, barSize);
                ArmorRing.Attach(go, armor, size * 0.5f + 0.12f);
            }

            // Sorted by where it stands, so the player can pass in front of and behind a crowd.
            // Every renderer this enemy owns, including the elite ring and any armor ticks,
            // keeps its relative order.
            var renderers = go.GetComponentsInChildren<SpriteRenderer>(true);
            DepthSorted.Attach(go, 0f, false, renderers);

            return ec;
        }

        /// <summary>
        /// An enemy TYPE's own armor by default, additive with whatever its tier (Basic/Elite)
        /// grants. Nothing carries any yet - this is the seam for the day one does, so that
        /// addition is one line here rather than a change to how armor is computed.
        /// </summary>
        static float BaseArmorFor(EnemyKind kind, float maxHp) => 0f;

        /// <summary>A spawn's max health. Public so WaveComposer prices an enemy with the same
        /// number this factory gives it - a copy of the formula would drift the first time
        /// either side was tuned.</summary>
        public static float MaxHpFor(EnemyDef def, int wave, bool elite)
            => def.BaseHp * FloorDifficulty.Hp(wave) * (elite ? Tuning.Enemy.EliteHpMul : 1f);

        /// <summary>
        /// A spawn's armor pool, sized off its own <paramref name="maxHp"/>. Public for the same
        /// reason as <see cref="MaxHpFor"/>.
        ///
        /// NeverArmored kinds skip this entirely, including the generic per-floor growth bar - not
        /// just the elite tier's own bonus. Gargoyle is the first kind that needs this: an armor
        /// bar arriving silently at depth would also silently take away the one counterplay that
        /// family has, since EnemyController.Flinch refuses a Medium interrupt while any armor
        /// remains.
        /// </summary>
        public static float ArmorFor(EnemyDef def, float maxHp, int wave, bool elite)
        {
            if (def.NeverArmored) return 0f;
            int growthBars = wave / Tuning.Enemy.ArmorGrowthFloors;
            float tierArmor = (elite ? maxHp * Tuning.Enemy.EliteArmorFraction : 0f)
                             + growthBars * maxHp * Tuning.Enemy.ArmorBarFraction;
            return BaseArmorFor(def.Kind, maxHp) + tierArmor;
        }
    }
}
