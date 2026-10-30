using System.Collections.Generic;
using UnityEngine;
using Convergence.Art.Gear;
using Convergence.Chain;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The pictures on SALT's option cells: the character's own head (or head and shoulders) with that one option
    /// applied, so a hairstyle, a beard, an expression or a mark is chosen by sight rather than by name.
    ///
    /// A REAL RIG, not the head grid on its own. A long style's length is the mane, drawn on its own layer behind
    /// the body, and a picture of the head alone showed every long style as the same short cap. The rig is
    /// parked away from the preview's and drawn bare (no gear, helm hidden): a picture of a hairstyle under a
    /// hood is not a picture of the hairstyle.
    ///
    /// Rendered a few a frame, from the frame after the rig is built (it poses itself in its own Update), into
    /// one small render texture per picture, kept until the screen closes. Keyed by the look drawn, so a hair
    /// colour change draws the hairstyles again and choosing one of them redraws nothing.
    /// </summary>
    public class SaltThumbs
    {
        const float Origin = 2000f;
        const int Size = 128, PerFrame = 6;

        GameObject _holder;
        Camera _cam;
        ICharacterRig _rig;
        int _readyFrame;

        // Not readonly - a domain reload during play hands these back empty or null (ENGINEERING.md).
        Dictionary<string, RenderTexture> _done = new();
        Queue<(string Key, Appearance Look, bool Bust)> _queue = new();

        public void Build(ElementType element)
        {
            if (_holder != null) return;
            _holder = new GameObject("salt-thumbs");
            _holder.transform.position = new Vector3(Origin, Origin, 0f);
            _rig = CharacterRigFactory.Build(_holder, element, 0);
            _rig.SetShowcasePose(true);
            _rig.SetDetailArt(true);
            _rig.Apply(new Loadout());
            _rig.SetHelmHidden(true);

            var camGo = new GameObject("salt-thumbs-camera");
            camGo.transform.position = new Vector3(Origin, Origin, -10f);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.075f, 0.085f, 0.115f, 1f);
            _cam.cullingMask = ~0;
            _cam.enabled = false;   // drawn by hand, one picture at a time
            _readyFrame = Time.frameCount + 1;
        }

        /// <summary>The picture of <paramref name="look"/> - head and shoulders when <paramref name="bust"/> -
        /// handed back at once and drawn into over the next frames.</summary>
        public RenderTexture Get(Appearance look, bool bust)
        {
            _done ??= new Dictionary<string, RenderTexture>();
            _queue ??= new Queue<(string, Appearance, bool)>();
            string key = Art.BodyLook.HeadKey(look) + (bust ? ".bust" : "");
            if (_done.TryGetValue(key, out var rt) && rt != null) return rt;

            rt = new RenderTexture(Size, Size, 16)
            {
                name = "salt-thumb",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            // Blank until drawn, not whatever the GPU last left there.
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, _cam != null ? _cam.backgroundColor : Color.black);
            RenderTexture.active = prev;

            _done[key] = rt;
            _queue.Enqueue((key, look, bust));
            return rt;
        }

        /// <summary>Draw the next few waiting pictures. Called from the screen's Update.</summary>
        public void Tick()
        {
            if (_rig == null || _cam == null || _queue == null || Time.frameCount < _readyFrame) return;
            for (int n = 0; n < PerFrame && _queue.Count > 0; n++)
            {
                var (key, look, bust) = _queue.Dequeue();
                if (_done == null || !_done.TryGetValue(key, out var rt) || rt == null) continue;
                Draw(look, bust, rt);
            }
        }

        void Draw(Appearance look, bool bust, RenderTexture rt)
        {
            _rig.SetAppearance(look);
            if (!HeadBounds(out var head)) return;

            // FACE frames the head; BUST hangs a little below it as well, for a beard on the collar - the
            // head's bounds already hold the mane. Square, centred, a margin round it.
            var region = head;
            if (bust) region.SetMinMax(new Vector3(head.min.x, head.min.y - head.size.y * 0.3f, head.min.z), head.max);
            float half = Mathf.Max(region.extents.x, region.extents.y) * 1.06f;

            _cam.orthographicSize = half;
            _cam.transform.position = new Vector3(region.center.x, region.center.y, _cam.transform.position.z);
            _cam.targetTexture = rt;
            _cam.Render();
            _cam.targetTexture = null;
        }

        /// <summary>Everything drawn off the head pivot - skin, hair, the mane (CharacterPreview's rule).</summary>
        bool HeadBounds(out Bounds bounds)
        {
            bounds = default;
            var head = Find(_rig.Transform, "head");
            if (head == null) return false;
            bool any = false;
            foreach (var sr in head.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr == null || !sr.enabled || sr.sprite == null) continue;
                if (any) bounds.Encapsulate(sr.bounds); else { bounds = sr.bounds; any = true; }
            }
            return any;
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var hit = Find(c, name);
                if (hit != null) return hit;
            }
            return null;
        }

        public void Dispose()
        {
            if (_done != null)
                foreach (var rt in _done.Values)
                    if (rt != null) { rt.Release(); Object.Destroy(rt); }
            _done?.Clear();
            _queue?.Clear();
            if (_holder != null) Object.Destroy(_holder);
            if (_cam != null) Object.Destroy(_cam.gameObject);
            _holder = null; _cam = null; _rig = null;
        }
    }
}
