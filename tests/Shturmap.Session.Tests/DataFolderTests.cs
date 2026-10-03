using Shturmap.Session.Reporting;

namespace Shturmap.Session.Tests;

// Only the installed release keeps the player's data folder; every other build its own, the download cache shared
// (owner, 2026-10-03; docs/DESIGN.md §8, "Data folders").
public class DataFolderTests
{
    private const string Local = @"C:\Local";

    [Fact]
    public void The_release_keeps_the_players_folder()
    {
        var paths = AppPaths.For(DataFolderKind.Release, null, Local);
        Assert.Equal(@"C:\Local\Shturmap", paths.Root);
        Assert.Equal(@"C:\Local\Shturmap\cache", paths.CacheRoot);
        Assert.Equal(@"C:\Local\Shturmap\shturmap.db", paths.Database);
        Assert.Equal("release", paths.KindText);
    }

    [Fact]
    public void Every_other_build_has_its_own_folder_but_shares_the_downloads()
    {
        var paths = AppPaths.For(DataFolderKind.Dev, null, Local);
        Assert.Equal(@"C:\Local\Shturmap-dev", paths.Root);
        Assert.Equal(@"C:\Local\Shturmap-dev\shturmap.db", paths.Database);
        Assert.Equal(@"C:\Local\Shturmap-dev\logs", paths.Logs);
        Assert.Equal(@"C:\Local\Shturmap-dev\study", paths.Study);
        Assert.Equal(@"C:\Local\Shturmap\cache", paths.CacheRoot);
        Assert.Equal(@"C:\Local\Shturmap\cache\tarkov-dev", paths.DataCache);
        Assert.Equal("dev", paths.KindText);
    }

    [Fact]
    public void Data_names_any_folder_and_still_shares_the_downloads()
    {
        var paths = AppPaths.For(DataFolderKind.Custom, @"D:\Elsewhere\Shturmap", Local);
        Assert.Equal(@"D:\Elsewhere\Shturmap", paths.Root);
        Assert.Equal(@"C:\Local\Shturmap\cache", paths.CacheRoot);
        Assert.Equal("custom (--data)", paths.KindText);
        // Without a folder there is nothing to name: the developer folder.
        Assert.Equal(DataFolderKind.Dev, AppPaths.For(DataFolderKind.Custom, " ", Local).Kind);
    }

    [Theory]
    [InlineData("ShturmapApp", null, DataFolderKind.Release)]
    [InlineData("ShturmapDev", null, DataFolderKind.Dev)]
    [InlineData(null, null, DataFolderKind.Dev)]
    [InlineData("SomethingElse", null, DataFolderKind.Dev)]
    [InlineData("ShturmapApp", @"D:\Data", DataFolderKind.Custom)]
    [InlineData(null, @"D:\Data", DataFolderKind.Custom)]
    public void Which_folder_a_build_uses(string? installedAppId, string? dataArgument, DataFolderKind expected) =>
        Assert.Equal(expected, Distribution.DataFolderFor(installedAppId, dataArgument));

    [Fact]
    public void A_process_that_chose_nothing_uses_the_developer_folder()
    {
        Assert.Equal(DataFolderKind.Dev, AppPaths.Default.Kind);
        Assert.EndsWith(@"\Shturmap-dev", AppPaths.Default.Root, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("dev build", "dev")]
    [InlineData("installed", "release")]
    [InlineData("folder build", "release")]
    public void Reports_from_a_dev_build_are_filed_apart(string build, string environment) =>
        Assert.Equal(environment, ReportEnvelopes.EnvironmentOf(new ReportInfo("0.2.0", build, "Windows 11")));

    [Fact]
    public void Diagnostics_name_the_data_folder()
    {
        var text = Diagnostics.Build(new SessionSnapshot(), "0.2.0-dev.1+abc1234", "Windows 11 (10.0.26200)", "dev build", [],
            new DateTime(2026, 10, 3, 18, 0, 0), null, "dev");
        Assert.Contains("Shturmap: 0.2.0-dev.1+abc1234 (dev build)" + Environment.NewLine + "Data folder: dev", text);
    }
}
