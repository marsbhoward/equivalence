using System.Collections.Generic;
using Convergence.Core;
using UnityEngine;
using T = Convergence.Core.Tuning.KingAndQueen;

namespace Convergence.Combat
{
    /// <summary>
    /// One half of the Aether Dual Discs in the King and Queen art (PlayerController.KingAndQueenStrike):
    ///
    ///   loop    thrown on a CIRCLE through the wielder and the target - out along the arc that bows
    ///           to its side (the pincer: the two halves bow opposite ways and meet on the target),
    ///           striking each body it passes once (<c>onHit</c>), then on round the rest of the
    ///           circle, crossing back to the far side, to the wielder - rising as it comes to
    ///           meet the jump (<see cref="Lift"/>) - where it is caught (<see cref="Caught"/>)
    ///   hurl    <see cref="Hurl"/>: from the hand straight down onto the target
    ///   impact  its light goes OUT: the cracks show again (the user's call)
    ///   recall  <see cref="Recall"/>: springs back to the landed wielder's hand, and is gone
    ///
    /// The real half (the hand's sprite, its Secret Fire cracks) - FULLY LIT, the light filling
    /// every crack, from the throw to the impact. Spins in flight. The hit test is on the ground
    /// plane; only the picture rises with the jump. Scaled time.
    /// </summary>
    public class PincerDisc : MonoBehaviour
    {
        enum Phase { Loop, Held, Hurl, Down, Recall }

        GameObject _owner;
        Vector2 _centre;
        float _radius, _from, _sweep, _t, _hurlT, _phaseSeconds;
        float _side;
        Phase _phase;
        Vector2 _hurlFrom, _hurlTo;
        System.Action<Health> _onHit;
        readonly HashSet<Health> _struck = new();
        SpriteRenderer _sr;
        Art.Gear.KindledMarks _marks;
        Transform _picture;
        Sprite _cracked;

        /// <summary>How high the picture is drawn above the ground point - the jump's height, as the
        /// wielder's is, so the catch happens in the hands.</summary>
        public float Lift;

        /// <summary>True once the loop has brought it home to the hands.</summary>
        public bool Caught => _phase == Phase.Held;

        /// <summary>True once a hurl has struck the ground.</summary>
        public bool Landed => _phase == Phase.Down || _phase == Phase.Recall;

