using Shturmap.Game.Screenshots;

namespace Shturmap.Game.Tests;

// The operational tasks' table read from a menu screenshot's words (owner, 2026-10-19: from a panel of four, B;
// docs/DESIGN.md §2, "Menu screenshots"; TaskTable, TasksReader; `shturmap-cli tasks` measures the same reader). The
// first tests lay out words by hand where the game's table
// puts them at 2560×1440, with made-up task names and objectives, so they need no picture and no text recognition.
// The last one reads the owner's real screenshots against hand-written truths; those stay on the PC that took them
// (tests\fixtures\ocr\operational, ignored by git) and it is skipped elsewhere.
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

    [Fact]
    public void A_traders_tasks_screen_is_told_and_nothing_is_read_from_it()
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
    }

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

    // Every truth's picture is read: the screen told right; on a table, every row's name, location and status, and an
    // expanded row's objectives and counts, exactly; the countdowns and progresses by vote, none wrong, nine in ten
    // of the countdowns read (2026-10-19: twelve of twelve) and half the progresses (seven of twelve: a lone slashed
    // "0%" is what the recognition drops).
    [Fact]
    public async Task Real_screenshots_are_read_against_their_truths()
    {
        var truths = Directory.Exists(Fixtures)
            ? Directory.GetFiles(Fixtures, "truth-*.txt").Order(StringComparer.Ordinal)
                .Select(t => (Picture: Path.Combine(Fixtures, Path.GetFileNameWithoutExtension(t)["truth-".Length..] + ".png"), Truth: t))
                .Where(t => File.Exists(t.Picture)).ToList()
            : [];
        if (truths.Count == 0)
            Assert.Skip(@"Real screenshots stay out of the repository: none with a truth on this PC (tests\fixtures\ocr\operational).");
        var ocr = TasksReader.Create();
        if (ocr is null)
            Assert.Skip("This Windows has no English text recognition.");
        int times = 0, timesRead = 0, progresses = 0, progressesRead = 0;
        foreach (var (picturePath, truthPath) in truths)
        {
            var truth = TaskTruth.Load(truthPath);
            var name = Path.GetFileName(truthPath);
            using var picture = StashPicture.Decode(await File.ReadAllBytesAsync(picturePath, TestContext.Current.CancellationToken));
            Assert.NotNull(picture);
            var page = await ocr.ReadPageAsync(picture, ct: TestContext.Current.CancellationToken);
            var screen = page.Screen switch { TaskScreen.Table => "table", TaskScreen.TraderScreen => "trader", TaskScreen.OtherTable => "other table", _ => "none" };
            Assert.True(screen == truth.Screen, $"{name}: {screen}");
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
                    Assert.True(TaskTable.Hms(left) == t.Time, $"{name}, {t.Name}: time left {TaskTable.Hms(left)}, not {t.Time}");
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
}
