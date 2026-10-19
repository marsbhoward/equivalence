using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Concussion's burst (Tuning.Concussion): a flash where the arrow bursts off the string, then
    /// two pale shock fronts running out through the cone along the aim with dust kicked behind
    /// them, thinning as they spread, then fading. Drawing only - PlayerController.ConcussionStrike
    /// lands the hits and the push.
    ///
    /// Texel-drawn at body density into one unrotated canvas round the burst point, redrawn each
    /// frame (EnergyWave's way), over the bodies. Scaled time.
    /// </summary>
    public class ConcussionBurst : MonoBehaviour
    {
        const float Ppu = 37.5f;

        SpriteRenderer _sr;
        Texture2D _tex;
        Sprite _sprite;
        Color32[] _px;
        int _n;
        float _reach, _clock, _aimAngle;
        int _seed;

        public static ConcussionBurst Play(Vector2 at, Vector2 aim, float reach)
        {
            var go = new GameObject("concussion-burst");
            go.transform.position = new Vector3(Mathf.Round(at.x * Ppu) / Ppu, Mathf.Round(at.y * Ppu) / Ppu, 0f);
            var b = go.AddComponent<ConcussionBurst>();
            b._reach = reach;
            b._aimAngle = Mathf.Atan2(aim.y, aim.x);
            b._seed = Random.Range(0, 10000);
            b._n = Mathf.CeilToInt((reach + 0.3f) * Ppu) * 2 + 1;
            b._px = new Color32[b._n * b._n];
            b._tex = new Texture2D(b._n, b._n, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            b._sprite = Sprite.Create(b._tex, new Rect(0, 0, b._n, b._n), new Vector2(0.5f, 0.5f), Ppu);
            b._sr = go.AddComponent<SpriteRenderer>();
            b._sr.sprite = b._sprite;
            b._sr.sortingOrder = SortingOrders.Fx - 1;
            b.Draw();
            return b;
        }

        static float Ease(float u) => 1f - (1f - u) * (1f - u);

        void Update()
        {
            _clock += Time.deltaTime;
            if (_clock >= Tuning.Concussion.WaveSeconds + Tuning.Concussion.WaveFadeSeconds) { Destroy(gameObject); return; }
            Draw();
        }

        void Draw()
        {
            float u = Mathf.Clamp01(_clock / Tuning.Concussion.WaveSeconds);
            float fade = 1f - Mathf.Clamp01((_clock - Tuning.Concussion.WaveSeconds) / Tuning.Concussion.WaveFadeSeconds);
            float full = _reach * Ppu;
            float front = full * Ease(u);
            float second = full * Ease(Mathf.Clamp01(u * 0.8f - 0.1f));
            float band = Mathf.Lerp(4f, 2f, u);
            float half = (Tuning.Concussion.HalfAngle + 6f) * Mathf.Deg2Rad;
            float flash = 1f - Mathf.Clamp01(_clock / 0.08f);
            var core = Tuning.Concussion.Core;
            var dust = Tuning.Concussion.Dust;
            int c = _n / 2;
            int tick = Mathf.FloorToInt(_clock * 24f);
            System.Array.Clear(_px, 0, _px.Length);

            for (int y = 0; y < _n; y++)
                for (int x = 0; x < _n; x++)
                {
                    float dx = x - c, dy = y - c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color col; float a;

                    // The burst itself: a hard white disc for the first frames.
                    if (flash > 0f && d <= 5f * flash + 1f)
                    {
                        col = Color.white; a = 1f;
                    }
                    else
                    {
                        float off = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg,
                                                               _aimAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                        if (off > half) continue;
                        // The fronts sag back toward the cone's edges, so they read as a blast, not a ring.
                        float sag = (off / half) * (off / half) * 6f;
                        float into1 = front - sag - d, into2 = second - sag - d;
                        // Ragged: a hashed bite per angle.
                        int ha = Mathf.FloorToInt(off * 40f) * 92837111 ^ _seed;
                        float rag = ((ha >> 3) & 3) * 0.5f;
                        if (into1 >= 0f && into1 < band + rag)
                        {
                            col = into1 < 1.2f ? Color.white : core;
                            a = 0.95f;
                        }
                        else if (into2 >= 0f && into2 < band * 0.6f)
                        {
                            col = core; a = 0.55f;
                        }
                        else if (into1 >= 0f && into1 < band + 10f)
                        {
                            // Dust kicked up behind the front: broken specks, flickering.
                            int h = (x * 73856093) ^ (y * 19349663) ^ (_seed * 83492791) ^ tick;
                            if ((h & 7) != 0) continue;
                            col = dust; a = 0.7f * (1f - (into1 - band) / 10f);
                        }
                        else continue;
                        // Thinner the further out it spreads.
                        a *= Mathf.Lerp(1f, 0.55f, d / Mathf.Max(1f, full));
                    }
                    a *= fade;
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
