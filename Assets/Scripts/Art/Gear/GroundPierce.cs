using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// Draws the weapon with everything below a GROUND LINE cut away, so a blade driven point-down
    /// reads as sunk into the floor (Sunder's slam). ClothBend's pattern: the weapon's own
    /// renderer - which the flash, the stone, Apply and depth sorting all talk to - is never
    /// changed, only stopped from drawing (forceRenderingOff), and a child draws a cut copy of
    /// whatever it shows. The copy is a Sprite over a sub-rect of the SAME texture (rows from the
    /// grip end up to the cut), so the art stays on its own grid and nothing is resampled.
    ///
    /// The cut is taken along the blade's own axis (the sprite's up): the rows whose point on the
    /// blade lies below <see cref="GroundY"/> go. Once the blade comes to rest pointing down, cracks
    /// are left where it went in (Combat/GroundCracks). Driven by PrimitiveCharacterRig
    /// (TickGroundPierce), which sets the public fields every frame - plain values, so a domain
    /// reload keeps them.
    /// </summary>
    [DefaultExecutionOrder(1001)]   // after DepthSorted (and ClothBend), whose order it copies
    public class GroundPierce : MonoBehaviour
    {
        /// <summary>Off: the weapon draws itself, whole.</summary>
        public bool Active;

        /// <summary>The ground line, world y.</summary>
        public float GroundY;

        SpriteRenderer _source, _copy;
        bool _cracked;
        Vector2 _lastEntry;
        Vector2 _down;   // the blade's direction this frame, unit

        /// <summary>Settled: within about 20 degrees of straight down, and the entry point moved
        /// less than a body texel since last frame.</summary>
        const float SettledCos = 0.94f, SettledStep = 1f / 37.5f;

        // Cut sprites by (source, rows kept). Non-readonly and rebuilt if a reload empties it.
        static Dictionary<(Sprite, int), Sprite> _cuts = new();

        public static GroundPierce On(SpriteRenderer source)
        {
            var p = source.GetComponent<GroundPierce>();
            if (p == null) p = source.gameObject.AddComponent<GroundPierce>();
            p._source = source;
            if (p._copy == null)
            {
                var found = source.transform.Find("pierced");
                var go = found != null ? found.gameObject : new GameObject("pierced");
                go.transform.SetParent(source.transform, false);
                p._copy = go.GetComponent<SpriteRenderer>();
                if (p._copy == null) p._copy = go.AddComponent<SpriteRenderer>();
                p._copy.enabled = false;
            }
            return p;
        }

        void LateUpdate()
        {
            if (_source == null || _copy == null) return;
            var src = _source.sprite;
            Vector2 entry = default;
            int keep = Active && src != null ? RowsAbove(src, out entry) : -1;
            if (keep < 0)
            {
                Release();
                return;
            }

            // The ground breaks where the blade COMES TO REST in it. Not the first frame it is
            // cut: the strike sweeps down out in front, and its tip dips under the line a long
            // way ahead of where the blade ends up standing.
            if (!_cracked && _down.y < -SettledCos && (entry - _lastEntry).sqrMagnitude < SettledStep * SettledStep)
            {
                Combat.GroundCracks.Spawn(entry);
                _cracked = true;
            }
            _lastEntry = entry;

            _source.forceRenderingOff = true;
            _copy.enabled = _source.enabled;
            _copy.color = _source.color;
            _copy.flipX = _source.flipX;
            _copy.flipY = _source.flipY;
            _copy.sharedMaterial = _source.sharedMaterial;
            _copy.sortingLayerID = _source.sortingLayerID;
            _copy.sortingOrder = _source.sortingOrder;
            _copy.maskInteraction = _source.maskInteraction;
            _copy.sprite = keep == 0 ? null : Cut(src, keep);
        }

        void Release()
        {
            if (_source != null) _source.forceRenderingOff = false;
            if (_copy != null) _copy.enabled = false;
            _cracked = false;
            _lastEntry = new Vector2(float.MaxValue, float.MaxValue);
        }

        void OnDisable() => Release();

        /// <summary>
        /// How many rows of the sprite, counted from its BOTTOM, are above the ground - or -1 when
        /// the whole sprite is (nothing to cut). Assumes the tip is at the top of the art (every
        /// sword is authored tip up) and the grip low.
        /// </summary>
        int RowsAbove(Sprite src, out Vector2 entry)
        {
            entry = default;
            var t = _source.transform;
            float ppu = src.pixelsPerUnit;
            var rect = src.rect;
            int rows = Mathf.RoundToInt(rect.height);
            // World y of the sprite's row r (its centre line along the blade): the pivot, plus r
            // texels along the sprite's up.
            Vector2 pivotWorld = t.position;
            Vector2 up = t.TransformVector(Vector3.up / ppu * (_source.flipY ? -1f : 1f));
            _down = up.normalized;
            float pivotRow = src.pivot.y;
            float yTop = pivotWorld.y + up.y * (rows - pivotRow);
            if (yTop >= GroundY) return -1;            // the tip is above the ground: nothing to cut
            if (up.y >= -1e-5f) return -1;             // not pointing down

            // The row where the blade's axis crosses the ground line.
            float cross = pivotRow + (GroundY - pivotWorld.y) / up.y;
            int keep = Mathf.Clamp(Mathf.FloorToInt(cross), 0, rows);
            entry = pivotWorld + up * (cross - pivotRow);
            return keep;
        }

        static Sprite Cut(Sprite src, int keep)
        {
            if (_cuts == null) _cuts = new Dictionary<(Sprite, int), Sprite>();
            if (_cuts.TryGetValue((src, keep), out var cut) && cut != null) return cut;
            if (_cuts.Count > 256) _cuts.Clear();   // a weapon swap leaves the old ones behind

            var rect = src.rect;
            var sub = new Rect(rect.x, rect.y, rect.width, keep);
            var pivot = new Vector2(src.pivot.x / rect.width, src.pivot.y / keep);
            cut = Sprite.Create(src.texture, sub, pivot, src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cut.name = src.name + ".pierced" + keep;
            _cuts[(src, keep)] = cut;
            return cut;
        }
    }
}
