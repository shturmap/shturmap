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
        var (steps, errors) = DevScript.Parse("""
            # a raid on Customs
            mode pve
            map customs   # shown in the app
            quest start "Shaking Up the Teller"
            place 0.5 0.4 0.55 0.4
            jump around
            """);
        Assert.Equal(["mode pve", "map customs", "quest start Shaking Up the Teller", "place 0.5 0.4 0.55 0.4"], steps.Select(s => s.ToString()));
        Assert.Equal(["start", "Shaking Up the Teller"], steps[2].Args);
        Assert.Equal(0.55, steps[3].Number(2));
        Assert.Equal(7, steps[3].Number(9, 7));
        Assert.Equal(["line 6: unknown step 'jump'"], errors);
    }
}
#endif
