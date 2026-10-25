using Shturmap.Game.Screenshots;

namespace Shturmap.Game.Tests;

// The operational tasks' table read from a menu screenshot's words (owner, 2026-10-19: from a panel of four, B;
// docs/DESIGN.md §2, "Menu screenshots"; TaskTable, TasksReader; `shturmap-cli tasks` measures the same reader), and a
// trader's task view (owner, 2026-10-25). The first tests lay out words by hand where the game's table and a trader's
// tab put them at 2560×1440, with made-up task names and objectives, so they need no picture and no text recognition.
// The last two read the owner's real screenshots against hand-written truths; those stay on the PC that took them
// (tests\fixtures\ocr\operational, ignored by git) and they are skipped elsewhere.
public sealed class TaskTableTests
{
    private const int Width = 2560;
    private const int Height = 1440;

    private static TaskWord W(string text, double x, double y, double w = 0, double h = 18) =>
        new(text, x, y, w > 0 ? w : text.Length * 11, h);

    // The header as the game draws it at 1440p.
    private static IEnumerable<TaskWord> Header() =>
    [
        W("Trader", 87, 155, 68, 17), W("Type", 210, 155, 46, 20), W("Class", 282, 155, 50, 17), W("Task", 675, 155, 45, 17),
        W("Location", 1083, 155, 84, 17), W("Status", 1280, 155, 66, 17), W("Progress", 1514, 155, 89, 20),
    ];

    // A row at the row's middle y: the name at the names' left edge, the location centred in its cell, the status
    // above the middle (the countdown below it is left to the bands, as the recognition drops it on the page).
    private static IEnumerable<TaskWord> Row(double cy, string[] name, string location, string status = "activel")
    {
        var x = 364.0;
        foreach (var part in name)
        {
            yield return W(part, x, cy - 9);
            x += part.Length * 11 + 9;
        }
        yield return W(location, 1125 - location.Length * 5.5, cy - 9);
        yield return W(status, 1284, cy - 30, 73, 18);
    }

    // A sentence from x on, one word every few pixels: an expanded row's text, which runs across the columns.
    private static IEnumerable<TaskWord> Sentence(double x, double y, string text)
    {
        foreach (var part in text.Split(' '))
        {
            yield return W(part, x, y);
            x += part.Length * 11 + 9;
        }
    }

    // Three rows, the second expanded: its text across the columns, "Objective(s)" with one objective and its count
    // (the slashed zero read as ø, the icon read as @), "Rewards" with a reward's name in the Task column.
    private static List<TaskWord> ThreeRowsSecondExpanded() =>
    [
        .. Header(),
        .. Row(254, ["Zorblat", "and", "transfer"], "Any location"),
        .. Row(391, ["Quenvik"], "Lighthouse"),
        .. Sentence(518, 475, "Here you are, friend: some made up words that run across all the columns of the table and on"),
        .. Sentence(518, 499, "help me out with the thing, need to pop some made up morons now"),
        W("Objective(s)", 101, 735, 126, 23),
        W("@", 287, 782, 30, 30),
        .. Sentence(335, 788, "Eliminate Zorbs with a pinch from a distance of more than 5øm"),
        W("ø/3", 1434, 788, 42, 19),
        W("Rewards", 101, 855, 95, 18),
        W("EXPERIENCE", 315, 919, 90, 12),
        .. Sentence(648, 929, "Roubles (72 730)"),
        .. Sentence(982, 918, "Pack of made up dried"),
        .. Row(1273, ["Pindrake", "the", "location"], "Shoreline"),
    ];

    [Fact]
    public void The_table_is_read_row_by_row_and_an_expanded_rows_text_and_rewards_make_no_row()
    {
        var page = TaskTable.Read(ThreeRowsSecondExpanded(), Width, Height);
        Assert.Equal(TaskScreen.Table, page.Screen);
        Assert.Equal(["Zorblat and transfer", "Quenvik", "Pindrake the location"], page.Rows.Select(r => r.Name));
        Assert.Equal(["Any location", "Lighthouse", "Shoreline"], page.Rows.Select(r => r.Location));
        Assert.All(page.Rows, r => Assert.Equal("active", r.Status));
    }

