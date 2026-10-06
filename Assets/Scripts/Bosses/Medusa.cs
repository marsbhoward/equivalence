using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Enemies;

namespace Convergence.Bosses
{
    /// <summary>
    /// Medusa: a gaze in the middle of a ring of pillars. She NEVER attacks the player - the
    /// whole fight is one rule, be behind a pillar when she looks, and everything else she does
    /// exists to make keeping it hard. See <see cref="Tuning.Medusa"/> for every number.
    ///
    ///     BREAK      a pillar shakes and shatters (and, deep enough, a PUSH or PULL lands on the
    ///                same beat)
    ///     GAZE       the arena fills red everywhere her gaze would reach - the pillars' shadows
    ///                stay clear - and then she looks. Anyone in the open takes half their max
    ///                health and turns to STONE for the window
    ///     OPEN       she is vulnerable, capped by Boss.WindowCap like every boss. A statue
    ///                misses it, and her adds can still hurt it (through stone skin)
    ///     DEBRIS     falling circles; once half the ring is gone, some of them bring pillars back
    ///
    /// THE RING IS THE AUTHORITY. Whether her gaze reaches a point is <see cref="PillarRing.Shadowed"/>,
    /// and the red is painted from that same call, so what the player sees is what decides.
    ///
    /// Getting caught costs the WINDOW, not just health - the fight gets longer. That is a
    /// punishment no gear can buy off, and it never ends a run on its own: stone skin
    /// (Tuning.Medusa.StoneSkinBase) is sized so a full-health statue survives its adds.
    ///
    /// Driven from one coroutine, like the Cantor (see its header). Everything she puts in the
    /// room - ring, red, debris, adds - lives under one object Cease destroys.
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

        Transform _room;
        PillarRing _ring;
        GazeShade _shade;
        SpriteRenderer _halo, _hair, _body, _eye;

        // Non-readonly and re-created if null - see CLAUDE.md, Domain reload traps.
        List<EnemyController> _adds = new();

        bool _pushing;
        float _hairSpin = 20f;

        static readonly Color Scale = new(0.34f, 0.52f, 0.40f);
        static readonly Color Serpent = new(0.42f, 0.70f, 0.38f);
        static readonly Color SerpentEnraged = new(0.80f, 0.38f, 0.30f);
        static readonly Color Open = new(1f, 0.92f, 0.55f);
        static readonly Color EyeDim = new(0.70f, 0.62f, 0.24f);
        static readonly Color EyeLit = new(1f, 0.95f, 0.55f);
        static readonly Color StoneDust = new(0.74f, 0.72f, 0.66f);
        static readonly Color Warn = new(1f, 0.55f, 0.25f);

        public static Medusa Spawn(Vector2 at, Transform target, Transform parent, int floor)
        {
            var b = CreateBody<Medusa>("boss.medusa", at, target, parent, floor, 0.6f, Tuning.Boss.Hp);
            var t = b.transform;

            b._halo = Quad(t, "halo", 3.0f, new Color(Serpent.r, Serpent.g, Serpent.b, 0.25f),
                           SortingOrders.Fx - 3, Spr.Glow);
            b._hair = Quad(t, "hair", 1.9f, Serpent, SortingOrders.Enemy, Spr.SegmentedRing(9));
            b._body = Quad(t, "body", 1.2f, Scale, SortingOrders.Enemy + 1, Spr.Circle);
            b._eye = Quad(t, "eye", 0.42f, EyeDim, SortingOrders.Enemy + 2, Spr.Circle);

            var room = new GameObject("medusa.room");
            room.transform.SetParent(parent, false);
            b._room = room.transform;
            b._ring = PillarRing.Build(b._room, at);
            b._shade = GazeShade.Build(b._room);

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
            _shade = null;
        }

