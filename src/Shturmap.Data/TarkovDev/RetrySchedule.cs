namespace Shturmap.Data.TarkovDev;

/// <summary>
/// How long to wait before asking tarkov.dev again after a failure that may pass (no connection, a timeout, a server
/// error): 2, 4, 8 and 16 minutes, then every 30. A success, or the player changing the game mode, starts over at 2.
/// Until 2026-10-04 it was every 2 minutes for as long as it failed: with tarkov.dev down, every running Shturmap
/// asked for up to 11 files every 2 minutes (owner, the same day: no unnecessary load on tarkov.dev). One schedule
/// for the game data, the game language's texts and the item sources.
/// </summary>
public static class RetrySchedule
{
    private static readonly int[] Minutes = [2, 4, 8, 16];

    /// <summary>The longest wait: what every try after the fourth waits.</summary>
    public static readonly TimeSpan Longest = TimeSpan.FromMinutes(30);

    /// <param name="failures">How many tries in a row have failed, this one included (1 after the first failure).</param>
    public static TimeSpan Wait(int failures) =>
        failures <= Minutes.Length ? TimeSpan.FromMinutes(Minutes[Math.Max(1, failures) - 1]) : Longest;

    /// <summary>A wait in words for a notice: "2 minutes", "30 minutes", "1 minute"; a shorter one (tests) in seconds.</summary>
    public static string InWords(TimeSpan wait)
    {
        if (wait < TimeSpan.FromMinutes(1))
            return wait.TotalSeconds is > 0.5 and < 1.5 ? "1 second" : $"{wait.TotalSeconds:0} seconds";
        var minutes = (int)Math.Round(wait.TotalMinutes);
        return minutes == 1 ? "1 minute" : $"{minutes} minutes";
    }
}
