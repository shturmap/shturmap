using Microsoft.UI.Xaml;
using Shturmap.Session;

namespace Shturmap.App;

// "Uninstall Shturmap…" in settings (owner, 2026-10-03; docs/DESIGN.md §8, "Distribution"): one question, with the data
// only on a tick, then Velopack's own uninstaller, the same one Windows' Settings → Apps runs.
public sealed partial class MainWindow
{
    /// <summary>Only in an install Velopack made (the release, the dev build); never in a folder build.</summary>
    public Visibility UninstallVisibility { get; } = App.Updater.UninstallOffered ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>What "Also delete my Shturmap data" takes, for the build it runs in.</summary>
    public string UninstallDataText { get; } = App.Updater.AppId == Distribution.DeveloperPackId
        ? $@"Settings, quest history, logs, {(StudyLog.Available ? "study log, " : "")}unsent reports and crash records in %LOCALAPPDATA%\Shturmap-dev. The download cache stays (the release shares it)."
        : @"The whole %LOCALAPPDATA%\Shturmap folder: settings, quest history, logs, unsent reports, crash records, download cache. Unticked, it stays for a later install.";

    private void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        ViewModel.UninstallDeleteData = false;
        ViewModel.UninstallAsking = !ViewModel.UninstallAsking;
    }

    private void OnUninstallDataClick(object sender, RoutedEventArgs e) => ViewModel.UninstallDeleteData = !ViewModel.UninstallDeleteData;

    private void OnUninstallCancelClick(object sender, RoutedEventArgs e) => ViewModel.UninstallAsking = false;

    private async void OnUninstallConfirmClick(object sender, RoutedEventArgs e)
    {
        var deleteData = ViewModel.UninstallDeleteData;
        ViewModel.UninstallAsking = false;
        Study.Ui("uninstall", ("deleteData", deleteData));
        await UninstallAsync(deleteData, silent: false);
    }

    /// <summary>
    /// Closes the session the way RESTART NOW does (so the next install doesn't take this exit for a crash), then hands
    /// over to Velopack's uninstaller and ends. Its hook deletes the data folder when <paramref name="deleteData"/>.
    /// </summary>
    internal async Task UninstallAsync(bool deleteData, bool silent)
    {
        var updater = App.Updater;
        if (!updater.UninstallerReady)
        {
            ShowNotice("Couldn't find Shturmap's uninstaller: remove it in Windows' Settings → Apps instead.");
            return;
        }
        SettingsFlyout.Hide();
        await _session.DisposeAsync();
        ((App)Application.Current).EndSession();
        updater.StartUninstall(deleteData, silent);
        Application.Current.Exit();
    }
}
