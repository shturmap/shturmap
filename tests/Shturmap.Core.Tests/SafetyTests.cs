namespace Shturmap.Core.Tests;

/// <summary>
/// Shturmap's boundary, enforced on every test run: nothing in the shipped code may touch another process,
/// synthesise input, hook the system or capture the screen. See docs/DESIGN.md, "Ground rules".
/// </summary>
public class SafetyTests
{
    private static readonly string[] Forbidden =
    [
        // other processes
        "OpenProcess", "ReadProcessMemory", "WriteProcessMemory", "VirtualAllocEx", "CreateRemoteThread",
        "NtReadVirtualMemory", "GetProcessesByName", "Process.GetProcesses", "MainModule", "EnumProcessModules",
        // input (the Windows clipboard isn't: the stash desk's click puts a list price there for the player to paste into the
        // flea's price field, as COPY DIAGNOSTICS puts its text; nothing reaches the game; owner, 2026-10-19: "Copy and paste
        // worked")
        "SendInput", "keybd_event", "mouse_event", "PostMessage", "SendMessage",
        // hooks
        "SetWindowsHookEx", "RegisterHotKey",
        // screen capture
        "BitBlt", "CopyFromScreen", "PrintWindow", "GraphicsCaptureItem", "Windows.Graphics.Capture", "IDXGIOutputDuplication",
        "DesktopDuplication",
    ];

    [Fact]
    public void Shipped_code_never_uses_forbidden_windows_apis()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var violations = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => !l.Text.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .SelectMany(l => Forbidden.Where(token => l.Text.Contains(token, StringComparison.Ordinal))
                .Select(token => $"{Path.GetRelativePath(src, l.File)}:{l.Line} uses {token}"))
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    // The public data and art Shturmap downloads, and its own project page (named in the User-Agent). Reports go to the
    // address the release is built with (eng\sentry.dsn), never one written in the code (docs/DESIGN.md §8, "Reports").
    // New versions come from the repository's GitHub Releases: Velopack asks api.github.com for the list and downloads
    // the packages from GitHub's release-asset hosts, which GitHub redirects to (docs/DESIGN.md §8, "Distribution").
    private static readonly string[] Hosts =
    [
        "json.tarkov.dev", "assets.tarkov.dev", "raw.githubusercontent.com", "github.com",
        "api.github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com",
    ];

    [Fact]
    public void Shipped_code_names_no_other_address()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var violations = Lines(src)
            .SelectMany(l => System.Text.RegularExpressions.Regex.Matches(l.Text, @"https?://([A-Za-z0-9.\-]+)")
                .Select(m => (l.File, l.Line, Host: m.Groups[1].Value)))
            .Where(m => !Hosts.Contains(m.Host, StringComparer.OrdinalIgnoreCase))
            .Select(m => $"{Path.GetRelativePath(src, m.File)}:{m.Line} names {m.Host}")
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Only_the_reporting_code_touches_sentry_and_never_its_automatic_client()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var reporting = Path.Combine(src, "Shturmap.Session", "Reporting") + Path.DirectorySeparatorChar;
        var lines = Lines(src).ToList();
        var violations = lines
            .Where(l => l.Text.Contains("using Sentry", StringComparison.Ordinal) && !l.File.StartsWith(reporting, StringComparison.OrdinalIgnoreCase))
            .Select(l => $"{Path.GetRelativePath(src, l.File)}:{l.Line} uses Sentry outside Reporting")
            .Concat(lines.Where(l => l.Text.Contains("SentrySdk", StringComparison.Ordinal) || l.Text.Contains("new SentryClient", StringComparison.Ordinal))
                .Select(l => $"{Path.GetRelativePath(src, l.File)}:{l.Line} uses Sentry's automatic client"))
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    // Velopack is only the exe's start (Program.cs) and the updater (Updater.cs), and never its own update service
    // (VelopackFlowSource, api.velopack.io, which the source-less UpdateManager uses): GitHub Releases are the source.
    [Fact]
    public void Only_the_updater_touches_velopack_and_only_with_github_as_its_source()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var allowed = new[] { Path.Combine(src, "Shturmap.App", "Program.cs"), Path.Combine(src, "Shturmap.App", "Updater.cs") };
        var lines = Lines(src).ToList();
        var violations = lines
            .Where(l => l.Text.Contains("using Velopack", StringComparison.Ordinal) && !allowed.Contains(l.File, StringComparer.OrdinalIgnoreCase))
            .Select(l => $"{Path.GetRelativePath(src, l.File)}:{l.Line} uses Velopack outside the updater")
            .Concat(lines.Where(l => l.Text.Contains("VelopackFlowSource", StringComparison.Ordinal) || l.Text.Contains("new UpdateManager()", StringComparison.Ordinal)
                                     || l.Text.Contains("new UpdateManager(options", StringComparison.Ordinal))
                .Select(l => $"{Path.GetRelativePath(src, l.File)}:{l.Line} uses Velopack's own update service"))
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    // Every timer of the main window is made by MainWindow.WindowTimer, whose tick does nothing once the app is
    // ending, and the few others stop as it ends: a tick after Application.Exit had taken the window's XAML down threw
    // inside the dispatcher queue and ended the exit with 0xC000027B (the tasks cue's QA fix round, 2026-10-19: a
    // cue's title still decoding). A new timer is made with WindowTimer, or stopped by MainWindow.StopForExit and
    // counted here.
    private static readonly Dictionary<string, int> Timers = new(StringComparer.OrdinalIgnoreCase)
    {
        [Path.Combine("Shturmap.App", "MainWindow.xaml.cs")] = 1, // WindowTimer itself
        [Path.Combine("Shturmap.App", "Controls", "CardStack.cs")] = 2, // Stop, from StopForExit
        [Path.Combine("Shturmap.App", "Controls", "MapView.cs")] = 4, // StopDrawing, from StopForExit
        [Path.Combine("Shturmap.App", "Controls", "DeskBoard.cs")] = 1, // the totals' count-up: Stop, from StopForExit
        [Path.Combine("Shturmap.App", "Controls", "TextScale.cs")] = 1, // stopped as the window closes
        [Path.Combine("Shturmap.App", "Dev", "DevView.cs")] = 1, // stopped as the developer view closes, before the app exits
    };

