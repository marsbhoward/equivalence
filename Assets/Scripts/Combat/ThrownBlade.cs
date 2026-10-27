using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using Convergence.Hazards;

namespace Convergence.Combat
{
    /// <summary>
    /// The blade leaves the hand and spears everything in a straight line. What happens next is
    /// the player's to decide, AFTER seeing where it landed:
    ///
    ///  - RECALL (the attack button): it flies home, rakes the line a second time,
    ///    and the weapon is back in the hand with the player still where they stood.
    ///  - BLINK (the ability button): the player appears where the blade is. Forward damage only.
    ///  - nothing: at its far point the throw is spent. The blade comes back COLD - no second
    ///    pass - so the player is never left disarmed, but the recall is where the value is.
    ///
    /// The forward line is the only guaranteed damage; recall and blink each sit on a button the
    /// player would otherwise be using, so taking either is a real trade rather than a freebie.
    /// The blade no longer loops home on its own past <see cref="MaxDistance"/> - that auto-return
    /// was the free second pass this rework removes.
    /// </summary>
    public class ThrownBlade : MonoBehaviour
    {
        /// <summary>
        /// Outbound travel speed. Tuned against <see cref="MaxDistance"/> so the trip to the point
        /// of no return - the whole window in which a recall is possible - lasts one second: a
        /// finisher whose payoff is a reaction needs long enough to actually react in. The return
        /// leg is deliberately much brisker (<see cref="ReturnSpeed"/>), so a recall reads as a
        /// yank rather than a slow drift back.
        /// </summary>
        public float OutSpeed = 7.5f;
        public float ReturnSpeed = 26f;
        public float MaxDistance = 7.5f;
        public float HitRadius = 0.55f;

        Transform _owner;
        Rigidbody2D _ownerBody;
        GameObject _ownerGo;
        Vector2 _dir, _start;
        float _damage;
        float _knockback;
        ElementType _element;
        System.Action<Health, DamageInfo> _onHit;
        System.Action _onFinished;
        bool _crit;
        /// <summary>Per-body scaling the thrower applies (the mastery board's target rules).</summary>
        System.Func<Health, float, float> _scaleHit;

        SpriteRenderer _sr;
        bool _returning;

        /// <summary>CLASH (Combat.Clash): whether this blade beats an enemy's strike, and where it
        /// was last frame - the line it cuts enemy shots along.</summary>
        bool _wins;
        Vector2 _last;

        /// <summary>Whether the return leg deals damage. Set by a recall or a wall bounce; a throw
        /// that merely reached its far point flies home without biting anything.</summary>
        bool _returnBites;
        /// <summary>Set once and kept for the rest of the flight - a Red force field crossed on
        /// either leg doubles whatever this blade goes on to hit.</summary>
        float _damageMul = 1f;
        float _age;
        float _trail;
        Color _trailTint = Color.white;

        /// <summary>A held weapon is a few dozen pixels; in flight it has to read across the arena.</summary>
        const float FlightScale = 1.8f;

        /// <summary>
        /// The blade's falloff through a crowd: the multiplier for the Nth body (0 = the first) a
        /// LEG strikes - the swing's own rule (ChainFalloff with Cleave, floored), set by the
        /// thrower. Each leg starts again at the first body. Null = no falloff.
        /// </summary>
        public System.Func<int, float> Falloff;

        /// <summary>Bodies struck so far on this leg - see Falloff. A plain int (reload-safe).</summary>
        int _bodiesThisLeg;

        /// <summary>Cleared between legs so the blade can hit the same body coming back.</summary>
        readonly HashSet<Health> _hitThisLeg = new();

        public bool Airborne { get; private set; } = true;

