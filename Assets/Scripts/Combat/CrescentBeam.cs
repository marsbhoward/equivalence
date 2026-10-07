using System.Collections.Generic;
using Convergence.Core;
using Convergence.Hazards;
using UnityEngine;
using T = Convergence.Core.Tuning.MagnumOpus;

namespace Convergence.Combat
{
    /// <summary>
    /// The Magnum Opus's shot: a crescent of the weapon's own light, cut loose by the big slash and
    /// sent straight out along the facing, striking every body it crosses once.
    ///
    /// IN THE WEAPON'S COLOURS - the Secret Fire's three tones for the element burning NOW
    /// (<see cref="Art.Gear.SecretFire.Shown"/>): core on the leading edge, the element's own tint
    /// through the body, ember on the trailing edge. Pixel art at the body's density
    /// (<see cref="Tuning.MagnumOpus.BeamPpu"/>), point filtered, so it sits on the character's grid
    /// instead of reading as a soft particle; a few fading after-images behind it carry the motion.
    ///
    /// DAMAGE RULES ARE THE THROWN BLADE'S: rolled once at the strike and carried, scaled per body
    /// by the board's and the ledger's target rules (<c>scaleHit</c>), each body struck once, and
    /// the thrower hears every hit (<c>onHit</c>). Unlike the blade it tapers through a crowd the
    /// way a swing does (<c>falloff</c> per body, floored), because it IS a swing's energy. Walls
    /// stop it as they stop every shot (<see cref="HazardQuery.CheckCrossing"/>): Blocked or Nulled
    /// spends it, a Red field doubles what it goes on to hit. It never comes back.
    /// </summary>
    public class CrescentBeam : MonoBehaviour
    {
        GameObject _owner;
        Vector2 _dir, _start;
        float _damage, _knockback, _falloff, _floor, _range, _damageMul = 1f, _chain = 1f;
        bool _crit, _done;
        ElementType _element, _tones;
        System.Action<Health, DamageInfo> _onHit;
        System.Func<Health, float, float> _scaleHit;

        /// <summary>Bodies the slash already struck for its share: each takes only the rest.</summary>
        ICollection<Health> _owedRest;
        float _rest = 1f;
        SpriteRenderer _sr;
        float _ghostTimer;
        readonly HashSet<Health> _struck = new();

        /// <summary>How high above the ground plane the picture flies - about the hands' height
        /// on the arena-scaled figure.</summary>
        public const float Lift = 0.45f;

        public static CrescentBeam Fire(GameObject owner, Vector2 origin, Vector2 dir, float damage, float knockback,
                                        float range, float falloff, float falloffFloor, bool crit, ElementType element,
                                        ICollection<Health> owedRest, float restFraction, Vector2 sweepFrom,
                                        System.Action<Health, DamageInfo> onHit, System.Func<Health, float, float> scaleHit)
        {
            dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            var go = new GameObject("opus.crescent");
            go.transform.position = origin;

            var b = go.AddComponent<CrescentBeam>();
            b._owner = owner;
            b._dir = dir;
            b._start = origin;
            b._damage = damage;
            b._knockback = knockback;
            b._range = range;
            b._falloff = falloff;
            b._floor = falloffFloor;
            b._crit = crit;
            b._element = element;
            b._tones = Art.Gear.SecretFire.Shown;
            b._onHit = onHit;
            b._scaleHit = scaleHit;
            b._owedRest = owedRest;
            b._rest = restFraction;

            // The hit is judged on the ground plane, where every body's root is; the picture flies
            // at the height of the blade that cut it loose.
            var pic = new GameObject("picture");
            pic.transform.SetParent(go.transform, false);
            pic.transform.localPosition = new Vector3(0f, Lift, 0f);
            pic.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            b._sr = pic.AddComponent<SpriteRenderer>();
            b._sr.sprite = Picture(b._tones);
            b._sr.sortingOrder = SortingOrders.Fx - 1;

            Spr.Flash(pic.transform.position, 1.1f, Art.Gear.SecretFire.Tone(b._tones, 0), 0.2f);
            // Bodies standing right on top of the blade are crossed before the first frame moves it.
            b.Grow(0f);
            // It leaves from the sword's TIP, but everything between the wielder and the tip is
            // crossed too - a body inside the blade's length must not slip under the crescent.
            b.Sweep(sweepFrom, origin);
            return b;
        }