        void OnSlain()
        {
            if (_ring == null) return;
            for (int i = 0; i < _ring.Count; i++)
                if (_ring.Standing(i)) Spr.Flash(_ring.SlotPosition(i), 1.4f, StoneDust, 0.6f);
            _ring.ShatterAll();
        }

        void Update()
        {
            if (_hair != null) _hair.transform.Rotate(0f, 0f, _hairSpin * Time.deltaTime);
        }

        // ------------------------------------------------------------------ the fight

        IEnumerator Fight()
        {
            int token = ++Token;
            SetOpen(false);

            // The room settles before anything happens - the player arrives, in cover, and sees
            // the ring before being asked anything of it.
            yield return new WaitForSeconds(0.8f);
            if (token != Token) yield break;

            while (true)
            {
                CheckEnrage(Tuning.Medusa.EnrageAt);
                bool opening = Cycle == 0;

                if (Floor >= Tuning.Medusa.AddsFromFloor) SummonAdds();

                // The opening has no break: the player has never seen the rule, and taking their
                // cover before the first look would teach "the ring lies" instead.
                if (!opening)
                {
                    yield return Break(token);
                    if (token != Token) yield break;
                }

                float tell = opening ? Tuning.Medusa.OpeningTelegraphSeconds
                           : Enraged ? Tuning.Medusa.TelegraphSecondsEnraged
                           : Tuning.Medusa.TelegraphSeconds;
                yield return Gaze(token, tell);
                if (token != Token) yield break;

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

        IEnumerator Break(int token)
        {
            Phase = Movement.Stirring;

            // The push or pull lands on the same beat as the break, so the player re-reads the
            // ring and their own position at once - and with the red about to start.
            if (Floor >= Tuning.Medusa.PushPullFromFloor && Target != null)
                StartCoroutine(PushPull(token, pull: Random.value < 0.5f));

            int pick = RandomStanding();
            var col = _ring != null ? _ring.At(pick) : null;
            float warn = Tuning.Medusa.BreakWarnSeconds;
            float t = 0f, cue = 0f;
            while (t < warn)
            {
                // The player may break it themselves while it shakes - then there is nothing left
                // to break, and the warning has said all it needed to.
                if (col == null || col.Broken) break;

                t += Time.deltaTime;
                cue -= Time.deltaTime;
                if (cue <= 0f)
                {
                    // Faster as it goes, so how long is left reads at a glance.
                    cue = Mathf.Lerp(0.28f, 0.09f, t / warn);
                    Spr.Pulse(col.transform, PillarRing.PillarRadius * 2.4f,
                              new Color(Warn.r, Warn.g, Warn.b, 0.55f), 0.22f, true, 0.25f);
                }
                yield return null;
                if (token != Token) yield break;
            }

            if (col != null && !col.Broken)
            {
                Spr.Flash(col.transform.position, 1.5f, StoneDust, 0.5f);
                col.Break();
            }

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
        /// Telegraphed by a ring rushing out from her (push) or in toward her (pull) for the
        /// wind-up, so the direction is readable before it lands. Neither does damage - a zero
        /// hit would still flash, wear armour and fire every Damaged listener.
        /// </summary>
        IEnumerator PushPull(int token, bool pull)
        {
            _pushing = true;
            var tint = pull ? new Color(0.70f, 0.45f, 1f) : new Color(0.55f, 1f, 0.60f);
            var wave = Quad(transform, pull ? "pull" : "push", 1f, tint, SortingOrders.Fx - 2, Spr.ThinRing);

            float t = 0f, windup = Tuning.Medusa.PushPullWindupSeconds;
            while (t < windup)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / windup);
                // Two passes of the ring in the wind-up - one reads as a pulse, two as a direction.
                float pass = (k * 2f) % 1f;
                float size = pull ? Mathf.Lerp(12f, 1.2f, pass) : Mathf.Lerp(1.2f, 12f, pass);
                if (wave != null)
                {
                    wave.transform.localScale = Vector3.one * size;
                    wave.color = new Color(tint.r, tint.g, tint.b, 0.75f * (1f - Mathf.Abs(pass - 0.5f)));
                }
                yield return null;
                if (token != Token) { if (wave != null) Destroy(wave.gameObject); _pushing = false; yield break; }
            }
            if (wave != null) Destroy(wave.gameObject);

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
                    Spr.Pulse(Target, 1.1f, tint, 0.3f);
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
            _shade.Invalidate();
            _hairSpin = 60f;

            float t = 0f;
            while (t < tell)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / tell);

                // Repaints only if a pillar broke since the last paint - the player can break
                // their own cover mid-telegraph, and the picture must say so the frame it happens.
                _shade.Paint(_ring, transform.position);

                // Ramps rather than switching on, and beats faster as it fills: the player is
                // reading a CLOCK, not a light.
                float beat = Mathf.PingPong(Time.time * (2f + k * 7f), 1f) * 0.06f;
                _shade.SetStrength(Mathf.Lerp(0.06f, 0.40f, k * k) + beat);
                if (_eye != null) _eye.color = Color.Lerp(EyeDim, EyeLit, k);
                _hairSpin = Mathf.Lerp(60f, 260f, k);

                yield return null;
                if (token != Token) yield break;
            }

