using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Combat;
using Convergence.Core;
using T2 = Convergence.Core.Tuning.Overhaul;

namespace Convergence.Exchange
{
    /// <summary>
    /// THE OVERHAUL'S RULES AND ITS ARTS (EXCHANGE-OVERHAUL.md, built 2026-11-04).
    ///
    /// THE FOUR DAMPING RULES - general, so no pair needs patching:
    ///   1. THE SHARED BANK (Hasten): every "your next art comes sooner" fills one bank; a chain
    ///      takes at most one basic from it. Unshackled goes through it too.
    ///   2. THE CONTROL BUDGET (Combat.ControlBudget): the ledger's new staggers, roots and held-off
    ///      attacks share a per-body budget, then the body shrugs them off.
    ///   3. THE GENERATION COUNT (_depth): an effect set off by a form's effect sets off others only
    ///      MaxFormDepth deep.
    ///   4. THE DECOY CLOCK: one decoy at a time, with a gap after it.
    ///
    /// THE COAT: what an entry adds in the character's element - Fire burns and feeds heat, Water
    /// soaks and pulls a step, Earth staggers, Air bleeds and throws back - stronger in TIERS as more
    /// coated entries are held. The ART forms: the ART ROLE families and the ART CRAFT forms, all
    /// firing for the art on their own socket.
    /// </summary>
    public partial class RunEffects
    {
        // ================================================================ rule 1: the shared bank

        float _hasteBank;
        int _hastedSerial = -1;

        /// <summary>Some fraction of a basic toward the next art. The bank holds one basic at most and
        /// a chain (the basics between two arts) takes at most one from it.</summary>
        public void Hasten(float basics)
        {
            if (_pc == null || basics <= 0f) return;
            _hasteBank = Mathf.Min(T2.HasteBasicsPerChain, _hasteBank + basics);
            TryHaste();
        }

        void TryHaste()
        {
            if (_pc == null || _hasteBank < 1f || _hastedSerial == _pc.ArtSerial || _pc.FinisherNext) return;
            _hasteBank -= 1f;
            _hastedSerial = _pc.ArtSerial;
            _pc.AdvanceChain();
            LedgerFx.Spark(_pc);
        }

        // ================================================================ rule 2: control

        float Staggers(Health h, float seconds)
        {
            float g = ControlBudget.Grant(h, seconds);
            if (g > 0f) _pc.Stagger(h, g);
            return g;
        }

        void Roots(Health h, float seconds)
        {
            float g = ControlBudget.Grant(h, seconds);
            if (g > 0f) StatusEffects.Get(h.gameObject).ApplyPetrify(g);
        }

        void HoldsOff(Health h, float seconds)
        {
            var ec = h != null ? h.GetComponent<Enemies.EnemyController>() : null;
            if (ec == null) return;
            float g = ControlBudget.Grant(h, seconds);
            if (g > 0f) ec.DenyTurns(g);
        }

        // ================================================================ rule 3: the generation count

        int _depth;

        /// <summary>Runs an effect one generation deeper - not at all past MaxFormDepth.</summary>
        void Deeper(System.Action act)
        {
            if (_depth >= T2.MaxFormDepth) return;
            _depth++;
            try { act(); }
            finally { _depth--; }
        }

        // ================================================================ rule 4: the decoy clock

        float _decoyClear;

        /// <summary>An afterimage at <paramref name="at"/>: the enemies near it hold off their attacks
        /// for a moment (through the control budget). False while another decoy's clock runs.</summary>
        bool Decoy(Vector2 at, float seconds)
        {
            if (_pc == null || Time.time < _decoyClear) return false;
            _decoyClear = Time.time + seconds + T2.DecoyGapSeconds;
            foreach (var hp in EnemiesWithin(at, T2.DecoyRadius)) HoldsOff(hp, seconds);
            TexelFx.Ring(at, LedgerFx.Lilac, 0.2f, 0.6f, seconds, 1.2f, true);
            LedgerFx.Force(at, LedgerFx.Lilac, 0.5f);
            return true;
        }

        // ================================================================ the coat

        float _coatFedAt = -10f;

        /// <summary>Coated entries held, in tiers of CoatTierEvery - each tier strengthens every coat.</summary>
        int CoatTier => _mods == null ? 0 : Mathf.Min(3, _mods.CoatedHeld / T2.CoatTierEvery);