    [Fact]
    public void An_expanded_rows_objectives_go_to_it_with_their_counts()
    {
        var page = TaskTable.Read(ThreeRowsSecondExpanded(), Width, Height);
        Assert.Empty(page.Rows[0].Objectives);
        var objective = Assert.Single(page.Rows[1].Objectives);
        Assert.Equal("Eliminate Zorbs with a pinch from a distance of more than 50m", objective.Text);
        Assert.Equal((0, 3), (objective.Done, objective.Of));
        Assert.Empty(page.Rows[2].Objectives);
    }

    [Fact]
    public void Objectives_without_a_count_are_kept_one_per_line()
    {
        List<TaskWord> words =
        [
            .. Header(),
            .. Row(254, ["Zorblat"], "Any location"),
            W("Objective(s)", 101, 600, 126, 23),
            .. Sentence(634, 650, "Hand over the found in raid item: \"Quill drive\""),
            .. Sentence(644, 724, "Hand over the found in raid item: “Tessaril fan”"),
            W("Rewards", 101, 790, 95, 18),
            .. Row(1100, ["Quenvik"], "Lighthouse"),
        ];
        var page = TaskTable.Read(words, Width, Height);
        Assert.Equal(2, page.Rows.Count);
        Assert.Equal(["Hand over the found in raid item: \"Quill drive\"", "Hand over the found in raid item: \"Tessaril fan\""],
            page.Rows[0].Objectives.Select(o => o.Text));
        Assert.All(page.Rows[0].Objectives, o => Assert.Null(o.Of));
        Assert.Empty(page.Rows[1].Objectives);
    }

    [Fact]
    public void The_bands_lie_below_the_status_and_over_the_progress_cell()
    {
        var row = TaskTable.Read(ThreeRowsSecondExpanded(), Width, Height).Rows[0];
        // The countdown stands at 1285..1354 × 265..288 at 1440p, the percentage at 1532..1576 × 220..248.
        Assert.InRange(row.TimeBand.X, 1180, 1285);
        Assert.InRange(row.TimeBand.X + row.TimeBand.W, 1354, 1440);
        Assert.InRange(row.TimeBand.Y, 240, 265);
        Assert.InRange(row.TimeBand.Y + row.TimeBand.H, 288, 310);
        Assert.InRange(row.ProgressBand.X, 1380, 1532);
        Assert.InRange(row.ProgressBand.X + row.ProgressBand.W, 1576, (int)(Width * TaskTable.LeftPart) + 1);
        Assert.InRange(row.ProgressBand.Y, 200, 220);
        Assert.InRange(row.ProgressBand.Y + row.ProgressBand.H, 248, 270);
    }

    [Fact]
    public void A_countdown_on_the_page_is_taken_and_a_table_without_one_is_not_the_operational_tab()
    {
        List<TaskWord> words = [.. Header(), .. Row(254, ["Zorblat"], "Lighthouse"), W("1ø:14:11", 1285, 266, 69, 20), W("57%", 1525, 222, 46, 24)];
        var page = TaskTable.Read(words, Width, Height);
        var row = Assert.Single(page.Rows);
        Assert.Equal(new TimeSpan(10, 14, 11), row.TimeLeft);
        Assert.Equal(57, row.Progress);
        Assert.Equal(TaskScreen.Table, TaskTable.Settle(page));

        var story = TaskTable.Read([.. Header(), .. Row(254, ["Zorblat"], "Lighthouse")], Width, Height);
        Assert.Single(story.Rows);
        Assert.Equal(TaskScreen.OtherTable, TaskTable.Settle(story));
    }

    // A trader's tab whose list has no operational entry: told, and nothing is read from it (its objectives and rewards
    // make no row).
    [Fact]
    public void A_traders_tasks_screen_without_operational_entries_is_told_and_nothing_is_read_from_it()
    {
        List<TaskWord> words =
        [
            W("TRADING", 78, 35), W("TASKS", 252, 35), W("SERVICES", 427, 35), W("COMPLETE", 116, 335), W("REPLACE", 499, 335),
            W("Zorblat", 826, 335), W("Shoreline", 2210, 335), W("Objective(s)", 776, 777), .. Sentence(1383, 840, "Survive on the location"),
            W("4/7", 1940, 840), W("OPERATIONAL", 70, 1265), W("TASKS", 230, 1265),
        ];
        var page = TaskTable.Read(words, Width, Height);
        Assert.Equal(TaskScreen.TraderScreen, page.Screen);
        Assert.Empty(page.Rows);
        Assert.Null(page.Selected);
    }

