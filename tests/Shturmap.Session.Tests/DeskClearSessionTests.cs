using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session.Tests;

// Clearing the desk (owner, 2026-10-26: "We need an option to clear the desk because it quickly gets messy"; from the
// coordinator's options, "Button + fresh after raid"; docs/DESIGN.md §4, *The stash desk*, *Clearing the page*): CLEAR
// empties the mode's page at once and keeps it so in desk.json, its notice's UNDO puts it back exactly while no reading came;
// the first reading after a raid (the game's logs: one started and ended since the page's last reading) starts a new page,
// with the same UNDO; a reading without a raid between keeps the page as before; each mode its own. The reader is the
// test's own, with the screenshot's time it says.
public class DeskClearSessionTests
{
    private const string Water = DeskSessionTests.Water, Wallet = DeskSessionTests.Wallet;

    private static ItemSources Sources(GameMode _) => new()
    {
        Items = new Dictionary<string, ApiItem>
        {
            [Water] = new(Water, ["provisions"], 1, 20_000, [], BasePrice: 10_000, Width: 1, Height: 2, LastOfferCount: 80,
                SellToTrader: [new ApiTraderPrice("trader-a", 5_000, "RUB", 5_000)]),
            [Wallet] = new(Wallet, ["container"], 1, null, [], BasePrice: 1_000, HasGrid: true,
                SellToTrader: [new ApiTraderPrice("trader-a", 900, "RUB", 900)]),
        },
        Barters = Array.Empty<ApiBarter>().ToLookup(b => ""),
        Crafts = Array.Empty<ApiCraft>().ToLookup(c => ""),
        Stations = new Dictionary<string, string>(),
        FleaMarket = new ApiFleaMarket(15, true, 0.05, 0.05, false),
        PricesAt = DateTimeOffset.Now,
    };

