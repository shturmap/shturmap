#if DEVTOOLS
using Microsoft.UI.Xaml;
using Shturmap.App.Controls;
using Shturmap.Session;
using Shturmap.Session.Dev;
using Shturmap.Session.Reporting;

namespace Shturmap.App;

// The developer view's ways into the main window (docs/DESIGN.md §8, "Developer aids"): F12 opens the view, F9 takes
// the last position again; the view's triggers open the window's own dialogs and lines.
public sealed partial class MainWindow
{
    private Dev.DevController? _dev;
    private Dev.DevView? _devView;

    /// <summary>Called once the session has started: the developer view's controller, on the session's fake game
    /// (null when this session reads the real game).</summary>
    public void DevInstall(GameSession session, FakeGame? game)
    {
        _dev = new Dev.DevController(this, session, game);
        if (game is not null)
            Title = "Shturmap · developer view";
    }

    private void AddDevShortcuts(Action<Windows.System.VirtualKey, Action> add)
    {
        add(Windows.System.VirtualKey.F12, DevOpenView);
        add(Windows.System.VirtualKey.F9, () => _dev?.Repeat());
    }

    public void DevOpenView()
    {
        if (_dev is null)
            return;
        if (_devView is null)
        {
            _devView = new Dev.DevView(_dev);
            _devView.Closed += (_, _) =>
            {
                _dev.Picking = false;
                _devView = null;
            };
        }
        _devView.Activate();
    }

    public Task DevRunScriptAsync(string path) => _dev?.RunScriptAsync(path) ?? Task.CompletedTask;

    internal MapView DevMapView => Map;

    internal SessionSnapshot? DevSnapshot => Volatile.Read(ref _snapshot);

    internal void DevShowReport() => OpenReport(ReportKind.Problem, null, "devview");

    /// <summary>A quest's card held, as "--show-quest" does (for a script's snapshot).</summary>
    internal void DevShowQuest(string name)
    {
        ShowQuest = name;
        if (_snapshot is { } s)
            ShowQuestForSnapshot(s);
    }

    /// <summary>
    /// Points at a quest, one of its objectives or an item as the pointer on its line would (for a script's
    /// snapshot): the same focus, with no mouse to hold it or to let go of it. Null points at nothing again.
    /// </summary>
    internal void DevPoint(Focus? focus)
    {
        ScriptedPointer = focus is not null;
        Linked.Set(focus);
    }

    /// <summary>
    /// Moves the pointer, as far as the linked highlight goes, onto a quest's block in the lists and from there into
    /// one of its objectives' lines ("&lt;objective id&gt;"), its first need cell ("cell") or a gold line that is a
    /// key ("key"); with no quest, out of the innermost thing it is in. It runs the code the pointer's own events
    /// call, so a script can check what a mouse would do without one. Returns what it couldn't find, or null.
    /// </summary>
    internal string? DevHover(string? questId, string? objectiveId = null, string? part = null)
    {
        if (questId is null)
        {
            if (Linked.DevSource is { } source)
                Linked.DevPointer(source, enters: false);
            ScriptedPointer = Linked.DevSource is not null;
            return null;
        }
        var root = Content.XamlRoot;
        // The quest's own block: not the glance's NEXT (an objective's row), not a glyph on a folded card.
        var row = Linked.DevLive(root)
            .Where(e => Linked.GetQuest(e) == questId && Linked.GetObjective(e) is null && !Linked.GetInline(e) && e.ActualHeight > 0 && !_cards.Contains(e))
            .OrderByDescending(e => e.ActualHeight).FirstOrDefault();
        if (row is null)
            return "no row of that quest in the lists";
        var inside = Linked.DevLive(root, row).ToList();
        var chain = new List<FrameworkElement> { row };
        switch (part)
        {
            case "cell":
                if (inside.OfType<Picture>().FirstOrDefault(e => Linked.GetItem(e) is not null) is not { } cell)
                    return "no need cell on that quest's row";
                chain.Add(cell);
                break;
            case "key":
                if (inside.FirstOrDefault(e => e is Microsoft.UI.Xaml.Controls.Border && Linked.GetItem(e) is not null) is not { } key)
                    return "no line that is one key under that quest";
                chain.AddRange(inside.Where(e => Linked.GetObjective(e) is not null && Linked.DevLive(root, e).Contains(key)));
                chain.Add(key);
                break;
            default:
                if (objectiveId is null)
                    break;
                if (inside.FirstOrDefault(e => Linked.GetObjective(e) == objectiveId) is not { } line)
                    return "no line of that objective under the quest";
                chain.Add(line);
                break;
        }
        ScriptedPointer = true;
        foreach (var element in chain)
            Linked.DevPointer(element, enters: true);
        return null;
    }

