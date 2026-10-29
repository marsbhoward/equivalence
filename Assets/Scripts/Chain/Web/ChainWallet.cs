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
    /// the page put something at window.EquivalenceChain. Outside a browser build Available is
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

        /// <summary>The service's session for the proven address (/auth/prove) - what the saves
        /// carry once a guest graduates onto this wallet (Chain/Service). Empty until proven.</summary>
        public static string Session { get; private set; } = "";

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

        /// <summary>The two things a first connect asks the wallet for, told to the game before each
        /// so it can say what the popup is about to ask (CIP-30 has no way to ask for both at once).</summary>
        public enum Step { Access, Sign, Done }

        /// <summary>
        /// Authorise, prove, and load what the wallet owns. Returns the address, or empty if the
        /// player declined - which is an ordinary thing to do and leaves the game exactly as it
        /// was, still playable on the local record. <paramref name="onStep"/> hears each wallet
        /// popup before it opens; a session this browser already holds for the address skips the
        /// signature, and its step is never announced.
        /// </summary>
        public static async Task<string> ConnectAsync(string walletKey, Action<Step> onStep = null)
        {
            try
            {
                // Switching wallets: the old one signs out here first, so its session and key go.
                if (Connected) await DisconnectAsync(everywhere: false);

                onStep?.Invoke(Step.Access);
                var address = await WebChainBridge.Call("connect", new ChainArgs { wallet = walletKey });
                Address = address ?? "";
                Proven = false;
                Session = "";

                if (Connected)
                {
                    if (!await RememberedAsync()) onStep?.Invoke(Step.Sign);
                    await ProveAsync();
                    onStep?.Invoke(Step.Done);
                    await WalletInventory.RefreshAsync();
                    Debug.Log($"[Chain] connected {walletKey}: {Short(Address)} (proven={Proven})");
                }

                Changed?.Invoke();
                return Address;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chain] connect({walletKey}) refused or failed: {e.Message}");
                Address = ""; Proven = false; Session = "";
                onStep?.Invoke(Step.Done);
                Changed?.Invoke();
                return "";
            }
        }

        [Serializable]
        class Resumed { public string wallet; public string address; }

        /// <summary>
        /// A QUIET reconnect at boot: the wallet signed in on an earlier visit, its session still
        /// holds, and the wallet already lets the site in - so no popup at all. Returns the address,
        /// or empty, in which case the player connects at the booth as usual.
        /// </summary>
        public static async Task<string> ResumeAsync()
        {
            if (!Available) return "";
            try
            {
                var json = await WebChainBridge.Call("resume");
                var r = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Resumed>(json);
                if (r == null || string.IsNullOrEmpty(r.address)) return "";
                Address = r.address;
                Session = await WebChainBridge.Call("session") ?? "";
                Proven = !string.IsNullOrEmpty(Session);
                if (!Proven) { Address = ""; return ""; }
                await WalletInventory.RefreshAsync();
                Debug.Log($"[Chain] resumed {r.wallet}: {Short(Address)}");
                Changed?.Invoke();
                return Address;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chain] resume failed: {e.Message}");
                Address = ""; Proven = false; Session = "";
                return "";
            }
        }

        /// <summary>Whether this browser already holds a session for the connected address - the
        /// signature step is skipped, so the game does not announce it.</summary>
        static async Task<bool> RememberedAsync()
        {
            try { return await WebChainBridge.Call("remembered", new ChainArgs { accountId = Address }) == "true"; }
            catch { return false; }
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
                else Session = await WebChainBridge.Call("session") ?? "";
                return Proven;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chain] ownership proof failed: {e.Message}");
                Proven = false;
                return false;
            }
        }

        /// <summary>
        /// Signs out. Here: the remembered session and the key it is bound to are dropped, so it
        /// cannot be used again. <paramref name="everywhere"/>: the service ends every session of
        /// this address first, on every device.
        /// </summary>
        public static async Task DisconnectAsync(bool everywhere)
        {
            try { await WebChainBridge.Call(everywhere ? "signOutEverywhere" : "disconnect"); }
            catch (Exception e) { Debug.LogWarning($"[Chain] {(everywhere ? "sign out everywhere" : "disconnect")}: {e.Message}"); }
            Address = ""; Proven = false; Session = "";
            WalletInventory.Clear();
            Changed?.Invoke();
        }

        /// <summary>An address is 100+ characters; the hub has room for neither end of it.</summary>
        public static string Short(string address)
        {
            if (string.IsNullOrEmpty(address)) return "";
            return address.Length <= 18 ? address : $"{address[..10]}...{address[^6..]}";
        }
    }
}
