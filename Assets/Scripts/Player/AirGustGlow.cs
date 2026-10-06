using UnityEngine;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// The yellow ring marking Gust as active - see <see cref="AirResource"/>'s own summary of
    /// why the ability moved from a fired cone to a held self-buff. A timed buff with no tell is
    /// a buff the player cannot plan around (the exact reasoning water's own surge already
    /// documents), and this one changes attack range and move speed - both things a player is
    /// actively relying on mid-fight - so the tell has to be something seen at a glance, not read
    /// off a HUD number.
    ///
    /// A STANDALONE OVERLAY, not a rig layer - <see cref="Art.Gear.ICharacterRig"/> has no
    /// generic tint/outline hook (only the binary white hit-flash), and adding one would mean
    /// implementing it twice (<see cref="Art.Gear.PrimitiveCharacterRig"/> and
    /// <see cref="Art.Gear.SpriteLibraryCharacterRig"/>) for a cosmetic that has nothing to do
    /// with gear. <see cref="Art.Gear.SaintHalo"/> already sets the precedent for a decorative
    /// ring bolted onto a character from outside the rig interface.
    ///
    /// Sorted at <see cref="SortingOrders.StatusOverlay"/> - an OVERLAY per the sorting rules in
    /// the project notes, so it must never be handed to <see cref="DepthSorted"/>.
    /// </summary>
    public class AirGustGlow : MonoBehaviour
    {
        static readonly Color GustYellow = new(1f, 0.85f, 0.2f);

        SpriteRenderer _ring;
        float _t;

        public static AirGustGlow Attach(Transform parent)
        {
            // Reused rather than doubled - same domain-reload/re-trigger guard SaintHalo.Attach
            // documents for its own ring.
            var existing = parent.GetComponentInChildren<AirGustGlow>(true);
            if (existing != null) return existing;

            var go = new GameObject("air.gustGlow");
            go.transform.SetParent(parent, false);

            var g = go.AddComponent<AirGustGlow>();
            g._ring = go.AddComponent<SpriteRenderer>();
            g._ring.sprite = Spr.ThinRing;
            g._ring.color = GustYellow;
            g._ring.sortingOrder = SortingOrders.StatusOverlay;
            go.transform.localScale = Vector3.one * 1.4f;   // just outside the body silhouette

            go.SetActive(false);
            return g;
        }

        public void SetShown(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        public void Toggle()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
            else gameObject.SetActive(true);
        }

        void Update()
        {
            if (_ring == null) return;
            _t += Time.deltaTime;

            // A slow breath rather than a flat colour, so the ring reads as lit rather than
            // printed - same reasoning SaintHalo's own brightness breath uses.
            float lit = 0.75f + 0.25f * Mathf.Sin(_t * 4f);
            _ring.color = new Color(GustYellow.r, GustYellow.g, GustYellow.b, lit);
        }
    }
}
