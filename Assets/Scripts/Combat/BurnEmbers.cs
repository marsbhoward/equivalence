using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using T = Convergence.Core.Tuning.FireFx;

namespace Convergence.Combat
{
    /// <summary>
    /// A burning body (the user's pick, 2026-10-25 - "ember motes"): no tint over it, only
    /// single-texel embers rising off its whole silhouette, cooling from yellow to red as they
    /// climb, and small flames standing on the highest part of it - the head. It replaced an orange
    /// halo flickering under the body and soft dots flashed at random.
    ///
    /// Drawn in ONE small texture on the BODY SPRITE'S OWN TEXEL GRID: the canvas's corner is the
    /// sprite's corner less a whole number of texels, so an ember starting on a body texel is on
    /// it exactly, whatever the body's position. Where the body is comes from its own picture - a
    /// per-column top and bottom read once per sprite frame and cached - so the embers leave the
    /// silhouette and the flames sit on the head of any kind, any frame, any weathering.
    ///
    /// Embers already in the air when the burn ends finish their climb; none start after it.
    /// An overlay, as the halo was: a body standing in front never hides that this one burns.
    /// Kept apart from a Cindered elite's motes by density, speed and the cooling ramp.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class BurnEmbers : MonoBehaviour
    {
        const int PadX = 4, PadBelow = 2, Rise = 18;

        [SerializeField] StatusEffects _status;
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] SpriteRenderer _body;
        [SerializeField] float _lastBurning = -99f;
        [SerializeField] int _seed;
        Texture2D _tex;
        Color32[] _px;
        int _w, _h;

        /// <summary>A sprite's per-column top and bottom texel, and its highest texel.</summary>
        internal class Profile { public int W, H, Highest; public int[] Top, Bottom; }

        // Plain static cache: a reload empties it, and it refills on the next frame drawn.
        static Dictionary<Sprite, Profile> _profiles = new();

        public static BurnEmbers For(StatusEffects status)
        {
            var go = new GameObject("status.burn");
            go.transform.SetParent(status.transform, false);
            var e = go.AddComponent<BurnEmbers>();
            e._status = status;
            e._seed = Mathf.Abs(status.GetEntityId().GetHashCode()) % 997;
            e._sr = go.AddComponent<SpriteRenderer>();
            e._sr.sortingOrder = SortingOrders.StatusOverlay;
            e._sr.enabled = false;
            return e;
        }

        void LateUpdate()
        {
            if (_status == null) { Destroy(gameObject); return; }
            float now = Time.time;
            if (_status.Burning) _lastBurning = now;

            var health = _status.GetComponent<Health>();
            bool dead = health != null && health.IsDead;
            if (dead || now - _lastBurning > T.EmberLife * 1.3f + 0.05f || !FindBody())
            {
                _sr.enabled = false;
                return;
            }

            var sprite = _body.sprite;
            var p = ProfileOf(sprite);
            float ppu = sprite.pixelsPerUnit;
            bool flip = _body.flipX ^ (_body.transform.lossyScale.x < 0f);
            EnsureCanvas(p.W + PadX * 2, p.H + PadBelow + Rise, ppu);

            System.Array.Clear(_px, 0, _px.Length);
            bool burning = _status.Burning;

            // Embers: each on its own cycle; a cycle that STARTED after the burn ended is not drawn.
            for (int k = 0; k < T.BurnEmbers; k++)
            {
                float life = T.EmberLife * (0.75f + FireArt.Hash(k, _seed, 1) * 0.5f);
                float phase = FireArt.Hash(k, _seed, 2) * life;
                float age = (now + phase) % life;
                int cycle = Mathf.FloorToInt((now + phase) / life);
                if (now - age > _lastBurning) continue;

                int col = Mathf.FloorToInt(FireArt.Hash(k, cycle, _seed) * p.W);
                if (p.Top[col] < 0) continue;   // an empty column this frame: skip the cycle
                int y0 = p.Bottom[col] + Mathf.FloorToInt(FireArt.Hash(k, cycle, _seed + 7) * (p.Top[col] - p.Bottom[col] + 1));
                int sc = flip ? p.W - 1 - col : col;
                int x = sc + PadX + Mathf.RoundToInt(Mathf.Sin(age * 3.3f + k) * 1.4f);
                int y = y0 + PadBelow + Mathf.RoundToInt(age * T.EmberRise * (0.7f + FireArt.Hash(k, 9) * 0.6f));
                float u = age / life;
                // The last of its life it gutters - on, off - before it goes.
                if (u > 0.85f && FireArt.Hash(k, cycle, Mathf.FloorToInt(now * 12f)) > 0.5f) continue;
                Put(x, y, FireArt.At(0.12f + u * 0.8f));
            }

            // The head's flames, only while it burns: on every column that tops out near the
            // highest texel of the body.
            if (burning)
                for (int col = 0; col < p.W; col++)
                {
                    int top = p.Top[col];
                    if (top < 0 || top < p.Highest - (T.HeadRows - 1)) continue;
                    int sc = flip ? p.W - 1 - col : col;
                    for (int i = 1; i <= T.HeadFlameTexels; i++)
                        if (FireArt.Tongue(sc, i, T.HeadFlameTexels, now, _seed, out var c))
                            Put(sc + PadX, top + PadBelow + i, c);
                }

            _tex.SetPixels32(_px);
            _tex.Apply();

            // On the body sprite's texel grid: its corner, less the pads, in its own texels.
            var min = _body.bounds.min;
            transform.position = new Vector3(min.x - PadX / ppu, min.y - PadBelow / ppu, 0f);
            _sr.enabled = true;
        }

