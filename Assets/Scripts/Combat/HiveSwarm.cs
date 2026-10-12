using System.Collections.Generic;
using Convergence.Core;
using UnityEngine;
using T = Convergence.Core.Tuning.Swarm;

namespace Convergence.Combat
{
    /// <summary>
    /// The Hive loosed - the Swarm art's whole flight (PlayerController.SwarmStrike). Every arrow
    /// the bow is made of (Art.Gear.DemoGear.HiveFlight) leaves from its own place in the picture,
    /// grip first, and the bow in the hand is swapped to its bare grip (HiveSpentSprite) until
    /// they are all back.
    ///
    ///   out      each arrow flies to the target and joins the BALL: its own circle round the
    ///            target's middle, its own radius, speed and tilt, so together they read as a
    ///            swarm rather than a ring.
    ///   strikes  every StrikeEvery one arrow leaves the ball and darts at its mark - the target,
    ///            or (alternating, while any are owed) one of the enemies near it - strikes as it
    ///            passes, and swings back into the ball on its own momentum.
    ///   home     with nothing owed (or at MaxSeconds) every arrow flies back to its place on the
    ///            bow, sits there, and when the last is home the bow is whole again.
    ///
    /// Damage is rolled by the caller ONCE at the release and handed out by share through
    /// <see cref="Strike"/> (the thrown blade's rule). The target dying passes the ball to the
    /// nearest enemy to where it stood, which takes the rest of the target's strikes.
    /// Not serialized state - a domain reload mid-flight ends it, and the grip's SpriteOverride
    /// backstop puts the bow back.
    /// </summary>
    public class HiveSwarm : MonoBehaviour
    {
        /// <summary>One strike landing: on whom, for what share of the art, whether it is the
        /// target (the main body), and whether it is the target's last - the art's flinch.</summary>
        public delegate void Strike(Health victim, float share, bool main, bool sting);

        enum State { Waiting, Out, Orbit, Dive, Home, Parked }

        class Bolt
        {
            public Transform T;
            public SpriteRenderer Sr;
            public int Slot;
            public float Scale, LeaveAt;
            public Vector2 Pos, Vel, Dir;
            public float Radius, Spin, Phase, Squash, Tilt;
            public State St;
            public Health Mark;
            public float Share;
            public bool Main;
        }

        List<Bolt> _bolts;
        Transform _bow, _owner;
        SpriteRenderer _bowSr;
        Art.Gear.SpriteOverride _spent;
        bool _fromSlots;
        Strike _strike;

        Health _target;
        Vector2 _focus;
        int _targetLeft, _pendingMain, _pickIndex;
        List<Health> _near;
        List<int> _nearLeft;
        float _falloff, _minChain;
        bool _formed, _returning, _turnNear;
        float _age, _nextStrike, _minAge, _returnedAt;

        /// <summary>True once every arrow is home and the bow is whole.</summary>
        public bool Home { get; private set; }

        public static HiveSwarm Launch(GameObject owner, SpriteRenderer bow, Health target, Vector2 facing,
                                       float falloff, float minChain, Strike strike)
        {
            var go = new GameObject("hive-swarm");
            var s = go.AddComponent<HiveSwarm>();
            s._owner = owner.transform;
            s._bowSr = bow;
            s._bow = bow != null ? bow.transform : owner.transform;
            s._strike = strike;
            s._falloff = falloff;
            s._minChain = minChain;
            s._targetLeft = T.TargetHits;
            s._near = new List<Health>();
            s._nearLeft = new List<int>();

            // With nothing locked: down the facing, and the nearest enemy there if any.
            if (target == null || target.IsDead)
            {
                s._focus = (Vector2)owner.transform.position + facing.normalized * T.ReachNoTarget;
                target = s.NearestEnemy(s._focus, T.NearRadius, null);
                s._minAge = target == null ? T.EmptySeconds + 0.35f : 0f;
            }
            s._target = target;
            if (target != null) s._focus = target.transform.position;

            s._fromSlots = bow != null && Art.Gear.DemoGear.IsHive(bow.sprite);
            if (s._fromSlots) s._spent = Art.Gear.SpriteOverride.Apply(bow, Art.Gear.DemoGear.HiveSpentSprite, 12f);
            s.Spawn(facing);
            return s;
        }

