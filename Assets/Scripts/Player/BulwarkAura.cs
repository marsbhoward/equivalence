using UnityEngine;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// Bulwark's identity: the character's own outline, thickened and lit warm, for the second
    /// the flat reduction is live.
    ///
    /// THE OPPOSITE OF BARRIER IN EVERY DIMENSION, on purpose - the two are each other's
    /// counterpart, and if both read as "a shell" the rename bought nothing:
    ///
    ///     Barrier   a hard faceted dome, in front of you, covering an arc, electric blue
    ///     Bulwark   a soft shapeless swell, ON you, covering everything, warm brass
    ///
    /// "On you" is what the silhouette buys that an aura circle cannot. A ring drawn round the
    /// character is a thing they are standing inside; their own outline, thickened, is something
    /// they are WEARING - which is what a flat damage reduction with no aiming and no direction
    /// actually is. It is a live <see cref="RigSilhouette"/> rather than a snapshot, so it tracks
    /// the lean, the swing and the cape exactly; an aura that lagged the figure it belongs to
    /// would read as a second body standing behind them.
    ///
    /// IT MUST NOT LOOK SEALED. Bulwark softens; it does not erase, and hits keep landing all the
    /// way through it. So it has no rim and no closed edge, and - the part worth guarding - it
    /// FLARES and recovers when a hit gets through rather than thinning. Depleting it would draw
    /// a pool being spent, and there is no pool: it is one multiplier, constant for its whole
    /// second (see Tuning.Defense.BulwarkDamageMultiplier).
    /// </summary>
    public class BulwarkAura : MonoBehaviour
    {
        // Inner is the stronger, tighter layer; outer is wider and fainter. See
        // Tuning.Defense.BulwarkAuraSwell for why one layer is not enough.
        [SerializeField] RigSilhouette _sil;
        [SerializeField] RigSilhouette _outer;
        PlayerController _pc;

        float _life;
        float _age;
        float _flare;
        bool _hooked;

        public static BulwarkAura Attach(PlayerController pc)
        {
            if (pc == null) return null;
            var aura = pc.GetComponent<BulwarkAura>() ?? pc.gameObject.AddComponent<BulwarkAura>();
            aura._pc = pc;

            // Guarded, because a second Attach on the same player would otherwise leave two
            // listeners flaring the aura twice for one hit. BuildPlayer only calls this once per
            // run today; the guard is what keeps that from being load-bearing.
            if (!aura._hooked)
            {
                var hp = pc.GetComponent<Combat.Health>();
                if (hp != null)
                {
                    hp.Damaged += _ => { if (aura != null && aura.Active) aura.Absorb(); };
                    aura._hooked = true;
                }
            }
            return aura;
        }

        /// <summary>Raise the aura for <paramref name="seconds"/>. Re-raising restarts it.</summary>
        public void Raise(float seconds)
        {
            EnsureSilhouette();
            _life = seconds;
            _age = 0f;
            _flare = 0f;
            _sil?.SetVisible(true);
            _outer?.SetVisible(true);
        }

        /// <summary>
        /// A hit landed anyway. Flares to full and settles back - see the class note on why this
        /// is not a drain.
        /// </summary>
        public void Absorb() => _flare = 1f;

        public bool Active => _life > 0f;

        void EnsureSilhouette()
        {
            if (_sil != null && _outer != null) return;

            var rigRoot = (_pc.Rig as MonoBehaviour)?.transform;
            if (rigRoot == null) return;

            // Children of the player, unlike DashTrail's figures: these are SUPPOSED to travel
            // with the character, because they are the character.
            _sil ??= RigSilhouette.Create(_pc.transform, rigRoot);
            _sil?.SetColor(Tuning.Defense.BulwarkAuraColor);
            _sil?.SetSwell(Tuning.Defense.BulwarkAuraSwell);

            _outer ??= RigSilhouette.Create(_pc.transform, rigRoot);
            _outer?.SetColor(Tuning.Defense.BulwarkAuraColor);
            _outer?.SetSwell(Tuning.Defense.BulwarkAuraOuterSwell);
        }

        void LateUpdate()
        {
            if (_life <= 0f)
            {
                _sil?.SetVisible(false);
                _outer?.SetVisible(false);
                return;
            }

            _life -= Time.deltaTime;
            _age += Time.deltaTime;
            _flare = Mathf.Max(0f, _flare - Time.deltaTime * Tuning.Defense.BulwarkAuraFlareDecay);

            EnsureSilhouette();
            if (_sil == null) return;

            // One order below the player's lowest layer, so the swell reads as something behind
            // and around the figure rather than a wash painted over it. The cost of sitting in
            // the depth band rather than the overlay band is that a body standing in front can
            // cover part of it - accepted, because GuardRing above carries the part of this that
            // the player must never miss, and the aura is identity rather than critical feedback.
            int under = _sil.LowestSourceOrder(SortingOrders.Character) - 1;
            _sil.SetOrder(under);
            _sil.Sync();
            _outer?.SetOrder(under - 1);
            _outer?.Sync();

            float breath = Tuning.Defense.BulwarkAuraAlpha
                         + Tuning.Defense.BulwarkAuraBreath * Mathf.Sin(_age * 7f);
            float alpha = Mathf.Lerp(breath, Tuning.Defense.BulwarkAuraFlareAlpha, _flare);

            // Eases out over the last fraction of a second rather than vanishing - a hard cut
            // would read as the ability being cancelled rather than running out.
            alpha *= Mathf.Clamp01(_life / 0.18f);

            _sil.Recolor(alpha);
            _outer?.Recolor(alpha * Tuning.Defense.BulwarkAuraOuterFraction);
        }
    }
}