    // ---- a trader's TASKS tab (owner, 2026-10-25) ----

    // A trader's tab as the game draws it at 1440p (measured 2026-10-25), the picture's left part only, as the reader
    // gives it: the tabs and the traders' row, COMPLETE | REPLACE and the header's glyph and task name on one line, the
    // quest's text and objective cut at the left part's edge, the list's sections (a story quest under ESSENTIAL TASKS),
    // OPERATIONAL TASKS with its glyph, each entry a glyph, a name and (where the page carries it) a countdown at the
    // list's right end, and the game's version line under the list.
    private static List<TaskWord> TraderTab(string[] header, params (string Name, string[] Countdown)[] entries)
    {
        List<TaskWord> words =
        [
            W("TRADING", 78, 35), W("TASKS", 253, 37, 63, 16), W("SERVICES", 428, 36, 94, 16), W("Zorbov", 61, 201), W("225", 77, 242),
            W("O", 781, 336, 27, 27), W("COMPLETE", 118, 338, 156, 23), W("REPLACE", 500, 338, 130, 24),
            .. Sentence(1386, 418, "Here you are friend some made up"),
            W("Objective(s)", 777, 778, 126, 22), W("Survive", 1544, 841, 87, 19), W("on", 1641, 845, 26, 15), W("th", 1677, 841, 25, 19),
            W("Rewards", 777, 915, 95, 18),
            W("=", 28, 950, 29, 15), W("ESSENTIAL", 72, 954, 127, 18), W("TASKS", 208, 954, 73, 18),
            W("&", 74, 1025, 31, 30), .. Sentence(118, 1031, "Zorbs on Parade"),
            W("O", 26, 1113, 33, 33), W("OPERATIONAL", 71, 1121, 165, 18), W("TASKS", 246, 1121, 72, 18),
            W("1.2.5.0.12345", 16, 1373, 138, 16), W("PvE", 289, 1373, 38, 16),
        ];
        var x = 828.0;
        foreach (var part in header)
        {
            words.Add(W(part, x, 338, part.Length * 14, 24));
            x += part.Length * 14 + 11;
        }
        var y = 1198.0;
        foreach (var (name, countdown) in entries)
        {
            words.Add(W("a", 27, y - 7, 31, 31));
            words.AddRange(Sentence(72, y, name));
            var cx = 716.0 - countdown.Sum(c => c.Length * 11 + 9) + 9;
            foreach (var part in countdown)
            {
                words.Add(W(part, cx, y));
                cx += part.Length * 11 + 9;
            }
            y += 92;
        }
        return words;
    }

    [Fact]
    public void A_traders_tab_reads_the_header_task_when_the_list_has_it_with_its_countdown()
    {
        var page = TaskTable.Read(TraderTab(["Zorblat", "the", "location"], ("Quenvik", []), ("Zorblat the location", ["6", "day(s)", "23:07:57"])), Width, Height);
        Assert.Equal(TaskScreen.TraderScreen, page.Screen);
        // The list's two operational entries, nothing else of the page: not the story quest above, nor the version line.
        Assert.Equal(["Quenvik", "Zorblat the location"], page.Rows.Select(r => r.Name));
        // The header's task is the entry of its name, whose countdown the page carried; the other entry is no task.
        Assert.Same(page.Rows[1], page.Selected);
        Assert.Equal(new TimeSpan(6, 23, 7, 57), page.Selected!.TimeLeft);
        Assert.Null(page.Rows[0].TimeLeft);
        Assert.Empty(page.Selected.Objectives);
        // Its location and status are in the header's band, the picture's right third from the left part on.
        Assert.Equal((int)Math.Round(Width * TaskTable.LeftPart), page.HeaderBand.X);
        Assert.Equal(Width, page.HeaderBand.X + page.HeaderBand.W);
        Assert.InRange(page.HeaderBand.Y, 320, 338);
        Assert.InRange(page.HeaderBand.Y + page.HeaderBand.H, 362, 380);
    }

