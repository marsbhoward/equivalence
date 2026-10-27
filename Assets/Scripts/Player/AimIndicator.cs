using UnityEngine;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// The ground an aimed weapon art will strike, along the facing, world units. Rounded: a
    /// capsule (the swing, a thrown weapon's strip); otherwise a quad whose width runs from
    /// <see cref="NearWidth"/> at the body to <see cref="FarWidth"/> at the reach (the crescent
    /// growing as it flies, Quintessence's cone narrowing to a point).
    /// </summary>
    public readonly struct ArtFootprint
    {
        public readonly float Reach, NearWidth, FarWidth;
        public readonly bool Rounded;

        public ArtFootprint(float reach, float nearWidth, float farWidth, bool rounded)
        {
            Reach = reach; NearWidth = nearWidth; FarWidth = farWidth; Rounded = rounded;
        }
    }

    /// <summary>
    /// Where a hand-aimed weapon art will go, shown only while the art is HELD to aim
    /// (PlayerController.ArtAimShown) - never through the timing bar, which has enough to read.
    /// Option H of the mock-ups (the user's pick, 2026-10-14,
    /// https://claude.ai/artifact/RdroBw4LPdLfzFeYGBgEuC): a stream of CHEVRONS marching out
    /// along the aim leads, a big chevron at the reach; under it the art's real FOOTPRINT
    /// (PlayerController.TryArtFootprint) as a faint outline over a checker dither. Tinted by the
    /// element. Absent for an art that hits all round.
    ///
    /// On the ground, under every body, at the body's texel size and on its grid, so it reads as
    /// part of the pixel world rather than a UI line laid over it - the same rule the target rim
    /// and the strike bar follow. Drawn texel by texel into ONE point-filtered texture centred on
    /// the player (snapped to the grid), redrawn only when the picture changes: the aim turning,
    /// or the chevrons stepping a texel.
    /// </summary>
    public class AimIndicator : MonoBehaviour
    {
        [SerializeField] PlayerController _player;
        [SerializeField] SpriteRenderer _renderer;

        // Rebuilt when the reach outgrows it - nothing here needs to survive a reload.
        Texture2D _tex;
        Sprite _sprite;
        Color32[] _px;
        float[] _chev;      // chevron alpha per texel this frame; 0 = none
        bool[] _chevLit;
        int _n;             // texture side, texels (even: the centre is a texel corner)
        RectInt _dirty;     // what the last picture touched, cleared before the next
        int _lastKey = int.MinValue;

        /// <summary>One body texel, world units - the target rim's own thickness.</summary>
        static float Texel => Tuning.Highlight.Thickness;

        // All in TEXELS, in the mock's proportions (the user's call, 2026-10-14). A rounded
        // footprint is DRAWN slimmer than the hit it stands for: its half-width is
        // SlimPerReach of the reach (at least SlimMin), never wider than the real one - at its true
        // width a swing's capsule was nearly as wide as it was long and read as a blob, not a
        // direction. The chevrons FILL it: each arm ArmFill of the half-width where it stands
        // (within ArmMin..ArmMax), the end chevron the whole of it (up to EndMax); spacing
        // follows the arm. March speed, where the stream starts, and the footprint's edge and
        // dither strengths are as mocked.
        //
        // DrawScale enlarges the whole picture against the art it stands for - length, width and
        // chevrons alike - so it holds its own beside the character (the user's call): the end
        // chevron lands PAST the art's real reach by that much. Whether a press lands is the
        // target rim's job (TargetHighlight), not this.
        const float DrawScale = 1.35f;
        const float SlimPerReach = 0.11f, SlimMin = 5f * DrawScale;
        const float ArmFill = 0.6f, ArmMin = 2f, ArmMax = 8f * DrawScale,
            EndMin = 3f, EndMax = 10f * DrawScale;
        const float ChevSpeed = 14f, ChevStart = 10f * DrawScale;
        const float EdgeAlpha = 0.4f, FillAlpha = 0.14f, OutlineAlpha = 0.7f;
        static readonly Color32 Outline = new(12, 10, 16, 255);

        public static AimIndicator Attach(PlayerController player)
        {
            var go = new GameObject("aim-indicator");
            go.transform.SetParent(player.transform, false);
            var ind = go.AddComponent<AimIndicator>();
            ind._player = player;
            ind._renderer = go.AddComponent<SpriteRenderer>();
            ind._renderer.sortingOrder = SortingOrders.GroundFx;   // over the pits, under danger lines and every body
            ind._renderer.enabled = false;
            return ind;
        }

        void LateUpdate()
        {
            if (_player == null) return;
            if (_renderer == null)
            {
                // An indicator from before this version (or a reload that lost the reference).
                // The old dots were child renderers; switch off any left lying.
                foreach (var old in GetComponentsInChildren<SpriteRenderer>()) old.enabled = false;
                _renderer = GetComponent<SpriteRenderer>();
                if (_renderer == null) _renderer = gameObject.AddComponent<SpriteRenderer>();
                _renderer.sortingOrder = SortingOrders.GroundFx;
            }
            if (!_player.ArtAimShown || !_player.TryArtFootprint(out var fp))
            {
                _renderer.enabled = false;
                _lastKey = int.MinValue;
                return;
            }

            float t = Texel;
            // The texture's centre sits on a texel corner, the player's position snapped to the grid,
            // so the picture never shimmers as the body moves.
            Vector3 p = _player.transform.position;
            transform.position = new Vector3(Mathf.Round(p.x / t) * t, Mathf.Round(p.y / t) * t, p.z);

            Vector2 dir = _player.Facing.sqrMagnitude > 1e-4f ? _player.Facing.normalized : Vector2.right;
            Shape(fp, out float sReach, out float sNear, out float sFar);
            int spacing = Spacing(sReach, sNear, sFar, fp.Rounded);
            int phase = Mathf.FloorToInt(Time.time * ChevSpeed) % spacing;
            int key = (Mathf.RoundToInt(Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg * 4f) * 31 + phase) * 31
                      + Mathf.RoundToInt(sReach) * 7 + Mathf.RoundToInt(sNear * 3f + sFar);
            _renderer.enabled = true;
            if (key == _lastKey && _sprite != null) return;
            _lastKey = key;

            Ensure(Mathf.CeilToInt(sReach + Mathf.Max(sNear, sFar) * 2f) + 4);
            Draw(fp.Rounded, sReach, sNear, sFar, dir, phase, spacing);
        }

        /// <summary>The footprint as DRAWN, in texels: reach and half-widths, a rounded one slimmed.</summary>
        static void Shape(ArtFootprint fp, out float reach, out float nearHalf, out float farHalf)
        {
            float t = Texel;
            reach = fp.Reach / t * DrawScale;
            nearHalf = fp.NearWidth / t * 0.5f * DrawScale;
            farHalf = fp.FarWidth / t * 0.5f * DrawScale;
            if (!fp.Rounded) return;
            nearHalf = farHalf = Mathf.Min(nearHalf, Mathf.Max(SlimMin, reach * SlimPerReach));
        }

        /// <summary>The footprint's half-width at <paramref name="u"/> texels along it.</summary>
        static float HalfAt(bool rounded, float reach, float nearHalf, float farHalf, float u) =>
            rounded ? nearHalf : Mathf.Lerp(nearHalf, farHalf, Mathf.Clamp01(u / reach));

        static float ArmAt(bool rounded, float reach, float nearHalf, float farHalf, float u) =>
            Mathf.Round(Mathf.Clamp(HalfAt(rounded, reach, nearHalf, farHalf, u) * ArmFill, ArmMin, ArmMax));

        static int Spacing(float reach, float nearHalf, float farHalf, bool rounded) =>
            Mathf.Clamp(Mathf.RoundToInt(ArmAt(rounded, reach, nearHalf, farHalf, reach * 0.5f) * 2.6f), 6, Mathf.RoundToInt(20f * DrawScale));

        void Ensure(int half)
        {
            int n = half * 2;
            if (_tex != null && _n >= n) return;
            if (_tex != null) Destroy(_tex);
            if (_sprite != null) Destroy(_sprite);
            _n = n;
            _tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _px = new Color32[n * n];
            _chev = new float[n * n];
            _chevLit = new bool[n * n];
            _tex.SetPixels32(_px);
            _tex.Apply();
            _sprite = Sprite.Create(_tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 1f / Texel);
            _renderer.sprite = _sprite;
            _dirty = new RectInt(0, 0, 0, 0);
        }

        void Draw(bool rounded, float reach, float nearHalf, float farHalf, Vector2 d, int phase, int spacing)
        {
            Vector2 n = new(-d.y, d.x);
            float c = _n * 0.5f;   // the player, in texture texels

            var before = _dirty;

            // Clear what the last picture drew.
            for (int y = _dirty.yMin; y < _dirty.yMax; y++)
                for (int x = _dirty.xMin; x < _dirty.xMax; x++)
                {
                    int i = y * _n + x;
                    _px[i] = default; _chev[i] = 0f; _chevLit[i] = false;
                }

            // This picture's bounds: the footprint's four corners, plus room for outlines.
            float wide = Mathf.Max(nearHalf, farHalf) + 2f;
            Vector2 a0 = n * wide, a1 = d * (reach + 2f);
            float minX = Mathf.Min(Mathf.Min(-a0.x, a0.x), Mathf.Min(a1.x - a0.x, a1.x + a0.x)) - 2f;
            float maxX = Mathf.Max(Mathf.Max(-a0.x, a0.x), Mathf.Max(a1.x - a0.x, a1.x + a0.x)) + 2f;
            float minY = Mathf.Min(Mathf.Min(-a0.y, a0.y), Mathf.Min(a1.y - a0.y, a1.y + a0.y)) - 2f;
            float maxY = Mathf.Max(Mathf.Max(-a0.y, a0.y), Mathf.Max(a1.y - a0.y, a1.y + a0.y)) + 2f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(c + minX), 0, _n), x1 = Mathf.Clamp(Mathf.CeilToInt(c + maxX), 0, _n);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(c + minY), 0, _n), y1 = Mathf.Clamp(Mathf.CeilToInt(c + maxY), 0, _n);
            _dirty = new RectInt(x0, y0, x1 - x0, y1 - y0);

            Color tint = _player.Resource != null ? ElementInfo.Tint(_player.Resource.Element) : Tuning.Highlight.Tint;
            Color32 col = tint, lit = Color.Lerp(tint, Color.white, 0.45f);

            // Under: the footprint - its edge, and a checker dither inside, fading in off the body.
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    float m = Inside(rounded, reach, nearHalf, farHalf, d, n, x + 0.5f - c, y + 0.5f - c);
                    if (m < 0f) continue;
                    float fade = Mathf.Clamp01((m - 4f) / 8f);
                    bool edge = Inside(rounded, reach, nearHalf, farHalf, d, n, x + 1.5f - c, y + 0.5f - c) < 0f
                             || Inside(rounded, reach, nearHalf, farHalf, d, n, x - 0.5f - c, y + 0.5f - c) < 0f
                             || Inside(rounded, reach, nearHalf, farHalf, d, n, x + 0.5f - c, y + 1.5f - c) < 0f
                             || Inside(rounded, reach, nearHalf, farHalf, d, n, x + 0.5f - c, y - 0.5f - c) < 0f;
                    if (edge) Over(y * _n + x, col, EdgeAlpha * fade + 0.05f);
                    else if (((x + y) & 1) == 0) Over(y * _n + x, col, FillAlpha * fade);
                }

            // Over: the chevrons, marching out, brighter toward the reach, the big one at the end.
            // The end chevron spans the footprint where it ends: a capsule's whole width (its arms
            // land on the end cap's rim), a taper's width at the reach.
            float endArm = Mathf.Round(Mathf.Clamp(HalfAt(rounded, reach, nearHalf, farHalf, reach), EndMin, EndMax));
            for (float s = ChevStart + phase; s < reach - endArm - 2f; s += spacing)
            {
                float alpha = s < ChevStart + 8f ? 0.8f * (s - ChevStart) / 8f : 0.5f + 0.35f * s / reach;
                Chevron(s, ArmAt(rounded, reach, nearHalf, farHalf, s), alpha, false, d, n, c);
            }
            Chevron(reach, endArm, 1f, true, d, n, c);
            Chevron(reach - 1f, endArm, 1f, false, d, n, c);

            // Their dark outline, then the chevrons themselves.
            for (int y = Mathf.Max(1, y0); y < Mathf.Min(_n - 1, y1); y++)
                for (int x = Mathf.Max(1, x0); x < Mathf.Min(_n - 1, x1); x++)
                {
                    int i = y * _n + x;
                    if (_chev[i] > 0f) continue;
                    float near = Mathf.Max(Mathf.Max(_chev[i - 1], _chev[i + 1]), Mathf.Max(_chev[i - _n], _chev[i + _n]));
                    if (near > 0f) Over(i, Outline, Mathf.Min(OutlineAlpha, near));
                }
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = y * _n + x;
                    if (_chev[i] > 0f) Over(i, _chevLit[i] ? lit : col, _chev[i]);
                }

            // Upload only the rows and columns either picture touched.
            var raw = _tex.GetPixelData<Color32>(0);
            int ux0 = Mathf.Min(before.xMin, x0), ux1 = Mathf.Max(before.xMax, x1);
            int uy0 = Mathf.Min(before.yMin, y0), uy1 = Mathf.Max(before.yMax, y1);
            if (before.width == 0 || before.height == 0) { ux0 = x0; ux1 = x1; uy0 = y0; uy1 = y1; }
            for (int y = uy0; y < uy1; y++)
                for (int x = ux0; x < ux1; x++) raw[y * _n + x] = _px[y * _n + x];
            _tex.Apply(false);
        }

        /// <summary>The footprint test at a texel offset from the player: how far along it the
        /// point is (for the fade), or -1 outside.</summary>
        static float Inside(bool rounded, float reach, float nearHalf, float farHalf, Vector2 d, Vector2 n, float rx, float ry)
        {
            float u = rx * d.x + ry * d.y, v = rx * n.x + ry * n.y;
            if (rounded)
            {
                // The swing's capsule: from the body to the reach INCLUDING its end caps.
                float h = nearHalf;
                if (u < h) return (u - h) * (u - h) + v * v <= h * h ? Mathf.Max(0f, u) : -1f;
                if (u > reach - h) return (u - (reach - h)) * (u - (reach - h)) + v * v <= h * h ? u : -1f;
                return Mathf.Abs(v) <= h ? u : -1f;
            }
            if (u < 0f || u > reach) return -1f;
            return Mathf.Abs(v) <= Mathf.Lerp(nearHalf, farHalf, u / reach) ? u : -1f;
        }

        void Chevron(float s, float arm, float alpha, bool lit, Vector2 d, Vector2 n, float c)
        {
            if (alpha <= 0f) return;
            for (float k = 0f; k <= arm; k += 0.5f)
                for (int g = -1; g <= 1; g += 2)
                {
                    int x = Mathf.RoundToInt(c + d.x * (s - k) + n.x * g * k - 0.5f);
                    int y = Mathf.RoundToInt(c + d.y * (s - k) + n.y * g * k - 0.5f);
                    if (x < 0 || y < 0 || x >= _n || y >= _n) continue;
                    int i = y * _n + x;
                    if (alpha >= _chev[i]) { _chev[i] = alpha; _chevLit[i] = lit; }
                }
        }

        /// <summary>Straight-alpha "over" into the buffer.</summary>
        void Over(int i, Color32 src, float a)
        {
            if (a <= 0f) return;
            var dst = _px[i];
            float da = dst.a / 255f, oa = a + da * (1f - a);
            if (oa <= 0f) return;
            float k = da * (1f - a);
            _px[i] = new Color32(
                (byte)((src.r * a + dst.r * k) / oa),
                (byte)((src.g * a + dst.g * k) / oa),
                (byte)((src.b * a + dst.b * k) / oa),
                (byte)(oa * 255f));
        }

        void OnDestroy()
        {
            if (_sprite != null) Destroy(_sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
