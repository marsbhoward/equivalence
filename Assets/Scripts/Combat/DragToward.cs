using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// A short drag toward a point - the mastery board's Borax drawing a crowd toward the centre of
    /// an area attack. Moves the BODY (MovePosition over a few frames, so walls still stop it) and
    /// records the shove, so a body pulled over a chasm falls rather than being put back. Anchored
    /// bodies are never moved, the same rule every knockback follows.
    /// </summary>
    public class DragToward : MonoBehaviour
    {
        const float Seconds = 0.18f;
        Rigidbody2D _rb;
        Vector2 _step;
        float _left;

        public static void Pull(Health body, Vector2 centre, float distance)
        {
            if (body == null || body.IsDead || body.Anchored) return;
            var rb = body.GetComponent<Rigidbody2D>();
            if (rb == null) return;

            Vector2 to = centre - rb.position;
            float gap = to.magnitude;
            float travel = Mathf.Min(distance, Mathf.Max(0f, gap - 0.6f));   // never through the centre
            if (travel <= 0.01f) return;

            var d = body.GetComponent<DragToward>() ?? body.gameObject.AddComponent<DragToward>();
            d._rb = rb;
            d._step = to / gap * travel / Seconds;
            d._left = Seconds;
            body.MarkShoved();
        }

        /// <summary>
        /// The same slide the other way: <paramref name="body"/> thrown <paramref name="distance"/>
        /// straight away from <paramref name="from"/> - the exchange's Tin Ward, Updraft and
        /// Phoenix. A shove, so a chasm reads it as one; an Anchored elite is moved by nothing.
        /// </summary>
        public static void Push(Health body, Vector2 from, float distance)
        {
            if (body == null || body.IsDead || body.Anchored || distance <= 0f) return;
            var rb = body.GetComponent<Rigidbody2D>();
            if (rb == null) return;

            Vector2 away = rb.position - from;
            if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle.normalized;

            var d = body.GetComponent<DragToward>() ?? body.gameObject.AddComponent<DragToward>();
            d._rb = rb;
            d._step = away.normalized * distance / Seconds;
            d._left = Seconds;
            body.MarkShoved();
        }

        void FixedUpdate()
        {
            if (_rb == null || _left <= 0f) { enabled = false; Destroy(this); return; }
            float dt = Mathf.Min(Time.fixedDeltaTime, _left);
            _rb.MovePosition(_rb.position + _step * dt);
            _left -= dt;
        }
    }
}
