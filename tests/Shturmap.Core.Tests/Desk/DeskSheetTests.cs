using Shturmap.Core.Desk;

namespace Shturmap.Core.Tests.Desk;

// The head line and each region's total (docs/DESIGN.md §4, *The head line and the fold*, *What it mirrors*): ON YOU the
// loadout and the middle column, STASH the stash, a window where its container is, one attached to none where all the
// containers of its name are (or in neither), a "+" where contents are unseen, KEEP the notches. Made-up items.
public class DeskSheetTests
{
    private const string Rig = "000000000000000000000301", Water = "000000000000000000000302", Wallet = "000000000000000000000303",
        Gun = "000000000000000000000304", Key = "000000000000000000000305", Pouch = "000000000000000000000306";

    private static readonly DeskFleaRules Flea = new(0.05, 0.05);

    private static readonly Dictionary<string, DeskItemFacts> Items = new()
    {
        [Rig] = new(Rig) { Traders = [("trader-a", 30_000)], Kind = DeskItemKind.Container, W = 4, H = 3 },
        [Water] = new(Water) { Traders = [("trader-a", 10_000)], Kind = DeskItemKind.FoodDrink, H = 2 },
        [Wallet] = new(Wallet) { Traders = [("trader-a", 1_000)], Kind = DeskItemKind.Container },
        [Gun] = new(Gun) { Traders = [("trader-a", 50_000)], Kind = DeskItemKind.Gun },
        [Key] = new(Key) { Traders = [("trader-a", 200_000)], StackMax = 1 },
        // The secure container: no flea, no trader: no figure.
        [Pouch] = new(Pouch) { NoFlea = true, Kind = DeskItemKind.Container },
    };

    private static DeskItemFacts? Facts(string id) => Items.GetValueOrDefault(id);

    private static DeskCell Cell(int i, int j, params string[] ids) => new(i, j, 1, 1, ids);

    [Fact]
    public void On_you_and_the_stash_each_count_their_own()
    {
        var page = new DeskPage(DateTimeOffset.MinValue, [
            new("loadout", DeskRegionKind.Loadout, 11, 1, [Cell(7, 0, Gun) with { Slot = "onsling", Holds = DeskHolds.Mods }]),
            new("pouch", DeskRegionKind.Pouch, 3, 3, [Cell(0, 0, Pouch) with { Slot = "pouch", Holds = DeskHolds.Contents }, Cell(0, 0, Water)]),
            new("stash", DeskRegionKind.Stash, 10, 10, [Cell(0, 0, Key), Cell(1, 0, Wallet) with { Holds = DeskHolds.Contents }]),
        ]);
        var sheet = DeskSheet.Of(page, Facts, Flea);
        Assert.Equal(60_000, sheet.Totals.OnYou);
        Assert.Equal(201_000, sheet.Totals.Stash);
        // A weapon's mods are unseen on you; the wallet's contents in the stash; the pouch's own grid is inline.
        Assert.True(sheet.Totals.OnYouPlus);
        Assert.True(sheet.Totals.StashPlus);
        var pouch = sheet.Regions.Single(r => r.Region.Key == "pouch");
        Assert.False(pouch.Cells[0].Unseen);
        Assert.Null(pouch.Cells[0].Figure);
        Assert.Equal(["loadout", "pouch", "stash"], sheet.Regions.Select(r => r.Region.Key));
    }

    // The owner's test of 2026-10-19: a drill (it doesn't stack) read "×310" put 53 M into a Junk window's total. A count
    // past what the item stacks to is a misread: one, at least. An item the file gives no stack size keeps its count.
    [Fact]
    public void A_count_past_the_items_stack_counts_one()
    {
        var page = new DeskPage(DateTimeOffset.MinValue, [
            new("stash", DeskRegionKind.Stash, 10, 10, [Cell(0, 0, Key) with { Count = 310 }, Cell(1, 0, Water) with { Count = 3 }]),
        ]);
        var sheet = DeskSheet.Of(page, Facts, Flea);
        var cells = sheet.Regions.Single().Cells;
        Assert.Equal(1, cells[0].Cell.Count);
        Assert.Equal(200_000, cells[0].Figure!.Value);
        Assert.Equal(3, cells[1].Cell.Count);
        Assert.Equal(230_000, sheet.Totals.Stash);
        // A flea item counts its net in the total, its cell shows the listing price.
        var flea = new DeskItemFacts("000000000000000000000399") { LastLow = 20_000, BasePrice = 10_000, Traders = [("trader-a", 100)] };
        var listed = DeskSheet.Of(new DeskPage(DateTimeOffset.MinValue, [new("stash", DeskRegionKind.Stash, 10, 10, [Cell(0, 0, flea.Id)])]),
            id => id == flea.Id ? flea : null, Flea);
        Assert.Equal(20_000, listed.Regions.Single().Cells.Single().Figure!.Value);
        Assert.Equal(20_000 - DeskValues.Fee(10_000, 20_000, 1, Flea), listed.Totals.Stash);
    }

