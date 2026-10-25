using System.Collections.Generic;
using Convergence.Combat;

namespace Convergence.Art.Gear
{
    /// <summary>
    /// A PORTRAIT POSE - how the character stands in the PFP (the user's calls, 2026-10-25;
    /// MONETIZATION.md "The plan as of 2026-10-25"). Rolled at 1% per avatar kill from the poses a
    /// character lacks, never duplicated, UNTRADEABLE: held on the character's record, never a
    /// token, so a rare pose says the character earned it.
    ///
    /// A pose is one FROZEN FRAME of a motion the rig already plays (<see cref="ICharacterRig.HoldPose"/>):
    /// the connectors the weapon arts start from and the arts' own peaks. Drawn by the rig, so every
    /// body, every piece of gear and every weapon class wears it with nothing new to author - the
    /// weapon goes where that frame puts the hand.
    ///
    /// <see cref="Id"/> is PERMANENT - a character's record names it - so a pose is appended, never
    /// renamed or re-keyed. Its frame may be retuned (that changes a picture, not what was earned).
    /// </summary>
    public sealed class PoseDef
    {
        public readonly string Id, Name, Description;
        public readonly AttackMotion Motion;
        /// <summary>Where on the motion's clock the frame is held, 0 (first frame) to 1 (last).</summary>
        public readonly float K;
        public readonly bool Alt;

        public PoseDef(string id, string name, AttackMotion motion, float k, string description, bool alt = false)
        {
            Id = id; Name = name; Motion = motion; K = k; Description = description; Alt = alt;
        }
    }

    public static class PoseCatalog
    {
        /// <summary>The ROLLED pool - ten, the user's planned count. FEAT poses (earned by a deed)
        /// will sit outside it and are not built yet. The names and frames are mine, unconfirmed.</summary>
        public static readonly PoseDef[] All =
        {
            new("pose.overhead", "Overhead",      AttackMotion.AscentPivot,  1.00f, "The weapon raised high overhead in both hands."),
            new("pose.hanging",  "Hanging Guard", AttackMotion.CrescentRise, 1.00f, "The weapon held level behind the shoulder."),
            new("pose.guard",    "Guard",         AttackMotion.ReturnSweep,  1.00f, "Square on, the weapon held level in front."),
            new("pose.coil",     "Coil",          AttackMotion.UndertowDraw, 1.00f, "Crouched, the weapon drawn back low behind the body."),
            new("pose.planted",  "Planted",       AttackMotion.SunderSlam,   0.92f, "Sunk low, the weapon driven point first at the floor."),
            new("pose.salute",   "Salute",        AttackMotion.WhirlThrow,   0.30f, "The weapon held upright before the face."),
            new("pose.vigil",    "Vigil",         AttackMotion.Plant,        0.40f, "Standing tall, the weapon point down in front."),
            new("pose.ready",    "Ready",         AttackMotion.Throw,        0.00f, "The weapon held across the chest."),
            new("pose.wind",     "Wind",          AttackMotion.Cast,         0.00f, "Leaning back, the weapon swung low across the body."),
            new("pose.leap",     "Leap",          AttackMotion.DiscLeap,     0.42f, "At the top of a jump, the weapon swept down."),
        };

        static Dictionary<string, PoseDef> _byId;

        public static PoseDef Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, PoseDef>();
                foreach (var p in All) _byId[p.Id] = p;
            }
            return _byId.TryGetValue(id, out var pose) ? pose : null;
        }
    }
}
