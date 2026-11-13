using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    public static partial class Body8
    {
        /// <summary>The light on a weapon's walls (drawn frame: x right, y up, z toward the camera): the kit's upper left.</summary>
        static readonly Vector3 SlabLight = new Vector3(-0.55f, 0.65f, 0.55f).normalized;
        static List<int> _slabPx;

        /// <summary>
        /// A weapon card WITH ITS THICKNESS (Weapon8.Depth, the user's "lean", 2026-11-12): each canvas texel's line of sight
        /// (the camera's, (0, DepthY, 1)) is walked through the card's texel columns, nearest first, and stops in the first
        /// column it is inside - on a face (the art, as today) or a wall (the part's own colour, lit from the upper left).
        /// A one-texel outline then goes round what the art's own outline does not edge. False: no slab, draw it flat.
        /// </summary>
        static bool DrawSlab(Weapon8.Card cd, int id)
        {
            var art = cd.Art;
            float uLen = cd.U.magnitude, vLen = cd.V.magnitude;
            var slab = Weapon8.SlabOf(art, 0.5f * (uLen + vLen));
            if (slab == null || uLen < 1e-4f || vLen < 1e-4f) return false;
            var N = Vector3.Cross(cd.U, cd.V);
            if (N.sqrMagnitude < 1e-8f) return false;
            N.Normalize();
            var M = new Matrix4x4(cd.U, cd.V, N, new Vector4(0f, 0f, 0f, 1f));
            if (Mathf.Abs(M.determinant) < 1e-6f) return false;
            var Mi = M.inverse;
            Vector3 Uh = cd.U / uLen, Vh = cd.V / vLen;
            float hmax = slab.HMax;

            // the screen box of the slab's corners
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int corner = 0; corner < 8; corner++)
            {
                float a = (corner & 1) == 0 ? -art.Pivot.x : art.W - art.Pivot.x, b = (corner & 2) == 0 ? -art.Pivot.y : art.H - art.Pivot.y;
                float w = (corner & 4) == 0 ? -hmax : hmax;
                var c = Locomotion8Screen(cd.O + cd.U * a + cd.V * b + N * w);
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x); minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
            }
            // the line of sight in the card's frame (texels across, texels down, body texels deep), walked AWAY from the
            // camera: s = -z
            var d = -(Vector3)Mi.MultiplyVector(new Vector3(0f, Locomotion8.DepthY, 1f));
            _slabPx ??= new List<int>(); _slabPx.Clear();
            int W = art.W, H = art.H;
            // the point on a texel's line of sight at depth 0, in the card's frame (art coordinates): linear in x and y
            int xa = Mathf.Max(0, Mathf.FloorToInt(minX) - 1), xb = Mathf.Min(_W - 1, Mathf.CeilToInt(maxX) + 1);
            int ya = Mathf.Max(0, Mathf.FloorToInt(minY) - 1), yb = Mathf.Min(_H - 1, Mathf.CeilToInt(maxY) + 1);
            var A00 = (Vector3)Mi.MultiplyVector(new Vector3(xa + 0.5f - _GX, _GY - (ya + 0.5f), 0f) - cd.O) + new Vector3(art.Pivot.x, art.Pivot.y, 0f);
            var Ax = (Vector3)Mi.MultiplyVector(Vector3.right); var Ay = (Vector3)Mi.MultiplyVector(Vector3.down);
            // plain floats from here: the walk runs per canvas texel per column, and Vector3 maths is slow outside IL2CPP
            float dx = d.x, dy = d.y, dz = d.z;
            const float Eps = 1e-7f, Inf = float.PositiveInfinity;
            bool fx = Mathf.Abs(dx) < Eps, fy = Mathf.Abs(dy) < Eps, fz = Mathf.Abs(dz) < Eps;
            float idx = fx ? 0f : 1f / dx, idy = fy ? 0f : 1f / dy, idz = fz ? 0f : 1f / dz;
            int si = dx > 0f ? 1 : -1, sj = dy > 0f ? 1 : -1;
            float tdx = fx ? Inf : Mathf.Abs(idx), tdy = fy ? Inf : Mathf.Abs(idy);
            var half = slab.Half; int[] rowLo = slab.RowLo, rowHi = slab.RowHi;
            for (int y = ya; y <= yb; y++)
            {
                float rx = A00.x + Ay.x * (y - ya), ry = A00.y + Ay.y * (y - ya), rz = A00.z + Ay.z * (y - ya);
                for (int x = xa; x <= xb; x++)
                {
                    float ax = rx + Ax.x * (x - xa), ay = ry + Ax.y * (x - xa), az = rz + Ax.z * (x - xa);
                    // where the line is inside the slab's box
                    float lo = float.NegativeInfinity, hi = Inf, t0, t1;
                    if (fx) { if (ax < 0f || ax > W) continue; }
                    else { t0 = -ax * idx; t1 = (W - ax) * idx; if (t0 > t1) (t0, t1) = (t1, t0); lo = t0; hi = t1; }
                    if (fy) { if (ay < 0f || ay > H) continue; }
                    else { t0 = -ay * idy; t1 = (H - ay) * idy; if (t0 > t1) (t0, t1) = (t1, t0); if (t0 > lo) lo = t0; if (t1 < hi) hi = t1; }
                    if (fz) { if (az < -hmax || az > hmax) continue; }
                    else { t0 = (-hmax - az) * idz; t1 = (hmax - az) * idz; if (t0 > t1) (t0, t1) = (t1, t0); if (t0 > lo) lo = t0; if (t1 < hi) hi = t1; }
                    if (lo > hi) continue;
                    // walk the texel columns it crosses (Amanatides-Woo), nearest first
                    float s = lo + 1e-4f;
                    int i = Mathf.Clamp((int)Mathf.Floor(ax + dx * s), 0, W - 1), j = Mathf.Clamp((int)Mathf.Floor(ay + dy * s), 0, H - 1);
                    float nx = fx ? Inf : dx > 0f ? (i + 1 - ax) * idx : (i - ax) * idx;
                    float ny = fy ? Inf : dy > 0f ? (j + 1 - ay) * idy : (j - ay) * idy;
                    float sIn = lo; int entered = -1;      // -1 the first column, 0 entered across x, 1 across y
                    bool hit = false; float sHit = 0f; bool wall = false; int hi_ = 0, hj = 0, side = 0;
                    while (i >= 0 && j >= 0 && i < W && j < H && sIn <= hi)
                    {
                        if (i < rowLo[j] || i > rowHi[j])
                        {
                            // clear here: jump to where the line meets this row's solid run, or to the next row
                            float sRun = Inf;
                            if (si > 0 && i < rowLo[j] && !fx) sRun = (rowLo[j] - ax) * idx;
                            else if (si < 0 && i > rowHi[j] && rowHi[j] >= 0 && !fx) sRun = (rowHi[j] + 1 - ax) * idx;
                            if (sRun < ny && sRun <= hi)
                            {
                                i = si > 0 ? rowLo[j] : rowHi[j]; sIn = sRun; entered = 0;
                                nx = si > 0 ? (i + 1 - ax) * idx : (i - ax) * idx;
                                continue;
                            }
                            if (ny > hi) break;
                            j += sj; sIn = ny; ny += tdy; entered = 1;
                            if (!fx)
                            {
                                i = (int)Mathf.Floor(ax + dx * sIn);
                                nx = dx > 0f ? (i + 1 - ax) * idx : (i - ax) * idx;
                            }
                            continue;
                        }
                        float sOut = nx < ny ? nx : ny; if (hi < sOut) sOut = hi;
                        float h = half[j * W + i];
                        if (h > 0f)
                        {
                            // where in this column the line is within its depth
                            float a0, a1;
                            if (fz) { if (az > h || az < -h) { a0 = 1f; a1 = 0f; } else { a0 = float.NegativeInfinity; a1 = Inf; } }
                            else { a0 = (-h - az) * idz; a1 = (h - az) * idz; if (a0 > a1) (a0, a1) = (a1, a0); }
                            float e0 = sIn > a0 ? sIn : a0, e1 = sOut < a1 ? sOut : a1;
                            if (e0 <= e1)
                            {
                                hit = true; sHit = e0; hi_ = i; hj = j; wall = entered >= 0 && a0 <= sIn;
                                side = entered; break;
                            }
                        }
                        if (nx < ny) { i += si; sIn = nx; nx += tdx; entered = 0; } else { j += sj; sIn = ny; ny += tdy; entered = 1; }
                    }
                    if (!hit) continue;
                    DrawSlabHit(art, slab, x, y, -sHit, wall, side, si, sj, hi_, hj, az + dz * sHit, Uh, Vh, N, uLen, vLen, id);
                }
            }
            DrawSlabOutline(slab, id);
            return true;
        }

        /// <summary>One canvas texel of a slab: the face (the art, shaded by its slope) or a wall (the part's colour).</summary>
        static void DrawSlabHit(Weapon8.Art art, Weapon8.Slab slab, int x, int y, float z, bool wall, int side, int si, int sj,
            int hi_, int hj, float wz, Vector3 Uh, Vector3 Vh, Vector3 N, float uLen, float vLen, int id)
        {
            int px = y * _W + x;
            if (z <= _z[px]) return;     // behind what is drawn: skip the shading
            int W = art.W, H = art.H;
            int k = hj * W + hi_;
            Vector3 n;
            if (wall) n = side == 0 ? -si * Uh : -sj * Vh;
            else
            {
                float sg = wz >= 0f ? 1f : -1f;
                float Hh(int u, int v) => u < 0 || v < 0 || u >= W || v >= H ? 0f : slab.Half[v * W + u];
                float gx = (Hh(hi_ + 1, hj) - Hh(hi_ - 1, hj)) * 0.5f / uLen, gy = (Hh(hi_, hj + 1) - Hh(hi_, hj - 1)) * 0.5f / vLen;
                n = (Uh * (-gx * sg * 0.6f) + Vh * (-gy * sg * 0.6f) + N * sg).normalized;
                wall = Mathf.Abs(Vector3.Dot(n, N)) < 0.6f;
            }
            Color32 c = wall ? slab.Wall[k] : art.Px[k];
            float f = Mathf.Abs(Vector3.Dot(n, N)) > 0.99f && !wall ? 1f : 1f + 0.55f * (Vector3.Dot(n, SlabLight) - SlabLight.z);
            f = Mathf.Round(Mathf.Clamp(f, 0.6f, 1.2f) * 10f) / 10f;
            Color col = c;
            col = f <= 1f ? new Color(col.r * f, col.g * f, col.b * f, 1f) : Color.Lerp(col, Color.white, (f - 1f) * 1.2f);
            col.a = 1f;
            Put(x, y, z, col, id);
            if (!SameRgb(c, slab.Outline)) _slabPx.Add(px);
        }

        /// <summary>The outline the walls need (a face's edge already carries the art's own).</summary>
        static void DrawSlabOutline(Weapon8.Slab slab, int id)
        {
            Color oc = slab.Outline;
            foreach (int px in _slabPx)
            {
                if (_owner[px] != id) continue;
                int x = px % _W, y = px / _W; float z = _z[px] - 0.05f;
                if (x > 0 && _owner[px - 1] != id) Put(x - 1, y, z, oc, id);
                if (x < _W - 1 && _owner[px + 1] != id) Put(x + 1, y, z, oc, id);
                if (y > 0 && _owner[px - _W] != id) Put(x, y - 1, z, oc, id);
                if (y < _H - 1 && _owner[px + _W] != id) Put(x, y + 1, z, oc, id);
            }
        }

        static bool SameRgb(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 24;
    }
}
