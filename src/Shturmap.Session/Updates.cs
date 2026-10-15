using Shturmap.Core.Raid;

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
        ? SessionTexts.UpdatesBroken
        : SessionTexts.UpdatesNotInBuild;

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

/// <summary>
/// What the rail's update band says, pinned under MAP between raids (owner, 2026-10-15, from a panel of four: B, "A cue,
/// then the band"; docs/DESIGN.md §8, "Updates"): its eyebrow and its note (<see cref="UpdatePolicy.Words"/>).
/// </summary>
public enum UpdateBand
{
    /// <summary>No band: nothing to say, a raid, or a build that can't update.</summary>
    None,

    /// <summary>"Tell me only" found a version: DOWNLOAD AVAILABLE, with DOWNLOAD.</summary>
    Available,

    /// <summary>A version is downloading: DOWNLOADING, no button.</summary>
    Downloading,

    /// <summary>Downloaded: UPDATE READY, with RESTART NOW.</summary>
    Ready,

    /// <summary>Downloaded before this start and still waiting: applying it failed (<see cref="UpdatePolicy.NotAppliedNotice"/>),
    /// with RESTART NOW.</summary>
    DidNotApply,
}

/// <param name="Check">Ask the update source (when it is due: <see cref="UpdatePolicy.CheckDue"/>).</param>
/// <param name="Download">Download the version found, now.</param>
/// <param name="Band">What the rail's update band says; <see cref="UpdateBand.None"/> for no band.</param>
/// <param name="OfferDownload">The band offers DOWNLOAD.</param>
/// <param name="OfferRestart">The band offers RESTART NOW.</param>
public sealed record UpdateDecision(bool Check, bool Download, UpdateBand Band, bool OfferDownload, bool OfferRestart);

public static class UpdatePolicy
{
    /// <summary>Checks at start, then at most every 6 hours.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    public static bool CheckDue(DateTime? lastCheck, DateTime now) => lastCheck is null || now - lastCheck.Value >= Interval;

    /// <summary>
    /// Said once at a start that finds a downloaded version still waiting (owner, 2026-10-05: "it tells me always that
    /// the update is available even though i downloaded and restarted"). Velopack applies a downloaded version before
    /// the app starts, by moving the install's folder aside; while another program holds the folder or a file in it
    /// that fails, and the old version starts again without a word. That day it was a browser Shturmap had opened a
    /// wiki link in, working in the install's folder (Program now starts the app in the user's folder instead).
    /// </summary>
    public static string NotAppliedNotice(string? version) =>
        SessionTexts.UpdateNotApplied(version: version);

    /// <summary>
    /// What happens about updates. A build that can't update (not installed by Velopack, or a developer run) does
    /// nothing; "Off" asks nothing; "Automatic" downloads what it finds; "Tell me only" offers the download. The band
    /// shows only outside raids, and so does RESTART NOW: Shturmap never restarts by itself, and never during a raid.
    /// A downloaded version applies at the next start whatever the setting.
    /// </summary>
    /// <param name="notApplied">The version was downloaded before this start and is still waiting: applying it failed
    /// (<see cref="NotAppliedNotice"/>).</param>
    public static UpdateDecision Decide(UpdateMode mode, bool canUpdate, bool inRaid, UpdateStage stage, string? version, bool notApplied = false)
    {
        if (!canUpdate)
            return new(false, false, UpdateBand.None, false, false);
        var check = mode != UpdateMode.Off && stage is UpdateStage.None or UpdateStage.Found;
        var download = mode == UpdateMode.Automatic && stage == UpdateStage.Found;
        var band = inRaid ? UpdateBand.None : stage switch
        {
            UpdateStage.Found when mode == UpdateMode.TellOnly => UpdateBand.Available,
            UpdateStage.Downloading => UpdateBand.Downloading,
            UpdateStage.Ready when notApplied => UpdateBand.DidNotApply,
            UpdateStage.Ready => UpdateBand.Ready,
            _ => UpdateBand.None,
        };
        return new(check, download, band,
            OfferDownload: band == UpdateBand.Available,
            OfferRestart: band is UpdateBand.Ready or UpdateBand.DidNotApply);
    }

