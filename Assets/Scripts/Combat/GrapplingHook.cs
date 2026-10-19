using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// The Grappling Hook art's chain (Tuning.Grapple). Thrown from the sword hand along the
    /// facing; the FIRST thing it meets decides the move:
    ///
    ///   an ordinary enemy           hit once, then REELED in to just inside sword reach
    ///   an elite, a boss            hit once, then the PLAYER is hauled to it
    ///   a wall, a pillar, anything  the PLAYER is hauled to it
    ///   solid that is not a body
    ///   nothing in Reach            the chain comes home - the art is spent
    ///
    /// Elites are Health.Anchored (moved by nothing), so the test is the one every knockback
    /// already answers to. The reel moves the body itself (MovePosition each physics step, the
    /// way DragToward does) and marks it shoved, so walls stop it and a chasm on the way takes
    /// it. The haul is the player's own Dash, so a haul that ends over a chasm is judged like any
    /// dash. No immunity either way (the user's call).
    ///
    /// The chain is drawn as links at weapon density, re-laid every frame from the hand to the
    /// hook. Scaled time throughout: a hit-stop or a pause holds it.
    /// </summary>
    public class GrapplingHook : MonoBehaviour
    {
        enum Phase { Out, Reel, Haul, Back }

        const float Ppu = 75f;
        const float LinkSpacing = 0.07f;

        Player.PlayerController _pc;
        Phase _phase;
        Vector2 _dir, _tip, _launchedFrom;
        float _clock;

        Health _caught;
        Rigidbody2D _caughtRb;
        Transform _anchorBody;   // an elite or boss the haul follows; null for a wall
        Vector2 _anchor;

        Action<Health> _onHit;
        Action _onFinished;
        bool _finished;

        SpriteRenderer _head;
        List<SpriteRenderer> _links = new();   // non-readonly - see CLAUDE.md, domain reload

        static readonly RaycastHit2D[] _hits = new RaycastHit2D[16];

        /// <summary>True from the throw until the chain is home.</summary>
        public bool Live => !_finished;

        public static GrapplingHook Throw(Player.PlayerController pc, Vector2 dir,
                                          Action<Health> onHit, Action onFinished)
        {
            var go = new GameObject("grapplinghook");
            var h = go.AddComponent<GrapplingHook>();
            h._pc = pc;
            h._dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            h._onHit = onHit;
            h._onFinished = onFinished;
            // The hook flies on the BODY's line (what every hit and every haul measures from);
            // only the drawn chain leaves the hand. From the hand, 0.4 above the feet, a haul to a
            // wall drifted that much off the aim.
            h._tip = h._launchedFrom = pc.transform.position;
            h._phase = Phase.Out;

            var head = new GameObject("hook");
            head.transform.SetParent(go.transform, false);
            h._head = head.AddComponent<SpriteRenderer>();
            h._head.sprite = HookSprite;
            h._head.sortingOrder = SortingOrders.Fx + 1;
            h.Lay();
            return h;
        }

        /// <summary>Where the chain leaves the body: the sword hand, or the chest without one.</summary>
        Vector2 Hand()
        {
            if (_pc == null) return _tip;
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

            switch (_phase)
            {
                case Phase.Out: Fly(dt); break;
                case Phase.Haul:
                    if (_anchorBody != null) _tip = _anchorBody.position;
                    else _tip = _anchor;
                    // The haul is a dash of a fixed length; once it is spent the chain lets go.
                    if (_clock >= _haulSeconds + 0.05f) BeginBack();
                    break;
                case Phase.Back:
                    var hand = Hand();
                    var to = hand - _tip;
                    float step = Tuning.Grapple.RetractSpeed * dt;
                    if (to.magnitude <= step) { Finish(); return; }
                    _tip += to.normalized * step;
                    break;
            }
            Lay();
        }

        void FixedUpdate()
        {
            if (_finished || _phase != Phase.Reel) return;

            if (_caught == null || _caught.IsDead || _caughtRb == null
                || (_caught.TryGetComponent<Enemies.EnemyController>(out var ec) && ec.FellIn)
                || _clock > Tuning.Grapple.MaxMoveSeconds)
            {
                BeginBack();
                return;
            }

            Vector2 player = _pc.transform.position;
            Vector2 at = _caughtRb.position;
            var from = at - player;
            float dist = from.magnitude;
            float left = dist - Tuning.Grapple.ReelStopAt;
            // A wall between them snags the chain: the body stops where it is.
            if (left <= 0.02f || Arena.WallBetween(at, player)) { BeginBack(); return; }

            float move = Mathf.Min(left, Tuning.Grapple.ReelSpeed * Time.fixedDeltaTime);
            _caughtRb.linearVelocity = Vector2.zero;
            _caughtRb.MovePosition(at - from / dist * move);
            _caught.MarkShoved();   // a chasm on the way takes it, as a throw does
            _tip = _caughtRb.position;
        }

        float _haulSeconds;

        void Fly(float dt)
        {
            float reach = Tuning.Grapple.Reach;
            float travelled = Vector2.Distance(_launchedFrom, _tip);
            float step = Mathf.Min(Tuning.Grapple.HookSpeed * dt, reach - travelled);
            if (step <= 0f) { BeginBack(); return; }

            var next = _tip + _dir * step;

            // A wall stops the hook, and the hook holds: the player is hauled to it. A spire's
            // bubble swallows it like any shot.
            switch (Hazards.HazardQuery.CheckCrossing(_tip, next))
            {
                case Hazards.SightResult.Nulled: BeginBack(); return;
                case Hazards.SightResult.Blocked: HaulTo(null, _tip, Tuning.Grapple.HaulStopShortWall); return;
            }

            if (Cast(_tip, step, out var hit, out var body))
            {
                _tip = hit;
                if (body != null) Catch(body);
                else HaulTo(null, hit, Tuning.Grapple.HaulStopShortWall);
                return;
            }

            _tip = next;
            if (step < Tuning.Grapple.HookSpeed * dt - 1e-4f) BeginBack();   // at full reach
        }

        /// <summary>The nearest thing along this stretch of the flight: a body (an enemy or a
        /// boss), or anything else solid. Triggers, the player and corpses are passed through.</summary>
        bool Cast(Vector2 from, float dist, out Vector2 point, out Health body)
        {
            point = default;
            body = null;
            int n = Physics2D.CircleCastNonAlloc(from, Tuning.Grapple.HookRadius, _dir, _hits, dist);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i].collider;
                if (col == null || _hits[i].distance >= best) continue;
                if (col.transform.IsChildOf(_pc.transform)) continue;
                if (col.GetComponentInParent<Corpse>() != null) continue;

                var hp = col.GetComponentInParent<Health>();
                bool isBody = hp != null && (col.GetComponentInParent<Enemies.EnemyController>() != null
                                             || col.GetComponentInParent<Bosses.Boss>() != null);
                if (isBody && hp.IsDead) continue;
                if (!isBody && col.isTrigger) continue;

                best = _hits[i].distance;
                point = from + _dir * _hits[i].distance;
                body = isBody ? hp : null;
                found = true;
            }
            return found;
        }

        void Catch(Health hp)
        {
            _onHit?.Invoke(hp);
            if (hp == null || hp.IsDead) { BeginBack(); return; }

            var rb = hp.GetComponent<Rigidbody2D>();
            bool movable = !hp.Anchored && rb != null && rb.bodyType == RigidbodyType2D.Dynamic
                           && hp.GetComponentInParent<Bosses.Boss>() == null;
            if (movable)
            {
                _caught = hp;
                _caughtRb = rb;
                _phase = Phase.Reel;
                _clock = 0f;
                return;
            }

            var col = hp.GetComponent<Collider2D>();
            float radius = col != null ? col.bounds.extents.x : 0f;
            HaulTo(hp.transform, hp.transform.position, Tuning.Grapple.HaulStopShortBody + radius);
        }

        void HaulTo(Transform body, Vector2 at, float stopShort)
        {
            _anchorBody = body;
            _anchor = at;
            _tip = at;
            _phase = Phase.Haul;
            _clock = 0f;

            Vector2 player = _pc.transform.position;
            // To a body, straight at it; to a wall, along the throw.
            var to = body != null ? at - player : _dir * Vector2.Dot(at - player, _dir);
            float distance = to.magnitude - stopShort;
            if (distance <= 0.05f) { BeginBack(); return; }
            _haulSeconds = Mathf.Min(distance / Tuning.Grapple.HaulSpeed, Tuning.Grapple.MaxMoveSeconds);
            _pc.Dash(to.normalized, Tuning.Grapple.HaulSpeed, _haulSeconds);
        }

        void BeginBack()
        {
            _phase = Phase.Back;
            _caught = null;
            _caughtRb = null;
            _anchorBody = null;
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
            // Torn down mid-flight (a run ending): still hand the gate back.
            if (!_finished) { _finished = true; _onFinished?.Invoke(); }
        }

        // ---- the picture ----

        /// <summary>Re-lay the links from the hand to the hook, alternating flat and edge-on.</summary>
        void Lay()
        {
            var hand = Hand();
            var along = _tip - hand;
            float len = along.magnitude;
            var dir = len > 1e-5f ? along / len : _dir;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            _head.transform.position = _tip;
            _head.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            int count = Mathf.FloorToInt(len / LinkSpacing);
            while (_links.Count < count)
            {
                var go = new GameObject("link");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = (_links.Count & 1) == 0 ? LinkFlat : LinkEdge;
                sr.sortingOrder = SortingOrders.Fx;
                _links.Add(sr);
            }
            for (int i = 0; i < _links.Count; i++)
            {
                var sr = _links[i];
                if (sr == null) continue;
                bool on = i < count;
                sr.enabled = on;
                if (!on) continue;
                sr.transform.position = hand + dir * ((i + 0.5f) * LinkSpacing);
                sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        static readonly Dictionary<char, Color> Steel = new()
        {
            ['h'] = new Color(0.78f, 0.80f, 0.84f),
            ['m'] = new Color(0.55f, 0.58f, 0.63f),
            ['d'] = new Color(0.26f, 0.27f, 0.31f),
        };

        static Sprite LinkFlat => Art.PixelSprite.From("grapple.link.flat", new[]
        {
            "dmmmmd",
            "h....m",
            "m....d",
            "dmmmmd",
        }, Steel, pixelsPerUnit: Ppu);

        static Sprite LinkEdge => Art.PixelSprite.From("grapple.link.edge", new[]
        {
            "......",
            "hhhhhh",
            "dddddd",
            "......",
        }, Steel, pixelsPerUnit: Ppu);

        // Points along +x, the eye at the left; two barbs swept back.
        static Sprite HookSprite => Art.PixelSprite.From("grapple.hook", new[]
        {
            "...dmh....",
            "......mh..",
            "........hd",
            "dmmmmmmhhm",
            "dmmmmmmmmd",
            "........md",
            "......md..",
            "...ddm....",
        }, Steel, outline: true, pivotTexel: new Vector2Int(0, 4), pixelsPerUnit: Ppu);
    }
}
