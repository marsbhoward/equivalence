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


        [SerializeField] Transform _player;
        [SerializeField] CircleArt _art;
        [SerializeField] float _completeAge = -1f;
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

            // Cut into the floor: a ring you stand in (the user's pick, group 4 - "three glyphs").
            int rt = Mathf.CeilToInt(Radius * Combat.TexelFx.Body) + 2;
            r._art = Combat.TexelPicture.Attach<CircleArt>(go.transform, "circle", Vector2.zero, rt, rt, rt, SortingOrders.GroundCarved, clip: true);
            return r;
        }

        /// <summary>A combat contact - a hit landed or taken - while the player stands in the ring
        /// lights the next mark. Anywhere else, nothing.</summary>
        public void Contact()
        {
            if (Spent || _lit >= ContactsNeeded || !PlayerInside) return;
            _lit++;
            _flash = 1f;   // the glyph lights white, then settles gold
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
            _completeAge = 0f;   // gold runs round, it flashes white and burns down (CircleArt)
        }

        /// <summary>The floor cleared before it was lit: it fades, harmless, and waits for another floor.</summary>
        public void Fade() => _fading = true;

        // What the picture reads.
        internal float Flash => _flash;
        internal float FadeShare => _fade;
        internal float CompleteAge => _completeAge;
        internal bool Inside => PlayerInside && !Spent;

        void Update()
        {
            _flash = Mathf.Max(0f, _flash - Time.deltaTime * 3f);
            if (_completeAge >= 0f)
            {
                _completeAge += Time.deltaTime;
                if (_completeAge > 0.7f) _fading = true;
            }
            if (_fading)
            {
                _fade -= Time.deltaTime / 0.8f;
                if (_fade <= 0f) { Destroy(gameObject); return; }
            }
        }
    }

    /// <summary>
    /// The transmutation circle (the user's pick, group 4 - "three glyphs"): a double ring CUT into
    /// the floor with the three principles' glyphs between its grooves. Each contact lights one gold
    /// (white for a moment first). When it transmutes, gold runs round the outer groove from the
    /// top, the whole circle flashes white and burns down. Its grooves warm toward gold while the
    /// player stands in it.
    /// </summary>
    public class CircleArt : Combat.TexelPicture
    {
        [SerializeField] TransmutationRing _r;

        /// <summary>The three principles' glyphs, five texels square, top row first (shared with the hub's circle).</summary>
        internal static readonly string[][] Glyph =
        {
            new[] { "..x..", ".x.x.", "xxxxx", "..x..", ".xxx." },   // Sulfur: the triangle over the cross
            new[] { "x...x", ".xxx.", ".x.x.", ".xxx.", "..x.." },   // Mercury: horns, the circle, the cross
            new[] { ".xxx.", "x...x", "xxxxx", "x...x", ".xxx." },   // Salt: the circle barred
        };
        static readonly Color32 GoldLit = new(242, 194, 90, 255), GoldHot = new(255, 227, 154, 255), White = new(255, 255, 255, 255);

        protected override bool Draw()
        {
            if (_r == null) _r = GetComponentInParent<TransmutationRing>();
            if (_r == null) return false;
            float fade = _r.FadeShare, R = TransmutationRing.Radius * Ppu, Ri = R * 0.74f;
            var lip = Lerp(Combat.TexelFx.GrooveLip, GoldLit, _r.Inside ? 0.35f + 0.15f * Mathf.Sin(Age * 5f) : 0f);
            Groove(R, 3, lip, fade);
            Groove(Ri, 2, lip, fade);

            for (int i = 0; i < 3; i++)
            {
                float a = (90f + i * 120f) * Mathf.Deg2Rad, rr = (R - 3f + Ri) * 0.5f;
                int gx = Mathf.RoundToInt(Mathf.Cos(a) * rr), gy = Mathf.RoundToInt(Mathf.Sin(a) * rr);
                bool on = i < _r.Lit;
                bool newest = on && i == _r.Lit - 1;
                var c = on ? (newest && _r.Flash > 0.5f ? White : GoldLit) : Combat.TexelFx.Groove;
                for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 5; x++)
                    {
                        if (Glyph[i][4 - y][x] != 'x') continue;
                        for (int sy = 0; sy < 2; sy++)
                            for (int sx = 0; sx < 2; sx++)
                                Over(gx - 5 + x * 2 + sx, gy - 5 + y * 2 + sy, c, fade);
                    }
            }

            // Transmuting: gold runs round the outer groove from the top, then white, then burns down.
            float t = _r.CompleteAge;
            if (t >= 0f)
            {
                float run = Mathf.Clamp01(t / 0.25f) * Mathf.PI * 2f;
                Color32 col = t < 0.25f ? GoldHot : t < 0.35f ? White : GoldLit;
                float a = t < 0.35f ? 1f : 1f - Mathf.Clamp01((t - 0.35f) / 0.35f);
                Light(R, 3, col, a * fade, run);
                if (t >= 0.25f) Light(Ri, 2, col, a * fade, Mathf.PI * 2f);
            }
            return true;
        }

        void Groove(float r, int w, Color32 lip, float fade)
            => Ring(0, 0, r - w, r, (x, y, d, ang) => Over(x, y, (y > 0 ? d > r - 1f : d < r - w + 1f) ? lip : Combat.TexelFx.Groove, fade));

        void Light(float r, int w, Color32 c, float a, float upTo)
            => Ring(0, 0, r - w + 0.6f, r - 0.4f, (x, y, d, ang) =>
            {
                if (Mathf.Repeat(Mathf.PI * 0.5f - ang, Mathf.PI * 2f) <= upTo) Over(x, y, c, a);
            });
    }
}
