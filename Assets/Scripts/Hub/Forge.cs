using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// Where boxes become gear and matching pieces combine. One object, independent of
    /// roster size, the same reasoning PhotoBooth already settled on - nothing here is tied to
    /// which character is being played.
    ///
    /// Holds no chain or profile state itself - see PhotoBooth's own note on why: an interface or
    /// a cached reference here would be exactly the kind of field that dies on a domain reload.
    /// HasBoxAction is a plain bool, refreshed by an explicit call from HubRoom whenever it could
    /// have changed (a run ending, a redemption), the same discipline RefreshBooth uses.
    /// </summary>
    public class Forge : MonoBehaviour
    {
        public Vector2 Anchor { get; private set; }
        /// <summary>Whether ANY box path (redeem/targeted/re-roll) or a combine has something to do -
        /// pushed from HubRoom, since Forge holds no profile state itself.</summary>
        public bool HasBoxAction { get; private set; }

        [SerializeField] HubFixture _art;
        [SerializeField] Convergence.Art.Lighting.Glow _light;
        float _lit;
        bool _focused;

        const float Foot = -0.4f;

        /// <summary>The hearth and anvil (the user's pick, group 6): a stone hearth whose fire burns
        /// full while there is something to do and sinks to embers otherwise; its light pools in the
        /// dim hub. No sign or soft glow.</summary>
        public static Forge Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("forge");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var f = go.AddComponent<Forge>();
            f.Anchor = centre;
            f._art = HubFixture.Attach(go, HubArt.Hearth(0, false), new Vector2(0f, Foot));
            f._art.Animate(8, 10f, i => HubArt.Hearth(i, f != null && f.HasBoxAction));
            f._light = Convergence.Art.Lighting.Glow.Attach(go, 2.4f, new Color(1f, 0.55f, 0.2f), 0.4f, 0.25f);
            DepthSorted.Attach(go, Foot, false, f._art.Art, f._art.RimRenderer);
            return f;
        }

        public void SetFocus(bool on) { _focused = on; if (_art != null) _art.SetFocus(on); }
        public void SetHasBoxAction(bool has) => HasBoxAction = has;

        /// <summary>Fires on a successful redemption: sparks leap from the hearth - feedback for the
        /// moment the item actually exists.</summary>
        public void Flash()
            => Combat.TexelFx.Spray((Vector2)transform.position + new Vector2(0f, Foot + 0.35f), Combat.TexelFx.Fire, 0.7f, 22, 0.6f);

        void Update()
        {
            if (_light == null) return;
            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
            _light.Intensity = HasBoxAction ? Mathf.Lerp(0.7f, 1f, Mathf.Max(_lit, pulse)) : 0.35f;
        }
    }
}
