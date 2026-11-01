using UnityEngine;

namespace Convergence.Hazards
{
    /// <summary>
    /// Marks a collider as an open edge's BARRIER (Core.Arena's open edges): it holds bodies at
    /// the lip and does nothing else. Sight passes over it (HazardQuery.Query skips it) as it
    /// passes over the void; shots never ask colliders about walls in the first place.
    /// </summary>
    public class Barrier : MonoBehaviour { }
}
