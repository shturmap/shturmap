using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shturmap.Core.Stash;

/// <summary>Where a mark was learned.</summary>
public enum MarkSource
{
    /// <summary>The game's notification log: a quest handed the item over, a reward, an insurance return.</summary>
    Log,

    /// <summary>A screenshot the player took in the game's menus.</summary>
    Screenshot,

    /// <summary>The player's word on the item card.</summary>
    You,
}

/// <summary>Which log message handed the item over.</summary>
public enum GrantKind
{
    QuestStart,
    QuestReward,
    Insurance,
}

/// <summary>
/// One observation of an item in the player's possession: never "you have it", always where Shturmap learned it and
/// when (docs/DESIGN.md §4, "What you have"). The count is "at least": what the message or the picture showed.
/// </summary>
/// <param name="Count">How many were seen, at least one.</param>
/// <param name="FoundInRaid">How many of them carried the game's found-in-raid mark (0 when not read).</param>
/// <param name="QuestId">For a log mark, the quest that handed the item over; null for an insurance return.</param>
/// <param name="NotSeenAt">For a screenshot mark, the date of a later full pass of the stash that didn't show the
/// item: the mark stays (a reading can miss), the words say so.</param>
public sealed record StashMark(string ItemId, int Count, int FoundInRaid, DateTimeOffset SeenAt, MarkSource Source,
    string? QuestId = null, GrantKind? Grant = null, DateTimeOffset? NotSeenAt = null);

/// <summary>A pass over the stash: menu screenshots taken within a quarter of an hour of each other with no raid between.</summary>
/// <param name="Full">Three or more pages of the stash itself were in it: enough to say that an item it didn't show wasn't seen.</param>
public sealed record StashPass(DateTimeOffset At, int Screenshots, bool Full);

/// <summary>
/// What Shturmap has learned of the player's possessions, kept in <c>stash.json</c> in the data folder: one mark per
/// item id (the newest observation wins), the passes made, and the items the player retracted (NOT ANY MORE). Item
/// ids and counts only: no names, no pictures, no paths. The file is the contract a later program could read; its
/// shape is docs/DESIGN.md §8, "The stash file".
/// </summary>
public sealed class StashLedger
{
    public const int Version = 1;

    /// <summary>Screenshots further apart than this start a new pass.</summary>
    public static readonly TimeSpan PassGap = TimeSpan.FromMinutes(15);

    /// <summary>Stash pages that make a pass a full one.</summary>
    public const int FullPassPages = 3;

    private readonly Dictionary<string, StashMark> _items = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _retracted = new(StringComparer.Ordinal);
    private readonly List<StashPass> _passes = [];
    private Pass? _pass;

