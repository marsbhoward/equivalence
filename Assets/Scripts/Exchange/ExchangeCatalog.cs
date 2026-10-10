using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Exchange
{
    /// <summary>
    /// Every boon and cost in the game, every Albedo a circle can make and every combination two
    /// held entries can open - the catalogue signed off 2026-10-05
    /// (docs/balance/2026-10-05-phase5-catalogue.md).
    ///
    /// COPY AND EFFECT TOGETHER. Each card's words are built from the same Tuning.Exchange constant
    /// its effect reads, so the two cannot drift. Stat effects are an Apply here (run-layer points,
    /// bent by the Vessel); anything needing a clock, a counter or a target is Conditional and lives
    /// in RunEffects, keyed by id.
    ///
    /// STACKS: unique (one), stackable (two or three) or recurring (deal-shaping, rests between
    /// takes). Every stackable boon has a RUBEDO at max stacks and every stackable cost a NIGREDO
    /// and an ALBEDO - the capstone is part of the entry's Apply or behaviour from that stack on.
    /// </summary>
    public static class ExchangeCatalog
    {
        static List<ExchangeEntry> _all;
        static List<ExchangeEntry> _combinations;
        static Dictionary<string, ExchangeEntry> _byId;

        public static IReadOnlyList<ExchangeEntry> All { get { Build(); return _all; } }

        /// <summary>Every Conjunction, Putrefaction and Citrinitas.</summary>
        public static IReadOnlyList<ExchangeEntry> Combinations { get { Build(); return _combinations; } }

        public static ExchangeEntry Get(string id)
        {
            Build();
            return id != null && _byId.TryGetValue(id, out var e) ? e : null;
        }

        // ---------------------------------------------------------------- words

        static string N(float v) => v.ToString("0.#");
        static string Pct(float f) => $"{Mathf.RoundToInt(f * 100f)}%";
        static string Sec(float s) => $"{s:0.#} s";

        // ---------------------------------------------------------------- building

        static ExchangeEntry Add(string id, string name, ExchangeKind kind, ExchangeFamily fam, ExchangeMark mark,
                                 int weight, int stacks, string effect, Action<Mods, int> apply, bool conditional)
        {
            var e = new ExchangeEntry
            {
                Id = id, Name = name, Effect = effect, Kind = kind, Family = fam, Mark = mark,
                Weight = weight, MaxStacks = stacks, Apply = apply, Conditional = conditional,
            };
            if (_byId.ContainsKey(id)) Debug.LogError($"[Exchange] duplicate entry id '{id}'");
            _all.Add(e);
            _byId[id] = e;
            if (e.IsCombination) _combinations.Add(e);
            return e;
        }

        static ExchangeEntry B(string id, string name, ExchangeFamily fam, ExchangeMark mark, int weight, int stacks,
                               string effect, Action<Mods, int> apply = null, bool conditional = false)
            => Add(id, name, ExchangeKind.Boon, fam, mark, weight, stacks, effect, apply, conditional);

        static ExchangeEntry C(string id, string name, ExchangeFamily fam, ExchangeMark mark, int weight, int stacks,
                               string effect, Action<Mods, int> apply = null, bool conditional = false)
            => Add(id, name, ExchangeKind.Cost, fam, mark, weight, stacks, effect, apply, conditional);

        /// <summary>An Albedo: a boon only a transmutation circle makes, from the named cost.</summary>
        static ExchangeEntry A(string id, string name, string ofCost, ExchangeFamily fam, ExchangeMark mark,
                               string effect, Action<Mods, int> apply = null, bool conditional = false)
        {
            var e = Add(id, name, ExchangeKind.Boon, fam, mark, 3, 1, effect, apply, conditional);
            e.Origin = EntryOrigin.Albedo;
            var cost = _byId[ofCost];
            cost.AlbedoId = id;
            return e;
        }

        /// <summary>A combination of two held entries - weight 3, one stack, opened once a run.</summary>
        static ExchangeEntry Combo(EntryOrigin origin, string id, string name, string partA, string partB,
                                   ExchangeFamily fam, ExchangeMark mark, string effect,
                                   Action<Mods, int> apply = null, bool conditional = false)
        {
            var kind = origin == EntryOrigin.Putrefaction ? ExchangeKind.Cost : ExchangeKind.Boon;
            var e = new ExchangeEntry
            {
                Id = id, Name = name, Effect = effect, Kind = kind, Family = fam, Mark = mark,
                Weight = 3, MaxStacks = 1, Apply = apply, Conditional = conditional,
                Origin = origin, Parts = new[] { partA, partB },
            };
            if (!_byId.ContainsKey(partA) || !_byId.ContainsKey(partB))
                Debug.LogError($"[Exchange] combination '{id}' names a part that does not exist");
            _all.Add(e);
            _byId[id] = e;
            _combinations.Add(e);
            return e;
        }

        static void Build()
        {
            if (_all != null) return;
            _all = new List<ExchangeEntry>();
            _combinations = new List<ExchangeEntry>();
            _byId = new Dictionary<string, ExchangeEntry>();

            const ExchangeFamily Edge = ExchangeFamily.Edge;
            const ExchangeFamily Anvil = ExchangeFamily.Anvil;
            const ExchangeFamily Hide = ExchangeFamily.Hide;
            const ExchangeFamily Quick = ExchangeFamily.Quicksilver;
            const ExchangeFamily Azoth = ExchangeFamily.Azoth;
            const ExchangeFamily Ledger = ExchangeFamily.Ledger;
            const ExchangeFamily Blunt = ExchangeFamily.Blunted;
            const ExchangeFamily Brittle = ExchangeFamily.Brittle;
            const ExchangeFamily Tithe = ExchangeFamily.Tithe;
            const ExchangeFamily Leaden = ExchangeFamily.Leaden;
            const ExchangeFamily Leaking = ExchangeFamily.Leaking;
            const ExchangeFamily Blind = ExchangeFamily.Blindfold;

            const ExchangeMark Dot1 = ExchangeMark.Dot1, Dot2 = ExchangeMark.Dot2, Dot3 = ExchangeMark.Dot3;
            const ExchangeMark Bar = ExchangeMark.Bar, Ring = ExchangeMark.Ring, Cross = ExchangeMark.Cross;
            const ExchangeMark Stroke = ExchangeMark.Stroke, Drop = ExchangeMark.Drop, NoMark = ExchangeMark.None;

            // ============================================================ BOONS

            // ---- Edge: strikes ----
            B("whetstone", "Whetstone", Edge, Bar, 2, 3, $"+{N(T.WhetstoneDamage)} Damage.",
              (m, n) => m.Add(StatKind.Damage, T.WhetstoneDamage * n), conditional: true)
              .Cap("Keen Edge", $"your hits strip enemy armour {Pct(T.KeenEdgeShred - 1f)} faster.");
            B("quickening", "Quickening", Edge, Dot2, 2, 3, $"+{N(T.QuickeningSpeed)} Attack Speed.",
              (m, n) =>
              {
                  m.Add(StatKind.AttackSpeed, T.QuickeningSpeed * n);
                  if (n >= 3) m.LockMul *= T.CelerityLockMul;
              })
              .Cap("Celerity", $"weapon arts lock you {Pct(1f - T.CelerityLockMul)} less long.");
            B("vein_finder", "Vein Finder", Edge, Dot3, 2, 3, $"+{Pct(T.VeinFinderCrit)} crit chance.",
              (m, n) => m.BonusCrit += T.VeinFinderCrit * n, conditional: true)
              .Cap("Fulminate", $"a crit bursts for {Pct(T.FulminateFraction)} of the hit onto enemies around its target.");
            B("executioner", "Executioner", Edge, Drop, 2, 2,
              $"+{N(T.ExecutionerDamage)} Damage against enemies under {Pct(T.ExecutionerBelow)} health.", conditional: true)
              .Cap("Coup de Grace", $"a non-elite you hit below {Pct(T.CoupDeGraceBelow)} health dies outright.");
            B("first_blood", "First Blood", Edge, Dot1, 2, 1,
              $"The first time an attack's main target is a full-health enemy, it also loses {Pct(T.FirstBloodShare)} of its health (not a boss).",
              conditional: true);
            B("reiteration", "Reiteration", Edge, Ring, 2, 2,
              $"Every {T.ReiterationEvery}th attack that lands ({T.ReiterationEvery - 1}th at II) strikes its target again for {Pct(T.ReiterationFraction)}.",
              conditional: true)
              .Cap("Rota", "a repeat that kills leaps to the nearest enemy and repeats again.");
            B("long_reach", "Long Reach", Edge, Stroke, 1, 3, $"+{N(T.LongReachRange)} Range.",
              (m, n) => m.Add(StatKind.Range, T.LongReachRange * n), conditional: true)
              .Cap("Far Strike", $"hits in the outer quarter of your reach get +{N(T.FarStrikeDamage)} Damage.");
            B("wide_arc", "Wide Arc", Edge, Cross, 1, 2, $"+{Pct(T.WideArcWidth)} swing width.",
              (m, n) =>
              {
                  m.StrikeWidthMul += T.WideArcWidth * n;
                  if (n >= 2) m.Add(StatKind.Cleave, T.CleavingHabitCleave);
              })
              .Cap("Cleaving Habit", $"+{N(T.CleavingHabitCleave)} Cleave.")
              .ForClasses(WeaponClass.Greatsword, WeaponClass.Disc);

            // ---- Anvil: weapon arts ----
            B("heavy_payoff", "Heavy Payoff", Anvil, Bar, 2, 3, $"+{N(T.HeavyPayoffArt)} Weapon Art.",
              (m, n) => m.Add(StatKind.FinisherPower, T.HeavyPayoffArt * n), conditional: true)
              .Cap("Crushing Blow", $"every weapon art staggers what it hits for {Sec(T.CrushingBlowStagger)}.");
            B("ouroboros", "Ouroboros", Anvil, Ring, 2, 3,
              $"A kill has a {Pct(T.OuroborosChance)} chance to bank your weapon art at once (an elite's counts for more).", conditional: true)
              .Cap("The Serpent Eats", "a weapon art that kills banks the next one at once.");
            B("green_lion", "Green Lion", Anvil, Drop, 2, 2, $"PERFECT weapon arts get +{N(T.GreenLionArt)} Weapon Art.",
              conditional: true)
              .Cap("Red Lion", "your perfect streak survives a GOOD - only an early, missed or wasted strike resets it.");
            B("short_chain", "Short Chain", Anvil, Dot1, 3, 1, "One basic fewer before every weapon art.",
              (m, n) => m.BasicsPerChainDelta -= 1);
            B("extra_sigil", "Extra Sigil", Anvil, Dot3, 3, 1, "One more weapon art in the rotation.",
              (m, n) => m.ExtraFinisherSlots += 1);

            // ---- Hide: defence ----
            B("thickened_hide", "Thickened Hide", Hide, Bar, 2, 3, $"+{N(T.ThickenedHideHp)}% max health.",
              (m, n) => m.Add(StatKind.MaxHp, T.ThickenedHideHp * n), conditional: true)
              .Cap("Fortitude", $"below {Pct(T.FortitudeBelow)} health, hits deal {Pct(1f - T.FortitudeMul)} less.");
            B("bloodletters_pact", "Bloodletter's Pact", Hide, Drop, 2, 3,
              $"+{Pct(T.BloodletterLifesteal)} lifesteal (one pool, capped).",
              (m, n) => m.BonusLifesteal += T.BloodletterLifesteal * n, conditional: true)
              .Cap("Transfusion", "lifesteal also drinks from your releases, burns and bleeds.");
            B("stonestance", "Stonestance", Hide, Dot1, 2, 3, $"+{N(T.StonestanceBrace)} Brace (mitigation standing still).",
              (m, n) => m.Add(StatKind.Brace, T.StonestanceBrace * n), conditional: true)
              .Cap("Lapis", $"after {Sec(T.LapisStillSeconds)} standing still, the next hit you take deals half.");
            B("reactive_plate", "Reactive Plate", Hide, Dot2, 2, 2,
              $"After a hit, {Sec(T.ReactivePlateSeconds)} of -{Pct(T.ReactivePlateReduction)} damage taken " +
              $"(-{Pct(T.ReactivePlateReductionII)} at II), at most once every {Sec(T.ReactivePlateCooldown)}.",
              conditional: true)
              .Cap("Tempered", $"the window comes back after {Sec(T.ReactivePlateCooldownTempered)} instead of {Sec(T.ReactivePlateCooldown)}.");
            B("aegis_cycle", "Aegis Cycle", Hide, Ring, 3, 2,
              $"A ward that cancels one hit, renewing every {Sec(T.AegisRenewSeconds)} ({Sec(T.AegisRenewSecondsII)} at II). " +
              $"Nothing in the ledger cancels another hit for {Sec(T.NegationGapSeconds)} after.",
              conditional: true)
              .Cap("Tin Ward", "when the ward breaks it throws nearby enemies back.");
            B("second_wind", "Second Wind", Hide, Stroke, 3, 1,
              $"Once a floor, a lethal hit leaves you at 1 health, then heals {Pct(T.SecondWindHeal)} over {Sec(T.SecondWindHealSeconds)}.",
              conditional: true);
            B("kiln_fired", "Kiln-Fired", Hide, Cross, 1, 1, "Your armour never wears.",
              (m, n) => m.ArmourNeverWears = true);

            // ---- Quicksilver: movement ----
            B("fleetfoot", "Fleetfoot", Quick, Dot2, 1, 3, $"+{N(T.FleetfootMove)} Move Speed.",
              (m, n) => m.Add(StatKind.MoveSpeed, T.FleetfootMove * n), conditional: true)
              .Cap("Wake", $"after {Sec(T.WakeAfterSeconds)} at full speed, your next basic gets +{N(T.WakeDamage)} Damage.");
            B("eagle", "Eagle", Quick, Stroke, 1, 2,
              $"After a kill, +{N(T.EagleMove)} Move Speed for {Sec(T.EagleSeconds)} ({Sec(T.EagleSecondsII)} at II).",
              conditional: true)
              .Cap("Stoop", $"after a kill, your next hit within {Sec(T.StoopSeconds)} has +{Pct(T.ForcedCritChance)} crit chance.");
            B("evanescence", "Evanescence", Quick, Bar, 2, 3, $"+{N(T.EvanescenceGraze)} Graze (mitigation while moving).",
              (m, n) => m.Add(StatKind.Graze, T.EvanescenceGraze * n), conditional: true)
              .Cap("Vapour", $"while moving, every {T.VapourEvery}th hit you take passes through you " +
                              $"(nothing else in the ledger cancels one for {Sec(T.NegationGapSeconds)} after).");
            B("ghostwalk", "Ghostwalk", Quick, Ring, 3, 1,
              $"After a hit, {Sec(T.GhostwalkSeconds)} untouchable, at most once every {Sec(T.GhostwalkCooldown)}. " +
              $"Nothing in the ledger cancels another hit for {Sec(T.NegationGapSeconds)} after.",
              conditional: true);

            // ---- Azoth: the element ----
            B("attunement", "Attunement", Azoth, Dot1, 1, 2, $"Start each floor with {Pct(T.AttunementFill)} of your meter.",
              (m, n) => m.StartMeterFraction = n >= 2 ? 1f : T.AttunementFill * n, conditional: true)
              .Cap("Primed", "you start each floor with it full instead.");
            B("rich_vein", "Rich Vein", Azoth, Dot3, 1, 3, $"+{N(T.RichVeinGrowth)} Element Growth (Fire: heat lasts longer).",
              (m, n) => m.Add(StatKind.ElementGrowth, T.RichVeinGrowth * n), conditional: true)
              .Cap("Mother Lode", $"a kill refills {Pct(T.MotherLodeRefill)} of your meter (an elite's more).");
            B("elixir", "Elixir", Azoth, Drop, 1, 3, $"+{N(T.ElixirPower)} Elemental Power.",
              (m, n) => m.Add(StatKind.ElementalEffectiveness, T.ElixirPower * n), conditional: true)
              .Cap("Grand Elixir", "your releases can crit.");
            B("dilation", "Dilation", Azoth, Ring, 1, 3, $"+{N(T.DilationArea)} Area.",
              (m, n) => m.Add(StatKind.AoeRadius, T.DilationArea * n), conditional: true)
              .Cap("Expansion", $"area attacks lose {Pct(1f - T.ExpansionEdgeLoss)} less damage toward their edge.");
            B("residue", "Residue", Azoth, Cross, 1, 1,
              $"A release leaves {Sec(T.ResidueSeconds)} of your element on the ground: Fire burns, Water soaks, Earth slows, Air pulls in.",
              conditional: true);
            B("overflow", "Overflow", Azoth, Bar, 3, 1, $"A release refunds {Pct(T.OverflowRefund)} of its meter.",
              conditional: true);
            B("twin_spark", "Twin Spark", Azoth, Dot2, 3, 1,
              $"Your release fires again {Sec(T.TwinSparkDelay)} later at {Pct(T.TwinSparkScale)} strength, spending nothing.",
              conditional: true);

            // ---- Ledger: the run itself ----
            B("curator", "Curator", Ledger, Dot1, 2, 1, "Floor rewards offer one more card.",
              (m, n) => m.ExtraRewardOptions += T.CuratorCards);
            B("transmuters_eye", "Transmuter's Eye", Ledger, Ring, 2, 1, "See this floor's reward before you choose a deal.",
              conditional: true);
            B("scrying_glass", "Scrying Glass", Ledger, Dot2, 2, 1,
              "See what the next floor holds - and its roster, if it is a fight.", conditional: true);
            B("lodestone", "Lodestone", Ledger, Stroke, 1, 1, "A spire boon you capture lasts one more floor.",
              (m, n) => m.SpireExtraFloors += T.LodestoneFloors);
            B("prima_materia", "Speculum", Ledger, Cross, 2, 1, "The next deal shows three pairs.")
              .Recurs(p => p.PairsDelta += 1);

            // ============================================================ COSTS

            // ---- Blunted: offence ----
            C("dulled", "Dulled", Blunt, Bar, 2, 3, $"-{N(T.DulledDamage)} Damage.",
              (m, n) => m.Add(StatKind.Damage, -T.DulledDamage * n), conditional: true)
              .Cap("Rebated", "every hit rolls the bottom of its damage range - Accuracy no help.");
            C("heavy_arms", "Heavy Arms", Blunt, Dot2, 2, 3, $"-{N(T.HeavyArmsSpeed)} Attack Speed.",
              (m, n) =>
              {
                  m.Add(StatKind.AttackSpeed, -T.HeavyArmsSpeed * n);
                  if (n >= 3) m.UpVoid[(int)StatKind.AttackSpeed] = true;
              })
              .Cap("Leaden Limbs", "attack speed from boons and spires stops counting.");
            C("cold_iron", "Cold Iron", Blunt, Cross, 2, 3,
              "Crits lose a third of their bonus damage (none left at III).", conditional: true)
              .Cap("Quenched", $"a crit deals {Pct(1f - T.QuenchedMul)} less than an ordinary hit.");
            C("fumbler", "Fumbler", Blunt, Dot1, 2, 3, "One swing in nine passes through without connecting.",
              conditional: true)
              .Cap("Lapsus", $"a swing that passes through leaves you stumbling: {Sec(T.LapsusStumble)} without moving or attacking.");
            C("overcommitted", "Overcommitted", Blunt, Dot3, 2, 3, $"Weapon arts lock you {Pct(T.OvercommittedLock)} longer.",
              (m, n) => m.LockMul *= 1f + T.OvercommittedLock * n, conditional: true)
              .Cap("Overextended", $"+{Pct(T.OverextendedTaken)} damage taken while an art locks you (outside the floor).");
            C("dross", "Dross", Blunt, Ring, 2, 3, $"-{N(T.DrossArt)} Weapon Art.",
              (m, n) =>
              {
                  m.Add(StatKind.FinisherPower, -T.DrossArt * n);
                  if (n >= 3) m.ArtsCantCrit = true;
              })
              .Cap("Slag", "your weapon arts can't crit.");
            C("induration", "Induration", Blunt, Drop, 2, 3, $"Enemies have {Pct(T.IndurationHealth)} more health and armour (not bosses).",
              (m, n) =>
              {
                  m.EnemyHealthMul *= 1f + T.IndurationHealth * n;
                  if (n >= 3) m.EliteHealthMul *= 1f + T.CoagulationEliteHealth;
              })
              .Cap("Coagulation", $"elites have {Pct(T.CoagulationEliteHealth)} more on top.");
            C("short_arm", "Short Arm", Blunt, Stroke, 1, 3, $"-{N(T.ShortArmRange)} Range.",
              (m, n) => m.Add(StatKind.Range, -T.ShortArmRange * n), conditional: true)
              .Cap("Cramped", $"hits beyond half your reach get -{N(T.CrampedDamage)} Damage.");

            // ---- Brittle: defence ----
            C("thin_blood", "Thin Blood", Brittle, Drop, 2, 3, $"-{N(T.ThinBloodHp)}% max health.",
              (m, n) => m.Add(StatKind.MaxHp, -T.ThinBloodHp * n), conditional: true)
              .Cap("Anaemia", $"healing above {Pct(T.AnaemiaAbove)} health is halved.");
            C("paper_guard", "Paper Guard", Brittle, Bar, 2, 3,
              $"+{Pct(T.PaperGuardTaken)} damage taken (outside the mitigation floor).",
              (m, n) =>
              {
                  m.DamageTakenOutside *= 1f + T.PaperGuardTaken * n;
                  if (n >= 3) m.MitigationFloor = Mathf.Max(m.MitigationFloor, T.ExposedFloor);
              })
              .Cap("Exposed", $"your mitigation floor rises from {Pct(Tuning.Stats.IncomingFloor)} to {Pct(T.ExposedFloor)}: armour, Resilience, Graze and Brace stop short.");
            C("slow_knit", "Slow Knit", Brittle, Ring, 2, 3, $"-{Pct(T.SlowKnitHeal)} healing.",
              (m, n) =>
              {
                  m.HealMul *= 1f - T.SlowKnitHeal * n;
                  if (n >= 3) m.HealCeiling = Mathf.Min(m.HealCeiling, T.HollowCeiling);
              })
              .Cap("Hollow", $"healing can't take you above {Pct(T.HollowCeiling)} health.");
            C("rust", "Rust", Brittle, Stroke, 1, 3, $"Your armour wears {Pct(T.RustWear)} faster.",
              (m, n) =>
              {
                  m.ArmourWearMul *= 1f + T.RustWear * n;
                  if (n >= 3) m.RepairMul *= T.CorrosionRepairMul;
              })
              .Cap("Corrosion", "repairs from every source are halved, and the damage worn armour adds can't be mitigated.");
            C("open_stance", "Open Stance", Brittle, Dot1, 1, 1,
              T.OpenStanceHits == 1 ? "The first hit you take each floor deals double."
                                    : $"The first {T.OpenStanceHits} hits you take each floor deal double.", conditional: true);

            // ---- Tithe: prices paid ----
            C("blood_price", "Blood Price", Tithe, Bar, 3, 2, $"Every swing costs {Pct(T.BloodPriceSwing)} of max health.",
              conditional: true)
              .Cap("Haemorrhage", $"the price doubles below {Pct(T.HaemorrhageBelow)} health.");
            C("withering", "Withering", Tithe, Dot3, 3, 2,
              $"Max health -{Pct(T.WitheringPerFloor)} for every floor cleared (stops at -{Pct(T.WitheringCap)}).",
              conditional: true)
              .Cap("Desiccation", "the loss no longer stops.");
            C("toll", "Toll", Tithe, Dot2, 1, 3, $"Clearing a floor costs {Pct(T.TollFraction)} of current health.",
              conditional: true)
              .Cap("Usury", "the toll is a share of MAX health instead of what you have left.");
            C("souring", "Souring", Tithe, Stroke, 2, 3,
              $"-{N(T.SouringDamage)} Damage for every {Sec(T.SouringEverySeconds)} on a floor (resets each floor).",
              conditional: true)
              .Cap("Acetum", $"enemies also hit {Pct(T.AcetumTaken)} harder for every {Sec(T.SouringEverySeconds)} on a floor.");
            C("backfire", "Backfire", Tithe, Cross, 2, 3, $"A release costs you {Pct(T.BackfireFraction)} of max health.",
              conditional: true)
              .Cap("Recoil", $"a release also roots you for {Sec(T.RecoilSeconds)} - no moving or attacking.");
            // The deal shapers were free (2026-10-10): run with them out of the pool, careful runs
            // ended no stronger - they filled a cost slot a real cost would have. Each now takes
            // something a deal is worth.
            C("caput_mortuum", "Caput Mortuum", Tithe, Ring, 2, 1, "The next two deals show one pair each, and can't be refused.")
              .Recurs(p => { p.PairsDelta -= 1; p.PairsDeltaNext -= 1; p.Forced = true; p.ForcedNext = true; });
            C("indenture", "Indenture", Tithe, Dot1, 2, 1, "The next deal can't be refused, and its boons are one weight lighter.")
              .Recurs(p => { p.Forced = true; p.BoonWeightDelta -= 1; });
            C("debt", "Debt", Tithe, NoMark, 3, 1, "The next deal gives its cost and no boon, and can't be refused.")
              .Recurs(p => { p.NoBoon = true; p.Forced = true; });

            // ---- Leaden: movement ----
            C("anchored", "Anchored", Leaden, Bar, 2, 3, $"-{N(T.AnchoredMove)} Move Speed.",
              (m, n) =>
              {
                  m.Add(StatKind.MoveSpeed, -T.AnchoredMove * n);
                  if (n >= 3) m.ExtraSlide += T.MiredSlide;
              }, conditional: true)
              .Cap("Mired", "sand and mire slow you twice as much, and you slide after you stop, as if in water.");
            C("encumbered", "Encumbered", Leaden, Dot3, 2, 3, $"Defensive ability cooldown +{Pct(T.EncumberedCooldown)}.",
              (m, n) => m.DefenseCooldownMul *= 1f + T.EncumberedCooldown * n, conditional: true)
              .Cap("Shackled", $"while your defensive ability recharges, you take {Pct(T.ShackledTaken)} more damage.");
            C("rooted", "Rooted", Leaden, Cross, 2, 1, "You can't move while any swing plays, basics included.",
              (m, n) => m.RootedWhileSwinging = true);

            // ---- Leaking: the element and the chain ----
            C("stubborn_ore", "Stubborn Ore", Leaking, Dot3, 2, 3,
              $"-{N(T.StubbornOreGrowth)} Element Growth (the meter fills slower; Fire's heat fades sooner).",
              (m, n) => m.Add(StatKind.ElementGrowth, -T.StubbornOreGrowth * n), conditional: true)
              .Cap("Barren", $"while your meter is under {Pct(T.BarrenBelow)}, you take {Pct(T.BarrenTaken)} more damage.");
            C("leaky_vessel", "Leaky Vessel", Leaking, Drop, 1, 3, $"Your meter fades {Pct(T.LeakyVesselDecay)} faster.",
              (m, n) => m.DecayMul *= 1f + T.LeakyVesselDecay * n, conditional: true)
              .Cap("Cracked Vessel", $"every hit you take spills {Pct(T.CrackedVesselSpill)} of your meter.");
            C("long_chain", "Long Chain", Leaking, Dot2, 2, 2, "One more basic before every weapon art.",
              (m, n) =>
              {
                  m.BasicsPerChainDelta += n;
                  if (n >= 2) m.ComboTimeMul *= T.FrayingComboMul;
              })
              .Cap("Fraying", "partial chains lapse twice as fast.");

            // ---- Blindfold: what you can read ----
            C("fog", "Fog", Blind, Ring, 1, 3,
              "I: your target loses its rim. II: the chain and rotation display is hidden too. III: damage numbers and enemy armour rings are hidden too.",
              (m, n) =>
              {
                  m.HideTargetHighlight = true;
                  if (n >= 2) m.HideChainHud = true;
                  if (n >= 3)
                  {
                      m.HideEnemyReadouts = true;
                      m.TelegraphStrength = Mathf.Min(m.TelegraphStrength, T.MurkStrength);
                  }
              })
              .Cap("Murk", "enemy attack telegraphs are drawn at half strength.");
            C("wandering_eye", "Wandering Eye", Blind, Dot2, 1, 3, "Auto-target abandons its target more eagerly.",
              (m, n) =>
              {
                  m.SwitchAdvantageDelta += T.WanderingEyeSwitch * n;
                  if (n >= 3) m.Targeting = TargetMode.Random;
              })
              .Cap("Blind Rage", "auto-target picks at random within reach.");
            C("dead_weight", "Dead Weight", Blind, Cross, 1, 3,
              $"An enemy you kill leaves a corpse that blocks you for {Sec(T.DeadWeightSeconds + 1f)} " +
              $"(+{Sec(1f)} a stack).", conditional: true)
              .Cap("Charnel", "corpses also stop your thrown weapons and arrows.");
            C("retrograde", "Retrograde", Blind, Stroke, 3, 2,
              $"Every {Sec(T.RetrogradeEvery)} ({Sec(T.RetrogradeEveryII)} at II) your movement inverts for {Sec(T.RetrogradeSeconds)}, " +
              $"warned {Sec(T.RetrogradeWarning)} ahead by a ring closing at your feet.", conditional: true)
              .Cap("Contrary", $"every hit you take also inverts you for {Sec(T.ContrarySeconds)}.");
            C("projection", "Projection", Blind, Bar, 3, 2,
              $"Every {Sec(T.ProjectionEvery)} ({Sec(T.ProjectionEveryII)} at II), {T.ProjectionLines} lines are drawn across the floor; " +
              $"after {Sec(T.ProjectionWarning)} they burn for {Sec(T.ProjectionBurn)}.", conditional: true)
              .Cap("Lattice", "a second set of lines crosses the first, drawn through where you stand.");

            // ============================================================ ELEMENTS

            // The trap boons: stack II is their capstone - the same bonus for all four, the same time.
            string trap = $"+{N(T.TrapBonusDamage)} Damage";
            B("salamander", "Salamander", Hide, Drop, 2, 2, "Fire pits don't burn you.", conditional: true)
              .Cap("Salamander II", $"while in a burning pit, and for {Sec(T.TrapBonusSeconds)} after: {trap}.")
              .ForElement(ElementType.Fire);
            B("gnome", "Gnome", Hide, Bar, 2, 2, "Sand doesn't slow you (not the Turret's mire).", conditional: true)
              .Cap("Gnome II", $"while in sand, and for {Sec(T.TrapBonusSeconds)} after: {trap}.")
              .ForElement(ElementType.Earth);
            B("undine", "Undine", Hide, Ring, 2, 2, "Water doesn't take your grip or soak you.", conditional: true)
              .Cap("Undine II", $"while in water, and for {Sec(T.TrapBonusSeconds)} after: {trap}.")
              .ForElement(ElementType.Water);
            B("sylph", "Sylph", Hide, Stroke, 2, 2, "Tornadoes don't hurt you.", conditional: true)
              .Cap("Sylph II", $"passing through a tornado: {trap} for {Sec(T.TrapBonusSeconds)}.")
              .ForElement(ElementType.Air);

            B("banked_embers", "Banked Embers", Azoth, Dot1, 1, 2, "Your heat never falls below one stack (two at II).",
              conditional: true)
              .Cap("Hearth", $"a release leaves you at {T.HearthStacks} heat stacks.")
              .ForElement(ElementType.Fire);
            B("high_tide", "High Tide", Azoth, Dot2, 2, 2, $"Your surge lasts {Sec(T.HighTideSeconds)} longer.",
              conditional: true)
              .Cap("Spring Tide", "a surge soaks every enemy around you.")
              .ForElement(ElementType.Water);
            B("deep_roots", "Deep Roots", Azoth, Dot3, 2, 2, $"Your charge stops bleeding for {Sec(T.DeepRootsHold)} after you move.",
              conditional: true)
              .Cap("Bedrock", $"your planted damage reduction holds for {Sec(T.BedrockHold)} after you move.")
              .ForElement(ElementType.Earth);
            B("tailwind", "Tailwind", Azoth, Stroke, 2, 2, "Momentum fades half as fast when you stop (II: not at all for a second).",
              conditional: true)
              .Cap("Updraft", "Gust throws nearby enemies back.")
              .ForElement(ElementType.Air);

            C("smother", "Smother", Leaking, Dot1, 2, 3, $"Each heat stack gives {N(T.SmotherPoints)} less Damage.",
              conditional: true)
              .Cap("Wet Ash", $"Fuel stops working, and below {T.WetAshBelowStacks} heat stacks you take {Pct(T.WetAshTaken)} more damage.")
              .ForElement(ElementType.Fire);
            C("low_water", "Low Water", Leaking, Dot2, 2, 3, $"Your surge gives {N(T.LowWaterSurge)} less Attack Speed.",
              conditional: true)
              .Cap("Ebb", $"soaked enemies stop filling your meter double, and outside a surge you take {Pct(T.EbbTaken)} more damage.")
              .ForElement(ElementType.Water);
            C("restless", "Restless", Leaking, Dot3, 2, 3,
              $"After {Sec(T.RestlessStillSeconds)} standing still you take +{Pct(T.RestlessTaken)} damage (outside the floor).",
              conditional: true)
              .Cap("Quicksand", $"standing still also costs {N(T.QuicksandSpeed)} Attack Speed.")
              .ForElement(ElementType.Earth);
            C("becalmed", "Becalmed", Leaking, Stroke, 2, 3,
              $"Your crit floor climbs {Pct(T.BecalmedCritPerHit)} less per hit.", conditional: true)
              .Cap("Doldrums", $"a hit you take resets your streak, and below {Pct(T.DoldrumsBelow)} momentum you take {Pct(T.DoldrumsTaken)} more damage.")
              .ForElement(ElementType.Air);

            // ============================================================ WEAPON CLASSES

            B("fletching", "Fletching", Edge, Dot3, 1, 2, $"+{N(T.FletchingPierce)} Pierce.",
              (m, n) =>
              {
                  m.Add(StatKind.Pierce, T.FletchingPierce * n);
                  if (n >= 2) m.Add(StatKind.Pierce, T.BroadheadPierce);
              })
              .Cap("Broadhead", $"+{N(T.BroadheadPierce)} Pierce.")
              .ForClasses(WeaponClass.Bow);
            B("ricochet", "Ricochet", Edge, Ring, 1, 2, "Thrown discs bounce to one more enemy.",
              (m, n) =>
              {
                  m.ExtraRicochets += T.RicochetBounces * n;
                  if (n >= 2) m.Add(StatKind.Cleave, T.BoomerangCleave);
              })
              .Cap("Boomerang", $"+{N(T.BoomerangCleave)} Cleave.")
              .ForClasses(WeaponClass.Disc);

            // ============================================================ ALBEDOS (made only by a circle)

            A("gilding", "Gilding", "dross", Anvil, Cross, $"+{N(T.GildingArt)} Weapon Art.",
              (m, n) => m.Add(StatKind.FinisherPower, T.GildingArt));
            A("mollification", "Mollification", "induration", Edge, Drop,
              $"Enemies have {Pct(T.MollificationHealth)} less health and armour (not bosses).",
              (m, n) => m.EnemyHealthMul *= 1f - T.MollificationHealth);
            A("honed", "Honed", "dulled", Edge, Bar,
              $"Your hits on enemies above {Pct(T.HonedAbove)} health have +{Pct(T.HonedCrit)} crit chance.", conditional: true);
            A("deliberate", "Deliberate", "heavy_arms", Edge, Dot2,
              $"Each basic in a chain gets +{N(T.DeliberateDamagePerBasic)} Damage for every basic before it, the weapon art too.",
              conditional: true);
            A("cementation", "Cementation", "cold_iron", Edge, Cross,
              $"Every {T.CementationEvery}th hit on the same enemy has +{Pct(T.ForcedCritChance)} crit chance.", conditional: true);
            A("felicity", "Felicity", "fumbler", Edge, Dot1, "One swing in nine strikes twice.", conditional: true);
            A("committed", "Committed", "overcommitted", Anvil, Dot3,
              $"-{Pct(T.CommittedTaken)} damage taken while a weapon art locks you.", conditional: true);
            A("close_quarters", "Close Quarters", "short_arm", Edge, Stroke,
              $"Hits within half your reach get +{N(T.CloseQuartersDamage)} Damage.", conditional: true);
            A("fury_of_the_frail", "Fury of the Frail", "thin_blood", Hide, Drop,
              $"Below {Pct(T.FuryBelow)} health, +{N(T.FuryDamage)} Damage.", conditional: true);
            A("adamant", "Adamant", "paper_guard", Hide, Bar, $"+{N(T.AdamantResilience)} Resilience.",
              (m, n) => m.Add(StatKind.Resilience, T.AdamantResilience));
            A("vital_spark", "Vital Spark", "slow_knit", Hide, Ring,
              $"You regenerate {Pct(T.VitalSparkRegen)} of max health a second (inside the healing limit).", conditional: true);
            A("patina", "Patina", "rust", Hide, Stroke, "Worn armour no longer makes you take more damage.",
              (m, n) => m.ArmourWearIgnored = true);
            A("pelican", "Pelican", "blood_price", Hide, Drop,
              $"Every swing heals {Pct(T.PelicanHeal)} of max health (inside the healing limit).", conditional: true);
            A("viriditas", "Viriditas", "withering", Hide, Dot3,
              $"Max health +{Pct(T.ViriditasPerFloor)} for every floor cleared (up to +{Pct(T.ViriditasCap)}).",
              conditional: true);
            A("tribute", "Tribute", "toll", Ledger, Dot2, $"Clearing a floor heals {Pct(T.TributeHeal)} of max health.",
              conditional: true);
            A("maturation", "Maturation", "souring", Edge, Stroke,
              $"+{N(T.MaturationDamage)} Damage for every {Sec(T.SouringEverySeconds)} on a floor (up to +{N(T.MaturationCap)}).",
              conditional: true);
            A("rebound", "Rebound", "backfire", Azoth, Cross, $"A release heals {Pct(T.ReboundHeal)} of max health.",
              conditional: true);
            A("lightfoot", "Lightfoot", "anchored", Quick, Bar,
              $"+{N(T.LightfootMove)} Move Speed, and Graze counts double at full speed.",
              (m, n) => m.Add(StatKind.MoveSpeed, T.LightfootMove), conditional: true);
            A("unshackled", "Unshackled", "encumbered", Quick, Dot3,
              $"A parry staggers every enemy within {N(T.UnshackledRadius)} units for {Sec(T.UnshackledStagger)}.",
              conditional: true);
            A("concentrate", "Concentrate", "stubborn_ore", Azoth, Dot3, $"+{N(T.ConcentratePower)} Elemental Power.",
              (m, n) => m.Add(StatKind.ElementalEffectiveness, T.ConcentratePower));
            A("sealed_vessel", "Sealed Vessel", "leaky_vessel", Azoth, Drop,
              $"Your meter never fades, and every hit you take adds {Pct(T.SealedVesselGain)} to it.",
              (m, n) => m.DecayMul = 0f, conditional: true);
            A("golden_chain", "Golden Chain", "long_chain", Anvil, Dot2,
              $"Every {T.GoldenChainEvery}rd weapon art comes with no basics before it.", conditional: true);
            A("lucid", "Lucid", "fog", Ledger, Ring, "An enemy winding up an attack on you is outlined.", conditional: true);
            A("basilisk", "Basilisk", "wandering_eye", Ledger, Dot2, "Auto-target always picks the weakest enemy in reach.",
              (m, n) => m.Targeting = TargetMode.Weakest);
            A("ossuary", "Ossuary", "dead_weight", Hide, Cross, "Corpses block enemies instead of you.", conditional: true);
            A("antipathy", "Antipathy", "retrograde", Quick, Stroke,
              $"Every {Sec(T.AntipathyEvery)}, enemies near you are staggered for {Sec(T.AntipathySeconds)}.", conditional: true);
            A("ley_lines", "Ley Lines", "projection", Azoth, Bar,
              $"Every {Sec(T.LeyLinesEvery)}, lines are drawn through the nearest enemies and burn them.", conditional: true);
            A("phlogiston", "Phlogiston", "smother", Azoth, Dot1, "Heat stacks give double Damage.", conditional: true);
            A("flood", "Flood", "low_water", Azoth, Dot2, $"Soaked enemies take {Pct(T.FloodVulnerability)} more.",
              conditional: true);
            A("mountain", "Mountain", "restless", Azoth, Dot3, $"While planted, +{N(T.MountainDamage)} Damage.",
              conditional: true);
            A("gale", "Gale", "becalmed", Azoth, Stroke, "Your crit floor climbs twice as fast.", conditional: true);

            // ============================================================ COMBINATIONS

            // Conjunctions: boon + boon.
            Combo(EntryOrigin.Conjunction, "oracle", "Oracle", "scrying_glass", "transmuters_eye", Ledger, Ring,
                  "Each slate shows the deal that would follow it.", conditional: true);
            Combo(EntryOrigin.Conjunction, "wellspring", "Wellspring", "overflow", "rich_vein", Azoth, Dot2,
                  T.WellspringEvery == 2 ? "Every second release fires twice." : $"Every {T.WellspringEvery}th release fires twice.",
                  conditional: true);
            Combo(EntryOrigin.Conjunction, "phoenix", "Phoenix", "second_wind", "thickened_hide", Hide, Stroke,
                  $"When Second Wind catches you, you rise at {Pct(T.PhoenixRise)} health and the blast throws enemies back.",
                  conditional: true);
            Combo(EntryOrigin.Conjunction, "gemini", "Gemini", "reiteration", "vein_finder", Edge, Ring,
                  "Repeats strike for the whole hit.", conditional: true);
            Combo(EntryOrigin.Conjunction, "damascene", "Damascene", "whetstone", "heavy_payoff", Anvil, Bar,
                  $"A weapon art scores its target: your next {T.DamasceneHits} hits on it get +{N(T.DamasceneDamage)} Damage.",
                  conditional: true);
            Combo(EntryOrigin.Conjunction, "hunt", "Hunt", "eagle", "executioner", Edge, Drop,
                  $"After a kill, your hits for {Sec(T.HuntSeconds)} get Executioner's bonus whatever the target's health.",
                  conditional: true);
            Combo(EntryOrigin.Conjunction, "athanor", "Athanor", "stonestance", "aegis_cycle", Hide, Ring,
                  "The ward renews twice as fast while you stand still.", conditional: true);
            Combo(EntryOrigin.Conjunction, "cataclysm", "Cataclysm", "elixir", "dilation", Azoth, Cross,
                  $"Your releases stagger everything they hit for {Sec(T.CataclysmStagger)}.", conditional: true);

            // Putrefactions: cost + cost.
            Combo(EntryOrigin.Putrefaction, "glass_bones", "Glass Bones", "thin_blood", "paper_guard", Brittle, Cross,
                  $"Below {Pct(T.GlassBonesBelow)} health, +{Pct(T.GlassBonesTaken)} damage taken (outside the floor).",
                  conditional: true);
            Combo(EntryOrigin.Putrefaction, "sol_niger", "Sol Niger", "fog", "wandering_eye", Blind, NoMark,
                  $"Enemies farther than {N(T.SolNigerBeyond)} units are drawn as silhouettes, telegraphs and all.",
                  (m, n) => m.SilhouetteBeyond = T.SolNigerBeyond);
            Combo(EntryOrigin.Putrefaction, "haemophilia", "Haemophilia", "blood_price", "slow_knit", Tithe, Drop,
                  $"Every hit you take also bleeds you for {Pct(T.HaemophiliaFraction)} of it over {Sec(T.HaemophiliaSeconds)}.",
                  conditional: true);
            Combo(EntryOrigin.Putrefaction, "senescence", "Senescence", "souring", "withering", Tithe, Stroke,
                  $"Each floor you clear, enemies hit {Pct(T.SenescenceTaken)} harder for the rest of the run.",
                  conditional: true);

            // Citrinitas: boon + cost - a boon that feeds on the cost, and fades if it is transmuted away.
            Combo(EntryOrigin.Citrinitas, "bloodstone", "Bloodstone", "blood_price", "bloodletters_pact", Hide, Drop,
                  $"+1 Damage for every {Pct(T.BloodstoneMissingPerPoint)} of health you're missing (up to +{N(T.BloodstoneCap)}).",
                  conditional: true);
            Combo(EntryOrigin.Citrinitas, "ponderous", "Ponderous", "heavy_arms", "heavy_payoff", Anvil, Dot2,
                  "Attack speed lost to costs becomes Weapon Art, point for point.",
                  (m, n) => m.Add(StatKind.FinisherPower, -m.Down[(int)StatKind.AttackSpeed]));
            Combo(EntryOrigin.Citrinitas, "iron_rhythm", "Iron Rhythm", "fumbler", "reiteration", Edge, Dot1,
                  "A swing that passes through makes your next hit repeat.", conditional: true);
            Combo(EntryOrigin.Citrinitas, "slow_fire", "Slow Fire", "stubborn_ore", "elixir", Azoth, Dot3,
                  $"Releases get +{N(T.SlowFirePowerPerSecond)} Elemental Power for every second the meter took to fill " +
                  $"(up to +{N(T.SlowFireCap)}).", conditional: true);
            Combo(EntryOrigin.Citrinitas, "blindsight", "Blindsight", "fog", "vein_finder", Edge, Ring,
                  $"+{Pct(T.BlindsightCrit)} crit chance while Fog hides your target.",
                  (m, n) => { if (m.HideTargetHighlight) m.BonusCrit += T.BlindsightCrit; });
            Combo(EntryOrigin.Citrinitas, "retrograde_motion", "Retrograde Motion", "retrograde", "fleetfoot", Quick, Stroke,
                  $"While inverted, +{N(T.RetrogradeMotionMove)} Move Speed and {Pct(1f - T.RetrogradeMotionTakenMul)} less damage taken.",
                  conditional: true);
        }

        // ---------------------------------------------------------------- the fluent half

        static ExchangeEntry Cap(this ExchangeEntry e, string name, string text)
        {
            e.CapName = name;
            e.CapText = text;
            return e;
        }

        static ExchangeEntry Recurs(this ExchangeEntry e, Action<PendingOffer> onTaken)
        {
            e.Recurring = true;
            e.OnTaken = onTaken;
            return e;
        }

        static ExchangeEntry ForElement(this ExchangeEntry e, ElementType element)
        {
            e.Element = element;
            return e;
        }

        static ExchangeEntry ForClasses(this ExchangeEntry e, params WeaponClass[] classes)
        {
            e.Classes = classes;
            return e;
        }
    }
}
