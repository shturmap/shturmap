using SkiaSharp;
using Shturmap.Core;
using Shturmap.Core.Maps;
using Shturmap.Data.TarkovDev;
using static Shturmap.Map.Tests.Fixtures;

namespace Shturmap.Map.Tests;

// The extracts a switch gates, on the map (owner, 2026-10-17: "Some exits still require specific actions, for example
// one exit in interchange requires the power to be switched on. This is currently not represented in the map at all";
// from a panel of three: "A", a badge on the exit and the link on pointing). tarkov.dev's own switches tell no extract
// apart (the verification of 2026-10-17), so the steps come from the hand-kept table (SwitchExits).
public class SwitchExitTests
{
    // A table row's extract on its map, with every step placed (switches east of it, the lock between), a plain extract
    // far off, and the data's one lever listed on both, as Customs lists it on all of its extracts.
    private static (ApiMap Map, GameData Data) TableMap(SwitchExits.Row row)
    {
        var switches = row.Steps.Where(s => !s.Lock).Select((s, i) => new ApiSwitch(s.Id, $"Step {i + 1}", At(20 * (i + 1), 0))).ToList();
        var locks = row.Steps.Where(s => s.Lock).Select(s => new ApiLock(s.Id, "door", "keycard", false, At(30, 10))).ToList();
        var map = new ApiMap("map-1", "Map", row.Map, row.Map, null, null, 40,
            [
                new(row.ExtractId, "Gated", "pmc", At(0, 0), null, null, null, [row.Steps[0].Id]),
                new("plain", "Plain", "pmc", At(-300, -300), null, null, null, [row.Steps[0].Id]),
            ],
            [], locks, [], [], Switches: switches);
        return (map, Keyed(map, new Dictionary<string, string> { [row.ExtractId] = row.Key, ["plain"] = "plain_exit" }));
    }

    private static GameData Keyed(ApiMap map, IReadOnlyDictionary<string, string> keys)
    {
        var plain = With(map);
        return new GameData
        {
            Mode = plain.Mode, Language = "en", Maps = plain.Maps, Tasks = plain.Tasks, Traders = plain.Traders, MapDefinitions = plain.MapDefinitions,
            CheckedAt = plain.CheckedAt, ExtractKeys = keys,
        };
    }

    public static TheoryData<int> Rows() => new(Enumerable.Range(0, SwitchExits.Table.Count));

    [Theory]
    [MemberData(nameof(Rows))]
    public void Each_table_extract_and_its_steps_light_each_other_and_it_carries_them_in_order(int index)
    {
        var row = SwitchExits.Table[index];
        var (map, data) = TableMap(row);
        var steps = row.Steps.Select(s => s.MarkerId).ToList();
        var links = MapContentBuilder.ExtractSwitchLinks(data, map);
        Assert.Equal(steps.Order(StringComparer.Ordinal), links["extract:" + row.ExtractId]);
        Assert.All(steps, s => Assert.Equal(["extract:" + row.ExtractId], links[s]));
        Assert.False(links.ContainsKey("extract:plain"));
        // The extract's marker carries its steps in the order of use; the plain one none, and so no badge.
        var content = MapContentBuilder.Build(data, map.Id, [], new HashSet<string>());
        Assert.Equal(steps, content.Markers.Single(m => m.Id == "extract:" + row.ExtractId).SwitchSteps);
        Assert.Null(content.Markers.Single(m => m.Id == "extract:plain").SwitchSteps);
        Assert.Equal(links.Keys.Order(StringComparer.Ordinal), content.Links.Keys.Order(StringComparer.Ordinal));
        // Every step has its marker (a switch's, the Saferoom's keycard lock's).
        Assert.All(steps, s => Assert.Contains(content.Markers, m => m.Id == s));
    }

