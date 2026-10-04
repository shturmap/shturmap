using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Shturmap.Game.Install;

public enum InstallKind
{
    BsgLauncher,
    Steam,
    Manual,
}

/// <summary>A folder that may hold the game, and what discovery concluded about it.</summary>
/// <param name="Root">The install folder (for Steam, the "build" folder that holds the exe).</param>
/// <param name="LogsFolder">The folder holding log_* sessions, if one was found.</param>
/// <param name="NewestSession">Start time of the newest log session, from its folder name.</param>
/// <param name="Found">How it was found, for the Game files diagnostics.</param>
/// <param name="Rejected">Why it does not count, or null if it does.</param>
public sealed record InstallCandidate(
    InstallKind Kind,
    string Root,
    string? LogsFolder,
    DateTime? NewestSession,
    string Found,
    string? Rejected)
{
    public bool IsValid => Rejected is null;
}

public sealed record GameLocations(
    InstallCandidate? Install,
    IReadOnlyList<InstallCandidate> Candidates,
    string ScreenshotsFolder,
    string SettingsFolder)
{
    public string? LogsFolder => Install?.LogsFolder;

    /// <summary>Log folders of every valid install, newest first: all of them count for quest history.</summary>
    public IEnumerable<string> AllLogsFolders =>
        Candidates.Where(c => c.IsValid && c.LogsFolder is not null).OrderByDescending(c => c.NewestSession).Select(c => c.LogsFolder!).Distinct(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Finds Escape from Tarkov whether it was installed by the Battlestate Games launcher or by Steam. Neither is
/// a fallback for the other: every candidate is collected, invalid ones are rejected with a reason, and when
/// several remain, the one with the newest log session is the live one.
/// </summary>
public sealed partial class InstallLocator(IGameEnvironment env)
{
    public const string SteamAppId = "3932890";

    private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";

    [GeneratedRegex(@"""path""\s+""(?<path>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex VdfPath();

    [GeneratedRegex(@"""installdir""\s+""(?<dir>[^""]+)""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AcfInstallDir();

    // log_2026.01.01_15-00-00_1.1.5.1.47510
    [GeneratedRegex(@"^log_(?<ts>\d{4}\.\d{2}\.\d{2}_\d{1,2}-\d{2}-\d{2})", RegexOptions.CultureInvariant)]
    private static partial Regex SessionFolder();

    /// <param name="manualInstallFolder">The folder the player chose ("Choose game folder…"), which wins when it holds the
    /// game.</param>
    /// <param name="discover">False to look only at the chosen folder: the developer switch <c>--no-game</c>, so the
    /// no-game state can be seen on a PC that has the game.</param>
    public GameLocations Locate(string? manualInstallFolder = null, bool discover = true)
    {
        var candidates = new List<InstallCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Consider(InstallKind kind, string? folder, string found)
        {
            if (string.IsNullOrWhiteSpace(folder))
                return;
            var normalized = Normalize(folder);
            if (!seen.Add(normalized))
                return;
            candidates.Add(Evaluate(kind, normalized, found));
        }

        if (manualInstallFolder is not null)
            Consider(InstallKind.Manual, manualInstallFolder, "chosen in Shturmap");

        if (discover)
        {
            foreach (var (folder, found) in BsgLauncherFolders())
                Consider(InstallKind.BsgLauncher, folder, found);

            foreach (var (folder, found) in SteamFolders())
                Consider(InstallKind.Steam, folder, found);
        }

        var manual = candidates.FirstOrDefault(c => c.Kind == InstallKind.Manual && c.IsValid);
        var chosen = manual ?? candidates
            .Where(c => c.IsValid)
            .OrderByDescending(c => c.NewestSession ?? DateTime.MinValue)
            .ThenBy(c => c.Kind)
            .FirstOrDefault();

        return new GameLocations(
            chosen,
            candidates,
            Path.Combine(env.DocumentsFolder, "Escape from Tarkov", "Screenshots"),
            Path.Combine(env.RoamingAppData, "Battlestate Games", "Escape from Tarkov", "Settings"));
    }

    private IEnumerable<(string Folder, string Found)> BsgLauncherFolders()
    {
        foreach (var (hive, view) in RegistryLocations())
        {
            var location = env.ReadRegistryString(hive, view, UninstallKey + "EscapeFromTarkov", "InstallLocation");
            if (!string.IsNullOrWhiteSpace(location))
                yield return (location, $"launcher uninstall entry ({hive}, {view})");
        }

        var roots = new List<(string Root, string Found)>();
        var settings = env.ReadAllText(Path.Combine(env.RoamingAppData, "Battlestate Games", "BsgLauncher", "settings"));
        if (settings is not null && LauncherGamesRoot(settings) is { } gamesRoot)
            roots.Add((gamesRoot, "launcher settings (gamesRootDir)"));
        roots.Add((@"C:\Battlestate Games", "launcher default folder"));

        foreach (var (root, found) in roots)
        {
            if (!env.DirectoryExists(root))
                continue;
            // The launcher's root holds one folder per game; its name varies ("EFT", "EFT (live)", "Escape from Tarkov").
            yield return (root, found);
            foreach (var sub in env.EnumerateDirectories(root))
            {
                if (!Path.GetFileName(sub).Equals("BsgLauncher", StringComparison.OrdinalIgnoreCase))
                    yield return (sub, found);
            }
        }
    }

    private IEnumerable<(string Folder, string Found)> SteamFolders()
    {
        foreach (var (hive, view) in RegistryLocations())
        {
            var location = env.ReadRegistryString(hive, view, UninstallKey + "Steam App " + SteamAppId, "InstallLocation");
            if (!string.IsNullOrWhiteSpace(location))
                yield return (location, $"Steam uninstall entry ({hive}, {view})");
        }

        var steamRoots = new[]
        {
            env.ReadRegistryString(RegistryHive.CurrentUser, RegistryView.Default, @"SOFTWARE\Valve\Steam", "SteamPath"),
            env.ReadRegistryString(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Valve\Steam", "InstallPath"),
            env.ReadRegistryString(RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Valve\Steam", "InstallPath"),
        };

        var libraries = new List<string>();
        foreach (var steamRoot in steamRoots.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => Normalize(r!)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            libraries.Add(steamRoot);
            var vdf = env.ReadAllText(Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"));
            if (vdf is null)
                continue;
            libraries.AddRange(VdfPath().Matches(vdf).Select(m => Normalize(m.Groups["path"].Value.Replace(@"\\", @"\"))));
        }

        foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var manifest = env.ReadAllText(Path.Combine(library, "steamapps", $"appmanifest_{SteamAppId}.acf"));
            if (manifest is null)
                continue;
            var installDir = AcfInstallDir().Match(manifest) is { Success: true } m ? m.Groups["dir"].Value : "Escape from Tarkov";
            yield return (Path.Combine(library, "steamapps", "common", installDir), "Steam library " + library);
        }
    }

    private InstallCandidate Evaluate(InstallKind kind, string folder, string found)
    {
        if (!env.DirectoryExists(folder))
            return new InstallCandidate(kind, folder, null, null, found, "folder does not exist");

        // Steam puts the game one level down, in "build"; the launcher puts it in the install folder itself.
        var root = folder;
        if (!HasExe(root) && HasExe(Path.Combine(folder, "build")))
            root = Path.Combine(folder, "build");

        string? logs = null;
        DateTime? newest = null;
        foreach (var candidate in new[] { Path.Combine(root, "Logs"), Path.Combine(folder, "Logs"), Path.Combine(folder, "build", "Logs") })
        {
            if (NewestSession(candidate) is { } time && (newest is null || time > newest))
            {
                logs = candidate;
                newest = time;
            }
        }

        if (!HasExe(root) && logs is null)
            return new InstallCandidate(kind, root, null, null, found, "no EscapeFromTarkov.exe and no log sessions");

        return new InstallCandidate(kind, root, logs, newest, found, null);
    }

    private bool HasExe(string folder) => env.FileExists(Path.Combine(folder, "EscapeFromTarkov.exe"));

    /// <summary>Why a chosen folder isn't the game, in the player's words (a candidate's <see cref="InstallCandidate.Rejected"/>).</summary>
    public static string Explain(InstallCandidate? candidate) => candidate?.Rejected switch
    {
        null when candidate is not null => "",
        "folder does not exist" => "That folder doesn't exist (any more).",
        _ => "That folder doesn't hold Escape from Tarkov: there's no EscapeFromTarkov.exe and no Logs folder with game " +
             "sessions in it or in its \"build\" folder. Choose the folder the game is installed in.",
    };

    private DateTime? NewestSession(string logsFolder)
    {
        if (!env.DirectoryExists(logsFolder))
            return null;
        DateTime? newest = null;
        foreach (var dir in env.EnumerateDirectories(logsFolder))
        {
            if (SessionStart(Path.GetFileName(dir)) is { } start && (newest is null || start > newest))
                newest = start;
        }
        return newest;
    }

    /// <summary>"log_2026.01.01_15-00-00_1.1.5.1.47510" → 2026-01-01 15:00:00.</summary>
    public static DateTime? SessionStart(string folderName)
    {
        var m = SessionFolder().Match(folderName);
        return m.Success && DateTime.TryParseExact(m.Groups["ts"].Value, "yyyy.MM.dd_H-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var t)
            ? t
            : null;
    }

    /// <summary>
    /// Reads only the games root from the launcher settings. The same file holds login tokens; they are never
    /// read into anything, logged or copied.
    /// </summary>
    internal static string? LauncherGamesRoot(string settingsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(settingsJson);
            // TryGetProperty throws on anything but an object; a settings file of another shape names no folder.
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            foreach (var name in new[] { "gamesRootDir", "gameRootDir" })
            {
                if (doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(value.GetString()))
                    return value.GetString();
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
        }
        return null;
    }

    private static IEnumerable<(RegistryHive, RegistryView)> RegistryLocations() =>
    [
        (RegistryHive.LocalMachine, RegistryView.Registry32),
        (RegistryHive.LocalMachine, RegistryView.Registry64),
        (RegistryHive.CurrentUser, RegistryView.Default),
    ];

    private static string Normalize(string path)
    {
        var p = path.Trim().Replace('/', '\\').TrimEnd('\\');
        return p.Length == 2 && p[1] == ':' ? p + "\\" : p;
    }
}
