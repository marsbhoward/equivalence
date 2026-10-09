using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain.Web
{
    /// <summary>
    /// Arguments for every connector call, as ONE type with named optional fields rather than a
    /// type per method.
    ///
    /// JsonUtility needs a concrete class to serialise, so a shape per call would be nine of
    /// them; and the TypeScript side reads only the fields its method cares about, so an unused
    /// "" costs nothing and the wire stays self-describing when you are reading it in a browser
    /// console at two in the morning.
    /// </summary>
    [Serializable]
    public class ChainArgs
    {
        public string profileId = "";
        public string accountId = "";
        public string wallet = "";
        public string runId = "";
        public string element = "";

        /// <summary>The CIP-68 datum itself - a serialised CharacterProfile or AccountProfile.</summary>
        public string datum = "";

        public string summary = "";

        /// <summary>A design-box open: the box tier - an enum NAME, never an ordinal, since the
        /// service reads it. Always random; there is no targeted open.</summary>
        public string tier = "";
        /// <summary>How many boxes one open takes (1-10), as text like every other field.</summary>
        public string count = "";
    }

    [Serializable]
    class ChainResponse
    {
        public string id;
        public bool ok;
        public string value;
        public string error;
    }

    /// <summary>
    /// The single point where the game talks to the browser.
    ///
    /// Nothing above this knows the chain exists: <see cref="ChainProfileStore"/> and
    /// <see cref="ChainAccountStore"/> implement the same interfaces LocalJson does, so
    /// GameBootstrap's awaits are unchanged. This class only converts Unity's one-way JS
    /// interop into a Task.
    ///
    /// OUTSIDE A WEBGL PLAYER THIS IS INERT. <see cref="Available"/> is false in the Editor and
    /// on every native target, which is what lets StoreFactory fall back to local JSON without
    /// anything else branching.
    /// </summary>
    public static class WebChainBridge
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int EquivalenceChainAvailable();
        [DllImport("__Internal")] static extern void EquivalenceChainCall(string reqId, string method, string payload);
#else
        // Editor and native builds compile against stubs rather than #if-ing every call site.
        static int EquivalenceChainAvailable() => 0;
        static void EquivalenceChainCall(string reqId, string method, string payload) { }
#endif

        /// <summary>
        /// How long a request may stay outstanding before it is failed.
        ///
        /// A chain write that never answers must not leave an await hanging forever - the run-end
        /// checkpoint is awaited on the path back to the hub, so a lost response would strand the
        /// player on a dead screen with no error. Cardano blocks average ~20s; 90 covers a slow
        /// settle plus a wallet dialog the player is reading.
        /// </summary>
        public const float TimeoutSeconds = 90f;

        /// <summary>
        /// Deliberately NOT readonly, and null-guarded on every use - the documented trap. A
        /// static collection is wiped by a domain reload while everything around it survives,
        /// and an emptied request table means every outstanding await never completes.
        /// </summary>
        static Dictionary<string, Pending> _pending = new();

        class Pending
        {
            public TaskCompletionSource<string> Task;
            public float Deadline;
            public string Method;
        }

        static int _nextId;

        /// <summary>True only in a browser build whose page actually supplied a connector.</summary>
        public static bool Available
        {
            get
            {
                try { return EquivalenceChainAvailable() != 0; }
                catch (Exception e)
                {
                    // EntryPointNotFoundException if the jslib did not link - worth one line,
                    // because the symptom otherwise is "the chain silently never works".
                    Debug.LogWarning($"[Chain] bridge unavailable: {e.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Call a connector method and await its result. The returned string is whatever the
        /// TypeScript side resolved with - a JSON datum, a transaction hash, an address.
        /// </summary>
        public static Task<string> Call(string method, ChainArgs args = null)
        {
            _pending ??= new Dictionary<string, Pending>();
            ChainBridgeReceiver.Ensure();

            var id = (++_nextId).ToString();
            var tcs = new TaskCompletionSource<string>();
            _pending[id] = new Pending
            {
                Task = tcs,
                Deadline = Time.realtimeSinceStartup + TimeoutSeconds,
                Method = method,
            };

            try
            {
                EquivalenceChainCall(id, method, JsonUtility.ToJson(args ?? new ChainArgs()));
            }
            catch (Exception e)
            {
                _pending.Remove(id);
                tcs.TrySetException(new ChainException($"{method} could not be dispatched: {e.Message}"));
            }

            return tcs.Task;
        }

        /// <summary>Called from the receiver, which is called from the jslib by name.</summary>
        internal static void Deliver(string json)
        {
            ChainResponse r;
            try { r = JsonUtility.FromJson<ChainResponse>(json); }
            catch (Exception e) { Debug.LogError($"[Chain] unreadable response: {e.Message}\n{json}"); return; }

            if (r == null || string.IsNullOrEmpty(r.id)) return;
            if (_pending == null || !_pending.TryGetValue(r.id, out var p)) return;   // late, or reloaded away
            _pending.Remove(r.id);

            if (r.ok) p.Task.TrySetResult(r.value ?? "");
            else p.Task.TrySetException(new ChainException($"{p.Method}: {r.error}"));
        }

        /// <summary>
        /// Fail anything past its deadline. Swept rather than timed per request because WebGL has
        /// no threads and a Task has no clock of its own.
        /// </summary>
        internal static void SweepTimeouts()
        {
            if (_pending == null || _pending.Count == 0) return;

            List<string> dead = null;
            float now = Time.realtimeSinceStartup;
            foreach (var kv in _pending)
                if (now > kv.Value.Deadline) (dead ??= new List<string>()).Add(kv.Key);

            if (dead == null) return;
            foreach (var id in dead)
            {
                var p = _pending[id];
                _pending.Remove(id);
                p.Task.TrySetException(new ChainException($"{p.Method}: no response in {TimeoutSeconds:0}s"));
            }
        }

        public static int PendingCount => _pending?.Count ?? 0;
    }

    /// <summary>Anything the connector refused or never answered. Distinct so callers can catch it.</summary>
    public class ChainException : Exception
    {
        public ChainException(string message) : base(message) { }
    }
}
