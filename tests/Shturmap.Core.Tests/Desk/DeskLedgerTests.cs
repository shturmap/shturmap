using Shturmap.Core.Desk;
using Shturmap.Core.Logs;

namespace Shturmap.Core.Tests.Desk;

// The stash desk's page in hand (docs/DESIGN.md §2, "For the stash desk", what is kept; §4, *What it mirrors*, *The head
// line and the fold*; §8, "The desk file"): a newer reading replaces the regions it shows and keeps the others, the cells a
// window covered keep their last reading, a container window attaches to its one container, and the fold learns which
// regions changed. Made-up ids, 2026-01-01.
public sealed class DeskLedgerTests : IDisposable
{
    private const string Zhuk = "000000000000000000000201", ZhukOther = "000000000000000000000202", Water = "000000000000000000000203",
        Wallet = "000000000000000000000204", Salewa = "000000000000000000000205";

    private static readonly DateTimeOffset At = new(2026, 1, 1, 15, 40, 0, TimeSpan.FromHours(1));

    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-desk-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static DeskCell Cell(int i, int j, int w, int h, params string[] ids) => new(i, j, w, h, ids);

    private static DeskRegion Stash(params DeskCell[] cells) => new("stash", DeskRegionKind.Stash, 10, 12, cells);

    private static DeskRegion Backpack(params DeskCell[] cells) => new("backpack", DeskRegionKind.Backpack, 5, 7, cells);

    private static DeskRegion Window(string title, string[] titleIds, params DeskCell[] cells) =>
        new(DeskRegion.WindowKey(title), DeskRegionKind.ContainerWindow, 5, 4, cells) { Title = title, TitleIds = titleIds };

    private static DeskPage Page(int minutes, params DeskRegion[] regions) => new(At.AddMinutes(minutes), regions);

