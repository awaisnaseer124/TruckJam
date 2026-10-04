namespace TankerJam.Game
{
    /// <summary>
    /// Presentation moments raised by GameController for audio, particles, haptics and analytics.
    /// Listeners must not change game state. The float argument is cue-specific (see each value).
    /// </summary>
    public enum GameCue
    {
        /// <summary>Blocked tap or no free bay. Arg: unused.</summary>
        Bonk,
        /// <summary>A truck starts driving to a bay. Arg: unused.</summary>
        Depart,
        /// <summary>A truck stops in its bay. Arg: unused.</summary>
        Brake,
        /// <summary>One glug while pouring. Arg: the receiving truck's fill fraction (0..1).</summary>
        Glug,
        /// <summary>A truck is full. Arg: unused.</summary>
        TruckFull,
        /// <summary>A full truck pulls out. Arg: unused.</summary>
        Leave,
        /// <summary>VIP lift starts. Arg: lift duration in seconds.</summary>
        VipLift,
        /// <summary>The extra bay was opened. Arg: unused.</summary>
        ExtraBayOpened,
        /// <summary>Level cleared. Arg: unused.</summary>
        Win,
        /// <summary>Level jammed. Arg: unused.</summary>
        Jam,
    }
}
