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
        public const float FlightScale = 1.15f;

        /// <summary>A HEAVY weapon art's disc moves what it hits (the user's call, 2026-10-15 - the
        /// swords' rule, now every class's): this knockback along the disc's flight, displacing.
        /// 0 for everything else.</summary>
        public float Knockback;
        Vector2 _travel = Vector2.right;

        SpriteRenderer _sr;
        float _spin;

        /// <summary>The spin, x an ordinary disc's (Heavy Cleaver's turns slower).</summary>
        public float SpinMul = 1f;

        /// <summary>Ripple's homing leg (Home): a curve from the release, bowed out along _heading,
        /// onto the target - until the first arrival; bounces after it fly straight.</summary>
        bool _homing;
        Vector2 _heading, _homeFrom;
        float _homeU;

        /// <summary>Aim the throw at <paramref name="target"/> instead of the nearest in front that
        /// Throw picked (the locked enemy, when in reach). Call right after Throw.</summary>
        /// <summary>Draw this disc as the weapon's combined picture (Carom's joined pair).</summary>
        public void ShowCombined()
        {
            DiscPictures.ShowCombined(_sr, _player);
            DiscVisual.Centre(_sr);
        }

        public void Aim(Health target)
        {
            if (target == null || target.IsDead) return;
            _seeking = target;
            _target = target.transform.position;
        }

        /// <summary>
        /// Send the disc out along <paramref name="heading"/> and let it CURVE onto
        /// <paramref name="target"/> - a homing missile's flight (Ripple). Null: it flies straight
        /// on along the heading to its range. Call right after Throw, which had picked its own.
        /// </summary>
        public void Home(Health target, Vector2 heading)
        {
            _homing = true;
            _homeU = 0f;
            _homeFrom = transform.position;
            _heading = heading.sqrMagnitude > 1e-6f ? heading.normalized : Vector2.right;
            _seeking = target;
            _target = target != null ? (Vector2)target.transform.position
                                     : (Vector2)transform.position + _heading * _range;
        }

        public static ThrownDisc Throw(GameObject owner, Art.Gear.ICharacterRig rig, Vector2 aim,
                                       float damage, ElementType element, int ricochets,
                                       float range, float bounceRange, float farFraction = 1f,
                                       float nearFraction = 1f, bool isFinisher = false,
                                       int discIndex = -1)
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
            if (rig != null && rig.TryGetDiscVisual(discIndex, out var sprite, out var tint, out var size))
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
            _spin += dt * 1080f * SpinMul;
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

            if (_caroms) { UpdateCarom(dt); return; }
            if (_straight) { UpdateStraight(dt); return; }

            // A target that dies mid-flight is not a reason to stop - the disc keeps going to
            // where it was aimed and bounces from there.
            if (_seeking != null && !_seeking.IsDead) _target = _seeking.transform.position;

            var prevPos = (Vector2)transform.position;
            var nextPos = Vector2.MoveTowards(prevPos, _target, Speed * dt);
            if (_homing)
            {
                // A curve, not steering (steering overshot a target close by and circled back):
                // from the release, bowed out along the disc's own fan heading, ending on the
                // target where it is NOW - travelled at the disc's speed, so the fan always opens
                // and the disc always arrives.
                var p0 = _homeFrom;
                var p2 = _target;
                float span = Vector2.Distance(p0, p2);
                var p1 = p0 + _heading * Mathf.Max(0.4f, span * Core.Tuning.Ripple.Bow);
                float length = Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2);
                _homeU = Mathf.Min(1f, _homeU + Speed * dt / Mathf.Max(0.05f, length));
                float u = _homeU, w = 1f - u;
                nextPos = w * w * p0 + 2f * w * u * p1 + u * u * p2;
                if (_homeU >= 1f) nextPos = p2;
            }
            if ((nextPos - prevPos).sqrMagnitude > 1e-8f) _travel = (nextPos - prevPos).normalized;
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
            _homing = false;   // arrived: any bounce after this flies straight

            if (_seeking != null && !_seeking.IsDead && !_hit.Contains(_seeking))
            {
                _hit.Add(_seeking);
                Strike(_seeking);
            }

            if (_hopsLeft-- > 0)
            {
                // A marking disc paints the nearest body NOT yet painted first, so one throw
                // covers a pack instead of re-marking what is already marked.
                Health next = null;
                if (MarkMultiplier > 1f)
                    next = Nearest(transform.position, Vector2.zero, BounceRange, _hit, _owner, skipMarked: true);
                if (next == null)
                    next = Nearest(transform.position, Vector2.zero, BounceRange, _hit, _owner);
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

        int _strikes;

        /// <summary>One hit on <paramref name="h"/> at the disc's current damage, then the damage
        /// falls off for the next - every path's hit (the straight flight and the carom).</summary>
        void Strike(Health h)
        {
            _strikes++;
            bool first = _strikes == 1;

            // Clamped to 1 rather than left open past _range: a bounce (or a future
            // directional ability) that lands somewhere past the thrower's own range circle
            // must not keep climbing past the curve's own peak for travelling further.
            float t = Mathf.Clamp01(Vector2.Distance(h.transform.position, _origin) / _range);
            float far = FarFraction >= 1f ? 1f : Mathf.Lerp(1f, FarFraction, t);
            float near = NearFraction >= 1f ? 1f : Mathf.Lerp(NearFraction, 1f, t);

            if (_volley != null && h != _reserved) _volley[h] = (_volley.TryGetValue(h, out int struck) ? struck : 0) + 1;
            if (h == _reserved) _reserved = null;

            float dmg = _damage * far * near * _damageMul;
            if (_player != null) dmg = _player.ScaleThrownHit(h, dmg, _isFinisher, Crit, t, first: first);
            var info = new DamageInfo(dmg, _element, _owner)
                { Thrown = true, IsFinisher = _isFinisher, Crit = Crit,
                  Knockback = _travel * Knockback, Displaces = Knockback > 0f };

            var where = h.transform.position;
            h.Take(info);
            if (first) CrowdHits.Splash(h, info.Amount, SplashFraction, _element, _owner);

            // Marked AFTER the hit resolves, so a disc cannot amplify its own damage - the
            // mark is a promise about the NEXT thing to land, which is what makes Mark a
            // setup move rather than just a slower way to deal the same damage.
            if (MarkMultiplier > 1f)
                StatusEffects.Get(h.gameObject).ApplyMark(MarkSeconds, MarkMultiplier, _element);

            // A thrown hit is still a hit. Until this was here, a disc player at range built
            // NO elemental resource at all and nothing in the run's ledger noticed they were
            // fighting - half the class was disconnected from both systems.
            _player?.Resource?.OnHitLanded(h, info);
            _player?.NotifyThrownHit(h, info, t, first);

            Spr.Flash(where, 0.55f, ElementInfo.Tint(_element), 0.16f, false);
            _damage *= Falloff;
        }

        // ---- the SPRAY (Kickback; the user's calls, 2026-10-15) ----
        //
        // Straight out along its own line, no seeking: the first body it meets takes the hit and
        // the disc comes home; nothing met by its range, it comes home from there. Walls stop it.

        bool _straight;

        /// <summary>Send this throw straight along <paramref name="dir"/> (Kickback's spray).
        /// Call right after Throw.</summary>
        public void Straight(Vector2 dir)
        {
            _straight = true;
            _seeking = null;
            _dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
        }

        void UpdateStraight(float dt)
        {
            var pos = (Vector2)transform.position;
            float step = Speed * dt;
            if (step <= 0f) return;   // a hit-stop
            var next = pos + _dir * step;
            switch (HazardQuery.CheckCrossing(pos, next))
            {
                case SightResult.Nulled:
                case SightResult.Blocked: Land(); return;
                case SightResult.Amplified: _damageMul = 2f; break;
            }
            if (Corpse.StopsShot(pos, next, out _)) { Land(); return; }
            transform.position = next;
            _travel = _dir;

            int n = Physics2D.OverlapCircleNonAlloc(next, Core.Tuning.Carom.HitRadius, _touch);
            for (int i = 0; i < n; i++)
            {
                var col = _touch[i];
                if (col == null) continue;
                var hp = col.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead || !IsEnemy(col)) continue;
                _hit.Add(hp);
                Strike(hp);
                if (_spoke) { SpokeOn(next, BounceRange); return; }
                _returning = true;
                return;
            }
            if (Vector2.Distance(next, _origin) < _range) return;
            if (_spoke && _owner != null) { SpokeOn(_owner.transform.position, _range); return; }
            _returning = true;
        }

        // ---- the SPOKES (Scattershot; the user's calls, 2026-10-16) ----
        //
        // Straight out along its spoke (the spray's flight). What it meets is struck and it
        // bounces ONCE, to the enemy the whole volley has reached least, nearest first; a disc
        // that meets nothing by its range turns back in on the enemy nearest the PLAYER, the same
        // way - so a lone enemy takes every disc and a crowd is spread over. Then home.

        bool _spoke;
        /// <summary>How often the volley has struck each body, shared by its discs.</summary>
        Dictionary<Health, int> _volley;
        /// <summary>The body this disc turned toward, already counted in the volley.</summary>
        Health _reserved;

        /// <summary>Send this throw out along a spoke of a ring (Scattershot), sharing
        /// <paramref name="volley"/> with the ring's other discs. Call right after Throw.</summary>
        public void Spoke(Vector2 dir, Dictionary<Health, int> volley)
        {
            Straight(dir);
            _spoke = true;
            _volley = volley;
        }

        /// <summary>The spoke is spent: on to the least-struck enemy within
        /// <paramref name="radius"/> of <paramref name="from"/> (never one this disc has hit), or
        /// home.</summary>
        void SpokeOn(Vector2 from, float radius)
        {
            _spoke = false;
            _straight = false;
            _hopsLeft = 0;
            EnemyRegistry.Prune();
            Health best = null;
            int bestHits = int.MaxValue;
            float bestDist = float.MaxValue;
            foreach (var e in EnemyRegistry.All)
            {
                if (e == null || e.gameObject == _owner) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead || _hit.Contains(hp)) continue;
                float d = Vector2.Distance(e.transform.position, from);
                if (d > radius) continue;
                int hits = _volley != null && _volley.TryGetValue(hp, out int n) ? n : 0;
                if (hits > bestHits || (hits == bestHits && d >= bestDist)) continue;
                best = hp; bestHits = hits; bestDist = d;
            }
            if (best == null) { _returning = true; return; }
            // Counted as it is CHOSEN, not as it lands: the empty spokes all turn in on the same
            // frame, and counting at the hit sent every one of them to the same body.
            _volley?.TryAdd(best, 0);
            if (_volley != null) { _volley[best]++; _reserved = best; }
            _seeking = best;
            _target = best.transform.position;
        }

        // ---- the CAROM (the art Carom; the user's calls, 2026-10-15) ----
        //
        // Flies in straight lines and REFLECTS off walls and pillars like a billiard ball off the
        // cushions. After every hit it picks its next leg: the nearest enemy in a clear line (not
        // the one it just hit), or else a BANK SHOT - a direction whose one reflection off a solid
        // carries it into an enemy, which may be the one it just hit (the wall is the something
        // in between). Every bounce - enemy or wall - spends one of its ricochets, and it spends
        // them all (the user's call): one enemy by a wall is struck, banked into, struck again.
        // Only a hit loses damage.

        bool _caroms;
        int _caromLeft;
        Vector2 _dir;
        Health _lastHit;
        bool _banking;
        float _caromTravel;
        /// <summary>How often the carom has struck each body - the next leg goes to the one struck
        /// least, so it spreads through a crowd instead of ping-ponging between the nearest two.
        /// Non-readonly - domain reload.</summary>
        Dictionary<Health, int> _caromHits = new();

        int HitsOn(Health hp) => hp != null && _caromHits.TryGetValue(hp, out int n) ? n : 0;

        static readonly RaycastHit2D[] _casts = new RaycastHit2D[16];
        static readonly Collider2D[] _touch = new Collider2D[16];

        /// <summary>Turn this throw into a carom toward <paramref name="first"/> (or along
        /// <paramref name="aim"/>), with <paramref name="bounces"/> bounces after the first hit.
        /// Call right after Throw.</summary>
        public void Carom(Health first, Vector2 aim, int bounces)
        {
            _caroms = true;
            _caromLeft = Mathf.Max(0, bounces);
            if (first != null) _seeking = first;   // else Throw's own pick, the nearest in front
            var to = _seeking != null ? (Vector2)_seeking.transform.position - (Vector2)transform.position : aim;
            _dir = to.sqrMagnitude > 1e-6f ? to.normalized : Vector2.right;
        }

        void UpdateCarom(float dt)
        {
            var pos = (Vector2)transform.position;

            // Homing onto the target while the line to it is clear - but a bank shot flies
            // straight until its reflection, or it would turn away from the wall it needs.
            if (!_banking && _seeking != null && !_seeking.IsDead)
            {
                var to = (Vector2)_seeking.transform.position - pos;
                if (to.sqrMagnitude > 1e-6f && !SolidOnLine(pos, to.normalized, to.magnitude, out _))
                    _dir = to.normalized;
            }

            float step = Speed * dt;
            if (step <= 0f) return;   // a hit-stop

            // A wall or a pillar: reflect off it - one bounce spent.
            if (SolidOnLine(pos, _dir, step, out var wall))
            {
                pos = wall.centroid + wall.normal * 0.02f;
                _dir = Vector2.Reflect(_dir, wall.normal).normalized;
                transform.position = pos;
                _travel = _dir;
                Spr.Flash(pos, 0.3f, ElementInfo.Tint(_element), 0.1f, false);
                _banking = false;
                _lastHit = null;   // the wall is the something in between
                if (_caromLeft-- <= 0) { _returning = true; _seeking = null; }
                return;
            }

            var next = pos + _dir * step;
            switch (HazardQuery.CheckCrossing(pos, next))
            {
                case SightResult.Nulled: Land(); return;
                case SightResult.Amplified: _damageMul = 2f; break;
            }
            if (Corpse.StopsShot(pos, next, out _)) { Land(); return; }
            transform.position = next;
            _travel = _dir;
            _caromTravel += step;

            // An enemy in the disc's way - any, not only the one it is after.
            int n = Physics2D.OverlapCircleNonAlloc(next, Core.Tuning.Carom.HitRadius, _touch);
            for (int i = 0; i < n; i++)
            {
                var col = _touch[i];
                if (col == null) continue;
                var hp = col.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead || hp == _lastHit || !IsEnemy(col)) continue;
                _caromHits[hp] = HitsOn(hp) + 1;
                _hit.Add(hp);
                Strike(hp);
                _lastHit = hp;
                _caromTravel = 0f;
                if (_caromLeft-- <= 0 || !PlanCarom(next)) { _returning = true; _seeking = null; }
                return;
            }

            // Flown its reach with nothing struck: home.
            if (_caromTravel > Core.Tuning.Carom.LegReach * BounceRange) { _returning = true; _seeking = null; }
        }

        static bool Available(Health hp) => hp != null && !hp.IsDead;

        /// <summary>The next leg from <paramref name="from"/>: a clear line to the nearest other
        /// enemy, else the shortest bank shot. False: nothing to go to.</summary>
        bool PlanCarom(Vector2 from)
        {
            float reach = BounceRange * Core.Tuning.Carom.LegReach;
            EnemyRegistry.Prune();

            // The least struck first, the nearest among those.
            Health best = null;
            float bestD = float.MaxValue;
            int bestN = int.MaxValue;
            foreach (var e in EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (!Available(hp) || hp == _lastHit) continue;
                var to = (Vector2)hp.transform.position - from;
                float d = to.magnitude;
                int n = HitsOn(hp);
                if (d > BounceRange || d < 1e-4f) continue;
                if (n > bestN || (n == bestN && d >= bestD)) continue;
                if (SolidOnLine(from, to / d, d, out _)) continue;
                best = hp; bestD = d; bestN = n;
            }
            if (best != null)
            {
                _seeking = best;
                _dir = ((Vector2)best.transform.position - from).normalized;
                _banking = false;
                return true;
            }

            // The bank shot: every direction round, one reflection, an enemy on the way out.
            float bestLen = float.MaxValue;
            Vector2 bestDir = default;
            const int Rays = 72;
            for (int r = 0; r < Rays; r++)
            {
                float a = r * Mathf.PI * 2f / Rays;
                var d1 = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                if (!SolidOnLine(from, d1, reach, out var w) || w.distance < 0.05f) continue;
                var p1 = w.centroid + w.normal * 0.02f;
                var d2 = Vector2.Reflect(d1, w.normal).normalized;
                float left = reach - w.distance;
                foreach (var e in EnemyRegistry.All)
                {
                    if (e == null) continue;
                    var hp = e.GetComponent<Health>();
                    if (!Available(hp)) continue;
                    var rel = (Vector2)hp.transform.position - p1;
                    float along = Vector2.Dot(rel, d2);
                    if (along <= 0f || along > left) continue;
                    if ((rel - d2 * along).magnitude > Core.Tuning.Carom.HitRadius + 0.15f) continue;
                    if (SolidOnLine(p1, d2, along, out _)) continue;
                    float len = w.distance + along;
                    int n = HitsOn(hp);
                    if (n < bestN || (n == bestN && len < bestLen)) { bestLen = len; bestDir = d1; best = hp; bestN = n; }
                }
            }
            if (best == null) return false;
            _seeking = best;
            _dir = bestDir;
            _banking = true;
            return true;
        }

        static bool IsEnemy(Collider2D c)
            => c.GetComponentInParent<EnemyController>() != null
               || c.GetComponentInParent<Bosses.Boss>() != null;

        /// <summary>The first wall or pillar on the line - a solid collider that is not a body or
        /// a shot.</summary>
        static bool SolidOnLine(Vector2 from, Vector2 dir, float dist, out RaycastHit2D hit)
        {
            hit = default;
            int n = Physics2D.CircleCastNonAlloc(from, Core.Tuning.Carom.DiscRadius, dir, _casts, dist);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = _casts[i];
                var c = h.collider;
                if (c == null || c.isTrigger) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
                if (IsEnemy(c) || c.GetComponentInParent<Player.PlayerController>() != null) continue;
                if (h.distance <= 1e-4f && Vector2.Dot(h.normal, dir) >= 0f) continue;   // leaving it
                if (h.distance < best) { best = h.distance; hit = h; found = true; }
            }
            return found;
        }

        /// <summary>
        /// Nearest live enemy not already struck. A zero <paramref name="facing"/> searches all
        /// round - that is a BOUNCE, which has no forward, unlike the initial throw.
        /// </summary>
        static Health Nearest(Vector2 from, Vector2 facing, float range, HashSet<Health> skip, GameObject owner,
                              bool skipMarked = false)
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
                if (skipMarked && hp.MarkMultiplier > 1f) continue;

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
        void OnDestroy()
        {
            _rig?.SetWeaponVisible(true);
            Landed?.Invoke();
        }

        /// <summary>Called once, when the disc is home (or gone) - Carom re-splits the pair.</summary>
        public System.Action Landed;
    }
}
