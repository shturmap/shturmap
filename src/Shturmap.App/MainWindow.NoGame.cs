using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Shturmap.Core.Logs;
using Shturmap.Session;

namespace Shturmap.App;

// No game on this PC (owner, 2026-10-03: the no-game fallback; docs/DESIGN.md, "No game on this PC"): a line where the
// Plan card would be, "Choose game folder…", and the mode chosen by hand, since there is no log to say it.
public sealed partial class MainWindow
{
    private void ApplyGameState(SessionSnapshot s)
    {
        var vm = ViewModel;
        var line = GameStateLine.For(s.Locations, s.CanChooseGameFolder);
        vm.GameLine = line?.Title ?? "";
        vm.GameNote = line?.Note ?? "";
        vm.GameFolderChoosable = line?.OffersChoice == true;
        vm.NoGameLogs = line is not null;
        vm.ModeChoosable = s.NoGameFound;
    }

    private async void OnChooseGameFolderClick(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker
        {
            SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder,
        };
        picker.FileTypeFilter.Add("*");
        // An unpackaged app's picker needs its window.
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));
        if (await picker.PickSingleFolderAsync() is not { } folder)
            return;
        Study.Ui("gamefolder.choose");
        await _session.ChooseGameFolderAsync(folder.Path);
    }

    private async void OnModeChosen(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem { Tag: string tag } && Enum.TryParse<GameMode>(tag, out var mode))
            await _session.ChooseModeAsync(mode);
    }
}
