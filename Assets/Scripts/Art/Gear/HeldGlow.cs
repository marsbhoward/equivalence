using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// A weapon that GLOWS on whoever holds it (<see cref="GearItem.GlowColor"/>): a soft 2D point
    /// light at the weapon's centre, lifting the character and the floor round them. Held in
    /// either hand, on a display, and in flight (<see cref="Follow"/>), the same shape
    /// LightningArcs has.
    ///
    /// ADDITIVE, not multiply. A multiply light scales a sprite's own colours, and this game's
    /// floors, cloth and undersuit are dark - at 1.5 it still barely registered on screen. The
    /// additive style adds the glow's colour on top, which is what reads as a glow on a dark
    /// scene; 0.3 is soft, 0.5 already fogs the character.
    ///
    /// FROM THE RIM, optionally (<see cref="GearItem.GlowFromRim"/>): the light's shape is a ring -
    /// dark over the weapon's middle, brightest at its edge, fading out past it - so a disc whose
    /// light is its ring does not also glow from its hub. Singularity: a black hole's core gives no
    /// light, and a centred glow greyed it.
    ///
    /// THE NEUTRAL GLOBAL LIGHT. URP's 2D renderer draws a lit sprite as its own colour only while
    /// the scene has NO 2D lights; the moment one exists, every lit sprite is its colour times the
    /// sum of the multiply-style lights present (CombinedShapeLightShared) - so a point light on
    /// its own turns the rest of the scene black. <see cref="EnsureNeutralGlobal"/> puts in a
    /// white global light at intensity 1 first, which multiplies everything by exactly 1:
    /// captured with and without it, the static scene is pixel-identical. The point light then
    /// adds on top.
    ///
    /// The radius is measured off the weapon's own sprite (so it rides the hub's 4/3 scale, a
    /// display's fit and a throw's scale), and the light sits on the sprite's CENTRE, placed there
    /// each frame - the anchor is the grip, which on most weapons is not the middle.
    /// </summary>
    public class HeldGlow : MonoBehaviour
    {
        /// <summary>Light radius as a multiple of the weapon's own height - out past the torso and
        /// onto the floor at the character's feet, not across the room.</summary>
        const float RadiusPerSize = 1.6f;

        SpriteRenderer _source;
        Light2D _light;

        static Light2D _global;

        /// <summary>Turn the glow on or off under <paramref name="anchor"/>, lighting from
        /// <paramref name="source"/> - a held weapon (either hand) or a display's.</summary>
        public static void SetOn(Transform anchor, SpriteRenderer source, GearItem item)
        {
            if (anchor == null) return;
            var g = anchor.GetComponentInChildren<HeldGlow>(true);
            bool on = item != null && item.GlowIntensity > 0f;
            if (!on)
            {
                if (g != null) g.gameObject.SetActive(false);
                return;
            }
            if (g == null) g = Create(anchor);
            g._source = source;
            g.Configure(item);
            if (!g.gameObject.activeSelf) g.gameObject.SetActive(true);
        }

        /// <summary>Give a FLYING weapon its glow, if the hand it left carried one - the same
        /// light, copied off the held one, so the flight code needs no item.</summary>
        public static void Follow(ICharacterRig rig, Transform flying, SpriteRenderer visual)
        {
            var held = rig?.WeaponAnchor != null
                ? rig.WeaponAnchor.GetComponentInChildren<HeldGlow>(false) : null;
            if (held == null || held._light == null || flying == null || visual == null) return;
            var g = Create(flying);
            g._source = visual;
            g._light.color = held._light.color;
            g._light.intensity = held._light.intensity;
            g._light.lightCookieSprite = held._light.lightCookieSprite;
        }

        static HeldGlow Create(Transform parent)
        {
            var go = new GameObject("held-glow");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<HeldGlow>();
            g._light = go.AddComponent<Light2D>();
            g._light.lightType = Light2D.LightType.Point;
            g._light.blendStyleIndex = 1;          // additive - see the class doc
            g._light.shadowsEnabled = false;
            g._light.pointLightInnerRadius = 0f;
            g._light.falloffIntensity = 0.75f;     // soft all the way out
            return g;
        }

        void Configure(GearItem item)
        {
            if (_light == null) _light = GetComponent<Light2D>();
            var c = item.GlowColor;
            c.a = 1f;
            _light.color = c;
            _light.intensity = item.GlowIntensity;
            _light.lightCookieSprite = item.GlowFromRim ? RimCookie : null;
        }

        static Sprite _rimCookie;
        /// <summary>The ring-shaped light: dark over the middle (a third of the weapon's radius),
        /// rising to its peak just inside the weapon's edge, then falling away softly to nothing at
        /// the light's own radius. Measured against RadiusPerSize, so the peak stays on the rim.
        /// Built once; bilinear, because it is light, not pixel art.</summary>
        static Sprite RimCookie => _rimCookie != null ? _rimCookie : (_rimCookie = BuildRimCookie());

        static Sprite BuildRimCookie()
        {
            const int n = 64;
            float rim = 0.5f / RadiusPerSize;                // the weapon's edge, in the light's radius
            float hollow = rim * 0.3f, peak = rim * 0.85f;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float r = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f).magnitude / (n / 2f);
                float rise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hollow, peak, r));
                float fall = r <= peak ? 1f : Mathf.Pow(1f - Mathf.InverseLerp(peak, 1f, r), 2f);
                float v = rise * fall;
                px[y * n + x] = new Color(v, v, v, v);
            }
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        }

        /// <summary>
        /// The white global light at intensity 1 every other light adds to - see the class doc.
        /// Found or made once; a scene that already has a global 2D light keeps its own.
        /// </summary>
        public static void EnsureNeutralGlobal()
        {
            if (_global != null) return;
            foreach (var l in FindObjectsByType<Light2D>(FindObjectsInactive.Exclude))
                if (l.lightType == Light2D.LightType.Global) { _global = l; return; }
            var go = new GameObject("Global Light 2D (neutral)");
            _global = go.AddComponent<Light2D>();
            _global.lightType = Light2D.LightType.Global;
            _global.blendStyleIndex = 0;
            _global.color = Color.white;
            _global.intensity = 1f;
        }

        void OnEnable() => EnsureNeutralGlobal();

        void LateUpdate()
        {
            if (_light == null) return;
            var src = _source;
            bool shown = src != null && src.enabled && src.gameObject.activeInHierarchy && src.sprite != null;
            _light.enabled = shown;
            if (!shown) return;
            EnsureNeutralGlobal();                  // a teardown can take it with it

            transform.position = src.bounds.center;
            float size = src.sprite.bounds.size.y * Mathf.Abs(src.transform.lossyScale.y);
            _light.pointLightOuterRadius = Mathf.Max(0.05f, size * RadiusPerSize);
        }
    }
}
