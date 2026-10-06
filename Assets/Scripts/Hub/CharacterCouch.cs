using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The couch: where the characters you are NOT playing are sitting.
    ///
    /// Switching character is a menu operation in most games, and it reads as one - a dropdown
    /// full of save slots. Here the other characters are just in the room, wearing their own gear
    /// with their own faces, and you swap by walking over and talking to one. That also makes the
    /// roster honest: a slot list can show six names, but six people on a couch is a number you
    /// feel.
    ///
    /// Seats are built from the profile store's roster, so this is a view of what is really
    /// saved. The character you are currently playing is not seated - they are the one standing.
    /// </summary>
    public class CharacterCouch : MonoBehaviour
    {
        /// <summary>
        /// Seats on the couch, which caps the roster at this many characters plus the one being
        /// played. Bound by the ROOM, not by the store: a wider couch runs into the upper-left
        /// door, and doors must stay reachable. Raising it means moving the furniture.
        /// </summary>
        public const int MaxSeats = 3;

        /// <summary>Width for a given number of seats, so a one-character couch is not a bench.</summary>
        public static float WidthFor(int seats) => 1.05f * Mathf.Clamp(seats, 1, MaxSeats) + 0.5f;

        /// <summary>
        /// The seats and who is in them.
        ///
        /// [NonSerialized] because Seat holds an ICharacterRig - an interface Unity cannot
        /// serialize - so it was warning that the field would be skipped. It genuinely does not
        /// need serializing: the couch is rebuilt with the room on every return to the hub.
        /// </summary>
        [System.NonSerialized] public List<Seat> Seats = new();

        public class Seat
        {
            public CharacterProfile Profile;   // null = the empty seat that makes a new character
            public Vector2 Anchor;
            public ICharacterRig Rig;
            public SpriteRenderer Cushion;
            public GameObject Root;
        }

        static readonly Color Frame = new(0.30f, 0.22f, 0.26f);
        static readonly Color Cushion = new(0.42f, 0.30f, 0.34f);

        /// <param name="roster">Every saved character. The active one is filtered out by the caller.</param>
        public static CharacterCouch Build(Transform parent, Vector2 centre, float width,
                                           IReadOnlyList<CharacterProfile> roster, bool allowNew)
        {
            var go = new GameObject("couch");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            var c = go.AddComponent<CharacterCouch>();

            int seats = Mathf.Min(MaxSeats, roster.Count + (allowNew ? 1 : 0));
            seats = Mathf.Max(seats, 1);

            // Backrest sits ABOVE the cushions in world y, so the seated figures draw in front of
            // it. In a top-down room that reading is the whole illusion of a couch.
            Quad(go.transform, "back", width, 0.46f, Frame,
                 SortingOrders.FloorDetail + 1, new Vector2(0f, 0.52f));
            Quad(go.transform, "seat", width, 0.86f, Cushion,
                 SortingOrders.FloorDetail + 2, new Vector2(0f, 0f));

            float pitch = width / seats;
            for (int i = 0; i < seats; i++)
            {
                float x = -width * 0.5f + pitch * (i + 0.5f);
                var profile = i < roster.Count ? roster[i] : null;

                var seat = new Seat
                {
                    Profile = profile,
                    // Stand in FRONT of the cushion, not on it - the prompt has to trigger where
                    // the player can actually walk.
                    Anchor = centre + new Vector2(x, -1.15f),
                };

                seat.Cushion = Quad(go.transform, $"cushion{i}", pitch - 0.12f, 0.72f,
                                    Cushion * 0.86f, SortingOrders.FloorDetail + 3,
                                    new Vector2(x, 0f));

                if (profile != null)
                {
                    // A real paper-doll, painted with that character's own look and gear. No
                    // Rigidbody, so the rig's walk cycle idles and it never turns - see
                    // PrimitiveCharacterRig.Body, which is null-tolerant for exactly this.
                    var figure = new GameObject($"seated-{profile.ProfileId}");
                    figure.transform.SetParent(go.transform, false);
                    figure.transform.localPosition = new Vector3(x, 0.18f, 0f);
                    figure.transform.localScale = Vector3.one * 0.86f;

                    seat.Rig = CharacterRigFactory.Build(figure, profile.LastElement,
                                                         SortingOrders.Character);
                    CharacterRigFactory.Paint(seat.Rig, profile);
                    seat.Rig.SetShowcasePose(true);
                    seat.Root = figure;

                    // Fixed: they never move, so this costs one call at build rather than a
                    // LateUpdate each, but they still have to sort against anything the player
                    // stands in front of them.
                    DepthSorted.Attach(figure, seat.Rig, isFixed: true);
                }

                c.Seats.Add(seat);
            }

            return c;
        }

        public void SetFocus(Seat seat, bool on)
        {
            if (seat?.Cushion == null) return;
            seat.Cushion.color = on ? Cushion * 1.5f : Cushion * 0.86f;
        }

        static SpriteRenderer Quad(Transform parent, string name, float w, float h, Color color,
                                   int order, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = offset;
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
