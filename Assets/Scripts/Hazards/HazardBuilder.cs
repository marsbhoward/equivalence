using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// Per-floor room-layout generation - the Hazards counterpart to EnemyFactory. Procedural
    /// rather than hand-authored templates (confirmed with the user): every floor rolls a fresh
    /// layout from the counts/spacing in Tuning.Hazards.
    /// </summary>
    public static class HazardBuilder
    {
        /// <summary>
        /// Builds the floor's room. <paramref name="spireAllowed"/> is the caller's say on whether
        /// this floor may roll a spire at all (never a boss floor); <paramref name="hurt"/> and
        /// <paramref name="worn"/> weight which boon it rolls. <paramref name="stormAllowed"/> is
        /// the same say for a tornado storm. Returns the spire, or null.
        /// </summary>
        /// <summary>The transmutation circle the last <see cref="Populate"/> drew, or null.</summary>
        public static TransmutationRing LastRing { get; private set; }

        public static Spire Populate(int floor, Vector2 halfExtents, Transform player, Transform parent,
                                     bool spireAllowed = false, bool hurt = false, bool worn = false,
                                     bool stormAllowed = false, bool circle = false)
        {
            LastRing = null;
            var playerPos = player != null ? (Vector2)player.position : Vector2.zero;
            var columns = new List<Column>();

            // Cleared explicitly rather than left to each outgoing pit's OnDestroy: Destroy is
            // deferred to end of frame, so last floor's pits would still be answering movement
            // queries for the rest of the frame this one is built on.
            FloorPits.Clear();
            // A new room: the enemy nav grid re-scans before the first enemy walks it.
            Enemies.NavField.MarkDirty();

            // ---- Floor pits FIRST, because the columns have to give way to them ----
            //
            // Ordering, not preference. Pits sit on the CELLS of the same lattice the columns sit
            // on the POINTS of, so a pit swallows the points inside its own footprint - and the
            // only way for that to be structural rather than a clearance check is for the pits to
            // exist before a single column is placed. Done the other way round the builder would
            // be back to retrying placements against each other, which is exactly what the old
            // free-placed lava tile needed and what the grid exists to delete.
            var pits = PlacePits(floor, halfExtents, playerPos, player, parent);

            // ---- The spire, second, so the columns give way to its ring. Pits do NOT - a
            // spire may stand in one, and the pit's hazard is one more price of the capture ----
            Spire spire = null;

            // A TRANSMUTATION CIRCLE, when the run's ledger is owed one this floor - placed like a
            // spire's ring, and a circle floor never also has a spire (the two rings would argue).
            if (circle)
            {
                LastRing = PlaceRing(halfExtents, playerPos, player, parent);
                spireAllowed = false;
            }

            if (spireAllowed && (Spire.DevForceNext ||
                                 (floor >= Tuning.Spire.FromFloor && Random.value < Tuning.Spire.FloorChance)))
            {
                Spire.DevForceNext = false;
                spire = PlaceSpire(floor, halfExtents, playerPos, player, parent, hurt, worn);
            }

            // Whether this floor has any columns at all is its own roll, made before any size or
            // count logic runs - a floor that's just open ground is worth more than the grid
            // always showing up in some quantity. Force fields only ever anchor to columns, so
            // skipping this skips them too for free; the floor pits are unaffected, being placed
            // above this on their own roll.
            if (Random.value < Tuning.Hazards.ColumnsFloorChance)
            {
                // Columns snap to a fixed lattice rather than landing anywhere - a room reads as
                // built rather than scattered, and a force field between two lattice points reads
                // as a designed wall segment instead of an arbitrary diagonal beam. Points already
                // too close to the player are filtered out here, once, rather than retried per
                // column.
                var grid = BuildColumnGrid(halfExtents, playerPos, pits, spire, LastRing);

                // ---- Large: at most one, whichever eligible point sits nearest true center ----
                //
                // The player spawns at the world origin every run, which is exactly where a
                // literal center-lock would place this - BuildColumnGrid's own player-clearance
                // filter has already dropped that point (and its close neighbours) by the time
                // this runs, so "nearest eligible point" degrades gracefully to the next ring out
                // instead of ever needing a special case for it.
                float largeChance = Mathf.Clamp01(Tuning.Hazards.LargeColumnBaseChance + floor * 0.02f);
                if (Random.value < largeChance && grid.Count > 0)
                {
                    grid.Sort((a, b) => a.sqrMagnitude.CompareTo(b.sqrMagnitude));
                    var pos = grid[0];
                    grid.RemoveAt(0);
                    columns.Add(Column.Spawn(pos, ColumnSize.Large, RollCracked(), parent));
                }

                // Everything after Large draws from the same pool at random - sorted-by-distance
                // would otherwise cluster every Medium and Small near center too, just because
                // that's how the list was left.
                Shuffle(grid);

                int mediumCount = Mathf.Min(Tuning.Hazards.MaxMediumColumns, 1 + floor / 3);
                for (int i = 0; i < mediumCount && grid.Count > 0; i++)
                {
                    var pos = PopLast(grid);
                    columns.Add(Column.Spawn(pos, ColumnSize.Medium, RollCracked(), parent));
                }

                int smallCount = Mathf.Min(Tuning.Hazards.MaxSmallColumns, 2 + floor / 2);
                for (int i = 0; i < smallCount && grid.Count > 0; i++)
                {
                    var pos = PopLast(grid);
                    columns.Add(Column.Spawn(pos, ColumnSize.Small, RollCracked(), parent));
                }
            }

            // ---- Force fields: between NEIGHBOURING columns, each eligible pair rolling alone ----
            //
            // A pair is eligible when the two stand on the same lattice row or column (STRAIGHT -
            // everything else in the room is horizontal or vertical, and a diagonal read as an
            // arbitrary beam that stair-stepped on the pixel grid) and nothing stands between
            // them: they are each other's NEAREST column that way. So a field never runs through
            // a third pillar or doubles back over another on the same line, and a column can carry
            // one field in each of its four directions - an L, a T, a cross - since every pair
            // rolls on its own. Nothing is owed: two columns lined up usually have NO field (the
            // chance per pair, and the floor's cap). Shuffled before rolling, so the cap does not
            // always go to whichever columns happened to be placed first.
            var pairs = new List<(Column A, Column B)>();
            for (int i = 0; i < columns.Count; i++)
            for (int j = i + 1; j < columns.Count; j++)
            {
                Vector2 a = columns[i].transform.position, b = columns[j].transform.position;
                float d = Vector2.Distance(a, b);
                if (d < Tuning.Hazards.ForceFieldMinSpan || d > Tuning.Hazards.ForceFieldMaxSpan) continue;
                bool sameColumn = Mathf.Abs(a.x - b.x) < 0.01f, sameRow = Mathf.Abs(a.y - b.y) < 0.01f;
                if (!sameColumn && !sameRow) continue;
                if (StandsBetween(columns, i, j)) continue;
                if (Arena.WallBetween(a, b)) continue;
                // A field strung across the ring would be a wall through the capture - the
                // bubble is that challenge, and it is meant to be the only one of its kind.
                if (spire != null && SegmentDistance(spire.Centre, a, b) < Tuning.Spire.CaptureRadius) continue;
                pairs.Add((columns[i], columns[j]));
            }
            for (int i = pairs.Count - 1; i > 0; i--)
            {
                int k = Random.Range(0, i + 1);
                (pairs[i], pairs[k]) = (pairs[k], pairs[i]);
            }

            int fieldsPlaced = 0;
            foreach (var (a, b) in pairs)
            {
                if (fieldsPlaced >= Tuning.Hazards.MaxForceFieldsPerFloor) break;
                if (Random.value > Tuning.Hazards.ForceFieldChancePerEligiblePair) continue;
                var charge = Random.value < 0.5f ? FieldColor.Blue : FieldColor.Red;
                bool flipping = Random.value < Tuning.Hazards.ForceFieldFlipChance;
                ForceField.Spawn(a, b, charge, flipping, parent);
                fieldsPlaced++;
            }

            // ---- The storm, last: it places nothing now. Its funnels form during the fight and
            // ask the finished room where open floor is (Tornado.Open) ----
            if (stormAllowed && TornadoStorm.Rolls(floor))
                TornadoStorm.Spawn(floor, player, parent);

            return spire;
        }

        // ---- the spire ----

        /// <summary>
        /// Free placement, not the lattice: the ring is a circle, and a circle on a cell would
        /// only ever line up with it by accident. The RING (not the obelisk) keeps clear of the
        /// player's spawn and both doors, so the capture is never something the player arrives
        /// standing in. Pits are fair ground: a ring over fire, sand or jacks is a harder capture,
        /// not a broken one. Gives up quietly if the room has no room.
        /// </summary>
        static Spire PlaceSpire(int floor, Vector2 halfExtents, Vector2 playerPos,
                                Transform player, Transform parent, bool hurt, bool worn)
        {
            float r = Tuning.Spire.CaptureRadius;
            float clear = r + Tuning.Spire.Clearance;
            float mx = halfExtents.x - r - 0.6f, my = halfExtents.y - r - 0.6f;
            if (mx <= 0f || my <= 0f) return null;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                var p = new Vector2(Random.Range(-mx, mx), Random.Range(-my, my));
                if (Vector2.Distance(p, playerPos) < clear + 0.6f) continue;
                if (Vector2.Distance(p, Arena.NorthDoorPoint) < clear) continue;
                if (Vector2.Distance(p, Arena.SouthSpawnPoint) < clear) continue;
                // The whole ring on open floor - a capture half inside a wall is half a capture.
                if (!Arena.OnFloor(p, r + 0.3f)) continue;

                var boon = Spire.DevForceBoon ?? SpireBoons.Roll(hurt, worn);
                int lines = Spire.DevForceLines >= 0 ? Spire.DevForceLines : RollLines(floor);
                bool bubble = Spire.DevForceBubble ?? RollBubble(floor);
                Spire.DevForceBoon = null; Spire.DevForceLines = -1; Spire.DevForceBubble = null;

                float speed = Mathf.Min(Tuning.Spire.LinesMaxSpeed,
                                        Tuning.Spire.LinesBaseSpeed + floor * Tuning.Spire.LinesSpeedPerFloor);
                float damage = Tuning.Spire.LineDamage * Enemies.FloorDifficulty.Damage(floor);

                Debug.Log($"[Spire] floor {floor}: {boon} at {p}, {lines} line(s){(bubble ? ", bubble" : "")}");
                return Spire.Spawn(p, boon, lines, speed, damage, bubble, player, parent);
            }
            return null;
        }

        /// <summary>
        /// The transmutation circle, placed by the spire's rules: the whole ring on open floor, clear
        /// of the player's arrival and both doors, pits fair ground. Gives up quietly if the room has
        /// no room - the Nigredo then waits for the next circle.
        /// </summary>
        static TransmutationRing PlaceRing(Vector2 halfExtents, Vector2 playerPos, Transform player, Transform parent)
        {
            float r = TransmutationRing.Radius;
            float clear = r + Tuning.Spire.Clearance;
            float mx = halfExtents.x - r - 0.6f, my = halfExtents.y - r - 0.6f;
            if (mx <= 0f || my <= 0f) return null;

            for (int attempt = 0; attempt < 40; attempt++)
            {
                var p = new Vector2(Random.Range(-mx, mx), Random.Range(-my, my));
                if (Vector2.Distance(p, playerPos) < clear + 0.6f) continue;
                if (Vector2.Distance(p, Arena.NorthDoorPoint) < clear) continue;
                if (Vector2.Distance(p, Arena.SouthSpawnPoint) < clear) continue;
                if (!Arena.OnFloor(p, r + 0.3f)) continue;
                Debug.Log($"[Circle] transmutation circle at {p}");
                return TransmutationRing.Spawn(p, player, parent);
            }
            return null;
        }

        static int RollLines(int floor)
        {
            float chance = Mathf.Min(Tuning.Spire.LinesMaxChance,
                                     Tuning.Spire.LinesBaseChance + floor * Tuning.Spire.LinesChancePerFloor);
            if (Random.value >= chance) return 0;
            return floor >= Tuning.Spire.LinesThreeFrom ? 3 : floor >= Tuning.Spire.LinesTwoFrom ? 2 : 1;
        }

        static bool RollBubble(int floor)
        {
            if (floor < Tuning.Spire.BubbleFromFloor) return false;
            float chance = Mathf.Min(Tuning.Spire.BubbleMaxChance,
                                     Tuning.Spire.BubbleBaseChance + floor * Tuning.Spire.BubbleChancePerFloor);
            return Random.value < chance;
        }

        /// <summary>Does any OTHER column stand on the straight line between columns i and j -
        /// within its own radius of the segment, between the two ends?</summary>
        static bool StandsBetween(List<Column> columns, int i, int j)
        {
            Vector2 a = columns[i].transform.position, b = columns[j].transform.position;
            for (int k = 0; k < columns.Count; k++)
            {
                if (k == i || k == j) continue;
                Vector2 c = columns[k].transform.position;
                var ab = b - a;
                float t = Vector2.Dot(c - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude);
                if (t <= 0f || t >= 1f) continue;
                if (SegmentDistance(c, a, b) < columns[k].Radius) return true;
            }
            return false;
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        // ---- floor pits ----

        /// <summary>
        /// The floor's pit layout: ONE size tier for the whole floor, laid into that tier's own
        /// regions.
        ///
        /// SIZE IS ROLLED ONCE FOR THE FLOOR AND THE REGIONS COME WITH IT, which is what turns
        /// "at most one large, or two medium, or four small" from a set of caps into an actual
        /// layout. Rolled per pit instead, a floor could land four Larges or one Small, and
        /// nothing would be distributing them - two Mediums that both rolled into the west half
        /// is the failure that makes "one per half" a requirement rather than a count.
        ///
        /// KIND IS THE SEPARATE ROLL, and the floor-25 rule lives entirely in how it is made.
        /// Below that floor one kind is drawn and every pit takes it, so a floor is a fire floor
        /// or a sand floor - a player meets each hazard on its own before meeting it in company.
        /// At and above it each pit rolls independently, so a room can hold all three at once.
        /// </summary>
        static List<Rect> PlacePits(int floor, Vector2 halfExtents, Vector2 playerPos,
                                    Transform player, Transform parent)
        {
            var rects = new List<Rect>();
            if (Random.value > Tuning.Hazards.PitsFloorChance) return rects;

            var size = RollLayoutSize();
            var regions = PitGrid.RegionsFor(size);

            bool mixed = floor >= Tuning.Hazards.PitMixedKindsFromFloor;
            var floorKind = RollKind();

            foreach (var region in regions)
            {
                // Player clearance is PitGrid's own job, checked against every candidate cell
                // rather than against one already-chosen rect - see TryRect for why doing it here
                // instead meant the Large layout never appeared at all.
                if (!PitGrid.TryRect(region, size, halfExtents, playerPos, out var rect)) continue;

                // Overlapping the one already placed cannot happen within a tier - the regions
                // are disjoint - so there is nothing to check here, which is the point of having
                // regions at all.
                var kind = mixed ? RollKind() : floorKind;
                switch (kind)
                {
                    case PitKind.Sand: SandPit.Spawn(rect, player, parent); break;
                    // Pits that hurt hurt like the floor's enemies do - scaled by its depth.
                    case PitKind.Spike: SpikePit.Spawn(rect, player, parent).ScaleDamage(Enemies.FloorDifficulty.Damage(floor)); break;
                    case PitKind.Water: WaterPit.Spawn(rect, player, parent); break;
                    default: FirePit.Spawn(rect, player, parent).ScaleDamage(Enemies.FloorDifficulty.Damage(floor)); break;
                }
                rects.Add(rect);
            }

            return rects;
        }

        static PitSize RollLayoutSize()
        {
            float wl = Tuning.Hazards.PitLayoutWeightLarge;
            float wm = Tuning.Hazards.PitLayoutWeightMedium;
            float ws = Tuning.Hazards.PitLayoutWeightSmall;
            float r = Random.value * (wl + wm + ws);
            if (r < wl) return PitSize.Large;
            return r < wl + wm ? PitSize.Medium : PitSize.Small;
        }

        static PitKind RollKind() => (PitKind)Random.Range(0, PitKinds);

        const int PitKinds = 4;

        /// <summary>
        /// The chance a floor holds at least one pit of a GIVEN kind, from the same rules that place
        /// them: one kind per floor below PitMixedKindsFromFloor, each pit rolling its own after.
        /// A tornado floor is matched to it (TornadoStorm.Rolls), so Air's hazard turns up exactly as
        /// often as Fire's, Earth's and Water's.
        /// </summary>
        public static float PitKindChance(int floor)
        {
            if (floor < Tuning.Hazards.PitMixedKindsFromFloor) return Tuning.Hazards.PitsFloorChance / PitKinds;
            float miss = 1f - 1f / PitKinds;
            float wl = Tuning.Hazards.PitLayoutWeightLarge, wm = Tuning.Hazards.PitLayoutWeightMedium,
                  ws = Tuning.Hazards.PitLayoutWeightSmall;
            float any = (wl * (1f - miss) + wm * (1f - miss * miss) + ws * (1f - Mathf.Pow(miss, 4f))) / (wl + wm + ws);
            return Tuning.Hazards.PitsFloorChance * any;
        }

        static bool RollCracked() => Random.value < Tuning.Hazards.ColumnCrackedChance;

        /// <summary>
        /// Every lattice point a column is allowed to occupy this floor, centered on the arena's
        /// own origin and already filtered to clear the player, the fixed north/south doorway
        /// spots, AND every pit already placed. Spacing is Tuning.Hazards.ColumnGridSpacing - see
        /// that constant for why it composes with the force-field span without retuning either.
        ///
        /// Filtering the pits out HERE, once, rather than checking per column, is what makes
        /// overlap impossible rather than unlikely: a point a pit blocks is never in the pool, so
        /// no amount of shuffling or count scaling downstream can reach it.
        ///
        /// THE FLOOR DOOR IS NOT A HAZARD AND WAS NEVER EXCLUDED - a Large column landing on
        /// Arena.NorthDoorPoint stood directly in front of the exit with nothing else marking that
        /// spot as off limits, so a cleared floor could still strand the player behind their own
        /// pillar. The player's own clearance already keeps this pool off Arena.SouthSpawnPoint in
        /// practice (that's exactly where they're standing while this runs), but excluding both
        /// points explicitly means the guarantee holds even if arrival logic ever changes, and it
        /// reads as the actual rule rather than a side effect of where the player happens to spawn.
        /// </summary>
        static List<Vector2> BuildColumnGrid(Vector2 halfExtents, Vector2 playerPos, List<Rect> pits, Spire spire,
                                             TransmutationRing ring)
        {
            var points = new List<Vector2>();
            float spacing = Tuning.Hazards.ColumnGridSpacing;
            float margin = Tuning.Hazards.ColumnGridWallMargin;
            Vector2 northDoor = Arena.NorthDoorPoint;
            Vector2 southSpawn = Arena.SouthSpawnPoint;

            int stepsX = Mathf.FloorToInt((halfExtents.x - margin) / spacing);
            int stepsY = Mathf.FloorToInt((halfExtents.y - margin) / spacing);

            for (int ix = -stepsX; ix <= stepsX; ix++)
            for (int iy = -stepsY; iy <= stepsY; iy++)
            {
                var p = new Vector2(ix * spacing, iy * spacing);
                if (Vector2.Distance(p, playerPos) < Tuning.Hazards.HazardClearance) continue;
                if (Vector2.Distance(p, northDoor) < Tuning.Hazards.HazardClearance) continue;
                if (Vector2.Distance(p, southSpawn) < Tuning.Hazards.HazardClearance) continue;

                // Off the room's walls by the biggest pillar's radius and a body - a column hard
                // against a wall would close the gap beside it into a one-body corridor.
                if (!Arena.OnFloor(p, Tuning.Hazards.ColumnRadiusLarge + 0.6f)) continue;

                bool blocked = false;
                for (int k = 0; k < pits.Count && !blocked; k++)
                    blocked = PitGrid.Blocks(pits[k], p);
                if (blocked) continue;

                // Measured from the column's RIM, as the pits are: no column inside the ring.
                if (spire != null && Vector2.Distance(p, spire.Centre)
                        < Tuning.Spire.CaptureRadius + Tuning.Hazards.ColumnRadiusLarge + 0.2f) continue;
                if (ring != null && Vector2.Distance(p, ring.Centre)
                        < TransmutationRing.Radius + Tuning.Hazards.ColumnRadiusLarge + 0.2f) continue;

                points.Add(p);
            }
            return points;
        }

        static Vector2 PopLast(List<Vector2> list)
        {
            var p = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
            return p;
        }

        static void Shuffle(List<Vector2> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

    }
}
