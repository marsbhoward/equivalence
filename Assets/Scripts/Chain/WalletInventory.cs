using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Convergence.Chain
{
    /// <summary>One art NFT the connected wallet holds. A COMPONENT a PFP can be assembled from.</summary>
    [Serializable]
    public class OwnedComponent
    {
        /// <summary>
        /// Stable identity - policy+name on Cardano. The same key a RoomLayout placement stores
        /// and the same key a PfpRecipe names, so all three talk about the same thing.
        /// </summary>
        public string Key;

        public string Title;
        public string Collection;

        /// <summary>Which part of a character this can be, if any - "Head", "Weapon", ... Empty means display-only art.</summary>
        public string Slot;

        /// <summary>Where the picture lives. Fetched lazily, never at load - see WalletShowcaseSource.</summary>
        public string ImageUrl;

        /// <summary>The piece's LIVE page - only for our own collections (the service's HTML_POLICIES,
        /// routes/nft.ts). Shown in the web build's close-up alone (Hub/NftFrame), never in the room.</summary>
        public string HtmlUrl;

        /// <summary>The card such a page draws, as colours and orbits, so the game can draw it spinning
        /// on the wall (Hub/NftCard). HasCard says it was sent - JsonUtility fills an absent object.</summary>
        public bool HasCard;
        public NftCardSpec Card;
    }

    /// <summary>A DONADA card as the service reads it from the piece's page (routes/nft.ts parseCard).</summary>
    [Serializable]
    public class NftCardSpec
    {
        /// <summary>#rrggbb, and how opaque the glass is.</summary>
        public string Glass;
        public float GlassAlpha;
        public List<NftOrb> Orbs = new();
        /// <summary>Each face has its own orbit.</summary>
        public NftOrbit FrontOrbit, BackOrbit;
        /// <summary>The faces' label lines - drawn only where they can be read.</summary>
        public List<string> Front = new(), Back = new();
    }

    [Serializable]
    public class NftOrb { public string Core, Color, Bloom, Trail; }

    [Serializable]
    public class NftOrbit { public float Tilt, Roll, Phase; }

    /// <summary>One policy - one collection - the wallet holds pieces of.</summary>
    [Serializable]
    public class WalletPolicy
    {
        /// <summary>56 hex: the head of every unit minted under it.</summary>
        public string Policy;
        /// <summary>The metadata's collection name, or empty until the service has read one.</summary>
        public string Name;
        /// <summary>Tokens held under it - before the picture filter, which only a details page applies.</summary>
        public int Count;
    }

    [Serializable]
    class Holdings
    {
        public List<string> held = new();
        public List<WalletPolicy> policies = new();
        public bool truncated;
    }

    [Serializable]
    class ItemPage
    {
        public List<OwnedComponent> items = new();
        public int page, pages;
    }

    /// <summary>
    /// What the connected wallet actually holds - POLICY FIRST (web/service/src/routes/components.ts).
    ///
    /// A refresh reads the HOLDINGS: every NFT's key and the policies they group into, with no
    /// per-token details - each of those is a Blockfrost call the whole service shares a rate limit
    /// for. Details arrive a policy page at a time as the art picker opens a policy
    /// (<see cref="LoadNextPageAsync"/>), or by key for the frames already placed in the room
    /// (<see cref="LoadUnitsAsync"/>).
    ///
    /// THE GAME GATES ON THIS; THE CHAIN ENFORCES IT. Ownership (<see cref="Owns"/>) reads the
    /// holdings alone, so it needs no details. The connector checks it again at mint - a cached
    /// list can go out of date the moment a token is sold in another tab.
    ///
    /// Empty is a NORMAL state - no wallet connected, or a wallet holding no art. The game stays
    /// fully playable either way.
    ///
    /// Static state is non-readonly and null-guarded (domain reload - ENGINEERING.md).
    /// </summary>
    public static class WalletInventory
    {
        static List<string> _held = new();
        static HashSet<string> _heldSet = new();
        static List<WalletPolicy> _policies = new();
        static Dictionary<string, OwnedComponent> _details = new();

        // Per policy: the keys loaded so far in page order, the next page to ask for (-1 when none).
        static Dictionary<string, List<string>> _policyKeys = new();
        static Dictionary<string, int> _policyNext = new();
        static HashSet<string> _loading = new();
        /// <summary>When a failed policy page may be asked again - a picker re-asks as it redraws.</summary>
        static Dictionary<string, float> _retryAt = new();
        /// <summary>Pieces already asked for by key: one held without a picture is never answered,
        /// and must not be asked for again on every repaint.</summary>
        static HashSet<string> _asked = new();

        /// <summary>Set by the Editor's testing read: details come over HTTP from here too, not the bridge.</summary>
        static string _serviceUrl, _address;

        /// <summary>Raised after a refresh and after details arrive, so a picker or the couch can redraw.</summary>
        public static event Action Changed;

        /// <summary>NFTs held - every one, whether its details are loaded or not.</summary>
        public static int Count => _held?.Count ?? 0;

        public static IReadOnlyList<WalletPolicy> Policies => _policies ??= new List<WalletPolicy>();

        /// <summary>The wallet was larger than the service reads (routes/components.ts MAX_PAGES).</summary>
        public static bool Truncated { get; private set; }

        public static bool Owns(string key)
        {
            if (string.IsNullOrEmpty(key) || _held == null) return false;
            _heldSet ??= new HashSet<string>();
            if (_heldSet.Count != _held.Count) _heldSet = new HashSet<string>(_held);
            return _heldSet.Contains(key);
        }

        /// <summary>A held piece's details - false until its page or its key has been loaded.</summary>
        public static bool TryGet(string key, out OwnedComponent found)
        {
            found = null;
            _details ??= new Dictionary<string, OwnedComponent>();
            return !string.IsNullOrEmpty(key) && _details.TryGetValue(key, out found) && Owns(key);
        }

        /// <summary>The pieces of a policy loaded so far, in the service's order. Pictures only.</summary>
        public static List<OwnedComponent> Loaded(string policy)
        {
            var list = new List<OwnedComponent>();
            _policyKeys ??= new Dictionary<string, List<string>>();
            if (policy == null || !_policyKeys.TryGetValue(policy, out var keys)) return list;
            foreach (var k in keys) if (TryGet(k, out var c)) list.Add(c);
            return list;
        }

        /// <summary>Whether a policy has more pages to load.</summary>
        public static bool HasMore(string policy)
        {
            _policyNext ??= new Dictionary<string, int>();
            return policy != null && (!_policyNext.TryGetValue(policy, out var next) || next >= 0);
        }

        public static bool IsLoading(string policy) => policy != null && (_loading?.Contains(policy) ?? false);

        /// <summary>Load a policy's next page of details. One at a time per policy; a no-op past the last.</summary>
        public static async Task LoadNextPageAsync(string policy)
        {
            if (string.IsNullOrEmpty(policy) || !HasMore(policy)) return;
            _loading ??= new HashSet<string>();
            _retryAt ??= new Dictionary<string, float>();
            if (_retryAt.TryGetValue(policy, out var at) && Time.realtimeSinceStartup < at) return;
            if (!_loading.Add(policy)) return;
            try
            {
                _policyNext.TryGetValue(policy, out var page);
                var json = await FetchItems(new Web.ChainArgs { policy = policy, page = page.ToString() });
                if (json == null) { _retryAt[policy] = Time.realtimeSinceStartup + 10f; return; }
                var parsed = JsonUtility.FromJson<ItemPage>(json) ?? new ItemPage();
                Absolutise(parsed.items, ServiceRoot);
                _policyKeys ??= new Dictionary<string, List<string>>();
                if (!_policyKeys.TryGetValue(policy, out var keys)) _policyKeys[policy] = keys = new List<string>();
                foreach (var c in parsed.items)
                {
                    _details[c.Key] = c;
                    if (!keys.Contains(c.Key)) keys.Add(c.Key);
                }
                _policyNext[policy] = parsed.page + 1 < parsed.pages ? parsed.page + 1 : -1;
            }
            catch (Exception e)
            {
                _retryAt[policy] = Time.realtimeSinceStartup + 10f;
                Debug.LogWarning($"[Chain] loading policy {policy} failed: {e.Message}");
            }
            finally
            {
                _loading.Remove(policy);
            }
            Changed?.Invoke();
        }

        /// <summary>Details for named pieces - the frames already placed, restored with the room.
        /// Only held pieces not yet loaded are asked for, 24 a request.</summary>
        public static async Task LoadUnitsAsync(IEnumerable<string> keys)
        {
            var want = new List<string>();
            _details ??= new Dictionary<string, OwnedComponent>();
            _asked ??= new HashSet<string>();
            foreach (var k in keys)
                if (Owns(k) && !_details.ContainsKey(k) && _asked.Add(k)) want.Add(k);
            if (want.Count == 0) return;
            int had = _details.Count;

            for (int i = 0; i < want.Count; i += 24)
            {
                var chunk = want.GetRange(i, Mathf.Min(24, want.Count - i));
                try
                {
                    var json = await FetchItems(new Web.ChainArgs { units = string.Join(",", chunk) });
                    if (json == null) continue;
                    var parsed = JsonUtility.FromJson<ItemPage>(json) ?? new ItemPage();
                    Absolutise(parsed.items, ServiceRoot);
                    foreach (var c in parsed.items) _details[c.Key] = c;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Chain] loading placed pieces failed: {e.Message}");
                }
            }
            // Only on news: Changed repaints the frames, which asks again - see _asked.
            if (_details.Count != had) Changed?.Invoke();
        }

        /// <summary>
        /// Re-read the holdings from the connector. Called on connect and after a mint; cheap enough
        /// to call again whenever an assembly screen opens, since a token can leave the wallet elsewhere.
        /// </summary>
        public static async Task RefreshAsync()
        {
            if (!Web.WebChainBridge.Available || !Web.ChainWallet.Connected)
            {
                Clear();
                return;
            }

            try
            {
                var json = await Web.WebChainBridge.Call("listComponents",
                    new Web.ChainArgs { accountId = Web.ChainWallet.Address });
                _serviceUrl = null;
                _address = Web.ChainWallet.Address;
                Take(string.IsNullOrEmpty(json) ? new Holdings() : JsonUtility.FromJson<Holdings>(json));
                Debug.Log($"[Chain] wallet holds {Count} NFT(s) in {Policies.Count} polic{(Policies.Count == 1 ? "y" : "ies")}");
            }
            catch (Exception e)
            {
                // Left as it was rather than emptied: a failed refresh must not look like a wallet
                // that suddenly owns nothing, which would grey out every part the player has.
                Debug.LogWarning($"[Chain] inventory refresh failed, keeping {Count}: {e.Message}");
            }

            Changed?.Invoke();
        }

        /// <summary>
        /// Editor/CLI testing path: read the service's own <c>/components</c> endpoint directly
        /// over HTTP, instead of through <see cref="Web.WebChainBridge"/>. The bridge is real
        /// browser JS and does not exist outside a WebGL build, so RefreshAsync above always reads
        /// empty in the Editor - this is the one way to see a real wallet's holdings without one.
        ///
        /// Kept separate from RefreshAsync rather than folded into it as a fallback: the split
        /// this project draws everywhere else is that <c>Chain/Web/</c> is the ONLY part that
        /// knows a browser exists, and every native target reads through the local JSON stores
        /// instead. A silent HTTP fallback here would quietly cross that line for every platform;
        /// gating this on <see cref="Core.Tuning.Testing.DemoWalletAddress"/> and calling it only from
        /// GameBootstrap's own testing branch keeps it something that has to be turned on, not
        /// something a native build could stumble into. Details then follow it over HTTP too.
        /// </summary>
        public static async Task RefreshFromServiceAsync(string serviceUrl, string address)
        {
            if (string.IsNullOrEmpty(serviceUrl) || string.IsNullOrEmpty(address)) return;

            var json = await Get($"{serviceUrl.TrimEnd('/')}/components?address={address}");
            if (json == null) return;   // left as it was - see RefreshAsync's note

            _serviceUrl = serviceUrl;
            _address = address;
            Take(JsonUtility.FromJson<Holdings>(json));
            Debug.Log($"[Chain] wallet holds {Count} NFT(s) in {Policies.Count} policies, read from {serviceUrl}");
            Changed?.Invoke();
        }

        public static void Clear()
        {
            if (Count == 0 && Policies.Count == 0) return;
            Take(new Holdings());
            Changed?.Invoke();
        }

        /// <summary>New holdings in. Details already loaded for pieces still held are kept; a
        /// policy's paging starts over, since its pieces may have changed.</summary>
        static void Take(Holdings h)
        {
            h ??= new Holdings();
            _held = h.held ?? new List<string>();
            _heldSet = new HashSet<string>(_held);
            _policies = h.policies ?? new List<WalletPolicy>();
            Truncated = h.truncated;
            _policyKeys = new Dictionary<string, List<string>>();
            _policyNext = new Dictionary<string, int>();
            _loading = new HashSet<string>();
            _retryAt = new Dictionary<string, float>();
            _asked = new HashSet<string>();
            _details ??= new Dictionary<string, OwnedComponent>();
            var gone = new List<string>();
            foreach (var k in _details.Keys) if (!_heldSet.Contains(k)) gone.Add(k);
            foreach (var k in gone) _details.Remove(k);
        }

        static string ServiceRoot => _serviceUrl ?? Service.ServiceClient.BaseUrl;

        /// <summary>A details request: over HTTP on the Editor's testing path, the connector otherwise.</summary>
        static async Task<string> FetchItems(Web.ChainArgs args)
        {
            if (!string.IsNullOrEmpty(_serviceUrl))
            {
                var q = !string.IsNullOrEmpty(args.units)
                    ? $"units={UnityWebRequest.EscapeURL(args.units)}"
                    : $"policy={args.policy}&page={args.page}";
                return await Get($"{_serviceUrl.TrimEnd('/')}/components/items?address={_address}&{q}");
            }
            if (!Web.WebChainBridge.Available || string.IsNullOrEmpty(_address)) return null;
            args.accountId = _address;
            return await Web.WebChainBridge.Call("listComponentItems", args);
        }

        static async Task<string> Get(string url)
        {
            using var req = UnityWebRequest.Get(url);
            // The hub's construction awaits the holdings. Against localhost a stopped service refuses
            // at once; a remote one (Railway) that stops answering would otherwise stall it forever.
            req.timeout = 20;
            var op = req.SendWebRequest();
            while (!op.isDone) await Task.Yield();
            if (req.result == UnityWebRequest.Result.Success) return req.downloadHandler.text;
            Debug.LogWarning($"[Chain] service inventory read failed: {req.error} ({url})");
            return null;
        }

        /// <summary>The service names pictures by its own path (/img/..., web/service/src/routes/img.ts)
        /// - the page may only fetch from its own origin. A texture request needs the whole URL.</summary>
        static void Absolutise(List<OwnedComponent> items, string serviceUrl)
        {
            if (items == null || string.IsNullOrEmpty(serviceUrl)) return;
            var root = serviceUrl.TrimEnd('/');
            foreach (var c in items)
            {
                if (!string.IsNullOrEmpty(c.ImageUrl) && c.ImageUrl.StartsWith("/")) c.ImageUrl = root + c.ImageUrl;
                if (!string.IsNullOrEmpty(c.HtmlUrl) && c.HtmlUrl.StartsWith("/")) c.HtmlUrl = root + c.HtmlUrl;
            }
        }
    }
}
