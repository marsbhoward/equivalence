using System.Collections.Generic;
using UnityEngine;
using Convergence.Art;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// LUCID (Fog's Albedo): every enemy winding up an attack is outlined while it does - the
    /// read a skilled player makes off the telegraph, handed over as a rim on the body itself.
    ///
    /// The same rim as Player.TargetHighlight (PixelSprite.Rim on every renderer of the body,
    /// one below its lowest), in a warning amber rather than the target's colour, and never
    /// parented to the enemy - a rim copies its renderers' transforms each LateUpdate, since the
    /// body can be destroyed any frame. Plain value fields and lazily rebuilt lists, for the
    /// domain reload rule.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class LucidRims : MonoBehaviour
    {
        static readonly Color Warning = new(1f, 0.62f, 0.18f, 0.95f);

        [SerializeField] PlayerController _player;
        List<SpriteRenderer> _rims;
        List<SpriteRenderer> _sources;

        /// <summary>On or off for the run's player - from the ledger, whenever it changes.</summary>
        public static void Set(PlayerController player, bool on)
        {
            if (player == null) return;
            var r = player.GetComponent<LucidRims>();
            if (on && r == null)
            {
                r = player.gameObject.AddComponent<LucidRims>();
                r._player = player;
            }
            if (r != null) r.enabled = on;
            if (!on && r != null) r.Hide(0);
        }

        /// <summary>The run ended: every live set of rims goes.</summary>
        public static void Clear()
        {
            foreach (var r in FindObjectsByType<LucidRims>(FindObjectsSortMode.None))
                if (r != null) { r.Hide(0); Destroy(r); }
        }

        void LateUpdate()
        {
            _rims ??= new List<SpriteRenderer>();
            _sources ??= new List<SpriteRenderer>();
            int used = 0;

            Enemies.EnemyRegistry.Prune();
            foreach (var e in Enemies.EnemyRegistry.All)
            {
                if (e == null) continue;
                var hp = e.GetComponent<Health>();
                if (hp == null || hp.IsDead) continue;
                float wind = Mathf.Max(e.TelegraphProgress01, e.Kind == Enemies.EnemyKind.Turret ? e.TurretChargeProgress01 : 0f);
                if (wind <= 0.02f) continue;

                CollectSources(hp);
                int lowest = int.MaxValue;
                foreach (var s in _sources) lowest = Mathf.Min(lowest, s.sortingOrder);
                var c = Warning;
                c.a *= Mathf.Lerp(0.45f, 1f, wind);

                foreach (var src in _sources)
                {
                    var st = src.transform;
                    var scale = st.lossyScale;
                    float texelWorld = Mathf.Abs(scale.y) / Mathf.Max(1f, src.sprite.pixelsPerUnit);
                    int pad = Mathf.Max(1, Mathf.RoundToInt(Tuning.Highlight.Thickness / Mathf.Max(1e-5f, texelWorld)));
                    var rimSprite = PixelSprite.Rim(src.sprite, pad, dashed: false);
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
            }
            Hide(used);
        }

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
            while (_rims.Count <= i) _rims.Add(null);
            if (_rims[i] != null) return _rims[i];
            var go = new GameObject("lucid-rim");
            var sr = go.AddComponent<SpriteRenderer>();
            _rims[i] = sr;
            return sr;
        }

        void Hide(int from)
        {
            if (_rims == null) return;
            for (int i = from; i < _rims.Count; i++)
                if (_rims[i] != null) _rims[i].enabled = false;
        }

        void OnDestroy()
        {
            if (_rims == null) return;
            foreach (var r in _rims) if (r != null) Destroy(r.gameObject);
        }
    }
}
