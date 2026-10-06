using System;
using System.Collections.Generic;

namespace Convergence.Chain
{
    /// <summary>
    /// Where the player has put things in their room.
    ///
    /// PER ACCOUNT, on <see cref="AccountProfile"/>, keyed by wallet address when there is one.
    /// A character is a save file you switch between; the room is the place you switch between
    /// them IN, so it cannot belong to whichever one you happen to be playing.
    ///
    /// Positions are stored, art is not. A placement is a stable showcase KEY plus a spot on the
    /// floor; what that key resolves to is the wallet's business and may legitimately change or
    /// disappear between sessions. A layout referring to art that is no longer there simply
    /// yields fewer trophies rather than a broken room.
    /// </summary>
    [Serializable]
    public class RoomLayout
    {
        /// <summary>
        /// Every placed piece of showcase art - a <see cref="Convergence.Hub.Frame"/>, standing
        /// as an easel or mounted on the wall. One list for both: a Frame decides which it is
        /// from its own (X, Y) at rebuild time (see HubRoom.ModeFor), so there is nothing here that could
        /// disagree with what actually got drawn. This field used to hold only floor pieces, with
        /// wall art in a separate <c>WallFrames</c> list keyed by slot index rather than position
        /// - that list is gone now that a wall frame is a placement like any other and can be
        /// picked back up and moved, same as an easel.
        /// </summary>
        public List<TrophyPlacement> Trophies = new();

        /// <summary>
        /// Moved furniture - the couch, the terminal, the mastery table, the crate, the circle,
        /// the photo booth. Additive field: a save written before the room editor existed loads
        /// with this empty, and every fixture simply keeps its built-in default position, the
        /// same pattern <c>Appearance.Beard</c> and every other field added to a saved profile
        /// after the fact already uses.
        ///
        /// Keyed by a fixed fixture name ("couch", "terminal", ...), NOT by a showcase key - these
        /// are not art, there is nothing to look up, only a spot to remember.
        /// </summary>
        public List<FurniturePlacement> Furniture = new();

        /// <summary>
        /// True once the couch has been taken OUT of the crate. False - in the crate, not in the
        /// room - is the default, and deliberately the one an old save loads as: the couch's spot
        /// became the armoury door, and a player realistically has one character, so switching
        /// is stored rather than cut. Its saved position in <see cref="Furniture"/> survives
        /// either way, the same "hidden, not forgotten" rule the terminal follows.
        /// </summary>
        public bool CouchOut;

        /// <summary>
        /// Stand or move a standing frame. Keyed on its own <see cref="TrophyPlacement.Id"/>, NOT
        /// on the content it happens to be showing - a frame is a place in the room, and what it
        /// shows is free to change without the frame itself moving or being re-created. That is
        /// also what lets two frames show the same piece: nothing here dedupes by content key.
        /// </summary>
        public void PlaceTrophy(string id, string key, float x, float y)
        {
            foreach (var t in Trophies)
            {
                if (t.Id != id) continue;
                t.Key = key; t.X = x; t.Y = y;
                return;
            }
            Trophies.Add(new TrophyPlacement { Id = id, Key = key, X = x, Y = y });
        }

        public void RemoveTrophy(string id) => Trophies.RemoveAll(t => t.Id == id);

        /// <summary>Change what a standing frame shows without moving it or losing its id.</summary>
        public void SetTrophyKey(string id, string key)
        {
            foreach (var t in Trophies) if (t.Id == id) { t.Key = key; return; }
        }

        /// <summary>
        /// Self-heal for a save written before a standing frame had an identity of its own - back
        /// then the content KEY doubled as the identity, so giving every un-migrated entry an Id
        /// equal to its own Key reproduces exactly what it already meant, and every method above
        /// keyed on Id keeps working against it unchanged. Idempotent and cheap; call once after
        /// load, the same discipline <c>Loadout.DropStale</c> and <c>BoardState.DropStale</c> use.
        /// </summary>
        public int NormalizeTrophyIds()
        {
            int n = 0;
            foreach (var t in Trophies)
            {
                if (!string.IsNullOrEmpty(t.Id)) continue;
                t.Id = t.Key;
                n++;
            }
            return n;
        }

        public bool Holds(string key)
        {
            foreach (var t in Trophies) if (t.Key == key) return true;
            return false;
        }

        /// <summary>The saved spot for a fixture, or false if it has never been moved - the
        /// caller keeps its own built-in default in that case.</summary>
        public bool TryGetFurniture(string key, out float x, out float y)
        {
            foreach (var f in Furniture)
            {
                if (f.Key != key) continue;
                x = f.X; y = f.Y;
                return true;
            }
            x = y = 0f;
            return false;
        }

        public void PlaceFurniture(string key, float x, float y)
        {
            foreach (var f in Furniture)
            {
                if (f.Key != key) continue;
                f.X = x; f.Y = y;
                return;
            }
            Furniture.Add(new FurniturePlacement { Key = key, X = x, Y = y });
        }

        /// <summary>
        /// What is on show in the armoury - the weapon rack's pegs and the armour stand's
        /// mannequin.
        ///
        /// Keyed "weaponrack:0" / "armourstand:Torso", so one list serves both fixtures and the
        /// key says which. It is an ITEM ID and nothing else, because the display is purely
        /// cosmetic: there is no durability, no stat and no equipped state to keep in step, so
        /// anything more than the id would be a second copy of something the catalogue already
        /// owns.
        ///
        /// Additive, the same as Furniture above and for the same reason: a save written before
        /// the armoury existed loads with this empty and both fixtures simply stand bare.
        /// </summary>
        public List<GearDisplay> Display = new();

        public string Displayed(string key)
        {
            foreach (var d in Display) if (d.Key == key) return d.ItemId;
            return null;
        }

        /// <summary>A null or empty id CLEARS the spot rather than storing a blank - an empty
        /// entry and a missing one would otherwise be two ways to say "nothing on this peg".</summary>
        public void SetDisplayed(string key, string itemId)
        {
            Display.RemoveAll(d => d.Key == key);
            if (!string.IsNullOrEmpty(itemId))
                Display.Add(new GearDisplay { Key = key, ItemId = itemId });
        }

    }

    [Serializable]
    public class GearDisplay
    {
        public string Key;
        public string ItemId;
    }

    [Serializable]
    public class TrophyPlacement
    {
        /// <summary>
        /// The frame's own identity - a spot in the room, independent of what it is currently
        /// showing. Additive: empty on a save written before frames had one of their own, and
        /// self-healed by <see cref="RoomLayout.NormalizeTrophyIds"/> at load.
        /// </summary>
        public string Id;

        public string Key;
        public float X, Y;
    }

    [Serializable]
    public class FurniturePlacement
    {
        public string Key;
        public float X, Y;
    }
}
