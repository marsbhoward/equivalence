using System.Collections.Generic;
using UnityEngine;
using Convergence.Chain;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// THE ARM PIECE (GearItem.Accessory of an arm kind - <see cref="ArmPieces"/>): an accessory that replaces
    /// the whole near arm, drawn LIVE like the other accessories. It takes over the near arm's own layers, so
    /// every stack the rig already builds (the carry, hands up, turned away, the forearm over the shoulders)
    /// orders it exactly as it orders an arm:
    ///   GlovesFront / GlovesFrontLower / GlovesFrontHand - the arm, cut at the elbow and wrist as the rig cuts
    ///       every arm (two texels of overlap; the forearm and hand with the elbow stretch taken out);
    ///   Shoulders - the shoulder piece. Unlike a pauldron (FollowPauldron: a quarter of the carry, else still)
    ///       it is the arm's own shoulder: its centre rides the arm's axis and it turns with
    ///       <see cref="ArmPieces.ShoulderShare"/> of the arm's angle, every frame (FollowArmPiece);
    ///   Accessory - what rides the torso (a strap, embers); Accessory2 - turned away, what stands above the
    ///       shoulder, over the cape.
    /// The body's near arm (ArmFront, ArmFrontLower, ArmFrontHand) is hidden while it is worn and put back
    /// when it comes off. The afterimage (Penumbra) is see-through and trails copies of its parts.
    /// </summary>
    public partial class PrimitiveCharacterRig
    {
        AccessoryKind _armPiece;
        ArmPieceState _armState;
        float _armClock, _armDrawnT;
        int _armFrame = -1;
        bool _armAway;
        List<TexelKit.Layer> _armLayers;
        List<AccessoryGlow> _armGlows, _armGlows8;
        AccessoryCanvas _armUpper, _armLower, _armHand, _armShoulder, _armTorso, _armRise;
        /// <summary>The body's own near-arm sprites, put back when the piece comes off. Non-readonly, rebuilt
        /// lazily (domain reload).</summary>
        Dictionary<RigLayer, Sprite> _armBodySaved;

        static readonly RigLayer[] ArmBodyLayers = { RigLayer.ArmFront, RigLayer.ArmFrontLower, RigLayer.ArmFrontHand };
        /// <summary>The piece's parts, in <see cref="ArmPieceCanvas"/> order.</summary>
        static readonly RigLayer[] ArmPartLayers = { RigLayer.GlovesFront, RigLayer.GlovesFrontLower, RigLayer.GlovesFrontHand, RigLayer.Shoulders };

        /// <summary>Rows of the (stretched) arm picture each part keeps: the elbow cut at the joint, the wrist at
        /// the rig's WristArtTexels (+ the stretch), two texels of overlap each side.</summary>
        const int ArmCutOverlap = 2;
        static int WristRow => 30 + Proportions.WristArtTexels + Accessories.ElbowExtra;

        AccessoryCanvas ArmPieceCanvas(int part) => part switch { 0 => _armUpper, 1 => _armLower, 2 => _armHand, _ => _armShoulder };

        /// <summary>Apply's part (from ApplyAccessory): put the piece on, or take it off.</summary>
        void ApplyArmPiece(AccessoryKind kind)
        {
            var was = _armPiece;
            _armPiece = kind;
            if (kind == AccessoryKind.None)
            {
                if (was != AccessoryKind.None) RestoreArm();
                return;
            }
            foreach (var layer in new[] { RigLayer.Accessory, RigLayer.Accessory2 })
            {
                if (!_layers.TryGetValue(layer, out var sr) || sr == null) continue;
                if (_torso != null && sr.transform.parent != _torso) sr.transform.SetParent(_torso, false);
                sr.transform.localPosition = Vector3.zero;
                sr.transform.localRotation = Quaternion.identity;
                sr.transform.localScale = Vector3.one;
                sr.drawMode = SpriteDrawMode.Simple;
                sr.flipX = false;
            }
            _armAway = _facingAway;
            _armFrame = -1;
            DrawArmPiece(force: true, speed: 0f);
            FollowArmPiece();
        }

        void RestoreArm()
        {
            if (_armBodySaved != null)
                foreach (var kv in _armBodySaved)
                    if (_layers.TryGetValue(kv.Key, out var sr) && sr != null && UnderSwap(kv.Key, sr) == null)
                        SetUnderSwap(kv.Key, sr, kv.Value);
            _armBodySaved?.Clear();
            // Apply has already cleared and repainted every gear layer; only what this moved by hand is left
            foreach (var layer in new[] { RigLayer.Shoulders, RigLayer.Accessory, RigLayer.Accessory2 })
                if (_layers.TryGetValue(layer, out var sr) && sr != null) { sr.transform.localRotation = Quaternion.identity; sr.color = Color.white; }
            foreach (var layer in ArmPartLayers)
                if (_layers.TryGetValue(layer, out var sr) && sr != null) sr.color = Color.white;
            ShowArmEchoes(false);
            ShowAccessoryGlows(null);
        }

        /// <summary>The frame's part: the clock, the redraw at the mock's rate, and the near arm kept hidden
        /// (anything that repaints the body or the gloves puts them back).</summary>
        void AnimateArmPiece(float dt, float speed)
        {
            if (_armPiece == AccessoryKind.None) return;
            _armClock += dt;
            if (_armClock > 3600f) _armClock -= 3600f;
            DrawArmPiece(force: false, speed: speed);
            HoldArmPiece();
        }

        void DrawArmPiece(bool force, float speed)
        {
            int frame = Mathf.FloorToInt(_armClock * AccessoryFps);
            bool turned = _armAway != _facingAway;
            if (!force && !turned && frame == _armFrame) return;
            _armFrame = frame;
            if (turned)
            {
                _armAway = _facingAway;
                ApplyLayerOrder();   // the risers rank differently turned away
            }

            var st = _armState ??= new ArmPieceState();
            st.T = _armClock;
            st.Walk = speed > AccessoryWalkSpeed;
            st.Element = _element;
            st.Sp = ShoulderPoint();
            _armDrawnT = _armClock;
            _armLayers ??= new List<TexelKit.Layer>();
            _armGlows ??= new List<AccessoryGlow>();
            ArmPieces.Draw(_armPiece, st, _armLayers, _armGlows);

            var arm = new List<TexelKit.Layer>();
            var shoulder = new List<TexelKit.Layer>();
            var rise = new List<TexelKit.Layer>();
            var torso = new List<TexelKit.Layer>();
            foreach (var l in _armLayers)
            {
                if (l.Joint == ArmPieces.JArm) arm.Add(l);
                else if (l.Joint == ArmPieces.JTorso) torso.Add(l);
                // facing the camera the risers stack among the shoulder's own layers, as the mock draws them;
                // turned away they leave it for the layer over the cape
                else if (_facingAway && l.Rise) rise.Add(l);
                else shoulder.Add(l);
            }

            // the arm composed WHOLE, then cut: an outline drawn at a cut would show as a black seam
            var grid = TexelKit.Compose(arm);
            int elbow = ArmPieces.ElbowRow, wrist = WristRow, stretch = Accessories.ElbowExtra;
            var upper = new Dictionary<int, Color32>();
            var lower = new Dictionary<int, Color32>();
            var hand = new Dictionary<int, Color32>();
            foreach (var kv in grid)
            {
                int x = TexelKit.KX(kv.Key), y = TexelKit.KY(kv.Key);
                if (y < elbow + ArmCutOverlap) upper[kv.Key] = kv.Value;
                if (y >= elbow - ArmCutOverlap && y < wrist + ArmCutOverlap) lower[TexelKit.K(x, y - stretch)] = kv.Value;
                if (y >= wrist - ArmCutOverlap) hand[TexelKit.K(x, y - stretch)] = kv.Value;
            }
            Paint(RigLayer.GlovesFront, ref _armUpper, upper, Accessories.NearShoulder);
            Paint(RigLayer.GlovesFrontLower, ref _armLower, lower, Accessories.NearShoulder);
            Paint(RigLayer.GlovesFrontHand, ref _armHand, hand, Accessories.NearShoulder);
            Paint(RigLayer.Shoulders, ref _armShoulder, TexelKit.Compose(shoulder), ArmPieces.Cap);
            Paint(RigLayer.Accessory, ref _armTorso, TexelKit.Compose(torso), Accessories.TorsoPivot);
            Paint(RigLayer.Accessory2, ref _armRise, TexelKit.Compose(rise), ArmPieces.Cap);
            // the eight-direction body places every light on its own drawn arm (Dir8Extras, PlaceGlows8)
            _armGlows8 ??= new List<AccessoryGlow>();
            _armGlows8.Clear(); _armGlows8.AddRange(_armGlows);
            // turned away the arm is under the cape: only a light above the shoulders still shows
            if (_facingAway) _armGlows.RemoveAll(g => g.Joint != 1 || g.At.y >= 29f);
            ShowAccessoryGlows(_armGlows);

            static void Paint(RigLayer layer, ref AccessoryCanvas canvas, Dictionary<int, Color32> g, Vector2 pivot)
            {
                canvas ??= new AccessoryCanvas();
                if (g.Count > 0) canvas.Paint(g, pivot);
                canvas.Empty = g.Count == 0;
            }
            HoldArmPiece(force: true);
        }

        /// <summary>Keep the piece's pictures on its layers and the body's near arm off - cheap when nothing
        /// changed. A layer someone else repainted (a glove recut, the body redrawn) is taken back and placed.</summary>
        void HoldArmPiece(bool force = false)
        {
            _armBodySaved ??= new Dictionary<RigLayer, Sprite>();
            foreach (var layer in ArmBodyLayers)
            {
                if (!_layers.TryGetValue(layer, out var sr) || sr == null) continue;
                var own = UnderSwap(layer, sr);
                if (own == null) continue;
                _armBodySaved[layer] = own;
                SetUnderSwap(layer, sr, null);
            }
            // a catalogue pauldron whose near plate sits on ShouldersBack (rest on the near side, -X)
            if (_shouldersBackRest.x < 0f && _layers.TryGetValue(RigLayer.ShouldersBack, out var sb) && sb != null && sb.enabled)
                sb.enabled = false;

            float alpha = ArmPieces.Alpha(_armPiece);
            for (int i = 0; i < ArmPartLayers.Length; i++) Hold(ArmPartLayers[i], ArmPieceCanvas(i), i < 3, i == 0 ? Vector3.zero : i < 3 ? -(Vector3)ElbowArtRest : (Vector3?)null);
            Hold(RigLayer.Accessory, _armTorso, false, Vector3.zero);
            Hold(RigLayer.Accessory2, _armRise, false, null);

            void Hold(RigLayer layer, AccessoryCanvas canvas, bool glove, Vector3? at)
            {
                if (!_layers.TryGetValue(layer, out var sr) || sr == null || canvas == null) return;
                var want = canvas.Empty ? null : canvas.Sprite;
                bool mine = UnderSwap(layer, sr) == want;
                if (!mine) SetUnderSwap(layer, sr, want);
                sr.enabled = want != null;
                var c = new Color(1f, 1f, 1f, alpha);
                if (sr.color != c) sr.color = c;
                if (mine && !force) return;
                sr.drawMode = SpriteDrawMode.Simple;
                sr.flipX = false;
                if (at.HasValue)
                {
                    sr.transform.localPosition = at.Value;
                    sr.transform.localRotation = Quaternion.identity;
                    sr.transform.localScale = Vector3.one;
                }
            }
        }

        // ---------------------------------------------------------------- the shoulder that turns

        /// <summary>The arm's angle off its rest, torso-local degrees (counter-clockwise).</summary>
        float ArmPieceAngle() => _armFront != null ? Mathf.DeltaAngle(0f, _armFront.localEulerAngles.z) : 0f;

        /// <summary>The shoulder piece's centre now, torso-local: carried round the live joint with the arm.</summary>
        Vector3 ArmPieceCapLocal(float angle)
        {
            var joint = _armFront != null ? _armFront.localPosition : Vector3.zero;
            var off = new Vector3((ArmPieces.Cap.x - Accessories.NearShoulder.x) / Proportions.BodyPpu,
                                  (Accessories.NearShoulder.y - ArmPieces.Cap.y) / Proportions.BodyPpu, 0f);
            return joint + Quaternion.Euler(0f, 0f, angle) * off;
        }

        /// <summary>Where a point drawn on the shoulder piece is now, in the picture's coordinates - what the
        /// embers rise from (the mock's shoulderPoint).</summary>
        System.Func<float, float, Vector2> ShoulderPoint()
        {
            float angle = ArmPieceAngle();
            var cap = ArmPieceCapLocal(angle);
            var capNow = new Vector2(Accessories.TorsoPivot.x + cap.x * Proportions.BodyPpu, Accessories.TorsoPivot.y - cap.y * Proportions.BodyPpu);
            // the picture's y runs down, so its turn is the other way round
            float a = -angle * ArmPieces.ShoulderShare * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            var c0 = ArmPieces.Cap;
            return (x, y) =>
            {
                float dx = x - c0.x, dy = y - c0.y;
                return new Vector2(capNow.x + dx * c - dy * s, capNow.y + dx * s + dy * c);
            };
        }

        /// <summary>LateUpdate's part, after the arm's pose is final: the shoulder piece (and turned away its
        /// risers) onto the arm, and the afterimage's trail.</summary>
        void FollowArmPiece()
        {
            if (_armPiece == AccessoryKind.None || _torso == null) return;
            float angle = ArmPieceAngle();
            var cap = ArmPieceCapLocal(angle);
            var turn = Quaternion.Euler(0f, 0f, angle * ArmPieces.ShoulderShare);
            foreach (var layer in new[] { RigLayer.Shoulders, RigLayer.Accessory2 })
            {
                if (!_layers.TryGetValue(layer, out var sr) || sr == null) continue;
                if (sr.transform.parent != _torso) sr.transform.SetParent(_torso, false);
                sr.transform.localPosition = new Vector3(cap.x, cap.y, sr.transform.localPosition.z);
                sr.transform.localRotation = turn;
                sr.transform.localScale = Vector3.one;
            }
            TrailArmEchoes();
        }

        // ---------------------------------------------------------------- the afterimage's trail

        struct ArmPose { public float T; public Vector3 P; public Quaternion R; public Vector3 S; }
        List<ArmPose>[] _armHistory;
        SpriteRenderer[] _armEchoSr;

        /// <summary>Each part's copies, drawn where the part was <see cref="ArmPieces.PenumbraEcho"/> seconds
        /// ago (torso-local, so a walk does not smear them), fainter, a hair behind it; offset by the arm's own
        /// drift then, so even a still arm shows them.</summary>
        void TrailArmEchoes()
        {
            if (_armPiece != AccessoryKind.PenumbraArm) { ShowArmEchoes(false); return; }
            var echoes = ArmPieces.PenumbraEcho;
            int parts = ArmPartLayers.Length;
            _armHistory ??= new List<ArmPose>[parts];
            if (_armEchoSr == null || _armEchoSr.Length != parts * echoes.Length) _armEchoSr = new SpriteRenderer[parts * echoes.Length];
            float now = Time.time, keep = echoes[echoes.Length - 1].dt + 0.1f;
            var torsoInv = Quaternion.Inverse(_torso.rotation);
            float alpha = ArmPieces.Alpha(_armPiece);
            for (int i = 0; i < parts; i++)
            {
                if (!_layers.TryGetValue(ArmPartLayers[i], out var src) || src == null) continue;
                var hist = _armHistory[i] ??= new List<ArmPose>();
                var t = src.transform;
                var ls = t.lossyScale; var ts = _torso.lossyScale;
                hist.Add(new ArmPose { T = now, P = _torso.InverseTransformPoint(t.position), R = torsoInv * t.rotation,
                                       S = new Vector3(ls.x / ts.x, ls.y / ts.y, 1f) });
                while (hist.Count > 2 && hist[1].T < now - keep) hist.RemoveAt(0);
                for (int e = 0; e < echoes.Length; e++)
                {
                    var sr = EchoRenderer(i * echoes.Length + e);
                    if (sr == null) continue;
                    var pose = hist[0];
                    for (int h = hist.Count - 1; h >= 0; h--) if (hist[h].T <= now - echoes[e].dt) { pose = hist[h]; break; }
                    // the canvas carries the drift of when it was drawn; the copy shows the drift of its own moment
                    var d = ArmPieces.Drift(_armDrawnT - echoes[e].dt) - ArmPieces.Drift(_armDrawnT);
                    if (i == 3) d *= 0.5f;
                    var off = pose.R * new Vector3(d.x / Proportions.BodyPpu, -d.y / Proportions.BodyPpu, 0f);
                    sr.transform.localPosition = pose.P + off + new Vector3(0f, 0f, 0.01f * (e + 1));   // behind, on a tie
                    sr.transform.localRotation = pose.R;
                    sr.transform.localScale = pose.S;
                    sr.sprite = src.sprite;
                    sr.sortingOrder = src.sortingOrder;
                    sr.color = new Color(1f, 1f, 1f, alpha * echoes[e].alpha);
                    sr.enabled = src.enabled && src.sprite != null;
                }
            }
        }

        SpriteRenderer EchoRenderer(int i)
        {
            var sr = _armEchoSr[i];
            if (sr != null || _torso == null) return sr;
            string name = "armpiece.echo" + i;
            var t = _torso.Find(name);
            if (t == null) { t = new GameObject(name).transform; t.SetParent(_torso, false); }
            sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
            return _armEchoSr[i] = sr;
        }

        void ShowArmEchoes(bool on)
        {
            if (_armEchoSr == null) return;
            foreach (var sr in _armEchoSr) if (sr != null) sr.enabled = on && sr.enabled;
            if (!on && _armHistory != null) foreach (var h in _armHistory) h?.Clear();
        }
    }
}
