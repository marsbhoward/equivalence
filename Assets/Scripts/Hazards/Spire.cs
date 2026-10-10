using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Exchange;

namespace Convergence.Hazards
{
    /// <summary>What a spire pays. ONE per spire, decided when it rises and shown by its colour.</summary>
    public enum SpireBoon { Heal, Repair, Might, Haste, Swiftness, Precision, Aegis }

    /// <summary>
    /// Copy, colour and effect of every spire boon, declared together for the same reason
    /// ExchangeCatalog declares them together: one place to add a boon, nowhere for the label and
    /// the effect to drift apart.
    ///
    /// Each spire is one of the seven PLANETARY METALS, the alchemist's own seven: Iron (Mars,
    /// war), Quicksilver (Mercury, the quick), Silver (the Moon), Gold (the Sun), Tin (Jupiter,
    /// the protector), Copper (Venus, verdigris - the healer) and Lead (Saturn, the heavy, the
    /// one a smith works). The enum keeps what the boon DOES; the metal is what the player reads.
    ///
    /// The floor-long boons are RUN-LAYER POINTS, added like any ledger entry and bent through
    /// the same Vessel (RunModifiers) - a spire tops up a run's stats, it never steps past the
    /// thresholds a run is allowed. Aegis is mitigation, inside the one mitigation floor.
    /// </summary>
    public static class SpireBoons
    {
        public const int Count = 7;

        /// <summary>
        /// The spire's colour IS the boon - the one thing the player reads before deciding whether
        /// a capture is worth the fight. Hues are spread as far apart as seven allow, and the name
        /// is flashed when the spire rises so colour is never the only channel.
        /// </summary>
        public static Color ColorOf(SpireBoon b) => b switch
        {
            SpireBoon.Heal      => new Color(0.36f, 0.95f, 0.45f),   // Copper: verdigris green
            SpireBoon.Repair    => new Color(0.62f, 0.74f, 0.92f),   // Lead: cold grey steel
            SpireBoon.Might     => new Color(1.00f, 0.28f, 0.22f),   // Iron: Mars red
            SpireBoon.Haste     => new Color(1.00f, 0.38f, 0.85f),   // Quicksilver: cinnabar's rose
            SpireBoon.Swiftness => new Color(0.20f, 0.92f, 0.95f),   // Silver: moonlit cyan
            SpireBoon.Precision => new Color(1.00f, 0.82f, 0.18f),   // Gold: the Sun
            _                   => new Color(0.56f, 0.40f, 1.00f),   // Tin (Aegis): Jupiter's violet
        };

        /// <summary>The metal - what the spire is called wherever it is named.</summary>
        public static string NameOf(SpireBoon b) => b switch
        {
            SpireBoon.Heal => "Copper",
            SpireBoon.Repair => "Lead",
            SpireBoon.Might => "Iron",
            SpireBoon.Haste => "Quicksilver",
            SpireBoon.Swiftness => "Silver",
            SpireBoon.Precision => "Gold",
            _ => "Tin",
        };

        public static string Describe(SpireBoon b) => b switch
        {
            SpireBoon.Heal => $"restore {Pct(Tuning.Spire.HealFraction)} health",
            SpireBoon.Repair => $"restore {Pct(Tuning.Spire.RepairFraction)} armour condition",
            SpireBoon.Might => $"+{Points(Tuning.Spire.DamagePoints)} damage this floor",
            SpireBoon.Haste => $"+{Points(Tuning.Spire.AttackSpeedPoints)} attack speed this floor",
            SpireBoon.Swiftness => $"+{Points(Tuning.Spire.MoveSpeedPoints)} move speed this floor",
            SpireBoon.Precision => $"+{Pct(Tuning.Spire.CritChance)} crit chance this floor",
            _ => $"-{Pct(1f - Tuning.Spire.DamageTakenMul)} damage taken this floor",
        };

        /// <summary>Heal and Repair happen once, at capture; the rest last the floor.</summary>
        public static bool Instant(SpireBoon b) => b == SpireBoon.Heal || b == SpireBoon.Repair;

