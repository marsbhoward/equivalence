using UnityEngine;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// The boss floor cut into eight slices, and the one place that geometry is defined.
    ///
    /// Eight wedges of 45 degrees about the arena's centre, numbered anticlockwise from due east.
    /// Both halves of the first boss read off this - the teleport ring picks a slice to appear in,
    /// and the hazard phase sets the same slice alight - so a slice the boss appeared in and a
    /// slice that later erupts are provably the same wedge rather than two numbers that happen to
    /// agree.
    ///
    /// THE WEDGES ARE RASTERISED PER SECTOR, CLIPPED TO THE ARENA. The obvious build - one pie
    /// slice sprite, rotated eight times - cannot work here, because the arena is a 24x14 RECTANGLE
    /// and a wedge is a circle's idea. Sized to fit inside it, the four corners are never covered
    /// and a player standing in one is safe from a slice they are plainly inside; sized to reach
    /// the corners (a circumradius of 13.9) it paints over the walls and out across the nine units
    /// of ground the camera needs drawn past them. Testing angle and rectangle together per texel
    /// gives the exact shape with no clipping pass and no mesh, and the eight textures are built
    /// once for the process.
    ///
    /// The angular test is the AUTHORITY and the texture merely draws it: a hazard asks
    /// <see cref="IndexAt"/>, never the sprite, so what damages the player cannot drift from what
    /// is painted on the floor.
    /// </summary>
    public static class ArenaSectors
    {
        public const int Count = 8;
        public const float Arc = 360f / Count;

        /// <summary>Texels across the arena's full width. 256 is about 10 per world unit, which is
        /// far finer than the angular edges need - they are feathered in the raster anyway - and
        /// the eight together are under a megabyte.</summary>
        const int RasterWidth = 256;

        static Sprite[] _sprites;
        static Vector2 _builtFor;

        /// <summary>Which slice a world point falls in. Sector 0 is centred on due east.</summary>
        public static int IndexAt(Vector2 world)
        {
            float deg = Mathf.Atan2(world.y, world.x) * Mathf.Rad2Deg + Arc * 0.5f;
            if (deg < 0f) deg += 360f;
            return Mathf.FloorToInt(deg / Arc) % Count;
        }

        /// <summary>The middle of a slice, as a unit vector.</summary>
        public static Vector2 Direction(int sector)
        {
            float rad = sector * Arc * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }

        /// <summary>
        /// Where the boss stands when it appears in a slice.
        ///
        /// One radius for every slice, so the ring it teleports around is a circle rather than
        /// something that follows the room's own proportions - the pattern is the thing being
        /// read, and a boss that appeared further away on the long axis would make the same note
        /// look different depending which one it was.
        /// </summary>
        public static Vector2 Station(int sector)
            => Direction(sector) * Tuning.Boss.TeleportRadius;

        /// <summary>
        /// One sprite per slice, each already clipped to the arena and each covering the arena
        /// exactly - so a renderer using it sits at the origin at the arena's own size and needs
        /// no rotation. Rebuilt if the arena is ever resized.
        /// </summary>
        public static Sprite Shape(int sector)
        {
            var half = Arena.HalfExtents;
            if (_sprites == null || _builtFor != half) Raster(half);
            return _sprites[((sector % Count) + Count) % Count];
        }

        static void Raster(Vector2 half)
        {
            _sprites = new Sprite[Count];
            _builtFor = half;

            int w = RasterWidth;
            int h = Mathf.Max(8, Mathf.RoundToInt(w * half.y / half.x));
            float ppu = w / (half.x * 2f);

            for (int s = 0; s < Count; s++)
            {
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                var px = new Color[w * h];
                float mid = s * Arc;

                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // Texel centres, mapped to world offsets from the arena's middle.
                    float wx = (x + 0.5f) / w * half.x * 2f - half.x;
                    float wy = (y + 0.5f) / h * half.y * 2f - half.y;

                    float deg = Mathf.Atan2(wy, wx) * Mathf.Rad2Deg;
                    float off = Mathf.Abs(Mathf.DeltaAngle(mid, deg));

                    // Feathered on the ANGLE rather than on distance, because the edge that has to
                    // read cleanly is the one between two slices. A radial fade would also make the
                    // middle of the room - where the stun happens - the faintest part of the mark.
                    float a = Mathf.Clamp01((Arc * 0.5f - off) / EdgeFeatherDegrees);

                    // The very centre has no meaningful angle: every slice meets there, so all
                    // eight would fight over the same texels and the middle would read as a bright
                    // knot. Faded out inside a small hub instead, which also keeps the stun spot
                    // visually clear of the pattern.
                    float r = Mathf.Sqrt(wx * wx + wy * wy);
                    a *= Mathf.Clamp01((r - HubRadius) / HubFade);

                    px[y * w + x] = new Color(1f, 1f, 1f, a);
                }

                tex.SetPixels(px);
                tex.Apply(false, true);
                _sprites[s] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
            }
        }

        /// <summary>How wide the soft edge between two slices is, in degrees.</summary>
        const float EdgeFeatherDegrees = 1.2f;

        /// <summary>Radius of the clear hub at the centre, and how far it fades over.</summary>
        const float HubRadius = 1.1f;
        const float HubFade = 0.9f;
    }
}
