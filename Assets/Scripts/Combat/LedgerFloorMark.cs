using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A mark the ledger lays on a body, as a small sign on the FLOOR under its feet while it lasts
    /// (the user's pick, 2026-10-26, group 3 - "mark on the floor"): a gilt ring for GILDING, a
    /// red crack for LUCID's exposure, an eye for BASILISK's gaze, a stone ring for MOUNTAIN's
    /// slow. The latest one set shows. Drawn smaller than the armour ring, inside it, at body
    /// density and following the contact shadow - nothing over the creature.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class LedgerFloorMark : MonoBehaviour
    {
        public enum Kind { Gild, Exposed, Gaze, Slow }

        const float Ppu = TexelFx.Body;
        const int Rx = 7, Ry = 4, W = Rx * 2 + 5, H = Ry * 2 + 5;

        [SerializeField] SpriteRenderer _sr;
        [SerializeField] Kind _kind;
        [SerializeField] float _until;
        Kind _drawn = (Kind)(-1);
        Texture2D _tex;
        Color32[] _px;

        static Color32 C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly Color32 Gold = C("#f2c25a"), GoldHi = C("#fff8e0"), Crack = C("#e06a10"), CrackHi = C("#ffd0a0"),
                                Eye = C("#bdb9b2"), EyeHi = C("#e8e6e2"), Stone = C("#946c43"), StoneHi = C("#c9a46e");

        /// <summary>Lays (or renews) the mark on <paramref name="body"/> for <paramref name="seconds"/>.</summary>
        public static void Set(GameObject body, Kind kind, float seconds)
        {
            if (body == null) return;
            var m = body.GetComponent<LedgerFloorMark>();
            if (m == null) m = body.AddComponent<LedgerFloorMark>();
            m._kind = kind;
            m._until = Mathf.Max(m._kind == kind ? m._until : 0f, Time.time + seconds);
        }

        void LateUpdate()
        {
            var hp = GetComponent<Health>();
            bool on = Time.time < _until && (hp == null || !hp.IsDead);
            if (_sr == null)
            {
                if (!on) return;
                var go = new GameObject("ledger.mark");
                go.transform.SetParent(transform, false);
                _sr = go.AddComponent<SpriteRenderer>();
                _sr.sortingOrder = TexelFx.Ground;   // on the floor, under every body (added late, so DepthSorted never re-bases it)
            }
            _sr.enabled = on;
            if (!on) return;
            if (_kind != _drawn) Draw();

            Vector2 at = Feet();
            _sr.transform.SetPositionAndRotation(new Vector3(Mathf.Round(at.x * Ppu) / Ppu, Mathf.Round(at.y * Ppu) / Ppu, 0f), Quaternion.identity);
            var s = transform.lossyScale;
            _sr.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(s.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(s.y)), 1f);
            // A last half second's fade, so a mark running out is seen to.
            var c = Color.white; c.a = Mathf.Clamp01((_until - Time.time) / 0.5f);
            _sr.color = c;
        }

        /// <summary>Under the feet: a creature has no contact shadow, so the lowest DRAWN row of its
        /// biggest sprite (its sprite's bounds are padded).</summary>
        Vector2 Feet()
        {
            SpriteRenderer body = null;
            float best = 0f;
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (r == null || r == _sr || !r.enabled || r.sprite == null || r.sortingOrder >= SortingOrders.StatusOverlay) continue;
                if (r.name.StartsWith("status.") || r.name == HitFlash.ChildName) continue;
                float area = r.bounds.size.x * r.bounds.size.y;
                if (area > best) { best = area; body = r; }
            }
            if (body == null) return transform.position;
            var prof = BurnEmbers.ProfileOf(body.sprite);
            int low = int.MaxValue;
            foreach (int b in prof.Bottom) if (b >= 0 && b < low) low = b;
            if (low == int.MaxValue) return body.bounds.center;
            float y = body.bounds.min.y + low / body.sprite.pixelsPerUnit * Mathf.Abs(body.transform.lossyScale.y);
            return new Vector2(body.bounds.center.x, y + 0.04f);
        }

        void Draw()
        {
            _drawn = _kind;
            if (_tex == null)
            {
                _tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _px = new Color32[W * H];
                _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            }
            System.Array.Clear(_px, 0, _px.Length);
            int cx = W / 2, cy = H / 2;
            switch (_kind)
            {
                case Kind.Gild:
                case Kind.Slow:
                    for (int y = -Ry - 1; y <= Ry + 1; y++)
                        for (int x = -Rx - 1; x <= Rx + 1; x++)
                        {
                            float d = Mathf.Sqrt(x * x / (float)(Rx * Rx) + y * y / (float)(Ry * Ry));
                            if (d < 0.82f || d > 1.08f) continue;
                            bool hi = y > 0;
                            Put(cx + x, cy + y, _kind == Kind.Gild ? (hi ? GoldHi : Gold) : (hi ? StoneHi : Stone));
                        }
                    break;
                case Kind.Gaze:
                    for (int x = -Rx; x <= Rx; x++)
                    {
                        int h = Mathf.RoundToInt(Ry * Mathf.Sqrt(Mathf.Max(0f, 1f - x * x / (float)((Rx + 0.5f) * (Rx + 0.5f)))));
                        Put(cx + x, cy + h, EyeHi); Put(cx + x, cy - h, Eye);
                    }
                    Put(cx, cy, EyeHi); Put(cx - 1, cy, Eye); Put(cx + 1, cy, Eye);
                    break;
                case Kind.Exposed:
                    // A crack across the floor under it: a few jagged branches from the middle.
                    for (int b = 0; b < 5; b++)
                    {
                        float a = b / 5f * Mathf.PI * 2f + 0.4f, x = 0f, y = 0f;
                        for (int st = 0; st < 7; st++)
                        {
                            a += (FireArt.Hash(b, st, 11) - 0.5f) * 0.8f;
                            x += Mathf.Cos(a); y += Mathf.Sin(a) * 0.5f;
                            Put(cx + Mathf.RoundToInt(x), cy + Mathf.RoundToInt(y), st < 2 ? CrackHi : Crack);
                        }
                    }
                    break;
            }
            _tex.SetPixels32(_px);
            _tex.Apply();
        }

        void Put(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < W && y < H) _px[y * W + x] = c; }

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
