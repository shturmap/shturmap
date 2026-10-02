namespace Shturmap.Session.Tests;

// The study log is the player's choice: off unless switched on in help, or --study for one session (owner, 2026-10-03).
public class StudyLogTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shturmap-study-").FullName;

    public void Dispose() => Directory.Delete(_folder, true);

    [Fact]
    public void A_new_study_log_writes_nothing()
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
    [InlineData(null, null, false)]
    [InlineData(null, "off", false)]
    [InlineData(null, "on", true)]
    [InlineData(true, null, true)]
    [InlineData(false, "on", false)]
    public void The_switch_decides_unless_the_command_line_does(bool? studyOverride, string? setting, bool on) =>
        Assert.Equal(on, GameSession.StudyOn(studyOverride, setting));

    [Theory]
    [InlineData(new string[0], null)]
    [InlineData(new[] { "--study" }, true)]
    [InlineData(new[] { "--snapshot", "out", "--study" }, false)]
    [InlineData(new[] { "--fake-game", "x" }, false)]
    [InlineData(new[] { "--verbose" }, null)]
    public void Study_on_the_command_line_counts_for_one_session_and_never_for_developer_runs(string[] cli, bool? studyOverride) =>
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
