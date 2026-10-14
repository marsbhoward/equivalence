using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Convergence.Chain
{
    /// <summary>
    /// Sends a run's writes to its <see cref="IRunStore"/> ONE AT A TIME, newest first, so they can
    /// never land out of order and a slow connection never queues a backlog:
    ///
    ///     an END             cancels anything still waiting - the run is over
    ///     a SNAPSHOT         replaces a snapshot still waiting (only the newest matters), and drops
    ///                        a waiting beat (a beat belongs to the snapshot before it)
    ///     a BEAT             only when nothing else is waiting; replaces a waiting beat
    ///
    /// A write that fails is retried every <see cref="RetrySeconds"/> (polled through
    /// <see cref="Tick"/> - no timers, which a WebGL build does not have) until a newer one replaces
    /// it; one <see cref="RunWrite.Refused"/> is dropped (a retry can't fix it). A
    /// <see cref="RunWrite.Superseded"/> answer stops everything and calls <see cref="Superseded"/>
    /// once: another session has the run.
    ///
    /// Not serializable, and it needn't be: a domain reload drops the instance, the bootstrap
    /// builds another, and at worst one write in flight is sent again with the next snapshot.
    /// </summary>
    public class RunKeeper
    {
        public const float RetrySeconds = 3f;

        readonly Func<IRunStore> _store;

        /// <summary>Another session took the run over. Called once, on the main thread.</summary>
        public Action Superseded;

        string _profileId, _session;
        string _snapshotJson;
        RunSnapshot _snapshot;
        RunBeat _beat;
        string _endRunId;
        /// <summary>Counts each Save/Beat - what is sent is cleared afterwards only if nothing newer
        /// replaced it meanwhile (the same snapshot object may be saved again, changed).</summary>
        int _snapshotVersion, _beatVersion;
        bool _busy, _dead;
        float _retryAt = -1f;

        public RunKeeper(Func<IRunStore> store) => _store = store;

        /// <summary>Nothing waiting and nothing in flight.</summary>
        public bool Idle => !_busy && _snapshot == null && _beat == null && _endRunId == null;

        /// <summary>The run was taken over elsewhere - nothing more is sent for it.</summary>
        public bool Dead => _dead;

        /// <summary>Writes waiting on a retry - the store could not be reached.</summary>
        public bool Failing => _retryAt >= 0f;

        /// <summary>The store refused a snapshot of this run outright (it never saw the run start) -
        /// the run plays on, but nothing of it can be resumed.</summary>
        public bool Refusing { get; private set; }

        /// <summary>A new run (or a resumed one) under this session. Clears anything left from the last.</summary>
        public void Begin(string profileId, string session)
        {
            _profileId = profileId;
            _session = session;
            _snapshot = null; _snapshotJson = null; _beat = null; _endRunId = null;
            _dead = false;
            Refusing = false;
            _retryAt = -1f;
        }

        /// <summary>The snapshot is serialised NOW - the run keeps changing while it waits to be sent.</summary>
        public void Save(RunSnapshot snapshot)
        {
            if (_dead || snapshot == null) return;
            _snapshotJson = JsonUtility.ToJson(snapshot);
            _snapshot = snapshot;
            _snapshotVersion++;
            _beat = null;
            Pump();
        }

        /// <summary>False when it was not taken - a snapshot or the end is waiting ahead of it.</summary>
        public bool Beat(RunBeat beat)
        {
            if (_dead || beat == null || _snapshot != null || _endRunId != null) return false;
            _beat = beat;
            _beatVersion++;
            Pump();
            return true;
        }

        public void End(string runId)
        {
            if (_dead || string.IsNullOrEmpty(runId)) return;
            _snapshot = null; _snapshotJson = null; _beat = null;
            _endRunId = runId;
            Pump();
        }

        /// <summary>Polled by the bootstrap: a failed write is tried again once its wait is up.</summary>
        public void Tick(float now)
        {
            if (_retryAt >= 0f && now >= _retryAt && !_busy)
            {
                _retryAt = -1f;
                Pump();
            }
        }

        /// <summary>Waits (polling, unscaled) until everything is sent or <paramref name="seconds"/>
        /// pass. True when it all went.</summary>
        public async Task<bool> FlushAsync(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!Idle && !_dead && Time.realtimeSinceStartup < until)
            {
                if (Failing && !_busy) { _retryAt = -1f; Pump(); }
                await Task.Yield();
            }
            return Idle;
        }

        async void Pump()
        {
            if (_busy || _dead || _retryAt >= 0f) return;
            var store = _store();
            if (store == null || string.IsNullOrEmpty(_profileId)) return;
            _busy = true;
            try
            {
                while (!_dead)
                {
                    RunWrite result;
                    if (_endRunId != null)
                    {
                        string runId = _endRunId;
                        result = await store.EndAsync(_profileId, runId, _session);
                        if (result != RunWrite.Failed && _endRunId == runId) _endRunId = null;
                        if (result == RunWrite.Refused) result = RunWrite.Ok;
                    }
                    else if (_snapshot != null)
                    {
                        var s = _snapshot;
                        int version = _snapshotVersion;
                        result = await store.SaveAsync(_profileId, _snapshotJson, s, _session);
                        if (result != RunWrite.Failed && _snapshotVersion == version) { _snapshot = null; _snapshotJson = null; }
                        if (result == RunWrite.Refused) { Refusing = true; result = RunWrite.Ok; }
                    }
                    else if (_beat != null)
                    {
                        var b = _beat;
                        int version = _beatVersion;
                        result = await store.BeatAsync(_profileId, b, _session);
                        // A failed beat is not retried - the next one carries newer numbers anyway.
                        if (_beatVersion == version) _beat = null;
                        if (result is RunWrite.Failed or RunWrite.Refused) result = RunWrite.Ok;
                    }
                    else break;

                    if (result == RunWrite.Superseded)
                    {
                        _dead = true;
                        _snapshot = null; _snapshotJson = null; _beat = null; _endRunId = null;
                        Debug.LogWarning("[RunStore] another session has taken this run over");
                        Superseded?.Invoke();
                        break;
                    }
                    if (result == RunWrite.Failed)
                    {
                        _retryAt = Time.unscaledTime + RetrySeconds;
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunStore] write failed: {e.Message}");
                _retryAt = Time.unscaledTime + RetrySeconds;
            }
            finally { _busy = false; }
        }
    }
}
