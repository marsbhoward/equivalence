using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain.Service
{
    /// <summary>
    /// Who this browser's saves belong to on the service: a GUEST until a wallet is proven, then
    /// that wallet (the user's call, 2026-10-13).
    ///
    /// A guest is a server-issued token kept in PlayerPrefs (IndexedDB in a WebGL build). Every save
    /// from the first run on is held by the service under it, so when a wallet is proven the guest
    /// GRADUATES: the service moves its characters and hub room onto the address. Nothing is copied
    /// up from the browser, so nothing unverified comes along.
    ///
    /// Graduation only goes into a wallet with no records of its own (the hub room's rule); a wallet
    /// that already has characters keeps them, and this browser's guest stays where it was.
    /// </summary>
    public static class ServiceIdentity
    {
        const string GuestKey = "equivalence.guest";

        public static string WalletAddress { get; private set; } = "";
        static string _walletSession = "";

        public static bool OnWallet => !string.IsNullOrEmpty(_walletSession);
        public static string GuestToken => PlayerPrefs.GetString(GuestKey, "");

        /// <summary>What every save request carries.</summary>
        public static string Token => OnWallet ? _walletSession : GuestToken;

        static Task<bool> _starting;

        /// <summary>Makes sure there is an identity, starting a guest if this browser has none.</summary>
        public static Task<bool> EnsureAsync()
        {
            if (OnWallet || !string.IsNullOrEmpty(GuestToken)) return Task.FromResult(true);
            return _starting ??= StartGuestAsync();
        }

        static async Task<bool> StartGuestAsync()
        {
            try
            {
                var r = await ServiceClient.Post("/guest", "{}", null);
                if (!r.Ok)
                {
                    Debug.LogWarning($"[Service] could not start a guest: {r}");
                    return false;
                }
                var token = JsonUtility.FromJson<GuestReply>(r.Body)?.guest;
                if (string.IsNullOrEmpty(token)) return false;
                PlayerPrefs.SetString(GuestKey, token);
                PlayerPrefs.Save();
                Debug.Log("[Service] started a guest");
                return true;
            }
            finally
            {
                _starting = null;
            }
        }

        /// <summary>
        /// The service refused the guest token as GRADUATED (410) - another tab, or an earlier
        /// session, moved it onto a wallet. Its records are the wallet's now; this browser starts a
        /// new guest next time it needs one.
        /// </summary>
        public static void ForgetGuest()
        {
            PlayerPrefs.DeleteKey(GuestKey);
            PlayerPrefs.Save();
        }

        public enum Graduation { Moved, AlreadyMoved, WalletHasRecords, NoGuest, Failed }

        /// <summary>
        /// The wallet is proven: graduate the guest onto it, then make the wallet the identity. On a
        /// failure the identity stays the GUEST - switching anyway would leave the guest's characters
        /// behind, and the wallet's first save would then close the door on graduating them.
        /// </summary>
        public static async Task<Graduation> AttachWalletAsync(string address, string session)
        {
            if (string.IsNullOrEmpty(session)) return Graduation.Failed;

            var result = Graduation.NoGuest;
            var guest = GuestToken;
            if (!string.IsNullOrEmpty(guest))
            {
                var r = await ServiceClient.Post("/saves/graduate",
                    "{\"guest\":" + ServiceClient.Quote(guest) + "}", session);
                if (r.Ok)
                {
                    result = r.Body.Contains("\"repeat\":true") ? Graduation.AlreadyMoved : Graduation.Moved;
                    ForgetGuest();
                }
                else if (r.Status == 409 && r.Body.Contains("wallet_has_records")) result = Graduation.WalletHasRecords;
                else
                {
                    Debug.LogWarning($"[Service] graduation failed, staying a guest: {r}");
                    return Graduation.Failed;
                }
            }

            _walletSession = session;
            WalletAddress = address;
            Debug.Log($"[Service] saves now belong to {address} ({result})");
            return result;
        }

        /// <summary>Back to the guest (a new one, if this browser's graduated).</summary>
        public static void DetachWallet()
        {
            _walletSession = "";
            WalletAddress = "";
        }

        [System.Serializable]
        class GuestReply { public string guest; public string id; }
    }
}
