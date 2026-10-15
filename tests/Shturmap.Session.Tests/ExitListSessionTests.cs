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
        // No notice: the window calls the list out with YOUR EXTRACTS THIS RAID, and the rail says it was read (owner,
        // 2026-10-15, from a panel of four: A; until then "Your extract list is read: 2 extracts for this raid").
        Assert.DoesNotContain(rig.Notices, n => n.Contains("extract", StringComparison.OrdinalIgnoreCase));
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

    // The whole way, with the reader the app uses: the game writes a screenshot while the raid fades in from black, the
    // watcher sees it, Windows' text recognition reads its top right corner, and the extracts take what the list says,
    // as the map draws them (lit, "?", struck out). Until 2026-10-15 such a dark picture was let go unread, without a
    // word: five of the owner's ten latest lists. The picture is drawn, so this runs on any PC with English text
    // recognition; tests\Shturmap.Game.Tests reads the owner's real ones.
    [Fact]
    public async Task A_screenshot_taken_as_the_raid_fades_in_is_read_and_the_map_takes_its_list()
    {
        if (Shturmap.Game.Screenshots.ExitListReader.Create("en") is null)
            Assert.Skip("This Windows has no text recognition language installed.");
        await using var rig = new SessionRig(configure: (paths, locations) => new GameSession(paths, locations) { GivenData = Data });
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.RaidMap?.NormalizedName == "customs" && s.Extracts.Count > 0, "the raid on Customs");

        rig.Screenshot(0, 0, 0, ListPicture(fadedTo: 0.3,
            ("EXFIL01", "Trailer Park", "??:??:??"), ("EXFIL02", "ZB-1011", ""), ("TRANSIT01", "Transit to Reserve", "0:00:57")));
        var s = await rig.Until(s => s.ExitsReadAt is not null, "the list read from the screenshot");
        Assert.Equal(ExitState.Unsure, StateOf(s, "Trailer Park"));
        Assert.Equal(ExitState.Listed, StateOf(s, "ZB-1011"));
        Assert.Equal(ExitState.NotListed, StateOf(s, "Crossroads"));
        Assert.Equal(ExitState.NotChecked, StateOf(s, "Transit to another map"));
        Assert.DoesNotContain(rig.Notices, n => n.Contains("extract", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A 2560×1440 screenshot's PNG with the game's list in its top right corner, as the game lays it out, the
    /// whole picture darkened to <paramref name="fadedTo"/> of its light.</summary>
    private static byte[] ListPicture(double fadedTo, params (string Label, string Name, string Right)[] rows)
    {
        using var bitmap = new SkiaSharp.SKBitmap(2560, 1440);
        using (var canvas = new SkiaSharp.SKCanvas(bitmap))
        {
            canvas.Clear(new SkiaSharp.SKColor(96, 104, 92));
            const float left = 2560 - 768, top = 5;
            using var panel = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(14, 16, 14) };
            canvas.DrawRect(left, top, 768, 74 + 70 * rows.Length, panel);
            using var green = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(124, 167, 14) };
            canvas.DrawRect(left, top, 573, 74, green);
            using var face = SkiaSharp.SKTypeface.FromFamilyName("Bahnschrift", SkiaSharp.SKFontStyle.Bold) ?? SkiaSharp.SKTypeface.Default;
            using var big = new SkiaSharp.SKFont(face, 30);
            using var small = new SkiaSharp.SKFont(face, 24);
            using var dark = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(10, 12, 10), IsAntialias = true };
            using var light = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(205, 210, 205), IsAntialias = true };
            canvas.DrawText(ExitList.EnglishHeader, left + 70, top + 48, SkiaSharp.SKTextAlign.Left, big, dark);
            for (var i = 0; i < rows.Length; i++)
            {
                var y = top + 74 + 70 * i + 46;
                canvas.DrawText(rows[i].Label, left + 14, y, SkiaSharp.SKTextAlign.Left, big, light);
                canvas.DrawText(rows[i].Name, left + 28 + big.MeasureText(rows[i].Label), y, SkiaSharp.SKTextAlign.Left, small, light);
                canvas.DrawText(rows[i].Right, left + 605, y, SkiaSharp.SKTextAlign.Left, big, light);
            }
            using var fade = new SkiaSharp.SKPaint { Color = new SkiaSharp.SKColor(0, 0, 0, (byte)Math.Round(255 * (1 - fadedTo))) };
            canvas.DrawRect(0, 0, 2560, 1440, fade);
        }
        using var png = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

#if DEVTOOLS
    // A developer script's "exits" step (tools\qa\extract-roll-call.devscript): the rows go the way a screenshot's reading
    // goes once text recognition hands it over, matched to the map's exits for the side, taken as read now (the window
    // calls a list out only when it is from a screenshot taken in the last 30 s).
    [Fact]
    public async Task A_scripts_list_is_taken_as_a_screenshot_taken_now_would_be()
    {
        await using var rig = await RaidOnCustoms(_ => null);
        var asked = DateTime.Now;
        Assert.Null(await rig.Session.DevReadExitsAsync([("Trailer Park", true), ("ZB-1011", false)]));
        var s = await rig.Until(s => s.ExitsReadAt is not null, "the list read");
        Assert.InRange(s.ExitsReadAt!.Value, asked, DateTime.Now);
        Assert.Equal(ExitState.Unsure, StateOf(s, "Trailer Park"));
        Assert.Equal(ExitState.Listed, StateOf(s, "ZB-1011"));
        Assert.Equal(ExitState.NotListed, StateOf(s, "Crossroads"));
        Assert.DoesNotContain(rig.Notices, n => n.Contains("extract", StringComparison.OrdinalIgnoreCase));

        // "all": every extract of the side, none marked; the Scav's isn't the PMC's.
        Assert.Null(await rig.Session.DevReadExitsAsync(null));
        s = await rig.Until(s => StateOf(s, "Crossroads") == ExitState.Listed, "every extract listed");
        Assert.Equal(ExitState.Listed, StateOf(s, "Trailer Park"));
        Assert.Equal(ExitState.NotChecked, StateOf(s, "Transit to another map"));

        // Out of a raid there is no list to read.
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
        await rig.Until(s => s.Raid.Phase == RaidPhase.Menu, "the raid over");
        Assert.NotNull(await rig.Session.DevReadExitsAsync(null));
        Assert.Null(rig.Snapshot.ExitsReadAt);
    }
#endif

    [Fact]
    public void A_sides_exits_are_the_ones_a_row_can_name()
    {
        var data = Data(GameMode.Pve);
        Assert.Equal(["extract:far", "extract:mid", "extract:near"], GameSession.ExitNames(data, "map-customs", RaidSide.Pmc).Select(e => e.Id).Order());
        Assert.Equal(["extract:scav"], GameSession.ExitNames(data, "map-customs", RaidSide.Scav).Select(e => e.Id));
        Assert.Empty(GameSession.ExitNames(data, "no-such-map", RaidSide.Pmc));
    }
}
