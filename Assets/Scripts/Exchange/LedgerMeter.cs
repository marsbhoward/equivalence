using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Convergence.Combat;

namespace Convergence.Exchange
{
    /// <summary>
    /// THE LEDGER'S METER (the overhaul's tuning, 2026-11-04): what each entry actually did in a
    /// played run - the damage its own strikes dealt (DamageInfo.Entry), the extra its hit
    /// multipliers added (RunEffects.Boost, before the target's mitigation), and the health a cost
    /// took. Measured against the player's whole output and intake, so a share reads straight
    /// against the Assay's StackTarget (5/10/15% a stack). Logged as [Ledger] at each floor clear and
    /// appended to Logs/ledger-meter.txt in the Editor. Measurement only: nothing in the game reads it.
    /// Not measured: status ticks by source (counted in the total), control seconds, damage prevented.
    /// </summary>
    public static class LedgerMeter
    {
        static readonly Dictionary<string, float> _floor = new(), _run = new();
        static float _dealt, _taken, _runDealt, _runTaken;
        static GameObject _player;

        /// <summary>The run's player, whose hits count as dealt and whose health as taken.</summary>
        public static void Watch(GameObject player)
        {
            _player = player;
            Health.Landed -= OnLanded;
            Health.Landed += OnLanded;
        }

        public static void RunStarted()
        {
            _floor.Clear(); _run.Clear();
            _dealt = _taken = _runDealt = _runTaken = 0f;
        }

        static void OnLanded(Health h, DamageInfo info, float amount)
        {
            if (_player == null || amount <= 0f) return;
            if (h.gameObject == _player)
            {
                _taken += amount; _runTaken += amount;
                if (info.Entry != null) Credit(info.Entry, amount);
                else if (info.Price) Credit("(paid)", amount);
                return;
            }
            if (info.Source != _player && info.Entry == null) return;
            _dealt += amount; _runDealt += amount;
            if (info.Entry != null) Credit(info.Entry, amount);
        }

        /// <summary>A hit multiplier's extra (RunEffects.Boost) - already inside the hit's total.</summary>
        public static void Added(string id, float amount)
        {
            if (amount > 0f && id != null) Credit(id, amount);
        }

        static void Credit(string id, float amount)
        {
            _floor.TryGetValue(id, out var a); _floor[id] = a + amount;
            _run.TryGetValue(id, out var b); _run[id] = b + amount;
        }

        /// <summary>A floor cleared: log what each entry did on it, then start the next.</summary>
        public static void FloorCleared(int floor)
        {
            if (_dealt <= 0f && _floor.Count == 0) return;
            string line = $"[Ledger] floor {floor}: " + Describe(_floor, _dealt, _taken);
            Debug.Log(line);
            Append(line);
            _floor.Clear();
            _dealt = _taken = 0f;
        }

        /// <summary>The run so far, every entry (dev eval: LedgerMeter.Report()).</summary>
        public static string Report() => "run: " + Describe(_run, _runDealt, _runTaken);

        static string Describe(Dictionary<string, float> d, float dealt, float taken)
        {
            var sb = new StringBuilder($"dealt {dealt:0}, taken {taken:0}");
            foreach (var kv in d.OrderByDescending(k => k.Value))
            {
                bool cost = kv.Key.StartsWith("co_") || kv.Key == "(paid)";
                float of = cost ? taken : dealt;
                sb.Append($" | {kv.Key} {kv.Value:0} ({(of > 0f ? kv.Value / of : 0f):P0} of {(cost ? "taken" : "dealt")})");
            }
            return sb.ToString();
        }

        static void Append(string line)
        {
#if UNITY_EDITOR
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/ledger-meter.txt"),
                                               System.DateTime.Now.ToString("s") + " " + line + "\n"); }
            catch (System.Exception) { }
#endif
        }
    }
}
