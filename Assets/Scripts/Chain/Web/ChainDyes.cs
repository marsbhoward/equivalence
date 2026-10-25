using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain.Web
{
    /// <summary>
    /// The DYE economy's chain half (MONETIZATION.md "The plan as of 2026-10-25") - everything else
    /// about dyes is the database's (CharacterItems, the store's Recolour/BreakBox):
    ///
    ///   WITHDRAW   earned dyes out of the database into the wallet as dye TOKENS, to sell. Unlimited
    ///              and PAID BY US; a PFP holder only (nothing earned reaches a wallet without one).
    ///              No player signature - it mints TO them.
    ///   DEPOSIT    dye tokens (bought, traded, or one's own from another character) burned back into
    ///              this character's database count - the player signs (the tokens are theirs).
    ///   BREAK      Diamond box TOKENS burned into charges - the player signs.
    ///
    /// The service builds every transaction (web/service/src/routes/dyes.ts). Each answers null on
    /// success, else why not (a declined wallet dialog included) - nothing throws to the caller.
    /// </summary>
    public static class ChainDyes
    {
        [Serializable] class Reply { public string hash; public string error; }

        static async Task<string> Run(string call, ChainArgs args, string summary)
        {
            var tx = TxLog.Post(TxKind.Dyes, summary, $"profile {args.profileId}\n{args.swatch} {args.count}");
            try
            {
                var json = await WebChainBridge.Call(call, args);
                var reply = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Reply>(json);
                if (reply == null || string.IsNullOrEmpty(reply.hash))
                {
                    TxLog.Fail(tx, reply?.error ?? "no transaction");
                    return reply?.error ?? "the service returned no transaction";
                }
                TxLog.Confirm(tx, reply.hash);
                return null;
            }
            catch (Exception e)
            {
                TxLog.Fail(tx, e.Message);
                Debug.LogWarning($"[Chain] {call} refused: {e.Message}");
                return e.Message;
            }
        }

        public static Task<string> WithdrawAsync(string profileId, string swatch, int count)
            => Run("withdrawDyes", new ChainArgs { profileId = profileId, swatch = swatch, count = count.ToString() },
                   $"Withdrew {count} {swatch}");

        public static Task<string> DepositAsync(string profileId, string swatch, int count)
            => Run("depositDyes", new ChainArgs { profileId = profileId, swatch = swatch, count = count.ToString() },
                   $"Deposited {count} {swatch}");

        public static Task<string> BreakBoxesAsync(string profileId, int count)
            => Run("breakBoxes", new ChainArgs { profileId = profileId, tier = "Diamond", count = count.ToString() },
                   $"Broke {count} Diamond box{(count == 1 ? "" : "es")} into charges");
    }
}
