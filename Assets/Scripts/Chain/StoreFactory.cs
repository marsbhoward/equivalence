using UnityEngine;

namespace Convergence.Chain
{
    /// <summary>
    /// Picks the backend once, so no gameplay code ever branches on where saves live.
    ///
    ///     a WebGL build      the SERVICE (Chain/Service): every browser player is a guest saved by
    ///                        equivalence-service from the first run, graduating onto a wallet when
    ///                        one is proven (the user's call, 2026-10-13). No local-only web tier.
    ///     the Editor, native local JSON - unless DevServiceUrl points at a service to test against.
    ///
    /// THE AUTHORITY SPLIT is unchanged by where saves live. The PLAYER's wallet signs exactly two
    /// kinds of thing - a MESSAGE proving the address, and transactions moving their own tokens
    /// (the PFP, opening boxes). Progression and the room are written by the SERVICE: a player who
    /// could author their own progression could write whatever they liked into it, so
    /// game-authored progression is the anti-cheat. The wallet features themselves (connecting,
    /// the PFP, boxes, inventory) go through the JS connector - <see cref="UsingChain"/>.
    /// </summary>
    public static class StoreFactory
    {
        /// <summary>Forces local JSON even in a WebGL build - for testing the fallback path.</summary>
        public static bool ForceLocal = false;

        /// <summary>
        /// Points the Editor or a native build at a service (`http://localhost:8787` for a local
        /// `npm run dev`). Set BEFORE the stores are first read - GameBootstrap picks them once.
        /// </summary>
        public static string DevServiceUrl = "";

        /// <summary>Saves go to equivalence-service.</summary>
        public static bool UsingService =>
            !ForceLocal && (Application.platform == RuntimePlatform.WebGLPlayer || !string.IsNullOrEmpty(DevServiceUrl));

        /// <summary>The page carries the wallet connector (window.EquivalenceChain) - the wallet's
        /// features are reachable. Says nothing about where saves live.</summary>
        public static bool UsingChain => !ForceLocal && Web.WebChainBridge.Available;

        public static IProfileStore Profiles()
        {
            if (UsingService)
            {
                Debug.Log($"[Convergence] profile store: equivalence-service at {Service.ServiceClient.BaseUrl}");
                return new Service.ServiceProfileStore();
            }
            Debug.Log("[Convergence] profile store: local JSON");
            return new LocalJsonProfileStore();
        }

        public static IAccountStore Accounts()
            => UsingService ? new Service.ServiceAccountStore() : (IAccountStore)new LocalJsonAccountStore();

        /// <summary>The run in flight - beside the character, wherever the character is saved.</summary>
        public static IRunStore Runs()
            => UsingService ? new Service.ServiceRunStore() : (IRunStore)new LocalJsonRunStore();
    }
}
