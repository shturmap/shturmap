using Velopack;

namespace Shturmap.App;

/// <summary>
/// The exe's start. Velopack goes first: its Setup, its updater and an uninstall start the exe with their own
/// arguments, which it handles and then ends the process; and a version downloaded in an earlier session is applied
/// here, before the app starts (docs/DESIGN.md §8, "Distribution"). Then WinUI's own start, as it generates it.
/// </summary>
public static class Program
{
    [STAThread]
    private static void Main()
    {
        VelopackApp.Build().Run();
        XamlGeneratedProgram.XamlGeneratedMain();
    }
}
