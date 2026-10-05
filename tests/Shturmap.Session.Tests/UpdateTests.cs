namespace Shturmap.Session.Tests;

// New versions through Velopack from GitHub Releases (owner, 2026-10-03; docs/DESIGN.md §8, "Distribution").
public class UpdateTests
{
    [Fact]
    public void Updates_are_automatic_unless_the_player_says_otherwise()
    {
        Assert.Equal(UpdateMode.Automatic, UpdateModes.Parse(null));
        Assert.Equal(UpdateMode.Automatic, UpdateModes.Parse("something else"));
        foreach (var mode in Enum.GetValues<UpdateMode>())
            Assert.Equal(mode, UpdateModes.Parse(UpdateModes.Format(mode)));
    }

    [Theory]
    // A build that can't update (not installed, or a developer run) asks nothing and shows nothing.
    [InlineData(UpdateMode.Automatic, false, false, UpdateStage.None, false, false, "", false, false)]
    [InlineData(UpdateMode.Automatic, false, false, UpdateStage.Ready, false, false, "", false, false)]
    // Off: no request at all.
    [InlineData(UpdateMode.Off, true, false, UpdateStage.None, false, false, "", false, false)]
    // Automatic: asks, downloads what it finds, says so when it is ready.
    [InlineData(UpdateMode.Automatic, true, false, UpdateStage.None, true, false, "", false, false)]
    [InlineData(UpdateMode.Automatic, true, false, UpdateStage.Found, true, true, "", false, false)]
    [InlineData(UpdateMode.Automatic, true, false, UpdateStage.Downloading, false, false, "Downloading Shturmap 0.2.1…", false, false)]
    [InlineData(UpdateMode.Automatic, true, false, UpdateStage.Ready, false, false, "Update 0.2.1 ready: applies at next start", false, true)]
    // In a raid: it may still ask and download, but says nothing and never offers a restart.
    [InlineData(UpdateMode.Automatic, true, true, UpdateStage.Found, true, true, "", false, false)]
    [InlineData(UpdateMode.Automatic, true, true, UpdateStage.Ready, false, false, "", false, false)]
    // Tell me only: asks, and offers the download instead of taking it.
    [InlineData(UpdateMode.TellOnly, true, false, UpdateStage.Found, true, false, "Shturmap 0.2.1 is available", true, false)]
    [InlineData(UpdateMode.TellOnly, true, true, UpdateStage.Found, true, false, "", false, false)]
    [InlineData(UpdateMode.TellOnly, true, false, UpdateStage.Ready, false, false, "Update 0.2.1 ready: applies at next start", false, true)]
    // A version downloaded before the player chose Off still applies at the next start; the line says so.
    [InlineData(UpdateMode.Off, true, false, UpdateStage.Ready, false, false, "Update 0.2.1 ready: applies at next start", false, true)]
    public void What_happens_follows_the_setting_the_build_and_the_raid(UpdateMode mode, bool canUpdate, bool inRaid, UpdateStage stage,
        bool check, bool download, string line, bool offerDownload, bool offerRestart) =>
        Assert.Equal(new UpdateDecision(check, download, line, offerDownload, offerRestart),
            UpdatePolicy.Decide(mode, canUpdate, inRaid, stage, "0.2.1"));

    // A version downloaded before this start and still waiting couldn't be applied (owner, 2026-10-05: "it tells me
    // always that the update is available even though i downloaded and restarted"): the line says what to do, RESTART
    // NOW stays, and a notice says it once.
    [Fact]
    public void A_version_that_didnt_apply_says_what_to_close()
    {
        Assert.Equal(new UpdateDecision(false, false, "Update 0.2.1 didn't apply: close what Shturmap opened, then restart", false, true),
            UpdatePolicy.Decide(UpdateMode.Automatic, canUpdate: true, inRaid: false, UpdateStage.Ready, "0.2.1", notApplied: true));
        // In a raid it says nothing, as ever.
        Assert.Equal("", UpdatePolicy.Decide(UpdateMode.Automatic, true, inRaid: true, UpdateStage.Ready, "0.2.1", notApplied: true).Line);
        var notice = UpdatePolicy.NotAppliedNotice("0.2.1");
        Assert.StartsWith("Update 0.2.1 couldn't be applied", notice, StringComparison.Ordinal);
        Assert.Contains("browser", notice, StringComparison.Ordinal);
        Assert.DoesNotContain("game", notice, StringComparison.Ordinal);
    }

