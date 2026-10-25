using System.Diagnostics;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session;

// The stash desk's page (owner, 2026-10-19, from a panel: A1 · B1 · C1 · D2, and the addendum: A(2) · B1 · C1a · Da;
// docs/DESIGN.md §2, "For the stash desk", §4, "Screen anatomy", *The stash desk*): a menu screenshot of the player's own
// stash or GEAR tab is read once more, against the whole catalogue (DeskReader), in the same path as What You Have's
// reading and under the same tick; the page in hand is kept per mode in desk.json (DeskLedger) and published with its
// values (SessionSnapshot.Desk). Never in a raid: the menus' screenshots are read only while the log shows the menus.
public sealed partial class GameSession
{
    /// <summary>Reads a menu screenshot for the stash desk against the catalogue, or null when the picture can't be read;
    /// the reader for the game's language when not set (tests give their own).</summary>
    public Func<string, DeskCatalogue, DateTimeOffset, CancellationToken, Task<DeskReading?>>? DeskReader { get; init; }

    private DeskLedger _desk = new();

    // What the last reading did to the mode's page (the fold's changed regions), how many readings were taken this run.
    private DeskChange? _deskChange;
    private GameMode _deskChangeMode;
    private int _deskReadings;

    // The catalogue, made once per data and item file (5,000 items' folded names).
    private DeskCatalogue? _deskCatalogue;
    private (GameData? Data, ItemSources? Sources) _deskCatalogueOf;

    // The keep levels, made once per data, item file and quest states; the published view, once per change.
    private DeskKeep? _deskKeep;
    private (GameData? Data, ItemSources? Sources, object? Quests) _deskKeepOf;
    private DeskView? _deskView;
    private (DeskPage? Page, ItemSources? Sources, DeskKeep? Keep, DeskChange? Change) _deskViewOf;

    // A refresh of the item file for the desk's prices on its way.
    private bool _deskPricesLoading;

    private string DeskPath => Path.Combine(paths.Root, "desk.json");

    private void OpenDesk() => _desk = DeskLedger.Load(DeskPath, warning => AppLog.Warn(warning));

