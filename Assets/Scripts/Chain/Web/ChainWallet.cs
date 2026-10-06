using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain.Web
{
    /// <summary>
    /// The player's wallet: who they are, and what they own.
    ///
    /// WHAT IT IS NOT ASKED TO DO IS THE POINT. Connecting proves an ADDRESS - a signature over a
    /// message, which cannot move funds, costs no fee and spends no UTxO. Progression and room
    /// layout are written by the service with its own key, so ordinary play never asks the player
    /// to approve anything. The only transaction they ever sign is assembling their own PFP out
    /// of components they already hold.
    ///
    /// That is not only about trust. A player who can sign their own progression datum can write
    /// whatever they like into it, so game-authored progression is the anti-cheat as much as it
    /// is a courtesy.
    ///
    /// A wallet is a browser-injected object (CIP-30) or a peer session that resolves to the same
    /// interface (CIP-45). Nothing here assumes an extension, or a particular browser - only that
    /// the page put something at window.CoalescenceChain. Outside a browser build Available is
    /// false and the game runs unconnected, which AccountProfile.LocalId already treats as
    /// normal.
    /// </summary>
    public static class ChainWallet
    {
        public static bool Available => WebChainBridge.Available;

        public static string Address { get; private set; } = "";

        public static bool Connected => !string.IsNullOrEmpty(Address);

        /// <summary>
        /// True once the address has been proved by a signature this session.
        ///
        /// Separate from Connected because they answer different questions: a wallet can expose an
        /// address without proving control of it, and anything the SERVICE will write on this
        /// player's behalf has to rest on the proof, not on the claim.
        /// </summary>
        public static bool Proven { get; private set; }

        public static event Action Changed;

        /// <summary>Wallet keys the page can see - "eternl", "lace". Names, not addresses.</summary>
        public static async Task<string[]> ListAsync()
        {
            try
            {
                var csv = await WebChainBridge.Call("listWallets");
                if (string.IsNullOrWhiteSpace(csv)) return Array.Empty<string>();
                return csv.Split(',', StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chain] no wallets listed: {e.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Authorise, prove, and load what the wallet owns. Returns the address, or empty if the
        /// player declined - which is an ordinary thing to do and leaves the game exactly as it
        /// was, still playable on the local record.
        /// </summary>
        public static async Task<string> ConnectAsync(string walletKey)
        {
            try
            {
                var address = await WebChainBridge.Call("connect", new ChainArgs { wallet = walletKey });
                Address = address ?? "";
                Proven = false;

                if (Connected)
                {
                    await ProveAsync();
                    await WalletInventory.RefreshAsync();
                    Debug.Log($"[Chain] connected {walletKey}: {Short(Address)} (proven={Proven})");
                }

                Changed?.Invoke();
                return Address;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chain] connect({walletKey}) refused or failed: {e.Message}");
                Address = ""; Proven = false;
                Changed?.Invoke();
                return "";
            }
        }

        /// <summary>
        /// Prove control of the address by signing a message - CIP-30 signData, NOT a transaction.
        ///
        /// The distinction is the whole reason ordinary play is trustless here: this signature
        /// authorises nothing, moves nothing and costs nothing. It is what the service checks
        /// before it writes progression or a room layout on this address's behalf.
        ///
        /// A failure is NOT fatal. An unproven wallet still shows its art and still plays; it just
        /// cannot have the service write for it, which is exactly the right consequence.
        /// </summary>
        public static async Task<bool> ProveAsync()
        {
            if (!Connected) return false;
            try
            {
                var nonce = Guid.NewGuid().ToString("N");
                var sig = await WebChainBridge.Call("proveOwnership",
                    new ChainArgs { accountId = Address, datum = nonce });
                Proven = !string.IsNullOrEmpty(sig);
                if (!Proven) Debug.LogWarning("[Chain] ownership proof declined; playing unproven");
                return Proven;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chain] ownership proof failed: {e.Message}");
                Proven = false;
                return false;
            }
        }

        public static async void Disconnect()
        {
            Address = ""; Proven = false;
            WalletInventory.Clear();
            Changed?.Invoke();
            try { await WebChainBridge.Call("disconnect"); }
            catch (Exception e) { Debug.LogWarning($"[Chain] disconnect: {e.Message}"); }
        }

        /// <summary>An address is 100+ characters; the hub has room for neither end of it.</summary>
        public static string Short(string address)
        {
            if (string.IsNullOrEmpty(address)) return "";
            return address.Length <= 18 ? address : $"{address[..10]}...{address[^6..]}";
        }
    }
}