        /// <summary>
        /// THE COAT on one body: the character's element finishes an entry. Strength scales its
        /// measure; the tiers add to it; at the top tier it reaches the nearest other body at half.
        /// <paramref name="centre"/> is where Water draws toward and Air throws from (the player when null).
        /// </summary>
        public void Coat(Health target, float strength, Vector2? centre = null)
        {
            if (_pc == null || target == null || target.IsDead || strength <= 0f) return;
            int tier = CoatTier;
            float s = strength * (1f + T2.CoatTierStep * tier);
            ApplyCoat(target, s, centre ?? (Vector2)_pc.transform.position);
            if (Time.time - _coatFedAt >= T2.CoatFeedEvery)
            {
                _coatFedAt = Time.time;
                Resource?.Refund(T2.CoatMeterFeed * Mathf.Min(1.5f, s));
            }
            if (tier >= 3)
                Deeper(() =>
                {
                    var near = NearestEnemy(target.transform.position, T2.CoatSpreadRadius, target);
                    if (near != null) ApplyCoat(near, s * 0.5f, centre ?? (Vector2)_pc.transform.position);
                });
        }

        void ApplyCoat(Health h, float s, Vector2 centre)
        {
            switch (Element)
            {
                case ElementType.Fire:
                    _pc.Burn(h, T2.CoatBurnShare * _pc.HitUnit * s, T2.CoatBurnSeconds);
                    break;
                case ElementType.Water:
                    _pc.Soak(h, T2.CoatSoakSeconds * s, Tuning.Elements.Water.SoakVulnerability);
                    DragToward.Pull(h, centre, T2.CoatPull * s);
                    break;
                case ElementType.Earth:
                    Staggers(h, T2.CoatStaggerSeconds * s);
                    break;
                default:
                    _pc.Bleed(h, T2.CoatBleedShare * _pc.HitUnit * s, T2.CoatBleedSeconds);
                    DragToward.Push(h, centre, T2.CoatPush * s);
                    break;
            }
        }

        Color32[] CoatRamp => Element switch
        {
            ElementType.Fire => TexelFx.Fire, ElementType.Water => LedgerFx.Water,
            ElementType.Earth => TexelFx.Stone, _ => LedgerFx.Blood,
        };

        // ================================================================ the art's life

        // The art now playing: where it began, what it struck, what it stopped.
        int _artSerialSeen = -1, _artSocketNow = -1;
        Vector2 _artStartPos;
        float _artStartedAt = -10f, _lastArtEndedSeen = -10f;
        readonly List<Health> _artStruck = new();
        int _artStopped;
        bool _artKilled;
        ArtRole _artRoles, _prevRoles;
        int _prevStopped;
        string _artId;
        // The rotation's memory: when each socket last fired (Full Turn), and the art after this one's gifts.
        readonly Dictionary<int, float> _socketFiredAt = new();
        int _coatArtSerial = -1;
        float _coatArtStrength;
        readonly Dictionary<Health, (int Serial, float Share, string Id)> _primed = new();
        float _arrivalUntil, _arrivalCrit;
        int _quickBasics;
        float _quickPoints;
        float _strideUntil;

        bool Fires(string id)
        {
            if (!OnThisSocket(id)) return false;
            var e = ExchangeCatalog.Get(id);
            return e == null || _mods.SocketFits(e, _pc.ArtSocket);
        }

        /// <summary>A weapon art was dispatched (PlayerController.Dispatch, after its socket is set).</summary>
        public void OnArtStarted()
        {
            if (_mods == null || _pc == null) return;
            _prevRoles = _artRoles;
            _prevStopped = _artStopped;
            _artSerialSeen = _pc.ArtSerial;
            _artSocketNow = _pc.ArtSocket;
            _artStartPos = _pc.transform.position;
            _artStartedAt = Time.time;
            _artStruck.Clear();
            _artStopped = 0;
            _artKilled = false;
            var art = _pc.ActiveMoveset;
            _artId = art?.Id;
            _artRoles = ArtRoles.Of(art);
            _socketFiredAt[_artSocketNow] = Time.time;
            CursedArtFired();   // a socket curse at its Nigredo

            // STEP (a Light art): a step toward its target first; a bow steps away.
            if (Fires("ac_step") && _pc.LastArtWeight == FinisherWeight.Light)
                StepFrom(_pc.Weapon == WeaponClass.Bow ? -1f : 1f, T2.StepDistance * Strength("ac_step"), T2.StepSeconds);
            // RECOIL STEP (bows): a stride clear of the nearest enemy.
            if (Fires("ac_recoil_step")) StepFrom(-1f, T2.RecoilStepDistance * Strength("ac_recoil_step"), T2.StepSeconds);
            // DECOY (a reposition art): an afterimage where you left.
            if (Fires("ar_decoy")) Decoy(_artStartPos, T2.DecoySeconds * Strength("ar_decoy"));
            // FULL TURN: every socket fired within the window - this art coats what it strikes.
            if (Fires("ac_full_turn") && WheelTurnedWithin(T2.FullTurnSeconds))
            {
                _coatArtSerial = _pc.ArtSerial;
                _coatArtStrength = Mathf.Max(_coatArtStrength, Strength("ac_full_turn"));
            }
        }

