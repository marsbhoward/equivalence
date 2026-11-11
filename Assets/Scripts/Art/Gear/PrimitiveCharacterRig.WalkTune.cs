using UnityEngine;

namespace Convergence.Art.Gear
{
    // WALK MOCKS (2026-11-09, the user: the walk "seems very cartoonish"). Testing only: a rig with a
    // DevWalk set walks with these changes against the Gait partial's constants; null is the walk as
    // built. Nothing in play sets it.
    public partial class PrimitiveCharacterRig
    {
        public class WalkTune
        {
            public float HzMul = 1f;        // stride cycles a second
            public float StrideMul = 1f;    // sideways foot travel
            public float LiftMul = 1f;      // the swinging foot's lift
            public float DepthMul = 1f;     // WalkFootDepth: the forward foot lower on screen
            public float ApartWalk;         // feet held apart while walking, each, rig units
            public float BobMul = 1f;
            public float LeanAdd;           // degrees on top of WalkLeanDegrees
            public float ArmDegrees = -1f;  // shoulder swing, -1 = as built
            /// <summary>The stride follows the TRAVEL: across the screen it swings sideways; up or
            /// down the screen it goes into depth - the farther foot rides higher - with only
            /// <see cref="SideFloor"/> of the sideways swing.</summary>
            public bool Directional;
            public float SideFloor = 0.3f;
            public float FarLift = 0.05f;   // rig units, the farther foot at full stride
            // the RUN mock
            public float Stance = -1f;      // share of a leg's cycle on the ground (-1 = the walk's 0.6); under 0.5 has a flight
            public float LiftEarly = 0.8f;  // the swing's lift peaks earlier as this falls - a heel kick
            public float CrouchAdd;         // hips lower through the cycle, rig units
            public float ElbowAdd;          // elbow bend on top of the walk's, degrees - the runner's pump
        }

        [System.NonSerialized] public WalkTune DevWalk;

        /// <summary>Testing (proportion mocks): legs stretched by this (the figure raised to keep the
        /// feet on the floor), and the hip joints this many texels either side of centre (-1 = as built).</summary>
        [System.NonSerialized] public float DevLegScale = 1f, DevHipSpan = -1f;

        System.Collections.Generic.Dictionary<Transform, float> _devLegRest;

        /// <summary>The proportion mock: each leg sprite stretched where it hangs and the knee lowered
        /// to match (no scale on a pivot, so a bent knee never shears), the hips and torso raised so
        /// the feet stay on the floor. The leg's reach (SolveLeg) is lengthened by the same.</summary>
        void ApplyDevLegs()
        {
            if (DevLegScale == 1f && DevHipSpan < 0f && _devLegRest == null) return;
            float lift = LegLength * (DevLegScale - 1f);
            if (_hips) _hips.localPosition += Vector3.up * lift;
            if (_torso) _torso.localPosition += Vector3.up * lift;
            float span = DevHipSpan >= 0f ? DevHipSpan : Proportions.HipHalfSpanCells;
            _devLegRest ??= new System.Collections.Generic.Dictionary<Transform, float>();
            foreach (var leg in new[] { _legFront, _legBack })
            {
                if (leg == null) continue;
                leg.localPosition = PixelSprite.Px(leg == _legFront ? span : -span, 0f);
                foreach (Transform c in leg) Stretch(c);
            }

            void Stretch(Transform t)
            {
                if (!_devLegRest.TryGetValue(t, out float y)) _devLegRest[t] = y = t.localPosition.y;
                t.localPosition = new Vector3(t.localPosition.x, y * DevLegScale, t.localPosition.z);
                if (t.GetComponent<SpriteRenderer>() != null) t.localScale = new Vector3(t.localScale.x, DevLegScale, 1f);
                else foreach (Transform c in t) Stretch(c);
            }
        }

        float TuneHz => DevWalk != null ? DevWalk.HzMul : 1f;
        float TuneStance => DevWalk != null && DevWalk.Stance > 0f ? DevWalk.Stance : WalkStanceFraction;
        float TuneLiftEarly => DevWalk != null ? DevWalk.LiftEarly : 0.8f;
        float TuneCrouchAdd => DevWalk != null ? DevWalk.CrouchAdd : 0f;
        float TuneElbowAdd => DevWalk != null ? DevWalk.ElbowAdd : 0f;
        float TuneBob => DevWalk != null ? DevWalk.BobMul : 1f;
        float TuneLeanAdd => DevWalk != null ? DevWalk.LeanAdd : 0f;
        float TuneArmDegrees => DevWalk != null && DevWalk.ArmDegrees >= 0f ? DevWalk.ArmDegrees : WalkArmDegrees;
        float TuneApart => DevWalk != null ? DevWalk.ApartWalk * _gaitWeight : 0f;

        /// <summary>The foot's sideways travel and its extra height, for a leg at <paramref name="travel"/>.</summary>
        void TuneFoot(float travel, out float side, out float lift, out float depthMul)
        {
            side = WalkStride; lift = 1f; depthMul = 1f;
            float far = 0f;
            if (DevWalk != null)
            {
                side *= DevWalk.StrideMul; lift = DevWalk.LiftMul; depthMul = DevWalk.DepthMul;
                if (DevWalk.Directional && Body != null)
                {
                    var v = Body.linearVelocity; float sp = v.magnitude;
                    if (sp > 0.01f)
                    {
                        side *= Mathf.Lerp(DevWalk.SideFloor, 1f, Mathf.Abs(v.x) / sp);
                        // walking up the screen the leading foot is the farther; walking down, the trailing one
                        far = Mathf.Max(0f, travel * v.y / sp) * DevWalk.FarLift;
                    }
                }
            }
            _tuneFar = far;
        }
        float _tuneFar;
    }
}
