using System.Diagnostics;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Core.Raid;
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

    // A refresh of the item file for the desk's prices on its way; the last one asked for, by mode (the next is never
    // asked sooner than DeskPricesAge after it, whatever came of it); the troubles said since prices last came in.
    private bool _deskPricesLoading;
    private (GameMode Mode, DateTimeOffset At)? _deskPricesAsked;
    private readonly HashSet<string> _deskPricesSaid = [];

    // Whether the desk is on screen (MainWindow says), and what wakes the watch over its prices when that changes or prices
    // come in.
    private volatile bool _deskOnScreen;
    private readonly SemaphoreSlim _deskPricesWake = new(0, 1);

    // What a CLEAR or a new page after a raid took away, for the notice's UNDO (owner, 2026-10-26: "Button + fresh after
    // raid"): the page to put back, and the change it showed; until a reading comes, or it is put back. The notice's going is
    // the window's to say: it drops the link (the page held here is then let go with the next reading).
    private sealed record DeskUndoHeld(int Id, GameMode Mode, DeskPage Page, DeskChange? Change);

    private DeskUndoHeld? _deskUndo;
    private int _deskUndoIds;

    // A new page after a raid, to be said once the screenshot's reading is all taken (ReadStashAsync): its notice last, so
    // the notice of the needed items the same screenshot showed doesn't take its UNDO away.
    private DeskUndoOffer? _deskFreshOwed;

    // The last raid of each mode that started, as the game's logs have it, live or read back at start: when it started and
    // ended (wall-clock times in Zone). The next desk reading after one starts a new page.
    private readonly Dictionary<GameMode, (DateTime Started, DateTime Ended)> _raidsOver = [];

    /// <summary>How long the notices with UNDO stand: a few seconds, as notices do, a little longer than most's 6 s for the
    /// click.</summary>
    public const double DeskUndoSeconds = 8;

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
            _deskCatalogue = new DeskCatalogue(DeskData.Candidates(_data, _sources), DeskIconsNow());
            _deskCatalogueOf = (_data, _sources);
        }
        return _deskCatalogue;
    }

    // Under the gate: a desk reading taken into the mode's page (a newer reading replaces the regions it shows and keeps
    // the others; DeskLedger), and the prices asked for when the item file is five minutes old. What the page did, for the
    // readings log; null when the screen was none of the desk's.
    private DeskChange? TakeDesk(DeskReading reading, double ms)
    {
        if (reading.Page is not { } page)
        {
            Study.Game("desk.screen", ("screen", reading.Screen), ("ms", ms));
            return null;
        }
        // A new reading: what a CLEAR or a new page took away can't come back over it (owner, 2026-10-26).
        _deskUndo = null;
        _deskFreshOwed = null;
        DeskChange change;
        if (_desk.Page(_mode) is { Regions.Count: > 0 } before && RaidSince(before, page) is { } raid)
        {
            // The first reading after a raid starts a new page (owner, 2026-10-26: "fresh after raid"): nothing of the page
            // before is kept, dimmed or not; UNDO puts back the page this reading would have made of it.
            var (fresh, kept) = _desk.ApplyFresh(_mode, page);
            change = fresh;
            _deskUndo = new DeskUndoHeld(++_deskUndoIds, _mode, kept.Page, kept);
            _deskFreshOwed = new DeskUndoOffer(_deskUndo.Id, _deskReadings + 1);
            AppLog.Info($"Desk page started anew: a raid started at {raid.Started:HH:mm} and ended at {raid.Ended:HH:mm} since the page's last reading");
            Study.Game("desk.fresh", ("regions", before.Regions.Count));
        }
        else
            change = _desk.Apply(_mode, page);
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
            AppLog.Info($"Desk reading kept the page's {key} grid: {ReadingLog.KeptWhy(page.Region(key))}");
        }
        // A window attached to nothing that three readings in a row didn't show (owner, 2026-10-25).
        foreach (var _ in change.Dropped)
            AppLog.Info($"Desk page dropped a window attached to nothing, not shown by {DeskLedger.MissedToDrop} readings in a row");
        SaveDeskIfDirty();
        RefreshDeskPrices();
        return change;
    }

    // Under the gate: the mode's last raid when it started after the page's last reading and ended before this reading's
    // screenshot (owner, 2026-10-26: "a raid started and ended since the page's last reading", as the game's logs have it);
    // null for none. A load cancelled before its raid began is no raid. By the instants the wall-clock times stand for
    // (WallClock), the earliest where an hour repeats.
    private (DateTime Started, DateTime Ended)? RaidSince(DeskPage before, DeskPage reading)
    {
        if (!_raidsOver.TryGetValue(_mode, out var raid))
            return null;
        var started = WallClock.Instants(raid.Started, Zone)[0];
        var ended = WallClock.Instants(raid.Ended, Zone)[0];
        return started > before.ReadAt.UtcDateTime && ended <= reading.ReadAt.UtcDateTime + DeskRaidEndSlack ? raid : null;
    }

    // A screenshot's time against the raid's end line: the line may reach the log a moment after the menus are back.
    private static readonly TimeSpan DeskRaidEndSlack = TimeSpan.FromSeconds(10);

    // Under the gate (RaidOver): a raid that started is over, by its mode (the session's where the log didn't say one).
    private void DeskRaidOver(RaidEnded ended)
    {
        if (ended.Previous.RaidStartedAt is not { } started)
            return;
        var mode = ended.Previous.Mode != GameMode.Unknown ? ended.Previous.Mode : _mode;
        _raidsOver[mode] = (started, ended.At);
    }

    // Under the gate, once a menu screenshot's reading is all taken: the notice of a new page after a raid, with its UNDO.
    private void SayDeskFresh()
    {
        if (_deskFreshOwed is not { } offer)
            return;
        _deskFreshOwed = null;
        Say(SessionTexts.DeskFreshAfterRaid, DeskUndoSeconds, undo: offer);
    }

    /// <summary>
    /// The desk's CLEAR (owner, 2026-10-26: "We need an option to clear the desk because it quickly gets messy"; from the
    /// coordinator's options, "Button + fresh after raid"; docs/DESIGN.md §4, *The stash desk*, *Clearing the page*): the
    /// mode's page emptied at once, every region back to never read, kept so in desk.json, and a notice "Desk cleared." whose
    /// UNDO puts the page back exactly as it was. No question (principle 7). False when there was nothing to clear.
    /// </summary>
    public async Task<bool> ClearDeskAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var change = _deskChangeMode == _mode ? _deskChange : null;
            if (_desk.Clear(_mode, DateTimeOffset.Now) is not { } had)
                return false;
            _deskUndo = new DeskUndoHeld(++_deskUndoIds, _mode, had, change);
            _deskFreshOwed = null;
            _deskChange = new DeskChange(_desk.Page(_mode)!, new HashSet<string>(), false);
            _deskChangeMode = _mode;
            AppLog.Info($"Desk page cleared ({_mode}): {had.Regions.Count} regions");
            Study.Ui("desk.clear", ("regions", had.Regions.Count));
            SaveDeskIfDirty();
            Publish();
            Say(SessionTexts.DeskCleared, DeskUndoSeconds, undo: new DeskUndoOffer(_deskUndo.Id, _deskReadings));
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The notice's UNDO (owner, 2026-10-26): the page a CLEAR or a new page after a raid took away put back as the mode's,
    /// exactly as it was (its regions, cells, picks and reading times; after a raid, the page this reading would have made
    /// of it), standing at once (it is no reading: nothing comes in). False when that page is no longer held: a reading came
    /// since, or it was put back already.
    /// </summary>
    public async Task<bool> UndoDeskAsync(int id)
    {
        await _gate.WaitAsync();
        try
        {
            if (_deskUndo is not { } held || held.Id != id)
                return false;
            _deskUndo = null;
            _desk.Restore(held.Mode, held.Page);
            if (held.Mode == _mode)
            {
                _deskChange = held.Change;
                _deskChangeMode = held.Mode;
            }
            AppLog.Info($"Desk page put back ({held.Mode}): {held.Page.Regions.Count} regions");
            Study.Ui("desk.undo", ("regions", held.Page.Regions.Count));
            SaveDeskIfDirty();
            Publish();
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Whether the desk is on screen: the window shows the Desk view and isn't minimised (MainWindow says so as that
    /// changes). While it is, out of a raid and with a page in hand, the desk's prices are asked for again each time the item
    /// file turns <see cref="GameDataLoader.DeskPricesAge"/> old, and at once when the desk comes on screen with older ones
    /// (owner, 2026-10-26; docs/DESIGN.md §3, "The stash desk's prices come per reading").
    /// </summary>
    public void SetDeskOnScreen(bool onScreen)
    {
        _deskOnScreen = onScreen;
        WakeDeskPrices();
    }

    /// <summary>The longest the watch over the desk's prices waits before it looks again (a test gives a shorter one): it
    /// waits until the item file turns five minutes old, or until it is woken, but no longer than this, so a PC's sleep
    /// doesn't hold it past the time.</summary>
    public TimeSpan DeskPricesLook { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>The item file asked for again for the desk's prices, by mode and data language, in place of the loader's
    /// (tests); it may throw, as a download fails.</summary>
    public Func<GameMode, string, CancellationToken, Task<ItemSources>>? GivenDeskPrices { get; init; }

    private void WakeDeskPrices()
    {
        try
        {
            if (_deskPricesWake.CurrentCount == 0)
                _deskPricesWake.Release();
        }
        catch (SemaphoreFullException)
        {
            // Woken already.
        }
    }

    // The time for the desk's prices: the PC's clock unless a test gives its own (Clock).
    private DateTimeOffset PricesNow() => new(Clock());

    // What asks for the item file again: a test's own, else the loader's; none for a session given its item sources.
    private Func<GameMode, string, CancellationToken, Task<ItemSources>>? DeskPricesLoad() =>
        GivenDeskPrices ?? (GivenSources is null && _loader is { } loader
            ? (mode, language, ct) => loader.LoadSourcesAsync(mode, language, GameDataLoader.DeskPricesAge, ct)
            : null);

    // Under the gate: when the desk's prices are due again: once the item file is DeskPricesAge old, and never sooner than
    // DeskPricesAge after the last ask in this mode, whatever came of it (a failure, a saved copy no newer, waits for the
    // next five minutes, never a tight loop).
    private DateTimeOffset DeskPricesDue(ItemSources sources) =>
        (_deskPricesAsked is { } asked && asked.Mode == _mode && asked.At > sources.PricesAt ? asked.At : sources.PricesAt) + GameDataLoader.DeskPricesAge;

    // Under the gate. The desk's prices (docs/DESIGN.md §3, "The stash desk's prices come per reading"): json.tarkov.dev
    // has no price file per item, so the item file (every item's prices, about 1.4 MB compressed) is asked for again with its
    // ETag when it is five minutes old (GameDataLoader.DeskPricesAge; ten minutes until 2026-10-26, an hour until
    // 2026-10-19), as a desk reading arrives and, while the desk is on screen, as it turns that old (WatchDeskPricesAsync);
    // an unchanged one costs a few hundred bytes. One at a time; what comes in prices the page again (Publish: the view's
    // reading stays, so nothing on it comes in or glows).
    private void RefreshDeskPrices()
    {
        if (_sources is not { } sources || _deskPricesLoading || _data is null || DeskPricesLoad() is not { } load)
            return;
        var now = PricesNow();
        if (now < DeskPricesDue(sources))
            return;
        _deskPricesLoading = true;
        _deskPricesAsked = (_mode, now);
        var mode = _mode;
        var language = _data.Language;
        var round = _sourcesRound;
        var had = sources.PricesAt;
        _ = Task.Run(async () =>
        {
            ItemSources? fresh = null;
            (string Kind, string Why)? trouble = null;
            try
            {
                fresh = await load(mode, language, _stop.Token);
            }
            catch (Exception e) when (!_stop.IsCancellationRequested)
            {
                trouble = (LoadProblem.Explain(e).Kind.ToString(), e.GetType().Name);
            }
            catch (OperationCanceledException)
            {
            }
            await _gate.WaitAsync();
            try
            {
                _deskPricesLoading = false;
                // tarkov.dev not reached with a copy saved: the loader gives that copy back, no newer.
                if (fresh is not null && fresh.PricesAt <= had)
                    (fresh, trouble) = (null, ("SavedCopy", $"the copy of {had.ToLocalTime():HH:mm} kept"));
                // Each kind of trouble said once until prices come in again; the next ask is five minutes on.
                if (trouble is { } t && _deskPricesSaid.Add(t.Kind))
                    AppLog.Warn($"Desk prices not refreshed ({t.Kind}): {t.Why}; asking again in {GameDataLoader.DeskPricesAge.TotalMinutes:0} min");
                if (fresh is null || mode != _mode || round != _sourcesRound)
                    return;
                _deskPricesSaid.Clear();
                _sources = fresh;
                AppLog.Debug($"Desk prices refreshed: the item file of {fresh.PricesAt.ToLocalTime():HH:mm:ss}");
                Study.Game("desk.prices", ("age", (int)(DateTimeOffset.Now - fresh.PricesAt).TotalMinutes));
                Publish();
            }
            finally
            {
                _gate.Release();
                WakeDeskPrices();
            }
        });
    }

    // While the desk is on screen, out of a raid and with a page in hand (owner, 2026-10-26: "When finding prices in the
    // stash it's important that they are recent"): its prices asked for again as the item file turns five minutes old, so
    // they are never much older than that and tarkov.dev's scan; nothing while it isn't, and at once when it comes back with
    // older ones (SetDeskOnScreen wakes it).
    private async Task WatchDeskPricesAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            var wait = DeskPricesLook;
            try
            {
                await _gate.WaitAsync(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            try
            {
                wait = LookAtDeskPrices();
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Failed("Looking at the desk's prices", e);
            }
            finally
            {
                _gate.Release();
            }
            try
            {
                await _deskPricesWake.WaitAsync(wait, _stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    // Under the gate: the prices asked for if the desk is on screen and they are due; how long to wait before looking again
    // (until woken while the desk isn't on screen).
    private TimeSpan LookAtDeskPrices()
    {
        if (!_deskOnScreen)
            return Timeout.InfiniteTimeSpan;
        // A page of no region (CLEAR) has nothing to price, as no page hasn't.
        if (_tracker.State.Phase != RaidPhase.Menu || _desk.Page(_mode) is not { Regions.Count: > 0 })
            return DeskPricesLook;
        RefreshDeskPrices();
        if (_deskPricesLoading || _sources is not { } sources)
            return DeskPricesLook;
        var wait = DeskPricesDue(sources) - PricesNow();
        return wait <= TimeSpan.Zero || wait >= DeskPricesLook ? DeskPricesLook : wait + TimeSpan.FromMilliseconds(50);
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
