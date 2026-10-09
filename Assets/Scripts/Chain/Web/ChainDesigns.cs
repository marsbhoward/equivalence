using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Convergence.Art.Gear;

namespace Convergence.Chain.Web
{
    /// <summary>
    /// Opening Diamond / Black Diamond BOXES against the service - the browser build's Forge path.
    ///
    /// A box is a TOKEN in the wallet (minted there at extraction), so opening one is a
    /// transaction the player approves - the one other thing besides the PFP they ever sign. The
    /// service builds it, pays its fee and keeps its own witness; the connector has the wallet
    /// co-sign and submits. 1-10 boxes per approval.
    ///
    /// THE SERVER ROLLS, before the signature: this sends only WHICH boxes (tier and count -
    /// design boxes always open at random). Declining and opening again shows the same designs. The
    /// pieces that come back are the records to add - design and instance ids are the server's.
    /// The game plays its opening animation regardless of chain time; the reveal waits on this.
    ///
    /// Rows in the terminal like any checkpoint (TxKind.DesignMint).
    /// </summary>
    public static class ChainDesigns
    {
        [Serializable]
        public class Piece
        {
            public string instanceId;
            public string design;
            public string name;
            public string slot;
            public string unit;
            public string reference;
        }

        [Serializable]
        public class Result
        {
            public string hash;
            public string tier;
            public List<Piece> pieces = new();
        }

        [Serializable]
        class Balance
        {
            public string profileId;
            public Boxes boxes;
        }

        [Serializable]
        class Boxes
        {
            public int Diamond;
            public int BlackDiamond;
        }

        /// <summary>Throws on any refusal - not enough boxes, no PFP, the player declining the
        /// wallet dialog - with the reason, after failing the terminal row.</summary>
        public static async Task<Result> OpenAsync(string profileId, LootTier tier, int count = 1)
        {
            string what = $"{count} {tier} box{(count == 1 ? "" : "es")}";
            var tx = TxLog.Post(TxKind.DesignMint, $"Opened {what}",
                                $"profile {profileId}\ntier {tier}\ncount {count}");
            try
            {
                var json = await WebChainBridge.Call("openBoxes", new ChainArgs
                {
                    profileId = profileId,
                    tier = tier.ToString(),
                    count = count.ToString(),
                });
                var result = JsonUtility.FromJson<Result>(json);
                // An empty hash is a write that did not happen - never confirm it.
                if (result == null || string.IsNullOrEmpty(result.hash) || result.pieces == null || result.pieces.Count == 0)
                    throw new Exception("the service returned no open");
                TxLog.Confirm(tx, result.hash);
                return result;
            }
            catch (Exception e)
            {
                TxLog.Fail(tx, e.Message);
                throw;
            }
        }

        /// <summary>The boxes the WALLET holds, written over the profile's display copy. Leaves the
        /// profile alone if the read fails - a failed read is not an empty wallet.</summary>
        public static async Task SyncBalanceAsync(CharacterProfile profile)
        {
            try
            {
                var json = await WebChainBridge.Call("designBoxes", new ChainArgs { profileId = profile.ProfileId });
                var b = JsonUtility.FromJson<Balance>(json);
                if (b?.boxes == null) return;
                profile.Boxes.Diamond = b.boxes.Diamond;
                profile.Boxes.BlackDiamond = b.boxes.BlackDiamond;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Chain] design-box balance unavailable: {e.Message}");
            }
        }
    }
}
