using UnityEngine;

namespace Convergence.Hub
{
    /// <summary>
    /// PLACEHOLDER art so the showcase and the trophies can be built and judged before any wallet
    /// exists. Exactly the same scaffolding role as GameBootstrap.EnsureStarterGear.
    ///
    /// It is not pretending to be a wallet: it invents pictures. When a real
    /// <see cref="IShowcaseSource"/> arrives, assigning it to <see cref="Showcase.Source"/> is
    /// the whole switchover and this file can be deleted. Enable it from
    /// <see cref="Core.Tuning.Testing.DemoShowcase"/>.
    /// </summary>
    public class DevShowcaseSource : IShowcaseSource
    {
        readonly ShowcaseItem[] _items;

        public DevShowcaseSource(int count)
        {
            _items = new ShowcaseItem[count];
            for (int i = 0; i < count; i++)
            {
                _items[i] = new ShowcaseItem
                {
                    Key = $"demo-{i:00}",
                    Art = Generate(i),
                    Title = Titles[i % Titles.Length],
                    Collection = "Placeholder Collection",
                };
            }
        }

        static readonly string[] Titles =
        {
            "Ashfall #114", "Tidewalker #7", "Stonewake #62", "Zephyr #201",
            "Emberkin #33", "Deepcurrent #88", "Rootbound #19", "Skysplit #5",
        };

        public int Count => _items.Length;

        public bool TryGet(int index, out ShowcaseItem item)
        {
            if (index < 0 || index >= _items.Length) { item = default; return false; }
            item = _items[index];
            return true;
        }

        public bool TryGetByKey(string key, out ShowcaseItem item)
        {
            foreach (var i in _items)
            {
                if (i.Key != key) continue;
                item = i;
                return true;
            }
            item = default;
            return false;
        }

        /// <summary>
        /// A deterministic little picture per index. Deliberately NOT square for some of them -
        /// real collection art is not uniformly 1:1, and the frames and the trophy both have to
        /// prove they preserve aspect rather than stretching somebody's picture.
        /// </summary>
        static Sprite Generate(int seed)
        {
            var rng = new System.Random(seed * 7919 + 13);
            int w = 96, h = seed % 3 == 0 ? 128 : seed % 3 == 1 ? 96 : 72;

            var bg = Color.HSVToRGB((float)rng.NextDouble(), 0.35f, 0.22f);
            var a = Color.HSVToRGB((float)rng.NextDouble(), 0.72f, 0.92f);
            var b = Color.HSVToRGB((float)rng.NextDouble(), 0.65f, 0.75f);

            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            int bands = 3 + rng.Next(4);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)w - 0.5f, ny = y / (float)h - 0.5f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float band = Mathf.Repeat(d * bands, 1f);
                if (band < 0.42f) px[y * w + x] = Color.Lerp(a, b, ny + 0.5f);
                else if (band < 0.5f) px[y * w + x] = Color.Lerp(bg, Color.black, 0.4f);
            }

            // A border, so a picture on a dark wall still has an edge.
            for (int x = 0; x < w; x++) { px[x] = Color.black; px[(h - 1) * w + x] = Color.black; }
            for (int y = 0; y < h; y++) { px[y * w] = Color.black; px[y * w + w - 1] = Color.black; }

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            tex.SetPixels(px);
            tex.Apply();

            // PPU = height, so every generated piece is exactly 1 world unit TALL and its width
            // follows its aspect - which is what the trophy's height-lock expects.
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
        }
    }
}
