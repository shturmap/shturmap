using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Rules;
using Shturmap.Core;
using Shturmap.Core.Text;
using Shturmap.Map;
using Shturmap.Session;
using SkiaSharp.Views.Windows;

namespace Shturmap.App;

// What's New (owner, 2026-10-07: "how to inform the user about news and changes in the app ... ideally accompanied with
// screenshots"; from the panels "C", then "Whats new: A"; docs/DESIGN.md §4, "Screen anatomy"): a card at the top of
// Plan's rail after an update, its lines from docs/whats-new.md. Pointing at a line previews it on the map, as pointing at
// a map's row does: the map is the screenshot, drawn from the cache by the app's own code, so no map art or game art goes
// into a build (§3). Every preview is an example, and says so. The card goes with ×, or once the first raid since the
// update is over; help brings it back.
public sealed partial class MainWindow
{
    private const string WhatsNewSetting = "whatsNew.seen";
    private const string WhatsNewPreviewPrefix = "whatsnew:";
    // What's New in the language it was last read in (WhatsNewSections).
    private (string Language, IReadOnlyList<WhatsNew.Section> Sections)? _whatsNewSections;
    private IReadOnlyList<WhatsNew.Section> _whatsNewShown = [];
    private bool _whatsNewChecked;

    // The lines in the language in use, read again when that has changed (Rules.BuiltDocs).
    private IReadOnlyList<WhatsNew.Section> WhatsNewSections
    {
        get
        {
            var language = UiLanguage.Code;
            if (_whatsNewSections is { } known && known.Language == language)
                return known.Sections;
            var sections = LoadWhatsNew(language);
            _whatsNewSections = (language, sections);
            return sections;
        }
    }

    // Each version's lines in the language's translation where it has them, else in English; the pseudo-language's
    // accented (docs/LANGUAGES.md, "Layout check").
    private static IReadOnlyList<WhatsNew.Section> LoadWhatsNew(string language)
    {
        static IReadOnlyList<WhatsNew.Section>? Read(string? name) => BuiltDoc(name) is { } text ? WhatsNew.Parse(text) : null;
        var sections = WhatsNew.InLanguage(Read(BuiltDocs.WhatsNew) ?? [], Read(BuiltDocs.TranslationOf(BuiltDocs.WhatsNew, language)));
        return language == UiLanguage.Pseudo ? WhatsNew.Map(sections, PseudoText.Of) : sections;
    }

