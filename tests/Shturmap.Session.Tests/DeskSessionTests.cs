using System.Globalization;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session.Tests;

// The stash desk in the session (owner, 2026-10-19; docs/DESIGN.md §2, "For the stash desk"): a menu screenshot of the
// stash or the GEAR tab is read for the desk too, its page kept per mode in desk.json and published with its values and
// the regions it changed; never in a raid, never with the tick off, never before the item file is in. The reader is the
// test's own: it says what a picture "shows" (tests\Shturmap.Game.Tests reads drawn ones).
public class DeskSessionTests
{
    private const string Water = "000000000000000000000401", Wallet = "000000000000000000000402";

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

    private static GameData Data(GameMode mode)
    {
        var data = SessionRig.Data(mode);
        return new GameData
        {
            Mode = data.Mode,
            Language = data.Language,
            Maps = data.Maps,
            Tasks = data.Tasks,
            Traders = data.Traders,
            ItemNames = new Dictionary<string, string> { [Water] = "Bottle of water", [Wallet] = "Wallet" },
            ItemShortNames = new Dictionary<string, string> { [Water] = "Water", [Wallet] = "Wallet" },
            MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt,
        };
    }

    private static DeskReading Shows(params DeskCell[] stash) =>
        new(DeskScreen.Stash, new DeskPage(DateTimeOffset.Now, [new DeskRegion("stash", DeskRegionKind.Stash, 10, 12, stash)]), 5);

    private static SessionRig Rig(Func<DeskReading?> shows, List<string>? asked = null, bool sources = true) =>
        new(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = Data,
            GivenSources = sources ? Sources : null,
            DeskReader = (path, _, _, _) =>
            {
                asked?.Add(path);
                return Task.FromResult(shows());
            },
        });

    private static int _shots;

    // A screenshot the game names without a position: taken in the menus.
    private static void MenuScreenshot(SessionRig rig)
    {
        var name = string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}[{0:HH-mm}] ({1}).png", DateTime.Now, Interlocked.Increment(ref _shots));
        File.WriteAllBytes(Path.Combine(rig.ScreenshotsFolder, name), []);
    }

    [Fact]
    public async Task A_menu_screenshot_is_read_for_the_desk_kept_and_published_with_its_values()
    {
        var shows = Shows(new DeskCell(0, 0, 1, 2, [Water]) { Count = 2 }, new DeskCell(3, 0, 1, 1, [Wallet]) { Holds = DeskHolds.Contents });
        await using var rig = Rig(() => shows);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Sources is not null, "the data and the item file");
        MenuScreenshot(rig);
        var snapshot = await rig.Until(s => s.Desk is not null, "the desk's page");
        var desk = snapshot.Desk!;
        Assert.Equal(GameMode.Pve, desk.Mode);
        Assert.True(desk.First);
        Assert.Equal(["stash"], desk.Changed);
        var cells = desk.Sheet.Regions.Single().Cells;
        // Two bottles at the flea's net after the fee, more than the trader's 5,000 each.
        var water = cells.Single(c => c.Cell.ItemId == Water).Figure!;
        Assert.Equal(DeskSource.Flea, water.Source);
        Assert.Equal(2 * 20_000 - DeskValues.Fee(10_000, 20_000, 2, new DeskFleaRules(0.05, 0.05)), water.Value);
        // The wallet: no offers, its trader's price, its contents unseen: "+" on it and on the stash's total.
        var wallet = cells.Single(c => c.Cell.ItemId == Wallet);
        Assert.Equal((DeskSource.Trader, true), (wallet.Figure!.Source, wallet.Figure.Plus));
        Assert.True(desk.Sheet.Totals.StashPlus);
        Assert.Equal(water.Value + 900, desk.Sheet.Totals.Stash);
        Assert.True(File.Exists(Path.Combine(rig.AppFolder, "desk.json")));

        // The same screenshot again changes nothing; a count that changed opens the stash again.
        MenuScreenshot(rig);
        await rig.Until(s => s.Desk!.Reading == 2, "the second reading");
        Assert.Empty(rig.Snapshot.Desk!.Changed);
        shows = Shows(new DeskCell(0, 0, 1, 2, [Water]) { Count = 3 });
        MenuScreenshot(rig);
        await rig.Until(s => s.Desk!.Reading == 3, "the third reading");
        Assert.Equal(["stash"], rig.Snapshot.Desk!.Changed);
        Assert.False(rig.Snapshot.Desk.First);
    }

    [Fact]
    public async Task The_page_in_hand_comes_back_at_the_next_start()
    {
        await using var rig = Rig(() => Shows(new DeskCell(0, 0, 1, 2, [Water])));
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Sources is not null, "the data and the item file");
        MenuScreenshot(rig);
        await rig.Until(s => s.Desk is not null, "the desk's page");
        await rig.RestartAsync();
        await rig.StartAsync();
        var snapshot = await rig.Until(s => s.Desk is not null, "the desk's page after the restart");
        Assert.Equal(Water, snapshot.Desk!.Sheet.Regions.Single().Cells.Single().Cell.ItemId);
        Assert.Empty(snapshot.Desk.Changed);
    }

    // Never a raid screenshot for the desk, the menus' view (docs/DESIGN.md §2): the in-raid glance is a step of its own.
    [Fact]
    public async Task In_a_raid_no_screenshot_is_read_for_the_desk()
    {
        var asked = new List<string>();
        await using var rig = Rig(() => Shows(new DeskCell(0, 0, 1, 2, [Water])), asked);
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.Sources is not null, "the raid running");
        MenuScreenshot(rig);
        await Task.Delay(600, TestContext.Current.CancellationToken);
        Assert.Empty(asked);
        Assert.Null(rig.Snapshot.Desk);
    }

    [Fact]
    public async Task With_the_tick_off_no_screenshot_is_read_for_the_desk()
    {
        var asked = new List<string>();
        await using var rig = Rig(() => Shows(new DeskCell(0, 0, 1, 2, [Water])), asked);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Sources is not null, "the item file");
        await rig.Session.SetReadStashAsync(false);
        MenuScreenshot(rig);
        await Task.Delay(600, TestContext.Current.CancellationToken);
        Assert.Empty(asked);
        Assert.Null(rig.Snapshot.Desk);
    }

    // Without the item file the desk has no footprints and no prices: its reading waits for it.
    [Fact]
    public async Task Before_the_item_file_is_in_no_screenshot_is_read_for_the_desk()
    {
        var asked = new List<string>();
        await using var rig = Rig(() => Shows(new DeskCell(0, 0, 1, 2, [Water])), asked, sources: false);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null, "the data");
        MenuScreenshot(rig);
        await Task.Delay(600, TestContext.Current.CancellationToken);
        Assert.Empty(asked);
    }
}
