using Microsoft.UI.Xaml;
using Shturmap.App.Controls;
using Shturmap.Map;
using Shturmap.Session;

namespace Shturmap.App;

// The squad grid on the map (owner, 2026-10-26, from a panel of five: E; docs/DESIGN.md §4, "Map drawing", *The grid*):
// settings' one tick, on unless unticked, and the map's own controls stepping inside its strips while it shows (the
// buttons and the floor list, the notes, the wiki link and the credit, the replay's band), so no letter or number of the
// strips lies under a control. The renderer keeps what it draws itself inside them (the scale bar, the edge badge, the
// chevrons).
public sealed partial class MainWindow
{
    private bool _gridLoaded;

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
        MapNotes.Margin = new Thickness(16 + strips.Left, 16 + strips.Top, 16 + strips.Right, 16);
        MapControls.Margin = new Thickness(16, 16, 16 + strips.Right, 16 + strips.Bottom);
        MapFoot.Margin = new Thickness(16 + strips.Left, 0, 64 + strips.Right, 10 + strips.Bottom);
        ReplayBand.Margin = new Thickness(strips.Left, 0, 68 + strips.Right, 46 + strips.Bottom);
        if (TourOpen)
            PlaceTour();
    }
}
