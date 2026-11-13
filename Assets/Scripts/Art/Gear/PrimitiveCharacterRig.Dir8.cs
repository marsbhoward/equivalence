using UnityEngine;

namespace Convergence.Art.Gear
{
    // ------------------------------------------------------------- eight directions, phase 1 (DEV)
    //
    // RIG.md "Eight directions": the character FACES ITS TRAVEL and TURNS TO THE AIM to swing (the bow aims all
    // the time - its legs follow travel, its torso the aim, phase 2), and the head shows the view for that facing:
    // front, front 3/4, side, back 3/4 or back, mirrored for left (BodyLook.HeadDetail5). Phase 1 is behind
    // DevEightDir: the body keeps today's front and back pictures until the skeleton (phase 2) replaces them.
    public partial class PrimitiveCharacterRig
    {
        /// <summary>The eight-direction rig, ON by default since 2026-11-13 (the showcase is Bronze only, the one tier
        /// baked). Off = the four-view rig exactly; the Editor's Convergence > Dev > Eight Directions switches it.
        /// Pieces with no eight-direction bake (Silver and up) draw as the bare suit until they are baked.</summary>
        public static bool DevEightDir = true;

        /// <summary>DEV ONLY: a direction of travel to face as if moving (checking the views without input).</summary>
        public static Vector2? DevTravel;

        /// <summary>How fast the character must move before it turns to face where it is going (world units/s).</summary>
        const float TravelFacingSpeed = 0.25f;

        /// <summary>The last direction of travel, held while standing still.</summary>
        Vector2 _travelDir = Vector2.down;

        BodyLook.HeadView5 _head8View;
        bool _head8Left, _head8Away;
        Sprite _head8Sprite;

        /// <summary>What the turning aim turns toward (FaceAimTurning): the aim while a swing plays or a bow is held,
        /// the direction of travel otherwise.</summary>
        Vector2 EightDirTarget(Vector2 aim)
        {
            if (!DevEightDir) return aim;
            var v = Body != null ? Body.linearVelocity : Vector2.zero;
            if (DevTravel.HasValue) _travelDir = DevTravel.Value.normalized;
            else if (v.sqrMagnitude > TravelFacingSpeed * TravelFacingSpeed) _travelDir = v.normalized;
            if (HeldBow) return aim;
            if (_attackTimer > 0f && _attackHold <= 0f) return aim;
            // A held LOCK keeps the body on its target (the user's call, 2026-11-12), moving or not.
            if (Player != null && Player.Targeting != null && Player.Targeting.IsLocked) return aim;
            return _travelDir;
        }

        /// <summary>The head's view for a facing angle (degrees, 0 = right, up the screen = away from the camera).</summary>
        static BodyLook.HeadView5 HeadViewFor(float angle)
        {
            float a = Mathf.Repeat(angle + 180f, 360f) - 180f, abs = Mathf.Abs(a);
            if (a >= 0f)
                return abs < 22.5f || abs > 157.5f ? BodyLook.HeadView5.Side
                     : abs < 67.5f || abs > 112.5f ? BodyLook.HeadView5.BackQuarter
                     : BodyLook.HeadView5.Back;
            return abs < 22.5f || abs > 157.5f ? BodyLook.HeadView5.Side
                 : abs < 67.5f || abs > 112.5f ? BodyLook.HeadView5.FrontQuarter
                 : BodyLook.HeadView5.Front;
        }

        /// <summary>Called after FaceAim: repaints the head when its view, mirror or the body's view changes.</summary>
        void SyncHeadView8()
        {
            if (!DevEightDir || !_aimViews || _look == null)
            {
                if (_head8Sprite != null)
                {
                    _head8Sprite = null;
                    RepaintHead(BodyLook.SkinRamp(_look.Skin), BodyLook.HairRamp(_look.Hair), BodyLook.EyeRamp(_look.Eyes), BodyLook.BrowRamp(_look));
                    DrawHeadBack(BodyLook.SkinRamp(_look.Skin), BodyLook.HairRamp(_look.Hair));
                    DrawMane(BodyLook.HairRamp(_look.Hair));
                }
                return;
            }
            var view = HeadViewFor(_aimAngle);
            bool same = _head8Sprite != null && view == _head8View && _facingLeft == _head8Left && _facingAway == _head8Away
                        && _layers.TryGetValue(RigLayer.Head, out var h) && h != null && h.sprite == _head8Sprite;
            if (same) return;
            _head8View = view; _head8Left = _facingLeft; _head8Away = _facingAway;
            PaintHead8(view, _facingLeft);
        }

        void PaintHead8(BodyLook.HeadView5 view, bool left)
        {
            int scale = _detailArt ? BodyLook.DetailScale : 1;
            float ppu = bodyPpu * scale;
            string style = BodyLook.HairStyle(_look.HairStyle).Key;
            var skin = BodyLook.SkinRamp(_look.Skin); var hair = BodyLook.HairRamp(_look.Hair);
            var pal = BodyLook.HeadPalette(skin, hair, BodyLook.EyeRamp(_look.Eyes), BodyLook.BrowRamp(_look), BodyLook.BeardRamp(_look));

            var head = BodyLook.HeadDetail5(view, left, _look.Expression, style, _look.Beard, _look.Brows, _look.Hair, _look.Mark,
                                            scale, view == BodyLook.HeadView5.Front ? 0 : GazeShift);
            var hv = BodyLook.View5(style, view, left);
            string[] mane = hv?.Mane == null ? null : BodyLook.ManeFromRows(hv.Mane, hv.ManeTop, _look.Hair, scale);
            bool over = view == BodyLook.HeadView5.Back || view == BodyLook.HeadView5.BackQuarter;
            string key = $"dir8.{view}.{(left ? "L" : "R")}.{BodyLook.DetailKey(_look, false)}.x{scale}";

            // Seen from behind the hair past the skull lies OVER the head: drawn into the head's own sprite. From the
            // front and the side it hangs behind the body, on its own layer, as today's mane does.
            float shift = 0f;
            if (over && mane != null)
            {
                head = Composite(head, mane, (32 - hv.ManeTop) * scale, out float headCentreRow, out float centreRow);
                shift = Proportions.Cells((headCentreRow - centreRow) / scale);
                key += ".m";
            }
            var sprite = PixelSprite.Cached(key) ?? PixelSprite.From(key, head, pal, pixelsPerUnit: ppu);
            var headOnlyAnchor = ChinOnNeckline(PixelSprite.Cached(key + ".h") ?? PixelSprite.From(key + ".h",
                BodyLook.HeadDetail5(view, left, _look.Expression, style, _look.Beard, _look.Brows, _look.Hair, _look.Mark,
                                     scale, view == BodyLook.HeadView5.Front ? 0 : GazeShift), pal, pixelsPerUnit: ppu));
            var at = new Vector2(0, headOnlyAnchor + shift);
            _head8Centre = headOnlyAnchor;
            SetBodyPixel(RigLayer.Head, sprite, at);
            SetBodyPixel(RigLayer.HeadBack, sprite, at);
            SetLayerEnabled(RigLayer.Head, !_facingAway);
            SetLayerEnabled(RigLayer.HeadBack, _facingAway);
            _head8Sprite = sprite;

            if (_layers.TryGetValue(RigLayer.HairBack, out var hb) && hb != null)
            {
                if (over || mane == null || (_headSealed && _helmOn)) { hb.enabled = false; hb.sprite = null; }
                else
                {
                    string mkey = $"dir8.mane.{view}.{(left ? "L" : "R")}.{style}.{_look.Hair}.x{scale}";
                    var ms = PixelSprite.Cached(mkey) ?? PixelSprite.From(mkey, mane, Palette.Of(hair, hair), pixelsPerUnit: ppu);
                    SetBodyPixel(RigLayer.HairBack, ms, new Vector2(0, Proportions.Cells(hv.ManeTop - hv.Mane.Length * 0.5f)));
                }
            }
        }

