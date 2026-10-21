using UnityEngine;
using Convergence.Exchange;

namespace Convergence.UI
{
    /// <summary>
    /// THE COLOUR DESIGNATIONS and PROCESS NAMES of the exchange's entries (the user's calls,
    /// 2026-10-21; mocked on https://claude.ai/artifact/Ei7m8JVaGoC6nhz216UM9e). Every screen that
    /// shows an entry asks here, so a kind reads the same in the pans, on the card and in the strip.
    ///
    ///   boon            green
    ///   cost            red
    ///   WEAPON ART      black lit violet - Black Diamond's colour, the weapons-only tier
    ///   ALBEDO          white
    ///   CITRINITAS      yellow
    ///   CAUDA PAVONIS   iridescent - an evolution (blue, green, bronze, violet, shifting)
    ///
    /// The capstones keep their marks: a Rubedo's gilt-crimson glow, a Nigredo's black smoke. Black
    /// is never drawn GREY - grey ash on black is the Nigredo's. A screen NAMES the process and the
    /// entry ("NIGREDO · Anaemia") and never explains the process.
    /// </summary>
    public static class ExchangeInk
    {
        public enum Kind { Boon, Cost, WeaponArt, Albedo, Citrinitas, Pavonis }

        public static Kind KindOf(ExchangeEntry e)
        {
            if (e == null) return Kind.Boon;
            if (e.Kind == ExchangeKind.Cost) return Kind.Cost;
            switch (e.Origin)
            {
                case EntryOrigin.Evolution:  return Kind.Pavonis;
                case EntryOrigin.Albedo:     return Kind.Albedo;
                case EntryOrigin.Citrinitas: return Kind.Citrinitas;
            }
            return e.Slot == FormSlot.Art ? Kind.WeaponArt : Kind.Boon;
        }

        public static readonly Color Boon = new(0.61f, 0.82f, 0.42f);
        public static readonly Color Cost = new(0.88f, 0.44f, 0.31f);
        public static readonly Color Violet = new(0.73f, 0.59f, 0.95f);      // the weapon-art light
        public static readonly Color White = new(0.94f, 0.93f, 0.89f);
        public static readonly Color Citrine = new(0.93f, 0.79f, 0.30f);
        public static readonly Color Pavonis = new(0.42f, 0.80f, 0.86f);     // the tail's blue-green, for text

        /// <summary>A flat colour for the entry's name, its process label and a tinted glyph.</summary>
        public static Color Tint(ExchangeEntry e) => KindOf(e) switch
        {
            Kind.Cost       => Cost,
            Kind.WeaponArt  => Violet,
            Kind.Albedo     => White,
            Kind.Citrinitas => Citrine,
            Kind.Pavonis    => Pavonis,
            _               => Boon,
        };

        /// <summary>The process an entry came out of, named and nothing more - null for a plain one.</summary>
        public static string Process(ExchangeEntry e)
        {
            if (e == null) return null;
            switch (e.Origin)
            {
                case EntryOrigin.Albedo:       return "ALBEDO";
                case EntryOrigin.Citrinitas:   return "CITRINITAS";
                case EntryOrigin.Conjunction:  return "CONJUNCTION";
                case EntryOrigin.Putrefaction: return "PUTREFACTION";
                case EntryOrigin.Evolution:    return "CAUDA PAVONIS";
                case EntryOrigin.Union:        return "UNION";
            }
            if (e.Oath) return "OATH";
            return e.Kind == ExchangeKind.Boon && e.Slot == FormSlot.Art ? "WEAPON ART" : null;
        }

        /// <summary>A capstone named: "RUBEDO · Keen Edge", "NIGREDO · Anaemia".</summary>
        public static string Capstone(ExchangeEntry e)
            => e?.CapName == null ? null : $"{e.CapKind.ToUpperInvariant()} · {e.CapName}";
    }
}
