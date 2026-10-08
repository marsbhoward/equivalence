using System.Collections.Generic;
using Convergence.Core;
using UnityEngine;
using T = Convergence.Core.Tuning.MagnumOpus;

namespace Convergence.Combat
{
    /// <summary>
    /// A body taken apart texel by texel: the death a non-boss enemy dies when a Magnum Opus hit
    /// (<see cref="DamageInfo.Disintegrates"/>) kills it while a reactive weapon is drawn - shared
    /// by all three (the Aether Greatsword, the Aether Longbow, the Aether Dual Discs).
    ///
    /// THE TEXELS ARE THE BODY'S OWN. Every sprite under the dying body's visual is read back and
    /// each opaque texel becomes one particle at its exact world position and colour, so a chaser
    /// comes apart as a chaser and a turret as a turret, for free (Bisection's rule). The side the
    /// shot struck goes first and the rest follows across the body
    /// (<see cref="Tuning.MagnumOpus.DisintegrateSweepSeconds"/>); each texel flares in the element's
    /// core as it lets go, then drifts off along the shot and upward, cooling through the element's
    /// tint to its ember and fading.
    ///
    /// STILL ON THE GRID. The particles are plotted every frame into one point-filtered texture at
    /// the body's own texel size, never drawn as free-floating quads - pixel art that dissolves
    /// into sub-texel specks stops being pixel art. Self-contained: captures everything it needs
    /// from the body at once, animates itself and destroys itself, never touching the enemy, which
    /// HookDeath is about to destroy anyway.
    /// </summary>
    public class Disintegration : MonoBehaviour
    {
        const int MaxTexels = 700;

        struct Mote
        {
            public Vector2 From, Drift;
            public Color Was;
            public float Delay, Life;
        }

        Mote[] _motes;
        Color32[] _px;
        Texture2D _tex;
        Vector2 _origin;
        float _cell, _t;
        int _w, _h;
        Color _core, _vein, _ember;

        public static Disintegration At(Transform body, DamageInfo killedBy)
        {
            if (body == null) return null;
            var vt = body.Find("visual");
            var sources = (vt != null ? vt : body).GetComponentsInChildren<SpriteRenderer>();

            var motes = new List<Mote>();
            float cell = float.MaxValue;
            int order = SortingOrders.Enemy;
            foreach (var sr in sources)
            {
                if (sr == null || !sr.enabled || sr.forceRenderingOff || sr.sprite == null) continue;
                var tex = sr.sprite.texture;
                if (tex == null || !tex.isReadable) continue;
                cell = Mathf.Min(cell, Mathf.Abs(sr.transform.lossyScale.x) / sr.sprite.pixelsPerUnit);
                order = Mathf.Max(order, sr.sortingOrder);
            }
            if (cell == float.MaxValue) return null;
            cell = Mathf.Max(cell, 1f / 80f);

            var dir = killedBy.Knockback.sqrMagnitude > 0.0001f ? killedBy.Knockback.normalized : Vector2.up;
            var seen = new HashSet<Vector2Int>();
            foreach (var sr in sources)
            {
                if (sr == null || !sr.enabled || sr.forceRenderingOff || sr.sprite == null) continue;
                var s = sr.sprite;
                var tex = s.texture;
                if (tex == null || !tex.isReadable) continue;
                var r = s.textureRect;
                int w = (int)r.width, h = (int)r.height;
                var px = tex.GetPixels((int)r.x, (int)r.y, w, h);
                var pivot = s.pivot;
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = px[y * w + x] * sr.color;
                    if (c.a < 0.5f) continue;
                    float lx = (x + 0.5f - pivot.x) / s.pixelsPerUnit, ly = (y + 0.5f - pivot.y) / s.pixelsPerUnit;
                    if (sr.flipX) lx = -lx;
                    if (sr.flipY) ly = -ly;
                    Vector2 world = sr.transform.TransformPoint(new Vector3(lx, ly, 0f));
                    // One mote per body texel: a finer source collapses onto the coarsest grid.
                    if (!seen.Add(new Vector2Int(Mathf.FloorToInt(world.x / cell), Mathf.FloorToInt(world.y / cell)))) continue;
                    c.a = 1f;
                    motes.Add(new Mote { From = world, Was = c });
                }
            }
            if (motes.Count == 0) return null;

