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

            var t = _targeting != null && _targeting.HasTarget ? _targeting.Target : null;
            if (t == null || _player == null || _player.Airborne || _player.Statue
                || _player.Ledger.HideTargetHighlight)   // Fog
            {
                Hide(0);
                _last = null;
                return;
            }

            Read(t, out bool solid, out float wantAlpha);

            // A new target starts at its own answer rather than easing in from the last one's.
            if (t != _last) _alpha = wantAlpha;
            _last = t;
            _alpha = Mathf.Lerp(_alpha, wantAlpha, 1f - Mathf.Exp(-Tuning.Highlight.Ease * Time.deltaTime));

            CollectSources(t);
            int lowest = int.MaxValue;
            foreach (var s in _sources) lowest = Mathf.Min(lowest, s.sortingOrder);

            var c = Tuning.Highlight.Tint;
            c.a *= _alpha;

            int used = 0;
            foreach (var src in _sources)
            {
                var st = src.transform;
                var scale = st.lossyScale;
                float texelWorld = Mathf.Abs(scale.y) / Mathf.Max(1f, src.sprite.pixelsPerUnit);
                int pad = Mathf.Max(1, Mathf.RoundToInt(Tuning.Highlight.Thickness / Mathf.Max(1e-5f, texelWorld)));
                var rimSprite = PixelSprite.Rim(src.sprite, pad, dashed: !solid);
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
            Hide(used);
        }

        /// <summary>How a press right now meets <paramref name="t"/>.</summary>
        void Read(Health t, out bool solid, out float alpha)
        {
            float d = Vector2.Distance(_player.transform.position, t.transform.position);
            var weapon = _player.Weapon;

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
            if (_rims == null) return;
            foreach (var r in _rims) if (r != null) Destroy(r.gameObject);
        }
    }
}