    [Fact]
    public void A_window_counts_where_its_container_is_and_takes_the_plus_away()
    {
        var page = DeskLedger.Attach(new DeskPage(DateTimeOffset.MinValue, [
            new("backpack", DeskRegionKind.Backpack, 5, 7, [Cell(1, 3, Rig) with { W = 4, H = 3, Holds = DeskHolds.Contents }]),
            new(DeskRegion.WindowKey("Zhuk"), DeskRegionKind.ContainerWindow, 5, 4, [Cell(0, 0, Water) with { H = 2 }]) { Title = "Zhuk", TitleIds = [Rig] },
            new("stash", DeskRegionKind.Stash, 10, 10, [Cell(0, 0, Key)]),
        ]));
        var sheet = DeskSheet.Of(page, Facts, Flea);
        Assert.Equal(DeskPart.OnYou, sheet.Regions.Single(r => r.Region.Kind == DeskRegionKind.ContainerWindow).Part);
        Assert.Equal(40_000, sheet.Totals.OnYou);
        Assert.False(sheet.Totals.OnYouPlus);
        // The container's figure stays its own; the contents count in the part it is in.
        Assert.Equal(30_000, sheet.Regions.Single(r => r.Region.Key == "backpack").Cells.Single().Figure!.Value);
    }

    [Fact]
    public void A_window_of_containers_split_between_on_you_and_the_stash_counts_in_neither()
    {
        var page = DeskLedger.Attach(new DeskPage(DateTimeOffset.MinValue, [
            new("backpack", DeskRegionKind.Backpack, 5, 7, [Cell(0, 0, Wallet) with { Holds = DeskHolds.Contents }]),
            new("stash", DeskRegionKind.Stash, 10, 10, [Cell(0, 0, Wallet) with { Holds = DeskHolds.Contents }]),
            new(DeskRegion.WindowKey("Wallet"), DeskRegionKind.ContainerWindow, 2, 2, [Cell(0, 0, Key)]) { Title = "Wallet", TitleIds = [Wallet] },
        ]));
        var sheet = DeskSheet.Of(page, Facts, Flea);
        var window = sheet.Regions.Single(r => r.Region.Kind == DeskRegionKind.ContainerWindow);
        Assert.Equal(DeskPart.Neither, window.Part);
        Assert.Equal(200_000, window.Total);
        Assert.Equal(1_000, sheet.Totals.OnYou);
        Assert.Equal(1_000, sheet.Totals.Stash);
        // Read in reading order, it stands at the page's end.
        Assert.Equal(DeskRegionKind.ContainerWindow, sheet.Regions[^1].Region.Kind);
    }

    [Fact]
    public void Keep_counts_the_cells_with_a_now_next_and_later_notch()
    {
        var keep = DeskKeep.Of([
                new DeskQuestNeeds("q1", [], [(Key, 1, false)]),
                new DeskQuestNeeds("q2", ["q1"], [(Water, 1, false)]),
                new DeskQuestNeeds("q3", ["q2"], [(Wallet, 1, true)]),
            ],
            new HashSet<string> { "q1" }, new HashSet<string>(), []);
        var page = new DeskPage(DateTimeOffset.MinValue, [
            new("stash", DeskRegionKind.Stash, 10, 10, [Cell(0, 0, Key), Cell(1, 0, Key), Cell(2, 0, Water), Cell(3, 0, Wallet), Cell(4, 0, Gun)]),
        ]);
        var sheet = DeskSheet.Of(page, Facts, Flea, keep);
        Assert.Equal((2, 1, 1), (sheet.Totals.KeepNow, sheet.Totals.KeepNext, sheet.Totals.KeepLater));
        var key = sheet.Regions[0].Cells[0];
        Assert.Equal(DeskKeepLevel.Now, key.Keep);
        Assert.Equal("q1", Assert.Single(key.Needs).For);
    }

    // A "?" cell takes a keep notch only when every candidate has one, the weakest of them.
    [Fact]
    public void A_question_cell_takes_the_weakest_notch_its_candidates_share()
    {
        var keep = DeskKeep.Of([new DeskQuestNeeds("q1", [], [(Key, 1, false)]), new DeskQuestNeeds("q2", ["q1"], [(Water, 1, false)])],
            new HashSet<string> { "q1" }, new HashSet<string>(), []);
        var page = new DeskPage(DateTimeOffset.MinValue, [
            new("stash", DeskRegionKind.Stash, 10, 10, [Cell(0, 0, Key, Water), Cell(1, 0, Key, Gun)]),
        ]);
        var cells = DeskSheet.Of(page, Facts, Flea, keep).Regions[0].Cells;
        Assert.Equal(DeskKeepLevel.Next, cells[0].Keep);
        Assert.Equal(DeskKeepLevel.None, cells[1].Keep);
        Assert.True(cells[0].Figure!.Unsure);
    }

    [Fact]
    public void A_trader_figure_carries_the_traders_name()
    {
        var page = new DeskPage(DateTimeOffset.MinValue, [new("stash", DeskRegionKind.Stash, 10, 10, [Cell(0, 0, Key)])]);
        var figure = DeskSheet.Of(page, Facts, Flea, traderName: id => id == "trader-a" ? "Therapist" : "").Regions[0].Cells[0].Figure!;
        Assert.Equal((DeskSource.Trader, "Therapist"), (figure.Source, figure.TraderName));
    }
}