        /// <summary>The floor-long half, folded into the ledger through RunModifiers.SetFloorBoon.
        /// Null for the instant boons.</summary>
        public static System.Action<Mods> Apply(SpireBoon b) => b switch
        {
            // Points, added exactly as the ledger's own entries add them (ExchangeCatalog), so the
            // Vessel bends a spire and a boon as one total.
            SpireBoon.Might => m => m.Add(Art.Gear.StatKind.Damage, Tuning.Spire.DamagePoints),
            SpireBoon.Haste => m => m.Add(Art.Gear.StatKind.AttackSpeed, Tuning.Spire.AttackSpeedPoints),
            SpireBoon.Swiftness => m => m.Add(Art.Gear.StatKind.MoveSpeed, Tuning.Spire.MoveSpeedPoints),
            SpireBoon.Precision => m => m.BonusCrit += Tuning.Spire.CritChance,
            SpireBoon.Aegis => m => m.DamageTakenMul *= Tuning.Spire.DamageTakenMul,
            _ => null,
        };

        /// <summary>
        /// Rolled when the spire rises. Heal and Repair are weighted by whether they would help
        /// RIGHT NOW (the floor reward's PickRestoreKind rule) - not excluded, because a full-health
        /// player at the start of a floor is exactly the one who may want it a minute later.
        /// </summary>
        public static SpireBoon Roll(bool hurt, bool worn)
        {
            float wHeal = hurt ? 1.5f : 0.4f;
            float wRepair = worn ? 1.5f : 0.3f;
            float total = wHeal + wRepair + 5f;
            float r = Random.value * total;
            if (r < wHeal) return SpireBoon.Heal;
            r -= wHeal;
            if (r < wRepair) return SpireBoon.Repair;
            return (SpireBoon)(2 + Mathf.Min(4, (int)(r - wRepair)));
        }

        static string Pct(float f) => $"{Mathf.RoundToInt(f * 100f)}%";
        static string Points(float p) => $"{Mathf.RoundToInt(p)}%";
    }

    /// <summary>
    /// A capture point: an obelisk with a ring round its foot. Stand in the ring for
    /// <see cref="Tuning.Spire.CaptureSeconds"/> and it pays its one boon; leaving drains progress
    /// at the rate it fills rather than resetting it. Enemies do not stop it - they make standing
    /// there expensive, which is the cost. A spire may stand in a pit: its ring draws over the
    /// pit band, and the pit's own hazard becomes one more price of the capture.
    ///
    /// It is a FLOOR EVENT: built with the room (so the columns give way to its ring) but fully
    /// under the floor, it RISES once a share of the floor's enemies are dead (GameBootstrap calls
    /// <see cref="Rise"/>), and when the floor clears it SINKS back (taken or not). Both are the
    /// one animation run either way: the shaft slides through a SpriteMask cut at ground level, so
    /// it comes out of / goes into the ground rather than growing or shrinking.
    ///
    /// TWO CHALLENGES, each its own roll, both scaling with depth:
    ///   LINES   - beams sweeping round the ring from the obelisk to the rim. Step over them; a
    ///             stationary capture is a capture that eats every pass.
    ///   BUBBLE  - a dome over the ring that attacks cannot cross (HazardQuery treats its edge as a
    ///             Blue field). Inside, you can only fight what is inside with you; outside, you
    ///             cannot touch what is standing in the ring. Bodies walk through freely.
    /// Both end at capture - the challenge guards the boon, not the ground.
    ///
    /// GameBootstrap POLLS <see cref="TryClaim"/> rather than being handed a callback (a delegate
    /// does not survive a domain reload; see the puzzle floors for the same rule). Every cached
    /// field is serialized and non-readonly for the reason FloorPit gives.
    ///
    /// THE OBELISK IS SOLID AND BLOCKS SIGHT like a column - it is a CircleCollider2D like one,
    /// and HazardQuery's "anything else solid blocks" rule applies to it without a special case.
    /// </summary>
    public class Spire : MonoBehaviour
    {
        /// <summary>One-shot: the next eligible floor gets a spire. For play-testing from eval:
        /// <c>Convergence.Hazards.Spire.DevForceNext = true;</c> (optionally with DevForceBoon /
        /// DevForceLines / DevForceBubble).</summary>
        public static bool DevForceNext;
        public static SpireBoon? DevForceBoon;
        public static int DevForceLines = -1;
        public static bool? DevForceBubble;

