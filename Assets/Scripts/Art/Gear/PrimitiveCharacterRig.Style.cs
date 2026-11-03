using UnityEngine;
using Convergence.Core;

namespace Convergence.Art.Gear
{
    // --------------------------------------------------------- element motion styles
    //
    // A weapon art's BODY shows the player's element (RIG.md, "Element motion styles"; the user's
    // picks 2026-11-03 off frames captured from this rig - Editor/MotionSheet). DRAWING ONLY, and
    // never the arms: the arcs, grips and hand-offs are untouched by construction, so the lead-in
    // checks still hold and the strike lands on the same frame. What a style may change:
    //
    //   the HOLD     the timing bar's wind-up: an extra body pose at full bar, how hard it trembles,
    //                and a rhythm on top (a breath in the hips, a sway in the lean, a bounce). A bow
    //                has no hold - the bar times its DRAW - so the draw takes the hold's picture.
    //   the STRIKE   the body's arrival inside the strike window (a warp of its clock - early, late,
    //                smooth) and how big each beat is
    //   the SETTLE   held, rebounding, or relaxed home early; a whole-cell shake at impact
    //   the REACH    scaled, overshooting
    //   a HOP        on the strike
    //   the CAPE     its springs, how hard the step kicks it, a wind toward the target in the hold
    //
    // PlayerController sets it as an art starts (SetArtElement) and clears it on a basic; the rig
    // lets it go itself once the swing has settled, so the cape's springs never outlive the art.
    public partial class PrimitiveCharacterRig
    {
        sealed class MotionStyle
        {
            public string Name;
            public SwingBody Hold;               // added to HoldLoad at full bar
            public float HoldShake = 1f;         // x the hold's tremble
            public float HoldBreathe;            // rig units of crouch, in and out
            public float HoldBreatheHz = 3f;
            public float HoldSway;               // degrees of lean, side to side
            public float HoldSwayHz = 1.6f;
            public float HoldBounce;             // rig units of hop, on the toes
            public float HoldBounceHz = 3.5f;
            public float LoadScale = 1f, StrikeScale = 1f, SettleScale = 1f;
            public SwingBody StrikeAdd;          // added through the strike and held into the settle
            public float CrouchScale = 1f, StepScale = 1f;
            public float StrikeWarp = 1f;        // the body's clock in the strike: t^p (p<1 early, p>1 late)
            public bool StrikeSmooth;            // smoothstep instead - no hard arrival
            public float SettleRelax;            // 0 holds the settle; 1 eases all the way home
            public float SettleRebound;          // rig units the hips bounce up after landing
            public float ReachScale = 1f;
            public float ReachOvershoot;         // share of the reach thrown past and pulled back
            public float StrikeHop;              // rig units
            public float ImpactShake;            // layout cells, whole
            public float ImpactShakeSeconds = 0.2f;
            public float CapeStiff = 1f, CapeDamp = 1f, CapeGain = 1f;
            public float CapeWind;               // degrees toward the target at full bar
        }

        static MotionStyle[] _styles;

