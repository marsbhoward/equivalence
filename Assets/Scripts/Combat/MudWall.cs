using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// MUDSLIDE's wall (Earth's Mobility, RELEASES.md): the mud the slide pushed sets behind the player
    /// as a short bank for <see cref="Tuning.Releases.MudWallSeconds"/>. A solid collider holds bodies
    /// (enemies walk round it, as the player must); an enemy shot crossing it dies on it
    /// (EnemyProjectile asks <see cref="Blocks"/>). The player's own shots are not stopped - it is
    /// their wall.
    /// </summary>
    public class MudWall : MonoBehaviour
    {
        /// <summary>The walls standing now. Not readonly: a domain reload re-creates it empty.</summary>
        static List<MudWall> _live = new();

        Vector2 _a, _b;
        float _until;

        public static void Raise(Vector2 centre, Vector2 along, float halfWidth, float seconds, Vector2 drawAt)
        {
            along = along.sqrMagnitude > 1e-6f ? along.normalized : Vector2.right;
            var go = new GameObject("mudslide.wall");
            go.transform.position = centre;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(halfWidth * 2f, 0.4f);
            var w = go.AddComponent<MudWall>();
            w._a = centre - along * halfWidth;
            w._b = centre + along * halfWidth;
            w._until = Time.time + seconds;
            (_live ??= new()).Add(w);
            ArsenalFx.Line(ArsenalFx.Band.Mud, drawAt - along * halfWidth, along, halfWidth * 2f, 0.3f,
                           ArsenalFx.Mud, 0.12f, seconds);
        }

        /// <summary>Whether a shot moving from <paramref name="p"/> to <paramref name="q"/> this frame
        /// meets a standing wall.</summary>
        public static bool Blocks(Vector2 p, Vector2 q)
        {
            if (_live == null) return false;
            foreach (var w in _live)
                if (w != null && Crosses(p, q, w._a, w._b, 0.2f)) return true;
            return false;
        }

        /// <summary>Two segments meet, or pass within <paramref name="pad"/> of each other's ends.</summary>
        static bool Crosses(Vector2 p, Vector2 q, Vector2 a, Vector2 b, float pad)
        {
            static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
            Vector2 r = q - p, s = b - a;
            float den = Cross(r, s);
            if (Mathf.Abs(den) > 1e-6f)
            {
                float t = Cross(a - p, s) / den, u = Cross(a - p, r) / den;
                if (t >= 0f && t <= 1f && u >= 0f && u <= 1f) return true;
            }
            return Near(q, a, b) < pad;
        }

        static float Near(Vector2 x, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(x - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(x, a + ab * t);
        }

        void Update()
        {
            if (Time.time < _until) return;
            TexelFx.Puff(transform.position, ArsenalFx.Mud, 10, 10f, 0.5f, TexelFx.Ground);
            Destroy(gameObject);
        }

        void OnDestroy() => _live?.Remove(this);
    }

    /// <summary>
    /// A line that keeps acting on what stands on it - Wavecrest's wet line soaking whatever crosses it.
    /// Every <c>period</c> seconds, each body within <c>halfWidth</c> of the segment is handed to the
    /// action. A domain reload drops the action, and the field with it.
    /// </summary>
    public class ArsenalField : MonoBehaviour
    {
        Vector2 _a, _b;
        float _half, _until, _next, _period;
        System.Action<Health> _act;

        public static void Line(Vector2 a, Vector2 b, float halfWidth, float seconds, float period, System.Action<Health> act)
        {
            var f = new GameObject("arsenal.field").AddComponent<ArsenalField>();
            f._a = a; f._b = b; f._half = halfWidth; f._until = Time.time + seconds; f._period = period;
            f._next = Time.time; f._act = act;
        }

        void Update()
        {
            if (_act == null || Time.time >= _until) { Destroy(gameObject); return; }
            if (Time.time < _next) return;
            _next += _period;
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                var ab = _b - _a;
                Vector2 x = e.transform.position;
                float t = Mathf.Clamp01(Vector2.Dot(x - _a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                if (Vector2.Distance(x, _a + ab * t) <= _half) _act(hp);
            }
        }
    }
}
