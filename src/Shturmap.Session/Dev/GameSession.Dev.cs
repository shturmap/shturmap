#if DEVTOOLS
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Install;
using Shturmap.Map;

namespace Shturmap.Session;

// The developer view's few ways into the session that no log line or screenshot can reach (docs/DESIGN.md §8,
// "Developer aids"). Everything else it does goes through a fake game folder, as the real game's files would.
public sealed partial class GameSession
{
    /// <summary>The last position, for the developer view's "repeat".</summary>
    public PlayerFix? DevLastFix => _fix;

    /// <summary>Developer view: the last position becomes older by <paramref name="by"/>, as if no screenshot had come
    /// since (the old-position look: the dashed ring, the age tag, the raid card's note).</summary>
    public async Task DevAgeFixAsync(TimeSpan by)
    {
        await _gate.WaitAsync();
        try
        {
            if (_fix is null)
                return;
            _fix = _fix with { At = _fix.At - by };
            AppLog.Info($"Developer view: position aged by {by.TotalMinutes:0} min");
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Developer view: as if loading tarkov.dev's data had just failed with <paramref name="failure"/>: no data,
    /// the plain notice and the DATA chip, as a real failure shows them (no retry; <see cref="DevReloadData"/> brings
    /// the data back).</summary>
    public async Task DevFailDataAsync(Exception failure)
    {
        var problem = LoadProblem.Explain(failure);
        await _gate.WaitAsync();
        try
        {
            _data = null;
            _sources = null;
            _dataProblem = problem;
            AppLog.Warn($"Developer view: data load failure simulated ({problem.Kind})");
            Say(DataNotice(problem), 30, offersReport: problem.Transient || problem.Advice == LoadProblem.Report);
            RecomputeQuests();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Developer view: loads tarkov.dev's data again, after a simulated failure.</summary>
    public void DevReloadData() => _ = Task.Run(() => LoadDataAsync(_mode));

    /// <summary>
    /// Developer view: Shturmap as if no game were on this PC: the no-game line, map browsing and the mode chooser
    /// (owner, 2026-10-03: the no-game fallback). "Choose game folder…" then works on any folder, the developer view's
    /// fake game included, so the switch back can be seen too.
    /// </summary>
    public async Task DevForgetGameAsync()
    {
        // The screenshot watcher stays on the fake game's folder, so its positions still arrive after a folder is chosen.
        var shots = _locations?.ScreenshotsFolder ?? "";
        var settings = _locations?.SettingsFolder ?? "";
        _locate ??= folder => new InstallLocator(_env ?? new WindowsGameEnvironment()).Locate(folder, discover: false) with
        {
            ScreenshotsFolder = shots,
            SettingsFolder = settings,
        };
        await FollowAsync(new GameLocations(null, [], shots, settings));
    }
}
#endif
