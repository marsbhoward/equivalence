using System;
using UnityEngine;
using Convergence.Core;
using T2 = Convergence.Core.Tuning.Overhaul;
using D = Convergence.Core.Tuning.Defense;

namespace Convergence.Balance
{
    /// <summary>
    /// THE OVERHAUL PRICED (EXCHANGE-OVERHAUL.md, the user's call 2026-11-04: "all boons and costs need
    /// to be properly weighted"). Every overhaul entry - forms, Concords, releases, humours, Fortune,
    /// the costs and their Albedos - as arithmetic on its own Tuning.Overhaul numbers, at the uptimes
    /// <see cref="Rates"/> states. Behaviours become four currencies the model already prices:
    ///
    /// - DAMAGE: hit units on the body being fought (TargetDps) or on others (CrowdDps).
    /// - CONTROL: body-seconds of stagger-equivalent a second; one shared budget turns them into
    ///   fewer blows taken (the game's ControlBudget caps a body at 2.5 s in 6).
    /// - TEMPO: haste basics a chain (one shared bank, capped at HasteBasicsPerChain) and attack-speed
    ///   bursts, as a shorter chain.
    /// - HEALTH: a price paid each time (bleed, through Sustain), blows made larger or more often
    ///   (incoming), bodies made tougher (damage).
    ///
    /// A COAT is the element's own: Fire's burn and Air's bleed as damage, Earth's stagger as
    /// control, Water's soak as damage taken by the body you fight; every coat feeds the meter.
    /// Class entries are priced on the model's one chain (a sword's) with their class's rates.
    /// </summary>
    public static partial class Assay
    {
        /// <summary>
        /// The overhaul's uptimes - every guess the pricing makes, here and nowhere else. The ledger's
        /// meter (Exchange.LedgerMeter, the [Ledger] log) measures what a played run actually did;
        /// move these toward it.
        /// </summary>
        public static class Rates
        {
            // ---- the art and its bodies
            /// <summary>Bodies a pull art drags in; a push art throws; a sweep / setup art strikes; a
            /// reposition's path passes.</summary>
            public const float PullBodies = 2f, PushBodies = 1.5f, SweepBodies = 2.5f, SetupBodies = 1.5f, PathBodies = 1f;
            /// <summary>Hits a ward art stops while it is up.</summary>
            public const float WardStops = 1.2f;
            /// <summary>A pushed body meets a wall or another body.</summary>
            public const float PushMeetsShare = 0.35f;
            /// <summary>A pull drags three or more (Reel); an art strikes four or more (Harvest); a
            /// Medium art three or more (Rebound).</summary>
            public const float ThreePulledShare = 0.35f, FourStruckShare = 0.25f, ThreeStruckShare = 0.35f;
            /// <summary>Nothing is within reach after a push art (Breathing Room).</summary>
            public const float ClearAfterPushShare = 0.4f;
            /// <summary>A second body stands where a form throws to (Collide's pair, Skewer's target, a
            /// spread's neighbour).</summary>
            public const float NeighbourShare = 0.7f;
            /// <summary>The next art lands on what this one marked (a payoff's prime, Primer).</summary>
            public const float PayoffLandsShare = 0.6f;
            /// <summary>The next art comes inside Ambush's window.</summary>
            public const float AmbushWindowShare = 0.3f;
            /// <summary>The wheel turns inside Full Turn's window; a perfect streak stands at five.</summary>
            public const float FullTurnShare = 0.8f, StreakShare = 0.3f;
            /// <summary>An art or basic is the killing blow on its main target.</summary>
            public const float ArtKillShare = 0.3f, BasicKillShare = 0.15f;

            // ---- the chain
            /// <summary>Bodies one basic strikes; a basic swings at nothing (Overswing); a basic is
            /// loosed moving (Unsteady); an arrow strikes near a wall (Pinning Shot).</summary>
            public const float BasicBodies = 1.3f, WhiffShare = 0.1f, MovingShotShare = 0.5f, NearWallShare = 0.25f;
            /// <summary>Catches a thrown basic gives back; bounces a disc basic makes.</summary>
            public const float CatchesPerBasic = 0.5f, BouncesPerBasic = 1f;

            // ---- the defence
            /// <summary>Seconds between presses of each ability (its cooldown, used near it), and the
            /// share of parry presses that meet a blow.</summary>
            public const float DashEvery = D.DashCooldown + 1f, BarrierEvery = D.BarrierCooldown + D.BarrierSeconds + 1f;
            public const float BulwarkEvery = D.BulwarkCooldown + 1f, ParryEvery = 4f, ParryLands = 0.6f;
            /// <summary>Blows a dome blocks a use (and the share that are shots), blows a Bulwark takes,
            /// bodies a dash passes.</summary>
            public const float DomeBlocks = 1f, ShotShare = 0.3f, BulwarkBlows = 0.8f, DashPasses = 1f;

            // ---- control into blows
            /// <summary>Enemies taking turns at you; the share of control that lands on one about to
            /// strike (a telegraph started still lands; a body held far off was no threat).</summary>
            public const float Attackers = 2.5f, ControlLands = 0.5f;
            /// <summary>Stagger-equivalent seconds of a push a step, a root's second, a slow's second
            /// (per share slowed), a held-off turn's second, a pull a step (it brings them to you).</summary>
            public const float PushStep = 0.3f, RootSecond = 0.6f, SlowSecond = 0.4f, DenySecond = 1f, PullStep = 0.05f;
            /// <summary>Hit units a pull a step is worth: the body joins the next swings.</summary>
            public const float PullStepHitUnits = 0.3f;

            // ---- statuses
            /// <summary>The share of a soaked body's damage that is yours to take (you fight it).</summary>
            public const float SoakedFought = 0.5f;
            /// <summary>Hit units a status's whole damage is worth on your element (burn, bleed), and the
            /// share of bodies under a status a moment you look.</summary>
            public const float StatusHitUnitsPerSecond = 0.5f, StatusedShare = 0.4f;
            /// <summary>Share of the time a Fire character's hits already ignite (its window after a release).</summary>
            public const float IgniteShare = Tuning.Elements.Fire.IgniteSeconds / Assume.ReleaseEvery * 0.8f;
            /// <summary>The meter a release spends.</summary>
            public const float MeterPerRelease = 1f;

            // ---- worth beyond the model (judged): survival of a step, a second of speed
            /// <summary>EHP a repositioning step is worth, per step a second; +100 Move Speed held.</summary>
            public const float StepWorth = 0.15f, MoveWorth = 0.08f;

            // ---- the room and the host
            /// <summary>Share of a floor's blows that come from ranged bodies; share of enemies that
            /// are elites; share of a floor spent near a pit or in one's path; share of strikes on
            /// where you stood that still find you.</summary>
            public const float RangedBlowShare = 0.3f, EliteShare = 0.15f, PitShare = 0.05f, StormFinds = 0.25f;
            /// <summary>Bodies on screen at the pressure cap; turns open at once.</summary>
            public const float BodiesOnScreen = 6f, TurnsAtOnce = 2f;
            /// <summary>Share of a release from under half meter; of arts landed GOOD / BAD.</summary>
            public const float GoodShare = 0.35f, BadShare = 0.15f;
            /// <summary>Seconds a floor lasts at a boss or a long room (Fatigue, Narrowing reach these).</summary>
            public const float LongFloorShare = 0.15f, LongFloorSeconds = 150f;
            /// <summary>A Martyr's burst finds you; a defence pressed with nothing to meet (Off Balance).</summary>
            public const float MartyrFinds = 0.3f, EmptyParryShare = 0.3f;
            /// <summary>Seconds the first wave stays unaware.</summary>
            public const float UnawareSeconds = 3f;
            /// <summary>Spires taken a floor.</summary>
            public const float SpiresPerFloor = 0.3f;
        }

