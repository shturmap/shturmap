#if DEVTOOLS
using Microsoft.UI.Xaml;
using Shturmap.App.Controls;
using Shturmap.App.Rules;
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
        if (Quiet)
        {
            // A script's run takes pictures (owner, 2026-10-15, "quiet local runs"): the view stands beside the main
            // window, off every monitor as that one is and not over it, shown without activation and out of the
            // taskbar and Alt+Tab. Its two pictures are rendered from its XAML (DevView.SaveSnapshotsAsync).
            var size = _devView.AppWindow.Size;
            var place = WindowPlace.Beside(BoundsNow(), size.Width, size.Height);
            _devView.AppWindow.IsShownInSwitchers = false;
            _devView.AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(place.X, place.Y, place.Width, place.Height));
            _devView.AppWindow.Show(activateWindow: false);
            AppLog.Info($"Developer view off every monitor, beside the window: {place.Width}×{place.Height} at {place.X},{place.Y}");
        }
        else
        {
            _devView.Activate();
        }
    }

    /// <summary>Plays a script (<see cref="Dev.DevController.RunScriptAsync"/>); true when its "exit" ended the app.</summary>
    public Task<bool> DevRunScriptAsync(string path) => _dev?.RunScriptAsync(path) ?? Task.FromResult(false);

    internal MapView DevMapView => Map;

    internal SessionSnapshot? DevSnapshot => Volatile.Read(ref _snapshot);

    internal void DevShowReport() => OpenReport(ReportKind.Problem, null, "devview");

    /// <summary>
    /// Holds a trader's focus as a click on its portrait in the strip does (by its name in the data's language, its
    /// normalized name or its id); null lets go, as Esc does with no card open. Returns what it couldn't find, or null.
    /// </summary>
    internal string? DevHoldTrader(string? name)
    {
        if (name is null)
        {
            HoldTrader(null, "dev");
            return null;
        }
        if (_snapshot?.Data is not { } data)
            return "no game data yet";
        if (TraderNamed(data, name) is not { } trader)
            return "no trader by that name";
        HoldTrader(trader.Id, "dev");
        return null;
    }

    /// <summary>Settings' Text size set to a step ("normal", "larger", "largest"), as its row sets it; what the factor
    /// comes to, for the view's log ("Text size: ×1.5 (Windows 1, settings largest)").</summary>
    internal string DevTextSize(string step)
    {
        ChooseTextSize(Rules.TextSize.Parse(step));
        return TextScale.Said("Text size");
    }

    /// <summary>Settings' Language set to a culture's language for this run, as a choice there sets it, not saved; what
    /// the session chose, for the view's log. The game data comes in it a moment later, when its language changed.</summary>
    internal async Task<string> DevLanguageAsync(string culture)
    {
        if (Shturmap.Core.UiLanguage.CodeOf(culture) is not { } code)
            return $"language {culture}: not a culture this Windows knows";
        await SwitchLanguageAsync(code, save: false);
        return "Language: " + GameSession.Describe(_session.Language);
    }

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
        if (DevQuestRow(questId) is not { } row)
            return "no row of that quest in the lists";
        // In view first, as a player scrolls to a row before resting on it.
        row.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
        ((FrameworkElement)Content).UpdateLayout();
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

    // A quest's own block in the lists: not the glance's NEXT (an objective's row), not a glyph on a folded card.
    private FrameworkElement? DevQuestRow(string questId) =>
        Linked.DevLive(Content.XamlRoot)
            .Where(e => Linked.GetQuest(e) == questId && Linked.GetObjective(e) is null && !Linked.GetInline(e) && e.ActualHeight > 0 && !_cards.Contains(e))
            .OrderByDescending(e => e.ActualHeight).FirstOrDefault();

    /// <summary>
    /// "show &lt;quest&gt; at row" (owner, 2026-10-15, D10): the quest's card held at its own row in the rail, as a click on
    /// the row holds it: the pointer onto the row, then the tap, through the code the pointer's own events call, so the
    /// card opens where a click opens it (a row near the window's foot: the card's place and its move up can be pictured).
    /// The row is scrolled into view first, as a player would scroll to it. Returns what it couldn't find, or null.
    /// </summary>
    internal string? DevHoldAtRow(string questId)
    {
        if (DevQuestRow(questId) is not { } row)
            return "no row of that quest in the lists";
        row.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
        ((FrameworkElement)Content).UpdateLayout();
        ScriptedPointer = true;
        Linked.DevPointer(row, enters: true);
        Linked.DevClick(row);
        return null;
    }

    /// <summary>
    /// "focus &lt;part&gt;" (QA pass, 2026-10-15: no step could focus a status bar button, so no still showed a focus ring):
    /// the keyboard focus on the control of that x:Name, or, where it takes none itself (the map, the rail), on the first
    /// control inside it that does, as Tab moves it there (FocusState.Keyboard: its ring shows). The window isn't activated.
    /// Returns what it couldn't do, or null.
    /// </summary>
    internal async Task<string?> DevFocusAsync(string name)
    {
        if (((FrameworkElement)Content).FindName(name) is not FrameworkElement element || !Shown(element))
            return "no such control shown in the window";
        DependencyObject? target = element is Microsoft.UI.Xaml.Controls.Control { IsTabStop: true, IsEnabled: true } ? element
            : Microsoft.UI.Xaml.Input.FocusManager.FindFirstFocusableElement(element);
        if (target is null)
            return "nothing in it takes the focus";
        var moved = await Microsoft.UI.Xaml.Input.FocusManager.TryFocusAsync(target, FocusState.Keyboard);
        var now = Microsoft.UI.Xaml.Input.FocusManager.GetFocusedElement(Content.XamlRoot);
        var named = (now as FrameworkElement)?.Name is { Length: > 0 } n ? n : now?.GetType().Name ?? "nothing";
        AppLog.Info("Developer view: focus on " + named);
        return moved.Succeeded ? null : "it didn't take the focus";
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

    /// <summary>
    /// An update ready to apply, as a download that just completed leaves it (Updater.DevReady): the band, and the
    /// UPDATE READY cue where it is due, never in a snapshot run (RESTART NOW finds no download here and says so). The
    /// version is the next patch of this one, so both say its line's name ("0.6.1 · OKAMI"); "0.2.1" where this one has
    /// no number. Returns it, or null before the updates have started.
    /// </summary>
    internal string? DevShowUpdateReady()
    {
        if (_updater is null)
            return null;
        var version = WhatsNew.VersionOf(GameSession.Version) is { } v ? $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build) + 1}" : "0.2.1";
        _updater.DevReady(version);
        return version;
    }

    /// <summary>
    /// The window and the map as PNGs (as "--snapshot" does), the cue alone while one is up, and the developer view's two
    /// tabs when it is open. The step ends with the window's pictures: the view's tabs are taken after them while the
    /// script goes on (release UI QA pass of 0.7.0, F2: each picture waits about 0.3 to 0.5 s for a frame in a quiet run,
    /// so the tabs made a step of about 3 s, and a still after another inside a motion, the roll call's t1 and t3, came
    /// after the cue had gone).
    /// </summary>
    internal async Task DevSnapshotAsync(string folder)
    {
        await SaveSnapshotAsync(folder);
        // The cue alone, with its transparency (cue.png), right after map.png: window.png shows the map's panel empty, so
        // a cue's dark band (YOUR EXTRACTS THIS RAID's going with its rows, its title's halo) shows only over map.png, put
        // in at the place the log line gives.
        if (CuePanel.Visibility == Visibility.Visible && CuePanel.ActualHeight > 0)
            await DevCueSnapshotAsync(folder);
        if (_devView is not null)
            _devViewPictures = DevViewPicturesAsync(_devViewPictures, _devView, folder);
    }

    // A cue on its way out: once its fade has ended, until it is taken away a moment later, a still draws it with no pixel
    // (not written); taken away while it is drawn (the fade's end collapses it), the render fails with "Value does not
    // fall within the expected range" (release UI QA pass of 0.7.0, F2: the step failed, and the view's tabs went with
    // it). Each says so in the app log instead.
    private async Task DevCueSnapshotAsync(string folder)
    {
        var at = CuePanel.TransformToVisual(Map).TransformPoint(new Windows.Foundation.Point(0, 0));
        try
        {
            if (await RenderToPngAsync(CuePanel, Path.Combine(folder, "cue.png"), unlessEmpty: true))
                AppLog.Debug(FormattableString.Invariant($"Snapshot: cue.png at {at.X:0},{at.Y:0} DIP in the map's panel"));
            else
                AppLog.Debug("Snapshot: no cue.png, the cue faded out (drawn empty)");
        }
        catch (ArgumentException e)
        {
            AppLog.Debug("Snapshot: no cue.png, the cue gone while it was drawn (" + e.Message + ")");
        }
    }

    // The developer view's tabs of each still, one still's after the other's.
    private Task _devViewPictures = Task.CompletedTask;

    private static async Task DevViewPicturesAsync(Task before, Dev.DevView view, string folder)
    {
        await before;
        try
        {
            await view.SaveSnapshotsAsync(folder);
        }
        catch (Exception e)
        {
            AppLog.Warn("Snapshot: the developer view's pictures failed", e);
        }
    }

    internal Task<bool> DevRenderAsync(UIElement element, string path) => RenderToPngAsync(element, path);

    /// <summary>What's New of the newest version, as help's link shows it, or of the version named (its card alone; owner,
    /// 2026-10-15, D10: 0.4.0's extracts preview); with a line (counted from 1), the pointer on it: its preview starts at
    /// once.</summary>
    internal string? DevWhatsNew(int line, string? version = null)
    {
        if (DevWhatsNewSection(version) is not { } newest)
            return version is null ? "no What's New in this build" : $"no What's New {version} in this build";
        ShowWhatsNew([newest], "dev");
        if (line < 1)
        {
            // The pointer off the card: a preview ends, as it does when the pointer leaves a line.
            Preview(null, TimeSpan.FromMilliseconds(1));
            return null;
        }
        if (line > newest.Items.Count)
            return $"What's New {newest.Label} has {newest.Items.Count} lines";
        // As the pointer's way onto the line asks (WhatsNewPreviewOf): a line about the tour previews nothing, and the
        // preview of the line before ends, as the pointer's leaving that line ends it; no label over nothing staged.
        Preview(WhatsNewPreviewOf($"{newest.Label}:{line - 1}"), TimeSpan.FromMilliseconds(1));
        return null;
    }

    // The What's New line a script's "whatsnew click" put the pointer on, for its "whatsnew leave".
    private Microsoft.UI.Xaml.Controls.Grid? _devWhatsNewRow;

    /// <summary>
    /// A click on the newest version's n-th What's New line (counted from 1), as the mouse makes it: the pointer onto
    /// the line, then the tap, through the code the pointer's own events call, so a line about the tour opens it there
    /// (a script can replay the owner's report of 2026-10-15 on the tour:5 line without a mouse). The card shows first
    /// if it isn't up. Returns what it couldn't find, or null.
    /// </summary>
    internal string? DevWhatsNewClick(int line, string? version = null)
    {
        if (DevWhatsNewSection(version) is not { } newest)
            return version is null ? "no What's New in this build" : $"no What's New {version} in this build";
        if (line < 1 || line > newest.Items.Count)
            return $"What's New {newest.Label} has {newest.Items.Count} lines";
        if (!ViewModel.WhatsNewShown || !_whatsNewShown.Contains(newest))
        {
            ShowWhatsNew([newest], "dev");
            ((FrameworkElement)Content).UpdateLayout();
        }
        var key = $"{newest.Label}:{line - 1}";
        if (DevWhatsNewRows(Content).FirstOrDefault(r => r.Tag as string == key) is not { } row)
            return $"line {line} of What's New {newest.Label} isn't in the window";
        _devWhatsNewRow = row;
        WhatsNewPointerEntered(row);
        WhatsNewTapped(row);
        return null;
    }

    // The newest version's section, or the one a script names ("0.4.0", as docs/whats-new.md heads it).
    private WhatsNew.Section? DevWhatsNewSection(string? version) =>
        version is null ? WhatsNewSections.FirstOrDefault() : WhatsNewSections.FirstOrDefault(s => s.Label == version);

    /// <summary>The pointer leaves the line a script's "whatsnew click" put it on (or, with none, the card), as moving
    /// off it does, the tour's layer coming over it too: what the line's PointerExited does.</summary>
    internal string? DevWhatsNewLeave()
    {
        var row = _devWhatsNewRow;
        _devWhatsNewRow = null;
        WhatsNewPointerExited(row);
        return null;
    }

    // What's New's lines in the window: the rows its card's template made (MainWindow.xaml, WhatsNewRow).
    private static IEnumerable<Microsoft.UI.Xaml.Controls.Grid> DevWhatsNewRows(DependencyObject under)
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(under); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(under, i);
            if (child is Microsoft.UI.Xaml.Controls.Grid { DataContext: WhatsNewRow, Tag: string } row)
            {
                yield return row;
                continue;
            }
            foreach (var deeper in DevWhatsNewRows(child))
                yield return deeper;
        }
    }

    /// <summary>Ends the tour as Esc does ("tour end"); false when none is up.</summary>
    internal bool DevEndTour()
    {
        if (!TourOpen)
            return false;
        CloseTour("dev");
        return true;
    }

    /// <summary>The made-up raid of the previews replayed on Customs, as at a raid's end (the map switches to Customs).</summary>
    internal async Task<string?> DevReplayAsync()
    {
        if (_snapshot?.Data is not { } data || data.MapByNormalizedName("customs") is not { } customs)
            return "no data for Customs";
        var content = Shturmap.Map.MapContentBuilder.Build(data, customs.Id, [], new HashSet<string>());
        if (ReplayExample(data, content) is not { } example)
            return "no example raid on Customs";
        if (_snapshot.Map?.NormalizedName != "customs")
            await _session.SelectMapAsync("customs");
        // The artwork of a map just picked takes a moment; the replay waits for its scene (ApplyReplay).
        ShowCue(new ViewCue(CueKind.RaidOver, example.MapName, TimeSpan.FromMinutes(example.Minutes), Replay: example), "dev");
        return null;
    }

    /// <summary>SEEN IN YOUR STASH with the example items (MainWindow.StashCue), as a reading shows it.</summary>
    internal string? DevStashCue()
    {
        if (_snapshot?.Data is not { } data)
            return "no data";
        ShowCue(StashExampleCue(data), "dev");
        return null;
    }

    /// <summary>The developer script's "glance" (DevScript.GlanceOf): the made-up in-raid glance's reading n, in a raid; what
    /// was done, or why not.</summary>
    internal string DevGlance(int reading)
    {
        if (_snapshot is not { } s)
            return "glance: no snapshot yet";
        if (s.Data is null)
            return "glance: no data yet";
        if (!ViewModel.InRaid)
            return "glance: not in a raid";
        ShowGlanceExample(reading, s);
        return $"glance: reading {reading}" + (GlanceUp ? "" : _glanceWaiting is not null ? " (waiting for the cue to go)" : " (not shown)");
    }

    /// <summary>The developer script's "desk" (DevScript.DeskOf): what was done, or why not.</summary>
    internal string DevDesk(DevScript.DeskStep step)
    {
        if (_snapshot is not { } s)
            return "desk: no snapshot yet";
        switch (step.Act)
        {
            case DevScript.DeskAct.Reading:
                if (s.Data is null)
                    return "desk: no data yet";
                ShowDeskExample(step.Reading, s);
                return $"desk: reading {step.Reading}" + (_deskChosen ? "" : " (not shown: " + State + ")");
            case DevScript.DeskAct.Plan:
                LeaveDesk("dev");
                return "desk: plan";
            case DevScript.DeskAct.Show:
                SetDesk(true, "dev");
                return "desk: shown" + (_deskChosen ? "" : " (not: " + State + ")");
            case DevScript.DeskAct.ShowPin:
                // The n-th pinned line in the rail's foot clicked, from the top, as the pointer's click does.
                if (!_deskChosen)
                    return "desk: the desk isn't up";
                if (step.N > _deskFootKeys.Count)
                    return $"desk: no pinned line {step.N} in the rail's foot ({_deskFootKeys.Count} there)";
                var line = _deskFootKeys[step.N - 1];
                ShowDeskPin(line, "dev");
                return $"desk: showed the pinned {line}";
            case DevScript.DeskAct.Rule:
                SetDeskRule(Core.Desk.DeskCopyRule.Of(step.Rule), "dev");
                return "desk: rule " + _deskRule.Format();
            case DevScript.DeskAct.UnpinAll:
                UnpinAllDesk();
                return "desk: every pin taken";
        }
        if (_deskShown is not { } view || !_deskChosen)
            return "desk: the desk isn't up";
        var key = view.Sheet.Regions.Select(r => r.Region.Key)
            .FirstOrDefault(k => k == step.Region || k == Core.Desk.DeskRegion.WindowKey(step.Region));
        if (key is null)
            return "desk: no region " + step.Region;
        var cell = step.Slot is { } named ? StashDesk.CellOf(new DeskCellKey(key, named, 0, 0))
            : step.I < 0 ? StashDesk.FirstCellOf(key) : StashDesk.CellAt(key, step.I, step.J);
        if (cell is null)
            return $"desk: no cell of {key} at {step.Slot ?? $"{step.I},{step.J}"} on the page";
        var at = cell.Key.ToString();
        switch (step.Act)
        {
            case DevScript.DeskAct.Pin:
                StashDesk.Point(cell);
                return $"desk: {ToggleDeskPin(cell.Key, "dev")} {at}";
            case DevScript.DeskAct.Unpin:
                if (!DeskPins.IsPinned(_deskPins, cell.Key))
                    return $"desk: {at} isn't pinned";
                return $"desk: {ToggleDeskPin(cell.Key, "dev")} {at}";
            case DevScript.DeskAct.Copy:
                StashDesk.Point(cell);
                var copy = CopyDeskCell(cell, "dev", clipboard: false);
                return $"desk: clicked {at}: {copy.State}" + (copy.Copies ? " " + DeskCards.Clipboard(copy) : "");
            case DevScript.DeskAct.Pick:
                var ids = cell.Sheet.Cell.Ids;
                if (ids.Count < 2 || step.N > ids.Count)
                    return $"desk: {at} has no candidate {step.N}";
                // The candidates as its card lists them.
                if (DeskCards.Of(cell.Region, cell.Sheet, DeskContext(view)).Candidates is not { Count: > 0 } candidates || step.N > candidates.Count)
                    return $"desk: {at}'s card lists no candidate {step.N}";
                PickDeskCell(cell.Key, candidates[step.N - 1].ItemId, "dev");
                return $"desk: picked {candidates[step.N - 1].Name} for {at}";
        }
        StashDesk.Point(cell);
        return $"desk: pointed at {at}";
    }

    // After the developer view's last pictures (DevSnapshotAsync).
    internal async Task DevExitAsync()
    {
        await _devViewPictures;
        _devView?.Close();
        ((App)Application.Current).EndSession();
        Application.Current.Exit();
    }
}
#endif
