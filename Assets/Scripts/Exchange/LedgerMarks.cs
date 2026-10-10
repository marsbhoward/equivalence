using UnityEngine;

namespace Convergence.Exchange
{
    /// <summary>
    /// What the run's ledger remembers about ONE enemy: whether it has been struck yet (First
    /// Blood, Honed), how many of the player's hits it has taken (Cementation), and how many
    /// scored hits it still owes (Damascene).
    ///
    /// A component on the enemy rather than a Dictionary on the player, for the reason Sulfur's
    /// Seasoning marks are: a dictionary comes back EMPTY after a domain reload while every body it
    /// named survives, and it would have to be cleared of the dead by hand. Plain value fields
    /// only, so a reload loses nothing.
    /// </summary>
    public class LedgerMarks : MonoBehaviour
    {
        public bool Struck;
        public int Hits;
        public int ScoredHits;

        public static LedgerMarks Of(GameObject go)
        {
            if (go == null) return null;
            var m = go.GetComponent<LedgerMarks>();
            return m != null ? m : go.AddComponent<LedgerMarks>();
        }

        /// <summary>Marks the enemy has without making it any - for reads that must not attach.</summary>
        public static LedgerMarks Peek(GameObject go) => go != null ? go.GetComponent<LedgerMarks>() : null;
    }
}
