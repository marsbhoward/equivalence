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
    /// THE OVERHAUL'S OTHER KINDS (2026-11-04): CHAIN forms on the basics, RELEASE entries, the
    /// HUMOUR additions, the TOLL, RULE, HOST, GROUND and SOCKET costs and their Albedos - and the
    /// dispatch the older partials call into (hits, swings, kills, releases, the floor, the tick).
    /// </summary>
    public partial class RunEffects
    {
        // ---------------------------------------------------------------- dispatch from the older hooks

        /// <summary>Every landed hit that is not a repeat, dead or alive (Offense.OnHitLanded).</summary>
        void OverhaulHit(Health target, DamageInfo info, in HitContext h)
        {
            if (_mods == null || _pc == null || target == null) return;
            if (h.IsFinisher) ArtHit(target, info, h);
            else BasicHit(target, info, h);
            WorldTwoHit(target);   // Cracked Plate
        }

        int _basicCount;
        bool _swingKilled;

        /// <summary>The swing now playing is the chain's connector - the last basic before an art.</summary>
        bool IsConnectorSwing => _pc != null && _swingChainIndex == _pc.BasicsPerChain - 1;

        /// <summary>A basic was committed (Offense.OnSwing, not a finisher).</summary>
        void OverhaulOnSwing(bool isFinisher)
        {
            if (_mods == null || _pc == null) return;
            if (isFinisher) return;
            if (_quickBasics > 0) _quickBasics--;
            _basicCount++;
            _swingKilled = false;
            RestOnSwing();   // Dull Link's window
            // LUNGE STEP: the first basic after an art steps you in (a bow steps out).
            if (Has("ch_lunge_step") && _swingChainIndex == 0 && Time.time - _pc.ArtEndedAt < 3f)
                StepFrom(_pc.Weapon == WeaponClass.Bow ? -1f : 1f, T2.LungeStepDistance * Strength("ch_lunge_step"), T2.StepSeconds);
            // OVERSWING (a cost): a swing that strikes nothing locks you.
            if (_pc.Weapon == WeaponClass.Greatsword && (Has("co_overswing") || Has("al_overswing")))
                StartCoroutine(OverswingCheck());
            WoundBladeSwing();
        }

        IEnumerator OverswingCheck()
        {
            yield return new WaitForSeconds(T2.OverswingCheck);
            if (_pc == null || (_swingStruck != null && _swingStruck.Count > 0)) yield break;
            if (Has("co_overswing")) _pc.Stun(T2.OverswingStun * (AtMax("co_overswing") ? 2f : 1f));
            if (Has("al_overswing"))
            {
                Vector2 from = _pc.transform.position, dir = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.right;
                float dmg = T2.OverswingCrescentShare * _pc.HitUnit * _mods.Potency("al_overswing");
                foreach (var hp in EnemiesInStrip(from, dir, 3f, 0.6f)) hp.Take(new DamageInfo(dmg, ElementType.Earth, _pc.gameObject) { Entry = "al_overswing" });
                LedgerFx.Crest(ReleaseCanvas.Feet(_pc.transform), dir, 3f, TexelFx.Gold);
            }
        }

        /// <summary>A basic's hit, on the CHAIN forms.</summary>
        void BasicHit(Health target, DamageInfo info, in HitContext h)
        {
            bool dead = target.IsDead;
            Vector2 at = target.transform.position;
            var w = _pc.Weapon;

            // The coat a basic carries.
            float coat = 0f;
            if (Has("ch_connector") && IsConnectorSwing) coat = Mathf.Max(coat, Strength("ch_connector"));
            if (Has("ch_ember_arc") && w == WeaponClass.Greatsword && !h.Thrown && _basicCount % T2.EmberArcEvery == 0) coat = Mathf.Max(coat, Strength("ch_ember_arc"));
            if (Has("ch_coated_bounce") && w == WeaponClass.Disc && h.Thrown && !h.First) coat = Mathf.Max(coat, Strength("ch_coated_bounce"));
            if (Has("ch_carried_tip") && w == WeaponClass.Bow && h.Thrown && h.First) coat = Mathf.Max(coat, Strength("ch_carried_tip"));
            if (coat > 0f && !dead) Coat(target, coat * Mathf.Max(0.35f, h.Proc));

            if (dead && !_swingKilled && Has("ch_follow_through") && w == WeaponClass.Greatsword && !h.Thrown)
            {
                _swingKilled = true;
                var next = NearestEnemy(at, T2.FollowThroughReach, target);
                if (next != null) next.Take(new DamageInfo(info.Amount * T2.FollowThroughShare * Strength("ch_follow_through"), info.Element, _pc.gameObject) { Entry = "ch_follow_through" });
            }
            if (Has("ch_shear") && w == WeaponClass.Disc && h.Thrown)
            {
                var near = NearestEnemy(at, T2.ShearReach, target);
                if (near != null) near.Take(new DamageInfo(info.Amount * T2.ShearShare * Strength("ch_shear"), info.Element, _pc.gameObject) { Entry = "ch_shear" });
            }
            if (Has("ch_pinning_shot") && w == WeaponClass.Bow && h.Thrown && h.First && !dead)
            {
                Vector2 away = at - (Vector2)_pc.transform.position;
                if (away.sqrMagnitude > 0.0001f && Arena.WallBetween(at, at + away.normalized * T2.PinningWallReach))
                    Roots(target, T2.PinningRoot * Strength("ch_pinning_shot"));
            }
            if (!dead && IsConnectorSwing && Has("al_planted_link")) Staggers(target, T2.PlantedLinkStagger * _mods.Potency("al_planted_link"));
            RestBasicHit(target, info, h);
        }

        /// <summary>BACKSTEP and UNSTEADY on a basic arrow, as it leaves the string (Forms.ArmArrow).</summary>
        void OverhaulArmArrow(ThrownArrow arrow, bool planted)
        {
            if (_pc == null) return;
            if (!planted && Has("co_unsteady")) arrow.Damage *= 1f - T2.UnsteadyDamage;
            if (planted && AtMax("co_unsteady")) arrow.Damage *= 1f - T2.UnsteadyDamage * 0.5f;
            if (!planted && Has("al_unsteady")) arrow.PierceFraction = Mathf.Max(arrow.PierceFraction, 0.75f);
            if (Has("ch_backstep") && EnemiesWithin(_pc.transform.position, T2.BackstepRadius).Count > 0)
                StepFrom(-1f, T2.BackstepDistance * Strength("ch_backstep"), T2.StepSeconds);
        }

        // ---------------------------------------------------------------- the release

        float _fillAtRelease = 1f;

        /// <summary>A release has fired (Element.OnReleased) - <see cref="_fillAtRelease"/> is the meter it fired from.</summary>
        void OverhaulReleased()
        {
            if (_pc == null) return;
            RestReleased();   // the release skeletons, Spill
            Vector2 me = _pc.transform.position;
            var hp = Hp;
            if (_fillAtRelease < T2.SpentMeterBelow)
            {
                int spent = N("co_spent_meter");
                if (spent > 0 && hp != null)
                {
                    Pay(hp.Max * T2.SpentMeterHealth * (1f + 0.5f * (spent - 1)));
                    if (AtMax("co_spent_meter")) _pc.Stun(0.3f);
                }
                if (Has("al_spent_meter") && hp != null) _pc.HealWithinLimit(hp.Max * T2.SpentMeterHeal * _mods.Potency("al_spent_meter"));
            }
            if ((AtMax("co_damp") || AtMax("co_kick")) && hp != null) Pay(hp.Max * 0.02f);
            if (Has("re_gathered")) foreach (var e in EnemiesWithin(me, T2.GatheredRadius)) DragToward.Pull(e, me, T2.GatheredPull);
            if (Has("re_clearing")) foreach (var e in EnemiesWithin(me, T2.ClearingRadius)) DragToward.Push(e, me, T2.ClearingPush * PushMul);
            if (Has("re_consume"))
                foreach (var e in EnemiesWithin(me, T2.ConsumeRadius))
                {
                    var st = e.GetComponent<StatusEffects>();
                    float left = st != null && st.Burning ? st.ConsumeBurn() : 0f;
                    if (left > 0f) { e.Take(new DamageInfo(left * T2.ConsumeShare, ElementType.Fire, _pc.gameObject) { Entry = "re_consume" }); LedgerFx.StatusBurst(e, TexelFx.Fire); }
                }
            if (Has("re_draw"))
                foreach (var e in EnemiesWithin(me, T2.DrawRadius))
                    if (e.GetComponent<StatusEffects>() is { Soaked: true }) DragToward.Pull(e, me, T2.DrawPull);
            if (Has("re_landslide"))
                foreach (var e in EnemiesWithin(me, T2.LandslideRadius))
                {
                    DragToward.Push(e, me, T2.LandslidePush * PushMul);
                    StartCoroutine(Slam(e, 0.75f));
                }
            if (Has("re_rubble")) Field("re_rubble", null, me, T2.RubbleRadius, ElementType.Earth, T2.RubbleSeconds, 1f);
            if (Has("re_funnel")) Field("re_funnel", null, me, T2.FunnelRadius, ElementType.Air, T2.FunnelSeconds, 1f);
            if (Has("re_carry"))
            {
                Vector2 dir = _pc.Facing.sqrMagnitude > 0.01f ? _pc.Facing.normalized : Vector2.down;
                _pc.Dash(dir, T2.CarryDistance / T2.CarrySeconds, T2.CarrySeconds);
            }
            if (Has("co_kick"))
            {
                var near = NearestEnemy(me, 8f, null);
                Vector2 away = near != null ? me - (Vector2)near.transform.position : -_pc.Facing;
                if (away.sqrMagnitude < 0.0001f) away = Vector2.down;
                _pc.Dash(away.normalized, T2.KickDistance / 0.15f, 0.15f);
            }
            if (Has("al_kick"))
            {
                var near = NearestEnemy(me, 8f, null);
                Vector2 away = near != null ? me - (Vector2)near.transform.position : -_pc.Facing;
                if (away.sqrMagnitude < 0.0001f) away = Vector2.down;
                StartCoroutine(KickCut(me, away.normalized));
            }
        }

        IEnumerator KickCut(Vector2 from, Vector2 dir)
        {
            _pc.Dash(dir, T2.KickDistance * 2f / 0.15f, 0.15f);
            yield return new WaitForSeconds(0.16f);
            if (_pc == null) yield break;
            foreach (var hp in EnemiesNearSegment(from, _pc.transform.position, 0.8f))
                hp.Take(new DamageInfo(T2.KickCutHitUnits * _pc.HitUnit * _mods.Potency("al_kick"), Element, _pc.gameObject) { Entry = "al_kick" });
        }

        /// <summary>DAMP (a socket cost): the release's area. Pressed Release (its Albedo) is damage.</summary>
        float OverhaulReleaseRadius => Has("co_damp") ? T2.DampRadiusMul : 1f;

        /// <summary>What a release's hits are worth now (Pressed Release).</summary>
        public float ReleaseDamageMul => Has("al_damp") ? 1f + T2.DampCentreBonus * _mods.Potency("al_damp") : 1f;

        // ---------------------------------------------------------------- the humours

        int _bleedingDeaths, _staggeredDeathsFloor;
        float _wildfireTick;

        /// <summary>DEEPENING: how much stronger the player's bleeds are now.</summary>
        public float BleedGrowth => Has("hu_deepening")
            ? 1f + Mathf.Min(T2.DeepeningCap, T2.DeepeningStep * (_bleedingDeaths / T2.DeepeningKillsPerStep)) : 1f;

        /// <summary>SETTLING: seconds the player's staggers last longer now.</summary>
        public float StaggerGrowth => Has("hu_settling") ? Mathf.Min(T2.SettlingCap, T2.SettlingStep * _staggeredDeathsFloor) : 0f;

        /// <summary>RIPPLE: a soak landed - one other body close by is soaked for half as long.</summary>
        public void OnSoaked(Health target, float seconds, float vulnerability)
        {
            if (!Has("hu_ripple") || target == null || _pc == null) return;
            Deeper(() =>
            {
                var near = NearestEnemy(target.transform.position, T2.RippleReach, target);
                if (near != null && near.GetComponent<StatusEffects>() is not { Soaked: true })
                    _pc.Soak(near, seconds * T2.RippleShare, vulnerability);
            });
        }

        void TickHumours(float dt)
        {
            _wildfireTick -= dt;
            if (!Has("hu_wildfire") || _wildfireTick > 0f) return;
            _wildfireTick = T2.WildfireTick;
            Enemies.EnemyRegistry.Prune();
            var burning = new List<Health>();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                var hp = e != null ? e.GetComponent<Health>() : null;
                if (hp != null && !hp.IsDead && hp.GetComponent<StatusEffects>() is { Burning: true }) burning.Add(hp);
            }
            foreach (var src in burning)
            {
                var st = src.GetComponent<StatusEffects>();
                foreach (var hp in EnemiesWithin(src.transform.position, T2.WildfireReach))
                    if (hp != src && hp.GetComponent<StatusEffects>() is not { Burning: true })
                        Deeper(() => _pc.Burn(hp, st.BurnDps * T2.WildfireShare, 2f));
            }
        }

        // ---------------------------------------------------------------- kills

        void OverhaulOnKill(Health h, float worth)
        {
            if (_pc == null || h == null) return;
            var st = h.GetComponent<StatusEffects>();
            var ec = h.GetComponent<Enemies.EnemyController>();
            Vector2 at = h.transform.position;
            bool bleeding = st != null && st.Bleeding, staggered = st != null && st.Staggered;
            if (bleeding) _bleedingDeaths++;
            if (staggered) _staggeredDeathsFloor++;
            if (st != null && st.Burning && Has("re_fuel")) Resource?.Refund(T2.FuelRefund);
            if (st != null && st.Soaked && Has("re_current")) Resource?.Refund(T2.CurrentRefund);
            if (bleeding && Has("hu_splatter"))
                Deeper(() =>
                {
                    var near = NearestEnemy(at, T2.SplatterReach, h);
                    if (near != null) _pc.Bleed(near, T2.SplatterShare * _pc.HitUnit, T2.SplatterSeconds);
                });
            if (staggered && Has("hu_topple"))
                Deeper(() => { foreach (var hp in EnemiesWithin(at, T2.ToppleReach)) Staggers(hp, T2.ToppleStagger); });
            if (ec != null && ec.Elite && ec.Affix != Enemies.EliteAffix.None && Has("al_affixed"))
                _pc.HealWithinLimit(Hp.Max * T2.AffixedHeal * _mods.Potency("al_affixed"));
            if (ec != null && ec.Elite && Has("al_wrath") && h.LastDamage.Source == _pc.gameObject) Hasten(1f);
            WorldTwoKill(h);   // Fallen Captain
            int martyrs = N("co_martyrs");
            if (martyrs > 0 || Has("al_martyrs")) StartCoroutine(Martyr(at, martyrs));
        }

        IEnumerator Martyr(Vector2 at, int stacks)
        {
            float r = T2.MartyrRadius * (AtMax("co_martyrs") ? 1.4f : 1f);
            bool mine = stacks <= 0;   // the Albedo's burst: enemies only
            TexelFx.Ring(at, mine ? CoatRamp : TexelFx.Danger, r * 0.9f, r, T2.MartyrDelay, 1.2f, false, SortingOrders.Telegraph);
            yield return new WaitForSeconds(T2.MartyrDelay);
            if (_pc == null) yield break;
            LedgerFx.Stuff(at, mine ? CoatRamp : TexelFx.Danger, r);
            if (mine)
            {
                foreach (var hp in EnemiesWithin(at, r))
                    hp.Take(new DamageInfo(T2.MartyrEnemyHitUnits * _pc.HitUnit * _mods.Potency("al_martyrs"), ElementType.Fire, _pc.gameObject) { Entry = "al_martyrs" });
                yield break;
            }
            if (Vector2.Distance(_pc.transform.position, at) <= r && Hp != null)
                Hp.Take(new DamageInfo(Hp.Max * T2.MartyrHealth * stacks, ElementType.Fire, null) { Entry = "co_martyrs" });
        }

        // ---------------------------------------------------------------- the floor

        float _stormTimer, _galeTimer, _sinkUntil;
        int _galeTurn;
        readonly Queue<(float At, Vector2 Pos)> _trail = new();

        void OverhaulFloorStarted()
        {
            _stormTimer = T2.StormEvery;
            _galeTimer = T2.GaleEvery;
            _staggeredDeathsFloor = 0;
            _parriesThisFloor = 0;
            _strainBank = 0f;
            _trail.Clear();
            _primed.Clear();
            _readMarks.Clear();
            WorldTwoFloorStarted();
            RestFloorStarted();
        }

        void TickWorld(float dt, bool moving)
        {
            if (_pc == null) return;
            Vector2 me = _pc.transform.position;
            _trail.Enqueue((Time.time, me));
            while (_trail.Count > 0 && Time.time - _trail.Peek().At > T2.StormLag + 0.2f) _trail.Dequeue();

            // SINKING: after standing still, the first second moving is slow.
            if (Has("co_sinking") && moving && _stillBeforeMove >= T2.SinkingAfter) _sinkUntil = Time.time + T2.SinkingSeconds;
            _stillBeforeMove = moving ? 0f : _stillBeforeMove + dt;

            if (Has("co_storm") || Has("al_storm"))
            {
                _stormTimer -= dt * (AtMax("co_storm") ? 2f : 1f);
                if (_stormTimer <= 0f)
                {
                    _stormTimer = T2.StormEvery;
                    if (Has("co_storm") && _trail.Count > 0) StartCoroutine(Strike(_trail.Peek().Pos, false));
                    if (Has("al_storm"))
                    {
                        var near = NearestEnemy(me, 10f, null);
                        if (near != null) StartCoroutine(Strike(near.transform.position, true));
                    }
                }
            }

            if (Has("co_gale_floor") || Has("al_gale_floor"))
            {
                _galeTimer -= dt * (AtMax("co_gale_floor") ? 1.5f : 1f);
                if (_galeTimer <= 0f)
                {
                    _galeTimer = T2.GaleEvery;
                    _galeTurn = (_galeTurn + 1) % 4;
                    StartCoroutine(Gale(_galeTurn));
                }
            }
        }

        float _stillBeforeMove;

        IEnumerator Strike(Vector2 at, bool enemiesOnly)
        {
            TexelFx.Ring(at, enemiesOnly ? CoatRamp : TexelFx.Danger, T2.StormRadius * 0.9f, T2.StormRadius, T2.StormLag, 1.2f, false, SortingOrders.Telegraph);
            yield return new WaitForSeconds(T2.StormLag);
            if (_pc == null) yield break;
            LedgerFx.Stuff(at, TexelFx.Charge, T2.StormRadius);
            if (enemiesOnly)
            {
                foreach (var hp in EnemiesWithin(at, T2.StormRadius))
                    hp.Take(new DamageInfo(T2.StormEnemyHitUnits * _pc.HitUnit * _mods.Potency("al_storm"), ElementType.Air, _pc.gameObject) { Entry = "al_storm" });
                yield break;
            }
            if (Hp != null && Vector2.Distance(_pc.transform.position, at) <= T2.StormRadius)
                Hp.Take(new DamageInfo(Hp.Max * T2.StormHealth, ElementType.Air, null) { Entry = "co_storm" });
        }

        IEnumerator Gale(int turn)
        {
            Vector2 dir = turn switch { 0 => Vector2.right, 1 => Vector2.down, 2 => Vector2.left, _ => Vector2.up };
            Vector2 me = _pc.transform.position;
            LedgerFx.Crest(me - dir * 3f, dir, 6f, TexelFx.Grey);
            yield return new WaitForSeconds(T2.GaleWarning);
            if (_pc == null) yield break;
            float push = T2.GalePush * (AtMax("co_gale_floor") ? 1.5f : 1f);
            Enemies.EnemyRegistry.Prune();
            foreach (var e in new List<Enemies.EnemyController>(Enemies.EnemyRegistry.All))
            {
                var hp = e != null ? e.GetComponent<Health>() : null;
                if (hp != null && !hp.IsDead) DragToward.Push(hp, (Vector2)hp.transform.position - dir, push);
            }
            if (Has("co_gale_floor")) _pc.Dash(dir, push / 0.25f, 0.25f);
            LedgerFx.Crest((Vector2)_pc.transform.position - dir * 3f, dir, 6f, TexelFx.Grey);
        }

        // ---------------------------------------------------------------- speeds

        /// <summary>Attack Speed points the overhaul's costs take right now (Fatigue, Slow Link). Negative.</summary>
        float OverhaulAttackSpeedDown
        {
            get
            {
                float down = 0f;
                int fat = N("co_fatigue");
                if (fat > 0)
                {
                    float over = _floorSeconds - T2.FatigueAfter;
                    if (over > 0f)
                    {
                        float steps = Mathf.Floor(over / T2.FatigueEvery);
                        float cap = AtMax("co_fatigue") ? float.MaxValue : T2.FatigueCap;
                        down -= Mathf.Min(cap, T2.FatigueSpeed * steps * fat);
                    }
                }
                if (Has("co_slow_link") && _pc != null && !_pc.FinisherNext && _pc.ComboIndex == _pc.BasicsPerChain - 1)
                    down -= T2.SlowLinkSpeed * N("co_slow_link");
                return down;
            }
        }

        /// <summary>Move Speed points the overhaul's costs take right now (Heavy Bulwark, Sinking). Negative.</summary>
        public float MoveSpeedDown
        {
            get
            {
                if (_pc == null) return 0f;
                float down = 0f;
                if (Has("co_heavy_bulwark") && _pc.BulwarkActive) down -= T2.HeavyBulwarkMove * (AtMax("co_heavy_bulwark") ? 2f : 1f);
                if (Time.time < _sinkUntil) down -= T2.SinkingMove * (AtMax("co_sinking") ? 2f : 1f);
                return down;
            }
        }

        /// <summary>PLANTED LINK (a socket cost): no moving during the connector basic.</summary>
        public bool ConnectorRooted => Has("co_planted_link") && _pc != null && !_pc.FinisherNext
                                       && _pc.ComboIndex == _pc.BasicsPerChain - 1 && _pc.AttackLocked;

        // ---------------------------------------------------------------- the costs' own moments

        /// <summary>OVERDRAWN (a cost): a weapon art that isn't PERFECT costs health. Asked as the bar settles.</summary>
        void OverhaulArtSettled(StrikeVerdict verdict)
        {
            int n = N("co_overdrawn");
            if (n <= 0 || Hp == null) return;
            bool pays = verdict == StrikeVerdict.Perfect ? false : verdict != StrikeVerdict.Good || AtMax("co_overdrawn");
            if (pays) Pay(Hp.Max * T2.OverdrawnHealth * n);
        }

        /// <summary>The socket curses at their Nigredo: firing a cursed art, chain or guard costs health.</summary>
        void CursedArtFired()
        {
            if (_mods == null || _pc == null || Hp == null) return;
            int s = _pc.ArtSocket;
            foreach (var id in new[] { "heavy_hand", "dulled_script", "brittle_script" })
                if (AtMax(id) && _mods.SocketOf(id) == s) { Pay(Hp.Max * 0.02f); return; }
            if (AtMax("co_slow_link") || AtMax("co_planted_link")) Pay(Hp.Max * 0.02f);
        }

        // ---------------------------------------------------------------- the overhaul's tick

        void OverhaulTick(float dt, bool moving)
        {
            TickArts();
            TickGuard(dt);
            TickHumours(dt);
            TickWorld(dt, moving);
            TickWorldTwo(dt);
            TickRest(dt);
        }

        // ---------------------------------------------------------------- shared

        static List<Health> EnemiesNearSegment(Vector2 a, Vector2 b, float halfWidth)
        {
            var list = new List<Health>();
            Vector2 ab = b - a;
            float len2 = Mathf.Max(1e-4f, ab.sqrMagnitude);
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                Vector2 p = e.transform.position;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                if ((p - (a + ab * t)).sqrMagnitude <= halfWidth * halfWidth) list.Add(hp);
            }
            return list;
        }
    }
}
