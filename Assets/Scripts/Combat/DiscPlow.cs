using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Plow's discs (Tuning.Plow; the user's calls, 2026-10-15): both discs bowled along the
    /// ground, ROLLING like wheels in two parallel lanes either side of the aim, each cutting a
    /// furrow behind it (GroundFurrow). Whatever a disc meets is struck once (onHit, the lane's
    /// side) - the art's caller shoves it out to that side, parting the crowd. A wall stops a disc;
    /// at the lane's end, or stopped, each flies home to the hands. Scaled time.
    /// </summary>
    public class DiscPlow : MonoBehaviour
    {
        Player.PlayerController _pc;
        Vector2 _dir, _perp;
        Action<Health, float> _onHit;   // the body, and the side (+1 / -1 along _perp) it goes to
        Action _onFinished;
        bool _finished;
        HashSet<Health> _struck = new();   // either disc strikes a body once - non-readonly

        sealed class Lane
        {
            public Transform Root, Spin;
            public SpriteRenderer Sr;
            public Vector2 Start, Pos;
            public float Side, Travelled;
            public bool Rolling = true, Home, Hidden = true;
            public GroundFurrow Furrow;
        }

        Lane[] _lanes = new Lane[2];

        static readonly Collider2D[] _overlap = new Collider2D[16];

        public static DiscPlow Bowl(Player.PlayerController pc, Vector2 aim, Action<Health, float> onHit, Action onFinished)
        {
            var go = new GameObject("disc.plow");
            var p = go.AddComponent<DiscPlow>();
            p._pc = pc;
            p._dir = aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector2.right;
            p._perp = new Vector2(-p._dir.y, p._dir.x);
            p._onHit = onHit;
            p._onFinished = onFinished;

            var rig = pc.Rig;
            Vector2 origin = (Vector2)pc.transform.position + p._dir * Tuning.Plow.StartAhead;
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? 1f : -1f;
                var root = new GameObject("lane" + i).transform;
                root.SetParent(go.transform, false);
                var spin = new GameObject("disc").transform;
                spin.SetParent(root, false);
                var sr = DiscVisual.Add(spin.gameObject);
                Vector2 want = Vector2.one * 0.45f;
                if (rig != null && rig.TryGetDiscVisual(i, out var sprite, out var tint, out var size))
                {
                    sr.sprite = sprite;
                    sr.color = tint;
                    want = size * ThrownDisc.FlightScale;
                }
                else sr.sprite = Spr.Circle;
                var native = sr.sprite.bounds.size;
                spin.localScale = new Vector3(
                    native.x > 1e-4f ? want.x / native.x : want.x,
                    native.y > 1e-4f ? want.y / native.y : want.y, 1f);
                DiscPictures.ShowCombined(sr, pc);   // both discs out at once: the combined picture
                DiscVisual.Centre(sr);
                sr.sortingOrder = SortingOrders.Fx - 1;

                // A wheel rolling along the aim: seen edge-on going up or down the screen, face-on
                // going across it.
                float across = Mathf.Abs(p._dir.x);
                root.localScale = new Vector3(Mathf.Lerp(Tuning.Plow.EdgeOnWidth, 1f, across), 1f, 1f);

                var start = origin + p._perp * side * Tuning.Plow.LaneHalf;
                var lane = new Lane { Root = root, Spin = spin, Sr = sr, Start = start, Pos = start, Side = side };
                var groundFrom = start + Vector2.down * Tuning.Plow.GroundBelow;
                var groundTo = groundFrom + p._dir * Tuning.Plow.Reach;
                lane.Furrow = GroundFurrow.Cut(groundFrom, groundTo,
                    Mathf.RoundToInt(start.x * 97f) * 31 + Mathf.RoundToInt(start.y * 89f) + i);
                p._lanes[i] = lane;
            }
            // Both hands are empty while the discs roll.
            rig?.SetWeaponVisible(false);
            rig?.SetWeaponVisible(false);
            p.Lay();
            return p;
        }

        void Update()
        {
            if (_finished) return;
            if (_pc == null) { Finish(); return; }
            float dt = Time.deltaTime;
            bool allHome = true;
            foreach (var lane in _lanes)
            {
                if (lane == null) continue;
                if (lane.Rolling) Roll(lane, dt);
                else if (!lane.Home) Return(lane, dt);
                if (!lane.Home) allHome = false;
            }
            Lay();
            if (allHome) Finish();
        }

        void Roll(Lane lane, float dt)
        {
            // A hit-stop freezes the frame (dt 0): the disc waits, it is not at the lane's end.
            if (dt <= 0f) return;
            float step = Mathf.Min(Tuning.Plow.Speed * dt, Tuning.Plow.Reach - lane.Travelled);
            var next = lane.Pos + _dir * step;
            var sight = Hazards.HazardQuery.CheckCrossing(lane.Pos, next);
            if (sight == Hazards.SightResult.Blocked || sight == Hazards.SightResult.Nulled || step <= 1e-5f)
            {
                lane.Rolling = false;
                lane.Furrow?.Reveal(lane.Travelled / Tuning.Plow.Reach, done: true);
                return;
            }
            lane.Pos = next;
            lane.Travelled += step;
            lane.Furrow?.Reveal(lane.Travelled / Tuning.Plow.Reach);

            int n = Physics2D.OverlapCircleNonAlloc(lane.Pos, Tuning.Plow.LaneRadius, _overlap);
            for (int i = 0; i < n; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;
                var hp = col.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead || _struck.Contains(hp)) continue;
                if (col.GetComponentInParent<Enemies.EnemyController>() == null
                    && col.GetComponentInParent<Bosses.Boss>() == null) continue;
                _struck.Add(hp);
                // Out to the side it already stands on, or the lane's side on the centre line.
                float side = Mathf.Sign(Vector2.Dot((Vector2)hp.transform.position - (Vector2)_pc.transform.position, _perp));
                if (Mathf.Abs(Vector2.Dot((Vector2)hp.transform.position - lane.Start, _perp)) < 0.05f || side == 0f)
                    side = lane.Side;
                _onHit?.Invoke(hp, side);
            }

            if (lane.Travelled >= Tuning.Plow.Reach - 1e-4f)
            {
                lane.Rolling = false;
                lane.Furrow?.Reveal(1f, done: true);
            }
        }

        void Return(Lane lane, float dt)
        {
            var hand = Hand();
            var to = hand - lane.Pos;
            float step = Tuning.Plow.ReturnSpeed * dt;
            if (to.magnitude <= step)
            {
                lane.Home = true;
                lane.Sr.enabled = false;
                if (lane.Hidden) { lane.Hidden = false; _pc.Rig?.SetWeaponVisible(true); }
                return;
            }
            lane.Pos += to.normalized * step;
        }

        Vector2 Hand()
        {
            var rig = _pc != null ? _pc.Rig : null;
            if (rig != null && rig.Transform != null && rig.TryGetSwordHand(out var local))
                return rig.Transform.TransformPoint(local);
            return _pc != null ? (Vector2)_pc.transform.position : Vector2.zero;
        }

        /// <summary>The discs drawn ON THE GROUND (GroundBelow under the lane, which is measured
        /// at the bodies' origins), rolling - spun by the distance covered over their radius -
        /// and lifted back to the hand on the way home.</summary>
        void Lay()
        {
            foreach (var lane in _lanes)
            {
                if (lane == null || lane.Root == null) continue;
                float lift = lane.Rolling ? 0f : Tuning.Plow.GroundBelow;
                lane.Root.position = lane.Pos + Vector2.down * (Tuning.Plow.GroundBelow - lift) + Vector2.up * Tuning.Plow.DiscRadius;
                float roll = -lane.Travelled / Mathf.Max(0.05f, Tuning.Plow.DiscRadius) * Mathf.Rad2Deg * Mathf.Sign(_dir.x == 0f ? 1f : _dir.x);
                lane.Spin.localRotation = Quaternion.Euler(0f, 0f, roll);
            }
        }

        void Finish()
        {
            if (_finished) return;
            _finished = true;
            foreach (var lane in _lanes)
            {
                if (lane == null) continue;
                if (lane.Hidden) { lane.Hidden = false; _pc?.Rig?.SetWeaponVisible(true); }
                lane.Furrow?.Reveal(lane.Travelled / Tuning.Plow.Reach, done: true);
            }
            _onFinished?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_finished) return;
            _finished = true;
            foreach (var lane in _lanes)
                if (lane != null && lane.Hidden) { lane.Hidden = false; _pc?.Rig?.SetWeaponVisible(true); }
            _onFinished?.Invoke();
        }
    }
}
