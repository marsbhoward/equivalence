using UnityEngine;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// A fading silhouette of the player left where they stood - Wanderblade's blink (the user's
    /// pick, group 2 - "after-images"). A frozen RigSilhouette of the rig at that moment, sorted
    /// as a body standing there, fading like the dash's figures, then gone.
    /// </summary>
    public class RigGhost : MonoBehaviour
    {
        [SerializeField] RigSilhouette _sil;
        [SerializeField] float _life, _left;

        public static void Drop(Transform owner, Color tint, float seconds)
        {
            var rig = owner != null ? owner.GetComponentInChildren<Art.Gear.ICharacterRig>() as MonoBehaviour : null;
            if (rig == null) return;
            var go = new GameObject("blink.ghost");
            var g = go.AddComponent<RigGhost>();
            g._sil = RigSilhouette.Create(go.transform, rig.transform);
            if (g._sil == null) { Destroy(go); return; }
            g._sil.SetColor(tint);
            g._sil.SetOrder(SortingOrders.ForDepth(owner.position.y));
            g._sil.SetVisible(true);
            g._sil.Sync();   // frozen at this frame
            g._life = g._left = Mathf.Max(0.01f, seconds);
        }

        void Update()
        {
            _left -= Time.deltaTime;
            if (_left <= 0f || _sil == null) { Destroy(gameObject); return; }
            float k = _left / _life;
            _sil.Recolor(k * k);
        }
    }
}
