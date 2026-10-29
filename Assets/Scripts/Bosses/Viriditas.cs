using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Lighting;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Enemies;

namespace Convergence.Bosses
{
    /// <summary>
    /// Viriditas: a beetle knight in a grove (the user's design, 2026-10-27 - BOSSES.md). Every number
    /// is in <see cref="Tuning.Viriditas"/>.
    ///
    ///     STOMPS   it walks at the player and stomps a CONE aimed where they stand (locked just before
    ///              the slam); saplings inside shoot up into TREES, the player inside is hurt. It can be
    ///              hit all the while - standing in the cone to keep hitting is a trade the player makes
    ///     ROLL     it curls into its seed and HOMES, as fast as the player at first and quickly faster.
    ///              Unavoidable: it stops only when it has broken HitsToStop of the grove, staggered by
    ///              each. A TREE gives it nothing (two trees: VULNERABLE); a SAPLING raises its POWER.
    ///              Reaching the player first: it GRABS them (big damage, never power) and the beetles
    ///              cover it - the WARD - until the next roll
    ///     BLOOM    enraged, and from band 4 always: a lotus opens over it and pollen fills the room;
    ///              only a tree's shadow is safe. Then it must pause
    ///
    /// Anything it breaks leaves SAP that spreads and ticks until the grove regrows - at the enrage,
    /// and whenever too little is left standing to stop a roll (the fallback bloom).
    ///
    /// It is NEVER immune. The Boss base's window cap is spent per CYCLE instead (<see cref="OpenCycle"/>),
    /// and the ward and vulnerability are a multiplier on its Health (ScaleIncoming, which no enemy uses).
    ///
    /// THE GROVE IS THE AUTHORITY on the Bloom: <see cref="Grove.Shadowed"/> decides, and the pollen is
    /// painted from the same call (<see cref="BloomFill"/>).
    /// </summary>
    public class Viriditas : Boss
    {
        public enum Movement { Stirring, Stomping, Rolling, Grabbing, Blooming, Open, Regrowing }
        public Movement Phase { get; private set; } = Movement.Stirring;

        public override string DisplayName => "VIRIDITAS";
        public override string PhaseLabel => Phase switch
        {
            Movement.Stomping => _ward ? "WARDED" : "STOMPING",
            Movement.Rolling => "ROLLING",
            Movement.Grabbing => "CAUGHT",
            Movement.Blooming => "FULL BLOOM",
            Movement.Open => "OPEN",
            Movement.Regrowing => "THE GROVE REGROWS",
            _ => _ward ? "WARDED" : "STIRRING",
        };

        public int Cycle { get; private set; }
        /// <summary>Its POWER: 1 at the start (the user's "mid power"), up with every sapling it rolls into.</summary>
        public float Power => _power;
        public bool Warded => _ward;
        public bool Vulnerable => _vulnerable;
        public Grove Grove => _grove;
        /// <summary>Hittable all fight, but the bar reads OPEN only while it is vulnerable.</summary>
        public override bool ReadsShut => !_vulnerable;

        [SerializeField] Transform _room;
        [SerializeField] Grove _grove;
        [SerializeField] BloomFill _bloom;
        [SerializeField] ViriditasFigure _figure;
        [SerializeField] Glow _glow;
        [SerializeField] float _power = 1f;
        [SerializeField] bool _ward, _vulnerable;
        [SerializeField] int _treesHit;
        [SerializeField] bool _caught;
        [SerializeField] Vector2 _at;

        // Non-readonly and re-created if null - see ENGINEERING.md, domain reload traps.
        List<SapPool> _sap = new();
        List<EnemyController> _adds = new();

        static readonly Color ItsLight = new(0.66f, 0.94f, 0.78f);
        static readonly Color OpenLight = new(1f, 0.86f, 0.45f);

        int HitsNeeded => Enraged ? Tuning.Viriditas.HitsToStopEnraged : Tuning.Viriditas.HitsToStop;

