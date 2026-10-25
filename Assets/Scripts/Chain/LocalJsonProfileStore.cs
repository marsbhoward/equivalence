using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Chain
{
    /// <summary>
    /// Demo implementation: writes the same datum shape to a local JSON file, at the same
    /// three checkpoints a Cardano transaction would occupy. Swapping in a Cip68ProfileStore
    /// should require no gameplay changes.
    ///
    /// DELIBERATELY POSTS NOTHING TO TxLog. The terminal is scoped to CHAIN transactions - it is a
    /// receipt roll for things that actually left the machine - so a local write has no row to
    /// contribute. This also retired the "local:" hash prefix, which existed only to mark rows in
    /// a mixed view; with local writes absent there is nothing left to distinguish them from.
    ///
    /// The pending-then-confirm split those posts used to demonstrate still matters and still
    /// lives in TxLog - it is exercised by the chain stores in Chain/Web, which is the only place
    /// a write genuinely settles a block later. Nothing was simplified away, only unhooked from
    /// the store that could never produce a real hash.
    /// </summary>
    public class LocalJsonProfileStore : IProfileStore
    {
        static string PathFor(string id) => Path.Combine(Application.persistentDataPath, $"profile_{id}.json");

        public Task<CharacterProfile> LoadAsync(string profileId)
        {
            var path = PathFor(profileId);
            CharacterProfile profile;
            if (File.Exists(path))
            {
                try { profile = JsonUtility.FromJson<CharacterProfile>(File.ReadAllText(path)); }
                catch (Exception e) { Debug.LogWarning($"[ProfileStore] unreadable profile, starting fresh: {e.Message}"); profile = null; }
            }
            else profile = null;

            profile ??= CharacterProfile.NewDefault(profileId);
            Debug.Log($"[ProfileStore] loaded {profileId} (runs={profile.TotalRuns}, best={profile.BestKills}) from {path}");
            return Task.FromResult(profile);
        }

        /// <summary>
        /// Everything matching profile_*.json, ordered oldest id first so the roster does not
        /// reshuffle itself between visits to the couch.
        /// </summary>
        public Task<System.Collections.Generic.List<CharacterProfile>> ListAsync()
        {
            var found = new System.Collections.Generic.List<CharacterProfile>();
            string dir = Application.persistentDataPath;
            if (!Directory.Exists(dir)) return Task.FromResult(found);

            foreach (var path in Directory.GetFiles(dir, "profile_*.json"))
            {
                try
                {
                    var p = JsonUtility.FromJson<CharacterProfile>(File.ReadAllText(path));
                    // A file whose id does not match its name is a rename or a hand-edit, and
                    // trusting the id would list a character that cannot then be loaded back.
                    if (p == null) continue;
                    p.ProfileId = Path.GetFileNameWithoutExtension(path).Substring("profile_".Length);
                    found.Add(p);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ProfileStore] skipping unreadable {Path.GetFileName(path)}: {e.Message}");
                }
            }

            found.Sort((a, b) => string.CompareOrdinal(a.ProfileId, b.ProfileId));
            return Task.FromResult(found);
        }

        public Task<string> DefaultProfileIdAsync() => Task.FromResult("local-dev-0");

        public string NewProfileId(System.Collections.Generic.ICollection<string> taken)
        {
            for (int i = 0; i < 64; i++)
            {
                var candidate = $"local-dev-{i}";
                if (!taken.Contains(candidate)) return candidate;
            }
            return null;
        }

        public async Task BeginRunAsync(CharacterProfile profile, string runId, ElementType element)
        {
            ProfileRules.RunStart(profile, element);
            Save(profile);
            Debug.Log($"[ProfileStore] CHECKPOINT run-start  run={runId} element={element}");

            // Yielded before confirming so the pending state is real for at least a frame. A local
            // file write settles instantly; a chain write does not, and the terminal has to be
            // able to draw the in-between.
            await Task.Yield();
        }

        public async Task CommitRunAsync(CharacterProfile profile, RunSummary summary)
        {
            ProfileRules.RunEnd(profile, summary);
            Save(profile);
            Debug.Log($"[ProfileStore] CHECKPOINT run-end    run={summary.RunId} " +
                      $"kills={summary.Kills} totalRuns={profile.TotalRuns} " +
                      $"mastery={profile.Mastery.TotalLevel}");

            await Task.Yield();
        }

        public async Task<FinalFloorReward?> FinalFloorRewardAsync(CharacterProfile profile, string runId)
        {
            int week = FinalFloorReward.ThisWeek;
            var reward = FinalFloorReward.Roll(profile.GuaranteeWeek != week, week, new System.Random());
            // The cosmetics, credited at the decision (database first) - once per run on a local save.
            if (Decide(profile, $"final:{runId}"))
            {
                if (!string.IsNullOrEmpty(reward.Dye)) profile.Items.AddDye(reward.Dye, 1);
                (reward.Pose, reward.PoseGold) = CosmeticRolls.Pose(profile.Items);
                if (!string.IsNullOrEmpty(reward.Pose)) profile.Items.Poses.Add(reward.Pose);
            }
            else reward.Dye = null;
            Debug.Log($"[ProfileStore] floor {Rifts.RunLoot.FinalFloor,-4}  run={runId} {reward.Count} {reward.Tier}" +
                      (reward.Guaranteed ? " (weekly guarantee)" : ""));
            await Task.Yield();
            return reward;
        }

        public async Task<AvatarBoxes?> AvatarBoxesAsync(CharacterProfile profile, string runId, int floor)
        {
            // Rolled BEFORE the first await, so a caller inside a seeded scope (Rifts.RunRolls) gets
            // the same boxes on a replay.
            var boxes = Rifts.RunLoot.RollAvatarLocal(floor);
            if (Rifts.RunLoot.IsRolledAvatar(floor) && Decide(profile, $"avatar:{runId}:{floor}"))
            {
                var rng = new System.Random();
                if (rng.NextDouble() < Tuning.Cosmetics.AvatarRareDyeChance) boxes.Dye = CosmeticRolls.RareDye(rng);
                (boxes.Pose, boxes.PoseGold) = CosmeticRolls.Pose(profile.Items, rng);
                if (!string.IsNullOrEmpty(boxes.Dye)) profile.Items.AddDye(boxes.Dye, 1);
                if (!string.IsNullOrEmpty(boxes.Pose)) profile.Items.Poses.Add(boxes.Pose);
            }
            await Task.Yield();
            return boxes;
        }

        /// <summary>True the first time a decision is asked for - a local save keeps no service
        /// record, so this is what stops a replayed floor crediting its cosmetics twice.</summary>
        static bool Decide(CharacterProfile profile, string key)
        {
            profile.CosmeticsDecided ??= new System.Collections.Generic.List<string>();
            profile.Items ??= new CharacterItems();
            if (profile.CosmeticsDecided.Contains(key)) return false;
            profile.CosmeticsDecided.Add(key);
            return true;
        }

        public async Task<MilestoneReward?> MilestoneAsync(CharacterProfile profile, string runId, int floor)
        {
            await Task.Yield();
            var m = CosmeticRolls.MilestoneAt(floor);
            profile.Items ??= new CharacterItems();
            if (m == null || profile.Items.Passed(m.Value.Milestone)) return null;
            profile.Items.Milestones.Add(m.Value.Milestone);
            if (!string.IsNullOrEmpty(m.Value.Dye)) profile.Items.AddDye(m.Value.Dye, 1);
            Debug.Log($"[ProfileStore] milestone {m.Value.Milestone} (floor {floor}): {m.Value.Diamond} Diamond, dye {m.Value.Dye}");
            return m;
        }

        public Task<CharacterItems> ItemsAsync(CharacterProfile profile)
            => Task.FromResult(profile.Items ??= new CharacterItems());

        public Task<string> RecolourAsync(CharacterProfile profile, System.Collections.Generic.List<PieceDye> changes)
        {
            profile.Items ??= new CharacterItems();
            string refused = Recolour.Apply(profile.Items, changes);
            if (refused == null) Save(profile);
            return Task.FromResult(refused);
        }

        public Task<string> BreakBoxAsync(CharacterProfile profile)
        {
            if (profile.Boxes.Diamond <= 0) return Task.FromResult("no Diamond box to break");
            profile.Boxes.Diamond--;
            profile.Items ??= new CharacterItems();
            profile.Items.Charges += Tuning.Cosmetics.ChargesPerBox;
            Save(profile);
            return Task.FromResult<string>(null);
        }

        public async Task UnlockAsync(CharacterProfile profile)
        {

            Save(profile);
            Debug.Log($"[ProfileStore] CHECKPOINT unlock     gear={profile.Gear.Count} " +
                      $"nodes={profile.Mastery.Unlocked.Count}");

            await Task.Yield();
        }


        static void Save(CharacterProfile p)
        {
            File.WriteAllText(PathFor(p.ProfileId), JsonUtility.ToJson(p, true));
        }
    }
}
