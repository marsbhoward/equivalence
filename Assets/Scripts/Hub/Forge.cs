using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// Where an earned voucher becomes a real, rolled piece of gear. One object, independent of
    /// roster size, the same reasoning PhotoBooth already settled on - nothing here is tied to
    /// which character is being played.
    ///
    /// Holds no chain or profile state itself - see PhotoBooth's own note on why: an interface or
    /// a cached reference here would be exactly the kind of field that dies on a domain reload.
    /// VoucherCount is a plain int, refreshed by an explicit call from HubRoom whenever the number
    /// could have changed (a run ending, a redemption), the same discipline RefreshBooth uses.
    /// </summary>
    public class Forge : MonoBehaviour
    {
        public Vector2 Anchor { get; private set; }
        public int VoucherCount { get; private set; }

        /// <summary>Whether ANY box path (redeem/targeted/re-roll) or a combine has something to do -
        /// pushed from HubRoom the same way VoucherCount is, since Forge holds no profile state
        /// itself.</summary>
        public bool HasBoxAction { get; private set; }

        SpriteRenderer _base, _hearth, _glow, _sign, _flash;
        float _lit, _flashT;
        bool _focused;

        static readonly Color Stone = new(0.16f, 0.15f, 0.17f);
        static readonly Color Ember = new(1f, 0.55f, 0.15f);

        public static Forge Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("forge");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var f = go.AddComponent<Forge>();
            f.Anchor = centre;

            f._base = Quad(go.transform, "base", 1.3f, 0.85f, Stone, 2, new Vector2(0f, 0.1f));

            // The hearth: what the player is actually looking at when deciding whether a
            // redemption is worth walking over for. Circle rather than square, matching the
            // "impact/glow" family of shapes this project already draws everything warm with.
            f._hearth = Quad(go.transform, "hearth", 0.5f, 0.5f, Ember, 3, new Vector2(0f, 0.32f), Spr.Circle);
            f._glow = Quad(go.transform, "glow", 0.95f, 0.95f, Ember, 1, new Vector2(0f, 0.32f), Spr.Glow);

            f._sign = Quad(go.transform, "sign", 0.9f, 0.15f, Ember, 4, new Vector2(0f, 0.82f));

            f._flash = Quad(go.transform, "flash", 1.8f, 1.8f, Color.white, SortingOrders.Fx, new Vector2(0f, 0.4f));
            f._flash.color = new Color(1f, 1f, 1f, 0f);

            DepthSorted.Attach(go, -0.4f, false, f._base, f._hearth, f._glow, f._sign);

            f.Apply(0f);
            return f;
        }

        public void SetFocus(bool on) => _focused = on;
        public void SetVoucherCount(int count) => VoucherCount = count;
        public void SetHasBoxAction(bool has) => HasBoxAction = has;

        /// <summary>Fires on a successful redemption - feedback for the moment the item actually
        /// exists, not a decoration that plays on approach.</summary>
        public void Flash() => _flashT = 1f;

        void Update()
        {
            if (_hearth == null || _glow == null || _flash == null) return;   // half-built after a reload; wait for the room rebuild

            _lit = Mathf.MoveTowards(_lit, _focused ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            _flashT = Mathf.MoveTowards(_flashT, 0f, Time.unscaledDeltaTime * 2.2f);
            Apply(_lit);
        }

        void Apply(float lit)
        {
            bool ready = VoucherCount > 0 || HasBoxAction;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);

            // Dim and still with nothing to redeem; a slow ember pulse once there is - the same
            // "meter threshold needs a tell" rule the ability-feedback pass already established.
            float hearthGlow = ready ? Mathf.Lerp(0.5f, 1f, Mathf.Max(lit, pulse)) : 0.2f;
            _hearth.color = Color.Lerp(Ember * 0.5f, Ember, hearthGlow);
            _glow.color = new Color(Ember.r, Ember.g, Ember.b,
                ready ? Mathf.Lerp(0.15f, 0.4f, Mathf.Max(lit, pulse)) : 0.04f);
            _sign.color = Color.Lerp(Ember * 0.4f, Ember, ready ? Mathf.Max(lit, 0.5f) : lit * 0.3f);

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
