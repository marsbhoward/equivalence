using UnityEngine;

namespace Convergence.Hazards
{
    /// <summary>
    /// Marks a collider as a BARRIER: it holds bodies and does nothing else. Sight passes over it
    /// (HazardQuery.Query skips it); shots never ask colliders about walls in the first place.
    /// An open edge's (Core.Arena's open edges) holds bodies at the lip; a TIER's
    /// (<see cref="Tier"/>, Hazards.RoomWalls.BuildTiers) along an edge between two heights -
    /// which a LEAP goes over (PlayerController.LeapStrike).
    /// </summary>
    public class Barrier : MonoBehaviour
    {
        /// <summary>A tier's edge, which a leap clears - not an open edge, which nothing crosses.</summary>
        public bool Tier;
    }
}
