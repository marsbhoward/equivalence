using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Chain.Service
{
    /// <summary>
    /// Characters saved by equivalence-service (routes/saves.ts), under this browser's identity - a
    /// guest, or a proven wallet (ServiceIdentity). Every browser player's store: there is no
    /// local-only web tier any more (the user's call, 2026-10-13).
    ///
    /// The same three write points as LocalJsonProfileStore, and the same rules (ProfileRules) before
    /// anything is sent - the service records what the game owns, it does not recompute it.
    ///
    /// A READ THAT FAILS IS NOT A MISSING CHARACTER. Only a 404 means "no such character, start
    /// fresh". Any other failure still hands the game a fresh profile so it stays playable, but that
    /// character is never WRITTEN this session: a fresh stand-in saved over the real one would erase
    /// it. (The service refuses a save going backwards as well - the far side of the same guard.)
    /// </summary>
    public class ServiceProfileStore : IProfileStore
    {
        // Non-readonly and null-guarded - a domain reload hands back a fresh, empty set either way,
        // and a readonly one would be reinitialised under live code (CLAUDE.md, domain reload traps).
        HashSet<string> _unreadable = new();

        [Serializable]
        class Roster { public List<CharacterProfile> items = new(); }

        static string CharacterPath(string profileId) => "/saves/characters/" + Uri.EscapeDataString(profileId);

        /// <summary>A request under the current identity; a guest token the service says was graduated
        /// elsewhere is dropped and a new guest started, once.</summary>
        static async Task<ServiceClient.Reply> Ask(Func<string, Task<ServiceClient.Reply>> send)
        {
            if (!await ServiceIdentity.EnsureAsync()) return new ServiceClient.Reply { Status = 0, Error = "no identity" };
            var r = await send(ServiceIdentity.Token);
            if (r.Status == 410 && !ServiceIdentity.OnWallet)
            {
                Debug.Log("[Service] this browser's guest was graduated onto a wallet - starting a new guest");
                ServiceIdentity.ForgetGuest();
                if (await ServiceIdentity.EnsureAsync()) r = await send(ServiceIdentity.Token);
            }
            return r;
        }

        public async Task<CharacterProfile> LoadAsync(string profileId)
        {
            _unreadable ??= new HashSet<string>();
            CharacterProfile profile = null;
            var r = await Ask(t => ServiceClient.Get(CharacterPath(profileId), t));
            if (r.Ok)
            {
                try { profile = JsonUtility.FromJson<CharacterProfile>(r.Body); }
                catch (Exception e) { Debug.LogWarning($"[Service] save for {profileId} unparseable: {e.Message}"); }
                if (profile == null) _unreadable.Add(profileId);
            }
            else if (!r.NotFound)
            {
                _unreadable.Add(profileId);
                Debug.LogWarning($"[Service] could not read {profileId} ({r}) - playing a fresh stand-in that will NOT be saved");
            }

            profile ??= CharacterProfile.NewDefault(profileId);
            profile.ProfileId = profileId;   // the key wins over whatever the save claims
            Debug.Log($"[Service] loaded {profileId} (runs={profile.TotalRuns}, best={profile.BestKills})");
            return profile;
        }

        public async Task<List<CharacterProfile>> ListAsync()
        {
            var r = await Ask(t => ServiceClient.Get("/saves/characters", t));
            if (!r.Ok)
            {
                Debug.LogWarning($"[Service] roster unavailable: {r}");
                return new List<CharacterProfile>();
            }
            var list = JsonUtility.FromJson<Roster>(r.Body)?.items ?? new List<CharacterProfile>();
            list.Sort((a, b) => string.CompareOrdinal(a.ProfileId, b.ProfileId));
            return list;
        }

        /// <summary>The character this identity plays first: its earliest, or a new one. A server
        /// character never takes a device name like local-dev-0 - its id has to be unique to the
        /// whole service, because the chain paths (runs, boxes, the PFP) key on it.</summary>
        public async Task<string> DefaultProfileIdAsync()
        {
            var roster = await ListAsync();
            return roster.Count > 0 ? roster[0].ProfileId : NewProfileId(new HashSet<string>());
        }

        public string NewProfileId(ICollection<string> taken)
        {
            string id;
            do id = "c-" + Guid.NewGuid().ToString("N"); while (taken.Contains(id));
            return id;
        }

        public async Task BeginRunAsync(CharacterProfile profile, string runId, ElementType element)
        {
            var tx = TxLog.Post(TxKind.RunStart, $"Run claimed - {element}",
                                $"run {runId}\nprofile {profile.ProfileId}\nelement {element}");
            ProfileRules.RunStart(profile, element);
            await Save(tx, profile, "run-start", runId);

            // A wallet's run is also REGISTERED for the box mint at its end (routes/player.ts) - only
            // a run the service saw start may mint, and only once.
            if (ServiceIdentity.OnWallet)
                await Write("/write/begin-run",
                    $"{{\"profileId\":{ServiceClient.Quote(profile.ProfileId)},\"runId\":{ServiceClient.Quote(runId)}}}");
        }

        public async Task CommitRunAsync(CharacterProfile profile, RunSummary summary)
        {
            var tx = TxLog.Post(TxKind.RunEnd,
                $"Run committed - {summary.Kills} kills, floor {summary.Floors}",
                $"run {summary.RunId}\nelement {summary.Element}\nkills {summary.Kills}\n" +
                $"xp +{summary.XpEarned}\nsurvived {summary.Survived}");
            ProfileRules.RunEnd(profile, summary);
            await Save(tx, profile, "run-end", summary.RunId);

            // EXTRACTION, for a wallet: the run's secured design boxes are minted to it.
            // OPEN (the user's rule, 2026-10-13): nothing should reach a wallet without a PFP - this
            // path does not check yet. See memory project_pfp_gates_tokens.
            if (ServiceIdentity.OnWallet)
                await Write("/write/commit-run",
                    $"{{\"profileId\":{ServiceClient.Quote(profile.ProfileId)},\"summary\":{ServiceClient.Quote(JsonUtility.ToJson(summary))}}}");
        }

        public async Task UnlockAsync(CharacterProfile profile)
        {
            var tx = TxLog.Post(TxKind.Unlock, "Permanent unlocks written",
                $"profile {profile.ProfileId}\ngear {profile.Gear.WornCount()} / " +
                $"{Art.Gear.GearSlots.Worn.Length}\nmastery nodes {profile.Mastery.Unlocked.Count}");
            await Save(tx, profile, "unlock", null);
        }

        async Task Save(TxRecord tx, CharacterProfile profile, string reason, string runId)
        {
            _unreadable ??= new HashSet<string>();
            if (_unreadable.Contains(profile.ProfileId))
            {
                TxLog.Fail(tx, "not saved - this character could not be read this session");
                return;
            }

            string body = "{\"reason\":" + ServiceClient.Quote(reason)
                        + (runId != null ? ",\"runId\":" + ServiceClient.Quote(runId) : "")
                        + ",\"datum\":" + ServiceClient.Quote(JsonUtility.ToJson(profile)) + "}";
            var r = await Ask(t => ServiceClient.Put(CharacterPath(profile.ProfileId), body, t));
            if (r.Ok) TxLog.Confirm(tx, HashOf(r.Body));
            else if (r.Status == 409 && r.Body.Contains("stale_save"))
                TxLog.Fail(tx, "the service holds a newer save of this character");
            else
            {
                TxLog.Fail(tx, r.Status == 0 ? $"service unreachable: {r.Error}" : $"{r.Status} {r.Body}");
                Debug.LogWarning($"[Service] {reason} save failed: {r}");
            }
        }

        /// <summary>A wallet-only write; its failure is logged, never thrown into the run.</summary>
        static async Task Write(string path, string body)
        {
            var r = await ServiceClient.Post(path, body, ServiceIdentity.Token);
            if (!r.Ok) Debug.LogWarning($"[Service] {path} failed: {r}");
            else Debug.Log($"[Service] {path}: {r.Body}");
        }

        [Serializable]
        class HashReply { public string hash; }

        static string HashOf(string body)
        {
            try { return JsonUtility.FromJson<HashReply>(body)?.hash ?? "saved"; }
            catch { return "saved"; }
        }
    }
}
