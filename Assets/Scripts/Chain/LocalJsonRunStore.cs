using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain
{
    /// <summary>
    /// The run in flight as a file beside the profile (run_&lt;id&gt;.json) - the Editor's and a native
    /// build's store, as LocalJsonProfileStore is for the character. One device, so the session rule
    /// is kept only so the two stores behave alike: a claim rewrites the session, and a write from
    /// another session is refused.
    /// </summary>
    public class LocalJsonRunStore : IRunStore
    {
        [Serializable]
        class Record
        {
            public string Session;
            public RunSnapshot Snapshot;
            public RunBeat Beat;
        }

        static string PathFor(string profileId)
            => Path.Combine(Application.persistentDataPath, $"run_{profileId}.json");

        static Record Read(string profileId)
        {
            var path = PathFor(profileId);
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<Record>(File.ReadAllText(path)); }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunStore] unreadable {Path.GetFileName(path)}: {e.Message}");
                return null;
            }
        }

        static void Write(string profileId, Record r)
            => File.WriteAllText(PathFor(profileId), JsonUtility.ToJson(r));

        public Task<RunLookup> LoadAsync(string profileId)
        {
            var r = Read(profileId);
            var run = r?.Snapshot != null ? new SuspendedRun { Snapshot = r.Snapshot, Beat = r.Beat } : null;
            return Task.FromResult(new RunLookup { Read = true, Run = run });
        }

        public Task<SuspendedRun> ClaimAsync(string profileId, string runId, string session)
        {
            var r = Read(profileId);
            if (r?.Snapshot == null || r.Snapshot.RunId != runId) return Task.FromResult<SuspendedRun>(null);
            r.Session = session;
            Write(profileId, r);
            return Task.FromResult(new SuspendedRun { Snapshot = r.Snapshot, Beat = r.Beat });
        }

        public Task<RunWrite> SaveAsync(string profileId, string json, RunSnapshot snapshot, string session)
        {
            try
            {
                var r = Read(profileId);
                if (r?.Snapshot != null && r.Snapshot.RunId == snapshot.RunId && r.Session != session)
                    return Task.FromResult(RunWrite.Superseded);
                if (r?.Snapshot != null && r.Snapshot.RunId == snapshot.RunId && snapshot.Seq <= r.Snapshot.Seq)
                    return Task.FromResult(RunWrite.Ok);   // an older one landing late - already overtaken
                // Parsed back from the JSON taken at capture time, not the live object - the run has
                // moved on since, and the record must say what the snapshot said.
                Write(profileId, new Record { Session = session, Snapshot = JsonUtility.FromJson<RunSnapshot>(json) });
                return Task.FromResult(RunWrite.Ok);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunStore] snapshot not written: {e.Message}");
                return Task.FromResult(RunWrite.Failed);
            }
        }

        public Task<RunWrite> BeatAsync(string profileId, RunBeat beat, string session)
        {
            try
            {
                var r = Read(profileId);
                if (r?.Snapshot == null || r.Snapshot.RunId != beat.RunId) return Task.FromResult(RunWrite.Failed);
                if (r.Session != session) return Task.FromResult(RunWrite.Superseded);
                if (r.Snapshot.Seq != beat.Seq) return Task.FromResult(RunWrite.Ok);   // a newer snapshot owns the floor
                r.Beat = beat;
                Write(profileId, r);
                return Task.FromResult(RunWrite.Ok);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunStore] beat not written: {e.Message}");
                return Task.FromResult(RunWrite.Failed);
            }
        }

        public Task<RunWrite> EndAsync(string profileId, string runId, string session)
        {
            try
            {
                var r = Read(profileId);
                if (r?.Snapshot == null || r.Snapshot.RunId != runId) return Task.FromResult(RunWrite.Ok);
                if (r.Session != session) return Task.FromResult(RunWrite.Superseded);
                File.Delete(PathFor(profileId));
                return Task.FromResult(RunWrite.Ok);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunStore] end not written: {e.Message}");
                return Task.FromResult(RunWrite.Failed);
            }
        }
    }
}
