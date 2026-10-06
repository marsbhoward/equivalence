using System;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    public class Health : MonoBehaviour
    {
        public float Max = 100f;
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        /// <summary>
        /// Hits never shove this body.
        ///
        /// Set on enemies. Being knocked back on every connecting hit made a crowd slide around
        /// under the player's swings, which reads as the enemies being weightless and makes a
        /// chain of hits land on a target that is no longer where the arc was aimed. Standing
        /// their ground is what lets the swing itself carry the impact.
        ///
        /// Overridden per effect by <see cref="DamageInfo.Displaces"/>, for the handful of
        /// abilities whose entire purpose is to move the target.
        /// </summary>
        public bool Immovable;

        /// <summary>
        /// Never repositioned by damage, not even by a <see cref="DamageInfo.Displaces"/> hit.
        /// See the note at the knockback site for why this is separate from
        /// <see cref="Immovable"/> rather than a stronger reading of it.
        /// </summary>
        public bool Anchored;

        /// <summary>
        /// Resistance to being shoved, 0 = none, 1 = immovable. Applies only to bodies that can
        /// be shoved at all, which now means the PLAYER - enemies are <see cref="Immovable"/>.
        /// </summary>
        public float Poise;
        /// <summary>Flat multiplier applied to incoming damage. Water's debuff raises it above 1.</summary>
        public float Vulnerability = 1f;

        /// <summary>
        /// Nothing lands at all: no damage, no knockback, no flash, no Damaged event.
        ///
        /// Deliberately a separate flag rather than Vulnerability = 0. A zero multiplier still
        /// runs the whole hit - the figure flashes white, the impulse shoves it, and every
        /// Damaged listener fires (durability wear, elemental reactions, the HUD) - so the frame
        /// reads as "hit for nothing" rather than "not hit". Being airborne means the attack
        /// misses, and a miss has no side effects.
        /// </summary>
        public bool Immune;

        /// <summary>
        /// Consulted right after Immune, for a block that depends on THIS hit rather than being a
        /// blanket flag - Barrier's front-facing-only block, which needs the attacker's position to
        /// answer. Returning true is treated identically to Immune: nothing lands at all.
        /// </summary>
        public Func<DamageInfo, bool> BlocksHit;

        /// <summary>
        /// Hooks the run's boons and costs plug into, left as plain delegates so Health knows
        /// nothing about the exchange system - the same shape PlayerController already uses for
        /// DamageDealtMultiplier. Null means "no run effects", which is the normal state for
        /// every enemy in the game.
        /// </summary>
        public Func<float, float> ModifyIncoming;

        /// <summary>
        /// A multiplier on an incoming hit that depends on WHO struck it - the mastery board's
        /// Solution (a soaked enemy's blows land lighter). Applied with Vulnerability, before
        /// ModifyIncoming; null is 1. Kept apart from ModifyIncoming because that delegate is
        /// handed only the amount, and composing a second meaning into it would mean changing
        /// every system already chained onto it.
        /// </summary>
        public Func<DamageInfo, float> ScaleIncoming;

        public Func<float, float> ModifyHeal;

        /// <summary>
        /// Asked only when a hit WOULD be lethal. True leaves the body at 1 instead of dead.
        /// Consulted before Current is written, so nothing downstream ever observes the death
        /// that did not happen.
        /// </summary>
        public Func<bool> SurviveLethal;

        public event Action<DamageInfo> Damaged;
        public event Action<Health> Died;

        /// <summary>
        /// The last hit this body took, whatever it was. Set the instant a hit lands, so a
        /// <see cref="Died"/> listener can ask what killed it - the event itself only carries the
        /// Health. Default-valued until the first hit; a body that dies to <see cref="Kill"/>
        /// leaves it at whatever last hurt it (or default if nothing ever did).
        /// </summary>
        public DamageInfo LastDamage { get; private set; }

        /// <summary>
        /// When a hit last actually MOVED this body (the impulse applied - not merely carried
        /// knockback that Immovable or Anchored refused). A chasm reads it: an enemy over the
        /// void falls only if something sent it there, never because its own pack leaned it over
        /// the lip (EnemyController.TickChasm).
        /// </summary>
        public float ShovedAt { get; private set; } = float.NegativeInfinity;

        /// <summary>Records that something other than a hit moved this body just now (a pull),
        /// so a chasm treats it the same as a shove.</summary>
        public void MarkShoved() => ShovedAt = Time.time;

        /// <summary>
        /// Forces this body to zero immediately, bypassing the whole damage pipeline -
        /// Vulnerability, Immune, ModifyIncoming, SurviveLethal, none of it is consulted. For a
        /// self-inflicted death (a bomb's own timer running out) that should never be preventable
        /// by the target's own defenses, unlike a hit landing from something else.
        /// </summary>
        public void Kill()
        {
            if (IsDead) return;
            Current = 0f;
            Died?.Invoke(this);
        }

        SpriteRenderer _sr;
        Color _base;
        float _flash;
        Rigidbody2D _rb;

        /// <summary>
        /// Set max HP and refill. Must be used instead of assigning Max directly after
        /// AddComponent: Awake() has already run by then and would leave Current at the
        /// inspector default rather than the intended max.
        /// </summary>
        public void Configure(float max)
        {
            Max = max;
            Current = max;
        }

        /// <summary>
        /// Change the maximum MID-RUN without refilling, for boons and costs that move it.
        ///
        /// Deliberately not Configure, which refills - calling that once a floor would hand the
        /// player a free full heal every time the ledger changed. An increase is granted as
        /// actual health (Thickened Hide should feel like a gain, not a longer bar to refill);
        /// a decrease clamps Current down but never below 1, because a cost that kills you the
        /// instant you accept it reads as the game cheating rather than as a price.
        /// </summary>
        public void SetMax(float max)
        {
            if (IsDead) return;

            max = Mathf.Max(1f, max);
            float delta = max - Max;
            Max = max;
            if (delta > 0f) Current += delta;
            Current = Mathf.Clamp(Current, 1f, Max);
        }

        void Awake()
        {
            if (Current <= 0f) Current = Max;
            _rb = GetComponent<Rigidbody2D>();
        }

        /// <summary>
        /// Resolved lazily rather than in Awake: the visual is attached as a child by ArtBinder,
        /// which may run after this component is added.
        /// </summary>
        SpriteRenderer Renderer
        {
            get
            {
                if (_sr == null)
                {
                    _sr = GetComponentInChildren<SpriteRenderer>();
                    if (_sr) _base = _sr.color;
                }
                return _sr;
            }
        }

        /// <summary>
        /// A humanoid rig flashes itself; anything else falls back to tinting its one sprite.
        ///
        /// The fallback is what enemies use - they are a single Spr.Circle attached by ArtBinder,
        /// where a colour lerp works fine. It is only the multi-layer rig with baked-colour art
        /// that needs to own its own flash.
        /// </summary>
        Art.Gear.ICharacterRig _rig;
        bool _rigResolved;

        Art.Gear.ICharacterRig Rig
        {
            get
            {
                if (!_rigResolved)
                {
                    _rigResolved = true;
                    _rig = GetComponentInChildren<Art.Gear.ICharacterRig>();
                }
                return _rig;
            }
        }

        void Update()
        {
            if (_flash > 0f)
            {
                _flash -= Time.deltaTime * 5f;
                float t = Mathf.Clamp01(_flash);

                var rig = Rig;
                if (rig != null) { rig.SetFlash(t); return; }

                var sr = Renderer;
                if (sr) sr.color = Color.Lerp(_base, Color.white, t);
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;
            if (ModifyHeal != null) amount = ModifyHeal(amount);
            if (amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
        }

        /// <summary>
        /// One-shot incoming-damage amplifier, consumed by the next hit to land. Set by Mark.
        /// NonSerialized so a domain reload cannot leave a stale amplifier attached to a body.
        /// </summary>
        [System.NonSerialized] public float MarkMultiplier = 1f;

        /// <summary>
        /// While above zero, no hit takes Current below it, and once Current reaches it nothing
        /// lands at all (treated as Immune, for the same "not hit" rather than "hit for nothing"
        /// reason). A boss's per-window damage cap - see Tuning.Boss.WindowCap. Zero means no floor.
        /// A plain float, so a domain reload leaves it intact.
        /// </summary>
        public float Floor;

        public void Take(DamageInfo info)
        {
            if (IsDead) return;
            if (info.Price) { Pay(info); return; }
            if (Immune) return;
            if (Floor > 0f && Current <= Floor) return;
            if (BlocksHit != null && BlocksHit(info)) return;

            float amount = info.Amount * Vulnerability;
            if (ScaleIncoming != null) amount *= Mathf.Max(0f, ScaleIncoming(info));

            // A MARK is spent by the hit that lands on it, not worn down over a duration. It
            // lives on Health rather than being looked up through StatusEffects because every
            // damage path in the game funnels through here, and a GetComponent per hit to read
            // one float is a cost paid on every swing to serve one finisher.
            if (MarkMultiplier > 1f)
            {
                amount *= MarkMultiplier;
                MarkMultiplier = 1f;
            }
            if (ModifyIncoming != null) amount = Mathf.Max(0f, ModifyIncoming(amount));

            // Second Wind is asked BEFORE the subtraction, not patched up afterwards: letting
            // Current go to zero and reviving it would fire Died and every listener behind it.
            if (amount >= Current && SurviveLethal != null && SurviveLethal())
                amount = Mathf.Max(0f, Current - 1f);

            if (Floor > 0f) amount = Mathf.Min(amount, Current - Floor);

            Current -= amount;
            _flash = 1f;

            // Immovable refuses ordinary knockback; Displaces overrides it, because a pull that
            // cannot pull is not a weaker pull, it is a broken finisher.
            //
            // Anchored is the tier ABOVE that, and Displaces does NOT override it - an elite is
            // moved by nothing. Deliberately a separate flag rather than "Immovable that really
            // means it": the two answer different questions. Immovable is the default posture for
            // an ordinary enemy, which a Heavy finisher is supposed to be able to overrule.
            // Anchored says this particular body is never repositioned by damage at all, which is
            // what makes an elite the thing a fight gets fought around rather than swept up in
            // Undertow with everything else. It does not touch flinch: an elite is still
            // interrupted, it just does not travel.
            if (_rb && info.Knockback.sqrMagnitude > 0.001f && !Anchored && (!Immovable || info.Displaces))
            {
                _rb.AddForce(info.Knockback * (1f - Mathf.Clamp01(Poise)), ForceMode2D.Impulse);
                ShovedAt = Time.time;
            }

            LastDamage = info;
            Damaged?.Invoke(info);

            if (Current <= 0f)
            {
                Current = 0f;
                Died?.Invoke(this);
            }
        }

        /// <summary>A price, not a hit - see DamageInfo.Price. Taken straight off; Second Wind is
        /// the one thing still asked, so a price can be caught but never softened.</summary>
        void Pay(DamageInfo info)
        {
            float amount = Mathf.Max(0f, info.Amount);
            if (amount <= 0f) return;
            if (amount >= Current && SurviveLethal != null && SurviveLethal())
                amount = Mathf.Max(0f, Current - 1f);
            Current -= amount;
            _flash = 1f;
            LastDamage = info;
            Damaged?.Invoke(info);
            if (Current <= 0f)
            {
                Current = 0f;
                Died?.Invoke(this);
            }
        }
    }
}