            // She LOOKS - held for GazeHoldSeconds, so a dash's i-frames cannot step through it.
            Spr.Flash(transform.position, 2.6f, EyeLit, 0.4f);
            if (_eye != null) _eye.color = Color.white;
            bool caught = false;
            float g = 0f;
            while (g < Tuning.Medusa.GazeHoldSeconds)
            {
                g += Time.deltaTime;
                _shade.Paint(_ring, transform.position);
                _shade.SetStrength(0.62f);
                if (!caught) caught = TryCatch();
                yield return null;
                if (token != Token) yield break;
            }

            for (float f = 0f; f < 0.3f; f += Time.deltaTime)
            {
                _shade.SetStrength(Mathf.Lerp(0.62f, 0f, f / 0.3f));
                yield return null;
                if (token != Token) yield break;
            }
            _shade.SetStrength(0f);
            if (_eye != null) _eye.color = EyeDim;
            _hairSpin = 20f;
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

            hp.Take(new DamageInfo(hp.Max * Tuning.Medusa.GazeFraction, ElementType.Earth, gameObject));
            if (hp.IsDead) return true;

            float window = Enraged ? Tuning.Medusa.WindowSecondsEnraged : Tuning.Medusa.WindowSeconds;
            // Both depth curves cancelled: on stone, adds hit like floor-1 enemies (see StoneSkinBase).
            float skin = Tuning.Medusa.StoneSkinBase * FloorDifficulty.AttackInterval(Floor)
                       / Mathf.Max(1f, FloorDifficulty.Damage(Floor));
            StatusEffects.Get(Target.gameObject).ApplyStone(window + 2f, skin);
            Spr.Flash(Target.position, 1.3f, StoneDust, 0.5f);
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
            SetOpen(true);

            float seconds = Enraged ? Tuning.Medusa.WindowSecondsEnraged : Tuning.Medusa.WindowSeconds;
            float t = 0f;
            while (t < seconds)
            {
                if (WindowSpent) break;
                t += Time.deltaTime;
                // The eye pulses faster as the window closes, as the Cantor's core does.
                float k = t / seconds;
                float beat = Mathf.PingPong(Time.time * (3f + k * 6f), 1f);
                if (_eye != null) _eye.color = Color.Lerp(Open, Color.white, beat);
                yield return null;
                if (token != Token) yield break;
            }

            CloseWindow();
            SetOpen(false);

