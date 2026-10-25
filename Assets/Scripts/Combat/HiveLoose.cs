using Convergence.Core;
using UnityEngine;
using Gear = Convergence.Art.Gear;
using T = Convergence.Core.Tuning.HiveLoose;

namespace Convergence.Combat
{
    /// <summary>
    /// The Hive's draw and loose - every shot is one of the bow's own arrows (the user's pick from
    /// the mock-up, https://claude.ai/artifact/5kiBCNbMqZv8uWF8MR4AXN). Riding the rig's own Draw
    /// motion, limbs taking turns:
    ///
    ///   peel      over the draw's reach beat, the grip-most arrow of the limb's belly lane leaves
    ///             its place for the string hand, and the rest of the lane slides one place toward
    ///             the grip together (Gear.DemoGear.HiveFrame steps through it).
    ///   nock      through the pull it lies across the bow, nock at the hand, head at the grip.
    ///   loose     it IS the shot: the ThrownArrow the player fires is drawn as this arrow and
    ///             starts where it lay (<see cref="Release"/>).
    ///   return    where the shot ends - struck, or stopped by a wall - an arrow flies home to the
    ///             limb's empty tip place, and the bow is whole again. A shot that never left (no
    ///             target in range) goes straight home from the hand.
    ///
    /// Purely a picture: damage, timing and the projectile are the bow's ordinary ones.
    /// Late in the frame, after the rig has posed the hand and the bow. Holds no serialized
    /// state: a domain reload drops the cycles and puts the whole bow back.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class HiveLoose : MonoBehaviour
    {
        enum Phase { Idle, Peel, Nocked, Loosed, Return }

        class Cycle
        {
            public bool Lower;
            public Phase Ph;
            public Transform Bolt;
            public SpriteRenderer Sr;
            public float Start, Draw, ReturnStart, ReturnSeconds;
            public ThrownArrow Shot;
            public Vector2 Pos, Dir, From;
        }

        [SerializeField] Player.PlayerController _pc;
        Cycle[] _cycles;
        bool _nextLower;

        public static HiveLoose For(Player.PlayerController pc)
        {
            var l = pc.GetComponent<HiveLoose>();
            if (l == null) l = pc.gameObject.AddComponent<HiveLoose>();
            l._pc = pc;
            return l;
        }

        SpriteRenderer Bow => _pc != null ? _pc.Rig?.WeaponRenderer : null;

        void EnsureCycles()
        {
            if (_cycles != null && _cycles[0] != null && _cycles[1] != null) return;
            _cycles = new[] { new Cycle { Lower = false }, new Cycle { Lower = true } };
        }

        // ---------------------------------------------------------------- called by the bow

        /// <summary>A shot is being drawn over <paramref name="drawSeconds"/>. Does nothing unless the
        /// Hive itself is in the hand and showing.</summary>
        public void Draw(float drawSeconds)
        {
            EnsureCycles();
            var bow = Bow;
            if (bow == null || !Gear.DemoGear.HiveFrameState(bow.sprite, out _, out _)) return;

            bool lower = _nextLower;
            _nextLower = !_nextLower;
            var c = _cycles[lower ? 1 : 0];
            if (c.Ph != Phase.Idle) Finish(c);   // its arrow was still out: home at once

            c.Ph = Phase.Peel;
            c.Start = Time.time;
            c.Draw = Mathf.Max(0.05f, drawSeconds);
            c.Shot = null;
            SlotWorld(bow, Gear.DemoGear.HiveBellySlot(lower, 0), out c.Pos, out c.Dir);
            var go = new GameObject("hive-nocked-arrow");
            c.Bolt = go.transform;
            c.Sr = go.AddComponent<SpriteRenderer>();
            c.Sr.sprite = Gear.DemoGear.HiveArrowSprite;
            c.Sr.sortingOrder = bow.sortingOrder + 1;
            Place(c, bow, Gear.DemoGear.HiveNockScale);
        }

        /// <summary>The draw ended. <paramref name="shot"/> is the arrow the bow fired, or null if
        /// nothing was in range - then the nocked arrow just goes home.</summary>
        public void Release(ThrownArrow shot)
        {
            if (_cycles == null) return;
            Cycle c = null;
            foreach (var x in _cycles)
                if (x != null && (x.Ph == Phase.Peel || x.Ph == Phase.Nocked)) c = x;
            if (c == null) return;

            var bow = Bow;
            if (shot != null && bow != null)
            {
                // The shot is drawn as this arrow and leaves from where it lay.
                var sr = shot.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = Gear.DemoGear.HiveArrowSprite;
                    sr.color = Color.white;
                }
                float s = Mathf.Abs(bow.transform.lossyScale.y) * Gear.DemoGear.HiveNockScale;
                shot.transform.localScale = new Vector3(s, s, 1f);
                shot.transform.position = c.Pos;
                c.Shot = shot;
                c.Ph = Phase.Loosed;
                if (c.Bolt != null) c.Bolt.gameObject.SetActive(false);
                return;
            }
            BeginReturn(c, c.Pos);
        }

