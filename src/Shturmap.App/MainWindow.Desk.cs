using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Controls;
using Shturmap.App.Rules;
using Shturmap.Core.Desk;
using Shturmap.Core.Logs;
using Shturmap.Session;

namespace Shturmap.App;

// The stash desk's state and page (owner, 2026-10-19, from a panel: A1, and the addendum: A(2) · B1 · C1a · Da;
// docs/DESIGN.md §4, principle 3 and "Screen anatomy", *The stash desk*; step B part 2, the view): PLAN | DESK at the
// rail's head; the desk in the map's place out of a raid, the rail its cards; it opens by itself with a reading in the
// menus, a raid loading takes the window back, and it keeps its page. Step C (owner, 2026-10-19): the desk's own card for
// the cell pointed at and for the pinned ones, a click on a cell copying its list price by the rule on the card, a
// right-click or the card's pin pinning it.
public sealed partial class MainWindow
{
    private bool _deskChosen;
    private int _deskReadingSeen;
    private DeskView? _deskShown;
    private DeskView? _deskExample;
    private bool _deskBoardDue = true;
    private IReadOnlySet<string> _deskEntering = new HashSet<string>();

    // The rail's cards: the cell pointed at; the pinned ones, newest first; the copy's rule; what each card was made of.
    private IReadOnlyList<DeskPin> _deskPins = [];
    private DeskCellKey? _deskPointed;
    private DeskCopyRule _deskRule = DeskCopyRule.Default;
    private (DeskCellKey Key, int Version, bool Pinned)? _deskPointedShown;
    private int _deskVersion;

    // The pinned cells standing as lines in the rail's foot, top first (the developer script's "desk show <n>").
    private IReadOnlyList<DeskCellKey> _deskFootKeys = [];

    // The made-up desk's reading and the picks made on its cards (a developer run's; a real page keeps its picks in desk.json).
    private int _deskExampleReading = 1;
    private readonly Dictionary<DeskCellKey, string> _deskExamplePicks = [];

    // The made-up desk's flea prices against its own (the developer script's "desk prices": new prices, as the session's
    // every five minutes bring them).
    private double _deskExamplePriced = 1;

    // The price histories the cards asked for, by item, for the mode in hand; those on their way.
    private readonly Dictionary<string, DeskHistory?> _deskHistories = new(StringComparer.Ordinal);
    private readonly HashSet<string> _deskHistoriesLoading = new(StringComparer.Ordinal);
    private GameMode _deskHistoriesMode;

    // The crafts and barters of each item, as the item card has them, made once per data and item file.
    private readonly Dictionary<string, IReadOnlyList<DeskCardSource>> _deskSources = new(StringComparer.Ordinal);
    private object? _deskSourcesOf;

    /// <summary>"--desk [&lt;n&gt;]" (a fake game's runs: tools\fake-raid.ps1 -Desk): the made-up desk's reading n
    /// (<see cref="DeskExample"/>) once the data is there, as a reading in the menus brings it.</summary>
    public int? DeskOnStart { get; set; }

    /// <summary>The window's state: Plan, Desk or Raid (<see cref="DeskSwitch"/>).</summary>
    internal WindowState State => DeskSwitch.Shown(ViewModel.InRaid, _deskChosen);

    // The desk on hand: a developer run's made-up one, else the session's.
    private DeskView? DeskNow(SessionSnapshot? s) => _deskExample ?? s?.Desk;

    private void InstallDesk()
    {
        StashDesk.CellPointed += OnDeskCellPointed;
        StashDesk.CellClicked += cell => CopyDeskCell(cell, "click");
        StashDesk.CellPinToggled += cell => ToggleDeskPin(cell.Key, "right-click");
        StashDesk.ClearClicked += () => _ = ClearDeskAsync("click");
        _deskRule = DeskCopyRule.Of(_session.GetSetting(DeskCopyRule.Setting));
        ShowCopyRuleSetting();
        // Minimised or restored: the desk leaves the screen or comes back to it, for its prices.
        AppWindow.Changed += (_, _) => TellDeskOnScreen();
    }

    // Whether the session was last told the desk is on screen.
    private bool _deskOnScreenTold;

