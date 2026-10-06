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

        public async Task BeginRunAsync(CharacterProfile profile, string runId, ElementType element)
        {

            profile.LastElement = element;
            Save(profile);
            Debug.Log($"[ProfileStore] CHECKPOINT run-start  run={runId} element={element}");

            // Yielded before confirming so the pending state is real for at least a frame. A local
            // file write settles instantly; a chain write does not, and the terminal has to be
            // able to draw the in-between.
            await Task.Yield();
        }

        public async Task CommitRunAsync(CharacterProfile profile, RunSummary summary)
        {

            profile.TotalRuns += 1;
            profile.TotalKills += summary.Kills;
            profile.BestKills = Mathf.Max(profile.BestKills, summary.Kills);
            profile.BestFloor = Mathf.Max(profile.BestFloor, summary.Floors);
            profile.Xp += summary.XpEarned;
            profile.Level = 1 + profile.Xp / 100;
            profile.RecentRuns.Add(summary);
            if (profile.RecentRuns.Count > 10) profile.RecentRuns.RemoveAt(0);

            Save(profile);
            Debug.Log($"[ProfileStore] CHECKPOINT run-end    run={summary.RunId} " +
                      $"kills={summary.Kills} totalRuns={profile.TotalRuns} " +
                      $"mastery={profile.Mastery.TotalLevel}");

            await Task.Yield();
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
