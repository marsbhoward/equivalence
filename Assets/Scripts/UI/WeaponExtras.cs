using UnityEngine;
using UnityEngine.UI;
using Convergence.Art.Gear;

namespace Convergence.UI
{
    /// <summary>
    /// The parts of a weapon that are NOT in its sprite, drawn for the flat UI pictures of it.
    ///
    /// In the world these ride along as their own components (LumenRibbon on the rig, the rack
    /// and the armoury wall). A UI card shows one Image, so without this the Lumen's live wire -
    /// part of its silhouette, not an effect - was simply missing from the gear picker and the
    /// rack's chooser.
    ///
    /// Three so far: Lumen's live wire, Saint's halo, and the glow at Prism's lit gem. A weapon earns an entry here only when
    /// the missing part changes how it reads, not for every glow.
    /// </summary>
    public static class WeaponExtras
    {
        const string LumenWeaponId = "lumen_blade";
        const string SaintWeaponId = "saint_blade";

        public static bool Hangs(GearItem item) => item != null && item.ItemId == LumenWeaponId;
        public static bool Haloed(GearItem item) => item != null && item.ItemId == SaintWeaponId;

        /// <summary>Anything drawn round the weapon's image - it then wants a rect of its own
        /// inside the art box, so the pieces can go behind and in front of it.</summary>
        public static bool Any(GearItem item) => Hangs(item) || Haloed(item) || Gemmed(item);

        public static bool Gemmed(GearItem item) => item != null && item.HasElementGems;

        /// <summary>
        /// The glow at a gemmed weapon's LIT gem - the current attunement's (see Attunement), whose
        /// sprite <see cref="Hub.GearDisplay.Represent"/> has already swapped in. Same size, height
        /// and breathing pulse as the in-world PrismGlow, in front of the blade.
        /// </summary>
        public static void AddGemGlow(Image weaponImg, GearItem item, Sprite weapon)
        {
            if (!Gemmed(item) || weaponImg == null || weapon == null) return;
            var parent = (RectTransform)weaponImg.transform.parent;
            var r = DrawnRect(weaponImg, weapon, parent);
            float upt = r.width / weapon.rect.width;
            float ppu = weapon.pixelsPerUnit;
            var element = Attunement.Current;
            var centre = r.min + weapon.pivot * upt + new Vector2(0f, item.GemAlong(element) * ppu * upt);
            float d = 0.24f * ppu * upt;                                    // PrismGlow's own size

            var glow = Ring(parent, "gem.glow", Core.Spr.Glow, centre, d);
            glow.transform.SetSiblingIndex(weaponImg.transform.GetSiblingIndex() + 1);
            glow.gameObject.AddComponent<GemPulse>().Tint = Core.ElementInfo.Tint(element);
        }

        /// <summary>PrismGlow's breathing, on unscaled time.</summary>
        class GemPulse : MonoBehaviour
        {
            public Color Tint;
            Image _img;

            void Update()
            {
                if (_img == null) _img = GetComponent<Image>();
                float pulse = 0.5f + 0.4f * Mathf.Sin(Time.unscaledTime * 2.2f);
                _img.color = new Color(Tint.r, Tint.g, Tint.b, pulse);
            }
        }

        /// <summary>
        /// Saint's halo round a weapon image already laid out: the far half behind the image, the
        /// near half in front, the same two sprites and the same size and height up the blade as
        /// the in-world halo (<see cref="DemoGear.SaintHaloSize"/>, <see cref="DemoGear.SaintHaloAlong"/>),
        /// so the card and the hand agree.
        /// </summary>
        public static void AddHalo(Image weaponImg, GearItem item, Sprite weapon)
        {
            if (!Haloed(item) || weaponImg == null || weapon == null) return;
            var parent = (RectTransform)weaponImg.transform.parent;
            var r = DrawnRect(weaponImg, weapon, parent);
            float upt = r.width / weapon.rect.width;                       // parent units per texel
            float ppu = weapon.pixelsPerUnit;
            var pivot = r.min + weapon.pivot * upt;
            var centre = pivot + new Vector2(0f, DemoGear.SaintHaloAlong * ppu * upt);
            float d = DemoGear.SaintHaloSize * ppu * upt;

            int at = weaponImg.transform.GetSiblingIndex();
            var far = Ring(parent, "halo.far", Core.Spr.HaloRing, centre, d);
            far.transform.SetSiblingIndex(at);                              // behind the blade
            var near = Ring(parent, "halo.near", Core.Spr.HalfRing, centre, d);
            near.transform.SetSiblingIndex(weaponImg.transform.GetSiblingIndex() + 1);   // in front
            far.gameObject.AddComponent<HaloPulse>().Init(far, near);
        }