        bool WheelTurnedWithin(float seconds)
        {
            int n = _pc.Slots.Count;
            if (n < 2) return false;
            for (int i = 0; i < n; i++)
                if (!_socketFiredAt.TryGetValue(i, out var at) || Time.time - at > seconds) return false;
            return true;
        }

        /// <summary>A short step toward (+1) or away from (-1) the target, or the nearest enemy.</summary>
        void StepFrom(float sign, float distance, float seconds)
        {
            var t = _pc.Target ?? NearestEnemy(_pc.transform.position, 8f, null);
            Vector2 dir = t != null ? (Vector2)(t.transform.position - _pc.transform.position) : _pc.Facing;
            if (dir.sqrMagnitude < 0.0001f) dir = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing : Vector2.down;
            _pc.Dash(dir.normalized * sign, distance / seconds, seconds);
        }

        /// <summary>The art has ended (its lock over, its bar closed) - read off ArtEndedAt.</summary>
        void OnArtEnded()
        {
            if (_mods == null || _pc == null || _artSocketNow < 0) return;
            int socket = _artSocketNow;
            bool On(string id) => Has(id) && _mods.SocketOf(id) == socket;
            float St(string id) => Strength(id);
            var weight = _pc.LastArtWeight;

            if (On("ar_shockfront"))
            {
                float s = St("ar_shockfront");
                foreach (var hp in EnemiesWithin(_pc.transform.position, T2.ShockfrontRadius))
                {
                    DragToward.Push(hp, _pc.transform.position, T2.ShockfrontPush * s);
                    Staggers(hp, T2.ShockfrontStagger * s);
                }
                LedgerFx.ForceRound(_pc, LedgerFx.Ward, T2.ShockfrontRadius);
            }
            if (On("ar_quickfuse")) Hasten(T2.QuickFuseHaste * Mathf.Min(1f, St("ar_quickfuse")));
            if (On("ar_reel") && _artStruck.Count >= T2.ReelBodies) Hasten(T2.ReelHaste * St("ar_reel"));
            if (On("ar_harvest") && _artStruck.Count >= T2.HarvestBodies) Hasten(T2.HarvestHaste * Mathf.Min(1f, St("ar_harvest")));
            if (On("ac_rebound") && weight == FinisherWeight.Medium && _artStruck.Count >= T2.ReboundBodies) Hasten(T2.ReboundHaste * Mathf.Min(1f, St("ac_rebound")));
            if (On("ac_settle") && weight == FinisherWeight.Heavy && Random.value < T2.SettleChance * St("ac_settle")) Hasten(1f);
            if (On("ac_stride") && weight == FinisherWeight.Medium) _strideUntil = Time.time + T2.StrideSeconds * St("ac_stride");
            if (On("ac_quicken") && weight == FinisherWeight.Light) { _quickBasics = Mathf.Max(_quickBasics, 1); _quickPoints = Mathf.Max(_quickPoints, T2.QuickenSpeed * St("ac_quicken")); }
            if (On("ac_leap") && weight == FinisherWeight.Heavy) StepFrom(-1f, T2.LeapDistance * St("ac_leap"), T2.LeapSeconds);
            if (On("ar_arrival"))
            {
                _arrivalUntil = Time.time + T2.ArrivalWindow;
                _arrivalCrit = T2.ArrivalCrit * St("ar_arrival");
            }
            if (On("ar_slipstream"))
            {
                float dmg = T2.SlipstreamHitUnits * _pc.HitUnit * St("ar_slipstream");
                Vector2 to = _pc.transform.position;
                foreach (var hp in EnemiesNearSegment(_artStartPos, to, T2.SlipstreamHalfWidth))
                    hp.Take(new DamageInfo(dmg, Element, _pc.gameObject) { Entry = "ar_slipstream" });
                LedgerFx.Crest(_artStartPos, (to - _artStartPos).normalized, (to - _artStartPos).magnitude, CoatRamp);
            }
            if (On("ar_breathing")) StartCoroutine(BreathingRoom(St("ar_breathing")));
            // RELAY: the art after this one coats what it strikes.
            if (On("ac_relay")) { _coatArtSerial = _pc.ArtSerial + 1; _coatArtStrength = St("ac_relay"); }
            RestArtEnded(socket);   // Ambush, Slipcoat, Wake Rings
            _strainBank = 0f;   // Coiled Guard's bank is spent on this art
            _artSocketNow = -1;
        }

