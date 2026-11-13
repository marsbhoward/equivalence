using UnityEngine;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// Holds a picture at a DEPTH relative to the room: 1 moves with the floor, under 1 is far
    /// (the void's layers - it drifts less than the room as the camera pans), over 1 is near
    /// (the foreground - it sweeps past). The picture is where it would be with the camera at
    /// <see cref="Anchor"/>'s own depth; its screen offset is (Anchor - camera) x Depth, snapped to
    /// the floor's texel grid against the camera so it never swims.
    ///
    /// Runs after the camera's follow (GameBootstrap.LateUpdate) - the execution order below.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class Parallax : MonoBehaviour
    {
        public float Depth = 1f;
        public Vector2 Anchor;
        /// <summary>Units a second the anchor drifts along x (clouds).</summary>
        public float Drift;
        /// <summary>When drifting, the span the anchor wraps within (0: never wraps).</summary>
        public float WrapSpan;

        /// <summary>Units every layer's anchor has RISEN, each by its own depth - the world going
        /// up past a lift that is going down (the Lift platform floor). Reset with the ground.</summary>
        public static float Rise;

        /// <summary>A shift on top of where the depth puts it (a far piece bobbing, FarSightMotion).</summary>
        public Vector2 Shift;

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector2 c = cam.transform.position;
            var a = Anchor;
            a.y += Rise;
            if (Drift != 0f)
            {
                a.x += Drift * Time.time;
                if (WrapSpan > 0f) a.x = Mathf.Repeat(a.x + WrapSpan * 0.5f, WrapSpan) - WrapSpan * 0.5f;
            }
            var off = (a - c) * Depth;
            float ppu = Scenery.Ppu;
            off = new Vector2(Mathf.Round(off.x * ppu) / ppu, Mathf.Round(off.y * ppu) / ppu);
            transform.position = new Vector3(c.x + off.x + Shift.x, c.y + off.y + Shift.y, transform.position.z);
        }
    }
}
