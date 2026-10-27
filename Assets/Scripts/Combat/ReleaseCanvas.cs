using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// A one-shot picture on the floor's texel grid - what the elemental releases draw on the
    /// ground (Erupt's sigil blast, Soak's ripple, Burst's sigil rings, the Quake's fault). One
    /// texture round a fixed centre, redrawn every frame from the effect's age until
    /// <see cref="Draw"/> says it is over. On the ground layer, over the pits and under every
    /// body. A picture only.
    ///
    /// The texture is not kept across a domain reload; an effect that loses it simply ends.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public abstract class ReleaseCanvas : MonoBehaviour
    {
        protected const float Ppu = Art.FloorArt.PixelsPerUnit;

        [SerializeField] protected SpriteRenderer Sr;
        [SerializeField] protected float Age;
        /// <summary>Texels per world unit of this canvas (the floor's by default), and whether it
        /// is clipped to standing room - a ground picture is, a spark in the air is not.</summary>
        [SerializeField] protected float TexelPpu = Ppu;
        [SerializeField] protected bool Clip = true;
        protected Texture2D Tex;
        protected Color32[] Px;
        /// <summary>Per texel, asked once: 1 standing room, 2 not (a wall, a chasm) - nothing is
        /// drawn there, so a ring never runs up a wall's face or out over the void.</summary>
        sbyte[] _floor;
        /// <summary>Canvas size, and the centre's texel inside it.</summary>
        protected int W, H, Cx, Cy;

        /// <summary>Make one, centred on <paramref name="centre"/> (snapped to the floor's grid),
        /// <paramref name="halfW"/> texels either side, <paramref name="below"/> under the centre and
        /// <paramref name="above"/> over it.</summary>
        protected static T Make<T>(string name, Vector2 centre, int halfW, int below, int above, int order,
                                   float ppu = Ppu, bool clip = true) where T : ReleaseCanvas
        {
            var go = new GameObject(name);
            var c = go.AddComponent<T>();
            c.TexelPpu = ppu;
            c.Clip = clip;
            c.Sr = go.AddComponent<SpriteRenderer>();
            c.Sr.sortingOrder = order;
            c.W = halfW * 2 + 1; c.H = below + above + 1; c.Cx = halfW; c.Cy = below;
            c.Tex = new Texture2D(c.W, c.H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            c.Px = new Color32[c.W * c.H];
            c._floor = new sbyte[c.W * c.H];
            c.Sr.sprite = Sprite.Create(c.Tex, new Rect(0, 0, c.W, c.H), Vector2.zero, ppu, 0, SpriteMeshType.FullRect);
            var snapped = new Vector2(Mathf.Round(centre.x * ppu), Mathf.Round(centre.y * ppu));
            go.transform.position = new Vector3((snapped.x - c.Cx) / ppu, (snapped.y - c.Cy) / ppu, 0f);
            c.Sr.enabled = false;
            return c;
        }

        /// <summary>Draw the frame at <see cref="Age"/>; false when the effect is over.</summary>
        protected abstract bool Draw();

        void LateUpdate()
        {
            if (Px == null || Tex == null) { Destroy(gameObject); return; }
            Age += Time.deltaTime;
            System.Array.Clear(Px, 0, Px.Length);
            if (!Draw()) { Destroy(gameObject); return; }
            Tex.SetPixels32(Px);
            Tex.Apply();
            Sr.enabled = true;
        }

        void OnDestroy()
        {
            if (Sr != null && Sr.sprite != null) Destroy(Sr.sprite);
            if (Tex != null) Destroy(Tex);
        }

        // ---- drawing, in texels from the centre ----

        protected void Put(int dx, int dy, Color32 c)
        {
            int x = Cx + dx, y = Cy + dy;
            if (x < 0 || y < 0 || x >= W || y >= H || !Standing(x, y)) return;
            Px[y * W + x] = c;
        }

        bool Standing(int x, int y)
        {
            int i = y * W + x;
            if (_floor == null || !Clip) return true;
            if (_floor[i] == 0)
            {
                // Inside the DRAWN walls (their inner faces stand WallInset in from the arena's
                // bounds), and off any interior wall or chasm.
                var p = (Vector2)transform.position + new Vector2(x + 0.5f, y + 0.5f) / TexelPpu;
                var inner = Core.Arena.HalfExtents - Vector2.one * Core.Tuning.Scenery.WallInset;
                bool inside = Mathf.Abs(p.x) <= inner.x && Mathf.Abs(p.y) <= inner.y;
                _floor[i] = (sbyte)(inside && Core.Arena.OnFloor(p) ? 1 : 2);
            }
            return _floor[i] == 1;
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
            float oa = o.a / 255f, na = ca + oa * (1f - ca);
            float k = ca / na;
            Px[i] = new Color32((byte)(c.r * k + o.r * (1f - k)), (byte)(c.g * k + o.g * (1f - k)),
                                (byte)(c.b * k + o.b * (1f - k)), (byte)(na * 255f));
        }

        /// <summary>Every texel whose distance from the centre (plus an offset) lies in
        /// [<paramref name="r0"/>, <paramref name="r1"/>]: its position, distance and angle.</summary>
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

        /// <summary>The sigil emblem (Combat.SigilEmblem), flattened as the heat sigil is, in one colour.</summary>
        protected void Sigil(int half, Color32 c, float alpha = 1f)
        {
            if (!_sigils.TryGetValue(half, out var s))
            {
                var made = SigilEmblem.Ink(half, Core.Tuning.FireFx.SigilSquash, Core.Tuning.FireFx.SigilWeight, out int mw, out int mh);
                _sigils[half] = s = (made, mw, mh);
            }
            var (ink, w, h) = s;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (ink[y * w + x]) Over(x - w / 2, y - h / 2, c, alpha);
        }

        // Plain static cache: a reload empties it and it refills.
        static System.Collections.Generic.Dictionary<int, (bool[], int, int)> _sigils = new();

        public static Color32 At(Color32[] ramp, float u)
            => ramp[Mathf.Clamp(Mathf.FloorToInt(u * ramp.Length), 0, ramp.Length - 1)];

        /// <summary>One under the lowest layer of the rig under <paramref name="body"/> - where a
        /// picture goes that may pass in front of the body but must never cover it.</summary>
        public static int UnderBody(Transform body)
        {
            var rig = body != null ? body.GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour : null;
            if (rig == null) return Core.SortingOrders.Character - 1;
            int low = int.MaxValue;
            foreach (var r in rig.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (r == null || !r.enabled || r.sprite == null || r.sortingOrder >= Core.SortingOrders.StatusOverlay) continue;
                if (r.sortingOrder < low) low = r.sortingOrder;
            }
            return (low == int.MaxValue ? Core.SortingOrders.Character : low) - 1;
        }

        /// <summary>A body's contact shadow - on the ground under its feet - or its own position.</summary>
        public static Vector2 Feet(Transform body) => body == null ? Vector2.zero : (Vector2)ShadowOf(body).position;

        /// <summary>The transform <see cref="Feet"/> reads - kept by anything asking every frame.</summary>
        public static Transform ShadowOf(Transform body)
        {
            foreach (var t in body.GetComponentsInChildren<Transform>(true))
                if (t.name == "shadow") return t;
            return body;
        }
    }
}