        IEnumerator BreathingRoom(float s)
        {
            yield return new WaitForSeconds(T2.BreathingDelay);
            if (_pc == null || EnemiesWithin(_pc.transform.position, T2.BreathingRadius).Count > 0) yield break;
            _quickBasics = Mathf.Max(_quickBasics, T2.BreathingBasics);
            _quickPoints = Mathf.Max(_quickPoints, T2.BreathingSpeed * s);
            LedgerFx.Spark(_pc);
        }

        /// <summary>A ward art stopped something (Riposte's guard, Revolve's ring, Palisade's arrows):
        /// RETURN sends it back, STEADY banks toward the next art, READER remembers it.</summary>
        public void OnWardStopped(Vector2 at)
        {
            if (_mods == null || _pc == null) return;
            if ((_artRoles & ArtRole.Ward) == 0 || Time.time - _artStartedAt > 6f) return;
            int socket = _artSocketNow >= 0 ? _artSocketNow : _pc.ArtSocket;
            bool On(string id) => Has(id) && _mods.SocketOf(id) == socket;
            _artStopped++;
            RestWardStopped(at, socket);   // Charge, Taint
            if (On("ar_return"))
            {
                var attacker = NearestEnemy(at, 6f, null);
                if (attacker != null)
                {
                    float s = Strength("ar_return");
                    attacker.Take(new DamageInfo(T2.ReturnHitUnits * _pc.HitUnit * s, ElementType.Earth, _pc.gameObject) { Entry = "ar_return" });
                    Staggers(attacker, T2.ReturnStagger * s);
                    LedgerFx.Echo(attacker.transform.position, at);
                }
            }
            if (On("ar_steady")) Hasten(T2.SteadyHastePerStop * Strength("ar_steady"));
        }

        // ================================================================ art hits