    // What a screenshot taken at a time shows: the stash with these cells, and the backpack with a wallet when asked.
    private static DeskReading Shows(DateTimeOffset at, bool backpack, params DeskCell[] stash)
    {
        var regions = new List<DeskRegion> { new("stash", DeskRegionKind.Stash, 10, 12, stash) };
        if (backpack)
            regions.Add(new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 6, [new DeskCell(0, 0, 1, 1, [Wallet])]));
        return new DeskReading(DeskScreen.Stash, new DeskPage(at, regions), 5);
    }

    private static DeskCell WaterAt(int i, int count = 1) => new(i, 0, 1, 2, [Water]) { Count = count };

    private static SessionRig Rig(Func<DeskReading?> shows) =>
        new(logSessionStarted: DateTime.Now.AddHours(-2), configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = DeskSessionTests.Data,
            GivenSources = Sources,
            DeskReader = (_, _, _, _) => Task.FromResult(shows()),
        });

    private static async Task<SessionRig> Started(Func<DeskReading?> shows, string mode = "Pve")
    {
        var rig = Rig(shows);
        rig.Log("Session mode: " + mode, DateTime.Now.AddHours(-1));
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Sources is not null, "the data and the item file");
        return rig;
    }

    private static string DeskFile(SessionRig rig) => Path.Combine(rig.AppFolder, "desk.json");

    // A raid from its loading to its end, in a mode ("Pve", or "Regular" for PvP), at these times of the log.
    private static void Raid(SessionRig rig, DateTime started, DateTime ended, string mode = "Pve")
    {
        rig.Log("Session mode: " + mode, started.AddMinutes(-3));
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0", started.AddMinutes(-3));
        rig.Log("scene preset path:maps/customs_preset.bundle rcid:x.scenespreset.asset", started.AddMinutes(-2));
        rig.Log("TRACE-NetworkGameCreate profileStatus: 'Profileid: 000000000000000000000003, Status: Busy, RaidMode: Online, Location: bigmap, shortId: TEST01'",
            started.AddMinutes(-2).AddSeconds(2));
        rig.Log("GameStarting:80.26(1.7) real:95.46(2.73) diff:15.19", started.AddSeconds(-1));
        rig.Log("GameStarted:90.6(10.33) real:107.49(12.02) diff:16.89", started);
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0", ended);
    }

    private static async Task<SessionSnapshot> Reading(SessionRig rig, int n)
    {
        DeskSessionTests.MenuScreenshot(rig);
        return await rig.Until(s => s.Desk?.Reading == n, $"reading {n}");
    }

    private static DeskUndoOffer Offered(SessionRig rig, string text) => rig.Said.Last(n => n.Text == text).Undo!;

    [Fact]
    public async Task Clear_empties_the_page_keeps_it_so_and_undo_puts_it_back_exactly()
    {
        var shows = Shows(DateTimeOffset.Now.AddMinutes(-3), backpack: true, WaterAt(0, 2));
        await using var rig = await Started(() => shows);
        var before = (await Reading(rig, 1)).Desk!;
        var page = before.Sheet.Page;

        Assert.True(await rig.Session.ClearDeskAsync());
        var cleared = (await rig.Until(s => s.Desk is { Sheet.Page.Regions.Count: 0 }, "the page cleared")).Desk!;
        // Not a reading: nothing comes in, nothing opens the desk anew.
        Assert.Equal(before.Reading, cleared.Reading);
        Assert.Empty(cleared.Changed);
        Assert.Equal((0d, 0d), (cleared.Sheet.Totals.OnYou, cleared.Sheet.Totals.Stash));
        Assert.Empty(DeskLedger.Load(DeskFile(rig)).Page(GameMode.Pve)!.Regions);
        var offer = Offered(rig, SessionTexts.DeskCleared);
        Assert.Equal(before.Reading, offer.Reading);
        // A page of no region has nothing to clear: no notice.
        Assert.False(await rig.Session.ClearDeskAsync());
        Assert.Single(rig.Said, n => n.Text == SessionTexts.DeskCleared);

        // UNDO: the page exactly as it was, its regions' reading times with it, and kept so.
        Assert.True(await rig.Session.UndoDeskAsync(offer.Id));
        var back = (await rig.Until(s => s.Desk is { Sheet.Page.Regions.Count: > 0 }, "the page put back")).Desk!;
        Assert.Same(page, back.Sheet.Page);
        Assert.Equal(before.Reading, back.Reading);
        Assert.Equal(before.Sheet.Totals.Stash, back.Sheet.Totals.Stash);
        var file = DeskLedger.Load(DeskFile(rig)).Page(GameMode.Pve)!;
        Assert.Equal(page.ReadAt, file.ReadAt);
        Assert.Equal(["backpack", "stash"], file.Regions.Select(r => r.Key).Order());
        Assert.Equal(2, file.Region("stash")!.Cells.Single().Count);
        // Once used, it's gone.
        Assert.False(await rig.Session.UndoDeskAsync(offer.Id));

        // The page cleared stays cleared at the next start.
        Assert.True(await rig.Session.ClearDeskAsync());
        await rig.Until(s => s.Desk is { Sheet.Page.Regions.Count: 0 }, "the page cleared again");
        await rig.RestartAsync();
        await rig.StartAsync();
        await rig.Until(s => s.Desk is { Sheet.Page.Regions.Count: 0 } && s.Sources is not null, "the cleared page after the restart");
    }

    [Fact]
    public async Task Undo_is_gone_once_a_new_reading_came_and_the_next_reading_is_the_modes_first()
    {
        var shows = Shows(DateTimeOffset.Now.AddMinutes(-3), backpack: true, WaterAt(0));
        await using var rig = await Started(() => shows);
        await Reading(rig, 1);
        Assert.True(await rig.Session.ClearDeskAsync());
        var offer = Offered(rig, SessionTexts.DeskCleared);
        shows = Shows(DateTimeOffset.Now, backpack: false, WaterAt(3));
        var next = (await Reading(rig, 2)).Desk!;
        Assert.True(next.First);
        Assert.Equal(["stash"], next.Sheet.Page.Regions.Select(r => r.Key));
        Assert.False(await rig.Session.UndoDeskAsync(offer.Id));
        Assert.Equal(["stash"], rig.Snapshot.Desk!.Sheet.Page.Regions.Select(r => r.Key));
    }

    [Fact]
    public async Task The_first_reading_after_a_raid_starts_a_new_page_and_the_next_keeps_it()
    {
        var shows = Shows(DateTimeOffset.Now.AddMinutes(-40), backpack: true, WaterAt(0));
        await using var rig = await Started(() => shows);
        await Reading(rig, 1);

        Raid(rig, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-5));
        await rig.Until(() => rig.Cues.Any(c => c.Kind == CueKind.RaidOver), "the raid over");
        shows = Shows(DateTimeOffset.Now, backpack: false, WaterAt(4));
        var fresh = (await Reading(rig, 2)).Desk!;
        // Nothing of the page before the raid, dimmed or not: the reading's regions as a first reading has them.
        Assert.True(fresh.First);
        Assert.Equal(["stash"], fresh.Sheet.Page.Regions.Select(r => r.Key));
        Assert.Equal(4, fresh.Sheet.Page.Region("stash")!.Cells.Single().I);
        var offer = await Said(rig, SessionTexts.DeskFreshAfterRaid);
        Assert.Equal(2, offer.Reading);
        Assert.Equal(["stash"], DeskLedger.Load(DeskFile(rig)).Page(GameMode.Pve)!.Regions.Select(r => r.Key));

        // The next reading, no raid between, keeps the page as any reading does.
        shows = Shows(DateTimeOffset.Now.AddSeconds(1), backpack: true, WaterAt(4));
        var kept = (await Reading(rig, 3)).Desk!;
        Assert.False(kept.First);
        Assert.Equal(["backpack", "stash"], kept.Sheet.Page.Regions.Select(r => r.Key).Order());
        Assert.Single(rig.Said, n => n.Text == SessionTexts.DeskFreshAfterRaid);
        Assert.False(await rig.Session.UndoDeskAsync(offer.Id));
    }

    [Fact]
    public async Task The_new_pages_undo_keeps_the_page_before_the_raid_with_the_reading_taken_into_it()
    {
        var shows = Shows(DateTimeOffset.Now.AddMinutes(-40), backpack: true, WaterAt(0));
        await using var rig = await Started(() => shows);
        await Reading(rig, 1);
        Raid(rig, DateTime.Now.AddMinutes(-30), DateTime.Now.AddMinutes(-5));
        await rig.Until(() => rig.Cues.Any(c => c.Kind == CueKind.RaidOver), "the raid over");
        shows = Shows(DateTimeOffset.Now, backpack: false, WaterAt(4));
        await Reading(rig, 2);
        var offer = await Said(rig, SessionTexts.DeskFreshAfterRaid);

        Assert.True(await rig.Session.UndoDeskAsync(offer.Id));
        var back = (await rig.Until(s => s.Desk?.Sheet.Page.Region("backpack") is not null, "the page before the raid")).Desk!;
        Assert.Equal(2, back.Reading);
        Assert.Equal(4, back.Sheet.Page.Region("stash")!.Cells.Single().I);
        // The backpack the reading didn't show, as a reading without a raid would have kept it: a step dimmer and dated.
        Assert.True(back.Sheet.Page.Region("backpack")!.ReadAt < back.Sheet.Page.ReadAt);
        Assert.Equal(["backpack", "stash"], DeskLedger.Load(DeskFile(rig)).Page(GameMode.Pve)!.Regions.Select(r => r.Key).Order());
    }

    [Fact]
    public async Task A_reading_without_a_raid_between_keeps_the_page_and_a_cancelled_load_is_no_raid()
    {
        var shows = Shows(DateTimeOffset.Now.AddMinutes(-40), backpack: true, WaterAt(0));
        await using var rig = await Started(() => shows);
        await Reading(rig, 1);
        // Matching cancelled: the raid never began.
        rig.Log("scene preset path:maps/customs_preset.bundle rcid:x.scenespreset.asset", DateTime.Now.AddMinutes(-20));
        await rig.Until(s => s.Raid.Phase == RaidPhase.Loading, "the load");
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0", DateTime.Now.AddMinutes(-19));
        await rig.Until(s => s.Raid.Phase == RaidPhase.Menu, "back in the menus");
        shows = Shows(DateTimeOffset.Now, backpack: false, WaterAt(4));
        var kept = (await Reading(rig, 2)).Desk!;
        Assert.False(kept.First);
        Assert.Equal(["backpack", "stash"], kept.Sheet.Page.Regions.Select(r => r.Key).Order());
        Assert.DoesNotContain(SessionTexts.DeskFreshAfterRaid, rig.Notices);
    }

    // A raid that ended before the page's last reading starts nothing: the page was read after it.
    [Fact]
    public async Task A_raid_before_the_pages_last_reading_starts_no_new_page()
    {
        var shows = Shows(DateTimeOffset.Now.AddMinutes(-3), backpack: true, WaterAt(0));
        await using var rig = Rig(() => shows);
        Raid(rig, DateTime.Now.AddMinutes(-40), DateTime.Now.AddMinutes(-10));
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Sources is not null && s.Raid.Phase == RaidPhase.Menu, "the data, back in the menus");
        await Reading(rig, 1);
        shows = Shows(DateTimeOffset.Now, backpack: false, WaterAt(4));
        var kept = (await Reading(rig, 2)).Desk!;
        Assert.Equal(["backpack", "stash"], kept.Sheet.Page.Regions.Select(r => r.Key).Order());
        Assert.DoesNotContain(SessionTexts.DeskFreshAfterRaid, rig.Notices);
    }

    // Each mode its own: a PvP raid starts no new PvE page, a PvE raid does.
    [Fact]
    public async Task A_raid_starts_a_new_page_in_its_own_mode_only()
    {
        var shows = Shows(DateTimeOffset.Now.AddMinutes(-50), backpack: true, WaterAt(0));
        await using var rig = await Started(() => shows);
        await Reading(rig, 1);
        Raid(rig, DateTime.Now.AddMinutes(-45), DateTime.Now.AddMinutes(-30), mode: "Regular");
        await rig.Until(s => s.Mode == GameMode.Pvp && s.LastRaid is not null && s.Raid.Phase == RaidPhase.Menu, "the PvP raid over");
        rig.Log("Session mode: Pve", DateTime.Now.AddMinutes(-29));
        await rig.Until(s => s.Mode == GameMode.Pve && s.Desk is not null, "back in PvE");
        shows = Shows(DateTimeOffset.Now.AddMinutes(-20), backpack: false, WaterAt(4));
        var kept = (await Reading(rig, 2)).Desk!;
        Assert.Equal(["backpack", "stash"], kept.Sheet.Page.Regions.Select(r => r.Key).Order());
        Assert.DoesNotContain(SessionTexts.DeskFreshAfterRaid, rig.Notices);

        var ended = DateTime.Now.AddMinutes(-2);
        Raid(rig, DateTime.Now.AddMinutes(-15), ended);
        await rig.Until(s => s.LastRaid is { } last && Math.Abs((last.EndedAt - ended).TotalSeconds) < 1 && s.Raid.Phase == RaidPhase.Menu,
            "the PvE raid over");
        shows = Shows(DateTimeOffset.Now, backpack: false, WaterAt(6));
        var fresh = (await Reading(rig, 3)).Desk!;
        Assert.Equal(["stash"], fresh.Sheet.Page.Regions.Select(r => r.Key));
        await Said(rig, SessionTexts.DeskFreshAfterRaid);
    }

    // The notice comes once the screenshot's reading is all taken.
    private static async Task<DeskUndoOffer> Said(SessionRig rig, string text)
    {
        await rig.Until(() => rig.Said.Any(n => n.Text == text), text);
        return Offered(rig, text);
    }
}
