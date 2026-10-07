using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;
using Convergence.UI;

namespace Convergence.Hub
{
    /// <summary>
    /// The main menu, as a room.
    ///
    /// Replaces the four-card select screen. The reason is not decoration: the card screen taught
    /// the player nothing and asked nothing of them, and then dropped them into a twin-stick arena
    /// where movement is the whole game. Here the first thing they do is walk - with their own
    /// gear on, at the speed the character actually moves - and the choice of element is made by
    /// going somewhere rather than by clicking a rectangle.
    ///
    /// Everything is built at runtime like the rest of the project, so the scene stays free of
    /// prefab references and rebuildable from the CLI.
    ///
    /// LAYOUT (world units, fixed camera at the origin):
    ///
    ///        +--------------[ gallery wall: showcase frames ]--------------+
    ///        |                                                             |
    ///     [door]                                                       [door]
    ///        |                        (you)                                |
    ///     [door]                                                       [door]
    ///        |                                                             |
    ///        +-------------------------------------------------------------+
    /// </summary>
    public class HubRoom : MonoBehaviour
    {
        // ---- geometry, resolved at build time (HalfWidth adapts to the aspect ratio) ----
        float _halfWidth;
        float FloorTop => Tuning.Hub.FloorTop;
        float FloorBottom => Tuning.Hub.FloorBottom;

        Transform _root;
        PhotoBooth _booth;
        Forge _forge;
        ManualShrine _shrine;

        GameObject _avatarGo;
        HubAvatar _avatar;
        ICharacterRig _rig;

        /// <summary>
        /// Mirrors whatever <see cref="ICharacterRig.SetFacingAway"/> was last told for the
        /// avatar's own untargeted walking - see <see cref="UpdateAvatarFacingAway"/>. Tracked
        /// here rather than re-derived every frame so a call that would change nothing is skipped;
        /// <c>SetFacingAway</c> rebuilds every layer's sorting order.
        /// </summary>
        bool _avatarFacingAway;

        // Not readonly: a domain reload (editing a script while playing) cannot write back into
        // a readonly field, so these would return empty while the objects they track live on.
        // See the note in TransmutationCircle for what that failure actually looks like.
        SigilDoor _door;
        WallButton _button;
        ElementType[] _order;

        /// <summary>Every placed piece of showcase art, wall-mounted or standing as an easel -
        /// see Frame's own header for why the two are one list and one class now.</summary>
        List<Frame> _frames = new();
        List<HubInteractable> _points = new();
        TransmutationCircle _circle;
        GridTable _table;
        CharacterCouch _couch;
        IReadOnlyList<CharacterProfile> _roster;

        /// <summary>True while the couch in hand came straight out of the crate - cancelling
        /// that carry puts it back IN the crate, since it has no spot in the room to return to.</summary>
        bool _couchFresh;

        WeaponRack _rack;

        // ---- the armoury: a doorway on the north wall, and the room it leads to ----
        Doorway _armouryDoor;
        ArmouryRoom _armoury;
        bool _inArmoury;
        ArmourStand _stand;

        System.Action<ElementType> _onEnter;
        System.Action _onTransmute;
        System.Action _onMastery;
        System.Action<string> _onSwitchCharacter;
        System.Action _onNewCharacter;
        System.Action _onOpenCollection;
        System.Action<string> _onOpenCollectionForFrame;
        System.Action<bool> _onDressArmoury;
        System.Action _onOpenTerminal;
        System.Action _onTakePortrait;
        System.Action _onOpenForge;
        System.Action _onOpenManual;
        System.Action _onLayoutChanged;
        System.Action _onLookChanged;
        CharacterProfile _profile;

        /// <summary>
        /// The ACCOUNT's room, not this character's - see AccountProfile. Held as the layout
        /// itself rather than reached through a profile, so switching character cannot possibly
        /// swap the room out from under the trophies standing in it.
        /// </summary>
        RoomLayout _room;

        // ---- placement mode ----
        Frame _carrying;
        ShowcaseItem _carryItem;
        string _carryId;        // the frame's own identity - see RoomLayout.TrophyPlacement.Id
        Vector2? _carryOrigin;   // where it stood before being picked up; null if newly taken out
        bool _carryLegal;

        // ---- room editor: every fixture but the sigil door and its button ----
        //
        // A PARALLEL carry flow to the trophy one above rather than a shared/generalised one -
        // trophies are backed by a showcase key and looked up from a source; a fixture is a live
        // component already standing in the room with its own behaviour. Unifying them would mean
        // bending one of the two shapes to fit the other for no real gain. Not readonly - see the
        // rule on MonoBehaviour collections a domain reload cannot write back into.
        List<MovableFixture> _movables = new();
        bool _editMode;
        string _carryFixtureKey;
        // The carried fixture's solid colliders, switched off for the carry - it is parked on
        // the avatar's own position, so left on they shoved the avatar out of it every frame.
        // Serialized and non-readonly so a domain reload mid-carry doesn't strand them off.
        [SerializeField] List<Collider2D> _carryColliders = new();
        Vector2 _carryFixtureOrigin;
        bool _carryFixtureLegal;
        HubInteractable _cratePoint;   // so Update() can recognise "focused on the crate specifically"

        /// <summary>One movable thing: its own transform, and every HubInteractable that has to
        /// translate WITH it - the couch has one per seat, everything else has exactly one.</summary>
        class MovableFixture
        {
            public string Key;
            public Transform Root;
            public HubInteractable[] Points;
            public float Footprint;
        }

        // ---- prompt UI ----
        RectTransform _promptPanel, _inspectRoot, _card, _titleRow, _subRow;
        Text _promptTitle, _promptSub, _promptBody, _promptKey;
        Image _promptAccent;
        Transform _canvas;

        HubInteractable _focused;

        /// <summary>
        /// True on the frame a screen closed. Screen and room both run their own Update in an
        /// undefined order, so the [E] that closed the transmutation screen would otherwise be
        /// read again here on the same frame and reopen it instantly.
        /// </summary>
        bool _wasBlocked;

        /// <summary>True while the hub owns the keyboard - the caller must not also read it.</summary>
        // A fixture in hand counts too: without it Escape (meant to cancel the carry) also
        // opened Settings over the room, and the carry could never be put down.
        public bool Blocking => _inspectRoot != null || _carrying != null || _carryFixtureKey != null;

        /// <summary>True while a piece is being carried around looking for a spot.</summary>
        public bool IsPlacing => _carrying != null;

        public ICharacterRig AvatarRig => _rig;

        /// <summary>
        /// Where to point a camera to say "this is the hub" in one glance - the sigil door.
        ///
        /// THE ONE FIXTURE THAT CANNOT MOVE. Everything else in the room is in the editor's
        /// movables list and may be anywhere the player put it; the door and its button are
        /// structurally absent from that list, so there is no code path that could ever relocate
        /// them. That makes the door the only landmark a snapshot can rely on. Used by
        /// Rifts.HubGlimpse - see its header.
        ///
        /// Raised toward the door's own middle rather than its floor anchor, because the anchor is
        /// the spot a player STANDS ON to use it, which is in front of the door rather than on it.
        /// </summary>
        public Vector2 GateFocus =>
            _door != null
                ? (Vector2)_door.transform.position + new Vector2(0f, Tuning.Hub.DoorHeight * 0.35f)
                : new Vector2(Tuning.Hub.DoorX, FloorTop);

        // ------------------------------------------------------------------ build

        /// <param name="roster">Saved characters OTHER than the one being played. They sit on
        /// the couch; the active character is the one standing.</param>
        public void Build(Transform parent, Transform canvas, Camera cam, CharacterProfile profile,
                          ElementType[] order, IReadOnlyList<CharacterProfile> roster,
                          System.Action<ElementType> onEnter, System.Action onTransmute,
                          System.Action onMastery, System.Action<string> onSwitchCharacter,
                          System.Action onNewCharacter, System.Action onOpenCollection,
                          System.Action<string> onOpenCollectionForFrame,
                          System.Action onLayoutChanged, RoomLayout room,
                          System.Action onOpenTerminal, System.Action onTakePortrait,
                          System.Action onOpenForge, System.Action onOpenManual,
                          System.Action<bool> onDressArmoury, System.Action onLookChanged)
        {
            _canvas = canvas;
            _profile = profile;
            _room = room ?? new RoomLayout();
            // What a Prism shows lit in here: the element the selector starts on (see Attunement).
            if (order != null && order.Length > 0) Attunement.Set(order[0]);
            _onOpenCollection = onOpenCollection;
            _onOpenCollectionForFrame = onOpenCollectionForFrame;
            _onOpenTerminal = onOpenTerminal;
            _onTakePortrait = onTakePortrait;
            _onOpenForge = onOpenForge;
            _onOpenManual = onOpenManual;
            _onDressArmoury = onDressArmoury;
            _onLayoutChanged = onLayoutChanged;
            _onLookChanged = onLookChanged;
            _onEnter = onEnter;
            _onTransmute = onTransmute;
            _onMastery = onMastery;
            _onSwitchCharacter = onSwitchCharacter;
            _onNewCharacter = onNewCharacter;

            // A door the player cannot see is a door they cannot choose. On anything narrower than
            // 16:9 the room is pulled in until both side walls are on screen, rather than letting
            // the choice hang off the edge of a menu.
            float viewHalfWidth = cam != null ? cam.orthographicSize * cam.aspect : 8.53f;
            _halfWidth = Mathf.Min(Tuning.Hub.HalfWidth, viewHalfWidth - 0.55f);

            var go = new GameObject("Hub");
            go.transform.SetParent(parent, false);
            _root = go.transform;

            _order = order;
            BuildFloor();
            BuildWalls();
            BuildGallery();
            BuildSigilDoor(order);
            BuildLoft();
            BuildArmouryDoor();
            BuildFurniture(roster);
            BuildCrate();
            BuildPhotoBooth();
            BuildForge();
            BuildManualShrine();
            BuildArmoury();
            BuildArmouryRoom();
            BuildFrames();
            BuildAvatar(profile);
            BuildPrompt(profile);
        }

