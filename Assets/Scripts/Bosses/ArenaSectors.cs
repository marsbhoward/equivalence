using UnityEngine;
using Convergence.Core;

namespace Convergence.Bosses
{
    /// <summary>
    /// The boss floor cut into eight slices, and the one place that geometry is defined.
    ///
    /// Eight wedges of 45 degrees about the arena's centre, numbered anticlockwise from due east.
    /// Both halves of the Wreathe's fight read off this - the call picks a slice to appear in, and
    /// the answer is stood in the same slice - so the slice it sang from and the slice that is safe
    /// are provably the same wedge rather than two numbers that happen to agree.
    ///
    /// The arena is a 24x14 RECTANGLE and a wedge is a circle's idea, so a slice is the ANGLE alone,
    /// out to the walls - the corners belong to the slice they are in. The Wreathe's floor
    /// (WreatheFloor) draws them texel by texel from <see cref="ZoneAt"/>.
    ///
    /// The angular test is the AUTHORITY and the floor merely draws it: the beat asks
    /// <see cref="ZoneAt"/>, never the picture, so what damages the player cannot drift from what
    /// is painted on the floor.
    /// </summary>
    public static class ArenaSectors
    {
        public const int Count = 8;
        public const float Arc = 360f / Count;

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
        {
            var d = Direction(sector);
            // The northern notes come in a little, so the held view shows the whole figure under
            // the boss bar (Tuning.Boss.StationMaxNorth) - still in the slice, the angle unchanged.
            float r = d.y > 0.01f ? Mathf.Min(Tuning.Boss.TeleportRadius, Tuning.Boss.StationMaxNorth / d.y) : Tuning.Boss.TeleportRadius;
            return d * r;
        }

        // ------------------------------------------------------------------ the stepped answer's zones

        /// <summary>
        /// Which ZONE a world point is in, for the Wreathe's stepped answer (the user's rules,
        /// 2026-10-26): -1 is the SAFE CENTRE (inside <see cref="Tuning.Boss.SafeRadius"/>), 0-7 the
        /// slices. SPLIT (enraged) cuts every slice in two by a ring at
        /// <see cref="Tuning.Boss.InnerRadius"/>: the OUTER half keeps the slice's number, the INNER
        /// half is the number + 8 - so a zone's number is also its sung note (8-15 an octave down).
        /// The ONE authority: the floor is drawn from it, the boss stands by it, the beat tests it.
        /// </summary>
        public static int ZoneAt(Vector2 world, bool split)
        {
            float r = world.magnitude;
            if (r < Tuning.Boss.SafeRadius) return -1;
            int s = IndexAt(world);
            return split && r < Tuning.Boss.InnerRadius ? s + Count : s;
        }

        /// <summary>Where the boss stands to sing a zone: an outer zone (or a whole slice) at
        /// <see cref="Station"/>, an inner one between the safe centre and the dividing ring.</summary>
        public static Vector2 ZoneStation(int zone)
            => zone >= Count ? Direction(zone - Count) * Tuning.Boss.InnerStation : Station(zone);
    }
}
