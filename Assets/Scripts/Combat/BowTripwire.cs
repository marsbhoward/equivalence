using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// TRIPWIRE (Tuning.Tripwire; PlayerController.TripwireStrike): the twin nock's two arrows fly
    /// out and stake into the ground either side of the aim, a wire strung taut between them. The
    /// first enemy to come within <see cref="Tuning.Tripwire.HalfWidth"/> of the wire SETS IT OFF:
    /// <c>onDetonate</c> gets the point on the wire it crossed (the blast is PlayerController's), and
    /// the posts are blown away. Unset after <see cref="Tuning.Tripwire.Seconds"/>, the wire goes
    /// slack and the posts fade. A post never stands on a wall or over a chasm - then there is no
    /// wire. Scaled time.
    /// </summary>
    public class BowTripwire : MonoBehaviour
    {
        static Sprite _pixel;

        Sprite _arrow;
        Action<Vector2> _onDetonate;
        Vector2 _a, _b, _fromA, _fromB;
        SpriteRenderer _postA, _postB, _flyA, _flyB, _wire;
        float _clock, _snapAt = -1f;
        bool _strung, _failed;

        public static BowTripwire Lay(Vector2 from, Vector2 a, Vector2 b, Sprite arrow, Action<Vector2> onDetonate)
        {
            if (_pixel == null)
            {
                var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _pixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            }
            var go = new GameObject("bow.tripwire");
            var w = go.AddComponent<BowTripwire>();
            w._arrow = arrow;
            w._onDetonate = onDetonate;
            w._a = a; w._b = b;
            w._fromA = from; w._fromB = from + Vector2.down * Tuning.Bow.TwinSpread;
            w._flyA = w.Arrow("tripwire.arrow");
            w._flyB = w.Arrow("tripwire.arrow");
            return w;
        }

        SpriteRenderer Arrow(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _arrow;
            sr.sortingOrder = SortingOrders.Fx - 2;
            float s = Tuning.Player.ArenaVisualScale;
            go.transform.localScale = new Vector3(s, s, 1f);
            return sr;
        }

        float ArrowLen => _arrow != null ? _arrow.bounds.size.x * Tuning.Player.ArenaVisualScale : 0.4f;

        static void Fly(SpriteRenderer sr, Vector2 from, Vector2 to, float t, float len)
        {
            var d = to - from;
            var dir = d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.right;
            var head = Vector2.Lerp(from, to, t);
            sr.transform.SetPositionAndRotation(head - dir * (len * 0.5f),
                Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg));
        }

        SpriteRenderer Post(Vector2 ground, float lean)
        {
            var sr = Arrow("tripwire.post");
            float len = ArrowLen;
            var go = sr.gameObject;
            go.transform.rotation = Quaternion.Euler(0f, 0f, -90f + lean);
            go.transform.position = (Vector3)(ground + (Vector2)(Quaternion.Euler(0f, 0f, lean) * Vector2.up) * (len * 0.2f));
            DepthSorted.Attach(go, -len * 0.2f, true, sr);
            SmokePuff.At(ground);
            return sr;
        }

        void Update()
        {
            _clock += Time.deltaTime;
            float fl = Tuning.Tripwire.FlightSeconds;
            if (!_strung && !_failed)
            {
                float t = Mathf.Clamp01(_clock / fl);
                Fly(_flyA, _fromA, _a, t, ArrowLen);
                Fly(_flyB, _fromB, _b, t, ArrowLen);
                if (t < 1f) return;
                Destroy(_flyA.gameObject); Destroy(_flyB.gameObject);
                if (!Arena.OnFloor(_a) || !Arena.OnFloor(_b) || Arena.WallBetween(_a, _b)) { _failed = true; Destroy(gameObject, 0.1f); return; }
                _postA = Post(_a, -10f);
                _postB = Post(_b, 10f);
                // The wire runs between the posts' tops, a little off the ground.
                var wgo = new GameObject("tripwire.wire");
                wgo.transform.SetParent(transform, false);
                _wire = wgo.AddComponent<SpriteRenderer>();
                _wire.sprite = _pixel;
                _wire.color = Tuning.Tripwire.WireColor;
                _wire.sortingOrder = SortingOrders.GroundDecal + 4;
                var lift = Vector2.up * (ArrowLen * 0.45f);
                var d = (_b + lift) - (_a + lift);
                wgo.transform.position = _a + lift;
                wgo.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                wgo.transform.localScale = new Vector3(d.magnitude, 1.5f / 37.5f, 1f);
                _strung = true;
                _clock = 0f;
                return;
            }
            if (!_strung) return;

            if (_snapAt >= 0f)
            {
                float u = (_clock - _snapAt) / Tuning.Tripwire.FadeSeconds;
                if (u >= 1f) { Destroy(gameObject); return; }
                var c = new Color(1f, 1f, 1f, 1f - u);
                _postA.color = c; _postB.color = c;
                if (_blown)
                {
                    // Thrown clear of the blast, spinning.
                    float dt = Time.deltaTime;
                    var away = (_b - _a).normalized;
                    _postA.transform.position -= (Vector3)(away * (4f * dt)) - Vector3.up * (2f * dt * (1f - u));
                    _postB.transform.position += (Vector3)(away * (4f * dt)) + Vector3.up * (2f * dt * (1f - u));
                    _postA.transform.rotation *= Quaternion.Euler(0f, 0f, 720f * dt);
                    _postB.transform.rotation *= Quaternion.Euler(0f, 0f, -720f * dt);
                }
                return;
            }
            if (_clock >= Tuning.Tripwire.Seconds) { Snap(); return; }

            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null || e.FellIn) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                if (DistanceToWire(hp.transform.position) > Tuning.Tripwire.HalfWidth) continue;
                // Set off: the blast where it was crossed, the posts thrown clear.
                var at = ClosestOnWire(hp.transform.position);
                _onDetonate?.Invoke(at);
                _blown = true;
                Snap();
                return;
            }
        }

        bool _blown;

        Vector2 ClosestOnWire(Vector2 p)
        {
            var ab = _b - _a;
            float t = Mathf.Clamp01(Vector2.Dot(p - _a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return _a + ab * t;
        }

        float DistanceToWire(Vector2 p) => Vector2.Distance(p, ClosestOnWire(p));

        void Snap()
        {
            if (_snapAt >= 0f) return;
            _snapAt = _clock;
            if (_wire != null) Destroy(_wire.gameObject);
        }
    }
}
