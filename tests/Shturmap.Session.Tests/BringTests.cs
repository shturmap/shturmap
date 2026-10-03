using Shturmap.Core;
using Shturmap.Core.Logs;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// BRING also lists what kills and exits take: the weapons and mods a kill objective names, and the items the exit a
// quest names asks for; gear a kill forbids is a note, not a row (owner, 2026-10-03: "it should be a coherent design so
// it is still clear what to bring and what is needed to solve a quest").
public class BringTests
{
    // Item categories as tarkov.dev gives them: most specific first.
    private const string Sniper = "cat-sniper", Shotgun = "cat-shotgun", Handgun = "cat-handgun", Revolver = "cat-revolver",
        Armor = "cat-armor", Headwear = "cat-headwear", Weapon = "cat-weapon";

    private static readonly Dictionary<string, string> Names = new()
    {
        [Sniper] = "Sniper rifle", [Shotgun] = "Shotgun", [Handgun] = "Handgun", [Revolver] = "Revolver",
        [Armor] = "Armor", [Headwear] = "Headwear", [Weapon] = "Weapon",
        ["mosin"] = "Mosin rifle", ["sv98"] = "SV-98 rifle", ["m700"] = "M700 rifle", ["mp18"] = "MP-18 rifle",
        ["ak12"] = "AK-12 rifle", ["suppressor"] = "AK-12 suppressor", ["scope"] = "PS-320 scope",
        [ExtractRules.RedFlare] = "Red flare", [ExtractRules.IcePick] = "Ice pick", [ExtractRules.Paracord] = "Paracord",
        ["5449016a4bdc2d6f028b456f"] = "Roubles", ["paca"] = "PACA armor", ["ssh"] = "SSh-68 helmet",
        ["r1"] = "Rhino revolver", ["r2"] = "RSh-12 revolver", ["r3"] = "MTs-255 revolver",
    };

    private static ApiItem Item(string id, params string[] categories) => new(id, [], null, null, [], [.. categories]);

    private static ItemSources Sources(params ApiItem[] items) => new()
    {
        Items = items.ToDictionary(i => i.Id),
        Barters = Array.Empty<ApiBarter>().ToLookup(b => ""),
        Crafts = Array.Empty<ApiCraft>().ToLookup(c => ""),
        Stations = new Dictionary<string, string>(),
    };

    // Three sniper rifles, a single-shot rifle the data files as a shotgun, three shotguns, two handguns, five revolvers.
    private static readonly ItemSources Catalogue = Sources(
        Item("mosin", Sniper, Weapon), Item("sv98", Sniper, Weapon), Item("m700", Sniper, Weapon),
        Item("mp18", Shotgun, Weapon), Item("sg1", Shotgun, Weapon), Item("sg2", Shotgun, Weapon), Item("sg3", Shotgun, Weapon),
        Item("pm", Handgun, Weapon), Item("m9", Handgun, Weapon),
        Item("r1", Revolver, Weapon), Item("r2", Revolver, Weapon), Item("r3", Revolver, Weapon), Item("r4", Revolver, Weapon), Item("r5", Revolver, Weapon),
        Item("paca", Armor), Item("ssh", Headwear),
        // A preset repeats the weapon it builds; it doesn't make the class bigger.
        new ApiItem("mosin-preset", ["preset"], null, null, [], [Sniper, Weapon]));