    [Fact]
    public void A_step_the_data_doesnt_place_has_no_marker_and_the_extract_keeps_its_badge()
    {
        var row = SwitchExits.Table.Single(r => r.Key == "EXFIL_Bunker");
        var (map, data) = TableMap(row);
        var unplaced = map with { Switches = [new ApiSwitch(row.Steps[0].Id, "Lever")] };
        var marker = MapContentBuilder.Build(Keyed(unplaced, data.ExtractKeys), unplaced.Id, [], new HashSet<string>()).Markers.Single(m => m.Id == "extract:" + row.ExtractId);
        Assert.NotNull(marker.SwitchSteps);
        Assert.Empty(marker.SwitchSteps!);
        Assert.Empty(MapContentBuilder.ExtractSwitchLinks(Keyed(unplaced, data.ExtractKeys), unplaced));
    }

    // ---- the badge ----

    private static MapMarker Gated(double x, double z, string name = "Saferoom Exfil", MarkerKind kind = MarkerKind.ExtractPmc, IReadOnlyList<string>? steps = null) =>
        new("extract:gated", kind, new WorldPoint(x, 0, z), name) { SwitchSteps = steps ?? ["switch:power", "switch:door"] };

    private static MapMarker Switch(string id, double x, double z, string name) => new("switch:" + id, MarkerKind.Switch, new WorldPoint(x, 0, z), name);

