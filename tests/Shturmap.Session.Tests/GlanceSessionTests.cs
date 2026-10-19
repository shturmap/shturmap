using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;

namespace Shturmap.Session.Tests;

// The in-raid glance's reading in the session (owner, 2026-10-19; docs/DESIGN.md §2, "In a raid", and §4, *The in-raid
// glance*): a screenshot taken in a raid is read for an inventory screen after its extract list, its page published with
// its totals (SessionSnapshot.Glance) and kept nowhere: no desk.json, never the desk's page in hand; it goes with the raid.
// The menu screenshots' tick says yes or no. The reader is the test's own: it says what a picture "shows".
public class GlanceSessionTests
{
    private const string Water = "000000000000000000000401", Rig = "000000000000000000000402";

    private static ItemSources Sources(GameMode _) => new()
    {
        Items = new Dictionary<string, ApiItem>
        {
            [Water] = new(Water, ["provisions"], 1, 20_000, [], BasePrice: 10_000, Width: 1, Height: 2, LastOfferCount: 80,
                SellToTrader: [new ApiTraderPrice("trader-a", 5_000, "RUB", 5_000)]),
            [Rig] = new(Rig, ["rig"], 1, null, [], BasePrice: 1_000, HasGrid: true, SellToTrader: [new ApiTraderPrice("trader-a", 900, "RUB", 900)]),
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
            ItemNames = new Dictionary<string, string> { [Water] = "Bottle of water", [Rig] = "Rig" },
            ItemShortNames = new Dictionary<string, string> { [Water] = "Water", [Rig] = "Rig" },
            MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt,
        };
    }

    // A body looted: a bottle in the player's pockets, the body's rig not searched yet and a bottle in its pockets.
    private static DeskReading Body() => new(DeskScreen.Raid, new DeskPage(DateTimeOffset.Now,
    [
        new DeskRegion("pockets", DeskRegionKind.Pockets, 4, 2, [new DeskCell(0, 0, 1, 2, [Water])]),
        new DeskRegion("loot:rig", DeskRegionKind.LootRig, 0, 0, [new DeskCell(0, 0, 1, 1, [Rig]) { Slot = "rig", Holds = DeskHolds.Contents }])
        {
            Search = DeskSearch.Unsearched,
            Title = "LOOT · BODY",
        },
        new DeskRegion("loot:pockets", DeskRegionKind.LootPockets, 4, 2, [new DeskCell(0, 0, 1, 2, [Water]) { Count = 2 }]) { Title = "LOOT · BODY" },
    ]), 5);

    private static SessionRig Rigged(Func<DeskReading?> shows, List<string> asked) =>
        new(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = Data,
            GivenSources = Sources,
            RaidInventoryReader = (path, _, _, _) =>
            {
                lock (asked)
                    asked.Add(path);
                return Task.FromResult(shows());
            },
        });

    [Fact]
    public async Task A_raid_screenshot_of_an_inventory_is_published_for_the_glance_and_kept_nowhere()
    {
        var asked = new List<string>();
        await using var rig = Rigged(Body, asked);
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.Sources is not null, "the raid running");
        rig.Screenshot(12, 1, 34);
        var snapshot = await rig.Until(s => s.Glance is not null, "the glance's reading");
        var glance = snapshot.Glance!;
        Assert.Equal(("LOOT · BODY", 1), (glance.Loot, glance.Reading));
        var totals = glance.Sheet.Totals;
        // ON YOU: the bottle in the player's pockets; LOOT: the body's two bottles and its rig, whose contents are unseen
        // until searched: "+".
        Assert.True(totals.OnYou > 0);
        Assert.True(totals.Loot > totals.OnYou);
        Assert.True(totals.LootPlus);
        Assert.Contains(glance.Sheet.Regions, r => r.Part == DeskPart.Loot && r.Region.Key == "loot:rig" && r.Plus);
        // Kept nowhere: not the desk's page, no desk file.
        Assert.Null(snapshot.Desk);
        Assert.False(File.Exists(Path.Combine(rig.AppFolder, "desk.json")));

        // The raid ends: the glance goes with it.
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
        var over = await rig.Until(s => s.Raid.Phase == RaidPhase.Menu, "the raid's end");
        Assert.Null(over.Glance);
    }

    [Fact]
    public async Task With_the_tick_off_or_out_of_a_raid_no_screenshot_is_read_for_the_glance()
    {
        var asked = new List<string>();
        await using var rig = Rigged(Body, asked);
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Sources is not null, "the item file");
        // Out of a raid: a position screenshot is placed, never read for the glance.
        rig.Screenshot(12, 1, 34);
        await Task.Delay(600, TestContext.Current.CancellationToken);
        lock (asked)
            Assert.Empty(asked);

        await rig.Session.SetReadStashAsync(false);
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-1));
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid, "the raid running");
        rig.Screenshot(12, 1, 34);
        await Task.Delay(600, TestContext.Current.CancellationToken);
        lock (asked)
            Assert.Empty(asked);
        Assert.Null(rig.Snapshot.Glance);
    }
}
