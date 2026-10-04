using System.Collections.Concurrent;
using Shturmap.Core.Screenshots;

namespace Shturmap.Game.Screenshots;

/// <param name="CreatedAt">Local time the file appeared, to the second (the name only has minutes).</param>
public sealed record ScreenshotSeen(string Path, ScreenshotInfo Info, DateTime CreatedAt);

/// <summary>
/// Reports new screenshots the game writes. Only the file name is needed for a position, so a fix is reported
/// as soon as the file is created; the images are never opened. Screenshots already in the folder at start are
/// remembered, not reported.
/// A slow rescan backs up the file notifications, which can drop events when many arrive at once, and sets the
/// watching up again when the folder was deleted and made anew or its notifications stopped.
/// </summary>
public sealed class ScreenshotWatcher : IDisposable
{
    private readonly ConcurrentDictionary<string, byte> _known = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private FileSystemWatcher? _watcher;
    private DateTime _watchedFolderCreatedUtc;
    private FileSystemWatcher? _parentWatcher;
    private Timer? _rescan;
    private bool _existingAreOld;
    private bool _disposed;

    public ScreenshotWatcher(string folder) => Folder = folder;

    public string Folder { get; }

    public bool FolderExists => Directory.Exists(Folder);

    public event Action<ScreenshotSeen>? ScreenshotTaken;

    /// <summary>
    /// Watching met something unexpected and goes on: what ("the screenshot rescan", "the screenshots folder's
    /// notifications", "a listener to a new screenshot") and why. Raised on a timer or notification thread, where an
    /// exception left alone would end the app.
    /// </summary>
    public event Action<string, Exception>? WatchProblem;

    /// <summary>Whether the screenshots folder itself is watched now.</summary>
    internal bool WatchesFolder
    {
        get
        {
            lock (_gate)
                return _watcher is not null;
        }
    }

    /// <summary>Whether a folder above is watched, for the screenshots folder to appear.</summary>
    internal bool WaitsForFolder
    {
        get
        {
            lock (_gate)
                return _parentWatcher is not null;
        }
    }

