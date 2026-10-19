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
// rail's head; the desk in the map's place out of a raid, the rail its card (for step B the item card the pointer asks
// for); it opens by itself with a reading in the menus, a raid loading takes the window back, and it keeps its page.
public sealed partial class MainWindow
{
    private bool _deskChosen;
    private int _deskReadingSeen;
    private IReadOnlySet<string>? _deskOpen;
    private DeskView? _deskShown;
    private DeskView? _deskExample;
    private bool _deskBoardDue = true;
    private IReadOnlySet<string> _deskEntering = new HashSet<string>();

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
        StashDesk.Unfolded += key =>
        {
            if (_deskOpen is null)
                return;
            Study.Ui("desk.unfold", ("region", key));
            _deskOpen = DeskFold.Unfold(_deskOpen, key);
            ShowDeskPage(motion: false);
        };
    }

    // Each snapshot: a new reading in the menus opens the desk, a raid takes the window, the fold follows the reading.
    private void UpdateDesk(SessionSnapshot s)
    {
        var view = DeskNow(s);
        var fresh = DeskSwitch.IsNew(view?.Reading, _deskReadingSeen);
        if (view is not null)
            _deskReadingSeen = Math.Max(_deskReadingSeen, view.Reading);
        var chosen = DeskSwitch.ChosenAfter(_deskChosen, ViewModel.InRaid, fresh, TourOpen);
        if (view is not null && (fresh || _deskOpen is null || !ReferenceEquals(view, _deskShown)))
        {
            var keys = view.Sheet.Regions.Select(r => r.Region.Key).ToList();
            _deskOpen = DeskFold.Open(_deskOpen, keys, view.Changed, view.First, fresh);
            _deskEntering = DeskFold.Entering(_deskOpen, view.Changed, view.First, fresh);
            _deskShown = view;
            _deskBoardDue = true;
        }
        else if (view is null && _deskShown is not null)
        {
            // The mode changed to one without a page: the desk says so, and the next page opens whole.
            _deskShown = null;
            _deskOpen = null;
            _deskBoardDue = true;
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
            DeskCard(null);
    }

    // The page as it is: the desk's view and fold; the cells of a new reading's opened regions come in, once.
    private void ShowDeskPage(bool motion)
    {
        _deskBoardDue = false;
        var entering = _deskEntering;
        _deskEntering = new HashSet<string>();
        StashDesk.Show(_deskShown, _deskOpen ?? new HashSet<string>(), entering, motion);
    }

    // PLAN and DESK at the rail's head: a click, or Space or Enter on the half with the focus.
    private void OnDeskSwitchPlanClick(object sender, RoutedEventArgs e) => SetDesk(false, "switch");

    private void OnDeskSwitchDeskClick(object sender, RoutedEventArgs e) => SetDesk(true, "switch");

    // ← and → on the switch pick PLAN or DESK, and the focus goes with the pick (owner, 2026-10-20: a primary control).
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

    // ---- the rail: the card of the cell pointed at ----

    private void OnDeskCellPointed(DeskCellSheet cell)
    {
        if (!_deskChosen)
            return;
        if (cell.Cell.ItemId is { } item)
            DeskCard(item);
        else if (cell.Cell.Ids.Count > 1)
        {
            DeskCardHost.Child = null;
            DeskRailLine.Text = AppTexts.DeskRailOneOf(count: cell.Cell.Ids.Count);
            DeskRailLine.Visibility = Visibility.Visible;
        }
    }

    // For step B the rail shows the item card the pointer opens anywhere (the desk's own card is step C), one at a time:
    // it stays until another cell is pointed at, so the way from the page to the rail doesn't lose it.
    private void DeskCard(string? itemId)
    {
        if (itemId is null)
        {
            DeskCardHost.Child = null;
            DeskRailLine.Text = ViewTexts.DeskRailHint;
            DeskRailLine.Visibility = Visibility.Visible;
            return;
        }
        if (DeskCardHost.Child is ItemCard { View.ItemId: var shown } && shown == itemId)
            return;
        if (CreateCard(new CardKey.Item(itemId)) is not ItemCard card)
            return;
        card.InRail();
        DeskCardHost.Child = card;
        DeskRailLine.Visibility = Visibility.Collapsed;
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
        var data = s.Data;
        _deskExample = DeskExample.View(reading, s.Mode, _deskReadingSeen + 1,
            data is null ? null : id => data.TraderName(id) is { Length: > 0 } name ? name : id, DateTimeOffset.Now);
        AppLog.Info($"Example desk: reading {Math.Clamp(reading, 1, DeskExample.Readings)} of {DeskExample.Readings}");
        UpdateDesk(s);
    }
}
