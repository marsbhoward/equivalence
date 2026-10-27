using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// CLASHES: two attacks that meet cancel each other out (the user's calls, 2026-10-25).
    ///
    /// - A player's MELEE swing that reaches an enemy whose own strike is about to land
    ///   (<see cref="Enemies.EnemyController.StrikeImminent"/> - the last
    ///   <see cref="Tuning.Clash.WindowSeconds"/> of its tell, or the strike itself while it is out)
    ///   clashes with it. Neither lands: the body takes nothing and the enemy's blow is void, sent
    ///   into its recovery. A HEAVY weapon art beats the strike - it lands as ever and the blow is
    ///   still void.
    /// - A player's SHOT (arrow, thrown disc, thrown blade) reaching such an enemy is BATTED AWAY:
    ///   the shot deals that body nothing, and the blow is void. A Heavy art's shot beats it.
    /// - A player's shot or swing that meets an enemy SHOT in flight (a bolt, a fireball -
    ///   <see cref="EnemyProjectile"/>) destroys it. A BASIC is spent on it (the arrow is gone, the
    ///   disc comes home, the swing hits no body); a weapon art's carries on through.
    ///
    /// Mortar shells are lobbed over everything and are not cut (the user's call). Only the
    /// player's own attacks clash - enemies never cancel each other.
    /// </summary>
    public static class Clash
    {
        /// <summary>Whether a player attack beats an enemy's strike rather than cancelling with
        /// it: a Heavy weapon art.</summary>
        public static bool Wins(bool isFinisher, FinisherWeight weight)
            => isFinisher && weight == FinisherWeight.Heavy;

        /// <summary>
        /// Asked by every player hit path just before a body takes the hit. If the body's own strike
        /// is imminent the two clash: the blow is void and the tell plays between them. Returns true
        /// when the player's hit must be WITHHELD from this body (the two cancelled) - false when
        /// there was no clash, or the player's attack won and lands as ever.
        /// </summary>
        public static bool AtBody(Health body, Vector2 from, bool wins)
        {
            if (body == null || body.IsDead) return false;
            var enemy = body.GetComponent<Enemies.EnemyController>();
            if (enemy == null || !enemy.StrikeImminent) return false;

            enemy.Clashed();
            Vector2 at = body.transform.position;
            var d = at - from;
            float len = d.magnitude;
            // The sparks fly where the two blows met - between them, nearer the body.
            Tell(len > 0.001f ? at - d / len * Mathf.Min(len * 0.4f, Tuning.Clash.SparkInset) : at, heavy: true);
            return !wins;
        }

        /// <summary>
        /// Destroys every hostile enemy shot within <paramref name="radius"/> of the segment
        /// <paramref name="a"/>-<paramref name="b"/> (a shot's travel this frame). Returns how many.
        /// </summary>
        public static int CutShots(Vector2 a, Vector2 b, float radius)
        {
            int cut = 0;
            var live = EnemyProjectile.Live;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = i < live.Count ? live[i] : null;
                if (p == null || !p.Hostile) continue;
                if (DistanceToSegment(p.Position, a, b) > radius + p.Radius) continue;
                Cut(p);
                cut++;
            }
            return cut;
        }

        /// <summary>
        /// Destroys every hostile enemy shot inside a swing's footprint: a capsule from
        /// <paramref name="origin"/> along <paramref name="facing"/> to <paramref name="range"/>,
        /// <paramref name="width"/> across (GatherTargets' capsule), or a circle of the range for an
        /// area swing.
        /// </summary>
        public static int CutShotsInSwing(Vector2 origin, Vector2 facing, float range, float width, bool area)
        {
            int cut = 0;
            var live = EnemyProjectile.Live;
            Vector2 tip = origin + facing * Mathf.Max(0f, range - width * 0.5f);
            Vector2 root = origin + facing * Mathf.Min(width * 0.5f, range * 0.5f);
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var p = i < live.Count ? live[i] : null;
                if (p == null || !p.Hostile) continue;
                bool inside = area
                    ? Vector2.Distance(p.Position, origin) <= range + p.Radius
                    : DistanceToSegment(p.Position, root, tip) <= width * 0.5f + p.Radius;
                if (!inside) continue;
                Cut(p);
                cut++;
            }
            return cut;
        }

        static void Cut(EnemyProjectile p)
        {
            Tell(p.Position, heavy: false);
            p.Cut();
        }

        /// <summary>The clash's tell: white-gold sparks, a ring and a clang. A melee clash also
        /// freezes the frame for a beat (a cut shot does not - a volley through a barrage would
        /// stutter).</summary>
        public static void Tell(Vector2 at, bool heavy)
        {
            // A white-gold X where the blades met (the user's pick, group 4 - "cross glint"), then the sparks.
            TexelFx.Glint(at, TexelFx.Gold, heavy ? 6 : 4, heavy ? 0.18f : 0.14f);
            ClashSparks.At(at, heavy ? Tuning.Clash.SparksMelee : Tuning.Clash.SparksShot);
            Audio.Sfx.Play(Audio.Sound.Clash, at);
            if (heavy) Hitstop.TriggerClash();
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }
    }

    /// <summary>
    /// A clash's sparks: a few short bright streaks flung out from the meeting point, falling a
    /// little and fading. Hashed off the point, never UnityEngine.Random (seeded floor rolls share
    /// its state). Scaled time, so a clash's hit-stop holds them too.
    /// </summary>
    public class ClashSparks : MonoBehaviour
    {
        SpriteRenderer[] _srs;
        Vector2[] _vel;
        float _age;

        public static void At(Vector2 at, int count)
        {
            var go = new GameObject("clash.sparks");
            go.transform.position = at;
            var s = go.AddComponent<ClashSparks>();
            s._srs = new SpriteRenderer[count];
            s._vel = new Vector2[count];
            uint h = (uint)(Mathf.RoundToInt(at.x * 97f) * 73856093) ^ (uint)(Mathf.RoundToInt(at.y * 97f) * 19349663);
            for (int i = 0; i < count; i++)
            {
                h = h * 1664525u + 1013904223u;
                float ang = (h >> 8) / 16777216f * Mathf.PI * 2f;
                h = h * 1664525u + 1013904223u;
                float speed = Mathf.Lerp(2.2f, 4.5f, (h >> 8) / 16777216f);
                s._vel[i] = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * speed;

                var b = new GameObject("spark");
                b.transform.SetParent(go.transform, false);
                var sr = b.AddComponent<SpriteRenderer>();
                sr.sprite = Spr.Capsule;
                sr.color = i % 2 == 0 ? Color.white : Tuning.Clash.Spark;
                sr.sortingOrder = SortingOrders.Fx + 1;
                b.transform.localScale = new Vector3(0.16f, 0.035f, 1f);
                s._srs[i] = sr;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            float u = _age / Tuning.Clash.SparkSeconds;
            if (u >= 1f || _srs == null) { Destroy(gameObject); return; }
            for (int i = 0; i < _srs.Length; i++)
            {
                var sr = _srs[i];
                if (sr == null) continue;
                _vel[i] += Vector2.down * (6f * dt);
                _vel[i] *= 1f - 3f * dt;
                var t = sr.transform;
                t.localPosition += (Vector3)(_vel[i] * dt);
                t.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_vel[i].y, _vel[i].x) * Mathf.Rad2Deg);
                var c = sr.color; c.a = 1f - u * u; sr.color = c;
            }
        }
    }
}
