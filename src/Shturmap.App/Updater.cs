using Shturmap.App.Rules;
using Shturmap.Session;
using Velopack;
using Velopack.Sources;

namespace Shturmap.App;

/// <summary>
/// New versions through Velopack, from the code repository's GitHub Releases, asked anonymously (docs/DESIGN.md §8,
/// "Distribution"). Only an installed app updates; what it does is <see cref="UpdatePolicy"/>'s to decide. Never
/// Velopack's own update service: a release feed on GitHub is the only source (SafetyTests). The dev build never asks
/// GitHub: it updates from the local feed eng\dev.ps1 builds, or not at all.
/// </summary>
public sealed class Updater
{
    private readonly UpdateManager? _manager;
    private readonly string _source;
    private UpdateInfo? _found;

    /// <param name="testFeed">Developer aid ("--update-feed &lt;folder&gt;"): a local folder holding a release feed
    /// stands in for GitHub, and updates work even in a developer run. A folder on a fixed local drive only.</param>
    /// <param name="developerRun">Snapshots, fake games and the demo ask nothing.</param>
    /// <param name="devBuild">A developer build (DEVTOOLS): never GitHub.</param>
    /// <param name="devFeed">The dev build's own feed, the local folder eng\dev.ps1 packs into (baked into the build).</param>
    public Updater(string? testFeed, bool developerRun, bool devBuild = false, string? devFeed = null)
    {
        var test = LocalFolder(testFeed);
        var dev = !test && devBuild && LocalFolder(devFeed);
        _source = test ? "the local feed " + testFeed : dev ? "the dev feed " + devFeed : devBuild ? "nowhere" : "GitHub";
        try
        {
            // A developer build without its feed still needs Velopack's view of the install (installed? which id?),
            // through a source it never asks: CanUpdate stays false.
            IUpdateSource source = test ? new SimpleFileSource(new DirectoryInfo(testFeed!))
                : dev ? new SimpleFileSource(new DirectoryInfo(devFeed!))
                : devBuild ? new SimpleFileSource(new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "no-feed")))
                : new GithubSource(Distribution.Repository, null, Distribution.PreReleases);
            _manager = new UpdateManager(source);
            Installed = _manager.IsInstalled;
            AppId = Installed ? _manager.AppId : null;
            CanUpdate = Installed && (test || (!developerRun && (dev || !devBuild)));
            if (Installed && _manager.UpdatePendingRestart is { } pending)
            {
                Stage = UpdateStage.Ready;
                Version = pending.Version.ToString();
            }
        }
        catch (Exception e)
        {
            StartProblem = e;
        }
    }

    // On a fixed local drive only: no network share in either slash form, no mapped or removable drive (LocalFeed).
    private static bool LocalFolder(string? folder) => LocalFeed.IsLocalFolder(folder);

    /// <summary>Velopack couldn't start: logged by the app once its log is open (the data folder depends on this).</summary>
    public Exception? StartProblem { get; }

    /// <summary>Installed with its Setup (or Velopack's portable zip), so it can update itself.</summary>
    public bool Installed { get; }

    /// <summary>Velopack's id of this install (<see cref="Distribution.PackId"/>, <see cref="Distribution.DeveloperPackId"/>), or
    /// null when not installed.</summary>
    public string? AppId { get; }

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

    /// <summary>"Uninstall Shturmap…" in settings: only in an install Velopack made, the release or the dev build.</summary>
    public bool UninstallOffered => Installed && Uninstall.Offered(AppId);

    /// <summary>Velopack's uninstaller is there to start: checked before the session is closed for it.</summary>
    public bool UninstallerReady
    {
        get
        {
            try
            {
                var locator = Velopack.Locators.VelopackLocator.Current;
                return UninstallOffered && locator.RootAppDir is not null && locator.UpdateExePath is { } exe && File.Exists(exe);
            }
            catch (Exception e)
            {
                AppLog.Warn("Uninstall: Velopack couldn't say where its uninstaller is", e);
                return false;
            }
        }
    }

    /// <summary>
    /// Starts Velopack's own uninstaller for this install (the same as Windows' Settings → Apps), after leaving the
    /// note that asks its hook to delete the data folder, or removing an old one. The caller has closed the session
    /// first: the uninstaller ends this process. <paramref name="silent"/> (developer checks only) keeps Velopack from
    /// showing any dialog; otherwise it shows one only when something goes wrong.
    /// </summary>
    public bool StartUninstall(bool deleteData, bool silent = false)
    {
        try
        {
            var locator = Velopack.Locators.VelopackLocator.Current;
            if (!UninstallOffered || locator.RootAppDir is not { } root || locator.UpdateExePath is not { } exe || !File.Exists(exe))
                return false;
            Uninstall.SetIntent(root, deleteData, DateTime.Now);
            AppLog.Info($"Uninstall: Velopack's uninstaller started{(deleteData ? "; the data folder goes too" : "; the data folder stays")}");
            var start = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false };
            if (silent)
                start.ArgumentList.Add("--silent");
            start.ArgumentList.Add("uninstall");
            System.Diagnostics.Process.Start(start);
            return true;
        }
        catch (Exception e)
        {
            AppLog.Warn("Uninstall: couldn't start Velopack's uninstaller", e);
            return false;
        }
    }

    /// <summary>
    /// The uninstaller's hook (Program.Main): deletes this install's data folder when the player asked for it, i.e. a
    /// fresh note is in the install folder; never otherwise, and never anything but that folder (Uninstall.MayDelete).
    /// </summary>
    public static void DeleteDataIfAsked()
    {
        var locator = Velopack.Locators.VelopackLocator.Current;
        if (locator.RootAppDir is not { } root || !Uninstall.IntentFresh(root, DateTime.Now))
            return;
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (Uninstall.DataFolder(locator.AppId, local) is { } data)
            Uninstall.DeleteData(data, locator.AppId, local, Uninstall.DeletePatience);
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
