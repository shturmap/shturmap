using System.Diagnostics;
using System.Globalization;
using Shturmap.Core.Desk;
using Shturmap.Core.Raid;
using Shturmap.Core.Screenshots;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session;

/// <summary>Why a raid screenshot gave the in-raid glance nothing (the app log's line, <see cref="GameSession.GlanceNotTaken"/>).</summary>
public enum GlanceSkip
{
    /// <summary>The menu screenshots' tick ("… and loot in a raid") is off.</summary>
    SettingOff,

    /// <summary>The game's logs show no raid running.</summary>
    NoRaid,

    /// <summary>The item data (the catalogue) isn't loaded yet.</summary>
    NoData,

    /// <summary>Windows has no text recognition language.</summary>
    NoRecognition,

    /// <summary>The picture couldn't be opened: nothing read.</summary>
    Unreadable,

    /// <summary>No inventory screen: the world, a map, another tab.</summary>
    NotInventory,

    /// <summary>The loadout beside the stash and the menus' bar: the menus' GEAR tab.</summary>
    MenusGear,

    /// <summary>The stash alone.</summary>
    MenusStash,

    /// <summary>A trader's screen.</summary>
    Trading,

    /// <summary>The raid ended, or another began, while the picture was read.</summary>
    RaidChanged,
}

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
        if (Shturmap.Game.Screenshots.DeskReader.Create(_settings.Language, Paddle()) is not { } reader)
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
    // raid is still on when it is done. One the glance doesn't take says so in the app log, once, with why (owner,
    // 2026-10-26: a weapon box's screenshots, told as the menus' GEAR tab, had left no line at all).
    private async Task ReadRaidInventoryAsync(ScreenshotSeen seen)
    {
        Func<string, DeskCatalogue, DateTimeOffset, CancellationToken, Task<DeskReading?>>? read;
        DeskCatalogue? catalogue;
        DateTime? raid;
        await _gate.WaitAsync();
        try
        {
            var why = !_readStash ? GlanceSkip.SettingOff
                : _tracker.State.Phase != RaidPhase.InRaid ? GlanceSkip.NoRaid
                : _data is null ? GlanceSkip.NoData
                : (GlanceSkip?)null;
            catalogue = why is null ? DeskCatalogueNow() : null;
            if (why is null && catalogue is null)
                why = GlanceSkip.NoData;
            if (why is { } skip)
            {
                AppLog.Info(GlanceNotTaken(seen.Info, skip));
                return;
            }
            read = ReaderForRaidInventory();
            raid = _tracker.State.RaidStartedAt;
        }
        finally
        {
            _gate.Release();
        }
        if (read is null)
        {
            AppLog.Info(GlanceNotTaken(seen.Info, GlanceSkip.NoRecognition));
            return;
        }
        var started = Stopwatch.GetTimestamp();
        var reading = await read(seen.Path, catalogue!, new DateTimeOffset(seen.CreatedAt), _stop.Token);
        var ms = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (reading?.Page is not { } page || reading.Screen != DeskScreen.Raid)
        {
            AppLog.Info(GlanceNotTaken(seen.Info, reading is null ? GlanceSkip.Unreadable : SkipFor(reading.Screen), ms));
            return;
        }
        await _gate.WaitAsync();
        try
        {
            var why = !_readStash ? GlanceSkip.SettingOff
                : _data is null ? GlanceSkip.NoData
                : _tracker.State.Phase != RaidPhase.InRaid || _tracker.State.RaidStartedAt != raid ? GlanceSkip.RaidChanged
                : (GlanceSkip?)null;
            if (why is { } skip)
            {
                AppLog.Info(GlanceNotTaken(seen.Info, skip, ms));
                return;
            }
            TakeGlance(page, seen.Info, seen.CreatedAt, ms);
        }
        finally
        {
            _gate.Release();
        }
    }

    // What a reading that is no raid inventory was told as.
    private static GlanceSkip SkipFor(DeskScreen screen) => screen switch
    {
        DeskScreen.Gear => GlanceSkip.MenusGear,
        DeskScreen.Stash => GlanceSkip.MenusStash,
        DeskScreen.Trading => GlanceSkip.Trading,
        DeskScreen.Raid => GlanceSkip.Unreadable,
        _ => GlanceSkip.NotInventory,
    };

    /// <summary>A raid screenshot as the app log names it: its date, minute and number as the game wrote them,
    /// "2026-01-01[13-35] (0)", never its position (docs/DESIGN.md §2, "Never share a position": the app log records
    /// none).</summary>
    public static string ScreenshotNamed(ScreenshotInfo shot) =>
        string.Create(CultureInfo.InvariantCulture, $"{shot.TakenAt:yyyy-MM-dd}[{shot.TakenAt:HH-mm}] ({shot.Counter})");

    /// <summary>The app log's line for a raid screenshot the in-raid glance didn't take, with why, and how long its reading
    /// took where it was read.</summary>
    public static string GlanceNotTaken(ScreenshotInfo shot, GlanceSkip why, double? ms = null) =>
        $"Raid screenshot {ScreenshotNamed(shot)} not taken for the in-raid glance: " + why switch
        {
            GlanceSkip.SettingOff => "the setting \"Read your stash, gear and tasks from menu screenshots, and loot in a raid\" is off",
            GlanceSkip.NoRaid => "no raid running as the game's logs tell it",
            GlanceSkip.NoData => "the item data isn't loaded yet",
            GlanceSkip.NoRecognition => "this Windows has no text recognition language",
            GlanceSkip.Unreadable => "read nothing (the picture couldn't be opened)",
            GlanceSkip.NotInventory => "not an inventory screen",
            GlanceSkip.MenusGear => "told as the menus' GEAR tab",
            GlanceSkip.MenusStash => "told as the menus' stash",
            GlanceSkip.Trading => "told as the trading screen",
            GlanceSkip.RaidChanged => "the raid changed meanwhile",
            _ => why.ToString(),
        } + (ms is { } t ? string.Create(CultureInfo.InvariantCulture, $" ({t:0} ms)") : "");

    // Under the gate, in the raid: the reading's page with its values, published; nothing of it written anywhere.
    private void TakeGlance(DeskPage page, ScreenshotInfo shot, DateTime takenAt, double ms)
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
        AppLog.Info($"Inventory read from raid screenshot {ScreenshotNamed(shot)}: {attached.Regions.Count} regions{(loot is not null ? ", a LOOT pane" : "")}, {cells.Count} cells, " +
                    $"{cells.Count(c => c.Ids.Count == 1)} named, {cells.Count(c => c.Ids.Count > 1)} one of several ({ms:0} ms); not kept");
        Publish();
    }
}
