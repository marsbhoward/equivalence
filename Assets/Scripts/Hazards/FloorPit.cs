using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// The shared half of the four floor pits: a SHAPE (<see cref="PitShape"/> - the cells a pit
    /// piece was laid on, rounded, or a rect the exchange lays), the recess that makes it read as
    /// a HOLE rather than as a mark, and the per-frame "is the player in this" test every kind
    /// needs.
    ///
    /// SHAPED, ON THE ROOM'S OWN CELLS (the user's call 2026-11-18, Hazards.PitPieces): pits are
    /// laid with the room rather than dropped into regions of it, so a trough runs along the foot
    /// of its wall and a spill pours out from under its crack. Columns keep clear by their rim
    /// (HazardBuilder.BuildColumnGrid asks <see cref="Covers"/>).
    ///
    /// SUNKEN IS SHARED, THE FILL IS NOT. Every one of these is a hole in the floor, so the lip,
    /// the four inner walls and the shadow they cast belong here; what is at the bottom of the
    /// hole is the subclass's whole job. Building the recess three times would be three chances
    /// for one pit to read as a decal while the other two read as holes.
    /// </summary>
    public abstract class FloorPit : MonoBehaviour
    {
        /// <summary>
        /// EVERY CACHED FIELD ON A PIT IS [SerializeField], AND THAT IS THE DOMAIN-RELOAD RULE
        /// THIS FILE ALREADY STATES ELSEWHERE, NOT A PREFERENCE. A script edit during play reloads
        /// the domain, and a private field with no attribute is not written back - while every
        /// GameObject the pit built survives untouched.
        ///
        /// This was not theoretical: it was hit during this pass, twice over. FirePit threw
        /// "_bed has not been assigned" once a frame for the rest of the session, which looks
        /// exactly like a stale error from the reload and is permanent until the object is
        /// rebuilt - the same shape TransmutationCircle and TerminalStation already record. And
        /// the quieter one is here: <see cref="Footprint"/> is what every damage and slow check is
        /// measured against, so losing it would reset the rect to zero and silently move the
        /// hazard's real area to the world origin while the art stayed where it was drawn.
        /// Nothing on screen would say so.
        /// </summary>
        [SerializeField] PitShape _shape;

        /// <summary>The pit's shape - what every damage, slow and route test is measured against.</summary>
        public PitShape Shape => _shape;

        /// <summary>The cells' world rect - a bound, never the test itself (ask <see cref="Contains"/>).</summary>
        public Rect Footprint => _shape != null ? _shape.Bounds : default;

        /// <summary>Textures and sprites baked for this pit alone, freed with it. Not readonly: a
        /// domain reload brings it back null (the textures then outlive it in the Editor only).</summary>
        protected List<Object> Owned = new();

        [SerializeField] protected Transform Player;
        [SerializeField] protected Health PlayerHealth;

        /// <summary>
        /// The player's body, for hazards that measure MOVEMENT rather than position.
        ///
        /// Kept beside PlayerHealth because it is the same object resolved at the same moment, and
        /// because the distinction it exists to preserve is easy to lose: Player.position is the
        /// RENDER pose, which the player's interpolation setting is free to change, while
        /// rb.position is the physics pose and is the only one that advances exactly once per
        /// step. See SpikePit for what reading the wrong one silently cost.
        /// </summary>
        [SerializeField] protected Rigidbody2D PlayerBody;

        /// <summary>
        /// Layer orders, all relative to <see cref="SortingOrders.PitBase"/> so the whole band
        /// moves as one if it is ever rebased.
        /// </summary>
        protected const int OrderFloor = SortingOrders.PitBase;       // the bottom of the hole
        protected const int OrderMaterial = SortingOrders.PitBase + 1; // what fills it
        protected const int OrderDetail = SortingOrders.PitBase + 2;   // features on the fill
        protected const int OrderWalls = SortingOrders.PitBase + 3;    // the recess's inner walls
        protected const int OrderLip = SortingOrders.PitBase + 4;      // the broken floor edge
        protected const int OrderAbove = SortingOrders.PitBase + 5;    // flames, jacks - still under every body

        /// <summary>
        /// The floor's damage scale on whatever this pit does to the player - set when it is placed
        /// (HazardBuilder), so a pit hurts on floor 80 the way an enemy does, not the way it did on
        /// floor 1. Serialized and non-readonly, like every cached field here (see Footprint).
        /// </summary>
        [SerializeField] protected float DamageScale = 1f;

        public FloorPit ScaleDamage(float scale) { DamageScale = Mathf.Max(0f, scale); return this; }

        protected void Init(PitShape shape, Transform player)
        {
            _shape = shape;
            Player = player;
            PlayerHealth = player != null ? player.GetComponent<Health>() : null;
            PlayerBody = player != null ? player.GetComponent<Rigidbody2D>() : null;
            transform.position = shape.Bounds.center;
            FloorPits.Register(this);
        }

        void OnDestroy()
        {
            FloorPits.Unregister(this);
            if (Owned != null) foreach (var o in Owned) if (o != null) Destroy(o);
        }

        /// <summary>One material layer, baked to the shape (PitArt.Shaped).</summary>
        protected SpriteRenderer Layer(PitArt.Pattern pattern, Color color, int order, string name)
            => PitArt.Shaped(gameObject, _shape, pattern, color, order, name, Owned ??= new List<Object>());

        /// <summary>A point inside the pit at least <paramref name="margin"/> in from its edge, in
        /// the pit's LOCAL space - for flames, jacks and ripples. Falls back to the centre.</summary>
        protected Vector2 PointInside(float margin, System.Func<float> rand01)
        {
            var b = _shape.Bounds;
            for (int i = 0; i < 24; i++)
            {
                var p = new Vector2(Mathf.Lerp(b.xMin, b.xMax, rand01()), Mathf.Lerp(b.yMin, b.yMax, rand01()));
                if (_shape.Sample(p) > margin && !_shape.PastSpill(p, out _)) return p - (Vector2)transform.position;
            }
            return Vector2.zero;
        }

        /// <summary>
        /// Is the player standing in this pit? Measured against the footprint shrunk by
        /// <see cref="Tuning.Hazards.PitStandInset"/>, so clipping the very edge of a hole is not
        /// the same as being in it - a hazard that fires on the outermost texel of its own art
        /// reads as having a bigger hitbox than it is drawn with, which is the exact complaint
        /// this project's own swept-capsule note records against the old attack cone.
        /// </summary>
        public bool ContainsPlayer()
        {
            if (Player == null) return false;
            return Contains(Player.position);
        }

        public bool Contains(Vector2 p) => _shape != null && _shape.Contains(p);

        /// <summary>Within <paramref name="pad"/> of the pit - a body's whole circle, a route's cell.</summary>
        public bool Covers(Vector2 p, float pad) => _shape != null && _shape.Covers(p, pad);

        /// <summary>Which of the four kinds this pit is - what the element trap boons ask.</summary>
        public abstract PitKind Kind { get; }

        /// <summary>Whether standing here hurts right now - a fire pit while it burns, spikes
        /// always. What Salamander's stack II asks of "a burning pit".</summary>
        public virtual bool HurtsNow => false;

        /// <summary>Movement multiplier this pit imposes on a body standing in it. 1 = no effect.</summary>
        public virtual float SpeedMultiplier => 1f;

        /// <summary>
        /// The most of the gap between a body's velocity and the one it wants that one physics
        /// step may close here. 1 = no limit (dry ground's own blend applies). Water lowers it,
        /// which is what a slide IS: the same speed, but no say over where it goes.
        /// </summary>
        public virtual float Grip => 1f;

        /// <summary>Whether standing here SOAKS the player - more damage taken from everything,
        /// held a while after stepping out (Tuning.Hazards.WaterSoak*). Water only.</summary>
        public virtual bool Soaks => false;

        /// <summary>Extra cost per unit an enemy's route pays to cross this pit (0 = plain
        /// floor) - see Enemies.NavField. Read every route recompute, so a fire's can follow its
        /// cycle.</summary>
        public virtual float RouteCost => 0f;

        protected bool PlayerAlive => Player != null && PlayerHealth != null && !PlayerHealth.IsDead;

        // ---- the recess ----

        /// <summary>
        /// The four inner walls and the lip, lit from above so the FAR wall (a top edge, which a
        /// top-down camera sees the inside of) is the one in shadow and the NEAR wall catches the
        /// light - getting those the wrong way round is what turns a hole into a plateau. Followed
        /// round the shape's whole outline (PitArt.Recess); drawn as falls rather than bands,
        /// because a hard dark stripe inside an outline is a painted border.
        /// </summary>
        protected void BuildRecess(float depthScale = 1f)
            => PitArt.Recess(gameObject, _shape, depthScale, OrderWalls, Owned ??= new List<Object>());
    }

    /// <summary>
    /// Every live pit, and the ONE place the terrain's effect on the player's movement is
    /// answered.
    ///
    /// The multiplier is asked FOR rather than written TO the player, deliberately. Two sand pits
    /// cannot overlap on the grid, but a pit writing a field on PlayerController would still need
    /// somebody to write it back on the frame the player steps out - and whoever does that last
    /// wins, which is exactly the flicker <c>Controls.Screen</c>'s own "one owner" note records
    /// from two Updates writing one field. Querying has no such frame.
    ///
    /// NOT readonly and self-healing, for the documented reason: a static collection is not
    /// restored across a domain reload while every pit GameObject survives it, so a script edit
    /// mid-run would empty this list and sand would silently stop slowing the player with nothing
    /// on screen saying so. <see cref="All"/> rebuilds from the scene when it finds itself empty on
    /// its first query after a reload (once: a floor with no pits stays empty).
    /// </summary>
    public static class FloorPits
    {
        static List<FloorPit> _pits = new();

        /// <summary>
        /// The pits the PLAYER is immune to - the element trap boons (Salamander, Gnome, Undine) -
        /// asked by every player-side query here and by the pits' own damage. Set by the run, a
        /// delegate so the hazards know nothing about the exchange; null is no immunity.
        /// </summary>
        public static System.Func<PitKind, bool> PlayerImmune;

        /// <summary>How hard pits bite the player now - the exchange's Hazard Bloom and Dead Ground.
        /// Asked by the pits that hurt; null is the plain bite.</summary>
        public static System.Func<float> PlayerDamageMul;
        public static float PlayerBite => PlayerDamageMul != null ? PlayerDamageMul() : 1f;

        static bool Ignores(FloorPit pit) => PlayerImmune != null && PlayerImmune(pit.Kind);

        public static void Register(FloorPit pit)
        {
            _pits ??= new List<FloorPit>();
            if (!_pits.Contains(pit)) _pits.Add(pit);
        }

        public static void Unregister(FloorPit pit) => _pits?.Remove(pit);

        /// <summary>
        /// Emptied explicitly when a floor is rebuilt rather than left to each pit's OnDestroy.
        /// <c>Destroy</c> is deferred to end of frame, so the outgoing floor's pits would
        /// otherwise still be answering queries for the rest of the frame the NEW floor is built
        /// on - a player who spawned inside last floor's sand would be slowed by a hazard that no
        /// longer exists.
        /// </summary>
        public static void Clear() => _pits?.Clear();

        static List<FloorPit> All()
        {
            _pits ??= new List<FloorPit>();
            // once only: a floor with no pits is empty every frame, and the scene search was paid on every query
            if (!_scanned && _pits.Count == 0)
            {
                var found = Object.FindObjectsByType<FloorPit>();
                if (found.Length > 0) _pits.AddRange(found);
            }
            _scanned = true;
            return _pits;
        }

        /// <summary>Whether the scene has been searched since the last domain reload (pits built after it register).</summary>
        static bool _scanned;

        /// <summary>Every live pit - for the enemy nav grid, which lays each footprint onto its
        /// cells rather than asking point by point.</summary>
        public static IReadOnlyList<FloorPit> Live => All();

        /// <summary>The pit an ENEMY standing at <paramref name="p"/> is in, or null. Pits never
        /// overlap on the grid, so the first one found is the only one.</summary>
        public static FloorPit At(Vector2 p)
        {
            var pits = All();
            for (int i = pits.Count - 1; i >= 0; i--)
            {
                var pit = pits[i];
                if (pit == null) { pits.RemoveAt(i); continue; }
                if (pit.Contains(p)) return pit;
            }
            return null;
        }

        /// <summary>
        /// The enemy side of <see cref="SpeedMultiplierAt"/>: sand slows enemies exactly as it
        /// slows the player, and a TURNED mire slows them too. Same "slowest wins" rule. Kept
        /// separate because the mire's colour decides which side it slows.
        /// </summary>
        public static float EnemySpeedMultiplierAt(Vector2 p)
        {
            float m = MireField.EnemySpeedAt(p);
            var pit = At(p);
            return pit != null ? Mathf.Min(m, pit.SpeedMultiplier) : m;
        }

        /// <summary>
        /// How much say a body at <paramref name="p"/> has over its own velocity this step - see
        /// <see cref="FloorPit.Grip"/>. The slipperiest ground under it wins, the speed rule's
        /// "ground cannot be layered". Asked by the player AND by enemies.
        /// </summary>
        public static float GripAt(Vector2 p)
        {
            var pits = All();
            float g = 1f;
            for (int i = pits.Count - 1; i >= 0; i--)
            {
                var pit = pits[i];
                if (pit == null) { pits.RemoveAt(i); continue; }
                if (!pit.Contains(p)) continue;
                g = Mathf.Min(g, pit.Grip);
            }
            return g;
        }

        /// <summary>
        /// The body damping that keeps a body's TOP SPEED on ground of this <paramref name="grip"/>
        /// exactly what it is on dry ground, for a body steered by <paramref name="blend"/> per
        /// step against <paramref name="dryDamping"/>.
        ///
        /// Water must change steering and nothing else. Capping the blend alone leaves the dry
        /// damping bleeding speed the blend can no longer restore - the player waded at ~30% -
        /// and scaling the damping down by grip overshoots the other way (enemies, steering at
        /// 0.1-0.3, crossed water ~30% faster than floor). So it is solved: physics applies
        /// v *= 1/(1 + dt*d) after the blend, the steady state of blend-then-damp is
        /// e = b*f / (1 - (1-b)*f), and this is the f (hence d) giving the same e at the capped
        /// blend.
        /// </summary>
        public static float DampingFor(float grip, float blend, float dryDamping)
        {
            if (grip >= blend) return dryDamping;
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return dryDamping;
            float fd = 1f / (1f + dt * dryDamping);
            float e = blend * fd / (1f - (1f - blend) * fd);
            float f = e / (grip + e * (1f - grip));
            return Mathf.Max(0f, (1f / f - 1f) / dt);
        }

        /// <summary>
        /// The slowest thing standing at this point wins, rather than the multipliers being
        /// multiplied together: they are a property of the GROUND, and ground cannot be layered.
        /// Multiplying would also mean two hazards that each halve speed stopping the player dead,
        /// which is a stun, not a slow.
        /// </summary>
        /// <summary>Whether ground at <paramref name="p"/> soaks the player standing on it - see
        /// <see cref="FloorPit.Soaks"/>. Asked, like speed and grip, never written onto the body.</summary>
        public static bool SoaksAt(Vector2 p)
        {
            var pits = All();
            for (int i = pits.Count - 1; i >= 0; i--)
            {
                var pit = pits[i];
                if (pit == null) { pits.RemoveAt(i); continue; }
                if (pit.Soaks && pit.Contains(p) && !Ignores(pit)) return true;
            }
            return false;
        }

        /// <summary><see cref="GripAt"/> for the PLAYER: ground the player is immune to (Undine's
        /// water) keeps its full grip.</summary>
        public static float PlayerGripAt(Vector2 p)
        {
            var pits = All();
            float g = 1f;
            for (int i = pits.Count - 1; i >= 0; i--)
            {
                var pit = pits[i];
                if (pit == null) { pits.RemoveAt(i); continue; }
                if (!pit.Contains(p) || Ignores(pit)) continue;
                g = Mathf.Min(g, pit.Grip);
            }
            return g;
        }

        public static float SpeedMultiplierAt(Vector2 p)
        {
            var pits = All();
            // An elite Turret's mire is ground too, and the same "slowest wins" rule applies.
            float m = MireField.PlayerSpeedAt(p);
            for (int i = pits.Count - 1; i >= 0; i--)
            {
                var pit = pits[i];
                if (pit == null) { pits.RemoveAt(i); continue; }
                if (!pit.Contains(p) || Ignores(pit)) continue;
                m = Mathf.Min(m, pit.SpeedMultiplier);
            }
            return m;
        }
    }
}
