using System.Collections.Generic;
using UnityEngine;
using Convergence.Art;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// A rim hugging the locked target's own silhouette, saying how a press right now would meet
    /// it. It took over from two things: the reticle (a ring floating over the target, which said
    /// only WHICH enemy) and the reach rings under the player (which said how far, on the ground,
    /// in a circle - while every strike is a capsule along the facing, so the circle claimed reach
    /// to the sides and behind that no swing has).
    ///
    ///     SOLID              a swing lands on it
    ///     DASHED             a sword not yet in reach (the lock runs AcquireMargin past it), or a
    ///                        disc that will THROW rather than swing
    ///     bow / disc throw   alpha follows the shot's distance-damage curve, dim near, full far
    ///
    /// Solid against dashed is a SHAPE channel, so it survives a colourblind player and a busy
    /// screen; the tint never changes. Only the ONE locked enemy is rimmed - rimming everything in
    /// reach would put information everywhere and make none of it stand out, which is the ring's
    /// own problem again.
    ///
    /// The reach test MIRRORS the real ones and must keep doing so: a sword connects when its
    /// capsule (length = PendingRange from the player) overlaps the target's collider, so centre
    /// distance minus collider radius; a disc swings exactly when
    /// <see cref="PlayerController.ThrowsInsteadOfSwinging"/> is false; the ranged curves are
    /// ThrownDisc's and ThrownArrow's `Lerp(near, 1, d / range)`.
    ///
    /// The rim objects are NOT parented to the enemy: it can die, and be destroyed, in any frame.
    /// They copy its renderers' world transforms after everything has animated, and sort one
    /// below the lowest of them, so the enemy's own parts cover the seams between its parts and
    /// only the outer edge shows.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class TargetHighlight : MonoBehaviour
    {
        [SerializeField] PlayerController _player;
        [SerializeField] PlayerTargeting _targeting;

        // Non-readonly and rebuilt lazily: see "Domain reload traps" in CLAUDE.md.
        List<SpriteRenderer> _rims;
        List<SpriteRenderer> _sources;
        float _alpha;
        Health _last;

        public static TargetHighlight Attach(PlayerController player)
        {
            var h = player.gameObject.AddComponent<TargetHighlight>();
            h._player = player;
            h._targeting = player.GetComponent<PlayerTargeting>();
            return h;
        }

        void LateUpdate()
        {
            _rims ??= new List<SpriteRenderer>();
            _sources ??= new List<SpriteRenderer>();

            int used = DrawRefused();
            DrawRing();

            var t = _targeting != null && _targeting.HasTarget ? _targeting.Target : null;
            if (t == null || _player == null || _player.Airborne || _player.Statue
                || _player.Ledger.HideTargetHighlight)   // Fog
            {
                Hide(used);
                _last = null;
                return;
            }

            Read(t, out bool solid, out float wantAlpha);
            // A LOCK reads strongest (the user's call, 2026-11-12): thicker, and never dimmer than LockMinAlpha -
            // solid against dashed still says whether a swing reaches. The soft assist's pick is thin and faint:
            // it says where a swing would go, nothing more.
            bool locked = _targeting.IsLocked;
            wantAlpha = locked ? Mathf.Max(wantAlpha, Tuning.Highlight.LockMinAlpha)
                               : wantAlpha * Tuning.Targeting.AssistRimAlpha;
            float thickness = Tuning.Highlight.Thickness * (locked ? Tuning.Highlight.LockThickness : 1f);

            // A new target starts at its own answer rather than easing in from the last one's.
            if (t != _last) _alpha = wantAlpha;
            _last = t;
            _alpha = Mathf.Lerp(_alpha, wantAlpha, 1f - Mathf.Exp(-Tuning.Highlight.Ease * Time.deltaTime));

            var c = Tuning.Highlight.Tint;
            c.a *= _alpha;
            if (t != _targeting.Refused || !RefusalShowing) used = DrawRims(t, c, !solid, thickness, used);
            Hide(used);
        }

        /// <summary>Rims round <paramref name="t"/>'s body from rim slot <paramref name="used"/> on; the next free slot.</summary>
        int DrawRims(Health t, Color c, bool dashed, float thickness, int used)
        {
            CollectSources(t);
            int lowest = int.MaxValue;
            foreach (var s in _sources) lowest = Mathf.Min(lowest, s.sortingOrder);
            foreach (var src in _sources)
            {
                var st = src.transform;
                var scale = st.lossyScale;
                float texelWorld = Mathf.Abs(scale.y) / Mathf.Max(1f, src.sprite.pixelsPerUnit);
                int pad = Mathf.Max(1, Mathf.RoundToInt(thickness / Mathf.Max(1e-5f, texelWorld)));
                var rimSprite = PixelSprite.Rim(src.sprite, pad, dashed: dashed);
                if (rimSprite == null) continue;

                var rim = RimAt(used++);
                rim.sprite = rimSprite;
                rim.flipX = src.flipX;
                rim.flipY = src.flipY;
                rim.color = c;
                rim.sortingLayerID = src.sortingLayerID;
                rim.sortingOrder = lowest - 1;
                rim.transform.SetPositionAndRotation(st.position, st.rotation);
                rim.transform.localScale = scale;
                rim.enabled = true;
            }
            return used;
        }

        // ------------------------------------------------------------ out of range (the user's call, 2026-11-13)
        //
        // A tap (or Tab) on an enemy past lock range: its rim flashes twice, dashed and warm - mock A. Until the
        // character's first lock, or always with HUD & TOUCH's LOCK RANGE RING, the lock range ring shows on the
        // floor round the player as well - mock E.

        float RefusalAge => _targeting != null ? Time.time - _targeting.RefusedAt : float.PositiveInfinity;
        bool RefusalShowing => RefusalAge < Tuning.Highlight.RefuseSeconds;

        int DrawRefused()
        {
            if (_targeting == null || !RefusalShowing) return 0;
            var t = _targeting.Refused;
            if (t == null || t.IsDead) return 0;
            // Two flashes: on through the first and third fifths of the time.
            int fifth = Mathf.FloorToInt(RefusalAge / Tuning.Highlight.RefuseSeconds * 5f);
            if (fifth != 0 && fifth != 2) return 0;
            return DrawRims(t, Tuning.Highlight.RefuseTint, true, Tuning.Highlight.Thickness * Tuning.Highlight.LockThickness, 0);
        }

        SpriteRenderer _ring;
        static Dictionary<int, Sprite> _ringSprites;

        /// <summary>The lock range ring: dotted, Tuning.Highlight.RingTexels thick at the rim's own texel size (one body
        /// pixel), white to be tinted, centred. Cached per radius in texels.</summary>
        static Sprite RingSprite(float radius)
        {
            _ringSprites ??= new Dictionary<int, Sprite>();
            float ppu = 1f / Tuning.Highlight.Thickness;
            int rT = Mathf.Clamp(Mathf.RoundToInt(radius * ppu), 4, 600);
            if (_ringSprites.TryGetValue(rT, out var hit) && hit != null) return hit;
            int n = rT * 2 + 3;
            var px = new Color32[n * n];
            float c = n / 2f, thick = Tuning.Highlight.RingTexels;
            // Dots of RingDash texels along the circumference, as many gaps.
            int dashes = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * rT / (2f * Tuning.Highlight.RingDash)));
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d > rT || d < rT - thick) continue;
                    float a = (Mathf.Atan2(dy, dx) + Mathf.PI) / (2f * Mathf.PI);
                    if ((Mathf.FloorToInt(a * dashes * 2f) & 1) == 1) continue;
                    px[y * n + x] = new Color32(255, 255, 255, 255);
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels32(px);
            tex.Apply();
            return _ringSprites[rT] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        }

        void DrawRing()
        {
            float age = RefusalAge;
            bool show = age < Tuning.Highlight.RingSeconds && _player != null
                        && (Core.HudPrefs.LockRangeRing || !UI.FirstRunHints.Seen("lock-made"));
            if (!show)
            {
                if (_ring != null && _ring.enabled) _ring.enabled = false;
                return;
            }
            if (_ring == null)
            {
                var go = new GameObject("lock-range-ring");
                _ring = go.AddComponent<SpriteRenderer>();
                _ring.sortingOrder = SortingOrders.GroundFx;
            }
            _ring.sprite = RingSprite(_targeting.LockRadius);
            _ring.transform.position = _player.transform.position;
            var c = Tuning.Highlight.Tint;
            // In over the first tenth, held, then out over the last half.
            float k = age / Tuning.Highlight.RingSeconds;
            c.a = Tuning.Highlight.RingAlpha * Mathf.Clamp01(k * 10f) * Mathf.Clamp01((1f - k) * 2f);
            _ring.color = c;
            _ring.enabled = true;
        }

        /// <summary>How a press right now meets <paramref name="t"/>.</summary>
        void Read(Health t, out bool solid, out float alpha)
        {
            float d = Vector2.Distance(_player.transform.position, t.transform.position);
            var weapon = _player.Weapon;

            // Aimed by hand (the right stick - PlayerController.AimingArt), the art goes along the
            // aim, not at the lock: a target off that line is not met however close it stands.
            if (_player.AimingArt)
            {
                Vector2 to = (Vector2)(t.transform.position - _player.transform.position);
                if (to.sqrMagnitude > 1e-6f
                    && Vector2.Dot(to.normalized, _player.Facing) < Tuning.Highlight.AimConeDot)
                {
                    solid = false;
                    alpha = Tuning.Highlight.Dim;
                    return;
                }
            }

            bool ranged = weapon == Art.Gear.WeaponClass.Bow
                       || (weapon == Art.Gear.WeaponClass.Disc && _player.ThrowsInsteadOfSwinging);
            if (ranged)
            {
                float range = weapon == Art.Gear.WeaponClass.Bow ? _player.PendingRange : _player.ThrowReach;
                float near = _player.RangeNearFraction;
                float worth = near >= 1f ? 1f : Mathf.Lerp(near, 1f, Mathf.Clamp01(d / Mathf.Max(0.01f, range)));
                float k = near >= 1f ? 1f : Mathf.InverseLerp(near, 1f, worth);
                // The bow is never "out of reach" - it locks only where it can shoot - so it stays
                // solid; the disc's throw is the dashed case by definition.
                solid = weapon == Art.Gear.WeaponClass.Bow;
                alpha = Mathf.Lerp(Tuning.Highlight.Dim, Tuning.Highlight.Bright, k);
                return;
            }

            bool reaches;
            if (weapon == Art.Gear.WeaponClass.Disc)
                reaches = true;   // not ranged above means ThrowsInsteadOfSwinging is false: it swings
            else
            {
                var col = t.GetComponent<Collider2D>();
                float radius = col != null ? col.bounds.extents.x : 0f;
                reaches = d - radius <= _player.PendingRange;
            }

            solid = reaches;
            alpha = reaches ? Tuning.Highlight.Bright : Tuning.Highlight.Dim;
        }

        /// <summary>The target's body renderers: its `visual` child and anything under it,
        /// minus overlays (status effects, telegraphs) and anything not currently drawing.</summary>
        void CollectSources(Health t)
        {
            _sources.Clear();
            var visual = t.transform.Find("visual");
            var root = visual != null ? visual : t.transform;
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                if (sr.sortingOrder >= SortingOrders.StatusOverlay) continue;
                _sources.Add(sr);
            }
        }

        SpriteRenderer RimAt(int i)
        {
            // A rim destroyed under us (scene teardown) leaves a dead entry - rebuild it in place.
            while (_rims.Count <= i) _rims.Add(null);
            if (_rims[i] != null) return _rims[i];

            var go = new GameObject("target-highlight");
            var sr = go.AddComponent<SpriteRenderer>();
            _rims[i] = sr;
            return sr;
        }

        void Hide(int from)
        {
            if (_rims == null) return;
            for (int i = from; i < _rims.Count; i++)
                if (_rims[i] != null && _rims[i].enabled) _rims[i].enabled = false;
        }

        void OnDestroy()
        {
            if (_ring != null) Destroy(_ring.gameObject);
            if (_rims == null) return;
            foreach (var r in _rims) if (r != null) Destroy(r.gameObject);
        }
    }
}
