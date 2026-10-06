using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Whirlwind's cosmetic replacement for the old whole-body <see cref="AttackMotion.Spin"/>:
    /// the weapon leaves the hand, orbits the player once at arm's length while spinning on its
    /// own axis, and lands back in the hand as the swing ends.
    ///
    /// PURELY DECORATIVE. The AoE hit this dresses up is resolved instantly by
    /// <c>PlayerController.ResolveArc</c> the moment the swing starts, exactly as every other
    /// finisher - nothing here changes when or what the swing damages, only what it looks like.
    /// That is deliberate: a version that made the hit land in a ring as the blade actually
    /// passed each body would be a different move (see <see cref="OrbitingDiscs"/>, which IS a
    /// multi-sweep damage mechanic), not a re-skin of one that already exists and is tuned.
    /// </summary>
    public class WhirlingBlade : MonoBehaviour
    {
        /// <summary>How fast the blade tumbles on its own axis - fast enough to read as loose
        /// and thrown rather than carried.</summary>
        const float SelfSpinDegPerSec = 720f;

        /// <summary>
        /// Laps made over the swing. Just over one full turn, so the blade visibly leaves the
        /// hand, sweeps all the way round, and arrives back at roughly where it started rather
        /// than stopping short of a full circle.
        /// </summary>
        const float Laps = 1.15f;

        /// <summary>
        /// Seconds spent flying from the orbit's end point back into the hand, after the orbit
        /// itself completes - without this the blade just vanishes off the rim and the hand
        /// weapon pops in a radius away, reading as a swap rather than a catch.
        /// </summary>
        const float ReturnSeconds = 0.12f;

        GameObject _owner;
        Art.Gear.ICharacterRig _rig;
        float _radius;
        float _duration;
        float _elapsed;
        float _startAngle;
        bool _returning;
        float _returnElapsed;
        Vector2 _returnFrom;
        bool _restored;

        /// <summary>
        /// <paramref name="duration"/> should be the ANIMATED length of the swing
        /// (<see cref="AttackMotions.SwingSeconds"/>), not the raw interval - the blade has to
        /// land back in the hand exactly when the rig's own release pose does.
        /// </summary>
        public static WhirlingBlade Launch(GameObject owner, Art.Gear.ICharacterRig rig,
                                           Vector2 facing, float radius, float duration)
        {
            var go = new GameObject("whirling-blade");
            go.transform.position = owner.transform.position;

            var w = go.AddComponent<WhirlingBlade>();
            w._owner = owner;
            w._rig = rig;
            w._radius = Mathf.Max(0.1f, radius);
            w._duration = Mathf.Max(0.05f, duration);
            w._startAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;

            // Draw the ACTUAL equipped weapon, the same rule ThrownBlade and OrbitingDiscs both
            // already follow - a generic capsule flying around the player would read as a
            // spawned effect rather than as your own sword leaving your hand.
            var sr = go.AddComponent<SpriteRenderer>();
            Sprite sprite = null; Color tint = Color.white; Vector2 size = Vector2.one;
            bool hasWeapon = rig != null && rig.TryGetWeaponVisual(out sprite, out tint, out size);
            if (hasWeapon)
            {
                sr.sprite = sprite;
                sr.color = tint;
                var native = sprite.bounds.size;
                go.transform.localScale = new Vector3(
                    native.x > 0.0001f ? size.x / native.x : size.x,
                    native.y > 0.0001f ? size.y / native.y : size.y, 1f);
            }
            else
            {
                sr.sprite = Spr.Capsule;
                sr.color = Color.white;
                go.transform.localScale = new Vector3(0.22f, 1.0f, 1f);
            }
            sr.sortingOrder = SortingOrders.Fx - 1;
            // A kindled blade's marks burn in flight as in the hand (SecretFire) - the Aether Greatsword.
            Art.Gear.KindledMarks.On(sr);

            // The hand empties for the same reason ThrowWeapon's does: with the real weapon still
            // drawn, this would read as a second sword rather than the one that was in the hand.
            rig?.SetWeaponVisible(false);
            return w;
        }

        void Update()
        {
            // The owner (or the whole run) can go away mid-swing.
            if (_owner == null) { Finish(); return; }

            if (_returning)
            {
                _returnElapsed += Time.deltaTime;
                float rt = Mathf.Clamp01(_returnElapsed / ReturnSeconds);
                transform.position = Vector2.Lerp(_returnFrom, _owner.transform.position, rt);
                transform.Rotate(0f, 0f, SelfSpinDegPerSec * Time.deltaTime);
                if (rt >= 1f) Finish();
                return;
            }

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            // Eased OUT: fast off the hand, slowing into the catch, rather than a constant
            // angular speed that still looks like it's accelerating away when the swing ends.
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            float rad = (_startAngle + Laps * 360f * eased) * Mathf.Deg2Rad;

            transform.position = (Vector2)_owner.transform.position
                + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * _radius;
            transform.Rotate(0f, 0f, SelfSpinDegPerSec * Time.deltaTime);

            // The orbit ends at the rim, not the hand - fly the last stretch back to the player
            // before restoring the hand weapon, rather than teleporting the gap on the same frame.
            if (t >= 1f)
            {
                _returning = true;
                _returnFrom = transform.position;
            }
        }

        void Finish()
        {
            if (_restored) return;
            _restored = true;
            _rig?.SetWeaponVisible(true);
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // Whatever tears this down early - a floor ending, the player dying - must still put
            // the weapon back in the hand, the same discipline ThrowWeapon and OrbitingDiscs both
            // already keep. `_restored` (rather than a collection someone else already cleared)
            // is what tells a Finish()-triggered destroy apart from an external one.
            if (!_restored) _rig?.SetWeaponVisible(true);
        }
    }
}
