using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Bosses
{
    public enum ShotKind : byte { Bead, Shard, Hoop, Bone }

    /// <summary>One of Mercurius's projectiles. Flies straight; a RETURNING shard stops at
    /// <see cref="StickAt"/>, waits until <see cref="BackAt"/>, then flies back to <see cref="Home"/>.</summary>
    [System.Serializable]
    public struct Shot
    {
        public Vector2 P, V, Home;
        public ShotKind Kind;
        public float Age, StickAt, BackAt;
        public bool Returns;
    }

    /// <summary>A crescent: an arc of water widening from <see cref="O"/>, aimed at <see cref="Aim"/>
    /// (radians) <see cref="Half"/> either side. One object, not points, so it never thins into gaps.</summary>
    [System.Serializable]
    public struct Crescent
    {
        public Vector2 O;
        public float Aim, Half, Born;
    }

    /// <summary>
    /// Every projectile Mercurius has in the room, drawn as ONE picture over the arena and tested by ONE
    /// call (<see cref="Touches"/>) - hundreds of shots as GameObjects would cost far more than they show.
    /// Not <see cref="EnemyProjectile"/>s: NOTHING CUTS THEM (the user's call), so Combat.Clash never sees
    /// them. The bone CHAINS are set by the fight every frame (they sweep, they don't fly); their test is
    /// SWEPT, from last frame's angle to this one's, since their far ends cross the player faster than a
    /// frame.
    /// </summary>
    public class Volley : TexelPicture
    {
        // Non-readonly and re-created if null - see ENGINEERING.md, domain reload traps.
        public List<Shot> Shots = new();
        public List<Crescent> Crescents = new();
        [SerializeField] float _clock;

        public Vector2 ChainO;
        public float ChainLength;
        public int ChainCount;
        public float[] ChainAngle = new float[3], ChainPrev = new float[3], ChainHole = new float[3];

        public float Clock => _clock;

        public static Volley Build(Transform room)
        {
            var go = new GameObject("mercurius.volley");
            go.transform.SetParent(room, false);
            go.transform.position = Vector3.zero;
            int hw = Mathf.CeilToInt(Arena.HalfExtents.x * TexelFx.Body) + 6, hh = Mathf.CeilToInt(Arena.HalfExtents.y * TexelFx.Body) + 6;
            return Attach<Volley>(go.transform, "shots", Vector2.zero, hw, hh, hh, TexelFx.Air);
        }

        public void Clear()
        {
            Shots?.Clear(); Crescents?.Clear();
            ChainCount = 0;
        }

        public void Add(Shot s) { Shots ??= new(); Shots.Add(s); }
        public void Add(Crescent c) { Crescents ??= new(); c.Born = _clock; Crescents.Add(c); }

        public static float CrescentRadius(Crescent c, float clock)
            => Tuning.Mercurius.SwirlRadius + Tuning.Mercurius.CrescentSpeed * (clock - c.Born);

        void Update()
        {
            float dt = Time.deltaTime;
            _clock += dt;
            Shots ??= new(); Crescents ??= new();
            var ext = Arena.HalfExtents + Vector2.one * 0.6f;
            for (int i = Shots.Count - 1; i >= 0; i--)
            {
                var s = Shots[i];
                s.Age += dt;
                bool gone = false;
                if (s.Returns && s.Age >= s.StickAt)
                {
                    if (s.Age >= s.BackAt)
                    {
                        var d = s.Home - s.P;
                        float step = s.V.magnitude * dt;
                        if (d.magnitude <= step) gone = true;
                        else s.P += d.normalized * step;
                    }
                }
                else s.P += s.V * dt;
                if (Mathf.Abs(s.P.x) > ext.x || Mathf.Abs(s.P.y) > ext.y) gone = true;
                if (gone) Shots.RemoveAt(i); else Shots[i] = s;
            }
            float far = Arena.HalfExtents.magnitude * 2f + 2f;
            Crescents.RemoveAll(c => CrescentRadius(c, _clock) > far);
        }

        /// <summary>
        /// Does anything touch a body of <paramref name="radius"/> at <paramref name="p"/>? A shot that
        /// does is spent when <paramref name="consume"/> (a crescent or a chain is not: the hurt grace
        /// covers it). The chains: swept since last frame, and a bone missing at the HOLE leaves a gap.
        /// </summary>
        public bool Touches(Vector2 p, float radius, bool consume)
        {
            float rr = radius + Tuning.Mercurius.ShotRadius;
            if (Shots != null)
                for (int i = 0; i < Shots.Count; i++)
                    if ((Shots[i].P - p).sqrMagnitude <= rr * rr)
                    {
                        if (consume) Shots.RemoveAt(i);
                        return true;
                    }
            if (Crescents != null)
                foreach (var c in Crescents)
                {
                    var d = p - c.O;
                    float dist = d.magnitude, r = CrescentRadius(c, _clock);
                    if (dist < 0.01f || Mathf.Abs(dist - r) > rr + 0.06f) continue;
                    float off = Mathf.Abs(Mathf.DeltaAngle(c.Aim * Mathf.Rad2Deg, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg));
                    if (off <= c.Half * Mathf.Rad2Deg + rr / dist * Mathf.Rad2Deg) return true;
                }
            for (int j = 0; j < ChainCount && j < 3; j++)
            {
                var d = p - ChainO;
                float rp = d.magnitude;
                if (rp > ChainLength + rr || rp < Tuning.Mercurius.BoneFrom - rr) continue;
                if (NearestBone(rp, ChainHole[j]) > rr) continue;     // in the hole
                float tol = Mathf.Asin(Mathf.Clamp01(rr / Mathf.Max(rr, rp))) * Mathf.Rad2Deg;
                float ap = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, prev = ChainPrev[j] * Mathf.Rad2Deg;
                float sweep = Mathf.DeltaAngle(prev, ChainAngle[j] * Mathf.Rad2Deg), rel = Mathf.DeltaAngle(prev, ap);
                if (sweep >= 0f ? (rel >= -tol && rel <= sweep + tol) : (rel <= tol && rel >= sweep - tol)) return true;
            }
            return false;
        }

        /// <summary>How far, along the chain, from <paramref name="r"/> to the nearest bone still on it.</summary>
        float NearestBone(float r, float hole)
        {
            float from = Tuning.Mercurius.BoneFrom, every = Tuning.Mercurius.BoneEvery, best = float.MaxValue;
            int k0 = Mathf.FloorToInt((r - from) / every);
            for (int k = k0 - 2; k <= k0 + 2; k++)
            {
                if (k < 0) continue;
                float b = from + k * every;
                if (b > ChainLength || IsHole(b, hole)) continue;
                best = Mathf.Min(best, Mathf.Abs(r - b));
            }
            return best;
        }

        public static bool IsHole(float b, float hole) => Mathf.Abs(b - hole) < Tuning.Mercurius.HoleHalf;

        // ------------------------------------------------------------------ the picture

        static readonly Vector2Int[] N4 = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        readonly List<(int x, int y, Color32 c)> _pix = new();

        protected override bool Draw()
        {
            bool any = (Shots != null && Shots.Count > 0) || (Crescents != null && Crescents.Count > 0) || ChainCount > 0;
            if (!any) return false;
            var o = transform.parent != null ? (Vector2)transform.parent.position : Vector2.zero;
            var W_ = MercuriusCanvas.Water; var B = MercuriusCanvas.Bronze;
            _pix.Clear();
            if (Shots != null)
                foreach (var s in Shots)
                {
                    int x = Mathf.RoundToInt((s.P.x - o.x) * Ppu), y = Mathf.RoundToInt((s.P.y - o.y) * Ppu);
                    switch (s.Kind)
                    {
                        case ShotKind.Bead:
                            // a bead of water: a round drop, lit from the top left
                            for (int dy = -2; dy <= 2; dy++)
                                for (int dx = -2; dx <= 2; dx++)
                                {
                                    if (dx * dx + dy * dy > 5) continue;
                                    int k = dx - dy <= -2 ? 0 : dx - dy <= 0 ? 1 : dx - dy <= 2 ? 2 : 4;
                                    _pix.Add((x + dx, y + dy, W_[k]));
                                }
                            break;
                        case ShotKind.Shard:
                        {
                            var v = s.Returns && s.Age >= s.BackAt ? s.Home - s.P : s.V;
                            var f = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right; var sd = new Vector2(-f.y, f.x);
                            for (int k = -4; k <= 5; k++)
                            {
                                float w = k >= 3 ? 0f : k >= -2 ? 1f : 0.5f;
                                for (float q = -w; q <= w; q += 1f)
                                    _pix.Add((x + Mathf.RoundToInt(f.x * k + sd.x * q), y + Mathf.RoundToInt(f.y * k + sd.y * q), B[k >= 4 ? 0 : q > 0 ? 1 : q < 0 ? 3 : 2]));
                            }
                            for (int q = -1; q <= 1; q += 2) _pix.Add((x + Mathf.RoundToInt(-f.x * 4 + sd.x * q * 2.5f), y + Mathf.RoundToInt(-f.y * 4 + sd.y * q * 2.5f), B[3]));
                            break;
                        }
                        case ShotKind.Hoop:
                            for (int k = 0; k < 22; k++)
                            {
                                float a = k / 22f * Mathf.PI * 2f, sa = Mathf.Sin(a);
                                var c = B[sa > 0.3f ? 0 : sa > -0.3f ? 1 : 3];
                                _pix.Add((x + Mathf.RoundToInt(Mathf.Cos(a) * 3.4f), y + Mathf.RoundToInt(sa * 3.4f), c));
                                _pix.Add((x + Mathf.RoundToInt(Mathf.Cos(a) * 2.5f), y + Mathf.RoundToInt(sa * 2.5f), B[2]));
                            }
                            break;
                        default:
                            Bone(x, y, B);
                            break;
                    }
                }
            for (int j = 0; j < ChainCount && j < 3; j++)
            {
                var dir = new Vector2(Mathf.Cos(ChainAngle[j]), Mathf.Sin(ChainAngle[j]));
                for (float b = Tuning.Mercurius.BoneFrom; b <= ChainLength; b += Tuning.Mercurius.BoneEvery)
                {
                    var w = ChainO + dir * b - o;
                    int x = Mathf.RoundToInt(w.x * Ppu), y = Mathf.RoundToInt(w.y * Ppu);
                    if (IsHole(b, ChainHole[j])) { if (Mathf.Repeat(Age * 4f, 1f) < 0.5f) _pix.Add((x, y, W_[0])); continue; }
                    Bone(x, y, B);
                    var thread = ChainO + dir * (b + Tuning.Mercurius.BoneEvery * 0.5f) - o;
                    if (!IsHole(b + Tuning.Mercurius.BoneEvery, ChainHole[j])) _pix.Add((Mathf.RoundToInt(thread.x * Ppu), Mathf.RoundToInt(thread.y * Ppu), W_[2]));
                }
            }
            foreach (var p in _pix) foreach (var n in N4) Over(p.x + n.x, p.y + n.y, K, 0.75f);
            foreach (var p in _pix) Put(p.x, p.y, p.c);

            if (Crescents != null)
                foreach (var c in Crescents)
                {
                    float r = CrescentRadius(c, _clock) * Ppu;
                    var co = (c.O - o) * Ppu;
                    float step = 0.6f / Mathf.Max(4f, r);
                    for (float a = c.Aim - c.Half; a <= c.Aim + c.Half; a += step)
                    {
                        float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                        float edge = Mathf.Min(a - (c.Aim - c.Half), c.Aim + c.Half - a) * r;
                        for (int k = -2; k <= 2; k++)
                        {
                            if (edge < 2f && Mathf.Abs(k) == 2) continue;
                            var col = k == 2 ? K : k == -2 ? K : k == 1 ? W_[0] : k == 0 ? W_[1] : W_[3];
                            Over(Mathf.RoundToInt(co.x + ca * (r + k)), Mathf.RoundToInt(co.y + sa * (r + k)), col, Mathf.Abs(k) == 2 ? 0.6f : 1f);
                        }
                    }
                }
            return true;
        }

        /// <summary>A vertebra: a bronze block with a spine on top and knuckles either side.</summary>
        void Bone(int x, int y, Color32[] B)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -2; dx <= 2; dx++) _pix.Add((x + dx, y + dy, B[dy > 0 ? 1 : dy < 0 ? 3 : 2]));
            _pix.Add((x, y + 2, B[0])); _pix.Add((x - 1, y + 2, B[1])); _pix.Add((x, y + 3, B[0]));
            _pix.Add((x - 3, y, B[3])); _pix.Add((x + 3, y, B[3]));
        }

        static readonly Color32 K = new(7, 11, 16, 255);
    }

    /// <summary>
    /// Mercurius's floor, one picture over the arena: the TIDE (a ring of water from its body to the
    /// walls, its front bright, its red tell on the floor first), the REPULSE (a ring of water sliding
    /// the player out), and the BUBBLES where it will surface. The fight sets the fields; the tide's
    /// hit is the fight's, read from the same <see cref="TideR"/>.
    /// </summary>
    public class MercuriusFloor : TexelPicture
    {
        public Vector2 TideEye;
        /// <summary>The tide's front, world units from its eye; below 0 there is no tide.</summary>
        public float TideR = -1f;
        /// <summary>0..1 through the tide's tell; below 0 no tell.</summary>
        public float Tell = -1f;
        public Vector2 RepulseAt;
        public float RepulseAge = 99f;
        public Vector2 BubblesAt;
        public bool Bubbles;

        public static MercuriusFloor Build(Transform room)
        {
            var go = new GameObject("mercurius.floor");
            go.transform.SetParent(room, false);
            go.transform.position = Vector3.zero;
            int hw = Mathf.CeilToInt(Arena.HalfExtents.x * TexelFx.Body) + 4, hh = Mathf.CeilToInt(Arena.HalfExtents.y * TexelFx.Body) + 4;
            return Attach<MercuriusFloor>(go.transform, "floor", Vector2.zero, hw, hh, hh, SortingOrders.GroundDanger, clip: true);
        }

        void Update() => RepulseAge += Time.deltaTime;

        /// <summary>Every texel whose distance from (ox, oy) lies in [r0, r1], row by row - only the
        /// band is visited, however large the ring.</summary>
        void Band(float ox, float oy, float r0, float r1, System.Action<int, int, float, float> fn)
        {
            if (r1 <= 0f) return;
            r0 = Mathf.Max(0f, r0);
            int y0 = Mathf.Max(-Cy, Mathf.FloorToInt(oy - r1)), y1 = Mathf.Min(H - 1 - Cy, Mathf.CeilToInt(oy + r1));
            for (int y = y0; y <= y1; y++)
            {
                float dy = y - oy, o1 = r1 * r1 - dy * dy;
                if (o1 < 0f) continue;
                float xo = Mathf.Sqrt(o1), xi = r0 * r0 - dy * dy > 0f ? Mathf.Sqrt(r0 * r0 - dy * dy) : -1f;
                int a0 = Mathf.Max(-Cx, Mathf.FloorToInt(ox - xo)), a1 = Mathf.Min(W - 1 - Cx, Mathf.CeilToInt(ox + xo));
                for (int x = a0; x <= a1; x++)
                {
                    float dx = x - ox;
                    if (xi >= 0f && Mathf.Abs(dx) < xi) { x = Mathf.Max(x, Mathf.FloorToInt(ox + xi) - 1); continue; }
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d >= r0 && d <= r1) fn(x, y, d, Mathf.Atan2(dy, dx));
                }
            }
        }

        protected override bool Draw()
        {
            bool repulse = RepulseAge < 0.55f;
            if (TideR < 0f && Tell < 0f && !repulse && !Bubbles) return false;
            var o = transform.parent != null ? (Vector2)transform.parent.position : Vector2.zero;
            var W_ = MercuriusCanvas.Water; var red = MedusaCanvas.Red;

            var ink = new Color32(7, 11, 16, 255);
            if (Tell >= 0f)
            {
                // the tell: a red ring round its coil, wider than its body so nothing covers it, filling in
                var e = (TideEye - o) * Ppu;
                bool on = Mathf.Repeat(Age * 6f, 1f) < 0.6f;
                float r = 1.9f * Ppu;
                Band(e.x, e.y, r - 2f, r + 2f, (x, y, d, a) => Over(x, y, red[2], on ? 1f : 0.55f));
                Band(e.x, e.y, 0f, r - 2f, (x, y, d, a) => { if (d >= r * (1f - Tell)) Over(x, y, red[3], 0.4f); });
            }
            if (TideR >= 0f)
            {
                // the tide: an ink line ahead of a white crest, foam, then deep water thinning out behind
                var e = (TideEye - o) * Ppu;
                float r1 = TideR * Ppu, th = Tuning.Mercurius.TideThickness * Ppu;
                int frame = Mathf.FloorToInt(Age * 12f);
                Band(e.x, e.y, r1 - th, r1 + 2f, (x, y, d, a) =>
                {
                    if (d > r1) { Over(x, y, ink, 0.8f); return; }
                    float k = (r1 - d) / th;   // 0 at the front .. 1 at its back
                    if (k < 0.05f) { Over(x, y, W_[0], 1f); return; }
                    if (k < 0.1f) { Over(x, y, W_[2], 1f); return; }
                    if (FireArt.Hash(x, y, frame) > 0.9f && k < 0.5f) { Over(x, y, W_[0], 0.9f); return; }
                    Over(x, y, W_[k < 0.35f ? 3 : 4], 0.75f * (1f - k * 0.8f));
                });
            }
            if (repulse)
            {
                var e = (RepulseAt - o) * Ppu;
                float r = (0.3f + RepulseAge / 0.55f * Tuning.Mercurius.RepulseRadius) * Ppu, fade = 1f - RepulseAge / 0.55f;
                Band(e.x, e.y, r - 4f, r + 1f, (x, y, d, a) => Over(x, y, d > r ? ink : ((x + y) & 1) == 0 ? W_[0] : W_[3], d > r ? fade * 0.7f : fade));
            }
            if (Bubbles)
            {
                var e = (BubblesAt - o) * Ppu;
                for (int i = 0; i < 14; i++)
                {
                    float ph = Mathf.Repeat(Age * 1.6f + FireArt.Hash(i, 4), 1f);
                    int x = Mathf.RoundToInt(e.x + (FireArt.Hash(i, 5) - 0.5f) * 40f), y = Mathf.RoundToInt(e.y + (FireArt.Hash(i, 6) - 0.5f) * 14f + ph * 4f);
                    Over(x, y - 1, ink, (1f - ph) * 0.6f); Over(x, y, W_[0], 1f - ph); Over(x + 1, y, W_[3], (1f - ph) * 0.9f);
                }
                for (int k = 0; k < 2; k++)
                {
                    float ph = Mathf.Repeat(Age * 0.9f + k * 0.5f, 1f), r = 10f + ph * 30f;
                    for (int i = 0; i < 80; i++) { float a = i / 80f * Mathf.PI * 2f; Over(Mathf.RoundToInt(e.x + Mathf.Cos(a) * r), Mathf.RoundToInt(e.y + Mathf.Sin(a) * r * 0.4f), W_[4], (1f - ph) * 0.8f); }
                }
            }
            return true;
        }
    }
}