        void Update()
        {
            if (_done) return;
            float dt = Time.deltaTime;
            var from = (Vector2)transform.position;
            var to = from + _dir * (T.BeamSpeed * dt);

            bool spent = false;
            switch (HazardQuery.CheckCrossing(from, to))
            {
                case SightResult.Nulled:
                case SightResult.Blocked: spent = true; break;
                case SightResult.Amplified: _damageMul = 2f; break;
            }
            if (Corpse.StopsShot(from, to, out _)) spent = true;

            var clamped = Arena.Clamp(to, 0.2f);
            if (Vector2.Distance(clamped, to) > 0.001f) spent = true;   // the outer wall
            if (!spent) Sweep(from, clamped);

            transform.position = clamped;
            float travelled = Vector2.Distance(_start, clamped);
            Grow(travelled / Mathf.Max(0.01f, _range));

            // Spent at the end of its range, fading over the last stretch rather than blinking out.
            float fadeFrom = _range * (1f - T.BeamFadeFraction);
            float a = travelled <= fadeFrom ? 1f : Mathf.Clamp01(1f - (travelled - fadeFrom) / (_range - fadeFrom));
            _sr.color = new Color(1f, 1f, 1f, a);

            _ghostTimer -= dt;
            if (_ghostTimer <= 0f && a > 0.05f)
            {
                _ghostTimer = 0.03f;
                Ghost(a);
            }

            if (spent || travelled >= _range)
            {
                _done = true;
                Spr.Flash(_sr.transform.position, 0.7f, Art.Gear.SecretFire.Tone(_tones, 1), 0.22f);
                Destroy(gameObject);
            }
        }

        /// <summary>Every body in the strip the crescent swept between two points.</summary>
        void Sweep(Vector2 from, Vector2 to)
        {
            var mid = (from + to) * 0.5f;
            float along = Vector2.Distance(from, to) + T.BeamDepth;
            float angle = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg;
            foreach (var col in Physics2D.OverlapBoxAll(mid, new Vector2(along, T.BeamSpan * _scale), angle))
            {
                if (col == null || col.gameObject == _owner) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead || !_struck.Add(hp)) continue;

                float dmg = _damage * _damageMul * _chain;
                if (_owedRest != null && _owedRest.Contains(hp)) dmg *= _rest;
                if (_scaleHit != null) dmg = _scaleHit(hp, dmg);
                _chain = Mathf.Max(_floor, _chain * _falloff);

                var info = new DamageInfo(dmg, _element, _owner)
                {
                    Knockback = _dir * _knockback,
                    // A HEAVY weapon art: it moves what it hits, along its own flight.
                    Displaces = true,
                    Thrown = true,
                    IsFinisher = true,
                    Crit = _crit,
                    Disintegrates = true,
                };
                hp.Take(info);
                // A RING, not a filled disc: the hit-stop holds this frame, and a solid disc sat over
                // the crescent for the whole freeze.
                Spr.Flash(hp.transform.position, 0.55f, Art.Gear.SecretFire.Tone(_tones, 1), 0.2f);
                _onHit?.Invoke(hp, info);
            }
        }

        /// <summary>How big the crescent is now, times its drawn size.</summary>
        float _scale = 1f;

        /// <summary>
        /// The crescent GROWS as it travels: from <see cref="Tuning.MagnumOpus.BeamGrowFrom"/> as it
        /// leaves the blade to <see cref="Tuning.MagnumOpus.BeamGrowTo"/> at the end of its range,
        /// easing out (it widens fastest just after the cut). The strip it strikes widens with it.
        /// </summary>
        void Grow(float travelled01)
        {
            float k = 1f - (1f - Mathf.Clamp01(travelled01)) * (1f - Mathf.Clamp01(travelled01));
            _scale = Mathf.Lerp(T.BeamGrowFrom, T.BeamGrowTo, k);
            if (_sr != null) _sr.transform.localScale = new Vector3(_scale, _scale, 1f);
        }

        /// <summary>An after-image left where the crescent was, fading fast.</summary>
        void Ghost(float a)
        {
            var go = new GameObject("opus.crescent.ghost");
            go.transform.SetPositionAndRotation(_sr.transform.position, _sr.transform.rotation);
            go.transform.localScale = _sr.transform.localScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sr.sprite;
            sr.sortingOrder = _sr.sortingOrder - 1;
            sr.color = new Color(1f, 1f, 1f, 0.45f * a);
            var fade = go.AddComponent<FxFade>();
            fade.Life = 0.16f;
            fade.Growth = 0f;   // it stays the crescent's size - an after-image, not a burst
        }

        // ---------------------------------------------------------------- the picture

        static readonly Dictionary<ElementType, Sprite> _sprites = new();

