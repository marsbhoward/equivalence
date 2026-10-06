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

        SpriteRenderer _glow;
        float _lit;
        bool _focused;

        static readonly Color Stone = new(0.18f, 0.17f, 0.20f);
        static readonly Color Glass = new(0.70f, 0.88f, 0.95f);
        static readonly Color Page = new(0.86f, 0.80f, 0.62f);
        static readonly Color Ink = new(0.40f, 0.34f, 0.22f);
        static readonly Color Gold = new(0.95f, 0.85f, 0.55f);

        public static ManualShrine Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("manual-shrine");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var m = go.AddComponent<ManualShrine>();
            m.Anchor = centre;

            var baseQ = Quad(go.transform, "base", 0.85f, 0.55f, Stone, 2, new Vector2(0f, -0.05f));

            // The open book, under the glass: two pages either side of a spine, each carrying a
            // couple of faint lines so it reads as text rather than a blank card.
            var pageL = Quad(go.transform, "pageL", 0.30f, 0.32f, Page, 3, new Vector2(-0.155f, 0.14f));
            var pageR = Quad(go.transform, "pageR", 0.30f, 0.32f, Page, 3, new Vector2(0.155f, 0.14f));
            var spine = Quad(go.transform, "spine", 0.02f, 0.34f, Ink, 4, new Vector2(0f, 0.14f));
            for (int i = 0; i < 3; i++)
            {
                float ly = 0.22f - i * 0.06f;
                Quad(go.transform, $"lineL{i}", 0.20f, 0.015f, Ink, 4, new Vector2(-0.155f, ly));
                Quad(go.transform, $"lineR{i}", 0.20f, 0.015f, Ink, 4, new Vector2(0.155f, ly));
            }

            // The case: a translucent fill over the whole book, plus a brighter strip near the
            // top edge for a sheen - the same "lit top, dark bottom" cue the liquid-glass recipe
            // elsewhere in this project uses, just simplified to one highlight instead of a ramp.
            var glassColor = new Color(Glass.r, Glass.g, Glass.b, 0.16f);
            Quad(go.transform, "glass", 0.62f, 0.62f, glassColor, 5, new Vector2(0f, 0.16f));
            var sheenColor = new Color(Glass.r, Glass.g, Glass.b, 0.28f);
            Quad(go.transform, "sheen", 0.60f, 0.05f, sheenColor, 6, new Vector2(0f, 0.44f));

            m._glow = Quad(go.transform, "glow", 1.0f, 1.0f, Gold, 1, new Vector2(0f, 0.16f), Spr.Glow);

            DepthSorted.Attach(go, -0.32f, false, baseQ, pageL, pageR, spine, m._glow);

            m.Apply(0f);
            return m;
        }

        public void SetFocus(bool on) => _focused = on;

        void Update()
        {
            if (_glow == null) return;   // half-built after a reload; wait for the room rebuild

            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            Apply(_lit);
        }

        void Apply(float lit)
        {
            // A slow idle shimmer even when nobody's looking - a shrine, not a switch - that
            // brightens further on approach the same way every other hub fixture's glow does.
            float idle = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.8f);
            float alpha = Mathf.Lerp(0.10f, 0.22f, idle) + lit * 0.18f;
            _glow.color = new Color(Gold.r, Gold.g, Gold.b, alpha);
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