        /// <summary>The mane laid over the head (rows offset <paramref name="maneRow"/> from the head's top, centred),
        /// on a canvas big enough for both; reports where the head's centre and the canvas's centre fall (rows).</summary>
        static string[] Composite(string[] head, string[] mane, int maneRow, out float headCentreRow, out float centreRow)
        {
            int hw = head[0].Length, hh = head.Length, mw = mane[0].Length, mh = mane.Length;
            int w = Mathf.Max(hw, mw), top = Mathf.Min(0, maneRow), bottom = Mathf.Max(hh, maneRow + mh), h = bottom - top;
            var g = new char[h][];
            for (int y = 0; y < h; y++) { g[y] = new char[w]; for (int x = 0; x < w; x++) g[y][x] = '.'; }
            void Lay(string[] src, int oy, int ox)
            {
                for (int y = 0; y < src.Length; y++)
                for (int x = 0; x < src[y].Length; x++)
                    if (src[y][x] != '.') g[y + oy][x + ox] = src[y][x];
            }
            Lay(head, -top, (w - hw) / 2);
            Lay(mane, maneRow - top, (w - mw) / 2);
            headCentreRow = -top + hh * 0.5f; centreRow = h * 0.5f;
            var rows = new string[h];
            for (int y = 0; y < h; y++) rows[y] = new string(g[y]);
            return rows;
        }
    
        // ------------------------------------------------------------- phases 2 and 3: the drawn body
        //
        // Locomotion8 poses the legs as a 3D skeleton (phase 2) and Body8 draws the whole figure below the head as
        // one picture for the facing (snapped to the eight directions, the mirror doing the left three): the legs
        // and the swinging arms on the skeleton, and every worn Bronze torso piece from its baked view (phase 3),
        // stacked by depth. Walking or running with the speed; the hips sit on the new legs (62 texels), the body
        // rides the gait's bob and leans with it, and the head goes where the neck is. Every other sprite of the
        // rig is hidden while it shows (accessories, arm pieces and scarves are next); the weapon at rest is a card in the
        // picture (phase 3c, Weapon8). The limb gear (3b) is swept along
        // the posed arms and legs from its baked strips.

        SpriteRenderer _legs8, _helm8;
        float _legs8Phase;
        System.Collections.Generic.List<SpriteRenderer> _srScratch;
        System.Collections.Generic.HashSet<SpriteRenderer> _hidden8;
        Chain.Loadout _worn8Loadout;
        string _worn8Ids, _worn8Key;
        System.Collections.Generic.List<Body8Art.Piece> _worn8;
        System.Collections.Generic.List<Body8Art.Strip> _worn8Strips;
        Body8Art.Piece _helm8Piece;

        /// <summary>Above this share of the base move speed the legs run; below it they walk.</summary>
        const float RunFromSpeed = 0.8f;
        /// <summary>One whole stride (two steps), seconds, at the gait's own speed: the mock's 32 and 48 frames.</summary>
        const float RunCycleSeconds = 0.64f, WalkCycleSeconds = 0.96f;
        /// <summary>The neck, texels above the soles (the baked torso's top): the head leans as it does.</summary>
        const float Neck8 = 106f;

        bool Body8On => DevEightDir && _aimViews && !TopDown && _hips != null;

