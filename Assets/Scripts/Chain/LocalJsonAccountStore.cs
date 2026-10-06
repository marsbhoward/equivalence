using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain
{
    /// <summary>
    /// Local JSON mirror of <see cref="IAccountStore"/>, the same demo shape the character store
    /// uses. A real implementation talks to the backend that indexes the wallet.
    /// </summary>
    public class LocalJsonAccountStore : IAccountStore
    {
        /// <summary>
        /// Wallet addresses are long and contain characters a filename cannot always carry, so
        /// the key is hashed rather than used raw. The address is stored INSIDE the record, which
        /// is what anything reading it actually wants.
        /// </summary>
        static string PathFor(string accountId)
        {
            string safe = accountId == AccountProfile.LocalId
                ? AccountProfile.LocalId
                : Mathf.Abs(accountId.GetHashCode()).ToString("x8");
            return Path.Combine(Application.persistentDataPath, $"account_{safe}.json");
        }

        public Task<bool> ExistsAsync(string accountId)
            => Task.FromResult(File.Exists(PathFor(accountId)));

        public Task<AccountProfile> LoadAsync(string accountId)
        {
            var path = PathFor(accountId);
            AccountProfile account = null;

            if (File.Exists(path))
            {
                try { account = JsonUtility.FromJson<AccountProfile>(File.ReadAllText(path)); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AccountStore] unreadable account, starting fresh: {e.Message}");
                }
            }

            account ??= new AccountProfile { AccountId = accountId };

            // The key wins over whatever the file claims: a record loaded by one id must not
            // report a different one, or the next save writes somewhere else entirely.
            account.AccountId = accountId;
            account.Room ??= new RoomLayout();

            Debug.Log($"[AccountStore] loaded {accountId} " +
                      $"({account.Room.Trophies.Count} trophies) from {path}");
            return Task.FromResult(account);
        }

        public async Task SaveAsync(AccountProfile account)
        {

            File.WriteAllText(PathFor(account.AccountId), JsonUtility.ToJson(account, true));

            await Task.Yield();
        }
    }
}
