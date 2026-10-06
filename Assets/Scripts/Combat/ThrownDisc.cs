using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;
using Convergence.Enemies;
using Convergence.Hazards;

namespace Convergence.Combat
{
    /// <summary>
    /// A disc thrown out, ricocheting between enemies, then returning to the hand.
    ///
    /// It always comes back. The disc is also the melee weapon, so a throw that consumed it would
    /// leave the player unarmed at exactly the moment something closed the distance - the hybrid
    /// only works if the weapon is reliably in hand again by the time it is needed.
    ///
    /// Bounces pick the NEAREST enemy not already hit. Nearest rather than random keeps the arc
    /// readable: a player throwing into a clump can see roughly where it will go, which is what
    /// makes positioning for a ricochet a decision instead of a lottery.
    /// </summary>
    public class ThrownDisc : MonoBehaviour
    {
        public float Speed = 17f;
        public float ReturnSpeed = 22f;

        /// <summary>How far a bounce looks for its next target. Set from the thrower's reach.</summary>
        public float BounceRange = 3.8f;

        /// <summary>Each successive hit deals this much of the last one.</summary>
        public float Falloff = 0.85f;

        /// <summary>Whether this throw's damage (rolled once at launch) was a crit - carried so
        /// crit listeners (Air's momentum, the ledger) hear a thrown crit like a swung one.</summary>
        public bool Crit;

        /// <summary>Gear's Splash, as a fraction. Spills off the FIRST body this disc hits only -
        /// the designated target - not off every ricochet.</summary>
        public float SplashFraction;

        /// <summary>
        /// Damage at the far edge of the throw against damage at the thrower, 1 for flat.
        /// Below 1 makes a throw hit hardest in your face - Kickback's own shape, a shotgun
        /// rather than a spray.
        ///
        /// Measured from where the disc was RELEASED, not from the player's current position -
        /// a move that hops the character backward as it fires would otherwise reward the recoil
        /// by making its own discs weaker.
        /// </summary>
        public float FarFraction = 1f;

        /// <summary>
        /// Damage at the RELEASE POINT against damage at the far edge, 1 for flat. Below 1 makes
        /// a throw weakest close and strongest at range - the basic disc throw's own shape,
        /// opposite Kickback's. Kept as a separate multiplier rather than repurposing
        /// FarFraction: the two throws want opposite curves, and a single shared field would
        /// mean inverting one to build the other, silently breaking whichever move got there
        /// first. Measured the same way FarFraction is, for the same reason.
        /// </summary>
        public float NearFraction = 1f;

        /// <summary>
        /// Paint every body this disc touches, so the next hit on each lands amplified. 1 is off.
        /// </summary>
        public float MarkMultiplier = 1f;
        public float MarkSeconds = 6f;

        Vector2 _origin;
        float _range;

        GameObject _owner;
        Player.PlayerController _player;
        Art.Gear.ICharacterRig _rig;
        float _damage;
        ElementType _element;
        int _hopsLeft;

        /// <summary>Whether this throw came off a finisher (ThrowVolley) rather than the basic
        /// disc throw (ThrowDisc) - see DamageInfo.IsFinisher.</summary>
        bool _isFinisher;

        Vector2 _target;
        Health _seeking;
        bool _returning;
        readonly HashSet<Health> _hit = new();

        /// <summary>Set once and kept for the rest of the flight (both legs) - a Red force field
        /// crossed doubles whatever this disc goes on to hit.</summary>
        float _damageMul = 1f;
        /// <summary>
        /// A hair larger in flight than in the hand. A spinning ring loses apparent mass at the
        /// edges, and the thrown disc has to stay readable against a busy floor.
        /// </summary>
        const float FlightScale = 1.15f;

        SpriteRenderer _sr;
        float _spin;

