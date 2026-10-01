using System.Collections.Concurrent;
using Shturmap.Core.Screenshots;

namespace Shturmap.Game.Screenshots;

/// <param name="CreatedAt">Local time the file appeared, to the second (the name only has minutes).</param>
public sealed record ScreenshotSeen(string Path, ScreenshotInfo Info, DateTime CreatedAt);

/// <summary>
/// Reports new screenshots the game writes. Only the file name is needed for a position, so a fix is reported
/// as soon as the file is created. Screenshots already in the folder at start are remembered, not reported.
/// A slow rescan backs up the file notifications, which can drop events when many arrive at once.
/// </summary>
public sealed class ScreenshotWatcher : IDisposable
{
    private readonly ConcurrentDictionary<string, byte> _known = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private FileSystemWatcher? _parentWatcher;
    private Timer? _rescan;

    public ScreenshotWatcher(string folder) => Folder = folder;

    public string Folder { get; }

    public bool FolderExists => Directory.Exists(Folder);

    public event Action<ScreenshotSeen>? ScreenshotTaken;

    public void Start()
    {
        if (Directory.Exists(Folder))
            WatchFolder(existingAreOld: true);
        else
            WatchForFolder();
        _rescan = new Timer(_ => Rescan(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    /// <summary>Screenshots already present that were taken within the window, newest first.</summary>
    public IReadOnlyList<ScreenshotSeen> Recent(TimeSpan window)
    {
        if (!Directory.Exists(Folder))
            return [];
        var since = DateTime.Now - window;
        return Directory.EnumerateFiles(Folder)
            .Select(TryRead)
            .OfType<ScreenshotSeen>()
            .Where(s => s.CreatedAt >= since)
            .OrderByDescending(s => s.CreatedAt)
            .ToList();
    }

    public void Dispose()
    {
        _rescan?.Dispose();
        _watcher?.Dispose();
        _parentWatcher?.Dispose();
    }

    private void WatchFolder(bool existingAreOld)
    {
        var existing = Directory.EnumerateFiles(Folder).ToList();
        if (existingAreOld)
        {
            foreach (var file in existing)
                _known.TryAdd(file, 0);
        }

        _watcher = new FileSystemWatcher(Folder)
        {
            NotifyFilter = NotifyFilters.FileName,
            IncludeSubdirectories = false,
            InternalBufferSize = 64 * 1024,
        };
        _watcher.Created += (_, e) => Consider(e.FullPath);
        _watcher.Renamed += (_, e) => Consider(e.FullPath);
        _watcher.EnableRaisingEvents = true;

        // A folder that appeared after Start was created by the game for this very screenshot.
        if (!existingAreOld)
        {
            foreach (var file in existing)
                Consider(file);
        }
    }

    // The game creates the Screenshots folder on the first screenshot; wait for it.
    private void WatchForFolder()
    {
        var parent = Path.GetDirectoryName(Folder);
        while (parent is not null && !Directory.Exists(parent))
            parent = Path.GetDirectoryName(parent);
        if (parent is null)
            return;
        _parentWatcher = new FileSystemWatcher(parent)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
        };
        _parentWatcher.Created += (_, _) =>
        {
            lock (_known)
            {
                if (_watcher is null && Directory.Exists(Folder))
                    WatchFolder(existingAreOld: false);
            }
        };
        _parentWatcher.EnableRaisingEvents = true;
    }

    private void Rescan()
    {
        if (_watcher is null)
        {
            lock (_known)
            {
                if (_watcher is null && Directory.Exists(Folder))
                    WatchFolder(existingAreOld: false); // appeared without a notification
            }
            return;
        }
        try
        {
            foreach (var file in Directory.EnumerateFiles(Folder))
                Consider(file);
        }
        catch (IOException)
        {
        }
    }

    private void Consider(string path)
    {
        if (!_known.TryAdd(path, 0))
            return;
        if (TryRead(path) is { } seen)
            ScreenshotTaken?.Invoke(seen);
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
        catch (IOException)
        {
            created = DateTime.Now;
        }
        return new ScreenshotSeen(path, info, created);
    }

    /// <summary>Waits until the game has finished writing the image (size stable and readable).</summary>
    public static async Task<bool> WaitUntilCompleteAsync(string path, TimeSpan timeout, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + timeout;
        long lastSize = -1;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var size = new FileInfo(path).Length;
                if (size > 0 && size == lastSize)
                {
                    using var _ = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return true;
                }
                lastSize = size;
            }
            catch (IOException)
            {
                // still being written
            }
            await Task.Delay(150, ct);
        }
        return false;
    }
}
