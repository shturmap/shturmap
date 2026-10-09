using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Shturmap.App.Controls;
using Shturmap.App.Rules;

namespace Shturmap.App;

// The keyboard's way through the rail's rows (docs/DESIGN.md, "Keyboard (window focused only)"; the review of
// 2026-10-04, E6: the linked highlight needed a pointer). Down and Up step through the rows in the order they stand
// on screen (RowSteps); the row reached is in focus as if the pointer were on it; Enter is a click on it, P its
// quest's pen, Esc lets it go. Nothing here is needed to use the app, and nothing sends a key: the keys only work
// while this window has focus.
public sealed partial class MainWindow
{
    // A row as the keyboard sees it: a linked row, or a map of Plan's list (a button, linked to nothing).
    private sealed record RailRow(FrameworkElement Element, RowId Id, RowBox Box);

    // The keyboard's row: the element, and what it shows. The rail's rows are made anew with every snapshot, by new
    // elements or by the same ones showing something else (the lists reuse them), so the row is the thing shown,
    // and the element only where it stands now.
    private sealed record KeyAt(FrameworkElement Element, RowId Id, int Nth);

    private KeyAt? _keyAt;

    // Where the pointer was last seen in this window, and where it was when the keyboard reached its row: it takes
    // over when it moves from there, not when it trembles.
    private Windows.Foundation.Point? _pointerSeenAt;
    private Windows.Foundation.Point? _pointerSeenAtReach;
    private const double PointerMoves = 4;

    // The next time the keyboard's row is found again, it is brought into view: its quest was picked and moved up.
    private bool _showKeyRowAgain;
    private bool _findingKeyRow;