        /// <summary>Every hit a weapon art lands (dead or alive), on its own socket's forms.</summary>
        void ArtHit(Health target, DamageInfo info, in HitContext h)
        {
            if (_artSerialSeen != _pc.ArtSerial) OnArtStarted();   // a path that skipped Dispatch's hook
            bool fresh = !_artStruck.Contains(target);
            if (fresh) _artStruck.Add(target);
            bool dead = target.IsDead;
            Vector2 at = target.transform.position;
            Vector2 me = _pc.transform.position;

            // ---- the coat, from the forms that give it to an art ----
            float coat = 0f;
            if (_coatArtSerial == _pc.ArtSerial) coat = Mathf.Max(coat, _coatArtStrength);
            if (Fires("ac_steady_hand") && _perfectArt && _pc.PerfectStreak >= T2.SteadyHandStreak) coat = Mathf.Max(coat, Strength("ac_steady_hand"));
            if (Fires("ac_charged_disc") && h.Thrown) coat = Mathf.Max(coat, Strength("ac_charged_disc"));
            if (Fires("ac_tipped") && h.Thrown) coat = Mathf.Max(coat, Strength("ac_tipped"));
            if (coat > 0f && !dead) Coat(target, coat);
            if (Fires("ac_brand_blade") && fresh)
            {
                float s = Strength("ac_brand_blade");
                Field("ac_brand_blade", target, at, T2.BrandBladeRadius, Element, T2.BrandBladeSeconds * s, s);
            }

            // ---- impact ----
            if (fresh && Fires("ar_collide") && _artStruck.Count == 1) StartCoroutine(Collide(Strength("ar_collide")));
            if (fresh && Fires("ar_bowl") && !dead) StartCoroutine(Bowl(target, Strength("ar_bowl")));
            if (dead && h.First && Fires("ar_carry") && !_artKilled)
            {
                _artKilled = true;
                var next = NearestEnemy(at, T2.CarryReach, target);
                if (next != null)
                {
                    next.Take(new DamageInfo(info.Amount * T2.CarryShare * Strength("ar_carry"), info.Element, _pc.gameObject) { Entry = "ar_carry", IsFinisher = true });
                    LedgerFx.Echo(next.transform.position, at);
                }
            }
            if (fresh && Fires("ar_link") && !dead) LinkedBodies.Link(target, _pc.ArtSerial, T2.LinkSeconds, T2.LinkShare * Strength("ar_link"), _pc.gameObject);
            if (fresh && Fires("ar_spread"))
            {
                var near = NearestEnemy(at, T2.SpreadReach, target);
                if (near != null && !_artStruck.Contains(near))
                    near.Take(new DamageInfo(info.Amount * T2.SpreadShare * Strength("ar_spread"), info.Element, _pc.gameObject) { Entry = "ar_spread" });
            }

            // ---- control ----
            if (fresh && Fires("ar_held") && !dead) StartCoroutine(Later(T2.HeldDelay, () => Staggers(target, T2.HeldStagger * Strength("ar_held"))));
            if (fresh && Fires("ar_slam") && !dead) StartCoroutine(Slam(target, Strength("ar_slam")));
            if (h.First && Fires("ar_daze") && !dead) Staggers(target, T2.DazeStagger * Strength("ar_daze"));
            if (fresh && Fires("ar_mire") && !dead) StatusEffects.Get(target.gameObject).ApplySlow(T2.MireSeconds * Strength("ar_mire"), T2.MireSlow);
            if (fresh && Fires("ar_snare") && !dead) Roots(target, T2.SnareRoot * Strength("ar_snare"));

            // ---- tempo ----
            if (dead && h.First && Fires("ar_cull")) Hasten(T2.CullHaste * Mathf.Min(1f, Strength("ar_cull")));

            // ---- force (weight trios) ----
            var weight = _pc.LastArtWeight;
            if (h.First && fresh && Fires("ac_flick") && weight == FinisherWeight.Light && !dead) DragToward.Push(target, me, T2.FlickPush * Strength("ac_flick") * PushMul);
            if (fresh && Fires("ac_shove") && weight == FinisherWeight.Medium && !dead) DragToward.Push(target, me, T2.ShovePush * Strength("ac_shove") * PushMul);
            if (h.First && fresh && Fires("ac_gravity") && weight == FinisherWeight.Heavy)
                foreach (var hp in EnemiesWithin(at, T2.GravityRadius))
                    if (hp != target) DragToward.Pull(hp, at, T2.GravityPull * Strength("ac_gravity"));

            // ---- the timing bar ----
            if (h.First && fresh && _perfectArt && Fires("ac_flare"))
            {
                float s = Strength("ac_flare");
                Vector2 dir = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.right;
                foreach (var hp in EnemiesInStrip(me, dir, T2.FlareLength, T2.FlareHalfWidth))
                    if (hp != target) hp.Take(new DamageInfo(T2.FlareHitUnits * _pc.HitUnit * s, Element, _pc.gameObject) { Entry = "ac_flare" });
                LedgerFx.Crest(ReleaseCanvas.Feet(_pc.transform), dir, T2.FlareLength, CoatRamp);
            }

            // ---- the rotation: PRIMER marks for the next art ----
            if (fresh && Fires("ac_primer") && !dead) _primed[target] = (_pc.ArtSerial, T2.PrimerShare * Strength("ac_primer"), "ac_primer");

            // ---- the classes ----
            if (h.First && fresh && Fires("ac_seam") && _artStruck.Count == 1) StartCoroutine(Seam(Strength("ac_seam")));
            if (h.First && fresh && Fires("ac_skewer") && !dead)
            {
                var into = NearestEnemy(at, T2.SkewerReach, target);
                if (into != null) StartCoroutine(Skewer(target, into, Strength("ac_skewer")));
            }
            if (fresh && Fires("ac_inward_orbit") && !dead) DragToward.Pull(target, me, T2.InwardOrbitPull * Strength("ac_inward_orbit"));
            if (h.Thrown && Fires("ac_art_splinter"))
            {
                var near = NearestEnemy(at, T2.ArtSplinterReach, target);
                if (near != null) near.Take(new DamageInfo(info.Amount * T2.ArtSplinterShare * Strength("ac_art_splinter"), info.Element, _pc.gameObject) { Entry = "ac_art_splinter" });
            }
            if (h.Thrown && Fires("ac_barbed") && !dead)
                _pc.Bleed(target, info.Amount * T2.BarbedBleedShare * Strength("ac_barbed"), T2.BarbedBleedSeconds);
            if (h.First && fresh && Fires("ac_kneeling_shot"))
            {
                var near = NearestEnemy(at, T2.KneelingReach, target);
                if (near != null)
                {
                    near.Take(new DamageInfo(T2.KneelingHitUnits * _pc.HitUnit * Strength("ac_kneeling_shot"), info.Element, _pc.gameObject) { Entry = "ac_kneeling_shot" });
                    LedgerFx.Echo(near.transform.position, me);
                }
            }

            RestArtHit(target, info, h, fresh, dead);   // Payoff, Contagion, Crowd, the craft skeletons

            // ---- the socket curses' Albedos: inscribed on the socket the curse sat on ----
            int sock = _pc.ArtSocket;
            if (fresh && !dead && Has("al_brittle_script") && _mods.SocketOf("al_brittle_script") == sock)
                Staggers(target, T2.BrittleScriptStagger * _mods.Potency("al_brittle_script"));
            if (Has("al_dulled_script") && _mods.SocketOf("al_dulled_script") == sock)
            {
                var near = NearestEnemy(at, T2.DulledScriptReach, target);
                if (near != null) near.Take(new DamageInfo(info.Amount * T2.DulledScriptSpread, info.Element, _pc.gameObject) { Entry = "al_dulled_script" });
            }
        }

