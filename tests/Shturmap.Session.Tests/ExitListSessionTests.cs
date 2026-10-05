using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Raid;
using Shturmap.Core.Screenshots;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// The game's extract list, read from a screenshot that shows it (owner, 2026-10-05): what it names holds for the raid,
// the exits it doesn't name step back, and a new raid starts unchecked. The reader is the test's own here: it says
// what a picture "shows", so no picture is needed (tests\Shturmap.Game.Tests reads real ones).
public class ExitListSessionTests
{
    private static GameData Data(GameMode mode)
    {
        var data = SessionRig.Data(mode);
        ApiExtract Exit(string id, string name, string faction, double x) => new(id, name, faction, new ApiPosition(x, 0, 0), null, null, null);
        return new GameData
        {
            Mode = data.Mode,
            Language = data.Language,
            Maps = new[]
            {
                new ApiMap("map-customs", "Customs", "customs", "bigmap", "maps/customs_preset.bundle", null, SessionRig.CustomsMinutes,
                    [Exit("near", "Crossroads", "pmc", 20), Exit("mid", "Trailer Park", "pmc", 100), Exit("far", "ZB-1011", "pmc", 300), Exit("scav", "Old Road Gate", "scav", 10)],
                    [new ApiTransit("t1", "Transit to Reserve", "map-woods", new ApiPosition(200, 0, 0), null)], [], [], []),
            }.ToDictionary(m => m.Id),
            Tasks = data.Tasks,
            Traders = data.Traders,
            MapDefinitions = data.MapDefinitions,
            CheckedAt = data.CheckedAt,
        };
    }

    private static ExitListReading List(params (string Text, bool Marked)[] rows) =>
        new(ExitList.EnglishHeader, rows.Select(r => new ExitListRow(r.Text, r.Marked)).ToList());