        void BuildFloor()
        {
            float h = FloorTop - FloorBottom;
            var floor = Quad(_root, "floor", _halfWidth * 2f, h,
                             new Color(0.115f, 0.108f, 0.13f), SortingOrders.FloorBack);
            floor.transform.localPosition = new Vector3(0f, (FloorTop + FloorBottom) * 0.5f, 0f);

            // The middle of the room is the transmutation circle. It doubles as the floor detail
            // the room needs anyway - an untextured floor gives the eye nothing to measure
            // movement against - but it is a real feature, not decoration.
            //
            // Movable in the room editor like everything else, which is a bigger claim than it
            // looks: the circle is the room's fixed centrepiece everywhere else in this project's
            // own documentation. Included anyway because the design it was built from said so
            // explicitly rather than sliding it in unannounced - see the room editor's own notes.
            var circleAt = DefaultOr("circle", new Vector2(0f, (FloorTop + FloorBottom) * 0.5f));
            _circle = TransmutationCircle.Build(_root, circleAt, Tuning.Hub.CircleRadius);
            var circlePoint = HubInteractable.Attach(_circle.gameObject, _circle.Anchor, _circle.Radius,
                () => _editMode
                    ? new Prompt
                    {
                        Title = "THE CIRCLE",
                        Sub = "editing",
                        Body = "Pick it up and stand it somewhere else.",
                        Key = "[ E ]  pick up",
                        Accent = new Color(0.86f, 0.72f, 0.38f),
                    }
                    : new Prompt
                    {
                        Title = "TRANSMUTATION CIRCLE",
                        Sub = "sulfur / mercury / salt",
                        Body = "Change what you fight with, what you are seen wearing, and the body " +
                               "underneath it.",
                        Key = "[ E ]  step in",
                        Accent = new Color(0.86f, 0.72f, 0.38f),
                    },
                () =>
                {
                    if (_editMode) BeginCarryFixture("circle");
                    else _onTransmute?.Invoke();
                },
                on => _circle.SetFocus(on));
            _points.Add(circlePoint);
            RegisterMovable("circle", _circle.transform, Tuning.Hub.CircleFootprint, circlePoint);
        }

        void BuildWalls()
        {
            float t = Tuning.Hub.WallThickness;
            float top = FloorTop + Tuning.Hub.GalleryHeight;
            float bottom = FloorBottom - t;
            var stone = new Color(0.20f, 0.21f, 0.26f);

            // Side walls, drawn whole - the door wall gets its dark opening painted over the top.
            for (int s = -1; s <= 1; s += 2)
            {
                var w = Quad(_root, "wall-side", t, top - bottom, stone, SortingOrders.FloorDetail);
                w.transform.localPosition = new Vector3(s * _halfWidth, (top + bottom) * 0.5f, 0f);
            }

            var south = Quad(_root, "wall-south", _halfWidth * 2f + t, t, stone, SortingOrders.FloorDetail);
            south.transform.localPosition = new Vector3(0f, FloorBottom - t * 0.5f, 0f);

            // Colliders. EVERY wall is solid now, the door's included: the sketch's doors stand
            // shut with light coming through the seam, so there is no opening to step into and
            // no recess to blocker off at the back. The door is used from the floor in front of
            // it, like the terminal or the crate - the wall behind it never opens.
            for (int s = -1; s <= 1; s += 2) Blocker(s * _halfWidth, bottom, top, t);

            BlockerBox(new Vector2(0f, FloorBottom - t * 0.5f), new Vector2(_halfWidth * 2f, t));
            BlockerBox(new Vector2(0f, FloorTop + 0.25f), new Vector2(_halfWidth * 2f, 0.5f));
        }

        void Blocker(float x, float lo, float hi, float thickness)
            => BlockerBox(new Vector2(x, (lo + hi) * 0.5f), new Vector2(thickness, hi - lo));

