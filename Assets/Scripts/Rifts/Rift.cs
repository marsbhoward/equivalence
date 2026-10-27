using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Rifts
{
    /// <summary>
    /// A tear in space-time, standing in the arena, with the hub visible through it.
    ///
    /// A RIFT IS A TEAR, NOT A DOOR, and the whole visual follows from that. An avatar's proximity
    /// thins reality; what opens is a rip, not something anybody built. So: no frame, no threshold,
    /// no architecture. A ragged vertical mandorla (see <see cref="Spr.Tear"/>) that tapers to two
    /// points where the tear runs out, with light escaping along the edge.
    ///
    /// THROUGH IT IS THE HUB - the actual room, as the player left it, furniture and all. That is
    /// the whole emotional content of the fixture and the reason it is worth building properly: the
    /// choice a Rift asks is "leave with this, or push on", and the thing being left FOR has to be
    /// visible or the choice is an abstraction on a menu. A player looking at their own couch
    /// through a hole in the world is being asked a different question from one reading the word
    /// "extract".
    ///
    /// The view is a <see cref="SpriteMask"/> cut to the tear's own shape, so the room is genuinely
    /// seen THROUGH the rip rather than being a picture pasted behind it - the ragged edge crosses
    /// the image and the image ends exactly where the tear does.
    ///
    /// THE MASK IS RANGE-LIMITED. A SpriteMask with no custom range masks every sprite in the scene
    /// at its sorting layer - the floor, the enemies, the player - so the arena would vanish
    /// wherever the tear did not cover it. `isCustomRangeActive` with a two-order window around the
    /// view is what keeps it cutting exactly one renderer.
    /// </summary>
    public class Rift : MonoBehaviour
    {
        /// <summary>The player is close enough to use the tear - and it is usable.</summary>
        public bool PlayerInside { get; private set; }

        /// <summary>
        /// The player is close enough to use the tear, usable or not. A SEALED Red Rift still
        /// answers [ E ]: breaking the seal is the player's call, so the tear has to know they are
        /// standing at it while it is shut.
        /// </summary>
        public bool PlayerNear { get; private set; }

        /// <summary>
        /// A COLLAPSING Rift before its floor is cleared: flickering, untouchable, its clock
        /// running. It offers nothing until <see cref="Stabilise"/> - a prompt for something that
        /// cannot be used would teach the player the button is broken.
        /// </summary>
        public bool Unstable { get; private set; }

        /// <summary>
        /// A RED Rift before its guard has fallen: steady, but burning red and shut. It stands
        /// there from the first wave so the player knows what is on offer - but it stays DORMANT
        /// until the player breaks the seal at the tear and confirms. Only then does its guard come
        /// (GameBootstrap.SummonRedGuard), and it opens once that guard has fallen.
        /// </summary>
        public bool Sealed { get; private set; }
        public bool Usable => !Unstable && !Sealed;

        /// <summary>Pieces pushed through this Rift's free capacity so far. Kept on the TEAR, not
        /// the screen, because the screen can now be closed and reopened - counted per opening,
        /// every reopen would hand out the capacity again.</summary>
        public int Secured;

        /// <summary>Seconds left on an unstable Rift's clock.</summary>
        public float Remaining { get; private set; }
        float _total;
        System.Action _onCollapsed;

        /// <summary>Renderers whose colour Update does not rewrite each frame, with their resting
        /// colours - the flicker scales these from rest rather than compounding on itself.</summary>
        SpriteRenderer[] _steady;
        Color[] _steadyRest;

        /// <summary>How wide and tall the rip stands, world units. Tall and narrow: a rip is
        /// something pulled apart, and a wide one reads as a portal.</summary>
        public const float Width = 2.3f;
        public const float Height = 3.4f;

        /// <summary>How close the player has to be for the Rift to offer itself.</summary>
        public const float Reach = 1.7f;

        SpriteRenderer _view;
        RiftRim _rimPic;
        SpriteMask _mask;
        Vector3 _viewRest;
        /// <summary>The rim's strength this frame: steady, or cut in and out while unstable.</summary>
        internal float RimAlpha = 1f;
        Transform _player;
        float _t;

        static readonly Color Ember = new(1f, 0.22f, 0.16f);

        /// <summary>Sorting orders. The view sits in its own two-order window so the mask has
        /// something narrow to cut and nothing else in the arena is touched.</summary>
        const int ViewOrder = SortingOrders.Fx - 40;

        public static Rift Open(Vector2 at, Transform parent, Transform player)
        {
            var go = new GameObject("rift");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = at;

            var r = go.AddComponent<Rift>();
            r._player = player;

            // No halo or bloom (the user's rule, group 4): the room's light pool round the tear -
            // its Glow, below - is the glow. The edge is texel-drawn (RiftRim).

            // ---- the view through the tear ----
            var viewGo = new GameObject("view");
            viewGo.transform.SetParent(go.transform, false);
            r._view = viewGo.AddComponent<SpriteRenderer>();
            r._view.sprite = HubGlimpse.Image;
            r._view.sortingOrder = ViewOrder;
            r._view.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            // Lifted and warmed. The hub is a deliberately dark room, so seen raw through the tear
            // it reads as a hole onto nothing - and the one thing this fixture has to communicate
            // is that there is somewhere on the other side. Warm against the rim's cold cyan is
            // also the whole emotional shape of the choice: home is the warm thing, and the tear
            // is not.
            r._view.color = new Color(1.35f, 1.22f, 1.05f, 1f);

            // CENTRED, because the glimpse is now framed on the gate - see HubGlimpse. It used to
            // be pushed down to hunt for something recognisable in a snapshot centred on the room's
            // middle, which is the one part of the hub with nothing in it; aiming the capture at
            // the one fixture that can never move solved that upstream, and the offset here would
            // now shove the gate back off centre.
            //
            // IT MUST OVERSHOOT THE APERTURE. Sized to the tear exactly, the room ran out before
            // the tear did and the pale core showed through at both tips - which reads as the top
            // and bottom of the rip being empty, and gives the whole illusion away.
            viewGo.transform.localScale = Vector3.one * (Height * 1.25f);
            r._viewRest = Vector3.zero;
            viewGo.transform.localPosition = r._viewRest;

            var maskGo = new GameObject("aperture");
            maskGo.transform.SetParent(go.transform, false);
            r._mask = maskGo.AddComponent<SpriteMask>();
            // Cut from the curve the rim is drawn on, on the same texel grid (RiftShape).
            r._mask.sprite = RiftShape.Aperture;
            r._mask.isCustomRangeActive = true;
            r._mask.frontSortingOrder = ViewOrder + 1;
            r._mask.backSortingOrder = ViewOrder - 1;

            // The lit edge, ON TOP of the view - half on the aperture's last texel, half outside it,
            // which is what makes the boundary read as torn rather than as a cut-out.
            int hh = RiftShape.HalfHeight, hw = Mathf.CeilToInt(Width * 0.5f * TexelFx.Body);
            r._rimPic = TexelPicture.Attach<RiftRim>(go.transform, "rim", Vector2.zero, hw + 14, hh + 2, hh + 16, ViewOrder + 2);
            Convergence.Art.Lighting.Glow.Attach(go, Tuning.Lighting.RiftRadius, Tuning.Lighting.RiftColor, Tuning.Lighting.RiftIntensity, 0.1f);

            // Stands where it is torn, so the player can walk behind it like anything else.
            DepthSorted.Attach(go, -Height * 0.5f, isFixed: false,
                               r._view, r._rimPic.GetComponent<SpriteRenderer>());

            r._steady = new[] { r._view };
            r._steadyRest = System.Array.ConvertAll(r._steady, x => x.color);
            return r;
        }

        /// <summary>
        /// Makes this a COLLAPSING Rift: unstable for <paramref name="seconds"/> of game time, then
        /// gone - unless <see cref="Stabilise"/> is called first. Game time, so a pause stops it.
        /// </summary>
        public void BeginCollapse(float seconds, System.Action onCollapsed)
        {
            Unstable = true;
            _total = Remaining = Mathf.Max(0.1f, seconds);
            _onCollapsed = onCollapsed;
        }

        /// <summary>Makes this a RED Rift: shut and burning until <see cref="Unseal"/>. The room
        /// beyond still shows, darkened - home is visible, just not yet reachable.</summary>
        public void Seal()
        {
            Sealed = true;
            PlayerInside = false;
            for (int i = 0; i < _steady.Length; i++)
                if (_steady[i] != null)
                {
                    var c = _steadyRest[i];
                    // The view is darkened toward the wound colour; everything else that glows
                    // takes the ember outright.
                    _steady[i].color = _steady[i] == _view
                        ? new Color(c.r * 0.55f, c.g * 0.22f, c.b * 0.22f, c.a)
                        : new Color(Ember.r, Ember.g, Ember.b, c.a);
                }
        }

        /// <summary>The guard fell: the red burns off and the tear opens, with the same cyan
        /// flash a Collapsing Rift gives when it holds.</summary>
        public void Unseal()
        {
            if (!Sealed) return;
            Sealed = false;
            for (int i = 0; i < _steady.Length; i++)
                if (_steady[i] != null) _steady[i].color = _steadyRest[i];
            if (_rimPic != null) _rimPic.Open();   // the rim drawn again up both sides from the foot
        }

        /// <summary>The floor was cleared in time: the flicker stops and the tear holds.</summary>
        public void Stabilise()
        {
            if (!Unstable) return;
            Unstable = false;
            for (int i = 0; i < _steady.Length; i++)
                if (_steady[i] != null) _steady[i].color = _steadyRest[i];
            RimAlpha = 1f;
            if (_rimPic != null) _rimPic.Open();
        }

        /// <summary>
        /// Time ran out. The same implosion an unclaimed Rift Box makes, at the tear's size - a
        /// player who has watched a box run out already knows what this means.
        /// </summary>
        void Collapse()
        {
            Unstable = false;
            PlayerInside = false;
            RiftImplosion.At(transform.position, 1.6f, tear: true);
            var cb = _onCollapsed;
            _onCollapsed = null;
            Destroy(gameObject);
            cb?.Invoke();
        }

        void Update()
        {
            _t += Time.deltaTime;

            // The view drifts a little inside the aperture, so the room beyond feels like a place
            // being glimpsed rather than a photograph nailed behind a hole.
            if (_view != null)
                _view.transform.localPosition = _viewRest
                    + new Vector3(Mathf.Sin(_t * 0.5f) * 0.06f, Mathf.Sin(_t * 0.37f + 1f) * 0.05f, 0f);

            if (Unstable)
            {
                Remaining -= Time.deltaTime;
                if (Remaining <= 0f) { Collapse(); return; }
                Flicker();
                PlayerInside = false;
                PlayerNear = false;
                return;
            }
            PlayerNear = _player != null && Vector2.Distance(_player.position, transform.position) <= Reach;
            if (Sealed) { PlayerInside = false; return; }

            if (_player != null)
                PlayerInside = Vector2.Distance(_player.position, transform.position) <= Reach;
        }

        /// <summary>
        /// An unstable tear cuts in and out - noise rather than a beat, so it reads as failing
        /// rather than blinking - and cuts FASTER in the last UrgentSeconds, so the clock is felt
        /// from the corner of the eye mid-fight without reading the HUD. Never fully gone: the
        /// player has to be able to find it the moment it holds.
        /// </summary>
        void Flicker()
        {
            float urgent = Tuning.CollapsingRift.UrgentSeconds;
            float u = Remaining < urgent ? 1f - Remaining / urgent : 0f;
            float rate = Mathf.Lerp(5f, 24f, u * u);
            float n = Mathf.PerlinNoise(_t * rate, 0.37f);
            float k = n > 0.42f ? Mathf.Lerp(0.55f, 0.85f, n) : 0.12f;

            for (int i = 0; i < _steady.Length; i++)
                if (_steady[i] != null)
                {
                    var c = _steadyRest[i];
                    _steady[i].color = new Color(c.r, c.g, c.b, c.a * k);
                }
            RimAlpha = k;
        }
    }
}