        public static Viriditas Spawn(Vector2 at, Transform target, Transform parent, int floor)
        {
            var b = CreateBody<Viriditas>("boss.viriditas", at, target, parent, floor, 0.7f, Tuning.Viriditas.Hp);
            // It walks and rolls, so it is drawn where it is (Boss.CreateBody's note).
            var rb = b.GetComponent<Rigidbody2D>();
            if (rb != null) rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            b._at = at;

            b._figure = TexelPicture.Attach<ViriditasFigure>(b.transform, "figure", Vector2.zero, 46, 8, 100, 0);
            DepthSorted.Attach(b.gameObject, 0f, false, b._figure.GetComponent<SpriteRenderer>());
            b._glow = Glow.Attach(b.gameObject, 2.6f, ItsLight, 0.6f, 0.04f);

            var room = new GameObject("viriditas.room");
            room.transform.SetParent(parent, false);
            b._room = room.transform;
            b._grove = Grove.Build(b._room);
            b._bloom = BloomFill.Build(b._room, b._grove);

            // Never immune: the cap is per cycle (OpenCycle), and it can be hit from the first frame.
            b.Health.Immune = false;
            if (b.BodyCollider != null) b.BodyCollider.enabled = true;
            b.HookIncoming();

            b.StartCoroutine(b.Fight());
            b.StartCoroutine(b.SapTicks());
            return b;
        }

        /// <summary>The ward and the vulnerability, as one multiplier on every hit it takes. A delegate,
        /// so it is lost to a domain reload - Update puts it back.</summary>
        void HookIncoming()
        {
            if (Health != null) Health.ScaleIncoming = _ => _ward ? Tuning.Viriditas.WardTaken
                                                       : _vulnerable ? Tuning.Viriditas.VulnerableTaken : 1f;
        }

        public override void Cease()
        {
            base.Cease();
            if (_adds != null)
            {
                foreach (var a in _adds) if (a != null) Destroy(a.gameObject);
                _adds.Clear();
            }
            _sap?.Clear();
            if (_room != null) Destroy(_room.gameObject);
            _room = null; _grove = null; _bloom = null;
        }

        /// <summary>Its death (the user's pick): a seed again, and a sapling grows out of it.</summary>
        public override void DeathPicture()
            => ViriditasDeath.At(transform.position, _figure != null ? _figure.Look : default);

        void Update()
        {
            if (Health != null && Health.ScaleIncoming == null) HookIncoming();
            if (_figure != null)
            {
                _figure.Look.Ward = Mathf.MoveTowards(_figure.Look.Ward, _ward ? 1f : 0f, Time.deltaTime / 0.5f);
                _figure.Look.Flowers = Mathf.Min(12, Mathf.RoundToInt((_power - 1f) / Tuning.Viriditas.PowerPerSapling) * Tuning.Viriditas.FlowersPerSapling);
                _figure.Look.Flowered = Enraged;
            }
        }

        void MoveTo(Vector2 p)
        {
            _at = Arena.Clamp(p, 0.6f);
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.MovePosition(_at);
            else transform.position = _at;
        }

        Vector2 PlayerAt => Target != null ? ReleaseCanvas.Feet(Target) : Vector2.zero;

        /// <summary>How far the ball is from the player - the nearer of their feet and their body,
        /// so a player tucked against a wall or in the doorway is still within reach.</summary>
        float PlayerGap => Target == null ? float.MaxValue
            : Mathf.Min((PlayerAt - _at).magnitude, ((Vector2)Target.position - _at).magnitude);

        // ------------------------------------------------------------------ the fight

        IEnumerator Fight()
        {
            int token = ++Token;
            yield return new WaitForSeconds(0.8f);
            if (token != Token) yield break;

            while (true)
            {
                bool was = Enraged;
                CheckEnrage(Tuning.Viriditas.EnrageAt);
                // The enrage: a lotus blooms on it and the whole grove comes back (the user's rule).
                if (Enraged && !was)
                {
                    yield return Regrow(token);
                    if (token != Token) yield break;
                }
                // The fallback bloom: too little left standing to ever stop a roll - regrow, no enrage.
                if (_grove != null && _grove.StandingCount < HitsNeeded)
                {
                    yield return Regrow(token);
                    if (token != Token) yield break;
                }

                OpenCycle();
                int stomps = Band >= Tuning.EnemyBands.ViriditasRandomStompsFromBand
                           ? Random.Range(Tuning.Viriditas.RandomStompsMin, Tuning.Viriditas.RandomStompsMax + 1)
                           : Tuning.Viriditas.Stomps;
                for (int i = 0; i < stomps; i++)
                {
                    yield return Approach(token);
                    if (token != Token) yield break;
                    yield return Stomp(token);
                    if (token != Token) yield break;
                }

                yield return Roll(token);
                if (token != Token) yield break;

                bool bloom = Enraged || Band >= Tuning.EnemyBands.ViriditasBloomAlwaysFromBand;
                if (bloom)
                {
                    yield return Bloom(token);
                    if (token != Token) yield break;
                }

                if (!_caught && _treesHit >= Tuning.Viriditas.TreesForVulnerable)
                    yield return Open(token);
                else
                    yield return new WaitForSeconds(bloom ? Tuning.Viriditas.PauseSeconds : Tuning.Viriditas.RecoverSeconds);
                if (token != Token) yield break;

                Cycle++;
            }
        }

