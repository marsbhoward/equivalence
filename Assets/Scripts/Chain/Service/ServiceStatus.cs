using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain.Service
{
    /// <summary>
    /// What the service has paused (web/service/src/switches.ts): one feature off while the rest of
    /// the game plays on. Read in the hub and before a run starts; the banner says what is paused,
    /// and a paused RUNS switch keeps a new run from starting. Also the oldest build it serves - an
    /// older one is told to reload (<see cref="ServiceClient.Outdated"/>).
    /// </summary>
    public static class ServiceStatus
    {
        /// <summary>A paused switch's message, or null while it is open.</summary>
        public static string Runs { get; private set; }
        public static string Minting { get; private set; }
        public static string Saves { get; private set; }

        /// <summary>The run format this instance plays ("short", or "showcase" on the showcase's own
        /// service - web/service/src/runFormat.ts primaryFormat). Null until read.</summary>
        public static string Format { get; private set; }

        /// <summary>One line for the banner - every paused feature's message, or "".</summary>
        public static string Banner { get; private set; } = "";

        const float FreshSeconds = 20f;
        static float _readAt = -999f;

        [System.Serializable] class Paused { public string runs; public string minting; public string saves; }
        [System.Serializable] class Reply { public Paused paused; public int minClient; public string format; }

        /// <summary>Asks the service again, unless it was asked moments ago (<paramref name="force"/>
        /// asks anyway - before a run starts). A failure leaves the last answer standing.</summary>
        public static async Task RefreshAsync(bool force = false)
        {
            if (!StoreFactory.UsingService) return;
            if (!force && Time.realtimeSinceStartup - _readAt < FreshSeconds) return;
            _readAt = Time.realtimeSinceStartup;
            var r = await ServiceClient.Get("/status", null);
            if (!r.Ok) return;
            Reply reply;
            try { reply = JsonUtility.FromJson<Reply>(r.Body); }
            catch { return; }
            var p = reply?.paused ?? new Paused();
            Runs = Nonempty(p.runs);
            Minting = Nonempty(p.minting);
            Saves = Nonempty(p.saves);
            Format = Nonempty(reply?.format);
            // The showcase instance plays nothing else - the hub reads the format too (the mastery cap).
            if (Format == "showcase") Core.RunFormat.Current = Core.RunFormatId.Showcase;
            Banner = string.Join("   ", new[] { Saves, Runs, Minting }.Where(m => m != null));
            if (reply != null && reply.minClient > ServiceClient.ClientBuild) ServiceClient.ReportOutdated();
        }

        static string Nonempty(string s) => string.IsNullOrEmpty(s) ? null : s;
    }
}
