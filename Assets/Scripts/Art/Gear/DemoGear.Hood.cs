using UnityEngine;

namespace Convergence.Art.Gear
{
    // ====================================================== the cowl: every hood's front
    //
    // HoodRows (black_hood, wraith_cloak) and PonchoHoodRows (wanderers_hood) are ONE shape,
    // sampled twice - inked for the two cloaks, padded and unlined for the poncho, which is cloth
    // without drawn lines by design (see wanderers_hood). They were two hand-typed grids that had
    // to be edited in step and each carried the other's window "row for row".
    //
    // A TRIANGLE, OPEN AT THE BOTTOM. The hood's sides fall outward from the eye line onto the
    // shoulders, and the opening widens with them - narrow at the brow, the face's full width at
    // the eyes, wider still past the chin, and open onto the chest. It used to be a straight
    // window closing in a V under the chin, which boxed in whatever the character wore on the
    // lower face (the Revenant respirator was the one that showed it) and read as a tube round
    // the head rather than cloth hanging from it. The user's call, after the Revenant set.
    //
    // HEAD-local texels, measured off the rendered head: skin spans x -14..13, the TURNED face
    // looks toward +X, the eyes sit at x -7..-3 and 7..12 at y 7.5..11.5, the chin at y 0.5. The
    // opening is the eyes' full span across those rows, and that is the one hard constraint:
    // everything else is silhouette.
    //
    // THE INK IS BAKED, NOT STROKED, on the inked version. The auto-outline draws four texels
    // outside a silhouette, and an opening wider than a narrow channel gets all four - into the
    // face. At the old window that hid most of the far eye. So the grid draws its own: the usual
    // heavy line round the OUTSIDE (Manhattan four, what StrokeOutline itself draws) and one texel
    // round the opening, on the cloth. 'o' is Palette.Outline - the caller's palette carries it.
    //
    // No glow anywhere: a lit crown is 'l'. A glow on cloth reads as metal.
    public static partial class DemoGear
    {
        // The grid, head-local texels: the cloth runs x -20..20, y -9..31, and the inked version's
        // outline takes four texels past that on every side.
        const int HoodMinX = -24, HoodMaxX = 24, HoodBottom = -13, HoodTop = 35;
        const float HoodCrownTop = 31f, HoodHem = -9f;

        /// <summary>Where every hood's centre goes, in head-local cells - the crown stays at y 31.</summary>
        static float HoodY => FieldCentreCells(HoodBottom, HoodTop);

        static string[] _hoodRows, _ponchoHoodRows;
        static string[] HoodRows => _hoodRows ??= PaintField(HoodMinX, HoodMaxX, HoodBottom, HoodTop,
            (ix, iy) => HoodTexel(ix, iy, inked: true));
        static string[] PonchoHoodRows => _ponchoHoodRows ??= PaintField(HoodMinX, HoodMaxX, HoodBottom, HoodTop,
            (ix, iy) => HoodTexel(ix, iy, inked: false));

        /// <summary>Half the hood's width at height y: the head's own 14 above the eyes, flaring to 20 at the hem.</summary>
        static float HoodOuter(float y) => y >= 12f ? 14f : 14f + (12f - y) * 0.29f;

        /// <summary>
        /// Whether (x, y) is in the face opening - nothing above its apex or below the hem. Lopsided
        /// toward +X like the face it frames: the turned face's far eye sits near the head's edge.
        /// </summary>
        static bool HoodOpening(float x, float y)
        {
            if (y >= 25f || y < HoodHem) return false;
            float l, r;
            if (y >= 13f) { float t = (25f - y) / 12f; l = -6f - 4f * t; r = 8f + 4f * t; }
            else { l = -10f - (13f - y) * 0.30f; r = 12f + (13f - y) * 0.25f; }
            return x >= l && x < r;
        }

        static bool HoodCloth(float x, float y)
        {
            if (y >= HoodCrownTop || y < HoodHem) return false;
            if (y >= 29f) return x >= -4f && x < 4f;
            if (y >= 27f) return x >= -8f && x < 8f;
            if (y >= 25f) return x >= -12f && x < 12f;
            float w = HoodOuter(y);
            // The hem's outer corners rounded off.
            if (y < HoodHem + 1f) w -= 1f;
            return x >= -w && x < w && !HoodOpening(x, y);
        }

        static char HoodTone(float x, float y)
        {
            float ax = Mathf.Abs(x);
            if (y >= 29f) return 'l';
            if (y >= 27f) return ax >= 4f ? 'l' : 'b';                 // the crown's lit shoulders
            if (y >= 19f) return 'b';
            if (y >= 11f) return 'd';
            // Where it falls onto the shoulders, the drape's top catches the light again.
            if (ax > 15f && y >= 2f) return 'd';
            return 's';
        }

        static char HoodTexel(int ix, int iy, bool inked)
        {
            float x = ix + 0.5f, y = iy + 0.5f;
            if (HoodCloth(x, y))
            {
                // The opening's own line, one texel, on the cloth.
                if (inked && (HoodOpening(x - 1f, y) || HoodOpening(x + 1f, y)
                              || HoodOpening(x, y - 1f) || HoodOpening(x, y + 1f)))
                    return 'o';
                return HoodTone(x, y);
            }
            if (HoodOpening(x, y)) return '.';

            // Past the cloth: the outline (inked), or a texel of padding in the cloth's own tone
            // (unlined - the poncho's "coverage as fill, not a stroke", see wanderers_hood).
            int reach = inked ? 4 : 1;
            for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach + Mathf.Abs(dy); dx <= reach - Mathf.Abs(dy); dx++)
            {
                if (!HoodCloth(x + dx, y + dy)) continue;
                return inked ? 'o' : HoodTone(x + dx, y + dy);
            }
            return '.';
        }
    }
}