        void PlaceLegs8(float dt, float speed)
        {
            if (!Body8On)
            {
                if (_legs8 != null && _legs8.enabled) { _legs8.enabled = false; if (_helm8) _helm8.enabled = false; HideExtras8(); Unhide8(); }
                return;
            }
            // The legs face the travel for a bow (the torso aims), the body's turn for everything else.
            float angle = HeldBow ? Mathf.Atan2(_travelDir.y, _travelDir.x) * Mathf.Rad2Deg : _aimAngle;
            if (_facingLeft) angle = 180f - angle;                      // drawn into the mirrored frame
            int dir = Mathf.RoundToInt(Mathf.Repeat(angle, 360f) / 45f) % 8;
            bool flip = dir >= 3 && dir <= 5;                           // pointing back across: draw it turned and flip
            if (flip) dir = (12 - dir) % 8;
            int view = dir switch { 0 => Body8Art.Side, 1 => Body8Art.BackQuarter, 2 => Body8Art.Back, 6 => Body8Art.Front, _ => Body8Art.FrontQuarter };
            bool mirrored = _facingLeft != flip;

            float rel = Mathf.Clamp(speed / Core.Tuning.Player.MoveSpeed, 0f, 2.5f);
            var gait = rel > RunFromSpeed ? Locomotion8.Run : Locomotion8.Walk;
            float size = Mathf.Round(Mathf.Clamp01(_gaitWeight) * 4f) / 4f;
            float cycle = gait == Locomotion8.Run ? RunCycleSeconds : WalkCycleSeconds;
            float pace = Mathf.Sqrt(Mathf.Max(0.25f, rel / (gait == Locomotion8.Run ? 1f : 0.5f)));
            if (size > 0f) _legs8Phase = Mathf.Repeat(_legs8Phase + dt / cycle * pace, 1f);
            int frame = size > 0f ? Mathf.FloorToInt(_legs8Phase * gait.Frames) % gait.Frames : 0;
            if (size <= 0f) gait = Locomotion8.Walk;
            float phase = frame / (float)gait.Frames;
            var stance = SwingStance8(dir, flip);
            var pose = Locomotion8.PoseAt(dir * 45f, gait, phase, size, stance);

            Worn8();
            bool fighting8 = _inCombat || Swinging8 || _sinceSwing < CarryReturnDelay;
            _bowReady8 = Mathf.MoveTowards(_bowReady8, fighting8 ? 1f : 0f, dt / BowReadySeconds);
            _b8Pose = pose; _b8Gait = gait; _b8Phase = phase; _b8Size = size; _b8View = view; _b8Mirrored = mirrored;
            _b8Flip = flip; _b8Dir = dir; _b8Sole = HipsRest.y - LegLength;
            var held = Held8();
            string key = $"b8.{_worn8Key}.{gait.Name}.{dir}.{(mirrored ? "m" : "r")}.{frame}.{size:0.00}{(held != null ? "." + held.Key : "")}{StanceKey8(stance)}{ExtrasFrame8(dt, size, phase)}";
            // Penumbra's echoes trailing a moving arm: a picture for this frame alone (its history is not the pose's)
            bool once = _extras8?.Echoes != null;
            var sprite = Body8.Draw(once ? null : key, pose, gait, phase, size, view, mirrored, _worn8, _worn8Strips, Palette.Of(Palette.Undersuit), bodyPpu, held, _extras8);
            var restOver = Body8.LastOver;
            DropRest8();
            if (once) { _rest8Sprite = sprite; _rest8Over = restOver; }

            if (_legs8 == null)
            {
                var go = new GameObject("body8");
                go.transform.SetParent(transform, false);
                _legs8 = go.AddComponent<SpriteRenderer>();
            }
            float sole = HipsRest.y - LegLength;
            _legs8.transform.localPosition = new Vector3(0f, sole, 0f);
            _legs8.transform.localScale = Vector3.one;
            _legs8.sprite = sprite;
            _legs8.flipX = flip;
            _legs8.enabled = true;
            if (_layers.TryGetValue(RigLayer.Torso, out var torsoSr) && torsoSr != null)
            {
                _legs8.sortingLayerID = torsoSr.sortingLayerID;
                _legs8.sortingOrder = torsoSr.sortingOrder;
            }

            // The hips and torso on the new legs, riding the bob; the head where the neck has leaned to.
            float Units(float texels) => PixelSprite.Px(0f, Proportions.Cells(texels)).y;
            float hipsY = sole + Units(pose.HipY);
            _hips.localPosition = new Vector3(HipsRest.x, hipsY, 0f);
            if (_torso) _torso.localPosition = new Vector3(TorsoRest.x, hipsY + TorsoRest.y - HipsRest.y, 0f);
            var lean = Body8.LeanAt(pose, Neck8);
            if (flip) lean.x = -lean.x;
            if (_head) _head.localPosition = new Vector3(HeadRest.x + Units(lean.x), HeadRest.y + Units(lean.y), 0f);
            PlaceHelm8(view, mirrored, flip);
            ShowRestOver8(restOver);
            PlaceStones8(dt, pose, mirrored, flip, sole);
        }

        /// <summary>The swing's body (PlaceBody's channels) on the drawn legs, in whole texels so pictures repeat: the
        /// crouch, the split stance, the lean, and the back foot held where it stood while the figure steps - the rig
        /// moves the whole picture for the step and the reach (drawing only), so the foot goes back by that much.</summary>
        Locomotion8.Stance SwingStance8(int dir, bool flip)
        {
            float legs = 1f - BodyStanceWalkGive * _gaitWeight;
            var st = new Locomotion8.Stance
            {
                Crouch = Mathf.Round(_poseBody.Crouch * bodyPpu),
                Front = Mathf.Round(_poseBody.Stance * legs * bodyPpu),
                Lean = Mathf.Round(-_poseBody.Lean),
            };
            float drawnStep = BodyStepNow + _reach * legs;
            if (Mathf.Abs(drawnStep) > 1e-5f)
            {
                // the picture moves by the step on the screen (LateUpdate's drawn step); the held foot moves back by it,
                // on the ground: x across, and up the screen is away from the camera (z = -up / DepthY)
                var along = StepDirection * drawnStep;
                var world = transform.parent != null ? transform.parent.TransformVector(new Vector3(Mathf.Sign(transform.localScale.x) * along.x * _visualScale, along.y * _visualScale, 0f))
                                                     : new Vector3(along.x, along.y, 0f);
                var l = transform.InverseTransformVector(world);
                float x = (flip ? -1f : 1f) * l.x * bodyPpu, up = l.y * bodyPpu;
                st.Hold = new Vector3(-Mathf.Round(x), 0f, Mathf.Round(up / Locomotion8.DepthY));
            }
            return st;
        }

        static string StanceKey8(Locomotion8.Stance st) =>
            st.Crouch == 0f && st.Front == 0f && st.Lean == 0f && st.Hold == Vector3.zero ? "" : $".w{st.Crouch},{st.Front},{st.Lean},{st.Hold.x},{st.Hold.z}";

