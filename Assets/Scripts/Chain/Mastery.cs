using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using Convergence.Progression;

namespace Convergence.Chain
{
    /// <summary>
    /// Per-element mastery plus which grid nodes have been bought.
    ///
    /// Mastery is earned ONLY by playing. There is no purchasable path into it, and the daily
    /// cap on completed runs is what bounds how fast it can move - so buying tries or false
    /// starts buys attempts, never progression.
    /// </summary>
    [Serializable]
    public class MasteryProfile
    {
        public List<ElementMastery> Elements = new();
        public List<string> Unlocked = new();

        /// <summary>
        /// Elements whose board has SWITCHED to the second ability, by name ("Air"). Absent means
        /// the first ability - the default, so an old save and a board that never bought the
        /// keystone both land on it without a migration. Names rather than ElementType ints for
        /// the same reason Unlocked is element-scoped strings: JsonUtility stores enums by ordinal.
        /// Only meaningful while the granting keystone is owned - see BoardState.UsesSecondAbility.
        /// </summary>
        public List<string> SecondAbilityChosen = new();

        /// <summary>
        /// Which MasteryBoard version the Unlocked ids were bought on. Zero for every save written
        /// before the 2026-10-05 rebuild - BoardState.Migrate brings it up, returning the levels the
        /// old board's purchases cost, and stamps the current version.
        /// </summary>
        public int BoardVersion;

        /// <summary>A note for the player the next time the mastery screen opens - what a board
        /// rebuild gave back - cleared once shown. Empty when there is nothing to say.</summary>
        public string BoardNotice = "";

        /// <summary>Sum of every element's level - the figure the core gates check.</summary>
        public int TotalLevel
        {
            get { int t = 0; foreach (var e in Elements) t += e.Level; return t; }
        }

        public ElementMastery For(ElementType element)
        {
            foreach (var e in Elements)
                if (e.Element == element) return e;

            var created = new ElementMastery { Element = element };
            Elements.Add(created);
            return created;
        }

        public bool IsUnlocked(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return false;

            // No free roots on the new board: a keystone is the mouth of its branch and is BOUGHT,
            // so there is nothing to special-case. Ids here are element-scoped ("Fire:tempo_..."),
            // written by BoardState.
            return Unlocked.Contains(nodeId);
        }

        /// <summary>Levels of a given element not yet spent on nodes.</summary>
        public int Available(ElementType element)
        {
            var m = For(element);
            return Mathf.Max(0, m.Level - m.Spent);
        }
    }

    [Serializable]
    public class ElementMastery
    {
        public ElementType Element;
        public int Xp;
        public int Level;
        public int Spent;

        /// <summary>XP needed to go from <paramref name="level"/> to the next one.</summary>
        public static int XpForNext(int level) => 100 + level * 40;

        /// <summary>Fold XP into levels. Returns how many levels were gained.</summary>
        public int Grant(int xp)
        {
            if (xp <= 0) return 0;
            Xp += xp;
            int gained = 0;
            while (Xp >= XpForNext(Level))
            {
                Xp -= XpForNext(Level);
                Level++;
                gained++;
            }
            return gained;
        }

        public float Progress01 => Mathf.Clamp01(Xp / (float)XpForNext(Level));
    }
}
