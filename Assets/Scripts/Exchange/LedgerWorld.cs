using UnityEngine;

namespace Convergence.Exchange
{
    /// <summary>
    /// What the ledger's WORLD costs (the overhaul's Host kind - Swift, Eager, Crowded, Affixed,
    /// Wrath, Vigilant) do to the enemies, as the enemies ask it. Read off the run's live ledger
    /// (RunModifiers.Live); with none - the hub, a balance simulation - every answer is the plain one.
    /// Asked, never written, so nothing here can drift from the ledger.
    /// </summary>
    public static class LedgerWorld
    {
        static Mods M => RunModifiers.Live?.Current;

        /// <summary>Swift: enemies' walking speed (elites a little more at III).</summary>
        public static float MoveMul(bool elite)
        {
            var m = M;
            if (m == null) return 1f;
            float mul = m.EnemyMoveMul;
            if (elite && mul > 1f && RunModifiers.Live.AtMax("co_swift")) mul += Core.Tuning.Overhaul.SwiftMove;
            return mul;
        }

        /// <summary>Eager: more enemies may hold an attack turn at once.</summary>
        public static int ExtraTurns => M?.ExtraTurns ?? 0;

        /// <summary>Frenzy (Eager's Nigredo): the gap between two enemies' turns is halved.</summary>
        public static float TurnGapMul => RunModifiers.Live != null && RunModifiers.Live.AtMax("co_eager") ? 0.5f : 1f;

        /// <summary>Crowded: added to the live pressure cap.</summary>
        public static float PressureAdd => M?.PressureAdd ?? 0f;

        /// <summary>Affixed: an elite's chance to roll its modifier.</summary>
        public static float AffixChance(int band, float plain)
        {
            var m = M;
            if (m == null || m.AffixChanceAdd <= 0f) return plain;
            if (plain <= 0f && !m.AffixFromBandOne) return plain;
            return Mathf.Clamp01(plain + m.AffixChanceAdd);
        }

        /// <summary>Volley: bolts a ranged enemy adds to each shot.</summary>
        public static int ExtraBolts => M?.ExtraBolts ?? 0;

        /// <summary>Twisted: bands the next mini-boss arrives above its own.</summary>
        public static int BossBandAdd => M?.BossBandAdd ?? 0;

        /// <summary>Pit Trap (Encroach's Albedo): how much harder pits bite an enemy.</summary>
        public static float EnemyPitMul => RunModifiers.Live != null && RunModifiers.Live.StacksOf("al_encroach") > 0
            ? Core.Tuning.Overhaul.EncroachEnemyPitMul : 1f;

        /// <summary>Plated: armour an unarmoured enemy arrives with, as a share of its health (0 for none).</summary>
        public static float PlatedShare(bool elite)
        {
            var m = M;
            if (m == null || m.PlatedShare <= 0f) return 0f;
            return !elite || RunModifiers.Live.AtMax("co_plated") ? m.PlatedShare : 0f;
        }

        /// <summary>Ambush (Fortune): seconds the first wave stays unaware longer.</summary>
        public static float UnawareExtra => RunModifiers.Live != null && RunModifiers.Live.StacksOf("fo_ambush") > 0
            ? Core.Tuning.Overhaul.AmbushUnawareSeconds : 0f;

        /// <summary>Vigilant: the first wave arrives alert.</summary>
        public static bool NoUnaware => M != null && M.NoUnaware;

        /// <summary>Wrath: how fast this body's attack cooldowns run (1 plain, more while enraged).</summary>
        public static float CooldownTempo(Combat.Health body, bool elite)
        {
            var m = M;
            if (m == null || m.WrathCooldownMul >= 0.999f || body == null || body.Max <= 0f) return 1f;
            bool all = RunModifiers.Live.AtMax("co_wrath");
            if (!elite && !all) return 1f;
            if (body.Current / body.Max > Core.Tuning.Overhaul.WrathBelow) return 1f;
            return 1f / Mathf.Max(0.2f, m.WrathCooldownMul);
        }
    }
}