        /// <summary>What is worn that has eight-direction pictures: the bare suit's torso, then each Bronze piece.</summary>
        void Worn8()
        {
            var lo = _lastLoadout;
            string ids = "";
            if (lo != null)
                foreach (var slot in new[] { GearSlot.Head, GearSlot.Torso, GearSlot.Shoulders, GearSlot.Belt, GearSlot.Legs, GearSlot.Back, GearSlot.Gloves, GearSlot.Boots })
                    ids += Body8Art.SetOf(lo.Get(slot)) + DyeTag8(lo.Get(slot)) + ",";
            ids += ExtrasKey8(lo);
            if (_worn8 != null && lo == _worn8Loadout && ids == _worn8Ids) return;
            _worn8Loadout = lo; _worn8Ids = ids; _worn8Key = ids.Replace(",", "-");
            _worn8 = new System.Collections.Generic.List<Body8Art.Piece>(Body8Art.Of("bare", GearSlot.Torso));
            _worn8Strips = new System.Collections.Generic.List<Body8Art.Strip>();
            _helm8Piece = null;
            _extras8 = null;
            if (lo == null) return;
            foreach (var slot in new[] { GearSlot.Legs, GearSlot.Boots, GearSlot.Gloves })
            {
                string id = lo.Get(slot), set = Body8Art.SetOf(id);
                if (set == null) continue;
                foreach (var st in Body8Art.StripsOf(set, slot))
                    _worn8Strips.Add(Dyed8(id, st.Palette) is { } pal ? st.Recoloured(pal) : st);
            }
            foreach (var slot in new[] { GearSlot.Torso, GearSlot.Shoulders, GearSlot.Belt, GearSlot.Legs, GearSlot.Back })
            {
                string id = lo.Get(slot), set = Body8Art.SetOf(id);
                if (set == null) continue;
                foreach (var p in Body8Art.Of(set, slot))
                    _worn8.Add(Dyed8(id, p.Palette) is { } pal ? p.Recoloured(pal) : p);
            }
            string helmId = lo.Get(GearSlot.Head), helmSet = Body8Art.SetOf(helmId);
            if (helmSet != null)
                foreach (var p in Body8Art.Of(helmSet, GearSlot.Head))
                    _helm8Piece = Dyed8(helmId, p.Palette) is { } pal ? p.Recoloured(pal) : p;
            WornExtras8(lo);
        }

        /// <summary>A worn piece's dye, for the cache keys: what follows the '@' of a dyed copy's id.</summary>
        static string DyeTag8(string itemId)
        {
            int at = itemId == null ? -1 : itemId.IndexOf('@');
            return at < 0 ? "" : itemId.Substring(at);
        }

        /// <summary>The baked palette in the worn copy's dye (its item's channels, the swatches its id names), or
        /// null when it is undyed or the dye reaches none of these colours.</summary>
        static System.Collections.Generic.Dictionary<char, Color> Dyed8(string itemId, System.Collections.Generic.Dictionary<char, Color> pal)
        {
            var swatches = GearDye.SwatchesOf(itemId);
            if (swatches == null || pal == null) return null;
            var design = GearCatalog.Get(itemId.Substring(0, itemId.IndexOf('@')));
            return design?.DyeChannels is { Length: > 0 } ch ? GearDye.Repainted(pal, ch, swatches) : null;
        }

        // ------------------------------------------------------------- phase 3c: the weapon at rest

        /// <summary>A swing (or its carry's return) is playing: attacks are phase 4, so today's weapon sprite shows and
        /// the drawn arms keep their walk.</summary>
        bool Swinging8 => _attackTimer > 0f || _chargeTimer > 0f || _sinceSwing < CarryReturnDelay;

        Weapon8.Held _held8;
        /// <summary>The bow's ready clock: 0 out of combat (O1) .. 1 at the ready (C1), 0.45 s each way as the discs'.</summary>
        float _bowReady8;
        const float BowReadySeconds = 0.45f;

        /// <summary>The bow's quiver and arrow art as cards (at the size the rig draws them), or null.</summary>
        Weapon8.Art QuiverArt8() => Rounded8(_quiver != null && _quiver.sprite != null ? Weapon8.ArtOf(_quiver.sprite, bodyPpu / _quiver.sprite.pixelsPerUnit) : null);
        Weapon8.Art ArrowArt8() => Rounded8(_arrowSprite != null ? Weapon8.ArtOf(_arrowSprite, bodyPpu / _arrowSprite.pixelsPerUnit) : null);
        static Weapon8.Art Rounded8(Weapon8.Art a) { if (a != null) a.Shape = Weapon8.Form.Round; return a; }
        /// <summary>How a weapon class's card takes its thickness (Weapon8.Depth).</summary>
        static Weapon8.Form FormOf8(WeaponClass c) => c == WeaponClass.Disc ? Weapon8.Form.Disc : c == WeaponClass.Bow ? Weapon8.Form.Bow : Weapon8.Form.Sword;
        Sprite _held8Sprite;
        Weapon8.Kind _held8Kind;

        /// <summary>The weapon drawn into the figure this pose, as a card (Weapon8), or null: none, hidden or swinging.</summary>
        Weapon8.Held Held8()
        {
            if (_weaponHiddenPref || _weaponHides > 0 || Swinging8 || _lastLoadout == null) return null;
            var item = GearCatalog.Get(_lastLoadout.Get(GearSlot.Weapon));
            if (item == null) return null;
            LayerSprite ls = null;
            foreach (var l in item.Layers) if (l != null && l.Layer == RigLayer.Weapon && l.Sprite != null) { ls = l; break; }
            if (ls == null) return null;
            bool bow = item.Class == WeaponClass.Bow;
            var kind = bow ? Weapon8.Kind.Bow
                     : item.Class == WeaponClass.Disc ? (_discDraw <= 0f ? Weapon8.Kind.DiscStowed : _discDraw >= 1f ? Weapon8.Kind.DiscWings : Weapon8.Kind.DiscDraw)
                     : Weapon8.Kind.Sword;
            // eighths of the bow's ease, sixteenths of the discs' draw: each a cached picture
            float ready = bow ? Mathf.Round(_bowReady8 * 8f) / 8f : kind == Weapon8.Kind.DiscDraw ? Mathf.Round(_discDraw * 16f) / 16f : 0f;
            float stowZ = Weapon8.StowDepthFor(Body8Art.SetOf(_lastLoadout.Get(GearSlot.Back)));
            if (_held8 != null && _held8Sprite == ls.Sprite && _held8Kind == kind && _held8.StowZ == stowZ && _held8.Ready == ready
                && (_held8.Quiver != null) == (QuiverArt8() != null) && (_held8.Arrow != null) == (_arrowSprite != null)) return _held8;
            // the art at the size the rig draws it: body texels per texture texel
            float scale = ls.Size.x / Mathf.Max(1f, ls.Sprite.rect.width) * bodyPpu;
            var art = Weapon8.ArtOf(ls.Sprite, Mathf.Round(scale * 4f) / 4f);
            if (art == null) return null;
            art.Shape = FormOf8(item.Class);
            _held8Sprite = ls.Sprite; _held8Kind = kind;
            _held8 = new Weapon8.Held { Art = art, Kind = kind, StowZ = stowZ, Ready = ready, Key = $"{item.ItemId}.{kind}.{stowZ:0.0}.{ready:0.000}" };
            if (bow)
            {
                _held8.Quiver = QuiverArt8(); _held8.Arrow = ArrowArt8();
                _held8.Key += (_held8.Quiver != null ? ".q" : "") + (_held8.Arrow != null ? ".a" : "");   // the picture differs
            }
            return _held8;
        }

