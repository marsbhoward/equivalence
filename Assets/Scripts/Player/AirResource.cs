using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.Elements.Air;

namespace Convergence.Player
{
    /// <summary>
    /// AIR - Crit Scaling (speed / finesse).
    /// Crits build momentum, which raises attack speed and reach. Low per-hit damage, high
    /// frequency, squishy.
    ///
    /// DESIGN DECISION (flagged as a structural risk in the handoff): air's resource was
    /// purely outcome-driven (RNG crits) while the other three are player-driven. Fixed here
    /// by giving air a parallel player-driven lever: a consecutive-hit streak raises a CRIT
    /// FLOOR, so skilled uninterrupted aggression makes crits more likely rather than leaving
    /// the whole element to variance. Both the floor and momentum are capped so one lucky
    /// exchange cannot spiral.
    ///
    /// A MISS WIPES BOTH - streak and momentum. That is what pays for air being the fastest and
    /// longest-reaching element: the bonuses exist only while every swing connects, so the
    /// element rewards picking shots rather than flailing. Momentum still decays on its own
    /// timer too, which covers backing off rather than whiffing.
    /// </summary>
    public class AirResource : ElementalResource
    {
        public float Momentum;                  // 0..1, decays over time
        /// <summary>
        /// Crits still add momentum, but they are no longer the engine - see MomentumPerSecond.
        /// Kept because the crit is air's signature moment and it should pay something.
        /// </summary>
        public float MomentumPerCrit = T.MomentumPerCrit;

        /// <summary>
        /// MOMENTUM IS BUILT BY MOVING.
        ///
        /// It used to build on crits and break on a miss, which worked for a weapon that has to
        /// close in and swing. A thrown weapon auto-targets, so it effectively cannot whiff - the
        /// entire risk that defined the element evaporated the moment discs existed, and a player
        /// could plant themselves at range and hold max momentum for free.
        ///
        /// Movement is the condition a comfortable ranged player actually fails. It also makes
        /// air the exact inverse of earth on the same axis - one element pays you to stand still
        /// and the other pays you to never stop - so the pair reads as a designed choice rather
        /// than two unrelated meters.
        /// </summary>
        public float MomentumPerSecond = T.MomentumPerSecond;

        /// <summary>Bleeds always, so momentum needs NET movement rather than one dash.</summary>
        public float MomentumDecay = T.MomentumDecay;

        /// <summary>Additional bleed while standing still. Roughly four seconds from full to nothing.</summary>
        public float MomentumDecayStill = T.MomentumDecayStill;

        bool _moving;

        /// <summary>
        /// Whether momentum should be building this frame.
        ///
        /// AN ANIMATION LOCK IS NOT STANDING STILL. A finisher takes the stick away for up to
        /// 0.55s, and reading that as stillness charged air 0.14 momentum for a swing it had no
        /// say in - 1.9 SECONDS OF RUNNING to undo 0.55 seconds of being held, and the same
        /// multiple at every attack speed. Every element pays a tempo cost for a finisher; air
        /// was paying that cost twice, once in time and again in the meter that is the entire
        /// reason to play it, which made the class feel like it should not be attacking at all.
        ///
        /// So while the lock is on, momentum follows what the player is ASKING for rather than
        /// what the body is allowed to do. That is not a handout: it still requires holding a
        /// direction, and letting go mid-finisher bleeds exactly as it always did. It is the
        /// difference between "you stopped" and "we stopped you".
        ///
        /// It matters most under Rooted, the ledger cost that extends the lock to basics. Freezing
        /// the meter instead of reading intent would have left an air character holding that cost
        /// with a meter that could never move, which is not a cost, it is an off switch.
        /// </summary>
        bool Building => Player != null && Player.AttackLocked ? Player.MoveIntent : _moving;

        public int HitStreak { get; private set; }
        public const int StreakCap = T.StreakCap;
        public float BaseCrit = T.BaseCrit;

        /// <summary>The player-driven half: +4% crit per consecutive hit, capped at +32%.</summary>
        public float CritFloor => Mathf.Min(StreakCap, HitStreak)
                                  * (Ledger != null ? Ledger.CritPerHit(T.CritPerStreak) : T.CritPerStreak);

        /// <summary>Doldrums: a hit taken resets the streak.</summary>
        public void BreakStreak() => HitStreak = 0;