        /// <summary>BULWARK ART (Heavy Hand's Albedo): blows taken while its art locks you are smaller.</summary>
        public float LockedBlowMul => Has("al_heavy_hand") && _pc != null && _pc.AttackLocked
                                      && _pc.ArtSocket == _mods.SocketOf("al_heavy_hand") ? T2.HeavyHandBlowMul : 1f;

        float PushMul => Has("al_swift") ? T2.SwiftPushMul : 1f;

        IEnumerator Later(float seconds, System.Action act)
        {
            yield return new WaitForSeconds(seconds);
            if (_pc != null) act();
        }

        IEnumerator Collide(float s)
        {
            yield return new WaitForSeconds(T2.CollideDelay);
            if (_pc == null) yield break;
            var hit = new HashSet<Health>();
            var list = new List<Health>(_artStruck);
            float dmg = T2.CollideHitUnits * _pc.HitUnit * s;
            for (int i = 0; i < list.Count; i++)
                for (int j = i + 1; j < list.Count; j++)
                {
                    var a = list[i]; var b = list[j];
                    if (a == null || b == null || a.IsDead || b.IsDead) continue;
                    if (((Vector2)a.transform.position - (Vector2)b.transform.position).sqrMagnitude > T2.CollideReach * T2.CollideReach) continue;
                    if (hit.Add(a)) a.Take(new DamageInfo(dmg, ElementType.Earth, _pc.gameObject) { Entry = "ar_collide" });
                    if (hit.Add(b)) b.Take(new DamageInfo(dmg, ElementType.Earth, _pc.gameObject) { Entry = "ar_collide" });
                    TexelFx.Star(((Vector2)a.transform.position + (Vector2)b.transform.position) * 0.5f, TexelFx.Steel, 3);
                }
        }

        IEnumerator Bowl(Health body, float s)
        {
            var hit = new HashSet<Health> { body };
            float dmg = T2.BowlHitUnits * _pc.HitUnit * s;
            for (float t = 0f; t < T2.BowlDelay * 2f; t += 0.05f)
            {
                yield return new WaitForSeconds(0.05f);
                if (_pc == null || body == null || body.IsDead) yield break;
                foreach (var hp in EnemiesWithin(body.transform.position, T2.BowlReach))
                    if (hit.Add(hp)) { hp.Take(new DamageInfo(dmg, ElementType.Earth, _pc.gameObject) { Entry = "ar_bowl" }); TexelFx.Star(hp.transform.position, TexelFx.Steel, 3); }
            }
        }

