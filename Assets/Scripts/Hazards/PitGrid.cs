using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>Which hazard fills a pit. Size and kind are independent rolls. Append only.</summary>
    public enum PitKind { Fire, Sand, Spike, Water }

    /// <summary>
    /// Footprint tiers, in GRID CELLS rather than world units - see <see cref="PitGrid"/> for why
    /// that distinction is the whole point.
    /// </summary>
    public enum PitSize { Small, Medium, Large }

    /// <summary>
    /// Where on the floor a pit is allowed to sit. The count guidance this was built to - at most
    /// one Large, two Medium (one per half), four Small (one per quadrant) - is expressed as
    /// REGIONS rather than as a bare number, because "two mediums" and "one in each half" are two
    /// different requirements and only the second of them stops both landing on top of each other.
    /// </summary>
    public enum PitRegion { Centre, WestHalf, EastHalf, NorthWest, NorthEast, SouthWest, SouthEast }

    /// <summary>
    /// The lattice pits are laid out on, and the one reason they can never overlap a column.
    ///
    /// COLUMNS SIT ON LATTICE POINTS; PITS OCCUPY THE CELLS BETWEEN THEM. That is the entire
    /// design. A pit measured in cells is bounded by four column points at its corners and
    /// contains none in its interior unless it is more than one cell across - and the points it
    /// does swallow are removed from the column pool before a single column is placed, so
    /// non-overlap is STRUCTURAL rather than a clearance check that can be got wrong. The old
    /// free-placed lava tile needed <see cref="HazardBuilder"/>'s own retry loop for exactly the
    /// job this removes.
    ///
    /// Cell centres are the column lattice offset by half a spacing on both axes, so the two
    /// grids are the same grid read two ways and neither can drift from the other if
    /// <see cref="Tuning.Hazards.ColumnGridSpacing"/> is ever retuned.
    /// </summary>
    public static class PitGrid
    {
        /// <summary>A pit's footprint in cells. Width first, in the arena's own x/y.</summary>
        public static Vector2Int CellsFor(PitSize size) => size switch
        {
            PitSize.Small => new Vector2Int(1, 1),
            PitSize.Medium => new Vector2Int(2, 1),
            _ => new Vector2Int(2, 2),
        };

        /// <summary>
        /// The drawn rect for a pit of this size in this region, or false if there is nowhere in
        /// that region the footprint both fits and clears the player - which a Large pit in a
        /// short arena genuinely can hit, and is why this reports rather than clamping. A clamped
        /// Large would silently become a Medium sitting off its own grid.
        ///
        /// THE PLAYER CLEARANCE IS CHECKED HERE, AGAINST EVERY CANDIDATE CELL, and doing it
        /// downstream instead was a real bug rather than a tidiness question. The player spawns at
        /// the world origin, so a Large pit centred on the arena is centred on the player - a
        /// caller that placed the rect first and rejected it afterwards rejected it EVERY TIME,
        /// and the Large layout silently never appeared at all. Filtering candidates first lets
        /// the region degrade to the next cell out, which is the same graceful fallback the Large
        /// COLUMN's own "nearest eligible point" already relies on.
        ///
        /// Clearance is measured to the RECT, not to its centre. Measured centre-to-centre with
        /// the footprint's own half-span added, a 5-unit-wide Medium needs its middle 3.9 units
        /// from the player however it is oriented - so Mediums whose near edge was comfortably
        /// clear were being thrown away for being long.
        /// </summary>
        public static bool TryRect(PitRegion region, PitSize size, Vector2 halfExtents,
                                   Vector2 playerPos, out Rect rect)
        {
            rect = default;
            float spacing = Tuning.Hazards.ColumnGridSpacing;
            var cells = CellsFor(size);

            // Cell INDEX range: cell i spans lattice points i and i+1, so the usable indices are
            // one short of the point range on each side.
            float margin = Tuning.Hazards.ColumnGridWallMargin;
            int stepsX = Mathf.FloorToInt((halfExtents.x - margin) / spacing);
            int stepsY = Mathf.FloorToInt((halfExtents.y - margin) / spacing);
            if (stepsX < 1 || stepsY < 1) return false;

            // Cells are indexed by their LOW corner's lattice index: [-stepsX, stepsX-1].
            int loX = -stepsX, hiX = stepsX - 1;
            int loY = -stepsY, hiY = stepsY - 1;

            // The region narrows that range before the size is placed inside it, so a WestHalf
            // pit is west whatever size it rolled.
            switch (region)
            {
                case PitRegion.WestHalf: hiX = -1; break;
                case PitRegion.EastHalf: loX = 0; break;
                case PitRegion.NorthWest: hiX = -1; loY = 0; break;
                case PitRegion.NorthEast: loX = 0; loY = 0; break;
                case PitRegion.SouthWest: hiX = -1; hiY = -1; break;
                case PitRegion.SouthEast: loX = 0; hiY = -1; break;
            }

            // Room for the footprint's own width inside the narrowed range.
            if (hiX - cells.x + 1 < loX || hiY - cells.y + 1 < loY) return false;

            // Every cell origin the footprint fits at, filtered to those that leave the player
            // room to stand. Built as a list rather than rolled directly so "nowhere works" is a
            // distinguishable answer from "the one spot we picked happened to be bad".
            var candidates = new List<Rect>();
            for (int cx = loX; cx <= hiX - cells.x + 1; cx++)
            for (int cy = loY; cy <= hiY - cells.y + 1; cy++)
            {
                var r = RectAt(cx, cy, cells, spacing);
                if (DistanceTo(r, playerPos) < Tuning.Hazards.HazardClearance) continue;
                // A pit REPLACES floor; there is none under a wall. Kept a little off it too, or
                // the pit's sunken lip would draw against the wall's face.
                if (!Core.Arena.RectOnFloor(r, 0.25f)) continue;
                candidates.Add(r);
            }
            if (candidates.Count == 0) return false;

            if (region == PitRegion.Centre)
            {
                // The one region that is CHOSEN rather than rolled: a Large pit is the floor's
                // centrepiece, so it takes the most central spot still available - which, with
                // the player standing on the true centre, is the first ring out rather than
                // nothing at all.
                rect = candidates[0];
                float best = rect.center.sqrMagnitude;
                for (int i = 1; i < candidates.Count; i++)
                {
                    float d = candidates[i].center.sqrMagnitude;
                    if (d < best) { best = d; rect = candidates[i]; }
                }
                return true;
            }

            rect = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        /// <summary>
        /// The drawn rect for a footprint at a cell origin. Inset from the cell block so the pit's
        /// own edge never lands exactly on the lattice line a corner column is centred on - the
        /// two read as adjacent rather than as one clipping the other.
        /// </summary>
        static Rect RectAt(int cx, int cy, Vector2Int cells, float spacing)
        {
            float inset = Tuning.Hazards.PitCellInset;
            return new Rect(cx * spacing + inset, cy * spacing + inset,
                            cells.x * spacing - inset * 2f, cells.y * spacing - inset * 2f);
        }

        /// <summary>Distance from a point to a rect's nearest edge, 0 if inside it.</summary>
        static float DistanceTo(Rect r, Vector2 p)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Every lattice point a pit's footprint makes unusable - measured as distance to the
        /// RECT rather than as "inside it", because a Large column's own radius (1.05) is nearly
        /// half the grid spacing, so a column merely OUTSIDE a pit can still overhang it by most
        /// of its body. Distance-to-rect covers both cases with one rule and needs no per-size
        /// arithmetic.
        /// </summary>
        public static bool Blocks(Rect rect, Vector2 point)
        {
            return DistanceTo(rect, point) < Tuning.Hazards.ColumnRadiusLarge + Tuning.Hazards.PitColumnMargin;
        }

        /// <summary>The regions a layout of this size uses, in the order they are filled.</summary>
        public static IReadOnlyList<PitRegion> RegionsFor(PitSize size) => size switch
        {
            PitSize.Large => LargeRegions,
            PitSize.Medium => MediumRegions,
            _ => SmallRegions,
        };

        static readonly PitRegion[] LargeRegions = { PitRegion.Centre };
        static readonly PitRegion[] MediumRegions = { PitRegion.WestHalf, PitRegion.EastHalf };
        static readonly PitRegion[] SmallRegions =
            { PitRegion.NorthWest, PitRegion.NorthEast, PitRegion.SouthWest, PitRegion.SouthEast };
    }
}