        public static ThrownDisc Throw(GameObject owner, Art.Gear.ICharacterRig rig, Vector2 aim,
                                       float damage, ElementType element, int ricochets,
                                       float range, float bounceRange, float farFraction = 1f,
                                       float nearFraction = 1f, bool isFinisher = false)
        {
            var go = new GameObject("thrown-disc");
            go.transform.position = owner.transform.position;

            var d = go.AddComponent<ThrownDisc>();
            d._owner = owner;
            d._player = owner.GetComponent<Player.PlayerController>();
            d._rig = rig;
            d._damage = damage;
            d._element = element;
            d._isFinisher = isFinisher;
            d._hopsLeft = Mathf.Max(0, ricochets);
            d.BounceRange = bounceRange;
            d.FarFraction = Mathf.Clamp01(farFraction);
            d.NearFraction = Mathf.Clamp01(nearFraction);
            d._origin = owner.transform.position;
            d._range = Mathf.Max(0.01f, range);

            d._sr = DiscVisual.Add(go);   // spins about its centre, not its grip

            // TryGetWeaponVisual reports a WORLD SIZE in units, not a scale factor. Assigning it
            // straight to localScale multiplied it by the sprite's own size a second time, so a
            // 0.24-unit disc flew at 0.24 x 0.24 = 0.058 units - four pixels, which is why the
            // throw read as a small ball rather than as the weapon leaving your hand. Divide by
            // the native bounds, exactly as ThrownBlade does.
            Vector2 want;
            // -1: the NEXT disc to leave, so a second throw while the first is out flies the off
            // hand's half of a split pair (the Armillary) - taken before SetWeaponVisible below.
            if (rig != null && rig.TryGetDiscVisual(-1, out var sprite, out var tint, out var size))
            {
                d._sr.sprite = sprite;
                d._sr.color = tint;
                want = size * FlightScale;
            }
            else
            {
                d._sr.sprite = Spr.Circle;
                d._sr.color = ElementInfo.Tint(element);
                want = Vector2.one * 0.45f;
            }

            var native = d._sr.sprite.bounds.size;
            go.transform.localScale = new Vector3(
                native.x > 0.0001f ? want.x / native.x : want.x,
                native.y > 0.0001f ? want.y / native.y : want.y, 1f);
            d._sr.sortingOrder = SortingOrders.Fx - 1;
            DiscVisual.Centre(d._sr);
            // the Rift Disc's light-cycle wall, if the hand it left carried one
            LightCycleTrail.Follow(rig, go.transform, d._sr, SortingOrders.Fx - 2);
            // and Rai's arcs, likewise
            LightningArcs.Follow(rig, go.transform, d._sr, SortingOrders.Fx - 2);
            // and a weapon that casts light keeps lighting in flight
            Art.Gear.HeldGlow.Follow(rig, go.transform, d._sr);

            // The disc leaves the hand, so the hand has to be empty. Without this the player is
            // holding a disc while a disc flies away from them.
            rig?.SetWeaponVisible(false);

            // Aimed at the first thing in front, or at nothing in particular if the arena is empty.
            d._seeking = Nearest((Vector2)owner.transform.position, aim, range, d._hit, owner);
            d._target = d._seeking != null
                ? (Vector2)d._seeking.transform.position
                : (Vector2)owner.transform.position + aim.normalized * range;

            return d;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _spin += dt * 1080f;
            transform.localRotation = Quaternion.Euler(0f, 0f, _spin);

            if (_owner == null) { Land(); return; }

            if (_returning)
            {
                var home = (Vector2)_owner.transform.position;
                var prevHomePos = (Vector2)transform.position;
                var nextHomePos = Vector2.MoveTowards(prevHomePos, home, ReturnSpeed * dt);

                // A Blue force field nulls the disc outright - OnDestroy still restores the
                // weapon to the hand however this ends, so a plain Land() here is safe.
                switch (HazardQuery.CheckCrossing(prevHomePos, nextHomePos))
                {
                    case SightResult.Nulled: Land(); return;
                    case SightResult.Amplified: _damageMul = 2f; break;
                }
                transform.position = nextHomePos;
                if (Vector2.Distance(nextHomePos, home) < 0.35f) Land();
                return;
            }

            // A target that dies mid-flight is not a reason to stop - the disc keeps going to
            // where it was aimed and bounces from there.
            if (_seeking != null && !_seeking.IsDead) _target = _seeking.transform.position;

            var prevPos = (Vector2)transform.position;
            var nextPos = Vector2.MoveTowards(prevPos, _target, Speed * dt);
            switch (HazardQuery.CheckCrossing(prevPos, nextPos))
            {
                case SightResult.Nulled:
                case SightResult.Blocked: Land(); return;
                case SightResult.Amplified: _damageMul = 2f; break;
            }
            // Charnel (the exchange): a corpse in the way drops the disc as a wall does.
            if (Corpse.StopsShot(prevPos, nextPos, out _)) { Land(); return; }
            transform.position = nextPos;
            if (Vector2.Distance(nextPos, _target) > 0.28f) return;

            if (_seeking != null && !_seeking.IsDead && !_hit.Contains(_seeking))
            {
                _hit.Add(_seeking);

                // Clamped to 1 rather than left open past _range: a bounce (or a future
                // directional ability) that lands somewhere past the thrower's own range circle
                // must not keep climbing past the curve's own peak for travelling further.
                float t = Mathf.Clamp01(Vector2.Distance(_seeking.transform.position, _origin) / _range);
                float far = FarFraction >= 1f ? 1f : Mathf.Lerp(1f, FarFraction, t);
                float near = NearFraction >= 1f ? 1f : Mathf.Lerp(NearFraction, 1f, t);

                float dmg = _damage * far * near * _damageMul;
                if (_player != null) dmg = _player.ScaleThrownHit(_seeking, dmg, _isFinisher, Crit, t);
                var info = new DamageInfo(dmg, _element, _owner)
                    { Thrown = true, IsFinisher = _isFinisher, Crit = Crit };

                var where = _seeking.transform.position;
                _seeking.Take(info);
                if (_hit.Count == 1) CrowdHits.Splash(_seeking, info.Amount, SplashFraction, _element, _owner);

                // Marked AFTER the hit resolves, so a disc cannot amplify its own damage - the
                // mark is a promise about the NEXT thing to land, which is what makes Mark a
                // setup move rather than just a slower way to deal the same damage.
                if (MarkMultiplier > 1f)
                    StatusEffects.Get(_seeking.gameObject).ApplyMark(MarkSeconds, MarkMultiplier);

                // A thrown hit is still a hit. Until this was here, a disc player at range built
                // NO elemental resource at all and nothing in the run's ledger noticed they were
                // fighting - half the class was disconnected from both systems.
                _player?.Resource?.OnHitLanded(_seeking, info);
                _player?.NotifyThrownHit(_seeking, info, t, _hit.Count == 1);

                Spr.Flash(where, 0.55f, ElementInfo.Tint(_element), 0.16f, false);
                _damage *= Falloff;
            }

            if (_hopsLeft-- > 0)
            {
                var next = Nearest(transform.position, Vector2.zero, BounceRange, _hit, _owner);
                if (next != null)
                {
                    _seeking = next;
                    _target = next.transform.position;
                    return;
                }
            }

            _returning = true;
            _seeking = null;
        }

