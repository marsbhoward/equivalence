using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// Draws a cloth layer BENT rather than turned: the rows above the hinge stay where they are,
    /// and each row below it slides sideways by a whole number of texels, more the deeper it hangs.
    ///
    /// WHY NOT A ROTATION. The cape used to swing as one board about its neckline, and a board
    /// turned about the middle of its top edge tips that whole edge: at the full swing a cape's
    /// shoulder corners rose and fell about eight cells, so the cloth visibly came away from the
    /// shoulders while the hem - all one plank with them - read as stiff. Cloth is pinned where it
    /// is fastened and moves more the further it hangs from that, which only a bend can draw.
    ///
    /// WHY A SECOND RENDERER. The layer's own SpriteRenderer is the one everything else talks to:
    /// the flash and the stone swap its sprite and put it back, the drape's facing compares it by
    /// identity, Apply clears and repaints it, depth sorting rewrites its order. All of that keeps
    /// working untouched because this never changes it - it only stops it DRAWING
    /// (forceRenderingOff, which is independent of enabled) and draws a bent copy of whatever it
    /// shows through a child renderer, every frame, after depth sorting has run.
    ///
    /// THE BEND IS A MESH, NOT A TEXTURE. The copy is a per-character Sprite on the SAME texture
    /// (the authored one is shared by every wearer and every display), its mesh rebuilt as one quad
    /// per run of rows sharing an offset. Offsets are whole texels, so the art stays point-sampled
    /// on its own grid: a stepped edge where it bends, never a resampled one.
    ///
    /// Driven by <see cref="PrimitiveCharacterRig"/> (AnimateCape, AnimateScarf, and
    /// AnimateLegCloth for the cloth over the legs - see OverLegs), which sets the public fields
    /// below every frame - plain values, so a domain reload keeps them.
    /// </summary>
    [DefaultExecutionOrder(1000)]   // after DepthSorted's LateUpdate, whose order it copies
    public class ClothBend : MonoBehaviour
    {
        /// <summary>Off: the layer draws itself, unbent, and the copy is hidden.</summary>
        public bool Active;

        /// <summary>The hinge's height in the layer's PARENT space (the torso). Rows above it
        /// never move - a collar standing up past the neck included.</summary>
        public float HingeY;

        /// <summary>Hinge to hem, parent-space units. Depth is measured as a fraction of this, so
        /// a drape bending with the cape bends exactly as the cape does at the same height.</summary>
        public float Length = 1f;

        /// <summary>The cloth's angle just under the hinge and at the hem, radians, the rig's
        /// sign: positive swings the hem toward +x.</summary>
        public float UpperRad, LowerRad;

        /// <summary>A wave running down the cloth: its sideways reach at the hem (parent-space
        /// units) and its phase this frame.</summary>
        public float RippleAmp, RipplePhase;

        // ---- cloth over the LEGS (a skirt, a coat, faulds - the Tasset and Belt layers) ----
        //
        // Cloth hung at the waist is worn over two things the cape never meets. The HIPS do not
        // lean - only the torso does, about the waist - so cloth that rode the torso swung its
        // hem back as one board every time the chest leaned into a walk, and the front leg
        // stepped out through it. And the LEGS move under it: in the swing a knee comes forward
        // ten texels and more.
        //
        // So below the waist each texel is first put back where the hips would carry it, then
        // the legs PUSH it. The cloth is a tube round both legs, not two sleeves: its FRONT goes
        // where the furthest-forward point of either leg goes, its BACK where the furthest-back
        // one goes, and across the middle it spreads between the two - a stride opens the skirt
        // into an A. Following each column's nearest leg instead tore it down the middle, the
        // hips being only four texels apart. Below a knee the cloth DRAPES off it rather than
        // following the shin back in (each row's reach is the row above's, less a little).

        /// <summary>On: the cloth hangs from the hips and is pushed by the legs (fields below).
        /// Off: the cape's bend alone.</summary>
        public bool OverLegs;

        /// <summary>The parent's rotation against the hips, radians, CCW positive - undone below
        /// the waist line (parent y 0), in proportion to depth, so it is continuous at the waist
        /// and the cloth under it hangs straight from the hips.</summary>
        public float Plumb;

        /// <summary>Each hip joint in PARENT space, and its leg's thigh and shin angle, radians,
        /// from straight down, positive toward +x (the rig's forward).</summary>
        public Vector2 HipFront, HipBack;
        public float ThighFront, ShinFront, ThighBack, ShinBack;

        /// <summary>Thigh and shin lengths, parent-space units.</summary>
        public float ThighLength, ShinLength;

        /// <summary>Half a leg's width, parent-space units: the cloth lying on the legs is the
        /// hips' span plus this either side, and the push spreads and fades on this scale.</summary>
        public float LegHalfWidth = 0.05f;

        /// <summary>How much of the push reaches cloth far out past the legs - a wide skirt's
        /// outer panel is carried less than the cloth lying on the thigh.</summary>
        public float FarFollow = 0.5f;

        /// <summary>How quickly cloth hanging below a protrusion (a knee) falls back from it:
        /// sideways units given back per unit of depth.</summary>
        public float DrapeFall = 0.35f;

        /// <summary>
        /// How the bend grows with depth: offset ~ depth^BendPower. At 2 the top fifth of the cloth
        /// moves under 4% of what the hem does - fastened at the shoulders - and the lower half
        /// takes most of it, which is where a cape actually swings.
        /// </summary>
        const float BendPower = 2f;

        [SerializeField] SpriteRenderer _source;
        [SerializeField] SpriteRenderer _cloth;

        // NON-READONLY and rebuilt if null - a domain reload empties a Dictionary while the
        // renderers around it survive (CLAUDE.md, Domain reload traps).
        Dictionary<Sprite, Sprite> _wraps;
        Sprite _built;
        int[] _dx, _dy;   // _dx per TEXEL (row-major, rows from the bottom), _dy per row
        int _cols;
        List<Vector3> _pos;
        List<Vector2> _uv;
        float[] _rowShift, _ahead, _behind, _colU, _colShare;

        /// <summary>The bend on a layer's renderer, added (with its drawing child) the first time.</summary>
        public static ClothBend On(SpriteRenderer source)
        {
            var bend = source.GetComponent<ClothBend>();
            if (bend == null) bend = source.gameObject.AddComponent<ClothBend>();
            bend._source = source;
            if (bend._cloth == null)
            {
                var found = source.transform.Find("cloth");
                var go = found != null ? found.gameObject : new GameObject("cloth");
                go.transform.SetParent(source.transform, false);
                bend._cloth = go.GetComponent<SpriteRenderer>();
                if (bend._cloth == null) bend._cloth = go.AddComponent<SpriteRenderer>();
                bend._cloth.enabled = false;
            }
            return bend;
        }

        void LateUpdate()
        {
            if (_source == null || _cloth == null) return;

            var src = _source.sprite;
            if (!Active || src == null)
            {
                _source.forceRenderingOff = false;
                _cloth.enabled = false;
                return;
            }

            _source.forceRenderingOff = true;
            _cloth.enabled = _source.enabled;
            _cloth.color = _source.color;
            _cloth.flipX = _source.flipX;
            _cloth.flipY = _source.flipY;
            _cloth.sharedMaterial = _source.sharedMaterial;
            _cloth.sortingLayerID = _source.sortingLayerID;
            _cloth.sortingOrder = _source.sortingOrder;
            _cloth.maskInteraction = _source.maskInteraction;

            var wrap = Wrap(src);
            if (Offsets(src) || wrap != _built)
            {
                Build(src, wrap);
                _built = wrap;
                // The renderer keeps the mesh it was handed; hand it the rebuilt one.
                _cloth.sprite = null;
            }
            _cloth.sprite = wrap;
        }

        /// <summary>
        /// This frame's offset of every texel, in whole texels; true when any changed. Rows are
        /// counted from the sprite's BOTTOM, as its rect is. The sideways offset is per TEXEL
        /// (cloth over two legs moves two ways at once), the lift per ROW.
        /// </summary>
        bool Offsets(Sprite src)
        {
            int rows = Mathf.RoundToInt(src.rect.height);
            int cols = Mathf.RoundToInt(src.rect.width);
            if (_dx == null || _dy == null || _dy.Length != rows || _cols != cols)
            {
                _cols = cols;
                _dx = new int[rows * cols];
                _dy = new int[rows];
                for (int i = 0; i < _dx.Length; i++) _dx[i] = int.MinValue;   // force the first build
                _rowShift = new float[rows];
                _ahead = new float[rows];
                _behind = new float[rows];
                _colU = new float[cols];
                _colShare = new float[cols];
            }

            float ppu = src.pixelsPerUnit;
            float sx = Mathf.Max(1e-4f, Mathf.Abs(transform.localScale.x));
            float sy = Mathf.Max(1e-4f, Mathf.Abs(transform.localScale.y));
            float flip = _source.flipX ? -1f : 1f;
            float length = Mathf.Max(1e-4f, Length);
            bool changed = false;

            if (OverLegs)
            {
                // Across the cloth: how far toward its FRONT a column is (0 back .. 1 front), and
                // how much of the push it takes - all of it on the legs, less further out.
                float half = Mathf.Max(1e-4f, LegHalfWidth);
                float centre = 0.5f * (HipFront.x + HipBack.x);
                float onLegs = 0.5f * Mathf.Abs(HipFront.x - HipBack.x) + half;
                for (int c = 0; c < cols; c++)
                {
                    float x = transform.localPosition.x + flip * (c + 0.5f - src.pivot.x) / ppu * sx;
                    _colU[c] = Mathf.SmoothStep(0f, 1f, 0.5f + (x - centre) / (4f * onLegs));
                    float past = Mathf.Max(0f, Mathf.Abs(x - centre) - onLegs) / (3f * half);
                    _colShare[c] = Mathf.Lerp(FarFollow, 1f, Mathf.Exp(-past * past));
                }
            }

            // Top row first: a row may rise no less than the one above it, or a transparent
            // seam would open between them.
            int lift = 0;
            float sinPlumb = Mathf.Sin(Plumb);
            float ahead = 0f, behind = 0f, lastY = float.NaN;
            for (int r = rows - 1; r >= 0; r--)
            {
                float y = transform.localPosition.y + (r + 0.5f - src.pivot.y) / ppu * sy;
                float t = Mathf.Clamp01((HingeY - y) / length);

                float x = 0f;
                int dy = 0;
                if (t > 0f)
                {
                    float a = Mathf.Lerp(UpperRad, LowerRad, t);
                    float w = Mathf.Pow(t, BendPower);
                    x = length * w * Mathf.Sin(a)
                        + RippleAmp * t * Mathf.Sin(RipplePhase - t * 2f * Mathf.PI);
                    dy = Mathf.RoundToInt(length * w * (1f - Mathf.Cos(a)) / sy * ppu);
                }
                lift = Mathf.Max(lift, dy);
                if (_dy[r] != lift) changed = true;
                _dy[r] = lift;

                if (OverLegs)
                {
                    // Undo the lean below the waist (parent y 0): continuous there, exact below.
                    if (y < 0f) x += y * sinPlumb;
                    float f = LegShift(HipFront, ThighFront, ShinFront, y);
                    float b = LegShift(HipBack, ThighBack, ShinBack, y);
                    // Draped: a row reaches as far as the row above it, less a little.
                    float give = float.IsNaN(lastY) ? 0f : DrapeFall * (lastY - y);
                    ahead = Mathf.Max(Mathf.Max(0f, ahead - give), Mathf.Max(f, b));
                    behind = Mathf.Min(Mathf.Min(0f, behind + give), Mathf.Min(f, b));
                    lastY = y;
                    _ahead[r] = ahead;
                    _behind[r] = behind;
                }
                _rowShift[r] = x;
            }

            for (int r = 0; r < rows; r++)
            {
                int row = r * cols;
                for (int c = 0; c < cols; c++)
                {
                    float x = _rowShift[r];
                    if (OverLegs)
                    {
                        x += Mathf.Lerp(_behind[r], _ahead[r], _colU[c]) * _colShare[c];
                    }
                    int dx = Mathf.RoundToInt(flip * x / sx * ppu);
                    if (_dx[row + c] != dx) changed = true;
                    _dx[row + c] = dx;
                }
            }
            return changed;
        }

        /// <summary>
        /// How far a leg has carried the point of itself at parent height <paramref name="y"/>
        /// sideways from where a straight, standing leg would have it: along the thigh above the
        /// knee, along the shin below it, and the foot's own shift for cloth hanging past the sole.
        /// </summary>
        float LegShift(Vector2 hip, float thigh, float shin, float y)
        {
            if (y >= hip.y) return 0f;
            var knee = hip + ThighLength * new Vector2(Mathf.Sin(thigh), -Mathf.Cos(thigh));
            var foot = knee + ShinLength * new Vector2(Mathf.Sin(shin), -Mathf.Cos(shin));

            float x;
            if (y >= knee.y)
                x = Mathf.Lerp(hip.x, knee.x, (hip.y - y) / Mathf.Max(1e-5f, hip.y - knee.y));
            else if (y >= foot.y)
                x = Mathf.Lerp(knee.x, foot.x, (knee.y - y) / Mathf.Max(1e-5f, knee.y - foot.y));
            else
                x = foot.x;
            return x - hip.x;
        }

        /// <summary>
        /// One quad per run of texels in a row sharing an offset, the top row first so a lower
        /// row that has risen draws over the cloth above it rather than under. Where the next run
        /// is shifted further along, the gap is filled by repeating the last column of the run
        /// before it - the cloth STRETCHES by whole texels, it never tears open; where it is
        /// shifted back, the runs overlap. Rows whose texels share one offset (a cape, or any
        /// cloth with the legs still) collapse into one quad per run of rows, as before.
        /// </summary>
        void Build(Sprite src, Sprite wrap)
        {
            var rect = src.rect;
            var tex = src.texture;
            float ppu = src.pixelsPerUnit;
            var pivot = src.pivot;
            int rows = _dy.Length, cols = _cols;

            // Kept between builds: cloth over walking legs is rebuilt every frame.
            var pos = _pos ??= new List<Vector3>(256);
            var uv = _uv ??= new List<Vector2>(256);
            pos.Clear();
            uv.Clear();

            void Quad(int bottom, int top, float c0, float c1, float u0c, float u1c, int dx, int dy)
            {
                float ox = dx / ppu, oy = dy / ppu;
                float xa = (c0 - pivot.x) / ppu + ox, xb = (c1 - pivot.x) / ppu + ox;
                float yb = (bottom - pivot.y) / ppu + oy, yt = (top + 1 - pivot.y) / ppu + oy;
                float ua = (rect.x + u0c) / tex.width, ub = (rect.x + u1c) / tex.width;
                float vb = (rect.y + bottom) / tex.height, vt = (rect.y + top + 1) / tex.height;
                pos.Add(new Vector3(xa, yb, 0f)); uv.Add(new Vector2(ua, vb));
                pos.Add(new Vector3(xa, yt, 0f)); uv.Add(new Vector2(ua, vt));
                pos.Add(new Vector3(xb, yt, 0f)); uv.Add(new Vector2(ub, vt));
                pos.Add(new Vector3(xb, yb, 0f)); uv.Add(new Vector2(ub, vb));
            }

            bool Uniform(int r)
            {
                int row = r * cols, d = _dx[row];
                for (int c = 1; c < cols; c++) if (_dx[row + c] != d) return false;
                return true;
            }

            for (int top = rows - 1; top >= 0;)
            {
                if (Uniform(top))
                {
                    // A run of rows moving as one: a single quad, as the cape always drew.
                    int d = _dx[top * cols];
                    int bottom = top;
                    while (bottom > 0 && _dy[bottom - 1] == _dy[top] && Uniform(bottom - 1)
                           && _dx[(bottom - 1) * cols] == d) bottom--;
                    Quad(bottom, top, 0f, rect.width, 0f, rect.width, d, _dy[top]);
                    top = bottom - 1;
                    continue;
                }

                int r = top, at = r * cols;
                for (int c = 0; c < cols;)
                {
                    int d = _dx[at + c], e = c;
                    while (e + 1 < cols && _dx[at + e + 1] == d) e++;
                    Quad(r, r, c, e + 1, c, e + 1, d, _dy[r]);
                    // Stretch into the gap the next run leaves by moving further along.
                    if (e + 1 < cols && _dx[at + e + 1] > d)
                        Quad(r, r, e + 1, e + 1 + (_dx[at + e + 1] - d), e, e + 1, d, _dy[r]);
                    c = e + 1;
                }
                top--;
            }

            int quads = pos.Count / 4;
            var p = new NativeArray<Vector3>(pos.Count, Allocator.Temp);
            var t = new NativeArray<Vector2>(uv.Count, Allocator.Temp);
            var idx = new NativeArray<ushort>(quads * 6, Allocator.Temp);
            for (int i = 0; i < pos.Count; i++) { p[i] = pos[i]; t[i] = uv[i]; }
            for (int q = 0; q < quads; q++)
            {
                int v = q * 4, i = q * 6;
                idx[i + 0] = (ushort)(v + 0); idx[i + 1] = (ushort)(v + 1); idx[i + 2] = (ushort)(v + 2);
                idx[i + 3] = (ushort)(v + 0); idx[i + 4] = (ushort)(v + 2); idx[i + 5] = (ushort)(v + 3);
            }

            wrap.SetVertexCount(pos.Count);
            wrap.SetVertexAttribute(VertexAttribute.Position, p);
            wrap.SetVertexAttribute(VertexAttribute.TexCoord0, t);
            wrap.SetIndices(idx);

            p.Dispose();
            t.Dispose();
            idx.Dispose();
        }

        /// <summary>
        /// This character's own copy of a sprite, on the same texture. One per sprite it has been
        /// shown - the cape, its flash silhouette, its stone - and dropped when the set changes.
        /// </summary>
        Sprite Wrap(Sprite src)
        {
            _wraps ??= new Dictionary<Sprite, Sprite>();
            if (_wraps.TryGetValue(src, out var wrap) && wrap != null) return wrap;

            // A new cape, not a new state of the old one: let the old copies go.
            if (_wraps.Count >= 6) Release();

            var rect = src.rect;
            wrap = Sprite.Create(src.texture, rect,
                                 new Vector2(src.pivot.x / rect.width, src.pivot.y / rect.height),
                                 src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            wrap.name = src.name + ".cloth";
            _wraps[src] = wrap;
            if (_dx != null) for (int i = 0; i < _dx.Length; i++) _dx[i] = int.MinValue;
            return wrap;
        }

        void Release()
        {
            if (_wraps == null) return;
            foreach (var wrap in _wraps.Values)
                if (wrap != null && wrap != _cloth.sprite) Destroy(wrap);
            _wraps.Clear();
        }

        void OnDestroy()
        {
            if (_source != null) _source.forceRenderingOff = false;
            if (_cloth != null) _cloth.sprite = null;
            Release();
        }
    }
}
