namespace Shturmap.App.Rules;

/// <summary>
/// Whether a release feed's folder ("--update-feed &lt;folder&gt;", the dev build's feed) is on this PC: a full path on
/// a fixed local drive that exists. An update is code that runs as the player, so the one switch that names where
/// updates come from takes nothing a stranger could host: no network share in either slash form ("\\server\feed",
/// "//server/feed"), no device path, no mapped, removable or optical drive (2026-10-04: "//server/share" passed as
/// local, since only the backslash form was refused).
/// </summary>
public static class LocalFeed
{
    public static bool IsLocalFolder(string? folder) =>
        IsLocalFolder(folder, root => new DriveInfo(root).DriveType, Directory.Exists);

    /// <param name="driveType">The kind of drive a root is, asked with its letter in upper case ("C:\").</param>
    /// <param name="exists">Whether the folder is there.</param>
    public static bool IsLocalFolder(string? folder, Func<string, DriveType> driveType, Func<string, bool> exists)
    {
        // "C:\…" or "C:/…" and nothing else: a letter, a colon, a separator. That leaves out both share forms, device
        // paths ("\\?\C:\…") and paths relative to a drive ("C:feed").
        if (folder is null || folder.Length < 3 || !char.IsAsciiLetter(folder[0]) || folder[1] != ':' || folder[2] is not ('\\' or '/'))
            return false;
        try
        {
            return driveType($"{char.ToUpperInvariant(folder[0])}:\\") == DriveType.Fixed && exists(folder);
        }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
