using UnityEngine;
using Convergence.Core;

namespace Convergence.Enemies
{
    /// <summary>
    /// The Brimstone Heart's shell coming apart as it bursts (the user's call): its plates fly
    /// loose from where it stood, spinning, slowing and fading. Purely a picture - nothing here
    /// touches anything. Its own GameObject, so it outlives the body it came from.
    /// </summary>
    public class ShellBurst : MonoBehaviour
    {
        const int Pieces = 5;
        const float Seconds = 0.6f, SpeedMin = 3f, SpeedMax = 5f;

        Transform[] _pieces;
        SpriteRenderer[] _srs;
        Vector2[] _vel;
        float[] _spin;
        float _age;

        public static void Spawn(Vector2 at, Sprite plate, Transform parent)
        {
            if (plate == null) return;
            var go = new GameObject("bomb.shell.burst");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = at;
            var b = go.AddComponent<ShellBurst>();
            b._pieces = new Transform[Pieces];
            b._srs = new SpriteRenderer[Pieces];
            b._vel = new Vector2[Pieces];
            b._spin = new float[Pieces];
            for (int i = 0; i < Pieces; i++)
            {
                var p = new GameObject("plate");
                p.transform.SetParent(go.transform, false);
                float a = (i / (float)Pieces) * Mathf.PI * 2f + Random.value * 0.5f;
                p.transform.rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
                var sr = p.AddComponent<SpriteRenderer>();
                sr.sprite = plate;
                sr.sortingOrder = SortingOrders.Fx - 1;
                b._pieces[i] = p.transform;
                b._srs[i] = sr;
                b._vel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(SpeedMin, SpeedMax);
                b._spin[i] = Random.Range(-720f, 720f);
            }
        }

        void Update()
        {
            if (_pieces == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _age += dt;
            float k = Mathf.Clamp01(_age / Seconds);
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (_pieces[i] == null) continue;
                _vel[i] *= Mathf.Exp(-4f * dt);
                _pieces[i].localPosition += (Vector3)(_vel[i] * dt);
                _pieces[i].Rotate(0f, 0f, _spin[i] * dt);
                var c = _srs[i].color;
                c.a = 1f - k * k;
                _srs[i].color = c;
            }
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
