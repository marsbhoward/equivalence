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
    /// Icarus: a sword duellist in the Malenia mould (the user's design, 2026-10-28 - BOSSES.md
    /// "Icarus"). Every number is in <see cref="Tuning.Icarus"/>.
    ///
    /// NEVER IMMUNE but at the cap, which counts over ANY 12 s (a rolling log of what he has taken). He
    /// HEALS on every blow that lands on the player - all the way back to full. His moves, BY DISTANCE:
    ///
    ///     near   STRINGS of blows, some HELD high before they fall; his blows CLASH (IClashes), and a
    ///            parried blow throws him back. Then a PAUSE
    ///     far    the LUNGE: he rises, a wide circle follows the player, LOCKS, and he dives into it.
    ///            Step aside (a dash's i-frames do not pass it). Caught: impaled, four blows, flung.
    ///            Parried: a STUN, then the FAN
    ///     clock  the WELL: the blade's star rises off the guard and opens over the middle, pulling the
    ///            player and the stones torn from the walls. He keeps fighting while it pulls
    ///
    /// The FAN (bands 3 and 4, and the end of every stun): a glint, then a two-handed sweep that fans the
    /// player's attack away (Health.BlocksHit) - blown back, a held blow following; arrows and discs
    /// fanned back as bolts. The enrage makes EVERYTHING 1.3x faster. The room comes apart as his HP
    /// falls (IcarusSky), and never rebuilds.
    /// </summary>
    public class Icarus : Boss, IClashes
    {
        public enum Movement { Walking, String, Pause, Rising, Diving, Caught, Stunned, Fanning, Recovering, Dance }
        enum Result { Miss, Hit, Clashed, Parried, Caught }
        const int Down = 0, Up = 1, Thrust = 2;

        public Movement Phase { get; private set; } = Movement.Walking;

        public override string DisplayName => "ICARUS";
        public override string PhaseLabel => Phase switch
        {
            Movement.Stunned => "STUNNED",
            Movement.Rising or Movement.Diving => "THE LUNGE",
            Movement.Dance => "THE DANCE",
            Movement.Caught => "IMPALED",
            Movement.Fanning => "THE FAN",
            _ => _wellAge >= 0f ? "THE STAR" : Enraged ? "BURNING" : "THE DUEL",
        };
        /// <summary>Hittable all fight; the bar reads OPEN while he is stunned (a parried dive).</summary>
        public override bool ReadsShut => Phase != Movement.Stunned;

        // ---- clashing: the last Tuning.Clash.WindowSeconds of a blow's wind, and its strike ----
        public bool StrikeImminent => _imminent && Health != null && !Health.IsDead;
        public void Clashed() { _clashed = true; _imminent = false; }

        [SerializeField] int _seed, _farMoves, _blows, _stones;
        [SerializeField] Vector2 _at, _starFrom;
        [SerializeField] Transform _room;
        [SerializeField] IcarusFigure _figure;
        [SerializeField] IcarusMarks _marks;
        [SerializeField] IcarusCircle _circle;
        [SerializeField] IcarusSky _sky;
        [SerializeField] IcarusWell _well;
        [SerializeField] Glow _glow, _shaft, _cracks;
        [SerializeField] bool _imminent, _clashed, _fanOpen, _fanned;
        [SerializeField] Result _result, _landed;
        [SerializeField] float _sinceLunge = 99f, _wellClock, _wellAge = -1f, _stoneNext, _stoneGrace, _lowest = 1f, _lastHp = -1f;
        [SerializeField] float _dark0 = -1f, _darkSet = -1f;
        [SerializeField] Color _shade0;
        /// <summary>The cap's stretch: (time, health lost) for every drop in the last CapSeconds.</summary>
        [SerializeField] List<Vector2> _taken = new();
        [SerializeField] int _debris;

        static readonly Color StarLight = new(1f, 0.7f, 0.38f), SkyLight = new(0.78f, 0.84f, 1f), CrackLight = new(1f, 0.55f, 0.25f);
        static readonly Color32[] StoneChips = { new(200, 187, 166, 255), new(154, 141, 122, 255), new(106, 95, 82, 255), new(67, 58, 50, 255), new(42, 36, 31, 255), new(22, 18, 15, 255) };

        float Sp => Enraged ? Tuning.Icarus.EnragedSpeed : 1f;
        float Tm(float s) => s / Sp;
        Vector2 PlayerAt => Target != null ? ReleaseCanvas.Feet(Target) : Vector2.zero;
        Vector2 PlayerBody => Target != null ? (Vector2)Target.position : Vector2.zero;
        float Gap => Target == null ? float.MaxValue : Mathf.Min((PlayerAt - _at).magnitude, (PlayerBody - _at).magnitude);
        Player.PlayerController Pc => Target != null ? Target.GetComponent<Player.PlayerController>() : null;
        Health PlayerHp => Target != null ? Target.GetComponent<Health>() : null;
        float Hash01(int a, int b = 0) => FireArt.Hash(_seed, a, b);
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        Vector2 Centre => Vector2.up * Tuning.Icarus.StarHeight;

        public static Icarus Spawn(Vector2 at, Transform target, Transform parent, int floor)
        {
            var b = CreateBody<Icarus>("boss.icarus", at, target, parent, floor, 0.55f, Tuning.Icarus.Hp);
            // He walks, rises and dives, so he is drawn where he is (Boss.CreateBody's note).
            var rb = b.GetComponent<Rigidbody2D>();
            if (rb != null) rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            b._at = at;
            b._seed = NextSeed;

            b._figure = TexelPicture.Attach<IcarusFigure>(b.transform, "figure", Vector2.zero, 90, 16, 150, 0, IcarusCanvas.Ppu37);
            DepthSorted.Attach(b.gameObject, 0f, false, b._figure.GetComponent<SpriteRenderer>());
            b._glow = Glow.Attach(b.gameObject, 2.2f, StarLight, 0.55f, 0.05f);

            var room = new GameObject("icarus.room");
            room.transform.SetParent(parent, false);
            b._room = room.transform;
            b._marks = IcarusMarks.Build(b._room, b._seed);
            b._circle = IcarusCircle.Build(b._room);
            b._sky = IcarusSky.Build(b._room, b._seed);
            b._well = IcarusWell.Build(b._room);
            // the shaft of light from above (the user's pick): it stays all fight
            var shaft = new GameObject("icarus.shaft");
            shaft.transform.SetParent(b._room, false);
            shaft.transform.position = Vector3.zero;
            b._shaft = Glow.Attach(shaft, Tuning.Icarus.ShaftRadius, SkyLight, Tuning.Icarus.ShaftIntensity, 0f);
            HideBiomeFixtures();
            b._dark0 = LightField.Darkness;
            b._shade0 = LightField.Shade;

            // Never immune but at the cap: hittable from the first frame.
            b.Health.Immune = false;
            if (b.BodyCollider != null) b.BodyCollider.enabled = true;
            b.HookFan();
            b.StartCoroutine(b.Fight());
            return b;
        }

        /// <summary>
        /// His room has its OWN walls and sky in every biome (the user's call) - IcarusSky draws them over
        /// the biome's boundary. What the biome hung ON its boundary would float in his sky: the sconces and
        /// their flames, the windows with their patches and beams, the forest's canopy shafts. Hidden for
        /// this floor (Scenery rebuilds them on the next).
        /// </summary>
        static void HideBiomeFixtures()
        {
            var scenery = GameObject.Find("Scenery");
            if (scenery == null) return;
            foreach (var t in scenery.GetComponentsInChildren<Transform>(true))
                if (t.name is "sconce" or "flame" or "window" or "patch" or "beam" or "canopy.gap") t.gameObject.SetActive(false);
        }

        /// <summary>The fan's block, on every hit he takes. A delegate, so it is lost to a domain reload -
        /// Update puts it back.</summary>
        void HookFan() { if (Health != null) Health.BlocksHit = FanBlocks; }

        public override void Cease()
        {
            base.Cease();
            _imminent = false; _fanOpen = false;
            if (_dark0 >= 0f) LightField.SetPlace(_dark0, _shade0);
            if (_room != null) Destroy(_room.gameObject);
            _room = null; _marks = null; _circle = null; _sky = null; _well = null;
        }

        /// <summary>His death (the user's words): planted, the light thrown out, melted away - only the
        /// blackened sphere left.</summary>
        public override void DeathPicture()
            => IcarusDeath.At(transform.position, _figure != null ? _figure.Look : default);

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (Health == null || Health.IsDead) return;
            if (Health.BlocksHit == null) HookFan();
            float dt = Time.deltaTime;
            _sinceLunge += dt;
            Cap();

            // The room follows the LOWEST he has been - it never rebuilds (the user's call) - and the
            // enrage's cracks stay once they are there.
            _lowest = Mathf.Min(_lowest, Health.Current / Mathf.Max(1f, Health.Max));
            float decay = Mathf.Clamp01((1f - _lowest - Tuning.Icarus.DecayFrom) / Tuning.Icarus.DecaySpan);
            if (_sky != null) _sky.Decay = decay;
            if (Enraged && _marks != null) _marks.Cracked = true;
            if (Enraged && _cracks == null && _room != null)
            {
                var g = new GameObject("icarus.cracks");
                g.transform.SetParent(_room, false);
                g.transform.position = Vector3.zero;
                _cracks = Glow.Attach(g, 3.5f, CrackLight, 0.45f, 0.04f);
            }
            // the dark lifts as the foundation goes: everything is skylight by the end
            if (_dark0 >= 0f)
            {
                float dark = Mathf.Round(_dark0 * (1f - decay) * 50f) / 50f;
                if (!Mathf.Approximately(dark, _darkSet)) { _darkSet = dark; LightField.SetPlace(dark, _shade0); }
            }
            Debris(decay);
            Well(dt);
            if (_figure != null)
            {
                _figure.Look.Heat = Enraged ? 0.6f : 0f;
                _figure.Look.StarGone = _wellAge >= 0f && _wellAge < Tuning.Icarus.WellSeconds + 0.4f;
            }
        }

        /// <summary>
        /// THE CAP over ANY stretch of CapSeconds (the user's pick): every drop in his health is logged; once
        /// the last CapSeconds hold WindowCap of his max he is immune (collider off too - never hit for
        /// nothing) until the oldest of it ages out. The allowance left sets Health.Floor, so one big hit
        /// stops at the cap; the last stretch may kill.
        /// </summary>
        void Cap()
        {
            _taken ??= new();
            float cur = Health.Current, now = Time.time;
            if (_lastHp < 0f) _lastHp = cur;
            if (cur < _lastHp - 1e-4f) _taken.Add(new Vector2(now, _lastHp - cur));
            _lastHp = cur;
            float recent = 0f;
            for (int i = _taken.Count - 1; i >= 0; i--)
            {
                if (now - _taken[i].x > Tuning.Icarus.CapSeconds) _taken.RemoveAt(i);
                else recent += _taken[i].y;
            }
            float allow = Health.Max * Tuning.Boss.WindowCap - recent;
            if (allow <= 0.01f)
            {
                Health.Immune = true;
                Health.Floor = 0f;
                if (BodyCollider != null) BodyCollider.enabled = false;
                return;
            }
            Health.Immune = false;
            float floor = cur - allow;
            Health.Floor = floor > 0f ? floor : 0f;
            if (BodyCollider != null) BodyCollider.enabled = true;
        }

        void HealOnHit()
        {
            if (Health == null || Health.IsDead) return;
            Health.Heal(Health.Max * Tuning.Icarus.HealFraction);
            _lastHp = Health.Current;   // a heal is not a drop
            Glow.Flash(_at + Vector2.up * 1.1f, 1.4f, StarLight, 0.25f, 0.6f);
        }

        /// <summary>A MECHANIC hit: a share of max health, mitigated through Take - the Boss base's rule.</summary>
        void HurtPlayer(float fraction)
        {
            var hp = PlayerHp;
            if (hp == null || hp.IsDead) return;
            hp.Take(new DamageInfo(hp.Max * fraction, ElementType.Fire, gameObject) { IgnoresHeat = true });
        }

        void MoveTo(Vector2 p)
        {
            _at = Arena.Clamp(p, 0.6f);
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null) rb.MovePosition(_at);
            else transform.position = _at;
        }

        void Face() { if (Target != null && _figure != null) _figure.Look.Dir = PlayerAt.x >= _at.x ? 1 : -1; }

        IEnumerator Wait(int token, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                yield return null;
                if (token != Token) yield break;
            }
        }

        // ------------------------------------------------------------------ the fight

        IEnumerator Fight()
        {
            int token = ++Token;
            yield return new WaitForSeconds(0.8f);
            if (token != Token) yield break;

            while (true)
            {
                CheckEnrage(Tuning.Icarus.EnrageAt);
                if (Gap > Tuning.Icarus.FarRange && _sinceLunge >= Tm(Tuning.Icarus.LungeCooldown))
                {
                    _farMoves++;
                    int every = Band >= Tuning.EnemyBands.IcarusDanceTwiceFromBand ? Tuning.Icarus.DanceEveryTwice : Tuning.Icarus.DanceEvery;
                    if (Band >= Tuning.EnemyBands.IcarusDanceFromBand && _farMoves % every == 0) yield return Dance(token);
                    else yield return Lunge(token, Tuning.Icarus.LungeTellSeconds, Tuning.Icarus.LungeRadius,
                                            1f - Tuning.Icarus.LockBeforeSeconds / Tuning.Icarus.LungeTellSeconds, Tuning.Icarus.RiseSeconds, false);
                    _sinceLunge = 0f;
                }
                else if (Gap > Tuning.Icarus.BlowReach * 0.85f) yield return Approach(token);
                else
                {
                    yield return String(token);
                    if (token != Token) yield break;
                    yield return Pause(token);
                }
                if (token != Token) yield break;
            }
        }

        IEnumerator Approach(int token)
        {
            Phase = Movement.Walking;
            for (float t = 0f; t < 1.2f && Target != null; t += Time.deltaTime)
            {
                var d = PlayerAt - _at;
                if (d.magnitude <= Tuning.Icarus.BlowReach * 0.8f) break;
                if (Gap > Tuning.Icarus.FarRange && _sinceLunge >= Tm(Tuning.Icarus.LungeCooldown)) break;
                MoveTo(_at + d.normalized * Tuning.Icarus.WalkSpeed * Sp * Time.deltaTime);
                Face();
                if (_figure != null) { _figure.Look.Stride = Mathf.Sin(t * 7f * Sp) * 0.6f; Rest(Time.deltaTime); }
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) _figure.Look.Stride = 0f;
        }

        /// <summary>Eases the pose back toward his guard: blade forward and low, the hands at his waist.</summary>
        void Rest(float dt)
        {
            if (_figure == null) return;
            ref var o = ref _figure.Look;
            float k = Mathf.Clamp01(dt * 8f);
            o.Aim = Mathf.Lerp(o.Aim, 0.75f, k); o.Raise = Mathf.Lerp(o.Raise, 0f, k); o.Lean = Mathf.Lerp(o.Lean, 0f, k);
            o.Crouch = Mathf.Lerp(o.Crouch, 0f, k); o.Lift = Mathf.Lerp(o.Lift, 0f, k); o.Smear = 0f;
        }

        // ------------------------------------------------------------------ strings

        static void Shape(int kind, out float a0, out float a1, out float lean)
        {
            switch (kind)
            {
                case Up: a0 = 0.95f; a1 = -1.5f; lean = 1.5f; break;
                case Thrust: a0 = 0.1f; a1 = 0.02f; lean = 4f; break;
                default: a0 = -1.9f; a1 = 0.95f; lean = 1.5f; break;
            }
        }

        IEnumerator String(int token)
        {
            Phase = Movement.String;
            var counts = Tuning.Icarus.StringBlows;
            int n = counts[Mathf.FloorToInt(Hash01(_blows, 1) * counts.Length) % counts.Length]
                  + (Band >= Tuning.EnemyBands.IcarusLongerStringsFromBand ? 1 : 0);
            int midFan = Band >= Tuning.EnemyBands.IcarusFanMidStringFromBand ? 1 + Mathf.FloorToInt(Hash01(_blows, 2) * (n - 1)) : -1;
            for (int i = 0; i < n; i++)
            {
                if (i == midFan)
                {
                    yield return Fan(token, false);
                    if (token != Token) yield break;
                    Phase = Movement.String;
                }
                bool held = Hash01(_blows, 3) < Tuning.Icarus.HeldChance;
                int kind = i % 3 == 2 ? Thrust : (i % 2 == 0 ? Down : Up);
                yield return Blow(token, kind, held ? Tuning.Icarus.HeldWindSeconds : Tuning.Icarus.WindSeconds, held);
                _blows++;
                if (token != Token) yield break;
                if (_result == Result.Clashed || _result == Result.Parried) yield break;
                yield return Wait(token, Tm(Tuning.Icarus.BlowGapSeconds));
                if (token != Token) yield break;
            }
        }

        /// <summary>
        /// One blow: the WIND (held high for a HELD BLOW, so it can't be counted), tracking the player until
        /// the last Tuning.Clash.WindowSeconds - when it is IMMINENT and a swing meeting it clashes - then the
        /// STRIKE, stepping in, and where it lands. A clashed blow is void and he reels; a parried one throws
        /// him back and heals nothing.
        /// </summary>
        IEnumerator Blow(int token, int kind, float windSeconds, bool held)
        {
            Shape(kind, out float a0, out float a1, out float lean);
            float wind = Tm(windSeconds), rest = _figure != null ? _figure.Look.Aim : 0.75f;
            var aim = (PlayerBody - _at).sqrMagnitude > 1e-4f ? (PlayerBody - _at).normalized : Vector2.right;
            _clashed = false;
            _result = Result.Miss;
            Convergence.Audio.Sfx.Play(held ? Convergence.Audio.Sound.Charge : Convergence.Audio.Sound.Swing, _at, held ? 0.6f : 0.4f);
            for (float t = 0f; t < wind; t += Time.deltaTime)
            {
                float u = Smooth(t / wind);
                if (_figure != null)
                {
                    _figure.Look.Aim = Mathf.Lerp(rest, a0, held ? Mathf.Clamp01(u * 2.5f) : u);
                    _figure.Look.Raise = held ? 0.45f * Mathf.Clamp01(u * 2.5f) : 0f;
                    _figure.Look.Lean = -lean * 0.3f * u;
                    _figure.Look.Smear = 0f;
                }
                bool late = t >= wind - Tuning.Clash.WindowSeconds;
                if (!late)
                {
                    Face();
                    var d = PlayerBody - _at;
                    if (d.sqrMagnitude > 1e-4f) aim = d.normalized;
                }
                _imminent = late;
                yield return null;
                if (token != Token) { _imminent = false; yield break; }
                if (_clashed) break;
            }
            if (_clashed)
            {
                _imminent = false;
                _result = Result.Clashed;
                yield return Reel(token, Tm(Tuning.Icarus.ClashedSeconds));
                yield break;
            }

            // the strike
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.SwingHeavy, _at, 0.8f);
            float strike = Tm(Tuning.Icarus.StrikeSeconds);
            var from = _at;
            // he steps in only while there is room: a step into the player would shove them across the floor
            float step = Gap > Tuning.Icarus.BlowReach * 0.6f ? Tuning.Icarus.StepPerBlow : 0f;
            for (float t = 0f; t < strike; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / strike);
                _imminent = true;
                MoveTo(Vector2.Lerp(from, from + aim * step, u));
                if (_figure != null)
                {
                    _figure.Look.Aim = Mathf.Lerp(a0, a1, u);
                    _figure.Look.Lean = lean * u;
                    _figure.Look.Raise = held ? 0.45f * (1f - u) : 0f;
                    _figure.Look.SmearFrom = a0; _figure.Look.SmearTo = _figure.Look.Aim; _figure.Look.Smear = 1f;
                }
                yield return null;
                if (token != Token) { _imminent = false; yield break; }
                if (_clashed) break;
            }
            _imminent = false;
            if (_clashed)
            {
                _result = Result.Clashed;
                if (_figure != null) _figure.Look.Smear = 0f;
                yield return Reel(token, Tm(Tuning.Icarus.ClashedSeconds));
                yield break;
            }
            _result = Land(aim);

            float follow = Tm(Tuning.Icarus.FollowSeconds);
            for (float t = 0f; t < follow; t += Time.deltaTime)
            {
                if (_figure != null) { _figure.Look.SmearTo = a1; _figure.Look.Smear = 1f - t / follow; }
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) _figure.Look.Smear = 0f;
            if (_result == Result.Parried) yield return Reel(token, Tm(Tuning.Icarus.ParriedSeconds));
        }

        Result Land(Vector2 aim)
        {
            var hp = PlayerHp;
            if (hp == null || hp.IsDead) return Result.Miss;
            var d = PlayerBody - _at;
            if (d.magnitude > Tuning.Icarus.BlowReach + Tuning.Icarus.PlayerRadius) return Result.Miss;
            if (Vector2.Angle(aim, d) > Tuning.Icarus.BlowArcDegrees) return Result.Miss;
            var pc = Pc;
            if (pc != null && pc.TryParry(_at)) return Result.Parried;
            if (hp.Immune) return Result.Miss;
            hp.Take(new DamageInfo(Tuning.Icarus.BlowDamage * FloorDifficulty.Damage(Floor), ElementType.Fire, gameObject) { IgnoresHeat = true });
            HealOnHit();
            return Result.Hit;
        }

        /// <summary>Thrown off balance (a clash, a parry): he steps back and the blade drops.</summary>
        IEnumerator Reel(int token, float seconds)
        {
            Phase = Movement.Recovering;
            var away = (_at - PlayerBody).sqrMagnitude > 1e-4f ? (_at - PlayerBody).normalized : Vector2.up;
            var from = _at;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (t < 0.15f) MoveTo(Vector2.Lerp(from, from + away * 0.4f, t / 0.15f));
                if (_figure != null) { _figure.Look.Aim = Mathf.Lerp(_figure.Look.Aim, 1.3f, Time.deltaTime * 10f); _figure.Look.Lean = -1f; _figure.Look.Smear = 0f; _figure.Look.Shake = Mathf.Round(Mathf.Sin(t * 50f) * (1f - t / seconds)); }
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) _figure.Look.Shake = 0f;
        }

        IEnumerator Pause(int token)
        {
            Phase = Movement.Pause;
            float T = Tm(Tuning.Icarus.PauseSeconds), spent = 0f;
            // the fan, in every pause from band 3 (the user's pick: "once a string")
            if (Band >= Tuning.EnemyBands.IcarusFanFromBand)
            {
                yield return Wait(token, Tm(0.15f));
                if (token != Token) yield break;
                float s0 = Time.time;
                yield return Fan(token, false);
                if (token != Token) yield break;
                spent = 0.15f / Sp + (Time.time - s0);
                Phase = Movement.Pause;
            }
            for (float t = spent; t < T; t += Time.deltaTime)
            {
                Rest(Time.deltaTime);
                yield return null;
                if (token != Token) yield break;
            }
        }

        // ------------------------------------------------------------------ the fan

        /// <summary>
        /// THE FAN (the user's design): the wing GLINTS white first (the tell; none after a stun), then a
        /// two-handed sweep up across his body brings the wing across his front - a hit that lands while it
        /// stands is FANNED AWAY (nothing, via Health.BlocksHit), an arrow or a disc fanned back as a bolt.
        /// Fanned (or at the end of a stun, within FanReach): BLOWN BACK, and a HELD BLOW follows, timed to
        /// catch a panicked dodge.
        /// </summary>
        IEnumerator Fan(int token, bool afterStun)
        {
            Phase = Movement.Fanning;
            Face();
            if (!afterStun)
                for (float t = 0f, g = Tm(Tuning.Icarus.GlintSeconds); t < g; t += Time.deltaTime)
                {
                    if (_figure != null) _figure.Look.Glint = Mathf.Sin(Mathf.Clamp01(t / g) * Mathf.PI);
                    yield return null;
                    if (token != Token) yield break;
                }
            if (_figure != null) _figure.Look.Glint = 0f;
            _fanned = false;
            _fanOpen = true;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Swipe, _at, 0.9f);
            float T = Tm(Tuning.Icarus.FanSeconds);
            for (float t = 0f; t < T; t += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Fan = Mathf.Clamp01(t / T);
                yield return null;
                if (token != Token) { _fanOpen = false; yield break; }
            }
            _fanOpen = false;
            if (_figure != null) _figure.Look.Fan = 0f;
            if (afterStun ? Gap <= Tuning.Icarus.FanReach : _fanned)
            {
                BlowBack();
                yield return Blow(token, Down, Tuning.Icarus.FollowWindSeconds, true);
            }
        }

        bool FanBlocks(DamageInfo info)
        {
            if (!_fanOpen) return false;
            _fanned = true;
            if (info.Thrown) FanBack();
            TexelFx.Star(_at + Vector2.up * 0.9f, IcarusCanvas.Metal, 4, 0.08f);
            return true;
        }

        /// <summary>An arrow or a disc fanned back the way it came (the user's pick) - a bolt at the player.</summary>
        void FanBack()
        {
            var hp = PlayerHp;
            if (hp == null || Target == null) return;
            var o = _at + Vector2.up * 0.9f;
            var d = PlayerBody - o;
            EnemyProjectile.Launch(gameObject, Target, hp, o, d.sqrMagnitude > 1e-4f ? d : Vector2.down,
                                   Tuning.Icarus.BlowDamage * FloorDifficulty.Damage(Floor), 0f, Tuning.Icarus.FannedShotSpeed, 3f);
        }

        void BlowBack()
        {
            var pc = Pc;
            if (pc == null) return;
            var d = PlayerBody - _at;
            pc.Dash(d.sqrMagnitude > 1e-4f ? d.normalized : Vector2.down, Tuning.Icarus.BlowBackSpeed, Tuning.Icarus.BlowBackDistance / Tuning.Icarus.BlowBackSpeed);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Push, _at);
        }

        // ------------------------------------------------------------------ the lunge and the dance

        IEnumerator Dance(int token)
        {
            for (int i = 0; i < Tuning.Icarus.DanceTells.Length; i++)
            {
                yield return Lunge(token, Tuning.Icarus.DanceTells[i], Tuning.Icarus.LungeRadius * Tuning.Icarus.DanceRadii[i],
                                   Tuning.Icarus.DanceLockAt[i], i == 0 ? Tuning.Icarus.RiseSeconds : Tuning.Icarus.DanceSpringSeconds, true);
                if (token != Token) yield break;
                if (_landed != Result.Miss) yield break;   // caught or parried ends the dance
            }
        }

        /// <summary>
        /// The LUNGE (the user's picks): he RISES in a high guard, the wing beating; a WIDE circle follows the
        /// player and fills (the clock), LOCKING <paramref name="lockAt"/> of the way through; he DIVES into
        /// it. In the circle as he lands: a parry (any chest ability's first 0.2 s) STUNS him; otherwise
        /// CAUGHT - a dash's i-frames do not pass it. The dance runs three of these, closing in.
        /// </summary>
        IEnumerator Lunge(int token, float tellSeconds, float radius, float lockAt, float riseSeconds, bool dance)
        {
            Phase = dance ? Movement.Dance : Movement.Rising;
            Face();
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Charge, _at);
            float lift0 = _figure != null ? _figure.Look.Lift : 0f;
            for (float t = 0f, r = Tm(riseSeconds); t < r; t += Time.deltaTime)
            {
                float u = Smooth(t / r);
                if (_figure != null) { _figure.Look.Lift = Mathf.Lerp(lift0, Tuning.Icarus.LiftUnits, u); _figure.Look.Raise = u; _figure.Look.Aim = Mathf.Lerp(_figure.Look.Aim, 0.1f, u); _figure.Look.Smear = 0f; }
                yield return null;
                if (token != Token) yield break;
            }

            var c = PlayerAt;
            float T = Tm(tellSeconds);
            if (_circle != null) { _circle.On = true; _circle.Radius = radius; _circle.Locked = false; _circle.Fill = 0f; }
            for (float t = 0f; t < T; t += Time.deltaTime)
            {
                bool locked = t >= T * lockAt;
                if (!locked) c = Arena.Clamp(PlayerAt, 0.6f);
                if (_circle != null) { _circle.transform.position = c; _circle.Fill = t / T; _circle.Locked = locked; }
                if (_figure != null) { _figure.Look.Dir = c.x >= _at.x ? 1 : -1; _figure.Look.Lift = Tuning.Icarus.LiftUnits + Mathf.Sin(t * 9f) * 0.05f; }
                yield return null;
                if (token != Token) { if (_circle != null) _circle.On = false; yield break; }
            }

            // the dive
            Phase = dance ? Movement.Dance : Movement.Diving;
            var from = _at;
            float D = Tm(Tuning.Icarus.DiveSeconds);
            for (float t = 0f; t < D; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / D);
                MoveTo(Vector2.Lerp(from, c, u));
                if (_figure != null) { _figure.Look.Lift = Tuning.Icarus.LiftUnits * (1f - u); _figure.Look.Aim = Mathf.Lerp(0.1f, 1.2f, u); _figure.Look.Raise = 1f - u; }
                yield return null;
                if (token != Token) { if (_circle != null) _circle.On = false; yield break; }
            }
            MoveTo(c);
            if (_circle != null) _circle.On = false;
            if (_figure != null) { _figure.Look.Lift = 0f; _figure.Look.Crouch = 2f; }
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Slam, c);
            TexelFx.Rubble(c, StoneChips, 0.2f, 6, 0.6f);

            var hp = PlayerHp;
            bool inside = hp != null && !hp.IsDead && (PlayerAt - c).magnitude <= radius + Tuning.Icarus.PlayerRadius;
            if (!inside)
            {
                _landed = Result.Miss;
                yield return Wait(token, Tm(Tuning.Icarus.MissRecoverSeconds));
                yield break;
            }
            var pc = Pc;
            if (pc != null && pc.TryParry(c))
            {
                _landed = Result.Parried;
                yield return Stun(token);
                yield break;
            }
            _landed = Result.Caught;
            yield return Caught(token);
        }

        /// <summary>
        /// CAUGHT (the user's pick, "impaled and lifted" - Sephiroth and Cloud): run through and held (the
        /// player petrified), the blade raised, twisted - CaughtBlows mechanic hits of CaughtFraction between
        /// them, each healing him - then flung off.
        /// </summary>
        IEnumerator Caught(int token)
        {
            Phase = Movement.Caught;
            if (Target != null) StatusEffects.Get(Target.gameObject).ApplyPetrify(Tuning.Icarus.CaughtSeconds + 0.1f);
            Face();
            int dir = _figure != null ? _figure.Look.Dir : 1;
            MoveTo(PlayerAt - Vector2.right * dir * 0.75f);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.HitHeavy, _at);
            float T = Tuning.Icarus.CaughtSeconds;
            int n = Tuning.Icarus.CaughtBlows, done = 0;
            for (float t = 0f; t < T; t += Time.deltaTime)
            {
                float u = t / T;
                if (_figure != null)
                {
                    _figure.Look.Aim = Mathf.Lerp(0.05f, -1.2f, Smooth(u * 3f));
                    _figure.Look.Raise = Smooth(u * 3f) * 0.6f;
                    _figure.Look.Shake = Mathf.Round(Mathf.Sin(t * 40f) * 1.2f);
                }
                int due = Mathf.Min(n, 1 + Mathf.FloorToInt(u * n));
                while (done < due)
                {
                    HurtPlayer(Tuning.Icarus.CaughtFraction / n);
                    HealOnHit();
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.HitHeavy, _at, 0.7f);
                    done++;
                }
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) { _figure.Look.Shake = 0f; _figure.Look.Aim = 1.1f; }
            var pc = Pc;
            if (pc != null && pc.Health != null && !pc.Health.IsDead)
            {
                var d = PlayerBody - _at;
                pc.Dash(d.sqrMagnitude > 1e-4f ? d.normalized : Vector2.right * dir, Tuning.Icarus.ThrowSpeed, Tuning.Icarus.ThrowDistance / Tuning.Icarus.ThrowSpeed);
            }
            yield return Wait(token, Tm(0.5f));
        }

        /// <summary>A parried dive (the user's rule): STUNNED - vulnerable for a short time, no extra damage -
        /// and every stun ENDS IN THE FAN.</summary>
        IEnumerator Stun(int token)
        {
            Phase = Movement.Stunned;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Vulnerable, _at);
            if (_figure != null) { _figure.Look.Dazed = true; _figure.Look.Crouch = 3f; _figure.Look.Aim = 1.4f; }
            var away = (_at - PlayerBody).sqrMagnitude > 1e-4f ? (_at - PlayerBody).normalized : Vector2.up;
            var from = _at;
            for (float t = 0f; t < Tuning.Icarus.StunSeconds; t += Time.deltaTime)
            {
                if (t < 0.25f) MoveTo(Vector2.Lerp(from, from + away * 0.6f, t / 0.25f));
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) { _figure.Look.Dazed = false; _figure.Look.Crouch = 0f; }
            yield return Fan(token, true);
        }

        // ------------------------------------------------------------------ the well

        /// <summary>
        /// The WELL, on its own clock (the user's picks: the blade's star; every stone costs the same): the
        /// star rises off his guard to hang over the middle of the room; for WellSeconds it pulls the player
        /// in (just under walking speed - a dash gains ground; never faster than that, enraged or not) and
        /// drags stones torn from the walls to it. He keeps fighting while it pulls.
        /// </summary>
        void Well(float dt)
        {
            _wellClock += dt * Sp;
            if (_wellAge < 0f)
            {
                if (_well != null) _well.Open = 0f;
                if (_wellClock < Tuning.Icarus.WellEvery || Phase == Movement.Caught) return;
                _wellAge = 0f; _wellClock = 0f; _stoneNext = 0f;
                _starFrom = _figure != null ? (Vector2)transform.position + _figureStar : _at + Vector2.up * 0.6f;
                Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Toll, Centre);
                Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Pull, Centre, 0.8f);
            }
            _wellAge += dt * Sp;
            float T = Tuning.Icarus.WellSeconds;
            float rise = Smooth(_wellAge / Tuning.Icarus.StarRiseSeconds);
            if (_well != null)
            {
                _well.StarAt = Vector2.Lerp(_starFrom, Centre, rise);
                _well.Open = Mathf.Clamp01(_wellAge / Tuning.Icarus.StarRiseSeconds) * (1f - Mathf.Clamp01((_wellAge - T) / 0.4f));
            }
            if (_wellAge < T)
            {
                var pc = Pc;
                if (pc != null && _wellAge > Tuning.Icarus.StarRiseSeconds)
                {
                    var d = Centre - PlayerBody;
                    float speed = Mathf.Min((Band >= Tuning.EnemyBands.IcarusHarderWellFromBand ? Tuning.Icarus.PullSpeedHard : Tuning.Icarus.PullSpeed) * Sp, Tuning.Icarus.PullSpeedHard);
                    if (d.magnitude > 0.3f) pc.PullFor(d.normalized * speed * Mathf.Clamp01((_wellAge - Tuning.Icarus.StarRiseSeconds) / 0.6f));
                }
                _stoneNext -= dt * Sp;
                while (_stoneNext <= 0f && _well != null)
                {
                    _stoneNext += Tuning.Icarus.StoneEvery;
                    _stones++;
                    _well.Stones ??= new();
                    _well.Stones.Add(new IcarusWell.Stone
                    {
                        From = WallPoint(_stones), Born = _well.Clock,
                        Flight = Tuning.Icarus.StoneFlightSeconds / Sp * (0.85f + Hash01(_stones, 9) * 0.3f),
                        Size = Hash01(_stones, 8) > 0.6f ? 2 : 1,
                    });
                }
            }
            else if (_wellAge > T + 0.4f && (_well == null || _well.Stones == null || _well.Stones.Count == 0))
            {
                _wellAge = -1f;
                TexelFx.Rubble(Centre, StoneChips, 0.4f, 12, 1f);
                Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Rubble, Centre);
            }
            if (_well != null)
            {
                if (_well.Settle() > 0 && Hash01(_stones, 11) > 0.7f) Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Chip, Centre, 0.5f);
                _stoneGrace -= dt;
                if (_stoneGrace <= 0f && Target != null && _well.Touches(PlayerBody, Tuning.Icarus.PlayerRadius, _well.StarAt))
                {
                    HurtPlayer(Tuning.Icarus.StoneFraction);
                    _stoneGrace = Tuning.Icarus.StoneGraceSeconds;
                }
            }
        }

        Vector2 _figureStar => _figure != null ? _figure.StarWorld : Vector2.up * 0.6f;

        /// <summary>Where a stone is torn from: the walls (mostly the north wall's standing stretches, never
        /// its open sky), a little beyond them.</summary>
        Vector2 WallPoint(int i)
        {
            var h = Arena.HalfExtents;
            float side = Hash01(i, 5), along = Hash01(i, 6) * 2f - 1f, out_ = 0.3f + Hash01(i, 7) * 1.6f;
            if (side < 0.5f)
            {
                float x = along * (h.x - 0.5f);
                if (Mathf.Abs(x) < Tuning.Icarus.OpenHalf) x = Mathf.Sign(x == 0f ? 1f : x) * (Tuning.Icarus.OpenHalf + Mathf.Abs(x) * 0.5f);
                return new Vector2(Mathf.Clamp(x, -h.x, h.x), h.y + out_);
            }
            if (side < 0.7f) return new Vector2(-h.x - out_, along * h.y);
            if (side < 0.9f) return new Vector2(h.x + out_, along * h.y);
            return new Vector2(along * h.x, -h.y - out_);
        }

        /// <summary>The tower's pieces falling past in the sky beyond, as the room comes apart.</summary>
        void Debris(float decay)
        {
            if (_room == null || decay <= 0.3f) return;
            int want = Mathf.RoundToInt((decay - 0.3f) / 0.7f * 14f);
            while (_debris < want) { IcarusDebris.Build(_room, _seed, _debris); _debris++; }
        }
    }
}
