using System.Collections.Generic;
using UnityEngine;
using Convergence.Art;

namespace Convergence.Player
{
    /// <summary>
    /// A flat, single-coloured copy of a character rig, built by MIRRORING that rig's own
    /// SpriteRenderers rather than by constructing a second rig.
    ///
    /// This is the primitive under Dash's after-images and Bulwark's aura, and it exists because
    /// <see cref="Combat.ShadowEcho"/> - the project's other duplicate-of-the-player - answers a
    /// different question. An echo has to PERFORM: it plays a swing the player is not playing, so
    /// it needs a rig of its own with its own animation state, and paying twenty-two GameObjects
    /// and a paint for that is correct. Neither of these two performs. Both want the figure
    /// exactly as it is posed right now, which a second rig is the hard way to get: it would have
    /// to have the whole live pose - lean, aim, cape spring, swing residual - replicated onto it
    /// every frame, and a first attempt at Dash that skipped that step dropped three figures
    /// standing in the rig's REST pose while the player ran past them holding a sword at a
    /// completely different angle.
    ///
    /// Copying sprite-and-transform off the originals gets the pose right by construction, needs
    /// no paint lambda and no opinion about loadouts, and costs renderers instead of rigs.
    ///
    /// The colour lands flat because every sprite is swapped for its
    /// <see cref="PixelSprite.Silhouette"/>. That swap is the only thing in the project that can
    /// BRIGHTEN pixel art at all - a renderer tint multiplies, so no tint composed any way round
    /// can lift baked colour toward a pale cyan.
    ///
    /// All mirrors share ONE sorting order, which is only safe because they are one flat colour:
    /// with no internal contrast there is nothing for an arbitrary draw order between them to get
    /// wrong, and the twenty-odd orders a real rig needs are simply not required here.
    /// </summary>
    public class RigSilhouette : MonoBehaviour
    {
        // Non-readonly, and rebuilt when found empty. A readonly collection comes back freshly
        // initialised from a domain reload while every renderer it pointed at survives, which
        // would weld a second set of mirrors over the first. See CLAUDE.md's reload traps.
        List<SpriteRenderer> _sources = new();
        List<SpriteRenderer> _mirrors = new();

        [SerializeField] Transform _rigRoot;
        Color _color = Color.white;
        float _swell = 1f;
        int _order;

        /// <summary>
        /// Build one under <paramref name="parent"/>, mirroring <paramref name="rigRoot"/>.
        ///
        /// The rig's OWN subtree, never the player GameObject's: the player also carries the
        /// range rings on the ground and GuardRing and BarrierDome above, and mirroring any of
        /// those would hang a flat copy of a circle in the air. Taking the rig root is the test
        /// that stays correct as more overlays are added, where a filter on names or sorting
        /// orders would need editing every time one is.
        /// </summary>
        public static RigSilhouette Create(Transform parent, Transform rigRoot)
        {
            if (rigRoot == null) return null;

            var go = new GameObject("rig-silhouette");
            go.transform.SetParent(parent, false);

            var s = go.AddComponent<RigSilhouette>();
            s._rigRoot = rigRoot;
            s.Rebuild();
            s.SetVisible(false);
            return s;
        }

        void Rebuild()
        {
            _sources ??= new List<SpriteRenderer>();
            _mirrors ??= new List<SpriteRenderer>();
            if (_mirrors.Count > 0 && _mirrors[0] != null) return;
            if (_rigRoot == null) return;

            _sources.Clear();
            _mirrors.Clear();

            foreach (var sr in _rigRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || sr.transform.IsChildOf(transform)) continue;

                // The contact shadow is a patch on the GROUND, not part of the figure. Copied in,
                // it would leave a flat disc floating under every after-image.
                if (sr.gameObject.name == "shadow") continue;

                var go = new GameObject("sil." + sr.gameObject.name);
                go.transform.SetParent(transform, false);
                var mirror = go.AddComponent<SpriteRenderer>();
                mirror.enabled = false;

                _sources.Add(sr);
                _mirrors.Add(mirror);
            }
        }

        public void SetColor(Color c) => _color = c;
        public void SetOrder(int order) => _order = order;

        /// <summary>
        /// How far each layer grows about ITS OWN pivot. Deliberately per-layer rather than a
        /// scale on this whole object: scaling the figure would lift the feet clear of the ground
        /// and slide the head up off the shoulders, where swelling each layer in place thickens
        /// the outline with every joint still exactly where the character's is.
        /// </summary>
        public void SetSwell(float swell) => _swell = swell;

        public void SetVisible(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        /// <summary>
        /// Copy the source rig's current sprites and world transforms across. Called once for a
        /// frozen after-image, or every frame for something that tracks the live figure.
        /// </summary>
        public void Sync()
        {
            Rebuild();

            for (int i = 0; i < _mirrors.Count; i++)
            {
                var src = _sources[i];
                var dst = _mirrors[i];
                if (src == null || dst == null) continue;

                if (!src.enabled || src.sprite == null) { dst.enabled = false; continue; }

                dst.enabled = true;
                dst.sprite = PixelSprite.Silhouette(src.sprite);
                dst.sortingOrder = _order;
                dst.flipX = src.flipX;
                dst.flipY = src.flipY;

                // WORLD space. Matching local transforms instead would need this hierarchy to
                // duplicate the rig's own nesting - hips inside root, torso inside hips - and
                // every future change to that tree would then have to be made twice.
                var t = src.transform;
                dst.transform.position = t.position;
                dst.transform.rotation = t.rotation;
                dst.transform.localScale = t.lossyScale * _swell;
            }

            Recolor(1f);
        }

        /// <summary>Alpha only, for a fade - no transform or sprite work, so it is cheap per frame.</summary>
        public void Recolor(float alpha)
        {
            var c = new Color(_color.r, _color.g, _color.b, _color.a * alpha);
            foreach (var m in _mirrors)
                if (m != null) m.color = c;
        }

        /// <summary>The lowest order the source rig is currently drawing at, for placing a copy just under it.</summary>
        public int LowestSourceOrder(int fallback)
        {
            int lowest = int.MaxValue;
            foreach (var sr in _sources)
                if (sr != null && sr.enabled && sr.sortingOrder < lowest) lowest = sr.sortingOrder;
            return lowest == int.MaxValue ? fallback : lowest;
        }
    }
}