    private static async Task<SessionRig> RaidOnCustoms(Func<string, ExitListReading?> shows)
    {
        var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations)
        {
            GivenData = Data,
            ExitReader = (path, _) => Task.FromResult(shows(path)),
        });
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.RaidMap?.NormalizedName == "customs" && s.Extracts.Count > 0, "the raid on Customs");
        return rig;
    }

    private static ExitState StateOf(SessionSnapshot s, string name) => s.Extracts.Single(e => e.Name == name).State;

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public void It_is_on_unless_the_saved_word_is_off(string? setting, bool on) =>
        Assert.Equal(on, GameSession.ReadExitsOn(setting));

    [Fact]
    public async Task Until_a_screenshot_shows_the_list_nothing_is_said_about_any_exit()
    {
        await using var rig = await RaidOnCustoms(_ => null);
        rig.Screenshot(0, 0, 0);
        var s = await rig.Until(s => s.RaidFix is not null, "the position");
        Assert.Null(s.ExitsReadAt);
        Assert.All(s.Extracts, e => Assert.Equal(ExitState.NotChecked, e.State));
        // Extracts nearest first, then transits (owner, 2026-10-05: "I would show primarily exfils"); the Scav's exit
        // isn't a PMC's.
        Assert.Equal(["Crossroads", "Trailer Park", "ZB-1011", "Transit to another map"], s.Extracts.Select(e => e.Name));
    }

    [Fact]
    public async Task The_list_in_a_screenshot_says_which_exits_are_the_players_and_the_others_come_last()
    {
        await using var rig = await RaidOnCustoms(_ => List(("EXFILØI Trailer Park", true), ("EXFILØ2 ZB-IØII", false), ("TRANSITØI Transit to Reserve", true)));
        rig.Screenshot(0, 0, 0);
        var s = await rig.Until(s => s.ExitsReadAt is not null, "the list read");
        Assert.Equal(ExitState.Unsure, StateOf(s, "Trailer Park"));
        Assert.Equal(ExitState.Listed, StateOf(s, "ZB-1011"));
        Assert.Equal(ExitState.NotListed, StateOf(s, "Crossroads"));
        // A transit is open to everyone, and the list isn't read for it.
        Assert.Equal(ExitState.NotChecked, StateOf(s, "Transit to another map"));
        // Crossroads is the nearest, and not on the list: it comes last, after the transit.
        Assert.Equal(["Trailer Park", "ZB-1011", "Transit to another map", "Crossroads"], s.Extracts.Select(e => e.Name));
        Assert.Contains(rig.Notices, n => n.Contains("2 extracts", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_later_list_adds_to_what_was_read_and_never_takes_an_exit_away()
    {
        var shots = 0;
        await using var rig = await RaidOnCustoms(_ => Interlocked.Increment(ref shots) == 1
            ? List(("EXFILØI Trailer Park", true), ("EXFILØ2 ZB-1011", false))
            : List(("EXFILØI Trailer Park", false), ("EXFILØ3 Crossroads", false)));
        rig.Screenshot(0, 0, 0);
        await rig.Until(s => s.ExitsReadAt is not null, "the first list");
        rig.Screenshot(5, 0, 0);
        var s = await rig.Until(s => StateOf(s, "Crossroads") == ExitState.Listed, "the second list");
        Assert.Equal(ExitState.Listed, StateOf(s, "Trailer Park")); // its "???" is gone in the later list
        Assert.Equal(ExitState.Listed, StateOf(s, "ZB-1011"));
    }

    [Fact]
    public async Task The_box_of_the_exit_the_player_stands_in_is_no_list()
    {
        await using var rig = await RaidOnCustoms(_ => new ExitListReading("Stay in the extraction point", [new ExitListRow("EXFILØ2 ZB-1011", true)]));
        rig.Screenshot(0, 0, 0);
        await rig.Until(s => s.RaidFix is not null, "the position");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Null(rig.Snapshot.ExitsReadAt);
        Assert.All(rig.Snapshot.Extracts, e => Assert.Equal(ExitState.NotChecked, e.State));
    }

    [Fact]
    public async Task The_next_raid_starts_unchecked()
    {
        await using var rig = await RaidOnCustoms(_ => List(("EXFILØI Trailer Park", false), ("EXFILØ2 ZB-1011", false)));
        rig.Screenshot(0, 0, 0);
        await rig.Until(s => s.ExitsReadAt is not null, "the list read");
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
        await rig.Until(s => s.Raid.Phase == RaidPhase.Menu, "the raid over");
        Assert.Null(rig.Snapshot.ExitsReadAt);
        Assert.All(rig.Snapshot.Extracts, e => Assert.Equal(ExitState.NotChecked, e.State));
    }

    [Fact]
    public async Task Unticked_no_picture_is_opened_and_what_was_read_is_let_go()
    {
        var opened = 0;
        await using var rig = await RaidOnCustoms(_ =>
        {
            Interlocked.Increment(ref opened);
            return List(("EXFILØI Trailer Park", false), ("EXFILØ2 ZB-1011", false));
        });
        rig.Screenshot(0, 0, 0);
        await rig.Until(s => s.ExitsReadAt is not null, "the list read");
        Assert.True(rig.Snapshot.ReadExits);

        await rig.Session.SetReadExitsAsync(false);
        Assert.False(rig.Snapshot.ReadExits);
        Assert.Null(rig.Snapshot.ExitsReadAt);
        Assert.Equal("off", rig.Session.GetSetting(GameSession.ReadExitsSetting));
        rig.Screenshot(7, 0, 0);
        await rig.Until(s => s.RaidFix?.Position.X == 7, "the next position");
        await Task.Delay(300, TestContext.Current.CancellationToken);
        Assert.Equal(1, opened);
        Assert.All(rig.Snapshot.Extracts, e => Assert.Equal(ExitState.NotChecked, e.State));
    }

    [Fact]
    public void A_sides_exits_are_the_ones_a_row_can_name()
    {
        var data = Data(GameMode.Pve);
        Assert.Equal(["extract:far", "extract:mid", "extract:near"], GameSession.ExitNames(data, "map-customs", RaidSide.Pmc).Select(e => e.Id).Order());
        Assert.Equal(["extract:scav"], GameSession.ExitNames(data, "map-customs", RaidSide.Scav).Select(e => e.Id));
        Assert.Empty(GameSession.ExitNames(data, "no-such-map", RaidSide.Pmc));
    }
}
