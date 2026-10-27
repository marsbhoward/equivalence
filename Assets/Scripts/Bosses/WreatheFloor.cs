using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// The Wreathe's floor (the user's picks, group 5, 2026-10-26), texel by texel at body density
    /// over the whole room, drawn from <see cref="ArenaSectors.ZoneAt"/> - the same call the beat
    /// tests, so what is painted cannot drift from what hurts.
    ///
    ///     SEAMS   (GroundCarved, always) the lines between the slices and round the safe centre,
    ///             glowing faintly in its ember; enraged, the ring that splits every slice too.
    ///             The call's PRINT (the slice it sang from, outlined cold, gone in 0.6 s), a CUT
    ///             (the slice you stood in on the beat, flashing cold) and a silent note's ring.
    ///     DANGER  (GroundDanger, the answer) EVERY slice fills inward together - the clock, never
    ///             the answer - with a hard red edge; on the beat the WRONG slices crack and vent.
    ///
    /// Each layer is one texture, redrawn at most <see cref="Fps"/> times a second and only while
    /// something on it moves. The zone of every texel is cached (non-serialized arrays, rebuilt on
    /// first use after a reload).
    /// </summary>
    public class WreatheFloor : MonoBehaviour
    {
        const float Fps = 15f, PrintSeconds = 0.6f, CutSeconds = 0.45f;

        [SerializeField] SpriteRenderer _seams, _danger;
        [SerializeField] public bool Split;
        /// <summary>The answer's clock, 0..1; negative when no beat is coming.</summary>
        [SerializeField] public float Fill = -1f;
        [SerializeField] int _printZone = -2, _cutZone = -2, _ventSafe = -2, _silentZone = -2;
        [SerializeField] float _printAt = -9f, _cutAt = -9f, _ventAt = -9f, _silentAt = -9f;

        int _w, _h;
        Texture2D _seamTex, _dangerTex;
        Color32[] _seamPx, _dangerPx;
        sbyte[] _zone8, _zone16;
        byte[] _edge8, _edge16;   // 1: a line between zones (2 texels thick)
        float[] _rad;             // 0 the middle .. 1 the walls (the fill runs inward from 1)
        bool[] _crack;
        float _seamDrawn = -9f, _dangerDrawn = -9f;
        bool _dangerOn;

        public static WreatheFloor Build(Transform parent)
        {
            var go = new GameObject("wreathe.floor");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<WreatheFloor>();
            f._seams = Layer(go.transform, "seams", SortingOrders.GroundCarved);
            f._danger = Layer(go.transform, "danger", SortingOrders.GroundDanger);
            return f;
        }

        static SpriteRenderer Layer(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        public void Print(int zone) { _printZone = zone; _printAt = Time.time; }
        public void Silent(int zone) { _silentZone = zone; _silentAt = Time.time; }
        /// <summary>The beat: every slice but <paramref name="safe"/> vents; <paramref name="landed"/>
        /// flashes it cold under the player - a cut.</summary>
        public void Beat(int safe, bool landed)
        {
            _ventSafe = safe; _ventAt = Time.time;
            if (landed) { _cutZone = safe; _cutAt = Time.time; }
        }
        public void Clear() { Fill = -1f; _ventAt = _printAt = _cutAt = _silentAt = -9f; _seamDrawn = _dangerDrawn = -9f; }

        // ------------------------------------------------------------------ geometry

        void Ensure()
        {
            var inner = Arena.HalfExtents - Vector2.one * Tuning.Scenery.WallInset;
            int w = 2 * Mathf.CeilToInt(inner.x * TexelFx.Body), h = 2 * Mathf.CeilToInt(inner.y * TexelFx.Body);
            if (_zone8 != null && _w == w && _h == h && _seamTex != null) return;
            _w = w; _h = h;
            _zone8 = new sbyte[w * h]; _zone16 = new sbyte[w * h]; _edge8 = new byte[w * h]; _edge16 = new byte[w * h];
            _rad = new float[w * h]; _crack = new bool[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = World(x, y);
                    int i = y * w + x;
                    _zone8[i] = (sbyte)ArenaSectors.ZoneAt(p, false);
                    _zone16[i] = (sbyte)ArenaSectors.ZoneAt(p, true);
                    _rad[i] = Mathf.Max(Mathf.Abs(p.x) / inner.x, Mathf.Abs(p.y) / inner.y);
                    // cracks running out from the middle: a jagged radial line every ~11 degrees,
                    // wandering a little, broken in places, none inside the safe centre
                    float deg = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg + 180f, r = p.magnitude;
                    float lane = deg / 11.25f + (FireArt.Noise(r * 1.6f, Mathf.Floor(deg / 11.25f)) - 0.5f) * 0.5f;
                    float off = Mathf.Abs(lane - Mathf.Round(lane)) * 11.25f * Mathf.Deg2Rad * r;   // world units off the line
                    _crack[i] = r > Tuning.Boss.SafeRadius + 0.3f && off < 0.04f
                             && FireArt.Noise(r * 0.9f, Mathf.Round(lane) * 3.1f) > 0.35f;
                }
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    _edge8[i] = (byte)(Differs(_zone8, i, w) ? 1 : 0);
                    _edge16[i] = (byte)(Differs(_zone16, i, w) ? 1 : 0);
                }
            _seamTex = Tex(w, h); _dangerTex = Tex(w, h);
            _seamPx = new Color32[w * h]; _dangerPx = new Color32[w * h];
            Place(_seams, _seamTex); Place(_danger, _dangerTex);
            // A new Texture2D starts OPAQUE GREY: clear both before they are ever shown.
            Upload(_seamTex, _seamPx); Upload(_dangerTex, _dangerPx);
            _dangerOn = false;
        }

        static bool Differs(sbyte[] z, int i, int w) { var c = z[i]; return z[i + 1] != c || z[i - 1] != c || z[i + w] != c || z[i - w] != c; }

        Vector2 World(int x, int y) => new((x - _w / 2 + 0.5f) / TexelFx.Body, (y - _h / 2 + 0.5f) / TexelFx.Body);

        static Texture2D Tex(int w, int h) => new(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };

        void Place(SpriteRenderer sr, Texture2D tex)
        {
            if (sr.sprite != null) Destroy(sr.sprite);
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, _w, _h), new Vector2(0.5f, 0.5f), TexelFx.Body, 0, SpriteMeshType.FullRect);
            sr.transform.position = Vector3.zero;   // _w, _h are even: the texel grid lands on the world's
        }

        // ------------------------------------------------------------------ drawing

        void LateUpdate()
        {
            if (_seams == null || _danger == null) return;
            Ensure();
            float now = Time.time;
            bool seamBusy = now - _printAt < PrintSeconds || now - _cutAt < CutSeconds || now - _silentAt < 0.8f;
            if (now - _seamDrawn >= (seamBusy ? 1f / Fps : 0.25f)) { DrawSeams(now); _seamDrawn = now; }

            bool dangerBusy = Fill >= 0f || now - _ventAt < Tuning.Boss.VentSeconds;
            if (dangerBusy && now - _dangerDrawn >= 1f / Fps) { DrawDanger(now); _dangerDrawn = now; _dangerOn = true; }
            else if (!dangerBusy && _dangerOn) { System.Array.Clear(_dangerPx, 0, _dangerPx.Length); Upload(_dangerTex, _dangerPx); _dangerOn = false; }
        }

        static void Upload(Texture2D t, Color32[] px) { t.SetPixels32(px); t.Apply(false); }

        static Color32 A(Color32 c, float a) { c.a = (byte)(Mathf.Clamp01(a) * 255f); return c; }

        void DrawSeams(float now)
        {
            var zone = Split ? _zone16 : _zone8;
            var edge = Split ? _edge16 : _edge8;
            var E = WreatheCanvas.Ember; var Cd = WreatheCanvas.Cold;
            float print = 1f - (now - _printAt) / PrintSeconds, cut = 1f - (now - _cutAt) / CutSeconds;
            int shimmer = Mathf.FloorToInt(now * 2f);
            Vector2 silentAt = _silentZone >= 0 ? ArenaSectors.ZoneStation(_silentZone) * TexelFx.Body : Vector2.zero;
            float silentR = (now - _silentAt) * 18f, silentA = 1f - (now - _silentAt) / 0.8f;
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    int i = y * _w + x;
                    var c = default(Color32);
                    int z = zone[i];
                    if (edge[i] != 0)
                    {
                        c = A(E[2], ((x + y) / 40 + shimmer) % 2 == 0 ? 0.5f : 0.36f);
                        if (print > 0f && Touches(zone, i, _printZone)) c = Color32.Lerp(c, A(Cd[2], 1f), print);
                        if (cut > 0f && Touches(zone, i, _cutZone)) c = Color32.Lerp(c, A(Cd[0], 1f), cut);
                    }
                    else if (z == -1 && FireArt.Hash(x, y, 5) > 0.994f) c = A(E[2], 0.3f);
                    else if (cut > 0f && z == _cutZone && (x + y) % 3 == 0) c = A(Cd[1], cut * 0.5f);
                    if (silentA > 0f && _silentZone >= 0)
                    {
                        float dx = x - _w / 2 - silentAt.x, dy = (y - _h / 2 - silentAt.y) / 0.6f, d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (Mathf.Abs(d - silentR) < 0.8f) c = A(E[3], silentA * 0.7f);
                    }
                    _seamPx[i] = c;
                }
            Upload(_seamTex, _seamPx);
        }

        // An edge texel belongs to every zone it borders.
        bool Touches(sbyte[] zone, int i, int z) => z >= -1 && (zone[i] == z || zone[i + 1] == z || zone[i - 1] == z || zone[i + _w] == z || zone[i - _w] == z);

        void DrawDanger(float now)
        {
            var zone = Split ? _zone16 : _zone8;
            var edge = Split ? _edge16 : _edge8;
            var D = TexelFx.Danger;
            var fillCol = Color32.Lerp(new Color32(58, 20, 16, 255), D[4], 0.4f);
            float k = Fill;
            bool blink = k > 0.85f && Mathf.FloorToInt(now * 16f) % 2 == 0;
            float vent = (now - _ventAt) / Tuning.Boss.VentSeconds, fade = 1f - vent;
            int puff = Mathf.FloorToInt(now * 12f);
            var E = WreatheCanvas.Ember; var S = WreatheCanvas.Smoke;
            for (int y = 0; y < _h; y++)
                for (int x = 0; x < _w; x++)
                {
                    int i = y * _w + x;
                    var c = default(Color32);
                    int z = zone[i];
                    if (z >= 0)
                    {
                        if (k >= 0f)
                        {
                            if (edge[i] != 0) c = D[blink ? 2 : 3];
                            else if (_rad[i] > 1f - k) c = A(fillCol, 0.8f);
                        }
                        else if (vent < 1f && z != _ventSafe)
                        {
                            if (edge[i] != 0) c = A(vent < 0.4f ? E[1] : D[3], fade);
                            else if (_crack[i]) c = A(vent < 0.4f ? E[1] : D[3], fade);
                            else if (FireArt.Hash(x >> 2, y >> 2, puff) > 0.86f) c = A(S[1], fade * 0.8f);
                        }
                    }
                    _dangerPx[i] = c;
                }
            Upload(_dangerTex, _dangerPx);
        }

        void OnDestroy()
        {
            if (_seams != null && _seams.sprite != null) Destroy(_seams.sprite);
            if (_danger != null && _danger.sprite != null) Destroy(_danger.sprite);
            if (_seamTex != null) Destroy(_seamTex);
            if (_dangerTex != null) Destroy(_dangerTex);
        }
    }
}
