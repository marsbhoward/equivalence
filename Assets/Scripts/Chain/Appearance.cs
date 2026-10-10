using System;
using Convergence.Art.Gear;

namespace Convergence.Chain
{
    /// <summary>
    /// How the character LOOKS, independent of what it is wearing for combat.
    ///
    /// This is the transmutation circle's output, and it is the NFT-facing half of the profile:
    /// the reference datum records cumulative progression, and this is the part of it a holder
    /// actually sees rendered in a wallet or a marketplace. That is exactly why it is separated
    /// from <see cref="CharacterProfile.Gear"/> - the stat loadout changes every run as loot
    /// arrives, and an image that churned with it would be worthless as an identity.
    ///
    /// EVERY FIELD IS A STABLE STRING KEY, never an index or an enum.
    ///
    /// The existing loadout already learned this lesson the hard way: JsonUtility serialises an
    /// enum as an int, so reordering GearSlot silently re-pointed every saved entry at a
    /// different slot (see Loadout.DropStale). Appearance has the same exposure and worse
    /// consequences - a reordered hair enum would not throw, it would just quietly give someone a
    /// different face than the one their token depicts. Unknown keys fall back to a default
    /// rather than failing, so a datum written by a newer client still renders on an older one.
    /// </summary>
    [Serializable]
    public class Appearance
    {
        /// <summary>
        /// Slot -> the item whose ART is worn there, overriding whatever is equipped for stats.
        /// An empty slot here means "show what is actually equipped", which is why this cannot
        /// simply be a second loadout: absence has to mean pass-through, not bare skin.
        /// </summary>
        public Loadout Transmog = new();

        public string Skin = "fair";
        public string Hair = "ash";
        public string Eyes = "slate";
        public string HairStyle = "crop";
        public string Expression = "neutral";
        public string Beard = "none";

        /// <summary>
        /// Eyebrows. ADDITIVE to saved profiles: JsonUtility leaves a missing field at its
        /// declared default, so every profile written before this loads as "natural" - which is an
        /// empty grid meaning "whatever the expression already draws" - and renders exactly as it
        /// did. Nothing needs migrating. See the note on Appearance.Beard.
        /// </summary>
        public string Brows = "natural";

        /// <summary>
        /// Brow colour, as a key into the HAIR swatches. EMPTY MEANS "match the hair", which is
        /// the default and the right one: changing hair colour should carry the brows with it
        /// unless the player has deliberately said otherwise. Also additive - an older profile
        /// loads empty and matches, exactly as it rendered before.
        /// </summary>
        public string BrowColour = "";

        /// <summary>
        /// The loadout to DRAW: transmogged art where a slot has been overridden, the real item
        /// everywhere else.
        ///
        /// Nothing about stats, durability or power passes through here - callers that care about
        /// those must keep reading the equipped loadout. Keeping the two apart at the type level
        /// is what stops a transmog from ever being worth power.
        /// </summary>
        public Loadout Resolve(Loadout equipped)
        {
            var visual = new Loadout();
            // Selectable, not Transmoggable: the relic is walked so its own art (a saya, a pouch)
            // still DRAWS from what is equipped - it is Allowed that refuses to honour a disguise
            // on it, not this loop. Dropping the slot here instead would have made the relic
            // invisible rather than undisguisable.
            foreach (var slot in GearSlots.Selectable)
            {
                var over = Transmog.Get(slot);
                bool use = !string.IsNullOrEmpty(over) && Allowed(slot, over, equipped);
                visual.Set(slot, use ? over : equipped.Get(slot));
            }
            return visual;
        }

        /// <summary>
        /// A WEAPON may only be disguised as another weapon of its own class.
        ///
        /// The class decides how the thing is HELD: `TwoHanded` brings the off hand across onto
        /// the same hilt and permutes the layer stack, while a disc uses a one-handed grip with the
        /// fist showing through the ring. Cross them and the character two-hands a chakram, or
        /// holds a greatsword by nothing - the grip comes from one item and the picture from
        /// another.
        ///
        /// Checked HERE and not only where the disguise is chosen, because a valid one can go
        /// stale on its own: skin your discs as other discs, then equip a greatsword, and nobody
        /// touched the transmog but it now describes a grip you are not using. Resolving falls
        /// back to the real weapon instead, so the mismatch simply never draws.
        ///
        /// A RELIC MAY NOT BE DISGUISED AT ALL, which is a different rule with a harder edge. A
        /// relic's content is its FINISHER, so an override on that slot would be a picture
        /// deciding a mechanic - and a disguise must never be worth anything the game grants. What
        /// is equipped decides, and it decides both halves. Refused HERE as well as omitted from
        /// the transmog grid (GearSlots.Transmoggable) for the same reason the weapon rule is
        /// checked in both places: a save written while the relic tab still existed still holds
        /// its override, and this is what makes that override inert rather than a live cheat. It
        /// self-heals by refusal, exactly as a stale weapon skin does, so nothing needs migrating.
        ///
        /// Every other slot is unrestricted. A helmet is a helmet.
        /// </summary>
        static bool Allowed(GearSlot slot, string overrideId, Loadout equipped)
        {
            if (slot == GearSlot.Relic) return false;
            if (slot != GearSlot.Weapon) return true;

            var skin = GearCatalog.Get(overrideId);
            var held = GearCatalog.Get(equipped.Get(GearSlot.Weapon));
            if (skin == null || held == null) return true;   // nothing to disagree about

            return skin.Class == held.Class && skin.TwoHanded == held.TwoHanded;
        }

        /// <summary>
        /// True when this slot is actually SHOWING something other than what is equipped.
        ///
        /// Asks the same question <see cref="Resolve"/> does, so the loadout screen cannot label a
        /// slot "(disguised)" while the disguise is being refused for a class mismatch.
        /// </summary>
        /// A perception that LOOKS LIKE the worn piece is not one (the user's call, 2026-10-09):
        /// nothing on the character changes, so nothing may say it is perceived as something else.
        public bool IsTransmogged(GearSlot slot, Loadout equipped)
        {
            var over = Transmog.Get(slot);
            return !string.IsNullOrEmpty(over) && Allowed(slot, over, equipped) &&
                   !SameLook(GearCatalog.Get(over), GearCatalog.Get(equipped.Get(slot)));
        }

        /// <summary>
        /// Whether two pieces draw the same picture: the same item, the same DESIGN (a minted
        /// instance wears its design's art), or neither has any art at all.
        /// </summary>
        public static bool SameLook(GearItem a, GearItem b)
        {
            if (a == null || b == null) return false;
            if (a.ItemId == b.ItemId || LookId(a) == LookId(b)) return true;
            return (a.Layers == null || a.Layers.Length == 0) && (b.Layers == null || b.Layers.Length == 0);
        }

        static string LookId(GearItem item) => string.IsNullOrEmpty(item.DesignId) ? item.ItemId : item.DesignId;

        /// <summary>True when this slot HOLDS a disguise, whether or not it is being honoured.</summary>
        public bool IsTransmogged(GearSlot slot) => !string.IsNullOrEmpty(Transmog.Get(slot));
    }
}
