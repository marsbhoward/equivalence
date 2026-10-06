using System;

namespace Convergence.Chain
{
    /// <summary>
    /// What belongs to the PLAYER rather than to any one character.
    ///
    /// The room and everything standing in it lives here. A character is a save file you switch
    /// between; the room is the place you switch between them IN, and it would be incoherent for
    /// your trophies to vanish because you decided to play your earth character today.
    ///
    /// KEYED BY WALLET ADDRESS when there is one, and by <see cref="LocalId"/> when there is not.
    /// That ordering matters: the game has to be completely playable with no wallet attached, so
    /// "not connected" is a normal state with its own account record rather than an error. When a
    /// wallet does connect, <see cref="Adopt"/> moves the local record onto that address instead
    /// of stranding it.
    /// </summary>
    [Serializable]
    public class AccountProfile
    {
        /// <summary>The account id used before any wallet is connected.</summary>
        public const string LocalId = "local";

        /// <summary>Wallet address, or <see cref="LocalId"/>. This is the storage key.</summary>
        public string AccountId = LocalId;

        /// <summary>
        /// The connected wallet, empty when there is none. Kept separately from
        /// <see cref="AccountId"/> so a record can remember which wallet it came from even if it
        /// is later loaded by some other key.
        /// </summary>
        public string WalletAddress = "";

        /// <summary>Trophies the player has placed around the hub, and where.</summary>
        public RoomLayout Room = new();

        /// <summary>
        /// Floors cleared today, and the UTC day they were counted against.
        ///
        /// ACCOUNT-LEVEL, NOT PER CHARACTER - the taper is an anti-farming measure and a player
        /// with several characters would otherwise get one fresh allowance per character, which is
        /// the exact hole it exists to close.
        ///
        /// Additive fields: an account saved before the taper existed loads at zero on a blank day
        /// and is treated as fresh, which is the only sensible reading of "we have never counted
        /// this before".
        /// </summary>
        public int FloorsClearedToday;

        /// <summary>The day FloorsClearedToday belongs to, as yyyy-MM-dd in UTC. UTC rather than
        /// local so a player cannot reset the count by changing timezone, and a plain string rather
        /// than a DateTime because JsonUtility does not serialise DateTime.</summary>
        public string TaperDayUtc = "";

        /// <summary>
        /// Rolls the daily counter over if the UTC day has changed, then returns today's count.
        /// Called before anything reads or writes it, so there is one place the rollover happens.
        /// </summary>
        public int TodaysFloors()
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (TaperDayUtc != today)
            {
                TaperDayUtc = today;
                FloorsClearedToday = 0;
            }
            return FloorsClearedToday;
        }

        public void CountFloorCleared()
        {
            TodaysFloors();
            FloorsClearedToday++;
        }

        public bool IsLocal => AccountId == LocalId;

        /// <summary>
        /// Re-key this record onto a wallet address.
        ///
        /// Used when a wallet connects and there is no record for it yet: the room the player
        /// already arranged follows them onto the address rather than being replaced by an empty
        /// one. If the address already HAS a record, the caller should load that instead - two
        /// arrangements cannot be merged automatically without silently overwriting one of them.
        /// </summary>
        public void Adopt(string walletAddress)
        {
            if (string.IsNullOrWhiteSpace(walletAddress)) return;
            AccountId = walletAddress;
            WalletAddress = walletAddress;
        }
    }
}
