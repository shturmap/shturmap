using System.Globalization;
using System.Text;
using Shturmap.Game.Install;

namespace Shturmap.Session;

/// <summary>
/// The text "Copy diagnostics" puts on the clipboard, for the player to paste into a report: versions, what was found,
/// the data's state and the end of today's app log (owner, 2026-10-03; docs/DESIGN.md §8, "Diagnostics"). It sends
/// nothing itself: the player pastes it, or a report carries it, shown first (§8, "Reports"). Paths are masked, ids
/// of any kind are cut (<see cref="Redact"/>), no quest lists.
/// </summary>
public static class Diagnostics
{
    public const int LogLines = 200;

    /// <param name="build">"installed" (its Setup, Velopack), "dev build" or "folder build".</param>
    /// <param name="profile">The user's profile folder, written as %USERPROFILE%.</param>
    /// <param name="dataFolder">Which data folder this build uses (<see cref="AppPaths.KindText"/>).</param>
    public static string Build(SessionSnapshot s, string version, string windows, string build, IReadOnlyList<string> logTail,
        DateTime now, string? profile, string? dataFolder = null)
    {
        var text = new StringBuilder();
        void Line(string key, string value) => text.Append(key).Append(": ").AppendLine(value);
        static string YesNo(bool found) => found ? "found" : "not found";

        text.AppendLine("Shturmap diagnostics");
        Line("Shturmap", $"{version} ({build})");
        if (dataFolder is not null)
            Line("Data folder", dataFolder);
        Line("Windows", windows);
        Line("Copied", now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        var install = s.Locations?.Install;
        Line("Install", install is null ? "not found" : install.Kind switch
        {
            InstallKind.Steam => "Steam",
            InstallKind.BsgLauncher => "BSG launcher",
            _ => "manual",
        });
        Line("Game folder", YesNo(install is not null));
        Line("Logs folder", YesNo(s.Locations?.LogsFolder is not null));
        Line("Screenshots folder", YesNo(s.Locations is { } l && Directory.Exists(l.ScreenshotsFolder)));
        Line("Mode", s.Mode.ToString());
        Line("Game language", s.GameLanguage ?? "unknown");
        Line("tarkov.dev language", s.Data is { } d
            ? d.Language + (d.MissingLanguage is { } missing ? $" (no '{missing}' texts there)" : "")
                // Why a session in another game language reads English: its texts couldn't be loaded.
                + (d.LanguageFailure is { } failed ? $" ('{failed.Language}' texts not loaded: {failed.Why.Kind}{(failed.Why.Status is { } answered ? " " + answered : "")})" : "")
            : "not loaded");
        Line("Data", s.Data switch
        {
            { Offline: true } data => $"offline copy, checked {data.CheckedAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            { } data => $"loaded, checked {data.CheckedAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            null when s.DataProblem is { } problem => $"failed: {problem.Kind}{(problem.Status is { } code ? " " + code : "")}: {problem.What}",
            null => "loading",
        });
        Line("Active quests", s.ActiveQuestCount.ToString(CultureInfo.InvariantCulture));
        // Answers "where did my screenshots go?" in a report.
        Line("Delete position screenshots", s.DeleteScreenshots ? "on" : "off");
        // And "why doesn't it show my extracts?": the setting, and whether Windows can read the list at all.
        Line("Read the extract list from screenshots", !s.ReadExits ? "off"
            : s.ExitReaderMissing ? "on, but Windows has no text recognition language"
            : s.ExitReaderLanguage is { } language ? $"on ({language})" : "on");
#if DEVTOOLS
        // Developer builds only: a release has no study log (owner, 2026-10-03).
        Line("Study log", s.StudyLogOn ? "on" : "off");
#endif
        text.AppendLine();
        text.AppendLine($"App log, today's last {LogLines} lines:");
        foreach (var line in logTail.TakeLast(LogLines))
            text.AppendLine(line);
        return Redact.Text(text.ToString(), profile);
    }

    /// <summary>"Windows 11 (10.0.26200)".</summary>
    public static string WindowsVersion()
    {
        var v = Environment.OSVersion.Version;
        return $"Windows {(v.Major == 10 && v.Build >= 22000 ? "11" : v.Major.ToString(CultureInfo.InvariantCulture))} ({v.Major}.{v.Minor}.{v.Build})";
    }
}
