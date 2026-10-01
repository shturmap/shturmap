using Microsoft.Win32;

namespace Shturmap.Game.Install;

/// <summary>
/// The slice of the machine that install discovery looks at. Abstracted so the BSG-launcher and Steam layouts
/// can both be tested without either being installed.
/// </summary>
public interface IGameEnvironment
{
    string? ReadRegistryString(RegistryHive hive, RegistryView view, string subKey, string valueName);

    /// <summary>The user's Documents folder, following OneDrive or other redirection.</summary>
    string DocumentsFolder { get; }

    /// <summary>%APPDATA% (Roaming).</summary>
    string RoamingAppData { get; }

    bool FileExists(string path);

    bool DirectoryExists(string path);

    IEnumerable<string> EnumerateDirectories(string path);

    /// <summary>The file's text, or null if it cannot be read.</summary>
    string? ReadAllText(string path);
}

public sealed class WindowsGameEnvironment : IGameEnvironment
{
    public string? ReadRegistryString(RegistryHive hive, RegistryView view, string subKey, string valueName)
    {
        try
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var key = root.OpenSubKey(subKey);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    // Environment.GetFolderPath asks the shell (SHGetKnownFolderPath), so a Documents folder moved to OneDrive
    // or another drive is found where it really is.
    public string DocumentsFolder => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

    public string RoamingAppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path).ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public string? ReadAllText(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