        /// <param name="sprite">What flies - the SPIRIT, the head on its ring of energy.</param>
        /// <param name="cracked">What it is again on the impact - the stone disc, cracks and all.</param>
        public static PincerDisc Throw(GameObject owner, Vector2 target, float side, Sprite sprite, Color tint,
                                       Vector2 size, System.Action<Health> onHit, Sprite cracked = null)
        {
            var go = new GameObject("king_and_queen.disc");
            var d = go.AddComponent<PincerDisc>();
            d._owner = owner;
            d._side = side;
            d._onHit = onHit;
            d._cracked = cracked;

            // The circle through the wielder P and the target T whose arc P->T bows out by
            // Bulge x |PT| to this disc's side: the sagitta h of a chord d gives the radius.
            Vector2 p = owner.transform.position, t = target;
            var pt = t - p;
            float len = Mathf.Max(0.5f, pt.magnitude);
            var dir = pt / len;
            var left = new Vector2(-dir.y, dir.x) * side;
            float h = T.Bulge * len;
            d._radius = (h * h + len * len * 0.25f) / (2f * h);
            d._centre = (p + t) * 0.5f - left * (d._radius - h);
            d._from = Mathf.Atan2(p.y - d._centre.y, p.x - d._centre.x);
            float to = Mathf.Atan2(t.y - d._centre.y, t.x - d._centre.x);
            // Out along the bowed (short) arc, the way that passes the bulge; the whole loop is
            // one turn of the circle in that sense.
            float minor = Mathf.DeltaAngle(d._from * Mathf.Rad2Deg, to * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            d._sweep = Mathf.Sign(minor);
            d._phase = Phase.Loop;
            d._phaseSeconds = Mathf.Abs(minor) / (Mathf.PI * 2f);   // the share of the turn that is the way out
            go.transform.position = p;

            d._picture = new GameObject("picture").transform;
            d._picture.SetParent(go.transform, false);
            d._sr = d._picture.gameObject.AddComponent<SpriteRenderer>();
            d._sr.sprite = sprite != null ? sprite : Spr.Circle;
            d._sr.color = tint;
            d._sr.sortingOrder = SortingOrders.Fx - 1;
            var native = d._sr.sprite.bounds.size;
            d._picture.localScale = new Vector3(native.x > 0.0001f ? size.x / native.x : 1f,
                                                native.y > 0.0001f ? size.y / native.y : 1f, 1f);
            // Fully lit, the light filling the cracks, until the impact puts it out.
            d._marks = Art.Gear.KindledMarks.On(d._sr);
            d._marks.ForceAlpha = 1f;
            d._marks.ForceHeat = 1f;
            return d;
        }

        /// <summary>From the hands straight down onto <paramref name="target"/>.</summary>
        public void Hurl(Vector2 target)
        {
            if (_phase != Phase.Held) return;
            _phase = Phase.Hurl;
            _hurlT = 0f;
            _hurlFrom = transform.position;
            _hurlTo = target;
        }

        /// <summary>Back to the wielder's hand, then gone.</summary>
        public void Recall()
        {
            _phase = Phase.Recall;
            _hurlT = 0f;
            _hurlFrom = transform.position;
        }

        void Update()
        {
            if (_owner == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _picture.Rotate(0f, 0f, T.SpinDegreesPerSecond * dt * (_phase == Phase.Down ? 0f : 1f));
            Vector2 hand = _owner.transform.position;

            switch (_phase)
            {
                case Phase.Loop:
                {
                    _t += dt;
                    float outS = T.OutSeconds, backS = T.ReturnSeconds;
                    // Out over OutSeconds, back round the rest of the turn over ReturnSeconds.
                    float turn = _t <= outS
                        ? Mathf.Lerp(0f, _phaseSeconds, _t / outS)
                        : Mathf.Lerp(_phaseSeconds, 1f, Mathf.Clamp01((_t - outS) / backS));
                    float a = _from + _sweep * turn * Mathf.PI * 2f;
                    var at = _centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * _radius;
                    // The circle's end is where the wielder STOOD; ease the last of it onto where
                    // they are now, so a step mid-throw still ends in the hands.
                    float home = Mathf.Clamp01((_t - outS) / backS);
                    if (_t > outS) at = Vector2.Lerp(at, hand, home * home);
                    transform.position = at;
                    if (_t <= outS + dt) Sweep();
                    if (_t >= outS + backS) { _phase = Phase.Held; transform.position = hand; }
                    break;
                }
                case Phase.Held:
                    transform.position = hand;
                    break;
                case Phase.Hurl:
                {
                    _hurlT += dt;
                    float k = Mathf.Clamp01(_hurlT / T.HurlSeconds);
                    transform.position = Vector2.Lerp(_hurlFrom, _hurlTo, k);
                    if (k >= 1f)
                    {
                        _phase = Phase.Down;
                        // The light goes OUT on the impact: the stone comes back, the cracks show again.
                        if (_cracked != null) _sr.sprite = _cracked;
                        _marks.ForceAlpha = 0f;
                        _marks.ForceHeat = 0f;
                    }
                    break;
                }
                case Phase.Down:
                    break;
                case Phase.Recall:
                {
                    _hurlT += dt;
                    float k = Mathf.Clamp01(_hurlT / T.RecallSeconds);
                    float e = 1f - (1f - k) * (1f - k);
                    transform.position = Vector2.Lerp(_hurlFrom, hand, e);
                    if (k >= 1f) { Destroy(gameObject); return; }
                    break;
                }
            }
            _picture.localPosition = new Vector3(0f, Lift, 0f);
        }

        /// <summary>Every body the disc passes on its way out, once each.</summary>
        void Sweep()
        {
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, T.HitRadius))
            {
                if (col == null || col.gameObject == _owner) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead || !_struck.Add(hp)) continue;
                if (hp.GetComponent<Enemies.EnemyController>() == null && hp.GetComponent<Bosses.Boss>() == null) continue;
                _onHit?.Invoke(hp);
            }
        }
    }
}
