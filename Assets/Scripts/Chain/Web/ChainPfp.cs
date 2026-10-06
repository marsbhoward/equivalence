using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain.Web
{
    /// <summary>
    /// The PFP: one per CHARACTER, held in the player's own wallet, and the only thing in this
    /// game they ever sign a transaction for.
    ///
    /// A TROPHY, NOT A LIVE EQUIP VISUALISATION. The datum is CIP-68 and genuinely updatable -
    /// what it may never do is update itself. Every take is a deliberate act with a fee behind
    /// it, so the portrait says "this is the build I want to be known for" rather than "this is
    /// what I happen to be wearing". Nothing in the game may reach <see cref="TakeAsync"/> except
    /// a player pressing the button for it: no auto-take on a floor clear, no sync on equip.
    ///
    /// WHO SIGNS WHAT, and the two halves are deliberately in different hands:
    ///
    ///     the PLAYER chooses WHEN     - they hold the token, they pay the fee, they press it
    ///     the SERVICE vouches for WHAT - MaxFloor is attested, never taken from the client
    ///
    /// Which is why the reference token cannot simply be locked (immutable), and cannot simply be
    /// handed to the player either - a player who can spend that UTxO alone can write any depth
    /// they like into their own trophy. It wants BOTH signatures on an update.
    /// </summary>
    public static class ChainPfp
    {
        /// <summary>Raised after a successful take, so the couch and the sheet redraw.</summary>
        public static event Action Changed;

        /// <summary>
        /// Take this character's portrait - minting the first time, updating its CIP-68 datum
        /// every time after.
        ///
        /// ONE ENTRY POINT rather than two, because the caller has no reason to care which
        /// happened and a UI that had to ask first could get the answer wrong. Refusals run
        /// cheapest-first: no wallet, unreadable chain, nothing changed, parts not owned.
        /// </summary>
        public static async Task<string> TakeAsync(CharacterProfile profile)
        {
            if (!ChainWallet.Connected)
                throw new ChainException("connect a wallet before taking a portrait");

            // Read FIRST, and let a failure throw. Answering "none" on a transport error would
            // MINT a second portrait for a character that already has one, which is the only
            // outcome in this file that cannot be undone.
            var existing = await CurrentAsync(profile.ProfileId);

            var recipe = PfpRecipe.From(profile);
            recipe.Revision = (existing?.Revision ?? 0) + 1;
            recipe.TakenAtUtc = DateTime.UtcNow.ToString("u");

            // A re-take that says nothing new is a fee paid for a duplicate datum.
            if (recipe.SaysTheSameAs(existing))
                throw new ChainException(
                    $"nothing has changed since take {existing.Revision} on {existing.TakenAtUtc}");

            // Freshest possible read before spending the player's attention on a dialog.
            await WalletInventory.RefreshAsync();

            var missing = recipe.MissingComponents();
            if (missing.Count > 0)
                throw new ChainException(
                    $"not yours to assemble - missing {missing.Count}: {string.Join(", ", missing)}");

            bool first = existing == null;
            var tx = TxLog.Post(TxKind.Unlock,
                first ? $"Portrait taken - {recipe.Name}"
                      : $"Portrait re-taken - {recipe.Name}, take {recipe.Revision}",
                $"profile {profile.ProfileId}\ncomponents {recipe.Components.Count}\n" +
                $"floor {recipe.MaxFloor}\nto {ChainWallet.Short(ChainWallet.Address)}");

            try
            {
                var hash = await WebChainBridge.Call(first ? "assemblePfp" : "updatePfp", new ChainArgs
                {
                    profileId = profile.ProfileId,
                    accountId = ChainWallet.Address,
                    datum = JsonUtility.ToJson(recipe),
                });

                if (string.IsNullOrEmpty(hash))
                {
                    TxLog.Fail(tx, "connector returned no transaction hash");
                    throw new ChainException("the portrait was not submitted");
                }

                TxLog.Confirm(tx, hash);
                Debug.Log($"[Chain] portrait take {recipe.Revision} for {profile.ProfileId} " +
                          $"(floor {recipe.MaxFloor}, {recipe.Components.Count} components): {hash}");

                // Only a FIRST take puts a new token in the wallet; an update touches the
                // reference datum and leaves the player's holdings alone.
                if (first) await WalletInventory.RefreshAsync();
                Changed?.Invoke();
                return hash;
            }
            catch (ChainException) { throw; }
            catch (Exception e)
            {
                // A declined dialog lands here and is NOT an error state - the player said no,
                // nothing was spent and the existing portrait is untouched.
                TxLog.Fail(tx, e.Message);
                Debug.LogWarning($"[Chain] portrait refused: {e.Message}");
                throw new ChainException(e.Message);
            }
        }

        /// <summary>The portrait this character has, or null if it has none.</summary>
        public static async Task<PfpRecipe> CurrentAsync(string profileId)
        {
            try
            {
                var json = await WebChainBridge.Call("currentPfp", new ChainArgs { profileId = profileId });
                if (string.IsNullOrEmpty(json)) return null;
                var r = JsonUtility.FromJson<PfpRecipe>(json);
                return string.IsNullOrEmpty(r?.ProfileId) ? null : r;
            }
            catch (Exception e)
            {
                // Deliberately NOT null. Null means "no portrait" and sends the caller to the mint
                // path, so a transport failure must refuse to answer rather than answer wrongly.
                Debug.LogWarning($"[Chain] currentPfp({profileId}): {e.Message}");
                throw new ChainException($"could not read the existing portrait: {e.Message}");
            }
        }

        /// <summary>
        /// What a take would change, for the confirm screen - the whole reason this reads as a
        /// decision rather than a sync. A player about to pay a fee has to see what they are
        /// paying to change, before a wallet dialog opens rather than afterwards in an explorer.
        /// </summary>
        public static async Task<string> DescribeAsync(CharacterProfile profile)
        {
            var next = PfpRecipe.From(profile);
            var missing = next.MissingComponents();
            if (missing.Count > 0)
                return $"CANNOT TAKE: you do not own {string.Join(", ", missing)}";

            PfpRecipe existing;
            try { existing = await CurrentAsync(profile.ProfileId); }
            catch (ChainException e) { return $"cannot check the existing portrait: {e.Message}"; }

            if (existing == null)
                return $"take \"{next.Name}\" - floor {next.MaxFloor}, " +
                       $"{next.Components.Count} pieces worn";

            if (next.SaysTheSameAs(existing))
                return $"nothing has changed since take {existing.Revision} on {existing.TakenAtUtc}";

            var changes = new System.Collections.Generic.List<string>();
            if (existing.Name != next.Name) changes.Add($"name {existing.Name} -> {next.Name}");
            if (existing.MaxFloor != next.MaxFloor) changes.Add($"floor {existing.MaxFloor} -> {next.MaxFloor}");
            if (existing.Gear != next.Gear) changes.Add("gear");
            if (existing.Look != next.Look) changes.Add("appearance");

            return $"take {existing.Revision} -> {existing.Revision + 1}: {string.Join(", ", changes)}";
        }
    }
}