        /// <summary>The body as drawn: the biggest renderer under the root that is not an overlay
        /// or a flash. Re-picked when the one held goes away.</summary>
        bool FindBody()
        {
            if (_body != null && _body.enabled && _body.sprite != null && _body.gameObject.activeInHierarchy)
                return true;
            _body = null;
            float best = 0f;
            foreach (var r in _status.GetComponentsInChildren<SpriteRenderer>())
            {
                if (r == _sr || r.sprite == null || !r.enabled || r.sortingOrder >= SortingOrders.StatusOverlay) continue;
                if (r.name == HitFlash.ChildName || r.name.StartsWith("status.")) continue;
                float area = r.bounds.size.x * r.bounds.size.y;
                if (area > best) { best = area; _body = r; }
            }
            return _body != null;
        }

        internal static Profile ProfileOf(Sprite sprite)
        {
            if (_profiles.TryGetValue(sprite, out var p)) return p;
            if (_profiles.Count > 4096) _profiles.Clear();

            var rect = sprite.textureRect;
            int w = Mathf.Max(1, Mathf.RoundToInt(rect.width)), h = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            p = new Profile { W = w, H = h, Top = new int[w], Bottom = new int[w], Highest = h - 1 };
            Color32[] px = null;
            var tex = sprite.texture;
            if (tex != null && tex.isReadable)
            {
                var all = tex.GetPixels32();
                px = new Color32[w * h];
                int x0 = Mathf.RoundToInt(rect.x), y0 = Mathf.RoundToInt(rect.y);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        px[y * w + x] = all[(y0 + y) * tex.width + x0 + x];
            }

            int highest = -1;
            for (int x = 0; x < w; x++)
            {
                int top = -1, bottom = -1;
                for (int y = 0; y < h; y++)
                {
                    bool solid = px == null || px[y * w + x].a > 128;
                    if (!solid) continue;
                    if (bottom < 0) bottom = y;
                    top = y;
                }
                p.Top[x] = top; p.Bottom[x] = bottom;
                if (top > highest) highest = top;
            }
            p.Highest = highest;
            _profiles[sprite] = p;
            return p;
        }

        void EnsureCanvas(int w, int h, float ppu)
        {
            if (_tex != null && _w >= w && _h >= h && Mathf.Approximately(_sr.sprite.pixelsPerUnit, ppu)) return;
            if (_sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
            _w = Mathf.Max(w, _w); _h = Mathf.Max(h, _h);
            _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            _px = new Color32[_w * _h];
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
        }

        void Put(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            _px[y * _w + x] = c;
        }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