        void BlockerBox(Vector2 centre, Vector2 size)
        {
            var go = new GameObject("blocker");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = centre;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        /// <summary>
        /// The north wall's face, and the frames hanging on it.
        ///
        /// Seen from directly above a wall is a line, so this band is the standard top-down cheat:
        /// the far wall is drawn as if seen slightly from the front. Without it there is no
        /// surface in a top-down room to hang a picture on at all.
        /// </summary>
        void BuildGallery()
        {
            float gh = Tuning.Hub.GalleryHeight;
            float mid = FloorTop + gh * 0.5f;

            var face = Quad(_root, "gallery", _halfWidth * 2f, gh,
                            new Color(0.155f, 0.16f, 0.20f), SortingOrders.FloorDetail);
            face.transform.localPosition = new Vector3(0f, mid, 0f);

            // Skirting where the wall meets the floor - the one line that stops the face and the
            // floor reading as a single flat rectangle.
            var skirt = Quad(_root, "skirt", _halfWidth * 2f, 0.10f,
                             new Color(0.26f, 0.27f, 0.33f), SortingOrders.FloorDetail + 1);
            skirt.transform.localPosition = new Vector3(0f, FloorTop, 0f);
        }

        /// <summary>
        /// The one door, and the button beside it that chooses which element it opens onto.
        ///
        /// Priority 10 on BOTH, same reasoning HubDoor's four instances used to carry
        /// individually: a couch must never be able to out-rank the one fixture every run starts
        /// at. The button additionally outranks nothing it doesn't have to - it is far enough from
        /// any furniture anchor that the two were never going to compete.
        /// </summary>
        void BuildSigilDoor(ElementType[] order)
        {
            _door = SigilDoor.Build(_root, order, order[0], Tuning.Hub.DoorX, FloorTop);
            var doorPoint = HubInteractable.Attach(_door.gameObject, _door.Anchor, Tuning.Hub.DoorPromptRadius,
                () => new Prompt
                {
                    Title = "Enter the Gate",
                    Sub = ElementInfo.ResourceName(_door.Element),
                    Body = ElementInfo.Tagline(_door.Element),
                    Key = "[ E ]  enter",
                    Accent = ElementInfo.Tint(_door.Element),
                },
                () => _onEnter?.Invoke(_door.Element), on => _door.SetFocus(on), priority: 10);

            // The prompt radius (1.9) stays generous on purpose - see ClearanceRadius's own note
            // on why that number shouldn't also decide how much open floor the door reserves for
            // itself against furniture placement.
            doorPoint.ClearanceRadius = Tuning.Hub.DoorClearanceRadius;
            _points.Add(doorPoint);

            _button = WallButton.Build(_root, order, order[0], e => { _door.SetElement(e); Attune(e); },
                                       Tuning.Hub.ButtonX, FloorTop);
            _points.Add(HubInteractable.Attach(_button.gameObject, _button.Anchor, 1.1f,
                () => new Prompt
                {
                    Title = "SIGIL SELECTOR",
                    Sub = $"currently {_button.Element.ToString().ToLower()}",
                    Body = "Cycle which element the door opens onto.",
                    Key = "[ E ]  cycle",
                    Accent = new Color(0.91f, 0.73f, 0.24f),
                },
                () => _button.Cycle(), on => _button.SetFocus(on), priority: 10));
        }

        /// <summary>
        /// The two things in the room that are neither a door nor the circle: the mastery table
        /// and the couch the rest of your characters are sitting on.
        ///
        /// Both sit in the upper corners of the floor, clear of the door prompt radii and clear
        /// of the frame anchor under the gallery wall. Doors still outrank them on priority, so
        /// the exact placement is a comfort question rather than a correctness one.
        /// </summary>
        void BuildFurniture(IReadOnlyList<CharacterProfile> roster)
        {
            // ---- couch: IN THE CRATE unless the player has taken it out - see RoomLayout.CouchOut ----
            _roster = roster ?? System.Array.Empty<CharacterProfile>();
            if (_room.CouchOut) BuildCouch(DefaultOr("couch", new Vector2(-3.6f, 2.15f)));

            // ---- terminal, west, mirroring the crate on the far side of the floor ----
            //
            // BUILT ONLY WITH A WALLET CONNECTED. The terminal is a receipt roll for CHAIN
            // transactions, and with no wallet there are none - the local stores deliberately post
            // nothing (see LocalJsonProfileStore). A fixture that is always empty is worse than an
            // absent one: it teaches the player that parts of the room are broken.
            //
            // Its saved position survives the hiding. RoomLayout keys placements by fixture name
            // and DefaultOr only reads them, so a player who arranged the room with the terminal in
            // it gets it back in the same spot when a wallet connects. ShowHub rebuilds the room
            // wholesale on connect, so nothing extra is needed to make it reappear.
            if (Chain.Web.ChainWallet.Connected)
            {
            var terminalAt = DefaultOr("terminal", new Vector2(-4.6f, -3.15f));
            var terminal = TerminalStation.Build(_root, terminalAt);
            var terminalPoint = HubInteractable.Attach(terminal.gameObject, terminalAt + new Vector2(0f, 0.65f), 1.2f,
                () => _editMode
                    ? new Prompt
                    {
                        Title = "THE TERMINAL",
                        Sub = "editing",
                        Body = "Pick it up and stand it somewhere else.",
                        Key = "[ E ]  pick up",
                        Accent = new Color(0.36f, 0.92f, 0.66f),
                    }
                    : DescribeTerminal(),
                () =>
                {
                    if (_editMode) BeginCarryFixture("terminal");
                    else _onOpenTerminal?.Invoke();
                },
                on => terminal.SetFocus(on));
            _points.Add(terminalPoint);
            RegisterMovable("terminal", terminal.transform, Tuning.Hub.TerminalFootprint, terminalPoint);
            }

            // ---- mastery table, ON THE LOFT by default ----
            //
            // The one object whose old position (4.5, 2.05) sat inside the loft's own footprint -
            // everything else in the room was clear of it. Moved up rather than the loft moved
            // around it: a board you stand and read is exactly the "quiet, look-and-think" object
            // the loft exists for, and it was already the room's most upstairs-shaped fixture.
            // DefaultOr means a player who edits it back onto the ground floor stays there.
            var (lx0, lx1, ly0, ly1) = LoftBounds();
            var tableDefault = new Vector2(lx0 + 3.2f, (ly0 + ly1) * 0.5f + 0.05f);
            var tableAt = DefaultOr("table", tableDefault);
            _table = GridTable.Build(_root, tableAt, 1.6f, 1.1f);
            var tablePoint = HubInteractable.Attach(_table.gameObject, tableAt - new Vector2(0f, 0.65f), 1.2f,
                () => _editMode
                    ? new Prompt
                    {
                        Title = "THE GRID",
                        Sub = "editing",
                        Body = "Pick it up and stand it somewhere else.",
                        Key = "[ E ]  pick up",
                        Accent = new Color(0.82f, 0.74f, 0.52f),
                    }
                    : new Prompt
                    {
                        Title = "THE GRID",
                        Sub = "mastery",
                        Body = "Four regions around a shared core. Spend mastery earned by clearing " +
                               "floors; the core is gated on your total across all four elements.",
                        Key = "[ E ]  read the sheet",
                        Accent = new Color(0.82f, 0.74f, 0.52f),
                    },
                () =>
                {
                    if (_editMode) BeginCarryFixture("table");
                    else _onMastery?.Invoke();
                },
                on => _table.SetFocus(on));
            _points.Add(tablePoint);
            RegisterMovable("table", _table.transform, Tuning.Hub.TableFootprint, tablePoint);
        }

        /// <summary>
        /// The couch and a seat for each other character. Only built while it is out of the
        /// crate: a player realistically has one character, so switching is stored away rather
        /// than cut, and its old spot on the loft is the armoury doorway now.
        /// </summary>
        void BuildCouch(Vector2 couchAt)
        {
            var roster = _roster ?? System.Array.Empty<CharacterProfile>();
            bool allowNew = roster.Count < CharacterCouch.MaxSeats;
            int seats = Mathf.Clamp(roster.Count + (allowNew ? 1 : 0), 1, CharacterCouch.MaxSeats);
            _couch = CharacterCouch.Build(_root, couchAt,
                                          CharacterCouch.WidthFor(seats), roster, allowNew);

            var couchPoints = new List<HubInteractable>();
            foreach (var seat in _couch.Seats)
            {
                var st = seat;
                var hi = HubInteractable.Attach(_couch.gameObject, st.Anchor, 0.85f,
                    () => _editMode
                        ? new Prompt
                        {
                            Title = "THE COUCH",
                            Sub = "editing",
                            Body = "Pick it up and stand it somewhere else, or put it away in " +
                                   "the crate. Whoever is sitting here goes with it.",
                            Key = "[ E ]  pick up     [ R ]  into the crate",
                            Accent = new Color(0.55f, 0.58f, 0.66f),
                        }
                    : st.Profile != null
                        ? new Prompt
                        {
                            Title = st.Profile.DisplayName.ToUpper(),
                            Sub = $"{st.Profile.LastElement.ToString().ToLower()}  -  level {st.Profile.Level}",
                            Body = $"{st.Profile.TotalRuns} runs, best {st.Profile.BestKills} kills. " +
                                   "Switching leaves this character exactly as they are.",
                            Key = "[ E ]  play as this character",
                            Accent = ElementInfo.Tint(st.Profile.LastElement),
                        }
                        : new Prompt
                        {
                            Title = "EMPTY SEAT",
                            Sub = "",
                            Body = "Start another character. Gear, mastery and appearance are " +
                                   "all their own - nothing is shared with the one you are playing.",
                            Key = "[ E ]  new character",
                            Accent = new Color(0.55f, 0.58f, 0.66f),
                        },
                    () =>
                    {
                        if (_editMode) { BeginCarryFixture("couch"); return; }
                        if (st.Profile != null) _onSwitchCharacter?.Invoke(st.Profile.ProfileId);
                        else _onNewCharacter?.Invoke();
                    },
                    on => _couch.SetFocus(st, on));
                _points.Add(hi);
                couchPoints.Add(hi);
            }
            RegisterMovable("couch", _couch.transform, Tuning.Hub.CouchFootprint, couchPoints.ToArray());
        }

        /// <summary>Out of the crate and straight into the player's hands, the way a trophy comes
        /// out - it has no spot in the room until it is put down somewhere.</summary>
        void TakeOutCouch()
        {
            if (_couch != null || _avatarGo == null) return;
            BuildCouch(CarryPoint());
            _couchFresh = true;
            BeginCarryFixture("couch");
        }

        /// <summary>Back in the crate. Its last position is kept, so taking it out again and
        /// cancelling is the only way it forgets where it stood.</summary>
        void StoreCouch()
        {
            if (_couch == null) return;
            var m = FindMovable("couch");
            if (m != null)
            {
                foreach (var p in m.Points) _points.Remove(p);
                _movables.Remove(m);
            }
            if (_focused != null && _focused.gameObject == _couch.gameObject) SetFocus(null);
            Destroy(_couch.gameObject);
            _couch = null;
            _couchFresh = false;
            if (_carryFixtureKey == "couch") { _carryFixtureKey = null; _carryColliders.Clear(); }

            _room.CouchOut = false;
            _onLayoutChanged?.Invoke();
            SetFocus(null);
        }

        Prompt DescribeTerminal()
        {
            int n = Chain.TxLog.All.Count;
            int pending = Chain.TxLog.PendingCount;
            return new Prompt
            {
                Title = "TERMINAL",
                Sub = "session ledger",
                Body = n == 0
                    ? "Nothing has been written yet this session."
                    : $"{n} checkpoint{(n == 1 ? "" : "s")} written" +
                      (pending > 0 ? $", {pending} still settling." : ", all confirmed."),
                Key = "[ E ]  read the log",
                Accent = new Color(0.36f, 0.92f, 0.66f),
            };
        }

        /// <summary>World bounds of the loft platform: (x0, x1, y0, y1). Backed by the north
        /// gallery wall (y1 = FloorTop) and the east wall (x1 = _halfWidth) - the one corner
        /// both already share.</summary>
        (float x0, float x1, float y0, float y1) LoftBounds()
            => (_halfWidth - Tuning.Hub.LoftWidth, _halfWidth, Tuning.Hub.LoftY0, FloorTop);

        /// <summary>
        /// The raised platform, same-plane cheat like the gallery band itself: a lighter floor
        /// tint, a skirt line at its two OPEN edges (north and east are already backed by real
        /// walls and need nothing), a shadow strip just outside those edges, and a blocker along
        /// them with one gap for the stairs. No real elevation - see the note on Tuning.Hub's
        /// loft constants for what that would actually cost.
        /// </summary>
        void BuildLoft()
        {
            var (x0, x1, y0, y1) = LoftBounds();
            float cx = (x0 + x1) * 0.5f, cy = (y0 + y1) * 0.5f;
            var stone = new Color(0.24f, 0.25f, 0.30f);

            var top = Quad(_root, "loft-top", x1 - x0, y1 - y0, stone, SortingOrders.FloorDetail);
            top.transform.localPosition = new Vector3(cx, cy, 0f);

            // Skirt lines - the same seam BuildGallery draws at FloorTop, run along whichever
            // edges are NOT already backed by a real wall.
            var skirtColor = new Color(0.34f, 0.35f, 0.41f);
            float stairX0 = x0 + 1.0f, stairX1 = stairX0 + Tuning.Hub.LoftStairGapWidth;

            // west edge (full height - no gap on this side)
            var westSkirt = Quad(_root, "loft-skirt-w", 0.08f, y1 - y0, skirtColor, SortingOrders.FloorDetail + 1);
            westSkirt.transform.localPosition = new Vector3(x0, cy, 0f);

            // south edge, split around the stair gap
            SkirtSegment(x0, stairX0, y0, skirtColor);
            SkirtSegment(stairX1, x1, y0, skirtColor);

            // Shadow: a soft strip on the OPEN floor just outside the platform, west and south.
            var shadow = new Color(0f, 0f, 0f, 0.16f);
            var westShadow = Quad(_root, "loft-shadow-w", 0.35f, y1 - y0, shadow, SortingOrders.FloorDetail + 1);
            westShadow.transform.localPosition = new Vector3(x0 - 0.22f, cy, 0f);
            ShadowSegment(x0, stairX0, y0, shadow);
            ShadowSegment(stairX1, x1, y0, shadow);

            // Stair treads: a few close-set bars in the gap, floor-toned at the bottom and
            // platform-toned at the top, the cheapest possible "this is the way up" without any
            // actual elevation to show.
            int treads = 4;
            for (int i = 0; i < treads; i++)
            {
                float t = (i + 0.5f) / treads;
                var tread = Quad(_root, "loft-stair", stairX1 - stairX0 - 0.1f, 0.10f,
                                 Color.Lerp(new Color(0.115f, 0.108f, 0.13f), stone, t),
                                 SortingOrders.FloorDetail + 2);
                tread.transform.localPosition = new Vector3((stairX0 + stairX1) * 0.5f, y0 + t * 0.7f - 0.35f, 0f);
            }

            // Blockers: west edge solid, south edge solid except the stair gap. Blocker(x,lo,hi,t)
            // already builds a THIN strip at x spanning lo..hi - exactly a vertical wall segment,
            // the same helper BuildWalls uses for the side walls' own door openings.
            Blocker(x0, y0, y1, 0.08f);
            if (stairX0 > x0) BlockerBox(new Vector2((x0 + stairX0) * 0.5f, y0), new Vector2(stairX0 - x0, 0.08f));
            if (x1 > stairX1) BlockerBox(new Vector2((stairX1 + x1) * 0.5f, y0), new Vector2(x1 - stairX1, 0.08f));

            void SkirtSegment(float a, float b, float y, Color c)
            {
                if (b <= a) return;
                var s = Quad(_root, "loft-skirt-s", b - a, 0.08f, c, SortingOrders.FloorDetail + 1);
                s.transform.localPosition = new Vector3((a + b) * 0.5f, y, 0f);
            }
            void ShadowSegment(float a, float b, float y, Color c)
            {
                if (b <= a) return;
                var s = Quad(_root, "loft-shadow-s", b - a, 0.35f, c, SortingOrders.FloorDetail + 1);
                s.transform.localPosition = new Vector3((a + b) * 0.5f, y - 0.22f, 0f);
            }
        }

        /// <summary>
        /// The crate the collection comes out of, and the switch for the room editor.
        ///
        /// Two separate bindings rather than one overloaded [E]: opening the collection is a real,
        /// already-shipped feature and Alt was free (it already means "back out" during trophy
        /// placement, and nothing else in the room uses it). [E] opens the crate normally, or
        /// picks the crate itself up once editing - see <see cref="ToggleEditMode"/> for why
        /// Alt is what enters that mode rather than another press of [E].
        /// </summary>
        void BuildCrate()
        {
            var at = DefaultOr("crate", new Vector2(2.6f, -3.25f));
            var go = new GameObject("showcase-crate");
            go.transform.SetParent(_root, false);
            go.transform.localPosition = at;

            var body = Quad(go.transform, "body", 1.15f, 0.78f,
                            new Color(0.34f, 0.26f, 0.18f), SortingOrders.FloorDetail + 2);
            var lid = Quad(go.transform, "lid", 1.22f, 0.16f,
                           new Color(0.42f, 0.33f, 0.22f), SortingOrders.FloorDetail + 3);
            lid.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            var band = Quad(go.transform, "band", 1.15f, 0.10f,
                            new Color(0.24f, 0.19f, 0.14f), SortingOrders.FloorDetail + 4);
            band.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            // Depth-sorted, unlike the couch and the table. Those sit against the top wall where
            // the player can barely get behind them; this stands in open floor where they walk
            // past it constantly, and a crate that is always in front of you is not a crate.
            DepthSorted.Attach(go, -0.32f, false, body, lid, band);

            var baseColor = body.color;
            _cratePoint = HubInteractable.Attach(go, at + new Vector2(0f, 0.75f), 1.15f,
                () => _editMode
                    ? new Prompt
                    {
                        Title = "THE CRATE",
                        Sub = "editing",
                        Body = _couch == null
                            ? "Pick it up and stand it somewhere else. The couch is packed away in " +
                              "here - take it out to switch characters."
                            : "Pick it up and stand it somewhere else, like everything else in " +
                              "the room right now.",
                        Key = _couch == null
                            ? "[ E ]  pick up     [ R ]  take out the couch     [ Q ]  finish editing"
                            : "[ E ]  pick up     [ Q ]  finish editing",
                        Accent = new Color(0.72f, 0.58f, 0.34f),
                    }
                    : DescribeCrate(),
                () =>
                {
                    if (_editMode) BeginCarryFixture("crate");
                    else _onOpenCollection?.Invoke();
                },
                on => body.color = on ? baseColor * 1.35f : baseColor);
            _points.Add(_cratePoint);
            RegisterMovable("crate", go.transform, Tuning.Hub.CrateFootprint, _cratePoint);
        }

        Prompt DescribeCrate()
        {
            int n = Showcase.Source?.Count ?? 0;
            return new Prompt
            {
                Title = "THE COLLECTION",
                Sub = n > 0 ? $"{n} piece{(n == 1 ? "" : "s")}" : "empty",
                Body = n > 0
                    ? "Take a piece out and stand it anywhere in the room, or hang it on the " +
                      "gallery wall."
                    : "No collection is connected, so there is nothing to display yet.",
                Key = "[ E ]  open the crate     [ Q ]  rearrange the room",
                Accent = new Color(0.72f, 0.58f, 0.34f),
            };
        }

        /// <summary>
        /// Rebuild every trophy from the saved layout.
        ///
        /// A placement whose art no longer resolves is SKIPPED, not defaulted to something else:
        /// a collection can legitimately change between sessions, and standing a placeholder in
        /// the spot where someone's piece used to be is worse than an empty floor.
        /// </summary>
        /// <summary>
        /// Where a portrait is taken - standing against the loft's west riser by default, so it
        /// reads as belonging to the platform beside it.
        ///
        /// It had a stone canopy for a while, flipped above the depth band by a proximity check
        /// so the platform appeared to cover whoever stood in it. That was the wrong answer to a
        /// question the design never asked - the sketch has no elevation in it at all - and what
        /// it actually drew was a grey slab dropping over the player. Removed; the booth is a
        /// fixture on the floor like every other one.
        /// </summary>
        void BuildPhotoBooth()
        {
            var (lx0, _, ly0, _) = LoftBounds();
            var at = DefaultOr("booth", new Vector2(lx0 - 0.62f, ly0 + 0.95f));
            _booth = PhotoBooth.Build(_root, at);
            RefreshBooth();

            var boothPoint = HubInteractable.Attach(_booth.gameObject, at + new Vector2(0f, -0.85f), 1.1f,
                () => _editMode
                    ? new Prompt
                    {
                        Title = "THE BOOTH",
                        Sub = "editing",
                        Body = "Pick it up and stand it somewhere else.",
                        Key = "[ E ]  pick up",
                        Accent = new Color(0.78f, 0.30f, 0.42f),
                    }
                    : new Prompt
                    {
                        Title = "PORTRAIT BOOTH",
                        Sub = Chain.Web.ChainWallet.Connected
                            ? Chain.Web.ChainWallet.Short(Chain.Web.ChainWallet.Address)
                            : "no wallet connected",
                        Body = _booth.CachedLine,
                        Key = _booth.Actionable ? "[ E ]  take a portrait" : "[ E ]  refresh",
                        Accent = new Color(0.78f, 0.30f, 0.42f),
                    },
                () =>
                {
                    if (_editMode) { BeginCarryFixture("booth"); return; }
                    if (_booth.Actionable) _onTakePortrait?.Invoke();
                    else RefreshBooth();   // e.g. the player just equipped the missing piece
                },
                on => _booth.SetFocus(on));
            _points.Add(boothPoint);
            RegisterMovable("booth", _booth.transform, Tuning.Hub.BoothFootprint, boothPoint);
        }


        /// <summary>
        /// Re-reads what a take would say right now. Called on build and after
        /// GameBootstrap finishes a take attempt (accepted, refused, or declined) - never on a
        /// timer and never from an event subscription, because a delegate held on a MonoBehaviour
        /// field would be exactly the trap CLAUDE.md already documents for interfaces and
        /// dictionaries. HubRoom already owns _profile as a plain serializable field and is
        /// rebuilt wholesale on wallet connect and character switch, so nothing here needs to
        /// survive a domain reload on its own.
        /// </summary>
        public void RefreshBooth()
        {
            if (_booth == null) return;
            _ = RefreshBoothAsync();
        }

        public void FlashBooth() => _booth?.Flash();

        async System.Threading.Tasks.Task RefreshBoothAsync()
        {
            if (!Chain.Web.ChainWallet.Connected)
            {
                _booth.SetLine("connect a wallet to take a portrait", false);
                return;
            }

            try
            {
                string line = await Chain.Web.ChainPfp.DescribeAsync(_profile);
                bool actionable = !line.StartsWith("CANNOT") && !line.StartsWith("nothing has changed")
                                   && !line.StartsWith("cannot check");
                if (_booth != null) _booth.SetLine(line, actionable);
            }
            catch (System.Exception e)
            {
                // async void's usual trap, guarded the same way StartRun is - a swallowed
                // exception here would leave the booth stuck on "checking..." forever with
                // nothing in the console to explain why.
                Debug.LogWarning($"[Convergence] portrait booth refresh failed: {e.Message}");
                if (_booth != null) _booth.SetLine("could not reach the chain", false);
            }
        }

        /// <summary>
        /// Where an earned voucher becomes a real item - see Forge's own header for why it holds
        /// no state of its own. Placed south-centre, clear of the circle's own footprint (which
        /// reaches to y -2.625) and mirroring the terminal/crate pair either side of it along the
        /// same wall.
        /// </summary>
        void BuildForge()
        {
            var at = DefaultOr("forge", new Vector2(0f, -3.3f));
            _forge = Forge.Build(_root, at);
            RefreshForge();

            var forgePoint = HubInteractable.Attach(_forge.gameObject, at + new Vector2(0f, 0.35f), 1.05f,
                () => _editMode
                    ? new Prompt
                    {
                        Title = "THE FORGE",
                        Sub = "editing",
                        Body = "Pick it up and stand it somewhere else.",
                        Key = "[ E ]  pick up",
                        Accent = new Color(1f, 0.55f, 0.15f),
                    }
                    : new Prompt
                    {
                        Title = "THE FORGE",
                        Sub = _forge.VoucherCount > 0
                            ? $"{_forge.VoucherCount} voucher{(_forge.VoucherCount == 1 ? "" : "s")} to redeem"
                            : _forge.HasBoxAction
                                ? "boxes to spend"
                                : "nothing to spend - clear a floor to earn a voucher or a box",
                        Body = "Trade a voucher, or a box, for gear - or combine and upgrade what you own.",
                        Key = _forge.VoucherCount > 0 || _forge.HasBoxAction ? "[ E ]  open" : "[ E ]  nothing to redeem",
                        Accent = new Color(1f, 0.55f, 0.15f),
                    },
                () =>
                {
                    if (_editMode) { BeginCarryFixture("forge"); return; }
                    if (_forge.VoucherCount > 0 || _forge.HasBoxAction) _onOpenForge?.Invoke();
                },
                on => _forge.SetFocus(on));
            _points.Add(forgePoint);
            RegisterMovable("forge", _forge.transform, Tuning.Hub.ForgeFootprint, forgePoint);
        }

        /// <summary>Re-reads the voucher count - called on build and after GameBootstrap finishes
        /// a redemption, the same explicit-call discipline RefreshBooth uses and for the same
        /// reason: nothing here should be a delegate held on this MonoBehaviour.</summary>
        public void RefreshForge()
        {
            if (_forge == null || _profile == null) return;
            _forge.SetVoucherCount(_profile.PendingGearVouchers);

            var b = _profile.Boxes;
            bool hasAnyBox = b.Bronze > 0 || b.Silver > 0 || b.Gold > 0 || b.Diamond > 0 ||
                              b.BlackDiamond > 0 || _profile.RiftBoxes > 0;
            // Combining costs no boxes, so a matching pair is reason enough to light the Forge.
            // So is a complete set for a fusion, for the same reason.
            System.Func<string, bool> equipped = id => _profile.Gear.Equipped.Exists(e => e.ItemId == id);
            bool canCombine = Chain.GearForge.Groups(_profile.MintedGear, equipped).Count > 0;
            bool canFuse = System.Array.Exists(Chain.GearForge.Fusions,
                f => Chain.GearForge.CanFuse(f, _profile.MintedGear, equipped));
            _forge.SetHasBoxAction(hasAnyBox || canCombine || canFuse);
        }

        public void FlashForge() => _forge?.Flash();

        /// <summary>
        /// The armoury: a weapon rack and an armour stand, side by side on the west wall.
        ///
        /// TWO FIXTURES, NOT ONE. They were built as a single "gear rack" first and that was
        /// wrong twice over - a weapon hangs on a peg and a suit of armour stands on a form, so
        /// one object had to pick a metaphor and get the other half wrong; and a case holding
        /// exactly one weapon and exactly one breastplate is not a case, it is a label. Split,
        /// each can hold as much as its own shape allows: three pegs, and a whole mannequin.
        ///
        /// PURELY COSMETIC - see either fixture's header. Neither reads the loadout. An earlier
        /// version showed what was EQUIPPED, which made both a status bar: they could only ever
        /// repeat what the character standing beside them already said, and they changed on their
        /// own whenever the player changed gear. What a display case is for is the pieces you are
        /// NOT carrying.
        ///
        /// WEST WALL, and staying there. The couch's old spot went to the doorway into the
        /// armoury proper (see BuildArmouryRoom) - the rack is the one FAVOURITE on show in the
        /// hall, the armoury is the whole collection.
        ///
        /// Positions are checked against every interactable in the room rather than only for
        /// arithmetic clearance, because clearance is necessary and not sufficient: doors carry
        /// Priority 10 and win focus regardless of distance, which is what made the photo booth's
        /// first position silently unreachable.
        /// </summary>
        void BuildArmoury()
        {
            var rackAt = DefaultOr("rack", new Vector2(-6.55f, 0.55f));
            _rack = WeaponRack.Build(_root, rackAt);

            var rackPoint = HubInteractable.Attach(_rack.gameObject, rackAt + new Vector2(0f, -0.7f), 1.0f,
                () => _editMode
                    ? new Prompt
                    {
                        Title = "THE WEAPON RACK",
                        Sub = "editing",
                        Body = "Pick it up and stand it somewhere else.",
                        Key = "[ E ]  pick up",
                        Accent = Gold,
                    }
                    : new Prompt
                    {
                        Title = "THE WEAPON RACK",
                        Sub = _rack.Shown > 0 ? _rack.Name : "empty",
                        Body = _rack.Shown > 0
                            ? "On show, and nothing more - it is not equipped and grants nothing."
                            : "Nothing on the hook.",
                        Key = _rack.Shown > 0 ? "[ E ]  look closer" : "[ E ]  choose what hangs here",
                        Accent = Gold,
                    },
                () =>
                {
                    if (_editMode) { BeginCarryFixture("rack"); return; }
                    _onDressArmoury?.Invoke(true);
                },
                on => _rack.SetFocus(on));
            _points.Add(rackPoint);
            RegisterMovable("rack", _rack.transform, Tuning.Hub.RackFootprint, rackPoint);

            // Directly below the rack and close enough to read as ONE armoury rather than two
            // unrelated objects, while still clearing both footprints - the two are separate
            // fixtures and separately movable, but they start as a pair because that is what
            // they are.
            var standAt = DefaultOr("stand", new Vector2(-6.55f, -1.35f));
            _stand = ArmourStand.Build(_root, standAt);

            var standPoint = HubInteractable.Attach(_stand.gameObject, standAt + new Vector2(0f, -0.95f), 1.0f,
                () => _editMode
                    ? new Prompt
                    {
                        Title = "THE ARMOUR STAND",
                        Sub = "editing",
                        Body = "Pick it up and stand it somewhere else.",
                        Key = "[ E ]  pick up",
                        Accent = Gold,
                    }
                    : new Prompt
                    {
                        Title = "THE ARMOUR STAND",
                        Sub = _stand.Shown > 0 ? $"{_stand.Shown} pieces on the mannequin" : "bare",
                        Body = _stand.Summary(),
                        Key = "[ E ]  dress the mannequin",
                        Accent = Gold,
                    },
                () =>
                {
                    if (_editMode) { BeginCarryFixture("stand"); return; }
                    _onDressArmoury?.Invoke(false);
                },
                on => _stand.SetFocus(on));
            _points.Add(standPoint);
            RegisterMovable("stand", _stand.transform, Tuning.Hub.StandFootprint, standPoint);

            RefreshArmoury();
        }

        /// <summary>
        /// The doorway into the armoury, on the north wall where the couch used to stand. Not
        /// movable - it is an opening in the wall - and Priority 10 like the sigil door, for the
        /// same reason: nothing in the room may ever make a way out hard to reach.
        /// </summary>
        void BuildArmouryDoor()
        {
            float x = _halfWidth - Tuning.Hub.ArmouryDoorFromEast;
            _armouryDoor = Doorway.Build(_root, "armoury-door", x, FloorTop,
                                         Tuning.Hub.DoorHalfWidth, Tuning.Hub.DoorHeight * 0.9f);
            var point = HubInteractable.Attach(_armouryDoor.gameObject, _armouryDoor.Anchor, 1.1f,
                () => new Prompt
                {
                    Title = "THE ARMOURY",
                    Sub = _armoury != null ? $"{_armoury.Owned} of {_armoury.Total} held" : "",
                    Body = "Every weapon there is, on one wall - and an empty space for each one " +
                           "you have not found.",
                    Key = "[ E ]  go in",
                    Accent = Gold,
                },
                EnterArmoury, on => _armouryDoor.SetFocus(on), priority: 10);
            point.ClearanceRadius = Tuning.Hub.DoorClearanceRadius;
            _points.Add(point);
        }

        /// <summary>The room itself, built with the hub and standing far off along X until the
        /// player walks in. Its points join the hub's own list, so the prompt card and the focus
        /// rule serve both rooms without a second copy of either.</summary>
        void BuildArmouryRoom()
        {
            // Refocused after a pick so the card re-reads "also on the rack" at once.
            _armoury = ArmouryRoom.Build(_root, LeaveArmoury, RackShown,
                                         id => { SetRackShown(id); SetFocus(null); },
                                         () => _profile,
                                         () => { _onLookChanged?.Invoke(); SetFocus(null); });
            foreach (var p in _armoury.Points) _points.Add(p);
        }

        /// <summary>True while the player is in the armoury - GameBootstrap reads it to change the
        /// camera over, since the camera is its to move and not the room's.</summary>
        public bool InArmoury => _inArmoury && _armoury != null;

        public Rect ArmouryBounds => _armoury != null ? _armoury.ViewBounds : default;

        public Vector2 AvatarPosition => CarryPoint();

        void EnterArmoury()
        {
            if (_armoury == null) return;
            _editMode = false;   // nothing in there is movable, and the crate that ends editing is out here
            _armoury.Refresh();  // what is held may have changed since the room was built
            Teleport(_armoury.Entry);
            _inArmoury = true;
            // The hall is zoomed so a menu texel is whole screen pixels; the arena's 4/3 would land
            // the body on 2.67 there and shimmer. Its own size beside the weapons, as authored.
            _rig?.SetVisualScale(1f);
            ShowTitle(false);   // the hall's title card, and it would sit across the doorway back
        }

        void LeaveArmoury()
        {
            if (_armouryDoor == null) return;
            Teleport(_armouryDoor.Anchor);
            _inArmoury = false;
            _rig?.SetVisualScale(Tuning.Player.ArenaVisualScale);
            ShowTitle(true);
        }

        void ShowTitle(bool on)
        {
            if (_titleRow != null) _titleRow.gameObject.SetActive(on);
            if (_subRow != null) _subRow.gameObject.SetActive(on);
        }

        /// <summary>Move the avatar without it travelling - both the body and the transform, or
        /// interpolation draws one frame of it streaking across the gap.</summary>
        void Teleport(Vector2 at)
        {
            if (_avatarGo == null) return;
            var rb = _avatarGo.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = at;
                rb.linearVelocity = Vector2.zero;
            }
            _avatarGo.transform.position = at;
            Physics2D.SyncTransforms();
            SetFocus(null);
        }

