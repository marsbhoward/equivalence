using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A wave of energy running out across the ground from King and Queen's impact (the user's
    /// call, 2026-10-16): a ragged ring in the Secret Fire's three tones - the core at its leading
    /// edge, the vein behind it, embers trailing - expanding to <c>radius</c> over
    /// Tuning.KingAndQueen.WaveSeconds and thinning out as it goes. The blast lands on each body as
    /// this front reaches it (PlayerController.KingAndQueenStrike reads <see cref="Front"/>).
    ///
    /// Texel-drawn at body density into one unrotated canvas, redrawn each frame, on the ground
    /// under everything that stands (GroundCracks' way). The ring's edge wobbles by a hashed noise
    /// per angle, so it reads as energy rather than a drawn circle. Scaled time.
    /// </summary>
    public class EnergyWave : MonoBehaviour
    {
        const float Ppu = 37.5f;

        SpriteRenderer _sr;
        Texture2D _tex;
        Sprite _sprite;
        Color32[] _px;
        int _n;
        float _radius, _clock;
        Color _core, _vein, _ember;
        int _seed;

        /// <summary>How far out the front is now, world units.</summary>
        public float Front => _radius * Ease(Mathf.Clamp01(_clock / Tuning.KingAndQueen.WaveSeconds));

        public static EnergyWave Run(Vector2 at, float radius, Color core, Color vein, Color ember)
        {
            var go = new GameObject("energy-wave");
            go.transform.position = new Vector3(Mathf.Round(at.x * Ppu) / Ppu, Mathf.Round(at.y * Ppu) / Ppu, 0f);
            var w = go.AddComponent<EnergyWave>();
            w._radius = radius;
            w._core = core; w._vein = vein; w._ember = ember;
            w._seed = Random.Range(0, 10000);
            w._n = Mathf.CeilToInt((radius + 0.3f) * Ppu) * 2 + 1;
            w._px = new Color32[w._n * w._n];
            w._tex = new Texture2D(w._n, w._n, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            w._sprite = Sprite.Create(w._tex, new Rect(0, 0, w._n, w._n), new Vector2(0.5f, 0.5f), Ppu);
            w._sr = go.AddComponent<SpriteRenderer>();
            w._sr.sprite = w._sprite;
            w._sr.sortingOrder = SortingOrders.GroundDecal + 3;
            w.Draw();
            return w;
        }

        static float Ease(float u) => 1f - (1f - u) * (1f - u) * (1f - u);

        void Update()
        {
            _clock += Time.deltaTime;
            float total = Tuning.KingAndQueen.WaveSeconds + Tuning.KingAndQueen.WaveFadeSeconds;
            if (_clock >= total) { Destroy(gameObject); return; }
            Draw();
        }

        /// <summary>The edge's wobble at an angle, texels - hashed, smooth round the ring.</summary>
        float Wobble(float a)
        {
            float s = _seed * 0.37f;
            return Mathf.Sin(a * 7f + s) * 0.9f + Mathf.Sin(a * 13f + s * 1.7f) * 0.6f
                   + Mathf.Sin(a * 3f - s) * 0.5f;
        }

        void Draw()
        {
            float u = Mathf.Clamp01(_clock / Tuning.KingAndQueen.WaveSeconds);
            float frontT = Front * Ppu;   // texels
            // Thicker as it leaves, thinner as it spreads; fades once it has reached the edge.
            float band = Mathf.Lerp(8f, 4f, u);
            float trail = Mathf.Lerp(6f, 14f, u);
            float fade = 1f - Mathf.Clamp01((_clock - Tuning.KingAndQueen.WaveSeconds) / Tuning.KingAndQueen.WaveFadeSeconds);
            float alpha = Mathf.Lerp(1f, 0.6f, u) * fade;
            int c = _n / 2;
            System.Array.Clear(_px, 0, _px.Length);
            for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > frontT + 3f || d < frontT - band - trail - 1f) continue;
                    float edge = frontT + Wobble(Mathf.Atan2(dy, dx));
                    float into = edge - d;   // texels behind the front
                    if (into < 0f) continue;
                    Color col; float a;
                    if (into < 1.2f) { col = Color.Lerp(_core, Color.white, 0.4f); a = 1f; }
                    else if (into < band * 0.6f) { col = _core; a = 0.95f; }
                    else if (into < band) { col = _vein; a = 0.85f; }
                    else if (into < band + trail)
                    {
                        // Embers: broken, hashed, thinning toward the back.
                        int h = (x * 73856093) ^ (y * 19349663) ^ (_seed * 83492791) ^ Mathf.FloorToInt(_clock * 20f);
                        if ((h & 3) != 0) continue;
                        col = _ember;
                        a = 0.7f * (1f - (into - band) / trail);
                    }
                    else continue;
                    a *= alpha;
                    _px[y * _n + x] = new Color32((byte)(col.r * 255f), (byte)(col.g * 255f), (byte)(col.b * 255f),
                                                  (byte)(Mathf.Clamp01(a) * 255f));
                }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