        // The mock round's twelve (2026-11-03), kept whole for Editor/MotionSheet; the picks are
        // built from them (StyleFor).
        static MotionStyle[] Styles => _styles ??= new[]
        {
            new MotionStyle { Name = "Today" },

            // ---- Fire: aggressive, comes IN
            new MotionStyle { Name = "Fire A: Pounce", Hold = new(-0.010f, 0.010f, 0.012f, -4f), HoldShake = 0.6f,
                StrikeScale = 1.25f, SettleScale = 1.2f, StrikeAdd = new(0.010f, 0.015f, 0.045f, -6f),
                StrikeWarp = 0.7f, ReachScale = 1.6f, CapeGain = 1.3f },
            new MotionStyle { Name = "Fire B: Coil and snap", Hold = new(0.030f, 0.020f, -0.030f, 7f), HoldShake = 1.8f,
                StrikeScale = 1.15f, StrikeWarp = 0.5f, SettleRebound = 0.016f, ReachScale = 1.2f },
            new MotionStyle { Name = "Fire C: Stoke", HoldBreathe = 0.018f, HoldBreatheHz = 3f, HoldShake = 0.5f,
                StrikeScale = 1.2f, SettleScale = 1.15f, StrikeAdd = new(0f, 0f, 0.01f, -4f), ImpactShake = 1f, ImpactShakeSeconds = 0.12f },

            // ---- Water: flowing, no hard stops
            new MotionStyle { Name = "Water A: Flow through", HoldSway = 5f, HoldSwayHz = 1.6f, HoldShake = 0f,
                StrikeSmooth = true, ReachOvershoot = 0.5f, SettleRelax = 0.6f, CapeStiff = 0.5f, CapeDamp = 0.5f, CapeGain = 1.5f },
            new MotionStyle { Name = "Water B: Undertow", Hold = new(0.030f, 0.020f, -0.045f, 6f), HoldShake = 0.4f,
                StrikeAdd = new(0f, 0.010f, 0.030f, -3f), StrikeSmooth = true, ReachScale = 1.7f, SettleRelax = 0.8f, CapeStiff = 0.6f },
            new MotionStyle { Name = "Water C: Current", HoldSway = 2f, HoldShake = 0.5f,
                CapeStiff = 0.35f, CapeDamp = 0.4f, CapeGain = 2f },

            // ---- Earth: heavy, stands still
            new MotionStyle { Name = "Earth A: Root", Hold = new(0.035f, 0.050f, 0f, 0f), HoldShake = 0.3f,
                CrouchScale = 1.35f, StepScale = 0f, ReachScale = 0f, StrikeAdd = new(0f, 0.030f, 0f, 0f),
                ImpactShake = 1f, CapeStiff = 1.6f, CapeGain = 0.5f },
            new MotionStyle { Name = "Earth B: Anvil drop", Hold = new(-0.030f, -0.010f, 0f, 3f), HoldShake = 0.6f,
                StrikeWarp = 2.2f, CrouchScale = 1.6f, ReachScale = 0.5f, ImpactShake = 2f, ImpactShakeSeconds = 0.25f },
            new MotionStyle { Name = "Earth C: Boulder", Hold = new(0.050f, 0.030f, 0f, 2f), HoldShake = 0f,
                StrikeWarp = 1.6f, StrikeScale = 1.2f, SettleScale = 1.2f, ImpactShake = 1f,
                CapeStiff = 1.5f, CapeGain = 0.4f },

            // ---- Air: light, keeps moving
            new MotionStyle { Name = "Air A: Spring", HoldBounce = 0.030f, HoldShake = 0.3f, CrouchScale = 0.5f,
                StrikeHop = 0.06f, ReachOvershoot = 0.6f, SettleRelax = 1f, CapeGain = 1.4f },
            new MotionStyle { Name = "Air B: Gale lean", Hold = new(0f, 0f, 0f, 8f), HoldShake = 0.5f, CapeWind = 30f,
                CrouchScale = 0.6f, ReachScale = 1.2f, CapeStiff = 1.3f, CapeDamp = 0.6f },
            new MotionStyle { Name = "Air C: Featherstep", HoldShake = 0.4f, CrouchScale = 0.3f, StepScale = 1.6f,
                StrikeHop = 0.04f, ReachScale = 1.3f, CapeStiff = 0.5f, CapeDamp = 0.6f },
        };

        /// <summary>Names of the prototype styles, index = <see cref="SetMotionStyle"/>'s id.</summary>
        public static string[] MotionStyleNames
        {
            get { var s = Styles; var n = new string[s.Length]; for (int i = 0; i < s.Length; i++) n[i] = s[i].Name; return n; }
        }

        // ---- the picks, per class (2026-11-03). Index into Styles where one prototype was picked
        // whole; the merged picks are built below.
        static MotionStyle _airMerged, _discWater, _discEarth, _bowFire;

