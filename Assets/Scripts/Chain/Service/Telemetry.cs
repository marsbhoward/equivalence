using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Convergence.Chain.Service
{
    /// <summary>
    /// ANONYMOUS RUN EVENTS for the service (web/service/src/routes/telemetry.ts, SHOWCASE.md
    /// "Telemetry") - what a public showcase cannot ask its players: where runs end and why, which
    /// boss, the deals taken, the timing bar, frame times, what broke.
    ///
    /// Events queue here and go once a floor (and at the run's end) as one POST with no retry - a
    /// batch lost to an outage is lost, and nothing waits on it. Every call is safe at any time: off
    /// the browser build, or with no run begun, it does nothing; nothing here may throw into the
    /// game (each entry point is guarded).
    ///
    /// Who: a hash of the character id, never the wallet, the session or the id itself.
    ///
    /// Domain reload: every field is non-readonly and re-derived lazily (ENGINEERING.md).
    /// </summary>
    public static class Telemetry
    {
        public static bool On => Core.Tuning.Telemetry.Enabled && StoreFactory.UsingService;

        static string _run, _who;
        static List<string> _queue;
        static int _perfect, _good, _missed;
        static int _errors;
        static HashSet<string> _errorsSeen;
        static bool _logHooked;
        static float _floorDamage;
        static Combat.Health _watched;
        static string _boss;
        static int _bossBand;
        static float _bossAt;
        static float _puzzleAt;
        static string _puzzle;
        static Sampler _sampler;

        // ---------------------------------------------------------------- the run

        /// <summary>A run begins (or is resumed): the batch's ids, and its run_start event.</summary>
        public static Event BeginRun(string runId, string profileId)
        {
            if (!On) return Event.None;
            try
            {
                Flush();
                _run = runId;
                _who = Hash(profileId);
                _queue ??= new List<string>();
                _queue.Clear();
                _perfect = _good = _missed = 0;
                _errors = 0;
                (_errorsSeen ??= new HashSet<string>()).Clear();
                _floorDamage = 0f;
                _boss = _puzzle = null;
                HookLog();
                EnsureSampler();
                _sampler.Reset();
                return Ev("run_start").S("dev", Device())
                    .I("vw", Screen.width).I("vh", Screen.height);
            }
            catch { return Event.None; }
        }

        /// <summary>The run ended: run_end with the run's tallies, sent at once. <paramref name="how"/>
        /// is "death", "extract" or "abandon".</summary>
        public static void EndRun(string how, int floor, float clock, int kills, Combat.DamageInfo? killing)
        {
            if (!On || _run == null) return;
            try
            {
                if (_boss != null) BossOver(how == "death" ? "died" : how);
                if (_puzzle != null) PuzzleOver(false);
                var e = Ev("run_end").S("how", how).I("f", floor).N("clk", clock).I("kills", kills)
                    .I("perfect", _perfect).I("good", _good).I("missed", _missed).I("exc", _errors);
                if (how == "death" && killing.HasValue) e.S("by", Killer(killing.Value));
                e.Send();
                AddFrames(Ev("frames").I("f", floor)).Send();
                Flush();
                _run = null;
            }
            catch { /* telemetry never breaks a run's end */ }
        }

        /// <summary>A floor cleared: what it was and how it went, frame times since the last, then
        /// the batch goes.</summary>
        public static void FloorCleared(int floor, string category, string rift, string biome, float clock, float hpFraction)
        {
            if (!On || _run == null) return;
            try
            {
                var e = Ev("floor").I("f", floor).S("cat", category).S("rift", rift).S("biome", biome)
                    .N("clk", clock).N("hp", hpFraction).N("dmg", _floorDamage);
                AddFrames(e).Send();
                _floorDamage = 0f;
                Flush();
            }
            catch { }
        }

        /// <summary>The player's Health this run - damage taken per floor is read off it.</summary>
        public static void WatchPlayer(Combat.Health hp)
        {
            if (!On || hp == null) return;
            if (_watched != null) _watched.Damaged -= OnPlayerDamaged;
            _watched = hp;
            hp.Damaged += OnPlayerDamaged;
        }

        static void OnPlayerDamaged(Combat.DamageInfo d) => _floorDamage += Mathf.Max(0f, d.Amount);

        // ---------------------------------------------------------------- moments

        public static void BossStarted(Bosses.BossKind kind, int band)
        {
            if (!On || _run == null) return;
            _boss = kind.ToString();
            _bossBand = band;
            _bossAt = Time.time;
        }

        /// <summary>The boss fight ended: "won", "died", or how the run left it.</summary>
        public static void BossOver(string outcome)
        {
            if (!On || _run == null || _boss == null) return;
            try { Ev("boss").S("boss", _boss).I("band", _bossBand).S("outcome", outcome).N("s", Time.time - _bossAt).Send(); }
            catch { }
            _boss = null;
        }

        public static void PuzzleStarted(Puzzles.PuzzleKind kind)
        {
            if (!On || _run == null) return;
            _puzzle = kind.ToString();
            _puzzleAt = Time.time;
        }

        public static void PuzzleOver(bool solved)
        {
            if (!On || _run == null || _puzzle == null) return;
            try { Ev("puzzle").S("kind", _puzzle).B("solved", solved).N("s", Time.time - _puzzleAt).Send(); }
            catch { }
            _puzzle = null;
        }

        /// <summary>A Rift answered: "extract" or "push".</summary>
        public static void Rift(int floor, string rift, string choice, int secured)
        {
            if (!On || _run == null) return;
            try { Ev("rift").I("f", floor).S("rift", rift).S("choice", choice).I("secured", secured).Send(); }
            catch { }
        }

        /// <summary>A deal answered - the boon and cost taken, or both null for a refusal.</summary>
        public static void Deal(int floor, string boon, string cost, int offered)
        {
            if (!On || _run == null) return;
            try
            {
                var e = Ev("deal").I("f", floor).I("offered", offered);
                if (boon != null) e.S("boon", boon);
                if (cost != null) e.S("cost", cost);
                e.Send();
            }
            catch { }
        }

        /// <summary>A weapon art's timing bar settled.</summary>
        public static void Strike(Combat.StrikeVerdict v)
        {
            if (v == Combat.StrikeVerdict.Perfect) _perfect++;
            else if (v == Combat.StrikeVerdict.Missed) _missed++;
            else if (v != Combat.StrikeVerdict.Pending) _good++;
        }

        /// <summary>A card on the showcase's closing screen pressed ("discord", "cashapp", "venmo",
        /// "ada", "continue"). Sent at once - the run ends right after.</summary>
        public static void Card(string card)
        {
            if (!On || _run == null) return;
            try { Ev("card").S("card", card).Send(); Flush(); }
            catch { }
        }

        // ---------------------------------------------------------------- events

        /// <summary>One event, built a field at a time - nothing is queued until <see cref="Send"/>.</summary>
        public sealed class Event
        {
            public static readonly Event None = new(null);
            readonly StringBuilder _sb;

            internal Event(string kind)
            {
                if (kind == null) return;
                _sb = new StringBuilder("{\"t\":").Append(ServiceClient.Quote(kind));
            }

            Event Key(string k) { _sb.Append(",\"").Append(k).Append("\":"); return this; }
            public Event I(string k, int v) { if (_sb != null) Key(k)._sb.Append(v.ToString(CultureInfo.InvariantCulture)); return this; }
            public Event N(string k, float v)
            {
                if (_sb != null && !float.IsNaN(v) && !float.IsInfinity(v))
                    Key(k)._sb.Append(System.Math.Round(v, 2).ToString(CultureInfo.InvariantCulture));
                return this;
            }
            public Event S(string k, string v) { if (_sb != null && v != null) Key(k)._sb.Append(ServiceClient.Quote(v.Length > 100 ? v[..100] : v)); return this; }
            public Event B(string k, bool v) { if (_sb != null) Key(k)._sb.Append(v ? "true" : "false"); return this; }

            public void Send()
            {
                if (_sb == null || _run == null) return;
                (_queue ??= new List<string>()).Add(_sb.Append('}').ToString());
                if (_queue.Count >= Core.Tuning.Telemetry.FlushAt) Flush();
            }
        }

        static Event Ev(string kind) => On && _run != null ? new Event(kind) : Event.None;

        /// <summary>The queue, posted now - fire and forget.</summary>
        public static void Flush()
        {
            if (_queue == null || _queue.Count == 0 || _run == null) return;
            var sb = new StringBuilder("{\"run\":").Append(ServiceClient.Quote(_run))
                .Append(",\"who\":").Append(ServiceClient.Quote(_who))
                .Append(",\"build\":").Append(ServiceClient.Quote($"{Application.version}.{ServiceClient.ClientBuild}"))
                .Append(",\"format\":").Append(ServiceClient.Quote(Core.RunFormat.Current.ToString().ToLowerInvariant()))
                .Append(",\"events\":[").Append(string.Join(",", _queue)).Append("]}");
            _queue.Clear();
            _ = ServiceClient.PostOnce("/telemetry", sb.ToString());
        }

        // ---------------------------------------------------------------- what is measured

        static Event AddFrames(Event e)
        {
            if (_sampler == null) return e;
            e.I("fr", _sampler.Frames).I("f33", _sampler.Slow).I("f50", _sampler.VerySlow).I("f100", _sampler.Stalls)
             .N("worst", _sampler.WorstMs);
            _sampler.Reset();
            return e;
        }

        static void EnsureSampler()
        {
            if (_sampler != null) return;
            var go = new GameObject("Telemetry") { hideFlags = HideFlags.HideAndDontSave };
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            _sampler = go.AddComponent<Sampler>();
        }

        /// <summary>Frame times, bucketed - unscaled, so a pause or hitstop is not a slow frame.</summary>
        sealed class Sampler : MonoBehaviour
        {
            public int Frames, Slow, VerySlow, Stalls;
            public float WorstMs;

            public void Reset() { Frames = Slow = VerySlow = Stalls = 0; WorstMs = 0f; }

            void Update()
            {
                if (_run == null) return;
                float ms = Time.unscaledDeltaTime * 1000f;
                Frames++;
                if (ms > Core.Tuning.Telemetry.SlowFrameMs) Slow++;
                if (ms > Core.Tuning.Telemetry.VerySlowFrameMs) VerySlow++;
                if (ms > Core.Tuning.Telemetry.StallMs) Stalls++;
                if (ms > WorstMs) WorstMs = ms;
            }
        }

        static void HookLog()
        {
            if (_logHooked) return;
            _logHooked = true;
            Application.logMessageReceived += OnLog;
        }

        /// <summary>An exception logged during a run: counted, and the first few distinct ones sent.</summary>
        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception || _run == null || !On) return;
            try
            {
                _errors++;
                string line = (message ?? "").Split('\n')[0];
                string where = (stack ?? "").Split('\n')[0];
                string key = line.Length > 60 ? line[..60] : line;
                if (_errorsSeen.Count >= Core.Tuning.Telemetry.ErrorsPerRun || !_errorsSeen.Add(key)) return;
                Ev("error").S("msg", line).S("at", where).Send();
            }
            catch { }
        }

        static string Killer(Combat.DamageInfo d)
        {
            var s = d.Source;
            if (s == null) return "unknown";
            var enemy = s.GetComponentInParent<Enemies.EnemyController>();
            if (enemy != null) return enemy.Elite ? enemy.Kind + "+elite" : enemy.Kind.ToString();
            if (s.GetComponentInParent<Bosses.Boss>() != null) return "boss:" + (_boss ?? "?");
            return s.name.Replace("(Clone)", "").Trim();
        }

        static string Device()
        {
            if (Application.isEditor) return "editor";
            if (Application.platform != RuntimePlatform.WebGLPlayer) return "native";
            if (!Application.isMobilePlatform) return "web";
            float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
            return Mathf.Min(Screen.width, Screen.height) / dpi >= Core.Tuning.Telemetry.PadInches ? "pad" : "phone";
        }

        static string Hash(string id)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes("equivalence-telemetry:" + (id ?? "")));
            var sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
