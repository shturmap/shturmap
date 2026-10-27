using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Rules;
using Shturmap.Core;
using Shturmap.Core.Planning;
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
        // The card stands in Plan's rail and previews on the map: the desk gives them their place back.
        if (sections.Count > 0)
            LeaveDesk("whatsnew");
        RenderWhatsNew(sections);
        if (sections.Count > 0)
            Study.Ui("whatsnew.show", ("how", how), ("versions", string.Join(",", sections.Select(x => x.Label))));
    }

    private void RenderWhatsNew(IReadOnlyList<WhatsNew.Section> sections)
    {
        // The rows are made anew: a line held lets go (its row is gone), and its preview goes as leaving a line makes it go.
        var held = _whatsNewHeld is not null;
        LetGoWhatsNew();
        if (held)
            Preview(null, PreviewEndAfter);
        _whatsNewShown = sections;
        ViewModel.WhatsNewBlocks = sections
            .Select(section => new WhatsNewBlock(WhatsNew.Heading(section),
                section.Items.Select((item, i) => new WhatsNewRow($"{section.Label}:{i}", item.Name, item.Text, Swatch(item.Preview), item.Preview == "clock",
                    item.Preview.StartsWith("stash", StringComparison.Ordinal), AboutTour: !WhatsNew.ShowsPreview(item.Preview), Notes: item.Preview == "notes"))
                    .ToList()))
            .ToList();
        // The note at its foot says what its lines do (Rules.WhatsNew.FootOf): none where no line previews anything.
        ViewModel.WhatsNewFoot = WhatsNew.FootOf(sections) == WhatsNew.Foot.PointAt ? ViewTexts.WhatsNewPointAt : "";
        ViewModel.WhatsNewShown = sections.Count > 0;
    }

    // A line's picture: the map's own symbol, drawn by its renderer as help's legend rows are; for 0.7.0's features a picture
    // of the feature drawn by the map's code (Shturmap.Map.FeatureGlyphs; owner, 2026-10-27, D4 B); the clock's is drawn in
    // XAML.
    private static ImageSource? Swatch(string preview)
    {
        FeatureGlyph? feature = preview switch
        {
            "desk" => FeatureGlyph.Desk,
            "tasks" => FeatureGlyph.Tasks,
            "textsize" => FeatureGlyph.TextSize,
            "glance" => FeatureGlyph.Glance,
            "grid" => FeatureGlyph.Grid,
            "desk-look" => FeatureGlyph.DeskLook,
            "prices" => FeatureGlyph.Prices,
            "grid-pointer" => FeatureGlyph.GridPointer,
            _ => null,
        };
        if (feature is { } f)
        {
            using var picture = FeatureGlyphs.Draw(f, 2);
            return picture.ToWriteableBitmap();
        }
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
        if (sender is Grid row)
            WhatsNewPointerEntered(row);
    }

    // The pointer onto a line (and a developer script's "whatsnew click"): the line takes the linked tint, and its
    // preview shows once the pointer rests there; on an older version's line about the tour, which shows nothing, the
    // line held stays (Preview).
    private void WhatsNewPointerEntered(Grid row)
    {
        if (row.Tag is not string key)
            return;
        row.Background = Resource("LinkBrush");
        Preview(WhatsNewPreviewOf(key), PreviewAfter);
    }

    // What resting on a line asks for: its preview, or nothing for an older version's line about the tour
    // (Rules.WhatsNew.ShowsPreview). A developer script's "whatsnew <n>" asks the same.
    private string? WhatsNewPreviewOf(string key) =>
        WhatsNewItem(key) is { } item && !WhatsNew.ShowsPreview(item.Preview) ? null : WhatsNewPreviewPrefix + key;

    // The preview of the line a click holds, while the card is up: what the map comes back to when nothing else is
    // asked for (Preview).
    private string? WhatsNewHeldPreview => _whatsNewHeld is { } key && ViewModel.WhatsNewShown ? WhatsNewPreviewPrefix + key : null;

    // The line a click holds (Rules.WhatsNew.Click), and its row, which keeps the linked tint while the pointer is away.
    private string? _whatsNewHeld;
    private Grid? _whatsNewHeldRow;

    // The line held has shown its preview once: coming back, it shows its end at once.
    private bool _whatsNewHeldPlayed;

    private void OnWhatsNewTapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid row)
            WhatsNewTapped(row);
    }

    // A click on a line holds its preview, in place of any held before; on the line held, it lets go (owner, 2026-10-27,
    // D4 B; Rules.WhatsNew.Click). What's New no longer opens the tour: "when I click on them the standard tour of the app
    // starts. This should not be the case" (the tour stays in help). Held, the preview shows at once if the pointer's rest
    // hasn't shown it yet, and stays when the pointer leaves the card; let go, it goes as the pointer leaves the line.
    private void WhatsNewTapped(Grid row)
    {
        if (row.Tag is not string key || WhatsNewItem(key) is not { } item)
            return;
        var held = WhatsNew.Click(_whatsNewHeld, key, item.Preview);
        if (held == _whatsNewHeld)
            return;
        if (_whatsNewHeldRow is { } before && !ReferenceEquals(before, row))
            before.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _whatsNewHeld = held;
        _whatsNewHeldRow = held is null ? null : row;
        // Held while it shows: it has played (or is playing), and comes back at its end.
        _whatsNewHeldPlayed = held is not null && _previewing == WhatsNewPreviewPrefix + held;
        Study.Ui("whatsnew.hold", ("item", item.Preview), ("held", held is not null));
        AppLog.Info(held is null ? $"What's New: {key} let go" : $"What's New: {key} held");
        if (held is not null)
            Preview(WhatsNewPreviewPrefix + held, TimeSpan.FromMilliseconds(1));
    }

    // The line held lets go, its row its tint: the preview ended by something else (EndPreview), the card made anew.
    private void LetGoWhatsNew()
    {
        _whatsNewHeld = null;
        _whatsNewHeldPlayed = false;
        if (_whatsNewHeldRow is { } row)
            row.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        _whatsNewHeldRow = null;
    }

    // The line a row's key ("0.4.0:2") stands for.
    private WhatsNew.Item? WhatsNewItem(string key)
    {
        var colon = key.LastIndexOf(':');
        return colon > 0 && int.TryParse(key[(colon + 1)..], out var index)
            && WhatsNewSections.FirstOrDefault(s => s.Label == key[..colon]) is { } section && index < section.Items.Count
            ? section.Items[index] : null;
    }

    private void OnWhatsNewPointerExited(object sender, PointerRoutedEventArgs e) => WhatsNewPointerExited(sender as Grid);

    // The pointer off a line (and a developer script's "whatsnew leave"): its tint goes, but on the line held, and its
    // preview a moment later, the held one coming back in its place (Preview).
    private void WhatsNewPointerExited(Grid? row)
    {
        if (row is not null && !ReferenceEquals(row, _whatsNewHeldRow))
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
        ViewModel.PreviewHint = item.Preview switch
        {
            "notes" => AppTexts.PreviewExampleNotes,
            "desk" or "desk-look" or "prices" => AppTexts.PreviewExampleStash,
            "tasks" => AppTexts.PreviewExampleTasks,
            "textsize" => AppTexts.PreviewExampleSetting,
            _ when item.Preview.StartsWith("stash", StringComparison.Ordinal) => AppTexts.PreviewExampleStash,
            _ => AppTexts.PreviewExample,
        };
        Study.Ui("whatsnew.point", ("version", section.Label), ("item", item.Preview));
        if (!WhatsNew.StagesMap(item.Preview))
            PlayersMapUnder(wanted);
        // A moving preview plays once and holds its end; its end at once with Windows' animation effects off, in a
        // snapshot, and for the line held coming back after another was pointed at: it has played (Rules.WhatsNew.Moves).
        var held = wanted == WhatsNewHeldPreview;
        var motion = !SnapshotMode && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled && !(held && _whatsNewHeldPlayed);
        _whatsNewHeldPlayed |= held;
        switch (item.Preview)
        {
            case "desk":
                PreviewDesk(wanted, data, motion);
                break;
            case "tasks":
                // The cue of the tasks read, with three made-up dailies and weeklies, each on its map, as a reading of the
                // Tasks screen calls it; it stays while the line is pointed at or held (MainWindow.TasksCue).
                _tasksExample = true;
                ShowCue(new ViewCue(CueKind.TasksRead, "", Tasks: TasksExample(data, DateTimeOffset.Now)), example: true);
                break;
            case "textsize":
                PreviewTextSize(data);
                break;
            case "glance":
                PreviewGlance(wanted, data, motion);
                break;
            case "grid":
                await PreviewGridAsync(wanted, data, motion);
                break;
            case "desk-look":
                PreviewDeskLook(wanted, data, motion);
                break;
            case "prices":
                PreviewPrices(wanted, data, motion);
                break;
            case "grid-pointer":
                await PreviewGridPointerAsync(wanted, data, motion);
                break;
            case "clock":
                // Not on the map: the raid card's own clock, with example times, on a plate over the dimmed map.
                WhatsNewPlateBox.Child = new Controls.RaidClock { Reading = RaidTime.Of(true, false, DateTime.Now.AddMinutes(-27), 40, DateTime.Now) };
                WhatsNewPlateNote.Text = AppTexts.WhatsNewClockExample;
                WhatsNewPlate.Visibility = Visibility.Visible;
                break;
            case "stash":
                // The SEEN IN YOUR STASH cue with example items, as a reading shows it; it stays while the pointer rests
                // on the line (MainWindow.StashCue).
                ShowCue(StashExampleCue(data), example: true);
                break;
            case "stash-card":
            case "stash-given":
                // An item card with a made-up mark, on the plate: from a screenshot, with NOT ANY MORE; or from the
                // game's log. The card is its own frame, so the plate's box steps back.
                if (StashExampleCard(data, given: item.Preview == "stash-given") is { } card)
                {
                    WhatsNewPlateBox.Child = card;
                    WhatsNewPlateBox.Padding = new Thickness(0);
                    WhatsNewPlateBox.BorderThickness = new Thickness(0);
                    WhatsNewPlateBox.Background = null;
                    WhatsNewPlateNote.Text = AppTexts.WhatsNewStashExample;
                    WhatsNewPlate.Visibility = Visibility.Visible;
                }
                break;
            case "extracts":
                // The example list read on Customs and called out as a list just read is: YOUR EXTRACTS THIS RAID with its
                // rows, "An example, not your raid." under the title, each icon flown to its place (MainWindow.RollCall).
                await PreviewSceneAsync(wanted, data, "customs", [], (scene, content) => StageExtractList(data, scene, content), null, 0, pmc: true);
                if (_previewing == wanted && Map.Scene is { } listed)
                    RollCallExample(listed, RollOwner.WhatsNew);
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
            case "notes":
                // A held quest card with two made-up notes, on the plate as the stash card is; its own frame, so the
                // plate's box steps back.
                if (NotesExampleCard(data) is { } questCard)
                {
                    WhatsNewPlateBox.Child = questCard;
                    WhatsNewPlateBox.Padding = new Thickness(0);
                    WhatsNewPlateBox.BorderThickness = new Thickness(0);
                    WhatsNewPlateBox.Background = null;
                    WhatsNewPlateNote.Text = AppTexts.WhatsNewNotesExample;
                    WhatsNewPlate.Visibility = Visibility.Visible;
                }
                break;
        }
    }

    // A line that lays only a plate or a cue over the map shows it over the player's map, so where the line before staged a
    // map of its own, the player's comes back first, made anew at the view it had, as leaving the card would give it back
    // (Rules.WhatsNew.StagesMap; UI QA re-check of the grid's fixes, 2026-10-26, R3: the clock stood over the extract list's
    // Customs, the stash cue and the notes card over the joined objectives' Woods, until the pointer left the card).
    private void PlayersMapUnder(string wanted)
    {
        if (_snapshot is { Definition: { } definition } s && _scenes.MakeAnew(definition.Key))
            UpdateMap(s, under: wanted);
    }

    // A preview's map: its artwork, the given quests' places (none of the player's), staged and framed.
    /// <param name="foot">DIPs at the map's foot that what is framed stays above (the tour's band).</param>
    /// <param name="padding">DIPs around what is framed (the tour's are wider); the right side keeps the label room
    /// (<see cref="Controls.MapView.FramingAbove"/>).</param>
    /// <param name="pmc">A PMC raid's map, as an example extract list is read in one: only that side's extracts.</param>
    /// <param name="head">DIPs at the map's top that what is framed stays under (the tour's label; QA pass, 2026-10-15).</param>
    private async Task PreviewSceneAsync(string wanted, Shturmap.Data.TarkovDev.GameData data, string mapName, IReadOnlyCollection<string> quests,
        Action<MapScene, MapContent>? stage, Func<MapContent, IReadOnlyCollection<WorldPoint>?>? frame, double minMetres, double foot = 0, double padding = 40,
        bool pmc = false, double head = 0)
    {
        if (data.MapByNormalizedName(mapName) is not { } map || data.DefinitionFor(mapName) is not { } definition || _session.Artwork is null)
            return;
        var artwork = await ArtworkFor(definition, map.Name);
        if (_previewing != wanted)
            return;
        var ids = data.Tasks.Values.Where(t => t.NormalizedName is { } n && quests.Contains(n)).Select(t => t.Id).ToList();
        var content = MapContentBuilder.Build(data, map.Id, ids, new HashSet<string>());
        // As a PMC raid's map shows it (GameSession's content in a raid): the Scav extracts go. Left in, tarkov.dev's
        // Scav twins of Crossroads, RUAF Roadblock, Trailer Park, Old Gas Station and ZB-1011, under 5 m from them,
        // stood unlit beside the example list's lit and struck-out ones, set apart on leaders (found 2026-10-15 in
        // frames drawn of the example, looking into the owner's report on What's New's tour:5 line).
        if (pmc)
            content = content with { Markers = content.Markers.Where(m => m.Kind != MarkerKind.ExtractScav).ToList() };
        var scene = new MapScene(definition, artwork, artwork is null ? TilesFor(definition, map.Name) : null)
            { Markers = content.Markers, Zones = content.Zones, Containers = content.Containers, QuestKeys = content.QuestKeys, QuestBosses = content.QuestBosses };
        stage?.Invoke(scene, content);
        var points = frame?.Invoke(content);
        Map.SetScene(scene, points is { Count: > 0 } ? Map.FramingAbove(scene, points, padding, foot, minMetres, head) : null);
        _scenes.Forget();
        ShowFloors();
    }

    // An example extract list on Customs: some lit, one marked "??:??:??", the rest struck out; called out once the scene
    // is in the view (RollCallExample), as a list just read is.
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

    // The example item card: the first of the example items, with a STASH line of today's date, solid (a card beside
    // its row is see-through; on the plate there is nothing under it).
    private Controls.ItemCard? StashExampleCard(Shturmap.Data.TarkovDev.GameData data, bool given)
    {
        if (_snapshot is not { } s || StashExampleItems(data).FirstOrDefault() is not { Id: { } itemId } example)
            return null;
        var date = UiLanguage.DayMonthTime(DateTime.Now);
        // A key says so even when none of the player's quests needs it (the card learns that from the quests).
        var view = ItemCards.Build(data, s.Sources, s.Quests, itemId, s.Done) with
        {
            IsKey = example.Kind == RequirementKind.Key,
            Stash = given ? SessionTexts.MarkGiven(date: date) : SessionTexts.MarkSeenCount(count: 2.ToString("N0", UiLanguage.Culture), date: date),
        };
        return new Controls.ItemCard(view) { Opacity = 1 };
    }

    // The example quest card for the notes' preview (owner, 2026-10-15; docs/DESIGN.md §4, "Quest cards", Notes): Ballet
    // Lover, or where the data has none by that name the first quest by name with a place on a map, active, with two
    // made-up notes written today, held, so its field and each note's × show. Nothing of the player's: neither the
    // quest's state nor their notes.
    private Controls.QuestCard? NotesExampleCard(Shturmap.Data.TarkovDev.GameData data)
    {
        var quest = data.Tasks.Values.FirstOrDefault(t => t.NormalizedName == "ballet-lover")
            ?? data.Tasks.Values.OrderBy(t => t.Name, StringComparer.Ordinal).FirstOrDefault(t => t.Objectives?.Any(o => o.Maps is { Count: > 0 }) == true);
        if (quest is null)
            return null;
        // Both of today: the older one 40 minutes before, but never before midnight.
        var now = DateTimeOffset.Now;
        var older = now - TimeSpan.FromMinutes(Math.Min(40, now.TimeOfDay.TotalMinutes));
        IReadOnlyList<QuestNote> notes =
        [
            new("example-newer", now, AppTexts.WhatsNewNoteExampleNewer),
            new("example-older", older, AppTexts.WhatsNewNoteExampleOlder),
        ];
        var active = new Dictionary<string, Shturmap.Core.Quests.QuestStatus>(StringComparer.Ordinal)
            { [quest.Id] = new(quest.Id, Shturmap.Core.Quests.QuestState.Active, null, null) };
        var view = QuestCards.Build(data, active, quest.Id, sources: _snapshot?.Sources,
            notes: new Dictionary<string, IReadOnlyList<QuestNote>>(StringComparer.Ordinal) { [quest.Id] = notes });
        if (view is null)
            return null;
        var card = new Controls.QuestCard(view);
        card.SetMode(Controls.CardMode.Held);
        return card;
    }

    // The plate stands in the middle of the map, and lower only as far as it must to keep clear of the notes at the map's
    // top, its preview label among them (QA pass of 2026-10-15: at 900×560 the label, in two lines now, lay over the item
    // card's top), and higher only as far as it must to keep clear of the map's foot, WIKI MAP and the credit. Where it is
    // taller than the room between them, its box is drawn smaller to fit, never cut, as the tour's plates are, and its
    // caption stands whole under it (QA pass 3, 2026-10-15: at 900×560 the notes' example card was cut at its top, its
    // portrait, name and pen gone, its caption not drawn and its foot under WIKI MAP and the credit). Centred between two
    // paddings, the plate moves by half of their difference.
    private void PlaceWhatsNewPlate()
    {
        const double gap = 8;
        var height = WhatsNewPlate.ActualHeight;
        if (height <= 0 || WhatsNewPlate.Visibility != Visibility.Visible)
            return;
        var top = MapNotes.Margin.Top + MapNotes.ActualHeight + gap;
        var bottom = height - (MapFoot.ActualHeight > 0 ? MapFoot.Margin.Bottom + MapFoot.ActualHeight : 0) - gap;
        // The box at its own size (measured whole in its Viewbox), and the room the caption leaves it.
        var box = WhatsNewPlateBox.DesiredSize.Height;
        var caption = WhatsNewPlateStack.Spacing + WhatsNewPlateNote.ActualHeight;
        var room = Math.Max(40, bottom - top - caption);
        var fit = box > room + 0.5 ? room : double.PositiveInfinity;
        var was = WhatsNewPlateFit.MaxHeight;
        if (double.IsInfinity(fit) != double.IsInfinity(was) || (!double.IsInfinity(fit) && Math.Abs(fit - was) > 0.5))
        {
            WhatsNewPlateFit.MaxHeight = fit;
            // In the app log (developer detail), for the QA's stills.
            if (!double.IsInfinity(fit))
                AppLog.Debug(FormattableString.Invariant($"What's New's plate: its box drawn at {fit / box:0.00}, {fit:0} of {box:0} DIP"));
        }
        var stack = Math.Min(box, room) + caption;
        var at = Math.Clamp((height - stack) / 2, top, Math.Max(top, bottom - stack));
        var shift = 2 * at - (height - stack);
        var padding = shift >= 0 ? new Thickness(0, shift, 0, 0) : new Thickness(0, 0, 0, -shift);
        if (Math.Abs(WhatsNewPlate.Padding.Top - padding.Top) > 0.5 || Math.Abs(WhatsNewPlate.Padding.Bottom - padding.Bottom) > 0.5)
            WhatsNewPlate.Padding = padding;
    }

    private void EndWhatsNewPreview()
    {
        // A moving preview's steps stop where they are, and the grid shown for its preview goes where the player has it off.
        StopWhatsNewSteps();
        PutGridBack();
        EndWhatsNewPointer();
        WhatsNewPlate.Visibility = Visibility.Collapsed;
        WhatsNewPlateBox.Child = null;
        WhatsNewPlateBox.Width = PlateWidth;
        WhatsNewPlateBox.Padding = new Thickness(20, 18, 20, 18);
        WhatsNewPlateBox.BorderThickness = new Thickness(1);
        WhatsNewPlateBox.Background = Resource("RailBrush");
        WhatsNewPlateBox.Opacity = 1;
        WhatsNewPlateRise.Y = 0;
        ViewModel.PreviewHint = AppTexts.PreviewClickToPlan;
        // The cue of the made-up tasks ends with its preview, as the stash cue's example does.
        if (_tasksExample)
        {
            _tasksExample = false;
            EndTasksRead();
            ClearTasksRows();
            _cueStory?.Stop();
            _cueTimer?.Stop();
            CuePanel.Visibility = Visibility.Collapsed;
        }
        // The stash cue shown as an example ends with its preview, and so does the example list's roll call.
        if (_stashExample || (_rollRows.Count > 0 && _rollOwner == RollOwner.WhatsNew))
        {
            EndStashSeen();
            EndRollCall();
            _cueStory?.Stop();
            _cueTimer?.Stop();
            CuePanel.Visibility = Visibility.Collapsed;
        }
    }
}
