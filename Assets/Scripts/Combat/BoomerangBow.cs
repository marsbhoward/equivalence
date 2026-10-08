using System.Collections.Generic;
using Convergence.Core;
using Convergence.Hazards;
using UnityEngine;
using T = Convergence.Core.Tuning.PrimaMateria;

namespace Convergence.Combat
{
    /// <summary>
    /// The bow thrown like a boomerang - the Prima Materia art's first beat (PlayerController.
    /// PrimaMateriaStrike). It leaves the hand spinning, curves out to the locked target (homing on
    /// it while it flies, so it arrives where the target IS), and curves back the other way to the
    /// thrower's hand, wherever they are by then.
    ///
    /// It strikes ONE body - the first it touches on the way out - and reports it
    /// (<c>onStrike</c>); the thrower decides what that hit is worth and starts bullet time from it.
    /// It comes home whether or not it struck anything (<c>onCaught</c>) - a weapon that could fail
    /// to come back would strand the player disarmed. Walls turn it round, as they turn the thrown
    /// blade.
    ///
    /// The ACTUAL bow (ThrownBlade's rule): the held weapon's sprite and tint, its Secret Fire
    /// marks burning in flight. SCALED time, so it slows with bullet time on the way back.
    /// </summary>
    public class BoomerangBow : MonoBehaviour
    {
        GameObject _owner;
        Health _target;
        Vector2 _start, _end, _turn;
        float _side, _t, _len, _age;
        bool _returning, _done;
        System.Action<Health> _onStrike;
        System.Action _onCaught;
        Health _struck;
        SpriteRenderer _sr, _ring;
        float _bowAlpha = 1f;
        float _trail;
        Color _trailTint;

        /// <summary>A held weapon is a few dozen pixels; in flight it has to read across the arena.</summary>
        const float FlightScale = 1.0f;

        public static BoomerangBow Launch(GameObject owner, Health target, Vector2 facing, float range,
                                          Sprite sprite, Color tint, Vector2 size,
                                          System.Action<Health> onStrike, System.Action onCaught)
        {
            var go = new GameObject("boomerang.bow");
            var b = go.AddComponent<BoomerangBow>();
            b._owner = owner;
            b._target = target != null && !target.IsDead ? target : null;
            b._start = owner.transform.position;
            facing = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector2.right;
            b._end = b._target != null ? (Vector2)b._target.transform.position : b._start + facing * range;
            // Curves out to the side the thrower's arm is on, and home on the other.
            b._side = facing.x >= 0f ? 1f : -1f;
            b._onStrike = onStrike;
            b._onCaught = onCaught;
            go.transform.position = b._start;

            b._sr = go.AddComponent<SpriteRenderer>();
            bool has = sprite != null;
            b._sr.sprite = has ? sprite : Spr.Capsule;
            b._sr.color = has ? tint : ElementInfo.Tint(Art.Gear.SecretFire.Shown);
            b._sr.sortingOrder = SortingOrders.Fx - 1;
            var want = has ? size * FlightScale : new Vector2(0.25f, 1.1f);
            var native = b._sr.sprite.bounds.size;
            go.transform.localScale = new Vector3(native.x > 0.0001f ? want.x / native.x : want.x,
                                                  native.y > 0.0001f ? want.y / native.y : want.y, 1f);
            b._trailTint = Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 1);

            // FULLY LIT in flight, whatever the beat (the user's call) - hot, not merely on.
            var marks = Art.Gear.KindledMarks.On(b._sr);
            marks.ForceAlpha = 1f;
            marks.ForceHeat = 0.85f;
            // Spun this fast it reads as a CIRCLE OF LIGHT: the ring is the picture, the bow a
            // shape glimpsed inside it.
            b._bowAlpha = b._sr.color.a;
            var ring = new GameObject("boomerang.ring");
            b._ring = ring.AddComponent<SpriteRenderer>();
            b._ring.sprite = Ring(Art.Gear.SecretFire.Shown, Mathf.Max(want.x, want.y) * T.RingDiameterShare);
            b._ring.sortingOrder = SortingOrders.Fx;
            b._ring.color = new Color(1f, 1f, 1f, 0f);
            ring.transform.position = go.transform.position;
            return b;
        }

        /// <summary>A quadratic curve from <paramref name="a"/> to <paramref name="c"/>, bowed
        /// sideways by <see cref="Tuning.PrimaMateria.CurveFraction"/> of its length.</summary>
        Vector2 Curve(Vector2 a, Vector2 c, float t, float side)
        {
            var d = c - a;
            var perp = new Vector2(-d.y, d.x) * (side * T.CurveFraction);
            var mid = (a + c) * 0.5f + perp;
            float u = 1f - t;
            return u * u * a + 2f * u * t * mid + t * t * c;
        }

