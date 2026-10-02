using Shturmap.Core.Logs;
using Shturmap.Core.Quests;
using Shturmap.Data.TarkovDev;
using Shturmap.Game.Install;

namespace Shturmap.Session.Tests;

// "Copy diagnostics": what a report needs, masked, with no ids and no quest lists (owner, 2026-10-03).
public class DiagnosticsTests
{
    private const string Profile = @"C:\Users\Jane Doe";
    private const string ProfileId = "5f2a9c1e8b7d6a4f3e2d1c0b";

    private static SessionSnapshot Snapshot(LoadProblem? problem = null)
    {
        var install = new InstallCandidate(InstallKind.Steam, @"C:\Users\Jane Doe\Games\EFT\build", @"C:\Users\Jane Doe\Games\EFT\build\Logs",
            null, "Steam library", null);
        var quests = new Dictionary<string, QuestStatus>
        {
            ["5936d90786f7742b1420ba5b"] = new("5936d90786f7742b1420ba5b", QuestState.Active, ObservationSource.Log, null),
            ["5967733e86f774602332fc84"] = new("5967733e86f774602332fc84", QuestState.Active, ObservationSource.Log, null),
        };
        return new SessionSnapshot
        {
            Mode = GameMode.Pve,
            Locations = new GameLocations(install, [install], @"C:\Users\Jane Doe\OneDrive\Documents\Escape from Tarkov\Screenshots", @"C:\Users\Jane Doe\AppData\Roaming"),
            GameLanguage = "ge",
            DataProblem = problem,
            Quests = quests,
            StudyLogOn = true,
        };
    }

    private static string Build(SessionSnapshot s, params string[] log) =>
        Diagnostics.Build(s, "0.1.0+d349909", "Windows 11 (10.0.26200)", "single exe", log, new DateTime(2026, 10, 3, 14, 5, 0), Profile);

    [Fact]
    public void It_has_the_fields_a_report_needs()
    {
        var text = Build(Snapshot(LoadProblem.Explain(new HttpRequestException("x", null, System.Net.HttpStatusCode.NotFound))));
        foreach (var expected in new[]
        {
            "Shturmap: 0.1.0+d349909 (single exe)", "Windows: Windows 11 (10.0.26200)", "Install: Steam", "Game folder: found",
            "Logs folder: found", "Mode: Pve", "Game language: ge", "tarkov.dev language: not loaded",
            "Data: failed: Refused 404: tarkov.dev answered 404.", "Active quests: 2", "Study log: on",
        })
            Assert.Contains(expected, text);
    }

    [Fact]
    public void Paths_are_masked_and_ids_and_quest_names_left_out()
    {
        var text = Build(Snapshot(), @"2026-10-03 14:00:00.000 INFO Logs: C:\Users\Jane Doe\Games\EFT\build\Logs",
            $"2026-10-03 14:01:00.000 INFO profile {ProfileId} joined");
        Assert.DoesNotContain("Jane", text);
        Assert.DoesNotContain(ProfileId, text);
        Assert.Contains("profile <id> joined", text);
        Assert.Contains(@"%USERPROFILE%\Games", text);
        Assert.DoesNotContain("5936d90786f7742b1420ba5b", text);
        Assert.DoesNotContain("5967733e86f774602332fc84", text);
    }

    [Fact]
    public void Only_the_last_lines_of_the_log_come_along()
    {
        var log = Enumerable.Range(0, 250).Select(i => $"line {i:000}").ToArray();
        var text = Build(Snapshot(), log);
        Assert.DoesNotContain("line 049", text);
        Assert.Contains("line 050", text);
        Assert.Contains("line 249", text);
    }
}
