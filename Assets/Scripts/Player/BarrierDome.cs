using UnityEngine;
using Convergence.Core;

namespace Convergence.Player
{
    /// <summary>
    /// Barrier's tell: a hexagonal shell thrown up over the character for the ability's duration.
    ///
    /// This is what separates Barrier from the other three defensive abilities, which until now
    /// all announced themselves with the same pale flash and then differed only in what the
    /// damage numbers did. Dash SHOWS its own identity - the character moves - so the two that
    /// resolve by standing still needed one of their own.
    ///
    /// DRAWN FROM THE BLOCK, NOT BESIDE IT. The lit arc is <see cref="Tuning.Defense.BarrierFrontDot"/>
    /// itself rather than a second number chosen to look right, and the facing is the same vector
    /// latched into PlayerController's _barrierFacing at activation - so the dome cannot drift out
    /// of agreement with what actually blocks. The half the block does not cover still draws, at
    /// <see cref="Tuning.Defense.BarrierGhostAlpha"/>: a lone 120-degree crescent reads as a stray
    /// arc, where a full shell with a dim back reads as a shell with an open back, which is the
    /// true statement.
    ///
    /// NOT in <see cref="Spr"/>, for the reason PitArt is not either: the lattice needs its own
    /// spherical unwrap and a rim treatment, and none of that is reusable by anything else.
    /// </summary>
    public class BarrierDome : MonoBehaviour
    {
        // Non-readonly and re-baked through ??= on first use - a domain reload clears statics,
        // and a cached Sprite that came back null would otherwise draw nothing for the session.
        static Sprite _shell;

        /// <summary>
        /// Seconds the shell takes to snap up. Deliberately far shorter than the parry window
        /// (0.2s): the dome must be fully drawn before the instant it is judged on, or a player
        /// reading the art would be reading it late.
        /// </summary>
        const float RaiseSeconds = 0.09f;
        const float FallSeconds = 0.18f;

        /// <summary>Where the lattice sits when raised. Under 1 because a solid panel would hide the character it is protecting.</summary>
        const float HoldAlpha = 0.62f;

        SpriteRenderer _sr;
        Quaternion _lockedRotation;
        float _age;
        float _life;

