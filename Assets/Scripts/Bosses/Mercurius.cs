using System.Collections;
using UnityEngine;
using Convergence.Art.Lighting;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Enemies;

namespace Convergence.Bosses
{
    /// <summary>
    /// Mercurius: a bullet hell of water holding up bronze (the user's design, 2026-10-28 - BOSSES.md).
    /// Every number is in <see cref="Tuning.Mercurius"/>.
    ///
    /// It keeps to ONE of five SEQUENCES per encounter (drawn from the run's seed), each its own
    /// projectile shape and its own order of limbs - opening with the limb its shape comes from. A PHASE:
    ///
    ///     GATHER    it lifts the limb; too close and the REPULSE slides the player out, no damage
    ///     PATTERN   the limb unwinds into a swirl and the pattern pours out of it. Never immune - the
    ///               player trades extra hits against being in place for the next pattern. Each sequence
    ///               has one kind of OPENING, tighter with each limb
    ///     RECOVERY  its bronze pieces circle back to the stump; hit it for KeepLostFraction and the limb
    ///               stays gone, or it regrows - and the next pattern is DENSER - and it moves on anyway
    ///
    /// THE CAP per phase (the Boss base's window cap, it is never otherwise immune): once a phase has
    /// taken WindowCap it turns immune, staggers, SINKS and SURFACES far from the player, inside the
    /// middle of the room (no pattern from a corner - the user's "no cheese"). Its LAST WINDOW, below
    /// SkullAt, is always the skull alone, running the sequence's last pattern until it dies.
    ///
    /// Bands: a TIDE to parry in every pattern, a shorter recovery, switching sequence at a regroup,
    /// two tides. Its projectiles are a <see cref="Volley"/>: nothing cuts them.
    /// </summary>
    public class Mercurius : Boss
    {
        public const int ClawL = 0, ClawR = 1, Wings = 2, Halo = 3, Armour = 4, Tail = 5, LimbCount = 6;
        public enum Movement { Rising, Gathering, Pattern, Recovering, Regrouping, Skull }
        public enum Sequence { Beads, Shards, Hoops, Crescents, Bones }

        /// <summary>Each sequence opens with the limb its shape comes from (the user's pick): beads from the
        /// wings, shards from the armour, hoops from the halo, crescents from the claws, bones from the tail.</summary>
        static int[] Order(Sequence s) => s switch
        {
            Sequence.Beads => new[] { Wings, ClawL, ClawR, Halo },
            Sequence.Shards => new[] { Armour, ClawR, Tail, Wings },
            Sequence.Hoops => new[] { Halo, Armour, ClawL, Tail },
            Sequence.Crescents => new[] { ClawL, ClawR, Wings, Armour },
            _ => new[] { Tail, Halo, Armour, ClawR },
        };

        public Movement Phase { get; private set; } = Movement.Rising;
        public Sequence Current => _seq;
        public int LostMask => _lost;
        public int Dense => _dense;

        public override string DisplayName => "MERCURIUS";
        public override string PhaseLabel => Phase switch
        {
            Movement.Recovering => "RECOVERING",
            Movement.Regrouping => "SINKING",
            Movement.Skull => "THE SKULL",
            _ => _seq.ToString().ToUpperInvariant(),
        };
        /// <summary>Hittable all fight; the bar reads OPEN in the recovery, where hits decide the limb.</summary>
        public override bool ReadsShut => Phase != Movement.Recovering;

        [SerializeField] Sequence _seq;
        [SerializeField] int _seed, _lost, _dense, _spent, _cursor;
        [SerializeField] Transform _room;
        [SerializeField] Volley _volley;
        [SerializeField] MercuriusFloor _floorPic;
        [SerializeField] MercuriusFigure _figure;
        [SerializeField] Glow _glow;
        [SerializeField] Vector2 _at;
        [SerializeField] float _grace;
        [SerializeField] bool _capped;
        // the tide: its tell counts down, then its front runs out from the eye
        [SerializeField] float _tell = -1f, _tideR = -1f;
        [SerializeField] bool _tideSpent;
        [SerializeField] Vector2 _tideEye;

