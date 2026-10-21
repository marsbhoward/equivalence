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

        /// <summary>The run format the catalogue's words were built for: the per-floor entries
        /// (Withering, Viriditas, Senescence) build per DEPTH, so a Short floor's step is twice
        /// Classic's and the card says so. A format switch rebuilds the words.</summary>
        static RunFormatId _builtFor;
        static int PerFloor => RunFormat.DepthPerFloor;

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

        /// <summary>An EVOLUTION: a form grown with its catalyst (Phase 5), never drawn - it takes the
        /// form's slot and socket when it evolves.</summary>
        static ExchangeEntry Evolution(string id, string name, string from, string catalyst, ExchangeFamily fam,
                                       ExchangeMark mark, string effect)
        {
            var e = Add(id, name, ExchangeKind.Boon, fam, mark, 3, 1, effect, null, true);
            e.Origin = EntryOrigin.Evolution;
            e.EvolvesFrom = from;
            e.Catalyst = catalyst;
            if (_byId.TryGetValue(from, out var f)) e.Slot = f.Slot;
            else Debug.LogError($"[Exchange] evolution '{id}' grows from '{from}', which does not exist");
            return e;
        }

        /// <summary>A UNION: granted when two opposite humours are held level (Phase 3), never drawn.</summary>
        static ExchangeEntry Union(string id, string name, ExchangeFamily fam, ExchangeMark mark, string effect)
        {
            var e = Add(id, name, ExchangeKind.Boon, fam, mark, 3, 1, effect, null, true);
            e.Origin = EntryOrigin.Union;
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
            if (_all != null && _builtFor == RunFormat.Current) return;
            _builtFor = RunFormat.Current;
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
              .Cap("Keen Edge", $"Your hits strip armour {Pct(T.KeenEdgeShred - 1f)} faster.");
            B("quickening", "Quickening", Edge, Dot2, 2, 3, $"+{N(T.QuickeningSpeed)} Attack Speed.",
              (m, n) =>
              {
                  m.Add(StatKind.AttackSpeed, T.QuickeningSpeed * n);
                  if (n >= 3) m.LockMul *= T.CelerityLockMul;
              })
              .Cap("Celerity", $"Weapon arts lock you {Pct(1f - T.CelerityLockMul)} shorter.");
            B("vein_finder", "Vein Finder", Edge, Dot3, 2, 3, $"+{Pct(T.VeinFinderCrit)} crit chance.",
              (m, n) => m.BonusCrit += T.VeinFinderCrit * n, conditional: true)
              .Cap("Fulminate", $"A crit bursts onto nearby enemies for {Pct(T.FulminateFraction)} of the hit.");
            B("executioner", "Executioner", Edge, Drop, 2, 2,
              $"+{N(T.ExecutionerDamage)} Damage to enemies under {Pct(T.ExecutionerBelow)} health.", conditional: true)
              .Cap("Coup de Grace", $"A non-elite you hit under {Pct(T.CoupDeGraceBelow)} health dies.");
            B("first_blood", "First Blood", Edge, Dot1, 2, 1,
              $"Your first hit on an unhurt enemy takes {Pct(T.FirstBloodShare)} of its health. Not bosses.",
              conditional: true);
            B("reiteration", "Reiteration", Edge, Ring, 2, 2,
              $"Every {T.ReiterationEvery}th hit ({T.ReiterationEvery - 1}th at II) strikes again for {Pct(T.ReiterationFraction)}.",
              conditional: true)
              .Cap("Rota", "A repeat that kills leaps to the next enemy.");
            B("long_reach", "Long Reach", Edge, Stroke, 1, 3, $"+{N(T.LongReachRange)} Range.",
              (m, n) => m.Add(StatKind.Range, T.LongReachRange * n), conditional: true)
              .Cap("Far Strike", $"+{N(T.FarStrikeDamage)} Damage at the edge of your reach.");
            B("wide_arc", "Wide Arc", Edge, Cross, 1, 2, $"+{Pct(T.WideArcWidth)} swing width.",
              (m, n) => m.StrikeWidthMul += T.WideArcWidth * n)
              .Cap("Scything", $"A swing that hits {T.ScythingBodies} or more enemies staggers them for {Sec(T.ScythingStagger)}.")
              .ForClasses(WeaponClass.Greatsword, WeaponClass.Disc);

            // ---- Anvil: weapon arts ----
            B("heavy_payoff", "Heavy Payoff", Anvil, Bar, 2, 3, $"+{N(T.HeavyPayoffArt)} Weapon Art.",
              (m, n) => m.Add(StatKind.FinisherPower, T.HeavyPayoffArt * n), conditional: true)
              .Cap("Crushing Blow", $"Weapon arts stagger for {Sec(T.CrushingBlowStagger)}.");
            B("ouroboros", "Ouroboros", Anvil, Ring, 2, 3,
              $"A kill has a {Pct(T.OuroborosChance)} chance to bank your weapon art. More for elites.", conditional: true)
              .Cap("The Serpent Eats", "A weapon art that kills banks the next.");
            B("green_lion", "Green Lion", Anvil, Drop, 2, 2, $"+{N(T.GreenLionArt)} Weapon Art on a PERFECT.",
              conditional: true)
              .Cap("Red Lion", "A GOOD no longer breaks your perfect streak.");
            B("short_chain", "Short Chain", Anvil, Dot1, 3, 1, "One basic fewer before each art.",
              (m, n) => m.BasicsPerChainDelta -= 1);
            B("extra_sigil", "Extra Sigil", Anvil, Dot3, 3, 1, "One more art in the rotation.",
              (m, n) => m.ExtraFinisherSlots += 1);

            // ---- Hide: defence ----
            B("thickened_hide", "Thickened Hide", Hide, Bar, 2, 3, $"+{N(T.ThickenedHideHp)}% max health.",
              (m, n) => m.Add(StatKind.MaxHp, T.ThickenedHideHp * n), conditional: true)
              .Cap("Fortitude", $"Under {Pct(T.FortitudeBelow)} health, take {Pct(1f - T.FortitudeMul)} less damage.");
            // One stack (2026-10-20): lifesteal heals no faster than Tuning.Stats.LifestealHealPerSecond,
            // a gear-tuned limit no boon may raise, and a strong character's damage fills it with the
            // first stack - stacks two and three added nothing. Transfusion is folded in.
            B("bloodletters_pact", "Bloodletter's Pact", Hide, Drop, 3, 1,
              $"+{Pct(T.BloodletterLifesteal)} lifesteal, from releases, burns and bleeds too.",
              (m, n) => m.BonusLifesteal += T.BloodletterLifesteal * n, conditional: true);
            B("stonestance", "Stonestance", Hide, Dot1, 2, 3, $"+{N(T.StonestanceBrace)} Brace.",
              (m, n) => m.Add(StatKind.Brace, T.StonestanceBrace * n), conditional: true)
              .Cap("Lapis", $"After {Sec(T.LapisStillSeconds)} standing still, your next hit taken is halved.");
            B("reactive_plate", "Reactive Plate", Hide, Dot2, 2, 2,
              $"When hit, take {Pct(T.ReactivePlateReduction)} less ({Pct(T.ReactivePlateReductionII)} at II) for {Sec(T.ReactivePlateSeconds)}. " +
              $"Once every {Sec(T.ReactivePlateCooldown)}.",
              conditional: true)
              .Cap("Tempered", $"Once every {Sec(T.ReactivePlateCooldownTempered)}.");
            B("aegis_cycle", "Aegis Cycle", Hide, Ring, 3, 2,
              $"A ward cancels one hit. It returns after {Sec(T.AegisRenewSeconds)} ({Sec(T.AegisRenewSecondsII)} at II). " +
              $"{Sec(T.NegationGapSeconds)} between cancels.",
              conditional: true)
              .Cap("Tin Ward", $"A broken ward bursts for {N(T.TinWardHitUnits)} hits around you.");
            B("second_wind", "Second Wind", Hide, Stroke, 3, 1,
              $"Once a floor, survive a killing blow at 1 health. Heal {Pct(T.SecondWindHeal)} over {Sec(T.SecondWindHealSeconds)}.",
              conditional: true);
            // Kiln-Fired was cut 2026-10-20 (Tuning.Exchange.RustPlates).

            // ---- Quicksilver: movement ----
            B("fleetfoot", "Fleetfoot", Quick, Dot2, 1, 3, $"+{N(T.FleetfootMove)} Move Speed.",
              (m, n) => m.Add(StatKind.MoveSpeed, T.FleetfootMove * n), conditional: true)
              .Cap("Wake", $"After {Sec(T.WakeAfterSeconds)} at full speed, your next basic has +{N(T.WakeDamage)} Damage.");
            B("eagle", "Eagle", Quick, Stroke, 1, 2,
              $"A kill gives +{N(T.EagleMove)} Move Speed for {Sec(T.EagleSeconds)} ({Sec(T.EagleSecondsII)} at II).",
              conditional: true)
              .Cap("Stoop", $"After a kill, your next hit within {Sec(T.StoopSeconds)} has +{Pct(T.ForcedCritChance)} crit chance.");
            B("evanescence", "Evanescence", Quick, Bar, 2, 3, $"+{N(T.EvanescenceGraze)} Graze.",
              (m, n) => m.Add(StatKind.Graze, T.EvanescenceGraze * n), conditional: true)
              .Cap("Vapour", $"While moving, every {T.VapourEvery}th hit taken passes through you. " +
                              $"{Sec(T.NegationGapSeconds)} between cancels.");
            // EQUIPOISE (2026-10-21): Fleetfoot, Evanescence and Stonestance as ONE arc - move, then
            // stand. Those three are Retired (see the RETIRED list below).
            B("equipoise", "Equipoise", Quick, Cross, 2, 3,
              $"I: +{N(T.EquipoiseMove)} Move Speed, +{N(T.EquipoiseGraze)} Graze. II: +{N(T.EquipoiseBrace)} Brace.",
              (m, n) =>
              {
                  m.Add(StatKind.MoveSpeed, T.EquipoiseMove);
                  m.Add(StatKind.Graze, T.EquipoiseGraze);
                  if (n >= 2) m.Add(StatKind.Brace, T.EquipoiseBrace);
              }, conditional: true)
              .Cap("Poise", $"Changing pace halves your next hit taken.");
            B("ghostwalk", "Ghostwalk", Quick, Ring, 3, 1,
              $"When hit, {Sec(T.GhostwalkSeconds)} untouchable. Once every {Sec(T.GhostwalkCooldown)}. " +
              $"{Sec(T.NegationGapSeconds)} between cancels.",
              conditional: true);

            // ---- Azoth: the element ----
            B("attunement", "Attunement", Azoth, Dot1, 1, 2, $"Start each floor with {Pct(T.AttunementFill)} meter.",
              (m, n) => m.StartMeterFraction = n >= 2 ? 1f : T.AttunementFill * n, conditional: true)
              .Cap("Primed", "Start each floor with a full meter.");
            B("rich_vein", "Rich Vein", Azoth, Dot3, 1, 3, $"+{N(T.RichVeinGrowth)} Element Growth.",
              (m, n) => m.Add(StatKind.ElementGrowth, T.RichVeinGrowth * n), conditional: true)
              .Cap("Mother Lode", $"A kill refills {Pct(T.MotherLodeRefill)} meter. More for elites.");
            B("elixir", "Elixir", Azoth, Drop, 1, 3, $"+{N(T.ElixirPower)} Elemental Power.",
              (m, n) => m.Add(StatKind.ElementalEffectiveness, T.ElixirPower * n), conditional: true)
              .Cap("Grand Elixir", "Releases can crit.");
            B("dilation", "Dilation", Azoth, Ring, 1, 3, $"+{N(T.DilationArea)} Area.",
              (m, n) => m.Add(StatKind.AoeRadius, T.DilationArea * n), conditional: true)
              .Cap("Expansion", $"Area attacks lose {Pct(1f - T.ExpansionEdgeLoss)} less at the edge.");
            B("residue", "Residue", Azoth, Cross, 1, 1,
              $"A release leaves your element on the ground for {Sec(T.ResidueSeconds)}.",
              conditional: true);
            B("overflow", "Overflow", Azoth, Bar, 3, 1, $"A release refunds {Pct(T.OverflowRefund)} of its meter.",
              conditional: true);
            B("twin_spark", "Twin Spark", Azoth, Dot2, 3, 1,
              $"Releases fire again {Sec(T.TwinSparkDelay)} later at {Pct(T.TwinSparkScale)}, free.",
              conditional: true);

            // ---- Ledger: the run itself ----
            B("curator", "Curator", Ledger, Dot1, 2, 1, "Floor rewards can be rerolled once.",
              (m, n) => m.ExtraRewardOptions += T.CuratorCards);
            B("transmuters_eye", "Transmuter's Eye", Ledger, Ring, 2, 1, "See the floor reward before the deal.",
              conditional: true);
            B("scrying_glass", "Scrying Glass", Ledger, Dot2, 2, 1,
              "See what the next floor holds.", conditional: true);
            B("lodestone", "Lodestone", Ledger, Stroke, 1, 1, "Spire boons last one more floor.",
              (m, n) => m.SpireExtraFloors += T.LodestoneFloors);
            B("prima_materia", "Speculum", Ledger, Cross, 2, 1, "The next deal shows three pairs.")
              .Recurs(p => p.PairsDelta += 1);

            // ============================================================ COSTS

            // ---- Blunted: offence ----
            C("dulled", "Dulled", Blunt, Bar, 2, 3, $"-{N(T.DulledDamage)} Damage.",
              (m, n) => m.Add(StatKind.Damage, -T.DulledDamage * n), conditional: true)
              .Cap("Rebated", "Every hit rolls its lowest damage.");
            C("heavy_arms", "Heavy Arms", Blunt, Dot2, 2, 3, $"-{N(T.HeavyArmsSpeed)} Attack Speed.",
              (m, n) =>
              {
                  m.Add(StatKind.AttackSpeed, -T.HeavyArmsSpeed * n);
                  if (n >= 3) m.UpVoid[(int)StatKind.AttackSpeed] = true;
              })
              .Cap("Leaden Limbs", "Attack speed from boons and spires stops counting.");
            C("cold_iron", "Cold Iron", Blunt, Cross, 2, 3,
              "Crits lose a third of their bonus (all at III).", conditional: true)
              .Cap("Quenched", $"Crits deal {Pct(1f - T.QuenchedMul)} less than a normal hit.");
            C("fumbler", "Fumbler", Blunt, Dot1, 2, 3, "One swing in nine misses.",
              conditional: true)
              .Cap("Lapsus", $"A miss stuns you for {Sec(T.LapsusStumble)}.");
            C("overcommitted", "Overcommitted", Blunt, Dot3, 2, 3, $"Weapon arts lock you {Pct(T.OvercommittedLock)} longer.",
              (m, n) => m.LockMul *= 1f + T.OvercommittedLock * n, conditional: true)
              .Cap("Overextended", $"+{Pct(T.OverextendedTaken)} damage taken while an art locks you.");
            C("dross", "Dross", Blunt, Ring, 2, 3, $"-{N(T.DrossArt)} Weapon Art.",
              (m, n) =>
              {
                  m.Add(StatKind.FinisherPower, -T.DrossArt * n);
                  if (n >= 3) m.ArtsCantCrit = true;
              })
              .Cap("Slag", "Weapon arts can't crit.");
            C("induration", "Induration", Blunt, Drop, 2, 3, $"Enemies have {Pct(T.IndurationHealth)} more health and armour. Not bosses.",
              (m, n) =>
              {
                  m.EnemyHealthMul *= 1f + T.IndurationHealth * n;
                  if (n >= 3) m.EliteHealthMul *= 1f + T.CoagulationEliteHealth;
              })
              .Cap("Coagulation", $"Elites have {Pct(T.CoagulationEliteHealth)} more on top.");
            C("short_arm", "Short Arm", Blunt, Stroke, 1, 3, $"-{N(T.ShortArmRange)} Range.",
              (m, n) => m.Add(StatKind.Range, -T.ShortArmRange * n), conditional: true)
              .Cap("Cramped", $"-{N(T.CrampedDamage)} Damage beyond half your reach.");

            // ---- Brittle: defence ----
            C("thin_blood", "Thin Blood", Brittle, Drop, 2, 3, $"-{N(T.ThinBloodHp)}% max health.",
              (m, n) => m.Add(StatKind.MaxHp, -T.ThinBloodHp * n), conditional: true)
              .Cap("Anaemia", $"Healing above {Pct(T.AnaemiaAbove)} health is halved.");
            C("paper_guard", "Paper Guard", Brittle, Bar, 2, 3,
              $"+{Pct(T.PaperGuardTaken)} damage taken.",
              (m, n) =>
              {
                  m.DamageTakenOutside *= 1f + T.PaperGuardTaken * n;
                  if (n >= 3) m.MitigationFloor = Mathf.Max(m.MitigationFloor, T.ExposedFloor);
              })
              .Cap("Exposed", $"Your mitigation floor rises to {Pct(T.ExposedFloor)}.");
            C("slow_knit", "Slow Knit", Brittle, Ring, 2, 3, $"-{Pct(T.SlowKnitHeal)} healing.",
              (m, n) =>
              {
                  m.HealMul *= 1f - T.SlowKnitHeal * n;
                  if (n >= 3) m.HealCeiling = Mathf.Min(m.HealCeiling, T.HollowCeiling);
              })
              .Cap("Hollow", $"Healing stops at {Pct(T.HollowCeiling)} health.");
            C("rust", "Rust", Brittle, Stroke, 1, 3, $"Plating covers {N(T.RustPlates)} fewer blows a floor.",
              (m, n) =>
              {
                  m.PlatingLost += T.RustPlates * n;
                  if (n >= 3) m.RepairMul *= T.CorrosionRepairMul;
              })
              .Cap("Corrosion", "Repairs are halved. Worn armour's damage can't be reduced.");
            C("open_stance", "Open Stance", Brittle, Dot1, 1, 1,
              T.OpenStanceHits == 1 ? "Your first hit taken each floor is doubled."
                                    : $"Your first {T.OpenStanceHits} hits taken each floor are doubled.", conditional: true);

            // ---- Tithe: prices paid ----
            C("blood_price", "Blood Price", Tithe, Bar, 3, 2, $"Each swing costs {Pct(T.BloodPriceSwing)} max health.",
              conditional: true)
              .Cap("Haemorrhage", $"Doubled under {Pct(T.HaemorrhageBelow)} health.");
            C("withering", "Withering", Tithe, Dot3, 3, 2,
              $"-{Pct(T.WitheringPerFloor * PerFloor)} max health each floor cleared, to -{Pct(T.WitheringCap)}.",
              conditional: true)
              .Cap("Desiccation", "No limit.");
            C("toll", "Toll", Tithe, Dot2, 1, 3, $"Each floor cleared costs {Pct(T.TollFraction)} health.",
              conditional: true)
              .Cap("Usury", "The toll is taken from max health.");
            C("souring", "Souring", Tithe, Stroke, 2, 3,
              $"-{N(T.SouringDamage)} Damage every {Sec(T.SouringEverySeconds)} on a floor.",
              conditional: true)
              .Cap("Acetum", $"Enemies hit {Pct(T.AcetumTaken)} harder every {Sec(T.SouringEverySeconds)} too.");
            C("backfire", "Backfire", Tithe, Cross, 2, 3, $"Each release costs {Pct(T.BackfireFraction)} max health.",
              conditional: true)
              .Cap("Recoil", $"A release stuns you for {Sec(T.RecoilSeconds)}.");
            // The deal shapers were free (2026-10-10): run with them out of the pool, careful runs
            // ended no stronger - they filled a cost slot a real cost would have. Each now takes
            // something a deal is worth.
            C("caput_mortuum", "Caput Mortuum", Tithe, Ring, 2, 1, "The next two deals: one pair each, no refusing.")
              .Recurs(p => { p.PairsDelta -= 1; p.PairsDeltaNext -= 1; p.Forced = true; p.ForcedNext = true; });
            C("indenture", "Indenture", Tithe, Dot1, 2, 1, "The next deal: no refusing, lighter boons.")
              .Recurs(p => { p.Forced = true; p.BoonWeightDelta -= 1; });
            C("debt", "Debt", Tithe, NoMark, 3, 1, "The next deal: a cost and no boon, no refusing.")
              .Recurs(p => { p.NoBoon = true; p.Forced = true; });

            // ---- Leaden: movement ----
            C("anchored", "Anchored", Leaden, Bar, 2, 3, $"-{N(T.AnchoredMove)} Move Speed.",
              (m, n) =>
              {
                  m.Add(StatKind.MoveSpeed, -T.AnchoredMove * n);
                  if (n >= 3) m.ExtraSlide += T.MiredSlide;
              }, conditional: true)
              .Cap("Mired", "Sand and mire slow you double. You slide like on water.");
            C("encumbered", "Encumbered", Leaden, Dot3, 2, 3, $"+{Pct(T.EncumberedCooldown)} defensive ability cooldown.",
              (m, n) => m.DefenseCooldownMul *= 1f + T.EncumberedCooldown * n, conditional: true)
              .Cap("Shackled", $"No defensive ability for {Sec(T.ShackledSeconds)} after each art.");
            C("rooted", "Rooted", Leaden, Cross, 2, 1, "You can't move while swinging.",
              (m, n) => m.RootedWhileSwinging = true);

            // ---- Leaking: the element and the chain ----
            C("stubborn_ore", "Stubborn Ore", Leaking, Dot3, 2, 3,
              $"-{N(T.StubbornOreGrowth)} Element Growth.",
              (m, n) => m.Add(StatKind.ElementGrowth, -T.StubbornOreGrowth * n), conditional: true)
              .Cap("Barren", "Your meter empties each floor.");
            C("leaky_vessel", "Leaky Vessel", Leaking, Drop, 1, 3, $"Your meter fades {Pct(T.LeakyVesselDecay)} faster.",
              (m, n) => m.DecayMul *= 1f + T.LeakyVesselDecay * n, conditional: true)
              .Cap("Cracked Vessel", $"Each hit taken spills {Pct(T.CrackedVesselSpill)} meter.");
            C("long_chain", "Long Chain", Leaking, Dot2, 2, 2, "One more basic before each art.",
              (m, n) =>
              {
                  m.BasicsPerChainDelta += n;
                  if (n >= 2) m.ComboTimeMul *= T.FrayingComboMul;
              })
              .Cap("Fraying", "Unfinished chains fade twice as fast.");

            // ---- Blindfold: what you can read ----
            C("fog", "Fog", Blind, Ring, 1, 3,
              "I: no target rim. II: no rotation display. III: no damage numbers or armour rings.",
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
              .Cap("Murk", "Enemy telegraphs are faint.");
            C("wandering_eye", "Wandering Eye", Blind, Dot2, 1, 3, "Auto-target switches targets more.",
              (m, n) =>
              {
                  m.SwitchAdvantageDelta += T.WanderingEyeSwitch * n;
                  if (n >= 3) m.Targeting = TargetMode.Random;
              })
              .Cap("Blind Rage", "Auto-target picks at random.");
            C("dead_weight", "Dead Weight", Blind, Cross, 1, 3,
              $"Your kills leave corpses that block you for {Sec(T.DeadWeightSeconds + 1f)} " +
              $"(+{Sec(1f)} a stack).", conditional: true)
              .Cap("Charnel", "Corpses stop your throws and arrows too.");
            C("retrograde", "Retrograde", Blind, Stroke, 3, 2,
              $"Every {Sec(T.RetrogradeEvery)} ({Sec(T.RetrogradeEveryII)} at II) your movement inverts for {Sec(T.RetrogradeSeconds)}. " +
              $"A closing ring warns you.", conditional: true)
              .Cap("Contrary", $"Each hit taken inverts you for {Sec(T.ContrarySeconds)}.");
            C("projection", "Projection", Blind, Bar, 3, 2,
              $"Every {Sec(T.ProjectionEvery)} ({Sec(T.ProjectionEveryII)} at II), {T.ProjectionLines} lines cross the floor. " +
              $"They burn {Sec(T.ProjectionWarning)} later, for {Sec(T.ProjectionBurn)}.", conditional: true)
              .Cap("Lattice", "A second set crosses through you.");

            // ============================================================ ELEMENTS

            // The trap boons: stack II is their capstone - the same bonus for all four, the same time.
            string trap = $"+{N(T.TrapBonusDamage)} Damage";
            B("salamander", "Salamander", Hide, Drop, 2, 2, "Fire pits don't burn you.", conditional: true)
              .Cap("Salamander II", $"{trap} in a burning pit and {Sec(T.TrapBonusSeconds)} after.")
              .ForElement(ElementType.Fire);
            B("gnome", "Gnome", Hide, Bar, 2, 2, "Sand doesn't slow you.", conditional: true)
              .Cap("Gnome II", $"{trap} in sand and {Sec(T.TrapBonusSeconds)} after.")
              .ForElement(ElementType.Earth);
            B("undine", "Undine", Hide, Ring, 2, 2, "Water doesn't slide or soak you.", conditional: true)
              .Cap("Undine II", $"{trap} in water and {Sec(T.TrapBonusSeconds)} after.")
              .ForElement(ElementType.Water);
            B("sylph", "Sylph", Hide, Stroke, 2, 2, "Tornadoes don't hurt you.", conditional: true)
              .Cap("Sylph II", $"{trap} for {Sec(T.TrapBonusSeconds)} after a tornado.")
              .ForElement(ElementType.Air);

            B("banked_embers", "Banked Embers", Azoth, Dot1, 1, 2, "Heat never falls below one stack (two at II).",
              conditional: true)
              .Cap("Hearth", $"A release leaves you at {T.HearthStacks} heat.")
              .ForElement(ElementType.Fire);
            B("high_tide", "High Tide", Azoth, Dot2, 2, 2, $"Surge lasts {Sec(T.HighTideSeconds)} longer.",
              conditional: true)
              .Cap("Spring Tide", "A surge soaks enemies around you.")
              .ForElement(ElementType.Water);
            B("deep_roots", "Deep Roots", Azoth, Dot3, 2, 2, $"Your charge holds for {Sec(T.DeepRootsHold)} after you move.",
              conditional: true)
              .Cap("Bedrock", $"Planted damage reduction holds {Sec(T.BedrockHold)} after you move.")
              .ForElement(ElementType.Earth);
            B("tailwind", "Tailwind", Azoth, Stroke, 2, 2, "Momentum fades half as fast (II: holds a second).",
              conditional: true)
              .Cap("Updraft", "Gust throws nearby enemies back.")
              .ForElement(ElementType.Air);

            C("smother", "Smother", Leaking, Dot1, 2, 3, $"Each heat gives {N(T.SmotherPoints)} less Damage.",
              conditional: true)
              .Cap("Wet Ash", "Fuel stops. Each hit taken puts out a heat.")
              .ForElement(ElementType.Fire);
            C("low_water", "Low Water", Leaking, Dot2, 2, 3, $"Surge gives {N(T.LowWaterSurge)} less Attack Speed.",
              conditional: true)
              .Cap("Ebb", "Soaked enemies stop filling double. A hit ends your surge.")
              .ForElement(ElementType.Water);
            C("restless", "Restless", Leaking, Dot3, 2, 3,
              $"After {Sec(T.RestlessStillSeconds)} still, take +{Pct(T.RestlessTaken)} damage.",
              conditional: true)
              .Cap("Quicksand", $"Standing still costs {N(T.QuicksandSpeed)} Attack Speed.")
              .ForElement(ElementType.Earth);
            C("becalmed", "Becalmed", Leaking, Stroke, 2, 3,
              $"Crit floor climbs {Pct(T.BecalmedCritPerHit)} less per hit.", conditional: true)
              .Cap("Doldrums", "A hit taken resets your streak and momentum.")
              .ForElement(ElementType.Air);

            // ============================================================ THE STATUS LAYER
            // Phase 1 (2026-10-20, EXCHANGE.md): sources and payoffs for the board's four humours,
            // each on a trigger or a verb the board's own rules don't use. Chance-based sources
            // carry the hit's proc weight (a thrown disc earns Tuning.Exchange.ProcDisc of a swing's).
            const ExchangeFamily Cho = ExchangeFamily.Choler, Phl = ExchangeFamily.Phlegm;
            const ExchangeFamily San = ExchangeFamily.Sanguine, Mel = ExchangeFamily.Melancholy;
            B("tinder", "Tinder", Cho, Dot1, 1, 1,
              $"Basics have a {Pct(T.TinderChance)} chance to burn: {Pct(T.TinderBurnShare)} of the hit a second for {Sec(T.TinderSeconds)}.",
              conditional: true);
            B("backdraft", "Backdraft", Cho, Ring, 1, 1,
              "A burning enemy that hits you takes its burn at once.", conditional: true);
            B("tidewater", "Tidewater", Phl, Dot1, 1, 1,
              $"Enemies that hit you are soaked for {Sec(T.TidewaterSeconds)}.", conditional: true);
            B("saturate", "Saturate", Phl, Ring, 1, 1,
              $"Soaking a soaked enemy staggers it for {Sec(T.SaturateStagger)}.", conditional: true);
            B("serrated", "Serrated", San, Dot1, 1, 1,
              $"Your first basic bleeds: {Pct(T.SerratedBleedShare)} of the hit a second for {Sec(T.SerratedSeconds)}.",
              conditional: true);
            B("lancet", "Lancet", San, Ring, 1, 1,
              $"Arts end bleeds, dealing {Pct(T.LancetShare)} of what was left at once.",
              conditional: true);
            B("knell", "Knell", Mel, Dot1, 1, 1,
              $"A PERFECT art staggers enemies within {N(T.KnellRadius)} for {Sec(T.KnellStagger)}.",
              conditional: true);
            B("pin_down", "Pin Down", Mel, Ring, 1, 1,
              $"Basics hold a stagger for at least {Sec(T.PinDownSeconds)}.",
              conditional: true);

            // ============================================================ FORMS (Phase 2)
            // A FORM changes what one action does (EXCHANGE.md 5.3). An Art form is inscribed on the
            // rotation socket the player picks, and its slate pays with a SOCKET COST on that same
            // socket. Forms sharing a slot share its strength; each names what weakens ("strength").
            // Names and numbers are Claude's, unconfirmed.
            B("aftershock", "Aftershock", Mel, Bar, 2, 1,
              $"Struck ground slows enemies for {Sec(T.AftershockSeconds)}.",
              conditional: true).Inscribed(FormSlot.Art);
            B("shockwave", "Shockwave", Cho, Bar, 2, 1,
              $"A ring around you: {N(T.ShockwaveHitUnits)} hits within {N(T.ShockwaveRadius)}. Burns.",
              conditional: true).Inscribed(FormSlot.Art);
            B("riptide", "Riptide", Phl, Bar, 2, 1,
              $"A wave {N(T.RiptideLength)} long along your aim. Soaks.",
              conditional: true).Inscribed(FormSlot.Art);
            B("fault_line", "Fault Line", Mel, Stroke, 2, 1,
              $"Basics: hitting {T.FaultLineBodies}+ enemies cracks the floor ahead. It strikes for {N(T.FaultLineHitUnits)} hit {Sec(T.FaultLineDelay)} later.",
              conditional: true).Inscribed(FormSlot.Basics).ForClasses(WeaponClass.Greatsword);
            B("splinter", "Splinter", San, Drop, 2, 1,
              $"Basics: a bounce throws a splinter for {Pct(T.SplinterShare)} of the hit. Bleeds.",
              conditional: true).Inscribed(FormSlot.Basics).ForClasses(WeaponClass.Disc);
            B("thornfield", "Thornfield", Phl, Stroke, 2, 1,
              $"Basics: a missed arrow becomes a caltrop. Slows for {Sec(T.ThornfieldSeconds)}.",
              conditional: true).Inscribed(FormSlot.Basics).ForClasses(WeaponClass.Bow);
            // Second batch (the user's picks, 2026-10-21): a basics form for the greatsword's duels, a
            // kill that gathers the crowd, a defensive ability that turns into the next attack. Weight 1:
            // they priced 0-5% against weight 2's 10% (Drawdown's worth is the crowd it gathers, unseen).
            // Spindrift (2026-10-21, the greatsword's third): every Phlegm mark was taken - the plain oval.
            B("spindrift", "Spindrift", Phl, NoMark, 1, 1,
              $"Basics: spray soaks enemies up to {N(T.SpindriftBeyond)} past your reach.",
              conditional: true).Inscribed(FormSlot.Basics).ForClasses(WeaponClass.Greatsword);
            B("lone_cut", "Lone Cut", San, Stroke, 1, 1,
              $"Basics: hitting a lone enemy bleeds it {Pct(T.LoneCutBleedShare)} of the hit a second for {Sec(T.LoneCutBleedSeconds)}.",
              conditional: true).Inscribed(FormSlot.Basics).ForClasses(WeaponClass.Greatsword);
            B("drawdown", "Drawdown", Phl, Dot2, 1, 1,
              $"Kills pull enemies within {N(T.DrawdownRadius)} a step closer.",
              conditional: true).Inscribed(FormSlot.Kill);
            B("afterburn", "Afterburn", Cho, Dot2, 1, 1,
              $"After your defensive ability, your next basic has +{N(T.AfterburnDamage)} Damage and burns.",
              conditional: true).Inscribed(FormSlot.Defence);
            // Class basics (the user's picks, 2026-10-21): two for the discs, two for the bow.
            B("lodestar", "Lodestar", Phl, Dot3, 1, 1,
              $"Basics: a disc banks off {(T.LodestarBanks == 1 ? "one wall" : $"{T.LodestarBanks} walls")}. A banked hit gets +{Pct(T.LodestarBonus)} and soaks.",
              conditional: true).Inscribed(FormSlot.Basics).ForClasses(WeaponClass.Disc);
            B("snapback", "Snapback", Cho, Dot3, 1, 1,
              $"Basics: a throw within {Sec(T.SnapbackWindow)} of a catch burns.",
              conditional: true).Inscribed(FormSlot.Basics).ForClasses(WeaponClass.Disc);
            B("patient_string", "Patient String", San, Dot2, 1, 1,
              $"Basics: a shot drawn standing still bleeds {Pct(T.PatientBleedShare)} of the hit over {Sec(T.PatientBleedSeconds)}.",
              conditional: true).Inscribed(FormSlot.Basics).ForClasses(WeaponClass.Bow);
            B("long_ember", "Long Ember", Cho, Drop, 1, 1,
              $"Basics: a shot landing past {Pct(T.LongEmberFrom)} of your reach burns.",
              conditional: true).Inscribed(FormSlot.Basics).ForClasses(WeaponClass.Bow);
            B("pyre", "Pyre", Cho, Cross, 2, 1,
              $"Kills leave burning ground for {Sec(T.PyreSeconds)}.",
              conditional: true).Inscribed(FormSlot.Kill);
            B("lacerate", "Lacerate", San, Cross, 2, 1,
              $"Your defensive ability bleeds enemies within {N(T.LacerateRadius)}.",
              conditional: true).Inscribed(FormSlot.Defence);

            // ============================================================ EVOLUTIONS AND GROWTH (Phase 5)
            // An EVOLUTION is a form grown: the form held with its humour's catalyst, it may evolve
            // at a circle or after a boss (Vampire Survivors' kind). Never drawn on a deal.
            Evolution("nova", "Nova", "shockwave", "tinder", Cho, Ring,
                      $"A ring around you: {N(T.NovaHitUnits)} hits within {N(T.NovaRadius)}. Burns longer.");
            Evolution("maelstrom", "Maelstrom", "riptide", "tidewater", Phl, Drop,
                      "A soaking wave that leaves a pool.");
            Evolution("hemorrhage", "Hemorrhage", "lacerate", "serrated", San, Ring,
                      $"Your defensive ability bleeds deep and staggers within {N(T.HemorrhageRadius)}.");
            Evolution("earthquake", "Earthquake", "aftershock", "knell", Mel, Drop,
                      "Struck ground slows and staggers.");
            // GROWTH (Balatro's kind): entries that grow by meeting their condition, shown on the HUD.
            B("kindling", "Kindling", Cho, Stroke, 1, 1,
              $"Burns grow {Pct(T.KindlingStep)} per {T.KindlingDeathsPerStep} burning kills, to {Pct(T.KindlingCap)}.",
              conditional: true);
            B("swell", "Swell", Phl, Cross, 1, 1,
              $"Soaks grow {Sec(T.SwellPerDeath)} per soaked kill this floor, to {Sec(T.SwellCap)}.",
              conditional: true);

            // ============================================================ OATHS AND BRANDS (Phase 4)
            // An OATH ends when its task is done - fulfilled, it leaves the ledger for the run. A
            // BRAND changes a rule; the brand deal after each mini-boss pays with one.
            C("unbloodied", "Unbloodied", Leaden, Dot1, 3, 1,
              $"Take {Pct(T.UnbloodiedTaken)} more damage until you clear a floor unhit.",
              conditional: true).Sworn();
            C("blood_debt", "Blood Debt", Leaden, Dot2, 2, 1,
              $"No healing until {N(T.BloodDebtWorth)} kills.",
              conditional: true).Sworn();
            C("sealed_sands", "Sealed Sands", Leaking, Ring, 3, 1,
              "Heat runs one tier ahead.", conditional: true).Branded().InShortOnly();

            // The socket costs: what an Art form's slate pays with, on the form's own socket.
            C("heavy_hand", "Heavy Hand", Leaden, Ring, 1, 1,
              $"This art locks you {Pct(T.HeavyHandLock)} longer.").OnSocket();
            C("dulled_script", "Dulled Script", Brittle, Dot3, 2, 1,
              $"This art deals {Pct(T.DulledScriptDamage)} less.").OnSocket();
            C("brittle_script", "Brittle Script", Brittle, Dot2, 1, 1,
              "This art can't crit.").OnSocket();

            // ============================================================ WEAPON CLASSES

            B("fletching", "Fletching", Edge, Dot3, 1, 2, $"+{N(T.FletchingPierce)} Pierce.",
              (m, n) => m.Add(StatKind.Pierce, T.FletchingPierce * n))
              .Cap("Broadhead", $"Arrows bleed {Pct(T.BroadheadBleedShare)} of the hit over {Sec(T.BroadheadBleedSeconds)}.")
              .ForClasses(WeaponClass.Bow);
            B("ricochet", "Ricochet", Edge, Ring, 1, 2, "Thrown discs bounce to one more enemy.",
              (m, n) => m.ExtraRicochets += T.RicochetBounces * n)
              .Cap("Eddy", $"Disc bounces soak for {Sec(T.EddySoakSeconds)}.")
              .ForClasses(WeaponClass.Disc);

            // ============================================================ ALBEDOS (made only by a circle)

            A("gilding", "Gilding", "dross", Anvil, Cross, $"+{N(T.GildingArt)} Weapon Art.",
              (m, n) => m.Add(StatKind.FinisherPower, T.GildingArt * m.PotencyOf("gilding")));
            A("mollification", "Mollification", "induration", Edge, Drop,
              $"Enemies have {Pct(T.MollificationHealth)} less health and armour. Not bosses.",
              (m, n) => m.EnemyHealthMul *= 1f - T.MollificationHealth * m.PotencyOf("mollification"));
            A("honed", "Honed", "dulled", Edge, Bar,
              $"+{Pct(T.HonedCrit)} crit chance on enemies above {Pct(T.HonedAbove)} health.", conditional: true);
            A("deliberate", "Deliberate", "heavy_arms", Edge, Dot2,
              $"+{N(T.DeliberateDamagePerBasic)} Damage per basic before it in the chain.",
              conditional: true);
            A("cementation", "Cementation", "cold_iron", Edge, Cross,
              $"Every {T.CementationEvery}th hit on one enemy has +{Pct(T.ForcedCritChance)} crit chance.", conditional: true);
            // Reworked 2026-10-20 (Phase 4): it was "one swing in nine strikes twice" - Reiteration's twin.
            A("felicity", "Felicity", "fumbler", Edge, Dot1,
              $"One swing in nine throws a crescent for {Pct(T.FelicityShare)} of the hit.",
              conditional: true);
            A("committed", "Committed", "overcommitted", Anvil, Dot3,
              $"Take {Pct(T.CommittedTaken)} less while an art locks you.", conditional: true);
            A("close_quarters", "Close Quarters", "short_arm", Edge, Stroke,
              $"+{N(T.CloseQuartersDamage)} Damage within half your reach.", conditional: true);
            // Reworked 2026-10-20: it was "+50 Damage below half health" - Bloodstone's twin.
            A("fury_of_the_frail", "Fury of the Frail", "thin_blood", Hide, Drop,
              $"Under {Pct(T.FuryBelow)} health, basics bleed {Pct(T.FuryBleedShare)} of the hit a second for {Sec(T.FuryBleedSeconds)}.",
              conditional: true);
            A("adamant", "Adamant", "paper_guard", Hide, Bar, $"+{N(T.AdamantResilience)} Resilience.",
              (m, n) => m.Add(StatKind.Resilience, T.AdamantResilience * m.PotencyOf("adamant")));
            // Reworked 2026-10-20: it was a regeneration - one of four "heal on the cost's trigger".
            A("vital_spark", "Vital Spark", "slow_knit", Hide, Ring,
              $"Under {Pct(T.VitalSparkBelow)} health, kills heal {Pct(T.VitalSparkKillHeal)}.",
              conditional: true);
            A("patina", "Patina", "rust", Hide, Stroke, $"Worn armour costs you {Pct(1f - T.PatinaPenaltyShare)} less.",
              (m, n) => m.ArmourWearPenaltyMul *= Mathf.Max(0.1f, 1f - (1f - T.PatinaPenaltyShare) * m.PotencyOf("patina")));
            // Reworked 2026-10-20: it was a heal per swing - Blood Price read backwards.
            A("pelican", "Pelican", "blood_price", Hide, Drop,
              "Your healing also hurts the nearest enemy.", conditional: true);
            A("viriditas", "Viriditas", "withering", Hide, Dot3,
              $"+{Pct(T.ViriditasPerFloor * PerFloor)} max health each floor cleared, to +{Pct(T.ViriditasCap)}.",
              conditional: true);
            // Reworked 2026-10-20: it was a heal at the clear - Toll read backwards. Now the deal.
            A("tribute", "Tribute", "toll", Ledger, Dot2, $"A refusal back every {T.TributeFloorsPerRefusal} floors.",
              conditional: true);
            A("maturation", "Maturation", "souring", Edge, Stroke,
              $"+{N(T.MaturationDamage)} Damage every {Sec(T.SouringEverySeconds)} on a floor, to +{N(T.MaturationCap)}.",
              conditional: true);
            // Reworked 2026-10-20: it was a heal on release - Backfire read backwards.
            A("rebound", "Rebound", "backfire", Azoth, Cross, "A release dashes you away from enemies.",
              conditional: true);
            A("lightfoot", "Lightfoot", "anchored", Quick, Bar,
              $"+{N(T.LightfootMove)} Move Speed. Double Graze at full speed.",
              (m, n) => m.Add(StatKind.MoveSpeed, T.LightfootMove * m.PotencyOf("lightfoot")), conditional: true);
            A("unshackled", "Unshackled", "encumbered", Quick, Dot3,
              $"A parry staggers enemies within {N(T.UnshackledRadius)} for {Sec(T.UnshackledStagger)}.",
              conditional: true);
            A("concentrate", "Concentrate", "stubborn_ore", Azoth, Dot3, $"+{N(T.ConcentratePower)} Elemental Power.",
              (m, n) => m.Add(StatKind.ElementalEffectiveness, T.ConcentratePower * m.PotencyOf("concentrate")));
            A("sealed_vessel", "Sealed Vessel", "leaky_vessel", Azoth, Drop,
              $"Meter fades half as fast. Hits taken add {Pct(T.SealedVesselGain)}.",
              (m, n) => m.DecayMul *= Mathf.Max(0.1f, 1f - (1f - T.SealedVesselDecay) * m.PotencyOf("sealed_vessel")), conditional: true);
            A("golden_chain", "Golden Chain", "long_chain", Anvil, Dot2,
              $"Every {T.GoldenChainEvery}rd art needs no basics.", conditional: true);
            A("lucid", "Lucid", "fog", Ledger, Ring, "Enemies winding up on you are outlined.", conditional: true);
            A("basilisk", "Basilisk", "wandering_eye", Ledger, Dot2, "Auto-target picks the weakest enemy.",
              (m, n) => m.Targeting = TargetMode.Weakest);
            A("ossuary", "Ossuary", "dead_weight", Hide, Cross, "Corpses block enemies instead of you.", conditional: true);
            A("antipathy", "Antipathy", "retrograde", Quick, Stroke,
              $"Every {Sec(T.AntipathyEvery)}, enemies near you stagger for {Sec(T.AntipathySeconds)}.", conditional: true);
            A("ley_lines", "Ley Lines", "projection", Azoth, Bar,
              $"Every {Sec(T.LeyLinesEvery)}, burning lines cross the nearest enemies.", conditional: true);
            A("phlogiston", "Phlogiston", "smother", Azoth, Dot1, "Heat stacks give double Damage.", conditional: true);
            A("flood", "Flood", "low_water", Azoth, Dot2, $"Soaked enemies take {Pct(T.FloodVulnerability)} more.",
              conditional: true);
            A("mountain", "Mountain", "restless", Azoth, Dot3, $"While planted, +{N(T.MountainDamage)} Damage.",
              conditional: true);
            A("gale", "Gale", "becalmed", Azoth, Stroke, "Crit floor climbs twice as fast.", conditional: true);

            // ============================================================ UNIONS (Phase 3)
            // Granted, never drawn: two opposite humours each held three boons deep, level within
            // one (RunModifiers.CheckUnions). Kept for the run once formed.
            Union("steam", "Steam", ExchangeFamily.Choler, Drop,
                  $"Burn meets soak: a burst of {N(T.SteamHitUnits)} hits within {N(T.SteamRadius)}.");
            Union("clot", "Clot", ExchangeFamily.Sanguine, Bar,
                  $"Staggering a bleeding enemy deals {Sec(T.ClotSeconds)} of its bleed at once.");

            // ============================================================ COMBINATIONS

            // Conjunctions: boon + boon.
            Combo(EntryOrigin.Conjunction, "oracle", "Oracle", "scrying_glass", "transmuters_eye", Ledger, Ring,
                  "Each slate shows the deal after it.", conditional: true);
            Combo(EntryOrigin.Conjunction, "wellspring", "Wellspring", "overflow", "rich_vein", Azoth, Dot2,
                  T.WellspringEvery == 2 ? "Every second release fires twice." : $"Every {T.WellspringEvery}th release fires twice.",
                  conditional: true);
            Combo(EntryOrigin.Conjunction, "phoenix", "Phoenix", "second_wind", "thickened_hide", Hide, Stroke,
                  $"Second Wind leaves you at {Pct(T.PhoenixRise)} health and burns all around you.",
                  conditional: true);
            Combo(EntryOrigin.Conjunction, "gemini", "Gemini", "reiteration", "vein_finder", Edge, Ring,
                  "Repeats strike for the whole hit.", conditional: true);
            Combo(EntryOrigin.Conjunction, "damascene", "Damascene", "whetstone", "heavy_payoff", Anvil, Bar,
                  $"An art marks its target: +{N(T.DamasceneDamage)} Damage on your next {T.DamasceneHits} hits.",
                  conditional: true);
            Combo(EntryOrigin.Conjunction, "hunt", "Hunt", "eagle", "executioner", Edge, Drop,
                  $"After a kill, Executioner works on any health for {Sec(T.HuntSeconds)}.",
                  conditional: true);
            Combo(EntryOrigin.Conjunction, "athanor", "Athanor", "equipoise", "aegis_cycle", Hide, Ring,
                  "The ward returns twice as fast standing still.", conditional: true);
            Combo(EntryOrigin.Conjunction, "cataclysm", "Cataclysm", "elixir", "dilation", Azoth, Cross,
                  $"Releases stagger for {Sec(T.CataclysmStagger)}.", conditional: true);

            // Putrefactions: cost + cost.
            Combo(EntryOrigin.Putrefaction, "glass_bones", "Glass Bones", "thin_blood", "paper_guard", Brittle, Cross,
                  $"Under {Pct(T.GlassBonesBelow)} health, +{Pct(T.GlassBonesTaken)} damage taken.",
                  conditional: true);
            Combo(EntryOrigin.Putrefaction, "sol_niger", "Sol Niger", "fog", "wandering_eye", Blind, NoMark,
                  $"Enemies beyond {N(T.SolNigerBeyond)} are silhouettes.",
                  (m, n) => m.SilhouetteBeyond = T.SolNigerBeyond);
            Combo(EntryOrigin.Putrefaction, "haemophilia", "Haemophilia", "blood_price", "slow_knit", Tithe, Drop,
                  $"Hits taken also bleed you {Pct(T.HaemophiliaFraction)} of the hit over {Sec(T.HaemophiliaSeconds)}.",
                  conditional: true);
            Combo(EntryOrigin.Putrefaction, "senescence", "Senescence", "souring", "withering", Tithe, Stroke,
                  $"Enemies hit {Pct(T.SenescenceTaken * PerFloor)} harder each floor cleared.",
                  conditional: true);

            // Citrinitas: boon + cost - a boon that feeds on the cost, and fades if it is transmuted away.
            Combo(EntryOrigin.Citrinitas, "bloodstone", "Bloodstone", "blood_price", "bloodletters_pact", Hide, Drop,
                  $"+1 Damage per {Pct(T.BloodstoneMissingPerPoint)} health missing, to +{N(T.BloodstoneCap)}.",
                  conditional: true);
            Combo(EntryOrigin.Citrinitas, "ponderous", "Ponderous", "heavy_arms", "heavy_payoff", Anvil, Dot2,
                  "Attack speed lost to costs becomes Weapon Art.",
                  (m, n) => m.Add(StatKind.FinisherPower, -m.Down[(int)StatKind.AttackSpeed]));
            Combo(EntryOrigin.Citrinitas, "iron_rhythm", "Iron Rhythm", "fumbler", "reiteration", Edge, Dot1,
                  "A miss makes your next hit repeat.", conditional: true);
            Combo(EntryOrigin.Citrinitas, "slow_fire", "Slow Fire", "stubborn_ore", "elixir", Azoth, Dot3,
                  $"Releases get +{N(T.SlowFirePowerPerSecond)} Elemental Power per second the meter took to fill, " +
                  $"to +{N(T.SlowFireCap)}.", conditional: true);
            Combo(EntryOrigin.Citrinitas, "blindsight", "Blindsight", "fog", "vein_finder", Edge, Ring,
                  $"+{Pct(T.BlindsightCrit)} crit chance in Fog.",
                  (m, n) => { if (m.HideTargetHighlight) m.BonusCrit += T.BlindsightCrit; });
            Combo(EntryOrigin.Citrinitas, "retrograde_motion", "Retrograde Motion", "retrograde", "equipoise", Quick, Stroke,
                  $"While inverted, +{N(T.RetrogradeMotionMove)} Move Speed and take {Pct(1f - T.RetrogradeMotionTakenMul)} less.",
                  conditional: true);

            // RETIRED (the user's calls, 2026-10-21): out of the deal pool, kept so holders still load.
            foreach (var id in new[] { "fleetfoot", "evanescence", "stonestance" })
                if (_byId.TryGetValue(id, out var gone)) gone.Retired = true;

            // BRANDS: the costs that change a rule (Slay the Spire's boss relics' kind) - the brand
            // deal after a mini-boss pays with one.
            foreach (var id in new[] { "fog", "retrograde", "rooted", "dead_weight", "projection", "induration", "fumbler" })
                if (_byId.TryGetValue(id, out var b)) b.Brand = true;
            foreach (var kv in Axes)
                if (_byId.TryGetValue(kv.Key, out var e)) e.Axis = kv.Value;
                else Debug.LogError($"[Exchange] axis names '{kv.Key}', which does not exist");
            // Schools: the table for base entries; an Albedo takes its cost's, a combination its
            // first part's, a union its own family's humour.
            foreach (var kv in Schools)
                if (_byId.TryGetValue(kv.Key, out var e)) e.School = kv.Value;
                else Debug.LogError($"[Exchange] school names '{kv.Key}', which does not exist");
            foreach (var e in _all)
            {
                if (e.School != School.None) continue;
                if (e.Origin == EntryOrigin.Albedo)
                    foreach (var c in _all) { if (c.AlbedoId == e.Id) { e.School = c.School; break; } }
                if (e.School == School.None && e.Parts != null && _byId.TryGetValue(e.Parts[0], out var p)) e.School = p.School;
                if (e.School == School.None && e.Origin == EntryOrigin.Union)
                    e.School = e.Family == ExchangeFamily.Choler ? School.Choler : School.Sanguine;
                if (e.School == School.None) Debug.LogWarning($"[Exchange] '{e.Id}' has no school");
            }
            Lint();
        }

        /// <summary>Every base entry's school (Phase 3) - by theme: Choler heat and haste, Phlegm water,
        /// guard and the meter, Sanguine blood and the crit, Melancholy weight, stillness and armour.</summary>
        static readonly Dictionary<string, School> Schools = new()
        {
                ["whetstone"] = School.Choler,
                ["quickening"] = School.Choler,
                ["vein_finder"] = School.Sanguine,
                ["executioner"] = School.Sanguine,
                ["first_blood"] = School.Sanguine,
                ["reiteration"] = School.Choler,
                ["long_reach"] = School.Melancholy,
                ["wide_arc"] = School.Melancholy,
                ["heavy_payoff"] = School.Melancholy,
                ["ouroboros"] = School.Sanguine,
                ["green_lion"] = School.Choler,
                ["short_chain"] = School.Choler,
                ["extra_sigil"] = School.Ledger,
                ["thickened_hide"] = School.Melancholy,
                ["bloodletters_pact"] = School.Sanguine,
                ["stonestance"] = School.Melancholy,
                ["reactive_plate"] = School.Phlegm,
                ["aegis_cycle"] = School.Phlegm,
                ["second_wind"] = School.Sanguine,
                ["fleetfoot"] = School.Choler,
                ["eagle"] = School.Choler,
                ["evanescence"] = School.Phlegm,
                ["equipoise"] = School.Phlegm,
                ["ghostwalk"] = School.Phlegm,
                ["attunement"] = School.Phlegm,
                ["rich_vein"] = School.Phlegm,
                ["elixir"] = School.Choler,
                ["dilation"] = School.Phlegm,
                ["residue"] = School.Melancholy,
                ["overflow"] = School.Phlegm,
                ["twin_spark"] = School.Choler,
                ["curator"] = School.Ledger,
                ["transmuters_eye"] = School.Ledger,
                ["scrying_glass"] = School.Ledger,
                ["lodestone"] = School.Ledger,
                ["prima_materia"] = School.Ledger,
                ["salamander"] = School.Choler,
                ["gnome"] = School.Melancholy,
                ["undine"] = School.Phlegm,
                ["sylph"] = School.Choler,
                ["banked_embers"] = School.Choler,
                ["high_tide"] = School.Phlegm,
                ["deep_roots"] = School.Melancholy,
                ["tailwind"] = School.Choler,
                ["fletching"] = School.Sanguine,
                ["ricochet"] = School.Phlegm,
                ["tinder"] = School.Choler,
                ["backdraft"] = School.Choler,
                ["tidewater"] = School.Phlegm,
                ["saturate"] = School.Phlegm,
                ["serrated"] = School.Sanguine,
                ["lancet"] = School.Sanguine,
                ["knell"] = School.Melancholy,
                ["pin_down"] = School.Melancholy,
                ["aftershock"] = School.Melancholy,
                ["shockwave"] = School.Choler,
                ["riptide"] = School.Phlegm,
                ["fault_line"] = School.Melancholy,
                ["splinter"] = School.Sanguine,
                ["thornfield"] = School.Phlegm,
                ["pyre"] = School.Choler,
                ["lone_cut"] = School.Sanguine,
                ["spindrift"] = School.Phlegm,
                ["lodestar"] = School.Phlegm,
                ["snapback"] = School.Choler,
                ["patient_string"] = School.Sanguine,
                ["long_ember"] = School.Choler,
                ["drawdown"] = School.Phlegm,
                ["afterburn"] = School.Choler,
                ["lacerate"] = School.Sanguine,
                ["dulled"] = School.Phlegm,
                ["heavy_arms"] = School.Melancholy,
                ["cold_iron"] = School.Sanguine,
                ["fumbler"] = School.Phlegm,
                ["overcommitted"] = School.Melancholy,
                ["dross"] = School.Choler,
                ["induration"] = School.Melancholy,
                ["short_arm"] = School.Phlegm,
                ["thin_blood"] = School.Sanguine,
                ["paper_guard"] = School.Sanguine,
                ["slow_knit"] = School.Phlegm,
                ["rust"] = School.Melancholy,
                ["open_stance"] = School.Sanguine,
                ["blood_price"] = School.Sanguine,
                ["withering"] = School.Melancholy,
                ["toll"] = School.Sanguine,
                ["souring"] = School.Choler,
                ["backfire"] = School.Choler,
                ["caput_mortuum"] = School.Ledger,
                ["indenture"] = School.Ledger,
                ["debt"] = School.Ledger,
                ["anchored"] = School.Melancholy,
                ["encumbered"] = School.Melancholy,
                ["rooted"] = School.Melancholy,
                ["stubborn_ore"] = School.Phlegm,
                ["leaky_vessel"] = School.Phlegm,
                ["long_chain"] = School.Phlegm,
                ["fog"] = School.Phlegm,
                ["wandering_eye"] = School.Sanguine,
                ["dead_weight"] = School.Melancholy,
                ["retrograde"] = School.Choler,
                ["projection"] = School.Choler,
                ["smother"] = School.Phlegm,
                ["low_water"] = School.Choler,
                ["restless"] = School.Choler,
                ["becalmed"] = School.Melancholy,
                ["heavy_hand"] = School.Melancholy,
                ["dulled_script"] = School.Phlegm,
                ["brittle_script"] = School.Choler,
                ["unbloodied"] = School.Melancholy,
                ["nova"] = School.Choler,
                ["maelstrom"] = School.Phlegm,
                ["hemorrhage"] = School.Sanguine,
                ["earthquake"] = School.Melancholy,
                ["kindling"] = School.Choler,
                ["swell"] = School.Phlegm,
                ["blood_debt"] = School.Sanguine,
                ["sealed_sands"] = School.Choler,
        };

        /// <summary>The mirrored pairs' axes (ExchangeEntry.Axis): a boon and a cost on one axis never
        /// share a slate. Listed here rather than on each line so the pairs read side by side.</summary>
        static readonly Dictionary<string, string> Axes = new()
        {
                ["whetstone"] = "damage",
                ["dulled"] = "damage",
                ["quickening"] = "attack_speed",
                ["heavy_arms"] = "attack_speed",
                ["heavy_payoff"] = "weapon_art",
                ["dross"] = "weapon_art",
                ["long_reach"] = "range",
                ["short_arm"] = "range",
                ["thickened_hide"] = "max_health",
                ["thin_blood"] = "max_health",
                ["attunement"] = "meter",
                ["leaky_vessel"] = "meter",
                ["short_chain"] = "chain",
                ["long_chain"] = "chain",
                ["bloodletters_pact"] = "healing",
                ["slow_knit"] = "healing",
                ["blood_debt"] = "healing",   // lifesteal is worthless while the oath holds healing off
                ["prima_materia"] = "deal_size",
                ["caput_mortuum"] = "deal_size",
                ["fleetfoot"] = "move_speed",
                ["equipoise"] = "move_speed",
                ["anchored"] = "move_speed",
                ["rich_vein"] = "element_growth",
                ["stubborn_ore"] = "element_growth",
                ["vein_finder"] = "crit",
                ["cold_iron"] = "crit",
        };

        /// <summary>
        /// THE LINT (2026-10-20): no boon may switch a system OFF - set a multiplier that starts above
        /// zero to zero, or turn on a flag that disables something. Kiln-Fired ("armour never wears")
        /// and Sealed Vessel ("the meter never fades") both did, and nothing caught them. Only an
        /// entry's Apply (its Mods) can be checked; a RunEffects behaviour cannot, so a new
        /// conditional boon must be checked by hand. Costs may do either.
        /// </summary>
        static void Lint()
        {
            var fields = typeof(Mods).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var e in _all)
            {
                if (e.Kind != ExchangeKind.Boon || e.Apply == null) continue;
                var bare = new Mods();
                var held = new Mods();
                e.Apply(held, e.MaxStacks);
                foreach (var f in fields)
                {
                    if (f.FieldType == typeof(float))
                    {
                        float a = (float)f.GetValue(bare), b = (float)f.GetValue(held);
                        if (a > 0f && b <= 0f)
                            Debug.LogError($"[Exchange] boon '{e.Id}' sets {f.Name} to {b} - a boon may not switch a system off");
                    }
                    else if (f.FieldType == typeof(bool) && !(bool)f.GetValue(bare) && (bool)f.GetValue(held))
                        Debug.LogError($"[Exchange] boon '{e.Id}' turns on {f.Name} - a boon may not switch a system off");
                }
            }
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

        static ExchangeEntry Inscribed(this ExchangeEntry e, FormSlot slot)
        {
            e.Slot = slot;
            return e;
        }

        static ExchangeEntry Sworn(this ExchangeEntry e) { e.Oath = true; return e; }
        static ExchangeEntry Branded(this ExchangeEntry e) { e.Brand = true; return e; }
        static ExchangeEntry InShortOnly(this ExchangeEntry e) { e.ShortOnly = true; return e; }

        static ExchangeEntry OnSocket(this ExchangeEntry e)
        {
            e.SocketCost = true;
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
