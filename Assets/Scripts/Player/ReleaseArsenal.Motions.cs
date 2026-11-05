using System.Collections;
using UnityEngine;
using Convergence.Combat;
using Convergence.Core;
using Convergence.Art.Gear;
using R = Convergence.Core.Tuning.Releases;
using M = Convergence.Combat.AttackMotion;

namespace Convergence.Player
{
    /// <summary>
    /// THE ARSENAL'S MOTIONS (2026-11-05): every release is swung, from the rig's own library, in the
    /// element's motion style (RIG.md - the rig styles the body, the motion is the weapon's). The hit
    /// lands on the drawn strike. A disc's delivery leaves the hand and comes back; a bow's is loosed
    /// at the end of its draw.
    /// </summary>
    public partial class ReleaseArsenal
    {
        static M Pick(WeaponClass w, M sword, M discs, M bow)
            => w switch { WeaponClass.Disc => discs, WeaponClass.Bow => bow, _ => sword };

        /// <summary>A full-meter release's motion: the Blast, the Buff, and taking up a Guard.</summary>
        public static M MotionFor(ElementType e, ReleaseKind k, WeaponClass w) => (e, k) switch
        {
            (ElementType.Fire, ReleaseKind.Blast) => Pick(w, M.Slam, M.DiscScatterBurst, M.LobDraw),          // Erupt
            (ElementType.Fire, ReleaseKind.Buff) => Pick(w, M.Rise, M.DiscUppercut, M.HighDraw),             // Ignite
            (ElementType.Water, ReleaseKind.Blast) => Pick(w, M.SunderSlam, M.DiscCarousel, M.LobDraw),      // Burst: the blade driven in
            (ElementType.Water, ReleaseKind.Buff) => Pick(w, M.Wind, M.DiscPirouette, M.TwinDraw),           // Surge
            (ElementType.Earth, ReleaseKind.Blast) => Pick(w, M.Slam, M.DiscLeap, M.KneelDraw),              // Quake
            (ElementType.Earth, ReleaseKind.Buff) => Pick(w, M.SunderSlam, M.DiscChop, M.KneelDraw),         // Tremor Stance
            (ElementType.Air, ReleaseKind.Blast) => Pick(w, M.Spin, M.DiscCarousel, M.CantedDraw),           // Squall
            (ElementType.Air, ReleaseKind.Buff) => Pick(w, M.CrescentRise, M.DiscPirouette, M.HighDraw),     // Gust
            _ => Pick(w, M.Rise, M.DiscBrake, M.TwinDraw),                                                   // a Guard taken up: a ready stance
        };