            // A big body thins out rather than costing more - every other mote still reads as the body.
            while (motes.Count > MaxTexels)
                for (int i = motes.Count - 1; i >= 0 && motes.Count > MaxTexels; i -= 2) motes.RemoveAt(i);

            // The side the shot reached first lets go first.
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var m in motes) { float p = Vector2.Dot(m.From, dir); lo = Mathf.Min(lo, p); hi = Mathf.Max(hi, p); }
            var up = Vector2.up;
            float life = T.DisintegrateSeconds - T.DisintegrateSweepSeconds;
            for (int i = 0; i < motes.Count; i++)
            {
                var m = motes[i];
                float along = hi > lo ? (Vector2.Dot(m.From, dir) - lo) / (hi - lo) : 0f;
                m.Delay = along * T.DisintegrateSweepSeconds + Random.Range(0f, 0.05f);
                m.Life = life * Random.Range(0.7f, 1f);
                var spread = Random.insideUnitCircle * 0.45f;
                m.Drift = (dir * 0.75f + up * 0.55f + spread) * (T.DisintegrateDrift * Random.Range(0.5f, 1f));
                motes[i] = m;
            }

            // The canvas: the body's bounds, grown by as far as anything can drift.
            Vector2 min = motes[0].From, max = min;
            foreach (var m in motes) { min = Vector2.Min(min, m.From); max = Vector2.Max(max, m.From); }
            float pad = T.DisintegrateDrift * 1.6f + cell * 2f;
            min -= Vector2.one * pad;
            max += Vector2.one * pad;
            min = new Vector2(Mathf.Floor(min.x / cell) * cell, Mathf.Floor(min.y / cell) * cell);

            var go = new GameObject("disintegration");
            go.transform.position = new Vector3(min.x, min.y, body.position.z);
            var e = go.AddComponent<Disintegration>();
            e._motes = motes.ToArray();
            e._cell = cell;
            e._origin = min;
            e._w = Mathf.Min(1024, Mathf.CeilToInt((max.x - min.x) / cell) + 1);
            e._h = Mathf.Min(1024, Mathf.CeilToInt((max.y - min.y) / cell) + 1);
            e._px = new Color32[e._w * e._h];
            e._tex = new Texture2D(e._w, e._h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var element = killedBy.Element;
            e._core = Art.Gear.SecretFire.Tone(element, 0);
            e._vein = Art.Gear.SecretFire.Tone(element, 1);
            e._ember = Art.Gear.SecretFire.Tone(element, 2);

            var sprite = Sprite.Create(e._tex, new Rect(0, 0, e._w, e._h), Vector2.zero, 1f / cell);
            sprite.name = "disintegration";
            var out_ = go.AddComponent<SpriteRenderer>();
            out_.sprite = sprite;
            out_.sortingOrder = order;
            e.Plot();
            return e;
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= T.DisintegrateSeconds + 0.1f)
            {
                Destroy(gameObject);
                return;
            }
            Plot();
        }

        void Plot()
        {
            System.Array.Clear(_px, 0, _px.Length);
            foreach (var m in _motes)
            {
                float k = (_t - m.Delay) / m.Life;
                if (k >= 1f) continue;
                Vector2 at;
                Color c;
                if (k <= 0f)
                {
                    // Still part of the body - but the edge about to go burns first.
                    at = m.From;
                    c = Color.Lerp(m.Was, _core, Mathf.Clamp01(1f + k * m.Life / 0.08f));
                }
                else
                {
                    float ease = 1f - (1f - k) * (1f - k);
                    at = m.From + m.Drift * ease;
                    c = k < 0.12f ? Color.Lerp(_core, _vein, k / 0.12f)
                      : Color.Lerp(_vein, _ember, (k - 0.12f) / 0.6f);
                    c.a = 1f - Mathf.SmoothStep(0f, 1f, (k - 0.35f) / 0.65f);
                    if (c.a <= 0.02f) continue;
                }
                int x = Mathf.FloorToInt((at.x - _origin.x) / _cell), y = Mathf.FloorToInt((at.y - _origin.y) / _cell);
                if (x < 0 || y < 0 || x >= _w || y >= _h) continue;
                _px[y * _w + x] = c;
            }
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        void OnDestroy()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null) Destroy(sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
