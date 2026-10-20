namespace Convergence.Core
{
    /// <summary>
    /// The game's few spoken lines, in one place - so the writing can be changed without touching
    /// the code that shows it.
    ///
    /// ALL DRAFTS, the user's to rewrite (the narrative is theirs - memory
    /// narrative_reincarnation_bosses): sparse, felt rather than stated, ONE short line reused by
    /// every avatar with only its TENSE shifting (past at the first, the future at the last), and
    /// never a line that explains the trick.
    /// </summary>
    public static class Narrative
    {
        /// <summary>The final avatar's words as it falls (the Short Descent's floor 50): the future
        /// form of the line whose past form would be the first avatar's ("I gave it up.").</summary>
        public static readonly string[] FinalAvatarWords =
        {
            "I will give it up.",
        };

        /// <summary>The closing card - a clear inside the hour (the gate held)...</summary>
        public const string EndingInTime = "THE GATE HOLDS";

        /// <summary>...and one in the Gate Collapse.</summary>
        public const string EndingCollapsed = "THROUGH THE COLLAPSE";
    }
}