    private void AddRowKeys(UIElement root)
    {
        void Add(Windows.System.VirtualKey key, string name, Func<bool> act)
        {
            var accelerator = new KeyboardAccelerator { Key = key };
            // Not handled when the key belongs to something else or there is nothing to do: it goes on to whatever
            // has the focus (a button's Enter, a list's arrows).
            accelerator.Invoked += (_, e) => e.Handled = RowKey(name, act);
            root.KeyboardAccelerators.Add(accelerator);
        }
        Add(Windows.System.VirtualKey.Down, "Down", () => StepRow(down: true));
        Add(Windows.System.VirtualKey.Up, "Up", () => StepRow(down: false));
        Add(Windows.System.VirtualKey.Enter, "Enter", ClickKeyRow);
        Add(Windows.System.VirtualKey.P, "P", PickKeyRow);
        root.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnPointerMovedOverKeys), handledEventsToo: true);
        // The pointer pointed at something elsewhere (a map marker, a popped-out card), or nobody is looking.
        Linked.KeyRowTaken += ForgetKeyRow;
        // The row's element shows something else now (a snapshot filled it anew): the row is looked for again.
        Linked.KeyRowChanged += FindKeyRowAgain;
    }

    // One of the rows' keys: taken when it is the rows' to take and did something.
    private bool RowKey(string name, Func<bool> act)
    {
        // The tour has the keys while it is up: Enter steps it on, the rows' others do nothing (MainWindow.Tour).
        if (TourOpen)
            return name == "Enter" ? TourKey(Windows.System.VirtualKey.Enter) : true;
        if (KeysElsewhere() is not null || !act())
            return false;
        Study.Ui("key", ("key", name));
        return true;
    }

    // Whose the keys are when they aren't the rows', or null. In the Report dialog they are text; in help, settings
    // or an open list (the MAP list) they are that flyout's; and a text box that has the focus takes its own. The
    // cards are popups too, and no reason to wait. The MAP list, closed, doesn't count: the window's focus rests on
    // it at the start, and its own arrows would switch the map with every press; opened, it has them.
    private string? KeysElsewhere()
    {
        if (ReportOpen)
            return "the report dialog";
        if (HelpFlyout.IsOpen || SettingsFlyout.IsOpen)
            return "help or settings";
        if (Content.XamlRoot is not { } root)
            return "no window yet";
        if (FocusManager.GetFocusedElement(root) is TextBox or PasswordBox or RichEditBox or AutoSuggestBox or ComboBoxItem)
            return "a text box or an open list";
        var cards = _cards.Cards;
        return VisualTreeHelper.GetOpenPopupsForXamlRoot(root).Any(p => p.Child is not ToolTip && !(p.Child is FrameworkElement card && cards.Contains(card)))
            ? "an open flyout or list"
            : null;
    }

    // ---- the rows ----

    // The rail's rows in the order they stand on screen: the linked rows (a quest's block and the lines in it, NEXT,
    // EXIT, BRING, the ways out) and the maps of Plan's list. Only what is shown: Plan and the raid card share the
    // rail, and the one not shown is collapsed.
    private List<RailRow> RailRows()
    {
        if (RailScroll.Content is not FrameworkElement content)
            return [];
        var found = Linked.RowsWithin(content).Concat(MapRows(content));
        var rows = new List<RailRow>();
        foreach (var element in found)
        {
            if (BoxOf(element, content) is { } box)
                rows.Add(new RailRow(element, IdOf(element), box));
        }
        return RowSteps.Order(rows.Select(r => r.Box).ToList()).Select(i => rows[i]).ToList();
    }

    // What a row shows now.
    private static RowId IdOf(FrameworkElement row) => row is Button { Tag: string map } ? RowId.Map(map) : Linked.IdOf(row);

    // The rows of Plan's map list: the buttons that show a suggested map.
    private static IEnumerable<Button> MapRows(DependencyObject under)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(under); i++)
        {
            var child = VisualTreeHelper.GetChild(under, i);
            if (child is UIElement { Visibility: Visibility.Collapsed })
                continue;
            if (child is Button { Tag: string, DataContext: PlanCard } map)
            {
                yield return map;
                continue;
            }
            foreach (var deeper in MapRows(child))
                yield return deeper;
        }
    }

    // Where a row stands in the rail's content, or null when it isn't shown (it, or something around it, is collapsed).
    private static RowBox? BoxOf(FrameworkElement element, FrameworkElement content)
    {
        if (!element.IsLoaded || element.ActualHeight <= 0 || element.ActualWidth <= 0)
            return null;
        for (DependencyObject? node = element; node is not null && !ReferenceEquals(node, content); node = VisualTreeHelper.GetParent(node))
        {
            if (node is UIElement { Visibility: Visibility.Collapsed })
                return null;
        }
        try
        {
            var box = element.TransformToVisual(content).TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));
            return new RowBox(box.Top, box.Left, box.Height);
        }
        catch (ArgumentException)
        {
            return null; // no longer in the rail's tree
        }
    }

    // ---- the keys ----

    private bool StepRow(bool down)
    {
        var rows = RailRows();
        var next = RowSteps.Step(rows.Select(r => r.Box).ToList(), KeyIndex(rows), down,
            RailScroll.VerticalOffset, RailScroll.VerticalOffset + RailScroll.ViewportHeight);
        if (next < 0)
            return false;
        Reach(rows, next, show: true);
        return true;
    }

    // Where the keyboard's row is among the rows now: its element while that still shows it, else the row that shows
    // the same thing; -1 when it has none, or nothing shows it any more.
    private int KeyIndex(List<RailRow> rows)
    {
        if (_keyAt is not { } at)
            return -1;
        var index = rows.FindIndex(r => ReferenceEquals(r.Element, at.Element) && r.Id == at.Id);
        return index >= 0 ? index : RowSteps.Find(rows.Select(r => r.Id).ToList(), at.Id, at.Nth);
    }

    private void Reach(List<RailRow> rows, int index, bool show)
    {
        var row = rows[index];
        if (_keyAt is { } before)
            Unhook(before, staying: ReferenceEquals(before.Element, row.Element));
        _keyAt = new KeyAt(row.Element, row.Id, RowSteps.NthOf(rows.Select(r => r.Id).ToList(), index));
        _pointerSeenAtReach = _pointerSeenAt;
        row.Element.Unloaded += OnKeyRowGone;
        if (row.Element is Button map)
        {
            // A map of Plan's list is linked to nothing: it wears the highlight's tint itself and previews its map,
            // as it does under the pointer. Its list fills it anew with every snapshot, which takes the tint off.
            map.DataContextChanged += OnKeyMapRefilled;
            Linked.ReachByKey(Content.XamlRoot, null);
            map.Background = Resource("LinkBrush");
            if (map.Tag is string name && !ViewModel.InRaid && name != _snapshot?.Map?.NormalizedName)
                Preview(name, PreviewAfter);
        }
        else
        {
            Linked.ReachByKey(Content.XamlRoot, row.Element);
        }
        if (show)
            row.Element.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
    }

    // The row the keyboard leaves: a map of Plan's list gets its own ground back, and its preview ends.
    private void Unhook(KeyAt at, bool staying = false)
    {
        at.Element.Unloaded -= OnKeyRowGone;
        if (at.Element is not Button map)
            return;
        map.DataContextChanged -= OnKeyMapRefilled;
        if (staying)
            return;
        if (map.DataContext is PlanCard card)
            map.Background = card.CardBackground;
        Preview(null, PreviewEndAfter);
    }

    // The keyboard's row as it stands now, found again if its element went or shows something else; false when the
    // keyboard has no row, or nothing shows it any more.
    private bool SettleKeyRow()
    {
        if (_keyAt is not { } at)
            return false;
        if (at.Element.IsLoaded && IdOf(at.Element) == at.Id)
            return true;
        var rows = RailRows();
        var index = RowSteps.Find(rows.Select(r => r.Id).ToList(), at.Id, at.Nth);
        if (index < 0)
        {
            LeaveKeyRow();
            return false;
        }
        Reach(rows, index, show: false);
        return true;
    }

    private bool ClickKeyRow()
    {
        if (!SettleKeyRow() || _keyAt is not { } at)
            return false;
        if (at.Element is Button map)
            OnPlanClick(map, new RoutedEventArgs());
        else
            Linked.ClickByKey();
        return true;
    }

    private bool PickKeyRow()
    {
        if (!SettleKeyRow() || Linked.KeyRowQuest is not { } quest)
            return false;
        // A picked quest moves up into PICKED: the keyboard goes with it.
        _showKeyRowAgain = true;
        Linked.RequestKeep(quest);
        return true;
    }

    /// <summary>Esc: the keyboard lets its row go.</summary>
    private void LeaveKeyRow()
    {
        if (_keyAt is null)
            return;
        ForgetKeyRow();
        Linked.LeaveByKey();
    }

    private void ForgetKeyRow()
    {
        if (_keyAt is not { } at)
            return;
        _keyAt = null;
        _showKeyRowAgain = false;
        Unhook(at);
    }

    private void OnKeyRowGone(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(_keyAt?.Element, sender))
            FindKeyRowAgain();
    }

    private void OnKeyMapRefilled(FrameworkElement sender, DataContextChangedEventArgs e)
    {
        if (ReferenceEquals(_keyAt?.Element, sender))
            FindKeyRowAgain();
    }

    // A snapshot made the rows anew: the keyboard's row is found again by what it shows, once the new rows stand
    // (they are filled and laid out a moment later). If nothing shows it any more, it is let go.
    private async void FindKeyRowAgain()
    {
        if (_findingKeyRow || _keyAt is null)
            return;
        _findingKeyRow = true;
        try
        {
            for (var attempt = 0; attempt < 8; attempt++)
            {
                await Task.Delay(40);
                if (_keyAt is not { } at)
                    return; // the keyboard let it go, or the pointer took over
                var rows = RailRows();
                var index = RowSteps.Find(rows.Select(r => r.Id).ToList(), at.Id, at.Nth);
                if (index < 0)
                    continue;
                var show = _showKeyRowAgain;
                _showKeyRowAgain = false;
                Reach(rows, index, show);
                return;
            }
            LeaveKeyRow();
        }
        finally
        {
            _findingKeyRow = false;
        }
    }

    // The pointer moved in this window while the keyboard had a row: it takes over at once, and is on whatever lies
    // under it now.
    private void OnPointerMovedOverKeys(object sender, PointerRoutedEventArgs e)
    {
        var at = e.GetCurrentPoint(null).Position;
        _pointerSeenAt = at;
        if (_keyAt is null || (_pointerSeenAtReach is { } was && Math.Abs(at.X - was.X) < PointerMoves && Math.Abs(at.Y - was.Y) < PointerMoves))
            return;
        var under = VisualTreeHelper.FindElementsInHostCoordinates(at, (UIElement)Content).ToList();
        ForgetKeyRow();
        Linked.PointerTakesOver(under);
        // On a map of Plan's list it previews that map, as on entering it.
        if (under.OfType<Button>().FirstOrDefault(b => b is { Tag: string, DataContext: PlanCard }) is { Tag: string map }
            && !ViewModel.InRaid && map != _snapshot?.Map?.NormalizedName)
            Preview(map, PreviewAfter);
    }

