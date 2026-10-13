using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain.Service
{
    /// <summary>
    /// The player's hub room, saved by equivalence-service under this browser's identity (a guest,
    /// or a proven wallet - ServiceIdentity). The identity is the key: the accountId the game passes
    /// only names the record it gets back (LocalId for a guest, the address for a wallet), the same
    /// "the key wins" rule as LocalJsonAccountStore.
    ///
    /// As with characters, only a 404 means "no record". Any other failed read plays on a fresh room
    /// that is never saved this session - a fresh room saved over an arranged one destroys work.
    /// </summary>
    public class ServiceAccountStore : IAccountStore
    {
        bool _unreadable;

        public async Task<bool> ExistsAsync(string accountId)
        {
            if (!await ServiceIdentity.EnsureAsync()) return false;
            // False on failure, NOT true: a wrong "true" loads an empty room over an arranged one.
            return (await ServiceClient.Get("/saves/account", ServiceIdentity.Token)).Ok;
        }

        public async Task<AccountProfile> LoadAsync(string accountId)
        {
            AccountProfile account = null;
            _unreadable = false;
            if (await ServiceIdentity.EnsureAsync())
            {
                var r = await ServiceClient.Get("/saves/account", ServiceIdentity.Token);
                if (r.Ok)
                {
                    try { account = JsonUtility.FromJson<AccountProfile>(r.Body); }
                    catch (Exception e) { Debug.LogWarning($"[Service] room unparseable: {e.Message}"); }
                    if (account == null) _unreadable = true;
                }
                else if (!r.NotFound)
                {
                    _unreadable = true;
                    Debug.LogWarning($"[Service] could not read the room ({r}) - it will NOT be saved this session");
                }
            }
            else _unreadable = true;

            account ??= new AccountProfile { AccountId = accountId };
            account.AccountId = accountId;
            account.Room ??= new RoomLayout();
            return account;
        }

        public async Task SaveAsync(AccountProfile account)
        {
            var tx = TxLog.Post(TxKind.Account, "Room saved",
                $"account {account.AccountId}\ntrophies placed {account.Room.Trophies.Count}");
            if (_unreadable)
            {
                TxLog.Fail(tx, "not saved - the room could not be read this session");
                return;
            }
            if (!await ServiceIdentity.EnsureAsync())
            {
                TxLog.Fail(tx, "no identity on the service");
                return;
            }

            var r = await ServiceClient.Put("/saves/account",
                "{\"datum\":" + ServiceClient.Quote(JsonUtility.ToJson(account)) + "}", ServiceIdentity.Token);
            if (r.Ok) TxLog.Confirm(tx, "save:account");
            else
            {
                TxLog.Fail(tx, r.Status == 0 ? $"service unreachable: {r.Error}" : $"{r.Status} {r.Body}");
                Debug.LogWarning($"[Service] room save failed: {r}");
            }
        }
    }
}
