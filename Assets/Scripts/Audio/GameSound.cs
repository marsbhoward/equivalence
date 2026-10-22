using UnityEngine;

namespace Convergence.Audio
{
    /// <summary>
    /// Real recordings, when there are any - the sound twin of <c>GameArt</c>. Every slot is
    /// optional: a blank one plays <see cref="Synth"/>'s placeholder. Lives at
    /// <c>Assets/Resources/GameSound.asset</c> (create it from the Assets menu); with no asset at all
    /// every sound is the placeholder. An array slot is variants - one is picked at random each play.
    /// </summary>
    [CreateAssetMenu(menuName = "Convergence/Game Sound", fileName = "GameSound")]
    public class GameSound : ScriptableObject
    {
        [Header("Footsteps")]
        public AudioClip[] StepStone, StepSand, StepWater, StepAsh, StepMetal;

        [Header("Combat")]
        public AudioClip[] HitBasic, HitLight, HitMedium, HitHeavy, Crit;
        public AudioClip[] PlayerHurt, EnemyDeath;
        public AudioClip PlayerDeath;

        [Header("Timing bar")]
        public AudioClip Perfect, Good, Miss;

        [Header("The player")]
        public AudioClip[] Swing, SwingHeavy;
        public AudioClip ReleaseFire, ReleaseWater, ReleaseEarth, ReleaseAir;

        [Header("Enemies")]
        public AudioClip[] Growl, Snarl, Zap, Lob, Explosion, Hissing, Crackle, Grind, Slam, Roar,
                           Bubbling, Pop, Flame, Swipe;
        public AudioClip Charge, FuseTick;

        [Header("The run")]
        public AudioClip FloorClear, DoorOpen;

        [Header("Bosses (Sing and Toll: one clip per Cantor slice, in slice order)")]
        public AudioClip[] Sing, Toll, Erupt, Blink, Rattle;
        public AudioClip Enrage, Vulnerable, Seal, BossDeath, GazeLook, Petrify, Push, Pull;

        [Header("Stone")]
        public AudioClip[] Chip, Rubble;

        [Header("Ambience (loops)")]
        public AudioClip BedHub, BedCrypt, BedDeep, BedAbyss, BedThrone, BedBoss, BedForest, BedSky, BedSalt;
        public AudioClip[] Distant, DistantForest, DistantSky, DistantSalt;
        [Tooltip("An enemy noticing the player")] public AudioClip[] Alert;

        static GameSound _loaded;
        static bool _looked;

        /// <summary>The project's asset, or null. Looked up once.</summary>
        public static GameSound Instance
        {
            get
            {
                if (!_looked) { _looked = true; _loaded = Resources.Load<GameSound>("GameSound"); }
                return _loaded;
            }
        }
    }
}
