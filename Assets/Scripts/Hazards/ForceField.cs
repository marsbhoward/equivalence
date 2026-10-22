using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    public enum FieldColor { Blue, Red }

    /// <summary>
    /// A barrier strung between two columns. Bodies walk through it freely (a trigger collider),
    /// but it blocks or amplifies anything that tries to attack across it - Blue nulls a
    /// projectile/beam outright, Red doubles its damage (see HazardQuery). It can never be
    /// attacked itself - there is no Health component at all - and its own lifetime is entirely
    /// derived from its two anchors: destroy either one and the field drops.
    ///
    /// Anchors are POLLED, not subscribed to. A domain reload silently drops C# events/delegates
    /// (this project's own notes on Health.ModifyIncoming and PlayerController.ModsSource are the
    /// same lesson) - a plain Column reference survives that exactly the way any UnityEngine.Object
    /// reference does, and Unity's own overloaded null check already tells the truth about whether
    /// it has been destroyed. This is also what makes "a field between two non-cracked columns can
    /// never be destroyed" free: a non-cracked column has no Health and so can never become null.
    /// </summary>
    public class ForceField : MonoBehaviour
    {
        /// <summary>Named apart from UnityEngine.Color on purpose - a member called "Color" on this
        /// class would shadow the type of that name for every method below.</summary>
        public FieldColor Charge { get; private set; }

        [SerializeField] Column _anchorA, _anchorB;
        [SerializeField] bool _flipping;
        float _flipTimer;

        // ---- the curtain ----
        //
        // A sheet of light STANDING on the boundary, as tall as the shorter column it hangs
        // between (the user's call, 2026-10-21, from the mock-up's "tall curtain" - the old
        // glowing line on the floor was drawn over everything, so with standing columns it ran
        // up the faces of the ones in front). Texel drawn at body density into its own texture
        // and redrawn a few times a second for its SHIMMER: a band of light sweeping along it,
        // flickering vertical lines drifting through it, glints rising, a ripple on its top edge.
        //
        // Depth sorted one order BEHIND its southern anchor - so that column covers it, and it
        // covers the northern one and anything beyond. A field running north-south is seen
        // edge on: a bright strip from the south column to the north one's drawn rim, and up.
        //
        // Picture only: the barrier is still the BoxCollider2D on the ground between the two
        // columns' centres, Tuning.Hazards.ForceFieldWidth across, which HazardQuery reads.

        const float Ppu = 37.5f;
        const int FloorRows = 2;   // the sheet's light on the floor in front of its foot

        [SerializeField] SpriteRenderer _curtain;
        Texture2D _tex;
        Color32[] _px;
        int _w, _h, _sheet, _foot;
        bool _edgeOn;
        float _nextDraw;

        public static ForceField Spawn(Column a, Column b, FieldColor charge, bool flipping, Transform parent)
        {
            var go = new GameObject("force-field");
            go.transform.SetParent(parent, false);

            var field = go.AddComponent<ForceField>();
            field._anchorA = a;
            field._anchorB = b;
            field.Charge = charge;
            field._flipping = flipping;
            field._flipTimer = Tuning.Hazards.ForceFieldFlipInterval;

            // Anchors never move once placed, so the span geometry is computed once here.
            Vector2 from = a.transform.position;
            Vector2 to = b.transform.position;
            Vector2 delta = to - from;
            float length = delta.magnitude;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;

            go.transform.position = (from + to) * 0.5f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(Tuning.Hazards.ForceFieldWidth, length, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;   // bodies pass through; HazardQuery's raycast still sees it (useTriggers = true)
            col.size = Vector2.one;

            // The picture is NOT a child: the root is rotated and stretched for its collider.
            var picture = new GameObject("force-field.curtain");
            picture.transform.SetParent(parent, false);
            field._curtain = picture.AddComponent<SpriteRenderer>();
            field.BuildCurtain();
            return field;
        }

        void BuildCurtain()
        {
            if (_curtain == null || _anchorA == null || _anchorB == null) return;
            Column south = _anchorA.transform.position.y <= _anchorB.transform.position.y ? _anchorA : _anchorB;
            Column north = south == _anchorA ? _anchorB : _anchorA;
            Vector2 s = south.transform.position, n = north.transform.position;

            float ha = _anchorA.DrawnHeight, hb = _anchorB.DrawnHeight;
            float height = ha > 0f && hb > 0f ? Mathf.Min(ha, hb) * Tuning.Hazards.ForceFieldCurtainShare
                                              : Tuning.Hazards.ForceFieldCurtainFallback;
            _edgeOn = Mathf.Abs(s.x - n.x) < 0.01f;
            _sheet = Mathf.Max(3, Mathf.RoundToInt(height * Ppu));

            Vector2 pivot;
            if (!_edgeOn)
            {
                // Across the screen: the sheet stands on the line between the centres; each
                // column's shaft covers an end (it is drawn after it, or beside it in front).
                float x0 = Mathf.Min(s.x, n.x), x1 = Mathf.Max(s.x, n.x);
                _w = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) * Ppu));
                _foot = 0;
                _h = FloorRows + 1 + _sheet;
                pivot = new Vector2(0f, FloorRows / (float)_h);
                _curtain.transform.position = new Vector3(x0, s.y, 0f);
            }
            else
            {
                // Edge on: from the south column's centre (hidden behind it) to the north one's
                // drawn front rim, and the sheet's height above that.
                float rim = n.y - north.Radius * ColumnArt.Squash;
                _foot = Mathf.Max(1, Mathf.RoundToInt((rim - s.y) * Ppu));
                _w = 5;
                _h = _foot + _sheet;
                pivot = new Vector2(0.5f, 0f);
                _curtain.transform.position = new Vector3(s.x, s.y, 0f);
            }

            _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _px = new Color32[_w * _h];
            var sprite = Sprite.Create(_tex, new Rect(0, 0, _w, _h), pivot, Ppu);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            _curtain.sprite = sprite;
            _curtain.sortingOrder = SortingOrders.ForDepth(s.y) - 1;
            _nextDraw = 0f;
            Draw();
        }

        void OnDestroy()
        {
            if (_curtain != null) Destroy(_curtain.gameObject);
        }

        void Update()
        {
            if (_anchorA == null || _anchorB == null) { Destroy(gameObject); return; }

            if (_flipping)
            {
                _flipTimer -= Time.deltaTime;
                if (_flipTimer <= 0f)
                {
                    _flipTimer = Tuning.Hazards.ForceFieldFlipInterval;
                    Charge = Charge == FieldColor.Blue ? FieldColor.Red : FieldColor.Blue;
                    _nextDraw = 0f;   // the new colour at once, not at the next shimmer frame
                }
            }

            if (_tex == null) BuildCurtain();   // a domain reload drops the texture
            if (Time.time >= _nextDraw) Draw();

            // Flip-capable fields breathe as a whole - a steady light reads as "this is what it
            // is", a pulsing one as "this is about to change".
            float pulse = _flipping ? 0.7f + 0.3f * Mathf.Sin(Time.time * 4f) : 1f;
            if (_curtain != null) _curtain.color = new Color(1f, 1f, 1f, pulse);
        }

        Color BaseColor => Charge == FieldColor.Blue ? new Color(0.35f, 0.65f, 1f) : new Color(1f, 0.35f, 0.35f);

        void Draw()
        {
            if (_tex == null || _px == null) return;
            _nextDraw = Time.time + 1f / Tuning.Hazards.ForceFieldShimmerFps;
            float t = Time.time;
            Color col = BaseColor;
            Color spine = Color.Lerp(col, Color.white, 0.4f);
            Color foot = Color.Lerp(col, Color.white, 0.5f);
            System.Array.Clear(_px, 0, _px.Length);

            if (Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest)) { DrawWeb(col, spine, t); _tex.SetPixels32(_px); _tex.Apply(false); return; }
            if (Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SkyRuin)) { DrawChain(col, spine, t); _tex.SetPixels32(_px); _tex.Apply(false); return; }
            if (Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SaltFlats)) { DrawThreads(col, spine, t); _tex.SetPixels32(_px); _tex.Apply(false); return; }

            if (_edgeOn)
            {
                for (int y = 0; y < _h; y++)
                {
                    // the sweep runs up the strip
                    float band = Mathf.Repeat(y - t * Tuning.Hazards.ForceFieldSweepSpeed, 24f);
                    float lift = band < 2f ? 0.25f : band < 4f ? 0.12f : 0f;
                    Set(2, y, spine, 0.55f + lift);
                    Set(1, y, col, 0.22f + lift * 0.5f);
                    Set(3, y, col, 0.22f + lift * 0.5f);
                    Set(0, y, col, 0.07f);
                    Set(4, y, col, 0.07f);
                }
            }
            else
            {
                int ground = FloorRows;
                for (int x = 0; x < _w; x++)
                {
                    uint hx = Hash(x);
                    // vertical lines drifting through the sheet, each flickering on its own beat
                    bool line = (x + Mathf.FloorToInt(t * Tuning.Hazards.ForceFieldLineDrift)) % 4 == 0;
                    float flicker = line ? 0.05f + 0.05f * Mathf.Sin(t * 9f + (hx & 255) * 0.1f) : 0f;
                    // a glint rising up this column, on some columns only
                    float glint = (hx % 7u == 0u) ? Mathf.Repeat(t * 22f + (hx >> 8 & 255), _sheet * 1.6f) : -99f;

                    for (int k = 0; k < _sheet; k++)
                    {
                        float a = 0.13f + 0.10f * (k / (float)_sheet) + flicker;
                        // a band of light sweeping along the curtain, leaning as it goes
                        float band = Mathf.Repeat(x + k * 0.6f - t * Tuning.Hazards.ForceFieldSweepSpeed, 46f);
                        if (band < 2f) a += 0.24f; else if (band < 4f) a += 0.11f;
                        Color c = col;
                        if (Mathf.Abs(k - glint) < 1f) { a += 0.35f; c = spine; }
                        Set(x, ground + 1 + k, c, a);
                    }
                    int top = ground + _sheet;
                    // the top edge, rippling
                    Set(x, top, spine, 0.78f + 0.17f * Mathf.Sin(x * 0.35f - t * 6f));
                    Set(x, top - 1, col, 0.35f);
                    // its foot on the floor, and the light it throws in front
                    Set(x, ground, foot, 0.6f);
                    Set(x, ground - 1, col, 0.18f);
                    Set(x, ground - 2, col, 0.08f);
                }
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        /// <summary>
        /// The forest's force field is a WEB strung between the trees (the user's call) - in the
        /// field's CHARGE colour, which is gameplay: blue silk and red silk stay what they were.
        /// Sagging threads, an orb in the middle, dew glints running along the silk; a faint line
        /// on the ground still marks where the barrier stands.
        /// </summary>
        void DrawWeb(Color col, Color spine, float t)
        {
            var silk = Color.Lerp(Color.white, col, 0.55f);
            if (_edgeOn)
            {
                for (int y = 0; y < _h; y++)
                {
                    float glint = Mathf.Repeat(y - t * 18f, 30f) < 2f ? 0.4f : 0f;
                    Set(1, y, silk, 0.42f + glint);
                    Set(3, y, silk, 0.3f + (Mathf.Repeat(y + 11 - t * 14f, 26f) < 2f ? 0.4f : 0f));
                    if (y % 7 == 0) Set(2, y, silk, 0.5f);
                }
                return;
            }

            int ground = FloorRows;
            for (int x = 0; x < _w; x++) Set(x, ground, col, 0.22f);

            // threads strung post to post, sagging
            float[] heights = { 0.86f, 0.58f, 0.3f };
            for (int i = 0; i < heights.Length; i++)
            {
                float y0 = ground + 1 + _sheet * heights[i], sagBy = _sheet * 0.08f * (i + 1);
                for (int x = 0; x < _w; x++)
                {
                    float u = x / (float)Mathf.Max(1, _w - 1);
                    int y = Mathf.RoundToInt(y0 - sagBy * Mathf.Sin(Mathf.PI * u));
                    float glint = Mathf.Repeat(x + i * 17 - t * 20f, 60f) < 2f ? 0.45f : 0f;
                    Set(x, y, silk, 0.48f + glint);
                }
            }

            // the orb: spokes and a spiral, in the middle of the span
            float cx = _w * 0.5f, cy = ground + 1 + _sheet * 0.55f;
            float rad = Mathf.Min(_w * 0.32f, _sheet * 0.42f);
            const int spokes = 11;
            for (int k = 0; k < spokes; k++)
            {
                float a = k / (float)spokes * Mathf.PI * 2f + 0.2f;
                for (float d = 1f; d < rad; d += 0.7f)
                    Set(Mathf.RoundToInt(cx + Mathf.Cos(a) * d), Mathf.RoundToInt(cy + Mathf.Sin(a) * d), silk, 0.4f);
            }
            for (float d = 2.5f; d < rad; d += 3f)
            {
                int steps = Mathf.CeilToInt(d * 7f);
                for (int i = 0; i < steps; i++)
                {
                    float a = i / (float)steps * Mathf.PI * 2f;
                    Set(Mathf.RoundToInt(cx + Mathf.Cos(a) * d), Mathf.RoundToInt(cy + Mathf.Sin(a) * d), silk, 0.34f);
                }
            }
            // dew catching the light, coming and going
            for (int i = 0; i < 7; i++)
            {
                float a = i * 2.4f, d = rad * (0.25f + 0.1f * i);
                float on = Mathf.Sin(t * 3f + i * 1.9f);
                if (on > 0.4f) Set(Mathf.RoundToInt(cx + Mathf.Cos(a) * d), Mathf.RoundToInt(cy + Mathf.Sin(a) * d), spine, 0.6f + on * 0.4f);
            }
        }

        /// <summary>
        /// The Sky Ruin's force field is a CHAIN OF LIGHT slung between the columns - links in the
        /// field's CHARGE colour (gameplay), a pulse running along it; the ground line as ever.
        /// </summary>
        void DrawChain(Color col, Color spine, float t)
        {
            var link = Color.Lerp(Color.white, col, 0.6f);
            if (_edgeOn)
            {
                for (int y = 0; y < _h; y++)
                {
                    float pulse = Mathf.Repeat(y - t * 24f, 40f) < 4f ? 0.4f : 0f;
                    bool ring = (y / 4) % 2 == 0;
                    Set(ring ? 1 : 2, y, link, 0.6f + pulse); if (ring) Set(3, y, link, 0.6f + pulse);
                    Set(0, y, col, 0.1f); Set(4, y, col, 0.1f);
                }
                return;
            }
            int ground = FloorRows;
            for (int x = 0; x < _w; x++) Set(x, ground, col, 0.22f);
            // two chains, sagging, one above the other
            for (int c = 0; c < 2; c++)
            {
                float y0 = ground + 1 + _sheet * (c == 0 ? 0.78f : 0.42f), sagBy = _sheet * 0.1f;
                for (int x = 0; x < _w; x++)
                {
                    float u = x / (float)Mathf.Max(1, _w - 1);
                    int y = Mathf.RoundToInt(y0 - sagBy * Mathf.Sin(Mathf.PI * u));
                    float pulse = Mathf.Max(0f, 1f - Mathf.Abs(Mathf.Repeat(t * 0.5f + c * 0.4f, 1.4f) - u) * 9f);
                    var cc = Color.Lerp(link, Color.white, pulse);
                    int phase = (x + c * 3) % 8;
                    if (phase < 5)
                    {
                        // an oval link seen side on: top and bottom rails
                        Set(x, y + 1, cc, 0.75f + pulse * 0.25f);
                        Set(x, y - 1, cc, 0.75f + pulse * 0.25f);
                        if (phase == 0 || phase == 4) Set(x, y, cc, 0.75f);
                    }
                    else Set(x, y, cc, 0.85f + pulse * 0.15f);   // the link through it, edge on
                    Set(x, y + 2, col, 0.12f + pulse * 0.2f);      // its glow
                    Set(x, y - 2, col, 0.12f + pulse * 0.2f);
                }
            }
        }

        /// <summary>
        /// The Salt Flats' force field: glassy brine THREADS, three strung post to post, in the
        /// field's CHARGE colour, a prism glint running along each.
        /// </summary>
        void DrawThreads(Color col, Color spine, float t)
        {
            var glass = Color.Lerp(Color.white, col, 0.5f);
            Color[] prism = { new(1f, 0.55f, 0.6f), new(1f, 0.92f, 0.5f), new(0.55f, 1f, 0.8f), new(0.6f, 0.7f, 1f) };
            if (_edgeOn)
            {
                for (int y = 0; y < _h; y++)
                {
                    Set(1, y, glass, 0.5f); Set(3, y, glass, 0.5f);
                    int g = Mathf.FloorToInt(Mathf.Repeat(y - t * 20f, 36f));
                    if (g < 4) { Set(1, y, prism[g], 0.9f); Set(3, y, prism[3 - g], 0.9f); }
                }
                return;
            }
            int ground = FloorRows;
            for (int x = 0; x < _w; x++) Set(x, ground, col, 0.22f);
            for (int k = 0; k < 3; k++)
            {
                float y0 = ground + 1 + _sheet * (0.3f + k * 0.27f), sagBy = _sheet * 0.05f * (k + 1);
                float glintAt = Mathf.Repeat(t * 0.4f + k * 0.33f, 1.3f) * _w;
                for (int x = 0; x < _w; x++)
                {
                    float u = x / (float)Mathf.Max(1, _w - 1);
                    int y = Mathf.RoundToInt(y0 - sagBy * Mathf.Sin(Mathf.PI * u));
                    Set(x, y, glass, 0.7f);
                    Set(x, y - 1, col, 0.18f);
                    int g = Mathf.FloorToInt(glintAt - x);
                    if (g >= 0 && g < 4) Set(x, y, prism[g], 0.95f);
                }
            }
        }

        void Set(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            _px[y * _w + x] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f),
                                          (byte)(Mathf.Clamp01(a) * 255f));
        }

        static uint Hash(int x)
        {
            unchecked
            {
                uint h = (uint)x * 0x9E3779B1u;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12;
                return h;
            }
        }
    }
}