        static Image Ring(RectTransform parent, string name, Sprite sprite, Vector2 centre, float d)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(d, d);
            rt.anchoredPosition = centre;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// Where an image actually draws its sprite, in <paramref name="parent"/>'s local space
        /// measured from the parent's centre - its rect, narrowed to the sprite's aspect if it
        /// preserves one.
        /// </summary>
        static Rect DrawnRect(Image img, Sprite sprite, RectTransform parent)
        {
            var c = new Vector3[4];
            img.rectTransform.GetWorldCorners(c);
            Vector2 bl = parent.InverseTransformPoint(c[0]), tr = parent.InverseTransformPoint(c[2]);
            // InverseTransformPoint answers relative to the parent's PIVOT; move to its centre,
            // which is what an anchor of (0.5, 0.5) measures from.
            var shift = (new Vector2(0.5f, 0.5f) - parent.pivot) * parent.rect.size;
            var r = Rect.MinMaxRect(bl.x - shift.x, bl.y - shift.y, tr.x - shift.x, tr.y - shift.y);
            if (!img.preserveAspect) return r;
            float aspect = sprite.rect.width / sprite.rect.height;
            float w = Mathf.Min(r.width, r.height * aspect), h = w / aspect;
            return new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
        }

        /// <summary>The in-world halo's tilt and shimmer (see SaintHalo), on unscaled time.</summary>
        class HaloPulse : MonoBehaviour
        {
            static readonly Color Gold = new(0.98f, 0.83f, 0.42f);
            Image _far, _near;

            public void Init(Image far, Image near) { _far = far; _near = near; }

            void Update()
            {
                if (_far == null || _near == null) return;
                float t = Time.unscaledTime;
                float squash = Mathf.Lerp(0.40f, 1f, Mathf.Abs(Mathf.Sin(t * 0.55f)));
                var scale = new Vector3(1f, squash, 1f);
                _far.rectTransform.localScale = scale;
                _near.rectTransform.localScale = scale;
                float lit = 0.80f + 0.20f * Mathf.Sin(t * 2.1f);
                _far.color = new Color(Gold.r, Gold.g, Gold.b, lit * 0.85f);
                _near.color = new Color(Gold.r, Gold.g, Gold.b, lit);
            }
        }

        /// <summary>How far the wire hangs below the pommel, in texels of <paramref name="weapon"/>.</summary>
        public static float HangTexels(GearItem item, Sprite weapon)
        {
            if (!Hangs(item) || weapon == null) return 0f;
            var wire = DemoGear.LumenWireFrames[0];
            return wire.pivot.y * weapon.pixelsPerUnit / wire.pixelsPerUnit;
        }

        /// <summary>
        /// Hang the wire from the pommel of a weapon image. <paramref name="pommel"/> is the
        /// bottom-centre of the weapon's rect in <paramref name="parent"/>'s local space, and
        /// <paramref name="unitsPerTexel"/> how many of those units one texel of the weapon covers.
        /// </summary>
        public static void AddHanging(RectTransform parent, GearItem item, Sprite weapon,
                                      Vector2 pommel, float unitsPerTexel)
        {
            if (!Hangs(item) || weapon == null) return;
            var frames = DemoGear.LumenWireFrames;
            var wire = frames[0];
            float s = unitsPerTexel * weapon.pixelsPerUnit / wire.pixelsPerUnit;

            var go = new GameObject("wire", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(wire.pivot.x / wire.rect.width, wire.pivot.y / wire.rect.height);
            rt.sizeDelta = new Vector2(wire.rect.width * s, wire.rect.height * s);
            rt.anchoredPosition = pommel;

            var img = go.AddComponent<Image>();
            img.sprite = wire;
            img.raycastTarget = false;
            go.AddComponent<Sparks>().Frames = frames;
        }

        /// <summary>
        /// Lay a weapon image and its hanging parts out together inside a box, centred - what
        /// preserveAspect does for the sword alone, with the wire counted in.
        /// </summary>
        public static void FitWithHanging(RectTransform area, Image img, GearItem item, Sprite weapon,
                                          Vector2 box)
        {
            float hang = HangTexels(item, weapon);
            var r = weapon.rect;
            float s = Mathf.Min(box.x / r.width, box.y / (r.height + hang));
            float h = r.height * s, total = (r.height + hang) * s;

            img.preserveAspect = false;
            var irt = img.rectTransform;
            irt.anchorMin = irt.anchorMax = irt.pivot = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(r.width * s, h);
            irt.anchoredPosition = new Vector2(0f, total * 0.5f - h * 0.5f);

            AddHanging(area, item, weapon, new Vector2(0f, total * 0.5f - h), s);
        }

        /// <summary>The sparks, on UNSCALED time - every screen this appears on pauses the game.</summary>
        class Sparks : MonoBehaviour
        {
            public Sprite[] Frames;
            Image _img;
            float _next;

            void Update()
            {
                if (Frames == null || Frames.Length < 2) return;
                if (_img == null) _img = GetComponent<Image>();
                if (Time.unscaledTime < _next) return;
                _img.sprite = Frames[Random.Range(0, Frames.Length)];
                _next = Time.unscaledTime + Random.Range(0.05f, 0.16f);
            }
        }
    }
}