        void Spawn(Vector2 facing)
        {
            var flight = Art.Gear.DemoGear.HiveFlight;
            var sprite = Art.Gear.DemoGear.HiveArrowSprite;
            float far = 0.0001f;
            foreach (var f in flight) far = Mathf.Max(far, f.Local.magnitude);

            _bolts = new List<Bolt>(flight.Count);
            for (int i = 0; i < flight.Count; i++)
            {
                var go = new GameObject("hive-arrow");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                var b = new Bolt
                {
                    T = go.transform, Sr = sr, Slot = i, Scale = flight[i].Scale,
                    // Grip first: the bow comes apart from the hand outward.
                    LeaveAt = _fromSlots ? T.LaunchSeconds * flight[i].Local.magnitude / far
                                         : T.LaunchSeconds * i / Mathf.Max(1, flight.Count - 1),
                    Radius = Random.Range(T.OrbitRadiusMin, T.OrbitRadiusMax),
                    // Most go one way round, some the other - a swarm, not a wheel.
                    Spin = Random.Range(T.OrbitSpeedMin, T.OrbitSpeedMax) * (Random.value < 0.7f ? 1f : -1f),
                    Phase = Random.value * Mathf.PI * 2f,
                    Squash = Random.Range(0.45f, 1f),
                    Tilt = Random.value * 180f,
                    St = State.Waiting,
                };
                if (!_fromSlots) b.Dir = facing.sqrMagnitude > 0f ? facing.normalized : Vector2.right;
                PlaceAtSlot(b);
                _bolts.Add(b);
            }
        }

        // ---------------------------------------------------------------- where things are

        Vector2 Focus => _target != null && !_target.IsDead
            ? (Vector2)_target.transform.position + Vector2.up * T.BodyLift
            : _focus + Vector2.up * T.BodyLift;

        void SlotPose(Bolt b, out Vector2 pos, out Vector2 dir)
        {
            if (_bow == null) { pos = b.Pos; dir = b.Dir; return; }
            if (!_fromSlots)
            {
                pos = _bowSr != null ? (Vector2)_bowSr.bounds.center : (Vector2)_bow.position;
                dir = b.Dir.sqrMagnitude > 0f ? b.Dir : Vector2.right;
                return;
            }
            var f = Art.Gear.DemoGear.HiveFlight[b.Slot];
            pos = _bow.TransformPoint(f.Local);
            dir = ((Vector2)_bow.TransformDirection(f.Dir)).normalized;
        }

        void PlaceAtSlot(Bolt b)
        {
            SlotPose(b, out var pos, out var dir);
            b.Pos = pos;
            b.Dir = dir;
            b.Sr.sortingOrder = _bowSr != null ? _bowSr.sortingOrder : SortingOrders.Fx - 1;
            Draw(b);
        }

        void Draw(Bolt b)
        {
            b.T.position = new Vector3(b.Pos.x, b.Pos.y, 0f);
            b.T.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(b.Dir.y, b.Dir.x) * Mathf.Rad2Deg);
            var ls = _bow != null ? _bow.lossyScale : Vector3.one;
            float s = Mathf.Abs(ls.y) * b.Scale;
            b.T.localScale = new Vector3(s, s, 1f);
        }

        Vector2 OrbitPoint(Bolt b)
        {
            var e = new Vector2(Mathf.Cos(b.Phase) * b.Radius, Mathf.Sin(b.Phase) * b.Radius * b.Squash);
            return Focus + (Vector2)(Quaternion.Euler(0f, 0f, b.Tilt) * e);
        }

        void Seek(Bolt b, Vector2 to, float speed, float gain, float dt)
        {
            var want = to - b.Pos;
            float d = want.magnitude;
            var wantVel = d > 1e-4f ? want / d * Mathf.Min(speed, d * gain) : Vector2.zero;
            b.Vel = Vector2.MoveTowards(b.Vel, wantVel, T.Accel * dt);
            b.Pos += b.Vel * dt;
            if (b.Vel.sqrMagnitude > 0.25f) b.Dir = b.Vel.normalized;
        }

        // ---------------------------------------------------------------- the frame

