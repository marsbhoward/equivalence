using UnityEngine;
using static Convergence.Hub.HubSketch;

namespace Convergence.Hub
{
    public static partial class HubThemeArt
    {
        /// <summary>A theme's weapon rack: the furniture BEHIND the weapon, anything in FRONT of it, and
        /// where the weapon's centre rests (world units above the foot). The weapon itself is the real
        /// one, hung by WeaponRack. The Volcano throne's is not a rack at all (<see cref="Assembles"/>).</summary>
        public struct RackDress { public Sprite Back, Front; public Vector2 RestAt; }

        /// <summary>True where the weapon lies on a table in parts that come together (the user's
        /// Volcano throne pick: turning, then settling, over a black slab with lava under its top).</summary>
        public static bool Assembles(HubTheme t) => t == HubTheme.Volcano;

        public static RackDress Rack(HubTheme theme)
        {
            var d = new RackDress { RestAt = new Vector2(0f, 0.74f) };
            switch (theme)
            {
                case HubTheme.Study: d.Back = HubArt.AFrame(40, 54); break;
                case HubTheme.Cloister: case HubTheme.Crypt: case HubTheme.Salt:
                    d.Back = Cached($"rack.{theme}", 0, s => Pedestal(s, theme == HubTheme.Crypt ? Pal.CryptS : theme == HubTheme.Salt ? Pal.SaltW : Pal.Stone));
                    d.RestAt = new Vector2(0f, 1.0f);
                    break;
                case HubTheme.Terrace:
                    d.Back = Cached("rack.terrace", 0, s => { s.Box3(-8, 0, 16, 6, 24, Pal.Marble, Flute(Pal.Marble)); s.Rect(-2, -27, 2, -26, Pal.Marble[4]); });
                    d.RestAt = new Vector2(0f, 1.2f);
                    break;
                case HubTheme.Shore: d.Back = Cached("rack.shore", 0, s => AFrame(s, Pal.Drift, true)); break;
                case HubTheme.Observatory:   // Wall board (re-paired): a board stood against the wall with brass pegs
                    d.Back = Cached("rack.observatory", 0, s => { s.Box3(-14, 0, 28, 3, 54, Pal.DarkWood, (x, y) => x % 5 == 0 ? Pal.DarkWood[3] : Pal.DarkWood[2]); s.Set(-5, -44, Pal.Brass[1]); s.Set(5, -44, Pal.Brass[1]); });
                    break;
                case HubTheme.Atrium:   // Trellis with a vine
                    d.Back = Cached("rack.atrium", 0, s => { for (int y = 0; y < 52; y++) for (int x = -15; x <= 15; x++) if ((x + y) % 7 == 0 || (x - y + 140) % 7 == 0) s.Set(x, -y, Pal.Wicker[2]); Ivy(s, -15, -48, 15, -2, 40, 9); });
                    break;
                case HubTheme.Sanctum:   // Crystal spears either side
                    d.Back = Cached("rack.sanctum.b", 0, s => { s.Box3(-16, 0, 32, 7, 6, Pal.Blacks); s.Crystal(-9, -6, 44, 4, Pal.RedX); });
                    d.Front = Cached("rack.sanctum.f", 0, s => s.Crystal(10, -6, 36, 4, Pal.RedX));
                    break;
                case HubTheme.Smithy:   // Tool rack
                    d.Back = Cached("rack.smithy", 0, s =>
                    {
                        s.Box3(-20, 0, 40, 3, 52, Pal.Iron, (x, y) => x == 0 || x == 39 || y == 48 || y == 8 ? Pal.Iron[2] : H("#1a1818"));
                        foreach (int tx in new[] { -15, -10, 10, 15 }) { s.Rect(tx, -40, tx, -14, Pal.Iron[1]); s.Rect(tx - 1, -40, tx + 1, -37, Pal.Iron[2]); }
                    });
                    break;
                case HubTheme.Chapel:   // Gilt stand, a crimson banner behind
                    d.Back = Cached("rack.chapel", 0, s =>
                    {
                        s.Cyl3(0, 0, 9, 3, Pal.Gold);
                        s.Rect(-14, -54, 14, -53, Pal.Gold[1]); s.Rect(-14, -54, -13, -6, Pal.Gold[2]);
                        for (int y = 0; y < 30; y++) for (int x = -11; x <= 11; x++) if (y < 27 || Mathf.Abs(x) > 3) s.Set(x, -51 + y, Pal.Crim[2 + (x == 0 ? 1 : 0)]);
                    });
                    break;
                case HubTheme.Dojo:   // Sword stand in black lacquer
                    d.Back = Cached("rack.dojo", 0, s =>
                    {
                        s.Box3(-15, 0, 30, 8, 4, Pal.BlackLacq);
                        foreach (int px in new[] { -12, 10 }) s.Box3(px, -4, 3, 2, 36, Pal.BlackLacq);
                        foreach (int y in new[] { -16, -30 }) s.Rect(-12, y, 12, y, Pal.Lacq[2]);
                    });
                    break;
                case HubTheme.TechLab:   // Server rack, the weapon mounted down its front
                    d.Back = Cached("rack.techlab", 0, s =>
                    {
                        var dark = new[] { H("#5a5c62"), H("#46484e"), H("#36383e"), H("#28292e"), H("#1a1b1e"), H("#0e0f10") };
                        s.Box3(-16, 0, 32, 10, 56, dark, (x, y) => y % 4 == 0 ? H("#000000") : x > 24 && x < 29 && Hash(x, y) > 0.55f ? Pal.Crt[1] : H("#1a1c20"));
                    });
                    break;
            }
            return d;
        }

        /// <summary>The Cloister's stone pedestal (round 1's pick) and its own versions: a block with a
        /// socket in its top, the blade standing point-down in it.</summary>
        static void Pedestal(HubSketch s, Color32[] stone)
        {
            s.Box3(-10, 0, 20, 7, 16, stone, (x, y) => y % 6 == 0 ? stone[3] : x < 2 ? stone[1] : stone[2]);
            s.Rect(-2, -20, 2, -19, stone[4]);
        }

        /// <summary>The A-frame in any wood (the Seashore's is driftwood, lashed with rope).</summary>
        static void AFrame(HubSketch s, Color32[] wood, bool rope)
        {
            s.Box3(-20, 0, 4, 2, 54, wood); s.Box3(16, 0, 4, 2, 54, wood);
            s.Box3(-20, -46, 40, 2, 3, wood); s.Box3(-20, -6, 40, 2, 3, wood);
            if (rope) foreach (int x in new[] { -18, 18 }) foreach (int y in new[] { -7, -47 }) { s.Set(x, y, H("#c8b070")); s.Set(x, y - 1, H("#c8b070")); }
        }

        // ---- the Volcano throne's assembly table ----

        /// <summary>The black slab with lava glowing under its glass top (the user's pick). Pivot at its
        /// front foot; the parts hang over it.</summary>
        public static Sprite AssemblySlab(int frame) => Cached($"assembly.slab.{frame}", frame / 8f, s =>
        {
            foreach (int lx in new[] { -24, 21 }) s.Box3(lx, 0, 3, 2, 6, Pal.Obsid);
            s.Box3(-26, -6, 52, 16, 8, Pal.Obsid);
            for (int x = -22; x <= 22; x++) for (int d = 0; d < 4; d++) s.Set(x, -6 - 8 - 6 - d, Lava(x, d, s.T), 0.85f);
        });
        public const int AssemblyFrames = 8;
    }
}
