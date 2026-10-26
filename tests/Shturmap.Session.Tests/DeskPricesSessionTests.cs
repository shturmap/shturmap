using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;
using static Shturmap.Session.Tests.DeskSessionTests;

namespace Shturmap.Session.Tests;

// The stash desk's prices while it is on screen (owner, 2026-10-26: "When finding prices in the stash it's important that
// they are recent"; docs/DESIGN.md §3, "The stash desk's prices come per reading"): the item file is asked for again as it
// turns five minutes old while the desk is on screen, out of a raid, and at once when the desk comes back with older
// prices; never while it is off screen or in a raid; one request at a time, a failure (or a saved copy no newer) tried
// again five minutes on, never in a loop; what comes in prices the page again, quietly. A clock and a loader of the test's
// own: no request leaves the PC, and five minutes pass at the test's word.
public class DeskPricesSessionTests
{
    // The clock the session's prices go by, moved by the test.
    private sealed class FakeClock(DateTime start)
    {
        private long _ticks = start.Ticks;

        public DateTime Now => new(Interlocked.Read(ref _ticks), DateTimeKind.Local);

        public DateTimeOffset At(TimeSpan after) => new(new DateTime(start.Ticks, DateTimeKind.Local) + after);

        public void Set(TimeSpan after) => Interlocked.Exchange(ref _ticks, (start + after).Ticks);
    }

    // tarkov.dev's item file asked for again, as the test has it answer: new prices of now, a failure, a saved copy no
    // newer, or an answer held back.
    private sealed class FakeLoader(FakeClock clock, DateTimeOffset savedAt)
    {
        private int _asked;

        public int Asked => Volatile.Read(ref _asked);

        public double Price { get; set; } = 30_000;
        public Exception? Fails { get; set; }
        public bool Saved { get; set; }
        public TaskCompletionSource? Held { get; set; }

        public async Task<ItemSources> LoadAsync(GameMode mode, string language, CancellationToken ct)
        {
            Interlocked.Increment(ref _asked);
            if (Held is { } held)
                await held.Task.WaitAsync(ct);
            if (Fails is { } e)
                throw e;
            return PricedAt(Price, Saved ? savedAt : new DateTimeOffset(clock.Now));
        }
    }

    private static ItemSources PricedAt(double water, DateTimeOffset at) => new()
    {
        Items = new Dictionary<string, ApiItem>
        {
            [Water] = new(Water, ["provisions"], 1, water, [], BasePrice: 10_000, Width: 1, Height: 2, LastOfferCount: 80,
                SellToTrader: [new ApiTraderPrice("trader-a", 5_000, "RUB", 5_000)]),
            [Wallet] = new(Wallet, ["container"], 1, null, [], BasePrice: 1_000, HasGrid: true,
                SellToTrader: [new ApiTraderPrice("trader-a", 900, "RUB", 900)]),
        },
        Barters = Array.Empty<ApiBarter>().ToLookup(b => ""),
        Crafts = Array.Empty<ApiCraft>().ToLookup(c => ""),
        Stations = new Dictionary<string, string>(),
        FleaMarket = new ApiFleaMarket(15, true, 0.05, 0.05, false),
        PricesAt = at,
    };

    private static TimeSpan Minutes(double m) => TimeSpan.FromMinutes(m);

