using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== armour drawn as fields
    //
    // The helpers the field-built armour sets share (DemoGear.Shogun.cs, DemoGear.Sovereign.cs,
    // DemoGear.Revenant.cs). Each draws its pieces as a function answering "what is at this
    // texel", sampled over a span of the rig's own frame - torso-, head-, arm- or leg-local body
    // texels - so a piece is positioned by the same numbers it is drawn with and cannot drift off
    // its joint.
    public static partial class DemoGear
    {
        /// <summary>
        /// Sample a field over texels [xMin, xMax) x [yMin, yMax), top row first. The field is
        /// asked about each texel by its lower-left corner. Keep both spans EVEN: an odd width
        /// has no texel boundary to mirror about, and an odd height puts the centre pivot
        /// mid-texel, off the body's own grid.
        /// </summary>
        static string[] PaintField(int xMin, int xMax, int yMin, int yMax, System.Func<int, int, char> texel)
        {
            var rows = new string[yMax - yMin];
            var sb = new System.Text.StringBuilder(xMax - xMin);
            for (int y = yMax - 1, r = 0; y >= yMin; y--, r++)
            {
                sb.Clear();
                for (int x = xMin; x < xMax; x++) sb.Append(texel(x, y));
                rows[r] = sb.ToString();
            }
            return rows;
        }

        /// <summary>A grid's centre in layout cells, from its texel span.</summary>
        static float FieldCentreCells(int lo, int hi)
            => PrimitiveCharacterRig.Proportions.Cells((lo + hi) * 0.5f);

        /// <summary>
        /// A ramp's grid character for a tone, 0 (line) .. 5 (glow), clamped. The ramp string is
        /// one material's six characters in that order - "ksdblh", "KSDBLH" or "123456", the
        /// three materials Palette.Of can carry.
        /// </summary>
        static char RampChar(string ramp, int tone) => ramp[Mathf.Clamp(tone, 0, 5)];

        /// <summary>
        /// A tone step for which way a surface faces: +1 past <paramref name="lit"/> (turned toward
        /// the light, which every piece here takes from +X), -1 short of <paramref name="shaded"/>.
        /// </summary>
        static int LitSide(float x, float lit, float shaded) => x > lit ? 1 : x < shaded ? -1 : 0;

        /// <summary>Modulo that stays correct below zero - fields run past every joint's 0.</summary>
        static int WrapMod(int a, int m) => ((a % m) + m) % m;
    }
}
