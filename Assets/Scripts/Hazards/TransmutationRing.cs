using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// THE TRANSMUTATION CIRCLE on the arena floor (the user's design, 2026-10-05): where a held
    /// Nigredo - a cost at max stacks - is transmuted into its Albedo. Not the hub's transmog circle
    /// (Hub.TransmutationCircle), which shares the look and nothing else.
    ///
    /// Drawn like a spire's ring, from the START of the floor, on a combat floor the run chose for
    /// it (GameBootstrap.CircleDue). It ACTIVATES after three combat contacts made while standing in
    /// it - a hit landed on an enemy, or a hit taken from one - so the transmutation is done
    /// mid-fight, never in the calm after. Each contact lights one of the three principle marks,
    /// Sulfur, Mercury, Salt, so progress reads at a glance. A floor cleared before the third
    /// fades it, and the Nigredo waits for the next circle.
    ///
    /// The circle knows nothing of the exchange: GameBootstrap feeds it contacts and POLLS
    /// <see cref="TryActivate"/> (a callback would not survive a domain reload). Every cached field
    /// is serialized and non-readonly, for FloorPit's reason.
    /// </summary>
    public class TransmutationRing : MonoBehaviour
    {
        public const float Radius = 2.1f;
        public const int ContactsNeeded = 3;

        static readonly Color Gold = new(0.86f, 0.72f, 0.38f);
        static readonly Color Dim = new(0.62f, 0.6f, 0.66f);

        [SerializeField] Transform _player;
        [SerializeField] SpriteRenderer _outer, _inner, _fill;
        [SerializeField] SpriteRenderer[] _marks;
        [SerializeField] int _lit;
        [SerializeField] bool _claimed, _fading, _spent;
        [SerializeField] float _fade = 1f, _flash;

        public Vector2 Centre => transform.position;
        public int Lit => _lit;
        public bool Spent => _spent || _fading;

        public bool PlayerInside
            => _player != null && Vector2.Distance(_player.position, transform.position) <= Radius;

        public static TransmutationRing Spawn(Vector2 at, Transform player, Transform parent)
        {
            var go = new GameObject("transmutation-ring");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var r = go.AddComponent<TransmutationRing>();
            r._player = player;

            const int ring = SortingOrders.PitBase + 5;   // over pits, as a spire's ring is
            r._fill = Disc(go.transform, "fill", Radius * 2f, Gold, ring - 1, Spr.Circle);
            r._outer = Disc(go.transform, "outer", Radius * 2f, Gold, ring, Spr.ThinRing);
            r._inner = Disc(go.transform, "inner", Radius * 1.52f, Gold, ring, Spr.ThinRing);

            var marks = new[] { Combat.Glyphs.PrimeMark.Sulfur, Combat.Glyphs.PrimeMark.Mercury, Combat.Glyphs.PrimeMark.Salt };
            r._marks = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad;
                var m = new GameObject(marks[i].ToString());
                m.transform.SetParent(go.transform, false);
                m.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (Radius * 0.76f);
                m.transform.localScale = Vector3.one * (Radius * 0.48f);
                var sr = m.AddComponent<SpriteRenderer>();
                sr.sprite = Combat.Glyphs.Prime(marks[i]);
                sr.sortingOrder = ring + 1;
                r._marks[i] = sr;
            }
            r.Paint();
            return r;
        }

        static SpriteRenderer Disc(Transform parent, string name, float size, Color color, int order, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>A combat contact - a hit landed or taken - while the player stands in the ring
        /// lights the next mark. Anywhere else, nothing.</summary>
        public void Contact()
        {
            if (Spent || _lit >= ContactsNeeded || !PlayerInside) return;
            _lit++;
            _flash = 1f;
            if (_lit < ContactsNeeded)
                Spr.Pulse(transform, Radius * 0.6f, new Color(Gold.r, Gold.g, Gold.b, 0.6f), 0.3f, true, 0.6f);
        }

        /// <summary>True ONCE, when the third mark is lit - the run then transmutes.</summary>
        public bool TryActivate()
        {
            if (_claimed || Spent || _lit < ContactsNeeded) return false;
            _claimed = true;
            return true;
        }

        /// <summary>The transmutation happened: the circle flares white and goes.</summary>
        public void Complete()
        {
            _spent = true;
            Spr.Flash(transform.position, Radius * 1.4f, Color.white, 0.6f);
            Spr.Flash(transform.position, Radius, Gold, 0.5f);
            _fading = true;
        }

        /// <summary>The floor cleared before it was lit: it fades, harmless, and waits for another floor.</summary>
        public void Fade() => _fading = true;

        void Update()
        {
            if (_outer == null || _marks == null) return;
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 3f);
            if (_fading)
            {
                _fade -= Time.deltaTime / 0.8f;
                if (_fade <= 0f) { Destroy(gameObject); return; }
            }
            Paint();
        }

        void Paint()
        {
            float inside = PlayerInside && !Spent ? 1f : 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (inside > 0f ? 5f : 2f));
            var ring = Color.Lerp(Dim, Gold, 0.5f + 0.5f * inside);
            ring.a = (0.35f + 0.35f * pulse + 0.3f * _flash) * _fade;
            if (_outer != null) _outer.color = ring;
            if (_inner != null) _inner.color = new Color(ring.r, ring.g, ring.b, ring.a * 0.7f);
            if (_fill != null) _fill.color = new Color(Gold.r, Gold.g, Gold.b, (0.04f + 0.05f * inside) * _fade);

            for (int i = 0; i < _marks.Length; i++)
            {
                if (_marks[i] == null) continue;
                bool on = i < _lit;
                var c = on ? Color.Lerp(Gold, Color.white, _flash * 0.6f) : Dim;
                c.a = (on ? 0.95f : 0.28f + 0.12f * pulse) * _fade;
                _marks[i].color = c;
            }
        }
    }
}
