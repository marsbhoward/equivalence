using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Combat
{
    /// <summary>
    /// Orrery (the user's calls, 2026-10-16): the pair flung out of ORBIT onto TWO ORBITS round
    /// the player, like the planets of an orrery - the main hand's disc on the INNER orbit, the
    /// off hand's on the OUTER one at half its speed (Tuning.Orrery) - following the player, who
    /// moves freely. Each spirals out of its hand onto its orbit, runs its revolutions and spirals
    /// back in. Both orbits are drawn faintly on the ground (the orrery's rails), so where it will
    /// cut is read before it gets there.
    ///
    /// A body is struck only when a disc actually PASSES it (within HitRadius of the disc's ground
    /// point), once per disc per revolution, for that orbit's share - set so that a body under
    /// either path for the whole art takes the same: the inner disc twice at half, the outer once
    /// in full. Thrown hits (the board's and the ledger's rules, the crit rolled at the cast), one
    /// hit-stop per disc per revolution. It replaced two discs teleported onto one 2.2 ring at
    /// 0.26 s a turn (a blur), hitting everything in the band every sweep wherever the discs were,
    /// with a hole up to 1.35 that missed every Chaser pressed against the player.
    /// </summary>
    public class OrbitingDiscs : MonoBehaviour
    {
        class Orbit
        {
            public Transform T;
            public float Radius, Share, Revs, StartAngle, FromRadius, FromAngle;
            public int LastRev = -1;
            public bool StoppedThisRev;
            // Per body, the revolution it was last struck in. Non-readonly (domain reload).
            public Dictionary<Health, int> Struck = new();
            public SpriteRenderer Rail;
        }

        // Non-readonly: a domain reload brings a readonly collection back fresh.
        List<Orbit> _orbits = new();
        GameObject _owner;
        Player.PlayerController _player;
        Art.Gear.ICharacterRig _rig;
        float _damage;
        bool _crit;
        ElementType _element;
        float _seconds, _clock, _sign;
        bool _first = true;
        bool _handsHidden;

        public static OrbitingDiscs Launch(GameObject owner, Player.PlayerController player,
                                           Art.Gear.ICharacterRig rig, float damage, bool crit,
                                           ElementType element, Vector2 facing, float seconds,
                                           Vector2 mainHand, Vector2 offHand)
        {
            var go = new GameObject("orrery");
            go.transform.position = owner.transform.position;

            var o = go.AddComponent<OrbitingDiscs>();
            o._owner = owner;
            o._player = player;
            o._rig = rig;
            o._damage = damage;
            o._crit = crit;
            o._element = element;
            o._seconds = Mathf.Max(Tuning.Orrery.SpiralSeconds * 2f + 0.3f, seconds);
            // Round the way the body faces: counter-clockwise facing right, as Carousel turns.
            o._sign = facing.x >= 0f ? 1f : -1f;

            Sprite sprite = null; Color tint = Color.white; Vector2 size = Vector2.one;
            bool hasWeapon = rig != null && rig.TryGetWeaponVisual(out sprite, out tint, out size);
            Sprite offSprite = null;
            if (hasWeapon) rig.TryGetDiscVisual(1, out offSprite, out _, out _);

            Vector2 centre = owner.transform.position;
            for (int i = 0; i < 2; i++)
            {
                bool outer = i == 1;
                var orbit = new Orbit
                {
                    Radius = outer ? Tuning.Orrery.OuterRadius : Tuning.Orrery.InnerRadius,
                    Revs = outer ? Tuning.Orrery.OuterRevolutions : Tuning.Orrery.InnerRevolutions,
                    Share = outer ? Tuning.Orrery.OuterShare : Tuning.Orrery.InnerShare,
                };
                // Out of its own hand: the spiral starts where the hand is.
                var hand = (outer ? offHand : mainHand) - centre;
                orbit.FromRadius = hand.magnitude;
                orbit.FromAngle = Mathf.Atan2(hand.y, hand.x) * Mathf.Rad2Deg;
                orbit.StartAngle = orbit.FromAngle;

                var d = new GameObject(outer ? "orrery-outer" : "orrery-inner");
                d.transform.SetParent(go.transform, false);
                d.transform.position = outer ? offHand : mainHand;
                var sr = DiscVisual.Add(d);   // spins about its centre, not its grip
                if (hasWeapon)
                {
                    sr.sprite = outer && offSprite != null ? offSprite : sprite;
                    sr.color = tint;
                    var native = sprite.bounds.size;
                    sr.transform.localScale = new Vector3(
                        native.x > 0.0001f ? size.x / native.x : size.x,
                        native.y > 0.0001f ? size.y / native.y : size.y, 1f);
                }
                else
                {
                    sr.sprite = Spr.Circle;
                    sr.color = ElementInfo.Tint(element);
                    sr.transform.localScale = Vector3.one * 0.4f;
                }
                DiscPictures.ShowCombined(sr, player);   // both out at once: the combined picture
                sr.sortingOrder = SortingOrders.Fx - 1;
                DiscVisual.Centre(sr);
                LightCycleTrail.Follow(rig, d.transform, sr, SortingOrders.Fx - 2);
                LightningArcs.Follow(rig, d.transform, sr, SortingOrders.Fx - 2);
                Art.Gear.HeldGlow.Follow(rig, d.transform, sr);
                orbit.T = d.transform;

                // The rail: the orbit drawn faintly on the ground.
                var rail = new GameObject(outer ? "orrery-rail-outer" : "orrery-rail-inner");
                rail.transform.SetParent(go.transform, false);
                var rsr = rail.AddComponent<SpriteRenderer>();
                rsr.sprite = RailSprite(orbit.Radius);
                rsr.sortingOrder = SortingOrders.GroundDecal + 2;
                var c = ElementInfo.Tint(element); c.a = 0f;
                rsr.color = c;
                orbit.Rail = rsr;

                o._orbits.Add(orbit);
            }

            // Both discs leave the hands for the duration.
            rig?.SetWeaponVisible(false);
            o._handsHidden = true;
            return o;
        }

        void Update()
        {
            // Follows the player, who moves freely: walking steers the whole orrery.
            if (_owner == null) { Finish(); return; }
            transform.position = _owner.transform.position;
            Vector2 centre = transform.position;

            float dt = Time.deltaTime;
            _clock += dt;
            float spiral = Tuning.Orrery.SpiralSeconds;
            float orbitSeconds = _seconds - spiral * 2f;

            // The rails come up as the discs go out and fade as they come in.
            float railA = Tuning.Orrery.RailAlpha
                          * Mathf.Clamp01(Mathf.Min(_clock / spiral, (_seconds - _clock) / spiral));

            foreach (var o in _orbits)
            {
                if (o.T == null) continue;
                var rc = o.Rail.color; rc.a = railA; o.Rail.color = rc;

                // Angle: steady through the orbit (its revolutions over the orbit's time), the
                // spirals carrying on at the same angular speed.
                float degPerSec = 360f * o.Revs / Mathf.Max(0.05f, orbitSeconds);
                float angle = o.StartAngle + _sign * degPerSec * (_clock - spiral);
                float radius;
                bool striking = false;
                if (_clock < spiral)
                {
                    float u = _clock / spiral;
                    u = 1f - (1f - u) * (1f - u);
                    radius = Mathf.Lerp(o.FromRadius, o.Radius, u);
                }
                else if (_clock < _seconds - spiral)
                {
                    radius = o.Radius;
                    striking = true;
                }
                else
                {
                    float u = Mathf.Clamp01((_clock - (_seconds - spiral)) / spiral);
                    radius = Mathf.Lerp(o.Radius, 0.15f, u * u);
                }

                Vector2 ground = centre + new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                                                      Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;
                o.T.position = ground + Vector2.up * Tuning.Orrery.DrawnHeight;
                o.T.Rotate(0f, 0f, 540f * dt);

                if (striking)
                {
                    int rev = Mathf.FloorToInt(degPerSec * (_clock - spiral) / 360f);
                    if (rev != o.LastRev) { o.LastRev = rev; o.StoppedThisRev = false; }
                    Strike(o, ground, rev);
                }

                // Its rail stays on the player too.
                o.Rail.transform.position = centre;
            }

            if (_clock >= _seconds) Finish();
        }

        static readonly Collider2D[] _touch = new Collider2D[24];

        /// <summary>The bodies this disc is passing now - each once a revolution.</summary>
        void Strike(Orbit o, Vector2 ground, int rev)
        {
            int n = Physics2D.OverlapCircleNonAlloc(ground, Tuning.Orrery.HitRadius, _touch);
            for (int i = 0; i < n; i++)
            {
                var col = _touch[i];
                if (col == null || col.gameObject == _owner) continue;
                var hp = col.GetComponentInParent<Health>();
                if (hp == null || hp.IsDead || hp.gameObject == _owner) continue;
                if (col.GetComponentInParent<Enemies.EnemyController>() == null
                    && col.GetComponentInParent<Bosses.Boss>() == null) continue;
                if (o.Struck.TryGetValue(hp, out int last) && last >= rev) continue;
                o.Struck[hp] = rev;

                float dmg = _damage * o.Share;
                if (_player != null) dmg = _player.ScaleThrownHit(hp, dmg, true, _crit, 0.5f, first: _first);
                // Orrery: a weapon art's hit - see DamageInfo.IsFinisher. One hit-stop per disc
                // per revolution, not per body.
                var info = new DamageInfo(dmg, _element, _owner)
                    { IsFinisher = true, Thrown = true, Crit = _crit, SuppressHitstop = o.StoppedThisRev };
                hp.Take(info);
                o.StoppedThisRev = true;

                _player?.Resource?.OnHitLanded(hp, info);
                _player?.NotifyThrownHit(hp, info, 0.5f, _first);
                _first = false;
                TexelFx.Hit(hp.transform.position, _element, Vector2.zero);
            }
        }

        void Finish()
        {
            if (_handsHidden) { _rig?.SetWeaponVisible(true); _handsHidden = false; }
            foreach (var o in _orbits)
            {
                if (o.T != null) Destroy(o.T.gameObject);
                if (o.Rail != null) Destroy(o.Rail.gameObject);
            }
            _orbits.Clear();
            Destroy(gameObject);
        }

        void OnDestroy()
        {
            // If anything destroys this early - a floor ending, the player dying - the hands must
            // still come back. SetWeaponVisible is reference counted, so the pairing has to hold
            // on every exit path or the character is left permanently empty-handed.
            if (_handsHidden) _rig?.SetWeaponVisible(true);
        }

        // ---- the rails ----
        //
        // A dotted circle on the texel grid at body density: one texel dots, every few round the
        // ring, white for the renderer to tint. Cached per radius.

        static Dictionary<int, Sprite> _rails = new();

        static Sprite RailSprite(float radius)
        {
            const float Ppu = 37.5f;
            int r = Mathf.RoundToInt(radius * Ppu);
            if (_rails.TryGetValue(r, out var s) && s != null) return s;
            int n = r * 2 + 3;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            int c = n / 2;
            int steps = Mathf.Max(12, Mathf.RoundToInt(2f * Mathf.PI * r));
            for (int k = 0; k < steps; k++)
            {
                // Dashes: two texels on, two off, round the circumference.
                if ((k / 2) % 2 == 1) continue;
                float a = k * Mathf.PI * 2f / steps;
                int x = c + Mathf.RoundToInt(Mathf.Cos(a) * r);
                int y = c + Mathf.RoundToInt(Mathf.Sin(a) * r);
                px[y * n + x] = new Color32(255, 255, 255, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), Ppu);
            _rails[r] = s;
            return s;
        }
    }
}
