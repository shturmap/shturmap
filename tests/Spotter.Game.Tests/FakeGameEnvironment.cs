using Microsoft.Win32;
using Spotter.Game.Install;

namespace Spotter.Game.Tests;

/// <summary>An in-memory machine: registry values, files and folders.</summary>
internal sealed class FakeGameEnvironment : IGameEnvironment
{
    private readonly Dictionary<string, string> _registry = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dirs = new(StringComparer.OrdinalIgnoreCase);

    public string DocumentsFolder { get; set; } = @"C:\Users\Player\Documents";

    public string RoamingAppData { get; set; } = @"C:\Users\Player\AppData\Roaming";

    public FakeGameEnvironment Registry(RegistryHive hive, RegistryView view, string subKey, string name, string value)
    {
        _registry[$"{hive}|{view}|{subKey}|{name}"] = value;
        return this;
    }

    public FakeGameEnvironment File(string path, string content = "")
    {
        _files[path] = content;
        Dir(Path.GetDirectoryName(path)!);
        return this;
    }

    public FakeGameEnvironment Dir(string path)
    {
        for (var p = path; !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
            _dirs.Add(p.TrimEnd('\\'));
        return this;
    }

    /// <summary>A game folder with the exe and some log sessions.</summary>
    public FakeGameEnvironment Game(string root, params string[] sessions)
    {
        File(Path.Combine(root, "EscapeFromTarkov.exe"));
        foreach (var session in sessions)
            Dir(Path.Combine(root, "Logs", session));
        return this;
    }

    public string? ReadRegistryString(RegistryHive hive, RegistryView view, string subKey, string valueName) =>
        _registry.GetValueOrDefault($"{hive}|{view}|{subKey}|{valueName}");

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) => _dirs.Contains(path.TrimEnd('\\'));

    public IEnumerable<string> EnumerateDirectories(string path)
    {
        var prefix = path.TrimEnd('\\') + "\\";
        return _dirs.Where(d => d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !d[prefix.Length..].Contains('\\')).ToList();
    }

    public string? ReadAllText(string path) => _files.GetValueOrDefault(path);
}
