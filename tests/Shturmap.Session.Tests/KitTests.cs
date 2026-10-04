using Shturmap.Core.Logs;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Core.Raid;

namespace Shturmap.Session.Tests;

// The kit reminder while a raid loads (owner, 2026-10-03: "it could still be a good reminder what to bring … with icon
// previews"; "If the player sees they forgot something they can still cancel loading into the map").
public class KitTests
{
    private static RequirementView Row(RequirementKind kind, string item, params string[] quests) =>
        new(kind, item, string.Join(", ", quests), item, quests, "for " + string.Join(", ", quests));

    private static PlanQuestView Quest(string id) => new(id, id, ObjectiveKind.Place);

    private static MapPlanView Plan(IReadOnlyList<RequirementView> rows, params string[] quests) =>
        new("streets-of-tarkov", "Streets of Tarkov", quests.Select(Quest).ToList(), [], rows, 40, []);

    private static readonly RequirementView Keycard = Row(RequirementKind.Entry, "keycard");
    private static readonly RequirementView Flare = Row(RequirementKind.Exit, "flare", "cease");
    private static readonly RequirementView Marker = Row(RequirementKind.Bring, "marker", "revision");
    private static readonly RequirementView Beanie = Row(RequirementKind.Wear, "beanie", "dandies");
    private static readonly RequirementView Key = Row(RequirementKind.Key, "key", "ballet");

    [Fact]
    public void Getting_in_and_out_comes_first_then_everything_without_picks()
    {
        var kit = Planning.Kit(Plan([Marker, Flare, Beanie, Keycard], "revision", "cease", "dandies"), new HashSet<string>());
        Assert.Equal(["keycard", "flare", "marker", "beanie"], kit.Main.Select(r => r.ItemId));
        Assert.Empty(kit.More);
    }

    [Fact]
    public void With_picks_here_their_items_come_next_and_the_rest_is_also_useful()
    {
        var kit = Planning.Kit(Plan([Marker, Key, Flare, Beanie], "revision", "ballet", "cease", "dandies"), new HashSet<string> { "dandies", "ballet" });
        // The exit item comes first although its quest isn't picked: without it there is no way out there.
        Assert.Equal(["flare", "key", "beanie"], kit.Main.Select(r => r.ItemId));
        Assert.Equal(["marker"], kit.More.Select(r => r.ItemId));
    }

    [Fact]
    public void Picks_on_another_map_dont_split_this_one()
    {
        var kit = Planning.Kit(Plan([Marker, Key], "revision", "ballet"), new HashSet<string> { "some-quest-on-customs" });
        Assert.Equal(["marker", "key"], kit.Main.Select(r => r.ItemId));
        Assert.Empty(kit.More);
    }

    [Fact]
    public void The_cue_pictures_three_rows_of_eight_and_counts_the_rest()
    {
        // Owner, 2026-10-04: many items "get quickly hidden behind a +x mark while we still have plenty of screen
        // space". Nine of them all show; only past twenty-four the rest is counted.
        var nine = Enumerable.Range(1, 9).Select(i => Row(RequirementKind.Bring, "item" + i, "q")).ToList();
        var (all, noMore) = Planning.CueKit(Planning.Kit(Plan(nine, "q"), new HashSet<string>()));
        Assert.Equal(9, all.Count);
        Assert.Equal(0, noMore);

        var rows = Enumerable.Range(1, 27).Select(i => Row(RequirementKind.Bring, "item" + i, "q")).ToList();
        var (shown, more) = Planning.CueKit(Planning.Kit(Plan(rows, "q"), new HashSet<string>()));
        Assert.Equal(24, shown.Count);
        Assert.Equal("item1", shown[0].ItemId);
        Assert.Equal(3, more);

        var (few, none) = Planning.CueKit(Planning.Kit(Plan([Marker, Key], "revision", "ballet"), new HashSet<string>()));
        Assert.Equal(2, few.Count);
        Assert.Equal(0, none);
    }

