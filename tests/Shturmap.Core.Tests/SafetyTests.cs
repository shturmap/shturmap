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
        // input
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
    private static readonly string[] Hosts = ["json.tarkov.dev", "assets.tarkov.dev", "raw.githubusercontent.com", "github.com"];

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

    // The shipped code's lines, comments left out.
    private static IEnumerable<(string File, int Line, string Text)> Lines(string src) =>
        Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
            .Where(l => !l.Text.TrimStart().StartsWith("//", StringComparison.Ordinal));

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
