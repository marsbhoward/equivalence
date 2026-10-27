using UnityEngine;
using Convergence.Combat;
using Convergence.Core;

namespace Convergence.Hazards
{
    /// <summary>
    /// The spire's capture ring (the user's pick, group 4 - "clock fill"): a groove CUT into the
    /// floor (a ring you stand in - the rule), and the boon's light running round it clockwise from
    /// the top as the capture climbs, a white tip at its front; running back as it drains; full and
    /// slowly breathing once taken. An untaken spire's groove stays dark. Shudders while the stone sinks.
    /// </summary>
    public class SpireGround : TexelPicture
    {
        [SerializeField] Spire _s;
        const int Groove = 3;

        protected override bool Draw()
        {
            if (_s == null) _s = GetComponentInParent<Spire>();
            if (_s == null) return false;
            float ground = 1f - _s.SinkShare;
            if (ground <= 0f) return false;
            float r = Tuning.Spire.CaptureRadius * Ppu;
            int j = _s.Sinking && _s.SinkShare < 0.25f ? Mathf.RoundToInt(Mathf.Sin(Age * 60f)) : 0;
            Color32 tint = SpireBoons.ColorOf(_s.Boon), tip = Lerp(tint, Color.white, 0.75f);
            float p = _s.Captured ? 1f : _s.Dark ? 0f : _s.Progress01;
            float breathe = _s.Captured ? 0.75f + 0.25f * Mathf.Sin(Age * 3f) : 1f;
            Ring(j, 0, r - Groove, r, (x, y, d, ang) =>
            {
                // The groove's NORTH wall catches the light: the outer edge on the top half, the
                // inner edge on the bottom half.
                bool lip = y > 0 ? d > r - 1f : d < r - Groove + 1f;
                Over(x, y, lip ? TexelFx.GrooveLip : TexelFx.Groove, ground);
                if (lip || p <= 0f) return;
                float round = Mathf.Repeat(Mathf.PI * 0.5f - ang, Mathf.PI * 2f);   // 0 at the top, clockwise
                if (round > p * Mathf.PI * 2f) return;
                bool front = p < 1f && p * Mathf.PI * 2f - round < 0.12f;
                Over(x, y, front ? tip : tint, ground * breathe);
            });
            return true;
        }
    }

    /// <summary>The spire's damage lines (the user's pick, group 4 - "beam", shared with
    /// Projection's): a red edge round a white-hot core, flickering, bright pulses running out
    /// along each arm. On the danger rung - nothing on the floor covers them.</summary>
    public class SpireLines : TexelPicture
    {
        [SerializeField] Spire _s;

        protected override bool Draw()
        {
            if (_s == null) _s = GetComponentInParent<Spire>();
            if (_s == null || _s.LineFade <= 0f) return false;
            float inner = Tuning.Spire.BodyRadius * Ppu, outer = Tuning.Spire.CaptureRadius * Ppu;
            for (int i = 0; i < _s.LineCount; i++)
            {
                float a = (_s.LineAngle + 360f * i / _s.LineCount) * Mathf.Deg2Rad;
                float cx = Mathf.Cos(a), cy = Mathf.Sin(a);
                Beam(cx * inner, cy * inner, cx * outer, cy * outer, true, _s.LineFade,
                     TexelFx.Danger[3], TexelFx.Danger[1], TexelFx.Danger[0], Age);
            }
            return true;
        }
    }

    /// <summary>
    /// The spire's bubble (the user's pick, group 4 - "lattice dome"): a dome of faint blue lines
    /// over the ring in the barrier's charge colour, a brighter band sweeping up it - the room's
    /// own wall, never the Drowned Bell's water bubble. Two halves: the BACK half sorted behind
    /// anything standing in the ring, the FRONT half in front of it, so a body inside reads as
    /// inside.
    /// </summary>
    public class SpireDome : TexelPicture
    {
        [SerializeField] Spire _s;
        [SerializeField] bool _front;

        public static void Build(Spire s)
        {
            float r = Tuning.Spire.CaptureRadius, y = s.transform.position.y;
            int half = Mathf.CeilToInt(r * TexelFx.Body) + 2, up = Mathf.CeilToInt(r * 0.8f * TexelFx.Body) + 2;
            var back = Attach<SpireDome>(s.transform, "dome.back", Vector2.zero, half, half, half + up, SortingOrders.ForDepth(y + r) - 1);
            var front = Attach<SpireDome>(s.transform, "dome.front", Vector2.zero, half, half, half + up, SortingOrders.ForDepth(y - r) + SortingOrders.DepthStride - 1);
            front._front = true;
            back._s = front._s = s;
        }

        protected override bool Draw()
        {
            if (_s == null || _s.BubbleFade <= 0f) return false;
            float R = Tuning.Spire.CaptureRadius * Ppu, Hd = R * 0.8f, fade = _s.BubbleFade;
            var ramp = TexelFx.Charge;
            float sweep = Mathf.Repeat(Age * 12f, Hd * 1.4f);
            // Parallels: rings at heights up the dome, the near or far half of each.
            for (int z = 6; z < Hd; z += 7)
            {
                float rr = R * Mathf.Sqrt(Mathf.Max(0f, 1f - (z / Hd) * (z / Hd)));
                bool lit = Mathf.Abs(sweep - z) < 3f;
                Ring(0, z, rr - 1f, rr, (x, y, d, ang) =>
                {
                    if ((y - z < 0) != _front) return;
                    Over(x, y, lit ? ramp[0] : ramp[2], (_front ? 0.6f : 0.32f) * fade);
                });
            }
            // Meridians: lines from the rim up over the top, turning slowly.
            for (int k = 0; k < 12; k++)
            {
                float phi = k * Mathf.PI / 6f + Age * 0.3f, sn = Mathf.Sin(phi);
                if ((sn < 0f) != _front) continue;
                for (float t = 0f; t <= 1f; t += 0.02f)
                {
                    float rr = R * Mathf.Cos(t * Mathf.PI * 0.5f), z = Hd * Mathf.Sin(t * Mathf.PI * 0.5f);
                    Over(Mathf.RoundToInt(Mathf.Cos(phi) * rr), Mathf.RoundToInt(sn * rr + z), ramp[2], (_front ? 0.55f : 0.3f) * fade);
                }
            }
            // The rim where it meets the floor.
            Ring(0, 0, R - 1f, R, (x, y, d, ang) => { if ((y < 0) == _front) Over(x, y, ramp[1], (_front ? 0.8f : 0.45f) * fade); });
            return true;
        }
    }
}
