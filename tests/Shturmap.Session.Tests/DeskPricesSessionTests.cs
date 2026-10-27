using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Core.Raid;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Screenshots;
using static Shturmap.Session.Tests.DeskSessionTests;

namespace Shturmap.Session.Tests;

// The stash desk's prices while it is on screen (owner, 2026-10-26: "When finding prices in the stash it's important that
// they are recent"; 2026-10-27, D3 E: "Ask when the file is due"; docs/DESIGN.md §3, "The stash desk's prices come per
// reading"): while no rhythm of the file is known, the item file is asked for again as it turns five minutes old; once two
// files a beat apart were seen, when the next is due by its Last-Modified, then each minute while it is late; always while
// the desk is on screen, out of a raid, and at once when the desk comes back with a file past due; never while it is off
// screen or in a raid; one request at a time, a failure (or a saved copy no newer) tried again five minutes on, never in a
// loop, the head line's age offline meanwhile; what comes in prices the page again, quietly, and the same file again
// changes nothing. A clock and a loader of the test's own: no request leaves the PC, and minutes pass at the test's word.
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

        // The file's Last-Modified as tarkov.dev would send it; asked for again while it is the same, the file in hand comes
        // back confirmed ("not modified"), as GameDataLoader gives it.
        public DateTimeOffset? Modified { get; set; }

        private ItemSources? _last;

        public async Task<ItemSources> LoadAsync(GameMode mode, string language, CancellationToken ct)
        {
            Interlocked.Increment(ref _asked);
            if (Held is { } held)
                await held.Task.WaitAsync(ct);
            if (Fails is { } e)
                throw e;
            var now = new DateTimeOffset(clock.Now);
            if (Modified is { } file && _last is { ModifiedAt: { } had } last && had == file)
                return last.Confirmed(now);
            return _last = PricedAt(Price, Saved ? savedAt : now, Modified);
        }
    }

    // The water scanned three minutes before the file was had.
    private static ItemSources PricedAt(double water, DateTimeOffset at, DateTimeOffset? modified = null) => new()
    {
        Items = new Dictionary<string, ApiItem>
        {
            [Water] = new(Water, ["provisions"], 1, water, [], BasePrice: 10_000, Width: 1, Height: 2, LastOfferCount: 80,
                SellToTrader: [new ApiTraderPrice("trader-a", 5_000, "RUB", 5_000)], Updated: (at - TimeSpan.FromMinutes(3)).ToString("o")),
            [Wallet] = new(Wallet, ["container"], 1, null, [], BasePrice: 1_000, HasGrid: true,
                SellToTrader: [new ApiTraderPrice("trader-a", 900, "RUB", 900)]),
        },
        Barters = Array.Empty<ApiBarter>().ToLookup(b => ""),
        Crafts = Array.Empty<ApiCraft>().ToLookup(c => ""),
        Stations = new Dictionary<string, string>(),
        FleaMarket = new ApiFleaMarket(15, true, 0.05, 0.05, false),
        PricesAt = at,
        ModifiedAt = modified,
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
        // The head line's age from the newest scan in the file (owner, 2026-10-27, D2 C), tarkov.dev reached.
        Assert.Equal(clock.At(Minutes(2)), after.PricesScannedAt);
        Assert.True(after.PricesReached);

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
        // The watch settles first: the first answer's wake can have it looking (on screen still) as the desk leaves, and a
        // clock moved 18 minutes in that instant would make a look due (CI's runner, 2026-10-27); the clock never jumps so.
        await Quiet();
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
        Assert.True(before.PricesReached);
        loader.Fails = new HttpRequestException("no route to json.tarkov.dev");
        rig.Session.SetDeskOnScreen(true);
        clock.Set(Minutes(5));
        await rig.Until(() => loader.Asked == 1, "the prices asked for");
        // tarkov.dev not reached: the head line's age says so (owner, 2026-10-27, D2 C), the prices as they were.
        Assert.False((await rig.Until(s => !s.Desk!.PricesReached, "the prices offline")).Desk!.PricesReached);
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
        Assert.True(after.PricesReached);
    }

    // D3 E (owner, 2026-10-27): files of 0:04 and 0:09 seen at five and ten minutes, a beat of five: the next is due at
    // 14:45 (0:09 + 5:00 + 0:45), sooner than five minutes after the last ask; still the same file then, so each minute,
    // the page untouched; the file of 14:50 at 15:45, and the next due at 20:35.
    [Fact]
    public async Task Once_the_files_rhythm_is_seen_the_prices_are_asked_for_when_the_next_file_is_due_and_each_minute_while_it_is_late()
    {
        var (rig, clock, loader) = await DeskAsync();
        await using var _ = rig;
        rig.Session.SetDeskOnScreen(true);

        // No rhythm yet: every five minutes.
        loader.Modified = clock.At(Minutes(4));
        clock.Set(Minutes(5));
        await rig.Until(() => loader.Asked == 1, "the first ask, five minutes on");
        await rig.Until(s => s.Desk!.PricesAt == clock.At(Minutes(5)), "the file of 0:04");
        loader.Modified = clock.At(Minutes(9));
        clock.Set(Minutes(10));
        await rig.Until(() => loader.Asked == 2, "the second ask, five minutes on");
        await rig.Until(s => s.Desk!.PricesAt == clock.At(Minutes(10)), "the file of 0:09");

        // Two files a beat apart: the next is due at 14:45.
        clock.Set(Minutes(14.7));
        await Quiet();
        Assert.Equal(2, loader.Asked);
        clock.Set(Minutes(14.75));
        await rig.Until(() => loader.Asked == 3, "the ask when the next file is due");

        // Still the same file: nothing on the page changes, and the next ask is a minute on.
        var unchanged = rig.Snapshot.Desk!;
        await Quiet();
        Assert.Same(unchanged, rig.Snapshot.Desk);
        clock.Set(Minutes(15.7));
        await Quiet();
        Assert.Equal(3, loader.Asked);
        clock.Set(Minutes(15.75));
        await rig.Until(() => loader.Asked == 4, "the ask a minute on");

        // The next file at last: the page priced again; due a beat after it.
        loader.Modified = clock.At(Minutes(14) + TimeSpan.FromSeconds(50));
        loader.Price = 40_000;
        clock.Set(Minutes(16.75));
        await rig.Until(() => loader.Asked == 5, "the ask another minute on");
        var after = (await rig.Until(s => WaterOf(s.Desk!).Figure?.Value == 2 * 40_000, "the file of 14:50")).Desk!;
        Assert.Equal(unchanged.Reading, after.Reading);
        clock.Set(Minutes(20.55));
        await Quiet();
        Assert.Equal(5, loader.Asked);
        clock.Set(Minutes(20.6));
        await rig.Until(() => loader.Asked == 6, "the ask when the file after is due");
    }
}
