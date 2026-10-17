using SkiaSharp;
using Shturmap.Core;
using Shturmap.Data.TarkovDev;
using static Shturmap.Map.Tests.Fixtures;

namespace Shturmap.Map.Tests;

// A boss's spawn zones rest stepped back until they matter (owner, 2026-10-17: "I feel like the red dots for enemy bosses
// are too distracting when trying to parse the quest icons"; from a panel of four, "A"): at rest a hollow octagon at 70 %,
// the likeliest zone alone named, in the map's muted spaced caps; lit, every zone solid with its full label, while the
// boss is pointed at, or a picked, pointed-at or held quest has a kill objective for it.
public class BossRestTests
{
    private const string Kaban = "boss:bossBoar";

    // Kaban's two zones, the likeliest at the middle and one 200 m east; a quest that targets him west of them, one that
    // doesn't north of them.
    private static (Camera Camera, MapScene Scene) View()
    {
        var (camera, scene) = TestView.Of(
        [
            new MapMarker("boss:bossBoar:Dealership", MarkerKind.BossSpawn, new WorldPoint(0, 0, 0), "Kaban 75% · 60% here", Kaban) { RestLabel = "Kaban 75%" },
            new MapMarker("boss:bossBoar:Backyard", MarkerKind.BossSpawn, new WorldPoint(200, 0, 0), "Kaban 75% · 40% here", Kaban) { RestLabel = "" },
            TestView.Quest("a", -200, 0, "Dandies"),
            TestView.Quest("b", 0, 200, "Audit"),
        ]);
        scene.QuestBosses = new Dictionary<string, IReadOnlyList<string>> { ["quest-a"] = [Kaban] };
        scene.Pulse = false;
        return (camera, scene);
    }

