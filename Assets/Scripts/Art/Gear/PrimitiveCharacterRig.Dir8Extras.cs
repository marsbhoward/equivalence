using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    // ------------------------------------------------------------- eight directions: the extras (DEV)
    //
    // RIG.md "Scarves, accessories, arm pieces" (the user's approved 3D mocks, 2026-11-11): what the eight-direction
    // body wears beyond the Bronze armour. A flowing scarf (GearItem.HasFlowingScarf): its collar baked, its tail drawn
    // live (Body8.ScarfTail); an accessory (the Ring slot): its baked pieces in the item's colours, the wraps' wrap as a
    // forearm strip with live tails, the orbiting stones as three sprites of their own sorted by depth; an arm piece:
    // its cloud on the drawn arm, which it replaces with the near pauldron. Only behind DevEightDir (Body8On).
    public partial class PrimitiveCharacterRig
    {
        Body8.Extras _extras8;
        float _scarf8Clock, _stones8Clock;
        SpriteRenderer[] _stones8;
        Vector3[] _stone8At;
        AccessoryKind _acc8Kind;
        string _acc8Look;
        float _acc8Push, _live8Clock;
        Body8.Live _live8;
        Color[] _stone8Tones;
        string _back8Key;
        float[] _back8;

        /// <summary>The standing scarf's drift: one loop in this long (the mock's GIF), in this many pictures.</summary>
        const float ScarfStandSeconds = 0.96f;
        const int ScarfStandFrames = 12;

        static string BaseId8(string id) { int at = id == null ? -1 : id.IndexOf('@'); return at < 0 ? id : id.Substring(0, at); }

        /// <summary>The part of Worn8's key the extras add: what is worn at the neck and as the accessory, the element
        /// (the phials' liquid, Element wraps) and whether the relic is a sheath (the tome is not drawn then).</summary>
        string ExtrasKey8(Chain.Loadout lo)
        {
            if (lo == null) return "";
            var relic = GearCatalog.Get(lo.Get(GearSlot.Relic));
            return $"{lo.Get(GearSlot.Neck)},{lo.Get(GearSlot.Ring)},{_element},{(relic != null && relic.IsSheath ? "s" : "")}";
        }

        /// <summary>Worn8's part: add the extras' pieces and strips (resolved to the item's colours) and build the live ones.</summary>
        void WornExtras8(Chain.Loadout lo)
        {
            var ex = new Body8.Extras();
            bool any = false;

            // ---- a flowing scarf: the collar (baked) and the tail (live), in the scarf's cloth and its Forge dye
            string neckId = lo.Get(GearSlot.Neck);
            var neck = GearCatalog.Get(BaseId8(neckId));
            if (neck != null && neck.HasFlowingScarf && neck.DyeChannels is { Length: > 0 } ch && ch[0].Tones is { Length: 6 } tones)
            {
                var pal = new Dictionary<char, Color>();
                for (int i = 0; i < 6; i++) pal["ksdblh"[i]] = tones[i];
                var dyed = Dyed8(neckId, pal) ?? pal;
                var cloth = new Color[6];
                for (int i = 0; i < 6; i++) cloth[i] = dyed["ksdblh"[i]];
                foreach (var p in Body8Art.Of("scarf", GearSlot.Neck)) _worn8.Add(p.Resolved(r => r == "scarf" ? cloth : null));
                ex.ScarfTones = cloth;
                any = true;
            }

            // ---- the accessory
            var item = GearCatalog.Get(BaseId8(lo.Get(GearSlot.Ring)));
            var kind = item != null ? item.Accessory : AccessoryKind.None;
            _acc8Kind = kind;
            _acc8Look = item?.AccessoryLook;
            _acc8Push = 0f;
            if (kind != AccessoryKind.None)
            {
                string look = item.AccessoryLook;
                Color[] Role(string r) => Accessories.Ramp8(kind, r, look, _element);
                var arm = ArmPieces8.For(kind);
                if (arm != null)
                {
                    // the arm piece replaces the near arm, its glove and its pauldron (arms.js stripNearArm; Body8.Draw)
                    ex.Arm = arm;
                    if (kind == AccessoryKind.CrucibleArm) _worn8.AddRange(Body8Art.Of("arm_crucible", GearSlot.Ring));
                    any = true;
                }
                else if (kind == AccessoryKind.OrbitingStones)
                {
                    _stone8Tones = Role("stone");
                }
                else
                {
                    var relic = GearCatalog.Get(lo.Get(GearSlot.Relic));
                    bool sheathed = kind == AccessoryKind.ChainedTome && relic != null && relic.IsSheath;   // as the flat rig
                    string set = "acc_" + kind + (kind == AccessoryKind.FeatherCollar && (look == "Peacock" || look == "Phoenix") ? "_" + look : "");
                    // the hourglass hangs on a pack's back face (the Athanor pack), not the cape it was baked against
                    float push = kind == AccessoryKind.HourglassPack ? Weapon8.PackDepth(Body8Art.SetOf(lo.Get(GearSlot.Back))) : 0f;
                    _acc8Push = push;
                    if (!sheathed)
                        foreach (var p in Body8Art.Of(set, GearSlot.Ring)) _worn8.Add(p.Resolved(Role).Pushed(push));
                    foreach (var st in Body8Art.StripsOf(set, GearSlot.Ring)) _worn8Strips.Add(st.Resolved(Role));
                    if (kind == AccessoryKind.WristWraps) { ex.WrapTones = Role("pal"); any = true; }
                    if (LiveKind8(kind) && !sheathed) any = true;   // their moving pieces are drawn live (Body8.Live.cs)
                }
            }

            if (ex.ScarfTones != null)
            {
                // the wearer's back, which the tail is kept clear of (cached per what is worn)
                if (_back8 == null || _back8Key != _worn8Ids) { _back8 = Body8.BackTable(_worn8); _back8Key = _worn8Ids; }
                ex.Back = _back8;
                // under a pack the tail is tucked away (it would come out of the pack's face); the collar stays
                if (Body8.ScarfUnderPack(_back8)) ex.ScarfTones = null;
            }
            _extras8 = any ? ex : null;
        }

        // ---- Penumbra's echoes (ArmPieces.PenumbraEcho): the arm where it was 0.12 and 0.24 s ago, from the pictures drawn

        struct ArmPast8 { public float T; public Body8.Frame[] Arm; public Vector2Int Drift; }
        System.Collections.Generic.List<ArmPast8> _armPast8;

        /// <summary>Hide8's part: the arm as this frame's final picture has it.</summary>
        void RecordArm8()
        {
            if (_acc8Kind != AccessoryKind.PenumbraArm || Body8.LastArm == null || _extras8 == null) { _armPast8?.Clear(); return; }
            _armPast8 ??= new System.Collections.Generic.List<ArmPast8>();
            float now = Time.time, keep = ArmPieces.PenumbraEcho[ArmPieces.PenumbraEcho.Length - 1].dt + 0.1f;
            _armPast8.Add(new ArmPast8 { T = now, Arm = Body8.LastArm, Drift = _extras8.ArmDrift });
            while (_armPast8.Count > 2 && _armPast8[1].T < now - keep) _armPast8.RemoveAt(0);
        }

        /// <summary>The echoes for this picture, or none when the arm has stayed where it is (then the picture caches).</summary>
        void Echoes8()
        {
            _extras8.Echoes = null;
            if (_armPast8 == null || _armPast8.Count < 2) return;
            var echoes = ArmPieces.PenumbraEcho;
            var now = _armPast8[_armPast8.Count - 1];
            float t = Time.time;
            Body8.Frame[][] arms = null;
            for (int e = 0; e < echoes.Length; e++)
            {
                var pose = _armPast8[0];
                for (int h = _armPast8.Count - 1; h >= 0; h--) if (_armPast8[h].T <= t - echoes[e].dt) { pose = _armPast8[h]; break; }
                if (Moved8(pose.Arm, now.Arm) < 0.75f && pose.Drift == now.Drift) continue;
                arms ??= new Body8.Frame[echoes.Length][];
                _extras8.EchoDrift ??= new Vector2Int[echoes.Length];
                _extras8.EchoAlpha ??= new float[echoes.Length];
                arms[e] = pose.Arm; _extras8.EchoDrift[e] = pose.Drift; _extras8.EchoAlpha[e] = echoes[e].alpha;
            }
            _extras8.Echoes = arms;
        }

        /// <summary>How far apart two poses of the arm are (texels): the most any joint or the hand moved.</summary>
        static float Moved8(Body8.Frame[] a, Body8.Frame[] b)
        {
            if (a == null || b == null) return 0f;
            float m = 0f;
            for (int i = 0; i < 3; i++) m = Mathf.Max(m, (a[i].J - b[i].J).magnitude);
            return Mathf.Max(m, (a[2].J + a[2].D * 15.3f * a[2].Scale - (b[2].J + b[2].D * 15.3f * b[2].Scale)).magnitude);
        }

        static bool LiveKind8(AccessoryKind k) => k is AccessoryKind.Bandolier or AccessoryKind.VialHarness or AccessoryKind.HourglassPack or AccessoryKind.BannerPole;

        /// <summary>A moving accessory's own loop standing (the mock's speeds: the banner's ripple, the wraps' wobble, the
        /// flask's slosh), in this many pictures; moving, each runs once a stride on the stride's phase.</summary>
        const int LoopFrames8 = 16;
        /// <summary>The wraps' flutter on a dash, in this many pictures a beat.</summary>
        const int WrapFlutterFrames8 = 6;
        static float LoopSeconds8(AccessoryKind k) => k switch
        {
            AccessoryKind.BannerPole => 2f * Mathf.PI / 3.4f,
            AccessoryKind.WristWraps => 2f * Mathf.PI / 3.2f,
            AccessoryKind.VialHarness => 2f * Mathf.PI / 2.4f,
            _ => 0f,
        };

        static float Ease8(float p) => p < 0f ? 0f : p > 1f ? 1f : p * p * (3f - 2f * p);

        /// <summary>The picture's key part for the live extras this frame - the standing scarf's drift, a moving
        /// accessory's loop, the phials, the flask, the sand - with their state set on the extras.</summary>
        string ExtrasFrame8(float dt, float size, float phase)
        {
            if (_extras8 == null) return "";
            var key = new System.Text.StringBuilder();
            float loop = LoopSeconds8(_acc8Kind);
            // a moving accessory's loop: standing, its own clock; moving, the stride (the clock's angle, radians)
            float angle = 0f;
            if (loop > 0f)
            {
                _live8Clock = Mathf.Repeat(_live8Clock + dt, loop);
                if (size > 0f) angle = phase * Mathf.PI * 2f;
                else
                {
                    int k = Mathf.FloorToInt(_live8Clock / loop * LoopFrames8) % LoopFrames8;
                    angle = k * Mathf.PI * 2f / LoopFrames8;
                    key.Append(".l").Append(k);
                }
            }
            if (_extras8.ScarfTones != null)
            {
                _scarf8Clock = Mathf.Repeat(_scarf8Clock + dt, ScarfStandSeconds);
                if (size <= 0f)
                {
                    // with a loop of its own going, the scarf rides that one's pictures (one picture a loop frame)
                    float u = loop > 0f ? Mathf.Repeat(angle / (Mathf.PI * 2f) * loop / ScarfStandSeconds, 1f) : _scarf8Clock / ScarfStandSeconds;
                    int k = Mathf.FloorToInt(u * ScarfStandFrames) % ScarfStandFrames;
                    _extras8.ScarfStand = k / (float)ScarfStandFrames;
                    if (loop <= 0f) key.Append(".s").Append(k);
                }
            }
            // Fulmen's bolts re-strike (12 a second standing, with the stride moving); Penumbra drifts and trails echoes
            if (_acc8Kind == AccessoryKind.FulmenArm)
            {
                int strike = (size > 0f ? Mathf.FloorToInt(phase * 24f) : Mathf.FloorToInt(_armClock * 12f)) % ArmPieces8.FulmenStrikes;
                _extras8.Arm = ArmPieces8.For(_acc8Kind, strike) ?? _extras8.Arm;
                key.Append(".z").Append(strike);
            }
            if (_acc8Kind == AccessoryKind.PenumbraArm)
            {
                var d = ArmPieces.Drift(_armClock);
                _extras8.ArmDrift = new Vector2Int(Mathf.RoundToInt(d.x), Mathf.RoundToInt(d.y));
                key.Append(".d").Append(_extras8.ArmDrift.x).Append(',').Append(_extras8.ArmDrift.y);
                Echoes8();
            }
            // the wraps' wobble: the mock's t * 3.2 is the angle
            _extras8.WrapT = _acc8Kind == AccessoryKind.WristWraps ? angle / 3.2f : 0.4f;
            // on a dash they stream out behind the travel (to an eighth of a turn), fluttering in 6 pictures
            _extras8.WrapDash = 0f;
            if (_acc8Kind == AccessoryKind.WristWraps && _accState != null && _accState.Dash > 0f)
            {
                float dash = Mathf.Round(_accState.Dash * 4f) / 4f;
                int oct = Mathf.RoundToInt(Mathf.Atan2(_travelDir.y, _travelDir.x) / (Mathf.PI / 4f)) & 7;
                int flut = Mathf.FloorToInt(_accClock * 14f / (Mathf.PI * 2f) * WrapFlutterFrames8) % WrapFlutterFrames8;
                if (dash > 0f)
                {
                    // the drawn frame: x the screen's (turned when mirrored), z toward the camera (down the screen)
                    float a = oct * Mathf.PI / 4f;
                    _extras8.WrapDash = dash;
                    _extras8.WrapBack = -new Vector3(Mathf.Cos(a) * (_b8Mirrored ? -1f : 1f), 0f, -Mathf.Sin(a)).normalized;
                    _extras8.WrapFlutter = flut * Mathf.PI * 2f / WrapFlutterFrames8;
                    key.Append(".w").Append(dash).Append(',').Append(oct).Append(',').Append(flut);
                }
            }
            _extras8.Live = LiveState8(size, phase, angle, key);
            return key.ToString();
        }

        /// <summary>The live pieces' state from the flat rig's (Accessories' AccessoryState: the phial's charges, the drink,
        /// a spill, the cracks, the run clock), as BandolierFront / HarnessFront / Hourglass / BannerFlag read it.</summary>
        Body8.Live LiveState8(float size, float phase, float angle, System.Text.StringBuilder key)
        {
            var st = _accState;
            if (!LiveKind8(_acc8Kind) || st == null) return null;
            var lv = _live8 ??= new Body8.Live();
            lv.Kind = _acc8Kind;
            lv.Liq = Accessories.Ramp8(_acc8Kind, "liq", _acc8Look, _element);
            lv.Cloth = Accessories.Ramp8(_acc8Kind, "pal", _acc8Look, _element);
            lv.Spill = -1f; lv.SpillIdx = -1; lv.FlaskDrink = -1f;
            float p = st.P;
            switch (_acc8Kind)
            {
                case AccessoryKind.Bandolier:
                    lv.Phials ??= new Body8.Phial8[3];
                    key.Append(".b");
                    for (int i = 0; i < lv.Phials.Length; i++)
                    {
                        bool active = st.Anim != 0 && st.AnimIdx == i;
                        float level = i < st.Charges ? 1f : 0f;
                        var ph = new Body8.Phial8 { Cork = i < st.Charges, Cracked = i < st.Cracked.Length && st.Cracked[i] };
                        ph.Sway = size > 0f ? Mathf.RoundToInt(Mathf.Sin(angle + i * 1.9f) * 1.2f) : 0;
                        if (active)
                        {
                            level = st.Anim == 1 ? 1f - Ease8((p - 0.15f) / 0.3f) : 1f - Ease8((p - 0.2f) / 0.15f);
                            if (st.Anim == 1)
                            {
                                ph.CorkDy = -Mathf.RoundToInt(Ease8(p / 0.35f) * 10f); ph.CorkDx = Mathf.RoundToInt(Ease8(p / 0.35f) * 4f); ph.Cork = p < 0.55f;
                            }
                            else
                            {
                                ph.Cracked = p > 0.2f; ph.Cork = true;
                                lv.Spill = p; lv.SpillIdx = i;
                            }
                        }
                        ph.Rows = Mathf.RoundToInt(level * 5f);
                        lv.Phials[i] = ph;
                        key.Append(ph.Rows).Append(ph.Cork ? 'c' : 'o').Append(ph.CorkDy).Append(',').Append(ph.CorkDx).Append(ph.Cracked ? 'x' : '-').Append(ph.Sway);
                    }
                    if (lv.Spill >= 0f) key.Append(".sp").Append(Mathf.RoundToInt(lv.Spill * 40f));
                    break;
                case AccessoryKind.VialHarness:
                {
                    bool active = st.Anim != 0, drink = st.Anim == 1;
                    float max = Mathf.Max(1, st.MaxCharges);
                    float level = st.Charges / max;
                    if (active) level = (st.Charges + 1 - (drink ? Ease8((p - 0.15f) / 0.3f) : Ease8((p - 0.2f) / 0.15f))) / max;
                    bool walk = size > 0f;
                    lv.FlaskLevel = Mathf.Round(Mathf.Clamp01(level) * 16f) / 16f;
                    lv.FlaskTilt = Mathf.Sin(angle) * (walk ? 0.32f : 0.14f) + (active && !drink ? 0.4f : 0f);
                    lv.FlaskCork = active && drink ? Mathf.RoundToInt(Mathf.Sin(Mathf.PI * Mathf.Min(1f, p / 0.6f)) * 4f) : 0f;
                    bool cracked = false;
                    for (int i = 0; i < st.MaxCharges && i < st.Cracked.Length; i++) cracked |= st.Cracked[i];
                    lv.FlaskCracked = cracked || (active && !drink && p > 0.2f);
                    if (active && drink) lv.FlaskDrink = p;
                    if (active && !drink) lv.Spill = p;
                    key.Append(".f").Append(Mathf.RoundToInt(lv.FlaskLevel * 16f)).Append(lv.FlaskCracked ? 'x' : '-').Append(lv.FlaskCork);
                    if (active) key.Append('a').Append(Mathf.RoundToInt(p * 40f));
                    break;
                }
                case AccessoryKind.HourglassPack:
                    // the sand: 11 texels from full to run out, two steps a texel
                    lv.Sand = Mathf.Round(Mathf.Clamp01(st.Run) * 22f) / 22f;
                    lv.Push = _acc8Push;
                    key.Append(".h").Append(Mathf.RoundToInt(lv.Sand * 22f));
                    break;
                case AccessoryKind.BannerPole:
                    lv.Ripple = angle / 3.4f;   // the mock's t * 3.4 is the angle
                    break;
            }
            return lv;
        }

        /// <summary>The picture's overlay (what of an accessory lies in front of the head) above the head, at rest.</summary>
        void ShowRestOver8(Sprite over)
        {
            if (over == null)
            {
                if (_over8 != null && _swing8Over == null) { _over8.sprite = null; _over8.enabled = false; }
                return;
            }
            if (_over8 == null)
            {
                var go = new GameObject("swing8over");
                go.transform.SetParent(transform, false);
                _over8 = go.AddComponent<SpriteRenderer>();
            }
            _over8.transform.localPosition = _legs8.transform.localPosition;
            _over8.flipX = _legs8.flipX;
            _over8.sprite = over;
            _over8.sortingLayerID = _legs8.sortingLayerID;
            _over8.enabled = true;   // its order is set in Hide8, above the head's layers
        }

        bool IsStone8(SpriteRenderer sr)
        {
            if (_stones8 != null) foreach (var s in _stones8) if (s == sr) return true;
            if (_glows8 != null) foreach (var s in _glows8) if (s == sr) return true;
            return false;
        }

        // ---- the lights (the flat rig's AccessoryGlow, which Hide8 hides with the rest): placed on the drawn figure

        SpriteRenderer[] _glows8;
        readonly struct Light8
        {
            public readonly Vector3 P; public readonly float R, A; public readonly Color32 C; public readonly bool Ring, Behind;
            /// <param name="behind">Shown behind the body too (a stone's light, dimmed already, as the flat rig has it).</param>
            public Light8(Vector3 p, float r, Color32 c, float a, bool ring, bool behind = false) { P = p; R = r; C = c; A = a; Ring = ring; Behind = behind; }
        }
        System.Collections.Generic.List<Light8> _lights8;

        /// <summary>A light this far behind the body's middle (texels) is hidden by it, as the mock's would be.</summary>
        const float LightBehind = -5f;

        /// <summary>Hide8's part, after the picture (rest or swing) is final: each light where the drawn figure has its
        /// source - an arm piece's on its posed arm, a stone's on the stone, a drink's on the chest - over the head's
        /// layers like the flat rig's (<paramref name="top"/>), unless the body is in front of it.</summary>
        void PlaceGlows8(int top)
        {
            _lights8 ??= new System.Collections.Generic.List<Light8>();
            _lights8.Clear();
            var arm = Body8.LastArm;
            if (_extras8?.Arm != null && arm != null && _armGlows8 != null)
                foreach (var g in _armGlows8) if (g.Joint >= 1) _lights8.Add(new Light8(Body8.ArmLight(arm, g.Joint, g.At), g.R, g.C, g.A, g.Ring));
            if (_acc8Kind == AccessoryKind.OrbitingStones)
            {
                if (_stones8 != null && _stone8At != null)
                    for (int i = 0; i < _stones8.Length; i++)
                        if (_stones8[i] != null && _stones8[i].enabled)
                            _lights8.Add(new Light8(_stone8At[i], 12f, Accessories.StoneLight, _stone8At[i].z > 0f ? 0.42f : 0.18f, false, behind: true));
            }
            else if (_acc8Kind != AccessoryKind.None && _extras8?.Arm == null && _accGlows != null)
                // a drink's swell over the phial and its heal ring: the flat front art's coordinates on the chest
                foreach (var g in _accGlows)
                    if (g.Joint == 0)
                        _lights8.Add(new Light8(Body8.BodyPoint(_b8Pose, _b8Mirrored, new Vector3(g.At.x + 0.5f - 19f, 135f - g.At.y - 0.5f, 14f)), g.R, g.C, g.A, g.Ring));

            int n = 0;
            float sx = _b8Flip ? -1f : 1f;
            foreach (var l in _lights8)
            {
                if (!l.Behind && l.P.z < LightBehind) continue;
                if (_glows8 == null || _glows8.Length <= n)
                {
                    var grown = new SpriteRenderer[n + 4];
                    if (_glows8 != null) System.Array.Copy(_glows8, grown, _glows8.Length);
                    _glows8 = grown;
                }
                var sr = _glows8[n];
                if (sr == null)
                {
                    var go = new GameObject("glow8_" + n);
                    go.transform.SetParent(transform, false);
                    sr = _glows8[n] = go.AddComponent<SpriteRenderer>();
                }
                sr.sprite = l.Ring ? Core.Spr.HaloRing : Core.Spr.Glow;
                sr.color = new Color(l.C.r / 255f, l.C.g / 255f, l.C.b / 255f, l.A);
                sr.transform.localPosition = new Vector3(sx * l.P.x / bodyPpu, _b8Sole + (l.P.y - l.P.z * Locomotion8.DepthY) / bodyPpu, 0f);
                var native = sr.sprite.bounds.size;
                float d = 2f * l.R / bodyPpu;
                sr.transform.localScale = new Vector3(d / native.x, d / native.y, 1f);
                sr.sortingLayerID = _legs8.sortingLayerID;
                sr.sortingOrder = l.P.z < 0f ? _legs8.sortingOrder - 1 : top;   // behind the body: under the figure
                sr.enabled = true;
                n++;
            }
            if (_glows8 != null) for (int i = n; i < _glows8.Length; i++) if (_glows8[i] != null) _glows8[i].enabled = false;
        }



        void HideExtras8()
        {
            if (_stones8 != null) foreach (var s in _stones8) if (s != null) s.enabled = false;
            if (_glows8 != null) foreach (var s in _glows8) if (s != null) s.enabled = false;
            if (_over8 != null && _swing8Over == null) { _over8.sprite = null; _over8.enabled = false; }
        }

        /// <summary>The orbiting stones: three, a level circle round the waist (the approved honest orbit), each turning
        /// on its axis - sprites of their own, in front of the drawn body or behind it by their depth.</summary>
        void PlaceStones8(float dt, Locomotion8.Pose pose, bool mirrored, bool flip, float sole)
        {
            bool on = _acc8Kind == AccessoryKind.OrbitingStones && _stone8Tones != null;
            if (!on)
            {
                if (_stones8 != null) foreach (var s in _stones8) if (s != null) s.enabled = false;
                return;
            }
            _stones8Clock += dt;
            if (_stones8Clock > 3600f) _stones8Clock -= 3600f;
            float t = _stones8Clock;
            if (_stones8 == null || _stones8.Length != 3 || _stones8[0] == null)
            {
                _stones8 = new SpriteRenderer[3];
                for (int i = 0; i < 3; i++)
                {
                    var go = new GameObject("stone8_" + i);
                    go.transform.SetParent(transform, false);
                    _stones8[i] = go.AddComponent<SpriteRenderer>();
                }
            }
            for (int i = 0; i < 3; i++)
            {
                float a = t * 0.95f + i * Mathf.PI * 2f / 3f, spin = t * 2.2f + i;
                var c = new Vector3(Mathf.Cos(a) * Body8.StoneOrbit, Body8.StoneY + Mathf.Sin(t * 2f + i * 2f) * 1.5f, Mathf.Sin(a) * Body8.StoneOrbit);
                var p = Body8.BodyPoint(pose, mirrored, c);
                (_stone8At ??= new Vector3[3])[i] = p;
                // the stone's own turn in the picture: its local x axis (the body's frame, turned by the spin) drawn
                var e1 = Body8.BodyPoint(pose, mirrored, c + new Vector3(Mathf.Cos(spin), 0f, Mathf.Sin(spin))) - p;
                float th = Mathf.Repeat(Mathf.Atan2(e1.z, e1.x), Mathf.PI * 0.5f);
                int bin = Mathf.FloorToInt(th / (Mathf.PI * 0.5f) * Body8.StoneBins);
                var sr = _stones8[i];
                sr.sprite = Body8.StoneSprite(bin, _stone8Tones, bodyPpu);
                sr.flipX = flip;
                float x = p.x * (flip ? -1f : 1f);
                sr.transform.localPosition = new Vector3(x / bodyPpu, sole + (p.y - p.z * Locomotion8.DepthY) / bodyPpu, 0f);
                sr.sortingLayerID = _legs8.sortingLayerID;
                sr.sortingOrder = _legs8.sortingOrder + (p.z > 0f ? 1 : -1);
                sr.enabled = true;
            }
        }
    }
}
