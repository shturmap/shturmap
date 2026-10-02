namespace Shturmap.Core;

/// <summary>
/// The release is one exe that .NET unpacks on its first start to %TEMP%\.net\&lt;exe name&gt;\&lt;bundle id&gt;, and
/// every other version or file name leaves its own copy there (about 200 MB each). A start from such a copy removes
/// the others: only folders of that exact shape, named after Shturmap, holding Shturmap.dll, and none in use.
/// </summary>
public static class UnpackedCopies
{
    /// <summary>
    /// Which of <paramref name="candidates"/> are other unpacked copies of Shturmap that may go: none unless this
    /// app runs from one itself (<paramref name="baseDirectory"/> is &lt;temp&gt;\.net\Shturmap…\&lt;id&gt;), and
    /// then only folders of the same shape, never this one. Paths only; whether one is in use is checked on delete.
    /// </summary>
    public static IReadOnlyList<string> Others(string baseDirectory, string tempPath, IEnumerable<string> candidates)
    {
        var root = Normalize(Path.Combine(tempPath, ".net"));
        var own = Normalize(baseDirectory);
        if (!IsCopy(own, root))
            return [];
        return candidates.Select(Normalize)
            .Where(c => IsCopy(c, root) && !string.Equals(c, own, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Whether this app runs from a single exe's unpacked copy (else from a folder build or a developer's).</summary>
    public static bool RunsFromCopy(string baseDirectory, string tempPath) =>
        IsCopy(Normalize(baseDirectory), Normalize(Path.Combine(tempPath, ".net")));

    /// <summary>Removes the other unpacked copies, in the background of a start; returns what it did, for the log.</summary>
    public static IReadOnlyList<string> RemoveOthers()
    {
        var done = new List<string>();
        var temp = Path.GetTempPath();
        var root = Path.Combine(temp, ".net");
        if (!Directory.Exists(root))
            return done;
        var candidates = Directory.EnumerateDirectories(root).SelectMany(SafeSubdirectories);
        foreach (var copy in Others(AppContext.BaseDirectory, temp, candidates))
        {
            if (!File.Exists(Path.Combine(copy, "Shturmap.dll")))
                continue;
            // Moved aside first: Windows refuses to rename a folder while a file in it is open, so a copy another
            // Shturmap runs from stays whole instead of losing the files that weren't locked.
            var doomed = copy + ".old";
            try
            {
                if (Directory.Exists(doomed))
                    Directory.Delete(doomed, true);
                Directory.Move(copy, doomed);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                done.Add($"kept {copy} (in use)");
                continue;
            }
            try
            {
                Directory.Delete(doomed, true);
                done.Add($"removed {copy}");
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                done.Add($"could not remove {doomed}: {e.Message}");
            }
            // The exe's own folder (…\.net\Shturmap-0.1.0-win-x64) goes when its last copy has.
            var parent = Path.GetDirectoryName(copy)!;
            try
            {
                if (!Directory.EnumerateFileSystemEntries(parent).Any())
                    Directory.Delete(parent);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
        }
        return done;
    }

    // <root>\<name>\<id>, two levels below the unpack root, under a name that is Shturmap's ("Shturmap",
    // "Shturmap-0.1.0-win-x64", "Shturmap-0.1.0-win-x64 (1)" after a browser renamed the download).
    private static bool IsCopy(string path, string root)
    {
        var parent = Path.GetDirectoryName(path);
        return parent is not null && Path.GetFileName(path).Length > 0 &&
               string.Equals(Path.GetDirectoryName(parent), root, StringComparison.OrdinalIgnoreCase) &&
               Path.GetFileName(parent).StartsWith("Shturmap", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static IEnumerable<string> SafeSubdirectories(string folder)
    {
        try
        {
            return Directory.GetDirectories(folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