        void Update()
        {
            if (_done) return;
            if (_owner == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            _age += dt;
            // It leaves the hand AS A BOW and only then spins up into the circle of light.
            float spun = Mathf.SmoothStep(0f, 1f, _age / T.SpinUpSeconds);
            transform.Rotate(0f, 0f, T.SpinDegreesPerSecond * spun * dt);
            var bc = _sr.color; bc.a = _bowAlpha * Mathf.Lerp(1f, T.BowInRingAlpha, spun); _sr.color = bc;
            _ring.color = new Color(1f, 1f, 1f, spun);

            Vector2 prev = transform.position, next;
            if (!_returning)
            {
                // Homes on a living target: the curve's far end follows it.
                if (_target != null && !_target.IsDead) _end = _target.transform.position;
                _len = Mathf.Max(0.5f, Vector2.Distance(_start, _end) * 1.15f);
                _t = Mathf.Min(1f, _t + T.ThrowSpeed * dt / _len);
                next = Curve(_start, _end, _t, _side);

                var crossing = HazardQuery.CheckCrossing(prev, next);
                var clamped = Arena.Clamp(next, 0.3f);
                bool wall = crossing == SightResult.Blocked || crossing == SightResult.Nulled
                            || Vector2.Distance(clamped, next) > 0.001f;
                if (wall) next = prev;
                transform.position = next;

                if (_struck == null) Sweep();
                if (wall || _t >= 1f) TurnBack();
            }
            else
            {
                Vector2 hand = _owner.transform.position;
                _len = Mathf.Max(0.5f, Vector2.Distance(_turn, hand) * 1.15f);
                _t = Mathf.Min(1f, _t + T.ReturnSpeed * dt / _len);
                next = Curve(_turn, hand, _t, _side);
                transform.position = next;
                if (_t >= 1f || Vector2.Distance(next, hand) < 0.25f || _age > 8f) { Catch(); return; }
            }

            // The ring is unparented (the bow's scale is non-uniform) and turns on its own.
            _ring.transform.position = transform.position;
            _ring.transform.Rotate(0f, 0f, T.SpinDegreesPerSecond * spun * dt);

            // A short trail of light - fading copies of the ring, never dust - once it spins.
            _trail -= dt;
            if (_trail <= 0f && spun > 0.6f)
            {
                _trail = T.TrailEvery;
                var go = new GameObject("boomerang.trail");
                go.transform.SetPositionAndRotation(_ring.transform.position, _ring.transform.rotation);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _ring.sprite;
                sr.sortingOrder = _ring.sortingOrder - 1;
                sr.color = new Color(1f, 1f, 1f, T.TrailAlpha);
                var fade = go.AddComponent<FxFade>();
                fade.Life = T.TrailLife;
                fade.Growth = -0.3f;   // shrinks as it fades - the trail tapers behind
            }
        }

        // ---------------------------------------------------------------- the circle of light

        static readonly Dictionary<(ElementType, int), Sprite> _rings = new();

        /// <summary>
        /// The spinning bow as light: a ring <paramref name="diameter"/> across, three texels thick,
        /// at the body's density, with two comet streaks chasing round it (the bow's two tips,
        /// smeared) - searing at their heads, through the element's core and tint to its ember, and
        /// clear behind. Cached per element and size.
        /// </summary>
        static Sprite Ring(ElementType element, float diameter)
        {
            const float ppu = 37.5f;
            int r = Mathf.Max(6, Mathf.RoundToInt(diameter * 0.5f * ppu));
            if (_rings.TryGetValue((element, r), out var cached) && cached != null) return cached;

            var core = Art.Gear.SecretFire.Tone(element, 0);
            var vein = Art.Gear.SecretFire.Tone(element, 1);
            var ember = Art.Gear.SecretFire.Tone(element, 2);
            var sear = Color.Lerp(core, Color.white, 0.6f);
            int size = r * 2 + 4;
            var px = new Color[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (Mathf.Abs(d - r) > 1.5f) continue;
                float a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f);
                float f = Mathf.Repeat(-a * 2f, 1f);           // 0 at a head, rising behind it
                float b = Mathf.Pow(1f - f, 1.6f);
                if (Mathf.Abs(d - r) > 0.75f) b *= 0.55f;       // the ring's rim softer than its line
                px[y * size + x] = b > 0.8f ? sear : b > 0.5f ? core : b > 0.25f ? vein : b > 0.08f ? ember : Color.clear;
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(px);
            tex.Apply();
            var made = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
            made.name = "boomerang.ring." + element;
            _rings[(element, r)] = made;
            return made;
        }

        void Sweep()
        {
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, T.HitRadius))
            {
                if (col == null || col.gameObject == _owner) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                // Only bodies that fight - a column or a cracked wall is not a target to stop time on.
                if (hp.GetComponent<Enemies.EnemyController>() == null && hp.GetComponent<Bosses.Boss>() == null) continue;
                _struck = hp;
                _onStrike?.Invoke(hp);
                TurnBack();   // the hit is the turn - it comes straight back from the body it struck
                return;
            }
        }

        void TurnBack()
        {
            if (_returning) return;
            _returning = true;
            _turn = transform.position;
            _t = 0f;
            _side = -_side;   // home on the other side of the line - the boomerang's loop
            Spr.Flash(transform.position, 0.55f, _trailTint, 0.2f);
        }

        void Catch()
        {
            if (_done) return;
            _done = true;
            if (_ring != null) Destroy(_ring.gameObject);
            _onCaught?.Invoke();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // Every exit hands the bow back - a weapon left out is a disarmed player.
            if (_ring != null) Destroy(_ring.gameObject);
            if (!_done) { _done = true; _onCaught?.Invoke(); }
        }
    }
}
