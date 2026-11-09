using UnityEngine;

namespace Convergence.Art.Gear
{
    // THE DISC PAIR'S STANCES (the user's picks, 2026-11-09, off renders of this rig:
    // https://claude.ai/artifact/DNXik8ZUaP3z1Kad2Wac32). Out of combat both discs are STOWED on the back,
    // the hands empty; in combat they are WINGS, held low behind the body with the rims standing out past
    // both sides - and turned EDGE-ON beside the hips while the aim points left or right. A swing takes the arms
    // as before; the stance eases back in after it (CarryReturnDelay, like the greatsword's carry).
    //
    // DRAWING AND STOWING (the user's call, 2026-11-09: "create the necessary transitions"): one clock,
    // _discDraw, 0 stowed .. 1 drawn, run at a constant rate. The first half lifts both arms up over the
    // shoulders (DiscReach*), the discs travel off the back into the fists across its middle, the second half
    // brings the arms down into the fight's stance; stowing runs the same clock backwards. A swing never waits
    // on it: pressed with the discs on the back, they are in the fists at once.
    //
    // Discs stay upright: turned at all, the point-filtered art breaks up.
    // Edge-on is the disc art scaled across - the one squash in the rig - about the disc's own centre.
    public partial class PrimitiveCharacterRig
    {
        // Rig degrees (z, counter-clockwise), written for the near arm; the far arm is the mirror.
        const float DiscWingUpper = -24f, DiscWingElbow = -2f;
        const float DiscEdgeUpper = -30f, DiscEdgeElbow = -4f;
        /// <summary>How far the winged and the edge-on arms swing with the walk, degrees.</summary>
        const float DiscWingWalkDegrees = 7f, DiscEdgeWalkDegrees = 9f;
        /// <summary>Stowed, the empty arms hang out from the sides (ShoulderOffArmDegrees) and swing with the
        /// walk this far, degrees.</summary>
        const float DiscStowWalkDegrees = 16f;
        /// <summary>Reaching over the shoulders for the stowed discs: the upper arm out level, the forearm folded
        /// up and in (forearm at 150 degrees on screen), the fist just above the shoulder.</summary>
        const float DiscReachUpper = -90f, DiscReachElbow = -120f;
        /// <summary>A whole draw (or stow), seconds.</summary>
        const float DiscDrawSeconds = 0.45f;
        /// <summary>Across this span of the draw clock the discs travel from the back into the fists.</summary>
        const float DiscGrabFrom = 0.35f, DiscGrabTo = 0.6f;
        /// <summary>Edge-on discs' width, a share of their face-on width.</summary>
        const float DiscEdgeSquash = 0.4f;
        /// <summary>The aim's sideways share (|cos|) over which the wings turn edge-on.</summary>
        const float DiscEdgeFrom = 0.55f, DiscEdgeFull = 0.85f;
        const float DiscEdgeSharpness = 10f;
        /// <summary>Stowed: each disc's centre against its own shoulder joint (rest), rig units - up and in
        /// behind the shoulders, rims showing over them.</summary>
        static readonly Vector3 DiscStowNear = new(0.05f, 0.06f, 0f), DiscStowFar = new(-0.05f, 0.08f, 0f);
        /// <summary>...and from behind, lower: on the shoulder blades, where the head no longer covers them.</summary>
        static readonly Vector3 DiscStowAwayDrop = new(0f, -0.16f, 0f);

        /// <summary>A fight on the floor - the caller's word (GameBootstrap, for the arena's player).
        /// Defaults TRUE so a rig nobody drives (the character preview) shows its discs in hand.</summary>
        [SerializeField] bool _inCombat = true;
        /// <summary>0 = the swing's arms, 1 = the stance's. Eased.</summary>
        float _discStance;
        /// <summary>The draw clock: 0 = stowed, 1 = drawn (see the note above). Starts drawn, like _inCombat.</summary>
        float _discDraw = 1f;
        /// <summary>0 = wings, 1 = edge-on. Eased.</summary>
        float _discEdge;
        /// <summary>Whether the discs are on the back this frame / the stance changed their sort orders.</summary>
        bool _discStowed, _discSorted;

        /// <summary>Whether the winged back view has the forearms and fists lifted over the cape.</summary>
        bool _discHandsLifted;

        /// <summary>The forearms and fists, in the order they stack over the cape and discs from behind.</summary>
        static readonly RigLayer[] DiscGripLayers =
        {
            RigLayer.ArmBackLower, RigLayer.ArmFrontLower, RigLayer.GlovesBackLower, RigLayer.GlovesFrontLower,
            RigLayer.ArmBackHand, RigLayer.ArmFrontHand, RigLayer.GlovesBackHand, RigLayer.GlovesFrontHand,
        };

