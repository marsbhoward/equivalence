using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// ARRIVAL BEATS (the user's picks, 2026-10-23): the moment a floor opens is marked, without
    /// ever holding the player (control is theirs from the first frame; it all fits in the entry
    /// grace).
    ///
    ///   - a GATE in the south boundary behind the arrival point, in the place's own terms - an
    ///     arch with stairs going down in the Tower, a trampled gap in the thicket, a stair falling
    ///     away into the clouds in the Sky Ruin, a cleft in the salt - so the player has just come
    ///     through it. A picture only: the wall's collider is untouched.
    ///   - DUST at the feet as the screen opens, in the place's material: grit, leaves, a cloud's
    ///     wisp, salt; and a burst of cinders under the Nigredo.
    ///   - the sting and the room tone's swell are Audio's (Sound.Arrive, Ambience.Swell).
    ///
    /// Never <c>UnityEngine.Random</c>; hashed from the floor's seed.
    /// </summary>
    public static class Arrival
    {
        const float Ppu = FloorArt.PixelsPerUnit;

        // ------------------------------------------------------------------ the gate

        /// <summary>Draw this floor's gate under <paramref name="parent"/> (last floor's goes).</summary>
        public static void Gate(Transform parent, Vector2 half, int seed)
        {
            if (parent == null) return;
            var old = parent.Find("arrival.gate");
            if (old != null) { old.name = "arrival.gate.old"; Object.Destroy(old.gameObject); }

            float h = half.y - Tuning.Scenery.WallInset;           // the wall's inner face
            float top = -h + 0.12f, bottom = -h - Tuning.Scenery.TopBand - 0.55f;
            int W = Mathf.RoundToInt(2.6f * Ppu) & ~1, H = Mathf.RoundToInt((top - bottom) * Ppu);
            var px = Draw(Biome.Current, W, H, Mathf.RoundToInt(Tuning.Scenery.TopBand * Ppu), seed);

            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(px); tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), Ppu, 0, SpriteMeshType.FullRect);
            sp.hideFlags = HideFlags.HideAndDontSave;

            var go = new GameObject("arrival.gate");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, Mathf.Round(bottom * Ppu) / Ppu, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = -7;   // over the wall tops (-8), under everything that stands
        }

        /// <summary>The gate, bottom row first: two posts flanking a passage that runs out south
        /// through the wall's top band (<paramref name="band"/> texels) and fades into what lies beyond.</summary>
        static Color[] Draw(BiomeKind biome, int W, int H, int band, int seed)
        {
            var px = new Color[W * H];
            int cx = W / 2, gapHalf = Mathf.RoundToInt(0.62f * Ppu), post = Mathf.RoundToInt(0.34f * Ppu);
            Color ink = new(0.05f, 0.045f, 0.06f);

            // Each place's materials: post (dark, mid, lit), passage (near, far) and what it fades to.
            Color[] postC; Color near, far, beyond;
            switch (biome)
            {
                case BiomeKind.Forest:
                    postC = new[] { new Color(0.06f, 0.13f, 0.07f), new Color(0.12f, 0.24f, 0.1f), new Color(0.24f, 0.4f, 0.16f) };
                    near = new Color(0.33f, 0.25f, 0.15f); far = new Color(0.16f, 0.12f, 0.08f); beyond = new Color(0.03f, 0.06f, 0.04f);
                    break;
                case BiomeKind.SkyRuin:
                    postC = new[] { new Color(0.5f, 0.48f, 0.45f), new Color(0.7f, 0.68f, 0.64f), new Color(0.86f, 0.84f, 0.8f) };
                    near = new Color(0.66f, 0.63f, 0.58f); far = new Color(0.78f, 0.84f, 0.92f); beyond = new Color(0.62f, 0.76f, 0.92f);
                    break;
                case BiomeKind.SaltFlats:
                    postC = new[] { new Color(0.66f, 0.58f, 0.6f), new Color(0.86f, 0.8f, 0.8f), new Color(0.97f, 0.95f, 0.94f) };
                    near = new Color(0.74f, 0.66f, 0.64f); far = new Color(0.6f, 0.52f, 0.55f); beyond = new Color(0.84f, 0.8f, 0.86f);
                    break;
                default:
                    postC = new[] { new Color(0.2f, 0.19f, 0.2f), new Color(0.34f, 0.32f, 0.33f), new Color(0.5f, 0.47f, 0.46f) };
                    near = new Color(0.3f, 0.28f, 0.27f); far = new Color(0.06f, 0.055f, 0.06f); beyond = new Color(0.03f, 0.03f, 0.035f);
                    break;
            }

            // The posts' shapes first, as a mask: masonry blocks in the Tower and the sky ruin, a
            // leafy clump in the forest, a cluster of pointed salt crystals on the flats - so the
            // outline follows the shape, never a box.
            var postMask = new bool[W * H];
            int baseY = H - band - 3;
            for (int side = -1; side <= 1; side += 2)
            {
                int pc = cx + side * (gapHalf + post / 2);
                for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int ox = x - pc, oy = y - baseY;
                    bool inside;
                    switch (biome)
                    {
                        case BiomeKind.Forest:      // three overlapping leaf-balls, ragged at the rim
                            inside = false;
                            for (int k = 0; k < 3; k++)
                            {
                                float bx = (k - 1) * post * 0.42f, by = band * (0.25f + 0.32f * (k % 2)) + k * 2f, r = post * 0.62f;
                                float d = Mathf.Sqrt((ox - bx) * (ox - bx) + (oy - by) * (oy - by));
                                if (d < r - (Hash(x, y, seed + k) < 0.3f ? 1f : 0f)) inside = true;
                            }
                            break;
                        case BiomeKind.SaltFlats:   // crystals: each a column rising to a point
                            inside = false;
                            for (int k = 0; k < 3; k++)
                            {
                                float bx = (k - 1) * post * 0.36f, hgt = band * (0.75f + 0.35f * Hash(k, side, seed)) + (k == 1 ? 5 : 0);
                                float hw = post * 0.22f * Mathf.Clamp01((hgt - oy) / (post * 0.35f));
                                if (oy >= 0 && oy <= hgt && Mathf.Abs(ox - bx) <= hw) inside = true;
                            }
                            break;
                        default:
                            inside = Mathf.Abs(ox) <= post / 2 - 1 && oy >= 0 && y < H - 1;
                            break;
                    }
                    if (inside && y >= 0) postMask[y * W + x] = true;
                }
            }
            bool M(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && postMask[y * W + x];

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int dx = Mathf.Abs(x - cx), i = y * W + x;
                float u = y / (float)(H - 1);   // 0 at the far (south) end, 1 at the room
                float hsh = Hash(x, y, seed);

                if (M(x, y))
                {
                    bool top = !M(x, y + 1);
                    Color c = top ? postC[2] : (x < cx ? (x - (cx - gapHalf - post)) < post / 2 : (x - (cx + gapHalf)) < post / 2) ? postC[1] : postC[0];
                    if (biome == BiomeKind.Forest) c = postC[hsh < 0.3f ? 0 : hsh < 0.75f ? 1 : 2];
                    else if (biome == BiomeKind.SaltFlats && hsh > 0.92f) c = Color.white;
                    else if (biome != BiomeKind.Forest && biome != BiomeKind.SaltFlats && (y - baseY) % 9 == 0) c = postC[0];   // a course line
                    px[i] = c;
                    continue;
                }
                if (M(x + 1, y) || M(x - 1, y) || M(x, y + 1) || M(x, y - 1)) { px[i] = ink; continue; }   // its outline

                // The passage between the posts, a texel ragged at its sides in the wild places,
                // and frayed where it runs out into what lies beyond (no hard end).
                int ragged = biome == BiomeKind.Tower ? 0 : (Hash(y >> 1, 3, seed) < 0.5f ? 1 : 0);
                if (dx >= gapHalf - ragged) continue;
                float fray = Mathf.Clamp01((u - 0.02f) / 0.3f);
                if (Hash(x >> 1, y >> 1, seed + 5) > fray) continue;
                Color pc2 = Color.Lerp(far, near, Mathf.SmoothStep(0f, 1f, u));
                switch (biome)
                {
                    case BiomeKind.Tower:      // stairs going down: a lit nosing on every step
                    case BiomeKind.SkyRuin:    // ... into the clouds
                        int step = Mathf.FloorToInt((H - 1 - y) / 4f);
                        if ((H - 1 - y) % 4 == 0) pc2 = Color.Lerp(pc2, Color.white, 0.18f * u);
                        else if ((H - 1 - y) % 4 == 3) pc2 = Color.Lerp(pc2, ink, 0.25f);
                        pc2 = Color.Lerp(pc2, beyond, Mathf.Clamp01(step / 9f) * (biome == BiomeKind.SkyRuin ? 0.9f : 0.7f));
                        break;
                    case BiomeKind.Forest:     // trampled earth, leaves on it
                        if (hsh < 0.08f) pc2 = new Color(0.45f, 0.3f, 0.12f);
                        else if (hsh > 0.93f) pc2 = new Color(0.22f, 0.36f, 0.14f);
                        pc2 = Color.Lerp(pc2, beyond, (1f - u) * 0.6f);
                        break;
                    case BiomeKind.SaltFlats:  // a worn path, a step darker than the crust so it reads
                        if (hsh > 0.97f) pc2 = Color.white;
                        else if (hsh < 0.1f) pc2 = Color.Lerp(pc2, far, 0.6f);
                        pc2 = Color.Lerp(pc2, beyond, (1f - u) * 0.5f);
                        break;
                }
                px[i] = pc2;
            }
            return px;
        }

        // ------------------------------------------------------------------ dust at the feet

        /// <summary>A small kick-up at <paramref name="at"/> as the screen opens, in the place's
        /// material; a burst of cinders too under the Nigredo. Plays on unscaled time, so it runs
        /// while the fade is still lifting.</summary>
        public static void Dust(Vector2 at, BiomeKind biome, bool nigredo, int seed)
        {
            var go = new GameObject("arrival.dust");
            go.transform.position = at;
            go.AddComponent<ArrivalDust>().Begin(biome, nigredo, seed);
        }

        internal static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + (s + 1) * 982451653);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }

    /// <summary>The arrival's dust: a few texels flung from the feet (or leaves drifting down
    /// through the canopy), each fading; cinders rising under the Nigredo.</summary>
    public class ArrivalDust : MonoBehaviour
    {
        const float Ppu = FloorArt.PixelsPerUnit, Life = 0.9f;
        [SerializeField] Transform[] _p;
        [SerializeField] SpriteRenderer[] _sr;
        [SerializeField] Vector2[] _v;
        [SerializeField] float[] _g;
        [SerializeField] Color[] _c;
        [SerializeField] float _t;

        static Sprite _dot;
        static Sprite Dot
        {
            get
            {
                if (_dot != null) return _dot;
                var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixel(0, 0, Color.white); tex.Apply();
                _dot = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), Ppu);
                _dot.hideFlags = HideFlags.HideAndDontSave;
                return _dot;
            }
        }

        public void Begin(BiomeKind biome, bool nigredo, int seed)
        {
            int n = biome == BiomeKind.Forest ? 9 : 14, nc = nigredo ? 10 : 0;
            _p = new Transform[n + nc]; _sr = new SpriteRenderer[n + nc]; _v = new Vector2[n + nc]; _g = new float[n + nc]; _c = new Color[n + nc];
            for (int i = 0; i < n + nc; i++)
            {
                float a = Arrival.Hash(i, 1, seed), b = Arrival.Hash(i, 2, seed), c = Arrival.Hash(i, 3, seed);
                var go = new GameObject("d");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = Dot;
                bool cinder = i >= n;
                Vector2 pos, vel; float grav; Color col; float size = 1f;
                if (cinder)
                {
                    pos = new Vector2((a - 0.5f) * 0.9f, b * 0.2f);
                    vel = new Vector2((a - 0.5f) * 0.6f, 0.5f + c * 0.7f); grav = -0.15f;
                    col = new Color(0.86f, 0.2f, 0.16f, 0.85f);
                    sr.sortingOrder = SortingOrders.StatusOverlay - 6;   // a glow sits over the dark
                }
                else if (biome == BiomeKind.Forest)
                {
                    // leaves shaken down through the canopy, drifting
                    pos = new Vector2((a - 0.5f) * 1.8f, 0.8f + b * 1.2f);
                    vel = new Vector2((c - 0.5f) * 0.3f, -0.45f - b * 0.3f); grav = 0f;
                    col = c < 0.5f ? new Color(0.45f, 0.32f, 0.12f) : new Color(0.28f, 0.42f, 0.16f);
                    size = 2f;
                    sr.sortingOrder = SortingOrders.StatusOverlay - 7;
                }
                else
                {
                    // kicked up from the feet and settling
                    float ang = Mathf.Lerp(20f, 160f, a) * Mathf.Deg2Rad, sp = 0.6f + b * 1.1f;
                    pos = new Vector2((a - 0.5f) * 0.4f, 0f);
                    vel = new Vector2(Mathf.Cos(ang) * sp, Mathf.Sin(ang) * sp * 0.55f); grav = -2.2f;
                    col = biome == BiomeKind.SkyRuin ? new Color(0.92f, 0.94f, 0.97f, 0.8f)
                        : biome == BiomeKind.SaltFlats ? new Color(0.95f, 0.92f, 0.9f, 0.85f)
                        : new Color(0.42f, 0.39f, 0.36f, 0.9f);
                    if (biome == BiomeKind.SkyRuin) { size = 2f; grav = -0.4f; vel *= 0.6f; }   // a cloud's wisp: slower, softer
                    sr.sortingOrder = SortingOrders.DepthBase - 1;   // at the feet, behind everything standing
                }
                go.transform.localPosition = pos;
                go.transform.localScale = new Vector3(size, size >= 2f ? 1f : size, 1f);
                sr.color = col;
                _p[i] = go.transform; _sr[i] = sr; _v[i] = vel; _g[i] = grav; _c[i] = col;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            if (_t > Life + 0.6f) { Destroy(gameObject); return; }
            for (int i = 0; i < _p.Length; i++)
            {
                if (_p[i] == null) continue;
                _v[i].y += _g[i] * dt;
                var p = (Vector2)_p[i].localPosition + _v[i] * dt;
                _p[i].localPosition = p;
                var c = _c[i];
                c.a *= 1f - Mathf.Clamp01((_t - Life * 0.4f) / (Life * 0.8f));
                _sr[i].color = c;
            }
        }
    }
}
