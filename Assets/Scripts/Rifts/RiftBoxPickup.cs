using UnityEngine;
using Convergence.Core;

namespace Convergence.Rifts
{
    /// <summary>
    /// A Rift Box lying on the floor where something died, waiting to be picked up.
    ///
    /// IT HAS TO BE TAKEN, NOT WALKED OVER. Auto-pickup would make a box something that happens to
    /// the player; pressing for it makes it something they chose, and in the middle of a fight that
    /// choice costs a beat - you have to stop, stand on it and commit a keypress while whatever is
    /// left of the wave is still moving. That is the right price for the thing that decides how
    /// much you get to keep, and it is why the drop is worth being physical at all rather than a
    /// number appearing in the corner.
    ///
    /// It draws a small tear on its face, off <see cref="Spr.Tear"/> - the same shape the Rift
    /// itself is cut from - so the connection between the consumable and the fixture it is spent
    /// at is made visually rather than only in a tooltip.
    ///
    /// IT COLLAPSES IF LEFT - see Tuning.Boss.RiftBoxLifeSeconds for the reasoning and the
    /// arithmetic. The timer is what makes the press a decision rather than friction, and it is
    /// also what the object IS: a fragment of a tear, and tears close. One that sat inert on the
    /// floor forever would be a crate.
    ///
    /// THE WIND-DOWN IS THE WHOLE CONDITION ON HAVING A TIMER AT ALL. Losing a one-in-five-hundred
    /// drop is only acceptable if the player watched it going and chose to keep swinging, so the
    /// box shrinks, the tear on its face closes, and the rim runs cyan -> violet -> hot as the
    /// clock runs out. A box that vanished without warning would be the reflex test this was
    /// nearly rejected for being.
    /// </summary>
    public class RiftBoxPickup : MonoBehaviour
    {
        public const float Reach = 1.1f;

        Transform _player;
        System.Action _onTaken;
        SpriteRenderer _glow, _box, _face, _rim;
        float _t;
        float _life = Tuning.Boss.RiftBoxLifeSeconds;
        bool _taken;

        /// <summary>1 when freshly dropped, 0 as it closes.</summary>
        public float Life01 => Mathf.Clamp01(_life / Mathf.Max(0.0001f, Tuning.Boss.RiftBoxLifeSeconds));

        static readonly Color Violet = new(0.62f, 0.42f, 0.95f);
        static readonly Color Cyan = new(0.45f, 0.88f, 1f);

        public bool InReach => _player != null && !_taken
            && Vector2.Distance(_player.position, transform.position) <= Reach;

        public static RiftBoxPickup Drop(Vector2 at, Transform parent, Transform player,
                                         System.Action onTaken)
        {
            var go = new GameObject("rift-box");
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = Arena.NearestFloor(at, 0.6f);

            var b = go.AddComponent<RiftBoxPickup>();
            b._player = player;
            b._onTaken = onTaken;

            b._glow = Quad(go.transform, "glow", 1.5f, 1.5f, Spr.Glow,
                           new Color(Violet.r, Violet.g, Violet.b, 0.35f), SortingOrders.Fx - 42);
            b._box = Quad(go.transform, "box", 0.42f, 0.42f, Spr.Square,
                          new Color(0.10f, 0.09f, 0.16f, 1f), SortingOrders.Fx - 41);
            b._rim = Quad(go.transform, "rim", 0.50f, 0.50f, Spr.Square,
                          new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f), SortingOrders.Fx - 43);
            b._face = Quad(go.transform, "face", 0.16f, 0.34f, Spr.Tear,
                           new Color(1f, 1f, 1f, 0.95f), SortingOrders.Fx - 40);

            // Turned 45 degrees so it reads as an object standing on a corner rather than a UI
            // panel lying on the ground - nothing else in the arena is an axis-aligned square.
            b._box.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            b._rim.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            DepthSorted.Attach(go, 0f, isFixed: false, b._glow, b._rim, b._box, b._face);
            return b;
        }

        void Update()
        {
            if (_taken) return;

            // Scaled time on both, so a screen opening mid-fight neither burns the clock nor lets
            // the box hang there animating behind a paused game.
            _t += Time.deltaTime;
            _life -= Time.deltaTime;
            if (_life <= 0f) { Implode(); return; }

            float k = Life01;

            // Bobs and turns, and BOTH SLOW as it closes - a thing running out of time should not
            // be as lively at one second as at ten. The wind-down is the tell, so it is carried by
            // everything the object does rather than by one flashing part.
            float bob = Mathf.Sin(_t * 2.2f) * 0.07f * k;
            float spin = 45f + Mathf.Sin(_t * 0.9f) * 8f * k;
            foreach (var sr in new[] { _box, _rim, _face })
                sr.transform.localPosition = new Vector3(0f, bob, 0f);
            _box.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
            _rim.transform.localRotation = _box.transform.localRotation;

            // It SHRINKS as it goes, and the tear on its face closes with it - the fragment is
            // being pulled back into itself, which is what an imploding piece of a rift should do.
            float shrink = Mathf.Lerp(0.55f, 1f, k);
            _box.transform.localScale = new Vector3(0.42f, 0.42f, 1f) * shrink;
            _rim.transform.localScale = new Vector3(0.50f, 0.50f, 1f) * shrink;
            _face.transform.localScale = new Vector3(0.16f * k, 0.34f * shrink, 1f);

            bool near = InReach;

            // Cyan while there is time, running to hot as it closes. The last two seconds also
            // pulse faster, so "about to go" reads at a glance from across the arena rather than
            // needing the player to be looking straight at it.
            float urgency = 1f - k;
            var rim = Color.Lerp(Cyan, new Color(1f, 0.45f, 0.35f), urgency * urgency);
            float beat = 3f + urgency * urgency * 14f;

            _glow.color = new Color(Mathf.Lerp(Violet.r, 1f, urgency * urgency),
                                    Violet.g * (1f - urgency * 0.4f),
                                    Violet.b * (1f - urgency * 0.5f),
                                    (near ? 0.55f : 0.30f) + 0.10f * Mathf.Sin(_t * beat));
            _rim.color = near ? Color.Lerp(Color.white, rim, 0.25f)
                              : new Color(rim.r, rim.g, rim.b, 0.75f + 0.2f * Mathf.Sin(_t * beat));

            if (!near || !Controls.InteractTapped) return;

            _taken = true;
            Spr.Flash(transform.position, 1.4f, Cyan, 0.35f);
            _onTaken?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>
        /// Time ran out. Collapses inward rather than fading: a fade reads as the object having
        /// been removed, and this one is closing on itself.
        /// </summary>
        void Implode()
        {
            _taken = true;
            // The shared effect, not a flash - see RiftImplosion. It is the same picture anything
            // the Rift Blade kills makes, which is the point: a player who has watched a box run out
            // already knows what that death means the first time they cause one.
            RiftImplosion.At(transform.position, 0.55f);
            Destroy(gameObject);
        }

        static SpriteRenderer Quad(Transform parent, string name, float w, float h, Sprite sprite,
                                   Color c, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