    // The pass under way: what each screenshot showed is folded into "the most any one screenshot showed".
    private sealed class Pass(DateTimeOffset started)
    {
        public DateTimeOffset Started { get; } = started;
        public DateTimeOffset Last { get; set; } = started;
        public int Screenshots { get; set; }
        public int Pages { get; set; }
        public Dictionary<string, (int Count, int Fir)> Items { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>Whether something changed since the last <see cref="Save"/> or load.</summary>
    public bool Dirty { get; private set; }

    /// <summary>The passes made, oldest first.</summary>
    public IReadOnlyList<StashPass> Passes => _passes;

    /// <summary>Every mark that stands: an item the player retracted after its observation has none.</summary>
    public IReadOnlyDictionary<string, StashMark> Marks =>
        _items.Where(i => !Retracted(i.Value)).ToDictionary(i => i.Key, i => i.Value, StringComparer.Ordinal);

    /// <summary>The mark for an item, or null when nothing is known or the player retracted it.</summary>
    public StashMark? MarkOf(string itemId) =>
        _items.TryGetValue(itemId, out var mark) && !Retracted(mark) ? mark : null;

    private bool Retracted(StashMark mark) => _retracted.TryGetValue(mark.ItemId, out var at) && at >= mark.SeenAt;

    /// <summary>
    /// Items a log message says the game put into the stash. Replayed messages (the log is read from its start) give
    /// the same mark again, which changes nothing; a newer observation of the item from any source stays.
    /// </summary>
    /// <returns>The ids whose mark is new or changed.</returns>
    public IReadOnlyList<string> ApplyGrant(DateTimeOffset at, GrantKind kind, string? questId, IEnumerable<(string ItemId, int Count)> items)
    {
        var changed = new List<string>();
        foreach (var group in items.Where(i => i.ItemId.Length > 0).GroupBy(i => i.ItemId, StringComparer.Ordinal))
        {
            var count = Math.Max(1, group.Sum(i => Math.Max(1, i.Count)));
            var mark = new StashMark(group.Key, count, 0, at, MarkSource.Log, kind == GrantKind.Insurance ? null : questId, kind);
            if (Put(mark))
                changed.Add(group.Key);
        }
        return changed;
    }

    /// <summary>
    /// Items a menu screenshot showed. Within a pass, the count of an item is the most any one screenshot of the
    /// pass showed (two overlapping pages never double a stack); a screenshot after the pass's gap, or after a raid,
    /// starts a new pass.
    /// </summary>
    /// <param name="stashPage">Whether the screenshot showed a page of the stash itself (a case's window alone doesn't count towards a full pass).</param>
    /// <returns>The ids whose mark is new or changed.</returns>
    public IReadOnlyList<string> ApplyScreenshot(DateTimeOffset at, bool stashPage, IEnumerable<(string ItemId, int Count, bool? FoundInRaid)> items)
    {
        if (_pass is null || at - _pass.Last > PassGap || at < _pass.Last)
            ClosePass();
        _pass ??= new Pass(at);
        _pass.Last = at;
        _pass.Screenshots++;
        if (stashPage)
            _pass.Pages++;
        var changed = new List<string>();
        foreach (var group in items.Where(i => i.ItemId.Length > 0).GroupBy(i => i.ItemId, StringComparer.Ordinal))
        {
            var count = group.Sum(i => Math.Max(1, i.Count));
            var fir = group.Where(i => i.FoundInRaid == true).Sum(i => Math.Max(1, i.Count));
            if (_pass.Items.TryGetValue(group.Key, out var seen))
            {
                count = Math.Max(count, seen.Count);
                fir = Math.Max(fir, seen.Fir);
            }
            _pass.Items[group.Key] = (count, fir);
            if (Put(new StashMark(group.Key, count, Math.Min(fir, count), at, MarkSource.Screenshot)))
                changed.Add(group.Key);
        }
        return changed;
    }

    /// <summary>A raid started: the pass under way, if any, is over.</summary>
    public void RaidStarted() => ClosePass();

    // A pass ends when the next screenshot comes too late, when a raid starts, or when the session ends. A full
    // pass that didn't show an item seen in a screenshot before says so on the item's mark; nothing is taken away.
    private void ClosePass()
    {
        if (_pass is null)
            return;
        var full = _pass.Pages >= FullPassPages;
        _passes.Add(new StashPass(_pass.Last, _pass.Screenshots, full));
        if (full)
        {
            foreach (var (id, mark) in _items.ToList())
            {
                if (mark.Source == MarkSource.Screenshot && !_pass.Items.ContainsKey(id) && mark.SeenAt < _pass.Started)
                    _items[id] = mark with { NotSeenAt = _pass.Last };
            }
        }
        Dirty = true;
        _pass = null;
    }

    /// <summary>Ends the pass under way, if any (the session is closing).</summary>
    public void EndPass() => ClosePass();

    /// <summary>NOT ANY MORE: the player retracts whatever is known of an item. A later observation stands again.</summary>
    public bool Retract(string itemId, DateTimeOffset at)
    {
        if (MarkOf(itemId) is null)
            return false;
        _retracted[itemId] = at;
        Dirty = true;
        return true;
    }

    /// <summary>
    /// A quest completed or failed: what the log said it handed over at its start is used up or moot, so those marks
    /// go. Its rewards stay (another quest may need them), and so do marks from screenshots: the item is in the stash,
    /// whatever the quest did.
    /// </summary>
    /// <returns>The ids whose mark went.</returns>
    public IReadOnlyList<string> Forget(string questId)
    {
        var gone = _items.Values.Where(m => m.Source == MarkSource.Log && m.Grant == GrantKind.QuestStart && m.QuestId == questId).Select(m => m.ItemId).ToList();
        foreach (var id in gone)
            _items.Remove(id);
        if (gone.Count > 0)
            Dirty = true;
        return gone;
    }

    // The newest observation of an item wins; the same one again changes nothing. A new observation after a
    // retraction stands: the retraction was about what was known before it.
    private bool Put(StashMark mark)
    {
        if (_items.TryGetValue(mark.ItemId, out var old))
        {
            if (old.SeenAt > mark.SeenAt)
                return false;
            if (old == mark)
                return false;
        }
        _items[mark.ItemId] = mark;
        if (_retracted.TryGetValue(mark.ItemId, out var at) && at < mark.SeenAt)
            _retracted.Remove(mark.ItemId);
        Dirty = true;
        return true;
    }

    // ---- the file ----

    /// <summary>The ledger in a file, or an empty one when there is none; a file that can't be read is set aside as
    /// <c>stash.json.unreadable</c> and an empty ledger starts (the reading is cheap to repeat; the file is nobody's record).</summary>
    public static StashLedger Load(string path, Action<string>? warn = null)
    {
        var ledger = new StashLedger();
        if (!File.Exists(path))
            return ledger;
        try
        {
            var model = JsonSerializer.Deserialize(File.ReadAllText(path), StashJson.Default.StashFile);
            if (model is null)
                return ledger;
            foreach (var pass in model.Passes ?? [])
                ledger._passes.Add(new StashPass(pass.At, pass.Screenshots, pass.Full));
            foreach (var (id, item) in model.Items ?? [])
            {
                if (id.Length == 0 || item is null)
                    continue;
                var source = item.Source switch { "log" => MarkSource.Log, "you" => MarkSource.You, _ => MarkSource.Screenshot };
                GrantKind? grant = item.Grant switch { "start" => GrantKind.QuestStart, "reward" => GrantKind.QuestReward, "insurance" => GrantKind.Insurance, _ => null };
                ledger._items[id] = new StashMark(id, Math.Max(1, item.Count), Math.Max(0, item.Fir ?? 0), item.SeenAt, source, item.Quest, grant, item.NotSeenAt);
            }
            foreach (var (id, at) in model.Retracted ?? [])
                ledger._retracted[id] = at;
            return ledger;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            warn?.Invoke($"The stash file couldn't be read ({e.GetType().Name}); starting over");
            try
            {
                File.Move(path, path + ".unreadable", overwrite: true);
            }
            catch (Exception moving) when (moving is IOException or UnauthorizedAccessException)
            {
            }
            return new StashLedger();
        }
    }

    /// <summary>Writes the ledger: a temporary file, then one move, so a crash leaves the old file whole.</summary>
    public void Save(string path)
    {
        var model = new StashFile(Version,
            _passes.Select(p => new PassFile(p.At, p.Screenshots, p.Full)).ToList(),
            _items.OrderBy(i => i.Key, StringComparer.Ordinal).ToDictionary(i => i.Key, i => new ItemFile(i.Value.Count, i.Value.FoundInRaid > 0 ? i.Value.FoundInRaid : null,
                i.Value.SeenAt,
                i.Value.Source switch { MarkSource.Log => "log", MarkSource.You => "you", _ => "screenshot" },
                i.Value.QuestId,
                i.Value.Grant switch { GrantKind.QuestStart => "start", GrantKind.QuestReward => "reward", GrantKind.Insurance => "insurance", _ => null },
                i.Value.NotSeenAt), StringComparer.Ordinal),
            _retracted.OrderBy(r => r.Key, StringComparer.Ordinal).ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(model, StashJson.Default.StashFile));
        File.Move(temp, path, overwrite: true);
        Dirty = false;
    }
}

// The file's shape (docs/DESIGN.md §8, "The stash file"): item ids and counts, dates with their offset, nothing else.
internal sealed record StashFile(int Version, List<PassFile> Passes, Dictionary<string, ItemFile> Items, Dictionary<string, DateTimeOffset> Retracted);

internal sealed record PassFile(DateTimeOffset At, int Screenshots, bool Full);

internal sealed record ItemFile(int Count, int? Fir, DateTimeOffset SeenAt, string Source, string? Quest, string? Grant, DateTimeOffset? NotSeenAt);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, WriteIndented = true)]
[JsonSerializable(typeof(StashFile))]
internal sealed partial class StashJson : JsonSerializerContext;
