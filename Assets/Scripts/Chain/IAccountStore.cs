using System.Threading.Tasks;

namespace Convergence.Chain
{
    /// <summary>
    /// Storage for the account-level record, separate from <see cref="IProfileStore"/>.
    ///
    /// Deliberately its own interface rather than more methods on the character store. That one
    /// is documented as the on-chain boundary with exactly three write points, and its datum is
    /// one CIP-68 reference NFT per character. This is neither: it is keyed by wallet address,
    /// not by character, and how a room was arranged is not something a checkpoint transaction
    /// should ever be paying for. Keeping them apart is what stops the two ideas being confused
    /// once a real backend exists.
    /// </summary>
    public interface IAccountStore
    {
        /// <summary>Load by wallet address, or by <see cref="AccountProfile.LocalId"/>.</summary>
        Task<AccountProfile> LoadAsync(string accountId);

        Task SaveAsync(AccountProfile account);

        /// <summary>True when a record already exists for this key - see AccountProfile.Adopt.</summary>
        Task<bool> ExistsAsync(string accountId);
    }
}