    private static SKBitmap Render(MapScene scene, Camera camera)
    {
        var bitmap = new SKBitmap(new SKImageInfo(1000, 1000, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        MapRenderer.Render(canvas, camera, scene);
        return bitmap;
    }

    private static void Near(SKColor expected, SKColor actual, string what) =>
        Assert.True(Math.Abs(expected.Red - actual.Red) <= 3 && Math.Abs(expected.Green - actual.Green) <= 3 && Math.Abs(expected.Blue - actual.Blue) <= 3,
            $"{what}: {actual}, expected {expected}");

    private static IReadOnlyList<MapRenderer.PlacedLabel> BossLabels(MapScene scene, Camera camera) =>
        MapRenderer.Layout(camera, scene, 1).Labels.Where(l => l.Of.Marker.Kind == MarkerKind.BossSpawn).OrderBy(l => l.Of.At.X).ToList();

    // The octagon's right side runs 6.65 px from its centre (7.2 to a corner); its 1.5 px outline covers 5.9 to 7.4 px out,
    // so the pixel 6 px right of the centre is all outline.
    private const int OnTheOutline = 506;

    [Fact]
    public void At_rest_a_boss_is_a_hollow_octagon_at_70_percent()
    {
        var (camera, scene) = View();
        using var bitmap = Render(scene, camera);
        var (empty, emptyScene) = TestView.Of([]);
        using var ground = Render(emptyScene, empty);
        // Dark inside, where the solid octagon was red: its collar alone, as an open V's inside is dark (FloorTests).
        var middle = bitmap.GetPixel(500, 500);
        Assert.True(middle.Red < 60 && middle.Red - middle.Green < 15, $"inside the octagon: {middle}");
        // Its outline in red, the whole at 70 % over the map.
        var red = SKColor.Parse(Palette.Red);
        var under = ground.GetPixel(OnTheOutline, 500);
        static byte Mix(byte a, byte b) => (byte)Math.Round(0.7 * a + 0.3 * b);
        Near(new SKColor(Mix(red.Red, under.Red), Mix(red.Green, under.Green), Mix(red.Blue, under.Blue)), bitmap.GetPixel(OnTheOutline, 500), "the outline");
        Assert.All(MapRenderer.ShownMarkers(camera, scene, 1).Where(m => m.Marker.Kind == MarkerKind.BossSpawn), m => Assert.True(m.Resting));
    }

    [Fact]
    public void At_rest_only_the_likeliest_zone_is_named_with_the_chance_alone_in_muted_spaced_caps()
    {
        var (camera, scene) = View();
        var label = Assert.Single(BossLabels(scene, camera));
        Assert.Equal("KABAN 75%", label.Text);
        Assert.Equal("boss:bossBoar:Dealership", label.Of.Marker.Id);
        Assert.Equal(SKColor.Parse(Palette.Muted), label.Color);
        Assert.False(label.Bold);
        Assert.Equal(MapRenderer.BossRestNameSize, label.Size);
        Assert.True(label.Tracking > 0, "letter-spaced");
        // The quests' names stay ink.
        Assert.All(MapRenderer.Layout(camera, scene, 1).Labels.Where(l => l.Of.Marker.Kind == MarkerKind.Objective),
            l => Assert.Equal(SKColor.Parse(Palette.Ink), l.Color));
    }

    // Pointing at "Kaban 75%" in the raid line or under the map's name in Plan, or at one of his zones on the map, puts his
    // group in the focus (MainWindow.BossParts, OnMarkerHovered).
    [Fact]
    public void Pointed_at_every_zone_is_solid_with_its_full_label()
    {
        var (camera, scene) = View();
        scene.Focus = new HashSet<string> { Kaban };
        Assert.All(MapRenderer.ShownMarkers(camera, scene, 1).Where(m => m.Marker.Kind == MarkerKind.BossSpawn), m => Assert.False(m.Resting));
        var labels = BossLabels(scene, camera);
        Assert.Equal(["Kaban 75% · 60% here", "Kaban 75% · 40% here"], labels.Select(l => l.Text));
        Assert.All(labels, l => Assert.Equal((SKColor.Parse(Palette.Red), true, 0f), (l.Color, l.Bold, l.Tracking)));
        using var bitmap = Render(scene, camera);
        Near(SKColor.Parse(Palette.Red), bitmap.GetPixel(500, 500), "the likeliest zone's middle");
        Near(SKColor.Parse(Palette.Red), bitmap.GetPixel(700, 500), "the other zone's middle");
    }

    // A held card keeps its quest in the focus, as pointing at it does (MainWindow.MapFocus).
    [Theory]
    [InlineData("picked")]
    [InlineData("held")]
    public void A_quest_with_a_kill_objective_for_the_boss_lights_every_zone(string how)
    {
        var (camera, scene) = View();
        if (how == "picked")
            scene.Kept = new HashSet<string> { "quest-a" };
        else
            scene.Focus = new HashSet<string> { "quest-a" };
        Assert.All(MapRenderer.ShownMarkers(camera, scene, 1).Where(m => m.Marker.Kind == MarkerKind.BossSpawn), m => Assert.False(m.Resting));
        var labels = BossLabels(scene, camera);
        Assert.Equal(["Kaban 75% · 60% here", "Kaban 75% · 40% here"], labels.Select(l => l.Text));
        using var bitmap = Render(scene, camera);
        Near(SKColor.Parse(Palette.Red), bitmap.GetPixel(500, 500), "the likeliest zone's middle");
        Near(SKColor.Parse(Palette.Red), bitmap.GetPixel(700, 500), "the other zone's middle");
        if (how == "picked")
        {
            // A pick steps nothing back, and lights the boss as the octagon was at rest until 2026-10-17: names in ink.
            Assert.Contains(Kaban, scene.KeptBosses);
            Assert.False(scene.HasHighlight);
            Assert.All(labels, l => Assert.Equal((SKColor.Parse(Palette.Ink), false), (l.Color, l.Bold)));
        }
        else
        {
            Assert.Contains(Kaban, scene.ShownFocus);
            Assert.All(labels, l => Assert.Equal(SKColor.Parse(Palette.Red), l.Color));
        }
    }

    [Theory]
    [InlineData("picked")]
    [InlineData("held")]
    public void A_quest_without_one_leaves_the_boss_at_rest(string how)
    {
        var (camera, scene) = View();
        if (how == "picked")
            scene.Kept = new HashSet<string> { "quest-b" };
        else
            scene.Focus = new HashSet<string> { "quest-b" };
        Assert.DoesNotContain(Kaban, scene.KeptBosses);
        Assert.DoesNotContain(Kaban, scene.ShownFocus);
        Assert.All(MapRenderer.ShownMarkers(camera, scene, 1).Where(m => m.Marker.Kind == MarkerKind.BossSpawn), m => Assert.True(m.Resting));
        Assert.Equal("KABAN 75%", Assert.Single(BossLabels(scene, camera)).Text);
    }

    [Fact]
    public void Bosses_set_after_the_pick_and_the_focus_still_light()
    {
        // The content (and with it the bosses quests target) can arrive after the picks: the scene catches up, as for keys.
        var (camera, scene) = View();
        scene.QuestBosses = new Dictionary<string, IReadOnlyList<string>>();
        scene.Kept = new HashSet<string> { "quest-a" };
        scene.Focus = new HashSet<string> { "quest-a" };
        Assert.Empty(scene.KeptBosses);
        scene.QuestBosses = new Dictionary<string, IReadOnlyList<string>> { ["quest-a"] = [Kaban] };
        Assert.Contains(Kaban, scene.KeptBosses);
        Assert.Contains(Kaban, scene.ShownFocus);
        Assert.All(MapRenderer.ShownMarkers(camera, scene, 1).Where(m => m.Marker.Kind == MarkerKind.BossSpawn), m => Assert.False(m.Resting));
    }

    // ---- which quests target which bosses: their kill objectives still open, on this map or any ----

    private static ApiObjective Kill(string id, params string[] maps) =>
        new(id, "shoot", "Eliminate the target", false, [.. maps], null, null, null, null, null, null, null, null, false);

    private static ApiTask QuestWith(string id, ApiObjective objective) =>
        new(id, id, null, null, "map-1", null, false, false, null, null, null, false, null, [objective], null);

    [Fact]
    public void A_quest_targets_the_bosses_its_open_kill_objectives_name_on_this_map()
    {
        var map = TestMap("streets-of-tarkov", bosses:
        [
            new("bossBoar", 0.75, [new("Dealership", 0.6, [At(0, 0)]), new("Backyard", 0.4, [At(200, 0)])]),
            new("bossKolontay", 0.75, [new("Mall", 1, [At(-300, 0)])]),
        ]);
        var plain = With(map, new ApiMob("bossBoar", "Kaban"), new ApiMob("bossKolontay", "Kollontay"));
        (string Quest, ApiObjective Objective, string[] Targets)[] quests =
        [
            ("kill", Kill("o-kill", "map-1"), ["bossBoar"]),
            ("any boss", Kill("o-any"), ["bossBoar", "bossKolontay", "followerBoar"]),
            ("by name", Kill("o-name", "map-1"), ["kaban"]),
            ("scavs", Kill("o-scavs", "map-1"), ["Savage", "Marksman"]),
            ("guards", Kill("o-guards", "map-1"), ["followerBoar"]),
            ("done", Kill("o-done", "map-1"), ["bossBoar"]),
            ("elsewhere", Kill("o-elsewhere", "map-2"), ["bossBoar"]),
            ("found", new ApiObjective("o-found", "findQuestItem", "Find the item", false, ["map-1"], null, null, null, null, null, null, null, null, false), ["bossBoar"]),
        ];
        var data = new GameData
        {
            Mode = plain.Mode, Language = "en", Maps = plain.Maps, Mobs = plain.Mobs, Traders = plain.Traders, MapDefinitions = plain.MapDefinitions,
            CheckedAt = plain.CheckedAt,
            Tasks = quests.ToDictionary(q => q.Quest, q => QuestWith(q.Quest, q.Objective)),
            ObjectiveFacts = quests.ToDictionary(q => q.Objective.Id, q => new ObjectiveFacts(q.Targets, [], [])),
        };
        var bosses = MapContentBuilder.Build(data, "map-1", quests.Select(q => q.Quest), new HashSet<string> { "o-done" }).QuestBosses;
        Assert.Equal(["any boss", "by name", "kill"], bosses.Keys.Order());
        Assert.Equal([Kaban], bosses["kill"]);
        Assert.Equal([Kaban], bosses["by name"]);
        Assert.Equal([Kaban, "boss:bossKolontay"], bosses["any boss"]);
    }
}