        /// <summary>The helm's view over the head, centred where the mock's head is (121 texels up).</summary>
        void PlaceHelm8(int view, bool mirrored, bool flip)
        {
            if (_helm8Piece == null || _helmHidden || _head == null) { if (_helm8) _helm8.enabled = false; return; }
            if (_helm8 == null)
            {
                var go = new GameObject("helm8");
                _helm8 = go.AddComponent<SpriteRenderer>();
            }
            if (_helm8.transform.parent != _head) _helm8.transform.SetParent(_head, false);
            var v = _helm8Piece.For(view, mirrored);
            string key = $"helm8.{_helm8Piece.Set}{DyeTag8(_lastLoadout?.Get(GearSlot.Head))}.{view}.{(mirrored && _helm8Piece.Mirror != null ? "m" : "r")}";
            var sprite = PixelSprite.Cached(key);
            if (sprite == null)
            {
                int w = v.W + (v.W & 1);
                var rows = new string[v.H];
                for (int y = 0; y < v.H; y++) rows[y] = v.Cols.Substring(y * v.W, v.W).PadRight(w, '.');
                sprite = PixelSprite.From(key, rows, _helm8Piece.Palette, pivotTexel: new Vector2Int(-v.X0, Body8Art.Top - HelmCentre8 - v.Y0), pixelsPerUnit: bodyPpu);
            }
            _helm8.sprite = sprite;
            _helm8.flipX = flip;
            _helm8.transform.localPosition = new Vector3(0f, PixelSprite.Px(0f, _head8Centre).y + HelmLift8, 0f);
            _helm8.enabled = true;
            if (_layers.TryGetValue(RigLayer.HeadArmor, out var ha) && ha != null)
            {
                _helm8.sortingLayerID = ha.sortingLayerID;
                _helm8.sortingOrder = ha.sortingOrder;
            }
        }

        /// <summary>The mock's head centre, texels above the soles; and a nudge (units) onto the game's head art.</summary>
        const int HelmCentre8 = 121;
        public static float HelmLift8 = 0f;
        float _head8Centre;

        /// <summary>
        /// The layers were re-ranked (ApplyLayerOrder - the depth sort moving the base as the figure walks, or a turn):
        /// the eight-direction pieces follow NOW - the drawn body onto the torso's rank, the helm and near layer over
        /// the head, the glows and the orbiting stones with the body. They were ranked only in this rig's own update,
        /// a frame after the layers, so every step a walking figure took down the screen put the mane over the body
        /// for a frame: the hair flickered (the showcase, 2026-11-13; worst over a back piece, which the body draws).
        /// </summary>
        void SyncOrders8()
        {
            if (_legs8 == null || !_layers.TryGetValue(RigLayer.Torso, out var torso) || torso == null) return;
            int delta = torso.sortingOrder - _legs8.sortingOrder;
            _legs8.sortingOrder = torso.sortingOrder;
            int headTop = _legs8.sortingOrder;
            foreach (var l in new[] { RigLayer.Head, RigLayer.HeadBack, RigLayer.HeadArmor })
                if (_layers.TryGetValue(l, out var hs) && hs != null) headTop = Mathf.Max(headTop, hs.sortingOrder);
            if (_helm8 != null) _helm8.sortingOrder = headTop + 1;
            if (_over8 != null) _over8.sortingOrder = headTop + 2;
            if (delta == 0) return;
            if (_glows8 != null) foreach (var g in _glows8) if (g != null) g.sortingOrder += delta;
            if (_stones8 != null) foreach (var st in _stones8) if (st != null) st.sortingOrder += delta;
        }

        /// <summary>LateUpdate, last: every sprite of the rig but the head and the drawn body stays hidden.</summary>
        void Hide8()
        {
            if (!Body8On || _legs8 == null || !_legs8.enabled) return;
            // the helm over the head, the swing's near blade over both - set last, after the swing has reordered the
            // head's layers this frame (a swing's table could put the head over the helm: it vanished mid-sweep)
            int headTop = _legs8.sortingOrder;
            foreach (var l in new[] { RigLayer.Head, RigLayer.HeadBack, RigLayer.HeadArmor })
                if (_layers.TryGetValue(l, out var hs) && hs != null) headTop = Mathf.Max(headTop, hs.sortingOrder);
            if (_helm8 != null) _helm8.sortingOrder = headTop + 1;
            if (_over8 != null) _over8.sortingOrder = headTop + 2;
            PlaceGlows8(headTop + 3);
            RecordArm8();
            _hidden8 ??= new System.Collections.Generic.HashSet<SpriteRenderer>();
            _srScratch ??= new System.Collections.Generic.List<SpriteRenderer>();
            bool swinging = Swinging8 && !_swing8Drawn;   // a swing drawn into the figure (phase 4) hides today's weapon too
            if (swinging && _hidden8.Count > 0)
            {
                // the swing takes today's weapon back: what this hid at rest shows again
                bool any = false;
                _srScratch.Clear();
                foreach (var sr in _hidden8) if (sr != null && IsWeapon8(sr)) _srScratch.Add(sr);
                foreach (var sr in _srScratch) { _hidden8.Remove(sr); if (sr.sprite != null) { sr.enabled = true; any = true; } }
                if (any) ApplyWeaponVisibility();
                _srScratch.Clear();
            }
            GetComponentsInChildren(true, _srScratch);
            foreach (var sr in _srScratch)
            {
                if (sr == _legs8 || sr == _helm8 || sr == _over8 || sr == _shadow || !sr.enabled || IsStone8(sr)) continue;
                if (_layers.TryGetValue(RigLayer.Head, out var h) && sr == h) continue;
                if (_layers.TryGetValue(RigLayer.HeadBack, out var hb) && sr == hb) continue;
                if (_layers.TryGetValue(RigLayer.HairBack, out var hr) && sr == hr) continue;
                if (swinging && IsWeapon8(sr)) continue;
                sr.enabled = false;
                _hidden8.Add(sr);
            }
        }