        public struct OverhaulTerms
        {
            public float Dps, Crowd, DpsMul, Incoming, EhpMul, Heal, Bleed;
            public static OverhaulTerms None => new() { DpsMul = 1f, Incoming = 1f, EhpMul = 1f };
        }

        /// <summary>
        /// The overhaul's entries on one build: damage added on the body fought and on the crowd, the
        /// chain's speed, blows taken, survival judged, and health paid or healed a second.
        /// </summary>
        static OverhaulTerms LedgerOverhaul(Build b, Func<string, int> n, ChainShape c, float hu, float targetDps,
                                            float releaseHit, float releasesPerSecond, float maxHp, int floor, float soaked)
        {
            var o = OverhaulTerms.None;
            var element = b.Element;
            float sec = Mathf.Max(0.1f, c.Seconds);
            float arts = 1f / sec;                                  // every art
            float sock = arts / Assume.RotationSockets;            // one socket's art
            float basics = c.Basics / sec;
            float art = c.ArtHit, basic = c.BasicHit;
            float artShare = art / Mathf.Max(1e-3f, c.Basics * basic + art);
            float kills = (targetDps * (1f + Assume.CrowdBodies * 0.5f)) / Mathf.Max(1f, ChaserEhp(floor));
            float blow = ChaserHit(floor);
            float critGain = (c.CritMul - 1f) / Mathf.Max(1f, c.CritFactor);
            float dash = 1f / Rates.DashEvery, dome = 1f / Rates.BarrierEvery, bulwark = 1f / Rates.BulwarkEvery;
            float parries = Rates.ParryLands / Rates.ParryEvery;

            // Water already soaks what it hits: a coat's soak is worth only the time it wasn't.
            float soakedUptime = Mathf.Clamp01((soaked - 1f) / (Tuning.Elements.Water.SoakVulnerability - 1f));
            float control = 0f, haste = 0f, coats = 0f, saved = 0f, ehp = 0f, meter = 0f;

            // Form strength alone: stacks grow the measure (FormStackStep; a Concord ConcordStackStep).
            float F(string id)
            {
                int k = n(id);
                if (k <= 0) return 0f;
                var e = Exchange.ExchangeCatalog.Get(id);
                if (e == null || !e.Stackable || e.Slot == Exchange.FormSlot.None) return T2.Measure(id);
                float step = e.IsConcord ? T2.ConcordStackStep : T2.FormStackStep;
                return (1f + step * (Mathf.Min(k, e.MaxStacks) - 1)) * T2.Measure(id);
            }
            bool H(string id) => n(id) > 0;
            bool Max(string id) => AtMax(n, id);

            void Hit(float perSecond, float hitUnits) => o.Dps += perSecond * hitUnits * hu;
            void Others(float perSecond, float hitUnits) => o.Crowd += perSecond * hitUnits * hu;
            void Damage(float perSecond, float amount) => o.Dps += perSecond * amount;
            void Spill(float perSecond, float amount) => o.Crowd += perSecond * amount;
            // A coat's burn, bleed and soak merge by the strongest (StatusEffects): coats SETTLE as
            // uptime at the end - a body coated again inside the status's seconds gains nothing more.
            float coatOn = 0f, coatOnStrength = 0f, coatOff = 0f, coatOffStrength = 0f;
            void Coat(float perSecond, float strength, bool onTarget = false)
            {
                if (perSecond <= 0f || strength <= 0f) return;
                coats += perSecond;
                float rate = perSecond * strength;
                if (onTarget) { coatOn += perSecond; coatOnStrength += rate; }
                else { coatOff += perSecond; coatOffStrength += rate; }
                switch (element)
                {
                    case ElementType.Water: control += rate * T2.CoatPull * Rates.PullStep; break;
                    case ElementType.Earth: control += rate * T2.CoatStaggerSeconds * Power(b.StaggerPoints); break;
                    case ElementType.Air: control += rate * T2.CoatPush * Rates.PushStep; break;
                }
            }
            void Stagger(float perSecond, float seconds) => control += perSecond * seconds;
            void Root(float perSecond, float seconds) => control += perSecond * seconds * Rates.RootSecond;
            void Slow(float perSecond, float seconds, float speedKept) => control += perSecond * seconds * (1f - speedKept) * Rates.SlowSecond;
            void Push(float perSecond, float steps) => control += perSecond * steps * Rates.PushStep;
            void Deny(float perSecond, float seconds) => control += perSecond * seconds * Rates.DenySecond;
            void Pull(float perSecond, float steps)
            {
                control += perSecond * steps * Rates.PullStep;
                Others(perSecond * steps, Rates.PullStepHitUnits);
            }
            void Haste(float perChain) => haste += perChain;
            // Attack-speed points for a number of basics, as seconds a second saved.
            void Quick(float perSecond, float basicsQuickened, float points)
                => saved += perSecond * basicsQuickened * c.Interval * (1f - 1f / (1f + points / 100f));
            void Crit(float perSecond, float add, float hit) => o.Dps += perSecond * add * critGain * hit;
            void Steps(float perSecond, float steps) => ehp += perSecond * steps * Rates.StepWorth;
            void Pay(float perSecond, float shareOfMax) => o.Bleed += perSecond * shareOfMax * maxHp;
            void Heal(float perSecond, float shareOfMax) => o.Heal += perSecond * shareOfMax * maxHp;
            void Meter(float perSecond, float share) => meter += perSecond * share;

            // ============================================================ ART ROLE
            // ---- Impact
            if (H("ar_collide")) Others(sock * Rates.NeighbourShare * Rates.PullBodies, T2.CollideHitUnits * F("ar_collide"));
            if (H("ar_bowl")) Others(sock * Rates.PushBodies * Rates.NeighbourShare * 0.5f, T2.BowlHitUnits * F("ar_bowl"));
            if (H("ar_slipstream")) Others(sock * Rates.PathBodies, T2.SlipstreamHitUnits * F("ar_slipstream"));
            if (H("ar_return"))
            {
                Hit(sock * Rates.WardStops * Assume.FromTargetShare, T2.ReturnHitUnits * F("ar_return"));
                Stagger(sock * Rates.WardStops, T2.ReturnStagger * F("ar_return"));
            }
            if (H("ar_carry")) Spill(sock * Rates.ArtKillShare * Rates.NeighbourShare, art * T2.CarryShare * F("ar_carry"));
            if (H("ar_link")) Spill(sock, targetDps * T2.LinkSeconds * T2.LinkShare * F("ar_link") * (Rates.SweepBodies - 1f));
            if (H("ar_spread")) Spill(sock * Rates.SetupBodies * Rates.NeighbourShare, art * T2.SpreadShare * F("ar_spread"));
            // ---- Control
            if (H("ar_held")) Stagger(sock * Rates.PullBodies, T2.HeldStagger * F("ar_held"));
            if (H("ar_slam"))
            {
                Hit(sock * Rates.PushMeetsShare, T2.SlamHitUnits * F("ar_slam"));
                Stagger(sock * Rates.PushMeetsShare, T2.SlamStagger * F("ar_slam"));
            }
            if (H("ar_decoy")) Deny(Mathf.Min(sock, 1f / (T2.DecoySeconds + T2.DecoyGapSeconds)) * BodiesWithin(T2.DecoyRadius) * 0.5f, T2.DecoySeconds * F("ar_decoy"));
            if (H("ar_shockfront"))
            {
                float bodies = BodiesWithin(T2.ShockfrontRadius);
                Push(sock * bodies, T2.ShockfrontPush * F("ar_shockfront"));
                Stagger(sock * bodies, T2.ShockfrontStagger * F("ar_shockfront"));
            }
            if (H("ar_daze")) Stagger(sock, T2.DazeStagger * F("ar_daze"));
            if (H("ar_mire")) Slow(sock * Rates.SweepBodies, T2.MireSeconds * F("ar_mire"), T2.MireSlow);
            if (H("ar_snare")) Root(sock * Rates.SetupBodies, T2.SnareRoot * F("ar_snare"));
            // ---- Tempo (the shared haste bank)
            if (H("ar_reel")) Haste(Rates.ThreePulledShare * T2.ReelHaste * F("ar_reel") / Assume.RotationSockets);
            if (H("ar_breathing")) Quick(sock * Rates.ClearAfterPushShare, T2.BreathingBasics, T2.BreathingSpeed * F("ar_breathing"));
            if (H("ar_arrival")) Crit(sock, T2.ArrivalCrit * F("ar_arrival"), basic);
            if (H("ar_steady")) Haste(Rates.WardStops * T2.SteadyHastePerStop * F("ar_steady") / Assume.RotationSockets);
            if (H("ar_cull")) Haste(Rates.ArtKillShare * T2.CullHaste * F("ar_cull") / Assume.RotationSockets);
            if (H("ar_harvest")) Haste(Rates.FourStruckShare * T2.HarvestHaste * F("ar_harvest") / Assume.RotationSockets);
            if (H("ar_quickfuse")) Haste(T2.QuickFuseHaste * F("ar_quickfuse") / Assume.RotationSockets);
            // ---- Payoff (primes the next art)
            if (H("ar_drawn_in")) Damage(sock * Rates.PayoffLandsShare, art * T2.DrawnInShare * F("ar_drawn_in"));
            if (H("ar_flank")) Damage(sock * Rates.PushMeetsShare * Assume.FromTargetShare, targetDps * T2.FlankSeconds * T2.FlankExposed * F("ar_flank"));
            if (H("ar_ambush")) Damage(sock * Rates.AmbushWindowShare, art * T2.AmbushShare * F("ar_ambush"));
            if (H("ar_charge")) Damage(sock, art * T2.ChargePerStop * Rates.WardStops * F("ar_charge"));
            if (H("ar_expose")) Damage(sock * Rates.PayoffLandsShare, art * T2.ExposeShare * F("ar_expose"));
            if (H("ar_gather")) Damage(sock, art * T2.GatherPerBody * Mathf.Max(0f, Rates.SweepBodies - 2f) * F("ar_gather"));
            if (H("ar_deepen")) Damage(sock * Rates.PayoffLandsShare, art * T2.DeepenShare * F("ar_deepen"));
            // ---- Contagion (spreads the coat)
            if (H("ar_undertow")) Coat(sock * Rates.PullBodies * (1f + Rates.NeighbourShare), F("ar_undertow"));
            if (H("ar_wake")) Coat(sock * Rates.PushBodies, F("ar_wake"));
            if (H("ar_slipcoat")) Coat(sock * Rates.PathBodies, F("ar_slipcoat"));
            if (H("ar_taint")) Coat(sock * Rates.WardStops, F("ar_taint"), true);
            if (H("ar_transfuse")) Coat(sock * Rates.NeighbourShare * Rates.StatusedShare * 2f, F("ar_transfuse"));
            if (H("ar_wash")) Coat(sock * Rates.SweepBodies * Rates.NeighbourShare, F("ar_wash"));
            if (H("ar_prime")) Coat(sock * Rates.SetupBodies, F("ar_prime"), true);

            // ============================================================ ART CRAFT
            if (H("ac_step")) { Steps(sock, T2.StepDistance * F("ac_step") * 0.5f); o.DpsMul *= 1f + 0.01f * F("ac_step"); }
            if (H("ac_stride")) ehp += sock * T2.StrideSeconds * T2.StrideMove * F("ac_stride") / 100f * Rates.MoveWorth;
            if (H("ac_leap")) Steps(sock, T2.LeapDistance * F("ac_leap"));
            if (H("ac_quicken")) Quick(sock, 1f, T2.QuickenSpeed * F("ac_quicken"));
            if (H("ac_rebound")) Haste(Rates.ThreeStruckShare * T2.ReboundHaste * F("ac_rebound") / Assume.RotationSockets);
            if (H("ac_settle")) Haste(T2.SettleChance * F("ac_settle") / Assume.RotationSockets);
            if (H("ac_flick")) Push(sock, T2.FlickPush * F("ac_flick"));
            if (H("ac_shove")) Push(sock * Rates.SweepBodies, T2.ShovePush * F("ac_shove"));
            if (H("ac_gravity")) Pull(sock * BodiesWithin(T2.GravityRadius), T2.GravityPull * F("ac_gravity"));
            if (H("ac_spark")) Spill(sock * Rates.NeighbourShare, art * T2.SparkShare * F("ac_spark"));
            if (H("ac_fan")) Spill(sock * Rates.SweepBodies * Rates.NeighbourShare, art * T2.FanShare * F("ac_fan"));
            if (H("ac_debris")) Push(sock * BodiesWithin(T2.DebrisRadius), T2.DebrisPush * F("ac_debris"));
            // Gilt: a PERFECT lifts the socket's shared forms to full - priced as a third more from two
            // forms sharing it, half the time.
            if (H("ac_gilt")) o.DpsMul *= 1f + Assume.PerfectShare * 0.25f * 0.1f * F("ac_gilt");
            if (H("ac_steady_hand")) Coat(sock * Assume.PerfectShare * Rates.StreakShare * Rates.SweepBodies, F("ac_steady_hand"));
            if (H("ac_flare")) Others(sock * Assume.PerfectShare * Rates.SetupBodies, T2.FlareHitUnits * F("ac_flare"));
            if (H("ac_primer")) Damage(arts * Rates.PayoffLandsShare / Assume.RotationSockets, art * T2.PrimerShare * F("ac_primer"));
            if (H("ac_relay")) Coat(sock * Rates.SweepBodies, F("ac_relay"));
            if (H("ac_reader")) Damage(sock, art * T2.ReaderShare * 0.6f * F("ac_reader"));
            if (H("ac_full_turn"))
            {
                Damage(sock * Rates.FullTurnShare, art * T2.FullTurnShare * F("ac_full_turn"));
                Coat(sock * Rates.FullTurnShare * Rates.SweepBodies, F("ac_full_turn"));
            }
            if (H("ac_seam")) Others(sock * Rates.SetupBodies, T2.SeamHitUnits * F("ac_seam"));
            if (H("ac_skewer")) Others(sock * Rates.NeighbourShare * 2f, T2.SkewerHitUnits * F("ac_skewer"));
            if (H("ac_inward_orbit")) Pull(sock * Rates.SweepBodies, T2.InwardOrbitPull * F("ac_inward_orbit"));
            if (H("ac_art_splinter")) Spill(sock * Rates.NeighbourShare, art * T2.ArtSplinterShare * F("ac_art_splinter"));
            if (H("ac_recoil_step")) Steps(sock, T2.RecoilStepDistance * F("ac_recoil_step"));
            if (H("ac_barbed")) Damage(sock, art * T2.BarbedBleedShare * T2.BarbedBleedSeconds * F("ac_barbed"));
            if (H("ac_kneeling_shot")) Others(sock * Rates.NeighbourShare, T2.KneelingHitUnits * F("ac_kneeling_shot"));
            if (H("ac_brand_blade")) Coat(sock * Rates.SweepBodies, F("ac_brand_blade") * 0.8f);
            if (H("ac_charged_disc")) Coat(sock * Rates.SweepBodies, F("ac_charged_disc"));
            if (H("ac_tipped")) Coat(sock * Rates.SweepBodies, F("ac_tipped"));
            if (H("ac_tempered_edge")) Coat(sock, 2f * F("ac_tempered_edge"), true);
            if (H("ac_wake_rings")) Coat(sock * Rates.SetupBodies, F("ac_wake_rings"));
            if (H("ac_trailing_fletch")) Coat(sock * Rates.SetupBodies, F("ac_trailing_fletch"));

            // ============================================================ CHAIN (one basic a chain unless stated)
            float chains = arts;
            if (H("ch_connector")) Coat(chains * Rates.BasicBodies, F("ch_connector"), true);
            if (H("ch_lunge_step")) Steps(chains, T2.LungeStepDistance * F("ch_lunge_step") * 0.5f);
            if (H("ch_follow_through")) Spill(basics * Rates.BasicKillShare * Rates.NeighbourShare, basic * T2.FollowThroughShare * F("ch_follow_through"));
            if (H("ch_ember_arc")) Coat(basics / T2.EmberArcEvery * Rates.BasicBodies, F("ch_ember_arc"), true);
            if (H("ch_shear")) Spill(basics * Rates.NeighbourShare, basic * T2.ShearShare * F("ch_shear"));
            if (H("ch_coated_bounce")) Coat(basics * Rates.BouncesPerBasic, F("ch_coated_bounce"));
            if (H("ch_pinning_shot")) Root(basics * Rates.NearWallShare, T2.PinningRoot * F("ch_pinning_shot"));
            if (H("ch_backstep")) Steps(basics * 0.5f, T2.BackstepDistance * F("ch_backstep"));
            if (H("ch_carried_tip")) Coat(basics, F("ch_carried_tip"), true);
            if (H("ch_coated_opener")) Coat(chains * Rates.BasicBodies, F("ch_coated_opener"), true);
            if (H("ch_coated_catch")) Coat(basics * Rates.CatchesPerBasic, F("ch_coated_catch"));
            if (H("ch_coated_kill")) Coat(basics * Rates.BasicKillShare, F("ch_coated_kill"));

            // ============================================================ RELEASE (a release a second: releasesPerSecond)
            float rel = releasesPerSecond;
            if (H("re_gathered")) Pull(rel * BodiesWithin(T2.GatheredRadius), T2.GatheredPull);
            if (H("re_clearing")) Push(rel * BodiesWithin(T2.ClearingRadius), T2.ClearingPush);
            // Consume cashes what would burn anyway: worth the time it saves, a quarter.
            if (H("re_consume") && element == ElementType.Fire)
                Others(rel * BodiesWithin(T2.ConsumeRadius) * Rates.StatusedShare, Rates.StatusHitUnitsPerSecond * T2.CoatBurnSeconds * 0.25f);
            if (H("re_fuel") && element == ElementType.Fire) Meter(kills * Rates.StatusedShare, T2.FuelRefund);
            if (H("re_current") && element == ElementType.Water) Meter(kills * Rates.StatusedShare, T2.CurrentRefund);
            if (H("re_draw") && element == ElementType.Water) Pull(rel * BodiesWithin(T2.DrawRadius) * Rates.StatusedShare, T2.DrawPull);
            if (H("re_landslide") && element == ElementType.Earth)
            {
                float bodies = BodiesWithin(T2.LandslideRadius);
                Push(rel * bodies, T2.LandslidePush);
                Others(rel * bodies * Rates.PushMeetsShare, T2.SlamHitUnits);
            }
            if (H("re_rubble") && element == ElementType.Earth) Slow(rel * BodiesWithin(T2.RubbleRadius) * 0.6f, T2.RubbleSeconds, 0.6f);
            if (H("re_funnel") && element == ElementType.Air) Pull(rel * BodiesWithin(T2.FunnelRadius) * T2.FunnelSeconds, T2.FunnelPull);
            if (H("re_carry") && element == ElementType.Air) Steps(rel, T2.CarryDistance);
            void Skeleton(string id, float bodies)
            {
                if (!H(id)) return;
                float m = T2.Measure(id);
                Others(rel * bodies, T2.SkeletonHitUnits * m);
                Coat(rel * bodies, m);
            }
            Skeleton("re_thrown_through", 1.5f);
            Skeleton("re_edge_cone", BodiesWithin(T2.SkeletonReach) * 0.33f);
            Skeleton("re_rim_discs", BodiesWithin(3.5f) - BodiesWithin(2f));
            Skeleton("re_answering_volley", BodiesWithin(5f));
            Skeleton("re_ring_stakes", BodiesWithin(3f) - BodiesWithin(2.2f) + 0.5f);
            if (H("re_seeded_orbit"))
            {
                float m = T2.Measure("re_seeded_orbit"), ticks = rel * T2.SkeletonOrbitSeconds * 4f * BodiesWithin(T2.SkeletonOrbitRadius);
                Others(ticks, 0.3f * m);
                Coat(ticks, 0.5f * m);
            }

            // ============================================================ GUARD (by ability)
            if (H("gd_cutting_dash")) Others(dash * Rates.DashPasses, T2.CuttingDashHitUnits * F("gd_cutting_dash"));
            if (H("gd_afterimage")) Deny(dash * BodiesWithin(T2.DecoyRadius) * 0.5f, T2.DecoySeconds * F("gd_afterimage"));
            if (H("gd_landing")) Crit(dash, T2.LandingCrit * F("gd_landing"), basic);
            if (H("gd_wake_dash")) Slow(dash * Rates.DashPasses, T2.WakeDashSeconds * F("gd_wake_dash"), T2.WakeDashSlow);
            if (H("gd_rebuke")) Others(dome * Rates.DomeBlocks * Rates.ShotShare, T2.RebukeHitUnits * F("gd_rebuke"));
            if (H("gd_plant")) Stagger(dome * Rates.DomeBlocks * (1f - Rates.ShotShare), T2.PlantStagger * F("gd_plant"));
            if (H("gd_sap")) Meter(dome * Rates.DomeBlocks, T2.SapRefund * F("gd_sap"));
            if (H("gd_bulldoze")) Push(dome * BodiesWithin(D.BarrierDomeRadius + 1f) * 0.5f, 1f * F("gd_bulldoze"));
            if (H("gd_backlash"))
                Spill(bulwark * Rates.BulwarkBlows * BodiesWithin(T2.BacklashRadius), blow * T2.BacklashShare * F("gd_backlash"));
            if (H("gd_mire_aura")) Slow(bulwark * BodiesWithin(T2.AuraRadius), D.BulwarkSeconds * F("gd_mire_aura"), T2.AuraSlow);
            if (H("gd_hold")) Push(bulwark * Rates.BulwarkBlows, T2.HoldPush * F("gd_hold"));
            if (H("gd_taunt")) Pull(bulwark * BodiesWithin(T2.TauntRadius), T2.TauntPull * F("gd_taunt"));
            if (H("gd_counterweight")) Stagger(parries, T2.CounterweightStagger * F("gd_counterweight"));
            if (H("gd_disarm")) Deny(parries, T2.DisarmSeconds * F("gd_disarm"));
            if (H("gd_read")) Crit(parries * T2.ReadHits, T2.ReadCrit * F("gd_read"), (basic + art) * 0.5f);
            if (H("gd_echo_guard")) Haste(parries * sec * T2.EchoGuardHaste * F("gd_echo_guard"));
            if (H("gd_coat_trail")) Coat(dash * Rates.DashPasses, F("gd_coat_trail"));
            if (H("gd_coat_dome")) Coat(dome * Rates.DomeBlocks, F("gd_coat_dome"));
            if (H("gd_coat_aura")) Coat(bulwark * (D.BulwarkSeconds / T2.AuraTick) * BodiesWithin(T2.AuraRadius), 0.5f * F("gd_coat_aura"));
            if (H("gd_coat_burst")) Coat(parries * BodiesWithin(T2.CoatBurstRadius), F("gd_coat_burst"));

            // ============================================================ CONCORDS (each strike coats)
            void Concord(string id, float perSecond, float bodies, float hitUnits)
            {
                if (!H(id)) return;
                float p = F(id);
                Others(perSecond * bodies, hitUnits * p);
                Coat(perSecond * bodies, p);
            }
            float orbitTicks = T2.ConcordOrbitSeconds / T2.ConcordTick, front = BodiesWithin(1.5f) * 0.4f;
            Concord("cc_gs_dash_a", dash, Rates.DashPasses, 1.5f);
            Concord("cc_gs_dash_b", dash, Rates.DashPasses, 1.5f);
            Concord("cc_gs_barrier_a", dome * Mathf.Min(1f, Rates.DomeBlocks), Rates.BasicBodies + Assume.CrowdBodies * 0.5f, 1f);
            Concord("cc_gs_barrier_b", dome * (D.BarrierSeconds / T2.ConcordTick), front, 0.4f);
            Concord("cc_gs_bulwark_a", bulwark * Rates.BulwarkBlows, 1f, 1f);
            Concord("cc_gs_bulwark_b", bulwark, BodiesWithin(2.5f), 2f);
            Concord("cc_gs_parry_a", parries, 1f, 1.5f);
            Concord("cc_gs_parry_b", parries / 3f, BodiesWithin(2.5f), 3f);
            Concord("cc_disc_dash_a", dash, Rates.DashPasses, 1f);
            Concord("cc_disc_dash_b", dash, 1f, 1f);
            Concord("cc_disc_barrier_a", dome * Rates.DomeBlocks, 1f, 1f);
            Concord("cc_disc_barrier_b", dome * (D.BarrierSeconds / T2.ConcordTick), front, 0.4f);
            Concord("cc_disc_bulwark_a", bulwark * (D.BulwarkSeconds / T2.ConcordTick), BodiesWithin(T2.ConcordOrbitRadius), 0.4f);
            Concord("cc_disc_bulwark_b", bulwark * Rates.BulwarkBlows, 1f, 1f);
            Concord("cc_disc_parry_a", parries * orbitTicks * F("cc_disc_parry_a"), BodiesWithin(T2.ConcordOrbitRadius), 0.4f);
            Concord("cc_disc_parry_b", parries, 1f, 1.5f);
            Concord("cc_bow_dash_a", dash, 1f, 1.5f);
            Concord("cc_bow_dash_b", dash * 0.6f, BodiesWithin(1.5f), 2f);
            Concord("cc_bow_barrier_a", dome * Rates.DomeBlocks * Rates.ShotShare, 1f, 1.2f);
            Concord("cc_bow_barrier_b", dome * (D.BarrierSeconds / 0.5f), 1f, 0.6f);
            Concord("cc_bow_bulwark_a", bulwark, BodiesWithin(2f), 1f);
            Concord("cc_bow_bulwark_b", bulwark * 3f, BodiesWithin(3f), 0.6f);
            if (H("cc_bow_parry_a"))
            {
                Root(parries, 1f * F("cc_bow_parry_a"));
                Coat(parries, F("cc_bow_parry_a"), true);
            }
            if (H("cc_bow_parry_b")) Damage(parries, basic * F("cc_bow_parry_b"));

            // ============================================================ HUMOUR (status growth and spread)
            float burnsOrBleeds = element == ElementType.Fire || element == ElementType.Air ? 1f : 0.3f;
            if (H("hu_deepening")) Damage(1f, Rates.StatusHitUnitsPerSecond * hu * burnsOrBleeds * T2.DeepeningCap * 0.5f);
            if (H("hu_settling")) control += kills * Rates.StatusedShare * Mathf.Min(T2.SettlingCap, T2.SettlingStep * 4f);
            if (H("hu_wildfire") && element == ElementType.Fire)
                Others(Rates.StatusedShare * Rates.NeighbourShare * 0.5f, Rates.StatusHitUnitsPerSecond * T2.WildfireShare);
            if (H("hu_ripple") && element == ElementType.Water) Coat(coats * 0.5f + arts * 0.3f, 0.5f);
            if (H("hu_splatter")) Others(kills * Rates.StatusedShare * burnsOrBleeds * Rates.NeighbourShare, T2.CoatBleedShare * T2.CoatBleedSeconds * T2.SplatterShare * 2f);
            if (H("hu_topple")) Stagger(kills * Rates.StatusedShare * BodiesWithin(T2.ToppleReach), T2.ToppleStagger);

            // ============================================================ FORTUNE
            if (H("fo_ambush")) o.DpsMul *= 1f + T2.AmbushTaken * (Rates.UnawareSeconds + T2.AmbushUnawareSeconds) / Assume.FloorSeconds * 0.5f;
            if (H("fo_spire_echo")) Coat(kills * Assume.SpireFloorShare, T2.Measure("fo_spire_echo"), true);

            // ============================================================ COSTS
            // ---- Toll: health paid each time
            int k;
            if ((k = n("co_spent_meter")) > 0)
                Pay(rel * Assume.MeterUnderHalfShare, T2.SpentMeterHealth * (1f + 0.5f * (k - 1)));
            if ((k = n("co_overdrawn")) > 0)
                Pay(arts * (Rates.BadShare + (Max("co_overdrawn") ? Rates.GoodShare : 0f)), T2.OverdrawnHealth * k);
            if ((k = n("co_fatigue")) > 0)
            {
                // The speed it takes, averaged over a floor (and the long floors' tails).
                float cap = Max("co_fatigue") ? 999f : T2.FatigueCap, lost = 0f;
                int steps = Mathf.RoundToInt(Assume.FloorSeconds);
                for (int t = 0; t < steps; t++)
                {
                    float down = Mathf.Min(cap, T2.FatigueSpeed * k * Mathf.Max(0f, Mathf.Floor((t - T2.FatigueAfter) / T2.FatigueEvery)));
                    lost += 1f - 1f / (1f + down / 100f);
                }
                o.DpsMul *= 1f - lost / steps;
            }
            // ---- Rule
            if ((k = n("co_overswing")) > 0) saved -= basics * Rates.WhiffShare * T2.OverswingStun * (Max("co_overswing") ? 2f : 1f);
            if ((k = n("co_unsteady")) > 0) o.DpsMul *= 1f - (1f - artShare) * T2.UnsteadyDamage * (Rates.MovingShotShare + (Max("co_unsteady") ? 0.5f * (1f - Rates.MovingShotShare) : 0f));
            if ((k = n("co_skid")) > 0) ehp -= dash * T2.SkidDistance * (Max("co_skid") ? 2f : 1f) * Rates.StepWorth * 2f;
            if ((k = n("co_narrow")) > 0) o.Incoming *= 1f + dome * D.BarrierSeconds * Assume.HitsTakenPerSecond * T2.NarrowDotAdd * k * 0.5f / Assume.HitsTakenPerSecond;
            if ((k = n("co_heavy_bulwark")) > 0) ehp -= bulwark * D.BulwarkSeconds * T2.HeavyBulwarkMove * (Max("co_heavy_bulwark") ? 2f : 1f) / 100f * Rates.MoveWorth * 4f;
            if ((k = n("co_off_balance")) > 0)
                o.Incoming *= 1f + Rates.EmptyParryShare / Rates.ParryEvery * T2.OffBalanceStun * (Max("co_off_balance") ? 2f : 1f) * Assume.HitsTakenPerSecond;
            if ((k = n("co_loose_catch")) > 0)
                saved -= basics * Rates.CatchesPerBasic * T2.LooseCatchChance * (1f + 0.5f * (k - 1)) * (Max("co_loose_catch") ? 2f : 1f) * T2.LooseCatchStun;
            // ---- Host: the enemies
            if ((k = n("co_affixed")) > 0) o.Incoming *= 1f + Rates.EliteShare * T2.AffixedChanceAdd * k * 0.5f * (Max("co_affixed") ? 1.3f : 1f);
            if ((k = n("co_crowded")) > 0)
            {
                float more = (T2.CrowdedPressure * k + (Max("co_crowded") ? 2f * T2.CrowdedPressure : 0f)) / Rates.BodiesOnScreen;
                o.Incoming *= 1f + more * 0.6f;
                o.Crowd += 0f;   // more bodies to cut is not worth: the floor's budget is the same
            }
            if (H("co_vigilant")) o.DpsMul *= 1f - 0.03f;
            if ((k = n("co_eager")) > 0) o.Incoming *= (1f + T2.EagerTurns * k / Rates.TurnsAtOnce * 0.5f) * (Max("co_eager") ? 1.08f : 1f);
            if ((k = n("co_swift")) > 0) o.Incoming *= 1f + T2.SwiftMove * k * 0.35f + (Max("co_swift") ? Rates.EliteShare * T2.SwiftMove * 0.35f : 0f);
            if ((k = n("co_wrath")) > 0)
            {
                float mul = Mathf.Lerp(1f, T2.WrathCooldownMul, k / 3f);
                float share = Max("co_wrath") ? T2.WrathBelow : Rates.EliteShare * T2.WrathBelow;
                o.Incoming *= 1f + share * (1f / mul - 1f);
            }
            if ((k = n("co_martyrs")) > 0) Pay(kills * Rates.MartyrFinds * (Max("co_martyrs") ? 1.4f : 1f), T2.MartyrHealth * k);
            if ((k = n("co_plated")) > 0)
                o.DpsMul /= 1f + T2.PlatedShare * k * (Max("co_plated") ? 1f : 1f - Rates.EliteShare) * 0.7f;
            if ((k = n("co_reinforced")) > 0)
            {
                float extra = Rates.EliteShare * (k * T2.ReinforcedBodies + (Max("co_reinforced") ? 1 : 0));
                o.DpsMul /= 1f + extra;
                o.Incoming *= 1f + extra * 0.5f;
            }
            if (H("co_volley")) o.Incoming *= 1f + Rates.RangedBlowShare * T2.VolleyExtraBolts * 0.5f;
            if (H("co_twisted")) o.Incoming *= 1.02f;   // one boss a band harder: a floor in fifty
            // ---- Ground: the room
            if ((k = n("co_storm")) > 0) Pay(1f / T2.StormEvery * (Max("co_storm") ? 2f : 1f) * Rates.StormFinds, T2.StormHealth);
            if ((k = n("co_gale_floor")) > 0) ehp -= 1f / T2.GaleEvery * (Max("co_gale_floor") ? 1.5f : 1f) * T2.GalePush * Rates.StepWorth * 2f * k;
            if ((k = n("co_sinking")) > 0) ehp -= Assume.StillAfterSecondShare * T2.SinkingMove * (Max("co_sinking") ? 2f : 1f) / 100f * Rates.MoveWorth * 3f * k;
            if (H("co_slick")) ehp -= 0.02f;
            float pitBite = Rates.PitShare * blow / Mathf.Max(1f, maxHp);
            if ((k = n("co_encroach")) > 0) Pay(1f / T2.EncroachEvery * (Max("co_encroach") ? 2f : 1f) * 0.3f, 0.04f);
            if ((k = n("co_bloom")) > 0) Pay(1f, pitBite * T2.BloomTaken * k * 0.2f);
            if ((k = n("co_narrowing")) > 0)
            {
                float after = T2.NarrowingAfter * (Max("co_narrowing") ? 0.5f : 1f);
                float share = Rates.LongFloorShare * Mathf.Max(0f, Rates.LongFloorSeconds - after) / Rates.LongFloorSeconds
                            + (Assume.FloorSeconds > after ? (Assume.FloorSeconds - after) / Assume.FloorSeconds : 0f);
                Pay(share * 0.15f * (1f + 0.3f * (k - 1)), T2.NarrowingHealth);
            }
            if ((k = n("co_crumble")) > 0) ehp -= 0.015f * k * (Max("co_crumble") ? 2f : 1f);
            if ((k = n("co_dim")) > 0) o.Incoming *= 1f + 0.015f * k * (Max("co_dim") ? 1.5f : 1f);
            if (H("co_tide")) ehp -= 0.02f;
            if ((k = n("co_spite")) > 0) Pay(Rates.SpiresPerFloor / Assume.FloorSeconds, T2.SpiteHealth * k);
            if (H("co_dead_ground")) Pay(1f, pitBite * (T2.DeadGroundMul - 1f) * 0.2f * Assume.OathHeldShare);
            // ---- Socket: paid on the slot; III's curse costs 2% each time the action fires
            if ((k = n("co_slow_link")) > 0) Quick(chains, 1f, -T2.SlowLinkSpeed * k);
            if ((k = n("co_planted_link")) > 0) ehp -= chains * c.Interval * Rates.MoveWorth;
            if ((k = n("co_dull_link")) > 0) o.DpsMul *= 1f - 0.02f * k;
            if ((k = n("co_spill")) > 0) Meter(rel, -T2.SpillShare * (1f + 0.5f * (k - 1)));
            if ((k = n("co_damp")) > 0) o.Crowd -= rel * releaseHit * Mathf.Max(0f, BodiesWithin(4f) - BodiesWithin(4f * T2.DampRadiusMul)) * 0.5f;
            if ((k = n("co_kick")) > 0) ehp -= rel * T2.KickDistance * Rates.StepWorth * 2f * k;
            if ((k = n("co_strain")) > 0) o.Incoming *= 1f + Assume.DefenseShare * T2.StrainCooldown * k;
            if ((k = n("co_brittle_guard")) > 0) o.Incoming *= 1f + Assume.DefenseShare * (1f - T2.BrittleGuardWindow) * k;
            if ((k = n("co_exertion")) > 0) Pay(1f / Assume.DefenseUseSeconds, T2.ExertionHealth * k);
            if (Max("co_slow_link") || Max("co_planted_link")) Pay(chains, 0.02f);
            if (Max("co_spill") || Max("co_damp") || Max("co_kick")) Pay(rel, 0.02f);
            if (Max("co_strain") || Max("co_brittle_guard") || Max("co_exertion")) Pay(1f / Assume.DefenseUseSeconds, 0.02f);
            foreach (var curse in new[] { "heavy_hand", "dulled_script", "brittle_script" })
                if (Max(curse)) Pay(sock, 0.02f);

            // ============================================================ THE NEW ALBEDOS
            if (H("al_spent_meter")) Heal(rel * Assume.MeterUnderHalfShare, T2.SpentMeterHeal);
            if (H("al_overdrawn")) Damage(arts * (1f - Assume.PerfectShare), art * T2.OverdrawnArt);
            if (H("al_fatigue"))
            {
                float points = 0f;
                int steps = Mathf.RoundToInt(Assume.FloorSeconds);
                for (int t = 0; t < steps; t++)
                    points += Mathf.Min(T2.SecondWindDamageCap, T2.SecondWindDamage * Mathf.Max(0f, Mathf.Floor((t - T2.FatigueAfter) / T2.FatigueEvery)));
                o.Dps += targetDps * points / steps / 100f;   // Damage points, roughly a percent each
            }
            if (H("al_overswing")) Others(basics * Rates.WhiffShare * Rates.SetupBodies, T2.OverswingCrescentShare);
            if (H("al_unsteady")) Spill(basics * Rates.MovingShotShare * Rates.NeighbourShare * 0.5f, basic);
            if (H("al_skid")) Others(dash * BodiesWithin(1.5f), T2.SkidCutHitUnits);
            if (H("al_narrow")) Stagger(dome * BodiesWithin(D.BarrierDomeRadius + 0.5f) * 0.5f, T2.NarrowEdgeStagger);
            if (H("al_heavy_bulwark")) Slow(bulwark * Rates.BulwarkBlows, T2.HeavyStillSeconds, T2.HeavyStillSlow);
            if (H("al_off_balance")) Push(Rates.EmptyParryShare / Rates.ParryEvery * BodiesWithin(2f), T2.OffBalancePush);
            if (H("al_affixed")) Heal(kills * Rates.EliteShare * T2.AffixedChanceAdd, T2.AffixedHeal);
            if (H("al_crowded")) o.DpsMul *= 1f + Mathf.Min(T2.CrowdedCap, T2.CrowdedPerBody * Mathf.Max(0f, Rates.BodiesOnScreen - T2.CrowdedFrom));
            if (H("al_eager")) o.DpsMul *= 1f + Assume.HitsTakenPerSecond * T2.EagerExposedSeconds * T2.EagerExposed * Assume.FromTargetShare;
            if (H("al_swift")) control += 0.02f;
            if (H("al_wrath")) Haste(kills * Rates.EliteShare * sec);
            if (H("al_martyrs")) Others(kills * BodiesWithin(T2.MartyrRadius), T2.MartyrEnemyHitUnits);
            if (H("al_storm")) Others(1f / T2.StormEvery, T2.StormEnemyHitUnits);
            if (H("al_gale_floor")) Push(1f / T2.GaleEvery * Rates.BodiesOnScreen * 0.5f, T2.GalePush);
            if (H("al_sinking")) Root(Assume.StillAfterSecondShare * Assume.HitsTakenPerSecond, T2.SinkingRoot);
            if (H("al_plated")) o.DpsMul *= 1f + Assume.ArmourBreaksPerSecond * T2.PlatedExposedSeconds * T2.PlatedExposed;
            if (H("al_reinforced")) Stagger(kills * Rates.EliteShare * BodiesWithin(T2.ReinforcedRadius), T2.ReinforcedStagger);
            if (H("al_encroach")) Others(1f / T2.EncroachEvery * 0.5f, T2.EncroachEnemyPitMul);
            if (H("al_bloom")) o.Dps += targetDps * Rates.PitShare * T2.BloomDamage / 100f;
            if (H("al_narrowing")) Slow(Rates.BodiesOnScreen * 0.2f, 1f, T2.NarrowingSlow);
            if (H("al_crumble")) Others(1f / T2.CrumbleEvery * 0.5f * BodiesWithin(T2.CrumbleRadius), T2.CrumbleHitUnits);
            if (H("al_dim")) o.DpsMul *= 1f + T2.LanternTaken * 0.4f;
            if (H("al_loose_catch")) Others(basics * Rates.CatchesPerBasic * T2.LooseCatchChance * BodiesWithin(T2.LooseCatchRadius), T2.LooseCatchBurstHitUnits);
            if (H("al_dull_link")) Coat(chains, 0.6f, true);
            if (H("al_spill")) Coat(rel * BodiesWithin(T2.SpillCoatRadius), T2.Measure("al_spill"));
            if (H("al_slow_link")) Damage(chains, basic * T2.SlowLinkArt);
            if (H("al_planted_link")) Stagger(chains * Rates.BasicBodies, T2.PlantedLinkStagger);
            if (H("al_damp")) Damage(rel, releaseHit * T2.DampCentreBonus);
            if (H("al_kick")) Others(rel * Rates.PathBodies, T2.KickCutHitUnits);
            if (H("al_strain")) Damage(1f / Assume.DefenseUseSeconds, art * Mathf.Min(0.3f, T2.StrainBank));
            if (H("al_brittle_guard")) Stagger(parries * BodiesWithin(T2.BrittleGuardRadius), T2.BrittleGuardStagger);
            if (H("al_exertion")) Heal(1f / Assume.DefenseUseSeconds, T2.ExertionHeal);
            if (H("al_heavy_hand")) o.Incoming *= 1f - c.LockShare / Assume.RotationSockets * (1f - T2.HeavyHandBlowMul);
            if (H("al_dulled_script")) Spill(sock * Rates.NeighbourShare, art * T2.DulledScriptSpread);
            if (H("al_brittle_script")) Stagger(sock * Rates.SweepBodies, T2.BrittleScriptStagger);

            // ============================================================ the four currencies, settled
            // The coats' statuses as uptime: on the body fought, capped at always; on the others, at
            // the crowd's bodies. Fire's own ignite already burns what it hits through its window.
            if (coatOn + coatOff > 0f && element != ElementType.Earth)
            {
                float seconds = element switch
                {
                    ElementType.Fire => T2.CoatBurnSeconds, ElementType.Water => T2.CoatSoakSeconds, _ => T2.CoatBleedSeconds,
                };
                float onUp = Mathf.Min(1f, coatOn * seconds), offUp = Mathf.Min(Assume.CrowdBodies, coatOff * seconds);
                float onS = coatOn > 0f ? coatOnStrength / coatOn : 0f, offS = coatOff > 0f ? coatOffStrength / coatOff : 0f;
                if (element == ElementType.Water)   // strength lengthens the soak: it is uptime, not size
                {
                    onUp = Mathf.Min(1f, coatOnStrength * seconds); offUp = Mathf.Min(Assume.CrowdBodies, coatOffStrength * seconds);
                    onS = offS = 1f;
                }
                switch (element)
                {
                    case ElementType.Fire:
                    {
                        float fresh = (1f - Rates.IgniteShare) * Power(b.BurnPoints);   // the board's Burn Power
                        o.Dps += onUp * onS * T2.CoatBurnShare * hu * fresh;
                        o.Crowd += offUp * offS * T2.CoatBurnShare * hu * fresh;
                        break;
                    }
                    case ElementType.Water:
                    {
                        float vuln = (Tuning.Elements.Water.SoakVulnerability - 1f) * Power(b.SoakPoints) * (1f - soakedUptime);
                        o.Dps += (onUp * onS + offUp / Mathf.Max(1f, Assume.CrowdBodies) * offS * Rates.SoakedFought) * vuln * targetDps;
                        break;
                    }
                    default:
                        o.Dps += onUp * onS * T2.CoatBleedShare * hu * Power(b.BleedPoints);
                        o.Crowd += offUp * offS * T2.CoatBleedShare * hu * Power(b.BleedPoints);
                        break;
                }
            }
            // Every coat feeds the meter, at most once every CoatFeedEvery.
            Meter(Mathf.Min(coats, 1f / T2.CoatFeedEvery), T2.CoatMeterFeed);
            if (meter != 0f) o.Dps += meter / Rates.MeterPerRelease * releaseHit;
            // The shared haste bank: a chain takes at most HasteBasicsPerChain basics from it.
            float h = Mathf.Min(T2.HasteBasicsPerChain, haste);
            if (h > 0f)
            {
                float before = (c.Basics * basic + art) / sec;
                float after = ((c.Basics - h) * basic + art) / Mathf.Max(0.1f, sec - h * c.Interval);
                o.DpsMul *= after / Mathf.Max(1e-3f, before);
            }
            if (saved != 0f) o.DpsMul /= Mathf.Max(0.5f, 1f - saved);
            // Control into blows: on the bodies taking turns, the share that lands on a strike, each
            // body capped by the control budget.
            float budget = T2.ControlBudgetSeconds / T2.ControlWindowSeconds;
            if (control != 0f)
                o.Incoming *= 1f - Rates.ControlLands * Mathf.Clamp(control / Rates.Attackers, -0.5f, budget);
            o.EhpMul *= Mathf.Max(0.5f, 1f + ehp);
            return o;
        }