        /// <summary>
        /// Re-read what is on show from the room layout. Called on build and after a pick - the
        /// same explicit-call discipline RefreshBooth and RefreshForge keep, and for the same
        /// reason: a subscription here would be one more thing that has to survive a domain reload
        /// on its own.
        ///
        /// It is NOT called from RepaintLive, which the single-fixture version was. That was
        /// right while the stand mirrored the equipped loadout and is exactly wrong now - the
        /// armoury has nothing to do with what the character is wearing, so an equip must leave it
        /// completely alone.
        /// </summary>
        /// <summary>
        /// The selector moved: a Prism anywhere in the room lights the new element's gem - in the
        /// hand, on the rack, on the armoury wall.
        /// </summary>
        void Attune(ElementType e)
        {
            Attunement.Set(e);
            if (_rig != null && _profile != null)
                CharacterRigFactory.ApplyAttunement(_rig,
                    GearCatalog.Get(_profile.Look.Resolve(_profile.Gear).Get(GearSlot.Weapon)), e);
            RefreshArmoury();
            _armoury?.Refresh();
        }

        public void RefreshArmoury()
        {
            if (_room == null) return;

            if (_rack != null) _rack.Show(_room.Displayed(WeaponRack.Key));

            if (_stand != null)
                foreach (var slot in Art.Gear.GearSlots.Worn)
                {
                    if (slot == Art.Gear.GearSlot.Weapon) continue;
                    _stand.Set(slot, _room.Displayed(ArmourStand.SlotKey(slot)));
                }
        }