        void Update()
        {
            if (_bolts == null || _near == null || _nearLeft == null || _owner == null) { Finish(); return; }
            float dt = Time.deltaTime;
            _age += dt;

            KeepTarget();

            bool owed = (_target != null && !_target.IsDead && _targetLeft > 0) || NearOwed();
            if (!_returning && (_age > T.MaxSeconds || (!owed && _pendingMain == 0 && !Diving() && _age > _minAge)))
            {
                _returning = true;
                _returnedAt = _age;
                foreach (var b in _bolts)
                    if (b.St != State.Parked && b.St != State.Waiting) { RefundIfDiving(b); b.St = State.Home; }
            }
            if (!_returning && _formed && _age >= _nextStrike)
            {
                SendOne();
                _nextStrike = _age + T.StrikeEvery;
            }

            bool allHome = true;
            foreach (var b in _bolts)
            {
                Step(b, dt);
                if (b.St != State.Parked && !(b.St == State.Waiting && _returning)) allHome = false;
            }
            if (allHome || (_returning && _age - _returnedAt > T.ForceHomeSeconds)) Finish();
        }

        void Step(Bolt b, float dt)
        {
            switch (b.St)
            {
                case State.Waiting:
                    PlaceAtSlot(b);
                    if (!_returning && _age >= b.LeaveAt)
                    {
                        b.St = State.Out;
                        b.Vel = b.Dir * (T.Speed * 0.5f);   // heads first, off the bow
                        b.Sr.sortingOrder = SortingOrders.Fx - 1;
                    }
                    return;

                case State.Out:
                    b.Phase += b.Spin * dt;
                    Seek(b, OrbitPoint(b), T.Speed, 1000f, dt);
                    if (Vector2.Distance(b.Pos, Focus) < T.OrbitRadiusMax + 0.3f) { b.St = State.Orbit; _formed = true; }
                    break;

                case State.Orbit:
                    b.Phase += b.Spin * dt;
                    Seek(b, OrbitPoint(b), T.Speed, 10f, dt);
                    break;

                case State.Dive:
                    if (b.Mark == null || b.Mark.IsDead) { RefundIfDiving(b); b.St = State.Orbit; break; }
                    var at = (Vector2)b.Mark.transform.position + Vector2.up * T.BodyLift;
                    Seek(b, at, T.DiveSpeed, 1000f, dt);
                    if (Vector2.Distance(b.Pos, at) <= T.HitRadius) Land(b);
                    break;

                case State.Home:
                    SlotPose(b, out var slot, out var dir);
                    // Steered from afar; close in it flies STRAIGHT onto its place - steering
                    // alone circles a moving slot (the bow sways with the walk) before it settles.
                    if (Vector2.Distance(b.Pos, slot) > T.SettleDistance) Seek(b, slot, T.ReturnSpeed, 14f, dt);
                    else
                    {
                        var to = slot - b.Pos;
                        if (to.sqrMagnitude > 1e-6f) b.Dir = Vector2.Lerp(b.Dir, to.normalized, 0.5f).normalized;
                        b.Pos = Vector2.MoveTowards(b.Pos, slot, T.ReturnSpeed * dt);
                        b.Vel = Vector2.zero;
                    }
                    if (Vector2.Distance(b.Pos, slot) < 0.02f)
                    {
                        b.St = State.Parked;
                        b.Vel = Vector2.zero;
                        PlaceAtSlot(b);
                        return;
                    }
                    break;

                case State.Parked:
                    PlaceAtSlot(b);
                    return;
            }
            Draw(b);
        }

        // ---------------------------------------------------------------- the strikes

        void SendOne()
        {
            ScanNear();

            int near = NextNear();
            bool targetOwed = _target != null && !_target.IsDead && _targetLeft > 0;
            Health victim;
            bool main;
            float share;
            if (targetOwed && (!_turnNear || near < 0))
            {
                victim = _target; main = true; share = T.TargetShare;
                _targetLeft--;
                _pendingMain++;
            }
            else if (near >= 0)
            {
                victim = _near[near]; main = false;
                share = T.NearShare * Mathf.Max(_minChain, Mathf.Pow(_falloff, near + 1));
                _nearLeft[near]--;
            }
            else return;
            _turnNear = !_turnNear;

            // The arrow in the ball nearest its mark goes - so they dart out of the near side.
            Bolt pick = null;
            float best = float.MaxValue;
            var markAt = (Vector2)victim.transform.position;
            for (int k = 0; k < _bolts.Count; k++)
            {
                var b = _bolts[(k + _pickIndex) % _bolts.Count];
                if (b.St != State.Orbit) continue;
                float d = (b.Pos - markAt).sqrMagnitude;
                if (d < best) { best = d; pick = b; }
            }
            _pickIndex = (_pickIndex + 7) % Mathf.Max(1, _bolts.Count);
            if (pick == null)
            {
                Refund(victim, main);
                return;
            }
            pick.St = State.Dive;
            pick.Mark = victim;
            pick.Main = main;
            pick.Share = share;
        }