        /// <summary>Today's weapon sprites (and what hangs off them): shown through a swing until phase 4.</summary>
        bool IsWeapon8(SpriteRenderer sr)
        {
            if (sr == _offhand) return true;
            if (_layers.TryGetValue(RigLayer.Weapon, out var w) && w != null && (sr == w || sr.transform.IsChildOf(w.transform))) return true;
            return _offhand != null && sr.transform.IsChildOf(_offhand.transform);
        }

        // ------------------------------------------------------------- phase 4: swings (the user's option D)
        //
        // A swing stays TODAY'S on screen: the rig animates its weapon and hands as it always has, and the eight-direction
        // figure draws them where they are - but each at a depth, so the blade passes in front of the chest and behind the
        // back as the body turns. The depth is the 3D swing the motion would make (the mock's C): overhead cuts in the
        // body's own forward/up plane, everything else level round the body. The blade's angle against the aim, as the
        // picture has it, says where it points in 3D; the grip sits where that swing would hold it. Drawn every frame
        // of the swing in LateUpdate (after the rig has posed it), uncached.

        Locomotion8.Pose _b8Pose;
        Locomotion8.Gait _b8Gait;
        float _b8Phase, _b8Size, _b8Sole;
        int _b8View, _b8Dir;
        bool _b8Mirrored, _b8Flip, _swing8Drawn;
        Sprite _swing8Sprite, _swing8Over;
        SpriteRenderer _over8;

        /// <summary>How far below today's shoulder-height swing a level swing is drawn (texels): the chest.</summary>
        public static float LevelSwingDrop = 14f;

        static bool Overhead8(Combat.AttackMotion m) => m is Combat.AttackMotion.Chop or Combat.AttackMotion.Rise or Combat.AttackMotion.Slam
            or Combat.AttackMotion.Throw or Combat.AttackMotion.Plant or Combat.AttackMotion.WhirlThrow;

