using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// A pixel-art SMEAR behind a greatsword's blade: the area the blade swept over the last few
    /// hundredths of a second, rasterised flat into one point-filtered texture at body density.
    /// Hand-animated pixel games draw the same thing as dedicated smear frames; here the swing is
    /// continuous, so the "frame" is rebuilt every frame from where the blade actually went.
    ///
    /// NOT the crescent Spr.Slice used to throw on every swing (removed - it read as a wash over
    /// the character). That was a soft, bilinear shape sized to the REACH; this covers only the
    /// blade's own path, on the texel grid, in two flat tones with hard edges - the swing's own
    /// animation drawn faster, not an effect laid over it. It also grows and shrinks with the
    /// blade's speed for free: slow frames sweep a sliver, the fast middle of a swing a fan.
    ///
    /// Basics smear the outer blade only; finishers smear most of it, for longer, in the warm
    /// finisher tones, ON TOP of the WeaponTrail they already draw - so a finisher still reads as
    /// the big one (see Tuning.Smear).
    ///
    /// The blade is measured off the weapon layer's own transform and sprite, AFTER the rig has
    /// posed it: the sprite's pivot is its GRIP, local +Y runs up the blade (WeaponTrail's own
    /// geometry). Samples are kept relative to the player, so a smear travels with a character
    /// who swings while walking rather than being left on the floor behind them.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class SwingSmear : MonoBehaviour
    {
        struct Sample
        {
            public Vector2 Grip;      // relative to the player
            public float Angle;       // of the blade, degrees, UNWRAPPED against the previous sample
            public float Length;      // world length from grip to blade top
            public float Time;
        }

        const int Capacity = 32;

        /// <summary>Half the canvas in world units: shoulder offset + a full greatsword at the
        /// arena's visual scale + a lunge, with room to spare.</summary>
        const float HalfExtent = 2.4f;

        [SerializeField] SpriteRenderer _weapon;
        [SerializeField] SpriteRenderer _sr;
        [SerializeField] Texture2D _tex;

        // Non-readonly and null-guarded: see "Domain reload traps" in CLAUDE.md.
        Sample[] _samples;
        Color32[] _px;
        int _count;
        int _size;
        float _recordUntil;
        bool _finisher;

        public static void Play(ICharacterRig rig, Transform player, float seconds, bool finisher)
        {
            if (rig == null || player == null || seconds <= 0f) return;
            var weapon = rig.WeaponRenderer;
            if (weapon == null) return;

            var s = player.GetComponentInChildren<SwingSmear>(true);
            if (s == null)
            {
                var go = new GameObject("weapon.smear");
                go.transform.SetParent(player, false);
                s = go.AddComponent<SwingSmear>();
            }

            s._weapon = weapon;
            s._finisher = finisher;
            // Cleared, never continued: between swings the arm returns toward rest, and joining
            // the last swing's end to this one's start would smear a fan across the body.
            s._count = 0;
            s._recordUntil = Time.time + seconds;
            s.enabled = true;
        }

        void EnsureCanvas()
        {
            _samples ??= new Sample[Capacity];
            int size = Mathf.CeilToInt(HalfExtent * 2f * Tuning.Smear.Ppu);
            if (_tex != null && _px != null && _size == size && _sr != null) return;

            _size = size;
            _px = new Color32[size * size];
            if (_tex == null || _tex.width != size)
            {
                _tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            _tex.SetPixels32(_px);
            _tex.Apply();

            if (_sr == null) _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = Sprite.Create(_tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                       Tuning.Smear.Ppu);
        }

        void LateUpdate()
        {
            EnsureCanvas();
            float now = Time.time;
            float window = _finisher ? Tuning.Smear.FinisherWindow : Tuning.Smear.BasicWindow;

            if (now <= _recordUntil && _weapon != null && _weapon.enabled && _weapon.sprite != null)
                Record(now);

            // Drop samples older than the window, but always keep the pair that spans its edge,
            // so a slow frame rate still has two samples to draw between.
            int keepFrom = 0;
            for (int i = 0; i < _count - 1; i++)
                if (now - _samples[i + 1].Time > window) keepFrom = i + 1;
            if (keepFrom > 0)
            {
                System.Array.Copy(_samples, keepFrom, _samples, 0, _count - keepFrom);
                _count -= keepFrom;
            }

            bool live = _count >= 2 && now - _samples[_count - 1].Time <= window;
            if (!live)
            {
                _sr.enabled = false;
                if (now > _recordUntil) { _count = 0; enabled = false; }
                return;
            }

            Clear();
            Draw(now, window);
            Upload();

            _sr.enabled = true;
            if (_weapon != null)
            {
                _sr.sharedMaterial = _weapon.sharedMaterial;
                _sr.sortingLayerID = _weapon.sortingLayerID;
                // Behind the blade, so the weapon is never drawn through its own smear.
                _sr.sortingOrder = _weapon.sortingOrder - 1;
            }
        }

        void Record(float now)
        {
            var wt = _weapon.transform;
            float top = _weapon.sprite.bounds.max.y;
            Vector2 grip = wt.TransformPoint(Vector3.zero);
            Vector2 tip = wt.TransformPoint(new Vector3(0f, top, 0f));
            var along = tip - grip;
            if (along.sqrMagnitude < 1e-6f) return;

            float angle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            if (_count > 0)
                angle = _samples[_count - 1].Angle + Mathf.DeltaAngle(_samples[_count - 1].Angle, angle);

            if (_count == Capacity)
            {
                System.Array.Copy(_samples, 1, _samples, 0, Capacity - 1);
                _count--;
            }
            _samples[_count++] = new Sample
            {
                Grip = grip - (Vector2)transform.position,
                Angle = angle,
                Length = along.magnitude,
                Time = now,
            };
        }

        void Draw(float now, float window)
        {
            float from = _finisher ? Tuning.Smear.FinisherFrom : Tuning.Smear.BasicFrom;
            float to = _finisher ? Tuning.Smear.FinisherTo : Tuning.Smear.BasicTo;
            Color32 lead = _finisher ? Tuning.Smear.FinisherLead : Tuning.Smear.BasicLead;
            Color32 tail = _finisher ? Tuning.Smear.FinisherTail : Tuning.Smear.BasicTail;

            // Oldest first, so the newest (bright) quads overwrite the tail where they overlap.
            for (int i = 0; i < _count - 1; i++)
            {
                var a = _samples[i];
                var b = _samples[i + 1];
                // One sub-step per few degrees, so a fast frame sweeps an ARC, not a chord.
                int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(b.Angle - a.Angle) / 4f), 1, 24);

                Segment(a, from, to, now, window, out var p0, out var q0);
                for (int k = 1; k <= steps; k++)
                {
                    float f = (float)k / steps;
                    var s = new Sample
                    {
                        Grip = Vector2.Lerp(a.Grip, b.Grip, f),
                        Angle = Mathf.Lerp(a.Angle, b.Angle, f),
                        Length = Mathf.Lerp(a.Length, b.Length, f),
                        Time = Mathf.Lerp(a.Time, b.Time, f),
                    };
                    Segment(s, from, to, now, window, out var p1, out var q1);

                    float age = Mathf.Clamp01((now - s.Time) / window);
                    var colour = age <= Tuning.Smear.LeadFraction ? lead : tail;
                    Triangle(p0, q0, q1, colour);
                    Triangle(p0, q1, p1, colour);
                    p0 = p1; q0 = q1;
                }
            }
        }

        /// <summary>The smeared span of the blade at sample <paramref name="s"/>, inner end
        /// <paramref name="inner"/> and outer end <paramref name="outer"/>, narrowing toward the
        /// tip as the sample ages.</summary>
        static void Segment(Sample s, float from, float to, float now, float window,
                            out Vector2 inner, out Vector2 outer)
        {
            float age = Mathf.Clamp01((now - s.Time) / window);
            float lo = Mathf.Lerp(from, to, age * Tuning.Smear.Taper);
            float rad = s.Angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            inner = s.Grip + dir * (s.Length * lo);
            outer = s.Grip + dir * (s.Length * to);
        }

        void Triangle(Vector2 a, Vector2 b, Vector2 c, Color32 colour)
        {
            float ppu = Tuning.Smear.Ppu;
            float half = _size * 0.5f;
            // World offsets to texel space; texel (i, j) has its centre at (i + 0.5, j + 0.5).
            var A = a * ppu + new Vector2(half, half);
            var B = b * ppu + new Vector2(half, half);
            var C = c * ppu + new Vector2(half, half);

            float area = (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x);
            if (Mathf.Abs(area) < 1e-4f) return;

            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.x, Mathf.Min(B.x, C.x))));
            int x1 = Mathf.Min(_size - 1, Mathf.CeilToInt(Mathf.Max(A.x, Mathf.Max(B.x, C.x))));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(A.y, Mathf.Min(B.y, C.y))));
            int y1 = Mathf.Min(_size - 1, Mathf.CeilToInt(Mathf.Max(A.y, Mathf.Max(B.y, C.y))));

            float sign = Mathf.Sign(area);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var P = new Vector2(x + 0.5f, y + 0.5f);
                    float w0 = ((B.x - A.x) * (P.y - A.y) - (B.y - A.y) * (P.x - A.x)) * sign;
                    float w1 = ((C.x - B.x) * (P.y - B.y) - (C.y - B.y) * (P.x - B.x)) * sign;
                    float w2 = ((A.x - C.x) * (P.y - C.y) - (A.y - C.y) * (P.x - C.x)) * sign;
                    if (w0 < 0f || w1 < 0f || w2 < 0f) continue;
                    _px[y * _size + x] = colour;
                }
        }

        void Clear()
        {
            System.Array.Clear(_px, 0, _px.Length);
        }

        void Upload()
        {
            _tex.SetPixels32(_px);
            _tex.Apply();
        }

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }
    }
}
