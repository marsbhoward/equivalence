using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>One streak of Gyre's draft: a thin pale line blown along <c>dir</c>, thinning out.
    /// Laid on whole texels of body density so it sits on the picture's grid.</summary>
    public class DraftStreak : MonoBehaviour
    {
        const float Ppu = 37.5f;
        SpriteRenderer _sr;
        Vector2 _dir;
        float _age;

        public static void Blow(Vector2 at, Vector2 dir)
        {
            var go = new GameObject("gyre-draft");
            go.transform.position = at;
            var s = go.AddComponent<DraftStreak>();
            s._dir = dir.normalized;
            s._sr = go.AddComponent<SpriteRenderer>();
            s._sr.sprite = Spr.Square;
            s._sr.sortingOrder = SortingOrders.Fx - 3;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(Tuning.Gyre.StreakLength, 1f / Ppu, 1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            float u = _age / Tuning.Gyre.StreakSeconds;
            if (u >= 1f) { Destroy(gameObject); return; }
            transform.position += (Vector3)(_dir * Tuning.Gyre.StreakSpeed * dt);
            var c = new Color(1f, 1f, 1f, 0.5f * Mathf.Sin(u * Mathf.PI));
            _sr.color = c;
        }
    }
}
