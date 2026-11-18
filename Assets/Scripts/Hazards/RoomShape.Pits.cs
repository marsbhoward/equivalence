namespace Convergence.Hazards
{
    /// <summary>
    /// What the pit pieces (Hazards.PitPieces) need of a room: a piece may carry its own wall,
    /// cracked wall or chasm, and the chasm stones lay islands (<c>s</c>: floor no foot reaches), so
    /// the room it is laid into is rebuilt with them before its walls go up. Everything else about
    /// the room - its name, landmarks, dais, tiers, lake - is kept.
    /// </summary>
    public sealed partial class RoomShape
    {
        /// <summary>The room's own check (<see cref="Validate"/>) - null when the grid is a room.</summary>
        public static string Check(string[] grid) => Validate(grid);

        /// <summary>This room with its grid replaced by <paramref name="grid"/>. A room that gains its
        /// first chasm rolls drop or lake as any chasm room does (<see cref="Styled"/>).</summary>
        public RoomShape Reshaped(string[] grid, System.Random rng)
        {
            var shape = new RoomShape(Name, grid, Lake, Landmarks, Dais, Tiers);
            return HasChasm || !shape.HasChasm ? shape : Styled(shape, rng);
        }

        /// <summary>An open floor given a shape by its pit pieces (a wall a trough runs along, the
        /// chasm stones).</summary>
        public static RoomShape Opened(string[] grid, System.Random rng)
        {
            var shape = new RoomShape("open", grid);
            return Styled(shape, rng);
        }

        /// <summary>Whether a cell is an ISLAND - floor laid by the chasm stones that only a dash
        /// reaches (RoomWalls publishes them so nothing arrives on one).</summary>
        public bool IslandAt(int col, int rowFromTop) => Grid[rowFromTop][col] == 's';
    }
}
