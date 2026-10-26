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

    // Owner, 2026-10-25: a pick stays "until its label changes": while the item picked is still among the cell's candidates
    // in a newer reading (a grid's cell by its place, a slot by its key), whatever else the reading changed of it; until
    // then a count read differently dropped it.
    [Fact]
    public void A_pick_stays_with_its_cell_while_the_item_is_among_its_candidates()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 1, Zhuk, ZhukOther))));
        Assert.True(ledger.Pick(GameMode.Pve, "stash", null, 0, 0, ZhukOther));
        Assert.Equal(ZhukOther, ledger.Page(GameMode.Pve)!.Region("stash")!.Cells.Single().ItemId);
        Assert.False(ledger.Pick(GameMode.Pve, "stash", null, 0, 0, Water));
        // Read again with another count and a third candidate: the pick stays.
        ledger.Apply(GameMode.Pve, Page(1, Stash(Cell(0, 0, 1, 1, Zhuk, ZhukOther, Salewa) with { Count = 2 })));
        Assert.Equal(ZhukOther, ledger.Page(GameMode.Pve)!.Region("stash")!.Cells.Single().ItemId);
        // Its label changed: the item picked is no candidate any more, and the pick goes.
        ledger.Apply(GameMode.Pve, Page(2, Stash(Cell(0, 0, 1, 1, Water, Wallet))));
        Assert.Null(ledger.Page(GameMode.Pve)!.Region("stash")!.Cells.Single().Pick);
        // Another place is another cell.
        ledger.Apply(GameMode.Pve, Page(3, Stash(Cell(0, 0, 1, 1, Zhuk, ZhukOther))));
        Assert.True(ledger.Pick(GameMode.Pve, "stash", null, 0, 0, Zhuk));
        ledger.Apply(GameMode.Pve, Page(4, Stash(Cell(1, 0, 1, 1, Zhuk, ZhukOther))));
        Assert.Null(ledger.Page(GameMode.Pve)!.Region("stash")!.Cells.Single().Pick);
    }

    // Owner, 2026-10-25 (step 1c, DeskIcons): a cell its picture settled has one id and no pick; the picture's item stands,
    // whatever was picked of the cell's candidates before.
    [Fact]
    public void A_cell_its_picture_settled_keeps_its_one_id_and_takes_no_earlier_pick()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 1, Zhuk, ZhukOther))));
        Assert.True(ledger.Pick(GameMode.Pve, "stash", null, 0, 0, ZhukOther));
        // Read again, the picture settling it as the item picked: one id, no pick.
        ledger.Apply(GameMode.Pve, Page(1, Stash(Cell(0, 0, 1, 1, ZhukOther))));
        var settled = ledger.Page(GameMode.Pve)!.Region("stash")!.Cells.Single();
        Assert.Equal([ZhukOther], settled.Ids);
        Assert.Null(settled.Pick);
        // Picked again as "one of N", then settled as the other: the picture's item stands.
        ledger.Apply(GameMode.Pve, Page(2, Stash(Cell(0, 0, 1, 1, Zhuk, ZhukOther))));
        Assert.True(ledger.Pick(GameMode.Pve, "stash", null, 0, 0, ZhukOther));
        ledger.Apply(GameMode.Pve, Page(3, Stash(Cell(0, 0, 1, 1, Zhuk))));
        settled = ledger.Page(GameMode.Pve)!.Region("stash")!.Cells.Single();
        Assert.Equal(Zhuk, settled.ItemId);
        Assert.Null(settled.Pick);
    }

    [Fact]
    public void A_slots_pick_stays_while_its_item_is_among_the_slots_candidates()
    {
        var ledger = new DeskLedger();
        DeskRegion Loadout(params string[] ids) => new("loadout", DeskRegionKind.Loadout, 0, 0, [new DeskCell(1, 0, 1, 1, ids) { Slot = "headwear" }]);
        ledger.Apply(GameMode.Pve, Page(0, Loadout(Zhuk, ZhukOther)));
        Assert.True(ledger.Pick(GameMode.Pve, "loadout", "headwear", 0, 0, ZhukOther));
        // Its condition read anew, a third candidate among the slot's: the pick stays.
        var read = Loadout(ZhukOther, Zhuk, Salewa);
        ledger.Apply(GameMode.Pve, Page(1, read with { Cells = [read.Cells[0] with { Print = new DeskPrint(30, 50) }] }));
        Assert.Equal(ZhukOther, ledger.Page(GameMode.Pve)!.Region("loadout")!.Cells.Single().ItemId);
        ledger.Apply(GameMode.Pve, Page(2, Loadout(Water, Wallet)));
        Assert.Null(ledger.Page(GameMode.Pve)!.Region("loadout")!.Cells.Single().Pick);
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

    // ---- a region read in part (owner, 2026-10-25) ----

    private static DeskCell Slot(string key, params string[] ids) => new(0, 0, 1, 1, ids) { Slot = key };

    // The page's backpack: its slot and a grid of five by six with an item in its first and its last row.
    private static DeskRegion FullBackpack() =>
        new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 6, [Slot("backpack", Zhuk), Cell(0, 0, 1, 2, Water), Cell(2, 5, 1, 1, Salewa)])
        {
            Parts = [new DeskRect(0, 0, 5, 6)],
        };

    // The test set's 11-gear.png to 13-gear-keys.png (2026-10-19): the backpack read as its slot alone (its grid out of
    // view, then under the Keys window) took the place of the page's grid of five by six. A container whose grid wasn't
    // in view keeps the page's grid, size and cells; the slot read takes the slot's place.
    [Fact]
    public void A_containers_slot_read_alone_keeps_the_pages_grid()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, FullBackpack()));
        var lone = new DeskRegion("backpack", DeskRegionKind.Backpack, 0, 0, [Slot("backpack", Zhuk, ZhukOther) with { Print = new DeskPrint(1, 2) }]) { Partial = true };
        var change = ledger.Apply(GameMode.Pve, Page(1, lone));
        Assert.Equal(["backpack"], change.Kept);
        var backpack = change.Page.Region("backpack")!;
        Assert.Equal((5, 6), (backpack.Cols, backpack.Rows));
        Assert.Equal([new DeskRect(0, 0, 5, 6)], backpack.Parts);
        Assert.Equal(new DeskPrint(1, 2), backpack.Cells.Single(c => c.IsSlot).Print);
        Assert.Equal([Water, Salewa], backpack.Cells.Where(c => !c.IsSlot).Select(c => c.ItemId));
        Assert.False(backpack.Partial);

        // Its slot under a window too (none read): the page's slot stays with its grid.
        change = ledger.Apply(GameMode.Pve, Page(2, new DeskRegion("backpack", DeskRegionKind.Backpack, 0, 0, []) { Partial = true, CoveredSlots = ["backpack"] }));
        Assert.Equal(3, change.Page.Region("backpack")!.Cells.Count);
        // A slot whose label wasn't read says nothing of the container: the page's named one stays.
        change = ledger.Apply(GameMode.Pve, Page(3, new DeskRegion("backpack", DeskRegionKind.Backpack, 0, 0, [Slot("backpack")]) { Partial = true }));
        Assert.Contains(Zhuk, change.Page.Region("backpack")!.Cells.Single(c => c.IsSlot).Ids);
    }

    // A container's region a first reading made from its heading alone (code review, 2026-10-25): no frame and no slot
    // read, its grid out of view; on the page it stood as its empty slot box, "empty" for "not read". A page without a
    // version of it doesn't take it (nor POCKETS of the kind), and draws it as never read; one with a version keeps its own.
    [Fact]
    public void A_region_made_from_its_heading_alone_stays_off_a_page_without_it()
    {
        var ledger = new DeskLedger();
        var alone = new DeskRegion("backpack", DeskRegionKind.Backpack, 0, 0, []) { Partial = true };
        var change = ledger.Apply(GameMode.Pve, Page(0, alone, new DeskRegion("pockets", DeskRegionKind.Pockets, 0, 0, []) { Partial = true },
            new DeskRegion("pouch", DeskRegionKind.Pouch, 0, 0, [Slot("pouch", Wallet)]) { Partial = true }));
        Assert.Null(change.Page.Region("backpack"));
        Assert.Null(change.Page.Region("pockets"));
        Assert.Equal(Wallet, change.Page.Region("pouch")!.Cells.Single().ItemId);
        // A slot seen empty (its frame in view, no label) is no reading in part: it stands, the grid gone with it.
        change = ledger.Apply(GameMode.Pve, Page(1, new DeskRegion("backpack", DeskRegionKind.Backpack, 0, 0, [])));
        Assert.NotNull(change.Page.Region("backpack"));

        ledger.Apply(GameMode.Pve, Page(2, FullBackpack()));
        change = ledger.Apply(GameMode.Pve, Page(3, alone));
        Assert.Equal(3, change.Page.Region("backpack")!.Cells.Count);
        Assert.Equal((5, 6), (change.Page.Region("backpack")!.Cols, change.Page.Region("backpack")!.Rows));
    }

    // Another container in the slot: the page's grid was the other one's, and goes.
    [Fact]
    public void A_slot_read_as_another_container_takes_the_pages_grid_away()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, FullBackpack()));
        var change = ledger.Apply(GameMode.Pve, Page(1, new DeskRegion("backpack", DeskRegionKind.Backpack, 0, 0, [Slot("backpack", Wallet)]) { Partial = true }));
        Assert.Empty(change.Kept);
        Assert.Equal(Wallet, change.Page.Region("backpack")!.Cells.Single().ItemId);
    }

    // The test set's 11-gear.png and 12-gear.png (2026-10-19): the backpack's grid ran out of view at the column's foot, four
    // rows of six in view, all of them empty. The rows in view are read as they show (the water taken out); the page keeps
    // what lies below them, and the grid stands as tall as the page had it.
    [Fact]
    public void A_grid_cut_at_the_columns_foot_keeps_the_pages_rows_below_it()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, FullBackpack()));
        var cut = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 4, [Slot("backpack", Zhuk), Cell(1, 1, 2, 1, Wallet)])
        {
            Parts = [new DeskRect(0, 0, 5, 4)],
            Partial = true,
        };
        var change = ledger.Apply(GameMode.Pve, Page(1, cut));
        Assert.Equal(["backpack"], change.Kept);
        var backpack = change.Page.Region("backpack")!;
        Assert.Equal((5, 6), (backpack.Cols, backpack.Rows));
        Assert.Equal([new DeskRect(0, 0, 5, 6)], backpack.Parts);
        Assert.Equal([Wallet, Salewa], backpack.Cells.Where(c => !c.IsSlot).Select(c => c.ItemId));
        // A first reading of a region cut so is what it shows: nothing to keep.
        var first = new DeskLedger().Apply(GameMode.Pve, Page(0, cut));
        Assert.Empty(first.Kept);
        Assert.Equal((5, 4), (first.Page.Region("backpack")!.Cols, first.Page.Region("backpack")!.Rows));
    }

    // The owner's second test of 2026-10-25, picture 17: the Beta 2 backpack (five by five, read in the backpack's grid in 14,
    // its rows below the Beta 2 window) was taken to the stash; the backpack's four rows in view read empty, and the page
    // kept the Beta 2 in them, as it lay in part below the foot. A page's cell the rows read overlap is what the reading saw
    // there; one wholly below them stays.
    [Fact]
    public void A_pages_cell_the_rows_read_overlap_gives_way_to_them()
    {
        var ledger = new DeskLedger();
        var beta = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 6, [Slot("backpack", Zhuk), Cell(0, 0, 5, 5, Wallet), Cell(2, 5, 1, 1, Salewa)])
        {
            Parts = [new DeskRect(0, 0, 5, 6)],
        };
        ledger.Apply(GameMode.Pve, Page(0, beta));
        var emptied = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 4, [Slot("backpack", Zhuk)])
        {
            Parts = [new DeskRect(0, 0, 5, 4)],
            Partial = true,
        };
        var backpack = ledger.Apply(GameMode.Pve, Page(1, emptied)).Page.Region("backpack")!;
        Assert.Equal([Salewa], backpack.Cells.Where(c => !c.IsSlot).Select(c => c.ItemId));
        Assert.Equal((5, 6), (backpack.Cols, backpack.Rows));
    }

    // ---- the two ways a page's cells stay: a region read in part (Filled) and the cells a window covered ----
    // (code review, 2026-10-25: each case held by a test before the two are made one, or left two.)

    // A region read in part keeps the page's slot where the reading read none, under a window or not (its grid in view, its
    // slot scrolled away); a region read whole keeps it only where a window covered it (the next test).
    [Fact]
    public void A_region_read_in_part_keeps_the_pages_slot_where_none_was_read_under_a_window_or_not()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, FullBackpack()));
        var grid = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 4, [Cell(0, 0, 1, 2, Water)])
        {
            Parts = [new DeskRect(0, 0, 5, 4)],
            Partial = true,
        };
        var change = ledger.Apply(GameMode.Pve, Page(1, grid));
        Assert.Equal(["backpack"], change.Kept);
        var backpack = change.Page.Region("backpack")!;
        Assert.Equal(Zhuk, backpack.Cells.Single(c => c.IsSlot).ItemId);
        Assert.Equal([Water, Salewa], backpack.Cells.Where(c => !c.IsSlot).Select(c => c.ItemId));
    }

    // A region read whole (its frames and slot in view): the page's slot stays where a window covered it, and a slot read
    // with its label unread stands as read, the page's named one gone (read in part, the page's named one stays:
    // A_containers_slot_read_alone_keeps_the_pages_grid). The page's cells nothing covered go.
    [Fact]
    public void A_region_read_whole_keeps_the_pages_slot_only_under_a_window()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, FullBackpack()));
        var covered = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 6, [Cell(0, 0, 1, 2, Water)])
        {
            Parts = [new DeskRect(0, 0, 5, 6)],
            CoveredSlots = ["backpack"],
        };
        var change = ledger.Apply(GameMode.Pve, Page(1, covered));
        Assert.Empty(change.Kept);
        var backpack = change.Page.Region("backpack")!;
        Assert.Equal(Zhuk, backpack.Cells.Single(c => c.IsSlot).ItemId);
        Assert.Equal([Water], backpack.Cells.Where(c => !c.IsSlot).Select(c => c.ItemId));

        var unread = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 6, [Slot("backpack"), Cell(0, 0, 1, 2, Water)])
        {
            Parts = [new DeskRect(0, 0, 5, 6)],
        };
        backpack = ledger.Apply(GameMode.Pve, Page(2, unread)).Page.Region("backpack")!;
        Assert.Empty(backpack.Cells.Single(c => c.IsSlot).Ids);
    }

    // A region read in part whose slot reads another container (owner, 2026-10-26; until then the page's cells a window
    // covered stayed): the page's grid was the other one's, so nothing of it is kept, covered or not (none kept, the size
    // as read); the new grid's cells under the window stay unread until a reading shows them.
    [Fact]
    public void A_slot_read_as_another_container_keeps_none_of_the_pages_cells_covered_or_not()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, FullBackpack()));
        var other = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 4, [Slot("backpack", Wallet)])
        {
            Parts = [new DeskRect(0, 0, 5, 4)],
            Covered = [new DeskRect(0, 0, 1, 1)],
            Partial = true,
        };
        var change = ledger.Apply(GameMode.Pve, Page(1, other));
        Assert.Empty(change.Kept);
        var backpack = change.Page.Region("backpack")!;
        Assert.Equal([Wallet], backpack.Cells.Select(c => c.ItemId));
        Assert.Equal((5, 4), (backpack.Cols, backpack.Rows));
        Assert.Equal([new DeskRect(0, 0, 5, 4)], backpack.Parts);
    }

    // The same, read whole (its slot and grid in view, a window over its top left; owner, 2026-10-26): the page's cells under
    // the window and a "?" cell's pick at a place the new grid reads were the other container's, and go. A slot whose label
    // wasn't read says nothing of the container: the page's cells under the window stay, its pick with its cell.
    [Fact]
    public void A_region_read_whole_whose_slot_reads_another_container_keeps_none_of_the_pages_cells_or_picks()
    {
        DeskLedger Picked()
        {
            var ledger = new DeskLedger();
            ledger.Apply(GameMode.Pve, Page(0, new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 6,
                [Slot("backpack", Zhuk), Cell(0, 0, 1, 2, Water), Cell(2, 5, 1, 1, Water, Salewa)])
            {
                Parts = [new DeskRect(0, 0, 5, 6)],
            }));
            Assert.True(ledger.Pick(GameMode.Pve, "backpack", null, 2, 5, Salewa));
            return ledger;
        }
        DeskRegion Reading(DeskCell slot) => new("backpack", DeskRegionKind.Backpack, 5, 6, [slot, Cell(2, 5, 1, 1, Water, Salewa)])
        {
            Parts = [new DeskRect(0, 0, 5, 6)],
            Covered = [new DeskRect(0, 0, 1, 2)],
        };

        var backpack = Picked().Apply(GameMode.Pve, Page(1, Reading(Slot("backpack", Wallet)))).Page.Region("backpack")!;
        Assert.Equal(Wallet, backpack.Cells.Single(c => c.IsSlot).ItemId);
        Assert.Null(Assert.Single(backpack.Cells, c => !c.IsSlot).Pick);

        backpack = Picked().Apply(GameMode.Pve, Page(1, Reading(Slot("backpack")))).Page.Region("backpack")!;
        Assert.Equal([Water, Salewa], backpack.Cells.Where(c => !c.IsSlot).Select(c => c.ItemId));
    }

    // Step 1c (DeskIcons) on both paths: a "?" cell read again settled by its picture takes no earlier pick, and that pick
    // is gone for good (read as "one of N" once more, it has none); a page's "?" cell kept, below the column's foot or
    // under a window, keeps its own pick.
    [Fact]
    public void A_kept_cell_keeps_its_pick_and_a_settled_one_takes_none()
    {
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 6,
            [Slot("backpack", Zhuk), Cell(0, 0, 1, 1, Water, Salewa), Cell(2, 5, 1, 1, Water, Salewa)])
        {
            Parts = [new DeskRect(0, 0, 5, 6)],
        }));
        Assert.True(ledger.Pick(GameMode.Pve, "backpack", null, 0, 0, Salewa));
        Assert.True(ledger.Pick(GameMode.Pve, "backpack", null, 2, 5, Water));
        // Cut at the column's foot, the cell in view settled as the other item.
        var cut = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 4, [Slot("backpack", Zhuk), Cell(0, 0, 1, 1, Water)])
        {
            Parts = [new DeskRect(0, 0, 5, 4)],
            Partial = true,
        };
        var cells = ledger.Apply(GameMode.Pve, Page(1, cut)).Page.Region("backpack")!.Cells;
        var settled = cells.Single(c => !c.IsSlot && c.J == 0);
        Assert.Equal([Water], settled.Ids);
        Assert.Null(settled.Pick);
        Assert.Equal(Water, cells.Single(c => !c.IsSlot && c.J == 5).Pick);
        // Read whole, the cell below under a window and the one above "one of N" again.
        var whole = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 6, [Slot("backpack", Zhuk), Cell(0, 0, 1, 1, Water, Salewa)])
        {
            Parts = [new DeskRect(0, 0, 5, 6)],
            Covered = [new DeskRect(2, 5, 1, 1)],
        };
        cells = ledger.Apply(GameMode.Pve, Page(2, whole)).Page.Region("backpack")!.Cells;
        Assert.Null(cells.Single(c => !c.IsSlot && c.J == 0).Pick);
        Assert.Equal(Water, cells.Single(c => !c.IsSlot && c.J == 5).Pick);
    }

    // A page's cell the compartments read overlap, under a window there: read in part, it stays (beside
    // A_pages_cell_the_rows_read_overlap_gives_way_to_them); a cell read over it takes its place, read in part or whole.
    [Fact]
    public void A_pages_cell_the_rows_read_overlap_stays_under_a_window_unless_a_cell_is_read_over_it()
    {
        var ledger = new DeskLedger();
        var beta = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 6, [Slot("backpack", Zhuk), Cell(0, 0, 5, 5, Wallet), Cell(2, 5, 1, 1, Salewa)])
        {
            Parts = [new DeskRect(0, 0, 5, 6)],
        };
        ledger.Apply(GameMode.Pve, Page(0, beta));
        var under = new DeskRegion("backpack", DeskRegionKind.Backpack, 5, 4, [Slot("backpack", Zhuk)])
        {
            Parts = [new DeskRect(0, 0, 5, 4)],
            Covered = [new DeskRect(0, 0, 2, 2)],
            Partial = true,
        };
        var backpack = ledger.Apply(GameMode.Pve, Page(1, under)).Page.Region("backpack")!;
        Assert.Equal([Wallet, Salewa], backpack.Cells.Where(c => !c.IsSlot).Select(c => c.ItemId));

        var over = under with { Cells = [Slot("backpack", Zhuk), Cell(3, 3, 1, 1, Water)] };
        backpack = ledger.Apply(GameMode.Pve, Page(2, over)).Page.Region("backpack")!;
        Assert.Equal([Water, Salewa], backpack.Cells.Where(c => !c.IsSlot).Select(c => c.ItemId));

        ledger.Apply(GameMode.Pve, Page(3, beta));
        var whole = beta with { Cells = [Slot("backpack", Zhuk), Cell(3, 3, 1, 1, Water)], Covered = [new DeskRect(0, 0, 2, 2), new DeskRect(2, 5, 1, 1)] };
        backpack = ledger.Apply(GameMode.Pve, Page(4, whole)).Page.Region("backpack")!;
        Assert.Equal([Water, Salewa], backpack.Cells.Where(c => !c.IsSlot).Select(c => c.ItemId));
    }

    // ---- what leaves the page (owner, 2026-10-25) ----

    // The owner's page of 2026-10-19 kept four windows made of misread headings and the POUCH's grid, which no reading
    // would ever take away. A window attached to nothing that three readings in a row don't show leaves the page; one
    // attached to its container stays however long it goes unseen.
    [Fact]
    public void A_window_attached_to_nothing_leaves_the_page_after_three_readings_that_dont_show_it()
    {
        var ledger = new DeskLedger();
        var change = ledger.Apply(GameMode.Pve, Page(0,
            Stash(Cell(4, 4, 2, 2, Wallet) with { Holds = DeskHolds.Contents }),
            Window("Keys", [Wallet], Cell(0, 0, 1, 1, Water)),
            Window("Ace cover", [], Cell(0, 0, 1, 1, Salewa))));
        Assert.Null(change.Page.Region("window:acecover")!.AttachedTo);
        for (var n = 1; n <= 2; n++)
        {
            change = ledger.Apply(GameMode.Pve, Page(n, Stash(Cell(4, 4, 2, 2, Wallet) with { Holds = DeskHolds.Contents })));
            Assert.Equal(n, change.Page.Region("window:acecover")!.Missed);
            Assert.Empty(change.Dropped);
        }
        change = ledger.Apply(GameMode.Pve, Page(3, Stash(Cell(4, 4, 2, 2, Wallet) with { Holds = DeskHolds.Contents })));
        Assert.Equal(["window:acecover"], change.Dropped);
        Assert.Null(change.Page.Region("window:acecover"));
        Assert.Equal(3, change.Page.Region("window:keys")!.Missed);
        Assert.NotNull(change.Page.Region("window:keys")!.AttachedTo);
        // A reading that shows a window counts from naught again.
        change = ledger.Apply(GameMode.Pve, Page(4, Window("Keys", [Wallet], Cell(0, 0, 1, 1, Water))));
        Assert.Equal(0, change.Page.Region("window:keys")!.Missed);
    }

    // A window of no cell and no title is no reading of anything: never on the page, read or loaded.
    [Fact]
    public void A_window_with_no_cell_and_no_title_is_never_kept()
    {
        var ledger = new DeskLedger();
        var nameless = new DeskRegion(DeskRegion.WindowKey(""), DeskRegionKind.ContainerWindow, 1, 2, []);
        var change = ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(0, 0, 1, 1, Water)), nameless));
        Assert.Null(change.Page.Region("window:"));
        Assert.DoesNotContain("window:", change.Changed);

        var path = Path.Combine(_root, "desk.json");
        File.WriteAllText(path, """
            { "version": 1, "modes": { "pve": { "readAt": "2026-01-01T15:40:00+01:00", "regions": [
              { "key": "stash", "kind": "stash", "cols": 10, "rows": 12, "cells": [ { "i": 0, "j": 0, "w": 1, "h": 1, "ids": [ "000000000000000000000203" ] } ], "readAt": "2026-01-01T15:40:00+01:00" },
              { "key": "window:", "kind": "window", "cols": 1, "rows": 2, "cells": [], "readAt": "2026-01-01T15:40:00+01:00" } ] } } }
            """);
        Assert.Null(DeskLedger.Load(path).Page(GameMode.Pve)!.Region("window:"));
    }

    // The owner's second test of 2026-10-25, picture 14: a pistol's rounds "0/17" in the stash, read "Ø/17", made a window of one
    // cell of nothing. A window with no cell read whose title names no container of the catalogue is never kept, read or
    // loaded; one whose title names a container stays (its window may lie under another).
    [Fact]
    public void A_window_with_no_cell_read_whose_title_names_nothing_is_never_kept()
    {
        var ledger = new DeskLedger();
        var rounds = Window("Ø/17", [], Cell(0, 0, 1, 2));
        var covered = Window("Keys", [Wallet]);
        var change = ledger.Apply(GameMode.Pve, Page(0, Stash(Cell(4, 4, 2, 2, Wallet) with { Holds = DeskHolds.Contents }), rounds, covered));
        Assert.Null(change.Page.Region(rounds.Key));
        Assert.NotNull(change.Page.Region("window:keys"));

        var path = Path.Combine(_root, "desk.json");
        File.WriteAllText(path, """
            { "version": 1, "modes": { "pve": { "readAt": "2026-01-01T15:40:00+01:00", "regions": [
              { "key": "stash", "kind": "stash", "cols": 10, "rows": 12, "cells": [ { "i": 0, "j": 0, "w": 1, "h": 1, "ids": [ "000000000000000000000203" ] } ], "readAt": "2026-01-01T15:40:00+01:00" },
              { "key": "window:ø17", "kind": "window", "cols": 1, "rows": 2, "cells": [ { "i": 0, "j": 0, "w": 1, "h": 2, "ids": [] } ], "title": "Ø/17", "readAt": "2026-01-01T15:40:00+01:00" } ] } } }
            """);
        Assert.Null(DeskLedger.Load(path).Page(GameMode.Pve)!.Region("window:ø17"));
    }

    // Each region keeps when the screenshot that last showed it was taken (for its head, "BACKPACK · 15:40") and how many
    // readings have missed it since; a file written before 2026-10-25 has neither: its regions take the page's time, and
    // its windows attached to nothing, unseen for how long it can't say, don't stay.
    [Fact]
    public void Each_region_keeps_its_reading_time_and_a_file_without_one_drops_its_windows_attached_to_nothing()
    {
        var path = Path.Combine(_root, "desk.json");
        var ledger = new DeskLedger();
        ledger.Apply(GameMode.Pve, Page(0, FullBackpack(), Stash(Cell(0, 0, 1, 1, Water))));
        ledger.Apply(GameMode.Pve, Page(5, Stash(Cell(0, 0, 1, 1, Salewa))));
        var page = ledger.Page(GameMode.Pve)!;
        Assert.Equal(At, page.Region("backpack")!.ReadAt);
        Assert.Equal(At.AddMinutes(5), page.Region("stash")!.ReadAt);
        Assert.Equal(1, page.Region("backpack")!.Missed);
        ledger.Save(path);
        var text = File.ReadAllText(path);
        Assert.Contains("\"readAt\": \"2026-01-01T15:40:00+01:00\"", text);
        Assert.Contains("\"missed\": 1", text);
        var loaded = DeskLedger.Load(path).Page(GameMode.Pve)!;
        Assert.Equal((At, 1), (loaded.Region("backpack")!.ReadAt, loaded.Region("backpack")!.Missed));
        Assert.Equal(At.AddMinutes(5), loaded.Region("stash")!.ReadAt);

        // The owner's file of 2026-10-19, in short: the Keys window attached to its case in the stash, a Junk window
        // attached to its box, "*ACE COVER" and a window of no name attached to nothing.
        File.WriteAllText(path, """
            { "version": 1, "modes": { "pve": { "readAt": "2026-01-01T15:40:00+01:00", "regions": [
              { "key": "stash", "kind": "stash", "cols": 10, "rows": 12, "cells": [
                { "i": 7, "j": 9, "w": 2, "h": 1, "ids": [ "000000000000000000000204" ], "holds": "contents" },
                { "i": 4, "j": 3, "w": 2, "h": 2, "ids": [ "000000000000000000000201" ], "holds": "contents" } ] },
              { "key": "window:keys", "kind": "window", "cols": 11, "rows": 7, "cells": [ { "i": 0, "j": 0, "w": 1, "h": 1, "ids": [ "000000000000000000000203" ] } ],
                "title": "Keys", "titleIds": [ "000000000000000000000204" ], "attachedTo": { "region": "stash", "i": 7, "j": 9 } },
              { "key": "window:junk", "kind": "window", "cols": 22, "rows": 14, "cells": [ { "i": 0, "j": 0, "w": 1, "h": 1, "ids": [ "000000000000000000000205" ] } ],
                "title": "Junk", "titleIds": [ "000000000000000000000201" ], "attachedTo": { "region": "stash", "i": 4, "j": 3 } },
              { "key": "window:acecover", "kind": "window", "cols": 2, "rows": 2, "cells": [ { "i": 0, "j": 0, "w": 1, "h": 1, "ids": [ "000000000000000000000205" ] } ],
                "title": "*ACE COVER" },
              { "key": "window:", "kind": "window", "cols": 3, "rows": 3, "cells": [ { "i": 0, "j": 0, "w": 1, "h": 1, "ids": [ "000000000000000000000203" ] } ] } ] } } }
            """);
        var old = DeskLedger.Load(path).Page(GameMode.Pve)!;
        Assert.Equal(["stash", "window:junk", "window:keys"], old.Regions.Select(r => r.Key).Order());
        Assert.All(old.Regions, r => Assert.Equal(At, r.ReadAt));
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
