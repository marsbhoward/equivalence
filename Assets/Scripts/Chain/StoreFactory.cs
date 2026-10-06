using UnityEngine;

namespace Convergence.Chain
{
    /// <summary>
    /// Picks the backend once, so no gameplay code ever branches on "are we on chain".
    ///
    /// THE AUTHORITY SPLIT, which is the shape everything below this depends on:
    ///
    ///     the PLAYER's wallet    holds the component art and the assembled PFP. Signs exactly
    ///                            two things - a MESSAGE proving the address (no fee, moves
    ///                            nothing) and the PFP mint, an asset going to them.
    ///
    ///     the SERVICE's wallet   writes progression (IProfileStore's three checkpoints) and
    ///                            room layout (IAccountStore). No player signature.
    ///
    /// Progression is service-authored for two reasons and the second is the stronger one. It
    /// spares the player a wallet dialog on every floor - approving transactions constantly is
    /// how a player learns to approve transactions without reading them. But more importantly a
    /// player who can sign their own progression datum can write whatever they like into it, so
    /// game-authored progression is the anti-cheat, not a compromise on decentralisation. What
    /// the player owns is the ASSET; what the game authors is its metadata.
    ///
    /// The whole point of IProfileStore/IAccountStore being interfaces is that this is the only
    /// file that knows there is a choice. In the Editor and on every native target the browser
    /// bridge reports unavailable and the local JSON stores are used, which is what keeps the
    /// existing `unity command eval` workflow working unchanged.
    /// </summary>
    public static class StoreFactory
    {
        /// <summary>
        /// Forces local JSON even in a browser that has a connector. For testing the fallback
        /// path in a real build, where you cannot simply not have a wallet extension installed.
        /// </summary>
        public static bool ForceLocal = false;

        public static bool UsingChain => !ForceLocal && Web.WebChainBridge.Available;

        public static IProfileStore Profiles()
        {
            if (UsingChain)
            {
                Debug.Log("[Convergence] profile store: CIP-68 connector");
                return new Web.ChainProfileStore();
            }
            Debug.Log("[Convergence] profile store: local JSON");
            return new LocalJsonProfileStore();
        }

        public static IAccountStore Accounts()
            => UsingChain ? new Web.ChainAccountStore() : (IAccountStore)new LocalJsonAccountStore();
    }
}
