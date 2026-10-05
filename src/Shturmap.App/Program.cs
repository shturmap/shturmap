using Velopack;

namespace Shturmap.App;

/// <summary>
/// The exe's start. Velopack goes first: its Setup, its updater and an uninstall start the exe with their own
/// arguments, which it handles and then ends the process; and a version downloaded in an earlier session is applied
/// here, before the app starts (docs/DESIGN.md §8, "Distribution"). An uninstall's hook deletes the data folder when
/// the player asked for it in settings (Updater.DeleteDataIfAsked). Then WinUI's own start, as it generates it.
/// </summary>
public static class Program
{
    [STAThread]
    private static void Main()
    {
        VelopackApp.Build()
            .OnBeforeUninstallFastCallback(_ => Updater.DeleteDataIfAsked())
            .Run();
        // Velopack starts the app in the install's current folder, and whatever the app opens (the browser for a wiki
        // link, Explorer for a folder, an editor for privacy.txt) inherits its working folder. A browser started that
        // way held the folder for as long as it ran, Velopack couldn't move it aside to apply an update, and no update
        // applied (owner, 2026-10-05: "it tells me always that the update is available"; found that day: Firefox
        // working in ShturmapDev\current). The user's own folder instead, which nothing ever needs to move. Every file
        // the app reads beside the exe is found from AppContext.BaseDirectory.
        Environment.CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        XamlGeneratedProgram.XamlGeneratedMain();
    }
}
