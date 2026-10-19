using Shturmap.Core.Logs;
using Shturmap.Core.Planning;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Session.Tests;

// A required weapon is pictured as the assembled gun (owner, 2026-10-19: "When a weapon is required for a quest, it
// shows just the icon for the weapon … barely recognisable as a weapon"; from a panel of five, A): every cell that
// pictures a gun shows its default preset's icon (ItemSources.PictureId), the quest card's and the item card's as BRING's;
// the cell's item, its card and its words stay the gun's. Made-up ids.
public class WeaponPictureTests
{
    private const string Gun = "gun-1", Preset = "gun-1-preset", Knife = "knife-1", Salewa = "salewa-1";

    private static readonly Dictionary<string, string> Names = new()
    {
        [Gun] = "AKS-74UN rifle", [Preset] = "AKS-74UN rifle Default", [Knife] = "6Kh5 bayonet", [Salewa] = "Salewa first aid kit",
    };

    private static GameData Data(params ApiTask[] tasks) => new()
    {
        Mode = GameMode.Pve,
        Language = "en",
        Maps = new Dictionary<string, ApiMap> { ["customs"] = new("customs", "Customs", "customs", "customs", null, null, 40, null, null, null, null, null) },
        Tasks = tasks.ToDictionary(t => t.Id),
        Traders = new Dictionary<string, ApiTrader>(),
        ItemNames = Names,
        MapDefinitions = [],
        CheckedAt = DateTimeOffset.Now,
    };

    private static readonly ItemSources Sources = new()
    {
        Items = new ApiItem[]
        {
            new(Gun, ["gun"], null, null, [], ["cat-ar", "cat-weapon"], new ApiItemProperties(Preset)),
            new(Preset, ["preset"], null, null, [], ["cat-ar", "cat-weapon"]),
            new(Knife, [], null, null, [], ["cat-knife", "cat-weapon"], new ApiItemProperties()),
            new(Salewa, [], null, null, []),
        }.ToDictionary(i => i.Id),
        Barters = Array.Empty<ApiBarter>().ToLookup(b => ""),
        Crafts = Array.Empty<ApiCraft>().ToLookup(c => ""),
        Stations = new Dictionary<string, string>(),
    };

    private static ApiTask Task(string id, params ApiObjective[] objectives) =>
        new(id, id, null, null, null, null, false, false, null, null, null, false, null, [.. objectives], null);

    private static ApiObjective Kill(string id, params string[] weapons) =>
        new(id, "shoot", id, false, ["customs"], null, null, 5, null, null, null, null, null, false, UsingWeapon: [.. weapons]);

    private static ApiObjective Plant(string id, string item) =>
        new(id, "plantItem", id, false, ["customs"], null, null, 1, null, [item], null, null, null, false);

    private static Dictionary<string, QuestStatus> Active(string id) =>
        new() { [id] = new QuestStatus(id, QuestState.Active, ObservationSource.Log, DateTime.Now) };

    private static readonly ApiTask Punisher = Task("Punisher", Kill("p1", Gun, Knife));

    [Fact]
    public void The_quest_cards_cells_picture_a_gun_by_its_preset_and_stay_the_gun()
    {
        var card = QuestCards.Build(Data(Punisher), Active("Punisher"), "Punisher", sources: Sources)!;
        var need = Assert.Single(card.Needs);
        Assert.Equal(RequirementKind.Weapon, need.Kind);
        Assert.Equal(Gun, need.ItemId);
        Assert.Equal(Preset, need.PictureId);
        Assert.Equal([Gun, Knife], need.Alternatives);
        var objective = Assert.Single(card.Objectives);
        Assert.Equal(Gun, objective.ItemId);
        Assert.Equal(Preset, objective.PictureId);
    }

    [Fact]
    public void Until_the_sources_load_and_for_anything_else_a_cell_pictures_its_own_item()
    {
        var waiting = QuestCards.Build(Data(Punisher), Active("Punisher"), "Punisher")!;
        Assert.Equal(Gun, Assert.Single(waiting.Needs).PictureId);
        Assert.Equal(Gun, Assert.Single(waiting.Objectives).PictureId);
        // A weapon without a preset (a knife) and an item that is no weapon: their own icons.
        var bayonet = Task("Bayonet", Kill("b1", Knife));
        Assert.Equal(Knife, Assert.Single(QuestCards.Build(Data(bayonet), Active("Bayonet"), "Bayonet", sources: Sources)!.Needs).PictureId);
        var plant = Task("Plant", Plant("s1", Salewa));
        var card = QuestCards.Build(Data(plant), Active("Plant"), "Plant", sources: Sources)!;
        Assert.Equal(Salewa, Assert.Single(card.Needs).PictureId);
        Assert.Equal(Salewa, Assert.Single(card.Objectives).PictureId);
    }

    [Fact]
    public void The_item_card_pictures_a_gun_as_its_cell_does_and_is_still_the_guns()
    {
        var card = ItemCards.Build(Data(Punisher), Sources, Active("Punisher"), Gun);
        Assert.Equal(Gun, card.ItemId);
        Assert.Equal("AKS-74UN rifle", card.Name);
        Assert.Equal(Preset, card.PictureId);
        Assert.Equal(["Punisher"], card.QuestIds);
        Assert.Equal(Gun, ItemCards.Build(Data(Punisher), null, Active("Punisher"), Gun).PictureId);
    }
}
