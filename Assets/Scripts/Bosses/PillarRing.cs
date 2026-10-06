using UnityEngine;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Bosses
{
    /// <summary>
    /// Medusa's ring of pillars: fixed SLOTS around her, each empty or holding one cracked
    /// <see cref="Column"/>. The ring is the fight's whole geometry - the only cover from her
    /// gaze - so it is slots rather than a scatter: a refill lands exactly where a pillar stood,
    /// and the circle stays a circle however many times it is broken and rebuilt.
    ///
    /// Pillars are ordinary cracked columns: solid, sight-blocking for everyone (HazardQuery
    /// already treats them so for every enemy shot), and breakable by the player's attacks as
    /// well as by her. A broken one is simply gone - Destroy - and its slot reads empty.
    ///
    /// <see cref="Shadowed"/> is THE AUTHORITY on whether her gaze reaches a point. The red
    /// picture (<see cref="GazeShade"/>) is painted from the same function, so the drawing and
    /// the rule cannot disagree - the same reason the Cantor's slices test ArenaSectors.IndexAt
    /// rather than a collider shaped like the sprite.
    /// </summary>
    public class PillarRing : MonoBehaviour
    {
        // Non-readonly array, element null-guarded everywhere: a domain reload hands a readonly
        // collection back freshly initialised (see CLAUDE.md, Domain reload traps). Column
        // references themselves survive - they are UnityEngine.Objects.
        Column[] _slots;
        Vector2 _centre;

        public int Count => _slots != null ? _slots.Length : 0;

        public static PillarRing Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("medusa.ring");
            go.transform.SetParent(parent, false);
            var ring = go.AddComponent<PillarRing>();
            ring._centre = centre;
            ring._slots = new Column[Tuning.Medusa.PillarSlots];
            for (int i = 0; i < ring._slots.Length; i++) ring.Refill(i);
            return ring;
        }

        /// <summary>Where slot <paramref name="i"/> stands. Slot 0 is straight below her, so the
        /// player's arrival point (Arena.SouthSpawnPoint) starts in its shadow.</summary>
        public Vector2 SlotPosition(int i)
        {
            float a = -Mathf.PI * 0.5f + i * (Mathf.PI * 2f / Mathf.Max(1, Count));
            return _centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Tuning.Medusa.RingRadius;
        }

        public static float PillarRadius => Column.RadiusFor(ColumnSize.Medium);

        public Column At(int i) => _slots != null && i >= 0 && i < _slots.Length ? _slots[i] : null;

        public bool Standing(int i)
        {
            var c = At(i);
            return c != null && !c.Broken;
        }

        public int StandingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Count; i++) if (Standing(i)) n++;
                return n;
            }
        }

        public int MissingCount => Count - StandingCount;

        /// <summary>One bit per standing slot - cheap to compare each frame, so the red picture is
        /// repainted the moment the player breaks their own cover mid-telegraph, and never else.</summary>
        public int StandingMask
        {
            get
            {
                int m = 0;
                for (int i = 0; i < Count; i++) if (Standing(i)) m |= 1 << i;
                return m;
            }
        }

        /// <summary>A pillar in slot <paramref name="i"/>, if it is empty.</summary>
        public Column Refill(int i)
        {
            if (_slots == null || i < 0 || i >= _slots.Length) return null;
            if (Standing(i)) return _slots[i];
            _slots[i] = Column.Spawn(SlotPosition(i), ColumnSize.Medium, cracked: true, transform,
                                     hp: Tuning.Medusa.PillarHp);
            return _slots[i];
        }

        /// <summary>
        /// Is <paramref name="point"/> hidden from an eye at <paramref name="eye"/> - does the
        /// straight line between them pass through a standing pillar? Geometry, not a raycast:
        /// the ring knows exactly where its pillars are, and a raycast would also meet the
        /// player's own triggers, adds and whatever else stands in the room.
        /// </summary>
        public bool Shadowed(Vector2 eye, Vector2 point)
        {
            float r = PillarRadius;
            for (int i = 0; i < Count; i++)
            {
                if (!Standing(i)) continue;
                if (SegmentHitsCircle(eye, point, SlotPosition(i), r)) return true;
            }
            return false;
        }

        static bool SegmentHitsCircle(Vector2 a, Vector2 b, Vector2 c, float r)
        {
            var ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(c - a, ab) / len2) : 0f;
            return ((a + ab * t) - c).sqrMagnitude <= r * r;
        }

        /// <summary>Every pillar comes down - her death. Destroy is deferred, so the ring's own
        /// object is removed by whoever owns it.</summary>
        public void ShatterAll()
        {
            for (int i = 0; i < Count; i++)
                if (Standing(i)) _slots[i].Break();
        }
    }
}
