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
    /// Medusa: a gaze in the middle of a ring of pillars. She NEVER attacks the player directly -
    /// the whole fight is one rule, be behind a pillar when she looks, and everything else she does
    /// exists to make keeping it hard. See <see cref="Tuning.Medusa"/> for every number.
    ///
    ///     BREAK      cracks climb a pillar and it shatters (deep enough, her TAIL heaves on the same
    ///                beat: a push or pull)
    ///     GAZE       the red fills outward from her everywhere her gaze would reach - the pillars'
    ///                shadows stay clear - while the scales of justice tip (the clock) and her hand
    ///                grips the blindfold; she tears it away and LOOKS. Anyone in the open takes half
    ///                their max health and turns to STONE for the window
    ///     OPEN       she drops the scales and slumps, open, capped by Boss.WindowCap like every
    ///                boss. A statue misses it, and her adds can still hurt it (through stone skin)
    ///     DEBRIS     stones fall near the player; once half the ring is gone, some are pillars
    ///                coming back
    ///
    /// The user's design (group 5, 2026-10-26): a lamia Justice - see <see cref="MedusaCanvas"/>.
    /// Enraged, her THIRD EYE opens, and before each gaze it turns the player's pillar to GLASS:
    /// it casts no shadow (<see cref="PillarRing.GlassMask"/>). By band she breaks the player's own
    /// pillar (1), spits venom through the tell (2), drops stones in the tell too (3), and looks
    /// twice a cycle (4) - <see cref="Tuning.EnemyBands"/>.
    ///
    /// THE RING IS THE AUTHORITY. Whether her gaze reaches a point is <see cref="PillarRing.Shadowed"/>,
    /// and the red is painted from that same call (<see cref="MedusaGaze"/>), so what the player
    /// sees is what decides.
    ///
    /// Getting caught costs the WINDOW, not just health - the fight gets longer. That is a
    /// punishment no gear can buy off, and it never ends a run on its own: stone skin
    /// (Tuning.Medusa.StoneSkinBase) is sized so a full-health statue survives its adds.
    ///
    /// Driven from one coroutine, like the Wreathe (see its header). Everything she puts in the
    /// room - ring, red, debris, venom, adds - lives under one object Cease destroys.
    /// </summary>
    public class Medusa : Boss
    {
        public enum Movement { Stirring, Gaze, Open, Debris }
        public Movement Phase { get; private set; } = Movement.Stirring;

        public override string DisplayName => "MEDUSA";
        public override string PhaseLabel => Phase switch
        {
            Movement.Stirring => "STIRRING",
            Movement.Gaze => "HER GAZE",
            Movement.Open => "OPEN",
            _ => "FALLING STONE",
        };

        /// <summary>Cycles completed. The first is the opening: no break, a longer red.</summary>
        public int Cycle { get; private set; }

        /// <summary>The ring, for tests and the HUD. Null once she is gone.</summary>
        public PillarRing Ring => _ring;

        [SerializeField] Transform _room;
        [SerializeField] PillarRing _ring;
        [SerializeField] MedusaGaze _gaze;
        [SerializeField] MedusaFigure _figure;
        [SerializeField] Glow _glow;

        // Non-readonly and re-created if null - see CLAUDE.md, Domain reload traps.
        List<EnemyController> _adds = new();

        bool _pushing;
        bool _caughtThisCycle;

        static readonly Color HerLight = new(0.78f, 0.92f, 0.82f);
        static readonly Color OpenLight = new(1f, 0.86f, 0.45f);
        static readonly Color EyeLight = new(1f, 1f, 1f);

        int Band => RunFormat.EnemyBand(Floor);

        /// <summary>Where her eyes are, in the world - the third eye's sight line and the look's flash.</summary>
        Vector2 Eyes => (Vector2)transform.position + Vector2.up * ((MedusaCanvas.Base - 8) / TexelFx.Body);

        public static Medusa Spawn(Vector2 at, Transform target, Transform parent, int floor)
        {
            var b = CreateBody<Medusa>("boss.medusa", at, target, parent, floor, 0.6f, Tuning.Boss.Hp);

            b._figure = TexelPicture.Attach<MedusaFigure>(b.transform, "figure", Vector2.zero, 38, 10, 62, 0);
            DepthSorted.Attach(b.gameObject, 0f, false, b._figure.GetComponent<SpriteRenderer>());
            b._glow = Glow.Attach(b.gameObject, 3.0f, HerLight, 0.75f, 0.05f);

            var room = new GameObject("medusa.room");
            room.transform.SetParent(parent, false);
            b._room = room.transform;
            b._ring = PillarRing.Build(b._room, at);
            b._gaze = MedusaGaze.Build(b._room, b._ring, at);

            // Her temple comes down with her. Registered before GameBootstrap's own Died handler
            // (which Ceases and destroys her), so the pillars shatter visibly first.
            b.Health.Died += _ => b.OnSlain();

            b.StartCoroutine(b.Fight());
            return b;
        }

        public override void Cease()
        {
            base.Cease();
            EndPlayerStone();

            if (_adds != null)
            {
                foreach (var a in _adds)
                    if (a != null) Destroy(a.gameObject);
                _adds.Clear();
            }
            if (_room != null) Destroy(_room.gameObject);
            _room = null;
            _ring = null;
            _gaze = null;
        }

        void OnSlain()
        {
            if (_ring == null) return;
            _ring.GlassMask = 0;
            _ring.ShatterAll();
        }

        /// <summary>Her own stone (the user's pick): it climbs her from the tail and she breaks into
        /// a heap that lies there until the floor ends.</summary>
        public override void DeathPicture()
            => MedusaDeath.At(transform.position, _figure != null ? _figure.Look : default);

        void Update()
        {
            if (_figure == null) return;
            // The third eye opens at the enrage and stays open.
            if (Enraged && _figure.Look.ThirdOpen < 1f)
                _figure.Look.ThirdOpen = Mathf.Min(1f, _figure.Look.ThirdOpen + Time.deltaTime / 0.8f);
        }

        // ------------------------------------------------------------------ the fight

        IEnumerator Fight()
        {
            int token = ++Token;
            SetOpen(0f);

            // The room settles before anything happens - the player arrives, in cover, and sees
            // the ring before being asked anything of it.
            yield return new WaitForSeconds(0.8f);
            if (token != Token) yield break;

            while (true)
            {
                CheckEnrage(Tuning.Medusa.EnrageAt);
                bool opening = Cycle == 0;
                _caughtThisCycle = false;

                if (RunFormat.Depth(Floor) >= Tuning.Medusa.AddsFromFloor) SummonAdds(token);

                // The opening has no break: the player has never seen the rule, and taking their
                // cover before the first look would teach "the ring lies" instead.
                if (!opening)
                {
                    yield return Break(token, Tuning.Medusa.BreakWarnSeconds, Band >= Tuning.EnemyBands.MedusaBreaksYoursFromBand, true);
                    if (token != Token) yield break;
                }

                float tell = opening ? Tuning.Medusa.OpeningTelegraphSeconds
                           : Enraged ? Tuning.Medusa.TelegraphSecondsEnraged
                           : Tuning.Medusa.TelegraphSeconds;
                yield return Gaze(token, tell);
                if (token != Token) yield break;

                // Band 4: she breaks your pillar and LOOKS AGAIN before the window opens.
                if (!opening && Band >= Tuning.EnemyBands.MedusaLooksTwiceFromBand)
                {
                    yield return Break(token, Tuning.Medusa.SecondBreakWarnSeconds, true, false);
                    if (token != Token) yield break;
                    yield return Gaze(token, Tuning.Medusa.SecondTellSeconds);
                    if (token != Token) yield break;
                }

                yield return Window(token);
                if (token != Token) yield break;

                yield return Debris(token);
                if (token != Token) yield break;

                yield return new WaitForSeconds(Tuning.Medusa.BeatSeconds);
                if (token != Token) yield break;

                Cycle++;
            }
        }

        // ------------------------------------------------------------------ BREAK

        /// <summary>Cracks climb a pillar for <paramref name="warn"/> (the clock), it shudders at
        /// the end, and it shatters. <paramref name="yours"/>: the pillar hiding the player (band 1),
        /// else any. <paramref name="mayPush"/>: deep enough, her tail heaves a push or pull on the
        /// same beat.</summary>
        IEnumerator Break(int token, float warn, bool yours, bool mayPush)
        {
            Phase = Movement.Stirring;

            // The push or pull lands on the same beat as the break, so the player re-reads the
            // ring and their own position at once - and with the red about to start.
            if (mayPush && RunFormat.Depth(Floor) >= Tuning.Medusa.PushPullFromFloor && Target != null)
                StartCoroutine(PushPull(token, pull: Random.value < 0.5f));

            int pick = yours && _ring != null && Target != null ? _ring.CoverOf(transform.position, ReleaseCanvas.Feet(Target)) : RandomStanding();
            var col = _ring != null ? _ring.At(pick) : null;
            var cracks = PillarCracks.On(col);
            float t = 0f, cue = 0f;
            while (t < warn)
            {
                // The player may break it themselves while it cracks - then there is nothing left
                // to break, and the warning has said all it needed to.
                if (col == null || col.Broken) break;

                t += Time.deltaTime;
                float k = t / warn;
                if (cracks != null) cracks.Climb = k;
                // It shudders through the last third, by a texel.
                col.Shake(k > 0.66f ? (Mathf.FloorToInt(Time.time * 24f) % 2 == 0 ? 1 : -1) : 0);
                cue -= Time.deltaTime;
                if (cue <= 0f)
                {
                    // Faster as it goes, so how long is left reads at a glance.
                    cue = Mathf.Lerp(0.28f, 0.09f, k);
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Chip, col.transform.position, 0.6f);
                }
                yield return null;
                if (token != Token) yield break;
            }

            if (col != null && !col.Broken) col.Break();   // its rubble is the place's own (group 4)

            while (_pushing)
            {
                yield return null;
                if (token != Token) yield break;
            }
            yield return new WaitForSeconds(Tuning.Medusa.BeatSeconds);
        }

        int RandomStanding()
        {
            if (_ring == null) return -1;
            int n = _ring.StandingCount;
            if (n == 0) return -1;
            int k = Random.Range(0, n);
            for (int i = 0; i < _ring.Count; i++)
                if (_ring.Standing(i) && k-- == 0) return i;
            return -1;
        }

        /// <summary>
        /// A PUSH shoves the player straight away from her - walls and pillars stop it as they
        /// stop anything. A PULL drags them toward her to <see cref="Tuning.Medusa.PullToRadius"/>,
        /// THROUGH the ring (pillar collisions are suspended for the drag): a pull a pillar could
        /// stop would never move the player it is aimed at, who is by definition behind one.
        ///
        /// Her TAIL does it (the user's call): the coil heaves twice through the wind-up and a ring
        /// of dust rolls out across the floor (push) or is drawn in to her (pull). Neither does
        /// damage - a zero hit would still flash, wear armour and fire every Damaged listener.
        /// </summary>
        IEnumerator PushPull(int token, bool pull)
        {
            _pushing = true;
            Convergence.Audio.Sfx.Play(pull ? Convergence.Audio.Sound.Pull : Convergence.Audio.Sound.Push, transform.position);
            float windup = Tuning.Medusa.PushPullWindupSeconds;
            if (_room != null) DustRing.At(_room, transform.position, windup, pull);

            float t = 0f;
            while (t < windup)
            {
                t += Time.deltaTime;
                if (_figure != null) _figure.Look.Loosen = Mathf.Abs(Mathf.Sin(t / windup * Mathf.PI * 2f)) * 0.6f;
                yield return null;
                if (token != Token) { _pushing = false; yield break; }
            }
            if (_figure != null) _figure.Look.Loosen = 0f;

            var pc = Target != null ? Target.GetComponent<Player.PlayerController>() : null;
            if (pc != null && pc.Health != null && !pc.Health.IsDead)
            {
                Vector2 me = transform.position;
                Vector2 d = (Vector2)Target.position - me;
                float dist = d.magnitude;
                var dir = dist > 0.01f ? d / dist : Vector2.down;
                float speed = Tuning.Medusa.PushPullSpeed;

                float travel = pull ? dist - Tuning.Medusa.PullToRadius : Tuning.Medusa.PushDistance;
                if (travel > 0.1f)
                {
                    float seconds = travel / speed;
                    if (pull) IgnorePillars(true);
                    pc.Dash(pull ? -dir : dir, speed, seconds);
                    TexelFx.Puff(ReleaseCanvas.Feet(Target), TexelFx.RubbleOf(true), 10, 7f, 0.4f, TexelFx.Ground);
                    yield return new WaitForSeconds(seconds + 0.06f);
                    if (pull) IgnorePillars(false);
                }
            }
            _pushing = false;
        }

        void IgnorePillars(bool ignore)
        {
            if (_ring == null || Target == null) return;
            var mine = Target.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < _ring.Count; i++)
            {
                var c = _ring.At(i);
                if (c == null) continue;
                var cc = c.GetComponent<Collider2D>();
                if (cc == null) continue;
                foreach (var m in mine)
                    if (m != null && !m.isTrigger) Physics2D.IgnoreCollision(m, cc, ignore);
            }
        }

        // ------------------------------------------------------------------ GAZE

        IEnumerator Gaze(int token, float tell)
        {
            Phase = Movement.Gaze;

            // Enraged, her third eye fixes the pillar the player hides behind: it turns to glass
            // and casts no shadow for this look.
            if (Enraged && _ring != null && Target != null)
            {
                int slot = _ring.CoverOf(transform.position, ReleaseCanvas.Feet(Target));
                if (slot >= 0)
                {
                    _ring.GlassMask = 1 << slot;
                    if (_room != null) SightLine.Draw(_room, Eyes + Vector2.up * (3f / TexelFx.Body), _ring.SlotPosition(slot) + Vector2.up * 0.8f);
                    StartCoroutine(GlassIn(token));
                }
            }

            // Band 2: venom lobbed over the pillars at the player through the tell.
            if (Band >= Tuning.EnemyBands.MedusaVenomFromBand) StartCoroutine(Venom(token, tell));
            // Band 3: stones fall near the player during the tell too.
            if (Band >= Tuning.EnemyBands.MedusaTellDebrisFromBand) StartCoroutine(TellDebris(token, tell));

            float t = 0f, rattle = 0f;
            while (t < tell)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / tell);

                // Her hair RATTLES, faster as the gaze fills - the clock, heard off screen too.
                rattle -= Time.deltaTime;
                if (rattle <= 0f)
                {
                    rattle = Mathf.Lerp(0.55f, 0.12f, k);
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Rattle, transform.position, Mathf.Lerp(0.5f, 1f, k));
                }

                // The red fills outward (the clock); the scales tip (the clock again, on her); her
                // hand grips the blindfold at once and holds it there (the user's pick).
                if (_gaze != null) _gaze.Fill = k;
                if (_figure != null)
                {
                    _figure.Look.Tilt = k;
                    _figure.Look.HandUp = Mathf.Clamp01(t / 0.3f);
                    _figure.Look.FoldDown = t >= tell - 0.12f ? 1f : 0f;
                }

                yield return null;
                if (token != Token) yield break;
            }

            // She LOOKS - held for GazeHoldSeconds, so a dash's i-frames cannot step through it.
            if (_gaze != null) _gaze.Look();
            Glow.Flash(Eyes, 3.4f, EyeLight, 0.45f);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.GazeLook, transform.position);
            if (_figure != null) { _figure.Look.FoldDown = 1f; _figure.Look.EyeLit = 1f; }
            float g = 0f;
            while (g < Tuning.Medusa.GazeHoldSeconds)
            {
                g += Time.deltaTime;
                if (_figure != null) _figure.Look.Tears = g / 0.5f;
                if (!_caughtThisCycle) _caughtThisCycle = TryCatch();
                yield return null;
                if (token != Token) yield break;
            }

            // The blindfold back up, the scales level, the glass stone again.
            if (_ring != null) { _ring.GlassMask = 0; _ring.ShowGlass(0f); }
            for (float f = 0f; f < 0.4f; f += Time.deltaTime)
            {
                float u = f / 0.4f;
                if (_figure != null)
                {
                    _figure.Look.EyeLit = 0f;
                    _figure.Look.Tears = Mathf.Min(1f, (g + f) / 0.5f);
                    _figure.Look.Tilt = 1f - u;
                    _figure.Look.HandUp = 1f - u;
                    _figure.Look.FoldDown = u > 0.5f ? 0f : 1f;
                }
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) { _figure.Look.Tilt = 0f; _figure.Look.HandUp = 0f; _figure.Look.FoldDown = 0f; _figure.Look.Tears = 0f; }
        }

        IEnumerator GlassIn(int token)
        {
            for (float t = 0f; t < Tuning.Medusa.GlassSeconds; t += Time.deltaTime)
            {
                if (token != Token || _ring == null) yield break;
                _ring.ShowGlass(t / Tuning.Medusa.GlassSeconds);
                yield return null;
            }
            if (_ring != null && _ring.GlassMask != 0) _ring.ShowGlass(1f);
        }

        IEnumerator Venom(int token, float tell)
        {
            int n = Tuning.Medusa.VenomCount;
            float flight = Tuning.Medusa.VenomFlightSeconds;
            // Spread through the tell, the last landing before the look.
            float span = Mathf.Max(0.2f, tell - flight - 0.25f), gap = span / Mathf.Max(1, n);
            yield return new WaitForSeconds(0.25f);
            for (int i = 0; i < n; i++)
            {
                if (token != Token || Target == null || _room == null) yield break;
                var at = ReleaseCanvas.Feet(Target) + Random.insideUnitCircle * 0.5f;
                float dmg = Tuning.Medusa.VenomDamage * FloorDifficulty.Damage(Floor);
                VenomShot.Lob(_room, Eyes + Vector2.up * (6f / TexelFx.Body), at, Target, dmg);
                StartCoroutine(HissPulse(token, 0.25f));
                yield return new WaitForSeconds(gap);
            }
        }

        IEnumerator TellDebris(int token, float tell)
        {
            float warn = Tuning.Medusa.DebrisWarnSeconds;
            int n = Tuning.Medusa.TellDebrisCount;
            float span = Mathf.Max(0.1f, tell - warn - 0.1f);
            for (int i = 0; i < n; i++)
            {
                yield return new WaitForSeconds(i == 0 ? span * 0.15f : span * 0.6f / Mathf.Max(1, n - 1));
                if (token != Token) yield break;
                StartCoroutine(Fall(token, LoosePoint(), Tuning.Medusa.DebrisRadius, -1));
            }
        }

        IEnumerator HissPulse(int token, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (token != Token || _figure == null) yield break;
                _figure.Look.Hiss = Mathf.Sin(t / seconds * Mathf.PI);
                yield return null;
            }
            if (_figure != null) _figure.Look.Hiss = 0f;
        }

        /// <summary>
        /// The verdict, one frame of the hold. In the open and not immune: half of max health as a
        /// MECHANIC hit (mitigated, so Brace and the ledger count; a Barrier facing her blocks the
        /// damage), and STONE for the coming window whatever the hit did - stone is not a hit to
        /// block. A hit that kills leaves no statue to make.
        /// </summary>
        bool TryCatch()
        {
            if (Target == null || _ring == null) return false;
            var hp = Target.GetComponent<Health>();
            if (hp == null || hp.IsDead || hp.Immune) return false;
            if (_ring.Shadowed(transform.position, Target.position)) return false;

            hp.Take(new DamageInfo(hp.Max * Tuning.Medusa.GazeFraction, ElementType.Earth, gameObject) { IgnoresHeat = true });
            if (hp.IsDead) return true;

            float window = Enraged ? Tuning.Medusa.WindowSecondsEnraged : Tuning.Medusa.WindowSeconds;
            // Both depth curves cancelled: on stone, adds hit like floor-1 enemies (see StoneSkinBase).
            // Long enough to cover a second look too (band 4); the window's end ends it regardless.
            float skin = Tuning.Medusa.StoneSkinBase * FloorDifficulty.AttackInterval(Floor)
                       / Mathf.Max(1f, FloorDifficulty.Damage(Floor));
            StatusEffects.Get(Target.gameObject).ApplyStone(window + 2f + Tuning.Medusa.SecondTellSeconds + Tuning.Medusa.SecondBreakWarnSeconds + 1f, skin);
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Petrify, Target.position);
            TexelFx.Dust(ReleaseCanvas.Feet(Target), MedusaCanvas.Stone, 0.6f);
            return true;
        }

        void EndPlayerStone()
        {
            if (Target == null) return;
            var s = Target.GetComponent<StatusEffects>();
            if (s != null) s.EndStone();
        }

        // ------------------------------------------------------------------ OPEN

        IEnumerator Window(int token)
        {
            Phase = Movement.Open;
            OpenWindow();       // the per-window cap - see Boss.OpenWindow

            float seconds = Enraged ? Tuning.Medusa.WindowSecondsEnraged : Tuning.Medusa.WindowSeconds;
            float t = 0f;
            while (t < seconds)
            {
                if (WindowSpent) break;
                t += Time.deltaTime;
                SetOpen(Mathf.Clamp01(t / 0.5f));
                // The gem at her girdle beats faster as the window closes, as the Wreathe's heart does.
                float k = t / seconds;
                if (_figure != null) _figure.Look.Gem = Mathf.PingPong(Time.time * (3f + k * 6f), 1f) > 0.5f ? 2 : 1;
                yield return null;
                if (token != Token) yield break;
            }

            CloseWindow();
            if (_figure != null) _figure.Look.Gem = 0;
            // She takes the scales back up.
            for (float f = 0f; f < 0.4f; f += Time.deltaTime)
            {
                SetOpen(1f - f / 0.4f);
                yield return null;
                if (token != Token) yield break;
            }
            SetOpen(0f);

            // The stone covered the window and nothing past it: the statue is flesh again the
            // moment she can no longer be hit, whatever the timer said.
            EndPlayerStone();
        }

        /// <summary>0 shut .. 1 open (the user's pick): the scales fall from her hand to the floor,
        /// she slumps forward over them with her eyes shut; her light turns gold.</summary>
        void SetOpen(float k)
        {
            if (_figure != null)
            {
                _figure.Look.DropScales = k;
                _figure.Look.Bow = k;
                _figure.Look.Sink = Mathf.RoundToInt(k * 2f);
                _figure.Look.Limp = k * 0.6f;
                _figure.Look.Shut = k > 0.5f;
            }
            if (_glow != null) _glow.Color = Color.Lerp(HerLight, OpenLight, k);
        }

        // ------------------------------------------------------------------ DEBRIS

        IEnumerator Debris(int token)
        {
            Phase = Movement.Debris;
            if (_ring == null) yield break;

            // Once half the ring is gone the empty slots come back - each as a falling pillar
            // with its own warned circle, so the player sees exactly where cover is returning,
            // and the warning that clears the spot is the same one that keeps a pillar from
            // landing on them.
            var jobs = new List<int>();   // a slot index, or -1 for loose debris
            int need = Mathf.CeilToInt(_ring.Count * Tuning.Medusa.RebuildAtMissingFraction);
            if (_ring.MissingCount >= need)
                for (int i = 0; i < _ring.Count; i++)
                    if (!_ring.Standing(i)) jobs.Add(i);

            int loose = RunFormat.Depth(Floor) >= Tuning.Medusa.AddsFromFloor ? Tuning.Medusa.DebrisCountDeep
                                                                               : Tuning.Medusa.DebrisCount;
            for (int i = 0; i < loose; i++) jobs.Add(-1);
            for (int i = jobs.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (jobs[i], jobs[j]) = (jobs[j], jobs[i]);
            }

            float gap = Tuning.Medusa.DebrisSpreadSeconds / Mathf.Max(1, jobs.Count);
            foreach (int slot in jobs)
            {
                Vector2 at = slot >= 0 ? _ring.SlotPosition(slot) : LoosePoint();
                float r = slot >= 0 ? PillarRing.PillarRadius + 0.35f : Tuning.Medusa.DebrisRadius;
                StartCoroutine(Fall(token, at, r, slot));
                yield return new WaitForSeconds(gap);
                if (token != Token) yield break;
            }

            yield return new WaitForSeconds(Tuning.Medusa.DebrisWarnSeconds + 0.2f);
        }

        /// <summary>
        /// Where a loose stone falls: anywhere within a few units of the player, inside the room.
        /// Random, but near the fight - stones raining on the far corners are scenery, not a move.
        /// </summary>
        Vector2 LoosePoint()
        {
            Vector2 around = Target != null ? (Vector2)Target.position : Vector2.zero;
            return Arena.Clamp(around + Random.insideUnitCircle * 3.5f, 1f);
        }

        /// <summary>A warned stone (the user's pick: a hard red edge filling inward, the stone itself
        /// seen coming down the whole time), one test at impact.</summary>
        IEnumerator Fall(int token, Vector2 at, float radius, int slot)
        {
            if (_room == null) yield break;
            float warn = Tuning.Medusa.DebrisWarnSeconds;
            DebrisWarning.At(_room, at, radius, warn);
            FallingStone.At(_room, at, warn, slot >= 0);

            yield return new WaitForSeconds(warn);
            if (token != Token) yield break;

            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Rubble, at);
            if (slot < 0) TexelFx.Rubble(at, TexelFx.RubbleOf(true), 0.3f, 8, 0.8f);
            FightMarks.Crack(at, radius * 0.7f);

            // One test, at impact - the Wreathe's beat rule: standing in it costs one hit, not
            // a hit per frame. A mechanic hit, a share of max health, mitigated through Take.
            if (Target != null && ((Vector2)Target.position - at).sqrMagnitude <= (radius + 0.25f) * (radius + 0.25f))
            {
                var hp = Target.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                    hp.Take(new DamageInfo(hp.Max * Tuning.Medusa.DebrisFraction, ElementType.Earth, gameObject) { IgnoresHeat = true });
            }

            // Debris never breaks a pillar - it only builds them.
            if (slot >= 0 && _ring != null && _ring.Refill(slot) != null)
            {
                TexelFx.Dust(at, TexelFx.RubbleOf(true), radius + 0.3f);
                ShoveOut(at, PillarRing.PillarRadius + 0.45f);
            }
        }

        /// <summary>A pillar that lands where the player stands leaves them at its edge, not
        /// inside it waiting for the physics solver to decide which way to spit them out.</summary>
        void ShoveOut(Vector2 centre, float clear)
        {
            if (Target == null) return;
            Vector2 p = Target.position;
            var d = p - centre;
            if (d.sqrMagnitude >= clear * clear) return;
            var dir = d.sqrMagnitude > 1e-4f ? d.normalized : (centre - (Vector2)transform.position).normalized;
            var to = centre + dir * clear;
            var rb = Target.GetComponent<Rigidbody2D>();
            if (rb != null) rb.position = to;
            Target.position = to;
            Physics2D.SyncTransforms();
        }

        // ------------------------------------------------------------------ ADDS

        /// <summary>
        /// Her only pressure. At the top of each cycle from the room's edge, never more than
        /// MaxAdds alive - and they arrive as STATUES (the user's pick): grey and still until her
        /// crown hisses, then they crack and the stone falls away. Owned by her rather than the
        /// wave: not in GameBootstrap's _alive (she holds the floor herself), destroyed with her,
        /// and they pay a kill but never a Rift Box - a boss that summons forever would otherwise
        /// be a box farm.
        /// </summary>
        void SummonAdds(int token)
        {
            if (Target == null) return;
            _adds ??= new List<EnemyController>();
            _adds.RemoveAll(a => a == null);
            int n = Mathf.Min(Tuning.Medusa.AddsPerCycle, Tuning.Medusa.MaxAdds - _adds.Count);
            if (n <= 0) return;

            var statues = new List<MedusaStatue>();
            for (int i = 0; i < n; i++)
            {
                var kind = RunFormat.Depth(Floor) >= Tuning.Medusa.RangedAddsFromFloor && Random.value < 0.4f
                         ? EnemyKind.Ranged : EnemyKind.Chaser;
                var pos = EdgePoint();
                var e = EnemyFactory.Spawn(pos, kind, Floor, Target, transform.parent);
                var target = Target;
                var h = e.GetComponent<Health>();
                if (h != null)
                    h.Died += dead =>
                    {
                        var pc = target != null ? target.GetComponent<Player.PlayerController>() : null;
                        if (pc != null) pc.RegisterKill();
                        TexelFx.Rubble(dead.transform.position, MedusaCanvas.Stone, 0.6f, 8, 0.7f);
                        Destroy(dead.gameObject);
                    };
                _adds.Add(e);
                statues.Add(MedusaStatue.Hold(e));
                TexelFx.Dust(pos, MedusaCanvas.Stone, 0.7f);
            }
            StartCoroutine(Wake(token, statues));
        }

        /// <summary>Her crown hisses; the statues crack and wake.</summary>
        IEnumerator Wake(int token, List<MedusaStatue> statues)
        {
            yield return new WaitForSeconds(Tuning.Medusa.StatueHoldSeconds);
            if (token != Token) yield break;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Hiss, transform.position);
            float crack = Tuning.Medusa.StatueCrackSeconds;
            for (float t = 0f; t < crack; t += Time.deltaTime)
            {
                if (token != Token) yield break;
                if (_figure != null) _figure.Look.Hiss = Mathf.Max(_figure.Look.Hiss, Mathf.Clamp01(t / 0.12f));
                foreach (var s in statues) if (s != null) s.Crack(t / crack);
                yield return null;
            }
            foreach (var s in statues) if (s != null) s.Wake();
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Hiss = 1f - t / 0.25f;
                yield return null;
            }
            if (_figure != null) _figure.Look.Hiss = 0f;
        }

        /// <summary>A point on the room's edge, the farthest of three from the player, so an add
        /// arrives as pressure rather than spawning on top of them.</summary>
        Vector2 EdgePoint()
        {
            var half = Arena.HalfExtents - Vector2.one * 1.2f;
            Vector2 best = Vector2.zero;
            float bestD = -1f;
            Vector2 p = Target != null ? (Vector2)Target.position : Vector2.zero;
            for (int i = 0; i < 3; i++)
            {
                Vector2 c = Random.Range(0, 4) switch
                {
                    0 => new Vector2(Random.Range(-half.x, half.x), half.y),
                    1 => new Vector2(Random.Range(-half.x, half.x), -half.y),
                    2 => new Vector2(half.x, Random.Range(-half.y, half.y)),
                    _ => new Vector2(-half.x, Random.Range(-half.y, half.y)),
                };
                float d = (c - p).sqrMagnitude;
                if (d > bestD) { bestD = d; best = c; }
            }
            return best;
        }
    }
}
