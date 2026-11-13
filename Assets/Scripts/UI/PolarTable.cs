using UnityEngine;

namespace Convergence.UI
{
    /// <summary>
    /// The distance and angle from a round widget's centre to each texel's centre, worked out once a size. The seal,
    /// the health crescents and the rotation wheel redraw their pictures every frame and read these instead of a
    /// square root and an arctangent a texel. The values are the widgets' own expressions (distance in texels; the
    /// angle in degrees as Atan2(dx, dy), 0 at the top, -180..180), so the pictures are unchanged. A margin round the
    /// square covers the seal's shake: it moves by whole texels, so a shaken texel's values are its neighbour's.
    /// </summary>
    static class PolarTable
    {
        public const int Margin = 8;

        public sealed class Table
        {
            public int Size, Stride;
            public float[] D, A;
            /// <summary>The index of texel (x, y), each -Margin .. Size + Margin - 1.</summary>
            public int At(int x, int y) => (y + Margin) * Stride + x + Margin;
        }

        static Table _a, _b;

        public static Table Of(int size)
        {
            if (_a != null && _a.Size == size) return _a;
            if (_b != null && _b.Size == size) return _b;
            var t = Build(size);
            _b = _a; _a = t;
            return t;
        }

        static Table Build(int size)
        {
            int stride = size + 2 * Margin;
            var t = new Table { Size = size, Stride = stride, D = new float[stride * stride], A = new float[stride * stride] };
            float centre = size * 0.5f;
            for (int y = -Margin; y < size + Margin; y++)
            for (int x = -Margin; x < size + Margin; x++)
            {
                float dx = x + 0.5f - centre, dy = y + 0.5f - centre;
                int k = t.At(x, y);
                t.D[k] = Mathf.Sqrt(dx * dx + dy * dy);
                t.A[k] = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
            }
            return t;
        }
    }
}
