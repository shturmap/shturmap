using System.Text.RegularExpressions;

namespace Shturmap.Core.Logs;

/// <summary>
/// The game's build, as its log folders carry it ("log_2026.01.01_15-00-00_1.1.5.1.47510" → "1.1.5.1.47510"), and
/// whether one is newer than another: for the developer build's notice that a patch came, and with it the checks of
/// docs/UPDATES.md (owner, 2026-10-05).
/// </summary>
public static partial class GameBuild
{
    [GeneratedRegex(@"^log_\d{4}\.\d{2}\.\d{2}_\d{1,2}-\d{2}-\d{2}_(?<build>\d+(?:\.\d+)+)$", RegexOptions.CultureInvariant)]
    private static partial Regex SessionFolder();

    /// <summary>The build in a log session folder's name, or null when the name carries none.</summary>
    public static string? Of(string? sessionFolder) =>
        sessionFolder is not null && SessionFolder().Match(sessionFolder) is { Success: true } m ? m.Groups["build"].Value : null;

    /// <summary>
    /// Whether <paramref name="build"/> is newer than <paramref name="than"/>, part by part as numbers (the game's
    /// builds have five parts, more than <see cref="Version"/> takes). Anything is newer than nothing.
    /// </summary>
    public static bool IsNewer(string build, string? than)
    {
        if (string.IsNullOrEmpty(than))
            return true;
        var a = Parts(build);
        var b = Parts(than);
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y)
                return x > y;
        }
        return false;
    }

    /// <summary>
    /// What a newly seen build means against the newest seen before: the build to keep as the newest (null: keep
    /// what is kept), and whether to say that the checks after a patch are due. The first build ever seen is kept
    /// without a word: it isn't news, only where counting starts.
    /// </summary>
    public static (string? Keep, bool Notice) Seen(string? build, string? newestBefore) =>
        build is null || !IsNewer(build, newestBefore) ? (null, false) : (build, newestBefore is not null);

    private static long[] Parts(string build) =>
        build.Split('.').Select(p => long.TryParse(p, out var n) ? n : 0).ToArray();
}