    [Fact]
    public void A_list_entrys_countdown_band_lies_at_the_lists_right_end_clear_of_the_version_line()
    {
        var page = TaskTable.Read(TraderTab(["Pindrake"], ("Quenvik", []), ("Pindrake", [])), Width, Height);
        Assert.Equal(2, page.Rows.Count);
        // The second entry at 1290..1308: its countdown (up to "6 day(s) 23:07:57" from 531 to 716), or one a hand-over's
        // icon pushes a unit lower, lies in its band; the version line's "PvE" (289..327) and the next panel (755) don't.
        var band = page.Rows[1].TimeBand;
        Assert.InRange(band.X, 330, 531);
        Assert.InRange(band.X + band.W, 716, 754);
        Assert.InRange(band.Y, 1280, 1290);
        Assert.InRange(band.Y + band.H, 1328, 1340);
    }

    [Fact]
    public void A_story_quests_tab_gives_no_task_but_its_lists_entries()
    {
        // The header names a quest the list's OPERATIONAL TASKS don't have: no task, and no band of the right third.
        var page = TaskTable.Read(TraderTab(["Zorbs", "on", "Parade"], ("Quenvik", ["00:48:48"]), ("Pindrake", [])), Width, Height);
        Assert.Equal(TaskScreen.TraderScreen, page.Screen);
        Assert.Null(page.Selected);
        Assert.Equal(default, page.HeaderBand);
        Assert.Equal(["Quenvik", "Pindrake"], page.Rows.Select(r => r.Name));
        Assert.Equal(new TimeSpan(0, 48, 48), page.Rows[0].TimeLeft);
    }

    [Fact]
    public void Two_entries_of_the_headers_name_give_the_task_without_a_countdown()
    {
        // Which of the two is selected can't be told: the task, but no entry's countdown.
        var page = TaskTable.Read(TraderTab(["Quenvik"], ("Quenvik", ["08:33:03"]), ("Quenvik", ["6", "day(s)", "23:07:57"])), Width, Height);
        Assert.NotNull(page.Selected);
        Assert.DoesNotContain(page.Selected, page.Rows);
        Assert.Equal("Quenvik", page.Selected!.Name);
        Assert.Null(page.Selected.TimeLeft);
        Assert.Equal(2, page.Rows.Count);
    }

    [Fact]
    public void The_header_band_gives_the_location_and_the_status_word()
    {
        TaskRow Header(params TaskWord[] words)
        {
            var row = new TaskRow { Name = "Zorblat" };
            TaskTable.TakeHeader(row, words, 17.5);
            return row;
        }
        // "Streets of Tarkov", far apart from "active!", and the menu's glyph at the line's end.
        var streets = Header(W("Streets", 2116, 340, 80, 22), W("of", 2205, 340, 22, 22), W("Tarkov", 2236, 340, 78, 22), W("activel", 2394, 340, 74, 22),
            W("E", 2492, 342, 26, 20));
        Assert.Equal(("Streets of Tarkov", "active"), (streets.Location, streets.Status));
        // A task before ACCEPT: another word there, or none (owner, 2026-10-25: it counts all the same).
        Assert.Equal(("Shoreline", null), (Header(W("Shoreline", 2212, 340, 102, 22)).Location, Header(W("Shoreline", 2212, 340, 102, 22)).Status));
        var any = Header(W("Any", 2177, 340, 40, 22), W("location", 2226, 340, 88, 22), W("Zorbing", 2394, 340, 90, 22));
        Assert.Equal(("Any location", "zorbing"), (any.Location, any.Status));
        Assert.Equal((null, "active"), (Header(W("active!", 2394, 340, 74, 22)).Location, Header(W("active!", 2394, 340, 74, 22)).Status));
        Assert.Equal((null, null), (Header().Location, Header().Status));
    }

    [Theory]
    [InlineData("6 day(s) 23:07:57", 6, 23, 7, 57)]
    [InlineData("6 day(s)23:07:57", 6, 23, 7, 57)]
    [InlineData("6 days 23:07:57", 6, 23, 7, 57)]
    [InlineData("6 day(5) 23:Ø7:57 >", 6, 23, 7, 57)]
    [InlineData("1 day(s) 00:00:05", 1, 0, 0, 5)]
    [InlineData("23:54:15 >", 0, 23, 54, 15)]
    public void A_countdown_with_days_reads_as_the_game_writes_it(string text, int d, int h, int m, int s) =>
        Assert.Equal(new TimeSpan(d, h, m, s), TaskTable.TimeLeft(text));