    [Fact]
    public void Asks_at_start_then_every_six_hours_at_most()
    {
        var now = new DateTime(2026, 10, 3, 12, 0, 0);
        Assert.True(UpdatePolicy.CheckDue(null, now));
        Assert.False(UpdatePolicy.CheckDue(now.AddHours(-5.9), now));
        Assert.True(UpdatePolicy.CheckDue(now.AddHours(-6), now));
    }

    // Settings say why a run can't update (the review of 2026-10-04, A40): an installed Shturmap whose updater
    // couldn't start was told "not available in this build", which wasn't the reason.
    [Fact]
    public void Settings_say_why_this_run_does_not_update()
    {
        var failed = Distribution.NoUpdatesText(installed: true, updaterFailed: true);
        Assert.Contains("couldn't start", failed, StringComparison.Ordinal);
        Assert.Contains("Setup", failed, StringComparison.Ordinal);
        foreach (var notInstalled in new[] { Distribution.NoUpdatesText(false, false), Distribution.NoUpdatesText(false, true), Distribution.NoUpdatesText(true, false) })
            Assert.StartsWith("Updates: not available in this build", notInstalled, StringComparison.Ordinal);
    }

    // Velopack installs to %LOCALAPPDATA%\<pack id> and an uninstall deletes that folder: it must never be the data
    // folder, and the release must pack with the same id.
    // Both installs (the release's and the dev build's) against every data folder, the shared cache included.
    [Fact]
    public void The_install_folder_is_never_the_data_folder()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var packId in new[] { Distribution.PackId, Distribution.DeveloperPackId })
        {
            var install = Path.TrimEndingDirectorySeparator(Distribution.InstallFolder(local, packId)) + Path.DirectorySeparatorChar;
            Assert.NotEqual("Shturmap", packId, StringComparer.OrdinalIgnoreCase);
            Assert.NotEqual("Shturmap-dev", packId, StringComparer.OrdinalIgnoreCase);
            foreach (var paths in new[] { AppPaths.For(DataFolderKind.Release, null, local), AppPaths.For(DataFolderKind.Dev, null, local), AppPaths.Default })
            {
                var data = Path.TrimEndingDirectorySeparator(Path.GetFullPath(paths.Root)) + Path.DirectorySeparatorChar;
                Assert.False(data.StartsWith(install, StringComparison.OrdinalIgnoreCase), $"{data} lies in {install}");
                Assert.False(install.StartsWith(data, StringComparison.OrdinalIgnoreCase), $"{install} lies in {data}");
                foreach (var folder in new[] { paths.Logs, paths.Study, paths.CacheRoot, paths.Database })
                    Assert.False(Path.GetFullPath(folder).StartsWith(install, StringComparison.OrdinalIgnoreCase), folder);
            }
        }
        Assert.NotEqual(Distribution.PackId, Distribution.DeveloperPackId);
    }

    // The dev build is its own Velopack app: its own install folder, shortcuts ("Shturmap DEV") and local feed; it
    // never packs as the release and never asks GitHub.
    [Fact]
    public void The_dev_build_packs_with_its_own_id_and_never_as_the_release()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "eng", "dev.ps1"));
        Assert.Contains($"$packId = '{Distribution.DeveloperPackId}'", script);
        Assert.Contains("--packId $packId", script);
        Assert.Contains("-p:ShturmapDev=true", script);
        Assert.Contains("--packTitle 'Shturmap DEV'", script);
        Assert.DoesNotContain($"'{Distribution.PackId}'", script);
        Assert.DoesNotMatch(@"(?i)vpk\s+(download|upload)\s+github|--repoUrl", script);
    }

    [Fact]
    public void The_release_packs_with_the_apps_id()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "eng", "release.ps1"));
        Assert.Contains($"$packId = '{Distribution.PackId}'", script);
        Assert.Contains("--packId $packId", script);
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
