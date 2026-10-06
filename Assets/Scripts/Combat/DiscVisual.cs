using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// The picture of a disc that SPINS - thrown, orbiting, suspended.
    ///
    /// A weapon sprite's pivot is its GRIP, which is right for the hand and wrong for a spin: a
    /// disc gripped off its centre (Deadlights' upper rail, Eclipse's rim, the Rift Disc's grip at
    /// the very top) rotated about the grip and orbited its own flight path. So the renderer goes
    /// on a CHILD of the spinning object, offset so the sprite's visual centre sits on the spin
    /// point. Scale goes on that child, as before.
    /// </summary>
    public static class DiscVisual
    {
        public static SpriteRenderer Add(GameObject spinning)
        {
            var v = new GameObject("visual");
            v.transform.SetParent(spinning.transform, false);
            var sr = v.AddComponent<SpriteRenderer>();
            // A kindled disc's marks burn in flight as in the hand (SecretFire); nothing for any other.
            Art.Gear.KindledMarks.On(sr);
            return sr;
        }

        /// <summary>Call once the sprite and the child's scale are set.</summary>
        public static void Centre(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null) return;
            var c = sr.sprite.bounds.center;
            var s = sr.transform.localScale;
            sr.transform.localPosition = new Vector3(-c.x * s.x, -c.y * s.y, 0f);
        }
    }
}
