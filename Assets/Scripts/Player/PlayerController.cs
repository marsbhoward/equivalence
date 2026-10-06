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
            if (want) _tapBuffer = 0f;   // a press made as flesh must not come out of the stone
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
        /// Movesets still declare three basics; the third is simply not reached. Left in place so
        /// this number can move again without re-authoring every moveset.
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
        public bool ParryWindowOpen => _parryWindowRemaining > 0f;

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

            if (Core.Controls.AltTapped && _defenseCooldown <= 0f && !Statue) ActivateDefensiveAbility();
        }

        void ActivateDefensiveAbility()
        {
            float cdMul = Art.Gear.StatPercents.ReductionFactor(Stats.AbilityCooldownReduction)
                          * Mods.DefenseCooldownMul;   // Encumbered
            // The board's Sal Ammoniac stretches the protection and the parry window, never the
            // cooldown or the dash's distance.
            float window = Board != null ? Board.DefenseWindowMul : 1f;
            _parryWindowRemaining = Tuning.Defense.ParryWindowSeconds * window * Mods.ParryWindowMul;   // Shackled, Unshackled

            switch (EquippedDefensiveAbility)
            {
                case Art.Gear.DefensiveAbility.Dash:
                    _defenseActiveTimer = Tuning.Defense.DashSeconds * window;
                    _defenseCooldown = Tuning.Defense.DashCooldown * cdMul;
                    if (Health != null) Health.Immune = true;

                    var input = Core.Controls.Move;
                    var dashDir = input.sqrMagnitude > 0.01f ? input.normalized : Facing;
                    Dash(dashDir, Tuning.Defense.DashSpeed, Tuning.Defense.DashSeconds);
                    // Figures dropped along the ground actually covered, for exactly as long as
                    // the i-frames last - see DashTrail. Null on any build whose chest grants
                    // something else, which is most of them.
                    if (Trail != null) Trail.Begin(Tuning.Defense.DashSeconds);
                    break;

                case Art.Gear.DefensiveAbility.Barrier:
                    _defenseActiveTimer = Tuning.Defense.BarrierSeconds * window;
                    _defenseCooldown = Tuning.Defense.BarrierCooldown * cdMul;
                    _barrierFacing = Facing;
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
            if (_parryWindowRemaining <= 0f) return false;
            _parryWindowRemaining = 0f;
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

            FireCounterStrike();

            // The board's Volatilization: a parry hands the ability straight back.
            if (Board != null && Board.RefundsOnParry) _defenseCooldown = 0f;
            return true;
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
        /// sword's target against.</summary>
        public float PendingRange => CurrentRange;

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
                if (FinisherNext) return ms.Finisher;

                // The LEAD-IN (Basics[1]) is always the swing right before the finisher, however
                // long the ledger makes the chain: it is authored to end where its finisher
                // starts, so Anvil's one-swing chain is just the lead-in, and Leaking's extra
                // swings go BETWEEN the opener and it (Basics[2]) rather than after it.
                int i = LeadInNext ? 1 : ComboIndex == 0 ? 0 : 2;
                return ms.Basics[Mathf.Min(i, ms.Basics.Length - 1)];
            }
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
        /// Mirrors whatever <see cref="ICharacterRig.SetFacingAway"/> was last told, so
        /// <see cref="UpdateTravelFacingAway"/> and <see cref="ForceFacingForward"/> can skip the
        /// call when nothing has actually changed - it rebuilds every layer's sorting order, and
        /// this runs every frame.
        /// </summary>
        bool _travelFacingAway;

        /// <summary>
        /// Turn the rig around for ordinary, untargeted movement - the same back-view
        /// <see cref="ICharacterRig.SetFacingAway"/> already gives the scripted door beat, now
        /// driven by where the player is actually walking rather than a cutscene's own clock.
        ///
        /// Deliberately does NOT run while a target is being tracked or a finisher is locked -
        /// see the two <see cref="ForceFacingForward"/> calls in <see cref="Update"/> - because
        /// showing a player's back mid-fight is exactly the combat-legibility problem
        /// <see cref="ICharacterRig.SetFacingAway"/>'s own doc warns never to wire this to.
        /// </summary>
        void UpdateTravelFacingAway(Vector2 travel)
        {
            bool away = travel.y > Tuning.Player.FacingAwayDot ? true
                      : travel.y < -Tuning.Player.FacingAwayDot ? false
                      : _travelFacingAway;   // ambiguous heading: hold whatever it already was
            if (away == _travelFacingAway) return;
            _travelFacingAway = away;
            Rig?.SetFacingAway(away);
        }
        /// <summary>Combat always shows the 3/4 view - see <see cref="UpdateTravelFacingAway"/>.</summary>
        void ForceFacingForward()
        {
            if (!_travelFacingAway) return;
            _travelFacingAway = false;
            Rig?.SetFacingAway(false);
        }

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
        /// Rolls one crit against a target: the ledger may make it one without rolling (Honed,
        /// Stoop, Cementation) and decides what it is worth (Cold Iron). Returns the multiplier.
        /// </summary>
        float RollCrit(Health target, out bool crit)
        {
            var (chance, multiplier) = Art.Gear.StatCurves.Crit(CritChanceSum, CritMulSum);
            crit = (Effects != null && target != null && Effects.ForcesCrit(target)) || Random.value < chance;
            if (!crit) return 1f;
            return Effects != null ? Effects.CritMultiplier(multiplier) : multiplier;
        }

        /// <summary>A crit roll for a hit that may be a weapon art: one settled PERFECT under the
        /// board's Cinnabar crits without rolling.</summary>
        float RollCritFor(bool isFinisher, out bool crit) => RollCritFor(isFinisher, null, out crit);

        float RollCritFor(bool isFinisher, Health target, out bool crit)
        {
            if (isFinisher && _strikeCrit)
            {
                crit = true;
                return CritMultiplierNow;
            }
            return RollCrit(target, out crit);
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
            => 1f - (1f - falloff) * Art.Gear.StatPercents.ReductionFactor(Stats.Cleave);

        Rigidbody2D _rb;
        Transform _aim;
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
            Spr.Flash(p, 1.0f, new Color(0.25f, 0.22f, 0.3f), 0.35f);
            _rb.position = back;
            transform.position = back;
            _rb.linearVelocity = Vector2.zero;
            Spr.Flash(back, 1.1f, Color.white, 0.25f);
            // A MECHANIC hit: a share of max HP through the usual mitigation.
            if (Health) Health.Take(new DamageInfo(Health.Max * Tuning.Chasm.PlayerFallFraction, ElementType.Earth, null));
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

        public void SetAimIndicator(Transform t) => _aim = t;
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

        // ---- the mastery board, for hits that land away from the swing ----

        /// <summary>A thrown hit (a disc, an arrow, the thrown blade) about to land: the ledger's
        /// and the board's target rules, exactly as ResolveArc applies them to a swing.
        /// <paramref name="reach01"/> is how far out it lands as a share of the throw's range.</summary>
        public float ScaleThrownHit(Health target, float damage, bool isFinisher, bool crit, float reach01 = 0.5f)
        {
            if (target == null) return damage;
            if (Effects != null)
            {
                damage = Effects.ModifyOutgoing(target, damage, new Exchange.HitContext
                {
                    IsFinisher = isFinisher, Reach01 = reach01, Thrown = true, First = true,
                });
                PierceArmourFor(target);
            }
            return Board != null ? Board.ModifyOutgoing(target, damage, isFinisher, crit) : damage;
        }

        /// <summary>Keen Edge: the next hit on this body passes its armour.</summary>
        void PierceArmourFor(Health target)
        {
            if (Effects == null || !Effects.PiercesArmour || target == null) return;
            var shell = target.GetComponent<Combat.EnemyArmor>();
            if (shell != null && shell.Current > 0f) shell.PierceNextHit = true;
        }

        /// <summary>
        /// A thrown hit landed. The board and Sulfur's Season hear it as they hear a swing -
        /// Season used to be swing-only, so a disc thrown or an arrow loosed never marked
        /// anything. <paramref name="first"/> is the projectile's first body; <paramref name="reach01"/>
        /// how far out it landed as a share of the throw's range.
        /// </summary>
        public void NotifyThrownHit(Health target, DamageInfo info, float reach01, bool first)
        {
            if (target == null) return;
            Effects?.OnHitLanded(target, info, new Exchange.HitContext
            {
                IsFinisher = info.IsFinisher, Reach01 = reach01, Thrown = true, First = first,
            });
            Board?.OnHitLanded(target, info, info.IsFinisher, reach01, first);
            if (info.IsFinisher) return;
            if (first) Board?.OnBasicLanded();
            if (!target.IsDead) Principles?.OnBasicLanded(target);
        }

        // ---- statuses the player applies, through the board when there is one ----
        //
        // Every element's own status goes through these, so the board's status POWER and its rules
        // (Aqua Fortis, Orpiment, Antimony, Congelation) reach the element's abilities as well as
        // the humour keystones - a Water player's own soak is the soak Soak Power strengthens.

        public void Burn(Health target, float dps, float seconds)
        {
            if (Board != null) Board.Burn(target, dps, seconds);
            else if (target != null) StatusEffects.Get(target.gameObject).ApplyBurn(dps, seconds, gameObject);
        }

        public void Soak(Health target, float seconds, float vulnerability)
        {
            if (Board != null) Board.Soak(target, seconds, vulnerability);
            else if (target != null) StatusEffects.Get(target.gameObject).ApplySoak(seconds, vulnerability);
        }

        public void Stagger(Health target, float seconds)
        {
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

        /// <summary>Held where you stand: no moving, no turning, no attacking (Recoil, Lapsus).</summary>
        public void Stun(float seconds)
        {
            _lockTimer = Mathf.Max(_lockTimer, seconds);
            _cooldown = Mathf.Max(_cooldown, seconds);
        }

        /// <summary>The defensive ability comes back sooner (Tempered).</summary>
        public void ShortenDefenseCooldown(float seconds)
            => _defenseCooldown = Mathf.Max(0f, _defenseCooldown - seconds);

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

            // A statue does nothing at all - no facing, no swing, no release. Its timers wait for
            // it, so a chain or a cooldown resumes where it was when the stone lets go.
            SyncStatue();
            if (Statue) return;

            // Facing is frozen for the whole finisher. Without this the auto-targeter keeps
            // steering the character mid-swing, so a 360 spin or a committed thrust would quietly
            // re-aim itself onto whatever wandered closest - which is exactly the decision the
            // lock exists to make the player own.
            var target = Targeting != null ? Targeting.Target : null;
            if (AttackLocked)
            {
                // hold the facing the finisher started on
                ForceFacingForward();
            }
            else if (target != null && !target.IsDead)
            {
                var d = (Vector2)target.transform.position - (Vector2)transform.position;
                if (d.sqrMagnitude > 0.0001f) Facing = d.normalized;
                ForceFacingForward();
            }
            else if (_rb.linearVelocity.sqrMagnitude > 0.35f)
            {
                Facing = _rb.linearVelocity.normalized;   // nothing to fight: face travel
                UpdateTravelFacingAway(Facing);
            }
            // A chakram in one hand is just a chakram. The pair only splits apart when the
            // player is at throwing distance - so the silhouette itself tells you which half of
            // the hybrid you are currently in, without reading the range rings.
            Rig?.SetWeaponSplit(Weapon == Art.Gear.WeaponClass.Disc && ThrowsInsteadOfSwinging);

            // The bow's own hip decoration: 2 base vials, +1 for Extra Sigil's single stack - see
            // Mods.ExtraFinisherSlots' own note on why that field is capped at one. Zero on every
            // other class, which hides the rack entirely.
            Rig?.SetVialCount(Weapon == Art.Gear.WeaponClass.Bow ? 2 + Mods.ExtraFinisherSlots : 0);

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

            if (_aim)
            {
                _aim.localPosition = Facing * (CurrentRange * 0.55f);
                _aim.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(Facing.y, Facing.x) * Mathf.Rad2Deg);
            }

            // ---- combo chain decays if you stop swinging, but a READY finisher never does ----
            //
            // Partial progress still lapses: the chain has to be a chain, and two stray swings at
            // a passing enemy should not bank toward a finisher forever. But once the three basics
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
            // ability button (below) means "blink to it". Both are fresh TAPS, not the held
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
            else if (_cooldown <= 0f && (tapPending || (attackHeld && _holdDelay <= 0f)))
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

            // ---- element ability / blade blink ----
            //
            // While a thrown blade is in the air this button takes the player to it instead -
            // forward damage only, the weapon back in hand where it was. The element release is
            // given over to the blink for the second of flight, the same way the attack button
            // above is wholly given over to the recall.
            bool releasePressed = Core.Controls.ReleaseTapped;
            if (Blade != null)
            {
                if (releasePressed) Blade.TeleportOwner();
            }
            else if (releasePressed && Resource != null && Resource.CanRelease)
                DoRelease();
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
            Resource.Release();
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
            _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, target,
                                              Mathf.Min(IsMoving ? WalkBlend : stopBlend, grip));
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
                Health.Vulnerability = Art.Gear.StatCurves.Incoming(
                                           (1f - (Resource?.DamageReduction ?? 0f))
                                           * mods.DamageTakenMul
                                           * (IncomingDamageMultiplier?.Invoke() ?? 1f),
                                           mods.MitigationFloor)
                                       * mods.DamageTakenOutside
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
        /// its last BarSeconds.</summary>
        bool _barShown;

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
        /// early - the katana's two sheathed cuts.</summary>
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

        /// <summary>
        /// Open the timing bar for a finisher being pressed, and return the wind-up it has to GAIN
        /// so the bar has its full length before the strike (0 for anything that already delays
        /// its damage long enough). Shadow's Echo runs no bar and strikes at a plain 1x.
        /// </summary>
        float BeginStrike(AttackStep step, float swingWindow)
        {
            if (step.NeverLocks)
            {
                _barLive = false;
                _strikeMul = 1f;
                return 0f;
            }

            float delay = StrikeDelay(step, swingWindow);
            float pad = Mathf.Max(0f, Tuning.StrikeTiming.BarSeconds - delay);

            _strikeParity = StrikeParity(step, swingWindow, delay, pad);
            _strikeCrit = false;
            _verdict = StrikeVerdict.Pending;
            _strikeMul = _strikeParity * StrikeJudge.Multiplier(StrikeVerdict.Pending);
            _strikeIn = pad + delay;
            _barLive = true;
            _barShown = false;
            _barOpenedFrame = Time.frameCount;

            // A perfect still waiting on the LAST finisher to connect never did - this one's hits
            // must not be credited to it.
            if (_streakAwaiting) ResolveStreak(false);
            _finisherConnected = false;
            _streakExempt = step.DiscSuspends;
            return pad;
        }

        /// <summary>
        /// Seconds from a finisher's dispatch to the hit its bar judges. MUST follow Dispatch's
        /// branch order and each path's own timing - the bar ends where this says the strike is.
        /// </summary>
        float StrikeDelay(AttackStep step, float swingWindow)
        {
            if (Weapon == Art.Gear.WeaponClass.Bow) return AttackMotions.SwingSeconds(swingWindow);  // ReleaseArrow's draw
            if (step.DiscThrows > 0) return 0f;
            if (step.LeapSeconds > 0f) return step.LeapSeconds;                    // the landing
            if (step.ReverseCones) return Core.Tuning.Quintessence.MergeSeconds;   // the first cone
            if (step.SplitsThreeWays) return Core.Tuning.Separatio.HoldSeconds;    // the first figure
            if (step.ChargeSeconds > 0f) return step.ChargeSeconds;                // the release
            if (step.SheathDraw && SheatheDrawn)                                   // the draw-cut
                return Core.Tuning.Katana.SheathSeconds + Core.Tuning.Katana.CutSeconds * 2f
                     + Core.Tuning.Katana.DrawSeconds;
            return 0f;   // an ordinary swing or a thrown blade strikes on dispatch
        }

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
            seconds += delay + AttackMotions.CooldownFor(finisherWindow);

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

            float into = Tuning.StrikeTiming.BarSeconds - _strikeIn;
            if (into < 0f) return false;

            if (!_barShown) { _barShown = true; Bar.Begin(Facing.x, _perfectStreak); }   // off-hand side, held
            Bar.Tick(into);

            if (!tapped || _verdict != StrikeVerdict.Pending || Time.frameCount == _barOpenedFrame)
                return false;

            _verdict = StrikeJudge.Judge(into);
            Bar.Press(into, _verdict);
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

            if (_verdict == StrikeVerdict.Pending) _verdict = StrikeVerdict.Missed;
            _strikeMul = _strikeParity * StrikeJudge.Multiplier(_verdict);
            LastStrikeVerdict = _verdict;

            // The board's steep (Cohobation) rides the same multiplier, and Cinnabar's crit is
            // decided here - both are about THIS weapon art, so both are fixed when it is.
            _strikeCrit = false;
            if (Board != null) _strikeMul *= Board.OnStrikeSettled(_verdict, out _strikeCrit);
            Effects?.OnArtSettled(_verdict);

            if (_barShown) Bar.Settle(_verdict);

            // The strike itself says PERFECT too - in the perfect band's green, over the swing's own flash.
            if (_verdict == StrikeVerdict.Perfect)
                Spr.Flash(transform.position, 1.3f, Tuning.StrikeTiming.Perfect, 0.24f);

            // The perfect streak. Anything but PERFECT breaks it now. A perfect counts once the
            // finisher CONNECTS: already, for the katana's sheathed cuts; on this same frame, for
            // a swing (its hits resolve right after this call); or later, for anything thrown or
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
                Bar.StreakGained(_perfectStreak);
            }
            else if (_perfectStreak > 0)
            {
                int was = _perfectStreak;
                _perfectStreak = 0;
                Bar.StreakBroken(was);
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
            // Every finisher that runs a bar strikes BarSeconds after the bar appears. One that
            // already delays its damage by at least that long shows the bar at the end of its own
            // delay; one that would have struck on this frame first holds its swing's opening
            // pose for the shortfall (WindUpThenDispatch) - mobile and still aiming, the same
            // convention ChargeSeconds lives by. The chain advances when that wind-up ENDS, not
            // now, so everything the strike reads off NextStep/CurrentRange still describes it.
            float pad = isFinisher ? BeginStrike(step, swingWindow) : 0f;
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
        void Dispatch(AttackStep step, bool isFinisher, float swingWindow, bool alt)
        {
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

            // The bow never swings - basic or finisher, every step is a single arrow at whatever
            // is currently locked. Checked before every other branch for the same reason the
            // disc's own throw check is: this does not resolve its damage through GatherTargets
            // or an arc at all, so none of the machinery below applies.
            if (Weapon == Art.Gear.WeaponClass.Bow)
            {
                FireArrow(step, isFinisher, swingWindow);
                AdvanceCombo(isFinisher);
                return;
            }

            // A disc FINISHER that declares throws resolves as a volley, not an arc.
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
            if (!isFinisher && ThrowsInsteadOfSwinging)
            {
                ThrowDisc(swingWindow);
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
                ? AttackMotions.SwingSeconds(swingWindow) * (isFinisher ? Mods.LockMul : 1f)
                : 0f;

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
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * Mods.LockMul;

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
        /// The held pose and the tightening ring every wind-up shares - Skyfall's charge and
        /// Separatio's raised blade. Publishes <see cref="ChargeProgress01"/> while it runs.
        /// </summary>
        IEnumerator WindUp(float seconds)
        {
            Rig?.PlayCharge(seconds);
            _visual?.PlayCharge(seconds);

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
            _lockTimer = (Core.Tuning.Quintessence.MergeSeconds + coneSpread) * Mods.LockMul;

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
            _lockTimer = (swing + SeparatioSpread) * Mods.LockMul;

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
            if (Health) Health.Immune = true;

            // Springing off the ground: a burst under the feet, so the launch has a moment of
            // contact rather than the character simply vanishing upward.
            Spr.Flash(transform.position, radius * 0.4f, Color.white, 0.22f);

            // The blade goes overhead and stays there for the whole flight - the same held pose
            // the charged slam uses, which is exactly what a body falling sword-first looks like.
            Rig?.PlayCharge(step.LeapSeconds);
            _visual?.PlayCharge(step.LeapSeconds);

            const float RiseSeconds = 0.28f;
            const float FallSeconds = 0.14f;
            const float PeakHeight = 9f;      // clears the top of the camera at any sane zoom

            float elapsed = 0f;
            float cue = 0f;
            while (elapsed < step.LeapSeconds)
            {
                elapsed += Time.deltaTime;
                float remaining = step.LeapSeconds - elapsed;

                // Fast up, hang, faster down. The fall is deliberately shorter than the rise:
                // coming back at the same speed you left reads as a lift, not as a drop.
                float height =
                    elapsed < RiseSeconds
                        ? Mathf.Sin(Mathf.Clamp01(elapsed / RiseSeconds) * Mathf.PI * 0.5f) * PeakHeight
                    : remaining < FallSeconds
                        ? Mathf.Clamp01(remaining / FallSeconds) * PeakHeight
                        : PeakHeight;
                Rig?.SetAirborne(height);

                // A ring closing in on the landing zone, quickening as the drop nears. Enemies
                // cannot read a character that is off the screen, so the ground has to say it.
                cue -= Time.deltaTime;
                if (cue <= 0f)
                {
                    float t = Mathf.Clamp01(elapsed / step.LeapSeconds);
                    cue = Mathf.Lerp(0.26f, 0.07f, t);
                    var c = Color.Lerp(tint, Color.white, t * 0.6f);
                    c.a = 0.30f + t * 0.45f;
                    Spr.Pulse(transform, radius * Mathf.Lerp(1.15f, 0.5f, t), c, 0.28f, true, -0.5f);
                }
                yield return null;
            }

            Rig?.SetAirborne(0f);
            Airborne = false;
            ImpactRadius = 0f;
            if (Health) Health.Immune = false;
            SettleOverChasm();

            // Landing commits like every other finisher, and the recovery starts the moment the
            // feet touch - not when the animation ends. The exposure IS the animation's meaning.
            _lockTimer = AttackMotions.SwingSeconds(swingWindow) * Mods.LockMul;
            ExposedSeconds = step.ExposedSeconds;
            _exposedMultiplier = Mathf.Max(1f, step.ExposedMultiplier);

            // No hop: the figure has just come down out of a real leap, and lifting it again on
            // the landing frame would fight the impact the whole move is built around.
            _visual?.PlayAttack(step.Motion, swingWindow);
            Rig?.SetSwingHop(0f);
            Rig?.PlayAttack(step.Motion, swingWindow, alt: false);

            // Always a finisher on this path - charge, leap and draw-cut are nothing else.
            Combat.WeaponTrail.Play(Rig, AttackMotions.SwingSeconds(swingWindow));
            SmearSwing(swingWindow, isFinisher: true);

            Spr.Flash(transform.position, radius * 1.1f, Color.white, 0.28f);
            Spr.Flash(transform.position, radius * 0.55f, tint, 0.34f);
            SettleStrike();   // the landing is the strike
            ResolveArc(step, isFinisher, advanceCombo: false);
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

        /// <summary>Total wall-clock length of the sheathe -> twin-cut -> draw sequence, so the
        /// dispatch and the coroutine agree on one number.</summary>
        float SheathDrawLength(float swingWindow)
            => Core.Tuning.Katana.SheathSeconds
             + Core.Tuning.Katana.CutSeconds * 2f
             + Core.Tuning.Katana.DrawSeconds
             + AttackMotions.SwingSeconds(swingWindow);

        /// <summary>
        /// The katana signature. Three beats, resolved here rather than through the ordinary arc
        /// path the way the leap and the throw are:
        ///
        ///   1. SHEATHE - the REAL blade sprite leaves the fist, turns onto the scabbard's axis
        ///      and slides in behind it (Art.Gear.KatanaSheathe), leaving the tsuka standing out
        ///      of the mouth. The rig's own weapon layer is hidden for the duration.
        ///   2. TWIN CUTS - two crescents cross the target while the blade is away. Each is an
        ///      ordinary <see cref="ResolveArc"/> on a small step of its own, so it still wears the
        ///      weapon, feeds fire heat and trips the echo passive - only the numbers are smaller.
        ///   3. DRAW - the blade comes back OUT of the saya along the same axis, then the rig
        ///      takes it back and swings. This step carries <c>SheathDraw</c>, so its DamageInfo
        ///      carries <c>Bisects</c> and a non-boss it finishes is left in two halves (see
        ///      Combat.Bisection, wired from GameBootstrap).
        ///
        /// The whole sequence is a committed Heavy: <c>_lockTimer</c> is set to its full length up
        /// front, which freezes movement AND facing (FaceAim reads _lockTimer), so the draw lands
        /// where the player was aiming when they pressed - a drawn cut, not a tracking swing.
        ///
        /// Scaled time throughout (WaitForSeconds), so a screen opening mid-sequence pauses it.
        /// </summary>
        IEnumerator SheathDrawStrike(AttackStep step, bool isFinisher, float swingWindow)
        {
            var tint = Core.Tuning.Katana.Tint;
            _lockTimer = SheathDrawLength(swingWindow) * Mods.LockMul;

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

            // The draw is the strike the bar judges; the two sheathed cuts above landed before it
            // closed, at the provisional GOOD rate (StrikeJudge.Multiplier).
            SettleStrike();
            ResolveArc(step, isFinisher, advanceCombo: false);
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
                var sight = HazardQuery.Query(origin, col.transform.position, gameObject, col.gameObject);
                if (sight == SightResult.Blocked) continue;
                _ordered.Add((hp, Vector2.Distance(col.transform.position, origin), HazardDamageMul(sight)));
            }

            _ordered.Sort((a, b) => a.dist.CompareTo(b.dist));
        }

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
        void ThrowDisc(float swingWindow)
        {
            // Fumbler still applies: the throw happens, it just finds nothing. Asked once, as the
            // attack was committed (Update), so a throw never pays Blood Price twice.
            bool whiffed = _swingWhiffed;

            Rig?.PlayAttack(AttackMotion.Throw, AttackMotions.SwingSeconds(swingWindow), false);

            if (whiffed) return;

            float dmg = (BaseDamage + Mods.BonusDamage)
                       
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
                disc.Falloff = Effects != null && Effects.RicochetsKeepAll ? 1f : CleaveFalloff(disc.Falloff);   // Boomerang
                disc.SplashFraction = SplashFraction;
            }
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

            // ---- ORRERY: driven in a ring around the player instead of thrown outward ----
            if (step.DiscOrbitSweeps > 0)
            {
                OrbitingDiscs.Launch(gameObject, this, Rig, Mathf.Max(1, step.DiscThrows), dmg,
                                     Resource?.Element ?? ElementType.Fire,
                                     step.DiscOrbitRadius, step.DiscOrbitSweeps);
                return;
            }

            // ---- SUBLIMATE: planted in the air, waiting for the NEXT finisher ----
            if (step.DiscSuspends)
            {
                // Replaces any set still hanging rather than stacking. Two live sets would make
                // the ordering unreadable - you could no longer tell which finisher was arming
                // which detonation, which is the entire point of the move.
                if (Suspended != null) Destroy(Suspended.gameObject);

                Suspended = SuspendedDiscs.Place(gameObject, this, Rig, Facing,
                                                 Mathf.Max(1, step.DiscThrows),
                                                 step.DiscSpreadDegrees,
                                                 step.DiscSuspendDistance, dmg,
                                                 Resource?.Element ?? ElementType.Fire);
                return;
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
                if (disc != null)
                {
                    disc.Crit = crit;
                    disc.Falloff = Effects != null && Effects.RicochetsKeepAll ? 1f : CleaveFalloff(disc.Falloff);   // Boomerang
                    disc.SplashFraction = SplashFraction;
                }
                if (disc != null && step.DiscMarkMultiplier > 1f)
                {
                    disc.MarkMultiplier = step.DiscMarkMultiplier;
                    disc.MarkSeconds = step.DiscMarkSeconds;
                }
            }
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

            Rig?.PlayAttack(step.Motion, animSeconds, false);

            if (whiffed)
            {
                if (isFinisher) SettleStrike();   // no arrow, but the bar still closes on time
                return;
            }

            StartCoroutine(ReleaseArrow(step, isFinisher, animSeconds));
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
            yield return new WaitForSeconds(delaySeconds);

            // The release is the finisher's strike, whether or not anything is left to shoot.
            if (isFinisher) SettleStrike();

            var target = Targeting != null ? Targeting.Target : null;
            if (target == null || target.IsDead) yield break;
            if (Vector2.Distance(transform.position, target.transform.position) > CurrentRange) yield break;

            float dmg = (BaseDamage + Mods.BonusDamage)
                        * step.DamageMultiplier
                        * (DamageDealtMultiplier?.Invoke() ?? 1f);
            dmg *= DamageRoll();
            dmg *= RollCritFor(isFinisher, out bool crit);
            if (isFinisher) dmg *= Mods.FinisherDamageMul * FinisherPowerMul * _strikeMul;

            var arrow = ThrownArrow.Fire(gameObject, target, Facing, dmg,
                             Resource?.Element ?? ElementType.Fire,
                             CurrentRange, Tuning.Bow.NearFraction, isFinisher, crit);
            arrow.SplashFraction = SplashFraction;
            // Broadhead: what the arrow pierces takes the full hit.
            arrow.PierceFraction = Effects != null && Effects.PierceFull && PierceFraction > 0f ? 1f : PierceFraction;
        }

        /// <summary>
        /// The smear behind the blade for this swing - see Combat.SwingSmear. Greatswords only: a
        /// disc's swing has no blade to smear, and the bow never swings. Shadow's echo figures
        /// play their own swings and smear nothing.
        /// </summary>
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
                    First = !ledgerDesignated,
                    Reach01 = range > 0.01f ? dist / range : 0f,
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
                    // Expansion: full damage out to the edge.
                    if (step.EdgeDamageFraction < 1f && !(Effects != null && Effects.FullEdge))
                        dmg *= Mathf.Lerp(1f, step.EdgeDamageFraction,
                                          Mathf.Clamp01(dist / Mathf.Max(range, 0.01f)));
                }
                else
                {
                    // A blade loses energy through a crowd. The first body takes the whole hit;
                    // every one after it takes less, floored so a big cleave still means something
                    // on its fifth target.
                    dmg *= chain;
                    // Cleaving Habit: a basic loses nothing for each body it passes through.
                    if (isFinisher || Effects == null || !Effects.NoChainFalloff)
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
                    bool first = !boardDesignated;
                    if (first) { boardDesignated = true; boardFirstHit = dmg; }
                    Board.OnHitLanded(hp, info, isFinisher, range > 0.01f ? dist / range : 0f, first);
                }

                // Splash spills off the DESIGNATED target only - the first body the swing reached
                // (targets are nearest-first). Off every body in a cleave it would compound.
                if (!splashed && !_echoing)
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
            if (landed && !_echoing && Board != null)
            {
                if (!isFinisher) Board.OnBasicLanded();
                else if (step.AreaOfEffect) Board.OnAreaAttack(origin, range, boardFirstHit);
            }

            // ONE swing, one wear tick. An Echo finisher resolves up to five times - once as the
            // player's own blast and once per shadow - and charging durability for each would
            // make the weapon that summons them the fastest way to destroy itself.
            if (landed && !_echoing) OnWeaponUsed?.Invoke();   // landing a hit is what wears a weapon
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
            else if (step.AreaOfEffect)
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
                // Locked Rotation shuffles the wheel: the next weapon art is any OTHER slot's,
                // drawn at random, instead of the next one in order.
                RotationIndex = Mods.RotationShuffled && Slots.Count > 1
                    ? (RotationIndex + Random.Range(1, Slots.Count)) % Slots.Count
                    : (RotationIndex + 1) % Slots.Count;   // next slot's finisher
                // Golden Chain: every third weapon art comes with no basics before it.
                if (Effects != null && Effects.OnArtCompleted()) RefundFinisher();
            }
            else ComboIndex++;
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
