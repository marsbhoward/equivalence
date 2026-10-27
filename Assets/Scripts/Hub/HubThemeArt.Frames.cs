using UnityEngine;
using static Convergence.Hub.HubSketch;

namespace Convergence.Hub
{
    public static partial class HubThemeArt
    {
        /// <summary>A wall frame round <paramref name="inner"/> texels of wallet art, in
        /// <paramref name="theme"/>. Pivot in its middle - the art is the wallet's, only the frame is ours.</summary>
        public static Sprite WallFrame(HubTheme theme, int inner)
        {
            if (theme == HubTheme.Cloister) return HubArt.GiltFrame(inner);
            return HubArt.Get($"frame.{theme}.{inner}", () =>
            {
                int b = 4, n = inner + b * 2;
                var c = new TexelCanvas(n, n);
                Color32 matte = H("#201e24");
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        bool frame = x < b || y < b || x >= n - b || y >= n - b;
                        if (!frame) { c.Set(x, y, matte); continue; }
                        bool outer = x == 0 || y == 0 || x == n - 1 || y == n - 1;
                        bool corner = (x < b || x >= n - b) && (y < b || y >= n - b);
                        c.Set(x, y, FrameTexel(theme, x, y, n, outer, corner));
                    }
                return c.ToSprite("frame", n / 2, n / 2, HubArt.Ppu);
            });
        }

        static Color32 FrameTexel(HubTheme t, int x, int y, int n, bool outer, bool corner)
        {
            switch (t)
            {
                case HubTheme.Study: return outer ? Pal.DarkWood[4] : (x + y) % 6 == 0 ? Pal.DarkWood[1] : Pal.DarkWood[2];
                case HubTheme.Observatory: return corner && !outer ? Pal.Brass[0] : outer ? Pal.Navy[5] : Pal.Navy[2];
                case HubTheme.Crypt: return outer ? K : (x + y) % 4 == 0 ? Pal.Iron[0] : Pal.Iron[3];
                case HubTheme.Atrium: return corner && !outer ? Pal.Leaf[1] : outer ? Pal.Wicker[4] : (x + y) % 3 == 0 ? Pal.Wicker[3] : Pal.Wicker[1];
                case HubTheme.Salt: return outer ? Pal.SaltW[4] : (x / 3 + y / 3) % 2 == 0 ? Pal.SaltW[1] : Pal.SaltW[2];
                case HubTheme.Terrace: return outer ? Pal.Marble[4] : y >= n - 2 ? Pal.Gold[1] : Pal.Marble[1];
                case HubTheme.Sanctum: return corner && !outer ? Pal.RedX[1] : outer ? K : Pal.Blacks[2];
                case HubTheme.Smithy: return outer ? K : (x + y) % 4 == 0 ? Pal.Iron[0] : Pal.Iron[2];
                case HubTheme.Chapel: return outer ? Pal.Gold[4] : y >= n - 3 && Mathf.Abs(x - n / 2) < 3 ? Pal.Gold[0] : (x + y) % 5 == 0 ? Pal.Gold[0] : Pal.Gold[2];
                case HubTheme.Shore: return corner && x < n / 2 && y < n / 2 && !outer ? Pal.Coral[1] : outer ? Pal.Drift[4] : Pal.Drift[(x * 7 + y) % 3 == 0 ? 1 : 2];
                case HubTheme.Volcano: return outer ? K : y < 2 ? Pal.RedX[2] : H("#16141a");
                case HubTheme.Dojo: return y < 2 || y >= n - 2 ? Pal.DarkWood[1] : Pal.Paper[1];   // a hanging scroll's rollers and mount
                case HubTheme.TechLab: return outer ? Pal.Beige[4] : y < 3 && x > n - 6 ? Pal.Crt[1] : Pal.Beige[2];
                default: return outer ? Pal.Gold[4] : Pal.Gold[2];
            }
        }

