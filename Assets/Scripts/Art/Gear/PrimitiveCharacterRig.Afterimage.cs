using System.Collections.Generic;
using UnityEngine;

namespace Convergence.Art.Gear
{
    /// <summary>Penumbra's afterimages (ArmPieces.AfterimageEvery), shared by the flat rig's arm parts and the
    /// eight-direction picture: copies dropped into the world, each fading on its own (AfterimageFade).</summary>
    public partial class PrimitiveCharacterRig
    {
        List<AfterimageFade> _afterimages;
        static Transform _afterimageRoot;

        /// <summary>A copy of <paramref name="sprite"/> left where <paramref name="at"/> is now, under the figure.</summary>
        void DropAfterimage(Sprite sprite, Transform at, bool flipX, int sortingLayer, int sortingOrder)
        {
            if (sprite == null || at == null) return;
            if (_afterimageRoot == null) _afterimageRoot = new GameObject("Afterimages").transform;
            _afterimages ??= new List<AfterimageFade>();
            AfterimageFade copy = null;
            foreach (var a in _afterimages) if (a != null && !a.enabled) { copy = a; break; }
            if (copy == null)
            {
                var go = new GameObject("afterimage");
                go.transform.SetParent(_afterimageRoot, false);
                copy = go.AddComponent<AfterimageFade>();
                _afterimages.Add(copy);
            }
            var t = copy.transform;
            t.SetPositionAndRotation(at.position, at.rotation);
            t.localScale = at.lossyScale;
            var sr = copy.Renderer;
            sr.sprite = sprite;
            sr.flipX = flipX;
            sr.sortingLayerID = sortingLayer;
            sr.sortingOrder = sortingOrder;
            copy.Begin();
        }

        void OnDestroy()
        {
            if (_afterimages == null) return;
            foreach (var a in _afterimages) if (a != null) Destroy(a.gameObject);
            _afterimages = null;
        }

        // ---------------------------------------------------------------- the flat rig's arm parts

        float _ghostAt;
        Vector3 _ghostHandAt;
        static Dictionary<Sprite, Sprite> _violet;

        /// <summary>A part's picture on Shadow's violet (ArmPieces.Afterimage), kept per picture.</summary>
        static Sprite Violet(Sprite src)
        {
            if (src == null) return null;
            _violet ??= new Dictionary<Sprite, Sprite>();
            if (_violet.TryGetValue(src, out var v) && v != null) return v;
            if (_violet.Count > 96) _violet.Clear();   // the arm redraws at the accessory rate: old pictures go
            var st = src.texture;
            if (st == null || !st.isReadable) return null;
            var r = src.rect;
            var px = st.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
            for (int i = 0; i < px.Length; i++) if (px[i].a > 0f) px[i] = ArmPieces.Afterimage(px[i]);
            var tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false)
                { filterMode = st.filterMode, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixels(px);
            tex.Apply();
            v = Sprite.Create(tex, new Rect(0, 0, r.width, r.height), new Vector2(src.pivot.x / r.width, src.pivot.y / r.height), src.pixelsPerUnit);
            return _violet[src] = v;
        }

        /// <summary>What an afterimage copies on the flat rig: the arm's parts and the weapon in its hand.</summary>
        static readonly RigLayer[] AfterimageLayers = { RigLayer.GlovesFront, RigLayer.GlovesFrontLower, RigLayer.GlovesFrontHand, RigLayer.Shoulders, RigLayer.Weapon };

        /// <summary>FollowArmPiece's part (the flat rig): through a weapon art, each of Penumbra's arm parts drops a
        /// violet copy every AfterimageEvery, once the hand has moved on AfterimageMoved texels.</summary>
        void DropArmAfterimages()
        {
            if (_armPiece != AccessoryKind.PenumbraArm || Body8On || Player == null || !Player.ArtInMotion) return;
            if (Time.time - _ghostAt < ArmPieces.AfterimageEvery) return;
            if (!_layers.TryGetValue(RigLayer.GlovesFrontHand, out var hand) || hand == null) return;
            if ((hand.transform.position - _ghostHandAt).magnitude * Proportions.BodyPpu < ArmPieces.AfterimageMoved) return;
            _ghostAt = Time.time; _ghostHandAt = hand.transform.position;
            foreach (var layer in AfterimageLayers)
            {
                if (!_layers.TryGetValue(layer, out var sr) || sr == null || !sr.enabled) continue;
                DropAfterimage(Violet(sr.sprite), sr.transform, sr.flipX, sr.sortingLayerID, sr.sortingOrder - 1);
            }
        }
    }
}
