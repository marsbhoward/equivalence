using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Convergence.Chain;
using Convergence.Core;
using Convergence.Progression;

namespace Convergence.UI
{
    /// <summary>
    /// The mastery board: a summary of the four elements over a pannable sphere grid.
    ///
    /// The summary mirrors what a mastery screen needs to say at a glance - each element's own
    /// level, and the total that account-wide progress is measured against. The grid below is
    /// where that total gets spent.
    /// </summary>
    public class MasteryScreen : MonoBehaviour
    {
        /// <summary>
        /// Content pixels per grid unit. Sized so one step of the board is wider than the circle
        /// drawn on a node: a node's ring is 34 units across (46 for a keystone), so at a smaller
        /// scale every branch renders as a row of merged blobs rather than as separate nodes.
        /// </summary>
        const float BaseScale = 70f;

        /// <summary>
        /// The gap between two ADJACENT nodes, in content units - MEASURED off the grid rather
        /// than typed, so it stays true when a node is added or moved.
        ///
        /// It is what a node's tap target is worth: the hit test takes the nearest node within
        /// half of this, and the opening zoom is derived so that target is a finger.
        ///
        /// Measured over LINKED PAIRS, not over every pair. Those two are the same number on a
        /// board laid out as an even lattice, and MasteryBoard now is one - but min-over-all-pairs
        /// answers "how close do two nodes ever come", which is a question about layout ACCIDENTS,
        /// while this wants "how far apart is one step". When two domains' keystones once drifted
        /// within a tenth of a step of each other the two answers differed by a factor of ten, and
        /// since the opening zoom is derived from this, the screen opened at eight times the zoom
        /// where the board fits - with a hardcoded floor that could not be reached to get back out.
        /// One near-collision anywhere should never be able to set the zoom for the whole screen.
        /// </summary>
        static float _pitch;

        static float Pitch
        {
            get
            {
                if (_pitch > 0f) return _pitch;
                _pitch = float.MaxValue;
                foreach (var a in MasteryBoard.All)
                foreach (var id in a.Neighbours)
                {
                    var b = MasteryBoard.Get(id);
                    if (b == null) continue;
                    _pitch = Mathf.Min(_pitch, Vector2.Distance(a.Position, b.Position) * BaseScale);
                }
                return _pitch;
            }
        }

        /// <summary>
        /// The zoom the board OPENS at, derived so a node is a finger.
        ///
        /// The grid is 65 nodes across a 613-unit board, and the whole thing fits in the viewport
        /// at about 1.14x - which is why it used to open there. At that zoom the tightest pair of
        /// nodes are 36 units apart, or roughly 14pt on a phone: not a small target, an
        /// unhittable one. It opens ZOOMED IN now and is scrolled instead, which is the trade
        /// every map on a touch device makes.
        ///
        /// Derived from the pitch rather than picked, so it cannot drift from the target size it
        /// exists to produce.
        /// </summary>
        static float DefaultZoom => UiKit.TouchTarget / Pitch;

        /// <summary>
        /// The floor is "the whole board fits", so zooming out can always get back to an overview;
        /// the ceiling is twice the opening zoom, which is as far in as anything is worth
        /// looking at.
        ///
        /// MEASURED against the live viewport rather than typed. As a constant 0.9 it was a floor
        /// in name only: the board needs 0.68 to fit in this viewport, so 0.9 stopped short of the
        /// overview it promised, and the opening zoom was above it by a factor of thirty-nine
        /// anyway. A floor that cannot reach the thing it is named after is worse than none - the
        /// board simply had no way back out.
        /// </summary>
        float MinZoom
        {
            get
            {
                if (_viewport == null) return 0.5f;
                var v = _viewport.rect.size;
                return Mathf.Min(v.x, v.y) / Mathf.Max(1f, BoardExtent);
            }
        }

        float MaxZoom => Mathf.Max(DefaultZoom * 2f, MinZoom);

