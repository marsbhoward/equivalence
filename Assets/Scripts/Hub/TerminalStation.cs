using UnityEngine;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The terminal in the corner of the room: every checkpoint written this session.
    ///
    /// A brass-bound PRINTING CABINET (the user's pick, group 6): a paper tape curls out of it onto
    /// the floor, a line printing as you watch - a machine that keeps the record, still the room's
    /// one piece of machinery, so the writes read as real rather than as decoration.
    ///
    /// It prints faster while a write is in flight, so a player who steps away mid-transaction can
    /// see from across the room that something is still settling.
    /// </summary>
    public class TerminalStation : MonoBehaviour
    {
        [SerializeField] HubFixture _art;
        [SerializeField] HubTheme _theme;
        [SerializeField] Convergence.Art.Lighting.Glow _light;
        float _lit;
        bool _focused;
        const float Foot = -0.30f;

        public static TerminalStation Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("terminal");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var t = go.AddComponent<TerminalStation>();
            // Dressed in the character's terminal theme (group 8); the Cloister's is the scrying basin.
            t._theme = HubDress.Of(HubItem.Terminal);
            var theme = t._theme;
            t._art = HubFixture.Attach(go, HubThemeArt.Terminal(theme, 0), new Vector2(0f, Foot));
            t._art.Animate(HubThemeArt.TerminalFrames, HubThemeArt.TerminalFps, i => HubThemeArt.Terminal(theme, i));
            t._light = Convergence.Art.Lighting.Glow.Attach(go, 1.2f, HubThemeArt.TerminalLight(theme), 0.3f);
            DepthSorted.Attach(go, Foot, false, t._art.Art, t._art.RimRenderer);
            return t;
        }

        public void SetFocus(bool on) { _focused = on; if (_art != null) _art.SetFocus(on); }

        void Update()
        {
            if (_art == null) return;
            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            // A pending write prints quickly whether or not anyone is standing here - the one piece
            // of state worth reading from across the room.
            bool pending = TxLog.PendingCount > 0;
            var theme = _theme;
            _art.Animate(HubThemeArt.TerminalFrames, HubThemeArt.TerminalFps * (pending ? 4f : 1f), i => HubThemeArt.Terminal(theme, i));
            if (_light != null) _light.Intensity = Mathf.Lerp(0.3f, 0.6f, Mathf.Max(_lit, pending ? 1f : 0f));
        }
    }
}