        /// <summary>Seconds since momentum last built - Tailwind holds it a moment before it fades.</summary>
        float _stoppedFor;

        public override ElementType Element => ElementType.Air;
        public override float CritChance => Mathf.Min(0.75f, BaseCrit + CritFloor);

        // The glass cannon as head starts (Tuning.Elements): lighter hits, faster swings, harder
        // crits - Air reaches the speed and crit caps sooner, never higher ones.
        public override float CritDamagePoints => T.CritDamagePoints;
        public override float DamagePoints => T.DamagePoints + (Gusting ? T.GustDamagePoints * Scale : 0f);
        public override float AttackSpeedPoints => T.AttackSpeedPoints + Momentum * T.AttackSpeedPointsPerMomentum;

        /// <summary>
        /// Momentum moves the CHARACTER, not the blade.
        ///
        /// It used to add up to +1.6 units of attack range, which was quietly the strongest thing
        /// in the game: reach is safety, and at full momentum air out-ranged everything while
        /// also swinging twice as fast, from outside where anything could answer. Legs instead of
        /// reach keeps the "fast and slippery" identity and pays for it honestly - you still have
        /// to close to normal distance to land a hit.
        /// </summary>
        public override float MoveSpeedPoints
            => Momentum * T.MoveSpeedPointsPerMomentum + (Gusting ? T.GustMoveSpeedPoints : 0f);

        public override void OnMoved(bool moving) => _moving = moving;

        void Update()
        {
            float dt = Time.deltaTime;
            _stoppedFor = Building ? 0f : _stoppedFor + dt;
            float fade = (MomentumDecay + MomentumDecayStill) * Decay
                       * (Ledger != null ? Ledger.MomentumFadeMul : 1f);
            if (Ledger != null && _stoppedFor < Ledger.MomentumHoldSeconds) fade = 0f;   // Tailwind II
            float delta = Building
                ? MomentumPerSecond * Gain - MomentumDecay * Decay
                : -fade;

            Momentum = Mathf.Clamp01(Momentum + delta * dt);

            TickGust(dt);
        }

        /// <summary>
        /// Ticks the gust timer and drives the outline's blink as it nears expiry - the same
        /// "the end matters as much as the start" tell water's own surge already uses, but as a
        /// held silhouette rather than a one-shot pulse since this buff has a visible duration to
        /// track, not just a moment to mark.
        /// </summary>
        void TickGust(float dt)
        {
            if (_gustRemaining <= 0f) return;

            _gustRemaining -= dt;
            if (_gustRemaining <= 0f)
            {
                _gustRemaining = 0f;
                _glow?.SetShown(false);
                Spr.Pulse(transform, 0.8f, GustOutlineColor, 0.25f, true, 0.5f);
                return;
            }

            if (_gustRemaining > GustBlinkWindow)
            {
                _glow?.SetShown(true);
                return;
            }

            // Blink: the interval SHRINKS as expiry nears, so the outline flickers faster right
            // before it drops rather than at one flat rate for the whole warning window.
            _gustBlinkTimer -= dt;
            if (_gustBlinkTimer > 0f) return;

            float urgency = 1f - Mathf.Clamp01(_gustRemaining / GustBlinkWindow);
            _gustBlinkTimer = Mathf.Lerp(GustBlinkInterval, GustBlinkInterval * 0.35f, urgency);
            _glow?.Toggle();
        }

        public override void OnHitLanded(Health target, DamageInfo info)
        {
            HitStreak++;

            // The streak is air's player-driven half, and it was the half with no tell at all -
            // the crit floor just quietly rose. Mark the cap, the point past which it stops paying.
            if (HitStreak == StreakCap)
                Spr.Pulse(transform, 1.3f, new Color(0.75f, 0.95f, 1f), 0.4f, true, 1.5f);

            if (info.Crit)
            {
                Momentum = Mathf.Clamp01(Momentum + MomentumPerCrit * Gain);
                Spr.Flash(target.transform.position, 0.9f, Color.white, 0.18f, false);
                Spr.Pulse(transform, 0.5f + Momentum * 0.6f,
                          new Color(0.7f, 0.9f, 1f, 0.7f), 0.2f, true, 1.2f);
            }
        }

