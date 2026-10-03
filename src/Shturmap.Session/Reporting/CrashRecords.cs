using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Shturmap.Session.Reporting;

/// <summary>
/// Crash records on this PC, in %LOCALAPPDATA%\Shturmap\crashes: one JSON file each, written when it happens, masked
/// (<see cref="Redact"/>) and sent only as the player allows (<see cref="CrashPolicy"/>). A running Shturmap holds a
/// marker file there; one left behind that no one holds means a session ended without closing (docs/DESIGN.md §8,
/// "Reports").
/// </summary>
public sealed class CrashRecords(string folder, string? profile)
{
    /// <summary>Records are kept this long, sent or not; at most <see cref="KeepCount"/> of them.</summary>
    public const int KeepDays = 30;

    public const int KeepCount = 20;

    /// <summary>Log lines a crash record carries.</summary>
    public const int LogLines = 50;

    // Errors the app survives (an unobserved task) are recorded at most this often in one session.
    private const int MaxErrorsPerSession = 3;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

    private readonly Lock _gate = new();
    private readonly ConditionalWeakTable<Exception, object> _seen = [];
    private readonly HashSet<string> _signatures = [];
    private int _errors;

    public string Folder { get; } = folder;

    /// <summary>This run's id, in its marker and its records.</summary>
    public string Session { get; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>
    /// Writes a record of <paramref name="e"/>, in the crash path itself: synchronous, and it never throws. The same
    /// exception reported twice (the UI's handler, then the process's) is written once; errors the app survives at most
    /// three times a session, and the same one once.
    /// </summary>
    public CrashRecord? Record(Exception e, string source, bool fatal, ReportInfo info, IReadOnlyList<string> logTail, DateTime now)
    {
        try
        {
            lock (_gate)
            {
                if (_seen.TryGetValue(e, out _))
                    return null;
                _seen.Add(e, new object());
                var record = FromException(e, Session, source, fatal, info, logTail, now, profile);
                if (!fatal)
                {
                    var signature = record.Summary;
                    if (_errors >= MaxErrorsPerSession || !_signatures.Add(signature))
                        return null;
                    _errors++;
                }
                Write(record);
                return record;
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The record of an exception: its chain (outermost first, at most five), each with where in the code it
    /// was thrown; messages and log lines masked.</summary>
    public static CrashRecord FromException(Exception e, string session, string source, bool fatal, ReportInfo info,
        IReadOnlyList<string> logTail, DateTime now, string? profile)
    {
        var chain = new List<CrashException>();
        for (var current = e; current is not null && chain.Count < 5; current = Inner(current))
            chain.Add(new CrashException(current.GetType().FullName ?? current.GetType().Name, Clean(current.Message, profile, 1000), Frames(current)));
        return new CrashRecord(NewId(), session, now, fatal, source, chain, info.Version, info.Build, info.Windows,
            logTail.TakeLast(LogLines).Select(l => Clean(l, profile, 2000)).ToList());
    }

    private static Exception? Inner(Exception e) =>
        e is AggregateException { InnerExceptions.Count: > 0 } aggregate ? aggregate.InnerExceptions[0] : e.InnerException;

    private static string Clean(string text, string? profile, int max)
    {
        var clean = Redact.Text(text, profile);
        return clean.Length > max ? clean[..max] + "…" : clean;
    }

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>The frames where it was thrown, innermost first: module, method and, where symbols say, the file's name
    /// (never its folder) and line.</summary>
    public static IReadOnlyList<CrashFrame> Frames(Exception e)
    {
        var frames = new List<CrashFrame>();
        foreach (var frame in new StackTrace(e, fNeedFileInfo: true).GetFrames())
        {
            if (frame.GetMethod() is not { } method)
                continue;
            var module = method.Module.Assembly.GetName().Name ?? "?";
            var file = frame.GetFileName() is { Length: > 0 } path ? Path.GetFileName(path) : null;
            var line = frame.GetFileLineNumber();
            frames.Add(new CrashFrame(module, FunctionName(method), file, line > 0 ? line : null,
                module.StartsWith("Shturmap", StringComparison.Ordinal)));
            if (frames.Count == 64)
                break;
        }
        return frames;
    }

    /// <summary>"Shturmap.App.MainWindow.Apply"; compiler-made methods by the method they belong to
    /// ("MainWindow.OnUninstallConfirmClick" for its async state machine, "… (lambda)" for a lambda).</summary>
    public static string FunctionName(MethodBase method)
    {
        var type = method.DeclaringType;
        var name = Readable(method.Name);
        // An async or iterator state machine: "<OnUninstallConfirmClick>d__42.MoveNext" in the type that declared it.
        if (type is { Name: ['<', ..] } && type.Name.LastIndexOf('>') is var end and > 1)
        {
            name = Readable(type.Name[1..end]);
            type = type.DeclaringType;
        }
        while (type is { Name: ['<', ..] })
            type = type.DeclaringType;
        return type is null ? name : $"{type.FullName?.Replace('+', '.') ?? type.Name}.{name}";
    }

    // "<Apply>g__Local|5_0" is the local function Apply.Local; "<Apply>b__12_0" a lambda in Apply.
    private static string Readable(string name)
    {
        if (name is not ['<', ..] || name.IndexOf('>') is not (var close and > 1))
            return name;
        var outer = name[1..close];
        var rest = name[(close + 1)..];
        if (rest.StartsWith("g__", StringComparison.Ordinal))
            return $"{outer}.{rest[3..].Split('|')[0]}";
        return outer + " (lambda)";
    }

    // ---- the files ----

    private string PathFor(CrashRecord record) =>
        Path.Combine(Folder, $"{record.At.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}-{record.Id}.json");

    public void Write(CrashRecord record)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(PathFor(record), JsonSerializer.Serialize(record, Json));
    }

    /// <summary>The records not answered yet (or approved but not sent), oldest first.</summary>
    public IReadOnlyList<CrashRecord> Waiting() => All().Where(r => r.State != CrashState.Kept).ToList();

    public IReadOnlyList<CrashRecord> All()
    {
        if (!Directory.Exists(Folder))
            return [];
        var records = new List<CrashRecord>();
        foreach (var file in Directory.EnumerateFiles(Folder, "*.json").Order(StringComparer.Ordinal))
        {
            if (Path.GetFileName(file).StartsWith("running-", StringComparison.Ordinal))
                continue;
            try
            {
                if (JsonSerializer.Deserialize<CrashRecord>(File.ReadAllText(file), Json) is { } record)
                    records.Add(record);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            {
            }
        }
        return records;
    }

    public void SetState(CrashRecord record, CrashState state)
    {
        try
        {
            Write(record with { State = state });
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>A sent record is deleted: it is in the developer's report list now.</summary>
    public void Remove(CrashRecord record)
    {
        try
        {
            File.Delete(PathFor(record));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Deletes records older than 30 days, and the oldest beyond twenty.</summary>
    public void Prune(DateTime now)
    {
        var records = All().OrderByDescending(r => r.At).ToList();
        foreach (var (record, i) in records.Select((r, i) => (r, i)))
        {
            if (i >= KeepCount || record.At < now.AddDays(-KeepDays))
                Remove(record);
        }
    }

    // ---- the running marker ----

    private sealed record Marker(string Session, DateTime Started, string Version, string Build, string Windows);

    /// <summary>
    /// The marker this run holds open until it closes; the process's end lets go of it either way, and a clean close
    /// deletes it (<see cref="RunningMarker.Dispose"/>).
    /// </summary>
    public RunningMarker? MarkRunning(DateTime now, ReportInfo info)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var path = Path.Combine(Folder, $"running-{Session}.json");
            var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            JsonSerializer.Serialize(stream, new Marker(Session, now, info.Version, info.Build, info.Windows));
            stream.Flush();
            return new RunningMarker(stream, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Records for sessions that ended without closing: a marker no running Shturmap holds, whose session left no
    /// crash record of its own. One from before Windows last started is a shutdown or restart while Shturmap ran, not
    /// a crash, and is dropped. <paramref name="logBetween"/> gives that session's last log lines.
    /// </summary>
    public IReadOnlyList<CrashRecord> CollectUnexpectedExits(DateTime now, DateTime bootTime, Func<DateTime, DateTime, IReadOnlyList<string>> logBetween)
    {
        if (!Directory.Exists(Folder))
            return [];
        var found = new List<CrashRecord>();
        var recorded = All().Select(r => r.Session).ToHashSet();
        foreach (var path in Directory.EnumerateFiles(Folder, "running-*.json"))
        {
            if (Path.GetFileName(path) == $"running-{Session}.json")
                continue;
            Marker? marker;
            try
            {
                // Held by a Shturmap that still runs: opening it alone fails.
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    marker = JsonSerializer.Deserialize<Marker>(stream);
                File.Delete(path);
            }
            catch (IOException)
            {
                continue;
            }
            catch (Exception e) when (e is UnauthorizedAccessException or JsonException)
            {
                TryDelete(path);
                continue;
            }
            if (marker is null || marker.Started < bootTime || recorded.Contains(marker.Session))
                continue;
            var record = new CrashRecord(NewId(), marker.Session, now, true, "exit", [], marker.Version, marker.Build, marker.Windows,
                logBetween(marker.Started, now).TakeLast(LogLines).Select(l => Clean(l, profile, 2000)).ToList());
            try
            {
                Write(record);
                found.Add(record);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        return found;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>This run's marker; disposing it on a clean close deletes it.</summary>
public sealed class RunningMarker(FileStream stream, string path) : IDisposable
{
    private int _disposed;

    public string Path { get; } = path;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;
        try
        {
            stream.Dispose();
            File.Delete(Path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