        /// <summary>
        /// THE CAP, per cycle: it is never immune, so the Boss base's window cap is spent across a
        /// whole cycle - stomps, roll and window - rather than one window. Whatever the build, a cycle
        /// takes no more than Tuning.Boss.WindowCap of its health; the last may kill.
        /// </summary>
        void OpenCycle()
        {
            if (Health == null) return;
            float floor = Health.Current - Health.Max * Tuning.Boss.WindowCap;
            Health.Floor = floor > 0f ? floor : 0f;
            Health.Immune = false;
            if (BodyCollider != null) BodyCollider.enabled = true;
        }

        // ------------------------------------------------------------------ STOMPS

        IEnumerator Approach(int token)
        {
            Phase = Movement.Stomping;
            if (_figure != null) _figure.Look.Walk = true;
            float t = 0f;
            while (t < Tuning.Viriditas.WalkMaxSeconds && Target != null)
            {
                var d = PlayerAt - _at;
                if (d.magnitude <= Tuning.Viriditas.StompReach) break;
                // Round its trees, not through them: the push off each turns its step sideways.
                var step = d.normalized + (_grove != null ? _grove.Avoid(_at, 0.7f) * 2.5f : Vector2.zero);
                MoveTo(_at + step.normalized * Tuning.Viriditas.WalkSpeed * Time.deltaTime);
                t += Time.deltaTime;
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) _figure.Look.Walk = false;
        }

