using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Convergence.Chain;

namespace Convergence.Hub
{
    /// <summary>
    /// The showcase, backed by what the wallet actually holds - the real thing
    /// <see cref="DevShowcaseSource"/> stands in for.
    ///
    /// This is the half of the ownership split the player SEES: components they own appear in the
    /// picker, they place them in the room, and where they placed them is written by the service
    /// onto the account record. The art is theirs; the arrangement is game-authored.
    ///
    /// ART IS FETCHED LAZILY AND CAPPED, which matters more here than anywhere else in the
    /// project. Every other texture in this game is procedural and sized by us; these are
    /// arbitrary images from arbitrary minters, at whatever resolution they were made, and a
    /// wallet holding two hundred of them would otherwise decompress all two hundred into memory
    /// at once - on WebGL, in a tab, which is where this build is going.
    /// </summary>
    public class WalletShowcaseSource : MonoBehaviour, IShowcaseSource
    {
        /// <summary>
        /// How many pictures may be resident. Placements beyond this still exist and still hold
        /// their spot in the room; they simply draw the placeholder until something else is
        /// evicted. A missing picture is a worse room, a killed tab is no game.
        /// </summary>
        public int MaxCachedTextures = 24;

        /// <summary>Longest edge an incoming image is allowed to keep. A 4K trophy on a plinth is 4K wasted.</summary>
        public int MaxEdge = 512;

        readonly Dictionary<string, Sprite> _art = new();
        readonly HashSet<string> _loading = new();
        readonly List<string> _order = new();          // eviction order, oldest first
        readonly Dictionary<string, float> _failedAt = new();

        /// <summary>
        /// How long a failed fetch is left alone before it may be retried. Without this, a single
        /// bad response turned into a request STORM: Art() is called on every repaint - every
        /// frame the picker is open, every trophy rebuild - so a gateway rate-limiting one request
        /// got hit again on the very next repaint, and the resulting flood of retries was what
        /// actually kept it rate-limited. Verified live: an IPFS gateway returning 429 was re-hit
        /// dozens of times a second before this existed.
        /// </summary>
        public float RetryCooldownSeconds = 20f;

        Sprite _placeholder;

        /// <summary>Raised when a picture arrives, so a frame already on screen can swap it in.</summary>
        public event Action ArtArrived;

        public int Count => WalletInventory.Count;

        public bool TryGet(int index, out ShowcaseItem item)
        {
            item = default;
            var owned = WalletInventory.Components;
            if (index < 0 || index >= owned.Count) return false;
            item = Compose(owned[index]);
            return true;
        }

        public bool TryGetByKey(string key, out ShowcaseItem item)
        {
            item = default;
            if (!WalletInventory.TryGet(key, out var c)) return false;
            item = Compose(c);
            return true;
        }

        ShowcaseItem Compose(OwnedComponent c)
        {
            return new ShowcaseItem
            {
                Key = c.Key,
                Title = c.Title,
                Collection = c.Collection,
                Art = Art(c),
            };
        }

        /// <summary>
        /// The cached picture, or the placeholder while it is on its way. Never blocks and never
        /// returns null - a frame drawing nothing at all reads as a broken room rather than a
        /// loading one.
        /// </summary>
        Sprite Art(OwnedComponent c)
        {
            if (_art.TryGetValue(c.Key, out var s) && s != null)
            {
                Touch(c.Key);
                return s;
            }
            bool coolingDown = _failedAt.TryGetValue(c.Key, out var failedAt) &&
                              Time.unscaledTime - failedAt < RetryCooldownSeconds;

            if (!string.IsNullOrEmpty(c.ImageUrl) && !coolingDown && _loading.Add(c.Key))
                StartCoroutine(Fetch(c.Key, c.ImageUrl));
            return _placeholder ??= Core.Spr.Square;
        }

        IEnumerator Fetch(string key, string url)
        {
            using var req = UnityWebRequestTexture.GetTexture(url);
            yield return req.SendWebRequest();
            _loading.Remove(key);

            if (req.result != UnityWebRequest.Result.Success)
            {
                // Recorded, not just logged - see RetryCooldownSeconds. Loud enough to find, quiet
                // enough not to spam: without the cooldown this fired on every repaint for as long
                // as the picker or a placed trophy kept asking for it.
                _failedAt[key] = Time.unscaledTime;
                Debug.LogWarning($"[Showcase] {key}: {req.error}");
                yield break;
            }

            _failedAt.Remove(key);

            var tex = DownloadHandlerTexture.GetContent(req);
            if (tex == null) yield break;

            tex.filterMode = FilterMode.Bilinear;   // NOT Point: this is somebody's artwork, not our pixel grid
            tex.wrapMode = TextureWrapMode.Clamp;

            if (Mathf.Max(tex.width, tex.height) > MaxEdge) Downscale(ref tex);

            // Frees the CPU-side copy. Every procedural texture in this project keeps one; these
            // are the ones we can least afford to hold twice.
            tex.Apply(false, true);

            _art[key] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            Touch(key);
            Evict();

            // Every placed Frame is registered with Showcase (see its own header) and repaints
            // itself through this, whichever mode it happens to be standing in.
            Showcase.Refresh();
            ArtArrived?.Invoke();
        }

        void Downscale(ref Texture2D tex)
        {
            float k = MaxEdge / (float)Mathf.Max(tex.width, tex.height);
            int w = Mathf.Max(1, Mathf.RoundToInt(tex.width * k));
            int h = Mathf.Max(1, Mathf.RoundToInt(tex.height * k));

            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var small = new Texture2D(w, h, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, w, h), 0, 0);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(tex);
            tex = small;
        }

        void Touch(string key)
        {
            _order.Remove(key);
            _order.Add(key);
        }

        void Evict()
        {
            while (_order.Count > MaxCachedTextures)
            {
                var oldest = _order[0];
                _order.RemoveAt(0);
                if (!_art.TryGetValue(oldest, out var s) || s == null) continue;
                var tex = s.texture;
                _art.Remove(oldest);
                Destroy(s);
                if (tex != null) Destroy(tex);
            }
        }

        void OnDestroy()
        {
            foreach (var s in _art.Values)
            {
                if (s == null) continue;
                var tex = s.texture;
                Destroy(s);
                if (tex != null) Destroy(tex);
            }
            _art.Clear();
            _order.Clear();
        }
    }
}