        /// <summary>
        /// Where the board opens, and it FOLLOWS THE DEVICE rather than being one number.
        ///
        /// <see cref="DefaultZoom"/> exists to make a node a finger, and that is a real
        /// requirement on glass - at the overview the tightest pair of nodes are a few points
        /// apart and simply cannot be tapped. A MOUSE has no such problem: it hits a twenty-pixel
        /// circle exactly as easily as a hundred-pixel one, so paying for a finger-sized target
        /// with four fifths of the board is a cost with nothing bought by it. The first question
        /// this screen answers is "where can I go", and on a pointer device it can answer that
        /// immediately instead of opening inside one domain with no indication there are five
        /// more.
        ///
        /// Same shape as the HUD's own touch layout and the hint text: one rule, read live,
        /// because the mode follows the last device used and can change mid-session.
        /// </summary>
        float OpeningZoom => Core.Controls.TouchMode ? DefaultZoom : MinZoom;

        /// <summary>The four boards, in the order the chips along the top present them.</summary>
        static readonly ElementType[] BoardElements =
            { ElementType.Fire, ElementType.Water, ElementType.Earth, ElementType.Air };

        public bool IsOpen { get; private set; }

        System.Func<CharacterProfile> _profile;
        BoardState _board;

        /// <summary>
        /// Which element's board is on screen. The four boards share one node layout, so the
        /// element is not decoration - it decides what every node on screen currently costs, is
        /// locked by, and resolves to. The chips along the top SELECT this rather than merely
        /// reporting levels, which is what they did when there was only one board.
        /// </summary>
        ElementType _viewing = ElementType.Fire;
        GameObject _root;
        RectTransform _content, _viewport;
        float _zoom;
        Vector2 _pan;
        bool _dragging;
        Vector2 _dragOrigin, _panOrigin;

        string _selectedId;
        string _notice;
        Text _detailTitle, _detailBody, _detailAction, _totalText, _globalText;
        readonly Dictionary<string, Image> _nodeImages = new();
        readonly Dictionary<string, Image> _nodeRings = new();
        readonly List<(Text Level, Image Fill, Text Spare, RectTransform Rect, Image Bg)> _elementChips = new();

        public void Init(System.Func<CharacterProfile> profile) => _profile = profile;

        // ------------------------------------------------------------------ open / close

        public void Toggle(Transform canvas)
        {
            if (IsOpen) Close(); else Open(canvas);
        }

        public void Open(Transform canvas)
        {
            if (IsOpen || _profile == null) return;
            IsOpen = true;
            _board = new BoardState(_profile().Mastery);

            // Taken off the profile as the screen opens (so it is shown once) and held here while
            // it stays open.
            _notice = _profile().Mastery.BoardNotice;
            _profile().Mastery.BoardNotice = "";
            Core.GamePause.Hold(this);
            _pan = Vector2.zero;
            _selectedId = null;

            Build(canvas);

            // After Build, because both ends of this are measured off the viewport Build creates.
            _zoom = Mathf.Clamp(OpeningZoom, MinZoom, MaxZoom);

            RefreshAll();

            // The board opens ZOOMED, so the transform has to be applied - Build lays the content
            // out at scale 1 and only a pan or a pinch used to touch it, so setting _zoom above
            // without this left the field saying 3.59 while the screen showed the whole board at
            // 1. Everything measured correctly and nothing on screen had changed.
            ApplyTransform();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Core.GamePause.Release(this);
            if (_root) Destroy(_root);
            _nodeImages.Clear();
            _nodeRings.Clear();
            _elementChips.Clear();
        }

        // ------------------------------------------------------------------ build

        void Build(Transform canvas)
        {
            _root = new GameObject("MasteryScreen", typeof(RectTransform));
            _root.transform.SetParent(canvas, false);
            var full = (RectTransform)_root.transform;
            full.anchorMin = Vector2.zero; full.anchorMax = Vector2.one;
            full.offsetMin = Vector2.zero; full.offsetMax = Vector2.zero;

            UiKit.Panel(full, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0.035f, 0.04f, 0.055f, 1f));

            // ---- header ----
            var title = UiKit.Rect(full, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(36, -76), new Vector2(-36, -24));
            UiKit.Label(title, "MASTERY", 34, new Color(0.93f, 0.94f, 0.97f));

            var totalRow = UiKit.Rect(full, "tot", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(36, -114), new Vector2(-36, -78));
            _totalText = UiKit.Label(totalRow, "", 20, new Color(0.7f, 0.73f, 0.8f));

