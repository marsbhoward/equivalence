using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.Rifts
{
    /// <summary>
    /// What a run is carrying, and what it has already got out.
    ///
    /// THE WHOLE STAKE OF THE EXTRACTION LOOP LIVES HERE. Carried is at risk: dying, or leaving
    /// without reaching another Rift, loses all of it. Secured is banked and cannot be lost, even
    /// on death. A Rift is where one becomes the other.
    ///
    /// RUN-SCOPED AND NEVER PERSISTED. Nothing here is written to the profile until it is BANKED -
    /// see <see cref="Bank"/> - which is what makes the risk real rather than a display. A carried
    /// item that had already been saved would be a promise the game could not take back.
    ///
    /// Items are <see cref="MintedGearRecord"/>, the same type the Forge already mints and
    /// `GearCatalog.Register` already knows how to make usable. Rolled at the moment they drop, so
    /// what the player is deciding whether to risk is a specific piece with specific stats rather
    /// than a sealed box - the choice at a Rift is only interesting if you know what is in the bag.
    ///
    /// XP IS DELIBERATELY NOT HERE. The run economy's rule is that death costs the tradeable stake
    /// and never the progression: mastery accrues per floor cleared regardless of how the run ends,
    /// so a bad extraction is a bad payday and never a wasted hour.
    /// </summary>
    public class RunLoot
    {
        readonly List<MintedGearRecord> _carried = new();
        readonly List<MintedGearRecord> _secured = new();

        public IReadOnlyList<MintedGearRecord> Carried => _carried;
        public IReadOnlyList<MintedGearRecord> Secured => _secured;

        public int CarriedCount => _carried.Count;
        public int SecuredCount => _secured.Count;

        // ------------------------------------------------------------------ Rift Boxes

        /// <summary>
        /// Boxes found on THIS run, not yet banked. At risk exactly as carried loot is.
        ///
        /// TWO POOLS, AND THE SPLIT IS THE POINT. A box already banked on the profile is safe and
        /// spendable on any run; a box found this run is only kept if the run is extracted. So a
        /// cautious run that leaves early banks its finds, and a deep run spends against a reserve
        /// it built on earlier, safer ones - which is the "bank Rift Boxes to spend on a later,
        /// more ambitious one" the design asks for, falling out of the two pools rather than
        /// needing a rule.
        /// </summary>
        public int FoundBoxes { get; private set; }

        /// <summary>Boxes carried in from the profile at run start - safe, and untouched by death.
        /// Tracked here only so the Rift can spend from one pool and know which.</summary>
        public int BankedBoxes { get; private set; }

        public int TotalBoxes => BankedBoxes + FoundBoxes;

        public void SetBankedBoxes(int n) => BankedBoxes = Mathf.Max(0, n);

        public void FindBox() => FoundBoxes++;

        // ------------------------------------------------------------------ tiered Forge boxes

        /// <summary>
        /// Bronze-BlackDiamond boxes found this run, not yet banked - the same found/banked,
        /// at-risk-until-extraction split as Rift Boxes above, generalised to a tier. Kept as a
        /// Dictionary here (unlike CharacterProfile.LootBoxes) because RunLoot is run-scoped and
        /// never itself passed through JsonUtility.
        /// </summary>
        readonly Dictionary<LootTier, int> _foundTieredBoxes = new();
        readonly Dictionary<LootTier, int> _bankedTieredBoxes = new();

        public void SetBankedTieredBoxes(LootBoxes boxes)
        {
            _bankedTieredBoxes.Clear();
            if (boxes == null) return;
            foreach (LootTier tier in System.Enum.GetValues(typeof(LootTier)))
                _bankedTieredBoxes[tier] = Mathf.Max(0, boxes.Get(tier));
        }

        public int FoundTieredBoxes(LootTier tier) => _foundTieredBoxes.TryGetValue(tier, out int n) ? n : 0;
        public int BankedTieredBoxes(LootTier tier) => _bankedTieredBoxes.TryGetValue(tier, out int n) ? n : 0;
        public int TotalTieredBoxes(LootTier tier) => FoundTieredBoxes(tier) + BankedTieredBoxes(tier);

        public void FindTieredBox(LootTier tier)
            => _foundTieredBoxes[tier] = FoundTieredBoxes(tier) + 1;

        /// <summary>
        /// Spend one box. Consumes a FOUND one first.
        ///
        /// Found-first is deliberate: those are the ones that die with the run, so spending them
        /// ahead of the safe reserve is what the player would choose every time. Making them pick
        /// would be a decision with one right answer, which is not a decision.
        /// </summary>
        public bool SpendBox()
        {
            if (FoundBoxes > 0) { FoundBoxes--; return true; }
            if (BankedBoxes > 0) { BankedBoxes--; return true; }
            return false;
        }

        public void Clear()
        {
            _carried.Clear();
            _secured.Clear();
            FoundBoxes = 0;
            BankedBoxes = 0;
            _foundTieredBoxes.Clear();
            _bankedTieredBoxes.Clear();
        }

        public void Add(MintedGearRecord record)
        {
            if (record != null) _carried.Add(record);
        }

        /// <summary>Move one carried item behind the line. Ignores anything not actually carried,
        /// so a double-click at the Rift screen cannot secure the same piece twice.</summary>
        public bool Secure(MintedGearRecord record)
        {
            if (record == null || !_carried.Remove(record)) return false;
            _secured.Add(record);
            return true;
        }

        /// <summary>
        /// Extraction: everything carried comes out too - including unspent boxes found this run.
        ///
        /// The boxes are moved into the BANKED pool here rather than being left to Bank, because
        /// EndRun calls ForfeitCarried before it banks and that zeroes the found pool. Without this
        /// an extracted run would lose exactly the boxes it had earned the right to keep, which is
        /// the opposite of what extracting means.
        /// </summary>
        public void SecureAll()
        {
            _secured.AddRange(_carried);
            _carried.Clear();
            BankedBoxes += FoundBoxes;
            FoundBoxes = 0;

            foreach (var tier in new List<LootTier>(_foundTieredBoxes.Keys))
            {
                _bankedTieredBoxes[tier] = BankedTieredBoxes(tier) + _foundTieredBoxes[tier];
                _foundTieredBoxes[tier] = 0;
            }
        }

        /// <summary>
        /// Death, or leaving without reaching another Rift. Carried is gone; secured is untouched.
        /// </summary>
        public void ForfeitCarried()
        {
            _carried.Clear();

            // Boxes found this run die with it, exactly as carried loot does - they were part of
            // the same stake. The BANKED pool is untouched: it was already safe before the run
            // started, and losing a reserve built over earlier runs because of one bad night is a
            // different and much harsher promise than the one this system makes.
            FoundBoxes = 0;

            foreach (var tier in new List<LootTier>(_foundTieredBoxes.Keys))
                _foundTieredBoxes[tier] = 0;
        }

        /// <summary>
        /// Write the secured half into the profile. Called once, at the run-end checkpoint, so it
        /// rides the same write everything else does rather than opening its own.
        ///
        /// Registers with the catalog as it goes, exactly as the Forge does - a record the catalog
        /// has never seen resolves to nothing the moment anything tries to equip it.
        /// </summary>
        public int Bank(CharacterProfile profile)
        {
            if (profile == null) return 0;
            foreach (var r in _secured)
            {
                profile.MintedGear.Add(r);
                GearCatalog.Register(r.ToGearItem());
            }
            int n = _secured.Count;
            _secured.Clear();

            // Whatever survived - found and unspent, plus the reserve that was never touched -
            // goes back to the profile as one number.
            profile.RiftBoxes = BankedBoxes + FoundBoxes;
            BankedBoxes = 0;
            FoundBoxes = 0;

            // Same overwrite, per tier - BankedTieredBoxes started as a snapshot of profile.Boxes
            // at run start (SetBankedTieredBoxes) and nothing else writes profile.Boxes mid-run.
            profile.Boxes.Bronze = TotalTieredBoxes(LootTier.Bronze);
            profile.Boxes.Silver = TotalTieredBoxes(LootTier.Silver);
            profile.Boxes.Gold = TotalTieredBoxes(LootTier.Gold);
            profile.Boxes.Diamond = TotalTieredBoxes(LootTier.Diamond);
            profile.Boxes.BlackDiamond = TotalTieredBoxes(LootTier.BlackDiamond);
            _foundTieredBoxes.Clear();
            _bankedTieredBoxes.Clear();
            return n;
        }

        // ------------------------------------------------------------------ drops

        /// <summary>
        /// What a floor pays, by depth.
        ///
        /// BANDED AT THE BOSS FLOORS RATHER THAN SMOOTHED, deliberately: a visible jump in what a
        /// floor pays is what makes clearing a reincarnation boss feel like it unlocked something.
        /// A continuous curve would be fairer and would say nothing.
        ///
        ///     1-24    Bronze 100%
        ///     25-49   Bronze 70   Silver 30
        ///     50-74   Bronze 40   Silver 45   Gold 15
        ///     75-99   Bronze 15   Silver 45   Gold 40
        ///     100     Diamond 100
        ///
        /// Diamond is reserved ENTIRELY for floor 100 - it is the completion prize, not a
        /// deep-floor drop, and the only way to hold one is to have finished a run.
        /// </summary>
        public static LootTier TierFor(int floor)
        {
            if (floor >= 100) return LootTier.Diamond;

            float r = Random.value;
            if (floor >= 75) return r < 0.15f ? LootTier.Bronze : r < 0.60f ? LootTier.Silver : LootTier.Gold;
            if (floor >= 50) return r < 0.40f ? LootTier.Bronze : r < 0.85f ? LootTier.Silver : LootTier.Gold;
            if (floor >= 25) return r < 0.70f ? LootTier.Bronze : LootTier.Silver;
            return LootTier.Bronze;
        }

        /// <summary>
        /// Roll a floor's drop. Returns null when the drop was a loot box instead of an item -
        /// see FloorBoxChance - in which case the box has already been credited via FindTieredBox
        /// and the caller has nothing further to add to Carried.
        ///
        /// The TIER comes from depth and the stats come from GearRoller as usual - but the roller
        /// picks its own tier from the seed, so the depth-banded tier is applied over the top. That
        /// keeps one source of truth for what a tier is WORTH (the roller) and another for how
        /// often you see one (depth), which are genuinely different questions.
        /// </summary>
        public MintedGearRecord Roll(int floor)
        {
            var tier = TierFor(floor);

            // A BOX INSTEAD OF AN ITEM, some of the time - the Forge's own currency for
            // combining/upgrading, not just an alternate item source. At risk exactly like the
            // gear it substitutes for, hence FindTieredBox rather than a direct profile write.
            if (Random.value < Tuning.GearRoll.FloorBoxChance)
            {
                FindTieredBox(tier);
                return null;
            }

            // Diamond and Black Diamond are DESIGNS, not rolls - one of the tier's authored
            // pieces (see DesignDrops). A Black Diamond weapon comes with its relic: the relic is
            // carried here and the weapon handed back like any other drop.
            if (DesignDrops.IsDesignTier(tier))
            {
                var design = DesignDrops.Pick(DesignDrops.Pool(tier), Random.Range(int.MinValue, int.MaxValue));
                if (design != null)
                {
                    var made = DesignDrops.Mint(design, "LOOT");
                    for (int i = 1; i < made.Count; i++) Add(made[i]);
                    return made[0];
                }
            }

            // RELICS DROP NOW. They used to be swapped out for a Torso because "the roller has no
            // relic table" - true while a relic was cosmetic and rolled nothing, and false since
            // a relic's roll became its FINISHER. Excluding them would mean the one slot whose
            // whole content is randomised was the one slot loot never handed you.
            var slot = GearSlots.Selectable[Random.Range(0, GearSlots.Selectable.Length)];

            string instanceId = $"LOOT-{slot}-{System.Guid.NewGuid():N}";

            // Tier comes from DEPTH, so the rolls are handed that tier rather than the forge draw
            // RollItem would make for itself. Calling RollItem here rolled the stats against a
            // tier the record then overwrote, which quietly decoupled what a deep drop claimed
            // to be from what it was worth.
            int seed = instanceId.GetHashCode();
            var (_, primary) = GearRoller.Roll(slot, tier, seed);
            var subs = GearRoller.RollSubStats(slot, tier, seed);
            var grants = GearRoller.BuildGrants(slot, tier, primary, 0, subs);
            var ability = slot == GearSlot.Torso
                ? GearRoller.RollDefensiveAbility(seed)
                : DefensiveAbility.Dash;

            // Greatsword because a minted record carries no class and GearItem.Class defaults
            // there - the two have to agree or the relic could never be socketed.
            var finisher = GearRoller.RollFinisher(slot, tier, WeaponClass.Greatsword, seed);

            return new MintedGearRecord
            {
                InstanceId = instanceId,
                DisplayName = $"{tier} {slot}",
                Slot = slot,
                Tier = tier,
                Grants = grants,
                PrimaryStat = primary,
                SubStats = subs,
                DefensiveAbility = ability,
                Finisher = finisher,
                Class = WeaponClass.Greatsword,
                StatsVersion = GearRoller.TablesVersion,
            };
        }
    }
}
