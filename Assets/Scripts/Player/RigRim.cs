using System.Collections.Generic;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// The ledger's moments told on the figure's own outline (the user's picks, 2026-10-26, group 3):
    /// a WARD READY (Aegis, Viriditas's shield, the board's gold shield) is a GLINT - the rim lit in
    /// the ward's colour from the feet up, layer by layer, then gone; a BLOW TURNED (Aegis spending
    /// its ward, Vapour, Viriditas's shield breaking) is a FLARE - the rim white for a few frames,
    /// then broken (dashed) and fading as specks.
    ///
    /// FlameOutline's method - a rim of every rig layer on that layer's own transform, sorted ONE
    /// BELOW the rig's lowest layer, so only the figure's outer edge shows and nothing lies over the
    /// player - but its own component, so it never fights Ignite's outline for the same renderers.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class RigRim : MonoBehaviour
    {
        const float GlintSeconds = 0.45f, GlintSweep = 0.3f, FlareSeconds = 0.4f, FlareSolid = 0.07f;

        [SerializeField] float _startedAt = -99f;
        [SerializeField] bool _flare;
        [SerializeField] Color _colour = Color.white;
        List<SpriteRenderer> _sources, _rims;

        /// <summary>A ward ready: the rim lit in <paramref name="colour"/>, feet to head.</summary>
        public static void Glint(Transform player, Color colour) => Start(player, colour, false);

        /// <summary>A blow turned: the rim flared white, breaking up.</summary>
        public static void Flare(Transform player) => Start(player, Color.white, true);

        static void Start(Transform player, Color colour, bool flare)
        {
            if (player == null) return;
            var r = player.GetComponent<RigRim>();
            if (r == null) r = player.gameObject.AddComponent<RigRim>();
            r._startedAt = Time.time;
            r._flare = flare;
            r._colour = colour;
        }

        void LateUpdate()
        {
            _sources ??= new List<SpriteRenderer>();
            _rims ??= new List<SpriteRenderer>();
            float age = Time.time - _startedAt, life = _flare ? FlareSeconds : GlintSeconds;
            if (age < 0f || age > life) { Hide(0); return; }

            var rig = GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour;
            if (rig == null) { Hide(0); return; }

            _sources.Clear();
            int lowest = int.MaxValue;
            float bottom = float.MaxValue, top = float.MinValue;
            foreach (var sr in rig.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (sr == null || !sr.enabled || sr.sprite == null || sr.forceRenderingOff) continue;
                if (sr.sortingOrder >= SortingOrders.StatusOverlay || sr.name == HitFlash.ChildName) continue;
                _sources.Add(sr);
                lowest = Mathf.Min(lowest, sr.sortingOrder);
                bottom = Mathf.Min(bottom, sr.bounds.min.y); top = Mathf.Max(top, sr.bounds.max.y);
            }
            if (_sources.Count == 0) { Hide(0); return; }

            float k = age / life;
            bool dashed = _flare && age > FlareSolid;
            // The glint's line climbs the figure; a layer is lit once the line passes its middle.
            float line = Mathf.Lerp(bottom, top + 0.1f, Mathf.Clamp01(age / GlintSweep));

            int used = 0;
            foreach (var src in _sources)
            {
                float mid = src.bounds.center.y;
                Color c;
                if (_flare) { c = Color.white; c.a = age < FlareSolid ? 1f : 1f - (age - FlareSolid) / (life - FlareSolid); }
                else
                {
                    if (mid > line) continue;
                    bool atLine = line - mid < 0.12f && age < GlintSweep;
                    c = atLine ? Color.Lerp(_colour, Color.white, 0.7f) : _colour;
                    c.a = 1f - Mathf.Clamp01((age - GlintSweep) / (life - GlintSweep));
                }

                var st = src.transform;
                var scale = st.lossyScale;
                float texelWorld = Mathf.Abs(scale.y) / Mathf.Max(1f, src.sprite.pixelsPerUnit);
                int pad = Mathf.Max(1, Mathf.RoundToInt(Tuning.Highlight.Thickness / Mathf.Max(1e-5f, texelWorld)));
                var rimSprite = Art.PixelSprite.Rim(src.sprite, pad, dashed);
                if (rimSprite == null) continue;
                var rim = RimAt(used++);
                rim.sprite = rimSprite;
                rim.flipX = src.flipX; rim.flipY = src.flipY;
                rim.color = c;
                rim.sortingLayerID = src.sortingLayerID;
                rim.sortingOrder = lowest - 1;
                // The flare's specks drift up and out as it breaks.
                var drift = dashed ? new Vector3(0f, (age - FlareSolid) * 0.25f, 0f) : Vector3.zero;
                rim.transform.SetPositionAndRotation(st.position + drift, st.rotation);
                rim.transform.localScale = scale;
                rim.enabled = true;
            }
            Hide(used);
        }

        SpriteRenderer RimAt(int i)
        {
            while (_rims.Count <= i) _rims.Add(null);
            if (_rims[i] != null) return _rims[i];
            return _rims[i] = new GameObject("ledger.rim").AddComponent<SpriteRenderer>();
        }

        void Hide(int from)
        {
            if (_rims == null) return;
            for (int i = from; i < _rims.Count; i++)
                if (_rims[i] != null && _rims[i].enabled) _rims[i].enabled = false;
        }

        void OnDestroy()
        {
            if (_rims != null) foreach (var r in _rims) if (r != null) Destroy(r.gameObject);
        }
    }
}
