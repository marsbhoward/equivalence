using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>A wisp of vapour rising off a hanging Sublimate disc: a pale texel puff that
    /// drifts up and thins out.</summary>
    public class Wisp : MonoBehaviour
    {
        SpriteRenderer _sr;
        float _age;
        Vector2 _drift;
        Color _c;

        public static void Rise(Vector2 at, ElementType element)
        {
            var go = new GameObject("sublimate-wisp");
            go.transform.position = at;
            var w = go.AddComponent<Wisp>();
            w._sr = go.AddComponent<SpriteRenderer>();
            w._sr.sprite = Spr.Circle;
            w._sr.sortingOrder = SortingOrders.Fx - 3;
            w._c = Color.Lerp(ElementInfo.Tint(element), Color.white, 0.6f);
            w._drift = new Vector2(Random.Range(-0.15f, 0.15f), Tuning.Sublimate.WispRise);
            go.transform.localScale = Vector3.one * 0.07f;
        }

        void Update()
        {
            _age += Time.deltaTime;
            float u = _age / Tuning.Sublimate.WispSeconds;
            if (u >= 1f) { Destroy(gameObject); return; }
            transform.position += (Vector3)(_drift * Time.deltaTime);
            transform.localScale = Vector3.one * Mathf.Lerp(0.07f, 0.16f, u);
            var c = _c; c.a = 0.55f * (1f - u);
            _sr.color = c;
        }
    }
}
