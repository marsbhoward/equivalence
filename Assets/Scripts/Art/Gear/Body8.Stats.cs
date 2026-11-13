using UnityEngine;
using UnityEngine.UI;
using Convergence.Core;

namespace Convergence.Art.Gear
{
    // DEV (Tuning.Testing.PoseCacheStats): how the pose cache is doing, for the phone test. Counted always (four
    // integers), shown only in the Editor, development builds and the perf-test build while eight directions is on.
    public static partial class Body8
    {
        static int _statReused, _statDrawn, _statFrames, _statEvicted;

        /// <summary>The counters since the game started (reused, drawn and kept, swing frames, dropped) and the cache now.</summary>
        public static void PoseStats(out int reused, out int drawn, out int frames, out int evicted, out int count, out long gpu, out long cpu)
        {
            reused = _statReused; drawn = _statDrawn; frames = _statFrames; evicted = _statEvicted;
            CacheMemory(out count, out gpu, out cpu);
        }

        public const int PoseCacheSize = CacheSize;

        /// <summary>The cache's pictures: how many, their texture memory, and how much of it is also kept on the CPU
        /// (a texture left readable keeps a copy of its pixels there).</summary>
        static void CacheMemory(out int count, out long gpu, out long cpu)
        {
            count = _cache?.Count ?? 0;
            long g = 0, c = 0;
            void Add(Sprite s)
            {
                if (s == null || s.texture == null) return;
                long b = (long)s.texture.width * s.texture.height * 4;
                g += b; if (s.texture.isReadable) c += b;
            }
            if (_cache != null) foreach (var s in _cache.Values) Add(s);
            if (_cacheOver != null) foreach (var s in _cacheOver.Values) Add(s);
            gpu = g; cpu = c;
        }

        /// <summary>The readout: a line at the top left of the screen (MB: GPU + the CPU copy), updated each second, and a log line every 10 s.</summary>
        sealed class StatsReadout : MonoBehaviour
        {
            Text _label;
            float _since, _logSince;
            int _reused0, _drawn0, _frames0, _evicted0, _logDrawn0, _logEvicted0;
            float _peak;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            static void Spawn()
            {
                if (!Tuning.Testing.PoseCacheStats || !(Application.isEditor || Debug.isDebugBuild || PerfTest.Build)) return;
                var go = new GameObject("PoseCacheStats");
                DontDestroyOnLoad(go);
                go.AddComponent<StatsReadout>();
            }

            void Update()
            {
                bool on = PrimitiveCharacterRig.DevEightDir;
                if (_label == null)
                {
                    if (!on) return;
                    foreach (Transform c in transform) Destroy(c.gameObject);   // a domain reload's leftover
                    var canvas = UI.UiKit.CreateCanvas("PoseCacheStatsCanvas", 900);
                    canvas.transform.SetParent(transform, false);
                    var rt = UI.UiKit.Rect(canvas.transform, "PoseCacheStats", new Vector2(0f, 1f), new Vector2(0f, 1f),
                        new Vector2(24, -46), new Vector2(824, -14));
                    _label = UI.UiKit.Label(rt, "", 18, new Color(0.62f, 0.9f, 1f), TextAnchor.MiddleLeft);
                    _label.raycastTarget = false;
                    UI.UiKit.Legible(_label);
                }
                _label.gameObject.SetActive(on);
                if (!on) return;

                _since += Time.unscaledDeltaTime; _logSince += Time.unscaledDeltaTime;
                if (_since >= 1f)
                {
                    int reused = _statReused - _reused0, drawn = _statDrawn - _drawn0, frames = _statFrames - _frames0, evicted = _statEvicted - _evicted0;
                    _reused0 = _statReused; _drawn0 = _statDrawn; _frames0 = _statFrames; _evicted0 = _statEvicted;
                    float perSec = drawn / _since;
                    _peak = Mathf.Max(_peak, perSec);
                    int asked = reused + drawn;
                    CacheMemory(out int count, out long gpu, out long cpu);
                    _label.text = $"POSES new {perSec:0.0}/s (peak {_peak:0.0})  reused {(asked > 0 ? 100f * reused / asked : 100f):0}%  " +
                                  $"dropped {evicted / _since:0.0}/s  {count}/{CacheSize}  {gpu / 1048576f:0.0}" +
                                  $"{(cpu > 0 ? $"+{cpu / 1048576f:0.0}" : "")} MB  swing {frames / _since:0}/s";
                    _since = 0f;
                }
                if (_logSince >= 10f)
                {
                    CacheMemory(out int count, out long gpu, out long cpu);
                    Debug.Log($"[PoseCache] 10 s: {_statDrawn - _logDrawn0} new poses, {_statEvicted - _logEvicted0} dropped, " +
                              $"cache {count}/{CacheSize}, {gpu / 1048576f:0.0} MB GPU + {cpu / 1048576f:0.0} MB CPU, peak {_peak:0.0}/s");
                    _logDrawn0 = _statDrawn; _logEvicted0 = _statEvicted; _logSince = 0f;
                }
            }
        }
    }
}
