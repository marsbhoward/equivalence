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
    /// A Grave Brute's cone fills from BOTH ends (<see cref="Pincer"/>), the two meeting as the
    /// blow lands, then flashes where it lay (<see cref="Spend"/>).
    /// </summary>
    public class BlastTell : TexelPicture
    {
        /// <summary>0 as the tell starts, 1 as it fires.</summary>
        public float Progress;

        [SerializeField] float _radius, _halfAngle;
        [SerializeField] Vector2 _dir = Vector2.right;
        [SerializeField] bool _cone;
        /// <summary>A CROSS (the Heart's cross shot): dotted arms out these ways, from a point on the floor.</summary>
        Vector2[] _arms;
        [SerializeField] Color _colour;

        /// <summary>A circle of <paramref name="radius"/> (world) round <paramref name="parent"/>.</summary>
        public static BlastTell Circle(Transform parent, Vector2 anchor, float radius, Color colour)
            => Make(parent, anchor, radius, colour, false, 0f);

        /// <summary>A wedge out to <paramref name="reach"/> (world), <paramref name="halfAngle"/>
        /// degrees either side of its aim (<see cref="Aim"/>).</summary>
        public static BlastTell Cone(Transform parent, Vector2 anchor, float reach, float halfAngle, Color colour)
            => Make(parent, anchor, reach, colour, true, halfAngle);

        /// <summary>Arms <paramref name="arm"/> long (world) out along <paramref name="dirs"/> from
        /// <paramref name="at"/>, a point on the floor (no parent): where a thrown shot will burst.</summary>
        public static BlastTell Cross(Vector2 at, float arm, Vector2[] dirs, Color colour)
        {
            var t = Make(null, at, arm, colour, false, 0f);
            t.name = "tell.cross";
            t._arms = dirs;
            return t;
        }

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

        /// <summary>Where the two fills meet (share of the reach), or 1: one fill, out from the body.</summary>
        float _meet = 1f;
        float _flashUntil = -1f, _flashFor;

        /// <summary>Two fills instead of one (the Brute's cone): out from the body and in from the
        /// far edge, meeting <paramref name="meet"/> of the way out at Progress 1.</summary>
        public void Pincer(float meet) => _meet = Mathf.Clamp01(meet);

        /// <summary>The blow has landed: unparent, flash the whole shape and fade over
        /// <paramref name="seconds"/>, then go.</summary>
        public void Spend(float seconds)
        {
            // Unparented, the anchor is read as a world point (TexelPicture.LateUpdate).
            if (transform.parent != null) Anchor += (Vector2)transform.parent.position;
            transform.SetParent(null, true);
            Progress = 1f;
            _flashFor = Mathf.Max(0.01f, seconds);
            _flashUntil = Time.time + _flashFor;
            Destroy(gameObject, _flashFor);
        }

        protected override bool Draw()
        {
            float strength = EnemyController.TelegraphStrength;
            if (strength <= 0f) return false;
            float p = Mathf.Clamp01(Progress), R = _radius;
            float fill = Mathf.Max(1f, R * p);
            float flash = _flashUntil > 0f ? Mathf.Clamp01((_flashUntil - Time.time) / _flashFor) : 0f;
            bool pincer = _meet < 1f;
            float near = Mathf.Max(1f, R * p * _meet), far = R - R * p * (1f - _meet);
            float aim = Mathf.Atan2(_dir.y, _dir.x), half = _halfAngle * Mathf.Deg2Rad;

            var c = _colour;
            // Brighter toward white as it nears.
            var hot = (Color32)new Color(Mathf.Lerp(c.r, 1f, p * 0.45f), Mathf.Lerp(c.g, 1f, p * 0.45f), Mathf.Lerp(c.b, 1f, p * 0.45f), 1f);
            var line = (Color32)c;

            if (_arms != null)
            {
                // the cross: each arm dotted to its end, a firm line filling out along it, a point swelling at the centre
                foreach (var d in _arms)
                {
                    var n = d.normalized;
                    Line(0f, 0f, n.x * R, n.y * R, (x, y, f) =>
                    {
                        int step = Mathf.RoundToInt(f * R);
                        if (f <= p) Over(x, y, f > p - 0.15f ? hot : line, strength * Mathf.Lerp(0.5f, 0.9f, p));
                        else if (step > 1 && (step & 3) < 2) Over(x, y, line, strength * Mathf.Lerp(0.6f, 0.9f, p));
                    });
                }
                Ring(0f, 0f, 0f, 1.5f + 2.5f * p, (x, y, d, a) => Over(x, y, hot, strength * Mathf.Lerp(0.5f, 0.95f, p)));
                return true;
            }

            Ring(0f, 0f, 0f, R + 0.5f, (x, y, d, a) =>
            {
                if (_cone && d > 1.5f && Mathf.Abs(Mathf.DeltaAngle(aim * Mathf.Rad2Deg, a * Mathf.Rad2Deg)) > _halfAngle + 0.01f)
                    return;
                if (flash > 0f) { Over(x, y, hot, strength * (0.25f + 0.5f * flash)); return; }
                if (pincer)
                {
                    // Two fills: out from the body, in from the far edge, each with a firm front.
                    if (d <= near)
                    {
                        bool front = d > near - 1.6f;
                        Over(x, y, front ? hot : line, strength * (front ? Mathf.Lerp(0.55f, 0.95f, p) : Mathf.Lerp(0.08f, 0.2f, p)));
                    }
                    else if (p > 0f && d >= far)
                    {
                        bool front = d < far + 1.6f;
                        Over(x, y, front ? hot : line, strength * (front ? Mathf.Lerp(0.55f, 0.95f, p) : Mathf.Lerp(0.08f, 0.2f, p)));
                    }
                }
                // The fill: faint inside, a firm front where it has got to.
                else if (d <= fill)
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
            if (_cone && flash <= 0f)
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
