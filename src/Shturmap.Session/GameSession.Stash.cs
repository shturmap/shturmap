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
    /// <summary>The key of "Read your stash from menu screenshots" in the app's settings ("on" or "off"; absent is on).</summary>
    public const string ReadStashSetting = "screenshots.readStash";

    private volatile bool _readStash = true;

    /// <summary>Whether the saved setting says on. Everything but "off" does.</summary>
    public static bool ReadStashOn(string? setting) => setting != "off";

    /// <summary>
    /// Reads the item grids in a menu screenshot against the items the quests need, or null when the picture can't
    /// be read; the reader for the game's language when not set (tests give their own).
    /// </summary>
    public Func<string, IReadOnlyList<StashCandidate>, CancellationToken, Task<StashReading?>>? StashReader { get; init; }

    private Func<string, IReadOnlyList<StashCandidate>, CancellationToken, Task<StashReading?>>? _stashReader;
    private bool _stashReaderLooked;
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
    /// The "Read your stash from menu screenshots" tick, saved with the app's settings. Off: no menu screenshot's
    /// picture is opened from now on; the marks already learned stay (the player retracts them one by one if wanted).
    /// </summary>
    public async Task SetReadStashAsync(bool on)
    {
        await _gate.WaitAsync();
        try
        {
            _store?.SetSetting(ReadStashSetting, on ? "on" : "off");
            _readStash = on;
            AppLog.Info("Read your stash from menu screenshots: " + (on ? "on" : "off"));
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

    // Under the gate. Made at the first menu screenshot, not at start: most runs never need it.
    private Func<string, IReadOnlyList<StashCandidate>, CancellationToken, Task<StashReading?>>? ReaderForStash()
    {
        if (StashReader is not null)
            return StashReader;
        if (_stashReaderLooked)
            return _stashReader;
        _stashReaderLooked = true;
        if (Shturmap.Game.Screenshots.StashReader.Create(_settings.Language) is { } reader)
        {
            _stashReaderLanguage = reader.LanguageTag;
            _stashReader = (path, candidates, ct) => reader.ReadAsync(path, candidates, ct: ct);
            AppLog.Info($"Stash: read with Windows' text recognition for {reader.LanguageTag}");
        }
        else
        {
            AppLog.Warn("Stash: this Windows has no text recognition language, so menu screenshots can't be read");
        }
        return _stashReader;
    }

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

    // A screenshot without a position, taken in the menus: its item grids, read against what the quests need.
    // Reading takes a moment (decoding, the recognition per cell), so it runs outside the gate.
    private async Task ReadStashAsync(ScreenshotSeen seen)
    {
        Func<string, IReadOnlyList<StashCandidate>, CancellationToken, Task<StashReading?>>? read;
        IReadOnlyList<StashCandidate> candidates;
        await _gate.WaitAsync();
        try
        {
            // Not while a raid loads or runs: the game's menus aren't on screen then, and a raid's own screenshots
            // keep their one corner (the extract list).
            if (!_readStash || _tracker.State.Phase != RaidPhase.Menu || _data is null)
                return;
            read = ReaderForStash();
            candidates = StashCandidates();
        }
        finally
        {
            _gate.Release();
        }
        if (read is null || candidates.Count == 0)
            return;
        var started = Stopwatch.GetTimestamp();
        var reading = await read(seen.Path, candidates, _stop.Token);
        if (reading is null)
            return;
        await _gate.WaitAsync();
        try
        {
            if (!_readStash || _data is null)
                return;
            var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (reading.IsTradingScreen)
            {
                // The trading screen writes prices on the stash's cells, not names: nothing to read.
                Study.Game("stash.trading", ("ms", ms));
                return;
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
        }
        finally
        {
            _gate.Release();
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
