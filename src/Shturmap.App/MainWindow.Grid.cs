using Microsoft.UI.Xaml;
using Shturmap.App.Controls;
using Shturmap.Map;
using Shturmap.Session;

namespace Shturmap.App;

// The squad grid on the map (owner, 2026-10-26, from a panel of five: E; docs/DESIGN.md §4, "Map drawing", *The grid*):
// settings' one tick, on unless unticked, and the map's own controls stepping inside its strips while it shows (the
// buttons and the floor list, the notes, the wiki link and the credit, the replay's band), so no letter or number of the
// strips lies under a control. The renderer keeps what it draws itself inside them (the scale bar, the edge badge, the
// chevrons), frames every view inside them (Camera.Inset) and keeps its names and the squares' references off the
// controls (ControlsOverMap).
public sealed partial class MainWindow
{
    private bool _gridLoaded;

    // How far from the map's left edge the notes keep clear (DIP): 0 but for What's New's grid-pointer preview.
    private double _notesClearOf;

    // It comes back as the player left it: on at first.
    private void LoadGridOnce()
    {
        if (_gridLoaded)
            return;
        _gridLoaded = true;
        ShowGrid(MapGrid.Parse(_session.GetSetting(MapGrid.Setting)));
    }

    public string MapGridNote => AppTexts.SettingsMapGridNote;

    private void OnMapGridClick(object sender, RoutedEventArgs e)
    {
        var on = !ViewModel.MapGrid;
        _session.SetSetting(MapGrid.Setting, MapGrid.Format(on));
        Study.Ui("settings.mapGrid", ("on", on));
        ShowGrid(on);
    }

    private void ShowGrid(bool on)
    {
        ViewModel.MapGrid = on;
        Map.ShowGrid = on;
        // The sheet's credit line names the grid drawn.
        ViewModel.Attribution = AttributionFor(_snapshot?.Definition);
        PlaceInsideGrid();
    }

    /// <summary>The room the grid's strips take at the map's edges (DIP): the renderer's strips at the text size; none
    /// without the grid.</summary>
    private Thickness GridStrips => Map.ShowGrid
        ? new Thickness(MapRenderer.GridStripWide * TextScale.Effective, MapRenderer.GridStripHigh * TextScale.Effective,
            MapRenderer.GridStripWide * TextScale.Effective, MapRenderer.GridStripHigh * TextScale.Effective)
        : new Thickness(0);

    // The margins of MainWindow.xaml, each grown by the strip on its side while the grid shows: the notes at the top
    // centre, the buttons and the floor list at the lower right, the wiki link and the credit at the lower left, the replay's
    // band at the foot (clear of the buttons, over neither the link nor the credit). Looked at again when the setting or the
    // text size changes.
    private void PlaceInsideGrid()
    {
        var strips = GridStrips;
        // While What's New previews the grid under the pointer, the notes stand right of the readout at the top left, so
        // YOU and POINTER are drawn as the feature draws them (MainWindow.WhatsNewPreviews, NotesClearOfReadout).
        MapNotes.Margin = new Thickness(Math.Max(16 + strips.Left, _notesClearOf), 16 + strips.Top, 16 + strips.Right, 16);
        MapControls.Margin = new Thickness(16, 16, 16 + strips.Right, 16 + strips.Bottom);
        MapFoot.Margin = new Thickness(16 + strips.Left, 0, 64 + strips.Right, 10 + strips.Bottom);
        ReplayBand.Margin = new Thickness(strips.Left, 0, 68 + strips.Right, 46 + strips.Bottom);
        if (TourOpen)
            PlaceTour();
        // Where they now stand, after the layout pass, and the notes' plates with them.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, ControlsOverMap);
        PlateOverMapSoon();
    }

    // Watched for the map's drawing: as they change their size, show or go, and as the map changes its own.
    private void InstallControlsOverMap()
    {
        MapFoot.SizeChanged += (_, _) => ControlsOverMap();
        MapControls.SizeChanged += (_, _) => ControlsOverMap();
        Map.SizeChanged += (_, _) => ControlsOverMap();
        MapFoot.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => ControlsOverMap());
        MapControls.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, (_, _) => ControlsOverMap());
    }

    // The map's own controls over the map while the grid shows, for its drawing: its names and labels keep off the WIKI MAP
    // link with the credit, the buttons and the floor list, and the squares' references are left out under them (UI QA pass
    // of the squad grid, 2026-10-26, F2: inside the strips the credit ran over "Transit to Interchange" and the link stood
    // over "A9"; MapView.ControlsOver). Without the grid, none: everything stands where it did.
    private void ControlsOverMap()
    {
        if (_stopped)
            return;
        var over = new List<Windows.Foundation.Rect>();
        if (Map.ShowGrid)
        {
            Windows.Foundation.Rect Of(FrameworkElement control) =>
                control.TransformToVisual(Map).TransformBounds(new Windows.Foundation.Rect(0, 0, control.ActualWidth, control.ActualHeight));
            bool Shows(FrameworkElement control) => control.Visibility == Visibility.Visible && control.ActualWidth >= 1 && control.ActualHeight >= 1;
            // The link and the credit each on its own: right of the link, above the credit, a name may stand.
            if (MapFoot.Visibility == Visibility.Visible)
                over.AddRange(MapFoot.Children.OfType<FrameworkElement>().Where(Shows).Select(Of));
            // Each of the column's parts on its own: the floor list is wider than the buttons. Stepped aside (a cue, a plate,
            // the crash question) it keeps its place unseen, so it counts all the same (ShowFloors).
            if (MapControls.Visibility == Visibility.Visible)
                over.AddRange(MapControls.Children.OfType<FrameworkElement>().Where(Shows).Select(Of));
        }
        if (!over.SequenceEqual(Map.ControlsOver))
            Map.ControlsOver = over;
    }
}