        /// <summary>The style a weapon art plays for <paramref name="element"/> on <paramref name="weapon"/>.</summary>
        static MotionStyle StyleFor(ElementType element, WeaponClass weapon)
        {
            var s = Styles;
            switch (element)
            {
                case ElementType.Fire:
                    return weapon == WeaponClass.Bow ? (_bowFire ??= BowPounce(s[1])) : s[2];   // Pounce : Coil and snap
                case ElementType.Water:
                    if (weapon == WeaponClass.Disc) return _discWater ??= FlowWithCurrent(s[4], s[6]);
                    return weapon == WeaponClass.Bow ? s[5] : s[4];                              // Undertow : Flow through
                case ElementType.Earth:
                    if (weapon == WeaponClass.Disc) return _discEarth ??= RootedAnvil(s[7], s[8]);
                    return weapon == WeaponClass.Bow ? s[8] : s[7];                              // Anvil drop : Root
                default:
                    return _airMerged ??= GaleFeather(s[11], s[12]);                             // every class
            }
        }

        // Gale lean's hold (leaning back, the cape blown at the target) into Featherstep's strike
        // (upright, a long light step and a hop). The cape: Gale's snap back, Featherstep's float -
        // left at its own stiffness, damped as lightly as either.
        static MotionStyle GaleFeather(MotionStyle gale, MotionStyle feather) => new()
        {
            Name = "Air: Gale lean + Featherstep",
            Hold = gale.Hold, HoldShake = Mathf.Min(gale.HoldShake, feather.HoldShake), CapeWind = gale.CapeWind,
            CrouchScale = feather.CrouchScale, StepScale = feather.StepScale, StrikeHop = feather.StrikeHop,
            ReachScale = feather.ReachScale, CapeStiff = 1f, CapeDamp = Mathf.Min(gale.CapeDamp, feather.CapeDamp),
        };

        // Flow through's body with Current's loose cape.
        static MotionStyle FlowWithCurrent(MotionStyle flow, MotionStyle current) => new()
        {
            Name = "Water: Flow through + Current",
            HoldSway = flow.HoldSway, HoldSwayHz = flow.HoldSwayHz, HoldShake = flow.HoldShake,
            StrikeSmooth = true, ReachOvershoot = flow.ReachOvershoot, SettleRelax = flow.SettleRelax,
            CapeStiff = current.CapeStiff, CapeDamp = current.CapeDamp, CapeGain = current.CapeGain,
        };

        // Anvil drop's rise and accelerating fall on Root's planted feet: risen tall but with the
        // feet wide, no step and no reach, the deeper crouch and the heavier shake, Root's heavy cape.
        static MotionStyle RootedAnvil(MotionStyle root, MotionStyle anvil) => new()
        {
            Name = "Earth: Root + Anvil drop",
            Hold = new(anvil.Hold.Crouch, root.Hold.Stance, 0f, anvil.Hold.Lean), HoldShake = anvil.HoldShake,
            StrikeWarp = anvil.StrikeWarp, CrouchScale = anvil.CrouchScale, StepScale = 0f, ReachScale = 0f,
            StrikeAdd = root.StrikeAdd, ImpactShake = anvil.ImpactShake, ImpactShakeSeconds = anvil.ImpactShakeSeconds,
            CapeStiff = root.CapeStiff, CapeGain = root.CapeGain,
        };

        // Pounce on a bow: its shallow coil, bigger beats and early arrival, but no step - a bow
        // makes space and never steps in (ReachOf; its reach is already zero).
        static MotionStyle BowPounce(MotionStyle pounce) => new()
        {
            Name = "Fire: Pounce (bow)",
            Hold = new(pounce.Hold.Crouch, pounce.Hold.Stance, 0f, pounce.Hold.Lean), HoldShake = pounce.HoldShake,
            StrikeScale = pounce.StrikeScale, SettleScale = pounce.SettleScale,
            StrikeAdd = new(pounce.StrikeAdd.Crouch, pounce.StrikeAdd.Stance, 0f, pounce.StrikeAdd.Lean),
            StrikeWarp = pounce.StrikeWarp, StepScale = 0f, ReachScale = 0f, CapeGain = pounce.CapeGain,
        };

        /// <summary>A weapon art's element (null for a basic): the style the swings that follow play.</summary>
        public void SetArtElement(ElementType? element, WeaponClass weapon)
        {
            _style = element.HasValue ? StyleFor(element.Value, weapon) : null;
            _styleSinceStrike = -1f;
        }