    private static GameData Data(Dictionary<string, ApiMap>? maps = null, Dictionary<string, ApiTask>? tasks = null,
        Dictionary<string, string>? extractKeys = null, Dictionary<string, ObjectiveFacts>? facts = null) => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = maps ?? new Dictionary<string, ApiMap>(),
        Tasks = tasks ?? new Dictionary<string, ApiTask>(),
        Traders = new Dictionary<string, ApiTrader>(),
        ItemNames = Names,
        ExtractKeys = extractKeys ?? new Dictionary<string, string>(),
        ObjectiveFacts = facts ?? new Dictionary<string, ObjectiveFacts>(),
        MapDefinitions = [],
        CheckedAt = DateTimeOffset.Now,
    };

    // ---- naming a weapon class ----

    [Fact]
    public void A_whole_category_is_any_of_it_and_presets_dont_count() =>
        Assert.Equal("Any sniper rifle", Planning.WeaponText(Data(), Catalogue, ["mosin", "sv98", "m700"]));

    [Fact]
    public void Most_of_a_category_says_how_many_kinds() =>
        Assert.Equal("Shotgun (3 of 4 kinds)", Planning.WeaponText(Data(), Catalogue, ["sg1", "sg2", "sg3"]));

    [Fact]
    public void A_class_and_a_stray_weapon_are_named_both() =>
        Assert.Equal("Any sniper rifle or MP-18 rifle", Planning.WeaponText(Data(), Catalogue, ["mosin", "sv98", "m700", "mp18"]));

    [Fact]
    public void Several_categories_are_named_by_category_when_naming_each_weapon_takes_too_many_parts() =>
        Assert.Equal("Any handgun or revolver (3 of 5 kinds)", Planning.WeaponText(Data(), Catalogue, ["pm", "m9", "r1", "r2", "r3"]));

    [Fact]
    public void A_few_weapons_of_one_category_stay_a_gun_to_name() =>
        Assert.Equal("Rhino revolver or 2 others", Planning.WeaponText(Data(), Catalogue, ["r1", "r2", "r3"]));

    [Fact]
    public void Without_the_item_categories_the_weapons_are_listed() =>
        Assert.Equal("Mosin rifle or 2 others", Planning.WeaponText(Data(), null, ["mosin", "sv98", "m700"]));

    // ---- rows: one per thing, with every quest it serves and what for ----

    private static readonly PlanMap Customs = new("customs", "Customs", new HashSet<string> { "customs" }, 35);

    private static PlanQuest Quest(string id, PlanObjective objective) => new(id, id, [objective], new Dictionary<string, IReadOnlyList<string>>());

    private static PlanObjective Kill(string id, IReadOnlyList<string>? weapons = null, IReadOnlyList<IReadOnlyList<string>>? mods = null,
        IReadOnlyList<string>? without = null) =>
        new(id, ObjectiveKind.Elimination, ["customs"], new Dictionary<string, IReadOnlyList<WorldPoint>>(), 1, false, [], [],
            Type: "shoot", Targets: ["Savage"], Weapons: weapons, Mods: mods, NotWearing: without);

    [Fact]
    public void The_same_weapons_for_two_quests_are_one_row_naming_both()
    {
        var plan = RaidPlanner.Plan([Quest("A", Kill("a1", ["mosin", "sv98"])), Quest("B", Kill("b1", ["sv98", "mosin"]))], Customs);
        var row = Assert.Single(plan.Requirements);
        Assert.Equal(RequirementKind.Weapon, row.Kind);
        Assert.Equal(["A", "B"], row.ForQuests.Order());
    }

    [Fact]
    public void Each_kind_of_row_says_what_it_is_for()
    {
        var tasks = new[] { "Wet Job", "Patriot", "Cease Fire" }.ToDictionary(n => n, n => Task(n, []));
        var data = Data(tasks: tasks);
        Assert.Equal("to use, for Wet Job", Planning.RequirementText(data, new Requirement(RequirementKind.Weapon, ["ak12"], 1, ["Wet Job"])).Why);
        Assert.Equal("to fit, for Patriot", Planning.RequirementText(data, new Requirement(RequirementKind.WeaponMods, ["scope", "suppressor"], 1, ["Patriot"])).Why);
        Assert.Equal("PS-320 scope / AK-12 suppressor", Planning.RequirementText(data, new Requirement(RequirementKind.WeaponMods, ["scope", "suppressor"], 1, ["Patriot"])).Text);
        var exit = Planning.RequirementText(data, new Requirement(RequirementKind.Exit, [ExtractRules.RedFlare], 1, ["Cease Fire"], "Klimov Street (Flare)"));
        Assert.Equal("to leave through Klimov Street (Flare), for Cease Fire", exit.Why);
        Assert.Equal("Red flare", exit.Text);
    }

    [Fact]
    public void Forbidden_gear_is_no_row()
    {
        var plan = RaidPlanner.Plan([Quest("Swift", Kill("s1", without: ["paca", "ssh"]))], Customs);
        Assert.Empty(plan.Requirements);
    }

    // ---- what an exit takes ----

    private static ApiExtract Extract(string id, string name, ApiCount? transfer = null) => new(id, name, "pmc", null, null, null, null, null, transfer);

    private static ApiMap Map(string id, params ApiExtract[] extracts) =>
        new(id, id, id, id, null, null, 40, [.. extracts], null, null, null, null);

    private static ApiTask Task(string id, List<ApiObjective> objectives) =>
        new(id, id, null, null, null, null, false, false, null, null, null, false, null, objectives, null);

    private static ApiObjective Objective(string id, string type, List<string>? maps = null, List<ApiItemRef>? notWearing = null,
        List<string>? weapons = null) =>
        new(id, type, id, false, maps ?? ["streets"], null, null, 1, null, null, null, null, null, false, null, weapons, null, notWearing);

    [Fact]
    public void A_flare_exit_takes_a_red_flare_a_climb_its_gear_and_a_paid_exit_its_price()
    {
        var keys = new Dictionary<string, string> { ["e1"] = "E9_sniper", ["e2"] = "Alpinist_exit", ["e3"] = "Taxi" };
        var data = Data(extractKeys: keys);
        Assert.Equal([(ExtractRules.RedFlare, 1)], ExtractRules.Items(data, Extract("e1", "Klimov Street (Flare)")));
        Assert.Equal([(ExtractRules.IcePick, 1), (ExtractRules.Paracord, 1)], ExtractRules.Items(data, Extract("e2", "Cliff Descent")));
        Assert.Equal([("5449016a4bdc2d6f028b456f", 5000)], ExtractRules.Items(data, Extract("e3", "Taxi V-Ex", new ApiCount("5449016a4bdc2d6f028b456f", 5000))));
        Assert.Empty(ExtractRules.Items(data, Extract("e4", "Gate")));
    }

    [Fact]
    public void A_quest_that_names_a_flare_exit_brings_the_flare()
    {
        var objective = Objective("o1", "extract");
        var task = Task("Cease Fire", [objective]);
        var data = Data(
            maps: new Dictionary<string, ApiMap> { ["streets"] = Map("streets", Extract("e1", "Klimov Street (Flare)")) },
            tasks: new Dictionary<string, ApiTask> { [task.Id] = task },
            extractKeys: new Dictionary<string, string> { ["e1"] = "E9_sniper" },
            facts: new Dictionary<string, ObjectiveFacts> { ["o1"] = new([], ["ExpBonusSurvived"], [], "E9_sniper") });
        var plan = Planning.ToPlan(task, data).Objectives.Single();
        Assert.Equal("Klimov Street (Flare)", plan.Exit);
        Assert.Equal([(ExtractRules.RedFlare, 1)], plan.ExitItems);
        var streets = new PlanMap("streets", "Streets", new HashSet<string> { "streets" }, 50);
        var row = Assert.Single(RaidPlanner.Plan([Planning.ToPlan(task, data)], streets).Requirements);
        Assert.Equal(RequirementKind.Exit, row.Kind);
        Assert.Equal("Klimov Street (Flare)", row.Exit);
    }

    // ---- the objective line: what it takes, and what it forbids ----

    [Fact]
    public void The_objective_line_names_the_weapon_and_the_gear_to_leave_behind()
    {
        var objective = Objective("o1", "shoot", notWearing: [new("paca", null), new("ssh", null)], weapons: ["mosin", "sv98", "m700"]);
        var task = Task("Swift", [objective]);
        var data = Data(tasks: new Dictionary<string, ApiTask> { [task.Id] = task });
        var line = Planning.Needs(data, task, objective, new HashSet<string> { "streets" }, false, Catalogue);
        Assert.Equal("Use: Any sniper rifle · Without: armor, headwear", line);
        // Before the item categories are loaded, the items themselves.
        Assert.Equal("Use: Mosin rifle or 2 others · Without: PACA armor / SSh-68 helmet", Planning.Needs(data, task, objective, new HashSet<string> { "streets" }, false));
    }

    // ---- what it takes to enter the map ----

    [Fact]
    public void A_map_s_entry_item_comes_first_with_no_quest()
    {
        // The Lab can't be entered without its keycard, quests or not (owner, 2026-10-03, from the map audit).
        var lab = new PlanMap("lab", "The Lab", new HashSet<string> { "lab" }, 40, ["keycard", "keycard"]);
        var row = Assert.Single(RaidPlanner.Plan([], lab).Requirements);
        Assert.Equal(RequirementKind.Entry, row.Kind);
        Assert.Equal("The Lab", row.Enter);
        Assert.Empty(row.ForQuests);
        var view = Planning.RequirementText(Data(), row);
        Assert.Equal("to enter The Lab", view.Why);
        // Before what the picks need, and before everything else.
        var key = new RequirementView(RequirementKind.Key, "Key", "A", "key1", ["A"]);
        var other = new RequirementView(RequirementKind.Bring, "Flare", "B", "flare", ["B"]);
        var rows = Planning.BringOrder([key, other, view], new HashSet<string> { "A" });
        Assert.Equal([RequirementKind.Entry, RequirementKind.Key, RequirementKind.Bring], rows.Select(r => r.Row.Kind));
        Assert.False(rows[0].ForPicks);
        Assert.True(rows[2].StartsOthers);
    }
}