        /// <summary>What is on the rack, or null. Read by GearDisplayScreen for its one "slot".</summary>
        public string RackShown() => _room?.Displayed(WeaponRack.Key);

        /// <summary>
        /// Put a weapon on the rack, or clear it with a null/empty id.
        ///
        /// A second pick REPLACES what is there rather than asking which peg to evict - the rack
        /// holds exactly one weapon, so there is never a non-arbitrary choice to make. An earlier
        /// three-peg rack refused a full case instead, which meant a player who filled it had no
        /// way to put a fourth piece out without first taking one down by hand.
        /// </summary>
        public void SetRackShown(string itemId)
        {
            if (_room == null) return;
            _room.SetDisplayed(WeaponRack.Key, itemId);
            RefreshArmoury();
            _onLayoutChanged?.Invoke();
        }

        /// <summary>What is on the mannequin's given slot, or null.</summary>
        public string StandShown(GearSlot slot) => _room?.Displayed(ArmourStand.SlotKey(slot));

        /// <summary>Dress (or, with a null/empty id, strip) one slot of the mannequin. Keyed by
        /// SLOT rather than by item, unlike the rack: an item already knows its own slot, so a
        /// pick for a slot that is already dressed simply replaces what is there.</summary>
        public void SetStandShown(GearSlot slot, string itemId)
        {
            if (_room == null) return;
            _room.SetDisplayed(ArmourStand.SlotKey(slot), itemId);
            RefreshArmoury();
            _onLayoutChanged?.Invoke();
        }

        /// <summary>
        /// The book in its glass case - see ManualShrine's own header for why it needs no refresh
        /// call. Placed east of the circle, south of the loft's own footprint at any supported
        /// aspect (the loft's y0 is 0.5; this sits at -1.0), mirroring the couch's height band on
        /// the opposite side of the room without ever landing on the platform itself.
        /// </summary>
        void BuildManualShrine()
        {
            var at = DefaultOr("shrine", new Vector2(3.6f, -1.0f));
            _shrine = ManualShrine.Build(_root, at);

            var shrinePoint = HubInteractable.Attach(_shrine.gameObject, at + new Vector2(0f, 0.1f), 0.9f,
                () => _editMode
                    ? new Prompt
                    {
                        Title = "THE SHRINE",
                        Sub = "editing",
                        Body = "Pick it up and stand it somewhere else.",
                        Key = "[ E ]  pick up",
                        Accent = Gold,
                    }
                    : new Prompt
                    {
                        Title = "THE MANUAL",
                        Sub = $"{ManualCatalog.Entries.Length} section{(ManualCatalog.Entries.Length == 1 ? "" : "s")}",
                        Body = "A book explaining what this room's other fixtures actually do.",
                        Key = "[ E ]  read",
                        Accent = Gold,
                    },
                () =>
                {
                    if (_editMode) { BeginCarryFixture("shrine"); return; }
                    _onOpenManual?.Invoke();
                },
                on => _shrine.SetFocus(on));
            _points.Add(shrinePoint);
            RegisterMovable("shrine", _shrine.transform, Tuning.Hub.ShrineFootprint, shrinePoint);
        }

        static readonly Color Gold = new(0.95f, 0.85f, 0.55f);

        /// <summary>
        /// Whether a floor spot counts as "against the gallery wall" - the one rule that decides
        /// a Frame's mode, both live while it is being carried and again when the room is
        /// rebuilt. The gallery is only the stretch of north wall the LOFT backs onto (see
        /// LoftBounds) - that is the one span actually drawn as a face; the rest of the room has
        /// no wall surface to mount anything on. "Against" means as close as the avatar's own
        /// collider can physically get (FloorTop - WallMarginY()), with a little slack so the
        /// zone does not require pixel-perfect contact.
        /// </summary>
        bool IsWallZone(Vector2 at)
        {
            var (lx0, lx1, _, _) = LoftBounds();
            return at.x >= lx0 && at.x <= lx1 && at.y >= FloorTop - WallMarginY() - 0.35f;
        }

        FrameMode ModeFor(Vector2 at) => IsWallZone(at) ? FrameMode.Wall : FrameMode.Easel;

