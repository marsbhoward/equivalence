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
    /// THE REST OF THE PLAN (2026-11-04, the user's "finish what was left out"): the PAYOFF and
    /// CONTAGION role families, the CROWD weight trio, the class x element skeletons for the release,
    /// the chain and the arts, Fortune's three, and the costs Loose Catch, Dull Link and Spill.
    /// </summary>
    public partial class RunEffects
    {
        float _ambushUntil, _ambushShare;
        int _ambushSerial = -1;
        float _chargeBonus;
        int _chargeSerial = -1;
        float _seededOrbitUntil, _stakesUntil;
        Vector2 _stakesAt;
        readonly HashSet<Health> _stakesStruck = new();
        readonly HashSet<Health> _echoed = new();
        float _restTick;
        float _connectorSwingAt = -10f;
        Health _lastArtStruck;

        // ---------------------------------------------------------------- priming the next art

        void PrimeNext(Health target, float share, string id)
        {
            if (target == null || target.IsDead) return;
            if (_primed.TryGetValue(target, out var p) && p.Serial == _pc.ArtSerial) share = Mathf.Max(share, p.Share);
            _primed[target] = (_pc.ArtSerial, share, id);
        }

        // ---------------------------------------------------------------- art hits

        void RestArtHit(Health target, DamageInfo info, in HitContext h, bool fresh, bool dead)
        {
            Vector2 at = target.transform.position, me = _pc.transform.position;
            _lastArtStruck = target;
            var weight = _pc.LastArtWeight;

            // ---- Payoff ----
            if (fresh && Fires("ar_drawn_in")) PrimeNext(target, T2.DrawnInShare * Strength("ar_drawn_in"), "ar_drawn_in");
            if (fresh && Fires("ar_flank") && !dead) StartCoroutine(Flank(target, Strength("ar_flank")));
            if (h.First && fresh && Fires("ar_expose")) PrimeNext(target, T2.ExposeShare * Strength("ar_expose"), "ar_expose");
            if (fresh && Fires("ar_deepen")) PrimeNext(target, T2.DeepenShare * Strength("ar_deepen"), "ar_deepen");

            // ---- Contagion ----
            if (fresh && !dead && Fires("ar_undertow"))
            {
                float s = Strength("ar_undertow");
                Coat(target, s, me);
                var near = NearestEnemy(at, 1.5f, target);
                if (near != null) Coat(near, s, me);
            }
            if (fresh && !dead && Fires("ar_wake")) StartCoroutine(Later(0.3f, () =>
            {
                if (target != null) Field("ar_wake", target, target.transform.position, 0.8f, Element, 2f * Strength("ar_wake"), Strength("ar_wake"));
            }));
            if (h.First && fresh && Fires("ar_transfuse")) Transfuse(target);
            if (fresh && Fires("ar_wash"))
            {
                var near = NearestEnemy(at, 2f, target);
                if (near != null && !_artStruck.Contains(near)) Coat(near, Strength("ar_wash"));
            }
            if (fresh && !dead && Fires("ar_prime")) Coat(target, Strength("ar_prime"));

            // ---- Crowd (a weight trio) ----
            if (h.First && fresh && weight == FinisherWeight.Light && Fires("ac_spark"))
            {
                var near = NearestEnemy(at, T2.SparkReach, target);
                if (near != null) { near.Take(new DamageInfo(info.Amount * T2.SparkShare * Strength("ac_spark"), info.Element, _pc.gameObject) { Entry = "ac_spark" }); LedgerFx.Echo(near.transform.position, at); }
            }
            if (fresh && weight == FinisherWeight.Medium && Fires("ac_fan"))
            {
                var near = NearestEnemy(at, T2.FanReach, target);
                if (near != null) near.Take(new DamageInfo(info.Amount * T2.FanShare * Strength("ac_fan"), info.Element, _pc.gameObject) { Entry = "ac_fan" });
            }
            if (h.First && fresh && weight == FinisherWeight.Heavy && Fires("ac_debris"))
            {
                foreach (var hp in EnemiesWithin(me, T2.DebrisRadius)) DragToward.Push(hp, me, T2.DebrisPush * Strength("ac_debris") * PushMul);
                Combat.TexelFx.Rubble(me, Combat.TexelFx.RubbleOf(false), 0.4f, 8, T2.DebrisRadius * 0.6f);
            }

            // ---- class x element (art craft) ----
            if (h.First && fresh && !dead && Fires("ac_tempered_edge")) Coat(target, 2f * Strength("ac_tempered_edge"));
            if (h.Thrown && Fires("ac_trailing_fletch"))
                Field("ac_trailing_fletch", target, at, 0.7f, Element, 1.5f * Strength("ac_trailing_fletch"), Strength("ac_trailing_fletch"));

            // ---- Fortune ----
            SpireEcho(target, dead);
        }

        IEnumerator Flank(Health body, float s)
        {
            Vector2 from = _pc.transform.position;
            yield return new WaitForSeconds(T2.SlamDelay);
            if (_pc == null || body == null || body.IsDead || !Slams(body, from)) yield break;
            StatusEffects.Get(body.gameObject).ApplyExposed(T2.FlankSeconds, 1f + T2.FlankExposed * s);
            LedgerFx.StatusBurst(body, TexelFx.Steel);
        }

        /// <summary>TRANSFUSE: the main target's burn, soak and bleed passed to the nearest enemy.</summary>
        void Transfuse(Health target)
        {
            var st = target.GetComponent<StatusEffects>();
            var near = NearestEnemy(target.transform.position, 3f, target);
            if (st == null || near == null) return;
            float s = Strength("ar_transfuse");
            if (st.Burning) _pc.Burn(near, st.BurnDps * s, 3f);
            if (st.Soaked) _pc.Soak(near, 2.5f * s, Tuning.Elements.Water.SoakVulnerability);
            if (st.Bleeding) _pc.Bleed(near, 0.3f * _pc.HitUnit * s, 3f);
            LedgerFx.Echo(near.transform.position, target.transform.position);
        }

        // ---------------------------------------------------------------- the art's end, and what it adds to hits

        void RestArtEnded(int socket)
        {
            bool On(string id) => Has(id) && _mods.SocketOf(id) == socket;
            if (On("ar_ambush")) { _ambushUntil = Time.time + T2.AmbushWindow; _ambushShare = T2.AmbushShare * Strength("ar_ambush"); _ambushSerial = _pc.ArtSerial; }
            if (On("ar_slipcoat"))
                foreach (var hp in EnemiesNearSegment(_artStartPos, _pc.transform.position, 1f)) Coat(hp, Strength("ar_slipcoat"));
            if (On("ac_wake_rings") && _lastArtStruck != null)
                Field("ac_wake_rings", null, _lastArtStruck.transform.position, 1f, Element, 2f * Strength("ac_wake_rings"), Strength("ac_wake_rings"));
            _lastArtStruck = null;
        }

        float RestOutgoing(Health target, float damage, in HitContext h)
        {
            if (h.IsFinisher)
            {
                if (Time.time < _ambushUntil && _pc.ArtSerial > _ambushSerial) damage = Boost("ar_ambush", damage, 1f + _ambushShare);
                if (_chargeSerial == _pc.ArtSerial && _chargeBonus > 0f) damage = Boost("ar_charge", damage, 1f + _chargeBonus);
                if (Fires("ar_gather") && _artStruck.Count > 2) damage = Boost("ar_gather", damage, 1f + T2.GatherPerBody * (_artStruck.Count - 2) * Strength("ar_gather"));
            }
            if (Has("fo_ambush") && target.GetComponent<Enemies.EnemyController>() is { Unaware: true }) damage = Boost("fo_ambush", damage, 1f + T2.AmbushTaken);
            return damage;
        }

        /// <summary>A ward art stopped a hit - CHARGE and TAINT (called from OnWardStopped).</summary>
        void RestWardStopped(Vector2 at, int socket)
        {
            if (Has("ar_charge") && _mods.SocketOf("ar_charge") == socket)
            {
                if (_chargeSerial != _pc.ArtSerial) { _chargeSerial = _pc.ArtSerial; _chargeBonus = 0f; }
                _chargeBonus += T2.ChargePerStop * Strength("ar_charge");
            }
            if (Has("ar_taint") && _mods.SocketOf("ar_taint") == socket)
            {
                var attacker = NearestEnemy(at, 6f, null);
                if (attacker != null) Coat(attacker, Strength("ar_taint"));
            }
        }

        // ---------------------------------------------------------------- basics

        void RestBasicHit(Health target, DamageInfo info, in HitContext h)
        {
            bool dead = target.IsDead;
            if (Has("ch_coated_opener") && _pc.Weapon == WeaponClass.Greatsword && !h.Thrown && _swingChainIndex == 0 && !dead)
                Coat(target, Strength("ch_coated_opener"));
            if (Has("ch_coated_kill") && _pc.Weapon == WeaponClass.Bow && h.Thrown && dead)
            {
                var near = NearestEnemy(target.transform.position, 3f, target);
                if (near != null) Coat(near, Strength("ch_coated_kill"));
            }
            // DEEP LINK (Dull Link's Albedo): the connector renews what its target already carries.
            if (Has("al_dull_link") && IsConnectorSwing && !dead)
            {
                var st = target.GetComponent<StatusEffects>();
                if (st != null && st.Burning) _pc.Burn(target, st.BurnDps, 3f);
                if (st != null && st.Soaked) _pc.Soak(target, 2.5f, Tuning.Elements.Water.SoakVulnerability);
            }
            SpireEcho(target, dead);
        }

        /// <summary>DULL LINK (a socket cost): for a moment as the connector swings, no status lands.</summary>
        public bool StatusesBlocked => Has("co_dull_link") && Time.time - _connectorSwingAt < 0.5f;

        void RestOnSwing()
        {
            if (IsConnectorSwing) _connectorSwingAt = Time.time;
        }

        /// <summary>SPIRE ECHO (Fortune): while a spire's boon holds, a first hit on each enemy coats it.</summary>
        void SpireEcho(Health target, bool dead)
        {
            if (dead || !Has("fo_spire_echo") || _mods == null || !_mods.HasFloorBoon || !_echoed.Add(target)) return;
            Coat(target, T2.Measure("fo_spire_echo"));
        }

        // ---------------------------------------------------------------- catches

        /// <summary>A disc came home - Loose Catch, Edge Catch and Coated Catch.</summary>
        void RestCaught()
        {
            if (_pc == null) return;
            int loose = N("co_loose_catch");
            if (loose > 0 && Random.value < T2.LooseCatchChance * (1f + 0.5f * (loose - 1)) * (AtMax("co_loose_catch") ? 2f : 1f))
                _pc.Stun(T2.LooseCatchStun);
            if (Has("al_loose_catch") && Random.value < T2.LooseCatchChance)
            {
                foreach (var hp in EnemiesWithin(_pc.transform.position, T2.LooseCatchRadius))
                    hp.Take(new DamageInfo(T2.LooseCatchBurstHitUnits * _pc.HitUnit * _mods.Potency("al_loose_catch"), ElementType.Air, _pc.gameObject) { Entry = "al_loose_catch" });
                TexelFx.Ring(_pc.transform.position, TexelFx.Steel, 0.4f, T2.LooseCatchRadius, 0.2f);
            }
            if (Has("ch_coated_catch"))
            {
                var near = NearestEnemy(_pc.transform.position, 3f, null);
                if (near != null) Coat(near, Strength("ch_coated_catch"));
            }
        }

        // ---------------------------------------------------------------- the release

        void RestReleased()
        {
            Vector2 me = _pc.transform.position;
            Vector2 face = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.down;
            float hit = T2.SkeletonHitUnits * _pc.HitUnit;
            void Strike(Health hp, string entry)
            {
                if (hp == null || hp.IsDead) return;
                float m = T2.Measure(entry);
                hp.Take(new DamageInfo(hit * m, Element, _pc.gameObject) { Entry = entry });
                if (!hp.IsDead) Coat(hp, m);
            }

            if (Has("co_spill")) Resource?.Spill(T2.SpillShare);
            if (Has("al_spill")) foreach (var hp in EnemiesWithin(me, T2.SpillCoatRadius)) Coat(hp, _mods.Potency("al_spill") * T2.Measure("al_spill"));

            if (Has("re_thrown_through"))
            {
                foreach (var hp in EnemiesInStrip(me, face, 6f, 0.7f)) Strike(hp, "re_thrown_through");
                LedgerFx.Crest(ReleaseCanvas.Feet(_pc.transform), face, 6f, TexelFx.Steel);
            }
            if (Has("re_edge_cone"))
            {
                foreach (var hp in EnemiesWithin(me, T2.SkeletonReach))
                    if (Vector2.Dot(((Vector2)hp.transform.position - me).normalized, face) > 0.5f) Strike(hp, "re_edge_cone");
                LedgerFx.Crest(ReleaseCanvas.Feet(_pc.transform), face, T2.SkeletonReach, CoatRamp);
            }
            if (Has("re_rim_discs"))
            {
                foreach (var hp in EnemiesWithin(me, 3.5f))
                    if (Vector2.Distance(hp.transform.position, me) >= 2f) Strike(hp, "re_rim_discs");
                TexelFx.Ring(me, TexelFx.Steel, 2f, 3.5f, 0.3f);
            }
            if (Has("re_seeded_orbit")) _seededOrbitUntil = Time.time + T2.SkeletonOrbitSeconds;
            if (Has("re_answering_volley"))
                foreach (var hp in EnemiesWithin(me, 5f)) { Strike(hp, "re_answering_volley"); LedgerFx.Echo(hp.transform.position, me); }
            if (Has("re_ring_stakes")) { _stakesUntil = Time.time + T2.SkeletonOrbitSeconds; _stakesAt = me; _stakesStruck.Clear(); }
        }

        // ---------------------------------------------------------------- the tick

        void TickRest(float dt)
        {
            if (_pc == null) return;
            _restTick -= dt;
            if (_restTick > 0f) return;
            _restTick = 0.25f;
            Vector2 me = _pc.transform.position;
            if (Time.time < _seededOrbitUntil)
            {
                foreach (var hp in EnemiesWithin(me, T2.SkeletonOrbitRadius))
                {
                    float m = T2.Measure("re_seeded_orbit");
                    hp.Take(new DamageInfo(0.3f * _pc.HitUnit * m, Element, _pc.gameObject) { Entry = "re_seeded_orbit" });
                    if (!hp.IsDead) Coat(hp, 0.5f * m);
                }
                TexelFx.Ring(me, TexelFx.Steel, T2.SkeletonOrbitRadius - 0.1f, T2.SkeletonOrbitRadius, 0.25f, 1f, true);
            }
            if (Time.time < _stakesUntil)
            {
                foreach (var hp in EnemiesWithin(_stakesAt, 3f))
                    if (Vector2.Distance(hp.transform.position, _stakesAt) >= 2.2f && _stakesStruck.Add(hp))
                    {
                        float m = T2.Measure("re_ring_stakes");
                        hp.Take(new DamageInfo(T2.SkeletonHitUnits * _pc.HitUnit * m, Element, _pc.gameObject) { Entry = "re_ring_stakes" });
                        if (!hp.IsDead) Coat(hp, m);
                    }
                TexelFx.Ring(_stakesAt, TexelFx.Steel, 2.2f, 2.6f, 0.25f, 1.2f, true);
            }
        }

        void RestFloorStarted() => _echoed.Clear();
    }
}
