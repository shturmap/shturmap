using System.Diagnostics;
using Shturmap.Core.Desk;
using Shturmap.Core.Raid;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session;

// The in-raid glance's reading (owner, 2026-10-19, three choices: when, what and what is kept; docs/DESIGN.md §2, "For the
// stash desk", "In a raid", and §4, *The in-raid glance*): a screenshot taken in a raid, once its corner was read for the
// extract list, is read for an inventory screen (DeskReader.ReadRaidAsync): the player's own regions and the LOOT pane, as
// the desk reads its grids, against the whole catalogue. The page is published with its values for the glance's plate
// (SessionSnapshot.Glance) and kept nowhere: no desk.json, never the desk's page in hand; it goes when the raid does. The
// menu screenshots' tick says yes or no (its words gain "and in a raid" with the plate).
public sealed partial class GameSession
{
    /// <summary>Reads a raid screenshot for the in-raid glance against the catalogue, or null when the picture can't be
    /// read; the reader for the game's language when not set (tests give their own).</summary>
    public Func<string, DeskCatalogue, DateTimeOffset, CancellationToken, Task<DeskReading?>>? RaidInventoryReader { get; init; }

    private Func<string, DeskCatalogue, DateTimeOffset, CancellationToken, Task<DeskReading?>>? _raidInventoryReader;
    private bool _raidInventoryReaderLooked;

    // The last inventory screenshot's reading this raid, and how many were read.
    private RaidGlance? _glance;
    private int _glanceReadings;

    private void ForgetGlance()
    {
        _glance = null;
        _glanceReadings = 0;
    }

    // Under the gate. Made at the first raid screenshot that is read for it, not at start.
    private Func<string, DeskCatalogue, DateTimeOffset, CancellationToken, Task<DeskReading?>>? ReaderForRaidInventory()
    {
        if (RaidInventoryReader is not null)
            return RaidInventoryReader;
        if (_raidInventoryReaderLooked)
            return _raidInventoryReader;
        _raidInventoryReaderLooked = true;
        if (Shturmap.Game.Screenshots.DeskReader.Create(_settings.Language) is not { } reader)
            return null;
        _raidInventoryReader = async (path, catalogue, at, ct) =>
        {
            using var picture = await MenuPicture.OpenAsync(path, ct: ct);
            return picture is null ? null : await reader.ReadRaidAsync(picture, catalogue, at, ct);
        };
        return _raidInventoryReader;
    }

    // A position screenshot taken in a raid, after its extract list: if it shows an inventory screen, its page for the
    // glance. Reading takes a moment (decoding, the recognition), so it runs outside the gate, and counts only if the same
    // raid is still on when it is done.
    private async Task ReadRaidInventoryAsync(ScreenshotSeen seen)
    {
        Func<string, DeskCatalogue, DateTimeOffset, CancellationToken, Task<DeskReading?>>? read;
        DeskCatalogue? catalogue;
        DateTime? raid;
        await _gate.WaitAsync();
        try
        {
            if (!_readStash || _tracker.State.Phase != RaidPhase.InRaid || _data is null)
                return;
            catalogue = DeskCatalogueNow();
            if (catalogue is null)
                return;
            read = ReaderForRaidInventory();
            raid = _tracker.State.RaidStartedAt;
        }
        finally
        {
            _gate.Release();
        }
        if (read is null)
            return;
        var started = Stopwatch.GetTimestamp();
        var reading = await read(seen.Path, catalogue, new DateTimeOffset(seen.CreatedAt), _stop.Token);
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (reading?.Page is not { } page || reading.Screen != DeskScreen.Raid)
            return;
        await _gate.WaitAsync();
        try
        {
            if (!_readStash || _data is null || _tracker.State.Phase != RaidPhase.InRaid || _tracker.State.RaidStartedAt != raid)
                return;
            TakeGlance(page, seen.CreatedAt, ms);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Under the gate, in the raid: the reading's page with its values, published; nothing of it written anywhere.
    private void TakeGlance(DeskPage page, DateTime takenAt, double ms)
    {
        var sources = _sources;
        var data = _data;
        var attached = DeskLedger.Attach(page);
        var sheet = DeskSheet.Of(attached, id => DeskData.Facts(sources, id), DeskData.Flea(sources), DeskKeepNow(), traderName: id => data?.TraderName(id) ?? "");
        var loot = attached.Regions.FirstOrDefault(r => r.IsLoot)?.Title;
        _glance = new RaidGlance(sheet, loot, new DateTimeOffset(takenAt), ++_glanceReadings);
        var cells = attached.Regions.SelectMany(r => r.Cells).ToList();
        var lootCells = attached.Regions.Where(r => r.IsLoot).SelectMany(r => r.Cells).ToList();
        Study.Game("glance.read", ("regions", attached.Regions.Count), ("loot", loot is not null), ("cells", cells.Count),
            ("named", cells.Count(c => c.Ids.Count == 1)), ("unsure", cells.Count(c => c.Ids.Count > 1)), ("lootCells", lootCells.Count), ("ms", ms));
        AppLog.Info($"Inventory read from a raid screenshot: {attached.Regions.Count} regions{(loot is not null ? ", a LOOT pane" : "")}, {cells.Count} cells, " +
                    $"{cells.Count(c => c.Ids.Count == 1)} named, {cells.Count(c => c.Ids.Count > 1)} one of several ({ms:0} ms); not kept");
        Publish();
    }
}
