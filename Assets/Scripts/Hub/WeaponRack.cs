using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// Half the armoury: a case holding ONE weapon the player has chosen to put on show.
    ///
    /// PURELY COSMETIC, and that is the whole design rather than a caveat. It is not a readout of
    /// what is equipped - an earlier version was exactly that, and it made the fixture a status
    /// bar: it could only ever say something the character standing next to it already said, and
    /// it changed on its own whenever the player changed weapon. A display case is for the pieces
    /// NOT on your person. Nothing here reads the loadout, grants anything, or wears down.
    ///
    /// It follows that the rack has no opinion about what belongs on it. Any weapon, including
    /// the one the player happens to be holding - forbidding that would mean the case quietly
    /// emptying itself the moment somebody equipped what was on it.
    ///
    /// HOLDS NO PROFILE STATE - three item ids and their sprites, repointed by an explicit
    /// <c>HubRoom.RefreshArmoury()</c>. The documented reason: a CharacterProfile reference or a
    /// Func on a MonoBehaviour is gone after a script edit during play while the object carries on
    /// rendering, so the rack would silently freeze showing whatever was on it when the edit landed.
    /// </summary>
    public class WeaponRack : MonoBehaviour
    {
        /// <summary>
        /// ONE weapon, and the case is built around that.
        ///
        /// It held three at first. A rack of three is a shop display - the eye has nowhere to land
        /// and each piece is a third of the space it deserves, which for the object a player is
        /// proudest of is exactly backwards. One weapon in a case its own size is a statement;
        /// three in a row is an inventory.
        /// </summary>
        public const string KeyPrefix = "weaponrack:";
        public const string Key = KeyPrefix + "0";

        public Vector2 Anchor { get; private set; }

        /// <summary>What is on the rack, for the prompt. A plain string, for the reason above.</summary>
        public string Name { get; private set; }
        public int Shown => string.IsNullOrEmpty(Name) ? 0 : 1;

        SpriteRenderer _shadow;
        [SerializeField] HubFixture _frame;

        /// <summary>The piece, and its off-hand twin. A DISC IS A PAIR - the character carries
        /// both anywhere the weapon is merely being looked at (see CharacterRigFactory.Paint's
        /// SetWeaponSplit), so a case showing one of a pair would be showing half a weapon.</summary>
        SpriteRenderer _main, _off;
        Vector2 _restAt;
        bool _focused;


        /// <summary>Footprint for placement legality and for the walk-into collider.</summary>
        public const float Radius = 0.85f;

        /// <summary>Tallest a piece may hang. A greatsword is 1.04 world units, so this is very
        /// slightly larger than life - which is what a display stand is for.</summary>
        const float PieceHeight = 1.1f;

        /// <summary>Sized to one weapon: a greatsword is 0.43 across and a pair of discs 0.60, so
        /// the opening clears the widest thing that can hang in it with a margin either side.</summary>
        const float Width = 1.06f;

        /// <summary>How far each of a pair of discs sits from the centre line, and how far the
        /// off-hand one drops. They OVERLAP rather than sitting side by side - two rings in a row
        /// read as a barbell, while a pair leaning together reads as one weapon that happens to
        /// come in twos, which is what a disc is.</summary>
        static readonly Vector2 OffHand = new(0.15f, -0.11f);

        public static WeaponRack Build(Transform parent, Vector2 centre)
        {
            var go = new GameObject("weapon-rack");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;

            var r = go.AddComponent<WeaponRack>();
            r.Anchor = centre;

            r._shadow = Quad(go.transform, "shadow", Width * 1.15f, Radius * 0.85f,
                             new Color(0f, 0f, 0f, 0.30f), 0, new Vector2(0f, -0.02f), Spr.Circle);

            // The oak A-frame (the user's pick, group 6): two posts and crossbars, solid at 3/4, the
            // weapon held upright between them - the room shows through. No soft glow behind it.
            r._frame = HubFixture.Attach(go, HubArt.AFrame(Mathf.RoundToInt(Width * HubArt.Ppu), Mathf.RoundToInt(1.45f * HubArt.Ppu)),
                                         Vector2.zero, 2);

            r._restAt = new Vector2(0f, 0.74f);

            // The off-hand copy is drawn BELOW the main one, so a pair reads with depth rather
            // than as two flat rings - the same reasoning the rig uses putting the second disc at
            // LegBack's order, behind the body.
            r._off = Piece(go.transform, "off", 5, r._restAt + OffHand);
            r._main = Piece(go.transform, "main", 6, r._restAt);

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(Width, 0.34f);
            col.offset = new Vector2(0f, 0.10f);

            // Sorted from the FOOT of the posts, which is where the rack touches the floor.
            DepthSorted.Attach(go, 0f, isFixed: false,
                               r._shadow, r._frame.Art, r._frame.RimRenderer, r._off, r._main);
            return r;
        }

        public void SetFocus(bool on) => _focused = on;

        /// <summary>Hang the weapon. A null or unknown id empties the case.</summary>
        public void Show(string itemId)
        {
            if (_main == null || _off == null) return;   // half-built after a reload

            var item = string.IsNullOrEmpty(itemId) ? null : GearCatalog.Get(itemId);
            var layer = GearDisplay.Represent(item);

            GearDisplay.Draw(_main, layer, GearDisplay.FitScale(layer, PieceHeight));
            Name = item != null ? item.DisplayName : null;

            // The rack is where a weapon is LOOKED at, so anything carrying its own effect carries
            // it here too - see GearDisplay.ApplyEffects.
            GearDisplay.ApplyEffects(_main, item);

            // A DISC IS A PAIR. Two are drawn for it and one for everything else - the same
            // question CharacterRigFactory.Paint answers with SetWeaponSplit, and the same answer,
            // because a case is one of the places a weapon is being looked at rather than swung.
            bool pair = item != null && item.Class == WeaponClass.Disc;
            _off.enabled = pair;
            if (pair)
            {
                GearDisplay.Draw(_off, GearDisplay.RepresentOffhand(item, menu: true) ?? layer,
                                 GearDisplay.FitScale(layer, PieceHeight));
                _off.color = new Color(_off.color.r * 0.72f, _off.color.g * 0.72f,
                                       _off.color.b * 0.72f, _off.color.a);
                // A kindled pair burns in BOTH halves (the Aether Dual Discs' lioness).
                if (item.Kindled) KindledMarks.On(_off);
            }
            else _off.sprite = null;

            if (layer == null)
            {
                _main.transform.localPosition = _restAt;
                return;
            }

            // A BLADE hangs point down, and only a blade. Weapon sprites are authored blade-up
            // in the hand, so a sword left alone reads as balanced on its tip - but the flip is
            // a fact about swords, not about the Weapon slot. Applied slot-wide it hung the bow
            // upside down: invisible on discs, since a ring is its own mirror, and nearly
            // invisible on this project's bow only because that art happens to be close to
            // symmetric, which is exactly the accident that stops being true on the next weapon.
            var spin = item != null && item.Class == WeaponClass.Greatsword
                ? Quaternion.Euler(0f, 0f, 180f)
                : Quaternion.identity;
            _main.transform.localRotation = spin;
            _off.transform.localRotation = spin;

            // A WEAPON'S PIVOT IS ITS GRIP, NOT ITS MIDDLE, so a rotation swings the blade about
            // the hand rather than about the sprite. Turned point-down on the rest position alone,
            // a greatsword's whole length moved to the far side of the grip and the blade hung a
            // foot below the rack - measured on screen, not guessed. Placing the piece by its
            // GEOMETRIC centre instead makes the case mean the same thing for every weapon,
            // whatever its pivot and whether or not it was flipped.
            var offset = Offset(_main, layer);

            // A pair is centred as a PAIR, not as two pieces each centred on the same point -
            // otherwise the twin hangs off to one side and the weapon looks badly hung rather
            // than doubled.
            var lean = pair ? (Vector2)OffHand * 0.5f : Vector2.zero;
            _main.transform.localPosition = _restAt - offset - lean;
            _off.transform.localPosition = _restAt - offset - lean + OffHand;
        }

        void Update()
        {
            if (_frame != null) _frame.SetFocus(_focused);
        }

        /// <summary>Where the sprite's geometric centre sits relative to its pivot, taken through
        /// the same scale and rotation the piece is actually drawn with.</summary>
        static Vector2 Offset(SpriteRenderer sr, LayerSprite layer)
        {
            var c = (Vector2)layer.Sprite.bounds.center;
            var s = sr.transform.localScale;
            return sr.transform.localRotation * new Vector2(c.x * s.x, c.y * s.y);
        }

        static SpriteRenderer Piece(Transform parent, string name, int order, Vector2 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        static SpriteRenderer Quad(Transform parent, string name, float w, float h,
                                   Color c, int order, Vector2 at, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : Spr.Square;
            sr.color = c;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
