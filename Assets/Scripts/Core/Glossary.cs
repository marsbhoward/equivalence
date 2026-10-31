using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Convergence.Art.Gear;

namespace Convergence.Core
{
    /// <summary>
    /// THE ONE LIST of what the game's words mean: every stat, and every piece of system language a
    /// rule's text leans on (steep, release, chain, parry, the statuses...). A player reads an effect
    /// without having to remember what its words are (the user's call, 2026-10-30).
    ///
    /// Rule text marks a defined word with <c>[[key]]</c>, or <c>[[key|shown words]]</c> where the
    /// sentence needs another form ("[[crit|crits]]"). A screen either shows the words highlighted
    /// with their definitions under the text (<see cref="Highlight"/> + <see cref="Footnotes"/>) or
    /// strips the marks (<see cref="Plain"/>). Numbers come from Tuning, as the rules' own do.
    ///
    /// The mastery grid mock (claude.ai artifact 2V5HV3GA2QcNf9uKisGt2k) carries a copy of these
    /// words; <see cref="Export"/> prints them to refresh it.
    /// </summary>
    public static class Glossary
    {
        static readonly Regex Mark = new(@"\[\[([^\]|]+)(?:\|([^\]]+))?\]\]", RegexOptions.Compiled);

        /// <summary>A stat's meaning, in the player's words. Empty for None.</summary>
        public static string Stat(StatKind k) => k switch
        {
            StatKind.Damage => "Raises the damage of every hit you deal.",
            StatKind.AttackSpeed => "You swing and throw faster.",
            StatKind.MoveSpeed => "You move faster.",
            StatKind.Range => "Your attacks reach further.",
            StatKind.MaxHp => "Raises your max health by a percentage.",
            StatKind.Armor => $"Plating: each floor, the first blow for every {Tuning.Defense.ArmorPerPlate:0} Armour wears nothing from your gear. Refilled at the start of every floor.",
            StatKind.DamageResistance => "Your armour wears down more slowly when you are hit.",
            StatKind.Resilience => "You take less damage from every hit.",
            StatKind.ElementGrowth => "Your [[element meter]] fills faster.",
            StatKind.AbilityCooldownReduction => "Shortens your [[defensive ability|defensive ability's]] cooldown.",
            StatKind.FinisherPower => "More damage on your [[weapon art|weapon arts]].",
            StatKind.FinisherKnockback => "Your [[weapon art|weapon arts]] push enemies further. It never stuns them for longer.",
            StatKind.CritChance => $"How often a hit is a [[crit]]. Capped at {Tuning.Stats.CritChanceCap * 100f:0}%; past the cap, the extra is added to Crit Damage, so nothing is wasted.",
            StatKind.CritDamage => "How much more a [[crit]] deals.",
            StatKind.Graze => "You take less damage while moving.",
            StatKind.Brace => "You take less damage while standing still.",
            StatKind.Cleave => "When a swing hits several enemies, each one after the first loses less damage; a disc loses less as it ricochets.",
            StatKind.ElementalEffectiveness => "Your [[release|releases]] hit harder.",
            StatKind.ComboTime => "How long you can pause between hits before your [[chain]] starts over.",
            StatKind.AoeRadius => "Your [[release|releases]] and [[area weapon arts]] cover more ground.",
            StatKind.HealReceived => "Strengthens the floor reward's Heal ring. It does not affect any other healing.",
            StatKind.RepairReceived => "Strengthens the floor reward's Repair ring. It does not affect any other repair.",
            StatKind.Mend => "Worn gear knits a little back together at every floor's clear.",
            StatKind.Splash => "A share of each hit also strikes every other enemy near the one you aimed at.",
            StatKind.Pierce => "A share of each arrow's hit also strikes every enemy in a line behind its target.",
            StatKind.Accuracy => "Every hit rolls its damage within a range. Accuracy raises the lowest roll toward the highest; at 100, every hit lands at the top.",
            _ => "",
        };