        /// <summary>
        /// A whiff costs the streak outright and HALF the momentum.
        ///
        /// It used to cost everything, which was correct while crits were the only way to build:
        /// the meter was earned a swing at a time and lost the same way. Momentum is now earned
        /// by running, so wiping it on one stray swing would delete several seconds of kiting for
        /// a mistake that had nothing to do with how it was built - a punishment aimed at the
        /// wrong verb. Halving it still makes flailing expensive without making it catastrophic,
        /// and the streak, which IS about connecting, still goes entirely.
        /// </summary>
        public override void OnAttackMissed()
        {
            float lost = Momentum * 0.5f;
            int streak = HitStreak;

            Momentum = Mathf.Max(0f, Momentum - lost);
            HitStreak = 0;

            // Nothing worth losing was on the table - stay quiet rather than crying wolf on
            // every stray swing, which is how a cue stops being read at all.
            if (lost < 0.05f && streak < 3) return;

            Spr.Pulse(transform, 0.7f + lost * 1.1f,
                      new Color(0.5f, 0.55f, 0.62f, 0.55f + lost * 0.4f),
                      0.25f + lost * 0.2f, true, 0.4f);
        }

        // ---- GUST: the max-momentum self-buff, replacing the old cone ----

        /// <summary>How long a gust lasts, once spent.</summary>
        public float GustDuration = T.GustSeconds;

        /// <summary>Flat attack-range bonus while gusting - reach the element otherwise gave up
        /// (see <see cref="MoveSpeedMultiplier"/>'s own comment on why momentum moves legs, not
        /// the blade) comes back, but only for a spent, timed window instead of a passive stat.</summary>
        public float GustRangeBonus = T.GustRangeBonus;

        /// <summary>Lifesteal while gusting - it joins the ONE lifesteal pool, capped with every
        /// other source (StatCurves.Lifesteal), rather than healing on its own.</summary>
        public override float LifestealBonus => Gusting ? T.GustLifesteal : 0f;

        /// <summary>How long before expiry the outline starts blinking - the same "the end matters
        /// as much as the start" reasoning water's own surge tell already uses.</summary>
        public float GustBlinkWindow = 1.5f;

        /// <summary>Seconds between blinks once inside <see cref="GustBlinkWindow"/>.</summary>
        public float GustBlinkInterval = 0.2f;

        float _gustRemaining;
        float _gustBlinkTimer;
        AirGustGlow _glow;

        public bool Gusting => _gustRemaining > 0f;
        public float GustRemaining => _gustRemaining;

        public override float AttackRangeBonus => Gusting ? GustRangeBonus : 0f;

        /// <summary>Full momentum, whichever ability is chosen - the board picks WHAT the meter is
        /// spent on, not a second meter to fill.</summary>
        public override bool CanRelease => Momentum >= 0.999f;

        /// <summary>
        /// Cash in full momentum for a timed self-buff instead of firing it downrange.
        ///
        /// The old cone made distance air's payoff; this makes COMMITMENT the payoff instead -
        /// spend the meter and get faster, longer-reaching, and sustaining for a few seconds,
        /// which rewards diving into the fight the meter's own momentum already asks the player
        /// to keep moving toward, rather than answering it with a ranged poke.
        /// </summary>
        public override void Release()
        {
            if (!CanRelease) return;
            if (UseSecondAbility) { Squall(); return; }

            _gustRemaining = GustDuration;
            _gustBlinkTimer = 0f;
            Momentum = 0f;
            HitStreak = 0;

            _glow ??= AirGustGlow.Attach(transform);
            _glow.SetShown(true);

            Spr.Pulse(transform, 1.1f, GustOutlineColor, 0.35f, true, 1.4f);
            Spr.Flash(transform.position, 1.1f, Color.white, 0.22f);
        }

        /// <summary>Yellow, per the ability's own tell - kept as one shared constant so the
        /// activation pulse, the blink, and the outline are all visibly the same colour.</summary>
        static readonly Color GustOutlineColor = new Color(1f, 0.85f, 0.2f);

        /// <summary>
        /// The release once more, spending nothing (Twin Spark, Wellspring): Gust's window runs on
        /// by the repeat's share of itself, or Squall's vortices are scattered again at its strength.
        /// </summary>
        public override void ReleaseAgain()
        {
            if (UseSecondAbility) { SquallAgain(); return; }
            _gustRemaining = Mathf.Max(0f, _gustRemaining) + GustDuration * RepeatStrength;
            _glow ??= AirGustGlow.Attach(transform);
            _glow.SetShown(true);
            Spr.Pulse(transform, 1f, GustOutlineColor, 0.3f, true, 1.2f);
        }

