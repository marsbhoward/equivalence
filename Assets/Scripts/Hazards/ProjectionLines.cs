using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// Lines drawn across the floor that, after a warning, BURN for a few seconds - the Projection
    /// cost's (on the player) and its Albedo, Ley Lines' (on enemies, in the player's colour).
    ///
    /// Shown thin and faint while they warn, then a red edge under a white-hot core like a spire's
    /// lines while they burn - a damaging line must read as DANGER whatever else is on the floor.
    /// Each line runs wall to wall through its point, clipped to the arena's outer rectangle; a
    /// projection is ON the floor, so interior walls do not stop it.
    ///
    /// The player's burn is priced like every other hazard, by the floor's depth
    /// (<see cref="DamageScale"/>, FloorDifficulty.Damage, set as each floor builds). Every cached
    /// field is serialized and non-readonly for the reason FloorPit gives.
    /// </summary>
    public class ProjectionLines : MonoBehaviour
    {
        /// <summary>The floor's FloorDifficulty.Damage - set by the run as each floor builds.</summary>
        public static float DamageScale = 1f;

        /// <summary>Every live set, so a floor clear can calm them. Non-readonly and null-guarded:
        /// a domain reload empties it while the sets survive, and they simply finish on their own.</summary>
        static List<ProjectionLines> _live;

        const float TickSeconds = 0.2f;
        const float BodyAllowance = 0.18f;
        const float EnemyAllowance = 0.3f;
        const int Order = SortingOrders.PitBase + 7;

        [SerializeField] Vector2[] _from, _to;
        [SerializeField] float _warning, _burn, _elapsed, _tick, _halfWidth;
        [SerializeField] float _playerDps, _enemyDps;
        [SerializeField] GameObject _owner;
        [SerializeField] Health _playerHealth;
        [SerializeField] Transform _player;
        [SerializeField] SpriteRenderer[] _edge, _core;
        [SerializeField] Color _edgeColor, _coreColor;

        static readonly Color DangerEdge = new(1f, 0.25f, 0.18f);
        static readonly Color DangerCore = new(1f, 0.92f, 0.85f);

        /// <summary>Lines that will burn the PLAYER (Projection, Lattice).</summary>
        public static ProjectionLines ForPlayer(List<(Vector2 A, Vector2 B)> lines, float warning, float burn,
                                                float damagePerSecond, Transform player)
            => Spawn(lines, warning, burn, damagePerSecond * DamageScale, 0f, player, null, DangerEdge, DangerCore);

        /// <summary>Lines that will burn ENEMIES, credited to the player (Ley Lines).</summary>
        public static ProjectionLines ForEnemies(List<(Vector2 A, Vector2 B)> lines, float warning, float burn,
                                                 float damagePerSecond, GameObject owner, Color tint)
            => Spawn(lines, warning, burn, 0f, damagePerSecond, null, owner, tint, Color.Lerp(tint, Color.white, 0.6f));

        static ProjectionLines Spawn(List<(Vector2 A, Vector2 B)> lines, float warning, float burn,
                                     float playerDps, float enemyDps, Transform player, GameObject owner,
                                     Color edge, Color core)
        {
            var go = new GameObject("projection");
            var p = go.AddComponent<ProjectionLines>();
            p._from = new Vector2[lines.Count];
            p._to = new Vector2[lines.Count];
            for (int i = 0; i < lines.Count; i++) { p._from[i] = lines[i].A; p._to[i] = lines[i].B; }
            p._warning = Mathf.Max(0.05f, warning);
            p._burn = Mathf.Max(0.05f, burn);
            p._halfWidth = Tuning.Exchange.ProjectionHalfWidth;
            p._playerDps = playerDps;
            p._enemyDps = enemyDps;
            p._owner = owner;
            p._player = player;
            p._playerHealth = player != null ? player.GetComponent<Health>() : null;
            p._edgeColor = edge;
            p._coreColor = core;
            p.Build();
            (_live ??= new List<ProjectionLines>()).Add(p);
            return p;
        }

        /// <summary>The floor cleared: every set goes out at once, harmless.</summary>
        public static void CalmAll()
        {
            if (_live == null) return;
            foreach (var p in _live) if (p != null) Destroy(p.gameObject);
            _live.Clear();
        }

        void OnDestroy() => _live?.Remove(this);

        void Build()
        {
            int n = _from.Length;
            _edge = new SpriteRenderer[n];
            _core = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var d = _to[i] - _from[i];
                float len = d.magnitude;
                var mid = (_from[i] + _to[i]) * 0.5f;
                float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                _edge[i] = Quad(mid, angle, new Vector2(len, _halfWidth * 3.6f), Order, "edge");
                _core[i] = Quad(mid, angle, new Vector2(len * 0.995f, _halfWidth * 1.2f), Order + 1, "core");
            }
            Paint();
        }

        SpriteRenderer Quad(Vector2 at, float angle, Vector2 size, int order, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Square;
            sr.sortingOrder = order;
            return sr;
        }

        void Update()
        {
            _elapsed += Time.deltaTime;
            Paint();
            if (_elapsed >= _warning + _burn) { Destroy(gameObject); return; }
            if (_elapsed < _warning) return;

            _tick -= Time.deltaTime;
            if (_tick > 0f) return;
            _tick = TickSeconds;
            Burn(TickSeconds);
        }

        bool Burning => _elapsed >= _warning && _elapsed < _warning + _burn;

        void Paint()
        {
            if (_edge == null) return;
            float a;
            Color edge = _edgeColor, core = _coreColor;
            if (!Burning)
            {
                // The warning: a thin line brightening toward the moment it burns.
                float t = Mathf.Clamp01(_elapsed / _warning);
                a = Mathf.Lerp(0.15f, 0.5f, t) * (0.75f + 0.25f * Mathf.Sin(_elapsed * 24f));
                edge.a = a * 0.6f;
                core.a = 0f;
            }
            else
            {
                float left = _warning + _burn - _elapsed;
                a = Mathf.Clamp01(left / 0.25f);
                edge.a = 0.85f * a;
                core.a = 0.95f * a;
            }
            for (int i = 0; i < _edge.Length; i++)
            {
                if (_edge[i] != null) _edge[i].color = edge;
                if (_core[i] != null) _core[i].color = core;
            }
        }

        void Burn(float seconds)
        {
            if (_playerDps > 0f && _player != null && _playerHealth != null && !_playerHealth.IsDead)
            {
                if (Touches(_player.position, _halfWidth + BodyAllowance))
                    _playerHealth.Take(new DamageInfo(_playerDps * seconds, ElementType.Fire, gameObject));
            }

            if (_enemyDps > 0f)
            {
                Enemies.EnemyRegistry.Prune();
                var hits = new List<Health>();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    if (e == null) continue;
                    var hp = e.GetComponent<Health>();
                    if (hp == null || hp.IsDead) continue;
                    if (Touches(e.transform.position, _halfWidth + EnemyAllowance)) hits.Add(hp);
                }
                foreach (var hp in hits)
                    if (hp != null && !hp.IsDead)
                        hp.Take(new DamageInfo(_enemyDps * seconds, ElementType.Fire, _owner));
            }
        }

        bool Touches(Vector2 p, float within)
        {
            for (int i = 0; i < _from.Length; i++)
                if (DistanceToSegment(p, _from[i], _to[i]) <= within) return true;
            return false;
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------------------------------------------------------------- the lines themselves

        /// <summary>A line through <paramref name="through"/> at <paramref name="degrees"/>, run
        /// wall to wall across the arena's outer rectangle.</summary>
        public static (Vector2 A, Vector2 B) Across(Vector2 through, float degrees)
        {
            var h = Arena.HalfExtents;
            float rad = degrees * Mathf.Deg2Rad;
            var d = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            float tMin = float.NegativeInfinity, tMax = float.PositiveInfinity;
            Clip(through.x, d.x, -h.x, h.x, ref tMin, ref tMax);
            Clip(through.y, d.y, -h.y, h.y, ref tMin, ref tMax);
            if (float.IsInfinity(tMin) || float.IsInfinity(tMax) || tMin > tMax) return (through, through);
            return (through + d * tMin, through + d * tMax);
        }

        static void Clip(float p, float d, float lo, float hi, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(d) < 1e-5f) return;   // parallel: bounded by the other axis
            float a = (lo - p) / d, b = (hi - p) / d;
            if (a > b) (a, b) = (b, a);
            tMin = Mathf.Max(tMin, a);
            tMax = Mathf.Min(tMax, b);
        }

        /// <summary>The four headings a line may take: the room's own grain, and its diagonals.</summary>
        public static readonly float[] Headings = { 0f, 90f, 45f, 135f };
    }
}