        /// <summary>Seconds past a settled swing a style is let go (the cape's springs come home).</summary>
        const float StyleReleaseSeconds = 0.6f;

        [System.NonSerialized] MotionStyle _style;
        [System.NonSerialized] float _styleSinceStrike = -1f;

        /// <summary>Testing (Editor/MotionSheet): shade the swings that follow by one of the round's
        /// styles, by index into <see cref="MotionStyleNames"/> (0 = none).</summary>
        public void SetMotionStyle(int id)
        {
            var s = Styles;
            _style = id > 0 && id < s.Length ? s[id] : null;
        }

        /// <summary>Testing: a fixed frame time for a harness that steps the rig itself (-1 = off).</summary>
        public static float DevDt = -1f;

        /// <summary>The body for this frame of the swing, through the style if one is set.</summary>
        SwingBody StyledBodyFor(float k)
        {
            var st = _style;
            if (st == null) return BodyFor(_motion, k, _attackLength);
            StrikePhases(_motion, _attackLength, out float cockEnd, out float strikeEnd);
            float kw = k;
            if (k >= cockEnd && k < strikeEnd && strikeEnd > cockEnd)
            {
                float t = (k - cockEnd) / (strikeEnd - cockEnd);
                t = st.StrikeSmooth ? Mathf.SmoothStep(0f, 1f, t) : Mathf.Pow(t, st.StrikeWarp);
                kw = cockEnd + t * (strikeEnd - cockEnd);
            }
            // Only the swing's own movement is shaded - the pose the motion starts from (a bow's kneel
            // or recoil, the connector a lead-in left) is kept whole, or the art's stance is lost.
            var zero = BodyFor(_motion, 0f, _attackLength);
            var body = BodyFor(_motion, kw, _attackLength) + zero * -1f;
            float scale, add;
            if (k < cockEnd) { scale = st.LoadScale; add = 0f; }
            else if (k < strikeEnd) { scale = st.StrikeScale; add = 1f - (1f - (k - cockEnd) / Mathf.Max(1e-4f, strikeEnd - cockEnd)) * (1f - (k - cockEnd) / Mathf.Max(1e-4f, strikeEnd - cockEnd)); }
            else { scale = st.SettleScale; add = 1f; }
            body = body * scale + st.StrikeAdd * add;
            body.Crouch *= st.CrouchScale;
            body.Step *= st.StepScale;
            if (k >= strikeEnd && strikeEnd < 1f)
            {
                float u = (k - strikeEnd) / (1f - strikeEnd);
                if (st.SettleRelax > 0f) body = SwingBody.Lerp(body, default, st.SettleRelax * Mathf.SmoothStep(0f, 1f, u));
                if (st.SettleRebound > 0f) body.Crouch -= st.SettleRebound * Mathf.Sin(Mathf.PI * Mathf.Clamp01(u / 0.3f));
            }
            return zero + body;
        }

        /// <summary>The style's extra for the hold at <paramref name="t"/> (0..1 of the bar), on
        /// <paramref name="clock"/> seconds into it. A bow has no hold - its bar times the DRAW - so
        /// the draw takes the hold's picture instead (see StyleDrawHold).</summary>
        SwingBody StyleHold(float t, ref float shake, float clock)
        {
            var st = _style;
            if (st == null) return default;
            shake *= st.HoldShake;
            var b = st.Hold * Mathf.SmoothStep(0f, 1f, t);
            if (st.HoldBreathe > 0f) b.Crouch += st.HoldBreathe * t * (0.5f - 0.5f * Mathf.Cos(clock * st.HoldBreatheHz * Mathf.PI * 2f));
            if (st.HoldSway > 0f) b.Lean += st.HoldSway * Mathf.Min(1f, t * 2f) * Mathf.Sin(clock * st.HoldSwayHz * Mathf.PI * 2f);
            return b;
        }

