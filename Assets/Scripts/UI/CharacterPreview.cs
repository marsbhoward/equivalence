using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// A character rendered large, into a texture a UI panel can show.
    ///
    /// Built as its own rig parked far from the arena and filmed by its own camera, because the
    /// gameplay camera can never show gear properly - even tightened, a character is ~12% of
    /// screen height, which is fine for combat readability and nowhere near enough to appreciate
    /// a cosmetic worth real money.
    ///
    /// Extracted from CharacterScreen once the transmutation circle needed the same thing. The
    /// duplicate that would otherwise exist is exactly where the two screens would drift apart -
    /// and a transmog preview that framed or posed the character differently from the loadout
    /// preview would be worse than no preview, since the whole job here is showing the player
    /// what they will actually look like.
    ///
    /// PIXEL PERFECTION, which this had none of and needed all of. Three things have to line up
    /// or the sharpest art in the world arrives on screen as mush, and all three were wrong:
    /// the texture has to be POINT filtered, it has to be the size it is DRAWN at so nothing
    /// resamples, and the camera has to put a whole number of screen pixels on an art texel.
    /// See <see cref="MatchTo"/> and <see cref="Fit"/>.
    /// </summary>
    public class CharacterPreview
    {
        /// <summary>Parked far from the arena so nothing else lands in this camera's view.</summary>
        const float Origin = 1000f;

        GameObject _holder;
        Camera _cam;
        RenderTexture _rt;
        ICharacterRig _rig;

        // Held so the size can be re-checked when the profile changes, which is also the cheapest
        // moment to catch a window resize while a screen is open.
        RawImage _image;
        Canvas _canvas;

        public RenderTexture Texture => _rt;
        public ICharacterRig Rig => _rig;

        public void Build(ElementType element, int width = 512, int height = 720)
        {
            _fittedLook = null;
            _holder = new GameObject("preview-character");
            _holder.transform.position = new Vector3(Origin, Origin, 0f);
            _rig = CharacterRigFactory.Build(_holder, element, 0);

            // The preview is the one place gear is actually studied, so it stands at ease with
            // the arms off the waist instead of in the square-on combat idle that hides the belt,
            // and it paints the higher-detail art where a piece has any. A texel is ~14 screen
            // pixels here against 4 in the arena, so detail the game cannot draw still lands.
            _rig.SetShowcasePose(true);
            _rig.SetDetailArt(true);

            // THE SAME REST POSE THE ARENA USES. ShowcasePose above is older than the shouldered
            // carry and only eases the arms a few degrees off the centre line; on its own it left
            // the preview standing in the pre-reproportion idle with both fists hanging, which is
            // no longer what the character looks like anywhere else. PlayerController sets this
            // every frame the chain is idle (see its SetGripShouldered call); the preview has no
            // combat to come out of, so it just holds it.
            //
            // The two are not alternatives - ShowcasePose still does its own job of lifting the
            // fists clear of the belt, which matters more here than in the arena because the belt
            // and the tasset under it are exactly what someone opens this screen to look at.
            _rig.SetGripShouldered(true);

            _rt = NewTexture(width, height);

            var camGo = new GameObject("preview-camera");
            camGo.transform.position = new Vector3(Origin, Origin + 0.05f, -10f);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 0.72f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.07f, 0.08f, 0.11f, 1f);
            _cam.targetTexture = _rt;
            _cam.cullingMask = ~0;
            FitSettled();
        }

        /// <summary>
        /// Point filtering, always. This is pixel art shown at a dozen screen pixels per texel;
        /// bilinear does not soften it slightly, it turns every edge into a gradient the width
        /// of a whole art pixel.
        /// </summary>
        static RenderTexture NewTexture(int w, int h)
            => new(Mathf.Max(1, w), Mathf.Max(1, h), 16)
            {
                name = "character-preview",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

        /// <summary>
        /// Resize the texture to the exact number of SCREEN pixels the image occupies, and hand
        /// it to that image.
        ///
        /// Without this the render texture is a fixed 460x760 (or 512x720) stretched into
        /// whatever rectangle the layout gave it - measured at 1.11x across and 1.08x down on the
        /// transmutation screen, so every pixel of the character was a bilinear blend of two, and
        /// the character was also 3% wider than tall because the two scales did not match. It
        /// read as low-resolution art. It was full-resolution art, resampled.
        ///
        /// Called after the layout exists, since the size comes FROM the layout.
        /// </summary>
        public void MatchTo(RawImage image, Canvas canvas)
        {
            _image = image;
            _canvas = canvas;
            if (ApplySize()) FitSettled();
        }

        /// <summary>Resize to the image's current pixel size. True if anything changed.</summary>
        bool ApplySize()
        {
            if (_image == null || _cam == null) return false;

            float scale = _canvas != null ? _canvas.scaleFactor : 1f;
            var rect = ((RectTransform)_image.transform).rect;
            int w = Mathf.RoundToInt(rect.width * scale);
            int h = Mathf.RoundToInt(rect.height * scale);
            if (w < 1 || h < 1) return false;

            bool changed = _rt == null || _rt.width != w || _rt.height != h;
            if (changed)
            {
                var old = _rt;
                _rt = NewTexture(w, h);
                _cam.targetTexture = _rt;
                if (old != null) { old.Release(); Object.Destroy(old); }
            }
            _image.texture = _rt;
            return changed;
        }

        /// <summary>Repaint to a profile and reframe. Safe before Build.</summary>
        public void Refresh(CharacterProfile profile)
        {
            if (_rig == null) return;
            bool resized = ApplySize();
            CharacterRigFactory.Paint(_rig, profile);

            // Reframe only when what is WORN changed (or the image did). Fit measures the live
            // renderers, which the idle animation is moving, so every refit centres on a slightly
            // different picture - and screens call this for things that change nothing (the
            // transmutation circle's tab switch rebuilds its pane and refreshes), which read as
            // the character jumping a texel or two on every tab.
            string look = profile != null ? JsonUtility.ToJson(profile) : null;
            if (resized || look != _fittedLook)
            {
                _fittedLook = look;
                FitSettled();
            }
        }

        /// <summary>The profile the current frame was fitted to, serialised - see Refresh.</summary>
        string _fittedLook;

        /// <summary>
        /// Fit now, and again once the rig has DRAWN. Every caller fits in the same frame it built
        /// or repainted the rig, before the rig's first animation tick - the arms and weapon are
        /// still where Apply left them, not in the pose the player will actually see - so the
        /// first fit measures a picture that is gone a frame later, and the next refit (any
        /// Refresh) jumped the character to where it should have been all along.
        /// </summary>
        void FitSettled()
        {
            Fit();
            if (_cam == null) return;
            var settle = _cam.GetComponent<PreviewSettle>();
            if (settle == null) settle = _cam.gameObject.AddComponent<PreviewSettle>();
            settle.Arm(this);
        }

        // ---------------------------------------------------------------- zoom and pan
        //
        // The preview is the one place a cosmetic worth real money is actually looked at, so it
        // has to be inspectable - but it must not become a close-up ONLY, because the first thing
        // it has to answer is "what do I look like".
        //
        // So the fit is the FLOOR. The view opens framing the whole character and zooms in from
        // there; it can never be zoomed out past itself, which means there is no state where the
        // character is small and lost in the frame.

        /// <summary>
        /// Zoom expressed as whole SCREEN PIXELS PER ART TEXEL, always even.
        ///
        /// Not a float multiplier, and that is the whole design. Point sampling is crisp only on
        /// an integer ratio - at 16.3 some texel columns come out 16 wide and some 17, and a grid
        /// of unequal squares is what actually reads as cheap. A continuous zoom would spend most
        /// of its range there. Stepping the RATIO instead means every stop on the way in is
        /// exactly as sharp as the one before it, and the steps come out as a natural set of
        /// notches rather than something that had to be invented.
        ///
        /// Even rather than merely integer for the same reason <see cref="SnapToTexelGrid"/> is:
        /// gear is drawn at a second density and has to land on whole pixels too.
        /// </summary>
        int _steps;

        int _fitSteps;
        Vector2 _pan;
        Bounds _bounds;

        /// <summary>How far in the view may be pushed, as a multiple of the fitted zoom.</summary>
        const int MaxZoomMultiple = 6;

        public bool CanZoomIn => _steps < _fitSteps * MaxZoomMultiple;
        public bool CanZoomOut => _steps > _fitSteps;

        /// <summary>1 at the fitted frame, rising as it is zoomed in. For a UI readout.</summary>
        public float ZoomFactor => _fitSteps > 0 ? (float)_steps / _fitSteps : 1f;

        /// <summary>
        /// Step the zoom, keeping whatever is under <paramref name="focus01"/> under it.
        ///
        /// The focus is in IMAGE space, 0..1 from the bottom left, because the caller has a mouse
        /// position over a RawImage and nothing else - it should not have to know where the
        /// preview camera is pointing.
        /// </summary>
        public void Zoom(float notches, Vector2 focus01)
        {
            if (_cam == null || _fitSteps <= 0 || Mathf.Abs(notches) < 0.01f) return;

            // MULTIPLICATIVE, then snapped back onto an even ratio.
            //
            // Adding two pixels-per-texel a notch is arithmetically tidy and useless in the hand:
            // from a fitted 18 it is a 11% step, so reaching a close-up takes a dozen of them and
            // every one feels like nothing happened. A constant PROPORTION per notch gives the
            // same felt step at every scale - 18, 24, 32, 44, 60 - and snapping the result keeps
            // each stop exactly as crisp as the fit.
            const float PerNotch = 1.35f;
            int want = Mathf.RoundToInt(_steps * Mathf.Pow(PerNotch, notches) / 2f) * 2;

            // A rounding that lands back on where we started would make a notch do nothing at all
            // at the low end, so force it at least one step in the direction asked for.
            if (want == _steps) want += notches > 0 ? 2 : -2;

            want = Mathf.Clamp(want, _fitSteps, _fitSteps * MaxZoomMultiple);
            if (want == _steps) return;

            // The world point under the focus has to still be under it afterwards. Same algebra as
            // the mastery board: pan' = pan*k + focus*(1-k), where k is the ratio of the two
            // scales. Writing it as `pan -= focus * (k-1)` is only correct while the pan is zero.
            var before = FocusOffset(focus01);
            _steps = want;
            var after = FocusOffset(focus01);
            _pan += before - after;

            ApplyView();
        }

        /// <summary>The world offset from the frame's centre to a normalised point in the image.</summary>
        Vector2 FocusOffset(Vector2 focus01)
        {
            float halfH = HalfHeight;
            float halfW = halfH * Aspect;
            return new Vector2((focus01.x - 0.5f) * 2f * halfW, (focus01.y - 0.5f) * 2f * halfH);
        }

        float Aspect => _rt != null && _rt.height > 0 ? (float)_rt.width / _rt.height : 1f;

        float HalfHeight => _rt == null || _steps <= 0
            ? (_cam != null ? _cam.orthographicSize : 1f)
            : _rt.height / (2f * _steps * Art.PixelSprite.LayoutUnit);

        /// <summary>Drag the view, in IMAGE pixels.</summary>
        public void PanBy(Vector2 deltaPixels)
        {
            if (_cam == null || _rt == null || _rt.height < 2) return;
            float worldPerPixel = 2f * HalfHeight / _rt.height;
            _pan -= deltaPixels * worldPerPixel;
            ApplyView();
        }

        /// <summary>Back to the whole character.</summary>
        public void ResetView()
        {
            _steps = _fitSteps;
            _pan = Vector2.zero;
            ApplyView();
        }

        /// <summary>
        /// Push the current zoom and pan onto the camera, with the pan CLAMPED so the character
        /// cannot be dragged out of its own frame.
        ///
        /// Unclamped, one flick leaves an empty panel and nothing to grab - the same failure the
        /// mastery board had, and worse here because there is no visible board to aim at.
        /// </summary>
        void ApplyView()
        {
            if (_cam == null) return;

            float halfH = HalfHeight;
            float halfW = halfH * Aspect;

            // Only clamp on an axis where the character is actually bigger than the view; when it
            // fits, it stays centred.
            float slackX = Mathf.Max(0f, _bounds.extents.x - halfW);
            float slackY = Mathf.Max(0f, _bounds.extents.y - halfH);
            _pan = new Vector2(Mathf.Clamp(_pan.x, -slackX, slackX),
                               Mathf.Clamp(_pan.y, -slackY, slackY));

            _cam.orthographicSize = halfH;
            _cam.transform.position = new Vector3(_bounds.center.x + _pan.x,
                                                  _bounds.center.y + _pan.y,
                                                  _cam.transform.position.z);
        }

        /// <summary>
        /// Frame whatever the character is actually wearing, and set the zoom FLOOR from it.
        ///
        /// The size used to be a constant - fine until a weapon was longer than the character.
        /// The greatsword is about twice the body's height, and a fixed frame simply cut the
        /// blade off partway up, which reads as a broken sprite rather than a big sword.
        /// Measuring the renderers instead means it keeps working whatever gets equipped next.
        /// </summary>
        public void Fit()
        {
            if (_cam == null || _rig == null || _rig.Transform == null) return;

            Bounds? total = null;
            foreach (var sr in _rig.Transform.root.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                if (total.HasValue) { var b = total.Value; b.Encapsulate(sr.bounds); total = b; }
                else total = sr.bounds;
            }
            if (!total.HasValue) return;

            var bounds = total.Value;
            float aspect = _rt != null && _rt.height > 0 ? (float)_rt.width / _rt.height : 1f;

            // Fit BOTH axes: a sword held out sideways is wider than it is tall, and sizing on
            // height alone would clip it at the edges instead of the top.
            const float Margin = 1.08f;
            float half = Mathf.Max(bounds.extents.y, bounds.extents.x / Mathf.Max(aspect, 0.01f));

            // Sitting at the fit is not a zoom anyone chose, so it follows the new fit whichever
            // way it moves. Kept only while the player had zoomed in: otherwise the opening fit
            // (measured before the rig had posed - see FitSettled) left the view locked too tight.
            bool atFit = _steps == _fitSteps;

            _bounds = bounds;
            _fitSteps = StepsFor(SnapToTexelGrid(Mathf.Max(0.5f, half * Margin)));

            // A re-fit happens on every equip and every resize. Keep the zoom the player chose,
            // but never below the new fit - swapping to a longer sword has to be able to pull the
            // frame back out, or the blade simply leaves the panel.
            _steps = _steps <= 0 || atFit
                ? _fitSteps
                : Mathf.Clamp(_steps, _fitSteps, _fitSteps * MaxZoomMultiple);
            ApplyView();
        }

        /// <summary>The even pixels-per-texel a given half-height corresponds to.</summary>
        int StepsFor(float halfHeight)
        {
            if (_rt == null || _rt.height < 2 || halfHeight <= 0f) return 2;
            float pxPerTexel = _rt.height / (2f * halfHeight) / Art.PixelSprite.LayoutUnit;
            return Mathf.Max(2, Mathf.FloorToInt(pxPerTexel / 2f) * 2);
        }

        /// <summary>
        /// Widen the frame until one art texel covers a whole EVEN number of screen pixels.
        ///
        /// This one is measured off the render texture's own height, so unlike the arena camera
        /// it was never at the mercy of what `Screen.height` reports - see the correction note in
        /// CLAUDE.md. The preview's blur was entirely the resampling above, not the ratio.
        ///
        /// Point sampling is only crisp on an integer ratio; at 16.3 pixels per texel some blocks
        /// come out 16 wide and some 17, and a grid of unequal squares is what actually reads as
        /// cheap. EVEN rather than merely integer is a holdover from when gear was drawn at twice
        /// the body's density; it costs at most one extra texel of margin and it keeps this
        /// correct if a second density is ever reintroduced.
        ///
        /// It always rounds the ratio DOWN, so the frame only ever grows and the fit computed
        /// above still holds. The cost is up to one texel of extra margin, which no one can see.
        /// </summary>
        float SnapToTexelGrid(float size)
        {
            if (_rt == null || _rt.height < 2) return size;

            float pxPerUnit = _rt.height / (2f * size);
            float pxPerTexel = pxPerUnit / Art.PixelSprite.LayoutUnit;
            int steps = Mathf.FloorToInt(pxPerTexel / 2f) * 2;      // even, so gear lands too
            if (steps < 2) return size;                             // too small to snap usefully

            return _rt.height / (2f * steps * Art.PixelSprite.LayoutUnit);
        }

        public void Dispose()
        {
            if (_rig != null && _rig.Transform != null) Object.Destroy(_rig.Transform.root.gameObject);
            else if (_holder != null) Object.Destroy(_holder);
            if (_cam != null) Object.Destroy(_cam.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
            _rig = null; _holder = null; _cam = null; _rt = null;
            _image = null; _canvas = null;
        }
    }

    /// <summary>
    /// Refits a <see cref="CharacterPreview"/> once its rig has animated - see FitSettled. Lives on
    /// the preview's own camera so it dies with it. Two frames, not one: the rig's own animation
    /// and this LateUpdate have no fixed order, so one frame could still measure the unposed rig.
    /// The preview is a plain class Unity cannot serialise, so a domain reload leaves this null
    /// and it simply stands down.
    /// </summary>
    public class PreviewSettle : MonoBehaviour
    {
        CharacterPreview _preview;
        int _frames;

        public void Arm(CharacterPreview preview)
        {
            _preview = preview;
            _frames = 2;
            enabled = true;
        }

        void LateUpdate()
        {
            if (_preview == null) { enabled = false; return; }
            if (--_frames > 0) return;
            enabled = false;
            _preview.Fit();
        }
    }
}