        /// <summary>
        /// The crescent for one element, flying along +x: ONE BRUSH STROKE (the user's reference,
        /// 2026-10-07) - an arc of a circle, convex toward the flight, full in the body and dragged
        /// out to fine points at the tips, one tip a little longer than the other. The brush shows
        /// on the texel grid: both edges ragged by a texel or two, dry-brush hairlines breaking the
        /// stroke toward the tips, a few loose hairs trailing past the outer edge. Toned by depth
        /// from the leading edge: core, then the element's tint, then ember on the trailing edge.
        /// Deterministic (hashed noise), so every crescent of an element is the same stroke.
        /// </summary>
        static Sprite Picture(ElementType element)
        {
            if (_sprites.TryGetValue(element, out var cached) && cached != null) return cached;

            float ppu = T.BeamPpu;
            float half = T.BeamArcDegrees * 0.5f * Mathf.Deg2Rad;
            float radius = T.BeamSpan * 0.5f / Mathf.Sin(half) * ppu;   // the outer edge, in texels
            float thick = T.BeamThickness * ppu;
            const int pad = 3;

            // The circle's centre sits behind the stroke; the canvas runs from the tips' inner
            // corners to the front of the arc.
            float back = radius * Mathf.Cos(half) - thick;
            int w = Mathf.CeilToInt(radius - back) + pad * 2;
            w += w & 1;
            int h = Mathf.CeilToInt(2f * radius * Mathf.Sin(half)) + pad * 2;
            h += h & 1;
            float cx = pad - back, cy = h * 0.5f;

            var core = Art.Gear.SecretFire.Tone(element, 0);
            var vein = Art.Gear.SecretFire.Tone(element, 1);
            var ember = Art.Gear.SecretFire.Tone(element, 2);

            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float u = Mathf.Atan2(dy, dx) / half;                // -1..1 tip to tip
                if (u < -1.06f || u > 1.06f) continue;

                // Fullest a little off the middle, and one tip drawn out longer than the other.
                float uu = u < 0f ? u * 0.94f : u * 1.04f;
                float body = 1f - (uu - 0.08f) * (uu - 0.08f) / 1.17f;
                if (body <= 0f) continue;
                float th = thick * Mathf.Pow(body, 0.62f);
                float arc = u * half * radius;                        // texels along the stroke

                float outer = radius + Noise(arc / 5f, 11) * 1.1f;
                float inner = radius - th + Noise(arc / 4f, 23) * 1.6f;
                float s = (outer - dist) / Mathf.Max(1f, outer - inner);   // 0 leading .. 1 trailing
                // The tones streak along the stroke rather than running in clean bands.
                float tone = s + Noise(arc / 7f, 59) * 0.12f + Noise(arc / 2.5f, 71) * 0.06f;

                if (dist > outer || dist < inner)
                {
                    // Loose hairs past the leading edge, mostly toward the tips.
                    float hair = Mathf.Abs(u) > 0.35f ? Noise(arc / 3f, 41) : -1f;
                    if (dist > outer && dist < outer + 2f && hair > 0.55f && Mathf.Abs(dist - outer - 1f) < 0.5f)
                        px[y * w + x] = Faint(vein, 0.75f);
                    continue;
                }

                // Dry-brush hairlines: thin gaps running ALONG the stroke, more of them nearer the tips.
                float tipward = Mathf.InverseLerp(0.25f, 0.95f, Mathf.Abs(u));
                bool gap = false;
                if (tipward > 0f && th > 3f)
                    foreach (var band in Bands)
                        gap |= Mathf.Abs(s - band) * th < 0.5f
                               && Noise(arc / 11f, (int)(band * 97)) > 0.7f - tipward * 0.9f;
                if (gap) continue;

                px[y * w + x] = tone < 0.25f ? core : tone < 0.62f ? vein : ember;
            }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(px);
            tex.Apply();
            // Pivot on the stroke's body, so the crescent leaves FROM the blade.
            var made = Sprite.Create(tex, new Rect(0, 0, w, h),
                                     new Vector2((cx + radius - thick * 0.5f) / w, 0.5f), T.BeamPpu);
            made.name = "opus.crescent." + element;
            _sprites[element] = made;
            return made;
        }

        /// <summary>Where across the stroke (0 leading, 1 trailing) the dry-brush gaps run.</summary>
        static readonly float[] Bands = { 0.18f, 0.47f, 0.79f };

        static Color Faint(Color c, float a) { c.a = a; return c; }

        /// <summary>Smooth hashed noise along the stroke, -1..1 - the same every time.</summary>
        static float Noise(float t, int seed)
        {
            int i = Mathf.FloorToInt(t);
            float f = t - i;
            f = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i, seed), Hash(i + 1, seed), f);
        }

        static float Hash(int i, int seed)
        {
            uint h = (uint)(i * 374761393 + seed * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFF) / 32767.5f - 1f;
        }
    }
}
