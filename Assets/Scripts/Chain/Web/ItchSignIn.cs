using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain.Web
{
    /// <summary>
    /// SIGN IN WITH ITCH.IO (2026-11-04, the user's call) - a sign-in without a wallet, for the
    /// players who found the game on itch (web/service/src/itch.ts). It earns the same key-bound
    /// session a wallet does, under the identity <c>itch:&lt;id&gt;</c>: saves, graduating a guest
    /// and the "was there" title (CHAIN.md "Titles") work as they do for a wallet; nothing on chain
    /// does - the service refuses it there, and <see cref="ServiceIdentity.OnWallet"/> stays false.
    ///
    /// The flow LEAVES the page: <see cref="BeginAsync"/> sends the player to itch.io's consent page,
    /// itch returns to /itch/callback, which hands the token back to /play/ - so the game boots again
    /// and <see cref="FinishPendingAsync"/> trades it. Offered only where the service names a client
    /// id (<see cref="RefreshAsync"/>): the app has one registered callback, the showcase's.
    /// </summary>
    public static class ItchSignIn
    {
        /// <summary>The key the connector remembers an itch sign-in under.</summary>
        public const string Key = "itch";
        public const string Prefix = "itch:";

        /// <summary>Whether an identity is an itch.io sign-in rather than a wallet's address.</summary>
        public static bool IsItch(string address) => !string.IsNullOrEmpty(address) && address.StartsWith(Prefix);

        public static string ClientId { get; private set; } = "";
        public static bool Offered => WebChainBridge.Available && !string.IsNullOrEmpty(ClientId);

        public static string Address { get; private set; } = "";
        public static string Name { get; private set; } = "";
        public static string Session { get; private set; } = "";
        public static bool SignedIn => !string.IsNullOrEmpty(Session);

        [Serializable] class Who { public string address; public string name; }

        /// <summary>Asks the service whether it offers itch sign-in (its client id).</summary>
        public static async Task RefreshAsync()
        {
            if (!WebChainBridge.Available) return;
            try { ClientId = await WebChainBridge.Call("itchConfig") ?? ""; }
            catch (Exception e) { ClientId = ""; Debug.LogWarning($"[Itch] config: {e.Message}"); }
        }

        /// <summary>Off to itch.io. The page is left - this never really returns.</summary>
        public static async Task BeginAsync()
        {
            if (!Offered) return;
            await WebChainBridge.Call("itchBegin", new ChainArgs { text = ClientId });
        }

        /// <summary>A token itch.io handed back through /itch/callback, traded for a session. Returns
        /// the identity, or empty when there was none or the service refused it (<paramref name="error"/>).</summary>
        public static async Task<(string Address, string Error)> FinishPendingAsync()
        {
            if (!WebChainBridge.Available) return ("", "");
            string token;
            try { token = await WebChainBridge.Call("itchPending") ?? ""; }
            catch { return ("", ""); }
            if (string.IsNullOrEmpty(token)) return ("", "");
            try
            {
                await AdoptAsync(await WebChainBridge.Call("itchSignIn", new ChainArgs { text = token }));
                if (SignedIn) Debug.Log($"[Itch] signed in as {Name} ({Address})");
                return (Address, SignedIn ? "" : "itch.io did not sign you in.");
            }
            catch (Exception e)
            {
                Clear();
                Debug.LogWarning($"[Itch] sign-in refused: {e.Message}");
                return ("", e.Message);
            }
        }

        /// <summary>A QUIET reconnect at boot, as ChainWallet.ResumeAsync is for a wallet.</summary>
        public static async Task<string> ResumeAsync()
        {
            if (!WebChainBridge.Available) return "";
            try
            {
                await AdoptAsync(await WebChainBridge.Call("itchResume"));
                return Address;
            }
            catch (Exception e)
            {
                Clear();
                Debug.LogWarning($"[Itch] resume failed: {e.Message}");
                return "";
            }
        }

        /// <summary>Signs out here: the remembered session and its key go (the connector's disconnect).</summary>
        public static async Task SignOutAsync()
        {
            try { await WebChainBridge.Call("disconnect"); }
            catch (Exception e) { Debug.LogWarning($"[Itch] sign out: {e.Message}"); }
            Clear();
        }

        static async Task AdoptAsync(string json)
        {
            var who = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Who>(json);
            if (who == null || !IsItch(who.address)) { Clear(); return; }
            // The connector holds the session itself and signs every request with it; the game only
            // needs to know there is one (ServiceIdentity) - the same call a wallet's proof makes.
            Session = await WebChainBridge.Call("session") ?? "";
            Address = string.IsNullOrEmpty(Session) ? "" : who.address;
            Name = string.IsNullOrEmpty(who.name) ? "itch.io player" : who.name;
        }

        static void Clear() { Address = ""; Name = ""; Session = ""; }
    }
}
