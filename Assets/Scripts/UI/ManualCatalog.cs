namespace Convergence.UI
{
    /// <summary>One page of the book the Manual Shrine displays.</summary>
    public readonly struct ManualEntry
    {
        public readonly string Title;
        public readonly string Body;
        public ManualEntry(string title, string body) { Title = title; Body = body; }
    }

    /// <summary>
    /// The book itself - a living document that grows one system at a time as each ships,
    /// rather than a fixed table of contents written up front. The same discipline this
    /// project's own CLAUDE.md already follows for its notes, just read from inside the room
    /// instead of a text file. Nothing here touches gameplay; it is purely explanatory.
    /// </summary>
    public static class ManualCatalog
    {
        public static readonly ManualEntry[] Entries =
        {
            new("ARMOR, RESISTANCE & RESILIENCE",
                "Three stats stand between an attack and your health bar. Armor blunts incoming " +
                "damage before anything else is applied. Damage Resistance shaves a further " +
                "percentage off whatever gets through. Resilience is the rarest of the three - it " +
                "reduces how hard status effects like burn and bleed can stack on you, rather than " +
                "the hit itself. All three come from worn gear, and all three fall as that gear " +
                "wears down over a run - a nearly-broken cuirass protects far less than a fresh one."),

            new("DEFENSIVE ABILITIES",
                "Every chest piece grants one defensive ability, chosen at random when it is " +
                "forged: Dash, Barrier, Bulwark, or Parry Stance. All four share the same instant - " +
                "activate one at the exact moment a hit lands, and the damage is negated outright, " +
                "the same true miss whichever ability you carry. Miss that instant and each ability " +
                "falls back to its own passive: Dash grants a quarter-second of immunity and moves " +
                "you in the direction you're holding; Barrier raises a hexagonal dome for two " +
                "seconds, turning aside everything that reaches it from the front - the lattice is " +
                "lit exactly where it covers you, and ghosted where it does not, so your back is " +
                "as open as it looks; Bulwark softens - but doesn't erase - incoming damage for a " +
                "second, no aiming required. Parry Stance skips the passive entirely for a much shorter cooldown, " +
                "trading safety for how often you can attempt the parry itself. A clean parry also " +
                "answers back: the nearest enemy within your weapon's own reach takes a counter-hit, " +
                "and a parried projectile is sent straight back the way it came."),

            new("GEAR IS LOCKED FOR THE RUN",
                "Whatever you're wearing when you step through the door is what you fight the whole " +
                "run with. The loadout screen still opens mid-run to inspect your build, but nothing " +
                "can be swapped until you're back in the room. This is deliberate: a build that can " +
                "change shape mid-fight isn't really a build, and every weapon art, stat, and relic in " +
                "this manual assumes the gear underneath it is holding still."),

            new("RELICS & THE WEAPON ART WHEEL",
                "The thirteenth socket carries no armor and no weapon - only a relic, which decides " +
                "what sits in your third weapon art slot. A plain relic leaves that slot on the " +
                "default Overhand. A rarer one grants a specific weapon art outright, giving you a " +
                "playstyle to lean into before a run even starts. A black-diamond relic goes " +
                "further still: it locks the slot to that weapon's own signature weapon art for the " +
                "whole run, unreplaceable by anything the floor rewards offer. Without a third " +
                "weapon art equipped at all, the wheel simply runs on its two remaining slots rather " +
                "than standing empty."),

            new("THE REORDER DOOR",
                "Clearing a floor's last enemy doesn't drop you straight into the next one. A door " +
                "opens at the top of the arena, and walking through it is the one moment between " +
                "fights where your weapon art order can change - open the character sheet and swap " +
                "slots freely before stepping through. Once you're on the other side, the order is " +
                "set again until the next floor closes."),

            new("THE FORGE",
                "Floors drop gear and boxes, and the Forge is where a box becomes a real item. Spend " +
                "one for a random slot at the box's tier, or four to choose the slot - stats, and " +
                "for chest pieces a defensive ability, are rolled on the spot. " +
                "Two matching pieces (same slot, tier, stars and primary stat) combine into one with " +
                "a star more - and two three-star pieces into the next tier. Sub-stats both pieces " +
                "share are kept at the higher roll; the rest are rolled fresh. Boxes re-roll a " +
                "single sub-stat: one for a new value, two for a new stat."),

            new("THE FOUR ELEMENTS",
                "Every character plays one element, and each one is built on a different cost. Fire " +
                "builds only on landed melee swings - it wants you in close. Air builds on movement " +
                "and bleeds away while you stand still - it wants you circling. Earth is the " +
                "opposite: it charges on stillness, and rewards planting your feet. Water builds per " +
                "hit and pays out double against a soaked target, so it rewards reading the crowd " +
                "rather than any one habit. None of them are free - each one is asking something " +
                "different of how you fight."),

            new("WEAPON CLASSES",
                "A weapon's class decides how it's fought with, independent of its stats or its " +
                "look. A greatsword is slow and two-handed. Discs are the hybrid: heavy in close, " +
                "faster and ricocheting at range, switching automatically by where you're standing - " +
                "no second button. Neither half of a disc is the best at what it does; that's the " +
                "price of being able to do both."),
        };
    }
}
