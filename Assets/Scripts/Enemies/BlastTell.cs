using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// What an attack will cover, drawn on the floor through its wind-up (the user's design,
    /// 2026-11-12): the true edge as a DOTTED line from the start, and a fill growing out from the
    /// body to meet it - a point under a Brimstone Heart swelling to its blast radius, a wedge
    /// running out of a Salamander's jaws to the end of its flame. When the fill reaches the dotted
    /// line, it goes off. The owner sets <see cref="Progress"/> every frame (0 as it starts, 1 as
    /// it fires); the shape is the damage's own (AttackRange, FlameReach and its half-angle).
    /// </summary>
    public class BlastTell : TexelPicture
    {
        /// <summary>0 as the tell starts, 1 as it fires.</summary>
        public float Progress;

        [SerializeField] float _radius, _halfAngle;
        [SerializeField] Vector2 _dir = Vector2.right;
        [SerializeField] bool _cone;
        [SerializeField] Color _colour;

        /// <summary>A circle of <paramref name="radius"/> (world) round <paramref name="parent"/>.</summary>
        public static BlastTell Circle(Transform parent, Vector2 anchor, float radius, Color colour)
            => Make(parent, anchor, radius, colour, false, 0f);

        /// <summary>A wedge out to <paramref name="reach"/> (world), <paramref name="halfAngle"/>
        /// degrees either side of its aim (<see cref="Aim"/>).</summary>
        public static BlastTell Cone(Transform parent, Vector2 anchor, float reach, float halfAngle, Color colour)
            => Make(parent, anchor, reach, colour, true, halfAngle);

        static BlastTell Make(Transform parent, Vector2 anchor, float radius, Color colour, bool cone, float halfAngle)
        {
            int r = Mathf.CeilToInt(radius * TexelFx.Body) + 2;
            var t = Attach<BlastTell>(parent, cone ? "tell.cone" : "tell.blast", anchor, r, r, r, SortingOrders.GroundDanger);
            t._radius = radius * TexelFx.Body;
            t._cone = cone;
            t._halfAngle = halfAngle;
            t._colour = colour;
            return t;
        }

        /// <summary>Where the wedge points (world).</summary>
        public void Aim(Vector2 dir) { if (dir.sqrMagnitude > 0.0001f) _dir = dir.normalized; }

        protected override bool Draw()
        {
            float strength = EnemyController.TelegraphStrength;
            if (strength <= 0f) return false;
            float p = Mathf.Clamp01(Progress), R = _radius;
            float fill = Mathf.Max(1f, R * p);
            float aim = Mathf.Atan2(_dir.y, _dir.x), half = _halfAngle * Mathf.Deg2Rad;

            var c = _colour;
            // Brighter toward white as it nears.
            var hot = (Color32)new Color(Mathf.Lerp(c.r, 1f, p * 0.45f), Mathf.Lerp(c.g, 1f, p * 0.45f), Mathf.Lerp(c.b, 1f, p * 0.45f), 1f);
            var line = (Color32)c;

            Ring(0f, 0f, 0f, R + 0.5f, (x, y, d, a) =>
            {
                if (_cone && d > 1.5f && Mathf.Abs(Mathf.DeltaAngle(aim * Mathf.Rad2Deg, a * Mathf.Rad2Deg)) > _halfAngle + 0.01f)
                    return;
                // The fill: faint inside, a firm front where it has got to.
                if (d <= fill)
                {
                    bool front = d > fill - 1.6f;
                    Over(x, y, front ? hot : line, strength * (front ? Mathf.Lerp(0.55f, 0.95f, p) : Mathf.Lerp(0.08f, 0.2f, p)));
                }
                // The edge: dotted, two on two off round the arc.
                if (d > R - 1f)
                {
                    int step = Mathf.FloorToInt(a * R);   // texels along the arc
                    if ((step & 3) < 2) Over(x, y, line, strength * Mathf.Lerp(0.6f, 0.9f, p));
                }
            });

            // A wedge's two sides, dotted the same way.
            if (_cone)
                for (int s = -1; s <= 1; s += 2)
                {
                    float a = aim + s * half;
                    Line(0f, 0f, Mathf.Cos(a) * R, Mathf.Sin(a) * R, (x, y, f) =>
                    {
                        int step = Mathf.RoundToInt(f * R);
                        if (step > 1 && (step & 3) < 2) Over(x, y, line, strength * Mathf.Lerp(0.6f, 0.9f, p));
                    });
                }
            return true;
        }
    }
}
