using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// A light cycle's wall: two ribbons left behind wherever the weapon goes - a wide PURPLE one
    /// with a narrow CYAN core laid over it, the Rift Disc's own two LEDs. It lingers a moment and
    /// thins out, and it draws nothing while the weapon holds still: a TrailRenderer only lays
    /// points when it moves, which is exactly the rule a light cycle's wall follows.
    ///
    /// An EFFECT, not part of the sprite. The sketch drew the trails rising off the disc; painted
    /// on, they turned with every swing like a pair of antennae. As trails they fall behind the
    /// motion instead - in the hand, through a swing, and in flight (<see cref="Follow"/>).
    ///
    /// TWO SHAPES, by the user's rule: a disc that is not spinning (in the hand, through a melee
    /// swing) leaves a STRAIGHT wall off its handle; a spinning one (in flight) leaves LOOPS that
    /// travel along the flight line with it. In flight the emitter circles the disc's centre at the
    /// handle's own distance, turning a fixed angle per unit TRAVELLED (<see cref="LoopRatio"/>),
    /// so the loops keep one shape at any speed - out, back, orbiting - and a disc that stops
    /// draws nothing. It turns faster than the sprite does: loops need the emitter to outrun the
    /// flight (about 4x the sprite's own spin here), and a sprite spun that fast strobes. Points
    /// are laid in sub-steps between frames, or a loop drawn from one point a frame is a polygon.
    ///
    /// Held, it sorts just behind the weapon renderer it is told about, read every frame (the rig
    /// re-sorts as it moves - WeaponTrail's rule), and stops emitting while that renderer is hidden
    /// (a disc in flight leaves the hand empty; its wall goes with the thrown copy).
    /// </summary>
    public class LightCycleTrail : MonoBehaviour
    {
        const float Persistence = 0.32f;
        /// <summary>How long a point of a flight's LOOPS survives - a little shorter than the
        /// straight wall's, or a throw is followed by a row of eight loops rather than five.</summary>
        const float LoopPersistence = 0.24f;
        /// <summary>Emitter speed round the centre over the disc's speed along its path. Over 1
        /// makes loops (at 1 the loops close to cusps); 1.8 is round, open loops.</summary>
        const float LoopRatio = 1.8f;
        /// <summary>The largest angle one sub-step may turn, radians - small enough that a loop is
        /// a curve, not a polygon.</summary>
        const float LoopStep = 0.22f;
        /// <summary>Below this handle distance (units) the grip is effectively the centre, and a
        /// flight's wall stays straight.</summary>
        const float MinLoopRadius = 0.04f;
        const float WallWidth = 0.075f;
        const float CoreWidth = 0.030f;
        static readonly Color Purple = new(0.64f, 0.30f, 1.00f);
        static readonly Color Cyan = new(0.35f, 0.92f, 1.00f);

        TrailRenderer _wall, _core;
        SpriteRenderer _sortAgainst;
        int _fixedOrder = int.MinValue;

        // flight loops (see the class doc)
        bool _loops;
        float _radius, _angle;
        Vector3 _last;
        bool _hasLast;

        /// <summary>Turn the wall on or off under <paramref name="anchor"/> (a held weapon).</summary>
        public static void SetOn(Transform anchor, SpriteRenderer sortAgainst, bool on)
        {
            if (anchor == null) return;
            var t = anchor.GetComponentInChildren<LightCycleTrail>(true);
            if (!on)
            {
                if (t != null) t.gameObject.SetActive(false);
                return;
            }
            t ??= Create(anchor, sortAgainst != null ? sortAgainst.sharedMaterial : null);
            t._sortAgainst = sortAgainst;
            if (!t.gameObject.activeSelf)
            {
                t.gameObject.SetActive(true);
                t._wall.Clear();
                t._core.Clear();
            }
        }

        /// <summary>
        /// Give a FLYING disc its wall, if the hand it left carried one - thrown, orbiting or
        /// suspended - looping from the handle's distance off the centre. <paramref name="visual"/>
        /// is the flying disc's picture, centred on <paramref name="flying"/> by DiscVisual, so its
        /// own position is the handle's offset. Sorted at a fixed order: nothing in flight re-sorts.
        /// </summary>
        public static void Follow(Art.Gear.ICharacterRig rig, Transform flying, SpriteRenderer visual,
                                  int sortingOrder)
        {
            var held = rig?.WeaponAnchor != null
                ? rig.WeaponAnchor.GetComponentInChildren<LightCycleTrail>(false) : null;
            if (held == null || flying == null) return;
            var t = Create(flying, held._wall.sharedMaterial);
            t._fixedOrder = sortingOrder;

            // The handle: the sprite's pivot, which DiscVisual left at the child's own position.
            Vector2 handle = visual != null ? (Vector2)(visual.transform.position - flying.position) : Vector2.zero;
            t._radius = handle.magnitude;
            t._loops = t._radius >= MinLoopRadius;
            if (t._loops)
            {
                t._angle = Mathf.Atan2(handle.y, handle.x);        // start AT the handle
                t._wall.time = t._core.time = LoopPersistence;
            }
        }

        static LightCycleTrail Create(Transform parent, Material material)
        {
            var go = new GameObject("light-cycle-trail");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<LightCycleTrail>();
            t._wall = Ribbon(go, "wall", WallWidth, Purple, 0.85f, material);
            t._core = Ribbon(go, "core", CoreWidth, Cyan, 1f, material);
            return t;
        }

        static TrailRenderer Ribbon(GameObject host, string name, float width, Color colour, float alpha,
                                    Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(host.transform, false);
            var tr = go.AddComponent<TrailRenderer>();
            tr.autodestruct = false;
            tr.time = Persistence;
            tr.minVertexDistance = 0.015f;
            tr.numCapVertices = 0;
            tr.widthCurve = AnimationCurve.Linear(0f, width, 1f, width * 0.35f);
            var head = colour; head.a = alpha;
            var tail = colour; tail.a = 0f;
            tr.startColor = head;
            tr.endColor = tail;
            // The weapon's own material, as WeaponTrail does: a TrailRenderer on Unity's built-in
            // material renders untinted in URP.
            if (material != null) tr.sharedMaterial = material;
            return tr;
        }

        void LateUpdate()
        {
            if (_wall == null || _core == null) return;
            if (_loops) LayLoops();
            int order;
            if (_fixedOrder != int.MinValue) order = _fixedOrder;
            else if (_sortAgainst != null)
            {
                order = _sortAgainst.sortingOrder - 1;
                _wall.sortingLayerID = _core.sortingLayerID = _sortAgainst.sortingLayerID;
                bool shown = _sortAgainst.enabled && _sortAgainst.gameObject.activeInHierarchy;
                _wall.emitting = _core.emitting = shown;
            }
            else return;
            _wall.sortingOrder = order - 1;
            _core.sortingOrder = order;
        }

        /// <summary>
        /// The emitter circling the centre, advanced by distance travelled, laid in sub-steps from
        /// where the centre was last frame to where it is now - and then MOVED to the last of them.
        /// The ribbons keep EMITTING: a TrailRenderer that is not emitting draws none of the points
        /// added by hand (found by test), and emitting from the centre would add a point there
        /// every frame, spiking each loop back to the middle. Sat on the orbit, its own point is
        /// one more point of the loop.
        /// </summary>
        void LayLoops()
        {
            var c = transform.parent != null ? transform.parent.position : transform.position;
            if (!_hasLast)
            {
                _last = c;
                _hasLast = true;
                transform.position = c + new Vector3(Mathf.Cos(_angle), Mathf.Sin(_angle), 0f) * _radius;
                _wall.Clear();
                _core.Clear();
                return;
            }
            float dist = Vector2.Distance(c, _last);
            if (dist < 1e-4f)
            {
                // still: hold the emitter on the orbit, where the parent's spin can't carry it off
                transform.position = c + new Vector3(Mathf.Cos(_angle), Mathf.Sin(_angle), 0f) * _radius;
                return;
            }

            float turn = LoopRatio * dist / _radius;                // counter-clockwise, as the disc spins
            int steps = Mathf.Max(1, Mathf.CeilToInt(turn / LoopStep));
            for (int k = 1; k <= steps; k++)
            {
                var at = Vector3.Lerp(_last, c, k / (float)steps);
                _angle += turn / steps;
                var p = at + new Vector3(Mathf.Cos(_angle), Mathf.Sin(_angle), 0f) * _radius;
                _wall.AddPosition(p);
                _core.AddPosition(p);
                transform.position = p;
            }
            _last = c;
        }
    }
}
