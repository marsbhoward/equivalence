using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// PALISADE, the bow's DEFENSIVE (the user's calls, 2026-10-18 - Intercept and Palisade mixed):
    /// the art's <see cref="Tuning.Palisade.Arrows"/> arrows go up off the string and come down. Each
    /// takes an enemy bolt or mortar shell in flight first (nearest the archer first): it lands ON
    /// it, the shot bursts in smoke, and the arrow sticks in the ground there. Arrows with nothing
    /// to take land in a LINE across the aim, <see cref="Tuning.Palisade.LineAhead"/> out.
    ///
    /// A stuck arrow is a STAKE for <see cref="Tuning.Palisade.StakeSeconds"/>: the next enemy bolt
    /// that reaches it bursts, and the stake breaks with it (one block each). An enemy that walks
    /// over one breaks it early and takes <c>onTrample</c> (PlayerController deals it). Shells lob
    /// over stakes, as over walls - only the art's own arrows take them. Scaled time.
    /// </summary>
    public class BowPalisade : MonoBehaviour
    {
        Sprite _arrow;
        Action<Health> _onTrample;

        class Flight
        {
            public SpriteRenderer Sr;
            public Vector2 From, Ground;
            public EnemyProjectile Bolt;
            public Enemies.MortarShell Shell;
            public float Clock;
            public bool Done;
        }

        class Stake
        {
            public SpriteRenderer Sr;
            public Vector2 Ground;
            public float Age, Gone = -1f;
        }

        List<Flight> _flights = new();   // non-readonly - domain reload
        List<Stake> _stakes = new();

        /// <summary>Loose the palisade from <paramref name="from"/> (the nocked arrow) along
        /// <paramref name="aim"/>, round the archer at <paramref name="archer"/>.</summary>
        public static BowPalisade Loose(Vector2 archer, Vector2 from, Vector2 aim, Sprite arrow, Action<Health> onTrample)
        {
            var go = new GameObject("bow.palisade");
            var p = go.AddComponent<BowPalisade>();
            p._arrow = arrow;
            p._onTrample = onTrample;
            p.Plan(archer, from, aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector2.right);
            return p;
        }

        void Plan(Vector2 archer, Vector2 from, Vector2 aim)
        {
            int left = Tuning.Palisade.Arrows;

            // Shots in flight, nearest the archer first.
            var shots = new List<(float d, EnemyProjectile bolt, Enemies.MortarShell shell)>();
            foreach (var b in FindObjectsByType<EnemyProjectile>())
                if (b != null && b.Hostile)
                    shots.Add((Vector2.Distance(archer, b.transform.position), b, null));
            foreach (var s in FindObjectsByType<Enemies.MortarShell>())
                if (s != null && s.Hostile)
                    shots.Add((Vector2.Distance(archer, s.GroundPosition), null, s));
            shots.Sort((a, b) => a.d.CompareTo(b.d));
            foreach (var (d, bolt, shell) in shots)
            {
                if (left == 0 || d > Tuning.Palisade.InterceptReach) break;
                Launch(from, bolt != null ? (Vector2)bolt.transform.position : shell.GroundPosition, bolt, shell);
                left--;
            }

            // The rest: a line across the aim, centred on it.
            var across = new Vector2(-aim.y, aim.x);
            Vector2 centre = archer + aim * Tuning.Palisade.LineAhead;
            for (int i = 0; i < left; i++)
            {
                float o = (i - (left - 1) * 0.5f) * Tuning.Palisade.Spacing;
                Launch(from, centre + across * o, null, null);
            }
        }

        void Launch(Vector2 from, Vector2 ground, EnemyProjectile bolt, Enemies.MortarShell shell)
        {
            var go = new GameObject("palisade.arrow");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _arrow;
            sr.sortingOrder = SortingOrders.Fx - 2;
            float s = Tuning.Player.ArenaVisualScale;
            go.transform.localScale = new Vector3(s, s, 1f);
            _flights.Add(new Flight { Sr = sr, From = from, Ground = ground, Bolt = bolt, Shell = shell });
            Place(_flights[^1], 0f);
        }

        /// <summary>The arrow on its arc at <paramref name="t"/> (0 off the string, 1 landed),
        /// turned along its path.</summary>
        void Place(Flight f, float t)
        {
            // A taken shot is chased where it IS (a shell where it is drawn, in the air).
            Vector2 end = f.Ground;
            if (f.Bolt != null) end = f.Bolt.transform.position;
            else if (f.Shell != null && f.Shell.Hostile) end = f.Shell.DrawnPosition;
            float h = Tuning.Palisade.LobHeight * Vector2.Distance(f.From, end) / Mathf.Max(1f, Tuning.Palisade.LineAhead);
            Vector2 At(float u) => Vector2.Lerp(f.From, end, u) + Vector2.up * (4f * h * u * (1f - u));
            var p = At(t);
            var ahead = At(Mathf.Min(1f, t + 0.02f)) - At(Mathf.Max(0f, t - 0.02f));
            f.Sr.transform.position = new Vector3(p.x, p.y, 0f);
            if (ahead.sqrMagnitude > 1e-8f)
                f.Sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _flights.Count; i++)
            {
                var f = _flights[i];
                if (f.Done) continue;
                f.Clock += dt;
                float t = Mathf.Clamp01(f.Clock / Tuning.Palisade.FlightSeconds);
                // A shot already gone (it landed, burst, or another stopped it): the arrow comes
                // down where it was going.
                if (f.Bolt == null && f.Shell != null && !f.Shell.Hostile) f.Shell = null;
                if (f.Bolt != null) f.Ground = f.Bolt.transform.position;
                else if (f.Shell != null) f.Ground = f.Shell.GroundPosition;
                Place(f, t);
                if (t < 1f) continue;

                f.Done = true;
                // Out of play AT ONCE (Destroy waits for the frame's end): the new stake below would
                // otherwise "block" the very shot it landed on, and break.
                if (f.Bolt != null) { SmokePuff.At(f.Bolt.transform.position); Gone(f.Bolt.gameObject); }
                if (f.Shell != null) { SmokePuff.At(f.Shell.DrawnPosition); Gone(f.Shell.gameObject); }
                Destroy(f.Sr.gameObject);
                StickAt(f.Ground);
            }

            for (int i = 0; i < _stakes.Count; i++)
            {
                var s = _stakes[i];
                if (s.Sr == null) continue;
                s.Age += dt;
                if (s.Gone >= 0f)
                {
                    // Breaking: it tips and fades.
                    s.Gone += dt;
                    float u = s.Gone / Tuning.Palisade.BreakSeconds;
                    if (u >= 1f) { Destroy(s.Sr.gameObject); s.Sr = null; continue; }
                    s.Sr.color = new Color(1f, 1f, 1f, 1f - u);
                    s.Sr.transform.rotation *= Quaternion.Euler(0f, 0f, 180f * dt);
                    continue;
                }
                if (s.Age >= Tuning.Palisade.StakeSeconds) { Break(s); continue; }
                if (Block(s)) { Break(s); continue; }
                var walker = Trampled(s);
                if (walker != null) { Break(s); _onTrample?.Invoke(walker); }
            }

            if (_flights.TrueForAll(f => f.Done) && _stakes.TrueForAll(s => s.Sr == null)) Destroy(gameObject);
        }

        void StickAt(Vector2 ground)
        {
            // Never on a wall or over a chasm - an arrow there is simply lost.
            if (!Arena.OnFloor(ground)) return;
            var go = new GameObject("palisade.stake");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _arrow;
            float s = Tuning.Player.ArenaVisualScale;
            go.transform.localScale = new Vector3(s, s, 1f);
            // Stood in the ground head down, leaning a little: a third of it buried.
            float len = _arrow != null ? _arrow.bounds.size.x * s : 0.4f;
            float lean = UnityEngine.Random.Range(-14f, 14f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, -90f + lean);
            go.transform.position = (Vector3)(ground + (Vector2)(Quaternion.Euler(0f, 0f, lean) * Vector2.up) * (len * 0.2f));
            DepthSorted.Attach(go, -len * 0.2f, true, sr);
            SmokePuff.At(ground);
            _stakes.Add(new Stake { Sr = sr, Ground = ground });
        }

        static void Gone(GameObject go)
        {
            go.SetActive(false);
            Destroy(go);
        }

        void Break(Stake s)
        {
            if (s.Gone >= 0f) return;
            s.Gone = 0f;
        }

        /// <summary>The first enemy bolt at the stake bursts - one block, then the stake goes.</summary>
        bool Block(Stake s)
        {
            float r2 = Tuning.Palisade.BlockRadius * Tuning.Palisade.BlockRadius;
            foreach (var b in FindObjectsByType<EnemyProjectile>())
            {
                if (b == null || !b.Hostile) continue;
                if (((Vector2)b.transform.position - s.Ground).sqrMagnitude > r2) continue;
                SmokePuff.At(b.transform.position);
                Gone(b.gameObject);
                return true;
            }
            return false;
        }

        Health Trampled(Stake s)
        {
            float r2 = Tuning.Palisade.TrampleRadius * Tuning.Palisade.TrampleRadius;
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null || e.FellIn) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (((Vector2)hp.transform.position - s.Ground).sqrMagnitude <= r2) return hp;
            }
            return null;
        }
    }
}