    /// <summary>
    /// Tells the cards where the pointer has been, in the window's coordinates, as the window does with every move
    /// of the mouse: a script can then check what a pointer heading for a card does (CardAim) without one.
    /// </summary>
    internal void DevTrail(IEnumerable<Windows.Foundation.Point> places)
    {
        foreach (var place in places)
            _cards.PointerAt(place);
    }

    /// <summary>The open cards' titles, first to last ("none" without any), a held one marked.</summary>
    internal string DevCards()
    {
        var cards = _cards.Cards.OfType<ICard>().Select(c => c.Mode == CardMode.Held ? c.Title + " (held)" : c.Title).ToList();
        return cards.Count == 0 ? "none" : string.Join(" > ", cards);
    }
    internal void DevShowCrash()
    {
        CrashRecord record;
        try
        {
            throw new InvalidOperationException("Example crash from the developer view");
        }
        catch (InvalidOperationException e)
        {
            record = CrashRecords.FromException(e, "devview", "ui", true, App.Reporter.Info, AppLog.Tail(5), DateTime.Now, null);
        }
        AskAboutCrashes([record]);
    }

    /// <summary>The line an update ready to apply shows (with RESTART NOW, which does nothing here: no update is
    /// downloaded).</summary>
    internal void DevShowUpdateReady(string version)
    {
        var decision = UpdatePolicy.Decide(UpdateMode.Automatic, canUpdate: true, ViewModel.InRaid, UpdateStage.Ready, version);
        ViewModel.UpdateLine = decision.Line;
        ViewModel.UpdateOffersDownload = decision.OfferDownload;
        ViewModel.UpdateOffersRestart = decision.OfferRestart;
    }

    /// <summary>The window and the map as PNGs (as "--snapshot" does), and the developer view when it is open.</summary>
    internal async Task DevSnapshotAsync(string folder)
    {
        await SaveSnapshotAsync(folder);
        if (_devView is not null)
            await _devView.SaveSnapshotsAsync(folder);
    }

    internal Task DevRenderAsync(UIElement element, string path) => RenderToPngAsync(element, path);

    /// <summary>What's New of the newest version, as help's link shows it; with a line (counted from 1), the pointer on
    /// it: its preview starts at once.</summary>
    internal string? DevWhatsNew(int line)
    {
        if (WhatsNewSections.FirstOrDefault() is not { } newest)
            return "no What's New in this build";
        ShowWhatsNew([newest], "dev");
        if (line < 1)
        {
            // The pointer off the card: a preview ends, as it does when the pointer leaves a line.
            Preview(null, TimeSpan.FromMilliseconds(1));
            return null;
        }
        if (line > newest.Items.Count)
            return $"What's New {newest.Label} has {newest.Items.Count} lines";
        Preview(WhatsNewPreviewPrefix + $"{newest.Label}:{line - 1}", TimeSpan.FromMilliseconds(1));
        return null;
    }

    /// <summary>The made-up raid of the previews replayed on Customs, as at a raid's end (the map switches to Customs).</summary>
    internal async Task<string?> DevReplayAsync()
    {
        if (_snapshot?.Data is not { } data || data.MapByNormalizedName("customs") is not { } customs)
            return "no data for Customs";
        var content = Shturmap.Map.MapContentBuilder.Build(data, customs.Id, [], new HashSet<string>());
        if (ReplayExample(content) is not { } example)
            return "no example raid on Customs";
        if (_snapshot.Map?.NormalizedName != "customs")
            await _session.SelectMapAsync("customs");
        // The artwork of a map just picked takes a moment; the replay waits for its scene (ApplyReplay).
        ShowCue(new ViewCue(CueKind.RaidOver, example.MapName, TimeSpan.FromMinutes(example.Minutes), Replay: example), "dev");
        return null;
    }

    internal void DevExit()
    {
        _devView?.Close();
        ((App)Application.Current).EndSession();
        Application.Current.Exit();
    }
}
#endif
