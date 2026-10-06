using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Hazards;
using T = Convergence.Core.Tuning.Exchange;

namespace Convergence.Exchange
{
    public partial class RunEffects
    {
        // ---------------------------------------------------------------- state

        float _retroTimer, _invertedRemaining, _contraryRemaining, _retroCue;
        float _projectionTimer, _leyTimer, _antipathyTimer;

        float RetrogradeEvery => AtMax("retrograde") ? T.RetrogradeEveryII : T.RetrogradeEvery;
        float ProjectionEvery => AtMax("projection") ? T.ProjectionEveryII : T.ProjectionEvery;

        /// <summary>True while the movement inputs are inverted - Retrograde's flip, or Contrary's.</summary>
        public bool InputsInverted => _invertedRemaining > 0f || _contraryRemaining > 0f;

        /// <summary>Retrograde's warning is showing: a flip is coming.</summary>
        public bool InversionComing => Has("retrograde") && FightLive && _retroTimer > 0f && _retroTimer <= T.RetrogradeWarning;

        /// <summary>
        /// The run's field effects fire only while there is a fight to fire into. Out of combat a
        /// flip or a line costs nothing worth having - walking to the door is not the test.
        /// </summary>
        static bool FightLive
        {
            get
            {
                Enemies.EnemyRegistry.Prune();
                foreach (var e in Enemies.EnemyRegistry.All)
                {
                    if (e == null) continue;
                    var hp = e.GetComponent<Health>();
                    if (hp != null && !hp.IsDead) return true;
                }
                return false;
            }
        }

        void TickField(float dt)
        {
            Countdown(ref _invertedRemaining, dt);
            Countdown(ref _contraryRemaining, dt);
            if (_pc == null) return;

            bool live = (Has("retrograde") || Has("projection") || Has("ley_lines") || Has("antipathy")) && FightLive;
            if (!live) return;

            if (Has("retrograde")) TickRetrograde(dt);

            if (Has("projection"))
            {
                _projectionTimer -= dt;
                if (_projectionTimer <= 0f) { CastProjection(); _projectionTimer = ProjectionEvery; }
            }

            if (Has("ley_lines"))
            {
                _leyTimer -= dt;
                if (_leyTimer <= 0f) { CastLeyLines(); _leyTimer = T.LeyLinesEvery; }
            }

            if (Has("antipathy"))
            {
                _antipathyTimer -= dt;
                if (_antipathyTimer <= 0f) { Antipathy(); _antipathyTimer = T.AntipathyEvery; }
            }
        }

        // ---------------------------------------------------------------- Retrograde

        /// <summary>
        /// Every few seconds the movement inputs invert. A SKILL test, never luck: a ring closes
        /// at the player's feet through the warning, so the flip can be played around.
        /// </summary>
        void TickRetrograde(float dt)
        {
            _retroTimer -= dt;
            if (_retroTimer > 0f && _retroTimer <= T.RetrogradeWarning)
            {
                _retroCue -= dt;
                if (_retroCue <= 0f)
                {
                    float t = 1f - _retroTimer / T.RetrogradeWarning;
                    _retroCue = Mathf.Lerp(0.16f, 0.07f, t);
                    Spr.Pulse(_pc.transform, Mathf.Lerp(1.6f, 0.6f, t), new Color(0.75f, 0.5f, 1f, 0.35f + 0.45f * t),
                              0.2f, true, -0.5f);
                }
            }
            if (_retroTimer > 0f) return;
            _retroTimer = RetrogradeEvery;
            _retroCue = 0f;
            _invertedRemaining = T.RetrogradeSeconds;
            Spr.Flash(_pc.transform.position, 1.1f, new Color(0.75f, 0.5f, 1f), 0.25f);
        }

        // ---------------------------------------------------------------- Projection, Ley Lines

        void CastProjection()
        {
            var lines = new List<(Vector2 A, Vector2 B)>();
            var h = Arena.HalfExtents;
            for (int i = 0; i < T.ProjectionLines; i++)
            {
                var through = new Vector2(Random.Range(-h.x * 0.8f, h.x * 0.8f), Random.Range(-h.y * 0.8f, h.y * 0.8f));
                lines.Add(ProjectionLines.Across(through, ProjectionLines.Headings[Random.Range(0, ProjectionLines.Headings.Length)]));
            }
            // Lattice: a second set crosses the first, drawn through where the player stands.
            if (AtMax("projection"))
            {
                Vector2 at = _pc.transform.position;
                lines.Add(ProjectionLines.Across(at, 45f));
                lines.Add(ProjectionLines.Across(at, 135f));
            }
            ProjectionLines.ForPlayer(lines, T.ProjectionWarning, T.ProjectionBurn, T.ProjectionDamagePerSecond, _pc.transform);
        }

        /// <summary>Ley Lines: the same lines, the other way round - through the nearest enemies,
        /// burning them in the player's own hit units.</summary>
        void CastLeyLines()
        {
            var lines = new List<(Vector2 A, Vector2 B)>();
            Vector2 from = _pc.transform.position;
            var near = new List<(float D, Vector2 P)>();
            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                near.Add((((Vector2)e.transform.position - from).sqrMagnitude, e.transform.position));
            }
            if (near.Count == 0) return;
            near.Sort((a, b) => a.D.CompareTo(b.D));
            for (int i = 0; i < Mathf.Min(T.ProjectionLines, near.Count); i++)
                lines.Add(ProjectionLines.Across(near[i].P, ProjectionLines.Headings[Random.Range(0, ProjectionLines.Headings.Length)]));

            var tint = ElementInfo.Tint(Element);
            ProjectionLines.ForEnemies(lines, T.ProjectionWarning * 0.5f, T.ProjectionBurn,
                                       T.LeyLinesHitUnits * _pc.HitUnit, _pc.gameObject, tint);
        }

        // ---------------------------------------------------------------- Antipathy

        /// <summary>Every few seconds the crowd round the player turns away - staggered, through
        /// the player's own stagger, so the board's Stagger rules apply.</summary>
        void Antipathy()
        {
            Vector2 from = _pc.transform.position;
            Enemies.EnemyRegistry.Prune();
            var hit = new List<Health>();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (((Vector2)e.transform.position - from).sqrMagnitude <= T.AntipathyRadius * T.AntipathyRadius) hit.Add(hp);
            }
            foreach (var hp in hit) _pc.Stagger(hp, T.AntipathySeconds);
            Spr.Flash(from, T.AntipathyRadius, new Color(0.75f, 0.5f, 1f, 0.5f), 0.3f);
        }
    }
}
