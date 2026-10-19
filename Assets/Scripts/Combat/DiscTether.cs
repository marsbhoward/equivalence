using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The Tether art's disc (Tuning.Tether; the user's calls, 2026-10-15). One disc leaves the
    /// main hand - no line drawn to it (the user's call) - curves out round one side of the
    /// target and on PAST it, then
    /// hooks back and takes it from behind:
    ///
    ///   an ordinary enemy      hit once, then DRAGGED to just in front of the player
    ///   an elite (Anchored)    hit once and FLINCHED, armour or not - it is not moved
    ///   a boss                 hit once
    ///   no target              the disc goes out along the facing and comes home - spent
    ///
    /// Every other enemy the disc passes on the way out is TAGGED: a small hit each, the swing's
    /// falloff in order. The drag moves the body itself (MovePosition each physics step, the
    /// Grappling Hook's reel) and marks it shoved, so walls stop it and a chasm on the way takes
    /// it. Scaled time throughout.
    /// </summary>
    public class DiscTether : MonoBehaviour
    {
        enum Phase { Out, Hook, Drag, Back }

        Player.PlayerController _pc;
        Phase _phase;
        Health _target;
        Rigidbody2D _targetRb;
        Vector2 _from, _ctrl, _past, _pos, _dir;
        float _t, _outSeconds, _clock;
        bool _movable;

        Action<Health> _onHit;
        Action<Health, int> _onTag;
        Action<Health> _onAnchored;
        Action _onFinished;
        bool _finished;
        int _tagged;
        HashSet<Health> _touched = new();   // non-readonly - domain reload

        SpriteRenderer _disc;
        Color _tint;
        float _spin;

        static readonly Collider2D[] _overlap = new Collider2D[16];

        public static DiscTether Throw(Player.PlayerController pc, Health target, Vector2 facing, Color tint,
                                       Action<Health> onHit, Action<Health, int> onTag,
                                       Action<Health> onAnchored, Action onFinished)
        {
            var go = new GameObject("disc.tether");
            var t = go.AddComponent<DiscTether>();
            t._pc = pc;
            t._tint = tint;
            t._onHit = onHit;
            t._onTag = onTag;
            t._onAnchored = onAnchored;
            t._onFinished = onFinished;
            t._dir = facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector2.right;
            t._from = t._pos = t.Hand();

            Vector2 at;
            if (target != null && !target.IsDead)
            {
                t._target = target;
                at = target.transform.position;
                var to = at - (Vector2)pc.transform.position;
                if (to.sqrMagnitude > 1e-6f) t._dir = to.normalized;
            }
            else at = (Vector2)pc.transform.position + t._dir * Tuning.Tether.Reach;

            // Out round the side away from the camera's bottom edge (it reads either way): the
            // control point bows the curve out beside the target, and it ends PAST it.
            var perp = new Vector2(-t._dir.y, t._dir.x);
            t._past = at + t._dir * Tuning.Tether.PastBy;
            t._ctrl = Vector2.Lerp(t._from, at, 0.7f) + perp * Tuning.Tether.Bow;
            float length = Vector2.Distance(t._from, t._ctrl) + Vector2.Distance(t._ctrl, t._past);
            t._outSeconds = Mathf.Max(0.12f, length / Tuning.Tether.Speed);
            t._phase = Phase.Out;

            // The thrown disc is the hand's own, and the hand is empty while it is out.
            var discGo = new GameObject("disc");
            discGo.transform.SetParent(go.transform, false);
            t._disc = DiscVisual.Add(discGo);
            var rig = pc.Rig;
            Vector2 want = Vector2.one * 0.45f;
            if (rig != null && rig.TryGetDiscVisual(-1, out var sprite, out var dtint, out var size))
            {
                t._disc.sprite = sprite;
                t._disc.color = dtint;
                want = size * ThrownDisc.FlightScale;
            }
            else
            {
                t._disc.sprite = Spr.Circle;
                t._disc.color = tint;
            }
            var native = t._disc.sprite.bounds.size;
            discGo.transform.localScale = new Vector3(
                native.x > 1e-4f ? want.x / native.x : want.x,
                native.y > 1e-4f ? want.y / native.y : want.y, 1f);
            t._disc.sortingOrder = SortingOrders.Fx;
            DiscVisual.Centre(t._disc);
            rig?.SetWeaponVisible(false);

            t.Lay();
            return t;
        }

        /// <summary>The main hand, where the line starts - the chest without a rig.</summary>
        Vector2 Hand()
        {
            if (_pc == null) return _pos;
            var rig = _pc.Rig;
            if (rig != null && rig.Transform != null && rig.TryGetSwordHand(out var local))
                return rig.Transform.TransformPoint(local);
            return (Vector2)_pc.transform.position + Vector2.up * 0.4f;
        }

        void Update()
        {
            if (_finished) return;
            if (_pc == null) { Finish(); return; }
            float dt = Time.deltaTime;
            _clock += dt;
            _spin += dt * 1080f;

            switch (_phase)
            {
                case Phase.Out:
                {
                    _t += dt / _outSeconds;
                    float u = Mathf.Clamp01(_t);
                    var a = Vector2.Lerp(_from, _ctrl, u);
                    var b = Vector2.Lerp(_ctrl, _past, u);
                    var next = Vector2.Lerp(a, b, u);
                    // Walls stop a disc as they stop every shot: it comes home.
                    var sight = Hazards.HazardQuery.CheckCrossing(_pos, next);
                    if (sight == Hazards.SightResult.Blocked || sight == Hazards.SightResult.Nulled)
                    {
                        _phase = Phase.Back;
                        break;
                    }
                    _pos = next;
                    TagAlong();
                    if (u >= 1f) _phase = _target != null && !_target.IsDead ? Phase.Hook : Phase.Back;
                    break;
                }
                case Phase.Hook:
                {
                    // Back toward the player, onto the target's FAR side - the hook takes it.
                    if (_target == null || _target.IsDead) { _phase = Phase.Back; break; }
                    var behind = Behind();
                    var to = behind - _pos;
                    float step = Tuning.Tether.Speed * dt;
                    if (to.magnitude <= step) { _pos = behind; Catch(); }
                    else _pos += to.normalized * step;
                    break;
                }
                case Phase.Drag:
                    if (_target != null) _pos = Behind();
                    break;
                case Phase.Back:
                {
                    var hand = Hand();
                    var to = hand - _pos;
                    float step = Tuning.Tether.ReturnSpeed * dt;
                    if (to.magnitude <= step) { Finish(); return; }
                    _pos += to.normalized * step;
                    break;
                }
            }
            Lay();
        }

        /// <summary>The point on the target's far side from the player the disc holds it by.</summary>
        Vector2 Behind()
        {
            Vector2 at = _targetRb != null ? _targetRb.position : (Vector2)_target.transform.position;
            var away = at - (Vector2)_pc.transform.position;
            if (away.sqrMagnitude < 1e-6f) away = _dir;
            return at + away.normalized * Tuning.Tether.HoldBehind;
        }

        void TagAlong()
        {
            int n = Physics2D.OverlapCircleNonAlloc(_pos, Tuning.Tether.TagRadius, _overlap);
            for (int i = 0; i < n; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;
                var hp = col.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead || hp == _target || _touched.Contains(hp)) continue;
                if (col.GetComponentInParent<Enemies.EnemyController>() == null
                    && col.GetComponentInParent<Bosses.Boss>() == null) continue;
                _touched.Add(hp);
                _onTag?.Invoke(hp, _tagged++);
            }
        }

        void Catch()
        {
            _onHit?.Invoke(_target);
            if (_target == null || _target.IsDead) { _phase = Phase.Back; return; }

            var rb = _target.GetComponent<Rigidbody2D>();
            _movable = !_target.Anchored && rb != null && rb.bodyType == RigidbodyType2D.Dynamic
                       && _target.GetComponentInParent<Bosses.Boss>() == null;
            if (!_movable)
            {
                // Too heavy to move (the user's call): it FLINCHES instead.
                if (_target.GetComponentInParent<Bosses.Boss>() == null) _onAnchored?.Invoke(_target);
                _phase = Phase.Back;
                return;
            }
            _targetRb = rb;
            _phase = Phase.Drag;
            _clock = 0f;
        }

        void FixedUpdate()
        {
            if (_finished || _phase != Phase.Drag) return;
            if (_target == null || _target.IsDead || _targetRb == null
                || (_target.TryGetComponent<Enemies.EnemyController>(out var ec) && ec.FellIn)
                || _clock > Tuning.Tether.MaxDragSeconds)
            {
                _phase = Phase.Back;
                return;
            }

            Vector2 player = _pc.transform.position;
            Vector2 at = _targetRb.position;
            var from = at - player;
            float dist = from.magnitude;
            float left = dist - Tuning.Tether.DragStopAt;
            if (left <= 0.02f || Arena.WallBetween(at, player)) { _phase = Phase.Back; return; }

            float move = Mathf.Min(left, Tuning.Tether.DragSpeed * Time.fixedDeltaTime);
            _targetRb.linearVelocity = Vector2.zero;
            _targetRb.MovePosition(at - from / dist * move);
            _target.MarkShoved();   // a chasm on the way takes it, as a throw does
        }

        void Finish()
        {
            if (_finished) return;
            _finished = true;
            _pc?.Rig?.SetWeaponVisible(true);
            _onFinished?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_finished) return;
            _finished = true;
            _pc?.Rig?.SetWeaponVisible(true);
            _onFinished?.Invoke();
        }

        // ---- the picture: the spinning disc ----

        void Lay()
        {
            _disc.transform.parent.position = _pos;
            _disc.transform.parent.rotation = Quaternion.Euler(0f, 0f, _spin);
        }
    }
}
