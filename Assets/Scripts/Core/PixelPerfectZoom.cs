using UnityEngine;
using Convergence.Art;

namespace Convergence.Core
{
    /// <summary>
    /// Holds the camera at a whole number of screen pixels per art texel.
    ///
    /// WHY THIS AND NOT URP'S PixelPerfectCamera, which is the obvious answer and was the first
    /// thing tried. That component takes <c>assetsPPU</c> as an <b>int</b>, and even though this
    /// project's art is now authored at <see cref="PixelSprite.LayoutUnit"/> = 75 - an int, unlike
    /// the 37.5 it moved from - <b>zoom</b> in URP's own formula is ALSO int-only, and that is the
    /// one that keeps failing:
    ///
    ///     orthoSize = screenHeight / (2 * zoom * assetsPPU),   zoom = screenHeight / refResolutionY
    ///
    /// so holding CameraSize 4.8 at 1080p needs <c>zoom * assetsPPU == 112.5</c>. At assetsPPU 75
    /// that is zoom 1.5, and URP's zoom is an integer division - it would round to 1 and open the
    /// view to orthoSize 7.2, half again as wide as the game is tuned for. No assetsPPU that is
    /// itself a whole number of screen pixels at every common render height (37.5's own reason for
    /// existing, and the reason 75 was chosen by doubling it rather than picking some other value)
    /// also gives an integer zoom at every resolution in this project's support matrix - so this
    /// component is still needed even now that assetsPPU itself CAN be typed into an int field.
    ///
    /// This is the same arithmetic PixelPerfectCamera does in its no-crop path, with the
    /// resolution kept in world units instead of integer pixels. It deliberately does NOT
    /// letterbox: it nudges the ZOOM to the nearest whole texel instead of cropping the frame to
    /// a fixed one, so the view still fills any window.
    ///
    /// Everything downstream reads <c>_cam.orthographicSize</c> - the camera clamp, the range
    /// rings, the arena bounds - so adjusting it here is enough and nothing else has to know.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PixelPerfectZoom : MonoBehaviour
    {
        /// <summary>
        /// The zoom the game is TUNED at, in world units of half-height. The snap moves off this
        /// by less than half a texel; it is never the source of truth for framing decisions.
        /// </summary>
        public float TargetSize = Tuning.Camera.Size;

        /// <summary>
        /// Off means the raw <see cref="TargetSize"/>, which is what shipped before this existed.
        /// Kept as a switch because the snap is the kind of thing that is invisible when it works
        /// and baffling when it does not, and being able to turn it off settles the question.
        /// </summary>
        public bool Snap = true;

        Camera _cam;
        int _lastW, _lastH;
        float _lastTarget;

        void Awake() => _cam = GetComponent<Camera>();

        /// <summary>
        /// LateUpdate, so it runs after anything that moves or reframes the camera in Update.
        /// Recomputed only when the screen or the target actually changes - this is arithmetic
        /// on a resize, not per-frame work.
        /// </summary>
        void LateUpdate()
        {
            if (_cam == null) return;
            if (Screen.width == _lastW && Screen.height == _lastH
                && Mathf.Approximately(TargetSize, _lastTarget)) return;

            _lastW = Screen.width;
            _lastH = Screen.height;
            _lastTarget = TargetSize;
            _cam.orthographicSize = Snap ? SizeFor(Screen.height, TargetSize) : TargetSize;
        }

        /// <summary>
        /// The nearest orthographic size that puts a whole number of screen pixels on an art
        /// texel. Static and pure so it can be checked without a camera.
        ///
        /// NEAREST rather than always-wider on purpose. Rounding down only ever grows the view,
        /// which sounds safe, but just below a step it is badly wrong in a way nobody would
        /// accept: at a 2159-pixel-tall window the ratio is 5.997, and flooring to 5 opens the
        /// frame 20% past what the game is framed for. Rounding lands on 6 and misses the tuned
        /// size by 0.04%. The cost is that the view can come in slightly TIGHTER than authored -
        /// bounded by half a texel, which no camera clamp in the game is sensitive to.
        /// </summary>
        public static float SizeFor(int screenHeight, float targetSize)
        {
            if (screenHeight < 2 || targetSize <= 0f) return targetSize;

            // Tried FINEST first, then LayoutUnit, then not at all.
            //
            // An integer at FinestUnit divides cleanly into every coarser density on the ladder
            // (150 -> 75 -> 37.5), so it is the only snap that makes the whole ladder safe at
            // once - rounding against 75 leaves 150 on a half-integer, the alternating-column
            // shimmer case. But it is also the COARSEST ladder of available sizes, because the
            // step it rounds is twice as big, so the nearest valid size can sit a long way from
            // the one the game is framed for.
            //
            // At 720p that is not a rounding detail, it is the difference between the tuned view
            // and a third of the arena: the only sizes 150 can hold there are 2.40 and 4.80, and
            // 3.6 is not among them at any k. FRAMING WINS. A view that shows the wrong amount of
            // the arena is a gameplay change on small screens; shimmer on a moving character is a
            // cosmetic one, and only shows up on art actually drawn at the finest density.
            //
            // So each rung is taken only if it lands close enough, and an unsnapped target is a
            // better answer than a snapped view nobody can play in.
            float best = Snapped(screenHeight, targetSize, PixelSprite.FinestUnit);
            if (WithinTolerance(best, targetSize)) return best;

            best = Snapped(screenHeight, targetSize, PixelSprite.LayoutUnit);
            if (WithinTolerance(best, targetSize)) return best;

            return targetSize;
        }

        /// <summary>
        /// How far the snap may pull the view off the tuned size before it is not worth having,
        /// as a fraction.
        ///
        /// 15% is set from what the two rungs actually offer rather than picked round. The sizes a
        /// density can hold are <c>H / (2k * ppu)</c> for whole k, so the gaps between them widen
        /// as k falls: at k 3 and up the neighbours are within a few percent and the snap is free,
        /// while the jump from k 2 to k 1 is a DOUBLING of the view. 15% takes every rung in the
        /// first case and refuses the second, which is the only one that has ever mattered.
        /// </summary>
        const float MaxDeviation = 0.15f;

        static float Snapped(int screenHeight, float targetSize, float ppu)
        {
            int steps = Mathf.Max(1, Mathf.RoundToInt(screenHeight / (2f * targetSize * ppu)));
            return screenHeight / (2f * steps * ppu);
        }

        static bool WithinTolerance(float size, float targetSize)
            => Mathf.Abs(size - targetSize) <= targetSize * MaxDeviation;
    }
}