    [Fact]
    public void The_cue_pictures_what_the_picks_need_first()
    {
        // Owner, 2026-10-04: "it should also show color coded the icons first of the quests we highlighted".
        var picks = new HashSet<string> { "dandies", "ballet" };
        var kit = Planning.Kit(Plan([Marker, Key, Flare, Beanie, Keycard], "revision", "ballet", "cease", "dandies"), picks);
        var (shown, _) = Planning.CueKit(kit, picks: picks);
        // The picks' two, in the kit's order; then the rest, in and out first as in the kit.
        Assert.Equal(["key", "beanie", "keycard", "flare", "marker"], shown.Select(r => r.ItemId));
        Assert.Equal([true, true, false, false, false], shown.Select(r => Planning.ForPick(r, picks)));

        // Without picks nothing moves and nothing is marked.
        var (plain, _) = Planning.CueKit(kit);
        Assert.Equal(["keycard", "flare", "key", "beanie", "marker"], plain.Select(r => r.ItemId));
        Assert.All(plain, r => Assert.False(Planning.ForPick(r, null)));
    }

    [Fact]
    public void Only_while_loading_and_never_for_a_scav()
    {
        var plan = Plan([Marker], "revision");
        var picks = new HashSet<string>();
        Assert.Empty(Planning.KitWhileLoading(new RaidState { Phase = RaidPhase.Menu }, plan, picks).All);
        Assert.Single(Planning.KitWhileLoading(new RaidState { Phase = RaidPhase.Loading }, plan, picks).All);
        Assert.Single(Planning.KitWhileLoading(new RaidState { Phase = RaidPhase.Loading, Side = RaidSide.Pmc }, plan, picks).All);
        Assert.Empty(Planning.KitWhileLoading(new RaidState { Phase = RaidPhase.Loading, Side = RaidSide.Scav }, plan, picks).All);
        Assert.Empty(Planning.KitWhileLoading(new RaidState { Phase = RaidPhase.InRaid }, plan, picks).All);
        Assert.Empty(Planning.KitWhileLoading(new RaidState { Phase = RaidPhase.Loading }, null, picks).All);
    }

    [Fact]
    public void A_cancelled_load_clears_it_and_a_setup_names_the_side_while_loading()
    {
        var at = new DateTime(2026, 10, 3, 20, 0, 0);
        var plan = Plan([Marker], "revision");
        var picks = new HashSet<string>();
        var tracker = new RaidTracker();
        tracker.Apply(new ProfileLoadedEvent(at, "pmc"));
        tracker.Apply(new MapLoadingEvent(at.AddSeconds(1), "maps/city_preset.bundle", null));
        Assert.Single(Planning.KitWhileLoading(tracker.State, plan, picks).All);

        // A server-hosted raid's setup names the joining profile: another one than the menu's is the Scav.
        tracker.Apply(new MatchSetupEvent(at.AddSeconds(20), "TarkovStreets", "ABC123", "scav"));
        Assert.Equal(RaidSide.Scav, tracker.State.Side);
        Assert.Empty(Planning.KitWhileLoading(tracker.State, plan, picks).All);

        tracker.Apply(new MatchingCancelledEvent(at.AddSeconds(25)));
        Assert.Equal(RaidPhase.Menu, tracker.State.Phase);
        Assert.Empty(Planning.KitWhileLoading(tracker.State, plan, picks).All);

        tracker.Apply(new MapLoadingEvent(at.AddSeconds(60), "maps/city_preset.bundle", null));
        tracker.Apply(new MatchSetupEvent(at.AddSeconds(80), "TarkovStreets", "DEF456", "pmc"));
        Assert.Equal(RaidSide.Pmc, tracker.State.Side);
        Assert.Single(Planning.KitWhileLoading(tracker.State, plan, picks).All);
    }

    [Fact]
    public void The_long_bring_notice_is_gone()
    {
        // The cue's pictures and the raid card's CHECK YOUR KIT replace "Loading … · bring: …" and its group-pick twin.
        var session = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Shturmap.Session", "GameSession.cs"));
        Assert.DoesNotContain("· bring: {", session);
        Assert.DoesNotContain("Your group picked {", session);
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
