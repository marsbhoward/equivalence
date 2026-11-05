using System;
using System.Collections.Generic;
using Convergence.Combat;

namespace Convergence.Exchange
{
    /// <summary>
    /// THE KINDS (EXCHANGE-OVERHAUL.md, 2026-11-04): what part of play an entry changes. A deal's
    /// slates draw a KIND first - every kind with something to offer at an equal chance - and then
    /// an entry inside it, so a big pool means more variety, not more of the deal. Append only:
    /// a run snapshot never stores these, but the deal's kind clocks are keyed by them.
    /// </summary>
    public enum DealKind
    {
        None,
        // boons
        ArtRole, ArtCraft, Chain, Release, Guard, Concord, Humour, Vessel, Fortune, SetPiece,
        // costs
        Frailty, Toll, Rule, Host, Ground, Socket,
    }

    /// <summary>The seven roles an art can play (WEAPON-ARTS.md: every class's rollable arts fill
    /// all of them). An art may hold two. An ART ROLE form is inscribed only on a socket whose art
    /// has its role.</summary>
    [Flags]
    public enum ArtRole
    {
        None = 0,
        Pull = 1 << 0,
        Push = 1 << 1,
        Reposition = 1 << 2,
        Ward = 1 << 3,
        Strike = 1 << 4,
        Sweep = 1 << 5,
        Setup = 1 << 6,
    }

    /// <summary>
    /// What an entry NEEDS to happen (a boon's trigger) or BLOCKS (a cost that takes the trigger
    /// away). The drawer never offers a cost that blocks a held boon's need, nor a boon whose need a
    /// held cost blocks - the dead-trigger guard. Flags, so a new verb is one more bit.
    /// </summary>
    [Flags]
    public enum Verb
    {
        None = 0,
        MoveWhileSwinging = 1 << 0,
        Catch = 1 << 1,
        Unaware = 1 << 2,
        Connector = 1 << 3,
        OwnGround = 1 << 4,
        StandStill = 1 << 5,
        MoveWhileShooting = 1 << 6,
        Crit = 1 << 7,
    }

    /// <summary>Which way an entry moves the crowd - the GATHER/SCATTER lean pulls the one you hold.</summary>
    public enum Intent { None, Gather, Scatter }

    /// <summary>A humour entry's part: a SOURCE puts a status on, a PAYOFF cashes one, GROWTH grows with it.</summary>
    public enum StatusPart { None, Source, Payoff, Growth }

    /// <summary>The roles of every rollable art, read from ARTS.md (2026-11-04). A Black Diamond
    /// signature is not here: its socket takes no role form.</summary>
    public static class ArtRoles
    {
        const ArtRole Pull = ArtRole.Pull, Push = ArtRole.Push, Repos = ArtRole.Reposition, Ward = ArtRole.Ward;
        const ArtRole Strike = ArtRole.Strike, Sweep = ArtRole.Sweep, Setup = ArtRole.Setup;

        static readonly Dictionary<string, ArtRole> Table = new()
        {
            // greatsword
            ["cleave"] = Sweep | Push,
            ["flurry"] = Strike,
            ["lunge"] = Strike | Push,
            ["hammer"] = Push | Sweep,          // Sunder
            ["undertow"] = Pull,                // Vortex
            ["ruin"] = Sweep,
            ["wanderblade"] = Repos | Setup,
            ["meteor"] = Repos | Sweep,         // Falling Star
            ["impale"] = Repos | Strike,
            ["grappling_hook"] = Pull | Repos,
            ["riposte"] = Ward,
            // discs
            ["disc_ripple"] = Sweep,
            ["disc_carom"] = Sweep,
            ["disc_cleaver"] = Strike | Push,
            ["disc_carousel"] = Sweep,
            ["disc_kickback"] = Repos,
            ["disc_scatter"] = Sweep,
            ["disc_mark"] = Setup,
            ["disc_tether"] = Pull,
            ["disc_gyre"] = Pull,
            ["disc_revolve"] = Ward,
            ["disc_plow"] = Push,
            ["disc_sublimate"] = Setup,
            ["disc_orrery"] = Sweep,
            // bows
            ["bow_harpoon"] = Pull,
            ["bow_split"] = Strike,
            ["bow_pin"] = Setup,
            ["bow_tripwire"] = Push | Setup,
            ["bow_longshot"] = Strike,
            ["bow_piercing_rain"] = Sweep | Push,
            ["bow_palisade"] = Ward,
            ["bow_disengage"] = Repos,
            ["bow_concussion"] = Push,
        };

        public static ArtRole Of(Moveset m) => m != null && m.Id != null && Table.TryGetValue(m.Id, out var r) ? r : ArtRole.None;
        public static ArtRole Of(string id) => id != null && Table.TryGetValue(id, out var r) ? r : ArtRole.None;

        /// <summary>The roles one at a time, in table order - what a translation picks between.</summary>
        public static IEnumerable<ArtRole> Each(ArtRole roles)
        {
            foreach (ArtRole r in Enum.GetValues(typeof(ArtRole)))
                if (r != ArtRole.None && (roles & r) != 0) yield return r;
        }

        /// <summary>The orbiting arts (Inward Orbit), the ground-striking ones (Seam), the run-through
        /// ones (Skewer) and the kneeling shots (Kneeling Shot).</summary>
        public static bool Orbits(string id) => id is "disc_orrery" or "disc_revolve" or "disc_carousel";
        public static bool StrikesGround(string id) => id is "hammer" or "ruin" or "meteor";
        public static bool RunsThrough(string id) => id is "lunge" or "impale";
        public static bool Kneels(string id) => id is "bow_pin" or "bow_longshot";
    }

    public static class DealKinds
    {
        public static readonly DealKind[] Boons =
        {
            DealKind.ArtRole, DealKind.ArtCraft, DealKind.Chain, DealKind.Release, DealKind.Guard,
            DealKind.Humour, DealKind.Vessel, DealKind.Fortune,
        };

        public static readonly DealKind[] Costs =
        {
            DealKind.Frailty, DealKind.Toll, DealKind.Rule, DealKind.Host, DealKind.Ground, DealKind.Socket,
        };

        public static bool IsArt(DealKind k) => k == DealKind.ArtRole || k == DealKind.ArtCraft;

        /// <summary>The slot a socket cost of this kind of boon is paid on - what the deal pairs.</summary>
        public static FormSlot SocketSlotFor(ExchangeEntry boon)
        {
            if (boon == null) return FormSlot.None;
            if (boon.Slot != FormSlot.None) return boon.Slot;
            return boon.Deal == DealKind.Release ? FormSlot.Release : FormSlot.None;
        }

        public static string Label(DealKind k) => k switch
        {
            DealKind.ArtRole => "ART ROLE",
            DealKind.ArtCraft => "ART CRAFT",
            DealKind.SetPiece => "SET PIECE",
            _ => k.ToString().ToUpperInvariant(),
        };
    }
}
