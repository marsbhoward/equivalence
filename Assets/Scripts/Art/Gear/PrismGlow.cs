using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// A soft pulsing glow over whichever of Prism's four gems is lit.
    ///
    /// The baked sprite alone (see <see cref="GearItem.BladeFor(Core.ElementType)"/>) already
    /// tells the LIT gem from the three dim ones by colour, but a flat texel of colour on a sword
    /// held at arm's length is a small thing to carry a run's whole identity - the same reasoning
    /// that gives the weapon-heat cycle a growing blast and not just a tinted blade. A breathing
    /// light over the gem is the confirmation: the sword doesn't just wear the colour, it is lit
    /// from within by it.
    ///
    /// RUN-SCOPED, not wired through <see cref="CharacterRigFactory"/> the way the Rift Blade's
    /// shards or Saint's halo are. Those react only to which ITEM is drawn, which
    /// CharacterRigFactory.Paint always knows; this reacts to which ELEMENT is being played, which
    /// only exists during a run and has no meaning in the hub. It is attached and repositioned
    /// from GameBootstrap instead, at the same two points the blade sprite itself is chosen.
    /// </summary>
    public class PrismGlow : MonoBehaviour
    {
        SpriteRenderer _sr;
        SpriteRenderer _weapon;
        Color _tint;
        float _t;

        public static PrismGlow Attach(Transform parent, float along, Color tint, SpriteRenderer weapon)
        {
            var glow = parent.GetComponentInChildren<PrismGlow>(true);
            if (glow == null)
            {
                var go = new GameObject("prism.glow");
                go.transform.SetParent(parent, false);
                glow = go.AddComponent<PrismGlow>();

                var spriteGo = new GameObject("glow");
                spriteGo.transform.SetParent(go.transform, false);
                spriteGo.transform.localScale = Vector3.one * 0.24f;
                glow._sr = spriteGo.AddComponent<SpriteRenderer>();
                glow._sr.sprite = Spr.Glow;
            }

            // Re-taken every call, never latched behind the null-check above - the position moves
            // when the lit element changes and the weapon renderer can be replaced by a repaint,
            // the exact trap SaintHalo's own note documents for the identical reason.
            glow.transform.localPosition = new Vector3(0f, along, 0f);
            glow._tint = tint;
            glow._weapon = weapon;
            return glow;
        }

        public void SetShown(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        void Update()
        {
            if (_sr == null) return;
            _t += Time.deltaTime;

            // A breathing pulse, the same idiom SaintHalo's own brightness cycle uses, so a lit
            // gem reads as alive rather than as a static decal.
            float pulse = 0.5f + 0.4f * Mathf.Sin(_t * 2.2f);
            _sr.color = new Color(_tint.r, _tint.g, _tint.b, pulse);

            if (_weapon != null)
            {
                _sr.sortingOrder = _weapon.sortingOrder + 1;
                _sr.sortingLayerID = _weapon.sortingLayerID;
            }
        }
    }
}