    private void SaveDeskIfDirty()
    {
        if (!_desk.Dirty)
            return;
        try
        {
            _desk.Save(DeskPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Failed("Saving the desk file", e);
        }
    }

    // Under the gate: the catalogue the desk names cells from, once the data and the item file (the footprints, what
    // holds a grid) are loaded; null before (a screenshot then isn't read for the desk: it would have no prices either).
    private DeskCatalogue? DeskCatalogueNow()
    {
        if (_data is null || _sources is null)
            return null;
        if (_deskCatalogue is null || !ReferenceEquals(_deskCatalogueOf.Data, _data) || !ReferenceEquals(_deskCatalogueOf.Sources, _sources))
        {
            _deskCatalogue = DeskData.Catalogue(_data, _sources);
            _deskCatalogueOf = (_data, _sources);
        }
        return _deskCatalogue;
    }

    // Under the gate: a desk reading taken into the mode's page (a newer reading replaces the regions it shows and keeps
    // the others; DeskLedger), and the prices asked for when the item file is over ten minutes old.
    private void TakeDesk(DeskReading reading, double ms)
    {
        if (reading.Page is not { } page)
        {
            Study.Game("desk.screen", ("screen", reading.Screen), ("ms", ms));
            return;
        }
        var change = _desk.Apply(_mode, page);
        _deskChange = change;
        _deskChangeMode = _mode;
        _deskReadings++;
        var cells = page.Regions.SelectMany(r => r.Cells).ToList();
        Study.Game("desk.read", ("screen", reading.Screen), ("regions", page.Regions.Count), ("cells", cells.Count),
            ("named", cells.Count(c => c.Ids.Count == 1)), ("unsure", cells.Count(c => c.Ids.Count > 1)), ("changed", change.Changed.Count), ("ms", ms));
        AppLog.Info($"Desk read from a menu screenshot ({reading.Screen}): {page.Regions.Count} regions, {cells.Count} cells, " +
                    $"{cells.Count(c => c.Ids.Count == 1)} named, {cells.Count(c => c.Ids.Count > 1)} one of several, {change.Changed.Count} regions changed ({ms:0} ms)");
        // A region read too blank to stand for the page's (a window with no cell read, a grid whose items' lines weren't
        // found) leaves the page's as it was (DeskLedger.Blank).
        foreach (var key in change.Refused)
        {
            var region = page.Region(key);
            AppLog.Info($"Desk reading kept the page's {(region?.Kind == DeskRegionKind.ContainerWindow ? "window" : key)}: " +
                        $"{region?.ReadCells ?? 0} cells read, {region?.Unread ?? 0} unread, against {change.Page.Region(key)?.ReadCells ?? 0} on the page");
            Study.Game("desk.kept", ("kind", region?.Kind.ToString() ?? ""), ("read", region?.ReadCells ?? 0), ("unread", region?.Unread ?? 0));
        }
        // A region shown in part (its grid cut by the column's foot, or none of it in view) keeps the page's cells and
        // compartments outside what was read (owner, 2026-10-25; DeskLedger, DeskRegion.Partial).
        foreach (var key in change.Kept)
        {
            var region = page.Region(key);
            var why = region is null ? "" : region.Parts.Count > 0 ? "cut at the column's foot"
                : region.CoveredSlots.Count > 0 ? "under a window" : "not in view";
            AppLog.Info($"Desk reading kept the page's {key} grid: {why}");
        }
        // A window attached to nothing that three readings in a row didn't show (owner, 2026-10-25).
        foreach (var _ in change.Dropped)
            AppLog.Info($"Desk page dropped a window attached to nothing, not shown by {DeskLedger.MissedToDrop} readings in a row");
        SaveDeskIfDirty();
        RefreshDeskPrices();
    }

    // Under the gate. The desk's prices come per reading (docs/DESIGN.md §3, "The stash desk's prices come per reading"):
    // json.tarkov.dev has no price file per item, so the item file (every item's prices, about 1.4 MB compressed) is asked
    // for again with its ETag when a desk reading arrives and the saved copy is over ten minutes old (GameDataLoader.
    // DeskPricesAge; an hour until 2026-10-19); an unchanged one costs a few hundred bytes. Never on a timer: a page left
    // open isn't refreshed.
    private void RefreshDeskPrices()
    {
        if (_sources is not { } sources || _deskPricesLoading || GivenSources is not null || _loader is null || _data is null)
            return;
        if (DateTimeOffset.Now - sources.PricesAt < GameDataLoader.DeskPricesAge)
            return;
        _deskPricesLoading = true;
        var mode = _mode;
        var language = _data.Language;
        var round = _sourcesRound;
        _ = Task.Run(async () =>
        {
            ItemSources? fresh = null;
            try
            {
                fresh = await _loader.LoadSourcesAsync(mode, language, GameDataLoader.DeskPricesAge, _stop.Token);
            }
            catch (Exception e) when (!_stop.IsCancellationRequested)
            {
                AppLog.Warn($"Desk prices not refreshed ({LoadProblem.Explain(e).Kind}): {e.GetType().Name}");
            }
            catch (OperationCanceledException)
            {
            }
            await _gate.WaitAsync();
            try
            {
                _deskPricesLoading = false;
                if (fresh is null || mode != _mode || round != _sourcesRound)
                    return;
                _sources = fresh;
                Study.Game("desk.prices", ("age", (int)(DateTimeOffset.Now - fresh.PricesAt).TotalMinutes));
                Publish();
            }
            finally
            {
                _gate.Release();
            }
        });
    }

    // Under the gate, for the snapshot: the mode's page with its values, made again only when the page, the prices, the
    // keep levels or the last change did.
    private DeskView? DeskViewNow()
    {
        if (_desk.Page(_mode) is not { } page)
            return null;
        var keep = DeskKeepNow();
        var change = _deskChangeMode == _mode ? _deskChange : null;
        if (_deskView is not null && ReferenceEquals(_deskViewOf.Page, page) && ReferenceEquals(_deskViewOf.Sources, _sources) &&
            ReferenceEquals(_deskViewOf.Keep, keep) && ReferenceEquals(_deskViewOf.Change, change))
            return _deskView;
        var sources = _sources;
        var data = _data;
        var flea = DeskData.Flea(sources);
        Func<string, DeskItemFacts?> facts = id => DeskData.Facts(sources, id);
        var sheet = DeskSheet.Of(page, facts, flea, keep, traderName: id => data?.TraderName(id) ?? "");
        _deskView = new DeskView(_mode, sheet, change?.Changed ?? new HashSet<string>(), change?.First ?? false, _deskReadings,
            sources?.PricesAt)
        {
            FactsOf = facts,
            Flea = flea,
        };
        _deskViewOf = (page, sources, keep, change);
        return _deskView;
    }

    /// <summary>
    /// A "?" cell's pick on the desk's card (step C, 2026-10-19; docs/DESIGN.md §4, *The click*): the item it is, kept with
    /// the cell in desk.json until a reading changes the cell; null takes the pick back. A slot by its key, a grid's cell
    /// by its place. False when the page has no such cell or the item isn't one of its candidates.
    /// </summary>
    public async Task<bool> PickDeskAsync(string region, string? slot, int i, int j, string? itemId)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_desk.Pick(_mode, region, slot, i, j, itemId))
                return false;
            Study.Ui("desk.pick", ("region", region));
            SaveDeskIfDirty();
            Publish();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    // The price histories asked for this run, by mode and item: the parsed history and when it was had.
    private readonly Dictionary<(GameMode Mode, string Id), (DeskHistory? History, DateTimeOffset At)> _deskHistories = [];
    private readonly Lock _deskHistoriesLock = new();

