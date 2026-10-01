using System.Globalization;

namespace Spotter.Session;

/// <summary>
/// A small diagnostic log in %LOCALAPPDATA%\Spotter\logs, one file per day, kept for 7 days. Records what
/// Spotter did (startup, raids, scans, errors); never game file contents.
/// </summary>
public static class AppLog
{
    private static readonly Lock Gate = new();
    private static string? _folder;

    public static void Initialize(string folder)
    {
        _folder = folder;
        try
        {
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.EnumerateFiles(folder, "spotter-*.log").Where(f => File.GetLastWriteTime(f) < DateTime.Now.AddDays(-7)))
                File.Delete(old);
        }
        catch (IOException)
        {
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? e = null) => Write("ERROR", e is null ? message : $"{message}: {e}");

    private static void Write(string level, string message)
    {
        if (_folder is null)
            return;
        var line = string.Create(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}");
        lock (Gate)
        {
            try
            {
                File.AppendAllText(Path.Combine(_folder, $"spotter-{DateTime.Now:yyyy-MM-dd}.log"), line);
            }
            catch (IOException)
            {
            }
        }
    }
}
