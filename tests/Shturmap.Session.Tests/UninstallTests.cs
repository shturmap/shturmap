using Shturmap.Session;

namespace Shturmap.Session.Tests;

// "Uninstall Shturmap…" in help (owner, 2026-10-03): only in Velopack's installs, and the data only on a tick, and
// then only ever the install's own data folder.
public class UninstallTests : IDisposable
{
    private readonly string _local = Directory.CreateTempSubdirectory("shturmap-uninstall-").FullName;

    public void Dispose() => Directory.Delete(_local, true);

    [Theory]
    [InlineData(Distribution.PackId, true)]
    [InlineData(Distribution.DeveloperPackId, true)]
    [InlineData(null, false)]
    [InlineData("SomethingElse", false)]
    [InlineData("Shturmap", false)]
    public void Offered_only_in_the_release_and_dev_installs(string? appId, bool offered) =>
        Assert.Equal(offered, Uninstall.Offered(appId));

    [Fact]
    public void Each_install_targets_its_own_data_folder()
    {
        Assert.Equal(Path.Combine(_local, "Shturmap"), Uninstall.DataFolder(Distribution.PackId, _local));
        Assert.Equal(Path.Combine(_local, "Shturmap-dev"), Uninstall.DataFolder(Distribution.DeveloperPackId, _local));
        Assert.Null(Uninstall.DataFolder(null, _local));
        // The same folders the installs use (AppPaths), so nothing else is ever a target.
        Assert.Equal(AppPaths.For(DataFolderKind.Release, null, _local).Root, Uninstall.DataFolder(Distribution.PackId, _local));
        Assert.Equal(AppPaths.For(DataFolderKind.Dev, null, _local).Root, Uninstall.DataFolder(Distribution.DeveloperPackId, _local));
        // The dev build's folder doesn't hold the download cache it shares with the release.
        Assert.StartsWith(Path.Combine(_local, "Shturmap"), AppPaths.For(DataFolderKind.Dev, null, _local).CacheRoot);
    }

    [Fact]
    public void Only_exactly_the_installs_data_folder_may_go()
    {
        var release = Path.Combine(_local, "Shturmap");
        var dev = Path.Combine(_local, "Shturmap-dev");
        Assert.True(Uninstall.MayDelete(release, Distribution.PackId, _local));
        Assert.True(Uninstall.MayDelete(release + Path.DirectorySeparatorChar, Distribution.PackId, _local));
        Assert.True(Uninstall.MayDelete(dev.ToUpperInvariant(), Distribution.DeveloperPackId, _local));

        // Another build's folder, the parent, a child, a sibling, tricks with "..", other ids, nothing.
        Assert.False(Uninstall.MayDelete(dev, Distribution.PackId, _local));
        Assert.False(Uninstall.MayDelete(release, Distribution.DeveloperPackId, _local));
        Assert.False(Uninstall.MayDelete(_local, Distribution.PackId, _local));
        Assert.False(Uninstall.MayDelete(Path.Combine(release, "cache"), Distribution.PackId, _local));
        Assert.False(Uninstall.MayDelete(Path.Combine(_local, "ShturmapApp"), Distribution.PackId, _local));
        Assert.False(Uninstall.MayDelete(Path.Combine(dev, "..", "Shturmap", "cache"), Distribution.PackId, _local));
        Assert.False(Uninstall.MayDelete(Path.Combine(_local, "other", "Shturmap"), Distribution.PackId, _local));
        Assert.False(Uninstall.MayDelete(release, null, _local));
        Assert.False(Uninstall.MayDelete(release, "Shturmap", _local));
        Assert.False(Uninstall.MayDelete("", Distribution.PackId, _local));
        Assert.False(Uninstall.MayDelete("C:\\", Distribution.PackId, _local));
    }

    [Fact]
    public void A_link_in_place_of_the_folder_is_refused()
    {
        var target = Directory.CreateDirectory(Path.Combine(_local, "elsewhere")).FullName;
        File.WriteAllText(Path.Combine(target, "keep.txt"), "x");
        var link = Path.Combine(_local, "Shturmap-dev");
        try
        {
            Directory.CreateSymbolicLink(link, target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A symbolic link needs developer mode or admin rights on Windows; a junction, the other kind, doesn't.
            using var mklink = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", ["/c", "mklink", "/J", link, target])
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            })!;
            mklink.WaitForExit();
            if (!Directory.Exists(link))
                Assert.Skip("Can't create a link here: " + e.Message);
        }
        try
        {
            Assert.False(Uninstall.MayDelete(link, Distribution.DeveloperPackId, _local));
            Assert.False(Uninstall.DeleteData(link, Distribution.DeveloperPackId, _local, TimeSpan.Zero));
            Assert.True(File.Exists(Path.Combine(target, "keep.txt")));
        }
        finally
        {
            // The link itself, not what it points to.
            Directory.Delete(link);
        }
    }

    [Fact]
    public void Deleting_takes_the_folder_and_nothing_beside_it()
    {
        var dev = Directory.CreateDirectory(Path.Combine(_local, "Shturmap-dev", "logs")).Parent!.FullName;
        File.WriteAllText(Path.Combine(dev, "shturmap.db"), "x");
        var release = Directory.CreateDirectory(Path.Combine(_local, "Shturmap", "cache")).Parent!.FullName;
        File.WriteAllText(Path.Combine(release, "shturmap.db"), "y");

        Assert.True(Uninstall.DeleteData(dev, Distribution.DeveloperPackId, _local, TimeSpan.Zero));
        Assert.False(Directory.Exists(dev));
        Assert.True(File.Exists(Path.Combine(release, "shturmap.db")));
        Assert.True(Directory.Exists(Path.Combine(release, "cache")));
        // A folder that isn't there is gone already.
        Assert.True(Uninstall.DeleteData(dev, Distribution.DeveloperPackId, _local, TimeSpan.Zero));
        // A refused target is left alone.
        Assert.False(Uninstall.DeleteData(release, Distribution.DeveloperPackId, _local, TimeSpan.Zero));
        Assert.True(Directory.Exists(release));
    }

    [Fact]
    public void Only_a_fresh_note_asks_for_the_data_to_go()
    {
        var install = Directory.CreateDirectory(Path.Combine(_local, "ShturmapDev")).FullName;
        var now = new DateTime(2026, 10, 3, 20, 0, 0, DateTimeKind.Local);
        Assert.False(Uninstall.IntentFresh(install, now));

        Uninstall.SetIntent(install, deleteData: true, now);
        Assert.True(Uninstall.IntentFresh(install, now.AddSeconds(30)));
        Assert.False(Uninstall.IntentFresh(install, now + Uninstall.IntentLifetime + TimeSpan.FromSeconds(1)));

        // Unticked: an old note is removed, so a later uninstall from Windows' Settings keeps the data.
        Uninstall.SetIntent(install, deleteData: false, now);
        Assert.False(File.Exists(Path.Combine(install, Uninstall.IntentFile)));
        Assert.False(Uninstall.IntentFresh(install, now));

        File.WriteAllText(Path.Combine(install, Uninstall.IntentFile), "not a time");
        Assert.False(Uninstall.IntentFresh(install, now));
    }
}
