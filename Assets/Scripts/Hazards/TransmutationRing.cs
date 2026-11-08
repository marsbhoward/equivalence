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
    /// it (GameBootstrap.CircleDue). It LIGHTS after three combat contacts made while standing in
    /// it - a hit landed on an enemy, or a hit taken from one - so it is EARNED mid-fight. Each
    /// contact lights one of the three principle marks, Sulfur, Mercury, Salt, so progress reads at
    /// a glance. A floor cleared before the third fades it, and the Nigredo waits for the next circle.
    ///
    /// LIT, IT WAITS TO BE USED (the user's call, 2026-11-07): it stays lit until the player leaves
    /// the floor, fight or no fight, and transmutes only when the player stands in it and presses
    /// USE - the Albedo screen always opens, one Nigredo or several. It used to transmute on the
    /// third contact by itself: with one Nigredo there was no screen, so a player saw it light and
    /// found nothing to do; with several, a third contact on the floor's last blow lost the circle
    /// to the clear.
    ///
    /// The circle knows nothing of the exchange: GameBootstrap feeds it contacts and POLLS
    /// <see cref="Ready"/> (a callback would not survive a domain reload). Every cached field is
    /// serialized and non-readonly, for FloorPit's reason.
    /// </summary>
    public class TransmutationRing : MonoBehaviour
    {
        public const float Radius = 2.1f;
        public const int ContactsNeeded = 3;


        [SerializeField] Transform _player;
        [SerializeField] CircleArt _art;
        [SerializeField] float _completeAge = -1f;
        [SerializeField] int _lit;
        [SerializeField] bool _announced, _fading, _spent;
        [SerializeField] float _fade = 1f, _flash;
        /// <summary>The ring's own clock, and when the newest mark was lit on it - the Tree of Life's
        /// pillar for that contact draws in from then (CircleArt).</summary>
        [SerializeField] float _clock, _litAt;

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

            // Cut into the floor: a ring you stand in, at the character's density (Tuning.Circle).
            int rt = Mathf.CeilToInt(Radius * Tuning.Circle.Ppu) + 2;
            r._art = Combat.TexelPicture.Attach<CircleArt>(go.transform, "circle", Vector2.zero, rt, rt, rt, SortingOrders.GroundCarved,
                Tuning.Circle.Ppu, clip: true);
            return r;
        }

        /// <summary>A combat contact - a hit landed or taken - while the player stands in the ring
        /// lights the next mark. Anywhere else, nothing.</summary>
        public void Contact()
        {
            if (Spent || _lit >= ContactsNeeded || !PlayerInside) return;
            _lit++;
            _flash = 1f;   // the glyph lights white, then settles gold
            _litAt = _clock;
        }

        /// <summary>All three marks lit and not yet used: standing in it, USE transmutes.</summary>
        public bool Ready => !Spent && _lit >= ContactsNeeded;

        /// <summary>True ONCE, the first time it is <see cref="Ready"/> - the run says so.</summary>
        public bool JustLit()
        {
            if (_announced || !Ready) return false;
            _announced = true;
            return true;
        }

        /// <summary>The transmutation happened: the light gathers in from the rim to the tree, and it burns down.</summary>
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
        internal float LitAge => _clock - _litAt;

        void Update()
        {
            _clock += Time.deltaTime;
            _flash = Mathf.Max(0f, _flash - Time.deltaTime / Tuning.Circle.FlashSeconds);
            if (_completeAge >= 0f)
            {
                _completeAge += Time.deltaTime;
                if (_completeAge > Tuning.Circle.BurnAt) _fading = true;
            }
            if (_fading)
            {
                _fade -= Time.deltaTime / Tuning.Circle.BurnSeconds;
                if (_fade <= 0f) { Destroy(gameObject); return; }
            }
        }
    }

    /// <summary>
    /// The arena's transmutation circle (the user's picks off the circle mock, 2026-11-01; drawn by
    /// <see cref="CircleFigureArt"/>): today's grooves with the sigil inside, warming while the player
    /// stands in it, the glyphs pulsing in turn. Each contact lights a third of the rim clockwise from
    /// the top and its glyph (white a moment, then gold), and draws a pillar of the Tree of Life over
    /// the sigil - left, right, then the middle. Transmuting, a ring of light gathers in from the rim,
    /// taking the light from what it passes, and the tree flares as it arrives; then it burns down.
    /// </summary>
    public class CircleArt : CircleFigureArt
    {
        [SerializeField] TransmutationRing _r;

        protected override bool Draw()
        {
            if (_r == null) _r = GetComponentInParent<TransmutationRing>();
            if (_r == null) return false;
            float fade = _r.FadeShare, R = Outer(TransmutationRing.Radius), Ri = Inner(R);
            float t = _r.CompleteAge;
            bool gathering = t >= 0f;
            float waveR = gathering ? WaveR(R, t) : float.MaxValue;
            bool inside = _r.Inside;
            int lit = _r.Lit;

            var lip = inside ? Lerp(Combat.TexelFx.GrooveLip, GoldLit, 0.35f + 0.15f * Mathf.Sin(Age * 5f)) : Combat.TexelFx.GrooveLip;
            (Color32, Color32) Tones(int x, int y)
                => (Combat.TexelFx.Groove, gathering && Taken(R, waveR, Mathf.Sqrt(x * x + y * y)) ? Combat.TexelFx.GrooveLip : lip);
            Groove(R, Tuning.Circle.OuterGroove, Vector2.zero, Tones, fade);
            Groove(Ri, Tuning.Circle.InnerGroove, Vector2.zero, Tones, fade);
            var (sigil, sr) = SigilRings(R);
            foreach (var c in sigil) Groove(sr, Tuning.Circle.SigilGroove, c, Tones, fade);

            // the rim, a third a contact - its light taken once the gathering ring has left it
            if (lit > 0 && !(gathering && Taken(R, waveR, R)))
                Paint(R, Tuning.Circle.OuterGroove, Vector2.zero, GoldLit, fade, (x, y) => FromTop(x, y) < lit * Tau / 3f);

            // the tree over the sigil, a pillar a contact
            if (lit > 0)
            {
                float Drawn(int p) => p < lit - 1 ? 1f : p == lit - 1 ? Mathf.Clamp01(_r.LitAge / Tuning.Circle.TreeDrawSeconds) : 0f;
                Tree(Ri, Drawn, p => Drawn(p) < 1f ? GoldHot : p == lit - 1 && _r.Flash > 0.5f ? White : GoldLit, fade);
            }

            // transmuting: the ring of light, then the tree's flare
            if (gathering)
            {
                if (t < Tuning.Circle.WaveSeconds) { GatheringRing(R, waveR, fade); LightUnderRing(R, waveR, fade, lit > 0); }
                else { var (c, a) = Flare(t - Tuning.Circle.WaveSeconds); Tree(Ri, _ => 1f, _ => c, a * fade); }
            }

            for (int i = 0; i < 3; i++)
            {
                var g = Polar(Band(R), GlyphAngles[i]);
                Color32 c = Combat.TexelFx.Groove;
                if (i < lit) c = i == lit - 1 && _r.Flash > 0.5f ? White : GoldLit;
                else if (inside) c = Lerp(Combat.TexelFx.Groove, GoldLit, 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Age * Tuning.Circle.BeatSpeed - i * 2.094f)));
                if (gathering && Taken(R, waveR, g.magnitude)) c = Combat.TexelFx.Groove;
                if (gathering && t < Tuning.Circle.WaveSeconds && OnWave(R, waveR, g.magnitude) > 0.3f) c = White;
                Glyph(i, g.x, g.y, c, fade);
            }
            return true;
        }
    }
}
