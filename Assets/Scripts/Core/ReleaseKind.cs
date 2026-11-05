namespace Convergence.Core
{
    /// <summary>
    /// THE RELEASE ARSENAL (2026-11-05, the user's design; RELEASES.md): every element has the same
    /// five kinds of release, each in its own flavour. Saved by NAME (the profile's choice, a run's
    /// snapshot) - append only, never reorder or rename.
    /// </summary>
    public enum ReleaseKind
    {
        Blast,
        Buff,
        Combo,
        Guard,
        Mobility,
    }

    public static class ReleaseInfo
    {
        public static readonly ReleaseKind[] All =
            { ReleaseKind.Blast, ReleaseKind.Buff, ReleaseKind.Combo, ReleaseKind.Guard, ReleaseKind.Mobility };

        /// <summary>The release's name for an element, as the seal and the board show it.</summary>
        public static string Name(ElementType e, ReleaseKind k) => (e, k) switch
        {
            (ElementType.Fire, ReleaseKind.Blast) => "ERUPT",
            (ElementType.Fire, ReleaseKind.Buff) => "IGNITE",
            (ElementType.Fire, ReleaseKind.Combo) => "FLARE CHAIN",
            (ElementType.Fire, ReleaseKind.Guard) => "EMBER GUARD",
            (ElementType.Fire, ReleaseKind.Mobility) => "COMET",
            (ElementType.Water, ReleaseKind.Blast) => "BURST",
            (ElementType.Water, ReleaseKind.Buff) => "SURGE",
            (ElementType.Water, ReleaseKind.Combo) => "TIDE STRIKES",
            (ElementType.Water, ReleaseKind.Guard) => "MIST VEIL",
            (ElementType.Water, ReleaseKind.Mobility) => "WAVECREST",
            (ElementType.Earth, ReleaseKind.Blast) => "QUAKE",
            (ElementType.Earth, ReleaseKind.Buff) => "TREMOR STANCE",
            (ElementType.Earth, ReleaseKind.Combo) => "STONE CHAIN",
            (ElementType.Earth, ReleaseKind.Guard) => "STONE THORNS",
            (ElementType.Earth, ReleaseKind.Mobility) => "MUDSLIDE",
            (_, ReleaseKind.Blast) => "SQUALL",
            (_, ReleaseKind.Buff) => "GUST",
            (_, ReleaseKind.Combo) => "CROSSWIND",
            (_, ReleaseKind.Guard) => "WINDBREAK",
            _ => "ZEPHYR",
        };

        /// <summary>The kind's own word, for the board's list.</summary>
        public static string KindName(ReleaseKind k) => k.ToString().ToUpperInvariant();

        /// <summary>The mastery path whose points open the kind (MasteryBoard.Paths ids), or null for
        /// Blast, open from the start.</summary>
        public static string PathOf(ReleaseKind k) => k switch
        {
            ReleaseKind.Combo => "offense",
            ReleaseKind.Mobility => "tempo",
            ReleaseKind.Guard => "survival",
            ReleaseKind.Buff => "affliction",
            _ => null,
        };

        /// <summary>The share of a full meter one use spends.</summary>
        public static float Cost(ReleaseKind k) => k switch
        {
            ReleaseKind.Combo => Tuning.Releases.ComboStrikeCost,
            ReleaseKind.Mobility => Tuning.Releases.MobilityCost,
            _ => 1f,
        };

        /// <summary>
        /// Whether the release is in the game. The arsenal was built in phases (RELEASES.md) and a kind
        /// not built for an element is never offered, so a choice can never point at nothing; every
        /// kind is built for every element since 2026-11-05. Kept as the one place to switch one off.
        /// </summary>
        public static bool Built(ElementType e, ReleaseKind k) => true;

        /// <summary>Whether a kind spends a whole meter - the kinds the ledger's and the board's release
        /// reactions answer (refunds, repeats, costs). A combo strike or a move spends a part and is not
        /// a release to them, or a refund on every third would pay for the next.</summary>
        public static bool FullMeter(ReleaseKind k) => Cost(k) >= 1f;

        public static bool TryParse(string s, out ReleaseKind k) => System.Enum.TryParse(s, out k) && System.Enum.IsDefined(typeof(ReleaseKind), k);
    }
}