    public void Start()
    {
        Safely("the start of the screenshot watching", () =>
        {
            lock (_gate)
            {
                // What is in the folder now was taken before Shturmap looked, also if the folder can only be read
                // at a later rescan.
                _existingAreOld = Directory.Exists(Folder);
                if (_existingAreOld)
                    WatchFolder();
                else
                    WatchForFolder();
            }
        });
        _rescan = new Timer(_ => Rescan(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    /// <summary>Screenshots already present that were taken within the window, newest first.</summary>
    public IReadOnlyList<ScreenshotSeen> Recent(TimeSpan window)
    {
        if (!Directory.Exists(Folder))
            return [];
        // By the time that passed, not by the two clock times, which are an hour apart across a clock change.
        var now = DateTime.Now;
        return Directory.EnumerateFiles(Folder)
            .Select(TryRead)
            .OfType<ScreenshotSeen>()
            .Where(s => Shturmap.Core.Logs.WallClock.Elapsed(s.CreatedAt, now) <= window)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _rescan?.Dispose();
            _watcher?.Dispose();
            _watcher = null;
            _parentWatcher?.Dispose();
            _parentWatcher = null;
        }
    }

    // Under _gate. Files in the folder are remembered as old the first time, if the folder was there at start; a
    // folder that appeared later was created by the game for the very screenshot in it, and after a new set-up
    // whatever isn't known yet is new.
    private void WatchFolder()
    {
        if (_existingAreOld)
        {
            foreach (var file in Directory.EnumerateFiles(Folder))
                _known.TryAdd(file, 0);
        }

        var watcher = new FileSystemWatcher(Folder)
        {
            NotifyFilter = NotifyFilters.FileName,
            IncludeSubdirectories = false,
            InternalBufferSize = 64 * 1024,
        };
        watcher.Created += (_, e) => Safely("the screenshots folder's notifications", () => Consider(e.FullPath));
        watcher.Renamed += (_, e) => Safely("the screenshots folder's notifications", () => Consider(e.FullPath));
        // Notifications end when the folder is deleted, and some are lost when too many come at once: set up again,
        // and look at what is there.
        watcher.Error += (_, e) => Safely("the screenshots folder's notifications", () =>
        {
            Report("the screenshots folder's notifications", e.GetException());
            lock (_gate)
            {
                if (_disposed || _watcher != watcher)
                    return;
                Unwatch();
                Watch();
            }
            ConsiderExisting();
        });
        watcher.EnableRaisingEvents = true;
        _watcher = watcher;
        _watchedFolderCreatedUtc = Directory.GetCreationTimeUtc(Folder);
        _existingAreOld = false;
    }

    // Under _gate.
    private void Unwatch()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    // Under _gate: the folder if it is there, else a folder above it until it appears.
    private void Watch()
    {
        if (_watcher is not null)
            return;
        if (Directory.Exists(Folder))
            WatchFolder();
        else if (_parentWatcher is null)
            WatchForFolder();
    }

    // Under _gate. The game creates the Screenshots folder on the first screenshot; wait for it.
    private void WatchForFolder()
    {
        var parent = Path.GetDirectoryName(Folder);
        while (parent is not null && !Directory.Exists(parent))
            parent = Path.GetDirectoryName(parent);
        if (parent is null)
            return;
        var watcher = new FileSystemWatcher(parent)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
        };
        watcher.Created += (_, _) => Safely("the screenshots folder's notifications", () =>
        {
            lock (_gate)
            {
                if (_disposed || _watcher is not null || !Directory.Exists(Folder))
                    return;
                WatchFolder();
            }
            ConsiderExisting();
        });
        watcher.EnableRaisingEvents = true;
        _parentWatcher = watcher;
    }

    /// <summary>One rescan; the timer's, and the tests'.</summary>
    internal void Rescan() => Safely("the screenshot rescan", () =>
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            // A watcher whose folder is gone, was made anew, or whose notifications stopped watches nothing.
            if (_watcher is not null &&
                (!Directory.Exists(Folder) || !_watcher.EnableRaisingEvents || Directory.GetCreationTimeUtc(Folder) != _watchedFolderCreatedUtc))
                Unwatch();
            Watch(); // also a folder that appeared without a notification
            if (_watcher is null)
                return;
            // The folder is watched: the watch above it (all of Documents, at worst) has done its job.
            _parentWatcher?.Dispose();
            _parentWatcher = null;
        }
        ConsiderExisting();
    });

    private void ConsiderExisting()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(Folder))
                Consider(file);
        }
        catch (DirectoryNotFoundException)
        {
            // Deleted again in the meantime: nothing to look at, and the next rescan waits for it anew.
        }
    }

    /// <summary>
    /// A reported screenshot was deleted (<see cref="ScreenshotCleaner"/>): a later file of the same name is a new
    /// screenshot again. The name can repeat: the same minute, place, facing and raid clock.
    /// </summary>
    public void Forget(string path) => _known.TryRemove(path, out _);

    private void Consider(string path)
    {
        if (!_known.TryAdd(path, 0))
            return;
        if (!File.Exists(path))
        {
            // Gone before it could be looked at (a rescan listed it just as it was deleted): no new screenshot, and
            // a later file of this name is one. A missing file's creation time would read as the year 1601.
            _known.TryRemove(path, out _);
            return;
        }
        if (TryRead(path) is not { } seen)
            return;
        try
        {
            ScreenshotTaken?.Invoke(seen);
        }
        catch (Exception e)
        {
            // One listener's failure is no reason to miss the screenshots after this one.
            Report("a listener to a new screenshot", e);
        }
    }

    // Timer and notification callbacks run on pool threads: an exception that leaves one ends the process.
    private void Safely(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Report(what, e);
        }
    }

    private void Report(string what, Exception e)
    {
        try
        {
            WatchProblem?.Invoke(what, e);
        }
        catch (Exception)
        {
            // Nor is a failure of the one who listens to problems.
        }
    }

    private static ScreenshotSeen? TryRead(string path)
    {
        if (!ScreenshotName.TryParse(path, out var info))
            return null;
        DateTime created;
        try
        {
            created = File.GetCreationTime(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            created = DateTime.Now;
        }
        return new ScreenshotSeen(path, info, created);
    }
}
