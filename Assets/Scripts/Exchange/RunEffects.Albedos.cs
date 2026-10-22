using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Exchange
{
    /// <summary>
    /// The TRANSFORMED Albedos (the user's picks, 2026-10-21): each keeps its cost's trigger or
    /// object and turns what it does, where the old ones read the cost backwards.
    /// Batch 1 - Maturation (the floor clock), Basilisk (the eye), Patina (the plating),
    /// Phlogiston (lost heat), Gale (the streak). Batch 2 - Gilding (the art's strike),
    /// Mollification (enemy armour), Adamant (the blows you take), Viriditas (the floor),
    /// Committed (the lock). Batch 3 - Mountain (stillness), Lucid (the telegraph), Sealed Vessel
    /// (lost meter), Lightfoot (an anchor), Concentrate (a full meter), Unshackled (the
    /// recharge), Ossuary (the corpses).
    /// </summary>
    public partial class RunEffects
    {
        // ---------------------------------------------------------------- state

        // Sealed Vessel: the meter as last seen, what has been lost since the last splash, and
        // whether a release spent it this step (spending is not losing).
        float _meterSeen = -1f, _meterLost;
        bool _meterSpent;

        // Lightfoot: where the anchor lies and how long it waits. The marker is a scene object,
        // which a reload keeps.
        Vector2 _anchorAt;
        float _anchorRemaining;
        [SerializeField] GameObject _anchorMark;

        // Lucid: every enemy seen winding up, and - once its wind-up ended - how long its attack
        // has left to land before it counts as dodged. Non-readonly, rebuilt if a reload empties it.
        [System.Serializable]
        struct LucidWatch
        {
            public Enemies.EnemyController Enemy;
            public float Window;   // < 0 while still winding up
        }
        List<LucidWatch> _lucid;

        // Batches 1 and 2.
        float _maturationNext;          // Maturation: the floor second the next art is readied at
        bool _maturationReady;
        int _maturingArt = -1;          // the art (ArtSerial) stripping armour, while it lands
        Health _gazeTarget;             // Basilisk: the lock being held, and for how long
        float _gazeSeconds, _gazePulse;
        float _temperedRemaining;       // Adamant
        float _committedBank;           // Committed: damage owed to the next art
        float _shield;                  // Viriditas: the floor's shield left

        void TickAlbedos(float dt, float meter01)
        {
            TickSealedVessel(meter01);
            TickAnchor(dt);
            TickLucid(dt);
            TickMaturation();
            TickBasilisk(dt);
            Countdown(ref _temperedRemaining, dt);
        }

        void OnFloorStartedAlbedos()
        {
            ClearAnchor();
            _lucid?.Clear();
            _meterSeen = -1f;
            _meterLost = 0f;
            _maturationNext = T.SouringEverySeconds;
            _maturationReady = false;
            _maturingArt = -1;
            _gazeTarget = null;
            _gazeSeconds = 0f;
            _temperedRemaining = 0f;

            // VIRIDITAS: the floor starts with a shield of new growth.
            var hp = Hp;
            _shield = Has("viriditas") && hp != null ? hp.Max * T.ViriditasShield * _mods.Potency("viriditas") : 0f;
            if (_shield > 0f && _pc != null)
                Spr.Pulse(_pc.transform, 1.1f, new Color(0.55f, 0.9f, 0.45f, 0.7f), 0.4f, true, 1.3f);
        }

        // ---------------------------------------------------------------- Maturation

        /// <summary>MATURATION: Souring's clock, kept - every SouringEverySeconds on a floor the next
        /// art is readied (once; an unspent one does not stack).</summary>
        void TickMaturation()
        {
            if (!Has("maturation") || _pc == null || _floorSeconds < _maturationNext) return;
            while (_maturationNext <= _floorSeconds) _maturationNext += T.SouringEverySeconds;
            if (_maturationReady) return;
            _maturationReady = true;
            Spr.Pulse(_pc.transform, 0.9f, new Color(0.85f, 0.75f, 0.35f, 0.7f), 0.3f, true, 1.2f);
        }

        /// <summary>A hit about to land (ModifyOutgoing): Maturation's readied art strips the armour
        /// of everything it hits, before the hit meets it; Committed's bank joins an art's first hit.</summary>
        float AlbedosOutgoing(Health target, float damage, in HitContext h)
        {
            if (!h.IsFinisher || h.Repeat || _pc == null) return damage;
            if (Has("maturation"))
            {
                if (_maturationReady) { _maturationReady = false; _maturingArt = _pc.ArtSerial; }
                var armour = _maturingArt == _pc.ArtSerial ? target.GetComponent<EnemyArmor>() : null;
                if (armour != null && armour.Current > 0f)
                {
                    armour.Strip();
                    Spr.Flash(target.transform.position, 0.9f, new Color(0.85f, 0.75f, 0.35f, 0.7f), 0.25f, false);
                }
            }
            if (h.First && _committedBank > 0f)
            {
                damage += _committedBank;
                _committedBank = 0f;
                Spr.Flash(target.transform.position, 0.8f, new Color(1f, 0.45f, 0.3f, 0.7f), 0.2f, false);
            }
            return damage;
        }

        // ---------------------------------------------------------------- Gilding, Mollification

        /// <summary>A hit landed (OnHitLanded): an art gilds what it strikes; armour broken by it
        /// shatters onto the bodies round it.</summary>
        void AlbedosOnHitLanded(Health target, in HitContext h)
        {
            if (_pc == null || target == null) return;
            if (h.IsFinisher && Has("gilding") && !target.IsDead)
            {
                var mk = LedgerMarks.Of(target.gameObject);
                if (mk.GildedArt != _pc.ArtSerial)
                {
                    mk.GildedArt = _pc.ArtSerial;
                    Spr.Flash(target.transform.position, 0.8f, new Color(1f, 0.84f, 0.35f, 0.75f), 0.3f, false);
                }
            }
            if (Has("mollification"))
            {
                var armour = target.GetComponent<EnemyArmor>();
                if (armour != null && armour.Max > 0f && armour.Current <= 0f && !armour.Shattered)
                {
                    armour.Shattered = true;
                    Shatter(target);
                }
            }
        }

        /// <summary>GILDING: the crit chance a basic adds against a body the last art gilded - until
        /// the next art (asked by the crit roll, and by a thrown basic's own).</summary>
        public float GildedCrit(Health target)
        {
            if (_mods == null || _pc == null || target == null || !Has("gilding")) return 0f;
            var mk = LedgerMarks.Peek(target.gameObject);
            return mk != null && mk.GildedArt == _pc.ArtSerial ? T.GildingCrit * _mods.Potency("gilding") : 0f;
        }

        /// <summary>MOLLIFICATION: the broken shell flies off - every OTHER enemy near takes the shards.</summary>
        void Shatter(Health from)
        {
            Vector2 at = from.transform.position;
            float damage = T.MollificationShardHitUnits * _pc.HitUnit * _mods.Potency("mollification");
            float radius = T.MollificationRadius * _pc.AreaScaleNow;
            foreach (var hp in EnemiesWithin(at, radius))
                if (hp != from) hp.Take(new DamageInfo(damage, Element, _pc.gameObject));
            Spr.Flash(at, radius, new Color(0.78f, 0.8f, 0.86f, 0.45f), 0.22f);
        }

        // ---------------------------------------------------------------- Basilisk

        /// <summary>BASILISK: the lock held on one body long enough stones it - slowed and taking
        /// more, refreshed while it stays held, gone a moment after the eye leaves it.</summary>
        void TickBasilisk(float dt)
        {
            if (!Has("basilisk") || _pc == null || _pc.Targeting == null) { _gazeTarget = null; _gazeSeconds = 0f; return; }
            var t = _pc.Targeting.Target;
            if (t == null || t.IsDead || t != _gazeTarget) { _gazeTarget = t; _gazeSeconds = 0f; return; }
            float was = _gazeSeconds;
            _gazeSeconds += dt;
            if (_gazeSeconds < T.BasiliskHoldSeconds) return;
            var st = StatusEffects.Get(t.gameObject);
            st.ApplySlow(0.3f, T.BasiliskSlowMul);
            st.ApplyExposed(0.3f, 1f + T.BasiliskVulnerability * _mods.Potency("basilisk"));
            _gazePulse -= dt;
            if (was < T.BasiliskHoldSeconds || _gazePulse <= 0f)
            {
                _gazePulse = 1.5f;
                Spr.Flash(t.transform.position, 0.9f, new Color(0.62f, 0.62f, 0.58f, 0.7f), 0.3f, false);
            }
        }

        // ---------------------------------------------------------------- Adamant, Committed, Viriditas

        /// <summary>A hit taken, after mitigation (OnHitTaken).</summary>
        void AlbedosOnHitTaken(float amount)
        {
            var hp = Hp;
            // ADAMANT: a heavy blow tempers you.
            if (Has("adamant") && hp != null && hp.Max > 0f && amount >= hp.Max * T.AdamantBlowShare)
            {
                _temperedRemaining = T.AdamantSeconds;
                if (_pc != null) Spr.Pulse(_pc.transform, 1f, new Color(0.75f, 0.8f, 0.9f, 0.7f), 0.3f, true, 1.2f);
            }
            // COMMITTED: a blow taken in an art's lock is banked for the next art.
            if (Has("committed") && _pc != null && _pc.AttackLocked && amount > 0f)
                _committedBank = Mathf.Min(T.CommittedCapHitUnits * _pc.HitUnit,
                                           _committedBank + amount * T.CommittedShare * _mods.Potency("committed"));
        }

        /// <summary>ADAMANT: tempered, the run's Resilience rises - a reduction inside the one floor.</summary>
        float TemperedNow
        {
            get
            {
                if (_temperedRemaining <= 0f || !Has("adamant")) return 1f;
                var m = M;
                float with = StatPercents.ReductionFactor(m.Points(StatKind.Resilience, T.AdamantResilience * _mods.Potency("adamant")));
                return with / Mathf.Max(0.01f, m.ResilienceFactor);
            }
        }

        /// <summary>VIRIDITAS: the floor's shield takes what it can of a hit before health does.</summary>
        float AbsorbShield(float amount)
        {
            if (_shield <= 0f || amount <= 0f) return amount;
            float taken = Mathf.Min(_shield, amount);
            _shield -= taken;
            if (_shield <= 0f && _pc != null)
                Spr.Flash(_pc.transform.position, 1f, new Color(0.55f, 0.9f, 0.45f, 0.6f), 0.25f);
            return amount - taken;
        }

        /// <summary>Viriditas's shield left this floor (for a readout).</summary>
        public float ShieldLeft => _shield;

        // ---------------------------------------------------------------- Patina, Phlogiston, Gale

        /// <summary>PATINA: plates a blow broke (GameBootstrap's plating) - a shard back at its striker
        /// for each.</summary>
        public void OnPlatesBroken(int broken, DamageInfo info)
        {
            if (_mods == null || _pc == null || broken <= 0 || !Has("patina") || info.Source == null) return;
            var attacker = info.Source.GetComponent<Health>();
            if (attacker == null || attacker.IsDead || attacker.gameObject == _pc.gameObject) return;
            float damage = broken * T.PatinaShardHitUnits * _pc.HitUnit * _mods.Potency("patina");
            attacker.Take(new DamageInfo(damage, Element, _pc.gameObject));
            Spr.Flash(attacker.transform.position, 0.7f, new Color(0.55f, 0.75f, 0.68f, 0.75f), 0.2f, false);
        }

        /// <summary>PHLOGISTON: heat that faded or spilled (FireResource) flares round you.</summary>
        public void OnHeatLost(int stacks)
        {
            if (_mods == null || _pc == null || stacks <= 0 || !Has("phlogiston")) return;
            Vector2 at = _pc.transform.position;
            float radius = T.PhlogistonRadius * _pc.AreaScaleNow;
            float dps = T.PhlogistonBurnHitUnits * _pc.HitUnit * _mods.Potency("phlogiston") * stacks;
            foreach (var hp in EnemiesWithin(at, radius)) _pc.Burn(hp, dps, T.PhlogistonBurnSeconds);
            Spr.Flash(at, radius, new Color(1f, 0.55f, 0.2f, 0.4f), 0.22f);
        }

        /// <summary>GALE: a streak lost to a whiff (AirResource) gusts out, if it was worth having.</summary>
        public void OnStreakLost(int streak)
        {
            if (_mods == null || _pc == null || streak < T.GaleMinStreak || !Has("gale")) return;
            Vector2 at = _pc.transform.position;
            float radius = T.GaleRadius * _pc.AreaScaleNow;
            Push(at, radius, T.GalePush * _mods.Potency("gale"));
            Spr.Flash(at, radius, new Color(0.8f, 0.92f, 1f, 0.45f), 0.25f);
        }

        void OnDestroy() => ClearAnchor();

        // ---------------------------------------------------------------- Mountain, Lucid

        /// <summary>An enemy's hit landed (from OnStruckBy, the attacker known and alive).</summary>
        void AlbedosOnStruckBy(Health attacker)
        {
            // MOUNTAIN: planted, the blow that lands on you costs its striker its legs.
            if (Has("mountain") && _stillSeconds >= T.RestlessStillSeconds && !attacker.IsDead)
            {
                StatusEffects.Get(attacker.gameObject)
                    .ApplySlow(T.MountainSlowSeconds * _mods.Potency("mountain"), T.MountainSlowMul);
                Spr.Flash(attacker.transform.position, 0.8f, new Color(0.62f, 0.55f, 0.44f, 0.6f), 0.22f, false);
            }

            // LUCID: an attack that landed was not dodged.
            if (_lucid == null) return;
            var ec = attacker.GetComponent<Enemies.EnemyController>();
            for (int i = _lucid.Count - 1; i >= 0; i--)
                if (_lucid[i].Enemy == ec) _lucid.RemoveAt(i);
        }

        /// <summary>
        /// LUCID: Fog's outlines (Player.LucidRims) stay, and an outlined enemy whose wind-up ends
        /// without its attack landing on you inside the window is EXPOSED. A flinched wind-up
        /// counts - its attack never landed. The Booster's roar and the Bell's bubble are wind-ups
        /// that were never aimed at you, and are left out.
        /// </summary>
        void TickLucid(float dt)
        {
            if (!Has("lucid") || _pc == null) { _lucid?.Clear(); return; }
            _lucid ??= new List<LucidWatch>();

            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null || e.Kind == Enemies.EnemyKind.Booster || e.Kind == Enemies.EnemyKind.Bubbles) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                float wind = Mathf.Max(e.TelegraphProgress01, e.Kind == Enemies.EnemyKind.Turret ? e.TurretChargeProgress01 : 0f);
                int i = LucidIndex(e);
                if (wind > 0.02f)
                {
                    if (i < 0) _lucid.Add(new LucidWatch { Enemy = e, Window = -1f });
                    else if (_lucid[i].Window >= 0f)
                    {
                        // Winding up again before the last attack landed: that one missed.
                        Expose(hp);
                        _lucid[i] = new LucidWatch { Enemy = e, Window = -1f };
                    }
                }
                else if (i >= 0 && _lucid[i].Window < 0f)
                    _lucid[i] = new LucidWatch { Enemy = e, Window = T.LucidDodgeWindow };
            }

            for (int i = _lucid.Count - 1; i >= 0; i--)
            {
                var w = _lucid[i];
                var hp = w.Enemy != null ? w.Enemy.GetComponent<Health>() : null;
                if (hp == null || hp.IsDead) { _lucid.RemoveAt(i); continue; }
                if (w.Window < 0f) continue;
                w.Window -= dt;
                if (w.Window > 0f) { _lucid[i] = w; continue; }
                _lucid.RemoveAt(i);
                Expose(hp);
            }
        }

        int LucidIndex(Enemies.EnemyController e)
        {
            for (int i = 0; i < _lucid.Count; i++) if (_lucid[i].Enemy == e) return i;
            return -1;
        }

        void Expose(Health hp)
        {
            StatusEffects.Get(hp.gameObject)
                .ApplyExposed(T.LucidExposedSeconds, 1f + T.LucidExposed * _mods.Potency("lucid"));
            Spr.Flash(hp.transform.position, 0.7f, new Color(1f, 0.62f, 0.18f, 0.7f), 0.25f, false);
        }

        // ---------------------------------------------------------------- Sealed Vessel

        /// <summary>
        /// SEALED VESSEL: meter LOST - faded, spilled, a heat run out - splashes the element's own
        /// status round you, every SplashEvery of it; meter SPENT on a release does not. Fire burns,
        /// Water soaks, Earth staggers, Air bleeds (the humours' elements), through the player's
        /// own status calls so the board's status rules reach it.
        /// </summary>
        void TickSealedVessel(float meter01)
        {
            float was = _meterSeen;
            _meterSeen = meter01;
            if (!Has("sealed_vessel") || _pc == null) { _meterLost = 0f; _meterSpent = false; return; }
            if (_meterSpent) { _meterSpent = false; return; }
            if (was < 0f || meter01 >= was) return;
            _meterLost += was - meter01;
            // One splash a step at most: a meter emptied at once (Barren, Doldrums) is one splash.
            if (_meterLost < T.SealedVesselSplashEvery) return;
            _meterLost = 0f;
            SealedSplash();
        }

        void SealedSplash()
        {
            float pot = _mods.Potency("sealed_vessel");
            Vector2 at = _pc.transform.position;
            float radius = T.SealedVesselRadius * _pc.AreaScaleNow;
            float dot = T.SealedVesselDotHitUnits * _pc.HitUnit * pot;
            var near = EnemiesWithin(at, radius);
            foreach (var hp in near)
            {
                switch (Element)
                {
                    case ElementType.Fire: _pc.Burn(hp, dot, T.SealedVesselSeconds); break;
                    case ElementType.Water:
                        _pc.Soak(hp, T.SealedVesselSeconds * pot, Tuning.Elements.Water.SoakVulnerability); break;
                    case ElementType.Earth: _pc.Stagger(hp, T.SealedVesselStagger * pot); break;
                    default: _pc.Bleed(hp, dot, T.SealedVesselSeconds); break;
                }
            }
            var tint = ElementInfo.Tint(Element);
            tint.a = near.Count > 0 ? 0.4f : 0.2f;
            Spr.Flash(at, radius, tint, 0.22f);
        }

        // ---------------------------------------------------------------- Lightfoot, Unshackled

        /// <summary>The defensive ability was used (any of the four).</summary>
        void AlbedosOnDefenceUsed()
        {
            // UNSHACKLED: the recharge's price paid back in tempo - the chain moves on a basic.
            if (Has("unshackled") && !_pc.FinisherNext)
            {
                _pc.AdvanceChain();
                Spr.Pulse(_pc.transform, 0.8f, new Color(1f, 0.85f, 0.45f, 0.6f), 0.2f, true, 1.2f);
            }

            // LIGHTFOOT: an anchor where you stood.
            if (Has("lightfoot"))
            {
                ClearAnchor();
                _anchorAt = _pc.transform.position;
                _anchorRemaining = T.LightfootAnchorSeconds;
                var go = new GameObject("lightfoot-anchor");
                go.transform.position = _anchorAt;
                go.transform.localScale = Vector3.one * 0.9f;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Spr.ThinRing;
                sr.color = new Color(0.72f, 0.9f, 1f, 0.8f);
                sr.sortingOrder = SortingOrders.GroundDecal + 2;
                _anchorMark = go;
            }
        }

        void TickAnchor(float dt)
        {
            if (_anchorRemaining <= 0f) return;
            _anchorRemaining -= dt;
            if (_anchorRemaining <= 0f) { ClearAnchor(); return; }
            if (_anchorMark != null && _anchorMark.TryGetComponent<SpriteRenderer>(out var sr))
            {
                var c = sr.color;
                c.a = 0.8f * Mathf.Clamp01(_anchorRemaining / 1f);   // fades over its last second
                sr.color = c;
            }
        }

        void ClearAnchor()
        {
            _anchorRemaining = 0f;
            if (_anchorMark != null) Destroy(_anchorMark);
            _anchorMark = null;
        }

        /// <summary>
        /// LIGHTFOOT: the guard pressed while the defensive ability recharges. With an anchor down,
        /// the player is DASHED back to it (movement only - no immunity, no parry, the cooldown as it
        /// was) and every enemy along the line actually travelled is cut as the body arrives.
        /// </summary>
        public void OnGuardWhileRecharging()
        {
            if (_mods == null || _pc == null || _anchorRemaining <= 0f || !Has("lightfoot")) return;
            if (_pc.AttackLocked || _pc.Airborne) return;
            Vector2 from = _pc.transform.position;
            var d = _anchorAt - from;
            float dist = d.magnitude;
            ClearAnchor();
            if (dist < 0.3f) return;
            float seconds = dist / T.LightfootSnapSpeed;
            _pc.Dash(d / dist, T.LightfootSnapSpeed, seconds);
            StartCoroutine(AnchorCut(from, seconds));
        }

        IEnumerator AnchorCut(Vector2 from, float seconds)
        {
            yield return new WaitForSeconds(seconds + Time.fixedDeltaTime);
            if (_pc == null || _mods == null) yield break;
            Vector2 to = _pc.transform.position;
            float damage = T.LightfootCutHitUnits * _pc.HitUnit * _mods.Potency("lightfoot");
            var seg = to - from;
            float len2 = Mathf.Max(0.0001f, seg.sqrMagnitude);
            Enemies.EnemyRegistry.Prune();
            var cut = new List<Health>();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                Vector2 p = e.transform.position;
                float t = Mathf.Clamp01(Vector2.Dot(p - from, seg) / len2);
                if ((from + seg * t - p).sqrMagnitude <= T.LightfootCutWidth * T.LightfootCutWidth) cut.Add(hp);
            }
            foreach (var hp in cut)
            {
                hp.Take(new DamageInfo(damage, Element, _pc.gameObject));
                Spr.Flash(hp.transform.position, 0.6f, new Color(0.72f, 0.9f, 1f, 0.7f), 0.18f, false);
            }
        }

        // ---------------------------------------------------------------- Concentrate

        /// <summary>CONCENTRATE: how much further a release reaches, given the meter it fires from
        /// (PlayerController.DoRelease asks, just before the release).</summary>
        public float ReleaseRadiusMul(float meter01)
        {
            if (_mods == null || !Has("concentrate") || meter01 < 0.999f) return 1f;
            return 1f + (T.ConcentrateFullRadiusMul - 1f) * _mods.Potency("concentrate");
        }

        // ---------------------------------------------------------------- Ossuary

        /// <summary>OSSUARY: the kill leaves a bone corpse that bursts when an enemy walks into it.</summary>
        void OssuaryCorpse(Vector3 at)
        {
            var c = Corpse.Spawn(at, T.OssuaryCorpseSeconds, _pc.gameObject, CorpseMode.Bursts);
            c.ArmBurst(T.OssuaryBurstRadius * _pc.AreaScaleNow,
                       T.OssuaryBurstHitUnits * _pc.HitUnit * _mods.Potency("ossuary"),
                       Element, _pc.gameObject);
        }
    }
}
