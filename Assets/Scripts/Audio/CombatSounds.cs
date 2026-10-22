using UnityEngine;
using Convergence.Combat;
using Convergence.Hazards;

namespace Convergence.Audio
{
    /// <summary>
    /// The fight's sounds, heard off each body's own <see cref="Health"/> events - attached once
    /// per Health at creation, beside <c>Hitstop.Attach</c>, so Health stays ignorant of audio.
    ///
    /// A hit says its WEIGHT: a basic, then a weapon art by the weight the player last fired
    /// (<c>PlayerController.LastArtWeight</c>, as the Drowned Bell reads it - a DamageInfo carries
    /// no weight). A crit rings over the top. Damage-over-time ticks (<see cref="DamageInfo.Tick"/>)
    /// and the ledger's prices make no sound - a burning enemy would thud twice a second.
    /// </summary>
    public static class CombatSounds
    {
        static Player.PlayerController _player;

        public static void AttachEnemy(Health hp)
        {
            if (hp == null) return;
            hp.Damaged += info => EnemyHit(hp, info);
            hp.Died += h => { if (h != null) Sfx.Play(Sound.EnemyDeath, h.transform.position); };
        }

        public static void AttachPlayer(Health hp)
        {
            if (hp == null) return;
            hp.Damaged += info =>
            {
                if (hp == null || info.Price || info.Tick || info.Amount <= 0f) return;
                Sfx.Play(Sound.PlayerHurt, hp.transform.position);
            };
            hp.Died += h => Sfx.Play(Sound.PlayerDeath);
        }

        static void EnemyHit(Health hp, DamageInfo info)
        {
            if (hp == null || info.Price || info.Tick || info.Amount <= 0f) return;
            Vector2 at = hp.transform.position;

            var sound = Sound.HitBasic;
            if (info.IsFinisher)
            {
                if (_player == null) _player = Object.FindAnyObjectByType<Player.PlayerController>();
                var w = _player != null ? _player.LastArtWeight : FinisherWeight.Light;
                sound = w == FinisherWeight.Heavy ? Sound.HitHeavy
                      : w == FinisherWeight.Medium ? Sound.HitMedium : Sound.HitLight;
            }
            Sfx.Play(sound, at);
            if (info.Crit) Sfx.Play(Sound.Crit, at);
        }

        /// <summary>The timing bar's verdict, as it settles - PERFECT is the sound worth chasing.</summary>
        public static void Verdict(StrikeVerdict v, Vector2 at)
        {
            switch (v)
            {
                case StrikeVerdict.Perfect: Sfx.Play(Sound.Perfect, at); break;
                case StrikeVerdict.Good: Sfx.Play(Sound.Good, at); break;
                case StrikeVerdict.Early: Sfx.Play(Sound.Miss, at); break;
                // MISSED (no press) stays silent: a hold-to-attack player would hear it every art.
            }
        }

        /// <summary>One footfall at <paramref name="at"/>, in the sound of the ground there.</summary>
        public static void Footstep(Vector2 at, float gain = 1f)
        {
            var pit = FloorPits.At(at);
            var sound = pit == null ? Sound.StepStone : pit.Kind switch
            {
                PitKind.Sand => Sound.StepSand,
                PitKind.Water => Sound.StepWater,
                PitKind.Fire => Sound.StepAsh,
                PitKind.Spike => Sound.StepMetal,
                _ => Sound.StepStone,
            };
            Sfx.Play(sound, at, gain);
        }
    }
}
