using UnityEngine;
using Convergence.Core;
using static Convergence.Hub.HubSketch;

namespace Convergence.Hub
{
    public static partial class HubThemeArt
    {
        enum BlockKind { Solid, Cage, Wire, Straw }

        struct BlockStyle { public Color32[] R; public BlockKind Kind; public Color32? Joint; }

        static BlockStyle StyleOf(HubTheme t) => t switch
        {
            HubTheme.Cloister => new BlockStyle { R = Pal.Iron, Kind = BlockKind.Cage },                 // the iron dress form
            HubTheme.Crypt => new BlockStyle { R = Pal.Rust, Kind = BlockKind.Cage, Joint = Pal.Bone[1] },
            HubTheme.Observatory => new BlockStyle { R = Pal.Brass, Kind = BlockKind.Solid, Joint = Pal.Navy[2] },
            HubTheme.Atrium => new BlockStyle { R = Pal.Straw, Kind = BlockKind.Straw, Joint = H("#6a5434") },   // the straw dummy
            HubTheme.Salt => new BlockStyle { R = Pal.SaltW, Kind = BlockKind.Solid },
            HubTheme.Terrace => new BlockStyle { R = Pal.Marble, Kind = BlockKind.Solid, Joint = Pal.Gold[1] },
            HubTheme.Sanctum => new BlockStyle { R = Pal.Blacks, Kind = BlockKind.Solid, Joint = Pal.RedX[1] },
            HubTheme.Smithy => new BlockStyle { R = new[] { H("#c89a64"), H("#a07448"), H("#7a5432"), H("#58391f"), H("#3a2412"), H("#20140a") }, Kind = BlockKind.Straw, Joint = Pal.Iron[2] },
            HubTheme.Chapel => new BlockStyle { R = Pal.DarkWood, Kind = BlockKind.Solid, Joint = Pal.Gold[0] },
            HubTheme.Shore => new BlockStyle { R = Pal.Drift, Kind = BlockKind.Solid, Joint = H("#c8b070") },
            HubTheme.Volcano => new BlockStyle { R = Pal.Obsid, Kind = BlockKind.Solid, Joint = Pal.RedX[2] },
            HubTheme.Dojo => new BlockStyle { R = new[] { H("#e8d0a0"), H("#c8a878"), H("#a88858"), H("#806040"), H("#584028"), H("#2c2014") }, Kind = BlockKind.Solid },
            HubTheme.TechLab => new BlockStyle { R = Pal.Crt, Kind = BlockKind.Wire },
            _ => new BlockStyle { R = Pal.Oak, Kind = BlockKind.Solid },
        };

        /// <summary>A mannequin block in <paramref name="theme"/> - the same sizes and pivots as
        /// <see cref="HubArt.MannequinBlock"/>, so gear hangs exactly where it does on the carved one.</summary>
        public static Sprite StandBlock(HubTheme theme, int w, int h, bool round)
        {
            if (theme == HubTheme.Study) return HubArt.MannequinBlock(w, h, round);   // the carved mannequin
            return HubArt.Get($"stand.{theme}.{w}.{h}.{round}", () =>
            {
                var st = StyleOf(theme); var r = st.R;
                var c = new TexelCanvas(w, h);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float ex = (x + 0.5f - w / 2f) / (w / 2f), ey = (y + 0.5f - h / 2f) / (h / 2f);
                        if (round && ex * ex + ey * ey > 1f) continue;
                        bool edge = round
                            ? Mathf.Pow((x + 0.5f - w / 2f) / (w / 2f - 1f), 2) + Mathf.Pow((y + 0.5f - h / 2f) / (h / 2f - 1f), 2) > 1f
                            : x == 0 || y == 0 || x == w - 1 || y == h - 1;
                        Color32 col;
                        switch (st.Kind)
                        {
                            case BlockKind.Cage:   // hoops and ribs, the room seen between them
                                if (!edge && y % 3 != 0 && x != w / 2) continue;
                                col = edge ? r[3] : y % 3 == 0 ? r[1] : r[2];
                                break;
                            case BlockKind.Wire:   // outline and a few lines only, glowing
                                if (!edge && (x + y) % 4 != 0) continue;
                                col = r[1];
                                break;
                            case BlockKind.Straw:
                                col = edge ? r[4] : (x + y) % 3 == 0 ? r[0] : r[2];
                                break;
                            default:
                                col = edge ? r[4] : x < w / 3 ? r[0] : x > w * 2 / 3 ? r[2] : r[1];
                                break;
                        }
                        c.Set(x, y, col);
                    }
                if (st.Joint.HasValue && !round && w >= 4) { c.Set(w / 2, h - 2, st.Joint.Value); c.Set(w / 2 - 1, h - 2, st.Joint.Value); }
                return c.ToSprite("stand", w / 2, h / 2, Combat.TexelFx.Body * Tuning.Player.ArenaVisualScale);
            });
        }

        /// <summary>What the mannequin stands on in <paramref name="theme"/>. Pivot at its front point.</summary>
        public static Sprite StandBase(HubTheme theme)
        {
            if (theme == HubTheme.Study) return HubArt.MannequinBase();
            return Cached($"stand.base.{theme}", 0, s =>
            {
                switch (theme)
                {
                    case HubTheme.Cloister: s.Cyl3(0, 0, 11, 5, Pal.Stone); s.Rect(-1, -12, 1, -6, Pal.Iron[2]); break;   // a stone plinth, an iron pole
                    case HubTheme.Crypt: s.Cyl3(0, 0, 11, 5, Pal.CryptS); s.Rect(-1, -12, 1, -6, Pal.Rust[2]); break;
                    case HubTheme.Atrium: s.Rect(-12, 0, 12, 0, Pal.Oak[3]); s.Rect(-1, -12, 1, 0, Pal.Oak[2]); break;   // a post on a cross-foot
                    case HubTheme.Smithy: foreach (int lx in new[] { -10, 0, 10 }) s.Line(0, -10, lx, lx == 0 ? 2 : 0, Pal.Iron[2]); break;   // an iron tripod
                    case HubTheme.Dojo: s.Box3(-12, 0, 24, 6, 4, Pal.BlackLacq); break;   // the lacquered armour box
                    case HubTheme.TechLab: s.Cyl3(0, 0, 11, 2, Pal.Plast); s.Rect(-1, -10, 1, -3, Pal.Plast[3]); break;
                    default:
                    {
                        var r = theme switch
                        {
                            HubTheme.Observatory => Pal.Navy, HubTheme.Salt => Pal.SaltW, HubTheme.Terrace => Pal.Marble,
                            HubTheme.Sanctum => Pal.Blacks, HubTheme.Chapel => Pal.DarkWood, HubTheme.Shore => Pal.Drift,
                            HubTheme.Volcano => Pal.Obsid, _ => Pal.Oak,
                        };
                        s.Cyl3(0, 0, 12, 4, r); s.Rect(-1, -12, 1, -6, r[2]);
                        break;
                    }
                }
            });
        }
    }
}