            var globalRow = UiKit.Rect(full, "glob", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(36, -148), new Vector2(-36, -116));
            _globalText = UiKit.Label(globalRow, "", 18, new Color(0.5f, 0.85f, 0.6f));

            var hint = UiKit.Rect(full, "hint", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(36, -76), new Vector2(-36, -24));
            UiKit.Hint(hint,
                "drag to pan    -    scroll or two-finger swipe to zoom    -    [M] or [ESC] close",
                "drag to pan    -    pinch to zoom    -    BACK to close",
                16, new Color(0.42f, 0.45f, 0.52f), TextAnchor.MiddleRight,
                UI.GamepadGlyphs.Navigate + " to move    -    " + UI.GamepadGlyphs.ZoomOut + " / " + UI.GamepadGlyphs.ZoomIn + " zoom    -    "
                + UI.GamepadGlyphs.Mastery + " or " + UI.GamepadGlyphs.Cancel + " close");

            BuildElementChips(full);
            BuildViewport(full);
            BuildDetail(full);
        }

        void BuildElementChips(RectTransform parent)
        {
            for (int i = 0; i < BoardElements.Length; i++)
            {
                var e = BoardElements[i];
                var tint = ElementInfo.Tint(e);
                float w = 250f, gap = 12f;
                float x = 36f + i * (w + gap);

                var card = UiKit.Panel(parent, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(x, -252), new Vector2(x + w, -160), new Color(0.08f, 0.09f, 0.13f, 1f));

                var accent = UiKit.Rect(card, "a", new Vector2(0, 0), new Vector2(0, 1),
                    new Vector2(0, 0), new Vector2(5, 0));
                accent.gameObject.AddComponent<Image>().color = tint;

                var name = UiKit.Rect(card, "n", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(16, -32), new Vector2(-12, -6));
                UiKit.Label(name, e.ToString().ToUpper(), 17, tint);

                var lvl = UiKit.Rect(card, "l", new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(16, -62), new Vector2(-12, -30));
                var lvlText = UiKit.Label(lvl, "", 22, new Color(0.9f, 0.92f, 0.96f));

                var barRt = UiKit.Rect(card, "b", new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(16, 22), new Vector2(-12, 32));
                var fill = UiKit.Bar(barRt, tint, new Color(0.13f, 0.14f, 0.18f, 1f));

                var spareRt = UiKit.Rect(card, "s", new Vector2(0, 0), new Vector2(1, 0),
                    new Vector2(16, 2), new Vector2(-12, 22));
                var spare = UiKit.Label(spareRt, "", 14, new Color(0.5f, 0.53f, 0.6f));

                _elementChips.Add((lvlText, fill, spare, card, card.GetComponent<Image>()));
            }
        }

        void BuildViewport(RectTransform parent)
        {
            _viewport = UiKit.Panel(parent, new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(36, 36), new Vector2(-372, -264), new Color(0.05f, 0.055f, 0.075f, 1f));
            _viewport.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject("content", typeof(RectTransform));
            contentGo.transform.SetParent(_viewport, false);
            _content = (RectTransform)contentGo.transform;
            _content.anchorMin = _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.sizeDelta = Vector2.zero;

            // Edges first so nodes draw over them.
            var drawn = new HashSet<string>();
            foreach (var node in MasteryBoard.All)
            foreach (var otherId in node.Neighbours)
            {
                string key = string.CompareOrdinal(node.Id, otherId) < 0
                    ? node.Id + "|" + otherId : otherId + "|" + node.Id;
                if (!drawn.Add(key)) continue;
                var other = MasteryBoard.Get(otherId);
                if (other != null) BuildEdge(node, other);
            }

            foreach (var node in MasteryBoard.All) BuildNode(node);
        }

        void BuildEdge(MasteryNode a, MasteryNode b)
        {
            var go = new GameObject("edge", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var rt = (RectTransform)go.transform;

            Vector2 pa = a.Position * BaseScale, pb = b.Position * BaseScale;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = (pa + pb) * 0.5f;
            rt.sizeDelta = new Vector2(Vector2.Distance(pa, pb), 2f);
            rt.localRotation = Quaternion.Euler(0, 0,
                Mathf.Atan2(pb.y - pa.y, pb.x - pa.x) * Mathf.Rad2Deg);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.24f, 0.3f, 1f);
            img.raycastTarget = false;
        }

