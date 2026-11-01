using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Core
{
    /// <summary>
    /// The north wall's collider, with a doorway left open wherever a door in the wall stands open
    /// (<see cref="Arena.OpenNorthGap"/>). The exit is cut INTO the face and walked through (the
    /// user's call, 2026-11-01), so the wall has to let a body step into the opening - and only
    /// there, and only while that door is open. A plug behind each opening keeps the room closed
    /// even if the walk-through never fires.
    ///
    /// Polls <see cref="Arena.NorthGapVersion"/> rather than hearing an event: a domain reload
    /// drops subscribers but re-runs every door's OnEnable, which bumps the version again.
    /// </summary>
    public class NorthWall : MonoBehaviour
    {
        [SerializeField] float _halfWidth, _thickness;
        int _built = -1;
        readonly List<BoxCollider2D> _boxes = new();

        /// <summary>The wall along y = <paramref name="y"/> (its centre line), spanning
        /// ±<paramref name="halfWidth"/>.</summary>
        public static NorthWall Build(Transform parent, float y, float halfWidth, float thickness)
        {
            var go = new GameObject("wall.n");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            var w = go.AddComponent<NorthWall>();
            w._halfWidth = halfWidth;
            w._thickness = thickness;
            w.Rebuild();
            return w;
        }

        void Update()
        {
            if (_built != Arena.NorthGapVersion) Rebuild();
        }

        void Rebuild()
        {
            _built = Arena.NorthGapVersion;
            foreach (var b in GetComponents<BoxCollider2D>()) Destroy(b);
            _boxes.Clear();

            var gaps = new List<float>(Arena.NorthGaps);
            gaps.Sort();
            float g = Tuning.Door.GapHalfWidth;
            float x = -_halfWidth;
            foreach (float c in gaps)
            {
                float a = Mathf.Max(-_halfWidth, c - g), b = Mathf.Min(_halfWidth, c + g);
                if (a > x) Box(x, a, 0f, _thickness);
                // The plug behind the opening: a body can stand IN the doorway, never past it.
                Box(a, b, _thickness * 0.5f + 0.7f, 0.4f);
                x = Mathf.Max(x, b);
            }
            if (x < _halfWidth) Box(x, _halfWidth, 0f, _thickness);
            Enemies.NavField.MarkDirty();
        }

        void Box(float x0, float x1, float y, float h)
        {
            var box = gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(x1 - x0, h);
            box.offset = new Vector2((x0 + x1) * 0.5f, y);
            _boxes.Add(box);
        }
    }
}
