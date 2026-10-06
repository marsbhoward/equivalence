using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Convergence.Chain
{
    /// <summary>One art NFT the connected wallet holds. A COMPONENT a PFP can be assembled from.</summary>
    [Serializable]
    public class OwnedComponent
    {
        /// <summary>
        /// Stable identity - policy+name on Cardano. The same key a RoomLayout placement stores
        /// and the same key a PfpRecipe names, so all three talk about the same thing.
        /// </summary>
        public string Key;

        public string Title;
        public string Collection;

        /// <summary>Which part of a character this can be, if any - "Head", "Weapon", ... Empty means display-only art.</summary>
        public string Slot;

        /// <summary>Where the picture lives. Fetched lazily, never at load - see WalletShowcaseSource.</summary>
        public string ImageUrl;
    }

    [Serializable]
    class ComponentList { public List<OwnedComponent> items = new(); }

    /// <summary>
    /// What the connected wallet actually holds.
    ///
    /// THE GAME GATES ON THIS; THE CHAIN ENFORCES IT. Assembly checks ownership here so the UI
    /// can grey out a part you do not have, and the connector checks it again at mint - the same
    /// split Appearance.Resolve already uses for a stale transmog, and for the same reason: a
    /// cached list can go out of date the moment a token is sold in another tab, so a check that
    /// only happens in the UI is a check that can be wrong.
    ///
    /// Empty is a NORMAL state - no wallet connected, or a wallet holding none of this
    /// collection. The game stays fully playable on placeholder art either way.
    /// </summary>
    public static class WalletInventory
    {
        static List<OwnedComponent> _owned = new();

        public static IReadOnlyList<OwnedComponent> Components => _owned;

        public static int Count => _owned?.Count ?? 0;

        /// <summary>Raised after a refresh, so a picker or the couch can redraw.</summary>
        public static event Action Changed;

        public static bool Owns(string key)
        {
            if (string.IsNullOrEmpty(key) || _owned == null) return false;
            foreach (var c in _owned) if (c.Key == key) return true;
            return false;
        }

        public static bool TryGet(string key, out OwnedComponent found)
        {
            found = null;
            if (_owned == null) return false;
            foreach (var c in _owned) if (c.Key == key) { found = c; return true; }
            return false;
        }

        /// <summary>Everything that can fill the given rig slot - what an assembly picker lists.</summary>
        public static List<OwnedComponent> ForSlot(string slot)
        {
            var list = new List<OwnedComponent>();
            if (_owned == null) return list;
            foreach (var c in _owned)
                if (string.Equals(c.Slot, slot, StringComparison.OrdinalIgnoreCase)) list.Add(c);
            return list;
        }

        /// <summary>
        /// Re-read from the connector. Called on connect and after a mint; cheap enough to call
        /// again whenever an assembly screen opens, since a token can leave the wallet elsewhere.
        /// </summary>
        public static async Task RefreshAsync()
        {
            _owned ??= new List<OwnedComponent>();

            if (!Web.WebChainBridge.Available || !Web.ChainWallet.Connected)
            {
                if (_owned.Count > 0) { _owned.Clear(); Changed?.Invoke(); }
                return;
            }

            try
            {
                var json = await Web.WebChainBridge.Call("listComponents",
                    new Web.ChainArgs { accountId = Web.ChainWallet.Address });

                var parsed = string.IsNullOrEmpty(json)
                    ? new ComponentList()
                    : JsonUtility.FromJson<ComponentList>(json);

                _owned = parsed?.items ?? new List<OwnedComponent>();
                Debug.Log($"[Chain] wallet holds {_owned.Count} component(s)");
            }
            catch (Exception e)
            {
                // Left as it was rather than emptied: a failed refresh must not look like a wallet
                // that suddenly owns nothing, which would grey out every part the player has.
                Debug.LogWarning($"[Chain] inventory refresh failed, keeping {_owned.Count}: {e.Message}");
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Editor/CLI testing path: read the service's own <c>/components</c> endpoint directly
        /// over HTTP, instead of through <see cref="Web.WebChainBridge"/>. The bridge is real
        /// browser JS and does not exist outside a WebGL build, so RefreshAsync above always reads
        /// empty in the Editor - this is the one way to see a real wallet's holdings without one.
        ///
        /// Kept separate from RefreshAsync rather than folded into it as a fallback: the split
        /// this project draws everywhere else is that <c>Chain/Web/</c> is the ONLY part that
        /// knows a browser exists, and every native target reads through the local JSON stores
        /// instead. A silent HTTP fallback here would quietly cross that line for every platform;
        /// gating this on <see cref="Tuning.Testing.DemoWalletAddress"/> and calling it only from
        /// GameBootstrap's own testing branch keeps it something that has to be turned on, not
        /// something a native build could stumble into.
        /// </summary>
        public static async Task RefreshFromServiceAsync(string serviceUrl, string address)
        {
            _owned ??= new List<OwnedComponent>();
            if (string.IsNullOrEmpty(serviceUrl) || string.IsNullOrEmpty(address)) return;

            using var req = UnityWebRequest.Get($"{serviceUrl}/components?address={address}");
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (req.result != UnityWebRequest.Result.Success)
            {
                // Left as it was rather than emptied - see RefreshAsync's own note on why a
                // failed read must not look like a wallet that suddenly owns nothing.
                Debug.LogWarning($"[Chain] service inventory read failed: {req.error} " +
                                 $"(is coalescence-service running at {serviceUrl}?)");
                return;
            }

            var parsed = JsonUtility.FromJson<ComponentList>(req.downloadHandler.text);
            _owned = parsed?.items ?? new List<OwnedComponent>();
            Debug.Log($"[Chain] wallet holds {_owned.Count} component(s), read from {serviceUrl}");
            Changed?.Invoke();
        }

        public static void Clear()
        {
            if (_owned == null || _owned.Count == 0) return;
            _owned.Clear();
            Changed?.Invoke();
        }
    }
}