        /// <summary>
        /// Where a Frame is actually DRAWN for a given floor spot and mode - the wall face itself
        /// for Wall mode (clamped so it never hangs off the gallery's own span), or the exact
        /// spot for Easel mode. The floor spot doubles as the interact ANCHOR in both cases - see
        /// Frame.Anchor - so this is the only place the two positions are allowed to diverge.
        /// </summary>
        Vector2 VisualFor(Vector2 at, FrameMode mode)
        {
            if (mode != FrameMode.Wall) return at;
            var (lx0, lx1, _, _) = LoftBounds();
            float half = Tuning.Hub.FrameSize * 0.5f;
            float x = lx1 > lx0 ? Mathf.Clamp(at.x, lx0 + half, lx1 - half) : at.x;
            return new Vector2(x, FloorTop + Tuning.Hub.GalleryHeight * 0.5f);
        }

        void BuildFrames()
        {
            foreach (var f in _frames) if (f != null) Destroy(f.gameObject);
            _frames.Clear();
            _points.RemoveAll(p => p == null || p.GetComponent<Frame>() != null);

            if (_room == null) return;
            var source = Showcase.Source;

            foreach (var placed in _room.Trophies)
            {
                if (source == null || !source.TryGetByKey(placed.Key, out var item)) continue;
                AddFrame(placed.Id, item, new Vector2(placed.X, placed.Y));
            }
        }

        void AddFrame(string id, in ShowcaseItem item, Vector2 at)
        {
            var mode = ModeFor(at);
            var frame = Frame.Build(_root, id, item, mode, VisualFor(at, mode), at, solid: true);
            _frames.Add(frame);

            _points.Add(HubInteractable.Attach(frame.gameObject, at, 1.0f,
                () => new Prompt
                {
                    Title = frame.Title.ToUpper(),
                    Sub = frame.Collection,
                    Body = frame.Mode == FrameMode.Wall
                        ? "Hung on the wall. Change what it shows, or pick it up to move it or " +
                          "put it back in the crate."
                        : "Standing where you put it. Change what it shows, or pick it up to " +
                          "move it or put it back in the crate.",
                    Key = _editMode
                        ? "[ E ]  change art     [ Q ]  pick up"
                        : "[ E ]  change art     [ Q ]  pick up     [ R ]  look closer",
                    Accent = new Color(0.86f, 0.78f, 0.55f),
                },
                () => _onOpenCollectionForFrame?.Invoke(id), on => frame.SetFocus(on)));
        }

        // ------------------------------------------------------------------ placement

        /// <summary>Take a piece out of the crate and start looking for a spot. Always a brand
        /// new frame - see RoomLayout.PlaceTrophy's own note on why identity is never reused
        /// across two different pieces of art.</summary>
        public void BeginPlacement(ShowcaseItem item)
        {
            if (_carrying != null) return;
            _carryItem = item;
            _carryId = System.Guid.NewGuid().ToString("N");
            _carryOrigin = null;
            var at = CarryPoint();
            var mode = ModeFor(at);
            _carrying = Frame.Build(_root, _carryId, item, mode, VisualFor(at, mode), at, solid: false);
        }

        /// <summary>Lift an already-placed frame back off the floor, to move it or return it.</summary>
        void PickUp(string frameId)
        {
            if (_carrying != null || _room == null) return;

            TrophyPlacement placement = null;
            foreach (var t in _room.Trophies) if (t.Id == frameId) { placement = t; break; }
            if (placement == null) return;

            var source = Showcase.Source;
            if (source == null || !source.TryGetByKey(placement.Key, out var item)) return;

            var origin = new Vector2(placement.X, placement.Y);
            _room.RemoveTrophy(frameId);
            _onLayoutChanged?.Invoke();
            RebuildFramePoints();

            _carryItem = item;
            _carryId = frameId;
            _carryOrigin = origin;
            var at = CarryPoint();
            var mode = ModeFor(at);
            _carrying = Frame.Build(_root, frameId, item, mode, VisualFor(at, mode), at, solid: false);
        }

        /// <summary>Change what an already-placed frame shows, without moving it - the crate
        /// picker's callback when it was opened from a standing frame.</summary>
        public void SetFrameArt(string frameId, string key)
        {
            if (_room == null) return;
            _room.SetTrophyKey(frameId, key);
            _onLayoutChanged?.Invoke();
            RebuildFramePoints();
        }

        Vector2 CarryPoint()
            => _avatarGo != null ? (Vector2)_avatarGo.transform.position : Vector2.zero;

        /// <summary>
        /// How close an item's anchor may sit to the EAST/WEST walls: exactly as close as the
        /// avatar's own collider can physically get, since <see cref="CarryPoint"/> places an
        /// item exactly where the avatar is standing - a margin any looser than that carves out a
        /// strip of floor the avatar can stand on but never place anything from (the "edges don't
        /// work" symptom this exists to fix).
        ///
        /// Deliberately NOT widened by the item's own footprint. That was tried - the worry being
        /// a fixture bigger than the avatar's reach could clip through the wall it's set against -
        /// and disproven live: a trophy with art reaching 1.15 units above its anchor, and a Forge
        /// with its sign plaque at 0.9, both photographed flush against the wall and read fine.
        /// Foreground objects here simply draw in front of the wall face regardless of how far
        /// their art reaches past the floor's edge; there is no occlusion to protect against.
        ///
        /// The side walls are CENTRED on <see cref="_halfWidth"/> (see BuildWalls), so half their
        /// own thickness eats into what would otherwise be floor space - hence the extra term
        /// here that <see cref="WallMarginY"/> doesn't need.
        /// </summary>
        float WallMarginX() => Tuning.Hub.WallThickness * 0.5f + Tuning.Hub.AvatarRadius;

        /// <summary>
        /// Same idea against the NORTH/SOUTH walls. Unlike the side walls, BuildWalls sits these
        /// flush against FloorTop/FloorBottom rather than centred on them, so there is no extra
        /// half-thickness term - the floor itself already ends exactly where the wall begins.
        /// </summary>
        float WallMarginY() => Tuning.Hub.AvatarRadius;

        /// <summary>
        /// Somewhere a trophy is allowed to stand: inside the floor, and clear of everything the
        /// player has to be able to walk up to.
        ///
        /// Blocking on the interaction ANCHORS rather than on the art is the important part - a
        /// trophy that merely looks fine next to a door but stands on the spot you have to occupy
        /// to use that door has broken the door, and doors outrank trophies on focus so the
        /// symptom would be a prompt that silently refuses to appear.
        /// </summary>
        bool IsLegal(Vector2 at)
        {
            float mx = WallMarginX(), my = WallMarginY();
            if (at.x < -_halfWidth + mx || at.x > _halfWidth - mx) return false;
            if (at.y < FloorBottom + my || at.y > FloorTop - my) return false;

            foreach (var point in _points)
            {
                if (point == null || !point.BlocksPlacement) continue;
                float clearance = point.EffectiveClearanceRadius * 0.6f + Frame.Radius;
                if (Vector2.Distance(at, point.Anchor) < clearance) return false;
            }

            foreach (var f in _frames)
            {
                if (f == null) continue;
                if (Vector2.Distance(at, f.Anchor) < Frame.Radius * 2.1f) return false;
            }
            return true;
        }

        /// <summary>
        /// The same rule <see cref="IsLegal"/> checks for a trophy, generalised to a fixture's own
        /// real footprint instead of the trophy constant for the INTER-FIXTURE clearance below
        /// (the wall margins are footprint-independent - see WallMarginX's own note on why), and
        /// excluding the fixture's OWN interactables from the clearance scan - without that, a
        /// fixture being carried would always measure itself as "too close to something" and
        /// could never be set back down anywhere, including exactly where it started.
        ///
        /// No loft-awareness needed here on purpose: the loft's open edges already carry real
        /// BoxCollider2D blockers, so the avatar physically cannot stand inside the blocked part
        /// of it, and <see cref="CarryPoint"/> only ever reports where the avatar actually is.
        /// The floor's own physics is doing this check for free.
        /// </summary>
        bool IsLegalFor(Vector2 at, float footprint, HubInteractable[] ignore)
        {
            float mx = WallMarginX(), my = WallMarginY();
            if (at.x < -_halfWidth + mx || at.x > _halfWidth - mx) return false;
            if (at.y < FloorBottom + my || at.y > FloorTop - my) return false;

            foreach (var point in _points)
            {
                if (point == null || !point.BlocksPlacement) continue;
                if (ignore != null && System.Array.IndexOf(ignore, point) >= 0) continue;
                // Frames are judged by the loop below, which knows which ones stand on the floor.
                if (point.GetComponent<Frame>() != null) continue;
                float clearance = point.EffectiveClearanceRadius * 0.6f + footprint;
                if (Vector2.Distance(at, point.Anchor) < clearance) return false;
            }

            // Only an EASEL takes floor. A wall frame hangs on the gallery face and its anchor is
            // just where you stand to use it - measuring furniture against that walled off the
            // whole strip of floor under the gallery, the natural place to stand a fixture.
            foreach (var f in _frames)
            {
                if (f == null || f.Mode == FrameMode.Wall) continue;
                if (Vector2.Distance(at, f.Anchor) < footprint + Frame.Radius * 1.5f) return false;
            }
            return true;
        }

        /// <summary>
        /// A fixture's saved spot, or its built-in default if it has never been moved. Every
        /// BuildX method for a movable fixture calls this instead of using its literal position
        /// directly, so a room the player has rearranged loads exactly as they left it.
        /// </summary>
        Vector2 DefaultOr(string key, Vector2 builtin)
            => _room != null && _room.TryGetFurniture(key, out float x, out float y) ? new Vector2(x, y) : builtin;

        /// <summary>Register a fixture as something the room editor can pick up. Called once per
        /// fixture, right where its own HubInteractable(s) are already being added to _points.</summary>
        void RegisterMovable(string key, Transform root, float footprint, params HubInteractable[] points)
            => _movables.Add(new MovableFixture { Key = key, Root = root, Points = points, Footprint = footprint });

        MovableFixture FindMovable(string key) => _movables.Find(m => m.Key == key);

        /// <summary>Start carrying a fixture - identical shape to BeginPlacement/PickUp for a
        /// trophy, but there is always an origin: a fixture was never "new from the crate".</summary>
        void BeginCarryFixture(string key)
        {
            if (_carrying != null || _carryFixtureKey != null) return;
            var m = FindMovable(key);
            if (m == null) return;

            _carryFixtureKey = key;
            _carryFixtureOrigin = m.Root.position;

            // Taken out of focus contention while it is in hand - the same reason PickUp removes
            // a trophy's interactable rather than leaving it clickable mid-carry.
            foreach (var p in m.Points) _points.Remove(p);

            _carryColliders.Clear();
            foreach (var col in m.Root.GetComponentsInChildren<Collider2D>())
            {
                if (!col.enabled) continue;
                col.enabled = false;
                _carryColliders.Add(col);
            }
        }

