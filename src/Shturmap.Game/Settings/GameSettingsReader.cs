using System.Text.Json;
using Shturmap.Game.Install;

namespace Shturmap.Game.Settings;

/// <param name="ScreenshotKeys">Display names of the keys bound to "MakeScreenshot", e.g. ["PrtSc", "Home"]. Empty if unbound.</param>
/// <param name="Language">The game's UI language code, e.g. "en".</param>
public sealed record GameSettings(IReadOnlyList<string> ScreenshotKeys, string? Language, bool Found);

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

    internal static IReadOnlyList<string> ScreenshotKeys(string controlJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(controlJson);
            if (!doc.RootElement.TryGetProperty("keyBindings", out var bindings) || bindings.ValueKind != JsonValueKind.Array)
                return [];
            foreach (var binding in bindings.EnumerateArray())
            {
                if (!binding.TryGetProperty("keyName", out var name) || name.GetString() != "MakeScreenshot")
                    continue;
                var keys = new List<string>();
                if (binding.TryGetProperty("variants", out var variants) && variants.ValueKind == JsonValueKind.Array)
                {
                    foreach (var variant in variants.EnumerateArray())
                    {
                        if (!variant.TryGetProperty("keyCode", out var codes) || codes.ValueKind != JsonValueKind.Array)
                            continue;
                        var combo = codes.EnumerateArray().Select(c => DisplayName(c.GetString() ?? "")).Where(s => s.Length > 0).ToList();
                        if (combo.Count > 0)
                            keys.Add(string.Join("+", combo));
                    }
                }
                return keys;
            }
        }
        catch (JsonException)
        {
        }
        return [];
    }

    internal static string? Language(string gameJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(gameJson);
            return doc.RootElement.TryGetProperty("Language", out var lang) && lang.ValueKind == JsonValueKind.String ? lang.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Unity KeyCode names → what is printed on the key.
    private static string DisplayName(string keyCode) => keyCode switch
    {
        "SysReq" or "Print" => "PrtSc",
        "BackQuote" => "`",
        "LeftControl" => "Ctrl",
        "RightControl" => "Right Ctrl",
        "LeftShift" => "Shift",
        "RightShift" => "Right Shift",
        "LeftAlt" => "Alt",
        "RightAlt" => "Alt Gr",
        _ when keyCode.StartsWith("Alpha", StringComparison.Ordinal) => keyCode["Alpha".Length..],
        _ when keyCode.StartsWith("Keypad", StringComparison.Ordinal) => "Num " + keyCode["Keypad".Length..],
        _ => keyCode,
    };
}
