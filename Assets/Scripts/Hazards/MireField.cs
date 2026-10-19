using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A patch of slowing ground left by an elite Turret's mire shell (Enemies.MortarShell).
    ///
    /// GROUND, so it slows the way sand does: the player's speed ASKS for it
    /// (FloorPits.SpeedMultiplierAt), nothing is written onto the player, and where it overlaps
    /// a sand pit the slower of the two wins rather than the two multiplying - ground cannot be
    /// layered. It does no damage.
    ///
    /// A shell a parry TURNED lands blue, and its patch slows ENEMIES instead
    /// (<see cref="EnemySpeedAt"/>), never the player - the deflected Mortar shell's rule, "this
    /// one is yours now", carried to the ground it leaves.
    ///
    /// The disc's radius is the authority; the drawn ring sits exactly on it so what slows is
    /// what is drawn. Registered on enable rather than at spawn, so a domain reload (which empties
    /// the static list and re-enables every survivor) puts every live patch back.
    /// </summary>
    public class MireField : MonoBehaviour
    {
        /// <summary>
        /// THE MIRE'S COLOUR - the patch's rim, and everything that says "a mire is coming": the
        /// turret's wind-up glow, the shell in flight and its landing ring (EnemyController,
        /// Enemies.MortarShell all read this). Yellow, so the elite Turret's lob reads apart from
        /// the Mortar's red-orange shell at a glance.
        /// </summary>
        public static readonly Color Yellow = new(1f, 0.86f, 0.2f);

        /// <summary>The shell itself - a deeper yellow than the ring it is falling toward, so the
        /// body and the ground it marks stay two things.</summary>
        public static readonly Color ShellYellow = new(0.92f, 0.70f, 0.10f);

        // The patch: a yellow wash under a bright rim. The very first pass was a soft olive glow
        // that read as nothing against the brick - a slow the player cannot see is a slow they
        // cannot route round - so the fill is solid and the rim is the edge the radius check uses.
        static readonly Color MireFill = new(0.95f, 0.78f, 0.12f);
        static readonly Color TurnedFill = new(0.08f, 0.22f, 0.45f);
        static readonly Color TurnedRim = new(0.35f, 0.7f, 1f);

        // Not readonly, null-guarded: a domain reload resets the static and OnEnable refills it.
        static List<MireField> _live;

        // Plain serialised fields - see the domain-reload notes in CLAUDE.md.
        [SerializeField] float _radius;
        [SerializeField] bool _turned;
        [SerializeField] float _age;
        [SerializeField] SpriteRenderer _fill, _edge;

        // The Tempest Eye's charged ground (the user's call, 2026-10-18): the same slow, drawn as
        // a dark field with sparks flickering across it, short arcs jumping between them and a
        // broken rim turning round it, in the look's accent ramp. Repainted a few times a second
        // onto one texel-grid texture. Null for the yellow mire.
        [SerializeField] Color[] _static;
        [SerializeField] SpriteRenderer _sparks;
        Texture2D _sparkTex;
        Color[] _sparkPx;
        float _nextSpark;
        static readonly Color StaticFill = new(0.08f, 0.09f, 0.13f);

        public static MireField Spawn(Vector2 at, Transform parent, bool turned, Color[] staticRamp = null)
        {
            var go = new GameObject(turned ? "mire.turned" : "mire");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = at;

            var f = go.AddComponent<MireField>();
            f._radius = Tuning.Enemy.MireRadius;
            f._turned = turned;

            // Ground, so under every body but over the floor and the pit band - a patch landing
            // in a pit still reads.
            var fill = new GameObject("fill");
            fill.transform.SetParent(go.transform, false);
            fill.transform.localScale = Vector3.one * (f._radius * 2f);
            f._fill = fill.AddComponent<SpriteRenderer>();
            f._fill.sprite = Spr.Circle;
            f._fill.color = Color.clear;
            f._fill.sortingOrder = SortingOrders.Enemy - 1;

            var edge = new GameObject("edge");
            edge.transform.SetParent(go.transform, false);
            edge.transform.localScale = Vector3.one * (f._radius * 2f);
            f._edge = edge.AddComponent<SpriteRenderer>();
            f._edge.sprite = Spr.ThinRing;
            f._edge.color = Color.clear;
            f._edge.sortingOrder = SortingOrders.Enemy - 1;

            // Turned by a parry it is the player's blue whatever threw it.
            if (!turned && staticRamp != null && staticRamp.Length >= 4)
            {
                f._static = staticRamp;
                var sp = new GameObject("sparks");
                sp.transform.SetParent(go.transform, false);
                f._sparks = sp.AddComponent<SpriteRenderer>();
                f._sparks.sortingOrder = SortingOrders.Enemy - 1;
            }

            Spr.Flash(at, f._radius, turned ? TurnedRim : f._static != null ? f._static[2] : Yellow, 0.25f);
            return f;
        }

        void OnEnable() => (_live ??= new List<MireField>()).Add(this);
        void OnDisable() => _live?.Remove(this);

        void Update()
        {
            _age += Time.deltaTime;
            float life = Tuning.Enemy.MireSeconds;
            if (_age >= life) { Destroy(gameObject); return; }

            // In fast, out over the last stretch - the edge going soft is the warning that the
            // ground is about to be free again.
            float a = Mathf.Min(Mathf.Clamp01(_age / 0.15f), Mathf.Clamp01((life - _age) / 0.8f));
            bool charged = _static != null && !_turned;
            var fill = _turned ? TurnedFill : charged ? StaticFill : MireFill;
            var rim = _turned ? TurnedRim : charged ? _static[2] : Yellow;
            float ripple = 0.5f + 0.5f * Mathf.Sin(_age * 3.2f);
            float body = _turned ? 0.48f : charged ? 0.38f : 0.30f;
            if (_fill != null) _fill.color = new Color(fill.r, fill.g, fill.b, a * (body + 0.06f * ripple));
            // the charged ground's rim is the broken one its sparks draw; the solid ring stays faint
            if (_edge != null) _edge.color = new Color(rim.r, rim.g, rim.b, a * (charged ? 0.25f : 0.9f));
            if (charged) PaintSparks(a);
        }

        void PaintSparks(float alpha)
        {
            if (_sparks == null) return;
            if (_sparks.sprite != null) _sparks.color = new Color(1f, 1f, 1f, alpha);
            if (Time.time < _nextSpark && _sparkTex != null) return;
            _nextSpark = Time.time + 1f / 14f;

            float tpu = Enemies.TexelRig.TexelsPerUnit;
            int rT = Mathf.CeilToInt(_radius * tpu), n = rT * 2 + 2;
            if (_sparkTex == null || _sparkTex.width != n)
            {
                _sparkTex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
                };
                _sparkPx = new Color[n * n];
                _sparks.sprite = Sprite.Create(_sparkTex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), tpu, 0, SpriteMeshType.FullRect);
            }
            System.Array.Clear(_sparkPx, 0, _sparkPx.Length);
            int tick = Mathf.FloorToInt(Time.time * 14f);
            float c = n / 2f, r2 = rT * rT;

            // sparks flickering on and off across the ground
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                if (dx * dx + dy * dy > r2) continue;
                float h = Enemies.TexelRig.Hash(x * 7 + tick, y * 13);
                if (h > 0.975f) _sparkPx[y * n + x] = _static[h > 0.99f ? 3 : 2];
            }
            // short arcs jumping between them
            for (int i = 0; i < 4; i++)
            {
                float ax = c + (Enemies.TexelRig.Hash(i, tick) - 0.5f) * rT * 1.4f;
                float ay = c + (Enemies.TexelRig.Hash(tick, i) - 0.5f) * rT * 1.4f;
                for (int j = 0; j < 7; j++)
                {
                    int px = Mathf.FloorToInt(ax), py = Mathf.FloorToInt(ay);
                    if (px >= 0 && py >= 0 && px < n && py < n && (px + 0.5f - c) * (px + 0.5f - c) + (py + 0.5f - c) * (py + 0.5f - c) <= r2)
                        _sparkPx[py * n + px] = _static[3];
                    ax += 1.5f;
                    ay += (Enemies.TexelRig.Hash(i * 9 + j, tick) - 0.5f) * 3f;
                }
            }
            // the broken rim, turning
            float turn = Time.time * 60f;
            for (float deg = 0f; deg < 360f; deg += 1f)
            {
                if (Mathf.FloorToInt((deg + turn) / 12f) % 2 == 1) continue;
                int px = Mathf.FloorToInt(c + Mathf.Cos(deg * Mathf.Deg2Rad) * (rT - 0.5f));
                int py = Mathf.FloorToInt(c + Mathf.Sin(deg * Mathf.Deg2Rad) * (rT - 0.5f));
                if (px >= 0 && py >= 0 && px < n && py < n) _sparkPx[py * n + px] = _static[2];
            }
            _sparkTex.SetPixels(_sparkPx);
            _sparkTex.Apply();
        }

        void OnDestroy()
        {
            if (_sparks != null && _sparks.sprite != null) Destroy(_sparks.sprite);
            if (_sparkTex != null) Destroy(_sparkTex);
        }

        bool Contains(Vector2 p) => ((Vector2)transform.position - p).sqrMagnitude <= _radius * _radius;

        /// <summary>The player's speed multiplier at <paramref name="p"/>: the ordinary patches
        /// only. 1 where there is none.</summary>
        public static float PlayerSpeedAt(Vector2 p) => SpeedAt(p, turned: false, Tuning.Enemy.MirePlayerSpeedMul);

        /// <summary>An enemy's: the turned patches only.</summary>
        public static float EnemySpeedAt(Vector2 p) => SpeedAt(p, turned: true, Tuning.Enemy.MireEnemySpeedMul);

        static float SpeedAt(Vector2 p, bool turned, float inside)
        {
            if (_live == null) return 1f;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var f = _live[i];
                if (f == null) { _live.RemoveAt(i); continue; }
                if (f._turned == turned && f.Contains(p)) return inside;
            }
            return 1f;
        }
    }
}
