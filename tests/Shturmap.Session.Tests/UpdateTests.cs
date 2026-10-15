using Shturmap.Core.Raid;

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
    [InlineData(UpdateMode.Automatic, false, false, UpdateStage.None, false, false, UpdateBand.None, false, false)]
    [InlineData(UpdateMode.Automatic, false, false, UpdateStage.Ready, false, false, UpdateBand.None, false, false)]
    // Off: no request at all.
    [InlineData(UpdateMode.Off, true, false, UpdateStage.None, false, false, UpdateBand.None, false, false)]
    // Automatic: asks, downloads what it finds, says so when it is ready.
    [InlineData(UpdateMode.Automatic, true, false, UpdateStage.None, true, false, UpdateBand.None, false, false)]
    [InlineData(UpdateMode.Automatic, true, false, UpdateStage.Found, true, true, UpdateBand.None, false, false)]
    [InlineData(UpdateMode.Automatic, true, false, UpdateStage.Downloading, false, false, UpdateBand.Downloading, false, false)]
    [InlineData(UpdateMode.Automatic, true, false, UpdateStage.Ready, false, false, UpdateBand.Ready, false, true)]
    // In a raid (and while one loads): it may still ask and download, but the band hides and never offers a restart.
    [InlineData(UpdateMode.Automatic, true, true, UpdateStage.Found, true, true, UpdateBand.None, false, false)]
    [InlineData(UpdateMode.Automatic, true, true, UpdateStage.Ready, false, false, UpdateBand.None, false, false)]
    // Tell me only: asks, and offers the download instead of taking it.
    [InlineData(UpdateMode.TellOnly, true, false, UpdateStage.Found, true, false, UpdateBand.Available, true, false)]
    [InlineData(UpdateMode.TellOnly, true, true, UpdateStage.Found, true, false, UpdateBand.None, false, false)]
    [InlineData(UpdateMode.TellOnly, true, false, UpdateStage.Ready, false, false, UpdateBand.Ready, false, true)]
    // A version downloaded before the player chose Off still applies at the next start; the band says so.
    [InlineData(UpdateMode.Off, true, false, UpdateStage.Ready, false, false, UpdateBand.Ready, false, true)]
    public void What_happens_follows_the_setting_the_build_and_the_raid(UpdateMode mode, bool canUpdate, bool inRaid, UpdateStage stage,
        bool check, bool download, UpdateBand band, bool offerDownload, bool offerRestart) =>
        Assert.Equal(new UpdateDecision(check, download, band, offerDownload, offerRestart),
            UpdatePolicy.Decide(mode, canUpdate, inRaid, stage, "0.2.1"));

    // A version downloaded before this start and still waiting couldn't be applied (owner, 2026-10-05: "it tells me
    // always that the update is available even though i downloaded and restarted"): the band says what to do, RESTART
    // NOW stays, and a notice says it once.
    [Fact]
    public void A_version_that_didnt_apply_says_what_to_close()
    {
        Assert.Equal(new UpdateDecision(false, false, UpdateBand.DidNotApply, false, true),
            UpdatePolicy.Decide(UpdateMode.Automatic, canUpdate: true, inRaid: false, UpdateStage.Ready, "0.2.1", notApplied: true));
        Assert.Equal(("UPDATE DIDN'T APPLY", "Close what Shturmap opened, then restart."), UpdatePolicy.Words(UpdateBand.DidNotApply));
        // In a raid it says nothing, as ever.
        Assert.Equal(UpdateBand.None, UpdatePolicy.Decide(UpdateMode.Automatic, true, inRaid: true, UpdateStage.Ready, "0.2.1", notApplied: true).Band);
        var notice = UpdatePolicy.NotAppliedNotice("0.2.1");
        Assert.StartsWith("Update 0.2.1 couldn't be applied", notice, StringComparison.Ordinal);
        Assert.Contains("browser", notice, StringComparison.Ordinal);
        Assert.DoesNotContain("game", notice, StringComparison.Ordinal);
    }

    // The band pinned under MAP (owner, 2026-10-15: "A Beta-tester did not realize there is an update, which shows me
    // that it is currently too subtle. I, however, do not want to have a blocking modal like a pop-up"; from a panel of
    // four: B, "A cue, then the band"): an eyebrow per stage, the version on a line of its own (the window's), and a note
    // that says what RESTART NOW does.
    [Theory]
    [InlineData(UpdateBand.Available, "DOWNLOAD AVAILABLE", "Downloads in the background. Applies at the next start.")]
    [InlineData(UpdateBand.Downloading, "DOWNLOADING", "Applies at the next start.")]
    [InlineData(UpdateBand.Ready, "UPDATE READY", "Restarts Shturmap in a few seconds. Never in a raid.")]
    [InlineData(UpdateBand.None, "", "")]
    public void The_band_says_the_stage_and_what_its_button_does(UpdateBand band, string eyebrow, string note) =>
        Assert.Equal((eyebrow, note), UpdatePolicy.Words(band));

    // ---- the UPDATE READY cue: when the download completes in the menus, then once per raid while it waits ----

    [Fact]
    public void The_cue_shows_when_the_download_completes_in_the_menus_and_once()
    {
        var cue = new UpdateCue();
        Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Found, "0.2.1", RaidPhase.Menu));
        Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Downloading, "0.2.1", RaidPhase.Menu));
        Assert.True(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
        cue.Shown(UpdateStage.Ready, "0.2.1");
        Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
        // A newer version downloaded later is said again.
        Assert.True(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.2", RaidPhase.Menu));
    }

    // A download that completes in a raid, or while one loads, waits for the menus: the first menu after it.
    [Fact]
    public void A_download_completed_in_a_raid_cues_at_the_first_menu()
    {
        var cue = new UpdateCue();
        Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Loading));
        Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.InRaid));
        cue.RaidEnded();
        Assert.True(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
        cue.Shown(UpdateStage.Ready, "0.2.1");
        // Once: the raid it completed in counts as the one just over.
        Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
    }

    // While the update waits, once more at the first menu after each raid, and only once per raid.
    [Fact]
    public void The_cue_comes_back_once_per_raid_while_the_update_waits()
    {
        var cue = new UpdateCue();
        cue.Shown(UpdateStage.Ready, "0.2.1");
        for (var raid = 0; raid < 3; raid++)
        {
            Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Loading));
            Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.InRaid));
            cue.RaidEnded();
            // Never in a raid or while one loads, even when due (a transit, the next raid loading at once).
            Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Loading));
            Assert.True(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
            cue.Shown(UpdateStage.Ready, "0.2.1");
            Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
        }
        // A load cancelled is no raid: nothing ended it (the window tells the rule of RAID OVER only).
        Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
    }

    // The start: a version still waiting has its notice ("couldn't be applied"), not the cue, and the raids read back
    // from the log at start never end one for the rule (no RAID OVER then); the first raid over live brings it.
    [Fact]
    public void The_start_and_its_read_back_never_cue()
    {
        var cue = new UpdateCue();
        cue.AtStart(UpdateStage.Ready, "0.2.1");
        foreach (var phase in new[] { RaidPhase.Loading, RaidPhase.InRaid, RaidPhase.Menu })
            Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", phase));
        cue.RaidEnded();
        Assert.True(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
        // Nothing waiting at start: what a check at start finds and downloads is new, and cues.
        var fresh = new UpdateCue();
        fresh.AtStart(UpdateStage.None, null);
        Assert.True(fresh.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
    }

    // Tell me only: a version found cues (DOWNLOAD AVAILABLE), and its download completing cues again (UPDATE READY).
    [Fact]
    public void Tell_me_only_cues_when_a_version_is_found()
    {
        var cue = new UpdateCue();
        Assert.True(cue.Due(UpdateMode.TellOnly, true, UpdateStage.Found, "0.2.1", RaidPhase.Menu));
        cue.Shown(UpdateStage.Found, "0.2.1");
        Assert.False(cue.Due(UpdateMode.TellOnly, true, UpdateStage.Found, "0.2.1", RaidPhase.Menu));
        Assert.False(cue.Due(UpdateMode.TellOnly, true, UpdateStage.Downloading, "0.2.1", RaidPhase.Menu));
        Assert.True(cue.Due(UpdateMode.TellOnly, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
        Assert.True(UpdateCue.About(UpdateMode.TellOnly, UpdateStage.Found));
        Assert.False(UpdateCue.About(UpdateMode.Automatic, UpdateStage.Found));
    }

    // Off never cues, a version downloaded before the player chose it included (its band still says it), and a build
    // that can't update has nothing to say.
    [Fact]
    public void Off_and_a_build_that_cant_update_never_cue()
    {
        var cue = new UpdateCue();
        cue.RaidEnded();
        Assert.False(cue.Due(UpdateMode.Off, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
        Assert.False(cue.Due(UpdateMode.Off, true, UpdateStage.Found, "0.2.1", RaidPhase.Menu));
        Assert.False(cue.Due(UpdateMode.Automatic, false, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
        Assert.Equal(UpdateBand.Ready, UpdatePolicy.Decide(UpdateMode.Off, true, false, UpdateStage.Ready, "0.2.1").Band);
    }

    // RESTART NOW clicked: nothing more, whatever comes of it (the updater couldn't start, the session goes on).
    [Fact]
    public void After_restart_now_nothing_more()
    {
        var cue = new UpdateCue();
        Assert.True(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
        cue.Restarting();
        Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.1", RaidPhase.Menu));
        cue.RaidEnded();
        Assert.False(cue.Due(UpdateMode.Automatic, true, UpdateStage.Ready, "0.2.2", RaidPhase.Menu));
    }

#if DEVTOOLS
    // tools\qa\update-ready.devscript (docs/QA.md, "Replay scripts"): the update taken for downloaded in the menus, stills
    // of the cue and the band, then a raid (the band gone, the raid card's head in its row) and its end (the cue again).
    // Developer builds only, as the scripts are.
    [Fact]
    public void The_qa_replay_of_the_update_band_reads_without_errors()
    {
        var (steps, errors) = Dev.DevScript.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", "qa", "update-ready.devscript")));
        Assert.Empty(errors);
        Assert.Equal("tour end", steps[0].ToString());
        var at = steps.ToList();
        var trigger = at.FindIndex(s => s.ToString() == "trigger update");
        Assert.True(trigger > 0);
        Assert.True(at.FindIndex(s => s.Verb == "start") > trigger);
        Assert.True(at.FindIndex(s => s.Verb == "end") > at.FindIndex(s => s.Verb == "start"));
        Assert.Equal(["t0", "t1", "t2", "t3", "t4"], steps.Where(s => s.Verb == "snapshot").Select(s => Path.GetFileName(s.Arg(0))));
        Assert.All(steps.Where(s => s.Verb == "snapshot"), s => Assert.StartsWith(@"%TEMP%\shturmap-qa\update-ready\", s.Arg(0)));
        Assert.Equal("exit", steps[^1].Verb);
    }
#endif

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
