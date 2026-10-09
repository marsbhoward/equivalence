using System;
using System.Collections.Generic;
using Convergence.Core;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// What a piece of gear rolls when it is minted: a small universal Armor+MaxHp floor (armour
    /// only), a PRIMARY and its SUB-STATS, every one of them drawn from the slot's ONE pool (see
    /// <see cref="Pools"/>). Rolled once per item id and fixed forever - not re-rolled on equip -
    /// so the seed is derived from identity rather than from live RNG state.
    ///
    /// Setters are keyed by StatKind rather than inlined per slot: a StatKind is now a saved
    /// value (MintedGearRecord.PrimaryStat, for the Forge's combine/upgrade paths), so there has
    /// to be exactly one place mapping it to the StatPercents field it means, or the two could
    /// drift. Adding a stat is still one line here plus one line in the pool it belongs to.
    /// </summary>
    public static class GearRoller
    {
        public delegate void StatSetter(StatPercents stats, float points);

        static readonly Dictionary<StatKind, StatSetter> Setters = new()
        {
            { StatKind.Damage, (s, p) => s.Damage += p },
            { StatKind.AttackSpeed, (s, p) => s.AttackSpeed += p },
            { StatKind.MoveSpeed, (s, p) => s.MoveSpeed += p },
            { StatKind.Range, (s, p) => s.Range += p },
            { StatKind.MaxHp, (s, p) => s.MaxHp += p },
            { StatKind.Armor, (s, p) => s.Armor += p },
            { StatKind.DamageResistance, (s, p) => s.DamageResistance += p },
            { StatKind.Resilience, (s, p) => s.Resilience += p },
            { StatKind.ElementGrowth, (s, p) => s.ElementGrowth += p },
            { StatKind.AbilityCooldownReduction, (s, p) => s.AbilityCooldownReduction += p },
            { StatKind.FinisherPower, (s, p) => s.FinisherPower += p },
            { StatKind.FinisherKnockback, (s, p) => s.FinisherKnockback += p },
            { StatKind.CritChance, (s, p) => s.CritChance += p },
            { StatKind.CritDamage, (s, p) => s.CritDamage += p },
            { StatKind.Graze, (s, p) => s.Graze += p },
            { StatKind.Brace, (s, p) => s.Brace += p },
            { StatKind.Cleave, (s, p) => s.Cleave += p },
            { StatKind.ElementalEffectiveness, (s, p) => s.ElementalEffectiveness += p },
            { StatKind.ComboTime, (s, p) => s.ComboTime += p },
            { StatKind.AoeRadius, (s, p) => s.AoeRadius += p },
            { StatKind.HealReceived, (s, p) => s.HealReceived += p },
            { StatKind.RepairReceived, (s, p) => s.RepairReceived += p },
            { StatKind.Mend, (s, p) => s.Mend += p },
            { StatKind.Splash, (s, p) => s.Splash += p },
            { StatKind.Pierce, (s, p) => s.Pierce += p },
            { StatKind.Accuracy, (s, p) => s.Accuracy += p },
        };

        /// <summary>Adds <paramref name="points"/> of <paramref name="kind"/> to a stat block - the
        /// one StatKind-to-field mapping, for callers outside the roller (the balance model, the
        /// mastery board).</summary>
        public static void Add(StatPercents stats, StatKind kind, float points)
        {
            if (stats != null && Setters.TryGetValue(kind, out var setter)) setter(stats, points);
        }

        /// <summary>The points a stat block holds of one kind - read through the same mapping, by
        /// adding to a scratch block, so the two directions cannot disagree.</summary>
        public static float PointsOf(StatPercents stats, StatKind kind)
        {
            if (stats == null || !Setters.TryGetValue(kind, out var setter)) return 0f;
            // Probe which field the kind writes: set 1 on an empty block, then read it back off the
            // same field of the given block by matching the probe.
            var probe = new StatPercents();
            setter(probe, 1f);
            return Dot(stats, probe);
        }

        static float Dot(StatPercents a, StatPercents unit)
            => a.Damage * unit.Damage + a.AttackSpeed * unit.AttackSpeed + a.MoveSpeed * unit.MoveSpeed
             + a.Range * unit.Range + a.MaxHp * unit.MaxHp + a.Armor * unit.Armor
             + a.DamageResistance * unit.DamageResistance + a.Resilience * unit.Resilience
             + a.ElementGrowth * unit.ElementGrowth + a.AbilityCooldownReduction * unit.AbilityCooldownReduction
             + a.FinisherPower * unit.FinisherPower + a.FinisherKnockback * unit.FinisherKnockback
             + a.CritChance * unit.CritChance + a.CritDamage * unit.CritDamage + a.Graze * unit.Graze
             + a.Brace * unit.Brace + a.Cleave * unit.Cleave + a.ElementalEffectiveness * unit.ElementalEffectiveness
             + a.ComboTime * unit.ComboTime + a.AoeRadius * unit.AoeRadius + a.HealReceived * unit.HealReceived
             + a.RepairReceived * unit.RepairReceived + a.Mend * unit.Mend + a.Splash * unit.Splash
             + a.Pierce * unit.Pierce + a.Accuracy * unit.Accuracy;

        /// <summary>
        /// ONE POOL PER SLOT, and every stat in it can be the PRIMARY or a SUB-STAT (the user's call,
        /// 2026-10-05 - a slot used to keep a short primary pool beside a longer sub pool, and the
        /// utility stats could never lead a piece). Every rollable stat sits on at least one slot,
        /// on the slots whose THEME it fits:
        ///
        ///     Weapon     the strike: damage, speed, crit damage, accuracy, its own weapon art,
        ///                range, the element channelled through it, and the crowd stats only a
        ///                weapon carries (splash; cleave on blades and discs, pierce on bows)
        ///     Head       the mind and the eye: the element, aim, weak points, reflexes
        ///     Neck       the spark: the element, raw power, its reach, vitality
        ///     Ring       power worn: damage, speed and crit, the element
        ///     Gloves     handling: speed, crit, accuracy, reach, the chain's rhythm
        ///     Shoulders  weight: the weapon art's force, bracing, the armour that takes it
        ///     Torso      the core: health, mitigation, the armour pool and its upkeep
        ///     Back       flow: movement, grazing blows, area, the cooldown, the element's tide
        ///     Legs       stance: health, movement, both halves of footwork, mitigation
        ///     Boots      footwork: movement, grazing, the dash's cooldown, wear
        ///     Belt       endurance and kit: mitigation, health, rhythm, power from the hips,
        ///                the potion and the tool pouch
        ///
        /// Each build finds something on almost every slot - Striker and Bulwark on all eleven,
        /// Tempo on ten, the Elementalist on nine - and the core stats sit on two to four slots
        /// each, so a build spreads across its stats instead of stacking one.
        ///
        /// A bigger pool means two pieces share a primary less often, so combining (which matches
        /// the primary) is slower than it was with two-stat primary pools.
        ///
        /// RELIC has no entry: its roll is its weapon art (see RollFinisher). TRINKET has none
        /// either - it retired into Relic and no item declares it.
        /// </summary>
        static readonly Dictionary<GearSlot, StatKind[]> Pools = new()
        {
            { GearSlot.Weapon,    new[] { StatKind.Damage, StatKind.AttackSpeed, StatKind.CritDamage,
                                          StatKind.Accuracy, StatKind.FinisherPower, StatKind.Range,
                                          StatKind.ElementalEffectiveness, StatKind.Splash,
                                          StatKind.Cleave, StatKind.Pierce } },
            { GearSlot.Head,      new[] { StatKind.ElementalEffectiveness, StatKind.ElementGrowth,
                                          StatKind.Accuracy, StatKind.CritChance,
                                          StatKind.AbilityCooldownReduction } },
            { GearSlot.Neck,      new[] { StatKind.Damage, StatKind.ElementalEffectiveness,
                                          StatKind.ElementGrowth, StatKind.AoeRadius, StatKind.HealReceived } },
            { GearSlot.Ring,      new[] { StatKind.Damage, StatKind.AttackSpeed, StatKind.CritChance,
                                          StatKind.CritDamage, StatKind.ElementalEffectiveness } },
            { GearSlot.Gloves,    new[] { StatKind.AttackSpeed, StatKind.CritChance, StatKind.CritDamage,
                                          StatKind.Accuracy, StatKind.Range, StatKind.ComboTime } },
            { GearSlot.Shoulders, new[] { StatKind.FinisherPower, StatKind.FinisherKnockback, StatKind.Brace,
                                          StatKind.Armor, StatKind.DamageResistance } },
            { GearSlot.Torso,     new[] { StatKind.MaxHp, StatKind.Resilience, StatKind.Brace,
                                          StatKind.Armor, StatKind.Mend } },
            { GearSlot.Back,      new[] { StatKind.MoveSpeed, StatKind.Graze, StatKind.AoeRadius,
                                          StatKind.AbilityCooldownReduction, StatKind.ElementGrowth } },
            { GearSlot.Legs,      new[] { StatKind.MaxHp, StatKind.MoveSpeed, StatKind.Graze,
                                          StatKind.Brace, StatKind.Resilience } },
            { GearSlot.Boots,     new[] { StatKind.MoveSpeed, StatKind.Graze, StatKind.AbilityCooldownReduction,
                                          StatKind.DamageResistance, StatKind.Mend } },
            { GearSlot.Belt,      new[] { StatKind.Resilience, StatKind.MaxHp, StatKind.ComboTime,
                                          StatKind.FinisherKnockback, StatKind.HealReceived,
                                          StatKind.RepairReceived } },
        };

        /// <summary>
        /// Whether a stat can do anything on this slot for this weapon class - the pools are per
        /// SLOT, but two weapon stats only mean something to one kind of weapon: Pierce is a bow's
        /// (an arrow passing through), Cleave is everything else's (a blade through a crowd, a
        /// disc's ricochets). Rolling either on the wrong class would mint a dead stat.
        /// </summary>
        public static bool FitsClass(StatKind kind, GearSlot slot, WeaponClass weapon)
        {
            if (slot != GearSlot.Weapon) return true;
            return kind switch
            {
                StatKind.Pierce => weapon == WeaponClass.Bow,
                StatKind.Cleave => weapon != WeaponClass.Bow,
                _ => true,
            };
        }

        static StatKind[] PoolFor(GearSlot slot, WeaponClass weapon)
            => Pools.TryGetValue(slot, out var pool)
                ? Array.FindAll(pool, k => FitsClass(k, slot, weapon))
                : Array.Empty<StatKind>();

        /// <summary>
        /// What a slot can roll for this weapon class, primary and sub-stats alike - a copy, read
        /// by the balance model (Balance.Assay), which builds its "targeted" pieces from the real
        /// pools so that a pool change moves the model with it rather than leaving a second list
        /// to drift.
        /// </summary>
        public static StatKind[] Pool(GearSlot slot, WeaponClass weapon = WeaponClass.Greatsword)
            => PoolFor(slot, weapon);

        /// <summary>Every slot whose pool holds <paramref name="kind"/> (any weapon class), in
        /// GearSlot order - for reports and the stat's own description.</summary>
        public static List<GearSlot> SlotsOf(StatKind kind)
        {
            var slots = new List<GearSlot>();
            foreach (var slot in GearSlots.All)
                if (Pools.TryGetValue(slot, out var pool) && Array.IndexOf(pool, kind) >= 0) slots.Add(slot);
            return slots;
        }

        /// <summary>
        /// Stats do not share units - +12 points of crit CHANCE is a lot, +12 points on the crit
        /// multiplier is barely felt - so each kind's magnitudes are the tier's scaled by this.
        /// Applies to the primary and the sub-stat range alike, so a primary still beats a
        /// sub-stat of its own kind. Set 2026-10-05 (the rebalance's gear tables), by four rules:
        ///
        /// KNEE STATS - one fully targeted Gold three-star piece (primary and three sub-stats of
        /// the stat, at mid range) fills about HALF the stat's character knee (Tuning.Stats), so
        /// every stat takes about the same investment to saturate and a build spreads across
        /// several instead of piling into one. Scale = knee / Damage's knee.
        ///
        /// THE CRIT FAMILY (crit chance, crit damage, accuracy) - one roll is worth what a roll of
        /// Damage is, on the Max player (Balance.Assay.GearWorth) - the user's rule that crit is
        /// never mandatory: trading a little chance for more damage costs nothing.
        ///
        /// MITIGATION - Resilience at Max HP's scale (a point of either adds the same effective
        /// health); Graze and Brace, which each work about half the time, at twice that.
        ///
        /// EVERYTHING ELSE keeps its 2026-09-25 placeholder, except the weapon-only crowd stats,
        /// whose scale has the weapon's bigger primary (Tuning.GearRoll.WeaponPrimaryMul) folded out
        /// so a weapon's primary of them is what it was.
        ///
        /// CHANGING A NUMBER HERE RE-VALUES EVERY MINTED ITEM. A primary is rebuilt from this, but a
        /// sub-stat's roll is stored - bump <see cref="TablesVersion"/> and give the old table a case
        /// in <see cref="SubStatRangeAt"/>, so GearForge.Migrate can carry each stored roll to the
        /// same place in its new range.
        /// </summary>
        public static float KindScale(StatKind kind) => kind switch
        {
            // ---- knee stats: knee / Damage's knee of 80 ----
            StatKind.Damage => 1f,                      // 80
            StatKind.AttackSpeed => 0.625f,             // 50
            StatKind.MoveSpeed => 0.25f,                // 20
            StatKind.Range => 0.3125f,                  // 25
            StatKind.AoeRadius => 0.5f,                 // 40
            StatKind.FinisherPower => 0.75f,            // 60
            StatKind.ElementGrowth => 0.625f,           // 50
            StatKind.ElementalEffectiveness => 1f,      // 80
            StatKind.MaxHp => 0.75f,                    // 60

            // ---- the crit family: a roll worth a Damage roll on the Max player ----
            StatKind.CritChance => 0.45f,
            StatKind.CritDamage => 1.9f,
            StatKind.Accuracy => 1.75f,

            // ---- mitigation ----
            StatKind.Resilience => 0.75f,
            StatKind.Graze => 1.5f,
            StatKind.Brace => 1.5f,

            // ---- the weapon's own crowd stats, sized as weapon primaries ----
            StatKind.Cleave => 3f / Tuning.GearRoll.WeaponPrimaryMul,
            StatKind.Splash => 2f / Tuning.GearRoll.WeaponPrimaryMul,
            StatKind.Pierce => 3f / Tuning.GearRoll.WeaponPrimaryMul,

            // ---- utility, unchanged ----
            StatKind.FinisherKnockback => 3f,
            StatKind.ComboTime => 3f,
            StatKind.HealReceived => 3f,
            StatKind.RepairReceived => 3f,
            StatKind.Mend => 0.5f,
            _ => 1f,   // Armor, DamageResistance (Sturdiness), AbilityCooldownReduction
        };

        /// <summary>
        /// Which gear tables a sub-stat's stored roll was made against (MintedGearRecord.StatsVersion).
        ///
        ///     0   the 2026-09-25 placeholders: two pools a slot, every stat x1 but the kinds below
        ///     1   2026-10-05, the rebalance: one pool a slot, scales re-based (KindScale)
        /// </summary>
        public const int TablesVersion = 1;

        /// <summary>The 2026-09-25 placeholder scales, frozen - only GearForge.Migrate reads them,
        /// to find where an old roll sat in its old range.</summary>
        static float KindScaleV0(StatKind kind) => kind switch
        {
            StatKind.CritChance => 0.5f,
            StatKind.Mend => 0.5f,
            StatKind.CritDamage => 3f,
            StatKind.FinisherKnockback => 3f,
            StatKind.Cleave => 3f,
            StatKind.ComboTime => 3f,
            StatKind.HealReceived => 3f,
            StatKind.RepairReceived => 3f,
            StatKind.Splash => 2f,
            StatKind.Pierce => 3f,
            StatKind.Accuracy => 5f,
            _ => 1f,
        };

        /// <summary>Highest star level. Base is its own level, so 0..3 is FOUR levels: base, one,
        /// two and three stars. Two three-star pieces combine into the next tier at base.</summary>
        public const int MaxLevel = 3;

        /// <summary>
        /// Diamond and Black Diamond roll nothing - both are cosmetic tiers. Black Diamond's power
        /// lives on its RELIC's signature finisher, never on stats.
        /// </summary>
        static float TierScale(LootTier tier, float silver, float gold) => tier switch
        {
            LootTier.Bronze => silver * Tuning.GearRoll.BronzeFraction,
            LootTier.Silver => silver,
            LootTier.Gold => gold,
            _ => 0f,
        };

        /// <summary>One roll's unit for a kind at a tier - an armour piece's primary, before star
        /// scaling. Sub-stat ranges are fractions of it on every slot.</summary>
        static float RollPoints(StatKind kind, LootTier tier)
            => TierScale(tier, Tuning.GearRoll.PoolStatSilver, Tuning.GearRoll.PoolStatGold) * KindScale(kind);

        /// <summary>
        /// The primary stat's FIXED value at a tier, before star scaling. Primaries never roll a
        /// range - only sub-stats do. A WEAPON's is <see cref="Tuning.GearRoll.WeaponPrimaryMul"/>
        /// bigger: it is the one slot every build fills and the one without the armour floor.
        /// </summary>
        public static float PrimaryPoints(StatKind kind, LootTier tier, GearSlot slot)
            => RollPoints(kind, tier) * (slot == GearSlot.Weapon ? Tuning.GearRoll.WeaponPrimaryMul : 1f);

        /// <summary>Sub-stat slots per tier: Bronze 1, Silver 2, Gold 3. The cosmetic tiers and
        /// relics carry none.</summary>
        public static int SubStatCount(LootTier tier) => tier switch
        {
            LootTier.Bronze => 1,
            LootTier.Silver => 2,
            LootTier.Gold => 3,
            _ => 0,
        };

        /// <summary>
        /// The range a sub-stat rolls in at a tier, before star scaling. A fraction of the
        /// tier's roll unit (an armour primary) with the top of the range BELOW 1, which is what
        /// guarantees a primary always beats a sub-stat of its own kind - on a weapon by more.
        ///
        /// Scaled per kind by KindScale, the same factor the primary uses. The same on every slot.
        /// </summary>
        public static (float Min, float Max) SubStatRange(StatKind kind, LootTier tier)
        {
            float p = RollPoints(kind, tier);
            return (Round1(p * Tuning.GearRoll.SubStatMinFraction),
                    Round1(p * Tuning.GearRoll.SubStatMaxFraction));
        }

        /// <summary>
        /// The range a sub-stat rolled in under gear tables <paramref name="version"/> - the current
        /// tables' <see cref="SubStatRange"/>, or an old table frozen as it was, for
        /// GearForge.Migrate. Version 0's tier numbers are written out (Silver 6, Gold 12, Bronze half
        /// of Silver, 25-60%) so a later change to Tuning.GearRoll cannot move the old table.
        /// </summary>
        public static (float Min, float Max) SubStatRangeAt(int version, StatKind kind, LootTier tier)
        {
            if (version >= TablesVersion) return SubStatRange(kind, tier);
            float unit = tier switch
            {
                LootTier.Bronze => 6f * 0.5f,
                LootTier.Silver => 6f,
                LootTier.Gold => 12f,
                _ => 0f,
            } * KindScaleV0(kind);
            return (Round1(unit * 0.25f), Round1(unit * 0.6f));
        }

        /// <summary>A value drawn from <see cref="SubStatRange"/>, rounded to a tenth so the
        /// number printed is the number held.</summary>
        public static float RollSubValue(StatKind kind, LootTier tier, Random rng)
        {
            var (min, max) = SubStatRange(kind, tier);
            return Round1(min + (float)rng.NextDouble() * (max - min));
        }

        /// <summary>A fresh sub-stat: a kind from the slot's pool, and a value in its range. Kinds
        /// may repeat on one item, and may be the primary's own.</summary>
        public static SubStat RollSubStat(GearSlot slot, LootTier tier, Random rng,
                                          WeaponClass weapon = WeaponClass.Greatsword)
        {
            var pool = PoolFor(slot, weapon);
            if (pool.Length == 0) return null;
            var kind = pool[rng.Next(pool.Length)];
            return new SubStat(kind, RollSubValue(kind, tier, rng));
        }

        /// <summary>A new item's full set of sub-stats. Seeded off the item's identity like the
        /// primary roll, with its own offset so the two draws are not correlated.</summary>
        public static List<SubStat> RollSubStats(GearSlot slot, LootTier tier, int seed,
                                                 WeaponClass weapon = WeaponClass.Greatsword)
        {
            var result = new List<SubStat>();
            var rng = new Random(seed ^ 0x5B57A7);
            for (int i = 0; i < SubStatCount(tier); i++)
            {
                var sub = RollSubStat(slot, tier, rng, weapon);
                if (sub != null) result.Add(sub);
            }
            return result;
        }

        /// <summary>
        /// What an item actually grants: the tier's base (armour/HP floor + fixed primary), plus
        /// every sub-stat's roll, all multiplied by the star level. Stars scale EVERYTHING on the
        /// piece, sub-stats included.
        /// </summary>
        public static StatPercents BuildGrants(GearSlot slot, LootTier tier, StatKind primary, int level,
                                               IReadOnlyList<SubStat> subs)
        {
            var result = RollExact(slot, tier, primary);
            if (subs != null)
                foreach (var sub in subs)
                    if (sub != null && Setters.TryGetValue(sub.Kind, out var setter))
                        setter(result, sub.Value);
            return Rescale(result, UpgradeScale(level));
        }

        static float Round1(float v) => (float)Math.Round(v, 1);

        /// <summary>
        /// Builds stats for a KNOWN stat, no RNG involved - what the random Roll delegates to
        /// once it has picked one, and the base BuildGrants starts from, so rebuilding an item at a
        /// new tier or star level can never land on a different primary than the one it carries.
        /// </summary>
        public static StatPercents RollExact(GearSlot slot, LootTier tier, StatKind primary)
        {
            var result = new StatPercents();

            // Cosmetic only - see GearSlots.Kind's own note that a relic is carried, not worn,
            // and Loadout.TotalStats already skips it for the same reason.
            if (slot == GearSlot.Relic) return result;

            if (slot != GearSlot.Weapon)
            {
                result.Armor += TierScale(tier, Tuning.GearRoll.FloorArmorSilver, Tuning.GearRoll.FloorArmorGold);
                result.MaxHp += TierScale(tier, Tuning.GearRoll.FloorMaxHpSilver, Tuning.GearRoll.FloorMaxHpGold);
            }

            if (primary != StatKind.None && Setters.TryGetValue(primary, out var setter))
                setter(result, PrimaryPoints(primary, tier, slot));

            return result;
        }

        /// <summary>
        /// <paramref name="seed"/> should be derived from the item's own identity (its ItemId
        /// today; a mint transaction's hash once the Forge exists) rather than from live RNG
        /// state, so the same item always rolls the same way and the result can be reproduced
        /// and audited rather than merely remembered.
        /// </summary>
        public static (StatPercents Grants, StatKind Primary) Roll(GearSlot slot, LootTier tier, int seed,
                                                                   WeaponClass weapon = WeaponClass.Greatsword)
        {
            StatKind primary = StatKind.None;
            var pool = PoolFor(slot, weapon);
            if (pool.Length > 0)
            {
                var rng = new Random(seed);
                primary = pool[rng.Next(pool.Length)];
            }

            return (RollExact(slot, tier, primary), primary);
        }

        /// <summary>
        /// A relic's roll IS its finisher, the way a helm's roll is its stats.
        ///
        /// THIS IS THE RELIC SLOT'S WHOLE CONTENT. Relics carry no stats at all (see Roll's own
        /// early return), so without this a bronze or silver relic would be a slot that rolled
        /// nothing - which is what made the socket read as a black-diamond-only feature. Now the
        /// two axes are clean: the relic slot is where finishers come from, every other slot is
        /// where stats come from.
        ///
        /// BRONZE THROUGH GOLD ONLY, and never a signature. The pool is what the floor reward
        /// already draws from - MovesetLibrary.RandomExcluding, which skips defaults and skips
        /// anything flagged SignatureOnly - so a rolled relic can never hand out Conflagration,
        /// Echo, Reckoning, Bloodletting or Crosscut. Those stay exclusive to Black Diamond,
        /// which is hand-authored and does not come through here.
        ///
        /// The pick is uniform within the class. Tier gates ELIGIBILITY rather than weighting the
        /// draw, because a finisher has no magnitude to scale - you either have the move or you
        /// do not. Weighting rarer moves toward gold is the obvious next lever if the spread ever
        /// needs one; it is deliberately not guessed at now.
        ///
        /// Class-matched, because GearSlots.Accepts will refuse the relic beside a weapon of the
        /// wrong class anyway - rolling a disc finisher onto a greatsword relic would mint an
        /// item that can never be socketed with the sword it was drawn for.
        /// </summary>
        public static string RollFinisher(GearSlot slot, LootTier tier, WeaponClass weapon, int seed)
        {
            if (slot != GearSlot.Relic) return null;
            if (tier is not (LootTier.Bronze or LootTier.Silver or LootTier.Gold)) return null;

            // A separate seed offset from the stat draw's, so two rolls off one identity are not
            // correlated - the same reason RollDefensiveAbility takes its own.
            var pool = Combat.MovesetLibrary.RollablePool(weapon);
            if (pool.Count == 0) return null;

            var rng = new Random(seed ^ 0x2E11C);
            return pool[rng.Next(pool.Count)].Id;
        }

        /// <summary>
        /// The star level's multiplier on every stat of the piece - base, one, two, three stars at
        /// levels 0..3. Independent of LootTier on purpose: tier is RNG rarity, stars are power
        /// bought by combining two matching items, and conflating the two would mean a lucky roll
        /// and a grinded-out combine chain read as the same kind of progress.
        ///
        /// Three stars must stay BELOW the next tier's base (Bronze three-star 4.35 against Silver
        /// base 6 at today's numbers), or promoting two maxed pieces would hand back a weaker one.
        /// </summary>
        public static float UpgradeScale(int level) => level switch
        {
            1 => Tuning.GearRoll.OneStarScale,
            2 => Tuning.GearRoll.TwoStarScale,
            3 => Tuning.GearRoll.ThreeStarScale,
            _ => 1f,
        };

        /// <summary>Every field of <paramref name="grants"/> multiplied by <paramref name="factor"/> -
        /// what applying an UpgradeScale to a freshly-built StatPercents means.</summary>
        public static StatPercents Rescale(StatPercents grants, float factor)
        {
            var result = new StatPercents();
            result.Add(grants);
            result.Damage *= factor;
            result.AttackSpeed *= factor;
            result.MoveSpeed *= factor;
            result.Range *= factor;
            result.MaxHp *= factor;
            result.Armor *= factor;
            result.DamageResistance *= factor;
            result.Resilience *= factor;
            result.ElementGrowth *= factor;
            result.AbilityCooldownReduction *= factor;
            result.FinisherPower *= factor;
            result.FinisherKnockback *= factor;
            result.CritChance *= factor;
            result.CritDamage *= factor;
            result.Graze *= factor;
            result.Brace *= factor;
            result.Cleave *= factor;
            result.ElementalEffectiveness *= factor;
            result.ComboTime *= factor;
            result.AoeRadius *= factor;
            result.HealReceived *= factor;
            result.RepairReceived *= factor;
            result.Mend *= factor;
            result.Splash *= factor;
            result.Pierce *= factor;
            result.Accuracy *= factor;
            return result;
        }

        /// <summary>
        /// Rolls a complete item for one slot at a GIVEN tier (a box's tier is a guarantee; a
        /// floor drop's comes from depth): stats, and a defensive ability if the slot is Torso
        /// (DefensiveAbility.Dash otherwise, ignored by every non-Torso item the same way
        /// GearItem's own field already is), and a finisher if it is a Relic. One call so a
        /// caller never has to remember which of the rolls a given slot actually needs.
        ///
        /// Returns the pieces rather than a finished record on purpose - this file stays ignorant
        /// of Chain.MintedGearRecord, which depends on Art.Gear types today and would otherwise
        /// gain a dependency back the other way for no reason. Stars are applied by the caller
        /// (BuildGrants with a level) - this rolls base.
        /// </summary>
        public static (LootTier Tier, StatPercents Grants, StatKind Primary, List<SubStat> SubStats,
                       DefensiveAbility DefensiveAbility, string Finisher) RollItem(
            GearSlot slot, LootTier tier, int seed, WeaponClass weapon = WeaponClass.Greatsword)
        {
            var (_, primary) = Roll(slot, tier, seed, weapon);
            var subs = RollSubStats(slot, tier, seed, weapon);
            var grants = BuildGrants(slot, tier, primary, 0, subs);
            var ability = slot == GearSlot.Torso ? RollDefensiveAbility(seed) : DefensiveAbility.Dash;
            var finisher = RollFinisher(slot, tier, weapon, seed);
            return (tier, grants, primary, subs, ability, finisher);
        }

        static readonly DefensiveAbility[] DefensiveAbilities =
            (DefensiveAbility[])Enum.GetValues(typeof(DefensiveAbility));

        /// <summary>
        /// Uniform across all four, and NOT tier-scaled - access to the mechanic itself is never
        /// gated, only the numbers each one carries (via AbilityCooldownReduction and the item's
        /// own tier-derived Power, once that scaling exists) are. A separate seed offset from the
        /// stat roll's, so a torso item's ability and its stat pick vary independently rather
        /// than being correlated by sharing one draw.
        /// </summary>
        public static DefensiveAbility RollDefensiveAbility(int seed)
        {
            var rng = new Random(seed ^ 0x5A17E);
            return DefensiveAbilities[rng.Next(DefensiveAbilities.Length)];
        }
    }
}