        public static ThrownBlade Launch(GameObject owner, Vector2 dir, float damage, float knockback,
                                         ElementType element, Sprite weaponSprite, Color weaponTint,
                                         Vector2 weaponSize,
                                         System.Action<Health, DamageInfo> onHit, System.Action onFinished,
                                         bool crit = false, System.Func<Health, float, float> scaleHit = null)
        {
            var go = new GameObject("thrown.blade");
            go.transform.position = owner.transform.position + (Vector3)(dir.normalized * 0.5f);

            var b = go.AddComponent<ThrownBlade>();
            b._owner = owner.transform;
            b._ownerGo = owner;
            b._ownerBody = owner.GetComponent<Rigidbody2D>();
            b._dir = dir.normalized;
            b._start = go.transform.position;
            b._damage = damage;
            b._knockback = knockback;
            b._element = element;
            b._onHit = onHit;
            b._onFinished = onFinished;
            b._crit = crit;
            b._scaleHit = scaleHit;
            // Always a weapon art's: it cuts enemy shots and flies on, and a Heavy one beats an
            // enemy's strike (Combat.Clash).
            var thrower = owner.GetComponent<Player.PlayerController>();
            b._wins = thrower != null && Clash.Wins(true, thrower.LastArtWeight);
            b._last = go.transform.position;

            // Draw the ACTUAL equipped weapon. Falling back to a capsule only when the hand is
            // genuinely empty keeps the throw legible as "your sword left your hand".
            var sr = go.AddComponent<SpriteRenderer>();
            bool hasWeapon = weaponSprite != null;
            sr.sprite = hasWeapon ? weaponSprite : Spr.Capsule;
            sr.color = hasWeapon ? weaponTint : ElementInfo.Tint(element);
            sr.sortingOrder = SortingOrders.Fx - 1;   // under the burst FX, over everything else

            // Enlarged in flight: at this camera distance a held weapon is only a few dozen
            // pixels, and something crossing the arena has to be readable while it moves.
            var size = hasWeapon ? weaponSize * FlightScale : new Vector2(0.22f, 1.0f);
            var native = sr.sprite.bounds.size;
            go.transform.localScale = new Vector3(
                native.x > 0.0001f ? size.x / native.x : size.x,
                native.y > 0.0001f ? size.y / native.y : size.y, 1f);
            b._sr = sr;
            b._trailTint = hasWeapon ? weaponTint : ElementInfo.Tint(element);
            // A kindled blade's marks burn in flight as in the hand (SecretFire) - the Aether Greatsword.
            Art.Gear.KindledMarks.On(sr);

            TexelFx.Star(go.transform.position, TexelFx.Steel, 2, 0.1f);   // leaving the hand
            return b;
        }

        void Update()
        {
            // The owner can die or the run can be torn down while the blade is out.
            if (_ownerGo == null || _owner == null) { Finish(); return; }

            float dt = Time.deltaTime;

            // Backstop: nothing should keep a blade out this long, and a weapon that never comes
            // back leaves the player permanently unable to attack.
            _age += dt;
            if (_age > 6f) { Finish(); return; }

            transform.Rotate(0f, 0f, 900f * dt);   // spin sells that it is loose, not held

            // After-images behind it (the user's pick, group 2): a held weapon is a few dozen pixels
            // at this camera distance, so the trail is what lets the eye follow a blade crossing the
            // arena and time the recall against it.
            _trail -= dt;
            if (_trail <= 0f)
            {
                _trail = 0.05f;
                var c = Color.Lerp(_trailTint, Color.white, 0.4f); c.a = 0.55f;
                AfterImage.Of(_sr, c, 0.18f);
            }

            if (!_returning)
            {
                var next = (Vector2)transform.position + _dir * (OutSpeed * dt);
                var clamped = Arena.Clamp(next, 0.35f);

                // A Blue force field nulls the throw outright - and because a blade always has to
                // come back to the hand (see Finish's own doc comment), this routes through Finish
                // rather than a raw Destroy, or the rig would be left permanently weaponless.
                switch (HazardQuery.CheckCrossing(transform.position, clamped))
                {
                    case SightResult.Nulled: Finish(); return;
                    // An interior wall throws it back exactly as the outer wall below does.
                    case SightResult.Blocked: _returnBites = true; BeginReturn(); return;
                    case SightResult.Amplified: _damageMul = 2f; break;
                }
                // Charnel (the exchange): a corpse in the way throws it back like a wall.
                if (Corpse.StopsShot(transform.position, clamped, out _)) { _returnBites = true; BeginReturn(); return; }
                transform.position = clamped;

                // A wall throws it back - a blade visibly rebounding reads fine, and it still
                // rakes the line on the way home, the same as a deliberate recall.
                if (Vector2.Distance(clamped, next) > 0.001f)
                {
                    _returnBites = true;
                    BeginReturn();
                }
                // The far point is the POINT OF NO RETURN. The blade no longer loops home on its
                // own: the outbound line was the guaranteed damage, and the second pass is earned
                // by pressing recall (see Recall). Left alone, the throw is spent - the blade
                // comes back to the hand without biting, so the player is never disarmed for it,
                // but there is nothing more to hit.
                else if (Vector2.Distance(_start, clamped) >= MaxDistance)
                {
                    TexelFx.Star(transform.position, TexelFx.Steel, 2, 0.1f);
                    BeginReturn();
                }
            }
            else
            {
                var to = (Vector2)_owner.position - (Vector2)transform.position;
                float d = to.magnitude;

                // Catch it when this frame's travel would carry it past the hand, rather than at a
                // fixed radius. At 26 u/s a single frame covers more than any sensible radius, so
                // the blade jumped over the player and homed back and forth forever - it looked
                // like it was orbiting, and the weapon never returned.
                float stepLen = ReturnSpeed * dt;
                if (d <= stepLen + 0.2f) { Finish(); return; }

                var nextPos = (Vector2)transform.position + to / d * stepLen;
                switch (HazardQuery.CheckCrossing(transform.position, nextPos))
                {
                    case SightResult.Nulled: Finish(); return;
                    case SightResult.Amplified: _damageMul = 2f; break;
                }
                transform.position = nextPos;
            }

            // Outbound always bites; the return leg only bites if it was RECALLED (or bounced off
            // a wall). A throw that just ran out of line flies home cold.
            if (!_returning || _returnBites) Sweep();
        }

