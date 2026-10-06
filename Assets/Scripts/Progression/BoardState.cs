using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.Progression
{
    /// <summary>
    /// What one character has actually bought, across all four element boards.
    ///
    /// FOUR BOARDS, ONE DESIGN. <see cref="MasteryBoard"/> is a single node layout; this stores
    /// unlocks scoped by element, so Fire's copy and Water's copy of the same node are separate
    /// purchases. Ids are stored as "Fire:v2.tempo.speed.3".
    ///
    /// BOARDS ARE ACTIVE ONLY FOR THE ELEMENT BEING PLAYED, which is load-bearing rather than
    /// flavour: global XP funds every board independently (spending in Water costs Earth nothing),
    /// so an always-on bonus would stack four ways the moment a player filled all four.
    ///
    /// What a board gives is read three ways: <see cref="Points"/> (gear's stat language, joined
    /// to gear before the character curve bends it), <see cref="Extra"/> (the board-only stats -
    /// lifesteal, status power, Rift capacity) and <see cref="Has"/> (the rules - see BoardEffects).
    /// </summary>
    public class BoardState
    {
        readonly MasteryProfile _profile;

        // Per-element totals, rebuilt when something is bought or the element asked about changes.
        StatPercents _points = new();
        readonly Dictionary<BoardStat, float> _extras = new();
        readonly HashSet<Notable> _notables = new();
        ElementType _computedFor;
        bool _dirty = true;

        public BoardState(MasteryProfile profile) => _profile = profile;

        public MasteryProfile Profile => _profile;

        public void Invalidate() => _dirty = true;

        static string Key(ElementType e, string nodeId) => $"{e}:{nodeId}";

        public bool IsUnlocked(ElementType e, string nodeId)
            => _profile.Unlocked.Contains(Key(e, nodeId));

        /// <summary>
        /// Whether a node is on this element's board at all. Only the Rebis can be absent: it is
        /// hidden for an element with no second ability, since it would offer a choice of one.
        /// </summary>
        public static bool Visible(ElementType e, MasteryNode node)
            => node != null && (node.Kind != NodeKind.Rebis || ElementInfo.SecondAbilityName(e) != null);

        // ---------------------------------------------------------------- totals

        void Recompute(ElementType element)
        {
            if (!_dirty && element == _computedFor) return;

            _points = new StatPercents();
            _extras.Clear();
            _notables.Clear();
            foreach (var node in MasteryBoard.All)
            {
                if (!IsUnlocked(element, node.Id)) continue;
                if (node.Stat != StatKind.None) GearRoller.Add(_points, node.Stat, node.Value);
                if (node.Extra != BoardStat.None)
                {
                    _extras.TryGetValue(node.Extra, out var cur);
                    _extras[node.Extra] = cur + node.Value;
                }
                if (node.Notable != Notable.None) _notables.Add(node.Notable);
            }
            _computedFor = element;
            _dirty = false;
        }

        /// <summary>
        /// The board's stat points for the element being played, in gear's language - added to the
        /// loadout's before the character curve, exactly as gear is. Range's secondary is Cleave on
        /// a blade or disc and Pierce on a bow, so the weapon class is asked for.
        /// </summary>
        public StatPercents Points(ElementType element, WeaponClass weapon = WeaponClass.Greatsword)
        {
            Recompute(element);
            var p = new StatPercents();
            p.Add(_points);
            if (weapon == WeaponClass.Bow && p.Cleave != 0f)
            {
                p.Pierce += p.Cleave;
                p.Cleave = 0f;
            }
            return p;
        }

        /// <summary>A board-only stat's total for the element being played.</summary>
        public float Extra(ElementType element, BoardStat stat)
        {
            Recompute(element);
            return _extras.TryGetValue(stat, out var v) ? v : 0f;
        }

        /// <summary>Whether the element's board owns the node carrying this rule.</summary>
        public bool Has(ElementType element, Notable notable)
        {
            Recompute(element);
            return _notables.Contains(notable);
        }

        /// <summary>Every rule the element's board owns - what BoardEffects is bound with.</summary>
        public HashSet<Notable> Notables(ElementType element)
        {
            Recompute(element);
            return new HashSet<Notable>(_notables);
        }

        /// <summary>
        /// Whether this element's board offers the CHOICE of a second ability: the element has one
        /// built, and the Rebis is owned. False otherwise, whatever was bought.
        /// </summary>
        public bool HasSecondAbility(ElementType element)
            => ElementInfo.SecondAbilityName(element) != null && Has(element, Notable.Rebis);

        /// <summary>Whether the release button fires the SECOND ability instead of the first. A
        /// stored choice counts only while the Rebis is still owned.</summary>
        public bool UsesSecondAbility(ElementType element)
            => HasSecondAbility(element) && Chosen.Contains(element.ToString());

        // Null-guarded: a profile deserialised from an older save may not carry the list.
        List<string> Chosen => _profile.SecondAbilityChosen ??= new List<string>();

        /// <summary>Switch which ability the release fires. Refused (false) with nothing to switch
        /// to. Free and reversible - it is a loadout choice, not a purchase.</summary>
        public bool ToggleSecondAbility(ElementType element)
        {
            if (!HasSecondAbility(element)) return false;
            string key = element.ToString();
            if (!Chosen.Remove(key)) Chosen.Add(key);
            return true;
        }

        // ---------------------------------------------------------------- principles

        /// <summary>Principle points held on one element's board.</summary>
        public int PrinciplePoints(ElementType element, Principle principle)
        {
            int total = 0;
            foreach (var node in MasteryBoard.All)
                if (node.Principle == principle && IsUnlocked(element, node.Id)) total += node.PrincipleWeight;
            return total;
        }

        /// <summary>How many links of a principle's chain are unlocked, 0..Thresholds.Length.</summary>
        public int ChainLinks(ElementType element, Principle principle)
        {
            int pts = PrinciplePoints(element, principle);
            int links = 0;
            foreach (int t in MasteryBoard.Thresholds)
                if (pts >= t) links++;
            return links;
        }

        // ---------------------------------------------------------------- spending

        /// <summary>Levels already committed to one element's board.</summary>
        public int Spent(ElementType element)
        {
            int total = 0;
            foreach (var node in MasteryBoard.All)
                if (IsUnlocked(element, node.Id)) total += node.Cost;
            return total;
        }

        /// <summary>What this element's board may still spend, against the cap.</summary>
        public int Remaining(ElementType element)
            => Mathf.Max(0, Mathf.Min(_profile.For(element).Level, Tuning.Mastery.LevelCap) - Spent(element));

        public enum Blocked
        {
            None,
            AlreadyOwned,
            /// <summary>Nothing adjacent is owned yet - the board is walked, not shopped.</summary>
            Unreachable,
            /// <summary>A sibling branch's keystone was taken. Permanent until reincarnation.</summary>
            KeystoneLocked,
            /// <summary>Not enough unspent levels on this board.</summary>
            TooExpensive,
            /// <summary>Not on this element's board (the Rebis, for an element with one ability).</summary>
            Hidden,
        }

        /// <summary>
        /// A keystone locks every SIBLING branch's keystone in the same domain, on that board,
        /// permanently - only reincarnation reopens it. A branch's fillers are reached only through
        /// its own keystone, so the locked branches are closed whole: the decision is the branch.
        /// </summary>
        public bool KeystoneLocked(ElementType element, MasteryNode node)
        {
            if (!node.Keystone) return false;
            foreach (var other in MasteryBoard.All)
            {
                if (!other.Keystone || other.Domain != node.Domain || other.Branch == node.Branch)
                    continue;
                if (IsUnlocked(element, other.Id)) return true;
            }
            return false;
        }

        public Blocked Check(ElementType element, MasteryNode node)
        {
            if (node == null) return Blocked.Unreachable;
            if (!Visible(element, node)) return Blocked.Hidden;
            if (IsUnlocked(element, node.Id)) return Blocked.AlreadyOwned;
            if (KeystoneLocked(element, node)) return Blocked.KeystoneLocked;
            if (!IsReachable(element, node)) return Blocked.Unreachable;
            if (node.Cost > Remaining(element)) return Blocked.TooExpensive;
            return Blocked.None;
        }

        /// <summary>A keystone is the mouth of its branch and the Rebis stands alone, so both are
        /// always reachable. Everything else needs an owned neighbour.</summary>
        public bool IsReachable(ElementType element, MasteryNode node)
        {
            if (node.Keystone || node.Kind == NodeKind.Rebis) return true;
            foreach (var id in node.Neighbours)
                if (IsUnlocked(element, id)) return true;
            return false;
        }

        public bool TryUnlock(ElementType element, MasteryNode node, out Blocked reason)
        {
            reason = Check(element, node);
            if (reason != Blocked.None) return false;

            _profile.Unlocked.Add(Key(element, node.Id));
            _dirty = true;
            return true;
        }

        // ---------------------------------------------------------------- migration

        /// <summary>
        /// Drops unlocked ids that no node answers to any more, and reports how many. The levels
        /// they cost come back on their own - Spent only counts nodes that exist - so dropping the
        /// ids only makes the file agree with what the board already believes, and stops a dead id
        /// ever colliding with a future node's.
        /// </summary>
        public int DropStale()
        {
            int removed = _profile.Unlocked.RemoveAll(id => Stale(id, out _));
            if (removed > 0) _dirty = true;
            return removed;
        }

        static bool Stale(string id, out ElementType element)
        {
            element = ElementType.Fire;
            if (string.IsNullOrEmpty(id)) return true;
            int colon = id.IndexOf(':');
            if (colon <= 0) return true;                       // unscoped - the first grid
            if (!System.Enum.TryParse(id.Substring(0, colon), out element)) return true;
            return MasteryBoard.Get(id.Substring(colon + 1)) == null;
        }

        /// <summary>
        /// Brings a profile from an older board up to this one: drops every purchase the rebuilt
        /// board no longer has (its levels return to that element's pool) and leaves the player a
        /// note naming what came back, shown the next time the mastery screen opens. Idempotent -
        /// a profile already on this version is left alone. True if anything changed.
        /// </summary>
        public bool Migrate()
        {
            if (_profile.BoardVersion >= MasteryBoard.Version) return false;

            // What each element had spent, measured on the ids before they go: the first rebuilt
            // board (version 1) cost 4 a keystone ("_key") and 1 a filler.
            var refunded = new SortedDictionary<string, int>();
            foreach (var id in _profile.Unlocked)
            {
                if (!Stale(id, out var e)) continue;
                int colon = id.IndexOf(':');
                if (colon <= 0) continue;
                int cost = id.EndsWith("_key") ? 4 : 1;
                refunded.TryGetValue(e.ToString(), out var had);
                refunded[e.ToString()] = had + cost;
            }
            DropStale();
            _profile.SecondAbilityChosen?.Clear();   // the old switch lived on a keystone that is gone
            _profile.BoardVersion = MasteryBoard.Version;

            if (refunded.Count > 0)
            {
                var parts = new List<string>();
                foreach (var kv in refunded) parts.Add($"{kv.Key} {kv.Value}");
                _profile.BoardNotice = "The mastery boards were rebuilt. Every level you had spent is yours " +
                                       $"to spend again - {string.Join(", ", parts)}.";
            }
            _dirty = true;
            return true;
        }

        /// <summary>
        /// Reincarnation: clears ONE element's board back to nothing. Level and the other three
        /// boards are untouched - the levels return to that board's own unspent pool.
        /// </summary>
        public int Reincarnate(ElementType element)
        {
            string prefix = element + ":";
            int removed = _profile.Unlocked.RemoveAll(id => id.StartsWith(prefix));
            Chosen.Remove(element.ToString());
            _dirty = true;
            return removed;
        }
    }
}