    /// <summary>
    /// The band's eyebrow, in capitals, and its note line: what RESTART NOW does ("Restarts Shturmap in a few seconds.
    /// Never in a raid."), what to close where the version didn't apply. The UPDATE READY cue says the same eyebrow, so
    /// the eye goes from the one to the other. Empty for <see cref="UpdateBand.None"/>.
    /// </summary>
    public static (string Eyebrow, string Note) Words(UpdateBand band) => band switch
    {
        UpdateBand.Available => (SessionTexts.UpdateBandAvailable, SessionTexts.UpdateNoteAvailable),
        UpdateBand.Downloading => (SessionTexts.UpdateBandDownloading, SessionTexts.UpdateNoteDownloading),
        UpdateBand.Ready => (SessionTexts.UpdateBandReady, SessionTexts.UpdateNoteReady),
        UpdateBand.DidNotApply => (SessionTexts.UpdateBandDidNotApply, SessionTexts.UpdateNoteDidNotApply),
        _ => ("", ""),
    };
}

/// <summary>
/// When the UPDATE READY cue shows (owner, 2026-10-15: "A Beta-tester did not realize there is an update, which shows me
/// that it is currently too subtle. I, however, do not want to have a blocking modal like a pop-up"; from a panel of
/// four: B, "A cue, then the band"; docs/DESIGN.md §8, "Updates", and §4, principle 11). It shows when a download
/// completes in the menus (one that completes in a raid or while one loads waits for the menus), then once more at the
/// first menu after each raid while the update waits: once per raid. In "Tell me only" a version found takes the
/// download's place (DOWNLOAD AVAILABLE). Never in a raid or while one loads, never with "Off", never after RESTART NOW,
/// and never for what the start found: a version still waiting then has its notice (<see cref="UpdatePolicy.NotAppliedNotice"/>),
/// and a raid read back from the log at start never counts as one that ended (<see cref="RaidEnded"/> is told of a raid
/// over live). Where it stands among the other cues is the window's to keep (it waits for RAID OVER and its replay,
/// QUEST COMPLETE and SEEN IN YOUR STASH).
/// </summary>
public sealed class UpdateCue
{
    // What the cue (or the start's notice) last said: the stage and the version. A new one is due at once in the menus.
    private (UpdateStage Stage, string? Version)? _said;
    // A raid has ended since then: the cue is due once more.
    private bool _raidSince;
    private bool _restarting;

    /// <summary>A stage the cue is about: a version downloaded, or one found that "Tell me only" offers to download.
    /// "Off" never cues: a version downloaded before the player chose it still has its band.</summary>
    public static bool About(UpdateMode mode, UpdateStage stage) =>
        mode != UpdateMode.Off && (stage == UpdateStage.Ready || stage == UpdateStage.Found && mode == UpdateMode.TellOnly);

    /// <summary>Whether the cue is due now.</summary>
    public bool Due(UpdateMode mode, bool canUpdate, UpdateStage stage, string? version, RaidPhase phase) =>
        !_restarting && canUpdate && phase == RaidPhase.Menu && About(mode, stage) && (_said != (stage, version) || _raidSince);

    /// <summary>The cue showed for this stage and version.</summary>
    public void Shown(UpdateStage stage, string? version)
    {
        _said = (stage, version);
        _raidSince = false;
    }

    /// <summary>The start found a version still waiting (it couldn't be applied): its notice says it, so no cue until a
    /// raid has ended.</summary>
    public void AtStart(UpdateStage stage, string? version)
    {
        if (stage != UpdateStage.None)
            _said = (stage, version);
    }

    /// <summary>A raid ended, live: RAID OVER (never one read back from the log at start).</summary>
    public void RaidEnded() => _raidSince = true;

    /// <summary>RESTART NOW was clicked: nothing more, whatever comes of it.</summary>
    public void Restarting() => _restarting = true;
}