        void Sweep()
        {
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, HitRadius))
            {
                if (col.gameObject == _ownerGo) continue;
                var hp = col.GetComponent<Health>();
                if (hp == null || hp.IsDead || _hitThisLeg.Contains(hp)) continue;

                _hitThisLeg.Add(hp);
                // CLASH: the body's own strike is about to land - the blade is batted off it and
                // its blow is void, unless this is a Heavy art's blade. It flies on either way.
                if (Clash.AtBody(hp, (Vector2)transform.position, _wins)) continue;
                var push = ((Vector2)hp.transform.position - (Vector2)transform.position).normalized;
                // Displaces, because the throw is a FINISHER's damage arriving somewhere else -
                // it follows the same rule as the swing it was spent on, not the basics.
                float dmg = _damage * _damageMul;
                if (Falloff != null) dmg *= Falloff(_bodiesThisLeg);
                _bodiesThisLeg++;
                if (_scaleHit != null) dmg = _scaleHit(hp, dmg);
                var info = new DamageInfo(dmg, _element, _ownerGo)
                {
                    Knockback = push * _knockback,
                    Displaces = true,
                    // Only ever launched from AttackStep.ThrowsWeapon, a finisher-only flag - see
                    // DamageInfo.IsFinisher.
                    IsFinisher = true,
                    Crit = _crit,
                };
                hp.Take(info);
                TexelFx.Hit(hp.transform.position, _element, _dir);
                _onHit?.Invoke(hp, info);
            }
        }

        void LateUpdate()
        {
            Vector2 now = transform.position;
            Clash.CutShots(_last, now, Core.Tuning.Clash.ShotCutRadius);
            _last = now;
        }

        void BeginReturn()
        {
            _returning = true;
            _hitThisLeg.Clear();   // it gets to bite again on the way home
            _bodiesThisLeg = 0;    // and the falloff starts again from the first body
            TexelFx.Star(transform.position, TexelFx.Steel, 2, 0.1f);
        }

        /// <summary>
        /// Call the blade home NOW, raking everything a second time on the way back and putting
        /// the weapon in the hand with the player left where they stand. This is the whole reason
        /// the throw carries a recall: the blink (<see cref="TeleportOwner"/>) only ever gets the
        /// forward damage, and a throw left alone is spent at its far point. Ignored once the
        /// blade is already on its way home or gone.
        /// </summary>
        public void Recall()
        {
            if (!Airborne || _returning) return;
            _returnBites = true;
            var hand = _owner != null ? (Vector2)_owner.position : (Vector2)transform.position;
            TexelFx.Star(transform.position, TexelFx.Steel, 2, 0.1f);   // the call answered at the blade, not over you
            _ = hand;
            BeginReturn();
        }

        /// <summary>
        /// Take the owner to the blade. Clamped inside the walls, and the velocity is dropped so
        /// the arrival is a stop rather than a slingshot in whatever direction they were running.
        /// </summary>
        public void TeleportOwner()
        {
            if (!Airborne || _ownerGo == null || _owner == null) return;

            var from = _owner.position;
            Player.RigGhost.Drop(_owner, new Color(0.85f, 0.88f, 0.95f, 0.6f), 0.35f);
            // Over a chasm, the player goes exactly there: the next physics step judges it as any
            // landing over the void (PlayerController.TrackSafeGround) - set down at the lip
            // inside the forgiveness, a FALL past it (the user's call, 2026-10-14: a player learns
            // their moveset's drawbacks). Anywhere else, off any interior wall the blade came to
            // rest against - a blink into one strands the player inside its collider.
            Vector2 at = transform.position;
            var to = Arena.DeepInChasm(at) ? at : Arena.NearestFloor(at, 0.45f);

            _owner.position = to;
            if (_ownerBody != null) _ownerBody.linearVelocity = Vector2.zero;

            // A fading silhouette left where you stood; you are simply there now.
            _ = to;

            Finish();
        }

        /// <summary>
        /// Every exit runs this, including the owner dying mid-flight: the callback is what puts
        /// the weapon back in the hand, and a rig left permanently weaponless is worse than any
        /// of the ways the throw can end.
        /// </summary>
        void Finish()
        {
            if (!Airborne) return;
            Airborne = false;
            _onFinished?.Invoke();
            Destroy(gameObject);
        }
    }
}
