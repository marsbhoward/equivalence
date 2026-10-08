using Convergence.Core;
using UnityEngine;
using T = Convergence.Core.Tuning.MagnumOpus;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// THE MAGNUM OPUS's light - what the Secret Fire does on ONE character while it performs the
    /// reactive weapons' weapon art. For the length of the art this answers, per renderer, in place
    /// of the one beat every other mark keeps (<see cref="KindledMarks"/> asks it):
    ///
    ///     gather   every mark on the ARMOUR and the WEAPON drains out, from wherever the beat
    ///              had it, while the RELIC swells to full and runs white-hot
    ///     flash    the relic lets go: a flash leaves it and runs out through the armour at
    ///              FlashSpeed - each piece lights as the light passes through it and goes dark
    ///              again behind it - and reaches the weapon, which stays lit and keeps swelling,
    ///              hotter and trembling, until the strike
    ///     fire     the shot leaves (<see cref="Fire"/>) and the weapon's glow drains with it
    ///     recover  everything blends back onto the beat, so nothing snaps
    ///
    /// ORDER IS BY DISTANCE, NOT BY LIST. Each piece flashes when the light has travelled from the
    /// relic to that renderer's middle, measured live, so it runs up the body from the hip whatever
    /// is worn, wherever the arms are, and straight to the blade when nothing else is kindled.
    ///
    /// Past full brightness a mark burns HOT: <see cref="SecretFire.HotOverlay"/> drawn over the
    /// ordinary overlay by <c>heat</c>, and near the top SEARING toward white (SearOverlay); a soft
    /// halo sits behind the relic, and the BLADE gets no halo - lightning instead
    /// (Combat.BladeLightning), growing wilder from the moment the flash lights it to the release.
    ///
    /// On the RIG ROOT, in SCALED time (a pause screen holds the art with everything else). Every
    /// field is serialized, and the live count is static only to keep KindledMarks from looking up
    /// its parents when no art is running anywhere - a domain reload zeroes it, which only means a
    /// running art's overlays go back to the beat early.
    /// </summary>
    public class MagnumOpusGlow : MonoBehaviour
    {
        /// <summary>How many characters are performing it right now.</summary>
        public static int Live;

        [SerializeField] float _startedAt = -1f;
        [SerializeField] float _firedAt = -1f;
        [SerializeField] SpriteRenderer _weapon, _relic, _offhand;
        [SerializeField] SpriteRenderer _relicHalo;

        /// <summary>The storm round the blade while it swells (Combat.BladeLightning).</summary>
        [SerializeField] Combat.BladeLightning _lightning;
        [SerializeField] bool _flashed, _arrived, _counted;

        /// <summary>Start the art's light on <paramref name="rig"/>. Restarts one already running.</summary>
        public static MagnumOpusGlow Begin(ICharacterRig rig)
        {
            var root = rig?.Transform;
            if (root == null) return null;
            var glow = root.GetComponent<MagnumOpusGlow>();
            if (glow == null) glow = root.gameObject.AddComponent<MagnumOpusGlow>();
            glow._weapon = rig.WeaponRenderer;
            glow._relic = rig.TrinketRenderer;
            glow._offhand = rig.OffhandRenderer;   // a disc pair lights as one weapon
            glow._startedAt = Time.time;
            glow._firedAt = -1f;
            glow._flashed = glow._arrived = false;
            if (!glow._counted) { glow._counted = true; Live++; }
            return glow;
        }

        /// <summary>The shot has left the blade: the weapon drains from now.</summary>
        public void Fire()
        {
            if (_firedAt >= 0f) return;
            _firedAt = Time.time;
            if (_lightning != null) _lightning.Discharge();   // the apex, let go with the shot
        }

        public bool Running => _startedAt >= 0f;

        /// <summary>The element the light burns in - the marks' own.</summary>
        static Color Tone(int tone) => SecretFire.Tone(SecretFire.Shown, tone);

        /// <summary>When the shot left, or a stand-in if it never did (the art was cut off - a
        /// death, a statue): the light must still come back to the beat.</summary>
        float FiredAt => _firedAt >= 0f ? _firedAt
                       : Time.time - _startedAt > T.GatherSeconds + 0.5f ? _startedAt + T.GatherSeconds : -1f;

        static float Bump(float t)
        {
            if (t <= 0f) return 0f;
            if (t < T.FlashUpSeconds) return t / T.FlashUpSeconds;
            return Mathf.Clamp01(1f - (t - T.FlashUpSeconds) / T.FlashDownSeconds);
        }

        Vector3 RelicAt => _relic != null ? _relic.bounds.center : transform.position;

        /// <summary>When the flash reaches whatever is drawn at <paramref name="at"/>.</summary>
        float ArrivesAt(Vector3 at)
            => T.FlashAt + Vector2.Distance(RelicAt, at) / T.FlashSpeed;

        /// <summary>
        /// The art's answer for one renderer's marks: <paramref name="alpha"/> for the ordinary
        /// overlay and <paramref name="heat"/> (0..1) for the hot one over it. <paramref name="live"/>
        /// is what the beat says right now - what the drain starts from and the recovery returns to.
        /// </summary>
        public void Sample(SpriteRenderer src, float live, out float alpha, out float heat)
        {
            alpha = live; heat = 0f;
            if (!Running || src == null) return;

            float now = Time.time, t = now - _startedAt;
            float fired = FiredAt;
            bool isWeapon = src == _weapon || (_offhand != null && src == _offhand), isRelic = src == _relic;

            if (fired >= 0f)
            {
                float s = now - fired;
                if (isWeapon && s < T.WeaponDrainSeconds)
                {
                    alpha = heat = 1f - Mathf.SmoothStep(0f, 1f, s / T.WeaponDrainSeconds);
                    return;
                }
                // The rest went dark long before the shot (their flashes have passed); everything
                // comes back onto the beat together once the weapon is out.
                float k = Mathf.SmoothStep(0f, 1f, (s - T.WeaponDrainSeconds) / T.RecoverSeconds);
                alpha = live * k;
                return;
            }

            float drained = live * (1f - Mathf.SmoothStep(0f, 1f, t / T.DrainSeconds));

            if (isRelic)
            {
                float swell = Mathf.SmoothStep(0f, 1f, t / T.SwellSeconds);
                if (t < T.FlashAt)
                {
                    alpha = Mathf.Max(live, swell);   // the stone only gathers - it never dims first
                    heat = Mathf.SmoothStep(0f, 1f, (t - T.SwellSeconds * 0.4f) / (T.FlashAt - T.SwellSeconds * 0.4f));
                }
                else
                {
                    alpha = heat = 1f - Mathf.SmoothStep(0f, 1f, (t - T.FlashAt) / T.RelicFadeSeconds);
                }
                return;
            }

            if (isWeapon)
            {
                // Dark until the flash has been through the armour; then lit by it and kept lit,
                // swelling hotter to the strike with a tremble in it.
                float arrives = T.WeaponLitAt;
                if (t < arrives)
                {
                    float up = Mathf.Clamp01((t - (arrives - T.FlashUpSeconds)) / T.FlashUpSeconds);
                    alpha = Mathf.Max(drained, up);
                    heat = up;
                    return;
                }
                float into = Mathf.Clamp01((t - arrives) / Mathf.Max(0.05f, T.GatherSeconds - arrives));
                alpha = 1f;
                heat = Mathf.Clamp01(Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, into))
                                     + Mathf.Sin(now * 47f) * 0.08f * into);
                return;
            }

            // Armour: drained, lit once as the light passes through it.
            float b = Bump(t - ArrivesAt(src.bounds.center));
            alpha = Mathf.Max(drained, b);
            heat = b * 0.7f;
        }

        void LateUpdate()
        {
            if (!Running) return;
            float now = Time.time, t = now - _startedAt;
            float fired = FiredAt;

            if (fired >= 0f && now - fired > T.WeaponDrainSeconds + T.RecoverSeconds)
            {
                End();
                return;
            }

            // The moments worth a ring of their own: the relic letting go, the light reaching the blade.
            if (!_flashed && t >= T.FlashAt)
            {
                _flashed = true;
                if (_relic != null) Spr.Pulse(_relic.transform, 0.45f, WithAlpha(Tone(0), 0.9f), 0.26f, true, 1.6f);
            }
            if (!_arrived && _weapon != null && t >= T.WeaponLitAt)
            {
                _arrived = true;
                Spr.Pulse(_weapon.transform, 0.6f, WithAlpha(Tone(1), 0.8f), 0.24f, true, 1.4f);
            }

            float relicHeat = 0f;
            if (_relic != null && _relic.enabled) Sample(_relic, 0f, out _, out relicHeat);
            _relicHalo = Halo(_relicHalo, _relic, relicHeat, 0.6f, 2.4f);

            // No glow round the blade: its own marks sear (KindledMarks) and lightning grows round
            // it from the moment the flash lights it, wilder and wilder to the release.
            if (_weapon != null && fired < 0f && t >= T.WeaponLitAt)
            {
                if (_lightning == null) _lightning = Combat.BladeLightning.On(_weapon);
                float into = Mathf.Clamp01((t - T.WeaponLitAt) / Mathf.Max(0.05f, T.GatherSeconds - T.WeaponLitAt));
                _lightning.Intensity = Mathf.Lerp(0.3f, 1f, into * into);
            }
            else if (_lightning != null) _lightning.Intensity = 0f;
        }

        /// <summary>A soft light BEHIND a glowing renderer, riding its transform (the blade's swing
        /// carries it), sized off the sprite's own local bounds.</summary>
        static SpriteRenderer Halo(SpriteRenderer halo, SpriteRenderer over, float heat, float peakAlpha, float grow)
        {
            if (over == null || heat <= 0.01f || over.sprite == null)
            {
                if (halo != null) halo.enabled = false;
                return halo;
            }
            if (halo == null || halo.transform.parent != over.transform)
            {
                if (halo != null) Destroy(halo.gameObject);
                var go = new GameObject("opus.halo");
                go.transform.SetParent(over.transform, false);
                halo = go.AddComponent<SpriteRenderer>();
                halo.sprite = Spr.Glow;
            }
            var b = over.sprite.bounds;
            var g = Spr.Glow.bounds.size;
            halo.transform.localPosition = new Vector3(b.center.x, b.center.y, 0.002f);   // just behind
            halo.transform.localScale = new Vector3(b.size.x * grow / g.x, b.size.y * grow / g.y, 1f);
            halo.sortingLayerID = over.sortingLayerID;
            halo.sortingOrder = over.sortingOrder;
            halo.color = WithAlpha(Tone(1), peakAlpha * heat);
            halo.enabled = over.enabled && !over.forceRenderingOff;
            return halo;
        }

        static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        void End()
        {
            _startedAt = -1f;
            _firedAt = -1f;
            if (_relicHalo != null) _relicHalo.enabled = false;
            if (_lightning != null) _lightning.Intensity = 0f;
            // Backstop for an art cut off mid-move: the arms go back to their own aim limit.
            GetComponent<ICharacterRig>()?.SetAimRange(0f);
            if (_counted) { _counted = false; Live = Mathf.Max(0, Live - 1); }
        }

        void OnDisable()
        {
            if (Running) End();
        }
    }
}
