using System.Threading.Tasks;

namespace Convergence.Chain
{
    /// <summary>
    /// The on-chain boundary. Per the roguelite handoff, the chain is only ever touched at
    /// run boundaries - never per-action, never per-turn. Any implementation of this
    /// interface (local JSON now, custodial CIP-68 backend later) must honour that: these
    /// three methods are the *only* write points in the game.
    /// </summary>
    /// <summary>
    /// Every write takes the LIVE profile the game is holding, not an id.
    ///
    /// An earlier version looked the profile up from storage inside each write and saved that
    /// copy, which silently threw away everything mutated during the run - durability, mastery,
    /// anything not passed in the summary. The store persists what the game owns; it does not
    /// re-read it.
    /// </summary>
    public interface IProfileStore
    {
        Task<CharacterProfile> LoadAsync(string profileId);

        /// <summary>
        /// Every character this account owns.
        ///
        /// A READ, not a write - it does not belong to the three checkpoints, because listing
        /// what you already have costs nothing on chain and is exactly the kind of thing a
        /// backend indexes rather than a transaction. The returned profiles are display copies:
        /// mutating one does nothing until it is loaded as the active profile.
        /// </summary>
        Task<System.Collections.Generic.List<CharacterProfile>> ListAsync();

        /// <summary>The character to play at start-up when nothing chose one: the local store's
        /// first slot, or the service identity's earliest character (or a new one).</summary>
        Task<string> DefaultProfileIdAsync();

        /// <summary>An id for a new character, never one in <paramref name="taken"/>. Local saves use
        /// device slots (local-dev-N); the service needs ids unique to the whole service, since the
        /// chain paths - runs, boxes, the PFP - key on them.</summary>
        string NewProfileId(System.Collections.Generic.ICollection<string> taken);

        /// <summary>Run-start checkpoint. Claims the run before play begins.</summary>
        Task BeginRunAsync(CharacterProfile profile, string runId, Core.ElementType element);

        /// <summary>Run-end checkpoint. Commits the cumulative datum update.</summary>
        Task CommitRunAsync(CharacterProfile profile, RunSummary summary);

        /// <summary>
        /// FLOOR 100's box - the one drop the game does not roll itself (<see cref="FinalFloorReward"/>):
        /// it carries the weekly guarantee, so the service decides it for a service player and
        /// records the decision against the run (asking again answers the same box), and a local
        /// save decides it from the profile. NOT a checkpoint - nothing about the profile is
        /// written. Null when no answer could be had.
        /// </summary>
        Task<FinalFloorReward?> FinalFloorRewardAsync(CharacterProfile profile, string runId);

        /// <summary>
        /// An AVATAR's boxes (<see cref="AvatarBoxes"/>), asked at its clear: the service decides
        /// them for a service player and records the decision against the run (asking again
        /// answers the same boxes), and a local save rolls them. NOT a checkpoint. Null when no
        /// answer could be had.
        /// </summary>
        Task<AvatarBoxes?> AvatarBoxesAsync(CharacterProfile profile, string runId, int floor);

        /// <summary>
        /// Permanent-unlock checkpoint - the third write point named in the roguelite handoff.
        /// Equipping is permanent, account-level and outside any run, so it is its own
        /// transaction rather than something folded into a run boundary.
        /// </summary>
        Task UnlockAsync(CharacterProfile profile);
    }
}
