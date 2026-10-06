using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A sand trap. Slows anything standing in it and does nothing else - no damage, no tick, no
    /// cycle.
    ///
    /// A PURE MOVEMENT COST IS THE POINT, NOT A SHORTFALL. The player is about 2.5x faster than
    /// anything chasing them, and this project's own notes make disengaging a standing promise -
    /// "the crowd is a positioning problem, not a race". Sand is the one hazard that argues with
    /// that promise without breaking it: it does not catch you, it makes the ground you were
    /// going to retreat across expensive, so a pack that could never corner you on open floor
    /// can corner you against one of these. A sand trap that also chipped health would be a worse
    /// fire pit, and there is already a fire pit.
    ///
    /// ALWAYS ON, deliberately - unlike fire, there is no cycle to read. A slow that came and went
    /// would ask the player to time a crossing, and timing a crossing is fire's whole idea. Sand's
    /// idea is that it is simply there, permanently, changing the shape of the room.
    ///
    /// GRANULAR, NOT SAND-COLOURED, and that is an art requirement with a mechanism behind it: the
    /// fine layer is a point-sampled per-texel hash and both layers are TILED rather than
    /// stretched, so the grain is the same size on a small trap as on a large one. See PitArt for
    /// why bilinear filtering or a stretched texture each turn this back into a flat tan
    /// rectangle.
    /// </summary>
    public class SandPit : FloorPit
    {
        public override float SpeedMultiplier => Tuning.Hazards.SandSpeedMultiplier;

        public override PitKind Kind => PitKind.Sand;
        public override float RouteCost => Tuning.Steering.SandRouteCost;

        public static SandPit Spawn(Rect rect, Transform player, Transform parent)
        {
            var go = new GameObject("pit.sand");
            go.transform.SetParent(parent, false);
            var pit = go.AddComponent<SandPit>();
            pit.Init(rect, player);
            pit.Build(rect.size);
            return pit;
        }

        static readonly Color SandDeep = new(0.44f, 0.35f, 0.22f);
        static readonly Color SandDune = new(0.70f, 0.59f, 0.38f);
        static readonly Color SandLit = new(0.93f, 0.85f, 0.64f);

        void Build(Vector2 size)
        {
            int seed = Random.Range(1, 9999);

            // Three layers, and each is doing a different job - one alone is what "just sand
            // coloured" actually looks like.
            //
            //   base    the shadowed bulk. Dark, because loose sand seen from above is mostly
            //           the shadow between grains rather than the grains themselves.
            //   dune    smooth two-octave noise: the slow undulation of a drift. Without it the
            //           speckle sits on a flat sheet and reads as noise laid over a colour.
            //   grain   per-texel hash, point-sampled: the individual lit grains. This is the
            //           layer the word "granular" is actually about.
            PitArt.Quad(gameObject, Spr.Square, SandDeep, size, OrderFloor, "floor");
            PitArt.Tiled(gameObject, PitArt.Mottle(seed), WithAlpha(SandDune, 0.85f), size, OrderMaterial, "dune");
            PitArt.Tiled(gameObject, PitArt.Grain(seed), WithAlpha(SandLit, 0.9f), size, OrderDetail, "grain");

            BuildRecess(size);
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }
    }
}
