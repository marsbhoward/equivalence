using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// The PLATFORM floors' rooms (Core.GameBootstrap's rounds; the user's calls 2026-11-13 on the
    /// floor-height mock): a floor whose wave is split into rounds fights each on its own ground.
    ///
    ///   BRIDGE  two platforms across a chasm - the arrival's to the south, the door's to the north.
    ///           Clearing round one raises a bridge over the gap (RoomWalls.RaiseBridge).
    ///   LIFT    round one on a lift hanging in the place's void; clearing it lowers the lift to a
    ///           ledge where round two waits (RoomWalls.DockLift).
    ///   GATES   the terraces, their stairs sealed until round two (RoomShape.Tiered "gates").
    ///
    /// Built whole, NOT validated as other rooms are: a platform is unreachable on foot until its
    /// round comes - that is the point of it.
    /// </summary>
    public sealed partial class RoomShape
    {
        /// <summary>The bridge's gap: these grid rows (north first), the bridge across these columns.</summary>
        public const int BridgeGapRow0 = 5, BridgeGapRows = 2, BridgeCol0 = 11, BridgeCols = 2;
        /// <summary>The lift's cells (north first): columns and rows, inclusive start, count.</summary>
        public const int LiftCol0 = 5, LiftCols = 14, LiftRow0 = 3, LiftRows = 10;

        /// <summary>The bridge floor: the door's platform north, the arrival's south, a drop between.</summary>
        public static RoomShape Bridge()
        {
            var g = new string[Rows];
            for (int r = 0; r < Rows; r++)
                g[r] = r >= BridgeGapRow0 && r < BridgeGapRow0 + BridgeGapRows ? new string('~', Cols) : new string('.', Cols);
            return new RoomShape("bridge", g);
        }

        /// <summary>The lift floor: the lift alone, everything round it wall that opens into the
        /// void (RoomWalls.OpenEdges) until the lift docks.</summary>
        public static RoomShape Lift()
        {
            var g = new string[Rows];
            for (int r = 0; r < Rows; r++)
            {
                var row = new char[Cols];
                for (int c = 0; c < Cols; c++)
                    row[c] = r >= LiftRow0 && r < LiftRow0 + LiftRows && c >= LiftCol0 && c < LiftCol0 + LiftCols ? '.' : '#';
                g[r] = new string(row);
            }
            return new RoomShape("lift", g);
        }

        /// <summary>The gates floor: the terraces with their stairs sealed.</summary>
        public static RoomShape Gates(System.Random rng) => Tiered(rng, "gates");

        /// <summary>World rect of the bridge floor's northern platform (round two's) and southern
        /// one (round one's), and of the lift - for where each round's bodies arrive.</summary>
        public static UnityEngine.Rect NorthPlatform
            => CellsRect(0, Cols, 0, BridgeGapRow0);
        public static UnityEngine.Rect SouthPlatform
            => CellsRect(0, Cols, BridgeGapRow0 + BridgeGapRows, Rows - BridgeGapRow0 - BridgeGapRows);
        public static UnityEngine.Rect LiftRect
            => CellsRect(LiftCol0, LiftCols, LiftRow0, LiftRows);

        /// <summary>A block of cells (north-first rows) as a world rect.</summary>
        static UnityEngine.Rect CellsRect(int c0, int cols, int r0, int rows)
        {
            var half = Arena.HalfExtents;
            float cw = half.x * 2f / Cols, ch = half.y * 2f / Rows;
            float x0 = -half.x + c0 * cw, yTop = half.y - r0 * ch;
            return new UnityEngine.Rect(x0, yTop - rows * ch, cols * cw, rows * ch);
        }
    }
}
