using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Enemies
{
    /// <summary>
    /// Behaviour + stat archetype. EnemyFactory picks stats, size and art by this; EnemyController
    /// branches its own movement and attack logic on it. Elite shares the plain chase-and-swing
    /// behaviour Bomb used to use before its own explosion path split off - Elite differs from
    /// that shared path only in stats/size, it is not a separate code path. Bomb and Ranged each
    /// have their own dedicated update logic below, and so does Turret - the stationary one - and
    /// Dasher - the hit-and-run striker.
    /// </summary>
    public enum EnemyKind { Bomb, Chaser, Ranged, Turret, Dasher, Gargoyle, Booster, Bubbles, Mortar }

    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public partial class EnemyController : MonoBehaviour
    {
        // Central values: Tuning.Enemy. EnemyFactory overwrites MoveSpeed / AttackRange / Damage
        // per spawn (bomb vs elite vs ranged, wave scaling); AttackInterval is used as-is.
        public float MoveSpeed = Tuning.Enemy.BombMoveSpeed;
        public float AttackRange = Tuning.Enemy.BombAttackRange;
        public float AttackInterval = Tuning.Enemy.AttackInterval;
        public float Damage = Tuning.Enemy.BombDamage;
        public EnemyKind Kind = EnemyKind.Bomb;

        /// <summary>
        /// Whether this spawn is an ELITE - a tier layered on top of <see cref="Kind"/>, not a
        /// kind of its own. Any archetype can be elite.
        ///
        /// EnemyFactory has already folded the stat side of the tier (HP, damage, size, armour)
        /// into the plain fields by the time this is set, so nothing downstream needs to multiply
        /// anything. What this flag is read for is the BEHAVIOUR the tier grants - displacement
        /// resistance, and the extra attack pattern.
        /// </summary>
        public bool Elite;

        /// <summary>The look this body was drawn in (EnemyLooks, rolled per floor), kept on the
        /// body so effects it makes later - the Tempest Eye's lightning and static mire - match
        /// its own art rather than whatever the floor's look is by then.</summary>
        public EnemyLook Look;

        /// <summary>
        /// Which of its floor's bodies this is - the wave's queue order, set as it spawns. Its drops
        /// are rolled from it (Rifts.RunRolls), so a floor replayed after a resume pays the same
        /// drops off the same enemies whatever order they die in. -1 for a body nothing numbered.
        /// </summary>
        public int RollIndex = -1;

        /// <summary>The tier's extra move for this KIND, set from the type record at spawn - see
        /// EnemyTypes. None on anything that is not elite.</summary>
        public ElitePattern ElitePattern = ElitePattern.None;

        /// <summary>How often the pattern replaces the ordinary attack.</summary>
        public int ElitePatternEvery = 2;

        /// <summary>Attacks made, so the pattern can land on a cadence rather than a coin flip -
        /// an elite whose big move is random is one the player cannot read.</summary>
        int _attackCount;

        /// <summary>True when THIS attack should be the elite one. Asked once per attack, and it
        /// counts here so every kind gets the same cadence for free.</summary>
        /// <summary>Whether the NEXT attack will be the elite pattern - the same count
        /// <see cref="ElitePatternDue"/> advances, read without advancing it, so art can show
        /// what is coming (the Cinder Stinger's three lit lobes before a volley).</summary>
        public bool NextShotIsElitePattern =>
            ElitePattern != ElitePattern.None && (_attackCount + 1) % Mathf.Max(1, ElitePatternEvery) == 0;

        bool ElitePatternDue()
        {
            if (ElitePattern == ElitePattern.None) return false;
            return ++_attackCount % Mathf.Max(1, ElitePatternEvery) == 0;
        }


        // ---- Ranged-only tuning, set by EnemyFactory. Unused by Elite. ----
        public float PreferredMinRange = Tuning.Enemy.RangedPreferredMinRange;
        public float ProjectileSpeed = Tuning.Enemy.RangedProjectileSpeed;
        public float ProjectileLifetime = Tuning.Enemy.RangedProjectileLifetime;

        /// <summary>
        /// Seconds a telegraph holds before it resolves. Shared by Ranged (its aim, frozen for
        /// the bolt) and Bomb (its blast circle) rather than each owning a separate field, since
        /// both are "freeze, show a warning, then commit" on the same shape of timer - only the
        /// per-kind duration EnemyFactory assigns differs.
        /// </summary>
        public float TelegraphDuration = Tuning.Enemy.RangedTelegraphDuration;

        /// <summary>
        /// 0 when not telegraphing, climbing to 1 as the attack approaches - exposed so the
        /// body art (see EnemyStageCycle) can show the wind-up without reaching into a private
        /// timer.
        /// </summary>
        /// <summary>Seconds left on the current telegraph, 0 when there is none - the raw timer,
        /// for art that keys a pose to the moment the attack lands (ChaserVisual's cocked frame).</summary>
        public float TelegraphRemaining => Mathf.Max(0f, _telegraphTimer);

        /// <summary>Who this enemy is after.</summary>
        public Transform Target => _target;

        public float TelegraphProgress01 =>
            _telegraphTimer > 0f ? 1f - Mathf.Clamp01(_telegraphTimer / Mathf.Max(0.0001f, TelegraphDuration)) : 0f;

        /// <summary>
        /// 0..1 through a Turret's charge-up - a SEPARATE signal from TelegraphProgress01, because
        /// Turret never touches _telegraphTimer at all: it counts _turretCharge against
        /// Tuning.Enemy.TurretChargeDuration directly (reading the same constant rather than the scaled
        /// TelegraphDuration field - matching what actually gates the beam is more important here
        /// than matching what EnemyFactory happened to also assign).
        /// </summary>
        public float TurretChargeProgress01 =>
            Mathf.Clamp01(_turretCharge / Mathf.Max(0.0001f, Tuning.Enemy.TurretChargeDuration));

        Transform _target;
        Health _targetHealth;
        Player.PlayerController _targetController;
        Rigidbody2D _rb;
        Health _health;
        StatusEffects _status;

        /// <summary>How fast this body's attack cooldowns run right now - slower while a stagger
        /// carries the mastery board's Antimony (StatusEffects.AttackTempo), faster on an elite a
        /// Booster has roared to power (_boostTempo). Antimony never slows a telegraph.</summary>
        float AttackTempo => (_status ? _status.AttackTempo : 1f) * (_boostTempo > 0f ? _boostTempo : 1f);
        float _cooldown;

        // ---- ranged telegraph state ----
        float _telegraphTimer;
        Vector2 _aimDir;
        GameObject _telegraphVisual;
        SpriteRenderer _telegraphSr;

        /// <summary>
        /// How strongly every enemy's attack telegraph is drawn - 1, or less under the run's Murk
        /// (Fog's Nigredo). A static the run sets and resets, so a telegraph knows nothing about
        /// the exchange.
        /// </summary>
        public static float TelegraphStrength = 1f;

        static Color Murk(Color c)
        {
            c.a *= TelegraphStrength;
            return c;
        }
        int _strafeDir = 1;

        // ---- turret beam state ----
        //
        // Kept apart from the telegraph fields above on purpose: a telegraph is a one-shot
        // countdown that always resolves once armed, where the beam is a continuous state that
        // can switch on and off many times a second as line of sight comes and goes.
        GameObject _beamVisual;
        LightningArc _beamArc;
        float _beamTick;
        float _turretCharge;
        float _overchargeUntil;

        // The elite Turret's mire lob - see TickMire. The clock counts toward the next lob; the
        // wind-up, while above zero, is the tell a flinch denies. The wind-up glow is a CHILD, so
        // it goes with the body.
        float _mireClock, _mireWindup;
        bool _mireClockStarted;
        SpriteRenderer _mireWindupSr;

        // A piece of a Turret's art that ROTATES to aim, if a look has one. The Tempest Eye has
        // none - its pupil is the swivel (TurretVisual) - so nothing sets this today; kept for a
        // look that would. A Transform survives a domain reload like every Object reference here.
        Transform _turretHead;

        /// <summary>Hands in a piece of the art that rotates to face the target.</summary>
        public void SetTurretHead(Transform head) => _turretHead = head;

        bool _turretFacesLeft;

        /// <summary>Which way the Tempest Eye faces - decided here, with a dead zone, so its
        /// drawing (TurretVisual) and its beam's origin at the eye always agree.</summary>
        public bool TurretFacesLeft => _turretFacesLeft;
        /// <summary>The beam is on, at this moment.</summary>
        public bool TurretBeamOn => _beamVisual != null;
        /// <summary>An elite's overcharge is running.</summary>
        public bool TurretOvercharged => Time.time < _overchargeUntil;
        /// <summary>An elite's mire lob is winding up.</summary>
        public bool TurretLobbing => _mireWindup > 0f;

        // ---- dasher state ----
        //
        // The Gale Hound's L (the user's reference clip, 2026-10-18): Approach -> LineUpTell ->
        // LineUp (a dash SIDEWAYS, to a point level with the player) -> Braced -> Telegraph (the
        // charge's tell; the end point is frozen as it starts) -> Rushing (straight THROUGH that
        // point and on past it) -> Spent (standing where it stopped) -> Evading -> Approach. Either
        // dash bites whatever it passes through, once. An elite's Redouble turns at the end of the
        // charge and tells again instead of standing spent.
        //
        // Its own timers, not the shared _telegraphTimer: nothing draws a ground shape for it, and
        // two tells and two dashes are not that machinery's shape. PUBLIC read-outs for DasherVisual.
        public enum DasherPhase { Approach, LineUpTell, LineUp, Braced, Telegraph, Rushing, Spent, Evading }
        public DasherPhase CurrentDasherPhase => _dasherPhase;

        /// <summary>Which way the coming (or current) dash goes, unit length.</summary>
        public Vector2 DasherAim => _dasherDashDir;

        /// <summary>0..1 through whichever tell is running; 0 outside one.</summary>
        public float DasherTellProgress01 =>
            _dasherPhase is DasherPhase.LineUpTell or DasherPhase.Telegraph
                ? 1f - Mathf.Clamp01(_dasherPhaseTimer / Mathf.Max(0.0001f, _dasherPhaseLength)) : 0f;

        /// <summary>A dash is under way (either of them).</summary>
        public bool DasherDashing => _dasherPhase is DasherPhase.LineUp or DasherPhase.Rushing;

        /// <summary>When (Time.time) a dash last bit something - DasherVisual's Bite frame.</summary>
        [System.NonSerialized] public float DasherBitAt = -10f;

        DasherPhase _dasherPhase;
        Vector2 _dasherRushTarget, _dasherDashDir = Vector2.left, _dasherLastPos;
        float _dasherPhaseTimer, _dasherPhaseLength;
        bool _dasherBit, _dasherRedoubled;

        // ---- gargoyle state ----
        //
        // The basic reuses the shared _telegraphTimer/_telegraphVisual/Recovering machinery
        // above wholesale - a stationary glow-then-resolve cycle is exactly Bomb/Chaser's own
        // shape. Only the FLIGHT is new: nothing else in this file moves toward a frozen point
        // that is not either "the target itself" (chase) or "a straight line through them"
        // (Dasher's rush), so it gets its own small pair of fields rather than being bent to fit
        // one of those.
        bool _gargoyleFlying;
        Vector2 _gargoyleFlyTarget, _gargoyleFlyFrom;
        float _gargoyleFlyTimer;

        /// <summary>An elite is in the air (GargoyleVisual's wings and lift).</summary>
        public bool GargoyleFlying => _gargoyleFlying;

        /// <summary>0..1 along the elite's current flight, by distance.</summary>
        public float GargoyleFlightProgress01
        {
            get
            {
                float total = Vector2.Distance(_gargoyleFlyFrom, _gargoyleFlyTarget);
                return total < 0.01f ? 1f : Mathf.Clamp01(1f - Vector2.Distance(transform.position, _gargoyleFlyTarget) / total);
            }
        }

        /// <summary>When (Time.time) a gaze last landed - the root, or the elite's slam.</summary>
        [System.NonSerialized] public float GargoyleStruckAt = -10f;

        // ---- booster state ----
        //
        // Reuses the shared _telegraphTimer/Recovering machinery for the cast itself - a
        // channel-then-resolve cycle is exactly Gargoyle's own basic shape, just aimed at an ally
        // instead of the player. The candidates are who it is CHANNELLING on right now (may still
        // fail - see ResolveBooster); the targets are who it has SUCCESSFULLY roared to power,
        // kept so it knows when one has died and its slot is free. An elite has two slots (the
        // user's call, 2026-10-19); the second pair is only ever used by one. The buff itself
        // lives on the RECIPIENT (see Boosted below), not here.
        EnemyController _boosterCandidate, _boosterCandidate2;
        EnemyController _boosterTarget, _boosterTarget2;

        /// <summary>When (Time.time) a Booster last roared - BoosterVisual's roar frames and waves.</summary>
        [System.NonSerialized] public float BoosterRoaredAt = -10f;

        /// <summary>Who that roar reached (the second only ever on an elite's double roar).</summary>
        [System.NonSerialized] public EnemyController BoosterRoaredA, BoosterRoaredB;

        /// <summary>Who a Booster is drawing breath to roar at, while it channels - it turns to it.</summary>
        public EnemyController BoosterFocus => _telegraphTimer > 0f ? _boosterCandidate : null;

        int BoosterSlots => Elite ? Tuning.Enemy.BoosterEliteSlots : 1;
        int BoosterHeld => (_boosterTarget != null ? 1 : 0) + (_boosterTarget2 != null ? 1 : 0);

        /// <summary>Every slot holds a living boosted ally: the lion lies spent until one dies.</summary>
        public bool BoosterSpent => Kind == EnemyKind.Booster && BoosterHeld >= BoosterSlots;

        // ---- boosted-recipient state (any kind, when a Booster has successfully cast on it) ----

        /// <summary>True once a Booster's cast has landed on THIS enemy - see ApplyBoosterBuff.
        /// Permanent for the rest of this enemy's life; nothing ever clears it early.</summary>
        public bool Boosted { get; private set; }

        /// <summary>An elite's boost: how much faster its attack cooldowns run (1 = unboosted).
        /// Read through AttackTempo; zero after an old domain reload means unboosted.</summary>
        float _boostTempo = 1f;

        // ---- bubbles state ----
        //
        // The Drowned Bell (redesigned 2026-10-19, the user's calls): the same channel-then-
        // resolve cycle as the lion, aimed at the unbubbled enemy NEAREST THE PLAYER that it can
        // see. A bubble is not permanent - it POPS - so the caster watches its ally's Bubbled flag
        // as well as its death. An ELITE also bubbles ITSELF with every blow (alone when no ally is
        // in sight), renewed only once its own is gone.
        EnemyController _bubbleCandidate;
        EnemyController _bubbleTarget;
        bool _bubbleSelfToo;
        bool _selfBubbleHeld;   // its own bubble stood last frame - an elite recovers when it pops

        /// <summary>When (Time.time) a Bubbles last blew - BubblesVisual's blow frames.</summary>
        [System.NonSerialized] public float BubblesBlewAt = -10f;

        /// <summary>Who a Bubbles is drawing water for, while it channels - it turns to it.</summary>
        public EnemyController BubblesFocus => _telegraphTimer > 0f ? _bubbleCandidate : null;

        /// <summary>Its ally's bubble holds (and its own, if elite): it tends, doing nothing.</summary>
        public bool BubblesTending => Kind == EnemyKind.Bubbles && _bubbleTarget != null && (!Elite || Bubbled);

        // ---- bubbled-recipient state (any kind, while a Bubbles' shield is holding) ----

        /// <summary>
        /// How many weapon-art breaks the shield on THIS enemy can still take before it pops - 1
        /// from an ordinary Bubbles' cast, BubblesEliteCharges (2) from an elite's, where a HEAVY
        /// art takes the whole of it at once (the user's call). Bubbles NEVER STACK (the user's
        /// call, 2026-10-19): a bubbled enemy is never picked, and a second bubble is refused.
        /// </summary>
        public int BubbleCharges { get; private set; }

        /// <summary>True while any charges remain - see BubbleCharges. Temporary, unlike Boosted:
        /// it clears once the last charge is spent by a weapon art.</summary>
        public bool Bubbled => BubbleCharges > 0;

        /// <summary>The drawn bubble on this body (BubbleShell) while it holds.</summary>
        BubbleShell _bubbleShell;

        /// <summary>
        /// The exact predicate installed on Health.BlocksHit, kept so PopBubble can tell "is this
        /// still MY block" before clearing the slot - Health.BlocksHit is a single field, not an
        /// event (see its own doc), so blindly nulling it on pop would silently erase a different
        /// system's block if one is ever added to an enemy later.
        /// </summary>
        System.Func<DamageInfo, bool> _bubbleBlock;

        void OnEnable() => EnemyRegistry.Register(this);
        void OnDisable()
        {
            EnemyRegistry.Unregister(this);
            CancelTelegraph();
            if (Kind == EnemyKind.Mortar) EndMortarAction();
            EndBeam();
            EndMireWindup();
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _status = StatusEffects.Get(gameObject);
            _strafeDir = Random.value < 0.5f ? -1 : 1;
            _body = GetComponent<CircleCollider2D>();
            RollTemperament();

            // Subscribed unconditionally rather than gated on Kind - EnemyFactory sets Kind
            // AFTER AddComponent returns, which is AFTER Awake has already run, so checking Kind
            // HERE would still see this field's own default (Bomb) for every enemy kind. The
            // gate belongs inside the handler, where Kind is guaranteed to already be correct.
            _health.Died += OnDied;
            _health.Damaged += ThrowIfChasm;
            _health.Damaged += WakeOnHurt;   // an unaware body is woken by a hit (the Idle partial)
        }

        /// <summary>
        /// The second of Bomb's two triggers - proximity is the other, in UpdateBomb. Routing
        /// both through Health.Died (the timer path calls Health.Kill to get here) means the
        /// explosion is applied in exactly one place, and it means a bomb killed mid-telegraph by
        /// the player's own attack still goes off exactly once rather than double-firing.
        /// </summary>
        void OnDied(Health h)
        {
            // A bomb that FELL goes off in the dark, out of reach of anything on the floor.
            if (Kind == EnemyKind.Bomb && !_fellIn) DoExplosion();
        }

        public void SetTarget(Transform t)
        {
            _target = t;
            _targetHealth = t ? t.GetComponent<Health>() : null;
            _targetController = t ? t.GetComponent<Player.PlayerController>() : null;
        }

        /// <summary>
        /// Impulse this enemy's attacks apply to the player. ZERO for bombs and elites: being
        /// shoved every time something touches you takes control away at exactly the moment you
        /// are trying to get out, and in a crowd the shoves compound into being pinballed.
        ///
        /// A field rather than a constant because a BOSS is expected to have it - a hit that
        /// moves you is a fine thing for one telegraphed enemy to own, and a bad thing for the
        /// twelve chasing you.
        /// </summary>
        public float AttackKnockback = Tuning.Enemy.AttackKnockback;

        // ---------------------------------------------------------------- flinch

        float _flinchTimer;

        /// <summary>True while this enemy is reeling: it cannot act and cannot steer.</summary>
        public bool Flinched => _flinchTimer > 0f;


        /// <summary>
        /// Interrupt this enemy: cancel whatever it was winding up, and hold it for a moment.
        ///
        /// FLINCH IS DENIAL, NOT A PUNISH WINDOW. There is deliberately no bonus damage and no
        /// Vulnerability attached - the value is that the attack does not happen. A window that
        /// also multiplied damage would make the Medium/Heavy finishers a damage choice twice
        /// over, which is exactly the collapse the weight classes exist to prevent.
        ///
        /// ARMOUR IS FLINCH RESISTANCE AS WELL AS DAMAGE MITIGATION - new scope on
        /// <see cref="EnemyArmor"/>, which previously only ever absorbed damage. While a shield is
        /// up the hit is soaked before it registers as something to react to, so a Medium is
        /// refused; Heavy is DEFINED as strong enough to force the reaction anyway. That is most of
        /// what makes Heavy worth its longer lock.
        ///
        /// THERE IS NO PER-ENEMY COOLDOWN, and none is needed: basics never flinch, so even an
        /// all-Heavy wheel lands at most one flinch per chain - two basics and a finisher apart.
        /// The spacing is structural. What that DOES require is that the duration stays under the
        /// fastest possible chain cycle, checked against the compressors rather than the base
        /// cadence (Air's momentum cap alone is 2.0x attack speed).
        /// </summary>
        /// <param name="seconds">How long to reel.</param>
        /// <param name="punchesThroughArmour">Heavy finishers only.</param>
        /// <returns>Whether the flinch was actually applied.</returns>
        public bool Flinch(float seconds, bool punchesThroughArmour)
        {
            if (_health == null || _health.IsDead) return false;

            var armor = GetComponent<EnemyArmor>();
            if (!punchesThroughArmour && armor != null && armor.Current > 0f) return false;

            // Longest wins rather than newest, so a Heavy cannot be shortened by a Light landing
            // a fraction of a second later.
            _flinchTimer = Mathf.Max(_flinchTimer, seconds);

            // The interrupt itself: whatever it was winding up does not happen.
            CancelTelegraph();
            _cooldown = Mathf.Max(_cooldown, seconds);
            // Stopped in its tracks - unless the hit that flinched it shoved it this frame. The
            // shove lands first (Health.Take) and the flinch after, so zeroing here erased every
            // Heavy knockback and pull before physics moved the body (measured 0.00 units,
            // 2026-10-14). The reel's own damping in FixedUpdate brings a shoved body to rest.
            if (_rb != null && _health.ShovedAt < Time.time) _rb.linearVelocity = Vector2.zero;

            RouteFlinch();
            return true;
        }

        /// <summary>
        /// Where a denied enemy goes next.
        ///
        /// The compression this design is built on: rather than authoring a bespoke reaction per
        /// kind, every kind gets ONE legible "I finished attacking" state and a flinch routes into
        /// it early - so a denied enemy and a successful one read as the same beat, which is what
        /// makes the reaction legible at all.
        ///
        /// ONLY DASHER HAS THAT STATE TODAY. Its Evading phase is exactly the post-combo beat this
        /// wants, so it routes straight into it. Bomb, Ranged, Turret and Chaser have no
        /// post-attack beat to route into yet - for them the flinch is the interrupt and the hold,
        /// which is real and correct as far as it goes, but it is half of what was designed. The
        /// beats are their own pass and every one of them touches a state machine.
        /// </summary>
        void RouteFlinch()
        {
            // Dasher has its own post-combo phase and predates this; everything else routes into
            // the shared beat. Both are the same idea - the flinch does not invent a reaction, it
            // arrives early at one the kind already performs.
            if (Kind == EnemyKind.Dasher)
            {
                if (_dasherPhase is DasherPhase.LineUpTell or DasherPhase.LineUp or DasherPhase.Braced
                                 or DasherPhase.Telegraph or DasherPhase.Rushing)
                    BeginEvade();
                return;
            }

            // A flinch mid-flight cancels the whole swoop, not merely the landing it was headed
            // toward - the same "the commitment is what gets denied" idea Dasher's own Rushing
            // phase lives by above, just routed into the shared recover beat below rather than a
            // dedicated evade phase: "it lost its run at you" reads fine on a kind that otherwise
            // never gives ground at all.
            if (Kind == EnemyKind.Gargoyle) _gargoyleFlying = false;

            // Denies the flame outright and whatever is left of a barrage - shells already in the
            // air still land.
            if (Kind == EnemyKind.Mortar) EndMortarAction();

            // Denies whatever is left of a sweep - fireballs already thrown still fly.
            if (Kind == EnemyKind.Bomb) { _bombShotsLeft = 0; _bombSpent = 0f; }

            BeginRecover(RecoverSeconds);

            // A Turret cannot gain space by moving, so its recovery is the beam having to spin up
            // again from cold. Killed here rather than left running, or a denied turret would keep
            // firing through the beat it is supposed to be spending.
            //
            // An elite's mire wind-up is denied with it - its cooldown was already spent at the
            // wind-up, so the flinch costs the turret the lob outright.
            if (Kind == EnemyKind.Turret) { EndBeam(); _turretCharge = 0f; EndMireWindup(); }
        }

        // ---------------------------------------------------------------- the booster buff

        /// <summary>
        /// Applied by a Booster's roar (see UpdateBooster). A BASIC ally GAINS ITS KIND'S OWN ELITE
        /// ATTACK PATTERN, and only that: no HP or damage multiplier, no displacement resistance.
        /// Setting ElitePattern/ElitePatternEvery directly is enough on every kind but Gargoyle -
        /// ElitePatternDue() (which Riposte/Volley/Cluster/Overcharge/Redouble all read) was never
        /// gated on the Elite bool to begin with, only on ElitePattern itself. Gargoyle is the one
        /// kind whose pattern (Descent) checks Elite directly instead, which is why
        /// FindBoosterCandidate excludes it.
        ///
        /// AN ELITE ALLY already has its pattern - the lion only picks one when no basic is in
        /// reach (the user's call, 2026-10-19) - so its attacks come round FASTER instead: its
        /// cooldowns run BoosterEliteTempo times as fast (through AttackTempo, so the kinds that
        /// tick a cooldown through it - Chaser, Ranged, Bomb, Mortar) and its wind-ups are
        /// BoosterEliteTelegraphMul as long, never under TelegraphMinSeconds.
        ///
        /// PERMANENT, and guarded against a second application - both by the Boosted flag here and
        /// by FindBoosterCandidate refusing to target an already-boosted ally in the first place.
        /// </summary>
        public void ApplyBoosterBuff()
        {
            if (Boosted) return;
            Boosted = true;

            if (Elite)
            {
                _boostTempo = Tuning.Enemy.BoosterEliteTempo;
                TelegraphDuration = Mathf.Max(Tuning.Enemy.TelegraphMinSeconds,
                                              TelegraphDuration * Tuning.Enemy.BoosterEliteTelegraphMul);
            }
            else
            {
                var def = EnemyTypes.Of(Kind);
                ElitePattern = def.Elite;
                ElitePatternEvery = Mathf.Max(1, def.EliteEvery);
            }

            // Green motes rising off it, the colour of the roar (the user's sign-off) - it was a
            // soft pale circle over the body.
            BoostedMotes.Attach(this);
        }

        // ---------------------------------------------------------------- the bubble shield

        /// <summary>
        /// Applied by a Bubbles' blow - see UpdateBubbles. IMMUNITY TO EVERYTHING except a weapon
        /// art: implemented on Health.BlocksHit rather than Health.Immune, because Immune
        /// short-circuits Take() unconditionally with no hook for a per-hit exception - BlocksHit
        /// blocks everything whose DamageInfo.IsFinisher is false and lets a finisher's hit proceed
        /// completely normally (full damage, flinch, wear), breaking the bubble once (see
        /// OnBubbleDamaged). LITERALLY EVERYTHING: a burn or bleed tick is a non-finisher Take too.
        ///
        /// NEVER STACKS: an already-bubbled body refuses a second one (the user's call).
        /// <paramref name="accent"/> is the blowing bell's colour (BubblesArt.AccentOf);
        /// <paramref name="from"/> is where the drawn bubble drifts in from - null for one that
        /// forms in place (an elite bubbling itself).
        /// </summary>
        public void ApplyBubble(int charges, Color[] accent, Vector2? from)
        {
            if (Bubbled) return;
            var hp = GetComponent<Health>();
            if (hp == null) return;

            BubbleCharges = Mathf.Max(1, charges);
            _bubbleBlock = info => !info.IsFinisher;
            hp.BlocksHit = _bubbleBlock;
            hp.Damaged += OnBubbleDamaged;
            _bubbleShell = BubbleShell.Attach(this, BubbleCharges, accent, from);
        }

        /// <summary>
        /// Fires only for a hit that made it PAST BlocksHit - which, while Bubbled, can only ever
        /// be a weapon art, by construction. A Light or Medium art breaks ONE wall; a HEAVY art
        /// breaks the whole bubble (the user's call: an elite's two walls go in one Heavy), read off
        /// the art the player last dispatched (PlayerController.LastArtWeight).
        /// </summary>
        void OnBubbleDamaged(DamageInfo info)
        {
            if (BubbleCharges <= 0) return;   // defensive; unreachable while unsubscribed
            bool heavy = info.Source != null && info.Source.TryGetComponent<Player.PlayerController>(out var pc)
                      && pc.LastArtWeight == FinisherWeight.Heavy;
            BubbleCharges = heavy ? 0 : BubbleCharges - 1;

            if (BubbleCharges <= 0) { PopBubble(); return; }
            if (_bubbleShell != null) _bubbleShell.SetWalls(BubbleCharges);
        }

        void PopBubble()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Pop, transform.position);
            BubbleCharges = 0;

            var hp = GetComponent<Health>();
            if (hp != null)
            {
                // Only clear the slot if it is still OUR predicate - see _bubbleBlock's own doc.
                if (hp.BlocksHit == _bubbleBlock) hp.BlocksHit = null;
                hp.Damaged -= OnBubbleDamaged;
            }
            _bubbleBlock = null;

            if (_bubbleShell != null) _bubbleShell.Burst();
            _bubbleShell = null;
        }

        // ---------------------------------------------------------------- the recover beat

        float _recoverTimer;

        /// <summary>
        /// True during the beat after an attack - landed OR denied.
        ///
        /// ONE STATE FOR BOTH OUTCOMES is the whole compression this rework rests on. Rather than
        /// authoring a bespoke reaction per kind for the flinched case, every kind gets one legible
        /// "I finished attacking" beat and a flinch routes into it early. A denied enemy and a
        /// successful one then read as the same shape, which is what makes the reaction legible at
        /// all - the player learns one thing and it means the same thing every time.
        ///
        /// What the beat CONTAINS still belongs to the kind: Ranged gives ground, Bomb backs off,
        /// Turret spins down, Chaser recovers in place. Only the shape is shared.
        /// </summary>
        public bool Recovering => _recoverTimer > 0f;

        void BeginRecover(float seconds)
        {
            _recoverTimer = Mathf.Max(_recoverTimer, seconds);
            CancelTelegraph();
        }

        void TickRecover()
        {
            if (_recoverTimer > 0f) _recoverTimer = Mathf.Max(0f, _recoverTimer - Time.deltaTime);
        }

        /// <summary>Seconds of recovery this kind takes after an attack resolves or is denied.</summary>
        float RecoverSeconds => Kind switch
        {
            EnemyKind.Ranged   => Tuning.Enemy.RangedKiteSeconds,
            EnemyKind.Bomb     => Tuning.Enemy.BombRecoverSeconds,
            EnemyKind.Turret   => Tuning.Enemy.TurretRecoverSeconds,
            EnemyKind.Chaser   => Tuning.Enemy.ChaserRecoverSeconds,
            EnemyKind.Gargoyle => Elite ? Tuning.Enemy.GargoyleEliteRecoverSeconds
                                        : Tuning.Enemy.GargoyleRecoverSeconds,
            EnemyKind.Booster  => Tuning.Enemy.BoosterRecoverSeconds,
            EnemyKind.Bubbles  => Tuning.Enemy.BubblesRecoverSeconds,
            EnemyKind.Mortar   => Tuning.Enemy.MortarRecoverSeconds,
            _                  => 0f,   // Dasher has its own Evading phase and does not use this
        };

        void TickFlinch()
        {
            if (_flinchTimer <= 0f) return;
            _flinchTimer -= Time.deltaTime;
        }

        void FixedUpdate()
        {
            if (_health.IsDead || _target == null) return;

            var pos = _rb.position;
            NavField.Tick(_target.position);
            EnemySurround.Tick(_target.position);

            if (_fellIn) return;

            // Before the flinch return on purpose: a body knocked into fire burns while it reels.
            TickPits(pos);
            if (TickChasm(pos)) return;

            // GRIP - water caps the steering blend (Drive, and the reel below) and the damping is
            // re-solved so this body's top speed stays its dry one - see FloorPits.DampingFor.
            float grip = FloorPits.GripAt(pos);
            _rb.linearDamping = FloorPits.DampingFor(grip, _turnRate, Tuning.Enemy.BodyDamping);

            // Damped rather than hard-zeroed, the same choice AttackLocked makes on the player:
            // a knockback landing on a flinched enemy should still move it, so the reel takes away
            // its own steering and nothing else.
            if (Flinched)
            {
                // On water the reel's damping is capped too, so a body knocked into a puddle
                // skates across it rather than stopping where it was hit.
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, Mathf.Min(0.35f, grip));
                return;
            }

            // Unaware: the idle wander near home, or standing startled (the Idle partial).
            if (_unaware || _startle > 0f) { IdleMove(pos); return; }

            var to = (Vector2)_target.position - pos;
            float dist = to.magnitude;
            float statusMul = _status ? _status.MoveMultiplier : 1f;
            // The GROUND is asked, the same way the player's speed asks it: sand slows enemies
            // as it slows the player, and so does a mire a parry turned (see Hazards.FloorPits).
            // Folded into every walking speed below - kiting, evading and rushing included.
            float moveMul = statusMul * FloorPits.EnemySpeedMultiplierAt(pos);
            float speed = MoveSpeed * moveMul;

            Vector2 desired;

            // THE RECOVER BEAT'S MOVEMENT. Ranged and Bomb spend it giving ground; Chaser spends
            // it standing still (it is a presser, and one that backed off after every landed hit
            // would stop being the thing that presses you); Turret cannot move at all. Handled
            // ahead of the per-kind steering so a beat cannot be silently overridden by the
            // approach logic below it.
            if (Recovering && Kind is EnemyKind.Ranged or EnemyKind.Bomb)
            {
                float away = Kind == EnemyKind.Ranged
                    ? Tuning.Enemy.RangedKiteSpeed
                    : Tuning.Enemy.BombRetreatSpeed;
                desired = Flee(to) * (away * moveMul);
                Drive(desired, speed);
                return;
            }
            if (Recovering && Kind == EnemyKind.Chaser)
            {
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, Vector2.zero, 0.25f);
                return;
            }

            if (Kind == EnemyKind.Ranged)
            {
                // Holds still to aim - re-aiming mid-telegraph would let it track a dodge instead
                // of committing to the shot the player already saw.
                if (_telegraphTimer > 0f)
                {
                    desired = Vector2.zero;
                }
                else if (dist < PreferredMinRange)
                {
                    desired = Flee(to) * speed;
                }
                else if (dist > AttackRange)
                {
                    desired = Steer(Weave(NavField.RouteDirection(pos, _target.position), dist), dist) * speed;
                }
                else
                {
                    // Inside the band: circle rather than stand dead still, so a lone kiter still
                    // reads as alive and moving instead of a turret.
                    desired = Strafe(to) * (speed * Tuning.Enemy.RangedStrafeSpeedFraction);
                }
            }
            else if (Kind == EnemyKind.Bomb && (_telegraphTimer > 0f || _bombShotsLeft > 0 || _bombSpent > 0f))
            {
                // Frozen once armed, same reasoning as the ranged bolt above - it detonates
                // where it caught you, not wherever you've since moved to relative to it. Held
                // still through a sweep's wind-up, the throw and the gutter after it too.
                desired = Vector2.zero;
            }
            else if (Kind == EnemyKind.Bomb && !BombCritical)
            {
                // Fights at arm's length: in from past the band, back off inside it, drift within.
                if (dist < Tuning.Enemy.BombFightRangeMin)
                    desired = Flee(to) * speed;
                else if (dist > Tuning.Enemy.BombFightRangeMax)
                    desired = Steer(Weave(NavField.RouteDirection(pos, _target.position), dist), dist) * speed;
                else
                    desired = Strafe(to) * (speed * 0.4f);
            }
            else if (Kind == EnemyKind.Turret || Kind == EnemyKind.Booster || Kind == EnemyKind.Bubbles)
            {
                // Never moves, full stop - all three are placed once at spawn and stand there for
                // the rest of the floor (no elite of theirs flies, unlike Gargoyle's). Written explicitly rather than left to fall out
                // of MoveSpeed == 0 so a future tuning pass can't accidentally make either walk.
                desired = Vector2.zero;
            }
            else if (Kind == EnemyKind.Dasher)
            {
                if (DasherDashing)
                {
                    // NOT steered, ever, and not blended in: a dash is a straight line at full speed
                    // from its first step, landing exactly on its end (a column in the way is the
                    // player's to use - see OnCollisionEnter2D). Damping is off for it, as the
                    // player's own dash suspends it, so the declared speed is the speed moved.
                    float along = Vector2.Dot(_dasherRushTarget - pos, _dasherDashDir);
                    float v = along > 0.01f
                        ? Mathf.Min(Tuning.Enemy.DasherDashSpeed * moveMul, along / Time.fixedDeltaTime) : 0f;
                    _rb.linearDamping = 0f;
                    _rb.linearVelocity = _dasherDashDir * v;
                    return;
                }
                switch (_dasherPhase)
                {
                    case DasherPhase.Evading:
                        // Straight away from wherever the player IS now - the whole point is
                        // buying real distance from the live threat.
                        desired = Flee(to) * (Tuning.Enemy.DasherEvadeSpeed * moveMul);
                        break;

                    case DasherPhase.Approach:
                        desired = MeleeApproach(pos, dist, Tuning.Enemy.DasherEngageRange) * speed;
                        break;

                    default:
                        // Committed: the tells, the brace and the spent stand all hold still.
                        desired = Vector2.zero;
                        break;
                }
            }
            else if (Kind == EnemyKind.Gargoyle)
            {
                if (_gargoyleFlying)
                {
                    // Flying: over the ground, so neither the route nor the ground's slow applies.
                    var toFly = _gargoyleFlyTarget - pos;
                    desired = toFly.sqrMagnitude > 0.0004f
                        ? toFly.normalized * (Tuning.Enemy.GargoyleFlySpeed * statusMul)
                        : Vector2.zero;
                }
                else
                {
                    // Perched, full stop - even the elite between swoops. Written explicitly
                    // rather than left to fall out of MoveSpeed == 0, same reasoning Turret's own
                    // branch states: a future tuning pass can't accidentally make it walk.
                    desired = Vector2.zero;
                }
            }
            else if (Kind == EnemyKind.Mortar)
            {
                desired = MortarSteer(to, dist, speed);
            }
            else
            {
                // Close the gap, then hold at attack range instead of shoving into the player.
                desired = dist > AttackRange * 0.9f ? MeleeApproach(pos, dist, AttackRange) * speed : Vector2.zero;
            }

            Drive(desired, speed);
        }

        // ---------------------------------------------------------------- steering
        //
        // THE ROUTE says which way round the room (NavField, shared by the wave); THE STEER picks
        // the actual direction for the next step - the wanted one unless a wall, a pit or a
        // packmate is in the way. Every walking branch above goes through Steer, so no kind can
        // walk into a column the others go round.

        CircleCollider2D _body;
        bool _hasSlot;
        float _slotAngle;
        float _weaveDeg, _weaveHz, _weavePhase, _turnRate = 0.2f;

        /// <summary>This body's radius, for packmates keeping their distance.</summary>
        internal float BodyRadius
        {
            get
            {
                if (_body == null) _body = GetComponent<CircleCollider2D>();
                return _body != null ? _body.radius : 0.3f;
            }
        }

        /// <summary>
        /// The melee kinds - the ones EnemySurround fans out. Kept in the pack while winding up
        /// or recovering too: it still OCCUPIES its side, and dropping out reshuffled every other
        /// slot each time one of them swung.
        /// </summary>
        internal bool WantsSlot =>
            _health != null && !_health.IsDead && _target != null
            && (Kind is EnemyKind.Chaser or EnemyKind.Dasher || (Kind == EnemyKind.Bomb && BombCritical));

        internal void AssignSlot(float bearingRad) { _hasSlot = true; _slotAngle = bearingRad; }
        internal void ClearSlot() => _hasSlot = false;

        /// <summary>Rolled once per enemy, so no two walk the same line at the same rate.</summary>
        void RollTemperament()
        {
            _weaveDeg = Random.Range(Tuning.Steering.WeaveDegreesMin, Tuning.Steering.WeaveDegreesMax);
            _weaveHz = Random.Range(Tuning.Steering.WeaveHzMin, Tuning.Steering.WeaveHzMax);
            _weavePhase = Random.value * Mathf.PI * 2f;
            _turnRate = Random.Range(Tuning.Steering.TurnRateMin, Tuning.Steering.TurnRateMax);
        }

        /// <summary>Velocity toward <paramref name="desired"/> at this enemy's own turn rate,
        /// after the stuck watch has had its say.</summary>
        void Drive(Vector2 desired, float speed)
        {
            desired = TickStuck(desired, speed);
            // Water caps the blend, as it does the player's: a pack wading through drifts.
            float grip = FloorPits.GripAt(_rb.position);
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, desired, Mathf.Min(_turnRate, grip));
        }

        /// <summary>
        /// A melee approach, unit length: down the route from afar, then - once inside the
        /// surround and with a clear line - curving round to its own side of the player.
        ///
        /// It never aims straight at the slot from the far side: the aim point swings at most
        /// 50 degrees round the player per step and comes in less the further round it still has
        /// to go, so a body sent to the other side ORBITS there instead of cutting through the
        /// player's reach.
        /// </summary>
        Vector2 MeleeApproach(Vector2 pos, float dist, float engage)
        {
            Vector2 player = _target.position;
            Vector2 want = Vector2.zero;
            float slotRadius = engage * Tuning.Steering.SlotRadiusFraction;

            // Only from OUTSIDE the slot's radius. Closer in, a slot would pull the body back out
            // to it - which is what a wary Dasher, already close but with a column in the way,
            // did: it parked beside the column instead of walking round it.
            if (_hasSlot && dist > slotRadius)
            {
                float bearing = Mathf.Atan2(pos.y - player.y, pos.x - player.x);
                float off = Mathf.DeltaAngle(bearing * Mathf.Rad2Deg, _slotAngle * Mathf.Rad2Deg);
                float swing = Mathf.Clamp(off, -50f, 50f);
                float aimBearing = bearing + swing * Mathf.Deg2Rad;
                // Comes in slower the further it is from its side, so it gets ROUND first - at the
                // same rate both ways it reached the player still on the side it started.
                float inward = 0.5f + 2f * Mathf.Clamp01(1f - Mathf.Abs(off) / 120f);
                float aimRadius = Mathf.Max(slotRadius, dist - inward);
                var aim = player + new Vector2(Mathf.Cos(aimBearing), Mathf.Sin(aimBearing)) * aimRadius;

                if (NavField.ClearLine(pos, aim) && (aim - pos).sqrMagnitude > 0.01f)
                    want = (aim - pos).normalized;
            }

            if (want == Vector2.zero) want = NavField.RouteDirection(pos, player);
            return Steer(Weave(want, dist), dist);
        }

        /// <summary>The route turned a few degrees either way on this enemy's own slow clock,
        /// fading to straight on the final approach so a swing still lands square on.</summary>
        Vector2 Weave(Vector2 dir, float dist)
        {
            float fade = Mathf.InverseLerp(Tuning.Steering.WeaveFadeNear, Tuning.Steering.WeaveFadeFar, dist);
            if (fade <= 0f || dir == Vector2.zero) return dir;
            float deg = _weaveDeg * fade * Mathf.Sin(Time.time * _weaveHz * Mathf.PI * 2f + _weavePhase);
            return (Vector2)(Quaternion.Euler(0f, 0f, deg) * dir);
        }

        /// <summary>
        /// Giving ground, unit length: away from the player, but toward OPEN floor - a straight
        /// "away" vector ran every kiter into the first corner behind it, where it stood still.
        /// Cornered, the open floor is along the wall, so it breaks out sideways; directions that
        /// pass close to the player are penalised so the break-out goes round, not through.
        /// </summary>
        Vector2 Flee(Vector2 toPlayer)
        {
            if (toPlayer.sqrMagnitude < 1e-6f) toPlayer = Vector2.up;
            return Steer(-toPlayer, float.PositiveInfinity, toPlayer);
        }

        /// <summary>Circling in the band, unit length - and when the circle runs into a wall,
        /// round the other way.</summary>
        Vector2 Strafe(Vector2 toPlayer)
        {
            if (toPlayer.sqrMagnitude < 1e-6f) return Vector2.zero;
            var perp = new Vector2(-toPlayer.y, toPlayer.x).normalized * _strafeDir;
            var v = Steer(perp, float.PositiveInfinity);
            if (Vector2.Dot(v, perp) < 0.3f)
            {
                _strafeDir = -_strafeDir;
                v = Steer(-perp, float.PositiveInfinity);
            }
            return v;
        }

        static Vector2[] _compass;

        /// <summary>
        /// The direction to actually take, unit length (zero when every way is shut): the wanted
        /// one and sixteen around it, each scored by how well it matches, how much free floor is
        /// ahead, the costliest pit it runs into, and how hard it runs into a packmate.
        /// </summary>
        /// <param name="reach">How far away the thing being walked to is - a wall beyond it is
        /// no reason to turn.</param>
        /// <param name="fleeFrom">Set when fleeing: the vector to the player, which open floor
        /// and the buffer round the player are scored against.</param>
        Vector2 Steer(Vector2 want, float reach, Vector2? fleeFrom = null)
        {
            if (want.sqrMagnitude < 1e-6f) return Vector2.zero;
            want.Normalize();

            if (_compass == null)
            {
                _compass = new Vector2[16];
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2f / 16f;
                    _compass[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                }
            }

            var pos = _rb.position;
            var crowd = CrowdPush(pos);
            bool flee = fleeFrom.HasValue;
            float look = Tuning.Steering.Lookahead * (flee ? 2f : 1f);
            float horizon = Mathf.Min(look, reach);

            Vector2 best = Vector2.zero;
            float bestScore = float.NegativeInfinity;
            for (int i = -1; i < _compass.Length; i++)
            {
                var d = i < 0 ? want : _compass[i];
                float free = NavField.FreeDistance(pos, d, look, out float pit);
                if (free < Mathf.Min(Tuning.Steering.MinFree, horizon)) continue;

                float s = Vector2.Dot(d, want);
                s -= Tuning.Steering.WallWeight * (1f - Mathf.Clamp01(free / horizon));
                s -= Tuning.Steering.PitWeight * pit;
                s -= Tuning.Steering.SeparationWeight * Mathf.Max(0f, Vector2.Dot(d, crowd));

                if (flee)
                {
                    s += Tuning.Steering.FleeOpenness * (free / look);
                    var tp = fleeFrom.Value;
                    float along = Mathf.Clamp(Vector2.Dot(tp, d), 0f, look);
                    float pass = (tp - d * along).magnitude;
                    if (pass < Tuning.Steering.FleePlayerBuffer)
                        s -= 1.5f * (1f - pass / Tuning.Steering.FleePlayerBuffer);
                }

                if (s > bestScore) { bestScore = s; best = d; }
            }

            // Every way shut (wedged in a crowd against a column): push the way it wanted and let
            // physics and the stuck watch sort it out, rather than freezing.
            return bestScore == float.NegativeInfinity ? want : best;
        }

        /// <summary>Summed bearings to packmates closer than <see cref="Tuning.Steering.SeparationGap"/>
        /// edge to edge, each weighted by how deep inside the gap it is.</summary>
        Vector2 CrowdPush(Vector2 pos)
        {
            var push = Vector2.zero;
            float mine = BodyRadius;
            float gap = Tuning.Steering.SeparationGap;
            var all = EnemyRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var e = all[i];
                if (e == null || e == this) continue;
                var d = (Vector2)e.transform.position - pos;
                float reach = mine + e.BodyRadius + gap;
                float m2 = d.sqrMagnitude;
                if (m2 >= reach * reach || m2 < 1e-6f) continue;
                float m = Mathf.Sqrt(m2);
                push += d / m * Mathf.Clamp01((reach - m) / gap);
            }
            return push;
        }

        // ---------------------------------------------------------------- pits

        float _pitTick;
        Vector2 _pitLastPos;
        bool _pitTracking;

        /// <summary>
        /// Fire and spikes hurt enemies too - as a fraction of THEIR max HP
        /// (<see cref="Tuning.Steering.FireEnemyHpPerSecond"/>), since the player's flat numbers
        /// are sized for a player. Routes go round both, so this mostly lands on a body knocked in
        /// or caught by a fire flaring up under it. Spikes measure DISPLACEMENT, the player's
        /// rule: knocked across them still hurts.
        ///
        /// Kinds that never walk are exempt - a turret that spawned on a pit could not step out,
        /// and the pit would be a free kill rather than a positioning cost. So is anything flying.
        /// </summary>
        // ---------------------------------------------------------------- chasms

        bool _fellIn;

        /// <summary>True from the frame this body went over a chasm's edge - it falls, then dies.</summary>
        public bool FellIn => _fellIn;

        /// <summary>
        /// The ground under this body, if it is a chasm. A body SENT over the lip by a hit that
        /// moved it (Health.ShovedAt, within Tuning.Chasm.ShoveGrace) falls - a Heavy finisher, a
        /// vortex's pull, anything that already displaces. A body that merely drifted there (its
        /// pack shouldering it, a steer at the lip) is put back on the floor: the route keeps off
        /// the void, and a chasm that ate enemies on its own would be a free clear.
        /// Elites are Anchored - moved by nothing - so they never get here by force.
        /// </summary>
        bool TickChasm(Vector2 pos)
        {
            if (!Arena.HasChasms || !Arena.DeepInChasm(pos, 0f)) return false;

            bool shoved = Time.time - _health.ShovedAt <= Tuning.Chasm.ShoveGrace;
            if (!shoved || !Arena.DeepInChasm(pos, Tuning.Chasm.FallInset))
            {
                if (!shoved)
                {
                    // The route and the steer keep bodies off the void; this firing means one of
                    // them let a body through - worth seeing in the log, it is not meant to happen.
                    Debug.Log($"[Chasm] {name} drifted over the void at {pos} - put back");
                    _rb.position = Arena.NearestFloor(pos, 0.3f);
                    _rb.linearVelocity = Vector2.zero;
                }
                return false;
            }

            StartCoroutine(Fall(_rb.linearVelocity));
            return true;
        }

        /// <summary>
        /// A hit that MOVES this body, aimed at a chasm within reach, throws it in.
        ///
        /// Physics alone almost never does it: a flinched enemy's velocity is damped hard every
        /// step, so a Heavy finisher's knockback (3-11) carries a body 0.2-0.6 units, and the
        /// route keeps enemies a body-width off the lip. So the throw is decided here, the moment
        /// the hit lands: walk the knockback's own direction out to a reach that grows with its
        /// strength (Tuning.Chasm.ThrowReach*, so Finisher Knockback lengthens it too); a point
        /// deep in a chasm on that line means the body goes over. Anything that already displaces
        /// qualifies - a Heavy finisher, Air's vortex pulling across a hole, Undertow dragging a
        /// pack toward a player standing on the far side. Anchored (an elite) is moved by nothing.
        /// </summary>
        void ThrowIfChasm(DamageInfo info)
        {
            if (_fellIn || !Arena.HasChasms || !info.Displaces || _health.Anchored || _health.IsDead) return;
            float k = info.Knockback.magnitude;
            if (k < 0.001f) return;

            var dir = info.Knockback / k;
            float reach = Mathf.Clamp(k * Tuning.Chasm.ThrowReachPerKnockback,
                                      Tuning.Chasm.ThrowReachMin, Tuning.Chasm.ThrowReachMax);
            var from = _rb.position;
            for (float d = 0.1f; d <= reach; d += 0.1f)
            {
                var p = from + dir * d;
                // A wall on the line stops the throw: nothing is thrown THROUGH stone.
                if (Arena.WallBetween(from, p)) return;
                if (!Arena.DeepInChasm(p, Tuning.Chasm.FallInset)) continue;
                StartCoroutine(Thrown(p + dir * 0.25f));
                return;
            }
        }

        /// <summary>The throw itself: a quick glide over the lip, then the fall.</summary>
        System.Collections.IEnumerator Thrown(Vector2 to)
        {
            _fellIn = true;
            _health.Immune = true;
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
            Vector3 from = transform.position;
            var target = new Vector3(to.x, to.y, from.z);
            for (float t = 0f; t < Tuning.Chasm.ThrowSeconds; t += Time.deltaTime)
            {
                float k = t / Tuning.Chasm.ThrowSeconds;
                transform.position = Vector3.Lerp(from, target, 1f - (1f - k) * (1f - k));
                yield return null;
            }
            transform.position = target;
            yield return Fall(target - from);
        }

        /// <summary>
        /// Down a drop or under a lake (Enemies.ChasmFall, drawn), then the kill - an ordinary
        /// death, so the wave counts it, the spire rises on it and a Rift Box may drop (at the
        /// lip: RiftBoxPickup asks for floor). The player's on-kill effects are credited here, the
        /// way an arrow or a disc credits its own kills: nothing struck the final blow, but the
        /// player is why it fell. <paramref name="thrown"/> is the way it was going over the lip.
        /// </summary>
        System.Collections.IEnumerator Fall(Vector2 thrown)
        {
            _fellIn = true;
            _health.Immune = true;
            _rb.linearVelocity = Vector2.zero;
            _rb.simulated = false;
            foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;

            var start = transform.position;
            if (Arena.ChasmIsWater) yield return ChasmFall.Sink(transform);
            else yield return ChasmFall.Drop(transform, thrown);

            // The fall is an ordinary death: the run's ledger hears it through Died like any
            // other (GameBootstrap.HookDeath, which draws nothing for a body that fell in),
            // credited at the lip.
            transform.position = Arena.NearestFloor(start, 0.3f);
            _health.Immune = false;
            _health.Kill();
        }

        void TickPits(Vector2 pos)
        {
            bool exempt = Kind is EnemyKind.Turret or EnemyKind.Booster or EnemyKind.Bubbles
                                 or EnemyKind.Gargoyle;
            var pit = exempt ? null : FloorPits.At(pos);
            if (pit == null) { _pitTick = 0f; _pitTracking = false; return; }

            float dt = Time.fixedDeltaTime;
            float perSecond = 0f;
            var element = ElementType.Fire;
            if (pit is FirePit fire)
            {
                perSecond = Tuning.Steering.FireEnemyHpPerSecond * fire.Burn;
            }
            else if (pit is SpikePit)
            {
                element = ElementType.Earth;
                float moved = _pitTracking && dt > 0f ? Vector2.Distance(pos, _pitLastPos) / dt : 0f;
                if (_pitTracking && moved >= Tuning.Hazards.SpikeMovementThreshold)
                    perSecond = Tuning.Steering.SpikeEnemyHpPerSecond;
            }
            _pitLastPos = pos;
            _pitTracking = true;

            // Reset rather than freeze, the player's rule: a part-charged tick held across a lull
            // would land the moment the fire stirred.
            if (perSecond <= 0.001f) { _pitTick = 0f; return; }

            _pitTick += dt;
            if (_pitTick < Tuning.Steering.PitEnemyTickInterval) return;
            _pitTick = 0f;
            _health.Take(new DamageInfo(_health.Max * perSecond * Tuning.Steering.PitEnemyTickInterval,
                                        element, pit.gameObject) { Tick = true });
        }

        // ---------------------------------------------------------------- stuck

        Vector2 _stuckOrigin;
        float _stuckSample, _stuckFor, _unstickTimer;
        Vector2 _unstickDir;
        bool _stuckReported;

        /// <summary>
        /// Notices a body that has been TRYING to move and has not, and gets it out:
        ///
        ///   every sample   a short push in a random open direction (wedged in a crowd, caught
        ///                  on a corner)
        ///   after a while  if the route cannot reach where it stands at all - walled into a
        ///                  pocket, or spawned inside geometry - it is moved to the nearest floor
        ///                  that is reachable. Only then: a body merely held up by its pack is
        ///                  never teleported.
        ///   after longer   one warning in the log, for StuckWatch-style reporting.
        ///
        /// Standing still on purpose (holding at range, telegraphing, rooted) never counts - it
        /// only runs while the steering is asking for real speed.
        /// </summary>
        Vector2 TickStuck(Vector2 desired, float speed)
        {
            float dt = Time.fixedDeltaTime;
            var pos = _rb.position;

            if (_unstickTimer > 0f)
            {
                _unstickTimer -= dt;
                desired = _unstickDir * Mathf.Max(speed, desired.magnitude);
            }

            bool trying = desired.sqrMagnitude > 0.09f * MoveSpeed * MoveSpeed;
            if (!trying)
            {
                _stuckSample = 0f;
                _stuckFor = 0f;
                _stuckOrigin = pos;
                return desired;
            }

            _stuckSample += dt;
            if (_stuckSample < Tuning.Steering.StuckSampleSeconds) return desired;

            bool moved = Vector2.Distance(pos, _stuckOrigin) >= Tuning.Steering.StuckMinTravel;
            _stuckSample = 0f;
            _stuckOrigin = pos;
            if (moved) { _stuckFor = 0f; _stuckReported = false; return desired; }

            _stuckFor += Tuning.Steering.StuckSampleSeconds;

            float a = Random.value * Mathf.PI * 2f;
            _unstickDir = Steer(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), float.PositiveInfinity);
            if (_unstickDir == Vector2.zero) _unstickDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            _unstickTimer = Tuning.Steering.UnstickSeconds;

            if (_stuckFor >= Tuning.Steering.RelocateAfterSeconds && !NavField.Reachable(pos))
            {
                var to = NavField.NearestReachable(pos);
                if (to != pos)
                {
                    _rb.position = to;
                    transform.position = to;
                    _stuckFor = 0f;
                    Debug.Log($"[Steering] {name} was walled off at {pos} - moved to {to}.");
                    return desired;
                }
            }

            if (_stuckFor >= Tuning.Steering.ReportAfterSeconds && !_stuckReported)
            {
                _stuckReported = true;
                Debug.LogWarning($"[StuckWatch] {name} ({Kind}) has not moved for {_stuckFor:0}s at {pos}, " +
                                 $"reachable={NavField.Reachable(pos)}, target at {(Vector2)_target.position}.");
            }
            return desired;
        }

        // ---------------------------------------------------------------- the Dasher meets a column

        /// <summary>Until when a slammed Dasher will only charge down a clear line - see
        /// Tuning.Steering.DasherShySeconds.</summary>
        float _dasherShyUntil;

        void OnCollisionEnter2D(Collision2D c) => TryRushImpact(c);
        void OnCollisionStay2D(Collision2D c) => TryRushImpact(c);

        /// <summary>
        /// A dash is a straight line that never steers - so one that meets something SOLID (a
        /// column, the spire, a wall) stops dead and the Dasher reels, and a cracked column breaks
        /// under it. This is the player's to use: bait the charge with a column between you, and
        /// it is stunned for longer than any finisher's flinch. Breaking the cover is the price.
        /// </summary>
        void TryRushImpact(Collision2D c)
        {
            if (Kind != EnemyKind.Dasher || !DasherDashing) return;
            var other = c.collider;
            if (other == null || other.isTrigger || other.attachedRigidbody != null) return;

            var column = other.GetComponent<Column>();
            if (column != null && column.Cracked) column.Break();

            var at = c.contactCount > 0 ? c.GetContact(0).point : (Vector2)transform.position;
            Spr.Flash(at, 0.7f, new Color(0.85f, 0.8f, 0.7f), 0.25f);

            // A reel that punches through armour - the wall does not care about a shield. Flinch
            // routes a Dasher into its Evading beat, which it plays once the stun is over.
            Flinch(Tuning.Steering.DasherWallStunSeconds, punchesThroughArmour: true);
            _dasherShyUntil = Time.time + Tuning.Steering.DasherShySeconds;
        }

        void Update()
        {
            if (_health.IsDead || _target == null || _fellIn) return;


            // REELING: no acting and no steering, for every kind. Returned from before the
            // per-kind dispatch rather than checked inside each one, so a kind added later cannot
            // forget it - the whole value of a flinch is that the attack does not happen, and one
            // path that kept running would be one enemy that ignored it.
            TickFlinch();
            if (Flinched) return;
            TickRecover();

            // UNAWARE or startled: nothing is acted on (see the Idle partial).
            if (TickIdle()) return;

            if (Kind == EnemyKind.Ranged) { UpdateRanged(); return; }
            if (Kind == EnemyKind.Bomb) { UpdateBomb(); return; }
            if (Kind == EnemyKind.Turret) { UpdateTurret(); return; }
            if (Kind == EnemyKind.Dasher) { UpdateDasher(); return; }
            if (Kind == EnemyKind.Gargoyle) { UpdateGargoyle(); return; }
            if (Kind == EnemyKind.Booster) { UpdateBooster(); return; }
            if (Kind == EnemyKind.Bubbles) { UpdateBubbles(); return; }
            if (Kind == EnemyKind.Mortar) { UpdateMortar(); return; }

            // Chaser from here down - every other kind splits off into its own path above.
            UpdateChaser();
        }

        /// <summary>
        /// The plain melee presser: close, WIND UP, swing, recover.
        ///
        /// The wind-up is new. It used to deal damage on the frame its cooldown expired, with
        /// nothing on screen between "walking at you" and "you are hurt" - which made it the one
        /// kind with nothing to interrupt, and a flinch that cannot deny anything is not a
        /// mechanic. Short, because a chaser that telegraphs slowly stops being pressure.
        /// </summary>
        void UpdateChaser()
        {
            float dt = Time.deltaTime;

            if (_telegraphTimer > 0f)
            {
                // Unlike the ranged bolt this does NOT freeze its aim: a melee swing lands where
                // the body is, and a chaser that committed to a stale point would whiff every time
                // the player stepped aside - which is the ranged enemy's job, not this one's.
                if (_targetHealth == null || _targetHealth.IsDead) { CancelTelegraph(); return; }

                _telegraphTimer -= dt;
                if (_telegraphTimer <= 0f) SwingChaser();
                return;
            }

            if (Recovering) return;

            _cooldown -= dt * AttackTempo;
            float dist = Vector2.Distance(transform.position, _target.position);
            if (dist <= AttackRange && _cooldown <= 0f && _targetHealth != null && !_targetHealth.IsDead)
                BeginChaserTelegraph();
        }

        /// <summary>
        /// When (Time.time) the Chaser last swung, when its elite riposte is due, and when one
        /// last landed - read by <see cref="ChaserVisual"/>, which shows the strike, the vein left
        /// lit and the backhand from them. Plain timestamps, nothing gameplay reads; a domain
        /// reload resetting them only drops one frame of art.
        /// </summary>
        [System.NonSerialized] public float SwungAt = -10f, RiposteDueAt = -10f, RiposteAt = -10f;

        void BeginChaserTelegraph()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Growl, transform.position);
            // The floor-scaled wind-up (EnemyFactory, FloorDifficulty.Telegraph), as every kind's.
            _telegraphTimer = TelegraphDuration;
        }

        void SwingChaser()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Swipe, transform.position);
            CancelTelegraph();
            _cooldown = AttackInterval;
            SwungAt = Time.time;

            // ELITE - Riposte: the swing comes back once more, immediately and harder. Scheduled
            // rather than resolved here, so the second hit is a SEPARATE beat the player can step
            // out of - a double hit landing on one frame is just a bigger number, not a pattern.
            if (ElitePattern == ElitePattern.Riposte && ElitePatternDue())
            {
                Invoke(nameof(Riposte), Tuning.Enemy.RiposteDelay);
                RiposteDueAt = Time.time + Tuning.Enemy.RiposteDelay;
            }

            // The beat happens whether or not the swing connects, which is the point: a denied
            // chaser and a whiffing one and a landing one all read the same, so the shape means
            // "it attacked" rather than "it hit you".
            BeginRecover(Tuning.Enemy.ChaserRecoverSeconds);

            LandMelee(1f);
        }

        /// <summary>The second half of an elite's Riposte. Guarded on every way the world can have
        /// changed in the 0.28s since it was scheduled - dead, flinched, or the player gone.</summary>
        void Riposte()
        {
            if (_health == null || _health.IsDead || Flinched) return;
            RiposteAt = Time.time;
            LandMelee(Tuning.Enemy.RiposteDamageMul);
            Spr.Flash(transform.position, 0.8f, new Color(1f, 0.75f, 0.35f), 0.2f, false);
        }

        /// <summary>One melee hit. Shared by the ordinary swing and the elite's Riposte so the two
        /// cannot drift - the riposte is the same strike at a multiplier, not a second one.</summary>
        void LandMelee(float damageMul)
        {
            if (_targetHealth == null || _targetHealth.IsDead || _target == null) return;
            if (Vector2.Distance(transform.position, _target.position) > AttackRange) return;
            // A spire's bubble wall stops a strike across it, as it stops the player's own.
            if (Spire.BubbleSeparates(transform.position, _target.position)) return;

            // Checked BEFORE Take, not inside it - a parry's counter-strike is the attacker's
            // problem to trigger, not Health's, and a melee hit has nothing further to do
            // once negated (unlike a projectile, which also reflects).
            if (_targetController != null && _targetController.TryParry(transform.position))
                return;

            var dir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
            _targetHealth.Take(new DamageInfo(Damage * damageMul, ElementType.Earth, gameObject)
            {
                Knockback = dir * AttackKnockback,
            });
            Spr.Flash(_target.position, 0.6f, new Color(1f, 0.25f, 0.3f), 0.18f, false);
        }

        void UpdateRanged()
        {
            float dt = Time.deltaTime;

            if (_telegraphTimer > 0f)
            {
                // The target dying mid-telegraph cancels the shot rather than firing at nothing -
                // there is no body left for the bolt to threaten.
                if (_targetHealth == null || _targetHealth.IsDead) { CancelTelegraph(); return; }

                _telegraphTimer -= dt;
                UpdateTelegraphVisual();
                if (_telegraphTimer <= 0f) Fire();
                return;
            }

            // Giving ground after a shot is ORDINARY behaviour, not a flinch response - see
            // Tuning.Enemy.RangedKiteSeconds. It is what a denied one routes into early.
            if (Recovering) return;

            _cooldown -= dt * AttackTempo;
            float dist = Vector2.Distance(transform.position, _target.position);
            if (dist <= AttackRange && _cooldown <= 0f && _targetHealth != null && !_targetHealth.IsDead)
                BeginTelegraph();
        }

        void BeginTelegraph()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Charge, transform.position);
            // Frozen once, here, and never touched again until it fires - the whole point of the
            // window is that the player is dodging a KNOWN line, not a re-aiming turret.
            // Aimed FROM the muzzle - the gland's sting, over the Stinger's back - so the line,
            // the flash and the bolt leave where the player is looking and still meet the target.
            _aimsLeft = _target.position.x < transform.position.x;
            _aimDir = ((Vector2)_target.position - RangedMuzzle).normalized;
            _telegraphTimer = TelegraphDuration;

            _telegraphVisual = new GameObject("ranged.telegraph");
            var sr = _telegraphVisual.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Capsule;
            sr.sortingOrder = SortingOrders.Telegraph;
            _telegraphSr = sr;
            UpdateTelegraphVisual();
        }

        void UpdateTelegraphVisual()
        {
            if (_telegraphVisual == null) return;

            float length = AttackRange;
            float angle = Mathf.Atan2(_aimDir.y, _aimDir.x) * Mathf.Rad2Deg - 90f;
            _telegraphVisual.transform.position = (Vector3)(RangedMuzzle + _aimDir * (length * 0.5f));
            _telegraphVisual.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _telegraphVisual.transform.localScale = new Vector3(Tuning.Enemy.RangedTelegraphWidth, length, 1f);

            // Grows from near-invisible to solid as the shot approaches, echoing the AoE
            // Charger's own fill-on-telegraph read.
            float t = 1f - Mathf.Clamp01(_telegraphTimer / TelegraphDuration);
            _telegraphSr.color = Murk(new Color(1f, 0.55f, 0.15f, Mathf.Lerp(0.08f, 0.75f, t)));
        }

        void CancelTelegraph()
        {
            _telegraphTimer = 0f;
            if (_telegraphVisual != null) Destroy(_telegraphVisual);
            _telegraphVisual = null;
            _telegraphSr = null;
        }

        /// <summary>When (Time.time) the Ranged last loosed - RangedVisual's recoil frame.</summary>
        [System.NonSerialized] public float FiredAt = -10f;

        bool _aimsLeft;

        /// <summary>Which way the current (or last) shot was aimed - the drawn Stinger holds this
        /// facing from the lock to the shot, so its mirrored muzzle and the picture agree.</summary>
        public bool AimsLeft => _aimsLeft;

        /// <summary>Where a Ranged's shot leaves: the sting's tip on its locked frame (RangedArt).</summary>
        public Vector2 RangedMuzzle => (Vector2)transform.position + RangedArt.MuzzleOffset(Elite, _aimsLeft);

        void Fire()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Zap, transform.position);
            FiredAt = Time.time;
            BeginRecover(Tuning.Enemy.RangedKiteSeconds);
            CancelTelegraph();
            _cooldown = Tuning.Enemy.RangedAttackInterval;

            // ELITE - Volley: a spread instead of a single bolt. Fanned around the SAME frozen
            // aim the telegraph drew, so the line the player read is still the middle of what
            // arrives - a volley aimed somewhere else would make the telegraph a lie.
            int bolts = ElitePattern == ElitePattern.Volley && ElitePatternDue()
                ? Tuning.Enemy.VolleyBolts : 1;

            for (int i = 0; i < bolts; i++)
            {
                var dir = _aimDir;
                if (bolts > 1)
                {
                    float t = bolts == 1 ? 0f : i / (float)(bolts - 1) - 0.5f;
                    dir = (Quaternion.Euler(0f, 0f, t * Tuning.Enemy.VolleySpreadDegrees) * dir).normalized;
                }
                EnemyProjectile.Launch(gameObject, _target, _targetHealth, RangedMuzzle, dir,
                    Damage, AttackKnockback, ProjectileSpeed, ProjectileLifetime);
            }
            Spr.Flash(RangedMuzzle, bolts > 1 ? 0.55f : 0.35f,
                      new Color(1f, 0.55f, 0.15f), 0.15f, false);
        }

        // ---- Mortar: keep away, plant, lob a shell at where the player stood ----

        /// <summary>
        /// Runs from the player inside PreferredMinRange, walks in only from past AttackRange,
        /// and drifts sideways in between. Planted while winding up - the one moment it stands
        /// still, and the moment to catch it.
        ///
        /// A straight "away from the player" vector pinned it into the first wall or corner it
        /// backed into, where it stood still and was simply a Ranged enemy with worse aim. It
        /// flees through the shared Flee (toward open floor, breaking out along a wall), walks in
        /// down the route, and drifts through Strafe (which turns round at a wall).
        /// </summary>
        Vector2 MortarSteer(Vector2 to, float dist, float speed)
        {
            // Planted for everything it does - a wind-up, a barrage still firing, the flame.
            if (MortarPlanted) return Vector2.zero;

            if (dist < PreferredMinRange) return Flee(to) * speed;
            if (dist > AttackRange)
                return Steer(NavField.RouteDirection(_rb.position, _target.position), dist) * speed;
            return Strafe(to) * (speed * Tuning.Enemy.MortarDriftSpeedFraction);
        }

        // ---- mortar state ----
        //
        // Three things it can do, each opened by the shared _telegraphTimer wind-up: LOB (one
        // shell), BARRAGE (the elite's three, spaced out), FLAME (the close-range answer to being
        // rushed). _mortarAction says which wind-up is running and, after it, which action is
        // live; plain fields, all of them, so a domain reload can't leave half a state behind.
        enum MortarAction { None, Lob, Barrage, Flame }
        MortarAction _mortarAction;
        int _barrageLeft;
        float _barrageTimer;
        float _flameTimer, _flameTick, _flameCooldown;
        Vector2 _flameDir;
        /// <summary>The drawn fire (MortarFlame): its licks through the wind-up, then the gout.</summary>
        MortarFlame _flame;

        /// <summary>Standing still: winding up, mid-barrage, or flaming.</summary>
        bool MortarPlanted => _telegraphTimer > 0f || _barrageLeft > 0 || _flameTimer > 0f;

        // ---- what the drawn Salamander reads (MortarVisual) ----
        /// <summary>When (Time.time) a shell last left its jaws.</summary>
        [System.NonSerialized] public float MortarLobbedAt = -10f;
        /// <summary>Rearing to lob: a lob's or a barrage's wind-up.</summary>
        public bool MortarWindingUp => _telegraphTimer > 0f && _mortarAction != MortarAction.Flame;
        /// <summary>Crouched to breathe fire: the flame's wind-up.</summary>
        public bool MortarFlameReady => _telegraphTimer > 0f && _mortarAction == MortarAction.Flame;
        public bool MortarFlaming => _flameTimer > 0f;
        /// <summary>Between a barrage's shells: it stays reared.</summary>
        public bool MortarBarraging => _barrageLeft > 0;
        public Vector2 MortarFlameDir => _flameDir;

        /// <summary>Where the drawn jaws are on <paramref name="frame"/>, facing whichever side
        /// <paramref name="toward"/> lies (the Salamander faces what it is aiming at).</summary>
        Vector2 MortarJaws(MortarFrame frame, float toward)
        {
            var m = MortarArt.MouthOffset(frame, Elite);
            if (toward < 0f) m.x = -m.x;
            return (Vector2)transform.position + m;
        }

        void UpdateMortar()
        {
            float dt = Time.deltaTime;
            _flameCooldown = Mathf.Max(0f, _flameCooldown - dt);

            bool targetAlive = _targetHealth != null && !_targetHealth.IsDead;

            if (_telegraphTimer > 0f)
            {
                if (!targetAlive) { EndMortarAction(); return; }

                _telegraphTimer -= dt;
                UpdateMortarTelegraphVisual();
                if (_telegraphTimer <= 0f) ResolveMortarWindup();
                return;
            }

            if (_flameTimer > 0f) { TickFlame(dt, targetAlive); return; }

            if (_barrageLeft > 0)
            {
                if (!targetAlive) { EndMortarAction(); return; }
                _barrageTimer -= dt;
                if (_barrageTimer <= 0f) FireBarrageShell();
                return;
            }

            if (Recovering || !targetAlive) return;

            // THE FLAME FIRST, but only for a player who has come to it. It is not an attack the
            // mortar goes looking for - it never closes in - and on its long cooldown it answers
            // one rush, then leaves the next to the lob.
            float dist = Vector2.Distance(transform.position, _target.position);
            if (dist <= Tuning.Enemy.MortarFlameTriggerRange && _flameCooldown <= 0f)
            {
                BeginMortarWindup(MortarAction.Flame, Tuning.Enemy.MortarFlameWindup);
                return;
            }

            _cooldown -= dt * AttackTempo;
            if (dist <= AttackRange && _cooldown <= 0f)
            {
                bool barrage = ElitePattern == ElitePattern.Barrage && Tuning.Enemy.MortarBarrageShells > 1;
                BeginMortarWindup(barrage ? MortarAction.Barrage : MortarAction.Lob, TelegraphDuration);
            }
        }

        void BeginMortarWindup(MortarAction action, float seconds)
        {
            _mortarAction = action;
            _telegraphTimer = seconds;

            // SPENT AT THE WIND-UP, NOT AT THE FLAME. A flinch that denies it still burns the
            // cooldown - otherwise denying the flame would only delay it by a flinch's length.
            if (action == MortarAction.Flame)
            {
                _flameCooldown = Tuning.Enemy.MortarFlameCooldown;
                _flameDir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
                if (_flameDir.sqrMagnitude < 0.0001f) _flameDir = Vector2.right;
            }

            // THE BODY IS THE TELL (the user's sign-off): the Salamander rears to lob and crouches
            // to breathe fire. The flame's wind-up also shows a few licks of fire along its aim, so
            // the way it is about to burn reads from above.
            if (action == MortarAction.Flame)
            {
                if (_flame == null) _flame = MortarFlame.Create();
                _flame.Tell = true;
            }
            UpdateMortarTelegraphVisual();
        }

        void UpdateMortarTelegraphVisual()
        {
            if (_mortarAction == MortarAction.Flame) PlaceFlame();
        }

        void ResolveMortarWindup()
        {
            var action = _mortarAction;
            CancelTelegraph();

            switch (action)
            {
                case MortarAction.Flame:
                    BeginFlame();
                    break;

                case MortarAction.Barrage:
                    // One wind-up, then the shells on their own clock. Each is deflectable on its
                    // own - how many a player turns is decided by their parry cooldown, not here.
                    _barrageLeft = Tuning.Enemy.MortarBarrageShells;
                    FireBarrageShell();
                    break;

                default:
                    LobShell();
                    BeginRecover(Tuning.Enemy.MortarRecoverSeconds);
                    _mortarAction = MortarAction.None;
                    break;
            }
        }

        void FireBarrageShell()
        {
            LobShell();
            _barrageLeft--;
            _barrageTimer = Tuning.Enemy.MortarBarrageSpacing;
            if (_barrageLeft <= 0)
            {
                _mortarAction = MortarAction.None;
                BeginRecover(Tuning.Enemy.MortarRecoverSeconds);
            }
        }

        void LobShell()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Lob, transform.position);
            _cooldown = AttackInterval;

            // Aimed HERE, at the moment this shell leaves, and never again - in a barrage each
            // shell takes the player's position at its own moment, so a player who keeps moving
            // leaves a trail of fuses behind them rather than standing in all three.
            var at = Arena.Clamp(_target.position, 0.3f);
            // From the drawn jaws, as the head comes up (the glob's own frame - MortarArt).
            var jaws = MortarJaws(MortarFrame.Lob, at.x - transform.position.x);
            MortarShell.Launch(jaws, at, transform.parent, _target, Damage, MortarArt.Accent);
            MortarLobbedAt = Time.time;
        }

        // ---- the flame ----

        void BeginFlame()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Flame, transform.position);
            _mortarAction = MortarAction.Flame;
            _flameTimer = Tuning.Enemy.MortarFlameSeconds;
            _flameTick = 0f;

            if (_flame == null) _flame = MortarFlame.Create();
            _flame.Tell = false;

            PlaceFlame();
        }

        void TickFlame(float dt, bool targetAlive)
        {
            _flameTimer -= dt;

            // TURNS toward the player, slowly - slower than the player can circle it at this
            // range, so the way out of a flame is round it, not back through it.
            if (targetAlive)
            {
                var want = ((Vector2)_target.position - (Vector2)transform.position).normalized;
                if (want.sqrMagnitude > 0.0001f)
                {
                    float cur = Mathf.Atan2(_flameDir.y, _flameDir.x) * Mathf.Rad2Deg;
                    float tgt = Mathf.Atan2(want.y, want.x) * Mathf.Rad2Deg;
                    float ang = Mathf.MoveTowardsAngle(cur, tgt, Tuning.Enemy.MortarFlameTurnDegPerSec * dt)
                              * Mathf.Deg2Rad;
                    _flameDir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                }
            }
            PlaceFlame();

            // Ticks, not per frame - a DamageInfo every frame would be a damage number every
            // frame, and hit reactions on the player stacking at 60Hz.
            _flameTick -= dt;
            if (_flameTick <= 0f)
            {
                _flameTick += Tuning.Enemy.MortarFlameTickSeconds;
                if (targetAlive && InFlame(_target.position)
                    && !Spire.BubbleSeparates(transform.position, _target.position))
                {
                    var dir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
                    _targetHealth.Take(new DamageInfo(
                        Damage * Tuning.Enemy.MortarFlameDpsMul * Tuning.Enemy.MortarFlameTickSeconds,
                        ElementType.Fire, gameObject)
                    {
                        Knockback = dir * AttackKnockback,
                    });
                }
            }

            if (_flameTimer <= 0f)
            {
                EndMortarAction();
                BeginRecover(Tuning.Enemy.MortarRecoverSeconds);
            }
        }

        bool InFlame(Vector2 p)
        {
            var to = p - (Vector2)transform.position;
            float d = to.magnitude;
            if (d > Tuning.Enemy.MortarFlameRange) return false;
            if (d < 0.3f) return true;   // point blank is inside whatever way it faces
            return Vector2.Angle(_flameDir, to) <= Tuning.Enemy.MortarFlameHalfAngle;
        }

        void PlaceFlame()
        {
            if (_flame == null) return;
            _flame.Place(MortarJaws(MortarFrame.Breath0, _flameDir.x), _flameDir);
        }

        /// <summary>
        /// Whatever the mortar was in the middle of - wind-up, barrage, flame - stops. Called on a
        /// flinch (the whole value of which is that the attack doesn't happen), on the target
        /// dying, and when the body goes away. Shells already in the air are their own objects and
        /// still land.
        /// </summary>
        void EndMortarAction()
        {
            CancelTelegraph();
            _mortarAction = MortarAction.None;
            _barrageLeft = 0;
            _flameTimer = 0f;
            if (_flame != null) Destroy(_flame.gameObject);
            _flame = null;
        }

        // ---- Bomb: chase, arm within AttackRange, telegraph, then go off ----

        // ---- the Brimstone Heart's sweep (the user's design, 2026-10-18; Tuning.Enemy.Bomb*) ----
        //
        // Above BombCriticalFraction of its health it fights at arm's length: a wind-up on the
        // shared telegraph timer, then BombFireballs thrown straight out of the heart one after
        // another, swept across a fan toward where the player stood when the sweep began. Under the
        // fraction it rushes in and arms the self-destruct, as it always did - a sweep under way
        // finishes first. An elite picks one of four sweeps at random: either way across, or there
        // and back starting from either side (the user's call).

        bool _bombArming;
        int _bombShotsLeft, _bombShot, _bombPattern;
        float _bombShotTimer, _bombSpent;
        bool _bombAimsLeft, _bombReverseNext;
        Vector2 _bombAim;

        /// <summary>When (Time.time) the Bomb last threw a fireball - BombVisual's pulse.</summary>
        [System.NonSerialized] public float BombThrewAt = -10f;

        /// <summary>Under BombCriticalFraction of its health: the self-destruct rush.</summary>
        public bool BombCritical => _health != null && _health.Current <= _health.Max * Tuning.Enemy.BombCriticalFraction;
        /// <summary>The telegraph running is the self-destruct arming, not a sweep's wind-up.</summary>
        public bool BombArming => _bombArming && _telegraphTimer > 0f;
        public bool BombGathering => !_bombArming && _telegraphTimer > 0f;
        public bool BombSweeping => _bombShotsLeft > 0;
        public bool BombSpent => _bombSpent > 0f;
        /// <summary>Which way the current sweep was aimed - the drawn heart holds this facing.</summary>
        public bool BombAimsLeft => _bombAimsLeft;

        void UpdateBomb()
        {
            float dt = Time.deltaTime;
            if (_bombSpent > 0f) _bombSpent -= dt;

            // A sweep under way always finishes - even one the health just dropped under the
            // fraction during (the user's call).
            if (_bombShotsLeft > 0) { TickBombSweep(dt); return; }

            if (_telegraphTimer > 0f)
            {
                // The target dying mid-telegraph cancels it outright - same reasoning as the
                // ranged bolt, there is no body left to threaten. Merely LEAVING the circle does
                // NOT cancel the self-destruct: an armed bomb still goes off, it just may find
                // nobody standing in it by the time it does.
                if (_targetHealth == null || _targetHealth.IsDead) { CancelTelegraph(); return; }

                _telegraphTimer -= dt;
                if (_telegraphTimer <= 0f)
                {
                    if (_bombArming) Explode();
                    else BeginBombSweep();
                }
                return;
            }

            // A denied bomb has to give ground before it may arm again. Without this the
            // interrupt buys nothing at all - it re-arms on the spot, and the player has spent a
            // finisher to delay an explosion by a third of a second.
            if (Recovering || _bombSpent > 0f) return;
            if (_targetHealth == null || _targetHealth.IsDead) return;
            // The practice dummy is a Bomb with no damage: it neither throws nor explodes.
            if (Damage <= 0f) return;

            float dist = Vector2.Distance(transform.position, _target.position);
            if (BombCritical)
            {
                if (dist <= AttackRange) BeginBombTelegraph();
                return;
            }

            _cooldown -= dt * AttackTempo;
            if (_cooldown > 0f || dist > Tuning.Enemy.BombSweepTriggerRange) return;
            if (HazardQuery.Query(transform.position, _target.position, gameObject, _target.gameObject) == SightResult.Blocked)
                return;

            // The wind-up: the heart swelling. Scaled with depth the way the arming is (the
            // factory scaled TelegraphDuration off the arming's own base), never under the floor.
            _bombArming = false;
            _telegraphTimer = Mathf.Max(Tuning.Enemy.TelegraphMinSeconds,
                TelegraphDuration * (Tuning.Enemy.BombSweepWindup / Tuning.Enemy.BombTelegraphDuration));
        }

        void BeginBombTelegraph()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Hissing, transform.position);
            _bombArming = true;
            _telegraphTimer = TelegraphDuration;
        }

        void BeginBombSweep()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Flame, transform.position);
            CancelTelegraph();
            _bombAim = ((Vector2)_target.position - (Vector2)transform.position).normalized;
            if (_bombAim.sqrMagnitude < 0.01f) _bombAim = Vector2.left;
            _bombAimsLeft = _bombAim.x < 0f;
            // 0 one way, 1 the other, 2 there and back from the first side, 3 from the other.
            // A basic alternates; an elite rolls any of the four.
            _bombPattern = Elite ? Random.Range(0, 4) : (_bombReverseNext ? 1 : 0);
            _bombReverseNext = !_bombReverseNext;
            _bombShotsLeft = Tuning.Enemy.BombFireballs * (_bombPattern >= 2 ? 2 : 1);
            _bombShot = 0;
            _bombShotTimer = 0f;
        }

        void TickBombSweep(float dt)
        {
            _bombShotTimer -= dt;
            while (_bombShotTimer <= 0f && _bombShotsLeft > 0)
            {
                ThrowBombFireball(BombLane(_bombShot));
                _bombShot++;
                _bombShotsLeft--;
                _bombShotTimer += Tuning.Enemy.BombFireballEvery;
            }
            if (_bombShotsLeft == 0)
            {
                _bombSpent = Tuning.Enemy.BombSpentSeconds;
                _cooldown = AttackInterval;
            }
        }

        /// <summary>Which lane of the fan the <paramref name="shot"/>th fireball of this sweep
        /// takes, by the sweep's pattern.</summary>
        int BombLane(int shot)
        {
            int n = Tuning.Enemy.BombFireballs, pass = shot / n, k = shot % n;
            bool forward = _bombPattern switch
            {
                0 => true,
                1 => false,
                2 => pass == 0,
                _ => pass == 1,
            };
            return forward ? k : n - 1 - k;
        }

        void ThrowBombFireball(int lane)
        {
            int n = Tuning.Enemy.BombFireballs;
            float half = Tuning.Enemy.BombSweepDegrees * 0.5f, slice = Tuning.Enemy.BombSweepDegrees / n;
            var dir = (Vector2)(Quaternion.Euler(0f, 0f, -half + slice * (lane + 0.5f)) * _bombAim);
            float life = Tuning.Enemy.BombFireballReach / Tuning.Enemy.BombFireballSpeed;
            EnemyProjectile.Launch(gameObject, _target, _targetHealth, transform.position, dir,
                    Damage * Tuning.Enemy.BombFireballDamageFraction, AttackKnockback,
                    Tuning.Enemy.BombFireballSpeed, life)
                .Dress(BombArt.Fireball(), Tuning.Enemy.BombFireballRadius + 0.16f, ElementType.Fire);
            BombThrewAt = Time.time;
        }

        /// <summary>
        /// The timer ran out. This does not deal damage itself - it kills the bomb, and Health.Kill
        /// firing Died is what actually triggers DoExplosion (see OnDied). Routing the timer path
        /// through the same Died event the "killed by the player" path already uses means there is
        /// exactly one place the explosion happens, not two that could drift apart or double-fire.
        /// </summary>
        void Explode()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Explosion, transform.position);
            CancelTelegraph();
            _health.Kill();
        }

        /// <summary>
        /// The actual blast: damage if the target is still standing in it, a visual regardless.
        /// Reached from BOTH triggers - the timer (via Explode -> Health.Kill -> Died) and being
        /// killed by the player's own hit (Died firing directly) - so this is the one place either
        /// path ends up.
        /// </summary>
        void DoExplosion()
        {
            if (_targetHealth != null && !_targetHealth.IsDead)
            {
                float dist = Vector2.Distance(transform.position, _target.position);
                if (dist <= AttackRange &&
                    !Spire.BubbleSeparates(transform.position, _target.position) &&
                    (_targetController == null || !_targetController.TryParry(transform.position)))
                {
                    var dir = ((Vector2)_target.position - (Vector2)transform.position).normalized;
                    _targetHealth.Take(new DamageInfo(Damage, ElementType.Fire, gameObject)
                    {
                        Knockback = dir * AttackKnockback,
                    });
                }
            }

            Spr.Flash(transform.position, AttackRange, BombArt.Blast, 0.35f);
            FightMarks.Scorch(transform.position, AttackRange);
            // The shell comes apart (the user's call): its plates fly loose.
            ShellBurst.Spawn(transform.position, BombArt.Plate(EnemyLooks.Of(Kind)), transform.parent);

            // ELITE - Cluster: the blast leaves delayed shards where it stood, so the space it
            // denied stays denied for a moment after the body is gone.
            //
            // SPAWNED AS THEIR OWN OBJECTS, not scheduled on this one: the bomb is mid-Died and is
            // about to be destroyed, so an Invoke here would be cancelled before it ever fired.
            if (ElitePattern == ElitePattern.Cluster && _target != null)
                for (int i = 0; i < Tuning.Enemy.ClusterShards; i++)
                {
                    float ang = (i / (float)Tuning.Enemy.ClusterShards) * Mathf.PI * 2f
                              + Random.value * 0.6f;
                    var at = (Vector2)transform.position
                           + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Tuning.Enemy.ClusterScatter;
                    // Each shard is one of the elite's orbiting motes, flung out from the heart.
                    ClusterShard.Spawn(Core.Arena.NearestFloor(at, 0.4f), transform.parent, _target,
                                       Damage * Tuning.Enemy.ClusterDamageFraction,
                                       transform.position, BombArt.Mote(EnemyLooks.Of(Kind)));
                }
        }

        // ---- Turret: stands still, ticks damage into the player for as long as it can see them ----

        void UpdateTurret()
        {
            UpdateTurretHeadFacing();

            // Before the line-of-sight gate, deliberately: the mire is LOBBED, and a player
            // hiding from the beam behind a column is exactly who it is for.
            if (Elite) TickMire();

            var sight = _targetHealth != null && !_targetHealth.IsDead
                ? HazardQuery.Query(transform.position, _target.position, gameObject, _target.gameObject)
                : SightResult.Blocked;

            // A Column blocks outright, and a Blue force field switches the beam off exactly the
            // same way - the beam either reaches the player or it doesn't, there's no state where
            // it's "still there but does nothing" the way a melee hit can be.
            if (sight == SightResult.Blocked || sight == SightResult.Nulled)
            {
                EndBeam();
                _beamTick = 0f;

                // Losing the line drops the charge as well as the beam. Otherwise a player who
                // broke line of sight would be shot the instant they stepped back out, having
                // spent the cover time charging it for free.
                _turretCharge = 0f;
                return;
            }

            // Spun down after a denial, and it has to spin up again from cold - a turret cannot
            // gain space by moving, so this IS its reposition.
            if (Recovering) { EndBeam(); _turretCharge = 0f; return; }

            // THE CHARGE-UP. The beam used to simply be on whenever line of sight held, which made
            // the Turret the one enemy with nothing to interrupt: a continuous effect denies
            // nothing, so a flinch landing on it did nothing the player could see. Now there is a
            // moment before it engages, and that moment is what a Medium or Heavy takes away.
            if (_turretCharge < Tuning.Enemy.TurretChargeDuration)
            {
                _turretCharge += Time.deltaTime;
                return;
            }

            if (_beamVisual == null) BeginBeam();
            UpdateBeamVisual();

            _beamTick += Time.deltaTime;
            if (_beamTick < Tuning.Enemy.TurretTickInterval) return;
            _beamTick = 0f;

            // Checked before Take, same as the elite melee hit above - a parry answers the beam
            // by negating this tick, not by reflecting a continuous effect back down the line.
            if (_targetController != null && _targetController.TryParry(transform.position)) return;

            // Damage is carried as a per-second RATE for this kind (see EnemyFactory), the same
            // way StatusEffects carries burn/bleed as dps rather than a per-tick amount - so the
            // tick size follows the tick interval instead of needing its own tuned constant.
            // A Red force field on the line doubles it - the beam isn't a "projectile" (parry
            // can't reflect it) but it obeys force fields exactly like one.
            float dmg = Damage * Tuning.Enemy.TurretTickInterval;
            if (sight == SightResult.Amplified) dmg *= 2f;

            // ELITE - Overcharge: every few ticks the beam swells for a burst. Counted on the
            // TICK rather than on the charge, because a turret charges once and then beams
            // forever - keyed to the charge it would fire exactly one overcharge per floor.
            if (ElitePattern == ElitePattern.Overcharge && ElitePatternDue())
                _overchargeUntil = Time.time + Tuning.Enemy.OverchargeSeconds;
            if (Time.time < _overchargeUntil) dmg *= Tuning.Enemy.OverchargeDamageMul;
            _targetHealth.Take(new DamageInfo(dmg, ElementType.Air, gameObject));
        }

        /// <summary>
        /// The elite Turret's second move, on top of Overcharge: every Tuning.Enemy.MireInterval
        /// it winds up and lobs a shell at where the player stands, which lands as a wide patch of
        /// slowing ground. The clock holds while the turret is spun down after a denial; the
        /// cooldown is spent when the wind-up STARTS, so a flinch during it costs the lob.
        /// </summary>
        void TickMire()
        {
            if (_targetHealth == null || _targetHealth.IsDead) { EndMireWindup(); return; }

            if (_mireWindup > 0f)
            {
                _mireWindup -= Time.deltaTime;
                if (_mireWindup > 0f) { UpdateMireWindup(); return; }
                EndMireWindup();
                LobMire();
                return;
            }

            if (Recovering) return;

            // Staggered, so two elite turrets on one floor do not lob in step.
            if (!_mireClockStarted)
            {
                _mireClockStarted = true;
                _mireClock = Random.Range(0f, Tuning.Enemy.MireInterval * 0.5f);
            }
            _mireClock += Time.deltaTime;
            if (_mireClock < Tuning.Enemy.MireInterval) return;
            _mireClock = 0f;
            _mireWindup = Tuning.Enemy.MireWindup;
        }

        /// <summary>The tell: a glow in the look's accent swelling on the turret over the wind-up.</summary>
        void UpdateMireWindup()
        {
            if (_mireWindupSr == null)
            {
                var go = new GameObject("turret.mire");
                go.transform.SetParent(transform, false);
                _mireWindupSr = go.AddComponent<SpriteRenderer>();
                _mireWindupSr.sprite = Spr.Glow;
                _mireWindupSr.sortingOrder = SortingOrders.Telegraph;
            }
            float k = 1f - Mathf.Clamp01(_mireWindup / Tuning.Enemy.MireWindup);
            _mireWindupSr.transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.8f, k);
            var y = TurretArt.Accent(Look)[2];
            _mireWindupSr.color = new Color(y.r, y.g, y.b, Mathf.Lerp(0.2f, 0.8f, k));
        }

        void EndMireWindup()
        {
            _mireWindup = 0f;
            if (_mireWindupSr == null) return;
            Destroy(_mireWindupSr.gameObject);
            _mireWindupSr = null;
        }

        /// <summary>Aimed once, at the moment it leaves - the Mortar's rule (see LobShell).</summary>
        void LobMire()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Lob, transform.position);
            var at = Arena.Clamp(_target.position, 0.3f);
            // The storm's own (the user's call): a ball of static that lands as charged ground,
            // in the look's accent - the slow, its radius, its fuse and its parry unchanged.
            var accent = TurretArt.Accent(Look);
            MortarShell.LaunchMire(transform.position, at, transform.parent, _target, accent);
            Spr.Flash(transform.position, 0.45f, accent[2], 0.15f, false);
        }

        /// <summary>
        /// Turns the gun to face whoever it's shooting - the one complaint the original static
        /// pylon drew. Runs every frame regardless of charge/blocked/recovering state, same as the
        /// beam's own rotation isn't gated on those either, so the turret keeps visibly watching
        /// its target through a charge-up rather than snapping to face them only once the beam is
        /// live. Line of sight is deliberately NOT required - a turret tracking through a wall it
        /// can't yet shoot through is honest (it knows where the player is; it just can't hit them)
        /// rather than a body that goes blind the instant cover is broken.
        ///
        /// Same angle convention <c>UpdateBeamVisual</c> already uses (Atan2 - 90, since both the
        /// head's art and the beam's own capsule are authored pointing "up"), so a beam and the gun
        /// it's coming out of always agree about which way they're facing.
        /// </summary>
        void UpdateTurretHeadFacing()
        {
            if (_target != null)
            {
                float dx = _target.position.x - transform.position.x;
                if (Mathf.Abs(dx) > 0.15f) _turretFacesLeft = dx < 0f;
            }
            if (_turretHead == null || _target == null) return;
            var to = (Vector2)_target.position - (Vector2)transform.position;
            if (to.sqrMagnitude < 0.0001f) return;
            float angle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg - 90f;
            _turretHead.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        void BeginBeam()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Crackle, transform.position);
            // The beam is lightning from the eye (LightningArc) - the picture only; the damage and
            // the line of sight below are measured from the body's centre, as they always were.
            _beamArc = LightningArc.Create("turret.beam");
            _beamVisual = _beamArc.gameObject;
        }

        void UpdateBeamVisual()
        {
            if (_beamVisual == null) return;

            if (_beamArc == null) _beamArc = _beamVisual.GetComponent<LightningArc>();
            if (_beamArc == null) return;
            var eye = (Vector2)transform.position + TurretArt.IrisOffset((int)TurretFrame.Beam0, Elite, _turretFacesLeft);
            // A live beam has to look like it is doing something every frame it's on: the arc
            // re-rolls its jags, and an overcharge thickens it and throws forks.
            _beamArc.Set(eye, _target.position, TurretArt.Accent(Look), TurretOvercharged);
        }

        void EndBeam()
        {
            if (_beamVisual != null) Destroy(_beamVisual);
            _beamVisual = null;
            _beamArc = null;
        }

        // ---- Dasher: approach, line up, charge through, spent, retreat ----

        void UpdateDasher()
        {
            bool targetGone = _targetHealth == null || _targetHealth.IsDead;
            switch (_dasherPhase)
            {
                case DasherPhase.LineUpTell:
                case DasherPhase.Braced:
                case DasherPhase.Telegraph:
                    // No body left to threaten: whatever it was about to do is called off.
                    if (targetGone) { _dasherPhase = DasherPhase.Approach; return; }
                    _dasherPhaseTimer -= Time.deltaTime;
                    if (_dasherPhaseTimer > 0f) return;
                    if (_dasherPhase == DasherPhase.LineUpTell) BeginDash(DasherPhase.LineUp);
                    else if (_dasherPhase == DasherPhase.Braced) BeginDasherTelegraph();
                    else BeginDash(DasherPhase.Rushing);
                    break;

                case DasherPhase.LineUp:
                    if (!TickDash()) return;
                    _dasherPhase = DasherPhase.Braced;
                    _dasherPhaseTimer = Tuning.Enemy.DasherBracedSeconds;
                    break;

                case DasherPhase.Rushing:
                    if (!TickDash()) return;
                    // ELITE - Redouble: it turns and dashes back through instead of standing
                    // spent. It still tells, so the answer is the one the player already knows -
                    // they simply do not get the window.
                    if (!_dasherRedoubled && !targetGone && ElitePattern == ElitePattern.Redouble && ElitePatternDue())
                    {
                        _dasherRedoubled = true;
                        BeginDasherTelegraph();
                    }
                    else
                    {
                        _dasherPhase = DasherPhase.Spent;
                        _dasherPhaseTimer = Tuning.Enemy.DasherSpentSeconds;
                    }
                    break;

                case DasherPhase.Spent:
                    _dasherPhaseTimer -= Time.deltaTime;
                    if (_dasherPhaseTimer <= 0f) BeginEvade();
                    break;

                case DasherPhase.Evading:
                    _dasherPhaseTimer -= Time.deltaTime;
                    if (_dasherPhaseTimer <= 0f) _dasherPhase = DasherPhase.Approach;
                    break;

                default: // Approach
                    if (targetGone) return;
                    if (Vector2.Distance(transform.position, _target.position) <= Tuning.Enemy.DasherEngageRange
                        && (Time.time >= _dasherShyUntil || NavField.ClearLine(transform.position, _target.position)))
                        BeginDasherLineUp();
                    break;
            }
        }

        /// <summary>
        /// The line-up: a point level with the player on whichever axis the Dasher is already
        /// further along, 2-3 units out (the reference's brute dashed straight up to the player's
        /// height and stayed about two units to their side). Skipped when it is already lined up,
        /// or the point is off the floor or behind something - then it goes straight to the charge.
        /// </summary>
        void BeginDasherLineUp()
        {
            _dasherRedoubled = false;
            Vector2 pos = transform.position, player = _target.position, d = player - pos;
            Vector2 to;
            if (Mathf.Abs(d.x) >= Mathf.Abs(d.y))
                to = new Vector2(player.x - Mathf.Sign(d.x) * Mathf.Clamp(Mathf.Abs(d.x), Tuning.Enemy.DasherLineUpMin, Tuning.Enemy.DasherLineUpMax), player.y);
            else
                to = new Vector2(player.x, player.y - Mathf.Sign(d.y) * Mathf.Clamp(Mathf.Abs(d.y), Tuning.Enemy.DasherLineUpMin, Tuning.Enemy.DasherLineUpMax));

            if ((to - pos).magnitude < Tuning.Enemy.DasherLineUpSkip
                || !Arena.OnFloor(to, BodyRadius) || !NavField.ClearLine(pos, to))
            {
                BeginDasherTelegraph();
                return;
            }
            // Frozen here, as the charge's point is: the tell commits to a known line.
            _dasherRushTarget = to;
            _dasherDashDir = (to - pos).normalized;
            _dasherPhase = DasherPhase.LineUpTell;
            _dasherPhaseTimer = _dasherPhaseLength =
                Mathf.Max(Tuning.Enemy.TelegraphMinSeconds, TelegraphDuration * Tuning.Enemy.DasherLineUpTellShare);
        }

        void BeginDasherTelegraph()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Snarl, transform.position);
            // Frozen HERE, at the start of the tell, not when the charge fires - the player gets
            // the whole window watching a committed, known point, the same rule Ranged's own aim
            // lives by. A charge that kept re-aiming through the tell would just be a slower bolt.
            Vector2 pos = transform.position, player = _target.position;
            var d = player - pos;
            _dasherDashDir = d.sqrMagnitude > 1e-4f ? d.normalized : _dasherDashDir;
            _dasherRushTarget = player + _dasherDashDir * Tuning.Enemy.DasherRushPast;
            _dasherPhase = DasherPhase.Telegraph;
            _dasherPhaseTimer = _dasherPhaseLength = TelegraphDuration;
        }

        void BeginDash(DasherPhase dash)
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.SwingHeavy, transform.position);
            _dasherPhase = dash;
            _dasherPhaseTimer = Tuning.Enemy.DasherDashMaxSeconds;
            _dasherBit = false;
            _dasherLastPos = transform.position;
        }

        /// <summary>One frame of a dash: bites what it passed through since the last, and says
        /// whether the dash is over (its end reached, or the backstop run out).</summary>
        bool TickDash()
        {
            Vector2 pos = transform.position;
            TryDasherBite(_dasherLastPos, pos);
            _dasherLastPos = pos;
            _dasherPhaseTimer -= Time.deltaTime;
            if (_dasherPhaseTimer > 0f && Vector2.Dot(_dasherRushTarget - pos, _dasherDashDir) > 0.02f) return false;
            // Stops dead where the dash ends: the last step's speed carried it on ~0.2 into the brace.
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            return true;
        }

        /// <summary>
        /// A dash bites once whatever it passes through - measured along the whole step it took
        /// this frame, since at 15 u/s a frame carries it a quarter of a unit and a point test
        /// would let it jump over the player.
        /// </summary>
        void TryDasherBite(Vector2 from, Vector2 to)
        {
            if (_dasherBit || _targetHealth == null || _targetHealth.IsDead) return;
            Vector2 player = _target.position, seg = to - from;
            float t = seg.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(player - from, seg) / seg.sqrMagnitude) : 0f;
            if (Vector2.Distance(from + seg * t, player) > Tuning.Enemy.DasherBiteReach) return;
            _dasherBit = true;
            if (Spire.BubbleSeparates(to, player)) return;

            // Checked before Take, as every other melee hit in this file - a parry answers it.
            if (_targetController != null && _targetController.TryParry(transform.position)) return;

            var dir = (player - from).sqrMagnitude > 1e-4f ? (player - from).normalized : _dasherDashDir;
            _targetHealth.Take(new DamageInfo(Damage, ElementType.Air, gameObject)
            {
                Knockback = dir * AttackKnockback,
            });
            DasherBitAt = Time.time;
            Spr.Flash(player, 0.45f, new Color(0.75f, 0.95f, 0.25f), 0.14f, false);
        }

        void BeginEvade()
        {
            _dasherPhase = DasherPhase.Evading;
            _dasherPhaseTimer = Tuning.Enemy.DasherEvadeDuration;
        }

        // ---- Gargoyle: a stationary watcher that roots on sight; its elite flies and slams ----

        void UpdateGargoyle()
        {
            float dt = Time.deltaTime;

            if (_gargoyleFlying)
            {
                // Only the target dying cancels a flight already under way, the same commitment
                // rule every other telegraph in this file follows once it has started. Unlike
                // this kind's OWN telegraph below, losing line of sight mid-flight does NOT turn
                // it back - the point was frozen the moment it was last seen, so there is nothing
                // left for sight to keep confirming until it lands.
                if (_targetHealth == null || _targetHealth.IsDead) { _gargoyleFlying = false; return; }

                _gargoyleFlyTimer -= dt;
                var toFly = _gargoyleFlyTarget - (Vector2)transform.position;
                if (toFly.sqrMagnitude <= 0.04f || _gargoyleFlyTimer <= 0f)
                {
                    _gargoyleFlying = false;
                    BeginGargoyleTelegraph();
                }
                return;
            }

            if (_telegraphTimer > 0f)
            {
                // SIGHT-GATED, NOT PROXIMITY-GATED - the one rule that sets this family apart
                // from every other kind's telegraph. "After they see the player" is the trigger,
                // so losing sight has to be able to un-trigger it; Bomb/Ranged/Chaser only cancel
                // on the target's death because theirs commit on PROXIMITY, which does not
                // un-happen the same way sight does.
                if (_targetHealth == null || _targetHealth.IsDead) { CancelTelegraph(); return; }
                var sight = HazardQuery.Query(transform.position, _target.position, gameObject, _target.gameObject);
                if (sight == SightResult.Blocked || sight == SightResult.Nulled) { CancelTelegraph(); return; }

                _telegraphTimer -= dt;
                if (_telegraphTimer <= 0f) ResolveGargoyleAttack();
                return;
            }

            if (Recovering) return;

            if (_targetHealth == null || _targetHealth.IsDead) return;
            var sight2 = HazardQuery.Query(transform.position, _target.position, gameObject, _target.gameObject);
            if (sight2 == SightResult.Blocked || sight2 == SightResult.Nulled) return;

            // ELITE - Descent: rather than glowing in place, it closes the whole distance first -
            // EVERY cycle, not only its first sighting, which is what makes it a genuinely mobile
            // threat rather than a one-time entrance. Frozen HERE, the moment sight is confirmed,
            // the same "commit to a known point" rule Dasher's own charge and Ranged's own aim
            // already use - a flight that kept re-aiming mid-swoop would just be a slower way of
            // walking at you.
            if (Elite)
            {
                _gargoyleFlying = true;
                _gargoyleFlyFrom = transform.position;
                _gargoyleFlyTarget = _target.position;
                _gargoyleFlyTimer = Tuning.Enemy.GargoyleFlyMaxSeconds;
                return;
            }

            BeginGargoyleTelegraph();
        }

        void BeginGargoyleTelegraph()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Grind, transform.position);
            // No glow drawn round it: the Watcher's own face is the tell (GargoyleVisual - its eyes
            // kindle and its cracks fill with light; the user's pick over a drawn line of sight).
            // An elite's slam edge is cracked into the floor beside it (GargoyleCracks).
            _telegraphTimer = TelegraphDuration;
        }

        void ResolveGargoyleAttack()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Slam, transform.position);
            CancelTelegraph();
            GargoyleStruckAt = Time.time;
            FightMarks.Crack(transform.position, Elite ? 1.2f : 0.8f);

            if (Elite)
            {
                BeginRecover(Tuning.Enemy.GargoyleEliteRecoverSeconds);

                // The one hit this family lands, and it belongs to the elite alone - see
                // Tuning.Enemy's own note on why GargoyleDamage is read only from here.
                if (_targetHealth != null && !_targetHealth.IsDead)
                {
                    float dist = Vector2.Distance(transform.position, _target.position);
                    if (dist <= Tuning.Enemy.GargoyleAoeRadius &&
                        (_targetController == null || !_targetController.TryParry(transform.position)))
                    {
                        _targetHealth.Take(new DamageInfo(Damage, ElementType.Earth, gameObject));
                        StatusEffects.Get(_target.gameObject).ApplyPetrify(Tuning.Enemy.GargoylePetrifySeconds);
                    }
                }
                // The slam's own picture is its cracks bursting white-hot (GargoyleCracks).
                return;
            }

            BeginRecover(Tuning.Enemy.GargoyleRecoverSeconds);

            // The basic's root - unconditionally zero damage, per the family's own rule; nothing
            // here ever calls Health.Take. Range is checked at resolve time rather than
            // continuously, mirroring Bomb's own "merely leaving does not cancel it" reasoning -
            // sight, not distance, is what this kind's telegraph actually commits to.
            if (_targetHealth != null && !_targetHealth.IsDead &&
                Vector2.Distance(transform.position, _target.position) <= AttackRange &&
                (_targetController == null || !_targetController.TryParry(transform.position)))
            {
                StatusEffects.Get(_target.gameObject).ApplyPetrify(Tuning.Enemy.GargoylePetrifySeconds);
                Spr.Flash(_target.position, 0.5f, new Color(0.62f, 0.45f, 0.95f), 0.3f, false);
            }
        }

        // ---- Booster: the Green Lion, which roars an ally to power ----

        void UpdateBooster()
        {
            // A slot frees when its ally dies - the buff outlives the lion's own fate, so only the
            // RECIPIENT's death matters - and the lion recovers before it looks again (unless it is
            // mid-breath for the other slot: that roar still comes).
            bool freed = BoostedDied(ref _boosterTarget) | BoostedDied(ref _boosterTarget2);
            if (freed && _telegraphTimer <= 0f) BeginRecover(Tuning.Enemy.BoosterRecoverSeconds);
            if (BoosterHeld >= BoosterSlots) return;   // spent: every slot's ally lives

            if (_telegraphTimer > 0f)
            {
                // SIGHT-GATED per ally, the same family rule Gargoyle's own telegraph lives by: an
                // ally it can no longer see (or one that died) drops out of the roar; with none
                // left the breath is let go.
                if (!StillBoostable(_boosterCandidate)) _boosterCandidate = null;
                if (!StillBoostable(_boosterCandidate2)) _boosterCandidate2 = null;
                if (_boosterCandidate == null) { _boosterCandidate = _boosterCandidate2; _boosterCandidate2 = null; }
                if (_boosterCandidate == null) { CancelTelegraph(); return; }

                _telegraphTimer -= Time.deltaTime;
                if (_telegraphTimer <= 0f) ResolveBooster();
                return;
            }

            if (Recovering) return;

            int free = BoosterSlots - BoosterHeld;
            _boosterCandidate = FindBoosterCandidate(null);
            _boosterCandidate2 = free > 1 && _boosterCandidate != null ? FindBoosterCandidate(_boosterCandidate) : null;
            if (_boosterCandidate != null) _telegraphTimer = TelegraphDuration;
        }

        /// <summary>Clears a held ally that has died; true if it did.</summary>
        static bool BoostedDied(ref EnemyController held)
        {
            if (held == null) { held = null; return false; }
            var hp = held.GetComponent<Health>();
            if (hp != null && !hp.IsDead) return false;
            held = null;
            return true;
        }

        bool StillBoostable(EnemyController c)
        {
            if (c == null || c.Boosted) return false;
            var hp = c.GetComponent<Health>();
            if (hp == null || hp.IsDead) return false;
            var sight = HazardQuery.Query(transform.position, c.transform.position, gameObject, c.gameObject);
            return sight != SightResult.Blocked && sight != SightResult.Nulled;
        }

        /// <summary>
        /// Who the lion roars at (the user's calls, 2026-10-19): an ally it can see within
        /// BoosterPickRadius, NON-ELITES FIRST (an elite only when no basic is in reach), then the
        /// biggest wave BUDGET - the body's max health plus max armour, exactly what the wave paid
        /// for it (WaveComposer.Ehp, depth and tier included) - then the nearest. Null: it waits.
        ///
        /// NEVER another Booster (nothing to grant it) and NEVER a Gargoyle: Descent is gated on
        /// the Elite bool directly rather than on ElitePattern the way every other kind's pattern
        /// is (see ApplyBoosterBuff), so granting a common Gargoyle ElitePattern.Descent alone
        /// would silently do nothing. <paramref name="except"/> is an elite's first pick, when it
        /// looks for its second.
        /// </summary>
        EnemyController FindBoosterCandidate(EnemyController except)
        {
            EnemyController best = null;
            float bestBudget = 0f, bestDist = 0f;
            bool bestElite = false;
            foreach (var other in EnemyRegistry.All)
            {
                if (other == null || other == this || other == except) continue;
                if (other.Kind is EnemyKind.Booster or EnemyKind.Gargoyle || other.Boosted) continue;
                var hp = other.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                float dist = Vector2.Distance(transform.position, other.transform.position);
                if (dist > Tuning.Enemy.BoosterPickRadius) continue;

                var armor = other.GetComponent<EnemyArmor>();
                float budget = hp.Max + (armor != null ? armor.Max : 0f);
                if (best != null)
                {
                    if (other.Elite != bestElite) { if (other.Elite) continue; }
                    else if (budget < bestBudget - 0.001f) continue;
                    else if (budget <= bestBudget + 0.001f && dist >= bestDist) continue;
                }

                var sight = HazardQuery.Query(transform.position, other.transform.position, gameObject, other.gameObject);
                if (sight == SightResult.Blocked || sight == SightResult.Nulled) continue;

                best = other;
                bestBudget = budget;
                bestDist = dist;
                bestElite = other.Elite;
            }
            return best;
        }

        /// <summary>The breath is out: the roar lands on every ally still in it, at once.</summary>
        void ResolveBooster()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Roar, transform.position);
            CancelTelegraph();
            var a = Roar(_boosterCandidate);
            var b = Roar(_boosterCandidate2);
            _boosterCandidate = _boosterCandidate2 = null;

            if (a == null && b == null)
            {
                // Every ally died in the same frame the breath completed - rare, but real.
                // Recover before searching again rather than re-scanning on the very next frame.
                BeginRecover(Tuning.Enemy.BoosterRecoverSeconds);
                return;
            }
            BoosterRoaredAt = Time.time;
            BoosterRoaredA = a ?? b;
            BoosterRoaredB = a != null ? b : null;
        }

        EnemyController Roar(EnemyController c)
        {
            if (c == null || c.Boosted) return null;
            var hp = c.GetComponent<Health>();
            if (hp == null || hp.IsDead) return null;
            c.ApplyBoosterBuff();
            if (_boosterTarget == null) _boosterTarget = c;
            else _boosterTarget2 = c;
            return c;
        }

        // ---- Bubbles: the Drowned Bell, which blows a bubble round an ally ----

        void UpdateBubbles()
        {
            // Its ally's bubble ended - popped, or its wearer died: it recovers before it looks
            // again (unless it is mid-breath for its own bubble: that blow still comes).
            if (_bubbleTarget != null)
            {
                var targetHp = _bubbleTarget.GetComponent<Health>();
                if (targetHp == null || targetHp.IsDead || !_bubbleTarget.Bubbled)
                {
                    _bubbleTarget = null;
                    if (_telegraphTimer <= 0f) BeginRecover(Tuning.Enemy.BubblesRecoverSeconds);
                }
            }
            // Its OWN bubble popping is the same beat (the user's call): it recovers before it
            // blows again, so a Heavy on the bell buys more than the channel.
            if (_selfBubbleHeld && !Bubbled && _telegraphTimer <= 0f) BeginRecover(Tuning.Enemy.BubblesRecoverSeconds);
            _selfBubbleHeld = Bubbled;

            if (_telegraphTimer > 0f)
            {
                // SIGHT-GATED, the family rule: an ally it can no longer see (or one that died, or
                // was bubbled by another bell meanwhile) drops out of the blow; with nothing left
                // to blow for, the water is let go.
                if (!StillBubblable(_bubbleCandidate)) _bubbleCandidate = null;
                if (_bubbleSelfToo && Bubbled) _bubbleSelfToo = false;
                if (_bubbleCandidate == null && !_bubbleSelfToo) { CancelTelegraph(); return; }

                _telegraphTimer -= Time.deltaTime;
                if (_telegraphTimer <= 0f) ResolveBubbles();
                return;
            }

            if (Recovering) return;

            // An elite renews its own bubble once it is gone - with its next blow, or alone.
            bool needSelf = Elite && !Bubbled;
            var ally = _bubbleTarget == null ? FindBubbleCandidate() : null;
            if (ally == null && !needSelf) return;   // tending, or nobody to shield
            _bubbleCandidate = ally;
            _bubbleSelfToo = needSelf;
            _telegraphTimer = TelegraphDuration;
        }

        bool StillBubblable(EnemyController c)
        {
            if (c == null || c.Bubbled) return false;
            var hp = c.GetComponent<Health>();
            if (hp == null || hp.IsDead) return false;
            var sight = HazardQuery.Query(transform.position, c.transform.position, gameObject, c.gameObject);
            return sight != SightResult.Blocked && sight != SightResult.Nulled;
        }

        /// <summary>
        /// Who the bell shields (the user's call, 2026-10-19): the enemy NEAREST THE PLAYER that it
        /// can see, other than itself, any kind, not already bubbled - whatever is about to take the
        /// next swing. No range limit; sight-gated. Decided as it starts drawing water, never
        /// switched mid-breath.
        /// </summary>
        EnemyController FindBubbleCandidate()
        {
            if (_target == null) return null;
            Vector2 player = _target.position;
            EnemyController best = null;
            float bestDist = float.MaxValue;
            foreach (var other in EnemyRegistry.All)
            {
                if (other == null || other == this || other.Bubbled) continue;
                var hp = other.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                float dist = Vector2.Distance(player, other.transform.position);
                if (dist >= bestDist) continue;
                var sight = HazardQuery.Query(transform.position, other.transform.position, gameObject, other.gameObject);
                if (sight == SightResult.Blocked || sight == SightResult.Nulled) continue;
                bestDist = dist;
                best = other;
            }
            return best;
        }

        /// <summary>The blow: the bubble round the ally, and round itself too for an elite.</summary>
        void ResolveBubbles()
        {
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Bubbling, transform.position);
            CancelTelegraph();

            // ELITE: a two-wall bubble (see Tuning.Enemy's own note) - the same shield, more of it.
            int charges = Elite ? Tuning.Enemy.BubblesEliteCharges : 1;
            var accent = BubblesArt.AccentOf(Look);
            bool any = false;
            // Sight was held every frame of the breath; here it only has to be alive and unshielded.
            var allyHp = _bubbleCandidate != null ? _bubbleCandidate.GetComponent<Health>() : null;
            if (allyHp != null && !allyHp.IsDead && !_bubbleCandidate.Bubbled)
            {
                _bubbleCandidate.ApplyBubble(charges, accent, (Vector2)transform.position + BubblesArt.MouthOffset(Elite));
                _bubbleTarget = _bubbleCandidate;
                any = true;
            }
            if (_bubbleSelfToo && !Bubbled)
            {
                ApplyBubble(charges, accent, null);
                any = true;
            }

            if (any) BubblesBlewAt = Time.time;
            // Everything it blew for died in the same frame the channel completed - rare, but real.
            // Recover before searching again rather than re-scanning on the very next frame.
            else BeginRecover(Tuning.Enemy.BubblesRecoverSeconds);

            _bubbleCandidate = null;
            _bubbleSelfToo = false;
        }
    }
}
