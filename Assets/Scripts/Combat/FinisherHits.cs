using Convergence.Player;

namespace Convergence.Combat
{
    /// <summary>
    /// Tells the player when one of THEIR finisher hits lands on an enemy - what the perfect
    /// streak needs to tell a perfect that connected from one swung at air. Every finisher path
    /// (swing, thrown blade, arrow, disc volley, leap, Separatio...) already marks its hits
    /// <see cref="DamageInfo.IsFinisher"/>, so hearing them on the body covers all of them
    /// without touching any. Attach/SetPlayer/Teardown mirror <see cref="Hitstop"/> exactly.
    /// </summary>
    public static class FinisherHits
    {
        static PlayerController _player;

        /// <summary>Set once from BuildPlayer.</summary>
        public static void SetPlayer(PlayerController player) => _player = player;

        /// <summary>Call once per enemy Health at creation, beside Hitstop.Attach.</summary>
        public static void Attach(Health hp)
        {
            hp.Damaged += info => OnDamaged(info);
        }

        static void OnDamaged(DamageInfo info)
        {
            if (_player == null || !info.IsFinisher || info.Source != _player.gameObject) return;
            _player.NoteFinisherHit();
        }

        public static void Teardown() => _player = null;
    }
}
