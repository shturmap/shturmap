#if DEVTOOLS
using Shturmap.Core.Raid;
using Shturmap.Core.Screenshots;
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
            Say(DataNotice(problem), 30, offersReport: DataNoticeOffersReport(problem));
            RecomputeQuests();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Developer view: as if a screenshot taken now had shown the game's extract list with these rows (a script's
    /// "exits"; owner, 2026-10-15, so the QA pass can replay the roll call without a picture): from where Windows' text
    /// recognition hands over its reading on, every step is a real screenshot's (<see cref="TakeExitList"/>: matched to
    /// the map's exits for the side, taken only if it is the list, the time of the reading now). A row marked is one the
    /// game shows "??:??:??" beside. Null rows: every extract of the raid's map for the side. Says why not when nothing
    /// is read: no raid, no data, or reading the list turned off.
    /// </summary>
    public async Task<string?> DevReadExitsAsync(IReadOnlyList<(string Name, bool Marked)>? rows)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_readExits)
                return "Reading the extract list from screenshots is off.";
            if (_tracker.State.Phase != RaidPhase.InRaid || _raidMap is null || _data is null)
                return "No raid to read an extract list in.";
            rows ??= ExitNames(_data, _raidMap.Id, ShownRaid.Side).Select(e => (e.Names[0], false)).ToList();
            var reading = new ExitListReading(ExitList.EnglishHeader, rows.Select((r, i) => new ExitListRow($"EXFIL{i + 1:00} {r.Name}", r.Marked)).ToList());
            AppLog.Info($"Developer view: an extract list of {reading.Rows.Count} rows, as a screenshot taken now would show it");
            TakeExitList(reading, DateTime.Now, 0);
            return null;
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
