using System;
using System.Collections.Generic;
using UnityEngine;
using Convergence.Art;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The armoury: a long hall off the hub with every weapon design hung on one wall - the ones
    /// the player holds drawn in full, the rest as dark silhouettes in their empty bays.
    ///
    /// A ROOM OF ITS OWN BECAUSE OF THE ZOOM. Weapons have menu art at 300 texels per unit, and
    /// the hub camera gives a world unit about 150 screen pixels at 1080p, so hung in the hub half
    /// of every texel would have nowhere to land. This room is built far along X from the hub and
    /// the camera zooms in while the player is inside it (Tuning.Armoury.ViewHalfHeight), so the
    /// art is drawn at a WHOLE number of screen pixels per texel - which only works because every
    /// piece is drawn at its true size. Nothing on this wall may be fit-scaled.
    ///
    /// Strict top-down, not tilted like the hub: the tilt squashes every sprite's height by
    /// cos(32 deg), and no pixel ratio survives that.
    ///
    /// "Owned" is exactly what the gear picker offers (<see cref="GearOwnership"/>), so the wall
    /// and the picker cannot disagree. Every design is a bay whether it is owned or not - an empty
    /// space is the point. Minted instances have no art of their own yet and no design to hang
    /// in, so only authored pieces get a bay.
    ///
    /// Owns no profile state: bays keep item ids, re-resolved on every <see cref="Refresh"/>,
    /// which HubRoom calls each time the player walks in.
    /// </summary>
    public class ArmouryRoom : MonoBehaviour
    {
        /// <summary>One space on the wall. Not a MonoBehaviour: plain data the room rebuilds.</summary>
        class Bay
        {
            public string ItemId;
            public float X;
            public SpriteRenderer Main, Off, Glow;
            public HubInteractable Point;
        }

        // Not readonly - see the domain reload rule on collections in CLAUDE.md.
        List<Bay> _bays = new();
        List<HubInteractable> _points = new();

        Doorway _exit;
        Func<string> _rackShown;
        Action<string> _setRack;
        Func<CharacterProfile> _profile;
        Action _lookChanged;

        static readonly Color Floor = new(0.105f, 0.10f, 0.12f);
        static readonly Color WallFace = new(0.155f, 0.16f, 0.20f);
        static readonly Color WallStone = new(0.20f, 0.21f, 0.26f);
        static readonly Color Skirt = new(0.26f, 0.27f, 0.33f);
        static readonly Color Recess = new(0.115f, 0.115f, 0.145f);
        static readonly Color Shelf = new(0.24f, 0.25f, 0.30f);
        static readonly Color Unfound = new(0.035f, 0.035f, 0.05f, 0.92f);
        static readonly Color Gold = new(0.91f, 0.73f, 0.24f);

        /// <summary>A disc's off-hand twin - the rack's own offset, so a pair reads the same in
        /// both places.</summary>
        static readonly Vector2 OffHand = new(0.15f, -0.11f);

        /// <summary>Every interaction point in the room - HubRoom adds them to its own list, so
        /// the one prompt card and the one focus rule serve both rooms.</summary>
        public IReadOnlyList<HubInteractable> Points => _points;

        /// <summary>Where the player appears on walking in: in front of the doorway back.</summary>
        public Vector2 Entry => _exit != null ? _exit.Anchor : (Vector2)transform.position;

        /// <summary>World rect of everything drawn - what the camera clamps to.</summary>
        public Rect ViewBounds { get; private set; }

        public int Owned { get; private set; }
        public int Total => _bays.Count;

        public static ArmouryRoom Build(Transform parent, Action onExit,
                                        Func<string> rackShown, Action<string> setRack,
                                        Func<CharacterProfile> profile, Action lookChanged)
        {
            var go = new GameObject("Armoury");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(Tuning.Armoury.OriginX, 0f, 0f);

            var r = go.AddComponent<ArmouryRoom>();
            r._rackShown = rackShown;
            r._setRack = setRack;
            r._profile = profile;
            r._lookChanged = lookChanged;
            r.BuildRoom(onExit);
            r.Refresh();
            return r;
        }

        /// <summary>
        /// Every authored weapon design, grouped by class and then by tier - the wall's fixed
        /// order. Fixed, so a bay is always the same design's space whether or not it is filled.
        /// </summary>
        static List<List<GearItem>> Designs()
        {
            var byClass = new SortedDictionary<WeaponClass, List<GearItem>>();
            foreach (var item in GearCatalog.ForSlot(GearSlot.Weapon))
            {
                if (!string.IsNullOrEmpty(item.StackKey)) continue;   // a minted instance, not a design
                if (!byClass.TryGetValue(item.Class, out var list)) byClass[item.Class] = list = new();
                list.Add(item);
            }

            var groups = new List<List<GearItem>>();
            foreach (var list in byClass.Values)
            {
                list.Sort((a, b) => a.Tier != b.Tier
                    ? a.Tier.CompareTo(b.Tier)
                    : string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));
                groups.Add(list);
            }
            return groups;
        }

        void BuildRoom(Action onExit)
        {
            float floorH = Tuning.Armoury.FloorHeight;
            float wallH = Tuning.Armoury.WallHeight;
            float t = Tuning.Armoury.WallThickness;
            float pitch = Tuning.Armoury.BayPitch;

            // ---- lay the bays out first: the room is as long as its wall needs to be ----
            float cursor = Tuning.Armoury.EntranceWidth + 0.3f;
            var placed = new List<(GearItem Item, float X)>();
            foreach (var group in Designs())
            {
                foreach (var item in group)
                {
                    placed.Add((item, cursor + pitch * 0.5f));
                    cursor += pitch;
                }
                cursor += Tuning.Armoury.ClassGap;
            }
            float width = Mathf.Max(cursor - Tuning.Armoury.ClassGap + 0.4f, 6f);

            // ---- floor, wall face, walls ----
            Quad(transform, "floor", width, floorH, Floor, SortingOrders.FloorBack,
                 new Vector2(width * 0.5f, floorH * 0.5f));
            Quad(transform, "wall-face", width, wallH, WallFace, SortingOrders.FloorDetail,
                 new Vector2(width * 0.5f, floorH + wallH * 0.5f));
            Quad(transform, "skirt", width, 0.08f, Skirt, SortingOrders.FloorDetail + 1,
                 new Vector2(width * 0.5f, floorH));
            Quad(transform, "wall-top", width + t * 2f, t * 0.6f, WallStone, SortingOrders.FloorDetail + 1,
                 new Vector2(width * 0.5f, floorH + wallH + t * 0.3f));
            Quad(transform, "wall-south", width + t * 2f, t, WallStone, SortingOrders.FloorDetail,
                 new Vector2(width * 0.5f, -t * 0.5f));
            float sideH = floorH + wallH + t * 1.6f;
            Quad(transform, "wall-west", t, sideH, WallStone, SortingOrders.FloorDetail,
                 new Vector2(-t * 0.5f, sideH * 0.5f - t));
            Quad(transform, "wall-east", t, sideH, WallStone, SortingOrders.FloorDetail,
                 new Vector2(width + t * 0.5f, sideH * 0.5f - t));

            Blocker(new Vector2(width * 0.5f, -t * 0.5f), new Vector2(width + t * 2f, t));
            Blocker(new Vector2(width * 0.5f, floorH + 0.25f), new Vector2(width + t * 2f, 0.5f));
            Blocker(new Vector2(-t * 0.5f, floorH * 0.5f), new Vector2(t, floorH + t * 2f));
            Blocker(new Vector2(width + t * 0.5f, floorH * 0.5f), new Vector2(t, floorH + t * 2f));

            var p0 = (Vector2)transform.position;
            ViewBounds = new Rect(p0.x - t, p0.y - t, width + t * 2f, floorH + wallH + t * 1.6f);

            // ---- the way back ----
            _exit = Doorway.Build(transform, "armoury-exit", Tuning.Armoury.EntranceWidth * 0.5f, floorH,
                                  Tuning.Armoury.DoorHalfWidth, Tuning.Armoury.DoorHeight);
            var exitPoint = HubInteractable.Attach(_exit.gameObject, _exit.Anchor, 1.0f,
                () => new Prompt
                {
                    Title = "THE ARMOURY",
                    Sub = $"{Owned} of {Total} held",
                    Body = "Back through to the hall.",
                    Key = "[ E ]  leave",
                    Accent = Gold,
                },
                onExit, on => _exit.SetFocus(on), priority: 10);
            _points.Add(exitPoint);

            // ---- the bays ----
            float bayY = floorH + 0.14f + Tuning.Armoury.BayHeight * 0.5f;
            foreach (var (item, x) in placed)
            {
                var bayGo = new GameObject("bay-" + item.ItemId);
                bayGo.transform.SetParent(transform, false);
                bayGo.transform.localPosition = new Vector3(x, bayY, 0f);

                Quad(bayGo.transform, "recess", Tuning.Armoury.BayWidth, Tuning.Armoury.BayHeight,
                     Recess, SortingOrders.GroundDecal + 1, Vector2.zero);
                Quad(bayGo.transform, "shelf", Tuning.Armoury.BayWidth + 0.06f, 0.05f,
                     Shelf, SortingOrders.GroundDecal + 2, new Vector2(0f, -Tuning.Armoury.BayHeight * 0.5f));

                var bay = new Bay
                {
                    ItemId = item.ItemId,
                    X = x,
                    Glow = Quad(bayGo.transform, "glow", Tuning.Armoury.BayWidth, Tuning.Armoury.BayHeight,
                                new Color(1f, 0.92f, 0.72f, 0f), SortingOrders.GroundDecal + 2,
                                Vector2.zero, Spr.Glow),
                    Off = Piece(bayGo.transform, "off", SortingOrders.GroundDecal + 3),
                    Main = Piece(bayGo.transform, "main", SortingOrders.GroundDecal + 4),
                };
                _bays.Add(bay);

                var b = bay;
                var anchor = (Vector2)transform.TransformPoint(new Vector3(x, floorH - 0.4f, 0f));
                var point = HubInteractable.Attach(bayGo, anchor, pitch * 0.55f,
                    () => Describe(b), () => Take(b), on => Focus(b, on));
                point.BlocksPlacement = false;
                bay.Point = point;
                _points.Add(point);
            }
        }

        /// <summary>Re-read what is held and repaint every bay. Explicit, never subscribed - the
        /// same discipline as HubRoom.RefreshArmoury.</summary>
        public void Refresh()
        {
            if (_bays == null) return;
            Owned = 0;
            foreach (var bay in _bays)
            {
                if (bay?.Main == null) continue;
                var item = GearCatalog.Get(bay.ItemId);
                bool owned = GearOwnership.Owns(item);
                if (owned) Owned++;
                Paint(bay, item, owned);
            }
        }

        void Paint(Bay bay, GearItem item, bool owned)
        {
            var layer = GearDisplay.Represent(item, menu: true);
            bool pair = item != null && item.Class == WeaponClass.Disc;

            // fit 1, ALWAYS - a scaled piece is off the texel grid the whole room is zoomed for.
            GearDisplay.Draw(bay.Main, layer);
            var offLayer = pair ? GearDisplay.RepresentOffhand(item, menu: true) : null;
            GearDisplay.Draw(bay.Off, offLayer);
            bay.Off.enabled = pair;

            if (layer == null) return;

            if (owned)
            {
                if (pair)
                    bay.Off.color = new Color(bay.Off.color.r * 0.72f, bay.Off.color.g * 0.72f,
                                              bay.Off.color.b * 0.72f, bay.Off.color.a);
                GearDisplay.ApplyEffects(bay.Main, item);
            }
            else
            {
                // The SHAPE is shown and nothing else - a silhouette says what the space is for
                // without handing over the look. PixelSprite.Silhouette is white, so the renderer's
                // colour (a multiply) is what makes it dark.
                bay.Main.sprite = PixelSprite.Silhouette(layer.Sprite);
                bay.Main.color = Unfound;
                if (pair)
                {
                    bay.Off.sprite = PixelSprite.Silhouette(offLayer.Sprite);
                    bay.Off.color = Unfound;
                }
                GearDisplay.ApplyEffects(bay.Main, null);
            }
            bay.Glow.color = new Color(1f, 0.92f, 0.72f, owned ? 0.10f : 0f);

            // Upright and centred by GEOMETRY, not pivot - a weapon's pivot is its grip. A pair is
            // centred as a pair. See WeaponRack.Show for the same two rules measured on the rack.
            var c = (Vector2)layer.Sprite.bounds.center;
            var s = bay.Main.transform.localScale;
            var offset = new Vector2(c.x * s.x, c.y * s.y);
            var lean = pair ? OffHand * 0.5f : Vector2.zero;
            bay.Main.transform.localPosition = -offset - lean;
            bay.Off.transform.localPosition = -offset - lean + OffHand;
        }

        Prompt Describe(Bay bay)
        {
            var item = GearCatalog.Get(bay.ItemId);
            if (item == null) return new Prompt { Title = "AN EMPTY BAY", Accent = Gold };

            string kind = item.Class.ToString().ToLowerInvariant();
            string tier = item.Tier.ToString().ToLowerInvariant();
            if (!GearOwnership.Owns(item))
                return new Prompt
                {
                    Title = "NOT YET FOUND",
                    Sub = $"{tier} {kind}",
                    Body = "Its space is kept on the wall.",
                    Key = "",
                    Accent = new Color(0.45f, 0.47f, 0.54f),
                };

            bool onRack = _rackShown?.Invoke() == item.ItemId;
            var look = LookFor(item, out string lookNote);
            string body = onRack ? "Also on the rack in the hall." : "Yours.";
            if (!string.IsNullOrEmpty(lookNote)) body += "\n" + lookNote;

            string key = onRack ? "" : "[ E ]  hang it on the rack in the hall";
            string lookKey = look switch
            {
                LookAction.Use => "[ Q ]  use its appearance",
                LookAction.Stop => "[ Q ]  stop using its appearance",
                _ => "",
            };
            if (lookKey.Length > 0) key = key.Length > 0 ? key + "     " + lookKey : lookKey;

            return new Prompt
            {
                Title = item.DisplayName.ToUpperInvariant(),
                Sub = $"{tier} {kind}",
                Body = body,
                Key = key,
                Accent = Gold,
            };
        }

        enum LookAction { None, Use, Stop }

        /// <summary>
        /// What [ Q ] would do to the character's weapon appearance at this bay, and a line saying
        /// why when it can do nothing. Asks the same questions <see cref="Appearance.Resolve"/>
        /// does - same class and handedness as the weapon HELD - so the wall never offers a look
        /// the resolver would quietly refuse.
        /// </summary>
        LookAction LookFor(GearItem item, out string note)
        {
            note = null;
            var p = _profile?.Invoke();
            if (p == null || item == null) return LookAction.None;

            var held = GearCatalog.Get(p.Gear.Get(GearSlot.Weapon));
            if (held == null) { note = "Nothing in hand to wear its look."; return LookAction.None; }
            if (item.Class != held.Class || item.TwoHanded != held.TwoHanded)
            {
                note = $"Held differently from your {held.Class.ToString().ToLowerInvariant()} - its look can't be worn.";
                return LookAction.None;
            }

            bool disguised = p.Look.IsTransmogged(GearSlot.Weapon, p.Gear);
            string shown = p.Look.Resolve(p.Gear).Get(GearSlot.Weapon);
            if (shown != item.ItemId) return LookAction.Use;
            if (!disguised) { note = "In your hand."; return LookAction.None; }
            note = $"Your {held.DisplayName} wears its look.";
            return LookAction.Stop;
        }

        /// <summary>
        /// [ Q ] at a bay: wear that design's look on the weapon in hand, or take it off again.
        /// HubRoom asks every focused point; false means the point isn't a bay here, or there is
        /// nothing to do at it. Picking the held weapon's OWN design clears the disguise rather
        /// than storing a transmog that names what is already equipped.
        /// </summary>
        public bool TryUseLook(HubInteractable point)
        {
            if (point == null || _bays == null) return false;
            var bay = _bays.Find(b => b != null && b.Point == point);
            if (bay == null) return false;

            var item = GearCatalog.Get(bay.ItemId);
            if (!GearOwnership.Owns(item)) return false;
            var action = LookFor(item, out _);
            if (action == LookAction.None) return false;

            var p = _profile();
            if (action == LookAction.Stop || p.Gear.Get(GearSlot.Weapon) == item.ItemId)
                p.Look.Transmog.Clear(GearSlot.Weapon);
            else
                p.Look.Transmog.Set(GearSlot.Weapon, item.ItemId);
            _lookChanged?.Invoke();
            return true;
        }

        void Take(Bay bay)
        {
            var item = GearCatalog.Get(bay.ItemId);
            if (!GearOwnership.Owns(item) || _rackShown?.Invoke() == item.ItemId) return;
            _setRack?.Invoke(item.ItemId);
        }

        void Focus(Bay bay, bool on)
        {
            if (bay?.Glow == null) return;
            bool owned = GearOwnership.Owns(GearCatalog.Get(bay.ItemId));
            float a = owned ? (on ? 0.28f : 0.10f) : (on ? 0.05f : 0f);
            bay.Glow.color = new Color(1f, 0.92f, 0.72f, a);
        }

        void Blocker(Vector2 centre, Vector2 size)
        {
            var go = new GameObject("blocker");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = centre;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        static SpriteRenderer Piece(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
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
