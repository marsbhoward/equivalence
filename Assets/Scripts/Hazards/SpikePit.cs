using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// A bed of jacks. Hurts only while the player is BOTH inside it and moving - stand still and
    /// the damage stops, which is the whole mechanic and the whole tell.
    ///
    /// IT IS A TEMPO TRAP, AND IT IS THE FIRST HAZARD THAT CAN BE ANSWERED BY DOING NOTHING. Fire
    /// is answered by timing, sand by routing around; this one is answered by standing still,
    /// which in a game about a closing crowd is the most expensive thing a player can be asked to
    /// do. The cost is never the damage - it is the seconds spent stationary with a pack arriving,
    /// and with an Air build's own momentum meter bleeding the entire time.
    ///
    /// MEASURED DISPLACEMENT, NOT THE STICK. It reads how far the body actually travelled rather
    /// than PlayerController.IsMoving, and the difference matters in exactly the case that makes
    /// the trap interesting: being knocked into one and sliding across it hurts. Spikes have no
    /// opinion about why you are moving over them. It also means the answer is genuinely "stop",
    /// not "let go of the stick" - a player still sliding on their own momentum is still being
    /// dragged over the bed.
    ///
    /// TIME-BASED WHILE MOVING, NOT PER UNIT TRAVELLED, and that is what makes it compose with
    /// the other two. Per-distance, crossing costs the same however you do it and a sand trap
    /// laid over spikes would change nothing. Per-second-while-moving, being slowed means being
    /// dragged for longer, so the post-floor-25 combination of the two is a real compound hazard
    /// rather than two decorations sharing a cell.
    /// </summary>
    public class SpikePit : FloorPit
    {
        public override PitKind Kind => PitKind.Spike;

        /// <summary>Spikes are always up.</summary>
        public override bool HurtsNow => true;

        // Serialized for the reason FloorPit's own note gives. Losing these to a reload would be
        // mild rather than silent - tracking simply restarts, costing one free frame - but "mild"
        // is a judgement about today's three fields, and the rule is what stops the fourth one
        // being the one that matters.
        [SerializeField] float _tick;
        [SerializeField] Vector2 _lastPos;
        [SerializeField] bool _tracking;

        public override float RouteCost => Tuning.Steering.SpikeRouteCost;

        public static SpikePit Spawn(Rect rect, Transform player, Transform parent)
        {
            var go = new GameObject("pit.spike");
            go.transform.SetParent(parent, false);
            var pit = go.AddComponent<SpikePit>();
            pit.Init(rect, player);
            pit.Build(rect.size);
            return pit;
        }

        static readonly Color PitFloor = new(0.20f, 0.19f, 0.21f);
        static readonly Color JackSteel = new(0.74f, 0.75f, 0.80f);
        static readonly Color JackShadow = new(0f, 0f, 0f, 0.45f);

        void Build(Vector2 size)
        {
            // The floor keeps the arena's own stone, just darker for sitting in shadow at the
            // bottom of a hole - the recess is what says "sunken", not a different material.
            PitArt.Quad(gameObject, Spr.Square, PitFloor, size, OrderFloor, "floor");
            PitArt.Tiled(gameObject, PitArt.Mottle(Random.Range(1, 9999)),
                         new Color(0.30f, 0.29f, 0.32f, 0.7f), size, OrderMaterial, "grit");

            BuildRecess(size);
            ScatterJacks(size);
        }

        /// <summary>
        /// Jacks on a JITTERED LATTICE rather than at free random points. Uniform random placement
        /// clumps - it leaves bare patches big enough to look like safe footing and clusters
        /// elsewhere that read as one lump of metal. A lattice cell each, jittered inside it,
        /// covers the bed evenly while still looking strewn, which is what "spread about"
        /// actually needs: no two in the same place, and no visible rows either.
        /// </summary>
        void ScatterJacks(Vector2 size)
        {
            float step = Tuning.Hazards.SpikeJackSpacing;
            int nx = Mathf.Max(1, Mathf.RoundToInt(size.x / step));
            int ny = Mathf.Max(1, Mathf.RoundToInt(size.y / step));
            float cw = size.x / nx, ch = size.y / ny;
            float margin = Tuning.Hazards.PitStandInset * 0.5f;

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                // A few cells left deliberately empty, so the bed is not a regular field however
                // well the jitter hides the lattice. A perfectly complete grid reads as a
                // manufactured grate rather than as loose caltrops someone scattered.
                if (Random.value < Tuning.Hazards.SpikeJackSkipChance) continue;

                float cx = -size.x * 0.5f + (ix + 0.5f) * cw;
                float cy = -size.y * 0.5f + (iy + 0.5f) * ch;
                var p = new Vector2(
                    cx + Random.Range(-cw * 0.34f, cw * 0.34f),
                    cy + Random.Range(-ch * 0.34f, ch * 0.34f));

                // Clamped inside the pit's own standing area, or a jack on the rim would draw
                // over the lip and read as lying on the floor outside the hole.
                p.x = Mathf.Clamp(p.x, -size.x * 0.5f + margin, size.x * 0.5f - margin);
                p.y = Mathf.Clamp(p.y, -size.y * 0.5f + margin, size.y * 0.5f - margin);

                float s = Random.Range(Tuning.Hazards.SpikeJackSizeMin, Tuning.Hazards.SpikeJackSizeMax);
                // Rotation is free: a jack lands however it lands, and identical orientations
                // would give the bed a grain it should not have.
                float rot = Random.Range(0f, 360f);

                // Drawn twice - a dark copy offset down-right, then the steel over it. A flat
                // silhouette on a flat floor reads as a mark painted on the stone; the offset
                // shadow is the cheapest thing that makes it an OBJECT lying in the pit.
                var shadow = PitArt.Quad(gameObject, PitArt.Jack, JackShadow, Vector2.one * s, OrderDetail, "jack.shadow");
                shadow.transform.localPosition = new Vector3(p.x + s * 0.09f, p.y - s * 0.09f, 0f);
                shadow.transform.localRotation = Quaternion.Euler(0f, 0f, rot);

                var jack = PitArt.Quad(gameObject, PitArt.Jack, JackSteel, Vector2.one * s, OrderAbove, "jack");
                jack.transform.localPosition = new Vector3(p.x, p.y, 0f);
                jack.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
            }
        }

        /// <summary>
        /// FIXEDUPDATE, AND THE PHYSICS POSE. This is the only hazard that differences a position
        /// against the previous sample, and that makes it the only one that cares where it is
        /// sampled from.
        ///
        /// It ran in Update against Player.position and was very nearly inert. A body only moves
        /// on a physics STEP, so between steps its transform is unchanged - at 120fps against a
        /// 50Hz step that is about three frames in five differencing a position against itself and
        /// reading a speed of exactly zero. Every one of those frames took the stationary branch
        /// and reset _tick, so the 0.4s interval needed ~48 consecutive moving frames and never
        /// got more than two. The trap charged a player for standing still correctly and charged
        /// them for running across it essentially never.
        ///
        /// Interpolating the player (which the arena now does) hides this by making the transform
        /// advance every frame, and that is exactly why the sampling is pinned here instead of
        /// left to benefit from it: an interpolation setting is a RENDERING decision, and a
        /// hazard's damage must not be downstream of one. In FixedUpdate the sample is the physics
        /// pose either way, one per step, and the trap behaves identically at any framerate.
        ///
        /// Still displacement, not velocity, per the class note above - a player pinned against a
        /// wall has a velocity but is not being dragged over anything.
        /// </summary>
        void FixedUpdate()
        {
            if (!PlayerAlive) { _tracking = false; return; }

            // rb.position, not Player.position: see above. The transform fallback is for a player
            // built without a body, which nothing does today.
            var pos = PlayerBody != null ? PlayerBody.position : (Vector2)Player.position;

            // Tested against the same sample the movement is differenced from, so "inside" and
            // "moved" can never disagree about which pose they are talking about.
            if (!Contains(pos))
            {
                _tick = 0f;
                _tracking = false;
                return;
            }

            // The first step inside has no previous sample to difference against, so it can
            // never be charged - otherwise entering at speed would read the whole approach as
            // movement inside the trap.
            if (!_tracking)
            {
                _tracking = true;
                _lastPos = pos;
                return;
            }

            float dt = Time.fixedDeltaTime;
            float speed = dt > 0f ? Vector2.Distance(pos, _lastPos) / dt : 0f;
            _lastPos = pos;

            // Reset rather than freeze when they stop: a partially-charged tick held across a
            // pause would mean the next step taken hurts immediately, which reads as the trap
            // punishing the decision to stop rather than rewarding it.
            if (speed < Tuning.Hazards.SpikeMovementThreshold) { _tick = 0f; return; }

            _tick += dt;
            if (_tick < Tuning.Hazards.SpikeTickInterval) return;
            _tick = 0f;

            PlayerHealth.Take(new DamageInfo(
                Tuning.Hazards.SpikeDamagePerSecond * Tuning.Hazards.SpikeTickInterval * DamageScale,
                ElementType.Earth, gameObject));
        }
    }
}
