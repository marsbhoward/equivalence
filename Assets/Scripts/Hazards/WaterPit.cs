using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A flooded basin. No damage, no slow - it takes away GRIP: a body in it keeps the speed it
    /// has and loses most of its say over where that speed goes (<see cref="FloorPit.Grip"/>,
    /// read by PlayerController and EnemyController's steering). Walk in and you carry on past
    /// where you meant to stop; turn and you drift wide.
    ///
    /// And it SOAKS the player (<see cref="FloorPit.Soaks"/>): everything hits harder while they
    /// stand in it and for a few seconds after (Tuning.Hazards.WaterSoak*). The slide alone was a
    /// price a skilled player simply steered round; a soaked player pays for the wade in the
    /// fight that follows it.
    ///
    /// THE COST IS PRECISION, NOT TIME. Sand makes the ground you retreat across expensive; water
    /// makes it UNRELIABLE - you get across at full speed, but you arrive where your momentum
    /// was going, which with a pack closing is not always where you wanted to be. It composes
    /// with the rest without arguing: a slide that carries you into fire or over jacks is the
    /// puddle's real threat, and enemies lose their footing in it too.
    ///
    /// GROUND, so it is ASKED FOR (<see cref="FloorPits.GripAt"/>) rather than written onto the
    /// body, for every reason speed is. Surefooted (the boon promising "no slide") ignores it.
    ///
    /// ITS RIPPLES ARE THE READOUT. A body moving through throws a wake at its feet, so the slide
    /// is visible while it happens rather than only felt; the surface ripples on its own too, so
    /// a still puddle never reads as a blue rug.
    /// </summary>
    public class WaterPit : FloorPit
    {
        // Serialized and not readonly - see FloorPit's own note. A ripple pool that came back
        // empty after a reload would leave the rings frozen mid-spread on screen.
        [SerializeField] List<Ripple> _ripples = new();
        [SerializeField] SpriteRenderer _glints;
        [SerializeField] Vector2 _size;
        [SerializeField] float _nextRipple, _wake, _clock;
        [SerializeField] int _nextSlot;

        [System.Serializable]
        struct Ripple
        {
            public Transform T;
            public SpriteRenderer Sr;
            public float Age, Size;
            public bool Live;
        }

        const int RipplePool = 10;

        public override float Grip => Tuning.Hazards.WaterGrip;
        public override bool Soaks => true;

        public override PitKind Kind => PitKind.Water;
        public override float RouteCost => Tuning.Steering.WaterRouteCost;

        public static WaterPit Spawn(Rect rect, Transform player, Transform parent)
        {
            var go = new GameObject("pit.water");
            go.transform.SetParent(parent, false);
            var pit = go.AddComponent<WaterPit>();
            pit.Init(rect, player);
            pit.Build(rect.size);
            return pit;
        }

        static readonly Color WaterDeep = new(0.08f, 0.19f, 0.31f);
        static readonly Color WaterShallow = new(0.24f, 0.48f, 0.64f);
        static readonly Color WaterLight = new(0.84f, 0.95f, 1f);
        static readonly Color RippleColor = new(0.78f, 0.92f, 1f);

        void Build(Vector2 size)
        {
            _size = size;
            int seed = Random.Range(1, 9999);
            _clock = Random.value * 10f;

            // Three layers, as sand: the deep body, a slow mottle of shallower water over it (the
            // bottom showing through unevenly), and the lit glints on the surface - which shimmer,
            // being the one part of still water that moves.
            PitArt.Quad(gameObject, Spr.Square, WaterDeep, size, OrderFloor, "floor");
            // Kept LOW-contrast: at full strength the mottle's one-unit tile repeats visibly and
            // the basin reads as a patterned blue rug rather than as depth.
            PitArt.Tiled(gameObject, PitArt.Mottle(seed), WithAlpha(WaterShallow, 0.35f), size, OrderMaterial, "shallows");
            _glints = PitArt.Tiled(gameObject, PitArt.Glints(seed), WithAlpha(WaterLight, 0.6f), size, OrderDetail, "glints");

            BuildRecess(size, Tuning.Hazards.WaterRecessDepth);

            for (int i = 0; i < RipplePool; i++)
            {
                var sr = PitArt.Quad(gameObject, Spr.Ring, Color.clear, Vector2.one, OrderAbove, "ripple");
                _ripples.Add(new Ripple { T = sr.transform, Sr = sr });
            }
            _nextRipple = Random.Range(0f, Tuning.Hazards.WaterRippleIntervalMax);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _clock += dt;

            if (_glints != null)
            {
                float a = 0.45f + 0.2f * Mathf.Sin(_clock * 2.3f) + 0.1f * Mathf.Sin(_clock * 5.1f + 1.3f);
                _glints.color = WithAlpha(WaterLight, a);
            }

            // The surface's own ripples: somewhere random, on an irregular beat.
            _nextRipple -= dt;
            if (_nextRipple <= 0f)
            {
                _nextRipple = Random.Range(Tuning.Hazards.WaterRippleIntervalMin, Tuning.Hazards.WaterRippleIntervalMax);
                var local = new Vector2(Random.Range(-0.5f, 0.5f) * _size.x, Random.Range(-0.5f, 0.5f) * _size.y);
                Emit(local, 0.75f);
            }

            // The wake: measured off the BODY's velocity, not the stick - a player sliding with
            // the stick released is exactly the case the wake exists to show.
            if (PlayerAlive && PlayerBody != null && Contains(PlayerBody.position)
                && PlayerBody.linearVelocity.magnitude > Tuning.Hazards.WaterWakeSpeed)
            {
                _wake -= dt;
                if (_wake <= 0f)
                {
                    _wake = Tuning.Hazards.WaterWakeInterval;
                    Emit(PlayerBody.position - (Vector2)transform.position, 0.6f);
                }
            }
            else _wake = 0f;

            TickRipples(dt);
        }

        /// <summary>
        /// Starts a ring at <paramref name="local"/>, shrunk so it never grows past the lip - a
        /// ripple spilling onto dry stone would say the water is bigger than the hazard is.
        /// Reuses the oldest ring when the pool is full.
        /// </summary>
        void Emit(Vector2 local, float sizeScale)
        {
            if (_ripples == null || _ripples.Count == 0) return;
            float hx = _size.x * 0.5f - 0.08f, hy = _size.y * 0.5f - 0.08f;
            local.x = Mathf.Clamp(local.x, -hx, hx);
            local.y = Mathf.Clamp(local.y, -hy, hy);
            float room = Mathf.Min(hx - Mathf.Abs(local.x), hy - Mathf.Abs(local.y)) * 2f;
            float size = Mathf.Min(Tuning.Hazards.WaterRippleSize * sizeScale, room);
            if (size < 0.12f) return;

            int i = _nextSlot % _ripples.Count;
            _nextSlot = (i + 1) % _ripples.Count;
            var r = _ripples[i];
            if (r.T == null) return;
            r.T.localPosition = new Vector3(local.x, local.y, 0f);
            r.Age = 0f;
            r.Size = size;
            r.Live = true;
            _ripples[i] = r;
        }

        void TickRipples(float dt)
        {
            float life = Tuning.Hazards.WaterRippleSeconds;
            for (int i = 0; i < _ripples.Count; i++)
            {
                var r = _ripples[i];
                if (!r.Live || r.T == null) continue;
                r.Age += dt;
                if (r.Age >= life)
                {
                    r.Live = false;
                    if (r.Sr != null) r.Sr.color = Color.clear;
                    _ripples[i] = r;
                    continue;
                }
                float k = r.Age / life;
                // Eased out: a ripple leaves fast and slows as it spreads.
                float grow = 1f - (1f - k) * (1f - k);
                r.T.localScale = Vector3.one * Mathf.Lerp(0.15f, r.Size, grow);
                if (r.Sr != null) r.Sr.color = WithAlpha(RippleColor, 0.7f * (1f - k));
                _ripples[i] = r;
            }
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
