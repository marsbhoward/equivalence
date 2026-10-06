using UnityEngine;

namespace Convergence.Rifts
{
    /// <summary>
    /// A picture of the hub, taken the moment the player walked out of it, for the Rift to show
    /// through its tear.
    ///
    /// A SNAPSHOT RATHER THAN A LIVE VIEW, and that is forced rather than chosen: `HideHub`
    /// destroys the room outright when a run starts - it is rebuilt wholesale by `ShowHub` - so
    /// there is nothing alive to point a second camera at mid-run. Keeping it alive off-screen
    /// would mean a whole room's worth of colliders, interactables and Update calls running behind
    /// a fight for the sake of one small image.
    ///
    /// It is also the better read. What the tear shows is the room AS THE PLAYER LEFT IT - their
    /// own furniture, their own trophies, whatever was on the armoury stand - frozen at the moment
    /// they stepped through the door. A live feed of an empty room would be a security camera; a
    /// still is a memory, and the thing the player is deciding whether to go back to.
    ///
    /// Captured through the game camera itself, so the framing and the hub's own tilt come for
    /// free and the image is literally the last thing they saw.
    ///
    /// STATIC, and deliberately not a MonoBehaviour field: it has to outlive the hub that produced
    /// it and every object in the run that displays it. It is one small texture held for the
    /// process, replaced on each visit to the hub.
    /// </summary>
    public static class HubGlimpse
    {
        /// <summary>The last captured hub, or null if the player has not left one yet.</summary>
        public static Sprite Image { get; private set; }

        /// <summary>Kept so the texture can be released before it is replaced - without this every
        /// trip to the hub would leak one.</summary>
        static Texture2D _texture;

        /// <summary>
        /// Square, because the tear is a tall narrow shape and only ever shows a slice down the
        /// middle: capturing the full 16:9 would spend most of the pixels on parts of the room the
        /// aperture can never reveal.
        /// </summary>
        const int Size = 384;

        /// <summary>
        /// How much of the room to take in - orthographic half-height, so the capture covers twice
        /// this in world units.
        ///
        /// Much tighter than the hub's own framing, for two reasons. The tear only ever shows a
        /// narrow vertical slice, so a wide capture spends most of its pixels on parts of the room
        /// the aperture can never reveal; and the gate has to be BIG enough in the aperture to be
        /// recognised at a glance, which is the entire point of centring on it. At 3.6 the sigil
        /// plate came out about a sixth of the frame and read as a grey rectangle with marks on it.
        /// </summary>
        const float FramingSize = 2.4f;

        /// <summary>
        /// Take the picture, centred on a chosen point. Called from GameBootstrap immediately
        /// BEFORE the hub is torn down.
        ///
        /// IT CENTRES ON THE GATE, and that is the whole reason this takes a focus at all. Every
        /// other fixture in the hub can be picked up and moved - the couch, the terminal, the
        /// table, the crate, the circle, the armoury - so a snapshot framed on the room's geometric
        /// middle shows something different for every player and, for a player who has rearranged
        /// things, possibly nothing recognisable at all. The sigil door is the one fixture that is
        /// structurally absent from the room editor's movables list and can never be picked up.
        /// Framing on it means the glimpse always says "this is your hub" at a glance, whoever is
        /// looking and however they have arranged the place.
        ///
        /// Read back into a plain Texture2D rather than kept as a RenderTexture, so what the Rift
        /// holds is an ordinary Sprite that any SpriteRenderer can draw and a SpriteMask can cut.
        /// A RenderTexture would need its own material and would be lost on a device context reset,
        /// which on WebGL is not hypothetical.
        /// </summary>
        /// <param name="focus">World point to centre on - the gate.</param>
        public static void Capture(Camera cam, Vector2 focus)
        {
            if (cam == null) return;

            var rt = RenderTexture.GetTemporary(Size, Size, 16, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            var prevPos = cam.transform.position;
            float prevSize = cam.orthographicSize;

            // Re-aim by SHIFTING rather than by assigning a position, so the hub's own 32-degree
            // tilt is preserved - a camera moved to "focus + offset" by hand would have to redo
            // SetHubCamera's trigonometry and would drift from it the moment either changed.
            //
            // Where the view is currently centred is found by casting through the middle of the
            // viewport onto the z=0 plane every sprite in this project lives on; the camera then
            // moves by the difference. Works for any orientation, including the tilt.
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Mathf.Abs(ray.direction.z) > 0.0001f)
            {
                var centre = ray.origin + ray.direction * (-ray.origin.z / ray.direction.z);
                var shift = new Vector3(focus.x - centre.x, focus.y - centre.y, 0f);
                cam.transform.position = prevPos + shift;
            }
            cam.orthographicSize = FramingSize;

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply(false, true);

            cam.targetTexture = prevTarget;
            cam.transform.position = prevPos;
            cam.orthographicSize = prevSize;
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            if (_texture != null) Object.Destroy(_texture);
            _texture = tex;
            Image = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
        }
    }
}
