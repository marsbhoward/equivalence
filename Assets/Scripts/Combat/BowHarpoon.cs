using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// HARPOON, the bow's PULL (the user's calls, 2026-10-17; WEAPON-ARTS.md): the art's line arrow
    /// trails a line from the bow. Where it sticks, the struck enemy and up to
    /// <see cref="Tuning.Harpoon.Extra"/> others within <see cref="Tuning.Harpoon.GrabRadius"/> of it
    /// are REELED in together to <see cref="Tuning.Harpoon.StopAt"/> in front of the archer - not
    /// into their face - keeping their places round it. A wall between a body and the archer snags
    /// it where it is (the Grappling Hook's rule); an Anchored elite or a boss is not moved. A miss
    /// draws the line back. Scaled time; the reel moves bodies itself (MovePosition, marked shoved,
    /// so a chasm on the way takes them).
    /// </summary>
    public class BowHarpoon : MonoBehaviour
    {
        Player.PlayerController _pc;
        ThrownArrow _arrow;
        Health _target;
        Vector2 _stuckAt;
        bool _reeling, _finished;
        float _clock;
        Action _onFinished;

        struct Held { public Health Hp; public Rigidbody2D Rb; public Vector2 Offset; public bool Snagged; }
        List<Held> _held = new();   // non-readonly - domain reload

        SpriteRenderer _line;
        static Sprite _pixel;

        /// <summary>True from the loose until the line is let go.</summary>
        public bool Live => !_finished;

        /// <summary>Whether the line caught anything.</summary>
        public bool Caught { get; private set; }

        /// <summary>Follow <paramref name="arrow"/> with the line; when it strikes,
        /// <paramref name="grab"/> names the bodies reeled with the struck one.</summary>
        public static BowHarpoon Follow(Player.PlayerController pc, ThrownArrow arrow,
                                        Func<Health, List<Health>> grab, Action onFinished)
        {
            var go = new GameObject("bow.harpoon");
            var h = go.AddComponent<BowHarpoon>();
            h._pc = pc;
            h._arrow = arrow;
            h._onFinished = onFinished;
            h.MakeLine();
            arrow.OnHit += (hp, info) =>
            {
                if (h == null || h._finished || h._reeling) return;
                h.Catch(hp, grab != null ? grab(hp) : null);
            };
            return h;
        }

        void MakeLine()
        {
            if (_pixel == null)
            {
                var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            }
            var l = new GameObject("line");
            l.transform.SetParent(transform, false);
            _line = l.AddComponent<SpriteRenderer>();
            _line.sprite = _pixel;
            _line.color = Tuning.Harpoon.LineColor;
            _line.sortingOrder = SortingOrders.Fx - 2;
        }

        void Catch(Health struck, List<Health> others)
        {
            if (struck == null || struck.IsDead) return;
            _target = struck;
            _reeling = true;
            Caught = true;
            _clock = 0f;
            _stuckAt = struck.transform.position;
            Hold(struck, Vector2.zero);
            if (others != null)
                foreach (var o in others)
                    if (o != null && o != struck && !o.IsDead)
                        Hold(o, (Vector2)o.transform.position - _stuckAt);
        }

        void Hold(Health hp, Vector2 offset)
        {
            var rb = hp.GetComponent<Rigidbody2D>();
            bool movable = !hp.Anchored && rb != null && rb.bodyType == RigidbodyType2D.Dynamic
                           && hp.GetComponentInParent<Bosses.Boss>() == null;
            if (!movable) return;
            _held.Add(new Held { Hp = hp, Rb = rb, Offset = offset });
        }

        void Update()
        {
            if (_finished) return;
            if (_pc == null) { Finish(); return; }
            _clock += Time.deltaTime;

            // Flying: the line runs to the arrow; gone without a catch, the line is let go.
            if (!_reeling)
            {
                if (_arrow == null) { Finish(); return; }
                Lay(_arrow.transform.position);
                return;
            }
            if (_target == null || _target.IsDead || _clock > Tuning.Harpoon.MaxSeconds) { Finish(); return; }
            Lay(_target.transform.position);
        }

        void FixedUpdate()
        {
            if (_finished || !_reeling || _pc == null) return;
            Vector2 archer = _pc.transform.position;
            if (_target == null) return;
            Vector2 toTarget = (Vector2)_target.transform.position - archer;
            var dir = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : Vector2.right;
            Vector2 goal = archer + dir * Tuning.Harpoon.StopAt;

            bool moving = false;
            float step = Tuning.Harpoon.DragSpeed * Time.fixedDeltaTime;
            for (int i = 0; i < _held.Count; i++)
            {
                var h = _held[i];
                if (h.Snagged || h.Hp == null || h.Hp.IsDead || h.Rb == null) continue;
                if (h.Hp.TryGetComponent<Enemies.EnemyController>(out var ec) && ec.FellIn) continue;
                Vector2 at = h.Rb.position;
                // A wall between it and the archer snags the line: it stays where it is.
                if (Arena.WallBetween(at, archer)) { h.Snagged = true; _held[i] = h; continue; }
                Vector2 want = goal + h.Offset;
                var to = want - at;
                float left = to.magnitude;
                if (left <= 0.02f) continue;
                moving = true;
                h.Rb.linearVelocity = Vector2.zero;
                h.Rb.MovePosition(at + to / left * Mathf.Min(left, step));
                h.Hp.MarkShoved();   // a chasm on the way takes it, as a throw does
            }
            if (!moving) Finish();
        }

        /// <summary>The line from the bow's grip (the chest without a rig) to <paramref name="end"/>.</summary>
        void Lay(Vector2 end)
        {
            Vector2 from = (Vector2)_pc.transform.position + Vector2.up * 0.4f;
            var bow = _pc.Rig != null ? _pc.Rig.WeaponRenderer : null;
            if (bow != null) from = bow.transform.position;
            var d = end - from;
            var t = _line.transform;
            t.position = new Vector3(from.x, from.y, 0f);
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            float thick = Tuning.Player.ArenaVisualScale / 75f;
            t.localScale = new Vector3(d.magnitude, thick, 1f);
        }

        void Finish()
        {
            if (_finished) return;
            _finished = true;
            _onFinished?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (_finished) return;
            _finished = true;
            _onFinished?.Invoke();
        }
    }
}