            // The stone covered the window and nothing past it: the statue is flesh again the
            // moment she can no longer be hit, whatever the timer said.
            EndPlayerStone();
        }

        void SetOpen(bool open)
        {
            var hair = Enraged ? SerpentEnraged : Serpent;
            if (_body != null) _body.color = open ? Open : Scale;
            if (_hair != null) _hair.color = open ? Color.Lerp(hair, Open, 0.5f) : hair;
            if (_halo != null)
            {
                var c = open ? Open : hair;
                _halo.color = new Color(c.r, c.g, c.b, open ? 0.5f : 0.25f);
            }
            if (_eye != null && !open) _eye.color = EyeDim;
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

            int loose = Floor >= Tuning.Medusa.AddsFromFloor ? Tuning.Medusa.DebrisCountDeep
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

        IEnumerator Fall(int token, Vector2 at, float radius, int slot)
        {
            if (_room == null) yield break;
            var go = new GameObject(slot >= 0 ? "medusa.pillarfall" : "medusa.debris");
            go.transform.SetParent(_room, false);
            go.transform.position = at;

            // On the ground, over the red's layer, under every body: the circle is what the
            // player reads to step out of, so a body standing in it has to stay visible on top.
            var shadow = Quad(go.transform, "shadow", 0.2f, new Color(0f, 0f, 0f, 0.15f),
                              SortingOrders.PitBase + 8, Spr.Circle);
            var rim = Quad(go.transform, "rim", radius * 2f, new Color(Warn.r, Warn.g, Warn.b, 0.5f),
                           SortingOrders.PitBase + 9, Spr.ThinRing);

            float warn = Tuning.Medusa.DebrisWarnSeconds;
            float t = 0f;
            while (t < warn)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / warn);
                // The shadow GROWS as the stone comes down - it fills the circle on impact.
                if (shadow != null)
                {
                    shadow.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, radius * 2f, k);
                    shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.15f, 0.55f, k));
                }
                if (rim != null)
                    rim.color = new Color(Warn.r, Warn.g, Warn.b, 0.45f + 0.4f * Mathf.PingPong(t * 6f, 1f));
                yield return null;
                if (token != Token) { if (go != null) Destroy(go); yield break; }
            }
            if (go != null) Destroy(go);

            Spr.Flash(at, radius * 1.1f, StoneDust, 0.35f);

            // One test, at impact - the Cantor's eruption rule: standing in it costs one hit, not
            // a hit per frame. A mechanic hit, a share of max health, mitigated through Take.
            if (Target != null && ((Vector2)Target.position - at).sqrMagnitude <= (radius + 0.25f) * (radius + 0.25f))
            {
                var hp = Target.GetComponent<Health>();
                if (hp != null && !hp.IsDead)
                    hp.Take(new DamageInfo(hp.Max * Tuning.Medusa.DebrisFraction, ElementType.Earth, gameObject));
            }

            // Debris never breaks a pillar - it only builds them.
            if (slot >= 0 && _ring != null && _ring.Refill(slot) != null)
                ShoveOut(at, PillarRing.PillarRadius + 0.45f);
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
        /// Her only pressure. Summoned at the top of each cycle from the room's edge, never more
        /// than MaxAdds alive. Owned by her rather than the wave: not in GameBootstrap's _alive
        /// (she holds the floor herself), destroyed with her, and they pay a kill but never a
        /// Rift Box - a boss that summons forever would otherwise be a box farm.
        /// </summary>
        void SummonAdds()
        {
            if (Target == null) return;
            _adds ??= new List<EnemyController>();
            _adds.RemoveAll(a => a == null);
            int n = Mathf.Min(Tuning.Medusa.AddsPerCycle, Tuning.Medusa.MaxAdds - _adds.Count);

            for (int i = 0; i < n; i++)
            {
                var kind = Floor >= Tuning.Medusa.RangedAddsFromFloor && Random.value < 0.4f
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
                        Spr.Flash(dead.transform.position, 0.9f, new Color(1f, 0.9f, 0.7f), 0.3f);
                        Destroy(dead.gameObject);
                    };
                _adds.Add(e);
                Spr.Flash(pos, 0.9f, Serpent, 0.45f);
            }
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