        void UpdateFixtureCarry(bool accept)
        {
            var m = FindMovable(_carryFixtureKey);
            if (m == null) { _carryFixtureKey = null; return; }

            var at = CarryPoint();
            m.Root.position = at;
            _carryFixtureLegal = IsLegalFor(at, m.Footprint, m.Points);

            _promptAccent.color = _carryFixtureLegal ? new Color(0.55f, 0.85f, 0.55f)
                                                     : new Color(0.9f, 0.45f, 0.42f);
            _promptTitle.color = _promptAccent.color;
            _promptTitle.text = _carryFixtureKey.ToUpper();
            _promptSub.text = "placing";
            _promptBody.text = _carryFixtureLegal
                ? "Walk to where you want it. It will stand where you are."
                : "Not here - too close to something, or off the floor.";
            // The couch can also go back in the crate, on the same [ Q ] a trophy uses for it.
            bool couch = _carryFixtureKey == "couch";
            _promptKey.text = Core.Controls.TouchMode
                ? (couch ? "PLACE  put it down     CRATE  back to the crate     CANCEL  back where it was"
                         : "PLACE  put it down     CANCEL  back where it was")
                : (couch ? "[ E ]  put it down     [ Q ]  back to the crate     [ ESC ]  cancel"
                         : "[ E ]  put it down     [ ESC ]  back where it was");
            _card.gameObject.SetActive(true);

            if (!accept) return;

            if (couch && Core.Controls.AltTapped) { StoreCouch(); return; }
            if (Core.Controls.CancelTapped || Core.Controls.AltTapped) { EndFixtureCarry(save: false); return; }
            if (Core.Controls.InteractTapped && _carryFixtureLegal) EndFixtureCarry(save: true, at);
        }

        void EndFixtureCarry(bool save, Vector2 at = default)
        {
            // Fresh out of the crate there is nowhere to put it back to but the crate.
            if (!save && _couchFresh && _carryFixtureKey == "couch") { StoreCouch(); return; }
            if (save && _carryFixtureKey == "couch")
            {
                _couchFresh = false;
                _room.CouchOut = true;
            }

            var m = FindMovable(_carryFixtureKey);
            if (m != null)
            {
                var finalPos = save ? at : _carryFixtureOrigin;

                // Against _carryFixtureOrigin, NOT m.Root.position - by the time this runs via
                // the normal flow, UpdateFixtureCarry has already moved the transform to follow
                // the avatar THIS frame, so measuring against it collapses the delta to whatever
                // moved on the last frame alone instead of the whole carry. Caught by testing: a
                // table carried clear across the room left its interactable anchor barely moved
                // while the object itself jumped the full distance.
                var delta = finalPos - _carryFixtureOrigin;
                m.Root.position = finalPos;
                foreach (var p in m.Points)
                {
                    p.Anchor += delta;
                    _points.Add(p);
                }

                if (save)
                {
                    _room.PlaceFurniture(_carryFixtureKey, finalPos.x, finalPos.y);
                    _onLayoutChanged?.Invoke();
                }
            }

            foreach (var col in _carryColliders) if (col != null) col.enabled = true;
            _carryColliders.Clear();

            _carryFixtureKey = null;
            SetFocus(null);
        }

        /// <summary>
        /// The crate's edit-mode toggle. Alt rather than a new key - the same secondary action
        /// already used to back out of carrying a trophy, and the only spare touch-safe button
        /// in the room.
        /// </summary>
        void ToggleEditMode()
        {
            _editMode = !_editMode;
            SetFocus(null);   // forces the prompt to redraw against the new mode immediately
        }

        /// <summary>
        /// <paramref name="accept"/> is false for exactly one frame after a screen closes - see
        /// _wasBlocked. It used to be expressed by passing a null Keyboard, which stopped meaning
        /// anything once the actions came from Core.Controls instead of from a device.
        /// </summary>
        /// <summary>True while a trophy is being carried - the state with its own button set.</summary>
        public bool Placing => _carrying != null || _carryFixtureKey != null;

        void UpdatePlacement(bool accept)
        {
            var at = CarryPoint();
            var mode = ModeFor(at);
            _carrying.UpdateCarry(at, mode, VisualFor(at, mode));
            _carryLegal = IsLegal(at);
            _carrying.SetGhost(_carryLegal);

            _promptAccent.color = _carryLegal ? new Color(0.55f, 0.85f, 0.55f)
                                              : new Color(0.9f, 0.45f, 0.42f);
            _promptTitle.color = _promptAccent.color;
            _promptTitle.text = _carryItem.Title.ToUpper();
            _promptSub.text = "placing";
            _promptBody.text = _carryLegal
                ? (mode == FrameMode.Wall
                    ? "It will hang here on the wall. Step away from the gallery to stand it as an easel instead."
                    : "It will stand where you are. Walk up to the gallery wall to hang it instead.")
                : "Not here - too close to something, or off the floor.";
            _promptKey.text = Core.Controls.TouchMode
                ? "PLACE  put it down     CRATE  back to the crate     CANCEL  leave it be"
                : "[ E ]  put it down     [ Q ]  back to the crate     [ ESC ]  cancel";
            _card.gameObject.SetActive(true);

            if (!accept) return;

            if (Core.Controls.CancelTapped) { CancelPlacement(); return; }
            if (Core.Controls.AltTapped) { EndPlacement(save: false); return; }
            if (Core.Controls.InteractTapped && _carryLegal) EndPlacement(save: true, at);
        }

        /// <summary>Esc puts it back exactly where it was, or back in the crate if it was new.</summary>
        void CancelPlacement()
        {
            if (_carryOrigin.HasValue)
            {
                _room.PlaceTrophy(_carryId, _carryItem.Key, _carryOrigin.Value.x, _carryOrigin.Value.y);
                _onLayoutChanged?.Invoke();
            }
            EndPlacement(save: false);
        }

        void EndPlacement(bool save, Vector2 at = default)
        {
            if (_carrying != null) Destroy(_carrying.gameObject);
            _carrying = null;

            if (save)
            {
                _room.PlaceTrophy(_carryId, _carryItem.Key, at.x, at.y);
                _onLayoutChanged?.Invoke();
            }

            _carryOrigin = null;
            _carryId = null;
            RebuildFramePoints();
            SetFocus(null);
        }

        /// <summary>Rebuild the frames and the interaction points that belong to them.</summary>
        void RebuildFramePoints()
        {
            _points.RemoveAll(p => p == null || p.GetComponent<Frame>() != null);
            BuildFrames();
        }

        void BuildAvatar(CharacterProfile profile)
        {
            _avatarGo = new GameObject("HubAvatar");
            _avatarGo.transform.SetParent(_root, false);
            _avatarGo.transform.localPosition = new Vector3(0f, (FloorTop + FloorBottom) * 0.5f - 0.4f, 0f);

            _rig = CharacterRigFactory.Build(_avatarGo, profile.LastElement, SortingOrders.Character);
            // Drawn at the arena's size (see Tuning.Player.ArenaVisualScale), at the same camera
            // zoom, so the character is one size everywhere it is played - at 1.0 it read small
            // beside the room's own furniture.
            _rig.SetVisualScale(Tuning.Player.ArenaVisualScale);
            CharacterRigFactory.Paint(_rig, profile);

            var col = _avatarGo.AddComponent<CircleCollider2D>();
            col.radius = Tuning.Hub.AvatarRadius;

            var rb = _avatarGo.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = 6f;
            rb.freezeRotation = true;

            // As in the arena (see GameBootstrap.BuildPlayer). The hub has no combat to be precise
            // about, but it is the first thing the game draws and the only room with a tilted
            // camera - the vertical squash of CameraTiltDegrees compresses every 20ms jump into
            // less screen distance without making it any less of a jump, so walking across the
            // room is exactly where stepped motion is easiest to notice and hardest to explain.
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            _avatar = _avatarGo.AddComponent<HubAvatar>();

            // Now that the room contains objects standing on the floor, the character has to sort
            // against them every time it moves - see DepthSorted.
            DepthSorted.Attach(_avatarGo, _rig);
        }

        // ------------------------------------------------------------------ prompt

