using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Player
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        // Defaults for every field below live in Core/Tuning.cs - the one file to tweak for feel.
        // The fields stay here so a scene/prefab could still override one, and so the tooltips
        // show up in the Inspector.

        [Header("Movement")]
        public float MoveSpeed = Tuning.Player.MoveSpeed;

        [Header("Attack")]
        public float BaseAttackInterval = Tuning.Attack.BaseInterval;
        public float BaseRange = Tuning.Player.BaseRange;
        public float BaseDamage = Tuning.Player.BaseDamage;
        public float ArcDot = Tuning.Player.SwingArcDot;      // ~150 degree swing arc
        public float Knockback = Tuning.Player.Knockback;

        [Tooltip("Universal attack-speed dial for the unbuffed baseline. 1 = as originally tuned; " +
                 "below 1 slows every swing AND its animation by the same factor, so the rest state " +
                 "feels deliberate rather than frantic. Per-element attack-speed buffs multiply on " +
                 "top of this, so a fully ramped late-run character still climbs back above 1 - this " +
                 "sets the floor, not the ceiling. Central value: Tuning.Attack.GlobalTempo.")]
        [Range(0.4f, 1.5f)] public float GlobalAttackTempo = Tuning.Attack.GlobalTempo;

        [Header("Undertow weapon art")]
        [Tooltip("The vortex hauls in every enemy on screen. Its pull is at full strength within " +
                 "this multiple of the reach ring (BaseRange); past that it eases off with " +
                 "distance, out to the edge of the view where it is weakest.")]
        public float UndertowFullPullRangeMul = Tuning.Undertow.FullPullRangeMul;

        [Tooltip("How much of the pull still reaches an enemy at the very edge of the screen, as a " +
                 "fraction of full strength. Above zero so a scattered fight always closes up.")]
        [Range(0f, 1f)] public float UndertowEdgePullFraction = Tuning.Undertow.EdgePullFraction;

        [Header("Combo")]
        [Tooltip("Seconds of not attacking before the chain resets to the first swing.")]
        public float ComboResetSeconds = Tuning.Combo.ResetSeconds;
        public const int MaxMovesets = Tuning.Combo.MaxMovesets;

        public Health Health { get; private set; }
        public ElementalResource Resource { get; private set; }

        StatusEffects _status;

        /// <summary>
        /// True while an external effect - so far only the Gargoyle family's gaze - has rooted
        /// the player. MOVEMENT ONLY: FixedUpdate zeroes the stick exactly as it does for
        /// AttackLocked, so attacking, aiming and every other action stay live. See
        /// StatusEffects.Petrified for why this lives there rather than on MoveMultiplier.
        /// </summary>
        public bool Petrified => _status != null && _status.Petrified;

        /// <summary>
        /// Turned to stone by Medusa's gaze (StatusEffects.Stone): no moving, attacking, releasing
        /// or defending, the figure drawn as a statue and frozen mid-pose. Unlike
        /// <see cref="Petrified"/>, which only roots.
        /// </summary>
        public bool Statue => _status != null && _status.Stone;

        /// <summary>What the rig was last told, so SetStone is called on the CHANGE only.</summary>
        bool _drawnAsStatue;

        void SyncStatue()
        {
            bool want = Statue;
            if (want == _drawnAsStatue) return;
            _drawnAsStatue = want;
            Rig?.SetStone(want);
            if (want)
            {
                _tapBuffer = 0f;   // a press made as flesh must not come out of the stone
                CancelArtHold();   // nor an aim held as one
            }
        }

        /// <summary>
        /// The finisher wheel. Always full: every slot starts on the default moveset (the
        /// overhand) and earned movesets replace one, so the chain always exists.
        ///
        /// A LIST rather than a fixed array because the count is no longer three. It grows from
        /// two independent sources - Extra Sigil off the ledger, and a black-diamond weapon's
        /// locked signature - so everything that reads it goes through Count.
        /// </summary>
        public readonly List<Moveset> Slots = new()
        {
            MovesetLibrary.Default, MovesetLibrary.Default, MovesetLibrary.Default,
        };

        /// <summary>
        /// Slots that cannot be overwritten by a floor reward, parallel to <see cref="Slots"/>.
        ///
        /// A black-diamond weapon's signature finisher lives in one. It is the WEAPON's move, not
        /// something the run earned, so handing it to the reward screen as a replaceable slot
        /// would let a player throw away the reason they equipped the weapon - and then be unable
        /// to get it back, since the reward pool has no idea the move exists.
        /// </summary>
        readonly List<bool> _locked = new() { false, false, false };

        public bool SlotLocked(int index) => index >= 0 && index < _locked.Count && _locked[index];

        /// <summary>
        /// Resize the wheel to base + ledger, keeping what is already in it.
        ///
        /// Called whenever the ledger changes rather than on a one-shot "you took the boon",
        /// because a one-shot cannot be replayed: `Mods` is recomputed from the whole ledger on
        /// every take, so the slot count has to be derived from it the same way or a second stack
        /// silently does nothing (the exact failure PendingOffer exists to avoid elsewhere).
        ///
        /// A locked signature no longer grows this - see SetSignatureFinisher's own doc for why
        /// it now occupies slot 3 instead of appending a fourth. Extra Sigil is the only source
        /// left, and it is capped at one stack, so the whole ceiling is BaseSlots + 1.
        ///
        /// Growing appends defaults; shrinking is not attempted here. Nothing in the ledger
        /// removes a boon mid-run, and a shrink would have to decide which earned finisher to
        /// throw away - see RemoveThirdSlot for the one shrink this file DOES support, which is a
        /// player's own choice rather than something the ledger would ever trigger.
        ///
        /// The base it grows FROM respects that choice too - a ledger change after the player
        /// dropped slot 3 must not silently regrow it, or a boon taken later in the run would
        /// undo a decision made three floors ago.
        /// </summary>
        public void SyncSlotCount(int extraFromLedger)
        {
            int baseNow = _thirdSlotRemoved ? MinSlots : BaseSlots;
            int want = Mathf.Clamp(baseNow + extraFromLedger, MinSlots, MaxSlots);
            while (Slots.Count < want) { Slots.Add(MovesetLibrary.DefaultFor(Weapon)); _locked.Add(false); }
            while (_locked.Count < Slots.Count) _locked.Add(false);
        }

        bool _thirdSlotRemoved;

        /// <summary>The wheel's default size - three, one of which (slot 3) is the relic-tied one.</summary>
        public const int BaseSlots = MaxMovesets;

        /// <summary>
        /// The floor: slot 3 dropped by the player's own choice, and nothing else earned yet.
        /// Never reachable while slot 3 is locked - see RemoveThirdSlot.
        /// </summary>
        public const int MinSlots = BaseSlots - 1;

        /// <summary>
        /// The ceiling. Extra Sigil is the only source left that grows the wheel, and it is
        /// capped at one stack - a locked signature occupies an existing slot instead of adding
        /// one, so this is exactly what the one remaining source can reach.
        /// </summary>
        public const int MaxSlots = BaseSlots + 1;

        Moveset _signature;

        /// <summary>
        /// Give the wheel a weapon or relic's locked signature finisher, or clear it with null.
        ///
        /// OCCUPIES slot 3 (index BaseSlots - 1) rather than appending a new one. It used to grow
        /// the wheel by one, which meant stacking a signature weapon AND a signature relic (or
        /// Extra Sigil on top of either) could reach five or six slots off gear alone - a lot of
        /// power for just equipping items, and it left Extra Sigil's own +1 feeling small next to
        /// what a signature was already handing out for free. Tying it to slot 3 instead makes a
        /// signature a REPLACEMENT for the slot a relic already owns, not a bonus on top of it.
        /// </summary>
        public void SetSignatureFinisher(Moveset signature, int extraFromLedger)
        {
            _signature = signature;
            SyncSlotCount(extraFromLedger);

            for (int i = 0; i < _locked.Count; i++) _locked[i] = false;
            if (_signature == null) return;

            int at = ThirdSlotIndex;
            Slots[at] = _signature;
            _locked[at] = true;
        }

        /// <summary>Slot 3's index - the one a relic's finisher ties to. BaseSlots - 1, i.e. 2.</summary>
        const int ThirdSlotIndex = BaseSlots - 1;

        /// <summary>
        /// A relic's pre-run pick for slot 3 when it names a finisher but is not Black Diamond -
        /// seeds the slot's content WITHOUT locking it, so a floor reward can still overwrite it
        /// same as any other unlocked slot. Skipped entirely whenever SetSignatureFinisher already
        /// locked the slot - a lock always wins over a mere seed.
        /// </summary>
        public void SeedThirdSlot(Moveset granted)
        {
            if (granted == null || _locked[ThirdSlotIndex]) return;
            Slots[ThirdSlotIndex] = granted;
        }

        /// <summary>
        /// Drops the wheel to its minimum by removing slot 3 - only possible while it holds the
        /// default or an unlocked relic pick, matching "locked means unreplaceable" everywhere
        /// else in this file. The reorder screen is what actually offers this to the player.
        /// </summary>
        public bool RemoveThirdSlot()
        {
            if (_locked[ThirdSlotIndex] || Slots.Count <= MinSlots) return false;
            Slots.RemoveAt(ThirdSlotIndex);
            _locked.RemoveAt(ThirdSlotIndex);
            _thirdSlotRemoved = true;
            return true;
        }

        /// <summary>Restores slot 3 (the default for this weapon class) after RemoveThirdSlot.</summary>
        public void RestoreThirdSlot()
        {
            if (!_thirdSlotRemoved) return;
            Slots.Insert(ThirdSlotIndex, MovesetLibrary.DefaultFor(Weapon));
            _locked.Insert(ThirdSlotIndex, false);
            _thirdSlotRemoved = false;
        }

        /// <summary>
        /// Reorder only - the same finishers, in a different sequence. A reward-locked slot (a
        /// signature, or a relic's granted finisher) can be swapped like any other one now - a
        /// lock only ever protected the moveset from being OVERWRITTEN by a floor reward, and
        /// there was no real reason to also pin its position. The flag travels WITH the moveset
        /// it protects, so after a swap the signature is locked wherever it ended up and the slot
        /// it left behind is not.
        /// </summary>
        public bool SwapSlots(int a, int b)
        {
            if (a < 0 || b < 0 || a >= Slots.Count || b >= Slots.Count || a == b) return false;
            (Slots[a], Slots[b]) = (Slots[b], Slots[a]);
            (_locked[a], _locked[b]) = (_locked[b], _locked[a]);
            return true;
        }

        /// <summary>Earned (non-default) movesets currently slotted.</summary>
        public IEnumerable<Moveset> EarnedMovesets
        {
            get { foreach (var m in Slots) if (m != null && !m.IsDefault) yield return m; }
        }

        public int EarnedCount
        {
            get { int n = 0; foreach (var m in Slots) if (m != null && !m.IsDefault) n++; return n; }
        }

        /// <summary>
        /// Basic swings before the finisher. The chain is therefore SWING, ALT-SWING, FINISHER.
        ///
        /// Was three basics. Two is enough to establish the back-and-forth - the alt-swing exists
        /// to answer the first one, and a third only repeated the first before anything had
        /// changed - and it gets the payoff round sooner, which is what the chain is for.
        ///
        /// Older movesets still declare a third basic: the FILLER, reached only when the ledger
        /// lengthens the chain (Leaking) - never in an ordinary chain, which is always two basics
        /// and the weapon art. New movesets declare two (a lengthened chain then repeats the lead-in).
        /// </summary>
        /// <summary>
        /// The run's accumulated boons and costs. Supplied by GameBootstrap as a lambda for the
        /// same reason DamageDealtMultiplier is: the ledger changes between floors and a value
        /// captured once at spawn would freeze the run's first floor into the whole run.
        ///
        /// Never null at the point of use - Mods falls back to an empty set, so the controller
        /// behaves identically when nothing has been taken.
        /// </summary>
        /// <summary>
        /// The weapon class this run is being played as, captured at the door and LOCKED.
        ///
        /// Not read from the equipped weapon each frame on purpose. The loadout screen opens
        /// mid-run, and a weapon swap that silently rewrote the chain, the finisher pool and the
        /// animation set would turn a cosmetic decision into a build reset three floors in. What
        /// you walked through the door holding is what you fight with.
        /// </summary>
        public Art.Gear.WeaponClass Weapon { get; private set; } = Art.Gear.WeaponClass.Greatsword;

        /// <summary>
        /// Extra ricochets beyond the class baseline. Wired to class progression when the grid
        /// gains it; a lambda so that can land without touching the controller.
        /// </summary>
        public System.Func<int> BonusRicochets;

        /// <summary>
        /// True when a disc BASIC should be thrown rather than swung.
        ///
        /// The decision is nothing but "is anything close enough to hit" - no mode, no toggle, no
        /// second button. That is what makes it one hybrid weapon rather than two weapons sharing
        /// a slot: the player chooses by where they stand, which is a decision they are already
        /// making constantly.
        ///
        /// Neither answer is the better one. See Tuning.Disc - the two halves are tuned to level
        /// single-target damage, so this switch changes the SHAPE of your output and not its size.
        ///
        /// Finishers are exempt. Those are the moves the class is defined by and each one already
        /// says whether it is thrown or swung.
        /// </summary>
        public bool ThrowsInsteadOfSwinging
        {
            get
            {
                if (Weapon != Art.Gear.WeaponClass.Disc) return false;
                var t = Targeting != null ? Targeting.Target : null;
                if (t == null || t.IsDead) return true;
                return Vector2.Distance(transform.position, t.transform.position) > CurrentRange;
            }
        }

        public void LockWeaponClass(Art.Gear.WeaponClass weapon)
        {
            Weapon = weapon;
            for (int i = 0; i < Slots.Count; i++)
            {
                if (_locked[i]) continue;      // the signature is the weapon's, not the class default
                Slots[i] = MovesetLibrary.DefaultFor(weapon);
            }

            // Re-applied: see ApplyForceMovesetOverride's own doc for why Awake alone isn't enough.
            ApplyForceMovesetOverride();
        }

        // ---- defensive ability: Dash / Barrier / Bulwark / Parry Stance ----
        //
        // Read this block alongside Tuning.Defense. All four share ONE mechanic - an
        // omnidirectional parry check for the first ParryWindowSeconds of any activation, which
        // negates the hit unconditionally and fires a counter - and differ only in what happens
        // if that instant is missed: Dash and Barrier fall back to real immunity for the rest of
        // their window (Barrier's gated to the front arc, which is why it needs BlocksHit rather
        // than the blanket Immune flag Dash uses), Bulwark falls back to a flat partial
        // reduction, and Parry Stance falls back to nothing at all - the trade for its much
        // shorter cooldown.

        public Art.Gear.DefensiveAbility EquippedDefensiveAbility { get; private set; }

        float _defenseCooldown;
        /// <summary>
        /// What <see cref="_defenseCooldown"/> was set to when the ability last fired, so a ring
        /// can draw the fraction rather than the raw seconds. Stored rather than recomputed from
        /// Tuning, because the number the player is actually waiting out already has the chest's
        /// AbilityCooldownReduction folded into it - a ring recomputing the base constant would
        /// close at the wrong rate on exactly the builds that paid for a shorter one.
        /// </summary>
        float _defenseCooldownTotal;
        float _defenseActiveTimer;
        float _parryWindowRemaining;
        Vector2 _barrierFacing;

        /// <summary>
        /// Dash's after-images and Bulwark's aura. Both are attached by BuildPlayer only when the
        /// run's chest actually grants that ability, so every other build pays nothing for them -
        /// which is affordable precisely because the ability is locked for the run and the test
        /// can be made once. Plain MonoBehaviour references, so a domain reload leaves them
        /// intact; null is the normal state, not a fault.
        /// </summary>
        public DashTrail Trail;
        public BulwarkAura Aura;

        /// <summary>Locked at run start from whatever chest is equipped, same as the weapon class.</summary>
        public void LockDefensiveAbility(Art.Gear.DefensiveAbility ability) => EquippedDefensiveAbility = ability;

        /// <summary>True while an attacker's hit should be checked against TryParry - see there.</summary>
        public bool ParryWindowOpen => _parryWindowRemaining > 0f || RiposteGuardUp;

        /// <summary>
        /// Riposte's guard (AttackStep.Guards): up for the whole timing bar, from the press to the
        /// strike. Not the chest's window - it doesn't spend it, doesn't fire the chest's counter
        /// and isn't drawn by the GuardRing; the held guard pose and the bar are its picture.
        /// </summary>
        bool RiposteGuardUp => _barLive && _barGuards && !Statue;

        /// <summary>Whether this guard has turned anything aside - decides the answer at the
        /// strike (RiposteCounter). Reset as the guard goes up.</summary>
        bool _guardParried;

        /// <summary>
        /// Seconds left on the shared window. Exposed so <see cref="GuardRing"/> can DRAW this
        /// number rather than run a copy of the clock beside it - one owner, the same rule
        /// FloorPits.SpeedMultiplierAt lives by. It matters here because the window does not only
        /// expire: a successful parry consumes it outright in TryParry, and a ring on its own
        /// timer would keep closing over a window that had already been spent.
        /// </summary>
        public float ParryWindowRemaining => Mathf.Max(0f, _parryWindowRemaining);

        /// <summary>Whether pressing guard right now would do anything - read by Controls.GuardReady
        /// so the touch button (and any future readout) can dim exactly while it's on cooldown.</summary>
        public bool DefenseReady => _defenseCooldown <= 0f;

        /// <summary>
        /// How far through the defensive cooldown, 0 to 1, reaching 1 exactly when
        /// <see cref="DefenseReady"/> turns true. Reads 1 before the ability has ever been used,
        /// since "never fired" and "recovered" are the same state to anything drawing it.
        /// </summary>
        public float DefenseCooldown01
            => _defenseCooldownTotal <= 0f
                ? 1f
                : Mathf.Clamp01(1f - _defenseCooldown / _defenseCooldownTotal);

        /// <summary>Bulwark's flat reduction is live - read by IncomingDamageMultiplier.</summary>
        public bool BulwarkActive =>
            EquippedDefensiveAbility == Art.Gear.DefensiveAbility.Bulwark && _defenseActiveTimer > 0f;

        void UpdateDefensiveAbility(float dt)
        {
            if (_defenseCooldown > 0f) _defenseCooldown -= dt;
            if (_parryWindowRemaining > 0f) _parryWindowRemaining -= dt;

            if (_defenseActiveTimer > 0f)
            {
                _defenseActiveTimer -= dt;
                if (_defenseActiveTimer <= 0f && Health != null)
                {
                    Health.Immune = false;
                    Health.BlocksHit = null;
                }
            }

            if (Core.Controls.GuardTapped && _defenseCooldown <= 0f && !Statue
                && !(Effects != null && Effects.DefenceShut(Time.time - _lastArtAt)))   // Shackled
                ActivateDefensiveAbility();
            // Lightfoot: pressed again while it recharges, the anchor it dropped snaps you back.
            else if (Core.Controls.GuardTapped && _defenseCooldown > 0f && !Statue)
                Effects?.OnGuardWhileRecharging();
        }

        void ActivateDefensiveAbility()
        {
            float cdMul = Art.Gear.StatPercents.ReductionFactor(Stats.AbilityCooldownReduction)
                          * Mods.DefenseCooldownMul;   // Encumbered
            // The board's Sal Ammoniac stretches the protection and the parry window, never the
            // cooldown or the dash's distance.
            float window = Board != null ? Board.DefenseWindowMul : 1f;
            _parryWindowRemaining = Tuning.Defense.ParryWindowSeconds * window;
            Effects?.OnDefenceUsed();   // Lacerate, a Defence form

            switch (EquippedDefensiveAbility)
            {
                case Art.Gear.DefensiveAbility.Dash:
                    _defenseActiveTimer = Tuning.Defense.DashSeconds * window;
                    _defenseCooldown = Tuning.Defense.DashCooldown * cdMul;
                    if (Health != null) Health.Immune = true;

                    // Where the right stick aims, else where the player is moving, else the facing.
                    var input = Core.Controls.Move;
                    var dashDir = AimedOr(input.sqrMagnitude > 0.01f ? input.normalized : Facing);
                    Dash(dashDir, Tuning.Defense.DashSpeed, Tuning.Defense.DashSeconds);
                    // Figures dropped along the ground actually covered, for exactly as long as
                    // the i-frames last - see DashTrail. Null on any build whose chest grants
                    // something else, which is most of them.
                    if (Trail != null) Trail.Begin(Tuning.Defense.DashSeconds);
                    break;

                case Art.Gear.DefensiveAbility.Barrier:
                    _defenseActiveTimer = Tuning.Defense.BarrierSeconds * window;
                    _defenseCooldown = Tuning.Defense.BarrierCooldown * cdMul;
                    _barrierFacing = AimedOr(Facing);   // the right stick points the dome
                    BarrierDome.Raise(transform, _barrierFacing, Tuning.Defense.BarrierSeconds * window);
                    if (Health != null)
                    {
                        Health.BlocksHit = info =>
                        {
                            if (info.Source == null) return false;
                            Vector2 to = (Vector2)info.Source.transform.position - (Vector2)transform.position;
                            if (to.sqrMagnitude < 0.0001f) return false;
                            return Vector2.Dot(to.normalized, _barrierFacing) >= Tuning.Defense.BarrierFrontDot;
                        };
                    }
                    break;

                case Art.Gear.DefensiveAbility.Bulwark:
                    _defenseActiveTimer = Tuning.Defense.BulwarkSeconds * window;
                    _defenseCooldown = Tuning.Defense.BulwarkCooldown * cdMul;
                    if (Aura != null) Aura.Raise(Tuning.Defense.BulwarkSeconds * window);
                    break;   // BulwarkActive is read directly by IncomingDamageMultiplier

                default:   // ParryStance - no benefit beyond the shared window above
                    _defenseActiveTimer = Tuning.Defense.ParryWindowSeconds * window;
                    _defenseCooldown = Tuning.Defense.ParryStanceCooldown * cdMul;
                    break;
            }

            // Captured AFTER the switch rather than beside each of the four assignments - one
            // line that cannot fall out of step with a fifth ability added later.
            _defenseCooldownTotal = _defenseCooldown;

            // No shared flash here any more. It fired identically for all four, so it could only
            // ever announce THAT an ability went off - never which, and never how much of the
            // window was left. GuardRing draws the window itself off ParryWindowRemaining, and
            // each case above raises its own identity on top of it.
        }

        /// <summary>
        /// Called by an attacker INSTEAD OF Health.Take when ParryWindowOpen is true - not folded
        /// into Take() itself, because what a parry does beyond negating the hit differs per
        /// attacker (a projectile reflects itself; a melee swing does not), which Health has no
        /// business knowing about. Returns whether it actually consumed the window, so a caller
        /// that already checked ParryWindowOpen a frame ago cannot double-fire.
        /// </summary>
        public bool TryParry(Vector2 attackerPosition)
        {
            // Riposte's guard turns every hit for as long as it is up; the art's own strike is
            // the answer, so no counter here.
            if (RiposteGuardUp)
            {
                _guardParried = true;   // the art answers with the counter (RiposteCounter)
                ParryTell(attackerPosition);
                Ward.Flash();
                return true;
            }

            if (_parryWindowRemaining <= 0f) return false;
            _parryWindowRemaining = 0f;
            ParryTell(attackerPosition);

            FireCounterStrike();

            // The board's Volatilization: a parry hands the ability straight back.
            if (Board != null && Board.RefundsOnParry) _defenseCooldown = 0f;
            return true;
        }

        /// <summary>What every parry shows: the freeze, the ring thrown outward, the camera
        /// shoved away from the attacker.</summary>
        void ParryTell(Vector2 attackerPosition)
        {
            Hitstop.TriggerParry();

            // The answer, and it OPENS where GuardRing closes. The contracting ring means a
            // window is running out; a ring thrown outward on the same spot means it was spent
            // well. Two directions, two meanings - which is the only reason a second ring here
            // is not just more circles on a screen that already has several.
            Spr.Pulse(transform, Tuning.Defense.ParryBurstRadius, Color.white,
                      Tuning.Defense.ParryBurstSeconds, ring: true, growth: 0.9f);

            // CameraKick was already public for exactly this - see its own note. Shoved AWAY
            // from the attacker, so the frame reads as the blow being turned rather than landing.
            Vector2 away = (Vector2)transform.position - attackerPosition;
            if (away.sqrMagnitude > 0.0001f)
                Combat.CameraKick.Kick(away.normalized, Tuning.Defense.ParryCameraKick);
        }

        /// <summary>
        /// Nearest enemy within whichever range circle is currently furthest out (a disc's throw
        /// band beats its melee reach). Damage negation already happened unconditionally in
        /// TryParry - this can whiff with nothing in range, which is deliberate: parrying a
        /// distant ranged attacker should not be a guaranteed free hit on somebody else entirely.
        /// </summary>
        void FireCounterStrike()
        {
            Enemies.EnemyRegistry.Prune();

            Health nearest = null;
            float bestDist = float.MaxValue;
            float reach = AcquireReach;
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                float d = Vector2.Distance(transform.position, e.transform.position);
                if (d > reach || d >= bestDist) continue;
                bestDist = d;
                nearest = hp;
            }

            // The parry's own tell fires whether or not the counter finds a target - the negation
            // already happened, and the player needs to see THAT regardless of the whiff.
            Spr.Flash(transform.position, 0.9f, Color.white, 0.25f);

            if (nearest == null) return;

            var dir = ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized;
            nearest.Take(new DamageInfo(Tuning.Defense.CounterDamage, ElementType.Earth, gameObject)
            {
                Knockback = dir * Tuning.Defense.CounterKnockback,
                Displaces = true,
            });
            Spr.Flash(nearest.transform.position, 0.7f, new Color(1f, 0.9f, 0.4f), 0.22f, false);
        }

        /// <summary>What one connecting hit is worth to a per-hit resource. See WeaponClasses.</summary>
        public float HitMeterScale => Art.Gear.WeaponClasses.HitMeterScale(Weapon);

        /// <summary>
        /// The fire blade's heat, when one is equipped. Null the rest of the time, and every read
        /// is guarded - most weapons have no cycle and must not pay for one.
        /// </summary>
        public Combat.WeaponHeat Heat;

        /// <summary>
        /// Shadow's chain passive: every connecting hit lands a second time for this fraction of
        /// the first. Zero the rest of the time, which is every weapon but one.
        ///
        /// It comes from the SOURCE of the signature - the blade in hand or the one in the relic
        /// socket - and not from the blade being drawn, because this half is mechanics. The
        /// figure that throws it is the half that needs the blade on screen; see EchoChorus.
        /// </summary>
        public float EchoHitFraction;

        /// <summary>
        /// The echo fraction that actually applies to THIS swing.
        ///
        /// CONDITIONAL, and that is the rework. EchoHitFraction used to be assigned once at
        /// BuildPlayer and applied to every landing hit for the rest of the run - a permanent,
        /// unconditional +50% damage that a socketed relic granted for free alongside whatever
        /// weapon was actually equipped. Measurably a straight power increase rather than a
        /// different way to play, and the exact "buy this, win" shape the rest of this design goes
        /// to some length to avoid.
        ///
        /// It now applies only while the wheel is on SHADOW'S OWN chain, which is what makes the
        /// cost real: one of the three rotating chains gives up its finisher's damage entirely in
        /// exchange for the echo (see the Echo step's DamageMultiplier), and the echo only pays
        /// during that chain.
        ///
        /// THE WHOLE CHAIN, NOT ONLY THE FINISHER SWING, and the arithmetic is why. Under the
        /// 2-basic chain, three basic-damage swings each landing twice is 3 x 1.5 = 4.5 over 3.0
        /// time = 1.50 DPS, against a Light chain's 1.67 - about 10% light, which is what pays for
        /// the AttackLocked exemption. Echoing only the finisher swing gives 3.5 over 3.0 = 1.17,
        /// a 30% shortfall that no exemption is worth. The design note describing this states both
        /// readings in different sentences; the arithmetic is stated twice and is what the balance
        /// argument rests on, so it wins.
        /// </summary>
        float ActiveEchoFraction =>
            EchoHitFraction > 0f && ActiveMoveset != null && ActiveMoveset.Id == ShadowSignatureId
                ? EchoHitFraction
                : 0f;

        /// <summary>The moveset id Shadow's signature carries. Matched rather than compared by
        /// reference, because the wheel holds library instances and a run rebuilds them.</summary>
        const string ShadowSignatureId = "shadow_echo";

        /// <summary>
        /// True while the weapon actually DRAWN carries Phantom's flicker - set once at
        /// BuildPlayer and re-synced on any weapon-slot equip, the same one-shot-plus-resync shape
        /// Prism's element gems use, and for the same reason: this is a picture, not a mechanic,
        /// so it follows whatever is drawn rather than whatever granted the signature.
        /// </summary>
        public bool PhantomFlicker;

        /// <summary>
        /// True while the weapon actually DRAWN is the one the sheathe-and-draw sequence belongs
        /// to. Same one-shot-plus-resync shape as <see cref="PhantomFlicker"/>, and the same
        /// reason: the RELIC grants Crosscut, the weapon grants its animation.
        ///
        /// Without it, socketing the saya beside a gold greatsword played the whole sequence -
        /// a sword sliding into a scabbard the character is not wearing. The finisher still
        /// lands, at the same weight and damage; it just resolves as an ordinary swing.
        /// </summary>
        public bool SheatheDrawn;

        /// <summary>
        /// The Gemini Blade's two halves while it is the DRAWN weapon (GearItem.HalfBlades), else
        /// null - what Solve et Coagula splits the sword into. Set where SheatheDrawn is, for the
        /// same reason: the relic grants the art, the drawn blade buys the sequence.
        /// </summary>
        public Sprite[] GeminiHalves;
        bool GeminiDrawn => GeminiHalves is { Length: >= 2 };

        /// <summary>The moveset id Phantom's signature carries. Matched rather than compared by
        /// reference, for the same reason ShadowSignatureId is.</summary>
        const string PhantomSignatureId = "phantom_reckoning";

        /// <summary>
        /// The Blood Blade's fuller. Created (from the SOURCE granting the signature) and read
        /// (from the DRAWN weapon's sprite set) the same split WeaponHeat's own Heat field uses -
        /// see GameBootstrap.BuildPlayer.
        /// </summary>
        public Combat.BloodVial Vial;

        /// <summary>The moveset id Blood Blade's signature carries. Matched rather than compared
        /// by reference, for the same reason ShadowSignatureId is.</summary>
        const string BloodSignatureId = "blood_drink";

        /// <summary>
        /// The shadow duplicates: the trailing after-image on every swing, and the ring the Echo
        /// finisher tears loose. Null on every weapon that is not Shadow.
        /// </summary>
        public Combat.EchoChorus Echoes;

        /// <summary>Separatio's three figures - set whenever the run has Tria Prima's finisher.</summary>
        public Combat.SeparatioFigures Split;

        /// <summary>The whole four-element armillary the Armillary's halves become overhead in
        /// Quintessence - the DRAWN weapon's (GearItem.CombinedFrames), so the picture follows the
        /// picture; null draws the main hand's half there instead.</summary>
        public Sprite[] CombinedFrames;

        /// <summary>The drawn weapon's own relic is socketed, so a both-discs attack draws its discs
        /// in flight as <see cref="CombinedFrames"/> (Combat.DiscPictures) - GameBootstrap sets it
        /// with CombinedFrames.</summary>
        public bool CombinedInFlight;

        /// <summary>The drawn bow's arrow (DemoGear.BowKit) - what Combat.ThrownArrow flies; null
        /// flies the plain element capsule (the Hive redraws its own).</summary>
        public Sprite ArrowSprite;
        public float CombinedFrameSeconds = 0.1f;

        /// <summary>The real window until the next swing for a step - what the rig animates over.</summary>
        public float SwingWindowFor(AttackStep step) => IntervalFor(step);

        /// <summary>
        /// The character's stat block, in percentage points over the Tuning.Player baselines.
        /// Summed from gear and the mastery grid at run start and bent through the character curve
        /// (Art.Gear.StatCurves) - the EFFECTIVE values. See StatPercents.
        /// </summary>
        public Art.Gear.StatPercents Stats = new();

        /// <summary>
        /// The same stat block BEFORE the character curve. The stats an element gives a head start
        /// on - damage, attack speed, move speed, crit damage - are bent from these together with
        /// the element's points, live (the *PointsNow below), so the element counts toward the
        /// same caps instead of multiplying past them. Null falls back to <see cref="Stats"/>.
        /// </summary>
        public Art.Gear.StatPercents RawStats;

        Art.Gear.StatPercents Raw => RawStats ?? Stats;

        /// <summary>Damage points right now: the character's and the element's head start, bent once.</summary>
        public float DamagePointsNow => Art.Gear.StatCurves.Character(Art.Gear.StatKind.Damage,
                                            Raw.Damage + (Resource?.DamagePoints ?? 0f)
                                            + (Board != null ? Board.DamagePoints : 0f));

        float AttackSpeedPointsNow => Art.Gear.StatCurves.Character(Art.Gear.StatKind.AttackSpeed,
                                          Raw.AttackSpeed + (Resource?.AttackSpeedPoints ?? 0f)
                                          + (Board != null ? Board.AttackSpeedPoints : 0f));

        float MoveSpeedPointsNow => Art.Gear.StatCurves.Character(Art.Gear.StatKind.MoveSpeed,
                                        Raw.MoveSpeed + (Resource?.MoveSpeedPoints ?? 0f));

        float CritDamagePointsNow => Art.Gear.StatCurves.Character(Art.Gear.StatKind.CritDamage,
                                          Raw.CritDamage + (Resource?.CritDamagePoints ?? 0f));

        /// <summary>
        /// The player's basic hit right now, before crit and the damage roll: the base, the ledger's
        /// bonus, and every damage multiplier the stat block carries. What an elemental release's
        /// damage is counted in (Tuning.Elements), so a release grows with the character.
        /// </summary>
        public float HitUnit => (BaseDamage + Mods.BonusDamage) * (DamageDealtMultiplier?.Invoke() ?? 1f);

        /// <summary>The unbuffed walk speed every bonus multiplies - MoveSpeed itself stays the
        /// character's own top speed, which the rig reads as its gait reference.</summary>
        public float BaseMoveSpeed = Tuning.Player.MoveSpeed;

        public System.Func<Exchange.Mods> ModsSource;

        /// <summary>The run's conditional effects. Null when nothing has been taken.</summary>
        public Exchange.RunEffects Effects;

        /// <summary>The mastery board's principle chains. Null when nothing is bound - every use
        /// is null-guarded, because a character with no thresholds crossed has no chains at all
        /// and that is the ordinary case rather than an error.</summary>
        public Progression.PrincipleEffects Principles;

        /// <summary>The mastery board's rules (Tinctures, Opuses, the status keystones). Null when
        /// the board owns none - every use is null-guarded, for PrincipleEffects' reason.</summary>
        public Progression.BoardEffects Board;

        static readonly Exchange.Mods NoMods = new();
        Exchange.Mods Mods => ModsSource?.Invoke() ?? NoMods;

        /// <summary>The ledger's view for presentation that has to honour a cost (Fog hides the
        /// target highlight). Read-only use - the ledger is written through RunModifiers.</summary>
        public Exchange.Mods Ledger => Mods;

        /// <summary>
        /// Basics before a finisher. Two by default; Short Chain lowers it, Long Chain raises it.
        /// Floored at one - a chain of zero basics is not a shorter chain, it is no chain.
        /// </summary>
        public int BasicsPerChain => Mathf.Max(1, BaseBasicsPerChain + Mods.BasicsPerChainDelta
                                                     + (Board != null ? Board.ChainDelta : 0));

        public const int BaseBasicsPerChain = 2;

        /// <summary>
        /// Longest the chain can ever get, including the finisher. Anything that builds a fixed
        /// row of chain widgets sizes itself to this and hides the unused end, because the chain
        /// length now moves during a run.
        /// </summary>
        public const int MaxChainSteps = BaseBasicsPerChain + 2 + 1;

        /// <summary>Which swing of the chain is next: 0..BasicsPerChain-1 basics, then finisher.</summary>
        public int ComboIndex { get; private set; }

        /// <summary>Which moveset's finisher is up next. Advances after every finisher.</summary>
        public int RotationIndex { get; private set; }

        /// <summary>The rotation socket of the weapon art last dispatched, and a count of arts
        /// dispatched - an Art FORM fires for its own socket only, once per art (the exchange's
        /// Phase 2). Set in Dispatch before the art resolves.</summary>
        public int ArtSocket { get; private set; }
        public int ArtSerial { get; private set; }
        float _lastArtAt = -99f;   // Shackled shuts the defensive ability for a moment after an art

        /// <summary>A weapon art's lock: the ledger's (Celerity, Overcommitted) and its socket's own
        /// (Heavy Hand on the socket a form is inscribed on).</summary>
        float ArtLockMul => Mods.LockMul * (Effects != null ? Effects.SocketLockMul(ArtSocket) : 1f);

        public Moveset ActiveMoveset => Slots[RotationIndex % Slots.Count] ?? MovesetLibrary.Default;

        /// <summary>True when the next swing is the finisher rather than a basic.</summary>
        public bool FinisherNext => ComboIndex >= BasicsPerChain || Tuning.Testing.EverySwingIsFinisher;

        /// <summary>
        /// True while a finisher is committing the character: no walking, no turning.
        ///
        /// Finishers are the payoff of a three-swing chain and hit two to four times as hard as a
        /// basic, so they have to cost something other than time. Committing to a direction is that
        /// cost - it turns "press the button" into "pick the moment", and it is what makes the
        /// wide-arc finishers a read on enemy position rather than a guaranteed hit.
        ///
        /// Basics stay fully mobile. Locking those would make the whole game feel stuck.
        /// </summary>
        public bool AttackLocked => _lockTimer > 0f;

        /// <summary>The weapon, while it is out of the player's hand. Null the rest of the time.</summary>
        public ThrownBlade Blade { get; private set; }

        /// <summary>
        /// Discs left hanging by Sublimate, waiting for the next finisher to set them off.
        /// Null whenever nothing is armed.
        /// </summary>
        public SuspendedDiscs Suspended { get; private set; }

        /// <summary>0..1 through a charged strike's wind-up; 0 when not charging.</summary>
        public float ChargeProgress01 { get; private set; }

        /// <summary>True while the character is off the screen mid-leap, immune and still mobile.</summary>
        public bool Airborne { get; private set; }

        /// <summary>
        /// Radius of the blast a leap is about to drop, or 0 when none is pending.
        ///
        /// Published so LandingZone can mark it. The player is off screen for
        /// two seconds and free to run the whole time, so the circle on the ground is the ONLY
        /// thing saying where the damage will actually fall - without it the move is a blind
        /// commitment with a two-second delay.
        /// </summary>
        public float ImpactRadius { get; private set; }

        /// <summary>Where a leap will come down, world - the player's own position for a leap
        /// straight up, ahead along the carry for a directed one (LandingZone draws it there).</summary>
        public Vector2 ImpactCentre { get; private set; }

        /// <summary>A directed leap's carry, world units a second, added to the walk while
        /// airborne - see AttackStep.LeapDistance.</summary>
        Vector2 _leapCarry;

        /// <summary>The carry as last written into the body's velocity (before damping) - taken
        /// off again before the walk's blend, see FixedUpdate.</summary>
        Vector2 _leapCarryPut;

        /// <summary>Seconds left of the double-damage window a leap leaves behind; 0 when clear.</summary>
        public float ExposedSeconds { get; private set; }

        /// <summary>
        /// Seconds left SOAKED - more damage taken from everything (Tuning.Hazards.WaterSoak*).
        /// Held full while the player stands in water and counted down once they are out. A leap
        /// over water never soaks: only the drawing leaves the ground, but nothing is wading.
        /// </summary>
        public float SoakedSeconds { get; private set; }
        public bool Soaked => SoakedSeconds > 0f;
        float _soakCue;

        /// <summary>Reach of the swing that will fire next - what TargetHighlight measures a
        /// sword's target against. A Grappling Hook next reaches as far as the chain flies; Impale
        /// as far as the lunge carries the thrust.</summary>
        public float PendingRange =>
            !FinisherNext || ActiveMoveset?.Finisher == null ? CurrentRange
            : ActiveMoveset.Finisher.Grapples ? Tuning.Grapple.Reach
            : ActiveMoveset.Finisher.Tethers ? Tuning.Tether.Reach
            : ActiveMoveset.Finisher.Gyres ? Tuning.Gyre.Reach
            : ActiveMoveset.Finisher.Plows ? Tuning.Plow.Reach
            : ActiveMoveset.Finisher.PiercesPath ? CurrentRange + ActiveMoveset.Finisher.LungeDistance
            : ActiveMoveset.Finisher.Guards ? CurrentRange + Tuning.Riposte.CounterStep
            : CurrentRange;

        /// <summary>
        /// How far the auto-targeter may lock on, which is NOT the same as reach.
        ///
        /// For the greatsword they are the same thing: everything it does happens inside its arc.
        /// A disc can hit something eight units away, and locking only at melee reach left the
        /// character facing wherever they last walked while throwing blind past a target they
        /// could plainly hit - the ranged half barely worked.
        ///
        /// Reach itself is deliberately left alone: TargetHighlight draws a target inside it
        /// solid (a swing lands) and one only in the throw band dashed (the disc throws), so the
        /// band where the weapon switches hands is still readable.
        /// </summary>
        public float AcquireReach => Mathf.Max(CurrentRange, ThrowReach);

        /// <summary>
        /// How far this weapon throws, or its melee reach when it does not throw at all. Anything
        /// drawing the throw band or aiming a thrown weapon reads this.
        /// </summary>
        public float ThrowReach => Weapon == Art.Gear.WeaponClass.Disc
            ? CurrentRange * Tuning.Disc.ThrowRangeMul
            : CurrentRange;

        /// <summary>
        /// The floor of THIS weapon's distance-damage curve - 1 for a weapon whose damage does
        /// not vary by range at all. TargetHighlight reads this to brighten a ranged target's rim
        /// with what the shot is worth at that distance.
        /// </summary>
        public float RangeNearFraction => Weapon == Art.Gear.WeaponClass.Bow ? Tuning.Bow.NearFraction
            : Weapon == Art.Gear.WeaponClass.Disc && ThrowsInsteadOfSwinging ? Tuning.Disc.ThrowDamageNear
            : 1f;

        /// <summary>The swing that will fire next.</summary>
        public AttackStep NextStep
        {
            get
            {
                var ms = ActiveMoveset;
                if (FinisherNext)
                    return DevBowPair > 0 && Weapon == Art.Gear.WeaponClass.Bow ? DevBowArt(ms.Finisher) : ms.Finisher;

                // The LEAD-IN (Basics[1]) is always the swing right before the finisher, however
                // long the ledger makes the chain: it is authored to end where its finisher
                // starts, so Anvil's one-swing chain is just the lead-in, and Leaking's extra
                // swings go BETWEEN the opener and it (Basics[2]) rather than after it.
                int i = LeadInNext ? 1 : ComboIndex == 0 ? 0 : 2;
                if (DevBasicsRendition > 0 && Weapon == Art.Gear.WeaponClass.Greatsword)
                    return DevBasic(DevBasicsRendition, i == 1 ? 1 : 0);
                if (DevBowPair > 0 && Weapon == Art.Gear.WeaponClass.Bow)
                    return DevBowBasic(i == 1 ? 1 : 0);
                return ms.Basics[Mathf.Min(i, ms.Basics.Length - 1)];
            }
        }

        /// <summary>
        /// Testing: swing one of the five DIMENSIONAL basic combos (AttackMotion's CrescentCut and
        /// on, PrimitiveCharacterRig's Combos partial) in place of the moveset's own basics, on a
        /// greatsword - 1 Crescent, 2 Cyclone, 3 Undertow, 4 Ascent, 5 Return; 0 the moveset's.
        /// The finisher is still the moveset's (the arts are connected to them next). From eval:
        /// <c>Convergence.Player.PlayerController.DevBasicsRendition = 2;</c>
        /// </summary>
        public static int DevBasicsRendition;

        // Built lazily; non-readonly and rebuilt if a reload empties it.
        static AttackStep[] _devBasics;

        static AttackStep DevBasic(int rendition, int hit)
        {
            if (_devBasics == null || _devBasics.Length == 0)
            {
                _devBasics = new AttackStep[10];
                for (int r = 0; r < 5; r++)
                    for (int h = 0; h < 2; h++)
                        _devBasics[r * 2 + h] = new AttackStep
                        {
                            Name = h == 0 ? "Cut" : "Lead",
                            DamageMultiplier = 1f, IntervalMultiplier = 1f,
                            Motion = (AttackMotion)((int)AttackMotion.CrescentCut + r * 2 + h),
                        };
            }
            int index = Mathf.Clamp(rendition - 1, 0, 4) * 2 + hit;
            return _devBasics[index];
        }

        /// <summary>
        /// Testing: the bow's connector pairs (WEAPON-ARTS.md) in place of the moveset's basics -
        /// 1 KNEEL: a shot for two basics' damage, then the roll down onto one knee, and a plain-Draw
        /// art drawn from the kneel (KneelDraw). 2 RECOIL: the same shot, then a hop straight back,
        /// and the art drawn from the recoil (RecoilDraw). 3 HIGH: a level shot, then a LOB (LobDraw,
        /// ThrownArrow.Lob), and the art drawn down from the raised bow (HighDraw). 4 CANTED: a SNAP
        /// shot (SnapDraw), then one drawn with the bow turned flat (CantedDraw), and the art drawn
        /// canted. 5 TWIN NOCK: a shot, then two arrows at once (TwinDraw), and the art drawn from the
        /// bow held ready. 0 the moveset's
        /// own. From eval:
        /// <c>Convergence.Player.PlayerController.DevBowPair = 1;</c>
        /// </summary>
        public static int DevBowPair;

        static AttackStep[] _devBow;

        static AttackStep DevBowBasic(int hit)
        {
            if (_devBow == null || _devBow.Length == 0)
                _devBow = new[]
                {
                    new AttackStep { Name = "Shot", DamageMultiplier = 2f, IntervalMultiplier = 1f,
                                     Motion = AttackMotion.Draw, BasicsWorth = 2 },
                    new AttackStep { Name = "Roll", DamageMultiplier = 0f, IntervalMultiplier = 1f,
                                     Motion = AttackMotion.BowKneelRoll, KneelRoll = true },
                    new AttackStep { Name = "Recoil", DamageMultiplier = 0f, IntervalMultiplier = 1f,
                                     Motion = AttackMotion.BowRecoilHop, RecoilHop = true },
                    new AttackStep { Name = "Shot", DamageMultiplier = 1f, IntervalMultiplier = 1f,
                                     Motion = AttackMotion.Draw },
                    new AttackStep { Name = "Lob", DamageMultiplier = 1f, IntervalMultiplier = 1f,
                                     Motion = AttackMotion.LobDraw, LobArc = Tuning.Bow.LobArc },
                    new AttackStep { Name = "Snap", DamageMultiplier = 1f, IntervalMultiplier = 1f,
                                     Motion = AttackMotion.SnapDraw },
                    new AttackStep { Name = "Canted", DamageMultiplier = 1f, IntervalMultiplier = 1f,
                                     Motion = AttackMotion.CantedDraw },
                    new AttackStep { Name = "Twin", DamageMultiplier = 1f, IntervalMultiplier = 1f,
                                     Motion = AttackMotion.TwinDraw, TwinShot = true },
                };
            if (DevBowPair == 3) return hit == 0 ? _devBow[3] : _devBow[4];
            if (DevBowPair == 4) return hit == 0 ? _devBow[5] : _devBow[6];
            if (DevBowPair == 5) return hit == 0 ? _devBow[3] : _devBow[7];
            if (hit == 0) return _devBow[0];
            return DevBowPair == 2 ? _devBow[2] : _devBow[1];
        }

        static AttackStep _devBowArt, _devBowArtFrom;

        /// <summary>The moveset's art, drawn from the kneel - only an ordinary drawn shot.</summary>
        static AttackStep DevBowArt(AttackStep art)
        {
            if (DevBowPair == 5) return art;   // TWIN NOCK: the plain draw, from the bow held ready
            if (art == null || art.Motion != AttackMotion.Draw || art.Swarm || art.Formation || art.Boomerang)
                return art;
            var motion = DevBowPair == 4 ? AttackMotion.CantedDraw
                       : DevBowPair == 3 ? AttackMotion.HighDraw
                       : DevBowPair == 2 ? AttackMotion.RecoilDraw : AttackMotion.KneelDraw;
            if (_devBowArtFrom != art || _devBowArt == null || _devBowArt.Motion != motion)
            {
                _devBowArtFrom = art;
                _devBowArt = art.Clone();
                _devBowArt.Motion = motion;
            }
            return _devBowArt;
        }

        /// <summary>True when the next swing is the chain's last basic - its finisher's lead-in.</summary>
        public bool LeadInNext => !FinisherNext && ComboIndex >= BasicsPerChain - 1;

        /// <summary>Icon for the pending swing: a generic basic, or this finisher's own glyph.</summary>
        public Combat.Glyph NextGlyph =>
            FinisherNext ? ActiveMoveset.FinisherGlyph : Combat.Glyph.Basic;

        /// <summary>Supplied by the game so gear wear and its penalties apply.</summary>
        public System.Func<float> DamageDealtMultiplier;

        /// <summary>
        /// Supplied by the game: the armour-wear penalty (Durability.DamageTakenMultiplier)
        /// composed with Resilience's own reduction. Read by both Health.Vulnerability and the
        /// HUD's armour readout, so the two can never disagree about what a hit actually costs -
        /// which is exactly the failure mode this replaces: the HUD used to reconstruct this
        /// number from the condition fraction alone, using its own copy of the wear formula,
        /// while the real multiplier it was describing was never wired up to anything at all.
        /// </summary>
        public System.Func<float> IncomingDamageMultiplier;

        /// <summary>Supplied by the game: worn armour's penalty alone (Durability.DamageTakenMultiplier),
        /// which IncomingDamageMultiplier leaves out while it lands outside the floor instead.</summary>
        public System.Func<float> ArmourWearMultiplier;

        /// <summary>Corrosion (Rust's Nigredo): worn armour's penalty can't be mitigated - it lands
        /// outside the floor, so a capped build feels it too.</summary>
        public bool WearOutsideFloor => Effects != null && Effects.WearOutsideFloor;

        float WearOutside => WearOutsideFloor ? (ArmourWearMultiplier?.Invoke() ?? 1f) : 1f;

        /// <summary>The armour readout the HUD shows: the mitigated multiplier and, under Corrosion,
        /// the wear that moved outside it - the same pieces Vulnerability composes.</summary>
        /// <remarks>The run clock's HEAT included (Core.RunClock - 1 outside the Short Descent): the
        /// readout is what an ordinary hit really costs now.</remarks>
        public float IncomingReadout => (IncomingDamageMultiplier?.Invoke() ?? 1f) * WearOutside
                                        * (Core.RunClock.Current != null ? Core.RunClock.Current.Heat : 1f);

        public System.Action OnWeaponUsed;

        /// <summary>Every attack the player starts, and whether it is a finisher - fired as the
        /// attack begins, before any branch resolves it. Cosmetic listeners only (the Sniper's
        /// cylinder, see CylinderSpin).</summary>
        public System.Action<bool> AttackStarted;

        /// <summary>
        /// Fraction of damage dealt that is returned as health, from the mastery grid's
        /// per-element lifesteal nodes. Supplied by the game, because the controller has no
        /// business knowing what a mastery grid is.
        ///
        /// Read per HIT rather than per swing, so a cleaving finisher through a crowd heals for
        /// each body it reaches - that is what makes lifesteal worth building toward on the
        /// wide movesets rather than being a flat trickle.
        /// </summary>
        public System.Func<float> LifestealFraction;

        float _comboTimer;
        float _lockTimer;

        /// <summary>
        /// True through the wind-up of a charged strike.
        ///
        /// Deliberately does NOT lock the player. You keep full movement and the auto-targeter
        /// keeps steering you, so the slam lands wherever you are facing when it releases -
        /// carrying the wind-up to the fight rather than praying it comes to you. Only the strike
        /// itself commits, like every other finisher.
        /// </summary>

        /// <summary>Damage multiplier applied while <see cref="ExposedSeconds"/> is running.</summary>
        float _exposedMultiplier = 1f;
        float _exposedCue;

        public Vector2 Facing { get; private set; } = Vector2.right;
        public PlayerTargeting Targeting { get; private set; }

        /// <summary>
        /// Set when the finisher now locking the player is an ordinary swing - one whose hit
        /// landed on the press (Dispatch). Its facing is held only until the rig has DRAWN the
        /// strike (ICharacterRig.PastStrike), then follows the target again while the movement
        /// lock runs on; every other lock (charges, leaps, lunges, thrown blades, a stun) holds
        /// the facing for its whole length, because its hit resolves later along it. The user's
        /// call (2026-10-14): facing is free until the strike.
        /// </summary>
        bool _lockFreesAfterStrike;

        /// <summary>
        /// THE SETTLE IS THE PLAYER'S (the user's call, 2026-10-21): set where an art's hit is out
        /// by the time its strike is drawn - ordinary swings, Riposte, the charges, the leap's
        /// landing, Impale, Cross Cut, the thrown disc arts. Once the strike is drawn and
        /// Tuning.Finisher.SettleMoveSeconds have passed, MOVING ends the lock and the rig lets go
        /// of the follow-through (ICharacterRig.ReleaseSettle); the next attack still waits for the
        /// settle's end. Arts that hold the player until something comes home (the hook, Tether,
        /// Gyre, Plow, the Black Diamond gathers) never set it, nor does Rooted. See SettleRelease.
        /// </summary>
        bool _settleIsFree;

        /// <summary>
        /// HOLD TO AIM, RELEASE TO FIRE (the user's calls, 2026-10-14). A fresh press on a banked
        /// art the hand can aim (AimsArt) does not fire on the press: released inside
        /// Tuning.Combo.ArtAimHoldSeconds it was a tap and the art goes at the auto-target;
        /// held past it the aim arrow appears and the stick, mouse or touch drag (Controls.Aim)
        /// points the art until the button is let go, which fires it - then the timing bar runs
        /// as ever, the direction already fixed. Any OTHER button pressed while holding cancels:
        /// the art stays banked, nothing spent, and that press does nothing else
        /// (Controls.HoldingArt). A button held since
        /// before the art banked (hold-to-attack) fires it on the hold path, unaimed.
        /// </summary>
        bool _artHolding;

        /// <summary>Seconds the current aim hold has lasted.</summary>
        float _artHeldFor;

        /// <summary>The hand's aim for the art, set while held past the threshold and kept
        /// through its wind-up to the strike - see UpdateArtAim.</summary>
        Vector2? _artAim;

        /// <summary>Held past the threshold: the arrow is up (AimIndicator).</summary>
        public bool ArtAimShown => _artHolding && _artHeldFor >= Tuning.Combo.ArtAimHoldSeconds;

        /// <summary>Whether the hand has aimed the art - held now, or fired and still winding up.
        /// The facing follows the aim; TargetHighlight judges along it.</summary>
        public bool AimingArt => _artAim.HasValue && (_artHolding || _barLive);

        /// <summary>Whether an art the hand can aim is pending - a touch drag aims only then.</summary>
        public bool ArtAimPossible => FinisherNext && AimsArt(ActiveMoveset?.Finisher);

        void UpdateArtAim()
        {
            if (ArtAimShown)
            {
                var aim = Core.Controls.Aim;
                if (aim.HasValue) _artAim = aim.Value;   // latched: letting the stick go keeps it
            }
            else if (!_artHolding && !_barLive) _artAim = null;
        }

        /// <summary>Drop a hold without firing - the art stays banked.</summary>
        void CancelArtHold()
        {
            _artHolding = false;
            _artAim = null;
        }

        /// <summary>
        /// The ground the aimed art will strike, along the facing - what AimIndicator draws. Read
        /// off the same rules that resolve each hit (change one, change this with it): the swing's
        /// capsule, the thrown blade's strip, the Magnum Opus crescent growing as it flies,
        /// Quintessence's cone narrowing to its point, a disc's throw. False for an art that hits
        /// all round (nothing to aim but the body) or when no art is banked.
        /// </summary>
        public bool TryArtFootprint(out ArtFootprint fp)
        {
            fp = default;
            var art = ActiveMoveset?.Finisher;
            if (art == null || !FinisherNext) return false;
            if (art.ThrowsWeapon)
                fp = new ArtFootprint(art.ThrowDistance, ThrownBladeWidth, ThrownBladeWidth, true);
            else if (art.MagnumOpus)
                fp = new ArtFootprint(Tuning.MagnumOpus.BeamRange,
                    Tuning.MagnumOpus.BeamSpan * Tuning.MagnumOpus.BeamGrowFrom,
                    Tuning.MagnumOpus.BeamSpan * Tuning.MagnumOpus.BeamGrowTo, false);
            else if (art.Plows)
                fp = new ArtFootprint(Tuning.Plow.Reach, (Tuning.Plow.LaneHalf + Tuning.Plow.LaneRadius) * 2f,
                                      (Tuning.Plow.LaneHalf + Tuning.Plow.LaneRadius) * 2f, false);
            else if (art.Gyres)
                fp = new ArtFootprint(Tuning.Gyre.Reach, 0.4f,
                                      2f * Tuning.Gyre.Reach * Mathf.Tan(Tuning.Gyre.HalfAngle * Mathf.Deg2Rad), false);
            else if (art.Tethers)
                fp = new ArtFootprint(Tuning.Tether.Reach, Tuning.Tether.TagRadius * 2f,
                                      Tuning.Tether.TagRadius * 2f, true);
            else if (art.Grapples)
                fp = new ArtFootprint(Tuning.Grapple.Reach, Tuning.Grapple.HookRadius * 2f,
                                      Tuning.Grapple.HookRadius * 2f, true);
            else if (art.Guards)
            {
                float w = Mathf.Max(0.1f, art.StrikeWidth * Mods.StrikeWidthMul);
                fp = new ArtFootprint(CurrentRange + Tuning.Riposte.CounterStep, w, w, true);
            }
            else if (art.LeapDistance > 0f)
                fp = new ArtFootprint(art.LeapDistance, 0.5f, 0.5f, true);
            else if (art.PiercesPath)
            {
                float w = Mathf.Max(0.1f, art.StrikeWidth * Mods.StrikeWidthMul);
                fp = new ArtFootprint(CurrentRange + art.LungeDistance, w, w, true);
            }
            else if (art.AreaOfEffect) return false;
            else if (art.DiscThrows > 0 || ThrowsInsteadOfSwinging)
                fp = new ArtFootprint(ThrowReach, DiscThrowWidth, DiscThrowWidth, true);
            else if (art.ReverseCones)
                fp = new ArtFootprint(CurrentRange, CurrentRange * Tuning.Quintessence.ConeBaseWidthFraction, 0f, false);
            else
            {
                float w = Mathf.Max(0.1f, art.StrikeWidth * Mods.StrikeWidthMul);
                fp = new ArtFootprint(CurrentRange, w, w, true);
            }
            return fp.Reach > 0f;
        }

        // Twice ThrownBlade.HitRadius (an instance default, 0.55) and ThrownDisc's 0.28 catch.
        const float ThrownBladeWidth = 1.1f, DiscThrowWidth = 0.56f;

        /// <summary>The aim indicator - attached on first use, a plain reference (survives a reload).</summary>
        [SerializeField] AimIndicator _aimIndicator;

        /// <summary>
        /// Arts the stick can aim: every one whose cut, thrust, cone, beam or throw goes along the
        /// facing - and the ones that hit all round, where it only turns the body. Not the arts that
        /// SEEK the locked target (Prima Materia's throw, Swarm, King and Queen's pincer) nor a
        /// bow's shot, which flies at the target whatever the facing says.
        /// </summary>
        bool AimsArt(AttackStep art) =>
            art != null && Weapon != Art.Gear.WeaponClass.Bow
            && !art.Boomerang && !art.Swarm && !art.Pincer && !art.Formation;

        /// <summary>The right stick's direction when it is aiming, else <paramref name="fallback"/>
        /// - the Dash and the Barrier take it (the user's call, 2026-10-14).</summary>
        Vector2 AimedOr(Vector2 fallback) => Core.Controls.Aim ?? fallback;

        /// <summary>The enemy currently locked on, if any.</summary>
        public Combat.Health Target => Targeting != null ? Targeting.Target : null;
        public bool IsMoving { get; private set; }

        /// <summary>
        /// Whether the player is ASKING to move, read before the animation lock takes the stick
        /// away. <see cref="IsMoving"/> is what the body is actually doing.
        ///
        /// The two are the same except during a finisher, and the gap between them is the whole
        /// reason this exists: a locked swing reports the character as stationary, which for an
        /// element that measures stillness is indistinguishable from the player choosing to stand
        /// there. It is not the same thing at all - one is a decision and the other is the game
        /// holding the stick - and only the element concerned can say whether the difference
        /// matters to it. Earth reads IsMoving and is right to; air reads this.
        /// </summary>
        public bool MoveIntent { get; private set; }
        /// <summary>How ready the next swing is, 0 to 1, against the gate it is waiting on.</summary>
        public float AttackCooldown01
            => Mathf.Clamp01(1f - _cooldown / Mathf.Max(0.0001f, CurrentInterval));

        float CurrentInterval => IntervalFor(NextStep);

        /// <summary>
        /// How long until the swing AFTER this step. Takes the step explicitly rather than reading
        /// _cooldown: that only holds the interval because Update assigns it one line before
        /// calling DoAttack, and anything else invoking DoAttack silently got a zero-length window
        /// and collapsed every animation to its minimum.
        /// </summary>
        float IntervalFor(AttackStep step)
        {
            // GlobalAttackTempo scales the whole baseline; the element's own buff rides on top of
            // it. Both are in the denominator, so tempo 0.8 with a 1.3x element buff lands at an
            // effective 1.04x - back above the original cadence, which is the point: the dial
            // lowers the floor without capping how fast a ramped character can end up.
            // The character's own attack-speed stat sits alongside the element buff and the
            // ledger. Points, not a multiplier - StatPercents.Apply floors it so a stack of
            // negatives can slow the swing without ever inverting the interval.
            // The element's attack speed is a head start inside the stat curve now, not a factor
            // on top of it (AttackSpeedPointsNow).
            float statSpeed = Art.Gear.StatPercents.Apply(Tuning.Player.AttackSpeedPercent,
                                                          AttackSpeedPointsNow) / 100f;
            // The run layer: the ledger's attack speed, and what its conditions take right now
            // (Quicksand) bent with it - see Exchange.Mods.Factor.
            var ledger = Mods;
            float run = ledger.AttackSpeedMul
                        * (Effects != null ? ledger.Factor(Art.Gear.StatKind.AttackSpeed, 0f, Effects.AttackSpeedDown) : 1f);
            float speed = Mathf.Max(0.1f, GlobalAttackTempo * run * statSpeed);

            // The disc's two halves have different cadences, and that difference IS the class.
            // The bow has one cadence but it is deliberately the slowest in the game - see
            // Tuning.Bow.AttackIntervalMul's own note on why it must clear the disc's melee.
            float classMul = Weapon == Art.Gear.WeaponClass.Disc
                ? (ThrowsInsteadOfSwinging ? Tuning.Disc.ThrowIntervalMul : Tuning.Disc.MeleeIntervalMul)
                : Weapon == Art.Gear.WeaponClass.Bow ? Tuning.Bow.AttackIntervalMul
                : 1f;

            return BaseAttackInterval * step.IntervalMultiplier * classMul / speed;
        }
        float CurrentRange => (Art.Gear.StatPercents.Apply(BaseRange, Stats.Range)
                              + NextStep.RangeBonus
                              + (Resource?.AttackRangeBonus ?? 0f)) * Mods.RangeMul
                              * (Weapon == Art.Gear.WeaponClass.Disc ? Tuning.Disc.RangeMul
                               : Weapon == Art.Gear.WeaponClass.Bow ? Tuning.Bow.RangeMul
                               : 1f)
                              * (NextStep.AreaOfEffect ? AoeScale : 1f);

        // ---- gear stats read at the moment of the hit ----

        /// <summary>Crit chance from every source, summed: the universal base, the element's own,
        /// gear's CritChance points, the ledger's bonus and the perfect streak.</summary>
        float CritChanceSum => Tuning.Player.BaseCritChance + (Resource?.CritChance ?? 0f)
                               + Stats.CritChance / 100f + Mods.BonusCrit + StreakCritBonus;

        /// <summary>The element's crit multiplier plus gear's CritDamage points.</summary>
        float CritMulSum => Tuning.Player.BaseCritMultiplier + CritDamagePointsNow / 100f + Mods.CritDamageAdd;

        /// <summary>
        /// Crit as ONE pool (Art.Gear.StatCurves.Crit): the chance held to the same cap for every
        /// element, and whatever lies past it added to the multiplier - an element with a head
        /// start reaches the cap sooner, it never gets a higher one.
        /// </summary>
        float CritChanceNow => Art.Gear.StatCurves.Crit(CritChanceSum, CritMulSum).Chance;
        float CritMul => Art.Gear.StatCurves.Crit(CritChanceSum, CritMulSum).Multiplier;

        /// <summary>
        /// One swing's or throw's place in its damage range: Tuning.Attack.DamageSpread either side
        /// of the average, the BOTTOM raised toward the top by Accuracy - 100 points, every hit at
        /// the top. Rolled once per swing or throw, like a disc's crit, so one swing reads as one
        /// number across every body it catches.
        /// </summary>
        float DamageRoll()
        {
            float spread = Tuning.Attack.DamageSpread;
            float accuracy = Mathf.Clamp01(Stats.Accuracy / 100f);
            return Random.Range(1f - spread + 2f * spread * accuracy, 1f + spread);
        }

        /// <summary>Rolls one crit. Returns the damage multiplier it earned (1 on a miss).</summary>
        float RollCrit(out bool crit) => RollCrit(null, out crit);

        /// <summary>
        /// Rolls one crit against a target: the ledger may add to this hit's chance (Honed, Stoop,
        /// Cementation) - into the one pool, so past the cap it is crit damage - and decides what a
        /// crit is worth (Cold Iron). Returns the multiplier.
        /// </summary>
        float RollCrit(Health target, out bool crit, bool isFinisher = false)
        {
            float bonus = Effects != null && target != null ? Effects.BonusCritChance(target, isFinisher) : 0f;
            var (chance, multiplier) = Art.Gear.StatCurves.Crit(CritChanceSum + bonus, CritMulSum);
            crit = Random.value < chance;
            if (!crit) return 1f;
            return Effects != null ? Effects.CritMultiplier(multiplier) : multiplier;
        }

        /// <summary>A crit roll for a hit that may be a weapon art: one settled PERFECT under the
        /// board's Cinnabar crits without rolling.</summary>
        float RollCritFor(bool isFinisher, out bool crit) => RollCritFor(isFinisher, null, out crit);

        float RollCritFor(bool isFinisher, Health target, out bool crit)
        {
            if (isFinisher && (Mods.ArtsCantCrit || (Effects != null && Effects.SocketCantCrit(ArtSocket))))   // Slag, Brittle Script
            {
                crit = false;
                return 1f;
            }
            if (isFinisher && _strikeCrit)
            {
                crit = true;
                return CritMultiplierNow;
            }
            return RollCrit(target, out crit, isFinisher);
        }

        /// <summary>What a crit is worth right now, after the ledger (Cold Iron) - what a repeat
        /// made a crit (Gemini) and a release that crits (Grand Elixir) multiply by.</summary>
        public float CritMultiplierNow
        {
            get
            {
                float m = Art.Gear.StatCurves.Crit(CritChanceSum, CritMulSum).Multiplier;
                return Effects != null ? Effects.CritMultiplier(m) : m;
            }
        }

        float FinisherPowerMul => Art.Gear.StatPercents.Apply(1f, Stats.FinisherPower);

        float SplashFraction => Stats.Splash / 100f;
        float PierceFraction => Weapon == Art.Gear.WeaponClass.Bow ? Stats.Pierce / 100f + Mods.PierceAdd : 0f;

        /// <summary>Radius multiplier for area-of-effect attacks.</summary>
        float AoeScale => Art.Gear.StatPercents.Apply(1f, Stats.AoeRadius) * Mods.AreaMul;

        /// <summary>Radius multiplier on every area attack and release right now - the character's
        /// Area and the run's together.</summary>
        public float AreaScaleNow => AoeScale;

        /// <summary>
        /// A per-body falloff with Cleave applied: Cleave shrinks what is LOST per extra body
        /// (0.85 keeps 15% loss at zero points, less as points climb), never pushes the keep
        /// fraction past 1.
        /// </summary>
        float CleaveFalloff(float falloff)
            => 1f - (1f - falloff) * Art.Gear.StatPercents.ReductionFactor(Stats.Cleave + Mods.CleavePoints);

        Rigidbody2D _rb;
        float _cooldown;

        /// <summary>This swing's quickening (the board's Realgar), 1 when none - read once by
        /// DoAttack for the animation after the gate has used it.</summary>
        float _quickSwing = 1f;

        /// <summary>Set by Fumbler: this swing plays out but touches nothing.</summary>
        bool _swingWhiffed;

        Vector2 _dashVelocity;
        float _dashSeconds;
        bool _dashStopping;

        /// <summary>
        /// Shove the character in a direction for a moment, overriding the stick.
        ///
        /// Driven through the Rigidbody's velocity rather than by writing the transform, so walls
        /// and bodies still stop it - a recoil that slid the player through the arena wall would
        /// be worse than no recoil. Input is ignored for the duration; a dash you can steer out of
        /// is not displacement, it is a speed boost.
        /// </summary>
        public void Dash(Vector2 direction, float speed, float seconds)
        {
            if (direction.sqrMagnitude < 0.0001f || seconds <= 0f) return;

            // Damping is suspended for the duration. The body carries linearDamping 6, which
            // takes about 11% out of every physics step - so a dash asking for 3.2 units actually
            // travelled 2.86, and no amount of fixing the loop was going to close that gap.
            // A dash is a scripted displacement, not an impulse, so the number in the moveset
            // should be the distance the character moves.
            if (_rb != null && _dashSeconds <= 0f) _dampingBeforeDash = _rb.linearDamping;

            _dashVelocity = direction.normalized * speed;
            _dashSeconds = seconds;
            if (_rb != null) _rb.linearDamping = 0f;
        }

        float _dampingBeforeDash;

        // ---------------------------------------------------------------- chasms
        //
        // THE EDGE HOLDS A WALK; A DASH CROSSES. Walking, the part of the velocity that would
        // carry the body over the lip is trimmed away, so the player slides along the edge
        // rather than sticking to it. A dash (or a leap) is not held - a two-deep rift is
        // crossable - and where it ENDS is judged: within Tuning.Chasm.Forgiveness of ground it
        // is set down on the lip, past that it FALLS: a share of max HP and back to the last
        // ground stood on. Enemies never knock the player back, so a fall is always the
        // player's own movement.

        Vector2 _lastSafe;
        bool _haveSafe;

        void TrackSafeGround()
        {
            if (Airborne) return;
            var p = _rb.position;
            if (!Arena.HasChasms) { _lastSafe = p; _haveSafe = true; return; }
            // Anything that got the body over the void without a dash (a shove from a hazard,
            // a teleport that missed) is judged like a dash's landing.
            if (Arena.DeepInChasm(p, 0f)) { SettleOverChasm(); return; }
            if (!Arena.ChasmNear(p, Tuning.Chasm.EdgeMargin * 2f)) { _lastSafe = p; _haveSafe = true; }
        }

        void HoldChasmEdge()
        {
            if (!Arena.HasChasms || Airborne) return;
            var p = _rb.position;
            float margin = Tuning.Chasm.EdgeMargin;
            // Already at the lip (set down there by the forgiveness): any step is allowed that
            // does not go deeper, so the player can always walk back out.
            if (Arena.ChasmNear(p, margin)) return;

            float dt = Time.fixedDeltaTime;
            var v = _rb.linearVelocity;
            if (!Arena.ChasmNear(p + v * dt, margin)) return;
            var along = new Vector2(v.x, 0f);
            if (!Arena.ChasmNear(p + along * dt, margin)) { _rb.linearVelocity = along; return; }
            along = new Vector2(0f, v.y);
            if (!Arena.ChasmNear(p + along * dt, margin)) { _rb.linearVelocity = along; return; }
            _rb.linearVelocity = Vector2.zero;
        }

        void SettleOverChasm()
        {
            if (!Arena.HasChasms) return;
            var p = _rb.position;
            if (!Arena.DeepInChasm(p, 0f)) return;

            // Forgiveness is measured to the LIP itself (standing room at no margin); the body
            // is then set down a little back from it. Measured to the set-down point instead, a
            // dash ending a hand past the edge was charged a fall.
            var edge = Arena.NearestFloor(p, 0.02f);
            if (Vector2.Distance(edge, p) <= Tuning.Chasm.Forgiveness)
            {
                _rb.position = Arena.NearestFloor(p, 0.3f);
                _rb.linearVelocity = Vector2.zero;
                return;
            }

            var back = _haveSafe ? _lastSafe : Arena.NearestFloor(p, 0.5f);
            if (Arena.ChasmIsWater) Hazards.LakeSurface.Splash(p);
            else Spr.Flash(p, 1.0f, new Color(0.25f, 0.22f, 0.3f), 0.35f);
            _rb.position = back;
            transform.position = back;
            _rb.linearVelocity = Vector2.zero;
            Spr.Flash(back, 1.1f, Color.white, 0.25f);
            // A MECHANIC hit: a share of max HP through the usual mitigation.
            if (Health) Health.Take(new DamageInfo(Health.Max * Tuning.Chasm.PlayerFallFraction, ElementType.Earth, null) { IgnoresHeat = true });
            Debug.Log($"[Chasm] the player fell at {p}, back to {back}");
        }

        /// <summary>
        /// Remaining grace period before a HELD (not freshly tapped) button may fire the next
        /// swing. Reset to <see cref="HoldExtraDelay"/> every time any attack fires, tapped or
        /// held, so only the NEXT hold-triggered swing pays it.
        /// </summary>
        float _holdDelay;

        [Tooltip("Extra pause a HELD attack button waits, on top of the swing's own interval, " +
                 "before auto-firing again. A fresh tap ignores this entirely and fires the " +
                 "instant the previous swing concludes - the delay exists so that lazily holding " +
                 "the button down settles into a visibly slower rhythm than deliberately timing " +
                 "taps, without capping how fast a player who taps on the beat can actually go.")]
        public float HoldExtraDelay = Tuning.Combo.HoldExtraDelay;

        /// <summary>
        /// Remaining life of a remembered attack tap. See <see cref="Tuning.Combo.TapBufferSeconds"/>
        /// - set on the press edge, spent by whichever gate opens first, and never extended by
        /// holding (the edge is what fills it, so a held button fills it exactly once).
        /// </summary>
        float _tapBuffer;

        /// <summary>
        /// Seconds left in which an attack tap still belongs to the timing bar of the throw that
        /// just left the hand, not to the blade. The bar ends AT the launch and stays on screen
        /// for its linger, so a press made for its last band can land a frame after the blade is
        /// out - without this it recalled a throw the player was still timing.
        /// </summary>
        float _throwTapGuard;

        [Tooltip("How long a fresh attack tap is remembered if it arrives before the gate opens. " +
                 "Absorbs input jitter only. A buffered tap only ever fires LATER than it was pressed.")]
        public float TapBufferSeconds = Tuning.Combo.TapBufferSeconds;
        Camera _cam;
        Art.ActorVisual _visual;
        /// <summary>
        /// The paper-doll this controller drives.
        ///
        /// Backed by a SERIALIZED MonoBehaviour reference, not held only as the interface. Unity
        /// cannot serialize an interface-typed field, so a domain reload - which is what an edit
        /// to any script during play mode triggers - nulled this while the rig GameObject itself
        /// survived. The character carried on being drawn, so nothing looked broken, but the
        /// controller had silently lost its handle: no attack animation, no hiding the weapon on
        /// a throw, no dual-disc split. An Object reference does survive, so the interface is
        /// re-derived from it on first use.
        /// </summary>
        public Art.Gear.ICharacterRig Rig => _rig ??= _rigBehaviour as Art.Gear.ICharacterRig;

        Art.Gear.ICharacterRig _rig;
        [SerializeField] MonoBehaviour _rigBehaviour;

        public int Kills { get; private set; }

        void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            Health = GetComponent<Health>();
            _status = StatusEffects.Get(gameObject);
            _cam = Camera.main;
            _visual = GetComponentInChildren<Art.ActorVisual>();
            Targeting = GetComponent<PlayerTargeting>();

            // Applied again after LockWeaponClass runs (see there) - Awake alone isn't enough,
            // since BuildPlayer calls LockWeaponClass straight after AddComponent and it resets
            // every unlocked slot to the class default, silently undoing this.
            ApplyForceMovesetOverride();
        }

        /// <summary>
        /// Test harness: pin every slot to one moveset (<see cref="Tuning.Testing.ForceMovesetId"/>)
        /// so the rotation cannot wander onto a different move between swings.
        ///
        /// Called from <see cref="Awake"/>, the end of <see cref="LockWeaponClass"/>, AND again
        /// by <c>GameBootstrap.BuildPlayer</c> after it locks in a black-diamond signature - each
        /// one mutates <see cref="Slots"/> after the one before it, so each has to reapply this or
        /// silently undo it. The point of the override is that NOTHING can change the loadout
        /// while it is on, and that includes the weapon-class lock and a signature finisher both -
        /// a real signature is exactly the kind of thing "add the finisher to all three slots"
        /// testing wants out of the way, not a case this should lose to.
        /// </summary>
        public void ApplyForceMovesetOverride()
        {
            if (string.IsNullOrEmpty(Tuning.Testing.ForceMovesetId)) return;

            var forced = MovesetLibrary.ById(Tuning.Testing.ForceMovesetId)
                         ?? (Tuning.Testing.ForceMovesetId == "default"
                             ? MovesetLibrary.Default : null);
            if (forced != null)
            {
                for (int i = 0; i < Slots.Count; i++) Slots[i] = forced;
                Debug.Log($"[Convergence] TEST: every slot forced to '{forced.Id}'" +
                          (Tuning.Testing.EverySwingIsFinisher
                              ? $", every swing plays its finisher ({forced.Finisher.Name})."
                              : "."));
            }
            else
            {
                Debug.LogWarning($"[Convergence] TEST: no moveset with id " +
                                 $"'{Tuning.Testing.ForceMovesetId}' - loadout left alone.");
            }
        }

        public void Equip(ElementalResource resource)
        {
            Resource = resource;
            Resource.Bind(this);
        }

        public void SetRig(Art.Gear.ICharacterRig rig)
        {
            _rig = rig;
            // Both rig implementations are MonoBehaviours; anything else would still work for
            // the rest of this session and simply not survive a reload.
            _rigBehaviour = rig as MonoBehaviour;
        }
        public void RegisterKill() => Kills++;

        /// <summary>
        /// Hand the chain straight back to a finisher. Ouroboros's refund.
        ///
        /// Sets the counter rather than adding to it, so a refund while a finisher is already
        /// banked is a no-op instead of overshooting into a state FinisherNext would read as
        /// several finishers deep.
        /// </summary>
        public void RefundFinisher() => ComboIndex = Mathf.Max(ComboIndex, BasicsPerChain);

        /// <summary>Unshackled: the chain moves on one basic - never past a banked art - and its
        /// clock starts again, as a swing's would.</summary>
        public void AdvanceChain()
        {
            if (FinisherNext) return;
            ComboIndex++;
            _comboTimer = ComboResetSeconds * Mods.ComboTimeMul;
        }

        // ---- the mastery board, for hits that land away from the swing ----

        /// <summary>A thrown hit (a disc, an arrow, the thrown blade) about to land: the ledger's
        /// and the board's target rules, exactly as ResolveArc applies them to a swing.
        /// <paramref name="reach01"/> is how far out it lands as a share of the throw's range.</summary>
        public float ScaleThrownHit(Health target, float damage, bool isFinisher, bool crit, float reach01 = 0.5f,
                                    bool first = true)
        {
            if (target == null) return damage;
            if (Effects != null)
            {
                damage = Effects.ModifyOutgoing(target, damage, new Exchange.HitContext
                {
                    IsFinisher = isFinisher, Reach01 = reach01, Thrown = true, First = first,
                });
                // Gilding: a thrown basic rolled its crit at the throw; on a gilded body it gets the
                // gild's chance again, here, as a crit of its own.
                if (!isFinisher && !crit)
                {
                    float gild = Effects.GildedCrit(target);
                    if (gild > 0f && Random.value < gild) damage *= CritMultiplierNow;
                }
                PierceArmourFor(target);
            }
            return Board != null ? Board.ModifyOutgoing(target, damage, isFinisher, crit) : damage;
        }

        /// <summary>Keen Edge: the next hit on this body spends its armour faster.</summary>
        void PierceArmourFor(Health target)
        {
            if (Effects == null || target == null) return;
            float shred = Effects.ArmourShred;
            if (shred <= 1f) return;
            var shell = target.GetComponent<Combat.EnemyArmor>();
            if (shell != null && shell.Current > 0f) shell.ShredNextHit = shred;
        }

        /// <summary>
        /// A thrown hit landed. The board and Sulfur's Season hear it as they hear a swing -
        /// Season used to be swing-only, so a disc thrown or an arrow loosed never marked
        /// anything. <paramref name="first"/> is the projectile's first body; <paramref name="reach01"/>
        /// how far out it landed as a share of the throw's range.
        /// </summary>
        public void NotifyThrownHit(Health target, DamageInfo info, float reach01, bool first, int basicsWorth = 1)
        {
            if (target == null) return;
            Effects?.OnHitLanded(target, info, new Exchange.HitContext
            {
                IsFinisher = info.IsFinisher, Reach01 = reach01, Thrown = true, First = first,
                Proc = Weapon == Art.Gear.WeaponClass.Disc ? Tuning.Exchange.ProcDisc : 1f,
            });
            Board?.OnHitLanded(target, info, info.IsFinisher, reach01, first);
            if (info.IsFinisher) return;
            for (int i = 0; i < basicsWorth; i++)
            {
                if (first) Board?.OnBasicLanded();
                if (!target.IsDead) Principles?.OnBasicLanded(target);
            }
            // (A twin shot's second arrow is worth 0 basics - the pair's one basic is the first's.)
        }

        // ---- statuses the player applies, through the board when there is one ----
        //
        // Every element's own status goes through these, so the board's status POWER and its rules
        // (Aqua Fortis, Orpiment, Antimony, Congelation) reach the element's abilities as well as
        // the humour keystones - a Water player's own soak is the soak Soak Power strengthens.

        public void Burn(Health target, float dps, float seconds)
        {
            Effects?.BeforeStatus(target, burning: true, soaking: false);   // Steam, a union
            if (Effects != null) dps *= Effects.BurnGrowth;                  // Kindling, grown
            if (Board != null) Board.Burn(target, dps, seconds);
            else if (target != null) StatusEffects.Get(target.gameObject).ApplyBurn(dps, seconds, gameObject);
        }

        public void Soak(Health target, float seconds, float vulnerability)
        {
            // Saturate (the exchange's status layer): a soak landing on a soaked body staggers it.
            bool already = target != null && Effects != null && Effects.Saturates
                           && target.GetComponent<StatusEffects>() is { Soaked: true };
            Effects?.BeforeStatus(target, burning: false, soaking: true);   // Steam, a union
            if (Effects != null) seconds += Effects.SoakGrowth;                 // Swell, grown
            if (Board != null) Board.Soak(target, seconds, vulnerability);
            else if (target != null) StatusEffects.Get(target.gameObject).ApplySoak(seconds, vulnerability);
            if (already && !target.IsDead) Stagger(target, Tuning.Exchange.SaturateStagger);
        }

        public void Stagger(Health target, float seconds)
        {
            Effects?.OnStaggering(target);   // Clot, a union
            if (Board != null) Board.Stagger(target, seconds);
            else if (target != null) StatusEffects.Get(target.gameObject).ApplyStagger(seconds);
        }

        public void Bleed(Health target, float dps, float seconds)
        {
            if (Board != null) Board.Bleed(target, dps, seconds);
            else if (target != null) StatusEffects.Get(target.gameObject).ApplyBleed(dps, seconds, gameObject);
        }

        // ---- what the run's ledger asks of the body ----

        /// <summary>
        /// A release's direct hit on an enemy (a burst, a shock, a pool's or a vortex's tick),
        /// through the ledger: it can crit (Grand Elixir), it staggers (Cataclysm), lifesteal drinks
        /// from it (Transfusion). Every element's release lands through here.
        /// </summary>
        public void ReleaseHit(Health target, DamageInfo info)
        {
            if (target == null || target.IsDead) return;
            if (Effects != null && Effects.ReleasesCrit)
            {
                float mul = RollCrit(target, out bool crit);
                if (crit) { info.Amount *= mul; info.Crit = true; }
            }
            target.Take(info);
            if (Effects == null) return;
            if (Effects.ReleasesStagger && !target.IsDead) Stagger(target, Tuning.Exchange.CataclysmStagger);
            if (Effects.LifestealFromAll) Drain(info.Amount);
        }

        /// <summary><see cref="ReleaseHit"/> for a hit whose owner may or may not be the player - a
        /// pool, a vortex, a burst. Anyone else's lands as a plain hit.</summary>
        public static void ReleaseHitFrom(GameObject owner, Health target, DamageInfo info)
        {
            var pc = owner != null ? owner.GetComponent<PlayerController>() : null;
            if (pc != null) pc.ReleaseHit(target, info);
            else target?.Take(info);
        }

        /// <summary>A price the run charges in health (Blood Price, Backfire, Toll): paid straight
        /// off, through no mitigation and no ward, but still past Second Wind.</summary>
        public void PayHealth(float amount)
        {
            if (Health == null || Health.IsDead || amount <= 0f) return;
            Health.Take(new DamageInfo(amount, ElementType.Earth, gameObject) { Price = true });
        }

        /// <summary>
        /// Healing that shares lifesteal's per-second limit (Vital Spark, Pelican) - one healing
        /// budget, so stacking sources cannot out-heal the cap the rebalance set.
        /// </summary>
        public void HealWithinLimit(float amount)
        {
            if (Health == null || Health.IsDead || amount <= 0f) return;
            float perSecond = Art.Gear.StatCurves.LifestealPerSecond(Health.Max);
            if (!float.IsPositiveInfinity(perSecond))
            {
                _lifestealBudget = Mathf.Min(perSecond, _lifestealBudget + perSecond * (Time.time - _lifestealAt));
                _lifestealAt = Time.time;
                amount = Mathf.Min(amount, _lifestealBudget);
                _lifestealBudget -= amount;
            }
            if (amount > 0f) Health.Heal(amount);
        }

        /// <summary>
        /// Ends an art's lock in its SETTLE when the player moves (_settleIsFree). Never mid-dash or
        /// in the air - a lunge's or a hop's travel is part of the move. The attack gate is held to
        /// where the lock would have ended, so only the feet are freed.
        /// </summary>
        void SettleRelease()
        {
            if (!_settleIsFree || !AttackLocked || Rig == null) return;
            if (_dashSeconds > 0f || Airborne || Petrified || Statue) return;
            if (Core.Controls.Move.sqrMagnitude <= 0.01f) return;
            if (Rig.SinceStrike < Tuning.Finisher.SettleMoveSeconds) return;
            _cooldown = Mathf.Max(_cooldown, _lockTimer);
            _lockTimer = 0f;
            _settleIsFree = false;
            Rig.ReleaseSettle();
        }

        /// <summary>Held where you stand: no moving, no turning, no attacking (Recoil, Lapsus).</summary>
        public void Stun(float seconds)
        {
            _lockFreesAfterStrike = false;   // held is held: no turning either
            _settleIsFree = false;
            _lockTimer = Mathf.Max(_lockTimer, seconds);
            _cooldown = Mathf.Max(_cooldown, seconds);
        }

        void Update()
        {
            // No device check. This used to open with `if (kb == null) return;`, which on a phone
            // - where Keyboard.current IS null - silently disabled the entire player: no attacks,
            // no facing, no combo decay, and nothing on screen saying why. Everything below goes
            // through Core.Controls, which has no opinion about what device an action came from.

            UpdateDefensiveAbility(Time.deltaTime);

            // ---- aim: the auto-acquired target, else where we are running ----
            // Aiming is not mouse-driven: this is heading for iOS, where there is no cursor, and
            // sticky auto-target is what makes a twin-stick arena playable on touch.
            _lockTimer -= Time.deltaTime;
            SettleRelease();

            // A statue does nothing at all - no facing, no swing, no release. Its timers wait for
            // it, so a chain or a cooldown resumes where it was when the stone lets go.
            SyncStatue();
            if (Statue) return;

            // Facing is frozen while a finisher commits. Without this the auto-targeter keeps
            // steering the character mid-swing, so a 360 spin or a committed thrust would quietly
            // re-aim itself onto whatever wandered closest - which is exactly the decision the
            // lock exists to make the player own. An ordinary swing's hit is already resolved by
            // the time its strike is drawn, so its facing frees there (_lockFreesAfterStrike).
            //
            // The rig turns toward the facing and chooses its own view from it - the back when
            // aiming up - in combat and out (SetAimViews; the user's call, 2026-10-14). It used to
            // show the back only for untargeted travel, and force the front in a fight.
            Rig?.SetAimViews(true);
            // The mouse aims from where the character stands on screen (Controls.Aim).
            var cam = Camera.main;
            Core.Controls.AimOriginScreen = cam != null ? (Vector2?)(Vector2)cam.WorldToScreenPoint(transform.position) : null;
            UpdateArtAim();
            if (_aimIndicator == null) _aimIndicator = AimIndicator.Attach(this);
            var target = Targeting != null ? Targeting.Target : null;
            bool facingHeld = AttackLocked && !(_lockFreesAfterStrike && Rig != null && Rig.PastStrike);
            if (facingHeld)
            {
                // hold the facing the finisher started on
            }
            else if (AimingArt)
            {
                // Aimed by hand: the hand points a banked or winding-up art (AimsArt, UpdateArtAim).
                Facing = _artAim.Value;
            }
            else if (target != null && !target.IsDead)
            {
                var d = (Vector2)target.transform.position - (Vector2)transform.position;
                if (d.sqrMagnitude > 0.0001f) Facing = d.normalized;
            }
            else if (_rb.linearVelocity.sqrMagnitude > 0.35f)
            {
                Facing = _rb.linearVelocity.normalized;   // nothing to fight: face travel
            }
            // A chakram in one hand is just a chakram. The pair only splits apart when the
            // player is at throwing distance - so the silhouette itself tells you which half of
            // the hybrid you are currently in, without reading the range rings.
            // Split always now (the user's call, 2026-10-15): the melee pairs strike with both
            // hands too. Which half of the hybrid you are in is the target rim's to say.
            // ...except while Carom holds the pair JOINED (combined over the head, hurled as one,
            // until it is home).
            Rig?.SetWeaponSplit(Weapon == Art.Gear.WeaponClass.Disc && !_discJoined);


            // Shoulder the weapon whenever the chain is cold. Asked off the COMBO rather than the
            // swing on purpose - see ICharacterRig.SetGripShouldered - so the grip closes once at
            // the start of a chain and opens once when the chain lapses, instead of re-forming in
            // the gap between every pair of swings.
            //
            // ComboIndex == 0 covers both ways a chain can be cold: never started, or lapsed at
            // ComboResetSeconds. A BANKED finisher (FinisherNext) deliberately keeps the combat
            // grip even though it never expires - the player is holding a loaded shot, and the
            // character standing at ease would say the opposite.
            Rig?.SetGripShouldered(ComboIndex == 0 && !AttackLocked);

            // ---- combo chain decays if you stop swinging, but a READY finisher never does ----
            //
            // Partial progress still lapses: the chain has to be a chain, and two stray swings at
            // a passing enemy should not bank toward a finisher forever. But once the basics
            // are paid for, the finisher is EARNED. Letting it expire punished exactly the play it
            // should reward - lining the shot up, waiting for the pack to close, backing off to
            // heal first - and silently dropped the player back to basics with no tell. It also
            // made a charged finisher impossible, since the wind-up outlasts the timer.
            if (ComboIndex > 0 && !FinisherNext)
            {
                _comboTimer -= Time.deltaTime;
                if (_comboTimer <= 0f) ComboIndex = 0;
            }

            // Ticked here rather than in FixedUpdate so the window is two seconds of wall clock
            // regardless of the physics rate, matching the two seconds of immunity that bought it.
            if (ExposedSeconds > 0f)
            {
                ExposedSeconds -= Time.deltaTime;
                if (ExposedSeconds <= 0f) { ExposedSeconds = 0f; _exposedMultiplier = 1f; }

                // A downside the player cannot see is just an unexplained death two seconds
                // later. The beat is slow and red, and deliberately unlike anything else the
                // character does - every other cue in the game is the element's own colour.
                _exposedCue -= Time.deltaTime;
                if (_exposedCue <= 0f)
                {
                    _exposedCue = 0.22f;
                    Spr.Pulse(transform, 0.9f, new Color(1f, 0.25f, 0.22f, 0.5f), 0.26f, true, 0.6f);
                }
            }

            // Soaked has the same problem and the same answer, in water's colour and at a
            // different beat: a small pale ring shed at the feet, like the wake the puddle throws.
            if (Soaked)
            {
                _soakCue -= Time.deltaTime;
                if (_soakCue <= 0f)
                {
                    _soakCue = Tuning.Hazards.WaterSoakCueInterval;
                    Spr.Pulse(transform, 0.55f, new Color(0.55f, 0.82f, 1f, 0.45f), 0.4f, true, 1.4f);
                }
            }
            else _soakCue = 0f;

            // ---- attack ----
            //
            // _cooldown gates the swing that is about to start; it is what has to be true for
            // EITHER a tap or a hold to fire. _holdDelay gates only the HOLD path on top of that -
            // it is what makes holding the button down read as a hair lazier than deliberately
            // timing taps, and it never touches a fresh press.
            _cooldown -= Time.deltaTime;
            _holdDelay -= Time.deltaTime;
            _tapBuffer -= Time.deltaTime;
            _throwTapGuard -= Time.deltaTime;
            TickStreak();
            bool attackHeld = Core.Controls.AttackHeld;
            bool attackTapped = Core.Controls.AttackTapped;

            // The finisher timing bar gets first refusal on a tap: while a strike is pending,
            // the first press after the bar appears is ITS press and nothing else. Consumed here,
            // before the buffer below, so a judged tap can never come back out as a swing.
            if (TickStrike(attackTapped)) attackTapped = false;

            // A press that arrives before any gate is open is remembered rather than dropped.
            // The buffer covers input jitter and nothing more - see Tuning.Combo.TapBufferSeconds.
            if (attackTapped) _tapBuffer = TapBufferSeconds;
            bool tapPending = attackTapped || _tapBuffer > 0f;

            // While the blade is out the attack button means "call it home" and nothing else; the
            // action button (below) means "blink to it". Both are fresh TAPS, not the held
            // state: attacking is a hold in this game, so reading isPressed would fire on the
            // very next frame after the throw and the player would never see the choice. A tap
            // inside _throwTapGuard was aimed at the bar that just closed and does nothing.
            if (Blade != null)
            {
                if (attackTapped && _throwTapGuard <= 0f) Blade.Recall();

                // The recall consumes the press outright. Left in the buffer it would come back
                // as a swing on the frame the blade returns to the hand, from a tap the player
                // spent on something else entirely.
                _tapBuffer = 0f;
            }
            else if (_artHolding)
            {
                // Holding to aim a weapon art (see _artHolding): nothing fires until the button is
                // let go; another button cancels; the art going away (a swap) drops the hold.
                _artHeldFor += Time.deltaTime;
                _tapBuffer = 0f;
                if (Core.Controls.ArtHoldCancelled || !ArtAimPossible) CancelArtHold();
                else if (!attackHeld)
                {
                    _artHolding = false;
                    if (_artAim.HasValue) Facing = _artAim.Value;   // a tap leaves the auto-target's
                    FireSwing();
                }
            }
            else if (_cooldown <= 0f && tapPending && attackHeld && ArtAimPossible)
            {
                // A fresh press on an aimable art: wait for the release (or the hold) - above.
                _artHolding = true;
                _artHeldFor = 0f;
                _artAim = null;
                _tapBuffer = 0f;
            }
            else if (_cooldown <= 0f && (tapPending || (attackHeld && _holdDelay <= 0f)))
                FireSwing();
            if (_artHolding) Core.Controls.HoldingArt(ArtAimShown);

            // ---- element ability / blade blink ----
            //
            // While a thrown blade is in the air the ACTION button takes the player to it -
            // forward damage only, the weapon back in hand where it was. A weapon art's mid-move
            // choice is an action, not the element release (on keyboard both are E anyway). The
            // release is held off for the second of flight, the same way the attack button above
            // is wholly given over to the recall.
            bool releasePressed = Core.Controls.ReleaseTapped;
            if (Blade != null)
            {
                if (Core.Controls.InteractTapped) Blade.TeleportOwner();
            }
            else if (releasePressed && Resource != null && Resource.CanRelease)
                DoRelease();
        }

        /// <summary>Start the next swing - basic or weapon art - through the attack gate: the
        /// cooldown, the hold delay, the ledger's whiff and the swing itself.</summary>
        void FireSwing()
        {
            // A tap that lands the instant the previous swing concludes fires immediately -
            // that is the reward for timing it. Continuing to hold through that same moment
            // waits out HoldExtraDelay first, so idly holding the button down settles into a
            // visibly slower cadence than a player pressing on the beat.
            // The board's Realgar quickens the basics right after a weapon art lands - this
            // swing's gate AND its animation (DoAttack reads _quickSwing), so the animation is
            // never longer than the gate holding the next swing.
            _quickSwing = !FinisherNext && Board != null && Board.ConsumeQuickBasic()
                ? Tuning.Board.RealgarSpeedMul : 1f;
            _cooldown = AttackMotions.CooldownFor(CurrentInterval / _quickSwing);
            _holdDelay = HoldExtraDelay;
            _tapBuffer = 0f;      // spent, whichever of the two gates it opened

            // A fumbled swing still costs the swing - the animation plays, the chain advances
            // and the cooldown runs. It simply connects with nothing, which is what makes it
            // a cost rather than a pause.
            _swingWhiffed = Effects != null && !Effects.OnSwing(FinisherNext);
            DoAttack();
            // Lapsus: a swing that passed through leaves you stumbling.
            if (_swingWhiffed && Effects.StumblesOnWhiff) Stun(Tuning.Exchange.LapsusStumble);
        }

        /// <summary>
        /// Fire the element release, through whatever the run's ledger has to say about it.
        ///
        /// Repeats are fired back to back rather than spread over time: the release is a single
        /// button press and a second one arriving half a second later would read as the input
        /// having stuttered rather than as the boon working.
        /// </summary>
        void DoRelease()
        {
            Convergence.Art.Lighting.Glow.Flash(transform.position, Tuning.Lighting.ReleaseRadius,
                ElementInfo.Tint(Resource.Element), Tuning.Lighting.ReleaseSeconds);
            Convergence.Audio.Sfx.Play(Resource.Element switch { ElementType.Fire => Convergence.Audio.Sound.ReleaseFire, ElementType.Water => Convergence.Audio.Sound.ReleaseWater, ElementType.Earth => Convergence.Audio.Sound.ReleaseEarth, _ => Convergence.Audio.Sound.ReleaseAir }, transform.position);
            // Concentrate: a release from a FULL meter reaches further - set round this one call.
            Resource.ReleaseRadiusMul = Effects != null ? Effects.ReleaseRadiusMul(Resource.Fill01) : 1f;
            try { Resource.Release(); }
            finally { Resource.ReleaseRadiusMul = 1f; }
            // Repeats (Twin Spark, Wellspring) are the ledger's to fire, a moment later and
            // spending nothing - see RunEffects.OnReleased.
            Effects?.OnReleased(Resource);
            Principles?.OnReleased();
            Board?.OnReleased(Resource);
        }

        void FixedUpdate()
        {
            // Keyboard and the on-screen stick, already merged. The stick applies its own dead
            // zone, so a thumb resting on the glass is not an input - which matters more than it
            // sounds, since MoveIntent and air's momentum both read this as a bare "is there any".
            Vector2 input = Core.Controls.Move;

            // A committed finisher ignores the stick. Deliberately dropped to zero input rather
            // than hard-zeroing the velocity, so the existing blend still lets knockback shove the
            // player mid-swing - being locked into an animation must not make you immovable.
            var mods = Mods;

            // Captured BEFORE the lock zeroes the stick - see MoveIntent.
            MoveIntent = input.sqrMagnitude > 0.01f;

            // Petrified reads the exact same "we stopped you" shape AttackLocked already uses -
            // MoveIntent above still reflects what the player is ASKING for, so a rooted Air
            // character does not lose momentum credit for an enemy's own CC on top of losing it
            // for their own finisher lock.
            if (AttackLocked || Petrified || Statue) input = Vector2.zero;

            IsMoving = input.sqrMagnitude > 0.01f;
            Resource?.OnMoved(IsMoving);

            // Water SOAKS (Hazards.WaterPit). Before the dash's early return - a dash through a
            // puddle wades it like a walk does; a leap is the one way over.
            if (!Airborne && Hazards.FloorPits.SoaksAt(_rb.position))
                SoakedSeconds = Tuning.Hazards.WaterSoakSeconds;
            else if (SoakedSeconds > 0f)
                SoakedSeconds = Mathf.Max(0f, SoakedSeconds - Time.fixedDeltaTime);

            // A dash owns the body outright while it runs.
            // Velocity is applied BEFORE the clock is decremented, and the stop happens on the
            // FOLLOWING frame. Both halves matter and each cost a measured hop to find:
            //
            //   decrementing first swallowed the last physics step, so a 3.2 unit hop went 2.86
            //   letting it bleed off through the usual blend carried it on to 3.82
            //
            // Applying every step and then stopping dead lands it on 3.2, which is what the
            // moveset says it does.
            // Epsilon, not zero. Subtracting a float timestep nine times from 0.18 leaves a
            // residue around 1e-9 rather than landing on it, which bought a tenth physics step and
            // overshot by a ninth.
            const float DashEpsilon = 1e-4f;

            if (_dashSeconds > DashEpsilon)
            {
                _rb.linearVelocity = _dashVelocity;
                _dashSeconds -= Time.fixedDeltaTime;
                if (_dashSeconds <= DashEpsilon)
                {
                    _dashSeconds = 0f;
                    _dashStopping = true;
                }

                IsMoving = true;
                MoveIntent = true;   // a dash IS movement, however it was started
                Resource?.OnMoved(true);
                return;
            }

            if (_dashStopping)
            {
                _dashStopping = false;
                _dashVelocity = Vector2.zero;
                _rb.linearVelocity = Vector2.zero;
                _rb.linearDamping = _dampingBeforeDash;
                // A dash is the one walk that crosses a chasm - and where it ENDS is judged.
                SettleOverChasm();
            }
            TrackSafeGround();

            // Retrograde inverts the stick, not the facing: you still look where you are
            // going, you just cannot steer there.
            if (Effects != null && Effects.InputsInverted) input = -input;

            // The ledger's conditional move speed (Eagle, Retrograde Motion), bent with its own.
            float timed = Effects != null ? mods.Factor(Art.Gear.StatKind.MoveSpeed, Effects.MoveSpeedUp) : 1f;

            // The GROUND's own contribution - a sand trap or an elite Turret's mire. Asked of the
            // terrain rather than written onto this component by whatever is standing on it: a
            // hazard that pushed a value here would need somebody to push it back on the frame
            // the player steps out, and whoever wrote last would win. See Hazards.FloorPits.
            float terrain = Hazards.FloorPits.SpeedMultiplierAt(_rb.position);
            // Mired: the slow bites twice as hard.
            if (Effects != null && Effects.Mired) terrain = Mathf.Max(0.2f, 1f - (1f - terrain) * Tuning.Exchange.MiredSlowMul);

            var target = input.normalized
                         * (BaseMoveSpeed * Art.Gear.StatPercents.Apply(1f, MoveSpeedPointsNow)
                            * mods.MoveSpeedMul * timed * terrain
                            * (Principles != null ? Principles.MoveSpeedMultiplier : 1f));

            // Blend rather than hard-set so knockback impulses still read. Drag slows the stop.
            float stopBlend = 0.25f / (1f + Mathf.Max(0f, mods.ExtraSlide));
            // Water takes away GRIP: the blend is capped however it was reached, so walking,
            // turning and stopping all drift - see Hazards.WaterPit. The body's damping is
            // re-solved with it so the TOP speed stays the dry one (FloorPits.DampingFor) - water
            // steers you, it neither slows nor hurries you. Written every step; a dash's
            // save/restore around it is harmless. Undine's holder keeps full grip in water.
            const float WalkBlend = 0.55f;
            float grip = Hazards.FloorPits.PlayerGripAt(_rb.position);
            _rb.linearDamping = Hazards.FloorPits.DampingFor(grip, WalkBlend, Tuning.Player.BodyDamping);
            // A directed leap's CARRY rides on top of the walk, kept out of its blend and paid
            // back for the body's damping (Box2D: v / (1 + h x damping) each step), so the body
            // is carried exactly the leap's distance and the stick steers as it always does. Last
            // step's carry is taken off first; on the landing step it simply is not put back.
            float damp = 1f + Time.fixedDeltaTime * _rb.linearDamping;
            var walked = _rb.linearVelocity - _leapCarryPut / damp;
            walked = Vector2.Lerp(walked, target, Mathf.Min(IsMoving ? WalkBlend : stopBlend, grip));
            _leapCarryPut = Airborne ? _leapCarry * damp : Vector2.zero;
            _rb.linearVelocity = walked + _leapCarryPut;
            HoldChasmEdge();

            Effects?.Tick(Time.fixedDeltaTime, IsMoving, Resource?.Fill01 ?? 0f);

            if (Health)
            {
                Health.Poise = Resource?.PoiseBonus ?? 0f;
                // Stat-based mitigation - the element, the ledger, armour condition, Resilience,
                // Graze/Brace, Bulwark - is floored as one (Art.Gear.StatCurves.Incoming): sturdy,
                // never untouchable. Being caught exposed after a leap and Medusa's stone skin are
                // states of the fight, not mitigation, and sit outside the floor.
                // The ledger's costs on damage taken land OUTSIDE the floor (Paper Guard), and an
                // entry may move the floor itself (Exposed, Adamant).
                // The ledger's conditional REDUCTIONS (Reactive Plate, Lapis, Fortitude,
                // Committed, Retrograde Motion) are mitigation too, so they sit inside the floor.
                Health.Vulnerability = Art.Gear.StatCurves.Incoming(
                                           (1f - (Resource?.DamageReduction ?? 0f))
                                           * mods.DamageTakenMul
                                           * (IncomingDamageMultiplier?.Invoke() ?? 1f)
                                           * (Effects != null ? Effects.MitigationNow : 1f),
                                           mods.MitigationFloor)
                                       * mods.DamageTakenOutside
                                       * (Effects != null ? Effects.ExposureNow : 1f)   // the Nigredos' exposures
                                       * WearOutside                                      // Corrosion
                                       * (ExposedSeconds > 0f ? _exposedMultiplier : 1f)
                                       * (Soaked ? 1f + Tuning.Hazards.WaterSoakDamageTaken : 1f)
                                       * (_status != null ? _status.StoneDamageTaken : 1f);   // stone skin
            }
        }

        /// <summary>
        /// Send the weapon out. Damage is rolled ONCE here and carried by the blade, so every body
        /// it passes through takes the same hit - a crit rolled per target would make one finisher
        /// a lottery of up to six independent rolls.
        /// </summary>
        void ThrowWeapon(AttackStep step)
        {
            if (Blade != null) return;   // one blade at a time

            // The ledger's bonus damage and weapon-art multiplier belong here as at every other
            // weapon-art damage site - this path left them out, so no boon ever touched a thrown blade.
            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier;
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool bladeCrit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;   // finisher-only (Wanderblade / Cast)
            if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();

            var element = Resource?.Element ?? ElementType.Fire;

            // Grab the weapon's look BEFORE hiding it - the blade in flight has to be this sword.
            Sprite wSprite = null; Color wTint = Color.white; Vector2 wSize = Vector2.one;
            Rig?.TryGetWeaponVisual(out wSprite, out wTint, out wSize);
            Rig?.SetWeaponVisible(false);

            bool firstBody = true;
            Blade = ThrownBlade.Launch(
                gameObject, Facing, dmg, step.Knockback, element, wSprite, wTint, wSize,
                onHit: (hp, info) =>
                {
                    // Wear and elemental hooks have to fire for a thrown hit exactly as for a
                    // swung one, or this finisher quietly opts out of fire's heat and durability.
                    OnWeaponUsed?.Invoke();
                    Resource?.OnHitLanded(Health, new DamageInfo(dmg, element, gameObject));
                    NotifyThrownHit(hp, info, 0f, firstBody);
                    firstBody = false;
                    // FLINCH, by the art's weight as a swing's hit does (ResolveArc) - the thrown
                    // path never flinched, so a Medium Wanderblade interrupted nothing.
                    if (!hp.IsDead && step.Weight != FinisherWeight.Light)
                        hp.GetComponent<Enemies.EnemyController>()?.Flinch(
                            Core.Tuning.Finisher.FlinchSeconds, step.Weight == FinisherWeight.Heavy);
                },
                onFinished: () =>
                {
                    Blade = null;
                    Rig?.SetWeaponVisible(true);
                    // Back in the hand (or blinked to) without a single hit: a perfect throw
                    // that speared nothing was swung at air.
                    if (_streakAwaiting) ResolveStreak(false);
                },
                crit: bladeCrit,
                scaleHit: (hp, d) => ScaleThrownHit(hp, d, true, bladeCrit));
            Blade.MaxDistance = step.ThrowDistance;
            // Through a crowd the blade loses damage body by body, as a swing does - each leg
            // from its first body (it used to hit every body in full, both ways).
            float keep = CleaveFalloff(step.ChainFalloff);
            Blade.Falloff = n => Mathf.Max(MinChainFraction, Mathf.Pow(keep, n));
            _throwTapGuard = Tuning.StrikeTiming.LingerSeconds;   // the bar is still on screen
        }

        // ---- the finisher timing bar ----
        //
        // Every finisher (bar Shadow's Echo, a basic in the finisher slot) runs a meter - a slim
        // arc on the character's off-hand side, filling from the bottom (Combat.StrikeBar): the first tap during it decides how hard the strike lands - PERFECT +20%,
        // GOOD +10%, EARLY or nothing -5% (Combat.StrikeJudge, Tuning.StrikeTiming). The strike
        // always lands at the bar's end; pressing never makes it come out sooner, so there is no
        // tempo to win by pressing early, only damage to win by pressing well.
        //
        // The result is folded into _strikeMul with the PARITY factor that pays a finisher back
        // for any wind-up it had to gain (StrikeJudge.Parity), so a chain landing GOOD deals what
        // it dealt before the bar existed. _strikeMul multiplies every finisher hit on the same
        // line as FinisherDamageMul and FinisherPowerMul; basics never read it.
        //
        // Plain value fields only, so a domain reload loses nothing (see CLAUDE.md).

        /// <summary>True from a timed finisher's press until its strike lands.</summary>
        bool _barLive;

        /// <summary>Whether the bar has been put on screen yet - a long charge shows it only for
        /// its last _barSeconds.</summary>
        bool _barShown;

        /// <summary>How long THIS finisher's bar is on screen: its weight's lead plus the judged
        /// segments, stretched over a delayed finisher's own delay up to DelayedBarSeconds.</summary>
        float _barSeconds;

        /// <summary>This finisher's wind-up is Riposte's guard (AttackStep.Guards).</summary>
        bool _barGuards;

        /// <summary>Seconds until the strike, scaled time. The ONE clock that both moves the
        /// marker and judges the press, so the picture can never disagree with the score.</summary>
        float _strikeIn;

        /// <summary>The frame the finisher was pressed on, so the press that STARTED the bar can
        /// never also be judged as its first tap.</summary>
        int _barOpenedFrame = -1;

        StrikeVerdict _verdict = StrikeVerdict.Pending;
        float _strikeParity = 1f;

        /// <summary>What every finisher hit is multiplied by: parity x the timing result. Before
        /// the bar closes it reads as GOOD (see StrikeJudge.Multiplier) for any hit that lands
        /// early - the Flurry's two cuts.</summary>
        float _strikeMul = 1f;

        /// <summary>Every hit of the settled weapon art crits (the board's Cinnabar, on a PERFECT).</summary>
        bool _strikeCrit;

        /// <summary>A strike this late past its own bar was never going to settle (a path that
        /// returned early) - close the bar as a miss rather than leave it stuck on screen.</summary>
        const float StrikeWatchdogSeconds = 0.5f;

        /// <summary>The result of the most recent timed finisher - for tests and any HUD that
        /// wants it.</summary>
        public StrikeVerdict LastStrikeVerdict { get; private set; } = StrikeVerdict.Pending;

        /// <summary>True while a finisher's timing bar is running.</summary>
        public bool StrikePending => _barLive;

        StrikeBar _bar;
        StrikeBar Bar => _bar != null ? _bar : (_bar = StrikeBar.For(transform));

        RiposteWard _ward;
        RiposteWard Ward => _ward != null ? _ward : (_ward = RiposteWard.For(transform));

        /// <summary>
        /// Open the timing bar for a finisher being pressed, and return the wind-up it has to GAIN
        /// so the bar has its full length before the strike (0 for anything that already delays
        /// its damage long enough). Shadow's Echo runs no bar and strikes at a plain 1x.
        /// </summary>
        float BeginStrike(AttackStep step, float swingWindow)
        {
            _barGuards = step.Guards;
            _guardParried = false;
            if (step.NeverLocks)
            {
                _barLive = false;
                _strikeMul = 1f;
                return 0f;
            }

            float delay = StrikeDelay(step, swingWindow);
            float bar = Tuning.StrikeTiming.BarSecondsFor(step.Weight);
            float pad = Mathf.Max(0f, bar - delay);
            _barSeconds = Mathf.Max(bar, Mathf.Min(delay, Tuning.StrikeTiming.DelayedBarSeconds));

            _strikeParity = StrikeParity(step, swingWindow, delay, pad);
            _strikeCrit = false;
            _verdict = StrikeVerdict.Pending;
            _strikeMul = _strikeParity * StrikeJudge.Multiplier(StrikeVerdict.Pending);
            _strikeIn = pad + delay;
            _barLive = true;
            _barShown = false;
            _barOpenedFrame = Time.frameCount;
            if (_barGuards) Ward.Raise(Facing.x);   // Riposte's guard is up from the press

            // A perfect still waiting on the LAST finisher to connect never did - this one's hits
            // must not be credited to it.
            if (_streakAwaiting) ResolveStreak(false);
            _finisherConnected = false;
            _streakExempt = step.DiscSuspends || step.Palisade || step.Tripwire;   // traps: may hit nothing by design
            return pad;
        }

        /// <summary>
        /// Seconds from a finisher's dispatch to the hit its bar judges. MUST follow Dispatch's
        /// branch order and each path's own timing - the bar ends where this says the strike is.
        /// </summary>
        float StrikeDelay(AttackStep step, float swingWindow)
        {
            if (step.Boomerang || step.Pincer) return step.ChargeSeconds;   // the gather, then the throw   // the Prima Materia art: the gather, then the throw
            if (step.Swarm) return AttackMotions.SwingSeconds(swingWindow);   // the Swarm art: the draw, then the hive
            if (step.Formation) return AttackMotions.SwingSeconds(swingWindow);   // the Four Birds: the draw, then the volley
            if (step.Longshot) return Tuning.Longshot.DrawSeconds;                 // the long draw's loose
            if (step.Disengage) return AttackMotions.SwingSeconds(swingWindow) * Tuning.Disengage.LooseAt;   // the top of the flip
            if (Weapon == Art.Gear.WeaponClass.Bow) return AttackMotions.SwingSeconds(swingWindow);  // ReleaseArrow's draw
            if (step.DiscSpray && Rig != null)                                     // Kickback's fling
                return Rig.StrikeDrawnSeconds(step.Motion, swingWindow);
            if (step.Gyres && Rig != null)                                         // Gyre: lift, pull, cut
                return Rig.StrikeDrawnSeconds(step.Motion, swingWindow) + Tuning.Gyre.PullSeconds
                       + Rig.StrikeDrawnSeconds(AttackMotion.DiscGyreCut, swingWindow);
            if (step.DiscOrrery && Rig != null)                                    // Orrery's cast
                return Rig.StrikeDrawnSeconds(step.Motion, swingWindow);
            if (step.DiscSuspends && Rig != null)                                  // Sublimate's opening
                return Rig.StrikeDrawnSeconds(step.Motion, swingWindow);
            if (step.DiscMarks && Rig != null)                                     // Mark's flick
                return Rig.StrikeDrawnSeconds(step.Motion, swingWindow);
            if (step.DiscScatter && Rig != null)                                   // Scattershot's burst
                return ScatterReleaseSeconds(step, swingWindow);
            if (step.DiscCarousel && Rig != null)                                  // the turn begins
                return Rig.StrikeStartSeconds(step.Motion, swingWindow);
            if (step.DiscCleaves && Rig != null)                                   // the Cleaver's chop
                return Rig.StrikeDrawnSeconds(step.Motion, swingWindow);
            if (step.DiscCaroms && Rig != null)                                    // Carom's hurl
                return Rig.StrikeDrawnSeconds(step.Motion, swingWindow);
            if (step.DiscHoming && Rig != null)                                    // Ripple's release
                return Rig.StrikeDrawnSeconds(step.Motion, swingWindow);
            if (step.DiscThrows > 0) return 0f;
            if (step.LeapSeconds > 0f) return step.LeapSeconds;                    // the landing
            if (step.ReverseCones) return Core.Tuning.Quintessence.MergeSeconds;   // the first cone
            if (step.SplitsThreeWays) return Core.Tuning.Separatio.HoldSeconds;    // the first figure
            if (step.ChargeSeconds > 0f) return step.ChargeSeconds;                // the release
            if (step.SheathDraw && SheatheDrawn)                                   // the blade home
                return Core.Tuning.Katana.SheathSeconds;   // the bar's shortfall is held sheathed
            if (step.SplitDraw && GeminiDrawn)                                     // both halves home
                return Core.Tuning.SolveCoagula.PartSeconds
                     + Core.Tuning.SolveCoagula.SheatheSeconds;   // held sheathed, Crosscut's rule
            if (step.LungeDistance > 0f) return Core.Tuning.Impale.LungeSeconds;   // the end of the lunge
            if (step.PommelFlurry) return Core.Tuning.Flurry.PommelAt;             // the pommel
            if (step.StrikesOnDraw && Rig != null)                                  // the drawn strike
                return Rig.StrikeDrawnSeconds(step.Motion, swingWindow);
            return 0f;   // an ordinary swing or a thrown blade strikes on dispatch
        }

        /// <summary>
        /// Seconds a finisher still spends AFTER its bar is judged before its own swing starts -
        /// Crosscut's two cuts and the draw out of the saya, which wait for the verdict. Part of
        /// the chain's time, so parity counts it; it is not a gained wind-up.
        /// </summary>
        float StrikeTail(AttackStep step)
            => step.Guards ? CounterSeconds(step, IntervalFor(step))
             : step.SheathDraw && SheatheDrawn
                ? Core.Tuning.Katana.CutSeconds * 2f + Core.Tuning.Katana.DrawSeconds
                : step.SplitDraw && GeminiDrawn
                    ? Core.Tuning.SolveCoagula.DrawSeconds + Core.Tuning.SolveCoagula.RejoinSeconds
                    : 0f;

        /// <summary>
        /// The parity factor for this finisher, from the chain actually being swung: the same
        /// basics NextStep hands out (opener, middle swings, lead-in), at their real intervals,
        /// and the finisher at its real damage. See StrikeJudge.Parity.
        /// </summary>
        float StrikeParity(AttackStep finisher, float finisherWindow, float delay, float pad)
        {
            var ms = ActiveMoveset;
            if (ms?.Basics == null || ms.Basics.Length == 0)
                return 1f / Tuning.StrikeTiming.GoodMultiplier;

            int n = BasicsPerChain;
            float basicDamage = 0f, seconds = 0f;
            for (int c = 0; c < n; c++)
            {
                int i = c >= n - 1 ? 1 : c == 0 ? 0 : 2;   // NextStep's own selection
                var b = ms.Basics[Mathf.Min(i, ms.Basics.Length - 1)];
                basicDamage += b.DamageMultiplier;
                seconds += AttackMotions.CooldownFor(IntervalFor(b));
            }
            seconds += delay + StrikeTail(finisher) + AttackMotions.CooldownFor(finisherWindow);

            float finisherDamage = finisher.DamageMultiplier * Mods.FinisherDamageMul * FinisherPowerMul;
            return StrikeJudge.Parity(basicDamage, finisherDamage, seconds, pad);
        }

        /// <summary>
        /// Run the bar's clock for a frame and judge a tap against it. Returns true when the tap
        /// was the bar's - the caller must then not treat it as an attack press.
        ///
        /// Taps before the bar is on screen (the first second of a long charge) are nobody's: the
        /// bar cannot be early-pressed before it can be seen.
        /// </summary>
        bool TickStrike(bool tapped)
        {
            if (!_barLive) return false;

            _strikeIn -= Time.deltaTime;
            if (_strikeIn < -StrikeWatchdogSeconds) { SettleStrike(); return false; }
            if (_barGuards) Ward.Face(Facing.x);

            if (_strikeIn > _barSeconds) return false;

            if (!_barShown) { _barShown = true; Bar.Begin(Facing.x, _barSeconds); }   // off-hand side, held
            Bar.Tick(_strikeIn);

            if (!tapped || _verdict != StrikeVerdict.Pending || Time.frameCount == _barOpenedFrame)
                return false;

            _verdict = StrikeJudge.Judge(_strikeIn);
            Bar.Press(_strikeIn, _verdict);
            _tapBuffer = 0f;
            return true;
        }

        /// <summary>
        /// The strike is landing: close the bar and fix what every hit of this finisher deals.
        /// Called by each finisher path at the moment its judged hit resolves, BEFORE its damage
        /// is rolled. Safe to call when no bar is running (a basic, Shadow's Echo).
        /// </summary>
        void SettleStrike()
        {
            if (!_barLive) return;
            _barLive = false;
            if (_barGuards) Ward.Drop();   // the guard ends as the thrust lands

            if (_verdict == StrikeVerdict.Pending) _verdict = StrikeVerdict.Missed;
            _strikeMul = _strikeParity * StrikeJudge.Multiplier(_verdict);
            LastStrikeVerdict = _verdict;

            // The board's steep (Cohobation) rides the same multiplier, and Cinnabar's crit is
            // decided here - both are about THIS weapon art, so both are fixed when it is.
            _strikeCrit = false;
            if (Board != null) _strikeMul *= Board.OnStrikeSettled(_verdict, out _strikeCrit);
            Effects?.OnArtSettled(_verdict);

            if (_barShown) Bar.Settle(_verdict);
            Convergence.Audio.CombatSounds.Verdict(_verdict, transform.position);

            // The strike itself says PERFECT too - in the perfect band's green, over the swing's own flash.
            if (_verdict == StrikeVerdict.Perfect)
                Spr.Flash(transform.position, 1.3f, Tuning.StrikeTiming.Perfect, 0.24f);

            // The perfect streak. Anything but PERFECT breaks it now. A perfect counts once the
            // finisher CONNECTS: already, for the Flurry's cuts; on this same frame, for a swing or
            // Crosscut's first cut (its hits resolve right after this call); or later, for anything thrown or
            // fired - so it waits. Sublimate is judged on timing alone: by design it deals nothing
            // until the NEXT finisher sets it off, which would otherwise always read as air.
            // Red Lion: a GOOD neither builds the streak nor breaks it.
            bool goodKeeps = _verdict == StrikeVerdict.Good && Effects != null && Effects.StreakSurvivesGood;
            if (_verdict != StrikeVerdict.Perfect) { if (!goodKeeps) ResolveStreak(false); }
            else if (_finisherConnected || _streakExempt) ResolveStreak(true);
            else
            {
                _streakAwaiting = true;
                _streakWait = Tuning.StrikeTiming.StreakConnectSeconds;
            }
        }

        // ---- the perfect streak ----
        //
        // Consecutive PERFECT finishers that connect stack crit chance onto every hit (Tuning.
        // StrikeTiming.Streak*). Lives on the player, so it lasts the run and ends with it. Plain
        // value fields only - nothing here is lost to a domain reload.

        int _perfectStreak;

        /// <summary>A perfect has been judged and is waiting to hear its finisher land.</summary>
        bool _streakAwaiting;
        float _streakWait;

        /// <summary>A finisher hit has landed since this bar began.</summary>
        bool _finisherConnected;

        /// <summary>This bar's finisher is judged on timing alone (Sublimate).</summary>
        bool _streakExempt;

        /// <summary>Consecutive connected PERFECT finishers. Keeps counting past the cap - only
        /// the bonus stops.</summary>
        public int PerfectStreak => _perfectStreak;

        /// <summary>Crit chance the streak adds to every hit, 0..1.</summary>
        public float StreakCritBonus
            => Mathf.Min(_perfectStreak, Tuning.StrikeTiming.StreakCap) * Tuning.StrikeTiming.StreakCritPerPerfect;

        /// <summary>One of the player's finisher hits landed on an enemy - from Combat.FinisherHits.</summary>
        public void NoteFinisherHit()
        {
            _finisherConnected = true;
            if (_streakAwaiting) ResolveStreak(true);
        }

        /// <summary>A waiting perfect that never hears a hit was swung at air. A thrown blade
        /// waits for as long as it is out - its onFinished settles it.</summary>
        void TickStreak()
        {
            if (!_streakAwaiting || Blade != null) return;
            _streakWait -= Time.deltaTime;
            if (_streakWait <= 0f) ResolveStreak(false);
        }

        void ResolveStreak(bool gained)
        {
            _streakAwaiting = false;
            if (gained)
            {
                _perfectStreak++;
            }
            else if (_perfectStreak > 0)
            {
                _perfectStreak = 0;
            }
        }

        void DoAttack()
        {
            var step = NextStep;          // captured before the chain advances
            bool isFinisher = FinisherNext;
            AttackStarted?.Invoke(isFinisher);

            // Blood Blade: a full vial swaps in the Heavy release for this cast and empties
            // itself in the same moment - spent on CASTING the release, not on it connecting,
            // the same completes-not-connects reasoning WeaponHeat's own cycle uses. Checked
            // before every other branch below, so the swapped step is what the leap/charge/throw
            // dispatch and the animation both see - the release is an ordinary AttackStep once
            // chosen, not a second code path.
            if (step.ReleaseStep != null && Vial != null && Vial.Full)
            {
                step = step.ReleaseStep;
                Vial.Release();
            }

            // Alternate the direction of consecutive basics so a chain reads as a back-and-forth
            // rather than one clip replayed three times. Read here, before AdvanceCombo: swing 0
            // runs forward and ends where swing 1 begins, so the blade never teleports back to the
            // wind-up between them.
            //
            // Finishers always play their canonical direction. Half of them are named for it -
            // an "Overhand" that came up from the hip every other chain would be a different move
            // wearing the same name and the same icon.
            // True on the ALT-SWING - the second basic of the chain. What "alt" means is the
            // motion's business: Chop stages the same downward stroke from the other shoulder,
            // Sweep genuinely runs its arc the other way. It is no longer "reversed", because
            // for the motion the whole chain is built on it no longer reverses anything.
            //
            // XOR, not OR: the step's own variant is the BASELINE and the chain's alternation
            // flips either side of it. Plain OR would pin every alt-swing of an already-alt step
            // to one variant, which is the wag the alternation exists to avoid.
            //
            // The lead-in is always staged as the alt-swing, wherever the chain's length puts
            // it, because its end pose is what the finisher was matched against.
            bool alt = step.AltVariant ^ (!isFinisher && (LeadInNext || (ComboIndex & 1) == 1));

            Resource?.OnAttackStarted();

            // The animation is given the real window until the next swing, so a fast moveset
            // reads as fast instead of restarting its arc part-way through.
            float swingWindow = IntervalFor(step) / _quickSwing;
            _quickSwing = 1f;

            // ---- the finisher timing bar ----
            //
            // Every finisher that runs a bar strikes as its bar (BarSecondsFor its weight) runs out. One that
            // already delays its damage by at least that long shows the bar at the end of its own
            // delay; one that would have struck on this frame first holds its swing's opening
            // pose for the shortfall (WindUpThenDispatch) - mobile and still aiming, the same
            // convention ChargeSeconds lives by. The chain advances when that wind-up ENDS, not
            // now, so everything the strike reads off NextStep/CurrentRange still describes it.
            float pad = isFinisher ? BeginStrike(step, swingWindow) : 0f;

            // Crosscut spends its wind-up SHEATHED, not holding the swing's first frame: the
            // blade goes home at once and waits in the saya for the bar (SheathDrawStrike).
            _sheathHold = 0f;
            if (pad > 0f && ((step.SheathDraw && SheatheDrawn) || (step.SplitDraw && GeminiDrawn)))
            {
                _sheathHold = pad;
                pad = 0f;
            }

            if (pad > 0f)
            {
                // The gate covers the wind-up too, or the swing after it comes for free. The
                // branch that runs afterwards either re-sets it outright or inherits what is left,
                // which is exactly its own gate.
                _cooldown = pad + AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(WindUpThenDispatch(step, isFinisher, swingWindow, alt, pad));
                return;
            }

            Dispatch(step, isFinisher, swingWindow, alt);
        }

        /// <summary>
        /// A finisher that would have struck on the press winds up first: its swing's opening
        /// frame, held (ICharacterRig.HoldSwingStart - the frame every lead-in is authored to end
        /// on, so the hand-off stays seamless), for exactly the time the timing bar needs.
        /// Scaled time, so a screen opening mid-wind-up pauses it.
        /// </summary>
        IEnumerator WindUpThenDispatch(AttackStep step, bool isFinisher, float swingWindow,
                                       bool alt, float seconds)
        {
            Rig?.SetSwingHop(0f);
            Rig?.HoldSwingStart(step.Motion, alt, seconds);
            yield return new WaitForSeconds(seconds);
            Dispatch(step, isFinisher, swingWindow, alt);
        }

        /// <summary>
        /// Everything an attack does once it actually goes - which, for a finisher that gained a
        /// wind-up, is when that wind-up ends rather than on the press.
        /// </summary>
        /// <summary>The weight of the weapon art last dispatched - a Heavy breaks an elite Drowned
        /// Bell's whole bubble at once (EnemyController.OnBubbleDamaged). Set after anything
        /// Sublimate left in the air goes off, so those blasts count as their own art's.</summary>
        public FinisherWeight LastArtWeight { get; private set; } = FinisherWeight.Light;

        void Dispatch(AttackStep step, bool isFinisher, float swingWindow, bool alt)
        {
            // A weapon art fired while Revolve's ring turns takes the discs left in it - they go
            // to the hands for the art (both discs, as every art throws).
            if (isFinisher && Combat.DiscOrbit.Current != null) Combat.DiscOrbit.Current.EndNow();

            // Only the ordinary swing below frees its facing at the drawn strike; every other
            // path resolves later and holds it for its whole lock.
            _lockFreesAfterStrike = false;
            _settleIsFree = false;

            // A finisher sets off anything Sublimate left in the air - but not the Sublimate that
            // planted it. Checked BEFORE the move resolves, so the detonation lands first and its
            // marks or its stagger are already on the bodies the new finisher is about to hit.
            // That ordering is what makes Hold -> Mark different from Mark -> Hold. Here rather
            // than on the press, so a wound-up finisher detonates as it swings, not as it starts.
            if (isFinisher && Suspended != null && !step.DiscSuspends)
            {
                var armed = Suspended;
                Suspended = null;
                armed.Detonate();
            }
            if (isFinisher) { LastArtWeight = step.Weight; ArtSocket = RotationIndex; ArtSerial++; _lastArtAt = Time.time; }

            // The King and Queen art: both discs in a pincer - ahead of the disc paths below.
            if (step.Pincer)
            {
                _cooldown = 60f;
                StartCoroutine(KingAndQueenStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // The Swarm art: drawn like a shot, but the whole hive leaves - ahead of the arrow path.
            if (step.Swarm)
            {
                // Held shut until the hive is home; the coroutine hands the gate back.
                _cooldown = 60f;
                StartCoroutine(SwarmStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // The Four Birds: drawn like a shot with the formation round it - ahead of the arrow path.
            // Harpoon: the bow's pull - a twin shot whose first arrow trails the line.
            if (step.Harpoon)
            {
                _cooldown = 60f;
                StartCoroutine(HarpoonStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Split, Pin, Tripwire: the bow's other three.
            if (step.SplitShot || step.Pin || step.Tripwire)
            {
                _cooldown = 60f;
                StartCoroutine(step.SplitShot ? SplitStrike(step, swingWindow)
                             : step.Pin ? PinStrike(step, swingWindow)
                             : TripwireStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Longshot: the long kneeling draw, then the line across the room.
            if (step.Longshot)
            {
                _cooldown = 60f;
                StartCoroutine(LongshotStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Piercing Rain: one arrow up, a rain down on the target's ground.
            if (step.ArrowRain)
            {
                _cooldown = 60f;
                StartCoroutine(PiercingRainStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Palisade: the bow's defence - arrows onto shots in flight, the rest a line.
            if (step.Palisade)
            {
                _cooldown = 60f;
                StartCoroutine(PalisadeStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Disengage: the bow's reposition - a backflip away, one shot from the top.
            if (step.Disengage)
            {
                _cooldown = 60f;
                StartCoroutine(DisengageStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Concussion: the bow's push - the arrow bursts off the string.
            if (step.Concussion)
            {
                _cooldown = 60f;
                StartCoroutine(ConcussionStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            if (step.Formation)
            {
                _cooldown = 60f;
                StartCoroutine(FormationStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // the Prima Materia art: the bow is THROWN, not drawn - ahead of the arrow path below.
            if (step.Boomerang)
            {
                // Held shut for the whole sequence; the coroutine hands the gate back at its end.
                _cooldown = 60f;
                StartCoroutine(PrimaMateriaStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // The bow never swings - basic or finisher, every step is a single arrow at whatever
            // is currently locked. Checked before every other branch for the same reason the
            // disc's own throw check is: this does not resolve its damage through GatherTargets
            // or an arc at all, so none of the machinery below applies.
            if (Weapon == Art.Gear.WeaponClass.Bow)
            {
                if (step.KneelRoll || step.RecoilHop) BowRollStrike(step, swingWindow);   // a move, not a shot
                else FireArrow(step, isFinisher, swingWindow);
                AdvanceCombo(isFinisher);
                return;
            }

            // A disc FINISHER that declares throws resolves as a volley, not an arc.
            if (step.DiscOrrery)
            {
                StartCoroutine(OrreryStrike(step, swingWindow));   // settled at the cast
                AdvanceCombo(isFinisher);
                return;
            }
            if (step.DiscSuspends)
            {
                StartCoroutine(SublimateStrike(step, swingWindow));   // settled at the opening
                AdvanceCombo(isFinisher);
                return;
            }
            if (step.DiscMarks)
            {
                StartCoroutine(MarkStrike(step, swingWindow));   // settled at the flick
                AdvanceCombo(isFinisher);
                return;
            }
            if (step.DiscScatter)
            {
                StartCoroutine(ScattershotStrike(step, swingWindow));   // settled at the burst
                AdvanceCombo(isFinisher);
                return;
            }
            if (step.DiscSpray)
            {
                StartCoroutine(KickbackStrike(step, swingWindow));   // settled at the fling
                AdvanceCombo(isFinisher);
                return;
            }
            if (step.DiscCarousel)
            {
                StartCoroutine(CarouselStrike(step, swingWindow));   // settled as the turn begins
                AdvanceCombo(isFinisher);
                return;
            }
            if (step.DiscCleaves)
            {
                StartCoroutine(CleaverStrike(step, swingWindow));   // settled at the chop
                AdvanceCombo(isFinisher);
                return;
            }
            if (step.DiscCaroms)
            {
                StartCoroutine(CaromStrike(step, swingWindow));   // settled at the hurl
                AdvanceCombo(isFinisher);
                return;
            }
            if (step.DiscHoming && step.DiscThrows > 0)
            {
                StartCoroutine(HomingVolley(step, swingWindow));   // settled at the release
                AdvanceCombo(isFinisher);
                return;
            }
            if (step.DiscThrows > 0)
            {
                SettleStrike();   // the discs carry the timing result from the moment they leave
                ThrowVolley(step, swingWindow);
                AdvanceCombo(isFinisher);
                return;
            }

            // A disc BASIC with nothing in reach is thrown instead of swung. Checked before every
            // other branch because a throw is not a swing with a different sprite - it resolves
            // its damage over seconds, out there, and none of the arc machinery below applies.
            // Revolve's ring is turning: the basic goes on unarmed and knocks ONE disc out of it,
            // and the chain goes on as ever - the weapon art after two basics takes the last TWO
            // (the user's call: a disc for a basic, both for an art; four discs, one chain).
            if (!isFinisher && Combat.DiscOrbit.Live)
            {
                KnockFromRing(step, swingWindow);
                AdvanceCombo(false);
                return;
            }

            if (!isFinisher && ThrowsInsteadOfSwinging)
            {
                ThrowDisc(step, swingWindow);
                AdvanceCombo(false);
                return;
            }

            // A leap goes airborne first: off the screen, untouchable, still steering.
            if (step.LeapSeconds > 0f)
            {
                // The cooldown covers the whole flight, or the swing after it lands for free
                // while the character is still in the air.
                _cooldown = step.LeapSeconds + AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(LeapStrike(step, isFinisher, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Quintessence: the halves join overhead, four reverse cones strike, the halves return.
            if (step.ReverseCones)
            {
                _cooldown = QuintessenceSeconds + AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(QuintessenceStrike(step));
                AdvanceCombo(isFinisher);
                return;
            }

            // Separatio holds the blade up, then comes apart into three figures. Its own hold, not
            // ChargeSeconds - see AttackStep.SplitsThreeWays.
            if (step.SplitsThreeWays)
            {
                _cooldown = Core.Tuning.Separatio.HoldSeconds + SeparatioSpread
                            + AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(SeparatioStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // The Magnum Opus gathers like a charge (its ChargeSeconds is the gather - StrikeDelay's
            // charge line answers for it) and then fires a crescent instead of resolving an arc.
            if (step.MagnumOpus)
            {
                _cooldown = step.ChargeSeconds + SlashCrossSeconds(step, swingWindow)
                            + AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(MagnumOpusStrike(step, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // A charged strike winds up first: hold the pose, root the player, resolve later.
            if (step.ChargeSeconds > 0f)
            {
                // The cooldown has to cover the wind-up too, or the swing after it comes for free.
                _cooldown = step.ChargeSeconds + AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(ChargedStrike(step, isFinisher, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // The katana signature runs a three-beat sequence in a coroutine: quick-sheathe, two
            // crescent cuts while sheathed, then the unsheathing draw-cut. Same hand-off shape as
            // the leap and the throw above.
            //
            // Gated on the DRAWN weapon, not on the step alone. The saya grants Crosscut whatever
            // is in hand, but the sequence needs a scabbard on the character to go into - beside
            // any other sword it fell through to the ordinary swing path below, which is exactly
            // what should happen: same weight, same damage, no borrowed animation.
            if (step.SheathDraw && SheatheDrawn)
            {
                _cooldown = SheathDrawLength(swingWindow) + AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(SheathDrawStrike(step, isFinisher, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Solve et Coagula: the Gemini Blade apart - home to both hips, a cross-drawn X with a
            // lunge, the halves slammed back together. Gated on the DRAWN blade, Crosscut's rule.
            if (step.SplitDraw && GeminiDrawn)
            {
                _cooldown = SplitDrawLength(swingWindow) + AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(SplitDrawStrike(step, isFinisher, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Impale: the body lunges with the thrust, and the hit lands where the lunge ends.
            if (step.LungeDistance > 0f)
            {
                _cooldown = Core.Tuning.Impale.LungeSeconds + AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(LungeStrike(step, isFinisher, swingWindow, alt));
                AdvanceCombo(isFinisher);
                return;
            }

            // A strike that lands as the motion DRAWS it, not on the press.
            if (step.StrikesOnDraw)
            {
                _cooldown = AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(DrawnStrike(step, isFinisher, swingWindow, alt));
                AdvanceCombo(isFinisher);
                return;
            }

            // Plow: both discs bowled along the ground; the gate is held until they are home.
            if (step.Plows)
            {
                _cooldown = 60f;
                StartCoroutine(PlowStrike(step, isFinisher, swingWindow, alt));
                AdvanceCombo(isFinisher);
                return;
            }

            // Revolve: both discs into a ring round the body for one turn.
            if (step.Orbits)
            {
                _cooldown = AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(RevolveStrike(step, isFinisher, swingWindow, alt));
                AdvanceCombo(isFinisher);
                return;
            }

            // Gyre: the rotor overhead, the cone drawn in, then the cut. The gate is held until
            // the discs are back in the hands.
            if (step.Gyres)
            {
                _cooldown = 60f;
                StartCoroutine(GyreStrike(step, isFinisher, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Tether: one disc round past the target and back, dragging it in. The gate is held
            // until the disc is home.
            if (step.Tethers)
            {
                _cooldown = 60f;
                StartCoroutine(TetherStrike(step, isFinisher, swingWindow, alt));
                AdvanceCombo(isFinisher);
                return;
            }

            // The Grappling Hook: a chain instead of a swing - it reels a body in, or hauls the
            // player to an elite, a boss or a wall. The gate is held until the chain is home.
            if (step.Grapples)
            {
                _cooldown = 60f;
                StartCoroutine(GrappleStrike(step, isFinisher, swingWindow, alt));
                AdvanceCombo(isFinisher);
                return;
            }

            // Riposte: the guard was the bar; the answer comes after the verdict - the counter if
            // the guard turned anything aside, else a turn back with no swing.
            if (step.Guards)
            {
                _cooldown = CounterSeconds(step, swingWindow) + AttackMotions.CooldownFor(swingWindow);
                StartCoroutine(RiposteCounter(step, isFinisher, swingWindow));
                AdvanceCombo(isFinisher);
                return;
            }

            // Flurry: two cuts and the pommel, on the sequence's own clock.
            if (step.PommelFlurry)
            {
                _cooldown = Mathf.Max(Core.Tuning.Flurry.Seconds / AttackMotions.FillFraction,
                                      AttackMotions.CooldownFor(swingWindow));
                StartCoroutine(FlurryStrike(step, isFinisher));
                AdvanceCombo(isFinisher);
                return;
            }

            // The ordinary swing.
            _visual?.PlayAttack(step.Motion, swingWindow);
            // Before PlayAttack: the rig times the hop against the swing it is about to start.
            Rig?.SetSwingHop(step.HopHeight);
            Rig?.PlayAttack(step.Motion, swingWindow, alt);

            // Finishers only - see WeaponTrail for why a basic deliberately draws nothing.
            if (isFinisher) Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            // Every swing smears - a basic thinly, a finisher fully, on top of its trail.
            SmearSwing(swingWindow, isFinisher);

            // Shadow's after-image: a translucent copy of the character playing the SAME swing a
            // few frames behind. It carries no damage of its own - the second hit is applied per
            // target inside ResolveArc - so this is purely the tell for a passive that would
            // otherwise be an invisible damage multiplier.
            Echoes?.Trail(step.Motion, swingWindow, alt);

            // Phantom's poof - purely cosmetic, unlike everything above it. Only the ordinary
            // swing path calls this; the signature leap (Reckoning) already teleports the whole
            // figure its own way, and stacking this on top of that would be two different
            // teleports arguing over the same swing.
            if (PhantomFlicker) StartCoroutine(PhantomPoof(swingWindow));

            // Whirlwind's weapon-orbit, in place of the plain body spin - purely cosmetic, the
            // same as Phantom's poof just above. The hit itself is still resolved by ResolveArc
            // below exactly as any other Spin-style AoE finisher's is; only the picture changes.
            if (step.OrbitsWeapon)
                WhirlingBlade.Launch(gameObject, Rig, Facing, CurrentRange,
                                     AttackMotions.SwingSeconds(swingWindow));

            // Vortex's wind spiralling in - a picture only, the pull is ResolveArc's. It turns the
            // way the body spins (the Spin goes round through the front, from the facing).
            if (step.PullsIn)
                Combat.VortexWind.Play(transform.position,
                                       Mathf.Min(ScreenReach(), Tuning.Undertow.WindRadius),
                                       Tuning.Undertow.WindSeconds, Facing.x >= 0f ? -1f : 1f);

            // Lock only the finisher, and only for as long as it is actually ANIMATING - not the
            // whole interval. The difference is the recovery window, and on the heavy movesets it
            // is a fifth of a second of standing frozen after the swing has visibly ended.
            // Rooted extends the finisher's own movement lock to basics. Reusing _lockTimer
            // rather than adding a second flag means facing freezes too, which is what being
            // stuck mid-swing should actually feel like.
            // NeverLocks opts a finisher-slot move out entirely - Shadow's Echo is the only one.
            // Rooted still overrules it: that ledger cost extends the lock to BASICS, so a move
            // that merely declines the finisher's own lock has no standing against it, and a
            // player who took Rooted should feel it on everything.
            _lockTimer = (isFinisher && !step.NeverLocks) || Mods.RootedWhileSwinging
                ? AttackMotions.SwingSeconds(swingWindow) * (isFinisher ? ArtLockMul : 1f)
                : 0f;
            _settleIsFree = !step.ThrowsWeapon && !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            // The hit resolves on this frame (ResolveArc below), so the facing may follow the
            // target again once the rig has drawn the strike. Not for a thrown blade: it flies
            // along the facing and is recalled along it.
            _lockFreesAfterStrike = !step.ThrowsWeapon && !Mods.RootedWhileSwinging;

            // The strike lands here, so the timing bar closes here - before the damage is rolled.
            // A no-op for a basic and for a finisher that runs no bar (Shadow's Echo).
            SettleStrike();

            if (step.ThrowsWeapon)
            {
                ThrowWeapon(step);
                AdvanceCombo(isFinisher);
                return;
            }

            ResolveArc(step, isFinisher);
        }

        /// <summary>
        /// Impale: the thrust starts out of the chamber the wind-up pulled the blade back into,
        /// the player dashes forward with it (Tuning.Impale.LungeSeconds), and the hit resolves
        /// where the lunge ends - so the reach is measured from where the body arrived. The chain
        /// already advanced at dispatch, so the resolve passes advanceCombo: false.
        /// </summary>
        IEnumerator LungeStrike(AttackStep step, bool isFinisher, float swingWindow, bool alt)
        {
            float seconds = Core.Tuning.Impale.LungeSeconds;

            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt);
            if (isFinisher) Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            SmearSwing(swingWindow, isFinisher);
            Echoes?.Trail(step.Motion, swingWindow, alt);

            _lockTimer = (isFinisher && !step.NeverLocks) || Mods.RootedWhileSwinging
                ? AttackMotions.SwingSeconds(swingWindow) * (isFinisher ? ArtLockMul : 1f)
                : 0f;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)

            var dir = Facing.sqrMagnitude > 0.0001f ? Facing.normalized : Vector2.right;
            Vector2 start = transform.position;
            // Impale's lunge (PiercesPath) is a DASH: the full distance, walls alone stopping it,
            // so it crosses a narrow chasm - and one that ends over the void FALLS, judged where it
            // ends like the dash ability and the leap (the user's call, 2026-10-14: a player
            // learns their moveset's drawbacks). Any other lunge keeps to the floor.
            float distance = step.PiercesPath ? step.LungeDistance : LungeReach(dir, step.LungeDistance);
            if (distance > 0.05f) Dash(dir, distance / seconds, seconds);

            yield return new WaitForSeconds(seconds);

            SettleStrike();   // the lunge's end is the strike
            if (!step.PiercesPath)
            {
                ResolveArc(step, isFinisher, advanceCombo: false);
                yield break;
            }

            // Through the crowd: one strike laid along the whole path - from where the lunge
            // started to the thrust's reach past where the body is now (a wall may have cut the
            // dash short, so the distance is measured, not assumed).
            float travelled = Vector2.Distance(start, transform.position);
            var path = step.Clone();
            path.RangeBonus += travelled / Mathf.Max(0.01f, Mods.RangeMul);
            _arcOrigin = start;
            _arcFacing = dir;
            try { ResolveArc(path, isFinisher, advanceCombo: false); }
            finally { _arcOrigin = null; _arcFacing = null; }
        }

        /// <summary>
        /// Slide <paramref name="hp"/> out from <paramref name="origin"/> to past the rim of a
        /// <paramref name="radius"/> circle (Tuning.Sunder): a near body travels further, every
        /// body at least PushMin. Finisher Knockback lengthens it. Bosses (kinematic), anchored
        /// elites and a body already falling are left where they are.
        /// </summary>
        void PushOut(Health hp, Vector2 origin, float dist, float radius)
        {
            if (hp.Anchored) return;
            var rb = hp.GetComponent<Rigidbody2D>();
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;
            var enemy = hp.GetComponent<Enemies.EnemyController>();
            if (enemy != null && enemy.FellIn) return;

            float travel = Mathf.Max(Core.Tuning.Sunder.PushMin, radius + Core.Tuning.Sunder.PushPast - dist);
            travel *= Art.Gear.StatPercents.Apply(1f, Stats.FinisherKnockback);
            rb.linearVelocity = Vector2.zero;   // the impulse is replaced, not added to
            Combat.DragToward.Push(hp, origin, travel);
        }

        /// <summary>
        /// The Grappling Hook (Combat/GrapplingHook, Tuning.Grapple): the blade points along the
        /// aim and the chain leaves the sword hand. The throw is the strike - the bar closes as it
        /// leaves, as a thrown blade's does - and the damage is rolled once, here. The player is
        /// held (no walking, the facing kept) until the chain is home; a haul is the player's own
        /// dash. A miss is spent: the chain comes home empty and the rotation moves on.
        /// </summary>
        /// <summary>
        /// An ordinary swing whose hit waits for the drawn strike (AttackStep.StrikesOnDraw): the
        /// motion, its trail and lock from the press, the bar settled and the arc resolved when the
        /// rig draws the strike.
        /// </summary>
        IEnumerator DrawnStrike(AttackStep step, bool isFinisher, float swingWindow, bool alt)
        {
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(step.HopHeight);
            Rig?.PlayAttack(step.Motion, swingWindow, alt);
            if (isFinisher) Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            SmearSwing(swingWindow, isFinisher);
            Echoes?.Trail(step.Motion, swingWindow, alt);
            _lockTimer = (isFinisher && !step.NeverLocks) || Mods.RootedWhileSwinging
                ? AttackMotions.SwingSeconds(swingWindow) * (isFinisher ? ArtLockMul : 1f)
                : 0f;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            var facing = Facing;
            float wait = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            if (wait > 0f) yield return new WaitForSeconds(wait);
            SettleStrike();   // the drawn strike is the judged one
            _arcFacing = facing;
            try { ResolveArc(step, isFinisher, advanceCombo: false); }
            finally { _arcFacing = null; }
        }

        /// <summary>
        /// Plow (Combat/DiscPlow): the cast, then both discs bowled along the ground in two lanes
        /// either side of the aim. HEAVY: the damage rolled once at the release, the first body a
        /// full hit and each after it less (the swing's falloff); every body struck FLINCHES through
        /// armour and is slid out to its side of the path (an Anchored elite and a boss stay). Held
        /// until the discs are home.
        /// </summary>
        IEnumerator PlowStrike(AttackStep step, bool isFinisher, float swingWindow, bool alt)
        {
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt);
            float release = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            SettleStrike();   // the release is the strike

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier;
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();
            var element = Resource?.Element ?? ElementType.Fire;
            float falloff = CleaveFalloff(step.ChainFalloff);

            if (release > 0f) yield return new WaitForSeconds(release);

            var dir = Facing.sqrMagnitude > 0.0001f ? Facing.normalized : Vector2.right;
            var perp = new Vector2(-dir.y, dir.x);
            Vector2 from = transform.position;
            int bodies = 0;
            bool struck = false, home = false;
            Combat.DiscPlow.Bowl(this, dir, (hp, side) =>
            {
                float share = Mathf.Max(MinChainFraction, Mathf.Pow(falloff, bodies));
                float d = ScaleThrownHit(hp, dmg * share, true, crit);
                var info = new DamageInfo(d, element, gameObject) { Thrown = true, IsFinisher = true, Crit = crit };
                hp.Take(info);
                OnWeaponUsed?.Invoke();
                Resource?.OnHitLanded(hp, info);
                NotifyThrownHit(hp, info, 0f, bodies == 0);
                bodies++;
                struck = true;
                if (hp.IsDead) return;
                hp.GetComponent<Enemies.EnemyController>()?.Flinch(Core.Tuning.Finisher.FlinchSeconds, true);

                // Slid OUT to its side of the path - until it stands PushPast beyond the outside
                // of the lane, at least PushMin - the crowd parted.
                if (hp.Anchored || hp.GetComponent<Bosses.Boss>() != null) return;
                var rb = hp.GetComponent<Rigidbody2D>();
                if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) return;
                float offset = Vector2.Dot(rb.position - from, perp) * side;   // how far out already
                float want = Tuning.Plow.LaneHalf + Tuning.Plow.LaneRadius + Tuning.Plow.PushPast;
                float travel = Mathf.Max(Tuning.Plow.PushMin, want - offset)
                               * Art.Gear.StatPercents.Apply(1f, Stats.FinisherKnockback);
                rb.linearVelocity = Vector2.zero;
                // Pushed from a point straight across the path from it, so it goes square out.
                Combat.DragToward.Push(hp, rb.position - perp * side, travel);
            }, () => home = true);

            float swing = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            float t = 0f;
            while (!home)
            {
                _lockTimer = Mathf.Max(_lockTimer, Mathf.Max(swing - t, 0.05f));
                t += Time.deltaTime;
                yield return null;
            }
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            if (!struck && _streakAwaiting) ResolveStreak(false);
        }

        /// <summary>
        /// Tether (Combat/DiscTether): the cast, then one disc round past the locked target (or
        /// along the facing) and back, taking the Light's damage rolled once at the throw, the
        /// thrown blade's rules; enemies it passes take TagShare each, falling off in order; an
        /// Anchored elite is FLINCHED through its armour instead of dragged. Held until home.
        /// </summary>
        /// <summary>
        /// Gyre (AttackStep.Gyres; the user's call, 2026-10-16 - a helicopter-style melee pull,
        /// aimed as a cone): the lift plays and as the arms reach out both discs leave the hands to
        /// spin IN FRONT as a rotor (Combat.DiscRotor); for Tuning.Gyre.PullSeconds every enemy in the
        /// cone along the aim is DRAWN in to DragStopAt in front (Tether's drag: MovePosition each
        /// physics step, marked shoved, a wall between snags it; an Anchored elite in the cone is
        /// FLINCHED instead, a boss untouched) while the rig holds the cut's first frame, and the
        /// rotor GRINDS whatever comes near it (Tuning.Gyre.Grind*); then the cut - the arms slash
        /// down, the discs back in the hands - is the judged strike, an ordinary melee hit
        /// (ResolveArc) along the aim. Rooted throughout; the gate held to the end.
        /// </summary>
        IEnumerator GyreStrike(AttackStep step, bool isFinisher, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            var aim = Facing;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            float lift = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            float cut = Rig != null ? Rig.StrikeDrawnSeconds(AttackMotion.DiscGyreCut, swingWindow) : 0f;
            float swing = AttackMotions.SwingSeconds(swingWindow);
            float total = lift + Tuning.Gyre.PullSeconds + swing;
            _lockTimer = total * ArtLockMul;
            if (lift > 0f) yield return new WaitForSeconds(lift);

            // The discs leave the raised hands: the rotor.
            Vector2 main = transform.position, off = transform.position;
            if (Rig != null && Rig.Transform != null && Rig.TryGetSwordHand(out var local))
                main = off = Rig.Transform.TransformPoint(local);
            var offR = Rig?.OffhandRenderer;
            if (offR != null && offR.enabled) off = offR.transform.position;
            var rotor = Combat.DiscRotor.Spin(this, aim, main, off);
            rotor.Drafting = true;
            Rig?.HoldSwingStart(AttackMotion.DiscGyreCut, false, Tuning.Gyre.PullSeconds);

            // What is in the cone along the aim, and not behind a wall.
            bool InCone(Health hp, out Vector2 to)
            {
                to = (Vector2)hp.transform.position - (Vector2)transform.position;
                float d = to.magnitude;
                if (d > Tuning.Gyre.Reach || d < 0.01f) return false;
                if (Vector2.Angle(aim, to) > Tuning.Gyre.HalfAngle) return false;
                return !Arena.WallBetween(transform.position, hp.transform.position);
            }

            // Too heavy to move: flinched as the draft takes hold (Tether's rule).
            if (!whiffed)
            {
                Enemies.EnemyRegistry.Prune();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    var hp = e != null ? e.GetComponent<Health>() : null;
                    if (hp == null || hp.IsDead || !hp.Anchored || !InCone(hp, out _)) continue;
                    if (hp.GetComponentInParent<Bosses.Boss>() == null)
                        e.Flinch(Core.Tuning.Finisher.FlinchSeconds, punchesThroughArmour: true);
                }
            }

            // The grind (the user's call): what comes close to the turning rotor is cut as it
            // arrives - a melee hit through ResolveArc, filtered to those bodies, no hit-stop.
            // Gathered all round (filtered to the bodies near the rotor below), not along the cut's
            // strip: one jostled beside the player by the pile is still at the blades.
            var grind = step.Clone();
            grind.SuppressHitstop = true;
            grind.AreaOfEffect = true;
            grind.RangeBonus += Tuning.Gyre.RotorAhead;
            bool grindFirst = true;
            float grindClock = 0f;
            void Grind()
            {
                Vector2 hub = (Vector2)transform.position + aim * Tuning.Gyre.RotorAhead;
                var near = new HashSet<Health>();
                Enemies.EnemyRegistry.Prune();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    var hp = e != null ? e.GetComponent<Health>() : null;
                    if (hp == null || hp.IsDead) continue;
                    if (Vector2.Distance(hub, hp.transform.position) <= Tuning.Gyre.GrindRadius) near.Add(hp);
                }
                if (near.Count == 0) return;
                Facing = aim;
                _arcOnly = near;
                _arcRepeat = !grindFirst;
                try { ResolveArc(grind, isFinisher, advanceCombo: false, damageScale: Tuning.Gyre.GrindShare); }
                finally { _arcOnly = null; _arcRepeat = false; }
                grindFirst = false;
            }

            // The pull: every physics step, the cone is drawn in. A body once CAUGHT stays caught
            // to the end - the pile jostles, and one shoved a little out of the cone used to be
            // let go short of everything.
            var caught = new HashSet<Health>();
            float pulled = 0f;
            var fixedWait = new WaitForFixedUpdate();
            while (pulled < Tuning.Gyre.PullSeconds)
            {
                yield return fixedWait;
                pulled += Time.fixedDeltaTime;
                if (whiffed) continue;
                grindClock += Time.fixedDeltaTime;
                if (grindClock >= Tuning.Gyre.GrindEvery) { grindClock -= Tuning.Gyre.GrindEvery; Grind(); }
                Enemies.EnemyRegistry.Prune();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    if (e == null || e.FellIn) continue;
                    var hp = e.GetComponent<Health>();
                    if (hp == null || hp.IsDead || hp.Anchored) continue;
                    if (hp.GetComponentInParent<Bosses.Boss>() != null) continue;
                    var rb = e.GetComponent<Rigidbody2D>();
                    if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) continue;
                    var to = (Vector2)hp.transform.position - (Vector2)transform.position;
                    if (!caught.Contains(hp))
                    {
                        if (!InCone(hp, out to)) continue;
                        caught.Add(hp);
                    }
                    else if (Arena.WallBetween(transform.position, hp.transform.position)) continue;
                    float left = to.magnitude - Tuning.Gyre.DragStopAt;
                    if (left <= 0.02f) continue;
                    float move = Mathf.Min(left, Tuning.Gyre.DragSpeed * Time.fixedDeltaTime);
                    rb.linearVelocity = Vector2.zero;
                    rb.MovePosition(rb.position - to.normalized * move);
                    hp.MarkShoved();   // a chasm on the way takes it, as a throw does
                }
            }

            // The cut: the arms chop down and the discs come back into the hands.
            rotor.Drafting = false;
            Rig?.PlayAttack(AttackMotion.DiscGyreCut, swingWindow, false);
            rotor.Land(cut);
            if (cut > 0f) yield return new WaitForSeconds(cut);
            Facing = aim;
            SettleStrike();   // the cut is the strike
            if (!whiffed)
            {
                // The grind already spent the swing's once-only effects if it struck anything.
                _arcRepeat = !grindFirst;
                try { ResolveArc(step, isFinisher, advanceCombo: false, damageScale: Tuning.Gyre.CutShare); }
                finally { _arcRepeat = false; }
            }

            float rest = swing - cut;
            if (rest > 0f) yield return new WaitForSeconds(rest * 0.5f);
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
        }

        IEnumerator TetherStrike(AttackStep step, bool isFinisher, float swingWindow, bool alt)
        {
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt);
            float release = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            SettleStrike();   // the cast is the strike

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier;
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();
            var element = Resource?.Element ?? ElementType.Fire;

            if (release > 0f) yield return new WaitForSeconds(release);

            var target = Target;
            if (target != null && Vector2.Distance(transform.position, target.transform.position) > Tuning.Tether.Reach)
                target = null;

            void Strike(Health hp, float amount, bool first)
            {
                float d = ScaleThrownHit(hp, amount, true, crit);
                var info = new DamageInfo(d, element, gameObject) { Thrown = true, IsFinisher = true, Crit = crit };
                hp.Take(info);
                OnWeaponUsed?.Invoke();
                Resource?.OnHitLanded(hp, info);
                NotifyThrownHit(hp, info, 0f, first);
            }

            bool struck = false, home = false;
            Combat.DiscTether.Throw(this, target, Facing, ElementInfo.Tint(element),
                onHit: hp => { Strike(hp, dmg, true); struck = true; },
                onTag: (hp, n) =>
                {
                    float falloff = CleaveFalloff(step.ChainFalloff);
                    Strike(hp, dmg * Tuning.Tether.TagShare * Mathf.Pow(falloff, n), false);
                    struck = true;
                },
                onAnchored: hp => hp.GetComponent<Enemies.EnemyController>()?.Flinch(
                    Core.Tuning.Finisher.FlinchSeconds, punchesThroughArmour: true),
                onFinished: () => home = true);

            float swing = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            float t = 0f;
            while (!home)
            {
                _lockTimer = Mathf.Max(_lockTimer, Mathf.Max(swing - t, 0.05f));
                t += Time.deltaTime;
                yield return null;
            }

            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            if (!struck && _streakAwaiting) ResolveStreak(false);
        }

        IEnumerator GrappleStrike(AttackStep step, bool isFinisher, float swingWindow, bool alt)
        {
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt);
            SmearSwing(swingWindow, isFinisher);
            Echoes?.Trail(step.Motion, swingWindow, alt);

            SettleStrike();   // the throw is the strike

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier;
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();
            var element = Resource?.Element ?? ElementType.Fire;

            bool struck = false, home = false;
            Combat.GrapplingHook.Throw(this, Facing,
                onHit: hp =>
                {
                    float d = ScaleThrownHit(hp, dmg, true, crit);
                    // Light: no flinch, and it moves nothing itself - the reel does.
                    var info = new DamageInfo(d, element, gameObject)
                    {
                        Thrown = true, IsFinisher = true, Crit = crit,
                    };
                    hp.Take(info);
                    OnWeaponUsed?.Invoke();
                    Resource?.OnHitLanded(hp, info);
                    NotifyThrownHit(hp, info, 0f, true);
                    struck = true;
                },
                onFinished: () => home = true);

            float swing = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            float t = 0f;
            while (!home)
            {
                _lockTimer = Mathf.Max(_lockTimer, Mathf.Max(swing - t, 0.05f));
                t += Time.deltaTime;
                yield return null;
            }

            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            // Home without catching a body: a perfect throw that caught nothing was air.
            if (!struck && _streakAwaiting) ResolveStreak(false);
        }

        /// <summary>
        /// Flurry: a downward cut, a rising cut, then the hilt turned and the pommel driven into
        /// the target (Tuning.Flurry). One motion plays the whole thing (AttackMotion.Flurry, keyed
        /// off the same Tuning numbers), and the hits land on its beats. The cuts are ordinary
        /// ResolveArc calls on a clone of the step at the cut's share, landing before the bar
        /// closes at its provisional GOOD; the pommel is
        /// the strike the bar judges - short, narrow, and the sequence's one hit-stop. The chain
        /// already advanced at dispatch, so every resolve passes advanceCombo: false. Committed for
        /// its whole length (_lockTimer), so the three blows land where the press aimed them.
        /// </summary>
        IEnumerator FlurryStrike(AttackStep step, bool isFinisher)
        {
            float seconds = Core.Tuning.Flurry.Seconds;
            // The rig animates SwingSeconds(window): ask for the window that makes it exactly this.
            float window = seconds / AttackMotions.FillFraction;

            _visual?.PlayAttack(step.Motion, window);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, window, false);
            if (isFinisher) Combat.WeaponTrail.Play(Rig, seconds);
            SmearSwing(window, isFinisher);
            Echoes?.Trail(step.Motion, window, false);

            _lockTimer = seconds * ArtLockMul;

            var cut = step.Clone();
            cut.DamageMultiplier = Core.Tuning.Flurry.CutShare;
            cut.SuppressHitstop = true;   // the pommel carries the sequence's freeze

            var pommel = step.Clone();
            pommel.DamageMultiplier = Core.Tuning.Flurry.PommelShare;
            pommel.RangeBonus += Core.Tuning.Flurry.PommelRangeBonus;
            pommel.StrikeWidth = Core.Tuning.Flurry.PommelWidth;
            pommel.ChainFalloff = Core.Tuning.Flurry.PommelFalloff;

            float clock = 0f;
            yield return new WaitForSeconds(Core.Tuning.Flurry.CutOneAt - clock);
            clock = Core.Tuning.Flurry.CutOneAt;
            ResolveArc(cut, isFinisher, advanceCombo: false);

            yield return new WaitForSeconds(Core.Tuning.Flurry.CutTwoAt - clock);
            clock = Core.Tuning.Flurry.CutTwoAt;
            ResolveArc(cut, isFinisher, advanceCombo: false);

            yield return new WaitForSeconds(Core.Tuning.Flurry.PommelAt - clock);

            SettleStrike();   // the pommel is the strike
            var range = (Art.Gear.StatPercents.Apply(BaseRange, Stats.Range) + pommel.RangeBonus) * Mods.RangeMul;
            Spr.Flash((Vector2)transform.position + Facing * Mathf.Max(0.3f, range * 0.8f),
                      0.32f, Color.white, 0.10f, false);
            ResolveArc(pommel, isFinisher, advanceCombo: false);
        }

        /// <summary>
        /// How far a lunge along <paramref name="dir"/> may go, up to <paramref name="max"/>: short
        /// of a locked target in front (it should end with the target inside the thrust, not run
        /// through it), and never off the floor - a wall or a chasm's edge stops it, so an attack
        /// can't carry the player into a fall.
        /// </summary>
        float LungeReach(Vector2 dir, float max, bool stopAtTarget = true)
        {
            Vector2 from = transform.position;

            var target = Target;
            if (stopAtTarget && target != null && !target.IsDead)
            {
                Vector2 to = (Vector2)target.transform.position - from;
                if (Vector2.Dot(to.normalized, dir) > 0.5f)
                    max = Mathf.Min(max, Mathf.Max(0f, to.magnitude - Core.Tuning.Impale.StopShort));
            }

            const float step = 0.1f;
            float reach = 0f;
            for (float d = step; d <= max + 0.0001f; d += step)
            {
                if (!Core.Arena.OnFloor(from + dir * d, Core.Tuning.Impale.EdgeMargin)) break;
                reach = d;
            }
            return Mathf.Min(reach, max);
        }

        /// <summary>
        /// Wind up, then strike. The player is rooted for the whole charge AND the swing - that
        /// commitment is the entire price of the damage, and a charge you could walk out of would
        /// just be a slower normal attack.
        ///
        /// Uses scaled time throughout, so opening a screen mid-charge pauses it rather than
        /// letting the strike resolve behind the pause.
        /// </summary>
        IEnumerator ChargedStrike(AttackStep step, bool isFinisher, float swingWindow)
        {
            yield return WindUp(step.ChargeSeconds);
            SettleStrike();   // the release is the strike

            // The swing itself still commits, exactly like every other finisher.
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)

            // The strike itself. No hop: the wind-up already spent the anticipation this move
            // has, and hopping out of a held charge would read as the pose being abandoned.
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt: false);

            // Always a finisher on this path - charge, leap and draw-cut are nothing else.
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            SmearSwing(swingWindow, isFinisher: true);

            Spr.Flash(transform.position, 1.4f, Color.white, 0.3f);
            ResolveArc(step, isFinisher, advanceCombo: false);
        }

        /// <summary>
        /// PRIMA MATERIA (AttackStep.Boomerang), the Aether Longbow's weapon art.
        ///
        ///   throw     the bow leaves the hand like a boomerang, curving out to the locked target
        ///             (Combat.BoomerangBow). The judged strike: the bar settles here.
        ///   hit       the first enemy it touches takes BoomerangShare of the art, and the world
        ///             drops into BULLET TIME (GamePause.Slow). The stone is thrown up overhead
        ///             (Combat.RelicToss) while the bow curves home.
        ///   catch     the bow is back in the hand, and looses ShotsPerEnemy rapid shots at the
        ///             struck enemy and every enemy within AoeRadius of it, nearest it first, each
        ///             for ShotShare less a swing's falloff per enemy before it.
        ///   settle    the stone drops back onto the hip and time runs again.
        ///
        /// A throw that strikes nothing just comes home: no bullet time, no volley - the art is
        /// spent. Damage is rolled ONCE at the throw and carried by every hit (the thrown blade's
        /// rule). Locked throughout: the volley turns the character to each target in turn.
        /// </summary>
        IEnumerator PrimaMateriaStrike(AttackStep step, float swingWindow)
        {
            float swing = AttackMotions.SwingSeconds(swingWindow);

            // The Magnum Opus's gather: the armour's and the bow's light drains to black, swells in
            // the stone, flashes back through the armour and floods the bow (with its lightning).
            var glow = Art.Gear.MagnumOpusGlow.Begin(Rig);
            var watch = new DrawWatch();
            StartCoroutine(WatchDraw(watch, step.ChargeSeconds));   // the gather is its draw
            yield return WindUp(step.ChargeSeconds, step.Motion);

            _lockTimer = 60f;
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt: false);
            SettleStrike();   // the throw is the strike
            glow?.Fire();
            // The pair leaves as the arms sweep apart (the pincer throw's drawn strike).
            float sling = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            if (sling > 0f) yield return new WaitForSeconds(sling);     // the light leaves with the bow - it flies fully lit

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier;
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();
            dmg *= watch.DamageMul;   // plant to fire
            var element = Resource?.Element ?? ElementType.Fire;

            var target = Targeting != null ? Targeting.Target : null;
            Sprite wSprite = null; Color wTint = Color.white; Vector2 wSize = Vector2.one;
            Rig?.TryGetWeaponVisual(out wSprite, out wTint, out wSize);
            Rig?.SetWeaponVisible(false);
            OnWeaponUsed?.Invoke();   // one throw's wear

            Health struck = null;
            Vector2 struckAt = default;
            bool caught = false;
            Combat.RelicToss toss = null;
            Combat.BoomerangBow.Launch(gameObject, target, Facing, CurrentRange, wSprite, wTint, wSize,
                onStrike: hp =>
                {
                    struck = hp;
                    struckAt = hp.transform.position;
                    float hit = ScaleThrownHit(hp, dmg * Core.Tuning.PrimaMateria.BoomerangShare, true, crit, 0.5f);
                    var away = ((Vector2)hp.transform.position - (Vector2)transform.position).normalized;
                    var info = new DamageInfo(hit, element, gameObject)
                    {
                        Knockback = away * step.Knockback * Art.Gear.StatPercents.Apply(1f, Stats.FinisherKnockback),
                        Displaces = true, Thrown = true, IsFinisher = true, Crit = crit, Disintegrates = true,
                    };
                    hp.Take(info);
                    Resource?.OnHitLanded(hp, info);
                    NotifyThrownHit(hp, info, 0.5f, true);
                    // A small ring, not a burst: bullet time holds it on screen ~3x as long.
                    Spr.Flash(struckAt, 0.35f, Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 0), 0.12f);

                    // The world slows; the stone goes up while the bow comes home.
                    GamePause.Slow(this, Core.Tuning.PrimaMateria.BulletTimeScale);
                    toss = Combat.RelicToss.Toss(Rig?.TrinketRenderer, transform, struckAt);
                },
                onCaught: () => caught = true);

            yield return new WaitForSeconds(swing * 0.5f);
            _lockTimer = 60f;   // still locked while the bow is out - re-set past the throw's own clock
            while (!caught) yield return null;

            Rig?.SetWeaponVisible(true);
            Spr.Pulse(transform, 0.5f, Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 1), 0.2f);

            if (struck != null)
            {
                // The volley: the struck enemy first if it still stands, then everything round it,
                // nearest it first.
                var targets = new List<Health>();
                if (!struck.IsDead) targets.Add(struck);
                var near = new List<(Health hp, float d)>();
                foreach (var col in Physics2D.OverlapCircleAll(struckAt, Core.Tuning.PrimaMateria.AoeRadius))
                {
                    var hp = col != null ? col.GetComponent<Health>() : null;
                    if (hp == null || hp.IsDead || hp == struck || hp.gameObject == gameObject) continue;
                    if (hp.GetComponent<Enemies.EnemyController>() == null && hp.GetComponent<Bosses.Boss>() == null) continue;
                    if (near.Exists(n => n.hp == hp)) continue;
                    near.Add((hp, Vector2.Distance(hp.transform.position, struckAt)));
                }
                near.Sort((a, b) => a.d.CompareTo(b.d));
                foreach (var n in near)
                    if (targets.Count < Core.Tuning.PrimaMateria.MaxTargets) targets.Add(n.hp);

                float chain = 1f, falloff = CleaveFalloff(step.ChainFalloff);
                var shot = Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 1);
                var sear = Color.Lerp(Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 0), Color.white, 0.6f);
                // The volley planned up front, so the LAST shot is known when it leaves: it is the
                // one that knocks the stone back home (the user's call).
                var plan = new List<(Health hp, float chain)>();
                foreach (var t in targets)
                {
                    for (int s = 0; s < Core.Tuning.PrimaMateria.ShotsPerEnemy; s++) plan.Add((t, chain));
                    chain = Mathf.Max(MinChainFraction, chain * falloff);
                }

                bool knocked = false;
                ThrownArrow lastFired = null;
                for (int i = 0; i < plan.Count; i++)
                {
                    var (t, c) = plan[i];
                    if (t == null || t.IsDead) continue;
                    var to = ((Vector2)t.transform.position - (Vector2)transform.position);
                    if (to.sqrMagnitude > 0.0001f) Facing = to.normalized;
                    Rig?.PlayAttack(AttackMotion.Draw, Core.Tuning.PrimaMateria.ShotSpacing * 2f, false);
                    var arrow = ThrownArrow.Fire(gameObject, t, Facing,
                                                 dmg * Core.Tuning.PrimaMateria.ShotShare * c, element,
                                                 CurrentRange * 2f, 1f, isFinisher: true, crit: crit);
                    arrow.Disintegrates = true;
                    arrow.Knockback = HeavyKnockback(step, true);
                    arrow.SuppressHitstop = true;   // one freeze for the art - the boomerang's
                    arrow.Tint = shot;
                    lastFired = arrow;
                    // Every shot goes THROUGH the stone hanging at eye level, and comes out of it
                    // searing (the user's call); the last one strikes it back to the wearer.
                    if (toss != null)
                    {
                        var stone = toss;
                        bool last = i == plan.Count - 1;
                        arrow.transform.position = Rig?.WeaponRenderer != null
                            ? Rig.WeaponRenderer.bounds.center : transform.position;
                        arrow.Through = () => stone != null ? stone.Point : (Vector2)arrow.transform.position;
                        arrow.OnThrough = a =>
                        {
                            a.Tint = sear;
                            if (stone == null) return;
                            if (last) { stone.KnockBack(); knocked = true; }
                            else stone.Pulse();
                        };
                    }
                    if (i < plan.Count - 1) yield return new WaitForSeconds(Core.Tuning.PrimaMateria.ShotSpacing);
                }

                // The planned last shot never left (its target fell first): the last one that DID
                // knocks the stone, as it passes through.
                if (toss != null && !knocked)
                {
                    float wait = 0f;
                    while (lastFired != null && lastFired.Through != null && wait < 1f)
                    {
                        wait += Time.deltaTime;
                        yield return null;
                    }
                    toss.KnockBack();
                }
            }

            if (toss != null)
            {
                // Bullet time holds until the stone is home on the hip.
                if (struck == null) toss.Land();
                float wait = 0f;
                while (toss != null && wait < 2f)
                {
                    wait += Time.deltaTime;
                    yield return null;
                }
            }
            GamePause.Unslow(this);
            _lockTimer = 0f;
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            // A throw that struck nothing never connected: a perfect on it was thrown at air.
            if (struck == null && _streakAwaiting) ResolveStreak(false);
        }

        /// <summary>
        /// SWARM (AttackStep.Swarm), The Hive's weapon art.
        ///
        ///   draw      the bow is drawn exactly as for a shot; the release is the judged strike.
        ///   release   instead of an arrow, every arrow the bow is made of leaves as one swarm
        ///             (Combat.HiveSwarm) - the bow in the hand is only its grip until they are back.
        ///   swarm     the arrows ball round the locked target and dart in one at a time: the target
        ///             takes TargetHits of TargetShare (one Heavy), and the enemies near it NearHits
        ///             of NearShare each, less a swing's falloff per enemy. The target's last strike
        ///             is the art's flinch (and, were the art ever Heavy, its displacement).
        ///   home      the arrows fly back to their places and the bow is whole.
        ///
        /// Damage is rolled ONCE at the release and carried by every strike (the thrown blade's
        /// rule). The player is free to move throughout (the draw is plant-to-fire) but
        /// cannot attack until it is home.
        /// </summary>
        IEnumerator SwarmStrike(AttackStep step, float swingWindow)
        {
            float draw = AttackMotions.SwingSeconds(swingWindow);
            Rig?.PlayAttack(step.Motion, draw, false);
            var watch = new DrawWatch();
            yield return WatchDraw(watch, draw);
            SettleStrike();   // the release is the strike

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier;
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            dmg *= watch.DamageMul;   // plant to fire
            if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();
            var element = Resource?.Element ?? ElementType.Fire;
            OnWeaponUsed?.Invoke();   // one release's wear

            bool struck = false;
            var target = Targeting != null ? Targeting.Target : null;
            GetComponent<Combat.HiveLoose>()?.Cancel();   // a shot's arrow still out comes home first
            var swarm = Combat.HiveSwarm.Launch(gameObject, Rig?.WeaponRenderer, target, Facing,
                CleaveFalloff(step.ChainFalloff), MinChainFraction,
                (hp, share, main, sting) =>
                {
                    if (hp == null || hp.IsDead) return;
                    struck = true;
                    float hit = ScaleThrownHit(hp, dmg * share, true, crit, 0.5f, main);
                    var away = ((Vector2)hp.transform.position - (Vector2)transform.position).normalized;
                    bool heavy = step.Weight == FinisherWeight.Heavy;
                    var info = new DamageInfo(hit, element, gameObject)
                    {
                        Knockback = sting && heavy ? away * step.Knockback * Art.Gear.StatPercents.Apply(1f, Stats.FinisherKnockback) : Vector2.zero,
                        Displaces = sting && heavy, Thrown = true, IsFinisher = true, Crit = crit,
                        SuppressHitstop = !sting,   // one freeze for the art - the last sting's
                    };
                    var where = hp.transform.position;
                    hp.Take(info);
                    // The finisher's flinch, as a swing's (ResolveArc): Medium is refused by armour.
                    if (sting && !hp.IsDead && step.Weight != FinisherWeight.Light)
                        hp.GetComponent<Enemies.EnemyController>()?.Flinch(Core.Tuning.Finisher.FlinchSeconds, heavy);
                    Resource?.OnHitLanded(hp, info);
                    NotifyThrownHit(hp, info, 0.5f, main);
                    Spr.Flash(where, sting ? 0.45f : 0.22f, ElementInfo.Tint(element), 0.1f, false);
                });

            while (swarm != null && !swarm.Home) yield return null;
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            // A swarm that struck nothing never connected: a perfect on it was loosed at air.
            if (!struck && _streakAwaiting) ResolveStreak(false);
        }

        /// <summary>
        /// THE FOUR BIRDS (AttackStep.Formation), the Phoenix's weapon art (the user's calls,
        /// 2026-10-17: a formation of all the bows, a flanking volley, Heavy; how each bird's arrow
        /// flies is mine).
        ///
        ///   draw      the bow is drawn exactly as for a shot, and the Crow, the Swan and the Peacock
        ///             form up round it as bows of light (Combat.BowFormation), drawing with it. The
        ///             release is the judged strike.
        ///   volley    the three loose at once: the CROW's arrow flies straight through its line,
        ///             piercing every body (a swing's falloff); the SWAN's strikes the target and
        ///             SPLITS into arrows at the nearest others; the PEACOCK's strikes it and bursts
        ///             into eyes that MARK it and every enemy near it (Tuning.Mark).
        ///   phoenix   a beat later the player's own arrow - the Heavy's flinch and knockback - lands
        ///             on the target, spending the peacock's mark. The birds' arrows spare marks
        ///             (DamageInfo.SparesMark), so those left on the rest wait for the next art.
        ///
        /// Damage is rolled ONCE at the release (the thrown blade's rule); the target takes exactly
        /// one Heavy when everything lands (Tuning.Formation's shares). Nothing locked: the crow's
        /// arrow still flies down the facing, the rest are spent.
        /// </summary>
        IEnumerator FormationStrike(AttackStep step, float swingWindow)
        {
            float draw = AttackMotions.SwingSeconds(swingWindow);
            Rig?.PlayAttack(step.Motion, draw, false);
            var formation = Combat.BowFormation.Gather(this, draw);
            var watch = new DrawWatch();
            yield return WatchDraw(watch, draw);
            SettleStrike();   // the release is the strike

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier;
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            dmg *= watch.DamageMul;   // plant to fire
            if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();
            var element = Resource?.Element ?? ElementType.Fire;
            OnWeaponUsed?.Invoke();   // one release's wear

            var target = Targeting != null ? Targeting.Target : null;
            if (target != null && (target.IsDead
                || Vector2.Distance(transform.position, target.transform.position) > CurrentRange)) target = null;
            Vector2 aim = target != null
                ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized : Facing;
            float range = CurrentRange;
            bool connected = false;

            var shots = formation != null ? formation.Release() : new Combat.BowFormation.Shot[0];
            foreach (var shot in shots)
            {
                switch (shot.Bird)
                {
                    case Combat.BowFormation.Bird.Crow:
                    {
                        // Straight down its line - through the target and on past it.
                        Vector2 at = target != null ? (Vector2)target.transform.position : shot.From + aim * range;
                        var dir = (at - shot.From).sqrMagnitude > 1e-4f ? (at - shot.From).normalized : aim;
                        var a = ThrownArrow.Fire(gameObject, null, dir, dmg * Tuning.Formation.CrowShare, element,
                            Vector2.Distance(shot.From, at) + Tuning.Formation.CrowOvershoot, 1f, true, crit,
                            shot.From, shot.Arrow);
                        a.Pierce(dir, CleaveFalloff(step.ChainFalloff), MinChainFraction);
                        a.SuppressHitstop = a.SparesMark = true;
                        a.OnHit = (hp, info) => connected = true;
                        break;
                    }
                    case Combat.BowFormation.Bird.Swan:
                    {
                        if (target == null) break;
                        var a = ThrownArrow.Fire(gameObject, target, aim, dmg * Tuning.Formation.SwanShare, element,
                            range, 1f, true, crit, shot.From, shot.Arrow);
                        a.SuppressHitstop = a.SparesMark = true;
                        var sprite = shot.Arrow;
                        a.OnHit = (hp, info) =>
                        {
                            connected = true;
                            // It SPLITS: arrows from the struck body to the nearest others.
                            foreach (var other in FormationNearest(hp, Tuning.Formation.SplitRadius, Tuning.Formation.SplitCount))
                            {
                                var from = (Vector2)hp.transform.position;
                                var d = ((Vector2)other.transform.position - from).normalized;
                                var split = ThrownArrow.Fire(gameObject, other, d, dmg * Tuning.Formation.SplitShare,
                                    element, Tuning.Formation.SplitRadius + 1f, 1f, true, crit, from, sprite);
                                split.SuppressHitstop = split.SparesMark = true;
                            }
                        };
                        break;
                    }
                    case Combat.BowFormation.Bird.Peacock:
                    {
                        if (target == null) break;
                        var a = ThrownArrow.Fire(gameObject, target, aim, dmg * Tuning.Formation.PeacockShare, element,
                            range, 1f, true, crit, shot.From, shot.Arrow);
                        a.SuppressHitstop = a.SparesMark = true;
                        a.OnHit = (hp, info) =>
                        {
                            connected = true;
                            // It bursts into EYES: the target and every enemy near it is marked.
                            var at = (Vector2)hp.transform.position;
                            if (!hp.IsDead) hp.GetComponent<StatusEffects>()?.ApplyMark(Tuning.Mark.Seconds, Tuning.Mark.Multiplier, element);
                            foreach (var other in FormationNearest(hp, Tuning.Formation.MarkRadius, 99))
                                other.GetComponent<StatusEffects>()?.ApplyMark(Tuning.Mark.Seconds, Tuning.Mark.Multiplier, element);
                            Spr.Flash(at, Tuning.Formation.MarkRadius, new Color(0.27f, 0.67f, 0.86f, 0.6f), 0.2f, true);
                        };
                        break;
                    }
                }
            }

            // The Phoenix's own arrow, a beat behind so it lands last - onto the mark.
            yield return new WaitForSeconds(Tuning.Formation.PhoenixDelay);
            Rig?.LooseArrow();
            if (target != null && !target.IsDead)
            {
                aim = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                var a = ThrownArrow.Fire(gameObject, target, aim, dmg * Tuning.Formation.PhoenixShare, element,
                                         range, 1f, true, crit);
                a.SplashFraction = SplashFraction;
                a.PierceFraction = PierceFraction;
                a.Knockback = HeavyKnockback(step, true);
                a.OnHit = (hp, info) =>
                {
                    connected = true;
                    // The Heavy's flinch, through armour, as a swing's (ResolveArc).
                    if (!hp.IsDead) hp.GetComponent<Enemies.EnemyController>()?.Flinch(Tuning.Finisher.FlinchSeconds, true);
                };
            }

            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            // A volley that struck nothing never connected: a perfect on it was loosed at air.
            yield return new WaitForSeconds(1f);
            if (!connected && _streakAwaiting) ResolveStreak(false);
        }

        /// <summary>Living enemies within <paramref name="radius"/> of <paramref name="from"/>, not
        /// it, nearest first, at most <paramref name="count"/>.</summary>
        List<Health> FormationNearest(Health from, float radius, int count)
        {
            var list = new List<(Health hp, float d)>();
            Vector2 at = from.transform.position;
            foreach (var col in Physics2D.OverlapCircleAll(at, radius))
            {
                var hp = col != null ? col.GetComponentInParent<Health>() : null;
                if (hp == null || hp == from || hp.IsDead) continue;
                if (col.GetComponentInParent<Enemies.EnemyController>() == null && col.GetComponentInParent<Bosses.Boss>() == null) continue;
                if (list.Exists(e => e.hp == hp)) continue;
                list.Add((hp, Vector2.Distance(at, hp.transform.position)));
            }
            list.Sort((p, q) => p.d.CompareTo(q.d));
            var result = new List<Health>();
            for (int i = 0; i < list.Count && i < count; i++) result.Add(list[i].hp);
            return result;
        }

        void OnDisable() => GamePause.Unslow(this);

        /// <summary>A piece's stone falls away (Combat.TexelShed) and its spirit is shown in its place
        /// (Art.Gear.GearSpirit) until the override is removed. Null for a piece with no spirit.</summary>
        static Art.Gear.SpriteOverride Shed(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) return null;
            var spirit = Art.Gear.GearSpirit.Of(sr.sprite);
            if (spirit == null) return null;
            Combat.TexelShed.Drop(sr, sr.sprite, spirit);
            return Art.Gear.SpriteOverride.Apply(sr, spirit);
        }

        /// <summary>A dark copy of the figure as it stands, left on the ground and fading - the
        /// shadow step's after-image (Player.RigSilhouette).</summary>
        void DropAfterImage(Color color, float seconds)
        {
            var rigRoot = (Rig as MonoBehaviour)?.transform;
            if (rigRoot == null) return;
            var sil = RigSilhouette.Create(transform.parent, rigRoot);
            if (sil == null) return;
            sil.SetColor(color);
            sil.SetVisible(true);
            sil.SetOrder(SortingOrders.ForDepth(transform.position.y));
            sil.Sync();
            StartCoroutine(FadeAfterImage(sil, seconds));
        }

        static IEnumerator FadeAfterImage(RigSilhouette sil, float seconds)
        {
            float left = seconds;
            while (left > 0f && sil != null)
            {
                left -= Time.deltaTime;
                float t = Mathf.Clamp01(left / seconds);
                sil.Recolor(t * t);
                yield return null;
            }
            if (sil != null) Destroy(sil.gameObject);
        }

        /// <summary>
        /// KING AND QUEEN (AttackStep.Pincer), the Aether Dual Discs' weapon art.
        ///
        ///   gather   the Magnum Opus's: the armour's and both discs' light drains, the Geode Stone
        ///            swells, the flash runs back through the armour and lights the pair
        ///   pincer   both discs thrown at once on mirrored arcs that meet on the target, each
        ///            striking what it passes (PincerShare) - Combat.PincerDisc
        ///   catch    they carry on round their circles, crossing back; the wielder jumps as they
        ///            pass the target and catches both at the top
        ///   hurl     both straight down onto the target: a blast (BlastShare at its centre,
        ///            BlastEdge of that at its rim) that throws back what it hits; the wielder
        ///            lands with it, the discs' light goes out - their cracks show again - and
        ///            they spring back to the hands
        ///
        /// Damage rolled once at the throw. The jump is the drawing only (SetAirborne), like a
        /// leap's - the body stays on the ground.
        /// </summary>
        IEnumerator KingAndQueenStrike(AttackStep step, float swingWindow)
        {
            var glow = Art.Gear.MagnumOpusGlow.Begin(Rig);

            // The gather, with the STONE FALLING AWAY (the user's call): as the stone swells its
            // rind drops off the crystals, and as the flash reaches the pair the lions' stone
            // bodies drop off their heads, leaving the heads on rings of energy (GearSpirit).
            StartCoroutine(WindUp(step.ChargeSeconds, step.Motion));
            Art.Gear.SpriteOverride relicSpirit = null, mainSpirit = null, offSpirit = null;
            float gathered = 0f;
            bool relicShed = false, pairShed = false;
            while (gathered < step.ChargeSeconds)
            {
                gathered += Time.deltaTime;
                if (!relicShed && gathered >= Core.Tuning.KingAndQueen.RelicShedAt)
                {
                    relicShed = true;
                    relicSpirit = Shed(Rig?.TrinketRenderer);
                }
                if (!pairShed && gathered >= Core.Tuning.MagnumOpus.WeaponLitAt)
                {
                    pairShed = true;
                    mainSpirit = Shed(Rig?.WeaponRenderer);
                    offSpirit = Shed(Rig?.OffhandRenderer);
                }
                yield return null;
            }

            _lockTimer = 60f;
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt: false);
            SettleStrike();   // the throw is the strike
            glow?.Fire();

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier;
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();
            var element = Resource?.Element ?? ElementType.Fire;
            OnWeaponUsed?.Invoke();

            var target = Targeting != null ? Targeting.Target : null;
            Vector2 at = target != null && !target.IsDead
                ? (Vector2)target.transform.position
                : (Vector2)transform.position + Facing * Core.Tuning.KingAndQueen.ReachNoTarget;

            // Each half's picture - the lion in the main hand, the lioness in the off - then the
            // hands empty (reference counted, both halves).
            Sprite s0 = null, s1 = null; Color c0 = Color.white, c1 = Color.white; Vector2 z0 = Vector2.one, z1 = Vector2.one;
            Rig?.TryGetDiscVisual(0, out s0, out c0, out z0);
            Rig?.TryGetDiscVisual(1, out s1, out c1, out z1);
            // What flies is what the hands show - the spirits once shed - and each turns back into
            // its stone disc on the impact.
            Sprite k0 = mainSpirit != null ? mainSpirit.Original : null, k1 = offSpirit != null ? offSpirit.Original : null;
            if (Rig?.WeaponRenderer != null && mainSpirit != null) s0 = Rig.WeaponRenderer.sprite;
            if (Rig?.OffhandRenderer != null && offSpirit != null) s1 = Rig.OffhandRenderer.sprite;
            Rig?.SetWeaponVisible(false);
            Rig?.SetWeaponVisible(false);

            var chain = new Dictionary<Health, int>();
            void Pinch(Health hp)
            {
                // Each half's hit is PincerShare, on every body it passes; one struck by both halves
                // takes both (the target, where they meet). Only the first rings the hit-stop.
                chain.TryGetValue(hp, out int n);
                float d = ScaleThrownHit(hp, dmg * Core.Tuning.KingAndQueen.PincerShare, true, crit, 0.5f);
                var info = new DamageInfo(d, element, gameObject)
                    { Thrown = true, IsFinisher = true, Crit = crit, Disintegrates = true, SuppressHitstop = n > 0 };
                hp.Take(info);
                Resource?.OnHitLanded(hp, info);
                NotifyThrownHit(hp, info, 0.5f, n == 0);
                chain[hp] = n + 1;
            }
            var a = Combat.PincerDisc.Throw(gameObject, at, 1f, s0, c0, z0, Pinch, k0);
            var b = Combat.PincerDisc.Throw(gameObject, at, -1f, s1, c1, z1, Pinch, k1);

            // Out to the target...
            yield return new WaitForSeconds(Core.Tuning.KingAndQueen.OutSeconds);
            // ...and the jump as a SHADOW STEP (the user's call): a dark after-image left standing
            // where the wielder was, and the wielder already at the top, waiting for the pair.
            DropAfterImage(Core.Tuning.KingAndQueen.ShadowColor, Core.Tuning.KingAndQueen.ShadowSeconds);
            Rig?.SetAirborne(Core.Tuning.KingAndQueen.JumpHeight);
            // Both arms raised to receive the pair (the hurl's first frame), held through the catch.
            Rig?.HoldSwingStart(AttackMotion.DiscPincerHurl, false,
                                Core.Tuning.KingAndQueen.ReturnSeconds + Core.Tuning.KingAndQueen.HangSeconds + 1f);
            // Untouchable in the air, as a leap is (the user's call) - from the step to the landing.
            Airborne = true;
            if (Health) Health.Immune = true;
            Spr.Flash(transform.position + Vector3.up * Core.Tuning.KingAndQueen.JumpHeight, 0.5f,
                      Core.Tuning.KingAndQueen.ShadowColor, 0.16f);
            float up = 0f, rise = Core.Tuning.KingAndQueen.ReturnSeconds;
            while ((a != null && !a.Caught) || (b != null && !b.Caught))
            {
                up += Time.deltaTime;
                float k = Mathf.Clamp01(up / rise);
                float overheadLift = Core.Tuning.KingAndQueen.JumpHeight + Core.Tuning.KingAndQueen.CatchAbove;
                if (a != null) a.Lift = overheadLift * k;
                if (b != null) b.Lift = overheadLift * k;
                if (up > rise + 1f) break;
                yield return null;
            }
            float held = Core.Tuning.KingAndQueen.JumpHeight + Core.Tuning.KingAndQueen.CatchAbove;
            if (a != null) a.Lift = held;
            if (b != null) b.Lift = held;
            // THE UNION (the user's call, 2026-10-16): caught together at the top, the king and the
            // queen become ONE disc - her head on his ring, facing him (Art.Gear.KingQueenUnion) -
            // and come down as one. Only with the Aether Dual Discs AND the Geode Stone socketed
            // (CombinedInFlight, the Black Diamond rule); they part again on the impact.
            if (CombinedInFlight && a != null && b != null && a.Caught && b.Caught)
            {
                a.Unite(Art.Gear.KingQueenUnion.Of(s0, s1));
                b.Hidden = true;
                Spr.Flash(a.transform.position + Vector3.up * held, 0.6f,
                          Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 0), 0.18f);
            }
            yield return new WaitForSeconds(Core.Tuning.KingAndQueen.HangSeconds);

            // Hurled down onto the target, wherever it is now - the wielder comes down with them.
            if (target != null && !target.IsDead) at = target.transform.position;
            // Both arms drive down: the pair leaves as they come over (the hurl's cock end).
            Rig?.PlayAttack(AttackMotion.DiscPincerHurl, Core.Tuning.KingAndQueen.HurlSwingSeconds, false);
            float overhead = Rig != null
                ? Rig.StrikeStartSeconds(AttackMotion.DiscPincerHurl, Core.Tuning.KingAndQueen.HurlSwingSeconds) : 0f;
            if (overhead > 0f) yield return new WaitForSeconds(overhead);
            a?.Hurl(at);
            b?.Hurl(at);
            float fall = 0f;
            while (fall < Core.Tuning.KingAndQueen.HurlSeconds)
            {
                fall += Time.deltaTime;
                float k = Mathf.Clamp01(fall / Core.Tuning.KingAndQueen.HurlSeconds);
                float h = Core.Tuning.KingAndQueen.JumpHeight * (1f - k * k);
                Rig?.SetAirborne(h);
                if (a != null) a.Lift = held * (1f - k);
                if (b != null) b.Lift = held * (1f - k);
                yield return null;
            }
            Rig?.SetAirborne(0f);
            Airborne = false;
            if (Health) Health.Immune = false;
            if (a != null) a.Lift = 0f;
            if (b != null) b.Lift = 0f;

            // The impact: a blast round the target - and the stone back on everything: the discs in
            // flight turn to stone themselves (PincerDisc), the hands' and the hip's come back.
            mainSpirit?.Remove();
            offSpirit?.Remove();
            if (relicSpirit != null)
            {
                relicSpirit.Remove();
                if (Rig?.TrinketRenderer != null)
                    Spr.Flash(Rig.TrinketRenderer.bounds.center, 0.35f, Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 1), 0.15f);
            }
            float radius = Core.Tuning.KingAndQueen.BlastRadius * AoeScale;
            var tone = Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 0);
            Spr.Flash(at, radius * 0.45f, tone, 0.2f);
            // The scorch only at the middle - the wave carries the blast outward (a burn the whole
            // radius drowned it).
            Spr.GroundBurn(at, radius * 0.45f, Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 1), 0.45f);
            Combat.CameraKick.Kick(Vector2.down, 0.18f);   // the discs come DOWN

            // A WAVE OF ENERGY runs out across the ground from the impact (the user's call,
            // 2026-10-16), and the blast lands on each body as its front reaches it.
            var wave = Combat.EnergyWave.Run(at, radius,
                Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 0),
                Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 1),
                Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 2));
            bool blastStopped = false;   // one hit-stop for the blast, not one a body
            var blasted = new HashSet<Health>();
            float waveT = 0f;
            while (true)
            {
                float front = wave != null ? wave.Front : radius;
                foreach (var col in Physics2D.OverlapCircleAll(at, Mathf.Max(0.05f, front)))
                {
                    var hp = col != null ? col.GetComponent<Health>() : null;
                    if (hp == null || hp.IsDead || hp.gameObject == gameObject || blasted.Contains(hp)) continue;
                    if (hp.GetComponent<Enemies.EnemyController>() == null && hp.GetComponent<Bosses.Boss>() == null) continue;
                    blasted.Add(hp);
                    var off = (Vector2)hp.transform.position - at;
                    float d01 = Mathf.Clamp01(off.magnitude / Mathf.Max(0.01f, radius));
                    float hit = dmg * Core.Tuning.KingAndQueen.BlastShare
                                * Mathf.Lerp(1f, Core.Tuning.KingAndQueen.BlastEdge, d01);
                    hit = ScaleThrownHit(hp, hit, true, crit, d01);
                    var away = off.sqrMagnitude > 0.0001f ? off.normalized : Facing;
                    var info = new DamageInfo(hit, element, gameObject)
                    {
                        Knockback = away * step.Knockback * Art.Gear.StatPercents.Apply(1f, Stats.FinisherKnockback),
                        Displaces = true, Thrown = true, IsFinisher = true, Crit = crit, Disintegrates = true,
                        SuppressHitstop = blastStopped,
                    };
                    hp.Take(info);
                    blastStopped = true;
                    Resource?.OnHitLanded(hp, info);
                    NotifyThrownHit(hp, info, d01, false);
                }
                if (front >= radius - 0.001f || waveT > Core.Tuning.KingAndQueen.WaveSeconds + 0.2f) break;
                yield return null;
                waveT += Time.deltaTime;
            }

            // Their light out, their cracks showing again, they spring back to the hands.
            a?.Recall();
            b?.Recall();
            yield return new WaitForSeconds(Core.Tuning.KingAndQueen.RecallSeconds);
            Rig?.SetWeaponVisible(true);
            Rig?.SetWeaponVisible(true);
            _lockTimer = 0f;
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
        }

        /// <summary>
        /// The MAGNUM OPUS (AttackStep.MagnumOpus). Gather as a charge does - the held pose, the
        /// tightening ring, still moving and aiming - while the light makes its trip
        /// (Art.Gear.MagnumOpusGlow: out of the armour and the blade, into the relic, back through
        /// the armour and into the blade, swelling); then the big slash, and on its downstroke the
        /// crescent leaves the blade and the blade's glow drains with it.
        ///
        /// The light runs off whatever is worn: the Aether set lights piece by piece, a plain
        /// sword with the stone socketed has only the stone to swell and nothing to flash through -
        /// the move and its damage are the same either way.
        /// </summary>
        IEnumerator MagnumOpusStrike(AttackStep step, float swingWindow)
        {
            var glow = Art.Gear.MagnumOpusGlow.Begin(Rig);
            // The arms may carry nearly all the aim for this move, so the slash sweeps THROUGH the
            // crescent's line even aimed up or down. Set at the gather, so the hold eases onto it.
            Rig?.SetAimRange(Core.Tuning.MagnumOpus.AimRangeDegrees);
            yield return WindUp(step.ChargeSeconds);
            SettleStrike();   // the slash is the strike

            // The line is fixed as the slash starts - the rig freezes its mirror and its aim for the
            // swing at this same moment, so the blade and the crescent cannot disagree.
            var line = Facing;
            float swing = AttackMotions.SwingSeconds(swingWindow);
            _lockTimer = swing * ArtLockMul;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt: false);
            Combat.WeaponTrail.Play(Rig, swing);
            SmearSwing(swingWindow, isFinisher: true);

            // The crescent leaves the moment the DRAWN blade points straight down its line -
            // mid-sweep, not on the cock - so the slash visibly throws it. Watched on the weapon
            // layer itself rather than timed off the motion's keyframes: the entry blend and the
            // two-handed wrist move the real blade off the authored angle by a few frames, and by
            // a different amount for every aim. The motion's own crossing time is the backstop.
            float cross = 0f, prev = float.NaN;
            float latest = Mathf.Max(SlashCrossSeconds(step, swingWindow) * 1.5f, 0.05f);
            while (cross < latest)
            {
                var blade = Rig?.WeaponRenderer;
                if (blade == null) break;
                float off = Vector2.SignedAngle(line, blade.transform.up);
                if (Mathf.Abs(off) < 4f) break;
                if (!float.IsNaN(prev) && Mathf.Sign(off) != Mathf.Sign(prev) && Mathf.Abs(off - prev) < 90f) break;
                prev = off;
                yield return null;
                cross += Time.deltaTime;
            }
            glow?.Fire();
            FireCrescent(step, Slash(step, line), line);

            yield return new WaitForSeconds(Mathf.Max(0f, swing - cross));
            Rig?.SetAimRange(0f);
        }

        /// <summary>Seconds into the slash at which the blade points straight down the aim (its arm
        /// angle, before the aim is added, passing -90).</summary>
        static float SlashCrossSeconds(AttackStep step, float swingWindow)
            => AttackMotions.SwingSeconds(swingWindow)
               * Art.Gear.PrimitiveCharacterRig.CrossingK(step.Motion, -90f, false);

        /// <summary>
        /// The slash's own hit, on the downstroke: the step's strike along the facing for
        /// Tuning.MagnumOpus.SlashShare of its damage. Through ResolveEcho so the once-per-swing
        /// effects (wear, heat, the chain passive) leave the crescent to fire them once. Returns
        /// the bodies it struck - the crescent owes each of them only the rest.
        /// </summary>
        /// <summary>
        /// Where the crescent leaves from: the TIP of the drawn blade (the user's call - the swing
        /// throws it off the point, not the hilt). The crescent is judged on the ground plane and
        /// drawn <see cref="Combat.CrescentBeam.Lift"/> above it, so the point under the picture is
        /// the tip less that lift. With no blade drawn, a sword's length down the line.
        /// </summary>
        Vector2 CrescentOrigin(Vector2 line)
        {
            var blade = Rig?.WeaponRenderer;
            if (blade == null || blade.sprite == null)
                return (Vector2)transform.position + line * BaseRange * 0.8f;
            Vector2 tip = blade.transform.TransformPoint(new Vector3(0f, blade.sprite.bounds.max.y, 0f));
            return tip - new Vector2(0f, Combat.CrescentBeam.Lift);
        }

        HashSet<Health> Slash(AttackStep step, Vector2 line)
        {
            ResolveEcho(step, transform.position, line, Core.Tuning.MagnumOpus.SlashShare);
            var struck = new HashSet<Health>();
            foreach (var (hp, _, _) in _ordered) if (hp != null) struck.Add(hp);
            return struck;
        }

        /// <summary>
        /// The crescent itself: damage rolled ONCE here and carried, as the thrown blade's is, then
        /// scaled per body by the board and the ledger and heard by both on every hit.
        /// </summary>
        void FireCrescent(AttackStep step, HashSet<Health> slashed, Vector2 facing)
        {
            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier;
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();

            // Heavy: it moves what it hits, and Finisher Knockback throws it further.
            float kick = step.Knockback * Art.Gear.StatPercents.Apply(1f, Stats.FinisherKnockback);
            float range = Core.Tuning.MagnumOpus.BeamRange * Mods.RangeMul
                          * Art.Gear.StatPercents.Apply(1f, Stats.Range);
            var element = Resource?.Element ?? ElementType.Fire;

            OnWeaponUsed?.Invoke();   // one swing's wear, not one per body
            bool first = true;
            Combat.CrescentBeam.Fire(gameObject, CrescentOrigin(facing), facing, dmg, kick,
                range, CleaveFalloff(step.ChainFalloff), MinChainFraction, crit, element,
                slashed, 1f - Core.Tuning.MagnumOpus.SlashShare, transform.position,
                onHit: (hp, info) =>
                {
                    Resource?.OnHitLanded(Health, new DamageInfo(info.Amount, element, gameObject));
                    float reach = Vector2.Distance(transform.position, hp.transform.position) / Mathf.Max(0.01f, range);
                    NotifyThrownHit(hp, info, Mathf.Clamp01(reach), first);
                    first = false;
                },
                scaleHit: (hp, d) => ScaleThrownHit(hp, d, true, crit,
                    Mathf.Clamp01(Vector2.Distance(transform.position, hp.transform.position) / Mathf.Max(0.01f, range))));
        }

        /// <summary>
        /// The held pose and the tightening ring every wind-up shares - Skyfall's charge and
        /// Separatio's raised blade. Publishes <see cref="ChargeProgress01"/> while it runs.
        /// </summary>
        IEnumerator WindUp(float seconds, AttackMotion? holdSwing = null)
        {
            // A charge holds the raised blade; a move that names its own swing holds THAT swing's
            // first frame instead (the Prima Materia art's cocked throw), trembling toward the strike.
            if (holdSwing.HasValue) Rig?.HoldSwingStart(holdSwing.Value, false, seconds);
            else
            {
                Rig?.PlayCharge(seconds);
                _visual?.PlayCharge(seconds);
            }

            var tint = ElementInfo.Tint(Resource?.Element ?? ElementType.Fire);
            float elapsed = 0f;
            float cue = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                ChargeProgress01 = Mathf.Clamp01(elapsed / seconds);

                // A ring that tightens as it fills. The wind-up is the only part of this move an
                // enemy - or the player deciding whether to commit - can react to, so it has to be
                // legible from across the arena, not just on the character.
                cue -= Time.deltaTime;
                if (cue <= 0f)
                {
                    float t = Mathf.Clamp01(elapsed / seconds);
                    cue = Mathf.Lerp(0.20f, 0.06f, t);
                    var c = Color.Lerp(tint, Color.white, t * 0.7f);
                    c.a = 0.35f + t * 0.5f;
                    Spr.Pulse(transform, Mathf.Lerp(2.2f, 0.8f, t), c, 0.22f, true, -0.35f);
                }
                yield return null;
            }

            ChargeProgress01 = 0f;
        }

        /// <summary>The four cones' elements, round the sides from the facing - one each.</summary>
        static readonly ElementType[] QuintessenceElements =
            { ElementType.Fire, ElementType.Air, ElementType.Water, ElementType.Earth };

        /// <summary>Seconds from the halves leaving the hands to their return.</summary>
        static float QuintessenceSeconds
            => Core.Tuning.Quintessence.MergeSeconds
               + Core.Tuning.Quintessence.ConeStaggerSeconds * (QuintessenceElements.Length - 1)
               + Core.Tuning.Quintessence.LingerSeconds + Core.Tuning.Quintessence.SplitSeconds;

        /// <summary>
        /// The Armillary's Quintessence. The hands empty (both halves - reference counted, see
        /// SetWeaponVisible), the halves rise and join overhead into the whole armillary
        /// (QuintessenceVisual), and while it holds four REVERSE CONES strike round the character,
        /// one per side starting at the facing, each in its element's colour. Each cone is a real
        /// hit through ResolveEcho for a quarter of the step's damage
        /// (Tuning.Quintessence.ConeDamageFraction), shaped by GatherTargets from
        /// AttackStep.ReverseCones - so the four are exactly the Medium the step declares, and the
        /// once-per-swing effects fire once. Then the whole parts and the hands fill again.
        ///
        /// Locked for the combine and the cones, like any finisher; the player can move again as
        /// it comes apart.
        /// </summary>
        IEnumerator QuintessenceStrike(AttackStep step)
        {
            float coneSpread = Core.Tuning.Quintessence.ConeStaggerSeconds * (QuintessenceElements.Length - 1);
            float hold = coneSpread + Core.Tuning.Quintessence.LingerSeconds;
            _lockTimer = (Core.Tuning.Quintessence.MergeSeconds + coneSpread) * ArtLockMul;

            Rig?.SetWeaponVisible(false);
            Rig?.SetWeaponVisible(false);
            Combat.QuintessenceVisual.Play(transform, Rig, CombinedFrames, CombinedFrameSeconds, hold);

            yield return new WaitForSeconds(Core.Tuning.Quintessence.MergeSeconds);
            SettleStrike();   // all four cones strike with the result

            var facing = Facing;
            float baseAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            float length = (Art.Gear.StatPercents.Apply(BaseRange, Stats.Range) + step.RangeBonus
                            + (Resource?.AttackRangeBonus ?? 0f)) * Mods.RangeMul * Tuning.Disc.RangeMul;
            for (int i = 0; i < QuintessenceElements.Length; i++)
            {
                if (i > 0) yield return new WaitForSeconds(Core.Tuning.Quintessence.ConeStaggerSeconds);
                float a = (baseAngle + i * 90f) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var tint = ElementInfo.Tint(QuintessenceElements[i]);
                tint.a = 0.75f;
                Spr.ReverseConeBlast(transform.position, dir, length,
                                     length * Core.Tuning.Quintessence.ConeBaseWidthFraction * 0.5f, tint, 0.34f);
                ResolveEcho(step, transform.position, dir, Core.Tuning.Quintessence.ConeDamageFraction);
            }

            yield return new WaitForSeconds(Core.Tuning.Quintessence.LingerSeconds + Core.Tuning.Quintessence.SplitSeconds);
            Rig?.SetWeaponVisible(true);
            Rig?.SetWeaponVisible(true);
        }

        /// <summary>What each of Separatio's figures swings, in Sulfur/Salt/Mercury order - the
        /// Ripsaw comes down, the Pacemaker sweeps across, the Reactor darts.</summary>
        static readonly AttackMotion[] SeparatioMotions = { AttackMotion.Chop, AttackMotion.Sweep, AttackMotion.Thrust };

        /// <summary>Seconds from the first figure's strike to the last one's.</summary>
        static float SeparatioSpread
            => Core.Tuning.Separatio.FigureStaggerSeconds * (SeparatioMotions.Length - 1);

        /// <summary>
        /// Tria Prima's Separatio. Hold the blade up; then the player goes pale and see-through,
        /// the blade leaves their hands, and three figures - one per principle, each holding one
        /// of the three swords - strike a third of a turn apart, the first along the facing; then
        /// the player is solid again with the blade whole.
        ///
        /// Each figure's hit goes through ResolveEcho: a real hit from where the figure stands,
        /// for a third of the step's damage (Tuning.Separatio.FigureDamageFraction), so three of
        /// them are exactly the Medium finisher the step declares. The echo path also keeps the
        /// once-per-swing effects (heat, wear, principle marks) from firing three times.
        ///
        /// The facing is read when the hold ENDS, not when it begins - the player keeps moving and
        /// aiming through the hold exactly as through Skyfall's, so it lands where they face.
        /// </summary>
        IEnumerator SeparatioStrike(AttackStep step, float swingWindow)
        {
            yield return WindUp(Core.Tuning.Separatio.HoldSeconds);
            SettleStrike();   // all three figures strike with the result

            float swing = AttackMotions.SwingSeconds(swingWindow);
            _lockTimer = (swing + SeparatioSpread) * ArtLockMul;

            // Come apart: the blade goes (reference counted - see SetWeaponVisible), the body
            // goes pale, and a flash marks the moment of separation.
            Rig?.SetWeaponVisible(false);
            var ghost = Ghost(Core.Tuning.Separatio.SplitTint);
            Spr.Flash(transform.position, 1.0f, Color.white, 0.18f);

            var facing = Facing;
            float baseAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            for (int i = 0; i < SeparatioMotions.Length; i++)
            {
                if (i > 0) yield return new WaitForSeconds(Core.Tuning.Separatio.FigureStaggerSeconds);
                float a = (baseAngle + i * 360f / SeparatioMotions.Length) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var at = (Vector2)transform.position + dir * Core.Tuning.Separatio.FigureOffset;

                var part = step.Clone();
                part.Motion = SeparatioMotions[i];
                part.SplitsThreeWays = false;

                Split?.Throw(i, at, dir, part.Motion, swingWindow);
                ResolveEcho(part, at, dir, Core.Tuning.Separatio.FigureDamageFraction);
            }

            yield return new WaitForSeconds(swing);

            // Whole again.
            RestoreGhost(ghost);
            Rig?.SetWeaponVisible(true);
            Spr.Flash(transform.position, 0.8f, Color.white, 0.2f);
        }

        /// <summary>
        /// Tint every renderer of the player's own figure, returning what each was so it can be
        /// put back exactly - pixel art draws at white, but anything else on the rig keeps
        /// whatever colour it carried rather than being flattened to white on the way back.
        /// </summary>
        List<(SpriteRenderer Renderer, Color Was)> Ghost(Color tint)
        {
            var saved = new List<(SpriteRenderer, Color)>();
            var root = Rig?.Transform;
            if (root == null) return saved;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                saved.Add((sr, sr.color));
                var c = sr.color * tint;
                sr.color = c;
            }
            return saved;
        }

        static void RestoreGhost(List<(SpriteRenderer Renderer, Color Was)> saved)
        {
            foreach (var (sr, was) in saved)
                if (sr != null) sr.color = was;
        }

        /// <summary>
        /// Leave the ground, hang, then come down on your own position.
        ///
        /// The opposite trade to <see cref="ChargedStrike"/>. A charge is a vulnerable wind-up
        /// paid for in tempo; this is total safety paid for AFTERWARDS - two seconds where
        /// nothing can touch you, then two seconds where everything hits twice as hard. The
        /// player keeps full movement throughout, so the airborne window is spent choosing where
        /// to land rather than waiting to be put back down.
        ///
        /// Only the DRAWING leaves the ground. The player's transform stays put, which is what
        /// keeps the camera, the ring and the blast origin all agreeing about where the character
        /// is: the marked circle really is where the damage lands.
        /// </summary>
        IEnumerator LeapStrike(AttackStep step, bool isFinisher, float swingWindow)
        {
            float radius = BaseRange + step.RangeBonus + (Resource?.AttackRangeBonus ?? 0f);
            if (step.AreaOfEffect) radius *= AoeScale;

            // Phantom's own leap is fixed to its purple haze rather than the played element, the
            // same reasoning Shadow's tint already gets elsewhere - a signature weapon has one
            // colour whatever build it is riding on.
            var tint = ActiveMoveset != null && ActiveMoveset.Id == PhantomSignatureId
                ? Core.Tuning.Phantom.Tint
                : ElementInfo.Tint(Resource?.Element ?? ElementType.Fire);

            Airborne = true;
            ImpactRadius = radius;
            ImpactCentre = transform.position;
            if (Health) Health.Immune = true;

            // A DIRECTED leap: carried along the aim (the facing, which the art's aim and the
            // auto-target both set), landing ON a locked target in front when it is within reach.
            _leapCarry = Vector2.zero;
            Transform mark = null;
            if (step.LeapDistance > 0f)
            {
                var dir = Facing.sqrMagnitude > 0.0001f ? Facing.normalized : Vector2.right;
                float distance = step.LeapDistance;
                var target = Target;
                if (target != null && !target.IsDead)
                {
                    Vector2 to = (Vector2)target.transform.position - (Vector2)transform.position;
                    if (to.sqrMagnitude > 0.0001f && to.magnitude <= distance && Vector2.Dot(to.normalized, dir) > 0.5f)
                    {
                        dir = to.normalized;
                        distance = Mathf.Min(distance, to.magnitude);
                    }
                }
                _leapCarry = dir * (distance / step.LeapSeconds);
                mark = new GameObject("leap.mark").transform;
                mark.position = LeapStop(transform.position, dir * distance);
                ImpactCentre = mark.position;
            }

            // Springing off the ground: a burst under the feet, so the launch has a moment of
            // contact rather than the character simply vanishing upward.
            Spr.Flash(transform.position, radius * 0.4f, Color.white, 0.22f);

            const float RiseSeconds = 0.28f;
            float FallSeconds = 0.14f;
            float PeakHeight = 9f;      // clears the top of the camera at any sane zoom

            // A SPINNING leap (Falling Star): takes off with a full turn close to the ground, then
            // launches; the landing's swing starts on the way down, the fall as long as it takes
            // to draw its strike, so the blade plants as the feet touch. Otherwise the blade goes
            // overhead and stays there for the whole flight - the charged slam's held pose.
            bool spin = step.SpinLeap && Rig != null;
            float spinEnd = 0f;
            bool landingSwung = false;
            if (spin)
            {
                FallSeconds = Mathf.Clamp(Rig.StrikeDrawnSeconds(step.Motion, swingWindow), 0.12f, step.LeapSeconds * 0.4f);
                PeakHeight = Core.Tuning.FallingStar.PeakHeight;
                float takeoff = step.LeapSeconds - FallSeconds;
                Rig.SetSwingHop(0f);
                Rig.PlayAttack(AttackMotion.StarLeap, takeoff, alt: false);
                spinEnd = Mathf.Min(Rig.StrikeDrawnSeconds(AttackMotion.StarLeap, takeoff), takeoff * 0.6f);
            }
            else
            {
                Rig?.PlayCharge(step.LeapSeconds);
                _visual?.PlayCharge(step.LeapSeconds);
            }

            float elapsed = 0f;
            float cue = 0f;
            while (elapsed < step.LeapSeconds)
            {
                elapsed += Time.deltaTime;
                float remaining = step.LeapSeconds - elapsed;

                float height;
                if (spin)
                {
                    // Turning up off the ground, then up and away, hang, and an accelerating drop.
                    float launch = Core.Tuning.FallingStar.LaunchSeconds, low = Core.Tuning.FallingStar.SpinHeight;
                    if (remaining < FallSeconds)
                    {
                        float u = 1f - Mathf.Clamp01(remaining / FallSeconds);
                        height = PeakHeight * (1f - u * u);
                    }
                    else if (elapsed < spinEnd)
                        height = low * Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(0.01f, spinEnd));
                    else if (elapsed < spinEnd + launch)
                        height = Mathf.Lerp(low, PeakHeight, Mathf.Sin((elapsed - spinEnd) / launch * Mathf.PI * 0.5f));
                    else
                        height = PeakHeight;

                    if (!landingSwung && remaining <= FallSeconds)
                    {
                        landingSwung = true;
                        _visual?.PlayAttack(step.Motion, swingWindow);
                        Rig.SetSwingHop(0f);
                        Rig.PlayAttack(step.Motion, swingWindow, alt: false);
                        Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
                        SmearSwing(swingWindow, isFinisher: true);
                    }
                }
                else
                {
                    // Fast up, hang, faster down. The fall is deliberately shorter than the rise:
                    // coming back at the same speed you left reads as a lift, not as a drop.
                    height =
                        elapsed < RiseSeconds
                            ? Mathf.Sin(Mathf.Clamp01(elapsed / RiseSeconds) * Mathf.PI * 0.5f) * PeakHeight
                        : remaining < FallSeconds
                            ? Mathf.Clamp01(remaining / FallSeconds) * PeakHeight
                            : PeakHeight;
                }
                Rig?.SetAirborne(height);

                // The landing moves with the body - wherever the carry has yet to take it.
                if (mark != null)
                {
                    ImpactCentre = LeapStop(transform.position, _leapCarry * Mathf.Max(0f, remaining));
                    mark.position = ImpactCentre;
                }
                else ImpactCentre = transform.position;

                // A ring closing in on the landing zone, quickening as the drop nears. Enemies
                // cannot read a character that is off the screen, so the ground has to say it.
                cue -= Time.deltaTime;
                if (cue <= 0f)
                {
                    float t = Mathf.Clamp01(elapsed / step.LeapSeconds);
                    cue = Mathf.Lerp(0.26f, 0.07f, t);
                    var c = Color.Lerp(tint, Color.white, t * 0.6f);
                    c.a = 0.30f + t * 0.45f;
                    Spr.Pulse(mark != null ? mark : transform, radius * Mathf.Lerp(1.15f, 0.5f, t), c, 0.28f, true, -0.5f);
                }
                yield return null;
            }

            Rig?.SetAirborne(0f);
            Airborne = false;
            ImpactRadius = 0f;
            _leapCarry = Vector2.zero;
            if (mark != null) Destroy(mark.gameObject, 0.4f);   // its last pulses fade out on it
            if (Health) Health.Immune = false;
            SettleOverChasm();

            // Landing commits like every other finisher, and the recovery starts the moment the
            // feet touch - not when the animation ends. The exposure IS the animation's meaning.
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            ExposedSeconds = step.ExposedSeconds;
            _exposedMultiplier = Mathf.Max(1f, step.ExposedMultiplier);

            // No hop: the figure has just come down out of a real leap, and lifting it again on
            // the landing frame would fight the impact the whole move is built around. A spinning
            // leap's landing swing is already under way (started on the way down).
            if (!landingSwung)
            {
                _visual?.PlayAttack(step.Motion, swingWindow);
                Rig?.SetSwingHop(0f);
                Rig?.PlayAttack(step.Motion, swingWindow, alt: false);

                // Always a finisher on this path - charge, leap and draw-cut are nothing else.
                Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
                SmearSwing(swingWindow, isFinisher: true);
            }

            Spr.Flash(transform.position, radius * 1.1f, Color.white, 0.28f);
            Spr.Flash(transform.position, radius * 0.55f, tint, 0.34f);
            SettleStrike();   // the landing is the strike
            ResolveArc(step, isFinisher, advanceCombo: false);
        }

        static readonly RaycastHit2D[] _leapHits = new RaycastHit2D[8];

        /// <summary>
        /// Where the body would stop travelling <paramref name="by"/> from <paramref name="from"/>:
        /// the player's own collider swept along it against what it collides with (walls, columns),
        /// enemies left out (they never block the player). So the landing ring never promises a
        /// spot past a wall - the carry itself is stopped there by physics.
        /// </summary>
        Vector2 LeapStop(Vector2 from, Vector2 by)
        {
            float dist = by.magnitude;
            if (dist < 0.001f || _rb == null) return from + by;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            int n = _rb.Cast(by / dist, filter, _leapHits, dist);
            float stop = dist;
            for (int i = 0; i < n; i++)
            {
                var c = _leapHits[i].collider;
                if (c == null || c.GetComponentInParent<Enemies.EnemyController>() != null) continue;
                stop = Mathf.Min(stop, _leapHits[i].distance);
            }
            return from + by / dist * stop;
        }

        /// <summary>Seconds from Riposte's answer starting to its strike: the counter's blade
        /// coming level (its swing's strike phase).</summary>
        float CounterSeconds(AttackStep step, float swingWindow)
        {
            float s = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            return s > 0.05f ? s : Core.Tuning.Riposte.CounterSecondsFallback;
        }

        /// <summary>
        /// Riposte's answer, after the guard (the timing bar) is judged. If the guard turned
        /// anything aside: the COUNTER (AttackMotion.RiposteTurnabout) - the body turns a full
        /// circle and steps in along the aim (a dash, so a chasm at its end takes the player), and
        /// as the blade comes level the step's strike lands from where the body arrived. If not:
        /// the figure turns back (RiposteReturn) and nothing is hit - the art was the guard.
        /// </summary>
        IEnumerator RiposteCounter(AttackStep step, bool isFinisher, float swingWindow)
        {
            bool answer = _guardParried;
            _guardParried = false;
            SettleStrike();   // the guard's end is the judged moment

            var motion = answer ? step.Motion : AttackMotion.RiposteReturn;
            _visual?.PlayAttack(motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(motion, swingWindow, alt: false);
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            _lockFreesAfterStrike = !Mods.RootedWhileSwinging;
            if (!answer) yield break;

            if (isFinisher) Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            SmearSwing(swingWindow, isFinisher);
            Echoes?.Trail(motion, swingWindow, false);

            float seconds = CounterSeconds(step, swingWindow);
            var dir = Facing.sqrMagnitude > 0.0001f ? Facing.normalized : Vector2.right;
            Dash(dir, Core.Tuning.Riposte.CounterStep / seconds, seconds);
            yield return new WaitForSeconds(seconds);

            _arcFacing = dir;
            try { ResolveArc(step, isFinisher, advanceCombo: false); }
            finally { _arcFacing = null; }
        }

        /// <summary>
        /// Phantom's poof: the DRAWING teleports to a random point inside the player's own melee
        /// reach for most of the swing, then teleports back - the real transform, the Rigidbody,
        /// the collider and the reach ring never move at all. "Cosmetic only, player circle still
        /// receives damage normally" is not a caveat here, it is the entire mechanism: nothing in
        /// this coroutine touches Health, GatherTargets or ArcOrigin, which is what ResolveArc
        /// still reads from the real position a moment later.
        /// </summary>
        IEnumerator PhantomPoof(float swingWindow)
        {
            float radius = (Art.Gear.StatPercents.Apply(BaseRange, Stats.Range)
                           + (Resource?.AttackRangeBonus ?? 0f)) * Core.Tuning.Phantom.PoofRadiusFraction;
            var offset = Random.insideUnitCircle * radius;
            var tint = Core.Tuning.Phantom.Tint;

            Spr.Flash(transform.position + (Vector3)offset, Core.Tuning.Phantom.PoofFlashRadius, tint, 0.16f);
            Rig?.SetPhantomOffset(offset);

            float hold = AttackMotions.SwingSeconds(swingWindow) * Core.Tuning.Phantom.PoofDurationFraction;
            yield return new WaitForSeconds(hold);

            Spr.Flash(transform.position, Core.Tuning.Phantom.PoofFlashRadius, tint, 0.16f);
            Rig?.SetPhantomOffset(Vector2.zero);
        }

        const string KatanaSignatureId = "crosscut";

        /// <summary>How long Crosscut waits SHEATHED for its timing bar to close - the bar's
        /// shortfall, set on the press (see the timing bar in OnAttack).</summary>
        float _sheathHold;

        /// <summary>Total wall-clock length of the sheathe -> hold -> twin-cut -> draw sequence,
        /// so the dispatch and the coroutine agree on one number.</summary>
        float SheathDrawLength(float swingWindow)
            => Core.Tuning.Katana.SheathSeconds + _sheathHold
             + Core.Tuning.Katana.CutSeconds * 2f
             + Core.Tuning.Katana.DrawSeconds
             + AttackMotions.SwingSeconds(swingWindow);

        /// <summary>
        /// The katana signature. Three beats, resolved here rather than through the ordinary arc
        /// path the way the leap and the throw are:
        ///
        ///   1. SHEATHE - the REAL blade sprite leaves the fist, turns onto the scabbard's axis
        ///      and slides in behind it (Art.Gear.KatanaSheathe), leaving the tsuka standing out
        ///      of the mouth. The rig's own weapon layer is hidden for the duration. It then WAITS
        ///      there while the timing bar fills (<see cref="_sheathHold"/>); the bar closes as
        ///      the wait ends, so every hit after it - cuts and draw alike - lands at the verdict.
        ///   2. TWIN CUTS - two crescents cross the target while the blade is away. Each is an
        ///      ordinary <see cref="ResolveArc"/> on a small step of its own, so it still wears the
        ///      weapon, feeds fire heat and trips the echo passive - only the numbers are smaller.
        ///   3. DRAW - the blade comes back OUT of the saya along the same axis, then the rig
        ///      takes it back and swings. This step carries <c>SheathDraw</c>, so its DamageInfo
        ///      carries <c>Bisects</c> and a non-boss it finishes is left in two halves (see
        ///      Combat.Bisection, wired from GameBootstrap).
        ///
        /// The sheathe and the wait are MOBILE (the other arts' wind-up rule); from the bar's close
        /// the rest is a committed Heavy: <c>_lockTimer</c> covers the cuts, the draw and the swing,
        /// freezing movement AND facing (FaceAim reads _lockTimer), so the draw lands where the
        /// player was aiming as the bar closed - a drawn cut, not a tracking swing.
        ///
        /// Scaled time throughout (WaitForSeconds), so a screen opening mid-sequence pauses it.
        /// </summary>
        IEnumerator SheathDrawStrike(AttackStep step, bool isFinisher, float swingWindow)
        {
            var tint = Core.Tuning.Katana.Tint;

            // ---- beat 1: sheathe ----
            //
            // The look is captured BEFORE the rig's copy is hidden, exactly as ThrowWeapon does:
            // what slides into the scabbard has to be the sword that was in the hand.
            Sprite wSprite = null; Color wTint = Color.white; Vector2 wSize = Vector2.one;
            Rig?.TryGetWeaponVisual(out wSprite, out wTint, out wSize);
            Rig?.SetWeaponVisible(false);

            var sheathe = Rig == null ? null : Art.Gear.KatanaSheathe.Attach(
                Rig, wSprite, wTint, wSize, Art.Gear.DemoGear.ZanmatoBladeAboveGrip);

            yield return SlideBlade(sheathe, 0f, 1f, Core.Tuning.Katana.SheathSeconds);

            // the guard meeting the mouth
            var hipAt = Rig?.TrinketRenderer != null
                ? (Vector2)Rig.TrinketRenderer.transform.position : (Vector2)transform.position;
            Spr.Flash(hipAt, 0.26f, tint, 0.12f, false);

            // ---- the bar: held sheathed until it closes, then the press is decided ----
            // Mobile and still aiming until here, like every other art's wind-up; the lock
            // starts with the cuts, so the draw goes where the player faces as the bar closes.
            if (_sheathHold > 0f) yield return new WaitForSeconds(_sheathHold);
            SettleStrike();
            _lockTimer = (SheathDrawLength(swingWindow) - Core.Tuning.Katana.SheathSeconds - _sheathHold)
                       * ArtLockMul;

            // ---- beat 2: two crossing cuts, blade still away ----
            var cut = new AttackStep
            {
                Name = "Cut", DamageMultiplier = Core.Tuning.Katana.CutMultiplier,
                Weight = FinisherWeight.Medium, Motion = step.Motion,
                ArcDot = 0.85f, StrikeWidth = 0.6f, Knockback = 2f, RangeBonus = step.RangeBonus,
                // The draw-cut below carries the sequence's hitstop - see AttackStep.SuppressHitstop.
                SuppressHitstop = true,
            };
            for (int i = 0; i < 2; i++)
            {
                var t = Targeting != null ? Targeting.Target : null;
                var at = t != null ? (Vector2)t.transform.position
                                   : (Vector2)transform.position + Facing * (CurrentRange * 0.6f);
                // Cross the two: one diagonal, then its mirror, so they read as an X on the body
                // rather than the same cut twice.
                var dir = (i == 0
                    ? new Vector2(Facing.x - Facing.y, Facing.y + Facing.x)
                    : new Vector2(Facing.x + Facing.y, Facing.y - Facing.x)).normalized;
                Spr.Slice(at, dir, CurrentRange * 0.55f, tint, 0.14f);
                Spr.Flash(at, 0.38f, tint, 0.14f, false);
                ResolveArc(cut, isFinisher: true, advanceCombo: false);
                yield return new WaitForSeconds(Core.Tuning.Katana.CutSeconds);
            }

            // ---- beat 3: the draw. Out of the saya along the same axis, then the rig swings it ----
            yield return SlideBlade(sheathe, 1f, 0f, Core.Tuning.Katana.DrawSeconds);
            sheathe?.Release();

            Rig?.SetWeaponVisible(true);
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt: false);

            // Always a finisher on this path - charge, leap and draw-cut are nothing else.
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            SmearSwing(swingWindow, isFinisher: true);

            var drawAt = (Vector2)transform.position + Facing * (CurrentRange * 0.5f);
            Spr.Slice(drawAt, Facing, CurrentRange, Color.white, 0.24f);
            Spr.Slice(drawAt, Facing, CurrentRange * 0.7f, tint, 0.30f);
            Spr.Flash((Vector2)transform.position + Facing * CurrentRange, CurrentRange * 0.35f, tint, 0.20f);

            // The bar was settled before the cuts, so all three hits land at its verdict.
            ResolveArc(step, isFinisher, advanceCombo: false);
        }

        /// <summary>Total length of Solve et Coagula - sheathe, the bar's hold, the X, the rejoin and
        /// the slam's swing - so the dispatch and the coroutine agree on one number.</summary>
        float SplitDrawLength(float swingWindow)
            => Core.Tuning.SolveCoagula.PartSeconds + Core.Tuning.SolveCoagula.SheatheSeconds + _sheathHold
             + Core.Tuning.SolveCoagula.DrawSeconds + Core.Tuning.SolveCoagula.RejoinSeconds
             + AttackMotions.SwingSeconds(swingWindow);

        /// <summary>
        /// Solve et Coagula, the Gemini Blade's art (Tuning.SolveCoagula). Four beats:
        ///
        ///   1. SOLVE - the sword comes apart in the hand and each half goes home to its hip
        ///      (Art.Gear.GeminiSplit; the Gemini Saya wears one sheath at each), the rig's weapon
        ///      hidden. Then it WAITS there while the timing bar fills (<see cref="_sheathHold"/>),
        ///      mobile and aiming - Crosscut's hold. The bar closes as the wait ends.
        ///   2. THE X - both halves cross-drawn at once into an X in front of the body, the lunge
        ///      riding it (Impale's: short of a locked target, never off the floor). The X's share
        ///      lands where the lunge ends.
        ///   3. COAGULA - the halves rise and close onto the rig's own sword, held at the slam's
        ///      first frame (HoldSwingStart), so the two become the one exactly where it is drawn.
        ///   4. THE SLAM - the whole sword's Chop, the rest of the damage and the sequence's one
        ///      hit-stop.
        ///
        /// From the bar's close it is committed (<c>_lockTimer</c>), so the X and the slam go where
        /// the player aimed as it closed. Scaled time throughout.
        /// </summary>
        IEnumerator SplitDrawStrike(AttackStep step, bool isFinisher, float swingWindow)
        {
            var tint = Core.Tuning.SolveCoagula.Tint;

            // ---- beat 1: solve ----
            Vector2 wSize = Vector2.one;
            Rig?.TryGetWeaponVisual(out _, out _, out wSize);
            Rig?.SetWeaponVisible(false);
            var split = Rig == null ? null : Art.Gear.GeminiSplit.Attach(
                Rig, GeminiHalves, wSize, Art.Gear.DemoGear.GeminiSayaHang, Art.Gear.DemoGear.GeminiSayaAxis);

            // the crack down the seam, then the halves pulled apart
            if (Rig?.WeaponAnchor != null)
                Spr.Flash(Rig.WeaponAnchor.position, 0.30f, Color.white, 0.14f, false);
            float part = Core.Tuning.SolveCoagula.PartSeconds;
            for (float t = 0f; t < part; t += Time.deltaTime)
            {
                split?.SetPhase(Art.Gear.GeminiSplit.Phase.Part, t / part);
                if (split != null) Rig?.SetTwinHandTargets(split.NearGrip, split.FarGrip);
                yield return null;
            }

            float sheathe = Core.Tuning.SolveCoagula.SheatheSeconds;
            for (float t = 0f; t < sheathe; t += Time.deltaTime)
            {
                split?.SetPhase(Art.Gear.GeminiSplit.Phase.Sheathe, t / sheathe);
                if (split != null) Rig?.SetTwinHandTargets(split.NearGrip, split.FarGrip);
                yield return null;
            }
            split?.SetPhase(Art.Gear.GeminiSplit.Phase.Home, 0f);

            // both hilts meeting their mouths
            if (Rig?.TrinketRenderer != null)
                Spr.Flash(Rig.TrinketRenderer.transform.position, 0.22f, tint, 0.12f, false);

            // ---- the bar: held sheathed until it closes, then the press is decided ----
            for (float t = 0f; t < _sheathHold; t += Time.deltaTime)
            {
                if (split != null) Rig?.SetTwinHandTargets(split.NearGrip, split.FarGrip);
                yield return null;
            }
            SettleStrike();
            _lockTimer = (Core.Tuning.SolveCoagula.DrawSeconds + Core.Tuning.SolveCoagula.RejoinSeconds
                          + AttackMotions.SwingSeconds(swingWindow)) * ArtLockMul;

            // ---- beat 2: the X, and the lunge it launches ----
            //
            // The X cuts as the blades leave the sheaths and THROWS the player through: the whole
            // lunge, past whatever is in the way (no stop short of the target - the point is to
            // come out the far side), stopped only by a wall or a chasm's edge. So the cut reaches
            // the whole path at once, from where it starts.
            float draw = Core.Tuning.SolveCoagula.DrawSeconds;
            var dir = Facing.sqrMagnitude > 0.0001f ? Facing.normalized : Vector2.right;
            float distance = LungeReach(dir, Core.Tuning.SolveCoagula.LungeDistance, stopAtTarget: false);

            var cut = step.Clone();
            cut.DamageMultiplier = step.DamageMultiplier * Core.Tuning.SolveCoagula.CutShare;
            cut.RangeBonus += distance;
            cut.SplitDraw = false;
            cut.SuppressHitstop = true;   // the slam carries the sequence's freeze
            ResolveArc(cut, isFinisher, advanceCombo: false);

            var at = (Vector2)transform.position + dir * (distance * 0.5f + CurrentRange * 0.3f);
            float span = distance * 0.5f + CurrentRange * 0.6f;
            Spr.Slice(at, new Vector2(dir.x - dir.y, dir.y + dir.x).normalized, span, tint, 0.18f);
            Spr.Slice(at, new Vector2(dir.x + dir.y, dir.y - dir.x).normalized, span, tint, 0.18f);
            Spr.Flash(at, 0.34f, tint, 0.14f, false);

            if (distance > 0.05f) Dash(dir, distance / draw, draw);
            for (float t = 0f; t < draw; t += Time.deltaTime)
            {
                split?.SetPhase(Art.Gear.GeminiSplit.Phase.Draw, t / draw);
                if (split != null) Rig?.SetTwinHandTargets(split.NearGrip, split.FarGrip);
                yield return null;
            }
            split?.SetPhase(Art.Gear.GeminiSplit.Phase.Draw, 1f);

            // Carried past the locked target: the slam turns back onto it.
            var locked = Target;
            if (locked != null && !locked.IsDead)
            {
                var back = (Vector2)locked.transform.position - (Vector2)transform.position;
                if (Vector2.Dot(back, dir) < 0f && back.sqrMagnitude > 0.0001f) Facing = back.normalized;
            }

            // ---- beat 3: coagula - the halves close onto the sword at the slam's first frame ----
            float rejoin = Core.Tuning.SolveCoagula.RejoinSeconds;
            Rig?.HoldSwingStart(step.Motion, false, rejoin);
            for (float t = 0f; t < rejoin; t += Time.deltaTime)
            {
                split?.SetJoin(Rig?.WeaponAnchor);
                split?.SetPhase(Art.Gear.GeminiSplit.Phase.Rejoin, t / rejoin);
                if (split != null) Rig?.SetTwinHandTargets(split.NearGrip, split.FarGrip);
                yield return null;
            }
            Rig?.SetTwinHandTargets(null, null);
            // the clap: the two halves meeting as one
            if (Rig?.WeaponAnchor != null)
                Spr.Flash(Rig.WeaponAnchor.position, 0.36f, Color.white, 0.14f, false);
            split?.Release();
            Rig?.SetWeaponVisible(true);

            // ---- beat 4: the slam ----
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt: false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            SmearSwing(swingWindow, isFinisher: true);
            Spr.Flash((Vector2)transform.position + Facing * (CurrentRange * 0.7f), CurrentRange * 0.3f, tint, 0.18f);

            var slam = step.Clone();
            slam.DamageMultiplier = step.DamageMultiplier * Core.Tuning.SolveCoagula.SlamShare;
            ResolveArc(slam, isFinisher, advanceCombo: false);
        }

        /// <summary>
        /// Drive the sheathing blade from one progress value to another over a real span of
        /// SCALED time, so a screen opening mid-sequence pauses the slide with everything else.
        /// Degrades to a plain wait when there is no rig to animate, which keeps the beat lengths
        /// (and so _lockTimer and _cooldown) honest either way.
        /// </summary>
        IEnumerator SlideBlade(Art.Gear.KatanaSheathe blade, float from, float to, float seconds)
        {
            if (blade == null) { yield return new WaitForSeconds(seconds); yield break; }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                blade.SetProgress(Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                yield return null;
            }
            blade.SetProgress(to);
        }

        /// <summary>
        /// Return part of a hit as health, if the mastery grid granted any lifesteal.
        ///
        /// Silent when there is nothing to steal - a heal cue on every swing of an un-invested
        /// build would be pure noise, and the flash is what tells an invested player it is
        /// working.
        /// </summary>
        void Drain(float dealt)
        {
            if (LifestealFraction == null || Health == null || Health.IsDead) return;

            // Every lifesteal source is one pool under one cap (Art.Gear.StatCurves.Lifesteal) -
            // the board, the ledger and the element (Air's Gust) alike.
            float frac = Art.Gear.StatCurves.Lifesteal(LifestealFraction() + (Resource?.LifestealBonus ?? 0f));
            if (frac <= 0f || dealt <= 0f) return;

            // Nothing to gain at full health, and no cue for it either: a flash that fires on
            // every hit while topped up would train the player to ignore it. The board's Cibation
            // banks it as a shield instead.
            if (Health.Current >= Health.Max - 0.01f)
            {
                Board?.AddShield(dealt * frac);
                return;
            }

            // And it heals no faster than a set share of max health a second - a budget that
            // refills over one second and is spent by every hit. Uncapped, deep floors' damage
            // healed most of a health bar a second, and a fight became a one-shot or nothing.
            float perSecond = Art.Gear.StatCurves.LifestealPerSecond(Health.Max);
            float heal = dealt * frac;
            if (!float.IsPositiveInfinity(perSecond))
            {
                _lifestealBudget = Mathf.Min(perSecond, _lifestealBudget + perSecond * (Time.time - _lifestealAt));
                _lifestealAt = Time.time;
                float allowed = Mathf.Min(heal, _lifestealBudget);
                Board?.AddShield(heal - allowed);   // Cibation: what the limit turned away
                heal = allowed;
                _lifestealBudget -= heal;
            }
            if (heal <= 0f) return;

            Health.Heal(heal);
            Spr.Flash(transform.position, 0.6f, new Color(0.55f, 1f, 0.6f), 0.16f);
        }

        /// <summary>Lifesteal still allowed this second, and when it was last topped up.</summary>
        float _lifestealBudget, _lifestealAt;

        /// <summary>Floor on the chain taper, so a wide cleave never decays into nothing.</summary>
        const float MinChainFraction = Tuning.Attack.CleaveFalloffFloor;

        /// <summary>Reusable hit buffers - these run on every swing, several times a second.</summary>
        /// <summary>
        /// Shared overlap results. A List rather than an array because the NonAlloc array calls
        /// are deprecated in Unity 6 - the List overloads grow it once and then reuse it, so this
        /// is still allocation-free after the first swing.
        /// </summary>
        static readonly List<Collider2D> _hitBuffer = new(32);

        /// <summary>
        /// Everything, including triggers - the old array calls filtered nothing either, and the
        /// loop below already rejects anything without a live Health.
        ///
        /// Built by hand rather than with ContactFilter2D.NoFilter(), which is itself deprecated.
        /// A default filter has every use* flag off, so the only difference NoFilter made was
        /// turning triggers on.
        /// </summary>
        static readonly ContactFilter2D _overlapAll = new() { useTriggers = true };

        /// <summary>
        /// hazardMul: 1 normally, 0 for a target only reachable through a Blue force field, 2
        /// through a Red one. A column excludes a candidate outright (never added at all); a
        /// force field never does - the hit still lands, on a target that is still validly
        /// targeted, it is just worth nothing or worth double.
        /// </summary>
        readonly List<(Health hp, float dist, float hazardMul)> _ordered = new();

        /// <summary>
        /// Everything this step can reach, nearest first.
        ///
        /// The ordering is load-bearing twice over. It decides which enemy takes the undiminished
        /// hit, and it decides which one a single-target basic connects with at all - that used to
        /// be whichever collider the physics query happened to return first, so a basic swing with
        /// two enemies in range picked one arbitrarily rather than the one in front of the blade.
        /// </summary>
        /// <summary>
        /// A radius from the player that comfortably covers the visible arena.
        ///
        /// The undertow finisher reaches this far - it gathers what is on screen rather than what
        /// a swing could touch - so it is measured off the camera, not off any weapon stat.
        /// </summary>
        float ScreenReach()
        {
            var cam = _cam != null ? _cam : Camera.main;
            if (cam != null && cam.orthographic)
            {
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                // Corner distance, plus a unit of slack so an enemy sitting right on the edge is
                // still caught rather than left behind the moment the gather fires.
                return Mathf.Sqrt(halfW * halfW + halfH * halfH) + 1f;
            }
            return 14f;
        }

        /// <summary>
        /// Where the swing being resolved is coming FROM, and which way it points.
        ///
        /// Normally the player, which is what every swing in the game has ever been. Separatio's
        /// figures and the Armillary's cones resolve from where the figure is, so the two
        /// have to be substitutable - and doing it with an override rather than by threading an
        /// origin through GatherTargets, ResolveArc and every effect below them keeps one tuned
        /// code path instead of a second one that can drift from it.
        ///
        /// Set and cleared around a single synchronous resolve in <see cref="ResolveEcho"/>;
        /// nothing here yields, so they can never be left set.
        /// </summary>
        Vector2? _arcOrigin, _arcFacing;

        Vector2 ArcOrigin => _arcOrigin ?? (Vector2)transform.position;
        Vector2 ArcFacing => _arcFacing ?? Facing;

        void GatherTargets(AttackStep step, float range)
        {
            _ordered.Clear();
            var origin = ArcOrigin;

            // The undertow is the one finisher that reaches past its own arc: it drags in the
            // whole visible fight. Queried with the allocating call on purpose - it fires once on
            // a finisher, not every frame, and the crowd can outnumber the shared non-alloc buffer.
            if (step.PullsIn)
            {
                foreach (var col in Physics2D.OverlapCircleAll(origin, ScreenReach()))
                {
                    if (col == null || col.gameObject == gameObject) continue;
                    var hp = col.GetComponent<Health>();
                    if (hp == null || hp.IsDead) continue;
                    // A column blocks an attack reaching across it, same as every other gather
                    // below - "can't attack through it" is a blanket rule, not one scoped to
                    // ranged/cone attacks specifically. A force field never excludes the target -
                    // see HazardDamageMul.
                    var sight = HazardQuery.Query(origin, col.transform.position, gameObject, col.gameObject);
                    if (sight == SightResult.Blocked) continue;
                    _ordered.Add((hp, Vector2.Distance(col.transform.position, origin), HazardDamageMul(sight)));
                }
                _ordered.Sort((a, b) => a.dist.CompareTo(b.dist));
                return;
            }

            int count;
            if (step.ReverseCones)
            {
                // Quintessence's cone: WIDEST at the character, narrowing to a point at the reach.
                // Gathered as the circle of that reach, then cut to the triangle - an enemy counts
                // while its offset along the facing is within the reach and its offset across is
                // inside the width left at that distance.
                count = Physics2D.OverlapCircle(origin, range, _overlapAll, _hitBuffer);
                float baseHalf = range * Core.Tuning.Quintessence.ConeBaseWidthFraction * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    var col = _hitBuffer[i];
                    if (col == null) continue;
                    var d = (Vector2)col.transform.position - origin;
                    float along = Vector2.Dot(d, ArcFacing);
                    float across = Mathf.Abs(ArcFacing.x * d.y - ArcFacing.y * d.x);
                    if (along < -0.15f || along > range || across > baseHalf * (1f - Mathf.Max(0f, along) / range) + 0.15f)
                        _hitBuffer[i] = null;
                }
            }
            else if (step.AreaOfEffect)
            {
                count = Physics2D.OverlapCircle(origin, range, _overlapAll, _hitBuffer);
            }
            else
            {
                // A capsule laid along the facing IS the swing: it starts at the character and
                // ends at the blade's reach, so nothing behind or beside the swing is touched and
                // there is no cone test to approximate it with.
                float width = Mathf.Max(0.1f, step.StrikeWidth * Mods.StrikeWidthMul);
                var centre = origin + ArcFacing * (range * 0.5f);
                // Length is the reach itself, NOT reach + width: size.x is the capsule's total
                // extent including its end caps, so adding the width would hang half a blade's
                // worth of hitbox out behind the character and quietly re-create the thing this
                // replaced - a strike that lands on what is standing at your back.
                var size = new Vector2(Mathf.Max(range, width), width);
                float angle = Mathf.Atan2(ArcFacing.y, ArcFacing.x) * Mathf.Rad2Deg;
                count = Physics2D.OverlapCapsule(centre, size, CapsuleDirection2D.Horizontal,
                                                 angle, _overlapAll, _hitBuffer);
            }

            for (int i = 0; i < count; i++)
            {
                var col = _hitBuffer[i];
                if (col == null || col.gameObject == gameObject) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (_arcOnly != null && !_arcOnly.Contains(hp)) continue;   // Carousel's swept bodies
                var sight = HazardQuery.Query(origin, col.transform.position, gameObject, col.gameObject);
                if (sight == SightResult.Blocked) continue;
                _ordered.Add((hp, Vector2.Distance(col.transform.position, origin), HazardDamageMul(sight)));
            }

            _ordered.Sort((a, b) => a.dist.CompareTo(b.dist));
        }

        /// <summary>Carousel's turn resolves its circle in parts, a body as a disc reaches it: the
        /// bodies this part may strike (null - any), and whether a part has already resolved (the
        /// swing's once-only effects - wear, the board's area tick, splash - have fired).</summary>
        HashSet<Health> _arcOnly;
        bool _arcRepeat;

        /// <summary>Nulled -> 0, Amplified -> double, Clear -> unchanged. Blocked never reaches
        /// this - a column excludes the candidate before HazardDamageMul is ever asked.</summary>
        static float HazardDamageMul(SightResult sight) => sight switch
        {
            SightResult.Nulled => 0f,
            SightResult.Amplified => 2f,
            _ => 1f,
        };

        /// <summary>Resolve a swing against everything it reaches.</summary>
        /// <summary>
        /// Send a disc out. Damage is settled by the projectile as it travels, not here.
        /// </summary>
        void ThrowDisc(AttackStep step, float swingWindow)
        {
            // Fumbler still applies: the throw happens, it just finds nothing. Asked once, as the
            // attack was committed (Update), so a throw never pays Blood Price twice.
            bool whiffed = _swingWhiffed;

            // The dance (the ranged pairs): the throw's own motion and hop, and the disc leaves
            // at its strike - the hand it was drawn in, the main hand's first, the off hand's
            // second. Charge's run really carries the player first (a dash).
            var motion = step.ThrowMotion;
            Rig?.SetSwingHop(step.ThrowHop);
            Rig?.PlayAttack(motion, swingWindow, false);
            float release = Rig != null
                ? Mathf.Max(0f, Rig.StrikeDrawnSeconds(motion, swingWindow) - step.ThrowReleaseEarly)
                : 0f;
            if (step.ThrowRun > 0f && release > 0.05f)
            {
                var dir = Facing.sqrMagnitude > 0.0001f ? Facing.normalized : Vector2.right;
                Dash(dir, step.ThrowRun / release, release);
            }
            if (whiffed) return;
            StartCoroutine(ReleaseDisc(step.ThrowOffhand, release));
        }

        IEnumerator ReleaseDisc(bool offhand, float after)
        {
            if (after > 0f) yield return new WaitForSeconds(after);
            if (offhand) Rig?.ThrowFromOffhandNext();
            BasicDiscThrow();
        }

        /// <summary>
        /// A basic while Revolve's ring turns: the swing plays unarmed - the melee one in reach,
        /// the throw out of it - and at its strike the disc in the ring nearest the facing is
        /// knocked out as an ordinary basic throw.
        /// </summary>
        void KnockFromRing(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            bool reach = !ThrowsInsteadOfSwinging;
            var motion = reach ? step.Motion : step.ThrowMotion;
            Rig?.SetSwingHop(reach ? step.HopHeight : step.ThrowHop);
            Rig?.PlayAttack(motion, swingWindow, false);
            float at = Rig != null ? Rig.StrikeDrawnSeconds(motion, swingWindow) : 0f;
            if (!whiffed) StartCoroutine(KnockAt(at));
        }

        IEnumerator KnockAt(float after)
        {
            if (after > 0f) yield return new WaitForSeconds(after);
            var ring = Combat.DiscOrbit.Current;
            if (ring == null) yield break;
            ring.Knock(Facing, BasicDiscThrow);
        }

        /// <summary>
        /// Revolve: the cast, then both discs into the ring at its strike. Medium; the ring's
        /// hits are its damage - each enemy a disc runs into takes TouchBasics of a basic.
        /// </summary>
        IEnumerator RevolveStrike(AttackStep step, bool isFinisher, float swingWindow, bool alt)
        {
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt);
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            SettleStrike();   // the cast is the strike
            float release = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;

            float dmg = (BaseDamage + Mods.BonusDamage) * Tuning.Revolve.TouchBasics;
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();
            var element = Resource?.Element ?? ElementType.Fire;

            if (release > 0f) yield return new WaitForSeconds(release);
            bool first = true;
            Combat.DiscOrbit.Begin(this, Facing, hp =>
            {
                float d = ScaleThrownHit(hp, dmg, true, crit);
                var info = new DamageInfo(d, element, gameObject) { Thrown = true, IsFinisher = true, Crit = crit };
                hp.Take(info);
                OnWeaponUsed?.Invoke();
                Resource?.OnHitLanded(hp, info);
                NotifyThrownHit(hp, info, 0f, first);
                first = false;
            });
        }

        /// <summary>One ordinary basic disc throw along the facing (the hand is the rig's next).</summary>
        ThrownDisc BasicDiscThrow()
        {
            float dmg = (BaseDamage + Mods.BonusDamage)
                        * Tuning.Disc.ThrowDamagePerThrow
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);

            // Rolled once per throw, like ThrowWeapon: every body the disc passes through takes
            // the same hit, rather than a lottery of independent rolls down one ricochet chain.
            dmg *= DamageRoll();
            dmg *= RollCrit(out bool crit);

            int ricochets = Tuning.Disc.BaseRicochets + (BonusRicochets?.Invoke() ?? 0) + Mods.ExtraRicochets;

            var disc = ThrownDisc.Throw(gameObject, Rig, Facing, dmg,
                             Resource?.Element ?? ElementType.Fire,
                             ricochets, ThrowReach,
                             CurrentRange * Tuning.Disc.BounceRangeMul,
                             nearFraction: Tuning.Disc.ThrowDamageNear,
                             isFinisher: false);   // the basic throw - see DamageInfo.IsFinisher
            if (disc != null)
            {
                disc.Crit = crit;
                disc.Falloff = CleaveFalloff(disc.Falloff);   // Boomerang adds run Cleave
                disc.SplashFraction = SplashFraction;
                Effects?.ArmDisc(disc);   // Lodestar, Snapback
            }
            return disc;
        }

        /// <summary>
        /// A disc finisher: several discs at once.
        ///
        /// Spread fans them evenly around the character; otherwise they all go out along the
        /// facing and each finds its own nearest target, because the projectile already excludes
        /// what previous discs have hit.
        /// </summary>
        void ThrowVolley(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            Rig?.PlayAttack(AttackMotion.Throw, AttackMotions.SwingSeconds(swingWindow), false);
            if (whiffed) return;

            float dmg = (BaseDamage + Mods.BonusDamage)
                        * step.DamageMultiplier
                       
                        * Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);

            int ricochets = Tuning.Disc.BaseRicochets + step.DiscRicochetBonus
                            + (BonusRicochets?.Invoke() ?? 0) + Mods.ExtraRicochets;

            // The recoil fires with the throw, not after it: the discs and the shove are one
            // motion, and a hop that arrived a beat later would read as two moves.
            if (step.RecoilDistance > 0f)
            {
                const float RecoilSeconds = 0.18f;
                Dash(-Facing, step.RecoilDistance / RecoilSeconds, RecoilSeconds);
            }

            float baseAngle = Mathf.Atan2(Facing.y, Facing.x) * Mathf.Rad2Deg;

            for (int i = 0; i < step.DiscThrows; i++)
            {
                var aim = Facing;
                if (step.DiscSpreadDegrees > 0f && step.DiscThrows > 1)
                {
                    // A full ring divides evenly and wraps; anything narrower is a fan centred on
                    // the facing, so the first and last disc sit on its edges rather than one of
                    // them going straight up the middle.
                    float spread = step.DiscSpreadDegrees;
                    float a = spread >= 359.9f
                        ? baseAngle + i / (float)step.DiscThrows * 360f
                        : baseAngle - spread * 0.5f + spread * (i / (float)(step.DiscThrows - 1));
                    aim = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                }

                var disc = ThrownDisc.Throw(gameObject, Rig, aim, dmg,
                                 Resource?.Element ?? ElementType.Fire,
                                 ricochets, ThrowReach,
                                 CurrentRange * Tuning.Disc.BounceRangeMul,
                                 step.DiscFarFraction, isFinisher: true);   // ThrowVolley is finisher-only
                if (disc != null && step.DiscThrows > 1) disc.ShowCombined();   // a volley: both out at once
                if (disc != null)
                {
                    disc.Crit = crit;
                    disc.Falloff = CleaveFalloff(disc.Falloff);   // Boomerang adds run Cleave
                    disc.SplashFraction = SplashFraction;
                }
                if (disc != null) disc.Knockback = HeavyKnockback(step, true);
                if (disc != null && step.DiscMarkMultiplier > 1f)
                {
                    disc.MarkMultiplier = step.DiscMarkMultiplier;
                    disc.MarkSeconds = step.DiscMarkSeconds;
                }
            }
        }

        /// <summary>
        /// Ripple (AttackStep.DiscHoming; the user's calls, 2026-10-15): the two-handed throw plays
        /// and at its drawn strike the discs leave - alternately from the main and the off hand,
        /// each hand's own picture - fanned over DiscSpreadDegrees round the facing, then each
        /// HOMES (ThrownDisc.Home) onto its own target: the locked enemy and the nearest others in
        /// reach, ordered round the fan so the discs do not cross, shared out in turn. With one
        /// enemy every disc curves onto it; with none they fly straight out and home. Damage rolled
        /// once at the release, as every volley.
        /// </summary>
        IEnumerator HomingVolley(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            var facing = Facing;
            float wait = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            if (wait > 0f) yield return new WaitForSeconds(wait);
            SettleStrike();   // the release is the strike
            if (whiffed) yield break;

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            int ricochets = Tuning.Disc.BaseRicochets + step.DiscRicochetBonus
                            + (BonusRicochets?.Invoke() ?? 0) + Mods.ExtraRicochets;

            int n = Mathf.Max(1, step.DiscThrows);
            var targets = HomingTargets(facing, ThrowReach, n);
            float baseAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            float spread = n > 1 ? step.DiscSpreadDegrees : 0f;

            // Where each hand is now - the discs leave from them.
            Vector2 main = transform.position, off = transform.position;
            if (Rig != null && Rig.Transform != null && Rig.TryGetSwordHand(out var local))
                main = off = Rig.Transform.TransformPoint(local);
            var offR = Rig?.OffhandRenderer;
            if (offR != null && offR.enabled) off = offR.transform.position;

            for (int i = 0; i < n; i++)
            {
                // Clockwise to anticlockwise round the fan; the targets are ordered the same way.
                float a = (baseAngle - spread * 0.5f + (n > 1 ? spread * i / (n - 1) : 0f)) * Mathf.Deg2Rad;
                var heading = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool fromOff = i % 2 == 1;
                var disc = ThrownDisc.Throw(gameObject, Rig, heading, dmg,
                                            Resource?.Element ?? ElementType.Fire,
                                            ricochets, ThrowReach,
                                            CurrentRange * Tuning.Disc.BounceRangeMul,
                                            step.DiscFarFraction, isFinisher: true,
                                            discIndex: i);
                if (disc == null) continue;
                disc.ShowCombined();   // both discs out at once: the combined picture
                disc.transform.position = fromOff ? off : main;
                disc.Home(targets.Count > 0 ? targets[i * targets.Count / n] : null, heading);
                disc.Crit = crit;
                disc.Falloff = CleaveFalloff(disc.Falloff);
                disc.SplashFraction = SplashFraction;
                disc.Knockback = HeavyKnockback(step, true);
            }
        }

        /// <summary>
        /// Carom (AttackStep.DiscCaroms; the user's calls, 2026-10-15): the hurl plays; as the
        /// hands meet over the head the pair becomes ONE disc (the split joined), and at the drawn
        /// strike it is hurled two-handed - one disc - at the locked enemy in
        /// reach (else the nearest in front), then caroms (ThrownDisc.Carom). Rolled once at the
        /// hurl. The pair splits again when it is home.
        /// </summary>
        /// <summary>
        /// Kickback (AttackStep.DiscSpray; the user's calls, 2026-10-15): the fling plays, and at
        /// its drawn strike the discs leave both hands - alternately - each STRAIGHT along its own
        /// line of the cone (the first body it meets takes it; point blank hits hardest -
        /// DiscFarFraction), and the recoil hop goes in the same instant, so the shove and the
        /// throw read as one. The hop is a dash (a chasm at its end takes the player).
        /// </summary>
        IEnumerator KickbackStrike(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            var facing = Facing;
            float drawn = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            if (drawn > 0f) yield return new WaitForSeconds(drawn);
            SettleStrike();   // the fling is the strike
            if (whiffed) yield break;

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);

            // Where each hand is now - the discs leave from them.
            Vector2 main = transform.position, off = transform.position;
            if (Rig != null && Rig.Transform != null && Rig.TryGetSwordHand(out var local))
                main = off = Rig.Transform.TransformPoint(local);
            var offR = Rig?.OffhandRenderer;
            if (offR != null && offR.enabled) off = offR.transform.position;

            int n = Mathf.Max(1, step.DiscThrows);
            float baseAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            float spread = n > 1 ? step.DiscSpreadDegrees : 0f;
            for (int i = 0; i < n; i++)
            {
                float a = (baseAngle - spread * 0.5f + (n > 1 ? spread * i / (n - 1) : 0f)) * Mathf.Deg2Rad;
                var heading = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool fromOff = i % 2 == 1;
                var disc = ThrownDisc.Throw(gameObject, Rig, heading, dmg,
                                            Resource?.Element ?? ElementType.Fire,
                                            0, ThrowReach, CurrentRange * Tuning.Disc.BounceRangeMul,
                                            step.DiscFarFraction, isFinisher: true,
                                            discIndex: i);
                if (disc == null) continue;
                disc.ShowCombined();   // both discs out at once: the combined picture
                // The fan is laid out on the ground from the player; each disc leaves its hand
                // aimed at where its line ends, or the hand's height shifts the whole fan up.
                var from = fromOff ? off : main;
                disc.transform.position = from;
                disc.Straight((Vector2)transform.position + heading * ThrowReach - from);
                disc.Crit = crit;
                disc.SplashFraction = SplashFraction;
                disc.Knockback = HeavyKnockback(step, true);
            }

            // The recoil, in the same instant as the discs.
            if (step.RecoilDistance > 0f)
            {
                const float RecoilSeconds = 0.18f;
                Dash(-facing, step.RecoilDistance / RecoilSeconds, RecoilSeconds);
            }
        }

        /// <summary>Scattershot's release: <see cref="Core.Tuning.Scattershot.ReleaseAt"/> of the
        /// way through the burst's strike - the arms open, the body near the top of the jump.</summary>
        float ScatterReleaseSeconds(AttackStep step, float swingWindow)
        {
            if (Rig == null) return 0f;
            float start = Rig.StrikeStartSeconds(step.Motion, swingWindow);
            float drawn = Rig.StrikeDrawnSeconds(step.Motion, swingWindow);
            return Mathf.Lerp(start, drawn, Core.Tuning.Scattershot.ReleaseAt);
        }

        /// <summary>
        /// Scattershot (AttackStep.DiscScatter; the user's calls, 2026-10-16): out of HALO a jump
        /// (the swing hop, drawing only), and at its top both arms fling the discs out in an even
        /// RING, alternately from each hand, one spoke on the facing (the lock). Each flies
        /// straight down its spoke (ThrownDisc.Spoke) - laid out on the ground from the player,
        /// aimed from its hand at the spoke's end, Kickback's rule - strikes the first body it
        /// meets and bounces once to the enemy the volley has reached least; one that meets
        /// nothing turns back in on the enemy nearest the player. Damage rolled once at the release.
        /// </summary>
        IEnumerator ScattershotStrike(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(Core.Tuning.Scattershot.HopHeight);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            var facing = Facing;
            float release = ScatterReleaseSeconds(step, swingWindow);
            if (release > 0f) yield return new WaitForSeconds(release);
            SettleStrike();   // the burst is the strike
            if (whiffed) yield break;

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);

            Vector2 main = transform.position, off = transform.position;
            if (Rig != null && Rig.Transform != null && Rig.TryGetSwordHand(out var local))
                main = off = Rig.Transform.TransformPoint(local);
            var offR = Rig?.OffhandRenderer;
            if (offR != null && offR.enabled) off = offR.transform.position;

            int n = Mathf.Max(1, step.DiscThrows);
            float baseAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
            var volley = new Dictionary<Health, int>();
            for (int i = 0; i < n; i++)
            {
                float a = (baseAngle + 360f * i / n) * Mathf.Deg2Rad;
                var heading = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bool fromOff = i % 2 == 1;
                var disc = ThrownDisc.Throw(gameObject, Rig, heading, dmg,
                                            Resource?.Element ?? ElementType.Fire,
                                            0, ThrowReach, CurrentRange * Tuning.Disc.BounceRangeMul,
                                            step.DiscFarFraction, isFinisher: true,
                                            discIndex: i);
                if (disc == null) continue;
                disc.ShowCombined();   // both discs out at once: the combined picture
                var from = fromOff ? off : main;
                disc.transform.position = from;
                disc.Spoke((Vector2)transform.position + heading * ThrowReach - from, volley);
                disc.Crit = crit;
                disc.Falloff = CleaveFalloff(disc.Falloff);
                disc.SplashFraction = SplashFraction;
                disc.Knockback = HeavyKnockback(step, true);
            }
        }

        /// <summary>
        /// Carousel (AttackStep.DiscCarousel; the user's calls, 2026-10-15): the arms held wide and
        /// two full turns (Tuning.Carousel.Revolutions), the player free to move throughout - each
        /// body struck once a turn for the turn's share of the step. The timing bar judges the moment the
        /// turn begins; from then each body in the circle is struck when one of the two discs -
        /// half a turn apart, the off disc leading along the facing - sweeps past it, through
        /// ResolveArc (filtered to the bodies just reached), so every rule a swing has applies and
        /// the swing's once-only effects fire on its first part alone.
        /// </summary>
        IEnumerator CarouselStrike(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            _lockTimer = Mods.RootedWhileSwinging ? AttackMotions.SwingSeconds(swingWindow) : 0f;   // free to move

            float start = Rig != null ? Rig.StrikeStartSeconds(step.Motion, swingWindow) : 0f;
            float end = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            if (start > 0f) yield return new WaitForSeconds(start);
            SettleStrike();   // the turn begins: the judged moment
            if (whiffed) yield break;

            // Toward the camera first (the rig's turn): clockwise facing right, anticlockwise left.
            var facing0 = Facing;
            float sign = facing0.x >= 0f ? -1f : 1f;
            // The circle's radius, as ResolveArc measures it - a body outside it is not swept, so
            // one walking in later in the turn can still be caught.
            float reach = (Art.Gear.StatPercents.Apply(BaseRange, Stats.Range) + step.RangeBonus
                           + (Resource?.AttackRangeBonus ?? 0f)) * Mods.RangeMul * Tuning.Disc.RangeMul * AoeScale;
            float dur = Mathf.Max(0.05f, end - start);
            // Each body is struck once a turn: its n-th hit is due when the sweep passes its
            // angle plus n-1 whole turns. Each hit is the turn's share of the step.
            int turns = Tuning.Carousel.Revolutions;
            float share = 1f / turns;
            var hits = new Dictionary<Health, int>();
            bool resolved = false;
            float t = 0f;
            while (true)
            {
                float u = Mathf.Clamp01(t / dur);
                float swept = 360f * turns * Mathf.SmoothStep(0f, 1f, u);
                var part = new HashSet<Health>();
                Enemies.EnemyRegistry.Prune();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    if (e == null) continue;
                    var hp = e.GetComponent<Health>();
                    if (hp == null || hp.IsDead) continue;
                    hits.TryGetValue(hp, out int had);
                    if (had >= turns) continue;
                    var to = (Vector2)hp.transform.position - (Vector2)transform.position;
                    if (to.magnitude > reach + 0.35f) continue;
                    float a = Mathf.Repeat(Vector2.SignedAngle(facing0, to) * sign, 360f);
                    // Two discs half a turn apart: whichever reaches the body first.
                    float first = Mathf.Min(a, Mathf.Repeat(a + 180f, 360f));
                    if (first + 360f * had <= swept) part.Add(hp);
                }
                if (part.Count > 0)
                {
                    _arcOnly = part;
                    _arcRepeat = resolved;
                    try { ResolveArc(step, true, advanceCombo: false, damageScale: share); }
                    finally { _arcOnly = null; _arcRepeat = false; }
                    resolved = true;
                    foreach (var hp in part) { hits.TryGetValue(hp, out int had); hits[hp] = had + 1; }
                }
                if (u >= 1f) break;
                yield return null;
                t += Time.deltaTime;
            }
            // Nothing in reach all the way round: the swing still counts (a miss).
            if (!resolved)
            {
                _arcOnly = new HashSet<Health>();
                try { ResolveArc(step, true, advanceCombo: false); }
                finally { _arcOnly = null; }
            }
        }

        /// <summary>
        /// Heavy Cleaver (AttackStep.DiscCleaves; the user's calls, 2026-10-15): the chop plays and
        /// at its drawn strike one HEAVY disc leaves the main hand (Tuning.Cleaver: bigger, slower,
        /// a slower spin) at what is in front, knocking back each body it hits (the Heavy's
        /// displacement) and bouncing, each bounce carrying half the hit before it. Rolled once.
        /// </summary>
        IEnumerator CleaverStrike(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            float drawn = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            if (drawn > 0f) yield return new WaitForSeconds(drawn);
            SettleStrike();   // the chop is the strike
            if (whiffed) yield break;

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            int ricochets = Tuning.Disc.BaseRicochets + step.DiscRicochetBonus
                            + (BonusRicochets?.Invoke() ?? 0) + Mods.ExtraRicochets;

            var disc = ThrownDisc.Throw(gameObject, Rig, Facing, dmg,
                                        Resource?.Element ?? ElementType.Fire,
                                        ricochets, ThrowReach, CurrentRange * Tuning.Disc.BounceRangeMul,
                                        step.DiscFarFraction, isFinisher: true, discIndex: 0);
            if (disc == null) yield break;
            if (Rig != null && Rig.Transform != null && Rig.TryGetSwordHand(out var local))
                disc.transform.position = Rig.Transform.TransformPoint(local);
            var locked = Targeting != null ? Targeting.Target : null;
            if (locked != null && !locked.IsDead
                && ((Vector2)locked.transform.position - (Vector2)transform.position).magnitude <= ThrowReach)
                disc.Aim(locked);
            disc.transform.localScale *= Tuning.Cleaver.FlightScale;
            disc.Speed = Tuning.Cleaver.Speed;
            disc.SpinMul = Tuning.Cleaver.SpinMul;
            disc.Crit = crit;
            disc.Falloff = CleaveFalloff(Tuning.Cleaver.BounceFalloff);
            disc.SplashFraction = SplashFraction;
            disc.Knockback = HeavyKnockback(step, true);
        }

        /// <summary>
        /// Orrery (AttackStep.DiscOrrery; the user's calls, 2026-10-16): the cast plays - out of
        /// ORBIT both arms sweep up and out - the player free to move (Carousel's rule), and at its
        /// drawn strike the pair spirals out of the hands onto two orbits (Combat.OrbitingDiscs),
        /// for what is left of the swing, then back in. Damage and crit rolled here, once.
        /// </summary>
        IEnumerator OrreryStrike(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            _lockTimer = Mods.RootedWhileSwinging ? AttackMotions.SwingSeconds(swingWindow) : 0f;   // free to move
            var facing = Facing;
            float drawn = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            if (drawn > 0f) yield return new WaitForSeconds(drawn);
            SettleStrike();   // the cast is the strike
            if (whiffed) yield break;

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);

            Vector2 main = transform.position, off = transform.position;
            if (Rig != null && Rig.Transform != null && Rig.TryGetSwordHand(out var local))
                main = off = Rig.Transform.TransformPoint(local);
            var offR = Rig?.OffhandRenderer;
            if (offR != null && offR.enabled) off = offR.transform.position;

            // The rest of the art's INTERVAL (the swing animation is capped well short of it),
            // so the discs are home as the next chain can start.
            float seconds = swingWindow * AttackMotions.FillFraction - drawn;
            OrbitingDiscs.Launch(gameObject, this, Rig, dmg, crit,
                                 Resource?.Element ?? ElementType.Fire, facing, seconds, main, off);
        }

        /// <summary>
        /// Sublimate (AttackStep.DiscSuspends; the user's calls, 2026-10-16): the opening plays -
        /// the crossed arms snap open - and at its drawn strike the discs fly from both hands
        /// (copies: the pair stays in hand) to hang over their enemies (HomingTargets, shared out
        /// in turn) and follow them, until the next weapon art sets them off (Dispatch, before
        /// the new art resolves). Damage and crit rolled here, once.
        /// </summary>
        IEnumerator SublimateStrike(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            var facing = Facing;
            float drawn = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            if (drawn > 0f) yield return new WaitForSeconds(drawn);
            SettleStrike();   // the opening is the strike
            if (whiffed) yield break;

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);

            Vector2 main = transform.position, off = transform.position;
            if (Rig != null && Rig.Transform != null && Rig.TryGetSwordHand(out var local))
                main = off = Rig.Transform.TransformPoint(local);
            var offR = Rig?.OffhandRenderer;
            if (offR != null && offR.enabled) off = offR.transform.position;

            // Replaces any set still hanging rather than stacking. Two live sets would make the
            // ordering unreadable - you could no longer tell which art was arming which
            // detonation, which is the entire point of the move.
            if (Suspended != null) Destroy(Suspended.gameObject);
            int n = Mathf.Max(1, step.DiscThrows);
            Suspended = SuspendedDiscs.Place(gameObject, this, Rig, facing,
                                             HomingTargets(facing, ThrowReach, n), n,
                                             step.DiscSpreadDegrees, step.DiscSuspendDistance, dmg, crit,
                                             Resource?.Element ?? ElementType.Fire, main, off);
        }

        /// <summary>
        /// Mark (AttackStep.DiscMarks; the user's calls, 2026-10-16): the flick plays - the off arm
        /// pointing at the target - and at its drawn strike ONE disc leaves the main hand at the
        /// lock in reach, bouncing Tuning.Mark.BounceRangeMul x melee reach to the nearest body
        /// not yet painted, painting each it strikes (Combat.MarkSigil). The mark waits for the
        /// next weapon art's hit (Health.Take).
        /// </summary>
        IEnumerator MarkStrike(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            float drawn = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            if (drawn > 0f) yield return new WaitForSeconds(drawn);
            SettleStrike();   // the flick is the strike
            if (whiffed) yield break;

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            int ricochets = Tuning.Disc.BaseRicochets + step.DiscRicochetBonus
                            + (BonusRicochets?.Invoke() ?? 0) + Mods.ExtraRicochets;

            var disc = ThrownDisc.Throw(gameObject, Rig, Facing, dmg,
                                        Resource?.Element ?? ElementType.Fire,
                                        ricochets, ThrowReach, CurrentRange * Tuning.Mark.BounceRangeMul,
                                        step.DiscFarFraction, isFinisher: true, discIndex: 0);
            if (disc == null) yield break;
            if (Rig != null && Rig.Transform != null && Rig.TryGetSwordHand(out var local))
                disc.transform.position = Rig.Transform.TransformPoint(local);
            var locked = Targeting != null ? Targeting.Target : null;
            if (locked != null && !locked.IsDead
                && ((Vector2)locked.transform.position - (Vector2)transform.position).magnitude <= ThrowReach)
                disc.Aim(locked);
            disc.Crit = crit;
            disc.Falloff = CleaveFalloff(disc.Falloff);
            disc.SplashFraction = SplashFraction;
            disc.Knockback = HeavyKnockback(step, true);
            disc.MarkMultiplier = step.DiscMarkMultiplier;
            disc.MarkSeconds = step.DiscMarkSeconds;
        }

        /// <summary>Carom's pair held JOINED - from the hands meeting until the disc is home.</summary>
        bool _discJoined;

        IEnumerator CaromStrike(AttackStep step, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * ArtLockMul;
            _settleIsFree = !Mods.RootedWhileSwinging;   // its hit is out: the settle is the player's (SettleRelease)
            float drawn = Rig != null ? Rig.StrikeDrawnSeconds(step.Motion, swingWindow) : 0f;
            float combine = drawn * Tuning.Carom.CombineAt;
            if (combine > 0f) yield return new WaitForSeconds(combine);
            _discJoined = true;   // the hands meet: one disc
            Rig?.SetWeaponSplit(false);
            if (drawn - combine > 0f) yield return new WaitForSeconds(drawn - combine);
            SettleStrike();   // the hurl is the strike
            if (whiffed) { _discJoined = false; yield break; }

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            int bounces = Tuning.Disc.BaseRicochets + step.DiscRicochetBonus
                          + (BonusRicochets?.Invoke() ?? 0) + Mods.ExtraRicochets;

            Health first = null;
            var locked = Targeting != null ? Targeting.Target : null;
            if (locked != null && !locked.IsDead
                && ((Vector2)locked.transform.position - (Vector2)transform.position).magnitude <= ThrowReach)
                first = locked;

            var disc = ThrownDisc.Throw(gameObject, Rig, Facing, dmg,
                                        Resource?.Element ?? ElementType.Fire,
                                        0, ThrowReach, CurrentRange * Tuning.Disc.BounceRangeMul,
                                        step.DiscFarFraction, isFinisher: true, discIndex: 0);
            if (disc == null) { _discJoined = false; yield break; }
            disc.ShowCombined();   // the pair joined: the weapon's combined picture
            if (Rig != null && Rig.Transform != null && Rig.TryGetSwordHand(out var local))
                disc.transform.position = Rig.Transform.TransformPoint(local);
            disc.Carom(first, Facing, bounces);
            disc.Crit = crit;
            disc.Falloff = CleaveFalloff(disc.Falloff);
            disc.SplashFraction = SplashFraction;
            disc.Knockback = HeavyKnockback(step, true);
            disc.Landed = () => _discJoined = false;
        }

        /// <summary>Ripple's targets: the locked enemy first, then the nearest others in reach (not
        /// behind a wall), up to <paramref name="max"/> - then ordered by angle round the facing,
        /// clockwise first, so the fan's discs each take the one on their own side.</summary>
        List<Health> HomingTargets(Vector2 facing, float reach, int max)
        {
            Vector2 at = transform.position;
            var all = new List<Health>();
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                Vector2 p = hp.transform.position;
                if ((p - at).sqrMagnitude > reach * reach || Arena.WallBetween(at, p)) continue;
                all.Add(hp);
            }
            var locked = Targeting != null ? Targeting.Target : null;
            all.Sort((x, y) =>
            {
                if (x == y) return 0;
                if (x == locked) return -1;
                if (y == locked) return 1;
                return ((Vector2)x.transform.position - at).sqrMagnitude
                       .CompareTo(((Vector2)y.transform.position - at).sqrMagnitude);
            });
            if (all.Count > max) all.RemoveRange(max, all.Count - max);
            all.Sort((x, y) => Vector2.SignedAngle(facing, (Vector2)x.transform.position - at)
                               .CompareTo(Vector2.SignedAngle(facing, (Vector2)y.transform.position - at)));
            return all;
        }

        /// <summary>
        /// Loose a single arrow at whatever PlayerTargeting is currently holding. No ricochet, no
        /// return flight - the bow stays in hand the whole time, unlike the disc which IS the
        /// thing that flies, so there is no hand to empty and nothing to bring back.
        /// </summary>
        void FireArrow(AttackStep step, bool isFinisher, float swingWindow)
        {
            bool whiffed = _swingWhiffed;
            float animSeconds = AttackMotions.SwingSeconds(swingWindow);

            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, animSeconds, false);

            if (whiffed)
            {
                if (isFinisher) SettleStrike();   // no arrow, but the bar still closes on time
                return;
            }

            // The Hive: the shot is one of the bow's own arrows, peeled off a limb (HiveLoose).
            Combat.HiveLoose.For(this).Draw(animSeconds);
            // A SNAP shot leaves early; the rest of the swing is follow-through.
            float release = step.Motion == AttackMotion.SnapDraw ? animSeconds * Tuning.Bow.SnapRelease : animSeconds;
            StartCoroutine(ReleaseArrow(step, isFinisher, release));
        }

        /// <summary>
        /// PLANT TO FIRE: how much of a bow's draw the player spent moving. The bow never locks
        /// the player - a draw on the move just hits for less (Tuning.Bow.MovingDamage, scaled by
        /// the share of the draw spent moving). A class, not a struct: a coroutine fills it.
        /// </summary>
        sealed class DrawWatch
        {
            public float Moving, Total;
            public float DamageMul => Total <= 0f ? 1f
                : Mathf.Lerp(1f, Tuning.Bow.MovingDamage, Mathf.Clamp01(Moving / Total));
        }

        /// <summary>Waits <paramref name="seconds"/> (scaled time), counting the moving part.</summary>
        IEnumerator WatchDraw(DrawWatch watch, float seconds)
        {
            while (watch.Total < seconds)
            {
                yield return null;
                float dt = Mathf.Min(Time.deltaTime, seconds - watch.Total);
                watch.Total += dt;
                if (IsMoving) watch.Moving += dt;
            }
        }

        /// <summary>
        /// Waits out the whole draw before the arrow actually leaves. It used to spawn on the
        /// same frame as the button press, so the shot was already in flight while the character
        /// was still visibly reaching for a vial - measured directly by checking for the arrow
        /// object immediately after triggering the attack, before any animation time had passed.
        ///
        /// Target and range are read AFTER the wait, not before - the same rule a charged strike
        /// resolves by: against where things actually are once the wind-up ends, not where they
        /// were when it started. A target that died or ran out of range during the draw should
        /// spoil the shot, not be hit anyway on stale information.
        /// </summary>
        IEnumerator ReleaseArrow(AttackStep step, bool isFinisher, float delaySeconds)
        {
            var watch = new DrawWatch();
            yield return WatchDraw(watch, delaySeconds);
            var nockedAt = Rig?.LooseArrow();   // the rig held full draw until now

            // The release is the finisher's strike, whether or not anything is left to shoot.
            if (isFinisher) SettleStrike();

            var loose = GetComponent<Combat.HiveLoose>();
            var target = Targeting != null ? Targeting.Target : null;
            if (target == null || target.IsDead
                || Vector2.Distance(transform.position, target.transform.position) > CurrentRange)
            {
                loose?.Release(null);   // nothing to loose at: the Hive's nocked arrow goes home
                yield break;
            }

            float dmg = (BaseDamage + Mods.BonusDamage)
                        * step.DamageMultiplier
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(isFinisher, out bool crit);
            if (isFinisher) dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            dmg *= watch.DamageMul;   // plant to fire

            var arrow = ThrownArrow.Fire(gameObject, target, Facing, dmg,
                             Resource?.Element ?? ElementType.Fire,
                             CurrentRange, Tuning.Bow.NearFraction, isFinisher, crit,
                             from: nockedAt);   // off the string, where the drawn arrow lay
            arrow.SplashFraction = SplashFraction;
            arrow.PierceFraction = PierceFraction;   // Fletching and Broadhead add run Pierce
            arrow.Knockback = HeavyKnockback(step, isFinisher);
            arrow.BasicsWorth = step.BasicsWorth;
            bool planted = watch.Moving <= 0.02f;
            if (!isFinisher) Effects?.ArmArrow(arrow, planted);   // Patient String, Long Ember
            if (step.LobArc > 0f)
                arrow.Lob(step.LobArc * Vector2.Distance(transform.position, target.transform.position));
            if (step.TwinShot)
            {
                // TWIN NOCK: two arrows, half the step each - the second at the target's nearest
                // neighbour within reach of it, else at the target as well; off the string a hair
                // apart, so the pair reads as two.
                arrow.Damage = dmg * 0.5f;
                var near = FormationNearest(target, Tuning.Bow.TwinReach, 1);
                var second = near.Count > 0 ? near[0] : target;
                Vector2 from2 = (nockedAt ?? (Vector2)transform.position) + Vector2.down * Tuning.Bow.TwinSpread;
                var twin = ThrownArrow.Fire(gameObject, second, Facing, dmg * 0.5f,
                               Resource?.Element ?? ElementType.Fire,
                               CurrentRange, Tuning.Bow.NearFraction, isFinisher, crit, from: from2);
                twin.SplashFraction = SplashFraction;
                twin.PierceFraction = PierceFraction;
                twin.BasicsWorth = 0;
                if (!isFinisher) Effects?.ArmArrow(twin, planted);
            }
            loose?.Release(arrow);
        }

        /// <summary>
        /// HARPOON (AttackStep.Harpoon, the user's calls 2026-10-17): drawn from the TWIN NOCK like
        /// its pair's twin shot, both arrows loosed at the target together - the first trails the
        /// line (Combat.BowHarpoon), the second strikes beside it. Where the line sticks, the struck
        /// enemy and up to Tuning.Harpoon.Extra others near it are reeled in to StopAt in front of
        /// the archer, who is held until the line lets go. Damage rolled once at the loose, the
        /// thrown rules; no flinch (Light). Nothing in range: the art is spent.
        /// </summary>
        IEnumerator HarpoonStrike(AttackStep step, float swingWindow)
        {
            float anim = AttackMotions.SwingSeconds(swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, anim, false);
            var watch = new DrawWatch();
            yield return WatchDraw(watch, anim);
            var nockedAt = Rig?.LooseArrow();
            SettleStrike();   // the loose is the strike

            var target = Targeting != null ? Targeting.Target : null;
            if (target == null || target.IsDead
                || Vector2.Distance(transform.position, target.transform.position) > CurrentRange)
            {
                _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
                if (_streakAwaiting) ResolveStreak(false);
                yield break;
            }

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            dmg *= watch.DamageMul;   // plant to fire
            var element = Resource?.Element ?? ElementType.Fire;
            Vector2 from = nockedAt ?? (Vector2)transform.position;

            var line = ThrownArrow.Fire(gameObject, target, Facing, dmg * Tuning.Harpoon.LineShare, element,
                                        CurrentRange, Tuning.Bow.NearFraction, true, crit, from: from);
            line.SplashFraction = SplashFraction;
            line.PierceFraction = PierceFraction;
            var other = ThrownArrow.Fire(gameObject, target, Facing, dmg * (1f - Tuning.Harpoon.LineShare), element,
                                         CurrentRange, Tuning.Bow.NearFraction, true, crit,
                                         from: from + Vector2.down * Tuning.Bow.TwinSpread);
            other.SplashFraction = SplashFraction;

            bool home = false, caught = false;
            Combat.BowHarpoon.Follow(this, line,
                hp => { caught = true; return FormationNearest(hp, Tuning.Harpoon.GrabRadius, Tuning.Harpoon.Extra); },
                () => home = true);

            while (!home)
            {
                _lockTimer = Mathf.Max(_lockTimer, 0.05f);   // held while the line is out
                yield return null;
            }
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            if (!caught && _streakAwaiting) ResolveStreak(false);
        }

        /// <summary>The bow art's draw to the loose (the drawn arrow's place, the bar settled) and
        /// the damage rolled there - thrown rules, once, plant-to-fire. Shared by Split, Pin and
        /// Tripwire.</summary>
        IEnumerator BowArtDraw(AttackStep step, float swingWindow, System.Action<Vector2?, float, bool> loosed)
        {
            float anim = AttackMotions.SwingSeconds(swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, anim, false);
            var watch = new DrawWatch();
            yield return WatchDraw(watch, anim);
            var nockedAt = Rig?.LooseArrow();
            SettleStrike();   // the loose is the strike
            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            dmg *= watch.DamageMul;
            loosed(nockedAt, dmg, crit);
        }

        Vector2 ArtAim(Health target) => target != null && !target.IsDead
            ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized
            : (Facing.sqrMagnitude > 1e-6f ? Facing.normalized : Vector2.right);

        Health LockedInRange()
        {
            var t = Targeting != null ? Targeting.Target : null;
            return t != null && !t.IsDead && Vector2.Distance(transform.position, t.transform.position) <= CurrentRange ? t : null;
        }

        /// <summary>
        /// SPLIT (AttackStep.SplitShot): out of the CANTED, one arrow flat at the target; at
        /// Tuning.Split.SplitAt of the way it breaks into three ThrownArrows - the target and its
        /// nearest within Reach (the target again for any missing), a third of the step each.
        /// Nothing locked in range: the art is spent.
        /// </summary>
        IEnumerator SplitStrike(AttackStep step, float swingWindow)
        {
            Vector2? nock = null; float dmg = 0f; bool crit = false;
            yield return BowArtDraw(step, swingWindow, (n, d, c) => { nock = n; dmg = d; crit = c; });
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            var target = LockedInRange();
            if (target == null) { if (_streakAwaiting) ResolveStreak(false); yield break; }

            Vector2 from = nock ?? (Vector2)transform.position;
            Vector2 at = Vector2.Lerp(from, target.transform.position, Tuning.Split.SplitAt);
            var element = Resource?.Element ?? ElementType.Fire;
            Combat.BowFlight.Fly(from, at, Tuning.Split.Speed, ArrowSprite, p =>
            {
                if (this == null) return;
                var aims = new List<Health> { target };
                if (target != null && !target.IsDead)
                    aims.AddRange(FormationNearest(target, Tuning.Split.Reach, 2));
                while (aims.Count < 3) aims.Add(target);
                var line = ((Vector2)target.transform.position - p).normalized;
                var across = new Vector2(-line.y, line.x);
                for (int i = 0; i < 3; i++)
                {
                    if (aims[i] == null || aims[i].IsDead) continue;
                    var a = ThrownArrow.Fire(gameObject, aims[i], line, dmg / 3f, element, CurrentRange,
                                             Tuning.Bow.NearFraction, true, crit,
                                             from: p + across * (Tuning.Split.Fan * (i - 1)));
                    a.SplashFraction = SplashFraction;
                    a.PierceFraction = PierceFraction;
                    a.SuppressHitstop = i > 0;
                }
            });
        }

        /// <summary>
        /// PIN (AttackStep.Pin): out of the KNEEL, two arrows at the target a beat apart, half the
        /// step each. The first to strike ROOTS it for Tuning.Pin.RootSeconds (petrify - it cannot
        /// walk but still attacks; never a boss) and nails two arrows at its feet (Combat.PinStakes).
        /// </summary>
        IEnumerator PinStrike(AttackStep step, float swingWindow)
        {
            Vector2? nock = null; float dmg = 0f; bool crit = false;
            yield return BowArtDraw(step, swingWindow, (n, d, c) => { nock = n; dmg = d; crit = c; });
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            var target = LockedInRange();
            if (target == null) { if (_streakAwaiting) ResolveStreak(false); yield break; }

            var element = Resource?.Element ?? ElementType.Fire;
            bool pinned = false;
            for (int i = 0; i < 2; i++)
            {
                if (i == 1) yield return new WaitForSeconds(0.08f);
                if (target == null || target.IsDead) yield break;
                Vector2 from = (nock ?? (Vector2)transform.position) + Vector2.down * (Tuning.Bow.TwinSpread * i);
                var a = ThrownArrow.Fire(gameObject, target, ArtAim(target), dmg * 0.5f, element, CurrentRange,
                                         Tuning.Bow.NearFraction, true, crit, from: from);
                a.SplashFraction = SplashFraction;
                a.PierceFraction = PierceFraction;
                a.SuppressHitstop = i > 0;
                a.OnHit += (hp, info) =>
                {
                    if (pinned || hp != target || hp.IsDead) return;
                    pinned = true;
                    if (hp.GetComponentInParent<Bosses.Boss>() != null) return;
                    StatusEffects.Get(hp.gameObject).ApplyPetrify(Tuning.Pin.RootSeconds);
                    Combat.PinStakes.Nail(hp, ArrowSprite, Tuning.Pin.RootSeconds);
                };
            }
        }

        /// <summary>
        /// TRIPWIRE (AttackStep.Tripwire; reworked by the user's calls 2026-10-18 - HEAVY, it
        /// explodes): out of the TWIN NOCK, two arrows staked either side of the aim, always
        /// Tuning.Tripwire.Length apart, a wire between (Combat.BowTripwire): Ahead out along the aim,
        /// or two thirds of the way to a nearer locked target. The first enemy to reach the wire sets
        /// it off: a blast where it was crossed, BlastRadius x Area - the step at the centre down to
        /// EdgeShare at the rim (rolled at the loose, thrown rules, one hit-stop), every body flinched
        /// through armour and thrown OUT from the blast (Heavy), and everything within SlowRadius
        /// slowed. Judged on timing alone (_streakExempt) - a trap may catch nothing.
        /// </summary>
        IEnumerator TripwireStrike(AttackStep step, float swingWindow)
        {
            Vector2? nock = null; float dmg = 0f; bool crit = false;
            yield return BowArtDraw(step, swingWindow, (n, d, c) => { nock = n; dmg = d; crit = c; });
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;

            var target = LockedInRange();
            Vector2 at = transform.position;
            var aim = ArtAim(target);
            float ahead = Tuning.Tripwire.Ahead;
            if (target != null) ahead = Mathf.Min(ahead, Vector2.Distance(at, target.transform.position) * 0.67f);
            var across = new Vector2(-aim.y, aim.x) * (Tuning.Tripwire.Length * 0.5f);
            var mid = at + aim * ahead;
            var element = Resource?.Element ?? ElementType.Fire;
            float knock = HeavyKnockback(step, true);
            Combat.BowTripwire.Lay(nock ?? at, mid - across, mid + across, ArrowSprite, blast =>
            {
                if (this == null) return;
                float radius = Tuning.Tripwire.BlastRadius * AoeScale;
                Combat.EnergyWave.Run(blast, radius, Tuning.Tripwire.BlastCore, Tuning.Tripwire.BlastVein, Tuning.Tripwire.BlastEmber);
                Spr.Flash(blast, radius * 0.6f, Tuning.Tripwire.BlastCore, 0.2f, false);
                var bodies = new List<(Health hp, float d)>();
                Enemies.EnemyRegistry.Prune();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    if (e == null || e.FellIn) continue;
                    var hp = e.GetComponent<Health>();
                    if (hp == null || hp.IsDead) continue;
                    float d = Vector2.Distance(blast, hp.transform.position);
                    if (Arena.WallBetween(blast, hp.transform.position)) continue;
                    if (d <= Tuning.Tripwire.SlowRadius * AoeScale)
                        StatusEffects.Get(hp.gameObject).ApplySlow(Tuning.Tripwire.SlowSeconds, Tuning.Tripwire.SlowMultiplier);
                    if (d <= radius) bodies.Add((hp, d));
                }
                bodies.Sort((p, q) => p.d.CompareTo(q.d));
                for (int i = 0; i < bodies.Count; i++)
                {
                    var (hp, d) = bodies[i];
                    float share = Mathf.Lerp(1f, Tuning.Tripwire.EdgeShare, d / Mathf.Max(0.01f, radius));
                    float amount = ScaleThrownHit(hp, dmg * share, true, crit, 0.5f, i == 0);
                    var away = (Vector2)hp.transform.position - blast;
                    away = away.sqrMagnitude > 1e-4f ? away.normalized : aim;
                    var info = new DamageInfo(amount, element, gameObject)
                    {
                        Thrown = true, IsFinisher = true, Crit = crit, SuppressHitstop = i > 0,
                        Knockback = away * knock, Displaces = knock > 0f,
                    };
                    hp.Take(info);
                    OnWeaponUsed?.Invoke();
                    Resource?.OnHitLanded(hp, info);
                    NotifyThrownHit(hp, info, 0.5f, i == 0);
                    if (!hp.IsDead)
                        hp.GetComponent<Enemies.EnemyController>()?.Flinch(Core.Tuning.Finisher.FlinchSeconds, true);
                }
            });
        }

        /// <summary>
        /// LONGSHOT (AttackStep.Longshot, the user's calls 2026-10-18): out of the KNEEL, drawn and
        /// HELD for Tuning.Longshot.DrawSeconds - rooted, the timing bar over the whole draw - then
        /// one arrow straight down the aim (the locked target, else the facing) across the room
        /// until a wall or the room's edge (Combat.LongshotStreak). Every body within HalfWidth of
        /// the line is struck as the arrow passes it, nearest first, each after the first keeping
        /// Falloff of the one before - thrown rules, rolled once, no distance curve. HEAVY: flinched
        /// through armour and knocked on along the line; one hit-stop, on the first body.
        /// </summary>
        IEnumerator LongshotStrike(AttackStep step, float swingWindow)
        {
            float draw = Tuning.Longshot.DrawSeconds;
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            Rig?.HoldDrawn(draw + 0.1f);
            _lockTimer = draw * ArtLockMul;   // rooted on the knee through the draw
            yield return new WaitForSeconds(draw);
            var nockedAt = Rig?.LooseArrow();
            SettleStrike();   // the loose is the strike

            var target = Targeting != null ? Targeting.Target : null;
            Vector2 at = transform.position;
            Vector2 aim = target != null && !target.IsDead
                ? ((Vector2)target.transform.position - at).normalized
                : (Facing.sqrMagnitude > 1e-6f ? Facing.normalized : Vector2.right);

            // How far the line runs: the first wall, or the room's edge.
            float reach = 0f;
            const float Step = 0.2f;
            for (float d = Step; d <= Tuning.Longshot.MaxReach; d += Step)
            {
                var p = at + aim * d;
                if ((Arena.Clamp(p, 0f) - p).sqrMagnitude > 1e-6f || Arena.WallBetween(at + aim * (d - Step), p)) break;
                reach = d;
            }
            Combat.LongshotStreak.Fly(nockedAt ?? at, at + aim * reach, ArrowSprite);

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            var element = Resource?.Element ?? ElementType.Fire;
            float knock = HeavyKnockback(step, true);

            // On the line, nearest first.
            var line = new List<(Health hp, float along)>();
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null || e.FellIn) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                var to = (Vector2)hp.transform.position - at;
                float along = Vector2.Dot(to, aim);
                if (along < 0f || along > reach + Tuning.Longshot.HalfWidth) continue;
                if (Mathf.Abs(to.x * aim.y - to.y * aim.x) > Tuning.Longshot.HalfWidth) continue;
                line.Add((hp, along));
            }
            line.Sort((p, q) => p.along.CompareTo(q.along));

            float clock = 0f;
            for (int i = 0; i < line.Count; i++)
            {
                var (hp, along) = line[i];
                // Struck as the arrow passes it.
                float due = along / Tuning.Longshot.Speed;
                while (clock < due) { yield return null; clock += Time.deltaTime; }
                if (hp == null || hp.IsDead) continue;
                float share = Mathf.Max(MinChainFraction, Mathf.Pow(Tuning.Longshot.Falloff, i));
                float amount = ScaleThrownHit(hp, dmg * share, true, crit, along / Mathf.Max(1f, reach), i == 0);
                var info = new DamageInfo(amount, element, gameObject)
                {
                    Thrown = true, IsFinisher = true, Crit = crit, SuppressHitstop = i > 0,
                    Knockback = aim * knock, Displaces = knock > 0f,
                };
                hp.Take(info);
                OnWeaponUsed?.Invoke();
                Resource?.OnHitLanded(hp, info);
                NotifyThrownHit(hp, info, along / Mathf.Max(1f, reach), i == 0);
                if (!hp.IsDead)
                    hp.GetComponent<Enemies.EnemyController>()?.Flinch(Core.Tuning.Finisher.FlinchSeconds, true);
            }
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            if (line.Count == 0 && _streakAwaiting) ResolveStreak(false);
        }

        /// <summary>
        /// PIERCING RAIN (AttackStep.ArrowRain, the user's calls 2026-10-18): drawn from the HIGH,
        /// one arrow loosed straight up (Combat.ArrowRain draws the rest). The circle is fixed where
        /// the locked target stood at the loose (else out along the aim), Tuning.PiercingRain.Radius
        /// x Area. After RiseSeconds it rains for RainSeconds: Ticks times, evenly, every body inside
        /// takes 1/Ticks of the step (RimShare of that at the rim) - thrown rules, rolled once at the
        /// loose, no hit-stop until the last. HEAVY: each body is flinched through its armour the
        /// first time it is struck, and the LAST tick throws everything inside OUT from the centre
        /// (the step's knockback x Finisher Knockback; Anchored elites and bosses stay). A wall
        /// between the centre and a body shelters it.
        /// </summary>
        IEnumerator PiercingRainStrike(AttackStep step, float swingWindow)
        {
            float anim = AttackMotions.SwingSeconds(swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, anim, false);
            var watch = new DrawWatch();
            yield return WatchDraw(watch, anim);
            var nockedAt = Rig?.LooseArrow();
            SettleStrike();   // the loose is the strike

            var target = Targeting != null ? Targeting.Target : null;
            Vector2 at = transform.position;
            Vector2 centre = target != null && !target.IsDead
                             && Vector2.Distance(at, target.transform.position) <= CurrentRange
                ? (Vector2)target.transform.position
                : at + (Facing.sqrMagnitude > 1e-6f ? Facing.normalized : Vector2.right) * Tuning.PiercingRain.NoTargetAhead;
            float radius = Tuning.PiercingRain.Radius * AoeScale;
            Combat.ArrowRain.Play(nockedAt ?? at, centre, radius, ArrowSprite);

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            dmg *= watch.DamageMul;   // plant to fire
            var element = Resource?.Element ?? ElementType.Fire;
            float knock = HeavyKnockback(step, true);

            var flinched = new HashSet<Health>();
            bool first = true, struck = false;
            int ticks = Mathf.Max(1, Tuning.PiercingRain.Ticks);
            float gap = Tuning.PiercingRain.RainSeconds / ticks;
            yield return new WaitForSeconds(Tuning.PiercingRain.RiseSeconds + gap * 0.5f);
            for (int t = 0; t < ticks; t++)
            {
                if (t > 0) yield return new WaitForSeconds(gap);
                bool last = t == ticks - 1;
                Enemies.EnemyRegistry.Prune();
                var inside = new List<(Health hp, float d)>();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    if (e == null || e.FellIn) continue;
                    var hp = e.GetComponent<Health>();
                    if (hp == null || hp.IsDead) continue;
                    float d = Vector2.Distance(centre, hp.transform.position);
                    if (d > radius || Arena.WallBetween(centre, hp.transform.position)) continue;
                    inside.Add((hp, d));
                }
                foreach (var (hp, d) in inside)
                {
                    float share = Mathf.Lerp(1f, Tuning.PiercingRain.RimShare, d / Mathf.Max(0.01f, radius)) / ticks;
                    float amount = ScaleThrownHit(hp, dmg * share, true, crit, 0.5f, first);
                    var info = new DamageInfo(amount, element, gameObject)
                        { Thrown = true, IsFinisher = true, Crit = crit, SuppressHitstop = !last };
                    hp.Take(info);
                    OnWeaponUsed?.Invoke();
                    Resource?.OnHitLanded(hp, info);
                    NotifyThrownHit(hp, info, 0.5f, first);
                    first = false;
                    struck = true;
                    if (hp.IsDead) continue;
                    if (flinched.Add(hp))
                        hp.GetComponent<Enemies.EnemyController>()?.Flinch(Core.Tuning.Finisher.FlinchSeconds, true);
                    if (last && knock > 0f && !hp.Anchored && hp.GetComponentInParent<Bosses.Boss>() == null)
                    {
                        var rb = hp.GetComponent<Rigidbody2D>();
                        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) continue;
                        // Out of the circle: to RimShare past its rim, at least a body's width.
                        float travel = Mathf.Max(0.6f, radius + 0.5f - d) * Art.Gear.StatPercents.Apply(1f, Stats.FinisherKnockback);
                        rb.linearVelocity = Vector2.zero;
                        Combat.DragToward.Push(hp, d > 0.05f ? centre : centre - (centre - at).normalized, travel);
                    }
                }
            }
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            if (!struck && _streakAwaiting) ResolveStreak(false);
        }

        /// <summary>
        /// PALISADE (AttackStep.Palisade, the user's calls 2026-10-18): drawn from the HIGH, and on
        /// the loose Tuning.Palisade.Arrows go up and come down (Combat.BowPalisade) - onto enemy
        /// shots in flight first, the rest in a line across the aim - and stick as stakes that each
        /// stop one more shot. An enemy walking over a stake breaks it and takes StakeShare of the
        /// step (rolled here, thrown rules, flinched if unarmoured - Medium). Judged on timing
        /// alone (_streakExempt): a perfect press keeps the streak whether or not anything is cut.
        /// </summary>
        IEnumerator PalisadeStrike(AttackStep step, float swingWindow)
        {
            float anim = AttackMotions.SwingSeconds(swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, anim, false);
            var watch = new DrawWatch();
            yield return WatchDraw(watch, anim);
            var nockedAt = Rig?.LooseArrow();
            SettleStrike();   // the loose is the strike

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            dmg *= watch.DamageMul;   // plant to fire
            float stake = dmg * Tuning.Palisade.StakeShare;
            var element = Resource?.Element ?? ElementType.Fire;

            var target = Targeting != null ? Targeting.Target : null;
            Vector2 at = transform.position;
            Vector2 aim = target != null && !target.IsDead
                ? ((Vector2)target.transform.position - at).normalized
                : (Facing.sqrMagnitude > 1e-6f ? Facing.normalized : Vector2.right);
            bool first = true;
            Combat.BowPalisade.Loose(at, nockedAt ?? at, aim, ArrowSprite, hp =>
            {
                if (this == null || hp == null || hp.IsDead) return;
                float d = ScaleThrownHit(hp, stake, true, crit, 0f, first);
                var info = new DamageInfo(d, element, gameObject)
                    { Thrown = true, IsFinisher = true, Crit = crit, SuppressHitstop = true };
                hp.Take(info);
                OnWeaponUsed?.Invoke();
                Resource?.OnHitLanded(hp, info);
                NotifyThrownHit(hp, info, 0f, first);
                first = false;
                if (!hp.IsDead)
                    hp.GetComponent<Enemies.EnemyController>()?.Flinch(Core.Tuning.Finisher.FlinchSeconds, false);
            });
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
        }

        /// <summary>
        /// DISENGAGE (AttackStep.Disengage, the user's calls 2026-10-18): out of the RECOIL, a gather
        /// and a BACKFLIP (AttackMotion.FlipDraw) - a real dash of Tuning.Disengage.Distance from
        /// takeoff to touchdown, so walls stop it and a chasm where it ends is judged. AWAY from the
        /// locked target; with none, away from the move input; with neither, back from the facing.
        /// No immunity (positioning only). Drawn through the turn, the arrow is loosed DOWN at the
        /// target from the top - the Medium, thrown rules - and the art's own flight costs nothing
        /// under plant-to-fire. The player is held until the landing. Nothing to shoot at: the
        /// flip still goes and the art is spent.
        /// </summary>
        IEnumerator DisengageStrike(AttackStep step, float swingWindow)
        {
            float anim = AttackMotions.SwingSeconds(swingWindow);
            var target = Targeting != null ? Targeting.Target : null;
            if (target != null && target.IsDead) target = null;
            Vector2 dir = target != null
                ? (Vector2)transform.position - (Vector2)target.transform.position
                : -Core.Controls.Move;
            if (dir.sqrMagnitude <= 0.01f) dir = -Facing;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.left;
            dir.Normalize();

            Rig?.SetSwingHop(Tuning.Disengage.Height);
            // The raw window: the rig takes SwingSeconds of it itself, so the drawing's phases
            // and these waits run on one clock (given anim, it ran 15% fast and loosed falling).
            Rig?.PlayAttack(step.Motion, swingWindow, false);
            _lockTimer = anim * ArtLockMul;

            // Off the ground: the dash runs from takeoff to touchdown.
            float takeoff = anim * Tuning.Disengage.TakeoffAt;
            float flight = anim * (Tuning.Disengage.LandAt - Tuning.Disengage.TakeoffAt);
            if (takeoff > 0f) yield return new WaitForSeconds(takeoff);
            Dash(dir, Tuning.Disengage.Distance / flight, flight);

            // The loose, at the top.
            float loose = anim * (Tuning.Disengage.LooseAt - Tuning.Disengage.TakeoffAt);
            if (loose > 0f) yield return new WaitForSeconds(loose);
            var nockedAt = Rig?.LooseArrow();
            SettleStrike();   // the loose is the strike

            if (target == null || target.IsDead
                || Vector2.Distance(transform.position, target.transform.position) > CurrentRange + Tuning.Disengage.Distance)
            {
                _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
                if (_streakAwaiting) ResolveStreak(false);
                yield break;
            }

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            var arrow = ThrownArrow.Fire(gameObject, target, Facing, dmg, Resource?.Element ?? ElementType.Fire,
                                         CurrentRange + Tuning.Disengage.Distance, Tuning.Bow.NearFraction, true, crit,
                                         from: nockedAt);
            arrow.SplashFraction = SplashFraction;
            arrow.PierceFraction = PierceFraction;
            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
        }

        /// <summary>
        /// CONCUSSION (AttackStep.Concussion, the user's calls 2026-10-18): drawn from the RECOIL,
        /// and on the loose the arrow BURSTS just off the string (Combat.ConcussionBurst). Every
        /// enemy in the cone along the aim (Tuning.Concussion.Reach / HalfAngle, not behind a wall)
        /// takes the Medium, nearest first with a swing's falloff - thrown rules, rolled once - is
        /// FLINCHED if unarmoured (Medium), and is slid out to PushPast beyond the cone's reach
        /// (Sunder's rule, x Finisher Knockback; Anchored elites and bosses stay). Everything struck,
        /// and anything within SlowRadius of the nearest body struck, is SLOWED (the plain slow).
        /// An empty cone still bursts: the art is spent.
        /// </summary>
        IEnumerator ConcussionStrike(AttackStep step, float swingWindow)
        {
            float anim = AttackMotions.SwingSeconds(swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, anim, false);
            _lockTimer = anim * ArtLockMul;
            var watch = new DrawWatch();
            yield return WatchDraw(watch, anim);
            var nockedAt = Rig?.LooseArrow();
            SettleStrike();   // the loose is the strike

            var target = Targeting != null ? Targeting.Target : null;
            Vector2 at = transform.position;
            Vector2 aim = target != null && !target.IsDead
                          && Vector2.Distance(at, target.transform.position) <= CurrentRange
                ? ((Vector2)target.transform.position - at).normalized
                : (Facing.sqrMagnitude > 1e-6f ? Facing.normalized : Vector2.right);
            Vector2 burst = (nockedAt ?? at) + aim * Tuning.Concussion.BurstAhead;
            Combat.ConcussionBurst.Play(burst, aim, Tuning.Concussion.Reach);

            float dmg = (BaseDamage + Mods.BonusDamage) * step.DamageMultiplier
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(true, out bool crit);
            dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;
            dmg *= watch.DamageMul;   // plant to fire
            var element = Resource?.Element ?? ElementType.Fire;
            float falloff = CleaveFalloff(step.ChainFalloff);

            // The cone, nearest first.
            var struck = new List<(Health hp, float dist)>();
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null || e.FellIn) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                var to = (Vector2)hp.transform.position - at;
                float d = to.magnitude;
                if (d > Tuning.Concussion.Reach) continue;
                if (d > 0.01f && Vector2.Angle(aim, to) > Tuning.Concussion.HalfAngle) continue;
                if (Arena.WallBetween(at, hp.transform.position)) continue;
                struck.Add((hp, d));
            }
            struck.Sort((p, q) => p.dist.CompareTo(q.dist));

            Vector2? contact = struck.Count > 0 ? (Vector2)struck[0].hp.transform.position : null;
            for (int i = 0; i < struck.Count; i++)
            {
                var (hp, dist) = struck[i];
                float share = Mathf.Max(MinChainFraction, Mathf.Pow(falloff, i));
                float d = ScaleThrownHit(hp, dmg * share, true, crit, dist / Mathf.Max(0.01f, CurrentRange), i == 0);
                var info = new DamageInfo(d, element, gameObject)
                    { Thrown = true, IsFinisher = true, Crit = crit, SuppressHitstop = i > 0 };
                hp.Take(info);
                OnWeaponUsed?.Invoke();
                Resource?.OnHitLanded(hp, info);
                NotifyThrownHit(hp, info, 0f, i == 0);
                if (hp.IsDead) continue;
                hp.GetComponent<Enemies.EnemyController>()?.Flinch(Core.Tuning.Finisher.FlinchSeconds, false);
                StatusEffects.Get(hp.gameObject).ApplySlow(Tuning.Concussion.SlowSeconds, Tuning.Concussion.SlowMultiplier);

                // Out past the cone - Sunder's rule.
                if (hp.Anchored || hp.GetComponentInParent<Bosses.Boss>() != null) continue;
                var rb = hp.GetComponent<Rigidbody2D>();
                if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic) continue;
                float travel = Mathf.Max(Tuning.Concussion.PushMin,
                                         Tuning.Concussion.Reach + Tuning.Concussion.PushPast - dist)
                               * Art.Gear.StatPercents.Apply(1f, Stats.FinisherKnockback);
                rb.linearVelocity = Vector2.zero;
                // From the archer, so the crowd goes straight back along its own line.
                Combat.DragToward.Push(hp, dist > 0.05f ? at : at - aim, travel);
            }

            // The group round the point of contact is slowed with it.
            if (contact.HasValue)
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    var hp = e != null ? e.GetComponent<Health>() : null;
                    if (hp == null || hp.IsDead) continue;
                    if (Vector2.Distance(contact.Value, hp.transform.position) > Tuning.Concussion.SlowRadius) continue;
                    StatusEffects.Get(hp.gameObject).ApplySlow(Tuning.Concussion.SlowSeconds, Tuning.Concussion.SlowMultiplier);
                }

            _cooldown = AttackMotions.CooldownFor(swingWindow) * 0.5f;
            if (struck.Count == 0 && _streakAwaiting) ResolveStreak(false);
        }

        /// <summary>
        /// A bow basic that is a MOVE (AttackStep.KneelRoll, the user's calls 2026-10-17): a turn
        /// through the views down onto one knee while the body travels Tuning.Bow.RollDistance - a
        /// real dash, so walls stop it and a chasm at its end is judged. The direction is the
        /// MOVE input held as it goes, never the aim; with none it rolls AWAY from the target (the
        /// bow wants space), else back from the facing. No arrow, no i-frames - positioning only.
        /// </summary>
        void BowRollStrike(AttackStep step, float swingWindow)
        {
            float anim = AttackMotions.SwingSeconds(swingWindow);
            // The roll goes by the move input; the RECOIL always straight back from the target.
            Vector2 dir = step.RecoilHop ? Vector2.zero : Core.Controls.Move;
            if (dir.sqrMagnitude <= 0.01f)
            {
                var t = Targeting != null ? Targeting.Target : null;
                dir = t != null && !t.IsDead
                    ? (Vector2)transform.position - (Vector2)t.transform.position
                    : -Facing;
            }
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.left;
            Rig?.SetSwingHop(step.RecoilHop ? Tuning.Bow.RecoilHopHeight : 0f);
            Rig?.PlayAttack(step.Motion, anim, false);
            float seconds = Mathf.Min(step.RecoilHop ? Tuning.Bow.RecoilSeconds : Tuning.Bow.RollSeconds, anim);
            float distance = step.RecoilHop ? Tuning.Bow.RecoilDistance : Tuning.Bow.RollDistance;
            Dash(dir.normalized, distance / seconds, seconds);
        }

        /// <summary>
        /// The smear behind the blade for this swing - see Combat.SwingSmear. Greatswords only: a
        /// disc's swing has no blade to smear, and the bow never swings. Shadow's echo figures
        /// play their own swings and smear nothing.
        /// </summary>
        /// <summary>
        /// The knockback a thrown disc or an arrow carries: a HEAVY weapon art's, lengthened by
        /// Finisher Knockback, as a swing's (ResolveArc) - 0 for anything lighter or a basic. The
        /// swords' rule for every class (the user's call, 2026-10-15).
        /// </summary>
        float HeavyKnockback(AttackStep step, bool isFinisher)
            => isFinisher && step != null && step.Weight == FinisherWeight.Heavy
                ? step.Knockback * Art.Gear.StatPercents.Apply(1f, Stats.FinisherKnockback)
                : 0f;

        void SmearSwing(float swingWindow, bool isFinisher)
        {
            if (Weapon != Art.Gear.WeaponClass.Greatsword) return;
            Combat.SwingSmear.Play(Rig, transform, AttackMotions.SwingSeconds(swingWindow), isFinisher);
        }

        void ResolveArc(AttackStep step, bool isFinisher, bool advanceCombo = true,
                        float damageScale = 1f)
        {
            // Range comes from THIS step, not from NextStep. A charged strike resolves after the
            // chain has already advanced, so CurrentRange would describe the wrong swing entirely.
            float range = (Art.Gear.StatPercents.Apply(BaseRange, Stats.Range) + step.RangeBonus
                          + (Resource?.AttackRangeBonus ?? 0f)) * Mods.RangeMul
                          * (Weapon == Art.Gear.WeaponClass.Disc ? Tuning.Disc.RangeMul : 1f);

            // The heat finisher's circle grows with the blade's colour. Added AFTER the disc
            // multiplier on purpose: the bonus is a declared world distance per stage, and scaling
            // it by a weapon-class multiplier would make the same stage mean two different radii.
            if (step.ScalesWithHeat && Heat != null) range += Heat.CurrentRadiusBonus;
            if (step.AreaOfEffect) range *= AoeScale;

            GatherTargets(step, range);

            // Captured BEFORE the resolve, because completing the finisher advances the cycle and
            // the fire it leaves belongs to the swing that made it, not to the next one.
            var tintAtSwing = Heat != null ? Heat.CurrentTint
                                           : ElementInfo.Tint(Resource?.Element ?? ElementType.Fire);

            bool landed = false;
            bool splashed = false;
            bool boardDesignated = false;
            bool ledgerDesignated = false;
            float boardFirstHit = 0f;
            float chain = 1f;
            var origin = ArcOrigin;
            float roll = DamageRoll();   // once per swing, shared by every body it catches

            foreach (var (hp, dist, hazardMul) in _ordered)
            {
                var delta = (Vector2)hp.transform.position - origin;
                var to = dist > 0.0001f ? delta / dist : ArcFacing;

                if (_swingWhiffed) break;

                var mods = Mods;
                float critMul = RollCritFor(isFinisher, hp, out bool crit);

                // BonusDamage is added to the BASE before the step multiplier, so +2 is worth more
                // on a heavy finisher than on a basic. A flat addend applied afterwards would make
                // every swing in the chain equally better, which is not what a sharper blade does.
                float dmg = (BaseDamage + mods.BonusDamage)
                            * step.DamageMultiplier;
                dmg *= roll * critMul;
                if (isFinisher) dmg *= mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;

                // A shadow echo swings at a fraction of the finisher it borrowed. Applied here,
                // on the same line as every other multiplier, rather than by pre-scaling the
                // step: the steps are shared objects owned by MovesetLibrary, and an echo that
                // edited one would permanently weaken the player's own copy of the move.
                dmg *= damageScale;

                // Disc melee hits heavy. Applied here rather than written into fifteen declared
                // basics because it is a property of the CLASS, not of any one moveset - and
                // anything reaching ResolveArc is melee by definition, since throws never do.
                if (Weapon == Art.Gear.WeaponClass.Disc) dmg *= Tuning.Disc.MeleeDamageMul;

                // The ledger's conditional damage - Executioner, First Blood, Souring and the rest -
                // is applied here rather than folded into Mods because every one of them needs the
                // TARGET (RunEffects.ModifyOutgoing: run-layer points, bent with the ledger's own).
                var ctx = new Exchange.HitContext
                {
                    IsFinisher = isFinisher,
                    First = !ledgerDesignated && !_arcRepeat,
                    Reach01 = range > 0.01f ? dist / range : 0f,
                    Proc = 1f,
                };
                if (Effects != null) dmg = Effects.ModifyOutgoing(hp, dmg, ctx);

                // The mastery board's rules that depend on the target (BoardEffects) - Vitriol,
                // Saltpetre, Mortification, Inceration. Not on an echo's repeat.
                if (Board != null && !_echoing) dmg = Board.ModifyOutgoing(hp, dmg, isFinisher, crit);

                // A target only reachable through a force field: nulled (0) or doubled - see
                // HazardDamageMul. The hit still happens (flash, chain slot, everything Health.Take
                // already does for a zero-damage hit) - only the number changes.
                dmg *= hazardMul;

                if (step.AreaOfEffect)
                {
                    // A blast is strongest under the player and tapers to the rim. Linear rather
                    // than squared: an inverse-square falloff is physically honest and unreadable
                    // in play, because almost the whole circle ends up in the weak tail and the
                    // ring drawn on the ground stops describing anything the player can feel.
                    // Expansion: the edge loses less (EdgeLossKept of its loss).
                    if (step.EdgeDamageFraction < 1f)
                    {
                        float edge = 1f - (1f - step.EdgeDamageFraction) * (Effects != null ? Effects.EdgeLossKept : 1f);
                        dmg *= Mathf.Lerp(1f, edge, Mathf.Clamp01(dist / Mathf.Max(range, 0.01f)));
                    }
                }
                else
                {
                    // A blade loses energy through a crowd. The first body takes the whole hit;
                    // every one after it takes less, floored so a big cleave still means something
                    // on its fifth target.
                    dmg *= chain;
                    chain = Mathf.Max(MinChainFraction, chain * CleaveFalloff(step.ChainFalloff));
                }

                // Worn weapons hit softer.
                if (DamageDealtMultiplier != null) dmg *= DamageDealtMultiplier();

                // A pull is the same impulse inverted. The undertow pulls at full strength inside
                // roughly twice the reach ring, and eases off with distance past that, out to the
                // edge of the view. So everything on screen is dragged in, but a far-flung enemy
                // arrives in a loose ring rather than being fired through the player and out the
                // far side, and UndertowEdgePullFraction keeps even the rim of the screen closing
                // rather than stalling.
                float kick;
                if (step.PullsIn)
                {
                    float full = BaseRange * UndertowFullPullRangeMul;
                    float t = Mathf.InverseLerp(ScreenReach(), full, dist);   // 1 within `full`, 0 at the screen edge
                    kick = -step.Knockback * Mathf.Lerp(UndertowEdgePullFraction, 1f, t);
                }
                else
                {
                    kick = step.Knockback;
                }

                // Only HEAVY finishers move enemies - see Displaces below. Finisher Knockback
                // lengthens that throw (or pull) and nothing else: it never touches flinch.
                bool displaces = isFinisher && step.Weight == FinisherWeight.Heavy;
                if (displaces) kick *= Art.Gear.StatPercents.Apply(1f, Stats.FinisherKnockback);

                var info = new DamageInfo(dmg, Resource?.Element ?? ElementType.Fire, gameObject)
                {
                    Crit = crit,
                    Knockback = to * kick,

                    // Only HEAVY finishers move enemies. Basics never did, and Medium and Light
                    // now join them: a chain of ordinary hits lands on a target that stays where
                    // it is, and only the heaviest payoff swing visibly throws it. That is what
                    // buys Heavy its 2.5x lock - see Tuning.Finisher.
                    //
                    // This narrowed from `isFinisher`, which displaced on EVERY finisher. Covers
                    // the pull too, since Undertow is Heavy - same flag, inverted direction.
                    Displaces = displaces,

                    // The katana draw-cut carries this; the two pre-cuts (a plain step the
                    // coroutine builds) do not, so only the finishing stroke leaves a body in
                    // two. Read by Combat.Bisection from GameBootstrap.HookDeath - a picture,
                    // nothing about the hit.
                    Bisects = step.SheathDraw,

                    // A Magnum Opus kill comes apart, whether the slash or the crescent made it.
                    Disintegrates = step.MagnumOpus,

                    // See DamageInfo's own doc - this is what lets Bubbles' shield tell a
                    // finisher apart from a basic without Health knowing anything about movesets.
                    IsFinisher = isFinisher,

                    SuppressHitstop = step.SuppressHitstop,
                };

                bool wasAlive = !hp.IsDead;
                var where = hp.transform.position;

                // MERCURY - phase strike. Asked immediately before the hit is applied and spent
                // here, so an armed charge cannot be left set across a swing that never connected.
                // Pierce is a one-hit flag on the armour itself: EnemyArmor absorbs through
                // Health.ModifyIncoming, so there is nowhere else to intercept it from.
                if (Principles != null && !_echoing)
                {
                    var armour = hp.GetComponent<Combat.EnemyArmor>();
                    if (armour != null && armour.Current > 0f && Principles.ConsumePhaseStrike())
                    {
                        armour.PierceNextHit = true;
                        Spr.Flash(where, 0.7f, new Color(0.6f, 0.85f, 1f), 0.22f, false);
                    }
                }

                PierceArmourFor(hp);   // Keen Edge
                hp.Take(info);
                Effects?.OnHitLanded(hp, info, ctx);
                ledgerDesignated = true;
                if (Board != null && !_echoing)
                {
                    bool first = !boardDesignated && !_arcRepeat;
                    if (first) { boardDesignated = true; boardFirstHit = dmg; }
                    Board.OnHitLanded(hp, info, isFinisher, range > 0.01f ? dist / range : 0f, first);
                }

                // Splash spills off the DESIGNATED target only - the first body the swing reached
                // (targets are nearest-first). Off every body in a cleave it would compound.
                if (!splashed && !_echoing && !_arcRepeat)
                {
                    splashed = true;
                    CrowdHits.Splash(hp, dmg, SplashFraction, info.Element, gameObject);
                }

                // Shadow's chain passive: the same repeat, unconditional. Deliberately the same
                // fraction as the boon above - the two stack as "more often", never as "and also
                // harder", which is what a second, larger number here would have meant.
                //
                // NOT while _echoing. An echo's hits are already the repeat; letting them repeat
                // again would square the passive on the one move that fires it four times.
                float echo = ActiveEchoFraction;
                if (echo > 0f && !_echoing && !hp.IsDead)
                {
                    hp.Take(new DamageInfo(dmg * echo, info.Element, gameObject) { IsFinisher = isFinisher });
                    Spr.Flash(where, 0.42f, Core.Tuning.Shadow.Tint, 0.16f, false);
                }

                // The top of the heat cycle leaves a burn on everything the circle caught. Its
                // OWN burn track, not the element's - see StatusEffects.ApplyWeaponBurn for why
                // sharing one would make this invisible to exactly the fire characters most
                // likely to be swinging it.
                if (step.ScalesWithHeat && Heat != null && Heat.AppliesBurn && !hp.IsDead)
                    Combat.StatusEffects.Get(hp.gameObject).ApplyWeaponBurn(
                        dmg * Combat.WeaponHeat.BlueBurnFraction,
                        Combat.WeaponHeat.BlueBurnSeconds, gameObject);

                // FLINCH. Medium and Heavy interrupt; Light and every basic never do - which is
                // also what makes a per-enemy cooldown unnecessary, since the chain already spaces
                // them two basics and a finisher apart. Heavy is the tier defined as punching
                // through armour; a Medium into a shielded enemy is refused and returns false.
                //
                // Applied AFTER the damage rather than before, so an enemy the swing kills is not
                // briefly routed into a reel state on its way to being destroyed.
                if (isFinisher && !hp.IsDead && step.Weight != FinisherWeight.Light)
                {
                    var enemy = hp.GetComponent<Enemies.EnemyController>();
                    enemy?.Flinch(Core.Tuning.Finisher.FlinchSeconds,
                                  step.Weight == FinisherWeight.Heavy);
                }

                // A REAL push (Sunder): every body the circle caught slides out past its rim. A
                // knockback is one impulse into heavy damping - 0.43 units, measured - so the
                // impulse is cancelled and the body is moved a set distance instead
                // (DragToward.Push: walls stop it, a chasm on the way takes it). The hit's
                // knockback still decided whether a chasm within reach throws it in
                // (EnemyController.ThrowIfChasm, on Damaged) - a body already going over is left.
                if (step.PushesOut && isFinisher && !hp.IsDead && displaces)
                    PushOut(hp, origin, dist, range);

                // SULFUR - basics season, finishers do not. The chain rewards sustained
                // pressure, and letting the swing that already pays best also build fastest would
                // collapse that into "use finishers", which every build already wants to do.
                if (!isFinisher && !hp.IsDead) Principles?.OnBasicLanded(hp);

                Resource?.OnHitLanded(hp, info);
                Drain(dmg);
                landed = true;

                if (!step.CleavesAll) break;   // basics stay single-target; finishers sweep
            }

            // The board hears the swing once, however many bodies it caught: a basic steeps the next
            // weapon art (Cohobation); an AREA weapon art draws the crowd in and salts the ground
            // (Borax, Fermentation) round its blast.
            if (landed && !_echoing && !_arcRepeat && Board != null)
            {
                if (!isFinisher) Board.OnBasicLanded();
                else if (step.AreaOfEffect) Board.OnAreaAttack(origin, range, boardFirstHit);
            }

            // ONE swing, one wear tick. An Echo finisher resolves up to five times - once as the
            // player's own blast and once per shadow - and charging durability for each would
            // make the weapon that summons them the fastest way to destroy itself.
            if (_arcRepeat) { }   // a later part of one swing (Carousel's turn): the swing counted once
            else if (landed && !_echoing) OnWeaponUsed?.Invoke();   // landing a hit is what wears a weapon
            else Resource?.OnAttackMissed();

            // The cycle turns on the finisher being COMPLETED, not on it connecting. A blast that
            // caught nobody is still a blast; making heat depend on finding a target would punish
            // exactly the moment a player uses it to clear space.
            if (step.ScalesWithHeat && !_echoing) Heat?.Advance();

            // Blood Blade: the OPPOSITE gate from heat, deliberately - see BloodVial's own doc.
            // step.ReleaseStep != null is only true on the FILLING half (the release step itself
            // carries no nested ReleaseStep), so this can never fire twice on the same cast.
            if (step.ReleaseStep != null && landed && !_echoing) Vial?.Advance();

            // A charged strike already advanced the chain when it began winding up; advancing
            // again here would skip the next moveset in the rotation.
            if (advanceCombo) AdvanceCombo(isFinisher);

            // A ring only fires for a blast, because a ring is a claim about the shape of the
            // hit. On a sword swing it drew a circle in front of the character on EVERY strike -
            // describing the old circular hitbox, which no longer exists, and doing it right where
            // the blade the player is actually watching does the same job.
            if (step.ScalesWithHeat && Heat != null)
            {
                // The heat finisher plants the blade and the fire comes out of the GROUND, so its
                // circle is a ground decal at exactly the damage radius rather than a burst over
                // the character. Drawn in the stage's own colour, so the widening circle and the
                // blade are telling the player the same thing.
                //
                // Note this reads Heat AFTER the resolve above, which has already advanced the
                // cycle - so use the tint captured before it turned, or the ground fire is one
                // stage ahead of the sword that made it.
                Spr.GroundBurn(origin, range, tintAtSwing, 0.55f);
            }
            else if (step.AreaOfEffect && !step.DiscCarousel && !step.Gyres)   // Carousel's discs draw their own circle; Gyre's grind its rotor
            {
                var tint = _echoing ? Core.Tuning.Shadow.Tint
                          : ActiveMoveset != null && ActiveMoveset.Id == PhantomSignatureId
                              ? Core.Tuning.Phantom.Tint
                          : ActiveMoveset != null && ActiveMoveset.Id == BloodSignatureId
                              ? Core.Tuning.Blood.Tint
                          : ElementInfo.Tint(Resource?.Element ?? ElementType.Fire);
                Spr.Flash(origin, range * 0.9f, tint, 0.3f);
                if (isFinisher)
                    Spr.Flash(origin, range * 0.6f, _echoing ? tint : Color.white, 0.22f);
            }
            // else: an ordinary swing (not AoE, not the heat finisher) used to draw a crescent
            // slash arc here (Spr.Slice) - a gray/white swipe over the blade's own animation.
            // Removed: the swing animation itself already shows the strike, and the crescent
            // read as a wash over the character rather than adding information.
        }

        /// <summary>
        /// True while a finisher is being resolved from a figure standing somewhere other than
        /// the player - Separatio's three parts, the Armillary's four cones. Suppresses exactly
        /// three things - the chain passive (which would otherwise repeat every figure), the
        /// durability tick, and the heat cycle - and nothing else: each figure's hit is a real
        /// hit for damage, for the element meter and for every conditional in the ledger,
        /// because it IS your finisher, thrown from somewhere else.
        /// </summary>
        bool _echoing;

        /// <summary>
        /// Resolve one part of the player's own finisher from where a figure is standing
        /// (Separatio's parts, Quintessence's cones).
        ///
        /// Synchronous and self-restoring: nothing between the two assignments yields, so the
        /// overrides cannot be left set for the player's own next swing.
        ///
        /// advanceCombo is false because the chain already advanced when the finisher that
        /// spawned the figures fired - advancing again per figure would skip movesets.
        /// </summary>
        public void ResolveEcho(AttackStep step, Vector2 origin, Vector2 facing, float damageScale)
        {
            if (step == null) return;

            _arcOrigin = origin;
            _arcFacing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Facing;
            _echoing = true;
            try
            {
                ResolveArc(step, isFinisher: true, advanceCombo: false, damageScale: damageScale);
            }
            finally
            {
                _echoing = false;
                _arcOrigin = null;
                _arcFacing = null;
            }
        }

        /// <summary>
        /// Step the chain. After a finisher the rotation advances, so the next chain ends with
        /// the next moveset's finisher - A then B then C then back to A.
        /// </summary>
        void AdvanceCombo(bool firedFinisher)
        {
            _comboTimer = ComboResetSeconds * Mods.ComboTimeMul;   // Fraying

            if (firedFinisher)
            {
                ComboIndex = 0;
                RotationIndex = (RotationIndex + 1) % Slots.Count;   // next slot's finisher
                // Golden Chain: every third weapon art comes with no basics before it.
                if (Effects != null && Effects.OnArtCompleted()) RefundFinisher();
            }
            else ComboIndex++;
        }

        // ------------------------------------------------------------------ continuation

        /// <summary>What a run snapshot keeps of the player beyond health: the wheel as earned and
        /// arranged, where its rotation stands, the kills and the perfect streak.</summary>
        public void CaptureRun(Chain.RunSnapshot s)
        {
            s.Slots.Clear();
            s.SlotLocked.Clear();
            for (int i = 0; i < Slots.Count; i++)
            {
                s.Slots.Add(Slots[i] != null ? Slots[i].Id : null);
                s.SlotLocked.Add(SlotLocked(i));
            }
            s.ThirdSlotRemoved = _thirdSlotRemoved;
            s.RotationIndex = RotationIndex;
            s.Kills = Kills;
            s.PerfectStreak = _perfectStreak;
        }

        /// <summary>
        /// A snapshot's wheel, kills and streak, laid over a freshly built player. Called after the
        /// signature, the seeded slot and the ledger's slot count are in, so a slot the snapshot
        /// names that no longer exists falls back to this class's default rather than leaving a gap.
        /// </summary>
        public void RestoreRun(Chain.RunSnapshot s)
        {
            if (s.Slots != null && s.Slots.Count > 0)
            {
                Slots.Clear();
                _locked.Clear();
                for (int i = 0; i < s.Slots.Count; i++)
                {
                    var m = string.IsNullOrEmpty(s.Slots[i]) ? null : MovesetLibrary.ById(s.Slots[i]);
                    Slots.Add(m ?? MovesetLibrary.DefaultFor(Weapon));
                    _locked.Add(s.SlotLocked != null && i < s.SlotLocked.Count && s.SlotLocked[i]);
                }
                _thirdSlotRemoved = s.ThirdSlotRemoved;
            }
            RotationIndex = Slots.Count > 0 ? Mathf.Clamp(s.RotationIndex, 0, Slots.Count - 1) : 0;
            ComboIndex = 0;
            Kills = Mathf.Max(0, s.Kills);
            _perfectStreak = Mathf.Max(0, s.PerfectStreak);
        }

        /// <summary>Put a moveset into a slot, replacing whatever was there.</summary>
        public bool SetSlot(int index, Moveset moveset)
        {
            if (moveset == null || index < 0 || index >= Slots.Count) return false;
            Slots[index] = moveset;
            return true;
        }

        public bool HoldsMoveset(Moveset moveset)
        {
            foreach (var m in Slots) if (m == moveset) return true;
            return false;
        }
    }
}
