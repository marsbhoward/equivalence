using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using Convergence.Art.Gear;

namespace Convergence.Combat
{
    /// <summary>
    /// Separatio's three figures - Sulfur, Salt and Mercury - that the player comes apart into for
    /// the length of the finisher, each holding one of the three swords Tria Prima is made of.
    ///
    /// The same machinery as Shadow's echoes (<see cref="ShadowEcho"/>: a real rig, painted from
    /// the player's own loadout, tinted by a MULTIPLY so the art keeps its shading) with three
    /// differences: exactly three, each in its principle's colour rather than black, and each
    /// holding a part rather than the whole - which is what makes the move read as the sword
    /// coming apart and not as three copies of the player.
    ///
    /// The parts come from the DRAWN weapon (<see cref="GearItem.SplitBlades"/>) - the same
    /// "pictures follow the picture" rule as everything else. Socket the seal beside another sword
    /// and the figures all hold that sword whole: there is nothing on screen to pull apart.
    ///
    /// Hung outside the player, like EchoChorus's figures, so a figure stays where it struck
    /// instead of sliding with the player - and destroyed with this component for the same reason.
    /// </summary>
    public class SeparatioFigures : MonoBehaviour
    {
        public Player.PlayerController Owner;
        public ElementType Element = ElementType.Fire;

        /// <summary>How to dress a figure - the same lambda EchoChorus takes, so a figure is
        /// dressed exactly the way the player is.</summary>
        public System.Action<ICharacterRig> Paint;

        /// <summary>The parts the figures hold, in Sulfur/Salt/Mercury order, or null to hold
        /// whatever the player is drawn holding. Re-set when the drawn weapon changes.</summary>
        public Sprite[] Blades;

        /// <summary>
        /// Built on first use and kept. NOT readonly and null-checked per element - a domain
        /// reload can hand this back empty or holding destroyed figures, and either must rebuild
        /// rather than throw (see "Domain reload traps").
        /// </summary>
        List<ShadowEcho> _figures = new();

        Transform _root;

        Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("separatio");
                    go.transform.SetParent(transform.parent, false);
                    _root = go.transform;
                }
                return _root;
            }
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        ShadowEcho Figure(int index)
        {
            _figures ??= new List<ShadowEcho>();
            while (_figures.Count <= index) _figures.Add(null);
            if (_figures[index] == null) _figures[index] = ShadowEcho.Build(Root, Element, Paint);
            return _figures[index];
        }

        /// <summary>
        /// Figure <paramref name="index"/> appears at <paramref name="at"/>, faces
        /// <paramref name="dir"/> and plays <paramref name="motion"/> with its part in hand.
        /// Purely the picture - the hit is resolved by the controller.
        /// </summary>
        public void Throw(int index, Vector2 at, Vector2 dir, AttackMotion motion, float window)
        {
            var tints = Tuning.Separatio.FigureTints;
            var figure = Figure(index);
            figure.Play(at, dir, motion, window, alt: false, Tuning.Separatio.FigureFadeSeconds,
                        tints[index % tints.Length]);
            figure.HoldWeapon(Blades != null && index < Blades.Length ? Blades[index] : null);
        }

        /// <summary>Re-dress every figure after a mid-run loadout change - see EchoChorus.Repaint.</summary>
        public void Repaint()
        {
            if (_figures == null) return;
            foreach (var f in _figures)
                if (f != null) f.Repaint(Paint);
        }
    }
}
