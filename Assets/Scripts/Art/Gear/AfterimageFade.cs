using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// One of Penumbra's afterimages (ArmPieces.AfterimageEvery): a copy left in the world where the arm and
    /// weapon were, fading from ArmPieces.AfterimageAlpha to nothing over ArmPieces.AfterimageLife, then switched
    /// off for the next drop to reuse. Fades on its own, so a copy outlives the rig that dropped it cleanly.
    /// </summary>
    public sealed class AfterimageFade : MonoBehaviour
    {
        SpriteRenderer _sr;
        float _born;

        public SpriteRenderer Renderer
        {
            get
            {
                // TryGetComponent, not ??: a missing component comes back as Unity's fake null, which ?? passes over
                if (_sr == null && !TryGetComponent(out _sr)) _sr = gameObject.AddComponent<SpriteRenderer>();
                return _sr;
            }
        }

        public void Begin()
        {
            _born = Time.time;
            Renderer.color = new Color(1f, 1f, 1f, ArmPieces.AfterimageAlpha);
            Renderer.enabled = true;
            enabled = true;
        }

        void Update()
        {
            float k = (Time.time - _born) / ArmPieces.AfterimageLife;
            if (k >= 1f) { Renderer.enabled = false; enabled = false; return; }
            Renderer.color = new Color(1f, 1f, 1f, ArmPieces.AfterimageAlpha * (1f - k));
        }
    }
}
