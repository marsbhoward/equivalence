using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using T = Convergence.Core.Tuning.ReleaseFx;

namespace Convergence.Player
{
    /// <summary>
    /// GUST's window (the user's pick, 2026-10-25 - "slipstream"; it replaced a yellow ring held
    /// round the body): pale yellow streaks stream off the body, opposite the way it is going,
    /// for as long as Gust runs, opening with a puff of air at the feet. Yellow stays Gust's
    /// colour - the tell the player plans round. Streaks come thick while moving and thin to a
    /// few standing still. Its last seconds blink (AirResource toggles it), which here reads as
    /// the wind sputtering out.
    ///
    /// Streaks are kept in WORLD space, so the body runs on past them. One texture on the
    /// floor's grid round the feet, sorted one under the rig's lowest layer: nothing is drawn
    /// over the player.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class Slipstream : MonoBehaviour
    {
        const float Ppu = Art.FloorArt.PixelsPerUnit;
        const float HalfExtent = 1.6f, BodyWidth = 0.5f, BodyHeight = 0.95f, Drift = 1.2f;
        const int Capacity = 64;

        struct Streak { public Vector2 At, Dir; public float Born; public int Len; }

        [SerializeField] Transform _owner;
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] bool _on;
        [SerializeField] float _shownAt = -99f, _carry;
        [SerializeField] Vector2 _lastDir = Vector2.left;
        [SerializeField] int _serial;
        Streak[] _streaks;
        int _count;
        Texture2D _tex;
        Color32[] _px;
        int _half;

        public static Slipstream Attach(Transform owner)
        {
            var existing = owner.GetComponentInChildren<Slipstream>(true);
            if (existing != null) return existing;
            var go = new GameObject("air.slipstream");
            go.transform.SetParent(owner, false);
            var s = go.AddComponent<Slipstream>();
            s._owner = owner;
            s._sr = go.AddComponent<SpriteRenderer>();
            s._sr.enabled = false;
            return s;
        }

        public void SetShown(bool on)
        {
            if (on && !_on && Time.time - _shownAt > T.GustPuffSeconds) _shownAt = Time.time;
            _on = on;
        }

        public void Toggle() => _on = !_on;

        void LateUpdate()
        {
            if (_owner == null) { Destroy(gameObject); return; }
            _streaks ??= new Streak[Capacity];
            float now = Time.time, dt = Time.deltaTime;

            // Which way the air streams: against the body's travel.
            var rb = _owner.GetComponent<Rigidbody2D>();
            Vector2 v = rb != null ? rb.linearVelocity : Vector2.zero;
            float speed = v.magnitude;
            if (speed > 0.2f) _lastDir = -v / speed;
            var feet = (Vector2)Shadow().position;

            if (_on)
            {
                float rate = Mathf.Lerp(T.GustStreaksStill, T.GustStreaksMoving, Mathf.Clamp01(speed / Tuning.Player.MoveSpeed));
                _carry += rate * dt;
                while (_carry >= 1f) { _carry -= 1f; Spawn(feet, now); }
            }

            // Drop the burned out.
            int keep = 0;
            for (int i = 0; i < _count; i++)
                if (now - _streaks[i].Born < T.GustStreakLife) _streaks[keep++] = _streaks[i];
            _count = keep;

            bool puff = now - _shownAt < T.GustPuffSeconds;
            if (_count == 0 && !puff) { _sr.enabled = false; return; }

            EnsureCanvas();
            System.Array.Clear(_px, 0, _px.Length);
            var centre = new Vector2(Mathf.Round(feet.x * Ppu), Mathf.Round(feet.y * Ppu));
            for (int i = 0; i < _count; i++)
            {
                var s = _streaks[i];
                float u = (now - s.Born) / T.GustStreakLife;
                var head = (s.At + s.Dir * Drift * (now - s.Born)) * Ppu - centre;
                for (int k = 0; k < s.Len; k++)
                {
                    var p = head + s.Dir * k;
                    var c = ReleaseCanvas.At(T.Gust, 0.1f + k / (float)s.Len * 0.5f + u * 0.3f);
                    c.a = (byte)(230f * (1f - u * u));
                    Put(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), c);
                }
            }
            if (puff)
            {
                float f = (now - _shownAt) / T.GustPuffSeconds, r = f * T.GustPuffTexels;
                int e = Mathf.CeilToInt(r) + 1;
                for (int dy = -e; dy <= e; dy++)
                    for (int dx = -e; dx <= e; dx++)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy * 4f);   // flattened onto the floor
                        if (d < r - 1.5f || d > r || FireArt.Hash(dx, dy) < 0.4f) continue;
                        var c = ReleaseCanvas.At(T.Gust, 0.2f + f * 0.4f);
                        c.a = (byte)(255f * (1f - f));
                        Put(dx, dy, c);
                    }
            }
            _tex.SetPixels32(_px);
            _tex.Apply();

            transform.position = new Vector3((centre.x - _half) / Ppu, (centre.y - _half) / Ppu, 0f);
            transform.rotation = Quaternion.identity;
            _sr.sortingOrder = ReleaseCanvas.UnderBody(_owner);
            _sr.enabled = true;
        }

        void Spawn(Vector2 feet, float now)
        {
            if (_count == Capacity) return;
            int n = _serial++;
            var at = feet + new Vector2((FireArt.Hash(n, 1) - 0.5f) * BodyWidth, 0.04f + FireArt.Hash(n, 2) * BodyHeight);
            _streaks[_count++] = new Streak
            {
                At = at, Dir = _lastDir, Born = now,
                Len = T.GustStreakMin + Mathf.FloorToInt(FireArt.Hash(n, 3) * (T.GustStreakMax - T.GustStreakMin + 1)),
            };
        }

        void EnsureCanvas()
        {
            if (_tex != null) return;
            _half = Mathf.CeilToInt(HalfExtent * Ppu);
            int size = _half * 2 + 1;
            _tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            _px = new Color32[size * size];
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, size, size), Vector2.zero, Ppu, 0, SpriteMeshType.FullRect);
        }

        void Put(int dx, int dy, Color32 c)
        {
            int x = _half + dx, y = _half + dy, size = _half * 2 + 1;
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            _px[y * size + x] = c;
        }

        [SerializeField] Transform _shadow;
        Transform Shadow() => _shadow != null ? _shadow : (_shadow = ReleaseCanvas.ShadowOf(_owner));

        void OnDestroy()
        {
            if (_sr != null && _sr.sprite != null) Destroy(_sr.sprite);
            if (_tex != null) Destroy(_tex);
        }
    }
}
