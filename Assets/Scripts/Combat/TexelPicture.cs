using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A picture that LASTS, texel-drawn and redrawn every frame (the room's pictures, group 4):
    /// a tornado's funnel, a spire's ring, a Rift's rim, a pickup's beam. ReleaseCanvas's way of
    /// drawing, but kept by its owner rather than ending itself, and REBUILT after a domain reload
    /// (only the sizes are serialized; the texture is made again on first use).
    ///
    /// It sits on a child of its owner, its centre texel at <see cref="Anchor"/> (local to the
    /// parent) snapped to the world's texel grid every frame, so a moving owner stays crisp.
    /// <see cref="Clip"/> keeps it to standing room, asked per texel and cached while it stays put.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public abstract class TexelPicture : MonoBehaviour
    {
        [SerializeField] protected SpriteRenderer Sr;
        [SerializeField] protected int W, H, Cx, Cy;
        [SerializeField] protected float Ppu = TexelFx.Body;
        [SerializeField] protected bool Clip;
        /// <summary>Where the centre texel stands, local to the parent.</summary>
        [SerializeField] public Vector2 Anchor;
        /// <summary>Seconds since it was made (scaled time).</summary>
        [SerializeField] protected float Age;

        protected Texture2D Tex;
        protected Color32[] Px;
        sbyte[] _floor;
        Vector3 _floorAt;

        /// <summary>Add one under <paramref name="parent"/>: <paramref name="halfW"/> texels either
        /// side of the centre, <paramref name="below"/> under it and <paramref name="above"/> over it.</summary>
        public static T Attach<T>(Transform parent, string name, Vector2 anchor, int halfW, int below, int above,
                                  int order, float ppu = TexelFx.Body, bool clip = false) where T : TexelPicture
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<T>();
            p.Sr = go.AddComponent<SpriteRenderer>();
            p.Sr.sortingOrder = order;
            p.W = halfW * 2 + 1; p.H = below + above + 1; p.Cx = halfW; p.Cy = below;
            p.Ppu = ppu; p.Clip = clip; p.Anchor = anchor;
            p.Sr.enabled = false;
            return p;
        }

        /// <summary>Draw this frame; false hides the picture (it stays, to be drawn again).</summary>
        protected abstract bool Draw();

        void Build()
        {
            Tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            Px = new Color32[W * H];
            _floor = null;
            if (Sr.sprite != null) Destroy(Sr.sprite);
            Sr.sprite = Sprite.Create(Tex, new Rect(0, 0, W, H), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
        }

        protected virtual void LateUpdate()
        {
            if (Sr == null || W <= 0 || H <= 0) return;
            if (Tex == null || Px == null) Build();
            Age += Time.deltaTime;

            // The centre on a whole texel of the world's grid, whatever the parent's position.
            var parent = transform.parent;
            Vector2 at = parent != null ? (Vector2)parent.position + Anchor : Anchor;
            var snapped = new Vector2(Mathf.Round(at.x * Ppu), Mathf.Round(at.y * Ppu));
            transform.SetPositionAndRotation(new Vector3((snapped.x - Cx) / Ppu, (snapped.y - Cy) / Ppu, 0f), Quaternion.identity);
            var s = parent != null ? parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, Mathf.Abs(s.x)), 1f / Mathf.Max(1e-4f, Mathf.Abs(s.y)), 1f);

            System.Array.Clear(Px, 0, Px.Length);
            if (!Draw()) { Sr.enabled = false; return; }
            Tex.SetPixels32(Px);
            Tex.Apply();
            Sr.enabled = true;
        }

        protected virtual void OnDestroy()
        {
            if (Sr != null && Sr.sprite != null) Destroy(Sr.sprite);
            if (Tex != null) Destroy(Tex);
        }

        // ---- drawing, in texels from the centre ----

        protected bool Inside(int dx, int dy) { int x = Cx + dx, y = Cy + dy; return x >= 0 && y >= 0 && x < W && y < H; }

        protected void Put(int dx, int dy, Color32 c)
        {
            int x = Cx + dx, y = Cy + dy;
            if (x < 0 || y < 0 || x >= W || y >= H || !Standing(x, y)) return;
            Px[y * W + x] = c;
        }

        /// <summary><paramref name="c"/> laid over what is already there at <paramref name="alpha"/>.</summary>
        protected void Over(int dx, int dy, Color32 c, float alpha = 1f)
        {
            int x = Cx + dx, y = Cy + dy;
            if (x < 0 || y < 0 || x >= W || y >= H || alpha <= 0f || !Standing(x, y)) return;
            int i = y * W + x;
            var o = Px[i];
            float ca = c.a / 255f * Mathf.Clamp01(alpha);
            if (o.a == 0) { c.a = (byte)(ca * 255f); Px[i] = c; return; }
            float oa = o.a / 255f, na = ca + oa * (1f - ca), k = ca / na;
            Px[i] = new Color32((byte)(c.r * k + o.r * (1f - k)), (byte)(c.g * k + o.g * (1f - k)),
                                (byte)(c.b * k + o.b * (1f - k)), (byte)(na * 255f));
        }

        /// <summary>Every texel whose distance from (ox, oy) lies in [r0, r1], with its distance and
        /// its angle (0 east, counter-clockwise).</summary>
        protected void Ring(float ox, float oy, float r0, float r1, System.Action<int, int, float, float> fn)
        {
            if (r1 <= 0f) return;
            int e = Mathf.CeilToInt(r1) + 1, x0 = Mathf.RoundToInt(ox), y0 = Mathf.RoundToInt(oy);
            for (int dy = -e; dy <= e; dy++)
                for (int dx = -e; dx <= e; dx++)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= r0 && d <= r1) fn(x0 + dx, y0 + dy, d, Mathf.Atan2(dy, dx));
                }
        }

        /// <summary>A one-texel line from (x0, y0) to (x1, y1), handing each texel and how far along it is.</summary>
        protected static void Line(float x0, float y0, float x1, float y1, System.Action<int, int, float> fn)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0))));
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                fn(Mathf.RoundToInt(x0 + (x1 - x0) * u), Mathf.RoundToInt(y0 + (y1 - y0) * u), u);
            }
        }

        /// <summary>
        /// A damage line (the user's pick, group 4 - "beam"; the spire's lines and Projection's):
        /// while WARNING a faint dotted line in the edge colour, blinking; while BURNING a red edge
        /// either side of a white-hot core, flickering, with bright pulses running along it.
        /// </summary>
        protected void Beam(float x0, float y0, float x1, float y1, bool burning, float strength,
                            Color32 edge, Color32 core, Color32 hot, float t)
        {
            float len = Mathf.Max(1f, Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)));
            bool steep = Mathf.Abs(y1 - y0) > Mathf.Abs(x1 - x0);
            bool blink = Mathf.Repeat(t * 6f, 1f) < 0.6f;
            Line(x0, y0, x1, y1, (x, y, u) =>
            {
                float s = u * len;
                if (!burning)
                {
                    if (blink && (Mathf.FloorToInt(s) & 3) < 2) Over(x, y, edge, strength);
                    return;
                }
                bool pulse = Mathf.Repeat(s - t * 70f, 26f) < 3f;
                var e = pulse ? Lerp(edge, hot, 0.5f) : edge;
                if (steep) { Over(x - 1, y, e, strength); Over(x + 1, y, e, strength); }
                else { Over(x, y - 1, e, strength); Over(x, y + 1, e, strength); }
                Over(x, y, pulse || FireArt.Noise(s * 0.3f, t * 8f) > 0.6f ? hot : core, strength);
            });
        }

        bool Standing(int x, int y)
        {
            if (!Clip) return true;
            if (_floor == null || _floor.Length != W * H || (transform.position - _floorAt).sqrMagnitude > 1e-6f)
            {
                if (_floor == null || _floor.Length != W * H) _floor = new sbyte[W * H];
                else System.Array.Clear(_floor, 0, _floor.Length);
                _floorAt = transform.position;
            }
            int i = y * W + x;
            if (_floor[i] == 0)
            {
                var p = (Vector2)transform.position + new Vector2(x + 0.5f, y + 0.5f) / Ppu;
                var inner = Arena.HalfExtents - Vector2.one * Tuning.Scenery.WallInset;
                bool inside = Mathf.Abs(p.x) <= inner.x && Mathf.Abs(p.y) <= inner.y;
                _floor[i] = (sbyte)(inside && Arena.OnFloor(p) ? 1 : 2);
            }
            return _floor[i] == 1;
        }

        public static Color32 At(Color32[] ramp, float u)
            => ramp[Mathf.Clamp(Mathf.FloorToInt(u * ramp.Length), 0, ramp.Length - 1)];

        protected static Color32 Lerp(Color32 a, Color32 b, float t) => Color32.Lerp(a, b, Mathf.Clamp01(t));
    }
}
