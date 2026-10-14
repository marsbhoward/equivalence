using System.Threading.Tasks;

namespace Convergence.Chain
{
    /// <summary>How a write to the run store went.</summary>
    public enum RunWrite
    {
        Ok,
        /// <summary>Another session took the run over (a resume on another device) - this one must
        /// leave the run WITHOUT settling it; the other is playing it.</summary>
        Superseded,
        /// <summary>Not stored - unreachable for now. Worth trying again.</summary>
        Failed,
        /// <summary>Never going to be stored - the service did not see this run start (its run-start
        /// save failed), so the run plays on without being resumable.</summary>
        Refused,
    }

    /// <summary>
    /// A RUN IN FLIGHT - the one thing saved mid-run (CONTINUATION.md). Kept apart from
    /// <see cref="IProfileStore"/> on purpose: that is the on-chain boundary, written at three
    /// checkpoints; this is written several times a floor and never goes near a chain. Deleted
    /// when the run ends.
    ///
    /// ONE LIVE SESSION PER RUN: every write names the session playing it; <see cref="ClaimAsync"/>
    /// hands the run to a new one, after which the old one's writes come back
    /// <see cref="RunWrite.Superseded"/>.
    ///
    /// Two implementations, picked with the profile store (<see cref="StoreFactory.Runs"/>): local JSON
    /// beside the profile, and equivalence-service (routes/runs.ts) for every browser player.
    /// </summary>
    public interface IRunStore
    {
        /// <summary>The run waiting for this character, if any. Never throws.</summary>
        Task<RunLookup> LoadAsync(string profileId);

        /// <summary>Takes the run over for <paramref name="session"/> and answers what it holds - the
        /// way a resume starts. Null when there is no such run or the store can't be reached.</summary>
        Task<SuspendedRun> ClaimAsync(string profileId, string runId, string session);

        Task<RunWrite> SaveAsync(string profileId, string json, RunSnapshot snapshot, string session);

        Task<RunWrite> BeatAsync(string profileId, RunBeat beat, string session);

        /// <summary>The run ended - death, extraction, or abandoned from the hub.</summary>
        Task<RunWrite> EndAsync(string profileId, string runId, string session);
    }
}