        /// <summary>
        /// Put a dome over <paramref name="owner"/> facing <paramref name="facing"/> for
        /// <paramref name="seconds"/>. Re-raising on an owner that already has one restarts that
        /// dome rather than welding a second lattice over the first - reachable today only by
        /// stacking enough AbilityCooldownReduction to bring the cooldown under the duration.
        /// </summary>
        public static BarrierDome Raise(Transform owner, Vector2 facing, float seconds)
        {
            if (owner == null) return null;

            var dome = owner.GetComponentInChildren<BarrierDome>(true);
            if (dome == null)
            {
                var go = new GameObject("barrier-dome");
                go.transform.SetParent(owner, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one * Tuning.Defense.BarrierDomeRadius * 2f;

                dome = go.AddComponent<BarrierDome>();
                dome._sr = go.AddComponent<SpriteRenderer>();
                dome._sr.sprite = Shell;
                // Above the whole depth band, like every other overlay: a shell the enemy
                // standing in front of you can hide is a shell you cannot rely on. Below
                // Telegraph so an attack's tell still reads through it.
                dome._sr.sortingOrder = SortingOrders.StatusOverlay + 5;
            }

            dome.gameObject.SetActive(true);
            dome._age = 0f;
            dome._life = seconds;
            dome._lockedRotation = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
            return dome;
        }

        void LateUpdate()
        {
            if (_sr == null) { gameObject.SetActive(false); return; }
            _age += Time.deltaTime;

            // Held in WORLD space. The dome's arc is the facing captured at activation, not the
            // one the player is holding now - they can turn freely while it is up, and the
            // covered side stays where they put it. The player root does not rotate today, so
            // this is belt-and-braces; it is written anyway because the day something rotates it
            // the failure is a shell that silently tracks a facing the block does not.
            transform.rotation = _lockedRotation;

            float raise = Mathf.Clamp01(_age / RaiseSeconds);
            float remaining = _life - _age;
            float fall = Mathf.Clamp01(remaining / FallSeconds);

            if (remaining <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }

            // Overshoots past full on the way up, so it reads as slammed into place rather than
            // faded in - the same reason Dash spends its distance in a quarter-second.
            float scale = Mathf.Lerp(0.72f, 1.06f, EaseOut(raise));
            if (raise >= 1f) scale = Mathf.Lerp(1.06f, 1f, Mathf.Clamp01((_age - RaiseSeconds) / 0.12f));
            transform.localScale = Vector3.one * Tuning.Defense.BarrierDomeRadius * 2f * scale;

            var c = Tuning.Defense.BarrierColor;
            // A slow breath on top of the hold value. Electric blue that sits perfectly still
            // reads as a decal stuck to the screen.
            float breathe = 1f + 0.10f * Mathf.Sin(_age * 11f);
            _sr.color = new Color(c.r, c.g, c.b, c.a * HoldAlpha * raise * fall * breathe);
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        // ---- the shell sprite ----

        const int ShellSize = 512;

        /// <summary>
        /// Hex panels laid out on a HEMISPHERE and looked at from above, not a flat honeycomb
        /// disc with a circle cut out of it.
        ///
        /// The unwrap is azimuthal equidistant: a screen point at radius d sits at polar angle
        /// asin(d) from the zenith, and the lattice is built in that angle rather than in d. Cells
        /// are therefore equal-sized ON THE SHELL, and the bunching toward the rim - which is what
        /// actually sells the curvature - falls out of the maths instead of being drawn in. A
        /// honeycomb in screen space keeps its cells the same size all the way to the edge and
        /// reads as a flat sticker.
        ///
        /// The honeycomb itself is the Voronoi boundary of a triangular lattice, found as the gap
        /// between the nearest and second-nearest lattice site. The thickening where three cells
        /// meet is kept rather than corrected - those are the panel joints, and a lattice with
        /// featureless vertices reads as a wireframe rather than as panelling.
        /// </summary>
        static Sprite Shell => _shell ??= BakeShell();

        static Sprite BakeShell()
        {
            float half = Mathf.Acos(Mathf.Clamp(Tuning.Defense.BarrierFrontDot, -1f, 1f)) * Mathf.Rad2Deg;
            float cell = Mathf.Max(0.02f, Tuning.Defense.BarrierHexCell);
            float rowH = cell * 0.8660254f;   // sqrt(3)/2 - triangular lattice row spacing

            var tex = new Texture2D(ShellSize, ShellSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "barrier.shell",
            };

            float c = ShellSize * 0.5f;
            var px = new Color[ShellSize * ShellSize];

            for (int y = 0; y < ShellSize; y++)
            for (int x = 0; x < ShellSize; x++)
            {
                float nx = (x + 0.5f - c) / c, ny = (y + 0.5f - c) / c;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                if (d >= 1f) { px[y * ShellSize + x] = new Color(1f, 1f, 1f, 0f); continue; }

                float theta = Mathf.Asin(Mathf.Clamp01(d));          // angle down from the zenith
                float phi = Mathf.Atan2(ny, nx);

                // Lattice coordinates, in cells, on the unwrapped shell.
                float px_ = theta * Mathf.Cos(phi) / cell;
                float py_ = theta * Mathf.Sin(phi) / cell;

                float line = Honeycomb(px_, py_, rowH / cell);

                // Kills the last sliver before the rim, where the unwrap compresses whole cells
                // into a pixel or two and the lattice would alias into a crawling ring. The limb
                // below is what draws the edge instead.
                float rimFade = Mathf.Clamp01((1f - d) / 0.07f);

                // The dome's own silhouette. Its brightness is what makes the shape read as a
                // dome rather than as a patch of honeycomb floating over the character.
                float limb = Mathf.Clamp01(1f - Mathf.Abs(d - 0.965f) / 0.030f);

                // Faint panel fill, so the enclosed space reads as glassed rather than as open
                // air with a grid drawn over it. Strengthens toward the rim for the same reason a
                // real shell looks denser where you are looking through more of it.
                float fill = Mathf.Lerp(0.05f, 0.16f, d * d) * rimFade;

                float a = Mathf.Clamp01(line * rimFade + fill);
                a = Mathf.Max(a, limb);

                // ---- the covered arc ----
                float ang = Mathf.Abs(phi) * Mathf.Rad2Deg;
                float lit = Mathf.Lerp(Tuning.Defense.BarrierGhostAlpha, 1f,
                                       Mathf.Clamp01((half - ang) / 2f));
                a *= lit;

                // The seam where coverage ends, drawn as a real terminating edge. Without it the
                // lattice is simply cropped mid-panel, which reads as a rendering fault rather
                // than as the boundary it is. Suppressed at the very centre, where every angle
                // meets and the seam has no direction to run in.
                float seam = Mathf.Clamp01(1f - Mathf.Abs(ang - half) / 1.6f)
                             * rimFade * Mathf.Clamp01(d / 0.05f);
                a = Mathf.Max(a, seam * 0.9f);

                px[y * ShellSize + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
            }

            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, ShellSize, ShellSize),
                                 new Vector2(0.5f, 0.5f), ShellSize);
        }

        /// <summary>
        /// Line strength of a honeycomb at (<paramref name="px"/>, <paramref name="py"/>), given
        /// in cells. Nearest and second-nearest sites of the triangular lattice are found over the
        /// 3x3 neighbourhood - enough, since no point in a cell is closer to a site two rows away
        /// than to one of these - and their difference is the distance to the cell wall.
        /// </summary>
        static float Honeycomb(float px, float py, float rowH)
        {
            int jc = Mathf.RoundToInt(py / rowH);
            float best = float.MaxValue, second = float.MaxValue;

            for (int dj = -1; dj <= 1; dj++)
            {
                int j = jc + dj;
                float sy = j * rowH;
                float offset = (j & 1) == 0 ? 0f : 0.5f;
                int ic = Mathf.RoundToInt(px - offset);

                for (int di = -1; di <= 1; di++)
                {
                    float sx = (ic + di) + offset;
                    float ex = px - sx, ey = py - sy;
                    float dist = Mathf.Sqrt(ex * ex + ey * ey);
                    if (dist < best) { second = best; best = dist; }
                    else if (dist < second) second = dist;
                }
            }

            // Half the gap, because the wall is midway between the two sites.
            float toWall = (second - best) * 0.5f;
            const float Width = 0.055f;   // in cells
            return Mathf.Clamp01(1f - toWall / Width);
        }
    }
}