        /// <summary>A second arrow off the same string (a twin nock): drawn as one of the Hive's own,
        /// when the Hive is in the hand - not peeled off a limb (one arrow a shot leaves the picture).</summary>
        public void Dress(ThrownArrow shot)
        {
            var bow = Bow;
            if (shot == null || bow == null || !Gear.DemoGear.HiveFrameState(bow.sprite, out _, out _)) return;
            var sr = shot.GetComponent<SpriteRenderer>();
            if (sr != null) { sr.sprite = Gear.DemoGear.HiveArrowSprite; sr.color = Color.white; }
            float s = Mathf.Abs(bow.transform.lossyScale.y) * Gear.DemoGear.HiveNockScale;
            shot.transform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Everything home at once, the bow whole - before the swarm takes the bow apart.</summary>
        public void Cancel()
        {
            if (_cycles == null) return;
            foreach (var c in _cycles) if (c != null) Finish(c);
            ShowFrame();
        }

        // ---------------------------------------------------------------- the frame

        void LateUpdate()
        {
            if (_cycles == null) return;
            var bow = Bow;
            if (bow == null || _pc == null) { Cancel(); return; }

            foreach (var c in _cycles)
            {
                if (c == null || c.Ph == Phase.Idle) continue;
                Step(c, bow);
            }
            ShowFrame();
        }

        void Step(Cycle c, SpriteRenderer bow)
        {
            float k = (Time.time - c.Start) / c.Draw;
            switch (c.Ph)
            {
                case Phase.Peel:
                {
                    SlotWorld(bow, Gear.DemoGear.HiveBellySlot(c.Lower, 0), out var from, out var fromDir);
                    Nock(bow, out var to, out var toDir);
                    float e = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / T.PeelEnd));
                    // A little lift off the limb on the way, so it reads as taken, not slid.
                    var lift = Vector2.Perpendicular(to - from).normalized * (Mathf.Sin(e * Mathf.PI) * T.PeelArc);
                    c.Pos = Vector2.Lerp(from, to, e) + lift;
                    float a = Mathf.LerpAngle(Angle(fromDir), Angle(toDir), e) * Mathf.Deg2Rad;
                    c.Dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    float scale = Mathf.Lerp(Gear.DemoGear.HiveBellySlot(c.Lower, 0).Scale, Gear.DemoGear.HiveNockScale, e);
                    Place(c, bow, scale);
                    if (k >= T.PeelEnd) c.Ph = Phase.Nocked;
                    break;
                }
                case Phase.Nocked:
                    Nock(bow, out c.Pos, out c.Dir);
                    Place(c, bow, Gear.DemoGear.HiveNockScale);
                    // Never told of a release (a path that skipped it): send it home.
                    if (k > 1f + T.ReleaseGraceSeconds / c.Draw) BeginReturn(c, c.Pos);
                    break;

                case Phase.Loosed:
                    if (c.Shot != null) { c.Pos = c.Shot.transform.position; break; }
                    BeginReturn(c, c.Pos);   // it struck, or a wall took it: home from there
                    break;

                case Phase.Return:
                {
                    var slot = Gear.DemoGear.HiveBellySlot(c.Lower, Gear.DemoGear.HiveBellyLast);
                    SlotWorld(bow, slot, out var to, out var toDir);
                    float t = Mathf.Clamp01((Time.time - c.ReturnStart) / c.ReturnSeconds);
                    float e = Mathf.SmoothStep(0f, 1f, t);
                    // Arcs home - up and over, the way the swarm's arrows curve back.
                    var mid = (c.From + to) * 0.5f + Vector2.up * (Vector2.Distance(c.From, to) * T.ReturnArc);
                    var p = Bezier(c.From, mid, to, e);
                    var ahead = Bezier(c.From, mid, to, Mathf.Min(1f, e + 0.02f)) - p;
                    var travel = ahead.sqrMagnitude > 1e-8f ? ahead.normalized : toDir;
                    float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 1f, t));
                    c.Pos = p;
                    c.Dir = Vector2.Lerp(travel, toDir, settle).normalized;
                    c.Sr.sortingOrder = t > 0.8f ? bow.sortingOrder + 1 : SortingOrders.Fx - 1;
                    Place(c, bow, slot.Scale);
                    if (t >= 1f) Finish(c);
                    break;
                }
            }
        }

        void BeginReturn(Cycle c, Vector2 from)
        {
            var bow = Bow;
            c.Ph = Phase.Return;
            c.Shot = null;
            c.From = from;
            c.ReturnStart = Time.time;
            float dist = bow != null ? Vector2.Distance(from, bow.transform.position) : 1f;
            c.ReturnSeconds = Mathf.Clamp(dist / T.ReturnSpeed, T.ReturnMinSeconds, T.ReturnMaxSeconds);
            if (c.Bolt == null)
            {
                var go = new GameObject("hive-returning-arrow");
                c.Bolt = go.transform;
                c.Sr = go.AddComponent<SpriteRenderer>();
                c.Sr.sprite = Gear.DemoGear.HiveArrowSprite;
            }
            c.Bolt.gameObject.SetActive(true);
            c.Sr.sortingOrder = SortingOrders.Fx - 1;
        }

        void Finish(Cycle c)
        {
            if (c.Bolt != null) Destroy(c.Bolt.gameObject);
            c.Bolt = null;
            c.Sr = null;
            c.Shot = null;
            c.Ph = Phase.Idle;
        }

        /// <summary>The bow at each limb's slide state - only while it is showing one of its own
        /// frames (a flash, the stone or the swarm's bare grip own the renderer meanwhile).</summary>
        void ShowFrame()
        {
            var bow = Bow;
            if (bow == null || !Gear.DemoGear.HiveFrameState(bow.sprite, out int u, out int l)) return;
            int wantU = Slide(_cycles?[0]), wantL = Slide(_cycles?[1]);
            if (u == wantU && l == wantL) return;
            var frame = Gear.DemoGear.HiveFrame(wantU, wantL);
            if (frame != null) bow.sprite = frame;
        }

        int Slide(Cycle c)
        {
            if (c == null || c.Ph == Phase.Idle) return 0;
            if (c.Ph != Phase.Peel) return Gear.DemoGear.HiveSlideStates;
            // Taken at once (state 1), then the lane slides over the rest of the reach.
            float k = (Time.time - c.Start) / c.Draw;
            float slid = Mathf.Clamp01((k - T.SlideFrom) / (T.SlideTo - T.SlideFrom));
            return 1 + Mathf.Min(Gear.DemoGear.HiveSlideStates - 1, (int)(slid * (Gear.DemoGear.HiveSlideStates - 1) + 0.5f));
        }

        // ---------------------------------------------------------------- where things are

        static void SlotWorld(SpriteRenderer bow, (Vector2 Local, Vector2 Dir, float Scale) slot,
                              out Vector2 pos, out Vector2 dir)
        {
            pos = bow.transform.TransformPoint(slot.Local);
            dir = ((Vector2)bow.transform.TransformDirection(slot.Dir)).normalized;
        }

        /// <summary>Nocked: the nock at the string hand, the head toward the grip.</summary>
        void Nock(SpriteRenderer bow, out Vector2 pos, out Vector2 dir)
        {
            Vector2 grip = bow.transform.position;
            Vector2 hand = grip;
            var rig = _pc != null ? _pc.Rig : null;
            if (rig != null && rig.Transform != null && rig.TryGetSwordHand(out var local))
                hand = rig.Transform.TransformPoint(local);
            var along = grip - hand;
            dir = along.sqrMagnitude > 1e-6f ? along.normalized : (_pc != null ? _pc.Facing : Vector2.right);
            float half = Gear.DemoGear.HiveArrowHalfLocal * Mathf.Abs(bow.transform.lossyScale.y) * Gear.DemoGear.HiveNockScale;
            pos = hand + dir * half;
        }

        void Place(Cycle c, SpriteRenderer bow, float scale)
        {
            if (c.Bolt == null) return;
            c.Bolt.position = new Vector3(c.Pos.x, c.Pos.y, 0f);
            c.Bolt.rotation = Quaternion.Euler(0f, 0f, Angle(c.Dir));
            float s = Mathf.Abs(bow.transform.lossyScale.y) * scale;
            c.Bolt.localScale = new Vector3(s, s, 1f);
        }

        static float Angle(Vector2 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        void OnDestroy()
        {
            if (_cycles == null) return;
            foreach (var c in _cycles) if (c?.Bolt != null) Destroy(c.Bolt.gameObject);
        }
    }
}
