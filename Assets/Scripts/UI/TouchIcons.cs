using System.Collections.Generic;
using UnityEngine;
using Convergence.Core;

namespace Convergence.UI
{
    /// <summary>
    /// The on-screen buttons' icons (the user's pick, 2026-10-31: "Glass, gold lining", mock round
    /// 2). Pixel art at the HUD's own density - one icon texel is <see cref="Texel"/> canvas units,
    /// the size the rotation wheel and the seal are drawn at - so the buttons stop reading as a
    /// phone overlay pasted over a pixel game. White, with a one-texel shadow baked down and to
    /// the right so they hold over a bright floor; the Image's colour dims them.
    /// </summary>
    public static class TouchIcons
    {
        /// <summary>Canvas units per icon texel: the 1920-wide canvas over a 640-texel HUD.</summary>
        public const float Texel = 3f;

        static Sprite _sword, _shield, _star, _eye;
        static Sprite[] _elements;

        public static Sprite Sword => _sword != null ? _sword : _sword = Make(SwordRows());
        public static Sprite Shield => _shield != null ? _shield : _shield = Make(new[]
        {
            ".#########.", "#.........#", "#.........#", "#....#....#", "#...###...#", "#....#....#",
            ".#.......#.", ".#.......#.", "..#.....#..", "...#...#...", "....#.#....", ".....#.....",
        });
        /// <summary>USE: a four-pointed glint - whatever is in reach.</summary>
        public static Sprite Star => _star != null ? _star : _star = Make(StarRows(19));
        public static Sprite Eye => _eye != null ? _eye : _eye = Make(new[]
        {
            "...#####...", ".##.....##.", "#...###...#", "#..#####..#", "#...###...#", ".##.....##.", "...#####...",
        });

        /// <summary>ABILITY: the element's alchemical triangle - Fire and Air point up, Water and
        /// Earth down, Air and Earth crossed by a bar - the signs the seal already uses.</summary>
        public static Sprite Element(ElementType e)
        {
            if (_elements == null || _elements.Length != 4 || _elements[0] == null)
                _elements = new[]
                {
                    Make(TriangleRows(15, up: true, bar: false)),   // Fire
                    Make(TriangleRows(15, up: false, bar: false)),  // Water
                    Make(TriangleRows(15, up: false, bar: true)),   // Earth
                    Make(TriangleRows(15, up: true, bar: true)),    // Air
                };
            int i = (int)e;
            return _elements[i >= 0 && i < _elements.Length ? i : 0];
        }

        /// <summary>The size an icon is drawn at, in canvas units.</summary>
        public static Vector2 Size(Sprite s) => s == null ? Vector2.zero : new Vector2(s.rect.width, s.rect.height) * Texel;

        // ------------------------------------------------------------ rows

        static readonly Dictionary<char, Color32> Palette = new()
        {
            ['X'] = new Color32(255, 255, 255, 255),
            ['S'] = new Color32(0, 0, 0, 100),
        };

        /// <summary>The rows with a shadow one texel down and right of every lit texel.</summary>
        static Sprite Make(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var g = new char[h + 1, w + 1];
            for (int y = 0; y <= h; y++) for (int x = 0; x <= w; x++) g[y, x] = '.';
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (rows[y][x] != '#') continue;
                g[y, x] = 'X';
                if (g[y + 1, x + 1] == '.') g[y + 1, x + 1] = 'S';
            }
            var outRows = new string[h + 1];
            var sb = new System.Text.StringBuilder(w + 1);
            for (int y = 0; y <= h; y++)
            {
                sb.Clear();
                for (int x = 0; x <= w; x++) sb.Append(g[y, x]);
                outRows[y] = sb.ToString();
            }
            return SigilRings.Grid(outRows, Palette);
        }

        static char[][] Blank(int w, int h)
        {
            var g = new char[h][];
            for (int y = 0; y < h; y++) { g[y] = new char[w]; for (int x = 0; x < w; x++) g[y][x] = '.'; }
            return g;
        }

        static string[] Rows(char[][] g)
        {
            var r = new string[g.Length];
            for (int i = 0; i < g.Length; i++) r[i] = new string(g[i]);
            return r;
        }

        /// <summary>A greatsword on the diagonal, point up and right: a two-texel blade, a
        /// crossguard across it, the grip and a square pommel.</summary>
        static string[] SwordRows()
        {
            var g = Blank(21, 21);
            void P(int x, int y) { if (x >= 0 && x < 21 && y >= 0 && y < 21) g[y][x] = '#'; }
            const int gx = 7, gy = 13;
            for (int t = 1; t <= 12; t++) { P(gx + t, gy - t); P(gx + t + 1, gy - t); }
            for (int i = -4; i <= 4; i++) { P(gx + i, gy + i); P(gx + i + 1, gy + i); }
            for (int t = 1; t <= 4; t++) { P(gx - t, gy + t); P(gx - t + 1, gy + t); }
            P(1, 18); P(2, 18); P(1, 19); P(2, 19);
            return Rows(g);
        }

        static string[] TriangleRows(int n, bool up, bool bar)
        {
            var g = Blank(n, n);
            int c = (n - 1) / 2;
            for (int row = 0; row < n; row++)
            {
                int y = up ? row : n - 1 - row;
                if (row == n - 1) { for (int x = 0; x < n; x++) g[y][x] = '#'; continue; }
                int hw = Mathf.RoundToInt(row * c / (float)(n - 1));
                g[y][c - hw] = '#';
                g[y][c + hw] = '#';
                if (bar && row == (n - 1) * 3 / 5)
                    for (int x = c - hw; x <= c + hw; x++) g[y][x] = '#';
            }
            return Rows(g);
        }

        static string[] StarRows(int n)
        {
            var g = Blank(n, n);
            float c = (n - 1) / 2f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                if ((Mathf.Abs(x - c) + 0.5f) * (Mathf.Abs(y - c) + 0.5f) <= 3.2f) g[y][x] = '#';
            return Rows(g);
        }
    }
}