    [Theory]
    [InlineData("6 day(s)")]
    [InlineData("6 day(s) >")]
    [InlineData("6 day(s) 123:07:57")]
    [InlineData("67 day(s) 23:07:57")]
    public void Days_without_a_time_of_day_are_no_countdown(string text) => Assert.Null(TaskTable.TimeLeft(text));

    [Theory]
    [InlineData("6 day(s)", 6)]
    [InlineData("6 day(s) >", 6)]
    [InlineData("S day(s) 2307:57", 5)]
    [InlineData("23:07:57", null)]
    [InlineData("6 aaycsj", null)]
    public void The_days_of_a_countdown_are_read_on_their_own(string text, int? days) => Assert.Equal(days, TaskTable.DaysOf(text));

    // A word of a trader's screen alone tells nothing: a stash page with "Replace" on it stays the stash reader's.
    [Fact]
    public void One_word_of_a_traders_screen_is_not_one()
    {
        Assert.Equal(TaskScreen.None, TaskTable.Read([W("REPLACE", 499, 335), W("STASH", 1900, 35)], Width, Height).Screen);
        Assert.Equal(TaskScreen.None, TaskTable.Read([W("TRADING", 78, 35), W("Zorblat", 826, 335)], Width, Height).Screen);
        Assert.Equal(TaskScreen.TraderScreen, TaskTable.Read([W("TASKS", 252, 35), W("REPLACE", 499, 335)], Width, Height).Screen);
    }

    [Fact]
    public void Another_screen_is_nothing()
    {
        var page = TaskTable.Read([W("OVERALL", 82, 27), W("GEAR", 321, 27), W("Task", 675, 155), W("Location", 1083, 155)], Width, Height);
        Assert.Equal(TaskScreen.None, page.Screen);
        Assert.Empty(page.Rows);
    }

    [Theory]
    [InlineData("10:14:11", 10, 14, 11)]
    [InlineData("1Ø:14:11", 10, 14, 11)]
    [InlineData("0833.19", 8, 33, 19)]
    [InlineData("083317", 8, 33, 17)]
    [InlineData("1014:øg", 10, 14, 9)]
    [InlineData("10 :14: 07", 10, 14, 7)]
    [InlineData("167:59:59", 167, 59, 59)]
    public void A_countdown_reads_through_the_recognitions_lookalikes(string text, int h, int m, int s) =>
        Assert.Equal(new TimeSpan(h, m, s), TaskTable.TimeLeft(text));

    [Theory]
    [InlineData("")]
    [InlineData("active!")]
    [InlineData("10:74:11")]
    [InlineData("1014")]
    [InlineData("Lighthouse 10:14:11")]
    public void Not_a_countdown(string text) => Assert.Null(TaskTable.TimeLeft(text));

    [Theory]
    [InlineData("57%", 57)]
    [InlineData("57 %", 57)]
    [InlineData("ø%", 0)]
    [InlineData("100%", 100)]
    [InlineData("57", 57)]
    public void A_progress_reads(string text, int percent) => Assert.Equal(percent, TaskTable.Percent(text));

    [Theory]
    [InlineData("")]
    [InlineData("101%")]
    [InlineData("570/0")]
    [InlineData("active")]
    public void Not_a_progress(string text) => Assert.Null(TaskTable.Percent(text));

    [Theory]
    [InlineData("activel", "active")]
    [InlineData("active!", "active")]
    [InlineData("Active1", "active")]
    public void The_status_word_loses_the_recognitions_reading_of_the_mark(string text, string status) =>
        Assert.Equal(status, TaskTable.Status(text));

    [Fact]
    public void Words_join_into_runs_by_line_and_gap()
    {
        List<TaskWord> words = [W("Any", 1052, 245, 41, 22), W("location", 1102, 245, 87, 18), W("activel", 1284, 224, 73, 18), W("Zorblat", 364, 245, 77, 18)];
        var chains = TaskTable.Chains(words, 17.5);
        Assert.Equal(["Zorblat", "Any location", "activel"], chains.OrderBy(c => c.X).Select(c => c.Text));
    }

