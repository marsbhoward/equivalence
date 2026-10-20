namespace Convergence.Core
{
    /// <summary>
    /// Which RUN FORMAT a run is played in (SHORT-DESCENT.md): Classic's 100 floors, or the Short
    /// Descent's 50, cleared in about an hour. A DEV-ONLY switch - players only ever see one format.
    ///
    /// THE RUN CLOCK IS THE SERVICE's: it times each floor from the snapshot entering it to the one
    /// clearing it (web/service/src/runProgress.ts), counting the pause menu and a hidden tab during
    /// a fight. The hourglass, when built, must count the same or the two will disagree at 60:00.
    ///
    /// Today it only NAMES the format to the service: the run-start save carries it
    /// (ServiceProfileStore), and the service judges the run's boxes by that format's rules
    /// (web/service/src/runFormat.ts - its final floor, which avatars roll, the plausibility floor).
    /// The Short Descent itself - 50 floors, the run clock, the hourglass - is not built yet; until
    /// it is, <see cref="Current"/> must stay Classic, or the service would judge a 100-floor run by
    /// 50-floor rules. A static resets to Classic on a domain reload, which is the safe default.
    /// </summary>
    public enum RunFormatId { Classic, ShortDescent }

    public static class RunFormat
    {
        public static RunFormatId Current = RunFormatId.Classic;

        /// <summary>The name the service knows the format by.</summary>
        public static string WireName => Current == RunFormatId.ShortDescent ? "short" : "classic";
    }
}