        /// <summary>What a standing frame's piece stands on in <paramref name="theme"/> - pivot at its
        /// front point - sized like the plinth it replaces.</summary>
        public static Sprite FrameStand(HubTheme theme, int rx, int h)
        {
            if (theme == HubTheme.Cloister) return HubArt.Plinth(rx, h);
            return Cached($"framestand.{theme}.{rx}.{h}", 0, s =>
            {
                switch (theme)
                {
                    case HubTheme.Study:   // an easel's legs behind the piece
                        s.Line(-rx + 2, 0, -2, -h - 26, Pal.DarkWood[2]); s.Line(rx - 2, 0, 2, -h - 26, Pal.DarkWood[2]);
                        s.Rect(-rx, -h + 2, rx, -h + 3, Pal.DarkWood[3]);
                        break;
                    case HubTheme.Observatory: s.Cyl3(0, 0, rx - 2, h, Pal.Navy); break;
                    case HubTheme.Crypt: s.Box3(-rx, 0, rx * 2, 6, h + 2, Pal.Obsid); break;
                    case HubTheme.Atrium: s.Cyl3(0, 0, rx - 1, h + 2, Pal.Terra); Ivy(s, -rx, -h - 4, rx, -h + 2, 10, 7); break;
                    case HubTheme.Salt: s.Box3(-rx, 0, rx * 2, 5, h, Pal.SaltW); s.Crystal(-rx + 2, -h, 8, 2, Pal.Pink); s.Crystal(rx - 3, -h, 6, 2, Pal.Pink); break;
                    case HubTheme.Terrace: s.Box3(-rx + 3, 0, (rx - 3) * 2, 3, h + 4, Pal.Marble, Flute(Pal.Marble)); break;
                    case HubTheme.Sanctum: s.Box3(-rx, 0, rx * 2, 5, h, Pal.Blacks); s.Crystal(-rx - 1, -1, 8, 2, Pal.RedX); break;
                    case HubTheme.Smithy:   // an anvil
                        s.Box3(-rx + 2, 0, (rx - 2) * 2, 5, 4, Pal.Iron); s.Box3(-4, -4, 8, 4, h - 6, Pal.Iron); s.Box3(-rx, -h + 2, rx * 2, 5, 4, Pal.Iron);
                        break;
                    case HubTheme.Chapel: s.Box3(-rx, 0, rx * 2, 5, h, Pal.Crim, (x, y) => y == h - 2 ? Pal.Gold[1] : Pal.Crim[2]); break;
                    case HubTheme.Shore: for (int y = 0; y < h + 4; y++) { float w = rx * 0.6f + Mathf.Sin(y * 1.3f) * 2; for (int x = -Mathf.RoundToInt(w); x <= Mathf.RoundToInt(w); x++) s.Set(x, -y, Pal.Coral[2 + (Hash(x, y) > 0.6f ? 1 : 0)]); } break;
                    case HubTheme.Volcano: s.Box3(-rx, 0, rx * 2, 6, h, Pal.Obsid, (x, y) => y == h - 3 ? Pal.RedX[2] : Pal.Obsid[2]); break;
                    case HubTheme.Dojo: s.Box3(-rx - 2, 0, rx * 2 + 4, 6, h - 2, Pal.BlackLacq); break;
                    case HubTheme.TechLab:   // a laptop: its base, the screen behind the piece
                        s.Box3(-rx, 0, rx * 2, 8, 2, Pal.Plast);
                        s.Box3(-rx, -2, rx * 2, 2, h + 22, Pal.Plast, (x, y) => x < 2 || x > rx * 2 - 3 || y < 2 ? Pal.Plast[3] : H("#000000"));
                        break;
                    default: s.Cyl3(0, 0, rx, h, Pal.Stone); break;
                }
            });
        }

        /// <summary>Over a standing piece: the Cloister's glass bell jar, the Observatory's brass ring,
        /// the Chapel's gilt arch - or nothing. Pivot at its foot's middle.</summary>
        public static Sprite FrameCover(HubTheme theme, int w, int h)
        {
            switch (theme)
            {
                case HubTheme.Cloister: return HubArt.BellJar(w, h);
                case HubTheme.Observatory:
                    return Cached($"framecover.obs.{w}.{h}", 0, s => s.Ring(0, -h / 2f, w / 2f - 1, w / 2f, (float)h / w, (x, y, d, a) => s.Set(x, y, Pal.Brass[1])));
                case HubTheme.Chapel:
                    return Cached($"framecover.chapel.{w}.{h}", 0, s =>
                    {
                        int hw = w / 2;
                        for (int y = 0; y <= h; y++)
                        {
                            float half = y < h - hw ? hw : Mathf.Sqrt(Mathf.Max(0f, hw * hw - Mathf.Pow(y - (h - hw), 2))) * (1f - (y - (h - hw)) / (float)hw * 0.3f);
                            s.Set(-half, -y, Pal.Gold[1]); s.Set(half, -y, Pal.Gold[2]);
                        }
                    });
                default: return null;
            }
        }
    }
}
