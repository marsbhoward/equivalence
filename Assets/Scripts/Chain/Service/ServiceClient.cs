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

        public struct Reply
        {
            /// <summary>HTTP status; 0 when nothing came back (offline, refused, timed out).</summary>
            public long Status;
            public string Body;
            public string Error;
            public bool Ok => Status >= 200 && Status < 300;
            public bool NotFound => Status == 404;
            public override string ToString() => Ok ? $"{Status}" : $"{Status} {Error} {Body}";
        }

        public static Task<Reply> Get(string path, string token) => Send("GET", path, null, token);
        public static Task<Reply> Put(string path, string json, string token) => Send("PUT", path, json, token);
        public static Task<Reply> Post(string path, string json, string token) => Send("POST", path, json, token);

        public static Task<Reply> Send(string method, string path, string json, string token)
        {
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
                    Status = req.responseCode,
                    Body = req.downloadHandler?.text ?? "",
                    Error = req.result == UnityWebRequest.Result.Success ? null : req.error,
                };
                req.Dispose();
                tcs.SetResult(reply);
            };
            return tcs.Task;
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
