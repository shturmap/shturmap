using System.Text.Json;
using Shturmap.Game.Install;

namespace Shturmap.Game.Settings;

/// <param name="ScreenshotKeys">The keys bound to "MakeScreenshot", e.g. Print and Home, named when shown ("PrtSc",
/// "Home"; <see cref="GameKey.Name"/>). Empty if unbound.</param>
/// <param name="Language">The game's UI language code, e.g. "en".</param>
public sealed record GameSettings(IReadOnlyList<GameKey> ScreenshotKeys, string? Language, bool Found);

/// <summary>Reads Control.ini and Game.ini (JSON despite the extension), read-only.</summary>
public sealed class GameSettingsReader(IGameEnvironment env)
{
    public GameSettings Read(string settingsFolder)
    {
        var control = env.ReadAllText(Path.Combine(settingsFolder, "Control.ini"));
        var game = env.ReadAllText(Path.Combine(settingsFolder, "Game.ini"));
        return new GameSettings(
            control is null ? [] : ScreenshotKeys(control),
            game is null ? null : Language(game),
            control is not null || game is not null);
    }

    internal static IReadOnlyList<GameKey> ScreenshotKeys(string controlJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(controlJson);
            if (!Property(doc.RootElement, "keyBindings", out var bindings) || bindings.ValueKind != JsonValueKind.Array)
                return [];
            foreach (var binding in bindings.EnumerateArray())
            {
                if (!Property(binding, "keyName", out var name) || name.ValueKind != JsonValueKind.String || name.GetString() != "MakeScreenshot")
                    continue;
                var keys = new List<GameKey>();
                if (Property(binding, "variants", out var variants) && variants.ValueKind == JsonValueKind.Array)
                {
                    foreach (var variant in variants.EnumerateArray())
                    {
                        if (!Property(variant, "keyCode", out var codes) || codes.ValueKind != JsonValueKind.Array)
                            continue;
                        var combo = codes.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.String)
                            .Select(c => c.GetString() ?? "").Where(s => s.Length > 0 && !s.Contains('+')).ToList();
                        if (combo.Count > 0)
                            keys.Add(new GameKey(string.Join("+", combo)));
                    }
                }
                return keys;
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            // Not JSON, or JSON of another shape than the game writes today: no keys known, never a crash.
        }
        return [];
    }

    internal static string? Language(string gameJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(gameJson);
            return Property(doc.RootElement, "Language", out var lang) && lang.ValueKind == JsonValueKind.String ? lang.GetString() : null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    // TryGetProperty throws on anything but an object (a number where a binding should be, a file that is a list).
    private static bool Property(JsonElement e, string name, out JsonElement value)
    {
        value = default;
        return e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out value);
    }
}