        /// <summary>
        /// Nearest live enemy not already struck. A zero <paramref name="facing"/> searches all
        /// round - that is a BOUNCE, which has no forward, unlike the initial throw.
        /// </summary>
        static Health Nearest(Vector2 from, Vector2 facing, float range, HashSet<Health> skip, GameObject owner)
        {
            EnemyRegistry.Prune();
            Health best = null;
            float bestDist = float.MaxValue;
            bool directional = facing.sqrMagnitude > 0.0001f;
            var dir = directional ? facing.normalized : Vector2.zero;

            foreach (var e in EnemyRegistry.All)
            {
                if (e == null || e.gameObject == owner) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead || skip.Contains(hp)) continue;

                var delta = (Vector2)e.transform.position - from;
                float d = delta.magnitude;
                if (d > range || d >= bestDist) continue;

                // The opening throw only considers what is roughly in front, so it goes where the
                // player is looking rather than snapping to something behind them.
                if (directional && Vector2.Dot(delta / Mathf.Max(d, 0.0001f), dir) < 0.2f) continue;

                best = hp;
                bestDist = d;
            }
            return best;
        }

        void Land() => Destroy(gameObject);

        /// <summary>
        /// The hide is released HERE and only here, so it happens exactly once however the disc
        /// ends - landing, a run teardown, or the scene going away. Visibility is reference
        /// counted on the rig, so releasing twice would hand the weapon back to a throw that is
        /// still in the air.
        /// </summary>
        void OnDestroy() => _rig?.SetWeaponVisible(true);
    }
}