        IEnumerator Slam(Health body, float s)
        {
            Vector2 from = _pc.transform.position;
            yield return new WaitForSeconds(T2.SlamDelay);
            if (_pc == null || body == null || body.IsDead) yield break;
            if (!Slams(body, from)) yield break;
            body.Take(new DamageInfo(T2.SlamHitUnits * _pc.HitUnit * s, ElementType.Earth, _pc.gameObject) { Entry = "ar_slam" });
            Staggers(body, T2.SlamStagger * s);
            TexelFx.Star(body.transform.position, TexelFx.Steel, 4);
        }

        /// <summary>A moved body meets something - a wall on its far side, or another body close by.</summary>
        static bool Slams(Health body, Vector2 from)
        {
            Vector2 p = body.transform.position;
            Vector2 away = p - from;
            if (away.sqrMagnitude > 0.0001f && Arena.WallBetween(p, p + away.normalized * T2.SlamReach)) return true;
            return NearestEnemy(p, T2.SlamReach, body) != null;
        }

        IEnumerator Seam(float s)
        {
            Vector2 from = _pc.transform.position;
            Vector2 dir = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.right;
            for (int i = 1; i <= 3; i++)
                Visual(GroundCracks.Spawn(Arena.NearestFloor(from + dir * (T2.SeamLength * i / 3f), 0.3f))?.gameObject);
            yield return new WaitForSeconds(T2.SeamDelay);
            if (_pc == null) yield break;
            float dmg = T2.SeamHitUnits * _pc.HitUnit * s;
            foreach (var hp in EnemiesInStrip(from, dir, T2.SeamLength, T2.SeamHalfWidth))
                hp.Take(new DamageInfo(dmg, ElementType.Earth, _pc.gameObject) { Entry = "ac_seam" });
        }

        IEnumerator Skewer(Health body, Health into, float s)
        {
            DragToward.Pull(body, into.transform.position, Vector2.Distance(body.transform.position, into.transform.position));
            yield return new WaitForSeconds(0.2f);
            if (_pc == null) yield break;
            float dmg = T2.SkewerHitUnits * _pc.HitUnit * s;
            if (body != null && !body.IsDead) body.Take(new DamageInfo(dmg, ElementType.Earth, _pc.gameObject) { Entry = "ac_skewer" });
            if (into != null && !into.IsDead) into.Take(new DamageInfo(dmg, ElementType.Earth, _pc.gameObject) { Entry = "ac_skewer" });
            if (into != null) TexelFx.Star(into.transform.position, TexelFx.Steel, 4);
        }

        // ================================================================ what the art's forms add to hits

        /// <summary>The overhaul's conditional damage on one hit (Primer, Reader, Full Turn, the Albedos).</summary>
        float OverhaulOutgoing(Health target, float damage, in HitContext h)
        {
            if (_pc == null) return damage;
            damage = RestOutgoing(target, damage, h);   // Ambush, Charge, Gather, the Fortune Ambush
            if (h.IsFinisher)
            {
                if (_primed.TryGetValue(target, out var p) && p.Serial == _pc.ArtSerial - 1) damage = Boost(p.Id, damage, 1f + p.Share);
                if (Fires("ac_reader"))
                {
                    float s = Strength("ac_reader");
                    float d = Vector2.Distance(_pc.transform.position, target.transform.position);
                    if ((_prevRoles & ArtRole.Pull) != 0 && d <= T2.ReaderNearRadius) damage = Boost("ac_reader", damage, 1f + T2.ReaderShare * s);
                    else if ((_prevRoles & ArtRole.Push) != 0 && d > T2.ReaderNearRadius) damage = Boost("ac_reader", damage, 1f + T2.ReaderShare * s);
                    if ((_prevRoles & ArtRole.Ward) != 0) damage = Boost("ac_reader", damage, 1f + T2.ReaderPerStop * _prevStopped * s);
                }
                if (Fires("ac_full_turn") && _coatArtSerial == _pc.ArtSerial) damage = Boost("ac_full_turn", damage, 1f + T2.FullTurnShare * Strength("ac_full_turn"));
                if (Has("al_overdrawn") && !_perfectArt) damage = Boost("al_overdrawn", damage, 1f + T2.OverdrawnArt * _mods.Potency("al_overdrawn"));
                if (Has("al_strain") && _strainBank > 0f) { damage = Boost("al_strain", damage, 1f + _strainBank); }
            }
            else
            {
                if (Has("al_slow_link") && IsConnectorSwing) damage = Boost("al_slow_link", damage, 1f + T2.SlowLinkArt * _mods.Potency("al_slow_link"));
                if (_freeArrow && h.Thrown && h.First && _pc.Weapon == WeaponClass.Bow) { damage = Boost("cc_bow_parry_b", damage, 1f + ConcordPower("cc_bow_parry_b")); _freeArrow = false; }
            }
            damage = WorldTwoOutgoing(target, damage);   // Lantern
            if (Has("al_crowded"))
            {
                Enemies.EnemyRegistry.Prune();
                int bodies = Enemies.EnemyRegistry.All.Count;
                damage = Boost("al_crowded", damage, 1f + Mathf.Min(T2.CrowdedCap, T2.CrowdedPerBody * Mathf.Max(0f, bodies - T2.CrowdedFrom)) * _mods.Potency("al_crowded"));
            }
            return damage;
        }

