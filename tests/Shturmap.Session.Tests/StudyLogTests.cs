#if DEVTOOLS
namespace Shturmap.Session.Tests;

// The study log exists only in developer builds, on unless switched off in settings, or --study for one session; never
// for snapshot and fake-game runs (owner, 2026-10-03: "The study log should only be part of the dev version and not be
// in the release version"). A release compiles the switch and the writer out (DevToolsGuardTests checks the build).
public class StudyLogTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-study-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public void This_build_has_a_study_log() => Assert.True(StudyLog.Available);

    [Fact]
    public void A_new_study_log_writes_nothing_until_enabled()
    {
        using var study = new StudyLog(_folder);
        study.Game("app.start");
        study.Ui("map.preview");
        Assert.False(study.Enabled);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void Switched_on_it_writes()
    {
        using var study = new StudyLog(_folder) { Enabled = true };
        study.Game("app.start");
        Assert.Single(Directory.GetFiles(_folder, "*.jsonl"));
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, "off", false)]
    [InlineData(null, "on", true)]
    [InlineData(true, "off", true)]
    [InlineData(false, null, false)]
    [InlineData(false, "on", false)]
    public void On_unless_switched_off_and_the_command_line_decides_first(bool? studyOverride, string? setting, bool on) =>
        Assert.Equal(on, GameSession.StudyOn(studyOverride, setting));

    [Theory]
    [InlineData(new string[0], null)]
    [InlineData(new[] { "--study" }, true)]
    [InlineData(new[] { "--snapshot", "out", "--study" }, false)]
    [InlineData(new[] { "--fake-game", "x" }, false)]
    [InlineData(new[] { "--verbose" }, null)]
    public void Study_on_the_command_line_counts_for_one_session_and_never_for_snapshots_or_fake_games(string[] cli, bool? studyOverride) =>
        Assert.Equal(studyOverride, GameSession.StudyOverrideFor(cli));

    [Fact]
    public void Thirty_days_are_kept()
    {
        foreach (var name in new[] { "2026-09-02.jsonl", "2026-09-03.jsonl", "2026-10-03.jsonl", "running" })
            File.WriteAllText(Path.Combine(_folder, name), "x");
        new StudyLog(_folder).Prune(new DateTime(2026, 10, 3, 8, 0, 0));
        Assert.Equal(["2026-09-03.jsonl", "2026-10-03.jsonl", "running"], Directory.GetFiles(_folder).Select(Path.GetFileName).Order());
    }
}
#endif