        void Land(Bolt b)
        {
            var victim = b.Mark;
            b.Mark = null;
            b.St = State.Orbit;   // carries on through on its own momentum, then swings back in
            b.Vel *= T.DiveCarry; // ...but not far: at full speed it flew off across the room
            if (b.Main) _pendingMain--;
            bool sting = b.Main && _targetLeft == 0 && _pendingMain == 0;
            _strike?.Invoke(victim, b.Share, b.Main, sting);
        }

        bool Diving()
        {
            foreach (var b in _bolts) if (b.St == State.Dive) return true;
            return false;
        }

        void RefundIfDiving(Bolt b)
        {
            if (b.St != State.Dive) return;
            Refund(b.Mark, b.Main);
            b.Mark = null;
        }

        void Refund(Health victim, bool main)
        {
            if (main) { _targetLeft++; _pendingMain--; return; }
            int i = _near.IndexOf(victim);
            if (i >= 0) _nearLeft[i]++;
        }

        // ---------------------------------------------------------------- who is struck

        /// <summary>The target fell: the ball passes to the nearest enemy to where it stood, which
        /// takes the rest of the target's strikes.</summary>
        void KeepTarget()
        {
            if (_target != null && !_target.IsDead) { _focus = _target.transform.position; return; }
            if (_target == null && _targetLeft <= 0) return;
            if (_target != null) _focus = _target.transform.position;
            var next = NearestEnemy(_focus, T.NearRadius, null);
            _target = next;
            if (next == null) { _targetLeft = 0; return; }
            int i = _near.IndexOf(next);
            if (i >= 0) { _near.RemoveAt(i); _nearLeft.RemoveAt(i); }
        }

        void ScanNear()
        {
            if (_near.Count >= T.MaxNear) return;
            var centre = Focus;
            var found = new List<(Health hp, float d)>();
            foreach (var col in Physics2D.OverlapCircleAll(centre, T.NearRadius))
            {
                var hp = IsEnemy(col);
                if (hp == null || hp == _target || _near.Contains(hp)) continue;
                if (found.Exists(f => f.hp == hp)) continue;
                if (Arena.WallBetween(centre, hp.transform.position)) continue;
                found.Add((hp, Vector2.Distance(centre, hp.transform.position)));
            }
            found.Sort((a, b) => a.d.CompareTo(b.d));
            foreach (var f in found)
            {
                if (_near.Count >= T.MaxNear) break;
                _near.Add(f.hp);
                _nearLeft.Add(T.NearHits);
            }
        }

        /// <summary>The next near enemy still owed, round-robin, or -1.</summary>
        int NextNear()
        {
            int best = -1, most = 0;
            for (int i = 0; i < _near.Count; i++)
            {
                if (_near[i] == null || _near[i].IsDead || _nearLeft[i] <= 0) continue;
                if (_nearLeft[i] > most) { most = _nearLeft[i]; best = i; }
            }
            return best;
        }

        bool NearOwed() => NextNear() >= 0;

        Health NearestEnemy(Vector2 at, float radius, Health except)
        {
            Health best = null;
            float bestD = float.MaxValue;
            foreach (var col in Physics2D.OverlapCircleAll(at, radius))
            {
                var hp = IsEnemy(col);
                if (hp == null || hp == except) continue;
                float d = Vector2.Distance(at, hp.transform.position);
                if (d < bestD) { bestD = d; best = hp; }
            }
            return best;
        }

        static Health IsEnemy(Collider2D col)
        {
            var hp = col != null ? col.GetComponent<Health>() : null;
            if (hp == null || hp.IsDead) return null;
            if (hp.GetComponent<Enemies.EnemyController>() == null && hp.GetComponent<Bosses.Boss>() == null) return null;
            return hp;
        }

        void Finish()
        {
            if (Home) return;
            Home = true;
            if (_spent != null) _spent.Remove();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_spent != null) _spent.Remove();
        }
    }
}