    // A session in the menus with the desk's page in hand (two bottles of water and a wallet), its item file of the clock's
    // start, the clock and the loader the test's.
    private static async Task<(SessionRig Rig, FakeClock Clock, FakeLoader Loader)> DeskAsync()
    {
        var clock = new FakeClock(DateTime.Now);
        var start = clock.At(TimeSpan.Zero);
        var loader = new FakeLoader(clock, start);
        var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = DeskSessionTests.Data,
            GivenSources = _ => PricedAt(20_000, start),
            GivenDeskPrices = loader.LoadAsync,
            Clock = () => clock.Now,
            DeskPricesLook = TimeSpan.FromMilliseconds(20),
            DeskReader = (_, _, _, _) => Task.FromResult<DeskReading?>(
                Shows(new DeskCell(0, 0, 1, 2, [Water]) { Count = 2 }, new DeskCell(3, 0, 1, 1, [Wallet]) { Holds = DeskHolds.Contents })),
        });
        rig.Log("Session mode: Pve");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Sources is not null, "the data and the item file");
        MenuScreenshot(rig);
        await rig.Until(s => s.Desk is not null, "the desk's page");
        return (rig, clock, loader);
    }

    // Long enough for the watch, which looks every 20 ms here, to have asked if it would.
    private static Task Quiet() => Task.Delay(400, TestContext.Current.CancellationToken);

    private static DeskCellSheet WaterOf(DeskView desk) => desk.Sheet.Regions.Single().Cells.Single(c => c.Cell.ItemId == Water);

    [Fact]
    public async Task On_screen_the_prices_are_asked_for_again_as_they_turn_five_minutes_old_and_the_page_is_priced_again_quietly()
    {
        var (rig, clock, loader) = await DeskAsync();
        await using var _ = rig;
        var before = rig.Snapshot.Desk!;
        Assert.Equal(2 * 20_000, WaterOf(before).Figure!.Value);
        rig.Session.SetDeskOnScreen(true);

        // Under five minutes nothing is asked; at five, once.
        clock.Set(Minutes(4.99));
        await Quiet();
        Assert.Equal(0, loader.Asked);
        clock.Set(Minutes(5));
        await rig.Until(() => loader.Asked == 1, "the prices asked for at five minutes");

        // The page priced again: the water's figure and the stash's total at the new price, the prices' age from the new
        // file; the same reading, so nothing on the page comes in or glows (MainWindow.UpdateDesk: DeskSwitch.IsNew).
        var after = (await rig.Until(s => s.Desk!.PricesAt == clock.At(Minutes(5)), "the page priced again")).Desk!;
        Assert.Equal(2 * 30_000, WaterOf(after).Figure!.Value);
        Assert.True(after.Sheet.Totals.Stash > before.Sheet.Totals.Stash);
        Assert.Equal(before.Reading, after.Reading);
        Assert.Equal(before.First, after.First);

        // The next five minutes after that file, not before.
        clock.Set(Minutes(9.99));
        await Quiet();
        Assert.Equal(1, loader.Asked);
        clock.Set(Minutes(10));
        await rig.Until(() => loader.Asked == 2, "the prices asked for again five minutes on");
    }

    [Fact]
    public async Task Off_screen_nothing_is_asked_and_back_on_screen_with_older_prices_they_are_asked_for_at_once()
    {
        var (rig, clock, loader) = await DeskAsync();
        await using var _ = rig;

        // Never shown, or shown and then left or minimised (MainWindow says off screen: DeskSwitch.OnScreen).
        clock.Set(Minutes(12));
        await Quiet();
        Assert.Equal(0, loader.Asked);
        rig.Session.SetDeskOnScreen(true);
        await rig.Until(() => loader.Asked == 1, "the prices asked for as the desk came on screen");
        await rig.Until(s => s.Desk!.PricesAt == clock.At(Minutes(12)), "the page priced again");
        rig.Session.SetDeskOnScreen(false);
        clock.Set(Minutes(30));
        await Quiet();
        Assert.Equal(1, loader.Asked);
        rig.Session.SetDeskOnScreen(true);
        await rig.Until(() => loader.Asked == 2, "the prices asked for as the desk came back");
    }

    [Fact]
    public async Task In_a_raid_nothing_is_asked()
    {
        var (rig, clock, loader) = await DeskAsync();
        await using var _ = rig;
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now);
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid, "the raid running");
        // The window takes Raid by itself; even told the desk is on screen, the session asks nothing.
        rig.Session.SetDeskOnScreen(true);
        clock.Set(Minutes(12));
        await Quiet();
        Assert.Equal(0, loader.Asked);
    }

    [Fact]
    public async Task One_request_at_a_time()
    {
        var (rig, clock, loader) = await DeskAsync();
        await using var _ = rig;
        loader.Held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rig.Session.SetDeskOnScreen(true);
        clock.Set(Minutes(5));
        await rig.Until(() => loader.Asked == 1, "the prices asked for");

        // While the answer is on its way: more time, the desk shown again, a new reading: no second request.
        clock.Set(Minutes(16));
        rig.Session.SetDeskOnScreen(false);
        rig.Session.SetDeskOnScreen(true);
        MenuScreenshot(rig);
        await rig.Until(s => s.Desk!.Reading == 2, "the second reading");
        await Quiet();
        Assert.Equal(1, loader.Asked);

        // The answer: prices of now (16 minutes), the next ask five minutes on.
        loader.Held.SetResult();
        await rig.Until(s => s.Desk!.PricesAt == clock.At(Minutes(16)), "the page priced again");
        await Quiet();
        Assert.Equal(1, loader.Asked);
        clock.Set(Minutes(21));
        await rig.Until(() => loader.Asked == 2, "the next ask");
    }

    [Fact]
    public async Task A_failure_or_a_saved_copy_no_newer_is_tried_again_five_minutes_on_never_in_a_loop()
    {
        var (rig, clock, loader) = await DeskAsync();
        await using var _ = rig;
        var before = rig.Snapshot.Desk!;
        loader.Fails = new HttpRequestException("no route to json.tarkov.dev");
        rig.Session.SetDeskOnScreen(true);
        clock.Set(Minutes(5));
        await rig.Until(() => loader.Asked == 1, "the prices asked for");
        await Quiet();
        Assert.Equal(1, loader.Asked);
        clock.Set(Minutes(9.99));
        // Shown again within the five minutes: still nothing.
        rig.Session.SetDeskOnScreen(false);
        rig.Session.SetDeskOnScreen(true);
        await Quiet();
        Assert.Equal(1, loader.Asked);

        // tarkov.dev not reached with a copy saved: the loader gives that copy back, as old as the page's. Not taken, and
        // not asked for again until five minutes on either.
        loader.Fails = null;
        loader.Saved = true;
        clock.Set(Minutes(10));
        await rig.Until(() => loader.Asked == 2, "the prices asked for again five minutes on");
        await Quiet();
        Assert.Equal(2, loader.Asked);
        Assert.Equal(before.PricesAt, rig.Snapshot.Desk!.PricesAt);
        Assert.Equal(2 * 20_000, WaterOf(rig.Snapshot.Desk).Figure!.Value);

        // Then it comes.
        loader.Saved = false;
        clock.Set(Minutes(15));
        await rig.Until(() => loader.Asked == 3, "the third ask");
        var after = (await rig.Until(s => s.Desk!.PricesAt == clock.At(Minutes(15)), "the page priced again")).Desk!;
        Assert.Equal(2 * 30_000, WaterOf(after).Figure!.Value);
    }
}
