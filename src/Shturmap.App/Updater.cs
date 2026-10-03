using Shturmap.Session;
using Velopack;
using Velopack.Sources;

namespace Shturmap.App;

/// <summary>
/// New versions through Velopack, from the code repository's GitHub Releases, asked anonymously (docs/DESIGN.md §8,
/// "Distribution"). Only an installed app updates; what it does is <see cref="UpdatePolicy"/>'s to decide. Never
/// Velopack's own update service: a release feed on GitHub is the only source (SafetyTests).
/// </summary>
public sealed class Updater
{
    private readonly UpdateManager? _manager;
    private readonly string _source;
    private UpdateInfo? _found;

    /// <param name="testFeed">Developer aid ("--update-feed &lt;folder&gt;"): a local folder holding a release feed
    /// stands in for GitHub, and updates work even in a developer run. Local folders only.</param>
    /// <param name="developerRun">Snapshots, fake games and the demo ask nothing.</param>
    public Updater(string? testFeed, bool developerRun)
    {
        var local = testFeed is not null && Path.IsPathFullyQualified(testFeed) && !testFeed.StartsWith(@"\\", StringComparison.Ordinal)
            && Directory.Exists(testFeed);
        _source = local ? "the local feed " + testFeed : "GitHub";
        try
        {
            IUpdateSource source = local ? new SimpleFileSource(new DirectoryInfo(testFeed!)) : new GithubSource(Distribution.Repository, null, Distribution.PreReleases);
            _manager = new UpdateManager(source);
            Installed = _manager.IsInstalled;
            CanUpdate = Installed && (local || !developerRun);
            if (Installed && _manager.UpdatePendingRestart is { } pending)
            {
                Stage = UpdateStage.Ready;
                Version = pending.Version.ToString();
            }
        }
        catch (Exception e)
        {
            AppLog.Warn("Updates: Velopack couldn't start", e);
        }
    }

    /// <summary>Installed with its Setup (or Velopack's portable zip), so it can update itself.</summary>
    public bool Installed { get; }

    /// <summary>This run may ask for and download new versions.</summary>
    public bool CanUpdate { get; }

    public UpdateStage Stage { get; private set; }

    /// <summary>The version found or downloaded.</summary>
    public string? Version { get; private set; }

    public DateTime? LastCheck { get; private set; }

    /// <summary>The stage changed (raised on a background thread).</summary>
    public event Action? Changed;

    /// <summary>Asks the source for a newer version; false and a WARN line when it couldn't.</summary>
    public async Task<bool> CheckAsync()
    {
        if (_manager is null || !CanUpdate)
            return false;
        LastCheck = DateTime.Now;
        try
        {
            AppLog.Info($"Updates: asking {_source} for a version newer than {_manager.CurrentVersion}");
            var info = await _manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (info is null)
            {
                AppLog.Info("Updates: this is the newest version");
                return true;
            }
            _found = info;
            Version = info.TargetFullRelease.Version.ToString();
            if (Stage != UpdateStage.Ready)
                Set(UpdateStage.Found);
            AppLog.Info($"Updates: {Version} found");
            return true;
        }
        catch (Exception e)
        {
            AppLog.Warn($"Updates: couldn't ask {_source} for a new version; trying again later", e);
            return false;
        }
    }

    /// <summary>Downloads the version found in the background; it applies at the next start.</summary>
    public async Task<bool> DownloadAsync()
    {
        if (_manager is null || _found is null || Stage is UpdateStage.Downloading or UpdateStage.Ready)
            return false;
        Set(UpdateStage.Downloading);
        try
        {
            await _manager.DownloadUpdatesAsync(_found).ConfigureAwait(false);
            Set(UpdateStage.Ready);
            AppLog.Info($"Updates: {Version} downloaded; it applies at the next start");
            return true;
        }
        catch (Exception e)
        {
            Set(UpdateStage.Found);
            AppLog.Warn($"Updates: downloading {Version} failed; trying again later", e);
            return false;
        }
    }

    /// <summary>Applies the downloaded version and starts it: Velopack ends this process at once. Only on the player's
    /// click, outside raids.</summary>
    public void RestartNow()
    {
        if (_manager?.UpdatePendingRestart is not { } pending)
            return;
        AppLog.Info($"Updates: restarting into {pending.Version}");
        _manager.ApplyUpdatesAndRestart(pending);
    }

    private void Set(UpdateStage stage)
    {
        Stage = stage;
        Changed?.Invoke();
    }
}
