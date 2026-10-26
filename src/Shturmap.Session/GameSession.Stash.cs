using System.Diagnostics;
using Shturmap.Core.Logs;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;
using Shturmap.Core.Stash;
using Shturmap.Data.Images;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session;

// What the player has of what the quests need (owner, 2026-10-11, after the panel of the same day and the spike:
// "all as recommended"): marks on BRING rows and item cards, learned from the game's notification log (a quest's
// start items, rewards, insurance returns) and from menu screenshots the player takes with the game's key, read
// on the PC (StashReader: the item grids' labels, with Windows' text recognition). Kept in stash.json in the data
// folder (StashLedger). Never "you have it": each mark says where it was learned and when. docs/DESIGN.md §2 and §4,
// "What you have".
public sealed partial class GameSession
{
    /// <summary>The key of "Read your stash and tasks from menu screenshots" in the app's settings ("on" or "off"; absent is on).</summary>
    public const string ReadStashSetting = "screenshots.readStash";

    private volatile bool _readStash = true;

    /// <summary>Whether the saved setting says on. Everything but "off" does.</summary>
    public static bool ReadStashOn(string? setting) => setting != "off";

    /// <summary>
    /// Reads the item grids in a menu screenshot against the items the quests need, or null when the picture can't
    /// be read; the reader for the game's language when not set (tests give their own).
    /// </summary>
    public Func<string, IReadOnlyList<StashCandidate>, CancellationToken, Task<StashReading?>>? StashReader { get; init; }

    // The menus' reader, made at the first menu screenshot: the tasks' table first, then the item grids (What You Have's,
    // then the stash desk's), the picture opened once (MenuScreenshotReader). Null when this Windows has no text
    // recognition at all.
    private Func<string, IReadOnlyList<StashCandidate>, DeskCatalogue?, DateTimeOffset, CancellationToken, Task<MenuReading?>>? _menuReader;
    private bool _stashReaderLooked;
    private bool _stashReaderMissing;
    private string? _stashReaderLanguage;

    private StashLedger _ledger = new();

    private string StashPath => Path.Combine(paths.Root, "stash.json");

    // At open: the file from the last run, if any.
    private void OpenStash() => _ledger = StashLedger.Load(StashPath, warning => AppLog.Warn(warning));

    // Under the gate: written when something changed, which is rare (a reading, a log message with items, a click).
    private void SaveStashIfDirty()
    {
        if (!_ledger.Dirty)
            return;
        try
        {
            _ledger.Save(StashPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Failed("Saving the stash file", e);
        }
    }

    /// <summary>
    /// The "Read your stash and tasks from menu screenshots" tick, saved with the app's settings. Off: no menu screenshot's
    /// picture is opened from now on; the marks already learned stay (the player retracts them one by one if wanted).
    /// </summary>
    public async Task SetReadStashAsync(bool on)
    {
        await _gate.WaitAsync();
        try
        {
            _store?.SetSetting(ReadStashSetting, on ? "on" : "off");
            _readStash = on;
            AppLog.Info("Read your stash, gear and tasks from menu screenshots: " + (on ? "on" : "off"));
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>FORGET ALL MARKS in settings (owner, 2026-10-11: "a reset"): everything known of the stash goes; the
    /// log's messages from before now stay forgotten, the next screenshot or message marks again.</summary>
    public async Task ForgetAllMarksAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var count = _ledger.ForgetAll(DateTimeOffset.Now);
            Study.Ui("stash.forgetAll", ("marks", count));
            AppLog.Info($"Stash: {count} marks forgotten");
            SaveStashIfDirty();
            Say(SessionTexts.StashForgotten(count: count));
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>NOT ANY MORE on the item card: whatever is known of the item is retracted, from any source.</summary>
    public async Task NotAnyMoreAsync(string itemId)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_ledger.Retract(itemId, DateTimeOffset.Now))
                return;
            Study.Ui("stash.notanymore");
            SaveStashIfDirty();
            Publish();
        }
        finally
        {
            _gate.Release();
        }
    }

