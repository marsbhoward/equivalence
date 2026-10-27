using System;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// A DESIGNED GROUP: kinds that make each other dangerous, drawn together (Tuning.WaveGroups).
    /// The names are code only - never shown to a player (the user's call, 2026-10-25).
    /// Append-only: a wave is composed from a seed, so reordering would change seeded floors.
    /// </summary>
    public enum WaveGroupId
    {
        None,
        /// <summary>A Watcher and two Hounds: the root holds, the dashes land.</summary>
        WatchersNet,
        /// <summary>A Drowned Bell and an elite Brute: the shielded one walks in.</summary>
        BellGuard,
        /// <summary>An elite Tempest Eye and a Salamander: the mire holds, the shells fall.</summary>
        Artillery,
        /// <summary>Two Stingers and a Hound: the missiles drive the player back onto the dash.</summary>
        Hunt,
        /// <summary>Two Brutes and a Salamander: the Brutes hold the ground the shells land on.</summary>
        Siege,
    }

    /// <summary>
    /// ONE modifier an elite may carry on top of its kind (Tuning.EliteAffixes), so any kind's elite
    /// can come in a new form without new art per kind. Each SHOWS itself; none is named on screen.
    /// Append-only, for the same reason as <see cref="WaveGroupId"/>.
    /// </summary>
    public enum EliteAffix
    {
        None,
        /// <summary>Arrives inside a Drowned Bell's bubble - only a weapon art breaks it.</summary>
        Warded,
        /// <summary>Embers rise off it and it sheds burning ground as it walks (the player's only).</summary>
        Cindered,
        /// <summary>Drips fall off it and it dies into a mire.</summary>
        Mired,
    }

    public static class WaveGroups
    {
        public readonly struct Member
        {
            public readonly EnemyKind Kind;
            public readonly bool Elite;
            public Member(EnemyKind kind, bool elite = false) { Kind = kind; Elite = elite; }
        }

        // Not readonly: a domain reload re-initialises it either way, and nothing holds an element.
        static Member[][] _table;

        /// <summary>The members of <paramref name="id"/>, in arrival order (the anchor first).</summary>
        public static Member[] Of(WaveGroupId id)
        {
            _table ??= new[]
            {
                Array.Empty<Member>(),
                new[] { new Member(EnemyKind.Gargoyle), new Member(EnemyKind.Dasher), new Member(EnemyKind.Dasher) },
                new[] { new Member(EnemyKind.Bubbles), new Member(EnemyKind.Chaser, elite: true) },
                new[] { new Member(EnemyKind.Turret, elite: true), new Member(EnemyKind.Mortar) },
                new[] { new Member(EnemyKind.Ranged), new Member(EnemyKind.Ranged), new Member(EnemyKind.Dasher) },
                new[] { new Member(EnemyKind.Chaser), new Member(EnemyKind.Chaser), new Member(EnemyKind.Mortar) },
            };
            int i = (int)id;
            return i >= 0 && i < _table.Length ? _table[i] : Array.Empty<Member>();
        }

        public static int Count => Enum.GetValues(typeof(WaveGroupId)).Length;

        /// <summary>Whether the group's arrival point is anywhere on the floor (it holds a kind that
        /// never walks in) rather than at the edge.</summary>
        public static bool PlacedOnFloor(WaveGroupId id)
        {
            foreach (var m in Of(id))
                if (m.Kind is EnemyKind.Turret or EnemyKind.Gargoyle or EnemyKind.Bubbles) return true;
            return false;
        }

        /// <summary>Whether an affix suits a kind: a Bell bubbles itself already, an Eye never walks
        /// to shed embers.</summary>
        public static bool Suits(EliteAffix affix, EnemyKind kind) => affix switch
        {
            EliteAffix.Warded   => kind != EnemyKind.Bubbles,
            EliteAffix.Cindered => kind != EnemyKind.Turret,
            EliteAffix.Mired    => true,
            _                   => false,
        };

        public static float AffixChance(int band)
        {
            var t = Tuning.EliteAffixes.Chance;
            return band < Tuning.EliteAffixes.FromBand ? 0f : t[Mathf.Clamp(band, 0, t.Length - 1)];
        }
    }
}
