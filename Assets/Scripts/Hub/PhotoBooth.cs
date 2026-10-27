using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// Where a portrait is taken. ONE object regardless of roster size - not a frame per
    /// character, and not mounted on the couch.
    ///
    /// The couch shows every OTHER character (RefreshRoster filters out whoever is being
    /// played), so anything keyed to a couch seat would have nowhere to put the character
    /// actually standing in the room. A booth sidesteps the question instead of answering it:
    /// walk up as whoever you are playing, and the room asks rather than assumes.
    ///
    /// The object itself holds no chain state - no profile, no wallet reference, nothing that
    /// would be an interface or a delegate on a MonoBehaviour and therefore lost on a domain
    /// reload (see the rule in CLAUDE.md). <see cref="CachedLine"/> and <see cref="Actionable"/>
    /// are plain strings and bools, refreshed by an explicit call from HubRoom - which already
    /// holds the profile as a serializable field and is rebuilt wholesale on the events that
    /// would change the answer (wallet connect, character switch), so the booth never needs to
    /// know why it is being asked to look again.
    /// </summary>
    public class PhotoBooth : MonoBehaviour
    {
        public Vector2 Anchor { get; private set; }

        /// <summary>What the prompt body should say right now. Starts "checking..." until the
        /// first refresh resolves.</summary>
        public string CachedLine { get; private set; } = "checking...";

        /// <summary>Whether pressing the interact key would open the confirm flow.</summary>
        public bool Actionable { get; private set; }

        [SerializeField] HubFixture _art;
        [SerializeField] float _flashT;
        float _lit;
        bool _focused;
        const float Foot = -0.55f;

        /// <summary>The standing mirror (the user's pick, group 6): a tall oval glass in a gilt frame
        /// on clawed feet, shimmering; the portrait is taken in it, and the glass flashes white.</summary>
        public static PhotoBooth Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("photo-booth");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var b = go.AddComponent<PhotoBooth>();
            b.Anchor = centre;
            b._art = HubFixture.Attach(go, HubArt.Mirror(0, false), new Vector2(0f, Foot));
            b._art.Animate(8, 4f, i => HubArt.Mirror(i, b != null && b._flashT > 0.5f));
            DepthSorted.Attach(go, Foot, false, b._art.Art, b._art.RimRenderer);
            return b;
        }

        public void SetFocus(bool on) { _focused = on; if (_art != null) _art.SetFocus(on); }

        /// <summary>Called on a successful take: the glass flashes white and a glint leaves it - the
        /// exact moment the transaction confirmed.</summary>
        public void Flash()
        {
            _flashT = 1f;
            Combat.TexelFx.Star((Vector2)transform.position + new Vector2(-0.12f, Foot + 1.2f), Combat.TexelFx.Steel, 3, 0.16f);
        }

        public void SetLine(string line, bool actionable)
        {
            CachedLine = line;
            Actionable = actionable;
        }

        void Update()
        {
            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            _flashT = Mathf.MoveTowards(_flashT, 0f, Time.unscaledDeltaTime * 2.2f);
        }
    }
}