    /// <summary>Tells the session whether the desk is on screen (<see cref="DeskSwitch.OnScreen"/>) when that changed: while
    /// it is, the session asks for the desk's prices again as they turn five minutes old, and at once when it comes back
    /// with older ones (owner, 2026-10-26; docs/DESIGN.md §3).</summary>
    private void TellDeskOnScreen()
    {
        var minimised = AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized };
        var on = DeskSwitch.OnScreen(ViewModel.InRaid, _deskChosen, minimised);
        if (on == _deskOnScreenTold)
            return;
        _deskOnScreenTold = on;
        _session.SetDeskOnScreen(on);
    }

    // Each snapshot: a new reading in the menus opens the desk, a raid takes the window, what a reading changed comes in.
    private void UpdateDesk(SessionSnapshot s)
    {
        var view = DeskNow(s);
        var fresh = DeskSwitch.IsNew(view?.Reading, _deskReadingSeen);
        if (view is not null)
            _deskReadingSeen = Math.Max(_deskReadingSeen, view.Reading);
        // A reading since the page was taken away: the notice's UNDO is gone (owner, 2026-10-26).
        if (_noticeUndo is { } undo && !DeskClearing.UndoStands(undo.Reading, view?.Reading))
            DropNoticeUndo();
        var chosen = DeskSwitch.ChosenAfter(_deskChosen, ViewModel.InRaid, fresh, TourOpen);
        if (view is not null && (fresh || !ReferenceEquals(view, _deskShown)))
        {
            var keys = view.Sheet.Regions.Select(r => r.Region.Key).ToList();
            _deskEntering = DeskPageRules.Entering(keys, view.Changed, view.First, fresh);
            if (view.Mode != _deskHistoriesMode)
            {
                _deskHistories.Clear();
                _deskHistoriesMode = view.Mode;
            }
            // The pins stay with their cells; a reading that changed a pinned cell takes its pin. A CLEAR took them all; its
            // UNDO puts its page back, and the pins come back with it.
            if (_deskPinsCleared is { } cleared && ReferenceEquals(cleared.Page, view.Sheet.Page))
            {
                _deskPins = DeskPins.Kept(cleared.Pins, view.Sheet);
                _deskPinsCleared = null;
            }
            else
                _deskPins = DeskPins.Kept(_deskPins, view.Sheet);
            _deskShown = view;
            _deskBoardDue = true;
            _deskVersion++;
        }
        else if (view is null && _deskShown is not null)
        {
            // The mode changed to one without a page: the desk says so.
            _deskShown = null;
            _deskPins = [];
            _deskPointed = null;
            _deskBoardDue = true;
            _deskVersion++;
        }
        SetDesk(chosen, fresh ? "reading" : ViewModel.InRaid ? "raid" : "snapshot");
    }

    /// <summary>Shows the desk or Plan (Raid comes with the game by itself). The desk on screen closes the cards, ends a
    /// map's preview and a replay (the map is out of sight), and draws the page if it is due.</summary>
    private void SetDesk(bool up, string how)
    {
        var was = _deskChosen;
        _deskChosen = up && !ViewModel.InRaid;
        if (_deskChosen != was)
        {
            Study.Ui(_deskChosen ? "desk.open" : "desk.close", ("how", how));
            AppLog.Info(_deskChosen ? $"Stash desk up ({how})" : $"Stash desk left ({how})");
        }
        if (_deskChosen && !was)
        {
            _cards.CloseAll();
            _previewTimer?.Stop();
            EndPreview(restore: false);
            StopReplay("desk");
        }
        ViewModel.DeskUp = _deskChosen;
        ViewModel.PlanUp = !ViewModel.InRaid && !_deskChosen;
        if (_deskChosen && _deskBoardDue)
            ShowDeskPage(motion: !SnapshotMode && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled);
        if (!_deskChosen)
            _deskPointed = null;
        if (_deskChosen || !was)
            RenderDeskRail();
        TellDeskOnScreen();
    }

    /// <summary>
    /// Shturmap's texts changed language (OnUiLanguageChanged): the page's words and figures (the head line, the regions'
    /// heads and the slots' names, the "Not attached" lines, "5.45 M", "−3%"), the cards and settings' row of the copy's rule are made
    /// as they are built, so each is built again in the new language at once: the page on screen standing (nothing comes
    /// in, the totals don't count up), a page out of sight when it next shows (release QA pass of 2026-10-19, F1: the page
    /// kept the old language beside a rail in the new one).
    /// </summary>
    private void RetellDesk()
    {
        _deskVersion++;
        ShowCopyRuleSetting();
        if (!_deskChosen)
        {
            _deskBoardDue = true;
            return;
        }
        ShowDeskPage(motion: false);
        RenderDeskRail();
    }

    // The page as it is; the cells of the regions a new reading changed come in, once.
    private void ShowDeskPage(bool motion)
    {
        _deskBoardDue = false;
        var entering = _deskEntering;
        _deskEntering = new HashSet<string>();
        StashDesk.Show(_deskShown, entering, motion, DeskClearing.Offered(_deskShown));
        StashDesk.ShowPinned(_deskPins.Select(p => p.Key).ToHashSet());
    }

    // PLAN and DESK at the rail's head: a click, or Space or Enter on the half with the focus.
    private void OnDeskSwitchPlanClick(object sender, RoutedEventArgs e) => SetDesk(false, "switch");

    private void OnDeskSwitchDeskClick(object sender, RoutedEventArgs e) => SetDesk(true, "switch");

    // ← and → on the switch pick PLAN or DESK, and the focus goes with the pick (owner, 2026-10-19: a primary control).
    // While the tour is up its own keys step through it.
    private void OnDeskSwitchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (TourOpen || DeskSwitch.ByArrow((int)e.Key) is not { } desk)
            return;
        SetDesk(desk, "key");
        (desk ? DeskSwitchDesk : DeskSwitchPlan).Focus(FocusState.Keyboard);
        e.Handled = true;
    }

    /// <summary>Leaves the desk for Plan, where a choice needs the map or Plan's rail: a map picked in the MAP list, a
    /// trader's focus held, What's New, the tour.</summary>
    private void LeaveDesk(string how)
    {
        if (_deskChosen)
            SetDesk(false, how);
    }

    // The switch's look: the view on screen in amber on Raised with the open map's row's amber bar, the other muted on
    // the rail's ground.
    public Brush DeskSwitchInk(bool deskUp, bool forDesk) => Resource(deskUp == forDesk ? "AmberBrush" : "MutedBrush");

    public Brush DeskSwitchGround(bool deskUp, bool forDesk) =>
        deskUp == forDesk ? Resource("RaisedBrush") : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public Visibility DeskSwitchBar(bool deskUp, bool forDesk) => deskUp == forDesk ? Visibility.Visible : Visibility.Collapsed;

    // The rail's scrolling part: 20 DIP under what scrolls in Plan and a raid; on the desk the cards carry them, so the card
    // in view runs on to the pins' foot (UI QA pass of the second round, F1: at 900×560 the room above the foot is small).
    public Thickness RailPadding(bool deskUp) => new(16, 0, 16, deskUp ? 0 : 20);

    // ---- the rail: the card of the cell pointed at, then the pinned cards ----

    // The pointer on a cell: its card in the rail, until another cell is pointed at, so the way from the page to the rail
    // doesn't lose it.
    private void OnDeskCellPointed(DeskCellView cell)
    {
        if (!_deskChosen || _deskPointed == cell.Key)
            return;
        _deskPointed = cell.Key;
        RenderDeskRail();
    }

    /// <summary>
    /// The rail anew (QA pass of step C, F2: the pointed card first, so pins never push it out of the smallest rail): the
    /// pointed cell's card, or the rail's hint; then the pinned cards, newest on top: while a pointed card stands, each one
    /// compact line (icon, name, gold figure) in the rail's foot, always in sight under the card's room, which scrolls (UI
    /// QA pass of the second round, F1, 2026-10-19: at 1600×900 the second line stood below the window under a key's
    /// card); whole under the hint while none does. A pinned cell pointed at has its card on top, pinned, and not again
    /// among the pins.
    /// </summary>
    private void RenderDeskRail()
    {
        DeskPinnedHost.Children.Clear();
        DeskPinnedFoot.Children.Clear();
        var footKeys = new List<DeskCellKey>();
        RenderDeskPointed();
        if (_deskShown is { } view && _deskChosen)
        {
            var compact = DeskCardHost.Child is not null;
            foreach (var pin in _deskPins)
            {
                if (pin.Key == _deskPointed && compact || DeskPins.Find(view.Sheet, pin.Key) is not { } found)
                    continue;
                if (compact)
                {
                    var key = pin.Key;
                    var words = DeskCards.Of(found.Region.Region, found.Cell, DeskContext(view));
                    DeskPinnedFoot.Children.Add(DeskCardView.CompactLine(words, () => ShowDeskPin(key, "click")));
                    footKeys.Add(key);
                }
                else
                    DeskPinnedHost.Children.Add(DeskCardFor(view, found.Region.Region, found.Cell, pinned: true));
            }
        }
        _deskFootKeys = footKeys;
        DeskFoot.Visibility = footKeys.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        StashDesk.ShowPinned(_deskPins.Select(p => p.Key).ToHashSet());
    }

    /// <summary>A pinned line in the rail's foot clicked: its card whole in the pointed card's place, the card's room
    /// back at its top, so the card is seen from its head, not from where the last one was scrolled to.</summary>
    private void ShowDeskPin(DeskCellKey key, string how)
    {
        Study.Ui("desk.pin.show", ("how", how));
        _deskPointed = key;
        RenderDeskRail();
        RailScroll.ChangeView(null, 0, null, disableAnimation: true);
    }

    // The foot's strip: its first three lines in sight, the fourth and on scrolled to within it (Rules.DeskPins.FootRoom),
    // measured as drawn, so a larger text size keeps three whole lines.
    private void OnDeskFootSized(object sender, SizeChangedEventArgs e)
    {
        var room = DeskPins.FootRoom(DeskPinnedFoot.Children.OfType<FrameworkElement>().Select(l => l.ActualHeight).ToList(), DeskPinnedFoot.Spacing);
        if (DeskFootScroll.MaxHeight != room)
            DeskFootScroll.MaxHeight = room;
    }

    // The pointed cell's card, made again only when the cell or what the card says changed (a pinned cell's framed as
    // pinned), the hint while nothing is pointed at.
    private void RenderDeskPointed()
    {
        if (_deskShown is not { } view || !_deskChosen || _deskPointed is not { } key || DeskPins.Find(view.Sheet, key) is not { } found)
        {
            DeskCardHost.Child = null;
            _deskPointedShown = null;
            DeskRailLine.Text = ViewTexts.DeskRailHint;
            DeskRailLine.Visibility = Visibility.Visible;
            return;
        }
        DeskRailLine.Visibility = Visibility.Collapsed;
        var pinned = DeskPins.IsPinned(_deskPins, key);
        if (_deskPointedShown == (key, _deskVersion, pinned) && DeskCardHost.Child is not null)
            return;
        DeskCardHost.Child = DeskCardFor(view, found.Region.Region, found.Cell, pinned);
        _deskPointedShown = (key, _deskVersion, pinned);
    }

    // A cell's card, its history asked for once when it first shows the item.
    private DeskCardView DeskCardFor(DeskView view, DeskRegion region, DeskCellSheet sheet, bool pinned)
    {
        var id = sheet.Cell.ItemId;
        var loading = false;
        var history = id is null ? null : DeskHistoryOf(id, out loading);
        var words = DeskCards.Of(region, sheet, DeskContext(view), history, loading, id is null ? null : DeskSourcesOf(id));
        var card = new DeskCardView(DeskCellKey.Of(region, sheet.Cell), words, pinned);
        card.PinToggled += c => ToggleDeskPin(c.Key, "card");
        card.RuleStepped += step => SetDeskRule(_deskRule.Step(step), "card");
        card.Picked += (c, item) => PickDeskCell(c.Key, item, "card");
        return card;
    }

    private DeskCardContext DeskContext(DeskView view)
    {
        var s = Volatile.Read(ref _snapshot);
        var data = s?.Data;
        var sources = s?.Sources;
        return new DeskCardContext(view.FactsOf, view.Flea, _deskRule,
            id => data?.ItemName(id) is { Length: > 0 } name ? name : id,
            id => data?.TraderName(id) is { Length: > 0 } name ? name : id,
            id => data?.Tasks.GetValueOrDefault(id)?.Name ?? id,
            id => sources?.Stations.GetValueOrDefault(id) ?? id,
            view.PricesAt, DateTimeOffset.Now);
    }

    // The item's price history: the made-up desk's own; else the one had, or asked for now (the card shows "…" until it
    // comes, then is made again).
    private DeskHistory? DeskHistoryOf(string itemId, out bool loading)
    {
        loading = false;
        if (_deskExample is not null)
            return DeskExample.History(itemId, _deskExample.PricesAt ?? DateTimeOffset.Now);
        if (_deskHistories.TryGetValue(itemId, out var had))
            return had;
        loading = true;
        if (_deskHistoriesLoading.Add(itemId))
            _ = LoadDeskHistoryAsync(itemId, _deskHistoriesMode);
        return null;
    }

    private async Task LoadDeskHistoryAsync(string itemId, GameMode mode)
    {
        DeskHistory? history = null;
        try
        {
            history = await _session.DeskHistoryAsync(itemId);
        }
        catch (Exception e)
        {
            AppLog.Warn("Desk history failed", e);
        }
        _deskHistoriesLoading.Remove(itemId);
        if (mode != _deskHistoriesMode)
            return;
        _deskHistories[itemId] = history;
        _deskVersion++;
        RenderDeskRail();
    }

    // The crafts and barters that give the item, as the item card words them.
    private IReadOnlyList<DeskCardSource> DeskSourcesOf(string itemId)
    {
        var s = Volatile.Read(ref _snapshot);
        if (s?.Data is not { } data)
            return [];
        if (!ReferenceEquals(_deskSourcesOf, s.Sources))
        {
            _deskSources.Clear();
            _deskSourcesOf = s.Sources;
        }
        if (_deskSources.TryGetValue(itemId, out var known))
            return known;
        var made = ItemCards.Build(data, s.Sources, s.Quests, itemId, s.Done).Get
            .Where(g => g.Kind is SourceKind.Barter or SourceKind.Craft)
            .Select(g => new DeskCardSource(g.TraderId, g.Text, g.Kind == SourceKind.Craft)).ToList();
        _deskSources[itemId] = made;
        return made;
    }

    // ---- pins ----

    /// <summary>Pins a cell's card in the rail on top, or unpins it (a right-click on the cell, the pin on its card).</summary>
    private string ToggleDeskPin(DeskCellKey key, string how)
    {
        if (_deskShown is not { } view || DeskPins.Find(view.Sheet, key) is not { } found)
            return "no such cell";
        _deskPins = DeskPins.Toggle(_deskPins, found.Region.Region, found.Cell.Cell);
        var pinned = DeskPins.IsPinned(_deskPins, key);
        Study.Ui(pinned ? "desk.pin" : "desk.unpin", ("how", how), ("pins", _deskPins.Count));
        RenderDeskRail();
        return pinned ? "pinned" : "unpinned";
    }

    /// <summary>Unpins every card (the developer script's "desk unpin all").</summary>
    private void UnpinAllDesk()
    {
        _deskPins = [];
        RenderDeskRail();
    }

    // ---- clearing the page: CLEAR, and the notice's UNDO (owner, 2026-10-26: "Button + fresh after raid") ----

    /// <summary>What a notice's UNDO does, and the desk's reading counter it was offered at (a later reading ends it).</summary>
    private sealed record NoticeUndoAct(int Reading, Func<Task<bool>> Run);

    // The UNDO of the notice standing; null while it offers none.
    private NoticeUndoAct? _noticeUndo;

    // The pins a CLEAR took, with the page they were on: its UNDO puts that page back, and they come back with it.
    private (DeskPage Page, IReadOnlyList<DeskPin> Pins)? _deskPinsCleared;

    /// <summary>The notice went, or another took its place, or a reading came: its UNDO with it.</summary>
    private void DropNoticeUndo()
    {
        _noticeUndo = null;
        ViewModel.NoticeOffersUndo = false;
    }

    /// <summary>
    /// CLEAR in the desk's head clicked (or the developer script's "desk clear"; docs/DESIGN.md §4, *The stash desk*,
    /// *Clearing the page*): the page in hand for the mode emptied at once, its pins and the card pointed at with it, and the
    /// notice "Desk cleared." with UNDO; no question (principle 7). The session clears its page; a developer run's made-up
    /// desk is emptied here. What was done, or why not.
    /// </summary>
    private async Task<string> ClearDeskAsync(string how)
    {
        if (!_deskChosen || _deskShown is not { } shown || !DeskClearing.Offered(shown))
            return "desk: nothing to clear";
        Study.Ui("desk.clear", ("how", how));
        // The card pointed at closes with the page; the pins come back with it on UNDO.
        _deskPinsCleared = (shown.Sheet.Page, _deskPins);
        _deskPointed = null;
        // The empty page stands from its head, as a first reading's does.
        StashDesk.ScrollToTop();
        if (_deskExample is { } example)
        {
            _deskExample = DeskClearing.Emptied(example, DateTimeOffset.Now);
            if (Volatile.Read(ref _snapshot) is { } s)
                UpdateDesk(s);
            ShowNotice(SessionTexts.DeskCleared, TimeSpan.FromSeconds(GameSession.DeskUndoSeconds),
                undo: new NoticeUndoAct(example.Reading, () => Task.FromResult(PutDeskExampleBack(example))));
            AppLog.Info("Example desk cleared");
            return "desk: cleared the made-up desk";
        }
        return await _session.ClearDeskAsync() ? "desk: cleared" : "desk: nothing to clear";
    }

    // The made-up desk's UNDO: the view as it was, while it is still the emptied one (no reading came since).
    private bool PutDeskExampleBack(DeskView example)
    {
        if (_deskExample is not { } now || now.Reading != example.Reading || now.Sheet.Page.Regions.Count > 0)
            return false;
        _deskExample = example;
        if (Volatile.Read(ref _snapshot) is { } s)
            UpdateDesk(s);
        AppLog.Info("Example desk put back");
        return true;
    }

    private async void OnNoticeUndoClick(object sender, RoutedEventArgs e) => await UndoNoticeAsync("click");

    /// <summary>The notice's UNDO clicked (or the developer script's "desk undo"): the page it took away back, standing at
    /// once; the notice goes. What was done, or why not.</summary>
    private async Task<string> UndoNoticeAsync(string how)
    {
        if (_noticeUndo is not { } undo || !ViewModel.NoticeOpen)
            return "desk: no notice offers UNDO";
        Study.Ui("desk.undo", ("how", how));
        ViewModel.NoticeOpen = false;
        DropNoticeUndo();
        _noticeTimer.Stop();
        var done = await undo.Run();
        if (!done)
            AppLog.Info("Desk UNDO came too late: a reading came since");
        return done ? "desk: page put back" : "desk: UNDO too late, a reading came since";
    }

    // ---- the click: the list price on the clipboard ----

    /// <summary>
    /// A click on a cell (*The click*): its list price by the rule to the clipboard, and only there (nothing is ever typed
    /// into the game; DESIGN.md §2); its frame flashes gold once and its figure decodes; a one-line notice says what and
    /// why, or why nothing was copied (the guards). A developer script's click leaves the clipboard alone.
    /// </summary>
    private DeskCopy CopyDeskCell(DeskCellView cell, string how, bool clipboard = true)
    {
        if (_deskPointed != cell.Key)
        {
            _deskPointed = cell.Key;
            RenderDeskRail();
        }
        var view = _deskShown;
        var item = cell.Sheet.Cell.ItemId;
        var copy = DeskCopies.Of(cell.Sheet.Cell, item is null || view is null ? null : view.FactsOf(item), view?.Flea ?? DeskFleaRules.Default, _deskRule);
        var context = view is null ? null : DeskContext(view);
        if (copy.Copies)
        {
            if (clipboard && !TryCopy(DeskCards.Clipboard(copy)))
            {
                ShowNotice(RuleTexts.DeskCopyFailed);
                return copy;
            }
            DeskBoard.Flash(cell);
            if (!SnapshotMode && new Windows.UI.ViewManagement.UISettings().AnimationsEnabled && cell.FigureBlock is { } block && cell.FigureNumber is { } number)
                Decode(block, number, TimeSpan.FromMilliseconds(600));
        }
        ShowNotice(DeskCards.Notice(copy, _deskRule, context?.TraderName ?? (id => id)), TimeSpan.FromSeconds(4));
        Study.Ui("desk.copy", ("state", copy.State.ToString()), ("how", how), ("rule", _deskRule.Format()));
        AppLog.Info($"Desk click on {cell.Key}: {copy.State}" + (copy.Copies ? $" {DeskCards.Clipboard(copy)}" + (clipboard ? "" : " (clipboard left alone)") : ""));
        return copy;
    }

    // The price's digits on the clipboard, kept there after Shturmap closes.
    private static bool TryCopy(string text)
    {
        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(text);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
            return true;
        }
        catch (Exception e)
        {
            AppLog.Warn("Copying a desk price failed", e);
            return false;
        }
    }

    // ---- the rule: on the card and in settings, one state ----

    /// <summary>The copy's rule set (the card's − and +, settings' row, the developer script's "desk rule"): kept in
    /// shturmap.db, and every card and settings' row say it at once.</summary>
    private void SetDeskRule(DeskCopyRule rule, string how)
    {
        if (rule == _deskRule)
            return;
        _deskRule = rule;
        _session.SetSetting(DeskCopyRule.Setting, rule.Format());
        Study.Ui("desk.rule", ("rule", rule.Format()), ("how", how));
        _deskVersion++;
        RenderDeskRail();
        ShowCopyRuleSetting();
    }

    // Settings' row "Flea price to copy": the same − and + as the card's plate.
    private void ShowCopyRuleSetting() =>
        SettingsCopyRuleHost.Child = DeskCardView.RuleStepper(_deskRule, step => SetDeskRule(_deskRule.Step(step), "settings"));

    // ---- a "?" cell's pick ----

    /// <summary>A candidate clicked on a "?" cell's card: the cell is that item from now on, its card and price follow.</summary>
    private async void PickDeskCell(DeskCellKey key, string itemId, string how)
    {
        Study.Ui("desk.pick", ("how", how));
        if (_deskExample is not null)
        {
            _deskExamplePicks[key] = itemId;
            if (Volatile.Read(ref _snapshot) is { } s)
                RemakeDeskExample(s);
            return;
        }
        await _session.PickDeskAsync(key.Region, key.Slot, key.I, key.J, itemId);
    }

    // ---- the made-up desk ----

    // "--desk [<n>]": the made-up page once the data and the mode are known.
    private void ShowDeskExampleOnStart(SessionSnapshot s)
    {
        if (DeskOnStart is not { } reading || s.Data is null || s.Mode == GameMode.Unknown)
            return;
        DeskOnStart = null;
        ShowDeskExample(reading, s);
    }

    /// <summary>The made-up desk's reading n as a reading brings it (<see cref="DeskExample"/>).</summary>
    private void ShowDeskExample(int reading, SessionSnapshot s)
    {
        _deskExampleReading = Math.Clamp(reading, 1, DeskExample.Readings);
        _deskExample = MadeUpDesk(s, _deskReadingSeen + 1);
        AppLog.Info($"Example desk: reading {_deskExampleReading} of {DeskExample.Readings}");
        UpdateDesk(s);
    }

    // The same reading again with the picks made since: not a new reading, so nothing comes in.
    private void RemakeDeskExample(SessionSnapshot s)
    {
        if (_deskExample is not { } shown)
            return;
        _deskExample = MadeUpDesk(s, shown.Reading) with { Changed = shown.Changed, First = shown.First, PricesAt = shown.PricesAt };
        UpdateDesk(s);
    }

    private DeskView MadeUpDesk(SessionSnapshot s, int counter)
    {
        var data = s.Data;
        return DeskExample.View(_deskExampleReading, s.Mode, counter,
            data is null ? null : id => data.TraderName(id) is { Length: > 0 } name ? name : id, DateTimeOffset.Now, _deskExamplePicks,
            _deskExamplePriced);
    }
}
