using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// A book in a glass case - where a player reads what the room's other systems actually do.
    /// Holds no state of its own: unlike the Forge or the booth there is nothing here that can go
    /// stale (no voucher count, no chain read), so there is no refresh call and no cached line to
    /// worry about surviving a domain reload. SetFocus is the only thing HubRoom ever calls.
    /// </summary>
    public class ManualShrine : MonoBehaviour
    {
        public Vector2 Anchor { get; private set; }

        [SerializeField] HubFixture _art;
        [SerializeField] Convergence.Art.Lighting.Glow _light;
        float _lit;
        bool _focused;

        const float Foot = -0.32f;

        /// <summary>The altar and tome (the user's pick, group 6): a stone altar, a great book open on
        /// it glowing gold, a candle at each end; the candles light a pool in the dim hub.</summary>
        public static ManualShrine Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("manual-shrine");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var m = go.AddComponent<ManualShrine>();
            m.Anchor = centre;
            m._art = HubFixture.Attach(go, HubArt.Altar(0), new Vector2(0f, Foot));
            m._art.Animate(6, 4f, HubArt.Altar);
            m._light = Convergence.Art.Lighting.Glow.Attach(go, 1.6f, new Color(1f, 0.82f, 0.45f), 0.5f, 0.15f);
            DepthSorted.Attach(go, Foot, false, m._art.Art, m._art.RimRenderer);
            return m;
        }

        public void SetFocus(bool on) { _focused = on; if (_art != null) _art.SetFocus(on); }

        void Update()
        {
            if (_light == null) return;
            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            _light.Intensity = 0.5f + 0.25f * _lit;
        }
    }
}
