using UnityEngine;

namespace Convergence.Art
{
    /// <summary>
    /// The wall sconce on the north face (Scenery.BuildSconces): an iron bracket and bowl, and a
    /// flame of four frames. Texel-drawn at the walls' density (FloorArt.PixelsPerUnit), made once
    /// and shared by every sconce; rebuilt lazily after a domain reload.
    /// </summary>
    public static class SconceArt
    {
        const int FlameW = 9, FlameH = 15;
        static Sprite _bracket;
        static Sprite[] _frames;

        static float Ppu => FloorArt.PixelsPerUnit;

        /// <summary>How far above the bracket's foot the flame stands, world units.</summary>
        public static float FlameRise => 6f / Ppu;

        public static Sprite Bracket { get { if (_bracket == null) Build(); return _bracket; } }
        public static Sprite[] Frames { get { if (_frames == null || _frames[0] == null) Build(); return _frames; } }

        static void Build()
        {
            // ---- the bracket: a wall plate, an arm, a bowl ----
            const int bw = 9, bh = 8;
            var iron = new Color32(44, 40, 46, 255);
            var ironHi = new Color32(86, 80, 88, 255);
            var ironLo = new Color32(24, 22, 26, 255);
            var b = new Color32[bw * bh];
            void B(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < bw && y < bh) b[y * bw + x] = c; }
            for (int y = 0; y < 4; y++) { B(3, y, iron); B(4, y, ironHi); B(5, y, ironLo); }   // the plate
            for (int x = 1; x < 8; x++) { B(x, 4, iron); B(x, 5, x < 4 ? ironHi : iron); }        // the bowl
            B(0, 5, ironLo); B(8, 5, ironLo); B(1, 6, iron); B(7, 6, iron);
            for (int x = 2; x < 7; x++) B(x, 6, new Color32(30, 18, 14, 255));                     // coals
            B(3, 6, new Color32(160, 60, 24, 255)); B(5, 6, new Color32(200, 90, 30, 255));
            _bracket = Make(b, bw, bh, "sconce");

            // ---- the flame: a teardrop that leans and breathes, four frames ----
            _frames = new Sprite[4];
            var core = new Color32(255, 246, 196, 255);
            var mid = new Color32(255, 178, 64, 255);
            var outer = new Color32(226, 92, 32, 255);
            for (int f = 0; f < 4; f++)
            {
                var px = new Color32[FlameW * FlameH];
                float lean = (f == 1 ? 0.6f : f == 3 ? -0.6f : 0f);
                float tall = f == 2 ? 1.1f : f == 0 ? 0.95f : 1f;
                for (int y = 0; y < FlameH; y++)
                for (int x = 0; x < FlameW; x++)
                {
                    float v = y / (FlameH * tall);
                    if (v > 1f) continue;
                    // width: full at the base third, tapering to a point; the tip leans
                    float half = (v < 0.3f ? 0.75f + v : 1.05f - (v - 0.3f) * 1.45f) * (FlameW * 0.5f);
                    float cx = (FlameW - 1) * 0.5f + lean * v * v * 2f;
                    float d = Mathf.Abs(x - cx);
                    if (d > half) continue;
                    float k = d / Mathf.Max(0.5f, half);
                    px[y * FlameW + x] = v > 0.7f || k > 0.7f ? outer : (v > 0.4f || k > 0.35f) ? mid : core;
                }
                _frames[f] = Make(px, FlameW, FlameH, $"flame{f}");
            }
        }

        static Sprite Make(Color32[] px, int w, int h, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = name };
            tex.SetPixels32(px);
            tex.Apply(false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), Ppu);
        }

        /// <summary>Steps the flame through its frames on unscaled time, each sconce out of step.</summary>
        public class Flicker : MonoBehaviour
        {
            SpriteRenderer _sr;
            float _offset;
            void Awake() { _sr = GetComponent<SpriteRenderer>(); _offset = Mathf.Abs(GetEntityId().GetHashCode() * 0.37f) % 1f; }
            void Update()
            {
                if (_sr == null) return;
                var frames = Frames;
                int i = Mathf.FloorToInt((Time.unscaledTime * Core.Tuning.Lighting.SconceFps + _offset * 4f)) % frames.Length;
                // a little irregular - every few beats one frame holds a beat longer
                if (Mathf.FloorToInt(Time.unscaledTime * 1.7f + _offset * 3f) % 5 == 0) i = 0;
                _sr.sprite = frames[i];
            }
        }
    }
}