        /// <summary>The style's hop this frame: a bounce in the hold, a lift through the strike.</summary>
        float StyleHop(float k)
        {
            var st = _style;
            if (st == null) return 0f;
            if (_attackHold > 0f || BowDrawing)
            {
                if (st.HoldBounce <= 0f) return 0f;
                return st.HoldBounce * Mathf.Abs(Mathf.Sin(HoldClock * st.HoldBounceHz * Mathf.PI));
            }
            if (st.StrikeHop <= 0f) return 0f;
            StrikePhases(_motion, _attackLength, out float cockEnd, out float strikeEnd);
            if (k < cockEnd || k >= strikeEnd) return 0f;
            return st.StrikeHop * Mathf.Sin(Mathf.PI * (k - cockEnd) / Mathf.Max(1e-4f, strikeEnd - cockEnd));
        }

        /// <summary>The reach through the style: scaled, and thrown past full stretch early in the settle.</summary>
        float StyleReach(float reach, float k)
        {
            var st = _style;
            if (st == null) return reach;
            reach *= st.ReachScale;
            if (st.ReachOvershoot > 0f)
            {
                StrikePhases(_motion, _attackLength, out _, out float strikeEnd);
                if (k >= strikeEnd && strikeEnd < 1f)
                {
                    float u = (k - strikeEnd) / (1f - strikeEnd);
                    if (u < 0.4f) reach += ReachOf(_motion) * st.ReachScale * st.ReachOvershoot * Mathf.Sin(Mathf.PI * u / 0.4f);
                }
            }
            return reach;
        }

        /// <summary>Marks the strike's end for the impact shake (once per swing).</summary>
        void TickStyleImpact(float k, float dt)
        {
            if (_style == null || _attackHold > 0f) { _styleSinceStrike = -1f; return; }
            StrikePhases(_motion, _attackLength, out _, out float strikeEnd);
            if (k >= strikeEnd && _styleSinceStrike < 0f) _styleSinceStrike = 0f;
            else if (_styleSinceStrike >= 0f) _styleSinceStrike += dt;
        }

        /// <summary>The impact shake: whole layout cells, side to side, dying away.</summary>
        float StyleShakeX()
        {
            var st = _style;
            if (st == null || st.ImpactShake <= 0f || _attackTimer <= 0f || _styleSinceStrike < 0f || _styleSinceStrike > st.ImpactShakeSeconds) return 0f;
            float left = 1f - _styleSinceStrike / st.ImpactShakeSeconds;
            float cells = Mathf.Round(st.ImpactShake * left + 0.49f) * (Mathf.Sin(_styleSinceStrike * 2f * Mathf.PI * 22f) >= 0f ? 1f : -1f);
            return PixelSprite.Px(cells);
        }

        /// <summary>A bow's arrow is on the string and drawing: the bow's wind-up.</summary>
        bool BowDrawing => HeldBow && _awaitLoose && _attackTimer > 0f && _attackHold <= 0f;

        /// <summary>Seconds into the wind-up: the hold's, or the bow's draw.</summary>
        float HoldClock => BowDrawing ? _attackLength - _attackTimer : _holdElapsed;

        /// <summary>The hold's picture laid over a bow's draw, by how far the draw has come.</summary>
        SwingBody StyleDrawHold(float k)
        {
            if (_style == null || !BowDrawing) return default;
            float none = 0f;
            return StyleHold(Mathf.Clamp01(k), ref none, HoldClock);
        }

        // The cape's springs under the style - read by AnimateCape.
        float StyleCapeStiff => _style?.CapeStiff ?? 1f;
        float StyleCapeDamp => _style?.CapeDamp ?? 1f;
        float StyleCapeGain => _style?.CapeGain ?? 1f;

        /// <summary>Degrees the cape is blown toward the target this frame (the hold's wind).</summary>
        float StyleCapeWind()
        {
            var st = _style;
            if (st == null || st.CapeWind == 0f) return 0f;
            if (BowDrawing) return st.CapeWind * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(SwingKNow));
            if (_attackHold <= 0f) return 0f;
            float held = _holdElapsed + _attackHold - HoldOvershoot;
            return st.CapeWind * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_holdElapsed / Mathf.Max(0.05f, held)));
        }
    }
}