    /// <summary>
    /// An item's price history for the desk's card (docs/DESIGN.md §3, "The stash desk's prices come per reading"):
    /// json.tarkov.dev's "/{mode}/prices/{id}", asked for when a card first shows the item and kept in the download cache
    /// with its ETag (one file per item, about 20 to 130 KB; an unchanged one costs a few hundred bytes). Never on a timer:
    /// within the hour the copy in hand is given again without a request. Null when it can't be had (no data yet, offline
    /// without a saved copy, a file that isn't a history).
    /// </summary>
    public async Task<DeskHistory?> DeskHistoryAsync(string itemId)
    {
        var mode = _mode;
        if (_loader is not { } loader || mode == GameMode.Unknown || string.IsNullOrEmpty(itemId))
            return null;
        lock (_deskHistoriesLock)
        {
            if (_deskHistories.TryGetValue((mode, itemId), out var had) && DateTimeOffset.Now - had.At < GameDataLoader.DeskHistoryAge)
                return had.History;
        }
        DeskHistory? history = null;
        try
        {
            var json = await loader.LoadPriceHistoryAsync(mode, itemId, _stop.Token);
            history = json is null ? null : DeskHistory.Parse(json);
            Study.Game("desk.history", ("points", history?.Points.Count ?? 0));
        }
        catch (Exception e) when (!_stop.IsCancellationRequested)
        {
            AppLog.Warn($"Price history not had ({LoadProblem.Explain(e).Kind}): {e.GetType().Name}");
            // Asked again when a card shows the item after two minutes, not at every pointing meanwhile.
            lock (_deskHistoriesLock)
                _deskHistories[(mode, itemId)] = (null, DateTimeOffset.Now - GameDataLoader.DeskHistoryAge + TimeSpan.FromMinutes(2));
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        lock (_deskHistoriesLock)
            _deskHistories[(mode, itemId)] = (history, DateTimeOffset.Now);
        return history;
    }

    // Under the gate: the keep levels for the quests as the logs have them now.
    private DeskKeep? DeskKeepNow()
    {
        if (_data is null)
            return null;
        if (_deskKeep is null || !ReferenceEquals(_deskKeepOf.Data, _data) || !ReferenceEquals(_deskKeepOf.Sources, _sources) ||
            !ReferenceEquals(_deskKeepOf.Quests, _quests))
        {
            var started = Stopwatch.GetTimestamp();
            _deskKeep = DeskData.Keep(_data, _quests, _sources);
            _deskKeepOf = (_data, _sources, _quests);
            AppLog.Debug($"Desk keep levels in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:0} ms");
        }
        return _deskKeep;
    }
}
