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

        SpriteRenderer _curtainL, _curtainR, _recess, _post, _lens, _glow, _sign, _flash;
        float _lit, _flashT;
        bool _focused;

        static readonly Color Curtain = new(0.30f, 0.11f, 0.16f);
        static readonly Color CurtainDeep = new(0.20f, 0.07f, 0.11f);
        static readonly Color Post = new(0.16f, 0.15f, 0.19f);
        static readonly Color Lens = new(0.62f, 0.78f, 0.92f);

        public static PhotoBooth Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("photo-booth");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var b = go.AddComponent<PhotoBooth>();
            b.Anchor = centre;

            // Two curtain panels either side of a dark recess - something to walk INTO, the way a
            // real photo booth reads from outside, rather than a machine bolted to the wall.
            b._curtainL = Quad(go.transform, "curtainL", 0.46f, 1.55f, Curtain, 2, new Vector2(-0.62f, 0.15f));
            b._curtainR = Quad(go.transform, "curtainR", 0.46f, 1.55f, Curtain, 2, new Vector2(0.62f, 0.15f));
            b._recess = Quad(go.transform, "recess", 0.86f, 1.35f, CurtainDeep, 3, new Vector2(0f, 0.10f));

            b._post = Quad(go.transform, "post", 1.55f, 0.18f, Post, 4, new Vector2(0f, 0.94f));

            // The lens: what the player is actually looking at when deciding to press E.
            b._lens = Quad(go.transform, "lens", 0.34f, 0.34f, Lens, 5, new Vector2(0f, 0.42f), Spr.Circle);
            b._glow = Quad(go.transform, "glow", 0.62f, 0.62f, Lens, 1, new Vector2(0f, 0.42f), Spr.Circle);

            b._sign = Quad(go.transform, "sign", 0.86f, 0.15f, Lens, 6, new Vector2(0f, 1.02f));

            // Renders above the paper-doll band on purpose - a camera flash happens IN FRONT of
            // whoever is standing in the booth, same sorting band Spr.Flash already uses for FX.
            b._flash = Quad(go.transform, "flash", 1.7f, 2.0f, Color.white, SortingOrders.Fx, new Vector2(0f, 0.5f));
            b._flash.color = new Color(1f, 1f, 1f, 0f);

            DepthSorted.Attach(go, -0.55f, false,
                b._curtainL, b._curtainR, b._recess, b._post, b._lens, b._glow, b._sign);

            b.Apply(0f);
            return b;
        }

        public void SetFocus(bool on) => _focused = on;

        /// <summary>Called on a successful take, so the flash reads as feedback for the exact
        /// moment the transaction confirmed rather than a decoration that fires on approach.</summary>
        public void Flash() => _flashT = 1f;

        public void SetLine(string line, bool actionable)
        {
            CachedLine = line;
            Actionable = actionable;
        }

        void Update()
        {
            if (_lens == null || _glow == null || _flash == null) return;   // half-built after a reload; wait for the room rebuild

            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            _flashT = Mathf.MoveTowards(_flashT, 0f, Time.unscaledDeltaTime * 2.2f);
            Apply(_lit);
        }

        void Apply(float lit)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            float lensGlow = Actionable ? Mathf.Lerp(0.4f, 0.9f, Mathf.Max(lit, pulse * 0.5f)) : 0.25f;

            _lens.color = Color.Lerp(Lens * 0.7f, Lens, lensGlow);
            _glow.color = new Color(Lens.r, Lens.g, Lens.b, Actionable ? Mathf.Lerp(0.06f, 0.22f, Mathf.Max(lit, pulse)) : 0.03f);
            _sign.color = Color.Lerp(Lens * 0.5f, Lens, lit);
            _curtainL.color = Color.Lerp(Curtain, Curtain * 1.25f, lit);
            _curtainR.color = Color.Lerp(Curtain, Curtain * 1.25f, lit);

            // Eased down over ~450ms rather than a hard cut, so it reads as a flash fading rather
            // than a frame of the screen turning white.
            _flash.color = new Color(1f, 1f, 1f, _flashT * _flashT * 0.85f);
        }

        static SpriteRenderer Quad(Transform parent, string name, float w, float h, Color color,
                                   int order, Vector2 offset, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : Spr.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
