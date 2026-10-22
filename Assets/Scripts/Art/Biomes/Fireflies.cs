using UnityEngine;

namespace Convergence.Art.Biomes
{
    /// <summary>
    /// The forest's fireflies: a few motes drifting over the floor, each a two-texel spark and a
    /// tiny <see cref="Lighting.Glow"/>, blinking on its own slow beat. Purely a picture; built with
    /// the forest's scenery (Scenery.BuildForest) and destroyed with it.
    /// </summary>
    public class Fireflies : MonoBehaviour
    {
        [SerializeField] Transform[] _motes;
        [SerializeField] Vector2[] _home;
        [SerializeField] float[] _phase;
        [SerializeField] SpriteRenderer[] _spark;
        [SerializeField] Lighting.Glow[] _glow;

        static Sprite _dot;

        public static void Spawn(Transform parent, Vector2 half, int count, int seed)
        {
            var go = new GameObject("fireflies");
            go.transform.SetParent(parent, false);
            var f = go.AddComponent<Fireflies>();
            var rng = new System.Random(seed);
            f._motes = new Transform[count];
            f._home = new Vector2[count];
            f._phase = new float[count];
            f._spark = new SpriteRenderer[count];
            f._glow = new Lighting.Glow[count];
            for (int i = 0; i < count; i++)
            {
                var m = new GameObject("mote");
                m.transform.SetParent(go.transform, false);
                f._home[i] = new Vector2((float)(rng.NextDouble() * 2 - 1) * (half.x - 1f), (float)(rng.NextDouble() * 2 - 1) * (half.y - 1f));
                f._phase[i] = (float)rng.NextDouble() * 20f;
                m.transform.localPosition = f._home[i];
                var sr = m.AddComponent<SpriteRenderer>();
                sr.sprite = Dot;
                sr.color = Core.Tuning.Biomes.FireflyColor;
                // over everything that stands, under the readouts - a firefly is never in shadow
                sr.sortingOrder = Core.SortingOrders.StatusOverlay - 5;
                f._spark[i] = sr;
                f._glow[i] = Lighting.Glow.Attach(m, Core.Tuning.Biomes.FireflyRadius, Core.Tuning.Biomes.FireflyColor, 0f);
                f._motes[i] = m.transform;
            }
        }

        static Sprite Dot
        {
            get
            {
                if (_dot != null) return _dot;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();
                _dot = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), FloorArt.PixelsPerUnit);
                _dot.hideFlags = HideFlags.HideAndDontSave;
                return _dot;
            }
        }

        void Update()
        {
            if (_motes == null) return;
            float t = Time.time;
            for (int i = 0; i < _motes.Length; i++)
            {
                if (_motes[i] == null) continue;
                float p = _phase[i];
                var drift = new Vector2(Mathf.Sin(t * 0.31f + p) * 0.9f + Mathf.Sin(t * 0.73f + p * 2f) * 0.3f,
                                        Mathf.Cos(t * 0.27f + p * 1.3f) * 0.6f + Mathf.Sin(t * 0.9f + p) * 0.15f);
                _motes[i].localPosition = _home[i] + drift;
                // a slow blink: mostly dark, glowing a few seconds in every ten
                float on = Mathf.Clamp01(Mathf.Sin(t * 0.6f + p) * 2.2f - 1.2f);
                var c = Core.Tuning.Biomes.FireflyColor; c.a = on;
                if (_spark[i] != null) _spark[i].color = c;
                if (_glow[i] != null) _glow[i].Intensity = on * Core.Tuning.Biomes.FireflyIntensity;
            }
        }
    }
}