        /// <summary>
        /// The price list as data (dev eval: Assay.Ratios()) - each overhaul entry's worth at one stack and
        /// at max against its target, unrounded, tab-separated: id, first, max, target, units. The tuning
        /// reads it; <see cref="Entries"/> is the one to read by eye.
        /// </summary>
        public static string Ratios(int floor = 50, string prefixes = "ar_ ac_ ch_ re_ gd_ cc_ hu_ fo_ co_ al_")
        {
            var sb = new System.Text.StringBuilder();
            var pre = prefixes.Split(' ');
            foreach (var e in Exchange.ExchangeCatalog.All)
            {
                if (Array.IndexOf(Unpriced, e.Id) >= 0 || Array.FindIndex(pre, x => e.Id.StartsWith(x)) < 0) continue;
                float bestFirst = float.MinValue, bestMax = float.MinValue;
                foreach (var b in PriceBuilds(e.Element))
                {
                    var bare = WorthOf(b, LedgerFor(b, e, 0, true), floor);
                    var one = WorthOf(b, LedgerFor(b, e, 1, true), floor);
                    var all = WorthOf(b, LedgerFor(b, e, e.MaxStacks, true), floor);
                    bool boon = e.Kind == Exchange.ExchangeKind.Boon;
                    float first = boon ? one.Dps / bare.Dps * one.Hp / bare.Hp - 1f : 1f - one.Dps / bare.Dps * one.Hp / bare.Hp;
                    float max = boon ? all.Dps / bare.Dps * all.Hp / bare.Hp - 1f : 1f - all.Dps / bare.Dps * all.Hp / bare.Hp;
                    if (max > bestMax) { bestMax = max; bestFirst = first; }
                }
                sb.Append($"{e.Id}\t{bestFirst:0.0000}\t{bestMax:0.0000}\t{StackTarget(e):0.00}\t{e.MaxStacks + (e.Stackable ? 1 : 0)}\n");
            }
            return sb.ToString();
        }

        /// <summary>
        /// What one coat is worth on each element (dev eval: Assay.Coats()) - the four should sit close,
        /// as damage x survival on the Max builds at <paramref name="floor"/>, from a coat a second.
        /// </summary>
        public static string Coats(int floor = 50)
        {
            var sb = new System.Text.StringBuilder("A coat a second (Prime on every art), alone, by element:\n");
            foreach (ElementType el in new[] { ElementType.Fire, ElementType.Water, ElementType.Earth, ElementType.Air })
            {
                var b = Make(Archetype.Striker, el, Kit.Max);
                var bare = WorthOf(b, LedgerFor(b, Exchange.ExchangeCatalog.Get("ar_prime"), 0, false), floor);
                var with = WorthOf(b, LedgerFor(b, Exchange.ExchangeCatalog.Get("ar_prime"), 1, false), floor);
                sb.Append($"{el,-6} damage {with.Dps / bare.Dps - 1f,6:P1}  survival {with.Hp / bare.Hp - 1f,6:P1}  " +
                          $"worth {with.Dps / bare.Dps * with.Hp / bare.Hp - 1f,6:P1}\n");
            }
            return sb.ToString();
        }
    }
}
