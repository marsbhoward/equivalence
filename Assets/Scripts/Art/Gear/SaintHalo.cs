using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// The golden ring hovering at the middle of Saint's blade.
    ///
    /// IT GENUINELY ENCIRCLES THE BLADE, and that is the whole point of it - a ring drawn behind a
    /// sword is a decal, and this has to read as a hoop the blade is passing through. Two
    /// renderers, one sprite each:
    ///
    ///     the FULL ring    behind the weapon layer - the far arc
    ///     the NEAR half    in front of it - the arc that passes over the blade
    ///
    /// There is no way to get that from one sprite at one sorting order, which is why
    /// <see cref="Spr.HalfRing"/> exists at all.
    ///
    /// IT ALSO TURNS. The vertical scale runs on |sin| so the hoop squashes to an edge and opens
    /// out again, which is what a ring rotating about its own horizontal axis does to a flat
    /// viewer. Held at a fixed squash it reads as a static ellipse - a shape rather than an object -
    /// and the whole illusion rests on it never quite stopping.
    ///
    /// PARENTED TO THE WEAPON so it follows a swing, and slid up the blade first: the anchor is the
    /// grip, and a halo centred there hangs around the character's hands instead of the blade. The
    /// same correction the Rift Blade's shards needed.
    ///
    /// ITS ORDERS ARE READ OFF THE WEAPON EVERY FRAME, never set once. This is the trap that cost
    /// the first version: the rig is y-sorted, so `DepthSorted` hands `SetSortingBase` a depth and
    /// every rig layer moves - measured at 11524..11545 - while the halo, which the rig has never
    /// heard of, sat where it was built at 30 and 32 and drew behind the entire character. It
    /// LOOKED right in a still, because the ring is wider than the blade and most of it is over
    /// bare floor either way.
    ///
    /// Derived rather than registered for the same reason `EchoChorus` derives its count: the
    /// weapon's own order also moves when the class changes the layer stack (`TwoHandedOrder`
    /// promotes the front arm above the weapon), so anything computed once from `RigLayer.Weapon`
    /// is wrong the moment a permutation applies.
    /// </summary>
    public class SaintHalo : MonoBehaviour
    {
        static readonly Color Gold = new(0.98f, 0.83f, 0.42f);

        SpriteRenderer _far, _near, _weapon;
        float _size = 0.5f;
        float _t;

        public static SaintHalo Attach(Transform parent, float size, SpriteRenderer weapon, float along)
        {
            // Reused rather than doubled if something asks twice - a weapon repainted mid-run
            // would otherwise weld a second ring on, the same trap EnsureLayers documents for the
            // rig's own layers. The renderer is RE-TAKEN on the way through: `Apply` can replace
            // the weapon layer's renderer, and a halo still pointing at the old one silently stops
            // tracking the sort. (A domain reload is the same case - the halo GameObject survives,
            // and one built before this field existed comes back holding nothing.)
            var existing = parent.GetComponentInChildren<SaintHalo>(true);
            if (existing != null)
            {
                // Position too, not just size: a halo made before the blade changed kept hanging
                // where the old one's middle was.
                existing._weapon = weapon;
                existing._size = size;
                existing.transform.localPosition = new Vector3(0f, along, 0f);
                return existing;
            }

            var go = new GameObject("saint.halo");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, along, 0f);

            var h = go.AddComponent<SaintHalo>();
            h._size = size;
            h._weapon = weapon;

            // Straddling the weapon's own order is what puts the blade INSIDE the ring - one
            // either side, so nothing can land between them. The two neighbours it ties with are
            // drawn elsewhere on the body (below the weapon is the head armour, above it the front
            // arm and glove, which `TwoHandedOrder` promotes so the fists show over the grip), and
            // the ring only ever occupies MID-BLADE - its lowest point sits above the crossguard.
            // So the ties are in the sorting table rather than on screen.
            h._far = Ring(go.transform, "far", Spr.HaloRing, 0);
            h._near = Ring(go.transform, "near", Spr.HalfRing, 0);
            return h;
        }

        public void SetShown(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        void Update()
        {
            if (_far == null || _near == null) return;
            _t += Time.deltaTime;

            // THE FLOOR IS HIGH, and it is measured rather than chosen. Scaling the sprite
            // non-uniformly thins the BAND along with the ring, so the top and bottom arcs - the
            // parts running nearest to horizontal - lose their weight fastest and break into
            // dashes long before the ring itself is edge-on. At a 0.18 floor the hoop spent most
            // of its cycle as a few gold flecks beside the blade, which is worse than not turning
            // at all. Captured at 0.18 and again at 0.40; 0.40 still reads as a full turn because
            // the change from there to 1.0 is what the eye is reading, not the extremes.
            //
            // (A real tilted ring keeps its band thickness, so this is a cheat with a known edge.
            // Buying the last of the turn back would mean drawing the ellipse rather than scaling
            // a circle, which is a lot of arithmetic for the end of a motion nobody watches.)
            float squash = Mathf.Lerp(0.40f, 1f, Mathf.Abs(Mathf.Sin(_t * 0.55f)));
            var scale = new Vector3(_size, _size * squash, 1f);
            _far.transform.localScale = scale;
            _near.transform.localScale = scale;

            if (_weapon != null)
            {
                _far.sortingOrder = _weapon.sortingOrder - 1;
                _near.sortingOrder = _weapon.sortingOrder + 1;
                _far.sortingLayerID = _near.sortingLayerID = _weapon.sortingLayerID;
            }

            // A slow breath on the brightness, so it reads as lit rather than printed.
            float lit = 0.80f + 0.20f * Mathf.Sin(_t * 2.1f);
            _far.color = new Color(Gold.r, Gold.g, Gold.b, lit * 0.85f);   // the far arc is dimmer:
            _near.color = new Color(Gold.r, Gold.g, Gold.b, lit);          //   it is behind the blade
        }

        static SpriteRenderer Ring(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Gold;
            sr.sortingOrder = order;   // replaced from the weapon's own on the first Update
            return sr;
        }
    }
}