        public void SetInCombat(bool inCombat) => _inCombat = inCombat;

        /// <summary>The disc pair in both hands, nothing else reaching with them.</summary>
        bool DiscStanceApplies => _dualWield && _split && !_twinReach && !_reaching && !HeldTwoHanded && !HeldBow;

        /// <summary>
        /// Lay the stance over the arms the swing code posed, before ApplyPresent (the showcase still poses
        /// over everything) and before the pauldrons follow the arms.
        /// </summary>
        void PoseDiscStance(float dt, float armTravel, bool swinging)
        {
            bool applies = DiscStanceApplies;
            bool fighting = _inCombat || !_gripShouldered || swinging || _sinceSwing < CarryReturnDelay;
            bool rest = applies && !swinging && _sinceSwing >= CarryReturnDelay && _present <= 0f;
            _discStance = Mathf.Lerp(_discStance, rest ? 1f : 0f,
                1f - Mathf.Exp(-(swinging ? GripToSwingSharpness : GripBlendSharpness) * dt));
            _discDraw = swinging ? 1f : Mathf.MoveTowards(_discDraw, fighting ? 1f : 0f, dt / DiscDrawSeconds);
            // where the discs are: 0 on the back, 1 in the fists
            float grab = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DiscGrabFrom, DiscGrabTo, _discDraw));