    // The badge of 2026-10-17 at 200 %, in white on the dark disc, the badge alone: a disc ⌀ 14 with a 1.5 rim like the
    // floor arrow's, dark inside, the power symbol in the extract's colour in the middle, clear of the rim.
    [Fact]
    public void The_switch_badge_is_a_dark_disc_with_a_rim_and_the_power_symbol_at_the_lower_left()
    {
        const float ui = 2;
        var shown = new MapRenderer.ShownMarker(Gated(0, 0), new SKPoint(80.5f, 40.5f), 7.5f * ui, SKColors.White, false, false, false, 0);
        Assert.True(MapRenderer.ShowsSwitchBadge(shown));
        var c = MapRenderer.SwitchBadgeCenter(shown, ui);
        // Lower left of the symbol, as far out as the floor arrow stands at its upper right.
        var floor = MapRenderer.FloorBadgeCenter(shown with { Floor = 1 }, ui);
        Assert.Equal(shown.At.X - (floor.X - shown.At.X), c.X, 3);
        Assert.Equal(shown.At.Y + (shown.At.Y - floor.Y), c.Y, 3);
        using var bitmap = new SKBitmap(new SKImageInfo(120, 120, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            MapRenderer.DrawSwitchBadge(canvas, shown, ui);
        }
        bool Lit(int x, int y) => bitmap.GetPixel(x, y) is { Alpha: > 200, Red: > 128 };
        bool Dark(int x, int y) => bitmap.GetPixel(x, y) is { Alpha: > 200, Red: < 60 };
        (int Width, int Height) Ink(Func<int, int, bool> counts)
        {
            int left = 120, top = 120, right = -1, bottom = -1;
            for (var y = 0; y < 120; y++)
                for (var x = 0; x < 120; x++)
                    if (counts(x, y))
                    {
                        left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                    }
            return right < 0 ? (0, 0) : (right - left + 1, bottom - top + 1);
        }
        // The disc with its rim: ⌀ 14 + 1.5.
        var badge = Ink((x, y) => bitmap.GetPixel(x, y).Alpha > 128);
        Assert.InRange(badge.Width, 15.5 * ui - 1, 15.5 * ui + 1);
        Assert.InRange(badge.Height, 15.5 * ui - 1, 15.5 * ui + 1);
        // The rim: 1.5 DIP of the extract's colour, then dark, along the line through the centre.
        var (cx, cy) = ((int)c.X, (int)c.Y);
        var from = Enumerable.Range(0, cx).First(x => bitmap.GetPixel(x, cy).Alpha > 200);
        var rim = Enumerable.Range(from, cx - from).TakeWhile(x => Lit(x, cy)).Count();
        Assert.InRange(rim, 1.5 * ui - 1, 1.5 * ui + 1);
        Assert.True(Dark(from + rim + 1, cy), "dark inside the rim");
        // Hollow: a dark ring between the rim and the symbol all round, and the symbol, lit, within it: about 8 DIP.
        foreach (var angle in Enumerable.Range(0, 16).Select(i => i * Math.PI / 8))
        {
            var (x, y) = ((int)(c.X + Math.Cos(angle) * 5.5 * ui), (int)(c.Y + Math.Sin(angle) * 5.5 * ui));
            Assert.False(Lit(x, y), $"lit between the rim and the symbol at {angle:0.00}");
        }
        var glyph = Ink((x, y) => SKPoint.Distance(new SKPoint(x + 0.5f, y + 0.5f), c) < 5 * ui && Lit(x, y));
        Assert.InRange(glyph.Width, 6 * ui, 9.5 * ui);
        Assert.InRange(glyph.Height, 6 * ui, 9.5 * ui);
    }

    [Fact]
    public void Only_a_gated_extract_carries_the_badge()
    {
        var shown = new MapRenderer.ShownMarker(Gated(0, 0), new SKPoint(50, 50), 7.5f, SKColors.White, false, false, false, 0);
        Assert.True(MapRenderer.ShowsSwitchBadge(shown));
        Assert.False(MapRenderer.ShowsSwitchBadge(shown with { Marker = shown.Marker with { SwitchSteps = null } }));
        Assert.False(MapRenderer.ShowsSwitchBadge(shown with { Marker = Switch("power", 0, 0, "Power") }));
    }

    // An SVG map: locks and switches show from 1.5 times the overview there (MapRenderer.LandmarkFromZoom), on a sheet
    // at any zoom.
    private static (Camera Camera, MapScene Scene, MapArtwork Artwork) OnArtwork(params MapMarker[] markers)
    {
        var (camera, sheet) = TestView.Of(markers);
        var path = Path.Combine(Path.GetTempPath(), $"shturmap-switch-{Guid.NewGuid():N}.svg");
        File.WriteAllText(path, """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1000 1000"><rect width="1000" height="1000" fill="#303030"/></svg>""");
        MapArtwork artwork;
        try
        {
            artwork = MapArtwork.Load(path, sheet.Definition);
        }
        finally
        {
            File.Delete(path);
        }
        var scene = new MapScene(sheet.Definition, artwork) { Markers = markers };
        camera.Fit(scene.Projection.WorldRect, 24, Camera.LabelRoom);
        return (camera, scene, artwork);
    }

    [Fact]
    public void The_badge_is_drawn_at_rest_at_the_overview_and_no_name_comes_near_it()
    {
        var exit = Gated(0, 0);
        var (camera, scene, artwork) = OnArtwork(exit, Switch("power", 120, 0, "Mall Main Power Switch"), Switch("door", 160, 40, "Saferoom Exfil Switch"),
            TestView.Quest("a", -40, -20, "Minute of Fame"));
        using var _ = artwork;
        Assert.True(MapRenderer.ZoomOverOverview(camera, scene, 1) < MapRenderer.LandmarkFromZoom);
        var layout = MapRenderer.Layout(camera, scene, 1);
        var shown = layout.Markers.Single(m => m.Marker.Id == exit.Id);
        Assert.True(MapRenderer.ShowsSwitchBadge(shown));
        // At rest the switches stay hidden at this zoom, and there is no guide.
        Assert.DoesNotContain(layout.Markers, m => m.Marker.Kind == MarkerKind.Switch);
        Assert.Null(layout.ExitGuide);
        var keep = SKRect.Inflate(MapRenderer.SwitchBadgeBox(shown, 1), 3, 3);
        Assert.All(layout.Labels, l => Assert.False(l.Box.IntersectsWith(keep), $"{l.Text} within 3 DIP of the switch badge"));
        Assert.Contains(layout.Labels, l => l.Text == "Saferoom Exfil");
        // Drawn: the badge's centre is dark, its rim in the PMC extract's green.
        using var bitmap = new SKBitmap((int)camera.Viewport.Width, (int)camera.Viewport.Height);
        using (var canvas = new SKCanvas(bitmap))
            MapRenderer.Render(canvas, camera, scene, 1);
        var c = MapRenderer.SwitchBadgeCenter(shown, 1);
        var green = SKColor.Parse(Palette.Green);
        static int Off(SKColor a, SKColor b) => Math.Abs(a.Red - b.Red) + Math.Abs(a.Green - b.Green) + Math.Abs(a.Blue - b.Blue);
        var rim = Enumerable.Range((int)(c.X - MapRenderer.SwitchBadgeDisc - 2), 4).Min(x => Off(bitmap.GetPixel(x, (int)c.Y), green));
        Assert.True(rim < 60, $"the rim is {rim} off the extract's green");
        Assert.True(Off(bitmap.GetPixel((int)c.X + 4, (int)c.Y - 4), SKColor.Parse(Palette.Ground)) < 40, "dark inside the badge");
    }

    [Fact]
    public void A_name_left_of_a_gated_extract_moves_out_past_the_badge_rather_than_giving_up_the_side()
    {
        // A neighbour takes the right; the name goes left, off the badge's keep-out.
        var exit = Gated(0, 0, "D-2");
        var (camera, scene) = TestView.Of([exit, TestView.Quest("b", 40, 0, "Network Provider - Part 1")]);
        var layout = MapRenderer.Layout(camera, scene, 1);
        var shown = layout.Markers.Single(m => m.Marker.Id == exit.Id);
        var name = layout.Labels.Single(l => l.Text == "D-2");
        Assert.False(name.Box.IntersectsWith(SKRect.Inflate(MapRenderer.SwitchBadgeBox(shown, 1), 3, 3)));
        Assert.True(name.Box.Right <= shown.At.X || name.Box.Bottom <= shown.At.Y, $"the name stands left of or above its extract: {name.Box}");
    }

    // ---- pointing at it ----

    // As the window's MapFocus does: the extract pointed at and what it links (MapContent.Links).
    private static IReadOnlySet<string> Pointing(params string[] ids) => new HashSet<string>(ids, StringComparer.Ordinal);

    [Fact]
    public void Pointed_at_its_switches_light_in_its_colour_named_for_it_with_the_guide_to_the_first_at_any_zoom()
    {
        var exit = Gated(0, 0);
        var (camera, scene, artwork) = OnArtwork(exit, Switch("power", 200, 0, "Mall Main Power Switch"), Switch("door", 240, 40, "Saferoom Exfil Switch"),
            Switch("alarm", -120, 0, "Alarm Switch"));
        using var _ = artwork;
        scene.Focus = Pointing(exit.Id, "switch:power", "switch:door");
        scene.Dim = 1;
        var layout = MapRenderer.Layout(camera, scene, 1);
        var green = MapRenderer.ShownMarkers(camera, scene, 1).Single(m => m.Marker.Id == exit.Id).Color;
        var lit = layout.Markers.Where(m => m.Marker.Kind == MarkerKind.Switch).ToList();
        // Below 1.5 times the overview, the chain's switches show while pointed at; another switch doesn't.
        Assert.Equal(["switch:power", "switch:door"], lit.Select(m => m.Marker.Id));
        Assert.All(lit, m => Assert.Equal(green, m.Color));
        Assert.All(lit, m => Assert.Equal(exit.Id, m.StepOf?.Id));
        Assert.Contains(layout.Labels, l => l.Text == "Mall Main Power Switch · Saferoom Exfil" && l.Color == green);
        Assert.Contains(layout.Labels, l => l.Text == "Saferoom Exfil Switch · Saferoom Exfil");
        // The guide: from the extract to the first switch, in its colour, with their distance (200 m) on its plate.
        var guide = layout.ExitGuide;
        Assert.NotNull(guide);
        var at = layout.Markers.Single(m => m.Marker.Id == exit.Id).At;
        var first = lit.Single(m => m.Marker.Id == "switch:power").At;
        Assert.Equal(at, guide!.From);
        Assert.Equal(first, guide.To);
        Assert.Equal(green, guide.Color);
        Assert.Equal(200, guide.Metres, 3);
        Assert.Equal(MapRenderer.DistanceText(200), guide.Plate);
        Assert.All(layout.Markers, m => Assert.False(guide.PlateBox.IntersectsWith(SKRect.Create(m.At.X - 2, m.At.Y - 2, 4, 4)), $"the plate on {m.Marker.Id}"));

        // Leaving puts it back: no guide, the switches hidden at this zoom again.
        scene.Focus = new HashSet<string>();
        scene.Dim = 0;
        var after = MapRenderer.Layout(camera, scene, 1);
        Assert.Null(after.ExitGuide);
        Assert.DoesNotContain(after.Markers, m => m.Marker.Kind == MarkerKind.Switch);
    }

    [Fact]
    public void Pointing_at_the_first_switch_lights_its_extract_and_draws_the_guide_but_a_later_one_doesnt()
    {
        var exit = Gated(0, 0);
        var (camera, scene) = TestView.Of([exit, Switch("power", 120, 0, "Power"), Switch("door", 160, 40, "Door")]);
        // A switch's links: its extract (MapContent.Links).
        scene.Focus = Pointing("switch:power", exit.Id);
        scene.Dim = 1;
        Assert.NotNull(MapRenderer.Layout(camera, scene, 1).ExitGuide);
        scene.Focus = Pointing("switch:door", exit.Id);
        var layout = MapRenderer.Layout(camera, scene, 1);
        Assert.Null(layout.ExitGuide);
        Assert.Contains(layout.Labels, l => l.Text == "Door · Saferoom Exfil");
    }

    [Fact]
    public void The_picks_guide_and_the_extracts_guide_are_drawn_together()
    {
        var exit = Gated(0, 0);
        var (camera, scene) = TestView.Of([exit, Switch("power", 120, 0, "Power"), TestView.Quest("a", -200, 100, "Gratitude")]);
        scene.Player = new PlayerFix(new WorldPoint(-100, 0, -100), null, DateTime.Now);
        scene.Kept = new HashSet<string> { "quest-a" };
        scene.Focus = Pointing(exit.Id, "switch:power");
        scene.Dim = 1;
        var layout = MapRenderer.Layout(camera, scene, 1);
        Assert.NotNull(layout.Guide);
        Assert.NotNull(layout.ExitGuide);
        Assert.NotEqual(layout.Guide!.Color, layout.ExitGuide!.Color);
        Assert.False(layout.Guide.PlateBox.IntersectsWith(layout.ExitGuide.PlateBox));
    }

    // ---- the roll call ----

    [Fact]
    public void The_roll_call_hands_its_rows_the_extracts_with_their_steps()
    {
        // The window's row draws the badge before the name of each extract whose marker carries steps
        // (MainWindow.RollCall's AddRollRow: MapMarker.SwitchSteps), the markers the scene calls.
        var gated = Gated(0, 0);
        var plain = new MapMarker("extract:plain", MarkerKind.ExtractPmc, new WorldPoint(50, 0, 0), "Plain");
        var (_, scene) = TestView.Of([gated, plain]);
        scene.ExitsListed = new HashSet<string> { gated.Id, plain.Id };
        var called = scene.ExitsToCall();
        Assert.NotNull(called.Single(m => m.Id == gated.Id).SwitchSteps);
        Assert.Null(called.Single(m => m.Id == plain.Id).SwitchSteps);
    }

    [Fact]
    public void The_legend_lists_the_badge_where_the_map_has_a_gated_extract()
    {
        var (_, without) = TestView.Of([new MapMarker("extract:plain", MarkerKind.ExtractPmc, new WorldPoint(0, 0, 0), "Plain")]);
        Assert.DoesNotContain(LegendSymbol.ExtractSwitch, MapLegend.On(without));
        var (_, with) = TestView.Of([Gated(0, 0)]);
        Assert.Contains(LegendSymbol.ExtractSwitch, MapLegend.On(with));
        using var swatch = MapLegend.Draw(LegendSymbol.ExtractSwitch, 2);
        var green = SKColor.Parse(Palette.Green);
        Assert.Contains(swatch.Pixels, p => p.Alpha == 255 && Math.Abs(p.Red - green.Red) <= 3 && Math.Abs(p.Green - green.Green) <= 3);
    }
}
