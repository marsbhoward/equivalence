using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Draws what <see cref="StatusEffects"/> is doing to a target.
    ///
    /// Burn, soak and stagger previously rendered nothing at all, which hit fire hardest: four of
    /// its five stack tiers are about burn, so the element's whole identity was happening with no
    /// sign of it on screen.
    ///
    /// Kept separate from the sim so authored VFX can replace it without touching the rules - the
    /// same seam ArtBinder gives the characters. Every picture is its own object beside the body,
    /// never a tint on it: <see cref="Health"/>'s hit flash already owns the body's colour.
    /// </summary>
    [DisallowMultipleComponent]
    public class StatusVisuals : MonoBehaviour
    {
        StatusEffects _status;
        [SerializeField] BurnEmbers _embers;
        [SerializeField] StatusMarks _marks;

        void Awake() => _status = GetComponent<StatusEffects>();

        void Update()
        {
            if (_status == null) return;

            // Each status draws its own picture, made the first time it lands: burn's embers
            // (BurnEmbers), and soak's drips, stagger's stars and petrify's stone (StatusMarks -
            // the user's pick, 2026-10-26; they replaced one soft halo blending the colours).
            if (_status.Burning && _embers == null) _embers = BurnEmbers.For(_status);
            if ((_status.Soaked || _status.Staggered || _status.Petrified) && _marks == null) _marks = StatusMarks.For(_status);
        }
    }
}
