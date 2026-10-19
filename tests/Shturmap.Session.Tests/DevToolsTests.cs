#if DEVTOOLS
using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Raid;
using Shturmap.Core.Screenshots;
using Shturmap.Game.Logs;
using Shturmap.Session.Dev;

namespace Shturmap.Session.Tests;

// The developer view writes what the game would; these read it back with the app's own parser, tracker and screenshot
// reader, so a dev raid exercises exactly what a real one does (docs/DESIGN.md §8, "Developer aids").
public sealed class DevToolsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shturmap-devtools-").FullName;

    public void Dispose() => Directory.Delete(_root, true);

    private static readonly DevMap Streets = DevMap.From("streets-of-tarkov", "Streets of Tarkov", "maps/city_preset.bundle", "TarkovStreets");

    private IReadOnlyList<GameEvent> Read(FakeGame game) => LogTailer.ReadSession(game.Session);

    [Theory]
    [InlineData(GameMode.Pve, "Pve")]
    [InlineData(GameMode.Pvp, "Regular")]
    [InlineData(GameMode.Seasonal, "PvpSeason")]
    public void Mode_lines_read_back_as_that_mode(GameMode mode, string raw)
    {
        var game = new FakeGame(_root);
        game.Mode(mode);
        var e = Assert.IsType<SessionModeEvent>(Assert.Single(Read(game)));
        Assert.Equal(mode, e.Mode);
        Assert.Equal(raw, e.Raw);
    }

    [Theory]
    [InlineData(false, false, RaidSide.Pmc)]
    [InlineData(true, false, RaidSide.Scav)]
    [InlineData(false, true, RaidSide.Unknown)] // a local raid's logs can't tell the side
    public void A_dev_raid_runs_through_the_tracker_from_loading_to_its_end(bool scav, bool local, RaidSide side)
    {
        var game = new FakeGame(_root);
        game.Mode(GameMode.Pve);
        game.ProfileLoaded();
        game.LoadingStarts(Streets, scav, local);
        game.LoadingSteps();
        game.RaidStarts(local);
        var tracker = new RaidTracker();
        var transitions = Read(game).Select(tracker.Apply).OfType<RaidTransition>().ToList();
        Assert.Single(transitions.OfType<RaidLoading>());
        var started = Assert.IsType<RaidStarted>(transitions[^1]);
        Assert.Equal(RaidPhase.InRaid, started.State.Phase);
        Assert.Equal("maps/city_preset.bundle", started.State.ScenePath);
        Assert.Equal("TarkovStreets", started.State.LocationId);
        Assert.Equal(side, started.State.Side);
        Assert.Equal(5, Read(game).OfType<LoadingStepEvent>().Count());

        game.RaidEnds();
        var ended = Read(game).Select(new RaidTracker().Apply).OfType<RaidEnded>().Single();
        Assert.Equal(RaidPhase.Menu, ended.State.Phase);
    }

    [Fact]
    public void A_map_without_a_scene_in_the_data_is_named_by_its_location_id()
    {
        var map = DevMap.From("ground-zero-21", "Ground Zero 21+", null, "Sandbox_high");
        Assert.Equal("maps/sandbox_high_preset.bundle", map.ScenePath);
        var game = new FakeGame(_root);
        game.LoadingStarts(map, scav: false, local: true);
        Assert.Equal("Sandbox_high", Read(game).OfType<TransitInfoEvent>().Single().LocationId);
    }

    [Theory]
    [InlineData(QuestLogStatus.Started)]
    [InlineData(QuestLogStatus.Completed)]
    [InlineData(QuestLogStatus.Failed)]
    public void Quest_messages_read_back_with_their_quest_and_status(QuestLogStatus status)
    {
        var game = new FakeGame(_root);
        game.Quest("5967733e86f774602332fc84", status, "54cb50c76803fa8b248b4571");
        var e = Assert.IsType<QuestEvent>(Assert.Single(Read(game)));
        Assert.Equal("5967733e86f774602332fc84", e.QuestId);
        Assert.Equal(status, e.Status);
        Assert.Equal("54cb50c76803fa8b248b4571", e.TraderId);
    }

    [Fact]
    public void A_group_pick_reads_back_with_its_map()
    {
        var game = new FakeGame(_root);
        game.GroupPick(Streets);
        var events = Read(game);
        Assert.Equal("TarkovStreets", events.OfType<GroupRaidSettingsEvent>().Single().LocationId);
        Assert.Equal(GroupStatus.Ready, events.OfType<GroupStatusEvent>().Single().Status);
    }

    [Theory]
    [InlineData(-60.00, 3.50, 300.00, 0)]
    [InlineData(40.00, 2.50, 120.00, 90)]
    [InlineData(25.4, -3.5, -210.75, 237.5)]
    public void A_screenshot_name_reads_back_as_its_position_facing_and_time(double x, double y, double z, double yaw)
    {
        var game = new FakeGame(_root);
        var at = new DateTime(2026, 10, 3, 14, 7, 30);
        var path = game.Screenshot(new WorldPoint(x, y, z), yaw, at, raidClockHours: 14.13);
        Assert.True(ScreenshotName.TryParse(path, out var info));
        Assert.Equal(new WorldPoint(x, y, z), info.Position);
        Assert.Equal(yaw, info.YawDegrees!.Value, 1);
        Assert.Equal(14.13, info.RaidClockHours);
        Assert.Equal(new DateTime(2026, 10, 3, 14, 7, 0), info.TakenAt);
        Assert.Equal(at, File.GetCreationTime(path));
    }

    [Fact]
    public void Shots_in_the_same_minute_count_up_as_the_game_numbers_them()
    {
        var game = new FakeGame(_root);
        var at = new DateTime(2026, 10, 3, 14, 7, 10);
        var first = game.Screenshot(new WorldPoint(1, 2, 3), 0, at);
        var second = game.Screenshot(new WorldPoint(1, 2, 3), 0, at.AddSeconds(20));
        Assert.EndsWith("(0).png", first);
        Assert.EndsWith("(1).png", second);
    }

    [Fact]
    public void A_drag_gives_the_facing_in_world_degrees()
    {
        Assert.Equal(0, DevPlaces.YawOf(0, 0, 0, 10)!.Value, 6);
        Assert.Equal(90, DevPlaces.YawOf(0, 0, 10, 0)!.Value, 6);
        Assert.Equal(270, DevPlaces.YawOf(0, 0, -10, 0)!.Value, 6);
        Assert.Null(DevPlaces.YawOf(5, 5, 5, 5));
    }

    [Fact]
    public void A_picked_place_takes_the_shown_floors_height()
    {
        var upper = new MapLayer("2nd Floor", null, null, false,
            [new LayerExtent(new HeightRange(8, 12), [new WorldBox(0, 0, 50, 50)]), new LayerExtent(new HeightRange(20, 24), [])]);
        var map = new MapDefinition
        {
            Key = "customs", Transform = [1, 0, 1, 0], Bounds = new WorldBox(-100, -100, 100, 100), Layers = [upper],
        };
        Assert.Equal(10, DevPlaces.HeightFor(map, upper, 10, 10, []));
        Assert.Equal(22, DevPlaces.HeightFor(map, upper, 90, 90, [])); // outside the first band's boxes: the band that has none
        // The base map: the nearest known place within 60 m that isn't on an upper floor.
        Assert.Equal(1.5, DevPlaces.HeightFor(map, null, 80, 80, [new WorldPoint(85, 1.5, 85), new WorldPoint(10, 10, 10)]));
        Assert.Equal(0, DevPlaces.HeightFor(map, null, -90, -90, [new WorldPoint(85, 1.5, 85)]));
        Assert.Equal(3, DevPlaces.HeightFor(map with { BaseHeight = new HeightRange(0, 6) }, null, 0, 0, []));
    }

    [Fact]
    public void Old_dev_view_folders_are_removed_and_nothing_else()
    {
        var temp = Path.GetTempPath();
        var old = Directory.CreateDirectory(Path.Combine(temp, "shturmap-devview-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        var fresh = Directory.CreateDirectory(Path.Combine(temp, "shturmap-devview-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        var other = Directory.CreateDirectory(Path.Combine(temp, "shturmap-devview-keep-" + Guid.NewGuid().ToString("N")[..8])).FullName;
        try
        {
            Directory.SetLastWriteTime(old, DateTime.Now.AddDays(-3));
            Directory.SetLastWriteTime(other, DateTime.Now.AddDays(-3));
            FakeGame.PruneTemporary(TimeSpan.FromDays(1));
            Assert.False(Directory.Exists(old));
            Assert.True(Directory.Exists(fresh));
            Assert.True(Directory.Exists(other));
        }
        finally
        {
            foreach (var dir in new[] { old, fresh, other }.Where(Directory.Exists))
                Directory.Delete(dir, true);
        }
    }

    // ---- the changelog of a developer build ----

    private const string Changelog = """
        { "build": "b018f54aa0c1d2e3f4a5b6c7d8e9f00112233445", "built": "2026-10-03T14:05:00+02:00",
          "commits": [
            { "hash": "b018f54", "date": "2026-10-03T13:50:00+02:00", "subject": "Never share a position, never take a screenshot" },
            { "hash": "aeff175", "date": "2026-10-03T12:10:00+02:00", "subject": "docs/NEXT.md: what's queued for Monday" },
            { "hash": "", "subject": "no hash: left out" },
            { "subject": "no hash either" },
            42
          ] }
        """;

    [Fact]
    public void A_changelog_reads_its_build_and_commits_newest_first()
    {
        var log = DevChangelog.Parse(Changelog)!;
        Assert.Equal("b018f54aa0c1d2e3f4a5b6c7d8e9f00112233445", log.Build);
        Assert.Equal(new DateTimeOffset(2026, 10, 3, 14, 5, 0, TimeSpan.FromHours(2)), log.Built);
        Assert.Equal(["b018f54", "aeff175"], log.Commits.Select(c => c.Hash));
        Assert.Equal(["aeff175"], log.Matching("monday").Select(c => c.Hash));
        Assert.Equal(["b018f54"], log.Matching("B018").Select(c => c.Hash));
        Assert.Equal(["b018f54"], log.Matching("never screenshot").Select(c => c.Hash));
        Assert.Empty(log.Matching("dev view"));
        Assert.Equal(2, log.Matching("  ").Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    public void A_broken_changelog_is_no_changelog(string json) => Assert.Null(DevChangelog.Parse(json));

    [Fact]
    public void A_changelog_file_is_read_from_the_apps_folder_or_is_absent()
    {
        Assert.Null(DevChangelog.Read(_root));
        Directory.CreateDirectory(Path.Combine(_root, "dev"));
        File.WriteAllText(Path.Combine(_root, "dev", "changelog.json"), Changelog);
        Assert.Equal(2, DevChangelog.Read(_root)!.Commits.Count);
    }

    [Theory]
    [InlineData("0.2.0+b018f54aa0c1d2e3f4a5", "b018f54")]
    [InlineData("0.2.0", null)]
    [InlineData(null, null)]
    public void The_builds_commit_comes_from_its_informational_version(string? version, string? commit) =>
        Assert.Equal(commit, DevChangelog.CommitOf(version));

    // ---- a developer script ----

    [Fact]
    public void A_script_is_read_into_steps_with_quoted_words_and_comments()
    {
        // Lines joined, not a raw string: a Release build leaves this file out, and the compiler still reads each line
        // of it that starts with "#" as a directive.
        var (steps, errors) = DevScript.Parse(string.Join('\n',
            "# a raid on Customs",
            "mode pve",
            "map customs   # shown in the app",
            "quest start \"Shaking Up the Teller\"",
            "place 0.5 0.4 0.55 0.4",
            "jump around"));
        Assert.Equal(["mode pve", "map customs", "quest start Shaking Up the Teller", "place 0.5 0.4 0.55 0.4"], steps.Select(s => s.ToString()));
        Assert.Equal(["start", "Shaking Up the Teller"], steps[2].Args);
        Assert.Equal(0.55, steps[3].Number(2));
        Assert.Equal(7, steps[3].Number(9, 7));
        Assert.Equal(["line 6: unknown step 'jump'"], errors);
    }

    [Fact]
    public void A_note_step_reads_its_quest_and_the_rest_of_the_line_as_its_words()
    {
        var (steps, errors) = DevScript.Parse(string.Join('\n',
            "note Dandies \"Behind the bar, ground floor\"",
            "note \"Shooting Cans\" under the bridge",
            "note Dandies \"Key in the jacket\\nthird floor\"",
            "note Dandies"));
        Assert.Empty(errors);
        Assert.Equal(("Dandies", "Behind the bar, ground floor"), DevScript.Note(steps[0]));
        Assert.Equal(("Shooting Cans", "under the bridge"), DevScript.Note(steps[1]));
        // "\n" breaks the line, as Shift+Enter does in the card's field.
        Assert.Equal(("Dandies", "Key in the jacket\nthird floor"), DevScript.Note(steps[2]));
        Assert.Null(DevScript.Note(steps[3]));
    }

    // The owner's report of 2026-10-15 on What's New's tour:5 line, replayed without a mouse: a tap on a line, and the
    // pointer leaving it, beside the card and its previews as before.
    [Fact]
    public void Whats_new_steps_point_at_a_line_click_one_or_leave_it()
    {
        var (steps, errors) = DevScript.Parse(string.Join('\n',
            "whatsnew",
            "whatsnew 3",
            "whatsnew click 4",
            "WhatsNew Click 2",
            "whatsnew leave",
            "whatsnew click",
            "whatsnew click none",
            "whatsnew click 0"));
        Assert.Equal(["line 6: 'whatsnew click' needs the line's number, from 1", "line 7: 'whatsnew click' needs the line's number, from 1",
            "line 8: 'whatsnew click' needs the line's number, from 1"], errors);
        Assert.Equal(
            [(DevScript.WhatsNewAct.Point, 0), (DevScript.WhatsNewAct.Point, 3), (DevScript.WhatsNewAct.Click, 4), (DevScript.WhatsNewAct.Click, 2),
                (DevScript.WhatsNewAct.Leave, 0)],
            steps.Select(s => DevScript.WhatsNewOf(s)!.Value));
    }

    // An older version's lines (owner, 2026-10-15, D10: 0.4.0's extracts preview): the version first, then what the step
    // does, as for the newest version's card. A line's number is never taken for a version.
    [Fact]
    public void Whats_new_steps_reach_an_older_versions_card_by_its_version()
    {
        var (steps, errors) = DevScript.Parse(string.Join('\n',
            "whatsnew 0.4.0",
            "whatsnew 0.4.0 2",
            "whatsnew 0.5.0 click 4",
            "whatsnew 0.4.0 leave",
            "whatsnew 3",
            "whatsnew 0.4.0 click"));
        Assert.Equal(["line 6: 'whatsnew click' needs the line's number, from 1"], errors);
        Assert.Equal(["0.4.0", "0.4.0", "0.5.0", "0.4.0", null], steps.Select(DevScript.WhatsNewVersionOf));
        Assert.Equal(
            [(DevScript.WhatsNewAct.Point, 0), (DevScript.WhatsNewAct.Point, 2), (DevScript.WhatsNewAct.Click, 4), (DevScript.WhatsNewAct.Leave, 0),
                (DevScript.WhatsNewAct.Point, 3)],
            steps.Select(s => DevScript.WhatsNewOf(s)!.Value));
        // A tap and a leaving follow at once, the version or none.
        Assert.True(DevScript.FollowsAtOnce(steps[2]));
        Assert.False(DevScript.FollowsAtOnce(steps[1]));
    }

    // "show <quest> at row" (owner, 2026-10-15, D10): the card held at the quest's own row, as a click on the row holds it;
    // without "at row", where --show-quest puts it, as before.
    [Fact]
    public void A_show_step_holds_the_card_at_its_row_with_at_row()
    {
        var (steps, errors) = DevScript.Parse(string.Join('\n',
            "show Dandies",
            "show insomnia at row",
            "show \"The Tarkov Shooter - Part 1\" AT ROW",
            "show Shooting at",
            "show"));
        Assert.Equal(["line 5: 'show' needs a quest's name"], errors);
        Assert.Equal(("Dandies", false), DevScript.ShowOf(steps[0]));
        Assert.Equal(("insomnia", true), DevScript.ShowOf(steps[1]));
        Assert.Equal(("The Tarkov Shooter - Part 1", true), DevScript.ShowOf(steps[2]));
        // "at" alone is a word of the name.
        Assert.Equal(("Shooting at", false), DevScript.ShowOf(steps[3]));
    }

    // "focus <part>" (QA pass, 2026-10-15: no step could focus a status bar button, so no still showed a focus ring): the
    // status bar's three buttons, the map and the rail by a word, any other control by its x:Name.
    [Fact]
    public void A_focus_step_names_the_control_by_its_word_or_its_x_name()
    {
        var (steps, errors) = DevScript.Parse(string.Join('\n',
            "focus help",
            "focus Feedback",
            "focus settings",
            "focus map",
            "focus rail",
            "focus MapPicker",
            "focus",
            "focus help settings"));
        Assert.Equal(["line 7: 'focus' takes one of help, feedback, settings, map, rail or a control's x:Name",
            "line 8: 'focus' takes one of help, feedback, settings, map, rail or a control's x:Name"], errors);
        Assert.Equal(["HelpButton", "FeedbackButton", "SettingsButton", "Map", "Rail", "MapPicker"], steps.Select(DevScript.FocusOf));
    }

    // The QA pass replays the roll call without a screenshot (owner, 2026-10-15, from a panel of four: A): "exits" names the
    // game's list as a screenshot would show it, "?" at a name's end for "??:??:??", "all" for every extract of the side.
    [Fact]
    public void The_exits_step_reads_its_names_their_marks_and_all()
    {
        var (steps, errors) = DevScript.Parse(string.Join('\n',
            "exits Crossroads \"Trailer Park\" \"ZB-1013?\" \"Railroad Passage (Flare)\"",
            "exits all",
            "Exits ALL",
            "exits \"Dorms V-Ex ?\"",
            "exits",
            "exits ? \"\""));
        Assert.Equal(["line 5: 'exits' needs the extracts' names, or 'all'", "line 6: 'exits' needs the extracts' names, or 'all'"], errors);
        Assert.Equal(4, steps.Count);
        Assert.Equal([("Crossroads", false), ("Trailer Park", false), ("ZB-1013", true), ("Railroad Passage (Flare)", false)], DevScript.ExitsOf(steps[0]));
        Assert.Null(DevScript.ExitsOf(steps[1]));
        Assert.Null(DevScript.ExitsOf(steps[2]));
        Assert.Equal([("Dorms V-Ex", true)], DevScript.ExitsOf(steps[3]));
        // It writes for the app to read, as a screenshot does: the app is given its 0.3 s.
        Assert.False(DevScript.FollowsAtOnce(steps[0]));
    }

    // The QA pass pictures the operational tasks without a screenshot of the Tasks screen (owner, 2026-10-19, B): "tasks"
    // takes how many made-up tasks (3 without a number, up to 8 so the cue's "+N" can be reached: QA pass of 2026-10-19,
    // F3) and "ended" for one more that runs out at once.
    [Fact]
    public void The_tasks_step_reads_how_many_and_ended()
    {
        var (steps, errors) = DevScript.Parse(string.Join('\n', "tasks", "tasks 5", "tasks 2 ended", "Tasks ENDED", "tasks 8", "tasks 9", "tasks some"));
        Assert.Equal(["line 6: 'tasks' takes how many (0 to 8) and 'ended'", "line 7: 'tasks' takes how many (0 to 8) and 'ended'"], errors);
        Assert.Equal([(3, false), (5, false), (2, true), (3, true), (8, false)], steps.Select(s => DevScript.TasksOf(s)!.Value));
        Assert.False(DevScript.FollowsAtOnce(steps[0]));
    }

    // QA pass of 2026-10-19 (F4): no step chose a text size while Shturmap runs, so its change couldn't be pictured.
    // "textsize" takes settings' three steps, as shturmap.db keeps them.
    [Fact]
    public void The_textsize_step_takes_settings_three_steps()
    {
        var (steps, errors) = DevScript.Parse(string.Join('\n', "textsize normal", "textsize Larger", "TEXTSIZE largest", "textsize", "textsize huge", "textsize larger largest"));
        Assert.Equal(["line 4: 'textsize' takes normal, larger or largest", "line 5: 'textsize' takes normal, larger or largest",
            "line 6: 'textsize' takes normal, larger or largest"], errors);
        Assert.Equal(["normal", "larger", "largest"], steps.Select(s => DevScript.TextSizeOf(s)!));
        // The window takes it at once, but what follows (the text's pass, the map, the smallest window) is given its 0.3 s.
        Assert.False(DevScript.FollowsAtOnce(steps[0]));
    }

    // tools\qa\extract-roll-call.devscript: a raid on Customs, a position, the list read, the stills, then a second raid
    // that reads it again.
    [Fact]
    public void The_qa_replay_of_the_roll_call_reads_without_errors()
    {
        var (steps, errors) = DevScript.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", "qa", "extract-roll-call.devscript")));
        Assert.Empty(errors);
        var lists = steps.Where(s => s.Verb == "exits").Select(DevScript.ExitsOf).ToList();
        Assert.Equal(2, lists.Count);
        Assert.All(lists, list => Assert.Equal([("Crossroads", false), ("Trailer Park", false), ("ZB-1013", true), ("Railroad Passage (Flare)", false)], list));
        Assert.Contains(steps, s => s.Verb == "map" && s.Arg(0) == "customs");
        Assert.True(steps.ToList().FindIndex(s => s.Verb == "pos") < steps.ToList().FindIndex(s => s.Verb == "exits"));
        Assert.Equal(["t0", "t1", "t2", "t3", "t4"], steps.Where(s => s.Verb == "snapshot").Select(s => Path.GetFileName(s.Arg(0))));
        Assert.All(steps.Where(s => s.Verb == "snapshot"), s => Assert.StartsWith(@"%TEMP%\shturmap-qa\extract-roll-call\", s.Arg(0)));
        Assert.Equal("exit", steps[^1].Verb);
    }

    [Fact]
    public void The_tour_step_ends_the_tour_and_takes_nothing_else()
    {
        var (steps, errors) = DevScript.Parse(string.Join('\n', "tour end", "tour", "tour 5"));
        Assert.Equal(["tour end"], steps.Select(s => s.ToString()));
        Assert.Equal(["line 2: 'tour' takes 'end'", "line 3: 'tour' takes 'end'"], errors);
    }

    // A step that writes nothing for the app to read follows at once: the click on a tour line, the pointer's leaving
    // 0.4 s later and the snapshot after it keep the times the script gives them.
    [Theory]
    [InlineData("whatsnew click 4", true)]
    [InlineData("whatsnew leave", true)]
    [InlineData("hover", true)]
    [InlineData("point", true)]
    [InlineData("trail 1 2", true)]
    [InlineData("cards", true)]
    [InlineData("wait 0.4", true)]
    [InlineData("whatsnew", false)]
    [InlineData("whatsnew 2", false)]
    [InlineData("tour end", false)]
    [InlineData("map woods", false)]
    [InlineData("snapshot t0", false)]
    public void Only_the_pointers_steps_and_wait_follow_at_once(string line, bool atOnce) =>
        Assert.Equal(atOnce, DevScript.FollowsAtOnce(Assert.Single(DevScript.Parse(line).Steps)));

    [Fact]
    public void A_snapshots_folder_takes_its_variables_so_a_script_in_the_repository_names_no_users_folder()
    {
        Environment.SetEnvironmentVariable("SHTURMAP_DEVSCRIPT_TEST", @"D:\qa");
        try
        {
            Assert.Equal(@"D:\qa\whatsnew-tour-line\t0", DevScript.Folder(@"%SHTURMAP_DEVSCRIPT_TEST%\whatsnew-tour-line\t0"));
            Assert.Equal(@"D:\plain", DevScript.Folder(@"D:\plain"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SHTURMAP_DEVSCRIPT_TEST", null);
        }
    }

    // The QA pass's replay of the tour:5 line (tools\qa): every step one the app knows, with what it takes.
    [Fact]
    public void The_qa_replay_of_whats_news_tour_line_reads_without_errors()
    {
        var (steps, errors) = DevScript.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", "qa", "whatsnew-tour-line.devscript")));
        Assert.Empty(errors);
        Assert.Contains(steps, s => DevScript.WhatsNewOf(s) is (DevScript.WhatsNewAct.Click, > 0) && s.Verb == "whatsnew");
        // The line of the owner's report: 0.5.0's fourth, its tour:5 line (0.6.0 is the newest card now).
        Assert.Contains(steps, s => s.Verb == "whatsnew" && DevScript.WhatsNewVersionOf(s) == "0.5.0" && DevScript.WhatsNewOf(s) is (DevScript.WhatsNewAct.Click, 4));
        Assert.Contains(steps, s => s.Verb == "whatsnew" && DevScript.WhatsNewOf(s) is (DevScript.WhatsNewAct.Leave, _));
        Assert.All(steps.Where(s => s.Verb == "snapshot"), s => Assert.StartsWith("%TEMP%", s.Arg(0)));
        Assert.Equal("exit", steps[^1].Verb);
    }

    // tools\qa (docs/QA.md, "Replay scripts"): one .devscript and one .md of expectations per flow, every step one the
    // app knows, each still in a folder of its own flow under %TEMP%\shturmap-qa, and the run ends with "exit".
    [Fact]
    public void Every_qa_replay_reads_without_errors_and_has_its_expectations_beside_it()
    {
        var scripts = Directory.GetFiles(Path.Combine(RepositoryRoot(), "tools", "qa"), "*.devscript");
        Assert.NotEmpty(scripts);
        foreach (var script in scripts)
        {
            var flow = Path.GetFileNameWithoutExtension(script);
            Assert.True(File.Exists(Path.ChangeExtension(script, ".md")), $"{flow}.md is missing beside {flow}.devscript");
            var (steps, errors) = DevScript.Parse(File.ReadAllText(script));
            Assert.Empty(errors);
            Assert.All(steps.Where(s => s.Verb == "snapshot"), s => Assert.StartsWith($@"%TEMP%\shturmap-qa\{flow}\", s.Arg(0)));
            Assert.Equal("exit", steps[^1].Verb);
        }
    }

    // DESIGN.md §8's list of the script's verbs follows DevScript.Verbs, the one source of truth: none is left out.
    [Fact]
    public void Designs_list_of_script_verbs_names_every_verb_the_parser_knows()
    {
        var design = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "DESIGN.md"));
        var start = design.IndexOf("`--dev-script <file>` plays", StringComparison.Ordinal);
        Assert.True(start >= 0, "DESIGN.md §8 has no --dev-script bullet");
        var end = design.IndexOf("for checks without clicking", start, StringComparison.Ordinal);
        Assert.True(end > start, "DESIGN.md's --dev-script bullet doesn't end its list with \"for checks without clicking\"");
        var list = design[start..end];
        Assert.All(DevScript.Verbs.Keys, verb => Assert.Contains("`" + verb, list));
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Shturmap.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
#endif
