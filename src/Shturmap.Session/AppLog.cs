using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Shturmap.Session;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

/// <summary>
/// A small diagnostic log in %LOCALAPPDATA%\Shturmap\logs, one file per day, kept for 7 days: a short support trail
/// (start, the game found, data loaded or why not, mode and raid changes, errors). DEBUG lines only with
/// <c>--verbose</c>. The user's profile path is written as %USERPROFILE%; never game file contents, never profile or
/// account ids (owner, 2026-10-03; docs/DESIGN.md §8, "App log").
/// </summary>
public static class AppLog
{
    private static LogFile? _file;

    /// <summary>Writes DEBUG lines too (<c>--verbose</c>, and the website demo, which times itself by them).</summary>
    public static bool Verbose { get; set; }

    public static string? Folder => _file?.Folder;

    public static void Initialize(string folder)
    {
        _file = new LogFile(folder, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        _file.Prune(DateTime.Now);
    }

    public static void Debug(string message)
    {
        if (Verbose)
            _file?.Write(DateTime.Now, LogLevel.Debug, message);
    }

    public static void Info(string message) => _file?.Write(DateTime.Now, LogLevel.Info, message);

    public static void Warn(string message, Exception? e = null) => _file?.Write(DateTime.Now, LogLevel.Warn, e is null ? message : $"{message}: {e}");

    public static void Error(string message, Exception? e = null) => _file?.Write(DateTime.Now, LogLevel.Error, e is null ? message : $"{message}: {e}");

    /// <summary>The text with the user's profile path written as %USERPROFILE%.</summary>
    public static string Mask(string text) => LogFile.Mask(text, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>The last lines of today's log, for the diagnostics.</summary>
    public static IReadOnlyList<string> Tail(int count) => _file?.Tail(DateTime.Now, count) ?? [];
}

/// <summary>The app log's files: masking, the daily cap and retention (<see cref="AppLog"/>).</summary>
public sealed class LogFile(string folder, string? profile, long capBytes = LogFile.Cap)
{
    /// <summary>At most this much a day; then one line says so and nothing more is written until the next day.</summary>
    public const long Cap = 1024 * 1024;

    public const int KeepDays = 7;

    private readonly Lock _gate = new();
    private readonly Regex? _profile = ProfilePattern(profile);
    private DateOnly _day;
    private long _written;
    private bool _capped;

    public string Folder { get; } = folder;

    public string PathFor(DateTime day) => Path.Combine(Folder, $"shturmap-{day:yyyy-MM-dd}.log");

    public void Write(DateTime now, LogLevel level, string message)
    {
        var line = string.Create(CultureInfo.InvariantCulture,
            $"{now:yyyy-MM-dd HH:mm:ss.fff} {level.ToString().ToUpperInvariant()} {Mask(message, _profile)}{Environment.NewLine}");
        lock (_gate)
        {
            try
            {
                var path = PathFor(now);
                var today = DateOnly.FromDateTime(now);
                if (today != _day)
                {
                    _day = today;
                    _written = File.Exists(path) ? new FileInfo(path).Length : 0;
                    _capped = false;
                }
                if (_capped)
                    return;
                var bytes = Encoding.UTF8.GetByteCount(line);
                if (_written + bytes > capBytes)
                {
                    _capped = true;
                    line = string.Create(CultureInfo.InvariantCulture,
                        $"{now:yyyy-MM-dd HH:mm:ss.fff} WARN Today's log reached {capBytes / 1024} KB; nothing more is written until tomorrow.{Environment.NewLine}");
                    bytes = Encoding.UTF8.GetByteCount(line);
                }
                Directory.CreateDirectory(Folder);
                File.AppendAllText(path, line);
                _written += bytes;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>Removes days older than a week, and the logs from when the app was called Spotter.</summary>
    public void Prune(DateTime now)
    {
        try
        {
            if (!Directory.Exists(Folder))
                return;
            var oldest = DateOnly.FromDateTime(now).AddDays(-KeepDays);
            foreach (var file in Directory.EnumerateFiles(Folder, "*.log"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var spotter = name.StartsWith("spotter-", StringComparison.OrdinalIgnoreCase);
                var old = name.StartsWith("shturmap-", StringComparison.OrdinalIgnoreCase) &&
                          DateOnly.TryParseExact(name["shturmap-".Length..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) &&
                          day < oldest;
                if (spotter || old)
                    File.Delete(file);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    public IReadOnlyList<string> Tail(DateTime day, int count)
    {
        lock (_gate)
        {
            try
            {
                var path = PathFor(day);
                if (!File.Exists(path))
                    return [];
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var lines = new Queue<string>();
                while (reader.ReadLine() is { } line)
                {
                    lines.Enqueue(Mask(line, _profile));
                    if (lines.Count > count)
                        lines.Dequeue();
                }
                return lines.ToList();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return [];
            }
        }
    }

    /// <summary>
    /// The text with <paramref name="profile"/> (C:\Users\name) written as %USERPROFILE%, in any case and with either
    /// slash; that covers Documents, OneDrive and AppData under it. "C:\Users\names" stays: a name only ends at a
    /// separator or a character that can't be part of it.
    /// </summary>
    public static string Mask(string text, string? profile) => Mask(text, ProfilePattern(profile));

    private static string Mask(string text, Regex? profile) => profile is null ? text : profile.Replace(text, "%USERPROFILE%");

    private static Regex? ProfilePattern(string? profile)
    {
        var trimmed = profile?.TrimEnd('\\', '/');
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length < 4)
            return null;
        var parts = trimmed.Split('\\', '/').Select(Regex.Escape);
        return new Regex(string.Join(@"[\\/]+", parts) + @"(?![\w\-]|\.\w)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
