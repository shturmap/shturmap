namespace Shturmap.Core;

/// <summary>
/// The 0.1.0 builds were one exe that .NET unpacked on its first start to %TEMP%\.net\&lt;exe name&gt;\&lt;bundle id&gt;,
/// one copy per version and file name (about 200 MB each). From 0.2.0 Shturmap is installed (Velopack) and nothing
/// unpacks there; the installed app removes those leftovers once (docs/DESIGN.md §8, "Distribution"): only folders of
/// that exact shape, named after Shturmap, holding Shturmap.dll, and none in use.
/// </summary>
public static class UnpackedCopies
{
    /// <summary>
    /// Which of <paramref name="candidates"/> are unpacked copies of Shturmap that may go: folders
    /// &lt;temp&gt;\.net\Shturmap…\&lt;id&gt; and nothing else. Paths only; whether one is in use is checked on delete.
    /// </summary>
    public static IReadOnlyList<string> Leftovers(string tempPath, IEnumerable<string> candidates)
    {
        var root = Normalize(Path.Combine(tempPath, ".net"));
        return candidates.Select(Normalize)
            .Where(c => IsCopy(c, root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Removes the leftovers, in the background of a start: what it did, for the log, and whether none had
    /// to stay (one in use: an old single exe still running).</summary>
    public static (IReadOnlyList<string> Done, bool Complete) RemoveLeftovers()
    {
        var done = new List<string>();
        var complete = true;
        var temp = Path.GetTempPath();
        var root = Path.Combine(temp, ".net");
        if (!Directory.Exists(root))
            return (done, complete);
        var candidates = Directory.EnumerateDirectories(root).SelectMany(SafeSubdirectories);
        foreach (var copy in Leftovers(temp, candidates))
        {
            if (!File.Exists(Path.Combine(copy, "Shturmap.dll")))
                continue;
            // Moved aside first: Windows refuses to rename a folder while a file in it is open, so a copy an old
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
                complete = false;
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
                complete = false;
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
        return (done, complete);
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
