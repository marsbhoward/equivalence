using System;
using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Chain
{
    /// <summary>
    /// The parts a PFP is assembled FROM, and the look they compose into.
    ///
    /// The PFP is the one thing in this design that lives in the PLAYER's wallet, and the rule
    /// is that it can only be assembled from components they already own. So the recipe is not
    /// decoration on the mint - it IS the mint's argument, and every key in it has to resolve to
    /// a token the wallet holds or the connector refuses.
    ///
    /// IT IS A TROPHY, NOT A LIVE EQUIP VISUALISATION, and the difference is the TRIGGER rather
    /// than the mechanism. The datum is CIP-68 and updatable; what it is never allowed to do is
    /// update ITSELF. A portrait that followed the loadout would be a status bar, changing every
    /// time the player tried a different weapon, and nothing that churns on every experiment is
    /// worth looking at. Taking one is a deliberate act with a fee behind it: this is the build I
    /// want to be known for, now.
    ///
    /// So nothing in the game may call the take path except a player pressing the button for it.
    /// No auto-take on a floor clear, no "sync" on equip, no refresh on load.
    ///
    /// It records the LOOK as well as the parts. Two characters can hold the same components and
    /// wear them differently, and a PFP that only listed its ingredients could not tell them
    /// apart - or reproduce either one later.
    /// </summary>
    [Serializable]
    public class PfpRecipe
    {
        /// <summary>The character this pictures. One PFP per character, not per wallet.</summary>
        public string ProfileId;

        /// <summary>Component keys, in slot order. Every one must be owned.</summary>
        public List<string> Components = new();

        /// <summary>The composed appearance, serialised - skin, hair, eyes, brows, expression.</summary>
        public string Look;

        /// <summary>Serialised loadout, so the picture can be rebuilt exactly rather than approximated.</summary>
        public string Gear;

        /// <summary>
        /// The character's name, as they chose it. A portrait of an anonymous figure is stock art.
        /// </summary>
        public string Name;

        /// <summary>
        /// Deepest floor cleared at the moment of this update - the one field the SERVICE
        /// attests to rather than the player. It is why this datum has two authors, and why a
        /// validator that trustlessly checked the gear while taking this number on trust would
        /// only be securing half the record.
        /// </summary>
        public int MaxFloor;

        /// <summary>
        /// When this take was made, UTC. A trophy is OF A MOMENT, so the moment has to be on the
        /// record - otherwise a portrait of a build from six months ago is indistinguishable from
        /// one taken this morning.
        /// </summary>
        public string TakenAtUtc;

        /// <summary>
        /// How many times this trophy has been re-taken, starting at 1.
        ///
        /// Not bookkeeping - it is part of what the trophy SAYS. A character on its seventh take
        /// has had seven distinct eras, and the chain keeps every one of them. It is also what
        /// makes concurrent updates orderable: reject a revision that is not exactly one past
        /// what is on chain, or two takes in flight silently overwrite each other.
        /// </summary>
        public int Revision;

        /// <summary>
        /// Built from what the character is actually WEARING, not from everything owned.
        ///
        /// A PFP is a portrait of a character, so the parts are the equipped ones. The relic
        /// socket is deliberately included even though it draws nothing - it is worn, it is a
        /// black-diamond weapon, and a portrait that silently dropped it would misrepresent what
        /// the player is carrying.
        /// </summary>
        public static PfpRecipe From(CharacterProfile profile)
        {
            var r = new PfpRecipe
            {
                ProfileId = profile.ProfileId,
                Look = JsonUtility.ToJson(profile.Look),
                Gear = JsonUtility.ToJson(profile.Gear),
                Name = profile.DisplayName,
                MaxFloor = profile.BestFloor,
            };

            foreach (var entry in profile.Gear.Equipped)
            {
                if (string.IsNullOrEmpty(entry.ItemId)) continue;
                if (r.Components.Contains(entry.ItemId)) continue;   // one item may paint two layers
                r.Components.Add(entry.ItemId);
            }

            return r;
        }

        /// <summary>
        /// Which parts the wallet does NOT hold. Empty means this recipe can be minted.
        ///
        /// A LIST rather than a bool, because "you cannot assemble this" is useless to a player
        /// standing in front of a character wearing twelve things - they need to be told which
        /// one, and there is usually more than one while a collection is being built up.
        /// </summary>
        public List<string> MissingComponents()
        {
            var missing = new List<string>();
            foreach (var key in Components)
                if (!WalletInventory.Owns(key)) missing.Add(key);
            return missing;
        }

        public bool FullyOwned => MissingComponents().Count == 0;

        /// <summary>
        /// Whether a take would actually say anything new.
        ///
        /// A trophy re-taken with nothing changed is a fee paid for an identical datum, so the
        /// take path refuses it rather than letting the player buy a duplicate of what they
        /// already have. Compares what the portrait DISPLAYS - not Revision or TakenAtUtc, which
        /// differ by construction on every take.
        /// </summary>
        public bool SaysTheSameAs(PfpRecipe other)
        {
            if (other == null) return false;
            if (Name != other.Name || MaxFloor != other.MaxFloor) return false;
            if (Look != other.Look || Gear != other.Gear) return false;
            if (Components.Count != other.Components.Count) return false;
            for (int i = 0; i < Components.Count; i++)
                if (Components[i] != other.Components[i]) return false;
            return true;
        }

        public override string ToString()
            => $"PFP {ProfileId}: {Components.Count} components, {MissingComponents().Count} missing";
    }
}
