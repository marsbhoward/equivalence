using System;
using System.Collections.Generic;
using System.Linq;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.Chain
{
    /// <summary>
    /// The gear stake: one EQUIPPED stat-bearing piece put on the line for a run, in exchange for
    /// a guaranteed Forge partner.
    ///
    ///     stake a piece -> clear its gate floor -> extract -> receive a piece with the same MatchKey
    ///     die at any point (before or after the gate)       -> the staked piece is destroyed
    ///     extract before the gate                            -> the stake comes home, nothing more
    ///
    /// WHY IT RISKS STATTED GEAR AND NOTHING ELSE. Stat-bearing Bronze/Silver/Gold pieces do not
    /// trade, so losing one costs power and never money - putting a tradeable piece at risk would
    /// be wagering an asset with a price on the outcome of play. Cosmetic tiers carry no power, so
    /// staking them would risk nothing. <see cref="GearForge.Combinable"/> is exactly that set, and
    /// is what decides it, so the two rules cannot drift: a piece that could not use its payout at
    /// the Forge cannot be staked for one.
    ///
    /// THE PAYOUT IS A PARTNER, NOT A STAR. It matches the stake on every field MatchKey compares -
    /// slot, tier, star level, primary, plus class on a weapon and ability on a torso - and rolls
    /// its sub-stats fresh. The player still combines at the Forge, where the sub-stat outcome
    /// stays uncertain; what the stake removes is only the search for a match.
    ///
    /// THE PAYOUT NEVER RIDES THE RUN. It is not carried loot and never enters RunLoot or a Rift's
    /// capacity: it is a claim on the extraction itself, minted only when the run ends extracted
    /// with the gate cleared. Pushing on past the gate and dying loses both.
    ///
    /// Pure rules, like GearForge - the caller owns the profile write and the checkpoint.
    /// </summary>
    public static class GearStake
    {
        public enum Outcome { None, Returned, Paid, Lost }

        /// <summary>Whether a piece may be staked at all. See the class header.</summary>
        public static bool Stakeable(MintedGearRecord r) => GearForge.Combinable(r);

        /// <summary>The floor a stake at <paramref name="level"/> stars must clear before an
        /// extraction pays. See Tuning.Stake.</summary>
        public static int GateFloor(int level) => level switch
        {
            0 => Tuning.Stake.GateBase,
            1 => Tuning.Stake.GateOneStar,
            2 => Tuning.Stake.GateTwoStar,
            _ => Tuning.Stake.GatePromotion,
        };

        /// <summary>Every equipped piece the player could stake, in slot order.</summary>
        public static List<MintedGearRecord> Candidates(CharacterProfile profile)
        {
            var result = new List<MintedGearRecord>();
            if (profile?.Gear == null || profile.MintedGear == null) return result;
            foreach (var entry in profile.Gear.Equipped.OrderBy(e => e.Slot))
            {
                var record = profile.MintedGear.Find(r => r != null && r.InstanceId == entry.ItemId);
                if (Stakeable(record)) result.Add(record);
            }
            return result;
        }

        public static MintedGearRecord Staked(CharacterProfile profile)
            => profile == null || string.IsNullOrEmpty(profile.StakedInstanceId) ? null
             : profile.MintedGear.Find(r => r != null && r.InstanceId == profile.StakedInstanceId);

        /// <summary>
        /// Puts <paramref name="record"/> on the line. Written onto the profile BEFORE the
        /// run-start checkpoint so the datum carries it - a stake held only in memory could be
        /// dodged by closing the game in a losing fight (see <see cref="ForfeitStale"/>).
        /// </summary>
        public static bool Place(CharacterProfile profile, MintedGearRecord record)
        {
            if (profile == null || !Stakeable(record) || !profile.MintedGear.Contains(record)) return false;
            profile.StakedInstanceId = record.InstanceId;
            profile.StakeGateFloor = GateFloor(record.UpgradeLevel);
            return true;
        }

        /// <summary>
        /// Settles the run's stake. <paramref name="extracted"/> is whether the run ended on the
        /// player's own terms; <paramref name="gateCleared"/> whether the gate floor was cleared
        /// during it. Returns the payout on <see cref="Outcome.Paid"/>, already added to the
        /// profile. Always clears the stake.
        /// </summary>
        public static (Outcome Outcome, MintedGearRecord Stake, MintedGearRecord Payout) Resolve(
            CharacterProfile profile, bool extracted, bool gateCleared, Random rng)
        {
            var stake = Staked(profile);
            bool hadStake = profile != null && !string.IsNullOrEmpty(profile.StakedInstanceId);
            Clear(profile);
            if (!hadStake || stake == null) return (Outcome.None, null, null);

            if (!extracted)
            {
                Destroy(profile, stake);
                return (Outcome.Lost, stake, null);
            }
            if (!gateCleared) return (Outcome.Returned, stake, null);

            var payout = Partner(stake, rng);
            profile.MintedGear.Add(payout);
            return (Outcome.Paid, stake, payout);
        }

        /// <summary>
        /// A stake still on the profile at LOAD belongs to a run that never reached its run-end
        /// checkpoint - the game was closed or crashed mid-run. It is lost, as the run's carried
        /// loot already is.
        ///
        /// HARSH ON A CRASH, AND DELIBERATELY SO. Returning it instead would make quitting the
        /// game the free way out of every losing fight, which empties the stake of meaning. Called
        /// on load rather than at the next run start so the piece cannot be combined away at the
        /// Forge in between.
        /// </summary>
        public static MintedGearRecord ForfeitStale(CharacterProfile profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.StakedInstanceId)) return null;
            var (_, stake, _) = Resolve(profile, extracted: false, gateCleared: false, rng: null);
            return stake;
        }

        /// <summary>A piece matching <paramref name="stake"/> on every MatchKey field, with its
        /// sub-stats rolled fresh.</summary>
        public static MintedGearRecord Partner(MintedGearRecord stake, Random rng)
        {
            rng ??= new Random();
            var subs = new List<SubStat>();
            for (int i = 0; i < GearRoller.SubStatCount(stake.Tier); i++)
            {
                var sub = GearRoller.RollSubStat(stake.Slot, stake.Tier, rng, stake.Class);
                if (sub != null) subs.Add(sub);
            }

            var record = new MintedGearRecord
            {
                InstanceId = $"STAKE-{stake.Slot}-{Guid.NewGuid():N}",
                DisplayName = $"{stake.Tier} {stake.Slot}",
                Slot = stake.Slot,
                Tier = stake.Tier,
                PrimaryStat = stake.PrimaryStat,
                DefensiveAbility = stake.DefensiveAbility,
                Class = stake.Class,
                UpgradeLevel = stake.UpgradeLevel,
                SubStats = subs,
                StatsVersion = GearRoller.TablesVersion,
            };
            record.RebuildGrants();
            return record;
        }

        static void Clear(CharacterProfile profile)
        {
            if (profile == null) return;
            profile.StakedInstanceId = "";
            profile.StakeGateFloor = 0;
        }

        /// <summary>Removes a lost piece everywhere it is referenced - the list, the loadout
        /// (if still worn) and its wear entry.</summary>
        static void Destroy(CharacterProfile profile, MintedGearRecord stake)
        {
            profile.MintedGear.Remove(stake);
            foreach (var entry in profile.Gear.Equipped.ToList())
                if (entry.ItemId == stake.InstanceId) profile.Gear.Clear(entry.Slot);
            profile.Wear?.Entries?.RemoveAll(e => e.ItemId == stake.InstanceId);
        }
    }
}
