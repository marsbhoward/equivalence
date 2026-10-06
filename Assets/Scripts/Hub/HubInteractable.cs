using System;
using UnityEngine;

namespace Convergence.Hub
{
    /// <summary>What the prompt card says while something is focused.</summary>
    public struct Prompt
    {
        public string Title, Sub, Body, Key;
        public Color Accent;
    }

    /// <summary>
    /// Something in the hub the character can walk up to and use.
    ///
    /// One concrete component rather than a base class the doors and the couch inherit, because
    /// the thing that varies between them is their ART, not their interaction: every one of them
    /// is a point on the floor, a radius, some words, and something that happens on [E]. Their
    /// own components stay in charge of how they look and configure one of these alongside.
    ///
    /// This exists because HubRoom's focus check had grown a hard-coded branch per feature -
    /// doors, then the circle, then the frames - and each new one had to remember to clear the
    /// others. Now the room scans a list and nothing has to know what else is in the room.
    /// </summary>
    public class HubInteractable : MonoBehaviour
    {
        /// <summary>Where the character has to stand. Measured from here, not from the art.</summary>
        public Vector2 Anchor;

        public float Radius = 1.6f;

        /// <summary>
        /// Whether OTHER placements have to stay clear of this one. True for everything by
        /// default - the room editor's whole point is that furniture doesn't swallow the spot you
        /// need to stand to use something else. Frames turn this off: unlike a door or a seat,
        /// standing directly in front of one is a nice-to-have rather than the room's whole
        /// purpose, and their own interact radius (1.5, generous so the art is easy to walk up
        /// to) was blocking a wide ring of otherwise normal floor space for no purpose stronger
        /// than that convenience.
        /// </summary>
        public bool BlocksPlacement = true;

        /// <summary>
        /// The radius placement clearance is measured against, if different from <see
        /// cref="Radius"/>. Negative (the default) means "same as Radius" - most things should
        /// reach exactly as far in both senses. The sigil door is the exception: its own Radius
        /// (1.9) is generous on purpose, so the prompt appears from a comfortable distance and it
        /// always wins focus over nearby furniture (see Priority) - but reusing that same
        /// generous number as "keep everything else this far away" turned a wide stretch of open
        /// floor the door never actually touches into dead space nothing could be placed on.
        /// </summary>
        public float ClearanceRadius = -1f;

        public float EffectiveClearanceRadius => ClearanceRadius >= 0f ? ClearanceRadius : Radius;

        /// <summary>
        /// Higher wins when two overlap. Doors sit above everything: they are what the room is
        /// for, and furniture placed near one must never be able to swallow it.
        /// </summary>
        public int Priority;

        /// <summary>Built fresh on every focus, so a prompt can name live state.</summary>
        public Func<Prompt> Describe;

        public Action OnInteract;
        public Action<bool> OnFocus;

        public static HubInteractable Attach(GameObject go, Vector2 anchor, float radius,
                                             Func<Prompt> describe, Action onInteract,
                                             Action<bool> onFocus = null, int priority = 0)
        {
            var i = go.AddComponent<HubInteractable>();
            i.Anchor = anchor;
            i.Radius = radius;
            i.Describe = describe;
            i.OnInteract = onInteract;
            i.OnFocus = onFocus;
            i.Priority = priority;
            return i;
        }
    }
}