    // Under the gate. Made at the first menu screenshot, not at start: most runs never need it. A test gives its own
    // readers (StashReader, TasksReader, DeskReader): a picture that shows the Tasks screen goes to the tasks' reader
    // alone, as the app's own does; without one of them, that reading has nothing to say.
    private Func<string, IReadOnlyList<StashCandidate>, DeskCatalogue?, DateTimeOffset, CancellationToken, Task<MenuReading?>>? ReaderForMenu()
    {
        if (StashReader is not null || TasksReader is not null || DeskReader is not null)
        {
            var stash = StashReader;
            var tasks = TasksReader;
            var desk = DeskReader;
            return async (path, candidates, catalogue, at, ct) =>
            {
                if (tasks is not null && await tasks(path, ct) is { Screen: not TaskScreen.None } page)
                    return new MenuReading(page, null);
                var reading = stash is not null && candidates.Count > 0 ? await stash(path, candidates, ct) : null;
                var deskReading = desk is not null && catalogue is not null && reading?.IsTradingScreen != true ? await desk(path, catalogue, at, ct) : null;
                return reading is null && deskReading is null ? null : new MenuReading(null, reading, deskReading);
            };
        }
        if (_stashReaderLooked)
            return _menuReader;
        _stashReaderLooked = true;
        var stashReader = Shturmap.Game.Screenshots.StashReader.Create(_settings.Language);
        // A countdown band and a desk label Windows' recognition leaves unread are read again by PaddleOCR's (2026-10-26).
        var tasksReader = Shturmap.Game.Screenshots.TasksReader.Create(Paddle());
        // The desk reads the labels as the stash's reader does, in the game's language.
        var deskReader = stashReader is null ? null : new Shturmap.Game.Screenshots.DeskReader(stashReader, Paddle());
        _stashReaderMissing = stashReader is null;
        if (stashReader is not null)
        {
            _stashReaderLanguage = stashReader.LanguageTag;
            AppLog.Info($"Stash: read with Windows' text recognition for {stashReader.LanguageTag}");
        }
        else
        {
            AppLog.Warn("Stash: this Windows has no text recognition language, so menu screenshots can't be read");
        }
        if (tasksReader is null)
            AppLog.Warn("Operational tasks: this Windows has no English text recognition, so the Tasks screen can't be read");
        if (stashReader is null && tasksReader is null)
            return null;
        var menu = new MenuScreenshotReader(tasksReader, stashReader, deskReader);
        _menuReader = menu.ReadAsync;
        return _menuReader;
    }

    // PaddleOCR's recognition, the second reader of a desk label or a countdown band Windows' recognition left unread
    // (owner, 2026-10-26, decision 1; docs/DESIGN.md §2): one for the session, shared by the menus' and the raid's readers,
    // made with the first of them (under the gate). Its model, beside the exe, is loaded at the first such band, off the
    // reading's thread; a model that can't be loaded is said once and the readings read as Windows' alone does. It is not
    // disposed with the session: a reading under way may still be in it, and the process ends with it.
    private PaddleReader? _paddle;

    private PaddleReader Paddle() => _paddle ??= PaddleReader.Bundled(
        loaded: ms => AppLog.Info($"Text recognition: PaddleOCR's model loaded ({ms} ms), for the labels and countdowns Windows' recognition leaves unread"),
        failed: why => AppLog.Warn($"Text recognition: PaddleOCR's model couldn't be loaded, so the readings use Windows' recognition alone ({why})"));

