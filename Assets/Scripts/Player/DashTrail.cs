using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// Dash's identity: pale figures left along the ground the character actually covered.
    ///
    /// THE TRAIL IS THE IMMUNITY WINDOW. Figures drop by distance TRAVELLED, and each lives
    /// exactly <see cref="Tuning.Defense.DashSeconds"/> - so the last one fading is the frame the
    /// i-frames end, and a dash cut short against a wall leaves a short trail rather than the
    /// three it would have drawn in the open. Neither is decoration timed to look about right;
    /// both are the mechanic drawn at 1:1.
    ///
    /// Each figure is a <see cref="RigSilhouette"/> - a frozen snapshot of the player's own
    /// renderers - which is what makes it an after-image rather than a ghost that happens to be
    /// nearby: the pose is whatever the player was actually in at that step, carrying their gear,
    /// their lean and their weapon at the angle they were holding it.
    ///
    /// Attached only when the run's chest actually grants Dash, so every other build pays nothing
    /// for it. The ability is locked for the run, so that test is made once and trusted.
    /// </summary>
    public class DashTrail : MonoBehaviour
    {
        /// <summary>One dropped figure: a frozen silhouette and the clock it fades on.</summary>
        class Figure
        {
            public RigSilhouette Sil;
            public float Life;
            public float LifeMax;
        }

        // Non-readonly - see the reload note on RigSilhouette's own lists.
        List<Figure> _figures = new();

        PlayerController _pc;
        Transform _root;

        bool _dashing;
        Vector3 _lastDrop;
        float _remaining;

        public static DashTrail Attach(PlayerController pc)
        {
            if (pc == null) return null;
            var trail = pc.GetComponent<DashTrail>() ?? pc.gameObject.AddComponent<DashTrail>();
            trail._pc = pc;
            return trail;
        }

        void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        /// <summary>
        /// Called as the dash launches. Drops a figure immediately at the launch point - the spot
        /// left behind is the most legible part of a dash - then one per
        /// <see cref="Tuning.Defense.DashTrailSpacing"/> units until the window closes.
        /// </summary>
        public void Begin(float seconds)
        {
            if (_pc == null) return;

            _dashing = true;
            _remaining = seconds;
            _lastDrop = _pc.transform.position;
            Drop(_lastDrop);
        }

        void LateUpdate()
        {
            Fade();

            if (!_dashing || _pc == null) return;

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f) { _dashing = false; return; }

            // Distance travelled, not elapsed time. A dash that ends early against a wall has
            // covered less ground and is entitled to fewer figures.
            var here = _pc.transform.position;
            float spacing = Tuning.Defense.DashTrailSpacing;
            if ((here - _lastDrop).sqrMagnitude < spacing * spacing) return;

            _lastDrop = here;
            Drop(here);
        }

        void Fade()
        {
            if (_figures == null) return;

            foreach (var f in _figures)
            {
                if (f == null || f.Sil == null || f.Life <= 0f) continue;

                f.Life -= Time.deltaTime;
                if (f.Life <= 0f) { f.Sil.SetVisible(false); continue; }

                // Squared, so a figure holds most of its presence and then goes quickly. A linear
                // fade spends half its life at half opacity, which reads as the after-image being
                // weak rather than as it leaving. ShadowEcho.Update makes the same call.
                float t = f.Life / f.LifeMax;
                f.Sil.Recolor(t * t);
            }
        }

        void Drop(Vector3 at)
        {
            var f = Take();
            if (f == null) return;

            f.Sil.SetVisible(true);

            // Captured BEFORE the order is set, since Sync writes the order it was last given.
            // Sorted as a body standing at that spot, the same way the player and every enemy
            // are - an after-image is something left on the floor and should be walked in front
            // of and behind like one.
            f.Sil.SetOrder(SortingOrders.ForDepth(at.y));
            f.Sil.Sync();

            f.LifeMax = f.Life = Tuning.Defense.DashTrailFadeSeconds;
        }

        Figure Take()
        {
            _figures ??= new List<Figure>();

            foreach (var f in _figures)
                if (f != null && f.Sil != null && f.Life <= 0f) return f;

            if (_figures.Count >= Tuning.Defense.DashTrailMaxFigures)
            {
                // Every figure still fading, which needs a dash to start before the previous
                // one's trail has gone - only reachable with enough cooldown reduction to bring
                // DashCooldown under DashSeconds. Re-use the oldest rather than grow: one figure
                // cut short is invisible, an unbounded pool is not. EchoChorus.Take, verbatim.
                var reused = _figures[0];
                _figures.RemoveAt(0);
                _figures.Add(reused);
                return reused;
            }

            if (_root == null)
            {
                // BESIDE the player, never under it. A figure parented to a moving transform
                // travels with the character, which is the one thing an after-image must not do.
                // EchoChorus.Root makes the same call for the same reason.
                var rootGo = new GameObject("dash-trail");
                rootGo.transform.SetParent(transform.parent, false);
                _root = rootGo.transform;
            }

            var rigRoot = (_pc.Rig as MonoBehaviour)?.transform;
            var sil = RigSilhouette.Create(_root, rigRoot);
            if (sil == null) return null;

            sil.SetColor(Tuning.Defense.DashTrailColor);

            var made = new Figure { Sil = sil };
            _figures.Add(made);
            return made;
        }
    }
}