        /// <summary>
        /// System language, keyed lower-case. A definition may mark other words; they are shown
        /// plain inside a definition (one level deep is enough to read).
        /// </summary>
        static readonly Dictionary<string, string> Terms = new()
        {
            ["basic"] = "One of your ordinary attacks. A [[chain]] is two basics, then a weapon art.",
            ["weapon art"] = "The last hit of a [[chain]]: your weapon's special move, timed on the bar.",
            ["chain"] = "Two [[basic|basics]], then a [[weapon art]].",
            ["perfect"] = "A weapon art pressed in the timing bar's perfect zone.",
            ["crit"] = "A critical hit: it deals extra damage, set by Crit Damage. Crit Chance is how often one happens.",
            ["steep"] = "A bonus stored up over several hits and spent all at once by your next [[weapon art]].",
            ["release"] = "Your element's ability (Erupt, Ignite, Surge, Quake, Gust or Squall), fired when the [[element meter]] is full.",
            ["element meter"] = "The ring around the element seal, bottom left. It fills as you fight, and a [[release]] spends it.",
            ["damage points"] = "Added to your Damage stat along with gear and the board. The more Damage you already have, the less each point adds.",
            ["attack speed points"] = "Added to your Attack Speed stat along with gear and the board. The more you already have, the less each point adds.",
            ["units"] = $"Distance in the arena. Your base [[reach]] is {Tuning.Player.BaseRange:0.#} units.",
            ["reach"] = "How far your attacks land from you. The Range stat extends it.",
            ["burning"] = "Takes a share of the hit that set it burning as damage each second.",
            ["soaked"] = "Slowed, and taking more damage from everything.",
            ["bleeding"] = "Takes a share of the hit that opened it as damage each second.",
            ["staggered"] = "Slowed. Each stagger in a row slows the enemy more, up to three.",
            ["congealed"] = "Rooted to the spot and taking more damage.",
            ["slowed"] = "Moving and acting more slowly, from a soak, a stagger or any other slow.",
            ["tells"] = "The warning an enemy shows before it attacks.",
            ["defensive ability"] = "Your chest piece's guard move: Dash, Barrier, Bulwark or Parry Stance. It has its own cooldown.",
            ["parry"] = "Using your defensive ability at the exact moment a hit lands: the hit misses outright.",
            ["lifesteal"] = "A share of the damage you deal, returned as health.",
            ["healing limit"] = $"Lifesteal heals at most {Tuning.Stats.LifestealHealPerSecond * 100f:0.#}% of your max health each second, and all lifesteal together is capped at {Tuning.Stats.LifestealCap * 100f:0}% of damage dealt.",
            ["shield"] = "Absorbs damage before your health does.",
            ["area weapon arts"] = "Weapon arts that hit everything in a zone, not only one target.",
            ["humour"] = "A status rule that lets any element apply burn, soak, bleed or stagger.",
            ["unit"] = "One unit of a stat is a quarter of what a Gold armour piece gives as its main stat.",
            ["principle"] = "Sulfur, Mercury or Salt. Every node feeds one, and enough points in one unlock the next link of its chain.",
        };

        /// <summary>A system word's meaning, or empty if it is not defined.</summary>
        public static string Term(string key)
            => key != null && Terms.TryGetValue(key.ToLowerInvariant(), out var t) ? t : "";

        /// <summary>The text with its marks removed: each marked word as written in the sentence.</summary>
        public static string Plain(string text)
            => string.IsNullOrEmpty(text) ? text ?? "" : Mark.Replace(text, m => Shown(m));

        /// <summary>The text with each marked word coloured (uGUI rich text) - the cue that it is
        /// defined below (<see cref="Footnotes"/>).</summary>
        public static string Highlight(string text, string hex)
            => string.IsNullOrEmpty(text) ? text ?? "" : Mark.Replace(text, m => $"<color=#{hex}>{Shown(m)}</color>");

        /// <summary>Every defined word the text marks, once each, in the order they appear.</summary>
        public static List<string> TermsIn(string text)
        {
            var keys = new List<string>();
            if (string.IsNullOrEmpty(text)) return keys;
            foreach (Match m in Mark.Matches(text))
            {
                string k = m.Groups[1].Value.ToLowerInvariant();
                if (Terms.ContainsKey(k) && !keys.Contains(k)) keys.Add(k);
            }
            return keys;
        }

        /// <summary>
        /// The definitions of every word the text marks, one per line ("Steep - a bonus..."), the
        /// name coloured <paramref name="hex"/>. Empty when the text marks nothing.
        /// </summary>
        public static string Footnotes(string text, string hex)
        {
            var sb = new StringBuilder();
            foreach (var k in TermsIn(text))
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"<color=#{hex}>{char.ToUpperInvariant(k[0])}{k.Substring(1)}</color>  {Plain(Terms[k])}");
            }
            return sb.ToString();
        }

        /// <summary>Every stat and term as JSON lines, to refresh the mock's copy.</summary>
        public static string Export()
        {
            var sb = new StringBuilder("{\"stats\":{");
            bool first = true;
            foreach (StatKind k in System.Enum.GetValues(typeof(StatKind)))
            {
                string s = Stat(k);
                if (s.Length == 0) continue;
                sb.Append(first ? "" : ",").Append($"\"{k}\":\"{Escape(s)}\"");
                first = false;
            }
            sb.Append("},\"terms\":{");
            first = true;
            foreach (var kv in Terms)
            {
                sb.Append(first ? "" : ",").Append($"\"{kv.Key}\":\"{Escape(kv.Value)}\"");
                first = false;
            }
            return sb.Append("}}").ToString();
        }

        static string Shown(Match m) => m.Groups[2].Success ? m.Groups[2].Value : m.Groups[1].Value;
        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