    // ---- real screenshots, where a PC has them ----

    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "fixtures", "ocr", "operational");

    // The truths in a folder with their pictures (TaskTruth), in name order; none where the folder isn't on this PC.
    private static List<(string Picture, string Truth)> Truths(string folder) => Directory.Exists(folder)
        ? Directory.GetFiles(folder, "truth-*.txt").Order(StringComparer.Ordinal)
            .Select(t => (Picture: Path.Combine(folder, Path.GetFileNameWithoutExtension(t)["truth-".Length..] + ".png"), Truth: t))
            .Where(t => File.Exists(t.Picture)).ToList()
        : [];

    private static async Task<(TaskPage Page, TaskTruth Truth)> ReadAsync(TasksReader ocr, string picturePath, string truthPath)
    {
        using var picture = StashPicture.Decode(await File.ReadAllBytesAsync(picturePath, TestContext.Current.CancellationToken));
        Assert.NotNull(picture);
        var page = await ocr.ReadPageAsync(picture, ct: TestContext.Current.CancellationToken);
        var screen = page.Screen switch { TaskScreen.Table => "table", TaskScreen.TraderScreen => "trader", TaskScreen.OtherTable => "other table", _ => "none" };
        var truth = TaskTruth.Load(truthPath);
        Assert.True(screen == truth.Screen, $"{Path.GetFileName(truthPath)}: {screen}");
        return (page, truth);
    }

    // A trader's screen against its truth (owner, 2026-10-25): the header's task, its location and status exactly (or
    // none for a story quest's tab); the list's entries by name in order; every countdown read right. Counts the list's
    // countdowns and how many were read. The messages name the file, never what the picture says.
    private static (int Countdowns, int Read) CheckTrader(TaskPage page, TaskTruth truth, string file)
    {
        if (truth.Task is { } task)
        {
            Assert.True(page.Selected is not null, $"{file}: no task read from the header");
            Assert.True(TaskTruth.Same(page.Selected!.Name, task.Name), $"{file}: the task's name");
            Assert.True(page.Selected.Location == task.Location, $"{file}: the task's location");
            Assert.True(page.Selected.Status == TaskTable.Status(task.Status), $"{file}: the task's status");
            Assert.Empty(page.Selected.Objectives);
            // Its own countdown, white on the selected entry, is read (2026-10-25: four of four).
            Assert.True(page.Selected.TimeLeft is not null || truth.Entries.Count(e => TaskTruth.Same(e.Name, task.Name)) != 1, $"{file}: the task's countdown unread");
        }
        else
        {
            Assert.True(page.Selected is null, $"{file}: a task read from a header that names none of the list's");
        }
        Assert.True(truth.Entries.Select(e => TaskTable.Fold(e.Name)).SequenceEqual(page.Rows.Select(r => TaskTable.Fold(r.Name))), $"{file}: the list's entries");
        var read = 0;
        for (var i = 0; i < truth.Entries.Count; i++)
        {
            if (page.Rows[i].TimeLeft is not { } left)
                continue;
            Assert.True(TaskTruth.SameTime(left, truth.Entries[i].Time), $"{file}, entry {i + 1}: its countdown misread");
            read++;
        }
        return (truth.Entries.Count, read);
    }

    // Every truth's picture is read: the screen told right; on a table, every row's name, location and status, and an
    // expanded row's objectives and counts, exactly; the countdowns and progresses by vote, none wrong, nine in ten
    // of the countdowns read (2026-10-19: twelve of twelve) and half the progresses (seven of twelve: a lone slashed
    // "0%" is what the recognition drops). A trader's tab as CheckTrader has it (2026-10-25: the two here, read whole).
    [Fact]
    public async Task Real_screenshots_are_read_against_their_truths()
    {
        var truths = Truths(Fixtures);
        if (truths.Count == 0)
            Assert.Skip(@"Real screenshots stay out of the repository: none with a truth on this PC (tests\fixtures\ocr\operational).");
        var ocr = TasksReader.Create();
        if (ocr is null)
            Assert.Skip("This Windows has no English text recognition.");
        int times = 0, timesRead = 0, progresses = 0, progressesRead = 0;
        foreach (var (picturePath, truthPath) in truths)
        {
            var (page, truth) = await ReadAsync(ocr, picturePath, truthPath);
            var name = Path.GetFileName(truthPath);
            if (truth.Screen == "trader")
            {
                var (countdowns, read) = CheckTrader(page, truth, name);
                (times, timesRead) = (times + countdowns, timesRead + read);
                continue;
            }
            if (truth.Screen != "table")
                continue;
            Assert.Equal(truth.Rows.Select(r => r.Name), page.Rows.Select(r => r.Name));
            Assert.Equal(truth.Rows.Select(r => r.Location), page.Rows.Select(r => r.Location));
            Assert.Equal(truth.Rows.Select(r => TaskTable.Status(r.Status)), page.Rows.Select(r => r.Status));
            for (var i = 0; i < truth.Rows.Count; i++)
            {
                var (row, t) = (page.Rows[i], truth.Rows[i]);
                times++;
                if (row.TimeLeft is { } left)
                {
                    Assert.True(TaskTruth.SameTime(left, t.Time), $"{name}, {t.Name}: time left {TaskTable.Hms(left)}, not {t.Time}");
                    timesRead++;
                }
                progresses++;
                if (row.Progress is { } progress)
                {
                    Assert.True($"{progress}" == t.Progress, $"{name}, {t.Name}: progress {progress}, not {t.Progress}");
                    progressesRead++;
                }
                var objectives = truth.Objectives.Where(o => TaskTruth.Same(o.Row, t.Name)).ToList();
                Assert.Equal(objectives.Select(o => (o.Text, o.Count)), row.Objectives.Select(o => (o.Text, TaskTruth.Count(o))));
            }
        }
        Assert.True(timesRead >= 0.9 * times, $"{timesRead} of {times} countdowns read");
        Assert.True(progressesRead >= 0.5 * progresses, $"{progressesRead} of {progresses} progresses read");
    }

    // A trader's task view (owner, 2026-10-25) and the tables taken with it, in tests\fixtures\ocr\operational\trader (the
    // owner's screenshots of 2026-10-19, renamed): on a trader's tab, CheckTrader, the list's countdowns two in three read
    // (2026-10-25: two of three; a daily's orange "00:48:48" is what every way drops); on a table, every row's name,
    // location and status exactly and its countdown, days and all ("6 day(s) 23:07:39"), read and right. A table's
    // objectives and progresses are the other test's: these tables' "Ø/7" reads as "øn" and their lone "0%" not at all
    // (open, 2026-10-25).
    [Fact]
    public async Task Real_trader_screenshots_are_read_against_their_truths()
    {
        var truths = Truths(Path.Combine(Fixtures, "trader"));
        if (truths.Count == 0)
            Assert.Skip(@"Real screenshots stay out of the repository: none with a truth on this PC (tests\fixtures\ocr\operational\trader).");
        var ocr = TasksReader.Create();
        if (ocr is null)
            Assert.Skip("This Windows has no English text recognition.");
        int countdowns = 0, read = 0;
        foreach (var (picturePath, truthPath) in truths)
        {
            var (page, truth) = await ReadAsync(ocr, picturePath, truthPath);
            var file = Path.GetFileName(truthPath);
            if (truth.Screen == "trader")
            {
                var (all, taken) = CheckTrader(page, truth, file);
                (countdowns, read) = (countdowns + all, read + taken);
                continue;
            }
            Assert.True(truth.Rows.Select(r => (TaskTable.Fold(r.Name), (string?)r.Location, (string?)TaskTable.Status(r.Status)))
                .SequenceEqual(page.Rows.Select(r => (TaskTable.Fold(r.Name), r.Location, r.Status))), $"{file}: the rows");
            for (var i = 0; i < truth.Rows.Count; i++)
            {
                if (page.Rows[i].TimeLeft is { } left)
                    Assert.True(TaskTruth.SameTime(left, truth.Rows[i].Time), $"{file}, row {i + 1}: its countdown misread");
                else
                    Assert.True(TaskTable.DaysOf(truth.Rows[i].Time) is null, $"{file}, row {i + 1}: a countdown with days unread");
            }
        }
        Assert.True(3 * read >= 2 * countdowns, $"{read} of {countdowns} of the lists' countdowns read");
    }
}
