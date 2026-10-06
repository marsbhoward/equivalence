using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// Cycles Phantom's blade through its baked haze flipbook, so the gas actually MOVES rather
    /// than sitting frozen in one irregular but static shape - a static "wavy" sprite still reads
    /// as a solid object with an odd edge, the same way a still photo of smoke reads as a solid
    /// shape until you see it drift.
    ///
    /// Ticks by calling <see cref="ICharacterRig.SetWeaponSprite"/> on a timer, the exact call
    /// WeaponHeat and Prism's gem already use to swap the weapon layer's look - this is a third
    /// consumer of that same seam, not a new rendering technique. No sprite of its own: this
    /// object draws nothing, it only decides which frame the WEAPON layer shows next.
    ///
    /// Also drives <see cref="GearItem.IdleFrames"/> - the Pacemaker's bead - at that item's own
    /// frame time. Named for Phantom because Phantom was first; the ticker is the same either way.
    /// </summary>
    public class PhantomHaze : MonoBehaviour
    {
        /// <summary>
        /// The rig, and the Object reference it can be re-derived from. An interface-typed field
        /// on a MonoBehaviour is NOT restored across a domain reload - see CLAUDE.md - so this
        /// keeps the serialized MonoBehaviour beside it, the same shape ShadowEcho's own rig
        /// reference uses.
        /// </summary>
        [SerializeField] MonoBehaviour _rigBehaviour;
        ICharacterRig _rig;
        ICharacterRig Rig => _rig ??= _rigBehaviour as ICharacterRig;

        Sprite[] _frames;
        float _frameSeconds = Tuning.Phantom.HazeFrameSeconds;
        int _lastIndex = -1;

        public static PhantomHaze Attach(Transform weaponAnchor, ICharacterRig rig, Sprite[] frames,
                                         float frameSeconds = Tuning.Phantom.HazeFrameSeconds)
        {
            var haze = weaponAnchor.GetComponentInChildren<PhantomHaze>(true);
            if (haze == null)
            {
                var go = new GameObject("phantom.haze");
                go.transform.SetParent(weaponAnchor, false);
                haze = go.AddComponent<PhantomHaze>();
            }

            // Re-taken every call, never latched behind the null-check above - the weapon
            // renderer this feeds can be replaced by a repaint, the same trap PrismGlow's and
            // SaintHalo's own notes document for the identical reason.
            haze._rig = rig;
            haze._rigBehaviour = rig as MonoBehaviour;
            haze._frames = frames;
            haze._frameSeconds = Mathf.Max(0.01f, frameSeconds);
            haze._lastIndex = -1;
            return haze;
        }

        public void SetShown(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0 || Rig == null) return;

            // Floor rather than round, so index 0 holds for a full frame's worth of time from
            // t=0 rather than switching immediately - matches how every other timer-driven cycle
            // in this project reads its own clock.
            int idx = Mathf.FloorToInt(Time.time / _frameSeconds) % _frames.Length;
            if (idx == _lastIndex) return;
            _lastIndex = idx;
            Rig.SetWeaponSprite(_frames[idx]);
        }
    }
}