        void DrawSwing8()
        {
            _swing8Drawn = false;
            _redraw8 ??= new System.Collections.Generic.List<Redraw8>();
            _redraw8.Clear();
            if (!Body8On || _legs8 == null || !_legs8.enabled || !Swinging8 || _b8Gait == null) { DropSwing8(); return; }
            if (!_layers.TryGetValue(RigLayer.Weapon, out var weapon) || weapon == null) { DropSwing8(); return; }

            var f = _b8Pose.Forward; var r = _b8Pose.Right;
            float alpha = _b8Dir * 45f * Mathf.Deg2Rad;
            var aim = new Vector2(Mathf.Cos(alpha), Mathf.Sin(alpha)); var aimUp = new Vector2(-aim.y, aim.x);
            bool overhead = Overhead8(_motion);
            float sx = _b8Flip ? -1f : 1f;
            // A LEVEL swing (a sweep, a spin, a thrust) is drawn at the chest, where its 3D swing (option C) has it: today's
            // rig swings it from the shoulder, which facing the camera ran the blade across the neck (the user's report).
            // Drawing only - the hit is untouched. Overhead cuts and the bow keep the rig's height.
            float drop = Overhead8(_motion) || IsBowDraw(_motion) ? 0f : LevelSwingDrop;
            _r8Drop = drop; _r8Sx = sx;
            Vector2 Tex(Vector3 world) => Tex8(world);
            Vector2 TexV(Vector3 worldV) { var l = transform.InverseTransformVector(worldV); return new Vector2(sx * l.x * bodyPpu, l.y * bodyPpu); }
            Vector3 At(Vector2 s, float z) => new Vector3(s.x, s.y + z * Locomotion8.DepthY, z);

            var cards = new System.Collections.Generic.List<Weapon8.Card>();
            var hands = new Vector3?[2];
            // the bow (the draw, the loose): held in the LEFT hand (Body8's arm 1), in its own upright plane, the arrows
            // along the shot; everything else in the right hand by the motion's plane
            bool bow = _bowGrip;
            if (IsBowDraw(_motion)) overhead = true;
            int mainHand = bow ? 1 : 0;
            float gripZ = 0f; Vector2 grip = default;
            bool oneOffFirst = _weaponHides == 1 && _offHiddenFirst && _split;
            bool mainShown = !_weaponHiddenPref && (_weaponHides == 0 || oneOffFirst);
            bool offShown = _split && _offhand != null && !_weaponHiddenPref && (_weaponHides == 0 || (_weaponHides == 1 && !_offHiddenFirst));
            float arrowZ = float.NaN;
            foreach (var (sr, role) in new[] { (weapon, 0), (_offhand, 1), (_nocked, 2), (_nocked2, 2) })
            {
                bool shown = role == 0 ? mainShown : role == 1 ? offShown : sr != null && sr.enabled;
                if (sr == null || sr.sprite == null || !shown) continue;
                var art = Weapon8.ArtOf(sr.sprite, 1f);
                if (art == null) { DropSwing8(); return; }     // unreadable art: today's flat weapon instead
                art.Shape = role == 0 ? FormOf8(GearCatalog.Get(_lastLoadout?.Get(GearSlot.Weapon))?.Class ?? (bow ? WeaponClass.Bow : WeaponClass.Greatsword))
                          : role == 1 ? Weapon8.Form.Disc : Weapon8.Form.Round;
                float ppu = sr.sprite.pixelsPerUnit;
                var S = Tex(sr.transform.position);
                var U = TexV(sr.transform.TransformVector(Vector3.right / ppu)) * (sr.flipX ? -1f : 1f);
                var V = -TexV(sr.transform.TransformVector(Vector3.up / ppu)) * (sr.flipY ? -1f : 1f);
                // the piece's long axis on screen: an arrow's art lies along x, a blade's or a bow's up the grid
                bool alongX = art.W > art.H;
                var b = alongX ? U.normalized : (-V).normalized;
                float th = Mathf.Atan2(Vector2.Dot(b, aimUp), Vector2.Dot(b, aim));
                var dir = overhead ? f * Mathf.Cos(th) + Vector3.up * Mathf.Sin(th)
                                   : (f * Mathf.Cos(th) + r * Mathf.Sin(th) - Vector3.up * 0.12f).normalized;
                float side = role == 2 ? 0f : (role == 0) == !bow ? 1f : -1f;
                float z = overhead ? side * 15f * r.z + 14f * dir.z : side * 4f * r.z + 3f * f.z + 16f * dir.z;
                Vector3 U3, V3;
                if (alongX)
                {
                    float uz = dir.z * U.magnitude;
                    U3 = new Vector3(U.x, U.y + uz * Locomotion8.DepthY, uz); V3 = new Vector3(V.x, V.y, 0f);
                }
                else
                {
                    float vz = -dir.z * V.magnitude;
                    U3 = new Vector3(U.x, U.y, 0f); V3 = new Vector3(V.x, V.y + vz * Locomotion8.DepthY, vz);
                }
                // THE BOW STANDS UPRIGHT (the user, 2026-11-11: "default to up and down"): whatever the aim, its length is
                // straight up and down and its belly faces along the facing, so aimed up or down it is seen edge-on
                // rather than lying across the body as today's picture turns it. The grip stays where the rig has it.
                if (bow && role == 0)
                {
                    float belly = Vector2.Dot(U, aim) >= 0f ? 1f : -1f;
                    U3 = f * (belly * U.magnitude);
                    V3 = Vector3.down * V.magnitude;
                }
                // held in a hand: the card goes with it where the drawn arm is shorter than today's (FollowsHand is 1-based);
                // an arrow lies along the bow, so it goes with the bow hand
                int follows = role == 1 ? 1 - mainHand : mainHand;
                cards.Add(new Weapon8.Card { Art = art, O = At(S, z), U = U3, V = V3, FollowsHand = follows + 1 });
                _redraw8.Add(new Redraw8 { Sr = sr, Card = cards.Count - 1, S0 = S, U2 = U, V2 = V });
                if (role == 0) { hands[mainHand] = At(S, z); grip = S; gripZ = z; }
                else if (role == 1) hands[1 - mainHand] = At(S, z);
                else if (float.IsNaN(arrowZ)) arrowZ = z;
            }
            if (cards.Count == 0) { DropSwing8(); return; }
            if (!bow && !_split && _motion == Combat.AttackMotion.Sweep) DiagonalSweep8(cards, hands, f, r);
            // where each held sprite is now drawn (the element pictures follow it, Redrawn8), and its element overlay
            // laid over its card
            for (int i = 0; i < _redraw8.Count; i++)
            {
                var m = _redraw8[i]; var cd = cards[m.Card];
                m.O = cd.O; m.U3 = cd.U; m.V3 = cd.V; _redraw8[i] = m;
                var ovT = m.Sr.transform.Find("element-overlay");
                var ov = ovT != null ? ovT.GetComponent<SpriteRenderer>() : null;
                if (ov == null || ov.sprite == null || ov.sprite.texture == null) continue;
                var ovArt = Weapon8.ArtOf(ov.sprite, 1f, fresh: true);
                if (ovArt != null) cards.Add(new Weapon8.Card { Art = ovArt, O = cd.O, U = cd.U, V = cd.V, FollowsHand = cd.FollowsHand, Blend = true });
            }
            // the other hand where the rig has it: on the grip (a two-handed hold), on the arrow's nock (the draw), or
            // at its own shoulder's depth
            int other = 1 - mainHand;
            var otherLayer = bow ? RigLayer.ArmFrontHand : RigLayer.ArmBackHand;
            if (hands[other] == null && _layers.TryGetValue(otherLayer, out var bh) && bh != null)
            {
                var S2 = Tex(bh.transform.position);
                float z2 = !float.IsNaN(arrowZ) ? arrowZ : (S2 - grip).magnitude < 14f ? gripZ : (bow ? 15f : -15f) * r.z;
                hands[other] = At(S2, z2);
            }

            var held = new Weapon8.Held { Art = cards[0].Art, Kind = Weapon8.Kind.Sword, Live = new Weapon8.Grip { Hand = hands, Cards = cards },
                                          Quiver = bow ? QuiverArt8() : null };
            var sprite = Body8.Draw(null, _b8Pose, _b8Gait, _b8Phase, _b8Size, _b8View, _b8Mirrored, _worn8, _worn8Strips,
                                    Palette.Of(Palette.Undersuit), bodyPpu, held, _extras8);
            DropSwing8();
            _swing8Sprite = sprite;
            _legs8.sprite = sprite;
            _swing8Drawn = true;

            // the blade where it passes before the face, over the head
            _swing8Over = Body8.LastOver; Body8.LastOver = null;
            if (_swing8Over != null)
            {
                if (_over8 == null)
                {
                    var go = new GameObject("swing8over");
                    go.transform.SetParent(transform, false);
                    _over8 = go.AddComponent<SpriteRenderer>();
                }
                _over8.transform.localPosition = _legs8.transform.localPosition;
                _over8.flipX = _legs8.flipX;
                _over8.sprite = _swing8Over;
                _over8.sortingLayerID = _legs8.sortingLayerID;
                int top = _legs8.sortingOrder;
                foreach (var l in new[] { RigLayer.Head, RigLayer.HeadBack, RigLayer.HeadArmor })
                    if (_layers.TryGetValue(l, out var hs) && hs != null) top = Mathf.Max(top, hs.sortingOrder);
                if (_helm8 != null) top = Mathf.Max(top, _helm8.sortingOrder);
                _over8.sortingOrder = top + 1;
                _over8.enabled = true;
            }
        }

        // ---- the greatsword's sweep: S3, the descending diagonal (the user's pick, 2026-11-11, off
        // https://claude.ai/artifact/KBM2dirVEgFRoCsA4QYPDv). A real 3D swing rather than today's picture: wound over the
        // right shoulder through the cock, down across the body to the left hip through the strike, a little further
        // after; a chain's alternate sweep runs the mirror diagonal (left shoulder to right hip). Both hands on the grip.
        // Drawing only - the hit, timing and step are the rig's.

        /// <summary>The sweep's angle in its plane (degrees: 90 out over the wound shoulder, -90 down past the other
        /// hip) at the end of each phase: rest, cocked, struck, followed through.</summary>
        static readonly float[] SweepPhi = { 30f, 140f, -100f, -110f };
        /// <summary>How steep the diagonal is, degrees above level.</summary>
        public static float SweepTilt = 40f;

