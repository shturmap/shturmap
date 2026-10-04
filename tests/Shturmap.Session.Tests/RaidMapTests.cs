using Shturmap.Core.Logs;
using Shturmap.Core.Maps;
using Shturmap.Core.Raid;

namespace Shturmap.Session.Tests;

// The raid's own map is not the map on screen (review of 2026-10-04, A1): a map picked in the MAP list during a raid
// is a look, and positions, the rail and the status words stay on the raid's map.
public class RaidMapTests
{
    private static readonly MapIdentity Customs = new("map-customs", "customs", "bigmap", "maps/customs_preset.bundle", "Customs");
    private static readonly MapIdentity Woods = new("map-woods", "woods", "Woods", "maps/woods_preset.bundle", "Woods");

    // ---- where a position goes ----

    [Theory]
    [InlineData(RaidPhase.InRaid)]
    [InlineData(RaidPhase.Loading)]
    public void In_a_raid_a_position_is_on_the_raids_map_whatever_map_is_shown(RaidPhase phase)
    {
        var place = GameSession.PlaceFix(phase, Customs, logsFollowed: true, shownMap: Woods, onShownMap: true, fromEndedRaid: false);
        Assert.Same(Customs, place.Map);
        Assert.Null(place.Why);
    }

    [Fact]
    public void A_raid_on_a_map_the_data_does_not_know_plots_nothing_and_says_so()
    {
        // The shown map is never taken for the raid's, even when the position would lie on it.
        var place = GameSession.PlaceFix(RaidPhase.InRaid, raidMap: null, logsFollowed: true, shownMap: Woods, onShownMap: true, fromEndedRaid: false);
        Assert.Null(place.Map);
        Assert.Contains("can't tell which map this raid is on", place.Why);
    }

    [Fact]
    public void Out_of_a_raid_there_is_no_you_while_the_logs_are_followed()
    {
        var place = GameSession.PlaceFix(RaidPhase.Menu, null, logsFollowed: true, shownMap: Customs, onShownMap: true, fromEndedRaid: false);
        Assert.Null(place.Map);
        Assert.Contains("the game's log shows no raid", place.Why);

        // Taken just before the raid's end line reached the log: not shown either, and nothing to say about it.
        var late = GameSession.PlaceFix(RaidPhase.Menu, null, logsFollowed: true, shownMap: Customs, onShownMap: true, fromEndedRaid: true);
        Assert.Null(late.Map);
        Assert.Null(late.Why);
    }

    [Fact]
    public void Only_without_any_game_logs_does_the_shown_map_take_a_position_that_lies_on_it()
    {
        Assert.Same(Customs, GameSession.PlaceFix(RaidPhase.Menu, null, logsFollowed: false, shownMap: Customs, onShownMap: true, fromEndedRaid: false).Map);
        var off = GameSession.PlaceFix(RaidPhase.Menu, null, logsFollowed: false, shownMap: Customs, onShownMap: false, fromEndedRaid: false);
        Assert.Null(off.Map);
        Assert.Contains("Pick the map", off.Why);
        Assert.Null(GameSession.PlaceFix(RaidPhase.Menu, null, logsFollowed: false, shownMap: null, onShownMap: false, fromEndedRaid: false).Map);
    }

    // ---- the status words ----

    [Fact]
    public void A_raid_whose_map_is_unknown_names_no_map()
    {
        var now = new DateTime(2026, 10, 4, 22, 0, 0);
        var raid = new RaidState { Phase = RaidPhase.InRaid, Side = RaidSide.Pmc, RaidStartedAt = now.AddMinutes(-12) };
        Assert.Equal("In raid · PMC · 12 min", RaidStatus.Text(raid, null, now));
        Assert.Equal("In raid", RaidStatus.Text(new RaidState { Phase = RaidPhase.InRaid }, null, now));
        Assert.Equal("Loading", RaidStatus.Text(new RaidState { Phase = RaidPhase.Loading }, null, now));
    }

    [Fact]
    public void The_status_words_name_the_raids_map_not_the_one_looked_at()
    {
        var now = new DateTime(2026, 10, 4, 22, 0, 0);
        var snapshot = new SessionSnapshot
        {
            Raid = new RaidState { Phase = RaidPhase.InRaid, Side = RaidSide.Pmc, RaidStartedAt = now.AddMinutes(-5) },
            Map = Woods,
            RaidMap = Customs,
        };
        Assert.True(snapshot.LooksAtAnotherMap);
        Assert.False(snapshot.RaidMapUnknown);
        Assert.Equal("In raid · Customs · PMC · 5 min", RaidStatus.Text(snapshot, now));

        var unknown = snapshot with { RaidMap = null };
        Assert.True(unknown.RaidMapUnknown);
        Assert.False(unknown.LooksAtAnotherMap);
        Assert.Equal("In raid · PMC · 5 min", RaidStatus.Text(unknown, now));

        var menu = new SessionSnapshot { Map = Woods };
        Assert.False(menu.LooksAtAnotherMap);
        Assert.False(menu.RaidMapUnknown);
    }

    // ---- a whole session ----