    [Fact]
    public void Every_timer_of_the_window_stops_as_the_app_ends()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var violations = Lines(src)
            .Where(l => l.Text.Contains("CreateTimer(", StringComparison.Ordinal))
            .GroupBy(l => Path.GetRelativePath(src, l.File), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > Timers.GetValueOrDefault(g.Key))
            .Select(g => $"{g.Key}:{g.First().Line} makes a timer that may tick after the app's exit began: use MainWindow.WindowTimer, "
                         + "or stop it in MainWindow.StopForExit and count it in SafetyTests.Timers")
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    // The readings log (owner, 2026-10-25; docs/DESIGN.md §8, "Developer aids", *Readings log*) stays on the PC: what a
    // report carries (the player's words, the diagnostics, a crash record's exception and log lines) is made by the reporting
    // code and the diagnostics, and none of it names the log or its folder.
    [Fact]
    public void Nothing_that_makes_a_report_or_the_diagnostics_reads_the_readings_log()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var session = Path.Combine(src, "Shturmap.Session");
        string[] reporting = [Path.Combine(session, "Reporting") + Path.DirectorySeparatorChar, Path.Combine(session, "Diagnostics.cs"), Path.Combine(session, "Redact.cs")];
        var violations = Lines(src)
            .Where(l => reporting.Any(r => l.File.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
            .Where(l => l.Text.Contains("ReadingLog", StringComparison.Ordinal) || l.Text.Contains("Readings", StringComparison.Ordinal) ||
                        l.Text.Contains("\"readings\"", StringComparison.OrdinalIgnoreCase))
            .Select(l => $"{Path.GetRelativePath(src, l.File)}:{l.Line} names the readings log, which never goes into a report")
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
        Assert.True(Directory.Exists(Path.Combine(session, "Reporting")), "the reporting code moved: point this test at it");
    }

    // PaddleOCR's recognition runs in ONNX Runtime, whose Windows build writes telemetry events unless told not to (owner,
    // 2026-10-26, decision 1; docs/DESIGN.md §2, "Where Windows' recognition reads nothing"): every file that loads a model
    // switches them off.
    [Fact]
    public void Onnx_runtimes_telemetry_is_switched_off_wherever_a_model_is_loaded()
    {
        var src = Path.Combine(RepositoryRoot(), "src");
        var files = Lines(src).Where(l => l.Text.Contains("new InferenceSession(", StringComparison.Ordinal)).Select(l => l.File).Distinct().ToList();
        Assert.True(files.Count > 0, "no model is loaded in src: point this test at PaddleOCR's reader");
        var violations = files.Where(f => !Lines(src).Any(l => l.File == f && l.Text.Contains("DisableTelemetryEvents()", StringComparison.Ordinal)))
            .Select(f => $"{Path.GetRelativePath(src, f)} loads a model without OrtEnv.Instance().DisableTelemetryEvents()")
            .ToList();
        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    // The shipped code's lines, comments left out.
    private static IEnumerable<(string File, int Line, string Text)> Lines(string src) =>
        Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => !l.Text.TrimStart().StartsWith("//", StringComparison.Ordinal));

    internal static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Shturmap.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
