using Shturmap.Game.Install;

namespace Shturmap.Session;

/// <summary>
/// What settings and the status bar say about the game's folder (owner, 2026-10-04: a way back from a chosen folder
/// to finding the game automatically; docs/DESIGN.md, "No game on this PC"): where the path comes from, and whether
/// another install has newer game logs than the one the player chose. Session dates only, facts read from the logs'
/// folder names, never a guess about which install is played.
/// </summary>
public static class GameFolder
{
    /// <summary>"GAME FOLDER: C:\… (chosen by you)", "(found automatically)", or "NOT FOUND".</summary>
    public static string Label(GameLocations? locations) => locations?.Install is not { } install
        ? "GAME FOLDER: NOT FOUND"
        : $"GAME FOLDER: {install.Root} ({(install.Kind == InstallKind.Manual ? "chosen by you" : "found automatically")})";

    /// <summary>
    /// Another install found by discovery whose newest log session is newer than that of the folder the player chose:
    /// the hint that the choice may not be the game they play. Null when the folder wasn't chosen, or nothing is newer.
    /// </summary>
    public static InstallCandidate? NewerElsewhere(GameLocations? locations)
    {
        if (locations?.Install is not { Kind: InstallKind.Manual } chosen)
            return null;
        return locations.Candidates
            .Where(c => c.Kind != InstallKind.Manual && c.IsValid && c.NewestSession is not null)
            .Where(c => !string.Equals(c.Root, chosen.Root, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(c.LogsFolder, chosen.LogsFolder, StringComparison.OrdinalIgnoreCase))
            .Where(c => chosen.NewestSession is null || c.NewestSession > chosen.NewestSession)
            .OrderByDescending(c => c.NewestSession)
            .FirstOrDefault();
    }

    /// <summary>The LOGS light's tooltip: which logs are followed, and the newer-logs hint when there is one.</summary>
    public static string LogsTip(GameLocations? locations)
    {
        if (locations?.LogsFolder is not { } logs)
            return "The game's logs aren't found: quests and raids can't follow the game.";
        var tip = "Game logs: " + logs;
        return NewerElsewhere(locations) is { } newer
            ? $"{tip}\nNewer game logs in {newer.Root}. Settings → FIND AUTOMATICALLY follows those instead."
            : tip;
    }
}