    [Fact]
    public async Task A_map_picked_during_a_raid_is_a_look_and_the_next_position_brings_the_raids_map_back()
    {
        await using var rig = new SessionRig();
        rig.RaidUpToItsStart("maps/customs_preset.bundle", "bigmap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        var raid = await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.RaidMap?.NormalizedName == "customs", "the raid on Customs");
        Assert.Equal("customs", raid.Map?.NormalizedName);
        Assert.NotNull(raid.RaidInfo);

        // A look at Woods: the map on screen changes, the raid doesn't.
        await rig.Session.SelectMapAsync("woods");
        var looking = rig.Snapshot;
        Assert.Equal("woods", looking.Map?.NormalizedName);
        Assert.Equal("customs", looking.RaidMap?.NormalizedName);
        Assert.True(looking.LooksAtAnotherMap);
        Assert.StartsWith("In raid · Customs · PMC", RaidStatus.Text(looking, DateTime.Now));
        Assert.Equal(SessionRig.CustomsMinutes, looking.RaidInfo?.RaidMinutes);
        Assert.Equal("customs", looking.MapPlan?.NormalizedName);

        // The position is the raid's: plotted on Customs, which comes back on screen.
        rig.Screenshot(12, 1, 34);
        var fixedOn = await rig.Until(s => s.RaidFix is not null, "the position");
        Assert.Equal("customs", fixedOn.Map?.NormalizedName);
        Assert.False(fixedOn.LooksAtAnotherMap);
        Assert.Equal(12, fixedOn.Fix?.Position.X);

        // Looking away again keeps the position for the rail, but draws no "you" on the other map.
        await rig.Session.SelectMapAsync("woods");
        Assert.Null(rig.Snapshot.Fix);
        Assert.Empty(rig.Snapshot.Trail);
        Assert.Equal(12, rig.Snapshot.RaidFix?.Position.X);

        // The raid ends: its cue and the last-raid line name Customs, though Woods is on screen.
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
        var over = await rig.Until(s => s.Raid.Phase == RaidPhase.Menu, "the raid's end");
        Assert.Null(over.RaidMap);
        Assert.Null(over.RaidFix);
        Assert.Equal("Customs", over.LastRaid?.MapName);
        await rig.Until(() => rig.Cues.Any(c => c.Kind == CueKind.RaidOver), "the RAID OVER cue");
        Assert.Equal("Customs", rig.Cues.Last(c => c.Kind == CueKind.RaidOver).MapName);
    }

    [Fact]
    public async Task A_raid_on_a_map_the_data_does_not_know_takes_no_map_for_its_own()
    {
        await using var rig = new SessionRig();
        rig.RaidUpToItsStart("maps/newmap_preset.bundle", "NewMap", DateTime.Now.AddMinutes(-5));
        await rig.StartAsync();
        var raid = await rig.Until(s => s.Raid.Phase == RaidPhase.InRaid && s.Data is not null && s.Map is not null, "the raid");
        Assert.Null(raid.RaidMap);
        Assert.True(raid.RaidMapUnknown);
        Assert.StartsWith("In raid · PMC", RaidStatus.Text(raid, DateTime.Now));
        Assert.Null(raid.RaidInfo);
        Assert.Null(raid.MapPlan);

        rig.Screenshot(12, 1, 34);
        await rig.Until(() => rig.Notices.Any(n => n.Contains("can't tell which map this raid is on", StringComparison.Ordinal)), "the notice");
        Assert.Null(rig.Snapshot.Fix);
        Assert.Null(rig.Snapshot.RaidFix);
    }

    [Fact]
    public async Task A_map_named_only_by_its_location_gets_its_loading_cue_when_that_line_comes()
    {
        await using var rig = new SessionRig();
        rig.Log("Session mode: Pve");
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
        await rig.StartAsync();
        // The mode shows that the lines above, the replay, have been read: what follows is live.
        await rig.Until(s => s.Data is not null && s.Map is not null && s.Raid.Mode == GameMode.Pve, "the data and the replay");

        // The scene line names a scene the data has no map for: no cue yet, and the shown map isn't taken for the raid's.
        rig.Log("scene preset path:maps/sandbox_high_preset.bundle rcid:sandbox_high.scenespreset.asset");
        var loading = await rig.Until(s => s.Raid.Phase == RaidPhase.Loading, "loading");
        Assert.Null(loading.RaidMap);
        Assert.Equal("Loading", RaidStatus.Text(loading, DateTime.Now));
        Assert.DoesNotContain(rig.Cues, c => c.Kind == CueKind.RaidLoading);

        // The match setup names the location: the raid's map, and the cue, come now.
        rig.Log("TRACE-NetworkGameCreate profileStatus: 'Profileid: 000000000000000000000003, Status: Busy, RaidMode: Online, Location: Sandbox_high, shortId: TEST02'");
        var named = await rig.Until(s => s.RaidMap is not null, "the raid's map");
        Assert.Equal("ground-zero-21", named.RaidMap?.NormalizedName);
        Assert.Equal("ground-zero-21", named.Map?.NormalizedName);
        await rig.Until(() => rig.Cues.Any(c => c.Kind == CueKind.RaidLoading), "the RAID LOADING cue");
        Assert.Equal("Ground Zero 21+", rig.Cues.Single(c => c.Kind == CueKind.RaidLoading).MapName);
    }

    [Fact]
    public async Task In_the_menus_a_position_is_not_shown_while_the_logs_are_followed()
    {
        await using var rig = new SessionRig();
        rig.Log("Session mode: Pve");
        rig.Log("PrepareSelectedProfileLocally ProfileId:000000000000000000000003 AccountId:0");
        await rig.StartAsync();
        await rig.Until(s => s.Data is not null && s.Map is not null, "the data");

        rig.Screenshot(12, 1, 34); // within the shown map's bounds: it used to be plotted there
        await rig.Until(() => rig.Notices.Any(n => n.Contains("the game's log shows no raid", StringComparison.Ordinal)), "the notice");
        Assert.Null(rig.Snapshot.Fix);
        Assert.Null(rig.Snapshot.RaidFix);
    }
}
