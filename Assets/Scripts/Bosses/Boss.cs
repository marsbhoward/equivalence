using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>Which boss a boss floor holds. Picked by Rifts.FloorPlanner.BossFor.</summary>
    public enum BossKind { Cantor, Medusa }

    /// <summary>
    /// What every boss shares, pulled out of the Cantor when the second one arrived. A boss is a
    /// fight driven from ONE coroutine (see Cantor's header on why), so what is shared is the
    /// frame around it rather than any of its moves:
    ///
    ///     the body        a kinematic, uninterpolated Rigidbody2D and a Health scaled by
    ///                     FloorDifficulty.BossHp, immovable and IMMUNE until a window opens
    ///     the window      OpenWindow / WindowSpent / CloseWindow - Tuning.Boss.WindowCap of max
    ///                     health per window and no more, so no build skips the cadence
    ///     the lifecycle   a token every coroutine checks, and Cease, which GameBootstrap calls
    ///                     BEFORE Destroy (deferred) and again at teardown
    ///
    /// GameBootstrap holds the floor on <c>_boss != null</c> and reads only what is declared here,
    /// so a new boss is a subclass and a case in <see cref="Spawn"/>, nothing else.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public abstract class Boss : MonoBehaviour
    {
        public Health Health { get; protected set; }
        public bool Enraged { get; protected set; }

        /// <summary>For the HUD bar: "THE CANTOR", "MEDUSA".</summary>
        public abstract string DisplayName { get; }

        /// <summary>For the HUD bar: where the fight is, in a word.</summary>
        public abstract string PhaseLabel { get; }

        protected Transform Target;
        protected Collider2D BodyCollider;
        protected int Floor = 1;

        /// <summary>Bumped by Cease; every coroutine compares the value it started with and quits
        /// once it changes. A plain int, so a domain reload leaves it intact.</summary>
        protected int Token;

        public static Boss Spawn(BossKind kind, Vector2 at, Transform target, Transform parent, int floor)
            => kind switch
            {
                BossKind.Medusa => Medusa.Spawn(at, target, parent, floor),
                _ => Cantor.Spawn(at, target, parent, floor),
            };

        /// <summary>
        /// The body every boss stands on. Kinematic, and DELIBERATELY NOT INTERPOLATED: a boss
        /// that teleports would slide visibly across the room under interpolation, inventing a
        /// path it never took (see Cantor.Spawn). A boss that walks can switch it on itself.
        /// </summary>
        protected static T CreateBody<T>(string name, Vector2 at, Transform target, Transform parent,
                                          int floor, float colliderRadius, float baseHp) where T : Boss
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.None;

            var b = go.AddComponent<T>();
            b.Target = target;
            b.Floor = Mathf.Max(1, floor);

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = colliderRadius;
            col.enabled = false;            // shut until a window opens
            b.BodyCollider = col;

            var hp = go.AddComponent<Health>();
            hp.Configure(baseHp * Enemies.FloorDifficulty.BossHp(b.Floor));
            hp.Immovable = true;          // nothing displaces a boss, not even a Heavy finisher
            hp.Immune = true;             // untouchable until the first window
            DamageNumbers.Attach(hp);
            Hitstop.Attach(hp);
            CameraKick.Attach(hp);
            FinisherHits.Attach(hp);
            b.Health = hp;
            return b;
        }

        /// <summary>Stops the fight cleanly. Called on death and on teardown - a coroutine that
        /// outlives its run is a documented failure mode in this project, not a hypothetical.
        /// Overrides clear whatever the boss put in the room, then call this.</summary>
        public virtual void Cease()
        {
            Token++;
            StopAllCoroutines();
        }

        protected virtual void OnDestroy() => Cease();

        protected void CheckEnrage(float at)
        {
            if (Enraged || Health == null) return;
            if (Health.Current > Health.Max * at) return;
            Enraged = true;
        }

        // ------------------------------------------------------------------ the window

        /// <summary>
        /// Open the damage window. THE WINDOW CAP: this window may take Tuning.Boss.WindowCap of
        /// max health and no more, so no build skips the cadence. Zero once the remaining health
        /// fits inside one window - the last window is allowed to kill.
        /// </summary>
        protected void OpenWindow()
        {
            float floor = Health.Current - Health.Max * Tuning.Boss.WindowCap;
            Health.Floor = floor > 0f ? floor : 0f;
            Health.Immune = false;
            if (BodyCollider != null) BodyCollider.enabled = true;
        }

        /// <summary>The cap is reached: the window should CLOSE rather than stand open to be hit
        /// for nothing. A strong player's reward is leaving the danger sooner, not a skipped fight.</summary>
        protected bool WindowSpent => Health.Floor > 0f && Health.Current <= Health.Floor;

        /// <summary>
        /// Shut again. Immunity AND the collider, together: a boss that looks open and is not, or
        /// is open and looks shut, is the fight lying about the only question the player is
        /// asking - a swing while shut passes through empty space instead of connecting for nothing.
        /// </summary>
        protected void CloseWindow()
        {
            Health.Immune = true;
            Health.Floor = 0f;
            if (BodyCollider != null) BodyCollider.enabled = false;
        }

        // ------------------------------------------------------------------ pieces

        protected void TeleportTo(Vector2 to)
        {
            var at = Arena.Clamp(to, 0.8f);
            transform.position = at;

            // Rigidbody2D: the transform write does not reach the physics world until the next
            // step, so a swing resolved in the same frame would test where the boss USED to be.
            Physics2D.SyncTransforms();
        }

        protected static SpriteRenderer Quad(Transform parent, string name, float size, Color c,
                                             int order, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