        static readonly Color ItsLight = new(0.55f, 0.92f, 0.86f);

        Vector2 Origin => _at + Vector2.up * Tuning.Mercurius.OriginUp;
        Vector2 PlayerAt => Target != null ? ReleaseCanvas.Feet(Target) : Vector2.zero;
        Vector2 PlayerBody => Target != null ? (Vector2)Target.position : Vector2.zero;

        /// <summary>DEV: the sequence the next one keeps to (0 beads .. 4 bones); -1 draws it from the seed.</summary>
        public static int DevSequence = -1;

        public static Mercurius Spawn(Vector2 at, Transform target, Transform parent, int floor)
        {
            var b = CreateBody<Mercurius>("boss.mercurius", at, target, parent, floor, 0.9f, Tuning.Mercurius.Hp);
            b._at = at;
            b._seed = NextSeed;
            b._seq = DevSequence >= 0 ? (Sequence)(DevSequence % 5) : (Sequence)(int)(FireArt.Hash(b._seed, 41) * 5f % 5f);

            b._figure = TexelPicture.Attach<MercuriusFigure>(b.transform, "figure", Vector2.zero, 64, 12, 100, 0);
            DepthSorted.Attach(b.gameObject, 0f, false, b._figure.GetComponent<SpriteRenderer>());
            b._glow = Glow.Attach(b.gameObject, 2.8f, ItsLight, 0.6f, 0.04f);

            var room = new GameObject("mercurius.room");
            room.transform.SetParent(parent, false);
            b._room = room.transform;
            b._volley = Volley.Build(b._room);
            b._floorPic = MercuriusFloor.Build(b._room);

            // Never immune but at the cap: hittable from the first frame.
            b.Health.Immune = false;
            if (b.BodyCollider != null) b.BodyCollider.enabled = true;
            b.StartCoroutine(b.Fight());
            return b;
        }

        public override void Cease()
        {
            base.Cease();
            if (_room != null) Destroy(_room.gameObject);
            _room = null; _volley = null; _floorPic = null;
        }

        /// <summary>Its death (the user's pick): hollow armour - the water drains, the bronze topples.</summary>
        public override void DeathPicture()
            => MercuriusDeath.At(transform.position, _figure != null ? _figure.Look : default);

        float Hash01(int a, int b = 0) => FireArt.Hash(_seed, a, b);

        // ------------------------------------------------------------------ every frame: hits, the tide, the look

        void Update()
        {
            float dt = Time.deltaTime;
            if (_figure != null)
            {
                _figure.Look.Lost = _lost;
                _figure.Look.Shape = (int)_seq;
                _figure.Look.Immune = Health != null && Health.Immune;
            }
            if (Target == null || Health == null || Health.IsDead) return;
            var hp = Target.GetComponent<Health>();
            if (hp == null || hp.IsDead) return;

            // The shots: chip damage, one at a time - a stream never shreds. A dash's i-frames pass
            // through them like any bolt (only the tide asks for the parry).
            _grace -= dt;
            if (_grace <= 0f && !hp.Immune && _volley != null && _volley.Touches(PlayerBody, Tuning.Mercurius.PlayerRadius, true))
            {
                hp.Take(new DamageInfo(Tuning.Mercurius.ShotDamage * FloorDifficulty.Damage(Floor), ElementType.Water, gameObject) { IgnoresHeat = true });
                _grace = Tuning.Mercurius.HurtGraceSeconds;
            }

            // The tide: its tell, then a ring of water out to the walls. Thick enough that it takes longer
            // to pass than a dash's i-frames: only the parry answers it (the user's rule).
            if (_tell >= 0f)
            {
                _tell += dt / Tuning.Mercurius.TideTellSeconds;
                if (_tell >= 1f) { _tell = -1f; _tideR = 0f; _tideSpent = false; Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.ReleaseWater, _tideEye); }
            }
            if (_tideR >= 0f)
            {
                _tideR += Tuning.Mercurius.TideSpeed * dt;
                float d = (PlayerAt - _tideEye).magnitude;
                if (!_tideSpent && d <= _tideR && d >= _tideR - Tuning.Mercurius.TideThickness)
                {
                    var pc = Target.GetComponent<Player.PlayerController>();
                    if (pc != null && pc.TryParry(_tideEye)) _tideSpent = true;
                    else if (!hp.Immune) { HurtPlayer(Tuning.Mercurius.TideFraction); _tideSpent = true; }
                }
                if (_tideR - Tuning.Mercurius.TideThickness > Arena.HalfExtents.magnitude * 2f) _tideR = -1f;
            }
            if (_floorPic != null) { _floorPic.TideEye = _tideEye; _floorPic.TideR = _tideR; _floorPic.Tell = _tell; }
        }