#if DEVTOOLS
    /// <summary>
    /// Developer scripts: one of the rows' keys ("down", "up", "enter", "p", "esc"), through the code the key's own
    /// event calls. Nothing is sent to the system. Returns what went wrong, or null.
    /// </summary>
    internal string? DevKey(string name)
    {
        // A script has no pointer and may not be the active window: the row stays until the script lets it go.
        ScriptedPointer = true;
        if (name.ToLowerInvariant() is "down" or "up" or "enter" or "p" && KeysElsewhere() is { } whose)
            return $"key {name}: not taken, the keys are those of {whose}";
        switch (name.ToLowerInvariant())
        {
            case "down":
                return RowKey("Down", () => StepRow(down: true)) ? null : "Down did nothing: the rail has no rows";
            case "up":
                return RowKey("Up", () => StepRow(down: false)) ? null : "Up did nothing: the rail has no rows";
            case "enter":
                return RowKey("Enter", ClickKeyRow) ? null : "Enter did nothing: the keyboard is on no row";
            case "p":
                return RowKey("P", PickKeyRow) ? null : "P did nothing: the keyboard's row has no quest";
            case "esc":
                // An open flyout takes the real key and closes (help opens by itself at a first start).
                if (HelpFlyout.IsOpen || SettingsFlyout.IsOpen)
                {
                    HelpFlyout.Hide();
                    SettingsFlyout.Hide();
                    return null;
                }
                LeaveKeyRow();
                _cards.CloseAll();
                return null;
            default:
                return "no such key: down, up, enter, p or esc";
        }
    }

    /// <summary>What the keyboard's row shows, for a script's log.</summary>
    internal string DevKeyRow => _keyAt is { } at ? $"{at.Id.Kind} {at.Id.Key}" : "none";
#endif
}