        /// <summary>A node's drawn size says what kind it is - a decision, a rule, a capstone, or
        /// one more unit of a stat.</summary>
        static float SizeOf(MasteryNode node) => node.Kind switch
        {
            NodeKind.Keystone => 36f,
            NodeKind.Tincture => 30f,
            NodeKind.Opus => 42f,
            NodeKind.Rebis => 42f,
            _ => 24f,
        };

        void BuildNode(MasteryNode node)
        {
            float size = SizeOf(node);

            var ringGo = new GameObject("ring", typeof(RectTransform));
            ringGo.transform.SetParent(_content, false);
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.anchoredPosition = node.Position * BaseScale;
            ringRt.sizeDelta = Vector2.one * (size + 10f);
            var ring = ringGo.AddComponent<Image>();
            ring.sprite = Spr.Ring;
            ring.raycastTarget = false;
            _nodeRings[node.Id] = ring;

            var go = new GameObject(node.Id, typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = node.Position * BaseScale;
            rt.sizeDelta = Vector2.one * size;

            var img = go.AddComponent<Image>();
            img.sprite = Spr.Circle;
            img.raycastTarget = false;
            _nodeImages[node.Id] = img;
        }

        void BuildDetail(RectTransform parent)
        {
            var panel = UiKit.Panel(parent, new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(-336, 36), new Vector2(-36, -264), new Color(0.08f, 0.09f, 0.13f, 1f));

            var t = UiKit.Rect(panel, "t", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(18, -56), new Vector2(-18, -16));
            _detailTitle = UiKit.Label(t, "select a node", 22, new Color(0.9f, 0.92f, 0.96f));

            var b = UiKit.Rect(panel, "b", new Vector2(0, 0), new Vector2(1, 1),
                new Vector2(18, 90), new Vector2(-18, -60));
            _detailBody = UiKit.Label(b, "", 17, new Color(0.6f, 0.63f, 0.7f));
            _detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;

            var a = UiKit.Panel(panel, new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(18, 24), new Vector2(-18, 78), new Color(0.13f, 0.15f, 0.2f, 1f));
            var at = UiKit.Rect(a, "at", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _detailAction = UiKit.Label(at, "", 18, new Color(0.5f, 0.53f, 0.6f), TextAnchor.MiddleCenter);
            _actionRect = a;
        }

        RectTransform _actionRect;

        // ------------------------------------------------------------------ refresh

        void RefreshAll()
        {
            var m = _profile().Mastery;

            // A rebuild's note (what a retired board's purchases gave back) stands in for the total
            // for as long as the screen is open - it is the thing the player most needs to read.
            _totalText.text = string.IsNullOrEmpty(_notice) ? $"Total Mastery Level  {m.TotalLevel}" : _notice;

            // What this element's board gives, in the words gear uses - the stat block's half of it.
            var parts = new List<string>();
            var pts = _board.Points(_viewing);
            foreach (Art.Gear.StatKind k in System.Enum.GetValues(typeof(Art.Gear.StatKind)))
            {
                if (k == Art.Gear.StatKind.None) continue;
                float v = Art.Gear.GearRoller.PointsOf(pts, k);
                if (v > 0.01f) parts.Add($"{Chain.GearForge.Label(k)} +{v:0.#}%");
            }
            foreach (BoardStat b in System.Enum.GetValues(typeof(BoardStat)))
            {
                if (b == BoardStat.None) continue;
                float v = _board.Extra(_viewing, b);
                if (v <= 0f) continue;
                parts.Add(b == BoardStat.Lifesteal ? $"{MasteryBoard.Label(b)} +{v * 100f:0.#}%"
                        : b == BoardStat.RiftCapacity ? $"{MasteryBoard.Label(b)} +{v:0}"
                        : $"{MasteryBoard.Label(b)} +{v:0}%");
            }
            _globalText.text = parts.Count > 0 ? string.Join("    ", parts) : $"nothing bought on the {_viewing} board yet";



            for (int i = 0; i < _elementChips.Count; i++)
            {
                var e = BoardElements[i];
                var em = m.For(e);
                var chip = _elementChips[i];
                chip.Level.text = $"Level {em.Level}";
                UiKit.SetFill(chip.Fill, em.Progress01);

                // Each chip reports its OWN board, not a shared pool: spending in Water costs
                // Earth nothing, so four separate "spent of cap" readings is the honest picture.
                int spent = _board.Spent(e);
                int remaining = _board.Remaining(e);
                chip.Spare.text = $"{spent} spent  -  {remaining} to spend";

                bool selected = e == _viewing;
                chip.Bg.color = selected ? new Color(0.16f, 0.18f, 0.26f, 1f)
                                         : new Color(0.08f, 0.09f, 0.13f, 1f);
            }

            foreach (var node in MasteryBoard.All) RefreshNode(node);
            RefreshDetail();
        }

        static Color PrincipleTint(Principle p) => p switch
        {
            Principle.Sulfur  => new Color(0.95f, 0.55f, 0.28f),   // volatile
            Principle.Mercury => new Color(0.55f, 0.80f, 0.95f),   // fluid
            _                 => new Color(0.72f, 0.78f, 0.62f),   // fixed
        };

        void RefreshNode(MasteryNode node)
        {
            if (!_nodeImages.TryGetValue(node.Id, out var img)) return;

            // The Rebis is not on a board whose element has one ability - nothing to choose.
            bool visible = BoardState.Visible(_viewing, node);
            img.enabled = visible;
            if (_nodeRings.TryGetValue(node.Id, out var hiddenRing)) hiddenRing.enabled = visible;
            if (!visible) return;

            // Colour reads the PRINCIPLE, not the element. Every board shows the same layout, so
            // tinting by element would paint one board entirely one colour and say nothing; the
            // principle is the axis a player is actually reading the board along.
            // The Rebis belongs to no principle: drawn white, the union of the three.
            Color baseColor = node.Kind == NodeKind.Rebis ? new Color(0.92f, 0.9f, 0.86f) : PrincipleTint(node.Principle);

            bool owned = _board.IsUnlocked(_viewing, node.Id);
            var check = _board.Check(_viewing, node);

            if (owned) img.color = baseColor;
            else if (check == BoardState.Blocked.None) img.color = baseColor * 0.75f;
            else if (_board.IsReachable(_viewing, node)) img.color = baseColor * 0.32f;
            else img.color = new Color(0.16f, 0.17f, 0.22f);

            var ring = _nodeRings[node.Id];
            bool selected = node.Id == _selectedId;
            bool buyable = !owned && check == BoardState.Blocked.None;
            ring.color = selected ? new Color(1f, 1f, 1f, 0.95f)
                       : buyable  ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.8f)
                       : new Color(0f, 0f, 0f, 0f);
        }

        void RefreshDetail()
        {
            var node = MasteryBoard.Get(_selectedId);
            if (node == null)
            {
                _detailTitle.text = "select a node";
                _detailBody.text = "Each domain forks into branches. A branch's KEYSTONE is the " +
                                   "decision - taking it closes the domain's other branches on this " +
                                   "board - then its nodes run out to a TINCTURE (a rule that changes " +
                                   "how you play) and an OPUS (its capstone). Every node also feeds a " +
                                   "principle, and enough of one unlocks the next link of its chain.";
                _detailAction.text = "";
                if (_actionRect) _actionRect.gameObject.SetActive(false);
                return;
            }

            bool owned = _board.IsUnlocked(_viewing, node.Id);
            if (_actionRect) _actionRect.gameObject.SetActive(true);

            string branch = MasteryBoard.BranchName(node.Branch);
            _detailTitle.text = node.Kind switch
            {
                NodeKind.Rebis => "THE REBIS",
                NodeKind.Tincture => $"{MasteryBoard.NameOf(node.Notable)} - TINCTURE",
                NodeKind.Opus when node.Notable != Notable.None => $"{MasteryBoard.NameOf(node.Notable)} - OPUS",
                NodeKind.Opus => $"{branch} - OPUS",
                NodeKind.Keystone when node.Notable != Notable.None => $"{MasteryBoard.NameOf(node.Notable)} - {branch} KEYSTONE",
                NodeKind.Keystone => $"{branch} - KEYSTONE",
                _ => branch,
            };

            var body = new System.Text.StringBuilder();
            string statLine = MasteryBoard.StatLine(node);
            if (statLine.Length > 0) body.Append(statLine);
            if (node.Notable != Notable.None)
            {
                if (body.Length > 0) body.Append("\n\n");
                body.Append(MasteryBoard.Describe(node.Notable));
            }
            if (node.Kind == NodeKind.Filler && node.Stat == Art.Gear.StatKind.None && node.Extra == BoardStat.None)
                body.Append("Carries its principle point and nothing else.");
            if (node.Kind == NodeKind.Rebis) body.Append($"\n\nbelongs to no principle");
            else body.Append($"\n\n{branch}  -  {node.Principle}");
            body.Append($"\n\ncost  {node.Cost} {_viewing} level{(node.Cost == 1 ? "" : "s")}");

            if (node.Keystone && MasteryBoard.BranchesOf(node.Domain).Length > 1)
            {
                // The one irreversible choice on the board, so it says so before it is taken
                // rather than after.
                body.Append("\n\nTaking this closes this domain's other branches on this board, " +
                            "permanently. Only reincarnation reopens them.");
            }

            if (node.Kind == NodeKind.Rebis)
            {
                string first = ElementInfo.AbilityName(_viewing);
                string second = ElementInfo.SecondAbilityName(_viewing);
                if (second == null)
                    body.Append($"\n\nWould offer an alternative to {first}, but {_viewing} has " +
                                "none built yet.");
                else if (!owned)
                    body.Append($"\n\nAlso unlocks {second} as an alternative to {first}. One is " +
                                "active at a time, on the same button - switch here, free.");
                else
                {
                    bool useSecond = _board.UsesSecondAbility(_viewing);
                    body.Append($"\n\nABILITY   {(useSecond ? first : $"[{first}]")}  /  " +
                                $"{(useSecond ? $"[{second}]" : second)}");
                }
            }

            if (node.PrincipleWeight > 0)
            {
                body.Append($"\n\ncontributes {node.PrincipleWeight} {node.Principle} - you hold " +
                            $"{_board.PrinciplePoints(_viewing, node.Principle)}");

                // THE CHAIN, spelled out. Points on their own say nothing: a player looking at a
                // node is deciding what it buys, and for the principle half what it buys is a link.
                body.Append(ChainSummary(node.Principle));
            }
            _detailBody.text = body.ToString();

            if (owned)
            {
                // The one owned node a click still does something on: it switches the ability.
                _detailAction.text = _board.HasSecondAbility(_viewing) && node.Kind == NodeKind.Rebis
                    ? $"CLICK AGAIN TO SWITCH TO " +
                      (_board.UsesSecondAbility(_viewing) ? ElementInfo.AbilityName(_viewing)
                                                          : ElementInfo.SecondAbilityName(_viewing))
                    : "UNLOCKED";
                return;
            }
            _detailAction.text = _board.Check(_viewing, node) switch
            {
                BoardState.Blocked.None           => "CLICK AGAIN TO UNLOCK",
                BoardState.Blocked.Unreachable    => "not connected yet",
                BoardState.Blocked.KeystoneLocked => "another keystone in this domain was taken",
                BoardState.Blocked.Hidden         => "this element has one ability",
                BoardState.Blocked.AlreadyOwned   => "UNLOCKED",
                _                                 => "not enough spare levels",
            };
        }

        /// <summary>
        /// A principle's chain, with what is held, what is earned, and what the next link costs.
        ///
        /// The NEXT link is named as well as the earned ones, because the distance to it is the
        /// number the player is actually weighing when they look at a node - "seven more points"
        /// means nothing without "...for Detonate".
        /// </summary>
        string ChainSummary(Principle principle)
        {
            var chain = MasteryBoard.ChainOf(principle);
            int pts = _board.PrinciplePoints(_viewing, principle);
            int links = _board.ChainLinks(_viewing, principle);

            var sb = new System.Text.StringBuilder();
            sb.Append($"\n\n{principle.ToString().ToUpper()} CHAIN   {links} of {chain.Length}");

            for (int i = 0; i < chain.Length; i++)
            {
                bool have = i < links;
                sb.Append($"\n  {(have ? "+" : "-")} {chain[i].Name}");
                if (have) sb.Append($"   {chain[i].What}");
                else sb.Append($"   at {MasteryBoard.Thresholds[i]} ({MasteryBoard.Thresholds[i] - pts} more)");
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ input

        /// <summary>
        /// The one screen with a gesture rather than a list of buttons: the board is bigger than
        /// the viewport, so it pans and zooms.
        ///
        /// A drag is a drag on either device - one finger or a held mouse button - so the only
        /// thing that had to be added for touch was the pinch, which arrives through
        /// <c>Controls.Zoom</c> in the same units as a wheel notch. The tap-versus-drag test is
        /// what makes one finger do both jobs, and its threshold is the reason it works: a mouse
        /// click lands within a pixel or two, a thumb wanders, so the slop below is generous
        /// enough that a tap on a node is not read as a one-pixel pan.
        /// </summary>
        void Update()
        {
            if (!IsOpen) return;

            var at = Core.Controls.PointerPosition;
            bool overViewport = RectTransformUtility.RectangleContainsScreenPoint(_viewport, at, null);

            float notches = Core.Controls.Zoom;
            if (overViewport && Mathf.Abs(notches) > 0.001f)
            {
                // 12% per notch, compounded - so zooming is geometric and a notch is worth the
                // same PROPORTION at every scale. Linear steps feel fine zoomed out and crawl
                // zoomed in, because the same absolute step is a smaller fraction of a bigger
                // number.
                float want = Mathf.Clamp(_zoom * Mathf.Pow(1.12f, notches), MinZoom, MaxZoom);

                // Zoom about the POINTER, not the middle. Pinching to inspect a node in the
                // corner and having it slide off screen is the difference between a map you can
                // read and one you fight - so the content is panned to keep whatever is under
                // the fingers under the fingers.
                //
                // The content point under `focus` is (focus - pan) / zoom, and it has to still be
                // under `focus` at the new scale, so pan' = pan*k + focus*(1-k) with k the ratio
                // of the two zooms. `pan -= focus * (k - 1)` is the same thing ONLY when the pan
                // is zero - correct on the first pinch, and wrong by pan*(k-1) on every one after
                // it, which reads as the board lurching sideways as you zoom.
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _viewport, at, null, out var focus))
                {
                    float k = want / _zoom;
                    _pan = _pan * k + focus * (1f - k);
                }

                _zoom = want;
                ApplyTransform();
            }

            // A pinch is not a pan. Both used to run off the same fingers, so zooming also
            // hurled the board sideways.
            if (Core.Controls.Pinching) _dragging = false;
            else if (Core.Controls.Tapped(out var down) && overViewport)
            {
                _dragging = true;
                _dragOrigin = down;
                _panOrigin = _pan;
            }
            if (!Core.Controls.PointerHeld(out _)) _dragging = false;

            if (_dragging)
            {
                var delta = at - _dragOrigin;
                if (delta.sqrMagnitude > DragSlop) { _pan = _panOrigin + delta; ApplyTransform(); }
            }

            // select / unlock - a press that did not turn into a drag
            if (Core.Controls.PointerReleased && overViewport)
            {
                var delta = at - _dragOrigin;
                if (delta.sqrMagnitude <= TapSlop) ClickAt(at);
            }
            // Switching boards. Handled apart from ClickAt because the chips are OUTSIDE the
            // viewport - the board's own tap path is gated on being over it, so that a drag that
            // wanders off the edge does not select a node on release.
            else if (Core.Controls.PointerReleased)
            {
                for (int i = 0; i < _elementChips.Count; i++)
                {
                    if (!RectTransformUtility.RectangleContainsScreenPoint(_elementChips[i].Rect, at, null))
                        continue;
                    if (BoardElements[i] == _viewing) break;
                    _viewing = BoardElements[i];
                    // The selection is a node id, and the same id means a different purchase on a
                    // different board - so it is cleared rather than carried across.
                    _selectedId = null;
                    RefreshAll();
                    break;
                }
            }
        }

        /// <summary>
        /// How far a press may travel and still count as a tap, and how far before it starts
        /// panning. In screen pixels, squared.
        ///
        /// Both are wider than the mouse-only values they replace (4 and 16). A finger covers
        /// tens of pixels of glass and rolls as it lifts, so at the old threshold a deliberate tap
        /// on a node registered as a two-pixel pan and the node never opened - which reads as the
        /// board ignoring you rather than as a threshold being wrong.
        /// </summary>
        const float DragSlop = 64f;    // 8 px
        const float TapSlop = 400f;    // 20 px

        /// <summary>
        /// Keep at least a viewport's worth of board reachable.
        ///
        /// Unclamped, one flick on a phone throws the whole grid off screen with nothing left to
        /// grab - and there is no scrollbar and no "reset view" to get back with. The allowance
        /// is half a viewport past each edge, so a node at the very rim can still be dragged into
        /// the middle to be read.
        /// </summary>
        Vector2 ClampPan(Vector2 pan)
        {
            if (_viewport == null) return pan;
            var half = _viewport.rect.size * 0.5f;
            var board = new Vector2(BoardExtent, BoardExtent) * (_zoom * 0.5f);
            var limit = board + half * 0.5f;
            return new Vector2(Mathf.Clamp(pan.x, -limit.x, limit.x),
                               Mathf.Clamp(pan.y, -limit.y, limit.y));
        }

        static float _extent;

        /// <summary>Widest span of the board in content units, measured off the grid.</summary>
        static float BoardExtent
        {
            get
            {
                if (_extent > 0f) return _extent;
                float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
                foreach (var n in MasteryBoard.All)
                {
                    minX = Mathf.Min(minX, n.Position.x); maxX = Mathf.Max(maxX, n.Position.x);
                    minY = Mathf.Min(minY, n.Position.y); maxY = Mathf.Max(maxY, n.Position.y);
                }
                _extent = Mathf.Max(maxX - minX, maxY - minY) * BaseScale;
                return _extent;
            }
        }

        void ApplyTransform()
        {
            _pan = ClampPan(_pan);
            _content.localScale = Vector3.one * _zoom;
            _content.anchoredPosition = _pan;
        }

        /// <summary>
        /// Which node a tap landed on, resolved in the board's OWN space.
        ///
        /// It used to compare a screen distance against `22f * _zoom` - two different spaces. The
        /// zoom was accounted for and the CANVAS SCALE was not, so the same node was a different
        /// size to the finger on every device: on a high-DPI phone it drew large and stayed a
        /// 22-pixel target. Converting the tap into content units instead makes the radius
        /// independent of both zoom and device, which is also the honest description of what the
        /// test means - nearer to this node than any other, within half a node's spacing.
        /// </summary>
        void ClickAt(Vector2 screenPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _content, screenPoint, null, out var local)) return;