        IEnumerator Stomp(int token)
        {
            Phase = Movement.Stomping;
            var cone = _room != null ? StompCone.At(_room, _at, Cycle * 7 + Random.Range(0, 99)) : null;
            float tell = Tuning.Viriditas.StompTellSeconds, t = 0f;
            Vector2 aim = (PlayerAt - _at).sqrMagnitude > 1e-4f ? (PlayerAt - _at).normalized : Vector2.down;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Grind, _at, 0.7f);
            while (t < tell)
            {
                t += Time.deltaTime;
                // It tracks the player through the tell and LOCKS for the last moment - stepping out is
                // the answer, as the user's rule has it.
                if (t < tell - Tuning.Viriditas.AimLockSeconds && Target != null)
                {
                    var d = PlayerAt - _at;
                    if (d.sqrMagnitude > 1e-4f) aim = d.normalized;
                }
                if (cone != null) { cone.Aim = Mathf.Atan2(aim.y, aim.x); cone.Fill = Mathf.Clamp01(t / tell); }
                if (_figure != null) _figure.Look.Rear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / (tell * 0.8f)));
                yield return null;
                if (token != Token) { if (cone != null) Destroy(cone.gameObject); yield break; }
            }

            // It SLAMS down (the user's yes): roots crack out along the cone, and the saplings in it
            // shoot up into trees.
            if (_figure != null) _figure.Look.Rear = 0f;
            if (cone != null) cone.Slam();
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Slam, _at);
            TexelFx.Puff(_at, GroveCanvas.Leaf, 14, 10f, 0.5f);
            if (_grove != null)
                for (int i = 0; i < _grove.Count; i++)
                    if (InCone(_grove.Position(i), aim)) _grove.Grow(i);
            if (Target != null && InCone(PlayerAt, aim)) HurtPlayer(Tuning.Viriditas.StompFraction * _power);

            yield return new WaitForSeconds(Tuning.Viriditas.StompGapSeconds);
        }

        bool InCone(Vector2 p, Vector2 aim)
        {
            var d = p - _at;
            float dist = d.magnitude;
            if (dist > Tuning.Viriditas.ConeLength) return false;
            if (dist < 0.6f) return true;
            return Vector2.Angle(aim, d) <= Tuning.Viriditas.ConeHalfAngleDegrees;
        }

        // ------------------------------------------------------------------ ROLL

        IEnumerator Roll(int token)
        {
            Phase = Movement.Rolling;
            _ward = false;            // the ward lasts until the next roll (the user's rule)
            _treesHit = 0; _caught = false;
            int hits = 0;

            // It curls into its seed.
            for (float c = 0f; c < Tuning.Viriditas.CurlSeconds; c += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Curl = Mathf.Clamp01(c / Tuning.Viriditas.CurlSeconds);
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) _figure.Look.Curl = 1f;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Charge, _at);

            Vector2 dir = (PlayerAt - _at).sqrMagnitude > 1e-4f ? (PlayerAt - _at).normalized : Vector2.down;
            float speed = Tuning.Player.MoveSpeed, t = 0f, trail = 0f;
            while (true)
            {
                float dt = Time.deltaTime;
                t += dt;
                if (t > Tuning.Viriditas.RollMaxSeconds) { _caught = true; _ward = true; break; }

                // It homes the whole time (the user's call), faster and faster.
                var want = PlayerAt - _at;
                if (want.sqrMagnitude > 1e-4f)
                    dir = Vector3.RotateTowards(dir, want.normalized, Tuning.Viriditas.RollTurnDegrees * Mathf.Deg2Rad * dt, 0f);
                speed = Mathf.Min(Tuning.Viriditas.RollMaxSpeed, speed + Tuning.Viriditas.RollAccel * dt);
                MoveTo(_at + dir * speed * dt);
                if (_figure != null) _figure.Look.Roll -= speed * dt / Tuning.Viriditas.BallRadius * Mathf.Sign(dir.x == 0f ? 1f : dir.x);

                // Leaves and speed lines streaming off it (the user's pick) - more as it speeds up.
                trail -= dt;
                if (trail <= 0f)
                {
                    trail = Mathf.Lerp(0.12f, 0.04f, (speed - Tuning.Player.MoveSpeed) / (Tuning.Viriditas.RollMaxSpeed - Tuning.Player.MoveSpeed));
                    TexelFx.Puff(_at - dir * Tuning.Viriditas.BallRadius, GroveCanvas.Leaf, 3, 4f, 0.35f);
                }

                // The player: caught. Dash i-frames do not pass it (the user's call) - the touch is a
                // touch, and the hold outlasts any dash.
                if (Target != null && PlayerGap <= Tuning.Viriditas.BallRadius + 0.35f)
                {
                    yield return Grab(token);
                    break;
                }

                // The grove: staggered by every hit; a sapling feeds it, two trees open it.
                int slot = _grove != null ? _grove.Touching(_at, Tuning.Viriditas.BallRadius) : -1;
                if (slot >= 0)
                {
                    var spot = _grove.Position(slot);
                    var was = _grove.Break(slot);
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Rubble, spot);
                    LeaveSap(spot);
                    if (was == Grove.State.Sapling)
                        _power = Mathf.Min(Tuning.Viriditas.PowerCap, _power + Tuning.Viriditas.PowerPerSapling);
                    else
                    {
                        _treesHit++;
                        if (Band >= Tuning.EnemyBands.ViriditasAddsFromBand) BeetleAdd(spot);
                    }
                    hits++;
                    yield return Stagger(token, (_at - spot).normalized);
                    if (token != Token) yield break;
                    if (hits >= HitsNeeded) break;
                    speed = Tuning.Player.MoveSpeed;
                }

                yield return null;
                if (token != Token) yield break;
            }

            // It uncurls.
            for (float c = 0f; c < 0.35f; c += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Curl = 1f - c / 0.35f;
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) { _figure.Look.Curl = 0f; _figure.Look.Grab = 0f; }
        }

        IEnumerator Stagger(int token, Vector2 away)
        {
            if (_figure != null) _figure.Look.Dazed = true;
            Vector2 from = _at, to = _at + away * Tuning.Viriditas.BounceDistance;
            for (float b = 0f; b < 0.15f; b += Time.deltaTime)
            {
                MoveTo(Vector2.Lerp(from, to, b / 0.15f));
                yield return null;
                if (token != Token) yield break;
            }
            yield return new WaitForSeconds(Mathf.Max(0f, Tuning.Viriditas.StaggerSeconds - 0.15f));
            if (_figure != null) _figure.Look.Dazed = false;
        }

        /// <summary>
        /// Glaciron's grab (the user's pick): it unravels round the player, its legs clamp and squeeze
        /// - held still for GrabSeconds - then throws them off. Big damage (the user's call), scaled by
        /// its power like any of its blows, but it never RAISES its power. Then the beetles cover it.
        /// </summary>
        IEnumerator Grab(int token)
        {
            Phase = Movement.Grabbing;
            _caught = true;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Growl, _at);
            var pc = Target != null ? Target.GetComponent<Player.PlayerController>() : null;
            if (Target != null) StatusEffects.Get(Target.gameObject).ApplyPetrify(Tuning.Viriditas.GrabSeconds + 0.1f);
            // It unravels ON the player: its legs close round them, not round the air beside them.
            Vector2 from = _at, onto = Target != null ? PlayerAt + Vector2.up * 0.25f : _at;

            float g = Tuning.Viriditas.GrabSeconds, squeeze = 0f;
            for (float t = 0f; t < g; t += Time.deltaTime)
            {
                if (t < 0.15f) MoveTo(Vector2.Lerp(from, onto, t / 0.15f));
                if (_figure != null) { _figure.Look.Curl = Mathf.Max(0f, 1f - t / 0.2f) * 0.49f; _figure.Look.Grab = Mathf.Clamp01(t / 0.2f); _figure.Look.Rear = 0.6f; }
                squeeze -= Time.deltaTime;
                if (squeeze <= 0f) { squeeze = 0.25f; Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Grind, _at, 0.8f); }
                yield return null;
                if (token != Token) yield break;
            }

            HurtPlayer(Tuning.Viriditas.CatchFraction * _power);
            if (pc != null && pc.Health != null && !pc.Health.IsDead)
            {
                var d = (Vector2)Target.position - _at;
                var dir = d.sqrMagnitude > 1e-4f ? d.normalized : Vector2.down;
                pc.Dash(dir, Tuning.Viriditas.ThrowSpeed, Tuning.Viriditas.ThrowDistance / Tuning.Viriditas.ThrowSpeed);
            }
            if (_figure != null) { _figure.Look.Grab = 0f; _figure.Look.Rear = 0f; }
            _ward = true;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Seal, _at);
        }

        // ------------------------------------------------------------------ BLOOM

        /// <summary>
        /// Full Bloom (Mechanar's nuke, the user's pick): a lotus opens over it as the pollen fills the
        /// room from it (the clock); then it blooms, held for BloomHoldSeconds like Medusa's look - so
        /// a dash's i-frames cannot step through. Only a TREE's shadow is safe.
        /// </summary>
        IEnumerator Bloom(int token)
        {
            Phase = Movement.Blooming;
            var eye = _at;
            if (_bloom != null) _bloom.Begin(eye);
            float tell = Tuning.Viriditas.BloomTellSeconds, cue = 0f;
            for (float t = 0f; t < tell; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / tell);
                if (_bloom != null) _bloom.Fill = k;
                if (_figure != null) _figure.Look.Bloom = k;
                cue -= Time.deltaTime;
                if (cue <= 0f)
                {
                    cue = Mathf.Lerp(0.5f, 0.12f, k);
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Crackle, _at, Mathf.Lerp(0.4f, 0.9f, k));
                }
                yield return null;
                if (token != Token) yield break;
            }

            if (_bloom != null) _bloom.Bloom();
            Glow.Flash(_at + Vector2.up * 1.6f, 4f, OpenLight, 0.5f);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Erupt, _at);
            bool hit = false;
            for (float h = 0f; h < Tuning.Viriditas.BloomHoldSeconds; h += Time.deltaTime)
            {
                if (!hit && Target != null)
                {
                    var hp = Target.GetComponent<Health>();
                    if (hp != null && !hp.IsDead && !hp.Immune && _grove != null && !_grove.Shadowed(eye, PlayerAt))
                    {
                        HurtPlayer(Tuning.Viriditas.BloomFraction * _power);
                        hit = true;
                    }
                }
                yield return null;
                if (token != Token) yield break;
            }
            for (float f = 0f; f < 0.5f; f += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Bloom = 1f - f / 0.5f;
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) _figure.Look.Bloom = 0f;
        }

        // ------------------------------------------------------------------ OPEN

        /// <summary>Two trees stopped it: its shell splits open, the body glowing gold (the user's
        /// pick), and it takes VulnerableTaken for VulnerableSeconds - or until the cycle's cap.</summary>
        IEnumerator Open(int token)
        {
            Phase = Movement.Open;
            _vulnerable = true;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Vulnerable, _at);
            if (_glow != null) _glow.Color = OpenLight;
            float seconds = Tuning.Viriditas.VulnerableSeconds;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (WindowSpent) break;
                if (_figure != null) _figure.Look.Split = Mathf.Clamp01(t / 0.3f);
                yield return null;
                if (token != Token) yield break;
            }
            _vulnerable = false;
            if (_glow != null) _glow.Color = ItsLight;
            for (float f = 0f; f < 0.3f; f += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Split = 1f - f / 0.3f;
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) _figure.Look.Split = 0f;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Seal, _at);
        }

        // ------------------------------------------------------------------ REGROW

        /// <summary>
        /// The grove regrows (the user's pick, "pollen settles"): the lotus opens, pollen drifts out
        /// over the room, the sap clears, and saplings come up on every empty spot as it settles.
        /// At the enrage, and as the fallback when too little is left standing.
        /// </summary>
        IEnumerator Regrow(int token)
        {
            Phase = Movement.Regrowing;
            float s = Tuning.Viriditas.RegrowSeconds;
            if (_room != null) PollenDrift.At(_room, _at + Vector2.up * 1.4f, s);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Toll, _at);

            var order = new List<int>();
            if (_grove != null) for (int i = 0; i < _grove.Count; i++) if (!_grove.Standing(i)) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (order[i], order[j]) = (order[j], order[i]); }

            ClearSap();
            int next = 0;
            for (float t = 0f; t < s; t += Time.deltaTime)
            {
                float k = t / s;
                if (_figure != null) _figure.Look.Bloom = Mathf.Min(1f, k * 3f) * (1f - Mathf.Clamp01((k - 0.8f) / 0.2f));
                // Spread through the middle of the drift, where the pollen settles.
                int due = Mathf.FloorToInt(Mathf.Clamp01((k - 0.3f) / 0.5f) * order.Count);
                while (next < due && _grove != null) _grove.Regrow(order[next++]);
                yield return null;
                if (token != Token) yield break;
            }
            while (next < order.Count && _grove != null) _grove.Regrow(order[next++]);
            if (_figure != null) _figure.Look.Bloom = 0f;
        }

        // ------------------------------------------------------------------ SAP

        void LeaveSap(Vector2 at)
        {
            if (_room == null) return;
            _sap ??= new List<SapPool>();
            float growth = Band >= Tuning.EnemyBands.ViriditasThickSapFromBand ? Tuning.Viriditas.SapGrowthBand : Tuning.Viriditas.SapGrowth;
            _sap.Add(SapPool.At(_room, at, growth));
        }

        void ClearSap()
        {
            if (_sap == null) return;
            foreach (var p in _sap) if (p != null) Destroy(p.transform.parent != null ? p.transform.parent.gameObject : p.gameObject);
            _sap.Clear();
        }

        /// <summary>One tick every SapTickSeconds while the player stands in any sap - chip damage,
        /// flat and scaled by depth like any enemy's (the Boss base's rule), and a Tick so it blinks.</summary>
        IEnumerator SapTicks()
        {
            int token = Token;
            while (true)
            {
                yield return new WaitForSeconds(Tuning.Viriditas.SapTickSeconds);
                if (token != Token) yield break;
                if (_sap == null || Target == null) continue;
                var feet = PlayerAt;
                bool inside = false;
                foreach (var p in _sap)
                    if (p != null && (p.Centre - feet).sqrMagnitude <= p.Radius * p.Radius) { inside = true; break; }
                if (!inside) continue;
                var hp = Target.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                    hp.Take(new DamageInfo(Tuning.Viriditas.SapTickDamage * FloorDifficulty.Damage(Floor), ElementType.Earth, gameObject) { Tick = true, IgnoresHeat = true });
            }
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>A MECHANIC hit: a share of max health, mitigated through Take (Brace and the
        /// ledger count) - the Boss base's rule.</summary>
        void HurtPlayer(float fraction)
        {
            if (Target == null) return;
            var hp = Target.GetComponent<Health>();
            if (hp == null || hp.IsDead) return;
            hp.Take(new DamageInfo(hp.Max * fraction, ElementType.Earth, gameObject) { IgnoresHeat = true });
        }

        /// <summary>Band 3: a beetle crawls out of each broken tree. Its own child like Medusa's adds -
        /// not in the wave, destroyed with it, a kill but never a Rift Box.</summary>
        void BeetleAdd(Vector2 at)
        {
            if (Target == null) return;
            _adds ??= new List<EnemyController>();
            _adds.RemoveAll(a => a == null);
            if (_adds.Count >= Tuning.Viriditas.MaxAdds) return;
            var e = EnemyFactory.Spawn(at, EnemyKind.Chaser, Floor, Target, transform.parent);
            if (e == null) return;
            var target = Target;
            var h = e.GetComponent<Health>();
            if (h != null)
                h.Died += dead =>
                {
                    var pc = target != null ? target.GetComponent<Player.PlayerController>() : null;
                    if (pc != null) pc.RegisterKill();
                    Destroy(dead.gameObject);
                };
            _adds.Add(e);
        }
    }
}
