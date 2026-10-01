using System.Text.Json;
using Spotter.Core.Logs;
using Spotter.Core.Quests;

namespace Spotter.Data.Progress;

/// <summary>
/// Reads quest states from a TarkovEyes save (%APPDATA%\TarkovEyes\local-data\progress.json). The file has no
/// per-quest times, so every state is dated to the file's last change; anything the game logged later wins.
/// </summary>
public static class TarkovEyesImport
{
    public static string DefaultPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TarkovEyes", "local-data", "progress.json");

    public static IReadOnlyList<QuestObservation> Read(string path)
    {
        if (!File.Exists(path))
            return [];
        var at = File.GetLastWriteTime(path);
        var result = new List<QuestObservation>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != JsonValueKind.Object)
                return [];
            foreach (var (key, mode) in new[] { ("pvp", GameMode.Pvp), ("pve", GameMode.Pve), ("seasonal", GameMode.Seasonal) })
            {
                if (!profiles.TryGetProperty(key, out var profile) || !profile.TryGetProperty("quests", out var quests) ||
                    quests.ValueKind != JsonValueKind.Object)
                    continue;
                foreach (var quest in quests.EnumerateObject())
                {
                    QuestState? state = quest.Value.GetString() switch
                    {
                        "active" => QuestState.Active,
                        "completed" => QuestState.Completed,
                        "failed" => QuestState.Failed,
                        _ => null,
                    };
                    if (state is not null)
                        result.Add(new QuestObservation(mode, quest.Name, state.Value, ObservationSource.Import, at,
                            $"tarkoveyes:{key}:{quest.Name}:{state}"));
                }
            }
        }
        catch (JsonException)
        {
            return [];
        }
        return result;
    }
}
