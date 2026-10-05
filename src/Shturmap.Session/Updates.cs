namespace Shturmap.Session;

/// <summary>
/// How Shturmap is installed and kept up to date: Velopack, from the code repository's GitHub Releases (owner,
/// 2026-10-03; docs/DESIGN.md §8, "Distribution").
/// </summary>
public static class Distribution
{
    /// <summary>
    /// Velopack's id for the app, and so its install folder (%LOCALAPPDATA%\ShturmapApp), which an uninstall deletes.
    /// It must not be "Shturmap": that is the data folder (database, logs, study log, cache, reports), which outlives
    /// the app. eng\release.ps1 packs with the same id.
    /// </summary>
    public const string PackId = "ShturmapApp";

    /// <summary>Where releases live: the code repository's GitHub Releases.</summary>
    public const string Repository = "https://github.com/shturmap/shturmap";

    /// <summary>
    /// While Shturmap is in private testing its releases are GitHub pre-releases ("Shturmap 0.2.0 (private testing)"),
    /// so the update check includes them. With the first stable release this becomes false.
    /// </summary>
    public const bool PreReleases = true;

    /// <summary>
    /// The dev build's Velopack id (eng\dev.ps1): its own install folder (%LOCALAPPDATA%\ShturmapDev), shortcuts and
    /// update feed, so it never mixes with the installed release (owner, 2026-10-03).
    /// </summary>
    public const string DeveloperPackId = "ShturmapDev";

    /// <summary>The folder Velopack installs to: never where Shturmap keeps the player's data.</summary>
    public static string InstallFolder(string localAppData, string packId = PackId) => Path.Combine(localAppData, packId);

    /// <summary>
    /// Which data folder an app uses: <c>--data</c> names one; only the installed release has the player's own; every
    /// other build (the dev build, the folder build, <c>dotnet run</c>) the developer folder.
    /// </summary>
    /// <param name="installedAppId">Velopack's id of this install, or null when not installed.</param>
    public static DataFolderKind DataFolderFor(string? installedAppId, string? dataArgument) =>
        !string.IsNullOrWhiteSpace(dataArgument) ? DataFolderKind.Custom
        : installedAppId == PackId ? DataFolderKind.Release
        : DataFolderKind.Dev;

    /// <summary>
    /// What settings say under "Updates" when this run can't update. A build that wasn't installed has no updater. An
    /// installed one whose updater couldn't start says that, and what repairs it: "not available in this build" was
    /// said there too, which wasn't the reason (review of 2026-10-04, A40).
    /// </summary>
    public static string NoUpdatesText(bool installed, bool updaterFailed) => installed && updaterFailed
        ? "Updates: the updater couldn't start, so no update checks. The log says why (LOG FOLDER). Running the Setup again repairs it; your data stays."
        : "Updates: not available in this build. Installed with the Setup, Shturmap updates itself.";

    /// <summary>
    /// Which install an exe belongs to by where it runs from, for a start in which Velopack itself failed (review of
    /// 2026-10-04, A40: an installed release then counted as not installed and opened the developer data folder, so
    /// the player's quest history and settings seemed gone, without a word). Velopack runs the app from
    /// %LOCALAPPDATA%\&lt;pack id&gt;\current. Null for any other place.
    /// </summary>
    public static string? InstalledIdByFolder(string baseDirectory, string localAppData)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory) || string.IsNullOrWhiteSpace(localAppData))
            return null;
        string exe;
        try
        {
            exe = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory)) + Path.DirectorySeparatorChar;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
        foreach (var id in new[] { PackId, DeveloperPackId })
        {
            var current = Path.Combine(InstallFolder(localAppData, id), "current") + Path.DirectorySeparatorChar;
            if (exe.StartsWith(current, StringComparison.OrdinalIgnoreCase))
                return id;
        }
        return null;
    }
}

/// <summary>What Shturmap does about new versions: the player's choice in settings ("Updates").</summary>
public enum UpdateMode
{
    /// <summary>Asks GitHub at start and every 6 hours, downloads a new version in the background; it applies at the
    /// next start. The default.</summary>
    Automatic,

    /// <summary>Asks, and says a new version is there; downloads it only on the player's click.</summary>
    TellOnly,

    /// <summary>No request at all.</summary>
    Off,
}

public static class UpdateModes
{
    /// <summary>The setting's key in shturmap.db ("automatic", "tell" or "off"; absent is automatic).</summary>
    public const string Setting = "updates";

    public static UpdateMode Parse(string? value) => value switch
    {
        "tell" => UpdateMode.TellOnly,
        "off" => UpdateMode.Off,
        _ => UpdateMode.Automatic,
    };

    public static string Format(UpdateMode mode) => mode switch
    {
        UpdateMode.TellOnly => "tell",
        UpdateMode.Off => "off",
        _ => "automatic",
    };
}

/// <summary>Where a new version stands in this session.</summary>
public enum UpdateStage
{
    /// <summary>None known.</summary>
    None,

    /// <summary>A newer version is on GitHub, not downloaded.</summary>
    Found,

    Downloading,

    /// <summary>Downloaded: it applies at the next start.</summary>
    Ready,
}

/// <param name="Check">Ask the update source (when it is due: <see cref="UpdatePolicy.CheckDue"/>).</param>
/// <param name="Download">Download the version found, now.</param>
/// <param name="Line">The one quiet line in the rail; empty for none.</param>
/// <param name="OfferDownload">The line offers DOWNLOAD.</param>
/// <param name="OfferRestart">The line offers RESTART NOW.</param>
public sealed record UpdateDecision(bool Check, bool Download, string Line, bool OfferDownload, bool OfferRestart);

public static class UpdatePolicy
{
    /// <summary>Checks at start, then at most every 6 hours.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    public static bool CheckDue(DateTime? lastCheck, DateTime now) => lastCheck is null || now - lastCheck.Value >= Interval;

    /// <summary>
    /// What happens about updates. A build that can't update (not installed by Velopack, or a developer run) does
    /// nothing; "Off" asks nothing; "Automatic" downloads what it finds; "Tell me only" offers the download. The line
    /// shows only outside raids, and so does RESTART NOW: Shturmap never restarts by itself, and never during a raid.
    /// A downloaded version applies at the next start whatever the setting.
    /// </summary>
    public static UpdateDecision Decide(UpdateMode mode, bool canUpdate, bool inRaid, UpdateStage stage, string? version)
    {
        if (!canUpdate)
            return new(false, false, "", false, false);
        var check = mode != UpdateMode.Off && stage is UpdateStage.None or UpdateStage.Found;
        var download = mode == UpdateMode.Automatic && stage == UpdateStage.Found;
        var line = inRaid ? "" : stage switch
        {
            UpdateStage.Found when mode == UpdateMode.TellOnly => $"Shturmap {version} is available",
            UpdateStage.Downloading => $"Downloading Shturmap {version}…",
            UpdateStage.Ready => $"Update {version} ready: applies at next start",
            _ => "",
        };
        return new(check, download, line,
            OfferDownload: !inRaid && mode == UpdateMode.TellOnly && stage == UpdateStage.Found,
            OfferRestart: !inRaid && stage == UpdateStage.Ready);
    }
}
