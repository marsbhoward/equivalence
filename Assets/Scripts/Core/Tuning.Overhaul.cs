namespace Convergence.Core
{
    public static partial class Tuning
    {
        /// <summary>
        /// THE EXCHANGE OVERHAUL (EXCHANGE-OVERHAUL.md, built 2026-11-04): kinds drawn first, gates,
        /// element coats, the four damping rules, and the new entries. Every number Claude's,
        /// unplayed - priced by the Assay where its model can see them, by hand where it can't.
        /// Damage is in the player's own hit units (PlayerController.HitUnit).
        /// </summary>
        public static class Overhaul
        {
            // ---------------------------------------------------------------- the draw

            /// <summary>THE KIND PITY: a kind not offered for this many deals jumps the queue.</summary>
            public const int KindPityDeals = 3;

            /// <summary>THE DRAW'S WEIGHTS (inside a kind): an entry that connects to the ledger, one that
            /// matches the crowd you gather or scatter, a status source past SourcesBeforeShift of its
            /// humour, and growth early or late in the run (by depth, 0-1).</summary>
            public const float ConnectWeight = 1.75f, IntentWeight = 1.5f, SourceShiftWeight = 0.35f;
            public const int SourcesBeforeShift = 2;
            public const float GrowthEarlyWeight = 2f, GrowthLateWeight = 0.5f, PayoffLateWeight = 1.3f;
            /// <summary>THE FLOOR CURVE (between kinds): Frailty leans early, Host and Ground late.</summary>
            public const float CurveEarly = 0.7f, CurveLate = 1.3f;

            /// <summary>
            /// THE MEASURE (the tuning, 2026-11-04): a per-entry strength for the entries whose card prints
            /// no number - the coats, the Concords, the release skeletons. It multiplies the form's
            /// Strength (RunEffects.Strength), so stacks and sharing still apply on top. Set from the Assay
            /// (Balance/AssayOverhaul.cs, Assay.Ratios) so each entry is worth its weight; 1 for any id not listed.
            /// </summary>
            public static float Measure(string id) => id switch
            {
                "ac_brand_blade" => 0.64f,
                "ac_charged_disc" => 0.52f,
                "ac_debris" => 4.11f,
                "ac_flick" => 5.00f,
                "ac_full_turn" => 0.63f,
                "ac_gilt" => 4.14f,
                "ac_gravity" => 5.00f,
                "ac_inward_orbit" => 5.00f,
                "ac_leap" => 2.03f,
                "ac_recoil_step" => 2.53f,
                "ac_relay" => 0.52f,
                "ac_shove" => 3.41f,
                "ac_steady_hand" => 3.47f,
                "ac_step" => 3.63f,
                "ac_tempered_edge" => 0.66f,
                "ac_tipped" => 0.52f,
                "ac_trailing_fletch" => 0.86f,
                "ac_wake_rings" => 0.86f,
                "al_spill" => 3.77f,
                "ar_prime" => 0.86f,
                "ar_slipcoat" => 1.25f,
                "ar_transfuse" => 2.31f,
                "ar_undertow" => 0.36f,
                "ar_wake" => 0.86f,
                "ar_wash" => 0.74f,
                "cc_bow_barrier_a" => 5.00f,
                "cc_bow_barrier_b" => 0.77f,
                "cc_bow_bulwark_a" => 1.34f,
                "cc_bow_bulwark_b" => 0.24f,
                "cc_bow_dash_a" => 2.18f,
                "cc_bow_dash_b" => 3.51f,
                "cc_bow_parry_a" => 1.88f,
                "cc_bow_parry_b" => 5.00f,
                "cc_disc_barrier_a" => 3.03f,
                "cc_disc_bulwark_a" => 0.51f,
                "cc_disc_bulwark_b" => 2.84f,
                "cc_disc_dash_a" => 2.27f,
                "cc_disc_dash_b" => 2.27f,
                "cc_disc_parry_a" => 0.80f,
                "cc_disc_parry_b" => 2.42f,
                "cc_gs_barrier_a" => 1.42f,
                "cc_gs_bulwark_a" => 2.84f,
                "cc_gs_bulwark_b" => 0.90f,
                "cc_gs_dash_a" => 2.18f,
                "cc_gs_dash_b" => 2.18f,
                "cc_gs_parry_a" => 2.42f,
                "cc_gs_parry_b" => 2.83f,
                "ch_backstep" => 0.69f,
                "ch_carried_tip" => 0.07f,
                "ch_coated_bounce" => 0.07f,
                "ch_coated_catch" => 0.19f,
                "ch_coated_kill" => 0.76f,
                "ch_coated_opener" => 0.14f,
                "ch_connector" => 0.14f,
                "ch_ember_arc" => 0.24f,
                "ch_lunge_step" => 1.70f,
                "fo_spire_echo" => 5.00f,
                "gd_bulldoze" => 5.00f,
                "gd_coat_aura" => 0.71f,
                "gd_coat_burst" => 1.13f,
                "gd_coat_dome" => 2.28f,
                "gd_coat_trail" => 1.61f,
                "gd_mire_aura" => 3.98f,
                "re_answering_volley" => 0.85f,
                "re_edge_cone" => 2.99f,
                "re_rim_discs" => 2.08f,
                "re_ring_stakes" => 2.54f,
                "re_seeded_orbit" => 0.87f,
                "re_thrown_through" => 2.61f,
                _ => 1f,
            };

            /// <summary>Concords at the brand deal: how many Concord slates it shows.</summary>
            public const int BrandConcordSlates = 2;

            /// <summary>FORM STACKS: each stack past the first grows the form's measure this much (II
            /// +35%); III is the Rubedo that frees it from its slot's sharing.</summary>
            public const float FormStackStep = 0.35f;

            /// <summary>A Concord's stacks: each past the first adds this much strength. III is +70%.</summary>
            public const float ConcordStackStep = 0.35f;

            // ---------------------------------------------------------------- the damping rules

            /// <summary>THE SHARED BANK: every "the next art comes a basic sooner" fills one bank, and a
            /// chain takes at most this many basics from it.</summary>
            public const int HasteBasicsPerChain = 1;

            /// <summary>THE CONTROL BUDGET: seconds of the ledger's control (stagger, root, a skipped
            /// turn) one body takes in a window before it shrugs it off for ImmuneSeconds.</summary>
            public const float ControlBudgetSeconds = 2.5f;
            public const float ControlWindowSeconds = 6f;
            public const float ControlImmuneSeconds = 2f;

            /// <summary>THE GENERATION COUNT: an effect set off by a form's effect sets off others
            /// at most this deep (Ripple, Wildfire, Topple, Splatter chains).</summary>
            public const int MaxFormDepth = 2;

            /// <summary>THE DECOY CLOCK: one decoy at a time, and this long after one ends before the next.</summary>
            public const float DecoyGapSeconds = 3f;
            public const float DecoyRadius = 3.5f;

            // ---------------------------------------------------------------- the coats

            public const float CoatBurnShare = 4.074f, CoatBurnSeconds = 3f;          // a second, of a hit
            public const float CoatSoakSeconds = 1.77f, CoatPull = 0.35f;
            public const float CoatStaggerSeconds = 1.55f;
            public const float CoatBleedShare = 2.509f, CoatBleedSeconds = 3f, CoatPush = 0.35f;
            /// <summary>The meter a coat feeds, at most once every CoatFeedEvery seconds.</summary>
            public const float CoatMeterFeed = 0.02f, CoatFeedEvery = 0.5f;
            /// <summary>COAT TIERS (Hades II's Infusions' kind): coated entries held for each tier,
            /// and what each tier adds to every coat. At the top tier a coat also reaches the nearest
            /// body within CoatSpreadRadius at half strength.</summary>
            public const int CoatTierEvery = 3;
            public const float CoatTierStep = 0.25f;
            public const float CoatSpreadRadius = 1.5f;

            // ---------------------------------------------------------------- art role forms

            public const float CollideHitUnits = 5.27f, CollideReach = 1.1f, CollideDelay = 0.35f;
            public const float HeldStagger = 0.99f, HeldDelay = 0.3f;
            public const float ReelBodies = 3f, ReelHaste = 2f;
            public const float BowlHitUnits = 6f, BowlReach = 0.9f, BowlDelay = 0.25f;
            public const float SlamHitUnits = 5.02f, SlamStagger = 1.67f, SlamReach = 0.8f, SlamDelay = 0.3f;
            public const float BreathingRadius = 2f, BreathingSpeed = 100f, BreathingDelay = 0.4f;
            public const int BreathingBasics = 2;
            public const float SlipstreamHitUnits = 6f, SlipstreamHalfWidth = 1f;
            public const float DecoySeconds = 1.5f;
            public const float ArrivalCrit = 0.5f, ArrivalWindow = 1f;
            public const float ReturnHitUnits = 3.14f, ReturnStagger = 1.25f;
            public const float ShockfrontRadius = 2f, ShockfrontPush = 2.17f, ShockfrontStagger = 0.54f;
            public const float SteadyHastePerStop = 1.58f;
            public const float CarryShare = 1.5f, CarryReach = 3f;
            public const float DazeStagger = 2f;
            public const float CullHaste = 2f;
            public const float LinkShare = 0.44f, LinkSeconds = 2f;
            public const float MireSlow = 0.6f, MireSeconds = 5.14f;
            public const int HarvestBodies = 4;
            public const float HarvestHaste = 2f;
            public const float SpreadShare = 1.06f, SpreadReach = 2.5f;
            public const float SnareRoot = 2f;
            public const float QuickFuseHaste = 1.87f;

            // ---------------------------------------------------------------- art craft forms

            public const float StepDistance = 1f, StepSeconds = 0.12f;
            public const float StrideMove = 100f, StrideSeconds = 1.5f;
            public const float LeapDistance = 1.5f, LeapSeconds = 0.15f;
            public const float QuickenSpeed = 100f;
            public const int ReboundBodies = 3;
            public const float ReboundHaste = 2f;
            public const float SettleChance = 0.6f;
            public const float FlickPush = 1f, ShovePush = 0.8f;
            public const float GravityRadius = 2f, GravityPull = 1f;
            public const int SteadyHandStreak = 5;
            public const float FlareHitUnits = 6f, FlareLength = 5f, FlareHalfWidth = 0.6f;
            public const float PrimerShare = 0.93f;
            public const float ReaderShare = 0.88f, ReaderCrit = 0.5f, ReaderPerStop = 0.44f;
            public const float ReaderNearRadius = 2.5f;
            public const float FullTurnSeconds = 12f, FullTurnShare = 0.05f;
            public const float SeamHitUnits = 4.97f, SeamLength = 4f, SeamHalfWidth = 0.55f, SeamDelay = 0.5f;
            public const float SkewerHitUnits = 5.27f, SkewerReach = 3f;
            public const float InwardOrbitPull = 0.5f;
            public const float ArtSplinterShare = 1.5f, ArtSplinterReach = 2.5f;
            public const float RecoilStepDistance = 1.2f;
            public const float BarbedBleedShare = 0.19f, BarbedBleedSeconds = 3f;
            public const float KneelingHitUnits = 6f, KneelingReach = 6f;
            public const float BrandBladeRadius = 0.8f, BrandBladeSeconds = 2f;

            // ---------------------------------------------------------------- chain

            public const float LungeStepDistance = 0.6f;
            public const float FollowThroughShare = 1.5f, FollowThroughReach = 1.5f;
            public const float ShearShare = 1.36f, ShearReach = 1f;
            public const float PinningWallReach = 1.5f, PinningRoot = 1.18f;
            public const float BackstepRadius = 2f, BackstepDistance = 0.8f;
            public const int EmberArcEvery = 3;

            // ---------------------------------------------------------------- release

            public const float GatheredRadius = 4f, GatheredPull = 4f;
            public const float ClearingRadius = 3f, ClearingPush = 4f;
            public const float ConsumeRadius = 3.5f, ConsumeShare = 1.5f;
            public const float FuelRefund = 0.3f, CurrentRefund = 0.3f;
            public const float DrawRadius = 5f, DrawPull = 4f;
            public const float LandslideRadius = 3f, LandslidePush = 3.24f;
            public const float RubbleRadius = 2f, RubbleSeconds = 8f;
            public const float FunnelRadius = 2.2f, FunnelSeconds = 3f, FunnelPull = 3f;
            public const float CarryDistance = 3.1f, CarrySeconds = 0.18f;

            // ---------------------------------------------------------------- guard

            public const float CuttingDashHitUnits = 6f, CuttingDashHalfWidth = 0.8f;
            public const float WakeDashSlow = 0.6f, WakeDashSeconds = 6f;
            public const float LandingCrit = 0.5f, LandingWindow = 1f;
            public const float MeleeBlowWithin = 2.5f;
            public const float RebukeHitUnits = 6f, PlantStagger = 2f, SapRefund = 0.4f;
            public const float BulldozeRadius = 2.5f, BulldozePush = 1.2f;
            public const float BacklashShare = 1.5f, BacklashRadius = 2.5f;
            public const float AuraRadius = 2.5f, AuraSlow = 0.6f, AuraTick = 0.5f;
            public const float HoldPush = 4f;
            public const float TauntRadius = 4f, TauntPull = 3f;
            public const float CounterweightStagger = 1.63f;
            public const float DisarmSeconds = 2.84f;
            public const float ReadCrit = 0.49f;
            public const int ReadHits = 3;
            public const float EchoGuardHaste = 1.52f;
            public const float ParryAttackerWithin = 1.8f;
            public const float CoatBurstRadius = 2f;

            // ---------------------------------------------------------------- concords

            public const float ConcordHitUnits = 1f, ConcordTick = 0.25f;
            public const float ConcordOrbitRadius = 1.6f, ConcordOrbitSeconds = 1f;

            // ---------------------------------------------------------------- humour

            public const float DeepeningStep = 0.3f, DeepeningCap = 2f;
            public const int DeepeningKillsPerStep = 10;
            public const float SettlingStep = 0.5f, SettlingCap = 3f;
            public const float WildfireReach = 0.9f, WildfireShare = 1.5f, WildfireTick = 0.5f;
            public const float RippleReach = 2f, RippleShare = 0.5f;
            public const float SplatterReach = 2f, SplatterShare = 1.5f, SplatterSeconds = 3f;
            public const float ToppleReach = 1.5f, ToppleStagger = 2f;

            // ---------------------------------------------------------------- costs

            public const float SpentMeterBelow = 0.5f, SpentMeterHealth = 0.215f;
            public const float OverdrawnHealth = 0.1f;
            public const float FatigueAfter = 20f, FatigueEvery = 10f, FatigueSpeed = 30f, FatigueCap = 30f;
            public const float OverswingStun = 0.97f, OverswingCheck = 0.25f;
            public const float UnsteadyDamage = 0.6f;
            public const float SkidDistance = 2.16f;
            public const float NarrowDotAdd = 0.6f;
            public const float HeavyBulwarkMove = 70f;
            public const float OffBalanceStun = 1.2f;
            public const float AffixedChanceAdd = 0.6f;
            public const float CrowdedPressure = 1.27f;
            public const int EagerTurns = 1;
            public const float SwiftMove = 0.3f;
            public const float WrathBelow = 0.6f, WrathCooldownMul = 0.7f;
            public const float MartyrDelay = 0.8f, MartyrRadius = 1.2f, MartyrHealth = 0.2f;
            public const float StormEvery = 8f, StormLag = 1f, StormRadius = 1f, StormHealth = 0.3f;
            public const float GaleEvery = 15f, GaleWarning = 1f, GalePush = 3f;
            public const float SinkingAfter = 2f, SinkingMove = 70f, SinkingSeconds = 1f;
            public const float SlickSlide = 0.6f;
            public const float SlowLinkSpeed = 36f;
            public const float DampRadiusMul = 0.8f;
            public const float KickDistance = 3f;
            public const float StrainCooldown = 0.6f;
            public const float BrittleGuardWindow = 0.4f;
            public const float ExertionHealth = 0.052f;

            // ---------------------------------------------------------------- the world costs, second batch

            /// <summary>Plated: unarmoured enemies gain armour of this share of their health, a stack.</summary>
            public const float PlatedShare = 0.21f;
            public const float PlatedExposed = 0.88f, PlatedExposedSeconds = 2f;
            /// <summary>Reinforced: bodies an elite's death calls, a stack (III: one more).</summary>
            public const int ReinforcedBodies = 1;
            public const float ReinforcedStagger = 2f, ReinforcedRadius = 3f;
            /// <summary>Volley: bolts a ranged enemy adds to each shot.</summary>
            public const int VolleyExtraBolts = 1;
            /// <summary>Twisted: bands the next mini-boss arrives above its own (the top band at most).</summary>
            public const int TwistedBands = 1, TopBand = 4;
            /// <summary>Encroach: a new pit opens this often on a floor, this far from you, after a warning.</summary>
            public const float EncroachEvery = 5f, EncroachWarning = 1.5f, EncroachNear = 3f, EncroachFar = 5f, EncroachSize = 1.2f;
            public const float EncroachEnemyPitMul = 8f;
            /// <summary>Hazard Bloom: pits hurt you this much more, a stack. Its Albedo: Damage while you stand in one.</summary>
            public const float BloomTaken = 1f, BloomDamage = 100f;
            /// <summary>Narrowing: after this long on a floor (halved at III), the band along the room's edges
            /// burns you this share of max health a second; each stack widens it.</summary>
            public const float NarrowingAfter = 25f, NarrowingBand = 1.5f, NarrowingBandPerStack = 0.5f, NarrowingHealth = 0.1f;
            public const float NarrowingSlow = 0.2f;
            /// <summary>Crumble: a column falls this often (halved at III). Its Albedo: a breaking column strikes.</summary>
            public const float CrumbleEvery = 20f, CrumbleHitUnits = 6f, CrumbleRadius = 2.5f;
            /// <summary>Dim: your light shrinks this share a stack. Its Albedo: enemies in your light take more.</summary>
            public const float DimShare = 0.2f, LanternTaken = 0.36f;
            /// <summary>Tide: shallow water laid at the start of each floor.</summary>
            public const int TidePools = 2;
            public const float TideSize = 2f;
            /// <summary>Spite Spires: a spire's boon costs this share of max health, a stack.</summary>
            public const float SpiteHealth = 0.3f;
            /// <summary>Dead Ground (an oath): pits hurt you this many times over until a floor is cleared untouched.</summary>
            public const float DeadGroundMul = 2f;

            // ---------------------------------------------------------------- the rest of the plan (2026-11-04)

            // Payoff: primes the next art.
            public const float DrawnInShare = 0.93f, FlankExposed = 1f, FlankSeconds = 3f, AmbushShare = 1.5f, AmbushWindow = 2f;
            public const float ChargePerStop = 0.46f, ExposeShare = 0.88f, GatherPerBody = 0.6f, DeepenShare = 0.91f;
            // Crowd trio.
            public const float SparkShare = 1.5f, SparkReach = 3f, FanShare = 0.64f, FanReach = 1.5f, DebrisRadius = 2f, DebrisPush = 1f;
            // Fortune.
            public const float LongRiftSeconds = 5f, AmbushUnawareSeconds = 2f, AmbushTaken = 1.76f;
            // The left-out costs.
            public const float LooseCatchChance = 1f / 6f, LooseCatchStun = 1f, LooseCatchBurstHitUnits = 6f, LooseCatchRadius = 1.5f;
            public const float SpillShare = 0.5f, SpillCoatRadius = 2f;
            // Release x class skeletons.
            public const float SkeletonHitUnits = 1f, SkeletonReach = 4f, SkeletonOrbitSeconds = 2f, SkeletonOrbitRadius = 1.6f;

            // ---------------------------------------------------------------- the new Albedos

            public const float SpentMeterHeal = 0.2f;
            public const float OverdrawnArt = 0.55f;
            public const float SecondWindDamage = 30f, SecondWindDamageCap = 150f;   // Fatigue's Albedo
            public const float OverswingCrescentShare = 6f;
            public const float SkidCutHitUnits = 6f;
            public const float NarrowEdgeStagger = 2f;
            public const float HeavyStillSlow = 0.6f, HeavyStillSeconds = 5f;
            public const float OffBalancePush = 3f;
            public const float AffixedHeal = 0.2f;
            public const float CrowdedPerBody = 0.073f, CrowdedFrom = 4f, CrowdedCap = 0.5f;
            public const float VigilantUnseenSeconds = 3f;
            public const float EagerExposed = 0.23f, EagerExposedSeconds = 2f;
            public const float SwiftPushMul = 1.3f;
            public const float MartyrEnemyHitUnits = 6f;
            public const float StormEnemyHitUnits = 6f;
            public const float SinkingRoot = 2f;
            public const float SlowLinkArt = 1.5f;
            public const float PlantedLinkStagger = 0.75f;
            public const float DampCentreBonus = 1f;
            public const float KickCutHitUnits = 6f;
            public const float StrainBank = 0.3f;
            public const float BrittleGuardStagger = 2f, BrittleGuardRadius = 2f;
            public const float ExertionHeal = 0.073f;
            public const float HeavyHandBlowMul = 0.1f;
            public const float DulledScriptSpread = 1.5f, DulledScriptReach = 2.5f;
            public const float BrittleScriptStagger = 1.18f;
        }
    }
}
