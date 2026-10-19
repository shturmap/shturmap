using System.Text.Json;
using Shturmap.Data.TarkovDev;

namespace Shturmap.Data.Tests;

// A required weapon is pictured as the assembled gun (owner, 2026-10-19: from a panel of five, A): a gun's item in
// tarkov.dev's items payload names its default preset (`properties.defaultPreset`), an item of its own whose icon is
// the gun as the traders sell it; the bare gun's icon is its receiver alone. Made-up ids.
public class WeaponPictureTests
{
    // The shape of json.tarkov.dev's items payload, cut to what is read: a gun with a preset, the preset, a knife
    // (a weapon without one), a medkit (no weapon: other properties, or none).
    private const string Payload = """
        {"data":{"items":{
          "gun-1":{"id":"gun-1","types":["gun","wearable"],"categories":["cat-ar","cat-weapon"],
                   "properties":{"propertiesType":"ItemPropertiesWeapon","caliber":"Caliber545x39","ergonomics":40,
                                 "allowedAmmo":["ammo-1"],"defaultPreset":"preset-1"}},
          "preset-1":{"id":"preset-1","types":["preset"],"categories":["cat-ar","cat-weapon"],
                      "properties":{"propertiesType":"ItemPropertiesPreset","baseItem":{"id":"gun-1"}}},
          "knife-1":{"id":"knife-1","types":["wearable"],"categories":["cat-knife","cat-weapon"],
                     "properties":{"propertiesType":"ItemPropertiesMelee","slashDamage":40,"defaultPreset":null}},
          "medkit-1":{"id":"medkit-1","types":[],"categories":["cat-meds"],"properties":null},
          "bolts-1":{"id":"bolts-1","types":["barter"],"categories":["cat-barter"]}
        }}}
        """;

    private static ItemSources Sources() => new()
    {
        Items = JsonSerializer.Deserialize(Payload, ApiJsonContext.Default.ApiItemsEnvelope)!.Data!.Items!,
        Barters = Array.Empty<ApiBarter>().ToLookup(b => ""),
        Crafts = Array.Empty<ApiCraft>().ToLookup(c => ""),
        Stations = new Dictionary<string, string>(),
    };

    [Fact]
    public void A_guns_default_preset_is_read_from_its_properties()
    {
        var items = Sources().Items;
        Assert.Equal("preset-1", items["gun-1"].Properties?.DefaultPreset);
        Assert.Null(items["preset-1"].Properties?.DefaultPreset);
        Assert.Null(items["knife-1"].Properties?.DefaultPreset);
        Assert.Null(items["medkit-1"].Properties);
        Assert.Null(items["bolts-1"].Properties);
    }

    [Fact]
    public void A_gun_is_pictured_by_its_preset_and_everything_else_by_itself()
    {
        var sources = Sources();
        Assert.Equal("preset-1", sources.PictureId("gun-1"));
        // A weapon without a preset (a knife, a grenade, a launcher) keeps its own icon, already whole.
        Assert.Equal("knife-1", sources.PictureId("knife-1"));
        Assert.Equal("medkit-1", sources.PictureId("medkit-1"));
        Assert.Equal("bolts-1", sources.PictureId("bolts-1"));
        // An id the items don't know, and the time before the sources have loaded: the item's own.
        Assert.Equal("unknown-1", sources.PictureId("unknown-1"));
        Assert.Equal("gun-1", ItemSources.PictureOf(null, "gun-1"));
        Assert.Equal("preset-1", ItemSources.PictureOf(sources, "gun-1"));
    }

    [Fact]
    public void Presets_still_dont_count_in_a_class()
    {
        var members = Sources().Members;
        Assert.Equal(["gun-1"], members["cat-ar"]);
        Assert.Equal(["gun-1", "knife-1"], members["cat-weapon"].Order(StringComparer.Ordinal));
    }
}
