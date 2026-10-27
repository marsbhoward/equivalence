using UnityEngine;

namespace Convergence.Hub
{
    public static partial class HubThemeArt
    {
        /// <summary>A card's picture of <paramref name="item"/> in <paramref name="theme"/> - for the
        /// collection's HUB tab. The room is a patch of its wall over its floor.</summary>
        public static Sprite Preview(HubItem item, HubTheme theme) => item switch
        {
            HubItem.Room => HubArt.Get($"preview.room.{theme}", () =>
            {
                int w = 60, wallH = 18, floorH = 30;
                var c = new TexelCanvas(w, wallH + floorH);
                for (int y = 0; y < floorH; y++) for (int x = 0; x < w; x++) c.Px[y * w + x] = HubRoomArt.Floor(theme, x, y, w, floorH);
                for (int y = 0; y < wallH; y++) for (int x = 0; x < w; x++) { var col = HubRoomArt.Wall(theme, x, wallH - 1 - y, wallH); col.a = 255; c.Px[(floorH + y) * w + x] = col; }
                return c.ToSprite("preview.room", w / 2, 0, HubArt.Ppu);
            }),
            HubItem.Forge => Forge(theme, 3, true),
            HubItem.Terminal => Terminal(theme, 4),
            HubItem.Booth => Booth(theme, 2, false, false),
            HubItem.Frames => WallFrame(theme, 22),
            HubItem.Shrine => Shrine(theme, 1),
            HubItem.Table => Table(theme, 60, 25),
            HubItem.Couch => Couch(theme, 66),
            HubItem.Crate => Crate(theme),
            HubItem.Rack => Assembles(theme) ? AssemblySlab(0) : Rack(theme).Back,
            HubItem.Stand => StandBlock(theme, 12, 10, false),
            HubItem.Selector => SelectorRing(theme, 0.5f, 0),
            HubItem.Doorway => Doorway(theme, 10, 30),
            HubItem.Circle => SelectorRing(theme, 0.9f, 1),
            _ => null,
        };
    }
}
