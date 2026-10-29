using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// A picture that is drawn only when it changes - the room's large, mostly still pictures (the
    /// scars, the sky over the eroding foundation). TexelPicture redraws and uploads every frame; at the
    /// size of the whole room that is megabytes a frame for nothing. Rebuilt after a domain reload.
    /// </summary>
    public abstract class StillPicture : TexelPicture
    {
        [System.NonSerialized] bool _valid;
        [System.NonSerialized] public bool Dirty = true;

        protected override void LateUpdate()
        {
            if (!Dirty && _valid && Tex != null) { Age += Time.deltaTime; return; }
            Dirty = false;
            base.LateUpdate();
            _valid = true;
        }
    }

    /// <summary>
    /// Icarus's marks on his floor (the user's picks): THE STAR'S SCARS - black burns radiating from the
    /// middle where the well has opened before - and, once he has enraged, the CRACKS running out from
    /// the centre and glowing in the star's light. The cracks stay, whatever he heals back (the user's
    /// call). Drawn over the biome's own floor (the room is the biome's, his marks on top).
    /// </summary>
    public class IcarusMarks : StillPicture
    {
        [SerializeField] bool _cracked;
        [SerializeField] int _seed;

        public bool Cracked { get => _cracked; set { if (_cracked != value) { _cracked = value; Dirty = true; } } }

        public static IcarusMarks Build(Transform room, int seed)
        {
            var go = new GameObject("icarus.marks");
            go.transform.SetParent(room, false);
            go.transform.position = Vector3.zero;
            int r = Mathf.CeilToInt(6.5f * IcarusCanvas.Ppu37);
            var p = Attach<IcarusMarks>(go.transform, "marks", Vector2.zero, r, r, r, SortingOrders.GroundCarved, IcarusCanvas.Ppu37, clip: true);
            p._seed = seed;
            return p;
        }

        static readonly Color32 Burn = new(20, 16, 16, 255), Scorch = new(42, 28, 20, 255);

        protected override bool Draw()
        {
            float ppu = IcarusCanvas.Ppu37;
            // the scars: burns from the middle, each its own length, a scorched edge on one side
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f + FireArt.Hash(_seed, i, 2) * 0.3f, len = (2.5f + FireArt.Hash(_seed, i, 3) * 3.5f) * ppu;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a) * 0.62f;
                for (float r = 0.25f * ppu; r < len; r += 0.6f)
                {
                    int x = Mathf.RoundToInt(ca * r), y = Mathf.RoundToInt(sa * r);
                    float k = r / len;
                    Over(x, y, Burn, 0.9f * (1f - k * 0.5f));
                    if (k < 0.65f) { Over(x, y - 1, Scorch, 0.7f); Over(x + 1, y, Burn, 0.6f * (1f - k)); }
                }
            }
            for (int dy = -6; dy <= 6; dy++) for (int dx = -9; dx <= 9; dx++) if (dx * dx / 81f + dy * dy / 36f <= 1f) Over(dx, dy, Burn, 0.8f);
            if (!_cracked) return true;
            // the cracks: wandering lines out from the centre, white-hot near it, cooling to red
            var e = IcarusCanvas.Ember;
            for (int i = 0; i < 9; i++)
            {
                float x = 0f, y = 0f, a = FireArt.Hash(_seed, i, 5) * Mathf.PI * 2f;
                for (int s = 0; s < 110; s++)
                {
                    a += (FireArt.Hash(_seed + i, s, 6) - 0.5f) * 0.7f;
                    x += Mathf.Cos(a) * 1.4f; y += Mathf.Sin(a) * 1.4f * 0.62f;
                    if (!Inside(Mathf.RoundToInt(x), Mathf.RoundToInt(y))) break;
                    var c = e[s < 30 ? 0 : s < 70 ? 1 : 2];
                    Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y) - 1, Burn, 0.8f);
                    Over(Mathf.RoundToInt(x), Mathf.RoundToInt(y), c, 0.95f);
                }
            }
            return true;
        }
    }

    /// <summary>
    /// The lunge's CIRCLE (the user's picks: he rises, a wide circle follows you, locks, he dives into
    /// it): a hard red edge round the spot, filling inward as the tell runs (the clock), the edge turning
    /// pale the moment it LOCKS. Its own small picture, moved to the circle.
    /// </summary>
    public class IcarusCircle : TexelPicture
    {
        public float Radius, Fill;
        public bool Locked, On;

        public static IcarusCircle Build(Transform room)
        {
            var go = new GameObject("icarus.circle");
            go.transform.SetParent(room, false);
            int r = Mathf.CeilToInt(Tuning.Icarus.LungeRadius * 1.3f * TexelFx.Body) + 3;
            return Attach<IcarusCircle>(go.transform, "circle", Vector2.zero, r, r, r, SortingOrders.GroundDanger);
        }

        protected override bool Draw()
        {
            if (!On || Radius <= 0f) return false;
            var red = MedusaCanvas.Red;
            float r = Radius * Ppu;
            bool blink = Mathf.Repeat(Age * 6f, 1f) < 0.6f;
            int e = Mathf.CeilToInt(r) + 1;
            for (int dy = -e; dy <= e; dy++)
                for (int dx = -e; dx <= e; dx++)
                {
                    // flattened to the floor's tilt, like every ground ring
                    float d = Mathf.Sqrt(dx * dx + dy * dy / 0.36f);
                    if (d > r + 0.5f) continue;
                    if (d > r - 1.4f) Over(dx, dy, Locked ? red[1] : red[2], Locked || blink ? 1f : 0.7f);
                    else if (d >= r * (1f - Fill)) Over(dx, dy, red[3], Locked ? 0.45f : 0.3f);
                }
            return true;
        }
    }

    /// <summary>
    /// The sky round his floor (the user's picks and corrections): the hall's north wall has FALLEN OPEN
    /// in the middle onto REAL SKY - that opening holds its shape from the first frame to the last and
    /// never erodes. As his HP falls, the FOUNDATION round the floor erodes away stone by stone - the
    /// walls, their tops, the dark beyond, the floor's very edge - opening onto sky, until the floor
    /// hangs in open sky. It follows the LOWEST he has been, so it never rebuilds. A picture only, over
    /// the walls and under every body; the walls' colliders stay where they were.
    /// </summary>
    public class IcarusSky : StillPicture
    {
        [SerializeField] float _decay;
        [SerializeField] int _seed;

        /// <summary>0 the broken hall .. 1 the floor hanging in sky. Redrawn in steps.</summary>
        public float Decay
        {
            get => _decay;
            set { float q = Mathf.Round(Mathf.Clamp01(value) * 40f) / 40f; if (!Mathf.Approximately(q, _decay)) { _decay = q; Dirty = true; } }
        }

        static float Ppu37 => IcarusCanvas.Ppu37;
        static float InnerW => Arena.HalfExtents.x - Tuning.Scenery.WallInset;
        static float InnerH => Arena.HalfExtents.y - Tuning.Scenery.WallInset;
        static float CapTop => InnerH + Tuning.Scenery.FaceHeight + Tuning.Scenery.CapHeight;

        public static IcarusSky Build(Transform room, int seed)
        {
            var go = new GameObject("icarus.sky");
            go.transform.SetParent(room, false);
            go.transform.position = Vector3.zero;
            int hw = Mathf.CeilToInt((InnerW + 2.5f) * Ppu37), below = Mathf.CeilToInt((InnerH + 2.5f) * Ppu37), above = Mathf.CeilToInt((CapTop + 2.5f) * Ppu37);
            // over the walls (-8, the tops and the face) and the time-of-day tint (-7), under every decal and body
            var p = Attach<IcarusSky>(go.transform, "sky", Vector2.zero, hw, below, above, -6, Ppu37);
            p._seed = seed;
            // past the picture's edge, as far as the camera can pan: flat sky over whatever the biome built
            float x0 = hw / Ppu37, y0 = below / Ppu37, y1 = above / Ppu37, far = 40f;
            var c = (Color)IcarusCanvas.Sky[1];
            Strip(go.transform, "sky.n", new Vector2(0f, y1 + far * 0.5f), new Vector2(far * 2f, far), c);
            Strip(go.transform, "sky.s", new Vector2(0f, -y0 - far * 0.5f), new Vector2(far * 2f, far), c);
            Strip(go.transform, "sky.w", new Vector2(-x0 - far * 0.5f, (y1 - y0) * 0.5f), new Vector2(far, y0 + y1), c);
            Strip(go.transform, "sky.e", new Vector2(x0 + far * 0.5f, (y1 - y0) * 0.5f), new Vector2(far, y0 + y1), c);
            return p;
        }

        static void Strip(Transform parent, string name, Vector2 centre, Vector2 size, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Square;
            sr.color = c;
            sr.sortingOrder = -6;
        }

        /// <summary>The sky: one flat blue (it meets the far strips without a seam), soft clouds drifting through it.</summary>
        Color32 SkyAt(int x, int y)
        {
            var c = IcarusCanvas.Sky[1];
            float cloud = FireArt.Noise(x * 0.018f + _seed, y * 0.045f);
            if (cloud > 0.62f) c = Lerp(c, IcarusCanvas.White, (cloud - 0.62f) * 1.6f);
            return c;
        }

        static Color32 Cs(int hex) => new((byte)(hex >> 16), (byte)(hex >> 8), (byte)hex, 255);
        static readonly Color32[] Stone = { Cs(0xa89c8a), Cs(0x887d6e), Cs(0x6a6156), Cs(0x4e4740), Cs(0x37322d) };
        static readonly Color32 Mortar = Cs(0x26221f);

        /// <summary>His walls: running-bond tops lit from above on three sides; on the north, the face in
        /// courses darkening down toward the floor, a lighter cap along its top.</summary>
        Color32 WallAt(int x, int y, float ih, float face, float cap, float iw, float ppu)
        {
            if (y >= ih)
            {
                if (y >= face)
                {
                    int cy = y - Mathf.RoundToInt(face);
                    if (cy == Mathf.RoundToInt(cap - face) - 1) return Mortar;
                    return Stone[cy >= Mathf.RoundToInt(cap - face) - 3 ? 0 : 1];
                }
                int row = Mathf.FloorToInt((y - ih) / 7f), col = Mathf.FloorToInt((x + 4000 + (row & 1) * 7) / 14f);
                bool m = Mathf.RoundToInt(y - ih) % 7 == 6 || (x + 4000 + (row & 1) * 7) % 14 == 13;
                if (m) return Mortar;
                float shade = 1f - (y - ih) / Mathf.Max(1f, face - ih);   // darker near the floor
                return Stone[Mathf.Clamp(Mathf.RoundToInt(1.5f + shade * 2f + FireArt.Hash(col, row, _seed) * 0.8f - 0.4f), 1, 4)];
            }
            // the tops: big blocks, a dark edge where they meet the floor
            float edge = Mathf.Max(Mathf.Abs(x) - iw, -y - ih);
            if (edge < 2f) return Mortar;
            int bx = Mathf.FloorToInt((x + 4000) / 12f), by = Mathf.FloorToInt((y + 4000) / 10f);
            bool mm = (x + 4000) % 12 == 11 || (y + 4000) % 10 == 9;
            if (mm) return Mortar;
            return Stone[Mathf.Clamp(Mathf.RoundToInt(1f + FireArt.Hash(bx, by, _seed) * 1.4f), 0, 4)];
        }

        protected override bool Draw()
        {
            float ppu = Ppu37, iw = InnerW * ppu, ih = InnerH * ppu, face = (InnerH + Tuning.Scenery.FaceHeight) * ppu, cap = CapTop * ppu, open = Tuning.Icarus.OpenHalf * ppu;
            float d = _decay;
            var ink = new Color32(13, 11, 14, 255);
            for (int y = -Cy; y < H - Cy; y++)
                for (int x = -Cx; x < W - Cx; x++)
                {
                    bool inFloor = Mathf.Abs(x) < iw && Mathf.Abs(y) < ih;
                    // the fallen stretch of the north wall: real sky from the start, ragged at its sides,
                    // a fringe of rubble at its foot - and it never changes
                    bool gap = y >= ih && Mathf.Abs(x) < open - Mathf.Abs(Mathf.Sin(y * 0.11f)) * 0.35f * ppu;
                    if (gap)
                    {
                        // the broken lip where the wall stood: one dark line, ragged, never a texel's noise
                        // (once the floor's own edge has begun to crumble, its lip goes with it)
                        if (d < 0.25f && y < ih + 2f + Mathf.Floor(FireArt.Hash(Mathf.FloorToInt((x + 4000) / 6f), 1, _seed) * 3f)) { Put(x, y, ink); continue; }
                        Put(x, y, SkyAt(x, y));
                        continue;
                    }
                    if (!inFloor)
                    {
                        // HIS OWN BOUNDARY in every biome (the user's call): walls lining the room - tops on
                        // three sides, the north wall's face and cap - and SKY behind them from the start. As
                        // his HP falls the walls go to sky in whole blocks, never a texel at a time.
                        bool north = y >= ih;
                        bool wall = north ? y < cap : (Mathf.Abs(x) < iw + Tuning.Scenery.TopBand * ppu && -y < ih + Tuning.Scenery.TopBand * ppu);
                        if (!north && Mathf.Abs(x) >= iw + Tuning.Scenery.TopBand * ppu) wall = false;
                        if (north && Mathf.Abs(x) >= iw + Tuning.Scenery.TopBand * ppu) wall = false;
                        float n = FireArt.Hash(Mathf.FloorToInt((x + 4000) / 6f), Mathf.FloorToInt((y + 4000) / 5f), _seed + 21);
                        if (wall && !(d > 0f && n < d * 1.15f)) Put(x, y, WallAt(x, y, ih, face, cap, iw, ppu));
                        else Put(x, y, SkyAt(x, y));
                        continue;
                    }
                    if (d <= 0f) continue;
                    // the floor's edge crumbles in, ragged, a dark lip where it broke
                    float toEdge = Mathf.Min(iw - Mathf.Abs(x), ih - Mathf.Abs(y)) / ppu;
                    float bite = d * Tuning.Icarus.EdgeBite * (0.5f + FireArt.Noise(x * 0.06f + _seed, y * 0.06f));
                    if (toEdge < bite) Put(x, y, SkyAt(x, y));
                    else if (toEdge < bite + 1f / ppu && d > 0.05f) Put(x, y, ink);
                }
            return true;
        }
    }

    /// <summary>One of the tower's pieces falling past in the sky beyond his floor (once it has come
    /// apart far enough). A small picture of its own; it wraps back to the top.</summary>
    public class IcarusDebris : TexelPicture
    {
        static readonly Color32 Ink = new(13, 11, 14, 255);
        [SerializeField] float _speed, _sway;
        [SerializeField] int _w, _seed;
        [SerializeField] Vector2 _from;

        public static IcarusDebris Build(Transform room, int seed, int i)
        {
            var go = new GameObject("icarus.debris");
            go.transform.SetParent(room, false);
            var p = Attach<IcarusDebris>(go.transform, "piece", Vector2.zero, 8, 6, 6, -5, IcarusCanvas.Ppu37);
            p._seed = seed + i * 7;
            p._w = 3 + Mathf.FloorToInt(FireArt.Hash(p._seed, 1) * 6f);
            p._speed = 1.2f + FireArt.Hash(p._seed, 2) * 1.6f;
            p._sway = FireArt.Hash(p._seed, 3) * 6f;
            bool side = FireArt.Hash(p._seed, 4) < 0.7f;
            float x = side ? (Arena.HalfExtents.x + 0.6f + FireArt.Hash(p._seed, 5) * 2f) * (FireArt.Hash(p._seed, 6) < 0.5f ? -1f : 1f)
                           : (FireArt.Hash(p._seed, 5) - 0.5f) * Arena.HalfExtents.x * 1.6f;
            p._from = new Vector2(x, Arena.HalfExtents.y + 4f + FireArt.Hash(p._seed, 7) * 14f);
            go.transform.position = p._from;
            return p;
        }

        void Update()
        {
            var at = (Vector2)transform.position + Vector2.down * _speed * Time.deltaTime;
            at.x = _from.x + Mathf.Sin(Age * 0.7f + _sway) * 0.15f;
            if (at.y < -Arena.HalfExtents.y - 4f) at.y = Arena.HalfExtents.y + 6f;
            transform.position = at;
        }

        protected override bool Draw()
        {
            var pl = IcarusCanvas.Plate;
            for (int dy = 0; dy < 3; dy++) for (int dx = 0; dx < _w; dx++) Put(dx - _w / 2, dy, dy == 2 ? pl[1] : pl[3]);
            for (int dx = -1; dx <= _w; dx++) Over(dx - _w / 2, -1, Ink, 0.6f);
            return true;
        }
    }

    /// <summary>
    /// The WELL (the user's pick, "the blade's star"): the dying star risen off his guard, hanging over
    /// the middle of the room - a black sun with a burning rim and flares - and the STONES torn from the
    /// walls, dragged in to it. One picture over the room, drawn only while the well is open; the stones'
    /// hits are the fight's (<see cref="Touches"/>).
    /// </summary>
    public class IcarusWell : TexelPicture
    {
        [System.Serializable]
        public struct Stone { public Vector2 From; public float Born, Flight; public int Size; }

        static readonly Color32 Ink = new(13, 11, 14, 255);
        public List<Stone> Stones = new();
        public Vector2 StarAt;
        /// <summary>0 none .. 1 the star fully open.</summary>
        public float Open;
        [SerializeField] float _clock;
        public float Clock => _clock;

        public static IcarusWell Build(Transform room)
        {
            var go = new GameObject("icarus.well");
            go.transform.SetParent(room, false);
            go.transform.position = Vector3.zero;
            int hw = Mathf.CeilToInt((Arena.HalfExtents.x + 1f) * TexelFx.Body), hh = Mathf.CeilToInt((Arena.HalfExtents.y + 3f) * TexelFx.Body);
            return Attach<IcarusWell>(go.transform, "well", Vector2.zero, hw, hh, hh, TexelFx.Air);
        }

        void Update() => _clock += Time.deltaTime;

        public static Vector2 PosOf(Stone s, Vector2 to, float clock)
        {
            float u = Mathf.Clamp01((clock - s.Born) / s.Flight);
            return Vector2.Lerp(s.From, to, u * u);
        }

        /// <summary>Does a stone touch a body of <paramref name="radius"/> at <paramref name="p"/>? The one
        /// that does is spent.</summary>
        public bool Touches(Vector2 p, float radius, Vector2 to)
        {
            Stones ??= new();
            float rr = radius + Tuning.Icarus.StoneRadius;
            for (int i = 0; i < Stones.Count; i++)
                if ((PosOf(Stones[i], to, _clock) - p).sqrMagnitude <= rr * rr) { Stones.RemoveAt(i); return true; }
            return false;
        }

        /// <summary>Stones that have arrived, gone; how many crushed in at the middle this frame.</summary>
        public int Settle()
        {
            Stones ??= new();
            return Stones.RemoveAll(s => _clock - s.Born >= s.Flight);
        }

        protected override bool Draw()
        {
            Stones ??= new();
            if (Open <= 0f && Stones.Count == 0) return false;
            var st = IcarusCanvas.Star; var pl = IcarusCanvas.Plate;
            float ppu = Ppu;
            var to = StarAt;
            // the star: a black sun, its rim burning, flares turning off it
            if (Open > 0f)
            {
                float r = (0.32f * Open) * ppu;
                int cx = Mathf.RoundToInt(StarAt.x * ppu), cy = Mathf.RoundToInt(StarAt.y * ppu);
                int e = Mathf.CeilToInt(r + 4f);
                for (int dy = -e; dy <= e; dy++)
                    for (int dx = -e; dx <= e; dx++)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d < r) Put(cx + dx, cy + dy, IcarusCanvas.Black);
                        else if (d < r + 1.3f) Put(cx + dx, cy + dy, st[1]);
                        else if (d < r + 3.2f && FireArt.Hash(Mathf.RoundToInt(Mathf.Atan2(dy, dx) * 8f + Age * 6f), 2) > 0.45f) Over(cx + dx, cy + dy, st[2], 0.7f * Open);
                    }
                for (int s = -1; s <= 1; s += 2)
                    for (int k = 0; k < Mathf.RoundToInt(r + 6f); k++) Over(cx + s * (Mathf.RoundToInt(r) + k), cy, st[k < 3 ? 1 : 2], (1f - k / (r + 6f)) * Open);
            }
            // the stones
            foreach (var s in Stones)
            {
                var p = PosOf(s, to, _clock) * ppu;
                int x = Mathf.RoundToInt(p.x), y = Mathf.RoundToInt(p.y), n = s.Size;
                for (int dy = -n; dy <= n; dy++) for (int dx = -n; dx <= n; dx++) if (Mathf.Abs(dx) + Mathf.Abs(dy) <= n + 1) Over(x + dx, y + dy - 1, Ink, 0.6f);
                for (int dy = -n; dy <= n; dy++) for (int dx = -n; dx <= n; dx++) if (Mathf.Abs(dx) + Mathf.Abs(dy) <= n) Put(x + dx, y + dy, pl[Mathf.Clamp(1 + (dx - dy + n) / Mathf.Max(1, n), 0, 4)]);
            }
            return true;
        }
    }
}
