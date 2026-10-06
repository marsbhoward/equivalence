using UnityEngine;
using Convergence.Core;

namespace Convergence.Art
{
    /// <summary>
    /// A small soft puff raised under a footfall, which drifts up a little, spreads, and fades.
    ///
    /// Raised by PrimitiveCharacterRig on the STRIDE's own clock, not on a timer of its own - see
    /// the footfall block there. Nothing else about it is shared with the rig: once dropped it is
    /// a loose object in the world that outlives the step, and parenting it to a character that
    /// is still walking would drag it along by the foot that made it.
    ///
    /// Deliberately not a ParticleSystem. One system per character configured from script, warmed
    /// up and torn down per run, is a great deal of machinery for a sprite that lives a third of
    /// a second - and the sorting order has to be a fixed number under every body, which a
    /// particle system makes harder to state than a SpriteRenderer does.
    /// </summary>
    public class DustPuff : MonoBehaviour
    {
        /// <summary>
        /// Under every body, above the floor and above pit art.
        ///
        /// Sitting above <see cref="SortingOrders.PitBase"/> rather than at GroundDecal is the
        /// same claim the pit band itself makes: a decal is painted ON the floor and dust is
        /// kicked up OFF it, so dust crossing a pit's lip has to stay visible over the hole.
        /// Still entirely below <see cref="SortingOrders.Enemy"/>, so nothing standing in it is
        /// ever drawn behind it.
        /// </summary>
        public const int Order = SortingOrders.PitBase + 4;

        const float Seconds = 0.34f;
        const float Rise = 0.16f;          // world units it drifts upward over its life
        const float StartSize = 0.13f;
        const float EndSize = 0.30f;
        const float PeakAlpha = 0.26f;

        static readonly Color Tone = new(0.72f, 0.68f, 0.60f);

        SpriteRenderer _sr;
        Vector3 _from;
        float _t;
        float _strength = 1f;

        /// <param name="strength">
        /// 0..1, how hard the step landed. Scales the puff's size and opacity together rather
        /// than either alone: a faint puff at full size reads as fog, and a small opaque one
        /// reads as a pebble.
        /// </param>
        public static void Raise(Vector3 at, float strength)
        {
            var go = new GameObject("dust.puff");
            go.transform.position = at;

            var p = go.AddComponent<DustPuff>();
            p._from = at;
            p._strength = Mathf.Clamp01(strength);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Glow;
            sr.sortingOrder = Order;
            p._sr = sr;

            // Placed once here rather than waited for: the object is created mid-frame and its
            // first Update is a frame away, so without this it draws once at full size and full
            // opacity before anything has faded it.
            p.Paint(0f);
        }

        void Update()
        {
            _t += Time.deltaTime;
            if (_t >= Seconds) { Destroy(gameObject); return; }
            Paint(_t / Seconds);
        }

        void Paint(float k)
        {
            // Out, not in-out: the puff is thrown clear by the step and then slows, so most of
            // the travel belongs to the first few frames. An eased-in drift reads as smoke being
            // released rather than as dust being displaced.
            float e = 1f - (1f - k) * (1f - k);

            transform.position = _from + new Vector3(0f, Rise * _strength * e, 0f);

            float size = Mathf.Lerp(StartSize, EndSize, e) * Mathf.Lerp(0.6f, 1f, _strength);
            transform.localScale = new Vector3(size, size * 0.62f, 1f);

            var c = Tone;
            c.a = PeakAlpha * _strength * (1f - k);
            if (_sr != null) _sr.color = c;
        }
    }
}
