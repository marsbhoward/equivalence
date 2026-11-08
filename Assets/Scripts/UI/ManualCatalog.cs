using Convergence.Core;
using T = Convergence.Core.Tuning;

namespace Convergence.UI
{
    /// <summary>One page of the book the Manual Shrine displays. Words the glossary defines are
    /// marked "[[steep]]" (Core.Glossary); the screen shows their meanings under the page.</summary>
    public readonly struct ManualEntry
    {
        public readonly string Title;
        public readonly string Body;
        public ManualEntry(string title, string body) { Title = title; Body = body; }
    }

    /// <summary>
    /// The book itself - a living document that grows one system at a time as each ships,
    /// rather than a fixed table of contents written up front. Nothing here touches gameplay; it
    /// is purely explanatory. Rewritten 2026-10-30 against the game as it stands (the Short
    /// Descent, the timing bar, the exchange, Rifts, puzzles, the mastery grid); numbers are read
    /// from Tuning so a page cannot drift from the rule it describes.
    /// </summary>
    public static class ManualCatalog
    {
        static string Pct(float multiplier) => $"{UnityEngine.Mathf.Abs(multiplier - 1f) * 100f:0}%";

        public static readonly ManualEntry[] Entries =
        {
            new("THE CLIMB",
                $"A run is a climb of {RunFormat.FinalFloor} floors. A boss waits on every tenth floor, and an avatar on " +
                $"floors 25 and {RunFormat.FinalFloor}; the second avatar ends the run. Most floors are a fight - clear every " +
                "wave and the door opens - and some are a puzzle or hold a Rift.\n\n" +
                "The run clock counts the time you spend fighting, from a floor's first enemy to its clear. The deals and " +
                "rewards between floors are off the clock; pausing during a fight is not. Every ten minutes the hourglass " +
                "appears and enemies hit harder. Past the hour comes the Gate Collapse: every hit grows heavier every five " +
                "minutes, without limit."),

            new("CHAINS AND THE TIMING BAR",
                "Your attacks come in [[chain|chains]]: two [[basic|basics]], then a [[weapon art]]. As a weapon art winds " +
                "up, a slim bar fills beside you. Tap attack once while it fills: in the green it is PERFECT, " +
                $"+{Pct(T.StrikeTiming.PerfectMultiplier)} damage; in the yellow it is GOOD, +{Pct(T.StrikeTiming.GoodMultiplier)}. " +
                $"Too early, or no tap at all, and it lands for {Pct(T.StrikeTiming.MissMultiplier)} less. The strike always " +
                "lands at the end of the bar, so pressing early gains nothing.\n\n" +
                $"Perfect weapon arts in a row that hit build a streak: +{T.StrikeTiming.StreakCritPerPerfect * 100f:0}% " +
                $"[[crit]] chance each, on every hit, up to +{T.StrikeTiming.StreakCap * T.StrikeTiming.StreakCritPerPerfect * 100f:0}%. " +
                "Anything else resets it."),

            new("DEFENSIVE ABILITIES",
                "Every chest piece grants one [[defensive ability]], chosen at random when it is forged: Dash, Barrier, " +
                "Bulwark or Parry Stance. All four share the same instant - use one at the exact moment a hit lands and " +
                "the hit misses outright, whichever you carry. Miss that instant and each falls back on its own: Dash " +
                "gives a moment of immunity and moves you the way you are holding; Barrier raises a dome that turns aside " +
                "what reaches it from the front, lit where it covers you; Bulwark softens incoming damage for a second, no " +
                "aiming needed. Parry Stance has no fallback, for a much shorter cooldown. A clean [[parry]] also answers " +
                "back: the nearest enemy in reach takes a counter-hit, and a parried projectile goes back the way it came."),

            new("THE FOUR ELEMENTS",
                "Every character plays one element, and each builds its meter on a different habit. Fire builds on every " +
                "blow you land and every blow you take or parry, and drains as soon as you stop fighting - it wants you " +
                "in the thick of it. Air builds on movement and drains while you stand still; an unbroken run of hits " +
                "also makes them land harder. Earth charges on stillness and rewards planting your feet. Water builds " +
                "on every blow you land and twice as fast against a soaked enemy, and a miss costs it - so it rewards " +
                "setting up and choosing your shots.\n\n" +
                "The meter is the ring of the element seal, bottom left, and your [[release|releases]] spend it. Each " +
                "element keeps its own mastery board."),

            new("RELEASES",
                "Every element has the same five kinds of release, each in its own form. A BLAST spends a full meter on " +
                "the crowd around you. A BUFF spends a full meter on a few seconds of power. A COMBO is three strikes " +
                "for a third of the meter each; land the next within two seconds to carry the chain to its finish, or " +
                "stop early. A GUARD spends a full meter on three charges, and each use of your defensive ability spends " +
                "one to throw your element around you. A MOBILITY move spends half a meter to carry you across the floor - " +
                "you can be hit all the way.\n\n" +
                "A combo and a buff are made for one enemy - a combo's strike lands in full on the nearest and only " +
                "partly on anything else it catches. A Blast is made for a crowd, and does less to a lone enemy.\n\n" +
                "Blast is yours from the start. The others open as you spend levels along the mastery board: five on " +
                "Affliction for the Buff, Offense for the Combo, Survival for the Guard, Tempo for Mobility. At the " +
                "board's center choose two: TAP the release button for one, HOLD it for the other."),

            new("WEAPON CLASSES",
                "A weapon's class decides how it is fought with, whatever its stats or look, and it is fixed for the run. " +
                "A greatsword is slow and two-handed. Discs are the hybrid: heavy in close, faster and ricocheting at " +
                "range, switching by where you stand - no second button - and neither half is the best at what it does. " +
                "A bow fights only at range: every one of its moves makes space rather than closing it."),

            new("STATUSES",
                "Four statuses can be laid on an enemy. A [[burning]] enemy takes damage each second; a [[soaked]] one is " +
                "slowed and takes more damage from everything; a [[bleeding]] one takes damage each second, opened by " +
                "crits; a [[staggered]] one is slowed, more with each stagger in a row. Fire burns, Water soaks and Earth " +
                "staggers on their own. The mastery grid's Affliction path strengthens all four, and its lanes open the " +
                "[[humour|humours]] that let any element apply them."),

            new("THE PHIAL",
                "Your healing is carried, not handed out. Your phial holds three drinks, full at the start of a run; " +
                "each boss and, now and then, a floor reward put one drink back. Drinking holds you " +
                "still for a moment, and a hit before you swallow spills it. Forge a Silver or Gold phial from boxes " +
                "for drinks that heal more."),

            new("ARMOR AND WEAR",
                "Your eight armor pieces turn away part of every hit - more from Silver and Gold - and wear down as " +
                "you are hit. Worn armor protects less, and broken armor protects nothing.\n\n" +
                "Armor buys plating: each floor, a number of blows wear nothing at all, refilled at the next floor's " +
                "start. Sturdiness slows wear, Mend repairs a little at every floor's clear, and the floor reward's Repair " +
                "ring restores more. Resilience lowers the damage of every hit you take; Graze lowers it while you move, " +
                "and Brace while you stand still."),

            new("BETWEEN FLOORS: THE EXCHANGE",
                "After every floor the exchange offers its deals, each a boon paired with a cost and weighed on a scale. " +
                $"Take one, or refuse - a run allows {T.Exchange.RefusalsPerRun} refusals. What you take lasts until the run " +
                "ends, and repeats stack. Some deals change how a weapon art or a status behaves rather than adding a " +
                "number, and a cost carried far enough can be transmuted into something new. Read the scale: early on a " +
                "boon outweighs its cost, and higher up the two weigh closer to even."),

            new("THE FLOOR REWARD",
                "After every second floor, four rings: two weapon arts above and below, a restore on the left - Heal or " +
                "Repair, whichever you need - and XP or calx on the right. Taking a weapon art opens a comparison with the " +
                "art it would replace on your wheel, and you can back out of it. Heal Received and Repair Received " +
                "strengthen the restore ring and nothing else."),

            new("RIFTS",
                "What you find on a run is CARRIED, and lost if you die. A Rift is the way out: at one you can extract and " +
                "leave with everything you carry, or push on and secure only what fits through it - one item, more with " +
                "the mastery grid's Rift diagonal. XP and mastery are never lost either way.\n\n" +
                "A Blue Rift stands open. A Collapsing Rift tears open as the floor starts: clear the floor in time and it " +
                "steadies; fail and it implodes. A Red Rift is sealed, and breaking it calls its elite guard. The avatar " +
                "on floor 25 always leaves a Rift behind. Rift Boxes secure one more item, promote a three-star piece, or " +
                "insure a stake. The crate's inventory chooses how many come into a run: those are carried, lost on a " +
                "death like anything found, and the rest stay home, safe."),

            new("PUZZLE FLOORS",
                "Some floors hold a puzzle instead of a fight, each on its own timer. ECHO plays a call to repeat. " +
                "ELEMENTS asks for four stones in an order deduced from clues. LIGHTS is a three-by-three board to light " +
                "fully. The LABYRINTH is the whole floor as a maze to the door.\n\n" +
                "Solve it and the door opens as for a cleared floor. A wrong answer or running out of time seals it, and " +
                "the side door leads to the floor's fight instead - which you may also take at any time to give up. A " +
                "failed puzzle stops the run clock until you step through."),

            new("GEAR AND THE FORGE",
                "Twelve slots and a relic. A piece has a tier - Bronze, Silver or Gold - and up to three stars; a main " +
                "stat, and sub-stats: one on Bronze, two on Silver, three on Gold. Stars raise every stat. Whatever you wear when the run starts is what " +
                "you fight the whole run with.\n\n" +
                "At the Forge, two matching pieces - same slot, tier, stars and main stat - combine into one with a star " +
                "more, keeping the sub-stats they share at the higher roll. Two three-star pieces promote into the next " +
                "tier for a Rift Box. Boxes open into gear, or re-roll one sub-stat: one box for a new value, two for a " +
                "new stat. Pieces you no longer want salvage into calx."),

            new("RELICS AND THE WEAPON ART WHEEL",
                "The wheel beside the element seal holds your weapon arts in order; the one at the top fires next. A " +
                "relic is a drop whose stat is a weapon art: worn in the relic slot, it sets the third art on your " +
                "wheel. Every character starts with a relic of Overhand. A Black Diamond relic carries its weapon's " +
                "signature art, locked in that slot for the whole run.\n\n" +
                "The order can change only between floors: when the door opens, open the character sheet and reorder the " +
                "wheel before you step through."),

            new("THE GEAR STAKE",
                "At the sigil door you may stake one equipped piece on the run. Clear its gate floor and extract, and you " +
                "leave with a matching partner for the Forge. Extract before the gate and the piece comes home; die and " +
                "it is lost - unless you insured it with a banked Rift Box. The gate rises with the piece's stars: floor " +
                $"{T.Stake.ShortGateBase}, {T.Stake.ShortGateOneStar} or {T.Stake.ShortGateTwoStar}, and " +
                $"{T.Stake.ShortGatePromotion} for a three-star piece."),

            new("MASTERY",
                "Every level is a point to spend on the board of the element you play. The board is a grid out of the " +
                "sigil, whose center chooses your [[release|releases]]. Four paths leave it: a trunk, a fork of four lanes, a " +
                "rule where they meet, the fork again and a capstone. Taking a lane closes only that fork's other lanes. " +
                "Diagonals and bridges join the paths, so a path can be entered from the side.\n\n" +
                "Every node also feeds a [[principle]]; enough points in one unlock the next link of its chain. " +
                "Reincarnation clears one element's board, free, once a day."),
        };
    }
}
