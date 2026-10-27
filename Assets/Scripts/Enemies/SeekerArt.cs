using UnityEngine;

namespace Convergence.Enemies
{
    /// <summary>
    /// Two enemy shots drawn here (2026-10-25). The Cinder Stinger's MISSILE: a violet glob - the colour of the Stinger's own
    /// tell, so it reads as the Stinger's - with three embers wheeling round it, at the creatures'
    /// texel density. Rounder and darker than the orange bolt, and pulsing, so the two shots never
    /// read as one. Four frames, cached; hashed, never UnityEngine.Random.
    /// </summary>
    public static class SeekerArt
    {
        static readonly Color[] Violet = TexelRig.Hex("#2e2452", "#5a45a3", "#8c73f2", "#d2c6ff");
        static readonly Color[] Ember = TexelRig.Hex("#4a2210", "#d0601e", "#ff8c26", "#ffe2b0");

        // Not readonly, null-guarded per element: a domain reload empties it (Domain reload traps).
        static Sprite[] _frames;

        public static Sprite[] Frames()
        {
            if (_frames != null && _frames.Length == 4 && _frames[0] != null) return _frames;
            float R = Core.Tuning.EnemyAi.MissileHitRadius * 0.85f * TexelRig.TexelsPerUnit;
            int n = Mathf.CeilToInt(R * 1.6f) * 2 + 2;
            float c = n / 2f;
            _frames = new Sprite[4];
            for (int k = 0; k < 4; k++)
            {
                var px = new Color[n * n];
                float swell = 1f + 0.08f * Mathf.Sin(k * Mathf.PI * 0.5f);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / (R * swell);
                    if (d > 1.12f) continue;
                    float lit = d - (dx < 0f && dy > 0f ? 0.18f : 0f);   // a highlight up and left
                    px[y * n + x] = d > 0.95f ? Violet[0]
                                  : lit < 0.3f ? Violet[3] : lit < 0.62f ? Violet[2] : Violet[1];
                }
                // Three embers wheeling round it, a third of a turn apart, stepping 30 degrees a frame.
                for (int i = 0; i < 3; i++)
                {
                    float a = (k * 30f + i * 120f) * Mathf.Deg2Rad;
                    int ex = Mathf.RoundToInt(c - 0.5f + Mathf.Cos(a) * R * 1.35f);
                    int ey = Mathf.RoundToInt(c - 0.5f + Mathf.Sin(a) * R * 1.35f);
                    Put(px, n, ex, ey, Ember[3]);
                    Put(px, n, ex + 1, ey, Ember[2]);
                    Put(px, n, ex, ey + 1, Ember[2]);
                    Put(px, n, ex + 1, ey + 1, Ember[1]);
                }
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave, name = $"enemy.ranged.seeker.{k}",
                };
                tex.SetPixels(px);
                tex.Apply();
                var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f),
                                      TexelRig.TexelsPerUnit, 0, SpriteMeshType.FullRect);
                s.name = tex.name;
                _frames[k] = s;
            }
            return _frames;
        }

        static readonly Color[] Stone = TexelRig.Hex("#26221f", "#4d4640", "#7a7068", "#a89c90");
        static Sprite[] _rubble;

        /// <summary>
        /// A Brute's thrown RUBBLE (Tuning.EnemyBands.BruteRubbleFromBand): a lumpy stone chunk in
        /// grey, tumbling over four frames - stone, so it never reads as one of the bright shots.
        /// </summary>
        public static Sprite[] Rubble()
        {
            if (_rubble != null && _rubble.Length == 4 && _rubble[0] != null) return _rubble;
            float R = Core.Tuning.EnemyBands.RubbleHitRadius * 0.9f * TexelRig.TexelsPerUnit;
            int n = Mathf.CeilToInt(R * 1.3f) * 2 + 2;
            float c = n / 2f;
            _rubble = new Sprite[4];
            for (int k = 0; k < 4; k++)
            {
                var px = new Color[n * n];
                float turn = k * Mathf.PI * 0.5f;
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float a = Mathf.Atan2(dy, dx) + turn;
                    // A lumpy outline: three lobes and a flat face, turning with the frame.
                    float edge = R * (0.85f + 0.12f * Mathf.Sin(a * 3f) + 0.06f * Mathf.Cos(a * 5f + 1f));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > edge) continue;
                    float lit = (dx * Mathf.Cos(turn) - dy * Mathf.Sin(turn)) / R;   // the lit side turns
                    px[y * n + x] = d > edge - 1f ? Stone[0] : lit < -0.35f ? Stone[3] : lit < 0.2f ? Stone[2] : Stone[1];
                }
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave, name = $"enemy.chaser.rubble.{k}",
                };
                tex.SetPixels(px);
                tex.Apply();
                var sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f),
                                       TexelRig.TexelsPerUnit, 0, SpriteMeshType.FullRect);
                sp.name = tex.name;
                _rubble[k] = sp;
            }
            return _rubble;
        }

        static void Put(Color[] px, int n, int x, int y, Color col)
        {
            if (x < 0 || y < 0 || x >= n || y >= n) return;
            px[y * n + x] = col;
        }
    }
}