        [SerializeField] SpireBoon _boon;
        [SerializeField] float _progress;          // 0..1
        [SerializeField] bool _captured, _claimed, _collapsed, _rising, _sinking;
        [SerializeField] float _sink = 1f;         // 0 = standing, 1 = wholly under the floor
        [SerializeField] int _lineCount;
        [SerializeField] float _lineSpeed, _lineAngle, _lineDamage;
        [SerializeField] float[] _lineCooldown;
        [SerializeField] bool _bubble;
        [SerializeField] float _fade;              // challenge art: in once risen, out once captured/sunk

        [SerializeField] Transform _player;
        [SerializeField] Health _playerHealth;

        [SerializeField] SpriteRenderer _ring, _fill, _core, _coreGlow, _channelUp, _channelDown, _grooveUp, _grooveDown;
        [SerializeField] SpriteRenderer _dome, _domeRim, _shadow;
        [SerializeField] Transform _sinkRoot;
        [SerializeField] CircleCollider2D _collider;
        [SerializeField] SpriteRenderer[] _lineArt;
        [SerializeField] Transform _linePivot;

        public SpireBoon Boon => _boon;
        public bool Captured => _captured;
        public float Progress01 => _progress;
        public bool HasBubble => _bubble;
        public int LineCount => _lineCount;
        public Vector2 Centre => transform.position;

        // ---- the bubble, as HazardQuery sees it ----

        /// <summary>The live bubble, re-asserted every frame by its own Update so a domain reload
        /// (which nulls statics) costs at most one frame. Unity's null check covers a destroyed one.</summary>
        static Spire _liveBubble;

        /// <summary>True when exactly one of the two points is inside a live bubble - the line
        /// between them crosses its wall.</summary>
        public static bool BubbleSeparates(Vector2 a, Vector2 b)
        {
            var s = _liveBubble;
            if (s == null || !s.BubbleUp) return false;
            return s.InRing(a) != s.InRing(b);
        }

        bool BubbleUp => _bubble && Live;

        /// <summary>Standing, and still to be taken - the only state in which it can be captured,
        /// its lines hurt, or its bubble stands.</summary>
        bool Live => Risen && !_captured && !_collapsed;

        /// <summary>Fully out of the ground.</summary>
        public bool Risen => _rising && _sink <= 0f;

        public bool InRing(Vector2 p)
            => (p - (Vector2)transform.position).sqrMagnitude < Tuning.Spire.CaptureRadius * Tuning.Spire.CaptureRadius;

        public bool PlayerInside => _player != null && InRing(_player.position);

        // ---- building ----

        // ABOVE the pit band (PitBase..PitBase+5), because a spire may stand in a pit and its ring
        // has to read over fire, sand and jacks alike - and still below every body.
        const int OrderFill = SortingOrders.PitBase + 5;
        const int OrderRing = SortingOrders.PitBase + 6;

        static readonly Color Stone = new(0.24f, 0.25f, 0.31f);
        static readonly Color StoneLit = new(0.36f, 0.37f, 0.45f);
        static readonly Color LineCore = new(1f, 0.92f, 0.85f);
        static readonly Color LineEdge = new(1f, 0.25f, 0.18f);
        static readonly Color DomeTint = new(0.75f, 0.88f, 1f);

