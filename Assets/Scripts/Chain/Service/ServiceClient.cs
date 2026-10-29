using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Convergence.Chain.Service
{
    /// <summary>
    /// Plain HTTP to equivalence-service (web/service) - the saves every browser player keeps there,
    /// guest or wallet. UnityWebRequest, so the same code runs in the WebGL build (where it is a
    /// fetch) and in a native build or the Editor (testing against a local `npm run dev`).
    ///
    /// Deliberately NOT through the JS connector: a save needs no wallet, and a C# store is one a
    /// native build can use too.
    /// </summary>
    public static class ServiceClient
    {
        /// <summary>
        /// Where the service listens. Unset, a WebGL build uses its own page's origin - the game is
        /// served by the service itself (`/play`), so the API is same-origin and needs no CORS. Set it
        /// (StoreFactory.DevServiceUrl) to point the Editor at `http://localhost:8787`.
        /// </summary>
        public static string BaseUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(StoreFactory.DevServiceUrl)) return StoreFactory.DevServiceUrl.TrimEnd('/');
                var page = Application.absoluteURL;
                if (string.IsNullOrEmpty(page)) return "";
                var uri = new Uri(page);
                return uri.GetLeftPart(UriPartial.Authority);
            }
        }

        public const int TimeoutSeconds = 30;

        /// <summary>
        /// This build's place in the service's compatibility line (X-Client-Build). The service
        /// answers 426 to anything older than its MIN_CLIENT_BUILD (web/service/src/switches.ts) and
        /// the game reloads the page. BUMP IT, and the connector's CLIENT_BUILD with it, whenever a
        /// change means an older page can no longer talk to the service.
        /// </summary>
        public const int ClientBuild = 1;

        /// <summary>The service said this build is too old - raised once; the game offers a reload.</summary>
        public static event Action Outdated;
        static bool _outdated;

        /// <summary>Raises <see cref="Outdated"/> once - a 426, or /status naming a newer minimum.</summary>
        public static void ReportOutdated()
        {
            if (_outdated) return;
            _outdated = true;
            Outdated?.Invoke();
        }

        /// <summary>Requests waiting out an outage right now (a deploy, a restart) - the game shows
        /// "reconnecting" while this is above zero.</summary>
        public static int Reconnecting { get; private set; }

        /// <summary>
        /// How long a request keeps trying through an outage before it is reported as failed.
        /// Two minutes: a deploy stops the old container before the new one boots and migrates
        /// (the volume cannot be mounted twice), which is well under a minute.
        /// </summary>
        const float OutageSeconds = 120f;

        public struct Reply
        {
            /// <summary>HTTP status; 0 when nothing came back (offline, refused, timed out).</summary>
            public long Status;
            public string Body;
            public string Error;
            /// <summary>Answered by the service itself (it names X-Proof-Nonce on every response) -
            /// false for a proxy's error page while no container is up.</summary>
            public bool FromService;
            public bool Ok => Status >= 200 && Status < 300;
            public bool NotFound => Status == 404;
            public override string ToString() => Ok ? $"{Status}" : $"{Status} {Error} {Body}";
        }

        public static Task<Reply> Get(string path, string token) => Send("GET", path, null, token);
        public static Task<Reply> Put(string path, string json, string token) => Send("PUT", path, json, token);
        public static Task<Reply> Post(string path, string json, string token) => Send("POST", path, json, token);

        /// <summary>
        /// One request, carried through an OUTAGE: while the service is down for a deploy or a
        /// restart it is asked again with a growing wait, and the game shows "reconnecting". A
        /// refusal the service itself made (a paused feature, a bad request) is never retried.
        /// </summary>
        public static async Task<Reply> Send(string method, string path, string json, string token)
        {
            float started = Time.realtimeSinceStartup;
            float wait = 1f;
            bool counted = false;
            try
            {
                while (true)
                {
                    var r = await SendOnce(method, path, json, token);
                    if (r.Status == 426 && r.Body != null && r.Body.Contains("client_outdated")) ReportOutdated();
                    if (!Transient(method, path, r) || Time.realtimeSinceStartup - started > OutageSeconds) return r;
                    if (!counted) { counted = true; Reconnecting++; Debug.LogWarning($"[Service] {method} {path}: {r} - waiting out the outage"); }
                    // Yielded, not Task.Delay: WebGL has no threads for a timer.
                    float until = Time.realtimeSinceStartup + wait;
                    while (Time.realtimeSinceStartup < until) await Task.Yield();
                    wait = Mathf.Min(wait * 2f, 15f);
                }
            }
            finally
            {
                if (counted) Reconnecting--;
            }
        }

        /// <summary>
        /// An outage, not an answer: nothing came back, or a gateway error with no service behind
        /// it, or the service saying it is restarting. NOTHING CAME BACK is ambiguous for a write -
        /// it may have landed - so it is retried only where doing it twice changes nothing: reads,
        /// saves (PUT, whole snapshots), and the claims the service decides once. Spending charges
        /// or breaking a box is never repeated blind.
        /// </summary>
        static bool Transient(string method, string path, Reply r)
        {
            if (r.Status == 0)
                return method != "POST" || !(path.Contains("/recolour") || path.Contains("/break-box"));
            if (r.Status == 502 || r.Status == 503 || r.Status == 504)
                return !r.FromService || (r.Body != null && r.Body.Contains("\"code\":\"restarting\""));
            return false;
        }

        static Task<Reply> SendOnce(string method, string path, string json, string token)
        {
            // A WALLET session is bound to a key only the browser connector holds (web/service/src/
            // session.ts) - every request it authorises must be signed there, so it goes out through
            // the connector. A guest token is a plain bearer and stays here.
            if (!string.IsNullOrEmpty(token) && token == Web.ChainWallet.Session && Web.WebChainBridge.Available)
                return SendSigned(method, path, json);

            var tcs = new TaskCompletionSource<Reply>();
            UnityWebRequest req;
            try
            {
                req = new UnityWebRequest(BaseUrl + path, method)
                {
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = TimeoutSeconds,
                };
                if (json != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(token)) req.SetRequestHeader("Authorization", "Bearer " + token);
                req.SetRequestHeader("X-Client-Build", ClientBuild.ToString());
            }
            catch (Exception e)
            {
                tcs.SetResult(new Reply { Status = 0, Error = e.Message });
                return tcs.Task;
            }

            req.SendWebRequest().completed += _ =>
            {
                var reply = new Reply
                {
                    FromService = !string.IsNullOrEmpty(req.GetResponseHeader("X-Proof-Nonce")),
                    Status = req.responseCode,
                    Body = req.downloadHandler?.text ?? "",
                    Error = req.result == UnityWebRequest.Result.Success ? null : req.error,
                };
                req.Dispose();
                tcs.SetResult(reply);
            };
            return tcs.Task;
        }

        [Serializable]
        class SignedReply { public long status; public string body; public string error; public bool service; }

        static async Task<Reply> SendSigned(string method, string path, string json)
        {
            try
            {
                var raw = await Web.WebChainBridge.Call("request",
                    new Web.ChainArgs { method = method, path = path, body = json ?? "" });
                var r = JsonUtility.FromJson<SignedReply>(raw ?? "");
                if (r == null) return new Reply { Status = 0, Error = "no reply from the connector" };
                bool ok = r.status >= 200 && r.status < 300;
                return new Reply
                {
                    FromService = r.service,
                    Status = r.status,
                    Body = r.body ?? "",
                    Error = ok ? null : (string.IsNullOrEmpty(r.error) ? $"HTTP {r.status}" : r.error),
                };
            }
            catch (Exception e)
            {
                return new Reply { Status = 0, Error = e.Message };
            }
        }

        /// <summary>A JSON string literal - JsonUtility cannot serialise a bare string.</summary>
        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append($"\\u{(int)c:x4}");
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
