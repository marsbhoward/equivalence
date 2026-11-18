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
    /// WADING WATER, NOT A LAKE (the user's flag, 2026-11-18). The floor shows through it - a
    /// tint over the ground, not an opaque deep body - because a LAKE is a chasm (dash only) and
    /// the two must never be mistaken. Laid against an open side where the place's void is water
    /// (Hazards.PitPieces' edge water), it runs out to the room's edge with no lip and a dark
    /// drop-off into the lake past it (<see cref="PitArt.Shelf"/>).
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
            => Spawn(PitShape.FromRect(rect), player, parent);

        public static WaterPit Spawn(PitShape shape, Transform player, Transform parent)
        {
            var go = new GameObject("pit.water");
            go.transform.SetParent(parent, false);
            var pit = go.AddComponent<WaterPit>();
            pit.Init(shape, player);
            pit.Build();
            return pit;
        }

        static Color WaterDeep => Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SaltFlats) ? new(0.10f, 0.36f, 0.40f) : Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SkyRuin) ? new(0.30f, 0.46f, 0.68f) : Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest) ? new(0.05f, 0.12f, 0.09f) : new(0.08f, 0.19f, 0.31f);   // the forest's own (Art.Biomes)
        static Color WaterShallow => Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SaltFlats) ? new(0.30f, 0.62f, 0.62f) : Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SkyRuin) ? new(0.52f, 0.68f, 0.86f) : Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest) ? new(0.16f, 0.30f, 0.20f) : new(0.24f, 0.48f, 0.64f);   // the forest's own (Art.Biomes)
        static Color WaterLight => Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SaltFlats) ? new(0.95f, 0.97f, 0.97f) : Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SkyRuin) ? new(0.92f, 0.96f, 1f) : Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest) ? new(0.70f, 0.85f, 0.70f) : new(0.84f, 0.95f, 1f);   // the forest's own (Art.Biomes)
        /// <summary>WADING water's tint over the floor: lighter and cooler than the place's ground
        /// and than its lake, so it reads as shallow water standing ON the floor - never as the
        /// lake's dark (a chasm). The forest's moss is dark green, so its wading water is pale.</summary>
        static Color WadeTint => Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SaltFlats) ? new(0.42f, 0.74f, 0.80f) : Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.SkyRuin) ? new(0.56f, 0.74f, 0.96f) : Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest) ? new(0.40f, 0.62f, 0.56f) : new(0.34f, 0.60f, 0.76f);
        /// <summary>The waterline: a pale line just inside the edge, where shallow water meets the stone.</summary>
        static Color Waterline => Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest) ? new(0.70f, 0.84f, 0.72f, 0.75f) : new(0.86f, 0.95f, 1f, 0.75f);
        static Color RippleColor => Art.Biomes.Biome.Is(Art.Biomes.BiomeKind.Forest) ? new(0.65f, 0.80f, 0.66f) : new(0.78f, 0.92f, 1f);   // the forest's own (Art.Biomes)

        void Build()
        {
            _size = Shape.Bounds.size;
            int seed = Random.Range(1, 9999);
            _clock = Random.value * 10f;

            // Three layers: a TINT of shallow water over the floor (which shows through - wading
            // water, not a lake), a slow mottle of deeper water in it, and the lit glints on the
            // surface, which shimmer, being the one part of still water that moves.
            Layer(PitArt.Solid, WithAlpha(WadeTint, Tuning.Hazards.WaterTint), OrderFloor, "floor");
            // Kept LOW-contrast: at full strength the mottle reads as a patterned blue rug.
            Layer(PitArt.MottleAt(seed), WithAlpha(WaterShallow, 0.22f), OrderMaterial, "shallows");
            _glints = Layer(PitArt.GlintsAt(seed), WithAlpha(WaterLight, 0.8f), OrderDetail, "glints");
            // The waterline: a pale band a texel or two inside the edge, broken by a hash so it
            // reads as water lapping rather than a drawn outline.
            var shape = Shape;
            Layer((tx, ty, u, v) =>
            {
                float d = shape.Sample(new Vector2(u, v));
                return d < 2.2f / Art.Scenery.Ppu && PitArt.Hash(tx >> 1, ty, 919) > 0.25f ? 1f : 0f;
            }, Waterline, OrderDetail, "waterline");

            BuildRecess(Tuning.Hazards.WaterRecessDepth);
            // Edge water: the step down into the lake past the room's edge.
            if (Shape.Spills != 0)
                PitArt.Shelf(gameObject, Shape, LakeSurface.Deep * 0.7f + new Color(0f, 0f, 0f, 0.3f), LakeSurface.Mid,
                             OrderWalls, Owned ??= new List<Object>());

            for (int i = 0; i < RipplePool; i++)
            {
                var sr = PitArt.Quad(gameObject, Combat.TexelFx.FaintRingSprite(0.1f), Color.clear, Vector2.one, OrderAbove, "ripple");
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
                float a = 0.6f + 0.2f * Mathf.Sin(_clock * 2.3f) + 0.1f * Mathf.Sin(_clock * 5.1f + 1.3f);
                _glints.color = WithAlpha(WaterLight, a);
            }

            // The surface's own ripples: somewhere random, on an irregular beat.
            _nextRipple -= dt;
            if (_nextRipple <= 0f)
            {
                _nextRipple = Random.Range(Tuning.Hazards.WaterRippleIntervalMin, Tuning.Hazards.WaterRippleIntervalMax);
                Emit(PointInside(0.2f, () => Random.value), 0.75f);
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
            // As big as the water round it allows - the shape's own distance to its edge.
            float room = (Shape.Sample((Vector2)transform.position + local) - 0.08f) * 2f;
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
                // A faint broken ring drawn at its real size on the texel grid (the user's pick, group 4).
                if (r.Sr != null) r.Sr.sprite = Combat.TexelFx.FaintRingSprite(Mathf.Lerp(0.15f, r.Size, grow) * 0.5f);
                r.T.localScale = Vector3.one;
                if (r.Sr != null) r.Sr.color = WithAlpha(RippleColor, 0.7f * (1f - k));
                _ripples[i] = r;
            }
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
