using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using Convergence.Art.Gear;

namespace Convergence.Combat
{
    /// <summary>
    /// Shadow's duplicate: the after-image that trails every swing.
    ///
    /// TWO HALVES ON DIFFERENT SWITCHES, and the split is the item's whole design:
    ///
    ///   the chain's second hit   from the SOURCE  - the blade in hand, or the one in the socket
    ///   the trailing figure      from the DRAWN blade - what is actually on screen
    ///
    /// The same rule Emberline's heat cycle already lives by ("the blade that heats is the one
    /// being DRAWN, not the one supplying the cycle"), pointed at a different effect: mechanics
    /// belong to the item you equipped, pictures belong to the item you can see. Socket Shadow as
    /// a relic while holding a gilded greatsword and every swing still lands twice - there is
    /// simply no shadow blade on screen for the shadow to be OF.
    ///
    /// (The Echo finisher once also tore a RING of these loose, each casting one of the wheel's
    /// other finishers. Removed: it stacked finisher damage on top of the chain's redistributed
    /// echo. The trail is all that is left.)
    ///
    /// Pooled and capped at <see cref="PoolCap"/>. Building a rig is not cheap - twenty-two
    /// GameObjects and a paint - so it happens at most twice in a run and never again.
    /// </summary>
    public class EchoChorus : MonoBehaviour
    {
        public Player.PlayerController Owner;
        public ElementType Element = ElementType.Fire;

        /// <summary>
        /// How to dress an echo. Supplied by whoever built this, so the chorus needs no opinion
        /// about profiles, transmog or the helmet preference - and so a duplicate is dressed
        /// exactly the way the player is, by the same code.
        /// </summary>
        public System.Action<ICharacterRig> Paint;

        /// <summary>
        /// Whether the trailing after-image is drawn at all: true only when the Shadow blade is
        /// the weapon on screen. See the class note.
        /// </summary>
        public bool ShowsTrail;

        readonly List<ShadowEcho> _pool = new();

        /// <summary>
        /// Where the figures live: BESIDE the player, never under it.
        ///
        /// Parented to the player they are children of a moving transform, so an after-image set
        /// back along the facing would travel with the character instead of staying where it was
        /// thrown, which is the whole point of putting it somewhere in the first place.
        ///
        /// The cost of hanging them outside is that they no longer die with the player, so this
        /// object destroys the root itself - the same leak Hud had when it parented its UI
        /// straight onto the canvas and only the component was destroyed.
        /// </summary>
        Transform _root;

        Transform Root
        {
            get
            {
                if (_root == null)
                {
                    var go = new GameObject("echoes");
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

        /// <summary>Two: a figure is busy for its swing plus its fade, which outlasts one swing
        /// interval, so consecutive swings' trails overlap by one.</summary>
        const int PoolCap = 2;

        ShadowEcho Take()
        {
            foreach (var e in _pool)
                if (e != null && !e.Busy) return e;

            if (_pool.Count >= PoolCap)
            {
                // At the cap every figure is mid-swing, which only happens at extreme attack
                // speed. Re-use the oldest rather than growing: one trail cut short is
                // invisible, an unbounded pool is not.
                var reused = _pool[0];
                _pool.RemoveAt(0);
                _pool.Add(reused);
                return reused;
            }

            var made = ShadowEcho.Build(Root, Element, Paint);
            _pool.Add(made);
            return made;
        }

        /// <summary>
        /// The after-image. A copy of the character playing the SAME swing, a beat behind and a
        /// step back along the facing.
        ///
        /// BEHIND rather than on top of the player. An after-image drawn in place is the honest
        /// version and it is unreadable: two translucent copies of the same figure at the same
        /// angle just look like one badly drawn one. Set back along the facing, the pair reads as
        /// a figure and the thing following it.
        ///
        /// It carries no damage. The second hit is applied per target in ResolveArc, so this is
        /// purely the tell for a passive that would otherwise be an invisible multiplier.
        /// </summary>
        public void Trail(AttackMotion motion, float window, bool alt)
        {
            if (!ShowsTrail || Owner == null) return;
            StartCoroutine(TrailAfter(motion, window, alt));
        }

        IEnumerator TrailAfter(AttackMotion motion, float window, bool alt)
        {
            // Short enough that the two swings visibly overlap - the copy has to be doing the
            // same thing at the same time, a beat late, not performing it afterwards.
            yield return new WaitForSeconds(TrailDelaySeconds);
            if (Owner == null) yield break;

            var facing = Owner.Facing;
            var at = (Vector2)Owner.transform.position - facing * TrailDistance;
            Take().Play(at, facing, motion, window, alt, Tuning.Shadow.EchoFadeSeconds);
        }

        const float TrailDelaySeconds = 0.06f;
        const float TrailDistance = 0.62f;

        /// <summary>
        /// Re-dress every pooled figure. Called when the player's own loadout changes mid-run,
        /// so a duplicate is never wearing the gear you took off three floors ago.
        /// </summary>
        public void Repaint()
        {
            foreach (var e in _pool)
                if (e != null) e.Repaint(Paint);
        }
    }
}