        void DiagonalSweep8(System.Collections.Generic.List<Weapon8.Card> cards, Vector3?[] hands, Vector3 f, Vector3 r)
        {
            float k = SwingKNow;
            StrikePhases(_motion, _attackLength, out float cockEnd, out float strikeEnd);
            float phi = k < cockEnd ? Mathf.Lerp(SweepPhi[0], SweepPhi[1], Mathf.SmoothStep(0f, 1f, k / Mathf.Max(1e-4f, cockEnd)))
                      : k < strikeEnd ? Mathf.Lerp(SweepPhi[1], SweepPhi[2], Mathf.SmoothStep(0f, 1f, (k - cockEnd) / Mathf.Max(1e-4f, strikeEnd - cockEnd)))
                      : Mathf.Lerp(SweepPhi[2], SweepPhi[3], Mathf.SmoothStep(0f, 1f, (k - strikeEnd) / Mathf.Max(1e-4f, 1f - strikeEnd)));
            float side = SweepSign(_motion, _alt, _attackLength) < 0f ? 1f : -1f;   // the first sweep winds over the RIGHT shoulder
            float tilt = SweepTilt * Mathf.Deg2Rad, a = phi * Mathf.Deg2Rad;
            var e2 = (r * (side * Mathf.Cos(tilt)) + Vector3.up * Mathf.Sin(tilt)).normalized;
            var dir = (f * Mathf.Cos(a) + e2 * Mathf.Sin(a)).normalized;
            // the flat turned to the camera (the line of sight is (0, DepthY, 1) - what projects to a point), so the blade
            // reads at its full width; pointing straight at the camera it falls back to the flat in the swing's plane
            var sight = new Vector3(0f, Locomotion8.DepthY, 1f);
            var across = Vector3.Cross(dir, sight);
            across = across.sqrMagnitude > 0.05f ? across.normalized : Vector3.Cross(Vector3.Cross(f, e2), dir).normalized;
            var pivot = new Vector3(0f, _b8Pose.HipY + 4f + 26f, 0f) + r * (side * 3f) + f * 4f;
            var grip = pivot + dir * 14f;
            var main = cards[0];
            float uLen = main.U.magnitude, vLen = main.V.magnitude;
            main.O = grip; main.U = across * uLen; main.V = -dir * vLen; main.FollowsHand = 1;
            cards[0] = main;
            hands[0] = grip; hands[1] = grip - dir * 6f;
        }

        // ---- where a held sprite is drawn (the element pictures, Combat.ElementArtFx, ride the drawn weapon)

        struct Redraw8 { public SpriteRenderer Sr; public int Card; public Vector2 S0, U2, V2; public Vector3 O, U3, V3; }
        System.Collections.Generic.List<Redraw8> _redraw8;
        float _r8Drop, _r8Sx = 1f;

        /// <summary>A world point to the drawn frame's screen texels (x right, y up from the soles), as the swing reads it.</summary>
        Vector2 Tex8(Vector3 world)
        {
            var l = transform.InverseTransformPoint(world);
            return new Vector2(_r8Sx * l.x * bodyPpu, (l.y - _b8Sole) * bodyPpu - _r8Drop);
        }

        /// <summary>Is <paramref name="sr"/> (today's weapon, hidden) drawn into the eight-direction body this frame?</summary>
        public bool Redrawn8(SpriteRenderer sr)
        {
            if (!_swing8Drawn || _redraw8 == null || sr == null) return false;
            foreach (var m in _redraw8) if (m.Sr == sr) return true;
            return false;
        }

        /// <summary>Where a point of <paramref name="sr"/> (today's world space) is drawn on the eight-direction body:
        /// its place on the sprite found from today's picture, then that place on the card.</summary>
        public bool Redrawn8(SpriteRenderer sr, Vector3 world, out Vector3 drawn)
        {
            drawn = world;
            if (!_swing8Drawn || _redraw8 == null || sr == null || _legs8 == null) return false;
            foreach (var m in _redraw8)
            {
                if (m.Sr != sr) continue;
                var d = Tex8(world) - m.S0;
                float det = m.U2.x * m.V2.y - m.U2.y * m.V2.x;
                float a, b;
                if (Mathf.Abs(det) > 1e-6f) { a = (d.x * m.V2.y - d.y * m.V2.x) / det; b = (m.U2.x * d.y - m.U2.y * d.x) / det; }
                else { a = 0f; b = Vector2.Dot(d, m.V2) / Mathf.Max(1e-6f, m.V2.sqrMagnitude); }
                var p = m.O + m.U3 * a + m.V3 * b;
                drawn = transform.TransformPoint(new Vector3(_r8Sx * p.x / bodyPpu, _b8Sole + (p.y - p.z * Locomotion8.DepthY) / bodyPpu, 0f));
                return true;
            }
            return false;
        }

        /// <summary>The last swing frame's picture, destroyed (each is drawn once).</summary>
        void DropSwing8()
        {
            if (_swing8Over != null)
            {
                if (_over8 != null && _over8.sprite == _swing8Over) { _over8.sprite = null; _over8.enabled = false; }
                Destroy(_swing8Over.texture); Destroy(_swing8Over);
                _swing8Over = null;
            }
            if (_swing8Sprite == null) return;
            if (_legs8 != null && _legs8.sprite == _swing8Sprite) _legs8.sprite = null;
            Destroy(_swing8Sprite.texture); Destroy(_swing8Sprite);
            _swing8Sprite = null;
        }

        Sprite _rest8Sprite, _rest8Over;

        /// <summary>The last frame-only rest picture (Penumbra's echoes), destroyed once replaced.</summary>
        void DropRest8()
        {
            if (_rest8Over != null)
            {
                if (_over8 != null && _over8.sprite == _rest8Over) _over8.sprite = null;
                Destroy(_rest8Over.texture); Destroy(_rest8Over); _rest8Over = null;
            }
            if (_rest8Sprite == null) return;
            if (_legs8 != null && _legs8.sprite == _rest8Sprite) _legs8.sprite = null;
            Destroy(_rest8Sprite.texture); Destroy(_rest8Sprite); _rest8Sprite = null;
        }

        void Unhide8()
        {
            if (_hidden8 == null) return;
            foreach (var sr in _hidden8) if (sr != null && sr.sprite != null) sr.enabled = true;
            _hidden8.Clear();
            if (_lastLoadout != null) Apply(_lastLoadout);
        }
    }
}