    // Under the gate: the items the active quests need brought, on any map (BRING's rows: keys, items, gear, mods;
    // not weapons, whose classes run to hundreds and which the reading doesn't tell apart). Each with the short
    // name the game writes on its cell and tarkov.dev's grid image when the cache has it; the images not yet there
    // are asked for now, for the next reading (GameArt: once per picture per run, a few kilobytes each).
    private IReadOnlyList<StashCandidate> StashCandidates()
    {
        if (_data is null)
            return [];
        var ids = _plan.SelectMany(p => p.Requirements)
            .Where(r => r.Kind is RequirementKind.Key or RequirementKind.Bring or RequirementKind.Wear or RequirementKind.WeaponMods)
            .SelectMany(r => r.Alternatives ?? [r.ItemId])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var candidates = new List<StashCandidate>(ids.Count);
        foreach (var id in ids)
        {
            if (!_data.ItemShortNames.TryGetValue(id, out var shortName) || shortName.Length == 0)
                continue;
            byte[]? image = null;
            if (Art?.Cached(ArtKind.Grid, id) is { } path)
            {
                try
                {
                    image = File.ReadAllBytes(path);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }
            else
            {
                _ = Art?.GetAsync(ArtKind.Grid, id);
            }
            candidates.Add(new StashCandidate(id, shortName, image));
        }
        return candidates;
    }

    // Under the gate: the kind BRING lists each needed item as (a key, or something to bring, wear or fit), for the
    // cue's glyphs and words; the first row that names an item decides.
    private Dictionary<string, RequirementKind> NeededKinds()
    {
        var kinds = new Dictionary<string, RequirementKind>(StringComparer.Ordinal);
        foreach (var r in _plan.SelectMany(p => p.Requirements))
        {
            foreach (var id in r.Alternatives ?? [r.ItemId])
                kinds.TryAdd(id, r.Kind);
        }
        return kinds;
    }

    // How many pictures the SEEN IN YOUR STASH cue holds, three rows of eight as the kit reminder (Planning.CueKit);
    // the rest is "+N".
    private const int StashCueShown = 24;

    // A screenshot without a position, taken in the menus: the Tasks screen's OPERATIONAL table (owner, 2026-10-19:
    // from a panel of four, B; GameSession.Operational), else its item grids, read against what the quests need. One
    // tick says yes to both ("Read your stash and tasks from menu screenshots"). Reading takes a moment (decoding, the
    // recognition), so it runs outside the gate.
    private async Task ReadStashAsync(ScreenshotSeen seen)
    {
        Func<string, IReadOnlyList<StashCandidate>, DeskCatalogue?, DateTimeOffset, CancellationToken, Task<MenuReading?>>? read;
        IReadOnlyList<StashCandidate> candidates;
        DeskCatalogue? catalogue;
        await _gate.WaitAsync();
        try
        {
            // Not while a raid loads or runs: the game's menus aren't on screen then, and a raid's own screenshots
            // keep their one corner (the extract list).
            if (!_readStash || _tracker.State.Phase != RaidPhase.Menu || _data is null)
                return;
            read = ReaderForMenu();
            candidates = StashCandidates();
            catalogue = DeskCatalogueNow();
        }
        finally
        {
            _gate.Release();
        }
        if (read is null)
            return;
        var started = Stopwatch.GetTimestamp();
        var menu = await read(seen.Path, candidates, catalogue, new DateTimeOffset(seen.CreatedAt), _stop.Token);
        ReadingLogEntry? log;
        await _gate.WaitAsync();
        try
        {
            log = TakeMenu(seen, menu, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        finally
        {
            _gate.Release();
        }
        if (log is not null)
            WriteReadingLog(log);
    }

    // Under the gate: what a menu screenshot's readings showed, taken into the operational tasks, the desk's page and What
    // You Have's marks; the readings log's entry of it all (null when the reading was turned off meanwhile).
    private ReadingLogEntry? TakeMenu(ScreenshotSeen seen, MenuReading? menu, double ms)
    {
        if (!_readStash || _data is null)
            return null;
        var log = ReadingLogOf(seen, menu, ms);
        if (menu is null)
            return log;
        if (menu.Tasks is { } page)
            return log with { Tasks = TakeTasks(page, seen.CreatedAt, ms) };
        if (menu.Desk is { } desk)
        {
            log = log with { Desk = TakeDesk(desk, desk.Ms) };
            if (menu.Stash is null)
                Publish();
        }
        if (menu.Stash is not { } reading)
            return log;
        if (reading.IsTradingScreen)
        {
            // The trading screen writes prices on the stash's cells, not names: nothing to read.
            Study.Game("stash.trading", ("ms", ms));
            return log;
        }
        var items = reading.Grids.SelectMany(g => g.Items).Select(i => (i.ItemId, i.Count, i.FoundInRaid)).ToList();
        var stashPage = reading.Grids.Any(g => g.Kind == StashGridKind.Stash);
        var changed = _ledger.ApplyScreenshot(new DateTimeOffset(seen.CreatedAt), stashPage, items);
        Study.Game("stash.read", ("grids", reading.Grids.Count), ("stashPage", stashPage), ("cells", items.Count), ("changed", changed.Count), ("ms", ms));
        AppLog.Info($"Stash read from a menu screenshot: {reading.Grids.Count} grids, {items.Count} cells with items the quests need ({ms:0} ms)");
        SaveStashIfDirty();
        if (items.Count > 0)
        {
            // The SEEN IN YOUR STASH cue (owner, 2026-10-11: "a little eyecandy animation when the app recognises
            // the player updates the stash"; docs/DESIGN.md §4, principle 11): the needed items it showed, each
            // once with the most seen of it. Nothing for a screenshot that showed none.
            var kinds = NeededKinds();
            var shown = items.GroupBy(i => i.ItemId, StringComparer.Ordinal)
                .Select(g => new CueItem(g.Key, kinds.GetValueOrDefault(g.Key, RequirementKind.Bring), Count: g.Max(i => i.Count)))
                .ToList();
            Announce(new ViewCue(CueKind.StashSeen, "", Kit: shown.Take(StashCueShown).ToList(), KitMore: Math.Max(0, shown.Count - StashCueShown)));
        }
        Publish();
        return log with { MarksChanged = changed.Count };
    }

    // ---- the readings log (owner, 2026-10-25: "Can't you write out logs of what was detected and link it to the screenshot
    // so we can troubleshoot it afterwards?"; docs/DESIGN.md §8, "Developer aids", *Readings log*) ----

    // Under the gate: the log's head (the screenshot, the build as the app log's first line says it, the mode, the ticks)
    // with what the readers gave; the items named as the data names them.
    private ReadingLogEntry ReadingLogOf(ScreenshotSeen seen, MenuReading? menu, double ms)
    {
        var data = _data;
        return new ReadingLogEntry(seen.Path, new DateTimeOffset(seen.CreatedAt))
        {
            Shturmap = $"{Version} ({BuildKind ?? "build not said"}, {paths.KindText} data folder) on {Diagnostics.WindowsVersion()}",
            Mode = _mode.ToString(),
            ReadOn = _readStash,
            DeleteOn = _deleteScreenshots,
            Reading = menu,
            Ms = ms,
            NameOf = id => data is not null && data.ItemNames.TryGetValue(id, out var name)
                ? data.ItemShortNames.TryGetValue(id, out var shortName) && shortName.Length > 0 && shortName != name ? $"{name} ({shortName})" : name
                : null,
        };
    }

    /// <summary>Writes a menu screenshot's readings log into the folder: <see cref="ReadingLog.Write"/> unless a test gives
    /// its own (one that fails).</summary>
    public Action<string, ReadingLogEntry>? ReadingLogWriter { get; init; }

    // Outside the gate: the screenshot's file in the data folder's "readings", the last 50 kept (ReadingLog). Never in a
    // report or the diagnostics; "Delete screenshots after reading" leaves it. Whatever fails here, a file that can't be
    // written or an entry its lines stumble over, is one WARN line and the session goes on: the readings were taken, and
    // their log never fails them (code review, 2026-10-25).
    private void WriteReadingLog(ReadingLogEntry log)
    {
        try
        {
            if (ReadingLogWriter is { } write)
                write(paths.Readings, log);
            else
                ReadingLog.Write(paths.Readings, log);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            AppLog.Warn($"Readings log not written: {e.GetType().Name}: {e.Message}");
        }
    }

    // Under the gate: a quest message of the game's log that came with items (a quest's start, its reward), or an
    // insurance return. Replayed messages give the same mark again, which the ledger ignores.
    private void StashGrant(QuestEvent quest)
    {
        if (quest.Items is not { Count: > 0 } items || quest.Status == QuestLogStatus.Failed)
            return;
        var kind = quest.Status == QuestLogStatus.Started ? GrantKind.QuestStart : GrantKind.QuestReward;
        var changed = _ledger.ApplyGrant(new DateTimeOffset(quest.At), kind, quest.QuestId, items.Select(i => (i.ItemId, i.Count)));
        if (changed.Count > 0 && !_replaying)
            Study.Game("stash.grant", ("kind", kind), ("items", changed.Count));
    }

    private void StashGrant(InsuranceNoticeEvent notice)
    {
        if (notice.Kind != InsuranceNotice.Returned || notice.Items is not { Count: > 0 } items)
            return;
        var changed = _ledger.ApplyGrant(new DateTimeOffset(notice.At), GrantKind.Insurance, null, items.Select(i => (i.ItemId, i.Count)));
        if (changed.Count > 0 && !_replaying)
            Study.Game("stash.grant", ("kind", GrantKind.Insurance), ("items", changed.Count));
    }

    // Under the gate, after the quests were worked out: what a quest handed over at its start goes with the quest.
    private void ForgetStashGrantsOfQuestsOver()
    {
        foreach (var questId in _ledger.Marks.Values.Where(m => m.Source == MarkSource.Log && m.Grant == GrantKind.QuestStart).Select(m => m.QuestId).OfType<string>().Distinct().ToList())
        {
            if (_quests.GetValueOrDefault(questId)?.State is QuestState.Completed or QuestState.Failed)
                _ledger.Forget(questId);
        }
    }

    // Under the gate, after the quests were worked out: a quest the log says completed used up what it was brought
    // (planted, marked, handed over; the quest card's BRING counts, Planning.ToPlan), so the marks from before the
    // completion lose that count (owner, 2026-10-11: "how do we deal with the inventory depleting over time"; docs/
    // DESIGN.md §4, "What you have"). Once per completion: the ledger remembers which. A completion the log never
    // showed (implied by a later quest's start) has no time, so it takes nothing.
    private void StashHandovers()
    {
        if (_data is null)
            return;
        var changed = 0;
        foreach (var quest in _quests.Values)
        {
            if (quest.State != QuestState.Completed || quest.At is not { } when || _data.Tasks.GetValueOrDefault(quest.QuestId) is not { } task)
                continue;
            var at = new DateTimeOffset(when);
            if (_ledger.HandedOver(quest.QuestId, at))
                continue;
            var used = new List<(string ItemId, int Count)>();
            foreach (var objective in Planning.ToPlan(task, _data).Objectives)
            {
                foreach (var (item, count) in objective.Bring)
                    used.Add((item, count));
            }
            if (used.Count > 0)
                changed += _ledger.HandOver(at, quest.QuestId, used).Count;
        }
        if (changed > 0 && !_replaying)
            Study.Game("stash.handover", ("items", changed));
    }
}
