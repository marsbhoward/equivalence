using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain.Service
{
    /// <summary>
    /// The run in flight, held by equivalence-service (routes/runs.ts) under this browser's identity -
    /// so a run left in one browser carries on in another, and survives a closed tab. Plain HTTP
    /// beside the saves (ServiceProfileStore), never through the wallet connector.
    /// </summary>
    public class ServiceRunStore : IRunStore
    {
        static string RunPath(string profileId) => "/saves/runs/" + Uri.EscapeDataString(profileId);

        /// <summary>The service's GET/claim answer. The snapshot and beat come back as JSON objects,
        /// which JsonUtility reads straight into the classes they were written from.</summary>
        [Serializable]
        class RunReply
        {
            public string runId;
            public int seq;
            public RunSnapshot snapshot;
            public RunBeat beat;
        }

        static async Task<ServiceClient.Reply> Ask(Func<string, Task<ServiceClient.Reply>> send)
        {
            if (!await ServiceIdentity.EnsureAsync()) return new ServiceClient.Reply { Status = 0, Error = "no identity" };
            return await send(ServiceIdentity.Token);
        }

        static SuspendedRun Parse(string body)
        {
            try
            {
                var r = JsonUtility.FromJson<RunReply>(body);
                if (r?.snapshot == null || string.IsNullOrEmpty(r.snapshot.RunId)) return null;
                // A beat the service returned is already its snapshot's; an empty object reads as Seq 0.
                var beat = r.beat != null && !string.IsNullOrEmpty(r.beat.RunId) ? r.beat : null;
                return new SuspendedRun { Snapshot = r.snapshot, Beat = beat };
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Service] run unreadable: {e.Message}");
                return null;
            }
        }

        public async Task<RunLookup> LoadAsync(string profileId)
        {
            var r = await Ask(t => ServiceClient.Get(RunPath(profileId), t));
            if (r.NotFound) return new RunLookup { Read = true };
            if (!r.Ok)
            {
                Debug.LogWarning($"[Service] could not ask for {profileId}'s run: {r}");
                return new RunLookup { Read = false };
            }
            return new RunLookup { Read = true, Run = Parse(r.Body) };
        }

        public async Task<SuspendedRun> ClaimAsync(string profileId, string runId, string session)
        {
            string body = $"{{\"runId\":{ServiceClient.Quote(runId)},\"session\":{ServiceClient.Quote(session)}}}";
            var r = await Ask(t => ServiceClient.Post(RunPath(profileId) + "/claim", body, t));
            if (!r.Ok)
            {
                Debug.LogWarning($"[Service] could not claim run {runId}: {r}");
                return null;
            }
            return Parse(r.Body);
        }

        public async Task<RunWrite> SaveAsync(string profileId, string json, RunSnapshot snapshot, string session)
        {
            string body = $"{{\"runId\":{ServiceClient.Quote(snapshot.RunId)},\"session\":{ServiceClient.Quote(session)}," +
                          $"\"seq\":{snapshot.Seq},\"snapshot\":{ServiceClient.Quote(json)}}}";
            var r = await Ask(t => ServiceClient.Put(RunPath(profileId), body, t));
            return Outcome(r, "snapshot");
        }

        public async Task<RunWrite> BeatAsync(string profileId, RunBeat beat, string session)
        {
            string body = $"{{\"runId\":{ServiceClient.Quote(beat.RunId)},\"session\":{ServiceClient.Quote(session)}," +
                          $"\"seq\":{beat.Seq},\"beat\":{ServiceClient.Quote(JsonUtility.ToJson(beat))}}}";
            var r = await Ask(t => ServiceClient.Put(RunPath(profileId) + "/beat", body, t));
            return Outcome(r, "beat");
        }

        public async Task<RunWrite> EndAsync(string profileId, string runId, string session)
        {
            string body = $"{{\"runId\":{ServiceClient.Quote(runId)},\"session\":{ServiceClient.Quote(session)}}}";
            var r = await Ask(t => ServiceClient.Post(RunPath(profileId) + "/end", body, t));
            return Outcome(r, "end");
        }

        static RunWrite Outcome(ServiceClient.Reply r, string what)
        {
            if (r.Ok) return RunWrite.Ok;
            if (r.Status == 409 && r.Body.Contains("superseded")) return RunWrite.Superseded;
            // An older snapshot landing after a newer one - nothing lost, the newer one is stored.
            if (r.Status == 409 && r.Body.Contains("stale_seq")) return RunWrite.Ok;
            // The service has no run start for this run - a retry can't fix that.
            if (r.Status == 409 && r.Body.Contains("not_current_run"))
            {
                Debug.LogWarning($"[Service] run {what} refused: the service did not see this run start - it will not be resumable");
                return RunWrite.Refused;
            }
            Debug.LogWarning($"[Service] run {what} not stored: {r}");
            return RunWrite.Failed;
        }
    }
}