            float sideways = _aimViews ? Mathf.Abs(Mathf.Cos(_aimAngle * Mathf.Deg2Rad)) : 0f;
            float edgeWant = _facingAway ? 0f
                : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(DiscEdgeFrom, DiscEdgeFull, sideways));
            _discEdge = Mathf.Lerp(_discEdge, edgeWant, 1f - Mathf.Exp(-DiscEdgeSharpness * dt));

            _discStowed = applies && grab < 0.5f && _present <= 0f;
            if (!applies) { RestoreDiscSort(); return; }

            float w = _discStance;
            if (w > 0.001f && _armFront && _elbowFront && _armBack && _elbowBack)
            {
                // stowed, the free arm's hang (out from the side, swinging); drawing, up over the shoulders and
                // down into the fight's stance
                float fightUpper = Mathf.Lerp(DiscWingUpper, DiscEdgeUpper, _discEdge);
                float fightElbow = Mathf.Lerp(DiscWingElbow, DiscEdgeElbow, _discEdge);
                float upper, elbow;
                if (_discDraw < 0.5f)
                {
                    float t = Mathf.SmoothStep(0f, 1f, _discDraw * 2f);
                    upper = Mathf.Lerp(-ShoulderOffArmDegrees, DiscReachUpper, t);
                    elbow = Mathf.Lerp(0f, DiscReachElbow, t);
                }
                else
                {
                    float t = Mathf.SmoothStep(0f, 1f, _discDraw * 2f - 1f);
                    upper = Mathf.Lerp(DiscReachUpper, fightUpper, t);
                    elbow = Mathf.Lerp(DiscReachElbow, fightElbow, t);
                }
                // the walk's swing, quiet while the arms are up over the shoulders
                float walk = _gaitWeight * Mathf.Abs(2f * _discDraw - 1f) * Mathf.Lerp(DiscStowWalkDegrees,
                    Mathf.Lerp(DiscWingWalkDegrees, DiscEdgeWalkDegrees, _discEdge), _discDraw);
                SetZ(_armFront, Mathf.LerpAngle(Z(_armFront), upper + armTravel * walk, w));
                SetZ(_elbowFront, Mathf.LerpAngle(Z(_elbowFront), elbow, w));
                SetZ(_armBack, Mathf.LerpAngle(Z(_armBack), -upper - armTravel * walk, w));
                SetZ(_elbowBack, Mathf.LerpAngle(Z(_elbowBack), -elbow, w));
                _armFront.localPosition = Vector3.Lerp(_armFront.localPosition, _armFrontRest, w);
                _armBack.localPosition = Vector3.Lerp(_armBack.localPosition, _armBackRest, w);
            }

            // Each disc between its place on the back and its place in the fist (the rig put it there, on the arm
            // posed above), by the draw clock. The off disc's place in its hand is SetWeaponSplit's, written once;
            // the stance moves and scales it, so it is put back every frame first.
            var off = _offhand;
            bool held = w > 0.5f && !_discStowed;
            bool upright = w > 0.5f || grab < 0.999f;
            float squash = Mathf.Lerp(1f, DiscEdgeSquash, _discEdge * w * grab);
            Vector3 drop = _facingAway ? DiscStowAwayDrop : Vector3.zero;
            if (_weaponPivot != null)
            {
                if (grab < 0.999f)
                    _weaponPivot.position = Vector3.Lerp(_armFront.parent.TransformPoint(_armFrontRest + DiscStowNear + drop),
                                                         _weaponPivot.position, grab);
                if (upright) _weaponPivot.rotation = transform.rotation;
                if (squash < 0.999f && _layers.TryGetValue(RigLayer.Weapon, out var main) && main != null)
                    SquashAboutCentre(main, squash, 1f);
            }
            if (off != null)
            {
                off.transform.localPosition = _weaponGripRest;
                off.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
                if (grab < 0.999f)
                    off.transform.position = Vector3.Lerp(_armBack.parent.TransformPoint(_armBackRest + DiscStowFar + drop),
                                                          off.transform.position, grab);
                if (upright) off.transform.rotation = transform.rotation;
                if (squash < 0.999f) SquashAboutCentre(off, squash, 0.92f);
            }

            // Sorting. Stowed or winged, both discs are BEHIND the body: face-on under all of it. From behind,
            // STOWED discs are the nearest thing to the camera and go over everything - one past the whole band,
            // the quiver's slot (the user's call, 2026-11-09); WINGS sit on the cape - its order, a hair nearer
            // the camera (a z tie-break) - so the head and helm still cover them. Edge-on, the far disc comes in
            // front, beside the far hip.
            bool sortStow = _discStowed || (held && _discEdge <= 0.5f), sortEdge = held && _discEdge > 0.5f && !_facingAway;
            if (sortStow || sortEdge)
            {
                _discSorted = true;
                int cape = _facingAway && _layers.TryGetValue(RigLayer.Back, out var c) && c != null && c.enabled
                    ? c.sortingOrder : int.MinValue;
                _layers.TryGetValue(RigLayer.Weapon, out var m);
                if (_discStowed && _facingAway)
                {
                    foreach (var d in new[] { m, off })
                        if (d != null) d.sortingOrder = BaseSortingOrder + RigLayers.All.Length;
                }
                else if (sortStow && cape != int.MinValue)
                {
                    foreach (var d in new[] { m, off })
                        if (d != null) { d.sortingOrder = cape; d.transform.position += new Vector3(0f, 0f, -0.002f); }
                }
                else
                {
                    if (m != null) m.sortingOrder = sortStow ? BaseSortingOrder - 2 : BaseSortingOrder + _weaponRank;
                    if (off != null) off.sortingOrder = sortStow ? BaseSortingOrder - 1 : BaseSortingOrder + _weaponRank - 1;
                }
            }
            else RestoreDiscSort();

            // WINGS FROM BEHIND: the cape covers the forearms and fists, and the discs on it read as stuck to the
            // back (the user's report, 2026-11-09). The forearms and fists come up over the cape and the discs -
            // the slots past the band (the quiver's and the vials'), which no disc rig uses - so each fist is
            // seen gripping its disc. The rest of the arm stays under the cape.
            bool lift = held && _facingAway && !_discStowed;
            if (lift)
            {
                _discHandsLifted = true;
                int top = BaseSortingOrder + RigLayers.All.Length;
                for (int i = 0; i < DiscGripLayers.Length; i++)
                    if (_layers.TryGetValue(DiscGripLayers[i], out var g) && g != null)
                        g.sortingOrder = top + i / 2;   // forearms, gloved forearms, hands, gloved hands
            }
            else if (_discHandsLifted)
            {
                _discHandsLifted = false;
                ApplyLayerOrder();
            }
        }

        /// <summary>Back to SetWeaponSplit's orders once the stance stops changing them.</summary>
        void RestoreDiscSort()
        {
            if (!_discSorted) return;
            _discSorted = false;
            if (_split) SetWeaponSplit(true);
        }

        static float Z(Transform t) => t.localEulerAngles.z;
        static void SetZ(Transform t, float z) => t.localRotation = Quaternion.Euler(0, 0, z);

        /// <summary>Scale a disc across about its own centre (its sprite's pivot is the rig's shoulder).</summary>
        static void SquashAboutCentre(SpriteRenderer sr, float squash, float size)
        {
            if (sr.sprite == null) return;
            var t = sr.transform;
            t.localScale = new Vector3(size, size, 1f);
            Vector3 before = t.TransformPoint(sr.sprite.bounds.center);
            t.localScale = new Vector3(size * squash, size, 1f);
            t.position += before - t.TransformPoint(sr.sprite.bounds.center);
        }
    }
}