        void StartTide()
        {
            _tideEye = _at;
            _tell = 0f;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Bubbling, _at);
        }

        // ------------------------------------------------------------------ the fight

        IEnumerator Fight()
        {
            int token = ++Token;
            yield return new WaitForSeconds(0.8f);
            if (token != Token) yield break;

            while (true)
            {
                if (Health.Current <= Health.Max * Tuning.Mercurius.SkullAt + 0.01f) break;
                int limb = NextLimb();
                if (limb < 0) break;

                OpenPhase();
                _capped = false;
                yield return Gather(token, limb);
                if (token != Token) yield break;
                if (!_capped) yield return Pattern(token, limb, Mathf.Min(_spent, 3));
                if (token != Token) yield break;
                _lost |= 1 << limb;   // spent: it grows back only from the recovery
                if (!_capped) yield return Recovery(token, limb);
                if (token != Token) yield break;
                _spent++;
                if (_capped)
                {
                    yield return Regroup(token);
                    if (token != Token) yield break;
                }
            }
            yield return Skull(token);
        }

        /// <summary>The sequence's limbs in order, then the two it doesn't open with; the next still on it.</summary>
        int NextLimb()
        {
            var order = Order(_seq);
            var queue = new int[LimbCount];
            int n = 0;
            foreach (var l in order) queue[n++] = l;
            for (int l = 0; l < LimbCount; l++) if (System.Array.IndexOf(order, l) < 0) queue[n++] = l;
            for (int k = 0; k < LimbCount; k++)
            {
                int l = queue[(_cursor + k) % LimbCount];
                if ((_lost & (1 << l)) != 0) continue;
                _cursor = (_cursor + k + 1) % LimbCount;
                return l;
            }
            return -1;
        }

        /// <summary>
        /// THE CAP, per phase: it is never immune, so the Boss base's window cap is spent across a whole
        /// phase. It also stops at SkullAt - reaching it is a cap like any other, and the skull follows.
        /// </summary>
        void OpenPhase()
        {
            float floor = Mathf.Max(Health.Current - Health.Max * Tuning.Boss.WindowCap, Health.Max * Tuning.Mercurius.SkullAt);
            Health.Floor = floor > 0f ? floor : 0f;
            Health.Immune = false;
            if (BodyCollider != null) BodyCollider.enabled = true;
        }

        IEnumerator Gather(int token, int limb)
        {
            Phase = Movement.Gathering;
            if (_figure != null) { _figure.Look.Raised = limb; _figure.Look.Sag = 0f; }
            // Too close as it begins: a ring of water slides the player out. No damage, and always before
            // the first projectile, so it never shoves anyone into one.
            if (Target != null)
            {
                var d = PlayerAt - _at;
                float r = Tuning.Mercurius.RepulseRadius;
                if (d.magnitude < r)
                {
                    var pc = Target.GetComponent<Player.PlayerController>();
                    var dir = d.sqrMagnitude > 1e-4f ? d.normalized : Vector2.down;
                    if (pc != null) pc.Dash(dir, Tuning.Mercurius.RepulseSpeed, (r + 0.3f - d.magnitude) / Tuning.Mercurius.RepulseSpeed);
                    if (_floorPic != null) { _floorPic.RepulseAt = _at; _floorPic.RepulseAge = 0f; }
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Push, _at);
                }
            }
            for (float t = 0f; t < Tuning.Mercurius.GatherSeconds; t += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Raise = Mathf.SmoothStep(0f, 1f, t / Tuning.Mercurius.GatherSeconds);
                yield return null;
                if (token != Token) yield break;
                if (WindowSpent) { _capped = true; break; }
            }
        }

        // ------------------------------------------------------------------ the pattern

        /// <summary>Where the limb fires from: an arm from its side, the wings and halo from above, the
        /// armour from its body, the tail low to its side.</summary>
        static Vector2 LimbOffset(int limb) => limb switch
        {
            ClawL => new Vector2(-0.7f, 0f),
            ClawR => new Vector2(0.7f, 0f),
            Wings => new Vector2(0f, 0.5f),
            Halo => new Vector2(0f, 0.8f),
            Tail => new Vector2(0.6f, -0.5f),
            _ => Vector2.zero,
        };

        IEnumerator Pattern(int token, int limb, int idx, bool skull = false)
        {
            if (!skull) Phase = Movement.Pattern;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.ReleaseWater, _at);
            var o = Origin + (skull ? Vector2.zero : LimbOffset(limb));
            int dense = skull ? 0 : _dense;
            float dir = Hash01(_spent, 3) > 0.5f ? 1f : -1f, theta = Hash01(_spent, 4) * Mathf.PI * 2f;
            float next = 0f, nextDense = 0f;
            int count = 0;

            bool tides = Band >= Tuning.EnemyBands.MercuriusTideFromBand;
            bool two = Band >= Tuning.EnemyBands.MercuriusTwoTidesFromBand;
            float tideA = two ? Tuning.Mercurius.TideFirstAt : Tuning.Mercurius.TideAt, tideB = two ? Tuning.Mercurius.TideSecondAt : -1f;

            float T = Tuning.Mercurius.PatternSeconds;
            for (float t = 0f; t < T; t += Time.deltaTime)
            {
                if (_figure != null && !skull)
                {
                    _figure.Look.Spinning = limb;
                    _figure.Look.Spin = Mathf.Clamp01(t / 0.5f);
                    _figure.Look.Raise = 1f - Mathf.Clamp01(t / 0.5f);
                }
                if (tides && ((t >= tideA - Tuning.Mercurius.TideTellSeconds && tideA > 0f) || (tideB > 0f && t >= tideB - Tuning.Mercurius.TideTellSeconds)))
                {
                    if (t >= tideA - Tuning.Mercurius.TideTellSeconds && tideA > 0f) { tideA = -1f; StartTide(); }
                    else if (tideB > 0f && t >= tideB - Tuning.Mercurius.TideTellSeconds) { tideB = -1f; StartTide(); }
                }

                switch (_seq)
                {
                    case Sequence.Beads:
                    {
                        // A turning spiral: RIDE THE GAP between two arms. More arms with each limb.
                        int arms = Tuning.Mercurius.BeadArms + idx / 2 + dense;
                        float turn = (Tuning.Mercurius.BeadTurn + idx * Tuning.Mercurius.BeadTurnPerLimb) * dir;
                        while (t >= next)
                        {
                            float a0 = theta + turn * next;
                            for (int j = 0; j < arms; j++) Fire(o, a0 + j * Mathf.PI * 2f / arms, Tuning.Mercurius.BeadSpeed, ShotKind.Bead);
                            next += Tuning.Mercurius.BeadEvery;
                        }
                        break;
                    }
                    case Sequence.Shards:
                    {
                        // A ring flies out, sticks in the floor, flies back: THE RETURN. The next ring is
                        // turned half a gap, so the player must shift.
                        while (t >= next && t < T - 2.4f)
                        {
                            int n = Tuning.Mercurius.ShardRing + idx * Tuning.Mercurius.ShardRingPerLimb + dense * Tuning.Mercurius.ShardDense;
                            float off = theta + (count % 2) * Mathf.PI / n;
                            float stick = (Tuning.Mercurius.ShardReach - Tuning.Mercurius.SwirlRadius) / Tuning.Mercurius.ShardSpeed;
                            for (int j = 0; j < n; j++)
                            {
                                float a = off + j * Mathf.PI * 2f / n;
                                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                                _volley?.Add(new Shot { P = o + d * Tuning.Mercurius.SwirlRadius, V = d * Tuning.Mercurius.ShardSpeed, Kind = ShotKind.Shard, Returns = true, StickAt = stick, BackAt = stick + Tuning.Mercurius.ShardStuckSeconds, Home = o });
                            }
                            count++;
                            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Swipe, _at, 0.7f);
                            next += Tuning.Mercurius.ShardEvery;
                        }
                        break;
                    }
                    case Sequence.Hoops:
                    {
                        // Rings spreading out, one gap each, each turned from the last: STEP TO THE NEXT GAP.
                        // The first gap opens toward the player; denser adds a ring between, its gap halfway.
                        float every = Tuning.Mercurius.HoopEvery - idx * Tuning.Mercurius.HoopEveryPerLimb;
                        float step = Tuning.Mercurius.HoopTurnDegrees * Mathf.Deg2Rad * dir;
                        if (count == 0 && t >= next) theta = Mathf.Atan2(PlayerAt.y - o.y, PlayerAt.x - o.x) - step;
                        while (t >= next) { theta += step; HoopRing(o, theta); count++; next += every; nextDense = next - every * 0.5f; }
                        if (dense > 0 && nextDense > 0f && t >= nextDense) { HoopRing(o, theta + step * 0.5f); nextDense = -1f; }
                        break;
                    }
                    case Sequence.Crescents:
                    {
                        // Arcs aimed where the player stands: step PAST THE END.
                        float every = Tuning.Mercurius.CrescentEvery - idx * Tuning.Mercurius.CrescentEveryPerLimb;
                        if (dense > 0) every *= 0.5f + 0.5f / (1 + dense);
                        while (t >= next)
                        {
                            float aim = Mathf.Atan2(PlayerAt.y - o.y, PlayerAt.x - o.x);
                            _volley?.Add(new Crescent { O = o, Aim = aim, Half = (Tuning.Mercurius.CrescentHalfDegrees + idx * Tuning.Mercurius.CrescentHalfPerLimb) * Mathf.Deg2Rad });
                            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.ReleaseWater, _at, 0.5f);
                            next += every;
                        }
                        break;
                    }
                    default:
                    {
                        // Chains sweep round like clock hands, each missing bones at its own distance: THE
                        // MISSING BONE. Denser: a third chain, its hole at a third distance.
                        if (_volley == null) break;
                        int n = Mathf.Min(3, 2 + dense);
                        float w = (Tuning.Mercurius.ChainTurn + idx * Tuning.Mercurius.ChainTurnPerLimb) * dir;
                        float grow = Mathf.Clamp01(t / Tuning.Mercurius.ChainGrowSeconds) * (1f - Mathf.Clamp01((t - (T - Tuning.Mercurius.ChainGrowSeconds)) / Tuning.Mercurius.ChainGrowSeconds));
                        _volley.ChainO = o;
                        _volley.ChainLength = grow * Tuning.Mercurius.ChainReach;
                        for (int j = 0; j < n; j++)
                        {
                            float a = theta + w * t + j * Mathf.PI * 2f / n;
                            _volley.ChainPrev[j] = _volley.ChainCount > j ? _volley.ChainAngle[j] : a;
                            _volley.ChainAngle[j] = a;
                            _volley.ChainHole[j] = Tuning.Mercurius.HoleAt[(j + _spent) % Tuning.Mercurius.HoleAt.Length];
                        }
                        _volley.ChainCount = n;
                        if (Mathf.Repeat(t, 0.5f) < Time.deltaTime) Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Rattle, _at, 0.5f);
                        break;
                    }
                }

                yield return null;
                if (token != Token) yield break;
                if (WindowSpent) { _capped = true; break; }
            }
            if (_volley != null) _volley.ChainCount = 0;
            if (_figure != null && !skull) { _figure.Look.Spinning = -1; _figure.Look.Spin = 0f; _figure.Look.Raised = -1; _figure.Look.Raise = 0f; }
        }

        void Fire(Vector2 o, float a, float speed, ShotKind kind)
        {
            var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            _volley?.Add(new Shot { P = o + d * Tuning.Mercurius.SwirlRadius, V = d * speed, Kind = kind });
        }

        void HoopRing(Vector2 o, float gap)
        {
            int n = Tuning.Mercurius.HoopRing, g = Tuning.Mercurius.HoopGap;
            for (int m = 0; m < n; m++)
            {
                // the gap: HoopGap slots centred on the gap angle
                int off = m - n / 2;
                if (Mathf.Abs(off) <= g / 2) continue;
                Fire(o, gap + off * Mathf.PI * 2f / n, Tuning.Mercurius.HoopSpeed, ShotKind.Hoop);
            }
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Chip, _at, 0.5f);
        }

        // ------------------------------------------------------------------ the recovery

        IEnumerator Recovery(int token, int limb)
        {
            Phase = Movement.Recovering;
            float seconds = Tuning.Mercurius.RecoverySeconds - (Band >= Tuning.EnemyBands.MercuriusShortRecoveryFromBand ? Tuning.Mercurius.RecoveryShorter : 0f);
            float need = Health.Max * Tuning.Mercurius.KeepLostFraction, start = Health.Current;
            int knocked = 0, pieces = Tuning.Mercurius.Pieces;
            if (_figure != null) { _figure.Look.PieceLimb = limb; _figure.Look.Pieces = pieces; }
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Vulnerable, _at);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float dealt = start - Health.Current;
                int k = Mathf.Min(pieces, Mathf.FloorToInt(dealt / need * pieces));
                if (k > knocked)
                {
                    // each share of the need knocks one of its pieces away
                    TexelFx.Rubble(_at + Vector2.up * 1.1f, BronzeChips, 0.6f, 4 * (k - knocked), 0.9f);
                    Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Chip, _at);
                    knocked = k;
                }
                if (_figure != null) { _figure.Look.Sag = Mathf.Clamp01(t / 0.3f); _figure.Look.Gather = t / seconds; _figure.Look.Pieces = pieces - knocked; }
                yield return null;
                if (token != Token) yield break;
                if (WindowSpent) { _capped = true; break; }
                if (knocked >= pieces) break;
            }
            if (_figure != null) { _figure.Look.Pieces = 0; _figure.Look.PieceLimb = -1; }

            if (knocked >= pieces || _capped)
            {
                // Hit hard enough: the limb stays gone, and it sheds one step of density.
                _dense = Mathf.Max(0, _dense - 1);
                Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Rubble, _at);
            }
            else
            {
                // Not enough: it grows back (the swirl winding in), and the next pattern is denser. The
                // recovery is over - the bar stops reading OPEN while it winds the limb back in.
                Phase = Movement.Gathering;
                _lost &= ~(1 << limb);
                _dense = Mathf.Min(Tuning.Mercurius.DenseCap, _dense + 1);
                Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Seal, _at);
                for (float t = 0f; t < 0.6f; t += Time.deltaTime)
                {
                    if (_figure != null) { _figure.Look.Spinning = limb; _figure.Look.Spin = 1f - t / 0.6f; _figure.Look.Sag = 1f - t / 0.6f; }
                    yield return null;
                    if (token != Token) yield break;
                }
            }
            if (_figure != null) { _figure.Look.Spinning = -1; _figure.Look.Spin = 0f; _figure.Look.Sag = 0f; }
        }

        // ------------------------------------------------------------------ the regroup

        /// <summary>
        /// The cap (the user's pick, "sinks and surfaces"): immune, a stagger, it sinks into the floor's
        /// water and surfaces at the farthest point from the player INSIDE the middle of the room
        /// (Tuning.Mercurius.SurfaceZone) - never a corner, where half of every pattern would hit the walls.
        /// From band 3 it may come up as another sequence; lost limbs stay lost.
        /// </summary>
        IEnumerator Regroup(int token)
        {
            Phase = Movement.Regrouping;
            Health.Immune = true;
            Health.Floor = 0f;
            if (BodyCollider != null) BodyCollider.enabled = false;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Seal, _at);
            if (_figure != null) { _figure.Look.Spinning = -1; _figure.Look.Raised = -1; _figure.Look.Pieces = 0; }

            for (float t = 0f; t < Tuning.Mercurius.StaggerSeconds; t += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Shake = Mathf.Round(Mathf.Sin(t * 60f) * 1.5f * (1f - t / Tuning.Mercurius.StaggerSeconds));
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) _figure.Look.Shake = 0f;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Bubbling, _at);
            yield return Sink(token, 0f, 1f, Tuning.Mercurius.SinkSeconds);
            if (token != Token) yield break;

            var to = SurfacePoint();
            if (_floorPic != null) { _floorPic.BubblesAt = to; _floorPic.Bubbles = true; }
            _at = to;
            transform.position = to;
            Physics2D.SyncTransforms();
            if (Band >= Tuning.EnemyBands.MercuriusSwitchFromBand && Hash01(_spent, 7) < 0.5f)
            {
                _seq = (Sequence)(((int)_seq + 1 + (int)(Hash01(_spent, 8) * 4f) % 4) % 5);
                _cursor = 0;
            }
            yield return new WaitForSeconds(Tuning.Mercurius.UnderSeconds);
            if (token != Token) yield break;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.ReleaseWater, _at);
            yield return Sink(token, 1f, 0f, Tuning.Mercurius.SurfaceSeconds);
            if (token != Token) yield break;
            if (_floorPic != null) _floorPic.Bubbles = false;
            Health.Immune = false;
            if (BodyCollider != null) BodyCollider.enabled = true;
        }

        IEnumerator Sink(int token, float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (_figure != null) _figure.Look.Sink = Mathf.SmoothStep(from, to, t / seconds);
                yield return null;
                if (token != Token) yield break;
            }
            if (_figure != null) _figure.Look.Sink = to;
        }

        /// <summary>The farthest point from the player inside the middle of the room - a corner of the
        /// inner zone, which is never a corner of the room.</summary>
        Vector2 SurfacePoint()
        {
            var zone = Arena.HalfExtents * Tuning.Mercurius.SurfaceZone;
            var p = PlayerAt;
            return new Vector2(p.x > 0f ? -zone.x : zone.x, p.y > 0f ? -zone.y : zone.y);
        }

        // ------------------------------------------------------------------ the skull

        /// <summary>Its LAST WINDOW (the user's pick): whatever limbs are left sink away and the skull
        /// alone runs the sequence's last pattern, on a loop, until it dies.</summary>
        IEnumerator Skull(int token)
        {
            Phase = Movement.Skull;
            Health.Floor = 0f;
            Health.Immune = false;
            if (BodyCollider != null) BodyCollider.enabled = true;
            _lost = (1 << LimbCount) - 1;
            _dense = 0;
            Convergence.Audio.Sfx.Play(Convergence.Audio.Sound.Enrage, _at);
            int last = Order(_seq)[3];
            while (true)
            {
                yield return Pattern(token, last, 3, true);
                if (token != Token) yield break;
                yield return new WaitForSeconds(1.2f);
                if (token != Token) yield break;
            }
        }

        // ------------------------------------------------------------------ pieces

        static readonly Color32[] BronzeChips = { new(255, 224, 168, 255), new(234, 169, 92, 255), new(198, 124, 56, 255), new(148, 83, 42, 255), new(95, 51, 28, 255), new(52, 27, 16, 255) };

        /// <summary>A MECHANIC hit: a share of max health, mitigated through Take - the Boss base's rule.</summary>
        void HurtPlayer(float fraction)
        {
            if (Target == null) return;
            var hp = Target.GetComponent<Health>();
            if (hp == null || hp.IsDead) return;
            hp.Take(new DamageInfo(hp.Max * fraction, ElementType.Water, gameObject) { IgnoresHeat = true });
        }
    }
}