    [Fact]
    public void The_first_reading_changes_every_region()
    {
        var ledger = new DeskLedger();
        var change = ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 2, Water)), Backpack()));
        Assert.True(change.First);
        Assert.Equal(["backpack", "stash"], change.Changed.Order());
    }

    [Fact]
    public void A_newer_reading_replaces_the_regions_it_shows_and_keeps_the_others()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 2, Water)), Backpack(Cell(0, 0, 1, 1, Wallet))));
        var change = ledger.Apply(GameMode.Pve, Page(1, Stash(Cell(3, 3, 1, 2, Salewa))));
        Assert.False(change.First);
        Assert.Equal(["stash"], change.Changed);
        var page = ledger.Page(GameMode.Pve)!;
        Assert.Equal(Salewa, Assert.Single(page.Region("stash")!.Cells).ItemId);
        Assert.Equal(Wallet, Assert.Single(page.Region("backpack")!.Cells).ItemId);
        Assert.Equal(At.AddMinutes(1), page.ReadAt);
    }

    [Fact]
    public void A_reading_that_changed_nothing_changes_nothing()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 2, Water) with { FoundInRaid = true })));
        // The found-in-raid mark isn't what the fold compares: place, ids, count and condition are.
        Assert.Empty(ledger.Apply(GameMode.Pve, Page(1, Stash(Cell(0, 0, 1, 2, Water) with { FoundInRaid = false }))).Changed);
        Assert.Equal(["stash"], ledger.Apply(GameMode.Pve, Page(2, Stash(Cell(0, 0, 1, 2, Water) with { Count = 2 }))).Changed);
        Assert.Equal(["stash"], ledger.Apply(GameMode.Pve, Page(3, Stash(Cell(0, 0, 1, 2, Water) with { Count = 2, Print = new DeskPrint(30, 60) }))).Changed);
        Assert.Equal(["stash"], ledger.Apply(GameMode.Pve, Page(4, Stash(Cell(0, 0, 1, 2, Water, Salewa) with { Count = 2, Print = new DeskPrint(30, 60) }))).Changed);
    }

    [Fact]
    public void The_cells_a_window_covered_keep_their_last_reading()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 2, Water), Cell(4, 4, 2, 2, Salewa))));
        // The next screenshot: a window over the stash's lower right; the water moved, the cells under the window unread.
        var reading = Stash(Cell(1, 0, 1, 2, Water)) with { Covered = [new DeskRect(3, 3, 1, 1), new DeskRect(4, 4, 1, 1)] };
        ledger.Apply(GameMode.Pve, Page(1, reading));
        var cells = ledger.Page(GameMode.Pve)!.Region("stash")!.Cells;
        Assert.Contains(cells, c => c.ItemId == Salewa && c.I == 4);
        Assert.Contains(cells, c => c.ItemId == Water && c.I == 1);
        Assert.DoesNotContain(cells, c => c.ItemId == Water && c.I == 0);
    }

    [Fact]
    public void A_window_attaches_to_the_one_container_of_its_name()
    {
        var ledger = new DeskLedger();
        // "Zhuk" names two rigs of one size: the cell is a "?", and its window's grid attaches to it either way.
        var change = ledger.Apply(GameMode.Pve, Page(0,
            Backpack(Cell(1, 3, 4, 3, Zhuk, ZhukOther) with { Holds = DeskHolds.Contents }),
            Window("Zhuk", [Zhuk, ZhukOther], Cell(0, 0, 1, 2, Water))));
        var page = change.Page;
        var window = page.Region("window:zhuk")!;
        Assert.Equal(new DeskAnchor("backpack", 1, 3), window.AttachedTo);
        var cell = page.Region("backpack")!.Cells[0];
        Assert.False(page.Unseen(page.Region("backpack")!, cell));
        Assert.Equal(["backpack", "window:zhuk"], page.InReadingOrder().Select(r => r.Key).Where(k => k != "stash"));
    }

    [Fact]
    public void A_window_attaches_across_readings_and_narrows_a_question_cell_to_its_name()
    {
        var ledger = new DeskLedger();
        // The first screenshot shows the container (read as one of a rig or a body armour of one name)...
        ledger.Apply(GameMode.Pve, Page(0, Backpack(Cell(1, 3, 3, 3, Zhuk, Salewa) with { Holds = DeskHolds.Contents })));
        // ...the next one its window over it: the container's cell under the window keeps its reading.
        var change = ledger.Apply(GameMode.Pve, Page(1,
            Backpack() with { Covered = [new DeskRect(1, 3, 3, 3)] },
            Window("Zhuk", [Zhuk], Cell(0, 0, 1, 2, Water))));
        Assert.Equal(new DeskAnchor("backpack", 1, 3), change.Page.Region("window:zhuk")!.AttachedTo);
        Assert.Equal([Zhuk], change.Page.Region("backpack")!.Cells.Single().Ids);
        Assert.Contains("window:zhuk", change.Changed);
    }

    [Fact]
    public void Several_containers_of_one_name_leave_the_window_attached_to_none()
    {
        var ledger = new DeskLedger();
        var change = ledger.Apply(GameMode.Pve, Page(0,
            Stash(Cell(0, 0, 1, 1, Wallet) with { Holds = DeskHolds.Contents }, Cell(2, 0, 1, 1, Wallet) with { Holds = DeskHolds.Contents }),
            Backpack(Cell(0, 0, 1, 1, Wallet) with { Holds = DeskHolds.Contents }),
            Window("Wallet", [Wallet], Cell(0, 0, 1, 1, Water))));
        var window = change.Page.Region("window:wallet")!;
        Assert.Null(window.AttachedTo);
        Assert.Equal(3, window.SameName);
        Assert.Equal(["backpack", "stash"], window.SameNameIn.Order());
    }

    [Fact]
    public void The_modes_keep_a_page_each()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 2, Water))));
        Assert.Null(ledger.Page(GameMode.Pvp));
        Assert.True(ledger.Apply(GameMode.Pvp, Page(1, Stash())).First);
        Assert.Single(ledger.Page(GameMode.Pve)!.Region("stash")!.Cells);
    }

    [Fact]
    public void The_file_keeps_the_pages_ids_and_marks_and_nothing_else()
    {
        var path = Path.Combine(_root, "desk.json");
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0,
            new DeskRegion("loadout", DeskRegionKind.Loadout, 11, 1, [Cell(1, 0, 1, 1, Zhuk, ZhukOther) with { Slot = "headwear", Print = new DeskPrint(46, 50) }]),
            Backpack(Cell(1, 3, 4, 3, Zhuk) with { Holds = DeskHolds.Contents, FoundInRaid = true, Count = 1 }) with { Parts = [new DeskRect(0, 0, 5, 7)] },
            Window("Zhuk", [Zhuk], Cell(0, 0, 1, 2, Water) with { Count = 3 })));
        Assert.True(ledger.Dirty);
        ledger.Save(path);
        Assert.False(ledger.Dirty);
        var text = File.ReadAllText(path);
        Assert.Contains("\"version\": 1", text);
        Assert.Contains("\"pve\"", text);
        Assert.Contains("\"46/50\"", text);

        var page = DeskLedger.Load(path).Page(GameMode.Pve)!;
        Assert.Equal(At, page.ReadAt);
        var slot = page.Region("loadout")!.Cells.Single();
        Assert.Equal("headwear", slot.Slot);
        Assert.Equal(new DeskPrint(46, 50), slot.Print);
        Assert.Equal([Zhuk, ZhukOther], slot.Ids);
        var zhuk = page.Region("backpack")!.Cells.Single();
        Assert.Equal((DeskHolds.Contents, true), (zhuk.Holds, zhuk.FoundInRaid));
        Assert.Equal([new DeskRect(0, 0, 5, 7)], page.Region("backpack")!.Parts);
        var window = page.Region("window:zhuk")!;
        Assert.Equal(("Zhuk", new DeskAnchor("backpack", 1, 3)), (window.Title, window.AttachedTo));
        Assert.Equal(3, window.Cells.Single().Count);
    }

    [Fact]
    public void A_file_that_cant_be_read_is_set_aside_and_the_pages_start_over()
    {
        var path = Path.Combine(_root, "desk.json");
        File.WriteAllText(path, "{ not json");
        string? warned = null;
        var ledger = DeskLedger.Load(path, w => warned = w);
        Assert.Null(ledger.Page(GameMode.Pve));
        Assert.NotNull(warned);
        Assert.True(File.Exists(path + ".unreadable"));
    }

    [Fact]
    public void A_pick_stays_with_its_cell_until_a_reading_changes_it()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 1, Zhuk, ZhukOther))));
        Assert.True(ledger.Pick(GameMode.Pve, "stash", null, 0, 0, ZhukOther));
        Assert.Equal(ZhukOther, ledger.Page(GameMode.Pve)!.Region("stash")!.Cells.Single().ItemId);
        Assert.False(ledger.Pick(GameMode.Pve, "stash", null, 0, 0, Water));
        // The same cell read again keeps its pick; a reading that changes it drops it.
        ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 1, Zhuk, ZhukOther))));
        Assert.Equal(ZhukOther, ledger.Page(GameMode.Pve)!.Region("stash")!.Cells.Single().ItemId);
        ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 1, Zhuk, ZhukOther) with { Count = 2 })));
        Assert.Null(ledger.Page(GameMode.Pve)!.Region("stash")!.Cells.Single().ItemId);
    }

    // The owner's test of 2026-10-19: the Keys window read whole (73 keys), then after the game's SORT as one "?" across six
    // cells; that reading took the window's place and its keys left the total. A window with no cell read never replaces
    // the page's: the page's stays, unchanged, and the change says which it kept.
    [Fact]
    public void A_window_with_no_cell_read_keeps_the_pages()
    {
        var ledger = new DeskLedger();
        var keys = Enumerable.Range(0, 12).Select(n => Cell(n % 4, n / 4, 1, 1, Water)).ToArray();
        ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(4, 4, 2, 2, Wallet) with { Holds = DeskHolds.Contents }), Window("Keys", [Wallet], keys)));
        var change = ledger.Apply(GameMode.Pve, Page(1, Stash(Cell(4, 4, 2, 2, Wallet) with { Holds = DeskHolds.Contents }),
            Window("Keys", [Wallet], Cell(0, 2, 4, 1)) with { Unread = 8 }));
        Assert.Equal(["window:keys"], change.Refused);
        Assert.DoesNotContain("window:keys", change.Changed);
        var window = ledger.Page(GameMode.Pve)!.Region("window:keys")!;
        Assert.Equal(12, window.Cells.Count);
        Assert.Equal(new DeskAnchor("stash", 4, 4), window.AttachedTo);
        // A first reading of a window keeps what it read, a "?" and all: there is nothing to keep instead.
        var first = new DeskLedger().Apply(GameMode.Pve, Page(0, Window("Keys", [Wallet], Cell(0, 2, 4, 1))));
        Assert.Empty(first.Refused);
        Assert.Single(first.Page.Region("window:keys")!.Cells);
    }

    // A region read with fewer than a third of the cells the page has for it, while more of it held something unread than
    // was read, is a failed reading (the lines between the items not found): the page's stays.
    [Fact]
    public void A_region_read_mostly_unread_keeps_the_pages()
    {
        var ledger = new DeskLedger();
        var full = Enumerable.Range(0, 30).Select(n => Cell(n % 10, n / 10, 1, 1, Water)).ToArray();
        ledger.Apply(GameMode.Pve, Page(0, Stash(full)));
        var change = ledger.Apply(GameMode.Pve, Page(1, Stash(Cell(0, 0, 1, 1, Water), Cell(1, 0, 1, 1, Salewa)) with { Unread = 25 }));
        Assert.Equal(["stash"], change.Refused);
        Assert.Empty(change.Changed);
        Assert.Equal(30, ledger.Page(GameMode.Pve)!.Region("stash")!.Cells.Count);
    }

    // A region the player emptied reads its empty cells as empty: few cells, nothing unread, and it replaces the page's (an
    // unloaded backpack after a raid, a stash paged to an emptier page). So does a window read whole with fewer items.
    [Fact]
    public void A_region_that_emptied_replaces_the_pages()
    {
        var ledger = new DeskLedger();
        var full = Enumerable.Range(0, 30).Select(n => Cell(n % 5, n / 5, 1, 1, Water)).ToArray();
        ledger.Apply(GameMode.Pve, Page(0, Backpack(full), Window("Keys", [Wallet], full[..12])));
        var change = ledger.Apply(GameMode.Pve, Page(1, Backpack(Cell(0, 0, 1, 1, Salewa)) with { Unread = 1 }, Window("Keys", [Wallet], Cell(0, 0, 1, 1, Water))));
        Assert.Empty(change.Refused);
        Assert.Equal(["backpack", "window:keys"], change.Changed.Order());
        Assert.Equal(Salewa, Assert.Single(ledger.Page(GameMode.Pve)!.Region("backpack")!.Cells).ItemId);
        Assert.Single(ledger.Page(GameMode.Pve)!.Region("window:keys")!.Cells);
        // A backpack taken off (a reading of its region with nothing in it) replaces it too.
        Assert.Empty(ledger.Apply(GameMode.Pve, Page(2, Backpack())).Refused);
        Assert.Empty(ledger.Page(GameMode.Pve)!.Region("backpack")!.Cells);
    }

    // A file written before the fix of 2026-10-19 kept a window's compartment found twice: its cells lay over each other and
    // counted twice. Read back, each place is taken once.
    [Fact]
    public void A_file_with_a_cell_read_twice_takes_it_once()
    {
        var path = Path.Combine(_root, "desk.json");
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, Window("Junk", [Wallet], Cell(0, 0, 2, 1, Water), Cell(2, 0, 1, 1, Salewa), Cell(0, 0, 2, 1, Water), Cell(1, 0, 1, 1, Zhuk))));
        ledger.Save(path);
        var cells = DeskLedger.Load(path).Page(GameMode.Pve)!.Region("window:junk")!.Cells;
        Assert.Equal([Water, Salewa], cells.Select(c => c.ItemId));
    }

    // A slot's "?" is picked by its key (the helmet in three colours).
    [Fact]
    public void A_slot_is_picked_by_its_key()
    {
        var ledger = new DeskLedger();
        var loadout = new DeskRegion("loadout", DeskRegionKind.Loadout, 0, 0, [new DeskCell(0, 0, 0, 0, [Zhuk, ZhukOther]) { Slot = "headwear" }]);
        ledger.Apply(GameMode.Pve, new DeskPage(DateTimeOffset.UnixEpoch, [loadout]));
        Assert.False(ledger.Pick(GameMode.Pve, "loadout", "earpiece", 0, 0, Zhuk));
        Assert.True(ledger.Pick(GameMode.Pve, "loadout", "headwear", 0, 0, Zhuk));
        Assert.Equal(Zhuk, ledger.Page(GameMode.Pve)!.Region("loadout")!.Cells.Single().ItemId);
    }
}