    /// <summary>A Markdown file built into the app (docs/tour.md, docs/whats-new.md and their translations;
    /// Rules.BuiltDocs), or null where there is none by that name.</summary>
    private static string? BuiltDoc(string? name)
    {
        if (name is null)
            return null;
        using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream(name);
        if (stream is null)
            return null;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // Help's link to the card: "WHAT'S NEW IN 0.4.0 · PRAETORIAN".
    private string WhatsNewHelpText() => WhatsNewSections.FirstOrDefault() is { } newest ? AppTexts.HelpWhatsNew(version: WhatsNew.Tag(newest)) : "";

    // Once, when the app has its data: what is new since the version last seen. Not at a first start (help opens then,
    // and this version counts as seen), and never by itself in a snapshot or the website demo.
    private void ShowWhatsNewOnce(SessionSnapshot s)
    {
        if (_whatsNewChecked || s.Data is null || DemoMode)
            return;
        _whatsNewChecked = true;
        var newest = SayWhatsNewHelp();
        if (SnapshotMode)
            return;
        var seen = _session.GetSetting(WhatsNewSetting);
        var firstStart = seen is null && _session.GetSetting("help.seen") is null;
        if (firstStart && newest is not null)
            _session.SetSetting(WhatsNewSetting, newest.Label);
        ShowWhatsNew(WhatsNew.Due(WhatsNewSections, seen, firstStart), "start");
    }

    // Help's link to the newest version's card; said again in a new language (MainWindow.Language).
    private WhatsNew.Section? SayWhatsNewHelp()
    {
        var newest = WhatsNewSections.FirstOrDefault();
        ViewModel.WhatsNewHelp = WhatsNewHelpText();
        return newest;
    }

    private void ShowWhatsNew(IReadOnlyList<WhatsNew.Section> sections, string how)
    {
        RenderWhatsNew(sections);
        if (sections.Count > 0)
            Study.Ui("whatsnew.show", ("how", how), ("versions", string.Join(",", sections.Select(x => x.Label))));
    }

    private void RenderWhatsNew(IReadOnlyList<WhatsNew.Section> sections)
    {
        _whatsNewShown = sections;
        ViewModel.WhatsNewBlocks = sections
            .Select(section => new WhatsNewBlock(WhatsNew.Heading(section),
                section.Items.Select((item, i) => new WhatsNewRow($"{section.Label}:{i}", item.Name, item.Text, Swatch(item.Preview), item.Preview == "clock")).ToList()))
            .ToList();
        ViewModel.WhatsNewShown = sections.Count > 0;
    }

    // A line's picture: the map's own symbol, drawn by its renderer as help's legend rows are; the clock's is drawn in XAML.
    private static ImageSource? Swatch(string preview)
    {
        LegendSymbol? symbol = preview switch
        {
            "replay" => LegendSymbol.Replay,
            "extracts" => LegendSymbol.ExtractListed,
            "joined" => LegendSymbol.Joined,
            "leaders" => LegendSymbol.Leader,
            _ => null,
        };
        if (symbol is not { } s)
            return null;
        using var bitmap = MapLegend.Draw(s, 2);
        return bitmap.ToWriteableBitmap();
    }

    /// <summary>The card goes (× or the first raid over): the newest version it showed counts as seen.</summary>
    private void WhatsNewSeen(string how)
    {
        if (!ViewModel.WhatsNewShown)
            return;
        ViewModel.WhatsNewShown = false;
        if (_previewing?.StartsWith(WhatsNewPreviewPrefix, StringComparison.Ordinal) == true)
        {
            _previewTimer?.Stop();
            EndPreview(restore: true);
        }
        if (!SnapshotMode && _whatsNewShown.FirstOrDefault() is { } newest
            && (WhatsNew.VersionOf(_session.GetSetting(WhatsNewSetting)) is not { } seen || newest.Version > seen))
            _session.SetSetting(WhatsNewSetting, newest.Label);
        Study.Ui("whatsnew.close", ("how", how));
    }

    private void OnWhatsNewClosed(object sender, RoutedEventArgs e) => WhatsNewSeen("close");

    // Help's WHAT'S NEW IN 0.4.0: the card of the newest version again.
    private void OnWhatsNewAgainClick(object sender, RoutedEventArgs e)
    {
        HelpFlyout.Hide();
        if (WhatsNewSections.FirstOrDefault() is { } newest)
            ShowWhatsNew([newest], "help");
    }

    private void OnWhatsNewPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not Grid { Tag: string key } row)
            return;
        row.Background = Resource("LinkBrush");
        // A line about the tour previews nothing: a click on it opens the tour (OnWhatsNewTapped).
        if (WhatsNewItem(key) is { } item && Tour.ChapterOf(item.Preview) is not null)
            return;
        Preview(WhatsNewPreviewPrefix + key, PreviewAfter);
    }

    // A click on a line about the tour opens it, at the chapter the line names (owner, 2026-10-09: a release that
    // changes a chapter says so here; docs/tour.md).
    private void OnWhatsNewTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is not Grid { Tag: string key } || WhatsNewItem(key) is not { } item || Tour.ChapterOf(item.Preview) is not { } chapter)
            return;
        Study.Ui("whatsnew.tour", ("chapter", chapter + 1));
        OpenTour(chapter, "whatsnew");
    }

    // The line a row's key ("0.4.0:2") stands for.
    private WhatsNew.Item? WhatsNewItem(string key)
    {
        var colon = key.LastIndexOf(':');
        return colon > 0 && int.TryParse(key[(colon + 1)..], out var index)
            && WhatsNewSections.FirstOrDefault(s => s.Label == key[..colon]) is { } section && index < section.Items.Count
            ? section.Items[index] : null;
    }

    private void OnWhatsNewPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid row)
            row.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        Preview(null, PreviewEndAfter);
    }

    // ---- the previews: each an example, staged on its map ----

    private async Task StartWhatsNewPreviewAsync(string wanted)
    {
        var key = wanted[WhatsNewPreviewPrefix.Length..];
        var colon = key.LastIndexOf(':');
        if (colon < 0 || !int.TryParse(key[(colon + 1)..], out var index)
            || WhatsNewSections.FirstOrDefault(s => s.Label == key[..colon]) is not { } section || index >= section.Items.Count
            || _snapshot?.Data is not { } data)
            return;
        var item = section.Items[index];
        if (_previewing is null)
            _restoreView = Map.HasView ? Map.View : null;
        _previewing = wanted;
        ViewModel.PreviewText = AppTexts.PreviewWhatsNew(name: Caps.Of(item.Name), version: section.Label);
        ViewModel.PreviewHint = AppTexts.PreviewExample;
        Study.Ui("whatsnew.point", ("version", section.Label), ("item", item.Preview));
        switch (item.Preview)
        {
            case "clock":
                // Not on the map: the raid card's own clock, with example times, on a plate over the dimmed map.
                WhatsNewPlateBox.Child = new Controls.RaidClock { Reading = RaidTime.Of(true, false, DateTime.Now.AddMinutes(-27), 40, DateTime.Now) };
                WhatsNewPlateNote.Text = AppTexts.WhatsNewClockExample;
                WhatsNewPlate.Visibility = Visibility.Visible;
                break;
            case "extracts":
                await PreviewSceneAsync(wanted, data, "customs", [], (scene, content) => StageExtractList(data, scene, content), null, 0);
                break;
            case "joined":
                // Gratitude's two stashes on one spot (docs/DESIGN.md "Map drawing", *Objectives of one quest on one spot*).
                await PreviewSceneAsync(wanted, data, "woods", ["gratitude"], null,
                    content => content.Objectives.Where(o => o.Quest.NormalizedName == "gratitude").SelectMany(o => o.Places).Take(1).ToList(), 220);
                break;
            case "leaders":
                // Streets' Scav Checkpoint, where an extract and a transit crowd one spot; found by its English name, the
                // label being in the data's language.
                await PreviewSceneAsync(wanted, data, "streets-of-tarkov", [], null,
                    content => content.Markers.Where(m => MapContentBuilder.EnglishName(data, m).Contains("Scav Checkpoint", StringComparison.OrdinalIgnoreCase))
                        .Select(m => m.Position).Take(1).ToList(), 700);
                break;
            case "replay":
                await PreviewSceneAsync(wanted, data, "customs", [], (scene, content) =>
                {
                    scene.Replay = ReplayExample(data, content);
                    scene.ReplayDone = true;
                }, content => ReplayExample(data, content)?.Fixes.Select(f => f.Position).ToList(), 300);
                break;
        }
    }

    // A preview's map: its artwork, the given quests' places (none of the player's), staged and framed.
    /// <param name="foot">DIPs at the map's foot that what is framed stays above (the tour's band).</param>
    /// <param name="padding">DIPs around what is framed (the tour's are wider); the right side keeps the label room
    /// (<see cref="Controls.MapView.FramingAbove"/>).</param>
    private async Task PreviewSceneAsync(string wanted, Shturmap.Data.TarkovDev.GameData data, string mapName, IReadOnlyCollection<string> quests,
        Action<MapScene, MapContent>? stage, Func<MapContent, IReadOnlyCollection<WorldPoint>?>? frame, double minMetres, double foot = 0, double padding = 40)
    {
        if (data.MapByNormalizedName(mapName) is not { } map || data.DefinitionFor(mapName) is not { } definition || _session.Artwork is null)
            return;
        var artwork = await ArtworkFor(definition, map.Name);
        if (_previewing != wanted)
            return;
        var ids = data.Tasks.Values.Where(t => t.NormalizedName is { } n && quests.Contains(n)).Select(t => t.Id).ToList();
        var content = MapContentBuilder.Build(data, map.Id, ids, new HashSet<string>());
        var scene = new MapScene(definition, artwork, artwork is null ? TilesFor(definition, map.Name) : null)
            { Markers = content.Markers, Zones = content.Zones, Containers = content.Containers, QuestKeys = content.QuestKeys };
        stage?.Invoke(scene, content);
        var points = frame?.Invoke(content);
        Map.SetScene(scene, points is { Count: > 0 } ? Map.FramingAbove(scene, points, padding, foot, minMetres) : null);
        _scenes.Forget();
    }

    // An example extract list on Customs: some lit, one marked "??:??:??", the rest hollow.
    private static void StageExtractList(Shturmap.Data.TarkovDev.GameData data, MapScene scene, MapContent content)
    {
        var (listed, unsure, notListed) = ExampleExits(data, content);
        scene.ExitsListed = listed.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        scene.ExitsUnsure = unsure.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        scene.ExitsNotListed = notListed;
    }

    /// <summary>
    /// The example extract list on Customs (What's New's preview, the tour): the extracts it names, one it marks
    /// "??:??:??", and the ids of the rest, which it leaves out. Nobody's real list. The extracts are picked by their
    /// English names (<see cref="MapContentBuilder.EnglishName"/>): the labels are in the data's language.
    /// </summary>
    internal static (List<MapMarker> Listed, List<MapMarker> Unsure, HashSet<string> NotListed) ExampleExits(Shturmap.Data.TarkovDev.GameData data, MapContent content)
    {
        var exits = content.Markers.Where(m => m.Kind is MarkerKind.ExtractPmc or MarkerKind.ExtractShared).ToList();
        string[] names = ["Crossroads", "Trailer Park", "Old Gas Station", "RUAF Roadblock", "Smugglers' Boat", "Dorms V-Ex"];
        var listed = exits.Where(e => names.Any(n => MapContentBuilder.EnglishName(data, e).Contains(n, StringComparison.OrdinalIgnoreCase))).ToList();
        if (listed.Count < 3)
            listed = exits.Where((_, i) => i % 2 == 0).ToList();
        var unsure = exits.Where(e => !listed.Contains(e)).Take(1).ToList();
        var notListed = exits.Where(e => !listed.Contains(e) && !unsure.Contains(e)).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        return (listed, unsure, notListed);
    }

    /// <summary>
    /// A made-up raid on Customs for the previews and the developer view: seven positions between two of its extracts,
    /// with gaps of minutes, 33 minutes in all. Nobody's real positions. The extracts are picked by their English names,
    /// the map named as the data names it.
    /// </summary>
    internal static RaidReplay? ReplayExample(Shturmap.Data.TarkovDev.GameData data, MapContent content)
    {
        var exits = content.Markers.Where(m => m.Kind is MarkerKind.ExtractPmc or MarkerKind.ExtractShared).ToList();
        var from = exits.FirstOrDefault(e => MapContentBuilder.EnglishName(data, e).Contains("Old Gas Station", StringComparison.OrdinalIgnoreCase)) ?? exits.FirstOrDefault();
        var to = exits.FirstOrDefault(e => MapContentBuilder.EnglishName(data, e).Contains("Dorms V-Ex", StringComparison.OrdinalIgnoreCase)) ?? exits.LastOrDefault();
        if (from is null || to is null || from == to || data.MapByNormalizedName("customs") is not { } customs)
            return null;
        double[] minutes = [0.5, 4, 9, 11, 17.5, 24, 29];
        double[] along = [0.08, 0.22, 0.4, 0.47, 0.63, 0.8, 0.9];
        double[] aside = [0, 18, -14, 12, -22, 10, -6];
        var dx = to.Position.X - from.Position.X;
        var dz = to.Position.Z - from.Position.Z;
        var length = Math.Max(1, Math.Sqrt(dx * dx + dz * dz));
        var fixes = minutes.Select((m, i) => new ReplayFix(m, new WorldPoint(
            from.Position.X + dx * along[i] - dz / length * aside[i], from.Position.Y,
            from.Position.Z + dz * along[i] + dx / length * aside[i]))).ToList();
        return new RaidReplay(customs.NormalizedName, customs.Name, fixes, 33) { Ticks = [12, 12.2], ListRead = 0.6 };
    }

    private void EndWhatsNewPreview()
    {
        WhatsNewPlate.Visibility = Visibility.Collapsed;
        WhatsNewPlateBox.Child = null;
        ViewModel.PreviewHint = AppTexts.PreviewClickToPlan;
    }
}
