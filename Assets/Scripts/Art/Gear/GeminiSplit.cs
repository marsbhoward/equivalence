using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// The Gemini Blade's two halves while Solve et Coagula has them apart: carried from the fist
    /// to the two sheaths (one at each hip - the Gemini Saya), held there with their hilts
    /// standing out of the mouths, cross-drawn into an X in front of the body, then brought
    /// together onto the rig's own sword at the pose its slam starts from. The rig's weapon layer
    /// is hidden for all of it.
    ///
    /// Each half is drawn on the WHOLE sword's canvas with the other half's columns blank
    /// (DemoGear.GeminiHalfBlades), so the two placed on one pose ARE the sword, texel for texel -
    /// the split starts and ends on exactly the picture in the hand.
    ///
    /// A half going into its sheath is CUT at the mouth, KatanaSheathe's way: re-sliced each step
    /// to only the part still outside, trimmed from the tip end so the grip never moves.
    ///
    /// Everything is in the RIG ROOT's local space, so the mirror is the root's business (see
    /// KatanaSheathe). Posed in LateUpdate from the current phase, so the sheaths' swing (the rig's
    /// AnimateHipPieces, in Update) is already applied when a half reads where its mouth is.
    /// </summary>
    public class GeminiSplit : MonoBehaviour
    {
        public enum Phase { Part, Sheathe, Home, Draw, Rejoin }

        const int ClipStep = 4;

        /// <summary>How far the grip's centre stands out of the mouth once home, in world units -
        /// the guard just proud of it.</summary>
        const float GripOut = 0.14f;

        /// <summary>Where a half arrives before it slides in, as a distance above the mouth along
        /// the sheath's axis - already part way, KatanaSheathe's Poise.</summary>
        const float Poise = 0.32f;

        Transform _root;
        SpriteRenderer _weapon;
        readonly Half[] _halves = new Half[2];
        Vector2 _hang;                   // the sheath's hang point, sprite-local (HipSwingPivot)
        Vector2 _sheathAxis;             // down the sheath, sprite-local
        MonoBehaviour _rigHost;
        ICharacterRig Rig => _rigHost as ICharacterRig;

        Phase _phase;
        float _t;

        /// <summary>The fist's pose when the split started, root-local.</summary>
        Vector2 _startPos;
        float _startDeg;

        /// <summary>The rejoin's target: the rig's own sword, read each frame (the rig is holding
        /// the slam's first frame by then).</summary>
        Vector2 _joinPos;
        float _joinDeg;

        sealed class Half
        {
            public SpriteRenderer Sr;
            public SpriteRenderer Sheath;
            public Sprite Full;
            public readonly Dictionary<int, Sprite> Clips = new();
            public bool Far;
        }

        /// <param name="halves">The near hip's half and the far hip's, each on the whole canvas.</param>
        /// <param name="hang">The sheath's hang point, sprite-local world units (GearItem.HipSwingPivot).</param>
        /// <param name="sheathAxis">Down the sheath, sprite-local (DemoGear.GeminiSayaAxis).</param>
        public static GeminiSplit Attach(ICharacterRig rig, Sprite[] halves, Vector2 size,
                                         Vector2 hang, Vector2 sheathAxis)
        {
            var root = rig?.Transform;
            var hand = rig?.WeaponAnchor;
            if (root == null || hand == null || halves == null || halves.Length < 2) return null;

            var go = new GameObject("gemini.split");
            go.transform.SetParent(root, false);
            var e = go.AddComponent<GeminiSplit>();
            e._root = root;
            e._rigHost = rig as MonoBehaviour;
            e._weapon = rig.WeaponRenderer;
            e._hang = hang;
            e._sheathAxis = sheathAxis.normalized;

            // `size` is a WORLD size; this sits under the root, which carries the visual scale.
            var rootScale = root.lossyScale;
            if (Mathf.Abs(rootScale.x) > 0.0001f && Mathf.Abs(rootScale.y) > 0.0001f)
                size = new Vector2(size.x / Mathf.Abs(rootScale.x), size.y / Mathf.Abs(rootScale.y));

            var sheaths = FindSheaths(rig);
            for (int i = 0; i < 2; i++)
            {
                var hgo = new GameObject(i == 0 ? "near" : "far");
                hgo.transform.SetParent(go.transform, false);
                var native = halves[i].bounds.size;
                hgo.transform.localScale = new Vector3(
                    native.x > 0.0001f ? size.x / native.x : 1f,
                    native.y > 0.0001f ? size.y / native.y : 1f, 1f);
                var sr = hgo.AddComponent<SpriteRenderer>();
                sr.sprite = halves[i];
                e._halves[i] = new Half { Sr = sr, Sheath = sheaths[i], Full = halves[i], Far = i == 1 };
            }

            e._startPos = root.InverseTransformPoint(hand.position);
            e._startDeg = LocalAngle(root, hand.up);
            e.SetPhase(Phase.Part, 0f);
            return e;
        }

        /// <summary>The near hip's sheath (Trinket) and the far one's (TrinketBack), by layer name.</summary>
        static SpriteRenderer[] FindSheaths(ICharacterRig rig)
        {
            var found = new SpriteRenderer[2];
            found[0] = rig.TrinketRenderer;
            foreach (var sr in rig.Transform.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.name == nameof(RigLayer.TrinketBack)) { found[1] = sr; break; }
            return found;
        }

        public void SetPhase(Phase phase, float t)
        {
            _phase = phase;
            _t = Mathf.Clamp01(t);
            Pose();
        }

        /// <summary>Where each half's grip is, root-local - what the two hands follow (the near
        /// arm the near half, the far arm the far).</summary>
        public Vector2 NearGrip => GripOf(0);
        public Vector2 FarGrip => GripOf(1);

        Vector2 GripOf(int i) => _halves[i]?.Sr != null
            ? (Vector2)_root.InverseTransformPoint(_halves[i].Sr.transform.position) : _startPos;

        /// <summary>Take the rejoin's target - the rig's sword as the slam's first frame holds it.</summary>
        public void SetJoin(Transform hand)
        {
            if (hand == null) return;
            _joinPos = _root.InverseTransformPoint(hand.position);
            _joinDeg = LocalAngle(_root, hand.up);
        }

        public void Release()
        {
            if (this != null) Destroy(gameObject);
        }

        void LateUpdate() => Pose();

        void Pose()
        {
            if (_root == null) return;
            for (int i = 0; i < 2; i++)
            {
                var h = _halves[i];
                if (h?.Sr == null) continue;
                var (pos, deg) = PoseFor(h, i);
                h.Sr.transform.localPosition = pos;
                h.Sr.transform.localRotation = Quaternion.Euler(0f, 0f, deg);
                h.Sr.sprite = Clipped(h, Outside(h, pos, deg));
                SortHalf(h, i);
            }
        }

        /// <summary>
        /// How much blade past the grip is still OUTSIDE the sheath: if the mouth lies on the
        /// blade's line, ahead of the grip, the blade is cut there; otherwise all of it shows. One
        /// rule for going in, sitting home and coming out, so nothing snaps between phases.
        /// </summary>
        float Outside(Half h, Vector2 pos, float deg)
        {
            if (h.Sheath == null || !(_phase == Phase.Sheathe || _phase == Phase.Home || _phase == Phase.Draw))
                return float.PositiveInfinity;
            var (mouth, _) = Mouth(h);
            float r = deg * Mathf.Deg2Rad;
            var up = new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
            var v = mouth - pos;
            float along = Vector2.Dot(v, up);
            float across = Mathf.Abs(v.x * up.y - v.y * up.x);
            return along > 0f && across < MouthHalfWidth ? along : float.PositiveInfinity;
        }

        /// <summary>How far off the blade's line the mouth may be and still be the blade going
        /// through it, world units - about the sheath's bore.</summary>
        const float MouthHalfWidth = 0.07f;

        /// <summary>One half's grip position and angle for the current phase.</summary>
        (Vector2 pos, float deg) PoseFor(Half h, int i)
        {
            var (mouth, axis) = Mouth(h);
            float homeDeg = AngleOfUp(axis);                 // the tip down the sheath
            var poised = mouth - axis * Poise;
            var home = mouth - axis * GripOut;
            var drawn = mouth - axis * DrawnOut;
            var parted = Parted(i);

            switch (_phase)
            {
                case Phase.Part:
                {
                    // The crack: both halves on the sword's own pose, pulled apart sideways and
                    // splayed - one hand each.
                    float s = Smooth(_t);
                    return (Vector2.Lerp(_startPos, parted.pos, s), Mathf.LerpAngle(_startDeg, parted.deg, s));
                }
                case Phase.Sheathe:
                {
                    // Across to the mouth, aligned with the sheath, THEN down it - a straight lerp
                    // pushes the blade sideways through the scabbard's wall.
                    const float Align = 0.55f;
                    if (_t < Align)
                    {
                        float k = Smooth(_t / Align);
                        return (Vector2.Lerp(parted.pos, poised, k), Mathf.LerpAngle(parted.deg, homeDeg, k));
                    }
                    float s = Smooth((_t - Align) / (1f - Align));
                    return (Vector2.Lerp(poised, home, s), homeDeg);
                }
                case Phase.Home:
                    return (home, homeDeg);
                case Phase.Draw:
                {
                    // Out along the axis, flung WIDE - the arms spread, the near half back and the
                    // far one forward - then both slashed inward across each other into the X.
                    const float Out = 0.25f, Wide = 0.55f;
                    var wide = HeldAt(i, i == 0 ? WideNear : WideFar);
                    float wideDeg = i == 0 ? WideLean : -WideLean;
                    var cross = HeldAt(i, i == 0 ? CrossNear : CrossFar);
                    float crossDeg = i == 0 ? -CrossLean : CrossLean;
                    if (_t < Out) return (Vector2.Lerp(home, drawn, Smooth(_t / Out)), homeDeg);
                    if (_t < Wide)
                    {
                        float k = Smooth((_t - Out) / (Wide - Out));
                        return (Vector2.Lerp(drawn, wide, k), Mathf.LerpAngle(homeDeg, wideDeg, k));
                    }
                    float s = Smooth((_t - Wide) / (1f - Wide));
                    return (Vector2.Lerp(wide, cross, s), Mathf.LerpAngle(wideDeg, crossDeg, s));
                }
                default: // Rejoin
                {
                    // Up and apart overhead, then clapped together onto the sword.
                    var cross = HeldAt(i, i == 0 ? CrossNear : CrossFar);
                    float crossDeg = i == 0 ? -CrossLean : CrossLean;
                    var high = HeldAt(i, i == 0 ? HighNear : HighFar);
                    float highDeg = i == 0 ? HighLean : -HighLean;
                    const float Rise = 0.55f;
                    if (_t < Rise)
                    {
                        float k = Smooth(_t / Rise);
                        return (Vector2.Lerp(cross, high, k), Mathf.LerpAngle(crossDeg, highDeg, k));
                    }
                    float s = Smooth((_t - Rise) / (1f - Rise));
                    return (Vector2.Lerp(high, _joinPos, s), Mathf.LerpAngle(highDeg, _joinDeg, s));
                }
            }
        }

        /// <summary>The parted pose: the sword's own, each half pushed off its side by
        /// <see cref="PartGap"/> and splayed out by <see cref="PartSplay"/>.</summary>
        (Vector2 pos, float deg) Parted(int i)
        {
            float r = _startDeg * Mathf.Deg2Rad;
            var side = new Vector2(Mathf.Cos(r), Mathf.Sin(r));   // across the blade, to its right
            float sign = i == 0 ? -1f : 1f;                       // the near half is the left one
            return (_startPos + side * (PartGap * sign), _startDeg - PartSplay * sign);
        }

        /// <summary>
        /// A pose for the half held in hand <paramref name="i"/>, as a direction from ITS shoulder
        /// (x forward, y up) times the arm's reach - so the fist can always get to the grip, on any
        /// body. A plausible point off the shoulder line for a rig that cannot say.
        /// </summary>
        Vector2 HeldAt(int i, Vector2 dirTimesReach)
        {
            if (_rigHost is PrimitiveCharacterRig prim && prim.TryGetShoulders(out var front, out var back, out var reach))
                return (i == 0 ? front : back) + dirTimesReach * reach;
            return new Vector2(i == 0 ? -0.08f : 0.08f, 0.2f) + dirTimesReach * 0.32f;
        }

        // The poses, as a direction from the holding shoulder times the arm's reach. Root-local:
        // +x is the facing. The near arm's shoulder is the one behind (-x), the far arm's ahead.

        /// <summary>Wide: the near arm flung back, the far arm forward - the halves as far apart
        /// as the body allows, tips up and outward.</summary>
        static readonly Vector2 WideNear = new(-0.85f, -0.25f), WideFar = new(0.9f, -0.15f);
        const float WideLean = 55f;

        /// <summary>The X: both hands in front, the near one behind the far, each blade leaning
        /// across the other so they cross above the hands - the slash meeting in the middle.</summary>
        static readonly Vector2 CrossNear = new(0.55f, -0.55f), CrossFar = new(0.35f, -0.6f);
        const float CrossLean = 40f;

        /// <summary>High: both arms up overhead and apart, tips leaning out - the beat before the
        /// clap.</summary>
        static readonly Vector2 HighNear = new(-0.15f, 0.9f), HighFar = new(0.2f, 0.9f);
        const float HighLean = 25f;

        /// <summary>How far each half is pulled off the seam as the sword parts, world units, and
        /// how far it splays, degrees.</summary>
        const float PartGap = 0.12f, PartSplay = 18f;

        /// <summary>How far the grip is pulled up the axis before the blade turns away - most of
        /// the blade out of the sheath.</summary>
        const float DrawnOut = 0.6f;

        /// <summary>The sheath's mouth and its axis (into it), root-local - read off the worn
        /// sheath every frame, so a swinging scabbard takes the blade with it.</summary>
        (Vector2 mouth, Vector2 axis) Mouth(Half h)
        {
            if (h.Sheath == null)
                return (new Vector2(h.Far ? 0.14f : -0.14f, -0.08f), Vector2.down);   // a plausible hip
            var t = h.Sheath.transform;
            var mouth = (Vector2)_root.InverseTransformPoint(t.TransformPoint(_hang));
            var axis = ((Vector2)_root.InverseTransformDirection(t.TransformDirection(_sheathAxis))).normalized;
            return (mouth, axis);
        }

        /// <summary>
        /// Drawn at the rig's own weapon depth while it travels; going home or home, just over its
        /// own sheath - the far half behind the body with the far sheath, the near one in front.
        /// </summary>
        void SortHalf(Half h, int i)
        {
            bool atHip = _phase == Phase.Home || (_phase == Phase.Sheathe && _t > 0.55f)
                         || (_phase == Phase.Draw && _t < 0.35f);
            if (atHip && h.Sheath != null) h.Sr.sortingOrder = h.Sheath.sortingOrder + 1;
            else if (_weapon != null) h.Sr.sortingOrder = _weapon.sortingOrder - (h.Far ? 1 : 0);
        }

        /// <summary>The half cut to <paramref name="outside"/> world units of blade past the grip,
        /// trimmed from the tip end so the grip stays put - KatanaSheathe's clip.</summary>
        Sprite Clipped(Half h, float outside)
        {
            if (float.IsPositiveInfinity(outside) || h.Full == null) return h.Full;
            var full = h.Full;
            var rect = full.rect;
            float ppu = full.pixelsPerUnit;
            // Texels from the pivot to the tip end of the rect, and how many of those still show.
            float pivotY = full.pivot.y;
            float scaleY = Mathf.Abs(h.Sr.transform.localScale.y);
            int keep = Mathf.Clamp(Mathf.FloorToInt(pivotY + outside * ppu / Mathf.Max(scaleY, 0.0001f)),
                                   1, (int)rect.height);
            keep = Mathf.Min((int)rect.height, (keep + ClipStep - 1) / ClipStep * ClipStep);
            if (keep >= rect.height) return full;
            if (h.Clips.TryGetValue(keep, out var cut) && cut != null) return cut;
            cut = Sprite.Create(full.texture, new Rect(rect.x, rect.y, rect.width, keep),
                                new Vector2(full.pivot.x / rect.width, pivotY / keep), ppu, 0,
                                SpriteMeshType.FullRect);
            h.Clips[keep] = cut;
            return cut;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        /// <summary>A sprite's rotation, in degrees, for its up-vector to point along <paramref name="dir"/>.</summary>
        static float AngleOfUp(Vector2 dir) => Mathf.Atan2(-dir.x, dir.y) * Mathf.Rad2Deg;

        static float LocalAngle(Transform root, Vector3 worldUp)
            => AngleOfUp(root.InverseTransformDirection(worldUp));
    }
}