        /// <summary>A conditional multiplier on one hit, its extra credited to its entry on the
        /// ledger's meter (before the target's own mitigation - the meter's estimate).</summary>
        float Boost(string id, float damage, float mul)
        {
            LedgerMeter.Added(id, damage * (mul - 1f));
            return damage * mul;
        }

        /// <summary>Damage POINTS the overhaul adds to one hit - joined to the ledger's own before the
        /// Vessel bends them (Second Wind of War, Fatigue's Albedo).</summary>
        float OverhaulDamagePoints()
        {
            if (!Has("al_fatigue")) return 0f;
            float over = _floorSeconds - T2.FatigueAfter;
            return over <= 0f ? 0f : Mathf.Min(T2.SecondWindDamageCap, T2.SecondWindDamage * Mathf.Floor(over / T2.FatigueEvery))
                                     * _mods.Potency("al_fatigue");
        }

        /// <summary>Crit chance the overhaul's forms add to one hit, into the one pool.</summary>
        float OverhaulCrit(Health target, bool isFinisher)
        {
            float add = 0f;
            if (!isFinisher && Time.time < _arrivalUntil) { add += _arrivalCrit; _arrivalUntil = 0f; }
            if (!isFinisher && Time.time < _landingUntil) { add += T2.LandingCrit * Strength("gd_landing"); _landingUntil = 0f; }
            if (_readMarks.TryGetValue(target, out var left) && left > 0)
            {
                add += T2.ReadCrit * Strength("gd_read");
                _readMarks[target] = left - 1;
            }
            if (isFinisher && Fires("ac_reader") && (_prevRoles & ArtRole.Reposition) != 0) add += T2.ReaderCrit * Strength("ac_reader");
            return add;
        }

        /// <summary>GILT: a PERFECT on its socket fires that socket's forms at full strength.</summary>
        float Gilded(string id, float strength)
        {
            if (strength >= 1f || !_perfectArt || _mods == null || _pc == null || !Has("ac_gilt")) return strength;
            int giltOn = _mods.SocketOf("ac_gilt");
            if (giltOn != _pc.ArtSocket || _mods.SocketOf(id) != giltOn) return strength;
            return Mathf.Max(1f, strength);
        }

        // ================================================================ the art side of the tick

        void TickArts()
        {
            if (_pc == null) return;
            if (_pc.ArtEndedAt > _lastArtEndedSeen + 1e-4f)
            {
                _lastArtEndedSeen = _pc.ArtEndedAt;
                OnArtEnded();
            }
            TryHaste();
            if (_primed.Count > 48)
                foreach (var k in new List<Health>(_primed.Keys)) if (k == null || _primed[k].Serial < _pc.ArtSerial - 1) _primed.Remove(k);
        }

        /// <summary>Attack Speed points the overhaul's conditions add right now (Quicken, Breathing Room).</summary>
        public float AttackSpeedUp => _quickBasics > 0 ? _quickPoints : 0f;

        /// <summary>Move Speed points the overhaul adds (Stride) - folded into MoveSpeedUp.</summary>
        float OverhaulMoveUp => Time.time < _strideUntil ? T2.StrideMove : 0f;
    }
}
