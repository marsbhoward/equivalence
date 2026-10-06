using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain.Web
{
    /// <summary>
    /// The PLAYER record over the connector, keyed by wallet address - or by
    /// AccountProfile.LocalId when nothing is connected, which stays a normal, playable state.
    ///
    /// Kept separate from ChainProfileStore for the reason IAccountStore already documents: how a
    /// room was arranged is not something a checkpoint transaction should pay for. In practice a
    /// connector will very likely back this with off-chain storage keyed by address and put only
    /// the character datum on chain - which is exactly why it is its own interface.
    /// </summary>
    public class ChainAccountStore : IAccountStore
    {
        public async Task<bool> ExistsAsync(string accountId)
        {
            try
            {
                var r = await WebChainBridge.Call("accountExists", new ChainArgs { accountId = accountId });
                return r == "true" || r == "1";
            }
            catch (Exception e)
            {
                // False on failure, NOT true. ConnectWallet reads this to decide whether to adopt
                // the local record; a wrong "true" loads an empty account over a room the player
                // arranged, which is the one outcome here that destroys work.
                Debug.LogWarning($"[Chain] exists({accountId}) failed, assuming no record: {e.Message}");
                return false;
            }
        }

        public async Task<AccountProfile> LoadAsync(string accountId)
        {
            AccountProfile account = null;
            try
            {
                var json = await WebChainBridge.Call("loadAccount", new ChainArgs { accountId = accountId });
                if (!string.IsNullOrEmpty(json)) account = JsonUtility.FromJson<AccountProfile>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chain] account {accountId} unreadable, starting fresh: {e.Message}");
            }

            account ??= new AccountProfile { AccountId = accountId };
            account.AccountId = accountId;      // the key wins - see LocalJsonAccountStore
            account.Room ??= new RoomLayout();

            Debug.Log($"[Chain] loaded account {accountId} ({account.Room.Trophies.Count} trophies)");
            return account;
        }

        public async Task SaveAsync(AccountProfile account)
        {
            var tx = TxLog.Post(TxKind.Account, "Account record written",
                $"account {account.AccountId}\n" +
                (string.IsNullOrEmpty(account.WalletAddress) ? "no wallet connected\n" : "wallet linked\n") +
                $"trophies placed {account.Room.Trophies.Count}");

            try
            {
                var hash = await WebChainBridge.Call("saveAccount", new ChainArgs
                {
                    accountId = account.AccountId,
                    datum = JsonUtility.ToJson(account),
                });
                if (string.IsNullOrEmpty(hash)) TxLog.Fail(tx, "connector returned no receipt");
                else TxLog.Confirm(tx, hash);
            }
            catch (Exception e)
            {
                TxLog.Fail(tx, e.Message);
                Debug.LogWarning($"[Chain] saveAccount failed: {e.Message}");
            }
        }
    }
}
