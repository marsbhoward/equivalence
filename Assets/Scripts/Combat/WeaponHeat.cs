using System;
using UnityEngine;

namespace Convergence.Combat
{
    /// <summary>
    /// The fire blade's heat cycle: red, orange, yellow, white, blue, and back to red.
    ///
    /// One stage per COMPLETED finisher, and the blade's colour is the only readout. That is
    /// deliberate and it is a design position, not an oversight: there is no meter, no pip row and
    /// no number anywhere. A player who notices their sword has changed colour and goes looking
    /// for why has found something; a bar that counts to five for them has taken that away.
    ///
    /// The blade is legible enough to carry it - checked at the arena's real 2 screen pixels per
    /// art texel, where the five stages are still told apart at a glance.
    ///
    /// It RESTARTS in exactly one place: after blue's finisher releases, because blue is the
    /// payoff and a weapon that parks at its ceiling stops being a cycle.
    ///
    /// It used to restart on every floor as well, on the reasoning that heat should be built
    /// inside a fight rather than banked between them. That was wrong in practice - floors clear
    /// faster than five finishers land, so the cycle was being wiped before it could reach the
    /// stages it exists for, and the top half of the sword was effectively unreachable. Heat now
    /// carries across floors and only a RUN starts it over.
    ///
    /// That run reset is implicit and needs no method: <c>GameBootstrap.BuildPlayer</c> builds a
    /// fresh player per run and adds a fresh component with it, so a new run begins on red by
    /// construction.
    /// </summary>
    public class WeaponHeat : MonoBehaviour
    {
        public enum Stage { Red, Orange, Yellow, White, Blue }

        /// <summary>
        /// Radius added to the heat finisher's own reach, per stage.
        ///
        /// WHITE IS THE CEILING and blue matches it rather than exceeding it. Blue's reward is the
        /// burn, so letting it also reach further would make white a step you pass through rather
        /// than a stage you play - and the cycle only has five rungs to say anything with.
        /// </summary>
        static readonly float[] RadiusBonus = { 0f, 0.55f, 1.15f, 1.80f, 1.80f };

        /// <summary>
        /// Damage per second of the burn blue leaves on everything it catches, as a fraction of
        /// the blow that applied it.
        ///
        /// WEAKER than the Fire element's own 0.35 on purpose. This arrives on a finisher that any
        /// character can swing; the element's costs a whole build. A weapon that out-burned the
        /// element would make picking fire a downgrade for anyone holding this sword.
        /// </summary>
        public const float BlueBurnFraction = 0.18f;
        public const float BlueBurnSeconds = 3f;

        /// <summary>
        /// The flame colour of each stage, for anything that has to agree with the blade - the
        /// ground fire Conflagration leaves, principally.
        ///
        /// Kept here rather than read off the sprite, because the sprite is a texture and this is
        /// a decision. It matches the mid tone of each stage's ramp in DemoGear; if one moves,
        /// move both, or the fire on the ground stops being the fire on the sword.
        /// </summary>
        static readonly Color[] StageTint =
        {
            new(0.80f, 0.20f, 0.09f),   // red
            new(0.94f, 0.42f, 0.10f),   // orange
            new(0.99f, 0.68f, 0.15f),   // yellow
            new(1.00f, 0.86f, 0.55f),   // white
            new(0.36f, 0.68f, 1.00f),   // blue
        };

        public Color CurrentTint => StageTint[(int)Current];

        public Stage Current { get; private set; } = Stage.Red;

        /// <summary>Raised when the stage changes, so the rig can repaint the blade.</summary>
        public event Action<Stage> Changed;

        public float RadiusFor(Stage s) => RadiusBonus[(int)s];
        public float CurrentRadiusBonus => RadiusBonus[(int)Current];

        /// <summary>Only the top of the cycle leaves a burn behind.</summary>
        public bool AppliesBurn => Current == Stage.Blue;

        /// <summary>
        /// One completed heat finisher. Blue wraps back to red - it does not sit at the top.
        /// </summary>
        public void Advance()
        {
            Current = Current == Stage.Blue ? Stage.Red : (Stage)((int)Current + 1);
            Changed?.Invoke(Current);
        }
    }
}
