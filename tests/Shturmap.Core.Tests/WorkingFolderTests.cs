namespace Shturmap.Core.Tests;

// Velopack starts the app in its install's current folder, and what the app opens inherits its working folder: a
// browser Shturmap had opened a wiki link in held that folder for as long as it ran, and no update could be applied
// (owner, 2026-10-05: "it tells me always that the update is available"). The app works in the user's folder instead,
// set right after Velopack's start, before the window. Read from Program.cs, which the app compiles.
public class WorkingFolderTests
{
    [Fact]
    public void The_app_leaves_its_install_folder_before_its_window_opens()
    {
        var program = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Shturmap.App", "Program.cs"));
        var velopack = program.IndexOf(".Run();", StringComparison.Ordinal);
        var leaves = program.IndexOf("Environment.CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);", StringComparison.Ordinal);
        var window = program.IndexOf("XamlGeneratedProgram.XamlGeneratedMain();", StringComparison.Ordinal);
        Assert.True(velopack >= 0 && leaves > velopack && window > leaves, "Program sets the working folder between Velopack's start and the window's");
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Shturmap.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
