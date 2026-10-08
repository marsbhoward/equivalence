using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// A piece's SPIRIT: the picture it shows once its stone has fallen away in a weapon art - the
    /// Aether Dual Discs' heads on a ring of energy, the Geode Stone's bare crystals. Same size, pivot
    /// and density as the stone picture it stands in for, so one can be swapped for the other on
    /// any renderer without the rig noticing.
    ///
    /// Keyed by the STONE sprite, registered where the art is built (DemoGear). Static and rebuilt
    /// with the catalogue: a domain reload clears it and the rebuild registers it again.
    /// </summary>
    public static class GearSpirit
    {
        static readonly Dictionary<Sprite, Sprite> _spirits = new();

        public static void Register(Sprite stone, Sprite spirit)
        {
            if (stone != null && spirit != null) _spirits[stone] = spirit;
        }

        /// <summary>The spirit picture for <paramref name="stone"/>, or null if it has none.</summary>
        public static Sprite Of(Sprite stone)
            => stone != null && _spirits.TryGetValue(stone, out var s) ? s : null;
    }

    /// <summary>
    /// Shows another picture on ONE renderer for a while, whatever owns it: each frame the renderer
    /// shows <c>_from</c>, it is put back to <c>_to</c> - so a hit flash (which swaps the sprite and
    /// back) or a repaint does not end it - until <see cref="Remove"/>, or a backstop lifetime, puts
    /// the original back. Runs before KindledMarks, so the Secret Fire burns in the picture shown.
    /// </summary>
    [DefaultExecutionOrder(900)]
    public class SpriteOverride : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] Sprite _from, _to;
        [SerializeField] float _until;

        /// <summary>Show <paramref name="to"/> on <paramref name="sr"/> in place of what it shows now,
        /// for at most <paramref name="maxSeconds"/> (scaled).</summary>
        public static SpriteOverride Apply(SpriteRenderer sr, Sprite to, float maxSeconds = 8f)
        {
            if (sr == null || sr.sprite == null || to == null) return null;
            var o = sr.gameObject.AddComponent<SpriteOverride>();
            o._sr = sr;
            o._from = sr.sprite;
            o._to = to;
            o._until = Time.time + maxSeconds;
            sr.sprite = to;
            return o;
        }

        /// <summary>The picture it stands in for.</summary>
        public Sprite Original => _from;

        public void Remove()
        {
            if (_sr != null && _sr.sprite == _to) _sr.sprite = _from;
            Destroy(this);
        }

        void LateUpdate()
        {
            if (_sr == null) { Destroy(this); return; }
            if (Time.time > _until) { Remove(); return; }
            if (_sr.sprite == _from) _sr.sprite = _to;
        }
    }
}
