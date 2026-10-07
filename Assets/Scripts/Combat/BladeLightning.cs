using System.Collections.Generic;
using Convergence.Core;
using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// Electricity round a blade, in the colours its marks burn in - the Magnum Opus's sword as it
    /// gathers to strike (Art.Gear.MagnumOpusGlow drives it). Two kinds of bolt: CRAWLERS run along
    /// the blade from one point of its edge to another, bowed out off the steel; LEAPERS jump off
    /// the edge into the air. <see cref="Intensity"/> (0..1) is how wild it is - how often a bolt
    /// strikes, how far a leaper reaches, how rough and how forked they are, how many are leapers -
    /// climbing to an apex at the release, where <see cref="Discharge"/> throws one last ring of long
    /// bolts off the whole blade and the storm ends with the shot.
    ///
    /// Rai's arcs' method (LightningArcs): PIXEL ART rasterised into one canvas, never a
    /// LineRenderer, at the BODY's texel size so the bolts sit on the character's grid. The canvas
    /// is unparented and unrotated, centred on the blade; the bolts themselves are kept in the
    /// blade's LOCAL space, so they ride every move of the swing and are re-projected each frame.
    /// Drawn just over the blade.
    ///
    /// SCALED time, the art's own clock: a pause holds the storm with the move.
    /// </summary>
    public class BladeLightning : MonoBehaviour
    {
        /// <summary>How wild the storm is right now, 0 (none) to 1 (the apex). Set every frame by
        /// whatever drives it; at 0 no new bolt strikes and the last ones die out.</summary>
        public float Intensity;

        const float BodyPpu = 37.5f;
        const float Margin = 30f;     // cells of canvas past the blade's own bounds

        class Bolt
        {
            public List<Vector2> Trunk;
            public List<List<Vector2>> Forks;
            public float Age, Life, Width;
            public bool Restrike;
        }

        [SerializeField] SpriteRenderer _blade;
        SpriteRenderer _canvas;
        Texture2D _tex;
        Color32[] _px, _tones;
        byte[] _lvl;
        int _size;
        // NOT readonly and null-guarded: a domain reload hands these back empty or null (CLAUDE.md).
        List<Bolt> _bolts = new();
        float _next;
        ElementType _toneOf = (ElementType)(-1);

        /// <summary>The storm on <paramref name="blade"/>, made if it has none.</summary>
        public static BladeLightning On(SpriteRenderer blade)
        {
            if (blade == null) return null;
            var l = blade.GetComponent<BladeLightning>();
            if (l == null)
            {
                l = blade.gameObject.AddComponent<BladeLightning>();
                l._blade = blade;
            }
            return l;
        }

        // ---------------------------------------------------------------- the blade's frame

        /// <summary>The blade's run along the sprite, local units: from above the grip to the tip.</summary>
        bool Frame(out float y0, out float y1, out float half, out float cellLocal, out float cellWorld)
        {
            y0 = y1 = half = cellLocal = cellWorld = 0f;
            var s = _blade != null ? _blade.sprite : null;
            if (s == null) return false;
            var b = s.bounds;
            y0 = b.min.y + b.size.y * 0.24f;
            y1 = b.max.y - b.size.y * 0.02f;
            half = b.extents.x * 0.42f;
            // One canvas cell is one BODY texel, whatever density the blade is drawn at - the layer
            // sits at the figure's own scale, so a body texel is 1/37.5 of a local unit.
            cellLocal = 1f / BodyPpu;
            cellWorld = cellLocal * Mathf.Abs(_blade.transform.lossyScale.y);
            return cellWorld > 0f;
        }

        /// <summary>A point on the blade's edge: <paramref name="t"/> 0 grip end .. 1 tip,
        /// <paramref name="side"/> -1 or 1.</summary>
        static Vector2 Edge(float t, float side, float y0, float y1, float half)
        {
            // The blade narrows to its point over the last fifth.
            float taper = t > 0.8f ? Mathf.Lerp(1f, 0.15f, (t - 0.8f) / 0.2f) : 1f;
            return new Vector2(side * half * taper, Mathf.Lerp(y0, y1, t));
        }

        // ---------------------------------------------------------------- the storm

        void LateUpdate()
        {
            _bolts ??= new List<Bolt>();
            var src = _blade;
            bool shown = src != null && src.enabled && !src.forceRenderingOff && src.gameObject.activeInHierarchy;
            if (!shown || !Frame(out float y0, out float y1, out float half, out float cellLocal, out float cellWorld))
            {
                if (_canvas != null) _canvas.enabled = false;
                _bolts.Clear();
                return;
            }

            float dt = Time.deltaTime, I = Mathf.Clamp01(Intensity);
            if (I > 0.001f && Time.time >= _next)
            {
                // More at once as it climbs - one to four bolts a strike, spread down the blade.
                int n = 1 + Mathf.FloorToInt(I * 3.5f * Random.value);
                for (int k = 0; k < n; k++) _bolts.Add(Strike(I, y0, y1, half, cellLocal));
                // Faster as it climbs, and in bursts near the top.
                float gap = Mathf.Lerp(0.11f, 0.02f, I);
                _next = Time.time + (Random.value < 0.25f + I * 0.3f ? gap * 0.3f : gap * Random.Range(0.6f, 1.4f));
            }
            for (int i = _bolts.Count - 1; i >= 0; i--)
            {
                _bolts[i].Age += dt;
                if (_bolts[i].Age >= _bolts[i].Life) _bolts.RemoveAt(i);
            }
            if (_bolts.Count == 0)
            {
                if (_canvas != null) _canvas.enabled = false;
                return;
            }

            var bounds = src.bounds;
            int size = Mathf.CeilToInt(Mathf.Max(bounds.size.x, bounds.size.y) / cellWorld + Margin * 2f);
            size += size & 1;
            EnsureCanvas(size, cellWorld);
            Redraw(src.transform, bounds.center, cellWorld);

            var t = _canvas.transform;
            t.position = new Vector3(bounds.center.x, bounds.center.y, src.transform.position.z - 0.003f);
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.one;
            _canvas.sharedMaterial = src.sharedMaterial;
            _canvas.sortingLayerID = src.sortingLayerID;
            _canvas.sortingOrder = src.sortingOrder;
            _canvas.enabled = true;
        }

        /// <summary>The release: long bolts off the whole length of the blade at once.</summary>
        public void Discharge()
        {
            if (!Frame(out float y0, out float y1, out float half, out float cellLocal, out _)) return;
            for (int i = 0; i < 9; i++)
            {
                var b = Leap(1f, Random.Range(0.2f, 1f), Random.value < 0.5f ? -1f : 1f, y0, y1, half, cellLocal, 1.4f);
                b.Life = Random.Range(0.14f, 0.24f);
                b.Restrike = false;
                _bolts.Add(b);
            }
            Intensity = 0f;
        }

        Bolt Strike(float I, float y0, float y1, float half, float cellLocal)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float t = Random.Range(0.05f, 1f);
            if (Random.value < Mathf.Lerp(0.2f, 0.65f, I))
                return Leap(I, t, side, y0, y1, half, cellLocal, 1f);

            // A crawler: along the edge to another point on it, bowed out off the steel. Now and
            // then it crosses to the other edge round the blade.
            float t2 = Mathf.Clamp01(t + (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.12f, 0.2f + 0.3f * I));
            float side2 = Random.value < 0.25f ? -side : side;
            var a = Edge(t, side, y0, y1, half);
            var c = Edge(t2, side2, y0, y1, half);
            var bow = new Vector2(side * half * Random.Range(0.6f, 1.6f + I), 0f);
            var trunk = Jag(a, (a + c) * 0.5f + bow, Rough(I), cellLocal);
            var rest = Jag((a + c) * 0.5f + bow, c, Rough(I), cellLocal);
            rest.RemoveAt(0);
            trunk.AddRange(rest);
            return new Bolt
            {
                Trunk = trunk, Forks = Forks(trunk, I, cellLocal, 0.5f),
                Life = Random.Range(0.06f, 0.15f) * (1f + I * 0.6f), Width = Mathf.Lerp(0.55f, 0.85f, I),
                Restrike = Random.value < 0.4f,
            };
        }

        Bolt Leap(float I, float t, float side, float y0, float y1, float half, float cellLocal, float reachMul)
        {
            var root = Edge(t, side, y0, y1, half);
            // Mostly out across the blade, leaning up or down it; at the apex, anywhere.
            float spread = Mathf.Lerp(0.6f, 1.2f, I);
            float a = (side > 0f ? 0f : Mathf.PI) + Random.Range(-spread, spread);
            float cells = Mathf.Lerp(5f, 20f, I) * Random.Range(0.55f, 1f) * reachMul;
            var tip = root + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * cells * cellLocal;
            var trunk = Jag(root, tip, Rough(I), cellLocal);
            return new Bolt
            {
                Trunk = trunk, Forks = Forks(trunk, I, cellLocal, 0.45f),
                Life = Random.Range(0.07f, 0.18f) * (1f + I * 0.6f), Width = Mathf.Lerp(0.6f, 1.05f, I),
                Restrike = Random.value < 0.5f,
            };
        }

        static float Rough(float I) => Mathf.Lerp(0.22f, 0.42f, I);

        static List<List<Vector2>> Forks(List<Vector2> trunk, float I, float cellLocal, float lengthShare)
        {
            var forks = new List<List<Vector2>>();
            int n = Random.Range(0, 1 + Mathf.RoundToInt(3f * I));
            for (int f = 0; f < n && trunk.Count > 4; f++)
            {
                int i = Random.Range(trunk.Count / 5, trunk.Count * 4 / 5);
                var along = (trunk[i + 1] - trunk[i - 1]).normalized;
                float turn = (Random.value < 0.5f ? -1f : 1f) * Random.Range(0.35f, 0.9f);
                var dir = new Vector2(along.x * Mathf.Cos(turn) - along.y * Mathf.Sin(turn),
                                      along.x * Mathf.Sin(turn) + along.y * Mathf.Cos(turn));
                float left = Vector2.Distance(trunk[i], trunk[trunk.Count - 1]);
                forks.Add(Jag(trunk[i], trunk[i] + dir * left * Random.Range(0.3f, 1f) * lengthShare, Rough(I), cellLocal));
            }
            return forks;
        }

        /// <summary>Midpoint displacement, down to segments of about three canvas cells.</summary>
        static List<Vector2> Jag(Vector2 a, Vector2 b, float rough, float cellLocal)
        {
            var pts = new List<Vector2> { a, b };
            float min = 3f * cellLocal;
            for (int pass = 0; pass < 8; pass++)
            {
                bool split = false;
                var next = new List<Vector2>(pts.Count * 2) { pts[0] };
                for (int i = 1; i < pts.Count; i++)
                {
                    var p = pts[i - 1];
                    var q = pts[i];
                    float len = Vector2.Distance(p, q);
                    if (len > min)
                    {
                        var n = new Vector2(-(q.y - p.y), q.x - p.x) / len;
                        next.Add((p + q) * 0.5f + n * Random.Range(-rough, rough) * len);
                        split = true;
                    }
                    next.Add(q);
                }
                pts = next;
                if (!split) break;
            }
            return pts;
        }

        // ---------------------------------------------------------------- the picture

        void EnsureCanvas(int size, float cellWorld)
        {
            var element = Art.Gear.SecretFire.Shown;
            if (element != _toneOf || _tones == null)
            {
                _toneOf = element;
                Color32 T(int tone, byte a) { Color32 c = Art.Gear.SecretFire.Tone(element, tone); c.a = a; return c; }
                Color32 sear = Color.Lerp(Art.Gear.SecretFire.Tone(element, 0), Color.white, 0.75f);
                _tones = new[] { new Color32(0, 0, 0, 0), T(2, 205), T(1, 240), T(0, 255), sear };
            }

            if (_canvas != null && _tex != null && _size == size && _px != null && _lvl != null
                && Mathf.Approximately(_canvas.sprite.pixelsPerUnit, 1f / cellWorld)) return;
            _size = size;
            if (_tex != null) Destroy(_tex);
            _tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            _px = new Color32[size * size];
            _lvl = new byte[size * size];
            if (_canvas == null)
            {
                var go = new GameObject("blade-lightning.canvas");
                _canvas = go.AddComponent<SpriteRenderer>();
            }
            // FullRect: a Tight mesh is cut to the alpha at creation, and the canvas starts empty.
            _canvas.sprite = Sprite.Create(_tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                           1f / cellWorld, 0, SpriteMeshType.FullRect);
        }

        void Redraw(Transform blade, Vector3 centre, float cellWorld)
        {
            System.Array.Clear(_lvl, 0, _lvl.Length);
            foreach (var b in _bolts)
            {
                // Bright, dim, then fading; a re-strike flashes twice down the same channel.
                float k = b.Age / b.Life;
                int stage = b.Restrike ? (k < 0.28f ? 3 : k < 0.42f ? 2 : k < 0.68f ? 3 : 1)
                                       : (k < 0.55f ? 3 : k < 0.75f ? 2 : 1);
                Stamp(Project(b.Trunk, blade, centre, cellWorld), b.Width, b.Width * 0.4f, 3 - stage);
                foreach (var f in b.Forks)
                    Stamp(Project(f, blade, centre, cellWorld), b.Width * 0.6f, b.Width * 0.3f, 3 - stage);
            }
            for (int i = 0; i < _lvl.Length; i++) _px[i] = _tones[_lvl[i]];
            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        /// <summary>Blade-local points to canvas cells, through the blade's transform as it is NOW.</summary>
        List<Vector2> Project(List<Vector2> local, Transform blade, Vector3 centre, float cellWorld)
        {
            var o = new List<Vector2>(local.Count);
            foreach (var p in local)
            {
                var w = blade.TransformPoint(p);
                o.Add(new Vector2((w.x - centre.x) / cellWorld, (w.y - centre.y) / cellWorld));
            }
            return o;
        }

        /// <summary>One path into the level buffer: a one-cell searing core, then core, tint and
        /// ember bands tapering from <paramref name="rootW"/> to <paramref name="tipW"/>; a dim or
        /// fading stage steps every tone down by <paramref name="shift"/>. Rai's stamp.</summary>
        void Stamp(List<Vector2> path, float rootW, float tipW, int shift)
        {
            int half = _size / 2;
            var cells = new List<Vector2Int>(path.Count * 4);
            for (int i = 1; i < path.Count; i++)
                Line(Mathf.FloorToInt(path[i - 1].x) + half, Mathf.FloorToInt(path[i - 1].y) + half,
                     Mathf.FloorToInt(path[i].x) + half, Mathf.FloorToInt(path[i].y) + half, cells, i == 1);
            int total = cells.Count;
            for (int k = 0; k < total; k++)
            {
                var p = cells[k];
                float w = Mathf.Lerp(rootW, tipW, k / (float)Mathf.Max(1, total - 1));
                float core = Mathf.Max(0.5f, 1.1f * w), tint = core + w, ember = tint + 0.7f * w;
                int box = Mathf.CeilToInt(ember);
                for (int oy = -box; oy <= box; oy++)
                for (int ox = -box; ox <= box; ox++)
                {
                    int x = p.x + ox, y = p.y + oy;
                    if (x < 0 || y < 0 || x >= _size || y >= _size) continue;
                    float d = Mathf.Sqrt(ox * ox + oy * oy);
                    int level = (d < 0.5f ? 4 : d <= core ? 3 : d <= tint ? 2 : d <= ember ? 1 : 0) - shift;
                    if (level <= 0) continue;
                    int idx = y * _size + x;
                    if (_lvl[idx] < level) _lvl[idx] = (byte)level;
                }
            }
        }

        static void Line(int x0, int y0, int x1, int y1, List<Vector2Int> into, bool first)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy;
            bool start = true;
            while (true)
            {
                if (!start || first) into.Add(new Vector2Int(x0, y0));
                start = false;
                if (x0 == x1 && y0 == y1) break;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        void OnDisable()
        {
            if (_canvas != null) _canvas.enabled = false;
            _bolts?.Clear();
        }

        void OnDestroy()
        {
            if (_canvas != null) Destroy(_canvas.gameObject);
            if (_tex != null) Destroy(_tex);
        }
    }
}
