using System.Collections.Generic;
using UnityEngine;
using Convergence.Chain;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// THE WORN ACCESSORY (GearItem.Accessory - the bandolier, the harness, the collar, the banner, the
    /// hourglass, the tome, the wraps, the stones), drawn LIVE: its pictures come from
    /// <see cref="Accessories.Draw"/> twelve times a second (the mock's rate) into a texture per picture, so
    /// the phials empty with the wearer's drinks, the hourglass runs with the run clock and the cloth moves
    /// with the walk. Two layers carry them, <see cref="RigLayer.Accessory"/> and
    /// <see cref="RigLayer.Accessory2"/>: a piece needs at most two pictures in a view, and each piece puts
    /// them on its own joints (<see cref="Accessories.Placement"/>) at its own ranks
    /// (<see cref="PlaceAccessories"/>).
    /// </summary>
    public partial class PrimitiveCharacterRig
    {
        /// <summary>Pictures per second - the mock's frame rate.</summary>
        const float AccessoryFps = 12f;
        /// <summary>Above this speed (units/s) the pieces move at the walk's rate.</summary>
        const float AccessoryWalkSpeed = 0.6f;
        /// <summary>How fast the wraps stream out on a dash and fall back.</summary>
        const float AccessoryDashRate = 8f;

        AccessoryKind _accessory;
        AccessoryState _accState;
        float _accClock;
        int _accFrame = -1;
        bool _accAway;
        float _accLastSpill = float.NegativeInfinity;
        AccessoryCanvas _accA, _accB;
        List<TexelKit.Layer> _accLayersA, _accLayersB;

        /// <summary>One picture's texture: a region of mock coordinates that only grows, pivoted on the
        /// joint the picture hangs from, so the sprite is made once per growth and only its pixels change.</summary>
        sealed class AccessoryCanvas
        {
            public Texture2D Tex;
            public Sprite Sprite;
            /// <summary>The last frame drew nothing (the sprite keeps the old pixels; show none).</summary>
            public bool Empty;
            Color32[] _buf;
            int _x0, _y0, _w, _h;
            Vector2 _pivot;
            int _hash;

            /// <summary>Paint the grid; true when the sprite was (re)made.</summary>
            public bool Paint(Dictionary<int, Color32> grid, Vector2 pivot)
            {
                int a = int.MaxValue, b = int.MaxValue, c = int.MinValue, d = int.MinValue, hash = 17;
                foreach (var kv in grid)
                {
                    int x = TexelKit.KX(kv.Key), y = TexelKit.KY(kv.Key);
                    if (x < a) a = x; if (y < b) b = y; if (x > c) c = x; if (y > d) d = y;
                    unchecked { hash = hash * 31 + kv.Key * 7 + (kv.Value.r << 16 | kv.Value.g << 8 | kv.Value.b); }
                }
                bool remade = false;
                if (grid.Count > 0 && (Tex == null || pivot != _pivot || a < _x0 || b < _y0 || c >= _x0 + _w || d >= _y0 + _h))
                {
                    // grow to hold this frame and what was there, with room to move; widths even
                    const int Pad = 6;
                    int x0 = a - Pad, y0 = b - Pad, x1 = c + Pad, y1 = d + Pad;
                    if (Tex != null && pivot == _pivot)
                    {
                        x0 = Mathf.Min(x0, _x0); y0 = Mathf.Min(y0, _y0);
                        x1 = Mathf.Max(x1, _x0 + _w - 1); y1 = Mathf.Max(y1, _y0 + _h - 1);
                    }
                    int w = x1 - x0 + 1, h = y1 - y0 + 1;
                    w += w % 2; h += h % 2;
                    Release();
                    _x0 = x0; _y0 = y0; _w = w; _h = h; _pivot = pivot;
                    Tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
                    };
                    _buf = new Color32[w * h];
                    // the pivot in the sprite's own pixels, from its bottom-left (texel EDGE coordinates - bake.js)
                    var pv = new Vector2((pivot.x - x0) / w, (y0 + h - pivot.y) / h);
                    Sprite = Sprite.Create(Tex, new Rect(0, 0, w, h), pv, Proportions.BodyPpu, 0, SpriteMeshType.FullRect);
                    Sprite.hideFlags = HideFlags.DontSave;
                    Sprite.name = "accessory";
                    _hash = 0;
                    remade = true;
                }
                if (Tex == null || (!remade && hash == _hash)) return remade;
                _hash = hash;
                System.Array.Clear(_buf, 0, _buf.Length);
                foreach (var kv in grid)
                {
                    int x = TexelKit.KX(kv.Key) - _x0, y = (_y0 + _h - 1) - TexelKit.KY(kv.Key);
                    if (x >= 0 && x < _w && y >= 0 && y < _h) _buf[y * _w + x] = kv.Value;
                }
                Tex.SetPixels32(_buf);
                Tex.Apply(false);
                return remade;
            }

            public void Release()
            {
                Kill(Sprite);
                Kill(Tex);
                Sprite = null; Tex = null;

                static void Kill(Object o)
                {
                    if (o == null) return;
                    if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
                }
            }
        }

        /// <summary>Apply's part: read the worn accessory, hang its two layers on their joints and draw it
        /// once. After every item has painted (the two layers belong to no other slot).</summary>
        void ApplyAccessory(Loadout loadout)
        {
            var item = loadout != null ? GearCatalog.Get(loadout.Get(GearSlot.Ring)) : null;
            var kind = item != null ? item.Accessory : AccessoryKind.None;
            // an arm piece draws itself on the near arm's layers (PrimitiveCharacterRig.ArmPiece.cs)
            bool armPiece = ArmPieces.Is(kind);
            if (armPiece) kind = AccessoryKind.None;
            _accessory = kind;
            _accState ??= new AccessoryState();
            _accState.Look = item != null ? item.AccessoryLook : null;
            if (kind == AccessoryKind.None) ShowAccessoryGlows(null);
            var relic = loadout != null ? GearCatalog.Get(loadout.Get(GearSlot.Relic)) : null;
            _accState.RelicSheath = relic != null && relic.IsSheath;
            _accFrame = -1;
            ApplyArmPiece(armPiece ? item.Accessory : AccessoryKind.None);
            if (kind == AccessoryKind.None) return;

            var (ja, jb, _, _) = Accessories.Placement(kind, _facingAway);
            Hang(RigLayer.Accessory, ja);
            Hang(RigLayer.Accessory2, jb);
            DrawAccessory(force: true);

            void Hang(RigLayer layer, Accessories.Joint joint)
            {
                if (!_layers.TryGetValue(layer, out var sr) || sr == null) return;
                var parent = joint switch
                {
                    Accessories.Joint.NearForearm => _elbowFront,
                    Accessories.Joint.FarForearm  => _elbowBack,
                    _                             => _torso,
                };
                if (parent != null && sr.transform.parent != parent) sr.transform.SetParent(parent, false);
                // a forearm picture is placed as the rig places a cut glove: shoulder-relative, less the elbow
                sr.transform.localPosition = joint == Accessories.Joint.Torso ? Vector3.zero : -(Vector3)ElbowArtRest;
                sr.transform.localRotation = Quaternion.identity;
                sr.transform.localScale = Vector3.one;
                sr.color = Color.white;
                sr.drawMode = SpriteDrawMode.Simple;
                sr.flipX = false;
            }
        }

        /// <summary>The frame's part: read the wearer's state and redraw at the mock's rate.</summary>
        void AnimateAccessory(float dt, float speed)
        {
            if (_accessory == AccessoryKind.None) return;
            _accClock += dt;
            if (_accClock > 3600f) _accClock -= 3600f;   // Sin on an hour-old float loses its texels
            DrawAccessory(force: false, speed: speed, dt: dt);
        }

        void DrawAccessory(bool force, float speed = 0f, float dt = 0f)
        {
            var st = _accState ??= new AccessoryState();
            st.T = _accClock;
            st.Walk = speed > AccessoryWalkSpeed;
            st.Element = _element;
            var pc = Player;
            st.Dash = Mathf.MoveTowards(st.Dash, pc != null && pc.Dashing ? 1f : 0f, dt * AccessoryDashRate);

            // the phial: its charges, the drink in progress, a spill (the charge was taken as the drink began)
            var phial = pc != null ? pc.Phial : null;
            st.MaxCharges = phial != null ? Mathf.Min(phial.MaxCharges, st.Cracked.Length) : 3;
            st.Charges = phial != null ? Mathf.Clamp(phial.Charges, 0, st.MaxCharges) : st.MaxCharges;
            st.Anim = 0;
            st.AnimIdx = st.Charges;
            if (phial != null && phial.Drinking)
            {
                st.Anim = 1;
                st.P = phial.DrinkT / Core.Tuning.Phial.DrinkSeconds;
            }
            else if (phial != null && Time.time - phial.SpilledAt < Core.Tuning.Phial.DrinkSeconds * SpillShown)
            {
                if (phial.SpilledAt != _accLastSpill)
                {
                    _accLastSpill = phial.SpilledAt;
                    if (st.AnimIdx < st.Cracked.Length) st.Cracked[st.AnimIdx] = true;
                }
                st.Anim = 2;
                // the spill picks up at its crack, the moment of the blow
                st.P = SpillCrackAt + (Time.time - phial.SpilledAt) / Core.Tuning.Phial.DrinkSeconds;
            }
            // a cracked glass is whole again once its charge is back
            for (int i = 0; i < st.Cracked.Length; i++) if (i < st.Charges) st.Cracked[i] = false;

            var clock = Core.RunClock.Current;
            st.Run = clock != null ? clock.Seconds / Core.Tuning.RunClock.CollapseAtSeconds : 0f;

            int frame = Mathf.FloorToInt(_accClock * AccessoryFps);
            bool turned = _accAway != _facingAway;
            if (!force && !turned && frame == _accFrame) return;
            _accFrame = frame;
            if (turned)
            {
                _accAway = _facingAway;
                ApplyLayerOrder();   // the pieces rank differently turned away
            }

            _accLayersA ??= new List<TexelKit.Layer>();
            _accLayersB ??= new List<TexelKit.Layer>();
            _accGlows ??= new List<AccessoryGlow>();
            Accessories.Draw(_accessory, st, _facingAway, _accLayersA, _accLayersB, _accGlows);
            ShowAccessoryGlows(_accGlows);
            var (ja, jb, _, _) = Accessories.Placement(_accessory, _facingAway);
            Show(RigLayer.Accessory, ref _accA, _accLayersA, ja);
            Show(RigLayer.Accessory2, ref _accB, _accLayersB, jb);

            void Show(RigLayer layer, ref AccessoryCanvas canvas, List<TexelKit.Layer> layers, Accessories.Joint joint)
            {
                if (!_layers.TryGetValue(layer, out var sr) || sr == null) return;
                canvas ??= new AccessoryCanvas();
                // the flash and the stone swap the sprite for a copy while they last: leave it be
                if (sr.sprite != null && sr.sprite != canvas.Sprite) return;
                var pivot = joint switch
                {
                    Accessories.Joint.NearForearm => Accessories.NearShoulder,
                    Accessories.Joint.FarForearm  => Accessories.FarShoulder,
                    _                             => Accessories.TorsoPivot,
                };
                var grid = TexelKit.Compose(layers);
                canvas.Paint(grid, pivot);
                sr.sprite = canvas.Sprite;
                sr.enabled = canvas.Sprite != null && grid.Count > 0;
            }
        }

        List<AccessoryGlow> _accGlows;
        SpriteRenderer[] _accGlowSr;

        /// <summary>The glows past the whole stack (one order above the vials' three, inside DepthStride):
        /// the mock lays its light over everything. Soft pools of Spr.Glow, a ring as Spr.HaloRing, sized
        /// to the mock's radius and faded by its alpha. Found by name after a reload (EnsureQuiver's way).</summary>
        void ShowAccessoryGlows(List<AccessoryGlow> glows)
        {
            int want = glows?.Count ?? 0;
            if (want == 0 && _accGlowSr == null) return;
            const int Max = 4;
            if (_accGlowSr == null || _accGlowSr.Length != Max) _accGlowSr = new SpriteRenderer[Max];
            for (int i = 0; i < Max; i++)
            {
                var sr = _accGlowSr[i];
                if (sr == null && _torso != null && i < want)
                {
                    string name = "accessory.glow" + i;
                    var t = FindDeep(transform, name);   // a light may hang off an arm's layer
                    if (t == null) { t = new GameObject(name).transform; t.SetParent(_torso, false); }
                    sr = t.GetComponent<SpriteRenderer>();
                    if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
                    _accGlowSr[i] = sr;
                }
                if (sr == null) continue;
                if (i >= want) { sr.enabled = false; continue; }
                var g = glows[i];
                sr.sprite = g.Ring ? Core.Spr.HaloRing : Core.Spr.Glow;
                sr.sortingOrder = BaseSortingOrder + RigLayers.All.Length + 3;
                sr.color = new Color(g.C.r / 255f, g.C.g / 255f, g.C.b / 255f, g.A);
                // what the light rides (an arm piece's shoulder, upper arm or forearm, else the torso), and
                // mock coordinates (texel centres, y down) to that picture's space
                var (parent, pivot) = g.Joint switch
                {
                    1 => (LayerTransform(RigLayer.Shoulders), ArmPieces.Cap),
                    2 => (LayerTransform(RigLayer.GlovesFront), Accessories.NearShoulder),
                    3 => (LayerTransform(RigLayer.GlovesFrontLower), Accessories.NearShoulder),
                    _ => (_torso, Accessories.TorsoPivot),
                };
                if (parent == null) parent = _torso;
                if (sr.transform.parent != parent) sr.transform.SetParent(parent, false);
                sr.transform.localRotation = Quaternion.identity;
                sr.transform.localPosition = new Vector3((g.At.x + 0.5f - pivot.x) / Proportions.BodyPpu,
                                                         (pivot.y - g.At.y - 0.5f) / Proportions.BodyPpu, 0f);
                var native = sr.sprite.bounds.size;
                float d = 2f * g.R / Proportions.BodyPpu;
                sr.transform.localScale = new Vector3(d / native.x, d / native.y, 1f);
                sr.enabled = true;
            }
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform c in root) { var f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }

        Transform LayerTransform(RigLayer layer) => _layers.TryGetValue(layer, out var sr) && sr != null ? sr.transform : null;

        /// <summary>A spill is shown from its crack (0.2 of the drink's beat) for this share of the beat.</summary>
        const float SpillCrackAt = 0.18f, SpillShown = 0.8f;

        /// <summary>
        /// Rank the two accessory layers for the worn piece and the view - after every other rule has
        /// shaped the stack, so each lands against the layer it is defined by wherever that one went.
        /// </summary>
        int[] PlaceAccessories(int[] table)
        {
            Accessories.Rank ra, rb;
            if (_armPiece != AccessoryKind.None)
            {
                // an arm piece's torso picture: a strap under the arm, embers over it; its risers turned away
                // stand over the cape (facing the camera they are drawn into the shoulder's own picture)
                ra = ArmPieces.TorsoOver(_armPiece) ? Accessories.Rank.Over : Accessories.Rank.Under;
                rb = Accessories.Rank.Over;
            }
            else if (_accessory == AccessoryKind.None) return table;
            else (_, _, ra, rb) = Accessories.Placement(_accessory, _facingAway);
            table = Ranked(table, RigLayer.Accessory, ra);
            table = Ranked(table, RigLayer.Accessory2, rb);
            return table;

            int[] Ranked(int[] t, RigLayer layer, Accessories.Rank rank)
            {
                var ranked = new List<RigLayer>();
                for (int i = 0; i < t.Length; i++) ranked.Add((RigLayer)i);
                ranked.Sort((a, b) => t[(int)a].CompareTo(t[(int)b]));
                ranked.Remove(layer);
                int at = rank switch
                {
                    // facing the camera, under the relic (which is over the belt); turned away, over the belt
                    Accessories.Rank.Under    => _facingAway ? ranked.IndexOf(RigLayer.Belt) + 1 : ranked.IndexOf(RigLayer.Trinket),
                    // facing the camera, over the head, the helm's lower edge and the hood (the mock's 14.2-14.6,
                    // over everything but the hands and what they hold); turned away, over the cape and its
                    // drape, under the head
                    Accessories.Rank.Over     => _facingAway ? ranked.IndexOf(RigLayer.BackOver) + 1
                                                             : Mathf.Max(ranked.IndexOf(RigLayer.HeadArmor), ranked.IndexOf(RigLayer.Hood)) + 1,
                    Accessories.Rank.Behind   => ranked.IndexOf(RigLayer.TrinketBack) + 1,
                    Accessories.Rank.Top      => ranked.Count,
                    Accessories.Rank.Bottom   => 0,
                    Accessories.Rank.NearHand => ranked.IndexOf(RigLayer.GlovesFrontHand) + 1,
                    Accessories.Rank.FarHand  => ranked.IndexOf(RigLayer.GlovesBackHand) + 1,
                    _                         => ranked.Count,
                };
                ranked.Insert(Mathf.Clamp(at, 0, ranked.Count), layer);
                var order = new int[t.Length];
                for (int i = 0; i < ranked.Count; i++) order[(int)ranked[i]] = i;
                return order;
            }
        }

        /// <summary>The two accessory layers, appended to a table written before they existed (every
        /// table's outermost wrapper calls this - see TrinketBehindBody). Their real ranks are set per
        /// piece by <see cref="PlaceAccessories"/>; here they only have to be present.</summary>
        static int[] WithAccessoryLayers(int[] table)
        {
            table = Inserted(table, RigLayer.Accessory, RigLayer.Back, above: true);
            return Inserted(table, RigLayer.Accessory2, RigLayer.Back, above: true);
        }
    }
}
