namespace Spotter.Core.Tests;

/// <summary>
/// Spotter's boundary, enforced on every test run: nothing in the shipped code may touch another process,
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

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Spotter.slnx")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