        /// <summary>A combo strike's motion, by its name (the user's picks; the weapon's personality).</summary>
        static M StrikeMotion(string strike, WeaponClass w) => strike switch
        {
            "Flare Jab" => Pick(w, M.Jab, M.DiscPunch, M.SnapDraw),
            "Flame Fan" => Pick(w, M.Sweep, M.DiscFanThrow, M.CantedDraw),
            "Backdraft" => Pick(w, M.Wind, M.DiscTetherCast, M.KneelDraw),
            "Ember Spiral" => Pick(w, M.Spin, M.DiscCarousel, M.KneelDraw),
            "Detonate" => Pick(w, M.Thrust, M.DiscScatterBurst, M.SnapDraw),
            "Pyre Drop" => Pick(w, M.StarLeap, M.DiscLeap, M.LobDraw),
            "Spray" => Pick(w, M.Sweep, M.DiscFanThrow, M.SnapDraw),
            "Eddy" => Pick(w, M.Spin, M.DiscCarousel, M.KneelDraw),
            "Breaker" => Pick(w, M.Rise, M.DiscCrossCut, M.HighDraw),
            "Draw In" => Pick(w, M.UndertowThrust, M.DiscTetherCast, M.RecoilDraw),
            "Rolling Wave" => Pick(w, M.Sweep, M.DiscSkip, M.KneelDraw),
            "Geyser Line" => Pick(w, M.Sweep, M.DiscMarkFlick, M.CantedDraw),
            "Lash" => Pick(w, M.Sweep, M.DiscTetherCast, M.CantedDraw),
            "Tidal Wall" => Pick(w, M.Slam, M.DiscSkip, M.RecoilDraw),
            "Spike Line" => Pick(w, M.SunderSlam, M.DiscChop, M.KneelDraw),
            "Tremor" => Pick(w, M.Slam, M.DiscCleaverChop, M.KneelDraw),
            "Monolith" => Pick(w, M.Rise, M.DiscUppercut, M.HighDraw),
            "Stomp" => Pick(w, M.Slam, M.DiscChop, M.SnapDraw),
            "Pillar Shove" => Pick(w, M.Thrust, M.DiscUppercut, M.KneelDraw),
            "Fault Rupture" => Pick(w, M.SunderSlam, M.DiscCleaverChop, M.KneelDraw),
            "Anvil Drop" => Pick(w, M.Slam, M.DiscLeap, M.HighDraw),
            "Feather Cut" => Pick(w, M.CrescentCut, M.DiscStepCut, M.FlipDraw),
            "Crescent" => Pick(w, M.CrescentCut, M.DiscCrowHop, M.RecoilDraw),
            "Whirl Step" => Pick(w, M.CycloneSpin, M.DiscPirouette, M.BowKneelRoll),
            "Gale Barrage" => Pick(w, M.Flurry, M.DiscWindmill, M.TwinDraw),
            "Cyclone" => Pick(w, M.CycloneSpin, M.DiscWindmill, M.CantedDraw),
            _ => Pick(w, M.Rise, M.DiscScatterBurst, M.HighDraw),   // Downburst
        };

        /// <summary>A move's motion.</summary>
        static M MoveMotion(ElementType e, WeaponClass w) => e switch
        {
            ElementType.Fire => Pick(w, M.StarLeap, M.DiscLeap, M.FlipDraw),              // Comet
            ElementType.Water => Pick(w, M.UndertowThrust, M.DiscCharge, M.BowRecoilHop), // Wavecrest
            ElementType.Earth => Pick(w, M.Wind, M.DiscBrake, M.BowRecoilHop),           // Mudslide
            _ => Pick(w, M.CrescentRise, M.DiscCrowHop, M.BowRecoilHop),                 // Zephyr, a blink
        };

        /// <summary>Swing <paramref name="motion"/> toward <paramref name="face"/> and wait for its strike.</summary>
        IEnumerator Swing(M motion, Vector2 face, float window = R.MotionWindow, bool alt = false)
        {
            if (Pc == null) yield break;
            float strike = Pc.PlayReleaseMotion(motion, window, face, alt);
            if (strike > 0f) yield return new WaitForSeconds(strike);
        }

        /// <summary>This strike's motion, swung toward <paramref name="face"/>, waited to its strike.</summary>
        IEnumerator SwingStrike(Vector2 face, float window = R.MotionWindow, bool alt = false)
            => Swing(StrikeMotion(StrikeName(Element, Weapon, _current), Weapon), face, window, alt);

        /// <summary>The strike playing now (0..2) - for its motion.</summary>
        int _current;

        /// <summary>A disc out of the hand for <paramref name="seconds"/> and back - the throw's picture,
        /// so a throw motion never leaves the disc drawn in the hand.</summary>
        IEnumerator DiscAway(Vector2 to, float seconds)
        {
            var rig = Pc != null ? Pc.Rig : null;
            rig?.SetWeaponVisible(false);
            yield return new WaitForSeconds(seconds);
            TexelFx.Mote(to, Me + Vector2.up * 0.35f, Ramp, 0.16f, null, TexelFx.Air);
            yield return new WaitForSeconds(0.16f);
            if (Pc != null) Pc.Rig?.SetWeaponVisible(true);
        }
    }
}