        public static Spire Spawn(Vector2 pos, SpireBoon boon, int lines, float lineSpeed, float lineDamage,
                                  bool bubble, Transform player, Transform parent)
        {
            var go = new GameObject($"spire.{boon}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var s = go.AddComponent<Spire>();
            s._boon = boon;
            s._lineCount = lines;
            s._lineSpeed = lineSpeed * (Random.value < 0.5f ? 1f : -1f);
            s._lineAngle = Random.Range(0f, 360f);
            s._lineDamage = lineDamage;
            s._lineCooldown = new float[lines];
            s._bubble = bubble;
            s._player = player;
            s._playerHealth = player != null ? player.GetComponent<Health>() : null;

            s.BuildGround();
            s.BuildObelisk();
            if (lines > 0) s.BuildLines();
            if (bubble) s.BuildBubble();

            s._collider = go.AddComponent<CircleCollider2D>();
            s._collider.radius = Tuning.Spire.BodyRadius;
            s._collider.enabled = false;   // under the floor until it rises
            s.PlaceShaft();

            s.Repaint();
            return s;
        }

        void BuildGround()
        {
            float d = Tuning.Spire.CaptureRadius * 2f;
            _fill = Quad(gameObject, Spr.Circle, Color.clear, Vector2.one * 0.01f, OrderFill, "fill");
            _ring = Quad(gameObject, Spr.ThinRing, Color.clear, Vector2.one * d, OrderRing, "ring");
        }

        /// <summary>
        /// A stone shaft standing up from the ring's centre, with the boon's light in it: a CIRCLE
        /// at the middle of the shaft and a line running UP and DOWN from it, so the colour reads
        /// as something inside the stone rather than paint on it. The channels fill outward from
        /// the circle as the capture climbs.
        /// </summary>
        void BuildObelisk()
        {
            var body = new GameObject("visual");
            body.transform.SetParent(transform, false);

            float w = Tuning.Spire.BodyRadius * 1.5f;
            float h = Tuning.Spire.BodyHeight;

            _shadow = Quad(body, Spr.Circle, new Color(0f, 0f, 0f, 0.38f),
                           new Vector2(w * 1.7f, w * 0.7f), 4, "shadow");
            _shadow.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            // Everything that sinks hangs off one root, so sinking is one transform moving.
            var sink = new GameObject("sink");
            sink.transform.SetParent(body.transform, false);
            _sinkRoot = sink.transform;

            var shaft = Quad(sink, Spr.Square, Stone, new Vector2(w, h), 5, "shaft");
            shaft.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            // Lit face down the left: the same top-left key light the pits' near wall assumes.
            var lit = Quad(sink, Spr.Square, StoneLit, new Vector2(w * 0.32f, h), 6, "lit");
            lit.transform.localPosition = new Vector3(-w * 0.34f, h * 0.5f, 0f);
            // The capstone: a diamond (a square turned 45) sitting on the shaft's top edge.
            var cap = Quad(sink, Spr.Square, StoneLit, Vector2.one * (w * 0.72f), 6, "cap");
            cap.transform.localPosition = new Vector3(0f, h, 0f);
            cap.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var plinth = Quad(sink, Spr.Square, Color.Lerp(Stone, Color.black, 0.2f),
                              new Vector2(w * 1.35f, w * 0.35f), 6, "plinth");
            plinth.transform.localPosition = new Vector3(0f, w * 0.17f, 0f);

            float mid = h * 0.5f;
            float chanW = w * 0.14f;
            // The grooves run the shaft's full length always, in the boon's colour sunk into the
            // stone - the spire reads as its boon from across the room, before it is touched.
            float grooveLen = h * 0.5f - 0.12f;
            _grooveUp = Quad(sink, Spr.Square, Color.clear, new Vector2(chanW, grooveLen), 6, "groove.up");
            _grooveUp.transform.localPosition = new Vector3(0f, mid + grooveLen * 0.5f, 0f);
            _grooveDown = Quad(sink, Spr.Square, Color.clear, new Vector2(chanW, grooveLen), 6, "groove.down");
            _grooveDown.transform.localPosition = new Vector3(0f, mid - grooveLen * 0.5f, 0f);
            _channelUp = Quad(sink, Spr.Square, Color.clear, new Vector2(chanW, 0.01f), 7, "channel.up");
            _channelDown = Quad(sink, Spr.Square, Color.clear, new Vector2(chanW, 0.01f), 7, "channel.down");
            _coreGlow = Quad(sink, Spr.Glow, Color.clear, Vector2.one * (w * 2.2f), 8, "core.glow");
            _coreGlow.transform.localPosition = new Vector3(0f, mid, 0f);
            _core = Quad(sink, Spr.Circle, Color.clear, Vector2.one * (w * 0.62f), 9, "core");
            _core.transform.localPosition = new Vector3(0f, mid, 0f);

            DepthSorted.Attach(body, 0f, true, body.GetComponentsInChildren<SpriteRenderer>(true));

            // The ground line: a mask covering everything ABOVE the foot. Whatever slides below
            // it is gone, which is what makes the sink read as INTO the floor. Range-limited to
            // the shaft's own (fixed, depth-sorted) orders, for the Rift's reason - an unranged
            // mask would cut whatever else opts into masking at those orders.
            float sinkDepth = SinkDepth;
            var maskGo = new GameObject("ground-line");
            maskGo.transform.SetParent(body.transform, false);
            maskGo.transform.localScale = new Vector3(w * 4f, sinkDepth * 2f, 1f);
            maskGo.transform.localPosition = new Vector3(0f, sinkDepth, 0f);
            var mask = maskGo.AddComponent<SpriteMask>();
            mask.sprite = Spr.Square;

            int lo = int.MaxValue, hi = int.MinValue;
            foreach (var sr in sink.GetComponentsInChildren<SpriteRenderer>(true))
            {
                sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                lo = Mathf.Min(lo, sr.sortingOrder);
                hi = Mathf.Max(hi, sr.sortingOrder);
            }
            // Except the glow: a soft sprite under a mask is cut to a hard-edged block. It
            // stays unmasked and fades out in the first moments of the sink instead.
            _coreGlow.maskInteraction = SpriteMaskInteraction.None;
            mask.isCustomRangeActive = true;
            mask.backSortingOrder = lo - 1;
            mask.frontSortingOrder = hi + 1;
        }

        /// <summary>How far the shaft travels to be wholly under the floor - its height plus the
        /// capstone's point above it.</summary>
        static float SinkDepth => Tuning.Spire.BodyHeight + Tuning.Spire.BodyRadius * 1.5f * 0.6f;

        void BuildLines()
        {
            _linePivot = new GameObject("lines").transform;
            _linePivot.SetParent(transform, false);

            float inner = Tuning.Spire.BodyRadius;
            float len = Tuning.Spire.CaptureRadius - inner;
            float width = Tuning.Spire.LineHalfWidth * 2f;
            _lineArt = new SpriteRenderer[_lineCount * 2];
            for (int i = 0; i < _lineCount; i++)
            {
                var arm = new GameObject($"line.{i}").transform;
                arm.SetParent(_linePivot, false);
                arm.localRotation = Quaternion.Euler(0f, 0f, 360f * i / _lineCount);

                // Laid along +X from the obelisk's rim to the ring's. A red edge band under a
                // white-hot core: a damaging line must read as DANGER whatever colour the spire
                // is, or a green Mending spire's lines would read as part of the heal.
                var edge = Quad(arm.gameObject, Spr.Capsule, LineEdge, new Vector2(width * 1.8f, len), OrderRing, "edge");
                edge.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                edge.transform.localPosition = new Vector3(inner + len * 0.5f, 0f, 0f);
                var core = Quad(arm.gameObject, Spr.Capsule, LineCore, new Vector2(width * 0.6f, len * 0.98f), OrderRing, "core");
                core.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
                core.transform.localPosition = new Vector3(inner + len * 0.5f, 0f, 0f);
                _lineArt[i * 2] = edge;
                _lineArt[i * 2 + 1] = core;
            }
        }

        void BuildBubble()
        {
            float d = Tuning.Spire.CaptureRadius * 2f;
            // The dome's fill on the GROUND, so it tints the floor and never washes out a body
            // standing inside it; only the rim is drawn over everything, as a force field is.
            _dome = Quad(gameObject, Spr.GradientDisc(), Color.clear, Vector2.one * d, OrderFill, "dome");
            // Just OUTSIDE the capture ring, so the boon-coloured ring still shows inside the wall.
            _domeRim = Quad(gameObject, Spr.ThinRing, Color.clear, Vector2.one * (d * 1.07f), SortingOrders.Telegraph, "dome.rim");
        }

        static SpriteRenderer Quad(GameObject parent, Sprite sprite, Color color, Vector2 size, int order, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        // ---- the floor's hooks ----

        /// <summary>The floor's event: the spire comes up out of the ground. Once only, and never
        /// after the floor has cleared.</summary>
        public void Rise()
        {
            if (_rising || _sinking) return;
            _rising = true;
        }

        /// <summary>One-shot: true exactly once, the first time it is asked after the capture.</summary>
        public bool TryClaim(out SpireBoon boon)
        {
            boon = _boon;
            if (!_captured || _claimed) return false;
            _claimed = true;
            return true;
        }

        /// <summary>
        /// The floor cleared: the spire sinks into the floor, taken or not (one that never rose
        /// simply stays under). An untaken one goes
        /// dark first and can no longer be captured - a spire is a prize taken UNDER pressure,
        /// and capturable after the clear a Mending spire would be a free heal on every floor.
        /// Scaled time, deliberately: the clear opens the reward screens under a pause, so the
        /// sink plays when the player is back in the room to see it.
        /// </summary>
        public void Sink()
        {
            if (_sinking) return;
            _sinking = true;
            if (!_captured) _collapsed = true;
        }

        public bool Sunk => _sink >= 1f;

        // ---- per frame ----

        void Update()
        {
            if (BubbleUp) _liveBubble = this;
            else if (_liveBubble == this) _liveBubble = null;

            float dt = Time.deltaTime;
            if (_sinking) { if (_sink < 1f) MoveShaft(+dt); }
            else if (_rising && _sink > 0f) MoveShaft(-dt);

            bool live = Live;
            if (live) TickCapture(dt);
            _fade = Mathf.MoveTowards(_fade, live ? 1f : 0f, dt * 2.5f);

            if (_lineCount > 0 && _linePivot != null)
            {
                _lineAngle += _lineSpeed * dt;
                _linePivot.localRotation = Quaternion.Euler(0f, 0f, _lineAngle);
                if (live) TickLines(dt);
            }

            Repaint();
        }

        void TickCapture(float dt)
        {
            bool inside = _playerHealth != null && !_playerHealth.IsDead && PlayerInside;

            float rate = 1f / Tuning.Spire.CaptureSeconds;
            if (inside) _progress += rate * dt;
            else _progress -= rate * Tuning.Spire.DecayRate * dt;
            _progress = Mathf.Clamp01(_progress);

            if (_progress >= 1f)
            {
                _captured = true;
                Spr.Flash(transform.position, Tuning.Spire.CaptureRadius * 2f, SpireBoons.ColorOf(_boon), 0.5f, true);
            }
        }

        /// <summary>
        /// One animation, both ways: dt > 0 sinks, dt < 0 rises - the rise is the sink played
        /// backwards. A puff of stone dust as it breaks the floor either way.
        /// </summary>
        void MoveShaft(float dt)
        {
            bool starting = dt > 0f ? _sink <= 0f : _sink >= 1f;
            _sink = Mathf.Clamp01(_sink + dt / Tuning.Spire.SinkSeconds);
            if (starting)
                Spr.Flash(transform.position, Tuning.Spire.BodyRadius * 3.2f, new Color(0.55f, 0.52f, 0.48f), 0.45f, true);
            PlaceShaft();
        }

        void PlaceShaft()
        {
            // Eased both ends: stone grinding loose, moving, settling. A sideways rumble while it
            // is part way, gone at either end.
            float e = _sink * _sink * (3f - 2f * _sink);
            float rumble = Mathf.Sin(Time.time * 55f) * 0.03f * Mathf.Sin(_sink * Mathf.PI);
            if (_sinkRoot != null) _sinkRoot.localPosition = new Vector3(rumble, -SinkDepth * e, 0f);

            // Solid only while mostly out of the ground; under it, the ground is just ground.
            if (_collider != null) _collider.enabled = _sink < 0.6f;
        }

        void TickLines(float dt)
        {
            if (_lineCooldown == null || _lineCooldown.Length != _lineCount) _lineCooldown = new float[_lineCount];
            for (int i = 0; i < _lineCount; i++) _lineCooldown[i] = Mathf.Max(0f, _lineCooldown[i] - dt);

            if (_player == null || _playerHealth == null || _playerHealth.IsDead) return;
            var d = (Vector2)_player.position - (Vector2)transform.position;
            float r = d.magnitude;
            if (r > Tuning.Spire.CaptureRadius || r < 0.001f) return;

            const float BodyAllowance = 0.18f;   // the player's feet are not a point
            for (int i = 0; i < _lineCount; i++)
            {
                if (_lineCooldown[i] > 0f) continue;
                float a = (_lineAngle + 360f * i / _lineCount) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (Vector2.Dot(dir, d) <= 0f) continue;                           // the other side
                float across = Mathf.Abs(dir.x * d.y - dir.y * d.x);
                if (across > Tuning.Spire.LineHalfWidth + BodyAllowance) continue;

                _lineCooldown[i] = Tuning.Spire.LineHitCooldown;
                _playerHealth.Take(new DamageInfo(_lineDamage, ElementType.Earth, gameObject));
            }
        }

        void Repaint()
        {
            var tint = SpireBoons.ColorOf(_boon);
            bool dark = _collapsed && !_captured;
            float t = Time.time;

            // Ground art goes with the spire: gone by the time the shaft is under.
            float ground = 1f - _sink;

            // Ring: steady at rest, brighter and quicker-pulsing while held.
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * (PlayerInside ? 6f : 2.2f));
            var ring = tint;
            ring.a = (dark ? 0.12f : _captured ? 0.8f : 0.6f + 0.3f * pulse) * ground;
            if (_ring != null) _ring.color = ring;

            if (_fill != null)
            {
                float fd = Tuning.Spire.CaptureRadius * 2f * (_captured ? 1f : _progress);
                _fill.transform.localScale = new Vector3(fd, fd, 1f);
                var f = tint;
                f.a = (dark ? 0f : _captured ? 0.10f : 0.22f) * ground;
                _fill.color = f;
            }

            // The light in the stone: the circle always burns (it names the boon), the channels
            // grow out of it toward each end as the capture climbs and run full once it is taken.
            float h = Tuning.Spire.BodyHeight;
            float mid = h * 0.5f;
            float reach = (h * 0.5f - 0.12f) * (_captured ? 1f : _progress);
            var light = dark ? Color.Lerp(tint, Color.gray, 0.8f) : tint;
            float glowA = dark ? 0.05f : _captured ? 0.65f : 0.35f + 0.2f * pulse;

            if (_core != null) _core.color = Color.Lerp(light, Color.white, _captured ? 0.35f : 0.1f);
            if (_coreGlow != null) { var g = light; g.a = glowA * Mathf.Clamp01(1f - _sink * 3f); _coreGlow.color = g; }
            var groove = Color.Lerp(light, Stone, dark ? 0.85f : 0.45f);
            if (_grooveUp != null) _grooveUp.color = groove;
            if (_grooveDown != null) _grooveDown.color = groove;
            var lit = Color.Lerp(light, Color.white, 0.25f);
            SetChannel(_channelUp, mid, reach, +1f, lit);
            SetChannel(_channelDown, mid, reach, -1f, lit);
            if (_shadow != null) _shadow.color = new Color(0f, 0f, 0f, 0.38f * ground);

            if (_lineArt != null)
                for (int i = 0; i < _lineArt.Length; i++)
                {
                    if (_lineArt[i] == null) continue;
                    var c = (i & 1) == 0 ? LineEdge : LineCore;
                    c.a = ((i & 1) == 0 ? 0.55f : 0.95f) * _fade;
                    _lineArt[i].color = c;
                }

            if (_dome != null)
            {
                float shimmer = 0.5f + 0.5f * Mathf.Sin(t * 3.1f);
                var dc = DomeTint; dc.a = (0.10f + 0.04f * shimmer) * _fade;
                _dome.color = dc;
                var rc = Color.Lerp(DomeTint, Color.white, 0.3f); rc.a = (0.28f + 0.14f * shimmer) * _fade;
                _domeRim.color = rc;
            }
        }

        static void SetChannel(SpriteRenderer sr, float mid, float reach, float sign, Color c)
        {
            if (sr == null) return;
            var s = sr.transform.localScale;
            sr.transform.localScale = new Vector3(s.x, reach, 1f);
            sr.transform.localPosition = new Vector3(0f, mid + sign * reach * 0.5f, 0f);
            sr.color = c;
        }

        void OnDestroy()
        {
            if (_liveBubble == this) _liveBubble = null;
        }
    }
}