            MasteryNode hit = null; float best = float.MaxValue;
            float reach = Pitch * 0.5f;
            foreach (var node in MasteryBoard.All)
            {
                if (!BoardState.Visible(_viewing, node)) continue;
                float d = Vector2.Distance(local, node.Position * BaseScale);
                if (d < reach && d < best) { best = d; hit = node; }
            }

            if (hit == null) { _selectedId = null; RefreshAll(); return; }

            // First click selects; a second click on the same node commits the purchase.
            if (_selectedId == hit.Id)
            {
                // The owned Rebis: a second click swaps which ability the release fires, rather
                // than being a dead click on something already bought.
                if (hit.Kind == NodeKind.Rebis && _board.IsUnlocked(_viewing, hit.Id))
                {
                    if (_board.ToggleSecondAbility(_viewing))
                        Debug.Log($"[Mastery] {_viewing}: ability -> " +
                                  (_board.UsesSecondAbility(_viewing) ? "second" : "first"));
                }
                else if (_board.TryUnlock(_viewing, hit, out var reason))
                    Debug.Log($"[Mastery] {_viewing}: unlocked {hit.Id}");
                else
                    Debug.Log($"[Mastery] {_viewing}: {hit.Id} refused - {reason}");
            }
            _selectedId = hit.Id;
            RefreshAll();
        }
    }
}
