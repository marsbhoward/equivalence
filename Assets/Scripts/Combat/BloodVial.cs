using System;
using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// The Blood Blade's fuller: stains one shade deeper every time the signature finisher LANDS
    /// a hit, and empties the moment a full vial is spent on the Heavy release.
    ///
    /// Unlike WeaponHeat, this advances on CONNECTING rather than on completing. Heat's own note
    /// explains why a blast that catches nobody still turns its cycle - but this weapon's whole
    /// claim is that it is drinking blood, and a swing that hit nothing drew none. See
    /// PlayerController.ResolveArc for the landed-gated call.
    ///
    /// Same shape as WeaponHeat otherwise, including the event: both components are rebuilt fresh
    /// every run (GameBootstrap.BuildPlayer), so there is nothing here that has to survive a
    /// domain reload mid-run.
    /// </summary>
    public class BloodVial : MonoBehaviour
    {
        public int Fill { get; private set; }

        public bool Full => Fill >= Core.Tuning.Blood.MaxFill;

        /// <summary>Raised whenever Fill changes, so the rig can repaint the blade.</summary>
        public event Action<int> Changed;

        /// <summary>One landed hit from the filling step. No-ops past the ceiling - the release
        /// is what empties it, not a fifth stain overwriting the fourth.</summary>
        public void Advance()
        {
            if (Full) return;
            Fill++;
            Changed?.Invoke(Fill);
        }

        /// <summary>Spent on casting the release, whether or not it connects - the same
        /// completes-not-connects reasoning WeaponHeat's own Advance uses, just for the opposite
        /// half of this weapon's cycle.</summary>
        public void Release()
        {
            Fill = 0;
            Changed?.Invoke(Fill);
        }
    }
}