        public override void Spill(float fraction01) => Momentum = Mathf.Max(0f, Momentum - fraction01);

        public override void Refund(float fraction01)
            => Momentum = Mathf.Clamp01(Momentum + fraction01);

        public override float Fill01 => Momentum;

        // ---- SQUALL: the second ability, chosen on the board in place of Gust ----

        /// <summary>How many vortices a squall scatters.</summary>
        public int VortexCount = T.VortexCount;

        /// <summary>
        /// Radius of the ring they land in, around the player - the same shape as Fire's
        /// <c>EruptSpread</c>, placed the same jittered way for the same reason: this is area
        /// denial the player chooses where to stand inside of, not an aimed hit.
        /// </summary>
        public float VortexSpread = T.VortexSpread;

        public float VortexRadius = T.VortexRadius;
        public float VortexPullForce = T.VortexPullForce;

        /// <summary>Damage per second at a vortex's own centre - the worst spot to be caught - in
        /// HIT UNITS (multiples of the player's own basic hit, Tuning.Elements).</summary>
        public float VortexNearHitUnits = T.VortexNearHitUnitsPerSecond;

        /// <summary>Damage per second at a vortex's rim, in hit units.</summary>
        public float VortexFarHitUnits = T.VortexFarHitUnitsPerSecond;

        public float VortexLifetime = T.VortexSeconds;

        /// <summary>
        /// Scatter spinning knots of wind around the player instead of cashing in Gust.
        ///
        /// Placed the way fire's eruption places its pools - evenly spaced around a ring, then
        /// jittered inward so the pattern never reads as a drawn circle - because pools and
        /// vortices answer the same question: both are ground the player commits to standing near,
        /// not a target they point at. Unlike Gust this needs no facing at all.
        /// </summary>
        void Squall()
        {
            ScatterVortices();
            Momentum = 0f;
            HitStreak = 0;
        }

        /// <summary>Squall's vortices again, spending nothing - a repeat, at its strength (Scale).</summary>
        void SquallAgain() => ScatterVortices();

        void ScatterVortices()
        {
            var origin = (Vector2)transform.position;
            float baseAngle = Random.value * Mathf.PI * 2f;
            float area = AreaScale;
            float spread = VortexSpread * area, radius = VortexRadius * area;
            float hit = HitUnit * Scale;

            for (int i = 0; i < VortexCount; i++)
            {
                float angle = baseAngle + (i / (float)VortexCount) * Mathf.PI * 2f
                              + Random.Range(-0.35f, 0.35f);
                float dist = spread * Random.Range(0.35f, 1f);
                var pos = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

                // Clamped inside the walls, same reasoning as Fire's eruption: a vortex spawned
                // through a wall is a cost the player paid for and can never stand near.
                pos = Arena.NearestFloor(Arena.Clamp(pos, radius), 0.3f);

                AirVortex.Spawn(pos, radius, VortexPullForce,
                               VortexNearHitUnits * hit, VortexFarHitUnits * hit,
                               VortexLifetime, gameObject, transform.parent);
            }

            Spr.Flash(transform.position, spread, new Color(0.8f, 0.95f, 1f), 0.5f);
            Spr.Flash(transform.position, spread * 0.5f, Color.white, 0.3f);
        }

        public override string StatusLine
        {
            get
            {
                // Same priority Water's own Surging line takes - a running buff outranks the
                // meter, since the meter is not going anywhere but the buff is on a clock.
                if (Gusting) return $"GUST  +range +speed +lifesteal  {_gustRemaining:0.0}s";

                if (CanRelease)
                    return UseSecondAbility
                        ? "SQUALL READY  [RMB/E] - scatter vortices"
                        : "GUST READY  [RMB/E] - range, speed, and lifesteal";

                return $"streak {Mathf.Min(StreakCap, HitStreak)}/{StreakCap}  crit {CritChance * 100f:0}%  " +
                       $"spd +{AttackSpeedPoints:0}  move +{MoveSpeedPoints:0}";
            }
        }
    }
}
