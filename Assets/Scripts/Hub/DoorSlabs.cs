using UnityEngine;
using Convergence.Core;

namespace Convergence.Hub
{
    /// <summary>
    /// The sigil door's leaves as STONE SLABS (the user's pick S2, 2026-11-03, mock
    /// https://claude.ai/artifact/SL8tfxKDxAUTxHADDRkHK6): each leaf shows its free edge and its top
    /// face, turned with the swing. Lit from the room's upper left (L2): the left leaf's face turns
    /// toward the light and its edge away; the right leaf the other way (the faces' shades are set
    /// by the doors' own Slab, <see cref="Tuning.Hub.DoorLeafLit"/>/<see cref="Tuning.Hub.DoorLeafShade"/>).
    ///
    /// The slab's depth, swung with the leaf by t, points toward the opening by d sin t and back into
    /// the wall by d cos t - up the screen under the camera's tilt (<see cref="Tuning.Hub.DoorSwingDrop"/>),
    /// the same rule the hinge's drop follows. A transform cannot draw that slanted quad, so each face
    /// is a four-corner mesh in the door's own frame, re-posed whenever the hinges are
    /// (<see cref="Pose"/>). Shared by the hub's SigilDoor and the arena's FloorDoor.
    /// </summary>
    public class DoorSlabs : MonoBehaviour
    {
        [SerializeField] Transform _left, _right;
        /// <summary>Left edge, left top, right edge, right top.</summary>
        [SerializeField] MeshFilter[] _faces;
        [SerializeField] float _w, _h;

        // Not readonly - see the domain reload rules; re-made if found null.
        static Vector3[] _corners;

        static readonly Color EdgeShade = new(0.15f, 0.155f, 0.19f);
        static readonly Color EdgeLit = new(0.40f, 0.41f, 0.47f);
        static readonly Color TopLit = new(0.52f, 0.53f, 0.59f);
        static readonly Color TopShade = new(0.44f, 0.45f, 0.51f);

        /// <summary>Gives <paramref name="door"/>'s two leaves (seam at their origins, <paramref name="w"/>
        /// wide, <paramref name="h"/> tall) their slab faces, drawn with the leaves' own
        /// <paramref name="material"/> on the slabs' sorting order.</summary>
        public static DoorSlabs Attach(GameObject door, Transform left, Transform right, float w, float h, Material material)
        {
            var s = door.AddComponent<DoorSlabs>();
            s._left = left;
            s._right = right;
            s._w = w;
            s._h = h;
            s._faces = new MeshFilter[4];
            var colours = new[] { EdgeShade, TopLit, EdgeLit, TopShade };
            for (int i = 0; i < 4; i++)
            {
                var go = new GameObject(i % 2 == 0 ? "slab-edge" : "slab-top");
                go.transform.SetParent(door.transform, false);
                var mesh = new Mesh { name = go.name };
                mesh.vertices = new Vector3[4];
                mesh.colors = new[] { colours[i], colours[i], colours[i], colours[i] };
                mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                // Both windings: the quad turns over between the two leaves.
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 };
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = material;
                mr.sortingOrder = SortingOrders.GroundDecal + 3;
                var block = new MaterialPropertyBlock();
                block.SetTexture("_MainTex", Texture2D.whiteTexture);
                mr.SetPropertyBlock(block);
                s._faces[i] = mf;
            }
            s.Pose(0f);
            return s;
        }

        /// <summary>Lays the faces on the leaves as they hang at <paramref name="degrees"/> - call after
        /// SigilDoor.SwingHinge has posed them.</summary>
        public void Pose(float degrees)
        {
            if (_faces == null || _faces.Length < 4 || _left == null || _right == null) return;
            float t = degrees * Mathf.Deg2Rad;
            float d = Tuning.Hub.DoorSlabThickness;
            // Shut, the top face sits under the lintel: it comes out with the swing.
            float emerge = Mathf.Clamp01(Mathf.Sin(t) / Mathf.Sin(Tuning.Hub.DoorSwingDegrees * Mathf.Deg2Rad));
            for (int i = 0; i < 2; i++)
            {
                var leaf = i == 0 ? _left : _right;
                int side = i == 0 ? -1 : 1;
                var bottom = At(leaf, 0f, 0f);
                var top = At(leaf, 0f, _h);
                var hinge = At(leaf, side * _w, _h);
                var o = new Vector3(-side * d * Mathf.Sin(t), d * Mathf.Cos(t) * Tuning.Hub.DoorSwingDrop * emerge, 0f);
                Set(_faces[i * 2], bottom, top, top + o, bottom + o);
                Set(_faces[i * 2 + 1], hinge, top, top + o, hinge + o);
            }
        }

        Vector3 At(Transform leaf, float x, float y) => transform.InverseTransformPoint(leaf.TransformPoint(new Vector3(x, y, 0f)));

        static void Set(MeshFilter mf, Vector3 a, Vector3 b, Vector3 c, Vector3 e)
        {
            if (mf == null || mf.sharedMesh == null) return;
            _corners ??= new Vector3[4];
            _corners[0] = a; _corners[1] = b; _corners[2] = c; _corners[3] = e;
            mf.sharedMesh.vertices = _corners;
            mf.sharedMesh.RecalculateBounds();
        }
    }
}