        void BuildPrompt(CharacterProfile profile)
        {
            var full = UiKit.Rect(_canvas, "HubUi", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _promptPanel = full;

            var titleRow = UiKit.Rect(full, "title", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(56, -116), new Vector2(700, -52));
            UiKit.Label(titleRow, "CONVERGENCE", 44, new Color(0.93f, 0.94f, 0.97f));
            _titleRow = titleRow;

            var subRow = UiKit.Rect(full, "sub", new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(58, -148), new Vector2(700, -116));
            UiKit.Label(subRow, "walk to a door to choose an element  -  the circle, the table and the armoury all do something", 19,
                new Color(0.45f, 0.48f, 0.56f));
            _subRow = subRow;

            var foot = UiKit.Rect(full, "foot", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 34), new Vector2(0, 74));
            string who = $"        profile: {profile.ProfileId}   runs {profile.TotalRuns}"
                       + $"   best {profile.BestKills} kills";
            UiKit.Hint(foot,
                "[WASD] move    [E] interact    [C] loadout" + who,
                "stick to move    USE to interact    GEAR for your loadout" + who,
                18, new Color(0.42f, 0.45f, 0.53f), TextAnchor.MiddleCenter,
                UI.GamepadGlyphs.Move + " move    " + UI.GamepadGlyphs.Interact + " interact    "
                + UI.GamepadGlyphs.Loadout + " loadout" + who);

            // The interaction card. One panel reused by every door and every frame - four cards
            // permanently on screen is the thing this room exists to get rid of.
            var card = UiKit.Panel(full, new Vector2(0.5f, 0), new Vector2(0.5f, 0),
                new Vector2(-380, 96), new Vector2(380, 268), new Color(0.07f, 0.075f, 0.10f, 0.96f));
            _promptAccent = UiKit.Rect(card, "accent", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(0, -5), new Vector2(0, 0)).gameObject.AddComponent<Image>();

            var nameRow = UiKit.Rect(card, "name", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(24, -58), new Vector2(-24, -14));
            _promptTitle = UiKit.Label(nameRow, "", 30, Color.white, TextAnchor.MiddleLeft);

            // The resource name rides the title line rather than the body. Stacked, it pushed the
            // tagline out of the card - and it is a NAME, so it belongs beside the other one.
            _promptSub = UiKit.Label(nameRow, "", 19, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleRight);

            var bodyRow = UiKit.Rect(card, "body", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(24, 48), new Vector2(-24, -64));
            _promptBody = UiKit.Label(bodyRow, "", 19, new Color(0.72f, 0.75f, 0.82f));
            _promptBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            var keyRow = UiKit.Rect(card, "key", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(24, 12), new Vector2(-24, 46));
            _promptKey = UiKit.Label(keyRow, "", 20, new Color(0.9f, 0.9f, 0.95f), TextAnchor.MiddleLeft);

            _card = card;
            _card.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ tick

        void Update()
        {
            // The enlarged view owns input while it is up.
            if (_inspectRoot != null)
            {
                if (Core.Controls.InteractTapped || Core.Controls.CancelTapped) CloseInspect();
                return;
            }

            // A screen is open over the room (loadout, mastery, a confirm). Physics is already
            // stopped, but Update is not - without this, E aimed at a menu would open a door.
            if (GamePause.IsPaused)
            {
                if (_avatar != null) _avatar.InputEnabled = false;
                SetFocus(null);
                _wasBlocked = true;
                return;
            }
            if (_avatar != null) _avatar.InputEnabled = true;
            if (_avatarGo == null) return;

            UpdateAvatarFacingAway();

            // Swallow exactly one frame of input after a screen closes; see _wasBlocked. Passed
            // down rather than returned on, so a piece picked out of the crate still tracks the
            // player on the frame the picker closed instead of hanging at the old position.
            bool justUnblocked = _wasBlocked;
            _wasBlocked = false;

            if (_carrying != null) { UpdatePlacement(!justUnblocked); return; }
            if (_carryFixtureKey != null) { UpdateFixtureCarry(!justUnblocked); return; }

            SetFocus(Nearest((Vector2)_avatarGo.transform.position));
            if (justUnblocked) return;

            // Alt-gated secondary actions, checked ahead of the generic dispatch rather than
            // folded into HubInteractable's own OnInteract, because Alt is not a concept that
            // system knows about anywhere else - Frame placement's own Alt/Cancel handling is the
            // same kind of special case. Each fixture here owns exactly one of these, so a plain
            // if-chain beats a second callback slot on HubInteractable for one caller apiece.
            if (Core.Controls.AltTapped && _focused != null)
            {
                if (_focused == _cratePoint) { ToggleEditMode(); return; }

                var frame = _focused.GetComponent<Frame>();
                if (frame != null) { PickUp(frame.Id); return; }

                // A weapon bay in the armoury: wear (or stop wearing) that design's look.
                if (InArmoury && _armoury.TryUseLook(_focused)) return;
            }

            // The couch in and out of the crate, on the button the enlarged view uses outside edit
            // mode - the two never compete, since that view is refused while rearranging.
            if (_editMode && Core.Controls.SecondAbilityTapped && _focused != null)
            {
                if (_focused == _cratePoint && _couch == null) { TakeOutCouch(); return; }
                if (_couch != null && _focused.gameObject == _couch.gameObject) { StoreCouch(); return; }
            }

            // The enlarged view, on its own button - free everywhere in the hub since nothing
            // else here reads a second ability. Refused while rearranging: the room editor
            // already owns [E]/[Q] on a frame for pick-up and swapping art, and standing still
            // to admire a piece is a different moment from moving furniture around it.
            if (!_editMode && Core.Controls.SecondAbilityTapped && _focused != null)
            {
                var frame = _focused.GetComponent<Frame>();
                if (frame != null && frame.HasArt) { OpenInspect(frame); return; }
            }

            if (Core.Controls.InteractTapped) _focused?.OnInteract?.Invoke();
        }

        /// <summary>
        /// Turn the avatar around while it walks toward the top of the room, on the same
        /// deadzone-and-hold shape <c>PlayerController.UpdateTravelFacingAway</c> uses for the
        /// arena. The hub avatar has no combat state to guard against - see its own class doc,
        /// "no attack, no targeting" - so unlike the arena version this is the ONLY thing that
        /// ever drives it here.
        /// </summary>
        void UpdateAvatarFacingAway()
        {
            if (_avatar == null || _rig == null) return;
            var v = _avatar.Velocity;
            if (v.sqrMagnitude < 0.0025f) return;   // too slow to have a clear heading

            bool away = v.y > Tuning.Player.FacingAwayDot ? true
                      : v.y < -Tuning.Player.FacingAwayDot ? false
                      : _avatarFacingAway;
            if (away == _avatarFacingAway) return;
            _avatarFacingAway = away;
            _rig.SetFacingAway(away);
        }

        /// <summary>
        /// The interaction point the character is standing in.
        ///
        /// Highest PRIORITY first, then nearest. Priority rather than pure distance because
        /// overlap is inevitable in a room this size - a couch pushed near a doorway would
        /// otherwise steal it whenever the player stood between them, and the doors are the one
        /// thing in here that must never become hard to reach.
        /// </summary>
        HubInteractable Nearest(Vector2 p)
        {
            HubInteractable best = null;
            float bestDist = 0f;

            foreach (var point in _points)
            {
                if (point == null) continue;
                float d = Vector2.Distance(p, point.Anchor);
                if (d > point.Radius) continue;
                if (best != null && (point.Priority < best.Priority ||
                                     (point.Priority == best.Priority && d >= bestDist))) continue;
                best = point;
                bestDist = d;
            }
            return best;
        }

        bool _promptTouch;

        void SetFocus(HubInteractable point)
        {
            // Re-render when the DEVICE changes as well as when the focus does. Without the second
            // condition, flipping to touch while already standing at a door left the card saying
            // "[ E ] step in" next to a screen with no keyboard attached - the prompt is only
            // rebuilt on a focus change, and standing still is precisely when there is not one.
            if (point == _focused && _promptTouch == Core.Controls.TouchMode) return;
            _promptTouch = Core.Controls.TouchMode;

            if (point != _focused)
            {
                _focused?.OnFocus?.Invoke(false);
                _focused = point;
                _focused?.OnFocus?.Invoke(true);
            }

            if (point == null || point.Describe == null)
            {
                _card.gameObject.SetActive(false);
                return;
            }

            var prompt = point.Describe();
            _promptAccent.color = prompt.Accent;
            _promptTitle.color = prompt.Accent;
            _promptTitle.text = prompt.Title;
            _promptSub.text = prompt.Sub;
            _promptBody.text = prompt.Body;
            // Every interactable writes its prompt as "[ E ]  do the thing". Substituting the
            // glyph HERE, at the one place they are drawn, beats editing ten declarations - and it
            // cannot go stale when the eleventh is added.
            _promptKey.text = SwapPromptGlyphs(prompt.Key);
            _card.gameObject.SetActive(true);
        }

        /// <summary>
        /// Rewrites the key tokens in a hub prompt for whatever device is in use.
        ///
        /// The prompts are authored with KEYBOARD tokens ("[ E ]  step in", and the carry flows
        /// add "[ ESC ]" and "[ Q ]"), so keyboard needs no work and the other two devices
        /// substitute. Touch only ever rewrote "[ E ]", which is why the carry prompts still read
        /// "[ ESC ] cancel" on a phone - left alone here rather than quietly changed, since touch
        /// answers cancel with its own BACK button and that is a separate decision.
        ///
        /// Gamepad rewrites all three, because a pad genuinely HAS a button for each and leaving
        /// any of them naming a key would be the exact "press a key that does not exist" problem
        /// the hint system exists to avoid.
        /// </summary>
        static string SwapPromptGlyphs(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;

            if (Core.Controls.GamepadMode)
                return key.Replace("[ E ]", UI.GamepadGlyphs.Interact)
                          .Replace("[E]", UI.GamepadGlyphs.Interact)
                          .Replace("[ ESC ]", UI.GamepadGlyphs.Cancel)
                          .Replace("[ Q ]", UI.GamepadGlyphs.Alt)
                          .Replace("[ R ]", UI.GamepadGlyphs.SecondAbility);

            if (Core.Controls.TouchMode)
                return key.Replace("[ E ]", "USE").Replace("[E]", "USE").Replace("[ R ]", "VIEW");

            return key;
        }

        // ------------------------------------------------------------------ inspect

        /// <summary>
        /// The enlarged view. A frame is a hundred pixels tall or so wherever it stands - enough
        /// to say "there is something here", nowhere near enough to be a showcase.
        /// </summary>
        void OpenInspect(Frame frame)
        {
            GamePause.Hold(this);

            _inspectRoot = UiKit.Rect(_canvas, "HubInspect", Vector2.zero, Vector2.one,
                                      Vector2.zero, Vector2.zero);
            UiKit.Panel(_inspectRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.03f, 0.035f, 0.05f, 0.92f));

            var box = UiKit.Panel(_inspectRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-360, -400), new Vector2(360, 400), new Color(0.09f, 0.09f, 0.11f, 0.99f));

            var plate = UiKit.Panel(box, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-300, -620), new Vector2(300, -20), new Color(0.05f, 0.05f, 0.065f));

            var art = UiKit.Rect(plate, "art", Vector2.zero, Vector2.one,
                                 new Vector2(14, 14), new Vector2(-14, -14));
            var img = art.gameObject.AddComponent<Image>();
            img.sprite = frame.Sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;

            var t = UiKit.Rect(box, "t", new Vector2(0, 0), new Vector2(1, 0),
                               new Vector2(40, 118), new Vector2(-40, 166));
            UiKit.Label(t, frame.Title, 30, new Color(0.93f, 0.94f, 0.97f));

            var b = UiKit.Rect(box, "b", new Vector2(0, 0), new Vector2(1, 0),
                               new Vector2(40, 56), new Vector2(-40, 116));
            var body = UiKit.Label(b, string.IsNullOrEmpty(frame.Collection) ? "" : frame.Collection,
                18, new Color(0.62f, 0.65f, 0.73f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var f = UiKit.Rect(box, "f", new Vector2(0, 0), new Vector2(1, 0),
                               new Vector2(40, 20), new Vector2(-40, 54));
            UiKit.Label(f, "[ E ]  back", 19, new Color(0.5f, 0.53f, 0.6f));
        }

        void CloseInspect()
        {
            if (_inspectRoot != null) Destroy(_inspectRoot.gameObject);
            _inspectRoot = null;
            GamePause.Release(this);
        }

        // ------------------------------------------------------------------ teardown

        public void Teardown()
        {
            CloseInspect();
            if (_promptPanel != null) Destroy(_promptPanel.gameObject);
            if (_root != null) Destroy(_root.gameObject);
            _promptPanel = null;
            _root = null;
            _door = null;
            _button = null;
            _cratePoint = null;
            _frames.Clear();
            _points.Clear();
            _carrying = null;
            _movables.Clear();
            _editMode = false;
            _carryFixtureKey = null;
            _carryColliders.Clear();
            _circle = null;
            _table = null;
            _couch = null;
            _couchFresh = false;
            _armouryDoor = null;
            _armoury = null;
            _inArmoury = false;
            _booth = null;
            _forge = null;
            _shrine = null;
            _rig = null;
            _avatar = null;
            _avatarGo = null;
        }

        void OnDestroy() => Teardown();

        static SpriteRenderer Quad(Transform parent, string name, float w, float h, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(w, h, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Spr.Square;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
