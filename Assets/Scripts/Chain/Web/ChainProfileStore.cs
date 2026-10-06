using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Chain.Web
{
    [Serializable]
    class ProfileList
    {
        /// <summary>
        /// JsonUtility cannot deserialise a top-level array, so the connector wraps the roster as
        /// {"items":[...]}. That is a Unity limitation leaking into the wire format on purpose -
        /// the alternative is hand-parsing JSON in C#, which is worse.
        /// </summary>
        public List<CharacterProfile> items = new();
    }

    /// <summary>
    /// The CIP-68 store, over the browser connector.
    ///
    /// Deliberately the same THREE write points as LocalJsonProfileStore, mutating the profile in
    /// exactly the same way before it is sent. That symmetry is load bearing: the two stores must
    /// agree about what a run DOES, or swapping backends silently changes progression. If a rule
    /// changes here it changes there.
    ///
    /// What is genuinely different is settlement. The local store confirms after a Task.Yield so
    /// the pending state is real for a frame; here the confirmation is a real transaction hash
    /// arriving a block later, which is what TxLog's post/confirm split was built for.
    /// </summary>
    public class ChainProfileStore : IProfileStore
    {
        /// <summary>
        /// Whether a refused checkpoint should also refuse the game.
        ///
        /// Default FALSE, and it is a real trade rather than laziness. True is the strict reading -
        /// a run the chain never claimed should not be playable - but it makes every wallet hiccup
        /// a hard stop on the door of the arena. False keeps the game playable and leaves the
        /// FAILED row in the terminal as the record, which is what the receipt roll is for. Set it
        /// true once the connector is trusted enough that a failure means something.
        /// </summary>
        public static bool FailRunOnCheckpointError = false;

        public async Task<CharacterProfile> LoadAsync(string profileId)
        {
            CharacterProfile profile = null;
            try
            {
                var json = await WebChainBridge.Call("loadProfile", new ChainArgs { profileId = profileId });
                if (!string.IsNullOrEmpty(json)) profile = JsonUtility.FromJson<CharacterProfile>(json);
            }
            catch (Exception e)
            {
                // A read failure must not be fatal - an unreachable indexer should leave the
                // player at a fresh character, not a dead screen. Same posture as the local
                // store's unreadable-file path.
                Debug.LogWarning($"[Chain] load {profileId} failed, starting fresh: {e.Message}");
            }

            profile ??= CharacterProfile.NewDefault(profileId);
            profile.ProfileId = profileId;      // the key wins over whatever the datum claims
            Debug.Log($"[Chain] loaded {profileId} (runs={profile.TotalRuns}, best={profile.BestKills})");
            return profile;
        }

        public async Task<List<CharacterProfile>> ListAsync()
        {
            try
            {
                var json = await WebChainBridge.Call("listProfiles");
                if (string.IsNullOrEmpty(json)) return new List<CharacterProfile>();
                var wrapped = JsonUtility.FromJson<ProfileList>(json);
                var list = wrapped?.items ?? new List<CharacterProfile>();
                list.Sort((a, b) => string.CompareOrdinal(a.ProfileId, b.ProfileId));
                return list;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chain] roster unavailable: {e.Message}");
                return new List<CharacterProfile>();
            }
        }

        public async Task BeginRunAsync(CharacterProfile profile, string runId, ElementType element)
        {
            var tx = TxLog.Post(TxKind.RunStart, $"Run claimed - {element}",
                                $"run {runId}\nprofile {profile.ProfileId}\nelement {element}");

            profile.LastElement = element;

            await Settle(tx, "beginRun", new ChainArgs
            {
                profileId = profile.ProfileId,
                runId = runId,
                element = element.ToString(),
                datum = JsonUtility.ToJson(profile),
            }, rethrow: FailRunOnCheckpointError);
        }

        public async Task CommitRunAsync(CharacterProfile profile, RunSummary summary)
        {
            var tx = TxLog.Post(TxKind.RunEnd,
                $"Run committed - {summary.Kills} kills, floor {summary.Floors}",
                $"run {summary.RunId}\nelement {summary.Element}\nkills {summary.Kills}\n" +
                $"xp +{summary.XpEarned}\nsurvived {summary.Survived}");

            // Mutated here, exactly as the local store does, BEFORE the datum is serialised - the
            // chain records what the game already owns rather than computing it.
            profile.TotalRuns += 1;
            profile.TotalKills += summary.Kills;
            profile.BestKills = Mathf.Max(profile.BestKills, summary.Kills);
            profile.BestFloor = Mathf.Max(profile.BestFloor, summary.Floors);
            profile.Xp += summary.XpEarned;
            profile.Level = 1 + profile.Xp / 100;
            profile.RecentRuns.Add(summary);
            if (profile.RecentRuns.Count > 10) profile.RecentRuns.RemoveAt(0);

            await Settle(tx, "commitRun", new ChainArgs
            {
                profileId = profile.ProfileId,
                datum = JsonUtility.ToJson(profile),
                summary = JsonUtility.ToJson(summary),
            });
        }

        public async Task UnlockAsync(CharacterProfile profile)
        {
            var tx = TxLog.Post(TxKind.Unlock, "Permanent unlocks written",
                $"profile {profile.ProfileId}\ngear {profile.Gear.WornCount()} / " +
                $"{Art.Gear.GearSlots.Worn.Length}\n" +
                $"mastery nodes {profile.Mastery.Unlocked.Count}\nlook {profile.Look.HairStyle}/{profile.Look.Skin}");

            await Settle(tx, "unlock", new ChainArgs
            {
                profileId = profile.ProfileId,
                datum = JsonUtility.ToJson(profile),
            });
        }

        /// <summary>
        /// One place where a checkpoint becomes a transaction, so the post/confirm/fail triple
        /// cannot drift between the three write points.
        /// </summary>
        static async Task Settle(TxRecord tx, string method, ChainArgs args, bool rethrow = false)
        {
            try
            {
                var hash = await WebChainBridge.Call(method, args);
                // An empty hash is a connector that resolved without submitting anything. Confirming
                // it would put a row in the terminal claiming a write that did not happen.
                if (string.IsNullOrEmpty(hash)) TxLog.Fail(tx, "connector returned no transaction hash");
                else TxLog.Confirm(tx, hash);
            }
            catch (Exception e)
            {
                TxLog.Fail(tx, e.Message);
                Debug.LogWarning($"[Chain] {method} failed: {e.Message}");
                if (rethrow) throw;
            }
        }
    }
}
