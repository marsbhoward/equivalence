using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Convergence.Art.Gear;

namespace Convergence.Core
{
    /// <summary>
    /// THE PERF-TEST BUILD (Convergence > Build for Web (perf test), the EQ_PERFTEST symbol, 2026-11-12): the ordinary
    /// web build with eight directions on, saves kept in the browser (no service: the page is served by
    /// Tools/perf-serve on the LAN, which has none), the pose-cache readout on screen, and a report posted every 10 s
    /// and at each floor change to the page's own host (<c>/devlog</c>), where perf-serve writes it to
    /// Builds/perf-logs. Release builds never carry the symbol, so none of this reaches players.
    /// </summary>
    public static class PerfTest
    {
#if EQ_PERFTEST
        public static readonly bool Build = true;
#else
        public static readonly bool Build = false;
#endif

        /// <summary>The accessory the perf-test character wears (2026-11-13): Penumbra's drift and echoes draw
        /// uncached moving frames, the heaviest case for the pose cache. Held in this build only
        /// (GearOwnership.Held), worn when the accessory slot is empty (GameBootstrap.EnsureStarterGear).</summary>
        public const string Accessory = "penumbra_arm";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Arm()
        {
            if (!Build) return;
            PrimitiveCharacterRig.DevEightDir = true;
            Chain.StoreFactory.ForceLocal = true;
            var go = new GameObject("PerfTestReport");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Report>();
        }

        /// <summary>Frame times and the pose cache over each window, posted as one JSON line.</summary>
        sealed class Report : MonoBehaviour
        {
            const float Window = 10f;
            List<float> _ms;
            Player.PlayerController _player;
            Rigidbody2D _body;
            GameBootstrap _boot;
            float _lookAt;
            float _since;
            int _moving, _lastFloor = -1, _gc0;
            int _reused0, _drawn0, _frames0, _evicted0;
            string _url;
            bool _sentHello;

            void Start()
            {
                var page = Application.absoluteURL;
                if (!string.IsNullOrEmpty(page)) _url = new System.Uri(page).GetLeftPart(System.UriPartial.Authority) + "/devlog";
                _gc0 = System.GC.CollectionCount(0);
            }

            void Update()
            {
                if (!_sentHello)
                {
                    _sentHello = true;
                    Post("{\"kind\":\"hello\",\"device\":\"" + Esc(SystemInfo.deviceModel) + "\",\"os\":\"" + Esc(SystemInfo.operatingSystem) +
                         "\",\"gpu\":\"" + Esc(SystemInfo.graphicsDeviceName) + "\",\"screen\":\"" + Screen.width + "x" + Screen.height +
                         "\",\"memMB\":" + SystemInfo.systemMemorySize + "}");
                }
                _ms ??= new List<float>();
                _ms.Add(Time.unscaledDeltaTime * 1000f);
                // the player and the bootstrap, looked for at most once a second while missing (a run builds a new player)
                if ((_player == null || _boot == null) && Time.unscaledTime >= _lookAt)
                {
                    _lookAt = Time.unscaledTime + 1f;
                    if (_player == null) { _player = FindAnyObjectByType<Player.PlayerController>(); _body = _player != null ? _player.GetComponent<Rigidbody2D>() : null; }
                    if (_boot == null) _boot = FindAnyObjectByType<GameBootstrap>();
                }
                if (_body != null && _body.linearVelocity.sqrMagnitude > 0.04f) _moving++;
                _since += Time.unscaledDeltaTime;

                int floor = _boot != null ? _boot.DevFloor : -1;
                bool floorChanged = _lastFloor >= 0 && floor != _lastFloor;
                if (_since >= Window || (floorChanged && _ms.Count > 30)) Send(floorChanged ? "floor-end" : "window", _lastFloor >= 0 ? _lastFloor : floor);
                _lastFloor = floor;
            }

            void Send(string kind, int floor)
            {
                _ms.Sort();
                int n = _ms.Count;
                float sum = 0f; int over33 = 0, over50 = 0;
                foreach (var m in _ms) { sum += m; if (m > 33.4f) over33++; if (m > 50f) over50++; }
                Body8.PoseStats(out int reused, out int drawn, out int frames, out int evicted, out int count, out long gpu, out long cpu);
                int gc = System.GC.CollectionCount(0);
                var sb = new StringBuilder();
                sb.Append("{\"kind\":\"").Append(kind).Append("\",\"t\":").Append(Time.realtimeSinceStartup.ToString("0.0"))
                  .Append(",\"state\":\"").Append(_boot != null ? _boot.DevState : "?").Append("\",\"floor\":").Append(floor)
                  .Append(",\"seconds\":").Append(_since.ToString("0.0")).Append(",\"frames\":").Append(n)
                  .Append(",\"fps\":").Append((n / Mathf.Max(0.01f, _since)).ToString("0.0"))
                  .Append(",\"ms\":{\"mean\":").Append((sum / Mathf.Max(1, n)).ToString("0.0"))
                  .Append(",\"median\":").Append(At(0.5f).ToString("0.0")).Append(",\"p95\":").Append(At(0.95f).ToString("0.0"))
                  .Append(",\"p99\":").Append(At(0.99f).ToString("0.0")).Append(",\"max\":").Append((n > 0 ? _ms[n - 1] : 0f).ToString("0.0"))
                  .Append(",\"over33\":").Append(over33).Append(",\"over50\":").Append(over50).Append('}')
                  .Append(",\"movingShare\":").Append((n > 0 ? (float)_moving / n : 0f).ToString("0.00"))
                  .Append(",\"gcRuns\":").Append(gc - _gc0).Append(",\"heapMB\":").Append((System.GC.GetTotalMemory(false) / 1048576f).ToString("0.0"))
                  .Append(",\"poses\":{\"new\":").Append(drawn - _drawn0).Append(",\"reused\":").Append(reused - _reused0)
                  .Append(",\"dropped\":").Append(evicted - _evicted0).Append(",\"swingFrames\":").Append(frames - _frames0)
                  .Append(",\"cache\":").Append(count).Append(",\"cap\":").Append(Body8.PoseCacheSize)
                  .Append(",\"gpuMB\":").Append((gpu / 1048576f).ToString("0.0")).Append(",\"cpuMB\":").Append((cpu / 1048576f).ToString("0.0"))
                  .Append("}}");
                _reused0 = reused; _drawn0 = drawn; _frames0 = frames; _evicted0 = evicted; _gc0 = gc;
                _ms.Clear(); _moving = 0; _since = 0f;
                Post(sb.ToString());
            }

            float At(float q) => _ms.Count == 0 ? 0f : _ms[Mathf.Clamp(Mathf.FloorToInt(q * (_ms.Count - 1)), 0, _ms.Count - 1)];

            void Post(string json)
            {
                Debug.Log("[PerfTest] " + json);
                if (_url == null) return;
                try
                {
                    var req = UnityWebRequest.Post(_url, json, "application/json");
                    req.SendWebRequest().completed += _ => req.Dispose();
                }
                catch (System.Exception e) { Debug.LogWarning("[PerfTest] report not sent: " + e.Message); _url = null; }
            }

            static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
