using UnityEngine;
using Convergence.Combat;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// The bow's QUIVER and the NOCKED ARROW (the user's calls, 2026-10-16 - they replace the belt
    /// vials). The quiver is the drawn bow's own (DemoGear.BowKit), worn on the BACK: facing the
    /// camera it sits behind the whole figure, cape included, so only its fletchings and mouth show
    /// past the far shoulder; turned away it is over everything - cape, shoulders, helmet. The draw's first beat
    /// reaches up over the shoulder into it (Animate's Draw); from the top of that reach the bow's
    /// own arrow is in the string hand - pulled out point-up, brought onto the bow, and drawn back
    /// along the line from the hand to the grip until it is loosed (Combat.ThrownArrow flies it).
    /// The Hive has neither (it is made of arrows - HiveLoose).
    /// </summary>
    public partial class PrimitiveCharacterRig
    {
        /// <summary>Where the quiver's MOUTH sits on the torso (body cells) and how far it leans,
        /// facing the camera and turned away - placed by eye in the arena: the mouth over the
        /// far shoulder so the fletchings show behind the head (lower, it was wholly hidden by
        /// the cloak), and turned away out past the head's edge (under it, the head hid them).</summary>
        public static Vector2 QuiverFrontAt = new(-9f, 26f), QuiverAwayAt = new(-7f, 20f);
        public static float QuiverFrontTilt = 40f, QuiverAwayTilt = 38f;

        /// <summary>Draw's beats with a quiver: up over the shoulder into it, then the hand brings
        /// the arrow forward onto the bow, then the pull. Fractions of the draw.</summary>
        const float DrawQuiverEnd = 0.26f, DrawNockEnd = 0.44f;
        /// <summary>The string arm's angle at the quiver's mouth, and at the nock.</summary>
        const float DrawQuiverSwing = -165f, DrawNockSwing = -30f;

        SpriteRenderer _quiver, _nocked, _nocked2;

        /// <summary>Set around the live swing's Animate call (Animate is static): the draw reaches
        /// over the shoulder into a quiver rather than to the belt.</summary>
        static bool _drawFromQuiver;
        Sprite _arrowSprite;

        /// <summary>The drawn bow's quiver and arrow (null, null hides both - not a bow, or the Hive).</summary>
        public void SetQuiver(Sprite quiver, Sprite arrow)
        {
            _arrowSprite = arrow;
            EnsureQuiver();
            if (_quiver != null)
            {
                _quiver.sprite = quiver;
                _quiver.enabled = quiver != null;
                Kindle(_quiver);
            }
            if (_nocked != null)
            {
                _nocked.sprite = arrow;
                _nocked.enabled = false;
                Kindle(_nocked);
            }
            ApplyLayerOrder();
        }

        bool HasQuiver => _quiver != null && _quiver.sprite != null && _quiver.enabled;

        /// <summary>Every bow draw - standing, from the kneel, from the recoil.</summary>
        static bool IsBowDraw(AttackMotion m) =>
            m == AttackMotion.Draw || m == AttackMotion.KneelDraw || m == AttackMotion.RecoilDraw
            || m == AttackMotion.LobDraw || m == AttackMotion.HighDraw
            || m == AttackMotion.SnapDraw || m == AttackMotion.CantedDraw || m == AttackMotion.TwinDraw
            || m == AttackMotion.FlipDraw;

        /// <summary>The draw's own clock: the swing's, or the SNAP's faster one.</summary>
        float DrawKNow => _motion == AttackMotion.SnapDraw
            ? Mathf.Min(1f, SwingKNow / Core.Tuning.Bow.SnapRelease)
            : _motion == AttackMotion.FlipDraw ? Mathf.Min(1f, SwingKNow / Core.Tuning.Disengage.LooseAt)
            : SwingKNow;

        /// <summary>A kindled kit's marks light on the Secret Fire's beat, as the bow's do.</summary>
        static void Kindle(SpriteRenderer sr)
        {
            if (sr == null || sr.sprite == null || !SecretFire.HasMarks(sr.sprite)) return;
            KindledMarks.On(sr);
        }

        /// <summary>Found by NAME before being created (a domain reload keeps the objects and loses
        /// the fields - EnsureVials' defence).</summary>
        void EnsureQuiver()
        {
            if (_quiver == null && _torso != null)
            {
                var t = _torso.Find("Quiver");
                if (t == null) { t = new GameObject("Quiver").transform; t.SetParent(_torso, false); }
                _quiver = t.GetComponent<SpriteRenderer>();
                if (_quiver == null) _quiver = t.gameObject.AddComponent<SpriteRenderer>();   // never ??: a missing component is not C# null
            }
            if (_nocked == null)
            {
                var t = transform.Find("NockedArrow");
                if (t == null) { t = new GameObject("NockedArrow").transform; t.SetParent(transform, false); }
                _nocked = t.GetComponent<SpriteRenderer>();
                if (_nocked == null) _nocked = t.gameObject.AddComponent<SpriteRenderer>();
                _nocked.enabled = false;
            }
            if (_nocked2 == null)
            {
                var t = transform.Find("NockedArrow2");
                if (t == null) { t = new GameObject("NockedArrow2").transform; t.SetParent(transform, false); }
                _nocked2 = t.GetComponent<SpriteRenderer>();
                if (_nocked2 == null) _nocked2 = t.gameObject.AddComponent<SpriteRenderer>();
                _nocked2.enabled = false;
            }
        }

        /// <summary>The quiver's sort, re-derived with every layer order: behind the whole figure
        /// facing the camera; turned away it is the piece NEAREST the camera, so it goes over the
        /// whole band - shoulders, helmet, hood and arms (the user's call; at the cape's rank the
        /// pauldrons and the helm covered it). The slot past the band is the vials' old one, inside
        /// DepthStride.</summary>
        void OrderQuiver(int[] table)
        {
            if (_quiver == null) return;
            _quiver.sortingOrder = _facingAway ? BaseSortingOrder + RigLayers.All.Length
                                               : BaseSortingOrder - 2;
        }

        /// <summary>The quiver on the back, by view.</summary>
        void AnimateQuiver()
        {
            if (!HasQuiver) return;
            var at = _facingAway ? QuiverAwayAt : QuiverFrontAt;
            var p = PixelSprite.Px(at.x, at.y);
            _quiver.transform.localPosition = new Vector3(p.x, p.y, _quiver.transform.localPosition.z);
            _quiver.transform.localRotation = Quaternion.Euler(0f, 0f, _facingAway ? QuiverAwayTilt : QuiverFrontTilt);
        }

        /// <summary>How fast the bow arm relaxes after the release.</summary>
        const float BowSquareRelease = 4f;
        /// <summary>How much of the arrow is drawn back past the bow at full draw - the head is
        /// left this share of the arrow's length ahead of the grip.</summary>
        const float BowHeadPastGrip = 0.14f;

        // The drawn string (GearItem.BowStringless): the bow's picture without its string while it
        // is drawn, and two lines from the nocks to the arrow's nock.
        Sprite _bowStringless, _bowStrung;
        Vector2 _nockTop, _nockBottom;
        Color _stringColor = Color.black;
        SpriteRenderer _stringTop, _stringBottom;
        static Sprite _stringPixel;

        public void SetBowString(Sprite stringless, Vector2 nockTop, Vector2 nockBottom, Color color)
        {
            RestoreStrung();
            _bowStringless = stringless;
            _nockTop = nockTop;
            _nockBottom = nockBottom;
            _stringColor = color;
        }

        void SquareBow(float degrees)
        {
            if (_weaponPivot == null || !_bowGrip || Mathf.Abs(degrees) < 0.01f) return;
            // A world turn, in a frame the root's mirror may have flipped.
            float sign = Mathf.Sign(_weaponPivot.lossyScale.x * _weaponPivot.lossyScale.y);
            _weaponPivot.localRotation *= Quaternion.Euler(0f, 0f, degrees * sign);
        }

        /// <summary>
        /// THE DRAW, after the arms are posed (the user's reports, 2026-10-17: the arrow was not
        /// square to the bow, the bow was not in the hand and wobbled at full draw, the arrow never
        /// sat on the string and the string never drew). The BOW is the anchor: it stands in the
        /// bow hand's fist, square to the bow arm's line (shoulder to fist) - which holds still
        /// through the pull. The arrow lies on that line through the grip. Out of the quiver the
        /// string hand brings it onto the STRING (where the string crosses the line), then pulls
        /// the nock straight back along the line until the head is just past the grip - the string
        /// hand REACHES that nock point (two-bone, SolveArmTo) rather than wandering on its own
        /// angles, and the string bends from both nocks to it.
        /// </summary>
        void PlaceNockedArrow()
        {
            if (_nocked == null) return;
            // The draw runs on its own clock, then HOLDS at full until the arrow is loosed - the
            // shot leaves a few frames after the clock, and relaxing first swivelled the bow.
            _looseBackstop -= Dt;
            if (_looseBackstop <= 0f) _awaitLoose = false;
            bool drawMotion = IsBowDraw(_motion);
            bool drawing = _arrowSprite != null && _bowGrip && drawMotion && _awaitLoose && !_stone && HasQuiver;
            float k = _attackTimer > 0f ? DrawKNow : 1f;
            var weapon = WeaponRenderer;
            if (!drawing || k < DrawQuiverEnd * 0.85f || _elbowFront == null || _armBack == null || weapon == null)
            {
                _nocked.enabled = false;
                if (_nocked2 != null) _nocked2.enabled = false;
                RestoreStrung();
                // After the loose the bow arm stays out and RELAXES over a moment - let go at once,
                // the bow dropped from square to the carry in a frame.
                // The HIGH connector holds the bow raised after the lob, until the art (as the kneel
                // and the recoil hold their stances - _sinceSwing is held at 0 for them).
                bool connectorHeld = (_motion == AttackMotion.LobDraw || _motion == AttackMotion.CantedDraw
                                      || _motion == AttackMotion.TwinDraw)
                                     && _sinceSwing <= 0f && _attackTimer <= 0f;
                if (!connectorHeld) _bowHold = Mathf.Lerp(_bowHold, 0f, 1f - Mathf.Exp(-BowSquareRelease * Dt));
                if (_bowHold > 0.002f && weapon != null && _armBack != null && _bowGrip && !_stone)
                    HoldBowArm(weapon, DrawLine(), _bowHold);
                else
                {
                    _bowHold = 0f;
                    if (!_stone) BowInFist(weapon);
                }
                // Held canted, the string hand stays turned; it turns back as the bow comes down.
                TurnDrawHand(_motion == AttackMotion.CantedDraw && _bowHold > 0.5f);
                return;
            }
            if (_nocked.sprite != _arrowSprite) _nocked.sprite = _arrowSprite;
            _nocked.enabled = true;

            // Out of the quiver point-up, onto the string over the nock beat, then the pull.
            float onto = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DrawQuiverEnd, DrawNockEnd, k));
            float pull = 1f - Mathf.Pow(1f - Mathf.Clamp01((k - DrawNockEnd) / (1f - DrawNockEnd)), 2f);

            // The line: the AIM. The bow arm is reached straight out along it from its own
            // shoulder and held there - a drawn bow's arm is locked, so nothing the body does
            // through the pull may swing it.
            var along = DrawLine();
            float lineAngle = Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg;
            float raise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / DrawQuiverEnd));
            // Never DOWN from where a previous loose left it (back-to-back shots stay up).
            _bowHold = Mathf.Max(raise, _bowHold);
            HoldBowArm(weapon, along, _bowHold);
            Vector2 grip = weapon.transform.position;

            // Where the arrow sits on the string at rest, and where the pull takes the nock.
            float sy = Mathf.Abs(transform.lossyScale.y);
            float length = _arrowSprite.bounds.size.x * sy;
            bool strung = _bowStringless != null;
            Vector2 nTop = weapon.transform.TransformPoint(_nockTop), nBottom = weapon.transform.TransformPoint(_nockBottom);
            // The pull ends at the ANCHOR by the chin whatever the aim (the user's report: aimed
            // down, a pull straight back along the aim took the hand up over the head). The arrow
            // lies from there to the grip - never drawn past its own length.
            Vector2 anchor = DrawAnchor();
            var shot = grip - anchor;
            float reachBack = Mathf.Min(shot.magnitude, length * (1f - BowHeadPastGrip));
            shot = shot.sqrMagnitude > 1e-6f ? shot.normalized : along;
            lineAngle = Mathf.Atan2(shot.y, shot.x) * Mathf.Rad2Deg;
            Vector2 rest = strung ? ClosestOnLine(nTop, nBottom, grip, shot) : grip - shot * (length * 0.2f);
            Vector2 full = grip - shot * reachBack;
            Vector2 drawn = Vector2.Lerp(rest, full, pull);

            // The string hand: wherever its own swing has it out of the quiver, then onto the nock.
            Vector2 handNow = _elbowFront.TransformPoint(CarryGrip);
            Vector2 handTo = Vector2.Lerp(handNow, drawn, onto);
            if (onto > 0f && _armFront != null)
            {
                SolveArmTo(_armFront, _elbowFront, _armFrontRest, transform.InverseTransformPoint(handTo), false);
                handNow = _elbowFront.TransformPoint(CarryGrip);
            }

            // CANTED: the string hand turns flat with the bow as it nocks the arrow (and back after
            // the loose) - its back to the camera, so a ring on it rolls out of sight (the user's
            // call, 2026-10-17).
            TurnDrawHand(_motion == AttackMotion.CantedDraw && onto > 0.5f);

            // The arrow: nock in the hand, point up out of the quiver, onto the line.
            float angle = Mathf.LerpAngle(90f, lineAngle, onto);
            var dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            var nock = onto >= 1f ? drawn : handNow;
            float half = length * 0.5f;
            float sx = transform.lossyScale.x;
            var t = _nocked.transform;
            t.position = new Vector3(nock.x + dir.x * half, nock.y + dir.y * half, weapon.transform.position.z - 0.002f);
            t.rotation = Quaternion.Euler(0f, 0f, angle);
            t.localScale = new Vector3(Mathf.Sign(sx), 1f, 1f);
            // Over the bow, under nothing that should hide it.
            _nocked.sortingOrder = weapon.sortingOrder + 1;

            // TWIN NOCK: a second arrow on the string a hair under the first, fanned a little.
            bool twin = _motion == AttackMotion.TwinDraw && _nocked2 != null;
            if (_nocked2 != null) _nocked2.enabled = twin;
            if (twin)
            {
                if (_nocked2.sprite != _arrowSprite) _nocked2.sprite = _arrowSprite;
                float spread = Core.Tuning.Bow.TwinSpread;
                float a2 = angle - TwinFanDegrees * Mathf.Sign(sx) * onto;
                var d2 = new Vector2(Mathf.Cos(a2 * Mathf.Deg2Rad), Mathf.Sin(a2 * Mathf.Deg2Rad));
                var n2 = nock + Vector2.down * spread;
                var t2 = _nocked2.transform;
                t2.position = new Vector3(n2.x + d2.x * half, n2.y + d2.y * half, t.position.z - 0.0005f);
                t2.rotation = Quaternion.Euler(0f, 0f, a2);
                t2.localScale = t.localScale;
                _nocked2.sortingOrder = _nocked.sortingOrder;
            }

            // The string: straight until the arrow is on it, then bent to the nock.
            if (strung) DrawString(weapon, nTop, nBottom, onto >= 1f ? nock : rest);
        }

        /// <summary>The string hand turned flat (CANTED): the ring on it is out of sight.</summary>
        void TurnDrawHand(bool turned)
        {
            if (_layers != null && _layers.TryGetValue(RigLayer.Ring, out var ring) && ring != null)
                ring.forceRenderingOff = turned;
        }

        /// <summary>Where the string hand anchors at full draw: by the chin, a little behind the
        /// neck on the side away from the facing.</summary>
        Vector2 DrawAnchor()
        {
            Vector2 back = _facingLeft ? Vector2.right : Vector2.left;
            float s = Mathf.Abs(transform.lossyScale.y);
            // CANTED, the string hand comes back to the SHOULDER, not the chin - under the flat bow,
            // on the bow arm's own line (the user's report, 2026-10-17: it was drawn and loosed from
            // the upright shot's place).
            if (_motion == AttackMotion.CantedDraw && _armFront != null)
                return (Vector2)_armFront.position + (back * (DrawAnchorBack * 0.5f) + Vector2.down * CantAnchorDrop) * s;
            Vector2 head = _head != null ? (Vector2)_head.position : (Vector2)transform.position;
            return head + (back * DrawAnchorBack + Vector2.up * DrawAnchorUp) * s;
        }

        /// <summary>The anchor against the head's pivot, in rig units (scaled with the rig).</summary>
        const float DrawAnchorBack = 0.09f, DrawAnchorUp = -0.015f;
        /// <summary>How far the twin nock's second arrow fans off the first.</summary>
        const float TwinFanDegrees = 5f;

        /// <summary>The CANTED anchor under the shoulder - the chest (rig units).</summary>
        const float CantAnchorDrop = 0.06f;

        /// <summary>The draw's line in the world: the rig's turned aim (FaceAimTurning), else the
        /// mirror's facing.</summary>
        Vector2 DrawLine()
        {
            var dir = _aimViews && _aimAngleKnown
                ? new Vector2(Mathf.Cos(_aimAngle * Mathf.Deg2Rad), Mathf.Sin(_aimAngle * Mathf.Deg2Rad))
                : (_facingLeft ? Vector2.left : Vector2.right);
            // The HIGH pair: the lob is loosed with the bow raised toward the sky above the aim, and
            // the art from there comes down onto the aim while the arrow is nocked.
            float raise = 0f;
            if (_motion == AttackMotion.LobDraw) raise = 1f;
            // CANTED is SIDEARM: the bow arm held a little under the aim.
            else if (_motion == AttackMotion.CantedDraw) raise = -Core.Tuning.Bow.CantDropDegrees / Core.Tuning.Bow.LobRaiseDegrees;
            else if (_motion == AttackMotion.HighDraw && _attackTimer > 0f)
                raise = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(SwingKNow / DrawNockEnd));
            if (Mathf.Abs(raise) <= 0f) return dir;
            float side = Mathf.Abs(dir.x) > 0.05f ? Mathf.Sign(dir.x) : (_facingLeft ? -1f : 1f);
            float elev = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            elev = Mathf.Clamp(elev + raise * Core.Tuning.Bow.LobRaiseDegrees, -85f, 85f);
            return new Vector2(side * Mathf.Cos(elev * Mathf.Deg2Rad), Mathf.Sin(elev * Mathf.Deg2Rad));
        }

        /// <summary>Whether the drawn arrow is still on the string (PlayAttack sets it, LooseArrow
        /// clears it), and the seconds left before the backstop clears it anyway.</summary>
        bool _awaitLoose;
        float _looseBackstop;
        const float LooseBackstopSeconds = 0.3f;

        public void HoldDrawn(float seconds) => _looseBackstop = Mathf.Max(_looseBackstop, seconds);

        public Vector2? LooseArrow()
        {
            _awaitLoose = false;
            if (_motion == AttackMotion.FlipDraw) _flipLooseK = SwingKNow;
            return _nocked != null && _nocked.enabled ? (Vector2)_nocked.transform.position : (Vector2?)null;
        }

        /// <summary>How far the bow arm is held out on the draw's line, 0..1 - up with the draw,
        /// eased down after the loose.</summary>
        float _bowHold;

        /// <summary>The bow arm reached out along <paramref name="along"/> by <paramref name="weight"/>
        /// of the way from wherever its own swing put it, and the bow turned square to the line by
        /// as much.</summary>
        void HoldBowArm(SpriteRenderer weapon, Vector2 along, float weight)
        {
            // STRAIGHT (the user's call, 2026-10-17): the whole bow arm swings from the shoulder
            // onto the aim - through the full half circle, straight up to straight down - with the
            // elbow locked and the wrist straight. Both segments hang straight DOWN at zero
            // rotation, so the arm's angle is the aim's in its parent's frame (mirror included).
            if (_armBack.parent != null)
            {
                Vector2 local = _armBack.parent.InverseTransformDirection(along);
                float want = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg + 90f;
                float now = _armBack.localEulerAngles.z;
                _armBack.localPosition = Vector3.Lerp(_armBack.localPosition, _armBackRest, weight);
                _armBack.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(now, want, weight));
                if (_elbowBack != null)
                    _elbowBack.localRotation = Quaternion.Euler(0f, 0f,
                        Mathf.LerpAngle(_elbowBack.localEulerAngles.z, 0f, weight));
            }
            BowInFist(weapon, weight);
        }

        /// <summary>
        /// The bow FIRM IN THE FIST (the user's call, 2026-10-17 - it moves with the arm, never on
        /// its own): square to the FOREARM, belly out along it and the string toward the archer,
        /// by as much as the forearm is raised from hanging - so a raised arm carries the bow
        /// upright with it, and the bow only tips back to its hanging angle as the arm comes down.
        /// Every frame the bow is held, after the arm is posed.
        /// </summary>
        void BowInFist(SpriteRenderer weapon, float atLeast = 0f)
        {
            if (weapon == null || _elbowBack == null || !_bowGrip) return;
            Vector2 fore = (Vector2)weapon.transform.position - (Vector2)_elbowBack.position;
            if (fore.sqrMagnitude < 1e-8f) return;
            float raised = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(BowFistHangingDegrees, 90f, Vector2.Angle(fore, Vector2.down)));
            raised = Mathf.Max(raised, atLeast);   // drawn, it is square whichever way it aims
            if (raised <= 0f) return;
            fore.Normalize();
            float foreAngle = Mathf.Atan2(fore.y, fore.x) * Mathf.Rad2Deg;
            Vector2 up = weapon.transform.up;
            float upAngle0 = Mathf.Atan2(up.y, up.x) * Mathf.Rad2Deg;
            float toA = Mathf.DeltaAngle(upAngle0, foreAngle + 90f), toB = Mathf.DeltaAngle(upAngle0, foreAngle - 90f);
            // Of the two squares, the one with the BELLY out along the arm (the user's report: the
            // nearer one held the arc backwards). The string is authored on the picture's left, so
            // the belly is its +x - mirror included.
            Vector2 belly = weapon.transform.TransformDirection(Vector3.right);
            float turn = Vector2.Dot(Rotate(belly, toA), fore) >= 0f ? toA : toB;
            SquareBow(turn * raised);
            // CANTED: the bow turned flat about the arrow's line - in this view, foreshortened to
            // CantSquash of its height (the string's nocks come with it, they are its own points).
            // The draw follows it down to the shoulder (DrawAnchor).
            if (_motion == AttackMotion.CantedDraw && _weaponPivot != null)
            {
                float squash = Mathf.Lerp(1f, Core.Tuning.Bow.CantSquash, _bowHold);
                var ls = _weaponPivot.localScale;
                _weaponPivot.localScale = new Vector3(ls.x, ls.y * squash, ls.z);
            }
        }

        /// <summary>The forearm's angle from hanging straight down under which the bow keeps its
        /// own hanging angle in the fist.</summary>
        const float BowFistHangingDegrees = 25f;

        /// <summary>Where the line through <paramref name="p"/> along <paramref name="d"/> crosses
        /// the line through <paramref name="a"/> and <paramref name="b"/>.</summary>
        static Vector2 ClosestOnLine(Vector2 a, Vector2 b, Vector2 p, Vector2 d)
        {
            var e = b - a;
            float den = d.x * e.y - d.y * e.x;
            if (Mathf.Abs(den) < 1e-6f) return p;
            float u = ((a.x - p.x) * e.y - (a.y - p.y) * e.x) / den;
            return p + d * u;
        }

        void DrawString(SpriteRenderer weapon, Vector2 top, Vector2 bottom, Vector2 at)
        {
            if (weapon.sprite != _bowStringless)
            {
                _bowStrung = weapon.sprite;
                weapon.sprite = _bowStringless;
            }
            EnsureString();
            // One bow texel thick (the authored grid's cell, at the rig's scale).
            float thick = Mathf.Abs(transform.lossyScale.y) / 75f;
            Line(_stringTop, top, at, thick, weapon);
            Line(_stringBottom, bottom, at, thick, weapon);
        }

        void Line(SpriteRenderer sr, Vector2 from, Vector2 to, float thick, SpriteRenderer over)
        {
            var d = to - from;
            sr.enabled = true;
            sr.color = _stringColor;
            sr.sortingOrder = over.sortingOrder;
            var t = sr.transform;
            t.position = new Vector3(from.x, from.y, over.transform.position.z - 0.001f);
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            t.localScale = Vector3.one;
            var parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(d.magnitude / Mathf.Abs(parentScale.x), thick / Mathf.Abs(parentScale.y), 1f);
        }

        void RestoreStrung()
        {
            var weapon = WeaponRenderer;
            if (weapon != null && _bowStringless != null && weapon.sprite == _bowStringless && _bowStrung != null)
                weapon.sprite = _bowStrung;
            if (_stringTop != null) _stringTop.enabled = false;
            if (_stringBottom != null) _stringBottom.enabled = false;
        }

        /// <summary>Found by NAME before being created (a reload keeps the objects, loses the fields).</summary>
        void EnsureString()
        {
            if (_stringPixel == null)
            {
                var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();
                _stringPixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            }
            _stringTop = _stringTop != null ? _stringTop : StringLine("BowStringTop");
            _stringBottom = _stringBottom != null ? _stringBottom : StringLine("BowStringBottom");
        }

        SpriteRenderer StringLine(string name)
        {
            var t = transform.Find(name);
            if (t == null) { t = new GameObject(name).transform; t.SetParent(transform, false); }
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = _stringPixel;
            return sr;
        }
    }
}
