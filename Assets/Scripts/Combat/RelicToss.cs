using Convergence.Core;
using UnityEngine;
using T = Convergence.Core.Tuning.PrimaMateria;

namespace Convergence.Combat
{
    /// <summary>
    /// The relic thrown up into the air - the Prima Materia art's flourish while the bow comes home. A
    /// copy of the worn relic's sprite leaves the hip, rises over the head and hangs there, turning
    /// slowly and burning (its Secret Fire marks, a soft light behind it), until <see cref="Land"/>
    /// drops it back onto the hip, where the real one is shown again.
    ///
    /// The worn layer is never touched beyond its colour's alpha (hidden while its copy is up, put
    /// back exactly) - the rig swaps that layer's sprite for flashes, stone and facing. Self-
    /// cleaning: if the wearer is gone, the copy goes and nothing is left hidden.
    /// </summary>
    public class RelicToss : MonoBehaviour
    {
        SpriteRenderer _relic, _copy, _halo;
        Transform _wearer;
        Color _relicWas;
        float _t, _fallT = -1f, _pulse, _spin;
        bool _knocked;
        Vector3 _from, _hipScale;
        Vector2 _target;

        /// <summary>
        /// Throw the worn relic up to EYE LEVEL, HALFWAY between the wearer and
        /// <paramref name="target"/> - right in the volley's line, so every shot passes through it
        /// (the user's calls).
        /// </summary>
        public static RelicToss Toss(SpriteRenderer relic, Transform wearer, Vector2 target)
        {
            if (relic == null || relic.sprite == null || wearer == null) return null;
            var go = new GameObject("relic.toss");
            var r = go.AddComponent<RelicToss>();
            r._relic = relic;
            r._wearer = wearer;
            r._target = target;
            r._relicWas = relic.color;
            r._from = relic.bounds.center;
            go.transform.position = r._from;
            var s = relic.transform.lossyScale;
            r._hipScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 1f);
            go.transform.localScale = r._hipScale;

            r._copy = go.AddComponent<SpriteRenderer>();
            r._copy.sprite = relic.sprite;
            r._copy.sharedMaterial = relic.sharedMaterial;
            r._copy.sortingOrder = SortingOrders.Fx - 2;
            // Up in the air it burns FULLY - the moment the volley passes through it.
            var marks = Art.Gear.KindledMarks.On(r._copy);
            marks.ForceAlpha = 1f;
            marks.ForceHeat = 0.5f;

            var halo = new GameObject("relic.toss.halo");
            halo.transform.SetParent(go.transform, false);
            halo.transform.localPosition = new Vector3(0f, 0f, 0.002f);
            r._halo = halo.AddComponent<SpriteRenderer>();
            r._halo.sprite = Spr.Glow;
            r._halo.sortingOrder = SortingOrders.Fx - 3;

            var c = relic.color; c.a = 0f; relic.color = c;
            return r;
        }

        /// <summary>Drop it back onto the hip.</summary>
        public void Land()
        {
            if (_fallT < 0f) { _fallT = 0f; _from = transform.position; }
        }

        /// <summary>
        /// The LAST shot strikes it and knocks it back to the wearer (the user's call): it flies
        /// home spinning, fast off the hit and slowing onto the hip, shrinking back to its worn size.
        /// </summary>
        public void KnockBack()
        {
            if (_knocked) return;
            _knocked = true;
            _fallT = 0f;
            _from = transform.position;
            Spr.Flash(transform.position, 0.45f, Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 0), 0.14f);
        }


        Vector3 Apex => Vector3.Lerp(_wearer.position, (Vector3)_target, T.RelicBetween) + new Vector3(0f, T.RelicApex, 0f);

        /// <summary>Where it hangs now - the point every shot of the volley flies through.</summary>
        public Vector2 Point => transform.position;

        /// <summary>A shot has just gone through it: it flares.</summary>
        public void Pulse()
        {
            _pulse = 1f;
            Spr.Flash(transform.position, 0.3f, Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 0), 0.12f);
        }

        void Update()
        {
            if (_wearer == null || _relic == null) { Destroy(gameObject); return; }
            float dt = Time.deltaTime;
            var tone = Art.Gear.SecretFire.Tone(Art.Gear.SecretFire.Shown, 1);

            if (_fallT < 0f)
            {
                _t += dt;
                float k = Mathf.Clamp01(_t / T.RelicRiseSeconds);
                float e = 1f - (1f - k) * (1f - k);   // thrown: fast off the hand, slowing to the top
                var hip = _relic.bounds.center;
                var at = Vector3.Lerp(hip, Apex, e);
                // A small bob once it hangs - it is held up by the moment, not set down in the air.
                if (k >= 1f) at.y += Mathf.Sin((_t - T.RelicRiseSeconds) * 5f) * 0.04f;
                transform.position = at;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 3f) * 18f);
                _pulse = Mathf.Max(0f, _pulse - dt / 0.08f);
                transform.localScale = _hipScale * Mathf.Lerp(1f, T.RelicAirScale, e) * (1f + 0.25f * _pulse);
                _halo.color = new Color(tone.r, tone.g, tone.b, Mathf.Min(1f, 0.55f * e + 0.4f * _pulse));
                _halo.transform.localScale = Vector3.one * (0.9f / Mathf.Max(0.01f, transform.localScale.x));
            }
            else
            {
                _fallT += dt;
                float k = Mathf.Clamp01(_fallT / (_knocked ? T.RelicKnockSeconds : T.RelicFallSeconds));
                // Knocked: fast off the hit, slowing onto the hip, turning as it goes. Dropped:
                // slow off the top, fast onto the hip.
                float e = _knocked ? 1f - (1f - k) * (1f - k) : k * k;
                transform.position = Vector3.Lerp(_from, _relic.bounds.center, e);
                if (_knocked)
                {
                    _spin += T.RelicKnockDegreesPerSecond * dt * (1f - e);
                    transform.rotation = Quaternion.Euler(0f, 0f, _spin);
                }
                else transform.rotation = Quaternion.Slerp(transform.rotation, _relic.transform.rotation, e);
                transform.localScale = _hipScale * Mathf.Lerp(T.RelicAirScale, 1f, e);
                _halo.color = new Color(tone.r, tone.g, tone.b, 0.55f * (1f - e));
                if (k >= 1f)
                {
                    Spr.Flash(_relic.bounds.center, 0.35f, tone, 0.18f);
                    Destroy(gameObject);
                }
            }
        }

        void OnDestroy()
        {
            if (_relic != null) _relic.color = _relicWas;
        }
    }
}
